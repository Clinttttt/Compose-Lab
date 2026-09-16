using ComposeLab.Api.Domain.Topology;

namespace ComposeLab.Api.Features.Topology.Shared;

/// <summary>
/// Maps between the wire contract and the domain model. No judgment, no defaults beyond Compose's own.
/// </summary>
/// <remarks>
/// The reverse direction exists so that parsing can hand back a topology in exactly the shape the client
/// posts. That keeps one topology contract on the wire and makes a generate-parse-generate round trip
/// expressible end to end.
/// </remarks>
internal static class TopologyMapper
{
    public static ApplicationTopology ToTopology(TopologyRequest request) => new()
    {
        Services = [.. request.Services.Select(ToService)],
        Networks =
        [
            .. request.Networks.Select(network => new ContainerNetwork
            {
                Name = network.Name,
                ComposeName = network.ComposeName
            })
        ],
        Volumes =
        [
            .. request.Volumes.Select(volume => new ContainerVolume
            {
                Name = volume.Name,
                ComposeName = volume.ComposeName
            })
        ]
    };

    public static TopologyRequest ToRequest(ApplicationTopology topology) => new()
    {
        Services = [.. topology.Services.Select(ToServiceRequest)],
        Networks =
        [
            .. topology.Networks.Select(network => new NetworkRequest
            {
                Name = network.Name,
                ComposeName = network.ComposeName
            })
        ],
        Volumes =
        [
            .. topology.Volumes.Select(volume => new VolumeRequest
            {
                Name = volume.Name,
                ComposeName = volume.ComposeName
            })
        ]
    };

    /// <summary>
    /// Token names for the healthcheck forms, shared by the mapper and the validator so the accepted set
    /// is declared once.
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

    private static ContainerService ToService(ServiceRequest service) => new()
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

    private static ServiceRequest ToServiceRequest(ContainerService service) => new()
    {
        Name = service.Name,
        Image = service.Image,
        Build = service.BuildContext,
        Ports =
        [
            .. service.Ports.Select(port => new PortRequest
            {
                HostPort = port.HostPort,
                ContainerPort = port.ContainerPort,
                Protocol = port.ProtocolToken
            })
        ],
        Networks = [.. service.Networks.Select(attachment => attachment.NetworkName)],
        Volumes =
        [
            .. service.Volumes.Select(mount => new VolumeMountRequest
            {
                Volume = mount.VolumeName,
                Path = mount.ContainerPath
            })
        ],
        DependsOn =
        [
            .. service.Dependencies.Select(dependency => new DependencyRequest
            {
                Service = dependency.ServiceName,
                Condition = ToConditionToken(dependency.Condition)
            })
        ],
        Environment = new Dictionary<string, string>(service.Environment, StringComparer.Ordinal),
        HealthCheck = ToHealthCheckRequest(service.HealthCheck)
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

    private static HealthCheckDeclaration? ToHealthCheck(HealthCheckRequest? healthCheck)
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

    private static HealthCheckRequest? ToHealthCheckRequest(HealthCheckDeclaration? healthCheck)
    {
        if (healthCheck is null)
        {
            return null;
        }

        return new HealthCheckRequest
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
