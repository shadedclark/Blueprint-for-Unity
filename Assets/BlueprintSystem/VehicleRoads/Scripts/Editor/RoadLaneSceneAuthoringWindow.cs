using System;
using System.Collections.Generic;
using System.Linq;
using VehicleRoads;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

using RuntimeConnectionMode = VehicleRoads.RoadLaneConnectionMode;
using RuntimeDirection = VehicleRoads.RoadLaneTravelDirection;
using RuntimeEndpoint = VehicleRoads.RoadLaneEndpoint;
using RuntimeTurnMask = VehicleRoads.RoadLaneTurnMask;

namespace VehicleRoads.Editor
{
    public sealed class RoadLaneSceneAuthoringWindow : EditorWindow
    {
        private const float EndpointHandleSize = 0.16f;
        private const float DraftPointSize = 0.12f;
        private const float ExistingKnotSize = 0.08f;
        private const float EditableKnotSize = 0.11f;
        private const float ExistingLaneLineWidth = 2.5f;
        private const float MinimumRayDistance = 1f;
        private const float SelectKnotPickRadius = 12f;
        private const float SelectJunctionPickRadius = 14f;
        private const float SelectLanePickRadius = 10f;
        private const float AdjacentInferenceAreaLineWidth = 1.5f;
        private const float AdjacentInferenceAreaSampleSpacing = 2f;
        private const float PolygonVertexHandleSize = 0.1f;
        private const float PolygonEdgeInsertHandleSize = 0.08f;
        private const float PolygonPortalHandleSize = 0.12f;
        private const float PolygonHeightHandleSize = 0.16f;
        private const int CurvePickSegmentCount = 24;

        private static readonly Color DraftLaneColor = new Color(0.1f, 0.75f, 1f, 0.95f);
        private static readonly Color DraftPointColor = new Color(1f, 0.86f, 0.18f, 0.95f);
        private static readonly Color ExistingLaneColor = new Color(0.18f, 0.58f, 1f, 0.42f);
        private static readonly Color ExistingConnectorColor = new Color(0.2f, 0.9f, 0.72f, 0.42f);
        private static readonly Color ExistingKnotColor = new Color(1f, 0.92f, 0.18f, 0.9f);
        private static readonly Color EditableKnotColor = new Color(1f, 0.35f, 0.12f, 0.95f);
        private static readonly Color JunctionBindingColor = new Color(0.25f, 1f, 0.35f, 0.95f);
        private static readonly Color JunctionHandleColor = new Color(0.95f, 0.45f, 1f, 0.9f);
        private static readonly Color ActiveJunctionHandleColor = new Color(0.25f, 1f, 0.95f, 0.95f);
        private static readonly Color ActiveJunctionBindingColor = new Color(0.25f, 1f, 0.95f, 0.85f);
        private static readonly Color EndpointColor = new Color(1f, 0.55f, 0.1f, 0.9f);
        private static readonly Color AdjacentInferenceAreaFillColor = new Color(1f, 0.72f, 0.12f, 0.12f);
        private static readonly Color AdjacentInferenceAreaInnerLineColor = new Color(1f, 0.92f, 0.24f, 0.85f);
        private static readonly Color AdjacentInferenceAreaOuterLineColor = new Color(1f, 0.48f, 0.08f, 0.85f);
        private static readonly Color PolygonLineColor = new Color(0.2f, 0.85f, 1f, 0.9f);
        private static readonly Color PolygonFillColor = new Color(0.2f, 0.85f, 1f, 0.18f);
        private static readonly Color PolygonTopLineColor = new Color(0.35f, 1f, 0.8f, 0.65f);
        private static readonly Color PolygonVertexColor = new Color(0.2f, 0.85f, 1f, 0.95f);
        private static readonly Color PolygonSelectedVertexColor = new Color(1f, 0.9f, 0.2f, 1f);
        private static readonly Color PolygonInsertColor = new Color(0.65f, 1f, 0.95f, 0.9f);
        private static readonly Color PolygonPortalColor = new Color(0.3f, 1f, 0.35f, 0.95f);
        private static readonly Color PolygonPortalWarningColor = new Color(1f, 0.42f, 0.25f, 0.95f);
        private static readonly Color PolygonSuggestionColor = new Color(1f, 0.86f, 0.18f, 0.9f);

        private enum AuthoringOperation
        {
            Select,
            DrawLane,
            BuildJunction,
            SplineEdit,
            Profile,
            Polygon
        }

        private static readonly string[] OperationLabels =
        {
            "1 Select",
            "2 Draw Lane",
            "3 Junction",
            "4 Spline Edit",
            "5 Profile",
            "6 Polygon"
        };

        private readonly List<Vector3> draftLanePoints = new List<Vector3>();
        private readonly List<Vector3> draftPolygonPoints = new List<Vector3>();
        private readonly List<int> polygonTriangles = new List<int>();
        private readonly List<JunctionEndpointDraft> draftJunctionBindings = new List<JunctionEndpointDraft>();
        private readonly UnitySplineRoadLaneGeometry adjacentAreaGeometry = new UnitySplineRoadLaneGeometry();

        private RoadLaneNetwork network;
        private RoadJunction activeJunction;
        private AuthoringOperation operation = AuthoringOperation.Select;
        private Vector2 scroll;
        private List<RoadLaneValidationIssue> validationIssues = new List<RoadLaneValidationIssue>();
        [SerializeField] private bool toolStateSectionExpanded = true;
        [SerializeField] private bool placementSnappingSectionExpanded = false;
        [SerializeField] private bool automaticTopologySectionExpanded = false;
        [SerializeField] private bool currentOperationSectionExpanded = true;
        [SerializeField] private bool networkActionsSectionExpanded = true;
        [SerializeField] private bool profileZoneSectionExpanded = false;
        [SerializeField] private bool adjacentLanesSectionExpanded = true;
        [SerializeField] private bool laneAlignmentSectionExpanded = false;
        [SerializeField] private bool previewValidationSectionExpanded = true;

        private string laneId = string.Empty;
        private string laneIdPrefix = "lane";
        private RuntimeDirection laneDirection = RuntimeDirection.Forward;
        private RuntimeConnectionMode laneConnectionMode = RuntimeConnectionMode.Automatic;
        private string manualNextLaneIds = string.Empty;
        private float laneSpeedLimit = 12f;
        private bool laneOpen = true;
        private float lateralOffset;
        private float verticalOffset;
        private float sampleSpacingOverride;

        private string junctionId = string.Empty;
        private string junctionIdPrefix = "junction";
        private RuntimeTurnMask junctionAllowedTurns = RuntimeTurnMask.Default;
        private float connectorHandleScale = 0.35f;
        private float connectorBaseCost = 1f;
        private float connectorSpeedLimit = 8f;

        private bool snapToRoadColliders = true;
        private LayerMask roadLayerMask = ~0;
        private float roadRayDistance = 10000f;
        private float roadProjectionHeight = 50f;
        private float fallbackPlaneY;
        private bool snapSplineEditToRoadColliders = true;
        private bool snapAuthoringXzToGrid;
        private float authoringGridSize = 1f;
        private bool sceneToolActive = true;
        private bool liveNetworkPreview = true;
        private bool selectCreatedObjects = true;
        private bool endpointSnap = true;
        private bool autoCreateJunction = true;
        private float endpointSnapRadius = 0.75f;
        private float laneInteriorSnapRadius = 1.5f;
        private float endpointDirectionTolerance = 25f;
        private RoadLane pendingTopologyLane;
        private RuntimeEndpoint pendingTopologyEndpoint;
        private RoadLaneProfileSource profileSource;
        private int profileKnotIndex = -1;
        private int loadedProfileKnotIndex = -1;
        private RoadLaneProfile profileOverride;
        private bool profileForceTopologyBreak;
        private RoadLane alignmentTarget;
        private RoadLaneKnotHeightReference alignmentHeightReference = RoadLaneKnotHeightReference.FirstKnot;
        private float alignmentCustomHeight;
        private bool alignmentFlattenTangentHeights = true;
        private float alignmentGridSize = 1f;
        private float adjacentLaneSpacing = RoadLaneAdjacentCopyUtility.DefaultLaneSpacing;
        private bool previewAdjacentLinks;
        private bool showAdjacentInferenceArea;
        private string adjacentPreviewError = string.Empty;
        private string polygonZoneId = string.Empty;
        private string polygonZoneIdPrefix = "polygon";
        private string polygonPortalId = string.Empty;
        private string polygonPortalIdPrefix = "portal";
        private RoadPolygonZone activePolygonZone;
        private int selectedPolygonVertexIndex = -1;
        private RoadPortal selectedPolygonPortal;
        private bool polygonPortalPlacementMode;
        private bool showPolygonVolume = true;
        private bool showPolygonPortalSuggestions = true;
        private float polygonPortalSuggestionRadius = 6f;
        private bool polygonUseReverseRuntimeLane;
        private string polygonSuggestionKey = string.Empty;

        [MenuItem("Tools/Blueprint System/Vehicle Road/Scene Authoring Tool")]
        public static void OpenFromMenu()
        {
            Open(FindSelectedNetwork());
        }

        public static void Open(RoadLaneNetwork targetNetwork)
        {
            Open(targetNetwork, null);
        }

        public static void Open(RoadLaneNetwork targetNetwork, RoadLane targetLane)
        {
            RoadLaneSceneAuthoringWindow window = GetWindow<RoadLaneSceneAuthoringWindow>("Road Lane Authoring");
            window.SetNetwork(targetNetwork != null ? targetNetwork : FindSelectedNetwork());
            if (targetLane != null &&
                targetLane.GetComponentInParent<RoadLaneNetwork>() == window.network)
            {
                window.alignmentTarget = targetLane;
            }

            window.Show();
            window.Repaint();
            SceneView.RepaintAll();
        }

        private void OnEnable()
        {
            if (network == null)
            {
                SetNetwork(FindSelectedNetwork());
            }
            else if (liveNetworkPreview)
            {
                RoadNetworkLivePreviewCoordinator.Register(network);
            }

            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            RoadNetworkLivePreviewCoordinator.Unregister(network);
            RoadLaneNetworkEditor.ClearAdjacentPreviewNetwork(network);
        }

        private void OnSelectionChange()
        {
            if (network == null)
            {
                SetNetwork(FindSelectedNetwork());
            }

            Repaint();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawNetworkSelection();
            DrawOperationSection();
            DrawNetworkActions();
            DrawAdvancedAuthoringTools();
            DrawAdjacentLaneTools();
            DrawAlignmentTools();
            DrawPreviewAndValidation();
            EditorGUILayout.EndScrollView();
        }

        private static bool DrawSectionFoldout(string label, bool expanded)
        {
            EditorGUILayout.Space(4f);
            return EditorGUILayout.Foldout(expanded, label, true, EditorStyles.foldoutHeader);
        }

        private void DrawNetworkSelection()
        {
            EditorGUILayout.LabelField("Scene Authoring Tool", EditorStyles.boldLabel);
            RoadLaneNetwork previousNetwork = network;
            EditorGUI.BeginChangeCheck();
            RoadLaneNetwork selectedNetwork = (RoadLaneNetwork)EditorGUILayout.ObjectField("Network", network, typeof(RoadLaneNetwork), true);
            if (EditorGUI.EndChangeCheck())
            {
                SetNetwork(selectedNetwork);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selection"))
                {
                    SetNetwork(FindSelectedNetwork());
                    RepaintScene();
                }

                using (new EditorGUI.DisabledScope(network == null))
                {
                    if (GUILayout.Button("Select Network"))
                    {
                        Selection.activeObject = network.gameObject;
                    }
                }
            }

            if (previousNetwork != network)
            {
                Repaint();
            }

            if (network == null)
            {
                EditorGUILayout.HelpBox("Select a RoadLaneNetwork, then use Scene View to author lanes and junctions.", MessageType.Info);
            }

            DrawToolStateSection();
            DrawPlacementSnappingSection();
            DrawAutomaticTopologySection();
        }

        private void DrawToolStateSection()
        {
            toolStateSectionExpanded = DrawSectionFoldout("Tool State", toolStateSectionExpanded);
            if (!toolStateSectionExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                sceneToolActive = EditorGUILayout.Toggle("Scene Tool Active", sceneToolActive);
                EditorGUI.BeginChangeCheck();
                bool nextLivePreview = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Live Network Preview",
                        "Rebuilds a hidden in-memory network after authoring changes. The saved asset is only changed by Bake."),
                    liveNetworkPreview);
                if (EditorGUI.EndChangeCheck())
                {
                    SetLiveNetworkPreview(nextLivePreview);
                }

                selectCreatedObjects = EditorGUILayout.Toggle("Select Created Objects", selectCreatedObjects);
            }
        }

        private void DrawPlacementSnappingSection()
        {
            placementSnappingSectionExpanded = DrawSectionFoldout("Placement & Snapping", placementSnappingSectionExpanded);
            if (!placementSnappingSectionExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                snapToRoadColliders = EditorGUILayout.Toggle("Snap To Road Colliders", snapToRoadColliders);
                roadLayerMask = EditorGUILayout.MaskField("Road Collider Layers", roadLayerMask, UnityEditorInternal.InternalEditorUtility.layers);
                roadRayDistance = EditorGUILayout.FloatField("Road Ray Distance", Mathf.Max(MinimumRayDistance, roadRayDistance));
                roadProjectionHeight = EditorGUILayout.FloatField("Road Projection Height", Mathf.Max(0.1f, roadProjectionHeight));
                using (new EditorGUI.DisabledScope(snapToRoadColliders))
                {
                    fallbackPlaneY = EditorGUILayout.FloatField("Fallback Plane Y", fallbackPlaneY);
                }

                using (new EditorGUI.DisabledScope(!snapToRoadColliders))
                {
                    snapSplineEditToRoadColliders = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Snap Spline Edit To Road",
                            "When enabled, dragged Spline Edit knots are projected back to road colliders."),
                        snapSplineEditToRoadColliders);
                }

                snapAuthoringXzToGrid = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Snap Authoring XZ To Grid",
                        "Applies to draft points and Spline Edit moves while preserving height unless another snap changes it."),
                    snapAuthoringXzToGrid);
                using (new EditorGUI.DisabledScope(!snapAuthoringXzToGrid))
                {
                    authoringGridSize = EditorGUILayout.FloatField("Authoring Grid Size", Mathf.Max(0.001f, authoringGridSize));
                }
            }
        }

        private void DrawAutomaticTopologySection()
        {
            automaticTopologySectionExpanded = DrawSectionFoldout("Automatic Topology", automaticTopologySectionExpanded);
            if (!automaticTopologySectionExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                endpointSnap = EditorGUILayout.Toggle("Endpoint Snap", endpointSnap);
                using (new EditorGUI.DisabledScope(!endpointSnap))
                {
                    autoCreateJunction = EditorGUILayout.Toggle("Auto Junction", autoCreateJunction);
                    endpointSnapRadius = EditorGUILayout.FloatField(
                        "Endpoint Radius",
                        Mathf.Max(0.01f, endpointSnapRadius));
                    laneInteriorSnapRadius = EditorGUILayout.FloatField(
                        "Lane Interior Radius",
                        Mathf.Max(0.01f, laneInteriorSnapRadius));
                    endpointDirectionTolerance = EditorGUILayout.Slider(
                        "Direct Connect Angle",
                        endpointDirectionTolerance,
                        0f,
                        180f);
                }
            }
        }

        private void DrawOperationSection()
        {
            currentOperationSectionExpanded = DrawSectionFoldout(
                "Current Operation - " + GetOperationTitle(operation),
                currentOperationSectionExpanded);
            if (!currentOperationSectionExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawCurrentOperationInfo();
                if (operation == AuthoringOperation.DrawLane)
                {
                    DrawLaneSettings();
                }
                else if (operation == AuthoringOperation.BuildJunction)
                {
                    DrawJunctionSettings();
                }
                else if (operation == AuthoringOperation.Profile)
                {
                    DrawProfileSettings();
                }
                else if (operation == AuthoringOperation.Polygon)
                {
                    DrawPolygonSettings();
                }
                else
                {
                    DrawOperationHelp();
                }
            }
        }

        private void DrawCurrentOperationInfo()
        {
            EditorGUILayout.LabelField("Current Scene Operation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(GetOperationTitle(operation) + " is selected from the Scene View toolbar. Use 1/2/3/4/5/6 in Scene View to switch operations.", MessageType.None);
        }

        private void DrawOperationHelp()
        {
            if (operation == AuthoringOperation.SplineEdit)
            {
                EditorGUILayout.HelpBox("All authored RoadLane and Connector knots are editable in Scene View. Parameters remain here; operation switching happens in Scene View.", MessageType.None);
            }
            else if (operation == AuthoringOperation.Profile)
            {
                EditorGUILayout.HelpBox("Profile mode edits the Lane Profile assigned at each reference Spline knot and can force a topology break.", MessageType.None);
            }
            else if (operation == AuthoringOperation.Polygon)
            {
                EditorGUILayout.HelpBox("Polygon mode creates and edits RoadPolygonZone boundaries, height volume, and RoadPortal boundary links.", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Select mode lets you select Lane curves, Lane or Connector knots, and Junction handles without creating points or changing Junction bindings.", MessageType.None);
            }
        }

        private void DrawProfileSettings()
        {
            EditorGUILayout.LabelField("Lane Profile Control Point", EditorStyles.boldLabel);
            RoadLaneProfileSource selectedSource = Selection.activeGameObject == null
                ? null
                : Selection.activeGameObject.GetComponentInParent<RoadLaneProfileSource>();
            if (selectedSource != null &&
                selectedSource.GetComponentInParent<RoadLaneNetwork>() == network &&
                profileSource == null)
            {
                profileSource = selectedSource;
            }

            EditorGUI.BeginChangeCheck();
            profileSource = (RoadLaneProfileSource)EditorGUILayout.ObjectField(
                "Profile Source",
                profileSource,
                typeof(RoadLaneProfileSource),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                loadedProfileKnotIndex = -1;
            }
            if (profileSource == null ||
                profileSource.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                EditorGUILayout.HelpBox("Select a Lane Profile Source inside this network.", MessageType.Info);
                return;
            }

            profileSource.SynchronizeControlPoints();
            int knotCount = profileSource.SplineContainer == null ||
                            profileSource.SplineContainer.Spline == null
                ? 0
                : profileSource.SplineContainer.Spline.Count;
            profileKnotIndex = EditorGUILayout.IntSlider(
                "Knot",
                Mathf.Clamp(profileKnotIndex, 0, Mathf.Max(0, knotCount - 1)),
                0,
                Mathf.Max(0, knotCount - 1));
            if (profileKnotIndex >= 0 && profileKnotIndex < profileSource.ControlPoints.Count)
            {
                RoadLaneProfileControlPoint point = profileSource.ControlPoints[profileKnotIndex];
                if (loadedProfileKnotIndex != profileKnotIndex)
                {
                    loadedProfileKnotIndex = profileKnotIndex;
                    profileOverride = point.profileOverride;
                    profileForceTopologyBreak = point.forceTopologyBreak;
                }
                EditorGUILayout.LabelField("Point ID", point.pointId);
                profileOverride = (RoadLaneProfile)EditorGUILayout.ObjectField(
                    "Profile Override",
                    profileOverride,
                    typeof(RoadLaneProfile),
                    false);
                profileForceTopologyBreak = EditorGUILayout.Toggle(
                    "Force Topology Break",
                    profileForceTopologyBreak);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply Control Point"))
                    {
                        Undo.RecordObject(profileSource, "Edit Road Lane Profile Control Point");
                        profileSource.SetControlPoint(
                            profileKnotIndex,
                            profileOverride,
                            profileForceTopologyBreak,
                            out string error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            Debug.LogError(error, profileSource);
                        }
                        RefreshProfileSource();
                    }

                    if (GUILayout.Button("Clear Override"))
                    {
                        Undo.RecordObject(profileSource, "Clear Road Lane Profile Control Point");
                        profileOverride = null;
                        profileForceTopologyBreak = false;
                        profileSource.SetControlPoint(profileKnotIndex, null, false, out _);
                        RefreshProfileSource();
                    }
                }
            }

            if (GUILayout.Button("Refresh Managed Lanes"))
            {
                RefreshProfileSource();
            }
        }

        private void DrawPolygonSettings()
        {
            EditorGUILayout.LabelField("Polygon Zone", EditorStyles.boldLabel);
            polygonZoneId = EditorGUILayout.TextField("Next Zone ID", polygonZoneId);
            polygonZoneIdPrefix = EditorGUILayout.TextField("Zone ID Prefix", polygonZoneIdPrefix);
            showPolygonVolume = EditorGUILayout.Toggle("Show Zone Volume", showPolygonVolume);

            EditorGUI.BeginChangeCheck();
            activePolygonZone = (RoadPolygonZone)EditorGUILayout.ObjectField(
                "Active Zone",
                activePolygonZone,
                typeof(RoadPolygonZone),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                selectedPolygonVertexIndex = -1;
                if (activePolygonZone != null && activePolygonZone.GetComponentInParent<RoadLaneNetwork>() != network)
                {
                    selectedPolygonPortal = null;
                }
            }

            if (activePolygonZone != null && activePolygonZone.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                EditorGUILayout.HelpBox("Active Zone must be inside the selected RoadLaneNetwork.", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected Zone"))
                {
                    activePolygonZone = FindSelectedPolygonZone();
                    selectedPolygonVertexIndex = -1;
                    RepaintScene();
                }

                using (new EditorGUI.DisabledScope(activePolygonZone == null))
                {
                    if (GUILayout.Button("Select Active Zone"))
                    {
                        Selection.activeObject = activePolygonZone.gameObject;
                        RepaintScene();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Draft Zone", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Scene View: left click adds vertices. Enter or right click creates the zone. Backspace removes the last draft vertex. Escape clears the draft.", MessageType.None);
            EditorGUILayout.LabelField("Draft Vertices", draftPolygonPoints.Count.ToString());
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(network == null || draftPolygonPoints.Count < 3))
                {
                    if (GUILayout.Button("Create Zone"))
                    {
                        CreatePolygonZoneFromDraft();
                    }
                }

                using (new EditorGUI.DisabledScope(draftPolygonPoints.Count == 0))
                {
                    if (GUILayout.Button("Clear Draft"))
                    {
                        draftPolygonPoints.Clear();
                        RepaintScene();
                    }
                }
            }

            if (activePolygonZone != null && activePolygonZone.GetComponentInParent<RoadLaneNetwork>() == network)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Selected Zone", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Zone ID", activePolygonZone.ZoneId);
                EditorGUILayout.LabelField("Vertices", activePolygonZone.Vertices.Count.ToString());
                using (new EditorGUI.DisabledScope(
                           selectedPolygonVertexIndex < 0 ||
                           selectedPolygonVertexIndex >= activePolygonZone.Vertices.Count ||
                           activePolygonZone.Vertices.Count <= 3))
                {
                    if (GUILayout.Button("Delete Selected Vertex"))
                    {
                        DeleteSelectedPolygonVertex();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Portal", EditorStyles.boldLabel);
            polygonPortalId = EditorGUILayout.TextField("Next Portal ID", polygonPortalId);
            polygonPortalIdPrefix = EditorGUILayout.TextField("Portal ID Prefix", polygonPortalIdPrefix);
            polygonPortalPlacementMode = EditorGUILayout.Toggle(
                new GUIContent(
                    "Create Portal On Boundary",
                    "When enabled, left click inside Polygon mode creates a RoadPortal on the active zone boundary instead of adding draft vertices."),
                polygonPortalPlacementMode);
            showPolygonPortalSuggestions = EditorGUILayout.Toggle("Show Target Suggestions", showPolygonPortalSuggestions);
            using (new EditorGUI.DisabledScope(!showPolygonPortalSuggestions))
            {
                polygonPortalSuggestionRadius = EditorGUILayout.FloatField(
                    "Suggestion Radius",
                    Mathf.Max(0f, polygonPortalSuggestionRadius));
            }

            EditorGUI.BeginChangeCheck();
            selectedPolygonPortal = (RoadPortal)EditorGUILayout.ObjectField(
                "Selected Portal",
                selectedPolygonPortal,
                typeof(RoadPortal),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                polygonSuggestionKey = string.Empty;
                if (selectedPolygonPortal != null && selectedPolygonPortal.SourceZone != null)
                {
                    activePolygonZone = selectedPolygonPortal.SourceZone;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected Portal"))
                {
                    selectedPolygonPortal = FindSelectedPortal();
                    polygonSuggestionKey = string.Empty;
                    if (selectedPolygonPortal != null && selectedPolygonPortal.SourceZone != null)
                    {
                        activePolygonZone = selectedPolygonPortal.SourceZone;
                    }

                    RepaintScene();
                }

                using (new EditorGUI.DisabledScope(selectedPolygonPortal == null))
                {
                    if (GUILayout.Button("Select Portal"))
                    {
                        Selection.activeObject = selectedPolygonPortal.gameObject;
                        RepaintScene();
                    }
                }
            }

            if (selectedPolygonPortal == null)
            {
                EditorGUILayout.HelpBox("Select or create a RoadPortal to see target suggestions.", MessageType.Info);
                return;
            }

            if (selectedPolygonPortal.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                EditorGUILayout.HelpBox("Selected Portal must be inside the selected RoadLaneNetwork.", MessageType.Warning);
                return;
            }

            DrawSelectedPortalSuggestion();
        }

        private void DrawSelectedPortalSuggestion()
        {
            if (!showPolygonPortalSuggestions)
            {
                return;
            }

            if (!RoadPolygonAuthoringUtility.TryFindPortalSuggestion(
                    network,
                    selectedPolygonPortal,
                    polygonPortalSuggestionRadius,
                    out RoadPolygonPortalSuggestion suggestion))
            {
                polygonSuggestionKey = string.Empty;
                EditorGUILayout.HelpBox("No Lane endpoint or Polygon Portal is within the suggestion radius.", MessageType.Info);
                return;
            }

            if (polygonSuggestionKey != suggestion.StableKey)
            {
                polygonSuggestionKey = suggestion.StableKey;
                polygonUseReverseRuntimeLane = suggestion.useReverseRuntimeLane;
            }

            EditorGUILayout.LabelField("Suggested Target", suggestion.DisplayName);
            EditorGUILayout.LabelField("Distance", suggestion.distance.ToString("0.00") + " m");
            if (suggestion.kind == RoadPolygonPortalSuggestionKind.Lane)
            {
                polygonUseReverseRuntimeLane = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Use Reverse Runtime Lane",
                        "Forward lanes default to false, reverse lanes default to true, and bidirectional lanes default to false."),
                    polygonUseReverseRuntimeLane);
            }

            if (GUILayout.Button("Apply Suggested Target"))
            {
                Undo.RecordObject(selectedPolygonPortal, "Apply Road Portal Suggested Target");
                if (RoadPolygonAuthoringUtility.ApplyPortalSuggestion(
                        selectedPolygonPortal,
                        suggestion,
                        polygonUseReverseRuntimeLane))
                {
                    EditorUtility.SetDirty(selectedPolygonPortal);
                    MarkNetworkDirty();
                    RepaintScene();
                }
            }
        }

        private void RefreshProfileSource()
        {
            if (profileSource == null)
            {
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(profileSource.gameObject, "Refresh Road Lane Profile");
            if (!profileSource.RefreshManagedLanes(
                    created => Undo.RegisterCreatedObjectUndo(created, "Create Managed Road Lane"),
                    modified => Undo.RecordObject(modified, "Refresh Managed Road Lane"),
                    out string error))
            {
                Debug.LogError(error, profileSource);
                return;
            }

            EditorUtility.SetDirty(profileSource);
            MarkNetworkDirty();
            RepaintScene();
        }

        private void DrawLaneSettings()
        {
            EditorGUILayout.LabelField("New Lane", EditorStyles.boldLabel);
            laneId = EditorGUILayout.TextField("Next Lane ID", laneId);
            laneIdPrefix = EditorGUILayout.TextField("ID Prefix", laneIdPrefix);
            laneDirection = (RuntimeDirection)EditorGUILayout.EnumPopup("Travel Direction", laneDirection);
            laneConnectionMode = (RuntimeConnectionMode)EditorGUILayout.EnumPopup("Connection Mode", laneConnectionMode);
            using (new EditorGUI.DisabledScope(laneConnectionMode != RuntimeConnectionMode.Manual))
            {
                manualNextLaneIds = EditorGUILayout.TextField("Manual Next Lane IDs", manualNextLaneIds);
            }

            laneSpeedLimit = EditorGUILayout.FloatField("Speed Limit", Mathf.Max(0f, laneSpeedLimit));
            laneOpen = EditorGUILayout.Toggle("Open", laneOpen);
            lateralOffset = EditorGUILayout.FloatField("Lateral Offset", lateralOffset);
            verticalOffset = EditorGUILayout.FloatField("Vertical Offset", verticalOffset);
            sampleSpacingOverride = EditorGUILayout.FloatField("Sample Spacing Override", Mathf.Max(0f, sampleSpacingOverride));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Draft", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Scene View: left click adds knots. Enter or right click creates the lane. Backspace removes the last knot. Escape clears the draft.", MessageType.None);
            EditorGUILayout.LabelField("Knot Count", draftLanePoints.Count.ToString());
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(draftLanePoints.Count < 2 || network == null))
                {
                    if (GUILayout.Button("Create Lane"))
                    {
                        CreateLaneFromDraft();
                    }
                }

                using (new EditorGUI.DisabledScope(draftLanePoints.Count == 0))
                {
                    if (GUILayout.Button("Clear Draft"))
                    {
                        draftLanePoints.Clear();
                        RepaintScene();
                    }
                }
            }
        }

        private void DrawJunctionSettings()
        {
            EditorGUILayout.LabelField("Junction", EditorStyles.boldLabel);
            junctionId = EditorGUILayout.TextField("Next Junction ID", junctionId);
            junctionIdPrefix = EditorGUILayout.TextField("ID Prefix", junctionIdPrefix);
            junctionAllowedTurns = (RuntimeTurnMask)EditorGUILayout.EnumFlagsField("Allowed Turns", junctionAllowedTurns);
            connectorHandleScale = EditorGUILayout.Slider("Connector Handle Scale", Mathf.Max(0.1f, connectorHandleScale), 0.1f, 2f);
            connectorBaseCost = EditorGUILayout.FloatField("Connector Base Cost", Mathf.Max(0f, connectorBaseCost));
            connectorSpeedLimit = EditorGUILayout.FloatField("Connector Speed Limit", Mathf.Max(0f, connectorSpeedLimit));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Endpoint Bindings", EditorStyles.boldLabel);
            activeJunction = (RoadJunction)EditorGUILayout.ObjectField("Active Junction", activeJunction, typeof(RoadJunction), true);
            if (activeJunction != null && activeJunction.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                EditorGUILayout.HelpBox("Active Junction must be a child of the selected RoadLaneNetwork.", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected Junction"))
                {
                    activeJunction = FindSelectedJunction();
                    if (activeJunction != null)
                    {
                        draftJunctionBindings.Clear();
                    }

                    RepaintScene();
                }

                using (new EditorGUI.DisabledScope(activeJunction == null))
                {
                    if (GUILayout.Button("Clear Active Junction"))
                    {
                        activeJunction = null;
                        RepaintScene();
                    }
                }
            }

            if (HasActiveJunction())
            {
                DrawActiveJunctionBindings();
                return;
            }

            EditorGUILayout.HelpBox("Scene View: click an existing Junction handle to edit it, or click lane endpoint handles to draft a new Junction.", MessageType.None);
            if (draftJunctionBindings.Count == 0)
            {
                EditorGUILayout.LabelField("No endpoints selected.");
            }

            for (int i = draftJunctionBindings.Count - 1; i >= 0; i--)
            {
                JunctionEndpointDraft draft = draftJunctionBindings[i];
                if (draft.lane == null)
                {
                    draftJunctionBindings.RemoveAt(i);
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(draft.lane.LaneId + " / " + draft.endpoint, EditorStyles.miniLabel);
                    if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                    {
                        draftJunctionBindings.RemoveAt(i);
                        RepaintScene();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(network == null || draftJunctionBindings.Count < 2))
                {
                    if (GUILayout.Button("Create Junction"))
                    {
                        CreateJunctionFromDraft();
                    }
                }

                using (new EditorGUI.DisabledScope(draftJunctionBindings.Count == 0))
                {
                    if (GUILayout.Button("Clear Bindings"))
                    {
                        draftJunctionBindings.Clear();
                        RepaintScene();
                    }
                }
            }
        }

        private void DrawActiveJunctionBindings()
        {
            EditorGUILayout.HelpBox("Scene View: click lane start/end handles to add or remove bindings on the active Junction. Connectors refresh immediately.", MessageType.None);
            EditorGUILayout.LabelField("Active", activeJunction.JunctionId, EditorStyles.miniLabel);
            if (activeJunction.Bindings.Count == 0)
            {
                EditorGUILayout.LabelField("No endpoint bindings.");
                return;
            }

            for (int i = activeJunction.Bindings.Count - 1; i >= 0; i--)
            {
                RoadJunctionBinding binding = activeJunction.Bindings[i];
                if (binding == null || binding.lane == null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Missing lane / " + (binding == null ? RuntimeEndpoint.End : binding.endpoint), EditorStyles.miniLabel);
                        if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                        {
                            RemoveActiveJunctionBindingAt(i);
                        }
                    }

                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(binding.lane.LaneId + " / " + binding.endpoint, EditorStyles.miniLabel);
                    if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                    {
                        RemoveActiveJunctionBindingAt(i);
                    }
                }
            }
        }

        private void DrawNetworkActions()
        {
            networkActionsSectionExpanded = DrawSectionFoldout("Network Actions", networkActionsSectionExpanded);
            if (!networkActionsSectionExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUI.DisabledScope(network == null))
                {
                    if (GUILayout.Button("Refresh All Junction Connectors"))
                    {
                        RoadLaneConnectorReport report = network.GenerateConnectors(
                            created => Undo.RegisterCreatedObjectUndo(created, "Create Road Connector"));
                        MarkNetworkDirty();
                        Debug.LogFormat(
                            network,
                            "Vehicle road connectors refreshed: {0} created, {1} updated, {2} locked, {3} orphaned.",
                            report.created,
                            report.updated,
                            report.locked,
                            report.orphaned);
                        RepaintScene();
                    }

                    if (GUILayout.Button("Validate Network"))
                    {
                        validationIssues = network.ValidateNetwork();
                    }

                    if (GUILayout.Button("Bake Network Asset"))
                    {
                        RoadLaneNetworkEditor.BakeAndSave(network);
                    }
                }
            }
        }

        private void DrawAdvancedAuthoringTools()
        {
            profileZoneSectionExpanded = DrawSectionFoldout("Profile / Polygon / Portal", profileZoneSectionExpanded);
            if (!profileZoneSectionExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(network == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create Profile Source"))
                    {
                        GameObject sourceObject = new GameObject("Road Lane Profile Source");
                        Undo.RegisterCreatedObjectUndo(sourceObject, "Create Road Lane Profile Source");
                        Undo.SetTransformParent(sourceObject.transform, network.transform, "Parent Road Lane Profile Source");
                        SplineContainer container = Undo.AddComponent<SplineContainer>(sourceObject);
                        Spline spline = new Spline();
                        spline.Add(Vector3.zero);
                        spline.Add(Vector3.forward * 10f);
                        container.Spline = spline;
                        Undo.AddComponent<RoadLaneProfileSource>(sourceObject);
                        Selection.activeObject = sourceObject;
                        MarkNetworkDirty();
                    }

                    if (GUILayout.Button("Create Polygon Zone"))
                    {
                        string id = RoadPolygonAuthoringUtility.GetUniqueZoneId(
                            network,
                            polygonZoneId,
                            polygonZoneIdPrefix);
                        GameObject zoneObject = new GameObject(id);
                        Undo.RegisterCreatedObjectUndo(zoneObject, "Create Road Polygon Zone");
                        Undo.SetTransformParent(zoneObject.transform, network.transform, "Parent Road Polygon Zone");
                        RoadPolygonZone zone = Undo.AddComponent<RoadPolygonZone>(zoneObject);
                        zone.ZoneId = id;
                        polygonZoneId = string.Empty;
                        activePolygonZone = zone;
                        selectedPolygonVertexIndex = -1;
                        Selection.activeObject = zoneObject;
                        MarkNetworkDirty();
                    }
                }

                RoadPolygonZone selectedZone = Selection.activeGameObject == null
                    ? null
                    : Selection.activeGameObject.GetComponentInParent<RoadPolygonZone>();
                using (new EditorGUI.DisabledScope(selectedZone == null))
                {
                    if (GUILayout.Button("Create Portal Under Selected Polygon"))
                    {
                        string id = RoadPolygonAuthoringUtility.GetUniquePortalId(
                            selectedZone,
                            polygonPortalId,
                            polygonPortalIdPrefix);
                        GameObject portalObject = new GameObject(id);
                        Undo.RegisterCreatedObjectUndo(portalObject, "Create Road Portal");
                        Undo.SetTransformParent(portalObject.transform, selectedZone.transform, "Parent Road Portal");
                        RoadPortal portal = Undo.AddComponent<RoadPortal>(portalObject);
                        portal.PortalId = id;
                        if (RoadPolygonAuthoringUtility.TryProjectToBoundary(
                                selectedZone,
                                selectedZone.LocalVertexToWorld(RoadPolygonAuthoringUtility.GetLocalCentroid(selectedZone)),
                                out RoadPolygonBoundaryProjection projection))
                        {
                            portalObject.transform.position = projection.worldPoint;
                            portalObject.transform.rotation = RoadPolygonAuthoringUtility.GetPortalRotation(
                                selectedZone,
                                projection.worldTangent);
                        }

                        polygonPortalId = string.Empty;
                        activePolygonZone = selectedZone;
                        selectedPolygonPortal = portal;
                        Selection.activeObject = portalObject;
                        MarkNetworkDirty();
                    }
                }

                if (GUILayout.Button("Open Road Network Query / Runtime Debug"))
                {
                    RoadNetworkRuntimeDebugWindow.Open();
                }
            }
        }

        private void DrawAdjacentLaneTools()
        {
            adjacentLanesSectionExpanded = DrawSectionFoldout("Adjacent Lanes", adjacentLanesSectionExpanded);
            if (!adjacentLanesSectionExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                bool targetValid = DrawTargetLanePicker();
                DrawAdjacentLaneCopy(targetValid);
                DrawAdjacentLaneInferenceSettings();
            }
        }

        private void DrawAdjacentLaneInferenceSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Adjacent Lane Inference", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(network == null))
            {
                if (network == null)
                {
                    EditorGUILayout.HelpBox("Select a RoadLaneNetwork to edit adjacent lane inference.", MessageType.Info);
                    return;
                }

                SerializedObject serializedNetwork = new SerializedObject(network);
                serializedNetwork.Update();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(
                    serializedNetwork.FindProperty("adjacentMinLateralDistance"),
                    new GUIContent("Min Lateral Distance"));
                EditorGUILayout.PropertyField(
                    serializedNetwork.FindProperty("adjacentMaxLateralDistance"),
                    new GUIContent("Max Lateral Distance"));
                EditorGUILayout.PropertyField(
                    serializedNetwork.FindProperty("adjacentHeadingTolerance"),
                    new GUIContent("Heading Tolerance"));
                EditorGUILayout.PropertyField(
                    serializedNetwork.FindProperty("adjacentMaxHeightDifference"),
                    new GUIContent("Max Height Difference"));
                EditorGUILayout.PropertyField(
                    serializedNetwork.FindProperty("adjacentMinimumOverlapLength"),
                    new GUIContent("Minimum Overlap Length"));
                bool settingsChanged = EditorGUI.EndChangeCheck();

                if (settingsChanged)
                {
                    serializedNetwork.ApplyModifiedProperties();
                    MarkNetworkDirty();
                    RepaintScene();
                }
                else
                {
                    serializedNetwork.ApplyModifiedProperties();
                }

                BakedLaneNetwork preview = RoadLaneNetworkEditor.GetAdjacentPreviewNetwork(network);
                string bakedCount = network.BakedNetwork == null
                    ? "none"
                    : network.BakedNetwork.AdjacentLinks.Count.ToString();
                string previewCount = !previewAdjacentLinks
                    ? "off"
                    : preview == null ? "none" : preview.AdjacentLinks.Count.ToString();
                EditorGUILayout.HelpBox(
                    "Baked adjacent links: " + bakedCount + "\nPreview adjacent links: " + previewCount,
                    MessageType.Info);

                EditorGUI.BeginChangeCheck();
                bool nextPreviewState = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Preview Adjacent Links",
                        "When enabled, inferred adjacent links are drawn continuously in Scene View using the current authoring settings."),
                    previewAdjacentLinks);
                if (EditorGUI.EndChangeCheck())
                {
                    SetAdjacentPreviewEnabled(nextPreviewState);
                }

                EditorGUI.BeginChangeCheck();
                showAdjacentInferenceArea = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Show Adjacent Inference Area",
                        "Draws the min/max lateral distance bands used to judge adjacent lane candidates."),
                    showAdjacentInferenceArea);
                if (EditorGUI.EndChangeCheck())
                {
                    RepaintScene();
                }

                if (!string.IsNullOrWhiteSpace(adjacentPreviewError))
                {
                    EditorGUILayout.HelpBox(adjacentPreviewError, MessageType.Warning);
                }
            }
        }

        private void DrawAlignmentTools()
        {
            laneAlignmentSectionExpanded = DrawSectionFoldout("Lane Alignment", laneAlignmentSectionExpanded);
            if (!laneAlignmentSectionExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                bool targetValid = DrawTargetLanePicker();

                alignmentHeightReference = (RoadLaneKnotHeightReference)EditorGUILayout.EnumPopup("Height Reference", alignmentHeightReference);
                using (new EditorGUI.DisabledScope(alignmentHeightReference != RoadLaneKnotHeightReference.Custom))
                {
                    alignmentCustomHeight = EditorGUILayout.FloatField("Custom Height", alignmentCustomHeight);
                }

                alignmentFlattenTangentHeights = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Flatten Tangent Heights",
                        "Also removes vertical Bezier tangent components when flattening."),
                    alignmentFlattenTangentHeights);
                alignmentGridSize = EditorGUILayout.FloatField("Alignment Grid Size", Mathf.Max(0.001f, alignmentGridSize));

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!targetValid))
                    {
                        if (GUILayout.Button("Flatten Lane Heights"))
                        {
                            int changed = RoadLaneAlignmentUtility.FlattenKnotHeights(
                                alignmentTarget,
                                alignmentHeightReference,
                                alignmentCustomHeight,
                                alignmentFlattenTangentHeights);
                            MarkNetworkDirty();
                            Debug.LogFormat(alignmentTarget, "Flattened {0} spline knot(s).", changed);
                            RepaintScene();
                        }

                        if (GUILayout.Button("Snap Lane To Road"))
                        {
                            int changed = RoadLaneAlignmentUtility.SnapKnotsToRoadColliders(
                                alignmentTarget,
                                roadLayerMask,
                                roadProjectionHeight);
                            MarkNetworkDirty();
                            Debug.LogFormat(alignmentTarget, "Snapped {0} spline knot(s) to road colliders.", changed);
                            RepaintScene();
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!targetValid))
                    {
                        if (GUILayout.Button("Snap Lane XZ To Grid"))
                        {
                            int changed = RoadLaneAlignmentUtility.SnapKnotPositionsToGrid(
                                alignmentTarget,
                                alignmentGridSize,
                                true,
                                false,
                                true);
                            MarkNetworkDirty();
                            Debug.LogFormat(alignmentTarget, "Snapped {0} spline knot(s) to the XZ grid.", changed);
                            RepaintScene();
                        }
                    }

                    using (new EditorGUI.DisabledScope(draftLanePoints.Count == 0))
                    {
                        if (GUILayout.Button("Flatten Draft"))
                        {
                            FlattenDraftLaneHeights();
                        }

                        if (GUILayout.Button("Snap Draft XZ"))
                        {
                            SnapDraftLanePointsToGrid();
                        }
                    }
                }
            }
        }

        private bool DrawTargetLanePicker()
        {
            RoadLane selectedLane = GetSelectedLaneInNetwork();
            if (alignmentTarget == null && selectedLane != null)
            {
                alignmentTarget = selectedLane;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                alignmentTarget = (RoadLane)EditorGUILayout.ObjectField(
                    "Target Lane",
                    alignmentTarget,
                    typeof(RoadLane),
                    true);
                using (new EditorGUI.DisabledScope(selectedLane == null))
                {
                    if (GUILayout.Button("Use Selected", GUILayout.Width(92f)))
                    {
                        alignmentTarget = selectedLane;
                    }
                }
            }

            bool targetValid = IsAlignmentTargetValid();
            if (alignmentTarget != null && !targetValid)
            {
                EditorGUILayout.HelpBox("Target Lane must belong to the selected RoadLaneNetwork.", MessageType.Warning);
            }

            return targetValid;
        }

        private void DrawPreviewAndValidation()
        {
            previewValidationSectionExpanded = DrawSectionFoldout("Preview & Validation", previewValidationSectionExpanded);
            if (!previewValidationSectionExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                if (liveNetworkPreview && network != null)
                {
                    BakedLaneNetwork preview = RoadNetworkLivePreviewCoordinator.GetPreview(network);
                    string previewError = RoadNetworkLivePreviewCoordinator.GetError(network);
                    if (!string.IsNullOrWhiteSpace(previewError))
                    {
                        EditorGUILayout.HelpBox(previewError, MessageType.Error);
                    }
                    else if (preview != null)
                    {
                        EditorGUILayout.HelpBox(
                            string.Format(
                                "Live Preview {0}: {1} lane(s), {2} connection(s), {3} adjacent link(s). Formal asset unchanged until Bake.",
                                preview.SchemaVersion,
                                preview.Summary.directedLaneCount,
                                preview.Summary.connectionCount,
                                preview.Summary.adjacentLinkCount),
                            MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Live Preview is waiting for the debounced rebuild.", MessageType.None);
                    }
                }

                if (validationIssues.Count > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Validation Issues", EditorStyles.boldLabel);
                    for (int i = 0; i < validationIssues.Count; i++)
                    {
                        RoadLaneValidationIssue issue = validationIssues[i];
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(issue.code + ": " + issue.message, EditorStyles.wordWrappedMiniLabel);
                            if (issue.lane != null && GUILayout.Button("Select", GUILayout.Width(56f)))
                            {
                                Selection.activeObject = issue.lane.gameObject;
                            }
                        }
                    }
                }
            }
        }

        private void DrawAdjacentLaneCopy(bool targetValid)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Adjacent Lane Copy", EditorStyles.boldLabel);
            adjacentLaneSpacing = EditorGUILayout.FloatField(
                "Lane Spacing",
                Mathf.Max(0.01f, adjacentLaneSpacing));

            string reason = string.Empty;
            bool canCopy = targetValid &&
                           RoadLaneAdjacentCopyUtility.CanCopy(alignmentTarget, out reason);
            if (!targetValid)
            {
                reason = "Choose a target lane from the selected RoadLaneNetwork.";
            }

            if (!canCopy)
            {
                EditorGUILayout.HelpBox(reason, MessageType.Info);
            }
            else if (!RoadLaneAdjacentCopyUtility.IsSpacingInsideInferenceRange(network, adjacentLaneSpacing))
            {
                EditorGUILayout.HelpBox(
                    string.Format(
                        "Spacing {0:0.###}m is outside this network's adjacent inference range ({1:0.###}m-{2:0.###}m). The copy is allowed, but Bake may not infer an adjacent link.",
                        adjacentLaneSpacing,
                        network.AdjacentMinLateralDistance,
                        network.AdjacentMaxLateralDistance),
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(!canCopy))
            {
                if (GUILayout.Button("Copy Left"))
                {
                    CopyAdjacentLane(RoadLaneAdjacentSide.Left);
                }

                if (GUILayout.Button("Copy Right"))
                {
                    CopyAdjacentLane(RoadLaneAdjacentSide.Right);
                }
            }
        }

        private void CopyAdjacentLane(RoadLaneAdjacentSide side)
        {
            if (RoadLaneAdjacentCopyUtility.TryCopyAdjacentLane(
                    alignmentTarget,
                    side,
                    adjacentLaneSpacing,
                    out RoadLane copiedLane,
                    out string error,
                    selectCreatedObjects))
            {
                alignmentTarget = copiedLane;
                RefreshAdjacentPreview();
                Debug.LogFormat(
                    copiedLane,
                    "Copied adjacent lane {0} to the {1} at {2:0.###}m spacing.",
                    copiedLane.LaneId,
                    side,
                    adjacentLaneSpacing);
                Repaint();
                RepaintScene();
                return;
            }

            Debug.LogWarning(error, alignmentTarget);
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (network == null)
            {
                return;
            }

            if (liveNetworkPreview)
            {
                RoadNetworkLivePreviewCoordinator.Draw(network);
            }

            if (showAdjacentInferenceArea)
            {
                DrawAdjacentInferenceArea();
            }

            if (previewAdjacentLinks && !liveNetworkPreview)
            {
                EnsureAdjacentPreview();
                RoadLaneNetworkEditor.DrawAdjacentLinkPreview(network, false);
            }

            if (!sceneToolActive)
            {
                return;
            }

            Event current = Event.current;
            HandleOperationShortcuts(current);

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (OperationUsesSceneInput(operation) && current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            DrawSceneOverlay();

            UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            try
            {
                switch (operation)
                {
                    case AuthoringOperation.Select:
                        HandleSelectScene(current);
                        break;
                    case AuthoringOperation.DrawLane:
                        DrawExistingLanePreview(true);
                        HandleDrawLaneScene(current);
                        break;
                    case AuthoringOperation.BuildJunction:
                        DrawExistingLanePreview(false);
                        HandleBuildJunctionScene(current);
                        break;
                    case AuthoringOperation.SplineEdit:
                        HandleSplineEditScene(current);
                        break;
                    case AuthoringOperation.Profile:
                        HandleProfileScene(current);
                        break;
                    case AuthoringOperation.Polygon:
                        HandlePolygonScene(current);
                        break;
                }
            }
            finally
            {
                Handles.zTest = previousZTest;
            }
        }

        private void DrawSceneOverlay()
        {
            Handles.BeginGUI();
            Rect rect = new Rect(12f, 12f, 560f, 118f);
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Label("Road Lane Authoring", EditorStyles.boldLabel);
            int nextOperation = GUILayout.Toolbar((int)operation, OperationLabels);
            if (nextOperation != (int)operation)
            {
                SetOperation((AuthoringOperation)nextOperation);
            }

            GUILayout.Space(2f);
            if (operation == AuthoringOperation.DrawLane)
            {
                GUILayout.Label("Left click: add knot    Enter/right click: create    Backspace: undo    Esc: clear", EditorStyles.miniLabel);
                GUILayout.Label("Draft knots: " + draftLanePoints.Count, EditorStyles.miniLabel);
            }
            else if (operation == AuthoringOperation.BuildJunction)
            {
                GUILayout.Label("Click lane endpoint handles to toggle bindings. Enter creates a junction.", EditorStyles.miniLabel);
                GUILayout.Label("Selected endpoints: " + draftJunctionBindings.Count, EditorStyles.miniLabel);
            }
            else if (operation == AuthoringOperation.SplineEdit)
            {
                GUILayout.Label("Drag any visible knot handle to move RoadLane or Connector Spline knots.", EditorStyles.miniLabel);
            }
            else if (operation == AuthoringOperation.Profile)
            {
                GUILayout.Label("Click a Profile Source knot, then assign an override or topology break in the window.", EditorStyles.miniLabel);
            }
            else if (operation == AuthoringOperation.Polygon)
            {
                GUILayout.Label("Left click: add zone vertex or portal    Enter/right click: create zone    Delete: remove selected vertex", EditorStyles.miniLabel);
                GUILayout.Label("Draft vertices: " + draftPolygonPoints.Count + "    Active Zone: " + (activePolygonZone == null ? "none" : activePolygonZone.ZoneId), EditorStyles.miniLabel);
            }
            else
            {
                GUILayout.Label("Click a lane, connector knot, or junction handle to select it without authoring.", EditorStyles.miniLabel);
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void HandleOperationShortcuts(Event current)
        {
            if (current.type != EventType.KeyDown || current.alt || current.control || current.command)
            {
                return;
            }

            AuthoringOperation nextOperation;
            switch (current.keyCode)
            {
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    nextOperation = AuthoringOperation.Select;
                    break;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    nextOperation = AuthoringOperation.DrawLane;
                    break;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    nextOperation = AuthoringOperation.BuildJunction;
                    break;
                case KeyCode.Alpha4:
                case KeyCode.Keypad4:
                    nextOperation = AuthoringOperation.SplineEdit;
                    break;
                case KeyCode.Alpha5:
                case KeyCode.Keypad5:
                    nextOperation = AuthoringOperation.Profile;
                    break;
                case KeyCode.Alpha6:
                case KeyCode.Keypad6:
                    nextOperation = AuthoringOperation.Polygon;
                    break;
                default:
                    return;
            }

            SetOperation(nextOperation);
            current.Use();
        }

        private void SetOperation(AuthoringOperation nextOperation)
        {
            if (operation == nextOperation)
            {
                return;
            }

            operation = nextOperation;
            GUI.FocusControl(null);
            Repaint();
            RepaintScene();
        }

        private static bool OperationUsesSceneInput(AuthoringOperation value)
        {
            return value == AuthoringOperation.DrawLane ||
                   value == AuthoringOperation.BuildJunction ||
                   value == AuthoringOperation.SplineEdit ||
                   value == AuthoringOperation.Profile ||
                   value == AuthoringOperation.Polygon;
        }

        private static string GetOperationTitle(AuthoringOperation value)
        {
            return value switch
            {
                AuthoringOperation.DrawLane => "2 Draw Lane",
                AuthoringOperation.BuildJunction => "3 Junction",
                AuthoringOperation.SplineEdit => "4 Spline Edit",
                AuthoringOperation.Profile => "5 Profile",
                AuthoringOperation.Polygon => "6 Polygon",
                _ => "1 Select"
            };
        }

        private void HandleDrawLaneScene(Event current)
        {
            DrawDraftLanePreview(current);

            if (current.alt)
            {
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && TryGetMouseWorldPoint(current.mousePosition, out Vector3 point))
            {
                Undo.RecordObject(this, "Add Road Lane Draft Point");
                draftLanePoints.Add(point);
                current.Use();
                Repaint();
                RepaintScene();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 1 && draftLanePoints.Count >= 2)
            {
                CreateLaneFromDraft();
                current.Use();
                return;
            }

            if (current.type == EventType.KeyDown)
            {
                if ((current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) &&
                    draftLanePoints.Count >= 2)
                {
                    CreateLaneFromDraft();
                    current.Use();
                }
                else if (current.keyCode == KeyCode.Backspace && draftLanePoints.Count > 0)
                {
                    draftLanePoints.RemoveAt(draftLanePoints.Count - 1);
                    current.Use();
                    Repaint();
                    RepaintScene();
                }
                else if (current.keyCode == KeyCode.Escape && draftLanePoints.Count > 0)
                {
                    draftLanePoints.Clear();
                    current.Use();
                    Repaint();
                    RepaintScene();
                }
            }
        }

        private void DrawDraftLanePreview(Event current)
        {
            if (draftLanePoints.Count == 0)
            {
                return;
            }

            List<Vector3> previewPoints = new List<Vector3>(draftLanePoints);
            if (!current.alt && TryGetMouseWorldPoint(current.mousePosition, out Vector3 hoverPoint))
            {
                previewPoints.Add(hoverPoint);
            }

            Color previousColor = Handles.color;
            Handles.color = DraftLaneColor;
            if (previewPoints.Count >= 2)
            {
                Handles.DrawAAPolyLine(4f, previewPoints.ToArray());
            }

            Handles.color = DraftPointColor;
            for (int i = 0; i < draftLanePoints.Count; i++)
            {
                float size = HandleUtility.GetHandleSize(draftLanePoints[i]) * DraftPointSize;
                Handles.SphereHandleCap(0, draftLanePoints[i], Quaternion.identity, size, EventType.Repaint);
            }

            Handles.color = previousColor;
        }

        private void HandleSplineEditScene(Event current)
        {
            DrawExistingLanePreview(false);

            RoadLane[] lanes = network.GetAuthoredLanes();
            Color previousColor = Handles.color;
            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                RoadLane lane = lanes[laneIndex];
                SplineContainer container = lane == null ? null : lane.SplineContainer;
                Spline spline = container == null ? null : container.Spline;
                if (spline == null)
                {
                    continue;
                }

                for (int knotIndex = 0; knotIndex < spline.Count; knotIndex++)
                {
                    BezierKnot knot = spline[knotIndex];
                    Vector3 worldPosition = container.transform.TransformPoint(knot.Position);
                    Handles.color = EditableKnotColor;
                    float capSize = HandleUtility.GetHandleSize(worldPosition) * EditableKnotSize;
                    Handles.SphereHandleCap(0, worldPosition, Quaternion.identity, capSize, EventType.Repaint);

                    EditorGUI.BeginChangeCheck();
                    Vector3 newWorldPosition = Handles.PositionHandle(worldPosition, Quaternion.identity);
                    if (!EditorGUI.EndChangeCheck())
                    {
                        continue;
                    }

                    Undo.RecordObject(container, "Move Road Lane Spline Knot");
                    newWorldPosition = ApplyAuthoringPointSnaps(newWorldPosition, snapSplineEditToRoadColliders);
                    Vector3 localPosition = container.transform.InverseTransformPoint(newWorldPosition);
                    knot.Position = new float3(localPosition.x, localPosition.y, localPosition.z);
                    spline.SetKnot(knotIndex, knot);
                    EditorUtility.SetDirty(container);
                    EditorUtility.SetDirty(lane);
                    if (knotIndex == 0 || knotIndex == spline.Count - 1)
                    {
                        pendingTopologyLane = lane;
                        pendingTopologyEndpoint = knotIndex == 0
                            ? RuntimeEndpoint.Start
                            : RuntimeEndpoint.End;
                    }
                    MarkNetworkDirty();
                }
            }

            Handles.color = previousColor;
            if (current.type == EventType.MouseUp && pendingTopologyLane != null)
            {
                TryApplyAutomaticTopology(pendingTopologyLane, pendingTopologyEndpoint);
                pendingTopologyLane = null;
            }
        }

        private void HandleProfileScene(Event current)
        {
            if (profileSource == null ||
                profileSource.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                RoadLaneProfileSource selected = Selection.activeGameObject == null
                    ? null
                    : Selection.activeGameObject.GetComponentInParent<RoadLaneProfileSource>();
                if (selected != null && selected.GetComponentInParent<RoadLaneNetwork>() == network)
                {
                    profileSource = selected;
                }
            }

            if (profileSource == null ||
                profileSource.SplineContainer == null ||
                profileSource.SplineContainer.Spline == null)
            {
                return;
            }

            profileSource.SynchronizeControlPoints();
            Spline spline = profileSource.SplineContainer.Spline;
            Color previous = Handles.color;
            for (int i = 0; i < spline.Count; i++)
            {
                Vector3 position = profileSource.SplineContainer.transform.TransformPoint(spline[i].Position);
                bool selected = i == profileKnotIndex;
                Handles.color = selected ? ActiveJunctionHandleColor : JunctionHandleColor;
                float size = HandleUtility.GetHandleSize(position) * 0.11f;
                if (Handles.Button(position, Quaternion.identity, size, size * 1.35f, Handles.SphereHandleCap))
                {
                    profileKnotIndex = i;
                    loadedProfileKnotIndex = i;
                    RoadLaneProfileControlPoint point = profileSource.ControlPoints[i];
                    profileOverride = point.profileOverride;
                    profileForceTopologyBreak = point.forceTopologyBreak;
                    Selection.activeObject = profileSource.gameObject;
                    Repaint();
                }

                string label = profileSource.ControlPoints[i].pointId;
                if (profileSource.ControlPoints[i].profileOverride != null)
                {
                    label += " / " + profileSource.ControlPoints[i].profileOverride.name;
                }
                if (profileSource.ControlPoints[i].forceTopologyBreak)
                {
                    label += " / break";
                }
                Handles.Label(position + Vector3.up * size, label, EditorStyles.miniLabel);
            }

            Handles.color = previous;
        }

        private void HandlePolygonScene(Event current)
        {
            SynchronizeActivePolygonSelection();
            DrawExistingLanePreview(false);
            DrawAuthoredPolygonZones();
            DrawDraftPolygonPreview(current);
            DrawSelectedPortalSuggestionLine();

            if (current.alt)
            {
                return;
            }

            HandlePolygonKeyboard(current);
            if (current.type == EventType.Used)
            {
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && TryGetMouseWorldPoint(current.mousePosition, out Vector3 point))
            {
                if (polygonPortalPlacementMode && IsActivePolygonValid())
                {
                    CreatePortalOnActivePolygon(point);
                }
                else if (!polygonPortalPlacementMode)
                {
                    Undo.RecordObject(this, "Add Road Polygon Draft Vertex");
                    draftPolygonPoints.Add(point);
                    Repaint();
                    RepaintScene();
                }

                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 1 && draftPolygonPoints.Count >= 3)
            {
                CreatePolygonZoneFromDraft();
                current.Use();
            }
        }

        private void HandlePolygonKeyboard(Event current)
        {
            if (current.type != EventType.KeyDown)
            {
                return;
            }

            if ((current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) &&
                draftPolygonPoints.Count >= 3)
            {
                CreatePolygonZoneFromDraft();
                current.Use();
            }
            else if (current.keyCode == KeyCode.Backspace && draftPolygonPoints.Count > 0)
            {
                draftPolygonPoints.RemoveAt(draftPolygonPoints.Count - 1);
                current.Use();
                Repaint();
                RepaintScene();
            }
            else if (current.keyCode == KeyCode.Escape && draftPolygonPoints.Count > 0)
            {
                draftPolygonPoints.Clear();
                current.Use();
                Repaint();
                RepaintScene();
            }
            else if ((current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace) &&
                     draftPolygonPoints.Count == 0 &&
                     IsActivePolygonValid() &&
                     selectedPolygonVertexIndex >= 0)
            {
                DeleteSelectedPolygonVertex();
                current.Use();
            }
        }

        private void DrawDraftPolygonPreview(Event current)
        {
            if (draftPolygonPoints.Count == 0)
            {
                return;
            }

            List<Vector3> previewPoints = new List<Vector3>(draftPolygonPoints);
            if (!current.alt && TryGetMouseWorldPoint(current.mousePosition, out Vector3 hoverPoint))
            {
                previewPoints.Add(hoverPoint);
            }

            Color previous = Handles.color;
            Handles.color = PolygonLineColor;
            if (previewPoints.Count >= 2)
            {
                Handles.DrawAAPolyLine(3f, previewPoints.ToArray());
                Handles.DrawDottedLine(previewPoints[previewPoints.Count - 1], previewPoints[0], 4f);
            }

            Handles.color = DraftPointColor;
            for (int i = 0; i < draftPolygonPoints.Count; i++)
            {
                float size = HandleUtility.GetHandleSize(draftPolygonPoints[i]) * DraftPointSize;
                Handles.SphereHandleCap(0, draftPolygonPoints[i], Quaternion.identity, size, EventType.Repaint);
            }

            Handles.color = previous;
        }

        private void DrawAuthoredPolygonZones()
        {
            RoadPolygonZone[] zones = network.GetPolygonZones();
            for (int i = 0; i < zones.Length; i++)
            {
                RoadPolygonZone zone = zones[i];
                if (zone == null)
                {
                    continue;
                }

                bool editable = zone == activePolygonZone;
                DrawPolygonZone(zone, editable);
            }
        }

        private void DrawPolygonZone(RoadPolygonZone zone, bool editable)
        {
            IReadOnlyList<Vector2> vertices = zone.Vertices;
            if (vertices == null || vertices.Count == 0)
            {
                return;
            }

            DrawPolygonFootprint(zone);
            if (showPolygonVolume)
            {
                DrawPolygonVolume(zone);
            }

            Vector3 centroid = zone.LocalVertexToWorld(RoadPolygonAuthoringUtility.GetLocalCentroid(zone));
            float centerSize = HandleUtility.GetHandleSize(centroid) * 0.08f;
            Handles.color = editable ? Handles.selectedColor : PolygonLineColor;
            if (Handles.Button(centroid, Quaternion.identity, centerSize, centerSize * 1.4f, Handles.CubeHandleCap))
            {
                activePolygonZone = zone;
                selectedPolygonVertexIndex = -1;
                Selection.activeObject = zone.gameObject;
                Repaint();
                RepaintScene();
            }

            Handles.Label(centroid + Vector3.up * centerSize, zone.ZoneId, EditorStyles.miniLabel);
            DrawPolygonPortals(zone, editable);
            if (!editable)
            {
                return;
            }

            DrawPolygonVertexHandles(zone);
            DrawPolygonEdgeInsertHandles(zone);
            if (showPolygonVolume)
            {
                DrawPolygonHeightHandles(zone);
            }
        }

        private void DrawPolygonFootprint(RoadPolygonZone zone)
        {
            Color previous = Handles.color;
            IReadOnlyList<Vector2> vertices = zone.Vertices;
            Handles.color = PolygonLineColor;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 a = zone.LocalVertexToWorld(vertices[i]);
                Vector3 b = zone.LocalVertexToWorld(vertices[(i + 1) % vertices.Count]);
                Handles.DrawLine(a, b, 3f);
            }

            Handles.color = PolygonFillColor;
            if (RoadPolygonGeometry.TryTriangulate(vertices, polygonTriangles, out _))
            {
                for (int i = 0; i + 2 < polygonTriangles.Count; i += 3)
                {
                    Handles.DrawAAConvexPolygon(
                        zone.LocalVertexToWorld(vertices[polygonTriangles[i]]),
                        zone.LocalVertexToWorld(vertices[polygonTriangles[i + 1]]),
                        zone.LocalVertexToWorld(vertices[polygonTriangles[i + 2]]));
                }
            }

            Handles.color = previous;
        }

        private void DrawPolygonVolume(RoadPolygonZone zone)
        {
            IReadOnlyList<Vector2> vertices = zone.Vertices;
            Color previous = Handles.color;
            Handles.color = PolygonTopLineColor;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 bottom = zone.LocalVertexToWorld(vertices[i]);
                Vector3 top = zone.LocalVertexToWorld(vertices[i], zone.Height);
                Vector3 nextTop = zone.LocalVertexToWorld(vertices[(i + 1) % vertices.Count], zone.Height);
                Handles.DrawLine(top, nextTop, 2f);
                Handles.DrawLine(bottom, top, 1f);
            }

            Handles.color = previous;
        }

        private void DrawPolygonVertexHandles(RoadPolygonZone zone)
        {
            Color previous = Handles.color;
            for (int i = 0; i < zone.Vertices.Count; i++)
            {
                Vector3 world = zone.LocalVertexToWorld(zone.Vertices[i]);
                Handles.color = i == selectedPolygonVertexIndex
                    ? PolygonSelectedVertexColor
                    : PolygonVertexColor;
                float size = HandleUtility.GetHandleSize(world) * PolygonVertexHandleSize;
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(
                    world,
                    size,
                    Vector3.zero,
                    Handles.DotHandleCap);
                if (!EditorGUI.EndChangeCheck())
                {
                    continue;
                }

                Undo.RecordObject(zone, "Move Road Polygon Vertex");
                moved = ApplyAuthoringPointSnaps(moved, true);
                zone.Vertices[i] = zone.WorldToLocalXZ(moved);
                selectedPolygonVertexIndex = i;
                EditorUtility.SetDirty(zone);
                MarkNetworkDirty();
            }

            Handles.color = previous;
        }

        private void DrawPolygonEdgeInsertHandles(RoadPolygonZone zone)
        {
            Color previous = Handles.color;
            Handles.color = PolygonInsertColor;
            for (int i = 0; i < zone.Vertices.Count; i++)
            {
                Vector3 a = zone.LocalVertexToWorld(zone.Vertices[i]);
                Vector3 b = zone.LocalVertexToWorld(zone.Vertices[(i + 1) % zone.Vertices.Count]);
                Vector3 midpoint = (a + b) * 0.5f;
                float size = HandleUtility.GetHandleSize(midpoint) * PolygonEdgeInsertHandleSize;
                if (!Handles.Button(midpoint, Quaternion.identity, size, size * 1.35f, Handles.CubeHandleCap))
                {
                    continue;
                }

                Undo.RecordObject(zone, "Insert Road Polygon Vertex");
                if (RoadPolygonAuthoringUtility.InsertVertexAfterEdge(zone, i, midpoint, out int insertedIndex))
                {
                    selectedPolygonVertexIndex = insertedIndex;
                    EditorUtility.SetDirty(zone);
                    MarkNetworkDirty();
                    Repaint();
                    RepaintScene();
                }
            }

            Handles.color = previous;
        }

        private void DrawPolygonHeightHandles(RoadPolygonZone zone)
        {
            Vector2 centroid = RoadPolygonAuthoringUtility.GetLocalCentroid(zone);
            Vector3 bottom = zone.LocalVertexToWorld(centroid);
            Vector3 top = zone.LocalVertexToWorld(centroid, zone.Height);
            Vector3 up = zone.transform.up.sqrMagnitude <= 0.0001f ? Vector3.up : zone.transform.up.normalized;
            Color previous = Handles.color;
            Handles.color = PolygonTopLineColor;

            float bottomSize = HandleUtility.GetHandleSize(bottom) * PolygonHeightHandleSize;
            Handles.CubeHandleCap(0, bottom, Quaternion.identity, bottomSize, EventType.Repaint);
            EditorGUI.BeginChangeCheck();
            Vector3 movedBottom = Handles.Slider(bottom, up, bottomSize, Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Adjust Road Polygon Minimum Height");
                float topLocalY = zone.MinimumHeight + zone.Height;
                float minimum = zone.transform.InverseTransformPoint(movedBottom).y;
                zone.MinimumHeight = minimum;
                zone.Height = Mathf.Max(0.1f, topLocalY - minimum);
                EditorUtility.SetDirty(zone);
                MarkNetworkDirty();
            }

            float topSize = HandleUtility.GetHandleSize(top) * PolygonHeightHandleSize;
            Handles.CubeHandleCap(0, top, Quaternion.identity, topSize, EventType.Repaint);
            EditorGUI.BeginChangeCheck();
            Vector3 movedTop = Handles.Slider(top, up, topSize, Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Adjust Road Polygon Height");
                float topLocalY = zone.transform.InverseTransformPoint(movedTop).y;
                zone.Height = Mathf.Max(0.1f, topLocalY - zone.MinimumHeight);
                EditorUtility.SetDirty(zone);
                MarkNetworkDirty();
            }

            Handles.DrawLine(bottom, top, 2f);
            Handles.Label(top + up * topSize, "Height " + zone.Height.ToString("0.00") + "m", EditorStyles.miniLabel);
            Handles.color = previous;
        }

        private void DrawPolygonPortals(RoadPolygonZone zone, bool editable)
        {
            RoadPortal[] portals = zone.GetPortals();
            Color previous = Handles.color;
            for (int i = 0; i < portals.Length; i++)
            {
                RoadPortal portal = portals[i];
                if (portal == null)
                {
                    continue;
                }

                bool selected = portal == selectedPolygonPortal;
                Handles.color = portal.LinkedLane == null && portal.LinkedPortal == null
                    ? PolygonPortalWarningColor
                    : selected ? Handles.selectedColor : PolygonPortalColor;
                Vector3 center = portal.transform.position;
                Vector3 right = portal.transform.right * portal.Width * 0.5f;
                Handles.DrawLine(center - right, center + right, 4f);
                float size = HandleUtility.GetHandleSize(center) * PolygonPortalHandleSize;
                if (editable)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 moved = Handles.FreeMoveHandle(
                        center,
                        size,
                        Vector3.zero,
                        Handles.SphereHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        MovePortalToBoundary(portal, moved);
                        selectedPolygonPortal = portal;
                        activePolygonZone = zone;
                        Selection.activeObject = portal.gameObject;
                    }
                }
                else
                {
                    Handles.SphereHandleCap(0, center, Quaternion.identity, size, EventType.Repaint);
                }

                if (portal.LinkedLane != null)
                {
                    if (RoadPolygonAuthoringUtility.TryGetEndpointWorldPosition(
                            portal.LinkedLane,
                            portal.LinkedLaneEndpoint,
                            out Vector3 target))
                    {
                        Handles.DrawDottedLine(center, target, 4f);
                    }
                }
                else if (portal.LinkedPortal != null)
                {
                    Handles.DrawDottedLine(center, portal.LinkedPortal.transform.position, 4f);
                }

                Handles.Label(center + Vector3.up * size, portal.PortalId, EditorStyles.miniLabel);
            }

            Handles.color = previous;
        }

        private void DrawSelectedPortalSuggestionLine()
        {
            if (!showPolygonPortalSuggestions ||
                selectedPolygonPortal == null ||
                selectedPolygonPortal.GetComponentInParent<RoadLaneNetwork>() != network ||
                !RoadPolygonAuthoringUtility.TryFindPortalSuggestion(
                    network,
                    selectedPolygonPortal,
                    polygonPortalSuggestionRadius,
                    out RoadPolygonPortalSuggestion suggestion))
            {
                return;
            }

            Color previous = Handles.color;
            Handles.color = PolygonSuggestionColor;
            Handles.DrawDottedLine(selectedPolygonPortal.transform.position, suggestion.targetPosition, 3f);
            Handles.Label(
                (selectedPolygonPortal.transform.position + suggestion.targetPosition) * 0.5f,
                "Suggested: " + suggestion.DisplayName,
                EditorStyles.miniLabel);
            Handles.color = previous;
        }

        private void HandleSelectScene(Event current)
        {
            DrawExistingLanePreview(true);
            DrawSelectJunctionHandles();

            if (current.type != EventType.MouseDown ||
                current.button != 0 ||
                current.alt ||
                current.control ||
                current.command)
            {
                return;
            }

            if (TryPickKnot(current.mousePosition, out RoadLane knotLane, out int knotIndex))
            {
                RoadLaneEditorSelectionUtility.SelectKnot(knotLane, knotIndex);
                current.Use();
                RepaintScene();
                return;
            }

            if (TryPickJunction(current.mousePosition, out RoadJunction junction))
            {
                RoadLaneEditorSelectionUtility.SelectJunction(junction);
                current.Use();
                RepaintScene();
                return;
            }

            if (TryPickLane(current.mousePosition, out RoadLane lane))
            {
                RoadLaneEditorSelectionUtility.SelectLane(lane);
                current.Use();
                RepaintScene();
            }
        }

        private void DrawExistingLanePreview(bool drawKnots)
        {
            RoadLane[] lanes = network.GetAuthoredLanes();
            Color previousColor = Handles.color;
            for (int i = 0; i < lanes.Length; i++)
            {
                DrawExistingLanePreview(lanes[i], drawKnots);
            }

            Handles.color = previousColor;
        }

        private static void DrawExistingLanePreview(RoadLane lane, bool drawKnots)
        {
            SplineContainer container = lane == null ? null : lane.SplineContainer;
            Spline spline = container == null ? null : container.Spline;
            if (spline == null)
            {
                return;
            }

            if (spline.Count >= 2)
            {
                Handles.color = RoadLaneEditorVisualUtility.GetLanePreviewColor(
                    lane,
                    ExistingLaneColor,
                    ExistingConnectorColor);
                bool selected = Selection.activeGameObject == lane.gameObject;
                RoadLaneEditorVisualUtility.DrawLaneWidthPreview(lane, Handles.color, selected);
                int curveCount = spline.Closed ? spline.Count : spline.Count - 1;
                for (int i = 0; i < curveCount; i++)
                {
                    BezierCurve curve = spline.GetCurve(i);
                    Handles.DrawBezier(
                        container.transform.TransformPoint(curve.P0),
                        container.transform.TransformPoint(curve.P3),
                        container.transform.TransformPoint(curve.P1),
                        container.transform.TransformPoint(curve.P2),
                        Handles.color,
                        null,
                        ExistingLaneLineWidth);
                }
            }

            if (!drawKnots)
            {
                return;
            }

            Handles.color = ExistingKnotColor;
            for (int i = 0; i < spline.Count; i++)
            {
                Vector3 position = container.transform.TransformPoint(spline[i].Position);
                float size = HandleUtility.GetHandleSize(position) * ExistingKnotSize;
                SelectableKnot selectableKnot = new SelectableKnot(new SplineInfo(container, 0), i);
                Handles.color = SplineSelection.Contains(selectableKnot)
                    ? Handles.selectedColor
                    : ExistingKnotColor;
                Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
            }
        }

        private void DrawAdjacentInferenceArea()
        {
            RoadLane[] lanes = network.GetAuthoredLanes();
            Color previousColor = Handles.color;
            UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            try
            {
                for (int i = 0; i < lanes.Length; i++)
                {
                    RoadLane lane = lanes[i];
                    if (lane == null || lane.Kind != RoadLaneKind.Standard)
                    {
                        continue;
                    }

                    if (lane.TravelDirection == RuntimeDirection.Reverse)
                    {
                        DrawAdjacentInferenceArea(lane, true);
                    }
                    else if (lane.TravelDirection == RuntimeDirection.Bidirectional)
                    {
                        DrawAdjacentInferenceArea(lane, false);
                        DrawAdjacentInferenceArea(lane, true);
                    }
                    else
                    {
                        DrawAdjacentInferenceArea(lane, false);
                    }
                }
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousZTest;
            }
        }

        private void DrawAdjacentInferenceArea(RoadLane lane, bool reverse)
        {
            float minDistance = network.AdjacentMinLateralDistance;
            float maxDistance = network.AdjacentMaxLateralDistance;
            if (maxDistance <= minDistance)
            {
                return;
            }

            float sampleSpacing = Mathf.Max(
                AdjacentInferenceAreaSampleSpacing,
                network.SampleSpacing * 2f);
            List<RoadLanePose> poses = adjacentAreaGeometry.SampleEqualDistance(lane, sampleSpacing, reverse);
            if (poses.Count < 2)
            {
                return;
            }

            DrawAdjacentInferenceAreaSide(poses, minDistance, maxDistance, RoadLaneAdjacentSide.Left);
            DrawAdjacentInferenceAreaSide(poses, minDistance, maxDistance, RoadLaneAdjacentSide.Right);
        }

        private static void DrawAdjacentInferenceAreaSide(
            List<RoadLanePose> poses,
            float minDistance,
            float maxDistance,
            RoadLaneAdjacentSide side)
        {
            int count = poses.Count;
            Vector3[] inner = new Vector3[count];
            Vector3[] outer = new Vector3[count];
            float sideSign = side == RoadLaneAdjacentSide.Left ? -1f : 1f;

            for (int i = 0; i < count; i++)
            {
                RoadLanePose pose = poses[i];
                Vector3 forward = pose.forward.sqrMagnitude > 0.000001f
                    ? pose.forward.normalized
                    : Vector3.forward;
                Vector3 up = pose.up.sqrMagnitude > 0.000001f
                    ? pose.up.normalized
                    : Vector3.up;
                Vector3 right = Vector3.Cross(up, forward);
                if (right.sqrMagnitude <= 0.000001f)
                {
                    right = Vector3.Cross(Vector3.up, forward);
                }

                right = right.sqrMagnitude > 0.000001f
                    ? right.normalized
                    : Vector3.right;
                Vector3 lateral = right * sideSign;
                inner[i] = pose.position + lateral * minDistance;
                outer[i] = pose.position + lateral * maxDistance;
            }

            Handles.color = AdjacentInferenceAreaFillColor;
            for (int i = 1; i < count; i++)
            {
                Handles.DrawAAConvexPolygon(inner[i - 1], inner[i], outer[i], outer[i - 1]);
            }

            Handles.color = AdjacentInferenceAreaInnerLineColor;
            Handles.DrawAAPolyLine(AdjacentInferenceAreaLineWidth, inner);
            Handles.color = AdjacentInferenceAreaOuterLineColor;
            Handles.DrawAAPolyLine(AdjacentInferenceAreaLineWidth, outer);
        }

        private void DrawSelectJunctionHandles()
        {
            RoadJunction[] junctions = network.GetJunctions();
            Color previousColor = Handles.color;
            for (int i = 0; i < junctions.Length; i++)
            {
                RoadJunction junction = junctions[i];
                if (junction == null)
                {
                    continue;
                }

                Vector3 position = junction.transform.position;
                bool selected = Selection.activeGameObject == junction.gameObject;
                Handles.color = selected ? Handles.selectedColor : JunctionHandleColor;
                float size = HandleUtility.GetHandleSize(position) * 0.22f;
                Handles.CubeHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(position + Vector3.up * size, junction.JunctionId, EditorStyles.miniLabel);
            }

            Handles.color = previousColor;
        }

        private bool TryPickKnot(Vector2 mousePosition, out RoadLane selectedLane, out int selectedKnotIndex)
        {
            selectedLane = null;
            selectedKnotIndex = -1;
            float closestDistance = SelectKnotPickRadius;
            RoadLane[] lanes = network.GetAuthoredLanes();
            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                RoadLane lane = lanes[laneIndex];
                SplineContainer container = lane == null ? null : lane.SplineContainer;
                Spline spline = container == null ? null : container.Spline;
                if (spline == null)
                {
                    continue;
                }

                for (int knotIndex = 0; knotIndex < spline.Count; knotIndex++)
                {
                    Vector3 worldPosition = container.transform.TransformPoint(spline[knotIndex].Position);
                    float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(worldPosition), mousePosition);
                    if (distance > closestDistance)
                    {
                        continue;
                    }

                    closestDistance = distance;
                    selectedLane = lane;
                    selectedKnotIndex = knotIndex;
                }
            }

            return selectedLane != null;
        }

        private bool TryPickJunction(Vector2 mousePosition, out RoadJunction selectedJunction)
        {
            selectedJunction = null;
            float closestDistance = SelectJunctionPickRadius;
            RoadJunction[] junctions = network.GetJunctions();
            for (int i = 0; i < junctions.Length; i++)
            {
                RoadJunction junction = junctions[i];
                if (junction == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(
                    HandleUtility.WorldToGUIPoint(junction.transform.position),
                    mousePosition);
                if (distance > closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                selectedJunction = junction;
            }

            return selectedJunction != null;
        }

        private bool TryPickLane(Vector2 mousePosition, out RoadLane selectedLane)
        {
            selectedLane = null;
            float closestDistance = SelectLanePickRadius;
            RoadLane[] lanes = network.GetAuthoredLanes();
            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                RoadLane lane = lanes[laneIndex];
                SplineContainer container = lane == null ? null : lane.SplineContainer;
                Spline spline = container == null ? null : container.Spline;
                if (spline == null || spline.Count < 2)
                {
                    continue;
                }

                int curveCount = spline.Closed ? spline.Count : spline.Count - 1;
                for (int curveIndex = 0; curveIndex < curveCount; curveIndex++)
                {
                    BezierCurve curve = spline.GetCurve(curveIndex);
                    Vector2 previous = HandleUtility.WorldToGUIPoint(
                        container.transform.TransformPoint(CurveUtility.EvaluatePosition(curve, 0f)));
                    for (int segmentIndex = 1; segmentIndex <= CurvePickSegmentCount; segmentIndex++)
                    {
                        float t = segmentIndex / (float)CurvePickSegmentCount;
                        Vector2 current = HandleUtility.WorldToGUIPoint(
                            container.transform.TransformPoint(CurveUtility.EvaluatePosition(curve, t)));
                        float distance = DistanceToLineSegment(mousePosition, previous, current);
                        if (distance <= closestDistance)
                        {
                            closestDistance = distance;
                            selectedLane = lane;
                        }

                        previous = current;
                    }
                }
            }

            return selectedLane != null;
        }

        private static float DistanceToLineSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f)
            {
                return Vector2.Distance(point, start);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private void HandleBuildJunctionScene(Event current)
        {
            DrawJunctionHandles();
            DrawLaneEndpointHandles();
            DrawActiveJunctionPreview();
            if (!HasActiveJunction())
            {
                DrawDraftJunctionPreview();
            }

            if (current.type != EventType.KeyDown)
            {
                return;
            }

            if (!HasActiveJunction() &&
                (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) &&
                draftJunctionBindings.Count >= 2)
            {
                CreateJunctionFromDraft();
                current.Use();
            }
            else if (current.keyCode == KeyCode.Escape && HasActiveJunction())
            {
                activeJunction = null;
                current.Use();
                Repaint();
                RepaintScene();
            }
            else if (current.keyCode == KeyCode.Escape && draftJunctionBindings.Count > 0)
            {
                draftJunctionBindings.Clear();
                current.Use();
                Repaint();
                RepaintScene();
            }
        }

        private void DrawJunctionHandles()
        {
            RoadJunction[] junctions = network.GetJunctions();
            Color previousColor = Handles.color;
            for (int i = 0; i < junctions.Length; i++)
            {
                RoadJunction junction = junctions[i];
                if (junction == null)
                {
                    continue;
                }

                Vector3 position = junction.transform.position;
                bool selected = junction == activeJunction;
                Handles.color = selected ? ActiveJunctionHandleColor : JunctionHandleColor;
                float size = HandleUtility.GetHandleSize(position) * 0.22f;
                if (Handles.Button(position, Quaternion.identity, size, size * 1.25f, Handles.CubeHandleCap))
                {
                    activeJunction = junction;
                    draftJunctionBindings.Clear();
                    Selection.activeObject = junction.gameObject;
                    Repaint();
                    RepaintScene();
                }

                Handles.Label(position + Vector3.up * size, junction.JunctionId, EditorStyles.miniLabel);
            }

            Handles.color = previousColor;
        }

        private void DrawLaneEndpointHandles()
        {
            RoadLane[] lanes = network.GetAuthoredLanes();
            Color previousColor = Handles.color;
            for (int i = 0; i < lanes.Length; i++)
            {
                RoadLane lane = lanes[i];
                if (lane == null || lane.Kind == RoadLaneKind.Connector)
                {
                    continue;
                }

                DrawLaneEndpointHandle(lane, RuntimeEndpoint.Start);
                DrawLaneEndpointHandle(lane, RuntimeEndpoint.End);
            }

            Handles.color = previousColor;
        }

        private void DrawLaneEndpointHandle(RoadLane lane, RuntimeEndpoint endpoint)
        {
            if (!TryGetEndpointWorldPosition(lane, endpoint, out Vector3 position))
            {
                return;
            }

            bool selected = HasActiveJunction()
                ? ActiveJunctionContainsBinding(lane, endpoint)
                : ContainsBinding(lane, endpoint);
            Handles.color = selected ? JunctionBindingColor : EndpointColor;
            float size = HandleUtility.GetHandleSize(position) * EndpointHandleSize;
            if (Handles.Button(position, Quaternion.identity, size, size * 1.4f, Handles.SphereHandleCap))
            {
                ToggleJunctionBinding(lane, endpoint);
            }

            Handles.Label(position + Vector3.up * size, lane.LaneId + " " + endpoint, EditorStyles.miniLabel);
        }

        private void DrawActiveJunctionPreview()
        {
            if (!HasActiveJunction())
            {
                return;
            }

            Vector3 center = activeJunction.transform.position;
            Color previousColor = Handles.color;
            Handles.color = ActiveJunctionBindingColor;
            for (int i = 0; i < activeJunction.Bindings.Count; i++)
            {
                RoadJunctionBinding binding = activeJunction.Bindings[i];
                if (binding == null ||
                    binding.lane == null ||
                    !TryGetEndpointWorldPosition(binding.lane, binding.endpoint, out Vector3 endpointPosition))
                {
                    continue;
                }

                Handles.DrawDottedLine(center, endpointPosition, 4f);
            }

            Handles.color = previousColor;
        }

        private void DrawDraftJunctionPreview()
        {
            if (draftJunctionBindings.Count == 0)
            {
                return;
            }

            Vector3 center = GetDraftJunctionCenter();
            Color previousColor = Handles.color;
            Handles.color = JunctionBindingColor;
            float centerSize = HandleUtility.GetHandleSize(center) * 0.18f;
            Handles.SphereHandleCap(0, center, Quaternion.identity, centerSize, EventType.Repaint);
            for (int i = 0; i < draftJunctionBindings.Count; i++)
            {
                JunctionEndpointDraft draft = draftJunctionBindings[i];
                if (draft.lane == null ||
                    !TryGetEndpointWorldPosition(draft.lane, draft.endpoint, out Vector3 endpointPosition))
                {
                    continue;
                }

                Handles.DrawDottedLine(center, endpointPosition, 4f);
            }

            Handles.color = previousColor;
        }

        private void ToggleJunctionBinding(RoadLane lane, RuntimeEndpoint endpoint)
        {
            if (HasActiveJunction())
            {
                ToggleActiveJunctionBinding(lane, endpoint);
                return;
            }

            for (int i = 0; i < draftJunctionBindings.Count; i++)
            {
                JunctionEndpointDraft existing = draftJunctionBindings[i];
                if (existing.lane == lane && existing.endpoint == endpoint)
                {
                    draftJunctionBindings.RemoveAt(i);
                    Repaint();
                    RepaintScene();
                    return;
                }
            }

            draftJunctionBindings.Add(new JunctionEndpointDraft(lane, endpoint));
            Repaint();
            RepaintScene();
        }

        private bool ContainsBinding(RoadLane lane, RuntimeEndpoint endpoint)
        {
            for (int i = 0; i < draftJunctionBindings.Count; i++)
            {
                JunctionEndpointDraft draft = draftJunctionBindings[i];
                if (draft.lane == lane && draft.endpoint == endpoint)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasActiveJunction()
        {
            return activeJunction != null && activeJunction.GetComponentInParent<RoadLaneNetwork>() == network;
        }

        private bool ActiveJunctionContainsBinding(RoadLane lane, RuntimeEndpoint endpoint)
        {
            if (!HasActiveJunction())
            {
                return false;
            }

            for (int i = 0; i < activeJunction.Bindings.Count; i++)
            {
                RoadJunctionBinding binding = activeJunction.Bindings[i];
                if (binding != null && binding.lane == lane && binding.endpoint == endpoint)
                {
                    return true;
                }
            }

            return false;
        }

        private void ToggleActiveJunctionBinding(RoadLane lane, RuntimeEndpoint endpoint)
        {
            if (!HasActiveJunction())
            {
                return;
            }

            for (int i = activeJunction.Bindings.Count - 1; i >= 0; i--)
            {
                RoadJunctionBinding binding = activeJunction.Bindings[i];
                if (binding != null && binding.lane == lane && binding.endpoint == endpoint)
                {
                    RemoveActiveJunctionBindingAt(i);
                    return;
                }
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Road Junction Endpoint");
            Undo.RegisterFullObjectHierarchyUndo(activeJunction.gameObject, "Add Road Junction Endpoint");
            activeJunction.Bindings.Add(new RoadJunctionBinding
            {
                lane = lane,
                endpoint = endpoint
            });
            RefreshActiveJunctionConnectors();
            Undo.CollapseUndoOperations(undoGroup);
        }

        private void RemoveActiveJunctionBindingAt(int index)
        {
            if (!HasActiveJunction() || index < 0 || index >= activeJunction.Bindings.Count)
            {
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Road Junction Endpoint");
            Undo.RegisterFullObjectHierarchyUndo(activeJunction.gameObject, "Remove Road Junction Endpoint");
            activeJunction.Bindings.RemoveAt(index);
            RefreshActiveJunctionConnectors();
            Undo.CollapseUndoOperations(undoGroup);
        }

        private void RefreshActiveJunctionConnectors()
        {
            if (!HasActiveJunction())
            {
                return;
            }

            network.RefreshConnectors(
                activeJunction,
                created => Undo.RegisterCreatedObjectUndo(created, "Create Road Connector"));
            EditorUtility.SetDirty(activeJunction);
            MarkGeneratedConnectorsDirty(activeJunction);
            MarkNetworkDirty();
            Repaint();
            RepaintScene();
        }

        private void CreateLaneFromDraft()
        {
            if (network == null || draftLanePoints.Count < 2)
            {
                return;
            }

            string id = GetUniqueLaneId(network, laneId, laneIdPrefix);
            GameObject laneObject = new GameObject(id);
            laneObject.transform.SetParent(network.transform, false);
            SplineContainer container = laneObject.AddComponent<SplineContainer>();
            RoadLane lane = laneObject.AddComponent<RoadLane>();

            Spline spline = new Spline(draftLanePoints.Count, false);
            for (int i = 0; i < draftLanePoints.Count; i++)
            {
                spline.Add(container.transform.InverseTransformPoint(draftLanePoints[i]), TangentMode.Linear);
            }

            spline.Closed = false;
            container.Spline = spline;

            lane.LaneId = id;
            lane.TravelDirection = laneDirection;
            lane.SpeedLimit = laneSpeedLimit;
            lane.Open = laneOpen;
            lane.ConnectionMode = laneConnectionMode;
            lane.ManualNextLaneIds = manualNextLaneIds;
            lane.LateralOffset = lateralOffset;
            lane.VerticalOffset = verticalOffset;
            lane.SampleSpacingOverride = sampleSpacingOverride;

            Undo.RegisterCreatedObjectUndo(laneObject, "Create Road Lane");
            EditorUtility.SetDirty(container);
            EditorUtility.SetDirty(lane);
            MarkNetworkDirty();
            if (endpointSnap)
            {
                TryApplyAutomaticTopology(lane, RuntimeEndpoint.Start);
                TryApplyAutomaticTopology(lane, RuntimeEndpoint.End);
            }

            draftLanePoints.Clear();
            laneId = string.Empty;
            if (selectCreatedObjects)
            {
                Selection.activeObject = laneObject;
            }

            Repaint();
            RepaintScene();
        }

        private void CreateJunctionFromDraft()
        {
            if (network == null || draftJunctionBindings.Count < 2)
            {
                return;
            }

            RemoveInvalidJunctionBindings();
            if (draftJunctionBindings.Count < 2)
            {
                return;
            }

            string id = GetUniqueJunctionId(network, junctionId, junctionIdPrefix);
            GameObject junctionObject = new GameObject(id);
            junctionObject.transform.position = GetDraftJunctionCenter();
            junctionObject.transform.SetParent(network.transform, true);
            RoadJunction junction = junctionObject.AddComponent<RoadJunction>();
            junction.JunctionId = id;
            junction.AllowedTurns = junctionAllowedTurns;
            junction.ConnectorHandleScale = connectorHandleScale;

            SerializedObject serializedJunction = new SerializedObject(junction);
            serializedJunction.FindProperty("connectorBaseCost").floatValue = Mathf.Max(0f, connectorBaseCost);
            serializedJunction.FindProperty("connectorSpeedLimit").floatValue = Mathf.Max(0f, connectorSpeedLimit);
            serializedJunction.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; i < draftJunctionBindings.Count; i++)
            {
                JunctionEndpointDraft draft = draftJunctionBindings[i];
                junction.Bindings.Add(new RoadJunctionBinding
                {
                    lane = draft.lane,
                    endpoint = draft.endpoint
                });
            }

            Undo.RegisterCreatedObjectUndo(junctionObject, "Create Road Junction");
            RoadLaneConnectorReport report = network.RefreshConnectors(
                junction,
                created => Undo.RegisterCreatedObjectUndo(created, "Create Road Connector"));

            EditorUtility.SetDirty(junction);
            MarkGeneratedConnectorsDirty(junction);
            MarkNetworkDirty();

            Debug.LogFormat(
                junction,
                "Created road junction {0}: {1} connector(s) created, {2} updated, {3} locked, {4} orphaned.",
                id,
                report.created,
                report.updated,
                report.locked,
                report.orphaned);

            draftJunctionBindings.Clear();
            junctionId = string.Empty;
            if (selectCreatedObjects)
            {
                Selection.activeObject = junctionObject;
            }

            Repaint();
            RepaintScene();
        }

        private void CreatePolygonZoneFromDraft()
        {
            if (network == null || draftPolygonPoints.Count < 3)
            {
                return;
            }

            string id = RoadPolygonAuthoringUtility.GetUniqueZoneId(
                network,
                polygonZoneId,
                polygonZoneIdPrefix);
            GameObject zoneObject = new GameObject(id);
            Undo.RegisterCreatedObjectUndo(zoneObject, "Create Road Polygon Zone");
            Undo.SetTransformParent(zoneObject.transform, network.transform, "Parent Road Polygon Zone");
            RoadPolygonZone zone = Undo.AddComponent<RoadPolygonZone>(zoneObject);
            zone.ZoneId = id;
            zone.Vertices.Clear();

            float minimumHeight = float.PositiveInfinity;
            float maximumHeight = float.NegativeInfinity;
            for (int i = 0; i < draftPolygonPoints.Count; i++)
            {
                Vector3 local = zone.transform.InverseTransformPoint(draftPolygonPoints[i]);
                zone.Vertices.Add(new Vector2(local.x, local.z));
                minimumHeight = Mathf.Min(minimumHeight, local.y);
                maximumHeight = Mathf.Max(maximumHeight, local.y);
            }

            if (float.IsInfinity(minimumHeight) ||
                float.IsInfinity(maximumHeight) ||
                float.IsNaN(minimumHeight) ||
                float.IsNaN(maximumHeight))
            {
                minimumHeight = 0f;
                maximumHeight = 3f;
            }

            zone.MinimumHeight = minimumHeight;
            zone.Height = Mathf.Max(3f, maximumHeight - minimumHeight);
            EditorUtility.SetDirty(zone);
            MarkNetworkDirty();

            draftPolygonPoints.Clear();
            polygonZoneId = string.Empty;
            activePolygonZone = zone;
            selectedPolygonVertexIndex = -1;
            if (selectCreatedObjects)
            {
                Selection.activeObject = zoneObject;
            }

            Repaint();
            RepaintScene();
        }

        private void CreatePortalOnActivePolygon(Vector3 worldPoint)
        {
            if (!IsActivePolygonValid() ||
                !RoadPolygonAuthoringUtility.TryProjectToBoundary(
                    activePolygonZone,
                    worldPoint,
                    out RoadPolygonBoundaryProjection projection))
            {
                return;
            }

            string id = RoadPolygonAuthoringUtility.GetUniquePortalId(
                activePolygonZone,
                polygonPortalId,
                polygonPortalIdPrefix);
            GameObject portalObject = new GameObject(id);
            Undo.RegisterCreatedObjectUndo(portalObject, "Create Road Portal");
            Undo.SetTransformParent(portalObject.transform, activePolygonZone.transform, "Parent Road Portal");
            RoadPortal portal = Undo.AddComponent<RoadPortal>(portalObject);
            portal.PortalId = id;
            portalObject.transform.position = projection.worldPoint;
            portalObject.transform.rotation = RoadPolygonAuthoringUtility.GetPortalRotation(
                activePolygonZone,
                projection.worldTangent);

            selectedPolygonPortal = portal;
            polygonPortalId = string.Empty;
            polygonSuggestionKey = string.Empty;
            if (selectCreatedObjects)
            {
                Selection.activeObject = portalObject;
            }

            EditorUtility.SetDirty(portal);
            MarkNetworkDirty();
            Repaint();
            RepaintScene();
        }

        private void MovePortalToBoundary(RoadPortal portal, Vector3 worldPoint)
        {
            RoadPolygonZone zone = portal == null ? null : portal.SourceZone;
            if (zone == null ||
                !RoadPolygonAuthoringUtility.TryProjectToBoundary(
                    zone,
                    ApplyAuthoringPointSnaps(worldPoint, true),
                    out RoadPolygonBoundaryProjection projection))
            {
                return;
            }

            Undo.RecordObject(portal.transform, "Move Road Portal");
            portal.transform.position = projection.worldPoint;
            portal.transform.rotation = RoadPolygonAuthoringUtility.GetPortalRotation(
                zone,
                projection.worldTangent);
            EditorUtility.SetDirty(portal);
            MarkNetworkDirty();
            Repaint();
            RepaintScene();
        }

        private void DeleteSelectedPolygonVertex()
        {
            if (!IsActivePolygonValid())
            {
                return;
            }

            Undo.RecordObject(activePolygonZone, "Delete Road Polygon Vertex");
            if (!RoadPolygonAuthoringUtility.RemoveVertexAt(activePolygonZone, selectedPolygonVertexIndex))
            {
                return;
            }

            selectedPolygonVertexIndex = Mathf.Min(
                selectedPolygonVertexIndex,
                activePolygonZone.Vertices.Count - 1);
            EditorUtility.SetDirty(activePolygonZone);
            MarkNetworkDirty();
            Repaint();
            RepaintScene();
        }

        private bool IsActivePolygonValid()
        {
            return network != null &&
                   activePolygonZone != null &&
                   activePolygonZone.GetComponentInParent<RoadLaneNetwork>() == network;
        }

        private void SynchronizeActivePolygonSelection()
        {
            RoadPolygonZone selectedZone = FindSelectedPolygonZone();
            if (selectedZone != null && selectedZone != activePolygonZone)
            {
                activePolygonZone = selectedZone;
                selectedPolygonVertexIndex = -1;
            }

            RoadPortal selectedPortal = FindSelectedPortal();
            if (selectedPortal != null && selectedPortal != selectedPolygonPortal)
            {
                selectedPolygonPortal = selectedPortal;
                polygonSuggestionKey = string.Empty;
                if (selectedPortal.SourceZone != null)
                {
                    activePolygonZone = selectedPortal.SourceZone;
                }
            }

            if (activePolygonZone != null && activePolygonZone.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                activePolygonZone = null;
                selectedPolygonVertexIndex = -1;
            }

            if (selectedPolygonPortal != null && selectedPolygonPortal.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                selectedPolygonPortal = null;
                polygonSuggestionKey = string.Empty;
            }
        }

        private bool TryGetMouseWorldPoint(Vector2 mousePosition, out Vector3 point)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (snapToRoadColliders &&
                Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    Mathf.Max(MinimumRayDistance, roadRayDistance),
                    roadLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                point = ApplyAuthoringPointSnaps(hit.point, true);
                return true;
            }

            Plane fallbackPlane = new Plane(Vector3.up, new Vector3(0f, fallbackPlaneY, 0f));
            if (fallbackPlane.Raycast(ray, out float distance))
            {
                point = ApplyAuthoringPointSnaps(ray.GetPoint(distance), true);
                return true;
            }

            point = default;
            return false;
        }

        private void TryApplyAutomaticTopology(RoadLane lane, RuntimeEndpoint endpoint)
        {
            if (!endpointSnap || network == null || lane == null)
            {
                return;
            }

            if (RoadLaneTopologyUtility.TryAutoConnect(
                    network,
                    lane,
                    endpoint,
                    endpointSnapRadius,
                    laneInteriorSnapRadius,
                    endpointDirectionTolerance,
                    autoCreateJunction,
                    out RoadLaneTopologyBuildResult result))
            {
                MarkNetworkDirty();
                if (!string.IsNullOrWhiteSpace(result.message))
                {
                    Debug.Log(result.message, lane);
                }
            }
            else if (!string.IsNullOrWhiteSpace(result.message))
            {
                Debug.LogWarning(result.message, lane);
            }
        }

        private Vector3 ApplyAuthoringPointSnaps(Vector3 point, bool allowRoadProjection)
        {
            if (snapAuthoringXzToGrid)
            {
                point = RoadLaneAlignmentUtility.SnapPointToGrid(
                    point,
                    authoringGridSize,
                    true,
                    false,
                    true);
            }

            if (allowRoadProjection &&
                snapToRoadColliders &&
                RoadLaneAlignmentUtility.TryProjectPointToRoad(
                    point,
                    roadLayerMask,
                    roadProjectionHeight,
                    out Vector3 projectedPoint))
            {
                point = projectedPoint;
            }

            return point;
        }

        private RoadLane GetSelectedLaneInNetwork()
        {
            GameObject selectedObject = Selection.activeGameObject;
            RoadLane selectedLane = selectedObject == null ? null : selectedObject.GetComponentInParent<RoadLane>();
            if (selectedLane == null || selectedLane.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                return null;
            }

            return selectedLane;
        }

        private bool IsAlignmentTargetValid()
        {
            return network != null &&
                   alignmentTarget != null &&
                   alignmentTarget.GetComponentInParent<RoadLaneNetwork>() == network;
        }

        private void FlattenDraftLaneHeights()
        {
            if (draftLanePoints.Count == 0 || !TryResolveDraftHeight(out float targetHeight))
            {
                return;
            }

            Undo.RecordObject(this, "Flatten Road Lane Draft");
            for (int i = 0; i < draftLanePoints.Count; i++)
            {
                Vector3 point = draftLanePoints[i];
                point.y = targetHeight;
                draftLanePoints[i] = point;
            }

            Repaint();
            RepaintScene();
        }

        private void SnapDraftLanePointsToGrid()
        {
            if (draftLanePoints.Count == 0)
            {
                return;
            }

            Undo.RecordObject(this, "Snap Road Lane Draft To Grid");
            for (int i = 0; i < draftLanePoints.Count; i++)
            {
                draftLanePoints[i] = RoadLaneAlignmentUtility.SnapPointToGrid(
                    draftLanePoints[i],
                    alignmentGridSize,
                    true,
                    false,
                    true);
            }

            Repaint();
            RepaintScene();
        }

        private bool TryResolveDraftHeight(out float height)
        {
            if (alignmentHeightReference == RoadLaneKnotHeightReference.Custom)
            {
                height = alignmentCustomHeight;
                return true;
            }

            if (draftLanePoints.Count == 0)
            {
                height = 0f;
                return false;
            }

            if (alignmentHeightReference == RoadLaneKnotHeightReference.LastKnot)
            {
                height = draftLanePoints[draftLanePoints.Count - 1].y;
                return true;
            }

            if (alignmentHeightReference == RoadLaneKnotHeightReference.Average)
            {
                float sum = 0f;
                for (int i = 0; i < draftLanePoints.Count; i++)
                {
                    sum += draftLanePoints[i].y;
                }

                height = sum / draftLanePoints.Count;
                return true;
            }

            height = draftLanePoints[0].y;
            return true;
        }

        private Vector3 GetDraftJunctionCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < draftJunctionBindings.Count; i++)
            {
                JunctionEndpointDraft draft = draftJunctionBindings[i];
                if (draft.lane == null ||
                    !TryGetEndpointWorldPosition(draft.lane, draft.endpoint, out Vector3 position))
                {
                    continue;
                }

                sum += position;
                count++;
            }

            return count == 0 ? network.transform.position : sum / count;
        }

        private void RemoveInvalidJunctionBindings()
        {
            for (int i = draftJunctionBindings.Count - 1; i >= 0; i--)
            {
                JunctionEndpointDraft draft = draftJunctionBindings[i];
                if (draft.lane == null || !TryGetEndpointWorldPosition(draft.lane, draft.endpoint, out _))
                {
                    draftJunctionBindings.RemoveAt(i);
                }
            }
        }

        private static bool TryGetEndpointWorldPosition(RoadLane lane, RuntimeEndpoint endpoint, out Vector3 position)
        {
            SplineContainer container = lane == null ? null : lane.SplineContainer;
            Spline spline = container == null ? null : container.Spline;
            if (spline == null || spline.Count == 0)
            {
                position = default;
                return false;
            }

            position = container.EvaluatePosition(endpoint == RuntimeEndpoint.Start ? 0f : 1f);
            return true;
        }

        private void SetNetwork(RoadLaneNetwork nextNetwork)
        {
            if (network == nextNetwork)
            {
                return;
            }

            RoadLaneNetwork previousNetwork = network;
            if (previousNetwork != null)
            {
                if (liveNetworkPreview)
                {
                    RoadNetworkLivePreviewCoordinator.Unregister(previousNetwork);
                }
                RoadLaneNetworkEditor.ClearAdjacentPreviewNetwork(previousNetwork);
            }

            network = nextNetwork;
            adjacentPreviewError = string.Empty;
            if (liveNetworkPreview && network != null)
            {
                RoadNetworkLivePreviewCoordinator.Register(network);
            }

            if (activeJunction != null &&
                activeJunction.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                activeJunction = null;
            }

            if (alignmentTarget != null &&
                alignmentTarget.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                alignmentTarget = null;
            }

            if (activePolygonZone != null &&
                activePolygonZone.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                activePolygonZone = null;
                selectedPolygonVertexIndex = -1;
            }

            if (selectedPolygonPortal != null &&
                selectedPolygonPortal.GetComponentInParent<RoadLaneNetwork>() != network)
            {
                selectedPolygonPortal = null;
                polygonSuggestionKey = string.Empty;
            }

            RefreshAdjacentPreview();
            RepaintScene();
        }

        private void SetLiveNetworkPreview(bool enabled)
        {
            if (liveNetworkPreview == enabled)
            {
                return;
            }

            liveNetworkPreview = enabled;
            if (enabled)
            {
                RoadNetworkLivePreviewCoordinator.Register(network);
                RoadNetworkLivePreviewCoordinator.MarkDirty(network);
            }
            else
            {
                RoadNetworkLivePreviewCoordinator.Unregister(network);
            }

            Repaint();
            RepaintScene();
        }

        private void SetAdjacentPreviewEnabled(bool enabled)
        {
            if (previewAdjacentLinks == enabled)
            {
                return;
            }

            previewAdjacentLinks = enabled;
            adjacentPreviewError = string.Empty;
            if (enabled)
            {
                RefreshAdjacentPreview();
            }
            else
            {
                RoadLaneNetworkEditor.ClearAdjacentPreviewNetwork(network);
            }

            Repaint();
            RepaintScene();
        }

        private void RefreshAdjacentPreview()
        {
            if (!previewAdjacentLinks || network == null)
            {
                return;
            }

            if (liveNetworkPreview)
            {
                RoadNetworkLivePreviewCoordinator.MarkDirty(network);
                adjacentPreviewError = RoadNetworkLivePreviewCoordinator.GetError(network);
                return;
            }

            try
            {
                BakedLaneNetwork previewNetwork = network.BakeNetwork();
                previewNetwork.hideFlags = HideFlags.HideAndDontSave;
                RoadLaneNetworkEditor.SetAdjacentPreviewNetwork(network, previewNetwork);
                adjacentPreviewError = string.Empty;
            }
            catch (Exception exception)
            {
                RoadLaneNetworkEditor.ClearAdjacentPreviewNetwork(network);
                adjacentPreviewError = "Failed to preview adjacent links: " + exception.Message;
            }
        }

        private void EnsureAdjacentPreview()
        {
            if (previewAdjacentLinks &&
                network != null &&
                RoadLaneNetworkEditor.GetAdjacentPreviewNetwork(network) == null)
            {
                RefreshAdjacentPreview();
            }
        }

        private static RoadLaneNetwork FindSelectedNetwork()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return null;
            }

            RoadLaneNetwork selectedNetwork = selected.GetComponent<RoadLaneNetwork>();
            return selectedNetwork != null ? selectedNetwork : selected.GetComponentInParent<RoadLaneNetwork>();
        }

        private static RoadJunction FindSelectedJunction()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return null;
            }

            RoadJunction selectedJunction = selected.GetComponent<RoadJunction>();
            return selectedJunction != null ? selectedJunction : selected.GetComponentInParent<RoadJunction>();
        }

        private RoadPolygonZone FindSelectedPolygonZone()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return null;
            }

            RoadPolygonZone selectedZone = selected.GetComponent<RoadPolygonZone>();
            selectedZone = selectedZone != null ? selectedZone : selected.GetComponentInParent<RoadPolygonZone>();
            return selectedZone != null && selectedZone.GetComponentInParent<RoadLaneNetwork>() == network
                ? selectedZone
                : null;
        }

        private RoadPortal FindSelectedPortal()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return null;
            }

            RoadPortal selectedPortal = selected.GetComponent<RoadPortal>();
            selectedPortal = selectedPortal != null ? selectedPortal : selected.GetComponentInParent<RoadPortal>();
            return selectedPortal != null && selectedPortal.GetComponentInParent<RoadLaneNetwork>() == network
                ? selectedPortal
                : null;
        }

        private static string GetUniqueLaneId(RoadLaneNetwork network, string requestedId, string prefix)
        {
            HashSet<string> existing = new HashSet<string>(
                network.GetAuthoredLanes()
                    .Where(lane => lane != null)
                    .Select(lane => RoadLaneNetwork.SanitizeId(lane.LaneId)),
                StringComparer.Ordinal);
            return GetUniqueId(existing, requestedId, prefix, "lane");
        }

        private static string GetUniqueJunctionId(RoadLaneNetwork network, string requestedId, string prefix)
        {
            HashSet<string> existing = new HashSet<string>(
                network.GetJunctions()
                    .Where(junction => junction != null)
                    .Select(junction => RoadLaneNetwork.SanitizeId(junction.JunctionId)),
                StringComparer.Ordinal);
            return GetUniqueId(existing, requestedId, prefix, "junction");
        }

        private static string GetUniqueId(HashSet<string> existing, string requestedId, string prefix, string fallbackPrefix)
        {
            string requested = RoadLaneNetwork.SanitizeId(requestedId);
            if (!string.IsNullOrWhiteSpace(requested) && !existing.Contains(requested))
            {
                return requested;
            }

            string sanitizedPrefix = RoadLaneNetwork.SanitizeId(prefix);
            if (string.IsNullOrWhiteSpace(sanitizedPrefix))
            {
                sanitizedPrefix = fallbackPrefix;
            }

            for (int i = 1; i < 100000; i++)
            {
                string candidate = sanitizedPrefix + "_" + i.ToString("D3");
                if (!existing.Contains(candidate))
                {
                    return candidate;
                }
            }

            return sanitizedPrefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private void MarkNetworkDirty()
        {
            if (network == null)
            {
                return;
            }

            EditorUtility.SetDirty(network);
            if (network.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(network.gameObject.scene);
            }

            RoadNetworkLivePreviewCoordinator.MarkDirty(network);
            RefreshAdjacentPreview();
        }

        private static void MarkGeneratedConnectorsDirty(RoadJunction junction)
        {
            RoadLane[] lanes = junction.GetComponentsInChildren<RoadLane>(true);
            for (int i = 0; i < lanes.Length; i++)
            {
                EditorUtility.SetDirty(lanes[i]);
                if (lanes[i].SplineContainer != null)
                {
                    EditorUtility.SetDirty(lanes[i].SplineContainer);
                }
            }
        }

        private void RepaintScene()
        {
            SceneView.RepaintAll();
        }

        private readonly struct JunctionEndpointDraft
        {
            public readonly RoadLane lane;
            public readonly RuntimeEndpoint endpoint;

            public JunctionEndpointDraft(RoadLane lane, RuntimeEndpoint endpoint)
            {
                this.lane = lane;
                this.endpoint = endpoint;
            }
        }
    }
}
