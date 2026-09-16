using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Features.Topology.Shared;

namespace ComposeLab.Api.Features.Topology.Validate;

/// <summary>The architecture to check.</summary>
public sealed record Query : TopologyRequest, IQuery<Response>;
