using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    public static class BehaviorTreeGraphToolkitBridge
    {
        private const string ImportMenu = "Tools/Blueprint System/Behavior Tree/Import Selected Behavior Tree JSON";
        private const string ExportMenu = "Tools/Blueprint System/Behavior Tree/Export Selected Behavior Tree Graph To JSON";
        private const string BehaviorTreeAssetSuffix = ".btree";
        private const string BehaviorTreeJsonAssetSuffix = ".btree.json";

        [MenuItem(ImportMenu)]
        public static void ImportSelectedBehaviorTreeJson()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            string graphPath = ImportBehaviorTreeAtPath(path, true);
            Debug.Log("[BehaviorTree] Imported visual graph: " + graphPath);
        }

        [MenuItem(ImportMenu, true)]
        private static bool CanImportSelectedBehaviorTreeJson()
        {
            return BehaviorTreeCompiledAssetCompiler.IsBehaviorTreeJsonPath(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [MenuItem(ExportMenu)]
        public static void ExportSelectedBehaviorTreeGraph()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            string outputPath = ExportGraphAtPath(path, null);
            Debug.Log("[BehaviorTree] Exported behavior tree JSON: " + outputPath);
        }

        [MenuItem(ExportMenu, true)]
        private static bool CanExportSelectedBehaviorTreeGraph()
        {
            return IsBehaviorTreeGraphAssetPath(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [OnOpenAsset(0)]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            return OpenAssetAtPath(GetAssetPathFromOpenAssetId(instanceId));
        }

        public static string ImportBehaviorTreeAtPath(string behaviorTreeAssetPath, bool openAsset)
        {
            return ImportBehaviorTreeAtPath(behaviorTreeAssetPath, GetDefaultGraphPath(behaviorTreeAssetPath), openAsset);
        }

        public static bool OpenAssetAtPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            if (BehaviorTreeCompiledAssetCompiler.IsBehaviorTreeJsonPath(assetPath))
            {
                return OpenBehaviorTreeJsonAtPath(assetPath);
            }

            return OpenCompiledAssetAtPath(assetPath);
        }

        public static bool OpenCompiledAssetAtPath(string assetPath)
        {
            BehaviorTreeCompiledAsset compiledAsset = AssetDatabase.LoadAssetAtPath<BehaviorTreeCompiledAsset>(assetPath);
            if (compiledAsset == null)
            {
                return false;
            }

            string sourcePath = BehaviorTreeCompiledAssetCompiler.GetCompiledAssetSourcePath(compiledAsset);
            if (OpenBehaviorTreeJsonAtPath(sourcePath))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(compiledAsset.SourceGuid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(compiledAsset.SourceGuid);
                if (!string.Equals(guidPath, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    return OpenBehaviorTreeJsonAtPath(guidPath);
                }
            }

            return false;
        }

        public static string ImportBehaviorTreeAtPath(string behaviorTreeAssetPath, string graphAssetPath, bool openAsset)
        {
            if (!BehaviorTreeCompiledAssetCompiler.IsBehaviorTreeJsonPath(behaviorTreeAssetPath))
            {
                throw new ArgumentException("Expected a .btree.json or .btree asset path.", "behaviorTreeAssetPath");
            }

            BehaviorTreeSource source = BehaviorTreeSource.FromJson(File.ReadAllText(behaviorTreeAssetPath));
            string graphPath = string.IsNullOrEmpty(graphAssetPath) ? GetDefaultGraphPath(behaviorTreeAssetPath) : graphAssetPath;
            string directory = Path.GetDirectoryName(graphPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            BehaviorTreeVisualGraph graph = GraphDatabase.CreateGraph<BehaviorTreeVisualGraph>(graphPath);
            graph.SourceBehaviorTreeAssetPath = behaviorTreeAssetPath;
            graph.SchemaVersion = source.SchemaVersion;
            graph.BehaviorTreeName = source.Name;
            graph.Category = source.Category;
            graph.Description = source.Description;
            graph.RootNodeId = source.Root;
            graph.Blackboard = ConvertBlackboard(source.Blackboard);
            graph.Nodes = ConvertNodes(source.Nodes);
            graph.Decorators = ConvertDecorators(source.Decorators);
            graph.Services = ConvertServices(source.Services);

            BehaviorTreeGraphToolkitBlackboardSync.SyncBlackboardToGraph(graph);
            CreateVisualGraphNodes(graph, graph.Nodes, graph.Decorators);
            BehaviorTreeGraphToolkitReflection.MarkDirty(graph);
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.ImportAsset(graphPath);

            if (openAsset && !Application.isBatchMode)
            {
                UnityEngine.Object graphAsset = AssetDatabase.LoadMainAssetAtPath(graphPath);
                if (graphAsset != null)
                {
                    AssetDatabase.OpenAsset(graphAsset);
                }
            }

            return graphPath;
        }

        private static bool OpenBehaviorTreeJsonAtPath(string assetPath)
        {
            if (!BehaviorTreeCompiledAssetCompiler.IsBehaviorTreeJsonPath(assetPath))
            {
                return false;
            }

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null)
            {
                return false;
            }

            try
            {
                ImportBehaviorTreeAtPath(assetPath, true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[BehaviorTree] Failed to open visual graph for '" + assetPath + "': " + ex.Message, asset);
                return false;
            }
        }

        public static string ExportGraphAtPath(string graphAssetPath, string outputBehaviorTreePath)
        {
            if (!IsBehaviorTreeGraphAssetPath(graphAssetPath))
            {
                throw new ArgumentException("Expected a ." + BehaviorTreeVisualGraph.AssetExtension + " asset path.", "graphAssetPath");
            }

            BehaviorTreeVisualGraph graph = GraphDatabase.LoadGraph<BehaviorTreeVisualGraph>(graphAssetPath);
            if (graph == null)
            {
                throw new InvalidOperationException("Unable to load behavior tree visual graph at " + graphAssetPath);
            }

            if (string.IsNullOrEmpty(outputBehaviorTreePath))
            {
                outputBehaviorTreePath = string.IsNullOrEmpty(graph.SourceBehaviorTreeAssetPath)
                    ? GetDefaultBehaviorTreeJsonPath(graphAssetPath)
                    : graph.SourceBehaviorTreeAssetPath;
            }

            BehaviorTreeSource source = ToBehaviorTreeSource(graph);
            File.WriteAllText(outputBehaviorTreePath, source.ToJson());
            AssetDatabase.ImportAsset(outputBehaviorTreePath);

            BehaviorTreeCompiledAsset compiledAsset;
            if (!BehaviorTreeCompiledAssetCompiler.CompileBehaviorTreeAtPath(outputBehaviorTreePath, false, out compiledAsset))
            {
                throw new InvalidOperationException("Exported behavior tree JSON could not be compiled: " + outputBehaviorTreePath);
            }

            return outputBehaviorTreePath;
        }

        public static BehaviorTreeSource ToBehaviorTreeSource(BehaviorTreeVisualGraph graph)
        {
            BehaviorTreeSource source = new BehaviorTreeSource();
            source.SchemaVersion = string.IsNullOrEmpty(graph.SchemaVersion) ? "0.1" : graph.SchemaVersion;
            source.Name = graph.BehaviorTreeName;
            source.Category = graph.Category;
            source.Description = graph.Description;
            source.Root = graph.RootNodeId;

            graph.Blackboard = BehaviorTreeGraphToolkitBlackboardSync.ExtractBlackboard(graph);
            if (graph.Blackboard != null)
            {
                for (int i = 0; i < graph.Blackboard.Count; i++)
                {
                    BehaviorTreeVisualBlackboardKeyData visual = graph.Blackboard[i];
                    if (visual == null)
                    {
                        continue;
                    }

                    BehaviorTreeBlackboardKey key = new BehaviorTreeBlackboardKey();
                    key.Name = visual.Name;
                    key.Type = visual.Type;
                    key.DefaultValue = visual.HasDefaultValue ? DeserializeJsonValue(visual.DefaultValueJson) : null;
                    key.Exposed = visual.Exposed;
                    key.Persistent = visual.Persistent;
                    key.Description = visual.Description;
                    source.Blackboard.Add(key);
                }
            }

            List<BehaviorTreeVisualNodeData> exportedNodes = ExtractVisualNodes(graph);
            if (exportedNodes.Count > 0)
            {
                graph.Nodes = exportedNodes;
            }

            List<BehaviorTreeVisualDecoratorData> exportedDecorators = ExtractVisualDecorators(graph);
            if (exportedDecorators.Count > 0)
            {
                graph.Decorators = exportedDecorators;
            }

            if (graph.Nodes != null)
            {
                for (int i = 0; i < graph.Nodes.Count; i++)
                {
                    BehaviorTreeVisualNodeData visual = graph.Nodes[i];
                    if (visual == null)
                    {
                        continue;
                    }

                    BehaviorTreeNodeSource node = new BehaviorTreeNodeSource();
                    node.Id = visual.Id;
                    node.TypeId = visual.TypeId;
                    node.X = visual.X;
                    node.Y = visual.Y;
                    AddRange(node.Children, visual.Children);
                    AddRange(node.Decorators, visual.Decorators);
                    AddRange(node.Services, visual.Services);
                    AddInputBindings(node.Inputs, visual.InputBindings);
                    CopyProperties(visual.PropertiesJson, node.Properties);
                    source.Nodes.Add(node);
                }
            }

            if (graph.Decorators != null)
            {
                for (int i = 0; i < graph.Decorators.Count; i++)
                {
                    BehaviorTreeVisualDecoratorData visual = graph.Decorators[i];
                    if (visual == null)
                    {
                        continue;
                    }

                    BehaviorTreeDecoratorSource decorator = new BehaviorTreeDecoratorSource();
                    decorator.Id = visual.Id;
                    decorator.TypeId = visual.TypeId;
                    AddInputBindings(decorator.Inputs, visual.InputBindings);
                    CopyProperties(visual.PropertiesJson, decorator.Properties);
                    RemoveMigratedDecoratorProperties(visual, decorator.Properties);
                    source.Decorators.Add(decorator);
                }
            }

            if (graph.Services != null)
            {
                for (int i = 0; i < graph.Services.Count; i++)
                {
                    BehaviorTreeVisualServiceData visual = graph.Services[i];
                    if (visual == null)
                    {
                        continue;
                    }

                    BehaviorTreeServiceSource service = new BehaviorTreeServiceSource();
                    service.Id = visual.Id;
                    service.TypeId = visual.TypeId;
                    service.Interval = visual.Interval;
                    service.RandomDeviation = visual.RandomDeviation;
                    CopyProperties(visual.PropertiesJson, service.Properties);
                    source.Services.Add(service);
                }
            }

            return source;
        }

        public static bool IsBehaviorTreeGraphAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.EndsWith("." + BehaviorTreeVisualGraph.AssetExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetDefaultGraphPath(string behaviorTreeAssetPath)
        {
            if (behaviorTreeAssetPath.EndsWith(BehaviorTreeJsonAssetSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return behaviorTreeAssetPath.Substring(0, behaviorTreeAssetPath.Length - BehaviorTreeJsonAssetSuffix.Length) + "." + BehaviorTreeVisualGraph.AssetExtension;
            }

            if (behaviorTreeAssetPath.EndsWith(BehaviorTreeAssetSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return behaviorTreeAssetPath.Substring(0, behaviorTreeAssetPath.Length - BehaviorTreeAssetSuffix.Length) + "." + BehaviorTreeVisualGraph.AssetExtension;
            }

            return Path.ChangeExtension(behaviorTreeAssetPath, "." + BehaviorTreeVisualGraph.AssetExtension);
        }

        private static string GetAssetPathFromOpenAssetId(int instanceId)
        {
#if UNITY_6000_3_OR_NEWER
            string entityPath = AssetDatabase.GetAssetPath((EntityId)instanceId);
            if (!string.IsNullOrEmpty(entityPath))
            {
                return entityPath;
            }
#endif

#pragma warning disable 0618
            UnityEngine.Object asset = EditorUtility.InstanceIDToObject(instanceId);
#pragma warning restore 0618
            return asset == null ? null : AssetDatabase.GetAssetPath(asset);
        }

        public static string GetDefaultBehaviorTreeJsonPath(string graphAssetPath)
        {
            if (graphAssetPath.EndsWith("." + BehaviorTreeVisualGraph.AssetExtension, StringComparison.OrdinalIgnoreCase))
            {
                return graphAssetPath.Substring(0, graphAssetPath.Length - BehaviorTreeVisualGraph.AssetExtension.Length - 1) + ".btree.json";
            }

            return Path.ChangeExtension(graphAssetPath, ".btree.json");
        }

        private static List<BehaviorTreeVisualBlackboardKeyData> ConvertBlackboard(List<BehaviorTreeBlackboardKey> blackboard)
        {
            List<BehaviorTreeVisualBlackboardKeyData> result = new List<BehaviorTreeVisualBlackboardKeyData>();
            if (blackboard == null)
            {
                return result;
            }

            for (int i = 0; i < blackboard.Count; i++)
            {
                BehaviorTreeBlackboardKey key = blackboard[i];
                if (key == null)
                {
                    continue;
                }

                object defaultValue = BehaviorTreeValueUtility.NormalizeValueForJson(key.DefaultValue, key.Type);
                result.Add(new BehaviorTreeVisualBlackboardKeyData
                {
                    Name = key.Name,
                    Type = key.Type,
                    HasDefaultValue = defaultValue != null,
                    DefaultValueJson = defaultValue == null ? string.Empty : BlueprintJson.Serialize(defaultValue, false),
                    Exposed = key.Exposed,
                    Persistent = key.Persistent,
                    Description = key.Description
                });
            }

            return result;
        }

        private static List<BehaviorTreeVisualNodeData> ConvertNodes(List<BehaviorTreeNodeSource> nodes)
        {
            List<BehaviorTreeVisualNodeData> result = new List<BehaviorTreeVisualNodeData>();
            if (nodes == null)
            {
                return result;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                BehaviorTreeNodeSource node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                BehaviorTreeVisualNodeData visual = new BehaviorTreeVisualNodeData();
                visual.Id = node.Id;
                visual.TypeId = node.TypeId;
                visual.X = node.X;
                visual.Y = node.Y;
                visual.Children.AddRange(node.Children);
                visual.Decorators.AddRange(node.Decorators);
                visual.Services.AddRange(node.Services);
                visual.InputBindings.AddRange(ConvertInputBindings(node.Inputs));
                AddLegacyInputBindings(visual, node.Properties);
                visual.PropertiesJson = BlueprintJson.Serialize(node.Properties, false);
                result.Add(visual);
            }

            return result;
        }

        private static List<BehaviorTreeVisualDecoratorData> ConvertDecorators(List<BehaviorTreeDecoratorSource> decorators)
        {
            List<BehaviorTreeVisualDecoratorData> result = new List<BehaviorTreeVisualDecoratorData>();
            if (decorators == null)
            {
                return result;
            }

            for (int i = 0; i < decorators.Count; i++)
            {
                BehaviorTreeDecoratorSource decorator = decorators[i];
                if (decorator == null)
                {
                    continue;
                }

                BehaviorTreeVisualDecoratorData visual = new BehaviorTreeVisualDecoratorData();
                visual.Id = decorator.Id;
                visual.TypeId = decorator.TypeId;
                visual.InputBindings.AddRange(ConvertInputBindings(decorator.Inputs));
                AddLegacyDecoratorInputBindings(visual, decorator.Properties);
                Dictionary<string, object> properties = new Dictionary<string, object>(decorator.Properties, StringComparer.Ordinal);
                RemoveMigratedDecoratorProperties(visual, properties);
                visual.PropertiesJson = BlueprintJson.Serialize(properties, false);
                result.Add(visual);
            }

            return result;
        }

        private static List<BehaviorTreeVisualServiceData> ConvertServices(List<BehaviorTreeServiceSource> services)
        {
            List<BehaviorTreeVisualServiceData> result = new List<BehaviorTreeVisualServiceData>();
            if (services == null)
            {
                return result;
            }

            for (int i = 0; i < services.Count; i++)
            {
                BehaviorTreeServiceSource service = services[i];
                if (service == null)
                {
                    continue;
                }

                result.Add(new BehaviorTreeVisualServiceData
                {
                    Id = service.Id,
                    TypeId = service.TypeId,
                    Interval = service.Interval,
                    RandomDeviation = service.RandomDeviation,
                    PropertiesJson = BlueprintJson.Serialize(service.Properties, false)
                });
            }

            return result;
        }

        private static void CreateVisualGraphNodes(
            BehaviorTreeVisualGraph graph,
            List<BehaviorTreeVisualNodeData> nodes,
            List<BehaviorTreeVisualDecoratorData> decorators)
        {
            if (graph == null || nodes == null)
            {
                return;
            }

            Dictionary<string, BehaviorTreeVisualNode> nodesById = new Dictionary<string, BehaviorTreeVisualNode>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
            {
                BehaviorTreeVisualNodeData nodeData = nodes[i];
                if (nodeData == null)
                {
                    continue;
                }

                BehaviorTreeVisualNode visualNode = CreateVisualNode(nodeData);
                BehaviorTreeGraphToolkitReflection.CreateNode(graph, visualNode, new Vector2(nodeData.X, nodeData.Y));
                if (!string.IsNullOrEmpty(nodeData.Id))
                {
                    nodesById[nodeData.Id] = visualNode;
                }
            }

            Dictionary<string, BehaviorTreeVisualDecoratorNode> decoratorsById = CreateVisualDecoratorNodes(graph, decorators, nodes);

            for (int i = 0; i < nodes.Count; i++)
            {
                BehaviorTreeVisualNodeData nodeData = nodes[i];
                BehaviorTreeVisualNode parentNode;
                if (nodeData == null || string.IsNullOrEmpty(nodeData.Id) || !nodesById.TryGetValue(nodeData.Id, out parentNode))
                {
                    continue;
                }

                if (nodeData.Children == null)
                {
                    continue;
                }

                for (int c = 0; c < nodeData.Children.Count; c++)
                {
                    string childId = nodeData.Children[c];
                    BehaviorTreeVisualNode childNode;
                    if (string.IsNullOrEmpty(childId) || !nodesById.TryGetValue(childId, out childNode))
                    {
                        continue;
                    }

                    IPort outputPort = SafeGetOutputPort(parentNode, BehaviorTreeVisualNode.GetChildPortName(c));
                    IPort inputPort = SafeGetInputPort(childNode, BehaviorTreeVisualNode.ParentPortName);
                    if (outputPort != null && inputPort != null)
                    {
                        BehaviorTreeGraphToolkitReflection.CreateWire(graph, inputPort, outputPort);
                    }
                }
            }

            CreateDecoratorWires(graph, nodes, nodesById, decoratorsById);
            CreateInputBindingWires(graph, nodes, nodesById);
            CreateDecoratorInputBindingWires(graph, decorators, decoratorsById);
        }

        private static BehaviorTreeVisualNode CreateVisualNode(BehaviorTreeVisualNodeData data)
        {
            BehaviorTreeVisualNode node = BehaviorTreeVisualNodeMetadata.Create(data.TypeId);
            node.Id = data.Id;
            node.TypeId = data.TypeId;
            node.Title = BehaviorTreeVisualNodeMetadata.CreateTitle(data.TypeId);
            node.PropertiesJson = string.IsNullOrEmpty(data.PropertiesJson) ? "{}" : data.PropertiesJson;

            if (data.Children != null)
            {
                node.Children.AddRange(data.Children);
            }

            if (data.Decorators != null)
            {
                node.Decorators.AddRange(data.Decorators);
            }

            if (data.Services != null)
            {
                node.Services.AddRange(data.Services);
            }

            if (data.InputBindings != null)
            {
                node.InputBindings.AddRange(CloneInputBindings(data.InputBindings));
            }

            return node;
        }

        private static Dictionary<string, BehaviorTreeVisualDecoratorNode> CreateVisualDecoratorNodes(
            BehaviorTreeVisualGraph graph,
            List<BehaviorTreeVisualDecoratorData> decorators,
            List<BehaviorTreeVisualNodeData> nodes)
        {
            Dictionary<string, BehaviorTreeVisualDecoratorNode> decoratorsById =
                new Dictionary<string, BehaviorTreeVisualDecoratorNode>(StringComparer.Ordinal);
            if (graph == null || decorators == null)
            {
                return decoratorsById;
            }

            for (int i = 0; i < decorators.Count; i++)
            {
                BehaviorTreeVisualDecoratorData decoratorData = decorators[i];
                if (decoratorData == null)
                {
                    continue;
                }

                BehaviorTreeVisualDecoratorNode decoratorNode = CreateVisualDecoratorNode(decoratorData);
                Vector2 position = GetDecoratorNodePosition(nodes, decoratorData.Id, i);
                BehaviorTreeGraphToolkitReflection.CreateNode(graph, decoratorNode, position);
                if (!string.IsNullOrEmpty(decoratorData.Id))
                {
                    decoratorsById[decoratorData.Id] = decoratorNode;
                }
            }

            return decoratorsById;
        }

        private static BehaviorTreeVisualDecoratorNode CreateVisualDecoratorNode(BehaviorTreeVisualDecoratorData data)
        {
            BehaviorTreeVisualDecoratorNode node = BehaviorTreeVisualNodeMetadata.CreateDecorator(data.TypeId);
            node.Id = data.Id;
            node.TypeId = data.TypeId;
            node.Title = BehaviorTreeVisualNodeMetadata.CreateTitle(data.TypeId);
            node.PropertiesJson = string.IsNullOrEmpty(data.PropertiesJson) ? "{}" : data.PropertiesJson;
            if (data.InputBindings != null)
            {
                node.InputBindings.AddRange(CloneInputBindings(data.InputBindings));
            }

            return node;
        }

        private static Vector2 GetDecoratorNodePosition(List<BehaviorTreeVisualNodeData> nodes, string decoratorId, int decoratorIndex)
        {
            if (nodes != null && !string.IsNullOrEmpty(decoratorId))
            {
                int attachedIndex = 0;
                for (int i = 0; i < nodes.Count; i++)
                {
                    BehaviorTreeVisualNodeData node = nodes[i];
                    if (node == null || node.Decorators == null)
                    {
                        continue;
                    }

                    for (int d = 0; d < node.Decorators.Count; d++)
                    {
                        if (node.Decorators[d] == decoratorId)
                        {
                            return new Vector2(node.X - 260f, node.Y - 90f + (attachedIndex + d) * 70f);
                        }
                    }

                    attachedIndex += node.Decorators.Count;
                }
            }

            return new Vector2(-260f, decoratorIndex * 90f);
        }

        private static List<BehaviorTreeVisualNodeData> ExtractVisualNodes(BehaviorTreeVisualGraph graph)
        {
            List<BehaviorTreeVisualNodeData> result = new List<BehaviorTreeVisualNodeData>();
            if (graph == null)
            {
                return result;
            }

            Dictionary<INode, string> nodeIds = new Dictionary<INode, string>();
            Dictionary<INode, string> decoratorIds = new Dictionary<INode, string>();
            foreach (INode node in graph.GetNodes())
            {
                BehaviorTreeVisualNode visualNode = node as BehaviorTreeVisualNode;
                if (visualNode != null)
                {
                    string nodeId = visualNode.ReadNodeId();
                    if (!string.IsNullOrEmpty(nodeId))
                    {
                        nodeIds[node] = nodeId;
                    }

                    continue;
                }

                BehaviorTreeVisualDecoratorNode visualDecorator = node as BehaviorTreeVisualDecoratorNode;
                if (visualDecorator != null)
                {
                    string decoratorId = visualDecorator.ReadDecoratorId();
                    if (!string.IsNullOrEmpty(decoratorId))
                    {
                        decoratorIds[node] = decoratorId;
                    }
                }
            }

            foreach (INode node in graph.GetNodes())
            {
                BehaviorTreeVisualNode visualNode = node as BehaviorTreeVisualNode;
                if (visualNode == null)
                {
                    continue;
                }

                BehaviorTreeVisualNodeData data = new BehaviorTreeVisualNodeData();
                data.Id = visualNode.ReadNodeId();
                data.TypeId = visualNode.ReadTypeId();
                data.PropertiesJson = MergeInlineInputValues(visualNode.ReadPropertiesJson(), visualNode.ReadInlineInputValues());

                Vector2 position;
                if (BehaviorTreeGraphToolkitReflection.TryGetNodePosition(visualNode, out position))
                {
                    data.X = position.x;
                    data.Y = position.y;
                }

                data.Decorators.AddRange(visualNode.ReadDecorators());
                AddUniqueRange(data.Decorators, ExtractConnectedDecorators(visualNode, decoratorIds));
                data.Services.AddRange(visualNode.ReadServices());
                data.InputBindings.AddRange(ExtractInputBindings(visualNode));
                if (data.InputBindings.Count == 0)
                {
                    data.InputBindings.AddRange(visualNode.ReadInputBindings());
                }

                Dictionary<string, object> extractedProperties = new Dictionary<string, object>(StringComparer.Ordinal);
                CopyProperties(data.PropertiesJson, extractedProperties);
                AddLegacyInputBindings(data, extractedProperties);

                List<string> connectedChildren = ExtractConnectedChildren(visualNode, nodeIds);
                if (connectedChildren.Count > 0)
                {
                    data.Children.AddRange(connectedChildren);
                }
                else if (visualNode.Children != null)
                {
                    AddNonEmptyRange(data.Children, visualNode.Children);
                }

                result.Add(data);
            }

            return result;
        }

        private static List<BehaviorTreeVisualDecoratorData> ExtractVisualDecorators(BehaviorTreeVisualGraph graph)
        {
            List<BehaviorTreeVisualDecoratorData> result = new List<BehaviorTreeVisualDecoratorData>();
            if (graph == null)
            {
                return result;
            }

            foreach (INode node in graph.GetNodes())
            {
                BehaviorTreeVisualDecoratorNode visualDecorator = node as BehaviorTreeVisualDecoratorNode;
                if (visualDecorator == null)
                {
                    continue;
                }

                BehaviorTreeVisualDecoratorData data = new BehaviorTreeVisualDecoratorData();
                data.Id = visualDecorator.ReadDecoratorId();
                data.TypeId = visualDecorator.ReadTypeId();
                data.PropertiesJson = MergeInlineInputValues(
                    visualDecorator.ReadPropertiesJson(),
                    visualDecorator.ReadInlineInputValues());
                data.InputBindings.AddRange(ExtractInputBindings(visualDecorator));
                if (data.InputBindings.Count == 0)
                {
                    data.InputBindings.AddRange(visualDecorator.ReadInputBindings());
                }

                Dictionary<string, object> extractedProperties = new Dictionary<string, object>(StringComparer.Ordinal);
                CopyProperties(data.PropertiesJson, extractedProperties);
                AddLegacyDecoratorInputBindings(data, extractedProperties);
                RemoveMigratedDecoratorProperties(data, extractedProperties);
                data.PropertiesJson = BlueprintJson.Serialize(extractedProperties, false);
                result.Add(data);
            }

            return result;
        }

        private static void CreateDecoratorWires(
            BehaviorTreeVisualGraph graph,
            List<BehaviorTreeVisualNodeData> nodes,
            Dictionary<string, BehaviorTreeVisualNode> nodesById,
            Dictionary<string, BehaviorTreeVisualDecoratorNode> decoratorsById)
        {
            if (graph == null || nodes == null || nodesById == null || decoratorsById == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                BehaviorTreeVisualNodeData nodeData = nodes[i];
                BehaviorTreeVisualNode targetNode;
                if (nodeData == null ||
                    nodeData.Decorators == null ||
                    string.IsNullOrEmpty(nodeData.Id) ||
                    !nodesById.TryGetValue(nodeData.Id, out targetNode))
                {
                    continue;
                }

                IPort inputPort = SafeGetInputPort(targetNode, BehaviorTreeVisualNode.DecoratorPortName);
                if (inputPort == null)
                {
                    continue;
                }

                for (int d = 0; d < nodeData.Decorators.Count; d++)
                {
                    BehaviorTreeVisualDecoratorNode decoratorNode;
                    if (string.IsNullOrEmpty(nodeData.Decorators[d]) ||
                        !decoratorsById.TryGetValue(nodeData.Decorators[d], out decoratorNode))
                    {
                        continue;
                    }

                    IPort outputPort = SafeGetOutputPort(decoratorNode, BehaviorTreeVisualDecoratorNode.DecoratorOutputPortName);
                    if (outputPort != null)
                    {
                        BehaviorTreeGraphToolkitReflection.CreateWire(graph, inputPort, outputPort);
                    }
                }
            }
        }

        private static void CreateInputBindingWires(
            BehaviorTreeVisualGraph graph,
            List<BehaviorTreeVisualNodeData> nodes,
            Dictionary<string, BehaviorTreeVisualNode> nodesById)
        {
            if (graph == null || nodes == null || nodesById == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                BehaviorTreeVisualNodeData nodeData = nodes[i];
                BehaviorTreeVisualNode targetNode;
                if (nodeData == null ||
                    nodeData.InputBindings == null ||
                    string.IsNullOrEmpty(nodeData.Id) ||
                    !nodesById.TryGetValue(nodeData.Id, out targetNode))
                {
                    continue;
                }

                for (int b = 0; b < nodeData.InputBindings.Count; b++)
                {
                    BehaviorTreeVisualInputBindingData binding = nodeData.InputBindings[b];
                    if (binding == null || string.IsNullOrEmpty(binding.InputId) || string.IsNullOrEmpty(binding.BlackboardKey))
                    {
                        continue;
                    }

                    IVariable variable;
                    if (!TryFindBlackboardVariable(graph, binding.BlackboardKey, out variable))
                    {
                        continue;
                    }

                    IPort inputPort = SafeGetInputPort(targetNode, binding.InputId);
                    if (inputPort == null)
                    {
                        continue;
                    }

                    Vector2 position = new Vector2(nodeData.X - 220f, nodeData.Y + b * 80f);
                    INode variableNode = BehaviorTreeGraphToolkitReflection.CreateBlackboardVariableNode(graph, variable, position);
                    IPort outputPort = SafeGetOutputPort(variableNode, "value");
                    if (outputPort != null)
                    {
                        BehaviorTreeGraphToolkitReflection.CreateWire(graph, inputPort, outputPort);
                    }
                }
            }
        }

        private static void CreateDecoratorInputBindingWires(
            BehaviorTreeVisualGraph graph,
            List<BehaviorTreeVisualDecoratorData> decorators,
            Dictionary<string, BehaviorTreeVisualDecoratorNode> decoratorsById)
        {
            if (graph == null || decorators == null || decoratorsById == null)
            {
                return;
            }

            for (int i = 0; i < decorators.Count; i++)
            {
                BehaviorTreeVisualDecoratorData decoratorData = decorators[i];
                BehaviorTreeVisualDecoratorNode targetNode;
                if (decoratorData == null ||
                    decoratorData.InputBindings == null ||
                    string.IsNullOrEmpty(decoratorData.Id) ||
                    !decoratorsById.TryGetValue(decoratorData.Id, out targetNode))
                {
                    continue;
                }

                Vector2 nodePosition;
                if (!BehaviorTreeGraphToolkitReflection.TryGetNodePosition(targetNode, out nodePosition))
                {
                    nodePosition = new Vector2(-260f, i * 90f);
                }

                for (int b = 0; b < decoratorData.InputBindings.Count; b++)
                {
                    BehaviorTreeVisualInputBindingData binding = decoratorData.InputBindings[b];
                    if (binding == null || string.IsNullOrEmpty(binding.InputId) || string.IsNullOrEmpty(binding.BlackboardKey))
                    {
                        continue;
                    }

                    IVariable variable;
                    if (!TryFindBlackboardVariable(graph, binding.BlackboardKey, out variable))
                    {
                        continue;
                    }

                    IPort inputPort = SafeGetInputPort(targetNode, binding.InputId);
                    if (inputPort == null)
                    {
                        continue;
                    }

                    Vector2 position = new Vector2(nodePosition.x - 220f, nodePosition.y + b * 80f);
                    INode variableNode = BehaviorTreeGraphToolkitReflection.CreateBlackboardVariableNode(graph, variable, position);
                    IPort outputPort = SafeGetOutputPort(variableNode, "value");
                    if (outputPort != null)
                    {
                        BehaviorTreeGraphToolkitReflection.CreateWire(graph, inputPort, outputPort);
                    }
                }
            }
        }

        private static List<string> ExtractConnectedChildren(BehaviorTreeVisualNode visualNode, Dictionary<INode, string> nodeIds)
        {
            List<string> result = new List<string>();
            if (visualNode == null || nodeIds == null)
            {
                return result;
            }

            int childCount = visualNode.ReadChildCount();
            for (int i = 0; i < childCount; i++)
            {
                IPort outputPort = SafeGetOutputPort(visualNode, BehaviorTreeVisualNode.GetChildPortName(i));
                if (outputPort == null)
                {
                    continue;
                }

                List<IPort> connectedPorts = new List<IPort>();
                outputPort.GetConnectedPorts(connectedPorts);
                for (int c = 0; c < connectedPorts.Count; c++)
                {
                    IPort connectedPort = connectedPorts[c];
                    if (connectedPort == null || connectedPort.direction != PortDirection.Input)
                    {
                        continue;
                    }

                    INode childNode = connectedPort.GetNode();
                    string childId;
                    if (childNode != null && nodeIds.TryGetValue(childNode, out childId) && !string.IsNullOrEmpty(childId))
                    {
                        result.Add(childId);
                        break;
                    }
                }
            }

            return result;
        }

        private static List<string> ExtractConnectedDecorators(BehaviorTreeVisualNode visualNode, Dictionary<INode, string> decoratorIds)
        {
            List<string> result = new List<string>();
            if (visualNode == null || decoratorIds == null)
            {
                return result;
            }

            IPort inputPort = SafeGetInputPort(visualNode, BehaviorTreeVisualNode.DecoratorPortName);
            if (inputPort == null)
            {
                return result;
            }

            List<IPort> connectedPorts = new List<IPort>();
            inputPort.GetConnectedPorts(connectedPorts);
            for (int i = 0; i < connectedPorts.Count; i++)
            {
                IPort connectedPort = connectedPorts[i];
                if (connectedPort == null || connectedPort.direction != PortDirection.Output)
                {
                    continue;
                }

                INode decoratorNode = connectedPort.GetNode();
                string decoratorId;
                if (decoratorNode != null &&
                    decoratorIds.TryGetValue(decoratorNode, out decoratorId) &&
                    !string.IsNullOrEmpty(decoratorId) &&
                    !result.Contains(decoratorId))
                {
                    result.Add(decoratorId);
                }
            }

            return result;
        }

        private static List<BehaviorTreeVisualInputBindingData> ExtractInputBindings(BehaviorTreeVisualNode visualNode)
        {
            List<BehaviorTreeVisualInputBindingData> result = new List<BehaviorTreeVisualInputBindingData>();
            if (visualNode == null || visualNode.Inputs == null)
            {
                return result;
            }

            HashSet<string> taskInputIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < visualNode.Inputs.Count; i++)
            {
                BehaviorTreeVisualInputPortData input = visualNode.Inputs[i];
                if (input != null && !string.IsNullOrEmpty(input.Id))
                {
                    taskInputIds.Add(input.Id);
                }
            }

            foreach (IPort inputPort in visualNode.GetInputPorts())
            {
                if (inputPort == null || !taskInputIds.Contains(inputPort.name))
                {
                    continue;
                }

                List<IPort> connectedPorts = new List<IPort>();
                inputPort.GetConnectedPorts(connectedPorts);
                for (int c = 0; c < connectedPorts.Count; c++)
                {
                    IPort connectedPort = connectedPorts[c];
                    if (connectedPort == null || connectedPort.direction != PortDirection.Output)
                    {
                        continue;
                    }

                    IVariableNode variableNode = connectedPort.GetNode() as IVariableNode;
                    if (variableNode == null || variableNode.variable == null || string.IsNullOrEmpty(variableNode.variable.name))
                    {
                        continue;
                    }

                    result.Add(new BehaviorTreeVisualInputBindingData
                    {
                        InputId = inputPort.name,
                        BlackboardKey = variableNode.variable.name
                    });
                    break;
                }
            }

            return result;
        }

        private static List<BehaviorTreeVisualInputBindingData> ExtractInputBindings(BehaviorTreeVisualDecoratorNode visualDecorator)
        {
            List<BehaviorTreeVisualInputBindingData> result = new List<BehaviorTreeVisualInputBindingData>();
            if (visualDecorator == null || visualDecorator.Inputs == null)
            {
                return result;
            }

            HashSet<string> decoratorInputIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < visualDecorator.Inputs.Count; i++)
            {
                BehaviorTreeVisualInputPortData input = visualDecorator.Inputs[i];
                if (input != null && !string.IsNullOrEmpty(input.Id))
                {
                    decoratorInputIds.Add(input.Id);
                }
            }

            foreach (IPort inputPort in visualDecorator.GetInputPorts())
            {
                if (inputPort == null || !decoratorInputIds.Contains(inputPort.name))
                {
                    continue;
                }

                List<IPort> connectedPorts = new List<IPort>();
                inputPort.GetConnectedPorts(connectedPorts);
                for (int c = 0; c < connectedPorts.Count; c++)
                {
                    IPort connectedPort = connectedPorts[c];
                    if (connectedPort == null || connectedPort.direction != PortDirection.Output)
                    {
                        continue;
                    }

                    IVariableNode variableNode = connectedPort.GetNode() as IVariableNode;
                    if (variableNode == null || variableNode.variable == null || string.IsNullOrEmpty(variableNode.variable.name))
                    {
                        continue;
                    }

                    result.Add(new BehaviorTreeVisualInputBindingData
                    {
                        InputId = inputPort.name,
                        BlackboardKey = variableNode.variable.name
                    });
                    break;
                }
            }

            return result;
        }

        private static IPort SafeGetInputPort(INode node, string portName)
        {
            if (node == null || string.IsNullOrEmpty(portName))
            {
                return null;
            }

            IVariableNode variableNode = node as IVariableNode;
            if (variableNode != null)
            {
                return FindFirstPort(variableNode.GetInputPorts());
            }

            try
            {
                return node.GetInputPortByName(portName);
            }
            catch
            {
                return null;
            }
        }

        private static IPort SafeGetOutputPort(INode node, string portName)
        {
            if (node == null || string.IsNullOrEmpty(portName))
            {
                return null;
            }

            IVariableNode variableNode = node as IVariableNode;
            if (variableNode != null)
            {
                return FindFirstPort(variableNode.GetOutputPorts());
            }

            try
            {
                return node.GetOutputPortByName(portName);
            }
            catch
            {
                return null;
            }
        }

        private static IPort FindFirstPort(IEnumerable<IPort> ports)
        {
            if (ports == null)
            {
                return null;
            }

            foreach (IPort port in ports)
            {
                if (port != null)
                {
                    return port;
                }
            }

            return null;
        }

        private static void AddRange(List<string> target, List<string> source)
        {
            if (target != null && source != null)
            {
                target.AddRange(source);
            }
        }

        private static void AddNonEmptyRange(List<string> target, List<string> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (!string.IsNullOrEmpty(source[i]))
                {
                    target.Add(source[i]);
                }
            }
        }

        private static void AddUniqueRange(List<string> target, List<string> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                string value = source[i];
                if (!string.IsNullOrEmpty(value) && !target.Contains(value))
                {
                    target.Add(value);
                }
            }
        }

        private static void AddInputBindings(Dictionary<string, string> target, List<BehaviorTreeVisualInputBindingData> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                BehaviorTreeVisualInputBindingData binding = source[i];
                if (binding == null || string.IsNullOrEmpty(binding.InputId) || string.IsNullOrEmpty(binding.BlackboardKey))
                {
                    continue;
                }

                target[binding.InputId] = binding.BlackboardKey;
            }
        }

        private static List<BehaviorTreeVisualInputBindingData> ConvertInputBindings(Dictionary<string, string> inputs)
        {
            List<BehaviorTreeVisualInputBindingData> result = new List<BehaviorTreeVisualInputBindingData>();
            if (inputs == null)
            {
                return result;
            }

            List<string> keys = new List<string>(inputs.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string inputId = keys[i];
                string blackboardKey = inputs[inputId];
                if (!string.IsNullOrEmpty(inputId) && !string.IsNullOrEmpty(blackboardKey))
                {
                    result.Add(new BehaviorTreeVisualInputBindingData
                    {
                        InputId = inputId,
                        BlackboardKey = blackboardKey
                    });
                }
            }

            return result;
        }

        private static List<BehaviorTreeVisualInputBindingData> CloneInputBindings(List<BehaviorTreeVisualInputBindingData> source)
        {
            List<BehaviorTreeVisualInputBindingData> result = new List<BehaviorTreeVisualInputBindingData>();
            if (source == null)
            {
                return result;
            }

            for (int i = 0; i < source.Count; i++)
            {
                BehaviorTreeVisualInputBindingData binding = source[i];
                if (binding == null)
                {
                    continue;
                }

                result.Add(new BehaviorTreeVisualInputBindingData
                {
                    InputId = binding.InputId,
                    BlackboardKey = binding.BlackboardKey
                });
            }

            return result;
        }

        private static void AddLegacyInputBindings(BehaviorTreeVisualNodeData visual, Dictionary<string, object> properties)
        {
            if (visual == null || properties == null)
            {
                return;
            }

            string[] candidateInputs =
            {
                "key",
                "value",
                "target",
                "complete",
                "failure"
            };

            for (int i = 0; i < candidateInputs.Length; i++)
            {
                string inputId = candidateInputs[i];
                if (HasInputBinding(visual.InputBindings, inputId))
                {
                    continue;
                }

                string legacyProperty = BehaviorTreeVisualNodeMetadata.GetLegacyInputBindingProperty(visual.TypeId, inputId);
                object value;
                if (string.IsNullOrEmpty(legacyProperty) ||
                    !properties.TryGetValue(legacyProperty, out value) ||
                    value == null)
                {
                    continue;
                }

                string blackboardKey = Convert.ToString(value);
                if (!string.IsNullOrEmpty(blackboardKey))
                {
                    visual.InputBindings.Add(new BehaviorTreeVisualInputBindingData
                    {
                        InputId = inputId,
                        BlackboardKey = blackboardKey
                    });
                }
            }
        }

        private static void AddLegacyDecoratorInputBindings(BehaviorTreeVisualDecoratorData visual, Dictionary<string, object> properties)
        {
            if (visual == null || properties == null)
            {
                return;
            }

            switch (visual.TypeId)
            {
                case BehaviorTreeVisualNodeMetadata.BlackboardCondition:
                case BehaviorTreeVisualNodeMetadata.CompareBool:
                case BehaviorTreeVisualNodeMetadata.ObjectIsSet:
                    AddLegacyDecoratorInputBinding(visual, properties, "key", "value");
                    break;
                case BehaviorTreeVisualNodeMetadata.CompareFloat:
                    AddLegacyDecoratorInputBinding(visual, properties, "leftKey", "left");
                    AddLegacyDecoratorInputBinding(visual, properties, "rightKey", "right");
                    break;
                case BehaviorTreeVisualNodeMetadata.DistanceLessThan:
                    AddLegacyDecoratorInputBinding(visual, properties, "distanceKey", "distance");
                    AddLegacyDecoratorInputBinding(visual, properties, "sourceKey", "source");
                    AddLegacyDecoratorInputBinding(visual, properties, "targetKey", "target");
                    break;
            }
        }

        private static void AddLegacyDecoratorInputBinding(
            BehaviorTreeVisualDecoratorData visual,
            Dictionary<string, object> properties,
            string legacyProperty,
            string inputId)
        {
            if (visual.InputBindings == null)
            {
                visual.InputBindings = new List<BehaviorTreeVisualInputBindingData>();
            }

            if (HasInputBinding(visual.InputBindings, inputId))
            {
                return;
            }

            object value;
            if (string.IsNullOrEmpty(legacyProperty) ||
                !properties.TryGetValue(legacyProperty, out value) ||
                value == null)
            {
                return;
            }

            string blackboardKey = Convert.ToString(value);
            if (!string.IsNullOrEmpty(blackboardKey))
            {
                visual.InputBindings.Add(new BehaviorTreeVisualInputBindingData
                {
                    InputId = inputId,
                    BlackboardKey = blackboardKey
                });
            }
        }

        private static void RemoveMigratedDecoratorProperties(
            BehaviorTreeVisualDecoratorData visual,
            Dictionary<string, object> properties)
        {
            if (visual == null || properties == null)
            {
                return;
            }

            switch (visual.TypeId)
            {
                case BehaviorTreeVisualNodeMetadata.BlackboardCondition:
                case BehaviorTreeVisualNodeMetadata.CompareBool:
                case BehaviorTreeVisualNodeMetadata.ObjectIsSet:
                    RemovePropertyWhenBound(visual, properties, "value", "key");
                    break;
                case BehaviorTreeVisualNodeMetadata.CompareFloat:
                    RemovePropertyWhenBound(visual, properties, "left", "leftKey");
                    RemovePropertyWhenBound(visual, properties, "right", "rightKey");
                    break;
                case BehaviorTreeVisualNodeMetadata.DistanceLessThan:
                    RemovePropertyWhenBound(visual, properties, "distance", "distanceKey");
                    RemovePropertyWhenBound(visual, properties, "source", "sourceKey");
                    RemovePropertyWhenBound(visual, properties, "target", "targetKey");
                    break;
            }
        }

        private static void RemovePropertyWhenBound(
            BehaviorTreeVisualDecoratorData visual,
            Dictionary<string, object> properties,
            string inputId,
            string propertyId)
        {
            if (HasInputBinding(visual.InputBindings, inputId))
            {
                properties.Remove(propertyId);
            }
        }

        private static bool HasInputBinding(List<BehaviorTreeVisualInputBindingData> bindings, string inputId)
        {
            if (bindings == null)
            {
                return false;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                BehaviorTreeVisualInputBindingData binding = bindings[i];
                if (binding != null && binding.InputId == inputId && !string.IsNullOrEmpty(binding.BlackboardKey))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindBlackboardVariable(BehaviorTreeVisualGraph graph, string variableName, out IVariable variable)
        {
            variable = null;
            if (graph == null || string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            foreach (IVariable candidate in graph.GetVariables())
            {
                if (candidate != null && candidate.name == variableName)
                {
                    variable = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string MergeInlineInputValues(string propertiesJson, Dictionary<string, object> inlineValues)
        {
            if (inlineValues == null || inlineValues.Count == 0)
            {
                return propertiesJson;
            }

            Dictionary<string, object> properties = new Dictionary<string, object>(StringComparer.Ordinal);
            CopyProperties(propertiesJson, properties);
            foreach (KeyValuePair<string, object> pair in inlineValues)
            {
                properties[pair.Key] = pair.Value;
            }

            return BlueprintJson.Serialize(properties, false);
        }

        private static void CopyProperties(string json, Dictionary<string, object> target)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                Dictionary<string, object> properties = BlueprintJson.DeserializeObject(json);
                foreach (KeyValuePair<string, object> pair in properties)
                {
                    target[pair.Key] = pair.Value;
                }
            }
            catch (BlueprintJsonException)
            {
            }
        }

        private static object DeserializeJsonValue(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return BlueprintJson.Deserialize(json);
            }
            catch (BlueprintJsonException)
            {
                return null;
            }
        }
    }

    internal static class BehaviorTreeGraphToolkitReflection
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void CreateNode(BehaviorTreeVisualGraph graph, Node node, Vector2 position)
        {
            object implementation = GetGraphImplementation(graph);
            MethodInfo createNodeMethod = implementation.GetType().GetMethod("CreateNodeModel", Flags, null, new[] { typeof(Node), typeof(Vector2) }, null);
            if (createNodeMethod == null)
            {
                throw new MissingMethodException(implementation.GetType().FullName, "CreateNodeModel");
            }

            createNodeMethod.Invoke(implementation, new object[] { node, position });
        }

        public static void CreateWire(BehaviorTreeVisualGraph graph, IPort inputPort, IPort outputPort)
        {
            object implementation = GetGraphImplementation(graph);
            MethodInfo createWireMethod = FindMethod(implementation.GetType(), "CreateWire", method => method.GetParameters().Length == 3);
            if (createWireMethod == null)
            {
                throw new MissingMethodException(implementation.GetType().FullName, "CreateWire");
            }

            createWireMethod.Invoke(implementation, new object[] { inputPort, outputPort, new Hash128() });
        }

        public static IVariable CreateBlackboardVariable(BehaviorTreeVisualGraph graph, string name, Type valueType, object defaultValue)
        {
            object implementation = GetGraphImplementation(graph);
            MethodInfo createVariableMethod = FindMethod(implementation.GetType(), "CreateVariable", method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 4 &&
                       parameters[0].ParameterType == typeof(string) &&
                       parameters[1].ParameterType == typeof(Type) &&
                       parameters[3].ParameterType == typeof(VariableKind);
            });

            if (createVariableMethod == null)
            {
                throw new MissingMethodException(implementation.GetType().FullName, "CreateVariable");
            }

            return createVariableMethod.Invoke(implementation, new object[] { name, valueType, defaultValue, VariableKind.Local }) as IVariable;
        }

        public static INode CreateBlackboardVariableNode(BehaviorTreeVisualGraph graph, IVariable variable, Vector2 position)
        {
            if (variable == null)
            {
                throw new ArgumentNullException("variable");
            }

            object implementation = GetGraphImplementation(graph);
            MethodInfo createVariableNodeMethod = FindMethod(implementation.GetType(), "CreateVariableNode", method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 4 &&
                       parameters[1].ParameterType == typeof(Vector2) &&
                       parameters[2].ParameterType == typeof(Hash128);
            });

            if (createVariableNodeMethod == null)
            {
                throw new MissingMethodException(implementation.GetType().FullName, "CreateVariableNode");
            }

            ParameterInfo[] methodParameters = createVariableNodeMethod.GetParameters();
            object spawnFlags = Enum.ToObject(methodParameters[3].ParameterType, 0);
            return createVariableNodeMethod.Invoke(implementation, new object[] { variable, position, new Hash128(), spawnFlags }) as INode;
        }

        public static bool TryGetNodePosition(INode node, out Vector2 position)
        {
            object implementation = GetNodeImplementation(node);
            if (implementation == null)
            {
                position = Vector2.zero;
                return false;
            }

            PropertyInfo property = FindProperty(implementation.GetType(), "Position");
            if (property == null)
            {
                position = Vector2.zero;
                return false;
            }

            object value = property.GetValue(implementation, null);
            if (value is Vector2)
            {
                position = (Vector2)value;
                return true;
            }

            position = Vector2.zero;
            return false;
        }

        public static void EnsureSupportedVariableTypes(BehaviorTreeVisualGraph graph, IEnumerable<Type> supportedTypes)
        {
            if (graph == null || supportedTypes == null)
            {
                return;
            }

            object implementation = GetGraphImplementation(graph);
            PropertyInfo supportedTypesProperty = FindProperty(implementation.GetType(), "SupportedTypes");
            if (supportedTypesProperty != null)
            {
                supportedTypesProperty.GetValue(implementation, null);
            }

            FieldInfo supportedTypesField = FindField(implementation.GetType(), "m_SupportedTypes");
            List<Type> list = supportedTypesField == null ? null : supportedTypesField.GetValue(implementation) as List<Type>;
            if (list == null)
            {
                return;
            }

            foreach (Type type in supportedTypes)
            {
                if (type != null && !list.Contains(type))
                {
                    list.Add(type);
                }
            }

            list.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
        }

        public static void MarkDirty(BehaviorTreeVisualGraph graph)
        {
            object implementation = GetGraphImplementation(graph);
            MethodInfo dirtyMethod = FindMethod(implementation.GetType(), "SetGraphObjectDirty", method => method.GetParameters().Length == 0);
            if (dirtyMethod != null)
            {
                dirtyMethod.Invoke(implementation, null);
            }
        }

        private static object GetGraphImplementation(Graph graph)
        {
            FieldInfo implementationField = typeof(Graph).GetField("m_Implementation", Flags);
            object implementation = implementationField == null ? null : implementationField.GetValue(graph);
            if (implementation == null)
            {
                throw new InvalidOperationException("Graph has no Graph Toolkit implementation. Load or create it through GraphDatabase first.");
            }

            return implementation;
        }

        private static object GetNodeImplementation(INode node)
        {
            Node visualNode = node as Node;
            if (visualNode == null)
            {
                return node;
            }

            FieldInfo implementationField = typeof(Node).GetField("m_Implementation", Flags);
            return implementationField == null ? null : implementationField.GetValue(visualNode);
        }

        private static MethodInfo FindMethod(Type type, string name, Func<MethodInfo, bool> predicate)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo[] methods = current.GetMethods(Flags | BindingFlags.DeclaredOnly);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name == name && predicate(method))
                    {
                        return method;
                    }
                }
            }

            return null;
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
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
                FieldInfo field = current.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }
    }
}
