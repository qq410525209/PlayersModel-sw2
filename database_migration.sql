-- PlayersModel 数据库迁移脚本
-- 从旧结构迁移到新结构

-- 备份旧表（可选）
-- CREATE TABLE player_current_models_backup AS SELECT * FROM player_current_models;

-- 删除旧表并创建新表
DROP TABLE IF EXISTS player_current_models;

CREATE TABLE player_current_models (
    steam_id BIGINT PRIMARY KEY,
    model_path VARCHAR(255),
    arms_path VARCHAR(255),
    equipped_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 完成！
-- 旧数据已清除，新结构已创建