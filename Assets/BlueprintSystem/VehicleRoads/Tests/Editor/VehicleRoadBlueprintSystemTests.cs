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
                Assert.False(behaviorTreeRegistry.HasService("BT.VehicleRoad.UpdateRoadAgent"));

                BlueprintVisualNode visualNode = BlueprintVisualNodeFactory.Create("VehicleRoad.FindNearestLane");
                Assert.AreEqual(typeof(BlueprintVisualNode), visualNode.GetType());
                Assert.AreEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.FindNearestLane").GetType());
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
            Assert.True(registry.HasService("BT.VehicleRoad.UpdateRoadAgent"));

            Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.FindNearestLane").GetType());
            Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.FindLaneRoute").GetType());
            Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.ComputeFollowerControl").GetType());

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
    }
}
