using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    public sealed partial class VehicleRoadSubsystem
    {
        private const float PassageGrantDistance = 0.25f;

        [Header("Traffic Runtime")]
        [SerializeField, Min(0.1f)] private float defaultVehicleLength = 4.5f;
        [SerializeField, Min(1f)] private float leadVehicleSearchDistance = 60f;
        [SerializeField, Min(0.1f)] private float laneChangeSafetyGap = 8f;
        [SerializeField, Min(0.1f)] private float laneChangeReservationDuration = 4f;
        [SerializeField, Min(0f)] private float redSignalRouteCost = 60f;
        [SerializeField, Min(0f)] private float yellowSignalRouteCost = 20f;
        [SerializeField, Min(0f)] private float queueRouteCostPerVehicle = 8f;
        [SerializeField, Min(0f)] private float staleVehicleTimeout = 5f;

        private readonly Dictionary<string, VehicleTrafficState> vehiclesById =
            new Dictionary<string, VehicleTrafficState>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<VehicleTrafficState>> vehiclesByLane =
            new Dictionary<string, List<VehicleTrafficState>>(StringComparer.Ordinal);
        private readonly Dictionary<string, BakedJunctionTrafficRecord> trafficJunctionsById =
            new Dictionary<string, BakedJunctionTrafficRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, BakedConnectorTrafficRecord> trafficConnectorsByLaneId =
            new Dictionary<string, BakedConnectorTrafficRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, BakedConnectorTrafficRecord> trafficConnectorsByConnectionId =
            new Dictionary<string, BakedConnectorTrafficRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, JunctionRuntimeState> junctionRuntimeById =
            new Dictionary<string, JunctionRuntimeState>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<JunctionQueueEntry>> queuesByConnectorLaneId =
            new Dictionary<string, List<JunctionQueueEntry>>(StringComparer.Ordinal);
        private readonly Dictionary<string, PassageToken> activePassagesByVehicleId =
            new Dictionary<string, PassageToken>(StringComparer.Ordinal);
        private readonly Dictionary<string, PassageToken> activePassagesByConnectorLaneId =
            new Dictionary<string, PassageToken>(StringComparer.Ordinal);
        private readonly Dictionary<string, LaneChangeReservation> laneChangeReservationsByVehicleId =
            new Dictionary<string, LaneChangeReservation>(StringComparer.Ordinal);

        private float trafficClock;
        private string lastTrafficFailureReason = string.Empty;

        private void Update()
        {
            AdvanceTraffic(Time.deltaTime);
        }

        public void UpdateVehicle(VehicleRoadVehicleUpdate update)
        {
            string vehicleId = update.vehicleId ?? string.Empty;
            string laneId = update.laneId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(vehicleId) || string.IsNullOrWhiteSpace(laneId))
            {
                SetTrafficFailure("Vehicle update requires a non-empty vehicleId and laneId.");
                return;
            }

            if (!TryGetLane(laneId, out BakedLaneRecord lane))
            {
                SetTrafficFailure("Vehicle update lane is not registered: " + laneId);
                return;
            }

            if (!vehiclesById.TryGetValue(vehicleId, out VehicleTrafficState state))
            {
                state = new VehicleTrafficState(vehicleId);
                vehiclesById.Add(vehicleId, state);
            }
            else if (!string.Equals(state.laneId, laneId, StringComparison.Ordinal))
            {
                RemoveVehicleFromLaneIndex(state);
            }

            state.laneId = laneId;
            state.agentMask = NormalizeAgentMask(update.agentMask);
            state.distanceAlongLane = Mathf.Clamp(update.distanceAlongLane, 0f, lane.length);
            state.speed = Mathf.Max(0f, update.speed);
            state.length = Mathf.Max(0.1f, update.length > 0f ? update.length : defaultVehicleLength);
            state.lastSeenTime = trafficClock;
            CopyRoute(update.routeLaneIds, state.routeLaneIds);
            AddVehicleToLaneIndex(state);
            ReleaseFinishedPassage(state);
            CompleteLaneChangeIfArrived(state);
        }

        public bool UnregisterVehicle(string vehicleId)
        {
            vehicleId ??= string.Empty;
            if (!vehiclesById.TryGetValue(vehicleId, out VehicleTrafficState state))
            {
                return false;
            }

            RemoveVehicleFromLaneIndex(state);
            vehiclesById.Remove(vehicleId);
            RemoveVehicleFromQueues(vehicleId);
            ReleasePassage(vehicleId);
            laneChangeReservationsByVehicleId.Remove(vehicleId);
            return true;
        }

        public bool TryGetLeadVehicle(
            string vehicleId,
            string laneId,
            float distanceAlongLane,
            IReadOnlyList<string> routeLaneIds,
            float searchDistance,
            out VehicleRoadLeadVehicleResult result)
        {
            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.LeadVehicleQuery);
            result = default;
            vehicleId ??= string.Empty;
            laneId ??= string.Empty;
            searchDistance = searchDistance > 0f ? searchDistance : leadVehicleSearchDistance;
            if (string.IsNullOrWhiteSpace(laneId))
            {
                return false;
            }

            bool found = false;
            float bestDistance = searchDistance;
            List<string> route = BuildRoute(laneId, routeLaneIds);
            float offset = 0f;
            for (int routeIndex = 0; routeIndex < route.Count; routeIndex++)
            {
                string candidateLaneId = route[routeIndex];
                if (!vehiclesByLane.TryGetValue(candidateLaneId, out List<VehicleTrafficState> laneVehicles))
                {
                    if (routeIndex == 0 && TryGetLane(candidateLaneId, out BakedLaneRecord skippedLane))
                    {
                        offset += Mathf.Max(0f, skippedLane.length - distanceAlongLane);
                    }
                    else if (TryGetLane(candidateLaneId, out skippedLane))
                    {
                        offset += skippedLane.length;
                    }

                    continue;
                }

                for (int i = 0; i < laneVehicles.Count; i++)
                {
                    VehicleTrafficState other = laneVehicles[i];
                    if (other == null || string.Equals(other.vehicleId, vehicleId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    float distance = routeIndex == 0
                        ? other.distanceAlongLane - distanceAlongLane
                        : offset + other.distanceAlongLane;
                    if (distance <= 0f || distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    result = new VehicleRoadLeadVehicleResult
                    {
                        vehicleId = other.vehicleId,
                        laneId = other.laneId,
                        distanceAlongRoute = distance,
                        speed = other.speed,
                        length = other.length
                    };
                    found = true;
                }

                if (routeIndex == 0 && TryGetLane(candidateLaneId, out BakedLaneRecord lane))
                {
                    offset += Mathf.Max(0f, lane.length - distanceAlongLane);
                }
                else if (TryGetLane(candidateLaneId, out lane))
                {
                    offset += lane.length;
                }
            }

            return found;
        }

        public VehicleRoadTrafficControlResult EvaluateTrafficControl(VehicleRoadTrafficQuery query)
        {
            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.JunctionControl);
            AdvanceTraffic(0f);
            VehicleRoadTrafficControlResult result = new VehicleRoadTrafficControlResult
            {
                passageStatus = VehicleRoadPassageStatus.NotRequired,
                signalState = VehicleRoadSignalState.None,
                queueIndex = -1,
                targetSpeedLimit = float.PositiveInfinity,
                distanceToStopLine = float.PositiveInfinity
            };

            string vehicleId = query.vehicleId ?? string.Empty;
            string laneId = query.laneId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(laneId) || !TryGetLane(laneId, out BakedLaneRecord lane))
            {
                result.failureReason = "Traffic query lane is not registered: " + laneId;
                SetTrafficFailure(result.failureReason);
                return result;
            }

            float vehicleLength = Mathf.Max(0.1f, query.vehicleLength > 0f ? query.vehicleLength : defaultVehicleLength);
            if (!string.IsNullOrWhiteSpace(vehicleId))
            {
                UpdateVehicle(new VehicleRoadVehicleUpdate
                {
                    vehicleId = vehicleId,
                    laneId = laneId,
                    agentMask = NormalizeAgentMask(query.agentMask),
                    distanceAlongLane = query.distanceAlongLane,
                    speed = query.speed,
                    length = vehicleLength,
                    routeLaneIds = query.routeLaneIds
                });
            }

            if (TryGetLeadVehicle(
                    vehicleId,
                    laneId,
                    query.distanceAlongLane,
                    query.routeLaneIds,
                    leadVehicleSearchDistance,
                    out VehicleRoadLeadVehicleResult lead))
            {
                float desiredGap = vehicleLength + Mathf.Max(0.5f, query.speed) * 1.2f + 2f;
                float distanceToLeadStop = lead.distanceAlongRoute - desiredGap;
                if (lead.distanceAlongRoute <= desiredGap)
                {
                    float ratio = Mathf.Clamp01(lead.distanceAlongRoute / Mathf.Max(0.1f, desiredGap));
                    float followSpeed = Mathf.Lerp(0f, Mathf.Max(0f, lead.speed), ratio);
                    ApplyStopConstraint(
                        ref result,
                        VehicleRoadStopReason.LeadVehicle,
                        VehicleRoadPassageStatus.NotRequired,
                        VehicleRoadSignalState.None,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        -1,
                        Mathf.Max(0f, distanceToLeadStop),
                        Mathf.Max(0f, followSpeed),
                        laneId,
                        query.distanceAlongLane + Mathf.Max(0f, distanceToLeadStop));
                    result.leadVehicle = lead;
                }
            }

            if (TryFindNextConnector(
                    laneId,
                    query.routeLaneIds,
                    out BakedConnectorTrafficRecord connectorTraffic,
                    out BakedLaneConnectionRecord connection) &&
                TryResolveConnectorApproachLane(
                    laneId,
                    connectorTraffic,
                    connection,
                    lane,
                    out BakedLaneRecord approachLane))
            {
                float stopLineDistance = float.IsFinite(connectorTraffic.stopLineDistance)
                    ? connectorTraffic.stopLineDistance
                    : 0f;
                bool isCurrentConnectorLane = string.Equals(
                    laneId,
                    connectorTraffic.connectorLaneId,
                    StringComparison.Ordinal);
                float vehicleFrontOffset = GetVehicleFrontOffset(vehicleLength);
                string stopLaneId = approachLane.laneId;
                float stopLineDistanceAlongLane = approachLane.length - stopLineDistance;
                float distanceToStopLine = stopLineDistanceAlongLane - (query.distanceAlongLane + vehicleFrontOffset);
                if (isCurrentConnectorLane)
                {
                    stopLaneId = laneId;
                    if (stopLineDistance < 0f)
                    {
                        stopLineDistanceAlongLane = -stopLineDistance;
                        distanceToStopLine = stopLineDistanceAlongLane - (query.distanceAlongLane + vehicleFrontOffset);
                    }
                    else
                    {
                        stopLineDistanceAlongLane = query.distanceAlongLane + vehicleFrontOffset;
                        distanceToStopLine = 0f;
                    }
                }

                if (distanceToStopLine <= GetApproachDetectionDistance(connectorTraffic))
                {
                    VehicleRoadTrafficControlResult passage = RequestConnectorPassage(
                        vehicleId,
                        connectorTraffic,
                        connection,
                        Mathf.Max(0f, distanceToStopLine));
                    if (passage.passageStatus != VehicleRoadPassageStatus.Granted &&
                        passage.passageStatus != VehicleRoadPassageStatus.NotRequired)
                    {
                        float queueStopDistanceAlongLane = GetQueueStopDistanceAlongLane(
                            connectorTraffic,
                            passage.queueIndex,
                            vehicleLength,
                            stopLineDistanceAlongLane);
                        ApplyStopConstraint(
                            ref result,
                            passage.stopReason,
                            passage.passageStatus,
                            passage.signalState,
                            passage.junctionId,
                            passage.connectorLaneId,
                            passage.connectionId,
                            passage.queueIndex,
                            Mathf.Max(0f, queueStopDistanceAlongLane - query.distanceAlongLane),
                            0f,
                            stopLaneId,
                            queueStopDistanceAlongLane);
                    }
                    else
                    {
                        result.passageStatus = passage.passageStatus;
                        result.signalState = passage.signalState;
                        result.junctionId = passage.junctionId;
                        result.connectorLaneId = passage.connectorLaneId;
                        result.connectionId = passage.connectionId;
                        result.queueIndex = passage.queueIndex;
                    }
                }
            }

            ApplyLaneChangeStatus(query, ref result);
            return result;
        }

        public VehicleRoadLaneChangeRequestResult RequestLaneChange(string vehicleId, RoadLaneAdjacentSide side)
        {
            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.LaneChangeQuery);
            AdvanceTraffic(0f);
            vehicleId ??= string.Empty;
            if (!vehiclesById.TryGetValue(vehicleId, out VehicleTrafficState vehicle))
            {
                return DenyLaneChange(vehicleId, side, "Vehicle is not registered.");
            }

            if (laneChangeReservationsByVehicleId.TryGetValue(vehicleId, out LaneChangeReservation existing) &&
                existing.expireTime >= trafficClock)
            {
                existing.status = VehicleRoadLaneChangeStatus.Active;
                return new VehicleRoadLaneChangeRequestResult
                {
                    status = existing.status,
                    fromLaneId = existing.fromLaneId,
                    targetLaneId = existing.targetLaneId,
                    side = existing.side,
                    reservedDistanceAlongLane = existing.reservedDistanceAlongLane
                };
            }

            List<BakedLaneAdjacentLinkRecord> links = GetLaneChangeLinks(vehicle.laneId, vehicle.agentMask);
            BakedLaneAdjacentLinkRecord selected = null;
            for (int i = 0; i < links.Count; i++)
            {
                if (links[i] != null && links[i].side == side)
                {
                    selected = links[i];
                    break;
                }
            }

            if (selected == null || !TryGetLane(selected.toLaneId, out BakedLaneRecord targetLane))
            {
                return DenyLaneChange(vehicleId, side, "No open adjacent lane is available on the requested side.");
            }

            float targetDistance = Mathf.Clamp(
                vehicle.distanceAlongLane,
                Mathf.Max(0f, selected.overlapStartDistance),
                Mathf.Max(0f, selected.overlapEndDistance));
            if (!HasSafeLaneChangeGap(vehicle.vehicleId, targetLane.laneId, targetDistance, vehicle.length, out string reason))
            {
                return DenyLaneChange(vehicleId, side, reason);
            }

            LaneChangeReservation reservation = new LaneChangeReservation
            {
                vehicleId = vehicle.vehicleId,
                fromLaneId = vehicle.laneId,
                targetLaneId = targetLane.laneId,
                side = side,
                reservedDistanceAlongLane = targetDistance,
                expireTime = trafficClock + laneChangeReservationDuration,
                status = VehicleRoadLaneChangeStatus.Granted
            };
            laneChangeReservationsByVehicleId[vehicle.vehicleId] = reservation;
            return new VehicleRoadLaneChangeRequestResult
            {
                status = reservation.status,
                fromLaneId = reservation.fromLaneId,
                targetLaneId = reservation.targetLaneId,
                side = reservation.side,
                reservedDistanceAlongLane = reservation.reservedDistanceAlongLane
            };
        }

        public bool CompleteLaneChange(string vehicleId)
        {
            vehicleId ??= string.Empty;
            return laneChangeReservationsByVehicleId.Remove(vehicleId);
        }

        public bool CancelLaneChange(string vehicleId)
        {
            vehicleId ??= string.Empty;
            if (!laneChangeReservationsByVehicleId.TryGetValue(vehicleId, out LaneChangeReservation reservation))
            {
                return false;
            }

            reservation.status = VehicleRoadLaneChangeStatus.Cancelled;
            laneChangeReservationsByVehicleId.Remove(vehicleId);
            return true;
        }

        public void AdvanceTraffic(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                trafficClock += deltaTime;
            }

            foreach (JunctionRuntimeState state in junctionRuntimeById.Values)
            {
                state.Advance(Mathf.Max(0f, deltaTime));
            }

            RemoveExpiredPassages();
            RemoveExpiredLaneChanges();
            RemoveStaleVehicles();
            ApplyDynamicTrafficCosts();
        }

        public bool TryGetJunctionSignalState(
            string junctionId,
            RoadLaneTurn turn,
            out VehicleRoadSignalState state)
        {
            state = VehicleRoadSignalState.None;
            junctionId ??= string.Empty;
            if (!trafficJunctionsById.TryGetValue(junctionId, out BakedJunctionTrafficRecord junction))
            {
                return false;
            }

            if (junction.controlMode != RoadJunctionTrafficControlMode.FixedSignal ||
                junction.signalPhases == null ||
                junction.signalPhases.Count == 0)
            {
                state = VehicleRoadSignalState.Green;
                return true;
            }

            if (!junctionRuntimeById.TryGetValue(junctionId, out JunctionRuntimeState runtime))
            {
                state = VehicleRoadSignalState.Red;
                return true;
            }

            state = GetSignalState(junction, runtime, turn);
            return true;
        }

        private void RegisterTrafficNetwork(BakedLaneNetwork network)
        {
            if (network == null)
            {
                return;
            }

            for (int i = 0; i < network.JunctionTraffic.Count; i++)
            {
                BakedJunctionTrafficRecord junction = network.JunctionTraffic[i];
                if (junction == null || string.IsNullOrWhiteSpace(junction.junctionId))
                {
                    continue;
                }

                trafficJunctionsById[junction.junctionId] = junction;
                junctionRuntimeById[junction.junctionId] = new JunctionRuntimeState(junction);
            }

            for (int i = 0; i < network.ConnectorTraffic.Count; i++)
            {
                BakedConnectorTrafficRecord connector = network.ConnectorTraffic[i];
                if (connector == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(connector.connectorLaneId))
                {
                    trafficConnectorsByLaneId[connector.connectorLaneId] = connector;
                }

                if (!string.IsNullOrWhiteSpace(connector.connectionId))
                {
                    trafficConnectorsByConnectionId[connector.connectionId] = connector;
                }
            }
        }

        private void UnregisterTrafficNetwork(BakedLaneNetwork network)
        {
            if (network == null)
            {
                return;
            }

            for (int i = 0; i < network.JunctionTraffic.Count; i++)
            {
                BakedJunctionTrafficRecord junction = network.JunctionTraffic[i];
                if (junction == null)
                {
                    continue;
                }

                trafficJunctionsById.Remove(junction.junctionId);
                junctionRuntimeById.Remove(junction.junctionId);
            }

            for (int i = 0; i < network.ConnectorTraffic.Count; i++)
            {
                BakedConnectorTrafficRecord connector = network.ConnectorTraffic[i];
                if (connector == null)
                {
                    continue;
                }

                trafficConnectorsByLaneId.Remove(connector.connectorLaneId);
                trafficConnectorsByConnectionId.Remove(connector.connectionId);
            }
        }

        private void ClearTrafficConfiguration()
        {
            trafficJunctionsById.Clear();
            trafficConnectorsByLaneId.Clear();
            trafficConnectorsByConnectionId.Clear();
            junctionRuntimeById.Clear();
            queuesByConnectorLaneId.Clear();
            activePassagesByVehicleId.Clear();
            activePassagesByConnectorLaneId.Clear();
            laneChangeReservationsByVehicleId.Clear();
            vehiclesById.Clear();
            vehiclesByLane.Clear();
            lastTrafficFailureReason = string.Empty;
        }

        private void HandleLaneClosureChanged(string laneId, bool closed)
        {
            if (!closed)
            {
                return;
            }

            laneId ??= string.Empty;
            List<string> queuedVehicles = new List<string>();
            foreach (KeyValuePair<string, List<JunctionQueueEntry>> pair in queuesByConnectorLaneId)
            {
                if (!trafficConnectorsByLaneId.TryGetValue(pair.Key, out BakedConnectorTrafficRecord connector) ||
                    string.Equals(connector.connectorLaneId, laneId, StringComparison.Ordinal) ||
                    string.Equals(connector.fromLaneId, laneId, StringComparison.Ordinal) ||
                    string.Equals(connector.toLaneId, laneId, StringComparison.Ordinal))
                {
                    for (int i = 0; i < pair.Value.Count; i++)
                    {
                        queuedVehicles.Add(pair.Value[i].vehicleId);
                    }
                }
            }

            for (int i = 0; i < queuedVehicles.Count; i++)
            {
                RemoveVehicleFromQueues(queuedVehicles[i]);
            }
        }

        private VehicleRoadTrafficControlResult RequestConnectorPassage(
            string vehicleId,
            BakedConnectorTrafficRecord connector,
            BakedLaneConnectionRecord connection,
            float distanceToStopLine)
        {
            VehicleRoadTrafficControlResult result = new VehicleRoadTrafficControlResult
            {
                hasConstraint = true,
                stopReason = VehicleRoadStopReason.Queue,
                passageStatus = VehicleRoadPassageStatus.Waiting,
                signalState = VehicleRoadSignalState.Green,
                junctionId = connector.junctionId,
                connectorLaneId = connector.connectorLaneId,
                connectionId = connection == null ? connector.connectionId : connection.connectionId,
                queueIndex = -1,
                distanceToStopLine = distanceToStopLine,
                targetSpeedLimit = 0f
            };

            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                result.passageStatus = VehicleRoadPassageStatus.Blocked;
                result.stopReason = VehicleRoadStopReason.Queue;
                result.failureReason = "Traffic passage requires a stable vehicleId.";
                SetTrafficFailure(result.failureReason);
                return result;
            }

            if (!trafficJunctionsById.TryGetValue(connector.junctionId, out BakedJunctionTrafficRecord junction))
            {
                result.passageStatus = VehicleRoadPassageStatus.NotRequired;
                result.targetSpeedLimit = float.PositiveInfinity;
                return result;
            }

            result.signalState = GetSignalState(junction, connector);
            if (activePassagesByVehicleId.TryGetValue(vehicleId, out PassageToken existing) &&
                string.Equals(existing.connectorLaneId, connector.connectorLaneId, StringComparison.Ordinal))
            {
                if (existing.enteredConnector || result.signalState == VehicleRoadSignalState.Green)
                {
                    result.passageStatus = VehicleRoadPassageStatus.Granted;
                    result.targetSpeedLimit = float.PositiveInfinity;
                    return result;
                }

                ReleasePassage(vehicleId);
            }

            if (result.signalState == VehicleRoadSignalState.Green &&
                distanceToStopLine > PassageGrantDistance)
            {
                result.hasConstraint = false;
                result.stopReason = VehicleRoadStopReason.None;
                result.passageStatus = VehicleRoadPassageStatus.NotRequired;
                result.targetSpeedLimit = float.PositiveInfinity;
                return result;
            }

            JunctionQueueEntry entry = EnsureQueueEntry(vehicleId, connector);
            result.queueIndex = GetQueueIndex(connector, vehicleId);
            if (result.signalState == VehicleRoadSignalState.Red || result.signalState == VehicleRoadSignalState.Yellow)
            {
                result.stopReason = VehicleRoadStopReason.TrafficSignal;
                return result;
            }

            if (!IsQueueHead(connector, vehicleId))
            {
                result.stopReason = VehicleRoadStopReason.Queue;
                return result;
            }

            if (HasConflictingActivePassage(connector, vehicleId))
            {
                result.stopReason = VehicleRoadStopReason.JunctionConflict;
                return result;
            }

            if (junction.controlMode == RoadJunctionTrafficControlMode.PriorityYield &&
                !IsPriorityWinner(junction, connector, entry))
            {
                result.stopReason = VehicleRoadStopReason.Queue;
                return result;
            }

            GrantPassage(vehicleId, connector, junction);
            RemoveVehicleFromQueue(connector, vehicleId);
            result.passageStatus = VehicleRoadPassageStatus.Granted;
            result.stopReason = VehicleRoadStopReason.None;
            result.queueIndex = -1;
            result.targetSpeedLimit = float.PositiveInfinity;
            return result;
        }

        private void GrantPassage(
            string vehicleId,
            BakedConnectorTrafficRecord connector,
            BakedJunctionTrafficRecord junction)
        {
            ReleasePassage(vehicleId);
            PassageToken token = new PassageToken
            {
                tokenId = vehicleId + "|" + connector.connectorLaneId + "|" + trafficClock.ToString("0.###"),
                vehicleId = vehicleId,
                junctionId = connector.junctionId,
                connectorLaneId = connector.connectorLaneId,
                targetLaneId = connector.toLaneId,
                expireTime = trafficClock + Mathf.Max(0.1f, junction.passageTokenDuration)
            };
            activePassagesByVehicleId[vehicleId] = token;
            activePassagesByConnectorLaneId[connector.connectorLaneId] = token;
        }

        private void ReleaseFinishedPassage(VehicleTrafficState vehicle)
        {
            if (vehicle == null ||
                !activePassagesByVehicleId.TryGetValue(vehicle.vehicleId, out PassageToken token))
            {
                return;
            }

            if (string.Equals(vehicle.laneId, token.connectorLaneId, StringComparison.Ordinal))
            {
                token.enteredConnector = true;
                return;
            }

            if (!token.enteredConnector)
            {
                return;
            }

            float releaseDistance = 0f;
            if (trafficJunctionsById.TryGetValue(token.junctionId, out BakedJunctionTrafficRecord junction))
            {
                releaseDistance = Mathf.Max(0f, junction.releaseDistance);
            }

            if (!string.Equals(vehicle.laneId, token.targetLaneId, StringComparison.Ordinal) ||
                vehicle.distanceAlongLane >= releaseDistance)
            {
                ReleasePassage(vehicle.vehicleId);
            }
        }

        private void ReleasePassage(string vehicleId)
        {
            vehicleId ??= string.Empty;
            if (!activePassagesByVehicleId.TryGetValue(vehicleId, out PassageToken token))
            {
                return;
            }

            activePassagesByVehicleId.Remove(vehicleId);
            if (activePassagesByConnectorLaneId.TryGetValue(token.connectorLaneId, out PassageToken existing) &&
                string.Equals(existing.vehicleId, vehicleId, StringComparison.Ordinal))
            {
                activePassagesByConnectorLaneId.Remove(token.connectorLaneId);
            }
        }

        private bool TryFindNextConnector(
            string laneId,
            IReadOnlyList<string> routeLaneIds,
            out BakedConnectorTrafficRecord connector,
            out BakedLaneConnectionRecord connection)
        {
            connector = null;
            connection = null;
            List<string> route = BuildRoute(laneId, routeLaneIds);
            if (trafficConnectorsByLaneId.TryGetValue(laneId, out connector))
            {
                connection = FindConnectorIncomingConnection(connector);
                return true;
            }

            int routeIndex = route.FindIndex(id => string.Equals(id, laneId, StringComparison.Ordinal));
            if (routeIndex >= 0 && routeIndex + 1 < route.Count)
            {
                string nextLaneId = route[routeIndex + 1];
                if (trafficConnectorsByLaneId.TryGetValue(nextLaneId, out connector))
                {
                    IReadOnlyList<BakedLaneConnectionRecord> outgoing = GetOutgoingConnections(laneId);
                    for (int i = 0; i < outgoing.Count; i++)
                    {
                        if (outgoing[i] != null &&
                            string.Equals(outgoing[i].toLaneId, nextLaneId, StringComparison.Ordinal))
                        {
                            connection = outgoing[i];
                            break;
                        }
                    }

                    return true;
                }
            }

            IReadOnlyList<BakedLaneConnectionRecord> connections = GetOutgoingConnections(laneId);
            for (int i = 0; i < connections.Count; i++)
            {
                BakedLaneConnectionRecord candidate = connections[i];
                if (candidate == null)
                {
                    continue;
                }

                if (trafficConnectorsByConnectionId.TryGetValue(candidate.connectionId, out connector) ||
                    trafficConnectorsByLaneId.TryGetValue(candidate.toLaneId, out connector))
                {
                    connection = candidate;
                    return true;
                }
            }

            return false;
        }

        private BakedLaneConnectionRecord FindConnectorIncomingConnection(BakedConnectorTrafficRecord connector)
        {
            if (connector == null)
            {
                return null;
            }

            string fromLaneId = connector.fromLaneId ?? string.Empty;
            string connectorLaneId = connector.connectorLaneId ?? string.Empty;
            IReadOnlyList<BakedLaneConnectionRecord> outgoing = GetOutgoingConnections(fromLaneId);
            for (int i = 0; i < outgoing.Count; i++)
            {
                BakedLaneConnectionRecord candidate = outgoing[i];
                if (candidate == null)
                {
                    continue;
                }

                if ((!string.IsNullOrWhiteSpace(connector.connectionId) &&
                     string.Equals(candidate.connectionId, connector.connectionId, StringComparison.Ordinal)) ||
                    string.Equals(candidate.toLaneId, connectorLaneId, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private bool TryResolveConnectorApproachLane(
            string laneId,
            BakedConnectorTrafficRecord connector,
            BakedLaneConnectionRecord connection,
            BakedLaneRecord currentLane,
            out BakedLaneRecord approachLane)
        {
            approachLane = currentLane;
            string approachLaneId = GetConnectorApproachLaneId(connector, connection);
            if (string.IsNullOrWhiteSpace(approachLaneId))
            {
                return true;
            }

            if (string.Equals(approachLaneId, laneId, StringComparison.Ordinal) ||
                string.Equals(connector.connectorLaneId, laneId, StringComparison.Ordinal))
            {
                return TryGetLane(approachLaneId, out approachLane);
            }

            return false;
        }

        private static string GetConnectorApproachLaneId(
            BakedConnectorTrafficRecord connector,
            BakedLaneConnectionRecord connection)
        {
            if (!string.IsNullOrWhiteSpace(connector?.fromLaneId))
            {
                return connector.fromLaneId;
            }

            return connection == null ? string.Empty : connection.fromLaneId ?? string.Empty;
        }

        private VehicleRoadSignalState GetSignalState(
            BakedJunctionTrafficRecord junction,
            BakedConnectorTrafficRecord connector)
        {
            if (junction == null ||
                junction.controlMode != RoadJunctionTrafficControlMode.FixedSignal ||
                junction.signalPhases == null ||
                junction.signalPhases.Count == 0)
            {
                return VehicleRoadSignalState.Green;
            }

            if (!junctionRuntimeById.TryGetValue(junction.junctionId, out JunctionRuntimeState runtime))
            {
                return VehicleRoadSignalState.Red;
            }

            return GetSignalState(junction, runtime, connector.turnType);
        }

        private static VehicleRoadSignalState GetSignalState(
            BakedJunctionTrafficRecord junction,
            JunctionRuntimeState runtime,
            RoadLaneTurn turn)
        {
            BakedJunctionSignalPhaseRecord phase = runtime.CurrentPhase;
            if (phase == null || !AllowsTurn(phase.allowedTurns, turn))
            {
                return VehicleRoadSignalState.Red;
            }

            if (runtime.CurrentPhaseTime < Mathf.Max(0.1f, phase.greenDuration))
            {
                return VehicleRoadSignalState.Green;
            }

            if (runtime.CurrentPhaseTime < Mathf.Max(0.1f, phase.greenDuration) + Mathf.Max(0f, phase.yellowDuration))
            {
                return VehicleRoadSignalState.Yellow;
            }

            return VehicleRoadSignalState.Red;
        }

        private bool HasConflictingActivePassage(BakedConnectorTrafficRecord connector, string requestingVehicleId)
        {
            foreach (PassageToken token in activePassagesByConnectorLaneId.Values)
            {
                if (token == null ||
                    string.Equals(token.vehicleId, requestingVehicleId, StringComparison.Ordinal) ||
                    !string.Equals(token.junctionId, connector.junctionId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(token.connectorLaneId, connector.connectorLaneId, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!trafficConnectorsByLaneId.TryGetValue(
                        token.connectorLaneId,
                        out BakedConnectorTrafficRecord activeConnector))
                {
                    if (connector.TryGetConflict(
                            token.connectorLaneId,
                            out BakedConnectorConflictRecord requestConflict))
                    {
                        if (IsActivePassageInsideConflictInterval(
                                token,
                                null,
                                requestConflict.otherStartDistance,
                                requestConflict.otherEndDistance))
                        {
                            return true;
                        }

                        continue;
                    }

                    if (!connector.HasStructuredConflicts && connector.ConflictsWith(token.connectorLaneId))
                    {
                        return true;
                    }

                    continue;
                }

                if (TryGetStructuredConnectorConflict(
                        connector,
                        activeConnector,
                        out float activeStartDistance,
                        out float activeEndDistance))
                {
                    if (IsActivePassageInsideConflictInterval(
                            token,
                            activeConnector,
                            activeStartDistance,
                            activeEndDistance))
                    {
                        return true;
                    }

                    continue;
                }

                if (connector.HasStructuredConflicts || activeConnector.HasStructuredConflicts)
                {
                    continue;
                }

                if (connector.ConflictsWith(token.connectorLaneId) ||
                    activeConnector.ConflictsWith(connector.connectorLaneId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetStructuredConnectorConflict(
            BakedConnectorTrafficRecord requestingConnector,
            BakedConnectorTrafficRecord activeConnector,
            out float activeStartDistance,
            out float activeEndDistance)
        {
            activeStartDistance = 0f;
            activeEndDistance = 0f;
            if (requestingConnector == null || activeConnector == null)
            {
                return false;
            }

            if (requestingConnector.TryGetConflict(
                    activeConnector.connectorLaneId,
                    out BakedConnectorConflictRecord requestConflict))
            {
                activeStartDistance = requestConflict.otherStartDistance;
                activeEndDistance = requestConflict.otherEndDistance;
                return true;
            }

            if (activeConnector.TryGetConflict(
                    requestingConnector.connectorLaneId,
                    out BakedConnectorConflictRecord activeConflict))
            {
                activeStartDistance = activeConflict.selfStartDistance;
                activeEndDistance = activeConflict.selfEndDistance;
                return true;
            }

            return false;
        }

        private bool IsActivePassageInsideConflictInterval(
            PassageToken token,
            BakedConnectorTrafficRecord activeConnector,
            float activeStartDistance,
            float activeEndDistance)
        {
            if (token == null)
            {
                return false;
            }

            if (!vehiclesById.TryGetValue(token.vehicleId, out VehicleTrafficState vehicle))
            {
                return true;
            }

            if (string.Equals(vehicle.laneId, token.connectorLaneId, StringComparison.Ordinal))
            {
                float min = Mathf.Min(activeStartDistance, activeEndDistance);
                float max = Mathf.Max(activeStartDistance, activeEndDistance);
                return vehicle.distanceAlongLane + 0.001f >= min &&
                       vehicle.distanceAlongLane - 0.001f <= max;
            }

            if (!token.enteredConnector ||
                activeConnector != null &&
                string.Equals(vehicle.laneId, activeConnector.fromLaneId, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private bool IsPriorityWinner(
            BakedJunctionTrafficRecord junction,
            BakedConnectorTrafficRecord connector,
            JunctionQueueEntry entry)
        {
            float bestScore = float.NegativeInfinity;
            string bestConnectorLaneId = string.Empty;
            foreach (KeyValuePair<string, List<JunctionQueueEntry>> pair in queuesByConnectorLaneId)
            {
                JunctionQueueEntry candidateEntry = pair.Value.Count == 0 ? null : pair.Value[0];
                if (candidateEntry == null ||
                    !trafficConnectorsByLaneId.TryGetValue(candidateEntry.connectorLaneId, out BakedConnectorTrafficRecord candidate) ||
                    !string.Equals(candidate.junctionId, junction.junctionId, StringComparison.Ordinal) ||
                    GetSignalState(junction, candidate) != VehicleRoadSignalState.Green ||
                    HasConflictingActivePassage(candidate, candidateEntry.vehicleId))
                {
                    continue;
                }

                float wait = Mathf.Max(0f, trafficClock - candidateEntry.requestTime);
                float score = GetTurnPriority(junction, candidate.turnType) + wait * 0.25f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestConnectorLaneId = candidate.connectorLaneId;
                }
            }

            return string.Equals(bestConnectorLaneId, connector.connectorLaneId, StringComparison.Ordinal) &&
                   entry != null;
        }

        private float GetTurnPriority(BakedJunctionTrafficRecord junction, RoadLaneTurn turn)
        {
            return turn switch
            {
                RoadLaneTurn.Straight => junction.straightPriority,
                RoadLaneTurn.Right => junction.rightPriority,
                RoadLaneTurn.Left => junction.leftPriority,
                RoadLaneTurn.UTurn => junction.uTurnPriority,
                _ => 0f
            };
        }

        private JunctionQueueEntry EnsureQueueEntry(string vehicleId, BakedConnectorTrafficRecord connector)
        {
            string queueKey = GetQueueKey(connector);
            RemoveVehicleFromQueuesExcept(vehicleId, queueKey);
            if (!queuesByConnectorLaneId.TryGetValue(queueKey, out List<JunctionQueueEntry> queue))
            {
                queue = new List<JunctionQueueEntry>();
                queuesByConnectorLaneId.Add(queueKey, queue);
            }

            for (int i = 0; i < queue.Count; i++)
            {
                if (string.Equals(queue[i].vehicleId, vehicleId, StringComparison.Ordinal))
                {
                    queue[i].connectorLaneId = connector == null ? string.Empty : connector.connectorLaneId;
                    return queue[i];
                }
            }

            JunctionQueueEntry entry = new JunctionQueueEntry
            {
                vehicleId = vehicleId,
                connectorLaneId = connector == null ? string.Empty : connector.connectorLaneId,
                requestTime = trafficClock
            };
            queue.Add(entry);
            return entry;
        }

        private int GetQueueIndex(BakedConnectorTrafficRecord connector, string vehicleId)
        {
            if (!queuesByConnectorLaneId.TryGetValue(GetQueueKey(connector), out List<JunctionQueueEntry> queue))
            {
                return -1;
            }

            for (int i = 0; i < queue.Count; i++)
            {
                if (string.Equals(queue[i].vehicleId, vehicleId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsQueueHead(BakedConnectorTrafficRecord connector, string vehicleId)
        {
            return queuesByConnectorLaneId.TryGetValue(GetQueueKey(connector), out List<JunctionQueueEntry> queue) &&
                   queue.Count > 0 &&
                   string.Equals(queue[0].vehicleId, vehicleId, StringComparison.Ordinal);
        }

        private void RemoveVehicleFromQueues(string vehicleId)
        {
            RemoveVehicleFromQueuesExcept(vehicleId, string.Empty);
        }

        private void RemoveVehicleFromQueuesExcept(string vehicleId, string allowedQueueKey)
        {
            foreach (KeyValuePair<string, List<JunctionQueueEntry>> pair in queuesByConnectorLaneId)
            {
                List<JunctionQueueEntry> queue = pair.Value;
                for (int i = queue.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(queue[i].vehicleId, vehicleId, StringComparison.Ordinal) &&
                        !string.Equals(pair.Key, allowedQueueKey, StringComparison.Ordinal))
                    {
                        queue.RemoveAt(i);
                    }
                }
            }
        }

        private void RemoveVehicleFromQueue(BakedConnectorTrafficRecord connector, string vehicleId)
        {
            if (!queuesByConnectorLaneId.TryGetValue(GetQueueKey(connector), out List<JunctionQueueEntry> queue))
            {
                return;
            }

            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if (string.Equals(queue[i].vehicleId, vehicleId, StringComparison.Ordinal))
                {
                    queue.RemoveAt(i);
                }
            }
        }

        private void ApplyDynamicTrafficCosts()
        {
            foreach (BakedConnectorTrafficRecord connector in trafficConnectorsByLaneId.Values)
            {
                if (connector == null || string.IsNullOrWhiteSpace(connector.connectionId))
                {
                    continue;
                }

                float cost = GetQueueCount(connector) * queueRouteCostPerVehicle;
                if (trafficJunctionsById.TryGetValue(connector.junctionId, out BakedJunctionTrafficRecord junction))
                {
                    VehicleRoadSignalState signal = GetSignalState(junction, connector);
                    if (signal == VehicleRoadSignalState.Red)
                    {
                        cost += redSignalRouteCost;
                    }
                    else if (signal == VehicleRoadSignalState.Yellow)
                    {
                        cost += yellowSignalRouteCost;
                    }
                }

                trafficCostProvider.SetSignalCost(connector.connectionId, cost);
            }
        }

        private int GetQueueCount(BakedConnectorTrafficRecord connector)
        {
            return queuesByConnectorLaneId.TryGetValue(GetQueueKey(connector), out List<JunctionQueueEntry> queue)
                ? queue.Count
                : 0;
        }

        private bool HasSafeLaneChangeGap(
            string vehicleId,
            string targetLaneId,
            float targetDistance,
            float vehicleLength,
            out string reason)
        {
            reason = string.Empty;
            float requiredGap = laneChangeSafetyGap + Mathf.Max(defaultVehicleLength, vehicleLength);
            if (vehiclesByLane.TryGetValue(targetLaneId, out List<VehicleTrafficState> laneVehicles))
            {
                for (int i = 0; i < laneVehicles.Count; i++)
                {
                    VehicleTrafficState other = laneVehicles[i];
                    if (other == null || string.Equals(other.vehicleId, vehicleId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (Mathf.Abs(other.distanceAlongLane - targetDistance) < requiredGap)
                    {
                        reason = "Target lane gap is occupied by vehicle " + other.vehicleId + ".";
                        return false;
                    }
                }
            }

            foreach (LaneChangeReservation reservation in laneChangeReservationsByVehicleId.Values)
            {
                if (reservation == null ||
                    string.Equals(reservation.vehicleId, vehicleId, StringComparison.Ordinal) ||
                    !string.Equals(reservation.targetLaneId, targetLaneId, StringComparison.Ordinal) ||
                    reservation.expireTime < trafficClock)
                {
                    continue;
                }

                if (Mathf.Abs(reservation.reservedDistanceAlongLane - targetDistance) < requiredGap)
                {
                    reason = "Target lane gap is already reserved.";
                    return false;
                }
            }

            return true;
        }

        private void ApplyLaneChangeStatus(
            VehicleRoadTrafficQuery query,
            ref VehicleRoadTrafficControlResult result)
        {
            string vehicleId = query.vehicleId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(vehicleId) ||
                !laneChangeReservationsByVehicleId.TryGetValue(vehicleId, out LaneChangeReservation reservation) ||
                reservation.expireTime < trafficClock)
            {
                return;
            }

            result.laneChangeStatus = reservation.status;
            result.laneChangeTargetLaneId = reservation.targetLaneId;
            if (TryEvaluate(
                    reservation.targetLaneId,
                    reservation.reservedDistanceAlongLane + 3f,
                    out RoadLanePose pose))
            {
                result.hasLaneChangeTargetPoint = true;
                result.laneChangeTargetPoint = pose.position;
            }
        }

        private void CompleteLaneChangeIfArrived(VehicleTrafficState vehicle)
        {
            if (vehicle == null ||
                !laneChangeReservationsByVehicleId.TryGetValue(vehicle.vehicleId, out LaneChangeReservation reservation))
            {
                return;
            }

            if (string.Equals(vehicle.laneId, reservation.targetLaneId, StringComparison.Ordinal))
            {
                laneChangeReservationsByVehicleId.Remove(vehicle.vehicleId);
            }
        }

        private VehicleRoadLaneChangeRequestResult DenyLaneChange(
            string vehicleId,
            RoadLaneAdjacentSide side,
            string reason)
        {
            SetTrafficFailure(reason);
            return new VehicleRoadLaneChangeRequestResult
            {
                status = VehicleRoadLaneChangeStatus.Denied,
                side = side,
                failureReason = reason
            };
        }

        private void ApplyStopConstraint(
            ref VehicleRoadTrafficControlResult result,
            VehicleRoadStopReason reason,
            VehicleRoadPassageStatus passageStatus,
            VehicleRoadSignalState signalState,
            string junctionId,
            string connectorLaneId,
            string connectionId,
            int queueIndex,
            float distanceToStop,
            float targetSpeed,
            string laneId,
            float stopDistanceAlongLane)
        {
            if (distanceToStop > result.distanceToStopLine)
            {
                result.targetSpeedLimit = Mathf.Min(result.targetSpeedLimit, targetSpeed);
                return;
            }

            result.hasConstraint = true;
            result.stopReason = reason;
            result.passageStatus = passageStatus;
            result.signalState = signalState;
            result.junctionId = junctionId ?? string.Empty;
            result.connectorLaneId = connectorLaneId ?? string.Empty;
            result.connectionId = connectionId ?? string.Empty;
            result.queueIndex = queueIndex;
            result.distanceToStopLine = distanceToStop;
            result.targetSpeedLimit = Mathf.Max(0f, targetSpeed);
            if (TryEvaluateLanePoseWithExtrapolation(laneId, stopDistanceAlongLane, out RoadLanePose stopPose))
            {
                result.hasStopPosition = true;
                result.stopPosition = stopPose.position;
            }
        }

        private float GetQueueStopDistanceAlongLane(
            BakedConnectorTrafficRecord connector,
            int queueIndex,
            float vehicleLength,
            float stopLineDistanceAlongLane)
        {
            float offsetBehindStopLine = GetVehicleFrontOffset(vehicleLength);
            if (queueIndex > 0)
            {
                float queueSpacing = GetQueueSpacing(connector);
                if (queuesByConnectorLaneId.TryGetValue(GetQueueKey(connector), out List<JunctionQueueEntry> queue))
                {
                    int count = Mathf.Min(queueIndex, queue.Count);
                    for (int i = 0; i < count; i++)
                    {
                        offsetBehindStopLine += GetQueuedVehicleLength(queue[i]) + queueSpacing;
                    }
                }
                else
                {
                    offsetBehindStopLine += queueIndex * (Mathf.Max(defaultVehicleLength, vehicleLength) + queueSpacing);
                }
            }

            return stopLineDistanceAlongLane - offsetBehindStopLine;
        }

        private float GetQueuedVehicleLength(JunctionQueueEntry entry)
        {
            if (entry != null &&
                vehiclesById.TryGetValue(entry.vehicleId, out VehicleTrafficState vehicle) &&
                vehicle != null)
            {
                return Mathf.Max(0.1f, vehicle.length);
            }

            return Mathf.Max(0.1f, defaultVehicleLength);
        }

        private float GetQueueSpacing(BakedConnectorTrafficRecord connector)
        {
            if (connector != null &&
                trafficJunctionsById.TryGetValue(connector.junctionId, out BakedJunctionTrafficRecord junction))
            {
                return Mathf.Max(0.5f, junction.queueSpacing);
            }

            return 0.5f;
        }

        private static string GetQueueKey(BakedConnectorTrafficRecord connector)
        {
            if (connector == null)
            {
                return string.Empty;
            }

            string laneId = string.IsNullOrWhiteSpace(connector.fromLaneId)
                ? connector.connectorLaneId
                : connector.fromLaneId;
            return (connector.junctionId ?? string.Empty) + "|" + (laneId ?? string.Empty);
        }

        private static float GetVehicleFrontOffset(float vehicleLength)
        {
            return Mathf.Max(0.1f, vehicleLength) * 0.5f;
        }

        private bool TryEvaluateLanePoseWithExtrapolation(
            string laneId,
            float distanceAlongLane,
            out RoadLanePose pose)
        {
            pose = default;
            if (!TryGetLane(laneId, out BakedLaneRecord lane))
            {
                return false;
            }

            float clampedDistance = Mathf.Clamp(distanceAlongLane, 0f, lane.length);
            if (!TryEvaluate(laneId, clampedDistance, out pose))
            {
                return false;
            }

            float extrapolatedDistance = distanceAlongLane - clampedDistance;
            if (Mathf.Abs(extrapolatedDistance) > 0.0001f)
            {
                Vector3 forward = pose.forward.sqrMagnitude <= 0.0001f
                    ? Vector3.forward
                    : pose.forward.normalized;
                Vector3 offset = forward * extrapolatedDistance;
                pose.position += offset;
                pose.splinePosition += offset;
                pose.distance = distanceAlongLane;
                pose.normalizedT = lane.length <= 0.0001f ? 0f : distanceAlongLane / lane.length;
            }

            return true;
        }

        private float GetApproachDetectionDistance(BakedConnectorTrafficRecord connector)
        {
            return connector != null &&
                   trafficJunctionsById.TryGetValue(connector.junctionId, out BakedJunctionTrafficRecord junction)
                ? Mathf.Max(1f, junction.approachDetectionDistance)
                : 0f;
        }

        private List<string> BuildRoute(string laneId, IReadOnlyList<string> routeLaneIds)
        {
            List<string> route = new List<string>();
            if (routeLaneIds != null)
            {
                for (int i = 0; i < routeLaneIds.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(routeLaneIds[i]))
                    {
                        route.Add(routeLaneIds[i]);
                    }
                }
            }

            int currentLaneIndex = route.FindIndex(id => string.Equals(id, laneId, StringComparison.Ordinal));
            if (currentLaneIndex < 0)
            {
                route.Insert(0, laneId);
            }
            else if (currentLaneIndex > 0)
            {
                route.RemoveRange(0, currentLaneIndex);
            }

            return route;
        }

        private static void CopyRoute(IReadOnlyList<string> source, List<string> destination)
        {
            destination.Clear();
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(source[i]))
                {
                    destination.Add(source[i]);
                }
            }
        }

        private static RoadAgentMask NormalizeAgentMask(RoadAgentMask agentMask)
        {
            return agentMask == RoadAgentMask.None ? RoadAgentMask.MotorVehicles : agentMask;
        }

        private void AddVehicleToLaneIndex(VehicleTrafficState state)
        {
            if (!vehiclesByLane.TryGetValue(state.laneId, out List<VehicleTrafficState> laneVehicles))
            {
                laneVehicles = new List<VehicleTrafficState>();
                vehiclesByLane.Add(state.laneId, laneVehicles);
            }

            if (!laneVehicles.Contains(state))
            {
                laneVehicles.Add(state);
            }
        }

        private void RemoveVehicleFromLaneIndex(VehicleTrafficState state)
        {
            if (state == null ||
                string.IsNullOrWhiteSpace(state.laneId) ||
                !vehiclesByLane.TryGetValue(state.laneId, out List<VehicleTrafficState> laneVehicles))
            {
                return;
            }

            laneVehicles.Remove(state);
            if (laneVehicles.Count == 0)
            {
                vehiclesByLane.Remove(state.laneId);
            }
        }

        private void RemoveExpiredPassages()
        {
            List<string> expired = new List<string>();
            foreach (PassageToken token in activePassagesByVehicleId.Values)
            {
                if (token != null && token.expireTime < trafficClock)
                {
                    expired.Add(token.vehicleId);
                }
            }

            for (int i = 0; i < expired.Count; i++)
            {
                ReleasePassage(expired[i]);
            }
        }

        private void RemoveExpiredLaneChanges()
        {
            List<string> expired = new List<string>();
            foreach (LaneChangeReservation reservation in laneChangeReservationsByVehicleId.Values)
            {
                if (reservation != null && reservation.expireTime < trafficClock)
                {
                    expired.Add(reservation.vehicleId);
                }
            }

            for (int i = 0; i < expired.Count; i++)
            {
                laneChangeReservationsByVehicleId.Remove(expired[i]);
            }
        }

        private void RemoveStaleVehicles()
        {
            if (staleVehicleTimeout <= 0f)
            {
                return;
            }

            List<string> stale = new List<string>();
            foreach (VehicleTrafficState state in vehiclesById.Values)
            {
                if (state != null && trafficClock - state.lastSeenTime > staleVehicleTimeout)
                {
                    stale.Add(state.vehicleId);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                UnregisterVehicle(stale[i]);
            }
        }

        private int GetRegisteredVehicleCount()
        {
            return vehiclesById.Count;
        }

        private int GetQueuedVehicleCount()
        {
            int count = 0;
            foreach (List<JunctionQueueEntry> queue in queuesByConnectorLaneId.Values)
            {
                count += queue.Count;
            }

            return count;
        }

        private int GetActiveTrafficTokenCount()
        {
            return activePassagesByVehicleId.Count;
        }

        private int GetLaneChangeReservationCount()
        {
            return laneChangeReservationsByVehicleId.Count;
        }

        private int GetSignalPhaseCount()
        {
            int count = 0;
            foreach (BakedJunctionTrafficRecord junction in trafficJunctionsById.Values)
            {
                count += junction?.signalPhases == null ? 0 : junction.signalPhases.Count;
            }

            return count;
        }

        private string GetLastTrafficFailureReason()
        {
            return lastTrafficFailureReason ?? string.Empty;
        }

        private void SetTrafficFailure(string reason)
        {
            lastTrafficFailureReason = reason ?? string.Empty;
        }

        private void ValidateTrafficSettings()
        {
            defaultVehicleLength = Mathf.Max(0.1f, defaultVehicleLength);
            leadVehicleSearchDistance = Mathf.Max(1f, leadVehicleSearchDistance);
            laneChangeSafetyGap = Mathf.Max(0.1f, laneChangeSafetyGap);
            laneChangeReservationDuration = Mathf.Max(0.1f, laneChangeReservationDuration);
            redSignalRouteCost = Mathf.Max(0f, redSignalRouteCost);
            yellowSignalRouteCost = Mathf.Max(0f, yellowSignalRouteCost);
            queueRouteCostPerVehicle = Mathf.Max(0f, queueRouteCostPerVehicle);
            staleVehicleTimeout = Mathf.Max(0f, staleVehicleTimeout);
        }

        private static bool AllowsTurn(RoadLaneTurnMask mask, RoadLaneTurn turn)
        {
            return turn switch
            {
                RoadLaneTurn.Straight => (mask & RoadLaneTurnMask.Straight) != 0,
                RoadLaneTurn.Left => (mask & RoadLaneTurnMask.Left) != 0,
                RoadLaneTurn.Right => (mask & RoadLaneTurnMask.Right) != 0,
                RoadLaneTurn.UTurn => (mask & RoadLaneTurnMask.UTurn) != 0,
                _ => false
            };
        }

        private sealed class VehicleTrafficState
        {
            public readonly string vehicleId;
            public readonly List<string> routeLaneIds = new List<string>();
            public string laneId = string.Empty;
            public RoadAgentMask agentMask = RoadAgentMask.MotorVehicles;
            public float distanceAlongLane;
            public float speed;
            public float length;
            public float lastSeenTime;

            public VehicleTrafficState(string vehicleId)
            {
                this.vehicleId = vehicleId;
            }
        }

        private sealed class JunctionRuntimeState
        {
            private readonly BakedJunctionTrafficRecord record;
            private int phaseIndex;
            private float phaseTime;

            public JunctionRuntimeState(BakedJunctionTrafficRecord record)
            {
                this.record = record;
            }

            public BakedJunctionSignalPhaseRecord CurrentPhase =>
                record.signalPhases == null || record.signalPhases.Count == 0
                    ? null
                    : record.signalPhases[Mathf.Clamp(phaseIndex, 0, record.signalPhases.Count - 1)];

            public float CurrentPhaseTime => phaseTime;

            public void Advance(float deltaTime)
            {
                if (record.controlMode != RoadJunctionTrafficControlMode.FixedSignal ||
                    record.signalPhases == null ||
                    record.signalPhases.Count == 0 ||
                    deltaTime <= 0f)
                {
                    return;
                }

                phaseTime += deltaTime;
                BakedJunctionSignalPhaseRecord phase = CurrentPhase;
                while (phase != null && phaseTime >= phase.TotalDuration)
                {
                    phaseTime -= phase.TotalDuration;
                    phaseIndex = (phaseIndex + 1) % record.signalPhases.Count;
                    phase = CurrentPhase;
                }
            }
        }

        private sealed class JunctionQueueEntry
        {
            public string vehicleId = string.Empty;
            public string connectorLaneId = string.Empty;
            public float requestTime;
        }

        private sealed class PassageToken
        {
            public string tokenId = string.Empty;
            public string vehicleId = string.Empty;
            public string junctionId = string.Empty;
            public string connectorLaneId = string.Empty;
            public string targetLaneId = string.Empty;
            public float expireTime;
            public bool enteredConnector;
        }

        private sealed class LaneChangeReservation
        {
            public string vehicleId = string.Empty;
            public string fromLaneId = string.Empty;
            public string targetLaneId = string.Empty;
            public RoadLaneAdjacentSide side;
            public float reservedDistanceAlongLane;
            public float expireTime;
            public VehicleRoadLaneChangeStatus status;
        }
    }
}
