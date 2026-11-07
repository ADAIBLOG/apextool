namespace ApexToolbox.Models;

/// <summary>把 Apex 配置键翻译成玩家能看懂的中文含义。未收录的键按关键字推断，仍未知则显示为「其他参数」。</summary>
public static class KeyGlossary
{
    private sealed record Entry(string Name, string Hint, string? On = null, string? Off = null);

    private static readonly Dictionary<string, Entry> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["setting.fullscreen"] = new("显示模式", "1 为全屏独占，0 为窗口 / 无边框", "全屏", "窗口"),
        ["setting.nowindowborder"] = new("无边框窗口", "为 1 时全屏窗口化，方便切屏与推流", "无边框", "普通窗口"),
        ["setting.defaultres"] = new("水平分辨率", "画面宽度（像素）"),
        ["setting.defaultresheight"] = new("垂直分辨率", "画面高度（像素）"),
        ["setting.mat_vsync"] = new("垂直同步", "开启可消除画面撕裂，但会增加输入延迟", "开启", "关闭"),
        ["setting.mat_triplebuffered"] = new("三重缓冲", "开启会增加显存占用与延迟，通常建议关闭", "开启", "关闭"),
        ["setting.mat_monitorgamma"] = new("亮度（gamma）", "数值越高画面越亮，常用 2.2"),
        ["setting.cl_fovScale"] = new("视野（FOV）缩放", "1.0 约等于 70°，数值越大视野越广"),
        ["setting.cl_gib_allow"] = new("死亡特效（肢体碎块）", "关闭后击杀特效更简单，可减少特效开销", "显示", "关闭"),
        ["setting.r_lod_switch_scale"] = new("模型细节 LOD", "数值越低远景模型越简化，帧数越高"),
        ["setting.mat_depth_feather_scale"] = new("深度羽化", "影响远景边缘柔和度，越低渲染开销越小"),
        ["setting.cl_particle_fallback_base"] = new("粒子回退基准", "越低爆炸、烟雾等粒子效果越简单"),
        ["setting.cl_particle_fallback_multiplier"] = new("粒子回退倍率", "与粒子回退基准配合，越低粒子越少"),
        ["setting.mat_tonemapping_occlusion_use_stencil"] = new("色调映射遮挡", "部分显卡关闭后可减少渲染开销", "开启", "关闭"),
        ["setting.mat_forceaniso"] = new("各向异性过滤", "纹理过滤等级，常见 0 / 2 / 4 / 8 / 16"),
        ["setting.stream_memory"] = new("纹理流内存", "纹理显存上限（字节），过高易造成卡顿"),
        ["setting.mat_picmip"] = new("纹理细节等级", "数值越高纹理越模糊，但更省显存"),
        ["setting.r_shadow_map_size"] = new("阴影贴图分辨率", "数值越低阴影越粗糙，性能越好"),
        ["setting.cl_ragdoll_maxcount"] = new("尸体数量上限", "数值越低尸体保留越少"),
        ["setting.mat_antialias_mode"] = new("抗锯齿模式", "不同模式对应不同抗锯齿算法"),
        ["setting.r_dynamic_lod"] = new("动态 LOD", "开启后按帧率自动调整模型细节", "开启", "关闭"),
        ["setting.cl_quality"] = new("特效质量", "控制爆炸、烟雾等特效的复杂度"),
        ["setting.mat_backbuffer_count"] = new("后台缓冲数量", "影响帧生成与输入延迟"),
        ["setting.mat_vsync_quality"] = new("垂直同步质量", "垂直同步的附加选项"),
        ["setting.mat_mip_linear"] = new("线性 Mip 过滤", "影响远处纹理的过渡平滑度", "开启", "关闭"),
        ["setting.particle_cpu_level"] = new("粒子 CPU 等级", "越低粒子计算越省 CPU"),
        ["setting.r_createmodeldecals"] = new("模型贴花", "控制弹孔、血迹等贴花生成", "开启", "关闭"),
        ["setting.r_decals"] = new("贴花数量上限", "越低场景中的贴花越少"),
        ["setting.cl_ragdoll_self_collision"] = new("尸体自碰撞", "关闭可减少尸体物理计算", "开启", "关闭"),
        ["setting.shadow_enable"] = new("阴影总开关", "关闭后完全不渲染动态阴影", "开启", "关闭"),
        ["setting.shadow_depth_dimen_min"] = new("阴影最小深度精度", "0 表示最低精度"),
        ["setting.shadow_depth_upres_factor_max"] = new("阴影深度升采样上限", "影响阴影边缘精度"),
        ["setting.shadow_maxdynamic"] = new("动态阴影数量上限", "0 表示不渲染动态阴影"),
        ["setting.ssao_enabled"] = new("环境光遮蔽 SSAO", "关闭可明显减少渲染开销", "开启", "关闭"),
        ["setting.ssao_downsample"] = new("SSAO 降采样", "数值越高越省性能，但遮蔽越粗糙"),
        ["setting.dvs_enable"] = new("动态分辨率", "开启后按帧率自动降低分辨率", "开启", "关闭"),
        ["setting.dvs_gpuframetime_min"] = new("动态分辨率帧时间下限", "单位微秒，低于该值会提升分辨率"),
        ["setting.dvs_gpuframetime_max"] = new("动态分辨率帧时间上限", "单位微秒，高于该值会降低分辨率"),
        ["setting.sound_volume"] = new("主音量", "0 - 1 之间的音量系数"),
        ["setting.last_display_width"] = new("上次窗口宽度", "游戏记录的窗口宽度"),
        ["setting.last_display_height"] = new("上次窗口高度", "游戏记录的窗口高度"),
        ["setting.volumetric_lighting"] = new("体积光", "关闭可减少光线散射效果的开销", "开启", "关闭"),
        ["setting.volumetric_fog"] = new("体积雾", "关闭可减少雾效计算", "开启", "关闭"),
        ["setting.mat_vsync_mode"] = new("垂直同步模式", "0 为关闭，数值对应不同的同步模式"),
        ["setting.csm_enabled"] = new("级联阴影", "控制大范围阴影的渲染方式", "开启", "关闭"),
        ["setting.csm_coverage"] = new("级联阴影覆盖范围", "数值越大阴影覆盖越广"),
        ["setting.csm_cascade_res"] = new("级联阴影分辨率", "数值越低阴影越粗糙"),
        ["setting.fadeDistScale"] = new("淡出距离缩放", "影响物体远景淡出的距离"),
        ["setting.new_shadow_settings"] = new("新版阴影设置", "切换新旧阴影渲染管线", "开启", "关闭"),
        ["setting.dynamic_stream_budget"] = new("动态纹理流预算", "控制纹理动态加载的显存预算", "开启", "关闭"),
        ["setting.gamma"] = new("画面亮度 gamma", "数值越高画面越亮，常用 2.2 附近"),
        ["setting.configversion"] = new("配置版本号", "游戏写入的配置格式版本，通常无需改动"),
        ["setting.map_detail_level"] = new("地图细节等级", "数值越低场景装饰物越少"),
    };

    public static (string Name, string Hint) Describe(string key, string value)
    {
        Entry? entry = Map.TryGetValue(key, out var found) ? found : null;

        if (entry == null)
        {
            var lowered = key.ToLowerInvariant();
            entry = lowered switch
            {
                _ when lowered.Contains("particle") => new Entry("粒子效果", "与爆炸、烟雾等粒子渲染相关"),
                _ when lowered.Contains("shadow") => new Entry("阴影质量", "控制阴影分辨率与渲染开销"),
                _ when lowered.Contains("picmip") || lowered.Contains("texture") => new Entry("纹理质量", "控制纹理清晰度与显存占用"),
                _ when lowered.Contains("fov") => new Entry("视野", "影响可见范围"),
                _ when lowered.Contains("vsync") => new Entry("垂直同步", "控制画面同步"),
                _ when lowered.Contains("mouse") => new Entry("鼠标设置", "与鼠标灵敏度或加速相关"),
                _ when lowered.Contains("fps") => new Entry("帧率设置", "与帧率上限或显示相关"),
                _ => new Entry("其他参数", "未收录说明，按原键值写入"),
            };
        }

        var hint = entry.Hint;
        if (entry.On != null && entry.Off != null)
        {
            var meaning = value is "1" or "true" ? entry.On : entry.Off;
            hint = $"{hint}（值 {value} = {meaning}）";
        }
        else if (value.Length > 0)
        {
            hint = $"{hint}（值 {value}）";
        }

        return (entry.Name, hint);
    }
}
