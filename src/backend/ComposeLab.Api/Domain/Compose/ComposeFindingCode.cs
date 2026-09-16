namespace ComposeLab.Api.Domain.Compose;

/// <summary>
/// The catalog of reasons a Compose file cannot be applied. Typed internally and serialized as stable
/// strings, like the simulation codes.
/// </summary>
public sealed record ComposeFindingCode
{
    private ComposeFindingCode(string value) => Value = value;

    public string Value { get; }

    /// <summary>The text is not valid YAML at all.</summary>
    public static readonly ComposeFindingCode SyntaxError = new("compose.syntax_error");

    /// <summary>Valid YAML, but a node is the wrong kind — a sequence where a mapping belongs.</summary>
    public static readonly ComposeFindingCode UnexpectedShape = new("compose.unexpected_shape");

    /// <summary>A real Compose key that ComposeLab does not model.</summary>
    public static readonly ComposeFindingCode KeyNotModeled = new("compose.key_not_modeled");

    /// <summary>Not a Compose key at all, so most likely a typo.</summary>
    public static readonly ComposeFindingCode UnknownKey = new("compose.unknown_key");

    /// <summary>A modeled key holding a form ComposeLab does not support, such as a port range.</summary>
    public static readonly ComposeFindingCode ValueNotModeled = new("compose.value_not_modeled");

    public override string ToString() => Value;
}
