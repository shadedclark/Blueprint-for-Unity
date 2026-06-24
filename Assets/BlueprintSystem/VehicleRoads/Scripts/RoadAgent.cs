using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vehicle Road/Road Network/Road Agent")]
    public sealed class RoadAgent : MonoBehaviour
    {
        [SerializeField] private string agentId = string.Empty;
        [SerializeField] private VehicleRoadSubsystem roadSubsystem;
        [SerializeField] private BakedLaneNetwork fallbackNetwork;
        [SerializeField] private RoadAgentProfile profile;
        [SerializeField] private bool registerOnEnable = true;

        [SerializeField] private RoadAgentState state = RoadAgentState.Idle;
        [SerializeField] private RoadRouteState routeState = RoadRouteState.None;
        [SerializeField] private RoadQueryFailureReason failureReason;
        [SerializeField] private Vector3 destination;
        [SerializeField] private int routeSegmentIndex = -1;
        [SerializeField] private int replanCount;
        [SerializeField] private RoadAgentControlOutput lastOutput;

        private readonly List<Vector3> polygonPath = new List<Vector3>(16);
        private RoadNetworkRouteResult route;
        private int polygonWaypointIndex;
        private string cachedPolygonId = string.Empty;

        public string AgentId => agentId;
        public VehicleRoadSubsystem RoadSubsystem
        {
            get => roadSubsystem;
            set
            {
                if (roadSubsystem == value)
                {
                    return;
                }

                Unregister();
                roadSubsystem = value;
                Register();
            }
        }

        public BakedLaneNetwork FallbackNetwork
        {
            get => fallbackNetwork;
            set => fallbackNetwork = value;
        }

        public RoadAgentProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        public RoadAgentState State => state;
        public RoadRouteState RouteState => routeState;
        public RoadQueryFailureReason FailureReason => failureReason;
        public Vector3 Destination => destination;
        public int RouteSegmentIndex => routeSegmentIndex;
        public RoadAgentControlOutput LastOutput => lastOutput;

        private void Reset()
        {
            EnsureAgentId();
        }

        private void OnValidate()
        {
            EnsureAgentId();
        }

        private void OnEnable()
        {
            EnsureAgentId();
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        public bool SetDestination(Vector3 currentPosition, Vector3 worldDestination)
        {
            destination = worldDestination;
            return PlanRoute(currentPosition, false);
        }

        public bool SetDestination(Vector3 currentPosition, in RoadLocation resolvedDestination)
        {
            if (!resolvedDestination.valid)
            {
                Fail(RoadQueryFailureReason.DestinationOutsideNetwork, currentPosition);
                return false;
            }

            destination = resolvedDestination.projectedPosition;
            return PlanRoute(currentPosition, false);
        }

        public bool Replan(Vector3 currentPosition)
        {
            replanCount++;
            return PlanRoute(currentPosition, true);
        }

        public void SetRoute(RoadNetworkRouteResult resolvedRoute)
        {
            route = resolvedRoute;
            routeSegmentIndex = resolvedRoute != null && resolvedRoute.segments.Count > 0 ? 0 : -1;
            destination = resolvedRoute == null
                ? destination
                : resolvedRoute.destination.projectedPosition;
            routeState = resolvedRoute == null ? RoadRouteState.Invalid : resolvedRoute.state;
            failureReason = resolvedRoute == null
                ? RoadQueryFailureReason.RouteNotFound
                : resolvedRoute.failureReason;
            ResetPolygonPath();
            SetState(
                routeState == RoadRouteState.Valid ? RoadAgentState.Following : RoadAgentState.Failed,
                Vector3.zero);
        }

        public void Cancel(Vector3 currentPosition)
        {
            route = null;
            routeSegmentIndex = -1;
            routeState = RoadRouteState.Cancelled;
            failureReason = RoadQueryFailureReason.Cancelled;
            ResetPolygonPath();
            SetState(RoadAgentState.Idle, currentPosition);
            lastOutput = CreateBaseOutput();
        }

        public void Suspend(Vector3 currentPosition)
        {
            SetState(RoadAgentState.Suspended, currentPosition);
            lastOutput = CreateBaseOutput();
        }

        public bool Resume(Vector3 currentPosition)
        {
            if (routeState == RoadRouteState.Valid && route != null)
            {
                SetState(RoadAgentState.Following, currentPosition);
                return true;
            }

            return PlanRoute(currentPosition, true);
        }

        public RoadAgentControlOutput Evaluate(
            Vector3 currentPosition,
            Vector3 currentForward,
            float currentSpeed,
            float deltaTime)
        {
            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.AgentEvaluate);
            _ = currentForward;
            _ = currentSpeed;
            _ = deltaTime;
            RoadAgentControlOutput output = CreateBaseOutput();
            if (state == RoadAgentState.Suspended ||
                state == RoadAgentState.Idle ||
                state == RoadAgentState.Failed ||
                state == RoadAgentState.Arrived)
            {
                lastOutput = output;
                return output;
            }

            if (route == null ||
                route.network == null ||
                routeState != RoadRouteState.Valid ||
                route.segments.Count == 0 ||
                routeSegmentIndex < 0)
            {
                Fail(RoadQueryFailureReason.RouteNotFound, currentPosition);
                lastOutput = CreateBaseOutput();
                return lastOutput;
            }

            RoadAgentProfile activeProfile = profile;
            RoadAgentMask agentMask = activeProfile == null ? RoadAgentMask.Car : activeProfile.AgentMask;
            RoadTagFilter tagFilter = activeProfile == null ? default : activeProfile.TagFilter;
            float radius = activeProfile == null ? 0.9f : activeProfile.Radius;
            float searchDistance = activeProfile == null ? 30f : activeProfile.RouteSearchDistance;
            float maximumHeightDifference = activeProfile == null ? 3f : activeProfile.MaximumHeightDifference;
            if (!route.network.TryFindNearestElement(
                    currentPosition,
                    agentMask,
                    tagFilter,
                    radius,
                    searchDistance,
                    maximumHeightDifference,
                    out RoadLocation currentLocation))
            {
                output.failureReason = RoadQueryFailureReason.NoElement;
                output.shouldRecover = true;
                output.recoveryPosition = GetCurrentSegmentEntry();
                output.remainingDistance = CalculateRemainingDistance(currentPosition);
                lastOutput = output;
                return output;
            }

            AlignSegmentToLocation(currentLocation);
            if (routeSegmentIndex < 0 || routeSegmentIndex >= route.segments.Count)
            {
                Fail(RoadQueryFailureReason.RouteNotFound, currentPosition);
                lastOutput = CreateBaseOutput();
                return lastOutput;
            }

            RoadRouteSegment segment = route.segments[routeSegmentIndex];
            output.valid = true;
            output.currentElementKind = currentLocation.kind;
            output.currentElementId = currentLocation.elementId ?? string.Empty;
            output.routeSegmentIndex = routeSegmentIndex;
            output.distanceToBoundary = currentLocation.distanceToBoundary;
            output.remainingDistance = CalculateRemainingDistance(currentPosition);
            float recoveryDistance = activeProfile == null ? 2f : activeProfile.RecoveryDistance;
            output.shouldRecover =
                !currentLocation.inside ||
                Vector3.Distance(currentPosition, currentLocation.projectedPosition) > recoveryDistance;
            output.recoveryPosition = currentLocation.projectedPosition;

            if (IsFinalSegment() &&
                Vector3.Distance(currentPosition, destination) <=
                (activeProfile == null ? 0.5f : activeProfile.ArrivalDistance))
            {
                routeSegmentIndex = route.segments.Count - 1;
                routeState = RoadRouteState.Valid;
                failureReason = RoadQueryFailureReason.None;
                SetState(RoadAgentState.Arrived, currentPosition);
                output = CreateBaseOutput();
                output.valid = true;
                output.arrived = true;
                output.targetPosition = destination;
                output.targetForward = currentLocation.forward;
                output.targetUp = currentLocation.up;
                output.currentElementKind = currentLocation.kind;
                output.currentElementId = currentLocation.elementId ?? string.Empty;
                output.routeSegmentIndex = routeSegmentIndex;
                output.distanceToBoundary = currentLocation.distanceToBoundary;
                lastOutput = output;
                return output;
            }

            bool evaluated = segment.kind == RoadElementKind.Polygon
                ? EvaluatePolygonSegment(currentPosition, segment, activeProfile, ref output)
                : EvaluateLaneSegment(currentLocation, segment, activeProfile, ref output);
            if (!evaluated)
            {
                output.valid = false;
                output.failureReason = RoadQueryFailureReason.NoTopology;
                output.shouldRecover = true;
            }

            lastOutput = output;
            return output;
        }

        public RoadAgentDebugSnapshot GetDebugSnapshot()
        {
            return new RoadAgentDebugSnapshot
            {
                agentId = agentId ?? string.Empty,
                state = state,
                routeState = routeState,
                failureReason = failureReason,
                destination = destination,
                currentElementKind = lastOutput.currentElementKind,
                currentElementId = lastOutput.currentElementId ?? string.Empty,
                routeSegmentIndex = routeSegmentIndex,
                routeSegmentCount = route == null ? 0 : route.segments.Count,
                remainingDistance = lastOutput.remainingDistance,
                targetSpeed = lastOutput.targetSpeed,
                distanceToBoundary = lastOutput.distanceToBoundary,
                replanCount = replanCount
            };
        }

        private bool PlanRoute(Vector3 currentPosition, bool replanning)
        {
            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.AgentReplan);
            SetState(replanning ? RoadAgentState.Replanning : RoadAgentState.Planning, currentPosition);
            routeState = RoadRouteState.Pending;
            failureReason = RoadQueryFailureReason.None;
            RoadAgentProfile activeProfile = profile;
            RoadRouteQuery query = new RoadRouteQuery
            {
                startPosition = currentPosition,
                destinationPosition = destination,
                agentMask = activeProfile == null ? RoadAgentMask.Car : activeProfile.AgentMask,
                tagFilter = activeProfile == null ? default : activeProfile.TagFilter,
                agentRadius = activeProfile == null ? 0.9f : activeProfile.Radius,
                maximumSearchDistance = activeProfile == null ? 30f : activeProfile.RouteSearchDistance,
                maximumHeightDifference = activeProfile == null ? 3f : activeProfile.MaximumHeightDifference
            };

            bool success;
            if (roadSubsystem != null)
            {
                success = roadSubsystem.TryFindRoute(query, out route);
            }
            else if (fallbackNetwork != null)
            {
                success = fallbackNetwork.TryFindRoute(query, out route);
            }
            else
            {
                route = null;
                success = false;
            }

            routeState = route == null ? RoadRouteState.Invalid : route.state;
            failureReason = route == null
                ? RoadQueryFailureReason.NetworkUnavailable
                : route.failureReason;
            routeSegmentIndex = success && route.segments.Count > 0 ? 0 : -1;
            ResetPolygonPath();
            SetState(success ? RoadAgentState.Following : RoadAgentState.Failed, currentPosition);
            lastOutput = CreateBaseOutput();
            return success;
        }

        private bool EvaluateLaneSegment(
            RoadLocation currentLocation,
            RoadRouteSegment segment,
            RoadAgentProfile activeProfile,
            ref RoadAgentControlOutput output)
        {
            if (route == null ||
                route.network == null ||
                !route.network.TryGetLane(segment.elementId, out BakedLaneRecord lane))
            {
                return false;
            }

            float lookAhead = activeProfile == null ? 3f : activeProfile.LookAheadDistance;
            float currentDistance = string.Equals(
                currentLocation.elementId,
                segment.elementId,
                StringComparison.Ordinal)
                ? currentLocation.distanceAlong
                : segment.startDistance;
            float targetDistance = Mathf.Clamp(
                currentDistance + lookAhead,
                segment.startDistance,
                segment.endDistance);
            if (!route.network.TryEvaluate(segment.elementId, targetDistance, out RoadLanePose pose))
            {
                return false;
            }

            output.targetPosition = pose.position;
            output.targetForward = pose.forward;
            output.targetUp = pose.up;
            output.targetSpeed = Mathf.Min(
                lane.speedLimit,
                activeProfile == null ? 12f : activeProfile.MaximumSpeed);
            if (currentDistance >= segment.endDistance - 0.05f && !IsFinalSegment())
            {
                AdvanceSegment();
            }

            return true;
        }

        private bool EvaluatePolygonSegment(
            Vector3 currentPosition,
            RoadRouteSegment segment,
            RoadAgentProfile activeProfile,
            ref RoadAgentControlOutput output)
        {
            if (route == null || route.network == null)
            {
                return false;
            }

            if (!string.Equals(cachedPolygonId, segment.elementId, StringComparison.Ordinal))
            {
                polygonPath.Clear();
                if (!route.network.TryBuildPolygonPath(
                        segment.elementId,
                        segment.entryPosition,
                        segment.exitPosition,
                        polygonPath))
                {
                    return false;
                }

                cachedPolygonId = segment.elementId;
                polygonWaypointIndex = Mathf.Min(1, polygonPath.Count - 1);
            }

            float arrivalDistance = activeProfile == null ? 0.5f : activeProfile.ArrivalDistance;
            while (polygonWaypointIndex < polygonPath.Count - 1 &&
                   Vector3.Distance(currentPosition, polygonPath[polygonWaypointIndex]) <= arrivalDistance)
            {
                polygonWaypointIndex++;
            }

            Vector3 target = polygonPath.Count == 0
                ? segment.exitPosition
                : polygonPath[Mathf.Clamp(polygonWaypointIndex, 0, polygonPath.Count - 1)];
            Vector3 direction = target - currentPosition;
            output.targetPosition = target;
            output.targetForward = direction.sqrMagnitude <= 0.0001f ? Vector3.forward : direction.normalized;
            output.targetUp = Vector3.up;
            output.targetSpeed = activeProfile == null ? 12f : activeProfile.MaximumSpeed;
            if (Vector3.Distance(currentPosition, segment.exitPosition) <= arrivalDistance &&
                !IsFinalSegment())
            {
                AdvanceSegment();
            }

            return true;
        }

        private void AlignSegmentToLocation(in RoadLocation location)
        {
            if (!location.valid || route == null)
            {
                return;
            }

            int start = Mathf.Max(0, routeSegmentIndex);
            for (int i = start; i < route.segments.Count; i++)
            {
                if (route.segments[i].kind == location.kind &&
                    string.Equals(route.segments[i].elementId, location.elementId, StringComparison.Ordinal))
                {
                    if (i != routeSegmentIndex)
                    {
                        routeSegmentIndex = i;
                        ResetPolygonPath();
                    }

                    return;
                }
            }
        }

        private float CalculateRemainingDistance(Vector3 currentPosition)
        {
            if (route == null || routeSegmentIndex < 0 || routeSegmentIndex >= route.segments.Count)
            {
                return 0f;
            }

            float remaining = 0f;
            for (int i = routeSegmentIndex; i < route.segments.Count; i++)
            {
                RoadRouteSegment segment = route.segments[i];
                remaining += i == routeSegmentIndex
                    ? Vector3.Distance(currentPosition, segment.exitPosition)
                    : Vector3.Distance(segment.entryPosition, segment.exitPosition);
            }

            return remaining;
        }

        private Vector3 GetCurrentSegmentEntry()
        {
            return route != null &&
                   routeSegmentIndex >= 0 &&
                   routeSegmentIndex < route.segments.Count
                ? route.segments[routeSegmentIndex].entryPosition
                : destination;
        }

        private bool IsFinalSegment()
        {
            return route != null && routeSegmentIndex >= route.segments.Count - 1;
        }

        private void AdvanceSegment()
        {
            if (route == null || routeSegmentIndex >= route.segments.Count - 1)
            {
                return;
            }

            routeSegmentIndex++;
            ResetPolygonPath();
        }

        private RoadAgentControlOutput CreateBaseOutput()
        {
            return new RoadAgentControlOutput
            {
                agentState = state,
                routeState = routeState,
                failureReason = failureReason,
                currentElementKind = lastOutput.currentElementKind,
                currentElementId = lastOutput.currentElementId ?? string.Empty,
                routeSegmentIndex = routeSegmentIndex,
                remainingDistance = lastOutput.remainingDistance,
                distanceToBoundary = lastOutput.distanceToBoundary,
                arrived = state == RoadAgentState.Arrived
            };
        }

        private void Fail(RoadQueryFailureReason reason, Vector3 position)
        {
            failureReason = reason;
            routeState = RoadRouteState.Invalid;
            SetState(RoadAgentState.Failed, position);
        }

        private void SetState(RoadAgentState next, Vector3 position)
        {
            if (state == next)
            {
                return;
            }

            RoadAgentState previous = state;
            state = next;
            roadSubsystem?.NotifyAgentStateChanged(
                agentId,
                previous,
                next,
                routeState,
                failureReason,
                lastOutput.currentElementId,
                position);
        }

        private void Register()
        {
            if (registerOnEnable && isActiveAndEnabled)
            {
                roadSubsystem?.RegisterRoadAgent(this);
            }
        }

        private void Unregister()
        {
            roadSubsystem?.UnregisterRoadAgent(this);
        }

        private void ResetPolygonPath()
        {
            polygonPath.Clear();
            polygonWaypointIndex = 0;
            cachedPolygonId = string.Empty;
        }

        private void EnsureAgentId()
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                agentId = Guid.NewGuid().ToString("N");
            }
        }
    }
}
