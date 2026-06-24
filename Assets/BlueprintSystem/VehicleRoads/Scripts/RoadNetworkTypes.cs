using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    [Flags]
    public enum RoadTagMask : uint
    {
        None = 0,
        Road = 1u << 0,
        Vehicle = 1u << 1,
        Pedestrian = 1u << 2,
        Sidewalk = 1u << 3,
        Crosswalk = 1u << 4,
        Parking = 1u << 5,
        Service = 1u << 6,
        Restricted = 1u << 7,
        Indoor = 1u << 8,
        Outdoor = 1u << 9,
        Junction = 1u << 10,
        Connector = 1u << 11,
        Custom12 = 1u << 12,
        Custom13 = 1u << 13,
        Custom14 = 1u << 14,
        Custom15 = 1u << 15,
        Custom16 = 1u << 16,
        Custom17 = 1u << 17,
        Custom18 = 1u << 18,
        Custom19 = 1u << 19,
        Custom20 = 1u << 20,
        Custom21 = 1u << 21,
        Custom22 = 1u << 22,
        Custom23 = 1u << 23,
        Custom24 = 1u << 24,
        Custom25 = 1u << 25,
        Custom26 = 1u << 26,
        Custom27 = 1u << 27,
        Custom28 = 1u << 28,
        Custom29 = 1u << 29,
        Custom30 = 1u << 30,
        Custom31 = 1u << 31,
        All = uint.MaxValue
    }

    [Flags]
    public enum RoadAgentMask : uint
    {
        None = 0,
        Car = 1u << 0,
        Truck = 1u << 1,
        Bus = 1u << 2,
        Emergency = 1u << 3,
        Service = 1u << 4,
        Bicycle = 1u << 5,
        Pedestrian = 1u << 6,
        Custom7 = 1u << 7,
        Custom8 = 1u << 8,
        Custom9 = 1u << 9,
        Custom10 = 1u << 10,
        Custom11 = 1u << 11,
        Custom12 = 1u << 12,
        Custom13 = 1u << 13,
        Custom14 = 1u << 14,
        Custom15 = 1u << 15,
        Custom16 = 1u << 16,
        Custom17 = 1u << 17,
        Custom18 = 1u << 18,
        Custom19 = 1u << 19,
        Custom20 = 1u << 20,
        Custom21 = 1u << 21,
        Custom22 = 1u << 22,
        Custom23 = 1u << 23,
        Custom24 = 1u << 24,
        Custom25 = 1u << 25,
        Custom26 = 1u << 26,
        Custom27 = 1u << 27,
        Custom28 = 1u << 28,
        Custom29 = 1u << 29,
        Custom30 = 1u << 30,
        Custom31 = 1u << 31,
        MotorVehicles = Car | Truck | Bus | Emergency | Service,
        All = uint.MaxValue
    }

    [Serializable]
    public struct RoadTagFilter
    {
        public RoadTagMask all;
        public RoadTagMask any;
        public RoadTagMask none;

        public static RoadTagFilter MatchAll => default;

        public bool Matches(RoadTagMask value)
        {
            uint rawValue = (uint)value;
            uint rawAll = (uint)all;
            uint rawAny = (uint)any;
            uint rawNone = (uint)none;
            return (rawValue & rawAll) == rawAll &&
                   (rawAny == 0u || (rawValue & rawAny) != 0u) &&
                   (rawValue & rawNone) == 0u;
        }
    }

    public enum RoadElementKind
    {
        None,
        Lane,
        Connector,
        Polygon,
        Portal
    }

    public enum RoadPortalDirection
    {
        Bidirectional,
        OutboundOnly,
        InboundOnly
    }

    public enum RoadAreaQueryShape
    {
        Point,
        Sphere,
        Bounds
    }

    public enum RoadQueryFailureReason
    {
        None,
        NetworkUnavailable,
        NoElement,
        FilterRejected,
        AgentNotAllowed,
        WidthInsufficient,
        HeightRejected,
        OutsideBoundary,
        PortalClosed,
        NoTopology,
        RouteNotFound,
        DestinationOutsideNetwork,
        InvalidInput,
        Cancelled
    }

    public enum RoadAgentState
    {
        Idle,
        Planning,
        Following,
        Replanning,
        Arrived,
        Suspended,
        Failed
    }

    public enum RoadRouteState
    {
        None,
        Pending,
        Valid,
        Partial,
        Invalid,
        Cancelled
    }

    [Serializable]
    public struct RoadLocation
    {
        public bool valid;
        public bool inside;
        public RoadElementKind kind;
        public string elementId;
        public Vector3 worldPosition;
        public Vector3 projectedPosition;
        public Vector3 forward;
        public Vector3 up;
        public float distanceAlong;
        public float lateralRatio;
        public float distanceToBoundary;
        public float heightDifference;
        public int polygonTriangleIndex;
        public RoadQueryFailureReason failureReason;
    }

    [Serializable]
    public struct RoadAreaQuery
    {
        public RoadAreaQueryShape shape;
        public Vector3 center;
        public float radius;
        public Bounds bounds;
        public float maximumHeightDifference;
        public float agentRadius;
        public RoadAgentMask agentMask;
        public RoadTagFilter tagFilter;
        public int maximumResults;

        public static RoadAreaQuery Point(
            Vector3 position,
            RoadAgentMask agents,
            RoadTagFilter tags,
            float agentRadius = 0f,
            float maximumHeightDifference = 3f)
        {
            return new RoadAreaQuery
            {
                shape = RoadAreaQueryShape.Point,
                center = position,
                bounds = new Bounds(position, Vector3.zero),
                maximumHeightDifference = maximumHeightDifference,
                agentRadius = agentRadius,
                agentMask = agents,
                tagFilter = tags,
                maximumResults = 1
            };
        }
    }

    [Serializable]
    public struct RoadAreaQueryResult
    {
        public RoadLocation location;
        public float distance;
    }

    [Serializable]
    public struct RoadRouteQuery
    {
        public Vector3 startPosition;
        public Vector3 destinationPosition;
        public RoadAgentMask agentMask;
        public RoadTagFilter tagFilter;
        public float agentRadius;
        public float maximumSearchDistance;
        public float maximumHeightDifference;
        public bool allowPartial;
    }

    [Serializable]
    public sealed class RoadRouteSegment
    {
        public RoadElementKind kind;
        public string elementId = string.Empty;
        public Vector3 entryPosition;
        public Vector3 exitPosition;
        public float startDistance;
        public float endDistance;
        public float cost;
    }

    [Serializable]
    public sealed class RoadNetworkRouteResult
    {
        public RoadRouteState state;
        public RoadQueryFailureReason failureReason;
        public BakedLaneNetwork network;
        public RoadLocation start;
        public RoadLocation destination;
        public float totalCost;
        public int visitedNodeCount;
        public List<RoadRouteSegment> segments = new List<RoadRouteSegment>();
    }

    [Serializable]
    public struct RoadAgentControlOutput
    {
        public bool valid;
        public RoadAgentState agentState;
        public RoadRouteState routeState;
        public RoadQueryFailureReason failureReason;
        public RoadElementKind currentElementKind;
        public string currentElementId;
        public int routeSegmentIndex;
        public Vector3 targetPosition;
        public Vector3 targetForward;
        public Vector3 targetUp;
        public float targetSpeed;
        public float remainingDistance;
        public float distanceToBoundary;
        public bool arrived;
        public bool shouldRecover;
        public Vector3 recoveryPosition;
    }

    public enum RoadDiagnosticEventType
    {
        None,
        QuerySucceeded,
        QueryFailed,
        RouteRequested,
        RouteSucceeded,
        RouteFailed,
        AgentStateChanged,
        PortalTransition,
        LaneClosureChanged,
        TrafficAuthorizationChanged
    }

    [Serializable]
    public struct RoadDiagnosticEvent
    {
        public RoadDiagnosticEventType type;
        public int frame;
        public float time;
        public string agentId;
        public string primaryId;
        public string secondaryId;
        public RoadElementKind elementKind;
        public RoadAgentState agentState;
        public RoadRouteState routeState;
        public RoadQueryFailureReason failureReason;
        public Vector3 position;
        public int candidateCount;
        public int visitedNodeCount;
        public float cost;
    }

    [Serializable]
    public sealed class RoadQueryDebugSnapshot
    {
        public RoadAreaQueryShape shape;
        public Vector3 center;
        public float radius;
        public Bounds bounds;
        public RoadAgentMask agentMask;
        public RoadTagFilter tagFilter;
        public float agentRadius;
        public int candidateCount;
        public int resultCount;
        public RoadLocation bestResult;
        public RoadQueryFailureReason failureReason;
    }

    [Serializable]
    public sealed class RoadRouteDebugSnapshot
    {
        public Vector3 startPosition;
        public Vector3 destinationPosition;
        public RoadAgentMask agentMask;
        public RoadTagFilter tagFilter;
        public RoadRouteState state;
        public RoadQueryFailureReason failureReason;
        public int visitedNodeCount;
        public int segmentCount;
        public float totalCost;
        public string startElementId;
        public string destinationElementId;
    }

    [Serializable]
    public sealed class RoadAgentDebugSnapshot
    {
        public string agentId;
        public RoadAgentState state;
        public RoadRouteState routeState;
        public RoadQueryFailureReason failureReason;
        public Vector3 destination;
        public RoadElementKind currentElementKind;
        public string currentElementId;
        public int routeSegmentIndex;
        public int routeSegmentCount;
        public float remainingDistance;
        public float targetSpeed;
        public float distanceToBoundary;
        public int replanCount;
    }

    public static class RoadAgentMaskUtility
    {
        public static bool Allows(RoadAgentMask allowed, RoadAgentMask requested)
        {
            return requested == RoadAgentMask.None || (allowed & requested) != 0;
        }
    }
}
