using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    public enum VehicleRoadLaneSortMode
    {
        Stable,
        ByLaneId
    }

    public enum VehicleRoadRouteTargetSelectionMode
    {
        Random,
        Cycle,
        NearestCost,
        LowestCost
    }

    public sealed class VehicleRoadLaneQuery
    {
        public RoadAgentMask agentMask = RoadAgentMask.Car;
        public bool includeConnectors;
        public bool onlyOpen = true;
        public bool excludeOrphaned = true;
        public bool requireOutgoingConnection;
        public bool requireRouteNode = true;
        public VehicleRoadLaneSortMode sortMode = VehicleRoadLaneSortMode.Stable;
        public float minLength;
    }

    public sealed class VehicleRoadRouteCandidateLaneQuery
    {
        public RoadAgentMask agentMask = RoadAgentMask.Car;
        public bool includeTerminalLanes = true;
        public bool includeDeadEnds;
        public float minLength = 3f;
        public bool excludeConnectors = true;
        public bool onlyOpen = true;
        public bool excludeOrphaned = true;
    }

    public sealed class VehicleRoadLaneFilterQuery
    {
        public RoadAgentMask agentMask = RoadAgentMask.Car;
        public bool excludeConnectors = true;
        public bool onlyOpen = true;
        public bool excludeOrphaned = true;
        public bool requireOutgoingConnection;
        public float minLength;
    }

    public sealed class VehicleRoadSpawnLaneQuery
    {
        public RoadAgentMask agentMask = RoadAgentMask.Car;
        public float minDistance = 35f;
        public float maxDistance = 235f;
        public float laneSearchDistance = 25f;
        public float maxHeightDifference = 3f;
        public List<string> candidateLaneIds = new List<string>();
        public bool requireReachableCandidate = true;
        public bool excludeConnectors = true;
        public int maxTrials = 32;
    }

    public sealed class VehicleRoadRouteTargetSelectionQuery
    {
        public string currentLaneId = string.Empty;
        public RoadAgentMask agentMask = RoadAgentMask.Car;
        public List<string> candidateLaneIds = new List<string>();
        public VehicleRoadRouteTargetSelectionMode selectionMode = VehicleRoadRouteTargetSelectionMode.Random;
        public int previousIndex = -1;
        public float minimumRouteCost = 0.001f;
        public bool allowSameLane;
        public bool excludeConnectors = true;
    }

    public sealed class VehicleRoadLaneInfoResult
    {
        public bool found;
        public string laneId = string.Empty;
        public RoadElementKind kind;
        public float length;
        public bool open;
        public bool orphaned;
        public RoadAgentMask agentMask;
        public int outgoingConnectionCount;
        public int adjacentLinkCount;
    }

    public sealed class VehicleRoadSpawnLaneResult
    {
        public bool found;
        public string laneId = string.Empty;
        public Vector3 position;
        public Vector3 forward = Vector3.forward;
        public Vector3 up = Vector3.up;
        public float distanceFromAnchor;
        public string failureReason = string.Empty;
    }

    public sealed class VehicleRoadRouteTargetSelectionResult
    {
        public bool success;
        public string destinationLaneId = string.Empty;
        public int selectedIndex = -1;
        public List<string> routeLaneIds = new List<string>();
        public float totalCost;
        public string failureReason = string.Empty;
    }

    public sealed partial class VehicleRoadSubsystem
    {
        public List<string> GetLaneIds(VehicleRoadLaneQuery query)
        {
            query ??= new VehicleRoadLaneQuery();
            List<string> result = new List<string>();
            for (int networkIndex = 0; networkIndex < registeredNetworks.Count; networkIndex++)
            {
                BakedLaneNetwork network = registeredNetworks[networkIndex];
                if (network == null)
                {
                    continue;
                }

                IReadOnlyList<BakedLaneRecord> lanes = network.Lanes;
                for (int laneIndex = 0; laneIndex < lanes.Count; laneIndex++)
                {
                    BakedLaneRecord lane = lanes[laneIndex];
                    if (LanePassesQuery(network, lane, query))
                    {
                        result.Add(lane.laneId);
                    }
                }
            }

            if (query.sortMode == VehicleRoadLaneSortMode.ByLaneId)
            {
                result.Sort(StringComparer.Ordinal);
            }

            return result;
        }

        public List<string> GetRouteCandidateLaneIds(VehicleRoadRouteCandidateLaneQuery query)
        {
            query ??= new VehicleRoadRouteCandidateLaneQuery();
            List<string> result = new List<string>();
            RoadAgentMask agentMask = NormalizeBlueprintAgentMask(query.agentMask);
            for (int networkIndex = 0; networkIndex < registeredNetworks.Count; networkIndex++)
            {
                BakedLaneNetwork network = registeredNetworks[networkIndex];
                if (network == null)
                {
                    continue;
                }

                IReadOnlyList<BakedLaneRecord> lanes = network.Lanes;
                for (int laneIndex = 0; laneIndex < lanes.Count; laneIndex++)
                {
                    BakedLaneRecord lane = lanes[laneIndex];
                    if (!LanePassesBase(
                            network,
                            lane,
                            !query.excludeConnectors,
                            query.onlyOpen,
                            query.excludeOrphaned,
                            true,
                            agentMask,
                            query.minLength))
                    {
                        continue;
                    }

                    int outgoingCount = CountUsableOutgoingConnections(
                        network,
                        lane.laneId,
                        agentMask,
                        query.onlyOpen,
                        query.excludeOrphaned);
                    if (outgoingCount == 0)
                    {
                        int incomingCount = CountUsableIncomingConnections(
                            network,
                            lane.laneId,
                            agentMask,
                            query.onlyOpen,
                            query.excludeOrphaned);
                        if (incomingCount > 0)
                        {
                            if (!query.includeTerminalLanes)
                            {
                                continue;
                            }
                        }
                        else if (!query.includeDeadEnds)
                        {
                            continue;
                        }
                    }

                    result.Add(lane.laneId);
                }
            }

            return result;
        }

        public List<string> FilterLaneIds(IList<string> laneIds, VehicleRoadLaneFilterQuery query, out int removedCount)
        {
            query ??= new VehicleRoadLaneFilterQuery();
            List<string> result = new List<string>();
            int inputCount = laneIds == null ? 0 : laneIds.Count;
            RoadAgentMask agentMask = NormalizeBlueprintAgentMask(query.agentMask);
            for (int i = 0; i < inputCount; i++)
            {
                string laneId = laneIds[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(laneId) ||
                    !TryFindRegisteredLane(laneId, out BakedLaneNetwork network, out BakedLaneRecord lane))
                {
                    continue;
                }

                VehicleRoadLaneQuery laneQuery = new VehicleRoadLaneQuery
                {
                    agentMask = agentMask,
                    includeConnectors = !query.excludeConnectors,
                    onlyOpen = query.onlyOpen,
                    excludeOrphaned = query.excludeOrphaned,
                    requireOutgoingConnection = query.requireOutgoingConnection,
                    requireRouteNode = true,
                    minLength = query.minLength
                };
                if (LanePassesQuery(network, lane, laneQuery))
                {
                    result.Add(lane.laneId);
                }
            }

            removedCount = inputCount - result.Count;
            return result;
        }

        public VehicleRoadLaneInfoResult GetLaneInfo(string laneId)
        {
            VehicleRoadLaneInfoResult result = new VehicleRoadLaneInfoResult
            {
                laneId = laneId ?? string.Empty
            };
            if (!TryFindRegisteredLane(laneId, out BakedLaneNetwork network, out BakedLaneRecord lane))
            {
                return result;
            }

            result.found = true;
            result.laneId = lane.laneId ?? string.Empty;
            result.kind = ToElementKind(lane);
            result.length = lane.length;
            result.open = lane.open && !trafficCostProvider.IsLaneClosed(lane.laneId);
            result.orphaned = lane.orphaned;
            result.agentMask = lane.allowedAgents;
            result.outgoingConnectionCount = network.GetOutgoingConnections(lane.laneId).Count;
            result.adjacentLinkCount = network.GetAdjacentLinks(lane.laneId).Count;
            return result;
        }

        public VehicleRoadSpawnLaneResult FindSpawnLaneAroundTransform(Transform anchor, VehicleRoadSpawnLaneQuery query)
        {
            query ??= new VehicleRoadSpawnLaneQuery();
            VehicleRoadSpawnLaneResult result = new VehicleRoadSpawnLaneResult();
            if (anchor == null)
            {
                result.failureReason = "Missing anchor Transform.";
                return result;
            }

            RoadAgentMask agentMask = NormalizeBlueprintAgentMask(query.agentMask);
            List<string> candidateLaneIds = ResolveSpawnCandidateLaneIds(query, agentMask);
            if (candidateLaneIds.Count == 0)
            {
                result.failureReason = "No candidate lanes.";
                return result;
            }

            HashSet<string> allowedLaneIds = new HashSet<string>(candidateLaneIds, StringComparer.Ordinal);
            float minDistance = Mathf.Max(0f, query.minDistance);
            float maxDistance = Mathf.Max(minDistance, query.maxDistance);
            float laneSearchDistance = query.laneSearchDistance > 0f ? query.laneSearchDistance : defaultNearestLaneSearchDistance;
            float maxHeightDifference = query.maxHeightDifference > 0f ? query.maxHeightDifference : defaultMaximumHeightDifference;
            int maxTrials = Mathf.Max(1, query.maxTrials);
            string lastFailure = "No lane found within search distance.";
            for (int trial = 0; trial < maxTrials; trial++)
            {
                Vector3 randomPoint = RandomPointAround(anchor.position, minDistance, maxDistance);
                if (!TryFindNearestLane(
                        randomPoint,
                        Vector3.zero,
                        agentMask,
                        laneSearchDistance,
                        maxHeightDifference,
                        out VehicleRoadNearestResult nearest,
                        allowedLaneIds))
                {
                    continue;
                }

                float distanceFromAnchor = Vector3.Distance(anchor.position, nearest.Position);
                if (distanceFromAnchor + 0.0001f < minDistance ||
                    distanceFromAnchor - 0.0001f > maxDistance)
                {
                    lastFailure = "Nearest lane was outside the requested distance band.";
                    continue;
                }

                if (query.requireReachableCandidate &&
                    !HasReachableCandidate(nearest.LaneId, candidateLaneIds, agentMask))
                {
                    lastFailure = "No reachable candidate lane from selected spawn lane.";
                    continue;
                }

                result.found = true;
                result.laneId = nearest.LaneId;
                result.position = nearest.Position;
                result.forward = nearest.Forward;
                result.up = nearest.Up;
                result.distanceFromAnchor = distanceFromAnchor;
                result.failureReason = string.Empty;
                return result;
            }

            result.failureReason = lastFailure;
            return result;
        }

        public VehicleRoadRouteTargetSelectionResult SelectReachableRouteTarget(VehicleRoadRouteTargetSelectionQuery query)
        {
            query ??= new VehicleRoadRouteTargetSelectionQuery();
            VehicleRoadRouteTargetSelectionResult result = new VehicleRoadRouteTargetSelectionResult();
            string currentLaneId = query.currentLaneId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(currentLaneId))
            {
                result.failureReason = "Missing current lane id.";
                return result;
            }

            RoadAgentMask agentMask = NormalizeBlueprintAgentMask(query.agentMask);
            if (!TryFindRegisteredLane(currentLaneId, out _, out BakedLaneRecord currentLane) ||
                currentLane == null ||
                !currentLane.open ||
                currentLane.orphaned ||
                !currentLane.AllowsAgent(agentMask) ||
                trafficCostProvider.IsLaneClosed(currentLane.laneId))
            {
                result.failureReason = "Current lane is not registered or open.";
                return result;
            }

            List<RouteTargetCandidate> candidates = BuildRouteTargetCandidates(query, agentMask);
            if (candidates.Count == 0)
            {
                result.failureReason = "No candidate lanes.";
                return result;
            }

            switch (query.selectionMode)
            {
                case VehicleRoadRouteTargetSelectionMode.Cycle:
                    if (TrySelectCycleRouteTarget(query, candidates, agentMask, result))
                    {
                        return result;
                    }

                    break;
                case VehicleRoadRouteTargetSelectionMode.NearestCost:
                case VehicleRoadRouteTargetSelectionMode.LowestCost:
                    if (TrySelectCostRouteTarget(query, candidates, agentMask, result))
                    {
                        return result;
                    }

                    break;
                default:
                    if (TrySelectRandomRouteTarget(query, candidates, agentMask, result))
                    {
                        return result;
                    }

                    break;
            }

            result.failureReason = "No reachable route target.";
            return result;
        }

        private bool LanePassesQuery(BakedLaneNetwork network, BakedLaneRecord lane, VehicleRoadLaneQuery query)
        {
            RoadAgentMask agentMask = NormalizeBlueprintAgentMask(query.agentMask);
            if (!LanePassesBase(
                    network,
                    lane,
                    query.includeConnectors,
                    query.onlyOpen,
                    query.excludeOrphaned,
                    query.requireRouteNode,
                    agentMask,
                    query.minLength))
            {
                return false;
            }

            return !query.requireOutgoingConnection ||
                   CountUsableOutgoingConnections(
                       network,
                       lane.laneId,
                       agentMask,
                       query.onlyOpen,
                       query.excludeOrphaned) > 0;
        }

        private bool LanePassesBase(
            BakedLaneNetwork network,
            BakedLaneRecord lane,
            bool includeConnectors,
            bool onlyOpen,
            bool excludeOrphaned,
            bool requireRouteNode,
            RoadAgentMask agentMask,
            float minLength)
        {
            if (network == null ||
                lane == null ||
                string.IsNullOrWhiteSpace(lane.laneId) ||
                !includeConnectors && lane.kind == RoadLaneKind.Connector ||
                excludeOrphaned && lane.orphaned ||
                minLength > 0f && lane.length + 0.0001f < minLength ||
                !lane.AllowsAgent(agentMask))
            {
                return false;
            }

            if (onlyOpen && (!lane.open || trafficCostProvider.IsLaneClosed(lane.laneId)))
            {
                return false;
            }

            return !requireRouteNode || IsRegisteredRouteLane(network, lane.laneId);
        }

        private bool IsRegisteredRouteLane(BakedLaneNetwork network, string laneId)
        {
            return network != null &&
                   !string.IsNullOrWhiteSpace(laneId) &&
                   networkByLaneId.TryGetValue(laneId, out BakedLaneNetwork owner) &&
                   owner == network &&
                   graphByNetwork.ContainsKey(network);
        }

        private bool TryFindRegisteredLane(string laneId, out BakedLaneNetwork network, out BakedLaneRecord lane)
        {
            lane = null;
            network = null;
            return networkByLaneId.TryGetValue(laneId ?? string.Empty, out network) &&
                   network != null &&
                   network.TryGetLane(laneId, out lane) &&
                   lane != null;
        }

        private int CountUsableOutgoingConnections(
            BakedLaneNetwork network,
            string laneId,
            RoadAgentMask agentMask,
            bool onlyOpen,
            bool excludeOrphaned)
        {
            if (network == null || string.IsNullOrWhiteSpace(laneId))
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<BakedLaneConnectionRecord> outgoing = network.GetOutgoingConnections(laneId);
            for (int i = 0; i < outgoing.Count; i++)
            {
                BakedLaneConnectionRecord connection = outgoing[i];
                if (connection == null ||
                    string.IsNullOrWhiteSpace(connection.toLaneId) ||
                    onlyOpen && !connection.open ||
                    !network.TryGetLane(connection.toLaneId, out BakedLaneRecord targetLane) ||
                    !LanePassesBase(
                        network,
                        targetLane,
                        true,
                        onlyOpen,
                        excludeOrphaned,
                        true,
                        agentMask,
                        0f))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private int CountUsableIncomingConnections(
            BakedLaneNetwork network,
            string laneId,
            RoadAgentMask agentMask,
            bool onlyOpen,
            bool excludeOrphaned)
        {
            if (network == null || string.IsNullOrWhiteSpace(laneId))
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<BakedLaneConnectionRecord> connections = network.Connections;
            for (int i = 0; i < connections.Count; i++)
            {
                BakedLaneConnectionRecord connection = connections[i];
                if (connection == null ||
                    !string.Equals(connection.toLaneId, laneId, StringComparison.Ordinal) ||
                    onlyOpen && !connection.open ||
                    !network.TryGetLane(connection.fromLaneId, out BakedLaneRecord sourceLane) ||
                    !LanePassesBase(
                        network,
                        sourceLane,
                        true,
                        onlyOpen,
                        excludeOrphaned,
                        true,
                        agentMask,
                        0f))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private List<string> ResolveSpawnCandidateLaneIds(VehicleRoadSpawnLaneQuery query, RoadAgentMask agentMask)
        {
            if (query.candidateLaneIds != null && query.candidateLaneIds.Count > 0)
            {
                return FilterLaneIds(
                    query.candidateLaneIds,
                    new VehicleRoadLaneFilterQuery
                    {
                        agentMask = agentMask,
                        excludeConnectors = query.excludeConnectors,
                        onlyOpen = true,
                        excludeOrphaned = true,
                        requireOutgoingConnection = false,
                        minLength = 0f
                    },
                    out _);
            }

            return GetRouteCandidateLaneIds(new VehicleRoadRouteCandidateLaneQuery
            {
                agentMask = agentMask,
                includeTerminalLanes = true,
                includeDeadEnds = false,
                minLength = 3f,
                excludeConnectors = query.excludeConnectors,
                onlyOpen = true,
                excludeOrphaned = true
            });
        }

        private bool HasReachableCandidate(string startLaneId, List<string> candidateLaneIds, RoadAgentMask agentMask)
        {
            if (string.IsNullOrWhiteSpace(startLaneId) || candidateLaneIds == null)
            {
                return false;
            }

            for (int i = 0; i < candidateLaneIds.Count; i++)
            {
                string destinationLaneId = candidateLaneIds[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(destinationLaneId) ||
                    string.Equals(startLaneId, destinationLaneId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryFindRoute(new LaneRouteQuery(startLaneId, destinationLaneId, agentMask), out VehicleRoadRouteResult route) &&
                    route != null &&
                    route.totalCost > 0.001f)
                {
                    return true;
                }
            }

            return false;
        }

        private List<RouteTargetCandidate> BuildRouteTargetCandidates(
            VehicleRoadRouteTargetSelectionQuery query,
            RoadAgentMask agentMask)
        {
            List<RouteTargetCandidate> result = new List<RouteTargetCandidate>();
            if (query.candidateLaneIds != null && query.candidateLaneIds.Count > 0)
            {
                VehicleRoadLaneQuery laneQuery = new VehicleRoadLaneQuery
                {
                    agentMask = agentMask,
                    includeConnectors = !query.excludeConnectors,
                    onlyOpen = true,
                    excludeOrphaned = true,
                    requireOutgoingConnection = false,
                    requireRouteNode = true,
                    minLength = 0f
                };
                for (int i = 0; i < query.candidateLaneIds.Count; i++)
                {
                    string laneId = query.candidateLaneIds[i] ?? string.Empty;
                    if (TryFindRegisteredLane(laneId, out BakedLaneNetwork network, out BakedLaneRecord lane) &&
                        LanePassesQuery(network, lane, laneQuery))
                    {
                        result.Add(new RouteTargetCandidate(lane.laneId, i));
                    }
                }

                return result;
            }

            List<string> automatic = GetRouteCandidateLaneIds(new VehicleRoadRouteCandidateLaneQuery
            {
                agentMask = agentMask,
                includeTerminalLanes = true,
                includeDeadEnds = false,
                minLength = 3f,
                excludeConnectors = query.excludeConnectors,
                onlyOpen = true,
                excludeOrphaned = true
            });
            for (int i = 0; i < automatic.Count; i++)
            {
                result.Add(new RouteTargetCandidate(automatic[i], i));
            }

            return result;
        }

        private bool TrySelectRandomRouteTarget(
            VehicleRoadRouteTargetSelectionQuery query,
            List<RouteTargetCandidate> candidates,
            RoadAgentMask agentMask,
            VehicleRoadRouteTargetSelectionResult result)
        {
            List<ReachableRouteTarget> reachable = new List<ReachableRouteTarget>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (TryResolveReachableRouteTarget(query, candidates[i], agentMask, out ReachableRouteTarget target))
                {
                    reachable.Add(target);
                }
            }

            if (reachable.Count == 0)
            {
                return false;
            }

            WriteRouteTargetSelection(result, reachable[UnityEngine.Random.Range(0, reachable.Count)]);
            return true;
        }

        private bool TrySelectCycleRouteTarget(
            VehicleRoadRouteTargetSelectionQuery query,
            List<RouteTargetCandidate> candidates,
            RoadAgentMask agentMask,
            VehicleRoadRouteTargetSelectionResult result)
        {
            int startIndex = ResolveCycleStart(candidates, query.previousIndex);
            for (int offset = 0; offset < candidates.Count; offset++)
            {
                int index = (startIndex + offset) % candidates.Count;
                if (TryResolveReachableRouteTarget(query, candidates[index], agentMask, out ReachableRouteTarget target))
                {
                    WriteRouteTargetSelection(result, target);
                    return true;
                }
            }

            return false;
        }

        private bool TrySelectCostRouteTarget(
            VehicleRoadRouteTargetSelectionQuery query,
            List<RouteTargetCandidate> candidates,
            RoadAgentMask agentMask,
            VehicleRoadRouteTargetSelectionResult result)
        {
            bool found = false;
            ReachableRouteTarget best = default;
            float bestRank = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!TryResolveReachableRouteTarget(query, candidates[i], agentMask, out ReachableRouteTarget target))
                {
                    continue;
                }

                float rank = query.selectionMode == VehicleRoadRouteTargetSelectionMode.NearestCost
                    ? EstimateLaneDistance(query.currentLaneId, target.destinationLaneId, target.totalCost)
                    : target.totalCost;
                if (found &&
                    (rank > bestRank + 0.0001f ||
                     Mathf.Abs(rank - bestRank) <= 0.0001f && target.totalCost >= best.totalCost))
                {
                    continue;
                }

                bestRank = rank;
                best = target;
                found = true;
            }

            if (!found)
            {
                return false;
            }

            WriteRouteTargetSelection(result, best);
            return true;
        }

        private bool TryResolveReachableRouteTarget(
            VehicleRoadRouteTargetSelectionQuery query,
            RouteTargetCandidate candidate,
            RoadAgentMask agentMask,
            out ReachableRouteTarget result)
        {
            result = default;
            string currentLaneId = query.currentLaneId ?? string.Empty;
            string destinationLaneId = candidate.laneId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(destinationLaneId))
            {
                return false;
            }

            if (string.Equals(currentLaneId, destinationLaneId, StringComparison.Ordinal))
            {
                if (!query.allowSameLane)
                {
                    return false;
                }

                result = new ReachableRouteTarget(
                    destinationLaneId,
                    candidate.originalIndex,
                    new List<string> { currentLaneId },
                    0f);
                return true;
            }

            if (!TryFindRoute(new LaneRouteQuery(currentLaneId, destinationLaneId, agentMask), out VehicleRoadRouteResult route) ||
                route == null ||
                route.totalCost + 0.0001f < Mathf.Max(0f, query.minimumRouteCost))
            {
                return false;
            }

            result = new ReachableRouteTarget(
                destinationLaneId,
                candidate.originalIndex,
                route.laneIds == null ? new List<string>() : new List<string>(route.laneIds),
                route.totalCost);
            return true;
        }

        private float EstimateLaneDistance(string startLaneId, string destinationLaneId, float fallback)
        {
            if (!TryFindRegisteredLane(startLaneId, out BakedLaneNetwork startNetwork, out BakedLaneRecord startLane) ||
                !TryFindRegisteredLane(destinationLaneId, out BakedLaneNetwork destinationNetwork, out BakedLaneRecord destinationLane) ||
                startNetwork != destinationNetwork ||
                !TryEvaluateLaneMidpoint(startNetwork, startLane, out Vector3 start) ||
                !TryEvaluateLaneMidpoint(destinationNetwork, destinationLane, out Vector3 destination))
            {
                return fallback;
            }

            return Vector3.Distance(start, destination);
        }

        private static bool TryEvaluateLaneMidpoint(BakedLaneNetwork network, BakedLaneRecord lane, out Vector3 position)
        {
            position = Vector3.zero;
            if (network == null || lane == null || !network.TryEvaluate(lane.laneId, lane.length * 0.5f, out RoadLanePose pose))
            {
                return false;
            }

            position = pose.position;
            return true;
        }

        private static int ResolveCycleStart(List<RouteTargetCandidate> candidates, int previousIndex)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].originalIndex == previousIndex)
                {
                    return (i + 1) % candidates.Count;
                }
            }

            int next = previousIndex + 1;
            return ((next % candidates.Count) + candidates.Count) % candidates.Count;
        }

        private static void WriteRouteTargetSelection(
            VehicleRoadRouteTargetSelectionResult result,
            ReachableRouteTarget target)
        {
            result.success = true;
            result.destinationLaneId = target.destinationLaneId ?? string.Empty;
            result.selectedIndex = target.selectedIndex;
            result.routeLaneIds = target.routeLaneIds == null ? new List<string>() : new List<string>(target.routeLaneIds);
            result.totalCost = target.totalCost;
            result.failureReason = string.Empty;
        }

        private static Vector3 RandomPointAround(Vector3 center, float minDistance, float maxDistance)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float distance = UnityEngine.Random.Range(minDistance, maxDistance);
            return center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        }

        private static RoadAgentMask NormalizeBlueprintAgentMask(RoadAgentMask agentMask)
        {
            return agentMask == RoadAgentMask.None ? RoadAgentMask.Car : agentMask;
        }

        private static RoadElementKind ToElementKind(BakedLaneRecord lane)
        {
            return lane != null && lane.kind == RoadLaneKind.Connector
                ? RoadElementKind.Connector
                : RoadElementKind.Lane;
        }

        private readonly struct RouteTargetCandidate
        {
            public readonly string laneId;
            public readonly int originalIndex;

            public RouteTargetCandidate(string laneId, int originalIndex)
            {
                this.laneId = laneId ?? string.Empty;
                this.originalIndex = originalIndex;
            }
        }

        private readonly struct ReachableRouteTarget
        {
            public readonly string destinationLaneId;
            public readonly int selectedIndex;
            public readonly List<string> routeLaneIds;
            public readonly float totalCost;

            public ReachableRouteTarget(
                string destinationLaneId,
                int selectedIndex,
                List<string> routeLaneIds,
                float totalCost)
            {
                this.destinationLaneId = destinationLaneId ?? string.Empty;
                this.selectedIndex = selectedIndex;
                this.routeLaneIds = routeLaneIds ?? new List<string>();
                this.totalCost = totalCost;
            }
        }
    }
}
