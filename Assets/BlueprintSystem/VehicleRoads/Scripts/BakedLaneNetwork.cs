using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    public enum RoadLaneAdjacentSide
    {
        Left,
        Right
    }

    [Flags]
    public enum RoadLaneAdjacentFlags
    {
        None = 0,
        LaneChangeAllowed = 1 << 0,
        Merge = 1 << 1,
        Split = 1 << 2,
        Auto = 1 << 3
    }

    [Serializable]
    public sealed class BakedLaneRecord
    {
        public string laneId = string.Empty;
        public string sourceLaneId = string.Empty;
        public RoadLaneKind kind;
        public RoadLaneTravelDirection direction;
        public RoadLaneTurn turnType;
        public bool open = true;
        public bool orphaned;
        public float length;
        public float speedLimit;
        public float width = 3.5f;
        public float minimumWidth = 3.5f;
        public float maximumWidth = 3.5f;
        public RoadTagMask tagMask = RoadTagMask.Road | RoadTagMask.Vehicle;
        public RoadAgentMask allowedAgents = RoadAgentMask.MotorVehicles;
        public bool allowLaneChangeLeft = true;
        public bool allowLaneChangeRight = true;
        public Bounds bounds;
        public float lateralOffset;
        public float verticalOffset;
        public int firstSampleIndex;
        public int sampleCount;
        public string connectorSourceLaneId = string.Empty;
        public string connectorTargetLaneId = string.Empty;
        public string connectorJunctionId = string.Empty;

        public bool AllowsAgent(RoadAgentMask agentMask)
        {
            return RoadAgentMaskUtility.Allows(allowedAgents, agentMask);
        }
    }

    [Serializable]
    public sealed class BakedLaneSampleRecord
    {
        public string sampleId = string.Empty;
        public string laneId = string.Empty;
        public int order;
        public Vector3 splinePosition;
        public Vector3 finalPosition;
        public Vector3 leftBoundary;
        public Vector3 rightBoundary;
        public Vector3 forward = Vector3.forward;
        public Vector3 up = Vector3.up;
        public float curvature;
        public float distanceAlongLane;
        public float width = 3.5f;
        public string previousSampleId = string.Empty;
        public string nextSampleId = string.Empty;
        public string connectionSampleIds = string.Empty;
        public float lateralOffset;
        public float verticalOffset;
        public bool valid = true;
        public string errorReason = string.Empty;
    }

    [Serializable]
    public sealed class BakedPolygonRecord
    {
        public string zoneId = string.Empty;
        public bool open = true;
        public float traversalCost = 1f;
        public RoadTagMask tagMask;
        public RoadAgentMask allowedAgents = RoadAgentMask.All;
        public float minimumWorldHeight;
        public float maximumWorldHeight;
        public Bounds bounds;
        public List<Vector3> vertices = new List<Vector3>();
        public List<int> triangles = new List<int>();

        public bool AllowsAgent(RoadAgentMask agentMask)
        {
            return RoadAgentMaskUtility.Allows(allowedAgents, agentMask);
        }
    }

    [Serializable]
    public sealed class BakedPortalRecord
    {
        public string portalId = string.Empty;
        public string sourceZoneId = string.Empty;
        public RoadElementKind targetKind;
        public string targetElementId = string.Empty;
        public RoadPortalDirection direction;
        public bool open = true;
        public float width = 2f;
        public float traversalCost = 1f;
        public RoadTagMask tagMask;
        public RoadAgentMask allowedAgents = RoadAgentMask.All;
        public Vector3 sourcePosition;
        public Vector3 targetPosition;
        public float targetLaneDistance;

        public bool AllowsAgent(RoadAgentMask agentMask, float agentRadius)
        {
            return RoadAgentMaskUtility.Allows(allowedAgents, agentMask) &&
                   width + 0.0001f >= Mathf.Max(0f, agentRadius) * 2f;
        }
    }

    [Serializable]
    public sealed class BakedLaneConnectionRecord
    {
        public string connectionId = string.Empty;
        public string fromLaneId = string.Empty;
        public string toLaneId = string.Empty;
        public string fromSampleId = string.Empty;
        public string toSampleId = string.Empty;
        public RoadLaneTurn turnType;
        public bool open = true;
        public float baseCost;
    }

    [Serializable]
    public sealed class BakedLaneAdjacentLinkRecord
    {
        public string linkId = string.Empty;
        public string fromLaneId = string.Empty;
        public string toLaneId = string.Empty;
        public RoadLaneAdjacentSide side;
        public RoadLaneAdjacentFlags flags;
        public bool open = true;
        public float baseCost;
        public float minLateralDistance;
        public float maxLateralDistance;
        public float overlapStartDistance;
        public float overlapEndDistance;
    }

    [Serializable]
    public sealed class BakedLaneSummary
    {
        public int authoredLaneCount;
        public int directedLaneCount;
        public int connectorLaneCount;
        public int sampleCount;
        public int connectionCount;
        public int adjacentLinkCount;
        public int laneChangeLinkCount;
        public int junctionTrafficCount;
        public int connectorTrafficCount;
        public int polygonCount;
        public int portalCount;
        public int polygonTriangleCount;
        public int invalidLaneCount;
        public int invalidPolygonCount;
        public int invalidSampleCount;
    }

    public struct BakedLaneNearestResult
    {
        public BakedLaneRecord lane;
        public Vector3 position;
        public Vector3 forward;
        public Vector3 up;
        public float distanceAlongLane;
        public float distanceToLane;
        public float distanceToBoundary;
        public float lateralRatio;
        public int segmentStartSampleIndex;
    }

    [CreateAssetMenu(menuName = "Vehicle Road/Road Network/Baked Lane Network")]
    public sealed partial class BakedLaneNetwork : ScriptableObject, ISerializationCallbackReceiver
    {
        public const string CurrentSchemaVersion = "3.1";

        [SerializeField] private string schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string scenePath = string.Empty;
        [SerializeField, Min(0.1f)] private float sampleSpacing = 1f;
        [SerializeField] private RoadNetworkSettings networkSettings;
        [SerializeField] private RoadNetworkRuntimeSettings runtimeSettings;
        [SerializeField] private BakedLaneSummary summary = new BakedLaneSummary();
        [SerializeField] private List<BakedLaneRecord> lanes = new List<BakedLaneRecord>();
        [SerializeField] private List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>();
        [SerializeField] private List<BakedLaneConnectionRecord> connections = new List<BakedLaneConnectionRecord>();
        [SerializeField] private List<BakedLaneAdjacentLinkRecord> adjacentLinks = new List<BakedLaneAdjacentLinkRecord>();
        [SerializeField] private List<BakedJunctionTrafficRecord> junctionTraffic = new List<BakedJunctionTrafficRecord>();
        [SerializeField] private List<BakedConnectorTrafficRecord> connectorTraffic = new List<BakedConnectorTrafficRecord>();
        [SerializeField] private List<BakedPolygonRecord> polygons = new List<BakedPolygonRecord>();
        [SerializeField] private List<BakedPortalRecord> portals = new List<BakedPortalRecord>();

        [NonSerialized] private Dictionary<string, BakedLaneRecord> laneById;
        [NonSerialized] private Dictionary<string, int> laneIndexById;
        [NonSerialized] private Dictionary<string, List<BakedLaneConnectionRecord>> outgoingConnections;
        [NonSerialized] private Dictionary<string, List<BakedLaneAdjacentLinkRecord>> adjacentLinksByLane;
        [NonSerialized] private Dictionary<string, BakedJunctionTrafficRecord> junctionTrafficById;
        [NonSerialized] private Dictionary<string, BakedConnectorTrafficRecord> connectorTrafficByLaneId;
        [NonSerialized] private Dictionary<string, BakedConnectorTrafficRecord> connectorTrafficByConnectionId;
        [NonSerialized] private Dictionary<string, BakedPolygonRecord> polygonById;
        [NonSerialized] private Dictionary<string, BakedPortalRecord> portalById;
        [NonSerialized] private Dictionary<string, List<BakedPortalRecord>> portalsBySourceZone;
        [NonSerialized] private Dictionary<string, List<BakedPortalRecord>> portalsByTargetElement;
        [NonSerialized] private Dictionary<Vector3Int, List<SegmentRef>> spatialCells;
        [NonSerialized] private Dictionary<Vector3Int, List<int>> polygonSpatialCells;
        [NonSerialized] private float spatialCellSize = 8f;

        public string SchemaVersion => schemaVersion;
        public string ScenePath => scenePath;
        public float SampleSpacing => sampleSpacing;
        public RoadNetworkSettings NetworkSettings => networkSettings;
        public RoadNetworkRuntimeSettings RuntimeSettings => runtimeSettings;
        public BakedLaneSummary Summary => summary;
        public IReadOnlyList<BakedLaneRecord> Lanes => lanes;
        public IReadOnlyList<BakedLaneSampleRecord> Samples => samples;
        public IReadOnlyList<BakedLaneConnectionRecord> Connections => connections;
        public IReadOnlyList<BakedLaneAdjacentLinkRecord> AdjacentLinks => adjacentLinks;
        public IReadOnlyList<BakedJunctionTrafficRecord> JunctionTraffic => junctionTraffic;
        public IReadOnlyList<BakedConnectorTrafficRecord> ConnectorTraffic => connectorTraffic;
        public IReadOnlyList<BakedPolygonRecord> Polygons => polygons;
        public IReadOnlyList<BakedPortalRecord> Portals => portals;

        public void SetData(
            string sourceScenePath,
            float spacing,
            BakedLaneSummary bakedSummary,
            List<BakedLaneRecord> bakedLanes,
            List<BakedLaneSampleRecord> bakedSamples,
            List<BakedLaneConnectionRecord> bakedConnections)
        {
            SetData(
                sourceScenePath,
                spacing,
                bakedSummary,
                bakedLanes,
                bakedSamples,
                bakedConnections,
                new List<BakedLaneAdjacentLinkRecord>(),
                new List<BakedJunctionTrafficRecord>(),
                new List<BakedConnectorTrafficRecord>());
        }

        public void SetData(
            string sourceScenePath,
            float spacing,
            BakedLaneSummary bakedSummary,
            List<BakedLaneRecord> bakedLanes,
            List<BakedLaneSampleRecord> bakedSamples,
            List<BakedLaneConnectionRecord> bakedConnections,
            List<BakedLaneAdjacentLinkRecord> bakedAdjacentLinks)
        {
            SetData(
                sourceScenePath,
                spacing,
                bakedSummary,
                bakedLanes,
                bakedSamples,
                bakedConnections,
                bakedAdjacentLinks,
                new List<BakedJunctionTrafficRecord>(),
                new List<BakedConnectorTrafficRecord>());
        }

        public void SetData(
            string sourceScenePath,
            float spacing,
            BakedLaneSummary bakedSummary,
            List<BakedLaneRecord> bakedLanes,
            List<BakedLaneSampleRecord> bakedSamples,
            List<BakedLaneConnectionRecord> bakedConnections,
            List<BakedLaneAdjacentLinkRecord> bakedAdjacentLinks,
            List<BakedJunctionTrafficRecord> bakedJunctionTraffic,
            List<BakedConnectorTrafficRecord> bakedConnectorTraffic)
        {
            SetData(
                sourceScenePath,
                spacing,
                bakedSummary,
                bakedLanes,
                bakedSamples,
                bakedConnections,
                bakedAdjacentLinks,
                bakedJunctionTraffic,
                bakedConnectorTraffic,
                new List<BakedPolygonRecord>(),
                new List<BakedPortalRecord>());
        }

        public void SetData(
            string sourceScenePath,
            float spacing,
            BakedLaneSummary bakedSummary,
            List<BakedLaneRecord> bakedLanes,
            List<BakedLaneSampleRecord> bakedSamples,
            List<BakedLaneConnectionRecord> bakedConnections,
            List<BakedLaneAdjacentLinkRecord> bakedAdjacentLinks,
            List<BakedJunctionTrafficRecord> bakedJunctionTraffic,
            List<BakedConnectorTrafficRecord> bakedConnectorTraffic,
            List<BakedPolygonRecord> bakedPolygons,
            List<BakedPortalRecord> bakedPortals)
        {
            schemaVersion = CurrentSchemaVersion;
            scenePath = sourceScenePath ?? string.Empty;
            sampleSpacing = Mathf.Max(0.1f, spacing);
            summary = bakedSummary ?? new BakedLaneSummary();
            lanes = bakedLanes ?? new List<BakedLaneRecord>();
            samples = bakedSamples ?? new List<BakedLaneSampleRecord>();
            connections = bakedConnections ?? new List<BakedLaneConnectionRecord>();
            adjacentLinks = bakedAdjacentLinks ?? new List<BakedLaneAdjacentLinkRecord>();
            junctionTraffic = bakedJunctionTraffic ?? new List<BakedJunctionTrafficRecord>();
            connectorTraffic = bakedConnectorTraffic ?? new List<BakedConnectorTrafficRecord>();
            polygons = bakedPolygons ?? new List<BakedPolygonRecord>();
            portals = bakedPortals ?? new List<BakedPortalRecord>();
            RebuildRuntimeCaches();
        }

        public void SetSettings(
            RoadNetworkSettings bakedNetworkSettings,
            RoadNetworkRuntimeSettings bakedRuntimeSettings)
        {
            networkSettings = bakedNetworkSettings;
            runtimeSettings = bakedRuntimeSettings;
        }

        public bool TryGetLane(string laneId, out BakedLaneRecord lane)
        {
            EnsureRuntimeCaches();
            return laneById.TryGetValue(laneId ?? string.Empty, out lane);
        }

        public IReadOnlyList<BakedLaneConnectionRecord> GetOutgoingConnections(string laneId)
        {
            EnsureRuntimeCaches();
            return outgoingConnections.TryGetValue(laneId ?? string.Empty, out List<BakedLaneConnectionRecord> result)
                ? result
                : Array.Empty<BakedLaneConnectionRecord>();
        }

        public IReadOnlyList<BakedLaneAdjacentLinkRecord> GetAdjacentLinks(string laneId)
        {
            EnsureRuntimeCaches();
            return adjacentLinksByLane.TryGetValue(laneId ?? string.Empty, out List<BakedLaneAdjacentLinkRecord> result)
                ? result
                : Array.Empty<BakedLaneAdjacentLinkRecord>();
        }

        public bool TryGetJunctionTraffic(string junctionId, out BakedJunctionTrafficRecord traffic)
        {
            EnsureRuntimeCaches();
            return junctionTrafficById.TryGetValue(junctionId ?? string.Empty, out traffic);
        }

        public bool TryGetConnectorTraffic(string connectorLaneId, out BakedConnectorTrafficRecord traffic)
        {
            EnsureRuntimeCaches();
            return connectorTrafficByLaneId.TryGetValue(connectorLaneId ?? string.Empty, out traffic);
        }

        public bool TryGetConnectorTrafficForConnection(string connectionId, out BakedConnectorTrafficRecord traffic)
        {
            EnsureRuntimeCaches();
            return connectorTrafficByConnectionId.TryGetValue(connectionId ?? string.Empty, out traffic);
        }

        public bool TryGetPolygon(string zoneId, out BakedPolygonRecord polygon)
        {
            EnsureRuntimeCaches();
            return polygonById.TryGetValue(zoneId ?? string.Empty, out polygon);
        }

        public bool TryGetPortal(string portalId, out BakedPortalRecord portal)
        {
            EnsureRuntimeCaches();
            return portalById.TryGetValue(portalId ?? string.Empty, out portal);
        }

        public IReadOnlyList<BakedPortalRecord> GetPortalsFromZone(string zoneId)
        {
            EnsureRuntimeCaches();
            return portalsBySourceZone.TryGetValue(zoneId ?? string.Empty, out List<BakedPortalRecord> result)
                ? result
                : Array.Empty<BakedPortalRecord>();
        }

        public IReadOnlyList<BakedPortalRecord> GetPortalsTargetingElement(string elementId)
        {
            EnsureRuntimeCaches();
            return portalsByTargetElement.TryGetValue(elementId ?? string.Empty, out List<BakedPortalRecord> result)
                ? result
                : Array.Empty<BakedPortalRecord>();
        }

        public bool TryGetAdjacentLane(
            string laneId,
            RoadLaneAdjacentSide side,
            out BakedLaneAdjacentLinkRecord link)
        {
            link = null;
            IReadOnlyList<BakedLaneAdjacentLinkRecord> links = GetAdjacentLinks(laneId);
            for (int i = 0; i < links.Count; i++)
            {
                if (links[i] != null && links[i].side == side)
                {
                    link = links[i];
                    return true;
                }
            }

            return false;
        }

        public List<BakedLaneAdjacentLinkRecord> GetLaneChangeLinks(string laneId, RoadAgentMask agentMask)
        {
            agentMask = NormalizeAgentMask(agentMask);
            List<BakedLaneAdjacentLinkRecord> result = new List<BakedLaneAdjacentLinkRecord>();
            if (!TryGetLane(laneId, out BakedLaneRecord sourceLane) ||
                !sourceLane.open ||
                sourceLane.orphaned ||
                !sourceLane.AllowsAgent(agentMask))
            {
                return result;
            }

            IReadOnlyList<BakedLaneAdjacentLinkRecord> links = GetAdjacentLinks(laneId);
            for (int i = 0; i < links.Count; i++)
            {
                BakedLaneAdjacentLinkRecord link = links[i];
                if (link == null ||
                    !link.open ||
                    (link.flags & RoadLaneAdjacentFlags.LaneChangeAllowed) == 0 ||
                    !TryGetLane(link.toLaneId, out BakedLaneRecord targetLane) ||
                    !targetLane.open ||
                    targetLane.orphaned ||
                    !targetLane.AllowsAgent(agentMask))
                {
                    continue;
                }

                result.Add(link);
            }

            return result;
        }

        public bool TryEvaluate(string laneId, float distance, out RoadLanePose pose)
        {
            pose = default;
            if (!TryGetLane(laneId, out BakedLaneRecord lane) || lane.sampleCount <= 0)
            {
                return false;
            }

            distance = Mathf.Clamp(distance, 0f, lane.length);
            int first = lane.firstSampleIndex;
            int last = first + lane.sampleCount - 1;
            if (first < 0 || last >= samples.Count)
            {
                return false;
            }

            int right = last;
            for (int i = first + 1; i <= last; i++)
            {
                if (samples[i].distanceAlongLane >= distance)
                {
                    right = i;
                    break;
                }
            }

            int left = Mathf.Max(first, right - 1);
            BakedLaneSampleRecord a = samples[left];
            BakedLaneSampleRecord b = samples[right];
            float range = b.distanceAlongLane - a.distanceAlongLane;
            float alpha = range <= 0.0001f ? 0f : Mathf.Clamp01((distance - a.distanceAlongLane) / range);
            Vector3 forward = Vector3.Slerp(a.forward, b.forward, alpha).normalized;
            Vector3 up = Vector3.Slerp(a.up, b.up, alpha).normalized;
            pose = new RoadLanePose
            {
                splinePosition = Vector3.Lerp(a.splinePosition, b.splinePosition, alpha),
                position = Vector3.Lerp(a.finalPosition, b.finalPosition, alpha),
                forward = forward.sqrMagnitude <= 0.0001f ? Vector3.forward : forward,
                up = up.sqrMagnitude <= 0.0001f ? Vector3.up : up,
                curvature = Mathf.Lerp(a.curvature, b.curvature, alpha),
                distance = distance,
                normalizedT = lane.length <= 0.0001f ? 0f : distance / lane.length,
                lateralOffset = Mathf.Lerp(a.lateralOffset, b.lateralOffset, alpha),
                verticalOffset = Mathf.Lerp(a.verticalOffset, b.verticalOffset, alpha)
            };
            return true;
        }

        public bool TryFindNearestLane(
            Vector3 position,
            Vector3 heading,
            RoadAgentMask agentMask,
            float maximumDistance,
            float maximumHeightDifference,
            out BakedLaneNearestResult result,
            ISet<string> allowedLaneIds = null)
        {
            EnsureRuntimeCaches();
            agentMask = NormalizeAgentMask(agentMask);
            result = default;
            float bestDistanceSquared = maximumDistance * maximumDistance;
            bool found = false;
            Vector3Int center = ToCell(position);
            int radius = Mathf.Max(1, Mathf.CeilToInt(maximumDistance / spatialCellSize));
            queryVisitedSegments.Clear();

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        Vector3Int cell = center + new Vector3Int(x, y, z);
                        if (!spatialCells.TryGetValue(cell, out List<SegmentRef> segments))
                        {
                            continue;
                        }

                        for (int i = 0; i < segments.Count; i++)
                        {
                            SegmentRef segment = segments[i];
                            long segmentKey = ((long)segment.laneIndex << 32) | (uint)segment.sampleIndex;
                            if (!queryVisitedSegments.Add(segmentKey))
                            {
                                continue;
                            }

                            BakedLaneRecord lane = lanes[segment.laneIndex];
                            if (!lane.open ||
                                lane.orphaned ||
                                !lane.AllowsAgent(agentMask) ||
                                allowedLaneIds != null && !allowedLaneIds.Contains(lane.laneId))
                            {
                                continue;
                            }

                            BakedLaneSampleRecord a = samples[segment.sampleIndex];
                            BakedLaneSampleRecord b = samples[segment.sampleIndex + 1];
                            Vector3 nearest = ClosestPointOnSegment(position, a.finalPosition, b.finalPosition, out float t);
                            if (Mathf.Abs(position.y - nearest.y) > maximumHeightDifference)
                            {
                                continue;
                            }

                            Vector3 segmentForward = Vector3.Slerp(a.forward, b.forward, t).normalized;
                            if (heading.sqrMagnitude > 0.0001f && Vector3.Dot(heading.normalized, segmentForward) < -0.25f)
                            {
                                continue;
                            }

                            float distanceSquared = (position - nearest).sqrMagnitude;
                            if (distanceSquared >= bestDistanceSquared)
                            {
                                continue;
                            }

                            bestDistanceSquared = distanceSquared;
                            float segmentDistance = Mathf.Lerp(a.distanceAlongLane, b.distanceAlongLane, t);
                            float localWidth = Mathf.Lerp(
                                Mathf.Max(0.1f, a.width),
                                Mathf.Max(0.1f, b.width),
                                t);
                            float halfWidth = Mathf.Max(0.05f, localWidth * 0.5f);
                            Vector3 rightVector = Vector3.Cross(
                                Vector3.Slerp(a.up, b.up, t).normalized,
                                segmentForward);
                            float signedLateral = rightVector.sqrMagnitude <= 0.0001f
                                ? 0f
                                : Vector3.Dot(position - nearest, rightVector.normalized);
                            result = new BakedLaneNearestResult
                            {
                                lane = lane,
                                position = nearest,
                                forward = segmentForward,
                                up = Vector3.Slerp(a.up, b.up, t).normalized,
                                distanceAlongLane = segmentDistance,
                                distanceToLane = Mathf.Sqrt(distanceSquared),
                                distanceToBoundary = halfWidth - Mathf.Abs(signedLateral),
                                lateralRatio = Mathf.Clamp(signedLateral / halfWidth, -1f, 1f),
                                segmentStartSampleIndex = segment.sampleIndex
                            };
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            laneById = null;
            laneIndexById = null;
            outgoingConnections = null;
            adjacentLinksByLane = null;
            junctionTrafficById = null;
            connectorTrafficByLaneId = null;
            connectorTrafficByConnectionId = null;
            polygonById = null;
            portalById = null;
            portalsBySourceZone = null;
            portalsByTargetElement = null;
            spatialCells = null;
            polygonSpatialCells = null;
        }

        private void OnEnable()
        {
            RebuildRuntimeCaches();
        }

        private void EnsureRuntimeCaches()
        {
            if (laneById == null ||
                adjacentLinksByLane == null ||
                junctionTrafficById == null ||
                polygonById == null ||
                spatialCells == null ||
                polygonSpatialCells == null)
            {
                RebuildRuntimeCaches();
            }
        }

        private void RebuildRuntimeCaches()
        {
            lanes ??= new List<BakedLaneRecord>();
            samples ??= new List<BakedLaneSampleRecord>();
            connections ??= new List<BakedLaneConnectionRecord>();
            adjacentLinks ??= new List<BakedLaneAdjacentLinkRecord>();
            junctionTraffic ??= new List<BakedJunctionTrafficRecord>();
            connectorTraffic ??= new List<BakedConnectorTrafficRecord>();
            polygons ??= new List<BakedPolygonRecord>();
            portals ??= new List<BakedPortalRecord>();
            summary ??= new BakedLaneSummary();
            laneById = new Dictionary<string, BakedLaneRecord>(StringComparer.Ordinal);
            laneIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
            outgoingConnections = new Dictionary<string, List<BakedLaneConnectionRecord>>(StringComparer.Ordinal);
            adjacentLinksByLane = new Dictionary<string, List<BakedLaneAdjacentLinkRecord>>(StringComparer.Ordinal);
            junctionTrafficById = new Dictionary<string, BakedJunctionTrafficRecord>(StringComparer.Ordinal);
            connectorTrafficByLaneId = new Dictionary<string, BakedConnectorTrafficRecord>(StringComparer.Ordinal);
            connectorTrafficByConnectionId = new Dictionary<string, BakedConnectorTrafficRecord>(StringComparer.Ordinal);
            polygonById = new Dictionary<string, BakedPolygonRecord>(StringComparer.Ordinal);
            portalById = new Dictionary<string, BakedPortalRecord>(StringComparer.Ordinal);
            portalsBySourceZone = new Dictionary<string, List<BakedPortalRecord>>(StringComparer.Ordinal);
            portalsByTargetElement = new Dictionary<string, List<BakedPortalRecord>>(StringComparer.Ordinal);
            spatialCells = new Dictionary<Vector3Int, List<SegmentRef>>();
            polygonSpatialCells = new Dictionary<Vector3Int, List<int>>();
            spatialCellSize = Mathf.Max(4f, sampleSpacing * 4f);

            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.BuildSpatialIndex);
            for (int i = 0; i < lanes.Count; i++)
            {
                BakedLaneRecord lane = lanes[i];
                if (lane == null || string.IsNullOrWhiteSpace(lane.laneId))
                {
                    continue;
                }

                laneById[lane.laneId] = lane;
                laneIndexById[lane.laneId] = i;
                int first = lane.firstSampleIndex;
                int lastSegmentStart = first + lane.sampleCount - 2;
                for (int sampleIndex = first; sampleIndex <= lastSegmentStart; sampleIndex++)
                {
                    if (sampleIndex < 0 || sampleIndex + 1 >= samples.Count)
                    {
                        continue;
                    }

                    AddSegmentToSpatialCells(
                        i,
                        sampleIndex,
                        samples[sampleIndex].finalPosition,
                        samples[sampleIndex + 1].finalPosition,
                        Mathf.Max(samples[sampleIndex].width, samples[sampleIndex + 1].width) * 0.5f);
                }
            }

            for (int i = 0; i < connections.Count; i++)
            {
                BakedLaneConnectionRecord connection = connections[i];
                if (connection == null || string.IsNullOrWhiteSpace(connection.fromLaneId))
                {
                    continue;
                }

                if (!outgoingConnections.TryGetValue(connection.fromLaneId, out List<BakedLaneConnectionRecord> list))
                {
                    list = new List<BakedLaneConnectionRecord>();
                    outgoingConnections.Add(connection.fromLaneId, list);
                }

                list.Add(connection);
            }

            for (int i = 0; i < adjacentLinks.Count; i++)
            {
                BakedLaneAdjacentLinkRecord link = adjacentLinks[i];
                if (link == null || string.IsNullOrWhiteSpace(link.fromLaneId))
                {
                    continue;
                }

                if (!adjacentLinksByLane.TryGetValue(link.fromLaneId, out List<BakedLaneAdjacentLinkRecord> list))
                {
                    list = new List<BakedLaneAdjacentLinkRecord>();
                    adjacentLinksByLane.Add(link.fromLaneId, list);
                }

                list.Add(link);
            }

            for (int i = 0; i < junctionTraffic.Count; i++)
            {
                BakedJunctionTrafficRecord traffic = junctionTraffic[i];
                if (traffic == null || string.IsNullOrWhiteSpace(traffic.junctionId))
                {
                    continue;
                }

                junctionTrafficById[traffic.junctionId] = traffic;
            }

            for (int i = 0; i < connectorTraffic.Count; i++)
            {
                BakedConnectorTrafficRecord traffic = connectorTraffic[i];
                if (traffic == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(traffic.connectorLaneId))
                {
                    connectorTrafficByLaneId[traffic.connectorLaneId] = traffic;
                }

                if (!string.IsNullOrWhiteSpace(traffic.connectionId))
                {
                    connectorTrafficByConnectionId[traffic.connectionId] = traffic;
                }
            }

            for (int i = 0; i < polygons.Count; i++)
            {
                BakedPolygonRecord polygon = polygons[i];
                if (polygon == null || string.IsNullOrWhiteSpace(polygon.zoneId))
                {
                    continue;
                }

                polygonById[polygon.zoneId] = polygon;
                AddPolygonToSpatialCells(i, polygon.bounds);
            }

            for (int i = 0; i < portals.Count; i++)
            {
                BakedPortalRecord portal = portals[i];
                if (portal == null || string.IsNullOrWhiteSpace(portal.portalId))
                {
                    continue;
                }

                portalById[portal.portalId] = portal;
                AddLookup(portalsBySourceZone, portal.sourceZoneId, portal);
                AddLookup(portalsByTargetElement, portal.targetElementId, portal);
            }
        }

        private static void AddLookup(
            Dictionary<string, List<BakedPortalRecord>> lookup,
            string key,
            BakedPortalRecord portal)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!lookup.TryGetValue(key, out List<BakedPortalRecord> list))
            {
                list = new List<BakedPortalRecord>();
                lookup.Add(key, list);
            }

            list.Add(portal);
        }

        private void AddPolygonToSpatialCells(int polygonIndex, Bounds bounds)
        {
            Vector3Int minCell = ToCell(bounds.min);
            Vector3Int maxCell = ToCell(bounds.max);
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    for (int z = minCell.z; z <= maxCell.z; z++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, z);
                        if (!polygonSpatialCells.TryGetValue(cell, out List<int> list))
                        {
                            list = new List<int>();
                            polygonSpatialCells.Add(cell, list);
                        }

                        list.Add(polygonIndex);
                    }
                }
            }
        }

        private void AddSegmentToSpatialCells(
            int laneIndex,
            int sampleIndex,
            Vector3 a,
            Vector3 b,
            float lateralExpansion)
        {
            Vector3 min = Vector3.Min(a, b);
            Vector3 max = Vector3.Max(a, b);
            Vector3 expansion = new Vector3(
                Mathf.Max(0f, lateralExpansion),
                0.1f,
                Mathf.Max(0f, lateralExpansion));
            min -= expansion;
            max += expansion;
            Vector3Int minCell = ToCell(min);
            Vector3Int maxCell = ToCell(max);
            SegmentRef segment = new SegmentRef(laneIndex, sampleIndex);
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    for (int z = minCell.z; z <= maxCell.z; z++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, z);
                        if (!spatialCells.TryGetValue(cell, out List<SegmentRef> list))
                        {
                            list = new List<SegmentRef>();
                            spatialCells.Add(cell, list);
                        }

                        list.Add(segment);
                    }
                }
            }
        }

        private Vector3Int ToCell(Vector3 position)
        {
            return new Vector3Int(
                Mathf.FloorToInt(position.x / spatialCellSize),
                Mathf.FloorToInt(position.y / spatialCellSize),
                Mathf.FloorToInt(position.z / spatialCellSize));
        }

        private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b, out float t)
        {
            Vector3 delta = b - a;
            float denominator = delta.sqrMagnitude;
            t = denominator <= 0.000001f ? 0f : Mathf.Clamp01(Vector3.Dot(point - a, delta) / denominator);
            return a + delta * t;
        }

        private static RoadAgentMask NormalizeAgentMask(RoadAgentMask agentMask)
        {
            return agentMask == RoadAgentMask.None ? RoadAgentMask.MotorVehicles : agentMask;
        }

        private readonly struct SegmentRef
        {
            public readonly int laneIndex;
            public readonly int sampleIndex;

            public SegmentRef(int laneIndex, int sampleIndex)
            {
                this.laneIndex = laneIndex;
                this.sampleIndex = sampleIndex;
            }
        }
    }
}
