using PlayersModel.Config;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;
using System.Text.Json;

namespace PlayersModel.Services;

/// <summary>
/// 翻译服务接口
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// 获取翻译文本（用于玩家消息）
    /// </summary>
    string Get(string key, IPlayer? player = null);

    /// <summary>
    /// 获取带参数的翻译文本（用于玩家消息）
    /// </summary>
    string Get(string key, IPlayer? player, params object[] args);
    
    /// <summary>
    /// 获取控制台消息翻译（不依赖玩家）
    /// </summary>
    string GetConsole(string key);
    
    /// <summary>
    /// 获取带参数的控制台消息翻译
    /// </summary>
    string GetConsole(string key, params object[] args);
    
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
    private Dictionary<string, string>? _consoleTranslations;
    private string? _currentLanguage;

    public TranslationService(
        ISwiftlyCore core,
        IOptionsMonitor<PluginConfig> config)
    {
        _core = core;
        _config = config;
        LoadConsoleTranslations();
    }

    /// <summary>
    /// 加载控制台翻译
    /// </summary>
    private void LoadConsoleTranslations()
    {
        try
        {
            var configLanguage = _config.CurrentValue.Language;
            
            // 确定使用的语言
            string language;
            if (!string.IsNullOrEmpty(configLanguage))
            {
                language = configLanguage;
            }
            else
            {
                // 如果配置为空，尝试从框架获取默认语言
                // 默认使用英文
                language = "en";
            }
            
            _currentLanguage = language;
            
            // 加载翻译文件
            var translationPath = Path.Combine("translations", $"{language}.jsonc");
            
            if (File.Exists(translationPath))
            {
                var jsonContent = File.ReadAllText(translationPath);
                // 移除JSONC注释（简单处理）
                var lines = jsonContent.Split('\n');
                var cleanedLines = lines.Where(line => !line.TrimStart().StartsWith("//")).ToArray();
                var cleanedJson = string.Join("\n", cleanedLines);
                
                _consoleTranslations = JsonSerializer.Deserialize<Dictionary<string, string>>(cleanedJson);
            }
            else
            {
                Console.WriteLine($"[PlayersModel] Warning: Translation file not found: {translationPath}");
                _consoleTranslations = new Dictionary<string, string>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayersModel] Error loading translations: {ex.Message}");
            _consoleTranslations = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// 获取控制台消息翻译
    /// </summary>
    public string GetConsole(string key)
    {
        if (_consoleTranslations == null || !_consoleTranslations.ContainsKey(key))
        {
            return key; // 如果找不到翻译，返回键名
        }
        
        return _consoleTranslations[key];
    }

    /// <summary>
    /// 获取带参数的控制台消息翻译
    /// </summary>
    public string GetConsole(string key, params object[] args)
    {
        var template = GetConsole(key);
        
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template; // 如果格式化失败，返回模板
        }
    }

    /// <summary>
    /// 获取玩家的本地化器
    /// </summary>
    public ILocalizer GetPlayerLocalizer(IPlayer player)
    {
        var configLanguage = _config.CurrentValue.Language;
        
        // 如果配置中指定了语言，所有玩家使用同一语言
        if (!string.IsNullOrEmpty(configLanguage))
        {
            // 注意：这里可能需要框架提供获取指定语言本地化器的API
            // 目前仍使用玩家的语言设置
        }

        // 使用玩家的游戏语言设置
        return _core.Translation.GetPlayerLocalizer(player);
    }

    /// <summary>
    /// 获取翻译文本（用于玩家消息）
    /// </summary>
    public string Get(string key, IPlayer? player = null)
    {
        var configLanguage = _config.CurrentValue.Language;
        
        // 如果配置了语言，所有消息使用配置的语言
        if (!string.IsNullOrEmpty(configLanguage))
        {
            return GetConsole(key);
        }
        
        if (player == null)
        {
            return GetConsole(key);
        }

        var localizer = GetPlayerLocalizer(player);
        var translation = localizer[key];
        
        // 如果翻译不存在，返回控制台翻译或键名
        return string.IsNullOrEmpty(translation) ? GetConsole(key) : translation;
    }

    /// <summary>
    /// 获取带参数的翻译文本（用于玩家消息）
    /// </summary>
    public string Get(string key, IPlayer? player, params object[] args)
    {
        var configLanguage = _config.CurrentValue.Language;
        
        // 如果配置了语言，所有消息使用配置的语言
        if (!string.IsNullOrEmpty(configLanguage))
        {
            return GetConsole(key, args);
        }
        
        if (player == null)
        {
            return GetConsole(key, args);
        }

        var localizer = GetPlayerLocalizer(player);
        
        try
        {
            var translation = localizer[key, args];
            return string.IsNullOrEmpty(translation) ? GetConsole(key, args) : translation;
        }
        catch
        {
            // 如果格式化失败，使用控制台翻译
            return GetConsole(key, args);
        }
    }
}