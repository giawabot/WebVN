using WebVN.Editor.Models;

namespace WebVN.Editor.Services;

public interface ICsvScriptImportService
{
    Task<ScriptDocument> ImportAsync(Stream csvStream, string fileName, CancellationToken cancellationToken = default);
}
