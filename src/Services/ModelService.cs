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
    bool ApplyModelToPlayer(IPlayer player, string modelId);

    /// <summary>
    /// 购买模型
    /// </summary>
    Task<(bool success, string message)> PurchaseModelAsync(IPlayer player, string modelId);
    
    /// <summary>
    /// 设置模型Hook服务
    /// </summary>
    void SetModelHookService(IModelHookService modelHookService);
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
    private readonly ITranslationService _translation;
    private IEconomyAPIv1? _economyAPI;
    private readonly IModelCacheService _modelCache;
    private IMeshGroupService? _meshGroupService;
    private ICookiesService? _cookiesService;

    private IModelHookService? _modelHookService;

    public ModelService(
        ISwiftlyCore core,
        IOptionsMonitor<PluginConfig> config,
        IOptionsMonitor<ModelConfigRoot> modelConfig,
        IDatabaseService database,
        IModelCacheService modelCache,
        ILogger<ModelService> logger,
        ITranslationService translation)
    {
        _core = core;
        _config = config;
        _modelConfig = modelConfig;
        _database = database;
        _modelCache = modelCache;
        _logger = logger;
        _translation = translation;
    }

    /// <summary>
    /// 设置模型Hook服务
    /// </summary>
    public void SetModelHookService(IModelHookService modelHookService)
    {
        _modelHookService = modelHookService;
    }

    /// <summary>
    /// 设置经济 API (由主插件调用)
    /// </summary>
    public void SetEconomyAPI(IEconomyAPIv1? economyAPI)
    {
        _economyAPI = economyAPI;
    }

    /// <summary>
    /// 设置 MeshGroup 服务 (由主插件调用)
    /// </summary>
    public void SetMeshGroupService(IMeshGroupService meshGroupService)
    {
        _meshGroupService = meshGroupService;
    }

    /// <summary>
    /// 设置 Cookies 服务 (由主插件调用)
    /// </summary>
    public void SetCookiesService(ICookiesService cookiesService)
    {
        _cookiesService = cookiesService;
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
        _logger.LogInformation(_translation.GetConsole("system.models_loading", models.Count));
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

            if (team != null && model.Team.ToLower() != team.ToLower())
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
                _logger.LogWarning(_translation.GetConsole("modelservice.model_not_found", modelId));
                return false;
            }

            // 检查权限
            if (!CanPlayerUseModel(player, modelId))
            {
                _logger.LogWarning(_translation.GetConsole("modelservice.no_permission", player.Controller.PlayerName, modelId));
                return false;
            }

            // 检查是否拥有此模型 (免费模型跳过)
            if (model.Price > 0)
            {
                var owns = _database.PlayerOwnsModelAsync(player.SteamID, modelId).GetAwaiter().GetResult();
                if (!owns)
                {
                    _logger.LogWarning(_translation.GetConsole("modelservice.not_owned", player.Controller.PlayerName, modelId));
                    return false;
                }
            }
            
            // 保存模型到数据库（简化版 - 一个玩家只有一个模型，不分队伍）
            _database.SetPlayerCurrentModelAsync(
                player.SteamID, 
                player.Controller.PlayerName, 
                modelId, model.ModelPath, model.ArmsPath, "").GetAwaiter().GetResult();
            
            // 更新缓存
            _modelCache.UpdatePlayerCache(player.SteamID, "", model.ModelPath, model.ArmsPath);

            // 保存模型到 Cookies 以实现跨服重启持久化
            _cookiesService?.SavePlayerModel(player, modelId);

            // 立即应用模型（如果配置允许且玩家在游戏中）
            if (_config.CurrentValue.ApplyModelImmediately && player.Pawn?.IsValid == true)
            {
                _core.Scheduler.NextWorldUpdate(() =>
                {
                    if (!player.IsValid || player.Pawn?.IsValid != true)
                        return;
                    
                    try
                    {
                        player.Pawn.SetModel(model.ModelPath);
                        
                        // 应用 MeshGroup 默认配置
                        if (_meshGroupService != null && model.MeshGroups != null && model.MeshGroups.Count > 0)
                        {
                            _meshGroupService.ApplyDefaultMeshGroups(player, model).GetAwaiter().GetResult();
                        }
                        
                        _logger.LogInformation(_translation.GetConsole("modelservice.applied_now", player.Controller.PlayerName, model.DisplayName));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to apply model immediately: {model.ModelPath}");
                    }
                });
            }
            else
            {
                _logger.LogInformation(_translation.GetConsole("modelservice.saved_for_next_spawn", player.Controller.PlayerName, model.DisplayName));
            }

            _logger.LogInformation(_translation.GetConsole("modelservice.applied", player.Controller.PlayerName, model.DisplayName));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("modelservice.apply_failed", modelId));
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
            _logger.LogInformation(_translation.GetConsole("modelservice.purchase_called", player.Controller.PlayerName, modelId));
            _logger.LogInformation(_translation.GetConsole("modelservice.economy_status", _economyAPI != null ? "Connected" : "null"));
            
            // 检查购买系统是否启用
            if (!_config.CurrentValue.EnablePurchaseSystem)
            {
                _logger.LogWarning(_translation.GetConsole("modelservice.purchase_disabled"));
                return (false, _translation.Get("purchase.system_disabled", player));
            }

            // 检查经济系统是否可用
            if (_economyAPI == null)
            {
                _logger.LogError(_translation.GetConsole("system.economy_warning"));
                return (false, _translation.Get("purchase.economy_unavailable", player));
            }

            var model = GetModelById(modelId);
            if (model == null)
            {
                return (false, _translation.Get("purchase.model_not_found", player));
            }

            // 检查权限
            if (!CanPlayerUseModel(player, modelId))
            {
                return (false, _translation.Get("purchase.no_permission", player));
            }

            // 检查是否已拥有
            var alreadyOwns = await _database.PlayerOwnsModelAsync(player.SteamID, modelId);
            if (alreadyOwns)
            {
                return (false, _translation.Get("purchase.already_owned", player));
            }

            // 免费模型直接添加
            if (model.Price == 0)
            {
                await _database.AddOwnedModelAsync(
                    player.SteamID,
                    player.Controller.PlayerName,
                    modelId,
                    0,
                    "free");
                return (true, _translation.Get("purchase.free_model", player, model.DisplayName));
            }

            // 检查余额
            var walletKind = _config.CurrentValue.WalletKind;
            var balance = _economyAPI.GetPlayerBalance(player.SteamID, walletKind);

            if (balance < model.Price)
            {
                return (false, _translation.Get("purchase.insufficient_funds", player, model.Price, walletKind, balance, walletKind));
            }

            // 扣除金额
            var balanceBefore = balance;
            _economyAPI.SubtractPlayerBalance(player.SteamID, walletKind, model.Price);

            var balanceAfter = _economyAPI.GetPlayerBalance(player.SteamID, walletKind);

            // 添加到拥有列表
            await _database.AddOwnedModelAsync(
                player.SteamID,
                player.Controller.PlayerName,
                modelId,
                model.Price,
                "purchased");

            // 记录交易
            await _database.LogTransactionAsync(
                player.SteamID,
                player.Controller.PlayerName,
                modelId,
                "purchase", model.Price, (int)balanceBefore, (int)balanceAfter);
            var newBalance = _economyAPI.GetPlayerBalance(player.SteamID, walletKind);
            _logger.LogInformation(_translation.GetConsole("modelservice.purchased", player.Controller.PlayerName, model.DisplayName, model.Price, walletKind));

            return (true, _translation.Get("purchase.success", player, model.DisplayName, newBalance, walletKind));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("modelservice.purchase_failed", modelId));
            return (false, _translation.Get("purchase.failed", player));
        }
    }
}