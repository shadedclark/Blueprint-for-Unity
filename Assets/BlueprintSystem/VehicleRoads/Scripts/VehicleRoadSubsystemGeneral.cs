using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VehicleRoads
{
    public sealed partial class VehicleRoadSubsystem
    {
        private readonly Dictionary<string, RoadAgent> roadAgents =
            new Dictionary<string, RoadAgent>(StringComparer.Ordinal);
        private readonly RoadDiagnosticRingBuffer diagnosticHistory = new RoadDiagnosticRingBuffer();
        private readonly List<RoadAreaQueryResult> networkQueryScratch = new List<RoadAreaQueryResult>(64);
        private RoadQueryDebugSnapshot lastQuerySnapshot = new RoadQueryDebugSnapshot();
        private RoadRouteDebugSnapshot lastRouteSnapshot = new RoadRouteDebugSnapshot();
        private int counterFrame = -1;
        private int queriesThisFrame;
        private int routesThisFrame;
        private int replansThisFrame;
        private int failuresThisFrame;
        private int lastCandidateCount;
        private int peakCandidateCount;
        private int lastVisitedNodeCount;
        private int peakVisitedNodeCount;
        private int lastRouteSegmentCount;
        private int peakRouteSegmentCount;

        public bool TryFindNearestElement(
            Vector3 position,
            RoadAgentMask agentMask,
            RoadTagFilter tagFilter,
            float agentRadius,
            float maximumDistance,
            float maximumHeightDifference,
            out RoadLocation location)
        {
            BeginCounterFrame();
            queriesThisFrame++;
            location = default;
            bool found = false;
            float bestDistance = float.PositiveInfinity;
            int candidateCount = 0;
            for (int i = 0; i < registeredNetworks.Count; i++)
            {
                BakedLaneNetwork network = registeredNetworks[i];
                if (network == null ||
                    !network.TryFindNearestElement(
                        position,
                        agentMask,
                        tagFilter,
                        agentRadius,
                        maximumDistance,
                        maximumHeightDifference,
                        out RoadLocation candidate))
                {
                    continue;
                }

                candidateCount++;
                float distance = Vector3.Distance(position, candidate.projectedPosition);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                location = candidate;
                found = true;
            }

            lastCandidateCount = candidateCount;
            peakCandidateCount = Mathf.Max(peakCandidateCount, candidateCount);
            lastQuerySnapshot = new RoadQueryDebugSnapshot
            {
                shape = RoadAreaQueryShape.Point,
                center = position,
                radius = maximumDistance,
                agentMask = agentMask,
                tagFilter = tagFilter,
                agentRadius = agentRadius,
                candidateCount = candidateCount,
                resultCount = found ? 1 : 0,
                bestResult = location,
                failureReason = found ? RoadQueryFailureReason.None : RoadQueryFailureReason.NoElement
            };
            if (!found)
            {
                failuresThisFrame++;
            }

            RecordQueryEvent(found, position, location.elementId, candidateCount);
            return found;
        }

        public int QueryArea(in RoadAreaQuery query, List<RoadAreaQueryResult> results)
        {
            BeginCounterFrame();
            queriesThisFrame++;
            if (results == null)
            {
                failuresThisFrame++;
                return 0;
            }

            results.Clear();
            int candidateCount = 0;
            int maximumResults = query.maximumResults <= 0 ? int.MaxValue : query.maximumResults;
            for (int i = 0; i < registeredNetworks.Count; i++)
            {
                networkQueryScratch.Clear();
                registeredNetworks[i].QueryArea(query, networkQueryScratch);
                candidateCount += networkQueryScratch.Count;
                for (int resultIndex = 0;
                     resultIndex < networkQueryScratch.Count && results.Count < maximumResults;
                     resultIndex++)
                {
                    results.Add(networkQueryScratch[resultIndex]);
                }
            }

            results.Sort((left, right) => left.distance.CompareTo(right.distance));
            lastCandidateCount = candidateCount;
            peakCandidateCount = Mathf.Max(peakCandidateCount, candidateCount);
            lastQuerySnapshot = new RoadQueryDebugSnapshot
            {
                shape = query.shape,
                center = query.center,
                radius = query.radius,
                bounds = query.bounds,
                agentMask = query.agentMask,
                tagFilter = query.tagFilter,
                agentRadius = query.agentRadius,
                candidateCount = candidateCount,
                resultCount = results.Count,
                bestResult = results.Count > 0 ? results[0].location : default,
                failureReason = results.Count > 0 ? RoadQueryFailureReason.None : RoadQueryFailureReason.NoElement
            };
            if (results.Count == 0)
            {
                failuresThisFrame++;
            }

            RecordQueryEvent(
                results.Count > 0,
                query.center,
                results.Count > 0 ? results[0].location.elementId : string.Empty,
                candidateCount);
            return results.Count;
        }

        public bool TryFindRoute(RoadRouteQuery query, out RoadNetworkRouteResult result)
        {
            BeginCounterFrame();
            routesThisFrame++;
            result = null;
            for (int i = 0; i < registeredNetworks.Count; i++)
            {
                if (!registeredNetworks[i].TryFindRoute(query, out RoadNetworkRouteResult candidate))
                {
                    if (result == null)
                    {
                        result = candidate;
                    }

                    continue;
                }

                if (result == null || candidate.totalCost < result.totalCost)
                {
                    result = candidate;
                }
            }

            bool success = result != null && result.state == RoadRouteState.Valid;
            int visited = result == null ? 0 : result.visitedNodeCount;
            int segmentCount = result == null ? 0 : result.segments.Count;
            lastVisitedNodeCount = visited;
            peakVisitedNodeCount = Mathf.Max(peakVisitedNodeCount, visited);
            lastRouteSegmentCount = segmentCount;
            peakRouteSegmentCount = Mathf.Max(peakRouteSegmentCount, segmentCount);
            lastRouteSnapshot = new RoadRouteDebugSnapshot
            {
                startPosition = query.startPosition,
                destinationPosition = query.destinationPosition,
                agentMask = query.agentMask,
                tagFilter = query.tagFilter,
                state = result == null ? RoadRouteState.Invalid : result.state,
                failureReason = result == null ? RoadQueryFailureReason.RouteNotFound : result.failureReason,
                visitedNodeCount = visited,
                segmentCount = segmentCount,
                totalCost = result == null ? 0f : result.totalCost,
                startElementId = result == null ? string.Empty : result.start.elementId,
                destinationElementId = result == null ? string.Empty : result.destination.elementId
            };
            if (!success)
            {
                failuresThisFrame++;
            }

            RecordDiagnosticEvent(new RoadDiagnosticEvent
            {
                type = success ? RoadDiagnosticEventType.RouteSucceeded : RoadDiagnosticEventType.RouteFailed,
                frame = Time.frameCount,
                time = Time.time,
                primaryId = lastRouteSnapshot.startElementId,
                secondaryId = lastRouteSnapshot.destinationElementId,
                routeState = lastRouteSnapshot.state,
                failureReason = lastRouteSnapshot.failureReason,
                position = query.startPosition,
                visitedNodeCount = visited,
                cost = lastRouteSnapshot.totalCost
            });
            return success;
        }

        public bool RegisterRoadAgent(RoadAgent agent)
        {
            if (agent == null || string.IsNullOrWhiteSpace(agent.AgentId))
            {
                return false;
            }

            if (roadAgents.TryGetValue(agent.AgentId, out RoadAgent existing) && existing != agent)
            {
                return false;
            }

            roadAgents[agent.AgentId] = agent;
            return true;
        }

        public bool UnregisterRoadAgent(RoadAgent agent)
        {
            return agent != null &&
                   roadAgents.TryGetValue(agent.AgentId, out RoadAgent existing) &&
                   existing == agent &&
                   roadAgents.Remove(agent.AgentId);
        }

        public bool TryGetAgentSnapshot(string agentId, out RoadAgentDebugSnapshot snapshot)
        {
            snapshot = null;
            return roadAgents.TryGetValue(agentId ?? string.Empty, out RoadAgent agent) &&
                   agent != null &&
                   (snapshot = agent.GetDebugSnapshot()) != null;
        }

        public RoadQueryDebugSnapshot GetLastQuerySnapshot()
        {
            return CloneQuerySnapshot(lastQuerySnapshot);
        }

        public RoadRouteDebugSnapshot GetLastRouteSnapshot()
        {
            return CloneRouteSnapshot(lastRouteSnapshot);
        }

        public int CopyDiagnosticHistory(RoadDiagnosticEvent[] destination, int destinationIndex = 0)
        {
            return diagnosticHistory.CopyTo(destination, destinationIndex);
        }

        public string CreateCompactDebugReport(int maximumAgents = 8, int maximumEvents = 16)
        {
            VehicleRoadSubsystemSnapshot snapshot = GetSnapshot();
            StringBuilder builder = new StringBuilder(1024);
            builder.Append("RoadNetwork networks=").Append(snapshot.registeredNetworkCount)
                .Append(" lanes=").Append(snapshot.laneCount)
                .Append(" polygons=").Append(snapshot.polygonCount)
                .Append(" portals=").Append(snapshot.portalCount)
                .Append(" agents=").Append(snapshot.registeredRoadAgentCount)
                .Append(" queued=").Append(snapshot.queuedVehicleCount)
                .Append(" tokens=").Append(snapshot.activeTokenCount)
                .AppendLine();
            builder.Append("Frame queries=").Append(snapshot.queriesThisFrame)
                .Append(" routes=").Append(snapshot.routesThisFrame)
                .Append(" replans=").Append(snapshot.replansThisFrame)
                .Append(" failures=").Append(snapshot.failuresThisFrame)
                .AppendLine();
            int agentCount = 0;
            foreach (KeyValuePair<string, RoadAgent> pair in roadAgents)
            {
                if (agentCount++ >= Mathf.Max(0, maximumAgents) || pair.Value == null)
                {
                    break;
                }

                RoadAgentDebugSnapshot agent = pair.Value.GetDebugSnapshot();
                builder.Append("Agent ").Append(agent.agentId)
                    .Append(" state=").Append(agent.state)
                    .Append(" route=").Append(agent.routeState)
                    .Append(" element=").Append(agent.currentElementKind).Append(':').Append(agent.currentElementId)
                    .Append(" segment=").Append(agent.routeSegmentIndex).Append('/').Append(agent.routeSegmentCount)
                    .Append(" remaining=").Append(agent.remainingDistance.ToString("0.###"))
                    .Append(" failure=").Append(agent.failureReason)
                    .AppendLine();
            }

            int eventCount = Mathf.Min(Mathf.Max(0, maximumEvents), diagnosticHistory.Count);
            RoadDiagnosticEvent[] events = new RoadDiagnosticEvent[eventCount];
            int copied = diagnosticHistory.CopyTo(events);
            for (int i = Mathf.Max(0, copied - eventCount); i < copied; i++)
            {
                RoadDiagnosticEvent diagnostic = events[i];
                builder.Append("Event ").Append(diagnostic.frame).Append(' ')
                    .Append(diagnostic.type)
                    .Append(" agent=").Append(diagnostic.agentId)
                    .Append(" primary=").Append(diagnostic.primaryId)
                    .Append(" failure=").Append(diagnostic.failureReason)
                    .AppendLine();
            }

            return builder.ToString();
        }

        internal void NotifyAgentStateChanged(
            string agentId,
            RoadAgentState previous,
            RoadAgentState current,
            RoadRouteState routeState,
            RoadQueryFailureReason failureReason,
            string elementId,
            Vector3 position)
        {
            if (previous == RoadAgentState.Replanning || current == RoadAgentState.Replanning)
            {
                BeginCounterFrame();
                replansThisFrame++;
            }

            if (runtimeSettings == null || !runtimeSettings.CaptureAgentStateTransitions)
            {
                return;
            }

            RecordDiagnosticEvent(new RoadDiagnosticEvent
            {
                type = RoadDiagnosticEventType.AgentStateChanged,
                frame = Time.frameCount,
                time = Time.time,
                agentId = agentId ?? string.Empty,
                primaryId = elementId ?? string.Empty,
                agentState = current,
                routeState = routeState,
                failureReason = failureReason,
                position = position
            });
        }

        private void ConfigureDiagnostics()
        {
            RoadNetworkProfiler.Configure(runtimeSettings);
            if (runtimeSettings != null && runtimeSettings.EnableDetailedDiagnosticHistory)
            {
                diagnosticHistory.Configure(runtimeSettings.DiagnosticHistoryCapacity);
            }
            else
            {
                diagnosticHistory.Clear();
            }
        }

        private void RecordQueryEvent(bool success, Vector3 position, string resultId, int candidateCount)
        {
            if (runtimeSettings == null ||
                success && !runtimeSettings.CaptureSuccessfulQueries ||
                !success && !runtimeSettings.CaptureFailedQueries)
            {
                return;
            }

            RecordDiagnosticEvent(new RoadDiagnosticEvent
            {
                type = success ? RoadDiagnosticEventType.QuerySucceeded : RoadDiagnosticEventType.QueryFailed,
                frame = Time.frameCount,
                time = Time.time,
                primaryId = resultId ?? string.Empty,
                position = position,
                candidateCount = candidateCount,
                failureReason = success ? RoadQueryFailureReason.None : RoadQueryFailureReason.NoElement
            });
        }

        private void RecordDiagnosticEvent(in RoadDiagnosticEvent diagnosticEvent)
        {
            if (runtimeSettings != null && runtimeSettings.EnableDetailedDiagnosticHistory)
            {
                if (diagnosticHistory.Capacity != runtimeSettings.DiagnosticHistoryCapacity)
                {
                    diagnosticHistory.Configure(runtimeSettings.DiagnosticHistoryCapacity);
                }

                diagnosticHistory.Add(diagnosticEvent);
            }
        }

        private void BeginCounterFrame()
        {
            int frame = Time.frameCount;
            if (counterFrame == frame)
            {
                return;
            }

            counterFrame = frame;
            queriesThisFrame = 0;
            routesThisFrame = 0;
            replansThisFrame = 0;
            failuresThisFrame = 0;
        }

        private int GetRegisteredRoadAgentCount() => roadAgents.Count;
        private int GetQueriesThisFrame() { BeginCounterFrame(); return queriesThisFrame; }
        private int GetRoutesThisFrame() { BeginCounterFrame(); return routesThisFrame; }
        private int GetReplansThisFrame() { BeginCounterFrame(); return replansThisFrame; }
        private int GetFailuresThisFrame() { BeginCounterFrame(); return failuresThisFrame; }
        private int GetLastCandidateCount() => lastCandidateCount;
        private int GetPeakCandidateCount() => peakCandidateCount;
        private int GetLastVisitedNodeCount() => lastVisitedNodeCount;
        private int GetPeakVisitedNodeCount() => peakVisitedNodeCount;
        private int GetLastRouteSegmentCount() => lastRouteSegmentCount;
        private int GetPeakRouteSegmentCount() => peakRouteSegmentCount;
        private int GetDiagnosticHistoryCount() => diagnosticHistory.Count;
        private int GetDiagnosticHistoryCapacity() => diagnosticHistory.Capacity;
        private int GetDiagnosticDroppedCount() => diagnosticHistory.DroppedCount;

        private static RoadQueryDebugSnapshot CloneQuerySnapshot(RoadQueryDebugSnapshot source)
        {
            return source == null
                ? new RoadQueryDebugSnapshot()
                : new RoadQueryDebugSnapshot
                {
                    shape = source.shape,
                    center = source.center,
                    radius = source.radius,
                    bounds = source.bounds,
                    agentMask = source.agentMask,
                    tagFilter = source.tagFilter,
                    agentRadius = source.agentRadius,
                    candidateCount = source.candidateCount,
                    resultCount = source.resultCount,
                    bestResult = source.bestResult,
                    failureReason = source.failureReason
                };
        }

        private static RoadRouteDebugSnapshot CloneRouteSnapshot(RoadRouteDebugSnapshot source)
        {
            return source == null
                ? new RoadRouteDebugSnapshot()
                : new RoadRouteDebugSnapshot
                {
                    startPosition = source.startPosition,
                    destinationPosition = source.destinationPosition,
                    agentMask = source.agentMask,
                    tagFilter = source.tagFilter,
                    state = source.state,
                    failureReason = source.failureReason,
                    visitedNodeCount = source.visitedNodeCount,
                    segmentCount = source.segmentCount,
                    totalCost = source.totalCost,
                    startElementId = source.startElementId,
                    destinationElementId = source.destinationElementId
                };
        }
    }
}
