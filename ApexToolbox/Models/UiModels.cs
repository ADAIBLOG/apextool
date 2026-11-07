namespace ApexToolbox.Models;

public sealed class ImportRow
{
    public string Key { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public bool Same { get; init; }
    public string CurrentValue { get; init; } = string.Empty;
    public string NewValue { get; init; } = string.Empty;
    public string Name { get; init; } = "其他参数";
    public string Hint { get; init; } = string.Empty;
    public string Target { get; init; } = "settings";
    public string CategoryText { get; init; } = "画面";
    public bool VideoRelated { get; init; }

    public string Value => NewValue;
    public string TargetText => Target == "videoconfig" ? "videoconfig.txt" : "settings.cfg";

    public string CurrentDisplay => !Exists ? "不存在" : CurrentValue.Length == 0 ? "（空）" : CurrentValue;
    public string ChangeText => Same ? "无变化" : Exists ? "修改" : "新增";
    public bool HasChange => !Same;
}
