using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace VehicleRoads
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vehicle Road/Road Lane Network")]
    public sealed class RoadLaneNetwork : MonoBehaviour
    {
        public const string SchemaVersion = BakedLaneNetwork.CurrentSchemaVersion;
        public const float DefaultPreviewLineWidth = 6f;
        private const float MinimumSpacing = 0.1f;
        private const float MinimumPreviewLineWidth = 1f;
        private const float MaximumPreviewLineWidth = 20f;
        private const float MinimumAdjacentLateralDistance = 0.1f;
        private const float MinimumAdjacentOverlapLength = 0.1f;
        private const float ConnectorConflictProbeStepMin = 0.25f;
        private const float ConnectorConflictProbeStepMax = 1f;

        [SerializeField, Min(MinimumSpacing)] private float sampleSpacing = 1f;
        [SerializeField, Min(0f)] private float connectionRadius = 0.25f;
        [SerializeField, Range(0f, 180f)] private float connectionDirectionTolerance = 20f;
        [SerializeField, Min(0.1f)] private float minimumTurnRadius = 4f;
        [Header("Adjacent Lane Inference")]
        [SerializeField, Min(MinimumAdjacentLateralDistance)] private float adjacentMinLateralDistance = 1.5f;
        [SerializeField, Min(MinimumAdjacentLateralDistance)] private float adjacentMaxLateralDistance = 5f;
        [SerializeField, Range(0f, 90f)] private float adjacentHeadingTolerance = 15f;
        [SerializeField, Min(0f)] private float adjacentMaxHeightDifference = 1f;
        [SerializeField, Min(MinimumAdjacentOverlapLength)] private float adjacentMinimumOverlapLength = 8f;
        [SerializeField, Range(MinimumPreviewLineWidth, MaximumPreviewLineWidth)]
        private float previewLineWidth = DefaultPreviewLineWidth;
        [SerializeField] private string outputAssetPath = "Assets/VehicleRoads/Generated/VehicleRoadNetwork";
        [SerializeField] private RoadNetworkSettings networkSettings;
        [SerializeField] private RoadNetworkRuntimeSettings runtimeSettings;
        [SerializeField] private BakedLaneNetwork bakedNetwork;

        private readonly UnitySplineRoadLaneGeometry geometry = new UnitySplineRoadLaneGeometry();

        public float SampleSpacing
        {
            get => Mathf.Max(MinimumSpacing, sampleSpacing);
            set => sampleSpacing = Mathf.Max(MinimumSpacing, value);
        }

        public float ConnectionRadius
        {
            get => Mathf.Max(0f, connectionRadius);
            set => connectionRadius = Mathf.Max(0f, value);
        }

        public float ConnectionDirectionTolerance
        {
            get => Mathf.Clamp(connectionDirectionTolerance, 0f, 180f);
            set => connectionDirectionTolerance = Mathf.Clamp(value, 0f, 180f);
        }

        public float MinimumTurnRadius
        {
            get => Mathf.Max(0.1f, minimumTurnRadius);
            set => minimumTurnRadius = Mathf.Max(0.1f, value);
        }

        public float AdjacentMinLateralDistance
        {
            get => Mathf.Max(MinimumAdjacentLateralDistance, adjacentMinLateralDistance);
            set => adjacentMinLateralDistance = Mathf.Max(MinimumAdjacentLateralDistance, value);
        }

        public float AdjacentMaxLateralDistance
        {
            get => Mathf.Max(AdjacentMinLateralDistance, adjacentMaxLateralDistance);
            set => adjacentMaxLateralDistance = Mathf.Max(AdjacentMinLateralDistance, value);
        }

        public float AdjacentHeadingTolerance
        {
            get => Mathf.Clamp(adjacentHeadingTolerance, 0f, 90f);
            set => adjacentHeadingTolerance = Mathf.Clamp(value, 0f, 90f);
        }

        public float AdjacentMaxHeightDifference
        {
            get => Mathf.Max(0f, adjacentMaxHeightDifference);
            set => adjacentMaxHeightDifference = Mathf.Max(0f, value);
        }

        public float AdjacentMinimumOverlapLength
        {
            get => Mathf.Max(MinimumAdjacentOverlapLength, adjacentMinimumOverlapLength);
            set => adjacentMinimumOverlapLength = Mathf.Max(MinimumAdjacentOverlapLength, value);
        }

        public float PreviewLineWidth
        {
            get => Mathf.Clamp(previewLineWidth, MinimumPreviewLineWidth, MaximumPreviewLineWidth);
            set => previewLineWidth = Mathf.Clamp(value, MinimumPreviewLineWidth, MaximumPreviewLineWidth);
        }

        public string OutputAssetPath
        {
            get => outputAssetPath ?? string.Empty;
            set => outputAssetPath = value ?? string.Empty;
        }

        public RoadNetworkSettings NetworkSettings
        {
            get => networkSettings;
            set => networkSettings = value;
        }

        public RoadNetworkRuntimeSettings RuntimeSettings
        {
            get => runtimeSettings;
            set => runtimeSettings = value;
        }

        public BakedLaneNetwork BakedNetwork
        {
            get => bakedNetwork;
            set => bakedNetwork = value;
        }

        public RoadLane[] GetAuthoredLanes()
        {
            return GetComponentsInChildren<RoadLane>(true);
        }

        public RoadJunction[] GetJunctions()
        {
            return GetComponentsInChildren<RoadJunction>(true);
        }

        public RoadPolygonZone[] GetPolygonZones()
        {
            return GetComponentsInChildren<RoadPolygonZone>(true);
        }

        public RoadLaneProfileSource[] GetProfileSources()
        {
            return GetComponentsInChildren<RoadLaneProfileSource>(true);
        }

        public void ResetOutputPathToScene()
        {
            Scene scene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
            string sceneName = scene.IsValid() && !string.IsNullOrWhiteSpace(scene.name) ? scene.name : "VehicleRoadNetwork";
            outputAssetPath = "Assets/VehicleRoads/Generated/" + SanitizeId(sceneName) + ".RoadLanes";
        }

        public RoadLaneConnectorReport GenerateConnectors(Action<GameObject> registerCreatedObject = null)
        {
            RoadLaneConnectorReport report = new RoadLaneConnectorReport();
            RoadLane[] authoredLanes = GetAuthoredLanes();
            Dictionary<string, RoadLane> existingByKey = authoredLanes
                .Where(lane => lane != null &&
                               lane.Kind == RoadLaneKind.Connector &&
                               !string.IsNullOrWhiteSpace(lane.ConnectorGenerationKey))
                .GroupBy(lane => lane.ConnectorGenerationKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            HashSet<string> validKeys = new HashSet<string>(StringComparer.Ordinal);

            RoadJunction[] roadJunctions = GetJunctions();
            for (int junctionIndex = 0; junctionIndex < roadJunctions.Length; junctionIndex++)
            {
                RoadJunction junction = roadJunctions[junctionIndex];
                if (junction == null)
                {
                    continue;
                }

                GenerateConnectorsForJunction(
                    junction,
                    existingByKey,
                    validKeys,
                    report,
                    registerCreatedObject);
            }

            foreach (KeyValuePair<string, RoadLane> pair in existingByKey)
            {
                if (!validKeys.Contains(pair.Key))
                {
                    pair.Value.MarkConnectorOrphaned();
                    report.orphaned++;
                }
            }

            return report;
        }

        public RoadLaneConnectorReport RefreshConnectors(
            RoadJunction junction,
            Action<GameObject> registerCreatedObject = null)
        {
            RoadLaneConnectorReport report = new RoadLaneConnectorReport();
            if (junction == null)
            {
                return report;
            }

            Dictionary<string, RoadLane> existingByKey = junction
                .GetComponentsInChildren<RoadLane>(true)
                .Where(lane => lane != null &&
                               lane.Kind == RoadLaneKind.Connector &&
                               !string.IsNullOrWhiteSpace(lane.ConnectorGenerationKey))
                .GroupBy(lane => lane.ConnectorGenerationKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            HashSet<string> validKeys = new HashSet<string>(StringComparer.Ordinal);

            GenerateConnectorsForJunction(
                junction,
                existingByKey,
                validKeys,
                report,
                registerCreatedObject);

            foreach (KeyValuePair<string, RoadLane> pair in existingByKey)
            {
                if (!validKeys.Contains(pair.Key))
                {
                    pair.Value.MarkConnectorOrphaned();
                    report.orphaned++;
                }
            }

            return report;
        }

        private void GenerateConnectorsForJunction(
            RoadJunction junction,
            Dictionary<string, RoadLane> existingByKey,
            HashSet<string> validKeys,
            RoadLaneConnectorReport report,
            Action<GameObject> registerCreatedObject)
        {
            List<DirectedEndpoint> incoming = new List<DirectedEndpoint>();
            List<DirectedEndpoint> outgoing = new List<DirectedEndpoint>();
            BuildDirectedEndpoints(junction, incoming, outgoing);
            for (int sourceIndex = 0; sourceIndex < incoming.Count; sourceIndex++)
            {
                for (int targetIndex = 0; targetIndex < outgoing.Count; targetIndex++)
                {
                    DirectedEndpoint source = incoming[sourceIndex];
                    DirectedEndpoint target = outgoing[targetIndex];
                    if (source.runtimeLaneId == target.runtimeLaneId)
                    {
                        continue;
                    }

                    RoadLaneTurn turn = ClassifyTurn(source.pose.forward, target.pose.forward, source.pose.up);
                    if (!AllowsTurn(junction.AllowedTurns, turn))
                    {
                        continue;
                    }

                    string stableKey = string.Join(
                        "|",
                        SanitizeId(junction.JunctionId),
                        source.runtimeLaneId,
                        target.runtimeLaneId,
                        turn.ToString());
                    validKeys.Add(stableKey);
                    if (existingByKey.TryGetValue(stableKey, out RoadLane existing))
                    {
                        if (!existing.ConnectorLocked)
                        {
                            UpdateConnector(existing, stableKey, source, target, turn, junction);
                            report.updated++;
                        }
                        else
                        {
                            report.locked++;
                        }

                        continue;
                    }

                    GameObject connectorObject = new GameObject(CreateConnectorId(stableKey));
                    connectorObject.transform.SetParent(junction.transform, true);
                    registerCreatedObject?.Invoke(connectorObject);
                    connectorObject.AddComponent<SplineContainer>();
                    RoadLane connector = connectorObject.AddComponent<RoadLane>();
                    UpdateConnector(connector, stableKey, source, target, turn, junction);
                    existingByKey.Add(stableKey, connector);
                    report.created++;
                }
            }
        }

        public BakedLaneNetwork BakeNetwork()
        {
            return BuildTransientNetwork(true);
        }

        public BakedLaneNetwork BuildTransientNetwork(bool refreshProfileSources = true)
        {
            RoadNetworkProfiler.Configure(runtimeSettings);
            if (refreshProfileSources)
            {
                RefreshProfileSourcesBeforeBake();
            }
            using RoadNetworkProfiler.Scope bakeScope =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.BakeLanes);
            RoadLane[] authored = GetAuthoredLanes();
            List<BakedLaneRecord> laneRecords = new List<BakedLaneRecord>();
            List<BakedLaneSampleRecord> sampleRecords = new List<BakedLaneSampleRecord>();
            List<BakedLaneConnectionRecord> connectionRecords = new List<BakedLaneConnectionRecord>();
            List<BakedLaneAdjacentLinkRecord> adjacentLinkRecords = new List<BakedLaneAdjacentLinkRecord>();
            List<BakedJunctionTrafficRecord> junctionTrafficRecords = new List<BakedJunctionTrafficRecord>();
            List<BakedConnectorTrafficRecord> connectorTrafficRecords = new List<BakedConnectorTrafficRecord>();
            List<BakedPolygonRecord> polygonRecords = new List<BakedPolygonRecord>();
            List<BakedPortalRecord> portalRecords = new List<BakedPortalRecord>();
            Dictionary<string, DirectedLaneBuildState> stateById = new Dictionary<string, DirectedLaneBuildState>(StringComparer.Ordinal);
            HashSet<string> authoredIds = new HashSet<string>(StringComparer.Ordinal);
            int invalidLaneCount = 0;
            int invalidPolygonCount = 0;

            for (int i = 0; i < authored.Length; i++)
            {
                RoadLane lane = authored[i];
                if (lane == null || string.IsNullOrWhiteSpace(lane.LaneId) || !authoredIds.Add(lane.LaneId))
                {
                    invalidLaneCount++;
                    continue;
                }

                if (lane.Kind == RoadLaneKind.Connector ||
                    lane.TravelDirection == RoadLaneTravelDirection.Forward ||
                    lane.TravelDirection == RoadLaneTravelDirection.Bidirectional)
                {
                    AddDirectedLane(lane, false, laneRecords, sampleRecords, stateById, ref invalidLaneCount);
                }

                if (lane.Kind != RoadLaneKind.Connector &&
                    (lane.TravelDirection == RoadLaneTravelDirection.Reverse ||
                     lane.TravelDirection == RoadLaneTravelDirection.Bidirectional))
                {
                    AddDirectedLane(lane, true, laneRecords, sampleRecords, stateById, ref invalidLaneCount);
                }
            }

            bakedScratchSamples = sampleRecords;
            BuildConnections(stateById, connectionRecords);
            AssignConnectionSampleIds(stateById, connectionRecords, sampleRecords);
            BuildAdjacentLinks(laneRecords, sampleRecords, adjacentLinkRecords);
            BuildTrafficRecords(stateById, connectionRecords, junctionTrafficRecords, connectorTrafficRecords);
            BuildPolygonAndPortalRecords(
                stateById,
                polygonRecords,
                portalRecords,
                ref invalidPolygonCount);
            bakedScratchSamples = null;
            BakedLaneSummary summary = new BakedLaneSummary
            {
                authoredLaneCount = authored.Length,
                directedLaneCount = laneRecords.Count,
                connectorLaneCount = laneRecords.Count(item => item.kind == RoadLaneKind.Connector),
                sampleCount = sampleRecords.Count,
                connectionCount = connectionRecords.Count,
                adjacentLinkCount = adjacentLinkRecords.Count,
                laneChangeLinkCount = adjacentLinkRecords.Count(item =>
                    item != null &&
                    item.open &&
                    (item.flags & RoadLaneAdjacentFlags.LaneChangeAllowed) != 0),
                junctionTrafficCount = junctionTrafficRecords.Count,
                connectorTrafficCount = connectorTrafficRecords.Count,
                polygonCount = polygonRecords.Count,
                portalCount = portalRecords.Count,
                polygonTriangleCount = polygonRecords.Sum(item => item == null ? 0 : item.triangles.Count / 3),
                invalidLaneCount = invalidLaneCount,
                invalidPolygonCount = invalidPolygonCount,
                invalidSampleCount = sampleRecords.Count(item => !item.valid)
            };

            BakedLaneNetwork network = ScriptableObject.CreateInstance<BakedLaneNetwork>();
            Scene scene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
            network.SetData(
                scene.IsValid() ? scene.path : string.Empty,
                SampleSpacing,
                summary,
                laneRecords,
                sampleRecords,
                connectionRecords,
                adjacentLinkRecords,
                junctionTrafficRecords,
                connectorTrafficRecords,
                polygonRecords,
                portalRecords);
            network.SetSettings(networkSettings, runtimeSettings);
            return network;
        }

        public List<RoadLaneValidationIssue> ValidateNetwork()
        {
            List<RoadLaneValidationIssue> issues = new List<RoadLaneValidationIssue>();
            RoadLane[] authored = GetAuthoredLanes();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < authored.Length; i++)
            {
                RoadLane lane = authored[i];
                if (lane == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(lane.LaneId))
                {
                    issues.Add(new RoadLaneValidationIssue("MissingLaneId", lane, "Lane ID is empty."));
                }
                else if (!ids.Add(lane.LaneId))
                {
                    issues.Add(new RoadLaneValidationIssue("DuplicateLaneId", lane, "Lane ID is not unique: " + lane.LaneId));
                }

                if (lane.Spline == null || lane.Spline.Count < 2)
                {
                    issues.Add(new RoadLaneValidationIssue("TooFewKnots", lane, "Spline must contain at least two knots."));
                    continue;
                }

                float length = geometry.GetLength(lane);
                if (!float.IsFinite(length) || length <= 0.01f)
                {
                    issues.Add(new RoadLaneValidationIssue("InvalidLength", lane, "Spline length is zero or invalid."));
                    continue;
                }

                List<RoadLanePose> samples = geometry.SampleEqualDistance(lane, Mathf.Min(1f, SampleSpacing), false);
                for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
                {
                    RoadLanePose pose = samples[sampleIndex];
                    if (!UnitySplineRoadLaneGeometry.IsFinite(pose.position) ||
                        !UnitySplineRoadLaneGeometry.IsFinite(pose.forward) ||
                        !UnitySplineRoadLaneGeometry.IsFinite(pose.up))
                    {
                        issues.Add(new RoadLaneValidationIssue("InvalidNumber", lane, "Spline contains a non-finite position or frame."));
                        break;
                    }

                    if (pose.curvature > 0.00001f && 1f / pose.curvature < MinimumTurnRadius)
                    {
                        issues.Add(new RoadLaneValidationIssue(
                            "TurnRadiusTooSmall",
                            lane,
                            "Curve radius is below " + MinimumTurnRadius.ToString("0.###", CultureInfo.InvariantCulture) + " m."));
                        break;
                    }
                }

                if (!TryValidateLaneBoundaryRibbon(samples, lane, false, out string boundaryError))
                {
                    issues.Add(new RoadLaneValidationIssue(
                        "InvalidLaneBoundary",
                        lane,
                        boundaryError));
                }

                if (lane.Kind == RoadLaneKind.Connector)
                {
                    if (lane.Orphaned ||
                        string.IsNullOrWhiteSpace(lane.SourceLaneId) ||
                        string.IsNullOrWhiteSpace(lane.TargetLaneId))
                    {
                        issues.Add(new RoadLaneValidationIssue("InvalidConnector", lane, "Connector is orphaned or missing endpoint IDs."));
                    }
                }

                if (lane.Width <= 0.1f)
                {
                    issues.Add(new RoadLaneValidationIssue("InvalidLaneWidth", lane, "Lane width must be greater than 0.1 m."));
                }
            }

            RoadPolygonZone[] zones = GetPolygonZones();
            HashSet<string> zoneIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < zones.Length; i++)
            {
                RoadPolygonZone zone = zones[i];
                if (zone == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(zone.ZoneId) || !zoneIds.Add(zone.ZoneId))
                {
                    issues.Add(new RoadLaneValidationIssue(
                        "InvalidPolygonId",
                        null,
                        "Polygon Zone ID is empty or duplicated: " + zone.ZoneId));
                    continue;
                }

                List<int> triangles = new List<int>();
                if (!RoadPolygonGeometry.TryTriangulate(zone.Vertices, triangles, out string polygonError))
                {
                    issues.Add(new RoadLaneValidationIssue("InvalidPolygon", null, zone.ZoneId + ": " + polygonError));
                }

                RoadPortal[] zonePortals = zone.GetPortals();
                HashSet<string> portalIds = new HashSet<string>(StringComparer.Ordinal);
                for (int portalIndex = 0; portalIndex < zonePortals.Length; portalIndex++)
                {
                    RoadPortal portal = zonePortals[portalIndex];
                    if (portal == null ||
                        string.IsNullOrWhiteSpace(portal.PortalId) ||
                        !portalIds.Add(portal.PortalId) ||
                        portal.LinkedLane == null && portal.LinkedPortal == null)
                    {
                        issues.Add(new RoadLaneValidationIssue(
                            "InvalidPortal",
                            null,
                            zone.ZoneId + " contains an invalid or unlinked Portal."));
                    }
                }
            }

            return issues;
        }

        private void RefreshProfileSourcesBeforeBake()
        {
            RoadLaneProfileSource[] sources = GetProfileSources();
            for (int i = 0; i < sources.Length; i++)
            {
                RoadLaneProfileSource source = sources[i];
                if (source == null || !source.RefreshBeforeBake)
                {
                    continue;
                }

                if (!source.RefreshManagedLanes(null, null, out string error))
                {
                    Debug.LogError(
                        "RoadLaneNetwork failed to refresh Lane Profile Source '" +
                        source.name + "': " + error,
                        source);
                }
            }
        }

        private void BuildPolygonAndPortalRecords(
            Dictionary<string, DirectedLaneBuildState> stateById,
            List<BakedPolygonRecord> polygonOutput,
            List<BakedPortalRecord> portalOutput,
            ref int invalidPolygonCount)
        {
            using RoadNetworkProfiler.Scope ignored =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.TriangulatePolygons);
            RoadPolygonZone[] zones = GetPolygonZones();
            Dictionary<RoadPolygonZone, BakedPolygonRecord> recordsByZone =
                new Dictionary<RoadPolygonZone, BakedPolygonRecord>();
            HashSet<string> zoneIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < zones.Length; i++)
            {
                RoadPolygonZone zone = zones[i];
                string zoneId = zone == null ? string.Empty : SanitizeId(zone.ZoneId);
                if (zone == null || string.IsNullOrWhiteSpace(zoneId) || !zoneIds.Add(zoneId))
                {
                    invalidPolygonCount++;
                    continue;
                }

                IReadOnlyList<Vector2> localVertices = zone.Vertices;
                List<int> triangles = new List<int>();
                if (localVertices == null ||
                    !RoadPolygonGeometry.TryTriangulate(localVertices, triangles, out _))
                {
                    invalidPolygonCount++;
                    continue;
                }

                BakedPolygonRecord record = new BakedPolygonRecord
                {
                    zoneId = zoneId,
                    open = zone.Open,
                    traversalCost = zone.TraversalCost,
                    tagMask = zone.Tags,
                    allowedAgents = zone.AllowedAgents,
                    triangles = triangles
                };
                bool hasBounds = false;
                Bounds bounds = default;
                float minHeight = float.PositiveInfinity;
                float maxHeight = float.NegativeInfinity;
                for (int vertexIndex = 0; vertexIndex < localVertices.Count; vertexIndex++)
                {
                    Vector3 minimum = zone.LocalVertexToWorld(localVertices[vertexIndex]);
                    Vector3 maximum = zone.LocalVertexToWorld(localVertices[vertexIndex], zone.Height);
                    record.vertices.Add(minimum);
                    minHeight = Mathf.Min(minHeight, minimum.y, maximum.y);
                    maxHeight = Mathf.Max(maxHeight, minimum.y, maximum.y);
                    if (!hasBounds)
                    {
                        bounds = new Bounds(minimum, Vector3.zero);
                        hasBounds = true;
                    }

                    bounds.Encapsulate(minimum);
                    bounds.Encapsulate(maximum);
                }

                record.minimumWorldHeight = minHeight;
                record.maximumWorldHeight = maxHeight;
                record.bounds = bounds;
                polygonOutput.Add(record);
                recordsByZone.Add(zone, record);
            }

            using RoadNetworkProfiler.Scope portalScope =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.BuildPortalPaths);
            HashSet<string> portalIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<RoadPolygonZone, BakedPolygonRecord> pair in recordsByZone)
            {
                RoadPortal[] authoredPortals = pair.Key.GetPortals();
                for (int i = 0; i < authoredPortals.Length; i++)
                {
                    RoadPortal portal = authoredPortals[i];
                    if (!TryBuildPortalRecord(
                            pair.Key,
                            pair.Value,
                            portal,
                            stateById,
                            recordsByZone,
                            portalIds,
                            out BakedPortalRecord record))
                    {
                        invalidPolygonCount++;
                        continue;
                    }

                    portalOutput.Add(record);
                }
            }
        }

        private bool TryBuildPortalRecord(
            RoadPolygonZone sourceZone,
            BakedPolygonRecord sourceRecord,
            RoadPortal portal,
            Dictionary<string, DirectedLaneBuildState> stateById,
            Dictionary<RoadPolygonZone, BakedPolygonRecord> recordsByZone,
            HashSet<string> portalIds,
            out BakedPortalRecord record)
        {
            record = null;
            if (portal == null || string.IsNullOrWhiteSpace(portal.PortalId))
            {
                return false;
            }

            string portalId = SanitizeId(sourceRecord.zoneId + "_" + portal.PortalId);
            if (!portalIds.Add(portalId))
            {
                return false;
            }

            Vector2 localPosition = sourceZone.WorldToLocalXZ(portal.transform.position);
            IReadOnlyList<Vector2> localVertices = sourceZone.Vertices;
            if (localVertices == null)
            {
                return false;
            }

            RoadPolygonGeometry.ClosestPointOnBoundary(
                localVertices,
                localPosition,
                out float boundaryDistance);
            if (boundaryDistance > 0.5f)
            {
                return false;
            }

            record = new BakedPortalRecord
            {
                portalId = portalId,
                sourceZoneId = sourceRecord.zoneId,
                direction = portal.Direction,
                open = portal.Open && sourceRecord.open,
                width = portal.Width,
                traversalCost = portal.TraversalCost,
                tagMask = portal.Tags == RoadTagMask.None ? sourceRecord.tagMask : portal.Tags,
                allowedAgents = portal.AllowedAgents & sourceRecord.allowedAgents,
                sourcePosition = portal.transform.position
            };

            if (portal.LinkedLane != null)
            {
                string laneId = GetRuntimeLaneId(portal.LinkedLane, portal.LinkedLaneReverse);
                if (!stateById.TryGetValue(laneId, out DirectedLaneBuildState laneState))
                {
                    return false;
                }

                record.targetKind = laneState.record.kind == RoadLaneKind.Connector
                    ? RoadElementKind.Connector
                    : RoadElementKind.Lane;
                record.targetElementId = laneId;
                bool zeroAtAuthoredStart =
                    portal.LinkedLaneEndpoint == RoadLaneEndpoint.Start &&
                    !portal.LinkedLaneReverse ||
                    portal.LinkedLaneEndpoint == RoadLaneEndpoint.End &&
                    portal.LinkedLaneReverse;
                record.targetLaneDistance = zeroAtAuthoredStart ? 0f : laneState.record.length;
                if (!TryGetScratchLanePose(laneState.record, record.targetLaneDistance, out RoadLanePose pose))
                {
                    return false;
                }

                record.targetPosition = pose.position;
                record.allowedAgents &= laneState.record.allowedAgents;
                return true;
            }

            RoadPortal linkedPortal = portal.LinkedPortal;
            RoadPolygonZone targetZone = linkedPortal == null ? null : linkedPortal.SourceZone;
            if (linkedPortal == null ||
                targetZone == null ||
                !recordsByZone.TryGetValue(targetZone, out BakedPolygonRecord targetRecord))
            {
                return false;
            }

            record.targetKind = RoadElementKind.Polygon;
            record.targetElementId = targetRecord.zoneId;
            record.targetPosition = linkedPortal.transform.position;
            record.allowedAgents &= targetRecord.allowedAgents & linkedPortal.AllowedAgents;
            record.width = Mathf.Min(record.width, linkedPortal.Width);
            record.open &= linkedPortal.Open && targetRecord.open;
            return true;
        }

        private bool TryGetScratchLanePose(BakedLaneRecord lane, float distance, out RoadLanePose pose)
        {
            pose = default;
            if (lane == null || bakedScratchSamples == null || lane.sampleCount <= 0)
            {
                return false;
            }

            distance = Mathf.Clamp(distance, 0f, lane.length);
            int first = lane.firstSampleIndex;
            int last = first + lane.sampleCount - 1;
            int right = last;
            for (int i = first + 1; i <= last; i++)
            {
                if (bakedScratchSamples[i].distanceAlongLane >= distance)
                {
                    right = i;
                    break;
                }
            }

            int left = Mathf.Max(first, right - 1);
            BakedLaneSampleRecord a = bakedScratchSamples[left];
            BakedLaneSampleRecord b = bakedScratchSamples[right];
            float range = b.distanceAlongLane - a.distanceAlongLane;
            float t = range <= 0.0001f ? 0f : Mathf.Clamp01((distance - a.distanceAlongLane) / range);
            pose.position = Vector3.Lerp(a.finalPosition, b.finalPosition, t);
            pose.splinePosition = Vector3.Lerp(a.splinePosition, b.splinePosition, t);
            pose.forward = Vector3.Slerp(a.forward, b.forward, t).normalized;
            pose.up = Vector3.Slerp(a.up, b.up, t).normalized;
            pose.curvature = Mathf.Lerp(a.curvature, b.curvature, t);
            pose.distance = distance;
            pose.normalizedT = lane.length <= 0.0001f ? 0f : distance / lane.length;
            return true;
        }

        private void AddDirectedLane(
            RoadLane lane,
            bool reverse,
            List<BakedLaneRecord> laneRecords,
            List<BakedLaneSampleRecord> sampleRecords,
            Dictionary<string, DirectedLaneBuildState> stateById,
            ref int invalidLaneCount)
        {
            string runtimeLaneId = GetRuntimeLaneId(lane, reverse);
            if (stateById.ContainsKey(runtimeLaneId))
            {
                invalidLaneCount++;
                return;
            }

            float spacing = lane.SampleSpacingOverride > 0f ? lane.SampleSpacingOverride : SampleSpacing;
            List<RoadLanePose> poses = geometry.SampleEqualDistance(lane, spacing, reverse);
            if (poses.Count < 2 ||
                !TryValidateLaneBoundaryRibbon(poses, lane, reverse, out _))
            {
                invalidLaneCount++;
                return;
            }

            int firstSample = sampleRecords.Count;
            Bounds laneBounds = default;
            bool hasBounds = false;
            using RoadNetworkProfiler.Scope boundaryScope =
                RoadNetworkProfiler.Sample(RoadNetworkProfiler.BakeBoundaries);
            for (int i = 0; i < poses.Count; i++)
            {
                RoadLanePose pose = poses[i];
                Vector3 right = Vector3.Cross(
                    pose.up.sqrMagnitude > 0.0001f ? pose.up.normalized : Vector3.up,
                    pose.forward.sqrMagnitude > 0.0001f ? pose.forward.normalized : Vector3.forward);
                right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
                float normalizedDistance = poses.Count <= 1
                    ? 0f
                    : pose.distance / Mathf.Max(0.0001f, poses[poses.Count - 1].distance);
                if (reverse)
                {
                    normalizedDistance = 1f - normalizedDistance;
                }

                float sampleWidth = lane.EvaluateWidth(normalizedDistance);
                float halfWidth = sampleWidth * 0.5f;
                Vector3 leftBoundary = pose.position - right * halfWidth;
                Vector3 rightBoundary = pose.position + right * halfWidth;
                string sampleId = runtimeLaneId + "_s_" + i.ToString("D5", CultureInfo.InvariantCulture);
                sampleRecords.Add(new BakedLaneSampleRecord
                {
                    sampleId = sampleId,
                    laneId = runtimeLaneId,
                    order = i,
                    splinePosition = pose.splinePosition,
                    finalPosition = pose.position,
                    leftBoundary = leftBoundary,
                    rightBoundary = rightBoundary,
                    forward = pose.forward,
                    up = pose.up,
                    curvature = pose.curvature,
                    distanceAlongLane = pose.distance,
                    width = sampleWidth,
                    previousSampleId = i == 0 ? string.Empty : runtimeLaneId + "_s_" + (i - 1).ToString("D5", CultureInfo.InvariantCulture),
                    nextSampleId = i + 1 == poses.Count ? string.Empty : runtimeLaneId + "_s_" + (i + 1).ToString("D5", CultureInfo.InvariantCulture),
                    lateralOffset = pose.lateralOffset,
                    verticalOffset = pose.verticalOffset,
                    valid = UnitySplineRoadLaneGeometry.IsFinite(pose.position),
                    errorReason = UnitySplineRoadLaneGeometry.IsFinite(pose.position) ? string.Empty : "InvalidSplineSample"
                });
                if (!hasBounds)
                {
                    laneBounds = new Bounds(leftBoundary, Vector3.zero);
                    hasBounds = true;
                }

                laneBounds.Encapsulate(leftBoundary);
                laneBounds.Encapsulate(rightBoundary);
            }

            BakedLaneRecord record = new BakedLaneRecord
            {
                laneId = runtimeLaneId,
                sourceLaneId = lane.LaneId,
                kind = lane.Kind,
                direction = reverse ? RoadLaneTravelDirection.Reverse : RoadLaneTravelDirection.Forward,
                turnType = lane.TurnType,
                open = lane.Open && lane.ConnectionMode != RoadLaneConnectionMode.Blocked,
                orphaned = lane.Orphaned,
                length = poses[poses.Count - 1].distance,
                speedLimit = lane.SpeedLimit,
                width = sampleRecords
                    .Skip(firstSample)
                    .Take(poses.Count)
                    .Average(sample => sample.width),
                minimumWidth = sampleRecords
                    .Skip(firstSample)
                    .Take(poses.Count)
                    .Min(sample => sample.width),
                maximumWidth = sampleRecords
                    .Skip(firstSample)
                    .Take(poses.Count)
                    .Max(sample => sample.width),
                tagMask = lane.TagMask,
                allowedAgents = lane.AllowedAgents,
                allowLaneChangeLeft = lane.AllowLaneChangeLeft,
                allowLaneChangeRight = lane.AllowLaneChangeRight,
                bounds = laneBounds,
                lateralOffset = lane.LateralOffset,
                verticalOffset = lane.VerticalOffset,
                firstSampleIndex = firstSample,
                sampleCount = poses.Count,
                connectorSourceLaneId = lane.SourceLaneId,
                connectorTargetLaneId = lane.TargetLaneId,
                connectorJunctionId = lane.ConnectorJunctionId
            };
            laneRecords.Add(record);
            stateById.Add(runtimeLaneId, new DirectedLaneBuildState(lane, record, reverse));
        }

        private static bool TryValidateLaneBoundaryRibbon(
            IReadOnlyList<RoadLanePose> poses,
            float width,
            out string error)
        {
            error = string.Empty;
            if (poses == null || poses.Count < 2 || width <= 0.1f)
            {
                error = "Lane boundary requires at least two valid poses and positive width.";
                return false;
            }

            float halfWidth = width * 0.5f;
            List<Vector2> ribbon = new List<Vector2>(poses.Count * 2);
            List<Vector2> rightBoundary = new List<Vector2>(poses.Count);
            for (int i = 0; i < poses.Count; i++)
            {
                RoadLanePose pose = poses[i];
                Vector3 forward = pose.forward.sqrMagnitude > 0.0001f
                    ? pose.forward.normalized
                    : Vector3.forward;
                Vector3 up = pose.up.sqrMagnitude > 0.0001f ? pose.up.normalized : Vector3.up;
                Vector3 right = Vector3.Cross(up, forward);
                if (!UnitySplineRoadLaneGeometry.IsFinite(right) || right.sqrMagnitude <= 0.0001f)
                {
                    error = "Lane frame cannot produce a stable left/right boundary.";
                    return false;
                }

                right.Normalize();
                Vector3 left = pose.position - right * halfWidth;
                Vector3 rightPoint = pose.position + right * halfWidth;
                if (!UnitySplineRoadLaneGeometry.IsFinite(left) ||
                    !UnitySplineRoadLaneGeometry.IsFinite(rightPoint) ||
                    Vector3.Distance(left, rightPoint) < width * 0.95f)
                {
                    error = "Lane boundary has insufficient effective width.";
                    return false;
                }

                ribbon.Add(new Vector2(left.x, left.z));
                rightBoundary.Add(new Vector2(rightPoint.x, rightPoint.z));
            }

            for (int i = rightBoundary.Count - 1; i >= 0; i--)
            {
                ribbon.Add(rightBoundary[i]);
            }

            if (RoadPolygonGeometry.HasSelfIntersection(ribbon))
            {
                error = "Lane boundary ribbon flips or self-intersects.";
                return false;
            }

            return true;
        }

        private static bool TryValidateLaneBoundaryRibbon(
            IReadOnlyList<RoadLanePose> poses,
            RoadLane lane,
            bool reverse,
            out string error)
        {
            error = string.Empty;
            if (poses == null || poses.Count < 2 || lane == null)
            {
                error = "Lane boundary requires at least two valid poses.";
                return false;
            }

            float length = Mathf.Max(0.0001f, poses[poses.Count - 1].distance);
            List<Vector2> ribbon = new List<Vector2>(poses.Count * 2);
            List<Vector2> rightBoundary = new List<Vector2>(poses.Count);
            for (int i = 0; i < poses.Count; i++)
            {
                RoadLanePose pose = poses[i];
                Vector3 forward = pose.forward.sqrMagnitude > 0.0001f
                    ? pose.forward.normalized
                    : Vector3.forward;
                Vector3 up = pose.up.sqrMagnitude > 0.0001f
                    ? pose.up.normalized
                    : Vector3.up;
                Vector3 right = Vector3.Cross(up, forward);
                if (right.sqrMagnitude <= 0.0001f)
                {
                    error = "Lane boundary frame is degenerate.";
                    return false;
                }

                float normalizedDistance = Mathf.Clamp01(pose.distance / length);
                if (reverse)
                {
                    normalizedDistance = 1f - normalizedDistance;
                }

                float width = lane.EvaluateWidth(normalizedDistance);
                Vector3 left = pose.position - right.normalized * width * 0.5f;
                Vector3 rightPoint = pose.position + right.normalized * width * 0.5f;
                if (!UnitySplineRoadLaneGeometry.IsFinite(left) ||
                    !UnitySplineRoadLaneGeometry.IsFinite(rightPoint) ||
                    Vector3.Distance(left, rightPoint) < width * 0.95f)
                {
                    error = "Lane boundary has insufficient effective width.";
                    return false;
                }

                ribbon.Add(new Vector2(left.x, left.z));
                rightBoundary.Add(new Vector2(rightPoint.x, rightPoint.z));
            }

            for (int i = rightBoundary.Count - 1; i >= 0; i--)
            {
                ribbon.Add(rightBoundary[i]);
            }

            if (RoadPolygonGeometry.HasSelfIntersection(ribbon))
            {
                error = "Lane boundary ribbon self-intersects.";
                return false;
            }

            return true;
        }

        private void BuildTrafficRecords(
            Dictionary<string, DirectedLaneBuildState> stateById,
            List<BakedLaneConnectionRecord> connections,
            List<BakedJunctionTrafficRecord> junctionOutput,
            List<BakedConnectorTrafficRecord> connectorOutput)
        {
            RoadJunction[] roadJunctions = GetJunctions();
            Dictionary<string, RoadJunction> junctionById = new Dictionary<string, RoadJunction>(StringComparer.Ordinal);
            for (int i = 0; i < roadJunctions.Length; i++)
            {
                RoadJunction junction = roadJunctions[i];
                if (junction == null || string.IsNullOrWhiteSpace(junction.JunctionId))
                {
                    continue;
                }

                string junctionId = SanitizeId(junction.JunctionId);
                junctionById[junctionId] = junction;
                junctionOutput.Add(CreateJunctionTrafficRecord(junction, junctionId));
            }

            Dictionary<string, BakedLaneConnectionRecord> incomingConnectionByConnector =
                new Dictionary<string, BakedLaneConnectionRecord>(StringComparer.Ordinal);
            for (int i = 0; i < connections.Count; i++)
            {
                BakedLaneConnectionRecord connection = connections[i];
                if (connection == null ||
                    !stateById.TryGetValue(connection.toLaneId, out DirectedLaneBuildState toState) ||
                    toState.record.kind != RoadLaneKind.Connector)
                {
                    continue;
                }

                incomingConnectionByConnector[connection.toLaneId] = connection;
            }

            List<DirectedLaneBuildState> connectors = stateById.Values
                .Where(state => state.record.kind == RoadLaneKind.Connector &&
                                !string.IsNullOrWhiteSpace(state.record.connectorJunctionId))
                .ToList();
            Dictionary<string, List<BakedConnectorConflictRecord>> conflictsByConnectorLaneId =
                BuildConnectorConflictRecords(connectors, junctionById);
            for (int i = 0; i < connectors.Count; i++)
            {
                DirectedLaneBuildState connector = connectors[i];
                if (!junctionById.TryGetValue(connector.record.connectorJunctionId, out RoadJunction junction))
                {
                    continue;
                }

                incomingConnectionByConnector.TryGetValue(
                    connector.record.laneId,
                    out BakedLaneConnectionRecord incomingConnection);
                conflictsByConnectorLaneId.TryGetValue(
                    connector.record.laneId,
                    out List<BakedConnectorConflictRecord> connectorConflicts);
                connectorOutput.Add(CreateConnectorTrafficRecord(
                    connector.record,
                    junction,
                    incomingConnection,
                    connectorConflicts));
            }
        }

        private static BakedJunctionTrafficRecord CreateJunctionTrafficRecord(
            RoadJunction junction,
            string junctionId)
        {
            BakedJunctionTrafficRecord record = new BakedJunctionTrafficRecord
            {
                junctionId = junctionId,
                controlMode = junction.TrafficControlMode,
                defaultStopLineDistance = junction.DefaultStopLineDistance,
                queueSpacing = junction.QueueSpacing,
                approachDetectionDistance = junction.ApproachDetectionDistance,
                passageTokenDuration = junction.PassageTokenDuration,
                releaseDistance = junction.ReleaseDistance,
                connectorConflictSafetyMargin = junction.ConnectorConflictSafetyMargin,
                straightPriority = junction.StraightPriority,
                rightPriority = junction.RightPriority,
                leftPriority = junction.LeftPriority,
                uTurnPriority = junction.UTurnPriority
            };

            List<RoadJunctionSignalPhase> phases = junction.SignalPhases;
            if (phases != null)
            {
                for (int i = 0; i < phases.Count; i++)
                {
                    RoadJunctionSignalPhase phase = phases[i];
                    if (phase == null)
                    {
                        continue;
                    }

                    record.signalPhases.Add(new BakedJunctionSignalPhaseRecord
                    {
                        phaseId = string.IsNullOrWhiteSpace(phase.phaseId)
                            ? "phase_" + i.ToString("D2", CultureInfo.InvariantCulture)
                            : SanitizeId(phase.phaseId),
                        allowedTurns = phase.allowedTurns,
                        greenDuration = Mathf.Max(0.1f, phase.greenDuration),
                        yellowDuration = Mathf.Max(0f, phase.yellowDuration),
                        allRedDuration = Mathf.Max(0f, phase.allRedDuration)
                    });
                }
            }

            if (record.controlMode == RoadJunctionTrafficControlMode.FixedSignal &&
                record.signalPhases.Count == 0)
            {
                record.signalPhases.Add(new BakedJunctionSignalPhaseRecord
                {
                    phaseId = "default",
                    allowedTurns = junction.AllowedTurns,
                    greenDuration = 8f,
                    yellowDuration = 2f,
                    allRedDuration = 1f
                });
            }

            return record;
        }

        private static BakedConnectorTrafficRecord CreateConnectorTrafficRecord(
            BakedLaneRecord connector,
            RoadJunction junction,
            BakedLaneConnectionRecord incomingConnection,
            IReadOnlyList<BakedConnectorConflictRecord> conflicts)
        {
            return new BakedConnectorTrafficRecord
            {
                junctionId = connector.connectorJunctionId,
                connectorLaneId = connector.laneId,
                connectionId = incomingConnection == null ? string.Empty : incomingConnection.connectionId,
                fromLaneId = connector.connectorSourceLaneId,
                toLaneId = connector.connectorTargetLaneId,
                turnType = connector.turnType,
                stopLineDistance = junction.DefaultStopLineDistance,
                conflictConnectorLaneIds = BuildLegacyConflictString(conflicts),
                conflicts = CloneConnectorConflicts(conflicts)
            };
        }

        private Dictionary<string, List<BakedConnectorConflictRecord>> BuildConnectorConflictRecords(
            List<DirectedLaneBuildState> connectors,
            Dictionary<string, RoadJunction> junctionById)
        {
            Dictionary<string, List<BakedConnectorConflictRecord>> output =
                new Dictionary<string, List<BakedConnectorConflictRecord>>(StringComparer.Ordinal);
            if (connectors == null || connectors.Count < 2 || bakedScratchSamples == null)
            {
                return output;
            }

            float probeStep = Mathf.Clamp(SampleSpacing, ConnectorConflictProbeStepMin, ConnectorConflictProbeStepMax);
            Dictionary<string, ConnectorConflictGeometry> geometryByLaneId =
                new Dictionary<string, ConnectorConflictGeometry>(StringComparer.Ordinal);
            for (int i = 0; i < connectors.Count; i++)
            {
                DirectedLaneBuildState state = connectors[i];
                if (state == null ||
                    state.record == null ||
                    !TryBuildConnectorConflictGeometry(state.record, probeStep, out ConnectorConflictGeometry geometry))
                {
                    continue;
                }

                geometryByLaneId[state.record.laneId] = geometry;
            }

            for (int i = 0; i < connectors.Count; i++)
            {
                BakedLaneRecord self = connectors[i].record;
                if (self == null || !geometryByLaneId.TryGetValue(self.laneId, out ConnectorConflictGeometry selfGeometry))
                {
                    continue;
                }

                for (int j = i + 1; j < connectors.Count; j++)
                {
                    BakedLaneRecord other = connectors[j].record;
                    if (other == null ||
                        !string.Equals(self.connectorJunctionId, other.connectorJunctionId, StringComparison.Ordinal) ||
                        !geometryByLaneId.TryGetValue(other.laneId, out ConnectorConflictGeometry otherGeometry))
                    {
                        continue;
                    }

                    float margin = 0.5f;
                    if (junctionById != null &&
                        junctionById.TryGetValue(self.connectorJunctionId, out RoadJunction junction))
                    {
                        margin = junction.ConnectorConflictSafetyMargin;
                    }

                    if (!TryBuildConnectorConflictPair(
                            selfGeometry,
                            otherGeometry,
                            margin,
                            probeStep,
                            out BakedConnectorConflictRecord selfConflict,
                            out BakedConnectorConflictRecord otherConflict))
                    {
                        continue;
                    }

                    AddConnectorConflict(output, self.laneId, selfConflict);
                    AddConnectorConflict(output, other.laneId, otherConflict);
                }
            }

            foreach (List<BakedConnectorConflictRecord> conflicts in output.Values)
            {
                conflicts.Sort((left, right) =>
                    string.Compare(left.otherConnectorLaneId, right.otherConnectorLaneId, StringComparison.Ordinal));
            }

            return output;
        }

        private bool TryBuildConnectorConflictGeometry(
            BakedLaneRecord lane,
            float probeStep,
            out ConnectorConflictGeometry geometry)
        {
            geometry = null;
            if (lane == null || lane.sampleCount < 2 || lane.length <= 0.0001f)
            {
                return false;
            }

            List<ConnectorConflictProbe> probes = new List<ConnectorConflictProbe>();
            for (float distance = 0f; distance < lane.length; distance += probeStep)
            {
                if (TryGetConnectorConflictProbe(lane, distance, out ConnectorConflictProbe probe))
                {
                    probes.Add(probe);
                }
            }

            if (probes.Count == 0 || probes[probes.Count - 1].distance < lane.length - 0.001f)
            {
                if (TryGetConnectorConflictProbe(lane, lane.length, out ConnectorConflictProbe probe))
                {
                    probes.Add(probe);
                }
            }

            if (probes.Count < 2)
            {
                return false;
            }

            geometry = new ConnectorConflictGeometry
            {
                lane = lane,
                probes = probes
            };
            return true;
        }

        private bool TryGetConnectorConflictProbe(
            BakedLaneRecord lane,
            float distance,
            out ConnectorConflictProbe probe)
        {
            probe = default;
            if (lane == null || bakedScratchSamples == null || lane.sampleCount <= 0)
            {
                return false;
            }

            distance = Mathf.Clamp(distance, 0f, lane.length);
            int first = lane.firstSampleIndex;
            int last = first + lane.sampleCount - 1;
            int right = last;
            for (int i = first + 1; i <= last; i++)
            {
                if (bakedScratchSamples[i].distanceAlongLane >= distance)
                {
                    right = i;
                    break;
                }
            }

            int left = Mathf.Max(first, right - 1);
            if (left < 0 || right < 0 || left >= bakedScratchSamples.Count || right >= bakedScratchSamples.Count)
            {
                return false;
            }

            BakedLaneSampleRecord a = bakedScratchSamples[left];
            BakedLaneSampleRecord b = bakedScratchSamples[right];
            float range = b.distanceAlongLane - a.distanceAlongLane;
            float t = range <= 0.0001f ? 0f : Mathf.Clamp01((distance - a.distanceAlongLane) / range);
            Vector3 position = Vector3.Lerp(a.finalPosition, b.finalPosition, t);
            float width = Mathf.Lerp(Mathf.Max(0.1f, a.width), Mathf.Max(0.1f, b.width), t);
            probe = new ConnectorConflictProbe
            {
                position = new Vector2(position.x, position.z),
                distance = distance,
                halfWidth = width * 0.5f
            };
            return true;
        }

        private static bool TryBuildConnectorConflictPair(
            ConnectorConflictGeometry self,
            ConnectorConflictGeometry other,
            float safetyMargin,
            float probeStep,
            out BakedConnectorConflictRecord selfConflict,
            out BakedConnectorConflictRecord otherConflict)
        {
            selfConflict = null;
            otherConflict = null;
            List<ConnectorConflictCandidate> candidates = CollectConnectorConflictCandidates(
                self,
                other,
                safetyMargin);
            if (candidates.Count == 0)
            {
                return false;
            }

            BakedConnectorConflictReason reason = ClassifyConnectorConflict(self.lane, other.lane, candidates);
            CollapseConnectorConflictInterval(
                reason,
                self.lane.length,
                other.lane.length,
                probeStep,
                candidates,
                out float selfStart,
                out float selfEnd,
                out float otherStart,
                out float otherEnd);

            if (selfEnd < selfStart || otherEnd < otherStart)
            {
                return false;
            }

            selfConflict = new BakedConnectorConflictRecord
            {
                otherConnectorLaneId = other.lane.laneId,
                selfStartDistance = selfStart,
                selfEndDistance = selfEnd,
                otherStartDistance = otherStart,
                otherEndDistance = otherEnd,
                reason = reason
            };
            otherConflict = new BakedConnectorConflictRecord
            {
                otherConnectorLaneId = self.lane.laneId,
                selfStartDistance = otherStart,
                selfEndDistance = otherEnd,
                otherStartDistance = selfStart,
                otherEndDistance = selfEnd,
                reason = reason
            };
            return true;
        }

        private static List<ConnectorConflictCandidate> CollectConnectorConflictCandidates(
            ConnectorConflictGeometry self,
            ConnectorConflictGeometry other,
            float safetyMargin)
        {
            List<ConnectorConflictCandidate> candidates = new List<ConnectorConflictCandidate>();
            for (int selfIndex = 0; selfIndex + 1 < self.probes.Count; selfIndex++)
            {
                ConnectorConflictProbe selfStart = self.probes[selfIndex];
                ConnectorConflictProbe selfEnd = self.probes[selfIndex + 1];
                for (int otherIndex = 0; otherIndex + 1 < other.probes.Count; otherIndex++)
                {
                    ConnectorConflictProbe otherStart = other.probes[otherIndex];
                    ConnectorConflictProbe otherEnd = other.probes[otherIndex + 1];
                    SegmentClosestPointXZ(
                        selfStart.position,
                        selfEnd.position,
                        otherStart.position,
                        otherEnd.position,
                        out float selfT,
                        out float otherT,
                        out float distance);
                    float selfDistance = Mathf.Lerp(selfStart.distance, selfEnd.distance, selfT);
                    float otherDistance = Mathf.Lerp(otherStart.distance, otherEnd.distance, otherT);
                    float selfRadius = Mathf.Lerp(selfStart.halfWidth, selfEnd.halfWidth, selfT) + safetyMargin;
                    float otherRadius = Mathf.Lerp(otherStart.halfWidth, otherEnd.halfWidth, otherT) + safetyMargin;
                    if (distance > selfRadius + otherRadius)
                    {
                        continue;
                    }

                    Vector2 selfSegment = selfEnd.position - selfStart.position;
                    Vector2 otherSegment = otherEnd.position - otherStart.position;
                    float directionDot = 1f;
                    if (selfSegment.sqrMagnitude > 0.000001f && otherSegment.sqrMagnitude > 0.000001f)
                    {
                        directionDot = Mathf.Abs(Vector2.Dot(selfSegment.normalized, otherSegment.normalized));
                    }

                    bool crosses = TryGetSegmentIntersectionParameters(
                        selfStart.position,
                        selfEnd.position,
                        otherStart.position,
                        otherEnd.position,
                        out float crossingSelfT,
                        out float crossingOtherT) &&
                                   crossingSelfT > 0.001f &&
                                   crossingSelfT < 0.999f &&
                                   crossingOtherT > 0.001f &&
                                   crossingOtherT < 0.999f;
                    bool interiorNonParallelProximity =
                        directionDot < 0.85f &&
                        selfDistance > 0.001f &&
                        selfDistance < self.lane.length - 0.001f &&
                        otherDistance > 0.001f &&
                        otherDistance < other.lane.length - 0.001f;
                    candidates.Add(new ConnectorConflictCandidate
                    {
                        selfDistance = selfDistance,
                        otherDistance = otherDistance,
                        crosses = crosses,
                        interiorNonParallelProximity = interiorNonParallelProximity
                    });
                }
            }

            return candidates;
        }

        private static BakedConnectorConflictReason ClassifyConnectorConflict(
            BakedLaneRecord self,
            BakedLaneRecord other,
            List<ConnectorConflictCandidate> candidates)
        {
            if (!string.IsNullOrWhiteSpace(self.connectorSourceLaneId) &&
                string.Equals(self.connectorSourceLaneId, other.connectorSourceLaneId, StringComparison.Ordinal))
            {
                return BakedConnectorConflictReason.SameSource;
            }

            if (!string.IsNullOrWhiteSpace(self.connectorTargetLaneId) &&
                string.Equals(self.connectorTargetLaneId, other.connectorTargetLaneId, StringComparison.Ordinal))
            {
                return BakedConnectorConflictReason.Merge;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].crosses || candidates[i].interiorNonParallelProximity)
                {
                    return BakedConnectorConflictReason.Crossing;
                }
            }

            return BakedConnectorConflictReason.Overlap;
        }

        private static void CollapseConnectorConflictInterval(
            BakedConnectorConflictReason reason,
            float selfLength,
            float otherLength,
            float probeStep,
            List<ConnectorConflictCandidate> candidates,
            out float selfStart,
            out float selfEnd,
            out float otherStart,
            out float otherEnd)
        {
            float padding = Mathf.Max(0.1f, probeStep * 0.5f);
            List<ConnectorConflictCandidate> selected = candidates;
            if (reason == BakedConnectorConflictReason.SameSource)
            {
                selected = SelectEntryConflictCandidates(candidates, probeStep);
            }
            else if (reason == BakedConnectorConflictReason.Merge)
            {
                selected = SelectExitConflictCandidates(candidates, selfLength, otherLength, probeStep);
            }
            else if (reason == BakedConnectorConflictReason.Crossing)
            {
                selected = candidates
                    .Where(candidate => candidate.crosses || candidate.interiorNonParallelProximity)
                    .ToList();
                if (selected.Count == 0)
                {
                    selected = candidates;
                }
            }

            GetConflictCandidateBounds(
                selected.Count == 0 ? candidates : selected,
                out selfStart,
                out selfEnd,
                out otherStart,
                out otherEnd);
            if (reason == BakedConnectorConflictReason.SameSource)
            {
                selfStart = 0f;
                otherStart = 0f;
                selfEnd = Mathf.Clamp(selfEnd + padding, 0f, selfLength);
                otherEnd = Mathf.Clamp(otherEnd + padding, 0f, otherLength);
                return;
            }

            if (reason == BakedConnectorConflictReason.Merge)
            {
                selfStart = Mathf.Clamp(selfStart - padding, 0f, selfLength);
                otherStart = Mathf.Clamp(otherStart - padding, 0f, otherLength);
                selfEnd = selfLength;
                otherEnd = otherLength;
                return;
            }

            selfStart = Mathf.Clamp(selfStart - padding, 0f, selfLength);
            selfEnd = Mathf.Clamp(selfEnd + padding, 0f, selfLength);
            otherStart = Mathf.Clamp(otherStart - padding, 0f, otherLength);
            otherEnd = Mathf.Clamp(otherEnd + padding, 0f, otherLength);
        }

        private static List<ConnectorConflictCandidate> SelectEntryConflictCandidates(
            List<ConnectorConflictCandidate> candidates,
            float probeStep)
        {
            List<ConnectorConflictCandidate> sorted = candidates
                .OrderBy(candidate => Mathf.Max(candidate.selfDistance, candidate.otherDistance))
                .ToList();
            List<ConnectorConflictCandidate> selected = new List<ConnectorConflictCandidate>();
            float allowedGap = Mathf.Max(0.5f, probeStep * 1.5f);
            float lastDistance = 0f;
            for (int i = 0; i < sorted.Count; i++)
            {
                ConnectorConflictCandidate candidate = sorted[i];
                float distance = Mathf.Max(candidate.selfDistance, candidate.otherDistance);
                if (selected.Count > 0 && distance - lastDistance > allowedGap)
                {
                    break;
                }

                selected.Add(candidate);
                lastDistance = distance;
            }

            return selected;
        }

        private static List<ConnectorConflictCandidate> SelectExitConflictCandidates(
            List<ConnectorConflictCandidate> candidates,
            float selfLength,
            float otherLength,
            float probeStep)
        {
            List<ConnectorConflictCandidate> sorted = candidates
                .OrderBy(candidate => Mathf.Max(selfLength - candidate.selfDistance, otherLength - candidate.otherDistance))
                .ToList();
            List<ConnectorConflictCandidate> selected = new List<ConnectorConflictCandidate>();
            float allowedGap = Mathf.Max(0.5f, probeStep * 1.5f);
            float lastDistanceFromEnd = 0f;
            for (int i = 0; i < sorted.Count; i++)
            {
                ConnectorConflictCandidate candidate = sorted[i];
                float distanceFromEnd = Mathf.Max(
                    selfLength - candidate.selfDistance,
                    otherLength - candidate.otherDistance);
                if (selected.Count > 0 && distanceFromEnd - lastDistanceFromEnd > allowedGap)
                {
                    break;
                }

                selected.Add(candidate);
                lastDistanceFromEnd = distanceFromEnd;
            }

            return selected;
        }

        private static void GetConflictCandidateBounds(
            List<ConnectorConflictCandidate> candidates,
            out float selfStart,
            out float selfEnd,
            out float otherStart,
            out float otherEnd)
        {
            selfStart = float.PositiveInfinity;
            selfEnd = float.NegativeInfinity;
            otherStart = float.PositiveInfinity;
            otherEnd = float.NegativeInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                ConnectorConflictCandidate candidate = candidates[i];
                selfStart = Mathf.Min(selfStart, candidate.selfDistance);
                selfEnd = Mathf.Max(selfEnd, candidate.selfDistance);
                otherStart = Mathf.Min(otherStart, candidate.otherDistance);
                otherEnd = Mathf.Max(otherEnd, candidate.otherDistance);
            }

            if (selfStart == float.PositiveInfinity)
            {
                selfStart = 0f;
                selfEnd = 0f;
                otherStart = 0f;
                otherEnd = 0f;
            }
        }

        private static void AddConnectorConflict(
            Dictionary<string, List<BakedConnectorConflictRecord>> output,
            string connectorLaneId,
            BakedConnectorConflictRecord conflict)
        {
            if (string.IsNullOrWhiteSpace(connectorLaneId) || conflict == null)
            {
                return;
            }

            if (!output.TryGetValue(connectorLaneId, out List<BakedConnectorConflictRecord> list))
            {
                list = new List<BakedConnectorConflictRecord>();
                output.Add(connectorLaneId, list);
            }

            list.Add(conflict);
        }

        private static List<BakedConnectorConflictRecord> CloneConnectorConflicts(
            IReadOnlyList<BakedConnectorConflictRecord> conflicts)
        {
            List<BakedConnectorConflictRecord> output = new List<BakedConnectorConflictRecord>();
            if (conflicts == null)
            {
                return output;
            }

            for (int i = 0; i < conflicts.Count; i++)
            {
                BakedConnectorConflictRecord conflict = conflicts[i];
                if (conflict == null)
                {
                    continue;
                }

                output.Add(new BakedConnectorConflictRecord
                {
                    otherConnectorLaneId = conflict.otherConnectorLaneId,
                    selfStartDistance = conflict.selfStartDistance,
                    selfEndDistance = conflict.selfEndDistance,
                    otherStartDistance = conflict.otherStartDistance,
                    otherEndDistance = conflict.otherEndDistance,
                    reason = conflict.reason
                });
            }

            return output;
        }

        private static string BuildLegacyConflictString(IReadOnlyList<BakedConnectorConflictRecord> conflicts)
        {
            if (conflicts == null || conflicts.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(
                ",",
                conflicts
                    .Where(conflict => conflict != null && !string.IsNullOrWhiteSpace(conflict.otherConnectorLaneId))
                    .Select(conflict => conflict.otherConnectorLaneId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal));
        }

        private static void SegmentClosestPointXZ(
            Vector2 p1,
            Vector2 q1,
            Vector2 p2,
            Vector2 q2,
            out float s,
            out float t,
            out float distance)
        {
            Vector2 d1 = q1 - p1;
            Vector2 d2 = q2 - p2;
            Vector2 r = p1 - p2;
            float a = Vector2.Dot(d1, d1);
            float e = Vector2.Dot(d2, d2);
            float f = Vector2.Dot(d2, r);
            if (a <= 0.000001f && e <= 0.000001f)
            {
                s = 0f;
                t = 0f;
                distance = Vector2.Distance(p1, p2);
                return;
            }

            if (a <= 0.000001f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector2.Dot(d1, r);
                if (e <= 0.000001f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector2.Dot(d1, d2);
                    float denominator = a * e - b * b;
                    s = denominator == 0f ? 0f : Mathf.Clamp01((b * f - c * e) / denominator);
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            Vector2 closestSelf = p1 + d1 * s;
            Vector2 closestOther = p2 + d2 * t;
            distance = Vector2.Distance(closestSelf, closestOther);
        }

        private static bool TryGetSegmentIntersectionParameters(
            Vector2 p,
            Vector2 p2,
            Vector2 q,
            Vector2 q2,
            out float selfT,
            out float otherT)
        {
            selfT = 0f;
            otherT = 0f;
            Vector2 r = p2 - p;
            Vector2 s = q2 - q;
            float denominator = Cross(r, s);
            if (Mathf.Abs(denominator) <= 0.000001f)
            {
                return false;
            }

            Vector2 qp = q - p;
            selfT = Cross(qp, s) / denominator;
            otherT = Cross(qp, r) / denominator;
            return selfT >= 0f && selfT <= 1f && otherT >= 0f && otherT <= 1f;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private void BuildConnections(
            Dictionary<string, DirectedLaneBuildState> stateById,
            List<BakedLaneConnectionRecord> output)
        {
            HashSet<string> connectionKeys = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> sourcesWithConnectors = new HashSet<string>(StringComparer.Ordinal);
            foreach (DirectedLaneBuildState connector in stateById.Values.Where(state => state.record.kind == RoadLaneKind.Connector))
            {
                if (!stateById.ContainsKey(connector.record.connectorSourceLaneId) ||
                    !stateById.ContainsKey(connector.record.connectorTargetLaneId))
                {
                    continue;
                }

                sourcesWithConnectors.Add(connector.record.connectorSourceLaneId);
                AddConnection(connector.record.connectorSourceLaneId, connector.record.laneId, connector.record.turnType, connector.lane.TraversalCost, stateById, output, connectionKeys);
                AddConnection(connector.record.laneId, connector.record.connectorTargetLaneId, connector.record.turnType, 0f, stateById, output, connectionKeys);
            }

            foreach (DirectedLaneBuildState source in stateById.Values)
            {
                if (source.record.kind == RoadLaneKind.Connector ||
                    source.lane.ConnectionMode == RoadLaneConnectionMode.Blocked)
                {
                    continue;
                }

                if (source.lane.ConnectionMode == RoadLaneConnectionMode.Manual)
                {
                    foreach (string targetId in ParseIds(source.lane.ManualNextLaneIds))
                    {
                        AddConnection(source.record.laneId, targetId, RoadLaneTurn.None, 0f, stateById, output, connectionKeys);
                    }

                    continue;
                }

                if (sourcesWithConnectors.Contains(source.record.laneId))
                {
                    continue;
                }

                BakedLaneSampleRecord sourceEnd = GetEndSample(source.record);
                foreach (DirectedLaneBuildState target in stateById.Values)
                {
                    if (target.record.kind == RoadLaneKind.Connector || source.record.laneId == target.record.laneId)
                    {
                        continue;
                    }

                    BakedLaneSampleRecord targetStart = GetStartSample(target.record);
                    if (Vector3.Distance(sourceEnd.finalPosition, targetStart.finalPosition) > ConnectionRadius ||
                        Vector3.Angle(sourceEnd.forward, targetStart.forward) > ConnectionDirectionTolerance)
                    {
                        continue;
                    }

                    AddConnection(
                        source.record.laneId,
                        target.record.laneId,
                        ClassifyTurn(sourceEnd.forward, targetStart.forward, sourceEnd.up),
                        0f,
                        stateById,
                        output,
                        connectionKeys);
                }
            }
        }

        private void AddConnection(
            string fromId,
            string toId,
            RoadLaneTurn turn,
            float cost,
            Dictionary<string, DirectedLaneBuildState> stateById,
            List<BakedLaneConnectionRecord> output,
            HashSet<string> keys)
        {
            if (!stateById.TryGetValue(fromId, out DirectedLaneBuildState from) ||
                !stateById.TryGetValue(toId, out DirectedLaneBuildState to))
            {
                return;
            }

            string key = fromId + "->" + toId;
            if (!keys.Add(key))
            {
                return;
            }

            output.Add(new BakedLaneConnectionRecord
            {
                connectionId = SanitizeId(key),
                fromLaneId = fromId,
                toLaneId = toId,
                fromSampleId = GetEndSample(from.record).sampleId,
                toSampleId = GetStartSample(to.record).sampleId,
                turnType = turn,
                open = from.record.open && to.record.open,
                baseCost = Mathf.Max(0f, cost)
            });
        }

        private void AssignConnectionSampleIds(
            Dictionary<string, DirectedLaneBuildState> states,
            List<BakedLaneConnectionRecord> connections,
            List<BakedLaneSampleRecord> samples)
        {
            Dictionary<string, List<string>> targetsBySample = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < connections.Count; i++)
            {
                BakedLaneConnectionRecord connection = connections[i];
                if (!targetsBySample.TryGetValue(connection.fromSampleId, out List<string> targets))
                {
                    targets = new List<string>();
                    targetsBySample.Add(connection.fromSampleId, targets);
                }

                targets.Add(connection.toSampleId);
            }

            for (int i = 0; i < samples.Count; i++)
            {
                if (targetsBySample.TryGetValue(samples[i].sampleId, out List<string> targets))
                {
                    samples[i].connectionSampleIds = string.Join(",", targets);
                }
            }
        }

        private void BuildAdjacentLinks(
            List<BakedLaneRecord> laneRecords,
            List<BakedLaneSampleRecord> sampleRecords,
            List<BakedLaneAdjacentLinkRecord> output)
        {
            List<BakedLaneRecord> standardLanes = laneRecords
                .Where(item => item != null &&
                               item.kind == RoadLaneKind.Standard &&
                               item.sampleCount >= 2)
                .ToList();

            for (int sourceIndex = 0; sourceIndex < standardLanes.Count; sourceIndex++)
            {
                BakedLaneRecord source = standardLanes[sourceIndex];
                AdjacentCandidate bestLeft = default;
                AdjacentCandidate bestRight = default;

                for (int targetIndex = 0; targetIndex < standardLanes.Count; targetIndex++)
                {
                    BakedLaneRecord target = standardLanes[targetIndex];
                    if (source == target ||
                        string.Equals(source.laneId, target.laneId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!TryEvaluateAdjacentCandidate(source, target, sampleRecords, out AdjacentCandidate candidate))
                    {
                        continue;
                    }

                    if (candidate.side == RoadLaneAdjacentSide.Left)
                    {
                        if (!bestLeft.valid || candidate.score < bestLeft.score)
                        {
                            bestLeft = candidate;
                        }
                    }
                    else if (!bestRight.valid || candidate.score < bestRight.score)
                    {
                        bestRight = candidate;
                    }
                }

                if (bestLeft.valid)
                {
                    output.Add(CreateAdjacentLink(source, bestLeft));
                }

                if (bestRight.valid)
                {
                    output.Add(CreateAdjacentLink(source, bestRight));
                }
            }
        }

        private bool TryEvaluateAdjacentCandidate(
            BakedLaneRecord source,
            BakedLaneRecord target,
            List<BakedLaneSampleRecord> samples,
            out AdjacentCandidate candidate)
        {
            candidate = default;
            float minLateral = float.PositiveInfinity;
            float maxLateral = 0f;
            float totalLateral = 0f;
            float overlapStart = float.PositiveInfinity;
            float overlapEnd = float.NegativeInfinity;
            int validSampleCount = 0;
            RoadLaneAdjacentSide side = RoadLaneAdjacentSide.Right;
            bool sideAssigned = false;

            int sourceFirst = source.firstSampleIndex;
            int sourceLast = source.firstSampleIndex + source.sampleCount - 1;
            for (int sampleIndex = sourceFirst; sampleIndex <= sourceLast; sampleIndex++)
            {
                if (sampleIndex < 0 || sampleIndex >= samples.Count)
                {
                    continue;
                }

                BakedLaneSampleRecord sourceSample = samples[sampleIndex];
                if (sourceSample == null || !sourceSample.valid)
                {
                    continue;
                }

                if (!TryGetNearestPointOnLane(
                        target,
                        samples,
                        sourceSample.finalPosition,
                        out Vector3 targetPosition,
                        out Vector3 targetForward))
                {
                    continue;
                }

                Vector3 sourceForward = SafeNormalize(sourceSample.forward, Vector3.forward);
                if (Vector3.Angle(sourceForward, targetForward) > AdjacentHeadingTolerance)
                {
                    continue;
                }

                Vector3 sourceToTarget = targetPosition - sourceSample.finalPosition;
                float longitudinalOffset = Mathf.Abs(Vector3.Dot(sourceToTarget, sourceForward));
                if (longitudinalOffset > Mathf.Max(SampleSpacing * 1.5f, 0.25f))
                {
                    continue;
                }

                if (Mathf.Abs(sourceSample.finalPosition.y - targetPosition.y) > AdjacentMaxHeightDifference)
                {
                    continue;
                }

                Vector3 sourceUp = SafeNormalize(sourceSample.up, Vector3.up);
                Vector3 sourceRight = Vector3.Cross(sourceUp, sourceForward);
                if (sourceRight.sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                sourceRight.Normalize();
                float signedLateral = Vector3.Dot(sourceToTarget, sourceRight);
                float lateralDistance = Mathf.Abs(signedLateral);
                if (lateralDistance < AdjacentMinLateralDistance ||
                    lateralDistance > AdjacentMaxLateralDistance)
                {
                    continue;
                }

                RoadLaneAdjacentSide sampleSide = signedLateral < 0f
                    ? RoadLaneAdjacentSide.Left
                    : RoadLaneAdjacentSide.Right;
                if (!sideAssigned)
                {
                    side = sampleSide;
                    sideAssigned = true;
                }
                else if (sampleSide != side)
                {
                    continue;
                }

                minLateral = Mathf.Min(minLateral, lateralDistance);
                maxLateral = Mathf.Max(maxLateral, lateralDistance);
                totalLateral += lateralDistance;
                overlapStart = Mathf.Min(overlapStart, sourceSample.distanceAlongLane);
                overlapEnd = Mathf.Max(overlapEnd, sourceSample.distanceAlongLane);
                validSampleCount++;
            }

            if (validSampleCount < 2)
            {
                return false;
            }

            float overlapLength = overlapEnd - overlapStart;
            if (overlapLength < AdjacentMinimumOverlapLength)
            {
                return false;
            }

            RoadLaneAdjacentFlags flags =
                RoadLaneAdjacentFlags.Auto;
            bool laneChangeAllowed = side == RoadLaneAdjacentSide.Left
                ? source.allowLaneChangeLeft
                : source.allowLaneChangeRight;
            if (laneChangeAllowed)
            {
                flags |= RoadLaneAdjacentFlags.LaneChangeAllowed;
            }
            float edgeTolerance = Mathf.Max(SampleSpacing * 1.5f, 0.25f);
            if (overlapStart > edgeTolerance)
            {
                flags |= RoadLaneAdjacentFlags.Merge;
            }

            if (overlapEnd < source.length - edgeTolerance)
            {
                flags |= RoadLaneAdjacentFlags.Split;
            }

            float averageLateral = totalLateral / validSampleCount;
            candidate = new AdjacentCandidate
            {
                valid = true,
                target = target,
                side = side,
                flags = flags,
                score = averageLateral,
                baseCost = averageLateral,
                minLateralDistance = minLateral,
                maxLateralDistance = maxLateral,
                overlapStartDistance = overlapStart,
                overlapEndDistance = overlapEnd
            };
            return true;
        }

        private static BakedLaneAdjacentLinkRecord CreateAdjacentLink(
            BakedLaneRecord source,
            AdjacentCandidate candidate)
        {
            string key = source.laneId + "->" + candidate.target.laneId + ":" + candidate.side;
            return new BakedLaneAdjacentLinkRecord
            {
                linkId = SanitizeId("adjacent_" + key),
                fromLaneId = source.laneId,
                toLaneId = candidate.target.laneId,
                side = candidate.side,
                flags = candidate.flags,
                open = source.open && candidate.target.open,
                baseCost = Mathf.Max(0f, candidate.baseCost),
                minLateralDistance = candidate.minLateralDistance,
                maxLateralDistance = candidate.maxLateralDistance,
                overlapStartDistance = candidate.overlapStartDistance,
                overlapEndDistance = candidate.overlapEndDistance
            };
        }

        private static bool TryGetNearestPointOnLane(
            BakedLaneRecord lane,
            List<BakedLaneSampleRecord> samples,
            Vector3 position,
            out Vector3 nearestPosition,
            out Vector3 nearestForward)
        {
            nearestPosition = default;
            nearestForward = Vector3.forward;
            if (lane == null || lane.sampleCount < 2)
            {
                return false;
            }

            float bestDistanceSquared = float.PositiveInfinity;
            int first = lane.firstSampleIndex;
            int lastSegmentStart = lane.firstSampleIndex + lane.sampleCount - 2;
            for (int sampleIndex = first; sampleIndex <= lastSegmentStart; sampleIndex++)
            {
                if (sampleIndex < 0 || sampleIndex + 1 >= samples.Count)
                {
                    continue;
                }

                BakedLaneSampleRecord a = samples[sampleIndex];
                BakedLaneSampleRecord b = samples[sampleIndex + 1];
                if (a == null || b == null || !a.valid || !b.valid)
                {
                    continue;
                }

                Vector3 nearest = ClosestPointOnSegment(position, a.finalPosition, b.finalPosition, out float t);
                float distanceSquared = (position - nearest).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                nearestPosition = nearest;
                nearestForward = SafeNormalize(Vector3.Slerp(a.forward, b.forward, t), Vector3.forward);
            }

            return bestDistanceSquared < float.PositiveInfinity;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b, out float t)
        {
            Vector3 delta = b - a;
            float denominator = delta.sqrMagnitude;
            t = denominator <= 0.000001f ? 0f : Mathf.Clamp01(Vector3.Dot(point - a, delta) / denominator);
            return a + delta * t;
        }

        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 0.000001f ? value.normalized : fallback;
        }

        private BakedLaneSampleRecord GetStartSample(BakedLaneRecord lane)
        {
            return bakedScratchSamples[lane.firstSampleIndex];
        }

        private BakedLaneSampleRecord GetEndSample(BakedLaneRecord lane)
        {
            return bakedScratchSamples[lane.firstSampleIndex + lane.sampleCount - 1];
        }

        [NonSerialized] private List<BakedLaneSampleRecord> bakedScratchSamples;

        private void BuildDirectedEndpoints(
            RoadJunction junction,
            List<DirectedEndpoint> incoming,
            List<DirectedEndpoint> outgoing)
        {
            for (int i = 0; i < junction.Bindings.Count; i++)
            {
                RoadJunctionBinding binding = junction.Bindings[i];
                RoadLane lane = binding == null ? null : binding.lane;
                if (lane == null || lane.Kind == RoadLaneKind.Connector)
                {
                    continue;
                }

                float length = geometry.GetLength(lane);
                bool hasForward = lane.TravelDirection != RoadLaneTravelDirection.Reverse;
                bool hasReverse = lane.TravelDirection != RoadLaneTravelDirection.Forward;
                if (hasForward)
                {
                    string id = GetRuntimeLaneId(lane, false);
                    if (binding.endpoint == RoadLaneEndpoint.End &&
                        geometry.TryEvaluate(lane, length, false, out RoadLanePose incomingPose))
                    {
                        incoming.Add(new DirectedEndpoint(lane, id, false, incomingPose));
                    }

                    if (binding.endpoint == RoadLaneEndpoint.Start &&
                        geometry.TryEvaluate(lane, 0f, false, out RoadLanePose outgoingPose))
                    {
                        outgoing.Add(new DirectedEndpoint(lane, id, false, outgoingPose));
                    }
                }

                if (hasReverse)
                {
                    string id = GetRuntimeLaneId(lane, true);
                    if (binding.endpoint == RoadLaneEndpoint.Start &&
                        geometry.TryEvaluate(lane, length, true, out RoadLanePose incomingPose))
                    {
                        incoming.Add(new DirectedEndpoint(lane, id, true, incomingPose));
                    }

                    if (binding.endpoint == RoadLaneEndpoint.End &&
                        geometry.TryEvaluate(lane, 0f, true, out RoadLanePose outgoingPose))
                    {
                        outgoing.Add(new DirectedEndpoint(lane, id, true, outgoingPose));
                    }
                }
            }
        }

        private static void UpdateConnector(
            RoadLane connector,
            string stableKey,
            DirectedEndpoint source,
            DirectedEndpoint target,
            RoadLaneTurn turn,
            RoadJunction junction)
        {
            connector.ConfigureConnector(
                stableKey,
                CreateConnectorId(stableKey),
                source.runtimeLaneId,
                target.runtimeLaneId,
                turn,
                junction.ConnectorSpeedLimit,
                junction.ConnectorBaseCost,
                SanitizeId(junction.JunctionId));
            connector.Width = Mathf.Max(0.1f, Mathf.Min(source.lane.Width, target.lane.Width));
            connector.TagMask = RoadTagMask.Road | RoadTagMask.Vehicle | RoadTagMask.Connector;
            connector.AllowedAgents = source.lane.AllowedAgents & target.lane.AllowedAgents;
            Spline spline = new Spline(2, false);
            float endpointDistance = Vector3.Distance(source.pose.position, target.pose.position);
            float handleLength = Mathf.Max(0.5f, endpointDistance * junction.ConnectorHandleScale);
            Transform connectorTransform = connector.SplineContainer.transform;
            Vector3 start = connectorTransform.InverseTransformPoint(source.pose.position);
            Vector3 end = connectorTransform.InverseTransformPoint(target.pose.position);
            Vector3 startTangent = connectorTransform.InverseTransformVector(source.pose.forward * handleLength);
            Vector3 endTangent = connectorTransform.InverseTransformVector(-target.pose.forward * handleLength);
            spline.Add(new BezierKnot(start, float3.zero, startTangent, quaternion.identity), TangentMode.Broken);
            spline.Add(new BezierKnot(end, endTangent, float3.zero, quaternion.identity), TangentMode.Broken);
            connector.SplineContainer.Spline = spline;
        }

        private static RoadLaneTurn ClassifyTurn(Vector3 incoming, Vector3 outgoing, Vector3 up)
        {
            float angle = Vector3.SignedAngle(incoming, outgoing, up.sqrMagnitude <= 0.0001f ? Vector3.up : up);
            float absolute = Mathf.Abs(angle);
            if (absolute <= 25f)
            {
                return RoadLaneTurn.Straight;
            }

            if (absolute >= 135f)
            {
                return RoadLaneTurn.UTurn;
            }

            return angle > 0f ? RoadLaneTurn.Right : RoadLaneTurn.Left;
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

        public static string GetRuntimeLaneId(RoadLane lane, bool reverse)
        {
            if (lane == null)
            {
                return string.Empty;
            }

            return reverse && lane.TravelDirection == RoadLaneTravelDirection.Bidirectional
                ? SanitizeId(lane.LaneId) + "_rev"
                : SanitizeId(lane.LaneId);
        }

        private static string CreateConnectorId(string stableKey)
        {
            uint hash = 2166136261;
            for (int i = 0; i < stableKey.Length; i++)
            {
                hash ^= stableKey[i];
                hash *= 16777619;
            }

            return "connector_" + hash.ToString("x8", CultureInfo.InvariantCulture);
        }

        public static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] chars = value.Trim().Select(character =>
                char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_').ToArray();
            return new string(chars);
        }

        private static IEnumerable<string> ParseIds(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value.Split(',').Select(SanitizeId).Where(item => !string.IsNullOrWhiteSpace(item));
        }

        private void OnValidate()
        {
            sampleSpacing = Mathf.Max(MinimumSpacing, sampleSpacing);
            connectionRadius = Mathf.Max(0f, connectionRadius);
            connectionDirectionTolerance = Mathf.Clamp(connectionDirectionTolerance, 0f, 180f);
            minimumTurnRadius = Mathf.Max(0.1f, minimumTurnRadius);
            adjacentMinLateralDistance = Mathf.Max(MinimumAdjacentLateralDistance, adjacentMinLateralDistance);
            adjacentMaxLateralDistance = Mathf.Max(adjacentMinLateralDistance, adjacentMaxLateralDistance);
            adjacentHeadingTolerance = Mathf.Clamp(adjacentHeadingTolerance, 0f, 90f);
            adjacentMaxHeightDifference = Mathf.Max(0f, adjacentMaxHeightDifference);
            adjacentMinimumOverlapLength = Mathf.Max(MinimumAdjacentOverlapLength, adjacentMinimumOverlapLength);
            previewLineWidth = Mathf.Clamp(previewLineWidth, MinimumPreviewLineWidth, MaximumPreviewLineWidth);
            outputAssetPath ??= string.Empty;
        }

        private void Reset()
        {
            sampleSpacing = 1f;
            connectionRadius = 0.25f;
            connectionDirectionTolerance = 20f;
            minimumTurnRadius = 4f;
            adjacentMinLateralDistance = 1.5f;
            adjacentMaxLateralDistance = 5f;
            adjacentHeadingTolerance = 15f;
            adjacentMaxHeightDifference = 1f;
            adjacentMinimumOverlapLength = 8f;
            previewLineWidth = DefaultPreviewLineWidth;
            ResetOutputPathToScene();
        }

        private sealed class DirectedLaneBuildState
        {
            public readonly RoadLane lane;
            public readonly BakedLaneRecord record;
            public readonly bool reverse;

            public DirectedLaneBuildState(RoadLane lane, BakedLaneRecord record, bool reverse)
            {
                this.lane = lane;
                this.record = record;
                this.reverse = reverse;
            }
        }

        private sealed class ConnectorConflictGeometry
        {
            public BakedLaneRecord lane;
            public List<ConnectorConflictProbe> probes;
        }

        private struct ConnectorConflictProbe
        {
            public Vector2 position;
            public float distance;
            public float halfWidth;
        }

        private struct ConnectorConflictCandidate
        {
            public float selfDistance;
            public float otherDistance;
            public bool crosses;
            public bool interiorNonParallelProximity;
        }

        private readonly struct DirectedEndpoint
        {
            public readonly RoadLane lane;
            public readonly string runtimeLaneId;
            public readonly bool reverse;
            public readonly RoadLanePose pose;

            public DirectedEndpoint(RoadLane lane, string runtimeLaneId, bool reverse, RoadLanePose pose)
            {
                this.lane = lane;
                this.runtimeLaneId = runtimeLaneId;
                this.reverse = reverse;
                this.pose = pose;
            }
        }

        private struct AdjacentCandidate
        {
            public bool valid;
            public BakedLaneRecord target;
            public RoadLaneAdjacentSide side;
            public RoadLaneAdjacentFlags flags;
            public float score;
            public float baseCost;
            public float minLateralDistance;
            public float maxLateralDistance;
            public float overlapStartDistance;
            public float overlapEndDistance;
        }
    }

    [Serializable]
    public sealed class RoadLaneConnectorReport
    {
        public int created;
        public int updated;
        public int locked;
        public int orphaned;
    }

    [Serializable]
    public sealed class RoadLaneValidationIssue
    {
        public string code;
        public RoadLane lane;
        public string message;

        public RoadLaneValidationIssue(string code, RoadLane lane, string message)
        {
            this.code = code;
            this.lane = lane;
            this.message = message;
        }
    }

}
