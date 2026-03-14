using WebVN.Editor.Models;

namespace WebVN.Editor.Services;

public static class SampleProjectFactory
{
    public static EditorProject Create()
    {
        return new EditorProject
        {
            Name = "Untitled Project",
            ScriptSource = new CsvImportSource
            {
                FileName = string.Empty
            },
            Script = new ScriptDocument
            {
                Title = "Untitled Scene",
                SourceName = string.Empty,
                Rows = []
            },
            Assets = [],
            SceneEdits = []
        };
    }
}
