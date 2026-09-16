namespace ComposeLab.Api.Domain.Simulation;

/// <summary>The findings for one architecture, with no timeline attached.</summary>
public sealed record ValidationReport
{
    /// <summary>True when nothing error-severity was found. Warnings do not make an architecture invalid.</summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// False when structural faults stopped the check before the advisory rules could run, so the learner
    /// knows the list is not the whole story yet.
    /// </summary>
    public required bool IsComplete { get; init; }

    public IReadOnlyList<SimulationIssue> Issues { get; init; } = [];
}
