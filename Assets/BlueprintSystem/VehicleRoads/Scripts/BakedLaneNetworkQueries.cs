using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    public sealed partial class BakedLaneNetwork
    {
        [NonSerialized] private readonly HashSet<long> queryVisitedSegments = new HashSet<long>();
        [NonSerialized] private readonly HashSet<int> queryVisitedPolygons = new HashSet<int>();
        [NonSerialized] private readonly List<string> routeOpenNodes = new List<string>();
        [NonSerialized] private readonly List<string> routeNeighborNodes = new List<string>();

        public bool TryFindNearestElement(
            Vector3 position,
            RoadAgentMask agentMask,
            RoadTagFilter tagFilter,
            float agentRadius,
            float maximumDistance,
            float maximumHeightDifference,
            out RoadLocation location)
        {
            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.NearestElementQuery);
            EnsureRuntimeCaches();
            location = default;
            maximumDistance = Mathf.Max(0.1f, maximumDistance);
            maximumHeightDifference = Mathf.Max(0f, maximumHeightDifference);
            float bestDistance = maximumDistance;
            bool found = false;
            Vector3Int center = ToCell(position);
            int radius = Mathf.Max(1, Mathf.CeilToInt(maximumDistance / spatialCellSize));
            queryVisitedSegments.Clear();
            queryVisitedPolygons.Clear();

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        Vector3Int cell = center + new Vector3Int(x, y, z);
                        if (spatialCells.TryGetValue(cell, out List<SegmentRef> segments))
                        {
                            for (int i = 0; i < segments.Count; i++)
                            {
                                SegmentRef segment = segments[i];
                                long key = ((long)segment.laneIndex << 32) | (uint)segment.sampleIndex;
                                if (!queryVisitedSegments.Add(key))
                                {
                                    continue;
                                }

                                if (TryProjectToLaneSegment(
                                        segment,
                                        position,
                                        agentMask,
                                        tagFilter,
                                        agentRadius,
                                        maximumHeightDifference,
                                        false,
                                        out RoadLocation candidate,
                                        out float distance) &&
                                    distance < bestDistance)
                                {
                                    bestDistance = distance;
                                    location = candidate;
                                    found = true;
                                }
                            }
                        }

                        if (!polygonSpatialCells.TryGetValue(cell, out List<int> polygonIndexes))
                        {
                            continue;
                        }

                        for (int i = 0; i < polygonIndexes.Count; i++)
                        {
                            int polygonIndex = polygonIndexes[i];
                            if (!queryVisitedPolygons.Add(polygonIndex) ||
                                !TryProjectToPolygon(
                                    polygonIndex,
                                    position,
                                    agentMask,
                                    tagFilter,
                                    agentRadius,
                                    maximumHeightDifference,
                                    out RoadLocation candidate,
                                    out float distance) ||
                                distance >= bestDistance)
                            {
                                continue;
                            }

                            bestDistance = distance;
                            location = candidate;
                            found = true;
                        }
                    }
                }
            }

            if (!found)
            {
                location.failureReason = RoadQueryFailureReason.NoElement;
            }

            return found;
        }

        public int QueryArea(in RoadAreaQuery query, List<RoadAreaQueryResult> results)
        {
            if (results == null)
            {
                return 0;
            }

            switch (query.shape)
            {
                case RoadAreaQueryShape.Sphere:
                    using (RoadNetworkProfiler.Sample(RoadNetworkProfiler.SphereQuery))
                    {
                        return QueryAreaInternal(query, results);
                    }
                case RoadAreaQueryShape.Bounds:
                    using (RoadNetworkProfiler.Sample(RoadNetworkProfiler.BoundsQuery))
                    {
                        return QueryAreaInternal(query, results);
                    }
                default:
                    using (RoadNetworkProfiler.Sample(RoadNetworkProfiler.PointQuery))
                    {
                        return QueryAreaInternal(query, results);
                    }
            }
        }

        public bool TryFindRoute(RoadRouteQuery query, out RoadNetworkRouteResult result)
        {
            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.RouteSearch);
            result = new RoadNetworkRouteResult
            {
                network = this,
                state = RoadRouteState.Invalid,
                failureReason = RoadQueryFailureReason.RouteNotFound
            };
            float searchDistance = query.maximumSearchDistance > 0f ? query.maximumSearchDistance : 30f;
            float heightDifference = query.maximumHeightDifference > 0f ? query.maximumHeightDifference : 3f;
            if (!TryFindNearestElement(
                    query.startPosition,
                    query.agentMask,
                    query.tagFilter,
                    query.agentRadius,
                    searchDistance,
                    heightDifference,
                    out RoadLocation start))
            {
                result.failureReason = start.failureReason;
                return false;
            }

            if (!TryFindNearestElement(
                    query.destinationPosition,
                    query.agentMask,
                    query.tagFilter,
                    query.agentRadius,
                    searchDistance,
                    heightDifference,
                    out RoadLocation destination))
            {
                result.start = start;
                result.failureReason = RoadQueryFailureReason.DestinationOutsideNetwork;
                return false;
            }

            result.start = start;
            result.destination = destination;
            string startNode = NodeKey(start.kind, start.elementId);
            string destinationNode = NodeKey(destination.kind, destination.elementId);
            if (string.Equals(startNode, destinationNode, StringComparison.Ordinal))
            {
                result.state = RoadRouteState.Valid;
                result.failureReason = RoadQueryFailureReason.None;
                AddRouteSegment(result, startNode, start, destination, null, null);
                return true;
            }

            Dictionary<string, float> costs = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                [startNode] = 0f
            };
            Dictionary<string, string> previous = new Dictionary<string, string>(StringComparer.Ordinal);
            HashSet<string> closed = new HashSet<string>(StringComparer.Ordinal);
            routeOpenNodes.Clear();
            routeOpenNodes.Add(startNode);

            while (routeOpenNodes.Count > 0)
            {
                int bestIndex = 0;
                float bestCost = costs[routeOpenNodes[0]];
                for (int i = 1; i < routeOpenNodes.Count; i++)
                {
                    float candidateCost = costs[routeOpenNodes[i]];
                    if (candidateCost < bestCost)
                    {
                        bestCost = candidateCost;
                        bestIndex = i;
                    }
                }

                string current = routeOpenNodes[bestIndex];
                routeOpenNodes.RemoveAt(bestIndex);
                if (!closed.Add(current))
                {
                    continue;
                }

                result.visitedNodeCount++;
                if (string.Equals(current, destinationNode, StringComparison.Ordinal))
                {
                    List<string> path = ReconstructNodePath(previous, current);
                    result.totalCost = costs[current];
                    result.state = RoadRouteState.Valid;
                    result.failureReason = RoadQueryFailureReason.None;
                    BuildRouteSegments(path, start, destination, result);
                    return true;
                }

                routeNeighborNodes.Clear();
                CollectRouteNeighbors(current, query, routeNeighborNodes);
                for (int i = 0; i < routeNeighborNodes.Count; i++)
                {
                    string neighbor = routeNeighborNodes[i];
                    float tentative = costs[current] + GetTransitionCost(current, neighbor);
                    if (costs.TryGetValue(neighbor, out float existing) && tentative >= existing)
                    {
                        continue;
                    }

                    costs[neighbor] = tentative;
                    previous[neighbor] = current;
                    routeOpenNodes.Add(neighbor);
                }
            }

            return false;
        }

        public bool TryBuildPolygonPath(
            string zoneId,
            Vector3 start,
            Vector3 destination,
            List<Vector3> output)
        {
            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.PolygonFunnel);
            output?.Clear();
            if (output == null ||
                !TryGetPolygon(zoneId, out BakedPolygonRecord polygon) ||
                polygon.vertices.Count < 3 ||
                polygon.triangles.Count < 3)
            {
                return false;
            }

            Vector2 start2 = new Vector2(start.x, start.z);
            Vector2 destination2 = new Vector2(destination.x, destination.z);
            if (!TryFindTriangleWorld(polygon, start2, out int startTriangle) ||
                !TryFindTriangleWorld(polygon, destination2, out int destinationTriangle))
            {
                return false;
            }

            output.Add(start);
            if (startTriangle == destinationTriangle)
            {
                output.Add(destination);
                return true;
            }

            List<int> trianglePath = FindTrianglePath(polygon, startTriangle, destinationTriangle);
            if (trianglePath.Count == 0)
            {
                output.Clear();
                return false;
            }

            List<PortalEdge> corridor = BuildTriangleCorridor(polygon, trianglePath);
            AppendFunnelPath(start, destination, corridor, output);
            return output.Count >= 2;
        }

        private int QueryAreaInternal(in RoadAreaQuery query, List<RoadAreaQueryResult> results)
        {
            EnsureRuntimeCaches();
            results.Clear();
            Bounds searchBounds = GetSearchBounds(query);
            Vector3Int minCell = ToCell(searchBounds.min);
            Vector3Int maxCell = ToCell(searchBounds.max);
            queryVisitedSegments.Clear();
            queryVisitedPolygons.Clear();
            int maximumResults = query.maximumResults <= 0 ? int.MaxValue : query.maximumResults;

            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    for (int z = minCell.z; z <= maxCell.z; z++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, z);
                        if (spatialCells.TryGetValue(cell, out List<SegmentRef> segments))
                        {
                            for (int i = 0; i < segments.Count && results.Count < maximumResults; i++)
                            {
                                SegmentRef segment = segments[i];
                                long key = ((long)segment.laneIndex << 32) | (uint)segment.sampleIndex;
                                if (!queryVisitedSegments.Add(key) ||
                                    !TryProjectToLaneSegment(
                                        segment,
                                        query.center,
                                        query.agentMask,
                                        query.tagFilter,
                                        query.agentRadius,
                                        query.maximumHeightDifference,
                                        query.shape == RoadAreaQueryShape.Point,
                                        out RoadLocation location,
                                        out float distance) ||
                                    !QueryShapeAccepts(query, location.projectedPosition, distance))
                                {
                                    continue;
                                }

                                AddUniqueResult(results, location, distance);
                            }
                        }

                        if (!polygonSpatialCells.TryGetValue(cell, out List<int> polygonIndexes))
                        {
                            continue;
                        }

                        for (int i = 0; i < polygonIndexes.Count && results.Count < maximumResults; i++)
                        {
                            int polygonIndex = polygonIndexes[i];
                            if (!queryVisitedPolygons.Add(polygonIndex) ||
                                !TryProjectToPolygon(
                                    polygonIndex,
                                    query.center,
                                    query.agentMask,
                                    query.tagFilter,
                                    query.agentRadius,
                                    query.maximumHeightDifference,
                                    out RoadLocation location,
                                    out float distance) ||
                                !QueryShapeAccepts(query, location.projectedPosition, distance))
                            {
                                continue;
                            }

                            AddUniqueResult(results, location, distance);
                        }
                    }
                }
            }

            results.Sort((left, right) => left.distance.CompareTo(right.distance));
            return results.Count;
        }

        private bool TryProjectToLaneSegment(
            SegmentRef segment,
            Vector3 position,
            RoadAgentMask agentMask,
            RoadTagFilter tagFilter,
            float agentRadius,
            float maximumHeightDifference,
            bool requireInside,
            out RoadLocation location,
            out float distance)
        {
            location = default;
            distance = float.PositiveInfinity;
            BakedLaneRecord lane = lanes[segment.laneIndex];
            if (!IsLaneQueryCompatible(lane, agentMask, tagFilter, agentRadius, false))
            {
                return false;
            }

            BakedLaneSampleRecord a = samples[segment.sampleIndex];
            BakedLaneSampleRecord b = samples[segment.sampleIndex + 1];
            Vector3 segmentDelta = b.finalPosition - a.finalPosition;
            float segmentLengthSquared = segmentDelta.sqrMagnitude;
            float rawT = segmentLengthSquared <= 0.000001f
                ? 0f
                : Vector3.Dot(position - a.finalPosition, segmentDelta) / segmentLengthSquared;
            float t = Mathf.Clamp01(rawT);
            Vector3 nearest = Vector3.Lerp(a.finalPosition, b.finalPosition, t);
            float heightDifference = Mathf.Abs(position.y - nearest.y);
            if (heightDifference > maximumHeightDifference)
            {
                return false;
            }

            Vector3 forward = Vector3.Slerp(a.forward, b.forward, t).normalized;
            Vector3 up = Vector3.Slerp(a.up, b.up, t).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            float signedLateral = right.sqrMagnitude <= 0.0001f ? 0f : Vector3.Dot(position - nearest, right);
            float localWidth = Mathf.Lerp(
                Mathf.Max(0.1f, a.width),
                Mathf.Max(0.1f, b.width),
                t);
            if (localWidth + 0.0001f < Mathf.Max(0f, agentRadius) * 2f)
            {
                return false;
            }
            float halfWidth = Mathf.Max(0.05f, localWidth * 0.5f);
            float boundaryDistance = halfWidth - Mathf.Abs(signedLateral);
            bool longitudinalInside = rawT >= -0.0001f && rawT <= 1.0001f;
            bool inside =
                longitudinalInside &&
                boundaryDistance + 0.0001f >= Mathf.Max(0f, agentRadius);
            if (requireInside && !inside)
            {
                return false;
            }

            Vector3 projected =
                nearest + right * Mathf.Clamp(signedLateral, -halfWidth, halfWidth);
            distance = Vector3.Distance(position, projected);
            if (!longitudinalInside)
            {
                boundaryDistance = -distance;
            }
            location = new RoadLocation
            {
                valid = true,
                inside = inside,
                kind = lane.kind == RoadLaneKind.Connector ? RoadElementKind.Connector : RoadElementKind.Lane,
                elementId = lane.laneId,
                worldPosition = position,
                projectedPosition = projected,
                forward = forward,
                up = up,
                distanceAlong = Mathf.Lerp(a.distanceAlongLane, b.distanceAlongLane, t),
                lateralRatio = Mathf.Clamp(signedLateral / halfWidth, -1f, 1f),
                distanceToBoundary = boundaryDistance,
                heightDifference = heightDifference,
                polygonTriangleIndex = -1,
                failureReason = RoadQueryFailureReason.None
            };
            return true;
        }

        private bool TryProjectToPolygon(
            int polygonIndex,
            Vector3 position,
            RoadAgentMask agentMask,
            RoadTagFilter tagFilter,
            float agentRadius,
            float maximumHeightDifference,
            out RoadLocation location,
            out float distance)
        {
            location = default;
            distance = float.PositiveInfinity;
            if (polygonIndex < 0 || polygonIndex >= polygons.Count)
            {
                return false;
            }

            BakedPolygonRecord polygon = polygons[polygonIndex];
            if (polygon == null ||
                !polygon.open ||
                !polygon.AllowsAgent(agentMask) ||
                !tagFilter.Matches(polygon.tagMask) ||
                !polygon.bounds.ExpandAndContains(position, maximumHeightDifference))
            {
                return false;
            }

            float projectedY = Mathf.Clamp(position.y, polygon.minimumWorldHeight, polygon.maximumWorldHeight);
            Vector2 point = new Vector2(position.x, position.z);
            bool inside = ContainsWorldPoint(polygon.vertices, point);
            Vector2 closest = ClosestWorldBoundary(polygon.vertices, point, out float boundaryDistance);
            float heightDifference = Mathf.Abs(position.y - projectedY);
            bool hasClearance = inside && boundaryDistance + 0.0001f >= Mathf.Max(0f, agentRadius);
            Vector3 projected = hasClearance
                ? new Vector3(position.x, projectedY, position.z)
                : new Vector3(closest.x, projectedY, closest.y);
            distance = hasClearance
                ? heightDifference
                : Mathf.Sqrt(boundaryDistance * boundaryDistance + heightDifference * heightDifference);
            TryFindTriangleWorld(polygon, new Vector2(projected.x, projected.z), out int triangle);
            location = new RoadLocation
            {
                valid = true,
                inside = hasClearance,
                kind = RoadElementKind.Polygon,
                elementId = polygon.zoneId,
                worldPosition = position,
                projectedPosition = projected,
                forward = Vector3.forward,
                up = Vector3.up,
                distanceAlong = 0f,
                lateralRatio = 0f,
                distanceToBoundary = inside ? boundaryDistance : -boundaryDistance,
                heightDifference = heightDifference,
                polygonTriangleIndex = triangle,
                failureReason = RoadQueryFailureReason.None
            };
            return true;
        }

        private bool IsLaneQueryCompatible(
            BakedLaneRecord lane,
            RoadAgentMask agentMask,
            RoadTagFilter tagFilter,
            float agentRadius,
            bool requireFullLaneWidth)
        {
            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.FilterElement);
            return lane != null &&
                   lane.open &&
                   !lane.orphaned &&
                   lane.AllowsAgent(agentMask) &&
                   tagFilter.Matches(lane.tagMask) &&
                   Mathf.Max(
                       0.1f,
                       requireFullLaneWidth ? lane.minimumWidth : lane.maximumWidth) +
                   0.0001f >= Mathf.Max(0f, agentRadius) * 2f;
        }

        private void CollectRouteNeighbors(string node, RoadRouteQuery query, List<string> output)
        {
            ParseNodeKey(node, out RoadElementKind kind, out string id);
            if (kind == RoadElementKind.Lane || kind == RoadElementKind.Connector)
            {
                IReadOnlyList<BakedLaneConnectionRecord> outgoing = GetOutgoingConnections(id);
                for (int i = 0; i < outgoing.Count; i++)
                {
                    BakedLaneConnectionRecord connection = outgoing[i];
                    if (connection != null &&
                        connection.open &&
                        TryGetLane(connection.toLaneId, out BakedLaneRecord target) &&
                        IsLaneQueryCompatible(target, query.agentMask, query.tagFilter, query.agentRadius, true))
                    {
                        AddUnique(output, NodeKey(
                            target.kind == RoadLaneKind.Connector ? RoadElementKind.Connector : RoadElementKind.Lane,
                            target.laneId));
                    }
                }

                IReadOnlyList<BakedPortalRecord> incomingPortals = GetPortalsTargetingElement(id);
                for (int i = 0; i < incomingPortals.Count; i++)
                {
                    BakedPortalRecord portal = incomingPortals[i];
                    if (PortalAllows(portal, query) &&
                        portal.direction != RoadPortalDirection.OutboundOnly &&
                        TryGetPolygon(portal.sourceZoneId, out BakedPolygonRecord zone) &&
                        IsPolygonQueryCompatible(zone, query))
                    {
                        AddUnique(output, NodeKey(RoadElementKind.Polygon, zone.zoneId));
                    }
                }

                return;
            }

            if (kind != RoadElementKind.Polygon || !TryGetPolygon(id, out _))
            {
                return;
            }

            IReadOnlyList<BakedPortalRecord> portalsFromZone = GetPortalsFromZone(id);
            for (int i = 0; i < portalsFromZone.Count; i++)
            {
                BakedPortalRecord portal = portalsFromZone[i];
                if (!PortalAllows(portal, query) ||
                    portal.direction == RoadPortalDirection.InboundOnly ||
                    !TargetElementAllows(portal, query))
                {
                    continue;
                }

                AddUnique(output, NodeKey(portal.targetKind, portal.targetElementId));
            }

            IReadOnlyList<BakedPortalRecord> portalsTargetingZone = GetPortalsTargetingElement(id);
            for (int i = 0; i < portalsTargetingZone.Count; i++)
            {
                BakedPortalRecord portal = portalsTargetingZone[i];
                if (PortalAllows(portal, query) &&
                    portal.direction != RoadPortalDirection.OutboundOnly &&
                    TryGetPolygon(portal.sourceZoneId, out BakedPolygonRecord source) &&
                    IsPolygonQueryCompatible(source, query))
                {
                    AddUnique(output, NodeKey(RoadElementKind.Polygon, source.zoneId));
                }
            }
        }

        private bool TargetElementAllows(BakedPortalRecord portal, RoadRouteQuery query)
        {
            if (portal.targetKind == RoadElementKind.Polygon)
            {
                return TryGetPolygon(portal.targetElementId, out BakedPolygonRecord polygon) &&
                       IsPolygonQueryCompatible(polygon, query);
            }

            return TryGetLane(portal.targetElementId, out BakedLaneRecord lane) &&
                   IsLaneQueryCompatible(lane, query.agentMask, query.tagFilter, query.agentRadius, true);
        }

        private static bool IsPolygonQueryCompatible(BakedPolygonRecord polygon, RoadRouteQuery query)
        {
            return polygon != null &&
                   polygon.open &&
                   polygon.AllowsAgent(query.agentMask) &&
                   query.tagFilter.Matches(polygon.tagMask);
        }

        private static bool PortalAllows(BakedPortalRecord portal, RoadRouteQuery query)
        {
            return portal != null &&
                   portal.open &&
                   portal.AllowsAgent(query.agentMask, query.agentRadius) &&
                   query.tagFilter.Matches(portal.tagMask);
        }

        private float GetTransitionCost(string from, string to)
        {
            ParseNodeKey(to, out RoadElementKind toKind, out string toId);
            if (toKind == RoadElementKind.Polygon &&
                TryGetPolygon(toId, out BakedPolygonRecord polygon))
            {
                return Mathf.Max(0.01f, polygon.traversalCost);
            }

            if (TryGetLane(toId, out BakedLaneRecord lane))
            {
                return lane.length / Mathf.Max(0.5f, lane.speedLimit);
            }

            return 1f;
        }

        private void BuildRouteSegments(
            List<string> path,
            RoadLocation start,
            RoadLocation destination,
            RoadNetworkRouteResult result)
        {
            result.segments.Clear();
            for (int i = 0; i < path.Count; i++)
            {
                string previous = i > 0 ? path[i - 1] : null;
                string next = i + 1 < path.Count ? path[i + 1] : null;
                AddRouteSegment(result, path[i], start, destination, previous, next);
            }
        }

        private void AddRouteSegment(
            RoadNetworkRouteResult result,
            string node,
            RoadLocation start,
            RoadLocation destination,
            string previous,
            string next)
        {
            ParseNodeKey(node, out RoadElementKind kind, out string id);
            RoadRouteSegment segment = new RoadRouteSegment
            {
                kind = kind,
                elementId = id
            };
            if (kind == RoadElementKind.Lane || kind == RoadElementKind.Connector)
            {
                TryGetLane(id, out BakedLaneRecord lane);
                segment.startDistance = string.Equals(id, start.elementId, StringComparison.Ordinal)
                    ? start.distanceAlong
                    : 0f;
                segment.endDistance = string.Equals(id, destination.elementId, StringComparison.Ordinal)
                    ? destination.distanceAlong
                    : lane == null ? 0f : lane.length;
                if (lane != null)
                {
                    TryEvaluate(id, segment.startDistance, out RoadLanePose entry);
                    TryEvaluate(id, segment.endDistance, out RoadLanePose exit);
                    segment.entryPosition = entry.position;
                    segment.exitPosition = exit.position;
                    segment.cost = lane.length / Mathf.Max(0.5f, lane.speedLimit);
                }
            }
            else if (kind == RoadElementKind.Polygon)
            {
                segment.entryPosition = string.Equals(id, start.elementId, StringComparison.Ordinal)
                    ? start.projectedPosition
                    : FindTransitionPosition(previous, node, true);
                segment.exitPosition = string.Equals(id, destination.elementId, StringComparison.Ordinal)
                    ? destination.projectedPosition
                    : FindTransitionPosition(node, next, false);
                if (TryGetPolygon(id, out BakedPolygonRecord polygon))
                {
                    segment.cost = polygon.traversalCost;
                }
            }

            result.segments.Add(segment);
        }

        private Vector3 FindTransitionPosition(string from, string to, bool targetSide)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                return Vector3.zero;
            }

            ParseNodeKey(from, out _, out string fromId);
            ParseNodeKey(to, out _, out string toId);
            for (int i = 0; i < portals.Count; i++)
            {
                BakedPortalRecord portal = portals[i];
                if (portal == null)
                {
                    continue;
                }

                bool forward = string.Equals(portal.sourceZoneId, fromId, StringComparison.Ordinal) &&
                               string.Equals(portal.targetElementId, toId, StringComparison.Ordinal);
                bool reverse = string.Equals(portal.sourceZoneId, toId, StringComparison.Ordinal) &&
                               string.Equals(portal.targetElementId, fromId, StringComparison.Ordinal);
                if (forward)
                {
                    return targetSide ? portal.targetPosition : portal.sourcePosition;
                }

                if (reverse)
                {
                    return targetSide ? portal.sourcePosition : portal.targetPosition;
                }
            }

            return Vector3.zero;
        }

        private static Bounds GetSearchBounds(in RoadAreaQuery query)
        {
            switch (query.shape)
            {
                case RoadAreaQueryShape.Sphere:
                    return new Bounds(query.center, Vector3.one * Mathf.Max(0f, query.radius) * 2f);
                case RoadAreaQueryShape.Bounds:
                    return query.bounds;
                default:
                    return new Bounds(query.center, Vector3.one * 0.01f);
            }
        }

        private static bool QueryShapeAccepts(in RoadAreaQuery query, Vector3 projected, float distance)
        {
            return query.shape switch
            {
                RoadAreaQueryShape.Sphere => distance <= Mathf.Max(0f, query.radius),
                RoadAreaQueryShape.Bounds => query.bounds.Contains(projected),
                _ => distance <= 0.001f
            };
        }

        private static void AddUniqueResult(
            List<RoadAreaQueryResult> results,
            RoadLocation location,
            float distance)
        {
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].location.kind == location.kind &&
                    string.Equals(results[i].location.elementId, location.elementId, StringComparison.Ordinal))
                {
                    if (distance < results[i].distance)
                    {
                        results[i] = new RoadAreaQueryResult { location = location, distance = distance };
                    }

                    return;
                }
            }

            results.Add(new RoadAreaQueryResult { location = location, distance = distance });
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }

        private static string NodeKey(RoadElementKind kind, string id)
        {
            return ((int)kind).ToString() + ":" + (id ?? string.Empty);
        }

        private static void ParseNodeKey(string key, out RoadElementKind kind, out string id)
        {
            int separator = key == null ? -1 : key.IndexOf(':');
            if (separator <= 0 || !int.TryParse(key.Substring(0, separator), out int rawKind))
            {
                kind = RoadElementKind.None;
                id = key ?? string.Empty;
                return;
            }

            kind = (RoadElementKind)rawKind;
            id = key.Substring(separator + 1);
        }

        private static List<string> ReconstructNodePath(Dictionary<string, string> previous, string current)
        {
            List<string> result = new List<string> { current };
            while (previous.TryGetValue(current, out string parent))
            {
                current = parent;
                result.Add(current);
            }

            result.Reverse();
            return result;
        }

        private static bool ContainsWorldPoint(IReadOnlyList<Vector3> vertices, Vector2 point)
        {
            bool inside = false;
            int previous = vertices.Count - 1;
            for (int i = 0; i < vertices.Count; previous = i++)
            {
                Vector2 a = new Vector2(vertices[i].x, vertices[i].z);
                Vector2 b = new Vector2(vertices[previous].x, vertices[previous].z);
                bool intersects = (a.y > point.y) != (b.y > point.y) &&
                                  point.x < (b.x - a.x) * (point.y - a.y) /
                                  Mathf.Max(0.00001f, b.y - a.y) + a.x;
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static Vector2 ClosestWorldBoundary(
            IReadOnlyList<Vector3> vertices,
            Vector2 point,
            out float distance)
        {
            Vector2 best = point;
            float bestSquared = float.PositiveInfinity;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 a = new Vector2(vertices[i].x, vertices[i].z);
                Vector2 b = new Vector2(vertices[(i + 1) % vertices.Count].x, vertices[(i + 1) % vertices.Count].z);
                Vector2 delta = b - a;
                float denominator = delta.sqrMagnitude;
                float t = denominator <= 0.00001f
                    ? 0f
                    : Mathf.Clamp01(Vector2.Dot(point - a, delta) / denominator);
                Vector2 candidate = a + delta * t;
                float squared = (point - candidate).sqrMagnitude;
                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    best = candidate;
                }
            }

            distance = Mathf.Sqrt(bestSquared);
            return best;
        }

        private static bool TryFindTriangleWorld(BakedPolygonRecord polygon, Vector2 point, out int triangleIndex)
        {
            triangleIndex = -1;
            for (int i = 0; i + 2 < polygon.triangles.Count; i += 3)
            {
                Vector2 a = ToXZ(polygon.vertices[polygon.triangles[i]]);
                Vector2 b = ToXZ(polygon.vertices[polygon.triangles[i + 1]]);
                Vector2 c = ToXZ(polygon.vertices[polygon.triangles[i + 2]]);
                if (PointInTriangle(point, a, b, c))
                {
                    triangleIndex = i / 3;
                    return true;
                }
            }

            return false;
        }

        private static List<int> FindTrianglePath(BakedPolygonRecord polygon, int start, int destination)
        {
            Queue<int> open = new Queue<int>();
            Dictionary<int, int> previous = new Dictionary<int, int>();
            HashSet<int> visited = new HashSet<int>();
            open.Enqueue(start);
            visited.Add(start);
            int triangleCount = polygon.triangles.Count / 3;
            while (open.Count > 0)
            {
                int current = open.Dequeue();
                if (current == destination)
                {
                    List<int> result = new List<int> { current };
                    while (previous.TryGetValue(current, out int parent))
                    {
                        current = parent;
                        result.Add(current);
                    }

                    result.Reverse();
                    return result;
                }

                for (int neighbor = 0; neighbor < triangleCount; neighbor++)
                {
                    if (!visited.Contains(neighbor) && TryGetSharedEdge(polygon, current, neighbor, out _, out _))
                    {
                        visited.Add(neighbor);
                        previous[neighbor] = current;
                        open.Enqueue(neighbor);
                    }
                }
            }

            return new List<int>();
        }

        private static List<PortalEdge> BuildTriangleCorridor(BakedPolygonRecord polygon, List<int> trianglePath)
        {
            List<PortalEdge> result = new List<PortalEdge>();
            for (int i = 1; i < trianglePath.Count; i++)
            {
                if (!TryGetSharedEdge(polygon, trianglePath[i - 1], trianglePath[i], out Vector3 a, out Vector3 b))
                {
                    continue;
                }

                Vector3 previousCenter = GetTriangleCenter(polygon, trianglePath[i - 1]);
                Vector3 nextCenter = GetTriangleCenter(polygon, trianglePath[i]);
                Vector3 travel = nextCenter - previousCenter;
                Vector3 edge = b - a;
                if (Vector3.Cross(travel, edge).y < 0f)
                {
                    (a, b) = (b, a);
                }

                result.Add(new PortalEdge(a, b));
            }

            return result;
        }

        private static void AppendFunnelPath(
            Vector3 start,
            Vector3 destination,
            List<PortalEdge> corridor,
            List<Vector3> output)
        {
            if (corridor.Count == 0)
            {
                output.Add(destination);
                return;
            }

            Vector2 apex = ToXZ(start);
            Vector2 left = ToXZ(corridor[0].left);
            Vector2 right = ToXZ(corridor[0].right);
            int apexIndex = 0;
            int leftIndex = 0;
            int rightIndex = 0;
            for (int i = 1; i <= corridor.Count; i++)
            {
                Vector2 nextLeft = i == corridor.Count ? ToXZ(destination) : ToXZ(corridor[i].left);
                Vector2 nextRight = i == corridor.Count ? ToXZ(destination) : ToXZ(corridor[i].right);
                if (TriangleArea2(apex, right, nextRight) <= 0f)
                {
                    if (Approximately(apex, right) || TriangleArea2(apex, left, nextRight) > 0f)
                    {
                        right = nextRight;
                        rightIndex = i;
                    }
                    else
                    {
                        output.Add(new Vector3(left.x, start.y, left.y));
                        apex = left;
                        apexIndex = leftIndex;
                        left = apex;
                        right = apex;
                        leftIndex = apexIndex;
                        rightIndex = apexIndex;
                        i = apexIndex;
                        continue;
                    }
                }

                if (TriangleArea2(apex, left, nextLeft) >= 0f)
                {
                    if (Approximately(apex, left) || TriangleArea2(apex, right, nextLeft) < 0f)
                    {
                        left = nextLeft;
                        leftIndex = i;
                    }
                    else
                    {
                        output.Add(new Vector3(right.x, start.y, right.y));
                        apex = right;
                        apexIndex = rightIndex;
                        left = apex;
                        right = apex;
                        leftIndex = apexIndex;
                        rightIndex = apexIndex;
                        i = apexIndex;
                    }
                }
            }

            if (output.Count == 0 || (output[output.Count - 1] - destination).sqrMagnitude > 0.0001f)
            {
                output.Add(destination);
            }
        }

        private static bool TryGetSharedEdge(
            BakedPolygonRecord polygon,
            int triangleA,
            int triangleB,
            out Vector3 a,
            out Vector3 b)
        {
            a = default;
            b = default;
            int offsetA = triangleA * 3;
            int offsetB = triangleB * 3;
            int found = 0;
            for (int i = 0; i < 3; i++)
            {
                int indexA = polygon.triangles[offsetA + i];
                for (int j = 0; j < 3; j++)
                {
                    if (indexA != polygon.triangles[offsetB + j])
                    {
                        continue;
                    }

                    if (found == 0)
                    {
                        a = polygon.vertices[indexA];
                    }
                    else
                    {
                        b = polygon.vertices[indexA];
                    }

                    found++;
                    break;
                }
            }

            return found == 2;
        }

        private static Vector3 GetTriangleCenter(BakedPolygonRecord polygon, int triangle)
        {
            int offset = triangle * 3;
            return (polygon.vertices[polygon.triangles[offset]] +
                    polygon.vertices[polygon.triangles[offset + 1]] +
                    polygon.vertices[polygon.triangles[offset + 2]]) / 3f;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float c0 = TriangleArea2(a, b, p);
            float c1 = TriangleArea2(b, c, p);
            float c2 = TriangleArea2(c, a, p);
            bool negative = c0 < -0.00001f || c1 < -0.00001f || c2 < -0.00001f;
            bool positive = c0 > 0.00001f || c1 > 0.00001f || c2 > 0.00001f;
            return !(negative && positive);
        }

        private static Vector2 ToXZ(Vector3 value)
        {
            return new Vector2(value.x, value.z);
        }

        private static float TriangleArea2(Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 ab = b - a;
            Vector2 ac = c - a;
            return ab.x * ac.y - ab.y * ac.x;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return (a - b).sqrMagnitude <= 0.000001f;
        }

        private readonly struct PortalEdge
        {
            public readonly Vector3 left;
            public readonly Vector3 right;

            public PortalEdge(Vector3 left, Vector3 right)
            {
                this.left = left;
                this.right = right;
            }
        }
    }

    internal static class RoadBoundsExtensions
    {
        public static bool ExpandAndContains(this Bounds bounds, Vector3 point, float expansion)
        {
            bounds.Expand(Mathf.Max(0f, expansion) * 2f);
            return bounds.Contains(point);
        }
    }
}
