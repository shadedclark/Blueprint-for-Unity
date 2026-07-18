using System;
using System.Collections;
using System.Collections.Generic;

namespace BlueprintSystem
{
    public static class BlueprintStableId
    {
        // FNV-1a gives compiled records a deterministic id on every Unity/runtime version.
        public static int FromString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    char character = value[i];
                    hash ^= (byte)character;
                    hash *= 16777619u;
                    hash ^= (byte)(character >> 8);
                    hash *= 16777619u;
                }

                return hash == 0u ? 1 : (int)hash;
            }
        }
    }

    public sealed class RuntimeBlueprint
    {
        public string Name;
        public readonly RuntimeNodeRecordCollection NodeRecords;
        public readonly RuntimeEventRecordCollection EventRecords;
        public readonly List<RuntimeConstantRecord> ConstantPool = new List<RuntimeConstantRecord>();
        public readonly List<BlueprintVariableDeclaration> Variables = new List<BlueprintVariableDeclaration>();
        public readonly List<BlueprintBindingDeclaration> Bindings = new List<BlueprintBindingDeclaration>();
        public readonly List<BlueprintComponentDeclaration> Components = new List<BlueprintComponentDeclaration>();

        // Source/debug compatibility views. They are record-backed and never build string dictionaries.
        public RuntimeNodeRecordCollection NodesById { get { return NodeRecords; } }
        public RuntimeEventRecordCollection EventEntries { get { return EventRecords; } }
        public RuntimeExecEdgeCollection ExecOutputs { get; private set; }
        public RuntimeValueEdgeCollection ValueInputs { get; private set; }

        public RuntimeBlueprint()
        {
            NodeRecords = new RuntimeNodeRecordCollection(this);
            EventRecords = new RuntimeEventRecordCollection(this);
            ExecOutputs = new RuntimeExecEdgeCollection(this);
            ValueInputs = new RuntimeValueEdgeCollection(this);
        }

        public RuntimeNode GetNode(int stableIndex)
        {
            return stableIndex >= 0 && stableIndex < NodeRecords.Count ? NodeRecords[stableIndex] : null;
        }

        public RuntimeNode GetNode(string nodeId)
        {
            return NodeRecords.Find(nodeId);
        }

        public bool TryGetEventNodeIndex(string eventName, out int nodeIndex)
        {
            return EventRecords.TryGetNodeIndex(eventName, out nodeIndex);
        }

        public bool HasEvent(string eventName)
        {
            return EventRecords.ContainsKey(eventName);
        }

        public IReadOnlyList<RuntimeExecTargetRecord> GetExecTargets(int nodeIndex, int outputPortStableId)
        {
            RuntimeNode node = GetNode(nodeIndex);
            RuntimeExecOutputRecord output = node == null ? null : node.FindExecOutput(outputPortStableId);
            return output == null ? null : output.Targets;
        }

        public bool HasExecTargets(int nodeIndex, int outputPortStableId)
        {
            IReadOnlyList<RuntimeExecTargetRecord> targets = GetExecTargets(nodeIndex, outputPortStableId);
            return targets != null && targets.Count > 0;
        }

        public List<RuntimeEdge> GetExecEdges(BlueprintPortKey output)
        {
            return ExecOutputs.Get(output);
        }

        public object GetConstant(int constantIndex)
        {
            return constantIndex >= 0 && constantIndex < ConstantPool.Count
                ? ConstantPool[constantIndex].Value
                : null;
        }

        public CompiledStructLayout GetStructLayout(int constantIndex)
        {
            return GetConstant(constantIndex) as CompiledStructLayout;
        }
    }

    public sealed class RuntimeNode
    {
        public int StableIndex = -1;
        public int StableId;
        public int ExecutorOpcode;
        public int VariableIndex = -1;
        public string Id;
        public string TypeId;
        public BlueprintNodeManifest Manifest;
        public IBlueprintNodeExecutor Executor;
        public CompiledBlueprintTarget CompiledTarget;
        public int CompiledTargetConstantIndex = -1;
        public int SpecializedConstantIndex = -1;
        public readonly RuntimePropertyRecordCollection Properties = new RuntimePropertyRecordCollection();
        public readonly List<RuntimeInputRecord> InputRecords = new List<RuntimeInputRecord>();
        public readonly List<RuntimeExecOutputRecord> ExecOutputRecords = new List<RuntimeExecOutputRecord>();

        public object GetProperty(string propertyId)
        {
            object value;
            return Properties.TryGetValue(propertyId, out value) ? value : null;
        }

        public RuntimeInputRecord FindInput(int portStableId)
        {
            for (int i = 0; i < InputRecords.Count; i++)
            {
                if (InputRecords[i].PortStableId == portStableId)
                {
                    return InputRecords[i];
                }
            }

            return null;
        }

        public RuntimeExecOutputRecord FindExecOutput(int portStableId)
        {
            for (int i = 0; i < ExecOutputRecords.Count; i++)
            {
                if (ExecOutputRecords[i].PortStableId == portStableId)
                {
                    return ExecOutputRecords[i];
                }
            }

            return null;
        }
    }

    public sealed class RuntimePropertyRecord
    {
        public int StableId;
        public string DebugName;
        public int ConstantIndex = -1;
        public object Value;
    }

    public sealed class RuntimeInputRecord
    {
        public int PortStableId;
        public string DebugPortId;
        public int SourceNodeIndex = -1;
        public int SourcePortStableId;
        public string DebugSourcePortId;
        public int ConstantIndex = -1;

        public bool IsConnected { get { return SourceNodeIndex >= 0; } }
    }

    public sealed class RuntimeExecOutputRecord
    {
        public int PortStableId;
        public string DebugPortId;
        public readonly List<RuntimeExecTargetRecord> Targets = new List<RuntimeExecTargetRecord>();
    }

    public sealed class RuntimeExecTargetRecord
    {
        public int NodeIndex = -1;
        public int InputPortStableId;
        public string DebugInputPortId;
    }

    public sealed class RuntimeEventRecord
    {
        public int StableId;
        public string DebugName;
        public int NodeIndex = -1;
        public string DebugNodeId;
    }

    public sealed class RuntimeConstantRecord
    {
        public int StableId;
        public string Kind;
        public object Value;
    }

    public sealed class RuntimeNodeRecordCollection : IEnumerable<RuntimeNode>
    {
        private readonly RuntimeBlueprint _owner;
        private readonly List<RuntimeNode> _records = new List<RuntimeNode>();

        internal RuntimeNodeRecordCollection(RuntimeBlueprint owner) { _owner = owner; }
        public int Count { get { return _records.Count; } }
        public IEnumerable<RuntimeNode> Values { get { return _records; } }
        public RuntimeNode this[int index] { get { return _records[index]; } }
        public RuntimeNode this[string nodeId]
        {
            get { return Find(nodeId); }
            set
            {
                RuntimeNode existing = Find(nodeId);
                if (existing != null)
                {
                    int index = existing.StableIndex;
                    Prepare(value, nodeId, index);
                    _records[index] = value;
                    return;
                }

                Add(nodeId, value);
            }
        }

        public void Add(string nodeId, RuntimeNode node)
        {
            if (node == null) return;
            Prepare(node, nodeId, _records.Count);
            _records.Add(node);
        }

        public void Add(RuntimeNode node) { Add(node == null ? null : node.Id, node); }
        public bool ContainsKey(string nodeId) { return Find(nodeId) != null; }

        public RuntimeNode Find(string nodeId)
        {
            int stableId = BlueprintStableId.FromString(nodeId);
            for (int i = 0; i < _records.Count; i++)
            {
                RuntimeNode node = _records[i];
                if (node != null && node.StableId == stableId && string.Equals(node.Id, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        public IEnumerator<RuntimeNode> GetEnumerator() { return _records.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        private static void Prepare(RuntimeNode node, string nodeId, int index)
        {
            node.Id = string.IsNullOrEmpty(node.Id) ? nodeId : node.Id;
            node.StableId = node.StableId == 0 ? BlueprintStableId.FromString(node.Id) : node.StableId;
            node.StableIndex = index;
            if (node.ExecutorOpcode == 0 && node.Executor != null)
            {
                node.ExecutorOpcode = BlueprintStableId.FromString(node.Executor.ExecutorId);
            }
        }
    }

    public sealed class RuntimePropertyRecordCollection : IDictionary<string, object>
    {
        private readonly List<RuntimePropertyRecord> _records = new List<RuntimePropertyRecord>();
        public int Count { get { return _records.Count; } }
        public bool IsReadOnly { get { return false; } }
        public ICollection<string> Keys
        {
            get
            {
                List<string> keys = new List<string>(_records.Count);
                for (int i = 0; i < _records.Count; i++) keys.Add(_records[i].DebugName);
                return keys;
            }
        }
        public ICollection<object> Values
        {
            get
            {
                List<object> values = new List<object>(_records.Count);
                for (int i = 0; i < _records.Count; i++) values.Add(_records[i].Value);
                return values;
            }
        }
        public IReadOnlyList<RuntimePropertyRecord> Records { get { return _records; } }
        public object this[string propertyId]
        {
            get { object value; return TryGetValue(propertyId, out value) ? value : null; }
            set { Set(propertyId, value, -1); }
        }

        public void Set(string propertyId, object value, int constantIndex)
        {
            int stableId = BlueprintStableId.FromString(propertyId);
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].StableId == stableId && string.Equals(_records[i].DebugName, propertyId, StringComparison.Ordinal))
                {
                    _records[i].Value = value;
                    _records[i].ConstantIndex = constantIndex;
                    return;
                }
            }

            _records.Add(new RuntimePropertyRecord { StableId = stableId, DebugName = propertyId, Value = value, ConstantIndex = constantIndex });
        }

        public bool TryGetValue(string propertyId, out object value)
        {
            return TryGetValue(BlueprintStableId.FromString(propertyId), out value);
        }

        public bool ContainsKey(string propertyId)
        {
            object ignored;
            return TryGetValue(propertyId, out ignored);
        }

        public void Add(string key, object value)
        {
            if (ContainsKey(key)) throw new ArgumentException("A property record with id '" + key + "' already exists.", "key");
            Set(key, value, -1);
        }

        public bool Remove(string key)
        {
            int stableId = BlueprintStableId.FromString(key);
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].StableId == stableId && string.Equals(_records[i].DebugName, key, StringComparison.Ordinal))
                {
                    _records.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void Add(KeyValuePair<string, object> item) { Add(item.Key, item.Value); }
        public void Clear() { _records.Clear(); }
        public bool Contains(KeyValuePair<string, object> item)
        {
            object value;
            return TryGetValue(item.Key, out value) && Equals(value, item.Value);
        }
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException("array");
            for (int i = 0; i < _records.Count; i++) array[arrayIndex + i] = new KeyValuePair<string, object>(_records[i].DebugName, _records[i].Value);
        }
        public bool Remove(KeyValuePair<string, object> item) { return Contains(item) && Remove(item.Key); }

        public bool TryGetValue(int propertyStableId, out object value)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].StableId == propertyStableId)
                {
                    value = _records[i].Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            for (int i = 0; i < _records.Count; i++)
            {
                yield return new KeyValuePair<string, object>(_records[i].DebugName, _records[i].Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    public sealed class RuntimeEventRecordCollection
    {
        private readonly RuntimeBlueprint _owner;
        private readonly List<RuntimeEventRecord> _records = new List<RuntimeEventRecord>();
        internal RuntimeEventRecordCollection(RuntimeBlueprint owner) { _owner = owner; }
        public int Count { get { return _records.Count; } }
        public IEnumerable<string> Keys { get { for (int i = 0; i < _records.Count; i++) yield return _records[i].DebugName; } }
        public IReadOnlyList<RuntimeEventRecord> Records { get { return _records; } }
        public string this[string eventName]
        {
            get { string nodeId; return TryGetValue(eventName, out nodeId) ? nodeId : null; }
            set { Add(eventName, value); }
        }

        public void Add(string eventName, string nodeId)
        {
            RuntimeNode node = _owner.GetNode(nodeId);
            Add(new RuntimeEventRecord
            {
                StableId = BlueprintStableId.FromString(eventName),
                DebugName = eventName,
                NodeIndex = node == null ? -1 : node.StableIndex,
                DebugNodeId = nodeId
            });
        }

        public void Add(RuntimeEventRecord record)
        {
            if (record == null) return;
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].StableId == record.StableId && string.Equals(_records[i].DebugName, record.DebugName, StringComparison.Ordinal))
                {
                    _records[i] = record;
                    return;
                }
            }
            _records.Add(record);
        }

        public bool TryGetNodeIndex(string eventName, out int nodeIndex)
        {
            int stableId = BlueprintStableId.FromString(eventName);
            for (int i = 0; i < _records.Count; i++)
            {
                RuntimeEventRecord record = _records[i];
                if (record.StableId != stableId || !string.Equals(record.DebugName, eventName, StringComparison.Ordinal)) continue;
                nodeIndex = record.NodeIndex;
                if (nodeIndex < 0)
                {
                    RuntimeNode node = _owner.GetNode(record.DebugNodeId);
                    nodeIndex = node == null ? -1 : node.StableIndex;
                    record.NodeIndex = nodeIndex;
                }
                return nodeIndex >= 0;
            }
            nodeIndex = -1;
            return false;
        }

        public bool TryGetValue(string eventName, out string nodeId)
        {
            int nodeIndex;
            if (TryGetNodeIndex(eventName, out nodeIndex))
            {
                RuntimeNode node = _owner.GetNode(nodeIndex);
                nodeId = node == null ? null : node.Id;
                return node != null;
            }
            nodeId = null;
            return false;
        }

        public bool ContainsKey(string eventName) { int ignored; return TryGetNodeIndex(eventName, out ignored); }
    }

    public sealed class RuntimeExecEdgeCollection
    {
        private readonly RuntimeBlueprint _owner;
        internal RuntimeExecEdgeCollection(RuntimeBlueprint owner) { _owner = owner; }
        public int Count { get { int count = 0; foreach (RuntimeNode node in _owner.NodeRecords) count += node == null ? 0 : node.ExecOutputRecords.Count; return count; } }
        public IEnumerable<List<RuntimeEdge>> Values
        {
            get
            {
                foreach (RuntimeNode node in _owner.NodeRecords)
                {
                    if (node == null) continue;
                    for (int i = 0; i < node.ExecOutputRecords.Count; i++) yield return BuildEdges(node, node.ExecOutputRecords[i]);
                }
            }
        }
        public bool ContainsKey(BlueprintPortKey key) { List<RuntimeEdge> edges = Get(key); return edges != null && edges.Count > 0; }
        public List<RuntimeEdge> Get(BlueprintPortKey key)
        {
            RuntimeNode node = _owner.GetNode(key.NodeId);
            RuntimeExecOutputRecord output = node == null ? null : node.FindExecOutput(BlueprintStableId.FromString(key.PortId));
            return output == null ? null : BuildEdges(node, output);
        }
        private List<RuntimeEdge> BuildEdges(RuntimeNode node, RuntimeExecOutputRecord output)
        {
            List<RuntimeEdge> edges = new List<RuntimeEdge>();
            for (int i = 0; i < output.Targets.Count; i++)
            {
                RuntimeExecTargetRecord target = output.Targets[i];
                RuntimeNode targetNode = _owner.GetNode(target.NodeIndex);
                if (targetNode != null) edges.Add(new RuntimeEdge(new BlueprintPortKey(node.Id, output.DebugPortId), new BlueprintPortKey(targetNode.Id, target.DebugInputPortId)));
            }
            return edges;
        }
    }

    public sealed class RuntimeValueEdgeCollection
    {
        private readonly RuntimeBlueprint _owner;
        internal RuntimeValueEdgeCollection(RuntimeBlueprint owner) { _owner = owner; }
        public int Count { get { int count = 0; foreach (RuntimeNode node in _owner.NodeRecords) if (node != null) for (int i = 0; i < node.InputRecords.Count; i++) if (node.InputRecords[i].IsConnected) count++; return count; } }
        public IEnumerable<RuntimeEdge> Values { get { foreach (RuntimeNode node in _owner.NodeRecords) if (node != null) for (int i = 0; i < node.InputRecords.Count; i++) { RuntimeEdge edge; if (TryBuild(node, node.InputRecords[i], out edge)) yield return edge; } } }
        public bool TryGetValue(BlueprintPortKey key, out RuntimeEdge edge)
        {
            RuntimeNode node = _owner.GetNode(key.NodeId);
            RuntimeInputRecord input = node == null ? null : node.FindInput(BlueprintStableId.FromString(key.PortId));
            return TryBuild(node, input, out edge);
        }
        private bool TryBuild(RuntimeNode node, RuntimeInputRecord input, out RuntimeEdge edge)
        {
            edge = null;
            if (node == null || input == null || !input.IsConnected) return false;
            RuntimeNode source = _owner.GetNode(input.SourceNodeIndex);
            if (source == null) return false;
            edge = new RuntimeEdge(new BlueprintPortKey(source.Id, input.DebugSourcePortId), new BlueprintPortKey(node.Id, input.DebugPortId));
            return true;
        }
    }

    [Serializable]
    public sealed class CompiledBlueprintTarget
    {
        public int OwnerTraversal = -1;
        public int ComponentIndex = -1;
        public List<int> ComponentIndexPath = new List<int>();
        public string ExpectedSourceGuid;
        public string SourcePath;

        [NonSerialized] internal int RuntimeVersion = -1;
        [NonSerialized] internal int RuntimeRecordIndex = -1;
        public int BoundRuntimeVersion { get { return RuntimeVersion; } }
        public int BoundRuntimeRecordIndex { get { return RuntimeRecordIndex; } }
        internal void SetRuntimeHandle(int runtimeVersion, int runtimeRecordIndex) { RuntimeVersion = runtimeVersion; RuntimeRecordIndex = runtimeRecordIndex; }
        internal void ClearRuntimeHandle() { RuntimeVersion = -1; RuntimeRecordIndex = -1; }
    }

    internal sealed class ComponentRuntimeRecord
    {
        public int RecordIndex;
        public int OwnerRecordIndex;
        public int ComponentIndex;
        public string SourceGuid;
        public string SourcePath;
        public IBlueprintInstance Instance;
    }

    internal interface IBlueprintTargetHandleResolver
    {
        bool TryResolveBlueprintTarget(IBlueprintInstance requester, CompiledBlueprintTarget compiledTarget, string targetPath, out IBlueprintInstance instance, out bool ambiguous);
    }
}
