namespace WebVN.Editor.Models;

public sealed class ScriptRow
{
    public int RowNumber { get; set; }

    public string? AnchorId { get; set; }

    public string Content { get; set; } = string.Empty;

    public ScriptActionType ActionType { get; set; }

    public string? Character { get; set; }

    public string? Condition { get; set; }

    public string? ActionEffect { get; set; }

    public string? Notes { get; set; }

    public bool HasAnchor => !string.IsNullOrWhiteSpace(AnchorId);

    public string ActionLabel => ActionType switch
    {
        ScriptActionType.EventId => "StoryId",
        ScriptActionType.SkillCheck => "JumpWhen",
        _ => ActionType.ToString()
    };
}
