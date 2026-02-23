namespace PlayersModel.Config;

/// <summary>
/// 货币系统配置
/// </summary>
public sealed class CreditsConfig
{
    /// <summary>
    /// 钱包名称/类型
    /// </summary>
    public string WalletName { get; set; } = "credits";

    /// <summary>
    /// 起始余额
    /// </summary>
    public int StartingBalance { get; set; } = 0;

    /// <summary>
    /// 是否每个玩家只授予一次起始余额 (true=仅首次加入时给予，false=每次加入都给予)
    /// </summary>
    public bool GrantStartingBalanceOncePerPlayer { get; set; } = true;

    /// <summary>
    /// 当应用起始余额时是否通知玩家
    /// </summary>
    public bool NotifyWhenStartingBalanceApplied { get; set; } = true;

    /// <summary>
    /// 定时收入配置
    /// </summary>
    public TimedIncomeConfig TimedIncome { get; set; } = new();

    /// <summary>
    /// 货币转账配置
    /// </summary>
    public CreditTransferConfig Transfer { get; set; } = new();

    /// <summary>
    /// 管理员货币调整配置
    /// </summary>
    public AdminCreditAdjustmentsConfig AdminAdjustments { get; set; } = new();
}

/// <summary>
/// 定时收入配置
/// </summary>
public class TimedIncomeConfig
{
    /// <summary>
    /// 是否启用定时收入
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 收入间隔 (秒)
    /// </summary>
    public float IntervalSeconds { get; set; } = 300f;

    /// <summary>
    /// 每次发放的金额
    /// </summary>
    public int Amount { get; set; } = 10;

    /// <summary>
    /// 最低在线时长要求 (秒) - 玩家需要在线这么久才能获得收入
    /// </summary>
    public float MinimumOnlineTime { get; set; } = 60f;

    /// <summary>
    /// 是否只在玩家活跃时发放 (true=玩家需要移动/操作，false=挂机也能获得)
    /// </summary>
    public bool RequireActivity { get; set; } = false;

    /// <summary>
    /// 是否在发放收入时通知玩家
    /// </summary>
    public bool NotifyPlayer { get; set; } = true;

    /// <summary>
    /// VIP 玩家的收入倍率 (1.0 = 无加成)
    /// </summary>
    public float VipMultiplier { get; set; } = 1.5f;
}

/// <summary>
/// 货币转账配置
/// </summary>
public class CreditTransferConfig
{
    /// <summary>
    /// 是否启用玩家之间的货币转账
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 单次转账最小金额
    /// </summary>
    public int MinimumAmount { get; set; } = 1;

    /// <summary>
    /// 单次转账最大金额 (0 = 无限制)
    /// </summary>
    public int MaximumAmount { get; set; } = 0;

    /// <summary>
    /// 转账手续费百分比 (0-100, 0 = 无手续费)
    /// </summary>
    public float TransactionFeePercent { get; set; } = 0f;

    /// <summary>
    /// 转账冷却时间 (秒)
    /// </summary>
    public float CooldownSeconds { get; set; } = 60f;

    /// <summary>
    /// 是否需要接收方确认
    /// </summary>
    public bool RequireReceiverConfirmation { get; set; } = false;

    /// <summary>
    /// 转账命令别名
    /// </summary>
    public string[] TransferCommands { get; set; } = new[] { "transfer", "pay", "send" };

    /// <summary>
    /// 是否记录转账日志到数据库
    /// </summary>
    public bool LogTransactions { get; set; } = true;
}

/// <summary>
/// 管理员货币调整配置
/// </summary>
public class AdminCreditAdjustmentsConfig
{
    /// <summary>
    /// 是否启用管理员货币调整功能
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否记录管理员操作日志
    /// </summary>
    public bool LogAdjustments { get; set; } = true;

    /// <summary>
    /// 给予货币命令别名
    /// </summary>
    public string[] GiveCommands { get; set; } = new[] { "pm_givecredits", "pm_addcredits" };

    /// <summary>
    /// 扣除货币命令别名
    /// </summary>
    public string[] TakeCommands { get; set; } = new[] { "pm_takecredits", "pm_removecredits" };

    /// <summary>
    /// 设置货币命令别名
    /// </summary>
    public string[] SetCommands { get; set; } = new[] { "pm_setcredits" };

    /// <summary>
    /// 重置所有玩家货币命令别名
    /// </summary>
    public string[] ResetAllCommands { get; set; } = new[] { "pm_resetallcredits" };

    /// <summary>
    /// 单次操作最大金额限制 (0 = 无限制) - 防止误操作
    /// </summary>
    public int MaxSingleOperationAmount { get; set; } = 100000;

    /// <summary>
    /// 是否在执行操作后向目标玩家发送通知
    /// </summary>
    public bool NotifyTargetPlayer { get; set; } = true;

    /// <summary>
    /// 是否在执行操作后向全服广播 (适合大额发放活动)
    /// </summary>
    public bool BroadcastLargeAdjustments { get; set; } = false;

    /// <summary>
    /// 广播的金额阈值 (当调整金额大于等于此值时广播)
    /// </summary>
    public int BroadcastThreshold { get; set; } = 10000;
}