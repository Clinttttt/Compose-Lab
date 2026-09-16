namespace ComposeLab.Api.Domain.Simulation;

/// <summary>The outcome of one simulation run.</summary>
public sealed record SimulationResult
{
    /// <summary>True when the run produced no error-severity issue. Warnings do not fail a run.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// False when structural errors stopped the run before resources could be created. A learner
    /// needs to know the difference between "this architecture has a problem" and "this architecture
    /// could not be simulated at all".
    /// </summary>
    public required bool Completed { get; init; }

    /// <summary>
    /// ComposeLab's deterministic presentation order. Real Compose starts services with no
    /// dependency between them concurrently, so this is an ordering for reading, not a claim about
    /// Docker's behavior.
    /// </summary>
    public IReadOnlyList<string> StartOrder { get; init; } = [];

    public IReadOnlyList<SimulationEvent> Events { get; init; } = [];

    public IReadOnlyList<SimulationIssue> Issues { get; init; } = [];

    public IReadOnlyList<ReachabilityPair> Reachability { get; init; } = [];

    public IReadOnlyList<InferredConnection> InferredConnections { get; init; } = [];
}
