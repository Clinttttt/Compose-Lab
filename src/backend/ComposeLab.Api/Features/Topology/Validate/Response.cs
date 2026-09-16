using ComposeLab.Api.Features.Topology.Shared;

namespace ComposeLab.Api.Features.Topology.Validate;

/// <summary>
/// What is wrong with an architecture, with no timeline attached.
/// </summary>
/// <remarks>
/// Deliberately small. This is the check meant to run after every edit, so it answers only the question
/// the inspector needs answered and leaves the ordered story of what would happen to the simulate route.
/// </remarks>
public sealed record Response
{
    /// <summary>True when nothing error-severity was found. Warnings do not make an architecture invalid.</summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// False when structural faults stopped the check early, so the list is not yet the whole story.
    /// </summary>
    public required bool IsComplete { get; init; }

    public IReadOnlyList<ArchitectureIssueResponse> Issues { get; init; } = [];
}
