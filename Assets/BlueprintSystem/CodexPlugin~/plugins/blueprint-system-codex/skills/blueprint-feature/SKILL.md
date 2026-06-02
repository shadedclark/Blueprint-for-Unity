---
name: blueprint-feature
description: Implement complete Unity game or UI features through the local BlueprintSystem package. Use when Codex should run the package's FeatureImplementationEntryAgent workflow, coordinate UI creation and Blueprint logic, use Unity MCP operations, compile .blueprint.json files, apply UIBlueprintBinder bindings, or turn a gameplay/UI request into BlueprintSystem assets.
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

Then follow `FeatureImplementationEntryAgent.md` as the source of truth.

## Direct Tooling

Prefer the package MCP tools when available:

- `blueprint_collect_feature_context`
- `blueprint_inspect_prefab_ui`
- `blueprint_compile_blueprints`
- `blueprint_apply_ui_bindings`
- `blueprint_run_unity_figma_to_ui`

Use Unity MCP for Editor operations such as scene, prefab, component, asset, screenshot, validation, and console work. Do not hand-edit Unity scene or prefab YAML.

## Guardrails

- Keep `.blueprint.json` as the behavior source of truth.
- Put feature-owned outputs under `Assets/Game/Blueprint/<FeatureName>/` unless the live agent docs specify a stronger convention.
- Run the UI-first track before the Blueprint logic track when the feature needs new UI.
- Do not enter Play Mode unless the user explicitly asks in the current task.
- If existing nodes cannot express the behavior, stop and ask for explicit permission to use `$blueprint-node`.
