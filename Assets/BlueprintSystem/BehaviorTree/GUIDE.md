# Behavior Tree Node Guide

This guide is for developers and coding agents extending the Behavior Tree runtime.
Behavior Trees and normal Blueprints are separate runtimes. Keep Behavior Tree runtime, editor tooling, samples, and node documentation under `Assets/BlueprintSystem/BehaviorTree/`.

Current source of truth:

```text
Behavior Tree JSON: **/*.btree.json
Behavior Tree visual graph cache: **/*.btgraph
Compiled runtime assets: BehaviorTreeCompiledAsset generated from .btree.json
Runtime executors and registry: Assets/BlueprintSystem/BehaviorTree/Runtime/BehaviorTreeExecutors.cs
Runtime model and Blackboard: Assets/BlueprintSystem/BehaviorTree/Runtime/BehaviorTreeRuntime.cs
Runner component: Assets/BlueprintSystem/BehaviorTree/Runtime/BehaviorTreeRunner.cs
Graph Toolkit visual nodes: Assets/BlueprintSystem/BehaviorTree/Editor/GraphToolkit/*.cs
Design notes: Assets/BlueprintSystem/BehaviorTree/BehaviorTreeDesign.md
```

Important rule:

```text
Behavior Tree nodes do not use Blueprint .node.json manifests or the normal Blueprint exec queue.
A user-facing Behavior Tree node needs a Behavior Tree executor, default registry entry,
and Graph Toolkit metadata or a dedicated visual node when it is edited visually.
```

## Runtime Model

Behavior Trees are tick-based AI graphs. Each node returns `Success`, `Failure`, or `Running`.

```text
.btree.json -> .btgraph -> BehaviorTreeCompiledAsset -> BehaviorTreeRunner -> BehaviorTreeRuntime
```

Use `.btree.json` as the source of truth. `.btgraph` is a Graph Toolkit editor/cache asset. `BehaviorTreeCompiledAsset` is the runtime asset assigned to `BehaviorTreeRunner.compiledBehaviorTree`.

`BehaviorTreeRunner` supports `Update`, `FixedUpdate`, `Manual`, and `Interval` tick modes. `Update` ticks through `maxTickRate`, `Interval` ticks through `intervalSeconds`, and `Manual` expects callers to invoke `ManualTick(deltaTime)`.

Value input resolution order:

```text
node.inputs inputId -> Blackboard value
node.properties property value
legacy property fallback or executor default
```

Blackboard key input resolution order:

```text
node.inputs inputId -> Blackboard key name
node.properties property key name
null/default
```

For example, `BT.SetBlackboard.inputs.key = "Target"` means the destination key is `Target`; it does not read the value stored in `Target`.

Decorator value input resolution order:

```text
decorator.inputs inputId -> Blackboard value
decorator.properties legacy *Key property -> Blackboard value
decorator.properties direct value
null/default
```

For example, `BT.CompareFloat.inputs.left = "DistanceToTarget"` reads the current `DistanceToTarget` Blackboard value. Decorator inputs are value sources; new visual Decorator nodes do not expose string Key ports for `key`, `leftKey`, `rightKey`, `sourceKey`, `targetKey`, or `distanceKey`.

Decorators and services are attached to tree nodes by id:

```json
{
  "id": "chase_sequence",
  "typeId": "BT.Sequence",
  "children": ["move_to_target", "attack"],
  "decorators": ["has_target"],
  "services": ["update_distance"],
  "properties": {}
}
```

The full decorator and service definitions live in the graph-level `decorators` and `services` arrays. They are not ordinary child nodes.

In the Behavior Tree Graph Toolkit, condition Decorators are edited as dedicated visual nodes. Connect a Decorator node's `Condition` output to a tree node's `Conditions` input to attach that condition. Export writes the connection back to the target node's `decorators` id list, so the `.btree.json` schema stays unchanged. Older graphs that only store the comma-separated Decorators id list on a tree node remain valid and continue to export.

## JSON Shape

Top-level `.btree.json` structure:

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

Blackboard key:

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

Tree node:

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

Decorator:

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

Service:

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

## Blackboard

Supported Blackboard types:

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

Default values may store primitives, strings, vectors, and `Blueprint` asset paths. `GameObject`, `Transform`, and `BlueprintRef` values are runtime-only object values and should use `null` JSON defaults.

`BehaviorTreeRunner` exposes Blackboard overrides in the Inspector for compiled keys. `GameObject` and `Transform` overrides use `ObjectValue`; other keys use JSON text where possible, with plain string fallback for `string` and `Blueprint`.

Vector resolution accepts `Vector3`, `Vector2`, `Transform`, `GameObject`, `Component`, or a three-item JSON array. A `Vector2` resolves to `(x, 0, y)`.

In the Behavior Tree Graph Toolkit, typed `int`, `float`, `bool`, `Vector2`, and `Vector3` value ports can be edited inline on the visual node when they are not connected to Blackboard variables. Generic/object value ports, destination key ports, and Blackboard key/reference ports remain Blackboard-only.

## Current Node Summary

| Family | Type IDs | Purpose |
| --- | --- | --- |
| Composites | `BT.Root`, `BT.Selector`, `BT.Sequence`, `BT.Parallel`, `BT.RandomSelector`, `BT.PrioritySelector`, `BT.WeightedSelector` | Root entry, ordered child evaluation, parallel child polling, randomized selection, priority re-evaluation, and weighted selection. |
| Tasks | `BT.Wait`, `BT.SetBlackboard`, `BT.ClearBlackboard`, `BT.MoveTo`, `BT.RotateTo`, `BT.TriggerBlueprintEvent`, `BT.RunBlueprintTask`, `BT.Log` | Basic actions, Blackboard mutation, movement, rotation, Blueprint event bridging, and simple async Blueprint-task polling. |
| Decorators | `BT.BlackboardCondition`, `BT.CompareFloat`, `BT.CompareBool`, `BT.ObjectIsSet`, `BT.DistanceLessThan`, `BT.Cooldown` | Branch guards evaluated before ticking the attached node. |
| Services | `BT.UpdateDistance`, `BT.PerceptionSphere`, `BT.PerceptionRaycast`, `BT.SetBlackboardFromBlueprint`, `BT.TriggerBlueprintService` | Periodic updates while the owning node is active. |

## Composite Nodes

| Type ID | Purpose | Parameters |
| --- | --- | --- |
| `BT.Root` | Entry node for one tree. Returns the status of its only child. | `children` must contain exactly one node id. |
| `BT.Selector` | Runs children left to right until one returns `Success` or `Running`. Continues a running child on the next tick before trying later children. | `children` must contain at least one node id. |
| `BT.Sequence` | Runs children left to right until one returns `Failure` or `Running`. Continues a running child on the next tick before trying later children. | `children` must contain at least one node id. |
| `BT.Parallel` | Ticks unfinished children each frame, fails when any child fails, succeeds only when every child succeeds, and aborts running siblings on failure. | `children` must contain at least one node id. |
| `BT.RandomSelector` | Shuffles child order when it starts, keeps that order while a child is running, succeeds on the first successful child, and fails when all shuffled children fail. | `children` must contain at least one node id. |
| `BT.PrioritySelector` | Treats child order as priority and rechecks from the first child every tick so higher-priority branches can preempt lower-priority running branches. | `children` must contain at least one node id. |
| `BT.WeightedSelector` | Builds a weighted random child order when it starts, keeps that order while a child is running, succeeds on the first successful child, and fails when all selectable children fail. | `children` must contain at least one node id. `properties.weights` may contain float weights matching child order. |

`BT.WeightedSelector` reads `properties.weights`. Missing weights or entries past the end of the array default to `1`. Extra weights are ignored. Entries that are `<= 0`, `NaN`, `Infinity`, or cannot convert to float are treated as unselectable. If every child is unselectable, the selector falls back to all children with equal weight.

## Task Nodes

### `BT.Wait`

Waits for a duration, then returns `Success`.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `duration` | input/property | float | `0` | Seconds to wait. |
| `seconds` | legacy property | float | `0` | Used only when `duration` is not set. |

Abort clears the stored end time.

### `BT.SetBlackboard`

Writes a Blackboard value and returns `Success`.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `key` | input/property | Blackboard key name | required | Destination key. An input binding names the destination key directly. |
| `value` | input/property | any | `null` | Value to write. Input binding reads the bound Blackboard value. |
| `valueKey` | legacy property | Blackboard key name | none | Used only when `value` is not set; copies from another Blackboard key. |

### `BT.ClearBlackboard`

Sets a Blackboard key value to `null` and returns `Success`.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `key` | input/property | Blackboard key name | required | Key to clear. An input binding names the key directly. |

### `BT.MoveTo`

Moves the owner toward a target and returns `Running` until it reaches the target. Uses `NavMeshAgent` when one is enabled and on the NavMesh; otherwise it can move the owner transform directly.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `target` | input | Vector3-compatible value | required | Blackboard value can be a vector, GameObject, Transform, or Component. |
| `targetKey` | property | Blackboard key name | none | Fallback target source. |
| `targetPosition` | input/property | Vector3 | none | Direct fallback target. |
| `acceptableRadius` | input/property | float | `0.25` | Success distance. |
| `stoppingDistance` | legacy property | float | `0.25` | Used only when `acceptableRadius` is not set. |
| `speed` | input/property | float | `3` | Transform fallback movement speed. |
| `allowTransformFallback` | input/property | bool | `true` | Allows direct transform movement when no usable `NavMeshAgent` exists. |
| `stopOnAbort` | input/property | bool | `true` | Stops the `NavMeshAgent` when the running node is aborted. |

### `BT.RotateTo`

Rotates the owner toward a target and returns `Running` until it faces the target.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `target` | input | Vector3-compatible value | required | Blackboard value can be a vector, GameObject, Transform, or Component. |
| `targetKey` | property | Blackboard key name | none | Fallback target source. |
| `targetPosition` | input/property | Vector3 | none | Direct fallback target. |
| `ignoreY` | input/property | bool | `true` | Flattens the direction onto the XZ plane. |
| `angleTolerance` | input/property | float | `2` | Success angle in degrees. |
| `rotationSpeed` | input/property | float | `360` | Degrees per second. |

### `BT.TriggerBlueprintEvent`

Triggers one event on a resolved Blueprint instance and returns immediately.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `eventName` | input/property | string | required | Event sent to the target Blueprint instance. |
| `target` | input/property | Blueprint target | owner `BlueprintRunner` fallback | Accepts `BlueprintRef`, `IBlueprintInstance`, GameObject with `BlueprintRunner`, or a `.blueprint.json` asset path resolvable inside the current Blueprint instance tree. |
| `targetKey` | property | Blackboard key name | none | Reads target from Blackboard. |
| `targetBlueprint` | legacy property | Blueprint asset path | none | Fallback target path. |
| `successOnMissing` | input/property | bool | `false` | Returns `Success` instead of `Failure` when no target resolves. |

### `BT.RunBlueprintTask`

Starts a Blueprint event once, then polls Blackboard completion/failure inputs until the task finishes, times out, or is aborted.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `startEventName` | input/property | string | none | Event sent when the BT task starts. |
| `eventName` | input/property | string | none | Fallback start event when `startEventName` is missing. |
| `target` | input/property | Blueprint target | owner `BlueprintRunner` fallback | Same target resolution as `BT.TriggerBlueprintEvent`. |
| `targetKey` | property | Blackboard key name | none | Reads target from Blackboard. |
| `targetBlueprint` | legacy property | Blueprint asset path | none | Fallback target path. |
| `timeout` | input/property | float | `0` | Seconds before timeout; `0` disables timeout. |
| `complete` | input/property | bool-like | none | When truthy, returns `Success`. Inline visual default is `false` and is omitted unless explicitly stored. |
| `completeKey` | legacy property | Blackboard key name | none | Fallback completion key. |
| `failure` | input/property | bool-like | none | When truthy, returns `Failure`. Inline visual default is `false` and is omitted unless explicitly stored. |
| `failureKey` | legacy property | Blackboard key name | none | Fallback failure key. |
| `timeoutStatus` | input/property | string | `Failure` | Use `Success` to succeed on timeout; any other value fails. |
| `abortEventName` | input/property | string | none | Event sent if the task is aborted while running. |

If no `complete` input and no `completeKey` are configured, the node succeeds immediately after starting the event.

### `BT.Log`

Logs a message through the Blueprint logger and returns `Success`.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `message` | input/property | string | node id | Log text after the `[BehaviorTree]` prefix. |

## Decorator Nodes

Decorators are evaluated before the attached tree node ticks. If any decorator returns false, the node returns `Failure`.

Condition nodes in the visual graph are Decorators, not ordinary tree children. A Decorator that exists in the graph-level `decorators` array but is not referenced by any tree node is serialized but never evaluated at runtime.

Current runtime re-evaluates attached decorators every tick. Do not rely on serialized `abortMode` properties for custom semantics unless the decorator/runtime code explicitly consumes them.

Decorator `operator` properties use the `BehaviorTreeComparisonOperator` enum. JSON stores the enum name, such as `"IsSet"` or `"LessOrEqual"`, for readability and compatibility with older source files.

Supported enum values:

```text
IsSet
IsNotSet
IsTrue
IsFalse
Equals
NotEquals
Greater
GreaterOrEqual
Less
LessOrEqual
```

### `BT.BlackboardCondition`

Evaluates a Blackboard value against a generic operator.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `value` | input | any Blackboard value | required | Actual value source. `IsSet` and `IsNotSet` test whether this bound Blackboard key currently has a non-null value. |
| `operator` | property | `BehaviorTreeComparisonOperator` | `IsSet` | Supports all comparison enum values. |
| `expected` | input/property | any Blackboard value | `null` | Expected value for equality or numeric comparisons. Legacy `value` property is still accepted as a fallback. |
| `key` | legacy property | Blackboard key name | none | Fallback actual value source for older JSON. |

### `BT.CompareFloat`

Compares two float values.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `left` | input/property | float Blackboard value | `0` | Left value. Legacy `leftKey` still reads from Blackboard before the direct `left` property fallback. |
| `right` | input/property | float Blackboard value | `0` | Right value. Legacy `rightKey` still reads from Blackboard before the legacy `value` property fallback. |
| `operator` | property | `BehaviorTreeComparisonOperator` | `LessOrEqual` | Supports `Greater`, `GreaterOrEqual`, `Less`, `LessOrEqual`, `Equals`, `NotEquals`. |

### `BT.CompareBool`

Compares two bool values.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `value` | input/property | bool Blackboard value | `false` | Actual bool source. Legacy `key` still reads from Blackboard as a fallback. |
| `expected` | input/property | bool Blackboard value | `true` | Expected value. Legacy `value` property is still accepted as a fallback. |
| `operator` | property | `BehaviorTreeComparisonOperator` | `Equals` | Supports `Equals` and `NotEquals`. |

### `BT.ObjectIsSet`

Returns true when a bound Blackboard value is currently non-null.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `value` | input | any Blackboard value | required | Value key to test. |
| `key` | legacy property | Blackboard key name | none | Fallback value source for older JSON. |

### `BT.DistanceLessThan`

Returns true when a resolved distance is less than or equal to a threshold.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `distance` | input/property | float value | none | If set, uses this distance value directly. Legacy `distanceKey` still reads from Blackboard before computing from source/target. |
| `source` | input/property | Vector3-compatible Blackboard value | owner position | Source value when computing distance. Legacy `sourceKey` and `sourcePosition` remain fallbacks. |
| `target` | input/property | Vector3-compatible Blackboard value | none | Target value when computing distance. Legacy `targetKey` and `targetPosition` remain fallbacks. |
| `maxDistance` | input/property | float value | `0` | Threshold. |
| `distanceKey` | legacy property | Blackboard key name | none | Fallback direct distance source for older JSON. |
| `sourceKey` | legacy property | Blackboard key name | owner position | Fallback source key. |
| `targetKey` | legacy property | Blackboard key name | none | Fallback target key. |
| `sourcePosition` | input/property | Vector3 | owner position | Direct source fallback. |
| `targetPosition` | input/property | Vector3 | none | Direct target fallback. |

### `BT.Cooldown`

Allows the branch, then blocks it until the cooldown expires. A node already returning `Running` is allowed to continue.

| Parameter | Type | Default | Notes |
| --- | --- | --- | --- |
| `duration` | float | `cooldown` or `0` | Cooldown seconds after an allowed evaluation. |
| `cooldown` | float | `0` | Legacy duration fallback. |

## Service Nodes

Services tick only while their owning tree node is active.

Common service timing:

| Parameter | Type | Default | Notes |
| --- | --- | --- | --- |
| `interval` | float | `0` | Seconds between service ticks. `0` means every tree tick while active. |
| `randomDeviation` | float | `0` | Random offset added to `interval` after each tick. |

### `BT.UpdateDistance`

Writes the distance between a source and target to Blackboard.

| Parameter | Type | Default | Notes |
| --- | --- | --- | --- |
| `sourceKey` | Blackboard key name | owner position | Source vector key. |
| `sourcePosition` | Vector3 | owner position | Direct source fallback. |
| `targetKey` | Blackboard key name | none | Target vector key. |
| `targetPosition` | Vector3 | none | Direct target fallback. |
| `distanceKey` | Blackboard key name | `DistanceToTarget` | Destination key. If the target cannot resolve, writes `Infinity`. |

### `BT.PerceptionSphere`

Finds the first non-owner collider inside a sphere around the owner.

| Parameter | Type | Default | Notes |
| --- | --- | --- | --- |
| `radius` | float | `10` | Sphere radius. |
| `layerMask` | int | `-1` | Physics layer mask. |
| `targetKey` | Blackboard key name | `Target` | Destination key for the found GameObject. |
| `clearOnMiss` | bool | `false` | Clears `targetKey` when no target is found. |

### `BT.PerceptionRaycast`

Casts from the owner position in the owner forward direction.

| Parameter | Type | Default | Notes |
| --- | --- | --- | --- |
| `maxDistance` | float | `30` | Raycast distance. |
| `layerMask` | int | `-1` | Physics layer mask. |
| `targetKey` | Blackboard key name | `Target` | Destination key for the hit GameObject. |
| `hitPointKey` | Blackboard key name | none | Optional destination key for the hit point. |
| `clearOnMiss` | bool | `false` | Clears `targetKey` and `hitPointKey` when no hit is found. |

### `BT.SetBlackboardFromBlueprint`

Reads a Blueprint variable and writes it to Blackboard.

| Parameter | Type | Default | Notes |
| --- | --- | --- | --- |
| `variableName` | string | required | Blueprint variable to read. |
| `blackboardKey` | Blackboard key name | required | Blackboard key to write. |
| `targetKey` | Blackboard key name | owner `BlueprintRunner` fallback | Reads target from Blackboard. |
| `target` | Blueprint target | owner `BlueprintRunner` fallback | Direct target value or asset path. |
| `targetBlueprint` | Blueprint asset path | none | Legacy target path fallback. |

### `BT.TriggerBlueprintService`

Triggers a Blueprint event every time the service ticks.

| Parameter | Type | Default | Notes |
| --- | --- | --- | --- |
| `eventName` | string | required | Event sent to the target Blueprint instance. |
| `targetKey` | Blackboard key name | owner `BlueprintRunner` fallback | Reads target from Blackboard. |
| `target` | Blueprint target | owner `BlueprintRunner` fallback | Direct target value or asset path. |
| `targetBlueprint` | Blueprint asset path | none | Legacy target path fallback. |

## Graph Toolkit Notes

Behavior Tree Graph Toolkit exposes dedicated visual node classes for tree nodes:

```text
BTCompositeRootNode
BTCompositeSelectorNode
BTCompositeSequenceNode
BTCompositeParallelNode
BTCompositeRandomSelectorNode
BTCompositePrioritySelectorNode
BTCompositeWeightedSelectorNode
BTTaskWaitNode
BTTaskSetBlackboardNode
BTTaskClearBlackboardNode
BTTaskMoveToNode
BTTaskRotateToNode
BTTaskTriggerBlueprintEventNode
BTTaskRunBlueprintTaskNode
BTTaskLogNode
```

Behavior Tree Graph Toolkit also exposes dedicated visual nodes for built-in condition Decorators:

```text
BehaviorTreeVisualDecoratorNode
BTDecoratorBlackboardConditionNode
BTDecoratorCompareFloatNode
BTDecoratorCompareBoolNode
BTDecoratorObjectIsSetNode
BTDecoratorDistanceLessThanNode
BTDecoratorCooldownNode
```

`BehaviorTreeVisualNode` is the compatibility fallback for older or unknown serialized tree nodes. `BehaviorTreeVisualDecoratorNode` is both the create-menu fallback Decorator node and the compatibility fallback for older or unknown serialized Decorator nodes. When created directly, it defaults to `BT.BlackboardCondition`; prefer the dedicated `BTDecorator*` visual node classes for the other built-in Decorator types. Decorators connect to tree nodes through the `Conditions` input and are still stored in graph-level `Decorators` plus each tree node's attached Decorator id list. Services remain edited as attached id lists on tree nodes and stored in graph-level `Services`.

Built-in task parameters are exposed as Blackboard input ports. Built-in Decorator value parameters are exposed as condition node input ports. Export stores connected Blackboard variable nodes for tree task inputs in `node.inputs` and Decorator inputs in `decorator.inputs`, both as `inputId -> blackboard key`. Inline task and Decorator values are exported to `properties` only for ports that allow inline values and differ from defaults. Decorator operator ports use enum fields and export their enum names to `properties.operator`.

## Validation Rules

The validator enforces:

- Blackboard keys must have unique names and known types.
- `BlueprintRef`, `GameObject`, and `Transform` Blackboard keys cannot store JSON object reference defaults.
- Each behavior tree must contain exactly one `BT.Root` node.
- The top-level `root` field must reference the `BT.Root` node.
- `BT.Root` must have exactly one child.
- Composite nodes must have at least one child.
- Task nodes cannot have children.
- Child, decorator, service, property-key, and input-key references must exist.
- The tree cannot contain cycles.
- `BT.MoveTo` needs `target`, `targetKey`, or `targetPosition`.
- `BT.SetBlackboard` and `BT.ClearBlackboard` need `key`.
- `BT.TriggerBlueprintEvent` and `BT.RunBlueprintTask` need `eventName` or `startEventName`.

## Extending Behavior Trees

Before adding a Behavior Tree node, search existing executors first. Prefer existing node semantics when they match.

For a new public node:

1. Add or update a Behavior Tree executor under `Assets/BlueprintSystem/BehaviorTree/Runtime/`.
2. Register it in `BehaviorTreeExecutorRegistry.CreateDefault()`.
3. Add Graph Toolkit metadata or a dedicated visual node under `Assets/BlueprintSystem/BehaviorTree/Editor/GraphToolkit/` when the node is user-facing.
4. Keep `.btree.json` source files authoritative; treat `.btgraph` as editor/cache data.
5. Update this `Assets/BlueprintSystem/BehaviorTree/GUIDE.md`, not `Assets/BlueprintSystem/GUIDE.md`.

Do not add Behavior Tree nodes to `Assets/BlueprintSystem/Specs/Nodes/*.node.json`. Those manifests belong to the normal Blueprint runtime.

Useful samples:

```text
Assets/BlueprintSystem/Samples~/BehaviorTree/AI/Behavior/CapsuleFixedRoutePatrol.btree.json
Assets/BlueprintSystem/Samples~/BehaviorTree/AI/Behavior/EnemyPatrolChaseAttack.btree.json
Assets/BlueprintSystem/Samples~/BehaviorTree/AI/Behavior/SelectorFallbackExample.btree.json
Assets/BlueprintSystem/Samples~/BehaviorTree/AI/Behavior/ParallelWaitAllExample.btree.json
Assets/BlueprintSystem/Samples~/BehaviorTree/AI/Behavior/RandomSelectorChoiceExample.btree.json
Assets/BlueprintSystem/Samples~/BehaviorTree/AI/Behavior/PrioritySelectorPreemptExample.btree.json
Assets/Game/Blueprint/BehaviorTreePatrol/Behavior/CapsuleFixedRoutePatrol.btree.json
```

Scene usage:

```text
Assets/Scenes/SampleScene.unity
BehaviorTreePatrolDemoRoot
BehaviorTreeCompositeExamplesRoot
```
