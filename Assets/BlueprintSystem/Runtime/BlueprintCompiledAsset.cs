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

            for (int i = 0; i < nodes.Count; i++)
            {
                BlueprintCompiledNode compiled = nodes[i];
                if (compiled == null || string.IsNullOrEmpty(compiled.Id))
                {
                    continue;
                }

                RuntimeNode node = new RuntimeNode();
                node.Id = compiled.Id;
                node.TypeId = compiled.TypeId;
                node.Manifest = null;
                node.CompiledTarget = CloneCompiledTarget(compiled.Target);

                IBlueprintNodeExecutor executor;
                if (!string.IsNullOrEmpty(compiled.ExecutorId) && registry.TryGet(compiled.ExecutorId, out executor))
                {
                    node.Executor = executor;
                }

                for (int p = 0; p < compiled.Properties.Count; p++)
                {
                    BlueprintCompiledProperty property = compiled.Properties[p];
                    if (property == null || string.IsNullOrEmpty(property.Id))
                    {
                        continue;
                    }

                    node.Properties[property.Id] = DeserializeValue(property.JsonValue);
                }

                runtime.NodesById[node.Id] = node;
            }

            for (int i = 0; i < execEdges.Count; i++)
            {
                RuntimeEdge edge;
                if (!TryCreateRuntimeEdge(execEdges[i], out edge))
                {
                    continue;
                }

                List<RuntimeEdge> list;
                if (!runtime.ExecOutputs.TryGetValue(edge.From, out list))
                {
                    list = new List<RuntimeEdge>();
                    runtime.ExecOutputs[edge.From] = list;
                }

                list.Add(edge);
            }

            for (int i = 0; i < valueEdges.Count; i++)
            {
                RuntimeEdge edge;
                if (!TryCreateRuntimeEdge(valueEdges[i], out edge))
                {
                    continue;
                }

                runtime.ValueInputs[edge.To] = edge;
            }

            for (int i = 0; i < eventEntries.Count; i++)
            {
                BlueprintCompiledEventEntry entry = eventEntries[i];
                if (entry == null || string.IsNullOrEmpty(entry.EventName) || string.IsNullOrEmpty(entry.NodeId))
                {
                    continue;
                }

                runtime.EventEntries[entry.EventName] = entry.NodeId;
            }

            return runtime;
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
}
