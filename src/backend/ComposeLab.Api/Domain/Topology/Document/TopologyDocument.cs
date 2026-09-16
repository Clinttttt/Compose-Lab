namespace ComposeLab.Api.Domain.Topology.Document;

/// <summary>
/// The authored topology in serializable form: what the learner wrote, carried on the wire and stored as
/// one document.
/// </summary>
/// <remarks>
/// This is the authored architecture, never a normalized one. The implicit <c>default</c> network is
/// something Compose does and something ComposeLab explains — it is not user configuration, and it must
/// not become configuration just because a project was saved and reopened.
/// <para>
/// One contract serves transport and storage deliberately. Two shapes would drift, and
/// <see cref="TopologySchema.CurrentVersion"/> is the mechanism for evolving the single one.
/// </para>
/// </remarks>
public record TopologyDocument
{
    public IReadOnlyList<ServiceDocument> Services { get; init; } = [];

    public IReadOnlyList<NetworkDocument> Networks { get; init; } = [];

    public IReadOnlyList<VolumeDocument> Volumes { get; init; } = [];
}

public sealed record ServiceDocument
{
    public string Name { get; init; } = string.Empty;

    public string? Image { get; init; }

    public string? Build { get; init; }

    /// <summary>Empty means the service is not published to the host.</summary>
    public IReadOnlyList<PortDocument> Ports { get; init; } = [];

    public IReadOnlyList<string> Networks { get; init; } = [];

    public IReadOnlyList<VolumeMountDocument> Volumes { get; init; } = [];

    public IReadOnlyList<DependencyDocument> DependsOn { get; init; } = [];

    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Omitted entirely when no healthcheck is declared.</summary>
    public HealthCheckDocument? HealthCheck { get; init; }
}

/// <summary>
/// A published port. Omit <see cref="HostPort"/> for Compose's <c>ports: ["8080"]</c> form, where Docker
/// assigns the host port. Omitting the whole mapping is what "not published" means.
/// </summary>
public sealed record PortDocument
{
    public int? HostPort { get; init; }

    public int ContainerPort { get; init; }

    /// <summary><c>tcp</c> or <c>udp</c>. Defaults to <c>tcp</c>, as Compose does.</summary>
    public string? Protocol { get; init; }
}

public sealed record VolumeMountDocument
{
    public string Volume { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;
}

public sealed record DependencyDocument
{
    public string Service { get; init; } = string.Empty;

    /// <summary>
    /// <c>service_started</c> or <c>service_healthy</c>. Defaults to <c>service_started</c>, which is what
    /// Compose's short list form means.
    /// </summary>
    public string? Condition { get; init; }
}

/// <summary>
/// A declared healthcheck. <see cref="Form"/> is an explicit discriminator rather than Compose's
/// string-or-list <c>test</c> value, so the contract needs no custom deserialization and cannot express a
/// healthcheck that has no valid Compose form.
/// </summary>
public sealed record HealthCheckDocument
{
    /// <summary><c>disabled</c>, <c>shell</c>, <c>command</c>, or <c>command_shell</c>.</summary>
    public string Form { get; init; } = string.Empty;

    /// <summary>
    /// Empty when disabled. Exactly one command for <c>shell</c> and <c>command_shell</c>. The argument
    /// vector, without the leading <c>CMD</c> token, for <c>command</c>.
    /// </summary>
    public IReadOnlyList<string> Test { get; init; } = [];
}

public sealed record NetworkDocument
{
    /// <summary>The key the architecture refers to this network by.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Compose's <c>name</c> attribute, when the created network name differs from the key.</summary>
    public string? ComposeName { get; init; }
}

public sealed record VolumeDocument
{
    /// <summary>The key the architecture refers to this volume by.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Compose's <c>name</c> attribute, when the created volume name differs from the key.</summary>
    public string? ComposeName { get; init; }
}
