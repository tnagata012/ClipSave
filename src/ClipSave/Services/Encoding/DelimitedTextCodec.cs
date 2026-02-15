using System.Text;

namespace ClipSave.Services;

internal static class DelimitedTextCodec
{
    internal static bool TryParseTabSeparated(string text, out List<List<string>> rows)
    {
        return TryParseDelimited(text, '\t', out rows);
    }

    internal static string ConvertTabSeparatedToCsv(string tabText)
    {
        if (!TryParseTabSeparated(tabText, out var rows))
        {
            return ConvertTabSeparatedToCsvFallback(tabText);
        }

        var result = new StringBuilder();
        foreach (var row in rows)
        {
            var escapedFields = row.Select(EscapeCsvField);
            result.AppendLine(string.Join(",", escapedFields));
        }

        return result.ToString();
    }

    private static bool TryParseDelimited(string text, char delimiter, out List<List<string>> rows)
    {
        // Parse quoted fields explicitly so delimiters and line breaks inside quotes are preserved.
        var parsedRows = new List<List<string>>();

        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;
        var atFieldStart = true;

        void CommitField()
        {
            currentRow.Add(currentField.ToString());
            currentField.Clear();
            atFieldStart = true;
        }

        void CommitRow()
        {
            parsedRows.Add(currentRow);
            currentRow = [];
        }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (ch == '"')
            {
                if (inQuotes)
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else if (atFieldStart)
                {
                    inQuotes = true;
                    atFieldStart = false;
                }
                else
                {
                    currentField.Append(ch);
                    atFieldStart = false;
                }

                continue;
            }

            if (!inQuotes && ch == delimiter)
            {
                CommitField();
                continue;
            }

            if (!inQuotes && (ch == '\r' || ch == '\n'))
            {
                CommitField();
                CommitRow();

                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                continue;
            }

            currentField.Append(ch);
            atFieldStart = false;
        }

        if (inQuotes)
        {
            rows = [];
            return false;
        }

        CommitField();
        CommitRow();

        if (parsedRows.Count > 0 &&
            parsedRows[^1].Count == 1 &&
            parsedRows[^1][0].Length == 0)
        {
            parsedRows.RemoveAt(parsedRows.Count - 1);
        }

        rows = parsedRows;
        return rows.Count > 0;
    }

    private static string ConvertTabSeparatedToCsvFallback(string tabText)
    {
        // If quoted parsing fails, keep a simple split-based conversion to avoid dropping clipboard data.
        var lines = tabText.Split('\n');
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd('\r');
            if (string.IsNullOrEmpty(trimmedLine))
            {
                result.AppendLine();
                continue;
            }

            var fields = trimmedLine.Split('\t');
            var csvFields = fields.Select(EscapeCsvField);
            result.AppendLine(string.Join(",", csvFields));
        }

        return result.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') ||
            field.Contains('"') ||
            field.Contains('\n') ||
            field.Contains('\r'))
        {
            var escaped = field.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        return field;
    }
}
