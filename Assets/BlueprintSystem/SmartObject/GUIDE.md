# SmartObject Blueprint Module

SmartObject nodes expose the dedicated SmartObject reservation flow to Blueprint graphs. They are intended for world-object planning, not general variable mutation. Blueprints pass request data and store returned ids/tokens; they must not manually edit reserved, occupied, token, or timeout state.

## Module Layout

SmartObject is part of BlueprintSystem core, but it is kept under one module root:

```text
Assets/BlueprintSystem/SmartObject/Runtime
Assets/BlueprintSystem/SmartObject/Executors
Assets/BlueprintSystem/SmartObject/Editor/GraphToolkit
Assets/BlueprintSystem/SmartObject/Specs/Nodes
Assets/BlueprintSystem/SmartObject/Tests/Editor
```

Core loads the public node manifests from `Assets/BlueprintSystem/*/Specs/Nodes`, and `BlueprintExecutorRegistry.CreateDefault()` delegates SmartObject registration through `SmartObjectExecutorRegistrar.Register(registry)`.

## Authoring

Add `SmartObjectComponent` to each usable world object.

Set a unique `objectId`, optional object tags/access group/base score, and one or more slots.

Each slot defines `slotId`, comma-separated `activities`, optional tags/access group, score, use duration, target/facing transforms or local fallback positions, and blocked/closed flags.

## Runtime Behavior

`FindBest` never changes state.

`Reserve`, `BeginUse`, `Release`, and `ReleaseByRequester` are the only public nodes that change slot state.

`Reserve` revalidates object, slot, activity, blocked/closed/access state, and current reservation/occupation before writing a token.

Reserved slots expire through `SmartObjectRegistry.TickTimeouts`, called by SmartObject components and registry operations.

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
