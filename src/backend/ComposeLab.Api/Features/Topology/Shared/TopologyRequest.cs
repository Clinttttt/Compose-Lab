namespace ComposeLab.Api.Features.Topology.Shared;

/// <summary>
/// The architecture, as it arrives over the wire. The engine is stateless and takes the whole topology
/// in the request body, so no operation requires a saved project and the backend holds no workspace
/// state.
/// </summary>
/// <remarks>
/// Each slice's message derives from this rather than wrapping it, which keeps the request body a flat
/// topology document across every engine endpoint. It lives in <c>Shared/</c> because more than one
/// sibling slice needs it — simulate, and now generate.
/// </remarks>
public record TopologyRequest
{
    public IReadOnlyList<ServiceRequest> Services { get; init; } = [];

    public IReadOnlyList<NetworkRequest> Networks { get; init; } = [];

    public IReadOnlyList<VolumeRequest> Volumes { get; init; } = [];
}

public sealed record ServiceRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Image { get; init; }

    public string? Build { get; init; }

    /// <summary>Empty means the service is not published to the host.</summary>
    public IReadOnlyList<PortRequest> Ports { get; init; } = [];

    public IReadOnlyList<string> Networks { get; init; } = [];

    public IReadOnlyList<VolumeMountRequest> Volumes { get; init; } = [];

    public IReadOnlyList<DependencyRequest> DependsOn { get; init; } = [];

    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Omit entirely when no healthcheck is declared.</summary>
    public HealthCheckRequest? HealthCheck { get; init; }
}

/// <summary>
/// A published port. Omit <see cref="HostPort"/> for Compose's <c>ports: ["8080"]</c> form, where
/// Docker assigns the host port. Omitting the whole mapping is what "not published" means.
/// </summary>
public sealed record PortRequest
{
    public int? HostPort { get; init; }

    public int ContainerPort { get; init; }

    /// <summary><c>tcp</c> or <c>udp</c>. Defaults to <c>tcp</c>, as Compose does.</summary>
    public string? Protocol { get; init; }
}

public sealed record VolumeMountRequest
{
    public string Volume { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;
}

public sealed record DependencyRequest
{
    public string Service { get; init; } = string.Empty;

    /// <summary>
    /// <c>service_started</c> or <c>service_healthy</c>. Defaults to <c>service_started</c>, which is
    /// what Compose's short list form means.
    /// </summary>
    public string? Condition { get; init; }
}

/// <summary>
/// A declared healthcheck. ComposeLab models which of Compose's test forms was used and never runs the
/// test, nor models interval, timeout, retries, or start period.
/// </summary>
/// <remarks>
/// <see cref="Form"/> is an explicit discriminator rather than Compose's string-or-list <c>test</c>
/// value, so the contract needs no custom deserialization and the client cannot express a healthcheck
/// that has no valid Compose form.
/// </remarks>
public sealed record HealthCheckRequest
{
    /// <summary><c>disabled</c>, <c>shell</c>, <c>command</c>, or <c>command_shell</c>.</summary>
    public string Form { get; init; } = string.Empty;

    /// <summary>
    /// Empty when disabled. Exactly one command for <c>shell</c> and <c>command_shell</c>. The argument
    /// vector, without the leading <c>CMD</c> token, for <c>command</c>.
    /// </summary>
    public IReadOnlyList<string> Test { get; init; } = [];
}

public sealed record NetworkRequest
{
    /// <summary>The key the architecture refers to this network by.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Compose's <c>name</c> attribute, when the created network name differs from the key.</summary>
    public string? ComposeName { get; init; }
}

public sealed record VolumeRequest
{
    /// <summary>The key the architecture refers to this volume by.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Compose's <c>name</c> attribute, when the created volume name differs from the key.</summary>
    public string? ComposeName { get; init; }
}
