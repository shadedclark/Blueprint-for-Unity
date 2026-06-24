using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    public enum VehicleLaneRecoveryMode
    {
        None,
        SmoothReturn,
        Reset
    }

    [Serializable]
    public struct VehicleLaneFollowerInput
    {
        public string vehicleId;
        public Vector3 position;
        public Vector3 forward;
        public float speed;
        public float wheelBase;
        public float vehicleLength;
        public RoadAgentMask agentMask;
        public float leadVehicleDistance;
        public float leadVehicleSpeed;
        public bool requestLaneChange;
        public RoadLaneAdjacentSide requestedLaneChangeSide;
    }

    [Serializable]
    public struct VehicleLaneFollowerOutput
    {
        public bool valid;
        public string currentLaneId;
        public float distanceAlongLane;
        public float targetSteeringAngle;
        public float targetSpeed;
        public Vector3 lookAheadPoint;
        public VehicleLaneRecoveryMode recoveryMode;
        public Vector3 recoveryPosition;
        public Quaternion recoveryRotation;
        public float lateralError;
        public VehicleRoadStopReason stopReason;
        public VehicleRoadPassageStatus passageStatus;
        public VehicleRoadSignalState signalState;
        public bool hasStopPoint;
        public Vector3 stopPoint;
        public float distanceToStopLine;
        public int queueIndex;
        public string junctionId;
        public string connectorLaneId;
        public VehicleRoadLaneChangeStatus laneChangeStatus;
        public string laneChangeTargetLaneId;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Vehicle Road/Vehicle Lane Follower")]
    public sealed class VehicleLaneFollower : MonoBehaviour
    {
        [SerializeField] private VehicleRoadSubsystem roadSubsystem;
        [SerializeField] private BakedLaneNetwork laneNetwork;
        [SerializeField, Min(0.5f)] private float minimumLookAhead = 3f;
        [SerializeField, Min(0f)] private float speedLookAheadFactor = 0.6f;
        [SerializeField, Min(0.1f)] private float maximumLateralAcceleration = 4f;
        [SerializeField, Min(0f)] private float curvatureSpeedLimitThreshold = 0.001f;
        [SerializeField, Min(0.1f)] private float smoothRecoveryDistance = 2f;
        [SerializeField, Min(0.1f)] private float resetRecoveryDistance = 8f;
        [SerializeField, Min(0.1f)] private float nearestLaneSearchDistance = 20f;
        [SerializeField, Min(0.1f)] private float maximumHeightDifference = 3f;
        [SerializeField, Min(0f)] private float minimumFollowGap = 4f;
        [SerializeField, Min(0f)] private float followTimeHeadway = 1.2f;
        [SerializeField] private List<string> routeLaneIds = new List<string>();

        public BakedLaneNetwork LaneNetwork
        {
            get => laneNetwork;
            set => laneNetwork = value;
        }

        public VehicleRoadSubsystem RoadSubsystem
        {
            get => roadSubsystem;
            set => roadSubsystem = value;
        }

        public List<string> RouteLaneIds => routeLaneIds;

        public void SetRoute(IEnumerable<string> laneIds)
        {
            routeLaneIds.Clear();
            if (laneIds != null)
            {
                routeLaneIds.AddRange(laneIds);
            }
        }

        public bool TryEvaluateRoutePose(
            string startLaneId,
            float distance,
            out string laneId,
            out RoadLanePose pose)
        {
            return EvaluateAlongRoute(startLaneId, distance, out laneId, out pose);
        }

        public bool IsAtRouteEnd(string laneId, float distanceAlongLane, float tolerance = 0.1f)
        {
            if (routeLaneIds.Count == 0 ||
                !string.Equals(routeLaneIds[routeLaneIds.Count - 1], laneId, StringComparison.Ordinal) ||
                !TryGetLane(laneId, out BakedLaneRecord lane))
            {
                return false;
            }

            return distanceAlongLane >= Mathf.Max(0f, lane.length - Mathf.Max(0f, tolerance));
        }

        public VehicleLaneFollowerOutput ComputeControl(VehicleLaneFollowerInput input)
        {
            VehicleLaneFollowerOutput output = default;
            RoadAgentMask agentMask = NormalizeAgentMask(input.agentMask);
            if (laneNetwork == null && roadSubsystem == null)
            {
                return output;
            }

            HashSet<string> routeFilter = routeLaneIds.Count == 0
                ? null
                : new HashSet<string>(routeLaneIds, StringComparer.Ordinal);
            if (!TryFindNearestLane(input, routeFilter, agentMask, out BakedLaneNearestResult nearest))
            {
                return output;
            }

            VehicleRoadTrafficControlResult trafficControl = default;
            bool hasTrafficControl = false;
            if (roadSubsystem != null && !string.IsNullOrWhiteSpace(input.vehicleId))
            {
                if (input.requestLaneChange)
                {
                    roadSubsystem.RequestLaneChange(input.vehicleId, input.requestedLaneChangeSide);
                }

                trafficControl = roadSubsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
                {
                    vehicleId = input.vehicleId,
                    laneId = nearest.lane.laneId,
                    agentMask = agentMask,
                    distanceAlongLane = nearest.distanceAlongLane,
                    speed = input.speed,
                    vehicleLength = input.vehicleLength,
                    routeLaneIds = routeLaneIds
                });
                hasTrafficControl = true;
            }

            float lookAheadDistance = Mathf.Max(minimumLookAhead, minimumLookAhead + Mathf.Max(0f, input.speed) * speedLookAheadFactor);
            if (!EvaluateAlongRoute(nearest.lane.laneId, nearest.distanceAlongLane + lookAheadDistance, out string targetLaneId, out RoadLanePose targetPose))
            {
                return output;
            }

            if (hasTrafficControl && trafficControl.hasLaneChangeTargetPoint)
            {
                targetLaneId = trafficControl.laneChangeTargetLaneId;
                targetPose.position = trafficControl.laneChangeTargetPoint;
            }

            float curvature = Mathf.Max(0f, targetPose.curvature);
            float curvatureThreshold = Mathf.Max(0f, curvatureSpeedLimitThreshold);
            if (curvature < curvatureThreshold)
            {
                curvature = 0f;
            }

            float curvatureSpeed = curvature <= Mathf.Epsilon
                ? float.PositiveInfinity
                : Mathf.Sqrt(maximumLateralAcceleration / curvature);
            float targetSpeed = Mathf.Min(nearest.lane.speedLimit, curvatureSpeed);
            targetSpeed = ApplyFollowingLimit(targetSpeed, input);
            if (hasTrafficControl)
            {
                targetSpeed = Mathf.Min(targetSpeed, trafficControl.targetSpeedLimit);
            }

            Vector3 localTarget = Quaternion.Inverse(Quaternion.LookRotation(
                    input.forward.sqrMagnitude <= 0.0001f ? nearest.forward : input.forward.normalized,
                    nearest.up)) *
                (targetPose.position - input.position);
            float wheelBase = Mathf.Max(0.1f, input.wheelBase);
            float planarDistance = Mathf.Max(0.1f, new Vector2(localTarget.x, localTarget.z).magnitude);
            float alpha = Mathf.Atan2(localTarget.x, localTarget.z);
            float steering = Mathf.Atan2(2f * wheelBase * Mathf.Sin(alpha), planarDistance) * Mathf.Rad2Deg;
            float lateralError = nearest.distanceToLane;
            VehicleLaneRecoveryMode recoveryMode = lateralError >= resetRecoveryDistance
                ? VehicleLaneRecoveryMode.Reset
                : lateralError >= smoothRecoveryDistance
                    ? VehicleLaneRecoveryMode.SmoothReturn
                    : VehicleLaneRecoveryMode.None;

            output = new VehicleLaneFollowerOutput
            {
                valid = true,
                currentLaneId = nearest.lane.laneId,
                distanceAlongLane = nearest.distanceAlongLane,
                targetSteeringAngle = steering,
                targetSpeed = Mathf.Max(0f, targetSpeed),
                lookAheadPoint = targetPose.position,
                recoveryMode = recoveryMode,
                recoveryPosition = nearest.position,
                recoveryRotation = Quaternion.LookRotation(nearest.forward, nearest.up),
                lateralError = lateralError,
                stopReason = hasTrafficControl ? trafficControl.stopReason : VehicleRoadStopReason.None,
                passageStatus = hasTrafficControl ? trafficControl.passageStatus : VehicleRoadPassageStatus.None,
                signalState = hasTrafficControl ? trafficControl.signalState : VehicleRoadSignalState.None,
                hasStopPoint = hasTrafficControl && trafficControl.hasStopPosition,
                stopPoint = hasTrafficControl ? trafficControl.stopPosition : Vector3.zero,
                distanceToStopLine = hasTrafficControl ? trafficControl.distanceToStopLine : float.PositiveInfinity,
                queueIndex = hasTrafficControl ? trafficControl.queueIndex : -1,
                junctionId = hasTrafficControl ? trafficControl.junctionId : string.Empty,
                connectorLaneId = hasTrafficControl ? trafficControl.connectorLaneId : string.Empty,
                laneChangeStatus = hasTrafficControl ? trafficControl.laneChangeStatus : VehicleRoadLaneChangeStatus.None,
                laneChangeTargetLaneId = hasTrafficControl ? trafficControl.laneChangeTargetLaneId : string.Empty
            };
            return output;
        }

        private float ApplyFollowingLimit(float laneTargetSpeed, VehicleLaneFollowerInput input)
        {
            if (input.leadVehicleDistance <= 0f)
            {
                return laneTargetSpeed;
            }

            float desiredGap = minimumFollowGap + Mathf.Max(0f, input.speed) * followTimeHeadway;
            if (input.leadVehicleDistance >= desiredGap)
            {
                return laneTargetSpeed;
            }

            float ratio = Mathf.Clamp01(input.leadVehicleDistance / Mathf.Max(0.1f, desiredGap));
            return Mathf.Min(laneTargetSpeed, Mathf.Lerp(0f, Mathf.Max(0f, input.leadVehicleSpeed), ratio));
        }

        private bool TryFindNearestLane(
            VehicleLaneFollowerInput input,
            ISet<string> routeFilter,
            RoadAgentMask agentMask,
            out BakedLaneNearestResult nearest)
        {
            nearest = default;
            if (roadSubsystem != null)
            {
                if (roadSubsystem.TryFindNearestLane(
                        input.position,
                        input.forward,
                        agentMask,
                        nearestLaneSearchDistance,
                        maximumHeightDifference,
                        out VehicleRoadNearestResult result,
                        routeFilter))
                {
                    nearest = result.laneResult;
                    return true;
                }

                return false;
            }

            return laneNetwork != null &&
                   laneNetwork.TryFindNearestLane(
                       input.position,
                       input.forward,
                       agentMask,
                       nearestLaneSearchDistance,
                       maximumHeightDifference,
                       out nearest,
                       routeFilter);
        }

        private static RoadAgentMask NormalizeAgentMask(RoadAgentMask agentMask)
        {
            return agentMask == RoadAgentMask.None ? RoadAgentMask.MotorVehicles : agentMask;
        }

        private bool EvaluateAlongRoute(
            string startLaneId,
            float distance,
            out string laneId,
            out RoadLanePose pose)
        {
            laneId = startLaneId;
            pose = default;
            int routeIndex = routeLaneIds.FindIndex(id => string.Equals(id, startLaneId, StringComparison.Ordinal));
            int safety = 0;
            while (TryGetLane(laneId, out BakedLaneRecord lane) && distance > lane.length && safety++ < 64)
            {
                distance -= lane.length;
                if (routeIndex >= 0 && routeIndex + 1 < routeLaneIds.Count)
                {
                    laneId = routeLaneIds[++routeIndex];
                    continue;
                }

                IReadOnlyList<BakedLaneConnectionRecord> outgoing = GetOutgoingConnections(laneId);
                BakedLaneConnectionRecord next = null;
                for (int i = 0; i < outgoing.Count; i++)
                {
                    if (outgoing[i] != null &&
                        outgoing[i].open &&
                        TryGetLane(outgoing[i].toLaneId, out _))
                    {
                        next = outgoing[i];
                        break;
                    }
                }

                if (next == null)
                {
                    distance = lane.length;
                    break;
                }

                laneId = next.toLaneId;
            }

            return TryEvaluate(laneId, distance, out pose);
        }

        private bool TryGetLane(string laneId, out BakedLaneRecord lane)
        {
            if (roadSubsystem != null)
            {
                return roadSubsystem.TryGetLane(laneId, out lane);
            }

            lane = null;
            return laneNetwork != null && laneNetwork.TryGetLane(laneId, out lane);
        }

        private IReadOnlyList<BakedLaneConnectionRecord> GetOutgoingConnections(string laneId)
        {
            if (roadSubsystem != null)
            {
                return roadSubsystem.GetOutgoingConnections(laneId);
            }

            return laneNetwork != null
                ? laneNetwork.GetOutgoingConnections(laneId)
                : Array.Empty<BakedLaneConnectionRecord>();
        }

        private bool TryEvaluate(string laneId, float distance, out RoadLanePose pose)
        {
            if (roadSubsystem != null)
            {
                return roadSubsystem.TryEvaluate(laneId, distance, out pose);
            }

            pose = default;
            return laneNetwork != null && laneNetwork.TryEvaluate(laneId, distance, out pose);
        }
    }
}
