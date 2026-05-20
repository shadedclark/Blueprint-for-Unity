using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    [Serializable]
    public class BlueprintVisualNode : Node
    {
        private const string NodeIdOptionName = "__bp_node_id";
        private const string TypeLabelOptionName = "__bp_type_label";

        public string NodeId;
        public string TypeId;
        public string Title;
        public string Category;
        public string Description;
        public List<BlueprintVisualPortData> Inputs = new List<BlueprintVisualPortData>();
        public List<BlueprintVisualPortData> Outputs = new List<BlueprintVisualPortData>();
        public List<BlueprintVisualPropertyData> Properties = new List<BlueprintVisualPropertyData>();

        public string ReadNodeId()
        {
            EnsureConfigured();
            INodeOption option = GetNodeOptionByName(NodeIdOptionName);
            string value;
            if (option != null && option.TryGetValue(out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return NodeId;
        }

        public string ReadTypeId()
        {
            EnsureConfigured();
            return TypeId;
        }

        public bool TryReadPropertyValue(BlueprintVisualPropertyData property, out object value)
        {
            EnsureConfigured();
            if (property != null && property.ShowInInspectorOnly)
            {
                INodeOption inspectorOption = GetNodeOptionByName(property.Id);
                if (inspectorOption != null && BlueprintVisualValueUtility.TryReadOptionValue(inspectorOption, property.Type, out value))
                {
                    return true;
                }
            }

            IPort inputPort = SafeGetInputPort(property.Id);
            if (inputPort != null && BlueprintVisualValueUtility.TryReadPortValue(inputPort, property.Type, out value))
            {
                return true;
            }

            INodeOption option = GetNodeOptionByName(property.Id);
            if (option != null && BlueprintVisualValueUtility.TryReadOptionValue(option, property.Type, out value))
            {
                return true;
            }

            if (property.HasValue)
            {
                value = BlueprintVisualValueUtility.FromJson(property.JsonValue);
                return true;
            }

            value = null;
            return false;
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            EnsureConfigured();
            context.AddOption<string>(TypeLabelOptionName)
                .WithDisplayName("Type")
                .WithDefaultValue(string.IsNullOrEmpty(Title) ? TypeId : Title)
                .Delayed();

            context.AddOption<string>(NodeIdOptionName)
                .WithDisplayName("Node Id")
                .WithDefaultValue(NodeId ?? string.Empty)
                .ShowInInspectorOnly()
                .Delayed();

            EnsureLists();
            for (int i = 0; i < Properties.Count; i++)
            {
                BlueprintVisualPropertyData property = Properties[i];
                if (property == null || string.IsNullOrEmpty(property.Id) || (HasInput(property.Id) && !property.ShowInInspectorOnly))
                {
                    continue;
                }

                AddPropertyOption(context, property);
            }
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            EnsureConfigured();
            for (int i = 0; i < Inputs.Count; i++)
            {
                AddInputPort(context, Inputs[i]);
            }

            for (int i = 0; i < Outputs.Count; i++)
            {
                AddOutputPort(context, Outputs[i]);
            }
        }

        protected virtual void ConfigureDefaultNode()
        {
        }

        protected virtual void ApplyDefaultMetadata()
        {
        }

        protected void SetIdentity(string typeId, string title, string category, string description)
        {
            TypeId = typeId;
            Title = title;
            Category = category;
            Description = description;
            if (string.IsNullOrEmpty(NodeId))
            {
                NodeId = CreateDefaultNodeId(typeId);
            }
        }

        protected void AddExecInput(string id)
        {
            AddInput(id, "exec", null, false, null, false);
        }

        protected void AddExecOutput(string id, bool allowMultiple = false)
        {
            AddOutput(id, "exec", null, false, null, allowMultiple);
        }

        protected void AddValueInput(string id, string type, bool required, string source, string displayName = null)
        {
            AddInput(id, "value", type, required, source, false, displayName);
        }

        protected void AddValueOutput(string id, string type, string displayName = null)
        {
            AddOutput(id, "value", type, false, null, false, displayName);
        }

        protected void AddProperty(string id, string type, bool required, object defaultValue = null, string displayName = null, bool showInInspectorOnly = false)
        {
            Properties.Add(new BlueprintVisualPropertyData
            {
                Id = id,
                DisplayName = displayName,
                Type = type,
                Required = required,
                HasValue = defaultValue != null,
                JsonValue = defaultValue == null ? string.Empty : BlueprintVisualValueUtility.ToJson(defaultValue),
                ShowInInspectorOnly = showInInspectorOnly
            });
        }

        protected void SetPropertyInspectorOnly(string propertyId, bool showInInspectorOnly)
        {
            BlueprintVisualPropertyData property = FindProperty(propertyId);
            if (property != null)
            {
                property.ShowInInspectorOnly = showInInspectorOnly;
            }
        }

        private void AddInput(string id, string kind, string type, bool required, string source, bool allowMultiple)
        {
            AddInput(id, kind, type, required, source, allowMultiple, null);
        }

        private void AddInput(string id, string kind, string type, bool required, string source, bool allowMultiple, string displayName)
        {
            Inputs.Add(new BlueprintVisualPortData
            {
                Id = id,
                DisplayName = displayName,
                Kind = kind,
                Type = type,
                Required = required,
                Source = source,
                AllowMultiple = allowMultiple
            });
        }

        private void AddOutput(string id, string kind, string type, bool required, string source, bool allowMultiple)
        {
            AddOutput(id, kind, type, required, source, allowMultiple, null);
        }

        private void AddOutput(string id, string kind, string type, bool required, string source, bool allowMultiple, string displayName)
        {
            Outputs.Add(new BlueprintVisualPortData
            {
                Id = id,
                DisplayName = displayName,
                Kind = kind,
                Type = type,
                Required = required,
                Source = source,
                AllowMultiple = allowMultiple
            });
        }

        private void AddInputPort(IPortDefinitionContext context, BlueprintVisualPortData port)
        {
            if (port == null || string.IsNullOrEmpty(port.Id))
            {
                return;
            }

            if (IsExec(port))
            {
                context.AddInputPort(port.Id)
                    .WithDisplayName(GetDisplayName(port.DisplayName, port.Id))
                    .WithConnectorUI(PortConnectorUI.Arrowhead)
                    .Build();
                return;
            }

            Type graphType = BlueprintVisualValueUtility.ToGraphType(port.Type);
            ITypedInputPortBuilder builder = context.AddInputPort(port.Id)
                .WithDisplayName(GetDisplayName(port.DisplayName, port.Id))
                .WithConnectorUI(PortConnectorUI.Circle)
                .WithDataType(graphType);

            object defaultValue;
            if (TryGetDefaultValue(port.Id, port.Type, out defaultValue))
            {
                builder.WithDefaultValue(defaultValue);
            }

            IPort inputPort = builder.Build();
            if (IsInspectorOnlyProperty(port.Id))
            {
                SuppressEmbeddedInputValue(inputPort);
            }
        }

        private void AddOutputPort(IPortDefinitionContext context, BlueprintVisualPortData port)
        {
            if (port == null || string.IsNullOrEmpty(port.Id))
            {
                return;
            }

            if (IsExec(port))
            {
                context.AddOutputPort(port.Id)
                    .WithDisplayName(GetDisplayName(port.DisplayName, port.Id))
                    .WithConnectorUI(PortConnectorUI.Arrowhead)
                    .Build();
                return;
            }

            context.AddOutputPort(port.Id)
                .WithDisplayName(GetDisplayName(port.DisplayName, port.Id))
                .WithConnectorUI(PortConnectorUI.Circle)
                .WithDataType(BlueprintVisualValueUtility.ToGraphType(port.Type))
                .Build();
        }

        private void AddPropertyOption(IOptionDefinitionContext context, BlueprintVisualPropertyData property)
        {
            Type graphType = BlueprintVisualValueUtility.ToGraphType(property.Type);
            object defaultValue = property.HasValue
                ? BlueprintVisualValueUtility.ConvertForGraphField(BlueprintVisualValueUtility.FromJson(property.JsonValue), property.Type)
                : BlueprintVisualValueUtility.ConvertForGraphField(null, property.Type);

            IOptionBuilder builder = context.AddOption(property.Id, graphType)
                .WithDisplayName(GetDisplayName(property.DisplayName, property.Id))
                .WithDefaultValue(defaultValue)
                .Delayed();

            if (property.ShowInInspectorOnly)
            {
                builder.ShowInInspectorOnly();
            }
        }

        private bool TryGetDefaultValue(string propertyId, string type, out object value)
        {
            BlueprintVisualPropertyData property = FindProperty(propertyId);
            if (property != null && property.HasValue && !property.ShowInInspectorOnly)
            {
                value = BlueprintVisualValueUtility.ConvertForGraphField(BlueprintVisualValueUtility.FromJson(property.JsonValue), type);
                return true;
            }

            value = null;
            return false;
        }

        private bool IsInspectorOnlyProperty(string propertyId)
        {
            BlueprintVisualPropertyData property = FindProperty(propertyId);
            return property != null && property.ShowInInspectorOnly;
        }

        private BlueprintVisualPropertyData FindProperty(string propertyId)
        {
            for (int i = 0; i < Properties.Count; i++)
            {
                BlueprintVisualPropertyData property = Properties[i];
                if (property != null && property.Id == propertyId)
                {
                    return property;
                }
            }

            return null;
        }

        private bool HasInput(string portId)
        {
            for (int i = 0; i < Inputs.Count; i++)
            {
                BlueprintVisualPortData input = Inputs[i];
                if (input != null && input.Id == portId)
                {
                    return true;
                }
            }

            return false;
        }

        private IPort SafeGetInputPort(string portId)
        {
            try
            {
                return GetInputPortByName(portId);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsExec(BlueprintVisualPortData port)
        {
            return string.Equals(port.Kind, "exec", StringComparison.Ordinal);
        }

        private static string GetDisplayName(string displayName, string fallback)
        {
            return string.IsNullOrEmpty(displayName) ? fallback : displayName;
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
            if (Inputs == null)
            {
                Inputs = new List<BlueprintVisualPortData>();
            }

            if (Outputs == null)
            {
                Outputs = new List<BlueprintVisualPortData>();
            }

            if (Properties == null)
            {
                Properties = new List<BlueprintVisualPropertyData>();
            }
        }

        private static void SuppressEmbeddedInputValue(IPort inputPort)
        {
            if (inputPort == null)
            {
                return;
            }

            // Graph Toolkit does not expose NoEmbeddedConstant through the public port builder.
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
                MethodInfo[] methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name == methodName && method.GetParameters().Length == 3)
                    {
                        return method;
                    }
                }
            }

            return null;
        }

        private static string CreateDefaultNodeId(string typeId)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                return "node";
            }

            return typeId.Replace('.', '_').ToLowerInvariant();
        }
    }
}
