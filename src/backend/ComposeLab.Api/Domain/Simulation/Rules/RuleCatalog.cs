using ComposeLab.Api.Domain.Topology.Normalization;

namespace ComposeLab.Api.Domain.Simulation.Rules;

/// <summary>
/// The single place the rule families are composed, so that simulating and validating an architecture can
/// never disagree about it.
/// </summary>
/// <remarks>
/// Two entry points, matching the two things a caller can want. <see cref="Structural"/> finds faults that
/// make an architecture impossible to run at all; <see cref="Advisory"/> finds everything else. The split
/// is not cosmetic: when structure is broken, the advisory rules would be reasoning about a topology that
/// cannot exist, so both callers stop after the first stage.
/// </remarks>
internal static class RuleCatalog
{
    /// <summary>Faults that stop an architecture from running. All error severity.</summary>
    public static IReadOnlyList<SimulationIssue> Structural(NormalizedTopology topology) =>
    [
        .. StructureRules.Evaluate(topology),
        .. PortRules.Evaluate(topology)
    ];

    /// <summary>
    /// Findings that need a runnable architecture to make sense: unsatisfied communication intent, missing
    /// persistence, and what a dependency actually guarantees.
    /// </summary>
    public static IReadOnlyList<SimulationIssue> Advisory(
        NormalizedTopology topology,
        IReadOnlyList<InferredConnection> inferredConnections) =>
    [
        .. ReadinessRules.Evaluate(topology),
        .. PersistenceRules.Evaluate(topology),
        .. ReachabilityRules.Evaluate(topology, inferredConnections)
    ];

    public static bool HasError(IEnumerable<SimulationIssue> issues) =>
        issues.Any(issue => issue.Severity == SimulationSeverity.Error);
}
