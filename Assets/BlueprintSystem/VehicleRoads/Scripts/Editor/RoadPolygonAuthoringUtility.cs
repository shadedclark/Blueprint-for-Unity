using System;
using System.Collections.Generic;
using System.Linq;
using VehicleRoads;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads.Editor
{
    internal enum RoadPolygonPortalSuggestionKind
    {
        None,
        Lane,
        Portal
    }

    internal readonly struct RoadPolygonBoundaryProjection
    {
        public readonly bool valid;
        public readonly int edgeIndex;
        public readonly Vector2 localPoint;
        public readonly Vector3 worldPoint;
        public readonly Vector3 worldTangent;
        public readonly float distance;

        public RoadPolygonBoundaryProjection(
            int edgeIndex,
            Vector2 localPoint,
            Vector3 worldPoint,
            Vector3 worldTangent,
            float distance)
        {
            valid = true;
            this.edgeIndex = edgeIndex;
            this.localPoint = localPoint;
            this.worldPoint = worldPoint;
            this.worldTangent = worldTangent;
            this.distance = distance;
        }
    }

    internal readonly struct RoadPolygonPortalSuggestion
    {
        public readonly RoadPolygonPortalSuggestionKind kind;
        public readonly RoadLane lane;
        public readonly RoadLaneEndpoint endpoint;
        public readonly bool useReverseRuntimeLane;
        public readonly RoadPortal portal;
        public readonly Vector3 targetPosition;
        public readonly float distance;

        public RoadPolygonPortalSuggestion(
            RoadLane lane,
            RoadLaneEndpoint endpoint,
            bool useReverseRuntimeLane,
            Vector3 targetPosition,
            float distance)
        {
            kind = RoadPolygonPortalSuggestionKind.Lane;
            this.lane = lane;
            this.endpoint = endpoint;
            this.useReverseRuntimeLane = useReverseRuntimeLane;
            portal = null;
            this.targetPosition = targetPosition;
            this.distance = distance;
        }

        public RoadPolygonPortalSuggestion(RoadPortal portal, Vector3 targetPosition, float distance)
        {
            kind = RoadPolygonPortalSuggestionKind.Portal;
            lane = null;
            endpoint = RoadLaneEndpoint.Start;
            useReverseRuntimeLane = false;
            this.portal = portal;
            this.targetPosition = targetPosition;
            this.distance = distance;
        }

        public bool IsValid => kind != RoadPolygonPortalSuggestionKind.None;

        public string StableKey
        {
            get
            {
                return kind switch
                {
                    RoadPolygonPortalSuggestionKind.Lane when lane != null =>
                        "Lane:" + lane.GetInstanceID() + ":" + endpoint,
                    RoadPolygonPortalSuggestionKind.Portal when portal != null =>
                        "Portal:" + portal.GetInstanceID(),
                    _ => string.Empty
                };
            }
        }

        public string DisplayName
        {
            get
            {
                return kind switch
                {
                    RoadPolygonPortalSuggestionKind.Lane when lane != null =>
                        lane.LaneId + " / " + endpoint,
                    RoadPolygonPortalSuggestionKind.Portal when portal != null =>
                        portal.PortalId + " / " + portal.SourceZone?.ZoneId,
                    _ => "No target"
                };
            }
        }
    }

    internal static class RoadPolygonAuthoringUtility
    {
        public const float PortalBoundaryTolerance = 0.5f;

        public static string GetUniqueZoneId(RoadLaneNetwork network, string requestedId, string prefix)
        {
            HashSet<string> existing = new HashSet<string>(
                (network == null ? Array.Empty<RoadPolygonZone>() : network.GetPolygonZones())
                .Where(zone => zone != null)
                .Select(zone => RoadLaneNetwork.SanitizeId(zone.ZoneId)),
                StringComparer.Ordinal);
            return GetUniqueId(existing, requestedId, prefix, "polygon");
        }

        public static string GetUniquePortalId(RoadPolygonZone zone, string requestedId, string prefix)
        {
            HashSet<string> existing = new HashSet<string>(
                (zone == null ? Array.Empty<RoadPortal>() : zone.GetPortals())
                .Where(portal => portal != null)
                .Select(portal => RoadLaneNetwork.SanitizeId(portal.PortalId)),
                StringComparer.Ordinal);
            return GetUniqueId(existing, requestedId, prefix, "portal");
        }

        public static bool TryProjectToBoundary(
            RoadPolygonZone zone,
            Vector3 worldPoint,
            out RoadPolygonBoundaryProjection projection)
        {
            projection = default;
            IReadOnlyList<Vector2> vertices = zone == null ? null : zone.Vertices;
            if (vertices == null || vertices.Count < 2)
            {
                return false;
            }

            int bestEdge = -1;
            Vector3 bestWorld = worldPoint;
            Vector3 bestTangent = Vector3.forward;
            float bestSquared = float.PositiveInfinity;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 a = zone.LocalVertexToWorld(vertices[i]);
                Vector3 b = zone.LocalVertexToWorld(vertices[(i + 1) % vertices.Count]);
                float squared = DistanceSquaredToSegment(worldPoint, a, b, out Vector3 candidate);
                if (squared >= bestSquared)
                {
                    continue;
                }

                bestSquared = squared;
                bestEdge = i;
                bestWorld = candidate;
                Vector3 tangent = b - a;
                bestTangent = tangent.sqrMagnitude <= 0.0001f
                    ? zone.transform.right
                    : tangent.normalized;
            }

            if (bestEdge < 0)
            {
                return false;
            }

            projection = new RoadPolygonBoundaryProjection(
                bestEdge,
                zone.WorldToLocalXZ(bestWorld),
                bestWorld,
                bestTangent,
                Mathf.Sqrt(bestSquared));
            return true;
        }

        public static bool InsertVertexAfterEdge(
            RoadPolygonZone zone,
            int edgeIndex,
            Vector3 worldPoint,
            out int insertedIndex)
        {
            insertedIndex = -1;
            if (zone == null || zone.Vertices == null || zone.Vertices.Count < 2)
            {
                return false;
            }

            int normalizedEdge = ((edgeIndex % zone.Vertices.Count) + zone.Vertices.Count) % zone.Vertices.Count;
            insertedIndex = normalizedEdge + 1;
            zone.Vertices.Insert(insertedIndex, zone.WorldToLocalXZ(worldPoint));
            return true;
        }

        public static bool RemoveVertexAt(RoadPolygonZone zone, int index)
        {
            if (zone == null ||
                zone.Vertices == null ||
                zone.Vertices.Count <= 3 ||
                index < 0 ||
                index >= zone.Vertices.Count)
            {
                return false;
            }

            zone.Vertices.RemoveAt(index);
            return true;
        }

        public static Vector2 GetLocalCentroid(RoadPolygonZone zone)
        {
            IReadOnlyList<Vector2> vertices = zone == null ? null : zone.Vertices;
            if (vertices == null || vertices.Count == 0)
            {
                return Vector2.zero;
            }

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < vertices.Count; i++)
            {
                sum += vertices[i];
            }

            return sum / vertices.Count;
        }

        public static Quaternion GetPortalRotation(RoadPolygonZone zone, Vector3 boundaryTangent)
        {
            Vector3 up = zone == null ? Vector3.up : zone.transform.up;
            if (up.sqrMagnitude <= 0.0001f)
            {
                up = Vector3.up;
            }

            up.Normalize();
            Vector3 tangent = boundaryTangent.sqrMagnitude <= 0.0001f
                ? Vector3.right
                : boundaryTangent.normalized;
            Vector3 forward = Vector3.Cross(tangent, up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            return Quaternion.LookRotation(forward.normalized, up);
        }

        public static bool TryFindPortalSuggestion(
            RoadLaneNetwork network,
            RoadPortal source,
            float radius,
            out RoadPolygonPortalSuggestion suggestion)
        {
            suggestion = default;
            if (network == null || source == null)
            {
                return false;
            }

            Vector3 sourcePosition = source.transform.position;
            float maxDistance = Mathf.Max(0f, radius);
            float bestSquared = maxDistance * maxDistance;
            RoadPolygonPortalSuggestion bestSuggestion = default;
            bool found = false;

            RoadLane[] lanes = network.GetAuthoredLanes();
            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                RoadLane lane = lanes[laneIndex];
                if (lane == null)
                {
                    continue;
                }

                TryConsiderLaneEndpoint(lane, RoadLaneEndpoint.Start);
                TryConsiderLaneEndpoint(lane, RoadLaneEndpoint.End);
            }

            RoadPortal[] portals = network.GetComponentsInChildren<RoadPortal>(true);
            for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
            {
                RoadPortal portal = portals[portalIndex];
                if (portal == null || portal == source || portal.SourceZone == null)
                {
                    continue;
                }

                float squared = (portal.transform.position - sourcePosition).sqrMagnitude;
                if (squared > bestSquared)
                {
                    continue;
                }

                bestSquared = squared;
                bestSuggestion = new RoadPolygonPortalSuggestion(
                    portal,
                    portal.transform.position,
                    Mathf.Sqrt(squared));
                found = true;
            }

            suggestion = bestSuggestion;
            return found;

            void TryConsiderLaneEndpoint(RoadLane lane, RoadLaneEndpoint endpoint)
            {
                if (!TryGetEndpointWorldPosition(lane, endpoint, out Vector3 endpointPosition))
                {
                    return;
                }

                float squared = (endpointPosition - sourcePosition).sqrMagnitude;
                if (squared > bestSquared)
                {
                    return;
                }

                bestSquared = squared;
                bestSuggestion = new RoadPolygonPortalSuggestion(
                    lane,
                    endpoint,
                    GetDefaultReverseRuntimeLane(lane),
                    endpointPosition,
                    Mathf.Sqrt(squared));
                found = true;
            }
        }

        public static bool ApplyPortalSuggestion(
            RoadPortal portal,
            RoadPolygonPortalSuggestion suggestion,
            bool useReverseRuntimeLane)
        {
            if (portal == null || !suggestion.IsValid)
            {
                return false;
            }

            if (suggestion.kind == RoadPolygonPortalSuggestionKind.Lane && suggestion.lane != null)
            {
                portal.LinkedLane = suggestion.lane;
                portal.LinkedLaneEndpoint = suggestion.endpoint;
                portal.LinkedLaneReverse = useReverseRuntimeLane;
                portal.LinkedPortal = null;
                return true;
            }

            if (suggestion.kind == RoadPolygonPortalSuggestionKind.Portal && suggestion.portal != null)
            {
                portal.LinkedPortal = suggestion.portal;
                portal.LinkedLane = null;
                portal.LinkedLaneReverse = false;
                portal.LinkedLaneEndpoint = RoadLaneEndpoint.Start;
                return true;
            }

            return false;
        }

        public static bool TryGetEndpointWorldPosition(
            RoadLane lane,
            RoadLaneEndpoint endpoint,
            out Vector3 position)
        {
            SplineContainer container = lane == null ? null : lane.SplineContainer;
            Spline spline = container == null ? null : container.Spline;
            if (spline == null || spline.Count == 0)
            {
                position = default;
                return false;
            }

            position = container.EvaluatePosition(endpoint == RoadLaneEndpoint.Start ? 0f : 1f);
            return true;
        }

        public static bool GetDefaultReverseRuntimeLane(RoadLane lane)
        {
            return lane != null && lane.TravelDirection == RoadLaneTravelDirection.Reverse;
        }

        private static string GetUniqueId(
            HashSet<string> existing,
            string requestedId,
            string prefix,
            string fallbackPrefix)
        {
            string requested = RoadLaneNetwork.SanitizeId(requestedId);
            if (!string.IsNullOrWhiteSpace(requested) && !existing.Contains(requested))
            {
                return requested;
            }

            string sanitizedPrefix = RoadLaneNetwork.SanitizeId(prefix);
            if (string.IsNullOrWhiteSpace(sanitizedPrefix))
            {
                sanitizedPrefix = fallbackPrefix;
            }

            for (int i = 1; i < 100000; i++)
            {
                string candidate = sanitizedPrefix + "_" + i.ToString("D3");
                if (!existing.Contains(candidate))
                {
                    return candidate;
                }
            }

            return sanitizedPrefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static float DistanceSquaredToSegment(
            Vector3 point,
            Vector3 a,
            Vector3 b,
            out Vector3 closest)
        {
            Vector3 delta = b - a;
            float denominator = delta.sqrMagnitude;
            float t = denominator <= 0.0001f
                ? 0f
                : Mathf.Clamp01(Vector3.Dot(point - a, delta) / denominator);
            closest = a + delta * t;
            return (point - closest).sqrMagnitude;
        }
    }
}
