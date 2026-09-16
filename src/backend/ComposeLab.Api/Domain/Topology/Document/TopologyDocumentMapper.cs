namespace ComposeLab.Api.Domain.Topology.Document;

/// <summary>Maps between the authored document and the domain model. No judgment, no invented defaults.</summary>
internal static class TopologyDocumentMapper
{
    /// <summary>
    /// Token names for the healthcheck forms, declared once so the mapper and the validator cannot disagree
    /// about which are accepted.
    /// </summary>
    public static class HealthCheckForms
    {
        public const string Disabled = "disabled";
        public const string Shell = "shell";
        public const string Command = "command";
        public const string CommandShell = "command_shell";

        public static readonly string[] All = [Disabled, Shell, Command, CommandShell];

        /// <summary>Forms that carry exactly one command string.</summary>
        public static bool IsSingleCommand(string? form) =>
            string.Equals(form, Shell, StringComparison.OrdinalIgnoreCase)
            || string.Equals(form, CommandShell, StringComparison.OrdinalIgnoreCase);

        public static bool IsDisabled(string? form) =>
            string.Equals(form, Disabled, StringComparison.OrdinalIgnoreCase);

        public static bool IsRecognized(string? form) =>
            All.Contains(form?.ToLowerInvariant(), StringComparer.Ordinal);
    }

    public static ApplicationTopology ToTopology(TopologyDocument document) => new()
    {
        Services = [.. document.Services.Select(ToService)],
        Networks =
        [
            .. document.Networks.Select(network => new ContainerNetwork
            {
                Name = network.Name,
                ComposeName = network.ComposeName
            })
        ],
        Volumes =
        [
            .. document.Volumes.Select(volume => new ContainerVolume
            {
                Name = volume.Name,
                ComposeName = volume.ComposeName
            })
        ]
    };

    /// <summary>
    /// Captures the authored architecture. Implicit elements are skipped: a normalized topology handed to
    /// this method must not come back out as though the author had written Compose's implied network down.
    /// </summary>
    public static TopologyDocument ToDocument(ApplicationTopology topology) => new()
    {
        Services = [.. topology.Services.Select(ToServiceDocument)],
        Networks =
        [
            .. topology.Networks
                .Where(network => network.Origin == DeclarationOrigin.Explicit)
                .Select(network => new NetworkDocument
                {
                    Name = network.Name,
                    ComposeName = network.ComposeName
                })
        ],
        Volumes =
        [
            .. topology.Volumes.Select(volume => new VolumeDocument
            {
                Name = volume.Name,
                ComposeName = volume.ComposeName
            })
        ]
    };

    private static ContainerService ToService(ServiceDocument service) => new()
    {
        Name = service.Name,
        Image = service.Image,
        BuildContext = service.Build,
        Ports =
        [
            .. service.Ports.Select(port => new PortMapping(
                port.HostPort,
                port.ContainerPort,
                ToProtocol(port.Protocol)))
        ],
        Networks =
        [
            .. service.Networks.Select(name => new NetworkAttachment(name, DeclarationOrigin.Explicit))
        ],
        Volumes = [.. service.Volumes.Select(mount => new VolumeMount(mount.Volume, mount.Path))],
        Dependencies =
        [
            .. service.DependsOn.Select(dependency => new ServiceDependency(
                dependency.Service,
                ToCondition(dependency.Condition)))
        ],
        Environment = new Dictionary<string, string>(service.Environment, StringComparer.Ordinal),
        HealthCheck = ToHealthCheck(service.HealthCheck)
    };

    private static ServiceDocument ToServiceDocument(ContainerService service) => new()
    {
        Name = service.Name,
        Image = service.Image,
        Build = service.BuildContext,
        Ports =
        [
            .. service.Ports.Select(port => new PortDocument
            {
                HostPort = port.HostPort,
                ContainerPort = port.ContainerPort,
                Protocol = port.ProtocolToken
            })
        ],
        Networks =
        [
            .. service.Networks
                .Where(attachment => attachment.Origin == DeclarationOrigin.Explicit)
                .Select(attachment => attachment.NetworkName)
        ],
        Volumes =
        [
            .. service.Volumes.Select(mount => new VolumeMountDocument
            {
                Volume = mount.VolumeName,
                Path = mount.ContainerPath
            })
        ],
        DependsOn =
        [
            .. service.Dependencies.Select(dependency => new DependencyDocument
            {
                Service = dependency.ServiceName,
                Condition = ToConditionToken(dependency.Condition)
            })
        ],
        Environment = new Dictionary<string, string>(service.Environment, StringComparer.Ordinal),
        HealthCheck = ToHealthCheckDocument(service.HealthCheck)
    };

    private static PortProtocol ToProtocol(string? protocol) =>
        string.Equals(protocol, "udp", StringComparison.OrdinalIgnoreCase)
            ? PortProtocol.Udp
            : PortProtocol.Tcp;

    private static DependencyCondition ToCondition(string? condition) =>
        string.Equals(condition, "service_healthy", StringComparison.OrdinalIgnoreCase)
            ? DependencyCondition.ServiceHealthy
            : DependencyCondition.ServiceStarted;

    private static string ToConditionToken(DependencyCondition condition) =>
        condition == DependencyCondition.ServiceHealthy ? "service_healthy" : "service_started";

    private static HealthCheckDeclaration? ToHealthCheck(HealthCheckDocument? healthCheck)
    {
        if (healthCheck is null)
        {
            return null;
        }

        return healthCheck.Form.ToLowerInvariant() switch
        {
            HealthCheckForms.Shell => HealthCheckDeclaration.Shell(healthCheck.Test[0]),
            HealthCheckForms.Command => HealthCheckDeclaration.Command(healthCheck.Test),
            HealthCheckForms.CommandShell => HealthCheckDeclaration.CommandShell(healthCheck.Test[0]),
            _ => HealthCheckDeclaration.Disabled
        };
    }

    private static HealthCheckDocument? ToHealthCheckDocument(HealthCheckDeclaration? healthCheck)
    {
        if (healthCheck is null)
        {
            return null;
        }

        return new HealthCheckDocument
        {
            Form = healthCheck.Form switch
            {
                HealthCheckTestForm.Shell => HealthCheckForms.Shell,
                HealthCheckTestForm.Command => HealthCheckForms.Command,
                HealthCheckTestForm.CommandShell => HealthCheckForms.CommandShell,
                _ => HealthCheckForms.Disabled
            },
            Test = healthCheck.Arguments
        };
    }
}
