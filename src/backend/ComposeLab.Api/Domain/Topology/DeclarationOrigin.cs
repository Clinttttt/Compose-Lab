namespace ComposeLab.Api.Domain.Topology;

/// <summary>
/// Whether an element exists because the author wrote it, or because Compose implies it.
/// </summary>
/// <remarks>
/// This distinction is tracked in two independent places: on a network declaration, and on each
/// service-to-network attachment. Both are needed. A top-level <c>default</c> network that the
/// author configured explicitly must survive regeneration even when every attachment to it was
/// implicit, and an implicitly created network must never be written back out as if the author
/// had declared it.
/// </remarks>
public enum DeclarationOrigin
{
    /// <summary>The author wrote this element.</summary>
    Explicit,

    /// <summary>Compose implies this element; ComposeLab materialized it during normalization.</summary>
    Implicit
}
