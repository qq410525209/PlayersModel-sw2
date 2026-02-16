using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Misc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayersModel.Services;

/// <summary>
/// 原生 Hook 服务 - 使用事件来管理模型应用
/// </summary>
public class NativeHookService
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<NativeHookService> _logger;
    private readonly IModelHookService _modelHookService;
    private readonly IDatabaseService _databaseService;
    private readonly IModelCacheService _modelCacheService;
    
    // 跟踪玩家当前的模型
    private readonly Dictionary<ulong, string> _playerCurrentModels = new();
    // 跟踪玩家当前的阵营
    private readonly Dictionary<ulong, int> _playerCurrentTeams = new();

    public NativeHookService(
        ISwiftlyCore core,
        ILogger<NativeHookService> logger,
        IModelHookService modelHookService,
        IDatabaseService databaseService,
        IModelCacheService modelCacheService)
    {
        _core = core;
        _logger = logger;
        _modelHookService = modelHookService;
        _databaseService = databaseService;
        _modelCacheService = modelCacheService;
    }

    /// <summary>
    /// 初始化事件监听
    /// </summary>
    public void InitializeHooks()
    {
        _logger.LogInformation("Initializing model hooks via game events...");
        
        try
        {
            // 监听玩家进入服务器和重生事件，重新应用模型
            _core.Event.OnClientPutInServer += (args) => OnPlayerSpawnOrTeamChange(args.PlayerId);
            
            _logger.LogInformation("✓ Model hooks initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error initializing hooks: {ex.Message}");
        }
    }

    /// <summary>
    /// 玩家 Spawn 或切队时的处理
    /// </summary>
    private void OnPlayerSpawnOrTeamChange(int playerId)
    {
        try
        {
            var playerManager = _core.PlayerManager.GetAlive();
            var player = playerManager.FirstOrDefault(p => p.Slot == playerId);
            
            if (player == null || !player.IsValid || player.SteamID == 0)
                return;
            
            // 检查是否切队
            var currentTeam = player.Controller.TeamNum;
            bool teamChanged = false;
            
            if (!_playerCurrentTeams.TryGetValue(player.SteamID, out var lastTeam))
            {
                _playerCurrentTeams[player.SteamID] = currentTeam;
            }
            else if (lastTeam != currentTeam)
            {
                teamChanged = true;
                _playerCurrentTeams[player.SteamID] = currentTeam;
            }
            
            // 延迟应用模型，确保 Pawn 已完全初始化
            var delaySeconds = teamChanged ? 0.3f : 0.15f;
            _core.Scheduler.DelayBySeconds(delaySeconds, () =>
            {
                if (!player.IsValid || player.Pawn?.IsValid != true)
                    return;
                
                ApplyPlayerModel(player);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in OnPlayerSpawnOrTeamChange: {ex.Message}");
        }
    }

    /// <summary>
    /// 为玩家应用保存的模型
    /// </summary>
    private void ApplyPlayerModel(IPlayer player)
    {
        try
        {
            var currentTeam = player.Controller.TeamNum;
            var teamName = currentTeam == 2 ? "T" : currentTeam == 3 ? "CT" : "";
            
            if (string.IsNullOrEmpty(teamName))
            {
                _logger.LogDebug($"Player {player.Controller.PlayerName} not in valid team");
                return;
            }
            
            // 获取玩家应该使用的模型（优先级：All > CT/T）
            var (modelPath, armsPath) = _databaseService
                .GetPlayerCurrentModelAsync(player.SteamID, "All").GetAwaiter().GetResult();
            
            // 如果All槽位没有模型，尝试获取当前阵营槽位
            if (string.IsNullOrEmpty(modelPath))
            {
                (modelPath, armsPath) = _databaseService
                    .GetPlayerCurrentModelAsync(player.SteamID, teamName).GetAwaiter().GetResult();
            }
            
            // 如果都没有，使用默认模型（这部分框架会自动处理）
            if (!string.IsNullOrEmpty(modelPath) && player.Pawn?.IsValid == true)
            {
                // 检查是否需要应用（避免重复应用）
                if (!_playerCurrentModels.TryGetValue(player.SteamID, out var lastModel) || 
                    !lastModel.Equals(modelPath, StringComparison.OrdinalIgnoreCase))
                {
                    // 标记玩家的模型待应用，让ModelHookService处理
                    _modelHookService.MarkPlayerForModelApply(player.SteamID, modelPath, 0.05f);
                    _playerCurrentModels[player.SteamID] = modelPath;
                    _logger.LogDebug($"Applying model for {player.Controller.PlayerName} (Team: {teamName}): {modelPath}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error applying model: {ex.Message}");
        }
    }

    /// <summary>
    /// 清除玩家数据
    /// </summary>
    public void ClearPlayerData(ulong steamId)
    {
        _playerCurrentModels.Remove(steamId);
        _playerCurrentTeams.Remove(steamId);
    }
}
