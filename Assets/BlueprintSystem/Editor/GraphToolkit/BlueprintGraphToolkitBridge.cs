using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    public static class BlueprintGraphToolkitBridge
    {
        private const string ImportMenu = "Tools/Blueprint System/Graph Toolkit/Import Selected Blueprint JSON";
        private const string ExportMenu = "Tools/Blueprint System/Graph Toolkit/Export Selected Blueprint Graph To JSON";

        [MenuItem(ImportMenu)]
        public static void ImportSelectedBlueprintJson()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            string graphPath = ImportBlueprintAtPath(path, true);
            Debug.Log("[Blueprint] Imported visual graph: " + graphPath);
        }

        [MenuItem(ImportMenu, true)]
        private static bool CanImportSelectedBlueprintJson()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return IsBlueprintJsonPath(path);
        }

        [MenuItem(ExportMenu)]
        public static void ExportSelectedBlueprintGraph()
        {
            string graphPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            string outputPath = ExportGraphAtPath(graphPath, null);
            Debug.Log("[Blueprint] Exported blueprint JSON: " + outputPath);
        }

        [MenuItem(ExportMenu, true)]
        private static bool CanExportSelectedBlueprintGraph()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return IsBlueprintGraphAssetPath(path);
        }

        public static string ImportBlueprintAtPath(string blueprintAssetPath, bool openAsset)
        {
            return ImportBlueprintAtPath(blueprintAssetPath, GetDefaultGraphPath(blueprintAssetPath), openAsset);
        }

        public static string ImportBlueprintAtPath(string blueprintAssetPath, string graphAssetPath, bool openAsset)
        {
            if (!IsBlueprintJsonPath(blueprintAssetPath))
            {
                throw new ArgumentException("Expected a .blueprint.json asset path.", "blueprintAssetPath");
            }

            BlueprintSource source = BlueprintSource.FromJson(File.ReadAllText(blueprintAssetPath));
            MigrateLegacyBindButtonClickEvents(source);
            BlueprintNodeManifestCollection manifests = LoadProjectManifests();
            string graphPath = string.IsNullOrEmpty(graphAssetPath) ? GetDefaultGraphPath(blueprintAssetPath) : graphAssetPath;
            string directory = Path.GetDirectoryName(graphPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
            graph.SourceBlueprintAssetPath = blueprintAssetPath;
            graph.SchemaVersion = source.SchemaVersion;
            graph.BlueprintName = source.Name;
            graph.Category = source.Category;
            graph.Description = source.Description;
            graph.Variables = ConvertVariables(source.Variables);
            graph.Bindings = ConvertBindings(source.Bindings);
            graph.Components = ConvertComponents(source.Components);
            BlueprintGraphToolkitBlackboardSync.SyncVariablesToBlackboard(graph);

            Dictionary<string, INode> nodesById = new Dictionary<string, INode>();
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BlueprintNodeSource nodeSource = source.Nodes[i];
                BlueprintNodeManifest manifest;
                manifests.TryGet(nodeSource.TypeId, out manifest);
                INode visualNode = CreateGraphNode(graph, nodeSource, manifest, source.Variables);
                if (!string.IsNullOrEmpty(nodeSource.Id))
                {
                    nodesById[nodeSource.Id] = visualNode;
                }
            }

            for (int i = 0; i < source.Edges.Count; i++)
            {
                BlueprintEdgeSource edge = source.Edges[i];
                string fromNodeId;
                string fromPortId;
                string toNodeId;
                string toPortId;
                if (!TrySplitPortReference(edge.From, out fromNodeId, out fromPortId) ||
                    !TrySplitPortReference(edge.To, out toNodeId, out toPortId))
                {
                    Debug.LogWarning("[Blueprint] Skipped invalid edge reference: " + edge.From + " -> " + edge.To);
                    continue;
                }

                INode fromNode;
                INode toNode;
                if (!nodesById.TryGetValue(fromNodeId, out fromNode) || !nodesById.TryGetValue(toNodeId, out toNode))
                {
                    Debug.LogWarning("[Blueprint] Skipped edge with missing node: " + edge.From + " -> " + edge.To);
                    continue;
                }

                IPort fromPort = SafeGetOutputPort(fromNode, fromPortId);
                IPort toPort = SafeGetInputPort(toNode, toPortId);
                if (fromPort == null || toPort == null)
                {
                    Debug.LogWarning("[Blueprint] Skipped edge with missing port: " + edge.From + " -> " + edge.To);
                    continue;
                }

                BlueprintGraphToolkitReflection.CreateWire(graph, toPort, fromPort);
            }

            BlueprintGraphToolkitReflection.MarkDirty(graph);
            using (BlueprintGraphToolkitAutoExport.SuppressAutoExport())
            {
                GraphDatabase.SaveGraphIfDirty(graph);
                AssetDatabase.ImportAsset(graphPath);
            }

            if (openAsset)
            {
                UnityEngine.Object graphAsset = AssetDatabase.LoadMainAssetAtPath(graphPath);
                if (graphAsset != null)
                {
                    AssetDatabase.OpenAsset(graphAsset);
                }
            }

            return graphPath;
        }

        private static INode CreateGraphNode(
            BlueprintVisualGraph graph,
            BlueprintNodeSource nodeSource,
            BlueprintNodeManifest manifest,
            List<BlueprintVariableDeclaration> variables)
        {
            IVariable blackboardVariable;
            if (CanImportAsBlackboardVariableNode(graph, nodeSource, out blackboardVariable))
            {
                return BlueprintGraphToolkitReflection.CreateBlackboardVariableNode(
                    graph,
                    blackboardVariable,
                    new Vector2(nodeSource.X, nodeSource.Y));
            }

            BlueprintVisualNode visualNode = CreateVisualNode(nodeSource, manifest, variables);
            BlueprintGraphToolkitReflection.CreateNode(graph, visualNode, new Vector2(nodeSource.X, nodeSource.Y));
            return visualNode;
        }

        private static bool CanImportAsBlackboardVariableNode(BlueprintVisualGraph graph, BlueprintNodeSource nodeSource, out IVariable variable)
        {
            variable = null;
            if (graph == null || nodeSource == null || nodeSource.TypeId != "Variable.Get")
            {
                return false;
            }

            string variableName = GetStringProperty(nodeSource, "name");
            if (string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            foreach (IVariable candidate in graph.GetVariables())
            {
                if (candidate == null || candidate.name != variableName)
                {
                    continue;
                }

                string blueprintType;
                if (!BlueprintGraphToolkitBlackboardSync.TryGetBlueprintType(graph, candidate, out blueprintType))
                {
                    return false;
                }

                variable = candidate;
                return true;
            }

            return false;
        }

        public static string ExportGraphAtPath(string graphAssetPath, string outputBlueprintPath)
        {
            if (!IsBlueprintGraphAssetPath(graphAssetPath))
            {
                throw new ArgumentException("Expected a ." + BlueprintVisualGraph.AssetExtension + " asset path.", "graphAssetPath");
            }

            BlueprintVisualGraph graph = GraphDatabase.LoadGraph<BlueprintVisualGraph>(graphAssetPath);
            if (graph == null)
            {
                throw new InvalidOperationException("Unable to load blueprint visual graph at " + graphAssetPath);
            }

            if (string.IsNullOrEmpty(outputBlueprintPath))
            {
                outputBlueprintPath = string.IsNullOrEmpty(graph.SourceBlueprintAssetPath)
                    ? GetDefaultBlueprintJsonPath(graphAssetPath)
                    : graph.SourceBlueprintAssetPath;
            }

            BlueprintSource source = ToBlueprintSource(graph);
            MigrateLegacyBindButtonClickEvents(source);
            File.WriteAllText(outputBlueprintPath, source.ToJson());
            AssetDatabase.ImportAsset(outputBlueprintPath);
            BlueprintCompiledAsset compiledAsset;
            if (!BlueprintCompiledAssetCompiler.CompileBlueprintAtPath(outputBlueprintPath, false, out compiledAsset))
            {
                throw new InvalidOperationException("Exported blueprint JSON could not be compiled: " + outputBlueprintPath);
            }

            return outputBlueprintPath;
        }

        internal static bool RefreshOpenGraphToolkitAtPath(string graphAssetPath)
        {
            if (!IsBlueprintGraphAssetPath(graphAssetPath) || !File.Exists(graphAssetPath))
            {
                return false;
            }

            List<EditorWindow> windows = GetGraphToolkitWindowsForPath(graphAssetPath);
            if (windows.Count == 0)
            {
                return false;
            }

            AssetDatabase.ImportAsset(graphAssetPath, ImportAssetOptions.ForceUpdate);
            bool refreshed = false;
            for (int i = 0; i < windows.Count; i++)
            {
                if (ReloadGraphToolkitWindowFromDisk(windows[i], graphAssetPath))
                {
                    refreshed = true;
                }
            }

            if (!refreshed)
            {
                UnityEngine.Object graphAsset = AssetDatabase.LoadMainAssetAtPath(graphAssetPath);
                if (graphAsset != null)
                {
                    AssetDatabase.OpenAsset(graphAsset);
                }
            }

            RepaintGraphToolkitWindows(windows);
            return refreshed;
        }

        public static BlueprintSource ToBlueprintSource(BlueprintVisualGraph graph)
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = string.IsNullOrEmpty(graph.SchemaVersion) ? "0.1" : graph.SchemaVersion;
            source.Name = graph.BlueprintName;
            source.Category = graph.Category;
            source.Description = graph.Description;

            List<BlueprintVisualVariableData> exportedVariables = BlueprintGraphToolkitBlackboardSync.ExtractVariables(graph);
            graph.Variables = exportedVariables;
            if (exportedVariables != null)
            {
                for (int i = 0; i < exportedVariables.Count; i++)
                {
                    BlueprintVisualVariableData visualVariable = exportedVariables[i];
                    if (visualVariable == null)
                    {
                        continue;
                    }

                    BlueprintVariableDeclaration variable = new BlueprintVariableDeclaration();
                    variable.Name = visualVariable.Name;
                    variable.Type = visualVariable.Type;
                    variable.Scope = visualVariable.Scope;
                    variable.Exposed = visualVariable.Exposed;
                    variable.Persistent = visualVariable.Persistent;
                    variable.Description = visualVariable.Description;
                    if (visualVariable.HasDefaultValue)
                    {
                        variable.DefaultValue = BlueprintVisualValueUtility.FromJson(visualVariable.JsonDefaultValue);
                    }

                    source.Variables.Add(variable);
                }
            }

            if (graph.Bindings != null)
            {
                for (int i = 0; i < graph.Bindings.Count; i++)
                {
                    BlueprintVisualBindingData visualBinding = graph.Bindings[i];
                    if (visualBinding == null)
                    {
                        continue;
                    }

                    source.Bindings.Add(new BlueprintBindingDeclaration
                    {
                        Name = visualBinding.Name,
                        Type = visualBinding.Type,
                        Required = visualBinding.Required
                    });
                }
            }

            if (graph.Components != null)
            {
                for (int i = 0; i < graph.Components.Count; i++)
                {
                    BlueprintVisualComponentData visualComponent = graph.Components[i];
                    if (visualComponent == null)
                    {
                        continue;
                    }

                    source.Components.Add(new BlueprintComponentDeclaration
                    {
                        Name = visualComponent.Name,
                        Blueprint = visualComponent.Blueprint,
                        Required = visualComponent.Required
                    });
                }
            }

            List<INode> exportNodes = GetExportableNodes(graph);
            Dictionary<INode, string> nodeIds = new Dictionary<INode, string>();
            HashSet<string> usedNodeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < exportNodes.Count; i++)
            {
                INode exportNode = exportNodes[i];
                BlueprintNodeSource node = exportNode is BlueprintVisualNode
                    ? ToSourceNode((BlueprintVisualNode)exportNode)
                    : ToVariableGetSourceNode((IVariableNode)exportNode, usedNodeIds);
                usedNodeIds.Add(node.Id);
                nodeIds[exportNode] = node.Id;
                source.Nodes.Add(node);
            }

            HashSet<string> edgeKeys = new HashSet<string>();
            for (int i = 0; i < exportNodes.Count; i++)
            {
                INode fromNode = exportNodes[i];
                string fromNodeId;
                if (!nodeIds.TryGetValue(fromNode, out fromNodeId))
                {
                    continue;
                }

                foreach (IPort outputPort in fromNode.GetOutputPorts())
                {
                    List<IPort> connectedPorts = new List<IPort>();
                    outputPort.GetConnectedPorts(connectedPorts);
                    for (int c = 0; c < connectedPorts.Count; c++)
                    {
                        IPort connectedPort = connectedPorts[c];
                        if (connectedPort == null || connectedPort.direction != PortDirection.Input)
                        {
                            continue;
                        }

                        INode toNode = connectedPort.GetNode();
                        string toNodeId;
                        if (toNode == null || !nodeIds.TryGetValue(toNode, out toNodeId))
                        {
                            continue;
                        }

                        string from = fromNodeId + "." + GetExportedPortName(fromNode, outputPort);
                        string to = toNodeId + "." + GetExportedPortName(toNode, connectedPort);
                        string key = from + "->" + to;
                        if (!edgeKeys.Add(key))
                        {
                            continue;
                        }

                        source.Edges.Add(new BlueprintEdgeSource { From = from, To = to });
                    }
                }
            }

            return source;
        }

        private static List<INode> GetExportableNodes(BlueprintVisualGraph graph)
        {
            List<INode> result = new List<INode>();
            foreach (INode node in graph.GetNodes())
            {
                if (node is BlueprintVisualNode || IsReadableVariableNode(node as IVariableNode))
                {
                    result.Add(node);
                }
            }

            return result;
        }

        private static BlueprintNodeSource ToSourceNode(BlueprintVisualNode visualNode)
        {
            BlueprintNodeSource node = new BlueprintNodeSource();
            node.Id = visualNode.ReadNodeId();
            node.TypeId = visualNode.ReadTypeId();
            Vector2 position;
            if (BlueprintGraphToolkitReflection.TryGetNodePosition(visualNode, out position))
            {
                node.X = position.x;
                node.Y = position.y;
            }

            if (visualNode.Properties != null)
            {
                for (int p = 0; p < visualNode.Properties.Count; p++)
                {
                    BlueprintVisualPropertyData property = visualNode.Properties[p];
                    if (property == null || string.IsNullOrEmpty(property.Id))
                    {
                        continue;
                    }

                    object value;
                    if (visualNode.TryReadPropertyValue(property, out value))
                    {
                        node.Properties[property.Id] = value;
                    }
                }
            }

            return node;
        }

        private static BlueprintNodeSource ToVariableGetSourceNode(IVariableNode variableNode, HashSet<string> usedNodeIds)
        {
            BlueprintNodeSource node = new BlueprintNodeSource();
            string variableName = variableNode.variable.name;
            node.Id = CreateUniqueNodeId("get_" + ToSnakeCase(variableName), usedNodeIds);
            node.TypeId = "Variable.Get";
            node.Properties["name"] = variableName;

            Vector2 position;
            if (BlueprintGraphToolkitReflection.TryGetNodePosition(variableNode, out position))
            {
                node.X = position.x;
                node.Y = position.y;
            }

            return node;
        }

        private static bool IsReadableVariableNode(IVariableNode variableNode)
        {
            if (variableNode == null || variableNode.variable == null)
            {
                return false;
            }

            foreach (IPort outputPort in variableNode.GetOutputPorts())
            {
                if (outputPort != null && outputPort.direction == PortDirection.Output)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetExportedPortName(INode node, IPort port)
        {
            if (node is IVariableNode)
            {
                return "value";
            }

            return port.name;
        }

        private static string CreateUniqueNodeId(string baseId, HashSet<string> usedNodeIds)
        {
            baseId = SanitizeIdentifier(baseId).ToLowerInvariant();
            if (string.IsNullOrEmpty(baseId))
            {
                baseId = "node";
            }

            if (usedNodeIds.Add(baseId))
            {
                return baseId;
            }

            int suffix = 2;
            while (!usedNodeIds.Add(baseId + "_" + suffix.ToString()))
            {
                suffix++;
            }

            return baseId + "_" + suffix.ToString();
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            char[] chars = value.ToCharArray();
            List<char> result = new List<char>(chars.Length);
            bool previousWasSeparator = false;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    result.Add(c);
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator && result.Count > 0)
                {
                    result.Add('_');
                    previousWasSeparator = true;
                }
            }

            while (result.Count > 0 && result[result.Count - 1] == '_')
            {
                result.RemoveAt(result.Count - 1);
            }

            if (result.Count > 0 && char.IsDigit(result[0]))
            {
                result.Insert(0, '_');
            }

            return new string(result.ToArray());
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            List<char> result = new List<char>(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsUpper(c) && i > 0 && result.Count > 0 && result[result.Count - 1] != '_')
                {
                    result.Add('_');
                }

                result.Add(char.ToLowerInvariant(c));
            }

            return new string(result.ToArray());
        }

        public static string GetDefaultGraphPath(string blueprintAssetPath)
        {
            if (blueprintAssetPath.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase))
            {
                return blueprintAssetPath.Substring(0, blueprintAssetPath.Length - ".blueprint.json".Length) + "." + BlueprintVisualGraph.AssetExtension;
            }

            return Path.ChangeExtension(blueprintAssetPath, "." + BlueprintVisualGraph.AssetExtension);
        }

        public static string GetDefaultBlueprintJsonPath(string graphAssetPath)
        {
            if (graphAssetPath.EndsWith("." + BlueprintVisualGraph.AssetExtension, StringComparison.OrdinalIgnoreCase))
            {
                return graphAssetPath.Substring(0, graphAssetPath.Length - BlueprintVisualGraph.AssetExtension.Length - 1) + ".blueprint.json";
            }

            return Path.ChangeExtension(graphAssetPath, ".blueprint.json");
        }

        private static List<BlueprintVisualVariableData> ConvertVariables(List<BlueprintVariableDeclaration> variables)
        {
            List<BlueprintVisualVariableData> result = new List<BlueprintVisualVariableData>();
            for (int i = 0; i < variables.Count; i++)
            {
                BlueprintVariableDeclaration variable = variables[i];
                object defaultValue = variable.DefaultValue;
                object structuredDefaultValue;
                if (BlueprintStructuredValueUtility.TryConvertToJsonValue(variable.DefaultValue, variable.Type, out structuredDefaultValue))
                {
                    defaultValue = structuredDefaultValue;
                }

                result.Add(new BlueprintVisualVariableData
                {
                    Id = variable.Id,
                    Name = variable.Name,
                    Type = variable.Type,
                    HasDefaultValue = defaultValue != null,
                    JsonDefaultValue = defaultValue == null ? string.Empty : BlueprintVisualValueUtility.ToJson(defaultValue),
                    Scope = variable.Scope,
                    Exposed = variable.Exposed,
                    Persistent = variable.Persistent,
                    Description = variable.Description
                });
            }

            return result;
        }

        private static List<BlueprintVisualBindingData> ConvertBindings(List<BlueprintBindingDeclaration> bindings)
        {
            List<BlueprintVisualBindingData> result = new List<BlueprintVisualBindingData>();
            for (int i = 0; i < bindings.Count; i++)
            {
                BlueprintBindingDeclaration binding = bindings[i];
                result.Add(new BlueprintVisualBindingData
                {
                    Name = binding.Name,
                    Type = binding.Type,
                    Required = binding.Required
                });
            }

            return result;
        }

        private static List<BlueprintVisualComponentData> ConvertComponents(List<BlueprintComponentDeclaration> components)
        {
            List<BlueprintVisualComponentData> result = new List<BlueprintVisualComponentData>();
            for (int i = 0; i < components.Count; i++)
            {
                BlueprintComponentDeclaration component = components[i];
                result.Add(new BlueprintVisualComponentData
                {
                    Name = component.Name,
                    Blueprint = component.Blueprint,
                    Required = component.Required
                });
            }

            return result;
        }

        internal static BlueprintVisualNode CreateVisualNode(BlueprintNodeSource nodeSource, BlueprintNodeManifest manifest)
        {
            return CreateVisualNode(nodeSource, manifest, null);
        }

        internal static BlueprintVisualNode CreateVisualNode(BlueprintNodeSource nodeSource, BlueprintNodeManifest manifest, List<BlueprintVariableDeclaration> variables)
        {
            BlueprintVisualNode node = BlueprintVisualNodeFactory.Create(nodeSource.TypeId);
            node.NodeId = nodeSource.Id;
            node.TypeId = nodeSource.TypeId;
            node.Title = manifest == null ? nodeSource.TypeId : manifest.Title;
            node.Category = manifest == null ? string.Empty : manifest.Category;
            node.Description = manifest == null ? string.Empty : manifest.Description;

            if (manifest != null)
            {
                for (int i = 0; i < manifest.Inputs.Count; i++)
                {
                    node.Inputs.Add(ConvertPort(manifest.Inputs[i]));
                }

                for (int i = 0; i < manifest.Outputs.Count; i++)
                {
                    node.Outputs.Add(ConvertPort(manifest.Outputs[i]));
                }

                HashSet<string> manifestPropertyIds = new HashSet<string>();
                for (int i = 0; i < manifest.Properties.Count; i++)
                {
                    BlueprintPropertySpec spec = manifest.Properties[i];
                    manifestPropertyIds.Add(spec.Id);
                    node.Properties.Add(ConvertProperty(spec, nodeSource));
                }

                foreach (KeyValuePair<string, object> pair in nodeSource.Properties)
                {
                    if (!manifestPropertyIds.Contains(pair.Key))
                    {
                        node.Properties.Add(new BlueprintVisualPropertyData
                        {
                            Id = pair.Key,
                            DisplayName = null,
                            Type = string.Empty,
                            Required = false,
                            HasValue = true,
                            JsonValue = BlueprintVisualValueUtility.ToJson(pair.Value),
                            ShowInInspectorOnly = false
                        });
                    }
                }
            }
            else
            {
                foreach (KeyValuePair<string, object> pair in nodeSource.Properties)
                {
                    node.Properties.Add(new BlueprintVisualPropertyData
                    {
                        Id = pair.Key,
                        DisplayName = null,
                        Type = string.Empty,
                        Required = false,
                        HasValue = true,
                        JsonValue = BlueprintVisualValueUtility.ToJson(pair.Value),
                        ShowInInspectorOnly = false
                    });
                }
            }

            ApplyCustomEventNodeMetadata(node, nodeSource);
            ApplyVariableNodeMetadata(node, nodeSource, variables);
            ApplyBlueprintAccessNodeMetadata(node, nodeSource);
            return node;
        }

        private static void ApplyCustomEventNodeMetadata(BlueprintVisualNode node, BlueprintNodeSource nodeSource)
        {
            if (node == null || nodeSource == null || nodeSource.TypeId != "Game.Event.Custom")
            {
                return;
            }

            SetPropertyDisplayName(node.Properties, "eventName", "Event");

            string eventName = GetStringProperty(nodeSource, "eventName");
            if (!string.IsNullOrEmpty(eventName))
            {
                node.Title = "Custom Event: " + eventName;
                SetPortDisplayName(node.Outputs, "execOut", eventName);
            }
        }

        private static void ApplyVariableNodeMetadata(BlueprintVisualNode node, BlueprintNodeSource nodeSource, List<BlueprintVariableDeclaration> variables)
        {
            if (node == null || nodeSource == null || (nodeSource.TypeId != "Variable.Get" && nodeSource.TypeId != "Variable.Set"))
            {
                return;
            }

            string variableName = GetStringProperty(nodeSource, "name");
            if (string.IsNullOrEmpty(variableName))
            {
                return;
            }

            if (nodeSource.TypeId == "Variable.Get")
            {
                node.Title = "Get " + variableName;
                BlueprintVariableDeclaration variable;
                if (TryGetVariable(variables, variableName, out variable))
                {
                    SetPortType(node.Outputs, "value", variable.Type);
                }
            }
            else if (nodeSource.TypeId == "Variable.Set")
            {
                node.Title = "Set " + variableName;
                SetPortDisplayName(node.Inputs, "value", "New Value");
                SetPropertyDisplayName(node.Properties, "name", "Variable");
                SetPropertyInspectorOnly(node.Properties, "name", true);
                BlueprintVariableDeclaration variable;
                if (TryGetVariable(variables, variableName, out variable))
                {
                    SetPortType(node.Inputs, "value", variable.Type);
                    SetPropertyType(node.Properties, "value", variable.Type);
                }
            }
        }

        private static string GetStringProperty(BlueprintNodeSource nodeSource, string propertyId)
        {
            object value;
            if (nodeSource.Properties.TryGetValue(propertyId, out value) && value != null)
            {
                return value.ToString();
            }

            return null;
        }

        private static bool TryGetVariable(List<BlueprintVariableDeclaration> variables, string variableName, out BlueprintVariableDeclaration variable)
        {
            variable = null;
            if (variables == null || string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            for (int i = 0; i < variables.Count; i++)
            {
                BlueprintVariableDeclaration candidate = variables[i];
                if (candidate != null && candidate.Name == variableName)
                {
                    variable = candidate;
                    return true;
                }
            }

            return false;
        }

        private static void SetPortType(List<BlueprintVisualPortData> ports, string portId, string type)
        {
            if (ports == null)
            {
                return;
            }

            for (int i = 0; i < ports.Count; i++)
            {
                BlueprintVisualPortData port = ports[i];
                if (port != null && port.Id == portId)
                {
                    port.Type = type;
                    return;
                }
            }
        }

        private static void SetPortDisplayName(List<BlueprintVisualPortData> ports, string portId, string displayName)
        {
            if (ports == null)
            {
                return;
            }

            for (int i = 0; i < ports.Count; i++)
            {
                BlueprintVisualPortData port = ports[i];
                if (port != null && port.Id == portId)
                {
                    port.DisplayName = displayName;
                    return;
                }
            }
        }

        private static void ApplyBlueprintAccessNodeMetadata(BlueprintVisualNode node, BlueprintNodeSource nodeSource)
        {
            if (node == null || nodeSource == null)
            {
                return;
            }

            if (nodeSource.TypeId != "Blueprint.IsValid" &&
                nodeSource.TypeId != "Blueprint.TriggerEvent" &&
                nodeSource.TypeId != "Blueprint.GetVariable" &&
                nodeSource.TypeId != "Blueprint.SetVariable")
            {
                return;
            }

            SetPropertyInspectorOnly(node.Properties, "target", true);
        }

        private static void SetPropertyType(List<BlueprintVisualPropertyData> properties, string propertyId, string type)
        {
            if (properties == null)
            {
                return;
            }

            for (int i = 0; i < properties.Count; i++)
            {
                BlueprintVisualPropertyData property = properties[i];
                if (property != null && property.Id == propertyId)
                {
                    property.Type = type;
                    return;
                }
            }
        }

        private static void SetPropertyDisplayName(List<BlueprintVisualPropertyData> properties, string propertyId, string displayName)
        {
            if (properties == null)
            {
                return;
            }

            for (int i = 0; i < properties.Count; i++)
            {
                BlueprintVisualPropertyData property = properties[i];
                if (property != null && property.Id == propertyId)
                {
                    property.DisplayName = displayName;
                    return;
                }
            }
        }

        private static void SetPropertyInspectorOnly(List<BlueprintVisualPropertyData> properties, string propertyId, bool showInInspectorOnly)
        {
            if (properties == null)
            {
                return;
            }

            for (int i = 0; i < properties.Count; i++)
            {
                BlueprintVisualPropertyData property = properties[i];
                if (property != null && property.Id == propertyId)
                {
                    property.ShowInInspectorOnly = showInInspectorOnly;
                    return;
                }
            }
        }

        private static BlueprintVisualPortData ConvertPort(BlueprintPortSpec spec)
        {
            return new BlueprintVisualPortData
            {
                Id = spec.Id,
                DisplayName = null,
                Kind = spec.Kind == BlueprintPortKind.Exec ? "exec" : "value",
                Type = spec.Type,
                Required = spec.Required,
                Source = SourceToString(spec.Source),
                AllowMultiple = spec.AllowMultiple
            };
        }

        private static BlueprintVisualPropertyData ConvertProperty(BlueprintPropertySpec spec, BlueprintNodeSource nodeSource)
        {
            object value;
            bool hasValue = nodeSource.Properties.TryGetValue(spec.Id, out value);
            if (!hasValue && spec.DefaultValue != null)
            {
                value = spec.DefaultValue;
                hasValue = true;
            }

            return new BlueprintVisualPropertyData
            {
                Id = spec.Id,
                DisplayName = null,
                Type = spec.Type,
                Required = spec.Required,
                HasValue = hasValue,
                JsonValue = hasValue ? BlueprintVisualValueUtility.ToJson(value) : string.Empty,
                ShowInInspectorOnly = false
            };
        }

        private static string SourceToString(BlueprintValueSource source)
        {
            switch (source)
            {
                case BlueprintValueSource.Property:
                    return "property";
                case BlueprintValueSource.Connection:
                    return "connection";
                case BlueprintValueSource.PropertyOrConnection:
                    return "propertyOrConnection";
                default:
                    return string.Empty;
            }
        }

        private static IPort SafeGetInputPort(INode node, string portId)
        {
            IVariableNode variableNode = node as IVariableNode;
            if (variableNode != null)
            {
                return FindFirstPort(variableNode.GetInputPorts());
            }

            try
            {
                return node.GetInputPortByName(portId);
            }
            catch
            {
                return null;
            }
        }

        private static IPort SafeGetOutputPort(INode node, string portId)
        {
            IVariableNode variableNode = node as IVariableNode;
            if (variableNode != null)
            {
                return FindFirstPort(variableNode.GetOutputPorts());
            }

            try
            {
                return node.GetOutputPortByName(portId);
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

        private static bool TrySplitPortReference(string reference, out string nodeId, out string portId)
        {
            int dotIndex = string.IsNullOrEmpty(reference) ? -1 : reference.LastIndexOf('.');
            if (dotIndex <= 0 || dotIndex >= reference.Length - 1)
            {
                nodeId = null;
                portId = null;
                return false;
            }

            nodeId = reference.Substring(0, dotIndex);
            portId = reference.Substring(dotIndex + 1);
            return true;
        }

        internal static BlueprintNodeManifestCollection LoadProjectManifests()
        {
            string root = Path.Combine(Application.dataPath, "BlueprintSystem/Specs/Nodes");
            List<string> jsonTexts = new List<string>();
            if (Directory.Exists(root))
            {
                string[] files = Directory.GetFiles(root, "*.node.json", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    jsonTexts.Add(File.ReadAllText(files[i]));
                }
            }

            return BlueprintNodeManifestCollection.FromJsonTexts(jsonTexts);
        }

        private static void MigrateLegacyBindButtonClickEvents(BlueprintSource source)
        {
            if (source == null)
            {
                return;
            }

            HashSet<string> edgeKeys = new HashSet<string>();
            for (int i = 0; i < source.Edges.Count; i++)
            {
                BlueprintEdgeSource edge = source.Edges[i];
                edgeKeys.Add(edge.From + "->" + edge.To);
            }

            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BlueprintNodeSource node = source.Nodes[i];
                if (node.TypeId != "UI.BindButtonClick")
                {
                    continue;
                }

                string oldFrom = node.Id + ".execOut";
                string newFrom = node.Id + ".bound";
                for (int edgeIndex = source.Edges.Count - 1; edgeIndex >= 0; edgeIndex--)
                {
                    BlueprintEdgeSource edge = source.Edges[edgeIndex];
                    if (edge.From != oldFrom)
                    {
                        continue;
                    }

                    string oldKey = edge.From + "->" + edge.To;
                    string newKey = newFrom + "->" + edge.To;
                    if (edgeKeys.Contains(newKey))
                    {
                        source.Edges.RemoveAt(edgeIndex);
                        edgeKeys.Remove(oldKey);
                    }
                    else
                    {
                        edge.From = newFrom;
                        edgeKeys.Remove(oldKey);
                        edgeKeys.Add(newKey);
                    }
                }
            }

            Dictionary<string, List<BlueprintEdgeSource>> customEventEdgesByName = new Dictionary<string, List<BlueprintEdgeSource>>();
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BlueprintNodeSource node = source.Nodes[i];
                if (node.TypeId != "Game.Event.Custom")
                {
                    continue;
                }

                object eventNameValue;
                if (!node.Properties.TryGetValue("eventName", out eventNameValue) || eventNameValue == null)
                {
                    continue;
                }

                string eventName = eventNameValue.ToString();
                if (string.IsNullOrEmpty(eventName))
                {
                    continue;
                }

                List<BlueprintEdgeSource> edges = new List<BlueprintEdgeSource>();
                string eventOutput = node.Id + ".execOut";
                for (int edgeIndex = 0; edgeIndex < source.Edges.Count; edgeIndex++)
                {
                    BlueprintEdgeSource edge = source.Edges[edgeIndex];
                    if (edge.From == eventOutput)
                    {
                        edges.Add(edge);
                    }
                }

                if (edges.Count > 0)
                {
                    customEventEdgesByName[eventName] = edges;
                }
            }

            HashSet<string> existingEdges = new HashSet<string>();
            for (int i = 0; i < source.Edges.Count; i++)
            {
                BlueprintEdgeSource edge = source.Edges[i];
                existingEdges.Add(edge.From + "->" + edge.To);
            }

            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BlueprintNodeSource node = source.Nodes[i];
                if (node.TypeId != "UI.BindButtonClick")
                {
                    continue;
                }

                object eventNameValue;
                if (!node.Properties.TryGetValue("eventName", out eventNameValue) || eventNameValue == null)
                {
                    continue;
                }

                node.Properties.Remove("eventName");
                string eventName = eventNameValue.ToString();
                List<BlueprintEdgeSource> eventEdges;
                if (string.IsNullOrEmpty(eventName) || !customEventEdgesByName.TryGetValue(eventName, out eventEdges))
                {
                    continue;
                }

                for (int edgeIndex = 0; edgeIndex < eventEdges.Count; edgeIndex++)
                {
                    string from = node.Id + ".clicked";
                    string to = eventEdges[edgeIndex].To;
                    string edgeKey = from + "->" + to;
                    if (existingEdges.Add(edgeKey))
                    {
                        source.Edges.Add(new BlueprintEdgeSource
                        {
                            From = from,
                            To = to
                        });
                    }
                }
            }
        }

        private static bool IsBlueprintJsonPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBlueprintGraphAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.EndsWith("." + BlueprintVisualGraph.AssetExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static List<EditorWindow> GetGraphToolkitWindowsForPath(string graphAssetPath)
        {
            List<EditorWindow> windows = new List<EditorWindow>();
            string normalizedPath = NormalizeAssetPath(graphAssetPath);
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                string windowPath;
                if (TryGetGraphToolkitWindowGraphPath(window, out windowPath) &&
                    string.Equals(NormalizeAssetPath(windowPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    windows.Add(window);
                }
            }

            return windows;
        }

        private static bool ReloadGraphToolkitWindowFromDisk(EditorWindow window, string graphAssetPath)
        {
            object graphTool = GetInstancePropertyValue(window, "GraphTool");
            object toolState = GetInstancePropertyValue(graphTool, "ToolState");
            object graphModel = GetInstancePropertyValue(toolState, "GraphModel");
            object graphObject = GetInstancePropertyValue(graphModel, "GraphObject") ?? GetInstancePropertyValue(toolState, "GraphObject");
            string graphLabel = GetInstancePropertyValue(toolState, "CurrentGraphLabel") as string;

            InvokeInstanceMethod(graphObject, "UnloadObject");

            BlueprintVisualGraph reloadedGraph = GraphDatabase.LoadGraph<BlueprintVisualGraph>(graphAssetPath);
            object reloadedGraphModel = GetGraphModelImplementation(reloadedGraph);
            object loadCommand = CreateGraphToolkitLoadGraphCommand(reloadedGraphModel, graphLabel);
            if (loadCommand == null || !DispatchGraphToolkitCommand(graphTool, loadCommand))
            {
                return false;
            }

            InvokeInstanceMethod(window, "UpdateTooltips");
            InvokeInstanceMethod(graphTool, "Update");
            ForceCompleteGraphToolkitUiUpdate(window);
            InvokeInstanceMethod(graphTool, "Update");
            return true;
        }

        private static void RepaintGraphToolkitWindows(List<EditorWindow> windows)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i] != null)
                {
                    windows[i].Repaint();
                }
            }
        }

        private static object GetGraphModelImplementation(Graph graph)
        {
            if (graph == null)
            {
                return null;
            }

            FieldInfo implementationField = typeof(Graph).GetField("m_Implementation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return implementationField == null ? null : implementationField.GetValue(graph);
        }

        private static object CreateGraphToolkitLoadGraphCommand(object graphModel, string title)
        {
            if (graphModel == null)
            {
                return null;
            }

            Type commandType = FindLoadedType(graphModel.GetType().Assembly, "Unity.GraphToolkit.Editor.LoadGraphCommand");
            if (commandType == null)
            {
                return null;
            }

            Type loadStrategiesType = commandType.GetNestedType("LoadStrategies", BindingFlags.Public | BindingFlags.NonPublic);
            if (loadStrategiesType == null)
            {
                return null;
            }

            ConstructorInfo constructor = FindInstanceConstructor(commandType, parameters =>
            {
                return parameters.Length == 5 &&
                       parameters[0].ParameterType.IsInstanceOfType(graphModel) &&
                       parameters[2].ParameterType == loadStrategiesType;
            });

            if (constructor == null)
            {
                return null;
            }

            object replaceStrategy = Enum.Parse(loadStrategiesType, "Replace");
            return constructor.Invoke(new object[] { graphModel, null, replaceStrategy, -1, title ?? string.Empty });
        }

        private static bool DispatchGraphToolkitCommand(object graphTool, object command)
        {
            if (graphTool == null || command == null)
            {
                return false;
            }

            MethodInfo dispatchMethod = FindInstanceMethod(graphTool.GetType(), "Dispatch", method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length >= 1 &&
                       parameters.Length <= 2 &&
                       parameters[0].ParameterType.IsInstanceOfType(command);
            });

            if (dispatchMethod == null)
            {
                return false;
            }

            ParameterInfo[] dispatchParameters = dispatchMethod.GetParameters();
            object[] arguments = dispatchParameters.Length == 1
                ? new[] { command }
                : new[] { command, GetDefaultValue(dispatchParameters[1].ParameterType) };
            dispatchMethod.Invoke(graphTool, arguments);
            return true;
        }

        private static void ForceCompleteGraphToolkitUiUpdate(EditorWindow window)
        {
            object graphView = GetInstancePropertyValue(window, "GraphView");
            object graphViewModel = GetInstancePropertyValue(graphView, "GraphViewModel");
            ForceCompleteStateComponentUpdate(GetInstancePropertyValue(graphViewModel, "GraphModelState"));
            ForceCompleteStateComponentUpdate(GetInstancePropertyValue(graphViewModel, "GraphViewState"));
        }

        private static void ForceCompleteStateComponentUpdate(object stateComponent)
        {
            object updateScope = GetInstancePropertyValue(stateComponent, "UpdateScope");
            if (updateScope == null)
            {
                return;
            }

            try
            {
                InvokeInstanceMethod(updateScope, "ForceCompleteUpdate");
            }
            finally
            {
                IDisposable disposable = updateScope as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
                else
                {
                    InvokeInstanceMethod(updateScope, "Dispose");
                }
            }
        }

        private static bool TryGetGraphToolkitWindowGraphPath(EditorWindow window, out string graphAssetPath)
        {
            graphAssetPath = null;
            if (window == null || !IsGraphToolkitWindowType(window.GetType()))
            {
                return false;
            }

            object graphTool = GetInstancePropertyValue(window, "GraphTool");
            object toolState = GetInstancePropertyValue(graphTool, "ToolState");
            object currentGraph = GetInstancePropertyValue(toolState, "CurrentGraph");
            graphAssetPath = GetInstancePropertyValue(currentGraph, "FilePath") as string;
            if (!string.IsNullOrEmpty(graphAssetPath))
            {
                return true;
            }

            object graphModel = GetInstancePropertyValue(toolState, "GraphModel");
            object graphObject = GetInstancePropertyValue(graphModel, "GraphObject");
            graphAssetPath = GetInstancePropertyValue(graphObject, "FilePath") as string;
            return !string.IsNullOrEmpty(graphAssetPath);
        }

        private static bool IsGraphToolkitWindowType(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (current.FullName == "Unity.GraphToolkit.Editor.GraphViewEditorWindow")
                {
                    return true;
                }
            }

            return false;
        }

        private static object GetInstancePropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            PropertyInfo property = FindInstanceProperty(target.GetType(), propertyName);
            return property == null ? null : property.GetValue(target, null);
        }

        private static PropertyInfo FindInstanceProperty(Type type, string propertyName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static object InvokeInstanceMethod(object target, string methodName)
        {
            if (target == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            MethodInfo method = FindInstanceMethod(target.GetType(), methodName, candidate => candidate.GetParameters().Length == 0);
            return method == null ? null : method.Invoke(target, null);
        }

        private static MethodInfo FindInstanceMethod(Type type, string methodName, Func<MethodInfo, bool> predicate)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo[] methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name == methodName && predicate(method))
                    {
                        return method;
                    }
                }
            }

            return null;
        }

        private static ConstructorInfo FindInstanceConstructor(Type type, Func<ParameterInfo[], bool> predicate)
        {
            ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < constructors.Length; i++)
            {
                ConstructorInfo constructor = constructors[i];
                if (predicate(constructor.GetParameters()))
                {
                    return constructor;
                }
            }

            return null;
        }

        private static Type FindLoadedType(Assembly preferredAssembly, string typeName)
        {
            if (preferredAssembly != null)
            {
                Type type = preferredAssembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static object GetDefaultValue(Type type)
        {
            if (type == null || !type.IsValueType)
            {
                return null;
            }

            return type.IsEnum ? Enum.ToObject(type, 0) : Activator.CreateInstance(type);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').Trim();
        }
    }

    internal static class BlueprintGraphToolkitReflection
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void CreateNode(BlueprintVisualGraph graph, BlueprintVisualNode node, Vector2 position)
        {
            object implementation = GetGraphImplementation(graph);
            CreateNode(implementation, node, position);
        }

        public static void CreateNodeWithUndo(BlueprintVisualGraph graph, BlueprintVisualNode node, Vector2 position, string undoName)
        {
            object implementation = GetGraphImplementation(graph);
            bool undoRegistered = TryRegisterUndo(implementation, string.IsNullOrEmpty(undoName) ? "Create Blueprint Node" : undoName);
            try
            {
                CreateNode(implementation, node, position);
            }
            finally
            {
                if (undoRegistered)
                {
                    TryEndUndo(implementation);
                }
            }
        }

        private static void CreateNode(object implementation, BlueprintVisualNode node, Vector2 position)
        {
            MethodInfo createNodeMethod = implementation.GetType().GetMethod("CreateNodeModel", Flags, null, new[] { typeof(Node), typeof(Vector2) }, null);
            if (createNodeMethod == null)
            {
                throw new MissingMethodException(implementation.GetType().FullName, "CreateNodeModel");
            }

            createNodeMethod.Invoke(implementation, new object[] { node, position });
        }

        private static bool TryRegisterUndo(object implementation, string undoName)
        {
            MethodInfo registerUndoMethod = FindMethod(implementation.GetType(), "RegisterUndo", method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(string);
            });

            if (registerUndoMethod == null)
            {
                return false;
            }

            registerUndoMethod.Invoke(implementation, new object[] { undoName });
            return true;
        }

        private static void TryEndUndo(object implementation)
        {
            MethodInfo endUndoMethod = FindMethod(implementation.GetType(), "EndUndo", method => method.GetParameters().Length == 0);
            if (endUndoMethod != null)
            {
                endUndoMethod.Invoke(implementation, null);
            }
        }

        public static void CreateWire(BlueprintVisualGraph graph, IPort inputPort, IPort outputPort)
        {
            object implementation = GetGraphImplementation(graph);
            MethodInfo createWireMethod = FindMethod(implementation.GetType(), "CreateWire", method => method.GetParameters().Length == 3);

            if (createWireMethod == null)
            {
                throw new MissingMethodException(implementation.GetType().FullName, "CreateWire");
            }

            createWireMethod.Invoke(implementation, new object[] { inputPort, outputPort, new Hash128() });
        }

        public static bool TryGetNodePosition(BlueprintVisualNode node, out Vector2 position)
        {
            return TryGetNodePosition((INode)node, out position);
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

        public static IVariable CreateBlackboardVariable(BlueprintVisualGraph graph, string name, Type valueType, object defaultValue)
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

        public static INode CreateBlackboardVariableNode(BlueprintVisualGraph graph, IVariable variable, Vector2 position)
        {
            if (variable == null)
            {
                throw new ArgumentNullException("variable");
            }

            object implementation = GetGraphImplementation(graph);
            return CreateBlackboardVariableNode(implementation, variable, position);
        }

        public static INode CreateBlackboardVariableNodeWithUndo(BlueprintVisualGraph graph, IVariable variable, Vector2 position, string undoName)
        {
            if (variable == null)
            {
                throw new ArgumentNullException("variable");
            }

            object implementation = GetGraphImplementation(graph);
            bool undoRegistered = TryRegisterUndo(implementation, string.IsNullOrEmpty(undoName) ? "Create Blueprint Variable Node" : undoName);
            try
            {
                return CreateBlackboardVariableNode(implementation, variable, position);
            }
            finally
            {
                if (undoRegistered)
                {
                    TryEndUndo(implementation);
                }
            }
        }

        private static INode CreateBlackboardVariableNode(object implementation, IVariable variable, Vector2 position)
        {
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

        public static void EnsureSupportedVariableTypes(BlueprintVisualGraph graph, IEnumerable<Type> supportedTypes)
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

        public static void MarkDirty(BlueprintVisualGraph graph)
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
