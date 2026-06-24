using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    public enum RoadJunctionTrafficControlMode
    {
        Uncontrolled,
        PriorityYield,
        FixedSignal
    }

    public enum VehicleRoadSignalState
    {
        None,
        Green,
        Yellow,
        Red
    }

    public enum VehicleRoadStopReason
    {
        None,
        LeadVehicle,
        TrafficSignal,
        Queue,
        JunctionConflict,
        LaneChangeReservation
    }

    public enum VehicleRoadPassageStatus
    {
        None,
        NotRequired,
        Waiting,
        Granted,
        Blocked
    }

    public enum VehicleRoadLaneChangeStatus
    {
        None,
        Requested,
        Granted,
        Denied,
        Active,
        Completed,
        Cancelled
    }

    public enum BakedConnectorConflictReason
    {
        Overlap,
        SameSource,
        Merge,
        Crossing
    }

    [Serializable]
    public sealed class RoadJunctionSignalPhase
    {
        public string phaseId = "phase";
        public RoadLaneTurnMask allowedTurns = RoadLaneTurnMask.Default;
        [Min(0.1f)] public float greenDuration = 8f;
        [Min(0f)] public float yellowDuration = 2f;
        [Min(0f)] public float allRedDuration = 1f;
    }

    [Serializable]
    public sealed class BakedJunctionSignalPhaseRecord
    {
        public string phaseId = string.Empty;
        public RoadLaneTurnMask allowedTurns = RoadLaneTurnMask.Default;
        public float greenDuration = 8f;
        public float yellowDuration = 2f;
        public float allRedDuration = 1f;

        public float TotalDuration =>
            Mathf.Max(0.1f, greenDuration) + Mathf.Max(0f, yellowDuration) + Mathf.Max(0f, allRedDuration);
    }

    [Serializable]
    public sealed class BakedJunctionTrafficRecord
    {
        public string junctionId = string.Empty;
        public RoadJunctionTrafficControlMode controlMode;
        public float defaultStopLineDistance = 2f;
        public float queueSpacing = 6f;
        public float approachDetectionDistance = 18f;
        public float passageTokenDuration = 8f;
        public float releaseDistance = 2f;
        public float connectorConflictSafetyMargin = 0.5f;
        public float straightPriority = 4f;
        public float rightPriority = 4f;
        public float leftPriority = 2f;
        public float uTurnPriority = 1f;
        public List<BakedJunctionSignalPhaseRecord> signalPhases = new List<BakedJunctionSignalPhaseRecord>();
    }

    [Serializable]
    public sealed class BakedConnectorConflictRecord
    {
        public string otherConnectorLaneId = string.Empty;
        public float selfStartDistance;
        public float selfEndDistance;
        public float otherStartDistance;
        public float otherEndDistance;
        public BakedConnectorConflictReason reason;
    }

    [Serializable]
    public sealed class BakedConnectorTrafficRecord
    {
        public string junctionId = string.Empty;
        public string connectorLaneId = string.Empty;
        public string connectionId = string.Empty;
        public string fromLaneId = string.Empty;
        public string toLaneId = string.Empty;
        public RoadLaneTurn turnType;
        public float stopLineDistance = 2f;
        public string conflictConnectorLaneIds = string.Empty;
        public List<BakedConnectorConflictRecord> conflicts = new List<BakedConnectorConflictRecord>();

        public bool HasStructuredConflicts => conflicts != null && conflicts.Count > 0;

        public bool TryGetConflict(
            string otherConnectorLaneId,
            out BakedConnectorConflictRecord conflict)
        {
            conflict = null;
            if (string.IsNullOrWhiteSpace(otherConnectorLaneId) || conflicts == null)
            {
                return false;
            }

            for (int i = 0; i < conflicts.Count; i++)
            {
                BakedConnectorConflictRecord candidate = conflicts[i];
                if (candidate != null &&
                    string.Equals(candidate.otherConnectorLaneId, otherConnectorLaneId, StringComparison.Ordinal))
                {
                    conflict = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool ConflictsWith(string otherConnectorLaneId)
        {
            if (string.IsNullOrWhiteSpace(otherConnectorLaneId))
            {
                return false;
            }

            string[] values = (conflictConnectorLaneIds ?? string.Empty).Split(',');
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i].Trim(), otherConnectorLaneId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public struct VehicleRoadVehicleUpdate
    {
        public string vehicleId;
        public string laneId;
        public RoadAgentMask agentMask;
        public float distanceAlongLane;
        public float speed;
        public float length;
        public IReadOnlyList<string> routeLaneIds;
    }

    public struct VehicleRoadLeadVehicleResult
    {
        public string vehicleId;
        public string laneId;
        public float distanceAlongRoute;
        public float speed;
        public float length;
    }

    public struct VehicleRoadTrafficQuery
    {
        public string vehicleId;
        public string laneId;
        public RoadAgentMask agentMask;
        public float distanceAlongLane;
        public float speed;
        public float vehicleLength;
        public IReadOnlyList<string> routeLaneIds;
    }

    public struct VehicleRoadTrafficControlResult
    {
        public bool hasConstraint;
        public VehicleRoadStopReason stopReason;
        public VehicleRoadPassageStatus passageStatus;
        public VehicleRoadSignalState signalState;
        public string junctionId;
        public string connectorLaneId;
        public string connectionId;
        public int queueIndex;
        public float distanceToStopLine;
        public float targetSpeedLimit;
        public bool hasStopPosition;
        public Vector3 stopPosition;
        public VehicleRoadLeadVehicleResult leadVehicle;
        public VehicleRoadLaneChangeStatus laneChangeStatus;
        public string laneChangeTargetLaneId;
        public bool hasLaneChangeTargetPoint;
        public Vector3 laneChangeTargetPoint;
        public string failureReason;
    }

    public struct VehicleRoadLaneChangeRequestResult
    {
        public VehicleRoadLaneChangeStatus status;
        public string fromLaneId;
        public string targetLaneId;
        public RoadLaneAdjacentSide side;
        public float reservedDistanceAlongLane;
        public string failureReason;
    }
}
