using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Features.Topology.Shared;

namespace ComposeLab.Api.Features.Topology.GenerateCompose;

/// <summary>The architecture to write out as Compose YAML.</summary>
public sealed record Query : TopologyRequest, IQuery<Response>;
