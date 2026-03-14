namespace WebVN.Editor.Models;

public sealed class AssetRecord
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public AssetKind Kind { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string? PublicUrl { get; set; }

    public string? SourceFileName { get; set; }

    public string? EmbeddedDataUrl { get; set; }
}
