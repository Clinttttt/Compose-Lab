namespace ComposeLab.Api.Domain.Topology;

/// <summary>
/// A Compose network. <see cref="Origin"/> records whether the declaration itself was written by the
/// author or materialized by normalization, independently of how any service attached to it.
/// </summary>
public sealed record ContainerNetwork
{
    /// <summary>The key the architecture refers to this network by.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Compose's <c>name</c> attribute — the actual network name Docker creates, when the author wants
    /// it to differ from the key. Null when unset, which is the usual case.
    /// </summary>
    public string? ComposeName { get; init; }

    public DeclarationOrigin Origin { get; init; } = DeclarationOrigin.Explicit;
}
