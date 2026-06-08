using System;
using System.Collections;
using System.Collections.Generic;

namespace BlueprintSystem
{
    public sealed class BehaviorTreeCompileResult
    {
        public RuntimeBehaviorTree Tree;
        public readonly BlueprintDiagnosticList Diagnostics = new BlueprintDiagnosticList();

        public bool Success
        {
            get { return Tree != null && !Diagnostics.HasErrors; }
        }
    }

    public sealed class BehaviorTreeCompiler
    {
        public BehaviorTreeCompileResult Compile(BehaviorTreeSource source, BehaviorTreeExecutorRegistry registry = null)
        {
            BehaviorTreeCompileResult result = new BehaviorTreeCompileResult();
            registry = registry ?? BehaviorTreeExecutorRegistry.CreateDefault();

            BehaviorTreeValidator validator = new BehaviorTreeValidator();
            result.Diagnostics.AddRange(validator.Validate(source, registry));
            if (result.Diagnostics.HasErrors)
            {
                return result;
            }

            RuntimeBehaviorTree runtime = new RuntimeBehaviorTree();
            runtime.Name = source.Name;
            runtime.RootNodeId = source.Root;
            runtime.Registry = registry;
            runtime.BlackboardSchema.AddRange(source.Blackboard);

            for (int i = 0; i < source.Decorators.Count; i++)
            {
                BehaviorTreeDecoratorSource sourceDecorator = source.Decorators[i];
                RuntimeBehaviorTreeDecorator decorator = new RuntimeBehaviorTreeDecorator();
                decorator.Id = sourceDecorator.Id;
                decorator.TypeId = sourceDecorator.TypeId;
                foreach (KeyValuePair<string, string> pair in sourceDecorator.Inputs)
                {
                    decorator.Inputs[pair.Key] = pair.Value;
                }

                foreach (KeyValuePair<string, object> pair in sourceDecorator.Properties)
                {
                    decorator.Properties[pair.Key] = pair.Value;
                }

                runtime.DecoratorsById[decorator.Id] = decorator;
            }

            for (int i = 0; i < source.Services.Count; i++)
            {
                BehaviorTreeServiceSource sourceService = source.Services[i];
                RuntimeBehaviorTreeService service = new RuntimeBehaviorTreeService();
                service.Id = sourceService.Id;
                service.TypeId = sourceService.TypeId;
                service.Interval = sourceService.Interval;
                service.RandomDeviation = sourceService.RandomDeviation;
                foreach (KeyValuePair<string, object> pair in sourceService.Properties)
                {
                    service.Properties[pair.Key] = pair.Value;
                }

                runtime.ServicesById[service.Id] = service;
            }

            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BehaviorTreeNodeSource sourceNode = source.Nodes[i];
                RuntimeBehaviorTreeNode node = new RuntimeBehaviorTreeNode();
                node.Id = sourceNode.Id;
                node.TypeId = sourceNode.TypeId;
                node.Children.AddRange(sourceNode.Children);
                node.Decorators.AddRange(sourceNode.Decorators);
                node.Services.AddRange(sourceNode.Services);
                foreach (KeyValuePair<string, string> pair in sourceNode.Inputs)
                {
                    node.Inputs[pair.Key] = pair.Value;
                }

                foreach (KeyValuePair<string, object> pair in sourceNode.Properties)
                {
                    node.Properties[pair.Key] = pair.Value;
                }

                runtime.NodesById[node.Id] = node;
            }

            result.Tree = runtime;
            return result;
        }

        public BehaviorTreeCompileResult CompileJson(string behaviorTreeJson, BehaviorTreeExecutorRegistry registry = null)
        {
            BehaviorTreeCompileResult result = new BehaviorTreeCompileResult();
            try
            {
                return Compile(BehaviorTreeSource.FromJson(behaviorTreeJson), registry);
            }
            catch (BlueprintJsonException ex)
            {
                result.Diagnostics.Add(BlueprintDiagnostic.Error("BT001", ex.Message));
                return result;
            }
        }

        public static List<BehaviorTreeCompiledBlackboardKey> BuildBlackboard(BehaviorTreeSource source)
        {
            List<BehaviorTreeCompiledBlackboardKey> result = new List<BehaviorTreeCompiledBlackboardKey>();
            for (int i = 0; i < source.Blackboard.Count; i++)
            {
                BehaviorTreeBlackboardKey key = source.Blackboard[i];
                if (key == null)
                {
                    continue;
                }

                result.Add(new BehaviorTreeCompiledBlackboardKey
                {
                    Name = key.Name,
                    Type = key.Type,
                    DefaultValueJson = SerializeValueForType(key.DefaultValue, key.Type),
                    Exposed = key.Exposed,
                    Persistent = key.Persistent,
                    Description = key.Description
                });
            }

            return result;
        }

        public static List<BehaviorTreeCompiledNode> BuildNodes(BehaviorTreeSource source)
        {
            List<BehaviorTreeCompiledNode> result = new List<BehaviorTreeCompiledNode>();
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BehaviorTreeNodeSource node = source.Nodes[i];
                if (node == null)
                {
                    continue;
                }

                BehaviorTreeCompiledNode compiled = new BehaviorTreeCompiledNode();
                compiled.Id = node.Id;
                compiled.TypeId = node.TypeId;
                compiled.Children.AddRange(node.Children);
                compiled.Decorators.AddRange(node.Decorators);
                compiled.Services.AddRange(node.Services);
                AddCompiledInputBindings(compiled.Inputs, node.Inputs);
                AddCompiledProperties(compiled.Properties, node.Properties);
                result.Add(compiled);
            }

            return result;
        }

        public static List<BehaviorTreeCompiledDecorator> BuildDecorators(BehaviorTreeSource source)
        {
            List<BehaviorTreeCompiledDecorator> result = new List<BehaviorTreeCompiledDecorator>();
            for (int i = 0; i < source.Decorators.Count; i++)
            {
                BehaviorTreeDecoratorSource decorator = source.Decorators[i];
                if (decorator == null)
                {
                    continue;
                }

                BehaviorTreeCompiledDecorator compiled = new BehaviorTreeCompiledDecorator();
                compiled.Id = decorator.Id;
                compiled.TypeId = decorator.TypeId;
                AddCompiledInputBindings(compiled.Inputs, decorator.Inputs);
                AddCompiledProperties(compiled.Properties, decorator.Properties);
                result.Add(compiled);
            }

            return result;
        }

        public static List<BehaviorTreeCompiledService> BuildServices(BehaviorTreeSource source)
        {
            List<BehaviorTreeCompiledService> result = new List<BehaviorTreeCompiledService>();
            for (int i = 0; i < source.Services.Count; i++)
            {
                BehaviorTreeServiceSource service = source.Services[i];
                if (service == null)
                {
                    continue;
                }

                BehaviorTreeCompiledService compiled = new BehaviorTreeCompiledService();
                compiled.Id = service.Id;
                compiled.TypeId = service.TypeId;
                compiled.Interval = service.Interval;
                compiled.RandomDeviation = service.RandomDeviation;
                AddCompiledProperties(compiled.Properties, service.Properties);
                result.Add(compiled);
            }

            return result;
        }

        public static string SerializeValueForType(object value, string type)
        {
            return BlueprintJson.Serialize(BehaviorTreeValueUtility.NormalizeValueForJson(value, type), false);
        }

        private static void AddCompiledProperties(List<BehaviorTreeCompiledProperty> target, Dictionary<string, object> properties)
        {
            List<string> keys = new List<string>(properties.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                target.Add(new BehaviorTreeCompiledProperty
                {
                    Id = key,
                    JsonValue = SerializeValueForType(properties[key], null)
                });
            }
        }

        private static void AddCompiledInputBindings(List<BehaviorTreeCompiledInputBinding> target, Dictionary<string, string> inputs)
        {
            List<string> keys = new List<string>(inputs.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string inputId = keys[i];
                string blackboardKey = inputs[inputId];
                if (string.IsNullOrEmpty(inputId) || string.IsNullOrEmpty(blackboardKey))
                {
                    continue;
                }

                target.Add(new BehaviorTreeCompiledInputBinding
                {
                    InputId = inputId,
                    BlackboardKey = blackboardKey
                });
            }
        }
    }

    public sealed class BehaviorTreeValidator
    {
        private static readonly HashSet<string> BlackboardPropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "key",
            "valueKey",
            "targetKey",
            "sourceKey",
            "distanceKey",
            "leftKey",
            "rightKey",
            "completeKey",
            "failureKey",
            "blackboardKey",
            "hitPointKey"
        };

        public BlueprintDiagnosticList Validate(BehaviorTreeSource source, BehaviorTreeExecutorRegistry registry = null)
        {
            BlueprintDiagnosticList diagnostics = new BlueprintDiagnosticList();
            registry = registry ?? BehaviorTreeExecutorRegistry.CreateDefault();

            if (source == null)
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BT001", "Behavior tree source is null."));
                return diagnostics;
            }

            Dictionary<string, BehaviorTreeBlackboardKey> blackboard = ValidateBlackboard(source, diagnostics);
            Dictionary<string, BehaviorTreeNodeSource> nodes = ValidateNodeIds(source, diagnostics);
            Dictionary<string, BehaviorTreeDecoratorSource> decorators = ValidateDecoratorIds(source, registry, blackboard, diagnostics);
            Dictionary<string, BehaviorTreeServiceSource> services = ValidateServiceIds(source, registry, blackboard, diagnostics);
            ValidateRoot(source, nodes, diagnostics);
            ValidateNodes(source, registry, nodes, decorators, services, blackboard, diagnostics);
            ValidateCycles(source, nodes, diagnostics);
            return diagnostics;
        }

        private static Dictionary<string, BehaviorTreeBlackboardKey> ValidateBlackboard(BehaviorTreeSource source, BlueprintDiagnosticList diagnostics)
        {
            Dictionary<string, BehaviorTreeBlackboardKey> result = new Dictionary<string, BehaviorTreeBlackboardKey>(StringComparer.Ordinal);
            for (int i = 0; i < source.Blackboard.Count; i++)
            {
                BehaviorTreeBlackboardKey key = source.Blackboard[i];
                if (key == null || string.IsNullOrEmpty(key.Name))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT020", "Blackboard key is missing a name."));
                    continue;
                }

                if (result.ContainsKey(key.Name))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT021", "Duplicate blackboard key '" + key.Name + "'."));
                    continue;
                }

                if (!BehaviorTreeValueUtility.IsKnownBlackboardType(key.Type))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT022", "Unknown blackboard type '" + key.Type + "' for key '" + key.Name + "'."));
                }

                if (key.Type == BlueprintVariableTypeRegistry.BlueprintRefTypeId && key.DefaultValue != null)
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT023", "BlueprintRef blackboard key '" + key.Name + "' cannot have a JSON default value."));
                }

                if ((key.Type == "GameObject" || key.Type == "Transform") && key.DefaultValue != null)
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT024", key.Type + " blackboard key '" + key.Name + "' cannot store a JSON object reference default."));
                }

                result[key.Name] = key;
            }

            return result;
        }

        private static Dictionary<string, BehaviorTreeNodeSource> ValidateNodeIds(BehaviorTreeSource source, BlueprintDiagnosticList diagnostics)
        {
            Dictionary<string, BehaviorTreeNodeSource> result = new Dictionary<string, BehaviorTreeNodeSource>(StringComparer.Ordinal);
            int rootCount = 0;
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BehaviorTreeNodeSource node = source.Nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT030", "Behavior tree node is missing an id."));
                    continue;
                }

                if (result.ContainsKey(node.Id))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT031", "Duplicate behavior tree node id '" + node.Id + "'.", node.Id));
                    continue;
                }

                if (node.TypeId == BehaviorTreeNodeTypeUtility.Root)
                {
                    rootCount++;
                }

                result[node.Id] = node;
            }

            if (rootCount != 1)
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BT032", "Behavior tree must contain exactly one BT.Root node."));
            }

            return result;
        }

        private static Dictionary<string, BehaviorTreeDecoratorSource> ValidateDecoratorIds(
            BehaviorTreeSource source,
            BehaviorTreeExecutorRegistry registry,
            Dictionary<string, BehaviorTreeBlackboardKey> blackboard,
            BlueprintDiagnosticList diagnostics)
        {
            Dictionary<string, BehaviorTreeDecoratorSource> result = new Dictionary<string, BehaviorTreeDecoratorSource>(StringComparer.Ordinal);
            for (int i = 0; i < source.Decorators.Count; i++)
            {
                BehaviorTreeDecoratorSource decorator = source.Decorators[i];
                if (decorator == null || string.IsNullOrEmpty(decorator.Id))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT040", "Behavior tree decorator is missing an id."));
                    continue;
                }

                if (result.ContainsKey(decorator.Id))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT041", "Duplicate behavior tree decorator id '" + decorator.Id + "'.", decorator.Id));
                    continue;
                }

                if (!registry.HasDecorator(decorator.TypeId))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT042", "No decorator executor registered for '" + decorator.TypeId + "'.", decorator.Id));
                }

                ValidateBlackboardReferences(decorator.Properties, blackboard, diagnostics, decorator.Id);
                ValidateInputBindings(decorator.Inputs, blackboard, diagnostics, decorator.Id);
                result[decorator.Id] = decorator;
            }

            return result;
        }

        private static Dictionary<string, BehaviorTreeServiceSource> ValidateServiceIds(
            BehaviorTreeSource source,
            BehaviorTreeExecutorRegistry registry,
            Dictionary<string, BehaviorTreeBlackboardKey> blackboard,
            BlueprintDiagnosticList diagnostics)
        {
            Dictionary<string, BehaviorTreeServiceSource> result = new Dictionary<string, BehaviorTreeServiceSource>(StringComparer.Ordinal);
            for (int i = 0; i < source.Services.Count; i++)
            {
                BehaviorTreeServiceSource service = source.Services[i];
                if (service == null || string.IsNullOrEmpty(service.Id))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT050", "Behavior tree service is missing an id."));
                    continue;
                }

                if (result.ContainsKey(service.Id))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT051", "Duplicate behavior tree service id '" + service.Id + "'.", service.Id));
                    continue;
                }

                if (!registry.HasService(service.TypeId))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT052", "No service executor registered for '" + service.TypeId + "'.", service.Id));
                }

                if (service.Interval > 0f && service.Interval < 0.02f)
                {
                    diagnostics.Add(BlueprintDiagnostic.Warning("BT053", "Service '" + service.Id + "' has a very small interval.", service.Id));
                }

                ValidateBlackboardReferences(service.Properties, blackboard, diagnostics, service.Id);
                result[service.Id] = service;
            }

            return result;
        }

        private static void ValidateRoot(
            BehaviorTreeSource source,
            Dictionary<string, BehaviorTreeNodeSource> nodes,
            BlueprintDiagnosticList diagnostics)
        {
            if (string.IsNullOrEmpty(source.Root))
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BT060", "Behavior tree root field is required."));
                return;
            }

            BehaviorTreeNodeSource root;
            if (!nodes.TryGetValue(source.Root, out root))
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BT061", "Behavior tree root node '" + source.Root + "' does not exist.", source.Root));
                return;
            }

            if (root.TypeId != BehaviorTreeNodeTypeUtility.Root)
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BT062", "Behavior tree root field must reference a BT.Root node.", root.Id));
            }
        }

        private static void ValidateNodes(
            BehaviorTreeSource source,
            BehaviorTreeExecutorRegistry registry,
            Dictionary<string, BehaviorTreeNodeSource> nodes,
            Dictionary<string, BehaviorTreeDecoratorSource> decorators,
            Dictionary<string, BehaviorTreeServiceSource> services,
            Dictionary<string, BehaviorTreeBlackboardKey> blackboard,
            BlueprintDiagnosticList diagnostics)
        {
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BehaviorTreeNodeSource node = source.Nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id))
                {
                    continue;
                }

                if (!registry.HasNode(node.TypeId))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT070", "No node executor registered for '" + node.TypeId + "'.", node.Id));
                }

                if (node.TypeId == BehaviorTreeNodeTypeUtility.Root && node.Children.Count != 1)
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT071", "Root node must have exactly one child.", node.Id));
                }
                else if (BehaviorTreeNodeTypeUtility.IsComposite(node.TypeId) && node.Children.Count == 0)
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT072", "Composite node must have at least one child.", node.Id));
                }
                else if (BehaviorTreeNodeTypeUtility.IsTask(node.TypeId) && node.Children.Count > 0)
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT073", "Task node cannot have children.", node.Id));
                }

                for (int c = 0; c < node.Children.Count; c++)
                {
                    if (!nodes.ContainsKey(node.Children[c]))
                    {
                        diagnostics.Add(BlueprintDiagnostic.Error("BT074", "Node '" + node.Id + "' references missing child '" + node.Children[c] + "'.", node.Id));
                    }
                }

                for (int d = 0; d < node.Decorators.Count; d++)
                {
                    if (!decorators.ContainsKey(node.Decorators[d]))
                    {
                        diagnostics.Add(BlueprintDiagnostic.Error("BT075", "Node '" + node.Id + "' references missing decorator '" + node.Decorators[d] + "'.", node.Id));
                    }
                }

                for (int s = 0; s < node.Services.Count; s++)
                {
                    if (!services.ContainsKey(node.Services[s]))
                    {
                        diagnostics.Add(BlueprintDiagnostic.Error("BT076", "Node '" + node.Id + "' references missing service '" + node.Services[s] + "'.", node.Id));
                    }
                }

                ValidateRequiredProperties(node, diagnostics);
                ValidateNodeBlackboardReferences(node, blackboard, diagnostics);
                ValidateRunSubtreeLocalMappings(node, blackboard, diagnostics);
                ValidateInputBindings(node.Inputs, blackboard, diagnostics, node.Id);
            }
        }

        private static void ValidateCycles(
            BehaviorTreeSource source,
            Dictionary<string, BehaviorTreeNodeSource> nodes,
            BlueprintDiagnosticList diagnostics)
        {
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(source.Root))
            {
                Visit(source.Root, nodes, visiting, visited, diagnostics);
            }
        }

        private static void Visit(
            string nodeId,
            Dictionary<string, BehaviorTreeNodeSource> nodes,
            HashSet<string> visiting,
            HashSet<string> visited,
            BlueprintDiagnosticList diagnostics)
        {
            if (visited.Contains(nodeId) || !nodes.ContainsKey(nodeId))
            {
                return;
            }

            if (!visiting.Add(nodeId))
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BT080", "Behavior tree contains a cycle at node '" + nodeId + "'.", nodeId));
                return;
            }

            BehaviorTreeNodeSource node = nodes[nodeId];
            for (int i = 0; i < node.Children.Count; i++)
            {
                Visit(node.Children[i], nodes, visiting, visited, diagnostics);
            }

            visiting.Remove(nodeId);
            visited.Add(nodeId);
        }

        private static void ValidateRequiredProperties(BehaviorTreeNodeSource node, BlueprintDiagnosticList diagnostics)
        {
            if (node.TypeId == "BT.MoveTo" &&
                !HasInputBinding(node, "target") &&
                !HasNonEmpty(node.Properties, "targetKey") &&
                !node.Properties.ContainsKey("targetPosition"))
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BT090", "MoveTo requires a target input, targetKey, or targetPosition.", node.Id));
            }

            if ((node.TypeId == "BT.SetBlackboard" || node.TypeId == "BT.ClearBlackboard") &&
                !HasInputBinding(node, "key") &&
                !HasNonEmpty(node.Properties, "key"))
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BT091", node.TypeId + " requires key.", node.Id));
            }

            if ((node.TypeId == "BT.TriggerBlueprintEvent" || node.TypeId == "BT.RunBlueprintTask") &&
                !HasInputBinding(node, "eventName") &&
                !HasInputBinding(node, "startEventName") &&
                !HasNonEmpty(node.Properties, "eventName") &&
                !HasNonEmpty(node.Properties, "startEventName"))
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BT092", node.TypeId + " requires eventName or startEventName.", node.Id));
            }

            if (node.TypeId == "BT.RunSubtree")
            {
                if (!HasNonEmpty(node.Properties, "behaviorTree"))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT097", "BT.RunSubtree requires behaviorTree.", node.Id));
                }

                string mode = GetPropertyString(node.Properties, "blackboardMode", "Shared");
                if (!IsRunSubtreeBlackboardMode(mode))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT098", "BT.RunSubtree blackboardMode must be Shared or Isolated.", node.Id));
                }
            }

            if (IsRunnerBlackboardNode(node.TypeId))
            {
                if (!HasInputBinding(node, "target"))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT093", node.TypeId + " requires target input.", node.Id));
                }

                if ((node.TypeId == "BT.SetRunnerBlackboard" || node.TypeId == "BT.ClearRunnerBlackboard") &&
                    !HasNonEmpty(node.Properties, "targetKey"))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT094", node.TypeId + " requires targetKey.", node.Id));
                }

                if (node.TypeId == "BT.SetRunnerBlackboard" &&
                    !HasInputBinding(node, "value") &&
                    !HasNonEmpty(node.Properties, "sourceKey"))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT095", node.TypeId + " requires value input or sourceKey.", node.Id));
                }

                if ((node.TypeId == "BT.GetRunnerBlackboard" || node.TypeId == "BT.CopyRunnerBlackboard") &&
                    (!HasNonEmpty(node.Properties, "sourceKey") || !HasNonEmpty(node.Properties, "targetKey")))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT096", node.TypeId + " requires sourceKey and targetKey.", node.Id));
                }
            }
        }

        private static void ValidateNodeBlackboardReferences(
            BehaviorTreeNodeSource node,
            Dictionary<string, BehaviorTreeBlackboardKey> blackboard,
            BlueprintDiagnosticList diagnostics)
        {
            if (node == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in node.Properties)
            {
                if (!ShouldValidateNodeBlackboardProperty(node, pair.Key))
                {
                    continue;
                }

                string key = pair.Value as string;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!blackboard.ContainsKey(key))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT100", "Blackboard key '" + key + "' is not declared.", node.Id));
                }
            }
        }

        private static bool ShouldValidateNodeBlackboardProperty(BehaviorTreeNodeSource node, string propertyName)
        {
            if (!BlackboardPropertyNames.Contains(propertyName))
            {
                return false;
            }

            if (node.TypeId == "BT.SetRunnerBlackboard")
            {
                if (propertyName == "targetKey")
                {
                    return false;
                }

                if (propertyName == "sourceKey")
                {
                    return !HasInputBinding(node, "value");
                }
            }
            else if (node.TypeId == "BT.GetRunnerBlackboard")
            {
                if (propertyName == "sourceKey")
                {
                    return false;
                }

                if (propertyName == "targetKey")
                {
                    return true;
                }
            }
            else if (node.TypeId == "BT.ClearRunnerBlackboard")
            {
                if (propertyName == "targetKey")
                {
                    return false;
                }
            }
            else if (node.TypeId == "BT.CopyRunnerBlackboard")
            {
                if (propertyName == "targetKey")
                {
                    return false;
                }

                if (propertyName == "sourceKey")
                {
                    return !HasInputBinding(node, "sourceTarget");
                }
            }

            return true;
        }

        private static bool IsRunnerBlackboardNode(string typeId)
        {
            return typeId == "BT.SetRunnerBlackboard" ||
                   typeId == "BT.GetRunnerBlackboard" ||
                   typeId == "BT.ClearRunnerBlackboard" ||
                   typeId == "BT.CopyRunnerBlackboard";
        }

        private static void ValidateRunSubtreeLocalMappings(
            BehaviorTreeNodeSource node,
            Dictionary<string, BehaviorTreeBlackboardKey> blackboard,
            BlueprintDiagnosticList diagnostics)
        {
            if (node == null || node.TypeId != "BT.RunSubtree")
            {
                return;
            }

            string mode = GetPropertyString(node.Properties, "blackboardMode", "Shared");
            if (!string.Equals(mode, "Isolated", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ValidateRunSubtreeMappingParentKey(node, "inputMappings", "sourceKey", blackboard, diagnostics);
            ValidateRunSubtreeMappingParentKey(node, "outputMappings", "targetKey", blackboard, diagnostics);
        }

        private static void ValidateRunSubtreeMappingParentKey(
            BehaviorTreeNodeSource node,
            string propertyName,
            string parentKeyName,
            Dictionary<string, BehaviorTreeBlackboardKey> blackboard,
            BlueprintDiagnosticList diagnostics)
        {
            List<Dictionary<string, object>> mappings = GetObjectArrayProperty(node.Properties, propertyName);
            for (int i = 0; i < mappings.Count; i++)
            {
                string sourceKey = GetPropertyString(mappings[i], "sourceKey", null);
                string targetKey = GetPropertyString(mappings[i], "targetKey", null);
                if (string.IsNullOrEmpty(sourceKey) || string.IsNullOrEmpty(targetKey))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT099", "BT.RunSubtree " + propertyName + " entries require sourceKey and targetKey.", node.Id));
                    continue;
                }

                string parentKey = GetPropertyString(mappings[i], parentKeyName, null);
                if (!string.IsNullOrEmpty(parentKey) && !blackboard.ContainsKey(parentKey))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT100", "Blackboard key '" + parentKey + "' is not declared.", node.Id));
                }
            }
        }

        private static List<Dictionary<string, object>> GetObjectArrayProperty(Dictionary<string, object> properties, string key)
        {
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            if (properties == null)
            {
                return result;
            }

            object value;
            if (!properties.TryGetValue(key, out value) || value == null || value is string)
            {
                return result;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return result;
            }

            foreach (object item in enumerable)
            {
                Dictionary<string, object> dictionary = item as Dictionary<string, object>;
                if (dictionary != null)
                {
                    result.Add(dictionary);
                }
            }

            return result;
        }

        private static string GetPropertyString(Dictionary<string, object> properties, string key, string defaultValue)
        {
            object value;
            if (properties == null || !properties.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            return Convert.ToString(value);
        }

        private static bool IsRunSubtreeBlackboardMode(string mode)
        {
            return string.IsNullOrEmpty(mode) ||
                   string.Equals(mode, "Shared", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(mode, "Isolated", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateBlackboardReferences(
            Dictionary<string, object> properties,
            Dictionary<string, BehaviorTreeBlackboardKey> blackboard,
            BlueprintDiagnosticList diagnostics,
            string nodeId)
        {
            foreach (KeyValuePair<string, object> pair in properties)
            {
                if (!BlackboardPropertyNames.Contains(pair.Key))
                {
                    continue;
                }

                string key = pair.Value as string;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!blackboard.ContainsKey(key))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT100", "Blackboard key '" + key + "' is not declared.", nodeId));
                }
            }
        }

        private static void ValidateInputBindings(
            Dictionary<string, string> inputs,
            Dictionary<string, BehaviorTreeBlackboardKey> blackboard,
            BlueprintDiagnosticList diagnostics,
            string nodeId)
        {
            if (inputs == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> pair in inputs)
            {
                if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value))
                {
                    continue;
                }

                if (!blackboard.ContainsKey(pair.Value))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BT100", "Blackboard key '" + pair.Value + "' is not declared for input '" + pair.Key + "'.", nodeId));
                }
            }
        }

        private static bool HasNonEmpty(Dictionary<string, object> properties, string key)
        {
            object value;
            return properties.TryGetValue(key, out value) && value != null && !string.IsNullOrEmpty(Convert.ToString(value));
        }

        private static bool HasInputBinding(BehaviorTreeNodeSource node, string inputId)
        {
            string blackboardKey;
            return node != null &&
                   node.Inputs != null &&
                   node.Inputs.TryGetValue(inputId, out blackboardKey) &&
                   !string.IsNullOrEmpty(blackboardKey);
        }
    }
}
