using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace PlayersModel.Services;

public interface IPreviewService
{
    void ShowPreview(IPlayer player, string modelPath);
}

public class PreviewService : IPreviewService
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<PreviewService> _logger;
    private readonly ITranslationService _translation;
    
    // 跟踪每个玩家的预览实体
    private readonly Dictionary<ulong, uint> _playerPreviewEntities = new();

    public PreviewService(ISwiftlyCore core, ILogger<PreviewService> logger, ITranslationService translation)
    {
        _core = core;
        _logger = logger;
        _translation = translation;
    }

    /// <summary>
    /// 显示模型预览（在玩家前方100单位、高5单位展示5秒，带辉光效果）
    /// </summary>
    public void ShowPreview(IPlayer player, string modelPath)
    {
        // 确保所有操作都在主线程执行
        _core.Scheduler.DelayBySeconds(0.01f, () => ShowPreviewInternal(player, modelPath));
    }

    private void ShowPreviewInternal(IPlayer player, string modelPath)
    {
        if (player.Pawn?.IsValid != true) return;

        // 删除该玩家之前的预览实体
        RemoveOldPreview(player.SteamID);

        try
        {
            // 创建预览实体
            var entity = _core.EntitySystem.CreateEntityByDesignerName<CBaseModelEntity>("prop_dynamic");
            if (entity == null || !entity.IsValid)
            {
                _logger.LogWarning(_translation.GetConsole("preview.entity_create_failed"));
                return;
            }

            var pawn = player.Pawn;
            if (pawn?.AbsOrigin == null || pawn?.AbsRotation == null) return;

            // 计算玩家前方100单位的位置，并增加5单位高度
            var spawnPos = CalculateFrontPosition(pawn.AbsOrigin.Value, pawn.AbsRotation.Value, 100.0f);
            spawnPos.Z += 5.0f;  // 增加5单位高度

            // 设置实体朝向玩家（旋转180度）
            var entityAngle = new QAngle(0, pawn.AbsRotation.Value.Y + 180, 0);

            // 设置实体属性
            entity.Spawnflags = 256u;
            if (entity.Collision != null)
            {
                entity.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
            }

            // 传送并生成实体
            entity.Teleport(spawnPos, entityAngle, pawn.AbsVelocity);
            entity.DispatchSpawn();

            var entityIndex = entity.Index;
            _playerPreviewEntities[player.SteamID] = entityIndex;

            // 生成后立即设置模型
            entity.SetModel(modelPath);

            // 设置传输状态：先允许个人传输，然后禁用有效玩家的传输
            
            var playerId = player.Slot;  // 获取预览者的Slot（玩家ID）
            entity.SetTransmitState(true, playerId);  // 只对预览者启用传输

            //禁用目前所有在线有效的玩家传输
            foreach (var playera in _core.PlayerManager.GetAlive())
            {
                if (playera == null || !playera.IsValid) continue;

                if (playera.Controller == null || !playera.Controller.IsValid) continue;

                if (playera.Controller.PlayerPawn.Value == null || !playera.Controller.PlayerPawn.IsValid) continue;

                if(iplayer.SteamID == playera.SteamID) continue;

                entity.SetTransmitState(false, playera.PlayerID);


            }
            

            
            // 设置辉光效果（轮廓）
            if (entity.Glow != null)
            {
                entity.Glow.GlowColorOverride = new Color(0, 255, 255, 255);  // 青色辉光
                entity.Glow.GlowRange = 500;  // 辉光范围
                entity.Glow.GlowRangeMin = 0;
                entity.Glow.GlowType = 3;  // 辉光类型
                entity.Glow.GlowTeam = -1;  // 辉光设置（实体本身已限制可见性）
            }

            // 5秒后自动删除预览实体
            _core.Scheduler.DelayBySeconds(5.0f, () =>
            {
                var previewEntity = _core.EntitySystem.GetEntityByIndex<CBaseModelEntity>(entityIndex);
                if (previewEntity != null && previewEntity.IsValid)
                {
                    try
                    {
                        previewEntity.AcceptInput("Kill", 0);
                        _playerPreviewEntities.Remove(player.SteamID);
                        _logger.LogInformation(_translation.GetConsole("preview.entity_removed", entityIndex));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, _translation.GetConsole("preview.entity_remove_failed"));
                    }
                }
            });

            _logger.LogInformation(_translation.GetConsole("preview.showing", player.Controller.PlayerName, modelPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _translation.GetConsole("preview.error", modelPath));
        }
    }

    /// <summary>
    /// 删除旧的预览实体
    /// </summary>
    private void RemoveOldPreview(ulong steamId)
    {
        if (_playerPreviewEntities.TryGetValue(steamId, out var oldEntityIndex))
        {
            var oldEntity = _core.EntitySystem.GetEntityByIndex<CBaseModelEntity>(oldEntityIndex);
            if (oldEntity != null && oldEntity.IsValid)
            {
                try
                {
                    oldEntity.AcceptInput("Kill", 0);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, _translation.GetConsole("preview.old_entity_remove_failed"));
                }
            }
            _playerPreviewEntities.Remove(steamId);
        }
    }

    /// <summary>
    /// 计算前方位置
    /// </summary>
    private static Vector CalculateFrontPosition(Vector position, QAngle angles, float distance)
    {
        float radYaw = angles.Y * (MathF.PI / 180.0f);
        return new Vector
        {
            X = position.X + MathF.Cos(radYaw) * distance,
            Y = position.Y + MathF.Sin(radYaw) * distance,
            Z = position.Z
        };
    }
}
