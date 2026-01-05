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
    void ApplyDefaultMeshGroups(IPlayer player, PlayerModelConfig model);

    /// <summary>
    /// 获取模型的所有 MeshGroup 配置
    /// </summary>
    List<MeshGroupConfig>? GetModelMeshGroups(string modelId);

    /// <summary>
    /// 切换 MeshGroup 选项
    /// </summary>
    bool ToggleMeshGroupOption(IPlayer player, string modelId, string componentId, string optionId);
}

/// <summary>
/// MeshGroup 组件服务实现
/// </summary>
public class MeshGroupService : IMeshGroupService
{
    private readonly ISwiftlyCore _core;
    private readonly IModelService _modelService;
    private readonly ILogger<MeshGroupService> _logger;
    private readonly ITranslationService _translation;

    // 存储玩家当前装备的 MeshGroup 选项
    // Key: SteamID, Value: Dictionary<ComponentId, OptionId>
    private readonly Dictionary<ulong, Dictionary<string, string>> _playerMeshGroups = new();

    public MeshGroupService(
        ISwiftlyCore core,
        IModelService modelService,
        ILogger<MeshGroupService> logger,
        ITranslationService translation)
    {
        _core = core;
        _modelService = modelService;
        _logger = logger;
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
                _logger.LogWarning($"无法应用 MeshGroup: 玩家 {player.Controller.PlayerName} 的 Pawn 无效");
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
                    _logger.LogDebug($"已应用 MeshGroup: {bodyGroupName} = {index} 到玩家 {player.Controller.PlayerName}");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"应用 MeshGroup 失败: {bodyGroupName}");
        }
    }

    /// <summary>
    /// 为模型应用所有默认 MeshGroup
    /// </summary>
    public void ApplyDefaultMeshGroups(IPlayer player, PlayerModelConfig model)
    {
        if (model.MeshGroups == null || model.MeshGroups.Count == 0)
            return;

        try
        {
            var steamId = player.SteamID;
            
            // 初始化玩家的 MeshGroup 配置
            if (!_playerMeshGroups.ContainsKey(steamId))
            {
                _playerMeshGroups[steamId] = new Dictionary<string, string>();
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
                    _playerMeshGroups[steamId][meshGroup.ComponentId] = defaultOption.OptionId;
                    
                    _logger.LogInformation($"为玩家 {player.Controller.PlayerName} 应用默认 MeshGroup: {meshGroup.DisplayName} -> {defaultOption.DisplayName}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"应用默认 MeshGroup 失败，模型: {model.ModelId}");
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
    public bool ToggleMeshGroupOption(IPlayer player, string modelId, string componentId, string optionId)
    {
        try
        {
            var model = _modelService.GetModelById(modelId);
            if (model == null)
            {
                _logger.LogWarning($"模型未找到: {modelId}");
                return false;
            }

            var meshGroup = model.MeshGroups?.FirstOrDefault(mg => mg.ComponentId == componentId);
            if (meshGroup == null)
            {
                _logger.LogWarning($"MeshGroup 组件未找到: {componentId}");
                return false;
            }

            var option = meshGroup.Options.FirstOrDefault(o => o.OptionId == optionId);
            if (option == null)
            {
                _logger.LogWarning($"MeshGroup 选项未找到: {optionId}");
                return false;
            }

            // 应用选项
            ApplyMeshGroupToPlayer(player, meshGroup.BodyGroupName, option.Index);

            // 记录玩家选择
            var steamId = player.SteamID;
            if (!_playerMeshGroups.ContainsKey(steamId))
            {
                _playerMeshGroups[steamId] = new Dictionary<string, string>();
            }
            _playerMeshGroups[steamId][componentId] = optionId;

            _logger.LogInformation($"玩家 {player.Controller.PlayerName} 切换 MeshGroup: {meshGroup.DisplayName} -> {option.DisplayName}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"切换 MeshGroup 选项失败");
            return false;
        }
    }

    /// <summary>
    /// 获取玩家当前的 MeshGroup 配置
    /// </summary>
    public Dictionary<string, string>? GetPlayerMeshGroups(ulong steamId)
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
}