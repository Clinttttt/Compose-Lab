using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Topology.Document;

namespace ComposeLab.Api.Features.Topology.Validate;

/// <summary>The architecture to check. Stateless: the engine never needs a saved project.</summary>
public sealed record Query : TopologyDocument, IQuery<Response>;
