using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlueprintSystem.Editor
{
    internal static class SmartObjectDebuggerUtility
    {
        public static List<SmartObjectComponent> FindSceneSmartObjects()
        {
            SmartObjectComponent[] components = Resources.FindObjectsOfTypeAll<SmartObjectComponent>();
            List<SmartObjectComponent> results = new List<SmartObjectComponent>();
            for (int i = 0; i < components.Length; i++)
            {
                SmartObjectComponent component = components[i];
                if (IsSceneSmartObject(component))
                {
                    results.Add(component);
                }
            }

            results.Sort(CompareSmartObjects);
            return results;
        }

        public static bool IsSceneSmartObject(SmartObjectComponent component)
        {
            if (component == null || EditorUtility.IsPersistent(component))
            {
                return false;
            }

            GameObject gameObject = component.gameObject;
            if (gameObject == null || EditorUtility.IsPersistent(gameObject))
            {
                return false;
            }

            Scene scene = gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        public static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        public static SmartObjectComponent FindSelectedSmartObject()
        {
            SmartObjectComponent selected = Selection.activeObject as SmartObjectComponent;
            if (selected != null)
            {
                return selected;
            }

            GameObject gameObject = Selection.activeGameObject;
            return gameObject == null ? null : gameObject.GetComponent<SmartObjectComponent>();
        }

        private static int CompareSmartObjects(SmartObjectComponent left, SmartObjectComponent right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int sceneCompare = string.Compare(
                GetSceneSortKey(left.gameObject.scene),
                GetSceneSortKey(right.gameObject.scene),
                StringComparison.OrdinalIgnoreCase);
            if (sceneCompare != 0)
            {
                return sceneCompare;
            }

            int hierarchyCompare = string.Compare(
                GetHierarchyPath(left.transform),
                GetHierarchyPath(right.transform),
                StringComparison.OrdinalIgnoreCase);
            if (hierarchyCompare != 0)
            {
                return hierarchyCompare;
            }

            return string.Compare(left.ObjectId, right.ObjectId, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSceneSortKey(Scene scene)
        {
            if (!scene.IsValid())
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(scene.path))
            {
                return scene.path;
            }

            return scene.name + "#" + Convert.ToString(scene.handle, CultureInfo.InvariantCulture);
        }
    }

    internal sealed class SmartObjectDebuggerWindow : EditorWindow
    {
        private const float ListWidth = 340f;
        private const double AutoRefreshIntervalSeconds = 0.35;

        private readonly List<SmartObjectDebugSnapshot> snapshots = new List<SmartObjectDebugSnapshot>();
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private SmartObjectComponent selectedComponent;
        private string searchText = string.Empty;
        private bool autoRefresh = true;
        private double nextAutoRefreshTime;

        [MenuItem("Tools/Blueprint System/SmartObject/Debugger")]
        public static void OpenWindow()
        {
            if (!BlueprintModuleSettings.SmartObjectEnabled)
            {
                EditorUtility.DisplayDialog(
                    "SmartObject Disabled",
                    "The SmartObject module is disabled in Project Settings > Blueprint System > Modules.",
                    "OK");
                return;
            }

            SmartObjectDebuggerWindow window = GetWindow<SmartObjectDebuggerWindow>("SmartObject Debugger");
            window.Show();
            window.RefreshSnapshots();
        }

        [MenuItem("Tools/Blueprint System/SmartObject/Debugger", true)]
        private static bool CanOpenWindow()
        {
            return BlueprintModuleSettings.SmartObjectEnabled;
        }

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
            Selection.selectionChanged += HandleSelectionChanged;
            RefreshSnapshots();
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
            Selection.selectionChanged -= HandleSelectionChanged;
        }

        private void OnInspectorUpdate()
        {
            if (!autoRefresh)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < nextAutoRefreshTime)
            {
                return;
            }

            nextAutoRefreshTime = now + AutoRefreshIntervalSeconds;
            RefreshSnapshots();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawObjectList();
                DrawDetails();
            }
        }

        private void HandleHierarchyChanged()
        {
            RefreshSnapshots();
            Repaint();
        }

        private void HandleSelectionChanged()
        {
            SmartObjectComponent selected = SmartObjectDebuggerUtility.FindSelectedSmartObject();
            if (selected != null && SmartObjectDebuggerUtility.IsSceneSmartObject(selected))
            {
                selectedComponent = selected;
            }

            Repaint();
        }

        private void RefreshSnapshots()
        {
            snapshots.Clear();
            List<SmartObjectComponent> components = SmartObjectDebuggerUtility.FindSceneSmartObjects();
            for (int i = 0; i < components.Count; i++)
            {
                snapshots.Add(SmartObjectRegistry.CreateDebugSnapshot(components[i]));
            }

            if (selectedComponent == null)
            {
                SmartObjectComponent selected = SmartObjectDebuggerUtility.FindSelectedSmartObject();
                if (selected != null && SmartObjectDebuggerUtility.IsSceneSmartObject(selected))
                {
                    selectedComponent = selected;
                }
            }

            if (selectedComponent != null && FindSnapshot(selectedComponent) == null)
            {
                selectedComponent = null;
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                {
                    RefreshSnapshots();
                    Repaint();
                }

                autoRefresh = GUILayout.Toggle(autoRefresh, "Auto Refresh", EditorStyles.toolbarButton, GUILayout.Width(96f));

                bool gizmosEnabled = SmartObjectDebuggerSceneOverlay.GizmosEnabled;
                bool newGizmosEnabled = GUILayout.Toggle(gizmosEnabled, "Scene Gizmos", EditorStyles.toolbarButton, GUILayout.Width(96f));
                if (newGizmosEnabled != gizmosEnabled)
                {
                    SmartObjectDebuggerSceneOverlay.GizmosEnabled = newGizmosEnabled;
                }

                GUILayout.Space(8f);
                GUILayout.Label("Search", GUILayout.Width(44f));
                string newSearchText = EditorGUILayout.TextField(searchText, GetToolbarSearchFieldStyle(), GUILayout.MinWidth(120f));
                if (newSearchText != searchText)
                {
                    searchText = newSearchText;
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(searchText)))
                {
                    if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(24f)))
                    {
                        searchText = string.Empty;
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private void DrawObjectList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth), GUILayout.ExpandHeight(true)))
            {
                List<SmartObjectDebugSnapshot> visibleSnapshots = GetVisibleSnapshots();
                EditorGUILayout.LabelField(
                    "SmartObjects",
                    snapshots.Count.ToString(CultureInfo.InvariantCulture) + " total, " + visibleSnapshots.Count.ToString(CultureInfo.InvariantCulture) + " visible",
                    EditorStyles.boldLabel);

                listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
                if (visibleSnapshots.Count == 0)
                {
                    EditorGUILayout.HelpBox("No SmartObjectComponent found in loaded scenes.", MessageType.Info);
                }

                for (int i = 0; i < visibleSnapshots.Count; i++)
                {
                    DrawObjectRow(visibleSnapshots[i]);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawObjectRow(SmartObjectDebugSnapshot snapshot)
        {
            Rect rect = GUILayoutUtility.GetRect(ListWidth - 18f, 54f, GUILayout.ExpandWidth(true));
            bool selected = snapshot != null && snapshot.Component == selectedComponent;

            if (Event.current.type == EventType.Repaint)
            {
                Color background = selected
                    ? new Color(0.24f, 0.42f, 0.72f, 0.32f)
                    : new Color(0.18f, 0.18f, 0.18f, 0.12f);
                EditorGUI.DrawRect(rect, background);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), GetRegistrationColor(snapshot));
            }

            string title = snapshot == null || snapshot.GameObject == null ? "<missing>" : snapshot.GameObject.name;
            string objectId = snapshot == null || string.IsNullOrEmpty(snapshot.ObjectId) ? "<empty id>" : snapshot.ObjectId;
            string sceneName = snapshot == null || snapshot.GameObject == null ? "<no scene>" : snapshot.GameObject.scene.name;
            string state = snapshot == null ? SmartObjectRegistrationState.MissingComponent.ToString() : snapshot.RegistrationState.ToString();
            string slotSummary = snapshot == null
                ? string.Empty
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "F:{0} R:{1} O:{2} B:{3} C:{4}",
                    snapshot.FreeSlotCount,
                    snapshot.ReservedSlotCount,
                    snapshot.OccupiedSlotCount,
                    snapshot.BlockedSlotCount,
                    snapshot.ClosedSlotCount);

            Rect contentRect = new Rect(rect.x + 9f, rect.y + 3f, rect.width - 16f, rect.height - 6f);
            GUI.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 18f), title, EditorStyles.boldLabel);
            GUI.Label(new Rect(contentRect.x, contentRect.y + 18f, contentRect.width, 16f), objectId + "  |  " + state, EditorStyles.miniLabel);
            GUI.Label(new Rect(contentRect.x, contentRect.y + 34f, contentRect.width, 16f), sceneName + "  |  " + slotSummary, EditorStyles.miniLabel);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                SelectSnapshot(snapshot);
                Event.current.Use();
            }
        }

        private void SelectSnapshot(SmartObjectDebugSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Component == null)
            {
                return;
            }

            selectedComponent = snapshot.Component;
            Selection.activeGameObject = snapshot.GameObject;
            EditorGUIUtility.PingObject(snapshot.GameObject);
            Repaint();
        }

        private void DrawDetails()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                SmartObjectDebugSnapshot snapshot = selectedComponent == null ? null : FindSnapshot(selectedComponent);
                if (snapshot == null && snapshots.Count > 0)
                {
                    EditorGUILayout.HelpBox("Select a SmartObject from the list to inspect its current state.", MessageType.Info);
                    return;
                }

                if (snapshot == null)
                {
                    EditorGUILayout.HelpBox("No SmartObjectComponent found in loaded scenes.", MessageType.Info);
                    return;
                }

                detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.ExpandHeight(true));
                DrawObjectDetails(snapshot);
                GUILayout.Space(8f);
                DrawSlotDetails(snapshot);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawObjectDetails(SmartObjectDebugSnapshot snapshot)
        {
            EditorGUILayout.LabelField("Object", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Component", snapshot.Component, typeof(SmartObjectComponent), true);
                    EditorGUILayout.ObjectField("GameObject", snapshot.GameObject, typeof(GameObject), true);
                }

                EditorGUILayout.LabelField("Scene", snapshot.GameObject == null ? "<none>" : snapshot.GameObject.scene.name);
                EditorGUILayout.LabelField("Hierarchy", snapshot.Component == null ? "<none>" : SmartObjectDebuggerUtility.GetHierarchyPath(snapshot.Component.transform));
                EditorGUILayout.LabelField("Registration", snapshot.RegistrationState.ToString());
                EditorGUILayout.LabelField("Registered Id", string.IsNullOrEmpty(snapshot.RegisteredObjectId) ? "<none>" : snapshot.RegisteredObjectId);
                EditorGUILayout.LabelField("Object Id", string.IsNullOrEmpty(snapshot.ObjectId) ? "<empty>" : snapshot.ObjectId);
                EditorGUILayout.LabelField("SmartObject Enabled", FormatBool(snapshot.SmartObjectEnabled));
                EditorGUILayout.LabelField("Component Enabled", FormatBool(snapshot.ComponentEnabled));
                EditorGUILayout.LabelField("Active In Hierarchy", FormatBool(snapshot.ActiveInHierarchy));
                EditorGUILayout.LabelField("Is Active And Enabled", FormatBool(snapshot.IsActiveAndEnabled));
                EditorGUILayout.LabelField("Object Base Score", FormatFloat(snapshot.ObjectBaseScore));
                EditorGUILayout.LabelField("Tags", string.IsNullOrEmpty(snapshot.Tags) ? "<none>" : snapshot.Tags);
                EditorGUILayout.LabelField("Access Group", string.IsNullOrEmpty(snapshot.AccessGroup) ? "<none>" : snapshot.AccessGroup);
                EditorGUILayout.LabelField("Registration Order", snapshot.RegistrationOrder.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void DrawSlotDetails(SmartObjectDebugSnapshot snapshot)
        {
            EditorGUILayout.LabelField("Slots", EditorStyles.boldLabel);
            if (snapshot.Slots == null || snapshot.Slots.Length == 0)
            {
                EditorGUILayout.LabelField("<none>");
                return;
            }

            for (int i = 0; i < snapshot.Slots.Length; i++)
            {
                SmartObjectSlotDebugSnapshot slot = snapshot.Slots[i];
                if (slot == null)
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawSlotHeader(snapshot, slot);
                    if (slot.IsMissing)
                    {
                        EditorGUILayout.HelpBox("This slot entry is null.", MessageType.Warning);
                        continue;
                    }

                    EditorGUILayout.LabelField("Slot Id", slot.SlotId.ToString(CultureInfo.InvariantCulture));
                    EditorGUILayout.LabelField("State", slot.State.ToString());
                    EditorGUILayout.LabelField("Runtime State", slot.RuntimeState.ToString());
                    EditorGUILayout.LabelField("Blocked", FormatBool(slot.Blocked));
                    EditorGUILayout.LabelField("Closed", FormatBool(slot.Closed));
                    EditorGUILayout.LabelField("Activities", string.IsNullOrEmpty(slot.Activities) ? "<none>" : slot.Activities);
                    EditorGUILayout.LabelField("Tags", string.IsNullOrEmpty(slot.Tags) ? "<none>" : slot.Tags);
                    EditorGUILayout.LabelField("Access Group", string.IsNullOrEmpty(slot.AccessGroup) ? "<inherit>" : slot.AccessGroup);
                    EditorGUILayout.LabelField("Slot Base Score", FormatFloat(slot.SlotBaseScore));
                    EditorGUILayout.LabelField("Use Duration", FormatFloat(slot.UseDuration));
                    EditorGUILayout.LabelField("Requester", string.IsNullOrEmpty(slot.RequesterId) ? "<none>" : slot.RequesterId);
                    DrawSelectableField("Reservation Token", slot.ReservationToken);
                    EditorGUILayout.LabelField("Remaining Seconds", FormatFloat(slot.RemainingSeconds));
                    EditorGUILayout.LabelField("Reserved Until", FormatFloat(slot.ReservedUntil));
                    EditorGUILayout.LabelField("Occupied Since", FormatFloat(slot.OccupiedSince));
                    EditorGUILayout.LabelField("Last Release Reason", string.IsNullOrEmpty(slot.LastReleaseReason) ? "<none>" : slot.LastReleaseReason);
                    EditorGUILayout.LabelField("Target Position", FormatVector3(slot.TargetPosition));
                    EditorGUILayout.LabelField("Facing Position", FormatVector3(slot.FacingPosition));
                }
            }
        }

        private void DrawSlotHeader(SmartObjectDebugSnapshot snapshot, SmartObjectSlotDebugSnapshot slot)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, SmartObjectDebuggerSceneOverlay.GetSlotColor(snapshot, slot));
            }

            string label = slot.IsMissing
                ? "Slot " + slot.Index.ToString(CultureInfo.InvariantCulture) + " <missing>"
                : "Slot " + slot.SlotId.ToString(CultureInfo.InvariantCulture) + "  |  " + slot.State;
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 18f), label, EditorStyles.whiteLabel);
        }

        private void DrawSelectableField(string label, string value)
        {
            EditorGUILayout.LabelField(label);
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            EditorGUI.SelectableLabel(rect, string.IsNullOrEmpty(value) ? "<none>" : value, EditorStyles.textField);
        }

        private List<SmartObjectDebugSnapshot> GetVisibleSnapshots()
        {
            List<SmartObjectDebugSnapshot> results = new List<SmartObjectDebugSnapshot>();
            for (int i = 0; i < snapshots.Count; i++)
            {
                SmartObjectDebugSnapshot snapshot = snapshots[i];
                if (MatchesSearch(snapshot))
                {
                    results.Add(snapshot);
                }
            }

            return results;
        }

        private bool MatchesSearch(SmartObjectDebugSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            if (snapshot == null)
            {
                return false;
            }

            string needle = searchText.Trim();
            if (Contains(snapshot.ObjectId, needle) ||
                Contains(snapshot.Tags, needle) ||
                Contains(snapshot.AccessGroup, needle) ||
                Contains(snapshot.RegistrationState.ToString(), needle) ||
                snapshot.GameObject != null && Contains(snapshot.GameObject.name, needle) ||
                snapshot.GameObject != null && Contains(snapshot.GameObject.scene.name, needle) ||
                snapshot.Component != null && Contains(SmartObjectDebuggerUtility.GetHierarchyPath(snapshot.Component.transform), needle))
            {
                return true;
            }

            for (int i = 0; i < snapshot.Slots.Length; i++)
            {
                SmartObjectSlotDebugSnapshot slot = snapshot.Slots[i];
                if (slot != null &&
                    (Contains(slot.Activities, needle) ||
                     Contains(slot.Tags, needle) ||
                     Contains(slot.AccessGroup, needle) ||
                     Contains(slot.RequesterId, needle) ||
                     Contains(slot.ReservationToken, needle) ||
                     Contains(slot.State.ToString(), needle)))
                {
                    return true;
                }
            }

            return false;
        }

        private SmartObjectDebugSnapshot FindSnapshot(SmartObjectComponent component)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i] != null && snapshots[i].Component == component)
                {
                    return snapshots[i];
                }
            }

            return null;
        }

        private static bool Contains(string value, string needle)
        {
            return !string.IsNullOrEmpty(value) &&
                   !string.IsNullOrEmpty(needle) &&
                   value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Color GetRegistrationColor(SmartObjectDebugSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new Color(0.45f, 0.45f, 0.45f, 1f);
            }

            switch (snapshot.RegistrationState)
            {
                case SmartObjectRegistrationState.Registered:
                    return new Color(0.2f, 0.72f, 0.34f, 1f);
                case SmartObjectRegistrationState.DuplicateObjectId:
                    return new Color(0.92f, 0.22f, 0.22f, 1f);
                case SmartObjectRegistrationState.MissingObjectId:
                    return new Color(0.9f, 0.55f, 0.18f, 1f);
                default:
                    return new Color(0.55f, 0.55f, 0.55f, 1f);
            }
        }

        private static string FormatBool(bool value)
        {
            return value ? "True" : "False";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatVector3(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.###}, {1:0.###}, {2:0.###})",
                value.x,
                value.y,
                value.z);
        }

        private static GUIStyle GetToolbarSearchFieldStyle()
        {
            GUIStyle style = GUI.skin.FindStyle("ToolbarSearchTextField");
            return style ?? EditorStyles.toolbarTextField;
        }
    }

    [InitializeOnLoad]
    internal static class SmartObjectDebuggerSceneOverlay
    {
        private const string GizmosEnabledKey = "BlueprintSystem.SmartObjectDebugger.SceneGizmos";

        private static readonly Color FreeColor = new Color(0.2f, 0.78f, 0.35f, 1f);
        private static readonly Color ReservedColor = new Color(1f, 0.74f, 0.18f, 1f);
        private static readonly Color OccupiedColor = new Color(0.22f, 0.56f, 1f, 1f);
        private static readonly Color BlockedColor = new Color(0.92f, 0.22f, 0.22f, 1f);
        private static readonly Color DisabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        private static bool? gizmosEnabled;

        static SmartObjectDebuggerSceneOverlay()
        {
            SceneView.duringSceneGui += DrawSceneGizmos;
        }

        public static bool GizmosEnabled
        {
            get
            {
                if (!gizmosEnabled.HasValue)
                {
                    gizmosEnabled = EditorPrefs.GetBool(GizmosEnabledKey, false);
                }

                return gizmosEnabled.Value;
            }
            set
            {
                if (GizmosEnabled == value)
                {
                    return;
                }

                gizmosEnabled = value;
                EditorPrefs.SetBool(GizmosEnabledKey, value);
                SceneView.RepaintAll();
            }
        }

        public static Color GetSlotColor(SmartObjectDebugSnapshot snapshot, SmartObjectSlotDebugSnapshot slot)
        {
            if (snapshot == null ||
                slot == null ||
                slot.IsMissing ||
                !snapshot.SmartObjectEnabled ||
                !snapshot.ComponentEnabled ||
                !snapshot.ActiveInHierarchy ||
                slot.Closed)
            {
                return DisabledColor;
            }

            switch (slot.State)
            {
                case SmartObjectSlotState.Reserved:
                    return ReservedColor;
                case SmartObjectSlotState.Occupied:
                    return OccupiedColor;
                case SmartObjectSlotState.Blocked:
                    return BlockedColor;
                default:
                    return FreeColor;
            }
        }

        private static void DrawSceneGizmos(SceneView sceneView)
        {
            if (!BlueprintModuleSettings.SmartObjectEnabled ||
                !GizmosEnabled ||
                Event.current == null ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            List<SmartObjectComponent> components = SmartObjectDebuggerUtility.FindSceneSmartObjects();
            for (int i = 0; i < components.Count; i++)
            {
                SmartObjectDebugSnapshot snapshot = SmartObjectRegistry.CreateDebugSnapshot(components[i]);
                for (int s = 0; s < snapshot.Slots.Length; s++)
                {
                    DrawSlot(snapshot, snapshot.Slots[s]);
                }
            }
        }

        private static void DrawSlot(SmartObjectDebugSnapshot snapshot, SmartObjectSlotDebugSnapshot slot)
        {
            if (snapshot == null || slot == null || slot.IsMissing)
            {
                return;
            }

            Color color = GetSlotColor(snapshot, slot);
            Vector3 target = slot.TargetPosition;
            Vector3 facing = slot.FacingPosition;
            Vector3 direction = facing - target;
            float handleSize = HandleUtility.GetHandleSize(target);
            if (direction.sqrMagnitude < 0.0001f && snapshot.Component != null)
            {
                facing = target + snapshot.Component.transform.forward * handleSize * 0.35f;
            }

            Handles.color = color;
            Handles.SphereHandleCap(0, target, Quaternion.identity, handleSize * 0.08f, EventType.Repaint);
            Handles.DrawLine(target, facing);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = color;
            Handles.Label(target + Vector3.up * handleSize * 0.12f, CreateSlotLabel(snapshot, slot), labelStyle);
        }

        private static string CreateSlotLabel(SmartObjectDebugSnapshot snapshot, SmartObjectSlotDebugSnapshot slot)
        {
            string objectId = snapshot == null || string.IsNullOrEmpty(snapshot.ObjectId) ? "<empty>" : snapshot.ObjectId;
            string label = objectId + "[" + slot.SlotId.ToString(CultureInfo.InvariantCulture) + "] " + slot.State;
            if (!string.IsNullOrEmpty(slot.RequesterId))
            {
                label += " " + slot.RequesterId;
            }

            return label;
        }
    }
}
