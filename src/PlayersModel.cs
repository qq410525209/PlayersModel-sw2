using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;
using Economy.Contract;
using PlayersModel.Config;
using PlayersModel.Services;

namespace PlayersModel
{
    [PluginMetadata(
        Id = "PlayersModel", 
        Version = "1.0.0", 
        Name = "PlayersModel", 
        Author = "小彩旗", 
        Description = "Player Model Plugins with Economy Integration"
    )]
    public partial class PlayersModel : BasePlugin
    {
        private IEconomyAPIv1? _economyAPI;
        private IInterfaceManager? _interfaceManager;
        private IServiceProvider? _serviceProvider;
        private IDatabaseService? _databaseService;
        private IModelService? _modelService;
        private ITranslationService? _translationService;

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
            
            // 注意：此时翻译服务还未初始化，使用硬编码消息
            if (interfaceManager.HasSharedInterface("Economy.API.v1"))
            {
                _economyAPI = interfaceManager.GetSharedInterface<IEconomyAPIv1>("Economy.API.v1");
                Console.WriteLine($"{PluginPrefix} ✓ Successfully connected to Economy system!");
            }
            else
            {
                Console.WriteLine($"{PluginPrefix} ⚠ Warning: Economy plugin not found, purchase features will be unavailable!");
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

                // 初始化数据库
                InitializeDatabaseAsync().Wait();

                // 加载模型配置
                LoadModels();

                // 注册命令
                RegisterCommands();

                // 注册事件
                InitializeEvents();

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
            services.AddSingleton<ITranslationService, TranslationService>();
            services.AddSingleton<IPreviewService, PreviewService>();
            services.AddSingleton<IMenuService, MenuService>();

            _serviceProvider = services.BuildServiceProvider();

            // 获取服务实例
            _databaseService = _serviceProvider.GetRequiredService<IDatabaseService>();
            _modelService = _serviceProvider.GetRequiredService<IModelService>();

            Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.di_initialized") ?? "Dependency injection initialized"}");
        }

        /// <summary>
        /// 初始化经济系统
        /// </summary>
        public void InitializeEconomy()
        {
            if (_economyAPI != null && _modelService != null)
            {
                // 将经济 API 传递给模型服务
                if (_modelService is ModelService modelService)
                {
                    modelService.SetEconomyAPI(_economyAPI);
                    Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.economy_api_passed") ?? "Economy API successfully passed to ModelService"}");
                }

                // 确保钱包类型存在
                var config = _serviceProvider?.GetRequiredService<IOptionsMonitor<PluginConfig>>();
                if (config != null)
                {
                    var walletKind = config.CurrentValue.WalletKind;
                    _economyAPI.EnsureWalletKind(walletKind);
                    Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.economy_wallet_initialized", walletKind) ?? $"Wallet type initialized: {walletKind}"}");
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
    }
}