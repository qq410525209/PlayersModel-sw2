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

        // 注册玩家命令
        Core.Command.RegisterCommand(commands.Player.Model, Command_Model);
        Core.Command.RegisterCommand(commands.Player.BuyModel, Command_BuyModel);
        Core.Command.RegisterCommand(commands.Player.Balance, Command_Balance);
        Core.Command.RegisterCommand(commands.Player.MyModels, Command_MyModels);

        // 注册管理员命令
        Core.Command.RegisterCommand(commands.Admin.GiveCredits, Command_Admin_GiveCredits);
        Core.Command.RegisterCommand(commands.Admin.GiveModel, Command_Admin_GiveModel);
        Core.Command.RegisterCommand(commands.Admin.SetModel, Command_Admin_SetModel);
        Core.Command.RegisterCommand(commands.Admin.ReloadConfig, Command_Admin_ReloadConfig);
        Core.Command.RegisterCommand(commands.Admin.ListPlayerModels, Command_Admin_ListPlayerModels);

        Console.WriteLine("[PlayersModel] ✓ 命令系统已注册");
    }

    #region 玩家命令

    /// <summary>
    /// !model - 打开模型选择菜单
    /// </summary>
    private void Command_Model(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("[PlayersModel] 此命令只能由玩家使用!");
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
            context.Reply("[PlayersModel] 菜单服务未加载!");
        }
    }

    /// <summary>
    /// !buymodel <modelId> - 购买指定模型
    /// </summary>
    private void Command_BuyModel(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("[PlayersModel] 此命令只能由玩家使用!");
            return;
        }

        var player = context.Sender!;
        var translation = _serviceProvider?.GetService<ITranslationService>();

        if (context.Args.Length < 1)
        {
            var usage = "[PlayersModel] " + (translation?.Get("command.buymodel.usage", player) ?? "用法: !buymodel <模型ID>");
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
                // TODO: 需要使用正确的 PrintToChat API
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
            context.Reply("[PlayersModel] 此命令只能由玩家使用!");
            return;
        }

        var player = context.Sender!;
        var translation = _serviceProvider?.GetService<ITranslationService>();
        var config = _serviceProvider?.GetRequiredService<IOptionsMonitor<PluginConfig>>();

        if (_economyAPI == null)
        {
            var message = "[PlayersModel] " + (translation?.Get("purchase.economy_unavailable", player) ?? "经济系统未加载");
            context.Reply(message);
            return;
        }

        var walletKind = config?.CurrentValue.WalletKind ?? "credits";
        var balance = _economyAPI.GetPlayerBalance(player, walletKind);
        var balanceMsg = "[PlayersModel] " + (translation?.Get("command.balance.format", player, balance) ?? $"余额: {balance} {walletKind}");
        context.Reply(balanceMsg);
    }

    /// <summary>
    /// !mymodels - 查看我的模型
    /// </summary>
    private void Command_MyModels(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("[PlayersModel] 此命令只能由玩家使用!");
            return;
        }

        var player = context.Sender!;

        // TODO: 打开"我的模型"菜单或列表
        context.Reply("[PlayersModel] 正在查看你的模型...");
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
            context.Reply("[PlayersModel] 此命令只能由玩家使用!");
            return;
        }

        var player = context.Sender!;

        // 检查管理员权限
        if (!IsPlayerAdmin(player.SteamID))
        {
            var translation = _serviceProvider?.GetService<ITranslationService>();
            var message = "[PlayersModel] " + (translation?.Get("permission.admin_required", player) ?? "需要管理员权限");
            context.Reply(message);
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply("[PlayersModel] 用法: !pm_givecredits <玩家名> <数量>");
            return;
        }

        var targetName = context.Args[0];
        if (!int.TryParse(context.Args[1], out int amount))
        {
            context.Reply("[PlayersModel] 无效的数量!");
            return;
        }

        // 查找目标玩家
        var targetPlayer = FindPlayerByName(targetName);
        if (targetPlayer == null)
        {
            context.Reply($"[PlayersModel] 未找到玩家: {targetName}");
            return;
        }

        if (_economyAPI == null)
        {
            context.Reply("[PlayersModel] 经济系统未加载!");
            return;
        }

        var config = _serviceProvider?.GetRequiredService<IOptionsMonitor<PluginConfig>>();
        var walletKind = config?.CurrentValue.WalletKind ?? "credits";

        _economyAPI.AddPlayerBalance(targetPlayer, walletKind, amount);
        var newBalance = _economyAPI.GetPlayerBalance(targetPlayer, walletKind);

        context.Reply($"[PlayersModel] ✓ 已给予 {targetPlayer.Controller.PlayerName} {amount} {walletKind}, 新余额: {newBalance}");
        // TODO: 使用正确的 PrintToChat API
        // targetPlayer.PrintToChat($"✓ 你收到了 {amount} {walletKind}! 当前余额: {newBalance}");
    }

    /// <summary>
    /// !pm_givemodel <玩家名> <模型ID> - 给予玩家模型
    /// </summary>
    private void Command_Admin_GiveModel(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("[PlayersModel] 此命令只能由玩家使用!");
            return;
        }

        var player = context.Sender!;

        // 检查管理员权限
        if (!IsPlayerAdmin(player.SteamID))
        {
            context.Reply("[PlayersModel] 需要管理员权限");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply("[PlayersModel] 用法: !pm_givemodel <玩家名> <模型ID>");
            return;
        }

        var targetName = context.Args[0];
        var modelId = context.Args[1];

        // 查找目标玩家
        var targetPlayer = FindPlayerByName(targetName);
        if (targetPlayer == null)
        {
            context.Reply($"[PlayersModel] 未找到玩家: {targetName}");
            return;
        }

        // 异步处理
        Task.Run(async () =>
        {
            if (_modelService == null || _databaseService == null) return;

            var model = _modelService.GetModelById(modelId);
            if (model == null)
            {
                context.Reply($"✗ 模型不存在: {modelId}");
                return;
            }

            // 直接添加到玩家拥有列表
            await _databaseService.AddOwnedModelAsync(targetPlayer.SteamID, modelId);

            context.Reply($"✓ 已给予 {targetPlayer.Controller.PlayerName} 模型: {model.DisplayName}");
            // TODO: 使用正确的 PrintToChat API
            // targetPlayer.PrintToChat($"✓ 你获得了新模型: {model.DisplayName}");
        });
    }

    /// <summary>
    /// !pm_setmodel <玩家名> <模型ID> - 为玩家设置模型
    /// </summary>
    private void Command_Admin_SetModel(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("[PlayersModel] 此命令只能由玩家使用!");
            return;
        }

        var player = context.Sender!;

        // 检查管理员权限
        if (!IsPlayerAdmin(player.SteamID))
        {
            context.Reply("[PlayersModel] 需要管理员权限");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply("[PlayersModel] 用法: !pm_setmodel <玩家名> <模型ID>");
            return;
        }

        var targetName = context.Args[0];
        var modelId = context.Args[1];

        // 查找目标玩家
        var targetPlayer = FindPlayerByName(targetName);
        if (targetPlayer == null)
        {
            context.Reply($"[PlayersModel] 未找到玩家: {targetName}");
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
                context.Reply($"✓ 已为 {targetPlayer.Controller.PlayerName} 设置模型: {model?.DisplayName}");
                // TODO: 使用正确的 PrintToChat API
                // targetPlayer.PrintToChat($"✓ 你的模型已被设置为: {model?.DisplayName}");
            }
            else
            {
                context.Reply($"✗ 设置模型失败");
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
            context.Reply("[PlayersModel] 此命令只能由玩家使用!");
            return;
        }

        var player = context.Sender!;

        // 检查管理员权限
        if (!IsPlayerAdmin(player.SteamID))
        {
            context.Reply("[PlayersModel] 需要管理员权限");
            return;
        }

        try
        {
            // 重新加载模型配置
            _modelService?.LoadModels();
            context.Reply("[PlayersModel] ✓ 配置已重新加载");
        }
        catch (Exception ex)
        {
            context.Reply($"[PlayersModel] ✗ 重新加载配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// !pm_listmodels <玩家名> - 查看玩家拥有的模型
    /// </summary>
    private void Command_Admin_ListPlayerModels(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("[PlayersModel] 此命令只能由玩家使用!");
            return;
        }

        var player = context.Sender!;

        // 检查管理员权限
        if (!IsPlayerAdmin(player.SteamID))
        {
            context.Reply("[PlayersModel] 需要管理员权限");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply("[PlayersModel] 用法: !pm_listmodels <玩家名>");
            return;
        }

        var targetName = context.Args[0];

        // 查找目标玩家
        var targetPlayer = FindPlayerByName(targetName);
        if (targetPlayer == null)
        {
            context.Reply($"[PlayersModel] 未找到玩家: {targetName}");
            return;
        }

        // 异步处理
        Task.Run(async () =>
        {
            if (_databaseService == null || _modelService == null) return;

            var ownedModelIds = await _databaseService.GetPlayerOwnedModelsAsync(targetPlayer.SteamID);
            
            if (ownedModelIds.Count == 0)
            {
                context.Reply($"{targetPlayer.Controller.PlayerName} 还没有任何模型");
                return;
            }

            context.Reply($"=== {targetPlayer.Controller.PlayerName} 的模型 ({ownedModelIds.Count}) ===");
            foreach (var modelId in ownedModelIds)
            {
                var model = _modelService.GetModelById(modelId);
                var displayName = model?.DisplayName ?? modelId;
                context.Reply($"- {displayName} ({modelId})");
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