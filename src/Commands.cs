using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using PlayersModel.Config;
using PlayersModel.Services;

namespace PlayersModel;

/// <summary>
/// 命令系统 (PlayersModel partial class)
/// </summary>
public partial class PlayersModel
{
    /// <summary>
    /// 注册所有命令
    /// </summary>
    private void RegisterCommands()
    {
        if (_serviceProvider == null) return;

        var config = _serviceProvider.GetRequiredService<IOptionsMonitor<PluginConfig>>();
        var commands = config.CurrentValue.Commands;

        // 注册玩家命令 (支持多个命令别名)
        if (commands.Player.Model != null && commands.Player.Model.Length > 0)
        {
            foreach (var cmd in commands.Player.Model)
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                    Core.Command.RegisterCommand(cmd, Command_Model);
            }
        }
        
        if (commands.Player.BuyModel != null && commands.Player.BuyModel.Length > 0)
        {
            foreach (var cmd in commands.Player.BuyModel)
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                    Core.Command.RegisterCommand(cmd, Command_BuyModel);
            }
        }
        
        if (commands.Player.Balance != null && commands.Player.Balance.Length > 0)
        {
            foreach (var cmd in commands.Player.Balance)
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                    Core.Command.RegisterCommand(cmd, Command_Balance);
            }
        }
        
        if (commands.Player.MyModels != null && commands.Player.MyModels.Length > 0)
        {
            foreach (var cmd in commands.Player.MyModels)
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                    Core.Command.RegisterCommand(cmd, Command_MyModels);
            }
        }

        // 注册管理员命令 (支持多个命令别名)
        if (commands.Admin.GiveCredits != null && commands.Admin.GiveCredits.Length > 0)
        {
            foreach (var cmd in commands.Admin.GiveCredits)
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                    Core.Command.RegisterCommand(cmd, Command_Admin_GiveCredits);
            }
        }
        
        if (commands.Admin.GiveModel != null && commands.Admin.GiveModel.Length > 0)
        {
            foreach (var cmd in commands.Admin.GiveModel)
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                    Core.Command.RegisterCommand(cmd, Command_Admin_GiveModel);
            }
        }
            
        if (commands.Admin.SetModel != null && commands.Admin.SetModel.Length > 0)
        {
            foreach (var cmd in commands.Admin.SetModel)
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                    Core.Command.RegisterCommand(cmd, Command_Admin_SetModel);
            }
        }
        
        if (commands.Admin.ReloadConfig != null && commands.Admin.ReloadConfig.Length > 0)
        {
            foreach (var cmd in commands.Admin.ReloadConfig)
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                    Core.Command.RegisterCommand(cmd, Command_Admin_ReloadConfig);
            }
        }
        
        if (commands.Admin.ListPlayerModels != null && commands.Admin.ListPlayerModels.Length > 0)
        {
            foreach (var cmd in commands.Admin.ListPlayerModels)
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                    Core.Command.RegisterCommand(cmd, Command_Admin_ListPlayerModels);
            }
        }

        Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.commands_registered") ?? "Command system registered"}");
    }

    #region 玩家命令

    /// <summary>
    /// !model - 打开模型选择菜单
    /// </summary>
    private void Command_Model(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("command.player_only") ?? "This command can only be used by players!"}");
            return;
        }

        var player = context.Sender!;

        // 打开模型选择菜单
        var menuService = _serviceProvider?.GetService<IMenuService>();
        if (menuService != null)
        {
            menuService.OpenMainMenu(player);
        }
        else
        {
            context.Reply($"{PluginPrefix} {_translationService?.Get("command.menu_not_loaded", player) ?? "Menu service not loaded!"}");
        }
    }

    /// <summary>
    /// !buymodel <modelId> - 购买指定模型
    /// </summary>
    private void Command_BuyModel(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("command.player_only") ?? "This command can only be used by players!"}");
            return;
        }

        var player = context.Sender!;

        if (context.Args.Length < 1)
        {
            var usage = $"{PluginPrefix} {_translationService?.Get("command.buymodel.usage", player) ?? "Usage: !buymodel <modelID>"}";
            context.Reply(usage);
            return;
        }

        var modelId = context.Args[0];

        // 异步购买模型
        Task.Run(async () =>
        {
            if (_modelService != null)
            {
                var result = await _modelService.PurchaseModelAsync(player, modelId);
                context.Reply(result.message);
            }
        });
    }

    /// <summary>
    /// !balance - 查看余额
    /// </summary>
    private void Command_Balance(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("command.player_only") ?? "This command can only be used by players!"}");
            return;
        }

        var player = context.Sender!;
        var config = _serviceProvider?.GetRequiredService<IOptionsMonitor<PluginConfig>>();

        if (_economyAPI == null)
        {
            var message = $"{PluginPrefix} {_translationService?.Get("purchase.economy_unavailable", player) ?? "Economy system not loaded"}";
            context.Reply(message);
            return;
        }

        var walletKind = config?.CurrentValue.WalletKind ?? "credits";
        var balance = _economyAPI.GetPlayerBalance(player.SteamID, walletKind);
        var balanceMsg = $"{PluginPrefix} {_translationService?.Get("command.balance.format", player, balance, walletKind) ?? $"Balance: {balance} {walletKind}"}";
        context.Reply(balanceMsg);
    }

    /// <summary>
    /// !mymodels - 查看我的模型
    /// </summary>
    private void Command_MyModels(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("command.player_only") ?? "This command can only be used by players!"}");
            return;
        }

        var player = context.Sender!;
        context.Reply($"{PluginPrefix} {_translationService?.Get("command.mymodels.viewing", player) ?? "Viewing your models..."}");
    }

    #endregion

    #region 管理员命令

    /// <summary>
    /// !pm_givecredits <玩家名> <数量> - 给予玩家积分
    /// </summary>
    private void Command_Admin_GiveCredits(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("command.player_only") ?? "This command can only be used by players!"}");
            return;
        }

        var player = context.Sender!;

        // 检查管理员权限
        if (!IsPlayerAdmin(player.SteamID))
        {
            var message = $"{PluginPrefix} {_translationService?.Get("admin.permission_required", player) ?? "Admin permission required"}";
            context.Reply(message);
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.givecredits.usage") ?? "Usage: !pm_givecredits <player> <amount>"}");
            return;
        }

        var targetName = context.Args[0];
        if (!int.TryParse(context.Args[1], out int amount))
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.givecredits.invalid_amount") ?? "Invalid amount!"}");
            return;
        }

        // 查找目标玩家
        var targetPlayer = FindPlayerByName(targetName);
        if (targetPlayer == null)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.givecredits.player_not_found", targetName) ?? $"Player not found: {targetName}"}");
            return;
        }

        if (_economyAPI == null)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.givecredits.economy_not_loaded") ?? "Economy system not loaded!"}");
            return;
        }

        var config = _serviceProvider?.GetRequiredService<IOptionsMonitor<PluginConfig>>();
        var walletKind = config?.CurrentValue.WalletKind ?? "credits";

        _economyAPI.AddPlayerBalance(targetPlayer.SteamID, walletKind, amount);
        var newBalance = _economyAPI.GetPlayerBalance(targetPlayer.SteamID, walletKind);

        context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.givecredits.success", targetPlayer.Controller.PlayerName, amount, walletKind, newBalance) ?? $"Gave {targetPlayer.Controller.PlayerName} {amount} {walletKind}, new balance: {newBalance}"}");
    }

    /// <summary>
    /// !pm_givemodel <玩家名> <模型ID> - 给予玩家模型
    /// </summary>
    private void Command_Admin_GiveModel(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("command.player_only") ?? "This command can only be used by players!"}");
            return;
        }

        var player = context.Sender!;

        // 检查管理员权限
        if (!IsPlayerAdmin(player.SteamID))
        {
            context.Reply($"{PluginPrefix} {_translationService?.Get("admin.permission_required", player) ?? "Admin permission required"}");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.givemodel.usage") ?? "Usage: !pm_givemodel <player> <modelID>"}");
            return;
        }

        var targetName = context.Args[0];
        var modelId = context.Args[1];

        // 查找目标玩家
        var targetPlayer = FindPlayerByName(targetName);
        if (targetPlayer == null)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.givemodel.player_not_found", targetName) ?? $"Player not found: {targetName}"}");
            return;
        }

        // 异步处理
        Task.Run(async () =>
        {
            if (_modelService == null || _databaseService == null) return;

            var model = _modelService.GetModelById(modelId);
            if (model == null)
            {
                context.Reply($"{_translationService?.GetConsole("admin.givemodel.model_not_found", modelId) ?? $"Model not found: {modelId}"}");
                return;
            }

            // 直接添加到玩家拥有列表
            await _databaseService.AddOwnedModelAsync(
            targetPlayer.SteamID,
            targetPlayer.Controller.PlayerName,
            modelId,
            0,
            "gifted",
            player.SteamID,
            $"由管理员 {player.Controller.PlayerName} 赠送");

            context.Reply($"{_translationService?.GetConsole("admin.givemodel.success", targetPlayer.Controller.PlayerName, model.DisplayName) ?? $"Gave {targetPlayer.Controller.PlayerName} model: {model.DisplayName}"}");
        });
    }

    /// <summary>
    /// !pm_setmodel <玩家名> <模型ID> - 为玩家设置模型
    /// </summary>
    private void Command_Admin_SetModel(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("command.player_only") ?? "This command can only be used by players!"}");
            return;
        }

        var player = context.Sender!;

        // 检查管理员权限
        if (!IsPlayerAdmin(player.SteamID))
        {
            context.Reply($"{PluginPrefix} {_translationService?.Get("admin.permission_required", player) ?? "Admin permission required"}");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.setmodel.usage") ?? "Usage: !pm_setmodel <player> <modelID>"}");
            return;
        }

        var targetName = context.Args[0];
        var modelId = context.Args[1];

        // 查找目标玩家
        var targetPlayer = FindPlayerByName(targetName);
        if (targetPlayer == null)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.setmodel.player_not_found", targetName) ?? $"Player not found: {targetName}"}");
            return;
        }

        // 异步处理
        Task.Run(async () =>
        {
            if (_modelService == null) return;

            var success = _modelService.ApplyModelToPlayer(targetPlayer, modelId);
            if (success)
            {
                var model = _modelService.GetModelById(modelId);
                context.Reply($"{_translationService?.GetConsole("admin.setmodel.success", targetPlayer.Controller.PlayerName, model?.DisplayName ?? "Unknown") ?? $"Set model for {targetPlayer.Controller.PlayerName}: {model?.DisplayName ?? "Unknown"}"}");
            }
            else
            {
                context.Reply($"{_translationService?.GetConsole("admin.setmodel.failed") ?? "Failed to set model"}");
            }
        });
    }

    /// <summary>
    /// !pm_reload - 重新加载配置
    /// </summary>
    private void Command_Admin_ReloadConfig(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("command.player_only") ?? "This command can only be used by players!"}");
            return;
        }

        var player = context.Sender!;

        // 检查管理员权限
        if (!IsPlayerAdmin(player.SteamID))
        {
            context.Reply($"{PluginPrefix} {_translationService?.Get("admin.permission_required", player) ?? "Admin permission required"}");
            return;
        }

        try
        {
            // 重新加载翻译
            _translationService?.Reload();
            
            // 重新加载模型配置
            _modelService?.LoadModels();
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.reload.success") ?? "Configuration and translations reloaded"}");
        }
        catch (Exception ex)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.reload.failed", ex.Message ?? "") ?? $"Failed to reload configuration: {ex.Message}"}");
        }
    }

    /// <summary>
    /// !pm_listmodels <玩家名> - 查看玩家拥有的模型
    /// </summary>
    private void Command_Admin_ListPlayerModels(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("command.player_only") ?? "This command can only be used by players!"}");
            return;
        }

        var player = context.Sender!;

        // 检查管理员权限
        if (!IsPlayerAdmin(player.SteamID))
        {
            context.Reply($"{PluginPrefix} {_translationService?.Get("admin.permission_required", player) ?? "Admin permission required"}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.listmodels.usage") ?? "Usage: !pm_listmodels <player>"}");
            return;
        }

        var targetName = context.Args[0];

        // 查找目标玩家
        var targetPlayer = FindPlayerByName(targetName);
        if (targetPlayer == null)
        {
            context.Reply($"{PluginPrefix} {_translationService?.GetConsole("admin.listmodels.player_not_found", targetName) ?? $"Player not found: {targetName}"}");
            return;
        }

        // 异步处理
        Task.Run(async () =>
        {
            if (_databaseService == null || _modelService == null) return;

            var ownedModelIds = await _databaseService.GetPlayerOwnedModelsAsync(targetPlayer.SteamID);
            
            if (ownedModelIds.Count == 0)
            {
                context.Reply($"{_translationService?.GetConsole("admin.listmodels.no_models", targetPlayer.Controller.PlayerName) ?? $"{targetPlayer.Controller.PlayerName} doesn't have any models"}");
                return;
            }

            context.Reply($"{_translationService?.GetConsole("admin.listmodels.title", targetPlayer.Controller.PlayerName, ownedModelIds.Count) ?? $"=== {targetPlayer.Controller.PlayerName}'s Models ({ownedModelIds.Count}) ==="}");
            foreach (var modelId in ownedModelIds)
            {
                var model = _modelService.GetModelById(modelId);
                var displayName = model?.DisplayName ?? modelId;
                context.Reply($"{_translationService?.GetConsole("admin.listmodels.item", displayName, modelId) ?? $"- {displayName} ({modelId})"}");
            }
        });
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 检查玩家是否有管理员权限
    /// </summary>
    private bool IsPlayerAdmin(ulong steamId)
    {
        var config = _serviceProvider?.GetRequiredService<IOptionsMonitor<PluginConfig>>();
        if (config == null) return false;

        var adminPerms = config.CurrentValue.Permissions.AdminPermissions;
        foreach (var perm in adminPerms)
        {
            if (Core.Permission.PlayerHasPermission(steamId, perm))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 根据玩家名查找玩家
    /// </summary>
    private IPlayer? FindPlayerByName(string name)
    {
        // 遍历所有在线玩家
        var players = _serviceProvider?.GetService<IPlayerManagerService>();
        if (players == null) return null;
        
        foreach (var player in players.GetAllPlayers())
        {
            if (player == null || !player.IsValid) continue;
            
            var playerName = player.Controller.PlayerName;
            if (playerName != null && playerName.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return player;
            }
        }
        return null;
    }

    #endregion
}
