using System;
using System.Collections.Generic;
using VehicleRoads;
using UnityEditor;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads.Editor
{
    [InitializeOnLoad]
    internal static class RoadNetworkLivePreviewCoordinator
    {
        private const double RebuildDelaySeconds = 0.25d;
        private static readonly Dictionary<int, PreviewState> States =
            new Dictionary<int, PreviewState>();

        static RoadNetworkLivePreviewCoordinator()
        {
            EditorApplication.update += Update;
            EditorApplication.hierarchyChanged += MarkAllDirty;
            Undo.undoRedoPerformed += MarkAllDirty;
            EditorSplineUtility.AfterSplineWasModified += OnSplineModified;
            AssemblyReloadEvents.beforeAssemblyReload += ClearAll;
        }

        internal static void Register(RoadLaneNetwork network)
        {
            if (network == null)
            {
                return;
            }

            int key = network.GetInstanceID();
            if (!States.TryGetValue(key, out PreviewState state))
            {
                state = new PreviewState(network);
                States.Add(key, state);
            }

            state.referenceCount++;
            state.dirtyAt = EditorApplication.timeSinceStartup;
        }

        internal static void Unregister(RoadLaneNetwork network)
        {
            if (network == null ||
                !States.TryGetValue(network.GetInstanceID(), out PreviewState state))
            {
                return;
            }

            state.referenceCount = Mathf.Max(0, state.referenceCount - 1);
            if (state.referenceCount == 0)
            {
                RemoveState(network.GetInstanceID(), state);
            }
        }

        internal static void MarkDirty(RoadLaneNetwork network)
        {
            if (network != null &&
                States.TryGetValue(network.GetInstanceID(), out PreviewState state))
            {
                state.dirtyAt = EditorApplication.timeSinceStartup;
                state.dirty = true;
            }
        }

        internal static BakedLaneNetwork GetPreview(RoadLaneNetwork network)
        {
            return network != null &&
                   States.TryGetValue(network.GetInstanceID(), out PreviewState state)
                ? state.preview
                : null;
        }

        internal static IReadOnlyList<RoadLaneValidationIssue> GetIssues(RoadLaneNetwork network)
        {
            return network != null &&
                   States.TryGetValue(network.GetInstanceID(), out PreviewState state)
                ? state.issues
                : Array.Empty<RoadLaneValidationIssue>();
        }

        internal static string GetError(RoadLaneNetwork network)
        {
            return network != null &&
                   States.TryGetValue(network.GetInstanceID(), out PreviewState state)
                ? state.error
                : string.Empty;
        }

        internal static BakedLaneNetwork RebuildNowForTests(RoadLaneNetwork network)
        {
            Register(network);
            if (network != null &&
                States.TryGetValue(network.GetInstanceID(), out PreviewState state))
            {
                Rebuild(state);
                return state.preview;
            }

            return null;
        }

        internal static void Draw(RoadLaneNetwork network)
        {
            BakedLaneNetwork preview = GetPreview(network);
            if (preview == null)
            {
                return;
            }

            Color previousColor = Handles.color;
            UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            try
            {
                for (int laneIndex = 0; laneIndex < preview.Lanes.Count; laneIndex++)
                {
                    BakedLaneRecord lane = preview.Lanes[laneIndex];
                    if (lane == null || lane.sampleCount < 2)
                    {
                        continue;
                    }

                    Handles.color = lane.kind == RoadLaneKind.Connector
                        ? new Color(0.2f, 0.9f, 0.72f, 0.75f)
                        : new Color(0.18f, 0.58f, 1f, 0.7f);
                    int first = lane.firstSampleIndex;
                    int end = Mathf.Min(preview.Samples.Count, first + lane.sampleCount);
                    for (int sampleIndex = first; sampleIndex + 1 < end; sampleIndex++)
                    {
                        BakedLaneSampleRecord a = preview.Samples[sampleIndex];
                        BakedLaneSampleRecord b = preview.Samples[sampleIndex + 1];
                        Handles.DrawLine(a.finalPosition, b.finalPosition, 2f);
                        Handles.DrawLine(a.leftBoundary, b.leftBoundary, 1.5f);
                        Handles.DrawLine(a.rightBoundary, b.rightBoundary, 1.5f);
                    }
                }

                DrawPolygonPreview(preview);
                DrawPortalPreview(preview);

                Handles.color = new Color(1f, 0.86f, 0.18f, 0.9f);
                for (int i = 0; i < preview.Connections.Count; i++)
                {
                    BakedLaneConnectionRecord connection = preview.Connections[i];
                    if (connection == null ||
                        !preview.TryGetLane(connection.fromLaneId, out BakedLaneRecord from) ||
                        !preview.TryGetLane(connection.toLaneId, out BakedLaneRecord to) ||
                        from.sampleCount == 0 ||
                        to.sampleCount == 0)
                    {
                        continue;
                    }

                    Vector3 a = preview.Samples[from.firstSampleIndex + from.sampleCount - 1].finalPosition;
                    Vector3 b = preview.Samples[to.firstSampleIndex].finalPosition;
                    Handles.DrawDottedLine(a + Vector3.up * 0.15f, b + Vector3.up * 0.15f, 3f);
                }

                RoadLaneNetworkEditor.DrawAdjacentLinkPreview(network, false);
                RoadJunction[] junctions = network.GetJunctions();
                Handles.color = new Color(0.95f, 0.45f, 1f, 0.9f);
                for (int i = 0; i < junctions.Length; i++)
                {
                    RoadJunction junction = junctions[i];
                    if (junction == null)
                    {
                        continue;
                    }

                    float size = HandleUtility.GetHandleSize(junction.transform.position) * 0.12f;
                    Handles.CubeHandleCap(
                        0,
                        junction.transform.position,
                        Quaternion.identity,
                        size,
                        EventType.Repaint);
                }

                Handles.color = Color.red;
                IReadOnlyList<RoadLaneValidationIssue> issues = GetIssues(network);
                int networkIssueCount = 0;
                for (int i = 0; i < issues.Count; i++)
                {
                    RoadLaneValidationIssue issue = issues[i];
                    if (issue.lane != null && issue.lane.SplineContainer != null)
                    {
                        Vector3 position = issue.lane.SplineContainer.EvaluatePosition(0.5f);
                        Handles.Label(position + Vector3.up * 0.5f, issue.code, EditorStyles.boldLabel);
                    }
                    else
                    {
                        Vector3 position = network.transform.position + Vector3.up * (1f + networkIssueCount * 0.35f);
                        Handles.Label(position, issue.code, EditorStyles.boldLabel);
                        networkIssueCount++;
                    }
                }
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousZTest;
            }
        }

        private static void DrawPolygonPreview(BakedLaneNetwork preview)
        {
            for (int polygonIndex = 0; polygonIndex < preview.Polygons.Count; polygonIndex++)
            {
                BakedPolygonRecord polygon = preview.Polygons[polygonIndex];
                if (polygon == null || polygon.vertices == null || polygon.vertices.Count < 3)
                {
                    continue;
                }

                Handles.color = polygon.open
                    ? new Color(0.2f, 0.85f, 1f, 0.16f)
                    : new Color(1f, 0.3f, 0.2f, 0.16f);
                if (polygon.triangles != null)
                {
                    for (int i = 0; i + 2 < polygon.triangles.Count; i += 3)
                    {
                        int a = polygon.triangles[i];
                        int b = polygon.triangles[i + 1];
                        int c = polygon.triangles[i + 2];
                        if (a < 0 || a >= polygon.vertices.Count ||
                            b < 0 || b >= polygon.vertices.Count ||
                            c < 0 || c >= polygon.vertices.Count)
                        {
                            continue;
                        }

                        Handles.DrawAAConvexPolygon(
                            polygon.vertices[a],
                            polygon.vertices[b],
                            polygon.vertices[c]);
                    }
                }

                Handles.color = polygon.open
                    ? new Color(0.2f, 0.85f, 1f, 0.85f)
                    : new Color(1f, 0.3f, 0.2f, 0.85f);
                for (int i = 0; i < polygon.vertices.Count; i++)
                {
                    Handles.DrawLine(
                        polygon.vertices[i],
                        polygon.vertices[(i + 1) % polygon.vertices.Count],
                        2f);
                }

                Handles.Label(polygon.bounds.center, polygon.zoneId, EditorStyles.miniLabel);
            }
        }

        private static void DrawPortalPreview(BakedLaneNetwork preview)
        {
            for (int portalIndex = 0; portalIndex < preview.Portals.Count; portalIndex++)
            {
                BakedPortalRecord portal = preview.Portals[portalIndex];
                if (portal == null)
                {
                    continue;
                }

                Handles.color = portal.open
                    ? new Color(0.3f, 1f, 0.35f, 0.9f)
                    : new Color(1f, 0.3f, 0.2f, 0.9f);
                float size = HandleUtility.GetHandleSize(portal.sourcePosition) * 0.12f;
                Handles.SphereHandleCap(
                    0,
                    portal.sourcePosition,
                    Quaternion.identity,
                    size,
                    EventType.Repaint);
                Handles.DrawDottedLine(
                    portal.sourcePosition,
                    portal.targetPosition,
                    3f);
                Handles.Label(
                    portal.sourcePosition + Vector3.up * size,
                    portal.portalId + " -> " + portal.targetElementId,
                    EditorStyles.miniLabel);
            }
        }

        private static void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            foreach (PreviewState state in new List<PreviewState>(States.Values))
            {
                if (!state.dirty || state.network == null ||
                    now - state.dirtyAt < RebuildDelaySeconds)
                {
                    continue;
                }

                Rebuild(state);
            }
        }

        private static void Rebuild(PreviewState state)
        {
            state.dirty = false;
            DestroyPreview(state);
            state.issues.Clear();
            state.error = string.Empty;
            try
            {
                state.preview = state.network.BuildTransientNetwork(true);
                state.preview.hideFlags = HideFlags.HideAndDontSave;
                state.issues.AddRange(state.network.ValidateNetwork());
                if (HasBlockingIssues(state.issues))
                {
                    DestroyPreview(state);
                    state.error = "Live preview is unavailable because the network has blocking validation issues.";
                }
            }
            catch (Exception exception)
            {
                DestroyPreview(state);
                state.error = "Live preview failed: " + exception.Message;
            }

            SceneView.RepaintAll();
        }

        private static bool HasBlockingIssues(IReadOnlyList<RoadLaneValidationIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                string code = issues[i].code;
                if (code == "MissingLaneId" ||
                    code == "DuplicateLaneId" ||
                    code == "TooFewKnots" ||
                    code == "InvalidLength" ||
                    code == "InvalidNumber" ||
                    code == "InvalidLaneWidth" ||
                    code == "InvalidLaneBoundary" ||
                    code == "InvalidPolygon" ||
                    code == "InvalidPolygonId" ||
                    code == "InvalidPortal")
                {
                    return true;
                }
            }

            return false;
        }

        private static void OnSplineModified(Spline spline)
        {
            MarkAllDirty();
        }

        private static void MarkAllDirty()
        {
            double now = EditorApplication.timeSinceStartup;
            foreach (PreviewState state in States.Values)
            {
                state.dirty = true;
                state.dirtyAt = now;
            }
        }

        private static void ClearAll()
        {
            foreach (PreviewState state in States.Values)
            {
                DestroyPreview(state);
            }
            States.Clear();
        }

        private static void RemoveState(int key, PreviewState state)
        {
            DestroyPreview(state);
            States.Remove(key);
        }

        private static void DestroyPreview(PreviewState state)
        {
            if (state.preview != null)
            {
                UnityEngine.Object.DestroyImmediate(state.preview);
                state.preview = null;
            }
        }

        private sealed class PreviewState
        {
            public readonly RoadLaneNetwork network;
            public readonly List<RoadLaneValidationIssue> issues =
                new List<RoadLaneValidationIssue>();
            public BakedLaneNetwork preview;
            public int referenceCount;
            public bool dirty = true;
            public double dirtyAt;
            public string error = string.Empty;

            public PreviewState(RoadLaneNetwork network)
            {
                this.network = network;
            }
        }
    }
}
