using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ApexToolbox.Models;
using ApexToolbox.Services;

namespace ApexToolbox;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> BasicVideoKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "setting.fullscreen",
        "setting.nowindowborder",
        "setting.defaultres",
        "setting.defaultresheight",
        "setting.mat_vsync",
        "setting.mat_triplebuffered",
        "setting.mat_monitorgamma",
        "setting.cl_fovScale",
    };

    private readonly ObservableCollection<ImportRow> _importRows = new();
    private readonly ObservableCollection<CfgLine> _detailCfgLines = new();
    private readonly ObservableCollection<BackupEntry> _backups = new();

    private string? _folder;
    private CfgDocument? _settingsDoc;
    private CfgDocument? _videoDoc;
    private SharePayload? _pendingPayload;
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();

        DiffItemsHost.ItemsSource = _importRows;
        DetailCfgHost.ItemsSource = _detailCfgLines;
        BackupItemsHost.ItemsSource = _backups;
        LaunchItemsHost.ItemsSource = SettingCatalog.LaunchGroups;
        var cached = PresetStore.Load();
        BindPresets(cached.Presets, cached.UpdatedAt);

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        NavPreset.IsChecked = true;
        DetectFolder();
        RefreshBackups();
        Status("就绪。");
    }

    // ---------- 路径与加载 ----------

    private void DetectFolder()
    {
        var detected = ConfigLocator.AutoDetect();
        if (detected == null)
        {
            _folder = null;
            PathStatusText.Text = "未找到 Apex 配置目录，请手动选择。";
            _settingsDoc = null;
            _videoDoc = null;
            return;
        }

        _folder = detected;
        PathStatusText.Text = _folder;
        ReloadDocs();
    }

    private void ReloadDocs()
    {
        if (_folder == null) return;

        _settingsDoc = SafeLoad(Path.Combine(_folder, ConfigLocator.SettingsFileName));
        _videoDoc = SafeLoad(Path.Combine(_folder, ConfigLocator.VideoFileName));

        UpdateReadOnlySwitch();
    }

    private static CfgDocument? SafeLoad(string path)
    {
        try
        {
            return File.Exists(path) ? CfgDocument.Load(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private void UpdateReadOnlySwitch()
    {
        if (_folder == null)
        {
            ReadOnlySwitch.IsChecked = false;
            return;
        }

        var files = new[]
        {
            Path.Combine(_folder, ConfigLocator.SettingsFileName),
            Path.Combine(_folder, ConfigLocator.VideoFileName),
        };
        ReadOnlySwitch.IsChecked = files.Any(f => File.Exists(f) && BackupService.IsReadOnly(f));
    }

    // ---------- 导航 ----------

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (sender is not RadioButton button || button.Tag is not string tag) return;
        ShowPage(tag);
    }

    private void ShowPage(string tag)
    {
        PagePreset.Visibility = tag == "preset" ? Visibility.Visible : Visibility.Collapsed;
        PagePresetDetail.Visibility = tag == "preset-detail" ? Visibility.Visible : Visibility.Collapsed;
        PageImport.Visibility = tag == "import" ? Visibility.Visible : Visibility.Collapsed;
        PageBackup.Visibility = tag == "backup" ? Visibility.Visible : Visibility.Collapsed;
        PageLaunch.Visibility = tag == "launch" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择 Apex 配置目录（Respawn\\Apex\\local）" };
        if (dialog.ShowDialog(this) != true) return;

        _folder = dialog.FolderName;
        PathStatusText.Text = _folder;
        ReloadDocs();
        Status($"已切换到：{_folder}");
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_folder == null || !Directory.Exists(_folder))
        {
            Warn("尚未选择有效的配置目录。");
            return;
        }
        OpenInExplorer(_folder);
    }

    private static void OpenInExplorer(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true,
        });
    }

    // ---------- 导入导出 ----------

    private void ChooseImportFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择他人的配置文件",
            Filter = "Apex 配置 (*.cfg;*.txt)|*.cfg;*.txt|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var text = CfgDocument.Load(dialog.FileName).ToText();
            ImportTextBox.Text = text;
            ParseImport(text, Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex)
        {
            Warn($"读取失败：{ex.Message}");
        }
    }

    private void ParsePasted_Click(object sender, RoutedEventArgs e) => ParseImport(ImportTextBox.Text, "粘贴内容");

    private void ClearImport_Click(object sender, RoutedEventArgs e)
    {
        ImportTextBox.Text = string.Empty;
        _importRows.Clear();
        _pendingPayload = null;
        ImportVideoOnly.IsEnabled = true;
        ImportAll.IsEnabled = true;
        ImportSummaryText.Text = "尚未导入内容。";
    }

    private void ParseImport(string text, string source)
    {
        _importRows.Clear();
        _pendingPayload = null;
        ImportVideoOnly.IsEnabled = true;
        ImportAll.IsEnabled = true;

        if (string.IsNullOrWhiteSpace(text))
        {
            Warn("没有可解析的内容。");
            return;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("APX1-", StringComparison.OrdinalIgnoreCase))
        {
            var payload = ShareCodec.Decode(trimmed);
            if (payload == null)
            {
                Warn("分享码无法解析，请确认内容完整。");
                return;
            }

            AddRawPreview(payload.SettingsText, "settings");
            AddRawPreview(payload.VideoText, "videoconfig");

            if (payload.SettingsText.Length > 0 || payload.VideoText.Length > 0)
            {
                _pendingPayload = payload;
                ImportVideoOnly.IsEnabled = false;
                ImportAll.IsEnabled = false;
                ImportSummaryText.Text =
                    $"分享码「{payload.Name}」包含完整配置文件：settings.cfg {_importRows.Count(r => r.Target == "settings")} 项、"
                    + $"videoconfig.txt {_importRows.Count(r => r.Target == "videoconfig")} 项。应用时将按原格式整文件写入。";
                Status($"已解析完整分享码：{payload.Name}", Tone.Ok);
                return;
            }

            AddImportRows(payload.Settings, "settings");
            AddImportRows(payload.Video, "videoconfig");
            ImportSummaryText.Text = BuildImportSummary($"分享码「{payload.Name}」");
            Status($"已解析分享码：{payload.Name}", Tone.Ok);
            return;
        }

        var doc = CfgDocument.Parse(text);
        int total = 0, related = 0;

        foreach (var entry in doc.Entries())
        {
            var target = GuessTarget(entry.Key, entry.Section);
            var isRelated = IsVideoRelated(entry.Key, entry.Section);

            _importRows.Add(BuildImportRow(entry.Key, entry.Value, target, isRelated));

            total++;
            if (isRelated) related++;
        }

        ImportSummaryText.Text = BuildImportSummary($"{source}") + $"（画面相关 {related} 项）";
        Status($"已解析 {total} 项，画面相关 {related} 项。", Tone.Ok);
    }

    private ImportRow BuildImportRow(string key, string value, string target, bool isRelated)
    {
        var current = GetCurrentValue(target, key);
        var (name, hint) = KeyGlossary.Describe(key, value);
        return new ImportRow
        {
            Key = key,
            Name = name,
            Hint = hint,
            Exists = current != null,
            Same = current == value,
            CurrentValue = current ?? string.Empty,
            NewValue = value,
            Target = target,
            CategoryText = isRelated ? "画面" : "其他",
            VideoRelated = isRelated,
        };
    }

    private string BuildImportSummary(string prefix)
    {
        var modified = _importRows.Count(r => r.Exists && !r.Same);
        var added = _importRows.Count(r => !r.Exists);
        var same = _importRows.Count(r => r.Same);
        return $"{prefix}：共 {_importRows.Count} 项（修改 {modified} / 新增 {added} / 无变化 {same}）。";
    }

    private void AddImportRows(Dictionary<string, string> items, string target)
    {
        foreach (var pair in items)
        {
            var isRelated = target == "videoconfig" || BasicVideoKeys.Contains(pair.Key);
            _importRows.Add(BuildImportRow(pair.Key, pair.Value, target, isRelated));
        }
    }

    /// <summary>按文件原文解析预览项，用于完整分享码。</summary>
    private void AddRawPreview(string text, string target)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var doc = CfgDocument.Parse(text);
        foreach (var entry in doc.Entries())
        {
            var isRelated = target == "videoconfig" || BasicVideoKeys.Contains(entry.Key);
            _importRows.Add(BuildImportRow(entry.Key, entry.Value, target, isRelated));
        }
    }

    private static string GuessTarget(string key, string? section)
    {
        if (!string.IsNullOrEmpty(section) && section.Equals("VideoConfig", StringComparison.OrdinalIgnoreCase))
            return "videoconfig";
        if (key.StartsWith("setting.r_", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("particle", StringComparison.OrdinalIgnoreCase))
            return "videoconfig";
        return "settings";
    }

    private static bool IsVideoRelated(string key, string? section)
    {
        if (!string.IsNullOrEmpty(section) && section.Equals("VideoConfig", StringComparison.OrdinalIgnoreCase))
            return true;
        if (BasicVideoKeys.Contains(key)) return true;
        return key.StartsWith("setting.r_", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("particle", StringComparison.OrdinalIgnoreCase);
    }

    private string? GetCurrentValue(string target, string key)
    {
        var doc = target == "videoconfig" ? _videoDoc : _settingsDoc;
        return doc?.GetOrNull(key);
    }

    private void ApplyImport_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureLoaded()) return;

        if (_pendingPayload != null)
        {
            ApplyFullPayload(_pendingPayload);
            return;
        }

        if (_importRows.Count == 0)
        {
            Warn("没有可导入的内容。请先选择文件或解析粘贴内容。");
            return;
        }

        var rows = ImportVideoOnly.IsChecked == true
            ? _importRows.Where(r => r.VideoRelated).ToList()
            : _importRows.ToList();

        if (rows.Count == 0)
        {
            Warn("没有画面相关的项可导入。");
            return;
        }

        BackupBeforeWrite("导入配置");
        UnprotectFiles();

        int applied = 0, skipped = 0;

        foreach (var row in rows)
        {
            var doc = row.Target == "videoconfig" ? _videoDoc : _settingsDoc;
            if (doc == null)
            {
                skipped++;
                continue;
            }

            if (doc.Contains(row.Key))
            {
                if (doc.TrySet(row.Key, row.Value)) applied++;
                else skipped++;
            }
            else if (ImportAll.IsChecked == true)
            {
                doc.AddOrSet(row.Key, row.Value, row.Target == "videoconfig" ? "VideoConfig" : null);
                applied++;
            }
            else
            {
                skipped++;
            }
        }

        SaveDocs();
        ReloadDocs();
        RefreshBackups();

        Status($"已导入 {applied} 项，跳过 {skipped} 项。重启游戏后生效。",
            applied > 0 ? Tone.Ok : Tone.Warn);
    }

    /// <summary>完整分享码：按原文与原始编码整文件写入，保留注释、顺序和格式。</summary>
    private void ApplyFullPayload(SharePayload payload)
    {
        if (_folder == null) return;

        BackupBeforeWrite($"导入完整配置：{payload.Name}");
        UnprotectFiles();

        int written = 0;
        if (payload.SettingsText.Length > 0) written += WriteRawFile(ConfigLocator.SettingsFileName, payload.SettingsText, payload.SettingsEncoding);
        if (payload.VideoText.Length > 0) written += WriteRawFile(ConfigLocator.VideoFileName, payload.VideoText, payload.VideoEncoding);

        ReloadDocs();
        RefreshBackups();

        _pendingPayload = null;
        ImportVideoOnly.IsEnabled = true;
        ImportAll.IsEnabled = true;

        Status($"已按分享码「{payload.Name}」完整写入 {written} 个配置文件（保留原格式），重启游戏后生效。",
            written > 0 ? Tone.Ok : Tone.Warn);
    }

    private int WriteRawFile(string fileName, string text, string encodingKind)
    {
        var path = Path.Combine(_folder!, fileName);
        if (!File.Exists(path)) return 0;
        File.WriteAllText(path, text, CfgDocument.EncodingFromKind(encodingKind));
        return 1;
    }

    private void ExportFile_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureLoaded()) return;

        var dialog = new OpenFolderDialog { Title = "选择保存完整配置的文件夹" };
        if (dialog.ShowDialog(this) != true) return;

        var targets = new List<(CfgDocument Doc, string Path)>();
        if (_settingsDoc != null) targets.Add((_settingsDoc, Path.Combine(dialog.FolderName, ConfigLocator.SettingsFileName)));
        if (_videoDoc != null) targets.Add((_videoDoc, Path.Combine(dialog.FolderName, ConfigLocator.VideoFileName)));

        if (targets.Count == 0)
        {
            Warn("当前没有可导出的配置文件。");
            return;
        }

        var existing = targets.Where(t => File.Exists(t.Path)).Select(t => Path.GetFileName(t.Path)).ToList();
        if (existing.Count > 0 &&
            MessageBox.Show(this, $"目标文件夹已存在 {string.Join("、", existing)}，是否覆盖？",
                "确认覆盖", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            foreach (var (doc, path) in targets) doc.Save(path);
            Status($"已完整保存 {targets.Count} 个配置文件到：{dialog.FolderName}", Tone.Ok);
        }
        catch (Exception ex)
        {
            Warn($"导出失败：{ex.Message}");
        }
    }

    private void GenerateCode_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureLoaded()) return;

        var payload = BuildPayload("我的 Apex 画面配置");
        ShareCodeBox.Text = ShareCodec.Encode(payload);
        Status("已生成完整配置分享码（含 settings.cfg 与 videoconfig.txt 原文），复制发送即可。", Tone.Ok);
    }

    private SharePayload BuildPayload(string name)
    {
        var payload = new SharePayload
        {
            Name = name,
            Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        };

        if (_settingsDoc != null)
        {
            payload.SettingsText = _settingsDoc.ToText();
            payload.SettingsEncoding = _settingsDoc.EncodingKind;
            foreach (var entry in _settingsDoc.Entries()) payload.Settings[entry.Key] = entry.Value;
        }

        if (_videoDoc != null)
        {
            payload.VideoText = _videoDoc.ToText();
            payload.VideoEncoding = _videoDoc.EncodingKind;
            foreach (var entry in _videoDoc.Entries()) payload.Video[entry.Key] = entry.Value;
        }

        return payload;
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ShareCodeBox.Text))
        {
            Warn("还没有生成分享码。");
            return;
        }
        TryCopy(ShareCodeBox.Text, "分享码已复制到剪贴板。");
    }

    // ---------- 备份还原 ----------

    private void RefreshBackups_Click(object sender, RoutedEventArgs e) => RefreshBackups();

    private void RefreshBackups()
    {
        try
        {
            _backups.Clear();
            foreach (var entry in BackupService.List()) _backups.Add(entry);
        }
        catch
        {
            // 备份目录不可读时忽略
        }
    }

    private void OpenBackupFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(BackupService.Root);
        OpenInExplorer(BackupService.Root);
    }

    private void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not BackupEntry entry) return;
        if (_folder == null)
        {
            Warn("尚未选择配置目录。");
            return;
        }

        var confirm = MessageBox.Show(this,
            $"将 {entry.DisplayTime} 的备份还原到：\n{_folder}\n\n当前配置会先自动备份。继续吗？",
            "确认还原", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        BackupBeforeWrite("还原前");
        BackupService.Restore(entry, _folder);
        ReloadDocs();

        Status($"已还原到 {entry.DisplayTime} 的备份。", Tone.Ok);
    }

    private void DeleteBackup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not BackupEntry entry) return;

        var confirm = MessageBox.Show(this,
            $"删除备份 {entry.DisplayTime}？此操作不可撤销。",
            "删除备份", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        BackupService.Delete(entry);
        RefreshBackups();
        Status("已删除备份。");
    }

    private void ReadOnly_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || _folder == null) return;

        var readOnly = ReadOnlySwitch.IsChecked == true;
        foreach (var name in new[] { ConfigLocator.SettingsFileName, ConfigLocator.VideoFileName })
        {
            var path = Path.Combine(_folder, name);
            if (File.Exists(path)) BackupService.SetReadOnly(path, readOnly);
        }

        Status(readOnly
            ? "已设为只读，游戏启动时不会覆盖配置。"
            : "已取消只读，可以继续修改配置。", readOnly ? Tone.Ok : Tone.Normal);
    }

    // ---------- 启动参数 ----------

    private void LaunchItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not LaunchOption option) return;
        TryCopy(option.Arg, $"已复制 {option.Arg}，粘贴到 Steam / EA App 启动选项即可。");
    }

    // ---------- 玩家设置 ----------

    private void ApplyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not GamePreset preset) return;
        if (!EnsureLoaded()) return;

        BackupBeforeWrite($"应用预设：{preset.Name}");
        UnprotectFiles();

        int applied = 0, added = 0, skipped = 0;
        var skippedKeys = new List<string>();

        void Apply(IEnumerable<PresetEntry> entries, CfgDocument? doc, bool addMissing)
        {
            if (doc == null)
            {
                skipped += entries.Count();
                skippedKeys.AddRange(entries.Select(x => x.Key));
                return;
            }
            foreach (var entry in entries)
            {
                if (doc.Contains(entry.Key) && doc.TrySet(entry.Key, entry.Value))
                {
                    applied++;
                }
                else if (addMissing && doc.AddOrSet(entry.Key, entry.Value, "VideoConfig"))
                {
                    added++;
                }
                else
                {
                    skipped++;
                    skippedKeys.Add(entry.Key);
                }
            }
        }

        Apply(preset.Settings, _settingsDoc, false);
        Apply(preset.Video, _videoDoc, true);

        SaveDocs();
        ReloadDocs();
        RefreshBackups();
        var protectedCount = ProtectFiles();

        var message = $"已应用「{preset.Name}」：修改 {applied} 项";
        if (added > 0) message += $"，新增 {added} 项";
        if (skipped > 0)
        {
            message += $"，跳过 {skipped} 项（{string.Join("、", skippedKeys.Take(5))}"
                     + (skippedKeys.Count > 5 ? " 等" : "") + "）";
        }
        message += protectedCount > 0
            ? $"。已为 {protectedCount} 个配置文件设置只读，重启游戏后生效。"
            : "。重启游戏后生效。";
        Status(message, applied + added > 0 ? Tone.Ok : Tone.Warn);
    }

    private void BindPresets(IReadOnlyList<GamePreset> presets, DateTime? updatedAt)
    {
        PresetItemsHost.ItemsSource = presets;
        var stamp = updatedAt is null
            ? "内置默认数据（尚未更新）"
            : $"本地缓存 · {updatedAt.Value:yyyy-MM-dd HH:mm}";
        PresetSourceText.Text = $"{stamp} · 数据源：{PresetStore.SourceUrl}";
    }

    private async void UpdatePresets_Click(object sender, RoutedEventArgs e)
    {
        PresetUpdateButton.IsEnabled = false;
        Status("正在从数据源更新玩家设置…");

        try
        {
            var (presets, updatedAt) = await PresetStore.UpdateAsync();
            BindPresets(presets, updatedAt);
            Status($"已更新 {presets.Count} 位玩家的设置（{updatedAt:yyyy-MM-dd HH:mm}），并保存到本地缓存。", Tone.Ok);
        }
        catch (Exception ex)
        {
            Warn($"更新失败：{ex.Message}\n仍继续使用本地缓存。");
            Status("更新失败，仍使用本地缓存。", Tone.Warn);
        }
        finally
        {
            PresetUpdateButton.IsEnabled = true;
        }
    }

    private void PresetDetail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not GamePreset preset) return;

        _detailCfgLines.Clear();
        foreach (var line in BuildPresetCfgLines(preset)) _detailCfgLines.Add(line);

        DetailTitleText.Text = preset.Name;
        DetailSubText.Text = $"{preset.Source} · 共 {preset.EntryCount} 项设置 · 与当前配置对比，重启游戏后生效";
        ShowPage("preset-detail");
    }

    private void PresetBack_Click(object sender, RoutedEventArgs e) => ShowPage("preset");

    /// <summary>把玩家模板还原成可直接对照的 cfg 文本，每行右侧附上仅用于阅读的说明。</summary>
    private static List<CfgLine> BuildPresetCfgLines(GamePreset preset)
    {
        var lines = new List<CfgLine>();

        if (preset.Settings.Count > 0)
        {
            lines.Add(new CfgLine { Text = "// settings.cfg" });
            foreach (var entry in preset.Settings)
            {
                lines.Add(new CfgLine
                {
                    Text = $"\"{entry.Key}\"\t\t\"{entry.Value}\"",
                    Comment = KeyGlossary.Annotate(entry.Key, entry.Value),
                });
            }
            lines.Add(new CfgLine());
        }

        lines.Add(new CfgLine { Text = "\"VideoConfig\"" });
        lines.Add(new CfgLine { Text = "{" });
        foreach (var entry in preset.Video)
        {
            lines.Add(new CfgLine
            {
                Text = $"\t\"{entry.Key}\"\t\t\"{entry.Value}\"",
                Comment = KeyGlossary.Annotate(entry.Key, entry.Value),
            });
        }
        lines.Add(new CfgLine { Text = "}" });
        return lines;
    }

    /// <summary>把现有配置文件设为只读，并同步界面上的只读开关。</summary>
    private int ProtectFiles()
    {
        if (_folder == null) return 0;

        var count = 0;
        foreach (var name in new[] { ConfigLocator.SettingsFileName, ConfigLocator.VideoFileName })
        {
            var path = Path.Combine(_folder, name);
            if (!File.Exists(path)) continue;
            BackupService.SetReadOnly(path, true);
            count++;
        }

        _loading = true;
        ReadOnlySwitch.IsChecked = count > 0;
        _loading = false;
        return count;
    }

    // ---------- 应用入口 ----------

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (NavPreset.IsChecked == true) Status("玩家设置请点击卡片上的「一键使用」。");
            else if (NavImport.IsChecked == true) ApplyImport_Click(sender, e);
            else if (NavBackup.IsChecked == true) Status("备份页无需应用，使用「还原」按钮即可。");
            else if (NavLaunch.IsChecked == true) Status("启动参数点击列表项即可复制，无需应用。");
        }
        catch (Exception ex)
        {
            Error($"操作失败：{ex.Message}");
        }
    }

    // ---------- 通用工具 ----------

    private bool EnsureLoaded()
    {
        if (_folder == null || (_settingsDoc == null && _videoDoc == null))
        {
            Warn("未找到配置文件。请先运行一次游戏，或点击右上角「选择目录」。");
            return false;
        }
        return true;
    }

    private void BackupBeforeWrite(string reason)
    {
        if (_folder == null) return;
        try
        {
            BackupService.Create(_folder, reason);
            RefreshBackups();
        }
        catch
        {
            // 备份失败不阻塞写入，但会在状态栏提示
            Status("备份失败，但仍会继续写入。", Tone.Warn);
        }
    }

    private void UnprotectFiles()
    {
        if (_folder == null) return;
        foreach (var name in new[] { ConfigLocator.SettingsFileName, ConfigLocator.VideoFileName })
        {
            var path = Path.Combine(_folder, name);
            if (File.Exists(path)) BackupService.RemoveReadOnly(path);
        }
        _loading = true;
        ReadOnlySwitch.IsChecked = false;
        _loading = false;
    }

    private void SaveDocs()
    {
        _settingsDoc?.Save();
        _videoDoc?.Save();
    }

    private void TryCopy(string text, string successMessage)
    {
        try
        {
            Clipboard.SetText(text);
            Status(successMessage, Tone.Ok);
        }
        catch (Exception ex)
        {
            Warn($"复制失败：{ex.Message}");
        }
    }

    private enum Tone { Normal, Ok, Warn, Error }

    private void Warn(string message) => Status(message, Tone.Warn);

    private void Error(string message) => Status(message, Tone.Error);

    private void Status(string message, Tone tone = Tone.Normal)
    {
        if (StatusText == null) return;

        StatusText.Text = message;
        StatusText.Foreground = tone switch
        {
            Tone.Ok => (Brush)FindResource("GoodBrush"),
            Tone.Warn => (Brush)FindResource("WarnBrush"),
            Tone.Error => (Brush)FindResource("AccentBrush"),
            _ => (Brush)FindResource("MutedBrush"),
        };
    }
}
