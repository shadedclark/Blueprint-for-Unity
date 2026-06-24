using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    public readonly struct LaneRouteQuery
    {
        public readonly string startLaneId;
        public readonly string destinationLaneId;
        public readonly RoadAgentMask agentMask;

        public LaneRouteQuery(string startLaneId, string destinationLaneId, RoadAgentMask agentMask)
        {
            this.startLaneId = startLaneId ?? string.Empty;
            this.destinationLaneId = destinationLaneId ?? string.Empty;
            this.agentMask = agentMask == RoadAgentMask.None ? RoadAgentMask.MotorVehicles : agentMask;
        }
    }

    public interface ILaneCostProvider
    {
        bool IsBlocked(
            BakedLaneRecord fromLane,
            BakedLaneConnectionRecord connection,
            BakedLaneRecord toLane,
            LaneRouteQuery query);

        float GetAdditionalCost(
            BakedLaneRecord fromLane,
            BakedLaneConnectionRecord connection,
            BakedLaneRecord toLane,
            LaneRouteQuery query);
    }

    public interface ILaneSignalCostProvider : ILaneCostProvider
    {
    }

    public sealed class MutableLaneTrafficCostProvider : ILaneCostProvider
    {
        private readonly HashSet<string> closedLaneIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> congestionCosts = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> signalCosts = new Dictionary<string, float>(StringComparer.Ordinal);

        public int ClosedLaneCount => closedLaneIds.Count;
        public int CongestionCostCount => congestionCosts.Count;
        public int SignalCostCount => signalCosts.Count;

        public void SetLaneClosed(string laneId, bool closed)
        {
            laneId ??= string.Empty;
            if (closed)
            {
                closedLaneIds.Add(laneId);
            }
            else
            {
                closedLaneIds.Remove(laneId);
            }
        }

        public bool IsLaneClosed(string laneId)
        {
            return closedLaneIds.Contains(laneId ?? string.Empty);
        }

        public void SetCongestionCost(string laneId, float cost)
        {
            SetCost(congestionCosts, laneId, cost);
        }

        public void SetSignalCost(string connectionId, float cost)
        {
            SetCost(signalCosts, connectionId, cost);
        }

        public bool IsBlocked(
            BakedLaneRecord fromLane,
            BakedLaneConnectionRecord connection,
            BakedLaneRecord toLane,
            LaneRouteQuery query)
        {
            return fromLane == null ||
                   toLane == null ||
                   closedLaneIds.Contains(fromLane.laneId) ||
                   closedLaneIds.Contains(toLane.laneId);
        }

        public float GetAdditionalCost(
            BakedLaneRecord fromLane,
            BakedLaneConnectionRecord connection,
            BakedLaneRecord toLane,
            LaneRouteQuery query)
        {
            float cost = 0f;
            if (toLane != null && congestionCosts.TryGetValue(toLane.laneId, out float congestion))
            {
                cost += congestion;
            }

            if (connection != null && signalCosts.TryGetValue(connection.connectionId, out float signal))
            {
                cost += signal;
            }

            return Mathf.Max(0f, cost);
        }

        public void Clear()
        {
            closedLaneIds.Clear();
            congestionCosts.Clear();
            signalCosts.Clear();
        }

        private static void SetCost(Dictionary<string, float> costs, string id, float cost)
        {
            id ??= string.Empty;
            cost = Mathf.Max(0f, cost);
            if (cost <= 0f)
            {
                costs.Remove(id);
            }
            else
            {
                costs[id] = cost;
            }
        }
    }

    public sealed class LaneGraph
    {
        private readonly BakedLaneNetwork network;
        private readonly List<ILaneCostProvider> costProviders = new List<ILaneCostProvider>();

        public LaneGraph(BakedLaneNetwork network, IEnumerable<ILaneCostProvider> providers = null)
        {
            this.network = network;
            if (providers != null)
            {
                costProviders.AddRange(providers);
            }
        }

        public IList<ILaneCostProvider> CostProviders => costProviders;

        public bool TryFindRoute(LaneRouteQuery query, out List<string> laneIds, out float totalCost)
        {
            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.RouteSearch);
            laneIds = new List<string>();
            totalCost = 0f;
            if (network == null ||
                !network.TryGetLane(query.startLaneId, out BakedLaneRecord start) ||
                !network.TryGetLane(query.destinationLaneId, out BakedLaneRecord destination) ||
                !IsLaneUsable(start, query.agentMask) ||
                !IsLaneUsable(destination, query.agentMask))
            {
                return false;
            }

            Dictionary<string, float> gScore = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                [start.laneId] = 0f
            };
            Dictionary<string, string> cameFrom = new Dictionary<string, string>(StringComparer.Ordinal);
            List<OpenNode> open = new List<OpenNode>
            {
                new OpenNode(start.laneId, Heuristic(start, destination))
            };
            HashSet<string> closed = new HashSet<string>(StringComparer.Ordinal);

            while (open.Count > 0)
            {
                int bestIndex = 0;
                for (int i = 1; i < open.Count; i++)
                {
                    if (open[i].score < open[bestIndex].score)
                    {
                        bestIndex = i;
                    }
                }

                OpenNode currentNode = open[bestIndex];
                open.RemoveAt(bestIndex);
                if (!closed.Add(currentNode.laneId))
                {
                    continue;
                }

                if (string.Equals(currentNode.laneId, destination.laneId, StringComparison.Ordinal))
                {
                    totalCost = gScore[currentNode.laneId];
                    Reconstruct(cameFrom, currentNode.laneId, laneIds);
                    return true;
                }

                if (!network.TryGetLane(currentNode.laneId, out BakedLaneRecord currentLane))
                {
                    continue;
                }

                IReadOnlyList<BakedLaneConnectionRecord> outgoing = network.GetOutgoingConnections(currentNode.laneId);
                for (int i = 0; i < outgoing.Count; i++)
                {
                    BakedLaneConnectionRecord connection = outgoing[i];
                    if (connection == null ||
                        !connection.open ||
                        !network.TryGetLane(connection.toLaneId, out BakedLaneRecord nextLane) ||
                        !IsLaneUsable(nextLane, query.agentMask) ||
                        IsBlocked(currentLane, connection, nextLane, query))
                    {
                        continue;
                    }

                    float tentative = gScore[currentNode.laneId] +
                                      TravelCost(nextLane) +
                                      Mathf.Max(0f, connection.baseCost) +
                                      AdditionalCost(currentLane, connection, nextLane, query);
                    if (gScore.TryGetValue(nextLane.laneId, out float existing) && tentative >= existing)
                    {
                        continue;
                    }

                    cameFrom[nextLane.laneId] = currentNode.laneId;
                    gScore[nextLane.laneId] = tentative;
                    open.Add(new OpenNode(nextLane.laneId, tentative + Heuristic(nextLane, destination)));
                }
            }

            return false;
        }

        private bool IsBlocked(
            BakedLaneRecord from,
            BakedLaneConnectionRecord connection,
            BakedLaneRecord to,
            LaneRouteQuery query)
        {
            for (int i = 0; i < costProviders.Count; i++)
            {
                if (costProviders[i] != null && costProviders[i].IsBlocked(from, connection, to, query))
                {
                    return true;
                }
            }

            return false;
        }

        private float AdditionalCost(
            BakedLaneRecord from,
            BakedLaneConnectionRecord connection,
            BakedLaneRecord to,
            LaneRouteQuery query)
        {
            float result = 0f;
            for (int i = 0; i < costProviders.Count; i++)
            {
                if (costProviders[i] != null)
                {
                    result += Mathf.Max(0f, costProviders[i].GetAdditionalCost(from, connection, to, query));
                }
            }

            return result;
        }

        private static bool IsLaneUsable(BakedLaneRecord lane, RoadAgentMask agentMask)
        {
            return lane != null && lane.open && !lane.orphaned && lane.AllowsAgent(agentMask);
        }

        private static float TravelCost(BakedLaneRecord lane)
        {
            return lane.length / Mathf.Max(0.5f, lane.speedLimit);
        }

        private static float Heuristic(BakedLaneRecord lane, BakedLaneRecord destination)
        {
            // The baked format intentionally keeps topology independent of scene objects.
            // A zero heuristic is admissible and makes this A* implementation behave as Dijkstra
            // when endpoint positions are not required by a caller.
            return 0f;
        }

        private static void Reconstruct(
            Dictionary<string, string> cameFrom,
            string current,
            List<string> output)
        {
            output.Add(current);
            while (cameFrom.TryGetValue(current, out string previous))
            {
                current = previous;
                output.Add(current);
            }

            output.Reverse();
        }

        private readonly struct OpenNode
        {
            public readonly string laneId;
            public readonly float score;

            public OpenNode(string laneId, float score)
            {
                this.laneId = laneId;
                this.score = score;
            }
        }
    }
}
