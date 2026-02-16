using System;
using System.Collections.Generic;
using System.Linq;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using Microsoft.Extensions.Logging;

namespace PlayersModel.Services;

/// <summary>
/// 模型钩子管理服务 - 处理SetModel冲突
/// </summary>
public interface IModelHookService
{
    /// <summary>
    /// 初始化模型钩子
    /// </summary>
    void InitializeHooks();
    
    /// <summary>
    /// 标记玩家需要强制应用模型
    /// </summary>
    void MarkPlayerForModelApply(ulong steamId, string modelPath, float delaySeconds = 0.05f);
    
    /// <summary>
    /// 获取玩家标记的模型
    /// </summary>
    string? GetMarkedModel(ulong steamId);
    
    /// <summary>
    /// 清除玩家标记
    /// </summary>
    void ClearMarkedModel(ulong steamId);
}

public class ModelHookService : IModelHookService
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<ModelHookService> _logger;
    private readonly Dictionary<ulong, (string modelPath, float applyTime)> _markedModels = new();
    private readonly HashSet<ulong> _isApplyingModel = new();

    public ModelHookService(ISwiftlyCore core, ILogger<ModelHookService> logger)
    {
        _core = core;
        _logger = logger;
    }

    public void InitializeHooks()
    {
        _logger.LogInformation("Initializing model hooks...");
        
        // 定期检查标记的模型并重新应用 - 使用递归延迟替代
        CheckAndApplyMarkedModels();
    }

    private void CheckAndApplyMarkedModels()
    {
        try
        {
            var now = DateTime.UtcNow.Ticks / 10000000f; // 转换为秒
            var steamIdsToRemove = new List<ulong>();
            
            foreach (var kvp in _markedModels.ToList()) // 复制列表避免迭代修改
            {
                var steamId = kvp.Key;
                var (modelPath, applyTime) = kvp.Value;
                
                // 检查是否到达应用时间
                if (now >= applyTime)
                {
                    try
                    {
                        if (_isApplyingModel.Contains(steamId))
                            continue;
                        
                        _isApplyingModel.Add(steamId);
                        
                        // 应用模型 - 从在线玩家中查找
                        var allPlayers = _core.PlayerManager.GetAlive();
                        var player = allPlayers.FirstOrDefault(p => p.SteamID == steamId);
                        
                        if (player?.Pawn?.IsValid == true)
                        {
                            player.Pawn.SetModel(modelPath);
                            _logger.LogInformation($"Applied marked model to {player.Controller.PlayerName}: {modelPath}");
                            steamIdsToRemove.Add(steamId);
                        }
                        
                        _isApplyingModel.Remove(steamId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error applying marked model for steamId {steamId}: {ex.Message}");
                        _isApplyingModel.Remove(steamId);
                        steamIdsToRemove.Add(steamId);
                    }
                }
            }
            
            // 清除已处理的标记
            foreach (var steamId in steamIdsToRemove)
            {
                _markedModels.Remove(steamId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in CheckAndApplyMarkedModels: {ex.Message}");
        }
        finally
        {
            // 递归调用自己，间隔100ms
            _core.Scheduler.DelayBySeconds(0.1f, () => CheckAndApplyMarkedModels());
        }
    }

    public void MarkPlayerForModelApply(ulong steamId, string modelPath, float delaySeconds = 0.05f)
    {
        var applyTime = DateTime.UtcNow.Ticks / 10000000f + delaySeconds;
        _markedModels[steamId] = (modelPath, applyTime);
        _logger.LogDebug($"Marked steamId {steamId} for model apply in {delaySeconds}s: {modelPath}");
    }

    public string? GetMarkedModel(ulong steamId)
    {
        return _markedModels.TryGetValue(steamId, out var marked) ? marked.modelPath : null;
    }

    public void ClearMarkedModel(ulong steamId)
    {
        _markedModels.Remove(steamId);
    }
}
