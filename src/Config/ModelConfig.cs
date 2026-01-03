namespace PlayersModel.Config;

/// <summary>
/// 模型配置根对象
/// </summary>
public class ModelConfigRoot
{
    /// <summary>
    /// 所有模型列表
    /// </summary>
    public List<PlayerModelConfig> Models { get; set; } = new();
}

/// <summary>
/// 玩家模型配置
/// </summary>
public class PlayerModelConfig
{
    /// <summary>
    /// 模型 ID (存储在数据库中的唯一标识)
    /// </summary>
    public string ModelId { get; set; } = "";

    /// <summary>
    /// 模型显示名称
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// 模型描述
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 模型文件路径 (vmdl)
    /// </summary>
    public string ModelPath { get; set; } = "";

    /// <summary>
    /// 手臂模型路径 (vmdl)
    /// </summary>
    public string ArmsPath { get; set; } = "";

    /// <summary>
    /// 模型分类 (CT/T/Both)
    /// </summary>
    public string Team { get; set; } = "Both";

    /// <summary>
    /// 模型价格 (credits, 0 表示免费)
    /// </summary>
    public int Price { get; set; } = 0;

    /// <summary>
    /// 是否为 VIP 专属
    /// </summary>
    public bool VipOnly { get; set; } = false;

    /// <summary>
    /// 所需权限 (为空表示无需权限)
    /// </summary>
    public string RequiredPermission { get; set; } = "";

    /// <summary>
    /// 限制的 Steam64 ID 列表 (为空表示无限制)
    /// </summary>
    public List<ulong> AllowedSteamIds { get; set; } = new();

    /// <summary>
    /// 是否启用此模型
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 排序优先级 (数字越小越靠前)
    /// </summary>
    public int Priority { get; set; } = 0;
}