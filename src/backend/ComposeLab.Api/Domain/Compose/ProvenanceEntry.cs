using ComposeLab.Api.Domain.Topology;

namespace ComposeLab.Api.Domain.Compose;

/// <summary>
/// Ties one topology element to the lines it produced.
/// </summary>
/// <remarks>
/// This is the mechanism behind the visual-to-YAML correspondence. Because the generator knows which
/// element wrote which lines, the mapping is a byproduct of generation rather than something the
/// frontend has to infer, and an issue's element reference resolves to the learner's real
/// configuration instead of to a reconstructed snippet.
/// </remarks>
public sealed record ProvenanceEntry(ElementReference Element, YamlRange Range);
