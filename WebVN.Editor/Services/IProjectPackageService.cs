using WebVN.Editor.Models;

namespace WebVN.Editor.Services;

public interface IProjectPackageService
{
    ValueTask ExportAsync(EditorProject project, CancellationToken cancellationToken = default);

    Task<EditorProject> ImportAsync(Stream packageStream, CancellationToken cancellationToken = default);
}
