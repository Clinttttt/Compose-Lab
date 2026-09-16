using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Topology.Document;

namespace ComposeLab.Api.Features.Projects.Update;

/// <summary>
/// Replaces a project's name and topology in one step.
/// </summary>
/// <remarks>
/// The topology is replaced wholesale rather than patched. There is no history and no autosave: this is the
/// explicit save, and it is the only thing that changes what is stored.
/// </remarks>
public sealed record Command : ICommand
{
    /// <summary>Taken from the route; anything in the body is ignored.</summary>
    public Guid ProjectId { get; init; }

    public string Name { get; init; } = string.Empty;

    public TopologyDocument Topology { get; init; } = new();
}
