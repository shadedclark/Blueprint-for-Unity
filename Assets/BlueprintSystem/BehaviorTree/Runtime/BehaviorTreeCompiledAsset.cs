using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    [CreateAssetMenu(menuName = "Blueprint System/Compiled Behavior Tree", fileName = "BehaviorTreeCompiledAsset")]
    public sealed class BehaviorTreeCompiledAsset : ScriptableObject
    {
        [SerializeField] private string schemaVersion = "0.1";
        [SerializeField] private string behaviorTreeName;
        [SerializeField] private string sourceGuid;
        [SerializeField] private string sourcePath;
        [SerializeField] private string sourceHash;
        [SerializeField] private string rootNodeId;
        [SerializeField] private List<BehaviorTreeCompiledBlackboardKey> blackboard = new List<BehaviorTreeCompiledBlackboardKey>();
        [SerializeField] private List<BehaviorTreeCompiledComponent> components = new List<BehaviorTreeCompiledComponent>();
        [SerializeField] private List<BehaviorTreeCompiledNode> nodes = new List<BehaviorTreeCompiledNode>();
        [SerializeField] private List<BehaviorTreeCompiledDecorator> decorators = new List<BehaviorTreeCompiledDecorator>();
        [SerializeField] private List<BehaviorTreeCompiledService> services = new List<BehaviorTreeCompiledService>();

        public string SchemaVersion
        {
            get { return schemaVersion; }
        }

        public string BehaviorTreeName
        {
            get { return behaviorTreeName; }
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

        public string RootNodeId
        {
            get { return rootNodeId; }
        }

        public IReadOnlyList<BehaviorTreeCompiledBlackboardKey> Blackboard
        {
            get { return blackboard; }
        }

        public IReadOnlyList<BehaviorTreeCompiledNode> Nodes
        {
            get { return nodes; }
        }

        public IReadOnlyList<BehaviorTreeCompiledComponent> Components
        {
            get { return components; }
        }

        public IReadOnlyList<BehaviorTreeCompiledDecorator> Decorators
        {
            get { return decorators; }
        }

        public IReadOnlyList<BehaviorTreeCompiledService> Services
        {
            get { return services; }
        }

        public void SetCompiledData(
            string newSchemaVersion,
            string newBehaviorTreeName,
            string newSourceGuid,
            string newSourcePath,
            string newSourceHash,
            string newRootNodeId,
            IEnumerable<BehaviorTreeCompiledBlackboardKey> newBlackboard,
            IEnumerable<BehaviorTreeCompiledComponent> newComponents,
            IEnumerable<BehaviorTreeCompiledNode> newNodes,
            IEnumerable<BehaviorTreeCompiledDecorator> newDecorators,
            IEnumerable<BehaviorTreeCompiledService> newServices)
        {
            schemaVersion = string.IsNullOrEmpty(newSchemaVersion) ? "0.1" : newSchemaVersion;
            behaviorTreeName = newBehaviorTreeName;
            sourceGuid = newSourceGuid;
            sourcePath = newSourcePath;
            sourceHash = newSourceHash;
            rootNodeId = newRootNodeId;
            ReplaceList(blackboard, newBlackboard);
            ReplaceList(components, newComponents);
            ReplaceList(nodes, newNodes);
            ReplaceList(decorators, newDecorators);
            ReplaceList(services, newServices);
        }

        public bool IsCurrent(string expectedSourceHash)
        {
            return !string.IsNullOrEmpty(sourceHash) && sourceHash == expectedSourceHash;
        }

        public RuntimeBehaviorTree CreateRuntimeTree(BehaviorTreeExecutorRegistry registry = null)
        {
            registry = registry ?? BehaviorTreeExecutorRegistry.CreateDefault();
            RuntimeBehaviorTree tree = new RuntimeBehaviorTree();
            tree.Name = behaviorTreeName;
            tree.SourceGuid = sourceGuid;
            tree.SourcePath = sourcePath;
            tree.RootNodeId = rootNodeId;
            tree.Registry = registry;

            for (int i = 0; i < blackboard.Count; i++)
            {
                BehaviorTreeCompiledBlackboardKey compiled = blackboard[i];
                if (compiled == null || string.IsNullOrEmpty(compiled.Name))
                {
                    continue;
                }

                tree.BlackboardSchema.Add(compiled.ToKey());
            }

            for (int i = 0; i < components.Count; i++)
            {
                BehaviorTreeCompiledComponent compiled = components[i];
                if (compiled == null || string.IsNullOrEmpty(compiled.Name))
                {
                    continue;
                }

                tree.ComponentsByName[compiled.Name] = compiled.ToRuntimeComponent();
            }

            for (int i = 0; i < decorators.Count; i++)
            {
                BehaviorTreeCompiledDecorator compiled = decorators[i];
                if (compiled == null || string.IsNullOrEmpty(compiled.Id))
                {
                    continue;
                }

                RuntimeBehaviorTreeDecorator decorator = new RuntimeBehaviorTreeDecorator();
                decorator.Id = compiled.Id;
                decorator.TypeId = compiled.TypeId;
                CopyInputBindings(compiled.Inputs, decorator.Inputs);
                CopyProperties(compiled.Properties, decorator.Properties);
                tree.DecoratorsById[decorator.Id] = decorator;
            }

            for (int i = 0; i < services.Count; i++)
            {
                BehaviorTreeCompiledService compiled = services[i];
                if (compiled == null || string.IsNullOrEmpty(compiled.Id))
                {
                    continue;
                }

                RuntimeBehaviorTreeService service = new RuntimeBehaviorTreeService();
                service.Id = compiled.Id;
                service.TypeId = compiled.TypeId;
                service.Interval = compiled.Interval;
                service.RandomDeviation = compiled.RandomDeviation;
                CopyProperties(compiled.Properties, service.Properties);
                tree.ServicesById[service.Id] = service;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                BehaviorTreeCompiledNode compiled = nodes[i];
                if (compiled == null || string.IsNullOrEmpty(compiled.Id))
                {
                    continue;
                }

                RuntimeBehaviorTreeNode node = new RuntimeBehaviorTreeNode();
                node.Id = compiled.Id;
                node.TypeId = compiled.TypeId;
                node.Children.AddRange(compiled.Children);
                node.Decorators.AddRange(compiled.Decorators);
                node.Services.AddRange(compiled.Services);
                CopyInputBindings(compiled.Inputs, node.Inputs);
                CopyProperties(compiled.Properties, node.Properties);
                tree.NodesById[node.Id] = node;
            }

            return tree;
        }

        private static void CopyInputBindings(List<BehaviorTreeCompiledInputBinding> inputs, Dictionary<string, string> target)
        {
            if (inputs == null)
            {
                return;
            }

            for (int i = 0; i < inputs.Count; i++)
            {
                BehaviorTreeCompiledInputBinding input = inputs[i];
                if (input == null || string.IsNullOrEmpty(input.InputId) || string.IsNullOrEmpty(input.BlackboardKey))
                {
                    continue;
                }

                target[input.InputId] = input.BlackboardKey;
            }
        }

        private static void CopyProperties(List<BehaviorTreeCompiledProperty> properties, Dictionary<string, object> target)
        {
            if (properties == null)
            {
                return;
            }

            for (int i = 0; i < properties.Count; i++)
            {
                BehaviorTreeCompiledProperty property = properties[i];
                if (property == null || string.IsNullOrEmpty(property.Id))
                {
                    continue;
                }

                target[property.Id] = DeserializeValue(property.JsonValue);
            }
        }

        private static void ReplaceList<T>(List<T> target, IEnumerable<T> source)
        {
            target.Clear();
            if (source != null)
            {
                target.AddRange(source);
            }
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
    public sealed class BehaviorTreeCompiledBlackboardKey
    {
        public string Name;
        public string Type;
        public string DefaultValueJson;
        public bool Exposed;
        public bool Persistent;
        public string Description;

        public BehaviorTreeBlackboardKey ToKey()
        {
            BehaviorTreeBlackboardKey key = new BehaviorTreeBlackboardKey();
            key.Name = Name;
            key.Type = Type;
            key.DefaultValue = DeserializeValue(DefaultValueJson);
            key.Exposed = Exposed;
            key.Persistent = Persistent;
            key.Description = Description;
            return key;
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
    public sealed class BehaviorTreeCompiledNode
    {
        public string Id;
        public string TypeId;
        public List<string> Children = new List<string>();
        public List<string> Decorators = new List<string>();
        public List<string> Services = new List<string>();
        public List<BehaviorTreeCompiledInputBinding> Inputs = new List<BehaviorTreeCompiledInputBinding>();
        public List<BehaviorTreeCompiledProperty> Properties = new List<BehaviorTreeCompiledProperty>();
    }

    [Serializable]
    public sealed class BehaviorTreeCompiledComponent
    {
        public string Name;
        public string BehaviorTreePath;
        public string BehaviorTreeGuid;
        public bool Required;
        public BehaviorTreeCompiledAsset CompiledBehaviorTree;

        public RuntimeBehaviorTreeComponent ToRuntimeComponent()
        {
            RuntimeBehaviorTreeComponent component = new RuntimeBehaviorTreeComponent();
            component.Name = Name;
            component.BehaviorTreePath = BehaviorTreePath;
            component.BehaviorTreeGuid = BehaviorTreeGuid;
            component.Required = Required;
            component.CompiledBehaviorTree = CompiledBehaviorTree;
            return component;
        }
    }

    [Serializable]
    public sealed class BehaviorTreeCompiledDecorator
    {
        public string Id;
        public string TypeId;
        public List<BehaviorTreeCompiledInputBinding> Inputs = new List<BehaviorTreeCompiledInputBinding>();
        public List<BehaviorTreeCompiledProperty> Properties = new List<BehaviorTreeCompiledProperty>();
    }

    [Serializable]
    public sealed class BehaviorTreeCompiledService
    {
        public string Id;
        public string TypeId;
        public float Interval;
        public float RandomDeviation;
        public List<BehaviorTreeCompiledProperty> Properties = new List<BehaviorTreeCompiledProperty>();
    }

    [Serializable]
    public sealed class BehaviorTreeCompiledProperty
    {
        public string Id;
        public string JsonValue;
    }

    [Serializable]
    public sealed class BehaviorTreeCompiledInputBinding
    {
        public string InputId;
        public string BlackboardKey;
    }
}
