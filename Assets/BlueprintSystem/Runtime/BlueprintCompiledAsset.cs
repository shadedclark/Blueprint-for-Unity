using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    [CreateAssetMenu(menuName = "Blueprint System/Compiled Blueprint", fileName = "BlueprintCompiledAsset")]
    public sealed class BlueprintCompiledAsset : ScriptableObject
    {
        [SerializeField] private string schemaVersion = "0.1";
        [SerializeField] private string blueprintName;
        [SerializeField] private string sourceGuid;
        [SerializeField] private string sourcePath;
        [SerializeField] private string sourceHash;
        [SerializeField] private string manifestHash;
        [SerializeField] private List<BlueprintCompiledVariable> variables = new List<BlueprintCompiledVariable>();
        [SerializeField] private List<BlueprintCompiledBinding> bindings = new List<BlueprintCompiledBinding>();
        [SerializeField] private List<BlueprintCompiledComponent> components = new List<BlueprintCompiledComponent>();
        [SerializeField] private List<BlueprintCompiledNode> nodes = new List<BlueprintCompiledNode>();
        [SerializeField] private List<BlueprintCompiledEdge> execEdges = new List<BlueprintCompiledEdge>();
        [SerializeField] private List<BlueprintCompiledEdge> valueEdges = new List<BlueprintCompiledEdge>();
        [SerializeField] private List<BlueprintCompiledEventEntry> eventEntries = new List<BlueprintCompiledEventEntry>();
        [SerializeField] private List<CompiledConstantRecord> constantPool = new List<CompiledConstantRecord>();
        [SerializeField] private List<CompiledNodeRecord> nodeRecords = new List<CompiledNodeRecord>();
        [SerializeField] private List<CompiledEventRecord> eventRecords = new List<CompiledEventRecord>();

        public string SchemaVersion
        {
            get { return schemaVersion; }
        }

        public string BlueprintName
        {
            get { return blueprintName; }
        }

        public string SourceGuid
        {
            get { return sourceGuid; }
        }

        public string SourcePath
        {
            get { return sourcePath; }
        }

        public string SourceHash
        {
            get { return sourceHash; }
        }

        public string ManifestHash
        {
            get { return manifestHash; }
        }

        public IReadOnlyList<BlueprintCompiledVariable> Variables
        {
            get { return variables; }
        }

        public IReadOnlyList<BlueprintCompiledBinding> Bindings
        {
            get { return bindings; }
        }

        public IReadOnlyList<BlueprintCompiledComponent> Components
        {
            get { return components; }
        }

        public IReadOnlyList<BlueprintCompiledNode> Nodes
        {
            get { return nodes; }
        }

        public IReadOnlyList<BlueprintCompiledEdge> ExecEdges
        {
            get { return execEdges; }
        }

        public IReadOnlyList<BlueprintCompiledEdge> ValueEdges
        {
            get { return valueEdges; }
        }

        public IReadOnlyList<BlueprintCompiledEventEntry> EventEntries
        {
            get { return eventEntries; }
        }

        public IReadOnlyList<CompiledConstantRecord> ConstantPool
        {
            get { return constantPool; }
        }

        public IReadOnlyList<CompiledNodeRecord> NodeRecords
        {
            get { return nodeRecords; }
        }

        public IReadOnlyList<CompiledEventRecord> EventRecords
        {
            get { return eventRecords; }
        }

        public void SetCompiledData(
            string newSchemaVersion,
            string newBlueprintName,
            string newSourceGuid,
            string newSourcePath,
            string newSourceHash,
            string newManifestHash,
            IEnumerable<BlueprintCompiledVariable> newVariables,
            IEnumerable<BlueprintCompiledBinding> newBindings,
            IEnumerable<BlueprintCompiledComponent> newComponents,
            IEnumerable<BlueprintCompiledNode> newNodes,
            IEnumerable<BlueprintCompiledEdge> newExecEdges,
            IEnumerable<BlueprintCompiledEdge> newValueEdges,
            IEnumerable<BlueprintCompiledEventEntry> newEventEntries)
        {
            schemaVersion = string.IsNullOrEmpty(newSchemaVersion) ? "0.1" : newSchemaVersion;
            blueprintName = newBlueprintName;
            sourceGuid = newSourceGuid;
            sourcePath = newSourcePath;
            sourceHash = newSourceHash;
            manifestHash = newManifestHash;
            ReplaceList(variables, newVariables);
            ReplaceList(bindings, newBindings);
            ReplaceList(components, newComponents);
            ReplaceList(nodes, newNodes);
            ReplaceList(execEdges, newExecEdges);
            ReplaceList(valueEdges, newValueEdges);
            ReplaceList(eventEntries, newEventEntries);
            RebuildLoweredRecords();
        }

        public bool IsCurrent(string expectedSourceHash, string expectedManifestHash)
        {
            return !string.IsNullOrEmpty(sourceHash) &&
                   !string.IsNullOrEmpty(manifestHash) &&
                   sourceHash == expectedSourceHash &&
                   manifestHash == expectedManifestHash;
        }

        public RuntimeBlueprint CreateRuntimeBlueprint(BlueprintExecutorRegistry registry)
        {
            registry = registry ?? BlueprintExecutorRegistry.CreateDefault();

            // Assets produced before record lowering are upgraded once at hydration time. Newly
            // compiled assets already serialize these records and skip all string edge resolution.
            if (nodeRecords.Count == 0 && nodes.Count > 0)
            {
                RebuildLoweredRecords();
            }

            RuntimeBlueprint runtime = new RuntimeBlueprint();
            runtime.Name = blueprintName;

            for (int i = 0; i < variables.Count; i++)
            {
                BlueprintCompiledVariable compiled = variables[i];
                if (compiled == null || string.IsNullOrEmpty(compiled.Name))
                {
                    continue;
                }

                runtime.Variables.Add(compiled.ToDeclaration());
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                BlueprintCompiledBinding compiled = bindings[i];
                if (compiled == null || string.IsNullOrEmpty(compiled.Name))
                {
                    continue;
                }

                runtime.Bindings.Add(compiled.ToDeclaration());
            }

            for (int i = 0; i < components.Count; i++)
            {
                BlueprintCompiledComponent compiled = components[i];
                if (compiled == null || string.IsNullOrEmpty(compiled.Name))
                {
                    continue;
                }

                runtime.Components.Add(compiled.ToDeclaration());
            }

            for (int i = 0; i < constantPool.Count; i++)
            {
                CompiledConstantRecord compiled = constantPool[i];
                if (compiled == null)
                {
                    runtime.ConstantPool.Add(new RuntimeConstantRecord());
                    continue;
                }

                runtime.ConstantPool.Add(new RuntimeConstantRecord
                {
                    StableId = compiled.StableId,
                    Kind = compiled.Kind,
                    Value = DeserializeCompiledConstant(compiled)
                });
            }

            for (int i = 0; i < nodeRecords.Count; i++)
            {
                CompiledNodeRecord compiled = nodeRecords[i];
                if (compiled == null)
                {
                    continue;
                }

                RuntimeNode node = new RuntimeNode
                {
                    StableIndex = compiled.StableIndex,
                    StableId = compiled.StableId,
                    ExecutorOpcode = compiled.ExecutorOpcode,
                    VariableIndex = compiled.VariableIndex,
                    Id = compiled.DebugNodeId,
                    TypeId = compiled.ExecutorType,
                    Manifest = null,
                    CompiledTargetConstantIndex = compiled.BlueprintTargetConstantIndex,
                    SpecializedConstantIndex = compiled.SpecializedConstantIndex
                };

                IBlueprintNodeExecutor executor;
                if (registry.TryGet(compiled.ExecutorOpcode, out executor))
                {
                    node.Executor = executor;
                }

                for (int p = 0; p < compiled.Properties.Count; p++)
                {
                    CompiledPropertyRecord property = compiled.Properties[p];
                    if (property == null) continue;
                    node.Properties.Set(property.DebugPropertyId, runtime.GetConstant(property.ConstantIndex), property.ConstantIndex);
                }

                for (int inputIndex = 0; inputIndex < compiled.Inputs.Count; inputIndex++)
                {
                    CompiledInputRecord input = compiled.Inputs[inputIndex];
                    if (input == null) continue;
                    node.InputRecords.Add(new RuntimeInputRecord
                    {
                        PortStableId = input.PortStableId,
                        DebugPortId = input.DebugPortId,
                        SourceNodeIndex = input.SourceNodeIndex,
                        SourcePortStableId = input.SourcePortStableId,
                        DebugSourcePortId = input.DebugSourcePortId,
                        ConstantIndex = input.ConstantIndex
                    });
                }

                for (int outputIndex = 0; outputIndex < compiled.ExecOutputs.Count; outputIndex++)
                {
                    CompiledExecOutputRecord compiledOutput = compiled.ExecOutputs[outputIndex];
                    if (compiledOutput == null) continue;
                    RuntimeExecOutputRecord output = new RuntimeExecOutputRecord
                    {
                        PortStableId = compiledOutput.PortStableId,
                        DebugPortId = compiledOutput.DebugPortId
                    };
                    for (int targetIndex = 0; targetIndex < compiledOutput.Targets.Count; targetIndex++)
                    {
                        CompiledExecTargetRecord target = compiledOutput.Targets[targetIndex];
                        if (target == null) continue;
                        output.Targets.Add(new RuntimeExecTargetRecord
                        {
                            NodeIndex = target.NodeIndex,
                            InputPortStableId = target.InputPortStableId,
                            DebugInputPortId = target.DebugInputPortId
                        });
                    }
                    node.ExecOutputRecords.Add(output);
                }

                node.CompiledTarget = runtime.GetConstant(node.CompiledTargetConstantIndex) as CompiledBlueprintTarget;
                runtime.NodeRecords.Add(node);
            }

            for (int i = 0; i < eventRecords.Count; i++)
            {
                CompiledEventRecord entry = eventRecords[i];
                if (entry == null) continue;
                runtime.EventRecords.Add(new RuntimeEventRecord
                {
                    StableId = entry.StableId,
                    DebugName = entry.DebugEventName,
                    NodeIndex = entry.NodeIndex,
                    DebugNodeId = entry.DebugNodeId
                });
            }

            return runtime;
        }

        private void RebuildLoweredRecords()
        {
            constantPool.Clear();
            nodeRecords.Clear();
            eventRecords.Clear();

            for (int i = 0; i < variables.Count; i++)
            {
                BlueprintCompiledVariable variable = variables[i];
                if (variable == null) continue;
                variable.CompiledLayoutConstantIndex = AddStructLayoutConstant(variable.Type);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                BlueprintCompiledNode sourceNode = nodes[i];
                if (sourceNode == null || string.IsNullOrEmpty(sourceNode.Id))
                {
                    continue;
                }

                CompiledNodeRecord record = new CompiledNodeRecord
                {
                    StableIndex = nodeRecords.Count,
                    StableId = BlueprintStableId.FromString(sourceNode.Id),
                    DebugNodeId = sourceNode.Id,
                    ExecutorOpcode = BlueprintStableId.FromString(sourceNode.ExecutorId),
                    ExecutorType = sourceNode.TypeId
                };

                if (sourceNode.Target != null)
                {
                    record.BlueprintTargetConstantIndex = AddCompiledConstant(
                        sourceNode.Id + ".blueprintTarget",
                        "BlueprintTarget",
                        null,
                        sourceNode.Target,
                        null);
                }

                for (int p = 0; p < sourceNode.Properties.Count; p++)
                {
                    BlueprintCompiledProperty property = sourceNode.Properties[p];
                    if (property == null || string.IsNullOrEmpty(property.Id)) continue;
                    int constantIndex = AddCompiledConstant(
                        sourceNode.Id + "." + property.Id,
                        IsAssetPathProperty(property.Id, property.JsonValue) ? "AssetReference" : "Property",
                        property.JsonValue,
                        null,
                        null);
                    record.Properties.Add(new CompiledPropertyRecord
                    {
                        StableId = BlueprintStableId.FromString(property.Id),
                        DebugPropertyId = property.Id,
                        ConstantIndex = constantIndex
                    });
                    record.Inputs.Add(new CompiledInputRecord
                    {
                        PortStableId = BlueprintStableId.FromString(property.Id),
                        DebugPortId = property.Id,
                        ConstantIndex = constantIndex
                    });
                }

                if (string.Equals(sourceNode.ExecutorId, "Variable.Get", StringComparison.Ordinal) ||
                    string.Equals(sourceNode.ExecutorId, "Variable.Set", StringComparison.Ordinal))
                {
                    object variableName = FindPropertyValue(sourceNode, "name");
                    record.VariableIndex = FindVariableIndex(variableName == null ? null : variableName.ToString());
                }

                if (sourceNode.ExecutorId == "SmartObject.FindBest" || sourceNode.ExecutorId == "SmartObject.FindBestActor")
                {
                    CompiledSmartObjectQueryDescription query = CompiledSmartObjectQueryDescription.Create(record);
                    record.SpecializedConstantIndex = AddCompiledConstant(
                        sourceNode.Id + ".smartObjectQuery",
                        "SmartObjectQuery",
                        null,
                        null,
                        query);
                }

                nodeRecords.Add(record);
            }

            for (int i = 0; i < valueEdges.Count; i++)
            {
                BlueprintCompiledEdge edge = valueEdges[i];
                int sourceIndex = FindNodeIndex(edge == null ? null : edge.FromNodeId);
                int targetIndex = FindNodeIndex(edge == null ? null : edge.ToNodeId);
                if (sourceIndex < 0 || targetIndex < 0 || string.IsNullOrEmpty(edge.ToPortId)) continue;
                CompiledNodeRecord targetNode = nodeRecords[targetIndex];
                int portId = BlueprintStableId.FromString(edge.ToPortId);
                CompiledInputRecord input = FindInput(targetNode, portId);
                if (input == null)
                {
                    input = new CompiledInputRecord { PortStableId = portId, DebugPortId = edge.ToPortId };
                    targetNode.Inputs.Add(input);
                }
                input.SourceNodeIndex = sourceIndex;
                input.SourcePortStableId = BlueprintStableId.FromString(edge.FromPortId);
                input.DebugSourcePortId = edge.FromPortId;
            }

            for (int i = 0; i < execEdges.Count; i++)
            {
                BlueprintCompiledEdge edge = execEdges[i];
                int sourceIndex = FindNodeIndex(edge == null ? null : edge.FromNodeId);
                int targetIndex = FindNodeIndex(edge == null ? null : edge.ToNodeId);
                if (sourceIndex < 0 || targetIndex < 0 || string.IsNullOrEmpty(edge.FromPortId)) continue;
                CompiledNodeRecord sourceNode = nodeRecords[sourceIndex];
                int portId = BlueprintStableId.FromString(edge.FromPortId);
                CompiledExecOutputRecord output = FindExecOutput(sourceNode, portId);
                if (output == null)
                {
                    output = new CompiledExecOutputRecord { PortStableId = portId, DebugPortId = edge.FromPortId };
                    sourceNode.ExecOutputs.Add(output);
                }
                output.Targets.Add(new CompiledExecTargetRecord
                {
                    NodeIndex = targetIndex,
                    InputPortStableId = BlueprintStableId.FromString(edge.ToPortId),
                    DebugInputPortId = edge.ToPortId
                });
            }

            for (int i = 0; i < eventEntries.Count; i++)
            {
                BlueprintCompiledEventEntry entry = eventEntries[i];
                int nodeIndex = FindNodeIndex(entry == null ? null : entry.NodeId);
                if (entry == null || string.IsNullOrEmpty(entry.EventName) || nodeIndex < 0) continue;
                eventRecords.Add(new CompiledEventRecord
                {
                    StableId = BlueprintStableId.FromString(entry.EventName),
                    DebugEventName = entry.EventName,
                    NodeIndex = nodeIndex,
                    DebugNodeId = entry.NodeId
                });
            }
        }

        private int AddCompiledConstant(
            string stableName,
            string kind,
            string jsonValue,
            CompiledBlueprintTarget target,
            CompiledSmartObjectQueryDescription query)
        {
            int index = constantPool.Count;
            constantPool.Add(new CompiledConstantRecord
            {
                StableIndex = index,
                StableId = BlueprintStableId.FromString(stableName),
                Kind = kind,
                JsonValue = jsonValue,
                BlueprintTarget = CloneCompiledTarget(target),
                SmartObjectQuery = query
            });
            return index;
        }

        private int AddStructLayoutConstant(string variableType)
        {
            string structType = variableType;
            string elementType;
            if (BlueprintArrayUtility.TryGetElementType(variableType, out elementType)) structType = elementType;

            CompiledStructLayout layout;
            if (!BlueprintUserStructRegistry.TryGetLayout(structType, out layout)) return -1;

            int stableId = BlueprintStableId.FromString("structLayout." + structType);
            for (int i = 0; i < constantPool.Count; i++)
            {
                CompiledConstantRecord existing = constantPool[i];
                if (existing != null && existing.StableId == stableId && existing.Kind == "StructLayout") return i;
            }

            int index = constantPool.Count;
            constantPool.Add(new CompiledConstantRecord
            {
                StableIndex = index,
                StableId = stableId,
                Kind = "StructLayout",
                StructLayout = CompiledStructLayoutRecord.Create(layout)
            });
            return index;
        }

        private static object DeserializeCompiledConstant(CompiledConstantRecord compiled)
        {
            if (string.Equals(compiled.Kind, "BlueprintTarget", StringComparison.Ordinal))
            {
                return CloneCompiledTarget(compiled.BlueprintTarget);
            }
            if (string.Equals(compiled.Kind, "SmartObjectQuery", StringComparison.Ordinal))
            {
                return compiled.SmartObjectQuery == null ? null : compiled.SmartObjectQuery.Clone();
            }
            if (string.Equals(compiled.Kind, "StructLayout", StringComparison.Ordinal))
            {
                return compiled.StructLayout == null ? null : compiled.StructLayout.ToLayout();
            }
            return DeserializeValue(compiled.JsonValue);
        }

        private int FindNodeIndex(string nodeId)
        {
            int stableId = BlueprintStableId.FromString(nodeId);
            for (int i = 0; i < nodeRecords.Count; i++)
            {
                if (nodeRecords[i].StableId == stableId && string.Equals(nodeRecords[i].DebugNodeId, nodeId, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private int FindVariableIndex(string variableName)
        {
            int stableId = BlueprintStableId.FromString(variableName);
            for (int i = 0; i < variables.Count; i++)
            {
                BlueprintCompiledVariable variable = variables[i];
                if (variable != null && BlueprintStableId.FromString(variable.Name) == stableId && string.Equals(variable.Name, variableName, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private static CompiledInputRecord FindInput(CompiledNodeRecord node, int portStableId)
        {
            for (int i = 0; i < node.Inputs.Count; i++) if (node.Inputs[i].PortStableId == portStableId) return node.Inputs[i];
            return null;
        }

        private static CompiledExecOutputRecord FindExecOutput(CompiledNodeRecord node, int portStableId)
        {
            for (int i = 0; i < node.ExecOutputs.Count; i++) if (node.ExecOutputs[i].PortStableId == portStableId) return node.ExecOutputs[i];
            return null;
        }

        private static object FindPropertyValue(BlueprintCompiledNode node, string propertyId)
        {
            for (int i = 0; i < node.Properties.Count; i++)
            {
                BlueprintCompiledProperty property = node.Properties[i];
                if (property != null && string.Equals(property.Id, propertyId, StringComparison.Ordinal)) return DeserializeValue(property.JsonValue);
            }
            return null;
        }

        private static bool IsAssetPathProperty(string propertyId, string jsonValue)
        {
            if (string.IsNullOrEmpty(jsonValue)) return false;
            return propertyId.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   propertyId.IndexOf("asset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   propertyId.IndexOf("prefab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   jsonValue.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static CompiledBlueprintTarget CloneCompiledTarget(CompiledBlueprintTarget source)
        {
            if (source == null)
            {
                return null;
            }

            return new CompiledBlueprintTarget
            {
                OwnerTraversal = source.OwnerTraversal,
                ComponentIndex = source.ComponentIndex,
                ComponentIndexPath = source.ComponentIndexPath == null
                    ? new List<int>()
                    : new List<int>(source.ComponentIndexPath),
                ExpectedSourceGuid = source.ExpectedSourceGuid,
                SourcePath = source.SourcePath
            };
        }

        private static void ReplaceList<T>(List<T> target, IEnumerable<T> source)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            target.AddRange(source);
        }

        private static bool TryCreateRuntimeEdge(BlueprintCompiledEdge compiled, out RuntimeEdge edge)
        {
            edge = null;
            if (compiled == null)
            {
                return false;
            }

            BlueprintPortKey from = new BlueprintPortKey(compiled.FromNodeId, compiled.FromPortId);
            BlueprintPortKey to = new BlueprintPortKey(compiled.ToNodeId, compiled.ToPortId);
            if (!from.IsValid || !to.IsValid)
            {
                return false;
            }

            edge = new RuntimeEdge(from, to);
            return true;
        }

        private static object DeserializeValue(string json)
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

    [Serializable]
    public sealed class BlueprintCompiledVariable
    {
        public string Id;
        public string Name;
        public string Type;
        public string DefaultValueJson;
        public string Scope;
        public bool Exposed;
        public bool Persistent;
        public string Description;
        public int CompiledLayoutConstantIndex = -1;

        public BlueprintVariableDeclaration ToDeclaration()
        {
            BlueprintVariableDeclaration declaration = new BlueprintVariableDeclaration();
            declaration.Id = Id;
            declaration.Name = Name;
            declaration.Type = Type;
            declaration.DefaultValue = DeserializeValue(DefaultValueJson);
            declaration.Scope = Scope;
            declaration.Exposed = Exposed;
            declaration.Persistent = Persistent;
            declaration.Description = Description;
            declaration.CompiledLayoutConstantIndex = CompiledLayoutConstantIndex;
            return declaration;
        }

        private static object DeserializeValue(string json)
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

    [Serializable]
    public sealed class BlueprintCompiledBinding
    {
        public string Name;
        public string Type;
        public bool Required;

        public BlueprintBindingDeclaration ToDeclaration()
        {
            BlueprintBindingDeclaration declaration = new BlueprintBindingDeclaration();
            declaration.Name = Name;
            declaration.Type = Type;
            declaration.Required = Required;
            return declaration;
        }
    }

    [Serializable]
    public sealed class BlueprintCompiledComponent
    {
        public string Name;
        public string BlueprintPath;
        public string BlueprintGuid;
        public bool Required;
        public BlueprintCompiledAsset CompiledBlueprint;

        public BlueprintComponentDeclaration ToDeclaration()
        {
            BlueprintComponentDeclaration declaration = new BlueprintComponentDeclaration();
            declaration.Name = Name;
            declaration.Blueprint = BlueprintPath;
            declaration.Required = Required;
            declaration.CompiledBlueprint = CompiledBlueprint;
            return declaration;
        }
    }

    [Serializable]
    public sealed class BlueprintCompiledNode
    {
        public string Id;
        public string TypeId;
        public string ExecutorId;
        public CompiledBlueprintTarget Target;
        public List<BlueprintCompiledProperty> Properties = new List<BlueprintCompiledProperty>();
    }

    [Serializable]
    public sealed class BlueprintCompiledProperty
    {
        public string Id;
        public string JsonValue;
    }

    [Serializable]
    public sealed class BlueprintCompiledEdge
    {
        public string FromNodeId;
        public string FromPortId;
        public string ToNodeId;
        public string ToPortId;
    }

    [Serializable]
    public sealed class BlueprintCompiledEventEntry
    {
        public string EventName;
        public string NodeId;
    }

    [Serializable]
    public sealed class CompiledConstantRecord
    {
        public int StableIndex;
        public int StableId;
        public string Kind;
        public string JsonValue;
        public CompiledBlueprintTarget BlueprintTarget;
        public CompiledSmartObjectQueryDescription SmartObjectQuery;
        public CompiledStructLayoutRecord StructLayout;
    }

    [Serializable]
    public sealed class CompiledStructLayoutRecord
    {
        public int TypeStableId;
        public string TypeId;
        public List<CompiledStructFieldRecord> Fields = new List<CompiledStructFieldRecord>();

        public static CompiledStructLayoutRecord Create(CompiledStructLayout layout)
        {
            if (layout == null) return null;
            CompiledStructLayoutRecord record = new CompiledStructLayoutRecord
            {
                TypeStableId = BlueprintStableId.FromString(layout.TypeId),
                TypeId = layout.TypeId
            };
            for (int i = 0; i < layout.FieldCount; i++)
            {
                BlueprintUserStructField field;
                if (!layout.TryGetFieldDefinition(i, out field) || field == null) continue;
                record.Fields.Add(new CompiledStructFieldRecord
                {
                    StableIndex = i,
                    IdStableId = BlueprintStableId.FromString(field.Id),
                    NameStableId = BlueprintStableId.FromString(field.Name),
                    Id = field.Id,
                    Name = field.Name,
                    Type = field.Type,
                    DefaultValueJson = BlueprintJson.Serialize(field.DefaultValue, false),
                    Description = field.Description,
                    Deprecated = field.Deprecated
                });
            }
            return record;
        }

        public CompiledStructLayout ToLayout()
        {
            BlueprintUserStructDefinition definition = new BlueprintUserStructDefinition { TypeId = TypeId };
            for (int i = 0; i < Fields.Count; i++)
            {
                CompiledStructFieldRecord field = Fields[i];
                if (field == null) continue;
                definition.Fields.Add(new BlueprintUserStructField
                {
                    Id = field.Id,
                    Name = field.Name,
                    Type = field.Type,
                    DefaultValue = DeserializeValue(field.DefaultValueJson),
                    Description = field.Description,
                    Deprecated = field.Deprecated
                });
            }
            return new CompiledStructLayout(definition);
        }

        private static object DeserializeValue(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return BlueprintJson.Deserialize(json); }
            catch (BlueprintJsonException) { return null; }
        }
    }

    [Serializable]
    public sealed class CompiledStructFieldRecord
    {
        public int StableIndex;
        public int IdStableId;
        public int NameStableId;
        public string Id;
        public string Name;
        public string Type;
        public string DefaultValueJson;
        public string Description;
        public bool Deprecated;
    }

    [Serializable]
    public sealed class CompiledNodeRecord
    {
        public int StableIndex;
        public int StableId;
        public string DebugNodeId;
        public int ExecutorOpcode;
        public string ExecutorType;
        public int VariableIndex = -1;
        public int BlueprintTargetConstantIndex = -1;
        public int SpecializedConstantIndex = -1;
        public List<CompiledPropertyRecord> Properties = new List<CompiledPropertyRecord>();
        public List<CompiledInputRecord> Inputs = new List<CompiledInputRecord>();
        public List<CompiledExecOutputRecord> ExecOutputs = new List<CompiledExecOutputRecord>();
    }

    [Serializable]
    public sealed class CompiledPropertyRecord
    {
        public int StableId;
        public string DebugPropertyId;
        public int ConstantIndex = -1;
    }

    [Serializable]
    public sealed class CompiledInputRecord
    {
        public int PortStableId;
        public string DebugPortId;
        public int SourceNodeIndex = -1;
        public int SourcePortStableId;
        public string DebugSourcePortId;
        public int ConstantIndex = -1;
    }

    [Serializable]
    public sealed class CompiledExecOutputRecord
    {
        public int PortStableId;
        public string DebugPortId;
        public List<CompiledExecTargetRecord> Targets = new List<CompiledExecTargetRecord>();
    }

    [Serializable]
    public sealed class CompiledExecTargetRecord
    {
        public int NodeIndex = -1;
        public int InputPortStableId;
        public string DebugInputPortId;
    }

    [Serializable]
    public sealed class CompiledEventRecord
    {
        public int StableId;
        public string DebugEventName;
        public int NodeIndex = -1;
        public string DebugNodeId;
    }

    [Serializable]
    public sealed class CompiledSmartObjectQueryDescription
    {
        public List<CompiledSmartObjectQueryInputRecord> Inputs = new List<CompiledSmartObjectQueryInputRecord>();

        public static CompiledSmartObjectQueryDescription Create(CompiledNodeRecord node)
        {
            CompiledSmartObjectQueryDescription description = new CompiledSmartObjectQueryDescription();
            if (node == null) return description;
            for (int i = 0; i < node.Inputs.Count; i++)
            {
                CompiledInputRecord input = node.Inputs[i];
                description.Inputs.Add(new CompiledSmartObjectQueryInputRecord
                {
                    PortStableId = input.PortStableId,
                    InputRecordIndex = i
                });
            }
            return description;
        }

        public static CompiledSmartObjectQueryDescription Create(RuntimeNode node)
        {
            CompiledSmartObjectQueryDescription description = new CompiledSmartObjectQueryDescription();
            if (node == null) return description;
            for (int i = 0; i < node.InputRecords.Count; i++)
            {
                description.Inputs.Add(new CompiledSmartObjectQueryInputRecord
                {
                    PortStableId = node.InputRecords[i].PortStableId,
                    InputRecordIndex = i
                });
            }
            return description;
        }

        public CompiledSmartObjectQueryDescription Clone()
        {
            CompiledSmartObjectQueryDescription clone = new CompiledSmartObjectQueryDescription();
            for (int i = 0; i < Inputs.Count; i++)
            {
                clone.Inputs.Add(new CompiledSmartObjectQueryInputRecord
                {
                    PortStableId = Inputs[i].PortStableId,
                    InputRecordIndex = Inputs[i].InputRecordIndex
                });
            }
            return clone;
        }
    }

    [Serializable]
    public sealed class CompiledSmartObjectQueryInputRecord
    {
        public int PortStableId;
        public int InputRecordIndex = -1;
    }
}
