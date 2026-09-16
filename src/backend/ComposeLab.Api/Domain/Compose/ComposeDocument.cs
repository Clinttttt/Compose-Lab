using ComposeLab.Api.Domain.Topology;

namespace ComposeLab.Api.Domain.Compose;

/// <summary>A generated Compose file and the element-to-line map that goes with it.</summary>
public sealed record ComposeDocument
{
    public required string Yaml { get; init; }

    public IReadOnlyList<ProvenanceEntry> Provenance { get; init; } = [];

    public YamlRange? RangeOf(ElementReference element) =>
        Provenance.FirstOrDefault(entry => entry.Element == element)?.Range;
}
