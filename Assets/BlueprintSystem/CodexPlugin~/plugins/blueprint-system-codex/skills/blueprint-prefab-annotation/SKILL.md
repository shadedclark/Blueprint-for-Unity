---
name: blueprint-prefab-annotation
description: Wire an existing Unity prefab to BlueprintSystem behavior from an annotation Markdown document. Use when Codex is given a prefab path and annotation notes, should follow PrefabAnnotationBlueprintAgent, generate or update .blueprint.json files, compile blueprints, and attach BlueprintRunner or UIBlueprintBinder references and bindings without rebuilding the UI or changing visual layout.
---

# Blueprint Prefab Annotation

Use this as the direct Codex entrypoint when the prefab already exists and annotations describe the desired BlueprintSystem behavior.

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

Read the live package files returned by the locator before editing:

- `README.md`
- `GUIDE.md`
- `Agents/PrefabAnnotationBlueprintAgent.md`
- `Agents/FeatureImplementationEntryAgent.md`
- `Agents/BlueprintFeatureAgent.md`

Then follow `PrefabAnnotationBlueprintAgent.md` as the source of truth.

## Workflow

- Inspect the prefab through Unity MCP, `blueprint_inspect_prefab_bindings`, or `blueprint_binding_snapshot`.
- Use the annotation Markdown as the behavior contract.
- Create or update `.blueprint.json` files only where needed.
- Compile changed blueprints with `blueprint_compile_blueprints`.
- Apply runner references with `blueprint_apply_bindings`.
- Run `blueprint_validate_assets` after source edits for JSON parsing, runtime registry sync, Blueprint compile, and captured logs.
- Use `blueprint_contract_check` when the annotation implies required nodes, edges, variables, bindings, or graph invariants.
- Re-run `blueprint_binding_snapshot` after applying bindings to verify compiled assets, missing scripts, and binding targets.
- Use `blueprint_runtime_snapshot` only when the user explicitly asks for Play Mode/runtime proof; do not enter Play Mode by default.

Do not rebuild the UI, change the visual layout, create C# code, or edit prefab YAML by hand.

Before creating temporary C# editor tests or ad hoc `Unity_RunCommand` probes, check whether these typed BlueprintSystem MCP tools can provide the needed evidence.
