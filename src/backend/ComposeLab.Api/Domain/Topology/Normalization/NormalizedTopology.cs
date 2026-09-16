namespace ComposeLab.Api.Domain.Topology.Normalization;

/// <summary>
/// A topology whose implicit Compose behavior has been made explicit. Only
/// <see cref="TopologyNormalizer"/> can produce one, so a rule that takes this type cannot
/// accidentally be handed raw author input.
/// </summary>
/// <remarks>
/// The distinction matters because the most damaging bug available to ComposeLab is reasoning about
/// networks before the implicit <c>default</c> network exists: the most common beginner Compose file
/// declares no networks at all, and evaluating it un-normalized reports a working architecture as
/// broken.
/// </remarks>
public sealed class NormalizedTopology
{
    internal NormalizedTopology(
        IReadOnlyList<ContainerService> services,
        IReadOnlyList<ContainerNetwork> networks,
        IReadOnlyList<ContainerVolume> volumes)
    {
        Services = services;
        Networks = networks;
        Volumes = volumes;
    }

    public IReadOnlyList<ContainerService> Services { get; }

    public IReadOnlyList<ContainerNetwork> Networks { get; }

    public IReadOnlyList<ContainerVolume> Volumes { get; }

    /// <summary>True when normalization had to invent the <c>default</c> network declaration.</summary>
    public bool DefaultNetworkWasMaterialized => Networks.Any(network =>
        network.Name == TopologyNormalizer.DefaultNetworkName && network.Origin == DeclarationOrigin.Implicit);

    public ContainerService? FindService(string name) =>
        Services.FirstOrDefault(service => service.Name == name);
}
