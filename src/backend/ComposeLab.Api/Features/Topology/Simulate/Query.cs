using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Topology.Document;

namespace ComposeLab.Api.Features.Topology.Simulate;

/// <summary>The architecture to simulate. Stateless: the engine never needs a saved project.</summary>
public sealed record Query : TopologyDocument, IQuery<Response>;
