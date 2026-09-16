using ComposeLab.Api.Abstractions.Messaging;
using ComposeLab.Api.Domain.Topology.Document;

namespace ComposeLab.Api.Features.Topology.GenerateCompose;

/// <summary>The architecture to write out as Compose YAML. Stateless: no saved project required.</summary>
public sealed record Query : TopologyDocument, IQuery<Response>;
