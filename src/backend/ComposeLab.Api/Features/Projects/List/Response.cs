namespace ComposeLab.Api.Features.Projects.List;

/// <summary>
/// Lightweight project summaries.
/// </summary>
/// <remarks>
/// Deliberately no topology. Listing projects must not read every stored document, so this carries only the
/// metadata columns. A client that needs an architecture asks for one project.
/// </remarks>
public sealed record Response
{
    public IReadOnlyList<ProjectSummaryResponse> Projects { get; init; } = [];
}

public sealed record ProjectSummaryResponse(
    Guid Id,
    string Name,
    int TopologySchemaVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
