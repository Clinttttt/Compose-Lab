namespace ComposeLab.Api.Domain.Topology;

/// <summary>
/// A machine-readable pointer to one element of a topology.
/// </summary>
/// <remarks>
/// This is shared vocabulary. Simulation attaches references to events and issues; generation attaches
/// them to YAML line ranges. Because both sides speak the same language, the frontend can take an
/// issue's element and resolve it to the exact lines that produced it — which is why an explanation
/// never needs to carry a YAML snippet of its own.
/// <para>
/// <see cref="OwnerServiceName"/> is what separates a declaration from a use. A network with no owner
/// is the top-level declaration; the same network with an owner is one service's attachment to it.
/// </para>
/// </remarks>
public sealed record ElementReference(
    ElementKind Kind,
    string Name,
    string? OwnerServiceName = null,
    string? Detail = null)
{
    public static ElementReference Service(string name) =>
        new(ElementKind.Service, name);

    /// <summary>The top-level network declaration.</summary>
    public static ElementReference Network(string name) =>
        new(ElementKind.Network, name);

    /// <summary>One service's membership of a network.</summary>
    public static ElementReference NetworkAttachment(string serviceName, string networkName) =>
        new(ElementKind.Network, networkName, serviceName);

    /// <summary>The top-level named volume declaration.</summary>
    public static ElementReference Volume(string name) =>
        new(ElementKind.Volume, name);

    /// <summary>One service's mount of a named volume.</summary>
    public static ElementReference VolumeMount(string serviceName, VolumeMount mount) =>
        new(ElementKind.Volume, mount.VolumeName, serviceName, mount.ContainerPath);

    public static ElementReference Port(string serviceName, PortMapping mapping) =>
        new(ElementKind.PortMapping, mapping.ToComposeSyntax(), serviceName, mapping.ToComposeSyntax());

    public static ElementReference Dependency(string serviceName, string dependsOnServiceName) =>
        new(ElementKind.Dependency, dependsOnServiceName, serviceName);

    public static ElementReference EnvironmentVariable(string serviceName, string key) =>
        new(ElementKind.EnvironmentVariable, key, serviceName);
}
