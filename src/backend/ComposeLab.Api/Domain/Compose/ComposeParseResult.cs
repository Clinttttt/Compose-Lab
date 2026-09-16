using ComposeLab.Api.Domain.Topology;

namespace ComposeLab.Api.Domain.Compose;

/// <summary>
/// The outcome of reading a Compose file: either a topology, or the reasons it cannot be applied. Never
/// both.
/// </summary>
/// <remarks>
/// All or nothing is the point. A partial apply would leave the learner with a topology that quietly
/// disagrees with the file they wrote, and they would have no way to know which parts survived.
/// </remarks>
public sealed record ComposeParseResult
{
    private ComposeParseResult(ApplicationTopology? topology, IReadOnlyList<ComposeFinding> findings)
    {
        Topology = topology;
        Findings = findings;
    }

    /// <summary>True when the file was fully understood and may replace the working topology.</summary>
    public bool CanApply => Topology is not null;

    public ApplicationTopology? Topology { get; }

    public IReadOnlyList<ComposeFinding> Findings { get; }

    public static ComposeParseResult Applicable(ApplicationTopology topology) =>
        new(topology, []);

    public static ComposeParseResult Blocked(IReadOnlyList<ComposeFinding> findings) =>
        findings.Count == 0
            ? throw new ArgumentException("A blocked parse must explain itself.", nameof(findings))
            : new ComposeParseResult(null, findings);
}
