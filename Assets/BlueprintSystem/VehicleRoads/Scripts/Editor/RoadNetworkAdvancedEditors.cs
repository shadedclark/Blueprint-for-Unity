using System;
using System.Collections.Generic;
using VehicleRoads;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VehicleRoads.Editor
{
    [CustomEditor(typeof(RoadLaneProfileSource))]
    public sealed class RoadLaneProfileSourceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            RoadLaneProfileSource source = (RoadLaneProfileSource)target;
            EditorGUILayout.Space();
            if (GUILayout.Button("Apply Lane Profile", GUILayout.Height(28f)))
            {
                Undo.RegisterFullObjectHierarchyUndo(source.gameObject, "Apply Road Lane Profile");
                bool refreshed = source.RefreshManagedLanes(
                    created => Undo.RegisterCreatedObjectUndo(created, "Create Managed Road Lane"),
                    modified => Undo.RecordObject(modified, "Refresh Managed Road Lane"),
                    out string error);
                if (!refreshed)
                {
                    Debug.LogError(error, source);
                    return;
                }

                EditorUtility.SetDirty(source);
                EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
            }

            EditorGUILayout.HelpBox(
                "Unlocked managed lanes refresh from the profile. Locked lanes retain manual edits; " +
                "locked lanes are marked Stale, and removed entries become Orphaned without deletion.",
                MessageType.Info);
        }
    }

    [CustomEditor(typeof(VehicleRoadSubsystem))]
    public sealed class VehicleRoadSubsystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            SerializedProperty runtimeSettings = serializedObject.FindProperty("runtimeSettings");
            if (runtimeSettings != null && runtimeSettings.objectReferenceValue == null)
            {
                if (GUILayout.Button("Assign Project Runtime Diagnostics Settings"))
                {
                    serializedObject.Update();
                    runtimeSettings.objectReferenceValue =
                        RoadNetworkProjectSettingsAssets.GetRuntimeSettings(true);
                    serializedObject.ApplyModifiedProperties();
                }
            }

            if (GUILayout.Button("Open Runtime Debug Panel"))
            {
                RoadNetworkRuntimeDebugWindow.Open();
            }
        }
    }

    [CustomEditor(typeof(RoadPolygonZone))]
    public sealed class RoadPolygonZoneEditor : UnityEditor.Editor
    {
        private readonly List<int> triangles = new List<int>();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            RoadPolygonZone zone = (RoadPolygonZone)target;
            if (RoadPolygonGeometry.TryTriangulate(zone.Vertices, triangles, out string error))
            {
                EditorGUILayout.HelpBox(
                    "Valid polygon: " + (triangles.Count / 3) + " triangle(s).",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        private void OnSceneGUI()
        {
            RoadPolygonZone zone = (RoadPolygonZone)target;
            if (zone.Vertices == null || zone.Vertices.Count == 0)
            {
                return;
            }

            Color previous = Handles.color;
            Handles.color = new Color(0.2f, 0.85f, 1f, 0.9f);
            for (int i = 0; i < zone.Vertices.Count; i++)
            {
                int next = (i + 1) % zone.Vertices.Count;
                Vector3 world = zone.LocalVertexToWorld(zone.Vertices[i]);
                Vector3 nextWorld = zone.LocalVertexToWorld(zone.Vertices[next]);
                Handles.DrawLine(world, nextWorld, 3f);
                Handles.DotHandleCap(
                    0,
                    world,
                    Quaternion.identity,
                    HandleUtility.GetHandleSize(world) * 0.08f,
                    EventType.Repaint);
            }

            Handles.color = new Color(0.2f, 0.85f, 1f, 0.25f);
            if (RoadPolygonGeometry.TryTriangulate(zone.Vertices, triangles, out _))
            {
                for (int i = 0; i + 2 < triangles.Count; i += 3)
                {
                    Handles.DrawAAConvexPolygon(
                        zone.LocalVertexToWorld(zone.Vertices[triangles[i]]),
                        zone.LocalVertexToWorld(zone.Vertices[triangles[i + 1]]),
                        zone.LocalVertexToWorld(zone.Vertices[triangles[i + 2]]));
                }
            }

            Handles.color = previous;
        }
    }

    [CustomEditor(typeof(RoadPortal))]
    public sealed class RoadPortalEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            RoadPortal portal = (RoadPortal)target;
            if (portal.SourceZone == null)
            {
                EditorGUILayout.HelpBox("A RoadPortal must be a child of a RoadPolygonZone.", MessageType.Error);
            }
            else if (portal.LinkedLane == null && portal.LinkedPortal == null)
            {
                EditorGUILayout.HelpBox("Assign a target Lane or another Polygon Portal.", MessageType.Warning);
            }
        }

        private void OnSceneGUI()
        {
            RoadPortal portal = (RoadPortal)target;
            Vector3 center = portal.transform.position;
            Vector3 right = portal.transform.right * portal.Width * 0.5f;
            Color previous = Handles.color;
            Handles.color = portal.Open ? Color.green : Color.red;
            Handles.DrawLine(center - right, center + right, 4f);
            if (portal.LinkedLane != null)
            {
                Handles.DrawDottedLine(center, portal.LinkedLane.transform.position, 4f);
            }
            else if (portal.LinkedPortal != null)
            {
                Handles.DrawDottedLine(center, portal.LinkedPortal.transform.position, 4f);
            }

            Handles.color = previous;
        }
    }

    public sealed class RoadNetworkRuntimeDebugWindow : EditorWindow
    {
        private VehicleRoadSubsystem subsystem;
        private BakedLaneNetwork network;
        private string agentId = string.Empty;
        private Vector3 queryPosition;
        private Vector3 destination;
        private RoadAreaQueryShape queryShape = RoadAreaQueryShape.Point;
        private float queryRadius = 10f;
        private Vector3 queryBoundsSize = Vector3.one * 10f;
        private float agentRadius = 0.9f;
        private RoadAgentMask agentMask = RoadAgentMask.Car;
        private RoadTagFilter tagFilter;
        private Vector2 scroll;
        private string report = string.Empty;
        private readonly List<RoadAreaQueryResult> areaResults = new List<RoadAreaQueryResult>(64);

        [MenuItem("Tools/Blueprint System/Vehicle Road/Road Network Runtime Debug")]
        public static void Open()
        {
            GetWindow<RoadNetworkRuntimeDebugWindow>("Road Network Debug");
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            subsystem = (VehicleRoadSubsystem)EditorGUILayout.ObjectField(
                "Subsystem",
                subsystem,
                typeof(VehicleRoadSubsystem),
                true);
            network = (BakedLaneNetwork)EditorGUILayout.ObjectField(
                "Baked Network",
                network,
                typeof(BakedLaneNetwork),
                false);
            agentId = EditorGUILayout.TextField("Agent Stable ID", agentId);
            agentMask = (RoadAgentMask)EditorGUILayout.EnumFlagsField("Agent Mask", agentMask);
            agentRadius = EditorGUILayout.FloatField("Agent Radius", agentRadius);
            queryPosition = EditorGUILayout.Vector3Field("Query Position", queryPosition);
            queryShape = (RoadAreaQueryShape)EditorGUILayout.EnumPopup("Area Query Shape", queryShape);
            queryRadius = EditorGUILayout.FloatField("Query Radius", queryRadius);
            if (queryShape == RoadAreaQueryShape.Bounds)
            {
                queryBoundsSize = EditorGUILayout.Vector3Field("Bounds Size", queryBoundsSize);
            }
            destination = EditorGUILayout.Vector3Field("Route Destination", destination);
            DrawTagFilter();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Nearest Element"))
                {
                    RunNearestQuery();
                }

                if (GUILayout.Button("Point / Sphere / Bounds"))
                {
                    RunAreaQuery();
                }

                if (GUILayout.Button("Route"))
                {
                    RunRouteQuery();
                }
            }

            if (subsystem != null)
            {
                DrawSubsystemSnapshot();
                if (GUILayout.Button("Copy Compact Report"))
                {
                    report = subsystem.CreateCompactDebugReport();
                    EditorGUIUtility.systemCopyBuffer = report;
                }
            }

            if (!string.IsNullOrWhiteSpace(report))
            {
                EditorGUILayout.TextArea(report, GUILayout.MinHeight(140f));
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTagFilter()
        {
            EditorGUILayout.LabelField("Tag Filter", EditorStyles.boldLabel);
            tagFilter.all = (RoadTagMask)EditorGUILayout.EnumFlagsField("All", tagFilter.all);
            tagFilter.any = (RoadTagMask)EditorGUILayout.EnumFlagsField("Any", tagFilter.any);
            tagFilter.none = (RoadTagMask)EditorGUILayout.EnumFlagsField("None", tagFilter.none);
        }

        private void RunNearestQuery()
        {
            bool found;
            RoadLocation location = default;
            if (subsystem != null)
            {
                found = subsystem.TryFindNearestElement(
                    queryPosition,
                    agentMask,
                    tagFilter,
                    agentRadius,
                    queryRadius,
                    3f,
                    out location);
            }
            else
            {
                found = network != null && network.TryFindNearestElement(
                    queryPosition,
                    agentMask,
                    tagFilter,
                    agentRadius,
                    queryRadius,
                    3f,
                    out location);
            }

            report = found
                ? string.Format(
                    "Nearest {0}:{1}\nProjected={2}\nInside={3} Boundary={4:0.###} Height={5:0.###}",
                    location.kind,
                    location.elementId,
                    location.projectedPosition,
                    location.inside,
                    location.distanceToBoundary,
                    location.heightDifference)
                : "Nearest element failed: " + location.failureReason;
        }

        private void RunRouteQuery()
        {
            RoadRouteQuery query = new RoadRouteQuery
            {
                startPosition = queryPosition,
                destinationPosition = destination,
                agentMask = agentMask,
                tagFilter = tagFilter,
                agentRadius = agentRadius,
                maximumSearchDistance = queryRadius,
                maximumHeightDifference = 3f
            };
            RoadNetworkRouteResult result;
            bool found;
            if (subsystem != null)
            {
                found = subsystem.TryFindRoute(query, out result);
            }
            else if (network != null)
            {
                found = network.TryFindRoute(query, out result);
            }
            else
            {
                result = null;
                found = false;
            }

            report = result == null
                ? "Route failed: no network result."
                : string.Format(
                    "Route {0} ({1})\nSegments={2} Visited={3} Cost={4:0.###}",
                    found ? "succeeded" : "failed",
                    result.failureReason,
                    result.segments.Count,
                    result.visitedNodeCount,
                    result.totalCost);
        }

        private void RunAreaQuery()
        {
            RoadAreaQuery query = new RoadAreaQuery
            {
                shape = queryShape,
                center = queryPosition,
                radius = Mathf.Max(0f, queryRadius),
                bounds = new Bounds(queryPosition, queryBoundsSize),
                maximumHeightDifference = 3f,
                agentRadius = agentRadius,
                agentMask = agentMask,
                tagFilter = tagFilter,
                maximumResults = 64
            };
            int count = subsystem != null
                ? subsystem.QueryArea(query, areaResults)
                : network == null
                    ? 0
                    : network.QueryArea(query, areaResults);
            if (count == 0)
            {
                report = queryShape + " query returned no elements.";
                return;
            }

            RoadAreaQueryResult best = areaResults[0];
            report = string.Format(
                "{0} query results={1}\nBest={2}:{3}\nDistance={4:0.###} Boundary={5:0.###} Inside={6}",
                queryShape,
                count,
                best.location.kind,
                best.location.elementId,
                best.distance,
                best.location.distanceToBoundary,
                best.location.inside);
        }

        private void DrawSubsystemSnapshot()
        {
            VehicleRoadSubsystemSnapshot snapshot = subsystem.GetSnapshot();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current Runtime Snapshot", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                string.Format(
                    "Networks {0}  Lanes {1}  Polygons {2}  Portals {3}\n" +
                    "Agents {4}  Vehicles {5}  Queued {6}  Tokens {7}\n" +
                    "Frame Queries {8}  Routes {9}  Replans {10}  Failures {11}\n" +
                    "Candidates {12}/{13} peak  Visited {14}/{15} peak  Segments {16}/{17} peak\n" +
                    "History {18}/{19}, dropped {20}",
                    snapshot.registeredNetworkCount,
                    snapshot.laneCount,
                    snapshot.polygonCount,
                    snapshot.portalCount,
                    snapshot.registeredRoadAgentCount,
                    snapshot.registeredVehicleCount,
                    snapshot.queuedVehicleCount,
                    snapshot.activeTokenCount,
                    snapshot.queriesThisFrame,
                    snapshot.routesThisFrame,
                    snapshot.replansThisFrame,
                    snapshot.failuresThisFrame,
                    snapshot.lastCandidateCount,
                    snapshot.peakCandidateCount,
                    snapshot.lastVisitedNodeCount,
                    snapshot.peakVisitedNodeCount,
                    snapshot.lastRouteSegmentCount,
                    snapshot.peakRouteSegmentCount,
                    snapshot.diagnosticHistoryCount,
                    snapshot.diagnosticHistoryCapacity,
                    snapshot.diagnosticDroppedCount),
                MessageType.Info);

            if (!string.IsNullOrWhiteSpace(agentId) &&
                subsystem.TryGetAgentSnapshot(agentId, out RoadAgentDebugSnapshot agent))
            {
                EditorGUILayout.HelpBox(
                    string.Format(
                        "Agent {0}: {1}/{2}\nElement {3}:{4}, segment {5}/{6}\n" +
                        "Remaining {7:0.###}, target speed {8:0.###}, boundary {9:0.###}, failure {10}",
                        agent.agentId,
                        agent.state,
                        agent.routeState,
                        agent.currentElementKind,
                        agent.currentElementId,
                        agent.routeSegmentIndex,
                        agent.routeSegmentCount,
                        agent.remainingDistance,
                        agent.targetSpeed,
                        agent.distanceToBoundary,
                        agent.failureReason),
                    MessageType.None);
            }
        }
    }
}
