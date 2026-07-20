using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class BlueprintExecutionContext
    {
        private readonly List<ValueCacheRecord> _valueCache = new List<ValueCacheRecord>();
        private readonly List<ValueAddressRecord> _evaluationStack = new List<ValueAddressRecord>();
        private readonly List<StateRecord> _state = new List<StateRecord>();
        private readonly Action<RuntimeNode, string> _executeFromOutput;
        private IBlueprintExecutionTraceSink _traceSink;
        private string _currentTraceEventName;
        private RuntimeNode _currentTraceNode;
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
        public IBlueprintExecutionTraceSink TraceSink
        {
            get { return _traceSink; }
            set { _traceSink = value; }
        }

        internal bool IsTraceEnabled
        {
            get { return _traceSink != null && _traceSink.IsEnabled; }
        }

        internal string CurrentTraceEventName
        {
            get { return _currentTraceEventName; }
        }

        internal RuntimeNode CurrentTraceNode
        {
            get { return _currentTraceNode; }
        }
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
            int portStableId = BlueprintStableId.FromString(portId);
            object value = GetInputValue(node, portStableId);
            if (value != null || node == null || node.Manifest == null ||
                node.FindInput(portStableId) != null || node.Properties.ContainsKey(portId))
            {
                return value;
            }

            BlueprintPropertySpec property = node.Manifest.FindProperty(portId);
            return property == null ? null : property.DefaultValue;
        }

        public object GetInputValue(RuntimeNode node, int portStableId)
        {
            if (node == null)
            {
                return null;
            }

            RuntimeInputRecord input = node.FindInput(portStableId);
            if (input != null)
            {
                if (input.IsConnected)
                {
                    return EvaluateOutput(input.SourceNodeIndex, input.SourcePortStableId, input.DebugSourcePortId);
                }
            }

            object propertyValue;
            if (node.Properties.TryGetValue(portStableId, out propertyValue)) return propertyValue;

            if (input != null && input.ConstantIndex >= 0)
            {
                return Blueprint.GetConstant(input.ConstantIndex);
            }

            if (node.Manifest != null)
            {
                RuntimePropertyRecord propertyRecord = FindPropertyRecord(node, portStableId);
                BlueprintPropertySpec property = propertyRecord == null ? null : node.Manifest.FindProperty(propertyRecord.DebugName);
                if (property != null)
                {
                    return property.DefaultValue;
                }
            }

            return null;
        }

        public object EvaluateOutput(BlueprintPortKey output)
        {
            RuntimeNode node = Blueprint.GetNode(output.NodeId);
            return node == null ? null : EvaluateOutput(node.StableIndex, BlueprintStableId.FromString(output.PortId), output.PortId);
        }

        public object EvaluateOutput(int nodeIndex, int portStableId, string debugPortId = null)
        {
            object cached;
            if (TryGetCachedValue(nodeIndex, portStableId, out cached))
            {
                return cached;
            }

            if (IsEvaluating(nodeIndex, portStableId))
            {
                Logger.Error("Value dependency cycle while evaluating node " + nodeIndex + ", port " + portStableId + ".");
                return null;
            }

            RuntimeNode sourceNode = Blueprint.GetNode(nodeIndex);
            if (sourceNode == null || sourceNode.Executor == null)
            {
                Logger.Error("Cannot evaluate missing value node index " + nodeIndex + ".");
                return null;
            }

            _evaluationStack.Add(new ValueAddressRecord { NodeIndex = nodeIndex, PortStableId = portStableId });
            object value = sourceNode.Executor.Evaluate(this, sourceNode, debugPortId ?? FindDebugOutputPort(sourceNode, portStableId));
            _evaluationStack.RemoveAt(_evaluationStack.Count - 1);
            _valueCache.Add(new ValueCacheRecord { NodeIndex = nodeIndex, PortStableId = portStableId, Value = value });
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

        /// <summary>
        /// Writes a declared runtime variable and records the mutation when diagnostics tracing is
        /// enabled. Blueprint executors should prefer this over Variables.Set directly.
        /// </summary>
        public void SetVariable(string name, object value)
        {
            Variables.Set(name, value);
            BlueprintPersistenceRuntime.MarkDirty(this, name);
            RecordTrace(BlueprintTraceRecordKind.VariableWrite, "", "written", value, name);
        }

        public void SetVariable(int variableIndex, object value)
        {
            IBlueprintIndexedVariableStore indexed = Variables as IBlueprintIndexedVariableStore;
            if (indexed == null)
            {
                return;
            }

            indexed.Set(variableIndex, value);
            BlueprintVariableDeclaration declaration = indexed.GetDeclaration(variableIndex);
            if (declaration != null)
            {
                BlueprintPersistenceRuntime.MarkDirty(this, declaration.Name);
            }
            RecordTrace(BlueprintTraceRecordKind.VariableWrite, "", "written", value, declaration == null ? variableIndex.ToString() : declaration.Name);
        }

        internal void SetTraceExecutionState(string eventName, RuntimeNode node)
        {
            _currentTraceEventName = eventName;
            _currentTraceNode = node;
        }

        internal void RecordTrace(
            BlueprintTraceRecordKind kind,
            string portId = "",
            string status = "",
            object value = null,
            string message = "")
        {
            if (!IsTraceEnabled)
            {
                return;
            }

            IBlueprintInstance instance = Instance;
            RuntimeNode node = _currentTraceNode;
            _traceSink.Record(new BlueprintTraceRecord
            {
                Kind = kind,
                Frame = UnityEngine.Time.frameCount,
                TimeSeconds = UnityEngine.Time.realtimeSinceStartup,
                InstancePath = BuildInstancePath(instance),
                BlueprintPath = instance == null ? string.Empty : instance.SourcePath ?? string.Empty,
                EventName = _currentTraceEventName ?? string.Empty,
                NodeId = node == null ? string.Empty : node.Id ?? string.Empty,
                TypeId = node == null ? string.Empty : node.TypeId ?? string.Empty,
                PortId = portId ?? string.Empty,
                Status = status ?? string.Empty,
                Value = value,
                Message = message ?? string.Empty
            });
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
            object ignored;
            return TryGetState(key, out ignored);
        }

        public bool TryGetState(string key, out object value)
        {
            int stableId = BlueprintStableId.FromString(key);
            for (int i = 0; i < _state.Count; i++)
            {
                if (_state[i].StableId == stableId && string.Equals(_state[i].DebugKey, key, StringComparison.Ordinal))
                {
                    value = _state[i].Value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        public void SetState(string key, object value)
        {
            int stableId = BlueprintStableId.FromString(key);
            for (int i = 0; i < _state.Count; i++)
            {
                if (_state[i].StableId == stableId && string.Equals(_state[i].DebugKey, key, StringComparison.Ordinal))
                {
                    _state[i].Value = value;
                    return;
                }
            }
            _state.Add(new StateRecord { StableId = stableId, DebugKey = key, Value = value });
        }

        public void RemoveState(string key)
        {
            int stableId = BlueprintStableId.FromString(key);
            for (int i = _state.Count - 1; i >= 0; i--)
            {
                if (_state[i].StableId == stableId && string.Equals(_state[i].DebugKey, key, StringComparison.Ordinal)) _state.RemoveAt(i);
            }
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

        private bool TryGetCachedValue(int nodeIndex, int portStableId, out object value)
        {
            for (int i = 0; i < _valueCache.Count; i++)
            {
                ValueCacheRecord record = _valueCache[i];
                if (record.NodeIndex == nodeIndex && record.PortStableId == portStableId)
                {
                    value = record.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        private bool IsEvaluating(int nodeIndex, int portStableId)
        {
            for (int i = 0; i < _evaluationStack.Count; i++)
            {
                if (_evaluationStack[i].NodeIndex == nodeIndex && _evaluationStack[i].PortStableId == portStableId) return true;
            }
            return false;
        }

        private static RuntimePropertyRecord FindPropertyRecord(RuntimeNode node, int stableId)
        {
            IReadOnlyList<RuntimePropertyRecord> records = node.Properties.Records;
            for (int i = 0; i < records.Count; i++) if (records[i].StableId == stableId) return records[i];
            return null;
        }

        private static string FindDebugOutputPort(RuntimeNode node, int stableId)
        {
            RuntimeExecOutputRecord exec = node.FindExecOutput(stableId);
            if (exec != null) return exec.DebugPortId;
            for (int i = 0; i < node.InputRecords.Count; i++)
            {
                RuntimeInputRecord input = node.InputRecords[i];
                if (input.SourcePortStableId == stableId && !string.IsNullOrEmpty(input.DebugSourcePortId)) return input.DebugSourcePortId;
            }
            return stableId.ToString();
        }

        private sealed class ValueAddressRecord { public int NodeIndex; public int PortStableId; }
        private sealed class ValueCacheRecord { public int NodeIndex; public int PortStableId; public object Value; }
        private sealed class StateRecord { public int StableId; public string DebugKey; public object Value; }

        private static string BuildInstancePath(IBlueprintInstance instance)
        {
            if (instance == null)
            {
                return string.Empty;
            }

            var names = new List<string>();
            IBlueprintInstance current = instance;
            while (current != null)
            {
                names.Add(current.InstanceName ?? string.Empty);
                current = current.OwnerInstance;
            }

            names.Reverse();
            return string.Join("/", names);
        }
    }
}
