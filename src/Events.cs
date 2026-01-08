using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using Economy.Contract;
using Microsoft.Extensions.Options;
using PlayersModel.Config;
using PlayersModel.Services;

namespace PlayersModel;

public partial class PlayersModel
{
    private void InitializeEvents()
    {
        // 地图加载事件 - 在此时初始化Economy（确保所有插件都已加载）
        Core.Event.OnMapLoad += (@event) =>
        {
            Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.economy_delayed_init") ?? "Initializing Economy on map load..."}");
            
            // 尝试获取Economy API
            if (_economyAPI == null && _interfaceManager != null)
            {
                if (_interfaceManager.HasSharedInterface("Economy.API.v1"))
                {
                    _economyAPI = _interfaceManager.GetSharedInterface<IEconomyAPIv1>("Economy.API.v1");
                    Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.economy_delayed_success") ?? "✓ Successfully connected to Economy system on map load!"}");
                }
            }
            
            // 初始化或重新初始化Economy
            InitializeEconomy();
        };

        // 预缓存资源事件
        Core.Event.OnPrecacheResource += (@event) =>
        {
            if (_modelService == null) return;
            var models = _modelService.GetAllModels();
            var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
            
            foreach (var model in models)
            {
                if (!string.IsNullOrEmpty(model.ModelPath))
                {
                    @event.AddItem(model.ModelPath);
                    logger?.LogInformation(_translationService?.GetConsole("events.precache_model", model.ModelPath) ?? $"Precaching model: {model.ModelPath}");
                }
                if (!string.IsNullOrEmpty(model.ArmsPath))
                {
                    @event.AddItem(model.ArmsPath);
                    logger?.LogInformation(_translationService?.GetConsole("events.precache_arms", model.ArmsPath) ?? $"Precaching arms model: {model.ArmsPath}");
                }
            }
        };

        // 玩家进入服务器事件
        Core.Event.OnClientPutInServer += (@event) =>
        {
            var playerManager = _serviceProvider?.GetService<IPlayerManagerService>();
            if (playerManager == null) return;
            var player = playerManager.GetPlayer(@event.PlayerId);
            // 跳过BOT
            if (player == null || !player.IsValid || player.SteamID == 0 || player.SteamID < 70000000000000000) return;

            Core.Scheduler.DelayBySeconds(1.0f, () =>
            {
                if (!player.IsValid || player.Pawn?.IsValid != true || _modelCacheService == null) return;
                
                try
                {
                    // 批量加载该玩家的缓存
                    _modelCacheService.BatchLoadPlayerCachesAsync(new[] { player.SteamID }).GetAwaiter().GetResult();
                    
                    // 获取玩家当前阵营
                    var currentTeam = player.Controller.TeamNum;
                    var teamName = currentTeam == 2 ? "T" : currentTeam == 3 ? "CT" : "";
                    
                    if (string.IsNullOrEmpty(teamName)) return; // 不在T或CT队伍
                    
                    // 从缓存获取应用的模型路径（优先级：All > CT/T）
                    var modelPathToApply = _modelCacheService.GetModelPathToApply(player.SteamID, teamName);
                    
                    // 如果缓存中没有，使用阵营默认模型
                    if (string.IsNullOrEmpty(modelPathToApply))
                    {
                        var config = _serviceProvider?.GetService<IOptionsMonitor<PluginConfig>>();
                        if (config != null)
                        {
                            modelPathToApply = teamName == "CT" 
                                ? config.CurrentValue.DefaultCTModelPath 
                                : config.CurrentValue.DefaultTModelPath;
                        }
                    }
                    
                    // 应用模型
                    if (!string.IsNullOrEmpty(modelPathToApply))
                    {
                        player.Pawn.SetModel(modelPathToApply);
                        var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                        logger?.LogInformation(_translationService?.GetConsole("events.player_join_applied", player.Controller.PlayerName) ?? $"Applied model on join: {player.Controller.PlayerName}");
                        
                        // 延迟应用 MeshGroup 配置，确保模型先加载
                        Core.Scheduler.DelayBySeconds(0.25f, () =>
                        {
                            if (player?.Pawn?.IsValid == true)
                            {
                                var model = _modelService?.GetAllModels().FirstOrDefault(m => m.ModelPath == modelPathToApply);
                                if (model != null && model.MeshGroups != null && model.MeshGroups.Count > 0)
                                {
                                    var meshGroupService = _serviceProvider?.GetService<IMeshGroupService>();
                                    if (meshGroupService != null)
                                    {
                                        meshGroupService.LoadAndApplyPlayerMeshGroupsAsync(player, model.ModelId, teamName).GetAwaiter().GetResult();
                                        logger?.LogDebug($"加载并应用 MeshGroup 配置: {player.Controller.PlayerName}");
                                    }
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                    logger?.LogError(ex, _translationService?.GetConsole("events.player_join_failed", player?.Controller.PlayerName ?? "") ?? $"Failed to apply model on join: {player?.Controller.PlayerName}");
                }
            });
        };

        // 玩家重生事件
        Core.GameEvent.HookPost<EventPlayerSpawn>((@event) =>
        {
            var player = @event.UserIdPlayer;
            // 跳过BOT
            if (player == null || !player.IsValid || player.SteamID == 0 || player.SteamID < 70000000000000000) return HookResult.Continue;

            Core.Scheduler.DelayBySeconds(0.1f, () =>
            {
                if (!player.IsValid || player.Pawn?.IsValid != true || _modelCacheService == null) return;
                
                try
                {
                    // 获取玩家当前阵营
                    var currentTeam = player.Controller.TeamNum;
                    var teamName = currentTeam == 2 ? "T" : currentTeam == 3 ? "CT" : "";
                    
                    if (string.IsNullOrEmpty(teamName)) return; // 不在T或CT队伍
                    
                    // 从缓存获取应用的模型路径（优先级：All > CT/T）
                    var modelPathToApply = _modelCacheService.GetModelPathToApply(player.SteamID, teamName);
                    
                    // 如果缓存中没有，使用阵营默认模型
                    if (string.IsNullOrEmpty(modelPathToApply))
                    {
                        var config = _serviceProvider?.GetService<IOptionsMonitor<PluginConfig>>();
                        if (config != null)
                        {
                            modelPathToApply = teamName == "CT" 
                                ? config.CurrentValue.DefaultCTModelPath 
                                : config.CurrentValue.DefaultTModelPath;
                        }
                    }
                    
                    // 应用模型
                    if (!string.IsNullOrEmpty(modelPathToApply))
                    {
                        player.Pawn.SetModel(modelPathToApply);
                        var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                        logger?.LogInformation(_translationService?.GetConsole("events.player_spawn_applied", player.Controller.PlayerName) ?? $"Applied model on spawn: {player.Controller.PlayerName}");
                        
                        // 延迟应用 MeshGroup 配置，确保模型先加载
                        Core.Scheduler.DelayBySeconds(0.25f, () =>
                        {
                            if (player?.Pawn?.IsValid == true)
                            {
                                var model = _modelService?.GetAllModels().FirstOrDefault(m => m.ModelPath == modelPathToApply);
                                if (model != null && model.MeshGroups != null && model.MeshGroups.Count > 0)
                                {
                                    var meshGroupService = _serviceProvider?.GetService<IMeshGroupService>();
                                    if (meshGroupService != null)
                                    {
                                        meshGroupService.LoadAndApplyPlayerMeshGroupsAsync(player, model.ModelId, teamName).GetAwaiter().GetResult();
                                        logger?.LogDebug($"加载并应用 MeshGroup 配置: {player.Controller.PlayerName}");
                                    }
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                    logger?.LogError(ex, _translationService?.GetConsole("events.player_spawn_failed", player?.Controller.PlayerName ?? "") ?? $"Failed to apply model on spawn: {player?.Controller.PlayerName}");
                }
            });
            
            return HookResult.Continue;
        });

        // 回合开始事件
        Core.GameEvent.HookPost<EventRoundStart>((@event) =>
        {
            var playerManager = _serviceProvider?.GetService<IPlayerManagerService>();
            if (playerManager == null) return HookResult.Continue;

            var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
            logger?.LogInformation(_translationService?.GetConsole("events.round_start") ?? "Round started, applying saved models to all players");

            Core.Scheduler.DelayBySeconds(0.5f, () =>
            {
                var allPlayers = Enumerable.Range(0, 64)
                    .Select(i => playerManager.GetPlayer(i))
                    // 跳过BOT
                    .Where(p => p != null && p.IsValid && p.SteamID > 0 && p.SteamID >= 70000000000000000 && p.Pawn?.IsValid == true)
                    .ToList();
                
                if (allPlayers.Count == 0) return;
                
                // 批量加载所有玩家的缓存（一次数据库查询）
                var steamIds = allPlayers.Select(p => p.SteamID).ToList();
                _modelCacheService?.BatchLoadPlayerCachesAsync(steamIds).GetAwaiter().GetResult();
                
                logger?.LogInformation(_translationService?.GetConsole("events.round_start_batch_loaded", allPlayers.Count) ?? $"Batch loaded {allPlayers.Count} player models");
                
                foreach (var player in allPlayers)
                {
                    if (player == null || _modelCacheService == null) continue;
                    
                    try
                    {
                        // 获取玩家当前阵营
                        var currentTeam = player.Controller.TeamNum;
                        var teamName = currentTeam == 2 ? "T" : currentTeam == 3 ? "CT" : "";
                        
                        if (string.IsNullOrEmpty(teamName)) continue; // 不在T或CT队伍
                        
                        // 从缓存获取应用的模型路径（优先级：All > CT/T）
                        var modelPathToApply = _modelCacheService.GetModelPathToApply(player.SteamID, teamName);
                        
                        // 如果缓存中没有，使用阵营默认模型
                        if (string.IsNullOrEmpty(modelPathToApply))
                        {
                            var config = _serviceProvider?.GetService<IOptionsMonitor<PluginConfig>>();
                            if (config != null)
                            {
                                modelPathToApply = teamName == "CT" 
                                    ? config.CurrentValue.DefaultCTModelPath 
                                    : config.CurrentValue.DefaultTModelPath;
                            }
                        }
                        
                        // 应用模型
                        if (!string.IsNullOrEmpty(modelPathToApply))
                        {
                            player.Pawn.SetModel(modelPathToApply);
                            logger?.LogInformation(_translationService?.GetConsole("events.round_start_applied", player.Controller.PlayerName) ?? $"Applied model on round start: {player.Controller.PlayerName}");
                            
                            // 延迟应用 MeshGroup 配置，确保模型先加载
                            Core.Scheduler.DelayBySeconds(0.25f, () =>
                            {
                                if (player?.Pawn?.IsValid == true)
                                {
                                    var model = _modelService?.GetAllModels().FirstOrDefault(m => m.ModelPath == modelPathToApply);
                                    if (model != null && model.MeshGroups != null && model.MeshGroups.Count > 0)
                                    {
                                        var meshGroupService = _serviceProvider?.GetService<IMeshGroupService>();
                                        if (meshGroupService != null)
                                        {
                                            meshGroupService.LoadAndApplyPlayerMeshGroupsAsync(player, model.ModelId, teamName).GetAwaiter().GetResult();
                                            logger?.LogDebug($"加载并应用 MeshGroup 配置: {player.Controller.PlayerName}");
                                        }
                                    }
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, _translationService?.GetConsole("events.round_start_failed", player?.Controller.PlayerName ?? "") ?? $"Failed to apply model on round start: {player?.Controller.PlayerName}");
                    }
                }
            });
            
            return HookResult.Continue;
        });

        Console.WriteLine($"{PluginPrefix} {_translationService?.GetConsole("system.events_registered") ?? "Event listeners registered"}");
    }
}