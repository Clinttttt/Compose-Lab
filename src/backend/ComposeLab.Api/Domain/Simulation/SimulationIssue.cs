using ComposeLab.Api.Domain.Topology;

namespace ComposeLab.Api.Domain.Simulation;

/// <summary>
/// A finding, carrying its teaching explanation as separate structured fields rather than one
/// pre-formatted blob, so the frontend controls presentation while the backend owns the words.
/// </summary>
/// <remarks>
/// There is deliberately no "relevant YAML" field. <see cref="Elements"/> resolves through the
/// generator's provenance metadata to the live YAML ranges, which keeps a single source of YAML and
/// guarantees the learner is shown their actual configuration.
/// </remarks>
public sealed record SimulationIssue(
    SimulationIssueCode Code,
    SimulationSeverity Severity,
    IReadOnlyList<ElementReference> Elements,
    string WhatHappened,
    string Why,
    string ArchitectureBehavior,
    string SuggestedFix);
