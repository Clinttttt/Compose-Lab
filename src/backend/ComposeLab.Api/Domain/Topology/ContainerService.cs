namespace ComposeLab.Api.Domain.Topology;

/// <summary>A containerized workload in the architecture.</summary>
public sealed record ContainerService
{
    public required string Name { get; init; }

    public string? Image { get; init; }

    public string? BuildContext { get; init; }

    /// <summary>
    /// Empty means the service is not published to the host, which is the correct configuration for
    /// a service only other services need to reach.
    /// </summary>
    public IReadOnlyList<PortMapping> Ports { get; init; } = [];

    public IReadOnlyList<NetworkAttachment> Networks { get; init; } = [];

    public IReadOnlyList<VolumeMount> Volumes { get; init; } = [];

    public IReadOnlyList<ServiceDependency> Dependencies { get; init; } = [];

    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Null means no healthcheck is declared in the supported Compose subset.</summary>
    public HealthCheckDeclaration? HealthCheck { get; init; }

    public bool IsPublishedToHost => Ports.Count > 0;

    public bool HasEnabledHealthCheck => HealthCheck is { IsEnabled: true };

    public bool HasRunnableSource => !string.IsNullOrWhiteSpace(Image) || !string.IsNullOrWhiteSpace(BuildContext);
}
