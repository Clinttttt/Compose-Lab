using ComposeLab.Api.Domain.Topology;

namespace ComposeLab.Api.Domain.Compose;

/// <summary>
/// Writes a topology as canonical Compose YAML, recording which lines each element produced.
/// </summary>
/// <remarks>
/// Hand-written rather than delegated to a YAML serializer, for one reason: provenance. A general
/// serializer will not tell you which lines an object produced, so line ranges would have to be
/// recovered by re-parsing the output and matching it back — fragile, and the correspondence feature
/// depends on it being exact. Emitting the text directly makes the map a byproduct. The supported
/// Compose subset is narrow enough that this costs less than the workaround would.
/// <para>
/// Generation does not validate. A topology with duplicate service names produces duplicate YAML keys,
/// because the generator's job is to project faithfully and the simulator's job is to explain what is
/// wrong. Implicit elements are never written: an implied <c>default</c> network is Compose's behavior,
/// not the author's configuration, and writing it out would put words in their mouth.
/// </para>
/// </remarks>
public static class ComposeGenerator
{
    public static ComposeDocument Generate(ApplicationTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);

        YamlWriter writer = new();
        List<ProvenanceEntry> provenance = [];

        IReadOnlyList<ContainerNetwork> networks =
        [
            .. topology.Networks.Where(network => network.Origin == DeclarationOrigin.Explicit)
        ];

        bool wroteSection = false;

        if (topology.Services.Count > 0)
        {
            writer.WriteKey(0, "services");

            for (int index = 0; index < topology.Services.Count; index++)
            {
                if (index > 0)
                {
                    writer.WriteBlankLine();
                }

                WriteService(writer, provenance, topology.Services[index]);
            }

            wroteSection = true;
        }

        if (networks.Count > 0)
        {
            wroteSection = WriteDeclarations(
                writer,
                provenance,
                "networks",
                [.. networks.Select(network => (network.Name, network.ComposeName))],
                ElementReference.Network,
                wroteSection);
        }

        if (topology.Volumes.Count > 0)
        {
            WriteDeclarations(
                writer,
                provenance,
                "volumes",
                [.. topology.Volumes.Select(volume => (volume.Name, volume.ComposeName))],
                ElementReference.Volume,
                wroteSection);
        }

        return new ComposeDocument
        {
            Yaml = writer.ToString(),
            Provenance = provenance
        };
    }

    private static bool WriteDeclarations(
        YamlWriter writer,
        List<ProvenanceEntry> provenance,
        string section,
        IReadOnlyList<(string Key, string? ComposeName)> declarations,
        Func<string, ElementReference> reference,
        bool wroteSection)
    {
        if (wroteSection)
        {
            writer.WriteBlankLine();
        }

        writer.WriteKey(0, section);

        foreach ((string key, string? composeName) in declarations)
        {
            int first = writer.WriteKey(1, key);
            int last = first;

            if (!string.IsNullOrWhiteSpace(composeName))
            {
                last = writer.WriteEntry(2, "name", YamlScalar.Plain(composeName));
            }

            provenance.Add(new ProvenanceEntry(reference(key), new YamlRange(first, last)));
        }

        return true;
    }

    private static void WriteService(
        YamlWriter writer,
        List<ProvenanceEntry> provenance,
        ContainerService service)
    {
        int start = writer.WriteKey(1, service.Name);

        if (!string.IsNullOrWhiteSpace(service.Image))
        {
            writer.WriteEntry(2, "image", YamlScalar.Plain(service.Image));
        }

        if (!string.IsNullOrWhiteSpace(service.BuildContext))
        {
            writer.WriteEntry(2, "build", YamlScalar.Plain(service.BuildContext));
        }

        WritePorts(writer, provenance, service);
        WriteEnvironment(writer, provenance, service);
        WriteVolumeMounts(writer, provenance, service);
        WriteNetworkAttachments(writer, provenance, service);
        WriteDependencies(writer, provenance, service);
        WriteHealthCheck(writer, service);

        provenance.Add(new ProvenanceEntry(
            ElementReference.Service(service.Name),
            new YamlRange(start, writer.LastLine)));
    }

    private static void WritePorts(
        YamlWriter writer,
        List<ProvenanceEntry> provenance,
        ContainerService service)
    {
        if (service.Ports.Count == 0)
        {
            return;
        }

        writer.WriteKey(2, "ports");

        foreach (PortMapping port in service.Ports)
        {
            // Always quoted: an unquoted 8080:8080 is a sexagesimal number to a YAML 1.1 parser.
            int line = writer.WriteItem(3, YamlScalar.Quoted(port.ToComposeSyntax()));

            provenance.Add(new ProvenanceEntry(
                ElementReference.Port(service.Name, port),
                new YamlRange(line, line)));
        }
    }

    private static void WriteEnvironment(
        YamlWriter writer,
        List<ProvenanceEntry> provenance,
        ContainerService service)
    {
        if (service.Environment.Count == 0)
        {
            return;
        }

        writer.WriteKey(2, "environment");

        foreach (KeyValuePair<string, string> variable in service.Environment
            .OrderBy(variable => variable.Key, StringComparer.Ordinal))
        {
            int line = writer.WriteEntry(3, variable.Key, YamlScalar.Plain(variable.Value));

            provenance.Add(new ProvenanceEntry(
                ElementReference.EnvironmentVariable(service.Name, variable.Key),
                new YamlRange(line, line)));
        }
    }

    private static void WriteVolumeMounts(
        YamlWriter writer,
        List<ProvenanceEntry> provenance,
        ContainerService service)
    {
        if (service.Volumes.Count == 0)
        {
            return;
        }

        writer.WriteKey(2, "volumes");

        foreach (VolumeMount mount in service.Volumes)
        {
            int line = writer.WriteItem(3, YamlScalar.Plain($"{mount.VolumeName}:{mount.ContainerPath}"));

            provenance.Add(new ProvenanceEntry(
                ElementReference.VolumeMount(service.Name, mount),
                new YamlRange(line, line)));
        }
    }

    private static void WriteNetworkAttachments(
        YamlWriter writer,
        List<ProvenanceEntry> provenance,
        ContainerService service)
    {
        List<NetworkAttachment> attachments =
        [
            .. service.Networks.Where(attachment => attachment.Origin == DeclarationOrigin.Explicit)
        ];

        if (attachments.Count == 0)
        {
            return;
        }

        writer.WriteKey(2, "networks");

        foreach (NetworkAttachment attachment in attachments)
        {
            int line = writer.WriteItem(3, YamlScalar.Plain(attachment.NetworkName));

            provenance.Add(new ProvenanceEntry(
                ElementReference.NetworkAttachment(service.Name, attachment.NetworkName),
                new YamlRange(line, line)));
        }
    }

    private static void WriteDependencies(
        YamlWriter writer,
        List<ProvenanceEntry> provenance,
        ContainerService service)
    {
        if (service.Dependencies.Count == 0)
        {
            return;
        }

        // The short list form cannot express a condition, so one health condition puts every
        // dependency of this service into the long form.
        bool longForm = service.Dependencies.Any(dependency =>
            dependency.Condition == DependencyCondition.ServiceHealthy);

        writer.WriteKey(2, "depends_on");

        foreach (ServiceDependency dependency in service.Dependencies)
        {
            YamlRange range;

            if (longForm)
            {
                int first = writer.WriteKey(3, dependency.ServiceName);
                int last = writer.WriteEntry(4, "condition", Condition(dependency.Condition));

                range = new YamlRange(first, last);
            }
            else
            {
                int line = writer.WriteItem(3, YamlScalar.Plain(dependency.ServiceName));

                range = new YamlRange(line, line);
            }

            provenance.Add(new ProvenanceEntry(
                ElementReference.Dependency(service.Name, dependency.ServiceName),
                range));
        }
    }

    private static void WriteHealthCheck(YamlWriter writer, ContainerService service)
    {
        if (service.HealthCheck is not { } healthCheck)
        {
            return;
        }

        writer.WriteKey(2, "healthcheck");

        switch (healthCheck.Form)
        {
            case HealthCheckTestForm.Disabled:
                // Compose also spells this test: ["NONE"]. Both mean the same thing; the canonical
                // generator picks the clearer one.
                writer.WriteEntry(3, "disable", "true");
                break;

            case HealthCheckTestForm.Shell:
                writer.WriteEntry(3, "test", YamlScalar.Plain(healthCheck.Arguments[0]));
                break;

            case HealthCheckTestForm.Command:
                writer.WriteKey(3, "test");
                writer.WriteItem(4, "CMD");

                foreach (string argument in healthCheck.Arguments)
                {
                    writer.WriteItem(4, YamlScalar.Plain(argument));
                }

                break;

            case HealthCheckTestForm.CommandShell:
                writer.WriteKey(3, "test");
                writer.WriteItem(4, "CMD-SHELL");
                writer.WriteItem(4, YamlScalar.Plain(healthCheck.Arguments[0]));
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(service),
                    healthCheck.Form,
                    "Unmapped healthcheck test form.");
        }
    }

    private static string Condition(DependencyCondition condition) => condition switch
    {
        DependencyCondition.ServiceHealthy => "service_healthy",
        DependencyCondition.ServiceStarted => "service_started",
        _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unmapped condition.")
    };
}
