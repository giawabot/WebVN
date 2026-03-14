namespace WebVN.Editor.Models;

public sealed class SceneEdit
{
    public int ScriptRowNumber { get; set; }

    public string ScriptRowAnchorId { get; set; } = string.Empty;

    public string? BackgroundAssetId { get; set; }

    public string? MusicCueName { get; set; }

    public bool StopMusic { get; set; }

    public List<string> OneShotSfxCueNames { get; set; } = [];

    public ChoicePopupAlignment? ChoicePopupAlignment { get; set; }

    public List<CharacterPlacement> Characters { get; set; } = [];
}
