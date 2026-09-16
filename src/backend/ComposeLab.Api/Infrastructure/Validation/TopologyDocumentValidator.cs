using System.Text.RegularExpressions;
using ComposeLab.Api.Domain.Topology.Document;
using FluentValidation;

namespace ComposeLab.Api.Infrastructure.Validation;

/// <summary>
/// Guards the request envelope for any slice that accepts a topology.
/// </summary>
/// <remarks>
/// This rejects input that is malformed — a port outside the valid range, a relative container path, an
/// unrecognized protocol. It must never reject an architecture that is merely wrong: duplicate service
/// names, references to undeclared networks, colliding host ports, and services with no image are all valid
/// input, because building and inspecting those mistakes is the product. Saving a project is held to the
/// same line, so a learner can save a broken architecture and come back to it.
/// <para>
/// It lives in <c>Infrastructure</c> rather than beside one feature because more than one feature needs it —
/// the engine routes and project saving — and it depends only on the domain document and FluentValidation,
/// so the dependency direction stays correct.
/// </para>
/// <para>
/// The collection limits are a deliberate bound on work per request. These endpoints are unauthenticated in
/// this phase, so an unbounded topology would be an easy way to burn CPU.
/// </para>
/// </remarks>
internal abstract class TopologyDocumentValidator<TDocument> : AbstractValidator<TDocument>
    where TDocument : TopologyDocument
{
    private const int MaxServices = 50;
    private const int MaxDeclarations = 50;

    protected TopologyDocumentValidator()
    {
        RuleFor(document => document.Services)
            .Must(services => services.Count <= MaxServices)
            .WithMessage($"A topology may contain at most {MaxServices} services.");

        RuleFor(document => document.Networks)
            .Must(networks => networks.Count <= MaxDeclarations)
            .WithMessage($"A topology may declare at most {MaxDeclarations} networks.");

        RuleFor(document => document.Volumes)
            .Must(volumes => volumes.Count <= MaxDeclarations)
            .WithMessage($"A topology may declare at most {MaxDeclarations} volumes.");

        RuleForEach(document => document.Services).SetValidator(new ServiceDocumentValidator());
        RuleForEach(document => document.Networks).SetValidator(new NetworkDocumentValidator());
        RuleForEach(document => document.Volumes).SetValidator(new VolumeDocumentValidator());
    }
}

internal sealed class ServiceDocumentValidator : AbstractValidator<ServiceDocument>
{
    private const int MaxPorts = 20;
    private const int MaxNetworks = 20;
    private const int MaxVolumes = 20;
    private const int MaxDependencies = 50;
    private const int MaxEnvironmentVariables = 100;

    public ServiceDocumentValidator()
    {
        RuleFor(service => service.Name)
            .NotEmpty()
            .MaximumLength(63)
            .Must(ComposeNames.IsValid)
            .When(service => !string.IsNullOrEmpty(service.Name), ApplyConditionTo.CurrentValidator)
            .WithMessage("A service name may contain letters, digits, dots, underscores, and hyphens, and must "
                + "start with a letter or digit.");

        RuleFor(service => service.Image).MaximumLength(255);
        RuleFor(service => service.Build).MaximumLength(1024);

        RuleFor(service => service.Ports)
            .Must(ports => ports.Count <= MaxPorts)
            .WithMessage($"A service may publish at most {MaxPorts} ports.");

        RuleFor(service => service.Networks)
            .Must(networks => networks.Count <= MaxNetworks)
            .WithMessage($"A service may join at most {MaxNetworks} networks.");

        RuleFor(service => service.Volumes)
            .Must(volumes => volumes.Count <= MaxVolumes)
            .WithMessage($"A service may mount at most {MaxVolumes} volumes.");

        RuleFor(service => service.DependsOn)
            .Must(dependencies => dependencies.Count <= MaxDependencies)
            .WithMessage($"A service may declare at most {MaxDependencies} dependencies.");

        RuleFor(service => service.Environment)
            .Must(environment => environment.Count <= MaxEnvironmentVariables)
            .WithMessage($"A service may declare at most {MaxEnvironmentVariables} environment variables.")
            .Must(environment => environment.Keys.All(key => !string.IsNullOrWhiteSpace(key)))
            .WithMessage("An environment variable name cannot be empty.");

        RuleForEach(service => service.Ports).SetValidator(new PortDocumentValidator());
        RuleForEach(service => service.Volumes).SetValidator(new VolumeMountDocumentValidator());
        RuleForEach(service => service.DependsOn).SetValidator(new DependencyDocumentValidator());

        RuleFor(service => service.HealthCheck!)
            .SetValidator(new HealthCheckDocumentValidator())
            .When(service => service.HealthCheck is not null);

        RuleForEach(service => service.Networks)
            .NotEmpty()
            .WithMessage("A network name cannot be empty.");
    }
}

internal sealed class PortDocumentValidator : AbstractValidator<PortDocument>
{
    private static readonly string[] Protocols = ["tcp", "udp"];

    public PortDocumentValidator()
    {
        RuleFor(port => port.ContainerPort).InclusiveBetween(1, 65535);

        RuleFor(port => port.HostPort)
            .InclusiveBetween(1, 65535)
            .When(port => port.HostPort is not null);

        RuleFor(port => port.Protocol)
            .Must(protocol => Protocols.Contains(protocol!.ToLowerInvariant(), StringComparer.Ordinal))
            .When(port => port.Protocol is not null)
            .WithMessage("A port protocol must be 'tcp' or 'udp'.");
    }
}

internal sealed class VolumeMountDocumentValidator : AbstractValidator<VolumeMountDocument>
{
    public VolumeMountDocumentValidator()
    {
        RuleFor(mount => mount.Volume).NotEmpty().MaximumLength(255);

        RuleFor(mount => mount.Path)
            .NotEmpty()
            .MaximumLength(1024)
            .Must(path => path.StartsWith('/'))
            .When(mount => !string.IsNullOrEmpty(mount.Path), ApplyConditionTo.CurrentValidator)
            .WithMessage("A mount path must be an absolute path inside the container, starting with '/'.");
    }
}

internal sealed class DependencyDocumentValidator : AbstractValidator<DependencyDocument>
{
    private static readonly string[] Conditions = ["service_started", "service_healthy"];

    public DependencyDocumentValidator()
    {
        RuleFor(dependency => dependency.Service).NotEmpty().MaximumLength(63);

        RuleFor(dependency => dependency.Condition)
            .Must(condition => Conditions.Contains(condition!.ToLowerInvariant(), StringComparer.Ordinal))
            .When(dependency => dependency.Condition is not null)
            .WithMessage("A dependency condition must be 'service_started' or 'service_healthy'.");
    }
}

/// <summary>
/// A healthcheck must name a form ComposeLab understands and carry the arguments that form requires, which
/// is what guarantees every accepted healthcheck has a valid Compose form to write back out.
/// </summary>
internal sealed class HealthCheckDocumentValidator : AbstractValidator<HealthCheckDocument>
{
    public HealthCheckDocumentValidator()
    {
        RuleFor(healthCheck => healthCheck.Form)
            .Must(TopologyDocumentMapper.HealthCheckForms.IsRecognized)
            .WithMessage("A healthcheck form must be 'disabled', 'shell', 'command', or 'command_shell'.");

        RuleFor(healthCheck => healthCheck.Test)
            .Must(test => test.Count == 1)
            .When(healthCheck => TopologyDocumentMapper.HealthCheckForms.IsSingleCommand(healthCheck.Form))
            .WithMessage("The 'shell' and 'command_shell' forms take exactly one command string.");

        RuleFor(healthCheck => healthCheck.Test)
            .Must(test => test.Count > 0)
            .When(healthCheck => string.Equals(
                healthCheck.Form,
                TopologyDocumentMapper.HealthCheckForms.Command,
                StringComparison.OrdinalIgnoreCase))
            .WithMessage("The 'command' form needs at least one argument, and must not include the leading "
                + "'CMD' token.");

        RuleFor(healthCheck => healthCheck.Test)
            .Must(test => test.Count == 0)
            .When(healthCheck => TopologyDocumentMapper.HealthCheckForms.IsDisabled(healthCheck.Form))
            .WithMessage("A disabled healthcheck has no test command.");

        RuleForEach(healthCheck => healthCheck.Test)
            .NotEmpty()
            .WithMessage("A healthcheck test cannot contain empty entries.");
    }
}

internal sealed class NetworkDocumentValidator : AbstractValidator<NetworkDocument>
{
    public NetworkDocumentValidator()
    {
        RuleFor(network => network.Name)
            .NotEmpty()
            .MaximumLength(63)
            .Must(ComposeNames.IsValid)
            .When(network => !string.IsNullOrEmpty(network.Name), ApplyConditionTo.CurrentValidator)
            .WithMessage("A network name may contain letters, digits, dots, underscores, and hyphens, and must "
                + "start with a letter or digit.");

        RuleFor(network => network.ComposeName).MaximumLength(63);
    }
}

internal sealed class VolumeDocumentValidator : AbstractValidator<VolumeDocument>
{
    public VolumeDocumentValidator()
    {
        RuleFor(volume => volume.Name)
            .NotEmpty()
            .MaximumLength(63)
            .Must(ComposeNames.IsValid)
            .When(volume => !string.IsNullOrEmpty(volume.Name), ApplyConditionTo.CurrentValidator)
            .WithMessage("A volume name may contain letters, digits, dots, underscores, and hyphens, and must "
                + "start with a letter or digit.");

        RuleFor(volume => volume.ComposeName).MaximumLength(63);
    }
}

/// <summary>Compose resource names: alphanumeric, then dots, underscores, and hyphens.</summary>
internal static partial class ComposeNames
{
    public static bool IsValid(string name) => Pattern().IsMatch(name);

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
