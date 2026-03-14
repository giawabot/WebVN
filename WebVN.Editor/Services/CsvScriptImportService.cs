using System.Text;
using WebVN.Editor.Models;

namespace WebVN.Editor.Services;

public sealed class CsvScriptImportService : ICsvScriptImportService
{
    public async Task<ScriptDocument> ImportAsync(Stream csvStream, string fileName, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var rows = ParseCsv(content);
        if (rows.Count == 0)
        {
            return new ScriptDocument
            {
                Title = Path.GetFileNameWithoutExtension(fileName),
                SourceType = "csv",
                SourceName = fileName
            };
        }

        var headers = rows[0];
        var scriptRows = new List<ScriptRow>();
        var seenDialogueAnchorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < rows.Count; index++)
        {
            var values = rows[index];
            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var actionType = ParseActionType(ReadValue(headers, values, "Action Type"));
            var anchorId = ReadValue(headers, values, "Line ID#");
            if (actionType == ScriptActionType.Dialogue && string.IsNullOrWhiteSpace(anchorId))
            {
                throw new InvalidOperationException($"CSV import requires every Dialogue row to have a Line ID#. Row {index + 1} is missing one.");
            }

            if (actionType == ScriptActionType.Dialogue && !seenDialogueAnchorIds.Add(anchorId!))
            {
                throw new InvalidOperationException($"CSV import requires unique Line ID# values on Dialogue rows. Duplicate found: {anchorId}.");
            }

            scriptRows.Add(new ScriptRow
            {
                RowNumber = index + 1,
                AnchorId = anchorId,
                Content = ReadValue(headers, values, "Content") ?? string.Empty,
                ActionType = actionType,
                Character = ReadValue(headers, values, "Character"),
                Condition = ReadValue(headers, values, "Condition"),
                ActionEffect = ReadValue(headers, values, "Action Effect"),
                Notes = ReadValue(headers, values, "Notes")
            });
        }

        return new ScriptDocument
        {
            Title = Path.GetFileNameWithoutExtension(fileName),
            SourceType = "csv",
            SourceName = fileName,
            Rows = scriptRows
        };
    }

    private static List<List<string>> ParseCsv(string content)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    break;
                case '\r':
                    if (i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        i++;
                    }

                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow);
                    currentRow = [];
                    break;
                case '\n':
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow);
                    currentRow = [];
                    break;
                default:
                    currentField.Append(ch);
                    break;
            }
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }

    private static string? ReadValue(IReadOnlyList<string> headers, IReadOnlyList<string> values, string columnName)
    {
        var columnIndex = headers
            .Select((header, index) => new { header, index })
            .FirstOrDefault(item => string.Equals(item.header?.Trim(), columnName, StringComparison.OrdinalIgnoreCase))
            ?.index;

        if (columnIndex is null || columnIndex.Value >= values.Count)
        {
            return null;
        }

        var value = values[columnIndex.Value]?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static ScriptActionType ParseActionType(string? actionType)
    {
        return actionType?.Trim() switch
        {
            "EventID" => ScriptActionType.EventId,
            "Dialogue" => ScriptActionType.Dialogue,
            "DialogueOption" => ScriptActionType.DialogueOption,
            "Skillcheck" => ScriptActionType.SkillCheck,
            "Jump" => ScriptActionType.Jump,
            "SetVariable" => ScriptActionType.SetVariable,
            "End" => ScriptActionType.End,
            _ => ScriptActionType.Unknown
        };
    }
}
