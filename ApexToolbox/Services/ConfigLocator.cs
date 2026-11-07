using System.IO;
using Microsoft.Win32;

namespace ApexToolbox.Services;

public sealed record ConfigCandidate(string Folder, string Label, bool HasConfig);

/// <summary>定位 Apex 本地配置目录与游戏安装目录。</summary>
public static class ConfigLocator
{
    public const string SettingsFileName = "settings.cfg";
    public const string VideoFileName = "videoconfig.txt";

    public static IReadOnlyList<ConfigCandidate> Candidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var list = new List<ConfigCandidate>();

        Add(list, Path.Combine(home, "Saved Games", "Respawn", "Apex", "local"), "Saved Games");
        Add(list, Path.Combine(home, "Documents", "Respawn", "Apex", "local"), "Documents");
        Add(list, Path.Combine(home, "OneDrive", "Documents", "Respawn", "Apex", "local"), "OneDrive Documents");
        Add(list, Path.Combine(home, "Saved Games", "Respawn", "Apex"), "Saved Games 根目录");

        return list;
    }

    private static void Add(List<ConfigCandidate> list, string folder, string label)
    {
        if (!Directory.Exists(folder)) return;
        bool hasConfig = File.Exists(Path.Combine(folder, SettingsFileName)) ||
                         File.Exists(Path.Combine(folder, VideoFileName));
        list.Add(new ConfigCandidate(folder, label, hasConfig));
    }

    /// <summary>优先返回真正含配置文件的目录，其次返回第一个存在的目录。</summary>
    public static string? AutoDetect()
    {
        var candidates = Candidates();
        var withConfig = candidates.FirstOrDefault(c => c.HasConfig);
        return withConfig?.Folder ?? candidates.FirstOrDefault()?.Folder;
    }

    /// <summary>查找游戏安装目录下的 cfg 文件夹（autoexec.cfg 所在位置）。</summary>
    public static string? FindGameCfgFolder()
    {
        var paths = new List<string>();

        var steam = ReadSteamPath();
        if (steam != null)
        {
            paths.Add(Path.Combine(steam, "steamapps", "common", "Apex Legends", "cfg"));
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        paths.Add(Path.Combine(programFiles, "EA Games", "Apex Legends", "cfg"));
        paths.Add(Path.Combine(programFilesX86, "EA Games", "Apex Legends", "cfg"));
        paths.Add(Path.Combine(programFilesX86, "Origin Games", "Apex", "cfg"));

        return paths.FirstOrDefault(Directory.Exists);
    }

    private static string? ReadSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var steamPath = key?.GetValue("SteamPath") as string;
            return steamPath != null && Directory.Exists(steamPath) ? steamPath : null;
        }
        catch
        {
            return null;
        }
    }
}
