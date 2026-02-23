using System.Collections.Concurrent;
using Economy.Contract;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlayersModel.Config;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared;
using Dapper;
using System.Data;

namespace PlayersModel.Services;

/// <summary>
/// 货币服务接口
/// </summary>
public interface ICreditsService
{
    /// <summary>
    /// 处理玩家首次加入，发放初始积分
    /// </summary>
    Task HandlePlayerJoinAsync(IPlayer player);

    /// <summary>
    /// 启动定时收入系统
    /// </summary>
    void StartTimedIncomeSystem();

    /// <summary>
    /// 停止定时收入系统
    /// </summary>
    void StopTimedIncomeSystem();

    /// <summary>
    /// 检查玩家是否可以获得定时收入
    /// </summary>
    bool CanReceiveTimedIncome(ulong steamId);

    /// <summary>
    /// 记录玩家活动（用于活跃检测）
    /// </summary>
    void RecordPlayerActivity(ulong steamId);
}

/// <summary>
/// 货币服务实现
/// </summary>
public class CreditsService : ICreditsService
{
    private readonly ILogger<CreditsService> _logger;
    private readonly IOptionsMonitor<PluginConfig> _config;
    private readonly IDatabaseService _databaseService;
    private readonly ISwiftlyCore _core;
    private readonly ITranslationService _translationService;
    private IEconomyAPIv1? _economyAPI;

    // 定时收入相关
    private Timer? _timedIncomeTimer;
    private readonly ConcurrentDictionary<ulong, DateTime> _playerJoinTimes = new();
    private readonly ConcurrentDictionary<ulong, DateTime> _playerLastActivity = new();

    public CreditsService(
        ILogger<CreditsService> logger,
        IOptionsMonitor<PluginConfig> config,
        IDatabaseService databaseService,
        ISwiftlyCore core,
        ITranslationService translationService)
    {
        _logger = logger;
        _config = config;
        _databaseService = databaseService;
        _core = core;
        _translationService = translationService;
    }

    /// <summary>
    /// 设置 Economy API
    /// </summary>
    public void SetEconomyAPI(IEconomyAPIv1 economyAPI)
    {
        _economyAPI = economyAPI;
        _logger.LogInformation("Economy API set for CreditsService");
    }

    /// <summary>
    /// 处理玩家首次加入，发放初始积分
    /// </summary>
    public async Task HandlePlayerJoinAsync(IPlayer player)
    {
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        var creditsConfig = _config.CurrentValue.Credits;
        if (creditsConfig.StartingBalance <= 0)
            return;

        if (_economyAPI == null)
        {
            _logger.LogWarning("Economy API not available, cannot grant starting balance");
            return;
        }

        var steamId = player.SteamID;
        var walletName = creditsConfig.WalletName;

        // 记录玩家加入时间（用于定时收入）
        _playerJoinTimes.TryAdd(steamId, DateTime.UtcNow);
        _playerLastActivity.TryAdd(steamId, DateTime.UtcNow);

        try
        {
            // 确保钱包类型存在
            _economyAPI.EnsureWalletKind(walletName);

            // 检查是否已经发放过初始积分
            if (creditsConfig.GrantStartingBalanceOncePerPlayer)
            {
                var hasReceived = await HasReceivedStartingBalanceAsync(steamId);
                if (hasReceived)
                {
                    _logger.LogDebug($"Player {player.Controller.PlayerName} ({steamId}) has already received starting balance");
                    return;
                }
            }

            // 获取当前余额
            var currentBalance = _economyAPI.GetPlayerBalance(steamId, walletName);

            // 如果是首次发放，或者每次都发放
            if (!creditsConfig.GrantStartingBalanceOncePerPlayer || currentBalance == 0)
            {
                // 发放初始积分
                _economyAPI.AddPlayerBalance(steamId, walletName, creditsConfig.StartingBalance);

                // 记录已发放
                if (creditsConfig.GrantStartingBalanceOncePerPlayer)
                {
                    await MarkStartingBalanceReceivedAsync(steamId);
                }

                _logger.LogInformation($"Granted starting balance of {creditsConfig.StartingBalance} {walletName} to {player.Controller.PlayerName} ({steamId})");

                // 通知玩家
                if (creditsConfig.NotifyWhenStartingBalanceApplied)
                {
                    var message = _translationService.Get(
                        "credits.starting_balance_granted",
                        player,
                        creditsConfig.StartingBalance,
                        walletName);
                    player.SendChat(message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to grant starting balance to player {player.Controller.PlayerName} ({steamId})");
        }
    }

    /// <summary>
    /// 启动定时收入系统
    /// </summary>
    public void StartTimedIncomeSystem()
    {
        var timedIncomeConfig = _config.CurrentValue.Credits.TimedIncome;
        if (!timedIncomeConfig.Enabled || timedIncomeConfig.IntervalSeconds <= 0)
            return;

        if (_economyAPI == null)
        {
            _logger.LogWarning("Economy API not available, cannot start timed income system");
            return;
        }

        // 停止现有的计时器
        StopTimedIncomeSystem();

        var interval = TimeSpan.FromSeconds(timedIncomeConfig.IntervalSeconds);
        _timedIncomeTimer = new Timer(ProcessTimedIncome, null, interval, interval);

        _logger.LogInformation($"Timed income system started with interval of {timedIncomeConfig.IntervalSeconds} seconds");
    }

    /// <summary>
    /// 停止定时收入系统
    /// </summary>
    public void StopTimedIncomeSystem()
    {
        _timedIncomeTimer?.Dispose();
        _timedIncomeTimer = null;
        _logger.LogInformation("Timed income system stopped");
    }

    /// <summary>
    /// 处理定时收入
    /// </summary>
    private void ProcessTimedIncome(object? state)
    {
        if (_economyAPI == null)
            return;

        var timedIncomeConfig = _config.CurrentValue.Credits.TimedIncome;
        var walletName = _config.CurrentValue.Credits.WalletName;
        var vipPermissions = _config.CurrentValue.Permissions.VipPermissions;

        try
        {
            var playerManager = _core.PlayerManager;
            if (playerManager == null)
                return;

            var now = DateTime.UtcNow;

            // 遍历所有在线玩家
            for (int i = 0; i < 64; i++)
            {
                var player = playerManager.GetPlayer(i);
                if (player == null || !player.IsValid || player.IsFakeClient)
                    continue;

                var steamId = player.SteamID;

                // 检查是否满足最低在线时长
                if (_playerJoinTimes.TryGetValue(steamId, out var joinTime))
                {
                    var onlineTime = (now - joinTime).TotalSeconds;
                    if (onlineTime < timedIncomeConfig.MinimumOnlineTime)
                        continue;
                }
                else
                {
                    continue;
                }

                // 如果需要活跃检测
                if (timedIncomeConfig.RequireActivity)
                {
                    if (_playerLastActivity.TryGetValue(steamId, out var lastActivity))
                    {
                        // 如果超过收入间隔时间没有活动，跳过
                        var inactiveTime = (now - lastActivity).TotalSeconds;
                        if (inactiveTime > timedIncomeConfig.IntervalSeconds)
                            continue;
                    }
                    else
                    {
                        continue;
                    }
                }

                // 计算收入金额
                var amount = timedIncomeConfig.Amount;

                // 检查VIP倍率
                if (timedIncomeConfig.VipMultiplier > 1.0f)
                {
                    var hasVip = vipPermissions.Any(perm => _core.Permission.PlayerHasPermission(steamId, perm));
                    if (hasVip)
                    {
                        amount = (int)(amount * timedIncomeConfig.VipMultiplier);
                    }
                }

                // 发放收入
                _economyAPI.AddPlayerBalance(steamId, walletName, amount);

                _logger.LogDebug($"Granted timed income of {amount} {walletName} to {player.Controller.PlayerName} ({steamId})");

                // 通知玩家
                if (timedIncomeConfig.NotifyPlayer)
                {
                    var message = _translationService.Get(
                        "credits.timed_income_received",
                        player,
                        amount,
                        walletName);
                    player.SendChat(message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing timed income");
        }
    }

    /// <summary>
    /// 检查玩家是否可以获得定时收入
    /// </summary>
    public bool CanReceiveTimedIncome(ulong steamId)
    {
        var timedIncomeConfig = _config.CurrentValue.Credits.TimedIncome;
        if (!timedIncomeConfig.Enabled)
            return false;

        if (!_playerJoinTimes.TryGetValue(steamId, out var joinTime))
            return false;

        var onlineTime = (DateTime.UtcNow - joinTime).TotalSeconds;
        return onlineTime >= timedIncomeConfig.MinimumOnlineTime;
    }

    /// <summary>
    /// 记录玩家活动
    /// </summary>
    public void RecordPlayerActivity(ulong steamId)
    {
        _playerLastActivity.AddOrUpdate(steamId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
    }

    /// <summary>
    /// 检查玩家是否已经领取过初始积分
    /// </summary>
    private async Task<bool> HasReceivedStartingBalanceAsync(ulong steamId)
    {
        try
        {
            using var connection = GetConnection();
            var query = "SELECT COUNT(*) FROM playersmodel_starting_balance_grants WHERE steam_id = @SteamId";
            var count = await connection.ExecuteScalarAsync<int>(query, new { SteamId = (long)steamId });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to check if player {steamId} has received starting balance");
            return false;
        }
    }

    /// <summary>
    /// 标记玩家已领取初始积分
    /// </summary>
    private async Task MarkStartingBalanceReceivedAsync(ulong steamId)
    {
        try
        {
            using var connection = GetConnection();
            var query = @"
                INSERT INTO playersmodel_starting_balance_grants (steam_id, granted_at)
                VALUES (@SteamId, @GrantedAt)
                ON DUPLICATE KEY UPDATE granted_at = @GrantedAt";

            await connection.ExecuteAsync(query, new
            {
                SteamId = (long)steamId,
                GrantedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to mark starting balance as received for player {steamId}");
        }
    }

    /// <summary>
    /// 清理玩家数据（玩家离开时调用）
    /// </summary>
    public void CleanupPlayerData(ulong steamId)
    {
        _playerJoinTimes.TryRemove(steamId, out _);
        _playerLastActivity.TryRemove(steamId, out _);
    }

    /// <summary>
    /// 获取数据库连接
    /// </summary>
    private IDbConnection GetConnection()
    {
        var connectionKey = _config.CurrentValue.Database.ConnectionKey;
        return _core.Database.GetConnection(connectionKey);
    }
}