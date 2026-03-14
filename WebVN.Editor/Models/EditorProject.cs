namespace WebVN.Editor.Models;

public sealed class EditorProject
{
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset LastModifiedUtc { get; set; } = DateTimeOffset.UtcNow;

    public CsvImportSource ScriptSource { get; set; } = new();

    public ScriptDocument Script { get; set; } = new();

    public List<AssetRecord> Assets { get; set; } = [];

    public List<SceneEdit> SceneEdits { get; set; } = [];
}
