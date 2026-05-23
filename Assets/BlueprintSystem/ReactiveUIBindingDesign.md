# 蓝图响应式 UI 绑定可用版设计

## 背景

当前 UI 文本、图片、显隐等节点多为一次性执行节点，例如 `UI.SetText`。

当 UI 已打开后，运行时修改蓝图内容并触发热重载，runner 会重建 runtime graph，但不会重新执行原本只在 `OnOpen` 中跑过的 UI 写值链路，因此画面不会刷新。

## 目标

- UI 打开时注册绑定，并立即应用一次。
- 热重载后自动恢复或重建绑定，并立即刷新当前 UI。
- 变量或外部状态变化时，先采用全量刷新策略，不做依赖分析。
- 不替换现有 `UI.SetText` 等一次性节点，新增绑定节点，兼容旧蓝图。
- 避免重复注册、旧 context 残留、事件监听泄漏。

## 非目标

- 不做精细依赖追踪。
- 不做变量级增量刷新。
- 不做完整 MVVM 框架。
- 不自动把所有现有 `UI.SetText` 转成绑定。
- 不改变现有 `OnOpen` / `OnClose` 行为语义。

## 新增节点

### `UI.BindText`

用途：绑定 `TMP_Text.text` 到一个蓝图表达式或输入值。

输入：

```text
execIn: exec
target: UIBinding<TMP_Text>
variableName: string
variableTarget: Blueprint
value: string
```

输出：

```text
bound: exec
```

行为：

- 执行时注册或更新一个文本绑定。
- `variableName` 非空时读取当前蓝图变量；`variableTarget` 非空时按跨蓝图目标读取暴露变量。
- `variableName` 为空时保持兼容，求值 `value` 并写入目标。
- 注册后立即求值并写入 `TMP_Text.text`。
- 后续刷新时重新读取变量或重新求值 `value` 并写入目标。

### 后续可扩展节点

一期先实现 `UI.BindText`。

接口设计预留以下节点：

```text
UI.BindVisible
UI.BindImageSprite
UI.BindGraphicColor
UI.BindInteractable
UI.BindLoopScrollView
```

## 运行时结构

新增运行时管理器：

```csharp
BlueprintReactiveBindingRuntime
```

职责：

- 按 runner/context 保存 active bindings。
- 执行绑定注册。
- 热重载时清理旧绑定。
- 支持全量刷新当前 runner 的绑定。
- UI 关闭或 runner 销毁时释放绑定。

核心接口建议：

```csharp
public interface IBlueprintReactiveBinding
{
    string Key { get; }
    BlueprintExecutionContext Context { get; }
    void Apply();
    bool IsAlive();
}

public static class BlueprintReactiveBindingRuntime
{
    public static void Register(BlueprintExecutionContext context, IBlueprintReactiveBinding binding);
    public static void Clear(BlueprintExecutionContext context);
    public static void Refresh(BlueprintExecutionContext context);
    public static void RefreshInstance(IBlueprintInstance instance);
}
```

绑定 key 规则：

```text
{instance-id}:{node-id}:{target-binding-name}:{property}
```

同一个 key 重复注册时覆盖旧绑定，避免 `OnOpen` 多次执行造成重复绑定。

## 文本绑定实现

`UI.BindText` executor 注册：

```csharp
TextReactiveBinding(
    context,
    node,
    targetBindingName,
    valuePortId: "value",
    variableNamePortId: "variableName",
    variableTargetPortId: "variableTarget"
)
```

`Apply()` 时：

1. 检查 context、node、binding resolver 是否仍有效。
2. 通过 `context.BindingResolver.Resolve<TMP_Text>(target)` 找目标。
3. 调用 `context.ClearValueCache()`。
4. 如果 `variableName` 非空，读取当前蓝图变量；如果 `variableTarget` 非空，解析目标蓝图并读取暴露变量。
5. 如果 `variableName` 为空，通过 `context.GetInputValue(node, "value", string.Empty)` 重新求值。
6. 写入 `text.text`。

## 热重载行为

修改 `BlueprintRunner.ReloadBlueprint()` 流程：

```text
Capture variables
Capture active reactive binding nodes
Create new runtime state
Invalidate old state
Clear old reactive bindings
Apply new runtime state
Restore active reactive binding nodes
Optionally trigger legacy OnReload
Refresh restored reactive bindings
```

一期推荐新增配置：

```csharp
BlueprintReloadOptions.TriggerReloadEvent = true
BlueprintReloadOptions.RefreshReactiveBindings = true
```

自动热重载路径默认启用：

```text
PreserveVariables = true
TriggerReloadEvent = true
RefreshReactiveBindings = true
```

这样蓝图可以用：

```text
OnOpen -> UI.BindText
```

热重载后 C# 会按 reload 前已经激活的 binding 节点重新注册绑定并刷新画面，不要求蓝图显式增加 `OnReload` 节点。

## UI 生命周期

`UIBlueprintBinder.OnEnable`：

```text
触发 OnOpen
```

`UIBlueprintBinder.OnDisable`：

```text
触发 OnClose
清理当前 runner/context 的 reactive bindings
```

这样关闭 UI 后不会继续持有 TMP_Text 或旧 context。

## 蓝图使用约定

旧写法：

```text
OnOpen -> UI.SetText
```

新写法：

```text
OnOpen -> UI.BindText
```

对于复杂 UI，建议抽一个刷新事件：

```text
OnOpen -> RegisterBindings
```

其中 `RegisterBindings` 负责执行所有 `UI.Bind*` 节点。

## 一期变量变化刷新策略

一期不做字段级依赖追踪；只记录绑定依赖的蓝图实例，用于跨蓝图全量刷新。

当 `Variable.Set` 或 `Blueprint.SetVariable` 成功写入变量后，调用：

```csharp
BlueprintReactiveBindingRuntime.RefreshInstance(changedInstance);
```

这会全量刷新当前 instance 或目标 instance 相关绑定。

跨蓝图绑定通过 `variableTarget` 记录依赖的目标蓝图实例。`Blueprint.SetVariable` 写入目标实例后，刷新依赖该实例的 UI 绑定，即使绑定注册在另一个 UI runner 上。

优点：

- 实现简单。
- 行为稳定。
- 足够覆盖 UI 文本、状态、数值展示。

代价：

- 大 UI 上可能有额外刷新成本。
- 后续可通过依赖追踪优化。

## 文件改动范围

预计新增：

```text
Assets/BlueprintSystem/Runtime/BlueprintReactiveBindingRuntime.cs
Assets/BlueprintSystem/Specs/Nodes/UI.BindText.node.json
```

预计修改：

```text
Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
Assets/BlueprintSystem/Runtime/BlueprintExecutor.cs
Assets/BlueprintSystem/Runtime/BlueprintRunner.cs
Assets/BlueprintSystem/Runtime/BlueprintRuntimeComponent.cs
Assets/BlueprintSystem/Runtime/UIBlueprintBinder.cs
Assets/BlueprintSystem/Executors/Variables/VariableExecutors.cs
Assets/BlueprintSystem/Executors/Blueprint/BlueprintAccessExecutors.cs
Assets/BlueprintSystem/GUIDE.md
```

如需 Graph Toolkit 可视化节点，再补：

```text
Assets/BlueprintSystem/Editor/GraphToolkit/BuiltinVisualNodes.cs
```

## 测试验收

必须覆盖：

- `UI.BindText` 执行后立即写入 TMP_Text。
- 同一节点重复执行不会产生重复绑定。
- 变量变化后绑定文本刷新。
- 热重载后 C# 自动恢复已激活绑定，文本显示新蓝图值。
- UI disable 后绑定被清理。
- 旧 `UI.SetText` 行为不受影响。

## 风险与处理

- 新增的绑定节点如果 reload 前从未激活，不会被自动恢复；关闭再打开 UI 或重新执行注册入口后生效。
- 重复注册：用稳定 binding key 覆盖旧绑定。
- 旧 context 泄漏：reload、disable、destroy 时清理。
- 全量刷新性能：一期接受，后续做依赖追踪。
- 按钮/Toggle 监听重复问题：本方案只绑定显示属性，不改变事件监听节点。
