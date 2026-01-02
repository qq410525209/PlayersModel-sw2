using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using PlayersModel.Config;

namespace PlayersModel.Services;

public interface IPreviewService
{
    void ShowPreview(IPlayer player, string modelPath);
}

public class PreviewService : IPreviewService
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<PreviewService> _logger;
    
    // 跟踪每个玩家的预览实体
    private readonly Dictionary<ulong, uint> _playerPreviewEntities = new();

    public PreviewService(ISwiftlyCore core, ILogger<PreviewService> logger)
    {
        _core = core;
        _logger = logger;
    }

    /// <summary>
    /// 显示模型预览（参考CSSharp cs2-store实现）
    /// </summary>
    public void ShowPreview(IPlayer player, string modelPath)
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
                _logger.LogWarning("无法创建预览实体");
                return;
            }

            var pawn = player.Pawn;
            if (pawn?.AbsOrigin == null || pawn?.AbsRotation == null) return;

            // 计算玩家前方100单位的位置
            var spawnPos = CalculateFrontPosition(pawn.AbsOrigin.Value, pawn.AbsRotation.Value, 100.0f);

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

            // 下一帧设置模型
            _core.Scheduler.DelayBySeconds(0.0f, () =>
            {
                var previewEntity = _core.EntitySystem.GetEntityByIndex<CBaseModelEntity>(entityIndex);
                if (previewEntity != null && previewEntity.IsValid)
                {
                    previewEntity.AcceptInput("SetModel", modelPath, null, null, 0);
                }
            });

            // 开始旋转动画（1秒后开始，持续4秒）
            _core.Scheduler.DelayBySeconds(1.0f, () => RotateEntity(player.SteamID, entityIndex, 0.0f));

            _logger.LogInformation($"玩家 {player.Controller.PlayerName} 预览模型: {modelPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"预览模型时出错: {modelPath}");
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
                    oldEntity.AcceptInput("Kill", "", null, null, 0);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "删除旧预览实体失败");
                }
            }
            _playerPreviewEntities.Remove(steamId);
        }
    }

    /// <summary>
    /// 旋转预览实体
    /// </summary>
    private void RotateEntity(ulong steamId, uint entityIndex, float elapsed)
    {
        var entity = _core.EntitySystem.GetEntityByIndex<CBaseModelEntity>(entityIndex);
        if (entity == null || !entity.IsValid)
        {
            _playerPreviewEntities.Remove(steamId);
            return;
        }

        const float totalTime = 4.0f;
        const float totalRotation = 360.0f;
        const float interval = 0.04f;
        float rotationStep = (interval / totalTime) * totalRotation;

        var currentAngles = entity.AbsRotation;
        if (currentAngles != null)
        {
            entity.Teleport(null, new QAngle(
                currentAngles.Value.X,
                currentAngles.Value.Y + rotationStep,
                currentAngles.Value.Z
            ), null);
        }

        if (elapsed < totalTime)
        {
            _core.Scheduler.DelayBySeconds(interval, () => RotateEntity(steamId, entityIndex, elapsed + interval));
        }
        else
        {
            // 旋转完成，删除实体
            _playerPreviewEntities.Remove(steamId);
            if (entity.IsValid)
            {
                try
                {
                    entity.AcceptInput("Kill", "", null, null, 0);
                    _logger.LogInformation($"预览实体旋转完成并删除，索引: {entityIndex}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "删除预览实体失败");
                }
            }
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