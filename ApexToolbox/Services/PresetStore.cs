using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ApexToolbox.Models;

namespace ApexToolbox.Services;

/// <summary>
/// 玩家设置数据源：优先读取本地缓存，其次使用内置默认数据。
/// 远端 JSON 只需发布到任意可直链访问的位置（GitHub raw、Gist、对象存储等）。
/// </summary>
public static class PresetStore
{
    /// <summary>默认数据源；也可在 %LOCALAPPDATA%\ApexToolbox\presets-url.txt 写入自定义地址覆盖。</summary>
    public const string DefaultUrl = "https://raw.githubusercontent.com/ADAIBLOG/apextool/main/data/presets.json";

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ApexToolbox");

    public static string CachePath => Path.Combine(Dir, "presets.json");
    private static string UrlOverridePath => Path.Combine(Dir, "presets-url.txt");

    public static string SourceUrl
    {
        get
        {
            try
            {
                if (File.Exists(UrlOverridePath))
                {
                    var custom = File.ReadAllText(UrlOverridePath).Trim();
                    if (custom.Length > 0) return custom;
                }
            }
            catch
            {
                // 读取失败时退回默认地址
            }
            return DefaultUrl;
        }
    }

    /// <summary>读取本地缓存；没有缓存时返回内置数据。</summary>
    public static (IReadOnlyList<GamePreset> Presets, DateTime? UpdatedAt) Load()
    {
        try
        {
            if (File.Exists(CachePath))
            {
                var json = File.ReadAllText(CachePath, Encoding.UTF8);
                var presets = Parse(json);
                if (presets.Count > 0) return (presets, File.GetLastWriteTime(CachePath));
            }
        }
        catch
        {
            // 缓存损坏时退回内置数据，下次更新会覆盖
        }

        return (PresetCatalog.Presets, null);
    }

    /// <summary>从远端拉取并替换本地缓存。</summary>
    public static async Task<(IReadOnlyList<GamePreset> Presets, DateTime UpdatedAt)> UpdateAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var json = await client.GetStringAsync(SourceUrl);
        var presets = Parse(json);
        if (presets.Count == 0) throw new InvalidDataException("数据源中没有有效的玩家设置。");

        Directory.CreateDirectory(Dir);
        File.WriteAllText(CachePath, json, new UTF8Encoding(false));
        return (presets, File.GetLastWriteTime(CachePath));
    }

    private sealed class PresetFile
    {
        public List<PresetDto> Presets { get; set; } = new();
    }

    private sealed class PresetDto
    {
        public string? Name { get; set; }
        public string? Tag { get; set; }
        public string? Source { get; set; }
        public string? Description { get; set; }
        public string? Highlights { get; set; }
        public List<PresetEntry>? Settings { get; set; }
        public List<PresetEntry>? Video { get; set; }
    }

    private static List<GamePreset> Parse(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<PresetFile>(json, options);
        if (file?.Presets == null) return new();

        return file.Presets
            .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new GamePreset(
                p.Name!.Trim(),
                p.Tag ?? string.Empty,
                p.Source ?? string.Empty,
                p.Description ?? string.Empty,
                p.Highlights ?? string.Empty,
                (p.Settings ?? new List<PresetEntry>()).Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Key)).ToArray(),
                (p.Video ?? new List<PresetEntry>()).Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Key)).ToArray()))
            .ToList();
    }
}
