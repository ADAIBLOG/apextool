using System.IO;
using System.Text.Json;

namespace ApexToolbox.Services;

public sealed class BackupEntry
{
    public string Name { get; init; } = string.Empty;
    public string Folder { get; init; } = string.Empty;
    public DateTime Created { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Files { get; init; } = string.Empty;
    public long SizeBytes { get; init; }

    public string DisplayTime => Created.ToString("yyyy-MM-dd HH:mm:ss");
    public string DisplaySize => SizeBytes < 1024
        ? $"{SizeBytes} B"
        : SizeBytes < 1024 * 1024
            ? $"{SizeBytes / 1024.0:0.#} KB"
            : $"{SizeBytes / 1024.0 / 1024.0:0.#} MB";
}

/// <summary>配置备份、还原与文件只读保护。</summary>
public static class BackupService
{
    private const string MetaFile = "meta.json";

    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ApexToolbox", "backups");

    public static BackupEntry Create(string sourceFolder, string reason)
    {
        Directory.CreateDirectory(Root);

        var created = DateTime.Now;
        var folder = Path.Combine(Root, $"{created:yyyyMMdd-HHmmss}-{Sanitize(reason)}");
        Directory.CreateDirectory(folder);

        var copied = new List<string>();
        long size = 0;

        foreach (var file in new[] { ConfigLocator.SettingsFileName, ConfigLocator.VideoFileName })
        {
            var source = Path.Combine(sourceFolder, file);
            if (!File.Exists(source)) continue;
            var target = Path.Combine(folder, file);
            File.Copy(source, target, overwrite: true);
            copied.Add(file);
            size += new FileInfo(target).Length;
        }

        File.WriteAllText(Path.Combine(folder, MetaFile),
            JsonSerializer.Serialize(new BackupMeta { Reason = reason, Created = created.ToString("O") }));

        return new BackupEntry
        {
            Name = Path.GetFileName(folder),
            Folder = folder,
            Created = created,
            Reason = reason,
            Files = string.Join(", ", copied),
            SizeBytes = size,
        };
    }

    public static List<BackupEntry> List()
    {
        if (!Directory.Exists(Root)) return new List<BackupEntry>();

        return Directory.EnumerateDirectories(Root)
            .Select(ReadEntry)
            .OrderByDescending(e => e.Created)
            .ToList();
    }

    private static BackupEntry ReadEntry(string folder)
    {
        var meta = new BackupMeta();
        var metaPath = Path.Combine(folder, MetaFile);
        if (File.Exists(metaPath))
        {
            try { meta = JsonSerializer.Deserialize<BackupMeta>(File.ReadAllText(metaPath)) ?? new BackupMeta(); }
            catch { /* 忽略损坏的元数据 */ }
        }

        var created = DateTime.TryParse(meta.Created, out var t) ? t : Directory.GetCreationTime(folder);
        var copied = new List<string>();
        long size = 0;

        foreach (var name in new[] { ConfigLocator.SettingsFileName, ConfigLocator.VideoFileName })
        {
            var file = Path.Combine(folder, name);
            if (!File.Exists(file)) continue;
            copied.Add(name);
            size += new FileInfo(file).Length;
        }

        return new BackupEntry
        {
            Name = Path.GetFileName(folder),
            Folder = folder,
            Created = created,
            Reason = meta.Reason ?? string.Empty,
            Files = string.Join(", ", copied),
            SizeBytes = size,
        };
    }

    public static void Restore(BackupEntry entry, string targetFolder)
    {
        foreach (var name in new[] { ConfigLocator.SettingsFileName, ConfigLocator.VideoFileName })
        {
            var source = Path.Combine(entry.Folder, name);
            if (!File.Exists(source)) continue;
            var target = Path.Combine(targetFolder, name);
            RemoveReadOnly(target);
            File.Copy(source, target, overwrite: true);
        }
    }

    public static void Delete(BackupEntry entry)
    {
        if (Directory.Exists(entry.Folder)) Directory.Delete(entry.Folder, recursive: true);
    }

    public static bool IsReadOnly(string path) =>
        File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;

    public static void SetReadOnly(string path, bool readOnly)
    {
        if (!File.Exists(path)) return;
        var attributes = File.GetAttributes(path);
        attributes = readOnly
            ? attributes | FileAttributes.ReadOnly
            : attributes & ~FileAttributes.ReadOnly;
        File.SetAttributes(path, attributes);
    }

    public static void RemoveReadOnly(string path) => SetReadOnly(path, false);

    private static string Sanitize(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = string.Join("_", text.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length > 20 ? cleaned[..20] : cleaned;
    }

    private sealed class BackupMeta
    {
        public string? Reason { get; set; }
        public string? Created { get; set; }
    }
}
