# BlueprintSystem Node Guide

This guide is for developers and coding agents extending the current BlueprintSystem.
Use it before adding a new node so existing functionality is not duplicated.

Current source of truth:

```text
Blueprint JSON behavior: Assets/BlueprintSystem/Sources/**/*.blueprint.json
Node manifests: BlueprintSystem package **/Specs/Nodes/*.node.json, plus project Assets/**/*.node.json
Compiled runtime assets: *.compiled.asset generated from blueprint JSON + node manifests
Runtime executors: Assets/BlueprintSystem/Executors/**/*.cs
Executor registry: Assets/BlueprintSystem/Runtime/BlueprintExecutor.cs
Graph Toolkit visual nodes: Assets/BlueprintSystem/Editor/GraphToolkit/*.cs
```

Important rule:

```text
Adding an executor alone does not make a user-facing node.
A user-facing node needs a .node.json manifest and, for Graph Toolkit UX, a visual node class.
```

## Runtime Model

Each blueprint node is identified by `typeId`. In the editor, the compiler finds a matching node manifest, bakes manifest defaults into a `BlueprintCompiledAsset`, then stores each node's executor id for runtime hydration. At runtime, `BlueprintRunner.compiledBlueprint` is the root blueprint asset reference and may include compiled component blueprint references; executor ids resolve through `BlueprintExecutorRegistry.CreateDefault()` without loading source JSON or node manifests.

Value input resolution order:

```text
connected value edge -> compiled node property -> null
```

`Binding<T>` values are stored in JSON as string binding names. At runtime, `BlueprintRunner` resolves the binding name to a Unity object and can also call `GetComponent<T>()` when the binding target is a `GameObject` or `Component`. `UIBlueprintBinder` inherits this resolver and only adds UI open/close lifecycle behavior.

## Current Node Summary

| Type ID | Title | Category | Executor ID | Executor Class | Purpose |
| --- | --- | --- | --- | --- | --- |
| `Game.Event.OnStart` | On Start | Events | `Flow.Event` | `FlowEventExecutor` | Entry fired by `BlueprintRunner.Start`. |
| `Game.Event.Custom` | Custom Event | Events | `Flow.Event` | `FlowEventExecutor` | Entry fired by a named event. |
| `UI.Event.OnOpen` | On Open | Events | `Flow.Event` | `FlowEventExecutor` | Entry fired by `UIBlueprintBinder.OnEnable` by default. |
| `UI.Event.OnClose` | On Close | Events | `Flow.Event` | `FlowEventExecutor` | Entry fired by `UIBlueprintBinder.OnDisable` by default. |
| `Flow.Branch` | Branch | Flow | `Flow.Branch` | `FlowBranchExecutor` | Routes execution by a bool condition. |
| `Flow.Sequence` | Sequence | Flow | `Flow.Sequence` | `FlowSequenceExecutor` | Runs up to four exec outputs in order. |
| `Flow.Delay` | Delay | Flow | `Flow.Delay` | `FlowDelayExecutor` | Suspends execution before continuing. |
| `Game.Log` | Log | Game | `Game.Log` | `GameLogExecutor` | Logs a string message. |
| `Game.SendEvent` | Send Event | Game | `Game.SendEvent` | `GameSendEventExecutor` | Publishes a named event to the blueprint event bus. |
| `Game.LoadScene` | Load Scene | Game | `Game.LoadScene` | `GameLoadSceneExecutor` | Loads a Unity scene by name. |
| `Game.LoadSceneAsync` | Load Scene Async | Game | `Game.LoadSceneAsync` | `GameLoadSceneAsyncExecutor` | Loads a Unity scene by name and continues from `complete` after the async operation finishes. |
| `Game.InstantiateObject` | Instantiate Object | Game/Object | `Game.InstantiateObject` | `GameInstantiateObjectExecutor` | Clones a `GameObject` prefab from a binding or connected runtime asset, optionally under a parent Transform. |
| `GameObject.SetActive` | Set GameObject Active | GameObject | `GameObject.SetActive` | `GameObjectSetActiveExecutor` | Sets active state on a connected runtime `GameObject` value. |
| `GameObject.Destroy` | Destroy GameObject | GameObject | `GameObject.Destroy` | `GameObjectDestroyExecutor` | Destroys a connected runtime `GameObject` value. |
| `GameObject.PrewarmPool` | Prewarm GameObject Pool | GameObject/Pool | `GameObject.PrewarmPool` | `GameObjectPrewarmPoolExecutor` | Creates inactive instances for a runner-scoped `GameObject` pool. |
| `GameObject.AcquireFromPool` | Acquire From GameObject Pool | GameObject/Pool | `GameObject.AcquireFromPool` | `GameObjectAcquireFromPoolExecutor` | Gets or optionally expands a runner-scoped `GameObject` pool and outputs the checked-out instance. |
| `GameObject.ReleaseToPool` | Release To GameObject Pool | GameObject/Pool | `GameObject.ReleaseToPool` | `GameObjectReleaseToPoolExecutor` | Returns a managed instance to a runner-scoped `GameObject` pool. |
| `GameObject.ClearPool` | Clear GameObject Pool | GameObject/Pool | `GameObject.ClearPool` | `GameObjectClearPoolExecutor` | Destroys all managed instances in a runner-scoped `GameObject` pool. |
| `GameObject.GetPoolStats` | Get GameObject Pool Stats | GameObject/Pool | `GameObject.GetPoolStats` | `GameObjectGetPoolStatsExecutor` | Reads active, available, and managed counts from a runner-scoped `GameObject` pool. |
| `GameObject.GetPoolActiveInstances` | Get Pool Active Instances | GameObject/Pool | `GameObject.GetPoolActiveInstances` | `GameObjectGetPoolActiveInstancesExecutor` | Returns a runtime snapshot of currently checked-out pool instances. |
| `Blueprint.IsValid` | Is Blueprint Valid | Blueprint | `Blueprint.IsValid` | `BlueprintIsValidExecutor` | Returns true when a `Blueprint` asset path or runtime `BlueprintRef` resolves inside the current Blueprint instance tree. |
| `Blueprint.GetOwner` | Get Blueprint Owner | Blueprint | `Blueprint.GetOwner` | `BlueprintGetOwnerExecutor` | Returns the owner `BlueprintRef` for the current component or supplied `BlueprintRef`. |
| `Blueprint.GetComponent` | Get Blueprint Component | Blueprint | `Blueprint.GetComponent` | `BlueprintGetComponentExecutor` | Finds a named component from the current instance or supplied `BlueprintRef`, walking owner instances outward. |
| `Blueprint.TriggerEvent` | Trigger Blueprint Event | Blueprint | `Blueprint.TriggerEvent` | `BlueprintTriggerEventExecutor` | Calls `TriggerEvent` on a Blueprint instance resolved by asset path or `BlueprintRef`. |
| `Blueprint.GetVariable` | Get Blueprint Variable | Blueprint | `Blueprint.GetVariable` | `BlueprintGetVariableExecutor` | Reads an exposed variable from a Blueprint instance resolved by asset path or `BlueprintRef`. |
| `Blueprint.SetVariable` | Set Blueprint Variable | Blueprint | `Blueprint.SetVariable` | `BlueprintSetVariableExecutor` | Writes an exposed variable on a Blueprint instance resolved by asset path or `BlueprintRef`. |
| `Blueprint.GetVariableFromGameObject` | Get Variable From GameObject | Blueprint | `Blueprint.GetVariableFromGameObject` | `BlueprintGetVariableFromGameObjectExecutor` | Reads an exposed variable from the `BlueprintRunner` on a bound or connected `GameObject`. |
| `Blueprint.SetVariableFromGameObject` | Set Variable From GameObject | Blueprint | `Blueprint.SetVariableFromGameObject` | `BlueprintSetVariableFromGameObjectExecutor` | Writes an exposed variable on the `BlueprintRunner` on a bound or connected `GameObject`. |
| `BehaviorTree.GetBlackboardBool` | Get Blackboard Bool | BehaviorTree/Blackboard | `BehaviorTree.GetBlackboardBool` | `BehaviorTreeGetBlackboardBoolExecutor` | Reads a bool value from a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.GetBlackboardInt` | Get Blackboard Int | BehaviorTree/Blackboard | `BehaviorTree.GetBlackboardInt` | `BehaviorTreeGetBlackboardIntExecutor` | Reads an int value from a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.GetBlackboardFloat` | Get Blackboard Float | BehaviorTree/Blackboard | `BehaviorTree.GetBlackboardFloat` | `BehaviorTreeGetBlackboardFloatExecutor` | Reads a float value from a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.GetBlackboardString` | Get Blackboard String | BehaviorTree/Blackboard | `BehaviorTree.GetBlackboardString` | `BehaviorTreeGetBlackboardStringExecutor` | Reads a string value from a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.GetBlackboardVector3` | Get Blackboard Vector3 | BehaviorTree/Blackboard | `BehaviorTree.GetBlackboardVector3` | `BehaviorTreeGetBlackboardVector3Executor` | Reads a `Vector3` value from a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.GetBlackboardGameObject` | Get Blackboard GameObject | BehaviorTree/Blackboard | `BehaviorTree.GetBlackboardGameObject` | `BehaviorTreeGetBlackboardGameObjectExecutor` | Reads a runtime `GameObject` value from a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.SetBlackboardBool` | Set Blackboard Bool | BehaviorTree/Blackboard | `BehaviorTree.SetBlackboardBool` | `BehaviorTreeSetBlackboardBoolExecutor` | Writes a bool value to a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.SetBlackboardInt` | Set Blackboard Int | BehaviorTree/Blackboard | `BehaviorTree.SetBlackboardInt` | `BehaviorTreeSetBlackboardIntExecutor` | Writes an int value to a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.SetBlackboardFloat` | Set Blackboard Float | BehaviorTree/Blackboard | `BehaviorTree.SetBlackboardFloat` | `BehaviorTreeSetBlackboardFloatExecutor` | Writes a float value to a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.SetBlackboardString` | Set Blackboard String | BehaviorTree/Blackboard | `BehaviorTree.SetBlackboardString` | `BehaviorTreeSetBlackboardStringExecutor` | Writes a string value to a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.SetBlackboardVector3` | Set Blackboard Vector3 | BehaviorTree/Blackboard | `BehaviorTree.SetBlackboardVector3` | `BehaviorTreeSetBlackboardVector3Executor` | Writes a `Vector3` value to a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.SetBlackboardGameObject` | Set Blackboard GameObject | BehaviorTree/Blackboard | `BehaviorTree.SetBlackboardGameObject` | `BehaviorTreeSetBlackboardGameObjectExecutor` | Writes a runtime `GameObject` value to a bound `BehaviorTreeRunner` Blackboard. |
| `BehaviorTree.ClearBlackboard` | Clear Blackboard | BehaviorTree/Blackboard | `BehaviorTree.ClearBlackboard` | `BehaviorTreeClearRunnerBlackboardExecutor` | Clears one key on a bound `BehaviorTreeRunner` Blackboard. |
| `Game.IsColliding` | Is Colliding | Game/Physics | `Game.IsColliding` | `GameIsCollidingExecutor` | Returns true when two bound GameObjects have overlapping colliders. |
| `Game.SetTransformPosition` | Set Transform Position | Game/Transform | `Game.SetTransformPosition` | `GameSetTransformPositionExecutor` | Sets world `Transform.position`. |
| `Game.SetTransformEulerAngles` | Set Transform Euler Angles | Game/Transform | `Game.SetTransformEulerAngles` | `GameSetTransformEulerAnglesExecutor` | Sets world `Transform.eulerAngles`. |
| `Game.SetTransformLocalScale` | Set Transform Local Scale | Game/Transform | `Game.SetTransformLocalScale` | `GameSetTransformLocalScaleExecutor` | Sets `Transform.localScale`. |
| `Game.SetRigidbodyLinearVelocity` | Set Rigidbody Linear Velocity | Game/Physics | `Game.SetRigidbodyLinearVelocity` | `GameSetRigidbodyLinearVelocityExecutor` | Sets 3D `Rigidbody.linearVelocity`. |
| `Game.AddRigidbodyForce` | Add Rigidbody Force | Game/Physics | `Game.AddRigidbodyForce` | `GameAddRigidbodyForceExecutor` | Adds force to a 3D `Rigidbody`. |
| `Game.SetColliderEnabled` | Set Collider Enabled | Game/Physics | `Game.SetColliderEnabled` | `GameSetColliderEnabledExecutor` | Sets 3D `Collider.enabled`. |
| `Game.SetColliderIsTrigger` | Set Collider Is Trigger | Game/Physics | `Game.SetColliderIsTrigger` | `GameSetColliderIsTriggerExecutor` | Sets 3D `Collider.isTrigger`. |
| `Game.SetRigidbody2DLinearVelocity` | Set Rigidbody2D Linear Velocity | Game/Physics2D | `Game.SetRigidbody2DLinearVelocity` | `GameSetRigidbody2DLinearVelocityExecutor` | Sets `Rigidbody2D.linearVelocity`. |
| `Game.AddRigidbody2DForce` | Add Rigidbody2D Force | Game/Physics2D | `Game.AddRigidbody2DForce` | `GameAddRigidbody2DForceExecutor` | Adds force to a `Rigidbody2D`. |
| `Game.SetCollider2DEnabled` | Set Collider2D Enabled | Game/Physics2D | `Game.SetCollider2DEnabled` | `GameSetCollider2DEnabledExecutor` | Sets `Collider2D.enabled`. |
| `Game.SetCollider2DIsTrigger` | Set Collider2D Is Trigger | Game/Physics2D | `Game.SetCollider2DIsTrigger` | `GameSetCollider2DIsTriggerExecutor` | Sets `Collider2D.isTrigger`. |
| `Game.SetRendererMaterial` | Set Renderer Material | Game/Rendering | `Game.SetRendererMaterial` | `GameSetRendererMaterialExecutor` | Sets an instance material slot on a `Renderer`. |
| `Game.SetRendererMaterialColor` | Set Renderer Material Color | Game/Rendering | `Game.SetRendererMaterialColor` | `GameSetRendererMaterialColorExecutor` | Sets a color property on `renderer.material`. |
| `Game.SetRendererTexture` | Set Renderer Texture | Game/Rendering | `Game.SetRendererTexture` | `GameSetRendererTextureExecutor` | Sets a texture property on `renderer.material`. |
| `Game.SetLightEnabled` | Set Light Enabled | Game/Lighting | `Game.SetLightEnabled` | `GameSetLightEnabledExecutor` | Sets `Light.enabled`. |
| `Game.SetLightIntensity` | Set Light Intensity | Game/Lighting | `Game.SetLightIntensity` | `GameSetLightIntensityExecutor` | Sets `Light.intensity`. |
| `Game.SetLightColor` | Set Light Color | Game/Lighting | `Game.SetLightColor` | `GameSetLightColorExecutor` | Sets `Light.color`. |
| `Game.SetLightColorTemperature` | Set Light Color Temperature | Game/Lighting | `Game.SetLightColorTemperature` | `GameSetLightColorTemperatureExecutor` | Enables color temperature and sets `Light.colorTemperature` in Kelvin. |
| `Game.SetLightRange` | Set Light Range | Game/Lighting | `Game.SetLightRange` | `GameSetLightRangeExecutor` | Sets `Light.range`. |
| `Game.SetLightSpotAngle` | Set Light Spot Angle | Game/Lighting | `Game.SetLightSpotAngle` | `GameSetLightSpotAngleExecutor` | Sets `Light.spotAngle`. |
| `Input.GetAxis` | Get Axis | Input | `Input.GetAxis` | `InputGetAxisExecutor` | Reads a smoothed legacy Input Manager axis value. |
| `Input.GetAxisRaw` | Get Axis Raw | Input | `Input.GetAxisRaw` | `InputGetAxisRawExecutor` | Reads an unsmoothed legacy Input Manager axis value. |
| `Input.GetActionVector2` | Get Action Vector2 | Input | `Input.GetActionVector2` | `InputGetActionVector2Executor` | Reads a `Vector2` from a project-wide Input System action. |
| `Input.ListenKey` | Listen Key | Input | `Input.ListenKey` | `InputListenKeyExecutor` | Polls a keyboard key when executed and emits input-state exec outputs. |
| `Input.ListenAction` | Listen Action | Input | `Input.ListenAction` | `InputListenActionExecutor` | Polls a project-wide Input System action when executed and emits input-state exec outputs. |
| `UI.SetText` | Set Text | UI | `UI.SetText` | `UISetTextExecutor` | Sets text on a bound `TMP_Text`. |
| `UI.BindText` | Bind Text | UI | `UI.BindText` | `UIBindTextExecutor` | Registers a reactive `TMP_Text.text` binding to a local or cross-blueprint variable, applies it immediately, and reapplies it on reactive refreshes. |
| `UI.SpriteBinding` | Sprite Binding | UI | `UI.SpriteBinding` | `UISpriteBindingExecutor` | Outputs a bound Sprite name for image nodes. |
| `UI.SetImageSprite` | Set Image Sprite | UI | `UI.SetImageSprite` | `UISetImageSpriteExecutor` | Sets `Image.sprite` from a bound `Sprite`. |
| `UI.SetVisible` | Set Visible | UI | `UI.SetVisible` | `UISetVisibleExecutor` | Sets active state on a bound `GameObject` or component owner. |
| `UI.SetInteractable` | Set Interactable | UI | `UI.SetInteractable` | `UISetInteractableExecutor` | Sets `Selectable.interactable`. |
| `UI.SetGraphicColor` | Set Graphic Color | UI | `UI.SetGraphicColor` | `UISetGraphicColorExecutor` | Sets `Graphic.color`. |
| `UI.SetGraphicEnabled` | Set Graphic Enabled | UI | `UI.SetGraphicEnabled` | `UISetGraphicEnabledExecutor` | Sets `Graphic.enabled`. |
| `UI.SetGraphicRaycastTarget` | Set Graphic Raycast Target | UI | `UI.SetGraphicRaycastTarget` | `UISetGraphicRaycastTargetExecutor` | Sets `Graphic.raycastTarget`. |
| `UI.SetImageFillAmount` | Set Image Fill Amount | UI | `UI.SetImageFillAmount` | `UISetImageFillAmountExecutor` | Sets clamped `Image.fillAmount`. |
| `UI.SetCanvasGroupAlpha` | Set Canvas Group Alpha | UI | `UI.SetCanvasGroupAlpha` | `UISetCanvasGroupAlphaExecutor` | Sets clamped `CanvasGroup.alpha`. |
| `UI.SetCanvasGroupInteractable` | Set Canvas Group Interactable | UI | `UI.SetCanvasGroupInteractable` | `UISetCanvasGroupInteractableExecutor` | Sets `CanvasGroup.interactable`. |
| `UI.SetCanvasGroupBlocksRaycasts` | Set Canvas Group Blocks Raycasts | UI | `UI.SetCanvasGroupBlocksRaycasts` | `UISetCanvasGroupBlocksRaycastsExecutor` | Sets `CanvasGroup.blocksRaycasts`. |
| `UI.SetRectAnchoredPosition` | Set Rect Anchored Position | UI | `UI.SetRectAnchoredPosition` | `UISetRectAnchoredPositionExecutor` | Sets `RectTransform.anchoredPosition`. |
| `UI.SetRectSizeDelta` | Set Rect Size Delta | UI | `UI.SetRectSizeDelta` | `UISetRectSizeDeltaExecutor` | Sets `RectTransform.sizeDelta`. |
| `UI.SetRectLocalScale` | Set Rect Local Scale | UI | `UI.SetRectLocalScale` | `UISetRectLocalScaleExecutor` | Sets `RectTransform.localScale`. |
| `UI.BindButtonClick` | Bind Button Click | UI | `UI.BindButtonClick` | `UIBindButtonClickExecutor` | Executes the `clicked` output when a bound `Button` is clicked. |
| `UI.BindButtonEvents` | Bind Button Events | UI | `UI.BindButtonEvents` | `UIBindButtonEventsExecutor` | Executes mutually exclusive click, double-click, and long-press outputs from a bound `Button`. |
| `UI.BindToggleChanged` | Bind Toggle Changed | UI | `UI.BindToggleChanged` | `UIBindToggleChangedExecutor` | Executes toggle changed/on/off outputs and exposes current `Toggle.isOn`. |
| `UI.RefreshLoopScrollView` | Refresh Loop Scroll View | UI | `UI.RefreshLoopScrollView` | `UIRefreshLoopScrollViewExecutor` | Refreshes a bound `BlueprintLoopScrollView` from an array value or variable. |
| `Variable.Get` | Get Variable | Variables | `Variable.Get` | `VariableGetExecutor` | Reads a blueprint variable. |
| `Variable.Set` | Set Variable | Variables | `Variable.Set` | `VariableSetExecutor` | Writes a blueprint variable. |
| `Variable.Compare` | Compare | Variables | `Variable.Compare` | `VariableCompareExecutor` | Compares two values and outputs bool. |
| `Variable.GetField` | Get Field | Variables | `Variable.GetField` | `VariableGetFieldExecutor` | Reads a field or nested field path from a structured value. |
| `Variable.SetField` | Set Field | Variables | `Variable.SetField` | `VariableSetFieldExecutor` | Returns a copy of a structured value with one field or nested field path changed. |
| `Variable.BreakStruct` | Break Struct | Variables | `Variable.BreakStruct` | `VariableBreakStructExecutor` | Breaks a Blueprint user struct into one output per field. |
| `DataTable.GetRow` | Data Table Get Row | DataTable | `DataTable.GetRow` | `DataTableGetRowExecutor` | Reads a typed struct row from a Blueprint data table by row name. |
| `DataTable.GetRowNames` | Data Table Get Row Names | DataTable | `DataTable.GetRowNames` | `DataTableGetRowNamesExecutor` | Returns all row names from a Blueprint data table. |
| `DataTable.GetAllRows` | Data Table Get All Rows | DataTable | `DataTable.GetAllRows` | `DataTableGetAllRowsExecutor` | Returns all typed struct rows from a Blueprint data table. |
| `Resource.LoadAsync` | Resource Load Async | Resource | `Resource.LoadAsync` | `ResourceLoadAsyncExecutor` | Loads a primary resource through the runtime resource manager and resumes through loaded, failed, or cancelled. |
| `Resource.PreloadGroupAsync` | Resource Preload Group Async | Resource | `Resource.PreloadGroupAsync` | `ResourcePreloadGroupAsyncExecutor` | Preloads every resource in a named preload group. |
| `Resource.Release` | Resource Release | Resource | `Resource.Release` | `ResourceReleaseExecutor` | Releases a primary resource reference or all resources owned by a scope. |
| `Resource.GetLoadState` | Resource Get Load State | Resource | `Resource.GetLoadState` | `ResourceGetLoadStateExecutor` | Reads the current load state, loaded object, and last error for a primary resource. |
| `Resource.GetMetadata` | Resource Get Metadata | Resource | `Resource.GetMetadata` | `ResourceGetMetadataExecutor` | Reads one metadata value or the full metadata JSON for a primary resource. |
| `Array.Count` | Array Count | Array | `Array.Count` | `ArrayCountExecutor` | Returns item count from an array value. |
| `Array.Get` | Array Get | Array | `Array.Get` | `ArrayGetExecutor` | Returns an item from an array by index. |
| `Array.ForEachLoop` | For Each Loop | Array | `Array.ForEachLoop` | `ArrayForEachLoopExecutor` | Executes a loop body once per array item. |
| `Array.ForEachLoopWithBreak` | For Each Loop with Break | Array | `Array.ForEachLoopWithBreak` | `ArrayForEachLoopWithBreakExecutor` | Executes a loop body once per array item and supports early break. |
| `Array.IsValidIndex` | Array Is Valid Index | Array | `Array.IsValidIndex` | `ArrayIsValidIndexExecutor` | Returns true when an index is inside array bounds. |
| `Array.Contains` | Array Contains | Array | `Array.Contains` | `ArrayContainsExecutor` | Returns true when an array contains a matching basic value. |
| `Array.IndexOf` | Array Index Of | Array | `Array.IndexOf` | `ArrayIndexOfExecutor` | Returns the first index of a matching basic value. |
| `Array.First` | Array First | Array | `Array.First` | `ArrayFirstExecutor` | Returns the first item and validity flag from an array. |
| `Array.Last` | Array Last | Array | `Array.Last` | `ArrayLastExecutor` | Returns the last item and validity flag from an array. |

## Cross-Blueprint Access Nodes

These nodes mirror the common Unreal Blueprint access pattern: the parent declares components as separate blueprint assets, the runner creates runtime component instances, and graphs pass runtime references when they need to talk across that instance tree.

`Blueprint` target values store `.blueprint.json` asset paths as strings. At runtime, `Blueprint.IsValid`, `Blueprint.TriggerEvent`, `Blueprint.GetVariable`, and `Blueprint.SetVariable` resolve that path only inside the current `BlueprintRunner` instance tree. The resolver starts from `context.Instance`, walks up to the root owner runner, then recursively checks the root compiled asset source path and each declared component's blueprint path. It does not search tags, bindings, prefabs, or other scene runners.

`BlueprintRef` is a runtime-only handle around `IBlueprintInstance`. `Blueprint.GetOwner` returns the owner of the current component, supplied `BlueprintRef`, or a scene `BlueprintRunner` assigned in the runner's `ownerRunner` field. `Blueprint.GetComponent` takes a component name and searches from the current instance or supplied `BlueprintRef`, then walks owner instances outward so sibling components can be found through their shared owner. This lets interactable UI GameObjects run their own `UIBlueprintBinder` while sending intent to a shared panel runner. `BlueprintRef` values can connect into `Blueprint.IsValid`, `Blueprint.TriggerEvent`, `Blueprint.GetVariable`, and `Blueprint.SetVariable`; they are not valid `.blueprint.json` variable defaults or blackboard variable types.

If the same asset path appears more than once in the current component tree, asset-path resolution fails and logs a warning so a cross-blueprint read/write/event cannot silently hit the wrong instance. `Blueprint.GetByBinding` and `Blueprint.FindByTag` must not be used as cross-blueprint resolvers; migrate old scene-bound UI graphs to component declarations plus `GetOwner`/`GetComponent` or direct `Blueprint` asset-path target properties.

Blueprint components follow the Unreal Actor Component pattern. The parent blueprint stores a component declaration and a reference to another blueprint asset; runtime instances are created in memory by the parent runner and do not require child GameObjects:

```json
"components": [
  {
    "name": "AddItemBehavior",
    "blueprint": "Assets/Game/Blueprints/Inventory/Behavior/InventoryAddItemBehavior.blueprint.json",
    "required": true
  }
]
```

Create a blackboard variable of type `Blueprint` and set its default value to the target `.blueprint.json` path when a serialized asset-path reference is needed. Connect a normal `Variable.Get` for that variable into the cross-blueprint node's `target` input, set the node's `target` property directly to the same asset path, or connect a runtime `BlueprintRef` from `GetOwner`/`GetComponent`. In Graph Toolkit, direct `target` path values are edited through the Inspector and the `target` input hides its inline path editor so long asset paths do not expand the node body.

Graph Toolkit stores the component list as editor metadata on the `.bpgraph`, but `.blueprint.json` remains the source of truth. Auto-export skips a `.bpgraph` cache when it is older than the JSON source so stale visual graphs do not erase component declarations. When a currently open Graph Toolkit window saves and auto-exports successfully, the bridge reimports that `.bpgraph`, reloads the open Graph Toolkit graph from disk, and forces the UI state to rebuild immediately.

Only variables declared with `"exposed": true` on the target blueprint can be read or written by `Blueprint.GetVariable` and `Blueprint.SetVariable`. Non-exposed variables, missing variables, invalid targets, duplicate target paths, and uncompiled targets fail. `Blueprint.TriggerEvent` forwards the event name to the resolved target instance; if the target blueprint has no matching custom event, the existing VM warning is used.

`Blueprint.GetVariableFromGameObject` and `Blueprint.SetVariableFromGameObject` are direct scene-object bridges for the same exposed-variable rules when the target is a real `GameObject` with a `BlueprintRunner` or `UIBlueprintBinder` on that same object. The `target` input is `Binding<GameObject>` with `propertyOrConnection`, so JSON stores a binding name while runtime outputs such as pooled or instantiated `GameObject` values can also be connected. The resolver does not search parent or child transforms and does not resolve in-memory component blueprints; it calls `GetComponent<BlueprintRunner>()` on the resolved object and then reuses the standard exposed-variable read/write path, including reactive binding refresh after successful writes.

Ports:

| Node | Inputs | Outputs | Failure behavior |
| --- | --- | --- | --- |
| `Blueprint.GetVariableFromGameObject` | `target: Binding<GameObject>`, `name: string` | `value`, `success: bool` | Missing target, missing runner, missing/non-exposed variable, or uncompiled target returns `success=false` and `value=null`. |
| `Blueprint.SetVariableFromGameObject` | `execIn`, `target: Binding<GameObject>`, `name: string`, `value` | `execOut` | Missing target, missing runner, missing/non-exposed variable, uncompiled target, or rejected value returns an execution error. |

Avoid duplicates:

```text
Use Blueprint component declarations, `BlueprintRef` nodes, and `Blueprint` asset variables for owner-owned behavior modules before adding one-off direct-object access nodes.
Use bindings only for Unity object/component access nodes, not for cross-blueprint target resolution.
Use GameObject variable access only when the runtime object itself is the intended scene Blueprint owner.
Use `Blueprint.TriggerEvent` before adding specialized cross-blueprint event nodes.
Use exposed variables for small public state only; prefer custom events when another blueprint should own the mutation.
```

## Behavior Tree Blackboard Nodes

These nodes let a normal Blueprint graph read or mutate the Blackboard owned by a scene `BehaviorTreeRunner`. They are ordinary BlueprintSystem nodes, not Behavior Tree nodes, so they live under `Assets/BlueprintSystem/Specs/Nodes` and run through the normal `BlueprintVM`.

Targets use `Binding<BehaviorTreeRunner>` and store a binding name in JSON. `BlueprintRunner.Resolve<T>()` can resolve the binding directly or get `BehaviorTreeRunner` from a bound `GameObject` or `Component`, so `.blueprint.json` files still do not serialize Unity object references.

| Type ID | Purpose | Ports and notes |
| --- | --- | --- |
| `BehaviorTree.GetBlackboardBool` | Reads a bool Blackboard key. | Inputs `target: Binding<BehaviorTreeRunner>`, `key: string`; outputs `value: bool`, `success: bool`. Missing targets, empty keys, missing values, and cleared values return `success: false`; `value` returns `false`. |
| `BehaviorTree.GetBlackboardInt` | Reads an int Blackboard key. | Same inputs; outputs `value: int`, `success: bool`; default value is `0`. |
| `BehaviorTree.GetBlackboardFloat` | Reads a float Blackboard key. | Same inputs; outputs `value: float`, `success: bool`; default value is `0`. |
| `BehaviorTree.GetBlackboardString` | Reads a string Blackboard key. | Same inputs; outputs `value: string`, `success: bool`; default value is an empty string. |
| `BehaviorTree.GetBlackboardVector3` | Reads a `Vector3` Blackboard key. | Same inputs; outputs `value: Vector3`, `success: bool`; default value is `[0,0,0]`. |
| `BehaviorTree.GetBlackboardGameObject` | Reads a runtime `GameObject` Blackboard key. | Same inputs; outputs `value: GameObject`, `success: bool`; missing or incompatible values output `null`. |
| `BehaviorTree.SetBlackboardBool` | Writes a bool Blackboard key. | Exec input `execIn`; inputs `target`, `key`, `value: bool`; output `execOut`. Missing target or empty key returns an execution error. |
| `BehaviorTree.SetBlackboardInt` | Writes an int Blackboard key. | Same shape with `value: int`. |
| `BehaviorTree.SetBlackboardFloat` | Writes a float Blackboard key. | Same shape with `value: float`. |
| `BehaviorTree.SetBlackboardString` | Writes a string Blackboard key. | Same shape with `value: string`. |
| `BehaviorTree.SetBlackboardVector3` | Writes a `Vector3` Blackboard key. | Same shape with `value: Vector3`. |
| `BehaviorTree.SetBlackboardGameObject` | Writes a runtime `GameObject` Blackboard key. | Same shape with `value: Binding<GameObject>`. The value may be a JSON binding name or a connected runtime `GameObject`/`Transform`/`Component`; the Blackboard stores the resolved runtime `GameObject`, not a serialized object reference. |
| `BehaviorTree.ClearBlackboard` | Clears one Blackboard key by setting its runtime value to null. | Exec input `execIn`; inputs `target`, `key`; output `execOut`. Missing target or empty key returns an execution error. |

Avoid duplicates:

```text
Use Behavior Tree-native `BT.SetBlackboard`, decorators, and services when the logic belongs inside the tree.
Use these Blueprint nodes when scene UI, demo visualization, or a normal Blueprint needs to observe or poke a running tree.
Use `BehaviorTree.SetBlackboardGameObject` for binding-based or runtime-object handoff into AI Blackboard state; keep `Transform` writes in C# or Behavior Tree nodes until a dedicated binding-based Transform write is explicitly needed.
Do not store direct `GameObject`, `Transform`, `Component`, or other Unity object references as `.blueprint.json` defaults.
```

## Behavior Tree Module

Behavior Tree is a core module kept under `Assets/BlueprintSystem/BehaviorTree`. It is default-on with BlueprintSystem and includes the `.btree.json` compiler, `BT.*` executor registry, `BehaviorTreeRunner`, Graph Toolkit bridge, debugger, and the normal Blueprint `BehaviorTree.*` blackboard bridge nodes.

The module can be toggled per active build target in `Project Settings > Blueprint System > Modules`. The setting is default-on; disabling it writes the `BLUEPRINTSYSTEM_DISABLE_BEHAVIOR_TREE` scripting define for the active build target and triggers script recompilation. When disabled, `BehaviorTree.*` manifests are not loaded, their executors are not registered, `BT.*` executors are not registered, Behavior Tree Graph Toolkit/import/export/debugger entry points are disabled, and `BehaviorTreeRunner` components remain serialized but do not start or tick. Existing blueprints or behavior trees that still contain Behavior Tree nodes report normal missing-manifest, missing-executor, or compile errors after they are recompiled while the module is disabled.

## VehicleRoads Module

VehicleRoads is a SmartObject-style optional plugin module kept under `Assets/BlueprintSystem/VehicleRoads` rather than the generic node folders. It is default-on with BlueprintSystem, but its runtime, executors, manifests, Graph Toolkit nodes, tests, settings, and docs stay inside that module directory.

The module can be toggled per active build target in `Project Settings > Blueprint System > Modules`. The setting is default-on; disabling it writes the `BLUEPRINTSYSTEM_DISABLE_VEHICLE_ROADS` scripting define for the active build target and triggers script recompilation. When disabled, `VehicleRoad.*` manifests are not loaded, VehicleRoad executors are not registered, VehicleRoad Graph Toolkit visual nodes fall back to generic nodes, and `BT.VehicleRoad.*` Behavior Tree executors are unavailable. Existing blueprints or behavior trees that still contain VehicleRoad nodes report normal missing-manifest, missing-executor, or compile errors after they are recompiled while the module is disabled.

Blueprint nodes expose runtime-only calls: `VehicleRoad.FindNearestLane`, `VehicleRoad.FindLaneRoute`, `VehicleRoad.GetLaneIds`, `VehicleRoad.GetRouteCandidateLaneIds`, `VehicleRoad.FindSpawnLaneAroundTransform`, `VehicleRoad.SelectReachableRouteTarget`, `VehicleRoad.FilterLaneIds`, `VehicleRoad.GetLaneInfo`, `VehicleRoad.SetLaneClosed`, `VehicleRoad.SetLaneCongestionCost`, `VehicleRoad.UpdateVehicle`, `VehicleRoad.UnregisterVehicle`, `VehicleRoad.EvaluateTrafficControl`, `VehicleRoad.EvaluateLaneOccupancy`, `VehicleRoad.EvaluateLaneChangeRoute`, `VehicleRoad.RequestLaneChange`, `VehicleRoad.CompleteLaneChange`, `VehicleRoad.SetFollowerRoute`, `VehicleRoad.ComputeFollowerControl`, and `VehicleRoad.GetSubsystemSnapshot`. Nodes resolve scene objects through binding names or connected runtime objects, never serialized Unity references in JSON. Side-effectful/random-selection nodes are exec nodes and cache their last result for output ports. Route-level lane-change evaluation nodes are read-only; they do not create reservations and should be followed by `VehicleRoad.RequestLaneChange` only when their request output is true.

Behavior Tree-native road nodes live in the Behavior Tree registry, not in `.node.json` manifests: `BT.VehicleRoad.FindNearestLane`, `BT.VehicleRoad.FindLaneRoute`, `BT.VehicleRoad.SetFollowerRoute`, `BT.VehicleRoad.SelectNextRouteTarget`, `BT.VehicleRoad.ComputeFollowerControl`, `BT.VehicleRoad.DriveFollower`, the traffic/lane-change strategy tasks `BT.VehicleRoad.UpdateTrafficState`, `BT.VehicleRoad.DecideLaneChange`, `BT.VehicleRoad.EvaluateLaneOccupancy`, `BT.VehicleRoad.EvaluateLaneChangeRoute`, `BT.VehicleRoad.RequestLaneChange`, `BT.VehicleRoad.CompleteLaneChange`, the split follower tasks `BT.VehicleRoad.UpdateFollowerSpeed`, `BT.VehicleRoad.EvaluateStopPointTravel`, `BT.VehicleRoad.ApplyStopPoint`, `BT.VehicleRoad.CheckFollowerRouteEnd`, `BT.VehicleRoad.MoveAlongBakedRoute`, `BT.VehicleRoad.MoveTowardLookAhead`, `BT.VehicleRoad.CaptureLoopStart`, `BT.VehicleRoad.TickLoopReset`, `BT.VehicleRoad.UnregisterVehicle`, and the `BT.VehicleRoad.UpdateRoadAgent` service. Query/control-output nodes write Blackboard keys and do not move GameObjects; `BT.VehicleRoad.DriveFollower` is the kinematic wrapper for demo/AI vehicles, while split follower and strategy tasks expose the same movement/traffic steps for custom trees. Detailed authoring rules, port lists, and runtime ownership boundaries live in `Assets/BlueprintSystem/VehicleRoads/Docs/VehicleRoadUsageGuide.md` and `Assets/BlueprintSystem/BehaviorTree/GUIDE.md`.

## SmartObject Module

SmartObject is a core module kept under `Assets/BlueprintSystem/SmartObject` rather than the generic node folders. It is default-on with BlueprintSystem, but its runtime, executors, manifests, Graph Toolkit nodes, tests, and detailed guide stay inside that module directory.

The module can be toggled per active build target in `Project Settings > Blueprint System > Modules`. The setting is default-on; disabling it writes the `BLUEPRINTSYSTEM_DISABLE_SMARTOBJECT` scripting define for the active build target and triggers script recompilation. When disabled, SmartObject manifests are not loaded, SmartObject executors are not registered, SmartObject Graph Toolkit visual nodes are hidden, the SmartObject debugger menu is disabled, and `SmartObjectComponent` instances remain serialized but do not register at runtime. Existing blueprints that still contain `SmartObject.*` nodes report normal missing-manifest or missing-executor validation/compile errors after they are recompiled while the module is disabled.

The module exposes `SmartObject.FindBest`, `SmartObject.FindBestActor`, `SmartObject.Reserve`, `SmartObject.BeginUse`, `SmartObject.Release`, `SmartObject.GetReservationInfo`, and `SmartObject.ReleaseByRequester`. `SmartObject.FindBestActor` adds an optional `excludeGameObject: Binding<GameObject>` and `targetGameObject: GameObject` output for actor handshakes that must skip the requester's own SmartObject. `SmartObjectComponent` ids are generated read-only GUID strings; detailed authoring rules, port lists, fail reasons, and duplicate-node guidance live in `Assets/BlueprintSystem/SmartObject/GUIDE.md`.

## Blueprint Asset Variables

Graph Toolkit supports a `Blueprint` blackboard variable type for `.blueprint.json` asset references. This is an editor-friendly path value: the JSON default value is the blueprint asset path string, not a `BlueprintRef`, `BlueprintRunner`, `TextAsset`, or other Unity object reference. Dragging a `.blueprint.json` asset into a visual graph can create a `Blueprint` variable and then a normal `Variable.Get` or `Variable.Set` node for that variable. Cross-blueprint access nodes accept this same `Blueprint` type on their `target` input, and the same target input can also receive runtime `BlueprintRef` output from `Blueprint.GetOwner` or `Blueprint.GetComponent`.

## High Priority Unreal Parity Nodes

The following nodes extend the system with high-priority Unreal Blueprint-style flow, math, vector/color, string, and array operations.

| Family | Type IDs | Purpose |
| --- | --- | --- |
| Flow loops | `Flow.ForLoop`, `Flow.ForLoopWithBreak` | Execute `loopBody` for inclusive integer ranges and expose current `index`; the break variant stops when its `break` exec input is triggered during the active loop. |
| Flow gates | `Flow.DoOnce`, `Flow.DoN`, `Flow.FlipFlop`, `Flow.Gate`, `Flow.MultiGate` | Stateful execution helpers for one-shot, limited-count, alternating, open/closed, and multi-output routing. `Flow.MultiGate` has fixed `out0`-`out7` pins and uses `outputCount` to choose the active subset. |
| Flow switches | `Flow.SwitchInt`, `Flow.SwitchString`, `Flow.SwitchEnum` | Route execution to `case0`-`case7` or `default`. `Flow.SwitchEnum` compares enum names as strings so project-specific enum types do not need separate manifests. |
| Float math | `Math.Add`, `Math.Subtract`, `Math.Multiply`, `Math.Divide`, `Math.Modulo`, `Math.Abs`, `Math.Clamp`, `Math.Min`, `Math.Max`, `Math.Round`, `Math.Floor`, `Math.Ceil`, `Math.Lerp`, `Math.MapRangeClamped`, `Math.RandomFloat`, `Math.RandomInt`, `Math.RandomBool` | Common scalar math and random value nodes. Divide/modulo return `0` when the divisor is zero. Random int is inclusive on both ends. |
| Vector constructors | `Vector.MakeVector2`, `Vector.BreakVector2`, `Vector.MakeVector3`, `Vector.BreakVector3`, `Vector.MakeVector4`, `Vector.BreakVector4` | Build or split Unity vector values from scalar components. |
| Vector math | `Vector.Add`, `Vector.Subtract`, `Vector.Multiply`, `Vector.Divide`, `Vector.Dot`, `Vector.Cross`, `Vector.Length`, `Vector.Normalize`, `Vector.Distance`, `Vector.Lerp` | Vector operations currently target `Vector3`; multiply/divide use a scalar input. |
| Color math | `Color.Make`, `Color.Break`, `Color.Lerp` | Build, split, and interpolate Unity `Color` values. |
| String utilities | `String.Append`, `String.Format`, `String.ToString`, `String.Contains`, `String.StartsWith`, `String.EndsWith`, `String.Replace`, `String.Split`, `String.Length`, `String.Substring`, `String.EqualIgnoreCase` | Text construction, formatting, comparison, replacement, splitting, and substring helpers. `String.Split` outputs `Array<string>`. |
| Array construction | `Array.Make`, `Array.Append`, `Array.Clear`, `Array.Resize`, `Array.Shuffle` | Create or transform array values. These nodes return a new array value instead of mutating a variable directly. |
| Array mutation-style values | `Array.Add`, `Array.AddUnique`, `Array.Insert`, `Array.RemoveIndex`, `Array.RemoveItem`, `Array.SetElement`, `Array.RandomItem`, `Array.LastIndex` | Return changed array copies plus useful metadata such as index, removed, added, success, or validity flags. Connect the returned `array` output into `Variable.Set.value` when persistent variable changes are needed. |
| Tick and time | `Game.Event.OnTick`, `Game.GetDeltaTime`, `Game.GetFixedDeltaTime`, `Game.GetTimeSeconds`, `Game.GetUnscaledTime`, `Game.GetTimeScale`, `Game.SetTimeScale` | Frame tick entry and common Unity time values/actions. `Game.Event.OnTick.phase` chooses `Update`, `FixedUpdate`, or `LateUpdate`; Runner only fires tick events that exist, so blueprints without Tick do not log every frame. |
| Object instantiation | `Game.InstantiateObject` | Clone an already available `GameObject` prefab from a binding or a connected runtime asset such as `Resource.LoadAsync.asset`; outputs the created `GameObject` and `Transform`. |
| GameObject lifecycle | `GameObject.SetActive`, `GameObject.Destroy` | Act on connected runtime `GameObject` values such as `Game.InstantiateObject.instance`; these nodes do not resolve binding names or store Unity object references in JSON. |
| GameObject pooling | `GameObject.PrewarmPool`, `GameObject.AcquireFromPool`, `GameObject.ReleaseToPool`, `GameObject.ClearPool`, `GameObject.GetPoolStats`, `GameObject.GetPoolActiveInstances` | Unreal-style runner-scoped object pooling and read-only pool queries for `GameObject` prefabs using string `poolId` keys. |
| Transform access/actions | `Game.GetTransformPosition`, `Game.GetTransformEulerAngles`, `Game.GetTransformLocalPosition`, `Game.GetTransformLocalEulerAngles`, `Game.GetTransformLocalScale`, `Game.GetTransformForward`, `Game.GetTransformRight`, `Game.GetTransformUp`, `Game.SetTransformLocalPosition`, `Game.SetTransformLocalEulerAngles`, `Game.TranslateTransform`, `Game.RotateTransform`, `Game.LookAtTransform`, `Game.SetTransformParent`, `Game.DetachTransform` | Common Unity Transform getters, local setters, movement/rotation actions, look-at, parent, and detach helpers. All target references are `Binding<Transform>` strings resolved at runtime. |
| Lighting actions | `Game.SetLightEnabled`, `Game.SetLightIntensity`, `Game.SetLightColor`, `Game.SetLightColorTemperature`, `Game.SetLightRange`, `Game.SetLightSpotAngle` | Common Unity `Light` component setters. All targets are `Binding<Light>` strings resolved at runtime. |
| Physics queries | `Game.Raycast`, `Game.SphereCast`, `Game.BoxCast`, `Game.OverlapSphere`, `Game.OverlapBox`, `Game.Raycast2D`, `Game.OverlapCircle2D`, `Game.OverlapBox2D` | 3D/2D query nodes that return plain blueprint values such as hit booleans, points, normals, distances, counts, first object name, and `Array<string>` object names. They do not serialize Unity object references. |

Avoid duplicates:

```text
Use these math/vector/string/array nodes before adding domain-specific one-off helper nodes.
Use `Flow.SwitchString` or `Flow.SwitchEnum` before creating specialized enum switch nodes unless the enum needs a custom editor dropdown.
Array mutation-style nodes are pure value transforms; do not add hidden variable side effects to them.
Use Transform binding nodes before creating domain-specific movement helpers.
Use physics query result names or follow-up bindings before adding JSON-stored Unity object references.
Use `Resource.LoadAsync` before `Game.InstantiateObject` when a prefab needs asynchronous resource loading.
```

## Tick, Time, Transform, and Physics Nodes

### Tick and Time

| Type ID | Purpose | Ports and notes |
| --- | --- | --- |
| `Game.Event.OnTick` | Entry fired by `BlueprintRunner.Update`, `FixedUpdate`, or `LateUpdate`. | Node input/property `phase: TickPhase` defaults to `Update` and is shown as a dropdown in Graph Toolkit; older serialized visual nodes backfill the missing `phase` input when Graph Toolkit defines ports/options. `Update` maps to event name `OnTick`, `FixedUpdate` maps to `OnFixedTick`, and `LateUpdate` maps to `OnLateTick`. Output `execOut`. Runner checks `RuntimeBlueprint.EventEntries` before firing so missing tick events stay quiet. |
| `Game.GetDeltaTime` | Reads `Time.deltaTime`. | Output `value: float`. |
| `Game.GetFixedDeltaTime` | Reads `Time.fixedDeltaTime`. | Output `value: float`. |
| `Game.GetTimeSeconds` | Reads `Time.time`. | Output `value: float`. |
| `Game.GetUnscaledTime` | Reads `Time.unscaledTime`. | Output `value: float`. |
| `Game.GetTimeScale` | Reads `Time.timeScale`. | Output `value: float`. |
| `Game.SetTimeScale` | Sets `Time.timeScale`. | Input `value: float`; negative values are clamped to `0`; output `execOut`. |

Avoid duplicates:

```text
Use `Game.Event.OnTick.phase` for Update, FixedUpdate, and LateUpdate behavior before adding specialized update events.
Use the time getter nodes instead of reading Unity Time directly in domain-specific executors.
```

### Object Instantiation

| Type ID | Purpose | Ports and notes |
| --- | --- | --- |
| `Game.InstantiateObject` | Instantiates a `GameObject` prefab. | `prefab: Binding<GameObject>` is required and may be a binding name or a connected runtime object such as `Resource.LoadAsync.asset`; optional `parent: Binding<Transform>` may be a binding name or connected `Transform`; outputs `instance: GameObject`, `transform: Transform`, and `execOut`. |

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.InstantiateObject.node.json
```

Executor:

```text
ID: Game.InstantiateObject
Class: GameInstantiateObjectExecutor
File: Assets/BlueprintSystem/Executors/Game/GameExecutors.cs
```

Function:

```text
Resolve `prefab` as a GameObject from a direct runtime object, Component owner, or binding name.
Resolve optional `parent` as a Transform from a direct runtime object, GameObject, Component, or binding name.
When `parent` is present, instantiate under it and set `localPosition` to `[0, 0, 0]`.
When `parent` is absent, instantiate as a scene root and set world position to `[0, 0, 0]`.
Preserve prefab-authored rotation and scale.
Return an error and stop when `prefab` is missing/invalid, or when a supplied `parent` cannot resolve.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts instantiation. |
| `prefab` | input value | `Binding<GameObject>` | propertyOrConnection | yes | none | Binding name string or connected runtime asset/object. |
| `parent` | input value | `Binding<Transform>` | propertyOrConnection | no | none | Binding name string or connected runtime Transform/GameObject/Component. |
| `prefab` | property | `Binding<GameObject>` | property | no | none | Optional binding-name fallback; no Unity object is serialized in JSON. |
| `parent` | property | `Binding<Transform>` | property | no | none | Optional binding-name fallback; empty means no parent. |
| `execOut` | output exec | none | none | no | none | Continuation after a successful instantiate. |
| `instance` | output value | `GameObject` | none | no | none | Runtime-only created GameObject. |
| `transform` | output value | `Transform` | none | no | none | Runtime-only created Transform. |

`GameObject`, `Transform`, and `Component` runtime outputs can connect to compatible `Binding<GameObject>` and `Binding<Transform>` inputs. They are runtime values only and must not be declared as persisted variables or serialized Unity references in `.blueprint.json`.

### GameObject Lifecycle

| Type ID | Purpose | Ports and notes |
| --- | --- | --- |
| `GameObject.SetActive` | Sets active state on a runtime `GameObject`. | Required `target: GameObject` must be connected from a runtime value output; `active: bool` defaults to `true` and may be connected or set as a property; output `execOut`. |
| `GameObject.Destroy` | Destroys a runtime `GameObject`. | Required `target: GameObject` must be connected from a runtime value output; output `execOut`. |

Manifests:

```text
Assets/BlueprintSystem/Specs/Nodes/GameObject.SetActive.node.json
Assets/BlueprintSystem/Specs/Nodes/GameObject.Destroy.node.json
```

Executors:

```text
ID: GameObject.SetActive
Class: GameObjectSetActiveExecutor
File: Assets/BlueprintSystem/Executors/Game/GameExecutors.cs

ID: GameObject.Destroy
Class: GameObjectDestroyExecutor
File: Assets/BlueprintSystem/Executors/Game/GameExecutors.cs
```

Function:

```text
Read `target` as a direct runtime GameObject value from a value connection.
Do not resolve `target` through Binding<GameObject> names or JSON properties.
GameObject.SetActive calls `target.SetActive(active)`.
GameObject.Destroy calls `Object.Destroy(target)` in play mode and `Object.DestroyImmediate(target)` outside play mode.
Return an error and stop when `target` is missing or not a GameObject.
```

### GameObject Pool

These nodes provide Unreal-style object pooling for `GameObject` prefabs. Pools are scoped to the current `BlueprintRunner` owner and keyed by `poolId`; the same `poolId` in another runner is a separate pool. In test or headless contexts without an owner, the pool registry is stored in the current execution context state.

| Type ID | Purpose | Ports and notes |
| --- | --- | --- |
| `GameObject.PrewarmPool` | Creates inactive instances up to `capacity`. | `poolId: string` defaults to `default`; required `prefab: Binding<GameObject>` may be a binding name or connected runtime prefab; optional `parent: Binding<Transform>` is used for newly created instances; `capacity: int` defaults to `10`; output `execOut`. |
| `GameObject.AcquireFromPool` | Checks out an instance. | `poolId: string` defaults to `default`; optional `prefab` is required only when the pool does not exist yet or needs to expand from an empty pool; optional `parent` is used for newly created instances; `activate: bool` defaults to `true`; `expandIfEmpty: bool` defaults to `true`; outputs `instance: GameObject`, `transform: Transform`, `success: bool`, and `execOut`. |
| `GameObject.ReleaseToPool` | Returns a managed instance. | Required `target: GameObject` must be connected from a runtime value output; `poolId: string` defaults to `default`; `deactivate: bool` defaults to `true`; outputs `reset`, `released: bool`, `target: GameObject`, and `execOut`. |
| `GameObject.ClearPool` | Destroys all managed instances. | `poolId: string` defaults to `default`; outputs `destroyedCount: int` and `execOut`. |
| `GameObject.GetPoolStats` | Reads pool counts without creating a pool. | `poolId: string` defaults to `default`; outputs `activeCount: int`, `availableCount: int`, `managedCount: int`, and `exists: bool`. Missing pools return zero counts and `exists=false`. |
| `GameObject.GetPoolActiveInstances` | Returns checked-out instances. | `poolId: string` defaults to `default`; outputs `instances: Array<GameObject>` as a runtime snapshot. Missing pools return an empty array. Connect this output to `Array.ForEachLoop` to iterate active instances. |

Manifests:

```text
Assets/BlueprintSystem/Specs/Nodes/GameObject.PrewarmPool.node.json
Assets/BlueprintSystem/Specs/Nodes/GameObject.AcquireFromPool.node.json
Assets/BlueprintSystem/Specs/Nodes/GameObject.ReleaseToPool.node.json
Assets/BlueprintSystem/Specs/Nodes/GameObject.ClearPool.node.json
Assets/BlueprintSystem/Specs/Nodes/GameObject.GetPoolStats.node.json
Assets/BlueprintSystem/Specs/Nodes/GameObject.GetPoolActiveInstances.node.json
```

Executors:

```text
ID: GameObject.PrewarmPool
Class: GameObjectPrewarmPoolExecutor
File: Assets/BlueprintSystem/Executors/Game/GameObjectPoolExecutors.cs

ID: GameObject.AcquireFromPool
Class: GameObjectAcquireFromPoolExecutor
File: Assets/BlueprintSystem/Executors/Game/GameObjectPoolExecutors.cs

ID: GameObject.ReleaseToPool
Class: GameObjectReleaseToPoolExecutor
File: Assets/BlueprintSystem/Executors/Game/GameObjectPoolExecutors.cs

ID: GameObject.ClearPool
Class: GameObjectClearPoolExecutor
File: Assets/BlueprintSystem/Executors/Game/GameObjectPoolExecutors.cs

ID: GameObject.GetPoolStats
Class: GameObjectGetPoolStatsExecutor
File: Assets/BlueprintSystem/Executors/Game/GameObjectPoolExecutors.cs

ID: GameObject.GetPoolActiveInstances
Class: GameObjectGetPoolActiveInstancesExecutor
File: Assets/BlueprintSystem/Executors/Game/GameObjectPoolExecutors.cs
```

Function:

```text
Resolve `prefab` and `parent` the same way as Game.InstantiateObject.
Prewarm and Acquire create inactive instances first, then Acquire sets active state from `activate`.
Release only accepts instances created or already managed by the same pool; it is not a generic SetActive(false) node.
When a release actually returns an object to the pool, `GameObject.ReleaseToPool.reset` executes synchronously before `deactivate` is applied and before the object is added back to the available list.
Use `GameObject.ReleaseToPool.target` inside the reset branch to connect the released instance to Transform, Rigidbody, GameObject, or other compatible runtime-object inputs.
If a reset branch uses async flow such as `Flow.Delay`, delayed continuations may run after the object has already been deactivated and returned to the pool.
Repeated release attempts for an object that is already available, or already in a pending release reset, return `released=false` and do not execute `reset`.
Acquire returns success=false without error when the pool is empty and expandIfEmpty=false.
Acquire returns an error when it must create a pool or expand an empty pool but cannot resolve a prefab.
Supplying a different prefab for an existing pool id is an error.
ClearPool destroys both active and inactive managed instances, then removes that pool id.
Pool query nodes are read-only: they do not create a pool registry or add `BlueprintGameObjectPoolHost` to the owner.
`activeCount` and active instances mean checked out from the pool and not yet released; they are not based on `GameObject.activeSelf`.
`GameObject.GetPoolActiveInstances.instances` is a runtime-only snapshot, order is unspecified, and it should not be stored as a persisted variable default.
Object reset and Rigidbody velocity cleanup should be authored on the `GameObject.ReleaseToPool.reset` branch with existing Blueprint nodes.
Custom OnAcquire behavior should still be authored explicitly after `GameObject.AcquireFromPool`.
```

### Transform

Getter nodes:

| Type IDs | Output |
| --- | --- |
| `Game.GetTransformPosition`, `Game.GetTransformEulerAngles`, `Game.GetTransformLocalPosition`, `Game.GetTransformLocalEulerAngles`, `Game.GetTransformLocalScale`, `Game.GetTransformForward`, `Game.GetTransformRight`, `Game.GetTransformUp` | Input `target: Binding<Transform>`; output `value: Vector3`. Missing bindings log an error and return a safe default. |

Action nodes:

| Type ID | Purpose | Ports and notes |
| --- | --- | --- |
| `Game.SetTransformLocalPosition` | Sets `Transform.localPosition`. | `target: Binding<Transform>`, `value: Vector3`, `execOut`. |
| `Game.SetTransformLocalEulerAngles` | Sets `Transform.localEulerAngles`. | `target: Binding<Transform>`, `value: Vector3`, `execOut`. |
| `Game.TranslateTransform` | Calls `Transform.Translate`. | `translation: Vector3`, `relativeToSelf: bool`; true uses `Space.Self`, false uses `Space.World`. |
| `Game.RotateTransform` | Calls `Transform.Rotate`. | `eulerAngles: Vector3`, `relativeToSelf: bool`; true uses `Space.Self`, false uses `Space.World`. |
| `Game.LookAtTransform` | Calls `Transform.LookAt`. | Uses optional `lookTarget: Binding<Transform>` when present, otherwise `targetPosition: Vector3`; `worldUp` defaults to `[0,1,0]`. |
| `Game.SetTransformParent` | Calls `Transform.SetParent(parent, worldPositionStays)`. | `parent: Binding<Transform>` is required. |
| `Game.DetachTransform` | Clears the parent via `SetParent(null, worldPositionStays)`. | Keeps world position by default. |

Avoid duplicates:

```text
Use `Binding<Transform>` strings for targets and parents.
Do not store direct Transform, GameObject, or Component references in `.blueprint.json`.
```

### Physics Queries

3D query nodes:

| Type ID | Inputs | Outputs |
| --- | --- | --- |
| `Game.Raycast` | `origin`, `direction`, `maxDistance`, `layerMask`, `includeTriggers` | `hit`, `point`, `normal`, `distance`, `colliderName`, `gameObjectName` |
| `Game.SphereCast` | `origin`, `radius`, `direction`, `maxDistance`, `layerMask`, `includeTriggers` | Same raycast outputs. |
| `Game.BoxCast` | `center`, `halfExtents`, `direction`, `orientationEuler`, `maxDistance`, `layerMask`, `includeTriggers` | Same raycast outputs. |
| `Game.OverlapSphere` | `center`, `radius`, `layerMask`, `includeTriggers` | `hasAny`, `count`, `firstName`, `names: Array<string>` |
| `Game.OverlapBox` | `center`, `halfExtents`, `orientationEuler`, `layerMask`, `includeTriggers` | Same overlap outputs. |

2D query nodes:

| Type ID | Inputs | Outputs |
| --- | --- | --- |
| `Game.Raycast2D` | `origin`, `direction`, `distance`, `layerMask` | `hit`, `point: Vector2`, `normal: Vector2`, `distance`, `colliderName`, `gameObjectName` |
| `Game.OverlapCircle2D` | `point`, `radius`, `layerMask` | `hasAny`, `count`, `firstName`, `names: Array<string>` |
| `Game.OverlapBox2D` | `point`, `size`, `angle`, `layerMask` | Same overlap outputs. |

Notes:

```text
Layer mask defaults to `-1` (all layers).
3D query `includeTriggers` defaults to true and maps to Unity `QueryTriggerInteraction.Collide`.
Ray/cast distance values <= 0 are treated as infinite.
Query nodes return primitive values and object names, not serialized Unity object references.
```

## Event Nodes

### `Game.Event.OnStart`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.Event.OnStart.node.json
```

Executor:

```text
ID: Flow.Event
Class: FlowEventExecutor
File: Assets/BlueprintSystem/Executors/Flow/FlowExecutors.cs
```

Function:

```text
Entry point for the `OnStart` event.
BlueprintRunner.Start triggers `startEventName`, default `OnStart`, when `triggerOnStart` is true.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execOut` | output exec | none | none | no | Allows multiple outgoing exec edges. |

Avoid duplicates:

```text
Do not add another startup event node unless it has different lifecycle semantics.
For a different event name, use `Game.Event.Custom`.
```

### `Game.Event.Custom`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.Event.Custom.node.json
```

Executor:

```text
ID: Flow.Event
Class: FlowEventExecutor
File: Assets/BlueprintSystem/Executors/Flow/FlowExecutors.cs
```

Function:

```text
Entry point for any named event.
Can be triggered by `BlueprintRunner.TriggerEvent` or `Game.SendEvent`.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `eventName` | property | string | property | yes | Name registered in `RuntimeBlueprint.EventEntries`. |
| `execOut` | output exec | none | none | no | Allows multiple outgoing exec edges. |

Graph Toolkit labels `eventName` as `Event`, shows the listened event on the node title as `Custom Event: Ping`, and mirrors it on the `execOut` port so custom event entries can be identified directly on the graph.

Avoid duplicates:

```text
Use this for any domain-specific event name before creating a specialized event node.
Specialized event nodes are only useful when the event name is implied by lifecycle or UI semantics.
```

### `UI.Event.OnOpen`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.Event.OnOpen.node.json
```

Executor:

```text
ID: Flow.Event
Class: FlowEventExecutor
File: Assets/BlueprintSystem/Executors/Flow/FlowExecutors.cs
```

Function:

```text
Entry point for `OnOpen`.
UIBlueprintBinder.OnEnable triggers `enableEventName`, default `OnOpen`, when `triggerOnEnable` is true.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execOut` | output exec | none | none | no | Allows multiple outgoing exec edges. |

Avoid duplicates:

```text
Reuse this for panel-open behavior.
For a custom panel event, use `Game.Event.Custom`.
```

### `UI.Event.OnClose`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.Event.OnClose.node.json
```

Executor:

```text
ID: Flow.Event
Class: FlowEventExecutor
File: Assets/BlueprintSystem/Executors/Flow/FlowExecutors.cs
```

Function:

```text
Entry point for `OnClose`.
UIBlueprintBinder.OnDisable triggers `disableEventName`, default `OnClose`, when `triggerOnDisable` is true.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execOut` | output exec | none | none | no | Allows multiple outgoing exec edges. |

Avoid duplicates:

```text
Reuse this for panel-close behavior.
For button click close actions, connect `UI.BindButtonClick.clicked` directly to the close behavior.
```

## Flow Nodes

### `Flow.Branch`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Flow.Branch.node.json
```

Executor:

```text
ID: Flow.Branch
Class: FlowBranchExecutor
File: Assets/BlueprintSystem/Executors/Flow/FlowExecutors.cs
```

Function:

```text
Reads `condition`.
Continues through output `true` when true, otherwise output `false`.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts the branch. |
| `condition` | input value | bool | propertyOrConnection | yes | `false` | Connection overrides property. |
| `condition` | property | bool | property | no | `false` | Used when no value edge is connected. |
| `true` | output exec | none | none | no | none | Executed when condition is true. |
| `false` | output exec | none | none | no | none | Executed when condition is false. |

Avoid duplicates:

```text
Use this for all boolean branching.
Do not create `If`, `BoolBranch`, or `Conditional` unless a new comparison/evaluation feature is needed.
```

### `Flow.Sequence`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Flow.Sequence.node.json
```

Executor:

```text
ID: Flow.Sequence
Class: FlowSequenceExecutor
File: Assets/BlueprintSystem/Executors/Flow/FlowExecutors.cs
```

Function:

```text
Queues `then0`, `then1`, `then2`, and `then3` in order.
Unconnected outputs are harmless.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts the sequence. |
| `then0` | output exec | none | none | no | First branch. |
| `then1` | output exec | none | none | no | Second branch. |
| `then2` | output exec | none | none | no | Third branch. |
| `then3` | output exec | none | none | no | Fourth branch. |

Avoid duplicates:

```text
Use this for simple multi-step fan-out.
If more than four outputs are needed, extend this node deliberately instead of adding `Sequence5`, `MultiExec`, etc.
```

### `Flow.Delay`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Flow.Delay.node.json
```

Executor:

```text
ID: Flow.Delay
Class: FlowDelayExecutor
File: Assets/BlueprintSystem/Executors/Flow/FlowExecutors.cs
```

Function:

```text
Reads `seconds`.
Suspends execution and resumes through `execOut` after `WaitForSeconds(seconds)`.
If there is no MonoBehaviour coroutine host, or seconds <= 0, continuation happens immediately.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts delay. |
| `seconds` | input value | float | propertyOrConnection | yes | `0` | Delay duration in seconds. |
| `seconds` | property | float | property | no | `0` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Resumed continuation. |

Avoid duplicates:

```text
Use this for basic time delays.
Do not create timer/wait nodes unless they need cancellation, unscaled time, repeated ticks, or another distinct timing model.
```

## Game Nodes

### `Game.Log`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.Log.node.json
```

Executor:

```text
ID: Game.Log
Class: GameLogExecutor
File: Assets/BlueprintSystem/Executors/Game/GameExecutors.cs
```

Function:

```text
Logs `message` through `context.Logger.Log`, then continues through `execOut`.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts log action. |
| `message` | input value | string | propertyOrConnection | yes | Text to log. |
| `message` | property | string | property | no | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | Continuation. |

Avoid duplicates:

```text
Use this for debug/runtime blueprint logging.
Only add specialized logging nodes if they route to a different service or severity.
```

### `Game.SendEvent`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.SendEvent.node.json
```

Executor:

```text
ID: Game.SendEvent
Class: GameSendEventExecutor
File: Assets/BlueprintSystem/Executors/Game/GameExecutors.cs
```

Function:

```text
Reads `eventName`, publishes it to `context.EventBus`, then continues through `execOut`.
Returns an error when `eventName` is empty.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts event publish. |
| `eventName` | input value | string | propertyOrConnection | yes | Event name to publish. |
| `eventName` | property | string | property | no | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | Continuation after publish. |

Avoid duplicates:

```text
Use this for intra-blueprint events.
For UI button clicks, prefer `UI.BindButtonClick`, which executes its `clicked` output directly.
```

### `Game.LoadScene`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.LoadScene.node.json
```

Executor:

```text
ID: Game.LoadScene
Class: GameLoadSceneExecutor
File: Assets/BlueprintSystem/Executors/Game/GameExecutors.cs
```

Function:

```text
Reads `sceneName` and `mode`, calls `SceneManager.LoadScene(sceneName, mode)`, then continues through `execOut`.
Returns an error when `sceneName` is empty or Unity rejects the load request.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts scene loading. |
| `sceneName` | input value | string | propertyOrConnection | yes | Name of a scene included in Build Settings. |
| `mode` | input value | LoadSceneMode | propertyOrConnection | no | `Single` or `Additive`; defaults to `Single`. |
| `sceneName` | property | string | property | yes | Used when no value edge is connected. |
| `mode` | property | LoadSceneMode | property | no | Graph Toolkit enum dropdown; exports `Single` or `Additive`. |
| `execOut` | output exec | none | none | no | Continuation after the load request is accepted. |

Avoid duplicates:

```text
Use this for direct scene changes by name.
Only add async scene nodes when the blueprint must wait for progress, completion, or activation control.
```

### `Game.LoadSceneAsync`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.LoadSceneAsync.node.json
```

Executor:

```text
ID: Game.LoadSceneAsync
Class: GameLoadSceneAsyncExecutor
File: Assets/BlueprintSystem/Executors/Game/GameExecutors.cs
```

Function:

```text
Reads `sceneName` and `mode`, calls `SceneManager.LoadSceneAsync(sceneName, mode)`, and stops immediate execution.
When Unity reports the async operation as completed, the node resumes blueprint execution through the `complete` output.
Returns an error when `sceneName` is empty, Unity rejects the load request, or no async operation is created.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts async scene loading. |
| `sceneName` | input value | string | propertyOrConnection | yes | Name of a scene included in Build Settings. |
| `mode` | input value | LoadSceneMode | propertyOrConnection | no | `Single` or `Additive`; defaults to `Single`. |
| `sceneName` | property | string | property | yes | Used when no value edge is connected. |
| `mode` | property | LoadSceneMode | property | no | Graph Toolkit enum dropdown; exports `Single` or `Additive`. |
| `complete` | output exec | none | none | no | Fires after the async load operation completes. |

Avoid duplicates:

```text
Use `Game.LoadScene` when the blueprint should continue immediately after requesting a scene load.
Use `Game.LoadSceneAsync` when downstream blueprint work must wait for Unity's async load completion.
For `Single` scene loads, keep the blueprint runner alive if the `complete` continuation needs to run after the old scene unloads.
```

### Transform Setters

Manifests:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.SetTransformPosition.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetTransformEulerAngles.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetTransformLocalScale.node.json
```

Executors:

```text
IDs: Game.SetTransformPosition, Game.SetTransformEulerAngles, Game.SetTransformLocalScale
Classes: GameSetTransformPositionExecutor, GameSetTransformEulerAnglesExecutor, GameSetTransformLocalScaleExecutor
File: Assets/BlueprintSystem/Executors/Game/GameTransformExecutors.cs
```

Function:

```text
Resolve `target` as Transform, including from a bound GameObject or Component.
Set world position, world eulerAngles, or localScale, then continue through `execOut`.
Return an error if the Transform binding cannot be resolved.
```

Ports and parameters:

| Node | Target | Value | Default |
| --- | --- | --- | --- |
| `Game.SetTransformPosition` | `target: Binding<Transform>` | `value: Vector3` | `[0, 0, 0]` |
| `Game.SetTransformEulerAngles` | `target: Binding<Transform>` | `value: Vector3` | `[0, 0, 0]` |
| `Game.SetTransformLocalScale` | `target: Binding<Transform>` | `value: Vector3` | `[1, 1, 1]` |

All three nodes have `execIn` and `execOut`. `target` is property-only; `value` is property-or-connection.

### 3D Physics Nodes

Manifests:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.SetRigidbodyLinearVelocity.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.AddRigidbodyForce.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetColliderEnabled.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetColliderIsTrigger.node.json
```

Executors:

```text
IDs: Game.SetRigidbodyLinearVelocity, Game.AddRigidbodyForce, Game.SetColliderEnabled, Game.SetColliderIsTrigger
Classes: GameSetRigidbodyLinearVelocityExecutor, GameAddRigidbodyForceExecutor, GameSetColliderEnabledExecutor, GameSetColliderIsTriggerExecutor
File: Assets/BlueprintSystem/Executors/Game/GamePhysicsExecutors.cs
```

Function:

```text
Resolve `target` as Rigidbody or Collider, including from a bound GameObject or Component.
Set `Rigidbody.linearVelocity`, call `Rigidbody.AddForce`, or set Collider flags.
Return an error if the binding cannot resolve or if force `mode` is invalid.
```

Ports and parameters:

| Node | Target | Value/Force | Extra | Default |
| --- | --- | --- | --- | --- |
| `Game.SetRigidbodyLinearVelocity` | `Binding<Rigidbody>` | `value: Vector3` | none | `[0, 0, 0]` |
| `Game.AddRigidbodyForce` | `Binding<Rigidbody>` | `force: Vector3` | `mode: ForceMode` | force `[0, 0, 0]`, mode `Force` |
| `Game.SetColliderEnabled` | `Binding<Collider>` | `value: bool` | none | `true` |
| `Game.SetColliderIsTrigger` | `Binding<Collider>` | `value: bool` | none | `true` |

`Game.AddRigidbodyForce.mode` is a Graph Toolkit enum field and exports as one of `Force`, `Acceleration`, `Impulse`, or `VelocityChange`.

### 2D Physics Nodes

Manifests:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.SetRigidbody2DLinearVelocity.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.AddRigidbody2DForce.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetCollider2DEnabled.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetCollider2DIsTrigger.node.json
```

Executors:

```text
IDs: Game.SetRigidbody2DLinearVelocity, Game.AddRigidbody2DForce, Game.SetCollider2DEnabled, Game.SetCollider2DIsTrigger
Classes: GameSetRigidbody2DLinearVelocityExecutor, GameAddRigidbody2DForceExecutor, GameSetCollider2DEnabledExecutor, GameSetCollider2DIsTriggerExecutor
File: Assets/BlueprintSystem/Executors/Game/GamePhysicsExecutors.cs
```

Function:

```text
Resolve `target` as Rigidbody2D or Collider2D, including from a bound GameObject or Component.
Set `Rigidbody2D.linearVelocity`, call `Rigidbody2D.AddForce`, or set Collider2D flags.
Return an error if the binding cannot resolve or if force `mode` is invalid.
```

Ports and parameters:

| Node | Target | Value/Force | Extra | Default |
| --- | --- | --- | --- | --- |
| `Game.SetRigidbody2DLinearVelocity` | `Binding<Rigidbody2D>` | `value: Vector2` | none | `[0, 0]` |
| `Game.AddRigidbody2DForce` | `Binding<Rigidbody2D>` | `force: Vector2` | `mode: ForceMode2D` | force `[0, 0]`, mode `Force` |
| `Game.SetCollider2DEnabled` | `Binding<Collider2D>` | `value: bool` | none | `true` |
| `Game.SetCollider2DIsTrigger` | `Binding<Collider2D>` | `value: bool` | none | `true` |

`Game.AddRigidbody2DForce.mode` is a Graph Toolkit enum field and exports as `Force` or `Impulse`.

### Rendering Material Nodes

Manifests:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.SetRendererMaterial.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetRendererMaterialColor.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetRendererTexture.node.json
```

Executors:

```text
IDs: Game.SetRendererMaterial, Game.SetRendererMaterialColor, Game.SetRendererTexture
Classes: GameSetRendererMaterialExecutor, GameSetRendererMaterialColorExecutor, GameSetRendererTextureExecutor
File: Assets/BlueprintSystem/Executors/Game/GameRenderingExecutors.cs
```

Function:

```text
Resolve `target` as Renderer, including from a bound GameObject or Component.
Use `renderer.material` / `renderer.materials` so runtime edits affect the instance material, not shared assets.
Return an error when bindings fail, the renderer has no material slot/material, the material index is out of range, or the shader property is missing.
```

Ports and parameters:

| Node | Target | Value | Extra | Default |
| --- | --- | --- | --- | --- |
| `Game.SetRendererMaterial` | `Binding<Renderer>` | `value: Binding<Material>` | `materialIndex: int` | `0` |
| `Game.SetRendererMaterialColor` | `Binding<Renderer>` | `value: Color` | `propertyName: string` | `_Color` |
| `Game.SetRendererTexture` | `Binding<Renderer>` | `value: Binding<Texture>` | `propertyName: string` | `_MainTex` |

All three nodes have `execIn` and `execOut`. `target` is property-only; value-like fields are property-or-connection where the manifest exposes them as inputs.

### Lighting Nodes

Manifests:

```text
Assets/BlueprintSystem/Specs/Nodes/Game.SetLightEnabled.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetLightIntensity.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetLightColor.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetLightColorTemperature.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetLightRange.node.json
Assets/BlueprintSystem/Specs/Nodes/Game.SetLightSpotAngle.node.json
```

Executors:

```text
IDs: Game.SetLightEnabled, Game.SetLightIntensity, Game.SetLightColor, Game.SetLightColorTemperature, Game.SetLightRange, Game.SetLightSpotAngle
Classes: GameSetLightEnabledExecutor, GameSetLightIntensityExecutor, GameSetLightColorExecutor, GameSetLightColorTemperatureExecutor, GameSetLightRangeExecutor, GameSetLightSpotAngleExecutor
File: Assets/BlueprintSystem/Executors/Game/GameRenderingExecutors.cs
```

Function:

```text
Resolve `target` as Light, including from a bound GameObject or Component.
Set the requested Light property directly with no extra clamping or type policy.
`Game.SetLightColorTemperature` also enables `Light.useColorTemperature` before setting the Kelvin value.
Unity color temperature additionally requires the project Graphics settings for color temperature and linear light intensity.
Return an error when the Light binding cannot be resolved.
```

Ports and parameters:

| Node | Target | Value | Default |
| --- | --- | --- | --- |
| `Game.SetLightEnabled` | `Binding<Light>` | `value: bool` | `true` |
| `Game.SetLightIntensity` | `Binding<Light>` | `value: float` | `1` |
| `Game.SetLightColor` | `Binding<Light>` | `value: Color` | `[1, 1, 1, 1]` |
| `Game.SetLightColorTemperature` | `Binding<Light>` | `value: float` (Kelvin) | `6500` |
| `Game.SetLightRange` | `Binding<Light>` | `value: float` | `10` |
| `Game.SetLightSpotAngle` | `Binding<Light>` | `value: float` | `30` |

All six nodes have `execIn` and `execOut`. `target` is property-only; `value` is property-or-connection.

## Input Nodes

### `Input.GetAxis`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Input.GetAxis.node.json
```

Executor:

```text
ID: Input.GetAxis
Class: InputGetAxisExecutor
File: Assets/BlueprintSystem/Executors/Input/InputExecutors.cs
```

Function:

```text
Reads `UnityEngine.Input.GetAxis(axisName)` and outputs the current smoothed float value. Use this with legacy Input Manager axes such as `Horizontal` or `Vertical`.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `axisName` | input value | string | propertyOrConnection | yes | Legacy Input Manager axis name. Default property value is `Horizontal`. |
| `axisName` | property | string | property | no | Stored in blueprint JSON; may be overridden by a connected value. |
| `value` | output value | float | none | no | Smoothed axis value returned by Unity. Missing or invalid axes log an error and return `0`. |

### `Input.GetAxisRaw`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Input.GetAxisRaw.node.json
```

Executor:

```text
ID: Input.GetAxisRaw
Class: InputGetAxisRawExecutor
File: Assets/BlueprintSystem/Executors/Input/InputExecutors.cs
```

Function:

```text
Reads `UnityEngine.Input.GetAxisRaw(axisName)` and outputs the current unsmoothed float value.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `axisName` | input value | string | propertyOrConnection | yes | Legacy Input Manager axis name. Default property value is `Horizontal`. |
| `axisName` | property | string | property | no | Stored in blueprint JSON; may be overridden by a connected value. |
| `value` | output value | float | none | no | Raw axis value returned by Unity. Missing or invalid axes log an error and return `0`. |

Avoid duplicates:

```text
Use `Input.GetAxis` when smoothed legacy axis values are desired.
Use `Input.GetAxisRaw` when immediate -1/0/1 style legacy axis values are desired.
Use `Input.ListenAction` for project-wide Input System actions that should follow modern action bindings.
Axis names are free-form strings and must match Project Settings > Input Manager entries when the legacy Input Manager is enabled.
```

### `Input.GetActionVector2`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Input.GetActionVector2.node.json
```

Executor:

```text
ID: Input.GetActionVector2
Class: InputGetActionVector2Executor
File: Assets/BlueprintSystem/Executors/Input/InputExecutors.cs
```

Function:

```text
Finds a project-wide Input System action by name or `Map/Action` path, enables it when needed, and outputs `action.ReadValue<Vector2>()`.
Use this from Tick-visible gameplay graphs for movement, look, navigation, or other continuous 2D input values.
Missing actions, missing project-wide actions, or non-Vector2 action values log an error and output `Vector2.zero`.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `action` | input value | string | property | yes | Project-wide Input System action name or `Map/Action` path. Default property value is `Player/Move`. |
| `action` | property | string | property | yes | Stored in blueprint JSON. |
| `value` | output value | Vector2 | none | no | Current Vector2 value returned by the action. |

Avoid duplicates:

```text
Use this when the graph needs the actual 2D value from a modern Input System action.
Use `Input.ListenAction` when only pressed, held, and released exec outputs are needed.
Do not store InputActionAsset object references in blueprint JSON.
```

### `Input.ListenKey`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Input.ListenKey.node.json
```

Executor:

```text
ID: Input.ListenKey
Class: InputListenKeyExecutor
File: Assets/BlueprintSystem/Executors/Input/InputExecutors.cs
```

Function:

```text
Polls a keyboard key immediately when the node is executed.
Wire this from `Game.Event.OnTick` when you need per-frame input handling. The node always emits `bound` so multiple input polling nodes can be chained in one Tick flow. It also emits `pressed` on the transition into pressed, `held` on later pressed polls, and `released` on the transition out of pressed.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Polls the key state. Usually driven by `Game.Event.OnTick`. |
| `key` | input value | Key | property | yes | Unity Input System `Key` enum value such as `Space`, `Escape`, `W`, or `LeftShift`. |
| `key` | property | Key | property | yes | Stored in blueprint JSON as an exact enum member name. |
| `bound` | output exec | none | none | no | Continuation after every poll; chain the next input polling node here. |
| `pressed` | output exec | none | none | no | Fired once when the key becomes pressed. |
| `held` | output exec | none | none | no | Fired on later polls while the key remains pressed. |
| `released` | output exec | none | none | no | Fired once when the key is released. |

Avoid duplicates:

```text
Use this for direct keyboard keys.
Hand-written JSON must use exact `Key` enum member names; aliases such as `w` or `esc` are invalid.
Use `Input.ListenAction` when the behavior should follow Input System action bindings.
Do not wire this only from `Game.Event.OnStart`; that polls once and will miss later input. Chain multiple input polling nodes from a single Tick using `bound`.
```

### `Input.ListenAction`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Input.ListenAction.node.json
```

Executor:

```text
ID: Input.ListenAction
Class: InputListenActionExecutor
File: Assets/BlueprintSystem/Executors/Input/InputExecutors.cs
```

Function:

```text
Finds an action in `InputSystem.actions`, enables it when needed, and polls it immediately when the node is executed.
Wire this from `Game.Event.OnTick` when you need per-frame input handling. The node always emits `bound` so multiple input polling nodes can be chained in one Tick flow.
Action names may be simple unique names like `Jump` or paths like `Player/Jump`.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Polls the action state. Usually driven by `Game.Event.OnTick`. |
| `action` | input value | string | property | yes | Project-wide Input System action name or `Map/Action` path. |
| `action` | property | string | property | yes | Stored in blueprint JSON. |
| `bound` | output exec | none | none | no | Continuation after every poll; chain the next input polling node here. |
| `pressed` | output exec | none | none | no | Fired once when the action becomes pressed. |
| `held` | output exec | none | none | no | Fired on later polls while the action remains pressed. |
| `released` | output exec | none | none | no | Fired once when the action is released. |

Avoid duplicates:

```text
Use this for gameplay/UI actions that should respect Input System bindings.
Do not store InputActionAsset object references in blueprint JSON.
Do not wire this only from `Game.Event.OnStart`; that polls once and will miss later input. Chain multiple input polling nodes from a single Tick using `bound`.
```

## UI Nodes

### `UI.SetText`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetText.node.json
```

Executor:

```text
ID: UI.SetText
Class: UISetTextExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `TMP_Text`.
Sets `target.text = value`.
Continues through `execOut`.
Returns an error if the binding cannot resolve a TMP_Text.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts set text action. |
| `target` | input value | Binding<TMP_Text> | property | yes | Binding name string. Must resolve to TMP_Text or owner with TMP_Text. |
| `value` | input value | string | propertyOrConnection | yes | New text. |
| `target` | property | Binding<TMP_Text> | property | yes | Stored as binding name string in JSON. |
| `value` | property | string | property | no | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | Continuation. |

Avoid duplicates:

```text
Use this for all TMP text assignment.
Do not add `SetLabel`, `SetTitle`, or `SetTextMeshProText`; use different binding names instead.
```

### `UI.BindText`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.BindText.node.json
```

Executor:

```text
ID: UI.BindText
Class: UIBindTextExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `TMP_Text`.
Registers a reactive binding keyed by instance, node id, target binding name, and `text`.
If `variableName` is set, reads that variable from the current context or from `variableTarget` using the same asset-path/BlueprintRef target rules as `Blueprint.GetVariable`.
If `variableName` is empty, evaluates fallback `value`.
Applies the binding immediately and writes `target.text`.
On reactive refresh, clears the context value cache, rereads the variable or fallback value, and writes `target.text` again.
Continues through `bound`.
Returns an error if the binding cannot resolve a TMP_Text during registration.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Registers or updates the text binding. |
| `target` | input value | Binding<TMP_Text> | property | yes | Binding name string. Must resolve to TMP_Text or owner with TMP_Text. |
| `variableName` | input value | string | propertyOrConnection | no | Variable to read. Empty means use `value` instead. |
| `variableTarget` | input value | Blueprint | propertyOrConnection | no | Optional target Blueprint asset path or connected BlueprintRef. Empty means the current blueprint context. |
| `value` | input value | string | propertyOrConnection | no | Fallback expression reevaluated when no `variableName` is set. |
| `target` | property | Binding<TMP_Text> | property | yes | Stored as binding name string in JSON. |
| `variableName` | property | string | property | no | Stored variable name. |
| `variableTarget` | property | Blueprint | property | no | Stored target `.blueprint.json` asset path; Graph Toolkit shows long paths in the Inspector. |
| `value` | property | string | property | no | Fallback used when no value edge is connected and no `variableName` is set. |
| `bound` | output exec | none | none | no | Continuation after the binding is registered and applied once. |

Reactive refresh behavior:

```text
`Variable.Set` refreshes reactive bindings for the current context or current Blueprint instance.
`Blueprint.SetVariable` refreshes reactive bindings that depend on the target Blueprint instance, including bindings registered on another runner through `variableTarget`.
`BlueprintRunner.ReloadBlueprint` and `BlueprintRuntimeComponent.ReloadBlueprint` capture active reactive binding nodes before invalidating the old runtime state, restore those bindings on the new runtime state, and refresh them when `BlueprintReloadOptions.RefreshReactiveBindings` is true.
`UIBlueprintBinder.OnDisable` fires `OnClose` and then clears reactive bindings for the panel runner and its runtime components.
```

Recommended UI usage:

```text
OnOpen -> UI.BindText
```

For larger UI graphs, route `OnOpen` into a shared custom registration event that executes every `UI.Bind*` node. Do not add explicit `OnReload` nodes for editor hot reload; active reactive bindings are restored by C# reload infrastructure.

Avoid duplicates:

```text
Use `UI.BindText` for live TMP text display that must survive variable changes and hot reload.
Use `UI.SetText` for one-shot text assignment.
Use `variableName`/`variableTarget` before wiring a separate `Blueprint.GetVariable` into `value` when cross-blueprint variable changes should refresh this binding automatically.
Do not store TMP_Text object references in blueprint JSON; store Binding<TMP_Text> binding names.
```

### `UI.SpriteBinding`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SpriteBinding.node.json
```

Executor:

```text
ID: UI.SpriteBinding
Class: UISpriteBindingExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Value node.
Reads `sprite` as a Binding<Sprite> string.
Outputs the binding name through `value` so it can connect to `UI.SetImageSprite.value`.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `sprite` | property | Binding<Sprite> | property | yes | Stored as binding name string in JSON. |
| `value` | output value | Binding<Sprite> | none | no | Binding name for a Sprite asset. |

Avoid duplicates:

```text
Use this when dragging a Sprite asset into a graph or when a Sprite binding needs to feed another node.
Do not turn Sprite assets into normal variables unless the variable system is deliberately extended for asset references.
```

### `UI.SetImageSprite`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetImageSprite.node.json
```

Executor:

```text
ID: UI.SetImageSprite
Class: UISetImageSpriteExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `Image`.
Resolves `value` as `Sprite`.
Sets `target.sprite = value`.
Continues through `execOut`.
Returns an error if either binding cannot be resolved.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts set image sprite action. |
| `target` | input value | Binding<Image> | property | yes | Binding name string. Must resolve to Image or owner with Image. |
| `value` | input value | Binding<Sprite> | propertyOrConnection | yes | Binding name string for the Sprite asset. |
| `target` | property | Binding<Image> | property | yes | Stored as binding name string in JSON. |
| `value` | property | Binding<Sprite> | property | no | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | Continuation. |

Avoid duplicates:

```text
Use this for Unity UI Image sprite assignment.
Do not use it for SpriteRenderer; add a separate node if non-UI sprite rendering is needed.
```

### `UI.SetVisible`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetVisible.node.json
```

Executor:

```text
ID: UI.SetVisible
Class: UISetVisibleExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as a Unity object.
If it is a GameObject, uses it directly.
If it is a Component, uses `component.gameObject`.
Calls `gameObject.SetActive(value)`.
Continues through `execOut`.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts visibility action. |
| `target` | input value | Binding<GameObject> | property | yes | Binding name string. Can bind GameObject or Component. |
| `value` | input value | bool | propertyOrConnection | yes | `true` | Active state. |
| `target` | property | Binding<GameObject> | property | yes | Stored as binding name string in JSON. |
| `value` | property | bool | property | no | `true` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

Avoid duplicates:

```text
Use this for showing/hiding UI objects through active state.
Only add a separate node if visibility means CanvasGroup alpha, Graphic.enabled, or another distinct mechanism.
```

### `UI.SetGraphicColor`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetGraphicColor.node.json
```

Executor:

```text
ID: UI.SetGraphicColor
Class: UISetGraphicColorExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `Graphic`.
Sets `graphic.color = value`.
Continues through `execOut`.
Returns an error if the binding cannot resolve a Graphic.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts graphic color action. |
| `target` | input value | Binding<Graphic> | property | yes | none | Binding name string. Image and TMP text are valid Graphics. |
| `value` | input value | Color | propertyOrConnection | yes | `[1,1,1,1]` | New color. |
| `target` | property | Binding<Graphic> | property | yes | none | Stored as binding name string in JSON. |
| `value` | property | Color | property | no | `[1,1,1,1]` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

### `UI.SetGraphicEnabled`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetGraphicEnabled.node.json
```

Executor:

```text
ID: UI.SetGraphicEnabled
Class: UISetGraphicEnabledExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `Graphic`.
Sets `graphic.enabled = value`.
Continues through `execOut`.
Returns an error if the binding cannot resolve a Graphic.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts graphic enabled action. |
| `target` | input value | Binding<Graphic> | property | yes | none | Binding name string. |
| `value` | input value | bool | propertyOrConnection | yes | `true` | Enabled state. |
| `target` | property | Binding<Graphic> | property | yes | none | Stored as binding name string in JSON. |
| `value` | property | bool | property | no | `true` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

### `UI.SetGraphicRaycastTarget`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetGraphicRaycastTarget.node.json
```

Executor:

```text
ID: UI.SetGraphicRaycastTarget
Class: UISetGraphicRaycastTargetExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `Graphic`.
Sets `graphic.raycastTarget = value`.
Continues through `execOut`.
Returns an error if the binding cannot resolve a Graphic.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts graphic raycast target action. |
| `target` | input value | Binding<Graphic> | property | yes | none | Binding name string. |
| `value` | input value | bool | propertyOrConnection | yes | `true` | Raycast target state. |
| `target` | property | Binding<Graphic> | property | yes | none | Stored as binding name string in JSON. |
| `value` | property | bool | property | no | `true` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

### `UI.SetImageFillAmount`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetImageFillAmount.node.json
```

Executor:

```text
ID: UI.SetImageFillAmount
Class: UISetImageFillAmountExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `Image`.
Sets `image.fillAmount = Mathf.Clamp01(value)`.
Continues through `execOut`.
Returns an error if the binding cannot resolve an Image.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts image fill amount action. |
| `target` | input value | Binding<Image> | property | yes | none | Binding name string. |
| `value` | input value | float | propertyOrConnection | yes | `1` | Fill amount, clamped to 0..1. |
| `target` | property | Binding<Image> | property | yes | none | Stored as binding name string in JSON. |
| `value` | property | float | property | no | `1` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

### `UI.SetCanvasGroupAlpha`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetCanvasGroupAlpha.node.json
```

Executor:

```text
ID: UI.SetCanvasGroupAlpha
Class: UISetCanvasGroupAlphaExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `CanvasGroup`.
Sets `canvasGroup.alpha = Mathf.Clamp01(value)`.
Continues through `execOut`.
Returns an error if the binding cannot resolve a CanvasGroup.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts canvas group alpha action. |
| `target` | input value | Binding<CanvasGroup> | property | yes | none | Binding name string. |
| `value` | input value | float | propertyOrConnection | yes | `1` | Alpha, clamped to 0..1. |
| `target` | property | Binding<CanvasGroup> | property | yes | none | Stored as binding name string in JSON. |
| `value` | property | float | property | no | `1` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

### `UI.SetCanvasGroupInteractable`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetCanvasGroupInteractable.node.json
```

Executor:

```text
ID: UI.SetCanvasGroupInteractable
Class: UISetCanvasGroupInteractableExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `CanvasGroup`.
Sets `canvasGroup.interactable = value`.
Continues through `execOut`.
Returns an error if the binding cannot resolve a CanvasGroup.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts canvas group interactable action. |
| `target` | input value | Binding<CanvasGroup> | property | yes | none | Binding name string. |
| `value` | input value | bool | propertyOrConnection | yes | `true` | Interactable state. |
| `target` | property | Binding<CanvasGroup> | property | yes | none | Stored as binding name string in JSON. |
| `value` | property | bool | property | no | `true` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

### `UI.SetCanvasGroupBlocksRaycasts`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetCanvasGroupBlocksRaycasts.node.json
```

Executor:

```text
ID: UI.SetCanvasGroupBlocksRaycasts
Class: UISetCanvasGroupBlocksRaycastsExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `CanvasGroup`.
Sets `canvasGroup.blocksRaycasts = value`.
Continues through `execOut`.
Returns an error if the binding cannot resolve a CanvasGroup.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts canvas group blocks raycasts action. |
| `target` | input value | Binding<CanvasGroup> | property | yes | none | Binding name string. |
| `value` | input value | bool | propertyOrConnection | yes | `true` | Blocks raycasts state. |
| `target` | property | Binding<CanvasGroup> | property | yes | none | Stored as binding name string in JSON. |
| `value` | property | bool | property | no | `true` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

### `UI.SetRectAnchoredPosition`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetRectAnchoredPosition.node.json
```

Executor:

```text
ID: UI.SetRectAnchoredPosition
Class: UISetRectAnchoredPositionExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `RectTransform`.
Sets `rectTransform.anchoredPosition = value`.
Continues through `execOut`.
Returns an error if the binding cannot resolve a RectTransform.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts rect anchored position action. |
| `target` | input value | Binding<RectTransform> | property | yes | none | Binding name string. |
| `value` | input value | Vector2 | propertyOrConnection | yes | `[0,0]` | New anchored position. |
| `target` | property | Binding<RectTransform> | property | yes | none | Stored as binding name string in JSON. |
| `value` | property | Vector2 | property | no | `[0,0]` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

### `UI.SetRectSizeDelta`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetRectSizeDelta.node.json
```

Executor:

```text
ID: UI.SetRectSizeDelta
Class: UISetRectSizeDeltaExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `RectTransform`.
Sets `rectTransform.sizeDelta = value`.
Continues through `execOut`.
Returns an error if the binding cannot resolve a RectTransform.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts rect size delta action. |
| `target` | input value | Binding<RectTransform> | property | yes | none | Binding name string. |
| `value` | input value | Vector2 | propertyOrConnection | yes | `[0,0]` | New size delta. |
| `target` | property | Binding<RectTransform> | property | yes | none | Stored as binding name string in JSON. |
| `value` | property | Vector2 | property | no | `[0,0]` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

### `UI.SetRectLocalScale`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetRectLocalScale.node.json
```

Executor:

```text
ID: UI.SetRectLocalScale
Class: UISetRectLocalScaleExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `RectTransform`.
Sets `rectTransform.localScale = value`.
Continues through `execOut`.
Returns an error if the binding cannot resolve a RectTransform.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts rect local scale action. |
| `target` | input value | Binding<RectTransform> | property | yes | none | Binding name string. |
| `value` | input value | Vector3 | propertyOrConnection | yes | `[1,1,1]` | New local scale. |
| `target` | property | Binding<RectTransform> | property | yes | none | Stored as binding name string in JSON. |
| `value` | property | Vector3 | property | no | `[1,1,1]` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

### `UI.SetInteractable`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.SetInteractable.node.json
```

Executor:

```text
ID: UI.SetInteractable
Class: UISetInteractableExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `Selectable`.
Sets `selectable.interactable = value`.
Continues through `execOut`.
Returns an error if the binding cannot resolve a Selectable.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Starts interactable action. |
| `target` | input value | Binding<Selectable> | property | yes | Binding name string. Button is valid because Button derives from Selectable. |
| `value` | input value | bool | propertyOrConnection | yes | `true` | Interactable state. |
| `target` | property | Binding<Selectable> | property | yes | Stored as binding name string in JSON. |
| `value` | property | bool | property | no | `true` | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | none | Continuation. |

Avoid duplicates:

```text
Use this for Button, Toggle, Slider, Dropdown, InputField, and other Selectable-derived UI.
Do not add Button-specific interactable nodes unless behavior must differ by component type.
```

### `UI.BindButtonClick`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.BindButtonClick.node.json
```

Executor:

```text
ID: UI.BindButtonClick
Class: UIBindButtonClickExecutor
File: Assets/BlueprintSystem/Executors/UI/UIExecutors.cs
```

Function:

```text
Resolves `target` as `Button`.
Adds a listener to `button.onClick` that executes nodes connected to this node's `clicked` output.
The listener is added only once per node/target in the current execution context.
Continues through `bound` after the listener is installed.
Returns an error when target cannot resolve a Button.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Run this once before clicks should be handled. |
| `target` | input value | Binding<Button> | property | yes | Binding name string. |
| `target` | property | Binding<Button> | property | yes | Stored as binding name string in JSON. |
| `bound` | output exec | none | none | no | Continuation after binding is installed. |
| `clicked` | output exec | none | none | no | Executed each time the Button is clicked. |

Avoid duplicates:

```text
Use this for all Unity UI Button click wiring inside the current blueprint.
Do not create `OnButtonClick` event nodes for each button; connect from `clicked` to the desired behavior.
```

Legacy migration:

```text
Older graphs used `eventName` plus `Game.Event.Custom`.
Compiler and Graph Toolkit import/export migrate old `eventName` links to `clicked` when a matching custom event entry exists.
Older `execOut` edges from this node are migrated to `bound`.
```

### `UI.BindButtonEvents`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.BindButtonEvents.node.json
```

Executor:

```text
ID: UI.BindButtonEvents
Class: UIBindButtonEventsExecutor
File: Assets/BlueprintSystem/Executors/UI/BlueprintUIComponentExecutors.cs
```

Function:

```text
Resolves `target` as `Button`.
Adds a `BlueprintButtonGestureListener` component to the Button GameObject when missing.
Executes `clicked`, `doubleClicked`, or `longPressed` as mutually exclusive gestures.
Uses unscaled time. Defaults: `longPressSeconds` 0.5, `doubleClickSeconds` 0.3.
Continues through `bound` after the listener is installed.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | none | Run once before gestures should be handled. |
| `target` | input value | Binding<Button> | property | yes | none | Binding name string. |
| `longPressSeconds` | input value | float | propertyOrConnection | no | `0.5` | Long-press threshold. |
| `doubleClickSeconds` | input value | float | propertyOrConnection | no | `0.3` | Double-click window. |
| `target` | property | Binding<Button> | property | yes | none | Stored as binding name string. |
| `bound` | output exec | none | none | no | none | Continuation after binding is installed. |
| `clicked` | output exec | none | none | no | none | Single click after the double-click window expires. |
| `doubleClicked` | output exec | none | none | no | none | Second click inside the double-click window. |
| `longPressed` | output exec | none | none | no | none | Pointer held past the long-press threshold. |

### `UI.BindToggleChanged`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.BindToggleChanged.node.json
```

Executor:

```text
ID: UI.BindToggleChanged
Class: UIBindToggleChangedExecutor
File: Assets/BlueprintSystem/Executors/UI/BlueprintUIComponentExecutors.cs
```

Function:

```text
Resolves `target` as `Toggle`.
Adds a `BlueprintToggleListener` component to the Toggle GameObject when missing.
Executes `changed` for every value change, plus `turnedOn` or `turnedOff`.
Value output `value` returns current `Toggle.isOn`.
Continues through `bound` after the listener is installed.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Run once before changes should be handled. |
| `target` | input value | Binding<Toggle> | property | yes | Binding name string. |
| `target` | property | Binding<Toggle> | property | yes | Stored as binding name string. |
| `bound` | output exec | none | none | no | Continuation after binding is installed. |
| `changed` | output exec | none | none | no | Executed on every Toggle value change. |
| `turnedOn` | output exec | none | none | no | Executed when `isOn` becomes true. |
| `turnedOff` | output exec | none | none | no | Executed when `isOn` becomes false. |
| `value` | output value | bool | none | no | Current Toggle state. |

### `UI.RefreshLoopScrollView`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/UI.RefreshLoopScrollView.node.json
```

Executor:

```text
ID: UI.RefreshLoopScrollView
Class: UIRefreshLoopScrollViewExecutor
File: Assets/BlueprintSystem/Executors/UI/BlueprintUIComponentExecutors.cs
```

Function:

```text
Resolves `target` as `BlueprintLoopScrollView`.
Refreshes from connected `items` when present; otherwise reads array variable named by `itemsVariable`.
The runtime component lives under `Assets/BlueprintSystem/UI/Components`.
Each visible item prefab may include a child `BlueprintRunner`/`UIBlueprintBinder`.
During refresh the row runner receives variables `item`, `index`, and `count`, then event `OnBindItem`.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts refresh. |
| `target` | input value | Binding<BlueprintLoopScrollView> | property | yes | Binding name string. |
| `items` | input value | untyped array | connection | no | Preferred data source when connected. |
| `itemsVariable` | input value | string | propertyOrConnection | no | Fallback variable name. |
| `target` | property | Binding<BlueprintLoopScrollView> | property | yes | Stored as binding name string. |
| `execOut` | output exec | none | none | no | Continuation after refresh. |

## Variable Nodes

### `Variable.Get`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Variable.Get.node.json
```

Executor:

```text
ID: Variable.Get
Class: VariableGetExecutor
File: Assets/BlueprintSystem/Executors/Variables/VariableExecutors.cs
```

Function:

```text
Value node.
Reads variable `name` from `context.Variables`.
Returns the value through output `value`.
Logs an error and returns null if `name` is empty.
Validator reports an error if `name` is missing or not declared in `variables[]`.
Graph Toolkit imports display as native Blackboard variable nodes when the variable type is supported.
Dragging a Blackboard variable into the graph offers `Get <variableName>` and `Set <variableName>` choices.
Native Graph Toolkit Blackboard variable nodes are exported as `Variable.Get` nodes.
Valid imported `Variable.Get` JSON nodes are restored as native Blackboard variable nodes.
`Array<T>` variables appear in the Blackboard as a single `Array` type. Select the element type inside the array field and edit the default value as JSON text; dragged nodes and exported JSON retain the original `Array<T>` type.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `name` | property | string | property | yes | Variable name. |
| `value` | output value | untyped | none | no | Current variable value. |

Avoid duplicates:

```text
Use this for all variable reads.
Do not add type-specific get nodes unless the type system needs stronger editor/runtime enforcement.
```

### `Variable.Set`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Variable.Set.node.json
```

Executor:

```text
ID: Variable.Set
Class: VariableSetExecutor
File: Assets/BlueprintSystem/Executors/Variables/VariableExecutors.cs
```

Function:

```text
Reads `name` and `value`.
Writes value into `context.Variables`.
Refreshes reactive bindings for the current context or current Blueprint instance after a successful write.
Continues through `execOut`.
Returns an error if `name` is empty.
Validator reports an error if `name` is missing, not declared in `variables[]`, or the literal `value` is not assignable.
Graph Toolkit imports and Blackboard drag-created nodes display as `Set <variableName>`.
The `value` input is displayed as `New Value` and typed from the Blackboard variable declaration.
Dragging from the Blackboard initializes `value` from the variable default value, or from the type default if none exists.
This writes to `context.Variables` at runtime and does not edit the Blackboard default value.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts variable write. |
| `name` | property | string | property | yes | Target variable name, shown as `Variable` in the editor. |
| `value` | input value | untyped | propertyOrConnection | yes | Value to write, shown as `New Value` in the editor. |
| `value` | property | untyped | property | no | Used when no value edge is connected. |
| `execOut` | output exec | none | none | no | Continuation. |

Avoid duplicates:

```text
Use this for all variable writes.
Do not add `SetString`, `SetBool`, etc. unless typed variables become explicitly enforced in manifests and UI.
```

### `Variable.Compare`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Variable.Compare.node.json
```

Executor:

```text
ID: Variable.Compare
Class: VariableCompareExecutor
File: Assets/BlueprintSystem/Executors/Variables/VariableExecutors.cs
```

Function:

```text
Value node.
Reads `left`, `right`, and `comparison`.
Returns bool through output `result`.
`Equals` and `NotEquals` normalize all CLR numeric values (`byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, and `decimal`) to double before comparing; nonnumeric values keep normal object equality.
Ordered numeric comparisons convert operands to double; failed conversion becomes 0.
```

Supported `comparison` values:

```text
Equals
NotEquals
Greater
GreaterOrEqual
Less
LessOrEqual
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `left` | input value | untyped | propertyOrConnection | yes | none | Left operand. |
| `right` | input value | untyped | propertyOrConnection | yes | none | Right operand. |
| `comparison` | input value | ComparisonMode | propertyOrConnection | no | `Equals` | Operator enum; can be set by the node dropdown or connected from an enum variable. |
| `comparison` | property | ComparisonMode | property | no | `Equals` | Default operator enum; Graph Toolkit displays it as a dropdown when no value edge is connected. |
| `left` | property | untyped | property | no | none | Used when no value edge is connected. |
| `right` | property | untyped | property | no | none | Used when no value edge is connected. |
| `result` | output value | bool | none | no | none | Comparison result. |

Avoid duplicates:

```text
Use this for simple equality and numeric comparisons.
Only add dedicated comparison/math nodes when they improve type safety, editor UX, or support operations not expressible here.
```

### `Variable.GetField`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Variable.GetField.node.json
```

Executor:

```text
ID: Variable.GetField
Class: VariableGetFieldExecutor
File: Assets/BlueprintSystem/Executors/Variables/BlueprintArrayExecutors.cs
```

Function:

```text
Reads `target` and a dot-separated `path`.
Supports Blueprint user structs, dictionaries, serializable fields/properties, list indexes, and Vector2/Vector3 x/y/z fields.
Returns the resolved value through output `value`; logs an error when the path cannot be read.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `target` | input value | untyped | connection | yes | Structured object, dictionary, or item from `Array.Get`. |
| `path` | input value | string | propertyOrConnection | yes | Field path such as `count` or `stats.attack`. |
| `path` | property | string | property | yes | Stored path when no value edge is connected. |
| `value` | output value | untyped | none | no | Resolved field value. |

### `Variable.SetField`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Variable.SetField.node.json
```

Executor:

```text
ID: Variable.SetField
Class: VariableSetFieldExecutor
File: Assets/BlueprintSystem/Executors/Variables/BlueprintArrayExecutors.cs
```

Function:

```text
Reads `target`, a dot-separated `path`, and a new `value`.
Returns a changed copy through output `result`; connect that output to `Variable.Set.value` when the changed struct should be stored.
Supports Blueprint user structs, dictionaries, list indexes, Vector2/Vector3 x/y/z fields, and serializable fields/properties.
Logs an error and returns the original target when the path cannot be written or the value does not fit a user struct field type.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `target` | input value | untyped | connection | yes | Structured object, dictionary, array item, or vector value. |
| `path` | input value | string | propertyOrConnection | yes | Field path such as `count`, `stats.attack`, or `items.0.count`. |
| `value` | input value | untyped | propertyOrConnection | yes | New field value. User struct fields are coerced through the struct definition. |
| `path` | property | string | property | yes | Stored path when no value edge is connected. |
| `value` | property | untyped | property | no | Stored new value when no value edge is connected. |
| `result` | output value | untyped | none | no | Changed copy of the target value. |

### `Variable.BreakStruct`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Variable.BreakStruct.node.json
```

Executor:

```text
ID: Variable.BreakStruct
Class: VariableBreakStructExecutor
File: Assets/BlueprintSystem/Executors/Variables/BlueprintArrayExecutors.cs
```

Function:

```text
Reads `target` as the configured Blueprint user struct type and returns a field value from the requested output port.
Output port IDs are stable hidden field IDs, while port labels use the current field names from the struct definition.
Logs an error and returns null when the target is missing, has the wrong struct type, or the field output no longer exists.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `target` | input value | `Struct.{FileName}` at runtime; untyped/object in Graph Toolkit | connection | yes | Struct value to split. The editor resolves this from `structAssetGuid` first, then `structTypeId`. |
| `field.id` | output value | `field.type` | none | no | One dynamic output per non-deprecated struct field. Display name is `field.name`; connection identity is `field.id`. |
| `structTypeId` | hidden property | string | property | yes | Runtime struct type id such as `Struct.InventoryItem`. |
| `structAssetGuid` | hidden property | string | property | no | Editor-only asset tracking so renamed `BlueprintUserStructAsset` files refresh to the latest `Struct.{FileName}`. |

Graph Toolkit behavior:

```text
Dragging a BlueprintUserStructAsset into the canvas offers `Variables/Break Struct.{FileName}`.
The created node does not create a variable; connect an existing struct variable or struct output into `target`.
An unconnected Break Struct node can remain on the canvas as a preview; `target` becomes a compile-time requirement once any field output is connected.
The `target` input is connection-only in Graph Toolkit, hides the embedded struct `Type`/`JSON` constant editor, and stays untyped/object so generic item outputs such as `Array.ForEachLoop.arrayElement` can connect to it.
When the visual node is built, it creates dynamic outputs from the current struct fields and hides the tracking properties from manual editing.
```

## Blueprint User Struct Values

Preferred authoring uses a ScriptableObject asset:

```text
Create > Blueprint System > User Struct Definition
```

For game/project data, save these assets under the feature that owns them:

```text
Assets/Game/Blueprint/<FeatureName>/.../*.asset
```

Package-provided structs may live under the BlueprintSystem package root, such as `Packages/com.shadedclark.blueprint-system/Specs/Structs` or the embedded-package fallback `Assets/BlueprintSystem/Specs/Structs`. `Assets/BlueprintSystem/Specs/Structs` remains a legacy/default authoring location, but it is not the only registry root.

The editor registry scans `BlueprintUserStructAsset` assets directly from the BlueprintSystem package roots and project `Assets/**`, so blueprints can use the type immediately after the asset is saved or after clicking `Refresh Registry` in the inspector. The asset inspector includes `Sync JSON`, which writes a generated schema next to the asset for portable/runtime-readable definitions:

```text
Assets/Game/Blueprint/<FeatureName>/.../*.bpstruct.json
```

Generated JSON shape:

```json
{
  "schemaVersion": "0.1",
  "typeId": "Struct.InventoryItem",
  "fields": [
    { "id": "fld_item_id", "name": "itemId", "type": "string", "defaultValue": "" },
    { "id": "fld_count", "name": "count", "type": "int", "defaultValue": 1 }
  ]
}
```

In the ScriptableObject inspector, `schemaVersion` and field `id` are hidden internal fields. `id` is auto-generated as the stable field identity and should not be hand-authored. `typeId` is read-only and derived from the asset file name as `Struct.{FileName}`; renaming the asset updates the type id. There is no separate display name; editor UI and generated definitions use `typeId` whenever a struct label is needed. In the field list, `name` is the user-facing field path used by `Variable.GetField` and `Variable.SetField`; `type` is selected from the field type enum. `defaultValueJson` stores the default as JSON, such as `1`, `"Sword"`, `[0, 0]`, or `{ "nested": true }`.

Blueprint variables can use `Struct.InventoryItem` or `Array<Struct.InventoryItem>` as their type. Defaults are stored in blueprint JSON as normal field objects, then coerced into runtime `BlueprintStructValue` instances by the variable store. Runtime values keep field IDs internally; `Variable.GetField` and `Variable.SetField` accept field names or field IDs in paths, while `Variable.BreakStruct` keeps connections stable by using field IDs as output port IDs.

The editor refreshes the user-struct registry when `BlueprintUserStructAsset` or generated `.bpstruct.json` definitions are imported, moved, or deleted. `.blueprint.json` remains the behavior source of truth; registry refreshes do not rewrite blueprint behavior.

ScriptableObject-authored user struct fields support the enum choices `String`, `Bool`, `Int`, `Float`, `Vector2`, `Vector3`, `Vector4`, `Color`, `Rect`, `ForceMode`, `ForceMode2D`, `LoadSceneMode`, `Key`, `ComparisonMode`, `TickPhase`, and `Blueprint`. `BlueprintRef` and `Binding<T>` are intentionally not supported as user struct fields because they are runtime/editor binding handles, not portable data.

## Blueprint Data Tables

Preferred authoring uses a ScriptableObject asset:

```text
Create > Blueprint System > Data Table
```

For game/project data, save these assets under the feature that owns them:

```text
Assets/Game/Blueprint/<FeatureName>/.../*.asset
```

Package-provided tables may live under the BlueprintSystem package root, such as `Packages/com.shadedclark.blueprint-system/Specs/Tables` or the embedded-package fallback `Assets/BlueprintSystem/Specs/Tables`. `Assets/BlueprintSystem/Specs/Tables` remains a legacy/default authoring location, but it is not the only registry root.

Each table selects one Blueprint user struct row type. Rows use a unique string `rowName`; row values are JSON objects matching the selected struct. The editor registry scans `BlueprintDataTableAsset` and `.bpdatatable.json` definitions from the BlueprintSystem package roots and project `Assets/**`. The inspector includes `Sync JSON`, which writes the portable/runtime-readable table next to the asset:

```text
Assets/Game/Blueprint/<FeatureName>/.../*.bpdatatable.json
```

Generated JSON shape:

```json
{
  "schemaVersion": "0.1",
  "tableId": "Table.ItemTable",
  "rowStructTypeId": "Struct.ItemRow",
  "rows": [
    { "rowName": "sword_01", "value": { "itemId": "sword_01", "count": 1 } }
  ]
}
```

`tableId` is read-only and derived from the asset file name as `Table.{FileName}`. New Blueprint node JSON stores the generated `.bpdatatable.json` path in `dataTable`; `tablePath` remains the legacy fallback. Graph Toolkit also stores a hidden `tableAssetGuid` so dragged table nodes can refresh to the current asset after editor moves or renames. Runtime row values are normalized through the selected user struct definition and returned as `BlueprintStructValue`.

Double-clicking a `BlueprintDataTableAsset` or `.bpdatatable.json` opens the Data Table editor. The editor shows `rowName` as the first column and the selected row struct fields as editable columns. Double-click a cell to edit it, then use `Save` to write the source asset and sync its `.bpdatatable.json` copy, or to write a JSON-only table directly when no source asset exists. Cell edits are recorded with Unity Undo/Redo. If the row struct type is missing or invalid, the editor falls back to a `Value JSON` column so the raw row values remain visible.

### DataTable Variables

Blueprint variables support strong typed table references using `DataTable<Struct.RowType>`. The runtime value is the normalized `.bpdatatable.json` path, not a Unity object or in-memory table definition:

```json
{
  "name": "itemTable",
  "type": "DataTable<Struct.ItemRow>",
  "defaultValue": "Assets/Game/Data/ItemTable.bpdatatable.json",
  "scope": "runtime"
}
```

The referenced table must exist and use the same `rowStructTypeId` as the generic type. Bare `DataTable`, unknown row structs, nested DataTables, and `Array<DataTable<...>>` are invalid. `Variable.Set` and cross-blueprint writes reject paths whose table row type does not match the declared variable type.

Graph Toolkit displays one `DataTable` Blackboard type with a row-struct selector, a `BlueprintDataTableAsset` picker, and the serialized path. Dragging a DataTable variable from the Blackboard offers normal Get/Set variable nodes. Dragging a `BlueprintDataTableAsset` from the Project window keeps the direct query-node choices and also offers DataTable variable Get/Set choices; an existing variable with the same path and row type is reused.

DataTable query nodes resolve their table in this order: connected `dataTable` input, literal `dataTable` property, then legacy `tableAssetGuid` / `tablePath`. The cached `rowStructTypeId` types the input and outputs, so connecting different row-struct types is a validation error.

### `DataTable.GetRow`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/DataTable.GetRow.node.json
```

Executor:

```text
ID: DataTable.GetRow
Class: DataTableGetRowExecutor
File: Assets/BlueprintSystem/Executors/Variables/DataTableExecutors.cs
```

Function:

```text
Reads `rowName` from the configured data table.
Returns the typed struct row through `row` and whether it was found through `found`.
Missing rows return the selected struct default value and `found=false`.
Invalid tables or row struct types log an error and return `row=null`, `found=false`.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `dataTable` | input value | `DataTable<Struct.{RowType}>` | propertyOrConnection | no | Typed table reference; overrides legacy table properties. |
| `rowName` | input value | string | propertyOrConnection | yes | Unique row key. |
| `row` | output value | `Struct.{RowType}` | none | no | Dynamic type from the table row struct. |
| `found` | output value | bool | none | no | True only when `rowName` exists. |
| `dataTable` | property | `DataTable<Struct.{RowType}>` | property | no | Optional literal table selected in Graph Toolkit. |
| `tablePath` | hidden property | string | property | no | Legacy generated `.bpdatatable.json` path fallback. |
| `tableAssetGuid` | hidden property | string | property | no | Editor-only tracking for dragged table assets. |
| `rowStructTypeId` | hidden property | string | property | yes | Cached row struct type id for typing. |

### `DataTable.GetRowNames`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/DataTable.GetRowNames.node.json
```

Executor:

```text
ID: DataTable.GetRowNames
Class: DataTableGetRowNamesExecutor
File: Assets/BlueprintSystem/Executors/Variables/DataTableExecutors.cs
```

Function:

```text
Returns all row names from the configured data table in table order.
Invalid tables log an error and return an empty array.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `dataTable` | input value | `DataTable<Struct.{RowType}>` | propertyOrConnection | no | Typed table reference; overrides legacy table properties. |
| `rowNames` | output value | `Array<string>` | none | no | Row names in table order. |
| `dataTable` | property | `DataTable<Struct.{RowType}>` | property | no | Optional literal table selected in Graph Toolkit. |
| `tablePath` | hidden property | string | property | no | Legacy generated `.bpdatatable.json` path fallback. |
| `tableAssetGuid` | hidden property | string | property | no | Editor-only tracking for dragged table assets. |
| `rowStructTypeId` | hidden property | string | property | yes | Cached row struct type id for typing. |

### `DataTable.GetAllRows`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/DataTable.GetAllRows.node.json
```

Executor:

```text
ID: DataTable.GetAllRows
Class: DataTableGetAllRowsExecutor
File: Assets/BlueprintSystem/Executors/Variables/DataTableExecutors.cs
```

Function:

```text
Returns every row value from the configured data table in table order.
Invalid tables or row values log an error and return an empty array.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `dataTable` | input value | `DataTable<Struct.{RowType}>` | propertyOrConnection | no | Typed table reference; overrides legacy table properties. |
| `rows` | output value | `Array<Struct.{RowType}>` | none | no | Dynamic type from the table row struct. |
| `dataTable` | property | `DataTable<Struct.{RowType}>` | property | no | Optional literal table selected in Graph Toolkit. |
| `tablePath` | hidden property | string | property | no | Legacy generated `.bpdatatable.json` path fallback. |
| `tableAssetGuid` | hidden property | string | property | no | Editor-only tracking for dragged table assets. |
| `rowStructTypeId` | hidden property | string | property | yes | Cached row struct type id for typing. |

## Resource Blueprints

Resource Blueprints describe project assets in the same role as an Unreal `PrimaryDataAsset`: stable identity, typed metadata, soft references, dependencies, preload groups, load priority, and budget hints. They are data assets only. V1 Resource Graphs do not execute `OnLoaded`, `OnRelease`, or any other lifecycle graph.

Source of truth:

```text
Resource Blueprint JSON: Assets/**/*.resourceblueprint.json
Resource Graph Toolkit asset: Assets/**/*.resourcebpgraph
Generated runtime registry: Assets/Resources/BlueprintResourceRegistry.asset
Resource type catalog: BlueprintResourceTypeCatalogAsset
Schema: Assets/BlueprintSystem/Specs/Schemas/resourceblueprint.schema.json
```

The JSON file is always authoritative. A `.resourcebpgraph` graph is an editor view for configuration, dependencies, preload groups, and validation context. Import/export bridge commands live under:

```text
Tools > Blueprint System > Resource Graph Toolkit > Import Selected Resource Blueprint JSON
Tools > Blueprint System > Resource Graph Toolkit > Export Selected Resource Graph To JSON
Assets > Create > Blueprint System > Resource Blueprint Graph
```

Double-clicking a `.resourceblueprint.json` source asset opens the Resource Graph Toolkit view by importing or refreshing the sibling `.resourcebpgraph`.

Resource Graph authoring in Graph Toolkit uses the Blackboard for the primary resource fields: `resourceType`, `resourceName`, `displayName`, and `mainAsset`. `resourceType` is a catalog-backed dropdown sourced from `BlueprintResourceTypeCatalogAsset.ResourceTypes`, similar to Unreal Asset Manager's central Primary Asset Types configuration. Add or edit entries in the catalog asset to add available type options. The blackboard stores the selected resource type as a string reference and exports it to JSON as `"resourceType": "<string>"`. Empty values stay empty, and missing or unknown current values remain selectable so older graphs do not lose their ids when a catalog entry is absent. `mainAsset` is a `BlueprintResourceAssetReference` value with an Object Field for picking or dragging the Unity asset; the graph still exports the soft-reference JSON shape `{ guid, path, address, assetType }`. When `mainAsset` changes and `resourceName` is empty, export/sync fills `resourceName` from the selected asset name, or from the asset path file name when the asset cannot be loaded. `mainAsset.address` is still generated by the Asset Manager, not hand-authored in the graph.

Older resource graphs may still contain `mainAssetPath`, `mainAssetGuid`, and `mainAssetType` blackboard variables. Opening or syncing the graph migrates those legacy string variables into the typed `mainAsset` field and removes the old variables from the fixed resource blackboard surface.

`Dependencies` are still edited through Asset Manager or JSON in V1. They use resource type and resource name selectors backed by scanned `.resourceblueprint.json` ids where available, while preserving missing typed values. A friendlier object picker/type dropdown surface can be added later as a dedicated Resource editor window because Graph Toolkit graph assets are not regular `UnityEngine.Object` graph model inspectors.

Resource identity uses `Type + Name` and forms the stable primary id:

```text
PrimaryResourceId = "{resourceType}:{resourceName}"
```

Unity GUIDs, asset paths, and Addressables addresses are tracking and migration details, not the primary identity. Renaming `resourceType` or `resourceName` is a semantic id migration and may affect dependents.

Minimal JSON shape:

```json
{
  "schemaVersion": "0.1",
  "resourceType": "Item",
  "resourceName": "Sword_01",
  "displayName": "Sword 01",
  "tags": ["Weapon"],
  "mainAsset": {
    "guid": "00000000000000000000000000000000",
    "path": "Assets/Game/Items/Sword_01.prefab",
    "address": "Resource/Item/Sword_01",
    "assetType": "UnityEngine.GameObject"
  },
  "dependencies": [
    { "resourceType": "Icon", "resourceName": "Sword_01", "required": true, "preloadGroup": "Inventory" }
  ],
  "preloadGroups": ["Inventory"],
  "priority": 0,
  "memoryBudgetMb": 0,
  "metadata": [
    { "key": "rarity", "type": "string", "value": "Rare" }
  ]
}
```

Authoring rules:

| Rule | Notes |
| --- | --- |
| Unique id | `resourceType + resourceName` must be unique across the project. |
| Main asset | The main asset is selected in the editor, but the JSON stores soft reference data: GUID, path, Addressables address, and asset type. |
| Dependencies | Dependencies reference other primary resource ids, not Unity object references. |
| Metadata | Common metadata is stored on every resource; resource-type-specific required fields come from the matching `BlueprintResourceTypeCatalogAsset.ResourceTypes` entry. |
| Preload groups | Any resource can belong to one or more groups. Groups are compiled into the registry for runtime batch preload. |
| Priority and budget | Priority participates in the runtime load queue; memory budget is an estimate used for reporting and scheduling hints. |

The Asset Manager creates the canonical project resource type catalog at `Assets/BlueprintSystem/Resources/BlueprintResourceTypeCatalog.asset` when the Resource Asset Manager window opens and no catalog exists. You can also create one manually with:

```text
Create > Blueprint System > Resource Type Catalog
```

The catalog contains the full project resource type list. Each type entry can require metadata fields and provide default values for the Asset Manager. Missing required fields are validation errors. Resource type metadata is authored only through this catalog.

### Resource Asset Manager

Open the project-level resource manager from:

```text
Tools > Blueprint System > Resource Asset Manager > Open
Tools > Blueprint System > Resource Asset Manager > Validate
Tools > Blueprint System > Resource Asset Manager > Sync All
```

The Asset Manager scans all `.resourceblueprint.json` files, validates them, normalizes soft reference data, syncs Addressables, and writes `Assets/Resources/BlueprintResourceRegistry.asset` when there are no blocking errors. The window uses a list plus detail layout with search, type/tag filters, validation issues, main asset status, dependencies, reverse dependencies, and buttons to open the JSON or Resource Graph. Its `Resource Types` toolbar view edits the canonical `BlueprintResourceTypeCatalogAsset` inline and includes a `Ping Catalog` shortcut for locating the asset in the Project window.

Addressables sync is owned by the Asset Manager:

| Generated value | Rule |
| --- | --- |
| Base group | `BlueprintResources_Base_{resourceType}` |
| DLC group | `BlueprintResources_DLC_{dlcId}_{resourceType}` |
| Address | `Resource/{resourceType}/{resourceName}` |
| Labels | `ResourceBlueprint`, `Resource.{resourceType}`, `ResourceContent.Base` or `ResourceContent.DLC`, optional `ResourceDLC.{dlcId}`, and `ResourceTag.{tag}` |

Automatic sync is scheduled after resource blueprint JSON, resource graph, resource type catalog, or resource packaging policy imports. Manual `Sync All` performs a full scan, Addressables sync, registry write, and validation pass.

Build-time validation blocks the player build when a resource error exists:

```text
duplicate Type+Name
missing main asset
invalid Addressables address
stale or missing registry
dependency cycle
missing dependency
asset type mismatch
required metadata field missing
invalid budget or priority configuration
```

Warnings do not block builds, but they should be fixed before content ship. Loading failures at runtime return an error state and log through the blueprint logger; V1 does not silently substitute fallback resources.

### Runtime Resource Manager

The generated `BlueprintResourceRegistryAsset` maps primary ids to runtime summaries:

```text
PrimaryResourceId -> Addressables address, asset type, tags, metadata, dependencies, preload groups, source hash, priority, memory budget
```

`BlueprintResourceManager` is async-first and Addressables-backed by default. It exposes:

```text
LoadAsync(resourceType, resourceName, scope)
PreloadGroupAsync(groupName, scope)
Release(resourceType, resourceName)
ReleaseScope(scope)
GetLoadState(resourceType, resourceName)
GetLoadedAsset(resourceType, resourceName)
GetLastError(resourceType, resourceName)
GetMetadata(resourceType, resourceName)
```

Scopes are explicit: `Scene`, `Screen`, `Gameplay`, `Global`, and `Manual`. Scope release decrements all references owned by that scope. Loading is reference-counted; same-id concurrent requests are deduplicated and share the provider operation. Cancelling a handle removes that subscriber. The underlying load continues while other subscribers or retained references remain.

The scheduler respects registry priority, a max concurrent load count, and memory budget estimates. Remote catalog and content version fields are preserved for future content delivery work, but V1 targets local Addressables packages.

### `Resource.LoadAsync`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Resource.LoadAsync.node.json
```

Executor:

```text
ID: Resource.LoadAsync
Class: ResourceLoadAsyncExecutor
File: Assets/BlueprintSystem/Executors/Resource/ResourceExecutors.cs
```

Function:

```text
Reads `resourceType`, `resourceName`, and `scope`, then requests an async load from `BlueprintResourceManager`.
When the handle completes, resumes through `loaded`, `failed`, or `cancelled`.
Outputs the loaded Unity object, final state, and error text.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts the load request. |
| `resourceType` | input value | string | propertyOrConnection | yes | Primary resource type. |
| `resourceName` | input value | string | propertyOrConnection | yes | Primary resource name. |
| `scope` | input value | `BlueprintResourceScope` | propertyOrConnection | no | Defaults to `Manual`. |
| `resourceType` | property | string | property | yes | Used when no value edge is connected. |
| `resourceName` | property | string | property | yes | Used when no value edge is connected. |
| `scope` | property | `BlueprintResourceScope` | property | no | Graph Toolkit enum dropdown. |
| `loaded` | output exec | none | none | no | Fires when a Unity object was loaded. |
| `failed` | output exec | none | none | no | Fires on registry, provider, or Addressables failure. |
| `cancelled` | output exec | none | none | no | Fires when this subscriber was cancelled before completion. |
| `asset` | output value | object | none | no | Loaded Unity object. |
| `state` | output value | `BlueprintResourceLoadState` | none | no | Final state for this node execution. |
| `error` | output value | string | none | no | Empty on success. |

### `Resource.PreloadGroupAsync`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Resource.PreloadGroupAsync.node.json
```

Executor:

```text
ID: Resource.PreloadGroupAsync
Class: ResourcePreloadGroupAsyncExecutor
File: Assets/BlueprintSystem/Executors/Resource/ResourceExecutors.cs
```

Function:

```text
Reads `groupName` and `scope`, then loads every registry entry in that preload group.
Resumes through `completed` when every handle succeeds, or `failed` when any member fails.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts preload. |
| `groupName` | input value | string | propertyOrConnection | yes | Preload group name. |
| `scope` | input value | `BlueprintResourceScope` | propertyOrConnection | no | Defaults to `Manual`. |
| `groupName` | property | string | property | yes | Used when no value edge is connected. |
| `scope` | property | `BlueprintResourceScope` | property | no | Graph Toolkit enum dropdown. |
| `completed` | output exec | none | none | no | Fires when all resources loaded. |
| `failed` | output exec | none | none | no | Fires if any load failed. |
| `state` | output value | `BlueprintResourceLoadState` | none | no | `Loaded` or `Failed`. |
| `error` | output value | string | none | no | Combined failure text. |

### `Resource.Release`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Resource.Release.node.json
```

Executor:

```text
ID: Resource.Release
Class: ResourceReleaseExecutor
File: Assets/BlueprintSystem/Executors/Resource/ResourceExecutors.cs
```

Function:

```text
Releases one resource reference by `resourceType + resourceName`, or releases every reference owned by `scope` when `releaseScope=true`.
Always continues through `execOut`.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `execIn` | input exec | none | none | no | Starts release. |
| `resourceType` | input value | string | propertyOrConnection | no | Required when `releaseScope=false`. |
| `resourceName` | input value | string | propertyOrConnection | no | Required when `releaseScope=false`. |
| `releaseScope` | input value | bool | propertyOrConnection | no | Releases a whole scope when true. |
| `scope` | input value | `BlueprintResourceScope` | propertyOrConnection | no | Scope to release. |
| `execOut` | output exec | none | none | no | Continuation after release. |

### `Resource.GetLoadState`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Resource.GetLoadState.node.json
```

Executor:

```text
ID: Resource.GetLoadState
Class: ResourceGetLoadStateExecutor
File: Assets/BlueprintSystem/Executors/Resource/ResourceExecutors.cs
```

Function:

```text
Reads current resource manager state without starting a load.
Returns state, loaded Unity object if present, and last error.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `resourceType` | input value | string | propertyOrConnection | yes | Primary resource type. |
| `resourceName` | input value | string | propertyOrConnection | yes | Primary resource name. |
| `state` | output value | `BlueprintResourceLoadState` | none | no | `Unloaded`, `Queued`, `Loading`, `Loaded`, `Failed`, or `Cancelled`. |
| `loaded` | output value | object | none | no | Loaded Unity object when present. |
| `error` | output value | string | none | no | Last failure text. |

### `Resource.GetMetadata`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Resource.GetMetadata.node.json
```

Executor:

```text
ID: Resource.GetMetadata
Class: ResourceGetMetadataExecutor
File: Assets/BlueprintSystem/Executors/Resource/ResourceExecutors.cs
```

Function:

```text
Reads metadata from the generated registry. If `key` is empty, returns the full metadata object as compact JSON.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `resourceType` | input value | string | propertyOrConnection | yes | Primary resource type. |
| `resourceName` | input value | string | propertyOrConnection | yes | Primary resource name. |
| `key` | input value | string | propertyOrConnection | no | Metadata key. Empty returns the full object. |
| `value` | output value | string | none | no | Metadata value or full metadata JSON. |
| `found` | output value | bool | none | no | True when a resource and matching key exist. |
| `error` | output value | string | none | no | Registry or missing resource error. |

## Array Nodes

Blueprint variables support `Array<T>` where `T` is a supported built-in type, enum, user struct, or `[BlueprintVariableType]` structured type. Nested arrays and `Binding<T>` elements are not supported.
In Graph Toolkit, `Array<T>` appears as one Blackboard type named `Array`. The array field contains an element type dropdown plus a JSON text field for defaults such as `["A","B"]`; `Variable.Get`, `Variable.Set`, validation, and export preserve the selected `Array<T>` blueprint type.

### `Array.Count`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Array.Count.node.json
```

Executor:

```text
ID: Array.Count
Class: ArrayCountExecutor
File: Assets/BlueprintSystem/Executors/Variables/BlueprintArrayExecutors.cs
```

Function:

```text
Returns item count from connected `array`.
Invalid or null values count as 0.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- | --- |
| `array` | input value | untyped array | connection | yes | Usually from `Variable.Get`. |
| `count` | output value | int | none | no | Number of items. |

### `Array.Get`

Manifest:

```text
Assets/BlueprintSystem/Specs/Nodes/Array.Get.node.json
```

Executor:

```text
ID: Array.Get
Class: ArrayGetExecutor
File: Assets/BlueprintSystem/Executors/Variables/BlueprintArrayExecutors.cs
```

Function:

```text
Reads `array` and `index`.
Returns the item through output `item`, or null when the index is out of range.
```

Ports and parameters:

| ID | Kind | Type | Source | Required | Default | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `array` | input value | untyped array | connection | yes | none | Usually from `Variable.Get`. |
| `index` | input value | int | propertyOrConnection | yes | `0` | Zero-based item index. |
| `index` | property | int | property | no | `0` | Used when no value edge is connected. |
| `item` | output value | untyped | none | no | none | Item value. |

## MCP Diagnostics and Safe Maintenance Tools

BlueprintSystem registers the following MCP tools from `Assets/BlueprintSystem/Editor/MCPTool`. They operate on embedded or upstream package source; do not edit `Library/PackageCache`.

| Tool | Use | Mutation boundary |
| --- | --- | --- |
| `blueprint_compile_dependency_ordered` | Builds a stable Component dependency graph, compiles children before owners, and reports cycles or missing dependencies. | Writes generated `.compiled.asset` files and optional runtime registries. `dryRun=true` writes nothing. |
| `blueprint_runtime_component_snapshot` | Reads declared runtime variables and owner/component metadata from a loaded Runner or nested Component. | Read-only; requires Play Mode and returns exposed variables by default. |
| `blueprint_event_trace` | Temporarily traces one Runner's event delivery, nodes, selected ports, variable writes, and cross-Blueprint events. | `observe` is read-only. `trigger` invokes the requested event and returns `stateMutation=true`. |
| `unity_asset_reference_scan` | Finds Unity dependencies plus Blueprint, Behavior Tree, DataTable, and optional text references before a deletion or migration. | Read-only; never moves or deletes assets. |

Recommended maintenance flow:

1. Before recompiling a root graph, call `blueprint_compile_dependency_ordered` with `dryRun=true`; resolve cycles and required missing dependencies, then rerun without `dryRun`.
2. Before deletion, call `unity_asset_reference_scan`. Only treat `safeToDelete=true` as a conservative clean result; a truncated or parse-incomplete scan always returns false.
3. In Play Mode, inspect a nested Component with `blueprint_runtime_component_snapshot` before using `blueprint_event_trace`. Supply `componentPath` whenever repeated Component names are possible.

All new tools return stable error codes rather than relying on Console text. Common codes include `BP_PLAY_MODE_REQUIRED`, `BP_RUNTIME_COMPONENT_AMBIGUOUS`, `BP_COMPILE_DEPENDENCY_CYCLE`, `BP_TRACE_RECORD_LIMIT`, `ASSET_SCAN_RESULT_LIMIT`, and `ASSET_SCAN_INCOMPLETE`. Trace execution errors use `BP_TRACE_EXECUTION_FAILED`; a trace sink is removed when the call ends, Play Mode exits, or a domain reload starts.

## Existing Node Families Not Yet Implemented

Before adding nodes in these areas, check whether a current node plus variables/events can solve the case. If not, add a new node deliberately and update this guide.

Currently not implemented as first-class nodes:

```text
Math arithmetic
String formatting
Animation/tweening
Scene loading
Audio playback
HTTP/networking
Async asset loading
```

## Adding A New Node

Before creating a new node:

1. Search this guide for the intended behavior.
2. Search package node manifests under `Packages/com.shadedclark.blueprint-system/**/Specs/Nodes` or embedded `Assets/BlueprintSystem/**/Specs/Nodes`, plus project manifests under `Assets/**`, for a similar `typeId`.
3. Search `BlueprintExecutorRegistry.CreateDefault()` for an existing executor.
4. Prefer extending an existing node only when the semantics remain the same.
5. Prefer a new node when the runtime side effect, lifecycle, or target Unity API differs.

Required files for a new public node:

```text
1. Package/core nodes: BlueprintSystem package root `**/Specs/Nodes/<TypeId>.node.json`; project-owned nodes: `Assets/**/<TypeId>.node.json`
2. Executor class under Assets/BlueprintSystem/Executors/
3. Registration in BlueprintExecutorRegistry.CreateDefault()
4. Graph Toolkit visual node class under Assets/BlueprintSystem/Editor/GraphToolkit/
5. Tests in Assets/BlueprintSystem/Tests/Editor/BlueprintSystemTests.cs
6. Update this GUIDE.md
```

Feature modules in the BlueprintSystem package may keep the same surfaces under `<PackageRoot>/<Module>/Specs/Nodes`, `Executors`, `Editor/GraphToolkit`, and `Tests/Editor`. Project-owned node manifests may live under `Assets/**`, while executor code and tests still need normal Unity assembly placement. Use a small module registrar when a node family would otherwise add several lines to `BlueprintExecutorRegistry.CreateDefault()`.

Naming conventions:

```text
typeId: Category.Action or Category.Event.Name
executor id: usually same as typeId, except event entries use Flow.Event
property IDs: match context.GetInputValue(node, "<id>") calls
exec input: usually execIn
exec continuation: usually execOut
UI target property: usually target
```

Manifest consistency checklist:

```text
manifest.typeId == blueprint JSON node typeId
manifest.executor == executor.ExecutorId
manifest input/property IDs == executor GetInputValue IDs
manifest output IDs == BlueprintExecResult.Continue IDs
Binding<T> property stores a binding name string in JSON
```
