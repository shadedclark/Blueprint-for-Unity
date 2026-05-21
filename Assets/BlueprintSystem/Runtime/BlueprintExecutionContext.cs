using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class BlueprintExecutionContext
    {
        private readonly Dictionary<BlueprintPortKey, object> _valueCache = new Dictionary<BlueprintPortKey, object>();
        private readonly HashSet<BlueprintPortKey> _evaluationStack = new HashSet<BlueprintPortKey>();
        private readonly Dictionary<string, object> _state = new Dictionary<string, object>();
        private readonly Action<RuntimeNode, string> _executeFromOutput;
        private int _executionGeneration = 1;

        public RuntimeBlueprint Blueprint { get; private set; }
        public IBlueprintInstance Instance { get; private set; }
        public IBlueprintInstance OwnerInstance { get; private set; }
        public GameObject Owner { get; private set; }
        public Component OwnerComponent { get; private set; }
        public IBlueprintBindingResolver BindingResolver { get; private set; }
        public IBlueprintVariableStore Variables { get; private set; }
        public IBlueprintEventBus EventBus { get; private set; }
        public IBlueprintLogger Logger { get; private set; }
        public string CurrentExecInputPortId { get; private set; }
        public int ExecutionGeneration
        {
            get { return _executionGeneration; }
        }

        public BlueprintExecutionContext(
            RuntimeBlueprint blueprint,
            GameObject owner,
            Component ownerComponent,
            IBlueprintBindingResolver bindingResolver,
            IBlueprintVariableStore variables,
            IBlueprintEventBus eventBus,
            IBlueprintLogger logger,
            Action<RuntimeNode, string> executeFromOutput = null,
            IBlueprintInstance instance = null,
            IBlueprintInstance ownerInstance = null)
        {
            Blueprint = blueprint;
            Instance = instance;
            OwnerInstance = ownerInstance;
            Owner = owner;
            OwnerComponent = ownerComponent;
            BindingResolver = bindingResolver ?? new NullBlueprintBindingResolver();
            Variables = variables ?? new DictionaryBlueprintVariableStore(blueprint);
            EventBus = eventBus;
            Logger = logger ?? new UnityBlueprintLogger();
            _executeFromOutput = executeFromOutput;
        }

        public T GetInputValue<T>(RuntimeNode node, string portId, T defaultValue)
        {
            object value = GetInputValue(node, portId);
            return BlueprintTypeUtility.ConvertValue(value, defaultValue);
        }

        public object GetInputValue(RuntimeNode node, string portId)
        {
            RuntimeEdge edge;
            if (Blueprint.ValueInputs.TryGetValue(new BlueprintPortKey(node.Id, portId), out edge))
            {
                return EvaluateOutput(edge.From);
            }

            object propertyValue;
            if (node.Properties.TryGetValue(portId, out propertyValue))
            {
                return propertyValue;
            }

            if (node.Manifest != null)
            {
                BlueprintPropertySpec property = node.Manifest.FindProperty(portId);
                if (property != null)
                {
                    return property.DefaultValue;
                }
            }

            return null;
        }

        public object EvaluateOutput(BlueprintPortKey output)
        {
            object cached;
            if (_valueCache.TryGetValue(output, out cached))
            {
                return cached;
            }

            if (_evaluationStack.Contains(output))
            {
                Logger.Error("Value dependency cycle while evaluating " + output + ".");
                return null;
            }

            RuntimeNode sourceNode = Blueprint.GetNode(output.NodeId);
            if (sourceNode == null || sourceNode.Executor == null)
            {
                Logger.Error("Cannot evaluate missing value node " + output.NodeId + ".");
                return null;
            }

            _evaluationStack.Add(output);
            object value = sourceNode.Executor.Evaluate(this, sourceNode, output.PortId);
            _evaluationStack.Remove(output);
            _valueCache[output] = value;
            return value;
        }

        public void ClearValueCache()
        {
            _valueCache.Clear();
        }

        public void SetCurrentExecInputPort(string inputPortId)
        {
            CurrentExecInputPortId = inputPortId;
        }

        public void SetLoopValue(RuntimeNode node, string outputPortId, object value)
        {
            if (node == null || string.IsNullOrEmpty(outputPortId))
            {
                return;
            }

            SetState(CreateLoopValueKey(node, outputPortId), value);
        }

        public bool TryGetLoopValue(RuntimeNode node, string outputPortId, out object value)
        {
            value = null;
            if (node == null || string.IsNullOrEmpty(outputPortId))
            {
                return false;
            }

            return TryGetState(CreateLoopValueKey(node, outputPortId), out value);
        }

        public void ClearLoopValues(RuntimeNode node)
        {
            if (node == null)
            {
                return;
            }

            RemoveState(CreateLoopValueKey(node, "arrayElement"));
            RemoveState(CreateLoopValueKey(node, "arrayIndex"));
        }

        public void ExecuteFromOutput(RuntimeNode node, string outputPortId)
        {
            if (_executeFromOutput == null)
            {
                Logger.Warning("No blueprint execution scheduler is available for output '" + outputPortId + "'.");
                return;
            }

            _executeFromOutput(node, outputPortId);
        }

        public bool HasState(string key)
        {
            return _state.ContainsKey(key);
        }

        public bool TryGetState(string key, out object value)
        {
            return _state.TryGetValue(key, out value);
        }

        public void SetState(string key, object value)
        {
            _state[key] = value;
        }

        public void RemoveState(string key)
        {
            _state.Remove(key);
        }

        public void InvalidateScheduledExecution()
        {
            unchecked
            {
                _executionGeneration++;
                if (_executionGeneration == 0)
                {
                    _executionGeneration = 1;
                }
            }
        }

        public bool IsExecutionGenerationCurrent(int generation)
        {
            return _executionGeneration == generation;
        }

        private static string CreateLoopValueKey(RuntimeNode node, string outputPortId)
        {
            return "loopValue:" + node.Id + ":" + outputPortId;
        }
    }
}
