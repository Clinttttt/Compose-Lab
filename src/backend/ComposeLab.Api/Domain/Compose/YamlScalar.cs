namespace ComposeLab.Api.Domain.Compose;

/// <summary>
/// Decides whether a value can be written as a plain YAML scalar, and quotes it when it cannot.
/// </summary>
/// <remarks>
/// The bias is deliberately toward over-quoting. A value quoted unnecessarily is untidy; a value left
/// unquoted when it needed quotes changes meaning, and in a teaching tool that means generating a file
/// that says something the learner did not.
/// <para>
/// The trap worth naming is sexagesimal notation: under YAML 1.1, which Compose parsers accept, an
/// unquoted <c>8080:8080</c> is a base-60 number rather than the port mapping it looks like. Port
/// mappings are therefore always quoted.
/// </para>
/// </remarks>
internal static class YamlScalar
{
    private static readonly char[] LeadingIndicators =
    [
        '-', '?', ':', ',', '[', ']', '{', '}', '#', '&', '*', '!', '|', '>', '\'', '"', '%', '@', '`'
    ];

    /// <summary>Words a YAML 1.1 parser reads as a boolean or null rather than as text.</summary>
    private static readonly string[] ReservedWords =
    [
        "true", "false", "yes", "no", "on", "off", "y", "n", "null", "~"
    ];

    private const string NumericCharacters = "0123456789+-._:eExXaAbBcCdDfF";

    public static string Plain(string value) => NeedsQuotes(value) ? Quoted(value) : value;

    public static string Quoted(string value)
    {
        System.Text.StringBuilder builder = new(value.Length + 2);

        builder.Append('"');

        foreach (char character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        builder.Append("\\x").Append(((int)character).ToString("x2",
                            System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');

        return builder.ToString();
    }

    private static bool NeedsQuotes(string value)
    {
        if (value.Length == 0 || value != value.Trim())
        {
            return true;
        }

        if (Array.IndexOf(LeadingIndicators, value[0]) >= 0)
        {
            return true;
        }

        if (value.EndsWith(':') || value.Contains(": ", StringComparison.Ordinal)
            || value.Contains(" #", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.Any(char.IsControl))
        {
            return true;
        }

        if (ReservedWords.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return LooksNumeric(value);
    }

    /// <summary>
    /// Recognizes anything a YAML parser might read as a number — integers, floats, hex, and
    /// sexagesimal. Intentionally broad: a version string such as <c>1.0.0</c> gets quoted too, which
    /// costs nothing and removes a class of surprise.
    /// </summary>
    private static bool LooksNumeric(string value) =>
        value.Any(char.IsAsciiDigit)
        && value.All(character => NumericCharacters.Contains(character, StringComparison.Ordinal));
}
