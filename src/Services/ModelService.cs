using PlayersModel.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using Economy.Contract;

namespace PlayersModel.Services;

/// <summary>
/// 模型服务接口
/// </summary>
public interface IModelService
{
    /// <summary>
    /// 加载模型配置
    /// </summary>
    void LoadModels();

    /// <summary>
    /// 获取所有可用模型
    /// </summary>
    List<PlayerModelConfig> GetAllModels();

    /// <summary>
    /// 根据 ModelId 获取模型配置
    /// </summary>
    PlayerModelConfig? GetModelById(string modelId);

    /// <summary>
    /// 获取玩家可见的模型列表
    /// </summary>
    List<PlayerModelConfig> GetAvailableModelsForPlayer(IPlayer player, string? team = null);

    /// <summary>
    /// 检查玩家是否有权限使用指定模型
    /// </summary>
    bool CanPlayerUseModel(IPlayer player, string modelId);

    /// <summary>
    /// 应用模型给玩家
    /// </summary>
    Task<bool> ApplyModelToPlayerAsync(IPlayer player, string modelId);

    /// <summary>
    /// 购买模型
    /// </summary>
    Task<(bool success, string message)> PurchaseModelAsync(IPlayer player, string modelId);
}

/// <summary>
/// 模型服务实现
/// </summary>
public class ModelService : IModelService
{
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<PluginConfig> _config;
    private readonly IOptionsMonitor<ModelConfigRoot> _modelConfig;
    private readonly IDatabaseService _database;
    private readonly ILogger<ModelService> _logger;
    private IEconomyAPIv1? _economyAPI;

    public ModelService(
        ISwiftlyCore core,
        IOptionsMonitor<PluginConfig> config,
        IOptionsMonitor<ModelConfigRoot> modelConfig,
        IDatabaseService database,
        ILogger<ModelService> logger)
    {
        _core = core;
        _config = config;
        _modelConfig = modelConfig;
        _database = database;
        _logger = logger;
    }

    /// <summary>
    /// 设置经济 API (由主插件调用)
    /// </summary>
    public void SetEconomyAPI(IEconomyAPIv1? economyAPI)
    {
        _economyAPI = economyAPI;
    }

    /// <summary>
    /// 检查玩家是否有VIP权限
    /// </summary>
    private bool IsPlayerVip(ulong steamId)
    {
        var vipPerms = _config.CurrentValue.Permissions.VipPermissions;
        foreach (var perm in vipPerms)
        {
            if (_core.Permission.PlayerHasPermission(steamId, perm))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 加载模型配置
    /// </summary>
    public void LoadModels()
    {
        var models = _modelConfig.CurrentValue.Models;
        _logger.LogInformation($"已加载 {models.Count} 个模型配置");
    }

    /// <summary>
    /// 获取所有可用模型
    /// </summary>
    public List<PlayerModelConfig> GetAllModels()
    {
        return _modelConfig.CurrentValue.Models
            .Where(m => m.Enabled)
            .OrderBy(m => m.Priority)
            .ToList();
    }

    /// <summary>
    /// 根据 ModelId 获取模型配置
    /// </summary>
    public PlayerModelConfig? GetModelById(string modelId)
    {
        return _modelConfig.CurrentValue.Models
            .FirstOrDefault(m => m.ModelId == modelId && m.Enabled);
    }

    /// <summary>
    /// 获取玩家可见的模型列表
    /// </summary>
    public List<PlayerModelConfig> GetAvailableModelsForPlayer(IPlayer player, string? team = null)
    {
        var allModels = GetAllModels();
        var steamId = player.SteamID;

        return allModels.Where(model =>
        {
            // 检查阵营筛选
            if (team != null && model.Team.ToUpper() != "BOTH" && model.Team.ToUpper() != team.ToUpper())
                return false;

            // 检查 Steam ID 限制
            if (model.AllowedSteamIds.Count > 0 && !model.AllowedSteamIds.Contains(steamId))
                return false;

            // 检查权限
            if (!string.IsNullOrEmpty(model.RequiredPermission))
            {
                if (!_core.Permission.PlayerHasPermission(steamId, model.RequiredPermission))
                    return false;
            }

            // 检查 VIP
            if (model.VipOnly && !IsPlayerVip(steamId))
                return false;

            return true;
        }).ToList();
    }

    /// <summary>
    /// 检查玩家是否有权限使用指定模型
    /// </summary>
    public bool CanPlayerUseModel(IPlayer player, string modelId)
    {
        var model = GetModelById(modelId);
        if (model == null) return false;

        var steamId = player.SteamID;

        // 检查 Steam ID 限制
        if (model.AllowedSteamIds.Count > 0 && !model.AllowedSteamIds.Contains(steamId))
            return false;

        // 检查权限
        if (!string.IsNullOrEmpty(model.RequiredPermission))
        {
            if (!_core.Permission.PlayerHasPermission(steamId, model.RequiredPermission))
                return false;
        }

        // 检查 VIP
        if (model.VipOnly && !IsPlayerVip(steamId))
            return false;

        return true;
    }

    /// <summary>
    /// 应用模型给玩家
    /// </summary>
    public bool ApplyModelToPlayer(IPlayer player, string modelId)
    {
        try
        {
            var model = GetModelById(modelId);
            if (model == null)
            {
                _logger.LogWarning($"模型 {modelId} 不存在");
                return false;
            }

            // 检查权限
            if (!CanPlayerUseModel(player, modelId))
            {
                _logger.LogWarning($"玩家 {player.Controller.PlayerName} 没有权限使用模型 {modelId}");
                return false;
            }

            // 检查是否拥有此模型 (免费模型跳过)
            if (model.Price > 0)
            {
                var owns = await _database.PlayerOwnsModelAsync(player.SteamID, modelId);
                if (!owns)
                {
                    _logger.LogWarning($"玩家 {player.Controller.PlayerName} 未拥有模型 {modelId}");
                    return false;
                }
            }

            // TODO: 这里需要实际应用模型到玩家
            // 需要使用 CS2 的 Schema API 或 Native Functions
            // 使用 SetModelAsync 设置玩家模型
            if (player.Pawn?.IsValid == true)
            {
                // 设置主模型
                player.Pawn.AcceptInput("SetModel", model.ModelPath, null, null, 0);
                
                // 如果配置了手臂模型，也设置手臂
                if (!string.IsNullOrEmpty(model.ArmsPath))
                {
                    // TODO: 设置手臂模型需要不同的API
                    // 可能需要使用CCSPlayerPawn的特定属性
                }
            }

            // 保存玩家当前使用的模型
            await _database.SetPlayerCurrentModelAsync(player.SteamID, model.ModelPath, model.ArmsPath);

            _logger.LogInformation($"玩家 {player.Controller.PlayerName} 应用模型 {model.DisplayName}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"应用模型失败: {modelId}");
            return false;
        }
    }

    /// <summary>
    /// 购买模型
    /// </summary>
    public async Task<(bool success, string message)> PurchaseModelAsync(IPlayer player, string modelId)
    {
        try
        {
            // 检查购买系统是否启用
            if (!_config.CurrentValue.EnablePurchaseSystem)
            {
                return (false, "[PlayersModel] 模型购买系统未启用");
            }

            // 检查经济系统是否可用
            if (_economyAPI == null)
            {
                return (false, "[PlayersModel] 经济系统未加载");
            }

            var model = GetModelById(modelId);
            if (model == null)
            {
                return (false, "[PlayersModel] 模型不存在");
            }

            // 检查权限
            if (!CanPlayerUseModel(player, modelId))
            {
                return (false, "[PlayersModel] 你没有权限使用此模型");
            }

            // 检查是否已拥有
            var alreadyOwns = await _database.PlayerOwnsModelAsync(player.SteamID, modelId);
            if (alreadyOwns)
            {
                return (false, "[PlayersModel] 你已经拥有此模型");
            }

            // 免费模型直接添加
            if (model.Price == 0)
            {
                await _database.AddOwnedModelAsync(player.SteamID, modelId);
                return (true, $"[PlayersModel] 成功获得免费模型: {model.DisplayName}");
            }

            // 检查余额
            var walletKind = _config.CurrentValue.WalletKind;
            var balance = _economyAPI.GetPlayerBalance(player, walletKind);

            if (balance < model.Price)
            {
                return (false, $"[PlayersModel] 余额不足! 需要: {model.Price} {walletKind}, 当前: {balance} {walletKind}");
            }

            // 扣除金额
            _economyAPI.SubtractPlayerBalance(player, walletKind, model.Price);

            // 添加到拥有列表
            await _database.AddOwnedModelAsync(player.SteamID, modelId);

            var newBalance = _economyAPI.GetPlayerBalance(player, walletKind);
            _logger.LogInformation($"玩家 {player.Controller.PlayerName} 购买模型 {model.DisplayName}, 花费 {model.Price} {walletKind}");

            return (true, $"[PlayersModel] 成功购买 {model.DisplayName}! 剩余: {newBalance} {walletKind}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"购买模型失败: {modelId}");
            return (false, "[PlayersModel] 购买失败,请联系管理员");
        }
    }
}