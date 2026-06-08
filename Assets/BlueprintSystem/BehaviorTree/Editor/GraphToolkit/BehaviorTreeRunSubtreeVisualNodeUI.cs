using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace BlueprintSystem.Editor
{
    [InitializeOnLoad]
    internal static class BehaviorTreeRunSubtreeVisualNodeUI
    {
        private const string GraphViewTypeName = "Unity.GraphToolkit.Editor.GraphView";
        private const string NodeViewTypeName = "Unity.GraphToolkit.Editor.NodeView";
        private const string CollapsibleNodeViewTypeName = "Unity.GraphToolkit.Editor.CollapsibleInOutNodeView";
        private const string BehaviorTreeOptionName = "behaviorTree";
        private const string RowName = "bt-run-subtree-object-field-row";
        private const string FieldName = "bt-run-subtree-object-field";
        private const string OpenButtonName = "bt-run-subtree-open-button";
        private static readonly BindingFlags ReflectionFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static bool _isRefreshing;
        private static double _nextScanTime;

        static BehaviorTreeRunSubtreeVisualNodeUI()
        {
            EditorApplication.update += RegisterOpenGraphViews;
        }

        private static void RegisterOpenGraphViews()
        {
            if (EditorApplication.timeSinceStartup < _nextScanTime)
            {
                return;
            }

            _nextScanTime = EditorApplication.timeSinceStartup + 0.5d;
            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                if (window == null || window.rootVisualElement == null)
                {
                    continue;
                }

                RegisterRunSubtreeFields(window.rootVisualElement);
            }
        }

        private static void RegisterRunSubtreeFields(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            BTTaskRunSubtreeNode node;
            if (IsNodeView(element) && TryGetRunSubtreeNode(element, out node))
            {
                EnsureRunSubtreeField(element, node);
            }

            foreach (VisualElement child in element.Children())
            {
                RegisterRunSubtreeFields(child);
            }
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

        private static bool TryGetRunSubtreeNode(VisualElement element, out BTTaskRunSubtreeNode node)
        {
            node = null;
            object model = GetPropertyValue(element, "Model");
            if (model == null)
            {
                return false;
            }

            node = GetPropertyValue(model, "Node") as BTTaskRunSubtreeNode;
            return node != null;
        }

        private static void EnsureRunSubtreeField(VisualElement nodeElement, BTTaskRunSubtreeNode node)
        {
            VisualElement row = nodeElement.Q<VisualElement>(RowName);
            if (row == null)
            {
                row = CreateRunSubtreeFieldRow(nodeElement, node);
                InsertBeforePortContainer(nodeElement, row);
            }

            RefreshRunSubtreeField(row, node);
        }

        private static VisualElement CreateRunSubtreeFieldRow(VisualElement nodeElement, BTTaskRunSubtreeNode node)
        {
            VisualElement row = new VisualElement { name = RowName };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginLeft = 8f;
            row.style.marginRight = 8f;
            row.style.marginTop = 2f;
            row.style.marginBottom = 4f;

            ObjectField field = new ObjectField("Subtree")
            {
                name = FieldName,
                objectType = typeof(Object),
                allowSceneObjects = false
            };
            field.style.flexGrow = 1f;
            field.style.minWidth = 140f;
            field.RegisterValueChangedCallback(evt =>
            {
                if (_isRefreshing)
                {
                    return;
                }

                string selectedPath = BehaviorTreeGraphToolkitBehaviorTreeTypes.GetBehaviorTreeAssetPath(evt.newValue);
                if (evt.newValue != null && string.IsNullOrEmpty(selectedPath))
                {
                    field.SetValueWithoutNotify(evt.previousValue);
                    return;
                }

                ApplyBehaviorTreePath(nodeElement, node, selectedPath ?? string.Empty);
                RefreshRunSubtreeField(row, node);
            });

            Button openButton = new Button(() =>
            {
                BehaviorTreeVisualGraph graph = TryGetOwnerGraph(node);
                BehaviorTreeGraphToolkitBehaviorTreeTypes.OpenAsset(ReadBehaviorTreePath(node), graph);
            })
            {
                name = OpenButtonName,
                text = "Open"
            };
            openButton.style.width = 54f;
            openButton.style.marginLeft = 4f;

            row.Add(field);
            row.Add(openButton);
            return row;
        }

        private static void InsertBeforePortContainer(VisualElement nodeElement, VisualElement row)
        {
            VisualElement portContainer = FindNamedElement(nodeElement, "port-container");
            if (portContainer != null && portContainer.parent == nodeElement)
            {
                int index = nodeElement.IndexOf(portContainer);
                nodeElement.Insert(Mathf.Max(0, index), row);
                return;
            }

            nodeElement.Add(row);
        }

        private static void RefreshRunSubtreeField(VisualElement row, BTTaskRunSubtreeNode node)
        {
            if (row == null || node == null)
            {
                return;
            }

            BehaviorTreeVisualGraph graph = TryGetOwnerGraph(node);
            string behaviorTreePath = ReadBehaviorTreePath(node);
            Object asset = BehaviorTreeGraphToolkitBehaviorTreeTypes.LoadAsset(behaviorTreePath, graph);

            ObjectField field = row.Q<ObjectField>(FieldName);
            Button openButton = row.Q<Button>(OpenButtonName);

            _isRefreshing = true;
            try
            {
                if (field != null)
                {
                    field.SetValueWithoutNotify(asset);
                }
            }
            finally
            {
                _isRefreshing = false;
            }

            if (openButton != null)
            {
                openButton.SetEnabled(BehaviorTreeGraphToolkitBehaviorTreeTypes.CanOpen(behaviorTreePath, graph));
            }
        }

        private static void ApplyBehaviorTreePath(VisualElement nodeElement, BTTaskRunSubtreeNode node, string behaviorTreePath)
        {
            if (node == null)
            {
                return;
            }

            behaviorTreePath = BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(behaviorTreePath);
            WriteBehaviorTreePathToProperties(node, behaviorTreePath);
            WriteBehaviorTreePathToOption(node, behaviorTreePath);
            RefreshTitle(nodeElement, node, behaviorTreePath);

            BehaviorTreeVisualGraph graph = TryGetOwnerGraph(node);
            if (graph != null)
            {
                BehaviorTreeGraphToolkitReflection.MarkDirty(graph);
            }
        }

        private static void WriteBehaviorTreePathToProperties(BTTaskRunSubtreeNode node, string behaviorTreePath)
        {
            Dictionary<string, object> properties = ReadProperties(node.PropertiesJson);
            properties[BehaviorTreeOptionName] = behaviorTreePath ?? string.Empty;
            node.PropertiesJson = BlueprintJson.Serialize(properties, false);
        }

        private static void WriteBehaviorTreePathToOption(BTTaskRunSubtreeNode node, string behaviorTreePath)
        {
            INodeOption option = null;
            try
            {
                option = node.GetNodeOptionByName(BehaviorTreeOptionName);
            }
            catch
            {
            }

            object portModel = GetPropertyValue(option, "PortModel");
            object embeddedValue = GetPropertyValue(portModel, "EmbeddedValue");
            if (embeddedValue == null)
            {
                return;
            }

            object graphValue = BehaviorTreeGraphToolkitBehaviorTreeTypes.CreateGraphValue(behaviorTreePath);
            PropertyInfo objectValueProperty = embeddedValue.GetType().GetProperty("ObjectValue", ReflectionFlags);
            if (objectValueProperty != null)
            {
                objectValueProperty.SetValue(embeddedValue, graphValue, null);
            }

            Action<object> setter = GetPropertyValue(embeddedValue, "SetterMethod") as Action<object>;
            if (setter != null)
            {
                setter(graphValue);
            }
        }

        private static string ReadBehaviorTreePath(BTTaskRunSubtreeNode node)
        {
            string behaviorTreePath;
            if (TryReadBehaviorTreeOptionPath(node, out behaviorTreePath))
            {
                return behaviorTreePath;
            }

            Dictionary<string, object> properties = ReadProperties(node == null ? null : node.PropertiesJson);
            object value;
            return properties.TryGetValue(BehaviorTreeOptionName, out value) && value != null
                ? BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(Convert.ToString(value))
                : string.Empty;
        }

        private static bool TryReadBehaviorTreeOptionPath(BTTaskRunSubtreeNode node, out string behaviorTreePath)
        {
            behaviorTreePath = null;
            if (node == null)
            {
                return false;
            }

            INodeOption option = null;
            try
            {
                option = node.GetNodeOptionByName(BehaviorTreeOptionName);
            }
            catch
            {
            }

            if (option == null)
            {
                return false;
            }

            BehaviorTreeAsset asset;
            if (option.TryGetValue(out asset))
            {
                behaviorTreePath = BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(asset.Path);
                return true;
            }

            string text;
            if (option.TryGetValue(out text))
            {
                behaviorTreePath = BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(text);
                return true;
            }

            return false;
        }

        private static Dictionary<string, object> ReadProperties(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            try
            {
                return BlueprintJson.DeserializeObject(json);
            }
            catch (BlueprintJsonException)
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }
        }

        private static void RefreshTitle(VisualElement nodeElement, BTTaskRunSubtreeNode node, string behaviorTreePath)
        {
            string displayName = BehaviorTreeGraphToolkitBehaviorTreeTypes.GetDisplayName(behaviorTreePath);
            string title = string.IsNullOrEmpty(displayName) ? "Task: Run Subtree" : "Task: Run Subtree: " + displayName;
            node.Title = title;

            VisualElement titleElement = FindNamedElement(nodeElement, "title");
            if (titleElement != null)
            {
                SetTextElementText(titleElement, title);
            }
        }

        private static BehaviorTreeVisualGraph TryGetOwnerGraph(BTTaskRunSubtreeNode node)
        {
            object nodeModel = GetFieldValue(typeof(Node), node, "m_Implementation");
            object graphModel = GetPropertyValue(nodeModel, "GraphModel");
            return GetPropertyValue(graphModel, "Graph") as BehaviorTreeVisualGraph;
        }

        private static object GetPropertyValue(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            PropertyInfo property = target.GetType().GetProperty(name, ReflectionFlags);
            return property == null ? null : property.GetValue(target, null);
        }

        private static object GetFieldValue(Type ownerType, object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            FieldInfo field = ownerType.GetField(name, ReflectionFlags);
            return field == null ? null : field.GetValue(target);
        }

        private static VisualElement FindNamedElement(VisualElement element, string name)
        {
            if (element == null)
            {
                return null;
            }

            if (element.name == name)
            {
                return element;
            }

            foreach (VisualElement child in element.Children())
            {
                VisualElement result = FindNamedElement(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void SetTextElementText(VisualElement element, string text)
        {
            Label label = element as Label;
            if (label != null)
            {
                label.text = text;
                return;
            }

            TextElement textElement = element as TextElement;
            if (textElement != null)
            {
                textElement.text = text;
                return;
            }

            MethodInfo setValueMethod = element.GetType().GetMethod(
                "SetValueWithoutNotify",
                ReflectionFlags,
                null,
                new[] { typeof(string) },
                null);
            if (setValueMethod != null)
            {
                setValueMethod.Invoke(element, new object[] { text });
            }
        }
    }
}
