using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.JSInterop;
using WebVN.Editor.Models;

namespace WebVN.Editor.Services;

public sealed class BrowserProjectPackageService(IJSRuntime jsRuntime) : IProjectPackageService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    private readonly IJSRuntime _jsRuntime = jsRuntime;

    public async ValueTask ExportAsync(EditorProject project, CancellationToken cancellationToken = default)
    {
        using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var exportProject = CloneForExport(project);
            var projectEntry = archive.CreateEntry("project.json", CompressionLevel.Fastest);
            await using (var projectStream = projectEntry.Open())
            {
                await JsonSerializer.SerializeAsync(projectStream, exportProject, SerializerOptions, cancellationToken);
            }

            foreach (var asset in project.Assets)
            {
                if (string.IsNullOrWhiteSpace(asset.StorageKey) || string.IsNullOrWhiteSpace(asset.EmbeddedDataUrl))
                {
                    continue;
                }

                var assetBytes = DecodeDataUrl(asset.EmbeddedDataUrl, out _);
                var assetEntry = archive.CreateEntry(asset.StorageKey.Replace('\\', '/'), CompressionLevel.NoCompression);
                await using var assetStream = assetEntry.Open();
                await assetStream.WriteAsync(assetBytes, cancellationToken);
            }
        }

        var fileName = $"{Slugify(project.Name)}.zip";
        await _jsRuntime.InvokeVoidAsync("webVN.downloadBytes", cancellationToken, fileName, archiveStream.ToArray(), "application/zip");
    }

    public async Task<EditorProject> ImportAsync(Stream packageStream, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await packageStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: false);
        var projectEntry = archive.GetEntry("project.json")
            ?? throw new InvalidOperationException("Project package is missing project.json.");

        EditorProject project;
        await using (var projectStream = projectEntry.Open())
        {
            project = await JsonSerializer.DeserializeAsync<EditorProject>(projectStream, SerializerOptions, cancellationToken)
                ?? throw new InvalidOperationException("Project package contains an invalid project.json.");
        }

        foreach (var asset in project.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.StorageKey))
            {
                continue;
            }

            var entry = archive.GetEntry(asset.StorageKey.Replace('\\', '/'));
            if (entry is null)
            {
                continue;
            }

            await using var assetStream = entry.Open();
            using var assetMemory = new MemoryStream();
            await assetStream.CopyToAsync(assetMemory, cancellationToken);
            var mimeType = InferMimeType(asset.SourceFileName ?? entry.FullName);
            asset.EmbeddedDataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(assetMemory.ToArray())}";
        }

        project.LastModifiedUtc = DateTimeOffset.UtcNow;
        return project;
    }

    private static EditorProject CloneForExport(EditorProject project)
    {
        var json = JsonSerializer.Serialize(project, SerializerOptions);
        var clone = JsonSerializer.Deserialize<EditorProject>(json, SerializerOptions) ?? new EditorProject();
        foreach (var asset in clone.Assets)
        {
            asset.EmbeddedDataUrl = null;
            asset.PublicUrl = null;
        }

        return clone;
    }

    private static byte[] DecodeDataUrl(string dataUrl, out string mimeType)
    {
        var parts = dataUrl.Split(',', 2);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException("Asset data URL is invalid.");
        }

        var header = parts[0];
        mimeType = header.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? header[5..].Split(';', 2)[0]
            : "application/octet-stream";
        return Convert.FromBase64String(parts[1]);
    }

    private static string InferMimeType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "project-package";
        }

        var chars = value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }
}
