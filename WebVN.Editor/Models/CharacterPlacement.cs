namespace WebVN.Editor.Models;

public sealed class CharacterPlacement
{
    public string CharacterId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public double Scale { get; set; } = 1;

    public int Layer { get; set; }

    public int TintR { get; set; } = 255;

    public int TintG { get; set; } = 255;

    public int TintB { get; set; } = 255;

    public bool FlipX { get; set; }
}
