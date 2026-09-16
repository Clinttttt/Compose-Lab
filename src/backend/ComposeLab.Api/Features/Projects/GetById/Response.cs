using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Topology.Document;

namespace ComposeLab.Api.Features.Projects.GetById;

/// <summary>The complete saved architecture, in the same shape the client posts back.</summary>
public sealed record Response
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The authored topology, exactly as saved. Never a normalized one.</summary>
    public required TopologyDocument Topology { get; init; }

    public required int TopologySchemaVersion { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
