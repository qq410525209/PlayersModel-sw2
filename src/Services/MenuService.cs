using PlayersModel.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Core.Menus.OptionsBase;

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
        ILogger<MenuService> logger)
    {
        _core = core;
        _config = config;
        _modelService = modelService;
        _databaseService = databaseService;
        _translation = translationService;
        _previewService = previewService;
        _logger = logger;
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

        var builder = _core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle($"📦 {model.DisplayName}")
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (menuConfig.EnableSound) builder.EnableSound();

        // 模型信息
        builder.AddOption(new TextMenuOption(model.Description)
        {
            Enabled = false,
            PlaySound = false
        }
        );
        builder.AddOption(new TextMenuOption($"{_translation["model.team"]}: {model.Team}")
        {
            Enabled = false,
            PlaySound = false
        }
        );
        
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
                // 操作完成，用户可按返回键回到模型列表
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
                // 操作完成，用户可按返回键回到模型列表
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
                // 操作完成，用户可按返回键回到模型列表
            };
            builder.AddOption(buyButton);
        }

        return builder.Build();
    }

    private async Task UnequipModelAsync(IPlayer player, string team)
    {
        // 删除数据库中对应槽位的记录
        // 不保存默认模型到数据库，默认模型只在查询不到记录时使用
        await _databaseService.DeletePlayerCurrentModelAsync(player.SteamID, team);
        
        _logger.LogInformation(_translation.GetConsole("menuservice.player_unequipped", player.Controller.PlayerName, team));
        
        // 如果玩家在线，需要重新应用模型（按优先级）
        if (player.Pawn?.IsValid == true)
        {
            var currentTeam = player.Controller.TeamNum;
            var teamName = currentTeam == 2 ? "T" : currentTeam == 3 ? "CT" : "";
            
            if (!string.IsNullOrEmpty(teamName))
            {
                string modelPathToApply = "";
                
                // 卸载后按优先级查找模型：
                // 1. 如果卸载的是All，检查当前阵营槽位是否有模型
                // 2. 如果卸载的是CT/T，不需要检查（因为已经删除了）
                // 3. 如果都没有，使用默认模型
                
                if (team.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    // 卸载All后，检查当前阵营槽位
                    var teamModelData = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID, teamName);
                    if (!string.IsNullOrEmpty(teamModelData.modelPath))
                    {
                        modelPathToApply = teamModelData.modelPath;
                    }
                }
                
                // 如果没有找到模型，使用默认模型
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