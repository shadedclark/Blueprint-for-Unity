using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [Graph(AssetExtension, GraphOptions.DisableAutoInclusionOfNodesFromGraphAssembly)]
    [Serializable]
    public sealed class BehaviorTreeVisualGraph : Graph
    {
        public const string AssetExtension = "btgraph";

        public string SourceBehaviorTreeAssetPath;
        public string SchemaVersion = "0.1";
        public string BehaviorTreeName;
        public string Category;
        public string Description;
        public string RootNodeId;
        public List<BehaviorTreeVisualBlackboardKeyData> Blackboard = new List<BehaviorTreeVisualBlackboardKeyData>();
        public List<BehaviorTreeVisualNodeData> Nodes = new List<BehaviorTreeVisualNodeData>();
        public List<BehaviorTreeVisualDecoratorData> Decorators = new List<BehaviorTreeVisualDecoratorData>();
        public List<BehaviorTreeVisualServiceData> Services = new List<BehaviorTreeVisualServiceData>();

        [MenuItem("Assets/Create/Blueprint System/Behavior Tree Visual Graph", false, 2115)]
        private static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<BehaviorTreeVisualGraph>("New Behavior Tree Graph");
        }

        public override void OnEnable()
        {
            base.OnEnable();
            if (BehaviorTreeGraphToolkitBlackboardSync.SyncBlackboardToGraph(this))
            {
                BehaviorTreeGraphToolkitReflection.MarkDirty(this);
            }
        }

        public override void OnGraphChanged(GraphLogger graphLogger)
        {
            HashSet<string> nodeIds = new HashSet<string>(StringComparer.Ordinal);
            List<BehaviorTreeVisualNode> visualNodes = GetNodes().OfType<BehaviorTreeVisualNode>().ToList();
            if (visualNodes.Count > 0)
            {
                for (int i = 0; i < visualNodes.Count; i++)
                {
                    BehaviorTreeVisualNode node = visualNodes[i];
                    string nodeId = node.ReadNodeId();
                    if (string.IsNullOrEmpty(nodeId))
                    {
                        graphLogger.LogWarning("Behavior tree node is missing an id.", node);
                        continue;
                    }

                    if (!nodeIds.Add(nodeId))
                    {
                        graphLogger.LogError("Duplicate behavior tree node id '" + nodeId + "'.", node);
                    }
                }

                return;
            }

            if (Nodes == null)
            {
                return;
            }

            for (int i = 0; i < Nodes.Count; i++)
            {
                BehaviorTreeVisualNodeData node = Nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id))
                {
                    graphLogger.LogWarning("Behavior tree node is missing an id.", this);
                    continue;
                }

                if (!nodeIds.Add(node.Id))
                {
                    graphLogger.LogError("Duplicate behavior tree node id '" + node.Id + "'.", this);
                }
            }
        }
    }

    [Serializable]
    public class BehaviorTreeVisualNode : Node
    {
        public const string ParentPortName = "parent";
        public const string DecoratorPortName = "conditions";
        private const string NodeIdOptionName = "__bt_node_id";
        private const string TypeIdOptionName = "__bt_type_id";
        private const string TypeLabelOptionName = "__bt_type_label";
        private const string ChildCountOptionName = "__bt_child_count";
        private const string DecoratorsOptionName = "__bt_decorators";
        private const string ServicesOptionName = "__bt_services";
        private const string PropertiesOptionName = "__bt_properties_json";

        public string Id;
        public string TypeId;
        public string Title;
        public List<string> Children = new List<string>();
        public List<string> Decorators = new List<string>();
        public List<string> Services = new List<string>();
        public List<BehaviorTreeVisualInputPortData> Inputs = new List<BehaviorTreeVisualInputPortData>();
        public List<BehaviorTreeVisualInputBindingData> InputBindings = new List<BehaviorTreeVisualInputBindingData>();
        public string PropertiesJson = "{}";

        public static string GetChildPortName(int index)
        {
            return "child_" + index;
        }

        public string ReadNodeId()
        {
            EnsureConfigured();
            string value;
            if (TryReadStringOption(NodeIdOptionName, out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return Id;
        }

        public string ReadTypeId()
        {
            EnsureConfigured();
            string value;
            if (TryReadStringOption(TypeIdOptionName, out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return TypeId;
        }

        public string ReadPropertiesJson()
        {
            EnsureConfigured();
            string value;
            return TryReadStringOption(PropertiesOptionName, out value) ? value : PropertiesJson;
        }

        public List<BehaviorTreeVisualInputBindingData> ReadInputBindings()
        {
            EnsureConfigured();
            return CloneInputBindings(InputBindings);
        }

        public Dictionary<string, object> ReadInlineInputValues()
        {
            EnsureConfigured();
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, object> properties = DeserializeProperties(ReadPropertiesJson());
            for (int i = 0; i < Inputs.Count; i++)
            {
                BehaviorTreeVisualInputPortData input = Inputs[i];
                if (input == null || string.IsNullOrEmpty(input.Id) || !input.AllowInlineValue || IsInputConnected(input.Id))
                {
                    continue;
                }

                IPort inputPort = SafeGetInputPort(input.Id);
                object value;
                if (inputPort != null && BehaviorTreeVisualValueUtility.TryReadPortValue(inputPort, input.Type, out value))
                {
                    if (!HasInlineProperty(properties, input) && IsDefaultInputValue(input, value))
                    {
                        continue;
                    }

                    result[input.Id] = value;
                }
            }

            return result;
        }

        public List<string> ReadDecorators()
        {
            EnsureConfigured();
            string value;
            return TryReadStringOption(DecoratorsOptionName, out value)
                ? ParseIdList(value)
                : new List<string>(Decorators ?? new List<string>());
        }

        public List<string> ReadServices()
        {
            EnsureConfigured();
            string value;
            return TryReadStringOption(ServicesOptionName, out value)
                ? ParseIdList(value)
                : new List<string>(Services ?? new List<string>());
        }

        public int ReadChildCount()
        {
            EnsureConfigured();
            int value;
            if (TryReadIntOption(ChildCountOptionName, out value))
            {
                return Math.Max(0, value);
            }

            return Children == null ? 0 : Children.Count;
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            EnsureConfigured();
            context.AddOption<string>(TypeLabelOptionName)
                .WithDisplayName("Type")
                .WithDefaultValue(string.IsNullOrEmpty(Title) ? TypeId : Title)
                .Delayed();

            if (CanHaveChildren())
            {
                context.AddOption<int>(ChildCountOptionName)
                    .WithDisplayName("Child Count")
                    .WithDefaultValue(GetStoredChildCount())
                    .Delayed();
            }

            context.AddOption<string>(NodeIdOptionName)
                .WithDisplayName("Node Id")
                .WithDefaultValue(Id ?? string.Empty)
                .ShowInInspectorOnly()
                .Delayed();

            context.AddOption<string>(TypeIdOptionName)
                .WithDisplayName("Type Id")
                .WithDefaultValue(TypeId ?? string.Empty)
                .ShowInInspectorOnly()
                .Delayed();

            context.AddOption<string>(DecoratorsOptionName)
                .WithDisplayName("Decorators")
                .WithDefaultValue(string.Join(", ", Decorators.ToArray()))
                .ShowInInspectorOnly()
                .Delayed();

            context.AddOption<string>(ServicesOptionName)
                .WithDisplayName("Services")
                .WithDefaultValue(string.Join(", ", Services.ToArray()))
                .ShowInInspectorOnly()
                .Delayed();

            context.AddOption<string>(PropertiesOptionName)
                .WithDisplayName("Properties JSON")
                .WithDefaultValue(string.IsNullOrEmpty(PropertiesJson) ? "{}" : PropertiesJson)
                .ShowInInspectorOnly()
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            EnsureConfigured();
            context.AddInputPort(ParentPortName)
                .WithDisplayName("Parent")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddInputPort(DecoratorPortName)
                .WithDisplayName("Conditions")
                .WithConnectorUI(PortConnectorUI.Circle)
                .Build();

            for (int i = 0; i < Inputs.Count; i++)
            {
                AddInputPort(context, Inputs[i]);
            }

            int childCount = ReadChildCount();
            for (int i = 0; i < childCount; i++)
            {
                string childId = Children != null && i < Children.Count ? Children[i] : string.Empty;
                context.AddOutputPort(GetChildPortName(i))
                    .WithDisplayName(string.IsNullOrEmpty(childId) ? "Child " + i : childId)
                    .WithConnectorUI(PortConnectorUI.Arrowhead)
                    .Build();
            }
        }

        protected virtual void ConfigureDefaultNode()
        {
        }

        protected virtual void ApplyDefaultMetadata()
        {
        }

        protected void SetIdentity(string typeId, string title, int childPortCount)
        {
            TypeId = typeId;
            Title = title;
            PropertiesJson = string.IsNullOrEmpty(PropertiesJson) ? "{}" : PropertiesJson;

            if (string.IsNullOrEmpty(Id))
            {
                Id = CreateDefaultNodeId(typeId);
            }

            EnsureLists();
            while (Children.Count < childPortCount)
            {
                Children.Add(string.Empty);
            }
        }

        protected void AddBlackboardInput(string id, string type, string displayName, bool allowInlineValue)
        {
            EnsureLists();
            for (int i = 0; i < Inputs.Count; i++)
            {
                BehaviorTreeVisualInputPortData existing = Inputs[i];
                if (existing != null && existing.Id == id)
                {
                    existing.Type = type;
                    existing.DisplayName = displayName;
                    existing.AllowInlineValue = allowInlineValue;
                    return;
                }
            }

            Inputs.Add(new BehaviorTreeVisualInputPortData
            {
                Id = id,
                Type = type,
                DisplayName = displayName,
                AllowInlineValue = allowInlineValue
            });
        }

        private void AddInputPort(IPortDefinitionContext context, BehaviorTreeVisualInputPortData input)
        {
            if (input == null || string.IsNullOrEmpty(input.Id))
            {
                return;
            }

            Type graphType = BehaviorTreeVisualValueUtility.ToGraphType(input.Type);
            ITypedInputPortBuilder builder = context.AddInputPort(input.Id)
                .WithDisplayName(string.IsNullOrEmpty(input.DisplayName) ? input.Id : input.DisplayName)
                .WithConnectorUI(PortConnectorUI.Circle)
                .WithDataType(graphType);

            object defaultValue;
            if (input.AllowInlineValue && TryGetInlineDefaultValue(input, out defaultValue))
            {
                builder.WithDefaultValue(defaultValue);
            }

            IPort inputPort = builder.Build();
            if (!input.AllowInlineValue)
            {
                SuppressEmbeddedInputValue(inputPort);
            }
        }

        private bool TryGetInlineDefaultValue(BehaviorTreeVisualInputPortData input, out object value)
        {
            Dictionary<string, object> properties = DeserializeProperties(ReadPropertiesJson());
            object rawValue;
            if (properties.TryGetValue(input.Id, out rawValue))
            {
                value = BehaviorTreeVisualValueUtility.ConvertForGraphField(rawValue, input.Type);
                return true;
            }

            string legacyPropertyId = BehaviorTreeVisualNodeMetadata.GetLegacyValueProperty(TypeId, input.Id);
            if (!string.IsNullOrEmpty(legacyPropertyId) && properties.TryGetValue(legacyPropertyId, out rawValue))
            {
                value = BehaviorTreeVisualValueUtility.ConvertForGraphField(rawValue, input.Type);
                return true;
            }

            if (BehaviorTreeVisualNodeMetadata.TryGetDefaultInputValue(TypeId, input.Id, out rawValue))
            {
                value = BehaviorTreeVisualValueUtility.ConvertForGraphField(rawValue, input.Type);
                return true;
            }

            value = null;
            return false;
        }

        private bool HasInlineProperty(Dictionary<string, object> properties, BehaviorTreeVisualInputPortData input)
        {
            if (properties == null || input == null)
            {
                return false;
            }

            if (properties.ContainsKey(input.Id))
            {
                return true;
            }

            string legacyPropertyId = BehaviorTreeVisualNodeMetadata.GetLegacyValueProperty(TypeId, input.Id);
            return !string.IsNullOrEmpty(legacyPropertyId) && properties.ContainsKey(legacyPropertyId);
        }

        private bool IsDefaultInputValue(BehaviorTreeVisualInputPortData input, object value)
        {
            object defaultValue;
            if (input == null || !BehaviorTreeVisualNodeMetadata.TryGetDefaultInputValue(TypeId, input.Id, out defaultValue))
            {
                return false;
            }

            return BehaviorTreeVisualValueUtility.AreEquivalent(value, defaultValue, input.Type);
        }

        private bool IsInputConnected(string inputId)
        {
            IPort inputPort = SafeGetInputPort(inputId);
            if (inputPort == null)
            {
                return false;
            }

            List<IPort> connectedPorts = new List<IPort>();
            inputPort.GetConnectedPorts(connectedPorts);
            return connectedPorts.Count > 0;
        }

        private IPort SafeGetInputPort(string inputId)
        {
            try
            {
                return GetInputPortByName(inputId);
            }
            catch
            {
                return null;
            }
        }

        private bool TryReadStringOption(string optionName, out string value)
        {
            INodeOption option = GetNodeOptionByName(optionName);
            if (option != null && option.TryGetValue(out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private bool TryReadIntOption(string optionName, out int value)
        {
            INodeOption option = GetNodeOptionByName(optionName);
            if (option != null && option.TryGetValue(out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        private void EnsureConfigured()
        {
            EnsureLists();
            if (string.IsNullOrEmpty(TypeId))
            {
                ConfigureDefaultNode();
            }

            ApplyDefaultMetadata();
        }

        private void EnsureLists()
        {
            if (Children == null)
            {
                Children = new List<string>();
            }

            if (Decorators == null)
            {
                Decorators = new List<string>();
            }

            if (Services == null)
            {
                Services = new List<string>();
            }

            if (Inputs == null)
            {
                Inputs = new List<BehaviorTreeVisualInputPortData>();
            }

            if (InputBindings == null)
            {
                InputBindings = new List<BehaviorTreeVisualInputBindingData>();
            }
        }

        private bool CanHaveChildren()
        {
            return BehaviorTreeVisualNodeMetadata.CanHaveChildren(TypeId) || GetStoredChildCount() > 0;
        }

        private int GetStoredChildCount()
        {
            return Children == null ? 0 : Children.Count;
        }

        private static List<string> ParseIdList(string value)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(value))
            {
                return result;
            }

            string[] parts = value.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i] == null ? string.Empty : parts[i].Trim();
                if (!string.IsNullOrEmpty(part))
                {
                    result.Add(part);
                }
            }

            return result;
        }

        private static string CreateDefaultNodeId(string typeId)
        {
            string prefix = string.IsNullOrEmpty(typeId) ? "bt_node" : typeId.Replace('.', '_').ToLowerInvariant();
            return prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static Dictionary<string, object> DeserializeProperties(string json)
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

        private static void SuppressEmbeddedInputValue(IPort inputPort)
        {
            if (inputPort == null)
            {
                return;
            }

            PropertyInfo optionsProperty = FindProperty(inputPort.GetType(), "Options");
            if (optionsProperty == null || optionsProperty.PropertyType == null || !optionsProperty.PropertyType.IsEnum)
            {
                return;
            }

            try
            {
                int options = Convert.ToInt32(optionsProperty.GetValue(inputPort, null));
                object noEmbeddedConstant = Enum.ToObject(optionsProperty.PropertyType, options | 1);
                optionsProperty.SetValue(inputPort, noEmbeddedConstant, null);

                object nodeModel = GetPropertyValue(inputPort, "NodeModel");
                MethodInfo updateConstant = FindMethod(nodeModel == null ? null : nodeModel.GetType(), "UpdateConstantForInput");
                if (updateConstant != null)
                {
                    updateConstant.Invoke(nodeModel, new object[] { inputPort, null, null });
                }
            }
            catch
            {
            }
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null)
            {
                return null;
            }

            PropertyInfo property = FindProperty(target.GetType(), propertyName);
            return property == null ? null : property.GetValue(target, null);
        }

        private static PropertyInfo FindProperty(Type type, string propertyName)
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

        private static MethodInfo FindMethod(Type type, string methodName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo method = current.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public class BehaviorTreeVisualDecoratorNode : Node
    {
        public const string DecoratorOutputPortName = "condition";
        private const string DecoratorIdOptionName = "__bt_decorator_id";
        private const string TypeIdOptionName = "__bt_decorator_type_id";
        private const string TypeLabelOptionName = "__bt_type_label";
        private const string PropertiesOptionName = "__bt_properties_json";

        public string Id;
        public string TypeId;
        public string Title;
        public List<BehaviorTreeVisualInputPortData> Inputs = new List<BehaviorTreeVisualInputPortData>();
        public List<BehaviorTreeVisualInputBindingData> InputBindings = new List<BehaviorTreeVisualInputBindingData>();
        public string PropertiesJson = "{}";

        public string ReadDecoratorId()
        {
            EnsureConfigured();
            string value;
            if (TryReadStringOption(DecoratorIdOptionName, out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return Id;
        }

        public string ReadTypeId()
        {
            EnsureConfigured();
            string value;
            if (TryReadStringOption(TypeIdOptionName, out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return TypeId;
        }

        public string ReadPropertiesJson()
        {
            EnsureConfigured();
            string value;
            return TryReadStringOption(PropertiesOptionName, out value) ? value : PropertiesJson;
        }

        public List<BehaviorTreeVisualInputBindingData> ReadInputBindings()
        {
            EnsureConfigured();
            return CloneInputBindings(InputBindings);
        }

        public Dictionary<string, object> ReadInlineInputValues()
        {
            EnsureConfigured();
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, object> properties = DeserializeProperties(ReadPropertiesJson());
            for (int i = 0; i < Inputs.Count; i++)
            {
                BehaviorTreeVisualInputPortData input = Inputs[i];
                if (input == null || string.IsNullOrEmpty(input.Id) || !input.AllowInlineValue || IsInputConnected(input.Id))
                {
                    continue;
                }

                IPort inputPort = SafeGetInputPort(input.Id);
                object value;
                if (inputPort != null && BehaviorTreeVisualValueUtility.TryReadPortValue(inputPort, input.Type, out value))
                {
                    if (!HasInlineProperty(properties, input) && IsDefaultInputValue(input, value))
                    {
                        continue;
                    }

                    result[input.Id] = value;
                }
            }

            return result;
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            EnsureConfigured();
            context.AddOption<string>(TypeLabelOptionName)
                .WithDisplayName("Type")
                .WithDefaultValue(string.IsNullOrEmpty(Title) ? TypeId : Title)
                .Delayed();

            context.AddOption<string>(DecoratorIdOptionName)
                .WithDisplayName("Decorator Id")
                .WithDefaultValue(Id ?? string.Empty)
                .ShowInInspectorOnly()
                .Delayed();

            context.AddOption<string>(TypeIdOptionName)
                .WithDisplayName("Type Id")
                .WithDefaultValue(TypeId ?? string.Empty)
                .ShowInInspectorOnly()
                .Delayed();

            context.AddOption<string>(PropertiesOptionName)
                .WithDisplayName("Properties JSON")
                .WithDefaultValue(string.IsNullOrEmpty(PropertiesJson) ? "{}" : PropertiesJson)
                .ShowInInspectorOnly()
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            EnsureConfigured();
            for (int i = 0; i < Inputs.Count; i++)
            {
                AddInputPort(context, Inputs[i]);
            }

            context.AddOutputPort(DecoratorOutputPortName)
                .WithDisplayName("Condition")
                .WithConnectorUI(PortConnectorUI.Circle)
                .Build();
        }

        protected virtual void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.BlackboardCondition, "Decorator: Blackboard Condition");
        }

        protected virtual void ApplyDefaultMetadata()
        {
            AddBuiltinDecoratorInputs(TypeId);
        }

        protected void SetIdentity(string typeId, string title)
        {
            TypeId = typeId;
            Title = title;
            PropertiesJson = string.IsNullOrEmpty(PropertiesJson) ? "{}" : PropertiesJson;

            if (string.IsNullOrEmpty(Id))
            {
                Id = CreateDefaultDecoratorId(typeId);
            }

            EnsureLists();
        }

        protected void AddDecoratorInput(string id, string type, string displayName, bool allowInlineValue)
        {
            EnsureLists();
            for (int i = 0; i < Inputs.Count; i++)
            {
                BehaviorTreeVisualInputPortData existing = Inputs[i];
                if (existing != null && existing.Id == id)
                {
                    existing.Type = type;
                    existing.DisplayName = displayName;
                    existing.AllowInlineValue = allowInlineValue;
                    return;
                }
            }

            Inputs.Add(new BehaviorTreeVisualInputPortData
            {
                Id = id,
                Type = type,
                DisplayName = displayName,
                AllowInlineValue = allowInlineValue
            });
        }

        protected void AddBuiltinDecoratorInputs(string typeId)
        {
            switch (typeId)
            {
                case BehaviorTreeVisualNodeMetadata.BlackboardCondition:
                    AddDecoratorInput("value", null, "Value", false);
                    AddDecoratorInput("operator", nameof(BehaviorTreeComparisonOperator), "Operator", true);
                    AddDecoratorInput("expected", null, "Expected", false);
                    break;
                case BehaviorTreeVisualNodeMetadata.CompareFloat:
                    AddDecoratorInput("left", "float", "Left", true);
                    AddDecoratorInput("right", "float", "Right", true);
                    AddDecoratorInput("operator", nameof(BehaviorTreeComparisonOperator), "Operator", true);
                    break;
                case BehaviorTreeVisualNodeMetadata.CompareBool:
                    AddDecoratorInput("value", "bool", "Value", true);
                    AddDecoratorInput("expected", "bool", "Expected", true);
                    AddDecoratorInput("operator", nameof(BehaviorTreeComparisonOperator), "Operator", true);
                    break;
                case BehaviorTreeVisualNodeMetadata.ObjectIsSet:
                    AddDecoratorInput("value", null, "Value", false);
                    break;
                case BehaviorTreeVisualNodeMetadata.DistanceLessThan:
                    AddDecoratorInput("distance", "float", "Distance", true);
                    AddDecoratorInput("source", null, "Source", false);
                    AddDecoratorInput("sourcePosition", "Vector3", "Source Position", true);
                    AddDecoratorInput("target", null, "Target", false);
                    AddDecoratorInput("targetPosition", "Vector3", "Target Position", true);
                    AddDecoratorInput("maxDistance", "float", "Max Distance", true);
                    break;
                case BehaviorTreeVisualNodeMetadata.Cooldown:
                    AddDecoratorInput("duration", "float", "Duration", true);
                    break;
            }
        }

        private void AddInputPort(IPortDefinitionContext context, BehaviorTreeVisualInputPortData input)
        {
            if (input == null || string.IsNullOrEmpty(input.Id))
            {
                return;
            }

            Type graphType = BehaviorTreeVisualValueUtility.ToGraphType(input.Type);
            ITypedInputPortBuilder builder = context.AddInputPort(input.Id)
                .WithDisplayName(string.IsNullOrEmpty(input.DisplayName) ? input.Id : input.DisplayName)
                .WithConnectorUI(PortConnectorUI.Circle)
                .WithDataType(graphType);

            object defaultValue;
            if (input.AllowInlineValue && TryGetInlineDefaultValue(input, out defaultValue))
            {
                builder.WithDefaultValue(defaultValue);
            }

            IPort inputPort = builder.Build();
            if (!input.AllowInlineValue)
            {
                SuppressEmbeddedInputValue(inputPort);
            }
        }

        private bool TryGetInlineDefaultValue(BehaviorTreeVisualInputPortData input, out object value)
        {
            Dictionary<string, object> properties = DeserializeProperties(ReadPropertiesJson());
            object rawValue;
            if (properties.TryGetValue(input.Id, out rawValue))
            {
                value = BehaviorTreeVisualValueUtility.ConvertForGraphField(rawValue, input.Type);
                return true;
            }

            if (BehaviorTreeVisualNodeMetadata.TryGetDefaultInputValue(TypeId, input.Id, out rawValue))
            {
                value = BehaviorTreeVisualValueUtility.ConvertForGraphField(rawValue, input.Type);
                return true;
            }

            value = null;
            return false;
        }

        private bool HasInlineProperty(Dictionary<string, object> properties, BehaviorTreeVisualInputPortData input)
        {
            return properties != null && input != null && properties.ContainsKey(input.Id);
        }

        private bool IsDefaultInputValue(BehaviorTreeVisualInputPortData input, object value)
        {
            object defaultValue;
            if (input == null || !BehaviorTreeVisualNodeMetadata.TryGetDefaultInputValue(TypeId, input.Id, out defaultValue))
            {
                return false;
            }

            return BehaviorTreeVisualValueUtility.AreEquivalent(value, defaultValue, input.Type);
        }

        private bool IsInputConnected(string inputId)
        {
            IPort inputPort = SafeGetInputPort(inputId);
            if (inputPort == null)
            {
                return false;
            }

            List<IPort> connectedPorts = new List<IPort>();
            inputPort.GetConnectedPorts(connectedPorts);
            return connectedPorts.Count > 0;
        }

        private IPort SafeGetInputPort(string inputId)
        {
            try
            {
                return GetInputPortByName(inputId);
            }
            catch
            {
                return null;
            }
        }

        private bool TryReadStringOption(string optionName, out string value)
        {
            INodeOption option = GetNodeOptionByName(optionName);
            if (option != null && option.TryGetValue(out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private void EnsureConfigured()
        {
            EnsureLists();
            if (string.IsNullOrEmpty(TypeId))
            {
                ConfigureDefaultDecorator();
            }

            ApplyDefaultMetadata();
        }

        private void EnsureLists()
        {
            if (Inputs == null)
            {
                Inputs = new List<BehaviorTreeVisualInputPortData>();
            }

            if (InputBindings == null)
            {
                InputBindings = new List<BehaviorTreeVisualInputBindingData>();
            }
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

        private static string CreateDefaultDecoratorId(string typeId)
        {
            string prefix = string.IsNullOrEmpty(typeId) ? "bt_decorator" : typeId.Replace('.', '_').ToLowerInvariant();
            return prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static Dictionary<string, object> DeserializeProperties(string json)
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

        private static void SuppressEmbeddedInputValue(IPort inputPort)
        {
            if (inputPort == null)
            {
                return;
            }

            PropertyInfo optionsProperty = FindProperty(inputPort.GetType(), "Options");
            if (optionsProperty == null || optionsProperty.PropertyType == null || !optionsProperty.PropertyType.IsEnum)
            {
                return;
            }

            try
            {
                int options = Convert.ToInt32(optionsProperty.GetValue(inputPort, null));
                object noEmbeddedConstant = Enum.ToObject(optionsProperty.PropertyType, options | 1);
                optionsProperty.SetValue(inputPort, noEmbeddedConstant, null);

                object nodeModel = GetPropertyValue(inputPort, "NodeModel");
                MethodInfo updateConstant = FindMethod(nodeModel == null ? null : nodeModel.GetType(), "UpdateConstantForInput");
                if (updateConstant != null)
                {
                    updateConstant.Invoke(nodeModel, new object[] { inputPort, null, null });
                }
            }
            catch
            {
            }
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null)
            {
                return null;
            }

            PropertyInfo property = FindProperty(target.GetType(), propertyName);
            return property == null ? null : property.GetValue(target, null);
        }

        private static PropertyInfo FindProperty(Type type, string propertyName)
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

        private static MethodInfo FindMethod(Type type, string methodName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo method = current.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class BehaviorTreeVisualBlackboardKeyData
    {
        public string Name;
        public string Type;
        public bool HasDefaultValue;
        public string DefaultValueJson;
        public bool Exposed;
        public bool Persistent;
        public string Description;
    }

    [Serializable]
    public sealed class BehaviorTreeVisualNodeData
    {
        public string Id;
        public string TypeId;
        public float X;
        public float Y;
        public List<string> Children = new List<string>();
        public List<string> Decorators = new List<string>();
        public List<string> Services = new List<string>();
        public List<BehaviorTreeVisualInputBindingData> InputBindings = new List<BehaviorTreeVisualInputBindingData>();
        public string PropertiesJson = "{}";
    }

    [Serializable]
    public sealed class BehaviorTreeVisualInputPortData
    {
        public string Id;
        public string Type;
        public string DisplayName;
        public bool AllowInlineValue;
    }

    [Serializable]
    public sealed class BehaviorTreeVisualInputBindingData
    {
        public string InputId;
        public string BlackboardKey;
    }

    [Serializable]
    public sealed class BehaviorTreeVisualDecoratorData
    {
        public string Id;
        public string TypeId;
        public List<BehaviorTreeVisualInputBindingData> InputBindings = new List<BehaviorTreeVisualInputBindingData>();
        public string PropertiesJson = "{}";
    }

    [Serializable]
    public sealed class BehaviorTreeVisualServiceData
    {
        public string Id;
        public string TypeId;
        public float Interval;
        public float RandomDeviation;
        public string PropertiesJson = "{}";
    }

    internal static class BehaviorTreeVisualValueUtility
    {
        public static Type ToGraphType(string behaviorType)
        {
            if (string.IsNullOrEmpty(behaviorType))
            {
                return typeof(object);
            }

            switch (behaviorType)
            {
                case nameof(BehaviorTreeComparisonOperator):
                    return typeof(BehaviorTreeComparisonOperator);
                case "bool":
                    return typeof(bool);
                case "int":
                    return typeof(int);
                case "float":
                    return typeof(float);
                case "Vector2":
                    return typeof(Vector2);
                case "Vector3":
                    return typeof(Vector3);
                case "GameObject":
                    return typeof(GameObject);
                case "Transform":
                    return typeof(Transform);
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                case BlueprintVariableTypeRegistry.BlueprintRefTypeId:
                default:
                    return typeof(string);
            }
        }

        public static object ConvertForGraphField(object value, string behaviorType)
        {
            Type graphType = ToGraphType(behaviorType);
            if (graphType == typeof(object))
            {
                return value;
            }

            if (graphType == typeof(string))
            {
                return value == null ? string.Empty : Convert.ToString(value);
            }

            if (graphType.IsEnum)
            {
                return ConvertToEnumValue(value, graphType);
            }

            if (graphType == typeof(bool))
            {
                return BlueprintTypeUtility.ConvertValue(value, false);
            }

            if (graphType == typeof(int))
            {
                return BlueprintTypeUtility.ConvertValue(value, 0);
            }

            if (graphType == typeof(float))
            {
                return BlueprintTypeUtility.ConvertValue(value, 0f);
            }

            if (graphType == typeof(Vector2))
            {
                return BlueprintTypeUtility.ToVector2(value, Vector2.zero);
            }

            if (graphType == typeof(Vector3))
            {
                return BlueprintTypeUtility.ToVector3(value, Vector3.zero);
            }

            return value;
        }

        public static bool TryReadPortValue(IPort port, string behaviorType, out object value)
        {
            Type graphType = ToGraphType(behaviorType);
            if (graphType == typeof(bool))
            {
                bool typed;
                if (port.TryGetValue(out typed))
                {
                    value = typed;
                    return true;
                }
            }
            else if (graphType == typeof(int))
            {
                int typed;
                if (port.TryGetValue(out typed))
                {
                    value = typed;
                    return true;
                }
            }
            else if (graphType == typeof(float))
            {
                float typed;
                if (port.TryGetValue(out typed))
                {
                    value = typed;
                    return true;
                }
            }
            else if (graphType == typeof(Vector2))
            {
                Vector2 typed;
                if (port.TryGetValue(out typed))
                {
                    value = BehaviorTreeValueUtility.NormalizeValueForJson(typed, behaviorType);
                    return true;
                }
            }
            else if (graphType == typeof(Vector3))
            {
                Vector3 typed;
                if (port.TryGetValue(out typed))
                {
                    value = BehaviorTreeValueUtility.NormalizeValueForJson(typed, behaviorType);
                    return true;
                }
            }
            else if (graphType == typeof(string))
            {
                string typed;
                if (port.TryGetValue(out typed))
                {
                    value = typed;
                    return true;
                }
            }
            else if (graphType.IsEnum)
            {
                return TryReadEnumPortValue(port, graphType, out value);
            }

            value = null;
            return false;
        }

        public static bool AreEquivalent(object left, object right, string behaviorType)
        {
            Type graphType = ToGraphType(behaviorType);
            if (graphType == typeof(bool))
            {
                return BlueprintTypeUtility.ConvertValue(left, false) == BlueprintTypeUtility.ConvertValue(right, false);
            }

            if (graphType == typeof(int))
            {
                return BlueprintTypeUtility.ConvertValue(left, 0) == BlueprintTypeUtility.ConvertValue(right, 0);
            }

            if (graphType == typeof(float))
            {
                return Mathf.Abs(BlueprintTypeUtility.ConvertValue(left, 0f) - BlueprintTypeUtility.ConvertValue(right, 0f)) <= 0.0001f;
            }

            if (graphType.IsEnum)
            {
                object leftEnum = ConvertToEnumValue(left, graphType);
                object rightEnum = ConvertToEnumValue(right, graphType);
                return Equals(leftEnum, rightEnum);
            }

            if (graphType == typeof(Vector2))
            {
                return Vector2.Distance(BlueprintTypeUtility.ToVector2(left, Vector2.zero), BlueprintTypeUtility.ToVector2(right, Vector2.zero)) <= 0.0001f;
            }

            if (graphType == typeof(Vector3))
            {
                return Vector3.Distance(BlueprintTypeUtility.ToVector3(left, Vector3.zero), BlueprintTypeUtility.ToVector3(right, Vector3.zero)) <= 0.0001f;
            }

            string leftText = left == null ? string.Empty : Convert.ToString(left);
            string rightText = right == null ? string.Empty : Convert.ToString(right);
            return string.Equals(leftText, rightText, StringComparison.Ordinal);
        }

        private static object ConvertToEnumValue(object value, Type enumType)
        {
            if (enumType == null || !enumType.IsEnum)
            {
                return value;
            }

            if (value == null)
            {
                return Activator.CreateInstance(enumType);
            }

            if (enumType.IsInstanceOfType(value))
            {
                return value;
            }

            string text = Convert.ToString(value);
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    return Enum.Parse(enumType, text, false);
                }
                catch
                {
                }
            }

            return Activator.CreateInstance(enumType);
        }

        private static bool TryReadEnumPortValue(IPort port, Type enumType, out object value)
        {
            value = null;
            if (port == null || enumType == null || !enumType.IsEnum)
            {
                return false;
            }

            MethodInfo method = typeof(BehaviorTreeVisualValueUtility).GetMethod(
                "TryReadEnumPortValueGeneric",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                return false;
            }

            object[] args = { port, null };
            try
            {
                bool success = (bool)method.MakeGenericMethod(enumType).Invoke(null, args);
                value = success && args[1] != null ? Convert.ToString(args[1]) : null;
                return success;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadEnumPortValueGeneric<TEnum>(IPort port, out object value)
            where TEnum : struct
        {
            TEnum typed;
            if (port.TryGetValue(out typed))
            {
                value = typed.ToString();
                return true;
            }

            value = null;
            return false;
        }
    }
}
