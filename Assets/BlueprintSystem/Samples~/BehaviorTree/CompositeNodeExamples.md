# Behavior Tree Composite Node Examples

本 sample 展示了行为树复合节点的四个常见用法。可以直接把 `Prefabs` 目录下的单个样例 prefab 拖进场景，也可以使用总览 prefab 一次查看全部样例。

## Prefabs

- `Prefabs/BehaviorTreeCompositeExamplesRoot.prefab`：包含全部四个复合节点样例。
- `Prefabs/SelectorFallbackExample.prefab`：Selector fallback 样例。
- `Prefabs/ParallelWaitAllExample.prefab`：Parallel wait-all 样例。
- `Prefabs/RandomSelectorChoiceExample.prefab`：RandomSelector 随机分支样例。
- `Prefabs/PrioritySelectorPreemptExample.prefab`：PrioritySelector 抢占样例。

## Shared Assets

- 行为树源文件：`AI/Behavior/*.btree.json`
- 行为树 compiled asset：`AI/Behavior/*.compiled.asset`
- 可视化 Blueprint：`Blueprint/Behavior/*Visualizer.blueprint.json`
- 可视化 compiled asset：`Blueprint/Behavior/*Visualizer.compiled.asset`

可视化 Blueprint 只负责把行为树事件显示到场景物体上，例如改材质颜色、缩放 marker、移动 gate、更新状态文字。核心行为仍然来自对应的 `.btree.json`。

## SelectorFallbackExample

Prefab：`Prefabs/SelectorFallbackExample.prefab`

行为树：`AI/Behavior/SelectorFallbackExample.btree.json`

主要节点：

- `BT.Root`：行为树入口。
- `BT.Selector`：先尝试 `Engage` 分支，失败后继续尝试 fallback 分支。
- `BT.Sequence`：分别组织 `Engage` 和 `PatrolFallback` 两条分支。
- `BT.CompareBool`：装饰在 `Engage` 分支上，检查 `EnemyVisible == true`。
- `BT.SetBlackboard`：把 `SelectedBranch` 写成 `Engage` 或 `PatrolFallback`。
- `BT.TriggerBlueprintEvent`：触发 `EngageSelected` 或 `PatrolFallbackSelected`，让 visualizer 高亮对应物体。
- `BT.Log`：输出当前选择的分支。

样例效果：同一个 prefab 内放了两个 guard 展示对象。一个 `EnemyVisible` 为 false，会走巡逻 fallback；另一个 `EnemyVisible` 为 true，会走 engage 分支。它展示了 `Selector` 如何按顺序寻找第一个可成功的子分支。

可视化 Blueprint：`Blueprint/Behavior/SelectorBranchVisualizer.blueprint.json`

可视化节点使用 `Game.Event.Custom` 接收行为树事件，用 `Variable.Set/Get` 记录当前分支，再通过 `Variable.Compare`、`Flow.Branch`、`Game.SetRendererMaterialColor`、`Game.SetTransformLocalScale` 和 `UI.SetText` 更新显示。

## ParallelWaitAllExample

Prefab：`Prefabs/ParallelWaitAllExample.prefab`

行为树：`AI/Behavior/ParallelWaitAllExample.btree.json`

主要节点：

- `BT.Root`：行为树入口。
- `BT.Parallel`：同时 tick 左右两个子分支，并等待所有分支完成。
- `BT.Sequence`：左右分支各自按顺序执行等待、写黑板、触发事件、日志。
- `BT.Wait`：左分支等待 `0.35` 秒，右分支等待 `0.9` 秒。
- `BT.SetBlackboard`：分别写入 `LeftDone = true` 和 `RightDone = true`。
- `BT.TriggerBlueprintEvent`：触发 `LeftDone` 和 `RightDone`。
- `BT.Log`：输出左右分支完成日志。

样例效果：左右两列会在不同时间完成，gate 只有在 `LeftDone` 和 `RightDone` 都为 true 后才打开。它展示了 `Parallel` 的 wait-all 行为。

可视化 Blueprint：`Blueprint/Behavior/ParallelWaitAllVisualizer.blueprint.json`

可视化节点使用 `Game.Event.Custom` 接收完成事件，用 `Variable.Get`、`Logic.And` 判断是否全部完成，并通过 `Game.GetDeltaTime`、`Math.Multiply`、`Game.GetTransformLocalPosition`、`Vector.Lerp`、`Game.SetTransformLocalPosition`、`Game.SetRendererMaterialColor` 和 `UI.SetText` 平滑更新柱子与 gate。

## RandomSelectorChoiceExample

Prefab：`Prefabs/RandomSelectorChoiceExample.prefab`

行为树：`AI/Behavior/RandomSelectorChoiceExample.btree.json`

主要节点：

- `BT.Root`：行为树入口。
- `BT.RandomSelector`：随机打乱 `Rare`、`Scout`、`Flank` 三条子分支，并选择第一个成功分支。
- `BT.Sequence`：每条候选分支各自写黑板、触发事件、输出日志。
- `BT.CompareBool`：装饰在 `Rare` 分支上，检查 `AllowRareBranch == true`。
- `BT.SetBlackboard`：把 `RandomChoice` 写成 `Rare`、`Scout` 或 `Flank`。
- `BT.TriggerBlueprintEvent`：触发 `RareSelected`、`ScoutSelected` 或 `FlankSelected`。
- `BT.Log`：输出本次随机选择结果。

样例效果：默认 `AllowRareBranch` 为 false，Rare 分支会被条件挡住；RandomSelector 会在可用分支里随机选择 Scout 或 Flank。打开 `AllowRareBranch` 后，Rare 也可能被选中。

可视化 Blueprint：`Blueprint/Behavior/RandomSelectorBranchVisualizer.blueprint.json`

可视化节点使用 `Game.Event.Custom` 接收三种选择事件，用 `Variable.Set/Get` 保存 `random_choice`，再通过 `Variable.Compare`、`Flow.Branch`、`Game.SetRendererMaterialColor`、`Game.SetTransformLocalScale` 和 `UI.SetText` 高亮被选中的路线。

## PrioritySelectorPreemptExample

Prefab：`Prefabs/PrioritySelectorPreemptExample.prefab`

行为树：`AI/Behavior/PrioritySelectorPreemptExample.btree.json`

主要节点：

- `BT.Root`：行为树入口。
- `BT.PrioritySelector`：每次 tick 都重新检查高优先级分支，让 alert 分支可以抢占 idle。
- `BT.SetBlackboardFromBlueprint`：service 节点，把 visualizer 里的 `alert_requested` 同步到行为树黑板 `AlertMode`。
- `BT.Sequence`：组织 `Alert` 和 `Idle` 两条分支。
- `BT.CompareBool`：装饰在 `Alert` 分支上，检查 `AlertMode == true`。
- `BT.Wait`：Idle 分支等待 `8` 秒，给抢占演示留出时间。
- `BT.SetBlackboard`：把 `SelectedPriority` 写成 `Alert` 或 `Idle`。
- `BT.TriggerBlueprintEvent`：触发 `AlertSelected` 或 `IdleSelected`。
- `BT.Log`：输出当前优先级分支。

样例效果：开局先进入 idle 等待。可视化 Blueprint 在 2 秒后设置 `alert_requested = true`，service 同步到 `AlertMode`，`PrioritySelector` 重新评估后切到更高优先级的 alert 分支。

可视化 Blueprint：`Blueprint/Behavior/PrioritySelectorVisualizer.blueprint.json`

可视化节点使用 `Game.Event.OnStart`、`Flow.Delay` 和 `Variable.Set` 延迟发起 alert 请求；再用 `Game.Event.Custom` 接收行为树分支事件，并通过 `Variable.Compare`、`Flow.Branch`、`Game.SetRendererMaterialColor`、`Game.SetTransformLocalScale` 和 `UI.SetText` 更新 idle/alert 状态。
