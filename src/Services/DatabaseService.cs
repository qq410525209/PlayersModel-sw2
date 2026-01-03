using System.Data;
using Dapper;
using PlayersModel.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace PlayersModel.Services;

/// <summary>
/// 数据库服务接口
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// 初始化数据库表
    /// </summary>
    Task InitializeTablesAsync();

    /// <summary>
    /// 检查玩家是否拥有指定模型
    /// </summary>
    Task<bool> PlayerOwnsModelAsync(ulong steamId, string modelId);

    /// <summary>
    /// 添加玩家拥有的模型
    /// </summary>
    Task AddOwnedModelAsync(ulong steamId, string modelId);

    /// <summary>
    /// 获取玩家拥有的所有模型 ID
    /// </summary>
    Task<List<string>> GetPlayerOwnedModelsAsync(ulong steamId);

    /// <summary>
    /// 获取玩家当前装备的模型
    /// </summary>
    Task<(string? modelPath, string? armsPath)> GetPlayerCurrentModelAsync(ulong steamId);

    /// <summary>
    /// 设置玩家当前装备的模型
    /// </summary>
    Task SetPlayerCurrentModelAsync(ulong steamId, string modelPath, string armsPath = "");

    /// <summary>
    /// 删除玩家的所有数据
    /// </summary>
    Task DeletePlayerDataAsync(ulong steamId);
}

/// <summary>
/// 数据库服务实现
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<PluginConfig> _config;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(
        ISwiftlyCore core,
        IOptionsMonitor<PluginConfig> config,
        ILogger<DatabaseService> logger)
    {
        _core = core;
        _config = config;
        _logger = logger;
    }

    private IDbConnection GetConnection()
    {
        var connectionKey = _config.CurrentValue.Database.ConnectionKey;
        return _core.Database.GetConnection(connectionKey);
    }

    private string OwnedModelsTable => _config.CurrentValue.Database.OwnedModelsTable;
    private string CurrentModelsTable => _config.CurrentValue.Database.CurrentModelsTable;

    /// <summary>
    /// 初始化数据库表
    /// </summary>
    public async Task InitializeTablesAsync()
    {
        if (!_config.CurrentValue.Database.AutoCreateTables)
        {
            _logger.LogInformation("数据库表自动创建已禁用");
            return;
        }

        try
        {
            using var connection = GetConnection();

            // 创建玩家拥有的模型表
            var createOwnedModelsTable = $@"
                CREATE TABLE IF NOT EXISTS {OwnedModelsTable} (
                    id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    steam_id BIGINT NOT NULL,
                    model_id VARCHAR(128) NOT NULL,
                    purchased_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE KEY unique_player_model (steam_id, model_id),
                    INDEX idx_steam_id (steam_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ";

            await connection.ExecuteAsync(createOwnedModelsTable);
            _logger.LogInformation($"数据库表 {OwnedModelsTable} 初始化成功");

            // 创建玩家当前装备的模型表 (新结构)
            var createCurrentModelsTable = $@"
                CREATE TABLE IF NOT EXISTS {CurrentModelsTable} (
                    steam_id BIGINT PRIMARY KEY,
                    model_path VARCHAR(255),
                    arms_path VARCHAR(255),
                    equipped_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ";

            await connection.ExecuteAsync(createCurrentModelsTable);
            _logger.LogInformation($"数据库表 {CurrentModelsTable} 初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化数据库表失败");
            throw;
        }
    }

    /// <summary>
    /// 检查玩家是否拥有指定模型
    /// </summary>
    public async Task<bool> PlayerOwnsModelAsync(ulong steamId, string modelId)
    {
        try
        {
            using var connection = GetConnection();
            var sql = $@"
                SELECT COUNT(*) FROM {OwnedModelsTable}
                WHERE steam_id = @SteamId AND model_id = @ModelId
            ";

            var count = await connection.ExecuteScalarAsync<int>(sql, new { SteamId = steamId, ModelId = modelId });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"检查玩家 {steamId} 是否拥有模型 {modelId} 失败");
            return false;
        }
    }

    /// <summary>
    /// 添加玩家拥有的模型
    /// </summary>
    public async Task AddOwnedModelAsync(ulong steamId, string modelId)
    {
        try
        {
            using var connection = GetConnection();
            var sql = $@"
                INSERT IGNORE INTO {OwnedModelsTable} (steam_id, model_id)
                VALUES (@SteamId, @ModelId)
            ";

            await connection.ExecuteAsync(sql, new { SteamId = steamId, ModelId = modelId });
            _logger.LogDebug($"添加玩家 {steamId} 拥有的模型 {modelId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"添加玩家 {steamId} 拥有的模型 {modelId} 失败");
            throw;
        }
    }

    /// <summary>
    /// 获取玩家拥有的所有模型 ID
    /// </summary>
    public async Task<List<string>> GetPlayerOwnedModelsAsync(ulong steamId)
    {
        try
        {
            using var connection = GetConnection();
            var sql = $@"
                SELECT model_id FROM {OwnedModelsTable}
                WHERE steam_id = @SteamId
            ";

            var result = await connection.QueryAsync<string>(sql, new { SteamId = steamId });
            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"获取玩家 {steamId} 拥有的模型列表失败");
            return new List<string>();
        }
    }

    /// <summary>
    /// 获取玩家当前装备的模型
    /// </summary>
    public async Task<(string? modelPath, string? armsPath)> GetPlayerCurrentModelAsync(ulong steamId)
    {
        try
        {
            using var connection = GetConnection();
            var sql = $@"
                SELECT model_path, arms_path FROM {CurrentModelsTable}
                WHERE steam_id = @SteamId
            ";

            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { SteamId = steamId });
            
            if (result == null)
                return (null, null);

            return ((string?)result.model_path, (string?)result.arms_path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"获取玩家 {steamId} 当前装备的模型失败");
            return (null, null);
        }
    }

    /// <summary>
    /// 设置玩家当前装备的模型
    /// </summary>
    public async Task SetPlayerCurrentModelAsync(ulong steamId, string modelPath, string armsPath = "")
    {
        try
        {
            using var connection = GetConnection();
            
            var sql = $@"
                INSERT INTO {CurrentModelsTable} (steam_id, model_path, arms_path)
                VALUES (@SteamId, @ModelPath, @ArmsPath)
                ON DUPLICATE KEY UPDATE 
                    model_path = @ModelPath, 
                    arms_path = @ArmsPath, 
                    equipped_time = CURRENT_TIMESTAMP
            ";

            await connection.ExecuteAsync(sql, new { 
                SteamId = steamId, 
                ModelPath = modelPath, 
                ArmsPath = armsPath 
            });
            
            _logger.LogDebug($"设置玩家 {steamId} 的模型: {modelPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"设置玩家 {steamId} 的模型失败");
            throw;
        }
    }

    /// <summary>
    /// 删除玩家的所有数据
    /// </summary>
    public async Task DeletePlayerDataAsync(ulong steamId)
    {
        try
        {
            using var connection = GetConnection();
            
            await connection.ExecuteAsync($"DELETE FROM {OwnedModelsTable} WHERE steam_id = @SteamId", 
                new { SteamId = steamId });
            
            await connection.ExecuteAsync($"DELETE FROM {CurrentModelsTable} WHERE steam_id = @SteamId", 
                new { SteamId = steamId });
            
            _logger.LogInformation($"删除玩家 {steamId} 的所有数据");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"删除玩家 {steamId} 的数据失败");
            throw;
        }
    }
}