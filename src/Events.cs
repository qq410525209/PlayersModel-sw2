using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;

namespace PlayersModel;

/// <summary>
/// 事件处理 (PlayersModel partial class)
/// </summary>
public partial class PlayersModel
{
    /// <summary>
    /// 初始化事件监听
    /// </summary>
    private void InitializeEvents()
    {
        // 预缓存资源事件 - 预缓存所有模型
        Core.Event.OnPrecacheResource += (@event) =>
        {
            if (_modelService == null) return;

            var models = _modelService.GetAllModels();
            var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
            
            foreach (var model in models)
            {
                // 预缓存模型文件
                if (!string.IsNullOrEmpty(model.ModelPath))
                {
                    @event.AddItem(model.ModelPath);
                    logger?.LogInformation($"预缓存模型: {model.ModelPath}");
                }
                
                // 预缓存手臂模型
                if (!string.IsNullOrEmpty(model.ArmsPath))
                {
                    @event.AddItem(model.ArmsPath);
                    logger?.LogInformation($"预缓存手臂模型: {model.ArmsPath}");
                }
            }
        };

        // 玩家进入服务器事件 - 自动应用保存的模型
        Core.Event.OnClientPutInServer += (@event) =>
        {
            var playerManager = _serviceProvider?.GetService<IPlayerManagerService>();
            if (playerManager == null) return;

            var player = playerManager.GetPlayer(@event.PlayerId);
            if (player == null || !player.IsValid) return;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1000);
                    if (_databaseService == null) return;

                    var modelData = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID);
                    if (!string.IsNullOrEmpty(modelData.modelPath) && player.Pawn?.IsValid == true)
                    {
                        player.Pawn.AcceptInput("SetModel", modelData.modelPath, null, null, 0);
                        var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                        logger?.LogInformation($"玩家进入应用模型: {player.Controller.PlayerName}");
                    }
                }
                catch (Exception ex)
                {
                    var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                    logger?.LogError(ex, $"玩家进入应用模型失败: {player?.Controller.PlayerName}");
                }
            });
        };

        // 玩家重生事件 - 应用保存的模型
        Core.GameEvent.HookPost<EventPlayerSpawn>((@event) =>
        {
            var playerManager = _serviceProvider?.GetService<IPlayerManagerService>();
            if (playerManager == null) return HookResult.Continue;

            var player = @event.UserIdPlayer;
            if (player == null || !player.IsValid) return HookResult.Continue;

            Core.Scheduler.DelayBySeconds(0.1f, () =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        if (_databaseService == null) return;

                        var modelData = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID);
                        if (!string.IsNullOrEmpty(modelData.modelPath) && player.Pawn?.IsValid == true)
                        {
                            player.Pawn.AcceptInput("SetModel", modelData.modelPath, null, null, 0);
                            var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                            logger?.LogInformation($"玩家重生应用模型: {player.Controller.PlayerName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                        logger?.LogError(ex, $"玩家重生应用模型失败: {player?.Controller.PlayerName}");
                    }
                });
            });
            
            return HookResult.Continue;
        });

        // 回合开始事件 - 为所有玩家应用模型
        Core.GameEvent.HookPost<EventRoundStart>((@event) =>
        {
            var playerManager = _serviceProvider?.GetService<IPlayerManagerService>();
            if (playerManager == null) return HookResult.Continue;

            var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
            logger?.LogInformation("回合开始，为所有玩家应用保存的模型");

            Core.Scheduler.DelayBySeconds(0.5f, () =>
            {
                var allPlayers = Enumerable.Range(0, 64).Select(i => playerManager.GetPlayer(i)).Where(p => p != null && p.IsValid);
            foreach (var player in allPlayers)
                {
                    if (player == null || !player.IsValid || player.Pawn?.IsValid != true) continue;

                    Task.Run(async () =>
                    {
                        try
                        {
                            if (_databaseService == null) return;

                            var modelData = await _databaseService.GetPlayerCurrentModelAsync(player.SteamID);
                            if (!string.IsNullOrEmpty(modelData.modelPath) && player.Pawn?.IsValid == true)
                            {
                                player.Pawn.AcceptInput("SetModel", modelData.modelPath, null, null, 0);
                                logger?.LogInformation($"回合开始应用模型: {player.Controller.PlayerName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, $"回合开始应用模型失败: {player?.Controller.PlayerName}");
                        }
                    });
                }
            });
            
            return HookResult.Continue;
        });

        Console.WriteLine("[PlayersModel] ✓ 事件监听器已注册");
    }
}