---
name: blueprint-feature
description: Implement complete Unity game, UI, or AI features through the local BlueprintSystem package. Use when Codex should run the package's FeatureImplementationEntryAgent workflow, coordinate UI creation, Blueprint logic, Behavior Tree AI, Unity MCP operations, compile .blueprint.json or .btree.json files, apply Blueprint runner bindings, or turn a gameplay/UI/AI request into BlueprintSystem assets.
---

# Blueprint Feature

Use this as the direct Codex entrypoint for full BlueprintSystem feature work.

## Locate BlueprintSystem

Resolve the locator script relative to this `SKILL.md`:

```bash
../../scripts/locate_blueprint_system.py
```

From the Unity project root, run that script with the project root as its only argument. For
example, after resolving it to an absolute path:

```bash
python3 /absolute/path/to/blueprint-system-codex/scripts/locate_blueprint_system.py .
```

If the locator cannot be run, manually locate a package whose `package.json` name is
`com.shadedclark.blueprint-system`.

## Required Context

Read the live package files returned by the locator before planning or editing:

- `README.md`
- `GUIDE.md`
- `Agents/FeatureImplementationEntryAgent.md`
- `Agents/BlueprintFeatureAgent.md`
- `Agents/UIImplementationAgent.md`
- `Agents/AIBehaviorTreeAgent.md` when the feature includes AI behavior
- `BehaviorTree/GUIDE.md` before any behavior tree work
- `BehaviorTree/BehaviorTreeDesign.md` when deeper behavior tree context is needed

Then follow `FeatureImplementationEntryAgent.md` as the source of truth.

## Direct Tooling

Prefer the package MCP tools when available:

- `blueprint_collect_feature_context`
- `blueprint_inspect_prefab_bindings`
- `blueprint_compile_blueprints`
- `blueprint_apply_bindings`
- `blueprint_run_unity_figma_to_ui`
- `blueprint_validate_assets`
- `blueprint_contract_check`
- `blueprint_binding_snapshot`
- `blueprint_runtime_snapshot`

Use the typed BlueprintSystem MCP tools before creating temporary C# editor tests or ad hoc `Unity_RunCommand` probes:

- After changing `.blueprint.json`, `.bpstruct.json`, `.bpdatatable.json`, or resource blueprint sources, run `blueprint_validate_assets` for JSON parsing, runtime registry sync, Blueprint compile, and captured logs. `.btree.json` assets are parsed by this tool, but Behavior Tree compile remains in the Behavior Tree editor tooling.
- When checking graph contracts such as required or forbidden nodes, edges, variables, bindings, components, blackboard keys, unknown edge nodes, or exec fan-in, use `blueprint_contract_check`.
- When checking prefab or loaded-scene integration, use `blueprint_binding_snapshot` for `BlueprintRunner`, `UIBlueprintBinder`, `BehaviorTreeRunner`, missing scripts, compiled asset references, and binding targets.
- Use `blueprint_runtime_snapshot` only after the user explicitly asks for Play Mode/runtime evidence or the current investigation already requires runtime truth; otherwise report runtime verification as not run.

Use Unity MCP for Editor operations such as scene, prefab, component, asset, screenshot, validation, and console work. Do not hand-edit Unity scene or prefab YAML.

## Guardrails

- Keep `.blueprint.json` as the behavior source of truth.
- Keep `.btree.json` as the Behavior Tree AI source of truth.
- Put feature-owned outputs under `Assets/Game/Blueprint/<FeatureName>/` unless the live agent docs specify a stronger convention.
- Run the UI-first track before the Blueprint logic track when the feature needs new UI.
- Route NPC, enemy, companion, creature, or bot decision logic through `Agents/AIBehaviorTreeAgent.md`.
- Do not create or modify `.cs` files for AI behavior tree feature work.
- Do not enter Play Mode unless the user explicitly asks in the current task.
- If existing nodes cannot express the behavior, stop and ask for explicit permission to use `$blueprint-node`.
- If existing `BT.*` nodes cannot express the AI behavior, stop with the unsupported Behavior Tree capability report from `AIBehaviorTreeAgent.md`.
