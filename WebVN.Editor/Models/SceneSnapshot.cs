namespace WebVN.Editor.Models;

public sealed class SceneSnapshot
{
    public string? BackgroundAssetId { get; set; }

    public string? MusicCueName { get; set; }

    public List<string> OneShotSfxCueNames { get; set; } = [];

    public List<CharacterPlacement> Characters { get; set; } = [];
}
