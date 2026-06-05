---
name: blueprint-ai-behavior-tree
description: Implement Unity BlueprintSystem AI behavior through Behavior Tree assets. Use when Codex should create or modify .btree.json files, follow AIBehaviorTreeAgent, read BehaviorTree/GUIDE.md first, compile behavior trees, or attach BehaviorTreeRunner integration without creating C# scripts.
---

# Blueprint AI Behavior Tree

Use this as the direct Codex entrypoint for NPC, enemy, companion, creature, bot, or other AI behavior that should be implemented with the BlueprintSystem Behavior Tree runtime.

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

- `BehaviorTree/GUIDE.md`
- `Agents/AIBehaviorTreeAgent.md`
- `BehaviorTree/BehaviorTreeDesign.md` when deeper design context is needed
- Relevant existing `.btree.json` samples under `Assets/BlueprintSystem/Samples~/BehaviorTree/**` and `Assets/Game/Blueprint/**`
- Related `.blueprint.json` assets only when the behavior tree calls Blueprint events or tasks

`BehaviorTree/GUIDE.md` must be read first. Then follow `AIBehaviorTreeAgent.md` as the source of truth.

## Direct Tooling

Use Unity MCP for Editor operations such as behavior tree compile, `BehaviorTreeRunner` attachment, prefab or scene inspection, asset refresh, validation, console checks, and scene saves. Do not hand-edit Unity scene or prefab YAML.

## Guardrails

- Keep `.btree.json` as the Behavior Tree source of truth.
- Treat `.btgraph` as editor visualization/cache unless the user explicitly asks for visual graph asset work.
- Use existing `BT.*` nodes, Decorators, Services, Blackboard keys, and Blueprint bridge tasks.
- Put feature-owned behavior tree outputs under `Assets/Game/Blueprint/<FeatureName>/Behavior/` unless the live agent docs specify a stronger convention.
- Do not create or modify any `.cs` file.
- Do not create Behavior Tree executors, runtime registry entries, Graph Toolkit visual node classes, or normal Blueprint `.node.json` manifests from this skill.
- Do not enter Play Mode unless the user explicitly asks in the current task.
- If existing `BT.*` nodes cannot express the requested AI behavior, stop with the unsupported capability report described in `AIBehaviorTreeAgent.md`.
