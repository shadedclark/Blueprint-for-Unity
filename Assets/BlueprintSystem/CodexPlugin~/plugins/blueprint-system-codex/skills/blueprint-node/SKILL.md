---
name: blueprint-node
description: Create or modify Unity BlueprintSystem public nodes. Use when Codex is explicitly asked to add a Blueprint node, edit a .node.json manifest, implement a C# executor, register BlueprintExecutor behavior, add Graph Toolkit visual node support, or update GUIDE.md for node behavior.
---

# Blueprint Node

Use this only when the user explicitly asks for BlueprintSystem node work or confirms that a missing feature capability should become a new node.

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
- Existing `Specs/Nodes/*.node.json`
- Existing executor and registry code under `Runtime/` and `Executors/`
- Existing Graph Toolkit visual node code under `Editor/GraphToolkit/`

## Node Workflow

1. Search existing manifests and executors first. Prefer existing nodes when semantics match.
2. If a new public node is justified, implement the full surface:
   - `Specs/Nodes/<TypeId>.node.json`
   - Runtime executor under `Executors/`
   - Registry entry in `Runtime/BlueprintExecutor.cs`
   - Graph Toolkit visual node when the node is user-facing
   - `GUIDE.md` update in the same change
3. Keep manifest port names aligned with executor input/output IDs.
4. Use Blueprint enum types for small fixed option sets instead of loose strings.
5. Keep `.blueprint.json` behavior source files authoritative; `.bpgraph` is an editor visualization/cache.

Do not add node code, manifests, registry entries, visual nodes, or GUIDE documentation until the current conversation contains explicit approval for that node.

## Validation Tools

After node work, prefer typed BlueprintSystem MCP tools over temporary C# editor tests or ad hoc `Unity_RunCommand` probes when they can provide the needed evidence:

- Use `blueprint_validate_assets` for affected Blueprint/DataTable/Struct sources and registry/compile checks.
- Use `blueprint_contract_check` to prove sample graphs contain the expected new node, ports, required edges, and no exec fan-in regressions.
- Use `blueprint_binding_snapshot` when node work affects runner or prefab integration.
