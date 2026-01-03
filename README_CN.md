# PlayersModel - 玩家模型系统

<div align="center">
  <h3>🎭 CS2 SwiftlyS2 高级玩家模型管理系统</h3>
  <p>集成经济系统、预览功能和直观菜单界面的综合性玩家模型管理插件</p>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="构建状态">
  <img src="https://img.shields.io/badge/.NET-10.0-blue" alt=".NET 版本">
  <img src="https://img.shields.io/badge/SwiftlyS2-兼容-purple" alt="SwiftlyS2">
  <img src="https://img.shields.io/github/license/SwiftlyS2-Plugins/PlayersModel" alt="许可证">
</p>

[中文文档](README_CN.md) | [English](README.md)

---

## 📋 目录

- [功能特性](#-功能特性)
- [系统要求](#-系统要求)
- [安装说明](#-安装说明)
- [配置指南](#-配置指南)
- [命令列表](#-命令列表)
- [菜单系统](#-菜单系统)
- [数据库](#-数据库)
- [编译构建](#-编译构建)
- [许可证](#-许可证)

---

## ✨ 功能特性

### 🎨 核心功能
- **多阵营支持**：为CT、T和通用（All）阵营提供独立模型
- **经济系统集成**：使用Economy插件的货币系统购买模型
- **预览系统**：购买前3D模型预览（50单位距离展示5秒）
- **持久化存储**：玩家拥有的模型和当前选择保存到数据库
- **权限系统**：VIP专属模型和基于权限的访问控制
- **默认模型**：卸载模型时自动恢复到默认模型

### 🎯 菜单系统
- **四级菜单结构**：
  - 主菜单：通用模型、CT模型、T模型、我的模型
  - 分类菜单：可用模型列表
  - 模型详情：描述、阵营信息、所有权状态、操作按钮
  - 导航系统：便捷的前后导航

### 🛠️ 管理工具
- 给予玩家积分
- 赠送模型给玩家
- 直接设置玩家模型
- 无需重启重载配置
- 查看玩家拥有的模型

### 🌐 本地化
- 多语言支持（英语、简体中文）
- 玩家专属语言设置
- 简易翻译系统

---

## 📦 系统要求

### 依赖项
- **SwiftlyS2**：最新版本
- **Economy插件**：用于购买功能（可选但推荐）
- **MySQL/MariaDB/PostgreSQL**：用于数据持久化

### 系统需求
- **.NET 10.0** 或更高版本
- 已安装SwiftlyS2的**CS2服务器**

---

## 🚀 安装说明

1. **下载**最新版本的插件
2. **解压**插件文件到SwiftlyS2插件目录：
   ```
   csgo/swiftly/plugins/PlayersModel/
   ```
3. **配置**SwiftlyS2配置中的数据库连接
4. **重启**服务器或使用SwiftlyS2命令加载插件

### 目录结构
```
PlayersModel/
├── PlayersModel.dll          # 主插件程序集
├── Economy.Contract.dll      # 经济API契约
├── config.jsonc              # 主配置文件
├── models.jsonc              # 模型定义文件
└── translations/
    ├── en.jsonc              # 英语翻译
    └── zh-CN.jsonc           # 简体中文翻译
```

---

## ⚙️ 配置指南

### config.jsonc（主配置文件）

```jsonc
{
  "Main": {
    "Enabled": true,
    "Debug": false,
    "Language": "",  // 留空则使用玩家的框架语言
    "EnablePurchaseSystem": true,
    "WalletKind": "credits",
    
    // 默认模型路径（卸载时使用）
    "DefaultCTModelPath": "characters/models/ctm_sas/ctm_sas.vmdl",
    "DefaultTModelPath": "characters/models/tm_phoenix/tm_phoenix.vmdl",
    
    "Permissions": {
      "AdminPermissions": ["root", "admin"],
      "VipPermissions": ["vip", "*.vip"]
    },
    
    "Menu": {
      "EnableSound": true,
      "ItemsPerPage": 5,
      "FreezePlayer": false,
      "AutoCloseDelay": 0.0
    },
    
    "Database": {
      "ConnectionKey": "economyapi",
      "OwnedModelsTable": "player_owned_models",
      "CurrentModelsTable": "player_current_models",
      "AutoCreateTables": true
    },
    
    "Commands": {
      "Player": {
        "Model": "model",
        "BuyModel": "buymodel",
        "Balance": "balance",
        "MyModels": "mymodels"
      },
      "Admin": {
        "GiveCredits": "pm_givecredits",
        "GiveModel": "pm_givemodel",
        "SetModel": "pm_setmodel",
        "ReloadConfig": "pm_reload",
        "ListPlayerModels": "pm_listmodels"
      }
    }
  }
}
```

### models.jsonc（模型配置文件）

```jsonc
{
  "Models": [
    {
      "ModelId": "gign",
      "DisplayName": "GIGN 特警",
      "Description": "法国国家宪兵干预队",
      "ModelPath": "characters/models/ctm_gign/ctm_gign.vmdl",
      "ArmsPath": "",
      "Team": "CT",
      "Price": 5000,
      "VipOnly": false,
      "RequiredPermission": "",
      "AllowedSteamIds": [],
      "Enabled": true,
      "Priority": 1
    },
    {
      "ModelId": "phoenix",
      "DisplayName": "凤凰连线",
      "Description": "精英雇佣兵部队",
      "ModelPath": "characters/models/tm_phoenix/tm_phoenix.vmdl",
      "ArmsPath": "",
      "Team": "T",
      "Price": 0,  // 免费模型
      "VipOnly": false,
      "RequiredPermission": "",
      "AllowedSteamIds": [],
      "Enabled": true,
      "Priority": 1
    }
  ]
}
```

---

## 🎮 命令列表

### 玩家命令

| 命令 | 描述 | 用法 |
|------|------|------|
| `!model` | 打开模型选择菜单 | `!model` |
| `!buymodel` | 购买指定模型 | `!buymodel <模型ID>` |
| `!balance` | 查看积分余额 | `!balance` |
| `!mymodels` | 查看拥有的模型 | `!mymodels` |

### 管理员命令

| 命令 | 描述 | 用法 | 权限要求 |
|------|------|------|---------|
| `!pm_givecredits` | 给予玩家积分 | `!pm_givecredits <玩家名> <数量>` | 管理员 |
| `!pm_givemodel` | 赠送模型给玩家 | `!pm_givemodel <玩家名> <模型ID>` | 管理员 |
| `!pm_setmodel` | 设置玩家模型 | `!pm_setmodel <玩家名> <模型ID>` | 管理员 |
| `!pm_reload` | 重载配置 | `!pm_reload` | 管理员 |
| `!pm_listmodels` | 查看玩家拥有的模型 | `!pm_listmodels <玩家名>` | 管理员 |

---

## 📱 菜单系统

### 主菜单
```
玩家模型选择
├── 通用模型           → 所有阵营可用的模型
├── CT模型            → 反恐精英模型
├── T模型             → 恐怖分子模型
└── 我的模型          → 你已购买的模型
```

### 模型详情菜单
```
📦 模型名称
------------------
模型描述文本
阵营: CT
🔍 预览模型
💰 购买 (5000 credits)  或  ✅ 装备模型  或  ❌ 卸载模型
```

---

## 💾 数据库

插件会自动创建以下数据表：

### player_owned_models（玩家拥有的模型）
存储玩家购买的模型。

| 字段 | 类型 | 说明 |
|------|------|------|
| steam_id | BIGINT | 玩家Steam ID |
| model_id | VARCHAR(64) | 模型标识符 |
| purchased_at | TIMESTAMP | 购买时间戳 |

### player_current_models（当前装备的模型）
存储当前装备的模型。

| 字段 | 类型 | 说明 |
|------|------|------|
| steam_id | BIGINT | 玩家Steam ID |
| model_path | VARCHAR(255) | 当前模型路径 |
| arms_path | VARCHAR(255) | 当前手臂模型路径 |
| updated_at | TIMESTAMP | 最后更新时间 |

---

## 🔨 编译构建

### 前置要求
- .NET SDK 10.0 或更高版本
- SwiftlyS2 开发环境

### 构建步骤

1. **克隆仓库**：
   ```bash
   git clone https://github.com/你的用户名/PlayersModel.git
   cd PlayersModel
   ```

2. **构建项目**：
   ```bash
   dotnet build -c Release
   ```

3. **发布插件**：
   ```bash
   dotnet publish -c Release
   ```

4. **输出文件**位于：
   ```
   build/publish/
   ```

---

## 🤝 贡献

欢迎贡献！请随时提交Pull Request。

---

## 📄 许可证

本项目基于MIT许可证 - 查看[LICENSE](LICENSE)文件了解详情。

---

## 👤 作者

**小彩旗**

- GitHub: [@YourGitHub](https://github.com/YourGitHub)

---

## 🙏 致谢

- **SwiftlyS2**：出色的CS2插件框架
- **Economy插件**：货币系统集成
- **社区**：测试和反馈支持

---

## 📞 支持

如果遇到任何问题或有疑问：

1. 查看[配置指南](#-配置指南)
2. 查看现有的[Issues](https://github.com/你的仓库/PlayersModel/issues)
3. 创建新issue并提供详细信息

---

<div align="center">
  <p>用 ❤️ 为CS2 SwiftlyS2社区打造</p>
</div>