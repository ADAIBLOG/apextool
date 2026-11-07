using System.IO;
using System.Text;

namespace ApexToolbox.Services;

/// <summary>配置文件中解析出的一条键值。</summary>
public sealed record CfgEntry(string Key, string Value, string? Section, int LineIndex);

/// <summary>
/// 面向 Apex settings.cfg / videoconfig.txt 的轻量解析器。
/// 只就地替换目标键的值，最大程度保留原文件的缩进、注释与格式。
/// </summary>
public sealed class CfgDocument
{
    private sealed record SectionRange(string Name, int Start, int End);

    private readonly List<string> _lines;
    private readonly Dictionary<string, int> _map = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SectionRange> _sections = new();

    public string? FilePath { get; }
    public Encoding Encoding { get; }
    public string EncodingKind { get; }
    public string NewLine { get; }
    public int EntryCount => _map.Count;

    private CfgDocument(string? path, List<string> lines, Encoding encoding, string encodingKind, string newLine)
    {
        FilePath = path;
        Encoding = encoding;
        EncodingKind = encodingKind;
        NewLine = newLine;
        _lines = lines;
        Reindex();
        ParseSections();
    }

    public static CfgDocument Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var encoding = DetectEncoding(bytes, out int bomLength, out var kind);
        var text = encoding.GetString(bytes.AsSpan(bomLength));
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        return new CfgDocument(path, lines, encoding, kind, newLine);
    }

    public static CfgDocument Parse(string text)
    {
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        return new CfgDocument(null, lines, new UTF8Encoding(false), "utf-8", newLine);
    }

    /// <summary>按编码标识创建编码器，用于还原分享码中的原始文件。</summary>
    public static Encoding EncodingFromKind(string kind) => kind switch
    {
        "utf-8-bom" => new UTF8Encoding(true),
        "utf-16le" => new UnicodeEncoding(false, true),
        "utf-16be" => new UnicodeEncoding(true, true),
        _ => new UTF8Encoding(false),
    };

    public bool Contains(string key) => _map.ContainsKey(key);

    public bool TryGet(string key, out string value)
    {
        value = string.Empty;
        if (!_map.TryGetValue(key, out int index)) return false;
        return TryParsePair(_lines[index], out _, out value, out _, out _);
    }

    public string? GetOrNull(string key) => TryGet(key, out var value) ? value : null;

    public bool TrySet(string key, string value)
    {
        if (!_map.TryGetValue(key, out int index)) return false;
        var line = _lines[index];
        if (!TryParsePair(line, out _, out _, out int start, out int end)) return false;
        _lines[index] = line[..start] + value + line[end..];
        return true;
    }

    /// <summary>存在则改值，不存在则添加到指定段落（或文件末尾）。</summary>
    public bool AddOrSet(string key, string value, string? section = null)
    {
        if (_map.ContainsKey(key)) return TrySet(key, value);

        int insertAt = -1;
        string indent = "\t";

        if (!string.IsNullOrEmpty(section))
        {
            var range = _sections.FirstOrDefault(s => s.Name.Equals(section, StringComparison.OrdinalIgnoreCase));
            if (range != null)
            {
                int lastKeyLine = LastKeyLine(range.Start, range.End);
                if (lastKeyLine >= 0)
                {
                    insertAt = lastKeyLine + 1;
                    indent = LeadingWhitespace(_lines[lastKeyLine]);
                    if (indent.Length == 0) indent = "\t";
                }
                else
                {
                    insertAt = Math.Min(range.Start + 1, _lines.Count);
                }
            }
        }

        if (insertAt < 0)
        {
            int lastKeyLine = LastKeyLine(0, _lines.Count - 1);
            insertAt = lastKeyLine >= 0 ? lastKeyLine + 1 : _lines.Count;
            if (lastKeyLine >= 0)
            {
                indent = LeadingWhitespace(_lines[lastKeyLine]);
                if (indent.Length == 0) indent = "\t";
            }
        }

        _lines.Insert(insertAt, $"{indent}\"{key}\"\t\t\"{value}\"");
        Reindex();
        ParseSections();
        return true;
    }

    public IEnumerable<CfgEntry> Entries()
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            if (TryParsePair(_lines[i], out var key, out var value, out _, out _))
                yield return new CfgEntry(key, value, SectionOf(i), i);
        }
    }

    public string ToText() => string.Join(NewLine, _lines);

    public void Save(string? path = null)
    {
        var target = path ?? FilePath ?? throw new InvalidOperationException("未指定保存路径");
        var dir = System.IO.Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(target, ToText(), Encoding);
    }

    private int LastKeyLine(int from, int to)
    {
        int last = -1;
        int start = Math.Max(0, from);
        int end = Math.Min(to, _lines.Count - 1);
        for (int i = start; i <= end; i++)
        {
            if (TryParsePair(_lines[i], out _, out _, out _, out _)) last = i;
        }
        return last;
    }

    private string? SectionOf(int lineIndex)
    {
        var range = _sections.FirstOrDefault(s => lineIndex > s.Start && lineIndex < s.End);
        return range?.Name;
    }

    private void Reindex()
    {
        _map.Clear();
        for (int i = 0; i < _lines.Count; i++)
        {
            if (TryParsePair(_lines[i], out var key, out _, out _, out _))
                _map[key] = i;
        }
    }

    private void ParseSections()
    {
        _sections.Clear();
        SectionRange? open = null;

        for (int i = 0; i < _lines.Count; i++)
        {
            var text = _lines[i].Trim();
            if (text.Length == 0) continue;

            if (text.StartsWith('['))
            {
                if (open != null) _sections.Add(open with { End = i - 1 });
                int close = text.IndexOf(']');
                var name = close > 1 ? text[1..close] : text.Trim('[', ']');
                open = new SectionRange(name, i, _lines.Count - 1);
                continue;
            }

            if (text == "{")
            {
                int prev = i - 1;
                while (prev >= 0 && string.IsNullOrWhiteSpace(_lines[prev])) prev--;
                if (prev >= 0)
                {
                    var header = _lines[prev].Trim();
                    if (header.Length > 1 && header[0] == '"' && header[^1] == '"')
                    {
                        if (open != null) _sections.Add(open with { End = prev - 1 });
                        open = new SectionRange(header.Trim('"'), prev, _lines.Count - 1);
                    }
                }
                continue;
            }

            if (text == "}" && open != null)
            {
                _sections.Add(open with { End = i });
                open = null;
            }
        }

        if (open != null) _sections.Add(open with { End = _lines.Count - 1 });
    }

    private static string LeadingWhitespace(string line)
    {
        int i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
        return line[..i];
    }

    private static bool TryParsePair(string line, out string key, out string value, out int valueStart, out int valueEnd)
    {
        key = string.Empty;
        value = string.Empty;
        valueStart = valueEnd = 0;

        int i = 0, n = line.Length;
        while (i < n && char.IsWhiteSpace(line[i])) i++;
        if (i >= n) return false;

        char first = line[i];
        if (first is '/' or '[' or '{' or '}') return false;

        if (first == '"')
        {
            int keyStart = i + 1;
            int keyEnd = line.IndexOf('"', keyStart);
            if (keyEnd < 0) return false;
            key = line[keyStart..keyEnd];

            i = keyEnd + 1;
            while (i < n && char.IsWhiteSpace(line[i])) i++;
            if (i >= n) return false;

            if (line[i] == '"')
            {
                int vStart = i + 1;
                int vEnd = line.IndexOf('"', vStart);
                if (vEnd < 0) vEnd = n;
                value = line[vStart..vEnd];
                valueStart = vStart;
                valueEnd = vEnd;
                return true;
            }

            int end = n;
            int comment = line.IndexOf("//", i, StringComparison.Ordinal);
            if (comment >= 0) end = comment;
            var raw = line[i..end];
            value = raw.Trim();
            if (value.Length == 0) return false;
            valueStart = i + (raw.Length - raw.TrimStart().Length);
            valueEnd = valueStart + value.Length;
            return true;
        }

        int eq = line.IndexOf('=', i);
        if (eq > i)
        {
            key = line[i..eq].Trim();
            int j = eq + 1;
            while (j < n && char.IsWhiteSpace(line[j])) j++;
            int end = n;
            int comment = line.IndexOf("//", j, StringComparison.Ordinal);
            if (comment >= 0) end = comment;
            var raw = line[j..end];
            value = raw.Trim();
            valueStart = j + (raw.Length - raw.TrimStart().Length);
            valueEnd = valueStart + value.Length;
            return key.Length > 0;
        }

        return false;
    }

    private static Encoding DetectEncoding(byte[] bytes, out int bomLength, out string kind)
    {
        bomLength = 0;
        kind = "utf-8";
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            bomLength = 3;
            kind = "utf-8-bom";
            return new UTF8Encoding(true);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            bomLength = 2;
            kind = "utf-16le";
            return new UnicodeEncoding(false, true);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            bomLength = 2;
            kind = "utf-16be";
            return new UnicodeEncoding(true, true);
        }
        return new UTF8Encoding(false);
    }
}
