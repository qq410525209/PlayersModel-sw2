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
    private readonly ITranslationService _translationService;
    private readonly IPreviewService _previewService;
    private readonly ILogger<MenuService> _logger;

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
        _translationService = translationService;
        _previewService = previewService;
        _logger = logger;
    }

    public void OpenMainMenu(IPlayer player)
    {
        var localizer = _translationService.GetPlayerLocalizer(player);
        var menuConfig = _config.CurrentValue.Menu;

        var builder = _core.MenusAPI
            .CreateBuilder()
            .SetPlayerFrozen(menuConfig.FreezePlayer)
            .Design.SetMenuTitle(localizer["menu.main.title"])
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (menuConfig.EnableSound) builder.EnableSound();
        if (menuConfig.AutoCloseDelay > 0) builder.SetAutoCloseDelay(menuConfig.AutoCloseDelay);

        // CT Models
        var ctButton = new ButtonMenuOption(localizer["menu.option.ct_models"]);
        ctButton.Click += async (sender, args) => OpenModelCategoryMenu(args.Player!, "CT");
        builder.AddOption(ctButton);

        // T Models  
        var tButton = new ButtonMenuOption(localizer["menu.option.t_models"]);
        tButton.Click += async (sender, args) => OpenModelCategoryMenu(args.Player!, "T");
        builder.AddOption(tButton);

        // My Models
        var myModelsButton = new ButtonMenuOption(localizer["menu.option.owned_models"]);
        myModelsButton.Click += async (sender, args) => await OpenOwnedModelsMenuAsync(args.Player!);
        builder.AddOption(myModelsButton);

        _core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private void OpenModelCategoryMenu(IPlayer player, string team, IMenuAPI? parentMenu = null)
    {
        var localizer = _translationService.GetPlayerLocalizer(player);
        var menuConfig = _config.CurrentValue.Menu;
        var titleKey = team == "CT" ? "menu.ct_models.title" : "menu.t_models.title";
        
        var builder = _core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle(localizer[titleKey])
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (parentMenu != null) builder.BindToParent(parentMenu);
        if (menuConfig.EnableSound) builder.EnableSound();

        var models = _modelService.GetAvailableModelsForPlayer(player, team);
        foreach (var model in models)
        {
            var displayName = model.Price > 0 ? $"{model.DisplayName} ({model.Price} credits)" : model.DisplayName;
            var button = new ButtonMenuOption(displayName);
            var capturedId = model.ModelId;
            button.Click += async (sender, args) => await OpenModelDetailMenuAsync(args.Player!, capturedId);
            builder.AddOption(button);
        }

        _core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private async Task OpenOwnedModelsMenuAsync(IPlayer player, IMenuAPI? parentMenu = null)
    {
        var localizer = _translationService.GetPlayerLocalizer(player);
        var menuConfig = _config.CurrentValue.Menu;

        var builder = _core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle(localizer["menu.owned_models.title"])
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (parentMenu != null) builder.BindToParent(parentMenu);
        if (menuConfig.EnableSound) builder.EnableSound();

        var ownedModelIds = await _databaseService.GetPlayerOwnedModelsAsync(player.SteamID);

        if (ownedModelIds.Count == 0)
        {
            builder.AddOption(new TextMenuOption("你还没有任何模型"));
        }
        else
        {
            foreach (var modelId in ownedModelIds)
            {
                var model = _modelService.GetModelById(modelId);
                if (model == null) continue;

                var button = new ButtonMenuOption($"✓ {model.DisplayName}");
                var capturedId = modelId;
                button.Click += async (sender, args) => await OpenModelDetailMenuAsync(args.Player!, capturedId, parentMenu);
                builder.AddOption(button);
            }
        }

        _core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private async Task OpenModelDetailMenuAsync(IPlayer player, string modelId, IMenuAPI? parentMenu = null)
    {
        var localizer = _translationService.GetPlayerLocalizer(player);
        var menuConfig = _config.CurrentValue.Menu;
        var model = _modelService.GetModelById(modelId);
        if (model == null) return;

        var builder = _core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle($"📦 {model.DisplayName}")
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (parentMenu != null) builder.BindToParent(parentMenu);
        if (menuConfig.EnableSound) builder.EnableSound();

        // 模型信息
        builder.AddOption(new TextMenuOption(model.Description));
        builder.AddOption(new TextMenuOption($"阵营: {model.Team}"));
        
        var owns = await _databaseService.PlayerOwnsModelAsync(player.SteamID, modelId);
        var currentModel = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID);
        bool isEquipped = currentModel.modelPath == model.ModelPath;
        
        if (owns)
        {
            builder.AddOption(new TextMenuOption(isEquipped ? "✅ 已装备" : "✓ 已拥有"));
        }
        else
        {
            builder.AddOption(new TextMenuOption($"价格: {model.Price} credits"));
        }

        // 预览按钮
        var previewButton = new ButtonMenuOption("🔍 预览模型");
        previewButton.Click += async (sender, args) => _previewService.ShowPreview(args.Player!, model.ModelPath);
        builder.AddOption(previewButton);

        // 根据状态显示不同按钮
        if (isEquipped)
        {
            var unequipButton = new ButtonMenuOption("❌ 卸载模型");
            unequipButton.Click += async (sender, args) =>
            {
                await UnequipModelAsync(args.Player!, model.Team);
                await OpenModelDetailMenuAsync(args.Player!, modelId, parentMenu);
            };
            builder.AddOption(unequipButton);
        }
        else if (owns || model.Price == 0)
        {
            var equipButton = new ButtonMenuOption("✅ 装备模型");
            equipButton.Click += async (sender, args) =>
            {
                var success = _modelService.ApplyModelToPlayer(args.Player!, modelId);
                if (success)
                {
                    _logger.LogInformation($"玩家 {args.Player!.Controller.PlayerName} 装备模型: {model.DisplayName}");
                    await OpenModelDetailMenuAsync(args.Player!, modelId, parentMenu);
                }
            };
            builder.AddOption(equipButton);
        }
        else
        {
            var buyButton = new ButtonMenuOption($"💰 购买 ({model.Price} credits)");
            buyButton.Click += async (sender, args) =>
            {
                var (success, message) = await _modelService.PurchaseModelAsync(args.Player!, modelId);
                _logger.LogInformation($"{message}");
                if (success) await OpenModelDetailMenuAsync(args.Player!, modelId, parentMenu);
            };
            builder.AddOption(buyButton);
        }

        _core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private async Task UnequipModelAsync(IPlayer player, string team)
    {
        await _databaseService.SetPlayerCurrentModelAsync(player.SteamID, "", "");
        _logger.LogInformation($"玩家 {player.Controller.PlayerName} 卸载模型");
    }
}