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
            
            int successCount = 0;
            int failCount = 0;
            var failedModels = new List<string>();
            
            foreach (var model in models)
            {
                try
                {
                    if (!string.IsNullOrEmpty(model.ModelPath))
                    {
                        @event.AddItem(model.ModelPath);
                       successCount++;
                     }
                    if (!string.IsNullOrEmpty(model.ArmsPath))
                    {
                        @event.AddItem(model.ArmsPath);
                        successCount++;
                    }
                }
                catch (Exception)
                {
                    failCount++;
                    failedModels.Add(model.DisplayName ?? model.ModelId);
                }
            }
            
            var failedInfo = failCount > 0 ? $", 失败的模型: {string.Join(", ", failedModels)}" : "";
            logger?.LogInformation(
                _translationService?.GetConsole("events.precache_summary", successCount, failCount, failedInfo) 
                ?? $"预缓存完成: 成功 {successCount} 个, 失败 {failCount} 个{failedInfo}"
            );
        };

        // 玩家进入服务器事件
        Core.Event.OnClientPutInServer += (@event) =>
        {
            var playerManager = _serviceProvider?.GetService<IPlayerManagerService>();
            if (playerManager == null) return;
            var player = playerManager.GetPlayer(@event.PlayerId);
            // 跳过无效玩家和BOT
            if (player == null || !player.IsValid || player.IsFakeClient) return;

            // 处理初始积分发放
            var creditsService = _serviceProvider?.GetService<ICreditsService>();
            if (creditsService != null)
            {
                Task.Run(async () => await creditsService.HandlePlayerJoinAsync(player));
            }

            Core.Scheduler.DelayBySeconds(1.5f, () =>
            {
                if (!player.IsValid || player.Pawn?.IsValid != true || _modelCacheService == null) return;
                
                var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                logger?.LogWarning($"[DEBUG] OnClientPutInServer - Player: {player.Controller.PlayerName}, SteamID: {player.SteamID}");
                
                try
                {
                    // 批量加载该玩家的缓存
                    logger?.LogWarning($"[DEBUG] OnClientPutInServer - Before BatchLoad");
                    _modelCacheService.BatchLoadPlayerCachesAsync(new[] { player.SteamID }).GetAwaiter().GetResult();
                    
                    var cacheAfterLoad = _modelCacheService.GetPlayerCache(player.SteamID);
                    logger?.LogWarning($"[DEBUG] OnClientPutInServer - Cache after load: {(cacheAfterLoad != null ? "EXISTS" : "NULL")}");
                    
                    if (cacheAfterLoad != null)
                    {
                        logger?.LogWarning($"[DEBUG] OnClientPutInServer - Cache content: AllTeam={cacheAfterLoad.AllTeamModelPath ?? "NULL"}, CT={cacheAfterLoad.CTModelPath ?? "NULL"}, T={cacheAfterLoad.TModelPath ?? "NULL"}");
                    }
                    
                    // 获取玩家当前阵营
                    var currentTeam = player.Controller.TeamNum;
                    var teamName = currentTeam == 2 ? "T" : currentTeam == 3 ? "CT" : "";
                    
                    logger?.LogWarning($"[DEBUG] EventPlayerSpawn - Team: {teamName}, TeamNum: {currentTeam}");
                    if (string.IsNullOrEmpty(teamName)) return; // 不在T或CT队伍
                    
                    // 确保玩家的缓存已加载（防止热身回合时缓存未加载）
                    var cacheBeforeLoad = _modelCacheService.GetPlayerCache(player.SteamID);
                    logger?.LogWarning($"[DEBUG] EventPlayerSpawn - Cache before load: {(cacheBeforeLoad != null ? "EXISTS" : "NULL")}");
                    
                    if (cacheBeforeLoad != null)
                    {
                        logger?.LogWarning($"[DEBUG] EventPlayerSpawn - Cache content: AllTeam={cacheBeforeLoad.AllTeamModelPath ?? "NULL"}, CT={cacheBeforeLoad.CTModelPath ?? "NULL"}, T={cacheBeforeLoad.TModelPath ?? "NULL"}");
                    }
                    
                    if (_modelCacheService.GetPlayerCache(player.SteamID) == null)
                    {
                        logger?.LogWarning($"[DEBUG] EventPlayerSpawn - Loading cache because it was NULL");
                        _modelCacheService.BatchLoadPlayerCachesAsync(new[] { player.SteamID }).GetAwaiter().GetResult();
                    }
                    
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
                    
                    logger?.LogWarning($"[DEBUG] OnClientPutInServer - ModelPath: {modelPathToApply ?? "NULL"}");
                    // 应用模型
                    if (!string.IsNullOrEmpty(modelPathToApply))
                    {
                        player.Pawn.SetModel(modelPathToApply);
                        logger?.LogInformation(_translationService?.GetConsole("events.player_join_applied", player.Controller.PlayerName) ?? $"Applied model on join: {player.Controller.PlayerName}");
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, _translationService?.GetConsole("events.player_join_failed", player?.Controller.PlayerName ?? "") ?? $"Failed to apply model on join: {player?.Controller.PlayerName}");
                }
            });
        };

        // 玩家重生事件
        Core.GameEvent.HookPost<EventPlayerSpawn>((@event) =>
        {
            var player = @event.UserIdPlayer;
            if (player == null || !player.IsValid) return HookResult.Continue;

            // 处理BOT随机模型
            if (player.IsFakeClient)
            {
                var config = _serviceProvider?.GetService<IOptionsMonitor<PluginConfig>>();
                if (config?.CurrentValue.EnableBotRandomModel == true)
                {
                    Core.Scheduler.DelayBySeconds(0.2f, () =>
                    {
                        if (!player.IsValid || player.Pawn?.IsValid != true || _modelService == null) return;
                        
                        try
                        {
                            // 获取BOT当前阵营
                            var currentTeam = player.Controller.TeamNum;
                            var teamName = currentTeam == 2 ? "T" : currentTeam == 3 ? "CT" : "";
                            
                            if (string.IsNullOrEmpty(teamName)) return;
                            
                            // 获取符合条件的模型列表
                            var availableModels = _modelService.GetAllModels()
                                .Where(m => (m.Team == teamName || m.Team == "All") && 
                                           (!config.CurrentValue.BotOnlyFreeModels || m.Price == 0))
                                .ToList();
                            
                            if (availableModels.Count > 0)
                            {
                                // 随机选择一个模型
                                var random = new Random();
                                var selectedModel = availableModels[random.Next(availableModels.Count)];
                                
                                // 应用模型
                                player.Pawn.SetModel(selectedModel.ModelPath);
                                var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                                logger?.LogDebug($"Applied random model to BOT {player.Controller.PlayerName}: {selectedModel.DisplayName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                            logger?.LogError(ex, $"Failed to apply random model to BOT: {player?.Controller.PlayerName}");
                        }
                    });
                }
                return HookResult.Continue;
            }

            Core.Scheduler.DelayBySeconds(0.3f, () =>
            {
                var logger = _serviceProvider?.GetService<ILogger<PlayersModel>>();
                logger?.LogWarning($"[DEBUG] EventPlayerSpawn - Player: {player.Controller.PlayerName}, SteamID: {player.SteamID}");
                if (!player.IsValid || player.Pawn?.IsValid != true || _modelCacheService == null) return;
                
                try
                {
                    // 获取玩家当前阵营
                    var currentTeam = player.Controller.TeamNum;
                    var teamName = currentTeam == 2 ? "T" : currentTeam == 3 ? "CT" : "";
                    
                    logger?.LogWarning($"[DEBUG] OnClientPutInServer - Team: {teamName}, TeamNum: {currentTeam}");
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
                    
                    logger?.LogWarning($"[DEBUG] EventPlayerSpawn - ModelPath: {modelPathToApply ?? "NULL"}");
                    // 应用模型
                    if (!string.IsNullOrEmpty(modelPathToApply))
                    {
                        player.Pawn.SetModel(modelPathToApply);
                        
                        // 刷新玩家实体以立即应用模型（避免需要二次重生）
                        // 注意：这可能会导致轻微的视觉闪烁，但确保模型立即生效
                        player.Pawn.Teleport(player.Pawn.AbsOrigin, player.Pawn.AbsRotation, player.Pawn.AbsVelocity);
                        
                        logger?.LogInformation(_translationService?.GetConsole("events.player_spawn_applied", player.Controller.PlayerName) ?? $"Applied model on spawn: {player.Controller.PlayerName}");
                    }
                }
                catch (Exception ex)
                {
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
                    // 跳过无效玩家和BOT
                    .Where(p => p != null && p.IsValid && !p.IsFakeClient && p.Pawn?.IsValid == true)
                    .ToList();
                
                if (allPlayers.Count == 0) return;
                
                // 批量加载所有玩家的缓存（一次数据库查询）
                var steamIds = allPlayers.Select(p => p!.SteamID).ToList();
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
                            if (player.Pawn == null) continue;
                            player.Pawn.SetModel(modelPathToApply);
                            logger?.LogInformation(_translationService?.GetConsole("events.round_start_applied", player.Controller.PlayerName) ?? $"Applied model on round start: {player.Controller.PlayerName}");
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