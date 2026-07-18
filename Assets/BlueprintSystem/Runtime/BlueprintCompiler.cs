using System.Collections.Generic;

namespace BlueprintSystem
{
    public sealed class BlueprintCompiler
    {
        public BlueprintCompileResult Compile(BlueprintSource source, BlueprintNodeManifestCollection manifests, BlueprintExecutorRegistry registry)
        {
            BlueprintCompileResult result = new BlueprintCompileResult();
            manifests = manifests ?? new BlueprintNodeManifestCollection();
            registry = registry ?? BlueprintExecutorRegistry.CreateDefault();
            MigrateLegacyBindButtonClickEvents(source);
            BlueprintVariableIdUtility.EnsureVariableIds(source);

            BlueprintValidator validator = new BlueprintValidator();
            result.Diagnostics.AddRange(validator.Validate(source, manifests, registry));
            if (result.Diagnostics.HasErrors)
            {
                return result;
            }

            RuntimeBlueprint runtime = new RuntimeBlueprint();
            runtime.Name = source.Name;
            runtime.Variables.AddRange(source.Variables);
            runtime.Bindings.AddRange(source.Bindings);
            runtime.Components.AddRange(source.Components);

            for (int i = 0; i < runtime.Variables.Count; i++)
            {
                BlueprintVariableDeclaration variable = runtime.Variables[i];
                if (variable != null) variable.CompiledLayoutConstantIndex = AddRuntimeStructLayout(runtime, variable.Type);
            }

            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BlueprintNodeSource sourceNode = source.Nodes[i];
                BlueprintNodeManifest manifest;
                manifests.TryGet(sourceNode.TypeId, out manifest);

                IBlueprintNodeExecutor executor;
                registry.TryGet(manifest.Executor, out executor);

                RuntimeNode runtimeNode = new RuntimeNode();
                runtimeNode.Id = sourceNode.Id;
                runtimeNode.StableId = BlueprintStableId.FromString(sourceNode.Id);
                runtimeNode.TypeId = sourceNode.TypeId;
                runtimeNode.Manifest = manifest;
                runtimeNode.Executor = executor;
                runtimeNode.ExecutorOpcode = BlueprintStableId.FromString(manifest.Executor);
                runtimeNode.CompiledTarget = BlueprintCompiledTargetUtility.Create(source, sourceNode);
                if (runtimeNode.CompiledTarget != null)
                {
                    runtimeNode.CompiledTargetConstantIndex = AddRuntimeConstant(
                        runtime,
                        sourceNode.Id + ".blueprintTarget",
                        "BlueprintTarget",
                        runtimeNode.CompiledTarget);
                }
                foreach (KeyValuePair<string, object> pair in sourceNode.Properties)
                {
                    int constantIndex = AddRuntimeConstant(runtime, sourceNode.Id + "." + pair.Key, "Property", pair.Value);
                    runtimeNode.Properties.Set(pair.Key, pair.Value, constantIndex);
                    runtimeNode.InputRecords.Add(new RuntimeInputRecord
                    {
                        PortStableId = BlueprintStableId.FromString(pair.Key),
                        DebugPortId = pair.Key,
                        ConstantIndex = constantIndex
                    });
                }
                if (manifest.Executor == "Variable.Get" || manifest.Executor == "Variable.Set")
                {
                    object variableName;
                    if (sourceNode.Properties.TryGetValue("name", out variableName))
                    {
                        runtimeNode.VariableIndex = FindVariableIndex(source, variableName == null ? null : variableName.ToString());
                    }
                }
                if (manifest.Executor == "SmartObject.FindBest" || manifest.Executor == "SmartObject.FindBestActor")
                {
                    runtimeNode.SpecializedConstantIndex = AddRuntimeConstant(
                        runtime,
                        sourceNode.Id + ".smartObjectQuery",
                        "SmartObjectQuery",
                        CompiledSmartObjectQueryDescription.Create(runtimeNode));
                }

                runtime.NodeRecords.Add(runtimeNode);

                if (BlueprintEventUtility.IsEventNode(manifest))
                {
                    string eventName = BlueprintEventUtility.GetEventName(sourceNode);
                    runtime.EventRecords.Add(new RuntimeEventRecord
                    {
                        StableId = BlueprintStableId.FromString(eventName),
                        DebugName = eventName,
                        NodeIndex = runtimeNode.StableIndex,
                        DebugNodeId = runtimeNode.Id
                    });
                }
            }

            for (int i = 0; i < source.Edges.Count; i++)
            {
                BlueprintPortKey from;
                BlueprintPortKey to;
                if (!BlueprintPortKey.TryParse(source.Edges[i].From, out from) || !BlueprintPortKey.TryParse(source.Edges[i].To, out to))
                {
                    continue;
                }

                RuntimeNode fromNode = runtime.GetNode(from.NodeId);
                RuntimeNode toNode = runtime.GetNode(to.NodeId);
                BlueprintPortSpec output = fromNode.Manifest.FindOutput(from.PortId);
                if (output == null && fromNode.TypeId == BlueprintBreakStructNodeUtility.NodeTypeId)
                {
                    BlueprintBreakStructNodeUtility.TryCreateOutputPort(fromNode.Properties, from.PortId, out output);
                }

                if (output == null)
                {
                    continue;
                }

                if (output.Kind == BlueprintPortKind.Exec)
                {
                    RuntimeExecOutputRecord execOutput = fromNode.FindExecOutput(BlueprintStableId.FromString(from.PortId));
                    if (execOutput == null)
                    {
                        execOutput = new RuntimeExecOutputRecord
                        {
                            PortStableId = BlueprintStableId.FromString(from.PortId),
                            DebugPortId = from.PortId
                        };
                        fromNode.ExecOutputRecords.Add(execOutput);
                    }

                    execOutput.Targets.Add(new RuntimeExecTargetRecord
                    {
                        NodeIndex = toNode.StableIndex,
                        InputPortStableId = BlueprintStableId.FromString(to.PortId),
                        DebugInputPortId = to.PortId
                    });
                }
                else
                {
                    int inputId = BlueprintStableId.FromString(to.PortId);
                    RuntimeInputRecord input = toNode.FindInput(inputId);
                    if (input == null)
                    {
                        input = new RuntimeInputRecord { PortStableId = inputId, DebugPortId = to.PortId };
                        toNode.InputRecords.Add(input);
                    }

                    input.SourceNodeIndex = fromNode.StableIndex;
                    input.SourcePortStableId = BlueprintStableId.FromString(from.PortId);
                    input.DebugSourcePortId = from.PortId;
                }
            }

            result.Blueprint = runtime;
            return result;
        }

        private static int AddRuntimeConstant(RuntimeBlueprint runtime, string stableName, string kind, object value)
        {
            int index = runtime.ConstantPool.Count;
            runtime.ConstantPool.Add(new RuntimeConstantRecord
            {
                StableId = BlueprintStableId.FromString(stableName),
                Kind = kind,
                Value = value
            });
            return index;
        }

        private static int AddRuntimeStructLayout(RuntimeBlueprint runtime, string variableType)
        {
            string structType = variableType;
            string elementType;
            if (BlueprintArrayUtility.TryGetElementType(variableType, out elementType)) structType = elementType;

            CompiledStructLayout layout;
            if (!BlueprintUserStructRegistry.TryGetLayout(structType, out layout)) return -1;

            int stableId = BlueprintStableId.FromString("structLayout." + structType);
            for (int i = 0; i < runtime.ConstantPool.Count; i++)
            {
                RuntimeConstantRecord existing = runtime.ConstantPool[i];
                if (existing.StableId == stableId && existing.Kind == "StructLayout") return i;
            }

            return AddRuntimeConstant(runtime, "structLayout." + structType, "StructLayout", layout);
        }

        private static int FindVariableIndex(BlueprintSource source, string variableName)
        {
            if (source == null || string.IsNullOrEmpty(variableName)) return -1;
            int stableId = BlueprintStableId.FromString(variableName);
            for (int i = 0; i < source.Variables.Count; i++)
            {
                BlueprintVariableDeclaration variable = source.Variables[i];
                if (variable != null && BlueprintStableId.FromString(variable.Name) == stableId && variable.Name == variableName) return i;
            }
            return -1;
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

        public BlueprintCompileResult CompileJson(string blueprintJson, IEnumerable<string> manifestJsonTexts, BlueprintExecutorRegistry registry)
        {
            BlueprintCompileResult result = new BlueprintCompileResult();
            try
            {
                BlueprintSource source = BlueprintSource.FromJson(blueprintJson);
                BlueprintNodeManifestCollection manifests = BlueprintNodeManifestCollection.FromJsonTexts(manifestJsonTexts);
                return Compile(source, manifests, registry);
            }
            catch (BlueprintJsonException ex)
            {
                result.Diagnostics.Add(BlueprintDiagnostic.Error("BP010", ex.Message));
                return result;
            }
        }
    }

    public static class BlueprintCompiledTargetUtility
    {
        public static CompiledBlueprintTarget Create(BlueprintSource source, BlueprintNodeSource targetNode)
        {
            if (source == null || targetNode == null || !IsCrossBlueprintTargetNode(targetNode.TypeId))
            {
                return null;
            }

            string targetPath;
            if (!TryGetStaticTargetPath(source, targetNode, out targetPath))
            {
                return null;
            }

            CompiledBlueprintTarget target = new CompiledBlueprintTarget();
            target.SourcePath = NormalizeAssetPath(targetPath);
            for (int i = 0; i < source.Components.Count; i++)
            {
                BlueprintComponentDeclaration component = source.Components[i];
                if (component != null && PathEquals(component.Blueprint, target.SourcePath))
                {
                    target.OwnerTraversal = 0;
                    target.ComponentIndex = i;
                    target.ComponentIndexPath.Add(i);
                    break;
                }
            }

            return target;
        }

        private static bool TryGetStaticTargetPath(BlueprintSource source, BlueprintNodeSource targetNode, out string targetPath)
        {
            targetPath = null;
            BlueprintEdgeSource targetEdge = null;
            string targetPort = targetNode.Id + ".target";
            for (int i = 0; i < source.Edges.Count; i++)
            {
                if (source.Edges[i] != null && source.Edges[i].To == targetPort)
                {
                    targetEdge = source.Edges[i];
                    break;
                }
            }

            if (targetEdge != null)
            {
                BlueprintPortKey from;
                if (!BlueprintPortKey.TryParse(targetEdge.From, out from) || from.PortId != "value")
                {
                    return false;
                }

                BlueprintNodeSource variableGet = null;
                for (int i = 0; i < source.Nodes.Count; i++)
                {
                    if (source.Nodes[i] != null && source.Nodes[i].Id == from.NodeId)
                    {
                        variableGet = source.Nodes[i];
                        break;
                    }
                }

                if (variableGet == null || variableGet.TypeId != "Variable.Get")
                {
                    return false;
                }

                object variableNameValue;
                if (!variableGet.Properties.TryGetValue("name", out variableNameValue) || variableNameValue == null)
                {
                    return false;
                }

                string variableName = variableNameValue.ToString();
                for (int i = 0; i < source.Variables.Count; i++)
                {
                    BlueprintVariableDeclaration variable = source.Variables[i];
                    if (variable != null && variable.Name == variableName &&
                        variable.Type == BlueprintVariableTypeRegistry.BlueprintAssetTypeId)
                    {
                        targetPath = variable.DefaultValue as string;
                        return !string.IsNullOrEmpty(targetPath);
                    }
                }

                return false;
            }

            object propertyValue;
            if (targetNode.Properties.TryGetValue("target", out propertyValue))
            {
                targetPath = propertyValue as string;
            }

            return !string.IsNullOrEmpty(targetPath);
        }

        private static bool IsCrossBlueprintTargetNode(string typeId)
        {
            return typeId == "Blueprint.IsValid" ||
                   typeId == "Blueprint.TriggerEvent" ||
                   typeId == "Blueprint.GetVariable" ||
                   typeId == "Blueprint.SetVariable";
        }

        public static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/').Trim();
        }

        public static bool PathEquals(string left, string right)
        {
            left = NormalizeAssetPath(left);
            right = NormalizeAssetPath(right);
            return !string.IsNullOrEmpty(left) &&
                   !string.IsNullOrEmpty(right) &&
                   string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
