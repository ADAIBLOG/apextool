namespace ApexToolbox.Models;

public sealed record PresetEntry(string Key, string Value);

public sealed record GamePreset(
    string Name,
    string Tag,
    string Source,
    string Description,
    string Highlights,
    IReadOnlyList<PresetEntry> Settings,
    IReadOnlyList<PresetEntry> Video)
{
    public int EntryCount => Settings.Count + Video.Count;
}

/// <summary>玩家提供的配置模板；只写入当前配置中已存在的键。</summary>
public static class PresetCatalog
{
    public static IReadOnlyList<GamePreset> Presets { get; } = new[]
    {
        new GamePreset(
            "无名（noname）", "玩家", "玩家提供的 videoconfig",
            "玩家 noname 的完整视频配置：关闭阴影、SSAO、体积光与动态分辨率，模型细节 0.6，兼顾清晰度与帧数。",
            "阴影 / SSAO / 体积光全关，LOD 0.6，全屏 1920×1440",
            Array.Empty<PresetEntry>(),
            new[]
            {
                new PresetEntry("setting.cl_gib_allow", "0"),
                new PresetEntry("setting.cl_particle_fallback_base", "0"),
                new PresetEntry("setting.cl_particle_fallback_multiplier", "0"),
                new PresetEntry("setting.cl_ragdoll_maxcount", "0"),
                new PresetEntry("setting.cl_ragdoll_self_collision", "0"),
                new PresetEntry("setting.mat_forceaniso", "0"),
                new PresetEntry("setting.mat_mip_linear", "0"),
                new PresetEntry("setting.stream_memory", "160000"),
                new PresetEntry("setting.mat_picmip", "0"),
                new PresetEntry("setting.particle_cpu_level", "0"),
                new PresetEntry("setting.r_createmodeldecals", "0"),
                new PresetEntry("setting.r_decals", "0"),
                new PresetEntry("setting.r_lod_switch_scale", "0.600000"),
                new PresetEntry("setting.shadow_enable", "0"),
                new PresetEntry("setting.shadow_depth_dimen_min", "0"),
                new PresetEntry("setting.shadow_depth_upres_factor_max", "0"),
                new PresetEntry("setting.shadow_maxdynamic", "0"),
                new PresetEntry("setting.ssao_enabled", "0"),
                new PresetEntry("setting.ssao_downsample", "3"),
                new PresetEntry("setting.dvs_enable", "0"),
                new PresetEntry("setting.dvs_gpuframetime_min", "15000"),
                new PresetEntry("setting.dvs_gpuframetime_max", "16500"),
                new PresetEntry("setting.sound_volume", "0.139286"),
                new PresetEntry("setting.last_display_width", "1920"),
                new PresetEntry("setting.last_display_height", "1440"),
                new PresetEntry("setting.nowindowborder", "1"),
                new PresetEntry("setting.fullscreen", "1"),
                new PresetEntry("setting.defaultres", "1920"),
                new PresetEntry("setting.defaultresheight", "1440"),
                new PresetEntry("setting.volumetric_lighting", "0"),
                new PresetEntry("setting.volumetric_fog", "0"),
                new PresetEntry("setting.mat_vsync_mode", "0"),
                new PresetEntry("setting.mat_backbuffer_count", "1"),
                new PresetEntry("setting.mat_antialias_mode", "0"),
                new PresetEntry("setting.csm_enabled", "0"),
                new PresetEntry("setting.csm_coverage", "0"),
                new PresetEntry("setting.csm_cascade_res", "0"),
                new PresetEntry("setting.fadeDistScale", "1.000000"),
                new PresetEntry("setting.new_shadow_settings", "0"),
                new PresetEntry("setting.dynamic_stream_budget", "0"),
                new PresetEntry("setting.gamma", "0.817857"),
                new PresetEntry("setting.configversion", "9"),
                new PresetEntry("setting.map_detail_level", "1"),
            }),
    };
}
