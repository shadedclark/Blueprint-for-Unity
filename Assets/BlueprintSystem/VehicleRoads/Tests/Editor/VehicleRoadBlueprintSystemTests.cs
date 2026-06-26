using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
                "VehicleRoad.SetLaneCongestionCost",
                "VehicleRoad.UpdateVehicle",
                "VehicleRoad.UnregisterVehicle",
                "VehicleRoad.EvaluateTrafficControl",
                "VehicleRoad.EvaluateLaneOccupancy",
                "VehicleRoad.EvaluateLaneChangeRoute",
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
                string[] splitTypeIds = GetSplitFollowerTypeIds();
                for (int i = 0; i < splitTypeIds.Length; i++)
                {
                    Assert.False(behaviorTreeRegistry.HasNode(splitTypeIds[i]), splitTypeIds[i]);
                }

                string[] strategyTypeIds = GetStrategyTypeIds();
                for (int i = 0; i < strategyTypeIds.Length; i++)
                {
                    Assert.False(behaviorTreeRegistry.HasNode(strategyTypeIds[i]), strategyTypeIds[i]);
                }

                Assert.False(behaviorTreeRegistry.HasService("BT.VehicleRoad.UpdateRoadAgent"));

                BlueprintVisualNode visualNode = BlueprintVisualNodeFactory.Create("VehicleRoad.FindNearestLane");
                Assert.AreEqual(typeof(BlueprintVisualNode), visualNode.GetType());
                Assert.AreEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.FindNearestLane").GetType());
                Assert.AreEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.DriveFollower").GetType());
                for (int i = 0; i < splitTypeIds.Length; i++)
                {
                    Assert.AreEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create(splitTypeIds[i]).GetType(), splitTypeIds[i]);
                }

                for (int i = 0; i < strategyTypeIds.Length; i++)
                {
                    Assert.AreEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create(strategyTypeIds[i]).GetType(), strategyTypeIds[i]);
                }
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
            Assert.True(BehaviorTreeValueUtility.IsKnownBlackboardType("VehicleRoadLaneChangeStatus"));
            Assert.True(BehaviorTreeValueUtility.IsKnownBlackboardType("VehicleRoadLaneOccupancyStatus"));
            Assert.True(BehaviorTreeValueUtility.IsKnownBlackboardType("VehicleRoadLaneChangeDecisionReason"));
            Assert.True(BehaviorTreeValueUtility.IsKnownBlackboardType("VehicleLaneRecoveryMode"));
            Assert.True(BehaviorTreeValueUtility.IsKnownBlackboardType("RoadLaneAdjacentSide"));
            Assert.True(BehaviorTreeValueUtility.IsKnownBlackboardType("RoadAgentState"));

            BehaviorTreeBlackboard blackboard = new BehaviorTreeBlackboard(new[]
            {
                new BehaviorTreeBlackboardKey { Name = "Route", Type = "Array<string>", DefaultValue = new List<object>() },
                new BehaviorTreeBlackboardKey { Name = "StopReason", Type = "VehicleRoadStopReason", DefaultValue = "None" },
                new BehaviorTreeBlackboardKey { Name = "OccupancyStatus", Type = "VehicleRoadLaneOccupancyStatus", DefaultValue = "Unknown" },
                new BehaviorTreeBlackboardKey { Name = "DecisionReason", Type = "VehicleRoadLaneChangeDecisionReason", DefaultValue = "None" }
            });

            blackboard.SetValue("Route", new List<string> { "lane_a", "lane_b" });
            IList route = blackboard.GetValue("Route") as IList;
            Assert.NotNull(route);
            Assert.AreEqual(2, route.Count);
            Assert.AreEqual("lane_b", route[1]);

            blackboard.SetValue("StopReason", "Queue");
            Assert.AreEqual(VehicleRoadStopReason.Queue, blackboard.GetValue("StopReason"));
            blackboard.SetValue("OccupancyStatus", "UnsafeGap");
            blackboard.SetValue("DecisionReason", "Selected");
            Assert.AreEqual(VehicleRoadLaneOccupancyStatus.UnsafeGap, blackboard.GetValue("OccupancyStatus"));
            Assert.AreEqual(VehicleRoadLaneChangeDecisionReason.Selected, blackboard.GetValue("DecisionReason"));
        }

        [Test]
        public void VehicleRoadBehaviorTreeSurfacesCompileAndDoNotMoveOwnerOnMissingFollower()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            Assert.True(registry.HasNode("BT.VehicleRoad.FindNearestLane"));
            Assert.True(registry.HasNode("BT.VehicleRoad.FindLaneRoute"));
            Assert.True(registry.HasNode("BT.VehicleRoad.ComputeFollowerControl"));
            Assert.True(registry.HasNode("BT.VehicleRoad.DriveFollower"));
            string[] splitTypeIds = GetSplitFollowerTypeIds();
            for (int i = 0; i < splitTypeIds.Length; i++)
            {
                Assert.True(registry.HasNode(splitTypeIds[i]), splitTypeIds[i]);
            }

            string[] strategyTypeIds = GetStrategyTypeIds();
            for (int i = 0; i < strategyTypeIds.Length; i++)
            {
                Assert.True(registry.HasNode(strategyTypeIds[i]), strategyTypeIds[i]);
            }

            Assert.True(registry.HasService("BT.VehicleRoad.UpdateRoadAgent"));

            Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.FindNearestLane").GetType());
            Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.FindLaneRoute").GetType());
            Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.ComputeFollowerControl").GetType());
            Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create("BT.VehicleRoad.DriveFollower").GetType());
            for (int i = 0; i < splitTypeIds.Length; i++)
            {
                Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create(splitTypeIds[i]).GetType(), splitTypeIds[i]);
            }

            for (int i = 0; i < strategyTypeIds.Length; i++)
            {
                Assert.AreNotEqual(typeof(BehaviorTreeVisualNode), BehaviorTreeVisualNodeMetadata.Create(strategyTypeIds[i]).GetType(), strategyTypeIds[i]);
            }

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

        [Test]
        public void VehicleRoadSplitFollowerNodesCompileWithOutputKeys()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateDriveFollowerSource();
            BehaviorTreeNodeSource root = source.Nodes[0];
            BehaviorTreeNodeSource sequence = AddNode(source, "split_sequence", "BT.Sequence");
            root.Children.Add(sequence.Id);

            BehaviorTreeNodeSource updateSpeed = AddNode(source, "update_speed", "BT.VehicleRoad.UpdateFollowerSpeed");
            updateSpeed.Properties["currentSpeedKey"] = "CurrentSpeed";
            sequence.Children.Add(updateSpeed.Id);

            BehaviorTreeNodeSource evaluateStop = AddNode(source, "evaluate_stop", "BT.VehicleRoad.EvaluateStopPointTravel");
            evaluateStop.Properties["requestedTravelDistanceKey"] = "RequestedTravelDistance";
            evaluateStop.Properties["travelDistanceKey"] = "TravelDistance";
            evaluateStop.Properties["reachedStopPointKey"] = "ReachedStopPoint";
            sequence.Children.Add(evaluateStop.Id);

            BehaviorTreeNodeSource applyStop = AddNode(source, "apply_stop", "BT.VehicleRoad.ApplyStopPoint");
            applyStop.Properties["currentSpeedKey"] = "CurrentSpeed";
            sequence.Children.Add(applyStop.Id);

            BehaviorTreeNodeSource checkEnd = AddNode(source, "check_end", "BT.VehicleRoad.CheckFollowerRouteEnd");
            checkEnd.Properties["arrivedKey"] = "Arrived";
            sequence.Children.Add(checkEnd.Id);

            sequence.Children.Add(AddNode(source, "move_baked", "BT.VehicleRoad.MoveAlongBakedRoute").Id);
            sequence.Children.Add(AddNode(source, "move_fallback", "BT.VehicleRoad.MoveTowardLookAhead").Id);

            BehaviorTreeNodeSource captureLoop = AddNode(source, "capture_loop", "BT.VehicleRoad.CaptureLoopStart");
            captureLoop.Properties["loopStartPositionKey"] = "LoopStartPosition";
            captureLoop.Properties["loopStartEulerAnglesKey"] = "LoopStartEulerAngles";
            captureLoop.Properties["loopStartCapturedKey"] = "LoopStartCaptured";
            sequence.Children.Add(captureLoop.Id);

            BehaviorTreeNodeSource tickReset = AddNode(source, "tick_reset", "BT.VehicleRoad.TickLoopReset");
            tickReset.Properties["loopResetDurationKey"] = "LoopResetDuration";
            tickReset.Properties["loopResetKey"] = "LoopReset";
            tickReset.Properties["currentSpeedKey"] = "CurrentSpeed";
            sequence.Children.Add(tickReset.Id);
            sequence.Children.Add(AddNode(source, "unregister", "BT.VehicleRoad.UnregisterVehicle").Id);

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());
        }

        [Test]
        public void VehicleRoadStrategyNodesCompileWithOutputKeys()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateDriveFollowerSource();
            BehaviorTreeNodeSource root = source.Nodes[0];
            BehaviorTreeNodeSource sequence = AddNode(source, "strategy_sequence", "BT.Sequence");
            root.Children.Add(sequence.Id);

            BehaviorTreeNodeSource setRoute = AddNode(source, "set_route", "BT.VehicleRoad.SetFollowerRoute");
            setRoute.Properties["successKey"] = "Valid";
            sequence.Children.Add(setRoute.Id);

            BehaviorTreeNodeSource selectTarget = AddNode(source, "select_target", "BT.VehicleRoad.SelectNextRouteTarget");
            selectTarget.Properties["successKey"] = "Valid";
            selectTarget.Properties["destinationLaneIdKey"] = "DestinationLaneId";
            selectTarget.Properties["selectedIndexKey"] = "SelectedIndex";
            selectTarget.Properties["routeLaneIdsKey"] = "RouteLaneIds";
            selectTarget.Properties["totalCostKey"] = "TotalCost";
            sequence.Children.Add(selectTarget.Id);

            BehaviorTreeNodeSource updateTraffic = AddNode(source, "update_traffic", "BT.VehicleRoad.UpdateTrafficState");
            updateTraffic.Properties["updatedKey"] = "Updated";
            updateTraffic.Properties["leadVehicleFoundKey"] = "LeadVehicleFound";
            updateTraffic.Properties["leadVehicleIdKey"] = "LeadVehicleId";
            updateTraffic.Properties["leadVehicleLaneIdKey"] = "LeadVehicleLaneId";
            updateTraffic.Properties["leadVehicleDistanceKey"] = "LeadVehicleDistance";
            updateTraffic.Properties["leadVehicleSpeedKey"] = "LeadVehicleSpeed";
            updateTraffic.Properties["leadVehicleLengthKey"] = "LeadVehicleLength";
            sequence.Children.Add(updateTraffic.Id);

            BehaviorTreeNodeSource decideLaneChange = AddNode(source, "decide_lane_change", "BT.VehicleRoad.DecideLaneChange");
            decideLaneChange.Properties["requestLaneChangeKey"] = "RequestLaneChange";
            decideLaneChange.Properties["requestedLaneChangeSideKey"] = "RequestedLaneChangeSide";
            decideLaneChange.Properties["laneChangeDecisionReasonKey"] = "LaneChangeDecisionReason";
            sequence.Children.Add(decideLaneChange.Id);

            BehaviorTreeNodeSource evaluateLaneOccupancy = AddNode(source, "evaluate_lane_occupancy", "BT.VehicleRoad.EvaluateLaneOccupancy");
            evaluateLaneOccupancy.Properties["validKey"] = "Valid";
            evaluateLaneOccupancy.Properties["statusKey"] = "LaneOccupancyStatus";
            evaluateLaneOccupancy.Properties["isEnterableKey"] = "IsLaneEnterable";
            evaluateLaneOccupancy.Properties["vehicleCountKey"] = "VehicleCount";
            evaluateLaneOccupancy.Properties["reservationCountKey"] = "ReservationCount";
            evaluateLaneOccupancy.Properties["occupancyRatioKey"] = "OccupancyRatio";
            evaluateLaneOccupancy.Properties["nearestForwardVehicleIdKey"] = "NearestForwardVehicleId";
            evaluateLaneOccupancy.Properties["nearestForwardDistanceKey"] = "NearestForwardDistance";
            evaluateLaneOccupancy.Properties["nearestRearVehicleIdKey"] = "NearestRearVehicleId";
            evaluateLaneOccupancy.Properties["nearestRearDistanceKey"] = "NearestRearDistance";
            evaluateLaneOccupancy.Properties["availableForwardGapKey"] = "AvailableForwardGap";
            evaluateLaneOccupancy.Properties["availableRearGapKey"] = "AvailableRearGap";
            evaluateLaneOccupancy.Properties["failureReasonKey"] = "LaneOccupancyFailureReason";
            sequence.Children.Add(evaluateLaneOccupancy.Id);

            BehaviorTreeNodeSource evaluateLaneChangeRoute = AddNode(source, "evaluate_lane_change_route", "BT.VehicleRoad.EvaluateLaneChangeRoute");
            evaluateLaneChangeRoute.Properties["requestLaneChangeKey"] = "RequestLaneChange";
            evaluateLaneChangeRoute.Properties["requestedLaneChangeSideKey"] = "RequestedLaneChangeSide";
            evaluateLaneChangeRoute.Properties["targetLaneIdKey"] = "TargetLaneId";
            evaluateLaneChangeRoute.Properties["targetDistanceAlongLaneKey"] = "TargetDistanceAlongLane";
            evaluateLaneChangeRoute.Properties["targetRouteLaneIdsKey"] = "TargetRouteLaneIds";
            evaluateLaneChangeRoute.Properties["totalCostKey"] = "TotalCost";
            evaluateLaneChangeRoute.Properties["currentRouteFoundKey"] = "CurrentRouteFound";
            evaluateLaneChangeRoute.Properties["currentNextLaneIdKey"] = "CurrentNextLaneId";
            evaluateLaneChangeRoute.Properties["decisionReasonKey"] = "RouteLaneChangeDecisionReason";
            evaluateLaneChangeRoute.Properties["failureReasonKey"] = "RouteLaneChangeFailureReason";
            evaluateLaneChangeRoute.Properties["currentOccupancyStatusKey"] = "CurrentOccupancyStatus";
            evaluateLaneChangeRoute.Properties["targetOccupancyStatusKey"] = "TargetOccupancyStatus";
            sequence.Children.Add(evaluateLaneChangeRoute.Id);

            BehaviorTreeNodeSource requestLaneChange = AddNode(source, "request_lane_change", "BT.VehicleRoad.RequestLaneChange");
            requestLaneChange.Properties["laneChangeStatusKey"] = "LaneChangeStatus";
            requestLaneChange.Properties["laneChangeTargetLaneIdKey"] = "LaneChangeTargetLaneId";
            requestLaneChange.Properties["laneChangeReservedDistanceKey"] = "LaneChangeReservedDistance";
            requestLaneChange.Properties["laneChangeFailureReasonKey"] = "LaneChangeFailureReason";
            sequence.Children.Add(requestLaneChange.Id);

            BehaviorTreeNodeSource completeLaneChange = AddNode(source, "complete_lane_change", "BT.VehicleRoad.CompleteLaneChange");
            completeLaneChange.Properties["completedKey"] = "Completed";
            sequence.Children.Add(completeLaneChange.Id);

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());
        }

        [Test]
        public void VehicleRoadSetFollowerRouteWritesRouteToFollower()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateSingleVehicleRoadNodeSource("BT.VehicleRoad.SetFollowerRoute");
            BehaviorTreeNodeSource setRoute = source.Nodes[1];
            Bind(setRoute, "laneIds", "RouteLaneIds");
            setRoute.Properties["successKey"] = "Valid";

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject owner = new GameObject("VehicleRoadBtSetFollowerRouteOwner");
            try
            {
                VehicleLaneFollower follower = owner.AddComponent<VehicleLaneFollower>();
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(compileResult.Tree, owner, null);
                runtime.Blackboard.SetValue("RouteLaneIds", new List<string> { "lane_a", "lane_b" });

                Assert.AreEqual(BehaviorTreeStatus.Success, runtime.Tick(0.1f));
                CollectionAssert.AreEqual(new[] { "lane_a", "lane_b" }, follower.RouteLaneIds);
                Assert.AreEqual(true, runtime.Blackboard.GetValue("Valid"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VehicleRoadDecideLaneChangeWritesRequestWhenLeadVehicleBlocks()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateSingleVehicleRoadNodeSource("BT.VehicleRoad.DecideLaneChange");
            BehaviorTreeNodeSource decideLaneChange = source.Nodes[1];
            Bind(decideLaneChange, "leadVehicleFound", "LeadVehicleFound");
            Bind(decideLaneChange, "leadVehicleDistance", "LeadVehicleDistance");
            Bind(decideLaneChange, "leadVehicleSpeed", "LeadVehicleSpeed");
            Bind(decideLaneChange, "currentSpeed", "CurrentSpeed");
            Bind(decideLaneChange, "hasStopPoint", "HasStopPoint");
            Bind(decideLaneChange, "distanceToStopLine", "DistanceToStopLine");
            Bind(decideLaneChange, "recoveryMode", "RecoveryMode");
            Bind(decideLaneChange, "laneChangeStatus", "LaneChangeStatus");
            decideLaneChange.Properties["minLeadDistance"] = 20f;
            decideLaneChange.Properties["minSpeedAdvantage"] = 0.5f;
            decideLaneChange.Properties["requestLaneChangeKey"] = "RequestLaneChange";
            decideLaneChange.Properties["requestedLaneChangeSideKey"] = "RequestedLaneChangeSide";
            decideLaneChange.Properties["laneChangeDecisionReasonKey"] = "LaneChangeDecisionReason";

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject owner = new GameObject("VehicleRoadBtDecideLaneChangeOwner");
            try
            {
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(compileResult.Tree, owner, null);
                runtime.Blackboard.SetValue("LeadVehicleFound", true);
                runtime.Blackboard.SetValue("LeadVehicleDistance", 8f);
                runtime.Blackboard.SetValue("LeadVehicleSpeed", 2f);
                runtime.Blackboard.SetValue("CurrentSpeed", 6f);

                Assert.AreEqual(BehaviorTreeStatus.Success, runtime.Tick(0.1f));
                Assert.AreEqual(true, runtime.Blackboard.GetValue("RequestLaneChange"));
                Assert.AreEqual(RoadLaneAdjacentSide.Right, runtime.Blackboard.GetValue("RequestedLaneChangeSide"));
                Assert.AreEqual("LeadVehicleBlocked", runtime.Blackboard.GetValue("LaneChangeDecisionReason"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VehicleRoadEvaluateLaneChangeRouteBlocksDuplicateRecoveryAndStopPointRequests()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateSingleVehicleRoadNodeSource("BT.VehicleRoad.EvaluateLaneChangeRoute");
            BehaviorTreeNodeSource evaluateRoute = source.Nodes[1];
            Bind(evaluateRoute, "currentLaneId", "CurrentLaneId");
            Bind(evaluateRoute, "destinationLaneId", "DestinationLaneId");
            Bind(evaluateRoute, "laneChangeStatus", "LaneChangeStatus");
            Bind(evaluateRoute, "recoveryMode", "RecoveryMode");
            Bind(evaluateRoute, "hasStopPoint", "HasStopPoint");
            Bind(evaluateRoute, "distanceToStopLine", "DistanceToStopLine");
            evaluateRoute.Properties["requestLaneChangeKey"] = "RequestLaneChange";
            evaluateRoute.Properties["requestedLaneChangeSideKey"] = "RequestedLaneChangeSide";
            evaluateRoute.Properties["decisionReasonKey"] = "RouteLaneChangeDecisionReason";
            evaluateRoute.Properties["failureReasonKey"] = "RouteLaneChangeFailureReason";

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject owner = new GameObject("VehicleRoadBtEvaluateLaneChangeRouteGuardOwner");
            try
            {
                owner.AddComponent<VehicleRoadSubsystem>();
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(compileResult.Tree, owner, null);
                runtime.Blackboard.SetValue("CurrentLaneId", "current");
                runtime.Blackboard.SetValue("DestinationLaneId", "goal");

                runtime.Blackboard.SetValue("LaneChangeStatus", VehicleRoadLaneChangeStatus.Active);
                Assert.AreEqual(BehaviorTreeStatus.Success, runtime.Tick(0.1f));
                Assert.AreEqual(false, runtime.Blackboard.GetValue("RequestLaneChange"));
                Assert.AreEqual(VehicleRoadLaneChangeDecisionReason.AlreadyChanging, runtime.Blackboard.GetValue("RouteLaneChangeDecisionReason"));

                runtime.Blackboard.SetValue("LaneChangeStatus", VehicleRoadLaneChangeStatus.None);
                runtime.Blackboard.SetValue("RecoveryMode", VehicleLaneRecoveryMode.Reset);
                Assert.AreEqual(BehaviorTreeStatus.Success, runtime.Tick(0.1f));
                Assert.AreEqual(false, runtime.Blackboard.GetValue("RequestLaneChange"));
                Assert.AreEqual(VehicleRoadLaneChangeDecisionReason.RecoveryMode, runtime.Blackboard.GetValue("RouteLaneChangeDecisionReason"));

                runtime.Blackboard.SetValue("RecoveryMode", VehicleLaneRecoveryMode.None);
                runtime.Blackboard.SetValue("HasStopPoint", true);
                runtime.Blackboard.SetValue("DistanceToStopLine", 5f);
                Assert.AreEqual(BehaviorTreeStatus.Success, runtime.Tick(0.1f));
                Assert.AreEqual(false, runtime.Blackboard.GetValue("RequestLaneChange"));
                Assert.AreEqual(VehicleRoadLaneChangeDecisionReason.ApproachingStopPoint, runtime.Blackboard.GetValue("RouteLaneChangeDecisionReason"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VehicleRoadSplitFollowerSpeedStopPointAndApplyNodesUpdateOwner()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateDriveFollowerSource();
            BehaviorTreeNodeSource root = source.Nodes[0];
            BehaviorTreeNodeSource sequence = AddNode(source, "split_sequence", "BT.Sequence");
            root.Children.Add(sequence.Id);

            BehaviorTreeNodeSource updateSpeed = AddNode(source, "update_speed", "BT.VehicleRoad.UpdateFollowerSpeed");
            Bind(updateSpeed, "valid", "Valid");
            Bind(updateSpeed, "currentSpeed", "CurrentSpeed");
            updateSpeed.Properties["targetSpeed"] = 4f;
            updateSpeed.Properties["acceleration"] = 1f;
            updateSpeed.Properties["deltaTime"] = 1f;
            updateSpeed.Properties["currentSpeedKey"] = "CurrentSpeed";
            sequence.Children.Add(updateSpeed.Id);

            BehaviorTreeNodeSource evaluateStop = AddNode(source, "evaluate_stop", "BT.VehicleRoad.EvaluateStopPointTravel");
            Bind(evaluateStop, "hasStopPoint", "HasStopPoint");
            Bind(evaluateStop, "distanceToStopLine", "DistanceToStopLine");
            Bind(evaluateStop, "currentSpeed", "CurrentSpeed");
            evaluateStop.Properties["targetSpeed"] = 0f;
            evaluateStop.Properties["deltaTime"] = 1f;
            evaluateStop.Properties["stopPointApproachSpeed"] = 2f;
            evaluateStop.Properties["requestedTravelDistanceKey"] = "RequestedTravelDistance";
            evaluateStop.Properties["travelDistanceKey"] = "TravelDistance";
            evaluateStop.Properties["reachedStopPointKey"] = "ReachedStopPoint";
            sequence.Children.Add(evaluateStop.Id);

            BehaviorTreeNodeSource applyStop = AddNode(source, "apply_stop", "BT.VehicleRoad.ApplyStopPoint");
            Bind(applyStop, "reachedStopPoint", "ReachedStopPoint");
            Bind(applyStop, "stopPoint", "StopPoint");
            applyStop.Properties["currentSpeedKey"] = "CurrentSpeed";
            sequence.Children.Add(applyStop.Id);

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject owner = new GameObject("VehicleRoadBtSplitStopOwner");
            try
            {
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(compileResult.Tree, owner, null);
                runtime.Blackboard.SetValue("Valid", true);
                runtime.Blackboard.SetValue("HasStopPoint", true);
                runtime.Blackboard.SetValue("DistanceToStopLine", 0.5f);
                runtime.Blackboard.SetValue("StopPoint", new Vector3(0f, 0f, 0.5f));

                Assert.AreEqual(BehaviorTreeStatus.Success, runtime.Tick(1f));
                Assert.AreEqual(true, runtime.Blackboard.GetValue("ReachedStopPoint"));
                Assert.AreEqual(0f, (float)runtime.Blackboard.GetValue("CurrentSpeed"));
                Assert.That((float)runtime.Blackboard.GetValue("TravelDistance"), Is.EqualTo(0.5f).Within(0.001f));
                Assert.AreEqual(new Vector3(0f, 0f, 0.5f), owner.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VehicleRoadEvaluateStopPointTravelKeepsMovingAfterPassingStopLine()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateSingleVehicleRoadNodeSource("BT.VehicleRoad.EvaluateStopPointTravel");
            BehaviorTreeNodeSource evaluateStop = source.Nodes[1];
            Bind(evaluateStop, "hasStopPoint", "HasStopPoint");
            Bind(evaluateStop, "distanceToStopLine", "DistanceToStopLine");
            Bind(evaluateStop, "currentSpeed", "CurrentSpeed");
            evaluateStop.Properties["targetSpeed"] = 0f;
            evaluateStop.Properties["deltaTime"] = 1f;
            evaluateStop.Properties["stopPointApproachSpeed"] = 2f;
            evaluateStop.Properties["requestedTravelDistanceKey"] = "RequestedTravelDistance";
            evaluateStop.Properties["travelDistanceKey"] = "TravelDistance";
            evaluateStop.Properties["reachedStopPointKey"] = "ReachedStopPoint";

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject owner = new GameObject("VehicleRoadBtPassedStopLineOwner");
            try
            {
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(compileResult.Tree, owner, null);
                runtime.Blackboard.SetValue("HasStopPoint", true);
                runtime.Blackboard.SetValue("DistanceToStopLine", -0.01f);
                runtime.Blackboard.SetValue("CurrentSpeed", 3f);

                Assert.AreEqual(BehaviorTreeStatus.Success, runtime.Tick(1f));
                Assert.AreEqual(false, runtime.Blackboard.GetValue("ReachedStopPoint"));
                Assert.That((float)runtime.Blackboard.GetValue("RequestedTravelDistance"), Is.EqualTo(3f).Within(0.001f));
                Assert.That((float)runtime.Blackboard.GetValue("TravelDistance"), Is.EqualTo(3f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VehicleRoadApplyStopPointFailsWhenStopPointIsBehindOwner()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateSingleVehicleRoadNodeSource("BT.VehicleRoad.ApplyStopPoint");
            BehaviorTreeNodeSource applyStop = source.Nodes[1];
            Bind(applyStop, "reachedStopPoint", "ReachedStopPoint");
            Bind(applyStop, "stopPoint", "StopPoint");
            applyStop.Properties["currentSpeedKey"] = "CurrentSpeed";

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject owner = new GameObject("VehicleRoadBtBehindStopPointOwner");
            try
            {
                owner.transform.position = Vector3.zero;
                owner.transform.rotation = Quaternion.identity;
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(compileResult.Tree, owner, null);
                runtime.Blackboard.SetValue("ReachedStopPoint", true);
                runtime.Blackboard.SetValue("StopPoint", new Vector3(0f, 0f, -1f));
                runtime.Blackboard.SetValue("CurrentSpeed", 4f);

                Assert.AreEqual(BehaviorTreeStatus.Failure, runtime.Tick(1f));
                Assert.AreEqual(Vector3.zero, owner.transform.position);
                Assert.AreEqual(4f, (float)runtime.Blackboard.GetValue("CurrentSpeed"));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VehicleRoadTestVehicleFallbackMovementDoesNotSnapBackwardToBehindStopPoint()
        {
            GameObject owner = new GameObject("VehicleRoadDemoBehindStopPointOwner");
            try
            {
                VehicleRoadTestVehicle vehicle = owner.AddComponent<VehicleRoadTestVehicle>();
                owner.transform.position = Vector3.zero;
                owner.transform.rotation = Quaternion.identity;

                VehicleLaneFollowerOutput output = new VehicleLaneFollowerOutput
                {
                    valid = true,
                    hasStopPoint = true,
                    stopPoint = new Vector3(0f, 0f, -1f),
                    lookAheadPoint = new Vector3(0f, 0f, 10f)
                };
                FieldInfo outputField = typeof(VehicleRoadTestVehicle).GetField(
                    "<LastOutput>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(outputField);
                outputField.SetValue(vehicle, output);

                MethodInfo isStopPointAhead = typeof(VehicleRoadTestVehicle).GetMethod(
                    "IsStopPointAhead",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(isStopPointAhead);
                Assert.AreEqual(false, (bool)isStopPointAhead.Invoke(vehicle, new object[] { output.stopPoint }));

                MethodInfo moveTowardLookAhead = typeof(VehicleRoadTestVehicle).GetMethod(
                    "MoveTowardLookAhead",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(moveTowardLookAhead);
                moveTowardLookAhead.Invoke(vehicle, new object[] { 2f });

                Assert.That(owner.transform.position.z, Is.GreaterThan(1.9f));
                Assert.That(owner.transform.position.z, Is.LessThan(2.1f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VehicleRoadSplitFollowerMovementNodesMoveOwner()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource bakedSource = CreateSingleVehicleRoadNodeSource("BT.VehicleRoad.MoveAlongBakedRoute");
            BehaviorTreeNodeSource bakedMove = bakedSource.Nodes[1];
            Bind(bakedMove, "currentLaneId", "CurrentLaneId");
            Bind(bakedMove, "distanceAlongLane", "DistanceAlongLane");
            Bind(bakedMove, "travelDistance", "TravelDistance");
            bakedMove.Properties["followBakedLanePose"] = true;

            BehaviorTreeCompileResult bakedCompile = new BehaviorTreeCompiler().Compile(bakedSource, registry);
            Assert.True(bakedCompile.Success, bakedCompile.Diagnostics.ToDisplayString());

            BakedLaneNetwork network = CreateStraightLaneNetwork();
            GameObject bakedOwner = new GameObject("VehicleRoadBtSplitBakedOwner");
            try
            {
                VehicleLaneFollower follower = bakedOwner.AddComponent<VehicleLaneFollower>();
                follower.LaneNetwork = network;
                follower.SetRoute(new[] { "lane" });
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(bakedCompile.Tree, bakedOwner, null);
                runtime.Blackboard.SetValue("CurrentLaneId", "lane");
                runtime.Blackboard.SetValue("DistanceAlongLane", 0f);
                runtime.Blackboard.SetValue("TravelDistance", 5f);

                Assert.AreEqual(BehaviorTreeStatus.Success, runtime.Tick(1f));
                Assert.That(bakedOwner.transform.position.z, Is.EqualTo(5f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(bakedOwner);
                Object.DestroyImmediate(network);
            }

            BehaviorTreeSource fallbackSource = CreateSingleVehicleRoadNodeSource("BT.VehicleRoad.MoveTowardLookAhead");
            BehaviorTreeNodeSource fallbackMove = fallbackSource.Nodes[1];
            Bind(fallbackMove, "lookAheadPoint", "LookAheadPoint");
            Bind(fallbackMove, "travelDistance", "TravelDistance");
            fallbackMove.Properties["turnSpeed"] = 360f;
            fallbackMove.Properties["deltaTime"] = 1f;
            BehaviorTreeCompileResult fallbackCompile = new BehaviorTreeCompiler().Compile(fallbackSource, registry);
            Assert.True(fallbackCompile.Success, fallbackCompile.Diagnostics.ToDisplayString());

            GameObject fallbackOwner = new GameObject("VehicleRoadBtSplitFallbackOwner");
            try
            {
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(fallbackCompile.Tree, fallbackOwner, null);
                runtime.Blackboard.SetValue("LookAheadPoint", new Vector3(0f, 0f, 10f));
                runtime.Blackboard.SetValue("TravelDistance", 3f);

                Assert.AreEqual(BehaviorTreeStatus.Success, runtime.Tick(1f));
                Assert.That(fallbackOwner.transform.position.z, Is.GreaterThan(2.9f));
            }
            finally
            {
                Object.DestroyImmediate(fallbackOwner);
            }
        }

        [Test]
        public void VehicleRoadSplitFollowerLoopResetRestoresOwner()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateSingleVehicleRoadNodeSource("BT.VehicleRoad.TickLoopReset");
            BehaviorTreeNodeSource reset = source.Nodes[1];
            reset.Properties["loopRoute"] = true;
            reset.Properties["resetRequested"] = true;
            reset.Properties["loopResetDuration"] = 1f;
            reset.Properties["loopResetDelay"] = 1f;
            reset.Properties["unregisterOnReset"] = false;
            Bind(reset, "loopStartPosition", "LoopStartPosition");
            Bind(reset, "loopStartEulerAngles", "LoopStartEulerAngles");
            reset.Properties["loopResetDurationKey"] = "LoopResetDuration";
            reset.Properties["loopResetKey"] = "LoopReset";
            reset.Properties["currentSpeedKey"] = "CurrentSpeed";

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject owner = new GameObject("VehicleRoadBtSplitLoopResetOwner");
            try
            {
                owner.transform.position = new Vector3(9f, 0f, 9f);
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(compileResult.Tree, owner, null);
                runtime.Blackboard.SetValue("LoopStartPosition", new Vector3(1f, 0f, 2f));
                runtime.Blackboard.SetValue("LoopStartEulerAngles", new Vector3(0f, 90f, 0f));
                runtime.Blackboard.SetValue("CurrentSpeed", 3f);

                Assert.AreEqual(BehaviorTreeStatus.Success, runtime.Tick(1f));
                Assert.AreEqual(new Vector3(1f, 0f, 2f), owner.transform.position);
                Assert.AreEqual(true, runtime.Blackboard.GetValue("LoopReset"));
                Assert.AreEqual(0f, (float)runtime.Blackboard.GetValue("CurrentSpeed"));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VehicleRoadSplitFollowerUnregisterFailsWithoutFollowerAndDoesNotMoveOwner()
        {
            BehaviorTreeExecutorRegistry registry = BehaviorTreeExecutorRegistry.CreateDefault();
            BehaviorTreeSource source = CreateSingleVehicleRoadNodeSource("BT.VehicleRoad.UnregisterVehicle");
            source.Nodes[1].Properties["vehicleId"] = "missing";

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, registry);
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject owner = new GameObject("VehicleRoadBtSplitUnregisterOwner");
            try
            {
                owner.transform.position = new Vector3(3f, 0f, 2f);
                Vector3 before = owner.transform.position;
                BehaviorTreeRuntime runtime = new BehaviorTreeRuntime(compileResult.Tree, owner, null);

                Assert.AreEqual(BehaviorTreeStatus.Failure, runtime.Tick(0.1f));
                Assert.AreEqual(before, owner.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static string[] GetSplitFollowerTypeIds()
        {
            return new[]
            {
                "BT.VehicleRoad.UpdateFollowerSpeed",
                "BT.VehicleRoad.EvaluateStopPointTravel",
                "BT.VehicleRoad.ApplyStopPoint",
                "BT.VehicleRoad.CheckFollowerRouteEnd",
                "BT.VehicleRoad.MoveAlongBakedRoute",
                "BT.VehicleRoad.MoveTowardLookAhead",
                "BT.VehicleRoad.CaptureLoopStart",
                "BT.VehicleRoad.TickLoopReset",
                "BT.VehicleRoad.UnregisterVehicle"
            };
        }

        private static string[] GetStrategyTypeIds()
        {
            return new[]
            {
                "BT.VehicleRoad.SetFollowerRoute",
                "BT.VehicleRoad.SelectNextRouteTarget",
                "BT.VehicleRoad.UpdateTrafficState",
                "BT.VehicleRoad.DecideLaneChange",
                "BT.VehicleRoad.EvaluateLaneOccupancy",
                "BT.VehicleRoad.EvaluateLaneChangeRoute",
                "BT.VehicleRoad.RequestLaneChange",
                "BT.VehicleRoad.CompleteLaneChange"
            };
        }

        private static BehaviorTreeSource CreateSingleVehicleRoadNodeSource(string typeId)
        {
            BehaviorTreeSource source = CreateDriveFollowerSource();
            BehaviorTreeNodeSource node = AddNode(source, "vehicle_road_task", typeId);
            source.Nodes[0].Children.Add(node.Id);
            return source;
        }

        private static void Bind(BehaviorTreeNodeSource node, string inputId, string blackboardKey)
        {
            node.Inputs[inputId] = blackboardKey;
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
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LaneChangeReservedDistance", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LaneChangeFailureReason", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "RequestLaneChange", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "RequestedLaneChangeSide", Type = "RoadLaneAdjacentSide", DefaultValue = "Right" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LaneChangeDecisionReason", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "RouteLaneChangeDecisionReason", Type = "VehicleRoadLaneChangeDecisionReason", DefaultValue = "None" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LaneOccupancyStatus", Type = "VehicleRoadLaneOccupancyStatus", DefaultValue = "Unknown" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "CurrentOccupancyStatus", Type = "VehicleRoadLaneOccupancyStatus", DefaultValue = "Unknown" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TargetOccupancyStatus", Type = "VehicleRoadLaneOccupancyStatus", DefaultValue = "Unknown" });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "IsLaneEnterable", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "VehicleCount", Type = "int", DefaultValue = 0 });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "ReservationCount", Type = "int", DefaultValue = 0 });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "OccupancyRatio", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "NearestForwardVehicleId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "NearestForwardDistance", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "NearestRearVehicleId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "NearestRearDistance", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "AvailableForwardGap", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "AvailableRearGap", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LaneOccupancyFailureReason", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TargetLaneId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TargetDistanceAlongLane", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TargetRouteLaneIds", Type = "Array<string>", DefaultValue = new List<object>() });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "CurrentRouteFound", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "CurrentNextLaneId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "RouteLaneChangeFailureReason", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "Completed", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "CurrentSpeed", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "DestinationLaneId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "SelectedIndex", Type = "int", DefaultValue = -1 });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "RouteLaneIds", Type = "Array<string>", DefaultValue = new List<object>() });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "CandidateLaneIds", Type = "Array<string>", DefaultValue = new List<object>() });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TotalCost", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "Updated", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LeadVehicleFound", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LeadVehicleId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LeadVehicleLaneId", Type = "string", DefaultValue = string.Empty });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LeadVehicleDistance", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LeadVehicleSpeed", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LeadVehicleLength", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "RequestedTravelDistance", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "TravelDistance", Type = "float", DefaultValue = 0f });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "ReachedStopPoint", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "Arrived", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LoopReset", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LoopStartPosition", Type = "Vector3", DefaultValue = new List<object> { 0f, 0f, 0f } });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LoopStartEulerAngles", Type = "Vector3", DefaultValue = new List<object> { 0f, 0f, 0f } });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LoopStartCaptured", Type = "bool", DefaultValue = false });
            source.Blackboard.Add(new BehaviorTreeBlackboardKey { Name = "LoopResetDuration", Type = "float", DefaultValue = 0f });
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
