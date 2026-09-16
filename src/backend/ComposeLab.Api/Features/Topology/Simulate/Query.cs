using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Features.Topology.Shared;

namespace ComposeLab.Api.Features.Topology.Simulate;

/// <summary>The architecture to simulate. Carries no state beyond the topology itself.</summary>
public sealed record Query : TopologyRequest, IQuery<Response>;
