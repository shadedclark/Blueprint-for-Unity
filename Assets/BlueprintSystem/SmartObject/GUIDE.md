# SmartObject Blueprint Module

SmartObject nodes expose the dedicated SmartObject reservation flow to Blueprint graphs. They are intended for world-object planning, not general variable mutation. Blueprints pass request data and store returned ids/tokens; they must not manually edit reserved, occupied, token, or timeout state.

## Module Layout

SmartObject is part of BlueprintSystem core, but it is kept under one module root:

```text
Assets/BlueprintSystem/SmartObject/Runtime
Assets/BlueprintSystem/SmartObject/Executors
Assets/BlueprintSystem/SmartObject/Editor
Assets/BlueprintSystem/SmartObject/Editor/GraphToolkit
Assets/BlueprintSystem/SmartObject/Specs/Nodes
Assets/BlueprintSystem/SmartObject/Tests/Editor
```

Core loads public node manifests from the BlueprintSystem package root `**/Specs/Nodes` and from project `Assets/**` manifests. `BlueprintExecutorRegistry.CreateDefault()` delegates SmartObject registration through `SmartObjectExecutorRegistrar.Register(registry)`.

## Authoring

Add `SmartObjectComponent` to each usable world object.

`SmartObjectComponent` automatically generates a read-only GUID-style `objectId` and repairs empty, legacy, or duplicated ids during editor validation. Use the Inspector copy button or debugger details when a graph needs a specific `objectId`.

Set optional object tags/access group/base score, and one or more slots.

Each slot defines `slotId`, comma-separated `activities`, optional tags/access group, score, use duration, target/facing transforms or local fallback positions, and blocked/closed flags.

## Authored Fields

`SmartObjectComponent` fields describe the usable world object as a whole.

| Field | Meaning |
| --- | --- |
| `objectId` | Stable generated id for this SmartObject. Blueprints use it with `slotId` to reserve a specific slot. The component generates and repairs this id in editor validation; authors should copy it from the Inspector/debugger instead of hand-editing it. |
| `smartObjectEnabled` | Master availability switch for the whole object. When disabled, the object is skipped by search and any reserved or occupied slots on it are released with reason `Disabled`. |
| `objectBaseScore` | Score bias applied to every candidate slot on this object. `FindBest` adds it to `100 + slotBaseScore + needScore - distancePenalty`. |
| `tags` | Comma-, semicolon-, or pipe-separated tags contributed by the object. Object tags are combined with each slot's tags for `FindBest` `requiredTags` and `forbiddenTags` filtering. They are good for broad traits shared by all slots, such as `chair`, `cover`, `indoor`, or `shop`. |
| `accessGroup` | Default permission group required by this object. Empty means unrestricted unless a slot provides its own access group. A non-empty slot `accessGroup` overrides this object value. |
| `slots` | Authored list of usable positions/actions on this object. Each slot is searched, reserved, occupied, and released independently. |

`SmartObjectSlot` fields describe one concrete use point on a SmartObject.

| Field | Meaning |
| --- | --- |
| `slotId` | Id of this slot within its SmartObject. Runtime calls identify a target slot by the pair `objectId` + `slotId`, so keep ids stable and unique within the object. |
| `activities` | Activity ids this slot supports, such as `sit`, `sleep`, `repair`, or `inspect`. Values are split by comma, semicolon, or pipe; matching is case-insensitive. Use `*` to allow any non-empty activity. Empty activities match nothing. |
| `tags` | Tags contributed only by this slot. They are added to the object tags for tag filtering; they do not override or remove object tags. Use slot tags for traits that differ between slots, such as `left`, `right`, `vip`, `front`, or `maintenance`. |
| `accessGroup` | Permission group required by this slot. When non-empty, it overrides the object's `accessGroup`; when empty, the slot inherits the object's access group. If both are empty, the slot is unrestricted. |
| `slotBaseScore` | Score bias applied only to this slot. Use it to prefer one seat, work point, or interaction point over another on the same object. |
| `useDuration` | Suggested duration for using this slot. It is returned by `FindBest`, `Reserve`, and `BeginUse` so the graph can drive animations/timers. Occupied slots are not auto-released by this value; the graph should call `Release` when use is complete. |
| `targetTransform` | Optional Transform that provides the world target position for the requester. If unset, `localTargetPosition` is transformed by the SmartObject owner. |
| `facingTransform` | Optional Transform that provides the world point the requester should face. If unset, `localFacingPosition` is transformed by the SmartObject owner. |
| `localTargetPosition` | Owner-local fallback target position used when `targetTransform` is unset. |
| `localFacingPosition` | Owner-local fallback facing point used when `facingTransform` is unset. |
| `blocked` | Temporary or physical availability flag. Blocked slots are skipped by `FindBest` and fail reservation/use with `SlotBlocked`. |
| `closed` | Logical closed-state flag. Closed slots are skipped by `FindBest` and fail reservation/use with `Closed`; use it for unavailable interactions that should be distinguished from a temporary block. |

Runtime reservation fields such as reserved/occupied state, requester id, reservation token, timeout, occupied time, and last release reason are internal, non-serialized state. Blueprints should read them through SmartObject nodes and must not edit them directly.

## Matching Rules

`activities`, `tags`, and `accessGroup` serve different purposes.

| Concept | Where authored | How it is used |
| --- | --- | --- |
| `activities` | Slot only | Determines whether a slot can perform the requested `activity`. If the requested activity does not match the slot's activities, that slot is ignored or returns `ActivityMismatch`. |
| `tags` | Object and slot | Describes traits for search filtering. `FindBest` combines object tags and slot tags, then requires every `requiredTags` value to be present and every `forbiddenTags` value to be absent. |
| `accessGroup` | Object and slot | Controls permission. `FindBest` and `Reserve` compare the requested access group against the required group. Slot access group overrides object access group; empty required group allows any requester. |

List fields (`activities`, `tags`, `requiredTags`, and `forbiddenTags`) accept comma, semicolon, or pipe separators and trim whitespace. Matching is case-insensitive. `accessGroup` is a single string, not a list.

## Runtime Behavior

`FindBest` never changes state.

`Reserve`, `BeginUse`, `Release`, and `ReleaseByRequester` are the only public nodes that change slot state.

`Reserve` revalidates object, slot, activity, blocked/closed/access state, and current reservation/occupation before writing a token.

Reserved slots expire through `SmartObjectRegistry.TickTimeouts`, called by SmartObject components and registry operations.

## Debugger

Open `Tools/Blueprint System/SmartObject/Debugger` to inspect SmartObjects in the currently loaded scenes.

The debugger is read-only. Selecting a row selects and pings the GameObject in the Unity Editor, then normal authoring edits should be made through the Inspector.

The list scans loaded scene objects, including inactive or disabled `SmartObjectComponent` instances, and excludes prefab/assets. Runtime registration status is shown as `Registered`, `NotRegistered`, `DuplicateObjectId`, or missing-id/component diagnostics.

Enable `Scene Gizmos` in the debugger toolbar to draw slot target/facing positions in the Scene View. Gizmos are off by default and persisted in EditorPrefs. Slot colors are:

```text
Free: green
Reserved: yellow
Occupied: blue
Blocked: red
Closed, inactive, disabled, or missing: gray
```

## Nodes

| Type ID | Purpose | Ports and notes |
| --- | --- | --- |
| `SmartObject.FindBest` | Finds the highest-scoring available slot. | Inputs `requesterId`, `activity`, `center`, `radius`, optional `requiredTags`, `forbiddenTags`, `accessGroup`, `needScore`, `maxDistancePenalty`; outputs `found`, `objectId`, `slotId`, `targetPosition`, `facingPosition`, `useDuration`, `score`, `failReason`. Score is `100 + objectBaseScore + slotBaseScore + needScore - distancePenalty`, with `distancePenalty = distance * 2` and optional cap. |
| `SmartObject.Reserve` | Reserves a specific `objectId` + `slotId`. | Exec input `execIn`; inputs `requesterId`, `objectId`, `slotId`, `activity`, optional `holdSeconds` default `30`, optional `accessGroup`; outputs `execOut`, `success`, `reservationToken`, `targetPosition`, `facingPosition`, `useDuration`, `failReason`. Repeating Reserve with the same requester and live token refreshes the hold time and returns the existing token. |
| `SmartObject.BeginUse` | Converts a reservation token to occupied state. | Inputs `requesterId`, `reservationToken`; outputs `success`, `objectId`, `slotId`, `useDuration`, `failReason`. Expired tokens return `TokenExpired`. |
| `SmartObject.Release` | Releases a reserved or occupied slot by token. | Inputs `requesterId`, `reservationToken`, optional `reason` default `Completed`; outputs `success`, `objectId`, `slotId`, `previousState`, `failReason`. Unknown or already released tokens return `TokenInvalid` without changing state. |
| `SmartObject.GetReservationInfo` | Reads token state without changing state. | Input `reservationToken`; outputs `valid`, `objectId`, `slotId`, `requesterId`, `state`, `targetPosition`, `facingPosition`, `remainingSeconds`, `failReason`. |
| `SmartObject.ReleaseByRequester` | Force-clears all reservations/occupancy held by a requester. | Inputs `requesterId`, optional `reason` default `ForceRelease`; outputs `releasedCount`, `failReason`. |

Failure reasons are returned as stable strings: `None`, `InvalidRequester`, `InvalidActivity`, `ObjectNotFound`, `SlotNotFound`, `ActivityMismatch`, `ObjectDisabled`, `SlotBlocked`, `AlreadyReserved`, `AlreadyOccupied`, `AccessDenied`, `Closed`, `OutOfRange`, `NoCandidate`, `TokenInvalid`, `TokenExpired`, `TokenOwnerMismatch`, `StateMismatch`, and `SystemError`.

## Avoid Duplicates

Use `SmartObject.FindBest -> SmartObject.Reserve -> SmartObject.BeginUse -> SmartObject.Release` for normal activity planning before adding one-off object-use nodes.

Use `ReleaseByRequester` only for requester death/disable/reset cleanup; normal completion should use `Release`.

Keep activity, tags, access group, release reason, and fail reason as strings until a project-specific config table or enum is explicitly needed.

Do not store scene object references in `.blueprint.json`; SmartObject nodes exchange object ids, slot ids, positions, and tokens.
