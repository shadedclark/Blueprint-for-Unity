using System;
using System.Collections.Generic;
using System.IO;
using VehicleRoads;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads.Editor
{
    [CustomEditor(typeof(RoadLaneNetwork))]
    public sealed class RoadLaneNetworkEditor : UnityEditor.Editor
    {
        private const float AdjacentArrowHandleScale = 0.7f;
        private const float AdjacentArrowMinimumSize = 0.9f;
        private const float AdjacentArrowMaximumSize = 3.2f;

        private SerializedProperty sampleSpacing;
        private SerializedProperty connectionRadius;
        private SerializedProperty connectionDirectionTolerance;
        private SerializedProperty minimumTurnRadius;
        private SerializedProperty previewLineWidth;
        private SerializedProperty outputAssetPath;
        private SerializedProperty networkSettings;
        private SerializedProperty runtimeSettings;
        private SerializedProperty bakedNetwork;
        private Vector2 validationScroll;
        private List<RoadLaneValidationIssue> validationIssues = new List<RoadLaneValidationIssue>();
        private static readonly Dictionary<int, BakedLaneNetwork> AdjacentPreviewNetworks =
            new Dictionary<int, BakedLaneNetwork>();

        private void OnEnable()
        {
            sampleSpacing = serializedObject.FindProperty("sampleSpacing");
            connectionRadius = serializedObject.FindProperty("connectionRadius");
            connectionDirectionTolerance = serializedObject.FindProperty("connectionDirectionTolerance");
            minimumTurnRadius = serializedObject.FindProperty("minimumTurnRadius");
            previewLineWidth = serializedObject.FindProperty("previewLineWidth");
            outputAssetPath = serializedObject.FindProperty("outputAssetPath");
            networkSettings = serializedObject.FindProperty("networkSettings");
            runtimeSettings = serializedObject.FindProperty("runtimeSettings");
            bakedNetwork = serializedObject.FindProperty("bakedNetwork");
        }

        public override void OnInspectorGUI()
        {
            RoadLaneNetwork network = (RoadLaneNetwork)target;
            serializedObject.Update();
            EditorGUILayout.LabelField("Unity Splines Vehicle Road Network", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(sampleSpacing);
            EditorGUILayout.PropertyField(connectionRadius);
            EditorGUILayout.PropertyField(connectionDirectionTolerance);
            EditorGUILayout.PropertyField(minimumTurnRadius);
            EditorGUILayout.PropertyField(
                previewLineWidth,
                new GUIContent(
                    "Lane Preview Line Width",
                    "Global Scene View preview width in pixels for every Lane and Connector in this network."));
            EditorGUILayout.PropertyField(outputAssetPath);

            EditorGUILayout.PropertyField(networkSettings);
            EditorGUILayout.PropertyField(runtimeSettings);
            if (networkSettings.objectReferenceValue == null ||
                runtimeSettings.objectReferenceValue == null)
            {
                if (GUILayout.Button("Assign Project Road Network Settings"))
                {
                    networkSettings.objectReferenceValue =
                        RoadNetworkProjectSettingsAssets.GetNetworkSettings(true);
                    runtimeSettings.objectReferenceValue =
                        RoadNetworkProjectSettingsAssets.GetRuntimeSettings(true);
                }
            }

            EditorGUILayout.PropertyField(bakedNetwork);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawCounts(network);
            DrawSceneAuthoring(network);
            DrawBakeTools(network);
            DrawValidation(network);
        }

        private static void DrawCounts(RoadLaneNetwork network)
        {
            EditorGUILayout.HelpBox(
                string.Format(
                    "Authored lanes: {0}\nJunctions: {1}\nBaked adjacent links: {2}\nBaked traffic junctions: {3}\nBaked traffic connectors: {4}",
                    network.GetAuthoredLanes().Length,
                    network.GetJunctions().Length,
                    network.BakedNetwork == null ? "none" : network.BakedNetwork.AdjacentLinks.Count.ToString(),
                    network.BakedNetwork == null ? "none" : network.BakedNetwork.JunctionTraffic.Count.ToString(),
                    network.BakedNetwork == null ? "none" : network.BakedNetwork.ConnectorTraffic.Count.ToString()),
                MessageType.Info);
        }

        internal static BakedLaneNetwork GetAdjacentPreviewNetwork(RoadLaneNetwork network)
        {
            if (network == null)
            {
                return null;
            }

            BakedLaneNetwork livePreview = RoadNetworkLivePreviewCoordinator.GetPreview(network);
            if (livePreview != null)
            {
                return livePreview;
            }

            return AdjacentPreviewNetworks.TryGetValue(network.GetInstanceID(), out BakedLaneNetwork preview)
                ? preview
                : null;
        }

        internal static void SetAdjacentPreviewNetwork(RoadLaneNetwork network, BakedLaneNetwork preview)
        {
            ClearAdjacentPreviewNetwork(network);
            if (network == null || preview == null)
            {
                return;
            }

            AdjacentPreviewNetworks[network.GetInstanceID()] = preview;
        }

        internal static void ClearAdjacentPreviewNetwork(RoadLaneNetwork network)
        {
            if (network == null)
            {
                return;
            }

            int key = network.GetInstanceID();
            if (!AdjacentPreviewNetworks.TryGetValue(key, out BakedLaneNetwork preview))
            {
                return;
            }

            AdjacentPreviewNetworks.Remove(key);
            if (preview != null)
            {
                DestroyImmediate(preview);
            }
        }

        internal static void DrawAdjacentLinkPreview(RoadLaneNetwork network, bool fallbackToBaked = true)
        {
            BakedLaneNetwork displayNetwork = GetAdjacentPreviewNetwork(network);
            if (displayNetwork == null && fallbackToBaked)
            {
                displayNetwork = network == null ? null : network.BakedNetwork;
            }

            if (displayNetwork == null || displayNetwork.AdjacentLinks.Count == 0)
            {
                return;
            }

            Color previousColor = Handles.color;
            UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            try
            {
                for (int i = 0; i < displayNetwork.AdjacentLinks.Count; i++)
                {
                    BakedLaneAdjacentLinkRecord link = displayNetwork.AdjacentLinks[i];
                    if (link == null ||
                        !TryGetAdjacentLinkPreviewPoints(displayNetwork, link, out Vector3 from, out Vector3 to))
                    {
                        continue;
                    }

                    Handles.color = link.side == RoadLaneAdjacentSide.Left
                        ? new Color(0.35f, 0.8f, 1f, 0.9f)
                        : new Color(0.2f, 1f, 0.55f, 0.9f);
                    Handles.DrawLine(from, to, 2f);
                    Vector3 direction = to - from;
                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        float size = Mathf.Clamp(
                            HandleUtility.GetHandleSize(to) * AdjacentArrowHandleScale,
                            AdjacentArrowMinimumSize,
                            AdjacentArrowMaximumSize);
                        Handles.ConeHandleCap(0, to, Quaternion.LookRotation(direction.normalized, Vector3.up), size, EventType.Repaint);
                    }
                }
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousZTest;
            }
        }

        private static bool TryGetAdjacentLinkPreviewPoints(
            BakedLaneNetwork network,
            BakedLaneAdjacentLinkRecord link,
            out Vector3 from,
            out Vector3 to)
        {
            from = default;
            to = default;
            float midpoint = Mathf.Max(0f, (link.overlapStartDistance + link.overlapEndDistance) * 0.5f);
            if (!network.TryEvaluate(link.fromLaneId, midpoint, out RoadLanePose fromPose))
            {
                return false;
            }

            from = fromPose.position + fromPose.up * 0.2f;
            HashSet<string> allowedLaneIds = new HashSet<string> { link.toLaneId };
            if (network.TryFindNearestLane(
                    fromPose.position,
                    fromPose.forward,
                    RoadAgentMask.All,
                    Mathf.Max(1f, link.maxLateralDistance + 1f),
                    Mathf.Max(1f, link.maxLateralDistance),
                    out BakedLaneNearestResult nearest,
                    allowedLaneIds))
            {
                to = nearest.position + nearest.up * 0.2f;
                return true;
            }

            if (network.TryEvaluate(link.toLaneId, midpoint, out RoadLanePose toPose))
            {
                to = toPose.position + toPose.up * 0.2f;
                return true;
            }

            return false;
        }

        private static void DrawSceneAuthoring(RoadLaneNetwork network)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Authoring", EditorStyles.boldLabel);
            if (GUILayout.Button("Open Scene Authoring Tool", GUILayout.Height(26f)))
            {
                RoadLaneSceneAuthoringWindow.Open(network);
            }
        }

        private static void DrawBakeTools(RoadLaneNetwork network)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bake ScriptableObject Network", EditorStyles.boldLabel);
            if (GUILayout.Button("Bake Network Asset", GUILayout.Height(30f)))
            {
                BakeAndSave(network);
            }
        }

        private void DrawValidation(RoadLaneNetwork network)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            if (GUILayout.Button("Validate Network"))
            {
                validationIssues = network.ValidateNetwork();
            }

            if (validationIssues.Count == 0)
            {
                EditorGUILayout.HelpBox("No validation issues.", MessageType.Info);
                return;
            }

            validationScroll = EditorGUILayout.BeginScrollView(validationScroll, GUILayout.MaxHeight(180f));
            for (int i = 0; i < validationIssues.Count; i++)
            {
                RoadLaneValidationIssue issue = validationIssues[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(issue.code + ": " + issue.message, EditorStyles.wordWrappedLabel);
                    if (issue.lane != null && GUILayout.Button("Select", GUILayout.Width(52f)))
                    {
                        Selection.activeObject = issue.lane.gameObject;
                        SceneView.FrameLastActiveSceneView();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        public static void BakeAndSave(RoadLaneNetwork network)
        {
            List<RoadLaneValidationIssue> issues = network.ValidateNetwork();
            if (issues.Exists(issue => issue.code == "MissingLaneId" ||
                                       issue.code == "DuplicateLaneId" ||
                                       issue.code == "TooFewKnots" ||
                                       issue.code == "InvalidLength" ||
                                       issue.code == "InvalidNumber" ||
                                       issue.code == "InvalidLaneWidth" ||
                                       issue.code == "InvalidLaneBoundary" ||
                                       issue.code == "InvalidPolygon" ||
                                       issue.code == "InvalidPolygonId" ||
                                       issue.code == "InvalidPortal"))
            {
                Debug.LogError("Vehicle road bake stopped because the network contains blocking validation errors.", network);
                return;
            }

            BakedLaneNetwork transient = network.BakeNetwork();
            string baseAssetPath = NormalizeBaseAssetPath(network.OutputAssetPath);
            string networkAssetPath = baseAssetPath + ".asset";
            EnsureAssetFolder(Path.GetDirectoryName(networkAssetPath));
            BakedLaneNetwork saved = AssetDatabase.LoadAssetAtPath<BakedLaneNetwork>(networkAssetPath);
            if (saved == null)
            {
                saved = transient;
                AssetDatabase.CreateAsset(saved, networkAssetPath);
            }
            else
            {
                EditorUtility.CopySerialized(transient, saved);
                DestroyImmediate(transient);
                EditorUtility.SetDirty(saved);
            }

            saved.name = Path.GetFileNameWithoutExtension(networkAssetPath);
            EditorUtility.SetDirty(saved);
            AssetDatabase.Refresh();
            Undo.RecordObject(network, "Assign Baked Lane Network");
            network.BakedNetwork = saved;
            EditorUtility.SetDirty(network);
            int refreshedSubsystemCount = EditorApplication.isPlaying
                ? RefreshVehicleRoadSubsystemsAfterBake()
                : 0;
            AssetDatabase.SaveAssets();
            Debug.LogFormat(
                network,
                "Baked vehicle roads: {0} directed lane(s), {1} sample(s), {2} connection(s).",
                saved.Summary.directedLaneCount,
                saved.Summary.sampleCount,
                saved.Summary.connectionCount);
            if (refreshedSubsystemCount > 0)
            {
                Debug.LogFormat(
                    network,
                    "Refreshed {0} VehicleRoadSubsystem instance(s) after runtime bake.",
                    refreshedSubsystemCount);
            }
        }

        internal static int RefreshVehicleRoadSubsystemsAfterBake()
        {
            VehicleRoadSubsystem[] subsystems =
                UnityEngine.Object.FindObjectsByType<VehicleRoadSubsystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int refreshed = 0;
            for (int i = 0; i < subsystems.Length; i++)
            {
                VehicleRoadSubsystem subsystem = subsystems[i];
                if (subsystem == null)
                {
                    continue;
                }

                subsystem.RebuildIndexes();
                refreshed++;
            }

            return refreshed;
        }

        private static string NormalizeBaseAssetPath(string value)
        {
            string path = string.IsNullOrWhiteSpace(value)
                ? "Assets/VehicleRoads/Generated/VehicleRoadNetwork"
                : value.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                path = "Assets/VehicleRoads/Generated/" + Path.GetFileNameWithoutExtension(path);
            }

            return Path.ChangeExtension(path, null);
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            string normalized = folder.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        [MenuItem("GameObject/Vehicle Road/Vehicle Road Network", false, 2000)]
        private static void CreateNetwork(MenuCommand command)
        {
            GameObject root = new GameObject("Vehicle Road Network");
            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            root.AddComponent<RoadLaneNetwork>();
            Undo.RegisterCreatedObjectUndo(root, "Create Vehicle Road Network");
            Selection.activeObject = root;
        }
    }

    [CustomEditor(typeof(RoadJunction))]
    public sealed class RoadJunctionEditor : UnityEditor.Editor
    {
        private const float MinimumHandleScale = 0.1f;
        private const float MaximumHandleScale = 2f;
        private static readonly UnitySplineRoadLaneGeometry PreviewGeometry = new UnitySplineRoadLaneGeometry();
        private static readonly RoadLaneTurn[] SignalTurnOrder =
        {
            RoadLaneTurn.Straight,
            RoadLaneTurn.Left,
            RoadLaneTurn.Right,
            RoadLaneTurn.UTurn
        };

        private SerializedProperty junctionId;
        private SerializedProperty allowedTurns;
        private SerializedProperty connectorHandleScale;
        private SerializedProperty connectorBaseCost;
        private SerializedProperty connectorSpeedLimit;
        private SerializedProperty trafficControlMode;
        private SerializedProperty defaultStopLineDistance;
        private SerializedProperty queueSpacing;
        private SerializedProperty approachDetectionDistance;
        private SerializedProperty passageTokenDuration;
        private SerializedProperty releaseDistance;
        private SerializedProperty straightPriority;
        private SerializedProperty rightPriority;
        private SerializedProperty leftPriority;
        private SerializedProperty uTurnPriority;
        private SerializedProperty signalPhases;
        private SerializedProperty bindings;

        private void OnEnable()
        {
            junctionId = serializedObject.FindProperty("junctionId");
            allowedTurns = serializedObject.FindProperty("allowedTurns");
            connectorHandleScale = serializedObject.FindProperty("connectorHandleScale");
            connectorBaseCost = serializedObject.FindProperty("connectorBaseCost");
            connectorSpeedLimit = serializedObject.FindProperty("connectorSpeedLimit");
            trafficControlMode = serializedObject.FindProperty("trafficControlMode");
            defaultStopLineDistance = serializedObject.FindProperty("defaultStopLineDistance");
            queueSpacing = serializedObject.FindProperty("queueSpacing");
            approachDetectionDistance = serializedObject.FindProperty("approachDetectionDistance");
            passageTokenDuration = serializedObject.FindProperty("passageTokenDuration");
            releaseDistance = serializedObject.FindProperty("releaseDistance");
            straightPriority = serializedObject.FindProperty("straightPriority");
            rightPriority = serializedObject.FindProperty("rightPriority");
            leftPriority = serializedObject.FindProperty("leftPriority");
            uTurnPriority = serializedObject.FindProperty("uTurnPriority");
            signalPhases = serializedObject.FindProperty("signalPhases");
            bindings = serializedObject.FindProperty("bindings");
        }

        public override void OnInspectorGUI()
        {
            RoadJunction junction = (RoadJunction)target;
            serializedObject.Update();

            EditorGUILayout.PropertyField(junctionId);
            EditorGUILayout.PropertyField(allowedTurns);

            connectorHandleScale.floatValue = EditorGUILayout.Slider(
                new GUIContent(
                    "Connector Handle Scale",
                    "Controls the generated Bezier tangent length. Larger values create broader turns."),
                connectorHandleScale.floatValue,
                MinimumHandleScale,
                MaximumHandleScale);

            EditorGUILayout.PropertyField(connectorBaseCost);
            EditorGUILayout.PropertyField(connectorSpeedLimit);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Traffic Control", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(trafficControlMode);
            EditorGUILayout.PropertyField(defaultStopLineDistance);
            EditorGUILayout.PropertyField(queueSpacing);
            EditorGUILayout.PropertyField(approachDetectionDistance);
            EditorGUILayout.PropertyField(passageTokenDuration);
            EditorGUILayout.PropertyField(releaseDistance);
            EditorGUILayout.PropertyField(straightPriority);
            EditorGUILayout.PropertyField(rightPriority);
            EditorGUILayout.PropertyField(leftPriority);
            EditorGUILayout.PropertyField(uTurnPriority);
            EditorGUILayout.PropertyField(signalPhases, true);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(bindings, true);
            bool junctionChanged = serializedObject.hasModifiedProperties;

            EditorGUILayout.HelpBox(
                "Connector generation is always live. Changing this Junction refreshes only its unlocked Connectors; locked Connectors keep their manually edited curves.",
                MessageType.Info);

            if (junctionChanged)
            {
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Update Road Junction");
                Undo.RegisterFullObjectHierarchyUndo(junction.gameObject, "Update Road Junction");
                serializedObject.ApplyModifiedProperties();
                RefreshConnectorPreview(junction);
                Undo.CollapseUndoOperations(undoGroup);
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void OnSceneGUI()
        {
            RoadJunction junction = (RoadJunction)target;
            if (junction == null)
            {
                return;
            }

            Color previousColor = Handles.color;
            Handles.color = junction.TrafficControlMode == RoadJunctionTrafficControlMode.FixedSignal
                ? new Color(1f, 0.84f, 0.15f, 0.95f)
                : new Color(0.35f, 0.8f, 1f, 0.95f);
            DrawStopLines(junction);
            Handles.color = previousColor;
        }

        [DrawGizmo(GizmoType.Active | GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawJunctionTrafficGizmo(RoadJunction junction, GizmoType gizmoType)
        {
            if (junction == null || !junction.isActiveAndEnabled)
            {
                return;
            }

            DrawSignalStateOverlay(junction);
        }

        private static void DrawSignalStateOverlay(RoadJunction junction)
        {
            Vector3 origin = junction.transform.position + Vector3.up * 2.25f;
            GUIStyle labelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                richText = false
            };
            labelStyle.normal.textColor = Color.white;

            string title = string.IsNullOrWhiteSpace(junction.JunctionId)
                ? "Signal"
                : "Signal " + junction.JunctionId;
            if (junction.TrafficControlMode != RoadJunctionTrafficControlMode.FixedSignal)
            {
                Handles.Label(origin, title + "\n" + junction.TrafficControlMode, labelStyle);
                return;
            }

            Camera camera = Camera.current;
            Vector3 cameraRight = camera == null ? Vector3.right : camera.transform.right;
            Vector3 labelPosition = origin + cameraRight * (HandleUtility.GetHandleSize(origin) * 0.35f);
            string label = title + "  " + junction.TrafficControlMode;
            bool hasAnyState = false;
            bool hasAnyAllowedTurn = false;
            for (int i = 0; i < SignalTurnOrder.Length; i++)
            {
                RoadLaneTurn turn = SignalTurnOrder[i];
                if (!RoadLaneEditorVisualUtility.AllowsTurn(junction.AllowedTurns, turn))
                {
                    continue;
                }

                hasAnyAllowedTurn = true;
                bool hasState = TryGetLiveSignalState(junction.JunctionId, turn, out VehicleRoadSignalState state);
                hasAnyState |= hasState;
                string stateText = hasState ? state.ToString() : "No runtime";
                label += "\n" + turn + ": " + stateText;
            }

            if (!hasAnyAllowedTurn)
            {
                label += "\nNo allowed turns";
            }

            Color previousColor = Handles.color;
            Handles.color = hasAnyState
                ? GetSignalStateColor(GetMostRestrictiveDisplayedState(junction))
                : new Color(0.45f, 0.45f, 0.45f, 0.85f);
            Handles.Label(labelPosition, label, labelStyle);
            Handles.color = previousColor;
        }

        private static bool TryGetLiveSignalState(
            string junctionId,
            RoadLaneTurn turn,
            out VehicleRoadSignalState state)
        {
            state = VehicleRoadSignalState.None;
            if (string.IsNullOrWhiteSpace(junctionId))
            {
                return false;
            }

            VehicleRoadSubsystem[] subsystems = UnityEngine.Object.FindObjectsByType<VehicleRoadSubsystem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < subsystems.Length; i++)
            {
                VehicleRoadSubsystem subsystem = subsystems[i];
                if (subsystem != null && subsystem.TryGetJunctionSignalState(junctionId, turn, out state))
                {
                    return true;
                }
            }

            return false;
        }

        private static Color GetSignalStateColor(VehicleRoadSignalState state)
        {
            return state switch
            {
                VehicleRoadSignalState.Green => new Color(0.15f, 1f, 0.25f, 0.95f),
                VehicleRoadSignalState.Yellow => new Color(1f, 0.82f, 0.08f, 0.95f),
                VehicleRoadSignalState.Red => new Color(1f, 0.12f, 0.08f, 0.95f),
                _ => new Color(0.45f, 0.45f, 0.45f, 0.85f)
            };
        }

        private static VehicleRoadSignalState GetMostRestrictiveDisplayedState(RoadJunction junction)
        {
            VehicleRoadSignalState result = VehicleRoadSignalState.None;
            for (int i = 0; i < SignalTurnOrder.Length; i++)
            {
                RoadLaneTurn turn = SignalTurnOrder[i];
                if (!RoadLaneEditorVisualUtility.AllowsTurn(junction.AllowedTurns, turn) ||
                    !TryGetLiveSignalState(junction.JunctionId, turn, out VehicleRoadSignalState state))
                {
                    continue;
                }

                if (state == VehicleRoadSignalState.Red)
                {
                    return VehicleRoadSignalState.Red;
                }

                if (state == VehicleRoadSignalState.Yellow)
                {
                    result = VehicleRoadSignalState.Yellow;
                }
                else if (result == VehicleRoadSignalState.None)
                {
                    result = state;
                }
            }

            return result;
        }

        private static void DrawStopLines(RoadJunction junction)
        {
            List<RoadJunctionBinding> bindings = junction.Bindings;
            if (bindings == null)
            {
                return;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                RoadJunctionBinding binding = bindings[i];
                RoadLane lane = binding == null ? null : binding.lane;
                if (lane == null || lane.Kind == RoadLaneKind.Connector)
                {
                    continue;
                }

                if (!TryEvaluateStopLinePose(lane, binding.endpoint, junction.DefaultStopLineDistance, out RoadLanePose pose))
                {
                    continue;
                }

                Vector3 right = Vector3.Cross(pose.up, pose.forward);
                if (right.sqrMagnitude <= 0.0001f)
                {
                    right = Vector3.right;
                }

                right.Normalize();
                Vector3 center = pose.position + pose.up * 0.12f;
                Handles.DrawLine(center - right * 1.5f, center + right * 1.5f, 3f);
            }
        }

        private static bool TryEvaluateStopLinePose(
            RoadLane lane,
            RoadLaneEndpoint endpoint,
            float stopLineDistance,
            out RoadLanePose pose)
        {
            pose = default;
            bool isIncoming = endpoint == RoadLaneEndpoint.End
                ? lane.TravelDirection != RoadLaneTravelDirection.Reverse
                : lane.TravelDirection != RoadLaneTravelDirection.Forward;
            if (!isIncoming)
            {
                return false;
            }

            bool reverse = endpoint == RoadLaneEndpoint.Start;
            float length = PreviewGeometry.GetLength(lane);
            float directedDistance = length - stopLineDistance;
            float clampedDistance = Mathf.Clamp(directedDistance, 0f, length);
            if (!PreviewGeometry.TryEvaluate(lane, clampedDistance, reverse, out pose))
            {
                return false;
            }

            float extrapolatedDistance = directedDistance - clampedDistance;
            if (Mathf.Abs(extrapolatedDistance) > 0.0001f)
            {
                Vector3 forward = pose.forward.sqrMagnitude <= 0.0001f
                    ? Vector3.forward
                    : pose.forward.normalized;
                Vector3 offset = forward * extrapolatedDistance;
                pose.position += offset;
                pose.splinePosition += offset;
                pose.distance = directedDistance;
                pose.normalizedT = length <= 0.0001f ? 0f : directedDistance / length;
            }

            return true;
        }

        private static void RefreshConnectorPreview(RoadJunction junction)
        {
            RoadLaneNetwork network = junction.GetComponentInParent<RoadLaneNetwork>();
            if (network == null)
            {
                Debug.LogWarning("RoadJunction must be a child of a RoadLaneNetwork to refresh Connectors.", junction);
                return;
            }

            network.RefreshConnectors(
                junction,
                created => Undo.RegisterCreatedObjectUndo(created, "Create Road Connector"));

            RoadLane[] lanes = junction.GetComponentsInChildren<RoadLane>(true);
            for (int i = 0; i < lanes.Length; i++)
            {
                EditorUtility.SetDirty(lanes[i]);
                if (lanes[i].SplineContainer != null)
                {
                    EditorUtility.SetDirty(lanes[i].SplineContainer);
                }
            }

            EditorUtility.SetDirty(junction);
            EditorUtility.SetDirty(network);
            if (junction.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(junction.gameObject.scene);
            }

            SceneView.RepaintAll();
        }
    }

    [CustomEditor(typeof(RoadLane))]
    public sealed class RoadLaneEditor : UnityEditor.Editor
    {
        private const float DirectionArrowSpacing = 18f;
        private const int UnselectedDirectionArrowLimit = 3;
        private const int SelectedDirectionArrowLimit = 8;
        private const float DirectionArrowHandleScale = 1f;
        private const float DirectionArrowMinimumSize = 1.3f;
        private const float DirectionArrowMaximumSize = 4f;
        private const float DirectionArrowVerticalOffset = 0.08f;
        private const float BidirectionalDirectionArrowSideOffset = 0.35f;
        private static readonly Color DirectionArrowColor = new Color(1f, 0.78f, 0.12f, 0.9f);
        private static readonly UnitySplineRoadLaneGeometry PreviewGeometry = new UnitySplineRoadLaneGeometry();

        [DrawGizmo(GizmoType.Active | GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawLanePreview(RoadLane lane, GizmoType gizmoType)
        {
            if (lane == null || !lane.isActiveAndEnabled)
            {
                return;
            }

            SplineContainer container = lane.SplineContainer;
            Spline spline = container == null ? null : container.Spline;
            if (spline == null || spline.Count < 2)
            {
                return;
            }

            int curveCount = spline.Closed ? spline.Count : spline.Count - 1;
            RoadLaneNetwork network = lane.GetComponentInParent<RoadLaneNetwork>();
            float previewLineWidth = network == null
                ? RoadLaneNetwork.DefaultPreviewLineWidth
                : network.PreviewLineWidth;
            bool selected = (gizmoType & (GizmoType.Selected | GizmoType.Active)) != 0;
            Color previousColor = Handles.color;
            Handles.color = RoadLaneEditorVisualUtility.GetLanePreviewColor(
                lane,
                selected ? Handles.selectedColor : Color.blue,
                selected ? Handles.selectedColor : Color.blue);
            Color laneColor = Handles.color;
            RoadLaneEditorVisualUtility.DrawLaneWidthPreview(lane, laneColor, selected);

            Transform splineTransform = container.transform;
            for (int i = 0; i < curveCount; i++)
            {
                BezierCurve curve = spline.GetCurve(i);
                Handles.DrawBezier(
                    splineTransform.TransformPoint(curve.P0),
                    splineTransform.TransformPoint(curve.P3),
                    splineTransform.TransformPoint(curve.P1),
                    splineTransform.TransformPoint(curve.P2),
                    Handles.color,
                    null,
                    previewLineWidth);
            }

            DrawLaneDirectionPreview(lane, selected);
            Handles.color = previousColor;
        }

        private static void DrawLaneDirectionPreview(RoadLane lane, bool selected)
        {
            float length = PreviewGeometry.GetLength(lane);
            if (length <= 0.001f)
            {
                return;
            }

            switch (lane.TravelDirection)
            {
                case RoadLaneTravelDirection.Reverse:
                    DrawDirectionArrows(lane, length, true, selected, 0f);
                    break;
                case RoadLaneTravelDirection.Bidirectional:
                    DrawDirectionArrows(lane, length, false, selected, BidirectionalDirectionArrowSideOffset);
                    DrawDirectionArrows(lane, length, true, selected, BidirectionalDirectionArrowSideOffset);
                    break;
                default:
                    DrawDirectionArrows(lane, length, false, selected, 0f);
                    break;
            }
        }

        private static void DrawDirectionArrows(
            RoadLane lane,
            float length,
            bool reverse,
            bool selected,
            float sideOffset)
        {
            int arrowLimit = selected ? SelectedDirectionArrowLimit : UnselectedDirectionArrowLimit;
            int arrowCount = Mathf.Clamp(Mathf.CeilToInt(length / DirectionArrowSpacing), 1, arrowLimit);
            float distanceStep = length / (arrowCount + 1);
            Handles.color = selected ? Handles.selectedColor : DirectionArrowColor;

            for (int i = 0; i < arrowCount; i++)
            {
                float distance = distanceStep * (i + 1);
                if (!PreviewGeometry.TryEvaluate(lane, distance, reverse, out RoadLanePose pose))
                {
                    continue;
                }

                Vector3 forward = pose.forward.sqrMagnitude > 0.000001f
                    ? pose.forward.normalized
                    : Vector3.forward;
                Vector3 up = pose.up.sqrMagnitude > 0.000001f
                    ? pose.up.normalized
                    : Vector3.up;
                Vector3 position = pose.position + up * DirectionArrowVerticalOffset;
                Vector3 right = Vector3.Cross(up, forward);
                if (sideOffset > 0f && right.sqrMagnitude > 0.000001f)
                {
                    position += right.normalized * sideOffset;
                }

                Quaternion rotation = Quaternion.LookRotation(forward, up);
                float size = Mathf.Clamp(
                    HandleUtility.GetHandleSize(position) * DirectionArrowHandleScale,
                    DirectionArrowMinimumSize,
                    DirectionArrowMaximumSize);
                Handles.ArrowHandleCap(0, position, rotation, size, EventType.Repaint);
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            RoadLane lane = (RoadLane)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Authoring", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Adjacent lane copy, snapping, flattening, and grid alignment are configured in the Road Lane Authoring window.",
                MessageType.Info);
            RoadLaneNetwork network = lane == null ? null : lane.GetComponentInParent<RoadLaneNetwork>();
            using (new EditorGUI.DisabledScope(network == null))
            {
                if (GUILayout.Button("Open Road Lane Authoring", GUILayout.Height(26f)))
                {
                    RoadLaneSceneAuthoringWindow.Open(network, lane);
                }
            }
        }
    }

    internal static class RoadLaneEditorVisualUtility
    {
        private const float LaneWidthBoundaryLineWidth = 1.75f;
        private const float LaneWidthCrossbarLineWidth = 1f;
        private const float LaneWidthSampleSpacing = 6f;
        private const float LaneWidthPreviewLift = 0.04f;
        private const float LaneWidthLabelLift = 0.45f;
        private const int LaneWidthMinimumSamples = 6;
        private const int LaneWidthMaximumSamples = 48;
        private const int LaneWidthCrossbarCount = 6;

        internal static readonly Color DisallowedConnectorColor = new Color(1f, 0.12f, 0.08f, 0.9f);

        internal static bool IsConnectorTurnAllowed(RoadLane lane)
        {
            if (lane == null || lane.Kind != RoadLaneKind.Connector)
            {
                return true;
            }

            RoadJunction junction = lane.GetComponentInParent<RoadJunction>();
            return junction == null || AllowsTurn(junction.AllowedTurns, lane.TurnType);
        }

        internal static bool AllowsTurn(RoadLaneTurnMask mask, RoadLaneTurn turn)
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

        internal static Color GetLanePreviewColor(
            RoadLane lane,
            Color standardLaneColor,
            Color connectorColor)
        {
            if (lane == null || lane.Kind != RoadLaneKind.Connector)
            {
                return standardLaneColor;
            }

            if (IsConnectorTurnAllowed(lane))
            {
                return connectorColor;
            }

            Color color = DisallowedConnectorColor;
            color.a = connectorColor.a;
            return color;
        }

        internal static void DrawLaneWidthPreview(RoadLane lane, Color baseColor, bool selected)
        {
            SplineContainer container = lane == null ? null : lane.SplineContainer;
            Spline spline = container == null ? null : container.Spline;
            if (spline == null || spline.Count < 2)
            {
                return;
            }

            float length = Mathf.Max(0.1f, container.CalculateLength());
            int sampleCount = Mathf.Clamp(
                Mathf.CeilToInt(length / LaneWidthSampleSpacing) + 1,
                LaneWidthMinimumSamples,
                LaneWidthMaximumSamples);
            List<Vector3> leftBoundary = new List<Vector3>(sampleCount);
            List<Vector3> rightBoundary = new List<Vector3>(sampleCount);
            Vector3 labelPosition = Vector3.zero;
            Vector3 labelUp = Vector3.up;
            bool hasLabelPosition = false;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount == 1 ? 0f : (float)i / (sampleCount - 1);
                if (!TryEvaluateLaneWidthSample(lane, t, out Vector3 left, out Vector3 right, out Vector3 center, out Vector3 up))
                {
                    continue;
                }

                leftBoundary.Add(left);
                rightBoundary.Add(right);
                if (!hasLabelPosition && t >= 0.5f)
                {
                    labelPosition = center;
                    labelUp = up;
                    hasLabelPosition = true;
                }
            }

            if (leftBoundary.Count < 2 || rightBoundary.Count < 2)
            {
                return;
            }

            Color previousColor = Handles.color;
            Color boundaryColor = baseColor;
            boundaryColor.a = selected ? 0.9f : 0.45f;
            Handles.color = boundaryColor;
            Handles.DrawAAPolyLine(LaneWidthBoundaryLineWidth, leftBoundary.ToArray());
            Handles.DrawAAPolyLine(LaneWidthBoundaryLineWidth, rightBoundary.ToArray());

            if (selected)
            {
                Color crossbarColor = baseColor;
                crossbarColor.a = 0.55f;
                Handles.color = crossbarColor;
                int step = Mathf.Max(1, (leftBoundary.Count - 1) / LaneWidthCrossbarCount);
                for (int i = 0; i < leftBoundary.Count; i += step)
                {
                    Handles.DrawAAPolyLine(LaneWidthCrossbarLineWidth, leftBoundary[i], rightBoundary[i]);
                }

                int lastIndex = leftBoundary.Count - 1;
                Handles.DrawAAPolyLine(LaneWidthCrossbarLineWidth, leftBoundary[lastIndex], rightBoundary[lastIndex]);

                if (hasLabelPosition)
                {
                    GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                    labelStyle.normal.textColor = Color.white;
                    Handles.Label(
                        labelPosition + labelUp.normalized * LaneWidthLabelLift,
                        GetLaneWidthLabel(lane),
                        labelStyle);
                }
            }

            Handles.color = previousColor;
        }

        private static bool TryEvaluateLaneWidthSample(
            RoadLane lane,
            float t,
            out Vector3 leftBoundary,
            out Vector3 rightBoundary,
            out Vector3 center,
            out Vector3 up)
        {
            leftBoundary = default;
            rightBoundary = default;
            center = default;
            up = Vector3.up;
            SplineContainer container = lane == null ? null : lane.SplineContainer;
            if (container == null ||
                !container.Evaluate(Mathf.Clamp01(t), out float3 position, out float3 tangent, out float3 splineUp))
            {
                return false;
            }

            Vector3 tangentVector = tangent;
            Vector3 forward = UnitySplineRoadLaneGeometry.IsFinite(tangentVector) && tangentVector.sqrMagnitude > 0.000001f
                ? tangentVector.normalized
                : Vector3.forward;
            Vector3 upVector = splineUp;
            up = UnitySplineRoadLaneGeometry.IsFinite(upVector) && upVector.sqrMagnitude > 0.000001f
                ? upVector.normalized
                : Vector3.up;
            Vector3 right = Vector3.Cross(up, forward);
            if (!UnitySplineRoadLaneGeometry.IsFinite(right) || right.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            right.Normalize();
            center = (Vector3)position + right * lane.LateralOffset + up * lane.VerticalOffset;
            float halfWidth = lane.EvaluateWidth(t) * 0.5f;
            Vector3 lift = up * LaneWidthPreviewLift;
            leftBoundary = center - right * halfWidth + lift;
            rightBoundary = center + right * halfWidth + lift;
            center += lift;
            return UnitySplineRoadLaneGeometry.IsFinite(leftBoundary) &&
                   UnitySplineRoadLaneGeometry.IsFinite(rightBoundary);
        }

        private static string GetLaneWidthLabel(RoadLane lane)
        {
            if (lane == null || lane.WidthKeys.Count == 0)
            {
                return lane == null ? string.Empty : lane.Width.ToString("0.##") + " m";
            }

            float minimum = float.PositiveInfinity;
            float maximum = 0f;
            for (int i = 0; i < lane.WidthKeys.Count; i++)
            {
                RoadLaneWidthKey key = lane.WidthKeys[i];
                if (key == null)
                {
                    continue;
                }

                minimum = Mathf.Min(minimum, key.width);
                maximum = Mathf.Max(maximum, key.width);
            }

            return float.IsFinite(minimum)
                ? minimum.ToString("0.##") + "-" + maximum.ToString("0.##") + " m"
                : lane.Width.ToString("0.##") + " m";
        }
    }

    internal static class RoadLaneEditorSelectionUtility
    {
        internal static void SelectLane(RoadLane lane)
        {
            if (lane == null)
            {
                return;
            }

            SplineSelection.Clear();
            Selection.activeObject = lane.gameObject;
        }

        internal static void SelectKnot(RoadLane lane, int knotIndex)
        {
            SplineContainer container = lane == null ? null : lane.SplineContainer;
            Spline spline = container == null ? null : container.Spline;
            if (spline == null || knotIndex < 0 || knotIndex >= spline.Count)
            {
                return;
            }

            Selection.activeObject = lane.gameObject;
            SplineSelection.Set(new SelectableKnot(new SplineInfo(container, 0), knotIndex));
        }

        internal static void SelectJunction(RoadJunction junction)
        {
            if (junction == null)
            {
                return;
            }

            SplineSelection.Clear();
            Selection.activeObject = junction.gameObject;
        }
    }
}
