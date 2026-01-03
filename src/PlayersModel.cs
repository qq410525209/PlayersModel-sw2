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
        private IServiceProvider? _serviceProvider;
        private IDatabaseService? _databaseService;
        private IModelService? _modelService;

        public PlayersModel(ISwiftlyCore core) : base(core)
        {
        }

        public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
        {
            // 如果需要对外暴露接口,在这里配置
        }

        public override void UseSharedInterface(IInterfaceManager interfaceManager)
        {
            // 获取经济系统 API
            if (interfaceManager.HasSharedInterface("Economy.API.v1"))
            {
                _economyAPI = interfaceManager.GetSharedInterface<IEconomyAPIv1>("Economy.API.v1");
                Console.WriteLine("[PlayersModel] ✓ 成功连接到经济系统!");
            }
            else
            {
                Console.WriteLine("[PlayersModel] ⚠ 警告: 未找到经济系统插件,购买模型功能将不可用!");
            }
        }

        public override void Load(bool hotReload)
        {
            Console.WriteLine("[PlayersModel] 开始加载插件...");

            try
            {
                // 初始化配置
                InitializeConfiguration();

                // 初始化依赖注入
                InitializeDependencyInjection();

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

                Console.WriteLine("[PlayersModel] ✓ 插件加载成功!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayersModel] ✗ 插件加载失败: {ex.Message}");
                Console.WriteLine($"[PlayersModel] 堆栈跟踪: {ex.StackTrace}");
            }
        }

        public override void Unload()
        {
            Console.WriteLine("[PlayersModel] 插件已卸载");
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

            Console.WriteLine("[PlayersModel] ✓ 配置文件初始化完成");
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

            Console.WriteLine("[PlayersModel] ✓ 依赖注入初始化完成");
        }

        /// <summary>
        /// 初始化经济系统
        /// </summary>
        private void InitializeEconomy()
        {
            if (_economyAPI != null && _modelService != null)
            {
                // 将经济 API 传递给模型服务
                if (_modelService is ModelService modelService)
                {
                    modelService.SetEconomyAPI(_economyAPI);
                }

                // 确保钱包类型存在
                var config = _serviceProvider?.GetRequiredService<IOptionsMonitor<PluginConfig>>();
                if (config != null)
                {
                    var walletKind = config.CurrentValue.WalletKind;
                    _economyAPI.EnsureWalletKind(walletKind);
                    Console.WriteLine($"[PlayersModel] ✓ 已初始化钱包类型: {walletKind}");
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
                Console.WriteLine("[PlayersModel] ✓ 数据库初始化完成");
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
                Console.WriteLine($"[PlayersModel] ✓ 已加载 {models.Count} 个模型");
            }
        }

        /// <summary>
        /// 注册事件
        /// </summary>
        private void RegisterEvents()
        {
            // 事件处理将在单独的文件中实现
            Console.WriteLine("[PlayersModel] ✓ 事件监听器已注册");
        }
    }
}