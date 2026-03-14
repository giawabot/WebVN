namespace WebVN.Editor.Models;

public sealed class ScriptDocument
{
    public string Title { get; set; } = string.Empty;

    public string SourceType { get; set; } = "csv";

    public string? SourceName { get; set; }

    public List<ScriptRow> Rows { get; set; } = [];
}
