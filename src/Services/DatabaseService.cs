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
    Task AddOwnedModelAsync(ulong steamId, string playerName, string modelId, int pricePaid = 0, string source = "purchased", ulong? giftedBy = null, string? notes = null);

    /// <summary>
    /// 获取玩家拥有的所有模型 ID
    /// </summary>
    Task<List<string>> GetPlayerOwnedModelsAsync(ulong steamId);

    /// <summary>
    /// 批量获取多个玩家拥有的所有模型
    /// </summary>
    Task<Dictionary<ulong, List<string>>> GetBatchPlayerOwnedModelsAsync(IEnumerable<ulong> steamIds);

    /// <summary>
    /// 获取玩家当前装备的模型
    /// </summary>
    Task<(string? modelPath, string? armsPath)> GetPlayerCurrentModelAsync(ulong steamId, string team);

    /// <summary>
    /// 设置玩家当前装备的模型
    /// </summary>
    Task SetPlayerCurrentModelAsync(ulong steamId, string playerName, string modelId, string modelPath, string armsPath, string team);

    /// <summary>
    /// 批量获取多个玩家的当前装备模型
    /// </summary>
    Task<Dictionary<ulong, Dictionary<string, (string? modelPath, string? armsPath)>>> GetBatchPlayerCurrentModelsAsync(IEnumerable<ulong> steamIds);

    /// <summary>
    /// 删除玩家指定阵营的当前模型
    /// </summary>
    Task DeletePlayerCurrentModelAsync(ulong steamId, string team);

    /// <summary>
    /// 删除玩家的所有数据
    /// </summary>
    Task DeletePlayerDataAsync(ulong steamId);
    
    /// <summary>
    /// 记录交易日志
    /// </summary>
    Task LogTransactionAsync(ulong steamId, string playerName, string modelId, string action, int amount = 0, int balanceBefore = 0, int balanceAfter = 0, ulong? operatorId = null, string? reason = null);

    /// <summary>
    /// 更新玩家模型的 MeshGroup 配置
    /// </summary>
    Task UpdatePlayerMeshGroupsAsync(ulong steamId, string team, string meshGroupsData);

    /// <summary>
    /// 获取玩家模型的 MeshGroup 配置
    /// </summary>
    Task<string?> GetPlayerMeshGroupsAsync(ulong steamId, string team);
}

/// <summary>
/// 数据库服务实现
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<PluginConfig> _config;
    private readonly ILogger<DatabaseService> _logger;
    private readonly ITranslationService _translation;

    public DatabaseService(
        ISwiftlyCore core,
        IOptionsMonitor<PluginConfig> config,
        ILogger<DatabaseService> logger,
        ITranslationService translation)
    {
        _core = core;
        _config = config;
        _logger = logger;
        _translation = translation;
    }

    private IDbConnection GetConnection()
    {
        var connectionKey = _config.CurrentValue.Database.ConnectionKey;
        return _core.Database.GetConnection(connectionKey);
    }

    private string OwnedModelsTable => _config.CurrentValue.Database.OwnedModelsTable;
    private string CurrentModelsTable => _config.CurrentValue.Database.CurrentModelsTable;
    private string StatisticsTable => "playersmodel_statistics";
    private string TransactionsTable => "playersmodel_transactions";

    /// <summary>
    /// 初始化数据库表
    /// </summary>
    public async Task InitializeTablesAsync()
    {
        if (!_config.CurrentValue.Database.AutoCreateTables)
        {
            _logger.LogInformation(_translation.GetConsole("database.auto_create_disabled"));
            return;
        }

        try
        {
            using var connection = GetConnection();

            // 创建玩家拥有的模型表（优化版）
            var createOwnedModelsTable = $@"
                CREATE TABLE IF NOT EXISTS {OwnedModelsTable} (
                    id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    steam_id BIGINT NOT NULL,
                    player_name VARCHAR(64),
                    model_id VARCHAR(128) NOT NULL,
                    price_paid INT DEFAULT 0,
                    source VARCHAR(32) DEFAULT 'purchased',
                    gifted_by BIGINT NULL,
                    notes VARCHAR(255) NULL,
                    purchased_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE KEY unique_player_model (steam_id, model_id),
                    INDEX idx_steam_id (steam_id),
                    INDEX idx_source (source),
                    INDEX idx_purchased_at (purchased_at)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ";

            await connection.ExecuteAsync(createOwnedModelsTable);
            _logger.LogInformation(_translation.GetConsole("database.table_initialized", OwnedModelsTable));

            // 创建玩家当前装备的模型表（优化版）
            var createCurrentModelsTable = $@"
                CREATE TABLE IF NOT EXISTS {CurrentModelsTable} (
                    id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    steam_id BIGINT NOT NULL,
                    player_name VARCHAR(64),
                    model_id VARCHAR(128),
                    model_path VARCHAR(255),
                    arms_path VARCHAR(255),
                    team VARCHAR(8),
                    meshgroup VARCHAR(255) NULL,
                    usage_count INT DEFAULT 0,
                    equipped_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    UNIQUE KEY unique_steam_team (steam_id, team),
                    INDEX idx_model_id (model_id),
                    INDEX idx_team (team)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ";

            await connection.ExecuteAsync(createCurrentModelsTable);
            _logger.LogInformation(_translation.GetConsole("database.table_initialized", CurrentModelsTable));

            // 创建统计表
            var createStatisticsTable = $@"
                CREATE TABLE IF NOT EXISTS {StatisticsTable} (
                    id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    steam_id BIGINT NOT NULL,
                    model_id VARCHAR(128) NOT NULL,
                    equipped_count INT DEFAULT 0,
                    total_playtime INT DEFAULT 0,
                    last_used_at TIMESTAMP NULL,
                    favorite TINYINT DEFAULT 0,
                    UNIQUE KEY unique_player_model_stat (steam_id, model_id),
                    INDEX idx_steam_id (steam_id),
                    INDEX idx_model_id (model_id),
                    INDEX idx_favorite (favorite)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ";

            await connection.ExecuteAsync(createStatisticsTable);
            _logger.LogInformation(_translation.GetConsole("database.table_initialized", StatisticsTable));

            // 创建交易日志表
            var createTransactionsTable = $@"
                CREATE TABLE IF NOT EXISTS {TransactionsTable} (
                    id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    steam_id BIGINT NOT NULL,
                    player_name VARCHAR(64),
                    model_id VARCHAR(128) NOT NULL,
                    action VARCHAR(32) NOT NULL,
                    amount INT DEFAULT 0,
                    balance_before INT DEFAULT 0,
                    balance_after INT DEFAULT 0,
                    operator_id BIGINT NULL,
                    reason VARCHAR(255) NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    INDEX idx_steam_id (steam_id),
                    INDEX idx_model_id (model_id),
                    INDEX idx_action (action),
                    INDEX idx_created_at (created_at)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ";

            await connection.ExecuteAsync(createTransactionsTable);
            _logger.LogInformation(_translation.GetConsole("database.table_initialized", TransactionsTable));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("database.init_failed"));
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
            _logger.LogError(ex, _translation.GetConsole("database.check_owns_failed", steamId, modelId));
            return false;
        }
    }

    /// <summary>
    /// 添加玩家拥有的模型
    /// </summary>
    public async Task AddOwnedModelAsync(ulong steamId, string playerName, string modelId, int pricePaid = 0, string source = "purchased", ulong? giftedBy = null, string? notes = null)
    {
        try
        {
            using var connection = GetConnection();
            var sql = $@"
                INSERT IGNORE INTO {OwnedModelsTable} 
                (steam_id, player_name, model_id, price_paid, source, gifted_by, notes)
                VALUES (@SteamId, @PlayerName, @ModelId, @PricePaid, @Source, @GiftedBy, @Notes)
            ";

            await connection.ExecuteAsync(sql, new
            {
                SteamId = steamId,
                PlayerName = playerName,
                ModelId = modelId,
                PricePaid = pricePaid,
                Source = source,
                GiftedBy = giftedBy,
                Notes = notes
            });

            _logger.LogDebug(_translation.GetConsole("database.add_model_debug", steamId, modelId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("database.add_model_failed", steamId, modelId));
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
            _logger.LogError(ex, _translation.GetConsole("database.get_models_failed", steamId));
            return new List<string>();
        }
    }

    /// <summary>
    /// 批量获取多个玩家拥有的所有模型
    /// </summary>
    public async Task<Dictionary<ulong, List<string>>> GetBatchPlayerOwnedModelsAsync(IEnumerable<ulong> steamIds)
    {
        try
        {
            var steamIdList = steamIds.ToList();
            if (steamIdList.Count == 0)
                return new Dictionary<ulong, List<string>>();

            using var connection = GetConnection();
            var sql = $@"
                SELECT steam_id, model_id FROM {OwnedModelsTable}
                WHERE steam_id IN @SteamIds
            ";

            var results = await connection.QueryAsync<(ulong steam_id, string model_id)>(
                sql, new { SteamIds = steamIdList });

            return results
                .GroupBy(r => r.steam_id)
                .ToDictionary(g => g.Key, g => g.Select(r => r.model_id).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("database.batch_get_models_failed"));
            return new Dictionary<ulong, List<string>>();
        }
    }

    /// <summary>
    /// 批量获取多个玩家的当前装备模型
    /// </summary>
    public async Task<Dictionary<ulong, Dictionary<string, (string? modelPath, string? armsPath)>>> GetBatchPlayerCurrentModelsAsync(IEnumerable<ulong> steamIds)
    {
        try
        {
            var steamIdList = steamIds.ToList();
            if (steamIdList.Count == 0)
                return new Dictionary<ulong, Dictionary<string, (string? modelPath, string? armsPath)>>();

            using var connection = GetConnection();
            var sql = $@"
                SELECT steam_id, team, model_path, arms_path FROM {CurrentModelsTable}
                WHERE steam_id IN @SteamIds
            ";

            var results = await connection.QueryAsync<dynamic>(sql, new { SteamIds = steamIdList });
            
            var playerModels = new Dictionary<ulong, Dictionary<string, (string? modelPath, string? armsPath)>>();
            
            foreach (var row in results)
            {
                ulong steamId = (ulong)row.steam_id;
                string team = (string)row.team;
                string? modelPath = row.model_path;
                string? armsPath = row.arms_path;
                
                if (!playerModels.ContainsKey(steamId))
                    playerModels[steamId] = new Dictionary<string, (string?, string?)>();
                
                playerModels[steamId][team] = (modelPath, armsPath);
            }
            
            return playerModels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("database.batch_get_current_failed"));
            return new Dictionary<ulong, Dictionary<string, (string? modelPath, string? armsPath)>>();
        }
    }

    /// <summary>
    /// 获取玩家当前装备的模型
    /// </summary>
    public async Task<(string? modelPath, string? armsPath)> GetPlayerCurrentModelAsync(ulong steamId, string team)
    {
        try
        {
            using var connection = GetConnection();
            var sql = $@"
                SELECT model_path, arms_path FROM {CurrentModelsTable}
                WHERE steam_id = @SteamId AND team = @Team
            ";

            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { SteamId = steamId, Team = team });
            
            if (result == null)
                return (null, null);

            return ((string?)result.model_path, (string?)result.arms_path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("database.get_current_failed", steamId));
            return (null, null);
        }
    }

    /// <summary>
    /// 设置玩家当前装备的模型
    /// </summary>
    public async Task SetPlayerCurrentModelAsync(ulong steamId, string playerName, string modelId, string modelPath, string armsPath, string team)
    {
        try
        {
            using var connection = GetConnection();
            
            var sql = $@"
                INSERT INTO {CurrentModelsTable} 
                (steam_id, player_name, model_id, model_path, arms_path, team, usage_count)
                VALUES (@SteamId, @PlayerName, @ModelId, @ModelPath, @ArmsPath, @Team, 1)
                ON DUPLICATE KEY UPDATE 
                    player_name = @PlayerName,
                    model_id = @ModelId,
                    model_path = @ModelPath, 
                    arms_path = @ArmsPath,
                    team = @Team,
                    usage_count = usage_count + 1,
                    equipped_time = CURRENT_TIMESTAMP
            ";

            await connection.ExecuteAsync(sql, new
            {
                SteamId = steamId,
                PlayerName = playerName,
                ModelId = modelId,
                ModelPath = modelPath,
                ArmsPath = armsPath,
                Team = team
            });

            // 更新统计表
            var statsSql = $@"
                INSERT INTO {StatisticsTable}
                (steam_id, model_id, equipped_count, last_used_at)
                VALUES (@SteamId, @ModelId, 1, CURRENT_TIMESTAMP)
                ON DUPLICATE KEY UPDATE
                    equipped_count = equipped_count + 1,
                    last_used_at = CURRENT_TIMESTAMP
            ";

            await connection.ExecuteAsync(statsSql, new
            {
                SteamId = steamId,
                ModelId = modelId
            });
            
            _logger.LogDebug(_translation.GetConsole("database.set_model_debug", steamId, modelPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("database.set_model_failed", steamId));
            throw;
        }
    }

    /// <summary>
    /// 删除玩家指定阵营的当前模型
    /// </summary>
    public async Task DeletePlayerCurrentModelAsync(ulong steamId, string team)
    {
        try
        {
            using var connection = GetConnection();
            var sql = $@"
                DELETE FROM {CurrentModelsTable}
                WHERE steam_id = @SteamId AND team = @Team
            ";

            await connection.ExecuteAsync(sql, new { SteamId = steamId, Team = team });
            
            _logger.LogDebug(_translation.GetConsole("database.delete_model_debug", steamId, team));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("database.delete_model_failed", steamId, team));
            throw;
        }
    }

    /// <summary>
    /// 记录交易日志
    /// </summary>
    public async Task LogTransactionAsync(ulong steamId, string playerName, string modelId, string action, int amount = 0, int balanceBefore = 0, int balanceAfter = 0, ulong? operatorId = null, string? reason = null)
    {
        try
        {
            using var connection = GetConnection();
            var sql = $@"
                INSERT INTO {TransactionsTable}
                (steam_id, player_name, model_id, action, amount, balance_before, balance_after, operator_id, reason)
                VALUES (@SteamId, @PlayerName, @ModelId, @Action, @Amount, @BalanceBefore, @BalanceAfter, @OperatorId, @Reason)
            ";

            await connection.ExecuteAsync(sql, new
            {
                SteamId = steamId,
                PlayerName = playerName,
                ModelId = modelId,
                Action = action,
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                OperatorId = operatorId,
                Reason = reason
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log transaction");
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
            
            await connection.ExecuteAsync($"DELETE FROM {StatisticsTable} WHERE steam_id = @SteamId",
                new { SteamId = steamId });
            
            _logger.LogInformation(_translation.GetConsole("database.delete_data", steamId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("database.delete_failed", steamId));
            throw;
        }
    }

    /// <summary>
    /// 更新玩家模型的 MeshGroup 配置
    /// </summary>
    public async Task UpdatePlayerMeshGroupsAsync(ulong steamId, string team, string meshGroupsData)
    {
        try
        {
            using var connection = GetConnection();
            var sql = $@"
                UPDATE {CurrentModelsTable}
                SET meshgroup = @MeshGroups
                WHERE steam_id = @SteamId AND team = @Team
            ";

            await connection.ExecuteAsync(sql, new
            {
                SteamId = steamId,
                Team = team,
                MeshGroups = meshGroupsData
            });

            _logger.LogDebug($"更新 MeshGroup 配置: SteamID={steamId}, Team={team}, Data={meshGroupsData}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"更新 MeshGroup 配置失败: SteamID={steamId}, Team={team}");
        }
    }

    /// <summary>
    /// 获取玩家模型的 MeshGroup 配置
    /// </summary>
    public async Task<string?> GetPlayerMeshGroupsAsync(ulong steamId, string team)
    {
        using var connection = GetConnection();
        var sql = $@"SELECT meshgroup FROM {CurrentModelsTable} WHERE steam_id = @SteamId AND team = @Team";
        
        return await connection.QueryFirstOrDefaultAsync<string?>(sql, new { SteamId = steamId, Team = team });
    }
}