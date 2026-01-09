using PlayersModel.Config;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace PlayersModel.Services;

/// <summary>
/// MeshGroup 组件服务接口
/// </summary>
public interface IMeshGroupService
{
    /// <summary>
    /// 应用 MeshGroup 到玩家
    /// </summary>
    void ApplyMeshGroupToPlayer(IPlayer player, string bodyGroupName, int index);

    /// <summary>
    /// 为模型应用所有默认 MeshGroup
    /// </summary>
    Task ApplyDefaultMeshGroups(IPlayer player, PlayerModelConfig model);

    /// <summary>
    /// 获取模型的所有 MeshGroup 配置
    /// </summary>
    List<MeshGroupConfig>? GetModelMeshGroups(string modelId);

    /// <summary>
    /// 切换 MeshGroup 选项
    /// </summary>
    Task<bool> ToggleMeshGroupOption(IPlayer player, string modelId, string componentId, string optionId);

    /// <summary>
    /// 获取玩家当前的 MeshGroup 配置
    /// </summary>
    Dictionary<string, int>? GetPlayerMeshGroups(ulong steamId);

    /// <summary>
    /// 从数据库加载并应用玩家的 MeshGroup 配置
    /// </summary>
    Task LoadAndApplyPlayerMeshGroupsAsync(IPlayer player, string modelId, string team);

    /// <summary>
    /// 序列化 MeshGroup 配置为数据库格式
    /// </summary>
    string SerializeMeshGroups(Dictionary<string, int> meshGroups);

    /// <summary>
    /// 从数据库格式反序列化 MeshGroup 配置
    /// </summary>
    Dictionary<string, int> DeserializeMeshGroups(string meshGroupsData);
}

/// <summary>
/// MeshGroup 组件服务实现
/// </summary>
public class MeshGroupService : IMeshGroupService
{
    private readonly ISwiftlyCore _core;
    private readonly IModelService _modelService;
    private readonly ILogger<MeshGroupService> _logger;
    private readonly IDatabaseService _databaseService;
    private readonly ITranslationService _translation;

    // 存储玩家当前装备的 MeshGroup 选项
    // Key: SteamID, Value: Dictionary<ComponentId, Index>
    private readonly Dictionary<ulong, Dictionary<string, int>> _playerMeshGroups = new();

    public MeshGroupService(
        ISwiftlyCore core,
        IModelService modelService,
        IDatabaseService databaseService,
        ILogger<MeshGroupService> logger,
        ITranslationService translation)
    {
        _core = core;
        _modelService = modelService;
        _logger = logger;
        _databaseService = databaseService;
        _translation = translation;
    }

    /// <summary>
    /// 应用 MeshGroup 到玩家
    /// </summary>
    public void ApplyMeshGroupToPlayer(IPlayer player, string bodyGroupName, int index)
    {
        try
        {
            var pawn = player.Pawn;
            if (pawn?.IsValid != true)
            {
                _logger.LogWarning(_translation.GetConsole("meshgroup.pawn_invalid", player.Controller.PlayerName) ?? $"Cannot apply MeshGroup: Player {player.Controller.PlayerName} pawn is invalid");
                return;
            }

            // 使用 AcceptInput 设置 BodyGroup
            // 格式: "BodyGroupName,Index"
            string value = $"{bodyGroupName},{index}";
            
            _core.Scheduler.DelayBySeconds(0.05f, () =>
            {
                if (pawn?.IsValid == true)
                {
                    pawn.AcceptInput("SetBodyGroup", activator: pawn, caller: pawn, value: value);
                    _logger.LogDebug(_translation.GetConsole("meshgroup.applied", bodyGroupName, index, player.Controller.PlayerName) ?? $"Applied MeshGroup: {bodyGroupName} = {index} to player {player.Controller.PlayerName}");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("meshgroup.apply_failed", bodyGroupName) ?? $"Failed to apply MeshGroup: {bodyGroupName}");
        }
    }

    /// <summary>
    /// 为模型应用所有默认 MeshGroup
    /// </summary>
    public async Task ApplyDefaultMeshGroups(IPlayer player, PlayerModelConfig model)
    {
        if (model.MeshGroups == null || model.MeshGroups.Count == 0)
            return;

        try
        {
            var steamId = player.SteamID;
            // 使用模型配置的 Team，这样 All 类型的模型可以跨阵营共享配置
            var teamName = model.Team == "Both" ? "All" : model.Team;
            
            // 先检查数据库中是否已有配置
            var existingData = await _databaseService.GetPlayerMeshGroupsAsync(steamId, teamName);
            if (!string.IsNullOrEmpty(existingData))
            {
                // 数据库中已有配置，直接返回，不应用默认值
                _logger.LogDebug(_translation.GetConsole("meshgroup.existing_config_skip", player.Controller.PlayerName) ?? $"Player {player.Controller.PlayerName} already has MeshGroup config, skipping default");
                return;
            }
            
            // 初始化玩家的 MeshGroup 配置
            if (!_playerMeshGroups.ContainsKey(steamId))
            {
                _playerMeshGroups[steamId] = new Dictionary<string, int>();
            }

            foreach (var meshGroup in model.MeshGroups)
            {
                // 查找默认选项
                var defaultOption = meshGroup.Options.FirstOrDefault(o => o.IsDefault) 
                                  ?? meshGroup.Options.FirstOrDefault();

                if (defaultOption != null)
                {
                    // 应用默认选项
                    ApplyMeshGroupToPlayer(player, meshGroup.BodyGroupName, defaultOption.Index);
                    
                    // 记录玩家选择
                    _playerMeshGroups[steamId][meshGroup.ComponentId] = defaultOption.Index;
                    
                    _logger.LogInformation(_translation.GetConsole("meshgroup.default_applied", player.Controller.PlayerName, meshGroup.DisplayName, defaultOption.DisplayName) ?? $"Applied default MeshGroup for player {player.Controller.PlayerName}: {meshGroup.DisplayName} -> {defaultOption.DisplayName}");
                }
            }

            // 保存到数据库
            var meshGroupsData = GetPlayerMeshGroupsData(steamId);
            await _databaseService.UpdatePlayerMeshGroupsAsync(steamId, teamName, meshGroupsData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("meshgroup.default_apply_failed", model.ModelId) ?? $"Failed to apply default MeshGroup, model: {model.ModelId}");
        }
    }

    /// <summary>
    /// 获取模型的所有 MeshGroup 配置
    /// </summary>
    public List<MeshGroupConfig>? GetModelMeshGroups(string modelId)
    {
        var model = _modelService.GetModelById(modelId);
        return model?.MeshGroups;
    }

    /// <summary>
    /// 切换 MeshGroup 选项
    /// </summary>
    public async Task<bool> ToggleMeshGroupOption(IPlayer player, string modelId, string componentId, string optionId)
    {
        try
        {
            var model = _modelService.GetModelById(modelId);
            if (model == null)
            {
                _logger.LogWarning(_translation.GetConsole("meshgroup.model_not_found", modelId) ?? $"Model not found: {modelId}");
                return false;
            }

            var meshGroup = model.MeshGroups?.FirstOrDefault(mg => mg.ComponentId == componentId);
            if (meshGroup == null)
            {
                _logger.LogWarning(_translation.GetConsole("meshgroup.component_not_found", componentId) ?? $"MeshGroup component not found: {componentId}");
                return false;
            }

            var option = meshGroup.Options.FirstOrDefault(o => o.OptionId == optionId);
            if (option == null)
            {
                _logger.LogWarning(_translation.GetConsole("meshgroup.option_not_found", optionId) ?? $"MeshGroup option not found: {optionId}");
                return false;
            }

            // 应用选项
            ApplyMeshGroupToPlayer(player, meshGroup.BodyGroupName, option.Index);

            // 记录玩家选择
            var steamId = player.SteamID;
            if (!_playerMeshGroups.ContainsKey(steamId))
            {
                _playerMeshGroups[steamId] = new Dictionary<string, int>();
            }
            _playerMeshGroups[steamId][componentId] = option.Index;

            // 保存到数据库
            var meshGroupsData = GetPlayerMeshGroupsData(steamId);
            // 使用模型配置的 Team，这样 All/Both 类型的模型可以跨阵营共享配置
            var teamName = model.Team == "Both" ? "All" : model.Team;
            await _databaseService.UpdatePlayerMeshGroupsAsync(steamId, teamName, meshGroupsData);

            _logger.LogInformation(_translation.GetConsole("meshgroup.toggle_success", player.Controller.PlayerName, meshGroup.DisplayName, option.DisplayName, option.Index) ?? $"Player {player.Controller.PlayerName} toggled MeshGroup: {meshGroup.DisplayName} -> {option.DisplayName} (Index: {option.Index})");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("meshgroup.toggle_failed") ?? "Failed to toggle MeshGroup option");
            return false;
        }
    }

    /// <summary>
    /// 从数据库加载并应用玩家的 MeshGroup 配置
    /// </summary>
    public async Task LoadAndApplyPlayerMeshGroupsAsync(IPlayer player, string modelId, string team)
    {
        try
        {
            var model = _modelService.GetModelById(modelId);
            if (model?.MeshGroups == null || model.MeshGroups.Count == 0)
            {
                _logger.LogDebug(_translation.GetConsole("meshgroup.no_config_skip", modelId) ?? $"Model {modelId} has no MeshGroup config, skipping load");
                return;
            }

            // 从数据库加载 MeshGroup 配置
            var meshGroupsData = await _databaseService.GetPlayerMeshGroupsAsync(player.SteamID, team);
            
            // 如果用当前阵营找不到，尝试用 "All" 查询
            if (string.IsNullOrEmpty(meshGroupsData) && team != "All")
            {
                meshGroupsData = await _databaseService.GetPlayerMeshGroupsAsync(player.SteamID, "All");
                if (!string.IsNullOrEmpty(meshGroupsData))
                {
                    _logger.LogDebug(_translation.GetConsole("meshgroup.using_all_team", player.Controller.PlayerName) ?? $"Using 'All' team MeshGroup config: {player.Controller.PlayerName}");
                }
            }
            
            if (string.IsNullOrEmpty(meshGroupsData))
                return;

            // 反序列化配置
            var meshGroups = DeserializeMeshGroups(meshGroupsData);
            if (meshGroups.Count == 0)
                return;

            // 应用每个组件
            foreach (var kvp in meshGroups)
            {
                var meshGroup = model.MeshGroups.FirstOrDefault(mg => mg.ComponentId == kvp.Key);
                if (meshGroup != null)
                {
                    ApplyMeshGroupToPlayer(player, meshGroup.BodyGroupName, kvp.Value);
                    _logger.LogDebug(_translation.GetConsole("meshgroup.loaded_applied", meshGroup.DisplayName, kvp.Value) ?? $"Loaded and applied MeshGroup: {meshGroup.DisplayName} Index={kvp.Value}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("meshgroup.load_apply_failed") ?? "Failed to load and apply MeshGroup config");
        }
    }

    /// <summary>
    /// 获取玩家当前的 MeshGroup 配置
    /// </summary>
    public Dictionary<string, int>? GetPlayerMeshGroups(ulong steamId)
    {
        return _playerMeshGroups.ContainsKey(steamId) ? _playerMeshGroups[steamId] : null;
    }

    /// <summary>
    /// 清除玩家的 MeshGroup 配置
    /// </summary>
    public void ClearPlayerMeshGroups(ulong steamId)
    {
        _playerMeshGroups.Remove(steamId);
    }

    /// <summary>
    /// 序列化 MeshGroup 配置为数据库格式
    /// 格式: head:1;hair:0;body:5
    /// </summary>
    public string SerializeMeshGroups(Dictionary<string, int> meshGroups)
    {
        if (meshGroups == null || meshGroups.Count == 0)
            return string.Empty;

        var parts = meshGroups.Select(kvp => $"{kvp.Key}:{kvp.Value}");
        return string.Join(";", parts);
    }

    /// <summary>
    /// 从数据库格式反序列化 MeshGroup 配置
    /// 格式: head:1;hair:0;body:5
    /// </summary>
    public Dictionary<string, int> DeserializeMeshGroups(string meshGroupsData)
    {
        var result = new Dictionary<string, int>();

        if (string.IsNullOrWhiteSpace(meshGroupsData))
            return result;

        try
        {
            var parts = meshGroupsData.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var keyValue = part.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (keyValue.Length == 2)
                {
                    var componentId = keyValue[0].Trim();
                    if (int.TryParse(keyValue[1].Trim(), out int index))
                    {
                        result[componentId] = index;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("meshgroup.deserialize_failed", meshGroupsData) ?? $"Failed to deserialize MeshGroup data: {meshGroupsData}");
        }

        return result;
    }

    /// <summary>
    /// 获取玩家当前的 MeshGroup 配置并序列化为数据库格式
    /// </summary>
    public string GetPlayerMeshGroupsData(ulong steamId)
    {
        var meshGroups = GetPlayerMeshGroups(steamId);
        return meshGroups != null ? SerializeMeshGroups(meshGroups) : string.Empty;
    }
}