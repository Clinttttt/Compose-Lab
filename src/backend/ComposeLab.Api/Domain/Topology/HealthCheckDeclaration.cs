namespace ComposeLab.Api.Domain.Topology;

/// <summary>
/// A healthcheck as ComposeLab models it: a declared test in one of Compose's forms, or health
/// reporting explicitly turned off.
/// </summary>
/// <remarks>
/// ComposeLab does not execute the test and does not model interval, timeout, retries, start period, or
/// start interval. Health is a declared property here, never an observed one, and every message about
/// readiness has to say so.
/// <para>
/// Modeling the form semantically rather than storing a flattened command has a concrete payoff: every
/// state this type can represent has a valid Compose form, so generation can always write a declared
/// healthcheck back out. "Enabled but with nothing to run" is not expressible.
/// </para>
/// <para>
/// The absence of this type on a service means no healthcheck is declared in the supported Compose
/// subset. It does not prove the image lacks a Dockerfile <c>HEALTHCHECK</c>, so absence must never be
/// reported as invalid Compose.
/// </para>
/// </remarks>
public sealed record HealthCheckDeclaration
{
    private HealthCheckDeclaration(HealthCheckTestForm form, IReadOnlyList<string> arguments)
    {
        Form = form;
        Arguments = arguments;
    }

    public HealthCheckTestForm Form { get; }

    /// <summary>
    /// Empty when disabled, a single command for the shell forms, and the argument vector for
    /// <see cref="HealthCheckTestForm.Command"/> — without the leading <c>CMD</c> token, which is form
    /// rather than content.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    public bool IsEnabled => Form != HealthCheckTestForm.Disabled;

    /// <summary>Health reporting turned off, from <c>disable: true</c> or <c>test: ["NONE"]</c>.</summary>
    public static readonly HealthCheckDeclaration Disabled = new(HealthCheckTestForm.Disabled, []);

    /// <summary>A scalar test, which Compose runs through a shell.</summary>
    public static HealthCheckDeclaration Shell(string command) =>
        new(HealthCheckTestForm.Shell, [command]);

    /// <summary>An exec-form test. Pass the arguments without the leading <c>CMD</c> token.</summary>
    public static HealthCheckDeclaration Command(IReadOnlyList<string> arguments) =>
        new(HealthCheckTestForm.Command, [.. arguments]);

    /// <summary>An explicit <c>CMD-SHELL</c> test.</summary>
    public static HealthCheckDeclaration CommandShell(string command) =>
        new(HealthCheckTestForm.CommandShell, [command]);
}
