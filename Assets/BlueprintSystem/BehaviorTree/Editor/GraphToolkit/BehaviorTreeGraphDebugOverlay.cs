using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlueprintSystem.Editor
{
    [InitializeOnLoad]
    internal static class BehaviorTreeGraphDebugOverlay
    {
        private const string NodeViewTypeName = "Unity.GraphToolkit.Editor.NodeView";
        private const string CollapsibleNodeViewTypeName = "Unity.GraphToolkit.Editor.CollapsibleInOutNodeView";
        private const string BadgeName = "bt-debug-status-badge";
        private const float ScanIntervalSeconds = 0.2f;
        private const float StaleStatusSeconds = 1f;
        private static readonly BindingFlags ReflectionFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static BehaviorTreeRunner _pinnedRunner;
        private static double _nextScanTime;

        static BehaviorTreeGraphDebugOverlay()
        {
            EditorApplication.update += UpdateOpenGraphViews;
        }

        public static void SetPinnedRunner(BehaviorTreeRunner runner)
        {
            _pinnedRunner = runner;
            _nextScanTime = 0d;
        }

        private static void UpdateOpenGraphViews()
        {
            if (EditorApplication.timeSinceStartup < _nextScanTime)
            {
                return;
            }

            _nextScanTime = EditorApplication.timeSinceStartup + ScanIntervalSeconds;
            BehaviorTreeRunner runner = ResolveRunner();
            BehaviorTreeDebugSnapshot snapshot = Application.isPlaying && runner != null ? runner.GetDebugSnapshot() : null;
            Dictionary<string, BehaviorTreeDebugSnapshot> snapshotsBySourcePath = BuildSnapshotsBySourcePath(snapshot);

            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                if (window == null || window.rootVisualElement == null)
                {
                    continue;
                }

                RefreshElement(window.rootVisualElement, snapshotsBySourcePath);
            }
        }

        private static BehaviorTreeRunner ResolveRunner()
        {
            if (_pinnedRunner != null)
            {
                return _pinnedRunner;
            }

            return BehaviorTreeRuntimeDebugEditorUtility.FindSelectedRunner();
        }

        private static Dictionary<string, BehaviorTreeDebugSnapshot> BuildSnapshotsBySourcePath(BehaviorTreeDebugSnapshot snapshot)
        {
            Dictionary<string, BehaviorTreeDebugSnapshot> result =
                new Dictionary<string, BehaviorTreeDebugSnapshot>(StringComparer.OrdinalIgnoreCase);
            AddSnapshotBySourcePath(snapshot, result);
            return result;
        }

        private static void AddSnapshotBySourcePath(
            BehaviorTreeDebugSnapshot snapshot,
            Dictionary<string, BehaviorTreeDebugSnapshot> result)
        {
            if (snapshot == null)
            {
                return;
            }

            string path = ResolveSnapshotSourcePath(snapshot);
            if (!string.IsNullOrEmpty(path))
            {
                result[path] = snapshot;
            }

            foreach (KeyValuePair<string, BehaviorTreeDebugSnapshot> pair in snapshot.SubtreeSnapshots)
            {
                AddSnapshotBySourcePath(pair.Value, result);
            }
        }

        private static string ResolveSnapshotSourcePath(BehaviorTreeDebugSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            string path = NormalizeAssetPath(snapshot.SourcePath);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (!string.IsNullOrEmpty(snapshot.SourceGuid))
            {
                return NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(snapshot.SourceGuid));
            }

            return null;
        }

        private static void RefreshElement(
            VisualElement element,
            Dictionary<string, BehaviorTreeDebugSnapshot> snapshotsBySourcePath)
        {
            if (element == null)
            {
                return;
            }

            if (IsNodeView(element))
            {
                RefreshNodeView(element, snapshotsBySourcePath);
            }

            foreach (VisualElement child in element.Children())
            {
                RefreshElement(child, snapshotsBySourcePath);
            }
        }

        private static void RefreshNodeView(
            VisualElement nodeElement,
            Dictionary<string, BehaviorTreeDebugSnapshot> snapshotsBySourcePath)
        {
            object graphNode = TryGetGraphNode(nodeElement);
            BehaviorTreeVisualNode behaviorNode = graphNode as BehaviorTreeVisualNode;
            BehaviorTreeVisualDecoratorNode decoratorNode = graphNode as BehaviorTreeVisualDecoratorNode;
            if (behaviorNode == null && decoratorNode == null)
            {
                ClearDebugStyle(nodeElement);
                return;
            }

            BehaviorTreeVisualGraph graph = TryGetOwnerGraph(graphNode);
            BehaviorTreeDebugSnapshot snapshot = ResolveSnapshotForGraph(graph, snapshotsBySourcePath);
            if (snapshot == null)
            {
                ClearDebugStyle(nodeElement);
                return;
            }

            if (behaviorNode != null)
            {
                string nodeId = behaviorNode.ReadNodeId();
                BehaviorTreeDebugVisualStyle style =
                    BehaviorTreeRuntimeDebugEditorUtility.GetNodeVisualStyle(snapshot, nodeId, StaleStatusSeconds);
                ApplyDebugStyle(nodeElement, style);
                return;
            }

            string decoratorId = decoratorNode.ReadDecoratorId();
            BehaviorTreeDebugVisualStyle decoratorStyle =
                BehaviorTreeRuntimeDebugEditorUtility.GetDecoratorVisualStyle(snapshot, decoratorId);
            ApplyDebugStyle(nodeElement, decoratorStyle);
        }

        private static BehaviorTreeDebugSnapshot ResolveSnapshotForGraph(
            BehaviorTreeVisualGraph graph,
            Dictionary<string, BehaviorTreeDebugSnapshot> snapshotsBySourcePath)
        {
            if (graph == null || snapshotsBySourcePath == null || snapshotsBySourcePath.Count == 0)
            {
                return null;
            }

            string graphPath = NormalizeAssetPath(graph.SourceBehaviorTreeAssetPath);
            if (string.IsNullOrEmpty(graphPath))
            {
                return null;
            }

            BehaviorTreeDebugSnapshot snapshot;
            return snapshotsBySourcePath.TryGetValue(graphPath, out snapshot) ? snapshot : null;
        }

        private static object TryGetGraphNode(VisualElement nodeElement)
        {
            object model = GetPropertyValue(nodeElement, "Model");
            object graphNode = GetPropertyValue(model, "Node");
            return graphNode ?? model;
        }

        private static BehaviorTreeVisualGraph TryGetOwnerGraph(object node)
        {
            if (node == null)
            {
                return null;
            }

            object graphModel = GetPropertyValue(node, "GraphModel");
            if (graphModel == null)
            {
                object nodeModel = GetFieldValue(typeof(Node), node, "m_Implementation");
                graphModel = GetPropertyValue(nodeModel, "GraphModel");
            }

            return GetPropertyValue(graphModel, "Graph") as BehaviorTreeVisualGraph;
        }

        private static bool IsNodeView(VisualElement element)
        {
            for (Type type = element.GetType(); type != null; type = type.BaseType)
            {
                string fullName = type.FullName;
                if (fullName == NodeViewTypeName || fullName == CollapsibleNodeViewTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyDebugStyle(VisualElement nodeElement, BehaviorTreeDebugVisualStyle style)
        {
            if (style.State == BehaviorTreeDebugVisualState.None)
            {
                ClearDebugStyle(nodeElement);
                return;
            }

            nodeElement.style.borderTopColor = style.Color;
            nodeElement.style.borderRightColor = style.Color;
            nodeElement.style.borderBottomColor = style.Color;
            nodeElement.style.borderLeftColor = style.Color;
            nodeElement.style.borderTopWidth = style.BorderWidth;
            nodeElement.style.borderRightWidth = style.BorderWidth;
            nodeElement.style.borderBottomWidth = style.BorderWidth;
            nodeElement.style.borderLeftWidth = style.BorderWidth;

            Label badge = nodeElement.Q<Label>(BadgeName);
            if (badge == null)
            {
                badge = CreateBadge();
                nodeElement.Add(badge);
            }

            badge.text = style.Label;
            badge.style.backgroundColor = style.Color;
            badge.style.opacity = style.Opacity;
            badge.style.display = DisplayStyle.Flex;
        }

        private static void ClearDebugStyle(VisualElement nodeElement)
        {
            if (nodeElement == null)
            {
                return;
            }

            nodeElement.style.borderTopWidth = StyleKeyword.Null;
            nodeElement.style.borderRightWidth = StyleKeyword.Null;
            nodeElement.style.borderBottomWidth = StyleKeyword.Null;
            nodeElement.style.borderLeftWidth = StyleKeyword.Null;
            nodeElement.style.borderTopColor = StyleKeyword.Null;
            nodeElement.style.borderRightColor = StyleKeyword.Null;
            nodeElement.style.borderBottomColor = StyleKeyword.Null;
            nodeElement.style.borderLeftColor = StyleKeyword.Null;

            Label badge = nodeElement.Q<Label>(BadgeName);
            if (badge != null)
            {
                badge.style.display = DisplayStyle.None;
            }
        }

        private static Label CreateBadge()
        {
            Label badge = new Label
            {
                name = BadgeName
            };
            badge.style.position = Position.Absolute;
            badge.style.top = 4f;
            badge.style.right = 6f;
            badge.style.paddingLeft = 5f;
            badge.style.paddingRight = 5f;
            badge.style.paddingTop = 1f;
            badge.style.paddingBottom = 1f;
            badge.style.color = Color.white;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.fontSize = 9f;
            return badge;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? null : path.Replace('\\', '/');
        }

        private static object GetPropertyValue(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            PropertyInfo property = FindProperty(target.GetType(), name);
            if (property == null)
            {
                return null;
            }

            try
            {
                return property.GetValue(target, null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object GetFieldValue(Type ownerType, object target, string name)
        {
            if (ownerType == null || target == null || !ownerType.IsInstanceOfType(target))
            {
                return null;
            }

            FieldInfo field = FindField(ownerType, name);
            if (field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(target);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, ReflectionFlags | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, ReflectionFlags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }
    }
}
