using PlayersModel.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Sounds;
using SwiftlyS2.Shared.GameEventDefinitions;

namespace PlayersModel.Services;

public interface IMenuService
{
    void OpenMainMenu(IPlayer player);
}

public class MenuService : IMenuService
{
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<PluginConfig> _config;
    private readonly IModelService _modelService;
    private readonly IDatabaseService _databaseService;
    private readonly ITranslationService _translation;
    private readonly IPreviewService _previewService;
    private readonly ILogger<MenuService> _logger;
    private readonly IModelCacheService _modelCache;
    private readonly IMeshGroupService _meshGroupService;

    // 菜单标题属性 - 使用索引器简化访问
    public string MenuTitle
    {
        get
        {
            var result = _translation["menu.main.title"];
            Console.WriteLine($"[MenuService DEBUG] MenuTitle getter called, result: '{result}'");
            return result;
        }
    }
    public string MenuTitleCT => _translation["menu.ct_models.title"];
    public string MenuTitleT => _translation["menu.t_models.title"];
    public string MenuTitleAll => _translation["menu.all_models.title"];
    public string MenuTitleOwned => _translation["menu.owned_models.title"];
    
    // 菜单选项属性
    public string OptionCT => _translation["menu.option.ct_models"];
    public string OptionT => _translation["menu.option.t_models"];
    public string OptionAll => _translation["menu.option.all_models"];
    public string OptionOwned => _translation["menu.option.owned_models"];
    public string OptionNoModels => _translation["menu.option.no_models"];
    public string OptionPreview => _translation["menu.option.preview"];
    public string OptionEquip => _translation["menu.option.equip_model"];
    public string OptionUnequip => _translation["menu.option.unequip_model"];
    public string OptionEquipped => _translation["menu.option.equipped"];
    public string OptionOwned2 => _translation["menu.option.owned"];

    public MenuService(
        ISwiftlyCore core,
        IOptionsMonitor<PluginConfig> config,
        IModelService modelService,
        IDatabaseService databaseService,
        ITranslationService translationService,
        IPreviewService previewService,
        ILogger<MenuService> logger,
        IModelCacheService modelCache,
        IMeshGroupService meshGroupService)
    {
        _core = core;
        _config = config;
        _modelService = modelService;
        _databaseService = databaseService;
        _translation = translationService;
        _previewService = previewService;
        _logger = logger;
        _modelCache = modelCache;
        _meshGroupService = meshGroupService;
    }

    public void OpenMainMenu(IPlayer player)
    {
        var menuConfig = _config.CurrentValue.Menu;

        var builder = _core.MenusAPI
            .CreateBuilder()
            .SetPlayerFrozen(menuConfig.FreezePlayer)
            .Design.SetMenuTitle(MenuTitle)
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (menuConfig.EnableSound) builder.EnableSound();
        if (menuConfig.AutoCloseDelay > 0) builder.SetAutoCloseDelay(menuConfig.AutoCloseDelay);

        // 使用SubmenuMenuOption - 框架自动处理返回
        builder.AddOption(new SubmenuMenuOption(OptionAll, () => Task.FromResult(BuildModelCategoryMenu(player, "All"))));
        builder.AddOption(new SubmenuMenuOption(OptionCT, () => Task.FromResult(BuildModelCategoryMenu(player, "CT"))));
        builder.AddOption(new SubmenuMenuOption(OptionT, () => Task.FromResult(BuildModelCategoryMenu(player, "T"))));
        builder.AddOption(new SubmenuMenuOption(OptionOwned, () => BuildOwnedModelsMenuAsync(player)));

        _core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private IMenuAPI BuildModelCategoryMenu(IPlayer player, string team)
    {
        var menuConfig = _config.CurrentValue.Menu;
        
        var title = team.ToLower() switch
        {
            "ct" => MenuTitleCT,
            "t" => MenuTitleT,
            "all" => MenuTitleAll,
            _ => MenuTitle
        };
        
        var builder = _core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle(title)
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (menuConfig.EnableSound) builder.EnableSound();

        var models = _modelService.GetAvailableModelsForPlayer(player, team);
        foreach (var model in models)
        {
            var capturedId = model.ModelId;
            // 子菜单：使用SubmenuMenuOption自动处理返回
            builder.AddOption(new SubmenuMenuOption(model.DisplayName, () => BuildModelDetailMenuAsync(player, capturedId)));
        }

        return builder.Build();
    }

    private async Task<IMenuAPI> BuildOwnedModelsMenuAsync(IPlayer player)
    {
        var menuConfig = _config.CurrentValue.Menu;

        var builder = _core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle(MenuTitleOwned)
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (menuConfig.EnableSound) builder.EnableSound();

        // 获取All、CT和T三个槽位的装备模型
        var currentModelAll = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID, "All");
        var currentModelCT = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID, "CT");
        var currentModelT = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID, "T");
        
        var ownedModelIds = await _databaseService.GetPlayerOwnedModelsAsync(player.SteamID);

        if (ownedModelIds.Count == 0)
        {
            builder.AddOption(new TextMenuOption(OptionNoModels));
        }
        else
        {
            foreach (var modelId in ownedModelIds)
            {
                var model = _modelService.GetModelById(modelId);
                if (model == null) continue;

                var capturedId = modelId;
                
                // 检查是否在All、CT或T槽位装备了此模型
                bool isEquippedAll = currentModelAll.modelPath == model.ModelPath;
                bool isEquippedCT = currentModelCT.modelPath == model.ModelPath;
                bool isEquippedT = currentModelT.modelPath == model.ModelPath;
                
                string displayName;
                if (isEquippedAll)
                    displayName = $"✓(All) {model.DisplayName}"; // All槽位装备
                else if (isEquippedCT && isEquippedT)
                    displayName = $"✓✓ {model.DisplayName}"; // CT和T都装备
                else if (isEquippedCT)
                    displayName = $"✓(CT) {model.DisplayName}"; // 只CT装备
                else if (isEquippedT)
                    displayName = $"✓(T) {model.DisplayName}"; // 只T装备
                else
                    displayName = $"  {model.DisplayName}"; // 未装备
                
                builder.AddOption(new SubmenuMenuOption(displayName, () => BuildModelDetailMenuAsync(player, capturedId)));
            }
        }

        return builder.Build();
    }

    private async Task<IMenuAPI> BuildModelDetailMenuAsync(IPlayer player, string modelId)
    {
        var menuConfig = _config.CurrentValue.Menu;
        var model = _modelService.GetModelById(modelId);
        if (model == null) 
        {
            // 返回空菜单或错误菜单
            var errorBuilder = _core.MenusAPI.CreateBuilder();
            errorBuilder.Design.SetMenuTitle("Error");
            errorBuilder.AddOption(new TextMenuOption("Model not found"));
            return errorBuilder.Build();
        }

        // 标题只显示名称和阵营
        var titleWithTeam = $"📦 {model.DisplayName} [{model.Team}]";
        
        var builder = _core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle(titleWithTeam)
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (menuConfig.EnableSound) builder.EnableSound();

        
        // 添加描述作为第一个不可点击的菜单项
        builder.AddOption(new TextMenuOption(model.Description)
        {
            Enabled = false,
            PlaySound = false

        });


        var owns = await _databaseService.PlayerOwnsModelAsync(player.SteamID, modelId);
        
        // 根据模型的Team属性检查对应槽位的装备状态
        // All类型模型检查All槽位，CT检查CT槽位，T检查T槽位
        var currentModel = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID, model.Team);
        bool isEquipped = currentModel.modelPath == model.ModelPath;
        
        if (owns)
        {
            var statusText = isEquipped ? OptionEquipped : OptionOwned2;
            builder.AddOption(new TextMenuOption(statusText));
        }

        // 预览按钮 - ButtonMenuOption，执行后不关闭菜单
        var previewButton = new ButtonMenuOption(OptionPreview);
        previewButton.Click += async (sender, args) => 
        {
            _previewService.ShowPreview(args.Player!, model.ModelPath);
        };
        builder.AddOption(previewButton);

        // 根据状态显示不同按钮 - ButtonMenuOption，操作完成后菜单保持打开
        if (isEquipped)
        {
            var unequipButton = new ButtonMenuOption(OptionUnequip);
            unequipButton.Click += async (sender, args) =>
            {
                await UnequipModelAsync(args.Player!, model.Team);
                // 刷新当前菜单，显示更新后的状态
                var refreshedMenu = await BuildModelDetailMenuAsync(args.Player!, modelId);
                _core.Scheduler.DelayBySeconds(0.1f, () =>
                {
                    _core.MenusAPI.OpenMenuForPlayer(args.Player!, refreshedMenu);
                });
            };
            builder.AddOption(unequipButton);
        }
        else if (owns || model.Price == 0)
        {
            var equipButton = new ButtonMenuOption(OptionEquip);
            equipButton.Click += async (sender, args) =>
            {
                var success = _modelService.ApplyModelToPlayer(args.Player!, modelId);
                if (success)
                {
                    _logger.LogInformation(_translation.GetConsole("menuservice.player_equipped", args.Player!.Controller.PlayerName, model.DisplayName));
                }
                // 刷新当前菜单，显示更新后的状态
                var refreshedMenu = await BuildModelDetailMenuAsync(args.Player!, modelId);
                _core.Scheduler.DelayBySeconds(0.1f, () =>
                {
                    _core.MenusAPI.OpenMenuForPlayer(args.Player!, refreshedMenu);
                });
            };
            builder.AddOption(equipButton);
        }
        else
        {
            var walletKind = _config.CurrentValue.WalletKind;
            var buyText = string.Format(_translation["menu.option.buy_model"], $"{model.Price} {walletKind}");
            var buyButton = new ButtonMenuOption(buyText);
            buyButton.Click += async (sender, args) =>
            {
                var (success, message) = await _modelService.PurchaseModelAsync(args.Player!, modelId);
                _logger.LogInformation($"{message}");
                // 如果购买成功，刷新菜单显示装备按钮
                if (success)
                {
                    var refreshedMenu = await BuildModelDetailMenuAsync(args.Player!, modelId);
                    _core.Scheduler.DelayBySeconds(0.1f, () =>
                    {
                        _core.MenusAPI.OpenMenuForPlayer(args.Player!, refreshedMenu);
                    });
                }
            };
            builder.AddOption(buyButton);
        }

        // 如果模型有 MeshGroup 配置且玩家拥有该模型，显示组件管理按钮
        if (owns && model.MeshGroups != null && model.MeshGroups.Count > 0)
        {
            var componentButton = new SubmenuMenuOption("⚙️ 组件管理", () => BuildMeshGroupMenuAsync(player, modelId));
            builder.AddOption(componentButton);
        }

        return builder.Build();
    }

    private async Task<IMenuAPI> BuildMeshGroupMenuAsync(IPlayer player, string modelId)
    {
        var menuConfig = _config.CurrentValue.Menu;
        var model = _modelService.GetModelById(modelId);
        
        var builder = _core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle($"⚙️ {model?.DisplayName} - 组件管理")
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (menuConfig.EnableSound) builder.EnableSound();

        if (model?.MeshGroups != null)
        {
            foreach (var meshGroup in model.MeshGroups)
            {
                var capturedComponentId = meshGroup.ComponentId;
                var capturedModelId = modelId;
                
                // 获取当前选择的选项
                var playerMeshGroups = _meshGroupService.GetPlayerMeshGroups(player.SteamID);
                int currentIndex;
                if (playerMeshGroups != null && playerMeshGroups.ContainsKey(meshGroup.ComponentId))
                {
                    currentIndex = playerMeshGroups[meshGroup.ComponentId];
                }
                else
                {
                    currentIndex = meshGroup.Options.FirstOrDefault(o => o.IsDefault)?.Index ?? 0;
                }
                
                var currentOption = meshGroup.Options.FirstOrDefault(o => o.Index == currentIndex);
                var displayName = currentOption != null 
                    ? $"{meshGroup.DisplayName}: {currentOption.DisplayName}"
                    : $"{meshGroup.DisplayName}";
                
                builder.AddOption(new SubmenuMenuOption(displayName, 
                    () => BuildMeshGroupOptionsMenuAsync(player, capturedModelId, capturedComponentId)));
            }
        }

        return builder.Build();
    }

    private async Task<IMenuAPI> BuildMeshGroupOptionsMenuAsync(IPlayer player, string modelId, string componentId)
    {
        var menuConfig = _config.CurrentValue.Menu;
        var model = _modelService.GetModelById(modelId);
        var meshGroup = model?.MeshGroups?.FirstOrDefault(mg => mg.ComponentId == componentId);
        
        var builder = _core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle($"🔧 {meshGroup?.DisplayName}")
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (menuConfig.EnableSound) builder.EnableSound();

        if (meshGroup != null)
        {
            foreach (var option in meshGroup.Options)
            {
                var capturedOptionId = option.OptionId;
                var button = new ButtonMenuOption(option.DisplayName);
                button.Click += async (sender, args) =>
                {
                    await _meshGroupService.ToggleMeshGroupOption(args.Player!, modelId, componentId, capturedOptionId);
                };
                builder.AddOption(button);
            }
        }

        return builder.Build();
    }

    private async Task UnequipModelAsync(IPlayer player, string team)
    {
        // 删除数据库中对应槽位的记录
        await _databaseService.DeletePlayerCurrentModelAsync(player.SteamID, team);
        
        // 更新缓存：清除该槽位的模型数据
        _modelCache.UpdatePlayerCache(player.SteamID, team, null, null);
        
        _logger.LogInformation(_translation.GetConsole("menuservice.player_unequipped", player.Controller.PlayerName, team));
        
        // 卸载后，重新按优先级查找并应用模型
        if (player.Pawn?.IsValid == true)
        {
            var currentTeam = player.Controller.TeamNum;
            var teamName = currentTeam == 2 ? "T" : currentTeam == 3 ? "CT" : "";
            
            if (!string.IsNullOrEmpty(teamName))
            {
                string modelPathToApply = "";
                
                // 优先级系统：All > CT/T > 默认
                // 1. 先检查All槽位
                var allModelData = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID, "All");
                if (!string.IsNullOrEmpty(allModelData.modelPath))
                {
                    modelPathToApply = allModelData.modelPath;
                }
                else
                {
                    // 2. 检查当前阵营槽位
                    var teamModelData = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID, teamName);
                    if (!string.IsNullOrEmpty(teamModelData.modelPath))
                    {
                        modelPathToApply = teamModelData.modelPath;
                    }
                }
                
                // 3. 如果都没有，使用默认模型
                if (string.IsNullOrEmpty(modelPathToApply))
                {
                    modelPathToApply = teamName == "CT" 
                        ? _config.CurrentValue.DefaultCTModelPath 
                        : _config.CurrentValue.DefaultTModelPath;
                }
                
                // 应用模型
                var pawn = player.Pawn;
                var pathToApply = modelPathToApply;
                _core.Scheduler.DelayBySeconds(0.01f, () =>
                {
                    if (pawn?.IsValid == true)
                    {
                        pawn.SetModel(pathToApply);
                    }
                });
            }
        }
    }
}