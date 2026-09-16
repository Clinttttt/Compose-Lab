namespace ComposeLab.Api.Domain.Compose;

/// <summary>
/// One reason a Compose file was not applied, located in the source text.
/// </summary>
/// <remarks>
/// Findings are about the file, not the architecture, which is why they carry a path and a position
/// rather than the four-part architectural explanation a simulation issue carries. Every finding blocks:
/// applying a file that ComposeLab only partly understands would silently discard the rest, and silent
/// loss is the worst failure available to a tool whose job is explaining configuration.
/// </remarks>
public sealed record ComposeFinding(
    ComposeFindingCode Code,
    string Path,
    int Line,
    int Column,
    string Message,
    string Suggestion)
{
    /// <summary>The dotted path to the offending node, such as <c>services.api.restart</c>.</summary>
    public string Path { get; } = Path;
}
