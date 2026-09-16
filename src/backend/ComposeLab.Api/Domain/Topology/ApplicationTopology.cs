namespace ComposeLab.Api.Domain.Topology;

/// <summary>
/// The architecture under analysis, modeled independently of YAML.
/// </summary>
/// <remarks>
/// This type deliberately permits invalid configurations. Duplicate service names, references to
/// undeclared networks, dependency cycles, and colliding host ports are all representable, because
/// explaining a broken architecture is the product rather than an error condition. Invariants are
/// therefore reported by the simulation rule catalog instead of being enforced by a factory — the
/// opposite of how a persistence entity is built, and for the opposite reason.
/// </remarks>
public sealed record ApplicationTopology
{
    public IReadOnlyList<ContainerService> Services { get; init; } = [];

    public IReadOnlyList<ContainerNetwork> Networks { get; init; } = [];

    public IReadOnlyList<ContainerVolume> Volumes { get; init; } = [];
}
