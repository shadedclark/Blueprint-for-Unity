using System.Collections;
using System.Collections.Generic;
using BlueprintSystem.Editor;
using NUnit.Framework;
using UnityEngine;
using VehicleRoads;

namespace BlueprintSystem.Tests
{
    public sealed class VehicleRoadBlueprintSystemTests
    {
        [Test]
        public void VehicleRoadBlueprintManifestsExecutorsAndVisualNodesAreAvailable()
        {
            string[] typeIds =
            {
                "VehicleRoad.FindNearestLane",
                "VehicleRoad.FindLaneRoute",
                "VehicleRoad.SetLaneClosed",
                "VehicleRoad.UpdateVehicle",
                "VehicleRoad.UnregisterVehicle",
                "VehicleRoad.EvaluateTrafficControl",
                "VehicleRoad.RequestLaneChange",
                "VehicleRoad.CompleteLaneChange",
                "VehicleRoad.SetFollowerRoute",
                "VehicleRoad.ComputeFollowerControl",
                "VehicleRoad.GetSubsystemSnapshot"
            };

            BlueprintNodeManifestCollection manifests = BlueprintNodeManifestAssetUtility.LoadManifests();
            BlueprintExecutorRegistry registry = BlueprintExecutorRegistry.CreateDefault();
            for (int i = 0; i < typeIds.Length; i++)
            {
                BlueprintNodeManifest manifest;
                Assert.True(manifests.TryGet(typeIds[i], out manifest), typeIds[i]);

                IBlueprintNodeExecutor executor;
                Assert.True(registry.TryGet(manifest.Executor, out executor), manifest.Executor);

                BlueprintNodeSource sourceNode = new BlueprintNodeSource
                {
                    Id = "vehicle_road_" + i,
                    TypeId = typeIds[i]
                };
                BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(sourceNode, manifest);
                Assert.NotNull(visualNode, typeIds[i]);
                Assert.AreEqual(typeIds[i], visualNode.ReadTypeId());
            }
        }

        [Test]
        public void VehicleRoadsModuleCanBeDisabledForPublicNodeSurfaces()
        {
            using (BlueprintModuleSettings.OverrideVehicleRoadsEnabledForTests(false))
            {
                Assert.False(BlueprintNodeManifestAssetUtility.IsManifestPath("Assets/BlueprintSystem/VehicleRoads/Specs/Nodes/VehicleRoad.FindNearestLane.node.json"));
                Assert.True(BlueprintNodeManifestAssetUtility.IsManifestPath("Assets/BlueprintSystem/Specs/Nodes/Game.Log.node.json"));

                BlueprintNodeManifestCollection manifests = BlueprintNodeManifestAssetUtility.LoadManifests();
                BlueprintNodeManifest manifest;
                Assert.False(manifests.TryGet("VehicleRoad.FindNearestLane", out manifest));

                IBlueprintNodeExecutor executor;
                Assert.False(BlueprintExecutorRegistry.CreateDefault().TryGet("VehicleRoad.FindNearestLane", out executor));

                BehaviorTreeExecutorRegistry behaviorTreeRegistry = BehaviorTreeExecutorRegistry.CreateDefault();
                Assert.False(behaviorTreeRegistry.HasNode("BT.VehicleRoad.FindNearestLane"));
                Assert.False(behaviorTreeRegistry.HasNode("BT.VehicleRoad.FindLaneRoute"));
                Assert.False(behaviorTreeRegistry.HasNode("BT.VehicleRoad.ComputeFollowerControl"));
                Assert.False(behaviorTreeRegistry.HasNode("BT.VehicleRoad.DriveFollower"));
                Assert.False(behaviorTreeRegistry.HasService("BT.VehicleRoad.UpdateRoadAgent"));

                BlueprintVisualNode visualNode = BlueprintVisualNodeFactory.Create("VehicleRoad.FindNearestLane");
                Assert.AreEqual(typeof(BlueprintVisualNode), visualNode.GetType());
                Assert.AreEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.FindNearestLane").GetType());
                Assert.AreEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.DriveFollower").GetType());
            }
        }

        [Test]
        public void VehicleRoadEnumAndArrayBlackboardTypesAreKnown()
        {
            Assert.True(BlueprintVariableTypeRegistry.TryGetClrType("RoadAgentMask", out System.Type maskType));
            Assert.AreEqual(typeof(RoadAgentMask), maskType);
            Assert.AreEqual(RoadAgentMask.MotorVehicles, BlueprintTypeUtility.ConvertValue("MotorVehicles", RoadAgentMask.None));

            Assert.True(BehaviorTreeValueUtility.IsKnownBlackboardType("Array<string>"));
            Assert.True(BehaviorTreeValueUtility.IsKnownBlackboardType("VehicleRoadStopReason"));
            Assert.True(BehaviorTreeValueUtility.IsKnownBlackboardType("RoadAgentState"));

            BehaviorTreeBlackboard blackboard = new BehaviorTreeBlackboard(new[]
            {
                new BehaviorTreeBlackboardKey { Name = "Route", Type = "Array<string>", DefaultValue = new List<object>() },
                new BehaviorTreeBlackboardKey { Name = "StopReason", Type = "VehicleRoadStopReason", DefaultValue = "None" }
            });

            blackboard.SetValue("Route", new List<string> { "lane_a", "lane_b" });
            IList route = blackboard.GetValue("Route") as IList;
            Assert.NotNull(route);
            Assert.AreEqual(2, route.Count);
            Assert.AreEqual("lane_b", route[1]);

            blackboard.SetValue("StopReason", "Queue");
            Assert.AreEqual(VehicleRoadStopReason.Queue, blackboard.GetValue("StopReason"));
        }

        [Test]
        public void VehicleRoadBehaviorTreeSurfacesCompileAndDoNotMoveOwnerOnMissingFollower()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            Assert.True(registry.HasNode("BT.VehicleRoad.FindNearestLane"));
            Assert.True(registry.HasNode("BT.VehicleRoad.FindLaneRoute"));
            Assert.True(registry.HasNode("BT.VehicleRoad.ComputeFollowerControl"));
            Assert.True(registry.HasNode("BT.VehicleRoad.DriveFollower"));
            Assert.True(registry.HasService("BT.VehicleRoad.UpdateRoadAgent"));

            Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.FindNearestLane").GetType());
            Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.FindLaneRoute").GetType());
            Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.ComputeFollowerControl").GetType());
            Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.DriveFollower").GetType());

            BehaviorTreeSource source = new BehaviorTreeSource
            {
                SchemaVersion = "0.1",
                Name = "VehicleRoadBehaviorTreeSurfaceTest",
                Root = "root"
            };
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "Valid", Type = "bool", DefaultValue = true });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "Route", Type = "Array<string>", DefaultValue = new List<object>() });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TotalCost", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "AgentState", Type = "RoadAgentState", DefaultValue = "Idle" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "RouteState", Type = "RoadRouteState", DefaultValue = "None" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "FailureReason", Type = "RoadQueryFailureReason", DefaultValue = "None" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "ElementKind", Type = "RoadElementKind", DefaultValue = "None" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "ElementId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "SegmentIndex", Type = "int", DefaultValue = -1 });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TargetPosition", Type = "Vector3", DefaultValue = new List<object> { 0f, 0f, 0f } });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TargetForward", Type = "Vector3", DefaultValue = new List<object> { 0f, 0f, 1f } });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TargetUp", Type = "Vector3", DefaultValue = new List<object> { 0f, 1f, 0f } });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TargetSpeed", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "RemainingDistance", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "DistanceToBoundary", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "Arrived", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "ShouldRecover", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "RecoveryPosition", Type = "Vector3", DefaultValue = new List<object> { 0f, 0f, 0f } });

            BehaviorTreeNodeSource root = AddNode(source, "root", "BT.Root");
            root.Children.Add("compute_control");
            root.Services.Add("update_agent");

            BehaviorTreeNodeSource compute = AddNode(source, "compute_control", "BT.VehicleRoad.ComputeFollowerControl");
            compute.Properties["validKey"] = "Valid";

            BehaviorTreeServiceSource service = new BehaviorTreeServiceSource
            {
                Id = "update_agent",
                TypeId = "BT.VehicleRoad.UpdateRoadAgent"
            };
            service.Properties["validKey"] = "Valid";
            service.Properties["agentStateKey"] = "AgentState";
            service.Properties["routeStateKey"] = "RouteState";
            service.Properties["failureReasonKey"] = "FailureReason";
            service.Properties["currentElementKindKey"] = "ElementKind";
            service.Properties["currentElementIdKey"] = "ElementId";
            service.Properties["routeSegmentIndexKey"] = "SegmentIndex";
            service.Properties["targetPositionKey"] = "TargetPosition";
            service.Properties["targetForwardKey"] = "TargetForward";
            service.Properties["targetUpKey"] = "TargetUp";
            service.Properties["targetSpeedKey"] = "TargetSpeed";
            service.Properties["remainingDistanceKey"] = "RemainingDistance";
            service.Properties["distanceToBoundaryKey"] = "DistanceToBoundary";
            service.Properties["arrivedKey"] = "Arrived";
            service.Properties["shouldRecoverKey"] = "ShouldRecover";
            service.Properties["recoveryPositionKey"] = "RecoveryPosition";
            source.Services.Add(service);

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject owner = new GameObject("VehicleRoadBtMissingFollowerOwner");
            try
            {
                owner.transform.position = new Vector3(3f, 0f, 2f);
                Vector3 before = owner.transform.position;
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(compileResult.Tree, owner, null);

                Assert.AreEqual(BehaviorTreeStatus.Failure, runtime.Tick(0.1f));
                Assert.AreEqual(false, runtime.Blackboard.GetValue("Valid"));
                Assert.AreEqual(before, owner.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VehicleRoadDriveFollowerCompilesAndDoesNotMoveOwnerOnMissingFollower()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateDriveFollowerSource();
            BehaviorTreeNodeSource drive = AddNode(source, "drive_follower", "BT.VehicleRoad.DriveFollower");
            ConfigureDriveFollowerOutputKeys(drive);
            source.Nodes[0].Children.Add(drive.Id);

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject owner = new GameObject("VehicleRoadBtDriveMissingFollowerOwner");
            try
            {
                owner.transform.position = new Vector3(3f, 0f, 2f);
                Vector3 before = owner.transform.position;
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(compileResult.Tree, owner, null);

                Assert.AreEqual(BehaviorTreeStatus.Failure, runtime.Tick(0.1f));
                Assert.AreEqual(false, runtime.Blackboard.GetValue("Valid"));
                Assert.AreEqual(before, owner.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VehicleRoadDriveFollowerMovesOwnerAlongStraightLane()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateDriveFollowerSource();
            BehaviorTreeNodeSource drive = AddNode(source, "drive_follower", "BT.VehicleRoad.DriveFollower");
            ConfigureDriveFollowerOutputKeys(drive);
            drive.Properties["vehicleId"] = "drive_smoke";
            drive.Properties["followBakedLanePose"] = true;
            drive.Properties["agentMask"] = "Car";
            source.Nodes[0].Children.Add(drive.Id);

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            BakedLaneNetwork network = CreateStraightLaneNetwork();
            GameObject owner = new GameObject("VehicleRoadBtDriveFollowerOwner");
            try
            {
                VehicleLaneFollower follower = owner.AddComponent<VehicleLaneFollower>();
                follower.LaneNetwork = network;
                follower.SetRoute(new[] { "lane" });

                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(compileResult.Tree, owner, null);
                Assert.AreEqual(BehaviorTreeStatus.Running, runtime.Tick(1f));
                Assert.AreEqual(true, runtime.Blackboard.GetValue("Valid"));
                Assert.That((float)runtime.Blackboard.GetValue("CurrentSpeed"), Is.GreaterThan(0f));
                Assert.That(owner.transform.position.z, Is.GreaterThan(0.1f));
                Assert.That(owner.transform.position.z, Is.LessThan(40f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(network);
            }
        }

        private static BehaviorTreeNodeSource AddNode(BehaviorTreeSource source, string id, string typeId)
        {
            BehaviorTreeNodeSource node = new BehaviorTreeNodeSource
            {
                Id = id,
                TypeId = typeId
            };
            source.Nodes.Add(node);
            return node;
        }

        private static BehaviorTreeSource CreateDriveFollowerSource()
        {
            BehaviorTreeSource source = new BehaviorTreeSource
            {
                SchemaVersion = "0.1",
                Name = "VehicleRoadDriveFollowerSurfaceTest",
                Root = "root"
            };
            AddNode(source, "root", "BT.Root");
            AddDriveFollowerBlackboard(source);
            return source;
        }

        private static void AddDriveFollowerBlackboard(BehaviorTreeSource source)
        {
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "Valid", Type = "bool", DefaultValue = true });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "CurrentLaneId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "DistanceAlongLane", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TargetSteeringAngle", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TargetSpeed", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LookAheadPoint", Type = "Vector3", DefaultValue = new List<object> { 0f, 0f, 0f } });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "RecoveryMode", Type = "VehicleLaneRecoveryMode", DefaultValue = "None" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "RecoveryPosition", Type = "Vector3", DefaultValue = new List<object> { 0f, 0f, 0f } });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LateralError", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "StopReason", Type = "VehicleRoadStopReason", DefaultValue = "None" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "PassageStatus", Type = "VehicleRoadPassageStatus", DefaultValue = "None" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "SignalState", Type = "VehicleRoadSignalState", DefaultValue = "None" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "HasStopPoint", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "StopPoint", Type = "Vector3", DefaultValue = new List<object> { 0f, 0f, 0f } });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "DistanceToStopLine", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "QueueIndex", Type = "int", DefaultValue = -1 });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "JunctionId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "ConnectorLaneId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LaneChangeStatus", Type = "VehicleRoadLaneChangeStatus", DefaultValue = "None" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LaneChangeTargetLaneId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "CurrentSpeed", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "Arrived", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LoopReset", Type = "bool", DefaultValue = false });
        }

        private static void ConfigureDriveFollowerOutputKeys(BehaviorTreeNodeSource node)
        {
            node.Properties["validKey"] = "Valid";
            node.Properties["currentLaneIdKey"] = "CurrentLaneId";
            node.Properties["distanceAlongLaneKey"] = "DistanceAlongLane";
            node.Properties["targetSteeringAngleKey"] = "TargetSteeringAngle";
            node.Properties["targetSpeedKey"] = "TargetSpeed";
            node.Properties["lookAheadPointKey"] = "LookAheadPoint";
            node.Properties["recoveryModeKey"] = "RecoveryMode";
            node.Properties["recoveryPositionKey"] = "RecoveryPosition";
            node.Properties["lateralErrorKey"] = "LateralError";
            node.Properties["stopReasonKey"] = "StopReason";
            node.Properties["passageStatusKey"] = "PassageStatus";
            node.Properties["signalStateKey"] = "SignalState";
            node.Properties["hasStopPointKey"] = "HasStopPoint";
            node.Properties["stopPointKey"] = "StopPoint";
            node.Properties["distanceToStopLineKey"] = "DistanceToStopLine";
            node.Properties["queueIndexKey"] = "QueueIndex";
            node.Properties["junctionIdKey"] = "JunctionId";
            node.Properties["connectorLaneIdKey"] = "ConnectorLaneId";
            node.Properties["laneChangeStatusKey"] = "LaneChangeStatus";
            node.Properties["laneChangeTargetLaneIdKey"] = "LaneChangeTargetLaneId";
            node.Properties["currentSpeedKey"] = "CurrentSpeed";
            node.Properties["arrivedKey"] = "Arrived";
            node.Properties["loopResetKey"] = "LoopReset";
        }

        private static BakedLaneNetwork CreateStraightLaneNetwork()
        {
            BakedLaneNetwork network = ScriptableObject.CreateInstance<BakedLaneNetwork>();
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord>
            {
                new BakedLaneRecord
                {
                    laneId = "lane",
                    sourceLaneId = "lane",
                    open = true,
                    length = 40f,
                    speedLimit = 12f,
                    firstSampleIndex = 0,
                    sampleCount = 5,
                    allowedAgents = RoadAgentMask.Car,
                    bounds = new Bounds(new Vector3(0f, 0f, 20f), new Vector3(4f, 2f, 40f))
                }
            };
            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>();
            for (int i = 0; i < 5; i++)
            {
                float distance = i * 10f;
                Vector3 position = new Vector3(0f, 0f, distance);
                samples.Add(new BakedLaneSampleRecord
                {
                    sampleId = "lane_" + i,
                    laneId = "lane",
                    order = i,
                    splinePosition = position,
                    finalPosition = position,
                    leftBoundary = position + Vector3.left * 1.75f,
                    rightBoundary = position + Vector3.right * 1.75f,
                    forward = Vector3.forward,
                    up = Vector3.up,
                    curvature = 0f,
                    distanceAlongLane = distance,
                    width = 3.5f,
                    previousSampleId = i == 0 ? string.Empty : "lane_" + (i - 1),
                    nextSampleId = i == 4 ? string.Empty : "lane_" + (i + 1),
                    valid = true
                });
            }

            network.SetData(
                "VehicleRoadDriveFollowerTest",
                1f,
                new BakedLaneSummary(),
                lanes,
                samples,
                new List<BakedLaneConnectionRecord>());
            return network;
        }
    }
}
