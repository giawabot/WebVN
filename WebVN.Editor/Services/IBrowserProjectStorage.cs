using WebVN.Editor.Models;

namespace WebVN.Editor.Services;

public interface IBrowserProjectStorage
{
    ValueTask SaveAsync(EditorProject project, CancellationToken cancellationToken = default);

    ValueTask SaveAssetAsync(AssetRecord asset, CancellationToken cancellationToken = default);

    ValueTask<EditorProject?> LoadAsync(CancellationToken cancellationToken = default);
}
