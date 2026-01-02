using PlayersModel.Config;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;

namespace PlayersModel.Services;

/// <summary>
/// 翻译服务接口
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// 获取翻译文本
    /// </summary>
    string Get(string key, IPlayer? player = null);

    /// <summary>
    /// 获取带参数的翻译文本
    /// </summary>
    string Get(string key, IPlayer? player, params object[] args);
    
    /// <summary>
    /// 获取玩家的本地化器
    /// </summary>
    ILocalizer GetPlayerLocalizer(IPlayer player);
}

/// <summary>
/// 翻译服务实现
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<PluginConfig> _config;

    public TranslationService(
        ISwiftlyCore core,
        IOptionsMonitor<PluginConfig> config)
    {
        _core = core;
        _config = config;
    }

    /// <summary>
    /// 获取玩家的本地化器
    /// </summary>
    public ILocalizer GetPlayerLocalizer(IPlayer player)
    {
        var configLanguage = _config.CurrentValue.Language;
        
        // 如果配置中指定了语言,获取该语言的本地化器
        if (!string.IsNullOrEmpty(configLanguage))
        {
            // TODO: 如果有指定语言的 API,使用指定语言
            // 目前使用玩家的语言设置
        }

        // 使用玩家的游戏语言设置
        return _core.Translation.GetPlayerLocalizer(player);
    }

    /// <summary>
    /// 获取翻译文本
    /// </summary>
    public string Get(string key, IPlayer? player = null)
    {
        if (player == null)
        {
            // 如果没有指定玩家,使用默认语言 (英文)
            // TODO: 可以改为配置的默认语言
            return key;
        }

        var localizer = GetPlayerLocalizer(player);
        var translation = localizer[key];
        
        // 如果翻译不存在,返回键名
        return string.IsNullOrEmpty(translation) ? key : translation;
    }

    /// <summary>
    /// 获取带参数的翻译文本
    /// </summary>
    public string Get(string key, IPlayer? player, params object[] args)
    {
        if (player == null)
        {
            return key;
        }

        var localizer = GetPlayerLocalizer(player);
        
        try
        {
            var translation = localizer[key, args];
            return string.IsNullOrEmpty(translation) ? key : translation;
        }
        catch
        {
            // 如果格式化失败,返回键名
            return key;
        }
    }
}