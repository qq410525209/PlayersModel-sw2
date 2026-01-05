# MeshGroup 组件配置功能使用指南

## 功能概述

MeshGroup 功能允许你为模型配置可自定义的组件（如帽子、武器、装饰品等），玩家可以选择装备或卸载这些组件。

## 配置说明

### 1. 基本配置结构

在 `resources/templates/models.jsonc` 中，为模型添加 `MeshGroups` 配置：

```jsonc
{
  "ModelId": "your_model_id",
  "DisplayName": "你的模型名称",
  // ... 其他模型配置 ...
  "MeshGroups": [
    {
      "ComponentId": "hat",              // 组件唯一ID（用于存储）
      "BodyGroupName": "Hat",            // 模型中的实际 BodyGroup 名称
      "DisplayName": "帽子",             // 组件显示名称
      "Description": "头部装饰",          // 组件描述（部位信息）
      "Price": 500,                      // 组件基础价格
      "Options": [                       // 可用选项列表
        {
          "OptionId": "no_hat",          // 选项ID
          "DisplayName": "无帽子",        // 选项显示名称
          "Index": 0,                    // BodyGroup 索引（0通常表示隐藏）
          "AdditionalPrice": 0,          // 此选项的额外价格
          "IsDefault": true              // 是否为默认选项
        },
        {
          "OptionId": "baseball_cap",
          "DisplayName": "棒球帽",
          "Index": 1,                    // 索引1表示第一个变体
          "AdditionalPrice": 100,
          "IsDefault": false
        }
      ]
    }
  ]
}
```

### 2. 配置要素说明

#### MeshGroupConfig（组件配置）
- **ComponentId**: 组件唯一标识符，用于数据库存储
- **BodyGroupName**: 模型文件中定义的实际 BodyGroup 名称
- **DisplayName**: 在菜单中显示给玩家的名称
- **Description**: 组件描述，说明组件的部位（如：头部、身体、武器等）
- **Price**: 组件的基础价格（0表示免费）
- **Options**: 该组件的所有可用选项列表

#### MeshGroupOption（选项配置）
- **OptionId**: 选项唯一标识符
- **DisplayName**: 选项显示名称
- **Index**: BodyGroup 索引值
  - `0` 通常表示隐藏/卸载该组件
  - `1, 2, 3...` 表示不同的组件变体
- **AdditionalPrice**: 在组件基础价格之上的额外价格
- **IsDefault**: 是否为默认选项（装备模型时自动应用）

### 3. 工作原理

当玩家装备模型时：
1. 系统会自动应用模型的基础外观
2. 如果模型配置了 MeshGroups，系统会自动应用所有标记为 `IsDefault: true` 的选项
3. 使用 CS2 的 `AcceptInput("SetBodyGroup")` 命令来切换组件

底层实现：
```csharp
// 格式: "BodyGroupName,Index"
entity.AcceptInput("SetBodyGroup", activator: entity, caller: entity, value: "Hat,1");
```

### 4. 完整示例

```jsonc
{
  "ModelId": "soldier_custom",
  "DisplayName": "可自定义士兵",
  "ModelPath": "characters/models/soldier/soldier.vmdl",
  "Team": "CT",
  "Price": 1000,
  "MeshGroups": [
    {
      "ComponentId": "helmet",
      "BodyGroupName": "Helmet",
      "DisplayName": "头盔",
      "Description": "头部防护装备",
      "Price": 300,
      "Options": [
        {
          "OptionId": "no_helmet",
          "DisplayName": "无头盔",
          "Index": 0,
          "AdditionalPrice": 0,
          "IsDefault": false
        },
        {
          "OptionId": "light_helmet",
          "DisplayName": "轻型头盔",
          "Index": 1,
          "AdditionalPrice": 100,
          "IsDefault": true
        },
        {
          "OptionId": "heavy_helmet",
          "DisplayName": "重型头盔",
          "Index": 2,
          "AdditionalPrice": 200,
          "IsDefault": false
        }
      ]
    },
    {
      "ComponentId": "vest",
      "BodyGroupName": "Vest",
      "DisplayName": "防弹衣",
      "Description": "身体防护装备",
      "Price": 500,
      "Options": [
        {
          "OptionId": "no_vest",
          "DisplayName": "无防弹衣",
          "Index": 0,
          "AdditionalPrice": 0,
          "IsDefault": false
        },
        {
          "OptionId": "standard_vest",
          "DisplayName": "标准防弹衣",
          "Index": 1,
          "AdditionalPrice": 0,
          "IsDefault": true
        }
      ]
    }
  ]
}
```

### 5. 注意事项

1. **可选功能**: 如果模型没有配置 `MeshGroups`，系统会正常应用模型，不会进行 MeshGroup 操作
2. **BodyGroup 名称**: `BodyGroupName` 必须与模型文件中定义的实际 BodyGroup 名称完全匹配
3. **索引值**: `Index` 的有效值取决于模型文件中该 BodyGroup 定义的选项数量
4. **默认选项**: 每个组件至少应该有一个 `IsDefault: true` 的选项
5. **价格系统**: 总价格 = 组件基础价格 + 选项额外价格

### 6. 服务说明

系统提供了专门的 `MeshGroupService` 来管理组件：

- `ApplyMeshGroupToPlayer()`: 应用单个组件到玩家
- `ApplyDefaultMeshGroups()`: 应用模型的所有默认组件
- `ToggleMeshGroupOption()`: 切换组件选项
- `GetModelMeshGroups()`: 获取模型的组件配置

## 技术实现

### 代码示例

```csharp
// 应用 MeshGroup
meshGroupService.ApplyMeshGroupToPlayer(player, "Hat", 1);

// 应用模型的所有默认组件
meshGroupService.ApplyDefaultMeshGroups(player, model);

// 切换组件选项
meshGroupService.ToggleMeshGroupOption(player, "soldier_custom", "helmet", "heavy_helmet");
```

### 数据结构

```csharp
// MeshGroup 配置
public class MeshGroupConfig
{
    public string ComponentId { get; set; }
    public string BodyGroupName { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public int Price { get; set; }
    public List<MeshGroupOption> Options { get; set; }
}

// MeshGroup 选项
public class MeshGroupOption
{
    public string OptionId { get; set; }
    public string DisplayName { get; set; }
    public int Index { get; set; }
    public int AdditionalPrice { get; set; }
    public bool IsDefault { get; set; }
}
```

## 未来扩展

可以进一步实现：
- 菜单界面让玩家选择组件
- 数据库存储玩家的组件配置
- 组件购买系统（基于 Economy 插件）
- 组件权限系统（VIP专属组件等）