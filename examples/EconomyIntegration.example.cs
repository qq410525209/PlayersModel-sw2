using SwiftlyS2.Shared.Commands;
using Economy.Contract;

namespace PlayersModel;

/// <summary>
/// 演示如何在 PlayersModel 插件中使用 Economy API
/// </summary>
public partial class PlayersModel
{
    // 模型价格配置
    private const int MODEL_PRICE = 1000;
    private const string WALLET_KIND = "credits";

    /// <summary>
    /// 示例命令: 购买玩家模型
    /// </summary>
    [Command("buymodel")]
    public void BuyModelCommand(ICommandContext context)
    {
        if (_economyAPI == null)
        {
            context.Reply(" [PlayersModel] 经济系统未加载,无法购买模型!");
            return;
        }

        var player = context.Player;
        if (player == null)
        {
            context.Reply(" [PlayersModel] 无效的玩家!");
            return;
        }

        // 检查玩家余额
        var balance = _economyAPI.GetPlayerBalance(player, WALLET_KIND);
        
        if (!_economyAPI.HasSufficientFunds(player, WALLET_KIND, MODEL_PRICE))
        {
            context.Reply($" [PlayersModel] 余额不足! 需要: {MODEL_PRICE} credits, 当前余额: {balance} credits");
            return;
        }

        // 扣除玩家余额
        _economyAPI.SubtractPlayerBalance(player, WALLET_KIND, MODEL_PRICE);
        
        // TODO: 在这里应用模型给玩家
        // ApplyModelToPlayer(player, selectedModel);
        
        var newBalance = _economyAPI.GetPlayerBalance(player, WALLET_KIND);
        context.Reply($" [PlayersModel] 购买成功! 剩余余额: {newBalance} credits");
    }

    /// <summary>
    /// 示例命令: 查看余额
    /// </summary>
    [Command("balance")]
    [CommandAlias("bal")]
    public void BalanceCommand(ICommandContext context)
    {
        if (_economyAPI == null)
        {
            context.Reply(" [PlayersModel] 经济系统未加载!");
            return;
        }

        var player = context.Player;
        if (player == null) return;

        var balance = _economyAPI.GetPlayerBalance(player, WALLET_KIND);
        context.Reply($" [PlayersModel] 你的余额: {balance} credits");
    }

    /// <summary>
    /// 示例命令: 管理员给予玩家积分
    /// </summary>
    [Command("givecredits")]
    public void GiveCreditsCommand(ICommandContext context)
    {
        if (_economyAPI == null)
        {
            context.Reply(" [PlayersModel] 经济系统未加载!");
            return;
        }

        // TODO: 添加管理员权限检查
        // if (!Core.Permission.PlayerHasPermission(context.Player.SteamID, "@css/admin"))
        // {
        //     context.Reply("你没有权限使用此命令!");
        //     return;
        // }

        var args = context.Arguments;
        if (args.Count < 2)
        {
            context.Reply(" [PlayersModel] 用法: !givecredits <玩家名> <数量>");
            return;
        }

        var targetName = args[0];
        if (!int.TryParse(args[1], out int amount))
        {
            context.Reply(" [PlayersModel] 无效的数量!");
            return;
        }

        // TODO: 查找目标玩家
        // var targetPlayer = FindPlayerByName(targetName);
        // if (targetPlayer == null)
        // {
        //     context.Reply($"未找到玩家: {targetName}");
        //     return;
        // }

        // _economyAPI.AddPlayerBalance(targetPlayer, WALLET_KIND, amount);
        // context.Reply($"已给予 {targetName} {amount} credits");
    }

    /// <summary>
    /// 监听余额变化事件
    /// </summary>
    private void InitializeEconomyEvents()
    {
        if (_economyAPI == null) return;

        // 监听玩家余额变化
        _economyAPI.OnPlayerBalanceChanged += (steamid, walletKind, newBalance, oldBalance) =>
        {
            if (walletKind == WALLET_KIND)
            {
                var change = newBalance - oldBalance;
                Console.WriteLine($"[PlayersModel] 玩家 {steamid} 余额变化: {oldBalance} -> {newBalance} (变化: {change})");
            }
        };

        // 监听玩家转账
        _economyAPI.OnPlayerFundsTransferred += (fromSteamid, toSteamid, walletKind, amount) =>
        {
            if (walletKind == WALLET_KIND)
            {
                Console.WriteLine($"[PlayersModel] 转账: {fromSteamid} -> {toSteamid}, 金额: {amount} credits");
            }
        };

        // 监听玩家数据加载
        _economyAPI.OnPlayerLoad += (player) =>
        {
            Console.WriteLine($"[PlayersModel] 玩家 {player.Controller.PlayerName} 的经济数据已加载");
        };
    }

    /// <summary>
    /// 示例: 检查玩家是否能购买指定价格的物品
    /// </summary>
    private bool CanPlayerAfford(IPlayer player, int price)
    {
        if (_economyAPI == null) return false;
        return _economyAPI.HasSufficientFunds(player, WALLET_KIND, price);
    }

    /// <summary>
    /// 示例: 玩家间转账
    /// </summary>
    private void TransferCredits(IPlayer fromPlayer, IPlayer toPlayer, int amount)
    {
        if (_economyAPI == null) return;
        
        if (_economyAPI.HasSufficientFunds(fromPlayer, WALLET_KIND, amount))
        {
            _economyAPI.TransferFunds(fromPlayer, toPlayer, WALLET_KIND, amount);
            Console.WriteLine($"[PlayersModel] 转账成功: {fromPlayer.Controller.PlayerName} -> {toPlayer.Controller.PlayerName}, 金额: {amount}");
        }
    }
}