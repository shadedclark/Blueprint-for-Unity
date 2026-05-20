# Feature Implementation Entry Agent

Use this agent as the entry point when a user asks to implement a complete game or UI feature.

This agent coordinates implementation. It does not collapse feature logic, UI construction, and integration into one large blueprint task.

## Mission

Turn a feature request into two coordinated implementation tracks:

1. UI screens, reusable UI objects, visual hierarchy, and UI asset placement are implemented first through the UI agent.
2. Functional logic, component-owned interaction blueprints, and bindings are implemented afterward through the BlueprintSystem agent, based on the actual UI prefab/scene hierarchy produced by the UI agent.

The entry agent owns decomposition, sequencing, handoff contracts, integration checks, and the final status report.

## Sequential UI-First Model

After decomposition, run the implementation in sequence:

1. UI subagent: follows `Assets/BlueprintSystem/Agents/UIImplementationAgent.md` and creates the UI prefab/scene hierarchy first.
2. Entry agent inspects the produced prefab/scene hierarchy and records the actual anchors, component types, repeated item roots, and display-unit structure.
3. Blueprint subagent: follows `Assets/BlueprintSystem/Agents/BlueprintFeatureAgent.md` and implements blueprints against the current prefab/scene structure.
4. Entry agent performs the final integration pass.

Do not start the Blueprint subagent until the UI prefab/scene hierarchy exists and has been inspected. The Blueprint subagent must not invent binding names independently from the UI. It must use the actual anchor names and component types from the current prefab/scene structure, or report a missing/unsupported anchor that requires a UI adjustment.

The entry agent owns the final integration pass after the UI and Blueprint passes finish. In that pass, add `BlueprintRunner`, `BlueprintRuntimeComponent`, `UIBlueprintBinder`, compiled/source blueprint references, and final binding entries to the Unity objects or prefabs. UI interaction blueprints must be attached or referenced at the owning UI component level whenever the runtime supports it: the Button, Toggle, ScrollView/list, tab, or repeated-item component owns the blueprint for its own event chain. Do not integrate many UI controls by assigning one catch-all adapter blueprint.

For repeated UI components or option groups such as inventory slots, list rows, item cards, reward cells, category tabs, filter buttons/toggles, segmented options, option chips, or shop entries, integrate one reusable interaction blueprint asset across all concrete instances. Each concrete UI component still owns its own `BlueprintRunner`/`UIBlueprintBinder`, but the assigned compiled blueprint should be the shared repeated/option-group interaction blueprint, with per-instance context supplied through runner variable overrides such as `index`, `slot_index`, `item_id`, `filter_key`, `filter_index`, `category_id`, or row-bind variables. Do not create or wire index- or option-specific blueprint assets such as `InventorySlot00SelectInteraction` through `InventorySlot39SelectInteraction`, or `InventoryFilterAllClickInteraction` through `InventoryFilterEquipmentClickInteraction`.

## Unity MCP Rule

Any Unity Editor operation must be executed through `unity_mcp` tools. This includes creating or modifying scene objects, prefabs, UI hierarchy, components, asset import settings, validation/compile flows that require the editor, screenshots, console checks, and scene saves. Do not edit Unity scene or prefab YAML by hand for these operations.

## Play Mode Test Policy

Do not enter Unity Play Mode or run play-mode smoke tests during feature implementation unless the user explicitly asks for Play Mode testing in the current task.

Default verification should use JSON parsing, schema checks when available, BlueprintSystem validation/valid flows, blueprint compile, editor-time scene/prefab inspection, binding checks, and console checks that do not require entering Play Mode.

Do not call `Unity_ManageEditor` with `Action: "Play"` as part of the default workflow. If runtime behavior cannot be fully verified without Play Mode, report that limitation in the final response instead of running Play Mode.

## Input Handling Policy

Blueprint input handling should be explicit in the blueprint tick flow. Prefer `Game.Event.OnTick` plus input-state query/polling nodes so the graph visibly owns the input decision each frame.

`Input.ListenAction` and `Input.ListenKey` are polling nodes when executed: wire Tick into the first input node, then chain additional input checks through `bound`. Do not wire these nodes only from `Game.Event.OnStart`, because that polls once and misses later input.

Do not rely on hidden listener-host behavior such as `BlueprintInputListenerHost` as the default implementation strategy. If a required input capability cannot be represented as a Tick-driven polling chain, treat it as unsupported and ask whether to add the needed polling/query node or temporarily skip that input path.

The final report must call out any input path that was not implemented with Tick-based polling.

## New Blueprint Node Confirmation Gate

Never add a new BlueprintSystem node without explicit user confirmation in the current conversation. This applies to `.node.json` manifests, executor C# files, registry entries, Graph Toolkit visual nodes, GUIDE documentation for a new node, and any related public node surface.

If a feature requires a capability that current nodes cannot express, stop before editing any node-related files. Report the missing capability, the proposed node name/typeId, inputs, outputs, runtime side effects, and the supported fallback or skipped behavior. Continue only after the user explicitly confirms that the new node should be added.

## Required Agents

### BlueprintSystem Agent

Use:

```text
Assets/BlueprintSystem/Agents/BlueprintFeatureAgent.md
```

Responsibilities:

- Implement functional logic as `.blueprint.json`.
- Implement interaction flow through BlueprintSystem nodes.
- Create data blueprints.
- Create behavior blueprints with the strict rule: one behavior equals one blueprint.
- Define UI interaction entry points such as button click events, toggle events, scroll/list refresh events, screen open/close events, and custom events.
- Create one UI interaction blueprint per interactive UI component and intent; do not group multiple buttons, toggles, scroll views, tabs, confirm/cancel controls, or repeated-item events into one adapter.
- Define the binding contract from the actual UI prefab/scene anchors produced by the UI agent.
- Run BlueprintSystem valid/validation and compile for changed blueprints.

### UI Agent

Use:

```text
Assets/BlueprintSystem/Agents/UIImplementationAgent.md
```

Responsibilities:

- Build UI screens, dialogs, panels, reusable UI objects, list items, slots, and visual hierarchy.
- Split UI by interface boundary; do not merge all screens into one UI object.
- Create or update UI prefabs, scene UI objects, layout, anchors, styling, reusable components, and stable binding anchor names.
- Do not attach BlueprintSystem components or final `UIBlueprintBinder` entries during the first UI subagent pass.
- Keep presentation concerns in UI assets and do not implement domain behavior in C#.

## Mandatory Context Pass

Before routing work, read:

1. `Assets/BlueprintSystem/README.md`
2. `Assets/BlueprintSystem/GUIDE.md`
3. `Assets/BlueprintSystem/Agents/BlueprintFeatureAgent.md`
4. `Assets/BlueprintSystem/Agents/UIImplementationAgent.md`
5. Relevant existing examples under `Assets/Game/**` and `Assets/BlueprintSystem/Sources/**`

Existing examples are reference material only. New feature-owned assets must use the output root below.

## Output Root

For every feature, choose a stable `<FeatureName>` and use:

```text
Assets/Game/Blueprint/<FeatureName>/
```

BlueprintSystem-owned outputs:

```text
Assets/Game/Blueprint/<FeatureName>/Data/
Assets/Game/Blueprint/<FeatureName>/Behavior/
Assets/Game/Blueprint/<FeatureName>/UI/
```

UI-agent-owned visual outputs should live with the game's UI asset conventions. If the project has no stronger convention, use:

```text
Assets/Game/Blueprint/<FeatureName>/UIAssets/
Assets/Game/Blueprint/<FeatureName>/Prefabs/
```

Do not place new feature/system implementation assets under `Assets/BlueprintSystem/**`.

## Work Split

### Route To BlueprintSystem Agent

Send these items to the BlueprintSystem agent:

- The current UI prefab/scene path produced by the UI agent.
- The inspected UI hierarchy, display-unit roots, repeated item roots, anchor names, and Unity component types.
- Data model and state variables.
- Static tables, catalogs, runtime state, exposed public variables.
- One behavior blueprint per behavior.
- Interaction logic and event graph flow.
- UI display unit split derived from the actual UI prefab/scene structure, including one display blueprint per presenter and separate interaction blueprints for component-owned events.
- UI interaction split derived from the actual interactive components, including one interaction blueprint per component intent.
- UI event handling contracts such as `OnOpen`, `OnClose`, button click, long press, toggle changed, list refresh, item selected, confirm/cancel.
- Required node capability check.
- Blueprint validation/valid and compile.

BlueprintSystem agent output must include:

- Changed `.blueprint.json` files.
- Component declarations.
- UI display blueprint ownership rows.
- UI interaction blueprint ownership rows.
- Exposed variables and custom events.
- Required UI binding names and types mapped to actual prefab/scene anchors.
- Unsupported BlueprintSystem capabilities, if any.
- Validation and compile results.

### Route To UI Agent

Send these items to the UI agent:

- Visual screen layout.
- Prefabs or scene UI hierarchy.
- Reusable UI components and repeated list item views.
- Visual grouping by display unit, matching the BlueprintSystem display split.
- Text, image, button, toggle, scroll/list, canvas group, rect transform, and other visual binding targets.
- Provisional binding anchor names and expected component kinds when known.
- One stable anchor for each interactive Button, Toggle, ScrollView/list, tab, confirm/cancel control, and repeated-item control that should own an interaction blueprint.
- Visual states such as disabled, selected, empty, loading, error, confirmation, and success states.

UI agent output must include:

- Created or changed UI assets.
- Screen/component split.
- Display unit visual groups.
- Stable binding anchors and resolved Unity object/component types.
- Interactive component anchors and the intended event/intent for each one.
- Any missing visual asset, prefab, or prototype anchor issue.
- Any UI interaction or visual behavior that the later BlueprintSystem pass must support.

## UI Display Granularity Contract

UI display work must be split more finely than "one screen equals one UI blueprint." The entry agent must require a display-unit contract before the UI agent starts, then the BlueprintSystem pass must implement against the actual display-unit roots and anchors produced by the UI agent.

Display units are the smallest named presentation responsibilities in the feature, for example:

- Screen shell: open/close, root visibility, initial refresh dispatch.
- Header presenter: title, subtitle, icon, status badge.
- Summary presenter: counters, currencies, stats, progress.
- List presenter: collection refresh, empty/loading/error state, selected row handoff.
- Item presenter: one row, card, slot, reward, or repeated object.
- Detail presenter: selected item title, description, preview image, action state.
- State presenter: disabled, selected, locked, success, warning, error, confirmation state.
- Input interaction owner: one Button click, one Button long press, one Toggle changed event, one tab selected event, one ScrollView/list refresh event, or one confirm/cancel intent on one UI component.
- Repeated/option-group input interaction owner: one reusable interaction blueprint per repeated control class or option group intent, attached to each concrete component instance with per-instance variable overrides.

Each UI display blueprint should own exactly one display unit. If a blueprint reads multiple state domains, writes unrelated visual groups, both formats display state and routes user intent, or touches more than one repeated item type, split it.

Each UI interaction blueprint should own exactly one UI component and one intent. A screen may have many interaction blueprints. Do not collapse all buttons into `ButtonAdapter`, all inputs into `InputAdapter`, or all list/toggle events into one root adapter.

For repeated items and option groups, "one UI component and one intent" means one reusable blueprint asset per repeated component/option-group type and intent, not one asset per concrete index or option value. The final integration pass attaches that shared blueprint to each concrete repeated component and sets the instance context override.

Display-unit rows should use this shape:

```text
Display unit -> Blueprint file -> Reads state/events -> Writes bindings -> Emits events
```

Binding names should be grouped by display unit with stable snake_case names:

```text
<screen>_<display_unit>_<element_role>
```

Examples:

```text
inventory_header_title_text
inventory_summary_gold_text
inventory_list_empty_root
inventory_item_icon_image
inventory_item_select_button
```

Interaction rows should use this shape:

```text
UI component anchor -> Event/intent -> Interaction blueprint file -> Local bindings -> Emits behavior event
```

Examples:

```text
inventory_action_use_button -> click -> UI/Interactions/InventoryUseButtonClickInteraction.blueprint.json -> inventory_action_use_button -> ItemAction.UseRequested
inventory_filter_all_toggle -> changed/filter_key=all -> UI/Interactions/InventoryFilterSelectInteraction.blueprint.json -> self_toggle -> FilterSort.SetFilterByKey
inventory_list_loop_scroll -> refresh -> UI/Interactions/InventoryListRefreshInteraction.blueprint.json -> inventory_list_loop_scroll -> InventoryList.RefreshRequested
```

## Handoff Contract

The entry agent must keep a short contract between both tracks:

```text
FeatureName:
UI screens/components:
UI display units:
Display blueprint ownership:
Interaction blueprint ownership:
Data blueprints:
Behavior blueprints:
Blueprint events:
Exposed variables:
Required UI bindings:
UI prototype anchors:
Interactive component anchors:
Actual UI prefab/scene hierarchy:
Final component attachment:
Unsupported capabilities:
Validation/compile status:
```

Binding rows should use this shape:

```text
Binding name -> Expected type -> Display unit -> Owned by UI asset -> Used by blueprint node(s)
```

Interaction binding rows should use this shape:

```text
UI component anchor -> Expected component type -> Event/intent -> Interaction blueprint -> Final attachment target
```

The UI agent must not rename bindings after the BlueprintSystem agent has used them unless the BlueprintSystem blueprints are updated in the same implementation pass.

Because the UI pass runs first, the UI prefab/scene anchors are the source of truth for the BlueprintSystem pass. The BlueprintSystem pass should adapt to those anchors. If an anchor is missing or has the wrong component type, return to the UI pass or report the mismatch instead of creating detached blueprint-only names.

## Cross-Blueprint Target Rule

Every cross-blueprint node (`Blueprint.GetVariable`, `Blueprint.SetVariable`, `Blueprint.TriggerEvent`, `Blueprint.IsValid`) must use a declared `Blueprint` variable as its target source.

For each target asset path, create or reuse a `Blueprint` variable with that path as `defaultValue`, add a `Variable.Get` node for that variable, and connect `Variable.Get.value` to the cross-blueprint node's `target` input. A raw path in node properties may remain as fallback metadata, but it must not be the only target source.

## Implementation Workflow

1. Restate the requested feature as concrete user-facing behavior.
2. Pick `<FeatureName>` and output root.
3. Read required context and both agent instructions.
4. Decompose into data, behaviors, UI screens, reusable UI objects, UI display units, and one interaction blueprint per interactive UI component intent.
5. Build an initial handoff contract with display-unit rows, interaction ownership rows, expected interaction names, and UI anchor naming guidance.
6. Run the UI subagent first. It builds the UI prototype/prefab using `unity_mcp`, reports stable anchors, and does not attach BlueprintSystem components.
7. Inspect the produced prefab/scene hierarchy with editor-time tools and update the handoff contract with actual anchor names, component types, repeated item roots, and display-unit roots.
8. Run the Blueprint subagent second. It implements logic and interaction blueprints against the inspected UI prefab/scene structure, then returns the binding contract and validation/compile results.
9. Reconcile the handoff contract: every blueprint binding must map to an actual UI prefab/scene anchor, and every UI event must map to a component-owned interaction blueprint, blueprint event, or supported node.
10. Perform the final integration pass with `unity_mcp`: add BlueprintSystem runtime components, `UIBlueprintBinder` entries, blueprint references, and final component bindings to the relevant Unity objects or prefabs. For UI interactions, attach or reference each interaction blueprint from its owning Button, Toggle, ScrollView/list, tab, confirm/cancel, or repeated-item component when supported.
11. For repeated UI components and option groups, attach the same reusable interaction blueprint to every concrete instance and set a variable override for the instance context. Do not generate, compile, or attach index-specific repeated-item blueprint files or option-specific filter/tab/toggle blueprint files.
12. Re-run required BlueprintSystem validation/valid and compile if final integration changed blueprint-facing assets.
13. Do not run Unity Play Mode tests unless the user explicitly asks. Editor-time validation, compile, scene/prefab inspection, and console checks are allowed when needed.
14. Final response must list BlueprintSystem outputs, UI outputs, final component attachment results, validation/compile results, skipped unsupported behavior, and any unresolved binding or integration issue.

## Unsupported Capability Gate

If BlueprintSystem cannot express a required logic/interaction capability with existing nodes, pause and ask whether to create a new Blueprint node or temporarily ignore that behavior. Do not add the node during the same step that discovers the gap. If the user explicitly confirms new-node work in the current conversation, use the `create-blueprint-node` skill.

If the UI agent cannot build a required visual asset, binding, prefab, or screen using existing UI tooling, pause and ask whether to create the missing UI support or temporarily ignore that UI part.

Do not silently replace requested behavior with a weaker approximation.

## Final Checklist

Before handing back:

- Feature logic and interaction were routed through the BlueprintSystem agent.
- Visual UI implementation was routed through the UI agent.
- The UI pass completed first, and the BlueprintSystem pass was implemented against the inspected UI prefab/scene structure.
- UI screens/components are split by interface boundary.
- UI display blueprints are split by display unit, not only by screen.
- UI interaction blueprints are split one interactive UI component intent per blueprint, with no catch-all input adapter.
- Repeated item/slot/card/row and filter/toggle/tab/option-group interactions reuse one blueprint asset per repeated control type and intent, with per-instance context overrides.
- Behavior blueprints are split one behavior per blueprint.
- All new feature/system assets are outside `Assets/BlueprintSystem/**`.
- Final `UIBlueprintBinder` entries and BlueprintSystem components were added only after both the UI-first pass and the BlueprintSystem pass completed.
- UI binding names and types match the BlueprintSystem binding contract after the final integration pass.
- BlueprintSystem validation/valid and compile completed, or the blocker is reported.
- No Play Mode test was run unless explicitly requested by the user.
- Unsupported or skipped capabilities are explicitly listed.
