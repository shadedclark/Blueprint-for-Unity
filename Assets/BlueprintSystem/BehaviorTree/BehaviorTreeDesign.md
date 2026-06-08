# Behavior Tree Design

这份文档给 coding agent 说明 Behavior Tree 系统的设计边界、目录归位、资产格式和整棵树的设计规范。具体节点参数、当前支持的 `BT.*` 类型和运行时细节以 `Assets/BlueprintSystem/BehaviorTree/GUIDE.md` 为准。

## 系统边界

Behavior Tree 是 BlueprintSystem 下的专用 AI 决策图，不是普通 Blueprint exec 图的变体。

核心边界：

- Behavior Tree 负责周期性决策，节点 Tick 后返回 `Success`、`Failure` 或 `Running`。
- 普通 Blueprint 负责事件驱动逻辑、UI、输入、动画事件、伤害结算和一次性流程。
- Behavior Tree runtime 不复用普通 Blueprint VM 的 exec 队列。
- `.btree.json` 是行为树源数据；`.btgraph` 是 Graph Toolkit 可视化/缓存资产；`.compiled.asset` 是运行时资产。
- 运行时代码不得依赖 `UnityEditor`、Graph Toolkit 或编辑器窗口。
- Behavior Tree JSON 不保存 Unity 场景对象直接引用，场景对象通过 Blackboard、Runner override、绑定名或组件解析获得。

推荐数据流：

```text
.btree.json
  -> Behavior Tree Graph Toolkit view/cache (.btgraph)
  -> Behavior Tree validator/compiler
  -> BehaviorTreeCompiledAsset (.compiled.asset)
  -> BehaviorTreeRunner
  -> BehaviorTreeRuntime
  -> Blackboard
  -> BT executor / Blueprint bridge / Unity component
```

## 目录规范

框架目录：

```text
Assets/BlueprintSystem/BehaviorTree/
```

放置 Behavior Tree 框架文档、运行时、编辑器和样例入口。不要把项目业务 AI 行为树放到这里。

```text
Assets/BlueprintSystem/BehaviorTree/GUIDE.md
```

当前节点、JSON shape、Blackboard 读写、编译和验证规则的权威说明。Agent 在创建或修改 `.btree.json` 前应先读这个文件。

```text
Assets/BlueprintSystem/BehaviorTree/BehaviorTreeDesign.md
```

行为树系统设计约束和树设计规范。用于帮助 agent 判断目录归位、树结构组织和系统边界。

```text
Assets/BlueprintSystem/BehaviorTree/Runtime/
```

Behavior Tree 运行时模型、Blackboard、Compiler、CompiledAsset、Runner 和 `BT.*` executor registry。新增或修改 Behavior Tree 运行时能力时放这里。

```text
Assets/BlueprintSystem/BehaviorTree/Editor/
Assets/BlueprintSystem/BehaviorTree/Editor/GraphToolkit/
```

Behavior Tree 编辑器支持、Inspector、编译入口、Graph Toolkit 可视化图、导入导出桥接和视觉节点元数据。只放编辑器代码。

```text
Assets/BlueprintSystem/Executors/BehaviorTree/
Assets/BlueprintSystem/Specs/Nodes/BehaviorTree.*.node.json
```

普通 Blueprint 访问 Behavior Tree Blackboard 的桥接节点。这里的 `.node.json` 属于普通 Blueprint 节点 manifest，不是 Behavior Tree 节点声明。不要把 `BT.*` 行为树节点加到普通 Blueprint `Specs/Nodes` 里。

样例目录：

```text
Assets/BlueprintSystem/Samples~/BehaviorTree/
Assets/Samples/BlueprintSystem/BehaviorTree/
```

放包内样例、演示 prefab、样例 `.btree.json`、`.btgraph`、`.compiled.asset`、可视化 Blueprint 和说明文档。

项目业务目录：

```text
Assets/Game/Blueprint/<FeatureName>/Behavior/
```

放具体游戏功能的行为树源数据和相关行为 Blueprint。新建项目 AI 行为树时优先使用这个目录。

推荐命名：

```text
Assets/Game/Blueprint/<FeatureName>/Behavior/<AIName>Behavior.btree.json
Assets/Game/Blueprint/<FeatureName>/Behavior/<AIName>PatrolBehavior.btree.json
Assets/Game/Blueprint/<FeatureName>/Behavior/<AIName>CombatBehavior.btree.json
```

不要把项目业务树写进 `Assets/BlueprintSystem/**`。`BlueprintSystem` 目录只承载框架、工具、文档和 samples。

Agent 说明目录：

```text
Assets/BlueprintSystem/Agents/AIBehaviorTreeAgent.md
Assets/BlueprintSystem/CodexPlugin~/plugins/blueprint-system-codex/skills/blueprint-ai-behavior-tree/
```

放给 Codex/agent 的行为树工作流说明。它们引用本设计文档和 `GUIDE.md`，但不替代运行时规则。

## 资产职责

### `.btree.json`

行为树源数据，必须适合版本控制和自动生成。

保存内容：

- tree metadata
- Blackboard schema
- root id
- tree nodes
- Decorator definitions
- Service definitions
- child 顺序
- node `inputs` 和 `properties`

不保存内容：

- Unity scene object direct reference
- Graph Toolkit 编辑器状态以外的运行时状态
- 普通 Blueprint exec 连线

### `.btgraph`

Graph Toolkit 可视化/缓存资产。

用途：

- 编辑节点位置和连线
- 可视化 Decorator / Service 附着关系
- 编辑 Blackboard 面板
- 显示调试状态

`.btgraph` 不是行为源数据。手写和自动生成行为树时优先修改 `.btree.json`。

### `.compiled.asset`

运行时加载资产，由 `.btree.json` 编译得到。

保存内容：

- 已验证节点表
- root node index
- children index array
- decorator index array
- service index array
- child behavior tree component references
- blackboard schema
- executor id
- node properties
- source id / display name for debug

运行时不读取 `.btree.json` 或 `.btgraph` 来 Tick。

## JSON Shape

顶层结构：

```json
{
  "schemaVersion": "0.1",
  "name": "EnemyMeleeBehavior",
  "category": "AI",
  "description": "Melee enemy decision tree.",
  "blackboard": [],
  "root": "root",
  "nodes": [],
  "decorators": [],
  "services": []
}
```

Blackboard key：

```json
{
  "name": "Target",
  "type": "GameObject",
  "defaultValue": null,
  "exposed": true,
  "persistent": false,
  "description": "Current perceived target."
}
```

Tree node：

```json
{
  "id": "move_to_target",
  "typeId": "BT.MoveTo",
  "position": [320, 160],
  "children": [],
  "decorators": [],
  "services": [],
  "inputs": {
    "target": "Target",
    "acceptableRadius": "MoveRadius"
  },
  "properties": {
    "speed": 3.5
  }
}
```

Decorator：

```json
{
  "id": "has_target",
  "typeId": "BT.BlackboardCondition",
  "inputs": {
    "value": "Target"
  },
  "properties": {
    "operator": "IsSet"
  }
}
```

Service：

```json
{
  "id": "update_distance",
  "typeId": "BT.UpdateDistance",
  "interval": 0.2,
  "randomDeviation": 0.05,
  "properties": {
    "targetKey": "Target",
    "distanceKey": "DistanceToTarget"
  }
}
```

## 节点模型

Behavior Tree 节点分为五类：

| Family | 设计职责 |
| --- | --- |
| Root | 树入口。每棵树必须有且只有一个 Root，Root 必须有且只有一个 child。 |
| Composite | 组织子节点执行顺序、优先级、并行或随机选择。 |
| Task | 执行具体行为或调用外部系统，可返回 `Success`、`Failure`、`Running`。 |
| Decorator | 分支条件和中断规则，挂在 tree node 上，不是普通 child。 |
| Service | 节点激活期间的周期性更新逻辑，挂在 tree node 上，不直接决定分支成功失败。 |

当前常用类型：

| Family | Type IDs |
| --- | --- |
| Composites | `BT.Root`, `BT.Selector`, `BT.Sequence`, `BT.Parallel`, `BT.RandomSelector`, `BT.PrioritySelector`, `BT.WeightedSelector` |
| Tasks | `BT.Wait`, `BT.SetBlackboard`, `BT.ClearBlackboard`, `BT.MoveTo`, `BT.RotateTo`, `BT.TriggerBlueprintEvent`, `BT.RunBlueprintTask`, `BT.Log` |
| Decorators | `BT.BlackboardCondition`, `BT.CompareFloat`, `BT.CompareBool`, `BT.ObjectIsSet`, `BT.DistanceLessThan`, `BT.Cooldown` |
| Services | `BT.UpdateDistance`, `BT.PerceptionSphere`, `BT.PerceptionRaycast`, `BT.SetBlackboardFromBlueprint`, `BT.TriggerBlueprintService` |

新增 Behavior Tree 节点时需要 Behavior Tree executor、registry entry 和 Graph Toolkit metadata 或专用 visual node。不要通过普通 Blueprint `.node.json` manifest 声明 `BT.*` 行为树节点。

## 树结构规范

基础结构规则：

- `BT.Root` 有且只有一个 child。
- Composite 至少有一个 child。
- Task 不能有 child。
- `children` 顺序就是执行顺序，也代表 Selector / Sequence / PrioritySelector 的语义顺序。
- Decorator 和 Service 通过 id 挂在 tree node 的 `decorators` / `services` 数组上。
- Decorator 和 Service 的完整定义放在顶层 `decorators` / `services` 数组，不放进 `nodes`。
- 节点 id 必须稳定、可读、schema-safe，推荐 `snake_case`，例如 `main_priority`, `attack_sequence`, `has_target`, `update_distance`, `move_to_target`。
- 一棵树表达一个 cohesive AI behavior，不要把多个互不相关的 AI 模式塞进同一棵树。

常见 AI 决策层级：

```text
root
  main_priority
    dead_sequence
    attack_sequence
    chase_sequence
    investigate_sequence
    patrol_sequence
```

分支选择规则：

- 高优先级行为会抢占低优先级 Running 行为时，用 `BT.PrioritySelector`。
- 普通 fallback 选择用 `BT.Selector`。
- 条件和动作必须按顺序全部成立时，用 `BT.Sequence`。
- 多个子行为需要同时 Tick 且任一失败会中止 siblings 时，用 `BT.Parallel`。
- 随机选择 fallback 行为时，用 `BT.RandomSelector`。
- 按权重随机选择 fallback 行为时，用 `BT.WeightedSelector`，权重放在 `properties.weights`，顺序与 children 对齐。

Decorator 使用规则：

- 分支前置条件用 Decorator，不要建成普通 Task child。
- 多个 Decorator 表示该节点进入前都必须满足。
- 运行时每 tick 重新评估已挂载 Decorator；条件变 false 时，当前节点会按 Decorator 失败处理。
- 复杂条件优先拆成可读的 Blackboard key，由 Service 或 Blueprint 写入。

Service 使用规则：

- 周期性感知、距离更新、从 Blueprint 同步状态等逻辑用 Service。
- Service 只在挂载节点处于 active path 时运行。
- Service 写 Blackboard 或触发轻量副作用，不直接返回分支成功失败。
- `interval` 不要过小；大量 NPC 的感知和查询应使用合理 tick 间隔。

Task 使用规则：

- Task 只表达一个明确动作，例如等待、移动、旋转、写 Blackboard、触发 Blueprint 事件。
- 持续任务首次进入时启动动作，之后每次 Tick 检查完成状态。
- 未完成时返回 `Running`；完成返回 `Success` 或 `Failure`。
- 退出 running 分支时必须能 Abort/Cleanup。
- 复杂动作细节交给普通 Blueprint 或 Unity 组件，Behavior Tree 只负责何时调用。

## Blackboard 规范

Blackboard 是树、Runner、Service、Task、Decorator、普通 Blueprint 和 Unity 组件之间的状态合同。

支持类型：

```text
bool
int
float
string
Vector2
Vector3
GameObject
Transform
Blueprint
BlueprintRef
```

默认值规则：

- `bool`、`int`、`float`、`string`、`Vector2`、`Vector3` 可以在 JSON 中保存默认值。
- `Blueprint` 默认值保存 `.blueprint.json` 资产路径。
- `GameObject`、`Transform`、`BlueprintRef` 是运行时对象值，JSON 默认值使用 `null`。
- 需要 Inspector 或集成方覆盖的 key 标记 `exposed: true`。
- 每个 key 都写清楚 `description`，说明谁写入、谁读取、何时更新。

读写规则：

- Decorator 默认只读 Blackboard。
- Task 和 Service 可以读写 Blackboard。
- 普通 Blueprint 可以通过 Runner 暴露接口读写 Blackboard。
- Runner override 覆盖 tree default；运行时写入形成 runtime value。
- 外部场景对象通过 Runner override、组件解析或感知 Service 注入，不写入 JSON 直接引用。

输入解析规则：

```text
value input:
node.inputs[inputId] -> Blackboard value
node.properties[propertyName] -> direct value
executor default

key input:
node.inputs[inputId] -> Blackboard key name
node.properties[propertyName] -> Blackboard key name
null/default
```

注意：`BT.SetBlackboard.inputs.key = "Target"` 表示目标 key 名叫 `Target`，不是读取 `Target` 当前值。

## Blueprint 和 Unity 组件交互

Behavior Tree 只做决策，不承载所有动作实现。

推荐桥接方式：

```text
BT.TriggerBlueprintEvent
  -> 触发普通 Blueprint 事件，例如 Attack、Die、Alert

BT.RunBlueprintTask
  -> 执行普通 Blueprint 异步任务，并等待完成/失败 key 或超时

BT.SetBlackboardFromBlueprint
  -> 从普通 Blueprint 暴露变量读取状态并写入 Blackboard

BT.TriggerBlueprintService
  -> active path 期间周期性触发普通 Blueprint 服务逻辑
```

Unity 组件交互规则：

- `BehaviorTreeRunner` 挂在 AI owner GameObject 上。
- Runner 持有 compiled behavior tree，并创建每个实例自己的 Blackboard。
- 通用能力用内置 BT Task 封装，例如 `BT.MoveTo` 读取 `NavMeshAgent`。
- 项目专属能力交给普通 Blueprint 或业务组件，例如战斗、动画事件、技能释放。
- JSON 中用 Blackboard key、binding name、component role 或 Blueprint asset path 描述依赖。

常见协作者：

```text
NavMeshAgent
Animator
Rigidbody
Collider
BlueprintRunner
Sensor component
Health component
Combat component
```

## 编辑器和可视化规范

Graph Toolkit 中的 Behavior Tree 图应保持树形阅读顺序：

- Root 在最上方。
- 子节点从左到右表示优先级和执行顺序。
- Composite 使用能承载多个 child 的视觉节点。
- Task 使用普通行为节点。
- Decorator 显示为条件标签或专用 condition visual node。
- Service 显示为周期更新标签。
- Running、Success、Failure 和 active path 可用于运行时调试高亮。

可视化图导出时仍写回 `.btree.json` schema：

- tree nodes 写入顶层 `nodes`。
- Decorator visual node 连接写回目标 node 的 `decorators` id list。
- Service 附着关系写回目标 node 的 `services` id list。
- Blackboard visual ports 写回 `inputs` 或 inline value。

## 编译和验证规范

编译前必须验证：

- 必须有且只有一个 Root。
- Root 必须有且只有一个 child。
- Composite 至少有一个 child。
- Task 不能有 child。
- 所有 child id 必须存在。
- 不能形成循环。
- Blackboard key 引用必须存在。
- Decorator / Service id 必须存在。
- 节点 `typeId` 必须有对应 Behavior Tree executor。
- 必填属性必须完整。
- 同级 child 顺序必须稳定。

建议警告：

- Selector 没有 fallback 分支。
- Sequence 中存在永远 Running 的 Task，后面的 child 可能不可达。
- Decorator 引用了没有写入来源的 Blackboard key。
- Service interval 过小。
- MoveTo 没有目标 key。
- Blueprint bridge target 为空或资产不存在。

## 运行时规范

每个 Tick 的基本顺序：

```text
1. 更新外部注入的 Blackboard 数据。
2. 从 Root 开始 Tick。
3. Composite 根据自身规则选择 child。
4. 进入节点前检查 Decorator。
5. active path 上的 Service 按 interval 执行。
6. Task 执行动作或检查动作进度。
7. 记录每个节点返回状态。
8. 对离开 active path 的 running task 调用 Abort/Cleanup。
9. 输出调试快照。
```

运行时需要记录：

```text
active path
node latest status
running child index
task local state
service next tick time
decorator latest result
abort reason
debug log
```

Runner tick mode：

```text
Update
FixedUpdate
Manual
Interval
```

大量 NPC 不应无节制每帧全量 Tick；优先使用 `maxTickRate`、`Interval` 或业务侧分批调度。

## 示例树

```text
Root
└── PrioritySelector
    ├── Sequence: Dead
    │   ├── Decorator: IsDead == true
    │   └── Task: TriggerBlueprintEvent("Die")
    ├── Sequence: Attack
    │   ├── Decorator: Target IsSet
    │   ├── Decorator: DistanceToTarget <= AttackRange
    │   └── Task: TriggerBlueprintEvent("Attack")
    ├── Sequence: Chase
    │   ├── Decorator: Target IsSet
    │   └── Task: MoveTo(Target)
    ├── Sequence: Investigate
    │   ├── Decorator: LastKnownPosition IsSet
    │   ├── Task: MoveTo(LastKnownPosition)
    │   └── Task: ClearBlackboard(LastKnownPosition)
    └── Sequence: Patrol
        ├── Task: PickPatrolPoint or SetBlackboard(PatrolPoint)
        ├── Task: MoveTo(PatrolPoint)
        └── Task: Wait(1.5)
```

对应 Blackboard 合同：

```text
IsDead -> bool -> runtime or health component writes -> Dead branch reads
Target -> GameObject -> perception/service writes -> Attack/Chase branches read
DistanceToTarget -> float -> UpdateDistance service writes -> Attack branch reads
AttackRange -> float -> tree default or runner override -> Attack branch reads
LastKnownPosition -> Vector3 -> perception/service writes -> Investigate branch reads
PatrolPoint -> Vector3 -> patrol task/service writes -> Patrol branch reads
```
