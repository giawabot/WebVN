using WebVN.Editor.Models;

namespace WebVN.Editor.Services;

public sealed class EditorState
{
    private readonly IBrowserProjectStorage _projectStorage;
    private readonly ICsvScriptImportService _csvScriptImportService;
    private readonly HashSet<string> _dirtyAssetIds = [];
    private IReadOnlyList<ScriptRow> _dialogueRows = [];
    private SceneSnapshot? _currentScene;
    private DialogueChoiceSet? _currentChoiceSet;
    private int _currentSceneRowNumber = -1;
    private int _currentChoiceRowNumber = -1;

    public EditorState(IBrowserProjectStorage projectStorage, ICsvScriptImportService csvScriptImportService)
    {
        _projectStorage = projectStorage;
        _csvScriptImportService = csvScriptImportService;
        Project = SampleProjectFactory.Create();
        RefreshDerivedState();
        SelectedRowNumber = GetDefaultSelectedRowNumber();
    }

    public EditorProject Project { get; private set; }

    public int SelectedRowNumber { get; private set; }

    public ScriptRow? SelectedRow => Project.Script.Rows.FirstOrDefault(row => row.RowNumber == SelectedRowNumber);

    public IReadOnlyList<ScriptRow> DialogueRows => _dialogueRows;

    public DialogueChoiceSet? CurrentChoiceSet
    {
        get
        {
            if (_currentChoiceSet is null || _currentChoiceRowNumber != SelectedRowNumber)
            {
                _currentChoiceSet = BuildChoiceSet(SelectedRowNumber);
                _currentChoiceRowNumber = SelectedRowNumber;
            }

            return _currentChoiceSet;
        }
    }

    public ChoicePopupAlignment CurrentChoiceAlignment => CurrentChoiceSet?.Alignment ?? ChoicePopupAlignment.Center;

    public SceneSnapshot CurrentScene
    {
        get
        {
            if (_currentScene is null || _currentSceneRowNumber != SelectedRowNumber)
            {
                _currentScene = BuildSceneSnapshot(SelectedRowNumber);
                _currentSceneRowNumber = SelectedRowNumber;
            }

            return _currentScene;
        }
    }

    public async Task InitializeAsync()
    {
        var storedProject = await _projectStorage.LoadAsync();
        if (storedProject is null)
        {
            return;
        }

        Project = storedProject;
        NormalizeLegacyDefaults();
        RefreshDerivedState();
        SelectedRowNumber = GetDefaultSelectedRowNumber();
        _dirtyAssetIds.Clear();
    }

    public void CreateNewProject()
    {
        Project = SampleProjectFactory.Create();
        RefreshDerivedState();
        SelectedRowNumber = GetDefaultSelectedRowNumber();
        _dirtyAssetIds.Clear();
        Touch();
    }

    public void SelectRow(int rowNumber)
    {
        SelectedRowNumber = rowNumber;
    }

    public void SelectNextRow()
    {
        var nextRow = DialogueRows.FirstOrDefault(row => row.RowNumber > SelectedRowNumber);
        if (nextRow is not null)
        {
            SelectedRowNumber = nextRow.RowNumber;
        }
    }

    public void SelectPreviousRow()
    {
        var previousRow = DialogueRows.LastOrDefault(row => row.RowNumber < SelectedRowNumber);
        if (previousRow is not null)
        {
            SelectedRowNumber = previousRow.RowNumber;
        }
    }

    public void UpdateProjectName(string? name)
    {
        var trimmed = name?.Trim();
        Project.Name = string.IsNullOrWhiteSpace(trimmed) ? "Untitled Project" : trimmed;
        Touch();
    }

    public void UpdateBackground(string? backgroundAssetId)
    {
        var edit = GetOrCreateEdit(SelectedRowNumber);
        edit.BackgroundAssetId = string.IsNullOrWhiteSpace(backgroundAssetId) ? null : backgroundAssetId;
        Touch();
    }

    public void UpdateAudio(string? musicCueName, bool stopMusic, IEnumerable<string> oneShotSfxCueNames)
    {
        var edit = GetOrCreateEdit(SelectedRowNumber);
        edit.StopMusic = stopMusic;
        edit.MusicCueName = stopMusic || string.IsNullOrWhiteSpace(musicCueName)
            ? null
            : musicCueName.Trim();
        edit.OneShotSfxCueNames = oneShotSfxCueNames
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Touch();
    }

    public AssetRecord AddBackgroundAsset(string name, string sourceFileName, string contentType, byte[] bytes)
    {
        var originalFileName = string.IsNullOrWhiteSpace(sourceFileName) ? name : sourceFileName;
        var extension = NormalizeExtension(Path.GetExtension(originalFileName));
        var baseName = string.IsNullOrWhiteSpace(name)
            ? Path.GetFileNameWithoutExtension(originalFileName)
            : StripMatchingExtension(name.Trim(), extension);
        var assetId = BuildUniqueAssetId($"bg-{Slugify(StripAssetPrefix(baseName, AssetKind.Background))}");
        var storageKey = $"backgrounds/{SanitizeFileStem(baseName)}{extension}";
        var dataUrl = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";

        var asset = new AssetRecord
        {
            Id = assetId,
            Name = baseName,
            Kind = AssetKind.Background,
            StorageKey = storageKey,
            SourceFileName = originalFileName,
            EmbeddedDataUrl = dataUrl
        };

        Project.Assets.Add(asset);
        _dirtyAssetIds.Add(asset.Id);
        Touch();
        return asset;
    }

    public AssetRecord AddCharacterAsset(string name, string sourceFileName, string contentType, byte[] bytes)
    {
        var originalFileName = string.IsNullOrWhiteSpace(sourceFileName) ? name : sourceFileName;
        var extension = NormalizeExtension(Path.GetExtension(originalFileName));
        var baseName = string.IsNullOrWhiteSpace(name)
            ? Path.GetFileNameWithoutExtension(originalFileName)
            : StripMatchingExtension(name.Trim(), extension);
        var assetId = BuildUniqueAssetId($"char-{Slugify(StripAssetPrefix(baseName, AssetKind.Character))}");
        var storageKey = $"characters/{SanitizeFileStem(baseName)}{extension}";
        var dataUrl = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";

        var asset = new AssetRecord
        {
            Id = assetId,
            Name = baseName,
            Kind = AssetKind.Character,
            StorageKey = storageKey,
            SourceFileName = originalFileName,
            EmbeddedDataUrl = dataUrl
        };

        Project.Assets.Add(asset);
        _dirtyAssetIds.Add(asset.Id);
        Touch();
        return asset;
    }

    public void UpdateChoicePopupAlignment(ChoicePopupAlignment alignment)
    {
        var edit = GetOrCreateEdit(SelectedRowNumber);
        edit.ChoicePopupAlignment = alignment;
        Touch();
    }

    public void UpsertCharacter(string characterId, string displayName, double x, double y, double scale, int tintR, int tintG, int tintB, bool flipX)
    {
        var edit = GetOrCreateCharacterEdit(SelectedRowNumber);
        var existing = edit.Characters.FirstOrDefault(item => item.CharacterId == characterId);
        if (existing is null)
        {
            edit.Characters.Add(new CharacterPlacement
            {
                CharacterId = characterId,
                DisplayName = displayName,
                X = x,
                Y = y,
                Scale = scale,
                Layer = edit.Characters.Count + 1,
                TintR = tintR,
                TintG = tintG,
                TintB = tintB,
                FlipX = flipX
            });
        }
        else
        {
            existing.DisplayName = displayName;
            existing.X = x;
            existing.Y = y;
            existing.Scale = scale;
            existing.TintR = tintR;
            existing.TintG = tintG;
            existing.TintB = tintB;
            existing.FlipX = flipX;
        }

        Touch();
    }

    public void RemoveCharacter(string characterId)
    {
        var edit = GetOrCreateCharacterEdit(SelectedRowNumber);
        edit.Characters.RemoveAll(item => item.CharacterId == characterId);
        Touch();
    }

    public bool JumpToAnchor(string? anchorId)
    {
        if (string.IsNullOrWhiteSpace(anchorId))
        {
            return false;
        }

        var targetRow = Project.Script.Rows.FirstOrDefault(row => string.Equals(row.AnchorId, anchorId, StringComparison.OrdinalIgnoreCase));
        if (targetRow is null)
        {
            return false;
        }

        SelectedRowNumber = targetRow.RowNumber;
        return true;
    }

    public async Task SaveAsync()
    {
        Touch();
        foreach (var assetId in _dirtyAssetIds.ToArray())
        {
            var asset = Project.Assets.FirstOrDefault(item => item.Id == assetId);
            if (asset is null)
            {
                _dirtyAssetIds.Remove(assetId);
                continue;
            }

            await _projectStorage.SaveAssetAsync(asset);
            _dirtyAssetIds.Remove(assetId);
        }

        await _projectStorage.SaveAsync(Project);
    }

    public async Task ImportFromCsvAsync(Stream csvStream, string fileName, CancellationToken cancellationToken = default)
    {
        Project.Script = await _csvScriptImportService.ImportAsync(csvStream, fileName, cancellationToken);
        Project.ScriptSource.FileName = fileName;
        RefreshDerivedState();
        SelectedRowNumber = GetDefaultSelectedRowNumber();
        Touch();
    }

    public async Task<(int PreservedEdits, int RemovedEdits)> ImportUpdatedCsvAsync(Stream csvStream, string fileName, CancellationToken cancellationToken = default)
    {
        var updatedScript = await _csvScriptImportService.ImportAsync(csvStream, fileName, cancellationToken);
        var previousSelectedAnchorId = SelectedRow?.AnchorId;

        BackfillSceneEditAnchorIds();

        var updatedRowsByAnchorId = updatedScript.Rows
            .Where(row => row.ActionType == ScriptActionType.Dialogue && !string.IsNullOrWhiteSpace(row.AnchorId))
            .ToDictionary(row => row.AnchorId!, StringComparer.OrdinalIgnoreCase);

        var preservedEdits = 0;
        var removedEdits = 0;
        var remappedEdits = new List<SceneEdit>();
        foreach (var edit in Project.SceneEdits.OrderBy(item => item.ScriptRowNumber))
        {
            if (string.IsNullOrWhiteSpace(edit.ScriptRowAnchorId))
            {
                removedEdits++;
                continue;
            }

            if (!updatedRowsByAnchorId.TryGetValue(edit.ScriptRowAnchorId, out var updatedRow))
            {
                removedEdits++;
                continue;
            }

            edit.ScriptRowNumber = updatedRow.RowNumber;
            remappedEdits.Add(edit);
            preservedEdits++;
        }

        Project.SceneEdits = remappedEdits;
        Project.Script = updatedScript;
        Project.ScriptSource.FileName = fileName;
        RefreshDerivedState();

        if (!string.IsNullOrWhiteSpace(previousSelectedAnchorId) && updatedRowsByAnchorId.TryGetValue(previousSelectedAnchorId, out var selectedRow))
        {
            SelectedRowNumber = selectedRow.RowNumber;
        }
        else
        {
            SelectedRowNumber = GetDefaultSelectedRowNumber();
        }

        Touch();
        return (preservedEdits, removedEdits);
    }

    public void ReplaceProject(EditorProject project)
    {
        Project = project;
        RefreshDerivedState();
        SelectedRowNumber = GetDefaultSelectedRowNumber();
        _dirtyAssetIds.Clear();
        foreach (var assetId in Project.Assets.Where(asset => !string.IsNullOrWhiteSpace(asset.EmbeddedDataUrl)).Select(asset => asset.Id))
        {
            _dirtyAssetIds.Add(assetId);
        }
        Touch();
    }

    public IEnumerable<AssetRecord> BackgroundAssets => Project.Assets.Where(asset => asset.Kind == AssetKind.Background);

    public IEnumerable<AssetRecord> CharacterAssets => Project.Assets.Where(asset => asset.Kind == AssetKind.Character);

    public AssetRecord? FindAsset(string? assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return null;
        }

        return Project.Assets.FirstOrDefault(item => item.Id == assetId);
    }

    public SceneEdit? FindSceneEdit(int rowNumber)
    {
        return Project.SceneEdits.FirstOrDefault(item => item.ScriptRowNumber == rowNumber);
    }

    private SceneEdit GetOrCreateEdit(int rowNumber)
    {
        var edit = Project.SceneEdits.FirstOrDefault(item => item.ScriptRowNumber == rowNumber);
        if (edit is not null)
        {
            if (string.IsNullOrWhiteSpace(edit.ScriptRowAnchorId))
            {
                edit.ScriptRowAnchorId = GetRequiredRowAnchorId(rowNumber);
            }

            return edit;
        }

        edit = new SceneEdit
        {
            ScriptRowNumber = rowNumber,
            ScriptRowAnchorId = GetRequiredRowAnchorId(rowNumber)
        };
        Project.SceneEdits.Add(edit);
        Project.SceneEdits.Sort((left, right) => left.ScriptRowNumber.CompareTo(right.ScriptRowNumber));
        return edit;
    }

    private SceneEdit GetOrCreateCharacterEdit(int rowNumber)
    {
        var edit = GetOrCreateEdit(rowNumber);
        if (edit.Characters.Count > 0)
        {
            return edit;
        }

        var inheritedCharacters = BuildSceneSnapshot(rowNumber - 1).Characters;
        if (inheritedCharacters.Count == 0)
        {
            return edit;
        }

        edit.Characters = inheritedCharacters
            .OrderBy(item => item.Layer)
            .Select(item => new CharacterPlacement
            {
                CharacterId = item.CharacterId,
                DisplayName = item.DisplayName,
                X = item.X,
                Y = item.Y,
                Scale = item.Scale,
                Layer = item.Layer,
                TintR = item.TintR,
                TintG = item.TintG,
                TintB = item.TintB,
                FlipX = item.FlipX
            })
            .ToList();
        return edit;
    }

    private SceneSnapshot BuildSceneSnapshot(int rowNumber)
    {
        var snapshot = new SceneSnapshot();
        foreach (var edit in Project.SceneEdits.Where(item => item.ScriptRowNumber <= rowNumber).OrderBy(item => item.ScriptRowNumber))
        {
            if (!string.IsNullOrWhiteSpace(edit.BackgroundAssetId))
            {
                snapshot.BackgroundAssetId = edit.BackgroundAssetId;
            }

            if (edit.StopMusic)
            {
                snapshot.MusicCueName = null;
            }
            else if (!string.IsNullOrWhiteSpace(edit.MusicCueName))
            {
                snapshot.MusicCueName = edit.MusicCueName;
            }

            snapshot.OneShotSfxCueNames = edit.ScriptRowNumber == rowNumber
                ? [.. edit.OneShotSfxCueNames]
                : [];

            snapshot.Characters = edit.Characters
                .OrderBy(item => item.Layer)
                .Select(item => new CharacterPlacement
                {
                    CharacterId = item.CharacterId,
                    DisplayName = item.DisplayName,
                    X = item.X,
                    Y = item.Y,
                    Scale = item.Scale,
                    Layer = item.Layer,
                    TintR = item.TintR,
                    TintG = item.TintG,
                    TintB = item.TintB,
                    FlipX = item.FlipX
                })
                .ToList();
        }

        return snapshot;
    }

    private void Touch()
    {
        Project.LastModifiedUtc = DateTimeOffset.UtcNow;
        _currentScene = null;
        _currentChoiceSet = null;
        _currentSceneRowNumber = -1;
        _currentChoiceRowNumber = -1;
    }

    private void RefreshDerivedState()
    {
        _dialogueRows = Project.Script.Rows
            .Where(row => row.ActionType == ScriptActionType.Dialogue)
            .ToList();
        _currentScene = null;
        _currentChoiceSet = null;
        _currentSceneRowNumber = -1;
        _currentChoiceRowNumber = -1;
    }

    private DialogueChoiceSet? BuildChoiceSet(int dialogueRowNumber)
    {
        var selectedRowIndex = Project.Script.Rows.FindIndex(row => row.RowNumber == dialogueRowNumber);
        if (selectedRowIndex < 0)
        {
            return null;
        }

        var optionRows = new List<ScriptRow>();
        for (var index = selectedRowIndex + 1; index < Project.Script.Rows.Count; index++)
        {
            var row = Project.Script.Rows[index];
            if (row.ActionType == ScriptActionType.DialogueOption)
            {
                optionRows.Add(row);
                continue;
            }

            break;
        }

        if (optionRows.Count == 0)
        {
            return null;
        }

        var alignment = Project.SceneEdits.FirstOrDefault(edit => edit.ScriptRowNumber == dialogueRowNumber)?.ChoicePopupAlignment
            ?? ChoicePopupAlignment.Center;

        return new DialogueChoiceSet
        {
            TriggerRowNumber = dialogueRowNumber,
            Alignment = alignment,
            Options = optionRows.Select(row => new DialogueChoiceOption
            {
                RowNumber = row.RowNumber,
                Text = row.Content,
                TargetAnchorId = row.ActionEffect ?? string.Empty
            }).ToList()
        };
    }

    private int GetDefaultSelectedRowNumber()
    {
        return DialogueRows.FirstOrDefault()?.RowNumber
            ?? Project.Script.Rows.FirstOrDefault()?.RowNumber
            ?? 0;
    }

    private void NormalizeLegacyDefaults()
    {
        if (string.Equals(Project.Name, "Head Witch Office", StringComparison.Ordinal))
        {
            Project.Name = "Untitled Project";
            Project.Script.Title = "Untitled Scene";
            Project.Script.SourceName = string.Empty;
            Project.ScriptSource.FileName = string.Empty;
        }
    }

    private void BackfillSceneEditAnchorIds()
    {
        foreach (var edit in Project.SceneEdits)
        {
            if (!string.IsNullOrWhiteSpace(edit.ScriptRowAnchorId))
            {
                continue;
            }

            edit.ScriptRowAnchorId = GetRequiredRowAnchorId(edit.ScriptRowNumber);
        }
    }

    private string GetRequiredRowAnchorId(int rowNumber)
    {
        var row = Project.Script.Rows.FirstOrDefault(item => item.RowNumber == rowNumber);
        if (row is null || row.ActionType != ScriptActionType.Dialogue || string.IsNullOrWhiteSpace(row.AnchorId))
        {
            throw new InvalidOperationException($"Dialogue row {rowNumber} is missing a required Line ID#.");
        }

        return row.AnchorId;
    }

    private string BuildUniqueAssetId(string baseId)
    {
        var candidate = baseId;
        var suffix = 2;
        while (Project.Assets.Any(asset => string.Equals(asset.Id, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseId}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
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

    private string SanitizeFileStem(string value)
    {
        var chars = value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_')
            .ToArray();

        var stem = new string(chars).Trim('_');
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "asset";
        }

        var candidate = stem;
        var suffix = 2;
        while (Project.Assets.Any(asset =>
                   string.Equals(Path.GetFileNameWithoutExtension(asset.StorageKey), candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{stem}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string NormalizeExtension(string extension)
    {
        return string.IsNullOrWhiteSpace(extension) ? ".bin" : extension.ToLowerInvariant();
    }

    private static string StripMatchingExtension(string value, string extension)
    {
        return value.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? value[..^extension.Length]
            : value;
    }

    private static string StripAssetPrefix(string value, AssetKind kind)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return value;
        }

        string[] prefixes = kind switch
        {
            AssetKind.Background => ["bg-", "bg_", "background-", "background_"],
            AssetKind.Character => ["char-", "char_", "character-", "character_"],
            _ => []
        };

        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var stripped = normalized[prefix.Length..].Trim();
                return string.IsNullOrWhiteSpace(stripped) ? normalized : stripped;
            }
        }

        return normalized;
    }
}
