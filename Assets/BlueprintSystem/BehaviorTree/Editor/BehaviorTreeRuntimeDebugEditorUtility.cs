using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    internal enum BehaviorTreeDebugVisualState
    {
        None,
        Active,
        Running,
        Success,
        Failure,
        RecentStatus,
        StaleStatus
    }

    internal struct BehaviorTreeDebugVisualStyle
    {
        public BehaviorTreeDebugVisualState State;
        public string Label;
        public Color Color;
        public float BorderWidth;
        public float Opacity;
    }

    internal static class BehaviorTreeRuntimeDebugEditorUtility
    {
        private const float StaleStatusSeconds = 1f;
        private const int MaxBlackboardRows = 32;

        public static BehaviorTreeRunner FindSelectedRunner()
        {
            BehaviorTreeRunner runner = Selection.activeObject as BehaviorTreeRunner;
            if (runner != null)
            {
                return runner;
            }

            GameObject gameObject = Selection.activeGameObject;
            return gameObject == null ? null : gameObject.GetComponent<BehaviorTreeRunner>();
        }

        public static void DrawRunnerDebugPanel(BehaviorTreeRunner runner)
        {
            if (!BlueprintModuleSettings.BehaviorTreeEnabled)
            {
                EditorGUILayout.HelpBox("The Behavior Tree module is disabled in Project Settings > Blueprint System > Modules.", MessageType.Warning);
                return;
            }

            if (runner == null)
            {
                EditorGUILayout.HelpBox("Select or pin a BehaviorTreeRunner to inspect runtime debug state.", MessageType.Info);
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Runtime debug is available in Play Mode.", MessageType.Info);
                DrawDebugGraphControls(runner, null);
                return;
            }

            BehaviorTreeDebugSnapshot snapshot = runner.GetDebugSnapshot();
            DrawDebugGraphControls(runner, snapshot);
            DrawSnapshotSummary(runner, snapshot);
            DrawActivePath(snapshot);
            DrawRunningTasks(snapshot);
            DrawReasons(snapshot);
            DrawServices(snapshot);
            DrawBlackboard(snapshot);
            DrawSubtrees(snapshot);
        }

        public static BehaviorTreeDebugVisualStyle GetNodeVisualStyle(
            BehaviorTreeDebugSnapshot snapshot,
            string nodeId,
            float staleStatusSeconds)
        {
            BehaviorTreeDebugVisualStyle style = new BehaviorTreeDebugVisualStyle
            {
                State = BehaviorTreeDebugVisualState.None,
                Label = string.Empty,
                Color = new Color(0.35f, 0.35f, 0.35f, 1f),
                BorderWidth = 0f,
                Opacity = 0f
            };

            if (snapshot == null || string.IsNullOrEmpty(nodeId))
            {
                return style;
            }

            if (snapshot.RunningTaskNodeIds.Contains(nodeId) || snapshot.RunningTaskNodeId == nodeId)
            {
                style.State = BehaviorTreeDebugVisualState.Running;
                style.Label = CreateRunningLabel(snapshot, nodeId);
                style.Color = new Color(1f, 0.78f, 0.18f, 1f);
                style.BorderWidth = 3f;
                style.Opacity = 1f;
                return style;
            }

            if (snapshot.ActivePath.Contains(nodeId))
            {
                style.State = BehaviorTreeDebugVisualState.Active;
                style.Label = "ACTIVE";
                style.Color = new Color(0.22f, 0.56f, 1f, 1f);
                style.BorderWidth = 2f;
                style.Opacity = 1f;
                return style;
            }

            string status;
            if (!snapshot.NodeStatuses.TryGetValue(nodeId, out status))
            {
                return style;
            }

            float tickTime;
            bool hasTickTime = snapshot.NodeTickTimes.TryGetValue(nodeId, out tickTime);
            float age = hasTickTime ? Mathf.Max(0f, snapshot.TimeSeconds - tickTime) : 0f;
            bool stale = hasTickTime && age > Mathf.Max(0f, staleStatusSeconds);
            if (status == BehaviorTreeStatus.Success.ToString())
            {
                style.State = stale ? BehaviorTreeDebugVisualState.StaleStatus : BehaviorTreeDebugVisualState.Success;
                style.Label = "SUCCESS";
                style.Color = new Color(0.19f, 0.72f, 0.38f, 1f);
            }
            else if (status == BehaviorTreeStatus.Failure.ToString())
            {
                style.State = stale ? BehaviorTreeDebugVisualState.StaleStatus : BehaviorTreeDebugVisualState.Failure;
                style.Label = "FAILURE";
                style.Color = new Color(0.92f, 0.22f, 0.22f, 1f);
            }
            else
            {
                style.State = stale ? BehaviorTreeDebugVisualState.StaleStatus : BehaviorTreeDebugVisualState.RecentStatus;
                style.Label = status.ToUpperInvariant();
                style.Color = new Color(0.55f, 0.55f, 0.55f, 1f);
            }

            style.BorderWidth = stale ? 1f : 2f;
            style.Opacity = stale ? 0.35f : 1f;
            return style;
        }

        private static string CreateRunningLabel(BehaviorTreeDebugSnapshot snapshot, string nodeId)
        {
            BehaviorTreeDebugSnapshot subtreeSnapshot;
            if (snapshot != null &&
                !string.IsNullOrEmpty(nodeId) &&
                snapshot.SubtreeSnapshots.TryGetValue(nodeId, out subtreeSnapshot) &&
                subtreeSnapshot != null &&
                subtreeSnapshot.RunningTaskNodeIds.Count > 0)
            {
                return "RUNNING: " + subtreeSnapshot.RunningTaskNodeIds[0];
            }

            return "RUNNING";
        }

        public static BehaviorTreeDebugVisualStyle GetDecoratorVisualStyle(
            BehaviorTreeDebugSnapshot snapshot,
            string decoratorId)
        {
            BehaviorTreeDebugVisualStyle style = new BehaviorTreeDebugVisualStyle
            {
                State = BehaviorTreeDebugVisualState.None,
                Label = string.Empty,
                Color = new Color(0.35f, 0.35f, 0.35f, 1f),
                BorderWidth = 0f,
                Opacity = 0f
            };

            if (snapshot == null || string.IsNullOrEmpty(decoratorId))
            {
                return style;
            }

            bool result;
            if (!snapshot.DecoratorResults.TryGetValue(decoratorId, out result))
            {
                return style;
            }

            style.State = result ? BehaviorTreeDebugVisualState.Success : BehaviorTreeDebugVisualState.Failure;
            style.Label = result ? "TRUE" : "FALSE";
            style.Color = result ? new Color(0.19f, 0.72f, 0.38f, 1f) : new Color(0.92f, 0.22f, 0.22f, 1f);
            style.BorderWidth = 2f;
            style.Opacity = 1f;
            return style;
        }

        public static void OpenDebugGraph(BehaviorTreeRunner runner, bool followRunningNode)
        {
            if (!BlueprintModuleSettings.BehaviorTreeEnabled)
            {
                EditorUtility.DisplayDialog(
                    "Behavior Tree Disabled",
                    "The Behavior Tree module is disabled in Project Settings > Blueprint System > Modules.",
                    "OK");
                return;
            }

            if (runner == null || runner.CompiledBehaviorTree == null)
            {
                return;
            }

            BehaviorTreeGraphDebugOverlay.SetPinnedRunner(runner);
            if (followRunningNode)
            {
                BehaviorTreeDebuggerWindow.ShowRunner(runner);
            }

            string compiledPath = AssetDatabase.GetAssetPath(runner.CompiledBehaviorTree);
            if (!string.IsNullOrEmpty(compiledPath))
            {
                BehaviorTreeGraphToolkitBridge.OpenCompiledAssetAtPath(compiledPath);
            }
        }

        public static string FormatSnapshot(BehaviorTreeDebugSnapshot snapshot)
        {
            StringBuilder builder = new StringBuilder();
            AppendSnapshot(builder, snapshot, 0, "Root");
            return builder.ToString();
        }

        private static void DrawDebugGraphControls(BehaviorTreeRunner runner, BehaviorTreeDebugSnapshot snapshot)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(runner == null || runner.CompiledBehaviorTree == null))
                {
                    if (GUILayout.Button("Open Debug Graph"))
                    {
                        OpenDebugGraph(runner, false);
                    }

                    if (GUILayout.Button("Follow Running Node"))
                    {
                        OpenDebugGraph(runner, true);
                    }
                }

                using (new EditorGUI.DisabledScope(snapshot == null))
                {
                    if (GUILayout.Button("Copy Snapshot"))
                    {
                        EditorGUIUtility.systemCopyBuffer = FormatSnapshot(snapshot);
                    }
                }
            }
        }

        private static void DrawSnapshotSummary(BehaviorTreeRunner runner, BehaviorTreeDebugSnapshot snapshot)
        {
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string treeName = snapshot == null || string.IsNullOrEmpty(snapshot.TreeName)
                    ? (runner.CompiledBehaviorTree == null ? "<none>" : runner.CompiledBehaviorTree.BehaviorTreeName)
                    : snapshot.TreeName;
                EditorGUILayout.LabelField("Tree", treeName);
                EditorGUILayout.LabelField("Runner", runner.IsRunning ? "Running" : "Stopped");
                EditorGUILayout.LabelField("Last Status", snapshot == null ? "<none>" : snapshot.LastStatus.ToString());
                EditorGUILayout.LabelField("Tick", snapshot == null ? "0" : snapshot.TickIndex.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Time", snapshot == null ? "0.000s" : snapshot.TimeSeconds.ToString("0.000", CultureInfo.InvariantCulture) + "s");
            }
        }

        private static void DrawActivePath(BehaviorTreeDebugSnapshot snapshot)
        {
            EditorGUILayout.LabelField("Active Path", EditorStyles.boldLabel);
            string path = snapshot == null || snapshot.ActivePath.Count == 0
                ? "<none>"
                : string.Join(" > ", snapshot.ActivePath.ToArray());
            EditorGUILayout.SelectableLabel(path, EditorStyles.textField, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
        }

        private static void DrawRunningTasks(BehaviorTreeDebugSnapshot snapshot)
        {
            EditorGUILayout.LabelField("Running Nodes", EditorStyles.boldLabel);
            if (snapshot == null || snapshot.RunningTaskNodeIds.Count == 0)
            {
                EditorGUILayout.LabelField("<none>");
                return;
            }

            for (int i = 0; i < snapshot.RunningTaskNodeIds.Count; i++)
            {
                EditorGUILayout.LabelField(snapshot.RunningTaskNodeIds[i]);
            }
        }

        private static void DrawReasons(BehaviorTreeDebugSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(snapshot.LastFailureReason))
            {
                EditorGUILayout.HelpBox(snapshot.LastFailureReason, MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(snapshot.LastAbortReason))
            {
                EditorGUILayout.HelpBox(snapshot.LastAbortReason, MessageType.Info);
            }
        }

        private static void DrawServices(BehaviorTreeDebugSnapshot snapshot)
        {
            if (snapshot == null || snapshot.ServiceStates.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Services", EditorStyles.boldLabel);
            foreach (KeyValuePair<string, BehaviorTreeDebugServiceState> pair in snapshot.ServiceStates)
            {
                BehaviorTreeDebugServiceState state = pair.Value;
                if (state == null)
                {
                    continue;
                }

                EditorGUILayout.LabelField(
                    pair.Key,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}, last {1:0.000}s, next {2:0.000}s",
                        state.Active ? "active" : "inactive",
                        state.LastTickTime,
                        state.NextTickTime));
            }
        }

        private static void DrawBlackboard(BehaviorTreeDebugSnapshot snapshot)
        {
            EditorGUILayout.LabelField("Blackboard", EditorStyles.boldLabel);
            if (snapshot == null || snapshot.BlackboardValues == null || snapshot.BlackboardValues.Count == 0)
            {
                EditorGUILayout.LabelField("<empty>");
                return;
            }

            int count = 0;
            foreach (KeyValuePair<string, object> pair in snapshot.BlackboardValues)
            {
                if (count >= MaxBlackboardRows)
                {
                    EditorGUILayout.LabelField("...", (snapshot.BlackboardValues.Count - count).ToString(CultureInfo.InvariantCulture) + " more");
                    break;
                }

                EditorGUILayout.LabelField(pair.Key, FormatValue(pair.Value));
                count++;
            }
        }

        private static void DrawSubtrees(BehaviorTreeDebugSnapshot snapshot)
        {
            if (snapshot == null || snapshot.SubtreeSnapshots.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Subtrees", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            foreach (KeyValuePair<string, BehaviorTreeDebugSnapshot> pair in snapshot.SubtreeSnapshots)
            {
                BehaviorTreeDebugSnapshot subtree = pair.Value;
                EditorGUILayout.LabelField(pair.Key, subtree == null ? "<missing snapshot>" : subtree.TreeName);
                if (subtree != null)
                {
                    EditorGUI.indentLevel++;
                    DrawActivePath(subtree);
                    DrawRunningTasks(subtree);
                    DrawReasons(subtree);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.indentLevel--;
        }

        private static void AppendSnapshot(StringBuilder builder, BehaviorTreeDebugSnapshot snapshot, int depth, string label)
        {
            string indent = new string(' ', depth * 2);
            if (snapshot == null)
            {
                builder.Append(indent).Append(label).AppendLine(": <null>");
                return;
            }

            builder.Append(indent).Append(label).Append(": ");
            builder.Append(string.IsNullOrEmpty(snapshot.TreeName) ? "<unnamed>" : snapshot.TreeName);
            builder.Append(" status=").Append(snapshot.LastStatus);
            builder.Append(" tick=").Append(snapshot.TickIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" time=").Append(snapshot.TimeSeconds.ToString("0.000", CultureInfo.InvariantCulture)).AppendLine("s");
            builder.Append(indent).Append("  activePath=").AppendLine(string.Join(" > ", snapshot.ActivePath.ToArray()));
            builder.Append(indent).Append("  running=").AppendLine(string.Join(", ", snapshot.RunningTaskNodeIds.ToArray()));
            if (!string.IsNullOrEmpty(snapshot.LastFailureReason))
            {
                builder.Append(indent).Append("  failure=").AppendLine(snapshot.LastFailureReason);
            }

            if (!string.IsNullOrEmpty(snapshot.LastAbortReason))
            {
                builder.Append(indent).Append("  abort=").AppendLine(snapshot.LastAbortReason);
            }

            foreach (KeyValuePair<string, object> pair in snapshot.BlackboardValues)
            {
                builder.Append(indent).Append("  bb.").Append(pair.Key).Append('=').AppendLine(FormatValue(pair.Value));
            }

            foreach (KeyValuePair<string, BehaviorTreeDebugSnapshot> pair in snapshot.SubtreeSnapshots)
            {
                AppendSnapshot(builder, pair.Value, depth + 1, "subtree " + pair.Key);
            }
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            UnityEngine.Object unityObject = value as UnityEngine.Object;
            if (unityObject != null)
            {
                return unityObject.name;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    internal sealed class BehaviorTreeDebuggerWindow : EditorWindow
    {
        private BehaviorTreeRunner pinnedRunner;

        [MenuItem("Tools/Blueprint System/Behavior Tree/Debugger")]
        public static void OpenWindow()
        {
            if (!BlueprintModuleSettings.BehaviorTreeEnabled)
            {
                EditorUtility.DisplayDialog(
                    "Behavior Tree Disabled",
                    "The Behavior Tree module is disabled in Project Settings > Blueprint System > Modules.",
                    "OK");
                return;
            }

            BehaviorTreeDebuggerWindow window = GetWindow<BehaviorTreeDebuggerWindow>("BT Debugger");
            if (window.pinnedRunner == null)
            {
                window.pinnedRunner = BehaviorTreeRuntimeDebugEditorUtility.FindSelectedRunner();
            }

            if (window.pinnedRunner != null)
            {
                BehaviorTreeGraphDebugOverlay.SetPinnedRunner(window.pinnedRunner);
            }

            window.Show();
        }

        [MenuItem("Tools/Blueprint System/Behavior Tree/Debugger", true)]
        private static bool CanOpenWindow()
        {
            return BlueprintModuleSettings.BehaviorTreeEnabled;
        }

        public static void ShowRunner(BehaviorTreeRunner runner)
        {
            if (!BlueprintModuleSettings.BehaviorTreeEnabled)
            {
                EditorUtility.DisplayDialog(
                    "Behavior Tree Disabled",
                    "The Behavior Tree module is disabled in Project Settings > Blueprint System > Modules.",
                    "OK");
                return;
            }

            BehaviorTreeDebuggerWindow window = GetWindow<BehaviorTreeDebuggerWindow>("BT Debugger");
            window.pinnedRunner = runner;
            BehaviorTreeGraphDebugOverlay.SetPinnedRunner(runner);
            window.Show();
            window.Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (!BlueprintModuleSettings.BehaviorTreeEnabled)
            {
                EditorGUILayout.HelpBox("The Behavior Tree module is disabled in Project Settings > Blueprint System > Modules.", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                BehaviorTreeRunner selectedRunner = BehaviorTreeRuntimeDebugEditorUtility.FindSelectedRunner();
                using (new EditorGUI.DisabledScope(selectedRunner == null))
                {
                    if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                    {
                        pinnedRunner = selectedRunner;
                        BehaviorTreeGraphDebugOverlay.SetPinnedRunner(pinnedRunner);
                    }
                }

                if (GUILayout.Button("Clear Pin", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    pinnedRunner = null;
                    BehaviorTreeGraphDebugOverlay.SetPinnedRunner(null);
                }
            }

            EditorGUI.BeginChangeCheck();
            pinnedRunner = (BehaviorTreeRunner)EditorGUILayout.ObjectField("Pinned Runner", pinnedRunner, typeof(BehaviorTreeRunner), true);
            if (EditorGUI.EndChangeCheck())
            {
                BehaviorTreeGraphDebugOverlay.SetPinnedRunner(pinnedRunner);
            }

            BehaviorTreeRunner runner = pinnedRunner != null
                ? pinnedRunner
                : BehaviorTreeRuntimeDebugEditorUtility.FindSelectedRunner();
            BehaviorTreeRuntimeDebugEditorUtility.DrawRunnerDebugPanel(runner);
        }
    }
}
