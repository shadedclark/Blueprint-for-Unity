# Resource Packaging Policy Design

## 背景

当前 BlueprintSystem 的 Resource Asset Manager 已经把 `.resourceblueprint.json` 扫描为运行时 registry，并同步到 Unity Addressables。

现有 Addressables 输出规则是固定的：

```text
Group   = BlueprintResources_{resourceType}
Address = Resource/{resourceType}/{resourceName}
Labels  = ResourceBlueprint, Resource.{resourceType}, ResourceTag.{tag}
```

这能满足基础按类型分组，但不能表达更细的资源分包策略，例如：

- `Hero` 类型按单个英雄分远程包。
- `UI` 类型按 `preloadGroup` 分首包和活动包。
- `NPC` 类型按 `metadata.chapter` 分章节包。
- 测试资源只在 Development 构建中出现。
- 某个资源覆盖类型默认策略。

本文设计一个可落地的资源级/类型级分包策略配置表，使 Resource Asset Manager 在同步 Addressables 时根据策略生成 group、address、label、构建规则和审计报告。

## Unreal 参考

Unreal Asset Manager 的几个关键思想值得借鉴：

- Primary Asset ID 由 `PrimaryAssetType` 和资产名组成，对应本系统的 `resourceType + resourceName`。
- Primary Asset Rules 可以按类型定义，并可被单个 Primary Asset 覆盖。
- `FPrimaryAssetRules` 包含 `Priority`、`ChunkId`、`CookRule` 和 `bApplyRecursively`。其中较高优先级的 Primary Asset 在管理引用资产时有更高权威。
- Chunk ID 用于 cook 时把资产分到不同包；未指定或负数通常落入默认 chunk。
- Primary Asset Label 可以把同一条规则批量应用到显式资产、集合或目录资产。
- Asset Bundles 是 Primary Asset 关联的命名 secondary asset 列表，加载 Primary Asset 时可以指定 bundle。

BlueprintSystem 不需要照搬 Unreal 的 cook pipeline，但应该保留这些产品语义：

```text
类型默认规则 -> 批量选择规则 -> 单资源覆盖
ChunkId / PackageId -> Addressables Group / label / remote catalog
CookRule -> Unity build profile 下是否进入 Addressables build
Priority -> 依赖归属和规则冲突决策
ApplyRecursively -> 依赖资源是否跟随主资源策略
Label -> 批量规则选择器
Bundle -> preloadGroup / dependency group / future bundle scope
```

参考资料：

- Unreal Asset Management: https://dev.epicgames.com/documentation/en-us/unreal-engine/asset-management-in-unreal-engine
- Unreal Cooking and Chunking: https://dev.epicgames.com/documentation/unreal-engine/cooking-content-and-creating-chunks-in-unreal-engine
- `FPrimaryAssetRules`: https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Engine/Engine/FPrimaryAssetRules
- `EPrimaryAssetCookRule`: https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Engine/Engine/EPrimaryAssetCookRule

## 目标

- 支持全局默认、类型级、选择器级、资源级四层策略。
- 默认配置完全兼容当前行为。
- 策略可被 Asset Manager 同步为 Addressables group、entry address、labels 和 group schema。
- 允许用 `resourceType`、`resourceName`、`tags`、`preloadGroups`、`metadata`、`mainAssetType`、`sourcePath` 匹配资源。
- 提供可审计的规则解析报告，能解释每个资源为什么进入某个包。
- 支持 Development / Production profile 下的不同构建规则。
- 给未来远程内容、DLC、热更、章节包、活动包留出扩展点。

## 非目标

- 不在第一期实现完整 Unity build pipeline 替换。
- 不在第一期递归分析所有 Unity 硬引用并重写依赖归属。
- 不要求运行时按策略自动下载远程 catalog。
- 不改变 `resourceType + resourceName` 作为 PrimaryResourceId 的稳定身份。
- 不把 `metadata` 变成强类型业务配置系统；它只是策略匹配和运行时查询的数据源之一。

## 核心概念

### Packaging Policy

项目级分包策略资产，建议路径：

```text
Assets/BlueprintSystem/Resources/BlueprintResourcePackagingPolicy.asset
```

它是 Resource Asset Manager 的唯一默认配置入口。没有该资产时，Asset Manager 自动创建一个兼容当前行为的 policy。

### Packaging Rule

一条可合并规则。字段采用 override 语义：未设置的字段从上一层继承。

建议模型：

```csharp
[Serializable]
public sealed class BlueprintResourcePackagingRule
{
    public int Priority = -1;
    public string PackageId;
    public int ChunkId = -1;
    public BlueprintResourceCookRule CookRule = BlueprintResourceCookRule.Unknown;
    public bool ApplyRecursively;
    public BlueprintResourceDependencyPackagingMode DependencyMode;

    public string GroupTemplate;
    public string AddressTemplate;
    public string[] Labels;

    public BlueprintResourceBundleMode BundleMode;
    public BlueprintResourceCompressionMode Compression;
    public BlueprintResourceDistributionMode Distribution;
    public string BuildPath;
    public string LoadPath;
    public string RemoteCatalog;
    public string ContentVersion;
}
```

Unity 序列化对 nullable 支持不理想，落地时建议用 `OverrideX + ValueX` 或 enum 的 `Inherit/Unknown` 表达继承，而不是直接使用 nullable。

### Rule Source

规则来源分四类，后面的层级覆盖前面的层级：

```text
GlobalDefault
TypeRule
SelectorRule
AssetRule
```

其中 SelectorRule 对应 Unreal Primary Asset Label 的批量规则能力。

### Effective Rule

单个资源最终解析出的规则，写入审计报告，并用于 Addressables 同步。

```text
resource id -> matched rules -> merged effective rule -> Addressables output
```

## 配置结构

### Policy Asset

建议 ScriptableObject：

```csharp
[CreateAssetMenu(menuName = "Blueprint System/Resource Packaging Policy")]
public sealed class BlueprintResourcePackagingPolicyAsset : ScriptableObject
{
    [SerializeField] private string schemaVersion = "0.1";
    [SerializeField] private BlueprintResourcePackagingRule defaultRule;
    [SerializeField] private List<BlueprintResourceTypePackagingRule> typeRules;
    [SerializeField] private List<BlueprintResourceSelectorPackagingRule> selectorRules;
    [SerializeField] private List<BlueprintResourceAssetPackagingRule> assetRules;
    [SerializeField] private List<BlueprintResourceBuildProfile> buildProfiles;
}
```

### Type Rule

类型级规则，类似 Unreal Primary Asset Type 的默认 rules。

```csharp
[Serializable]
public sealed class BlueprintResourceTypePackagingRule
{
    public string ResourceType;
    public BlueprintResourcePackagingRule Rule;
}
```

示例：

```json
{
  "resourceType": "UI",
  "rule": {
    "packageId": "ui",
    "groupTemplate": "BlueprintResources_UI_{preloadGroup:firstOrDefault:Common}",
    "bundleMode": "PackTogether",
    "cookRule": "AlwaysBuild"
  }
}
```

### Selector Rule

批量选择规则，类似 Unreal Primary Asset Label。它不要求改每个资源文件。

```csharp
[Serializable]
public sealed class BlueprintResourceSelectorPackagingRule
{
    public string Name;
    public bool Enabled = true;
    public int Order;
    public BlueprintResourceSelector Selector;
    public BlueprintResourcePackagingRule Rule;
}
```

Selector 支持：

```text
resourceType equals / in
resourceName glob
tags any / all / none
preloadGroups any / all / none
metadata key equals / in / exists / glob
mainAssetType equals / glob
sourcePath glob
```

示例：

```json
{
  "name": "ChapterContent",
  "selector": {
    "resourceTypes": ["NPC", "Map", "Quest"],
    "metadataExists": ["chapter"]
  },
  "rule": {
    "packageId": "chapter-{metadata.chapter}",
    "groupTemplate": "BlueprintResources_Chapter_{metadata.chapter}",
    "chunkIdTemplate": "chapter:{metadata.chapter}",
    "distribution": "Remote",
    "applyRecursively": true
  }
}
```

`chunkIdTemplate` 如果实现成本高，第一期可以只保留 `packageId` 字符串，并额外生成 label `Package.{packageId}`。数值 `ChunkId` 可作为可选兼容字段。

### Asset Rule

资源级精确覆盖，类似 Unreal Primary Asset Rules Overrides。

```csharp
[Serializable]
public sealed class BlueprintResourceAssetPackagingRule
{
    public string ResourceType;
    public string ResourceName;
    public BlueprintResourcePackagingRule Rule;
}
```

示例：

```json
{
  "resourceType": "Hero",
  "resourceName": "Hero_001",
  "rule": {
    "packageId": "hero-001",
    "chunkId": 101,
    "groupTemplate": "BlueprintResources_Hero_001",
    "distribution": "Remote",
    "priority": 100
  }
}
```

### Inline Resource Override

可选扩展：允许 `.resourceblueprint.json` 里写局部 override。

```json
{
  "resourceType": "Hero",
  "resourceName": "Hero_001",
  "packaging": {
    "packageId": "hero-001",
    "chunkId": 101
  }
}
```

建议第一期先不开放 inline override，避免配置分散。等中心策略稳定后再加入。

## 字段语义

### PackageId

字符串包身份，作为 Unity 侧主要抽象。它比 Unreal 的数值 `ChunkId` 更适合 Addressables group 和远程内容命名。

默认值：

```text
{resourceType}
```

建议生成 label：

```text
ResourcePackage.{packageId}
```

### ChunkId

数值包身份，用于对齐 Unreal 术语、审计和外部构建系统。

规则：

- `-1` 表示未指定，继承上层或落入默认包。
- `0` 表示基础包。
- 大于 `0` 表示可独立分发包。

Unity Addressables 本身不需要 ChunkId，但可以生成 label：

```text
ResourceChunk.{chunkId}
```

### CookRule

参考 Unreal `EPrimaryAssetCookRule`，Unity 侧建议命名为 `BlueprintResourceCookRule`：

```csharp
public enum BlueprintResourceCookRule
{
    Unknown,
    NeverBuild,
    ProductionNeverBuild,
    DevelopmentAlwaysProductionNeverBuild,
    DevelopmentAlwaysProductionUnknownBuild,
    AlwaysBuild
}
```

语义：

| Rule | Development | Production |
| --- | --- | --- |
| Unknown | 按引用和 Addressables 默认行为 | 按引用和 Addressables 默认行为 |
| NeverBuild | 不进入构建 | 不进入构建 |
| ProductionNeverBuild | 可被依赖带入 | 不进入构建 |
| DevelopmentAlwaysProductionNeverBuild | 强制进入构建 | 不进入构建 |
| DevelopmentAlwaysProductionUnknownBuild | 强制进入构建 | 按默认行为 |
| AlwaysBuild | 强制进入构建 | 强制进入构建 |

第一期可先实现 `Unknown / NeverBuild / AlwaysBuild / DevelopmentOnly` 四种 UI 展示，内部保留完整枚举。

### Priority

用于两个场景：

- 多条 SelectorRule 同时匹配时，决定谁覆盖谁。
- `ApplyRecursively` 后多个主资源争抢同一依赖资源时，决定哪个主资源拥有管理权。

规则：

- `AssetRule` 默认高于 `SelectorRule`。
- `SelectorRule` 默认高于 `TypeRule`。
- 同层级用 `Priority` 决定。
- `Priority` 相同且输出冲突时，按 `Order` 决定，并产出 warning。

### ApplyRecursively

参考 Unreal `bApplyRecursively`。

第一期建议只作用于 `.resourceblueprint.json` 的显式 `dependencies`，不扫描所有 Unity 硬引用。

后续可扩展为：

```text
None
ResourceDependenciesOnly
AddressablesDependencies
AssetDatabaseDependencies
```

### DependencyMode

控制依赖资源是否跟随当前资源分包。

```csharp
public enum BlueprintResourceDependencyPackagingMode
{
    Inherit,
    AddressablesDefault,
    CoLocateExplicitDependencies,
    CoLocateRequiredDependencies,
    AuditOnly
}
```

第一期默认 `AddressablesDefault`，只审计，不强行移动依赖。

## 模板变量

`GroupTemplate`、`AddressTemplate`、`PackageId` 和 labels 支持模板变量。

必选支持：

```text
{resourceType}
{resourceName}
{displayName}
{packageId}
{chunkId}
{mainAssetType}
```

集合字段：

```text
{tag:firstOrDefault:None}
{preloadGroup:firstOrDefault:Common}
```

metadata：

```text
{metadata.chapter}
{metadata.rarity}
{metadata.chapter:default:Base}
```

路径字段：

```text
{sourceFolder}
{assetFolder}
```

所有模板结果必须经过 Addressables 兼容的 sanitize：

```text
空白 -> _
路径分隔符 -> _
非安全字符 -> _
空结果 -> Default
```

## 规则解析算法

输入：

```text
BlueprintResourceAssetRecord
BlueprintResourcePackagingPolicyAsset
BuildProfile
```

步骤：

1. 从 `defaultRule` 创建 effective rule。
2. 查找匹配 `resourceType` 的 TypeRule，合并非默认字段。
3. 扫描所有 Enabled SelectorRule，收集匹配项。
4. 按 `Priority`、`Order`、`Name` 稳定排序，依次合并。
5. 查找精确 AssetRule，合并。
6. 如开启 inline override，再合并 resource JSON 的 `packaging`。
7. 应用 build profile override。
8. 展开模板，生成 `packageId`、`groupName`、`address`、labels。
9. 运行 validation。
10. 输出 `BlueprintResourceResolvedPackaging`。

合并规则：

```text
unset    -> 保留已有值
inherit  -> 保留已有值
set      -> 覆盖已有值
append labels -> 追加并去重
```

## Addressables 同步设计

当前 `SyncAddressables` 中 group 固定为：

```csharp
EnsureGroup(settings, "BlueprintResources_" + SanitizeLabelPart(record.Source.ResourceType))
```

改为：

```csharp
BlueprintResourceResolvedPackaging packaging = ResolvePackaging(record, policy, profile);
AddressableAssetGroup group = EnsureGroup(settings, packaging.GroupName, packaging.GroupSettings);
AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
entry.address = packaging.Address;
SetLabels(entry, packaging.Labels);
```

GroupSettings 映射：

```text
BundleMode      -> BundledAssetGroupSchema.BundleMode
Compression     -> BundledAssetGroupSchema.Compression
BuildPath       -> BundledAssetGroupSchema.BuildPath
LoadPath        -> BundledAssetGroupSchema.LoadPath
Distribution    -> local/remote path profile
ContentVersion  -> ContentUpdateGroupSchema or custom audit field
```

需要注意：Addressables group 已存在时不应每次无脑覆盖人工设置。建议提供 policy 字段：

```text
groupSettingsMode = CreateOnly | Enforce | AuditOnly
```

第一期默认 `CreateOnly`：

- 新建 group 时写 schema 默认值。
- group 已存在时只移动 entry 和 label。
- 如果现有 group schema 与策略不一致，输出 warning。

## Registry 扩展

运行时加载只依赖 Addressables address，因此最小落地不必扩展 registry。

为了审计和运行时查询，建议新增字段：

```csharp
public string PackageId;
public int ChunkId;
public string PackagingGroup;
public string CookRule;
public string Distribution;
```

这些字段不参与 PrimaryResourceId，不影响旧资源。

## Editor UX

Resource Asset Manager 增加 `Packaging` 视图：

- Policy asset ping / create。
- 当前 build profile 选择。
- 资源列表增加 columns：
  - `PackageId`
  - `ChunkId`
  - `Group`
  - `CookRule`
  - `Distribution`
  - `MatchedRules`
- Detail 面板显示规则解析链：

```text
DefaultRule -> TypeRule(Hero) -> SelectorRule(RemoteHeroes) -> AssetRule(Hero:Hero_001)
```

- 提供 `Dry Run Packaging`：
  - 不写 Addressables。
  - 只输出每个资源的目标 group/address/labels。
  - 显示冲突和会移动的 entry。

- 提供 `Sync All`：
  - Scan resources。
  - Resolve packaging。
  - Validate。
  - Sync Addressables。
  - Write registry。

## Validation

必须阻断的错误：

- `resourceType + resourceName` 不合法。
- 模板变量不存在且没有 default。
- 生成 group/address 为空。
- Addressables address 重复。
- `CookRule = NeverBuild` 的资源被 `AlwaysBuild` 资源 required dependency 引用。
- `AssetRule` 指向不存在资源。
- 同一资源有多个同优先级 selector 输出互斥字段且没有 Order。
- remote distribution 缺少有效 LoadPath。

Warning：

- ChunkId 重复但 PackageId 不同。
- PackageId 相同但 GroupTemplate 解析到不同 group。
- group 已存在且 schema 与策略不一致。
- metadata selector 使用的 key 不在 resource type fields 中声明。
- `ApplyRecursively` 匹配到 dependency，但 dependency 有更高优先级规则。

## 示例策略

### 默认兼容当前行为

```json
{
  "defaultRule": {
    "packageId": "{resourceType}",
    "groupTemplate": "BlueprintResources_{resourceType}",
    "addressTemplate": "Resource/{resourceType}/{resourceName}",
    "labels": [
      "ResourceBlueprint",
      "Resource.{resourceType}",
      "ResourcePackage.{packageId}"
    ],
    "cookRule": "Unknown",
    "bundleMode": "PackTogether"
  }
}
```

### UI 首包和活动包

```json
{
  "typeRules": [
    {
      "resourceType": "UI",
      "rule": {
        "packageId": "ui-{preloadGroup:firstOrDefault:Common}",
        "groupTemplate": "BlueprintResources_UI_{preloadGroup:firstOrDefault:Common}",
        "chunkId": 0,
        "cookRule": "AlwaysBuild",
        "distribution": "Local"
      }
    }
  ]
}
```

### 英雄资源按英雄分远程包

```json
{
  "selectorRules": [
    {
      "name": "RemoteHeroPackages",
      "priority": 50,
      "selector": {
        "resourceTypes": ["Hero"]
      },
      "rule": {
        "packageId": "hero-{resourceName}",
        "groupTemplate": "BlueprintResources_Hero_{resourceName}",
        "distribution": "Remote",
        "loadPath": "{RemoteLoadPath}/heroes/{resourceName}",
        "applyRecursively": true,
        "dependencyMode": "CoLocateRequiredDependencies"
      }
    }
  ]
}
```

### 章节内容按 metadata 分包

```json
{
  "selectorRules": [
    {
      "name": "ChapterPackages",
      "priority": 40,
      "selector": {
        "resourceTypes": ["NPC", "Quest", "Map"],
        "metadataExists": ["chapter"]
      },
      "rule": {
        "packageId": "chapter-{metadata.chapter}",
        "groupTemplate": "BlueprintResources_Chapter_{metadata.chapter}",
        "distribution": "Remote",
        "labels": ["Chapter.{metadata.chapter}"]
      }
    }
  ]
}
```

### 测试资源只进开发包

```json
{
  "selectorRules": [
    {
      "name": "DevOnlyResources",
      "priority": 100,
      "selector": {
        "tagsAny": ["DevOnly", "Test"]
      },
      "rule": {
        "cookRule": "DevelopmentAlwaysProductionNeverBuild",
        "groupTemplate": "BlueprintResources_DevOnly",
        "distribution": "Local"
      }
    }
  ]
}
```

## 实施计划

### Phase 1: 策略资产和兼容同步

- 新增 packaging policy ScriptableObject 和模型。
- Asset Manager 创建默认 policy。
- `SyncAddressables` 通过 resolved policy 生成 group/address/labels。
- 支持 defaultRule、typeRules、assetRules。
- 支持 `PackageId`、`ChunkId`、`CookRule`、`GroupTemplate`、`AddressTemplate`、`Labels`。
- 默认输出完全等价当前行为。
- 增加 dry-run report 和 editor tests。

### Phase 2: SelectorRule

- 支持 tags、preloadGroups、metadata、sourcePath、mainAssetType 选择器。
- 支持 priority/order 合并。
- Packaging 视图显示 matched rules。
- 对 metadata key 与 resource type fields 做 warning。

### Phase 3: Build Profile 和 CookRule

- 增加 Development / Production profile。
- `NeverBuild` 和 dev-only 规则参与 Addressables sync/filter。
- 生产构建前 validation 阻断违规资源。
- 支持 local/remote distribution 基础 schema。

### Phase 4: Dependency Ownership

- 第一版只处理 `.resourceblueprint.json` 显式 dependencies。
- 实现 `ApplyRecursively` 和 dependency ownership audit。
- 高优先级主资源可接管低优先级依赖的 package。
- 冲突时输出可解释报告。

### Phase 5: 深度 Addressables/AssetDatabase 分析

- 可选扫描 Unity 资产硬引用。
- 输出类似 Unreal Asset Audit 的包体/引用报告。
- 支持 duplicate dependency 风险提示。
- 结合 bundle size 估算优化分包。

## 测试计划

Editor tests：

- 没有 policy 时生成默认 policy，Addressables group 与当前规则一致。
- TypeRule 覆盖默认 group。
- AssetRule 覆盖 TypeRule。
- 多个 SelectorRule 按 priority/order 合并。
- metadata selector 能匹配和不匹配资源。
- 模板变量 sanitize 后生成稳定 group/address。
- address 冲突报 error。
- `NeverBuild` 被 required dependency 引用时报 error。
- group 已存在时 `CreateOnly` 不覆盖 schema，只 warning。

Golden tests：

- 输入一组 resource JSON 和 policy asset。
- 输出 resolved packaging snapshot。
- 确保后续重构不改变分包解析结果。

## 与现有系统的兼容性

- 默认 policy 保持当前分组和地址规则。
- 旧 `.resourceblueprint.json` 不需要改。
- 旧 runtime 仍然通过 `MainAssetAddress` 加载资源。
- 新增 registry 字段只做审计和可选运行时查询。
- Resource Type Catalog 的 `fields` 可以继续作为 metadata schema，Packaging selector 可复用 metadata。

## 风险

- Addressables group schema 如果被策略强制覆盖，可能覆盖人工调优。默认应采用 `CreateOnly`。
- metadata 驱动分包会让内容作者修改 metadata 时触发包移动，需要在 dry-run 中明确显示。
- 过度细分 group 会增加包数量和 catalog 复杂度。
- `ApplyRecursively` 如果过早扫描 Unity 硬引用，可能引入大量误移动。应先从显式 resource dependencies 开始。
- CookRule 与 Unity Addressables 构建 profile 的语义不是完全等价，需要用项目 build profile 明确解释。

## 推荐第一版决策

- 配置入口只做中心 policy asset，不先做 inline resource override。
- 默认保持 `BlueprintResources_{resourceType}`。
- 第一版实现 default/type/asset 三层，SelectorRule 放第二期。
- 第一版只移动主资源 Addressables entry，不接管依赖。
- 第一版把 ChunkId 作为审计和 label，不直接依赖 Unity 内部 chunk。
- Editor 必须先有 dry-run，再允许 sync。
