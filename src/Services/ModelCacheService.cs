using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace PlayersModel.Services;

/// <summary>
/// 玩家模型缓存数据
/// </summary>
public class PlayerModelCache
{
    public string? AllTeamModelPath { get; set; }
    public string? AllTeamArmsPath { get; set; }
    public string? CTModelPath { get; set; }
    public string? CTArmsPath { get; set; }
    public string? TModelPath { get; set; }
    public string? TArmsPath { get; set; }
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
    /// 获取玩家应该应用的模型路径（根据优先级）
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
        return _cache.TryGetValue(steamId, out var cache) ? cache : null;
    }

    /// <summary>
    /// 更新玩家的模型缓存
    /// </summary>
    public void UpdatePlayerCache(ulong steamId, string team, string? modelPath, string? armsPath)
    {
        var cache = _cache.GetOrAdd(steamId, _ => new PlayerModelCache());
        
        switch (team.ToUpper())
        {
            case "ALL":
                cache.AllTeamModelPath = modelPath;
                cache.AllTeamArmsPath = armsPath;
                break;
            case "CT":
                cache.CTModelPath = modelPath;
                cache.CTArmsPath = armsPath;
                break;
            case "T":
                cache.TModelPath = modelPath;
                cache.TArmsPath = armsPath;
                break;
        }
        
        cache.LastUpdated = DateTime.UtcNow;
        _logger.LogDebug($"Updated cache for player {steamId}, team {team}");
    }

    /// <summary>
    /// 批量加载玩家缓存
    /// </summary>
    public async Task BatchLoadPlayerCachesAsync(IEnumerable<ulong> steamIds)
    {
        try
        {
            var steamIdList = steamIds.ToList();
            if (steamIdList.Count == 0) return;

            _logger.LogInformation(_translation.GetConsole("cache.batch_loading", steamIdList.Count));

            // 批量获取所有玩家的模型数据
            var batchData = await _database.GetBatchPlayerCurrentModelsAsync(steamIdList);

            foreach (var steamId in steamIdList)
            {
                if (batchData.TryGetValue(steamId, out var teamModels))
                {
                    var cache = _cache.GetOrAdd(steamId, _ => new PlayerModelCache());
                    
                    // 更新All槽位
                    if (teamModels.TryGetValue("All", out var allModel))
                    {
                        cache.AllTeamModelPath = allModel.modelPath;
                        cache.AllTeamArmsPath = allModel.armsPath;
                    }
                    
                    // 更新CT槽位
                    if (teamModels.TryGetValue("CT", out var ctModel))
                    {
                        cache.CTModelPath = ctModel.modelPath;
                        cache.CTArmsPath = ctModel.armsPath;
                    }
                    
                    // 更新T槽位
                    if (teamModels.TryGetValue("T", out var tModel))
                    {
                        cache.TModelPath = tModel.modelPath;
                        cache.TArmsPath = tModel.armsPath;
                    }
                    
                    cache.LastUpdated = DateTime.UtcNow;
                }
                else
                {
                    // 即使没有数据也创建空缓存，避免重复查询
                    _cache.GetOrAdd(steamId, _ => new PlayerModelCache
                    {
                        LastUpdated = DateTime.UtcNow
                    });
                }
            }

            _logger.LogInformation(_translation.GetConsole("cache.batch_loaded", steamIdList.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("cache.batch_load_failed"));
        }
    }

    /// <summary>
    /// 获取玩家应该应用的模型路径（根据优先级：All > CT/T）
    /// </summary>
    public string? GetModelPathToApply(ulong steamId, string teamName)
    {
        var cache = GetPlayerCache(steamId);
        if (cache == null) return null;

        // 优先级系统：All > CT/T
        if (!string.IsNullOrEmpty(cache.AllTeamModelPath))
        {
            return cache.AllTeamModelPath;
        }

        // 根据阵营返回对应模型
        return teamName.ToUpper() switch
        {
            "CT" => cache.CTModelPath,
            "T" => cache.TModelPath,
            _ => null
        };
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
        _logger.LogInformation(_translation.GetConsole("cache.all_cleared"));
    }
}