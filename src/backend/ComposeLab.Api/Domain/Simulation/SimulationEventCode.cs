namespace ComposeLab.Api.Domain.Simulation;

/// <summary>
/// The catalog of simulation timeline event codes. Typed internally so a code cannot be
/// mistyped the way a bare string literal can, and serialized as a stable string so the frontend can
/// render from a code table.
/// </summary>
public sealed record SimulationEventCode
{
    private SimulationEventCode(string value) => Value = value;

    public string Value { get; }

    public static readonly SimulationEventCode TopologyNormalized = new("topology.normalized");

    public static readonly SimulationEventCode NetworkCreated = new("network.created");

    public static readonly SimulationEventCode VolumeCreated = new("volume.created");

    public static readonly SimulationEventCode StartOrderResolved = new("dependency.start_order_resolved");

    public static readonly SimulationEventCode DependencySatisfied = new("service.dependency_satisfied");

    public static readonly SimulationEventCode NetworkAttached = new("service.network_attached");

    public static readonly SimulationEventCode VolumeMounted = new("service.volume_mounted");

    public static readonly SimulationEventCode PortPublished = new("service.port_published");

    public static readonly SimulationEventCode ServiceNotPublished = new("service.not_published");

    public static readonly SimulationEventCode ServiceStarted = new("service.started");

    public static readonly SimulationEventCode ReachabilityEvaluated = new("reachability.evaluated");

    public static readonly SimulationEventCode SimulationCompleted = new("simulation.completed");

    public static readonly SimulationEventCode SimulationAborted = new("simulation.aborted");

    public override string ToString() => Value;
}
