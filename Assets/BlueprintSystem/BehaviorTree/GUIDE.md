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

## Module Requirement

The Behavior Tree module is default-on. It can be toggled for the active build target in `Project Settings > Blueprint System > Modules`. Disabling it writes the `BLUEPRINTSYSTEM_DISABLE_BEHAVIOR_TREE` scripting define and triggers Unity script recompilation.

When the module is disabled, `BT.*` executors are not registered, `.btree.json` editor compilation fails, Graph Toolkit import/export/open/debug surfaces are disabled, and `BehaviorTreeRunner` components stay serialized but do not start or tick. The normal Blueprint `BehaviorTree.*` Blackboard bridge nodes are also filtered from manifests and executor registration. Existing assets must be recompiled after toggling the module to surface missing-node or missing-executor errors.

## Runtime Model

Behavior Trees are tick-based AI graphs. Each node returns `Success`, `Failure`, or `Running`.

```text
.btree.json -> .btgraph -> BehaviorTreeCompiledAsset -> BehaviorTreeRunner -> BehaviorTreeRuntime
```

Use `.btree.json` as the source of truth. `.btgraph` is a Graph Toolkit editor/cache asset. `BehaviorTreeCompiledAsset` is the runtime asset assigned to `BehaviorTreeRunner.compiledBehaviorTree`.

`BehaviorTreeRunner` supports `Update`, `FixedUpdate`, `Manual`, and `Interval` tick modes. `Update` ticks through `maxTickRate`, `Interval` ticks through `intervalSeconds`, and `Manual` expects callers to invoke `ManualTick(deltaTime)`.

`BehaviorTreeRunner.playOnStart` starts the tree from Unity `Start`. `BehaviorTreeRunner.restartOnEnable` is disabled by default and is intended for pooled AI owners or other GameObjects that repeatedly toggle active state. When enabled, `OnEnable` starts a fresh runtime if the runner is not already running, so Blackboard defaults and runner overrides are reapplied before the next tick. `OnDisable` always stops the current tree; the next enable only restarts it when `restartOnEnable` is enabled. In `Manual` tick mode, `restartOnEnable` initializes the runtime but callers still drive execution through `ManualTick(deltaTime)`.

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
NavMeshPath
Blueprint
BlueprintRef
Array<string>
RoadAgentMask
RoadLaneAdjacentSide
RoadElementKind
RoadAgentState
RoadRouteState
RoadQueryFailureReason
VehicleRoadStopReason
VehicleRoadPassageStatus
VehicleRoadSignalState
VehicleRoadLaneChangeStatus
VehicleLaneRecoveryMode
```

Default values may store primitives, strings, vectors, `Array<string>`, enum names, and `Blueprint` asset paths. `GameObject`, `Transform`, `NavMeshPath`, and `BlueprintRef` values are runtime-only object values and must use `null` JSON defaults.

`BehaviorTreeRunner` exposes Blackboard overrides in the Inspector for compiled keys. `GameObject` and `Transform` overrides use `ObjectValue`; other keys use JSON text where possible, with plain string fallback for `string` and `Blueprint`. `NavMeshPath` keys are not exposed as Runner overrides because paths are runtime query results.

Vector resolution accepts `Vector3`, `Vector2`, `Transform`, `GameObject`, `Component`, or a three-item JSON array. A `Vector2` resolves to `(x, 0, y)`.

In the Behavior Tree Graph Toolkit, typed `int`, `float`, `bool`, `Vector2`, and `Vector3` value ports can be edited inline on the visual node when they are not connected to Blackboard variables. Generic/object value ports, destination key ports, and Blackboard key/reference ports remain Blackboard-only.

Runner Blackboard task nodes can read or write another `BehaviorTreeRunner` Blackboard. Their `target` and `sourceTarget` inputs read runtime objects from the current tree Blackboard and resolve `BehaviorTreeRunner`, `GameObject`, `Transform`, or `Component` values to a runner on the same GameObject. They do not use normal Blueprint binding names or scene-path strings. `sourceKey` and `targetKey` are string properties so remote Blackboard keys can use different names from the current tree keys.

## Current Node Summary

| Family | Type IDs | Purpose |
| --- | --- | --- |
| Composites | `BT.Root`, `BT.Selector`, `BT.Sequence`, `BT.Parallel`, `BT.RandomSelector`, `BT.PrioritySelector`, `BT.WeightedSelector` | Root entry, ordered child evaluation, parallel child polling, randomized selection, priority re-evaluation, and weighted selection. |
| Tasks | `BT.Wait`, `BT.SetBlackboard`, `BT.ClearBlackboard`, `BT.SetRunnerBlackboard`, `BT.GetRunnerBlackboard`, `BT.ClearRunnerBlackboard`, `BT.CopyRunnerBlackboard`, `BT.RunSubtree`, `BT.MoveTo`, `BT.StopNavigation`, `BT.SetNavigationDestination`, `BT.CalculateNavigationPath`, `BT.SetNavigationPath`, `BT.WaitForNavigation`, `BT.PauseNavigation`, `BT.ResumeNavigation`, `BT.SampleNavMeshPosition`, `BT.WarpNavigation`, `BT.TraverseOffMeshLink`, `BT.RotateTo`, `BT.TriggerBlueprintEvent`, `BT.RunBlueprintTask`, `BT.Log`, `BT.VehicleRoad.FindNearestLane`, `BT.VehicleRoad.FindLaneRoute`, `BT.VehicleRoad.SetFollowerRoute`, `BT.VehicleRoad.SelectNextRouteTarget`, `BT.VehicleRoad.ComputeFollowerControl`, `BT.VehicleRoad.DriveFollower`, `BT.VehicleRoad.UpdateTrafficState`, `BT.VehicleRoad.DecideLaneChange`, `BT.VehicleRoad.RequestLaneChange`, `BT.VehicleRoad.CompleteLaneChange`, `BT.VehicleRoad.UpdateFollowerSpeed`, `BT.VehicleRoad.EvaluateStopPointTravel`, `BT.VehicleRoad.ApplyStopPoint`, `BT.VehicleRoad.CheckFollowerRouteEnd`, `BT.VehicleRoad.MoveAlongBakedRoute`, `BT.VehicleRoad.MoveTowardLookAhead`, `BT.VehicleRoad.CaptureLoopStart`, `BT.VehicleRoad.TickLoopReset`, `BT.VehicleRoad.UnregisterVehicle` | Basic actions, Blackboard mutation, subtree execution, navigation, rotation, Blueprint event bridging, async task polling, VehicleRoads decision/control-output publication, route/traffic strategy, and optional kinematic VehicleRoad movement. |
| Decorators | `BT.BlackboardCondition`, `BT.CompareFloat`, `BT.CompareBool`, `BT.ObjectIsSet`, `BT.DistanceLessThan`, `BT.Cooldown`, `BT.NavigationCondition` | Branch guards evaluated before ticking the attached node. |
| Services | `BT.UpdateDistance`, `BT.UpdateNavigationState`, `BT.PerceptionSphere`, `BT.PerceptionRaycast`, `BT.SetBlackboardFromBlueprint`, `BT.TriggerBlueprintService`, `BT.VehicleRoad.UpdateRoadAgent` | Periodic updates while the owning node is active. |

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

### `BT.SetRunnerBlackboard`

Writes a value to another `BehaviorTreeRunner` Blackboard and returns `Success`.

```json
{
  "id": "share_target",
  "typeId": "BT.SetRunnerBlackboard",
  "inputs": {
    "target": "FriendRunner"
  },
  "properties": {
    "sourceKey": "EnemyTarget",
    "targetKey": "Target"
  }
}
```

This example writes `current.Blackboard["EnemyTarget"]` to `friendRunner.Blackboard["Target"]`.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `target` | input | `BehaviorTreeRunner`-compatible value | required | Current Blackboard value that resolves to `BehaviorTreeRunner`, `GameObject`, `Transform`, or `Component`. |
| `value` | input | any Blackboard value | none | Optional direct value source. When connected, it overrides `sourceKey`. |
| `sourceKey` | property | current Blackboard key name | none | Used only when `value` is not connected. |
| `targetKey` | property | target runner Blackboard key name | required | Remote destination key; it does not need to be declared in the current tree. |

Returns `Failure` if `target` cannot resolve, the target runner has no initialized Blackboard, `targetKey` is missing, or no value source resolves.

### `BT.GetRunnerBlackboard`

Reads another `BehaviorTreeRunner` Blackboard value and writes it into the current tree Blackboard.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `target` | input | `BehaviorTreeRunner`-compatible value | required | Current Blackboard value that resolves to the remote runner. |
| `sourceKey` | property | target runner Blackboard key name | required | Remote source key. |
| `targetKey` | property | current Blackboard key name | required | Local destination key; it must be declared in the current tree. |

Returns `Failure` if the runner cannot resolve, the remote Blackboard is not initialized, either key is missing, `targetKey` is not declared locally, or the remote `sourceKey` does not exist.

### `BT.ClearRunnerBlackboard`

Sets one key on another `BehaviorTreeRunner` Blackboard to `null`.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `target` | input | `BehaviorTreeRunner`-compatible value | required | Current Blackboard value that resolves to the remote runner. |
| `targetKey` | property | target runner Blackboard key name | required | Remote key to clear. |

Returns `Failure` if the runner cannot resolve, the remote Blackboard is not initialized, or `targetKey` is missing.

### `BT.CopyRunnerBlackboard`

Copies a Blackboard value from the current tree or a source runner to a target runner.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `sourceTarget` | input | `BehaviorTreeRunner`-compatible value | current tree | Optional source runner. When omitted, `sourceKey` reads from the current tree Blackboard. |
| `target` | input | `BehaviorTreeRunner`-compatible value | required | Target runner to write. |
| `sourceKey` | property | source Blackboard key name | required | Source key on `sourceTarget` or the current tree. |
| `targetKey` | property | target runner Blackboard key name | required | Remote destination key. |

Returns `Failure` if a configured runner cannot resolve, a runner Blackboard is not initialized, either key is missing, or `sourceKey` does not exist on its source Blackboard.

These `BT.*RunnerBlackboard` nodes are Behavior Tree runtime nodes. They are separate from the normal Blueprint `BehaviorTree.GetBlackboard*`, `BehaviorTree.SetBlackboard*`, and `BehaviorTree.ClearBlackboard` bridge nodes under `Assets/BlueprintSystem/Specs/Nodes/`.

### `BT.RunSubtree`

Runs another compiled Behavior Tree asset from the current tree and returns the child tree status.

```json
{
  "id": "run_patrol_subtree",
  "typeId": "BT.RunSubtree",
  "children": [],
  "properties": {
    "behaviorTree": "EnemyPatrolBehavior.btree.json",
    "blackboardMode": "Shared",
    "inputMappings": [],
    "outputMappings": []
  }
}
```

`behaviorTree` may reference an `Assets/...` path, a `Packages/...` path, a path relative to the parent `.btree.json`, or a `.compiled.asset` whose source path resolves back to a `.btree.json` / `.btree` asset. The editor compiler recursively compiles this child tree and stores the `BehaviorTreeCompiledAsset` reference in the parent compiled asset's component list, keyed by the `BT.RunSubtree` node id. Runtime execution resolves that component reference; it does not load source JSON or asset paths.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `behaviorTree` | property | Behavior Tree asset path | required | Source `.btree.json` / `.btree`, relative path, package/project path, or `.compiled.asset`. |
| `blackboardMode` | property | `Shared` or `Isolated` | `Shared` | Controls whether the child tree uses the parent Blackboard or its own Blackboard. |
| `inputMappings` | property | array | `[]` | `Isolated` only. Each item maps parent `sourceKey` to child `targetKey` before each child tick. |
| `outputMappings` | property | array | `[]` | `Isolated` only. Each item maps child `sourceKey` to parent `targetKey` after each child tick. |

In `Shared` mode, the child runtime uses the parent `BehaviorTreeBlackboard`. Missing child Blackboard schema entries are merged into the parent Blackboard with their child defaults. If parent and child declare the same key with different types, editor compilation fails.

In `Isolated` mode, the child runtime owns a separate Blackboard initialized from the child tree schema. `inputMappings` copy parent values into the child before every tick, and `outputMappings` copy child values back into the parent after every tick. Mapping entries use this shape:

```json
{ "sourceKey": "Target", "targetKey": "Target" }
```

The child runtime is created on the first tick and reused while it returns `Running`. When the child returns `Success` or `Failure`, `BT.RunSubtree` returns that same status and clears its cached runtime so the next start begins at the child root. Aborting the parent node stops the child runtime.

The editor compiler rejects missing subtree assets, subtree cycles, invalid `blackboardMode`, incompatible shared Blackboard key types, and isolated mappings whose source or target keys do not exist.

### `BT.MoveTo`

Moves the owner toward a target and returns `Running` until it reaches the target. Uses `NavMeshAgent` when one is enabled and on the NavMesh; otherwise it can move the owner transform directly. NavMesh movement succeeds only after the agent reports a complete path and the remaining distance is within radius; partial or invalid NavMesh paths return `Failure`.

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

### `BT.StopNavigation`

Clears the owner `NavMeshAgent` path and returns `Success`. This task is useful when a higher-priority branch interrupts movement and the tree should explicitly cancel the current NavMesh destination instead of only stopping the agent.

It returns `Failure` when the owner is missing or the owner does not have an enabled `NavMeshAgent` on the NavMesh.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `stopAgent` | input/property | bool | `true` | When true, sets `NavMeshAgent.isStopped` after clearing the path. `BT.MoveTo` will set it back to false when it issues a new destination. |

### Composable NavMesh Tasks

The following tasks expose the intermediate `NavMeshAgent` states that `BT.MoveTo` normally owns internally. A typical precomputed-path sequence is:

```text
BT.CalculateNavigationPath -> BT.SetNavigationPath -> BT.WaitForNavigation
```

Unless stated otherwise, these tasks return `Failure` when the owner is missing, the `NavMeshAgent` is missing or disabled, or the operation requires an agent on the NavMesh and `isOnNavMesh` is false.

### `BT.SetNavigationDestination`

Calls `NavMeshAgent.SetDestination` and returns immediately. `Success` means the destination request was accepted; path calculation may remain pending for later ticks.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `target` | input | Vector3-compatible value | required | Preferred destination source. |
| `targetKey` | property | Blackboard key name | none | Legacy/fallback destination source. |
| `targetPosition` | input/property | Vector3 | none | Direct destination fallback. |

### `BT.CalculateNavigationPath`

Synchronously calculates a path with the owner agent and writes the resulting runtime `NavMeshPath` to Blackboard. The destination key must be declared with type `NavMeshPath`.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `target` | input | Vector3-compatible value | required | Preferred destination source. |
| `targetKey` | property | Blackboard key name | none | Legacy/fallback destination source. |
| `targetPosition` | input/property | Vector3 | none | Direct destination fallback. |
| `pathKey` | property | `NavMeshPath` Blackboard key name | required | Receives the calculated path, including invalid results. |
| `allowPartial` | input/property | bool | `false` | Allows `PathPartial` to return `Success`; `PathInvalid` always fails. |

Path calculation is synchronous. Avoid evaluating large numbers of candidates in the same frame.

### `BT.SetNavigationPath`

Assigns a precomputed path with `NavMeshAgent.SetPath`.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `path` | input | `NavMeshPath` Blackboard value | required | Preferred path source; this port is Blackboard-only and has no inline value. |
| `pathKey` | property | `NavMeshPath` Blackboard key name | none | Legacy/fallback path source. |
| `allowPartial` | input/property | bool | `false` | Allows assigning `PathPartial`; `PathInvalid` always fails. |

### `BT.WaitForNavigation`

Waits for the current agent path to finish. Returns `Running` while the path is pending, paused, or moving; returns `Success` after a complete path reaches the arrival threshold; returns `Failure` for no path, stale path, partial path, or invalid path.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `acceptableRadius` | input/property | float | `0.25` | Combined with `agent.stoppingDistance`; the larger value is used. |
| `velocityThreshold` | input/property | float | `0.05` | Arrival also requires velocity magnitude at or below this value. |

### `BT.PauseNavigation`

Sets `NavMeshAgent.isStopped = true` without clearing the path and returns `Success`.

### `BT.ResumeNavigation`

Sets `NavMeshAgent.isStopped = false` so the agent continues along its retained path and returns `Success`.

### `BT.SampleNavMeshPosition`

Samples the nearest position with a `NavMeshQueryFilter` built from the owner agent type and selected area mask. It requires an enabled agent but does not require the agent to already be on the NavMesh.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `source` | input | Vector3-compatible value | owner position | Preferred sample origin. |
| `sourceKey` | property | Blackboard key name | none | Legacy/fallback sample origin. |
| `sourcePosition` | input/property | Vector3 | owner position | Direct origin fallback. |
| `maxDistance` | input/property | float | `agent.height * 2` | Search radius when the value is not connected or stored. |
| `areaMask` | input/property | int | `-1` | Areas allowed by the query filter; `-1` includes all areas. |
| `positionKey` | property | `Vector3` Blackboard key name | required | Receives the sampled position. |
| `areaMaskKey` | property | `int` Blackboard key name | none | Optional destination for the sampled hit mask. |

### `BT.WarpNavigation`

Calls `NavMeshAgent.Warp` and returns `Success` when the target is accepted. It requires an enabled agent but can be used when the agent is currently off the NavMesh. The executor does not call `ResetPath`.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `target` | input | Vector3-compatible value | required | Preferred warp destination. |
| `targetKey` | property | Blackboard key name | none | Legacy/fallback destination. |
| `targetPosition` | input/property | Vector3 | none | Direct destination fallback. |

### `BT.TraverseOffMeshLink`

Runs a custom traversal for the current OffMeshLink. It disables automatic traversal while active, moves to the link end, calls `CompleteOffMeshLink`, restores the previous `autoTraverseOffMeshLink` value, and returns `Success`.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `mode` | input/property | `BehaviorTreeOffMeshLinkTraversalMode` | `Linear` | `Teleport`, `Linear`, or `Parabola`. |
| `duration` | input/property | float | `0.5` | Linear/parabolic traversal duration in seconds. |
| `height` | input/property | float | `1` | Parabolic vertical height. |

`Teleport` completes in one tick. `Linear` and `Parabola` return `Running` until `duration` elapses. Aborting a running traversal returns the transform to the stored link start, restores automatic traversal, clears the path, and stops the agent.

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

### VehicleRoad Tasks

Most VehicleRoad tasks are decision/control-output nodes only. They write Blackboard keys and never move the owner `Transform`. The explicit kinematic exceptions are `BT.VehicleRoad.DriveFollower` and the smaller follower movement tasks documented below.

`BT.VehicleRoad.FindNearestLane` resolves `subsystem` from a Blackboard input, reads `position`, `heading`, `agentMask`, `maxDistance`, and `maxHeightDifference`, then writes `foundKey`, `laneIdKey`, `positionKey`, `forwardKey`, `upKey`, `distanceAlongLaneKey`, and `distanceToLaneKey`. It returns `Success` only when a lane is found.

`BT.VehicleRoad.FindLaneRoute` resolves `subsystem`, reads `startLaneId`, `destinationLaneId`, and `agentMask`, then writes `successKey`, `routeLaneIdsKey: Array<string>`, and `totalCostKey`. It returns `Success` only when a same-network route exists.

`BT.VehicleRoad.SetFollowerRoute` resolves a `VehicleLaneFollower` from `follower` or the owner GameObject, reads `laneIds: Array<string>` or a comma-separated string, calls `VehicleLaneFollower.SetRoute`, writes `successKey`, and returns `Failure` when no follower is available.

`BT.VehicleRoad.SelectNextRouteTarget` resolves `subsystem` directly or through a `VehicleLaneFollower`, reads `currentLaneId`, `candidateLaneIds: Array<string>`, `agentMask`, `selectionMode` (`First`, `Cycle`, or `Random`), and `previousIndex`. It writes `successKey`, `destinationLaneIdKey`, `selectedIndexKey`, `routeLaneIdsKey: Array<string>`, and `totalCostKey`. It returns `Success` for the first reachable candidate and `Failure` when no candidate route exists.

`BT.VehicleRoad.ComputeFollowerControl` resolves a `VehicleLaneFollower` from the `follower` Blackboard input or from the owner GameObject. It reads vehicle pose/control inputs and writes `validKey`, lane pose, target steering/speed, recovery, stop/signal/queue, and lane-change result keys. It returns `Success` only when the follower output is valid. Movement remains owned by an external vehicle executor.

`BT.VehicleRoad.DriveFollower` is the wrapper task for the sample `VehicleRoadTestVehicle` behavior. It resolves a `VehicleLaneFollower` from the `follower` Blackboard input or from the owner GameObject, computes follower control using the owner pose and an internal `currentSpeed`, writes the same output keys as `BT.VehicleRoad.ComputeFollowerControl`, and moves the owner `Transform` kinematically. It returns `Failure` if no follower exists or if the follower output is invalid while `loopRoute` is false, returns `Success` when `followBakedLanePose` reaches the end of a non-loop route, and returns `Running` while driving, waiting at an explicit stop point, or performing loop reset delay.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `follower` | input | VehicleLaneFollower-compatible object | owner component | Optional Blackboard input; owner fallback is used when omitted. |
| `vehicleId` | input/property | string | auto id | Stable id for traffic control; an empty value becomes a per-owner runtime id. |
| `vehicleLength` | input/property | float | `4.5` | Clamped to at least `0.1`. |
| `wheelBase` | input/property | float | `0` | `0` derives `vehicleLength * 0.55`; positive values are clamped to at least `0.1`. |
| `acceleration` | input/property | float | `6` | Speed change rate in units per second. |
| `turnSpeed` | input/property | float | `180` | Degrees per second for fallback look-at steering. |
| `agentMask` | input/property | RoadAgentMask | `Car` | `None` normalizes to `Car`. |
| `followBakedLanePose` | input/property | bool | `false` | When true, samples route pose from `VehicleLaneFollower.TryEvaluateRoutePose`; otherwise rotates toward `lookAheadPoint` and moves forward. |
| `stopPointApproachSpeed` | input/property | float | `2` | Minimum approach speed while clamping to an explicit stop point. |
| `loopRoute` | input/property | bool | `false` | Invalid output or route end waits for `loopResetDelay`, unregisters the vehicle, and restores the first tick pose. |
| `loopResetDelay` | input/property | float | `2` | Minimum reset delay is `0.1`. |
| `leadVehicleDistance` / `leadVehicleSpeed` | input/property | float | `0` | Optional follower speed limiting inputs. |
| `requestLaneChange` | input/property | bool | `false` | Requests a lane change through the follower's subsystem-backed traffic layer. |
| `requestedLaneChangeSide` | input/property | RoadLaneAdjacentSide | `Right` | Side used when `requestLaneChange` is true. |
| `unregisterOnAbort` | input/property | bool | `true` | Calls `VehicleRoadSubsystem.UnregisterVehicle` through the follower on abort when a vehicle id is known. |

Additional output key properties:

| Property | Blackboard Type | Notes |
| --- | --- | --- |
| `currentSpeedKey` | float | Current internal speed after acceleration/deceleration. |
| `arrivedKey` | bool | True when a baked-pose route end is reached. |
| `loopResetKey` | bool | True only on the tick that performs loop reset. |

Use the split follower tasks when a tree should customize the `VehicleRoadTestVehicle` steps instead of using the wrapper. A typical sequence is `BT.VehicleRoad.SelectNextRouteTarget` -> `BT.VehicleRoad.SetFollowerRoute` -> `BT.VehicleRoad.ComputeFollowerControl` -> `BT.VehicleRoad.UpdateTrafficState` -> `BT.VehicleRoad.EvaluateLaneChangeRoute` -> optional `BT.VehicleRoad.RequestLaneChange` when `requestLaneChangeKey` is true -> `BT.VehicleRoad.UpdateFollowerSpeed` -> `BT.VehicleRoad.EvaluateStopPointTravel`, followed by a Selector that tries `BT.VehicleRoad.ApplyStopPoint`, `BT.VehicleRoad.MoveAlongBakedRoute`, then `BT.VehicleRoad.MoveTowardLookAhead`. `BT.VehicleRoad.DecideLaneChange` remains the lighter lead-vehicle policy node; `BT.VehicleRoad.EvaluateLaneChangeRoute` is the route-level policy for missing, closed, unsafe, congested, or full next lanes. Route-end and invalid-output branches can call `BT.VehicleRoad.TickLoopReset` after `BT.VehicleRoad.CaptureLoopStart` has saved the loop origin.

| Task | Main Inputs | Outputs | Status |
| --- | --- | --- | --- |
| `BT.VehicleRoad.UpdateFollowerSpeed` | `valid`, `currentSpeed`, `targetSpeed`, `acceleration`, `deltaTime` | `currentSpeedKey: float` | Always `Success`; invalid output decelerates toward zero. |
| `BT.VehicleRoad.EvaluateStopPointTravel` | `hasStopPoint`, `distanceToStopLine`, `targetSpeed`, `currentSpeed`, `stopPointApproachSpeed`, `deltaTime` | `requestedTravelDistanceKey: float`, `travelDistanceKey: float`, `reachedStopPointKey: bool` | Always `Success`. |
| `BT.VehicleRoad.ApplyStopPoint` | `reachedStopPoint`, `stopPoint` | `currentSpeedKey: float` | `Success` when it applies the stop point, `Failure` when no stop was reached. |
| `BT.VehicleRoad.CheckFollowerRouteEnd` | `follower`, `currentLaneId`, `distanceAlongLane`, `followBakedLanePose` | `arrivedKey: bool` | `Success` only when `VehicleLaneFollower.IsAtRouteEnd` is true. |
| `BT.VehicleRoad.MoveAlongBakedRoute` | `follower`, `currentLaneId`, `distanceAlongLane`, `travelDistance`, `followBakedLanePose` | none | `Success` when route pose evaluation moves owner, otherwise `Failure` for fallback. |
| `BT.VehicleRoad.MoveTowardLookAhead` | `lookAheadPoint`, `travelDistance`, `turnSpeed`, `deltaTime` | none | `Success` after fallback look-at movement; `Failure` without owner or target. |
| `BT.VehicleRoad.CaptureLoopStart` | owner transform | `loopStartPositionKey: Vector3`, `loopStartEulerAnglesKey: Vector3`, `loopStartCapturedKey: bool` | `Success` when owner exists. |
| `BT.VehicleRoad.TickLoopReset` | `follower`, `vehicleId`, `loopRoute`, `resetRequested`, `loopResetDuration`, `loopResetDelay`, `loopStartPosition`, `loopStartEulerAngles`, `deltaTime`, `unregisterOnReset` | `loopResetDurationKey: float`, `loopResetKey: bool`, `currentSpeedKey: float` | `Failure` when no reset is requested, `Running` while waiting, `Success` on reset. |
| `BT.VehicleRoad.UnregisterVehicle` | `subsystem`, `follower`, `vehicleId` | none | `Success` only when a subsystem can be resolved and the vehicle id is registered. |

Traffic and lane-change strategy tasks expose the traffic-layer portions of `VehicleRoadTestVehicle` / `VehicleLaneFollower` so a tree can reason about the next behavior before movement:

| Task | Main Inputs | Outputs | Status |
| --- | --- | --- | --- |
| `BT.VehicleRoad.UpdateTrafficState` | `subsystem`, `follower`, `vehicleId`, `laneId`, `agentMask`, `distanceAlongLane`, `speed`, `vehicleLength`, `routeLaneIds`, `leadVehicleSearchDistance` | `updatedKey: bool`, `leadVehicleFoundKey: bool`, `leadVehicleIdKey: string`, `leadVehicleLaneIdKey: string`, `leadVehicleDistanceKey: float`, `leadVehicleSpeedKey: float`, `leadVehicleLengthKey: float` | `Success` after publishing the vehicle state; `Failure` without subsystem, vehicle id, or lane id. |
| `BT.VehicleRoad.DecideLaneChange` | `leadVehicleFound`, `leadVehicleDistance`, `leadVehicleSpeed`, `currentSpeed`, `hasStopPoint`, `distanceToStopLine`, `recoveryMode`, `laneChangeStatus`, `minLeadDistance`, `minSpeedAdvantage`, `blockWhenStopping`, `preferredSide`, `allowActiveRequest` | `requestLaneChangeKey: bool`, `requestedLaneChangeSideKey: RoadLaneAdjacentSide`, `laneChangeDecisionReasonKey: string` | Always `Success`; writes a request only when a slower lead vehicle blocks progress and no stop/recovery/active request blocks the attempt. |
| `BT.VehicleRoad.EvaluateLaneOccupancy` | `subsystem`, `follower`, `vehicleId`, `laneId`, `distanceAlongLane`, `agentMask`, `vehicleLength`, `lookAheadDistance`, `requiredGap`, `maxOccupancyRatio` | `validKey: bool`, `statusKey: VehicleRoadLaneOccupancyStatus`, `isEnterableKey: bool`, vehicle/reservation counts, nearest forward/rear ids and gaps, `failureReasonKey: string` | `Success` when the lane query is valid, `Failure` for missing subsystem or invalid lane. This is read-only and does not reserve lane space. |
| `BT.VehicleRoad.EvaluateLaneChangeRoute` | `subsystem`, `follower`, `vehicleId`, `currentLaneId`, `destinationLaneId`, `currentRouteLaneIds`, `distanceAlongLane`, `agentMask`, `vehicleLength`, `preferredSide`, `allowOppositeSide`, occupancy thresholds, `laneChangeStatus`, `recoveryMode`, stop-point guard inputs | `requestLaneChangeKey: bool`, `requestedLaneChangeSideKey: RoadLaneAdjacentSide`, `targetLaneIdKey: string`, `targetRouteLaneIdsKey: Array<string>`, `decisionReasonKey: VehicleRoadLaneChangeDecisionReason`, `failureReasonKey: string`, `currentOccupancyStatusKey` and `targetOccupancyStatusKey: VehicleRoadLaneOccupancyStatus` | `Success` means evaluation completed, not that a lane change is required. It defaults to blocking duplicate active requests, recovery mode, and near-stop-point requests unless the corresponding allow inputs are true. |
| `BT.VehicleRoad.RequestLaneChange` | `subsystem`, `follower`, `vehicleId`, `side` | `laneChangeStatusKey: VehicleRoadLaneChangeStatus`, `laneChangeTargetLaneIdKey: string`, `laneChangeReservedDistanceKey: float`, `laneChangeFailureReasonKey: string` | `Success` for requested/granted/active/completed states, `Failure` for denied/missing subsystem/id. |
| `BT.VehicleRoad.CompleteLaneChange` | `subsystem`, `follower`, `vehicleId` | `completedKey: bool` | `Success` only when the subsystem removes an active lane-change reservation. |

Recommended route-level lane-change flow: compute follower output, publish traffic state, evaluate lane-change route, request the returned side only when `requestLaneChangeKey` is true, compute follower control again with `requestLaneChange=true` and the requested side so the follower receives a lane-change target point, then after the vehicle reports `currentLaneId == laneChangeTargetLaneId`, call `SetFollowerRoute` with `targetRouteLaneIdsKey` and `CompleteLaneChange`.

## Decorator Nodes

Decorators are evaluated before the attached tree node ticks. If any decorator returns false, the node returns `Failure`.

Condition nodes in the visual graph are Decorators, not ordinary tree children. A Decorator that exists in the graph-level `decorators` array but is not referenced by any tree node is serialized but never evaluated at runtime.

Current runtime re-evaluates attached decorators every tick. Decorators do not have per-node abort mode settings.

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

### `BT.NavigationCondition`

Evaluates the owner `NavMeshAgent`. `AgentAvailable` checks for an enabled component, `IsOnNavMesh` also checks placement, and all other conditions evaluate false when the agent is unavailable or off the NavMesh.

| Parameter | Source | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `condition` | input/property | `BehaviorTreeNavigationCondition` | `AgentAvailable` | State to evaluate. |
| `invert` | input/property | bool | `false` | Inverts the final result. |
| `acceptableRadius` | input/property | float | `0.25` | Used by `HasArrived` with `agent.stoppingDistance`. |
| `velocityThreshold` | input/property | float | `0.05` | Used by `IsMoving` and `HasArrived`. |

Supported conditions:

```text
AgentAvailable
IsOnNavMesh
HasPath
PathPending
PathComplete
PathPartial
PathInvalid
IsStopped
IsMoving
HasArrived
IsPathStale
IsOnOffMeshLink
```

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

### `BT.UpdateNavigationState`

Writes selected `NavMeshAgent` state values to declared Blackboard keys. Every destination property is optional. `acceptableRadius` and `velocityThreshold` use the same arrival semantics as `BT.NavigationCondition` and `BT.WaitForNavigation`.

| Property | Blackboard Type | Invalid-agent value |
| --- | --- | --- |
| `agentAvailableKey` | bool | `false` when the component is missing or disabled |
| `isOnNavMeshKey` | bool | `false` |
| `hasPathKey` | bool | `false` |
| `pathPendingKey` | bool | `false` |
| `pathStatusKey` | string | `PathInvalid` |
| `remainingDistanceKey` | float | `Infinity` |
| `velocityKey` | Vector3 | `(0, 0, 0)` |
| `destinationKey` | Vector3 | `(0, 0, 0)` |
| `isStoppedKey` | bool | `true` |
| `isMovingKey` | bool | `false` |
| `hasArrivedKey` | bool | `false` |
| `isPathStaleKey` | bool | `false` |
| `isOnOffMeshLinkKey` | bool | `false` |

The service also accepts `acceptableRadius` with default `0.25` and `velocityThreshold` with default `0.05`.

### `BT.VehicleRoad.UpdateRoadAgent`

Evaluates a `RoadAgent` service and writes target, recovery, arrival, and failure state to Blackboard. It resolves the agent from `agentKey` or from the owner GameObject, reads `positionKey`, `forwardKey`, `speedKey`, and `deltaTimeKey` when configured, and falls back to owner transform plus `context.DeltaTime`.

| Property | Blackboard Type |
| --- | --- |
| `validKey` | bool |
| `agentStateKey` | RoadAgentState |
| `routeStateKey` | RoadRouteState |
| `failureReasonKey` | RoadQueryFailureReason |
| `currentElementKindKey` | RoadElementKind |
| `currentElementIdKey` | string |
| `routeSegmentIndexKey` | int |
| `targetPositionKey` | Vector3 |
| `targetForwardKey` | Vector3 |
| `targetUpKey` | Vector3 |
| `targetSpeedKey` | float |
| `remainingDistanceKey` | float |
| `distanceToBoundaryKey` | float |
| `arrivedKey` | bool |
| `shouldRecoverKey` | bool |
| `recoveryPositionKey` | Vector3 |

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
BTTaskSetRunnerBlackboardNode
BTTaskGetRunnerBlackboardNode
BTTaskClearRunnerBlackboardNode
BTTaskCopyRunnerBlackboardNode
BTTaskRunSubtreeNode
BTTaskMoveToNode
BTTaskStopNavigationNode
BTTaskSetNavigationDestinationNode
BTTaskCalculateNavigationPathNode
BTTaskSetNavigationPathNode
BTTaskWaitForNavigationNode
BTTaskPauseNavigationNode
BTTaskResumeNavigationNode
BTTaskSampleNavMeshPositionNode
BTTaskWarpNavigationNode
BTTaskTraverseOffMeshLinkNode
BTTaskRotateToNode
BTTaskTriggerBlueprintEventNode
BTTaskRunBlueprintTaskNode
BTTaskLogNode
BTTaskVehicleRoadFindNearestLaneNode
BTTaskVehicleRoadFindLaneRouteNode
BTTaskVehicleRoadSetFollowerRouteNode
BTTaskVehicleRoadSelectNextRouteTargetNode
BTTaskVehicleRoadComputeFollowerControlNode
BTTaskVehicleRoadDriveFollowerNode
BTTaskVehicleRoadUpdateTrafficStateNode
BTTaskVehicleRoadDecideLaneChangeNode
BTTaskVehicleRoadRequestLaneChangeNode
BTTaskVehicleRoadCompleteLaneChangeNode
BTTaskVehicleRoadUpdateFollowerSpeedNode
BTTaskVehicleRoadEvaluateStopPointTravelNode
BTTaskVehicleRoadApplyStopPointNode
BTTaskVehicleRoadCheckFollowerRouteEndNode
BTTaskVehicleRoadMoveAlongBakedRouteNode
BTTaskVehicleRoadMoveTowardLookAheadNode
BTTaskVehicleRoadCaptureLoopStartNode
BTTaskVehicleRoadTickLoopResetNode
BTTaskVehicleRoadUnregisterVehicleNode
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
BTDecoratorNavigationConditionNode
```

`BehaviorTreeVisualNode` is the compatibility fallback for older or unknown serialized tree nodes. `BehaviorTreeVisualDecoratorNode` is both the create-menu fallback Decorator node and the compatibility fallback for older or unknown serialized Decorator nodes. When created directly, it defaults to `BT.BlackboardCondition`; prefer the dedicated `BTDecorator*` visual node classes for the other built-in Decorator types. Decorators connect to tree nodes through the `Conditions` input and are still stored in graph-level `Decorators` plus each tree node's attached Decorator id list. Services remain edited as attached id lists on tree nodes and stored in graph-level `Services`.

Built-in task parameters are exposed as Blackboard input ports. Built-in Decorator value parameters are exposed as condition node input ports. Export stores connected Blackboard variable nodes for tree task inputs in `node.inputs` and Decorator inputs in `decorator.inputs`, both as `inputId -> blackboard key`. Inline task and Decorator values are exported to `properties` only for ports that allow inline values and differ from defaults. Decorator operator ports use enum fields and export their enum names to `properties.operator`.

For `BT.SetRunnerBlackboard`, `BT.GetRunnerBlackboard`, `BT.ClearRunnerBlackboard`, and `BT.CopyRunnerBlackboard`, visual `target`, `sourceTarget`, and `value` are Blackboard value ports. `sourceKey` and `targetKey` remain string fields in `PropertiesJson`; they are not visual Blackboard input bindings, because remote runner keys may use names that are not declared in the current tree.

For `BT.RunSubtree`, the visual node title and a `Subtree` ObjectField on the visual node show the referenced Behavior Tree asset. The same `Subtree` asset field and `Open` jump button are also editable from the Graph Inspector. Export writes the selected asset path to `properties.behaviorTree`. `blackboardMode`, `inputMappings`, and `outputMappings` remain in node properties. The node is a Task and does not expose child ports.

`NavMeshPath` Blackboard variables are Graph Toolkit connection values with no inline default. `BT.CalculateNavigationPath.pathKey`, `BT.SampleNavMeshPosition.positionKey` / `areaMaskKey`, and `BT.UpdateNavigationState` destination keys remain explicit properties because they name Blackboard entries that the node writes.

## Validation Rules

The validator enforces:

- Blackboard keys must have unique names and known types.
- `BlueprintRef`, `GameObject`, `Transform`, and `NavMeshPath` Blackboard keys cannot store JSON object reference defaults.
- Each behavior tree must contain exactly one `BT.Root` node.
- The top-level `root` field must reference the `BT.Root` node.
- `BT.Root` must have exactly one child.
- Composite nodes must have at least one child.
- Task nodes cannot have children.
- Child, decorator, service, property-key, and input-key references must exist.
- The tree cannot contain cycles.
- `BT.MoveTo` needs `target`, `targetKey`, or `targetPosition`.
- `BT.SetNavigationDestination`, `BT.CalculateNavigationPath`, and `BT.WarpNavigation` need `target`, `targetKey`, or `targetPosition`.
- `BT.CalculateNavigationPath.pathKey` and `BT.SetNavigationPath.path` / `pathKey` must reference `NavMeshPath` Blackboard keys.
- `BT.SampleNavMeshPosition` needs a `Vector3` `positionKey`; optional `areaMaskKey` must be `int`.
- `BT.UpdateNavigationState` validates every configured output key against its documented Blackboard type.
- `BT.VehicleRoad.*` task and service output keys validate against their documented Blackboard types, including `Array<string>` route keys and VehicleRoads enum keys.
- `BT.SetBlackboard` and `BT.ClearBlackboard` need `key`.
- `BT.*RunnerBlackboard` task nodes need a `target` input and their required `sourceKey`/`targetKey` properties. Remote key properties are not validated against the current tree, except `BT.GetRunnerBlackboard.targetKey`, which writes back into the current tree.
- `BT.RunSubtree` needs `behaviorTree`, accepts only `Shared` or `Isolated` `blackboardMode`, and validates parent-side isolated mapping keys in the source validator. The editor compiler also validates subtree paths, child-side mapping keys, shared Blackboard type compatibility, and subtree cycles.
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
