using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;
using Economy.Contract;
using Cookies.Contract;
using PlayersModel.Config;
using PlayersModel.Services;

namespace PlayersModel
{
    [PluginMetadata(
        Id = "PlayersModel", 
        Version = "1.1", 
        Name = "PlayersModel", 
        Author = "小彩旗", 
        Description = "Player Model Plugins with Economy Integration"
    )]
    public partial class PlayersModel : BasePlugin
    {
        private IEconomyAPIv1? _economyAPI;
        private IPlayerCookiesAPIv1? _cookiesAPI;
        private IInterfaceManager? _interfaceManager;
    private IServiceProvider? _serviceProvider;
    private IDatabaseService? _databaseService;
    private IModelService? _modelService;
    private IModelCacheService? _modelCacheService;
    private IMeshGroupService? _meshGroupService;
    private ITranslationService? _translationService;
    private ICreditsService? _creditsService;
    private ICookiesService? _cookiesService;

        /// <summary>
        /// 获取格式化的插件前缀 [PluginName]
        /// </summary>
        private string PluginPrefix
        {
            get
            {
                var metadata = (PluginMetadata?)Attribute.GetCustomAttribute(GetType(), typeof(PluginMetadata));
                return $"[{metadata?.Name ?? "PlayersModel"}]";
            }
        }

        public PlayersModel(ISwiftlyCore core) : base(core)
        {
        }

        public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
        {
            // 如果需要对外暴露接口,在这里配置
        }

        public override void UseSharedInterface(IInterfaceManager interfaceManager)
        {
            _interfaceManager = interfaceManager;
            
            try
            {
                if (interfaceManager.HasSharedInterface("Economy.API.v1"))
                {
                    _economyAPI = interfaceManager.GetSharedInterface<IEconomyAPIv1>("Economy.API.v1");
                    Console.WriteLine($"{PluginPrefix} ✓ Economy API loaded successfully");
                }
                else
                {
                    Console.WriteLine($"{PluginPrefix} ⚠ Economy API not found");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{PluginPrefix} ⚠ Warning: Failed to load Economy API: {ex.Message}");
                _economyAPI = null;
            }

            try
            {
                Console.WriteLine($"{PluginPrefix} [DEBUG] Checking for Cookies API...");
                if (interfaceManager.HasSharedInterface("Cookies.Player.v1"))
                {
                    _cookiesAPI = interfaceManager.GetSharedInterface<IPlayerCookiesAPIv1>("Cookies.Player.v1");
                    Console.WriteLine($"{PluginPrefix} ✓ Cookies API loaded successfully");
                }
                else
                {
                    Console.WriteLine($"{PluginPrefix} ⚠ Cookies API not found (interface 'Cookies.Player.v1' not available)");
                    Console.WriteLine($"{PluginPrefix} [DEBUG] This is OK - Cookies plugin may not be installed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{PluginPrefix} ⚠ Warning: Failed to load Cookies API: {ex.Message}");
                _cookiesAPI = null;
            }
        }

        public override void Load(bool hotReload)
        {
            Console.WriteLine($"{PluginPrefix} Loading plugin...");

            try
            {
                // 初始化配置
                InitializeConfiguration();

                // 初始化依赖注入
                InitializeDependencyInjection();
                
                // 获取翻译服务
                _translationService = _serviceProvider?.GetRequiredService<ITranslationService>();

                // 初始化经济系统
                InitializeEconomy();
                
                // 初始化 Cookies 系统
                InitializeCookies();

                // 初始化数据库
                InitializeDatabaseAsync().Wait();

                // 加载模型配置
                LoadModels();

                // 注册命令
                RegisterCommands();

                // 注册事件
                InitializeEvents();

                // 启动定时收入系统
                _creditsService?.StartTimedIncomeSystem();

                // 为热重载时已经在线的玩家应用模型，或在插件启动时为所有玩家应用
                ApplyModelsToAllOnlinePlayers();

                Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("plugin.loaded") ?? "Plugin loaded successfully!"}");
                
                // Economy将在地图加载时连接（见Events.cs中的OnMapLoaded事件）
            }
            catch (Exception ex)
            {
                var errorMsg = _translationService?.GetConsole("error.load_failed", ex.Message) ?? $"Plugin load failed: {ex.Message}";
                var stackMsg = _translationService?.GetConsole("error.stack_trace", ex.StackTrace ?? "") ?? $"Stack trace: {ex.StackTrace ?? ""}";
                Console.WriteLine($"{PluginPrefix} {errorMsg}");
                Console.WriteLine($"{PluginPrefix} {stackMsg}");
            }
        }

        public override void Unload()
        {
            // 停止定时收入系统
            _creditsService?.StopTimedIncomeSystem();

            Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("plugin.unloaded") ?? "Plugin unloaded"}");
        }

        /// <summary>
        /// 初始化配置
        /// </summary>
        private void InitializeConfiguration()
        {
            // 初始化主配置文件
            Core.Configuration
                .InitializeWithTemplate("config.jsonc", "config.jsonc")
                .Configure(builder =>
                {
                    builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true);
                });

            // 初始化模型配置文件
            Core.Configuration
                .InitializeWithTemplate("models.jsonc", "models.jsonc")
                .Configure(builder =>
                {
                    builder.AddJsonFile("models.jsonc", optional: false, reloadOnChange: true);
                });

            Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.config_initialized") ?? "Configuration initialized"}");
        }

        /// <summary>
        /// 初始化依赖注入
        /// </summary>
        private void InitializeDependencyInjection()
        {
            var services = new ServiceCollection();

            // 添加 SwiftlyS2 核心服务
            services.AddSwiftly(Core);

            // 添加配置
            services.AddOptionsWithValidateOnStart<PluginConfig>()
                .BindConfiguration("Main");
            
            services.AddOptionsWithValidateOnStart<ModelConfigRoot>()
                .BindConfiguration("");

            // 添加日志
            services.AddLogging();

            // 注册服务
            services.AddSingleton<IDatabaseService, DatabaseService>();
            services.AddSingleton<IModelService, ModelService>();
            services.AddSingleton<IMeshGroupService, MeshGroupService>();
            services.AddSingleton<IModelCacheService, ModelCacheService>();
            services.AddSingleton<ITranslationService, TranslationService>();
            services.AddSingleton<IPreviewService, PreviewService>();
            services.AddSingleton<IMenuService, MenuService>();
            services.AddSingleton<IModelHookService, ModelHookService>();
            services.AddSingleton<ICreditsService, CreditsService>();
            services.AddSingleton<ICookiesService, CookiesService>();

            _serviceProvider = services.BuildServiceProvider();

            // 获取服务实例
            _databaseService = _serviceProvider.GetRequiredService<IDatabaseService>();
            _modelService = _serviceProvider.GetRequiredService<IModelService>();
            _modelCacheService = _serviceProvider.GetRequiredService<IModelCacheService>();
            _meshGroupService = _serviceProvider.GetRequiredService<IMeshGroupService>();
            
            _creditsService = _serviceProvider.GetRequiredService<ICreditsService>();
            // 获取 CookiesService 实例
            _cookiesService = _serviceProvider.GetRequiredService<ICookiesService>();
            
            // 将 CookiesService 注入到 ModelService
            if (_modelService is ModelService modelService)
            {
                modelService.SetCookiesService(_cookiesService);
            }
            
            // 初始化模型Hook服务并注入到ModelService
            var modelHookService = _serviceProvider.GetRequiredService<IModelHookService>();
            _modelService.SetModelHookService(modelHookService);
            
            // 在事件初始化后启动Hook
            modelHookService.InitializeHooks();

            Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.di_initialized") ?? "Dependency injection initialized"}");
        }

        /// <summary>
        /// 初始化 Cookies 系统
        /// </summary>
        public void InitializeCookies()
        {
            if (_cookiesAPI != null && _cookiesService != null)
            {
                try
                {
                    _cookiesService.SetCookiesAPI(_cookiesAPI);
                    Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.cookies_api_passed") ?? "Cookies API successfully passed to CookiesService"}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{PluginPrefix} ⚠ Warning: Failed to initialize Cookies integration: {ex.Message}");
                }
            }
            else
            {
                if (_cookiesAPI == null)
                {
                    Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.cookies_warning") ?? "Warning: Cookies API is null, player model persistence may not work"}");
                }
                if (_cookiesService == null)
                {
                    Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.cookiesservice_warning") ?? "Warning: CookiesService is null"}");
                }
            }
        }

        /// <summary>
        /// 初始化经济系统
        /// </summary>
        public void InitializeEconomy()
        {
            if (_economyAPI != null && _modelService != null)
            {
                try
                {
                    // 将经济 API 传递给模型服务
                    if (_modelService is ModelService modelService)
                    {
                        modelService.SetEconomyAPI(_economyAPI);
                        Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.economy_api_passed") ?? "Economy API successfully passed to ModelService"}");
                         
                        // 将 MeshGroupService 传递给 ModelService
                        if (_meshGroupService != null)
                        {
                            modelService.SetMeshGroupService(_meshGroupService);
                            Console.WriteLine($"{PluginPrefix} MeshGroupService successfully passed to ModelService");
                        }
                    }
                    
                    // 将 Economy API 传递给 CreditsService
                    if (_creditsService is CreditsService creditsService)
                    {
                        creditsService.SetEconomyAPI(_economyAPI);
                        Console.WriteLine($"{PluginPrefix} Economy API successfully passed to CreditsService");
                    }

                    // 确保钱包类型存在
                    var config = _serviceProvider?.GetRequiredService<IOptionsMonitor<PluginConfig>>();
                    if (config != null && _economyAPI != null)
                    {
                        var walletKind = config.CurrentValue.WalletKind;
                        _economyAPI.EnsureWalletKind(walletKind);
                        Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.economy_wallet_initialized", walletKind) ?? $"Wallet type initialized: {walletKind}"}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{PluginPrefix} ⚠ Warning: Failed to fully initialize Economy integration: {ex.Message}");
                    _economyAPI = null;
                }
            }
            else
            {
                if (_economyAPI == null)
                {
                    Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.economy_warning") ?? "Warning: Economy API is null, purchase features unavailable"}");
                }
                if (_modelService == null)
                {
                    Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.modelservice_warning") ?? "Warning: ModelService is null"}");
                }
            }
        }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        private async Task InitializeDatabaseAsync()
        {
            if (_databaseService != null)
            {
                await _databaseService.InitializeTablesAsync();
                Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.database_initialized") ?? "Database initialized"}");
            }
        }

        /// <summary>
        /// 加载模型配置
        /// </summary>
        private void LoadModels()
        {
            if (_modelService != null)
            {
                _modelService.LoadModels();
                var models = _modelService.GetAllModels();
                Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.models_loaded", models.Count) ?? $"Loaded {models.Count} models"}");
            }
        }

        /// <summary>
        /// 注册事件
        /// </summary>
        private void RegisterEvents()
        {
            // 事件处理将在单独的文件中实现
            Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.events_registered") ?? "Event listeners registered"}");
        }

        /// <summary>
        /// 为所有在线玩家应用保存的模型（热重载或启动时）
        /// </summary>
        private void ApplyModelsToAllOnlinePlayers()
        {
            if (_modelCacheService == null)
                return;

            var allPlayers = Core.PlayerManager.GetAllValidPlayers().ToList();
            if (allPlayers.Count == 0)
                return;

            try
            {
                var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                
                // 批量加载所有玩家的缓存
                var steamIds = allPlayers.Select(p => p.SteamID).ToList();
                _modelCacheService.BatchLoadPlayerCachesAsync(steamIds).GetAwaiter().GetResult();
                
                logger?.LogInformation(
                    _translationService?.GetConsole("system.applying_models_to_players", allPlayers.Count.ToString()) 
                    ?? $"Applying saved models to {allPlayers.Count} online players..."
                );

                // 在下一个Tick应用模型给所有玩家
                Core.Scheduler.NextWorldUpdate(() =>
                {
                    foreach (var player in allPlayers)
                    {
                        if (player == null || !player.IsValid || player.Pawn?.IsValid != true)
                            continue;

                        try
                        {
                            // 获取玩家当前阵营
                            var currentTeam = player.Controller.TeamNum;
                            var teamName = currentTeam == 2 ? "T" : currentTeam == 3 ? "CT" : "";
                            
                            if (string.IsNullOrEmpty(teamName))
                                continue; // 不在T或CT队伍

                            // 从缓存获取应用的模型路径（优先级：All > CT/T）
                            var modelPathToApply = _modelCacheService.GetModelPathToApply(player.SteamID, teamName);
                            
                            // 如果缓存中没有，使用阵营默认模型
                            if (string.IsNullOrEmpty(modelPathToApply))
                            {
                                var config = _serviceProvider?.GetService<IOptionsMonitor<PluginConfig>>();
                                if (config != null)
                                {
                                    modelPathToApply = teamName == "CT" 
                                        ? config.CurrentValue.DefaultCTModelPath 
                                        : config.CurrentValue.DefaultTModelPath;
                                }
                            }
                            
                            // 应用模型
                            if (!string.IsNullOrEmpty(modelPathToApply) && player.Pawn?.IsValid == true)
                            {
                                player.Pawn.SetModel(modelPathToApply);
                                logger?.LogInformation(
                                    _translationService?.GetConsole("system.model_applied_on_startup", player.Controller.PlayerName)
                                    ?? $"Applied model on startup: {player.Controller.PlayerName}"
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, $"Failed to apply model on startup for player {player?.Controller.PlayerName}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                logger?.LogError(ex, "Failed to apply models to online players on startup");
            }
        }
    }
}