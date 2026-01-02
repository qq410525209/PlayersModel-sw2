namespace PlayersModel.Config;

/// <summary>
/// PlayersModel 插件主配置
/// </summary>
public class PluginConfig
{
    /// <summary>
    /// 插件是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 调试模式
    /// </summary>
    public bool Debug { get; set; } = false;

    /// <summary>
    /// 语言设置 (留空则使用玩家在框架中设置的语言)
    /// 可选值: zh-Hans, en, 等
    /// </summary>
    public string Language { get; set; } = "";

    /// <summary>
    /// 是否启用模型购买系统
    /// </summary>
    public bool EnablePurchaseSystem { get; set; } = true;

    /// <summary>
    /// Economy 钱包类型 (用于购买模型)
    /// </summary>
    public string WalletKind { get; set; } = "credits";

    /// <summary>
    /// 默认 CT 模型 ID (数据库中的名称)
    /// </summary>
    public string DefaultCTModel { get; set; } = "default_ct";

    /// <summary>
    /// 默认 T 模型 ID (数据库中的名称)
    /// </summary>
    public string DefaultTModel { get; set; } = "default_t";

    /// <summary>
    /// 权限设置
    /// </summary>
    public PermissionSettings Permissions { get; set; } = new();

    /// <summary>
    /// 菜单设置
    /// </summary>
    public MenuSettings Menu { get; set; } = new();

    /// <summary>
    /// 数据库设置
    /// </summary>
    public DatabaseSettings Database { get; set; } = new();

    /// <summary>
    /// 命令设置
    /// </summary>
    public CommandConfig Commands { get; set; } = new();
}

/// <summary>
/// 权限设置
/// </summary>
public class PermissionSettings
{
    /// <summary>
    /// 管理员权限 (默认: root 或 admin)
    /// </summary>
    public List<string> AdminPermissions { get; set; } = new() { "root", "admin" };

    /// <summary>
    /// VIP 权限
    /// </summary>
    public List<string> VipPermissions { get; set; } = new() { "vip", "*.vip" };
}

/// <summary>
/// 菜单设置
/// </summary>
public class MenuSettings
{
    /// <summary>
    /// 是否启用菜单音效
    /// </summary>
    public bool EnableSound { get; set; } = true;

    /// <summary>
    /// 每页显示的模型数量 (1-5)
    /// </summary>
    public int ItemsPerPage { get; set; } = 5;

    /// <summary>
    /// 打开菜单时是否冻结玩家
    /// </summary>
    public bool FreezePlayer { get; set; } = false;

    /// <summary>
    /// 菜单自动关闭时间 (秒, 0 = 不自动关闭)
    /// </summary>
    public float AutoCloseDelay { get; set; } = 0f;
}

/// <summary>
/// 数据库设置
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// 数据库连接键 (使用 Economy 的数据库连接)
    /// </summary>
    public string ConnectionKey { get; set; } = "economyapi";

    /// <summary>
    /// 玩家拥有的模型表名
    /// </summary>
    public string OwnedModelsTable { get; set; } = "player_owned_models";

    /// <summary>
    /// 玩家当前使用的模型表名
    /// </summary>
    public string CurrentModelsTable { get; set; } = "player_current_models";

    /// <summary>
    /// 是否自动创建数据库表
    /// </summary>
    public bool AutoCreateTables { get; set; } = true;
}