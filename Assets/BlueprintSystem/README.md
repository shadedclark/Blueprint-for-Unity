# BlueprintSystem Agent Guide

This document is optimized for coding agents maintaining the Unity blueprint system.

Primary rule:

```text
.blueprint.json is the source of truth.
.bpgraph is an editor visualization/cache.
Runtime code must not depend on UnityEditor or Graph Toolkit.
```

## 1. Mental Model

BlueprintSystem is a JSON-first blueprint runtime and editor bridge.

```text
Blueprint JSON
  -> Node Manifests
  -> Validator
  -> Compiler
  -> RuntimeBlueprint
  -> BlueprintVM
  -> C# Executors
  -> Unity objects
```

Blueprints may also declare Unreal-style runtime components. A component declaration names a child behavior/data blueprint asset; the parent `BlueprintRunner` creates an in-memory component instance at runtime, so no child `GameObject` is needed for pure logic modules.

Graph Toolkit is only an editor view:

```text
.blueprint.json <-> .bpgraph
```

If a task can be done by editing JSON, prefer JSON. Use `.bpgraph` only when the user specifically wants visual graph editing or inspection.

## 2. File Map

```text
Assets/BlueprintSystem/
  Runtime/
    BlueprintSource.cs              JSON source model
    BlueprintSourceMapper.cs        JSON <-> source model mapping
    BlueprintManifest.cs            node manifest model
    BlueprintCompiler.cs            source -> RuntimeBlueprint
    BlueprintValidator.cs           structural/type validation
    BlueprintVM.cs                  runtime execution loop
    BlueprintRunner.cs              MonoBehaviour runner
    BlueprintRuntimeComponent.cs    pure runtime component instance
    BlueprintRef.cs                 runtime-only blueprint instance handle
    UIBlueprintBinder.cs            UI binding resolver + panel events
    BlueprintExecutor.cs            executor interface + default registry

  Executors/
    Flow/FlowExecutors.cs
    Game/GameExecutors.cs
    UI/UIExecutors.cs
    Variables/VariableExecutors.cs

  Specs/
    Nodes/*.node.json               node manifests
    Schemas/*.schema.json           JSON schemas

  Sources/
    **/*.blueprint.json             authoritative blueprint files

  Editor/
    BlueprintEditorWindow.cs        simple JSON editor/validator
    GraphToolkit/                   .blueprint.json <-> .bpgraph bridge

  Tests/Editor/
    BlueprintSystemTests.cs         edit mode regression tests
```

## 3. Source-of-Truth Rules

Agents should follow these rules unless the user explicitly asks otherwise:

1. Modify `.blueprint.json` for blueprint logic.
2. Modify `.node.json` for node shape, ports, required properties, and executor binding.
3. Modify C# Executors for runtime behavior.
4. Modify Graph Toolkit visual node classes only for editor UX.
5. Do not make `.bpgraph` the only place where behavior exists.
6. Do not store Unity object references in JSON; store binding names.
7. Keep node IDs stable and readable.

Recommended node IDs:

```text
event_open
set_title
branch_has_item
bind_close_button
```

Avoid IDs generated from display text or random GUIDs unless there is no better stable name.

## 4. Blueprint JSON Contract

Blueprint files live under `Assets/BlueprintSystem/Sources/**`.

Minimal shape:

```json
{
  "schemaVersion": "0.1",
  "name": "InventoryPanel",
  "category": "UI",
  "description": "Panel behavior.",
  "variables": [],
  "bindings": [],
  "components": [],
  "nodes": [],
  "edges": []
}
```

Node shape:

```json
{
  "id": "set_title",
  "typeId": "UI.SetText",
  "position": [360, 100],
  "properties": {
    "target": "TitleText",
    "value": "Inventory"
  }
}
```

Edge shape:

```json
{
  "from": "event_open.execOut",
  "to": "set_title.execIn"
}
```

Binding shape:

```json
{
  "name": "TitleText",
  "type": "TMP_Text",
  "required": true
}
```

JSON must refer to Unity objects through binding names such as `TitleText`, not serialized Unity references.

Component shape:

```json
{
  "name": "AddItemBehavior",
  "blueprint": "Assets/Game/Blueprints/Inventory/Behavior/InventoryAddItemBehavior.blueprint.json",
  "required": true
}
```

Use components for owner-owned logic modules. Cross-blueprint access nodes accept either `Blueprint` asset path variables or runtime `BlueprintRef` handles as `target`, and asset paths resolve only inside the current runner/component tree. Use `Blueprint.GetOwner` and `Blueprint.GetComponent` to obtain runtime refs without creating child GameObjects. Keep `UIBinding<T>` for real Unity object or scene/UI component references; `Blueprint.GetByBinding` and `Blueprint.FindByTag` are not cross-blueprint resolvers.

Variable shape:

```json
{
  "name": "selectedItemId",
  "type": "string",
  "defaultValue": "",
  "scope": "runtime",
  "exposed": true,
  "persistent": false,
  "description": "Currently selected inventory item id."
}
```

## 5. Node Manifest Contract

Node manifests live in `Assets/BlueprintSystem/Specs/Nodes/*.node.json`.

Manifest fields:

```json
{
  "schemaVersion": "0.1",
  "typeId": "UI.SetText",
  "title": "Set Text",
  "category": "UI",
  "description": "Sets text on a bound TMP_Text element.",
  "executor": "UI.SetText",
  "inputs": [],
  "outputs": [],
  "properties": []
}
```

Port fields:

```json
{
  "id": "value",
  "kind": "value",
  "type": "string",
  "required": true,
  "source": "propertyOrConnection"
}
```

Supported `kind`:

```text
exec
value
```

Supported `source`:

```text
property
connection
propertyOrConnection
```

Current common value types:

```text
string
bool
int
float
Vector2
Vector3
Color
Blueprint
UIBinding<T>
```

`Blueprint` values store `.blueprint.json` asset paths as strings. They are configuration references, not runtime `BlueprintRef` instances or serialized Unity object references.

`BlueprintRef` is a runtime-only handle produced by nodes such as `Blueprint.GetOwner` and `Blueprint.GetComponent`. It can flow through node connections into `Blueprint.IsValid`, `Blueprint.TriggerEvent`, `Blueprint.GetVariable`, and `Blueprint.SetVariable`, but it is not a supported blackboard/default variable type and must not be persisted in `.blueprint.json`.

`UIBinding<T>` values are stored in JSON as strings and resolved at runtime by `IBlueprintBindingResolver`.

## 6. Runtime Executor Contract

Executors implement `IBlueprintNodeExecutor`, usually by inheriting `BlueprintNodeExecutor`.

Execution nodes override `Execute`:

```csharp
public sealed class GameSetTimeScaleExecutor : BlueprintNodeExecutor
{
    public override string ExecutorId
    {
        get { return "Game.SetTimeScale"; }
    }

    public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
    {
        float value = context.GetInputValue(node, "value", 1f);
        Time.timeScale = value;
        return BlueprintExecResult.Continue("execOut");
    }
}
```

Value nodes override `Evaluate`:

```csharp
public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
{
    if (outputPortId == "value")
    {
        return Time.deltaTime;
    }

    return null;
}
```

Every new executor must be registered in:

```text
Assets/BlueprintSystem/Runtime/BlueprintExecutor.cs
BlueprintExecutorRegistry.CreateDefault()
```

## 7. Common Agent Tasks

### Add a New Runtime Node

Follow this exact sequence:

1. Add a manifest in `Assets/BlueprintSystem/Specs/Nodes`.
2. Add or update an executor in `Assets/BlueprintSystem/Executors`.
3. Register the executor in `BlueprintExecutorRegistry.CreateDefault()`.
4. Add or update a sample `.blueprint.json`.
5. Add or update EditMode tests.
6. Optionally add a dedicated Graph Toolkit visual node.
7. Run validation/tests.

Consistency checks:

```text
manifest.typeId == blueprint node typeId
manifest.executor == executor.ExecutorId
manifest port IDs == context.GetInputValue(...) names
exec output IDs == BlueprintExecResult.Continue(...) names
```

### Add a Dedicated Graph Toolkit Visual Node

Use this when the user wants separate node types in Graph Toolkit, not just the generic `BlueprintVisualNode`.

Create a subclass under:

```text
Assets/BlueprintSystem/Editor/GraphToolkit/
```

Template:

```csharp
using System;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetText")]
    public sealed class UISetTextVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(
                "UI.SetText",
                "Set Text",
                "UI",
                "Sets text on a bound TMP_Text element.");

            AddExecInput("execIn");
            AddValueInput("target", "UIBinding<TMP_Text>", true, "property");
            AddValueInput("value", "string", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "UIBinding<TMP_Text>", true);
            AddProperty("value", "string", false);
        }
    }
}
```

Important:

```text
[BlueprintVisualNodeType("...")] must equal manifest.typeId.
SetIdentity typeId should equal manifest.typeId.
Ports/properties should mirror the manifest.
```

JSON import uses `BlueprintVisualNodeFactory`:

```text
typeId -> dedicated BlueprintVisualNode subclass
missing mapping -> generic BlueprintVisualNode
```

Manual nodes created inside Graph Toolkit need a stable `Node Id` in the Inspector before exporting back to JSON.

### Add a New Value Type

Update all relevant places:

```text
Runtime/BlueprintTypeUtility.cs
Editor/GraphToolkit/BlueprintVisualGraphData.cs
Tests/Editor/BlueprintSystemTests.cs
```

Usually update:

```text
BlueprintTypeUtility.IsCompatible
BlueprintTypeUtility.IsValueAssignableToType
BlueprintTypeUtility.ConvertValue
BlueprintVisualValueUtility.ToGraphType
BlueprintVisualValueUtility.ConvertForGraphField
BlueprintVisualValueUtility.ConvertFromGraphField
```

### Add a New UI Binding Type

Use a manifest property type like:

```json
{
  "id": "target",
  "type": "UIBinding<Button>",
  "required": true
}
```

Executor pattern:

```csharp
string target = context.GetInputValue(node, "target", string.Empty);
Button button = context.BindingResolver.Resolve<Button>(target);
```

`UIBlueprintBinder.Resolve<T>()` supports direct object resolution and `GetComponent<T>()` from `GameObject` or `Component`.

## 8. Graph Toolkit Workflow

Import JSON to visual graph:

```text
Select .blueprint.json
Tools/Blueprint System/Graph Toolkit/Import Selected Blueprint JSON
```

Export visual graph back to JSON:

```text
Select .bpgraph
Tools/Blueprint System/Graph Toolkit/Export Selected Blueprint Graph To JSON
```

JSON editor shortcut:

```text
Double-click .blueprint.json
or
Right-click .blueprint.json
Assets/Blueprint System/Open Blueprint JSON
or
Tools/Blueprint System/Blueprint JSON Editor
Open Selected
Visual Graph
```

Known Graph Toolkit bridge files:

```text
BlueprintVisualGraph.cs
BlueprintVisualNode.cs
BlueprintVisualNodeTypeAttribute.cs
BlueprintVisualNodeFactory.cs
BlueprintGraphToolkitBridge.cs
BlueprintGraphToolkitUIDragDrop.cs
BlueprintGraphToolkitBlackboardSync.cs
BlueprintGraphToolkitTypeRegistry.cs
```

Graph Toolkit is a preview package. Some bridge operations use reflection against internal Graph Toolkit APIs. Keep reflection isolated in the Graph Toolkit bridge helpers.

Variable workflow:

```text
Import .blueprint.json to .bpgraph
Blueprint variables appear in the Graph Toolkit Blackboard
Drag a Blackboard variable into the graph and choose Get or Set
Export .bpgraph back to .blueprint.json
```

Blackboard variables are an editor projection of `.blueprint.json` `variables[]`.
On import, valid `Variable.Get` JSON nodes are shown as native Blackboard variable nodes.
On export, supported native Blackboard variable nodes are written back as `Variable.Get` nodes.
Choosing Set from the Blackboard drag menu creates a `Variable.Set` node that writes to `context.Variables` at runtime. It does not edit the Blackboard default value.
Array variables use one Blackboard type named `Array`; choose the element type inside the array field and edit the default value as JSON text.
Blueprint-only metadata such as `exposed`, `persistent`, and `description` remains serialized in `.blueprint.json` and is preserved during graph export when already present.

Saving a `.bpgraph` in Graph Toolkit automatically exports the graph back to JSON on the next editor tick. The export target is:

```text
graph.SourceBlueprintAssetPath
fallback: same path as .bpgraph with .blueprint.json extension
```

The auto-export hook lives in `BlueprintGraphToolkitAutoExport.cs` and watches `.bpgraph` asset imports after Graph Toolkit saves the graph file.
If the `.bpgraph` file is older than the target `.blueprint.json`, auto-export skips it so stale visual caches do not overwrite newer JSON source changes such as component declarations.
After a successful auto-export from an already open Graph Toolkit window, the bridge reimports that `.bpgraph`, unloads the open Graph Toolkit graph, loads the saved graph from disk again, and forces the Graph Toolkit UI state to rebuild immediately.

### Drag UI Objects Into Graph Toolkit

An open `.bpgraph` accepts Unity UI objects dragged from the Hierarchy.

Behavior:

```text
Drag GameObject/Component onto graph
  -> scan node manifests for properties typed UIBinding<T>
  -> match T against the dragged object or one of its components
  -> show an Add Node menu
  -> create the selected visual node at the drop position
  -> set the node binding property, usually target
  -> add/update graph.Bindings
  -> add/update the nearest parent UIBlueprintBinder serialized binding entry
```

Current built-in examples:

```text
TMP_Text/GameObject with TMP_Text -> UI.SetText
Button/GameObject with Button -> UI.BindButtonClick
Selectable/Button/GameObject with Selectable -> UI.SetInteractable
Any GameObject/Component -> UI.SetVisible
```

To make a new node appear in the drag menu, add a manifest property such as:

```json
{
  "id": "target",
  "type": "UIBinding<MyWidget>",
  "required": true
}
```

Then ensure `MyWidget` is a `UnityEngine.Object` type resolvable by name. The drag handler matches a dragged component directly or calls `GameObject.GetComponent(MyWidget)`.

If the object has no parent `UIBlueprintBinder`, the graph node and graph binding are still created, but the scene binding must be added manually before runtime.

## 9. Compiled Blueprint Assets

`BlueprintRunner.compiledBlueprint` is the required runtime reference. It points at a shared `BlueprintCompiledAsset` generated from a `.blueprint.json` source plus the node manifests used by that graph.

Automation lives in:

```text
Assets/BlueprintSystem/Editor/BlueprintRunnerManifestSync.cs
```

It validates a `.blueprint.json` asset against `Assets/BlueprintSystem/Specs/Nodes/*.node.json`, bakes manifest defaults into node properties, and writes `<BlueprintName>.compiled.asset` next to the source blueprint. Runtime execution requires the compiled asset; runners do not reference source JSON or compile from node manifests.

Compile runs from:

```text
Blueprint JSON Editor toolbar: Compile
Graph Toolkit export and auto-export after writing .blueprint.json
Tools/Blueprint System/Migrate Legacy Runner JSON References for one-time scene/prefab migration
```

At runtime the player uses:

```text
BlueprintRunner.compiledBlueprint -> RuntimeBlueprint -> BlueprintVM -> C# Executors
```

No runtime resource scan, source JSON reference, or per-runner manifest list is required. If a compiled asset is stale or missing in the editor, open the source blueprint in the Blueprint JSON Editor and run `Compile`, then assign the generated `.compiled.asset` to the runner.

## 10. Validation and Tests

Run EditMode tests after changing runtime, manifests, compiler, validator, or Graph Toolkit bridge:

```bash
/Applications/Unity/Unity-6000.3.14f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -quit \
  -nographics \
  -projectPath /Users/liding/Blueprint \
  -runTests \
  -testPlatform EditMode \
  -testResults Temp/BlueprintSystemEditModeResults.xml
```

If Unity already has this project open, batchmode fails with a project lock. Ask the user to close Unity before running batchmode tests.

Current tests live in:

```text
Assets/BlueprintSystem/Tests/Editor/BlueprintSystemTests.cs
```

Current test coverage:

```text
Validator accepts InventoryPanel
Validator reports unknown UI binding
Compiler builds runtime indexes
Runtime sets TMP text and executes button clicked output
Graph Toolkit bridge round-trips InventoryPanel
Every manifest has a dedicated Graph Toolkit visual node
UI.SetText imports as UISetTextVisualNode
```

## 11. Diagnostics Cheat Sheet

Common validator codes:

```text
BP001  unknown node type
BP002  missing required property or value input
BP003  port type mismatch
BP004  bad edge, unknown port/node, duplicate value input
BP005  unknown UI binding
BP006  duplicate node id
BP008  value dependency cycle
BP009  missing executor
BP010  malformed source / missing required root fields
BP011  event node issue
BP012  property type mismatch
BP020  variable access node is missing a variable name
BP021  variable access node references an unknown variable
BP022  Variable.Set value is not assignable to the variable type
BP023  duplicate variable name
BP024  variable default value is not assignable to the declared type
```

When fixing diagnostics, prefer the smallest source change:

```text
unknown node type -> add/fix manifest typeId
missing executor -> register executor or fix manifest.executor
unknown binding -> add binding declaration or fix property value
bad edge -> verify node IDs and port IDs
type mismatch -> fix manifest type or connected output/input
```

## 12. Current Built-in Node Families

Flow:

```text
Flow.Branch
Flow.Sequence
Flow.Delay
```

Events:

```text
UI.Event.OnOpen
UI.Event.OnClose
Game.Event.OnStart
Game.Event.Custom
```

Variables:

```text
Variable.Get
Variable.Set
Variable.Compare
```

UI:

```text
UI.SetText
UI.SetVisible
UI.SetInteractable
UI.BindButtonClick
```

Game:

```text
Game.Log
Game.SendEvent
```

## 13. Do Not Break These Boundaries

Runtime must not reference:

```text
UnityEditor
Unity.GraphToolkit.Editor
Editor/GraphToolkit/*
```

Editor bridge may reference:

```text
UnityEditor
Unity.GraphToolkit.Editor
BlueprintSystem runtime models
```

Executors should not know about Graph Toolkit visual nodes.

Manifests should not contain Unity object references.

Blueprint JSON should stay diffable and readable.

## 14. Pre-Final Checklist for Agents

Before responding after a change:

1. Mention changed files.
2. Mention whether tests/compile checks were run.
3. If tests could not run because Unity is open, say that clearly.
4. For new nodes, confirm manifest, executor, registry, and tests are aligned.
5. For Graph Toolkit nodes, confirm `[BlueprintVisualNodeType]` matches `typeId`.
6. Keep final response short and actionable.
