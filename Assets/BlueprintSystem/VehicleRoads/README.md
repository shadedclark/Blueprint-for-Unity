# Vehicle Roads Module

Vehicle Roads is a BlueprintSystem optional plugin module, implemented in the
same style as SmartObject. It stays under one Unity import root so the runtime,
Blueprint nodes, editor tools, tests, settings, and docs can be enabled,
disabled, exported, or imported together.

The module is default-on. Disable it per active build target from
`Project Settings > Blueprint System > Modules`, or by adding the scripting
define:

```text
BLUEPRINTSYSTEM_DISABLE_VEHICLE_ROADS
```

When disabled, `VehicleRoad.*` manifests are filtered out, VehicleRoad Blueprint
executors are not registered, VehicleRoad Graph Toolkit nodes fall back to
generic nodes, and `BT.VehicleRoad.*` Behavior Tree executors/services are not
registered.

## Directory Layout

- `Scripts/`: runtime and road authoring editor tooling.
- `Editor/`: Blueprint Graph Toolkit integration through the BlueprintSystem
  editor assembly.
- `Tests/`: BlueprintSystem integration tests for public VehicleRoad surfaces.
- `Settings/`: default `RoadNetworkSettings` and `RoadNetworkRuntimeSettings`.
- `Generated/`: sample baked `BakedLaneNetwork` assets.
- `Docs/`: system design, usage guide, and debugging workflow.

## Unity Package Dependencies

Vehicle Roads depends on these Unity packages:

- `com.unity.mathematics` 1.3.2 or a compatible version.
- `com.unity.splines` 2.8.4 or a compatible version.
- `com.unity.test-framework` only if running the included EditMode tests.

Runtime code remains independent from Unity Navigation and NavMesh APIs.
