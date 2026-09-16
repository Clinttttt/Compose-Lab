using System.Text;

namespace ComposeLab.Api.Domain.Compose;

/// <summary>
/// Builds a document line by line while keeping count, so that whoever writes a line knows which line
/// number it landed on.
/// </summary>
internal sealed class YamlWriter
{
    private const string Indent = "  ";

    private readonly StringBuilder _builder = new();

    /// <summary>The number of the last line written. Zero before anything is written.</summary>
    public int LastLine { get; private set; }

    /// <summary>Writes one line and returns its one-based line number.</summary>
    public int Write(int depth, string content)
    {
        for (int level = 0; level < depth; level++)
        {
            _builder.Append(Indent);
        }

        _builder.Append(content).Append('\n');

        return ++LastLine;
    }

    /// <summary>Writes a block sequence item, as <c>- value</c>.</summary>
    public int WriteItem(int depth, string value) => Write(depth, $"- {value}");

    /// <summary>Writes a mapping key with no value, as <c>key:</c>.</summary>
    public int WriteKey(int depth, string key) => Write(depth, $"{YamlScalar.Plain(key)}:");

    /// <summary>Writes a mapping entry, as <c>key: value</c>.</summary>
    public int WriteEntry(int depth, string key, string value) =>
        Write(depth, $"{YamlScalar.Plain(key)}: {value}");

    public void WriteBlankLine()
    {
        _builder.Append('\n');

        LastLine++;
    }

    public override string ToString() => _builder.ToString();
}
