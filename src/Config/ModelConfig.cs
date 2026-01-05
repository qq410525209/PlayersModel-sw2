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

    /// <summary>
    /// MeshGroup 组件配置列表 (可选)
    /// </summary>
    public List<MeshGroupConfig>? MeshGroups { get; set; } = null;
}

/// <summary>
/// MeshGroup 组件配置
/// </summary>
public class MeshGroupConfig
{
    /// <summary>
    /// 组件唯一ID (用于存储)
    /// </summary>
    public string ComponentId { get; set; } = "";

    /// <summary>
    /// BodyGroup 名称 (模型中的实际组件名)
    /// </summary>
    public string BodyGroupName { get; set; } = "";

    /// <summary>
    /// 组件显示名称
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// 组件描述 (如：头部、身体、武器等)
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 组件价格 (0表示免费)
    /// </summary>
    public int Price { get; set; } = 0;

    /// <summary>
    /// 可用的索引选项列表
    /// </summary>
    public List<MeshGroupOption> Options { get; set; } = new();
}

/// <summary>
/// MeshGroup 选项配置
/// </summary>
public class MeshGroupOption
{
    /// <summary>
    /// 选项ID
    /// </summary>
    public string OptionId { get; set; } = "";

    /// <summary>
    /// 选项显示名称
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// BodyGroup 索引值 (0通常表示隐藏/卸载，1+表示不同变体)
    /// </summary>
    public int Index { get; set; } = 0;

    /// <summary>
    /// 此选项的额外价格 (在组件基础价格之上)
    /// </summary>
    public int AdditionalPrice { get; set; } = 0;

    /// <summary>
    /// 是否为默认选项
    /// </summary>
    public bool IsDefault { get; set; } = false;
}