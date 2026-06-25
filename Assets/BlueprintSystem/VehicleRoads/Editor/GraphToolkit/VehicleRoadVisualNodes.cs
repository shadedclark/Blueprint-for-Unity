using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    public abstract class VehicleRoadVisualNode : BlueprintVisualNode
    {
        protected static List<object> Vector3Default(float x, float y, float z)
        {
            return new List<object> { x, y, z };
        }

        protected void AddSubsystemInput()
        {
            AddValueInput("target", "Binding<VehicleRoadSubsystem>", true, "propertyOrConnection", "Subsystem");
            AddProperty("target", "Binding<VehicleRoadSubsystem>", true);
        }

        protected void AddAgentMaskProperty()
        {
            AddProperty("agentMask", "RoadAgentMask", false, "MotorVehicles");
        }

        protected void AddFollowerInputs()
        {
            AddValueInput("vehicleId", "string", false, "propertyOrConnection");
            AddValueInput("position", "Vector3", true, "propertyOrConnection");
            AddValueInput("forward", "Vector3", true, "propertyOrConnection");
            AddValueInput("speed", "float", false, "propertyOrConnection");
            AddValueInput("wheelBase", "float", false, "propertyOrConnection");
            AddValueInput("vehicleLength", "float", false, "propertyOrConnection");
            AddValueInput("agentMask", "RoadAgentMask", false, "propertyOrConnection");
            AddValueInput("leadVehicleDistance", "float", false, "propertyOrConnection");
            AddValueInput("leadVehicleSpeed", "float", false, "propertyOrConnection");
            AddValueInput("requestLaneChange", "bool", false, "propertyOrConnection");
            AddValueInput("requestedLaneChangeSide", "RoadLaneAdjacentSide", false, "propertyOrConnection");
            AddProperty("vehicleId", "string", false, "");
            AddProperty("position", "Vector3", false, Vector3Default(0f, 0f, 0f));
            AddProperty("forward", "Vector3", false, Vector3Default(0f, 0f, 1f));
            AddProperty("speed", "float", false, 0f);
            AddProperty("wheelBase", "float", false, 2.7f);
            AddProperty("vehicleLength", "float", false, 4.5f);
            AddAgentMaskProperty();
            AddProperty("leadVehicleDistance", "float", false, 0f);
            AddProperty("leadVehicleSpeed", "float", false, 0f);
            AddProperty("requestLaneChange", "bool", false, false);
            AddProperty("requestedLaneChangeSide", "RoadLaneAdjacentSide", false, "Right");
        }

        protected void AddTrafficOutputs()
        {
            AddValueOutput("hasConstraint", "bool");
            AddValueOutput("stopReason", "VehicleRoadStopReason");
            AddValueOutput("passageStatus", "VehicleRoadPassageStatus");
            AddValueOutput("signalState", "VehicleRoadSignalState");
            AddValueOutput("junctionId", "string");
            AddValueOutput("connectorLaneId", "string");
            AddValueOutput("connectionId", "string");
            AddValueOutput("queueIndex", "int");
            AddValueOutput("distanceToStopLine", "float");
            AddValueOutput("targetSpeedLimit", "float");
            AddValueOutput("hasStopPosition", "bool");
            AddValueOutput("stopPosition", "Vector3");
            AddValueOutput("leadVehicleId", "string");
            AddValueOutput("leadVehicleLaneId", "string");
            AddValueOutput("leadVehicleDistance", "float");
            AddValueOutput("leadVehicleSpeed", "float");
            AddValueOutput("leadVehicleLength", "float");
            AddValueOutput("laneChangeStatus", "VehicleRoadLaneChangeStatus");
            AddValueOutput("laneChangeTargetLaneId", "string");
            AddValueOutput("hasLaneChangeTargetPoint", "bool");
            AddValueOutput("laneChangeTargetPoint", "Vector3");
            AddValueOutput("failureReason", "string");
        }

        protected void AddLaneOccupancyOutputs()
        {
            AddValueOutput("valid", "bool");
            AddValueOutput("status", "VehicleRoadLaneOccupancyStatus");
            AddValueOutput("isEnterable", "bool");
            AddValueOutput("vehicleCount", "int");
            AddValueOutput("reservationCount", "int");
            AddValueOutput("occupancyRatio", "float");
            AddValueOutput("nearestForwardVehicleId", "string");
            AddValueOutput("nearestForwardDistance", "float");
            AddValueOutput("nearestRearVehicleId", "string");
            AddValueOutput("nearestRearDistance", "float");
            AddValueOutput("availableForwardGap", "float");
            AddValueOutput("availableRearGap", "float");
            AddValueOutput("failureReason", "string");
        }

        protected void AddLaneChangeRouteOutputs()
        {
            AddValueOutput("shouldRequestLaneChange", "bool");
            AddValueOutput("side", "RoadLaneAdjacentSide");
            AddValueOutput("targetLaneId", "string");
            AddValueOutput("targetDistanceAlongLane", "float");
            AddValueOutput("routeLaneIds", "Array<string>");
            AddValueOutput("totalCost", "float");
            AddValueOutput("currentRouteFound", "bool");
            AddValueOutput("currentNextLaneId", "string");
            AddValueOutput("currentOccupancyStatus", "VehicleRoadLaneOccupancyStatus");
            AddValueOutput("targetOccupancyStatus", "VehicleRoadLaneOccupancyStatus");
            AddValueOutput("reason", "VehicleRoadLaneChangeDecisionReason");
            AddValueOutput("failureReason", "string");
        }

        protected void AddFollowerOutputs()
        {
            AddValueOutput("valid", "bool");
            AddValueOutput("currentLaneId", "string");
            AddValueOutput("distanceAlongLane", "float");
            AddValueOutput("targetSteeringAngle", "float");
            AddValueOutput("targetSpeed", "float");
            AddValueOutput("lookAheadPoint", "Vector3");
            AddValueOutput("recoveryMode", "VehicleLaneRecoveryMode");
            AddValueOutput("recoveryPosition", "Vector3");
            AddValueOutput("lateralError", "float");
            AddValueOutput("stopReason", "VehicleRoadStopReason");
            AddValueOutput("passageStatus", "VehicleRoadPassageStatus");
            AddValueOutput("signalState", "VehicleRoadSignalState");
            AddValueOutput("hasStopPoint", "bool");
            AddValueOutput("stopPoint", "Vector3");
            AddValueOutput("distanceToStopLine", "float");
            AddValueOutput("queueIndex", "int");
            AddValueOutput("junctionId", "string");
            AddValueOutput("connectorLaneId", "string");
            AddValueOutput("laneChangeStatus", "VehicleRoadLaneChangeStatus");
            AddValueOutput("laneChangeTargetLaneId", "string");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.EvaluateLaneOccupancy")]
    public sealed class VehicleRoadEvaluateLaneOccupancyVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.EvaluateLaneOccupancy", "VehicleRoad Evaluate Lane Occupancy", "VehicleRoads", "Reads target lane gap and density occupancy.");
            AddSubsystemInput();
            AddValueInput("vehicleId", "string", false, "propertyOrConnection");
            AddValueInput("laneId", "string", true, "propertyOrConnection");
            AddValueInput("distanceAlongLane", "float", true, "propertyOrConnection");
            AddValueInput("agentMask", "RoadAgentMask", false, "propertyOrConnection");
            AddValueInput("vehicleLength", "float", false, "propertyOrConnection");
            AddValueInput("lookAheadDistance", "float", false, "propertyOrConnection");
            AddValueInput("requiredGap", "float", false, "propertyOrConnection");
            AddValueInput("maxOccupancyRatio", "float", false, "propertyOrConnection");
            AddLaneOccupancyOutputs();
            AddProperty("vehicleId", "string", false, "");
            AddProperty("laneId", "string", false, "");
            AddProperty("distanceAlongLane", "float", false, 0f);
            AddAgentMaskProperty();
            AddProperty("vehicleLength", "float", false, 0f);
            AddProperty("lookAheadDistance", "float", false, 30f);
            AddProperty("requiredGap", "float", false, 0f);
            AddProperty("maxOccupancyRatio", "float", false, 0.85f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.EvaluateLaneChangeRoute")]
    public sealed class VehicleRoadEvaluateLaneChangeRouteVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.EvaluateLaneChangeRoute", "VehicleRoad Evaluate Lane Change Route", "VehicleRoads", "Evaluates route-level adjacent-lane recovery.");
            AddSubsystemInput();
            AddValueInput("vehicleId", "string", false, "propertyOrConnection");
            AddValueInput("currentLaneId", "string", true, "propertyOrConnection");
            AddValueInput("destinationLaneId", "string", true, "propertyOrConnection");
            AddValueInput("currentRouteLaneIds", "Array<string>", false, "propertyOrConnection");
            AddValueInput("distanceAlongLane", "float", true, "propertyOrConnection");
            AddValueInput("agentMask", "RoadAgentMask", false, "propertyOrConnection");
            AddValueInput("vehicleLength", "float", false, "propertyOrConnection");
            AddValueInput("preferredSide", "RoadLaneAdjacentSide", false, "propertyOrConnection");
            AddValueInput("allowOppositeSide", "bool", false, "propertyOrConnection");
            AddValueInput("lookAheadDistance", "float", false, "propertyOrConnection");
            AddValueInput("requiredGap", "float", false, "propertyOrConnection");
            AddValueInput("maxOccupancyRatio", "float", false, "propertyOrConnection");
            AddLaneChangeRouteOutputs();
            AddProperty("vehicleId", "string", false, "");
            AddProperty("currentLaneId", "string", false, "");
            AddProperty("destinationLaneId", "string", false, "");
            AddProperty("currentRouteLaneIds", "Array<string>", false, new List<object>());
            AddProperty("distanceAlongLane", "float", false, 0f);
            AddAgentMaskProperty();
            AddProperty("vehicleLength", "float", false, 0f);
            AddProperty("preferredSide", "RoadLaneAdjacentSide", false, "Right");
            AddProperty("allowOppositeSide", "bool", false, true);
            AddProperty("lookAheadDistance", "float", false, 30f);
            AddProperty("requiredGap", "float", false, 0f);
            AddProperty("maxOccupancyRatio", "float", false, 0.85f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.FindNearestLane")]
    public sealed class VehicleRoadFindNearestLaneVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.FindNearestLane", "VehicleRoad Find Nearest Lane", "VehicleRoads", "Finds the closest open lane for an agent mask.");
            AddSubsystemInput();
            AddValueInput("position", "Vector3", true, "propertyOrConnection");
            AddValueInput("heading", "Vector3", true, "propertyOrConnection");
            AddValueInput("agentMask", "RoadAgentMask", false, "propertyOrConnection");
            AddValueInput("maxDistance", "float", false, "propertyOrConnection");
            AddValueInput("maxHeightDifference", "float", false, "propertyOrConnection");
            AddValueOutput("found", "bool");
            AddValueOutput("laneId", "string");
            AddValueOutput("position", "Vector3");
            AddValueOutput("forward", "Vector3");
            AddValueOutput("up", "Vector3");
            AddValueOutput("distanceAlongLane", "float");
            AddValueOutput("distanceToLane", "float");
            AddProperty("position", "Vector3", false, Vector3Default(0f, 0f, 0f));
            AddProperty("heading", "Vector3", false, Vector3Default(0f, 0f, 1f));
            AddAgentMaskProperty();
            AddProperty("maxDistance", "float", false, 0f);
            AddProperty("maxHeightDifference", "float", false, 0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.FindLaneRoute")]
    public sealed class VehicleRoadFindLaneRouteVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.FindLaneRoute", "VehicleRoad Find Lane Route", "VehicleRoads", "Finds a same-network lane route.");
            AddSubsystemInput();
            AddValueInput("startLaneId", "string", true, "propertyOrConnection");
            AddValueInput("destinationLaneId", "string", true, "propertyOrConnection");
            AddValueInput("agentMask", "RoadAgentMask", false, "propertyOrConnection");
            AddValueOutput("success", "bool");
            AddValueOutput("laneIds", "Array<string>");
            AddValueOutput("totalCost", "float");
            AddProperty("startLaneId", "string", false, "");
            AddProperty("destinationLaneId", "string", false, "");
            AddAgentMaskProperty();
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.SetLaneClosed")]
    public sealed class VehicleRoadSetLaneClosedVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.SetLaneClosed", "VehicleRoad Set Lane Closed", "VehicleRoads", "Closes or reopens a runtime lane.");
            AddExecInput("execIn");
            AddSubsystemInput();
            AddValueInput("laneId", "string", true, "propertyOrConnection");
            AddValueInput("closed", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("laneId", "string", false, "");
            AddProperty("closed", "bool", false, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.SetLaneCongestionCost")]
    public sealed class VehicleRoadSetLaneCongestionCostVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.SetLaneCongestionCost", "VehicleRoad Set Lane Congestion Cost", "VehicleRoads", "Writes a runtime route-cost penalty for a lane.");
            AddExecInput("execIn");
            AddSubsystemInput();
            AddValueInput("laneId", "string", true, "propertyOrConnection");
            AddValueInput("cost", "float", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("laneId", "string", false, "");
            AddProperty("cost", "float", false, 0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.UpdateVehicle")]
    public sealed class VehicleRoadUpdateVehicleVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.UpdateVehicle", "VehicleRoad Update Vehicle", "VehicleRoads", "Publishes a vehicle lane state to traffic runtime.");
            AddExecInput("execIn");
            AddSubsystemInput();
            AddValueInput("vehicleId", "string", true, "propertyOrConnection");
            AddValueInput("laneId", "string", true, "propertyOrConnection");
            AddValueInput("agentMask", "RoadAgentMask", false, "propertyOrConnection");
            AddValueInput("distanceAlongLane", "float", true, "propertyOrConnection");
            AddValueInput("speed", "float", false, "propertyOrConnection");
            AddValueInput("length", "float", false, "propertyOrConnection");
            AddValueInput("routeLaneIds", "Array<string>", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("vehicleId", "string", false, "");
            AddProperty("laneId", "string", false, "");
            AddAgentMaskProperty();
            AddProperty("distanceAlongLane", "float", false, 0f);
            AddProperty("speed", "float", false, 0f);
            AddProperty("length", "float", false, 0f);
            AddProperty("routeLaneIds", "Array<string>", false, new List<object>());
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.UnregisterVehicle")]
    public sealed class VehicleRoadUnregisterVehicleVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.UnregisterVehicle", "VehicleRoad Unregister Vehicle", "VehicleRoads", "Removes a vehicle from the traffic runtime.");
            AddExecInput("execIn");
            AddSubsystemInput();
            AddValueInput("vehicleId", "string", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddValueOutput("removed", "bool");
            AddProperty("vehicleId", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.EvaluateTrafficControl")]
    public sealed class VehicleRoadEvaluateTrafficControlVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.EvaluateTrafficControl", "VehicleRoad Evaluate Traffic Control", "VehicleRoads", "Evaluates stop, signal, queue, lead vehicle, and lane-change constraints.");
            AddExecInput("execIn");
            AddSubsystemInput();
            AddValueInput("vehicleId", "string", true, "propertyOrConnection");
            AddValueInput("laneId", "string", true, "propertyOrConnection");
            AddValueInput("agentMask", "RoadAgentMask", false, "propertyOrConnection");
            AddValueInput("distanceAlongLane", "float", true, "propertyOrConnection");
            AddValueInput("speed", "float", false, "propertyOrConnection");
            AddValueInput("vehicleLength", "float", false, "propertyOrConnection");
            AddValueInput("routeLaneIds", "Array<string>", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddTrafficOutputs();
            AddProperty("vehicleId", "string", false, "");
            AddProperty("laneId", "string", false, "");
            AddAgentMaskProperty();
            AddProperty("distanceAlongLane", "float", false, 0f);
            AddProperty("speed", "float", false, 0f);
            AddProperty("vehicleLength", "float", false, 0f);
            AddProperty("routeLaneIds", "Array<string>", false, new List<object>());
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.RequestLaneChange")]
    public sealed class VehicleRoadRequestLaneChangeVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.RequestLaneChange", "VehicleRoad Request Lane Change", "VehicleRoads", "Requests a traffic-runtime adjacent lane reservation.");
            AddExecInput("execIn");
            AddSubsystemInput();
            AddValueInput("vehicleId", "string", true, "propertyOrConnection");
            AddValueInput("side", "RoadLaneAdjacentSide", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddValueOutput("status", "VehicleRoadLaneChangeStatus");
            AddValueOutput("fromLaneId", "string");
            AddValueOutput("targetLaneId", "string");
            AddValueOutput("reservedDistanceAlongLane", "float");
            AddValueOutput("failureReason", "string");
            AddProperty("vehicleId", "string", false, "");
            AddProperty("side", "RoadLaneAdjacentSide", false, "Right");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.CompleteLaneChange")]
    public sealed class VehicleRoadCompleteLaneChangeVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.CompleteLaneChange", "VehicleRoad Complete Lane Change", "VehicleRoads", "Completes and clears an active lane-change reservation.");
            AddExecInput("execIn");
            AddSubsystemInput();
            AddValueInput("vehicleId", "string", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddValueOutput("completed", "bool");
            AddProperty("vehicleId", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.SetFollowerRoute")]
    public sealed class VehicleRoadSetFollowerRouteVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.SetFollowerRoute", "VehicleRoad Set Follower Route", "VehicleRoads", "Assigns a lane route to a VehicleLaneFollower.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<VehicleLaneFollower>", true, "propertyOrConnection", "Follower");
            AddValueInput("laneIds", "Array<string>", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<VehicleLaneFollower>", true);
            AddProperty("laneIds", "Array<string>", false, new List<object>());
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.ComputeFollowerControl")]
    public sealed class VehicleRoadComputeFollowerControlVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.ComputeFollowerControl", "VehicleRoad Compute Follower Control", "VehicleRoads", "Computes steering, target speed, stop, and lane-change outputs without moving the vehicle.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<VehicleLaneFollower>", true, "propertyOrConnection", "Follower");
            AddFollowerInputs();
            AddExecOutput("execOut");
            AddFollowerOutputs();
            AddProperty("target", "Binding<VehicleLaneFollower>", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("VehicleRoad.GetSubsystemSnapshot")]
    public sealed class VehicleRoadGetSubsystemSnapshotVisualNode : VehicleRoadVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("VehicleRoad.GetSubsystemSnapshot", "VehicleRoad Get Subsystem Snapshot", "VehicleRoads", "Reads flat runtime diagnostic counters from a VehicleRoadSubsystem.");
            AddSubsystemInput();
            AddValueOutput("registeredNetworkCount", "int");
            AddValueOutput("laneCount", "int");
            AddValueOutput("connectionCount", "int");
            AddValueOutput("adjacentLinkCount", "int");
            AddValueOutput("polygonCount", "int");
            AddValueOutput("portalCount", "int");
            AddValueOutput("closedLaneCount", "int");
            AddValueOutput("congestionCostCount", "int");
            AddValueOutput("signalCostCount", "int");
            AddValueOutput("registeredVehicleCount", "int");
            AddValueOutput("queuedVehicleCount", "int");
            AddValueOutput("activeTokenCount", "int");
            AddValueOutput("laneChangeReservationCount", "int");
            AddValueOutput("signalPhaseCount", "int");
            AddValueOutput("registeredRoadAgentCount", "int");
            AddValueOutput("queriesThisFrame", "int");
            AddValueOutput("routesThisFrame", "int");
            AddValueOutput("replansThisFrame", "int");
            AddValueOutput("failuresThisFrame", "int");
            AddValueOutput("lastCandidateCount", "int");
            AddValueOutput("peakCandidateCount", "int");
            AddValueOutput("lastVisitedNodeCount", "int");
            AddValueOutput("peakVisitedNodeCount", "int");
            AddValueOutput("lastRouteSegmentCount", "int");
            AddValueOutput("peakRouteSegmentCount", "int");
            AddValueOutput("diagnosticHistoryCount", "int");
            AddValueOutput("diagnosticHistoryCapacity", "int");
            AddValueOutput("diagnosticDroppedCount", "int");
            AddValueOutput("lastTrafficFailureReason", "string");
        }
    }
}
