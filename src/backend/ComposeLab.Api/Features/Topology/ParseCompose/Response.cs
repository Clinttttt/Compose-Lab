using ComposeLab.Api.Features.Topology.Shared;

namespace ComposeLab.Api.Features.Topology.ParseCompose;

/// <summary>
/// Either a topology or the reasons the file cannot be applied. Never both.
/// </summary>
/// <remarks>
/// <see cref="Topology"/> comes back in the same shape the client posts to the other engine routes, so
/// applying a parsed file is a straight swap of the working topology rather than a translation step.
/// </remarks>
public sealed record Response
{
    /// <summary>
    /// True when the whole file was understood. False means nothing should be replaced: any valid Compose
    /// key ComposeLab does not model, and any key that is not Compose at all, blocks the apply so that
    /// nothing is silently discarded.
    /// </summary>
    public required bool CanApply { get; init; }

    public TopologyRequest? Topology { get; init; }

    public IReadOnlyList<ComposeFindingResponse> Findings { get; init; } = [];
}

/// <summary>One reason the file was not applied, located in the source text.</summary>
public sealed record ComposeFindingResponse(
    string Code,
    string Path,
    int Line,
    int Column,
    string Message,
    string Suggestion);
