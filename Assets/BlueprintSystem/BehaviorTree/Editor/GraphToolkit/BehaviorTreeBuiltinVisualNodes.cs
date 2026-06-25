using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    internal static class BehaviorTreeVisualNodeMetadata
    {
        public const string Root = "BT.Root";
        public const string Selector = "BT.Selector";
        public const string Sequence = "BT.Sequence";
        public const string Parallel = "BT.Parallel";
        public const string RandomSelector = "BT.RandomSelector";
        public const string PrioritySelector = "BT.PrioritySelector";
        public const string WeightedSelector = "BT.WeightedSelector";
        public const string BlackboardCondition = "BT.BlackboardCondition";
        public const string CompareFloat = "BT.CompareFloat";
        public const string CompareBool = "BT.CompareBool";
        public const string ObjectIsSet = "BT.ObjectIsSet";
        public const string DistanceLessThan = "BT.DistanceLessThan";
        public const string Cooldown = "BT.Cooldown";
        public const string NavigationCondition = "BT.NavigationCondition";
        public const string SetRunnerBlackboard = "BT.SetRunnerBlackboard";
        public const string GetRunnerBlackboard = "BT.GetRunnerBlackboard";
        public const string ClearRunnerBlackboard = "BT.ClearRunnerBlackboard";
        public const string CopyRunnerBlackboard = "BT.CopyRunnerBlackboard";
        public const string RunSubtree = "BT.RunSubtree";
        public const string VehicleRoadFindNearestLane = "BT.VehicleRoad.FindNearestLane";
        public const string VehicleRoadFindLaneRoute = "BT.VehicleRoad.FindLaneRoute";
        public const string VehicleRoadSetFollowerRoute = "BT.VehicleRoad.SetFollowerRoute";
        public const string VehicleRoadSelectNextRouteTarget = "BT.VehicleRoad.SelectNextRouteTarget";
        public const string VehicleRoadComputeFollowerControl = "BT.VehicleRoad.ComputeFollowerControl";
        public const string VehicleRoadDriveFollower = "BT.VehicleRoad.DriveFollower";
        public const string VehicleRoadUpdateTrafficState = "BT.VehicleRoad.UpdateTrafficState";
        public const string VehicleRoadDecideLaneChange = "BT.VehicleRoad.DecideLaneChange";
        public const string VehicleRoadEvaluateLaneOccupancy = "BT.VehicleRoad.EvaluateLaneOccupancy";
        public const string VehicleRoadEvaluateLaneChangeRoute = "BT.VehicleRoad.EvaluateLaneChangeRoute";
        public const string VehicleRoadRequestLaneChange = "BT.VehicleRoad.RequestLaneChange";
        public const string VehicleRoadCompleteLaneChange = "BT.VehicleRoad.CompleteLaneChange";
        public const string VehicleRoadUpdateFollowerSpeed = "BT.VehicleRoad.UpdateFollowerSpeed";
        public const string VehicleRoadEvaluateStopPointTravel = "BT.VehicleRoad.EvaluateStopPointTravel";
        public const string VehicleRoadApplyStopPoint = "BT.VehicleRoad.ApplyStopPoint";
        public const string VehicleRoadCheckFollowerRouteEnd = "BT.VehicleRoad.CheckFollowerRouteEnd";
        public const string VehicleRoadMoveAlongBakedRoute = "BT.VehicleRoad.MoveAlongBakedRoute";
        public const string VehicleRoadMoveTowardLookAhead = "BT.VehicleRoad.MoveTowardLookAhead";
        public const string VehicleRoadCaptureLoopStart = "BT.VehicleRoad.CaptureLoopStart";
        public const string VehicleRoadTickLoopReset = "BT.VehicleRoad.TickLoopReset";
        public const string VehicleRoadUnregisterVehicle = "BT.VehicleRoad.UnregisterVehicle";
        public const string VehicleRoadUpdateRoadAgent = "BT.VehicleRoad.UpdateRoadAgent";

        public static BehaviorTreeVisualNode Create(string typeId)
        {
            if (!BlueprintModuleSettings.BehaviorTreeEnabled)
            {
                return new BehaviorTreeVisualNode();
            }

            switch (typeId)
            {
                case Root:
                    return new BTCompositeRootNode();
                case Selector:
                    return new BTCompositeSelectorNode();
                case Sequence:
                    return new BTCompositeSequenceNode();
                case Parallel:
                    return new BTCompositeParallelNode();
                case RandomSelector:
                    return new BTCompositeRandomSelectorNode();
                case PrioritySelector:
                    return new BTCompositePrioritySelectorNode();
                case WeightedSelector:
                    return new BTCompositeWeightedSelectorNode();
                case "BT.Wait":
                    return new BTTaskWaitNode();
                case "BT.SetBlackboard":
                    return new BTTaskSetBlackboardNode();
                case "BT.ClearBlackboard":
                    return new BTTaskClearBlackboardNode();
                case SetRunnerBlackboard:
                    return new BTTaskSetRunnerBlackboardNode();
                case GetRunnerBlackboard:
                    return new BTTaskGetRunnerBlackboardNode();
                case ClearRunnerBlackboard:
                    return new BTTaskClearRunnerBlackboardNode();
                case CopyRunnerBlackboard:
                    return new BTTaskCopyRunnerBlackboardNode();
                case RunSubtree:
                    return new BTTaskRunSubtreeNode();
                case "BT.MoveTo":
                    return new BTTaskMoveToNode();
                case "BT.StopNavigation":
                    return new BTTaskStopNavigationNode();
                case "BT.SetNavigationDestination":
                    return new BTTaskSetNavigationDestinationNode();
                case "BT.CalculateNavigationPath":
                    return new BTTaskCalculateNavigationPathNode();
                case "BT.SetNavigationPath":
                    return new BTTaskSetNavigationPathNode();
                case "BT.WaitForNavigation":
                    return new BTTaskWaitForNavigationNode();
                case "BT.PauseNavigation":
                    return new BTTaskPauseNavigationNode();
                case "BT.ResumeNavigation":
                    return new BTTaskResumeNavigationNode();
                case "BT.SampleNavMeshPosition":
                    return new BTTaskSampleNavMeshPositionNode();
                case "BT.WarpNavigation":
                    return new BTTaskWarpNavigationNode();
                case "BT.TraverseOffMeshLink":
                    return new BTTaskTraverseOffMeshLinkNode();
                case "BT.RotateTo":
                    return new BTTaskRotateToNode();
                case "BT.TriggerBlueprintEvent":
                    return new BTTaskTriggerBlueprintEventNode();
                case "BT.RunBlueprintTask":
                    return new BTTaskRunBlueprintTaskNode();
                case "BT.Log":
                    return new BTTaskLogNode();
                case VehicleRoadFindNearestLane:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadFindNearestLaneNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadFindLaneRoute:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadFindLaneRouteNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadSetFollowerRoute:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadSetFollowerRouteNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadSelectNextRouteTarget:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadSelectNextRouteTargetNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadComputeFollowerControl:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadComputeFollowerControlNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadDriveFollower:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadDriveFollowerNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadUpdateTrafficState:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadUpdateTrafficStateNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadDecideLaneChange:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadDecideLaneChangeNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadEvaluateLaneOccupancy:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadEvaluateLaneOccupancyNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadEvaluateLaneChangeRoute:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadEvaluateLaneChangeRouteNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadRequestLaneChange:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadRequestLaneChangeNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadCompleteLaneChange:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadCompleteLaneChangeNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadUpdateFollowerSpeed:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadUpdateFollowerSpeedNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadEvaluateStopPointTravel:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadEvaluateStopPointTravelNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadApplyStopPoint:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadApplyStopPointNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadCheckFollowerRouteEnd:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadCheckFollowerRouteEndNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadMoveAlongBakedRoute:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadMoveAlongBakedRouteNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadMoveTowardLookAhead:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadMoveTowardLookAheadNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadCaptureLoopStart:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadCaptureLoopStartNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadTickLoopReset:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadTickLoopResetNode()
                        : new BehaviorTreeVisualNode();
                case VehicleRoadUnregisterVehicle:
                    return BlueprintModuleSettings.VehicleRoadsEnabled
                        ? new BTTaskVehicleRoadUnregisterVehicleNode()
                        : new BehaviorTreeVisualNode();
                default:
                    return new BehaviorTreeVisualNode();
            }
        }

        public static BehaviorTreeVisualDecoratorNode CreateDecorator(string typeId)
        {
            if (!BlueprintModuleSettings.BehaviorTreeEnabled)
            {
                return new BehaviorTreeVisualDecoratorNode();
            }

            switch (typeId)
            {
                case BlackboardCondition:
                    return new BTDecoratorBlackboardConditionNode();
                case CompareFloat:
                    return new BTDecoratorCompareFloatNode();
                case CompareBool:
                    return new BTDecoratorCompareBoolNode();
                case ObjectIsSet:
                    return new BTDecoratorObjectIsSetNode();
                case DistanceLessThan:
                    return new BTDecoratorDistanceLessThanNode();
                case Cooldown:
                    return new BTDecoratorCooldownNode();
                case NavigationCondition:
                    return new BTDecoratorNavigationConditionNode();
                default:
                    return new BehaviorTreeVisualDecoratorNode();
            }
        }

        public static bool IsDecorator(string typeId)
        {
            if (!BlueprintModuleSettings.BehaviorTreeEnabled)
            {
                return false;
            }

            return typeId == BlackboardCondition ||
                   typeId == CompareFloat ||
                   typeId == CompareBool ||
                   typeId == ObjectIsSet ||
                   typeId == DistanceLessThan ||
                   typeId == Cooldown ||
                   typeId == NavigationCondition;
        }

        public static string CreateTitle(string typeId)
        {
            if (IsVehicleRoadsBehaviorTreeNodeType(typeId) && !BlueprintModuleSettings.VehicleRoadsEnabled)
            {
                return CreateFallbackTitle(typeId);
            }

            switch (typeId)
            {
                case Root:
                    return "Composite: Root";
                case Selector:
                    return "Composite: Selector";
                case Sequence:
                    return "Composite: Sequence";
                case Parallel:
                    return "Composite: Parallel";
                case RandomSelector:
                    return "Composite: Random Selector";
                case PrioritySelector:
                    return "Composite: Priority Selector";
                case WeightedSelector:
                    return "Composite: Weighted Selector";
                case "BT.Wait":
                    return "Task: Wait";
                case "BT.SetBlackboard":
                    return "Task: Set Blackboard";
                case "BT.ClearBlackboard":
                    return "Task: Clear Blackboard";
                case SetRunnerBlackboard:
                    return "Task: Set Runner Blackboard";
                case GetRunnerBlackboard:
                    return "Task: Get Runner Blackboard";
                case ClearRunnerBlackboard:
                    return "Task: Clear Runner Blackboard";
                case CopyRunnerBlackboard:
                    return "Task: Copy Runner Blackboard";
                case RunSubtree:
                    return "Task: Run Subtree";
                case "BT.MoveTo":
                    return "Task: Move To";
                case "BT.StopNavigation":
                    return "Task: Stop Navigation";
                case "BT.SetNavigationDestination":
                    return "Task: Set Navigation Destination";
                case "BT.CalculateNavigationPath":
                    return "Task: Calculate Navigation Path";
                case "BT.SetNavigationPath":
                    return "Task: Set Navigation Path";
                case "BT.WaitForNavigation":
                    return "Task: Wait For Navigation";
                case "BT.PauseNavigation":
                    return "Task: Pause Navigation";
                case "BT.ResumeNavigation":
                    return "Task: Resume Navigation";
                case "BT.SampleNavMeshPosition":
                    return "Task: Sample NavMesh Position";
                case "BT.WarpNavigation":
                    return "Task: Warp Navigation";
                case "BT.TraverseOffMeshLink":
                    return "Task: Traverse OffMeshLink";
                case "BT.RotateTo":
                    return "Task: Rotate To";
                case "BT.TriggerBlueprintEvent":
                    return "Task: Trigger Blueprint Event";
                case "BT.RunBlueprintTask":
                    return "Task: Run Blueprint Task";
                case "BT.Log":
                    return "Task: Log";
                case VehicleRoadFindNearestLane:
                    return "Task: VehicleRoad Find Nearest Lane";
                case VehicleRoadFindLaneRoute:
                    return "Task: VehicleRoad Find Lane Route";
                case VehicleRoadSetFollowerRoute:
                    return "Task: VehicleRoad Set Follower Route";
                case VehicleRoadSelectNextRouteTarget:
                    return "Task: VehicleRoad Select Next Route Target";
                case VehicleRoadComputeFollowerControl:
                    return "Task: VehicleRoad Compute Follower Control";
                case VehicleRoadDriveFollower:
                    return "Task: VehicleRoad Drive Follower";
                case VehicleRoadUpdateTrafficState:
                    return "Task: VehicleRoad Update Traffic State";
                case VehicleRoadDecideLaneChange:
                    return "Task: VehicleRoad Decide Lane Change";
                case VehicleRoadEvaluateLaneOccupancy:
                    return "Task: VehicleRoad Evaluate Lane Occupancy";
                case VehicleRoadEvaluateLaneChangeRoute:
                    return "Task: VehicleRoad Evaluate Lane Change Route";
                case VehicleRoadRequestLaneChange:
                    return "Task: VehicleRoad Request Lane Change";
                case VehicleRoadCompleteLaneChange:
                    return "Task: VehicleRoad Complete Lane Change";
                case VehicleRoadUpdateFollowerSpeed:
                    return "Task: VehicleRoad Update Follower Speed";
                case VehicleRoadEvaluateStopPointTravel:
                    return "Task: VehicleRoad Evaluate Stop Point Travel";
                case VehicleRoadApplyStopPoint:
                    return "Task: VehicleRoad Apply Stop Point";
                case VehicleRoadCheckFollowerRouteEnd:
                    return "Task: VehicleRoad Check Follower Route End";
                case VehicleRoadMoveAlongBakedRoute:
                    return "Task: VehicleRoad Move Along Baked Route";
                case VehicleRoadMoveTowardLookAhead:
                    return "Task: VehicleRoad Move Toward Look Ahead";
                case VehicleRoadCaptureLoopStart:
                    return "Task: VehicleRoad Capture Loop Start";
                case VehicleRoadTickLoopReset:
                    return "Task: VehicleRoad Tick Loop Reset";
                case VehicleRoadUnregisterVehicle:
                    return "Task: VehicleRoad Unregister Vehicle";
                case BlackboardCondition:
                    return "Decorator: Blackboard Condition";
                case CompareFloat:
                    return "Decorator: Compare Float";
                case CompareBool:
                    return "Decorator: Compare Bool";
                case ObjectIsSet:
                    return "Decorator: Object Is Set";
                case DistanceLessThan:
                    return "Decorator: Distance Less Than";
                case Cooldown:
                    return "Decorator: Cooldown";
                case NavigationCondition:
                    return "Decorator: Navigation Condition";
                case "BT.UpdateDistance":
                    return "Service: Update Distance";
                case "BT.UpdateNavigationState":
                    return "Service: Update Navigation State";
                case "BT.PerceptionSphere":
                    return "Service: Perception Sphere";
                case "BT.PerceptionRaycast":
                    return "Service: Perception Raycast";
                case "BT.SetBlackboardFromBlueprint":
                    return "Service: Set Blackboard From Blueprint";
                case "BT.TriggerBlueprintService":
                    return "Service: Trigger Blueprint Service";
                case VehicleRoadUpdateRoadAgent:
                    return "Service: VehicleRoad Update Road Agent";
                default:
                    return CreateFallbackTitle(typeId);
            }
        }

        public static bool CanHaveChildren(string typeId)
        {
            return typeId == Root ||
                   typeId == Selector ||
                   typeId == Sequence ||
                   typeId == Parallel ||
                   typeId == RandomSelector ||
                   typeId == PrioritySelector ||
                   typeId == WeightedSelector;
        }

        public static string GetLegacyValueProperty(string typeId, string inputId)
        {
            if (typeId == "BT.Wait" && inputId == "duration")
            {
                return "seconds";
            }

            return null;
        }

        public static string GetLegacyInputBindingProperty(string typeId, string inputId)
        {
            switch (inputId)
            {
                case "key":
                    if (typeId == "BT.SetBlackboard" || typeId == "BT.ClearBlackboard")
                    {
                        return "key";
                    }

                    break;
                case "value":
                    if (typeId == "BT.SetBlackboard")
                    {
                        return "valueKey";
                    }

                    break;
                case "target":
                    if (typeId == "BT.MoveTo" ||
                        typeId == "BT.SetNavigationDestination" ||
                        typeId == "BT.CalculateNavigationPath" ||
                        typeId == "BT.WarpNavigation" ||
                        typeId == "BT.RotateTo" ||
                        typeId == "BT.TriggerBlueprintEvent" ||
                        typeId == "BT.RunBlueprintTask")
                    {
                        return "targetKey";
                    }

                    break;
                case "source":
                    if (typeId == "BT.SampleNavMeshPosition")
                    {
                        return "sourceKey";
                    }

                    break;
                case "path":
                    if (typeId == "BT.SetNavigationPath")
                    {
                        return "pathKey";
                    }

                    break;
                case "complete":
                    if (typeId == "BT.RunBlueprintTask")
                    {
                        return "completeKey";
                    }

                    break;
                case "failure":
                    if (typeId == "BT.RunBlueprintTask")
                    {
                        return "failureKey";
                    }

                    break;
            }

            return null;
        }

        public static bool TryGetDefaultInputValue(string typeId, string inputId, out object value)
        {
            value = null;
            if (IsVehicleRoadsBehaviorTreeNodeType(typeId) && !BlueprintModuleSettings.VehicleRoadsEnabled)
            {
                return false;
            }

            switch (typeId)
            {
                case "BT.Wait":
                    if (inputId == "duration")
                    {
                        value = 0f;
                        return true;
                    }

                    break;
                case "BT.MoveTo":
                    if (inputId == "targetPosition")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 0f);
                        return true;
                    }

                    if (inputId == "acceptableRadius")
                    {
                        value = 0.25f;
                        return true;
                    }

                    if (inputId == "speed")
                    {
                        value = 3f;
                        return true;
                    }

                    if (inputId == "allowTransformFallback" || inputId == "stopOnAbort")
                    {
                        value = true;
                        return true;
                    }

                    break;
                case "BT.StopNavigation":
                    if (inputId == "stopAgent")
                    {
                        value = true;
                        return true;
                    }

                    break;
                case "BT.SetNavigationDestination":
                case "BT.CalculateNavigationPath":
                case "BT.WarpNavigation":
                    if (inputId == "targetPosition")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 0f);
                        return true;
                    }

                    if (typeId == "BT.CalculateNavigationPath" && inputId == "allowPartial")
                    {
                        value = false;
                        return true;
                    }

                    break;
                case "BT.SetNavigationPath":
                    if (inputId == "allowPartial")
                    {
                        value = false;
                        return true;
                    }

                    break;
                case "BT.WaitForNavigation":
                    if (inputId == "acceptableRadius")
                    {
                        value = 0.25f;
                        return true;
                    }

                    if (inputId == "velocityThreshold")
                    {
                        value = 0.05f;
                        return true;
                    }

                    break;
                case "BT.SampleNavMeshPosition":
                    if (inputId == "sourcePosition")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 0f);
                        return true;
                    }

                    if (inputId == "maxDistance")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "areaMask")
                    {
                        value = -1;
                        return true;
                    }

                    break;
                case "BT.TraverseOffMeshLink":
                    if (inputId == "mode")
                    {
                        value = BehaviorTreeOffMeshLinkTraversalMode.Linear;
                        return true;
                    }

                    if (inputId == "duration")
                    {
                        value = 0.5f;
                        return true;
                    }

                    if (inputId == "height")
                    {
                        value = 1f;
                        return true;
                    }

                    break;
                case "BT.RotateTo":
                    if (inputId == "targetPosition")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 0f);
                        return true;
                    }

                    if (inputId == "ignoreY")
                    {
                        value = true;
                        return true;
                    }

                    if (inputId == "angleTolerance")
                    {
                        value = 2f;
                        return true;
                    }

                    if (inputId == "rotationSpeed")
                    {
                        value = 360f;
                        return true;
                    }

                    break;
                case "BT.TriggerBlueprintEvent":
                    if (inputId == "successOnMissing")
                    {
                        value = false;
                        return true;
                    }

                    break;
                case "BT.RunBlueprintTask":
                    if (inputId == "timeout")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "complete" || inputId == "failure")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "timeoutStatus")
                    {
                        value = "Failure";
                        return true;
                    }

                    break;
                case VehicleRoadFindNearestLane:
                    if (inputId == "position")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 0f);
                        return true;
                    }

                    if (inputId == "heading")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 1f);
                        return true;
                    }

                    if (inputId == "agentMask")
                    {
                        value = VehicleRoads.RoadAgentMask.MotorVehicles;
                        return true;
                    }

                    if (inputId == "maxDistance" || inputId == "maxHeightDifference")
                    {
                        value = 0f;
                        return true;
                    }

                    break;
                case VehicleRoadFindLaneRoute:
                    if (inputId == "startLaneId" || inputId == "destinationLaneId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (inputId == "agentMask")
                    {
                        value = VehicleRoads.RoadAgentMask.MotorVehicles;
                        return true;
                    }

                    break;
                case VehicleRoadSetFollowerRoute:
                    if (inputId == "laneIds")
                    {
                        value = new List<object>();
                        return true;
                    }

                    break;
                case VehicleRoadSelectNextRouteTarget:
                    if (inputId == "currentLaneId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (inputId == "candidateLaneIds")
                    {
                        value = new List<object>();
                        return true;
                    }

                    if (inputId == "agentMask")
                    {
                        value = VehicleRoads.RoadAgentMask.MotorVehicles;
                        return true;
                    }

                    if (inputId == "selectionMode")
                    {
                        value = "Cycle";
                        return true;
                    }

                    if (inputId == "previousIndex")
                    {
                        value = -1;
                        return true;
                    }

                    break;
                case VehicleRoadComputeFollowerControl:
                    if (inputId == "position")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 0f);
                        return true;
                    }

                    if (inputId == "forward")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 1f);
                        return true;
                    }

                    if (inputId == "speed" ||
                        inputId == "leadVehicleDistance" ||
                        inputId == "leadVehicleSpeed")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "wheelBase")
                    {
                        value = 2.7f;
                        return true;
                    }

                    if (inputId == "vehicleLength")
                    {
                        value = 4.5f;
                        return true;
                    }

                    if (inputId == "agentMask")
                    {
                        value = VehicleRoads.RoadAgentMask.MotorVehicles;
                        return true;
                    }

                    if (inputId == "requestLaneChange")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "requestedLaneChangeSide")
                    {
                        value = VehicleRoads.RoadLaneAdjacentSide.Right;
                        return true;
                    }

                    break;
                case VehicleRoadDriveFollower:
                    if (inputId == "vehicleId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (inputId == "vehicleLength")
                    {
                        value = 4.5f;
                        return true;
                    }

                    if (inputId == "wheelBase")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "acceleration")
                    {
                        value = 6f;
                        return true;
                    }

                    if (inputId == "turnSpeed")
                    {
                        value = 180f;
                        return true;
                    }

                    if (inputId == "agentMask")
                    {
                        value = VehicleRoads.RoadAgentMask.Car;
                        return true;
                    }

                    if (inputId == "followBakedLanePose" ||
                        inputId == "loopRoute" ||
                        inputId == "requestLaneChange")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "stopPointApproachSpeed" ||
                        inputId == "loopResetDelay")
                    {
                        value = 2f;
                        return true;
                    }

                    if (inputId == "leadVehicleDistance" || inputId == "leadVehicleSpeed")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "requestedLaneChangeSide")
                    {
                        value = VehicleRoads.RoadLaneAdjacentSide.Right;
                        return true;
                    }

                    if (inputId == "unregisterOnAbort")
                    {
                        value = true;
                        return true;
                    }

                    break;
                case VehicleRoadUpdateTrafficState:
                    if (inputId == "vehicleId" || inputId == "laneId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (inputId == "agentMask")
                    {
                        value = VehicleRoads.RoadAgentMask.MotorVehicles;
                        return true;
                    }

                    if (inputId == "distanceAlongLane" ||
                        inputId == "speed" ||
                        inputId == "leadVehicleSearchDistance")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "vehicleLength")
                    {
                        value = 4.5f;
                        return true;
                    }

                    if (inputId == "routeLaneIds")
                    {
                        value = new List<object>();
                        return true;
                    }

                    break;
                case VehicleRoadDecideLaneChange:
                    if (inputId == "leadVehicleFound" ||
                        inputId == "hasStopPoint" ||
                        inputId == "allowActiveRequest")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "blockWhenStopping")
                    {
                        value = true;
                        return true;
                    }

                    if (inputId == "leadVehicleDistance" ||
                        inputId == "leadVehicleSpeed" ||
                        inputId == "currentSpeed" ||
                        inputId == "distanceToStopLine")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "minLeadDistance")
                    {
                        value = 14f;
                        return true;
                    }

                    if (inputId == "minSpeedAdvantage")
                    {
                        value = 1f;
                        return true;
                    }

                    if (inputId == "recoveryMode")
                    {
                        value = VehicleRoads.VehicleLaneRecoveryMode.None;
                        return true;
                    }

                    if (inputId == "laneChangeStatus")
                    {
                        value = VehicleRoads.VehicleRoadLaneChangeStatus.None;
                        return true;
                    }

                    if (inputId == "preferredSide")
                    {
                        value = VehicleRoads.RoadLaneAdjacentSide.Right;
                        return true;
                    }

                    break;
                case VehicleRoadEvaluateLaneOccupancy:
                    if (inputId == "vehicleId" || inputId == "laneId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (inputId == "agentMask")
                    {
                        value = VehicleRoads.RoadAgentMask.MotorVehicles;
                        return true;
                    }

                    if (inputId == "distanceAlongLane" ||
                        inputId == "vehicleLength" ||
                        inputId == "requiredGap")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "lookAheadDistance")
                    {
                        value = 30f;
                        return true;
                    }

                    if (inputId == "maxOccupancyRatio")
                    {
                        value = 0.85f;
                        return true;
                    }

                    break;
                case VehicleRoadEvaluateLaneChangeRoute:
                    if (inputId == "vehicleId" ||
                        inputId == "currentLaneId" ||
                        inputId == "destinationLaneId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (inputId == "currentRouteLaneIds")
                    {
                        value = new List<object>();
                        return true;
                    }

                    if (inputId == "agentMask")
                    {
                        value = VehicleRoads.RoadAgentMask.MotorVehicles;
                        return true;
                    }

                    if (inputId == "preferredSide")
                    {
                        value = VehicleRoads.RoadLaneAdjacentSide.Right;
                        return true;
                    }

                    if (inputId == "laneChangeStatus")
                    {
                        value = VehicleRoads.VehicleRoadLaneChangeStatus.None;
                        return true;
                    }

                    if (inputId == "recoveryMode")
                    {
                        value = VehicleRoads.VehicleLaneRecoveryMode.None;
                        return true;
                    }

                    if (inputId == "allowOppositeSide")
                    {
                        value = true;
                        return true;
                    }

                    if (inputId == "allowActiveRequest" ||
                        inputId == "allowDuringRecovery" ||
                        inputId == "allowNearStopPoint" ||
                        inputId == "hasStopPoint")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "distanceAlongLane" ||
                        inputId == "vehicleLength" ||
                        inputId == "requiredGap" ||
                        inputId == "distanceToStopLine")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "lookAheadDistance")
                    {
                        value = 30f;
                        return true;
                    }

                    if (inputId == "maxOccupancyRatio")
                    {
                        value = 0.85f;
                        return true;
                    }

                    if (inputId == "stopPointBlockDistance")
                    {
                        value = 14f;
                        return true;
                    }

                    break;
                case VehicleRoadRequestLaneChange:
                    if (inputId == "vehicleId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (inputId == "side")
                    {
                        value = VehicleRoads.RoadLaneAdjacentSide.Right;
                        return true;
                    }

                    break;
                case VehicleRoadCompleteLaneChange:
                    if (inputId == "vehicleId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    break;
                case VehicleRoadUpdateFollowerSpeed:
                    if (inputId == "valid")
                    {
                        value = true;
                        return true;
                    }

                    if (inputId == "currentSpeed" ||
                        inputId == "targetSpeed" ||
                        inputId == "deltaTime")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "acceleration")
                    {
                        value = 6f;
                        return true;
                    }

                    break;
                case VehicleRoadEvaluateStopPointTravel:
                    if (inputId == "hasStopPoint")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "currentSpeed" ||
                        inputId == "targetSpeed" ||
                        inputId == "distanceToStopLine" ||
                        inputId == "deltaTime")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "stopPointApproachSpeed")
                    {
                        value = 2f;
                        return true;
                    }

                    break;
                case VehicleRoadApplyStopPoint:
                    if (inputId == "reachedStopPoint")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "stopPoint")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 0f);
                        return true;
                    }

                    break;
                case VehicleRoadCheckFollowerRouteEnd:
                    if (inputId == "currentLaneId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (inputId == "distanceAlongLane")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "followBakedLanePose")
                    {
                        value = true;
                        return true;
                    }

                    break;
                case VehicleRoadMoveAlongBakedRoute:
                    if (inputId == "currentLaneId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (inputId == "distanceAlongLane" || inputId == "travelDistance")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "followBakedLanePose")
                    {
                        value = true;
                        return true;
                    }

                    break;
                case VehicleRoadMoveTowardLookAhead:
                    if (inputId == "lookAheadPoint")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 1f);
                        return true;
                    }

                    if (inputId == "travelDistance" || inputId == "deltaTime")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "turnSpeed")
                    {
                        value = 180f;
                        return true;
                    }

                    break;
                case VehicleRoadTickLoopReset:
                    if (inputId == "vehicleId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (inputId == "loopRoute" || inputId == "resetRequested")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "loopResetDuration" || inputId == "deltaTime")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "loopResetDelay")
                    {
                        value = 2f;
                        return true;
                    }

                    if (inputId == "unregisterOnReset")
                    {
                        value = true;
                        return true;
                    }

                    break;
                case VehicleRoadUnregisterVehicle:
                    if (inputId == "vehicleId")
                    {
                        value = string.Empty;
                        return true;
                    }

                    break;
                case BlackboardCondition:
                    if (inputId == "operator")
                    {
                        value = BehaviorTreeComparisonOperator.IsSet;
                        return true;
                    }

                    break;
                case CompareFloat:
                    if (inputId == "left" || inputId == "right")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "operator")
                    {
                        value = BehaviorTreeComparisonOperator.LessOrEqual;
                        return true;
                    }

                    break;
                case CompareBool:
                    if (inputId == "value")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "expected")
                    {
                        value = true;
                        return true;
                    }

                    if (inputId == "operator")
                    {
                        value = BehaviorTreeComparisonOperator.Equals;
                        return true;
                    }

                    break;
                case DistanceLessThan:
                    if (inputId == "sourcePosition" || inputId == "targetPosition")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 0f);
                        return true;
                    }

                    if (inputId == "distance" || inputId == "maxDistance")
                    {
                        value = 0f;
                        return true;
                    }

                    break;
                case Cooldown:
                    if (inputId == "duration")
                    {
                        value = 0f;
                        return true;
                    }

                    break;
                case NavigationCondition:
                    if (inputId == "condition")
                    {
                        value = BehaviorTreeNavigationCondition.AgentAvailable;
                        return true;
                    }

                    if (inputId == "invert")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "acceptableRadius")
                    {
                        value = 0.25f;
                        return true;
                    }

                    if (inputId == "velocityThreshold")
                    {
                        value = 0.05f;
                        return true;
                    }

                    break;
            }

            if (inputId == "eventName" ||
                inputId == "startEventName" ||
                inputId == "abortEventName" ||
                inputId == "message" ||
                inputId == "key")
            {
                value = string.Empty;
                return true;
            }

            return false;
        }

        private static bool IsVehicleRoadsBehaviorTreeNodeType(string typeId)
        {
            return typeId == VehicleRoadFindNearestLane ||
                   typeId == VehicleRoadFindLaneRoute ||
                   typeId == VehicleRoadSetFollowerRoute ||
                   typeId == VehicleRoadSelectNextRouteTarget ||
                   typeId == VehicleRoadComputeFollowerControl ||
                   typeId == VehicleRoadDriveFollower ||
                   typeId == VehicleRoadUpdateTrafficState ||
                   typeId == VehicleRoadDecideLaneChange ||
                   typeId == VehicleRoadEvaluateLaneOccupancy ||
                   typeId == VehicleRoadEvaluateLaneChangeRoute ||
                   typeId == VehicleRoadRequestLaneChange ||
                   typeId == VehicleRoadCompleteLaneChange ||
                   typeId == VehicleRoadUpdateFollowerSpeed ||
                   typeId == VehicleRoadEvaluateStopPointTravel ||
                   typeId == VehicleRoadApplyStopPoint ||
                   typeId == VehicleRoadCheckFollowerRouteEnd ||
                   typeId == VehicleRoadMoveAlongBakedRoute ||
                   typeId == VehicleRoadMoveTowardLookAhead ||
                   typeId == VehicleRoadCaptureLoopStart ||
                   typeId == VehicleRoadTickLoopReset ||
                   typeId == VehicleRoadUnregisterVehicle ||
                   typeId == VehicleRoadUpdateRoadAgent;
        }

        private static string CreateFallbackTitle(string typeId)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                return "Behavior Tree Node";
            }

            const string prefix = "BT.";
            return typeId.StartsWith(prefix, StringComparison.Ordinal) ? typeId.Substring(prefix.Length) : typeId;
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeRootNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.Root, "Composite: Root", 1);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeSelectorNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.Selector, "Composite: Selector", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeSequenceNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.Sequence, "Composite: Sequence", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeParallelNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.Parallel, "Composite: Parallel", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeRandomSelectorNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.RandomSelector, "Composite: Random Selector", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositePrioritySelectorNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.PrioritySelector, "Composite: Priority Selector", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeWeightedSelectorNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.WeightedSelector, "Composite: Weighted Selector", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskWaitNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.Wait", "Task: Wait", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("duration", "float", "Duration", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskSetBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.SetBlackboard", "Task: Set Blackboard", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("key", null, "Key", false);
            AddBlackboardInput("value", null, "Value", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskClearBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.ClearBlackboard", "Task: Clear Blackboard", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("key", null, "Key", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskSetRunnerBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.SetRunnerBlackboard, "Task: Set Runner Blackboard", 0);
            PropertiesJson = "{\"sourceKey\":\"\",\"targetKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target Runner", false);
            AddBlackboardInput("value", null, "Value", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskGetRunnerBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.GetRunnerBlackboard, "Task: Get Runner Blackboard", 0);
            PropertiesJson = "{\"sourceKey\":\"\",\"targetKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target Runner", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskClearRunnerBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.ClearRunnerBlackboard, "Task: Clear Runner Blackboard", 0);
            PropertiesJson = "{\"targetKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target Runner", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskCopyRunnerBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.CopyRunnerBlackboard, "Task: Copy Runner Blackboard", 0);
            PropertiesJson = "{\"sourceKey\":\"\",\"targetKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("sourceTarget", null, "Source Runner", false);
            AddBlackboardInput("target", null, "Target Runner", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskRunSubtreeNode : BehaviorTreeVisualNode
    {
        private const string BehaviorTreeOptionName = "behaviorTree";

        public override string ReadPropertiesJson()
        {
            Dictionary<string, object> properties = ReadProperties(base.ReadPropertiesJson());
            string behaviorTreePath;
            if (!TryReadBehaviorTreeOptionPath(out behaviorTreePath))
            {
                behaviorTreePath = ReadBehaviorTreePath(properties);
            }

            if (!string.IsNullOrEmpty(behaviorTreePath) || properties.ContainsKey(BehaviorTreeOptionName))
            {
                properties[BehaviorTreeOptionName] = behaviorTreePath ?? string.Empty;
            }

            return BlueprintJson.Serialize(properties, false);
        }

        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.RunSubtree, "Task: Run Subtree", 0);
            PropertiesJson = "{\"behaviorTree\":\"\",\"blackboardMode\":\"Shared\",\"inputMappings\":[],\"outputMappings\":[]}";
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            string behaviorTreePath = ReadBehaviorTreePathForDisplay();
            Title = CreateTitle(behaviorTreePath);
            base.OnDefineOptions(context);

            context.AddOption<BehaviorTreeAsset>(BehaviorTreeOptionName)
                .WithDisplayName("Subtree")
                .WithDefaultValue(BehaviorTreeGraphToolkitBehaviorTreeTypes.CreateGraphValue(behaviorTreePath))
                .Delayed();
        }

        private string ReadBehaviorTreePathForDisplay()
        {
            string behaviorTreePath;
            if (TryReadBehaviorTreeOptionPath(out behaviorTreePath))
            {
                return behaviorTreePath;
            }

            return ReadBehaviorTreePath(ReadProperties(PropertiesJson));
        }

        private bool TryReadBehaviorTreeOptionPath(out string behaviorTreePath)
        {
            behaviorTreePath = null;
            INodeOption option = null;
            try
            {
                option = GetNodeOptionByName(BehaviorTreeOptionName);
            }
            catch
            {
            }

            if (option != null)
            {
                BehaviorTreeAsset asset;
                if (option.TryGetValue(out asset))
                {
                    behaviorTreePath = BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(asset.Path);
                    return true;
                }

                string text;
                if (option.TryGetValue(out text))
                {
                    behaviorTreePath = BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(text);
                    return true;
                }
            }

            return false;
        }

        private static string ReadBehaviorTreePath(Dictionary<string, object> properties)
        {
            object value;
            return properties != null && properties.TryGetValue(BehaviorTreeOptionName, out value) && value != null
                ? BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(Convert.ToString(value))
                : string.Empty;
        }

        private static string CreateTitle(string behaviorTreePath)
        {
            string displayName = BehaviorTreeGraphToolkitBehaviorTreeTypes.GetDisplayName(behaviorTreePath);
            return string.IsNullOrEmpty(displayName) ? "Task: Run Subtree" : "Task: Run Subtree: " + displayName;
        }

        private static Dictionary<string, object> ReadProperties(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            try
            {
                return BlueprintJson.DeserializeObject(json);
            }
            catch (BlueprintJsonException)
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskMoveToNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.MoveTo", "Task: Move To", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target", false);
            AddBlackboardInput("targetPosition", "Vector3", "Target Position", true);
            AddBlackboardInput("acceptableRadius", "float", "Acceptable Radius", true);
            AddBlackboardInput("speed", "float", "Speed", true);
            AddBlackboardInput("allowTransformFallback", "bool", "Allow Transform Fallback", true);
            AddBlackboardInput("stopOnAbort", "bool", "Stop On Abort", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskStopNavigationNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.StopNavigation", "Task: Stop Navigation", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("stopAgent", "bool", "Stop Agent", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskSetNavigationDestinationNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.SetNavigationDestination", "Task: Set Navigation Destination", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target", false);
            AddBlackboardInput("targetPosition", "Vector3", "Target Position", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskCalculateNavigationPathNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.CalculateNavigationPath", "Task: Calculate Navigation Path", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target", false);
            AddBlackboardInput("targetPosition", "Vector3", "Target Position", true);
            AddBlackboardInput("allowPartial", "bool", "Allow Partial", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskSetNavigationPathNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.SetNavigationPath", "Task: Set Navigation Path", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("path", BehaviorTreeValueUtility.NavMeshPathTypeId, "Path", false);
            AddBlackboardInput("allowPartial", "bool", "Allow Partial", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskWaitForNavigationNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.WaitForNavigation", "Task: Wait For Navigation", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("acceptableRadius", "float", "Acceptable Radius", true);
            AddBlackboardInput("velocityThreshold", "float", "Velocity Threshold", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskPauseNavigationNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.PauseNavigation", "Task: Pause Navigation", 0);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskResumeNavigationNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.ResumeNavigation", "Task: Resume Navigation", 0);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskSampleNavMeshPositionNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.SampleNavMeshPosition", "Task: Sample NavMesh Position", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("source", null, "Source", false);
            AddBlackboardInput("sourcePosition", "Vector3", "Source Position", true);
            AddBlackboardInput("maxDistance", "float", "Max Distance", true);
            AddBlackboardInput("areaMask", "int", "Area Mask", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskWarpNavigationNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.WarpNavigation", "Task: Warp Navigation", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target", false);
            AddBlackboardInput("targetPosition", "Vector3", "Target Position", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskTraverseOffMeshLinkNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.TraverseOffMeshLink", "Task: Traverse OffMeshLink", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput(
                "mode",
                nameof(BehaviorTreeOffMeshLinkTraversalMode),
                "Mode",
                true);
            AddBlackboardInput("duration", "float", "Duration", true);
            AddBlackboardInput("height", "float", "Height", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskRotateToNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.RotateTo", "Task: Rotate To", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target", false);
            AddBlackboardInput("targetPosition", "Vector3", "Target Position", true);
            AddBlackboardInput("ignoreY", "bool", "Ignore Y", true);
            AddBlackboardInput("angleTolerance", "float", "Angle Tolerance", true);
            AddBlackboardInput("rotationSpeed", "float", "Rotation Speed", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskTriggerBlueprintEventNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.TriggerBlueprintEvent", "Task: Trigger Blueprint Event", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("eventName", "string", "Event Name", true);
            AddBlackboardInput("target", null, "Target", false);
            AddBlackboardInput("successOnMissing", "bool", "Success On Missing", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskRunBlueprintTaskNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.RunBlueprintTask", "Task: Run Blueprint Task", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("startEventName", "string", "Start Event", true);
            AddBlackboardInput("eventName", "string", "Event Name", true);
            AddBlackboardInput("target", null, "Target", false);
            AddBlackboardInput("timeout", "float", "Timeout", true);
            AddBlackboardInput("complete", "bool", "Complete", true);
            AddBlackboardInput("failure", "bool", "Failure", true);
            AddBlackboardInput("timeoutStatus", "string", "Timeout Status", true);
            AddBlackboardInput("abortEventName", "string", "Abort Event", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskLogNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.Log", "Task: Log", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("message", "string", "Message", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadFindNearestLaneNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadFindNearestLane, "Task: VehicleRoad Find Nearest Lane", 0);
            PropertiesJson = "{\"foundKey\":\"\",\"laneIdKey\":\"\",\"positionKey\":\"\",\"forwardKey\":\"\",\"upKey\":\"\",\"distanceAlongLaneKey\":\"\",\"distanceToLaneKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("subsystem", null, "Subsystem", false);
            AddBlackboardInput("position", "Vector3", "Position", true);
            AddBlackboardInput("heading", "Vector3", "Heading", true);
            AddBlackboardInput("agentMask", "RoadAgentMask", "Agent Mask", true);
            AddBlackboardInput("maxDistance", "float", "Max Distance", true);
            AddBlackboardInput("maxHeightDifference", "float", "Max Height Difference", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadFindLaneRouteNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadFindLaneRoute, "Task: VehicleRoad Find Lane Route", 0);
            PropertiesJson = "{\"routeLaneIdsKey\":\"\",\"totalCostKey\":\"\",\"successKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("subsystem", null, "Subsystem", false);
            AddBlackboardInput("startLaneId", "string", "Start Lane", true);
            AddBlackboardInput("destinationLaneId", "string", "Destination Lane", true);
            AddBlackboardInput("agentMask", "RoadAgentMask", "Agent Mask", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadSetFollowerRouteNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadSetFollowerRoute, "Task: VehicleRoad Set Follower Route", 0);
            PropertiesJson = "{\"successKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("laneIds", "Array<string>", "Lane Ids", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadSelectNextRouteTargetNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadSelectNextRouteTarget, "Task: VehicleRoad Select Next Route Target", 0);
            PropertiesJson = "{\"successKey\":\"\",\"destinationLaneIdKey\":\"\",\"selectedIndexKey\":\"\",\"routeLaneIdsKey\":\"\",\"totalCostKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("subsystem", null, "Subsystem", false);
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("currentLaneId", "string", "Current Lane", true);
            AddBlackboardInput("candidateLaneIds", "Array<string>", "Candidate Lanes", true);
            AddBlackboardInput("agentMask", "RoadAgentMask", "Agent Mask", true);
            AddBlackboardInput("selectionMode", "string", "Selection Mode", true);
            AddBlackboardInput("previousIndex", "int", "Previous Index", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadComputeFollowerControlNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadComputeFollowerControl, "Task: VehicleRoad Compute Follower Control", 0);
            PropertiesJson = "{\"validKey\":\"\",\"currentLaneIdKey\":\"\",\"distanceAlongLaneKey\":\"\",\"targetSteeringAngleKey\":\"\",\"targetSpeedKey\":\"\",\"lookAheadPointKey\":\"\",\"recoveryModeKey\":\"\",\"recoveryPositionKey\":\"\",\"lateralErrorKey\":\"\",\"stopReasonKey\":\"\",\"passageStatusKey\":\"\",\"signalStateKey\":\"\",\"hasStopPointKey\":\"\",\"stopPointKey\":\"\",\"distanceToStopLineKey\":\"\",\"queueIndexKey\":\"\",\"junctionIdKey\":\"\",\"connectorLaneIdKey\":\"\",\"laneChangeStatusKey\":\"\",\"laneChangeTargetLaneIdKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("vehicleId", "string", "Vehicle Id", true);
            AddBlackboardInput("position", "Vector3", "Position", true);
            AddBlackboardInput("forward", "Vector3", "Forward", true);
            AddBlackboardInput("speed", "float", "Speed", true);
            AddBlackboardInput("wheelBase", "float", "Wheel Base", true);
            AddBlackboardInput("vehicleLength", "float", "Vehicle Length", true);
            AddBlackboardInput("agentMask", "RoadAgentMask", "Agent Mask", true);
            AddBlackboardInput("leadVehicleDistance", "float", "Lead Distance", true);
            AddBlackboardInput("leadVehicleSpeed", "float", "Lead Speed", true);
            AddBlackboardInput("requestLaneChange", "bool", "Request Lane Change", true);
            AddBlackboardInput("requestedLaneChangeSide", "RoadLaneAdjacentSide", "Lane Change Side", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadDriveFollowerNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadDriveFollower, "Task: VehicleRoad Drive Follower", 0);
            PropertiesJson = "{\"validKey\":\"\",\"currentLaneIdKey\":\"\",\"distanceAlongLaneKey\":\"\",\"targetSteeringAngleKey\":\"\",\"targetSpeedKey\":\"\",\"lookAheadPointKey\":\"\",\"recoveryModeKey\":\"\",\"recoveryPositionKey\":\"\",\"lateralErrorKey\":\"\",\"stopReasonKey\":\"\",\"passageStatusKey\":\"\",\"signalStateKey\":\"\",\"hasStopPointKey\":\"\",\"stopPointKey\":\"\",\"distanceToStopLineKey\":\"\",\"queueIndexKey\":\"\",\"junctionIdKey\":\"\",\"connectorLaneIdKey\":\"\",\"laneChangeStatusKey\":\"\",\"laneChangeTargetLaneIdKey\":\"\",\"currentSpeedKey\":\"\",\"arrivedKey\":\"\",\"loopResetKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("vehicleId", "string", "Vehicle Id", true);
            AddBlackboardInput("vehicleLength", "float", "Vehicle Length", true);
            AddBlackboardInput("wheelBase", "float", "Wheel Base", true);
            AddBlackboardInput("acceleration", "float", "Acceleration", true);
            AddBlackboardInput("turnSpeed", "float", "Turn Speed", true);
            AddBlackboardInput("agentMask", "RoadAgentMask", "Agent Mask", true);
            AddBlackboardInput("followBakedLanePose", "bool", "Follow Baked Pose", true);
            AddBlackboardInput("stopPointApproachSpeed", "float", "Stop Approach Speed", true);
            AddBlackboardInput("loopRoute", "bool", "Loop Route", true);
            AddBlackboardInput("loopResetDelay", "float", "Loop Reset Delay", true);
            AddBlackboardInput("leadVehicleDistance", "float", "Lead Distance", true);
            AddBlackboardInput("leadVehicleSpeed", "float", "Lead Speed", true);
            AddBlackboardInput("requestLaneChange", "bool", "Request Lane Change", true);
            AddBlackboardInput("requestedLaneChangeSide", "RoadLaneAdjacentSide", "Lane Change Side", true);
            AddBlackboardInput("unregisterOnAbort", "bool", "Unregister On Abort", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadUpdateTrafficStateNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadUpdateTrafficState, "Task: VehicleRoad Update Traffic State", 0);
            PropertiesJson = "{\"updatedKey\":\"\",\"leadVehicleFoundKey\":\"\",\"leadVehicleIdKey\":\"\",\"leadVehicleLaneIdKey\":\"\",\"leadVehicleDistanceKey\":\"\",\"leadVehicleSpeedKey\":\"\",\"leadVehicleLengthKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("subsystem", null, "Subsystem", false);
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("vehicleId", "string", "Vehicle Id", true);
            AddBlackboardInput("laneId", "string", "Lane Id", true);
            AddBlackboardInput("agentMask", "RoadAgentMask", "Agent Mask", true);
            AddBlackboardInput("distanceAlongLane", "float", "Distance Along Lane", true);
            AddBlackboardInput("speed", "float", "Speed", true);
            AddBlackboardInput("vehicleLength", "float", "Vehicle Length", true);
            AddBlackboardInput("routeLaneIds", "Array<string>", "Route Lanes", true);
            AddBlackboardInput("leadVehicleSearchDistance", "float", "Lead Search Distance", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadDecideLaneChangeNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadDecideLaneChange, "Task: VehicleRoad Decide Lane Change", 0);
            PropertiesJson = "{\"requestLaneChangeKey\":\"\",\"requestedLaneChangeSideKey\":\"\",\"laneChangeDecisionReasonKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("leadVehicleFound", "bool", "Lead Found", true);
            AddBlackboardInput("leadVehicleDistance", "float", "Lead Distance", true);
            AddBlackboardInput("leadVehicleSpeed", "float", "Lead Speed", true);
            AddBlackboardInput("currentSpeed", "float", "Current Speed", true);
            AddBlackboardInput("hasStopPoint", "bool", "Has Stop Point", true);
            AddBlackboardInput("distanceToStopLine", "float", "Distance To Stop", true);
            AddBlackboardInput("recoveryMode", "VehicleLaneRecoveryMode", "Recovery Mode", true);
            AddBlackboardInput("laneChangeStatus", "VehicleRoadLaneChangeStatus", "Lane Change Status", true);
            AddBlackboardInput("minLeadDistance", "float", "Min Lead Distance", true);
            AddBlackboardInput("minSpeedAdvantage", "float", "Min Speed Advantage", true);
            AddBlackboardInput("blockWhenStopping", "bool", "Block When Stopping", true);
            AddBlackboardInput("preferredSide", "RoadLaneAdjacentSide", "Preferred Side", true);
            AddBlackboardInput("allowActiveRequest", "bool", "Allow Active Request", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadEvaluateLaneOccupancyNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadEvaluateLaneOccupancy, "Task: VehicleRoad Evaluate Lane Occupancy", 0);
            PropertiesJson = "{\"validKey\":\"\",\"statusKey\":\"\",\"isEnterableKey\":\"\",\"vehicleCountKey\":\"\",\"reservationCountKey\":\"\",\"occupancyRatioKey\":\"\",\"nearestForwardVehicleIdKey\":\"\",\"nearestForwardDistanceKey\":\"\",\"nearestRearVehicleIdKey\":\"\",\"nearestRearDistanceKey\":\"\",\"availableForwardGapKey\":\"\",\"availableRearGapKey\":\"\",\"failureReasonKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("subsystem", null, "Subsystem", false);
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("vehicleId", "string", "Vehicle Id", true);
            AddBlackboardInput("laneId", "string", "Lane Id", true);
            AddBlackboardInput("distanceAlongLane", "float", "Distance Along Lane", true);
            AddBlackboardInput("agentMask", "RoadAgentMask", "Agent Mask", true);
            AddBlackboardInput("vehicleLength", "float", "Vehicle Length", true);
            AddBlackboardInput("lookAheadDistance", "float", "Look Ahead Distance", true);
            AddBlackboardInput("requiredGap", "float", "Required Gap", true);
            AddBlackboardInput("maxOccupancyRatio", "float", "Max Occupancy Ratio", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadEvaluateLaneChangeRouteNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadEvaluateLaneChangeRoute, "Task: VehicleRoad Evaluate Lane Change Route", 0);
            PropertiesJson = "{\"requestLaneChangeKey\":\"\",\"requestedLaneChangeSideKey\":\"\",\"targetLaneIdKey\":\"\",\"targetDistanceAlongLaneKey\":\"\",\"targetRouteLaneIdsKey\":\"\",\"totalCostKey\":\"\",\"currentRouteFoundKey\":\"\",\"currentNextLaneIdKey\":\"\",\"decisionReasonKey\":\"\",\"failureReasonKey\":\"\",\"currentOccupancyStatusKey\":\"\",\"targetOccupancyStatusKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("subsystem", null, "Subsystem", false);
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("vehicleId", "string", "Vehicle Id", true);
            AddBlackboardInput("currentLaneId", "string", "Current Lane Id", true);
            AddBlackboardInput("destinationLaneId", "string", "Destination Lane Id", true);
            AddBlackboardInput("currentRouteLaneIds", "Array<string>", "Current Route", true);
            AddBlackboardInput("distanceAlongLane", "float", "Distance Along Lane", true);
            AddBlackboardInput("agentMask", "RoadAgentMask", "Agent Mask", true);
            AddBlackboardInput("vehicleLength", "float", "Vehicle Length", true);
            AddBlackboardInput("preferredSide", "RoadLaneAdjacentSide", "Preferred Side", true);
            AddBlackboardInput("allowOppositeSide", "bool", "Allow Opposite Side", true);
            AddBlackboardInput("laneChangeStatus", "VehicleRoadLaneChangeStatus", "Lane Change Status", true);
            AddBlackboardInput("recoveryMode", "VehicleLaneRecoveryMode", "Recovery Mode", true);
            AddBlackboardInput("hasStopPoint", "bool", "Has Stop Point", true);
            AddBlackboardInput("distanceToStopLine", "float", "Distance To Stop", true);
            AddBlackboardInput("allowActiveRequest", "bool", "Allow Active Request", true);
            AddBlackboardInput("allowDuringRecovery", "bool", "Allow During Recovery", true);
            AddBlackboardInput("allowNearStopPoint", "bool", "Allow Near Stop Point", true);
            AddBlackboardInput("stopPointBlockDistance", "float", "Stop Block Distance", true);
            AddBlackboardInput("lookAheadDistance", "float", "Look Ahead Distance", true);
            AddBlackboardInput("requiredGap", "float", "Required Gap", true);
            AddBlackboardInput("maxOccupancyRatio", "float", "Max Occupancy Ratio", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadRequestLaneChangeNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadRequestLaneChange, "Task: VehicleRoad Request Lane Change", 0);
            PropertiesJson = "{\"laneChangeStatusKey\":\"\",\"laneChangeTargetLaneIdKey\":\"\",\"laneChangeReservedDistanceKey\":\"\",\"laneChangeFailureReasonKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("subsystem", null, "Subsystem", false);
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("vehicleId", "string", "Vehicle Id", true);
            AddBlackboardInput("side", "RoadLaneAdjacentSide", "Side", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadCompleteLaneChangeNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadCompleteLaneChange, "Task: VehicleRoad Complete Lane Change", 0);
            PropertiesJson = "{\"completedKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("subsystem", null, "Subsystem", false);
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("vehicleId", "string", "Vehicle Id", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadUpdateFollowerSpeedNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadUpdateFollowerSpeed, "Task: VehicleRoad Update Follower Speed", 0);
            PropertiesJson = "{\"currentSpeedKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("valid", "bool", "Valid", true);
            AddBlackboardInput("currentSpeed", "float", "Current Speed", true);
            AddBlackboardInput("targetSpeed", "float", "Target Speed", true);
            AddBlackboardInput("acceleration", "float", "Acceleration", true);
            AddBlackboardInput("deltaTime", "float", "Delta Time", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadEvaluateStopPointTravelNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadEvaluateStopPointTravel, "Task: VehicleRoad Evaluate Stop Point Travel", 0);
            PropertiesJson = "{\"requestedTravelDistanceKey\":\"\",\"travelDistanceKey\":\"\",\"reachedStopPointKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("hasStopPoint", "bool", "Has Stop Point", true);
            AddBlackboardInput("distanceToStopLine", "float", "Distance To Stop", true);
            AddBlackboardInput("targetSpeed", "float", "Target Speed", true);
            AddBlackboardInput("currentSpeed", "float", "Current Speed", true);
            AddBlackboardInput("stopPointApproachSpeed", "float", "Stop Approach Speed", true);
            AddBlackboardInput("deltaTime", "float", "Delta Time", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadApplyStopPointNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadApplyStopPoint, "Task: VehicleRoad Apply Stop Point", 0);
            PropertiesJson = "{\"currentSpeedKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("reachedStopPoint", "bool", "Reached Stop", true);
            AddBlackboardInput("stopPoint", "Vector3", "Stop Point", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadCheckFollowerRouteEndNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadCheckFollowerRouteEnd, "Task: VehicleRoad Check Follower Route End", 0);
            PropertiesJson = "{\"arrivedKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("currentLaneId", "string", "Current Lane", true);
            AddBlackboardInput("distanceAlongLane", "float", "Distance Along Lane", true);
            AddBlackboardInput("followBakedLanePose", "bool", "Follow Baked Pose", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadMoveAlongBakedRouteNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadMoveAlongBakedRoute, "Task: VehicleRoad Move Along Baked Route", 0);
            PropertiesJson = "{}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("currentLaneId", "string", "Current Lane", true);
            AddBlackboardInput("distanceAlongLane", "float", "Distance Along Lane", true);
            AddBlackboardInput("travelDistance", "float", "Travel Distance", true);
            AddBlackboardInput("followBakedLanePose", "bool", "Follow Baked Pose", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadMoveTowardLookAheadNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadMoveTowardLookAhead, "Task: VehicleRoad Move Toward Look Ahead", 0);
            PropertiesJson = "{}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("lookAheadPoint", "Vector3", "Look Ahead Point", true);
            AddBlackboardInput("travelDistance", "float", "Travel Distance", true);
            AddBlackboardInput("turnSpeed", "float", "Turn Speed", true);
            AddBlackboardInput("deltaTime", "float", "Delta Time", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadCaptureLoopStartNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadCaptureLoopStart, "Task: VehicleRoad Capture Loop Start", 0);
            PropertiesJson = "{\"loopStartPositionKey\":\"\",\"loopStartEulerAnglesKey\":\"\",\"loopStartCapturedKey\":\"\"}";
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadTickLoopResetNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadTickLoopReset, "Task: VehicleRoad Tick Loop Reset", 0);
            PropertiesJson = "{\"loopResetDurationKey\":\"\",\"loopResetKey\":\"\",\"currentSpeedKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("vehicleId", "string", "Vehicle Id", true);
            AddBlackboardInput("loopRoute", "bool", "Loop Route", true);
            AddBlackboardInput("resetRequested", "bool", "Reset Requested", true);
            AddBlackboardInput("loopResetDuration", "float", "Reset Duration", true);
            AddBlackboardInput("loopResetDelay", "float", "Reset Delay", true);
            AddBlackboardInput("loopStartPosition", "Vector3", "Start Position", true);
            AddBlackboardInput("loopStartEulerAngles", "Vector3", "Start Rotation", true);
            AddBlackboardInput("deltaTime", "float", "Delta Time", true);
            AddBlackboardInput("unregisterOnReset", "bool", "Unregister On Reset", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskVehicleRoadUnregisterVehicleNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.VehicleRoadUnregisterVehicle, "Task: VehicleRoad Unregister Vehicle", 0);
            PropertiesJson = "{}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("subsystem", null, "Subsystem", false);
            AddBlackboardInput("follower", null, "Follower", false);
            AddBlackboardInput("vehicleId", "string", "Vehicle Id", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorBlackboardConditionNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.BlackboardCondition, "Decorator: Blackboard Condition");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("value", null, "Value", false);
            AddDecoratorInput("operator", nameof(BehaviorTreeComparisonOperator), "Operator", true);
            AddDecoratorInput("expected", null, "Expected", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorCompareFloatNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.CompareFloat, "Decorator: Compare Float");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("left", "float", "Left", true);
            AddDecoratorInput("right", "float", "Right", true);
            AddDecoratorInput("operator", nameof(BehaviorTreeComparisonOperator), "Operator", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorCompareBoolNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.CompareBool, "Decorator: Compare Bool");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("value", "bool", "Value", true);
            AddDecoratorInput("expected", "bool", "Expected", true);
            AddDecoratorInput("operator", nameof(BehaviorTreeComparisonOperator), "Operator", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorObjectIsSetNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.ObjectIsSet, "Decorator: Object Is Set");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("value", null, "Value", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorDistanceLessThanNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.DistanceLessThan, "Decorator: Distance Less Than");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("distance", "float", "Distance", true);
            AddDecoratorInput("source", null, "Source", false);
            AddDecoratorInput("sourcePosition", "Vector3", "Source Position", true);
            AddDecoratorInput("target", null, "Target", false);
            AddDecoratorInput("targetPosition", "Vector3", "Target Position", true);
            AddDecoratorInput("maxDistance", "float", "Max Distance", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorCooldownNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.Cooldown, "Decorator: Cooldown");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("duration", "float", "Duration", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorNavigationConditionNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(
                BehaviorTreeVisualNodeMetadata.NavigationCondition,
                "Decorator: Navigation Condition");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput(
                "condition",
                nameof(BehaviorTreeNavigationCondition),
                "Condition",
                true);
            AddDecoratorInput("invert", "bool", "Invert", true);
            AddDecoratorInput("acceptableRadius", "float", "Acceptable Radius", true);
            AddDecoratorInput("velocityThreshold", "float", "Velocity Threshold", true);
        }
    }
}
