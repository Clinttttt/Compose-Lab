namespace ComposeLab.Api.Domain.Compose;

/// <summary>A one-based, inclusive span of lines in a generated document.</summary>
public sealed record YamlRange(int StartLine, int EndLine)
{
    public int LineCount => EndLine - StartLine + 1;

    public bool Contains(int line) => line >= StartLine && line <= EndLine;
}
