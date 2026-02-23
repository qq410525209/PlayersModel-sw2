using Cookies.Contract;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;

namespace PlayersModel.Services;

/// <summary>
/// Cookies 持久化服务 - 使用 Cookies 插件保存玩家模型选择
/// </summary>
public interface ICookiesService
{
    /// <summary>
    /// 设置 Cookies API（由主插件调用）
    /// </summary>
    void SetCookiesAPI(IPlayerCookiesAPIv1 cookiesAPI);
    
    /// <summary>
    /// 保存玩家当前装备的模型 ID 到 Cookies
    /// </summary>
    void SavePlayerModel(IPlayer player, string modelId);
    
    /// <summary>
    /// 从 Cookies 加载玩家保存的模型 ID
    /// </summary>
    string? LoadPlayerModel(IPlayer player);
    
    /// <summary>
    /// 清除玩家保存的模型
    /// </summary>
    void ClearPlayerModel(IPlayer player);
}

/// <summary>
/// Cookies 持久化服务实现
/// </summary>
public class CookiesService : ICookiesService
{
    private IPlayerCookiesAPIv1? _cookiesAPI;
    private readonly ILogger<CookiesService> _logger;
    private readonly ITranslationService _translation;
    
    // Cookies 的键名
    private const string PLAYER_MODEL_COOKIE_KEY = "playersmodel_current_model";

    public CookiesService(
        ILogger<CookiesService> logger,
        ITranslationService translation)
    {
        _logger = logger;
        _translation = translation;
    }

    /// <summary>
    /// 设置 Cookies API
    /// </summary>
    public void SetCookiesAPI(IPlayerCookiesAPIv1 cookiesAPI)
    {
        _cookiesAPI = cookiesAPI;
        _logger.LogInformation("Cookies API successfully injected into CookiesService");
    }

    /// <summary>
    /// 保存玩家当前装备的模型 ID 到 Cookies
    /// </summary>
    public void SavePlayerModel(IPlayer player, string modelId)
    {
        if (_cookiesAPI == null)
        {
            _logger.LogWarning("Cookies API not available, cannot save player model");
            return;
        }

        try
        {
            _cookiesAPI.Set(player, PLAYER_MODEL_COOKIE_KEY, modelId);
            _cookiesAPI.Save(player);
            
            var playerName = player.Controller?.PlayerName ?? "Unknown";
            _logger.LogDebug($"Saved model '{modelId}' for player {playerName} to Cookies");
        }
        catch (Exception ex)
        {
            var playerName = player.Controller?.PlayerName ?? "Unknown";
            _logger.LogError(ex, $"Failed to save player model to Cookies for {playerName}");
        }
    }

    /// <summary>
    /// 从 Cookies 加载玩家保存的模型 ID
    /// </summary>
    public string? LoadPlayerModel(IPlayer player)
    {
        if (_cookiesAPI == null)
        {
            _logger.LogWarning("Cookies API not available, cannot load player model");
            return null;
        }

        try
        {
            // 先加载玩家的 Cookies
            _cookiesAPI.Load(player);
            
            // 获取保存的模型 ID
            var modelId = _cookiesAPI.Get<string>(player, PLAYER_MODEL_COOKIE_KEY);
            
            if (!string.IsNullOrEmpty(modelId))
            {
                var playerName = player.Controller?.PlayerName ?? "Unknown";
                _logger.LogDebug($"Loaded model '{modelId}' for player {playerName} from Cookies");
                return modelId;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            var playerName = player.Controller?.PlayerName ?? "Unknown";
            _logger.LogError(ex, $"Failed to load player model from Cookies for {playerName}");
            return null;
        }
    }

    /// <summary>
    /// 清除玩家保存的模型
    /// </summary>
    public void ClearPlayerModel(IPlayer player)
    {
        if (_cookiesAPI == null)
        {
            _logger.LogWarning("Cookies API not available, cannot clear player model");
            return;
        }

        try
        {
            _cookiesAPI.Unset(player, PLAYER_MODEL_COOKIE_KEY);
            _cookiesAPI.Save(player);
            
            var playerName = player.Controller?.PlayerName ?? "Unknown";
            _logger.LogDebug($"Cleared saved model for player {playerName}");
        }
        catch (Exception ex)
        {
            var playerName = player.Controller?.PlayerName ?? "Unknown";
            _logger.LogError(ex, $"Failed to clear player model from Cookies for {playerName}");
        }
    }
}
