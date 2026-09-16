using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Topology.Document;

namespace ComposeLab.Api.Features.Projects.Create;

/// <summary>
/// Saves a new project.
/// </summary>
/// <remarks>
/// The architecture does not have to be sound. Saving is held to the same malformed-input boundary as the
/// engine and no further: a learner can save duplicate service names, colliding ports, or a connection that
/// cannot work, and come back to inspect them.
/// </remarks>
public sealed record Command : ICommand<Guid>
{
    public string Name { get; init; } = string.Empty;

    public TopologyDocument Topology { get; init; } = new();
}
