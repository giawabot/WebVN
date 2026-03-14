namespace WebVN.Editor.Models;

public sealed class DialogueChoiceSet
{
    public int TriggerRowNumber { get; set; }

    public ChoicePopupAlignment Alignment { get; set; } = ChoicePopupAlignment.Center;

    public List<DialogueChoiceOption> Options { get; set; } = [];
}
