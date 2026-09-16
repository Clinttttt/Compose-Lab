using System.Globalization;
using ComposeLab.Api.Domain.Topology;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ComposeLab.Api.Domain.Compose;

/// <summary>
/// Reads a Compose file into a topology, or explains why it cannot.
/// </summary>
/// <remarks>
/// Every key encountered falls into one of three buckets: modeled, real Compose that ComposeLab does not
/// model, or not Compose at all. The last two both block, which is what keeps applying a file
/// all-or-nothing — a partial apply would leave the learner with a topology that quietly disagrees with
/// what they wrote.
/// <para>
/// All findings are collected before returning, rather than stopping at the first, so a learner fixes a
/// file in one pass instead of playing whack-a-mole.
/// </para>
/// </remarks>
public static class ComposeParser
{
    public static ComposeParseResult Parse(string yaml) => new Walker(yaml).Run();

    private sealed class Walker(string yaml)
    {
        private static readonly IReadOnlySet<string> ModeledServiceKeys =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "build", "depends_on", "environment", "healthcheck", "image", "networks", "ports",
                "volumes"
            };

        private readonly List<ComposeFinding> _findings = [];

        public ComposeParseResult Run()
        {
            if (string.IsNullOrWhiteSpace(yaml))
            {
                Add(ComposeFindingCode.UnexpectedShape, string.Empty, 1, 1,
                    "There is nothing to apply.",
                    "Write a Compose file with at least a 'services' section, or keep the architecture you "
                        + "already have.");

                return ComposeParseResult.Blocked(_findings);
            }

            YamlMappingNode root;

            try
            {
                YamlStream stream = [];
                using StringReader reader = new(yaml);
                stream.Load(reader);

                if (stream.Documents.Count == 0)
                {
                    Add(ComposeFindingCode.UnexpectedShape, string.Empty, 1, 1,
                        "There is nothing to apply.",
                        "Write a Compose file with at least a 'services' section.");

                    return ComposeParseResult.Blocked(_findings);
                }

                if (stream.Documents[0].RootNode is not YamlMappingNode mapping)
                {
                    YamlNode node = stream.Documents[0].RootNode;

                    Add(ComposeFindingCode.UnexpectedShape, string.Empty, (int)node.Start.Line,
                        (int)node.Start.Column,
                        "A Compose file is a mapping of top-level sections.",
                        "Start the file with 'services:' and indent each service beneath it.");

                    return ComposeParseResult.Blocked(_findings);
                }

                root = mapping;
            }
            catch (YamlException exception)
            {
                Add(ComposeFindingCode.SyntaxError, string.Empty, (int)exception.Start.Line,
                    (int)exception.Start.Column,
                    $"This is not valid YAML: {exception.Message}",
                    "Check the indentation and punctuation around this line. Every nested level is indented "
                        + "further than its parent, and a key is followed by a colon and a space.");

                return ComposeParseResult.Blocked(_findings);
            }

            List<ContainerService> services = [];
            List<ContainerNetwork> networks = [];
            List<ContainerVolume> volumes = [];

            foreach (KeyValuePair<YamlNode, YamlNode> section in root)
            {
                string key = Scalar(section.Key) ?? string.Empty;

                switch (key)
                {
                    case "services":
                        services = ReadServices(section.Value);
                        break;

                    case "networks":
                        networks = ReadDeclarations(section.Value, "networks", ComposeKeyCatalog.Network,
                            (name, composeName) => new ContainerNetwork { Name = name, ComposeName = composeName });
                        break;

                    case "volumes":
                        volumes = ReadDeclarations(section.Value, "volumes", ComposeKeyCatalog.Volume,
                            (name, composeName) => new ContainerVolume { Name = name, ComposeName = composeName });
                        break;

                    default:
                        ClassifyKey(key, section.Key, key, ComposeKeyCatalog.TopLevel, TopLevelSuggestion(key));
                        break;
                }
            }

            return _findings.Count > 0
                ? ComposeParseResult.Blocked(_findings)
                : ComposeParseResult.Applicable(new ApplicationTopology
                {
                    Services = services,
                    Networks = networks,
                    Volumes = volumes
                });
        }

        private static string TopLevelSuggestion(string key) => key == "version"
            ? "The 'version' key is obsolete — Compose ignores it. Delete the line."
            : "Remove it to work on this architecture in ComposeLab.";

        private List<ContainerService> ReadServices(YamlNode node)
        {
            List<ContainerService> services = [];

            if (Mapping(node, "services") is not { } mapping)
            {
                return services;
            }

            foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping)
            {
                string name = Scalar(entry.Key) ?? string.Empty;
                string path = $"services.{name}";

                if (Mapping(entry.Value, path) is not { } body)
                {
                    continue;
                }

                services.Add(ReadService(name, path, body));
            }

            return services;
        }

        private ContainerService ReadService(string name, string path, YamlMappingNode body)
        {
            string? image = null;
            string? build = null;
            List<PortMapping> ports = [];
            List<NetworkAttachment> attachments = [];
            List<VolumeMount> mounts = [];
            List<ServiceDependency> dependencies = [];
            Dictionary<string, string> environment = new(StringComparer.Ordinal);
            HealthCheckDeclaration? healthCheck = null;

            foreach (KeyValuePair<YamlNode, YamlNode> entry in body)
            {
                string key = Scalar(entry.Key) ?? string.Empty;
                string keyPath = $"{path}.{key}";

                switch (key)
                {
                    case "image":
                        image = Scalar(entry.Value, keyPath);
                        break;

                    case "build":
                        build = Scalar(entry.Value, keyPath);

                        if (build is null && entry.Value is YamlMappingNode)
                        {
                            Add(ComposeFindingCode.ValueNotModeled, keyPath, entry.Value,
                                "ComposeLab models the short build form only — a path to a build context.",
                                "Replace the block with 'build: ./path'. Options such as dockerfile, args, and "
                                    + "target are not modeled yet.");
                        }

                        break;

                    case "ports":
                        ports = ReadPorts(entry.Value, keyPath);
                        break;

                    case "environment":
                        environment = ReadEnvironment(entry.Value, keyPath);
                        break;

                    case "volumes":
                        mounts = ReadMounts(entry.Value, keyPath);
                        break;

                    case "networks":
                        attachments = ReadAttachments(entry.Value, keyPath);
                        break;

                    case "depends_on":
                        dependencies = ReadDependencies(entry.Value, keyPath);
                        break;

                    case "healthcheck":
                        healthCheck = ReadHealthCheck(entry.Value, keyPath);
                        break;

                    default:
                        ClassifyKey(key, entry.Key, keyPath, ComposeKeyCatalog.Service,
                            "Remove it to work on this architecture in ComposeLab, or keep it in your own "
                                + "Compose file and model the rest here.");
                        break;
                }
            }

            return new ContainerService
            {
                Name = name,
                Image = image,
                BuildContext = build,
                Ports = ports,
                Networks = attachments,
                Volumes = mounts,
                Dependencies = dependencies,
                Environment = environment,
                HealthCheck = healthCheck
            };
        }

        private List<PortMapping> ReadPorts(YamlNode node, string path)
        {
            List<PortMapping> ports = [];

            if (Sequence(node, path) is not { } sequence)
            {
                return ports;
            }

            for (int index = 0; index < sequence.Children.Count; index++)
            {
                YamlNode item = sequence.Children[index];
                string itemPath = $"{path}[{index}]";

                if (Scalar(item) is not { } text)
                {
                    Add(ComposeFindingCode.ValueNotModeled, itemPath, item,
                        "ComposeLab models the short port syntax only.",
                        "Write the mapping as \"8080:8080\" instead of a block with target and published "
                            + "keys.");

                    continue;
                }

                if (TryReadPort(text, out PortMapping? port))
                {
                    ports.Add(port);

                    continue;
                }

                Add(ComposeFindingCode.ValueNotModeled, itemPath, item,
                    $"ComposeLab does not model the port form '{text}'.",
                    "Supported forms are \"8080\", \"5000:8080\", and an optional /tcp or /udp suffix. Port "
                        + "ranges and host addresses are not modeled yet.");
            }

            return ports;
        }

        private static bool TryReadPort(string text, out PortMapping port)
        {
            port = new PortMapping(null, 0);

            string body = text.Trim();
            PortProtocol protocol = PortProtocol.Tcp;

            int slash = body.LastIndexOf('/');

            if (slash >= 0)
            {
                string suffix = body[(slash + 1)..].ToLowerInvariant();

                if (suffix is not ("tcp" or "udp"))
                {
                    return false;
                }

                protocol = suffix == "udp" ? PortProtocol.Udp : PortProtocol.Tcp;
                body = body[..slash];
            }

            string[] parts = body.Split(':');

            if (parts.Length is < 1 or > 2 || parts.Any(part => !IsPortNumber(part)))
            {
                return false;
            }

            int container = int.Parse(parts[^1], CultureInfo.InvariantCulture);
            int? host = parts.Length == 2 ? int.Parse(parts[0], CultureInfo.InvariantCulture) : null;

            if (container is < 1 or > 65535 || host is < 1 or > 65535)
            {
                return false;
            }

            port = new PortMapping(host, container, protocol);

            return true;
        }

        private static bool IsPortNumber(string value) =>
            value.Length > 0 && value.Length <= 5 && value.All(char.IsAsciiDigit);

        private Dictionary<string, string> ReadEnvironment(YamlNode node, string path)
        {
            Dictionary<string, string> environment = new(StringComparer.Ordinal);

            if (node is YamlMappingNode mapping)
            {
                foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping)
                {
                    string key = Scalar(entry.Key) ?? string.Empty;

                    if (Scalar(entry.Value) is { } value && value.Length > 0)
                    {
                        environment[key] = value;

                        continue;
                    }

                    Add(ComposeFindingCode.ValueNotModeled, $"{path}.{key}", entry.Value,
                        $"'{key}' has no value, so Compose would take it from the machine running Docker.",
                        "Give the variable a value here. ComposeLab models environment variables as explicit "
                            + "values, because a value that comes from somewhere else cannot be reasoned about.");
                }

                return environment;
            }

            if (Sequence(node, path) is not { } sequence)
            {
                return environment;
            }

            for (int index = 0; index < sequence.Children.Count; index++)
            {
                YamlNode item = sequence.Children[index];
                string itemPath = $"{path}[{index}]";
                string? text = Scalar(item);
                int separator = text?.IndexOf('=', StringComparison.Ordinal) ?? -1;

                if (text is null || separator <= 0 || separator == text.Length - 1)
                {
                    Add(ComposeFindingCode.ValueNotModeled, itemPath, item,
                        "ComposeLab models environment entries as NAME=value.",
                        "Write the entry as NAME=value. An entry with no value would be taken from the "
                            + "machine running Docker, which cannot be reasoned about here.");

                    continue;
                }

                environment[text[..separator]] = text[(separator + 1)..];
            }

            return environment;
        }

        private List<VolumeMount> ReadMounts(YamlNode node, string path)
        {
            List<VolumeMount> mounts = [];

            if (Sequence(node, path) is not { } sequence)
            {
                return mounts;
            }

            for (int index = 0; index < sequence.Children.Count; index++)
            {
                YamlNode item = sequence.Children[index];
                string itemPath = $"{path}[{index}]";

                if (Scalar(item) is not { } text)
                {
                    Add(ComposeFindingCode.ValueNotModeled, itemPath, item,
                        "ComposeLab models the short volume syntax only.",
                        "Write the mount as 'volume-name:/path/in/container'.");

                    continue;
                }

                string[] parts = text.Split(':');

                if (parts.Length != 2 || !parts[1].StartsWith('/'))
                {
                    Add(ComposeFindingCode.ValueNotModeled, itemPath, item,
                        $"ComposeLab does not model the mount form '{text}'.",
                        "Supported form is 'volume-name:/path/in/container'. Access modes such as ':ro' are "
                            + "not modeled yet.");

                    continue;
                }

                if (parts[0].StartsWith('.') || parts[0].StartsWith('/') || parts[0].StartsWith('~'))
                {
                    Add(ComposeFindingCode.ValueNotModeled, itemPath, item,
                        "This is a bind mount, which maps a host folder into the container.",
                        "ComposeLab models named volumes, which exist independently of any container. Declare "
                            + "a named volume and mount that instead.");

                    continue;
                }

                mounts.Add(new VolumeMount(parts[0], parts[1]));
            }

            return mounts;
        }

        private List<NetworkAttachment> ReadAttachments(YamlNode node, string path)
        {
            List<NetworkAttachment> attachments = [];

            if (node is YamlMappingNode mapping)
            {
                Add(ComposeFindingCode.ValueNotModeled, path, mapping,
                    "ComposeLab models a plain list of network names.",
                    "Write the networks as a list. Per-network options such as aliases and static addresses "
                        + "are not modeled yet.");

                return attachments;
            }

            if (Sequence(node, path) is not { } sequence)
            {
                return attachments;
            }

            for (int index = 0; index < sequence.Children.Count; index++)
            {
                if (Scalar(sequence.Children[index], $"{path}[{index}]") is { } name)
                {
                    attachments.Add(new NetworkAttachment(name, DeclarationOrigin.Explicit));
                }
            }

            return attachments;
        }

        private List<ServiceDependency> ReadDependencies(YamlNode node, string path)
        {
            List<ServiceDependency> dependencies = [];

            if (node is YamlSequenceNode sequence)
            {
                for (int index = 0; index < sequence.Children.Count; index++)
                {
                    if (Scalar(sequence.Children[index], $"{path}[{index}]") is { } name)
                    {
                        dependencies.Add(new ServiceDependency(name, DependencyCondition.ServiceStarted));
                    }
                }

                return dependencies;
            }

            if (Mapping(node, path) is not { } mapping)
            {
                return dependencies;
            }

            foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping)
            {
                string name = Scalar(entry.Key) ?? string.Empty;
                string entryPath = $"{path}.{name}";

                if (Mapping(entry.Value, entryPath) is not { } options)
                {
                    continue;
                }

                DependencyCondition condition = DependencyCondition.ServiceStarted;

                foreach (KeyValuePair<YamlNode, YamlNode> option in options)
                {
                    string key = Scalar(option.Key) ?? string.Empty;
                    string optionPath = $"{entryPath}.{key}";

                    if (key != "condition")
                    {
                        ClassifyKey(key, option.Key, optionPath, ComposeKeyCatalog.DependsOn,
                            "ComposeLab models the condition only.");

                        continue;
                    }

                    switch (Scalar(option.Value, optionPath))
                    {
                        case "service_healthy":
                            condition = DependencyCondition.ServiceHealthy;
                            break;

                        case "service_started":
                            condition = DependencyCondition.ServiceStarted;
                            break;

                        case { } other:
                            Add(ComposeFindingCode.ValueNotModeled, optionPath, option.Value,
                                $"ComposeLab does not model the condition '{other}'.",
                                "Supported conditions are service_started and service_healthy.");
                            break;

                        default:
                            break;
                    }
                }

                dependencies.Add(new ServiceDependency(name, condition));
            }

            return dependencies;
        }

        private HealthCheckDeclaration? ReadHealthCheck(YamlNode node, string path)
        {
            if (Mapping(node, path) is not { } mapping)
            {
                return null;
            }

            HealthCheckDeclaration? declaration = null;
            bool disabled = false;

            foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping)
            {
                string key = Scalar(entry.Key) ?? string.Empty;
                string keyPath = $"{path}.{key}";

                switch (key)
                {
                    case "disable":
                        disabled = string.Equals(Scalar(entry.Value, keyPath), "true",
                            StringComparison.OrdinalIgnoreCase);
                        break;

                    case "test":
                        declaration = ReadHealthCheckTest(entry.Value, keyPath);
                        break;

                    default:
                        ClassifyKey(key, entry.Key, keyPath, ComposeKeyCatalog.HealthCheck,
                            "ComposeLab records that a healthcheck exists and which form its test takes. It "
                                + "does not run the check, so timing options are not modeled.");
                        break;
                }
            }

            if (disabled)
            {
                return HealthCheckDeclaration.Disabled;
            }

            if (declaration is null)
            {
                Add(ComposeFindingCode.ValueNotModeled, path, node,
                    "This healthcheck has no test and is not disabled, so there is nothing to model.",
                    "Add a test, for example test: [\"CMD\", \"pg_isready\"], or set disable: true.");
            }

            return declaration;
        }

        private HealthCheckDeclaration? ReadHealthCheckTest(YamlNode node, string path)
        {
            if (Scalar(node) is { } shell)
            {
                return string.Equals(shell, "NONE", StringComparison.Ordinal)
                    ? HealthCheckDeclaration.Disabled
                    : HealthCheckDeclaration.Shell(shell);
            }

            if (Sequence(node, path) is not { } sequence || sequence.Children.Count == 0)
            {
                Add(ComposeFindingCode.ValueNotModeled, path, node,
                    "A healthcheck test is either a command string or a list starting with CMD, CMD-SHELL, or "
                        + "NONE.",
                    "Write test: [\"CMD\", \"pg_isready\"] for a direct command, or test: \"pg_isready\" to "
                        + "run it through a shell.");

                return null;
            }

            List<string> items = [];

            foreach (YamlNode child in sequence.Children)
            {
                if (Scalar(child, path) is { } value)
                {
                    items.Add(value);
                }
            }

            if (items.Count == 0)
            {
                return null;
            }

            switch (items[0])
            {
                case "NONE":
                    return HealthCheckDeclaration.Disabled;

                case "CMD" when items.Count > 1:
                    return HealthCheckDeclaration.Command(items[1..]);

                case "CMD-SHELL" when items.Count == 2:
                    return HealthCheckDeclaration.CommandShell(items[1]);

                default:
                    Add(ComposeFindingCode.ValueNotModeled, path, node,
                        $"ComposeLab does not model this healthcheck test form.",
                        "A list test starts with CMD followed by the command and its arguments, CMD-SHELL "
                            + "followed by a single shell command, or NONE to turn the check off.");

                    return null;
            }
        }

        private List<TDeclaration> ReadDeclarations<TDeclaration>(
            YamlNode node,
            string section,
            IReadOnlySet<string> knownKeys,
            Func<string, string?, TDeclaration> create)
        {
            List<TDeclaration> declarations = [];

            if (IsEmpty(node))
            {
                return declarations;
            }

            if (Mapping(node, section) is not { } mapping)
            {
                return declarations;
            }

            foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping)
            {
                string name = Scalar(entry.Key) ?? string.Empty;
                string path = $"{section}.{name}";
                string? composeName = null;

                if (!IsEmpty(entry.Value) && Mapping(entry.Value, path) is { } options)
                {
                    foreach (KeyValuePair<YamlNode, YamlNode> option in options)
                    {
                        string key = Scalar(option.Key) ?? string.Empty;
                        string optionPath = $"{path}.{key}";

                        if (key == "name")
                        {
                            composeName = Scalar(option.Value, optionPath);

                            continue;
                        }

                        ClassifyKey(key, option.Key, optionPath, knownKeys,
                            "ComposeLab models the declaration and its name.");
                    }
                }

                declarations.Add(create(name, composeName));
            }

            return declarations;
        }

        private void ClassifyKey(
            string key,
            YamlNode node,
            string path,
            IReadOnlySet<string> knownKeys,
            string suggestion)
        {
            if (knownKeys.Contains(key) || ComposeKeyCatalog.IsExtension(key))
            {
                Add(ComposeFindingCode.KeyNotModeled, path, node,
                    $"'{key}' is valid Compose, but ComposeLab does not model it yet.",
                    suggestion);

                return;
            }

            Add(ComposeFindingCode.UnknownKey, path, node,
                $"'{key}' is not a Compose key. It looks like a typo.",
                "Check the spelling. Compose will reject an unknown key too, so this would fail outside "
                    + "ComposeLab as well.");
        }

        private static bool IsEmpty(YamlNode node) => node is YamlScalarNode { Value: null or "" };

        private YamlMappingNode? Mapping(YamlNode node, string path)
        {
            if (node is YamlMappingNode mapping)
            {
                return mapping;
            }

            Add(ComposeFindingCode.UnexpectedShape, path, node,
                $"'{path}' should be a mapping of names to settings.",
                "Indent each entry under the key, as 'name:' followed by its settings.");

            return null;
        }

        private YamlSequenceNode? Sequence(YamlNode node, string path)
        {
            if (node is YamlSequenceNode sequence)
            {
                return sequence;
            }

            Add(ComposeFindingCode.UnexpectedShape, path, node,
                $"'{path}' should be a list.",
                "Write each entry on its own line, starting with '- '.");

            return null;
        }

        private static string? Scalar(YamlNode node) =>
            node is YamlScalarNode { Value: { Length: > 0 } value } ? value : null;

        private string? Scalar(YamlNode node, string path)
        {
            if (Scalar(node) is { } value)
            {
                return value;
            }

            Add(ComposeFindingCode.UnexpectedShape, path, node,
                $"'{path}' should be a single value.",
                "Replace the block with one value on the same line as the key.");

            return null;
        }

        private void Add(ComposeFindingCode code, string path, YamlNode node, string message, string suggestion) =>
            Add(code, path, (int)node.Start.Line, (int)node.Start.Column, message, suggestion);

        private void Add(
            ComposeFindingCode code,
            string path,
            int line,
            int column,
            string message,
            string suggestion) =>
            _findings.Add(new ComposeFinding(code, path, line, column, message, suggestion));
    }
}
