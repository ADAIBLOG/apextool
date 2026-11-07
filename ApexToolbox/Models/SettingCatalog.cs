namespace ApexToolbox.Models;

public static class SettingCatalog
{
    public static IReadOnlyList<LaunchGroup> LaunchGroups { get; } = new[]
    {
        new LaunchGroup("启动速度", new[]
        {
            new LaunchOption("-novid", "跳过开场动画", "每次启动少等几秒"),
            new LaunchOption("-preload", "预加载资源", "部分机械硬盘可减少卡顿"),
        }),
        new LaunchGroup("帧率与性能", new[]
        {
            new LaunchOption("+fps_max 0", "解锁帧率上限", "以显示器/显卡实际能力为准"),
            new LaunchOption("+fps_max 144", "限制 144 帧", "降低功耗与温度"),
            new LaunchOption("+fps_max 240", "限制 240 帧", "高刷屏常用"),
            new LaunchOption("-high", "高进程优先级", "可能提升前台稳定性"),
            new LaunchOption("-nojoy", "禁用游戏手柄", "不用手柄时减少外设检测"),
        }),
        new LaunchGroup("显示与分辨率", new[]
        {
            new LaunchOption("-fullscreen", "强制全屏启动", "相当于游戏内全屏模式"),
            new LaunchOption("-noborder", "无边框窗口", "窗口化无边框，切屏更平滑；必要时与 -windowed 一起用"),
            new LaunchOption("-windowed", "窗口模式", "以窗口方式启动，便于多任务"),
            new LaunchOption("-w 1920 -h 1080", "指定分辨率 1920×1080", "把数字改成你的显示器分辨率"),
            new LaunchOption("-refresh 144", "指定刷新率 144", "数字改成显示器实际刷新率"),
            new LaunchOption("-freq 144", "指定刷新率 144", "按显示器实际刷新率填写"),
        }),
        new LaunchGroup("渲染与进阶", new[]
        {
            new LaunchOption("-anticheat_settings=SettingsDX12.json", "使用 DirectX 12", "新显卡可尝试"),
            new LaunchOption("+cl_showfps 1", "显示帧率", "左上角显示 FPS"),
            new LaunchOption("+exec autoexec.cfg", "启动时执行 autoexec", "需游戏 cfg 目录存在该文件"),
            new LaunchOption("-dev", "开发模式", "跳过部分启动检查，可能影响 HUD"),
        }),
    };
}

public sealed record LaunchOption(string Arg, string Name, string Description);

public sealed record LaunchGroup(string Category, IReadOnlyList<LaunchOption> Options);
