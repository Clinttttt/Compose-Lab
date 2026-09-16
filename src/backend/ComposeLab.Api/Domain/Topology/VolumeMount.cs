namespace ComposeLab.Api.Domain.Topology;

/// <summary>
/// A named volume mounted at a path inside a container. Bind mounts are outside the supported
/// Compose subset and are reported rather than modeled.
/// </summary>
public sealed record VolumeMount(string VolumeName, string ContainerPath);
