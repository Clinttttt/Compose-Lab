namespace ComposeLab.Api.Domain.Compose;

/// <summary>
/// The Compose keys ComposeLab recognizes, which is a much larger set than the keys it models.
/// </summary>
/// <remarks>
/// This catalog exists to separate two findings a learner needs to tell apart: a key that is real
/// Compose but outside ComposeLab's subset, and a key that is not Compose at all and is probably a typo.
/// Collapsing them into one "unsupported" bucket would throw away the cheapest teaching win available —
/// catching <c>enviroment</c> before Docker does.
/// <para>
/// Maintained by hand against the Compose specification, and therefore able to lag it. A genuinely new
/// Compose key would be reported as a probable typo until it is added here. That is the same species of
/// limitation as the stateful-image catalog: wrong in a visible, correctable way rather than silent.
/// </para>
/// </remarks>
internal static class ComposeKeyCatalog
{
    /// <summary>Compose's own escape hatch for custom data. Valid anywhere, and modeled nowhere.</summary>
    public const string ExtensionPrefix = "x-";

    public static readonly IReadOnlySet<string> TopLevel = new HashSet<string>(StringComparer.Ordinal)
    {
        "configs",
        "include",
        "name",
        "networks",
        "secrets",
        "services",
        "version",
        "volumes"
    };

    public static readonly IReadOnlySet<string> Service = new HashSet<string>(StringComparer.Ordinal)
    {
        "annotations", "attach", "blkio_config", "build", "cap_add", "cap_drop", "cgroup",
        "cgroup_parent", "command", "configs", "container_name", "cpu_count", "cpu_percent",
        "cpu_period", "cpu_quota", "cpu_rt_period", "cpu_rt_runtime", "cpu_shares", "cpus", "cpuset",
        "credential_spec", "depends_on", "deploy", "develop", "device_cgroup_rules", "devices", "dns",
        "dns_opt", "dns_search", "domainname", "entrypoint", "env_file", "environment", "expose",
        "extends", "external_links", "extra_hosts", "gpus", "group_add", "healthcheck", "hostname",
        "image", "init", "ipc", "isolation", "label_file", "labels", "links", "logging", "mac_address",
        "mem_limit", "mem_reservation", "mem_swappiness", "memswap_limit", "network_mode", "networks",
        "oom_kill_disable", "oom_score_adj", "pid", "pids_limit", "platform", "post_start", "ports",
        "pre_stop", "privileged", "profiles", "provider", "pull_policy", "read_only", "restart",
        "runtime", "scale", "secrets", "security_opt", "shm_size", "stdin_open", "stop_grace_period",
        "stop_signal", "storage_opt", "sysctls", "tmpfs", "tty", "ulimits", "user", "userns_mode",
        "uts", "volumes", "volumes_from", "working_dir"
    };

    public static readonly IReadOnlySet<string> Network = new HashSet<string>(StringComparer.Ordinal)
    {
        "attachable",
        "driver",
        "driver_opts",
        "enable_ipv4",
        "enable_ipv6",
        "external",
        "internal",
        "ipam",
        "labels",
        "name"
    };

    public static readonly IReadOnlySet<string> Volume = new HashSet<string>(StringComparer.Ordinal)
    {
        "driver",
        "driver_opts",
        "external",
        "labels",
        "name"
    };

    public static readonly IReadOnlySet<string> HealthCheck = new HashSet<string>(StringComparer.Ordinal)
    {
        "disable",
        "interval",
        "retries",
        "start_interval",
        "start_period",
        "test",
        "timeout"
    };

    public static readonly IReadOnlySet<string> DependsOn = new HashSet<string>(StringComparer.Ordinal)
    {
        "condition",
        "required",
        "restart"
    };

    public static bool IsExtension(string key) =>
        key.StartsWith(ExtensionPrefix, StringComparison.Ordinal);
}
