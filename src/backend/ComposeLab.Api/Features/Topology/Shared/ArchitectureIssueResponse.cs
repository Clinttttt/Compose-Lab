using ComposeLab.Api.Domain.Simulation;

namespace ComposeLab.Api.Features.Topology.Shared;

/// <summary>
/// An architectural finding and its explanation, carried as separate structured fields so the frontend
/// controls presentation while the backend owns the words.
/// </summary>
/// <remarks>
/// Shared by validation and simulation, which is the point: both read the same rule catalog, so both
/// report findings in the same shape and a learner sees the same explanation either way.
/// <para>
/// There is deliberately no YAML field. <see cref="Elements"/> resolves against the provenance returned by
/// Compose generation, so the learner is shown their real configuration rather than a reconstruction.
/// </para>
/// </remarks>
public sealed record ArchitectureIssueResponse(
    string Code,
    string Severity,
    IReadOnlyList<ElementResponse> Elements,
    string WhatHappened,
    string Why,
    string ArchitectureBehavior,
    string SuggestedFix)
{
    public static ArchitectureIssueResponse From(SimulationIssue issue) => new(
        issue.Code.Value,
        SeverityToken(issue.Severity),
        [.. issue.Elements.Select(ElementResponse.From)],
        issue.WhatHappened,
        issue.Why,
        issue.ArchitectureBehavior,
        issue.SuggestedFix);

    public static string SeverityToken(SimulationSeverity severity) => severity switch
    {
        SimulationSeverity.Information => "information",
        SimulationSeverity.Warning => "warning",
        SimulationSeverity.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unmapped severity.")
    };
}
