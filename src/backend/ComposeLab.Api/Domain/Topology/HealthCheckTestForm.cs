namespace ComposeLab.Api.Domain.Topology;

/// <summary>
/// The shape of a healthcheck test, as Compose distinguishes them.
/// </summary>
/// <remarks>
/// The distinction is not cosmetic. Compose treats a scalar test as a shell command and a
/// <c>CMD</c> list as a direct exec, so collapsing them would regenerate the wrong one and would
/// describe the wrong behavior to a learner.
/// </remarks>
public enum HealthCheckTestForm
{
    /// <summary>Health reporting is off, from <c>disable: true</c> or <c>test: ["NONE"]</c>.</summary>
    Disabled,

    /// <summary>A scalar test, which Compose runs through a shell — <c>test: pg_isready</c>.</summary>
    Shell,

    /// <summary>An exec-form test — <c>test: ["CMD", "pg_isready", "-U", "postgres"]</c>.</summary>
    Command,

    /// <summary>An explicit shell-form test — <c>test: ["CMD-SHELL", "pg_isready || exit 1"]</c>.</summary>
    CommandShell
}
