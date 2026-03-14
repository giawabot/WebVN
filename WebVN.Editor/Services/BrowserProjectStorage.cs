using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.JSInterop;
using WebVN.Editor.Models;

namespace WebVN.Editor.Services;

public sealed class BrowserProjectStorage(IJSRuntime jsRuntime) : IBrowserProjectStorage
{
    private const string StorageKey = "webvn.editor.project";
    private const string AssetKeyPrefix = "webvn.editor.asset:";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly IJSRuntime _jsRuntime = jsRuntime;

    public async ValueTask SaveAsync(EditorProject project, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(CloneForStorage(project), SerializerOptions);
        await _jsRuntime.InvokeVoidAsync("webVN.storage.setAsync", cancellationToken, StorageKey, payload);
    }

    public async ValueTask SaveAssetAsync(AssetRecord asset, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(asset.Id) || string.IsNullOrWhiteSpace(asset.EmbeddedDataUrl))
        {
            return;
        }

        await _jsRuntime.InvokeVoidAsync("webVN.storage.setAsync", cancellationToken, BuildAssetKey(asset.Id), asset.EmbeddedDataUrl);
    }

    public async ValueTask<EditorProject?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var payload = await _jsRuntime.InvokeAsync<string?>("webVN.storage.getAsync", cancellationToken, StorageKey);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var project = JsonSerializer.Deserialize<EditorProject>(payload, SerializerOptions);
        if (project is null)
        {
            return null;
        }

        foreach (var asset in project.Assets)
        {
            if (!string.IsNullOrWhiteSpace(asset.EmbeddedDataUrl))
            {
                await SaveAssetAsync(asset, cancellationToken);
                continue;
            }

            asset.EmbeddedDataUrl = await _jsRuntime.InvokeAsync<string?>("webVN.storage.getAsync", cancellationToken, BuildAssetKey(asset.Id));
        }

        return project;
    }

    private static string BuildAssetKey(string assetId) => $"{AssetKeyPrefix}{assetId}";

    private static EditorProject CloneForStorage(EditorProject project)
    {
        return new EditorProject
        {
            Name = project.Name,
            LastModifiedUtc = project.LastModifiedUtc,
            ScriptSource = new CsvImportSource
            {
                FileName = project.ScriptSource.FileName
            },
            Script = new ScriptDocument
            {
                Title = project.Script.Title,
                SourceType = project.Script.SourceType,
                SourceName = project.Script.SourceName,
                Rows = project.Script.Rows
                    .Select(row => new ScriptRow
                    {
                        RowNumber = row.RowNumber,
                        AnchorId = row.AnchorId,
                        Content = row.Content,
                        ActionType = row.ActionType,
                        Character = row.Character,
                        Condition = row.Condition,
                        ActionEffect = row.ActionEffect,
                        Notes = row.Notes
                    })
                    .ToList()
            },
            Assets = project.Assets
                .Select(asset => new AssetRecord
                {
                    Id = asset.Id,
                    Name = asset.Name,
                    Kind = asset.Kind,
                    StorageKey = asset.StorageKey,
                    PublicUrl = asset.PublicUrl,
                    SourceFileName = asset.SourceFileName,
                    EmbeddedDataUrl = null
                })
                .ToList(),
            SceneEdits = project.SceneEdits
                .Select(edit => new SceneEdit
                {
                    ScriptRowNumber = edit.ScriptRowNumber,
                    ScriptRowAnchorId = edit.ScriptRowAnchorId,
                    BackgroundAssetId = edit.BackgroundAssetId,
                    MusicCueName = edit.MusicCueName,
                    StopMusic = edit.StopMusic,
                    OneShotSfxCueNames = [.. edit.OneShotSfxCueNames],
                    ChoicePopupAlignment = edit.ChoicePopupAlignment,
                    Characters = edit.Characters
                        .Select(character => new CharacterPlacement
                        {
                            CharacterId = character.CharacterId,
                            DisplayName = character.DisplayName,
                            X = character.X,
                            Y = character.Y,
                            Scale = character.Scale,
                            Layer = character.Layer,
                            TintR = character.TintR,
                            TintG = character.TintG,
                            TintB = character.TintB,
                            FlipX = character.FlipX
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
