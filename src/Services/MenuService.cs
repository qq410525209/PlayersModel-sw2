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
            .Design.SetMenuTitle(_translation.Get("menu.main.title", player))
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (menuConfig.EnableSound) builder.EnableSound();
        if (menuConfig.AutoCloseDelay > 0) builder.SetAutoCloseDelay(menuConfig.AutoCloseDelay);

        // 通用模型
        var allButton = new ButtonMenuOption(_translation.Get("menu.option.all_models", player));
        allButton.Click += async (sender, args) => OpenModelCategoryMenu(args.Player!, "All");
        builder.AddOption(allButton);

        // CT模型
        var ctButton = new ButtonMenuOption(_translation.Get("menu.option.ct_models", player));
        ctButton.Click += async (sender, args) => OpenModelCategoryMenu(args.Player!, "CT");
        builder.AddOption(ctButton);

        // T模型  
        var tButton = new ButtonMenuOption(_translation.Get("menu.option.t_models", player));
        tButton.Click += async (sender, args) => OpenModelCategoryMenu(args.Player!, "T");
        builder.AddOption(tButton);

        // 我的模型
        var myModelsButton = new ButtonMenuOption(_translation.Get("menu.option.owned_models", player));
        myModelsButton.Click += async (sender, args) => await OpenOwnedModelsMenuAsync(args.Player!);
        builder.AddOption(myModelsButton);

        _core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private void OpenModelCategoryMenu(IPlayer player, string team, IMenuAPI? parentMenu = null)
    {
        var menuConfig = _config.CurrentValue.Menu;
        
        var title = team.ToLower() switch
        {
            "ct" => _translation.Get("menu.ct_models.title", player),
            "t" => _translation.Get("menu.t_models.title", player),
            "all" => _translation.Get("menu.all_models.title", player),
            _ => _translation.Get("menu.main.title", player)
        };
        
        var builder = _core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle(title)
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (parentMenu != null) builder.BindToParent(parentMenu);
        if (menuConfig.EnableSound) builder.EnableSound();

        var models = _modelService.GetAvailableModelsForPlayer(player, team);
        foreach (var model in models)
        {
            var button = new ButtonMenuOption(model.DisplayName);
            var capturedId = model.ModelId;
            button.Click += async (sender, args) => await OpenModelDetailMenuAsync(args.Player!, capturedId);
            builder.AddOption(button);
        }

        _core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private async Task OpenOwnedModelsMenuAsync(IPlayer player, IMenuAPI? parentMenu = null)
    {
        var menuConfig = _config.CurrentValue.Menu;

        var builder = _core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle(_translation.Get("menu.owned_models.title", player))
            .Design.SetMaxVisibleItems(menuConfig.ItemsPerPage);

        if (parentMenu != null) builder.BindToParent(parentMenu);
        if (menuConfig.EnableSound) builder.EnableSound();

        var ownedModelIds = await _databaseService.GetPlayerOwnedModelsAsync(player.SteamID);

        if (ownedModelIds.Count == 0)
        {
            builder.AddOption(new TextMenuOption(_translation.Get("menu.option.no_models", player)));
        }
        else
        {
            foreach (var modelId in ownedModelIds)
            {
                var model = _modelService.GetModelById(modelId);
                if (model == null) continue;

                var button = new ButtonMenuOption($"✓ {model.DisplayName}");
                var capturedId = modelId;
                button.Click += async (sender, args) => await OpenModelDetailMenuAsync(args.Player!, capturedId);
                builder.AddOption(button);
            }
        }

        _core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private async Task OpenModelDetailMenuAsync(IPlayer player, string modelId, IMenuAPI? parentMenu = null)
    {
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
        builder.AddOption(new TextMenuOption($"{_translation.Get("model.team", player)}: {model.Team}"));
        
        var owns = await _databaseService.PlayerOwnsModelAsync(player.SteamID, modelId);
        var currentModel = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID);
        bool isEquipped = currentModel.modelPath == model.ModelPath;
        
        if (owns)
        {
            var statusText = isEquipped 
                ? _translation.Get("menu.option.equipped", player)
                : _translation.Get("menu.option.owned", player);
            builder.AddOption(new TextMenuOption(statusText));
        }

        // 预览按钮
        var previewButton = new ButtonMenuOption(_translation.Get("menu.option.preview", player));
        previewButton.Click += async (sender, args) => _previewService.ShowPreview(args.Player!, model.ModelPath);
        builder.AddOption(previewButton);

        // 根据状态显示不同按钮
        if (isEquipped)
        {
            var unequipButton = new ButtonMenuOption(_translation.Get("menu.option.unequip_model", player));
            unequipButton.Click += async (sender, args) =>
            {
                await UnequipModelAsync(args.Player!, model.Team);
                await OpenModelDetailMenuAsync(args.Player!, modelId, parentMenu);
            };
            builder.AddOption(unequipButton);
        }
        else if (owns || model.Price == 0)
        {
            var equipButton = new ButtonMenuOption(_translation.Get("menu.option.equip_model", player));
            equipButton.Click += async (sender, args) =>
            {
                var success = _modelService.ApplyModelToPlayer(args.Player!, modelId);
                if (success)
                {
                    _logger.LogInformation(_translation.GetConsole("menuservice.player_equipped", args.Player!.Controller.PlayerName, model.DisplayName));
                    await OpenModelDetailMenuAsync(args.Player!, modelId, parentMenu);
                }
            };
            builder.AddOption(equipButton);
        }
        else
        {
            var walletKind = _config.CurrentValue.WalletKind;
            var buyButton = new ButtonMenuOption(_translation.Get("menu.option.buy_model", player, $"{model.Price} {walletKind}"));
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
        // 根据阵营获取默认模型路径
        var defaultModelPath = team.ToLower() == "ct" 
            ? _config.CurrentValue.DefaultCTModelPath 
            : _config.CurrentValue.DefaultTModelPath;

        // 设置为默认模型
        await _databaseService.SetPlayerCurrentModelAsync(player.SteamID, defaultModelPath, "");
        
        // 应用默认模型到玩家
        if (player.Pawn?.IsValid == true)
        {
            var pawn = player.Pawn;
            _core.Scheduler.DelayBySeconds(0.01f, () =>
            {
                if (pawn?.IsValid == true)
                {
                    pawn.SetModel(defaultModelPath);
                }
            });
        }
        
        _logger.LogInformation(_translation.GetConsole("menuservice.player_unequipped", player.Controller.PlayerName, team));
    }
}