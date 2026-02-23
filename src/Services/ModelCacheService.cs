using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace PlayersModel.Services;

/// <summary>
/// 玩家模型缓存数据（简化版 - 一个玩家只有一个模型）
/// </summary>
public class PlayerModelCache
{
    public string? ModelPath { get; set; }
    public string? ArmsPath { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// 模型缓存服务接口
/// </summary>
public interface IModelCacheService
{
    /// <summary>
    /// 获取玩家的模型缓存
    /// </summary>
    PlayerModelCache? GetPlayerCache(ulong steamId);
    
    /// <summary>
    /// 更新玩家的模型缓存
    /// </summary>
    void UpdatePlayerCache(ulong steamId, string team, string? modelPath, string? armsPath);
    
    /// <summary>
    /// 批量加载玩家缓存
    /// </summary>
    Task BatchLoadPlayerCachesAsync(IEnumerable<ulong> steamIds);
    
    /// <summary>
    /// 清除玩家缓存
    /// </summary>
    void ClearPlayerCache(ulong steamId);
    
    /// <summary>
    /// 清除所有缓存
    /// </summary>
    void ClearAllCaches();
    
    /// <summary>
    /// 获取玩家应该应用的模型路径（简化版 - 直接返回存储的模型）
    /// </summary>
    string? GetModelPathToApply(ulong steamId, string teamName);
}

/// <summary>
/// 模型缓存服务实现
/// </summary>
public class ModelCacheService : IModelCacheService
{
    private readonly ConcurrentDictionary<ulong, PlayerModelCache> _cache = new();
    private readonly IDatabaseService _database;
    private readonly ILogger<ModelCacheService> _logger;
    private readonly ITranslationService _translation;

    public ModelCacheService(
        IDatabaseService database,
        ILogger<ModelCacheService> logger,
        ITranslationService translation)
    {
        _database = database;
        _logger = logger;
        _translation = translation;
    }

    /// <summary>
    /// 获取玩家的模型缓存
    /// </summary>
    public PlayerModelCache? GetPlayerCache(ulong steamId)
    {
        _cache.TryGetValue(steamId, out var cache);
        return cache;
    }

    /// <summary>
    /// 更新玩家的模型缓存
    /// </summary>
    public void UpdatePlayerCache(ulong steamId, string team, string? modelPath, string? armsPath)
    {
        var cache = new PlayerModelCache
        {
            ModelPath = modelPath,
            ArmsPath = armsPath,
            LastUpdated = DateTime.UtcNow
        };

        _cache.AddOrUpdate(steamId, cache, (_, _) => cache);
        _logger.LogDebug($"Updated cache for player {steamId}");
    }

    /// <summary>
    /// 批量加载玩家缓存
    /// </summary>
    public async Task BatchLoadPlayerCachesAsync(IEnumerable<ulong> steamIds)
    {
        try
        {
            var steamIdList = steamIds.ToList();
            if (steamIdList.Count == 0)
                return;

            _logger.LogInformation(
                _translation.GetConsole("cache.batch_loading", steamIdList.Count.ToString()) 
                ?? $"Batch loading model cache for {steamIdList.Count} players..."
            );

            // 从数据库批量加载当前模型
            var playerModels = await _database.GetBatchPlayerCurrentModelsAsync(steamIdList);

            foreach (var steamId in steamIdList)
            {
                if (playerModels.TryGetValue(steamId, out var models))
                {
                    // 由于现在只有一个模型，直接取第一个（键为 ""）
                    if (models.TryGetValue("", out var modelData))
                    {
                        var cache = new PlayerModelCache
                        {
                            ModelPath = modelData.modelPath,
                            ArmsPath = modelData.armsPath,
                            LastUpdated = DateTime.UtcNow
                        };
                        _cache.AddOrUpdate(steamId, cache, (_, _) => cache);
                    }
                    else
                    {
                        // 玩家没有装备模型
                        _cache.TryRemove(steamId, out _);
                    }
                }
                else
                {
                    // 玩家在数据库中没有记录
                    _cache.TryRemove(steamId, out _);
                }
            }

            _logger.LogInformation(
                _translation.GetConsole("cache.batch_loaded", steamIdList.Count.ToString())
                ?? $"Successfully batch loaded model cache for {steamIdList.Count} players"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("cache.batch_load_failed") ?? "Failed to batch load player model cache");
        }
    }

    /// <summary>
    /// 清除玩家缓存
    /// </summary>
    public void ClearPlayerCache(ulong steamId)
    {
        _cache.TryRemove(steamId, out _);
        _logger.LogDebug($"Cleared cache for player {steamId}");
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public void ClearAllCaches()
    {
        _cache.Clear();
        _logger.LogInformation(_translation.GetConsole("cache.all_cleared") ?? "Cleared all model caches");
    }

    /// <summary>
    /// 获取玩家应该应用的模型路径（简化版 - 直接返回缓存中的模型）
    /// </summary>
    public string? GetModelPathToApply(ulong steamId, string teamName)
    {
        if (_cache.TryGetValue(steamId, out var cache))
        {
            return cache.ModelPath;
        }

        return null;
    }
}