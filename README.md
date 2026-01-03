# PlayersModel

<div align="center">
  <h3>🎭 Advanced Player Model System for CS2 SwiftlyS2</h3>
  <p>A comprehensive player model management plugin with economy integration, preview system, and intuitive menu interface</p>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/badge/.NET-10.0-blue" alt=".NET Version">
  <img src="https://img.shields.io/badge/SwiftlyS2-Compatible-purple" alt="SwiftlyS2">
  <img src="https://img.shields.io/github/license/SwiftlyS2-Plugins/PlayersModel" alt="License">
</p>

[中文文档](README_CN.md) | [English](README.md)

---

## 📋 Table of Contents

- [Features](#-features)
- [Requirements](#-requirements)
- [Installation](#-installation)
- [Configuration](#-configuration)
- [Commands](#-commands)
- [Menu System](#-menu-system)
- [Database](#-database)
- [Building](#-building)
- [License](#-license)

---

## ✨ Features

### 🎨 Core Features
- **Multi-Team Support**: Separate models for CT, T, and universal (All) teams
- **Economy Integration**: Purchase models using the Economy plugin's currency system
- **Preview System**: 3D model preview before purchase (5-second display at 50 units)
- **Persistent Storage**: Player-owned models and current selections saved to database
- **Permission System**: VIP-only models and permission-based access control
- **Default Models**: Automatic restoration to default models when unequipping

### 🎯 Menu System
- **Four-Tier Menu Structure**:
  - Main Menu: Universal Models, CT Models, T Models, My Models
  - Category Menus: List of available models
  - Model Details: Description, team info, ownership status, actions
  - Navigation: Easy back-and-forth navigation

### 🛠️ Admin Tools
- Give credits to players
- Grant models to players
- Set player models directly
- Reload configuration without restart
- View player-owned models

### 🌐 Localization
- Multi-language support (English, Simplified Chinese)
- Player-specific language settings
- Easy translation system

---

## 📦 Requirements

### Dependencies
- **SwiftlyS2**: Latest version
- **Economy Plugin**: For purchase functionality (optional but recommended)
- **MySQL/MariaDB/PostgreSQL**: For data persistence

### System Requirements
- **.NET 10.0** or higher
- **CS2 Server** with SwiftlyS2 installed

---

## 🚀 Installation

1. **Download** the latest release from the releases page
2. **Extract** the plugin files to your SwiftlyS2 plugins directory:
   ```
   csgo/swiftly/plugins/PlayersModel/
   ```
3. **Configure** the database connection in your SwiftlyS2 configuration
4. **Restart** the server or load the plugin using SwiftlyS2 commands

### Directory Structure
```
PlayersModel/
├── PlayersModel.dll          # Main plugin assembly
├── Economy.Contract.dll      # Economy API contract
├── config.jsonc              # Main configuration
├── models.jsonc              # Model definitions
└── translations/
    ├── en.jsonc              # English translations
    └── zh-CN.jsonc           # Simplified Chinese translations
```

---

## ⚙️ Configuration

### config.jsonc

```jsonc
{
  "Main": {
    "Enabled": true,
    "Debug": false,
    "Language": "",  // Leave empty to use player's framework language
    "EnablePurchaseSystem": true,
    "WalletKind": "credits",
    
    // Default model paths (used when unequipping)
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

### models.jsonc

```jsonc
{
  "Models": [
    {
      "ModelId": "gign",
      "DisplayName": "GIGN Operator",
      "Description": "French National Gendarmerie Intervention Group",
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
      "DisplayName": "Phoenix Connexion",
      "Description": "Elite mercenary unit",
      "ModelPath": "characters/models/tm_phoenix/tm_phoenix.vmdl",
      "ArmsPath": "",
      "Team": "T",
      "Price": 0,  // Free model
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

## 🎮 Commands

### Player Commands

| Command | Description | Usage |
|---------|-------------|-------|
| `!model` | Open model selection menu | `!model` |
| `!buymodel` | Purchase a specific model | `!buymodel <modelId>` |
| `!balance` | Check your credit balance | `!balance` |
| `!mymodels` | View your owned models | `!mymodels` |

### Admin Commands

| Command | Description | Usage | Permission |
|---------|-------------|-------|------------|
| `!pm_givecredits` | Give credits to a player | `!pm_givecredits <player> <amount>` | Admin |
| `!pm_givemodel` | Give a model to a player | `!pm_givemodel <player> <modelId>` | Admin |
| `!pm_setmodel` | Set a player's model | `!pm_setmodel <player> <modelId>` | Admin |
| `!pm_reload` | Reload configuration | `!pm_reload` | Admin |
| `!pm_listmodels` | List player's owned models | `!pm_listmodels <player>` | Admin |

---

## 📱 Menu System

### Main Menu
```
Player Model Selection
├── Universal Models    → Models for all teams
├── CT Models          → Counter-Terrorist models
├── T Models           → Terrorist models
└── My Models          → Your purchased models
```

### Model Details Menu
```
📦 Model Name
------------------
Description text
Team: CT
🔍 Preview Model
💰 Purchase (5000 credits)  or  ✅ Equip Model  or  ❌ Unequip Model
```

---

## 💾 Database

The plugin automatically creates the following tables:

### player_owned_models
Stores models owned by players.

| Column | Type | Description |
|--------|------|-------------|
| steam_id | BIGINT | Player's Steam ID |
| model_id | VARCHAR(64) | Model identifier |
| purchased_at | TIMESTAMP | Purchase timestamp |

### player_current_models
Stores currently equipped models.

| Column | Type | Description |
|--------|------|-------------|
| steam_id | BIGINT | Player's Steam ID |
| model_path | VARCHAR(255) | Current model path |
| arms_path | VARCHAR(255) | Current arms model path |
| updated_at | TIMESTAMP | Last update timestamp |

---

## 🔨 Building

### Prerequisites
- .NET SDK 10.0 or higher
- SwiftlyS2 development environment

### Build Steps

1. **Clone the repository**:
   ```bash
   git clone https://github.com/YourUsername/PlayersModel.git
   cd PlayersModel
   ```

2. **Build the project**:
   ```bash
   dotnet build -c Release
   ```

3. **Publish the plugin**:
   ```bash
   dotnet publish -c Release
   ```

4. **Output files** will be in:
   ```
   build/publish/
   ```

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 👤 Author

**小彩旗**

- GitHub: [@YourGitHub](https://github.com/YourGitHub)

---

## 🙏 Credits

- **SwiftlyS2**: The amazing CS2 plugin framework
- **Economy Plugin**: For currency system integration
- **Community**: For testing and feedback

---

## 📞 Support

If you encounter any issues or have questions:

1. Check the [Documentation](#-configuration)
2. Review existing [Issues](https://github.com/YourRepo/PlayersModel/issues)
3. Create a new issue with detailed information

---

<div align="center">
  <p>Made with ❤️ for the CS2 SwiftlyS2 community</p>
</div>