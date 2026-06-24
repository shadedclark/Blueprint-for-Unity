using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    public readonly struct VehicleRoadNearestResult
    {
        public readonly BakedLaneNetwork network;
        public readonly BakedLaneNearestResult laneResult;

        public VehicleRoadNearestResult(BakedLaneNetwork network, BakedLaneNearestResult laneResult)
        {
            this.network = network;
            this.laneResult = laneResult;
        }

        public string LaneId => laneResult.lane == null ? string.Empty : laneResult.lane.laneId;
        public Vector3 Position => laneResult.position;
        public Vector3 Forward => laneResult.forward;
        public Vector3 Up => laneResult.up;
        public float DistanceAlongLane => laneResult.distanceAlongLane;
        public float DistanceToLane => laneResult.distanceToLane;
    }

    public sealed class VehicleRoadRouteResult
    {
        public BakedLaneNetwork network;
        public List<string> laneIds = new List<string>();
        public float totalCost;
    }

    public sealed class VehicleRoadSubsystemSnapshot
    {
        public int registeredNetworkCount;
        public int laneCount;
        public int connectionCount;
        public int adjacentLinkCount;
        public int polygonCount;
        public int portalCount;
        public int closedLaneCount;
        public int congestionCostCount;
        public int signalCostCount;
        public int registeredVehicleCount;
        public int queuedVehicleCount;
        public int activeTokenCount;
        public int laneChangeReservationCount;
        public int signalPhaseCount;
        public int registeredRoadAgentCount;
        public int queriesThisFrame;
        public int routesThisFrame;
        public int replansThisFrame;
        public int failuresThisFrame;
        public int lastCandidateCount;
        public int peakCandidateCount;
        public int lastVisitedNodeCount;
        public int peakVisitedNodeCount;
        public int lastRouteSegmentCount;
        public int peakRouteSegmentCount;
        public int diagnosticHistoryCount;
        public int diagnosticHistoryCapacity;
        public int diagnosticDroppedCount;
        public string lastTrafficFailureReason = string.Empty;
        public List<string> duplicateLaneIds = new List<string>();
        public List<string> invalidRegistrationMessages = new List<string>();
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Vehicle Road/Vehicle Road Subsystem")]
    public sealed partial class VehicleRoadSubsystem : MonoBehaviour
    {
        [SerializeField] private List<BakedLaneNetwork> networks = new List<BakedLaneNetwork>();
        [SerializeField] private bool autoRegisterSceneRoadLaneNetworks = true;
        [SerializeField] private RoadNetworkRuntimeSettings runtimeSettings;
        [SerializeField, Min(0.1f)] private float defaultNearestLaneSearchDistance = 20f;
        [SerializeField, Min(0.1f)] private float defaultMaximumHeightDifference = 3f;

        private readonly List<BakedLaneNetwork> registeredNetworks = new List<BakedLaneNetwork>();
        private readonly Dictionary<string, BakedLaneNetwork> networkByLaneId =
            new Dictionary<string, BakedLaneNetwork>(StringComparer.Ordinal);
        private readonly Dictionary<BakedLaneNetwork, LaneGraph> graphByNetwork =
            new Dictionary<BakedLaneNetwork, LaneGraph>();
        private readonly MutableLaneTrafficCostProvider trafficCostProvider = new MutableLaneTrafficCostProvider();
        private readonly List<string> invalidRegistrationMessages = new List<string>();
        private readonly HashSet<string> duplicateLaneIds = new HashSet<string>(StringComparer.Ordinal);

        public IList<BakedLaneNetwork> Networks => networks;
        public bool AutoRegisterSceneRoadLaneNetworks
        {
            get => autoRegisterSceneRoadLaneNetworks;
            set => autoRegisterSceneRoadLaneNetworks = value;
        }

        public float DefaultNearestLaneSearchDistance
        {
            get => defaultNearestLaneSearchDistance;
            set => defaultNearestLaneSearchDistance = Mathf.Max(0.1f, value);
        }

        public float DefaultMaximumHeightDifference
        {
            get => defaultMaximumHeightDifference;
            set => defaultMaximumHeightDifference = Mathf.Max(0.1f, value);
        }

        public RoadNetworkRuntimeSettings RuntimeSettings
        {
            get => runtimeSettings;
            set
            {
                runtimeSettings = value;
                ConfigureDiagnostics();
            }
        }

        private void Awake()
        {
            ConfigureDiagnostics();
            RebuildIndexes();
        }

        private void OnEnable()
        {
            ConfigureDiagnostics();
            RebuildIndexes();
        }

        public bool RegisterNetwork(BakedLaneNetwork network)
        {
            if (network == null)
            {
                return false;
            }

            if (registeredNetworks.Contains(network))
            {
                return true;
            }

            if (!CanRegisterNetwork(network))
            {
                return false;
            }

            registeredNetworks.Add(network);
            graphByNetwork[network] = new LaneGraph(network, new[] { trafficCostProvider });
            IReadOnlyList<BakedLaneRecord> lanes = network.Lanes;
            for (int i = 0; i < lanes.Count; i++)
            {
                BakedLaneRecord lane = lanes[i];
                if (lane != null && !string.IsNullOrWhiteSpace(lane.laneId))
                {
                    networkByLaneId[lane.laneId] = network;
                }
            }

            RegisterTrafficNetwork(network);
            return true;
        }

        public bool UnregisterNetwork(BakedLaneNetwork network)
        {
            if (network == null || !registeredNetworks.Remove(network))
            {
                return false;
            }

            graphByNetwork.Remove(network);
            IReadOnlyList<BakedLaneRecord> lanes = network.Lanes;
            for (int i = 0; i < lanes.Count; i++)
            {
                BakedLaneRecord lane = lanes[i];
                if (lane != null &&
                    !string.IsNullOrWhiteSpace(lane.laneId) &&
                    networkByLaneId.TryGetValue(lane.laneId, out BakedLaneNetwork owner) &&
                    owner == network)
                {
                    networkByLaneId.Remove(lane.laneId);
                }
            }

            UnregisterTrafficNetwork(network);
            return true;
        }

        public void ClearNetworks()
        {
            ClearRegisteredState();
        }

        public void RebuildIndexes()
        {
            ConfigureDiagnostics();
            ClearRegisteredState();
            for (int i = 0; i < networks.Count; i++)
            {
                RegisterNetwork(networks[i]);
            }

            if (!autoRegisterSceneRoadLaneNetworks)
            {
                return;
            }

            RoadLaneNetwork[] sceneNetworks =
                UnityEngine.Object.FindObjectsByType<RoadLaneNetwork>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneNetworks.Length; i++)
            {
                BakedLaneNetwork bakedNetwork = sceneNetworks[i] == null ? null : sceneNetworks[i].BakedNetwork;
                if (bakedNetwork != null)
                {
                    RegisterNetwork(bakedNetwork);
                }
            }
        }

        public bool TryGetLane(string laneId, out BakedLaneRecord lane)
        {
            lane = null;
            return networkByLaneId.TryGetValue(laneId ?? string.Empty, out BakedLaneNetwork network) &&
                   network.TryGetLane(laneId, out lane) &&
                   !trafficCostProvider.IsLaneClosed(lane.laneId);
        }

        public bool TryEvaluate(string laneId, float distance, out RoadLanePose pose)
        {
            pose = default;
            return networkByLaneId.TryGetValue(laneId ?? string.Empty, out BakedLaneNetwork network) &&
                   network.TryEvaluate(laneId, distance, out pose);
        }

        public bool TryFindNearestLane(
            Vector3 position,
            Vector3 heading,
            RoadAgentMask agentMask,
            float maxDistance,
            float maxHeightDifference,
            out VehicleRoadNearestResult result,
            ISet<string> allowedLaneIds = null)
        {
            result = default;
            maxDistance = maxDistance > 0f ? maxDistance : defaultNearestLaneSearchDistance;
            maxHeightDifference = maxHeightDifference > 0f ? maxHeightDifference : defaultMaximumHeightDifference;
            float bestDistance = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < registeredNetworks.Count; i++)
            {
                BakedLaneNetwork network = registeredNetworks[i];
                ISet<string> filter = BuildAllowedLaneFilter(network, allowedLaneIds);
                if (!network.TryFindNearestLane(
                        position,
                        heading,
                        agentMask,
                        maxDistance,
                        maxHeightDifference,
                        out BakedLaneNearestResult candidate,
                        filter) ||
                    candidate.lane == null ||
                    trafficCostProvider.IsLaneClosed(candidate.lane.laneId) ||
                    candidate.distanceToLane >= bestDistance)
                {
                    continue;
                }

                bestDistance = candidate.distanceToLane;
                result = new VehicleRoadNearestResult(network, candidate);
                found = true;
            }

            return found;
        }

        public bool TryFindNearestLane(
            Vector3 position,
            Vector3 heading,
            RoadAgentMask agentMask,
            out VehicleRoadNearestResult result)
        {
            return TryFindNearestLane(
                position,
                heading,
                agentMask,
                defaultNearestLaneSearchDistance,
                defaultMaximumHeightDifference,
                out result);
        }

        public bool TryFindRoute(LaneRouteQuery query, out VehicleRoadRouteResult result)
        {
            AdvanceTraffic(0f);
            result = null;
            string startLaneId = query.startLaneId ?? string.Empty;
            string destinationLaneId = query.destinationLaneId ?? string.Empty;
            if (!networkByLaneId.TryGetValue(startLaneId, out BakedLaneNetwork startNetwork) ||
                !networkByLaneId.TryGetValue(destinationLaneId, out BakedLaneNetwork destinationNetwork) ||
                startNetwork != destinationNetwork ||
                !graphByNetwork.TryGetValue(startNetwork, out LaneGraph graph) ||
                !graph.TryFindRoute(query, out List<string> laneIds, out float totalCost))
            {
                return false;
            }

            result = new VehicleRoadRouteResult
            {
                network = startNetwork,
                laneIds = laneIds,
                totalCost = totalCost
            };
            return true;
        }

        public IReadOnlyList<BakedLaneConnectionRecord> GetOutgoingConnections(string laneId)
        {
            return networkByLaneId.TryGetValue(laneId ?? string.Empty, out BakedLaneNetwork network) &&
                   !trafficCostProvider.IsLaneClosed(laneId)
                ? network.GetOutgoingConnections(laneId)
                : Array.Empty<BakedLaneConnectionRecord>();
        }

        public IReadOnlyList<BakedLaneAdjacentLinkRecord> GetAdjacentLinks(string laneId)
        {
            return networkByLaneId.TryGetValue(laneId ?? string.Empty, out BakedLaneNetwork network) &&
                   !trafficCostProvider.IsLaneClosed(laneId)
                ? network.GetAdjacentLinks(laneId)
                : Array.Empty<BakedLaneAdjacentLinkRecord>();
        }

        public List<BakedLaneAdjacentLinkRecord> GetLaneChangeLinks(string laneId, RoadAgentMask agentMask)
        {
            List<BakedLaneAdjacentLinkRecord> result = new List<BakedLaneAdjacentLinkRecord>();
            if (!networkByLaneId.TryGetValue(laneId ?? string.Empty, out BakedLaneNetwork network) ||
                trafficCostProvider.IsLaneClosed(laneId))
            {
                return result;
            }

            List<BakedLaneAdjacentLinkRecord> links = network.GetLaneChangeLinks(laneId, agentMask);
            for (int i = 0; i < links.Count; i++)
            {
                BakedLaneAdjacentLinkRecord link = links[i];
                if (link != null && !trafficCostProvider.IsLaneClosed(link.toLaneId))
                {
                    result.Add(link);
                }
            }

            return result;
        }

        public void SetLaneClosed(string laneId, bool closed)
        {
            trafficCostProvider.SetLaneClosed(laneId, closed);
            HandleLaneClosureChanged(laneId, closed);
            RecordDiagnosticEvent(new RoadDiagnosticEvent
            {
                type = RoadDiagnosticEventType.LaneClosureChanged,
                frame = Time.frameCount,
                time = Time.time,
                primaryId = laneId ?? string.Empty,
                failureReason = closed ? RoadQueryFailureReason.FilterRejected : RoadQueryFailureReason.None
            });
        }

        public void SetLaneCongestionCost(string laneId, float cost)
        {
            trafficCostProvider.SetCongestionCost(laneId, cost);
        }

        public void SetConnectionSignalCost(string connectionId, float cost)
        {
            trafficCostProvider.SetSignalCost(connectionId, cost);
        }

        public void ClearDynamicTrafficState()
        {
            trafficCostProvider.Clear();
        }

        public VehicleRoadSubsystemSnapshot GetSnapshot()
        {
            VehicleRoadSubsystemSnapshot snapshot = new VehicleRoadSubsystemSnapshot
            {
                registeredNetworkCount = registeredNetworks.Count,
                closedLaneCount = trafficCostProvider.ClosedLaneCount,
                congestionCostCount = trafficCostProvider.CongestionCostCount,
                signalCostCount = trafficCostProvider.SignalCostCount,
                registeredVehicleCount = GetRegisteredVehicleCount(),
                queuedVehicleCount = GetQueuedVehicleCount(),
                activeTokenCount = GetActiveTrafficTokenCount(),
                laneChangeReservationCount = GetLaneChangeReservationCount(),
                signalPhaseCount = GetSignalPhaseCount(),
                registeredRoadAgentCount = GetRegisteredRoadAgentCount(),
                queriesThisFrame = GetQueriesThisFrame(),
                routesThisFrame = GetRoutesThisFrame(),
                replansThisFrame = GetReplansThisFrame(),
                failuresThisFrame = GetFailuresThisFrame(),
                lastCandidateCount = GetLastCandidateCount(),
                peakCandidateCount = GetPeakCandidateCount(),
                lastVisitedNodeCount = GetLastVisitedNodeCount(),
                peakVisitedNodeCount = GetPeakVisitedNodeCount(),
                lastRouteSegmentCount = GetLastRouteSegmentCount(),
                peakRouteSegmentCount = GetPeakRouteSegmentCount(),
                diagnosticHistoryCount = GetDiagnosticHistoryCount(),
                diagnosticHistoryCapacity = GetDiagnosticHistoryCapacity(),
                diagnosticDroppedCount = GetDiagnosticDroppedCount(),
                lastTrafficFailureReason = GetLastTrafficFailureReason(),
                duplicateLaneIds = new List<string>(duplicateLaneIds),
                invalidRegistrationMessages = new List<string>(invalidRegistrationMessages)
            };

            for (int i = 0; i < registeredNetworks.Count; i++)
            {
                BakedLaneNetwork network = registeredNetworks[i];
                snapshot.laneCount += network.Lanes.Count;
                snapshot.connectionCount += network.Connections.Count;
                snapshot.adjacentLinkCount += network.AdjacentLinks.Count;
                snapshot.polygonCount += network.Polygons.Count;
                snapshot.portalCount += network.Portals.Count;
            }

            return snapshot;
        }

        private bool CanRegisterNetwork(BakedLaneNetwork network)
        {
            bool valid = true;
            HashSet<string> localLaneIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<BakedLaneRecord> lanes = network.Lanes;
            for (int i = 0; i < lanes.Count; i++)
            {
                BakedLaneRecord lane = lanes[i];
                string laneId = lane == null ? string.Empty : lane.laneId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(laneId))
                {
                    invalidRegistrationMessages.Add("BakedLaneNetwork contains a lane with an empty laneId.");
                    valid = false;
                    continue;
                }

                if (!localLaneIds.Add(laneId) ||
                    networkByLaneId.TryGetValue(laneId, out BakedLaneNetwork existingNetwork) &&
                    existingNetwork != network)
                {
                    duplicateLaneIds.Add(laneId);
                    invalidRegistrationMessages.Add("Duplicate vehicle road laneId rejected: " + laneId);
                    valid = false;
                }
            }

            return valid;
        }

        private ISet<string> BuildAllowedLaneFilter(BakedLaneNetwork network, ISet<string> callerAllowedLaneIds)
        {
            if (trafficCostProvider.ClosedLaneCount == 0 && callerAllowedLaneIds == null)
            {
                return null;
            }

            HashSet<string> allowed = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<BakedLaneRecord> lanes = network.Lanes;
            for (int i = 0; i < lanes.Count; i++)
            {
                BakedLaneRecord lane = lanes[i];
                if (lane == null ||
                    string.IsNullOrWhiteSpace(lane.laneId) ||
                    trafficCostProvider.IsLaneClosed(lane.laneId) ||
                    callerAllowedLaneIds != null && !callerAllowedLaneIds.Contains(lane.laneId))
                {
                    continue;
                }

                allowed.Add(lane.laneId);
            }

            return allowed;
        }

        private void ClearRegisteredState()
        {
            registeredNetworks.Clear();
            networkByLaneId.Clear();
            graphByNetwork.Clear();
            invalidRegistrationMessages.Clear();
            duplicateLaneIds.Clear();
            ClearTrafficConfiguration();
        }

        private void OnValidate()
        {
            networks ??= new List<BakedLaneNetwork>();
            defaultNearestLaneSearchDistance = Mathf.Max(0.1f, defaultNearestLaneSearchDistance);
            defaultMaximumHeightDifference = Mathf.Max(0.1f, defaultMaximumHeightDifference);
            ValidateTrafficSettings();
        }
    }
}
