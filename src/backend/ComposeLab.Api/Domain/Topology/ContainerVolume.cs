namespace ComposeLab.Api.Domain.Topology;

/// <summary>A named volume, which exists independently of any one container.</summary>
public sealed record ContainerVolume
{
    /// <summary>The key the architecture refers to this volume by.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Compose's <c>name</c> attribute — the actual volume name Docker creates, when the author wants it
    /// to differ from the key. Null when unset, which is the usual case.
    /// </summary>
    public string? ComposeName { get; init; }
}
