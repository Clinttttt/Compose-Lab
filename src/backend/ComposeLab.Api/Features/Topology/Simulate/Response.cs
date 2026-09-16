using ComposeLab.Api.Features.Topology.Shared;

namespace ComposeLab.Api.Features.Topology.Simulate;

/// <summary>
/// The simulation outcome. Codes, phases, severities, and element kinds are serialized as stable
/// strings so the frontend can render from a code table without knowing about .NET enums.
/// </summary>
public sealed record Response
{
    /// <summary>False when the architecture has at least one error-severity issue.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>False when structural faults stopped the run before anything could start.</summary>
    public required bool Completed { get; init; }

    public IReadOnlyList<string> StartOrder { get; init; } = [];

    public IReadOnlyList<SimulationEventResponse> Events { get; init; } = [];

    public IReadOnlyList<SimulationIssueResponse> Issues { get; init; } = [];

    public IReadOnlyList<ReachabilityResponse> Reachability { get; init; } = [];

    public IReadOnlyList<InferredConnectionResponse> InferredConnections { get; init; } = [];
}

public sealed record SimulationEventResponse(
    int Step,
    string Phase,
    string Code,
    string Severity,
    IReadOnlyList<ElementResponse> Elements,
    string Message);

/// <summary>
/// A finding and its explanation. There is no YAML field: <see cref="Elements"/> resolves against the
/// provenance returned by Compose generation, so the learner is shown their real configuration rather
/// than a reconstruction of it.
/// </summary>
public sealed record SimulationIssueResponse(
    string Code,
    string Severity,
    IReadOnlyList<ElementResponse> Elements,
    string WhatHappened,
    string Why,
    string ArchitectureBehavior,
    string SuggestedFix);

public sealed record ReachabilityResponse(
    string ServiceA,
    string ServiceB,
    bool CanCommunicate,
    IReadOnlyList<string> SharedNetworks);

/// <summary>An intent read from environment configuration. Inferred, not declared.</summary>
public sealed record InferredConnectionResponse(
    string FromService,
    string ToService,
    string EnvironmentKey,
    string Host);
