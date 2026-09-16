using ComposeLab.Api.Features.Topology.Shared;

namespace ComposeLab.Api.Features.Topology.GenerateCompose;

/// <summary>The generated Compose file, with the map from topology elements to the lines they produced.</summary>
public sealed record Response
{
    public required string Yaml { get; init; }

    /// <summary>
    /// One entry per element that produced output. This is what the frontend uses to highlight the YAML
    /// for a selected node, to select a node from a clicked line, and to resolve the configuration
    /// behind a simulation issue — so it never has to infer the mapping itself.
    /// </summary>
    public IReadOnlyList<ProvenanceResponse> Provenance { get; init; } = [];
}

/// <summary>A one-based, inclusive line span.</summary>
public sealed record ProvenanceResponse(
    ElementResponse Element,
    int StartLine,
    int EndLine);
