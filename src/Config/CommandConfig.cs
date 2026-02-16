namespace PlayersModel.Config;

/// <summary>
/// 命令配置
/// </summary>
public class CommandConfig
{
    /// <summary>
    /// 玩家命令配置
    /// </summary>
    public PlayerCommands Player { get; set; } = new();

    /// <summary>
    /// 管理员命令配置
    /// </summary>
    public AdminCommands Admin { get; set; } = new();
}

/// <summary>
/// 玩家命令
/// </summary>
public class PlayerCommands
{
    /// <summary>
    /// 打开模型选择菜单命令
    /// </summary>
    public string[] Model { get; set; } = new[] { "model" };

    /// <summary>
    /// 购买模型命令
    /// </summary>
    public string[] BuyModel { get; set; } = new[] { "buymodel" };

    /// <summary>
    /// 查看余额命令
    /// </summary>
    public string[] Balance { get; set; } = new[] { "balance" };

    /// <summary>
    /// 我的模型命令
    /// </summary>
    public string[] MyModels { get; set; } = new[] { "mymodels" };
}

/// <summary>
/// 管理员命令
/// </summary>
public class AdminCommands
{
    /// <summary>
    /// 给予玩家积分命令
    /// </summary>
    public string[] GiveCredits { get; set; } = new[] { "pm_givecredits" };

    /// <summary>
    /// 给予玩家模型命令
    /// </summary>
    public string[] GiveModel { get; set; } = new[] { "pm_givemodel" };

    /// <summary>
    /// 为玩家设置模型命令
    /// </summary>
    public string[] SetModel { get; set; } = new[] { "pm_setmodel" };

    /// <summary>
    /// 重新加载配置命令
    /// </summary>
    public string[] ReloadConfig { get; set; } = new[] { "pm_reload" };

    /// <summary>
    /// 查看玩家拥有的模型命令
    /// </summary>
    public string[] ListPlayerModels { get; set; } = new[] { "pm_listmodels" };
}