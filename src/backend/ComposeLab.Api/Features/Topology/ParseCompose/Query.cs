using ComposeLab.Api.Abstractions.Messaging;

namespace ComposeLab.Api.Features.Topology.ParseCompose;

/// <summary>Compose YAML to read into a topology.</summary>
public sealed record Query : IQuery<Response>
{
    public string Yaml { get; init; } = string.Empty;
}
