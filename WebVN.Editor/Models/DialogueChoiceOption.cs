namespace WebVN.Editor.Models;

public sealed class DialogueChoiceOption
{
    public int RowNumber { get; set; }

    public string Text { get; set; } = string.Empty;

    public string TargetAnchorId { get; set; } = string.Empty;
}
