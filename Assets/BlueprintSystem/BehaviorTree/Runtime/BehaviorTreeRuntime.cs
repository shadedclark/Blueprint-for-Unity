using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BlueprintSystem
{
    public enum BehaviorTreeStatus
    {
        Success,
        Failure,
        Running
    }

    public enum BehaviorTreeComparisonOperator
    {
        IsSet,
        IsNotSet,
        IsTrue,
        IsFalse,
        Equals,
        NotEquals,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual
    }

    public sealed class RuntimeBehaviorTree
    {
        public string Name;
        public string SourceGuid;
        public string SourcePath;
        public string RootNodeId;
        public BehaviorTreeExecutorRegistry Registry;
        public readonly List<BehaviorTreeBlackboardKey> BlackboardSchema = new List<BehaviorTreeBlackboardKey>();
        public readonly Dictionary<string, RuntimeBehaviorTreeComponent> ComponentsByName = new Dictionary<string, RuntimeBehaviorTreeComponent>(StringComparer.Ordinal);
        public readonly Dictionary<string, RuntimeBehaviorTreeNode> NodesById = new Dictionary<string, RuntimeBehaviorTreeNode>(StringComparer.Ordinal);
        public readonly Dictionary<string, RuntimeBehaviorTreeDecorator> DecoratorsById = new Dictionary<string, RuntimeBehaviorTreeDecorator>(StringComparer.Ordinal);
        public readonly Dictionary<string, RuntimeBehaviorTreeService> ServicesById = new Dictionary<string, RuntimeBehaviorTreeService>(StringComparer.Ordinal);

        public RuntimeBehaviorTreeNode GetNode(string nodeId)
        {
            RuntimeBehaviorTreeNode node;
            return !string.IsNullOrEmpty(nodeId) && NodesById.TryGetValue(nodeId, out node) ? node : null;
        }

        public RuntimeBehaviorTreeComponent GetComponent(string componentName)
        {
            RuntimeBehaviorTreeComponent component;
            return !string.IsNullOrEmpty(componentName) && ComponentsByName.TryGetValue(componentName, out component)
                ? component
                : null;
        }
    }

    public sealed class RuntimeBehaviorTreeComponent
    {
        public string Name;
        public string BehaviorTreePath;
        public string BehaviorTreeGuid;
        public bool Required;
        public BehaviorTreeCompiledAsset CompiledBehaviorTree;
    }

    public sealed class RuntimeBehaviorTreeNode
    {
        public string Id;
        public string TypeId;
        public readonly List<string> Children = new List<string>();
        public readonly List<string> Decorators = new List<string>();
        public readonly List<string> Services = new List<string>();
        public readonly Dictionary<string, string> Inputs = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, object> Properties = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public sealed class RuntimeBehaviorTreeDecorator
    {
        public string Id;
        public string TypeId;
        public readonly Dictionary<string, string> Inputs = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, object> Properties = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public sealed class RuntimeBehaviorTreeService
    {
        public string Id;
        public string TypeId;
        public float Interval;
        public float RandomDeviation;
        public readonly Dictionary<string, object> Properties = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public sealed class BehaviorTreeRuntime
    {
        private readonly Dictionary<string, BehaviorTreeNodeRuntimeState> _nodeStates = new Dictionary<string, BehaviorTreeNodeRuntimeState>(StringComparer.Ordinal);
        private readonly Dictionary<string, BehaviorTreeDecoratorRuntimeState> _decoratorStates = new Dictionary<string, BehaviorTreeDecoratorRuntimeState>(StringComparer.Ordinal);
        private readonly Dictionary<string, BehaviorTreeServiceRuntimeState> _serviceStates = new Dictionary<string, BehaviorTreeServiceRuntimeState>(StringComparer.Ordinal);
        private readonly HashSet<string> _previousActiveNodes = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _activeNodes = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _activePath = new List<string>();
        private float _elapsedTime;
        private int _tickIndex;

        public BehaviorTreeRuntime(RuntimeBehaviorTree tree, GameObject owner, Component ownerComponent, BehaviorTreeBlackboard blackboard = null, IBlueprintLogger logger = null)
        {
            Tree = tree;
            Owner = owner;
            OwnerComponent = ownerComponent;
            Logger = logger ?? new UnityBlueprintLogger();
            Blackboard = blackboard ?? new BehaviorTreeBlackboard(tree == null ? null : tree.BlackboardSchema);
        }

        public RuntimeBehaviorTree Tree { get; private set; }
        public GameObject Owner { get; private set; }
        public Component OwnerComponent { get; private set; }
        public BehaviorTreeBlackboard Blackboard { get; private set; }
        public IBlueprintLogger Logger { get; private set; }
        public BehaviorTreeStatus LastStatus { get; private set; }
        public string LastAbortReason { get; private set; }
        public string LastFailureReason { get; private set; }
        public float TimeSeconds
        {
            get { return _elapsedTime; }
        }

        public BehaviorTreeStatus Tick(float deltaTime)
        {
            _elapsedTime += Mathf.Max(0f, deltaTime);
            _tickIndex++;
            _activeNodes.Clear();
            _activePath.Clear();

            if (Tree == null || string.IsNullOrEmpty(Tree.RootNodeId))
            {
                LastFailureReason = "Behavior tree has no root node.";
                LastStatus = BehaviorTreeStatus.Failure;
                return LastStatus;
            }

            BehaviorTreeExecutionContext context = new BehaviorTreeExecutionContext(this, deltaTime);
            LastStatus = TickNode(context, Tree.RootNodeId);
            ExitInactiveNodes(context);
            _previousActiveNodes.Clear();
            foreach (string nodeId in _activeNodes)
            {
                _previousActiveNodes.Add(nodeId);
            }

            return LastStatus;
        }

        public void Stop()
        {
            BehaviorTreeExecutionContext context = new BehaviorTreeExecutionContext(this, 0f);
            List<string> previous = new List<string>(_previousActiveNodes);
            for (int i = 0; i < previous.Count; i++)
            {
                AbortNode(context, previous[i], "Tree stopped.");
                ExitNodeServices(context, previous[i]);
            }

            _previousActiveNodes.Clear();
            _activeNodes.Clear();
            _activePath.Clear();
        }

        internal BehaviorTreeStatus TickNode(BehaviorTreeExecutionContext context, string nodeId)
        {
            RuntimeBehaviorTreeNode node = Tree.GetNode(nodeId);
            if (node == null)
            {
                LastFailureReason = "Missing behavior tree node '" + nodeId + "'.";
                return BehaviorTreeStatus.Failure;
            }

            if (!EvaluateDecorators(context, node))
            {
                SetNodeStatus(node.Id, BehaviorTreeStatus.Failure);
                return BehaviorTreeStatus.Failure;
            }

            MarkActive(node.Id);
            TickServices(context, node);

            IBehaviorTreeNodeExecutor executor;
            if (Tree.Registry == null || !Tree.Registry.TryGetNode(node.TypeId, out executor))
            {
                LastFailureReason = "No behavior tree executor registered for '" + node.TypeId + "'.";
                SetNodeStatus(node.Id, BehaviorTreeStatus.Failure);
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeStatus status = executor.Tick(context, node);
            SetNodeStatus(node.Id, status);
            if (status != BehaviorTreeStatus.Running)
            {
                GetNodeState(node.Id).RunningChildIndex = -1;
            }

            return status;
        }

        internal BehaviorTreeNodeRuntimeState GetNodeState(string nodeId)
        {
            BehaviorTreeNodeRuntimeState state;
            if (!_nodeStates.TryGetValue(nodeId, out state))
            {
                state = new BehaviorTreeNodeRuntimeState();
                state.RunningChildIndex = -1;
                _nodeStates[nodeId] = state;
            }

            return state;
        }

        internal BehaviorTreeServiceRuntimeState GetServiceState(string serviceId)
        {
            BehaviorTreeServiceRuntimeState state;
            if (!_serviceStates.TryGetValue(serviceId, out state))
            {
                state = new BehaviorTreeServiceRuntimeState();
                _serviceStates[serviceId] = state;
            }

            return state;
        }

        internal BehaviorTreeDecoratorRuntimeState GetDecoratorState(string decoratorId)
        {
            BehaviorTreeDecoratorRuntimeState state;
            if (!_decoratorStates.TryGetValue(decoratorId, out state))
            {
                state = new BehaviorTreeDecoratorRuntimeState();
                _decoratorStates[decoratorId] = state;
            }

            return state;
        }

        internal void MarkFailure(string reason)
        {
            LastFailureReason = reason;
        }

        private bool EvaluateDecorators(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            for (int i = 0; i < node.Decorators.Count; i++)
            {
                string decoratorId = node.Decorators[i];
                RuntimeBehaviorTreeDecorator decorator;
                if (!Tree.DecoratorsById.TryGetValue(decoratorId, out decorator))
                {
                    LastFailureReason = "Node '" + node.Id + "' references missing decorator '" + decoratorId + "'.";
                    return false;
                }

                IBehaviorTreeDecoratorExecutor executor;
                if (Tree.Registry == null || !Tree.Registry.TryGetDecorator(decorator.TypeId, out executor))
                {
                    LastFailureReason = "No behavior tree decorator registered for '" + decorator.TypeId + "'.";
                    return false;
                }

                bool allowed = executor.Evaluate(context, node, decorator);
                BehaviorTreeDecoratorRuntimeState state = GetDecoratorState(decorator.Id);
                state.HasResult = true;
                state.LastResult = allowed;
                state.LastTickTime = _elapsedTime;
                if (!allowed)
                {
                    LastFailureReason = "Decorator '" + decorator.Id + "' blocked node '" + node.Id + "'.";
                    return false;
                }
            }

            return true;
        }

        private void MarkActive(string nodeId)
        {
            if (_activeNodes.Add(nodeId))
            {
                _activePath.Add(nodeId);
            }
        }

        private void TickServices(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            for (int i = 0; i < node.Services.Count; i++)
            {
                string serviceId = node.Services[i];
                RuntimeBehaviorTreeService service;
                if (!Tree.ServicesById.TryGetValue(serviceId, out service))
                {
                    LastFailureReason = "Node '" + node.Id + "' references missing service '" + serviceId + "'.";
                    continue;
                }

                IBehaviorTreeServiceExecutor executor;
                if (Tree.Registry == null || !Tree.Registry.TryGetService(service.TypeId, out executor))
                {
                    LastFailureReason = "No behavior tree service registered for '" + service.TypeId + "'.";
                    continue;
                }

                BehaviorTreeServiceRuntimeState state = GetServiceState(service.Id);
                if (!state.Active)
                {
                    state.Active = true;
                    state.NextTickTime = _elapsedTime;
                    executor.OnEnter(context, node, service);
                }

                if (_elapsedTime + 0.0001f < state.NextTickTime)
                {
                    continue;
                }

                executor.Tick(context, node, service);
                state.LastTickTime = _elapsedTime;
                state.NextTickTime = _elapsedTime + GetServiceDelay(service);
            }
        }

        private float GetServiceDelay(RuntimeBehaviorTreeService service)
        {
            float interval = Mathf.Max(0f, service.Interval);
            float deviation = Mathf.Max(0f, service.RandomDeviation);
            if (deviation > 0f)
            {
                interval += UnityEngine.Random.Range(-deviation, deviation);
            }

            return Mathf.Max(0f, interval);
        }

        private void ExitInactiveNodes(BehaviorTreeExecutionContext context)
        {
            List<string> inactive = new List<string>();
            foreach (string nodeId in _previousActiveNodes)
            {
                if (!_activeNodes.Contains(nodeId))
                {
                    inactive.Add(nodeId);
                }
            }

            for (int i = 0; i < inactive.Count; i++)
            {
                AbortNode(context, inactive[i], "Node left active path.");
                ExitNodeServices(context, inactive[i]);
            }
        }

        private void AbortNode(BehaviorTreeExecutionContext context, string nodeId, string reason)
        {
            RuntimeBehaviorTreeNode node = Tree.GetNode(nodeId);
            if (node == null)
            {
                return;
            }

            BehaviorTreeNodeRuntimeState state = GetNodeState(nodeId);
            if (!state.HasStatus || state.LastStatus != BehaviorTreeStatus.Running)
            {
                return;
            }

            IBehaviorTreeNodeExecutor executor;
            if (Tree.Registry != null && Tree.Registry.TryGetNode(node.TypeId, out executor))
            {
                executor.Abort(context, node);
            }

            state.RunningChildIndex = -1;
            state.Data.Clear();
            LastAbortReason = reason + " (" + nodeId + ")";
            SetNodeStatus(nodeId, BehaviorTreeStatus.Failure);
        }

        internal void AbortSubtree(BehaviorTreeExecutionContext context, string nodeId, string reason)
        {
            if (string.IsNullOrEmpty(nodeId) || Tree == null)
            {
                return;
            }

            List<string> nodeIds = new List<string>();
            CollectSubtreeNodeIds(nodeId, nodeIds, new HashSet<string>(StringComparer.Ordinal));
            for (int i = nodeIds.Count - 1; i >= 0; i--)
            {
                string currentNodeId = nodeIds[i];
                AbortNode(context, currentNodeId, reason);
                ExitNodeServices(context, currentNodeId);
                RemoveActiveNode(currentNodeId);
            }
        }

        private void CollectSubtreeNodeIds(string nodeId, List<string> nodeIds, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(nodeId) || !visited.Add(nodeId))
            {
                return;
            }

            RuntimeBehaviorTreeNode node = Tree.GetNode(nodeId);
            if (node == null)
            {
                return;
            }

            nodeIds.Add(nodeId);
            for (int i = 0; i < node.Children.Count; i++)
            {
                CollectSubtreeNodeIds(node.Children[i], nodeIds, visited);
            }
        }

        private void RemoveActiveNode(string nodeId)
        {
            _activeNodes.Remove(nodeId);
            _activePath.RemoveAll(item => string.Equals(item, nodeId, StringComparison.Ordinal));
        }

        private void ExitNodeServices(BehaviorTreeExecutionContext context, string nodeId)
        {
            RuntimeBehaviorTreeNode node = Tree.GetNode(nodeId);
            if (node == null)
            {
                return;
            }

            for (int i = 0; i < node.Services.Count; i++)
            {
                string serviceId = node.Services[i];
                RuntimeBehaviorTreeService service;
                if (!Tree.ServicesById.TryGetValue(serviceId, out service))
                {
                    continue;
                }

                BehaviorTreeServiceRuntimeState state = GetServiceState(serviceId);
                if (!state.Active)
                {
                    continue;
                }

                IBehaviorTreeServiceExecutor executor;
                if (Tree.Registry != null && Tree.Registry.TryGetService(service.TypeId, out executor))
                {
                    executor.OnExit(context, node, service);
                }

                state.Active = false;
            }
        }

        private void SetNodeStatus(string nodeId, BehaviorTreeStatus status)
        {
            BehaviorTreeNodeRuntimeState state = GetNodeState(nodeId);
            state.HasStatus = true;
            state.LastStatus = status;
            state.LastTickTime = _elapsedTime;
            state.LastTickIndex = _tickIndex;
        }

        public BehaviorTreeDebugSnapshot CreateDebugSnapshot()
        {
            return CreateDebugSnapshot(new HashSet<BehaviorTreeRuntime>());
        }

        private BehaviorTreeDebugSnapshot CreateDebugSnapshot(HashSet<BehaviorTreeRuntime> visited)
        {
            BehaviorTreeDebugSnapshot snapshot = new BehaviorTreeDebugSnapshot();
            snapshot.TreeName = Tree == null ? null : Tree.Name;
            snapshot.SourceGuid = Tree == null ? null : Tree.SourceGuid;
            snapshot.SourcePath = Tree == null ? null : Tree.SourcePath;
            snapshot.TickIndex = _tickIndex;
            snapshot.TimeSeconds = _elapsedTime;
            snapshot.LastStatus = LastStatus;
            snapshot.ActivePath.AddRange(_activePath);
            snapshot.BlackboardValues = Blackboard == null ? new Dictionary<string, object>() : Blackboard.ToDictionary();
            snapshot.LastAbortReason = LastAbortReason;
            snapshot.LastFailureReason = LastFailureReason;

            if (!visited.Add(this))
            {
                snapshot.LastFailureReason = "Behavior tree debug snapshot recursion was stopped.";
                return snapshot;
            }

            foreach (KeyValuePair<string, BehaviorTreeNodeRuntimeState> pair in _nodeStates)
            {
                BehaviorTreeNodeRuntimeState state = pair.Value;
                if (state == null || !state.HasStatus)
                {
                    continue;
                }

                snapshot.NodeStatuses[pair.Key] = state.LastStatus.ToString();
                snapshot.NodeTickTimes[pair.Key] = state.LastTickTime;
                if (state.LastStatus == BehaviorTreeStatus.Running &&
                    Tree != null &&
                    Tree.GetNode(pair.Key) != null &&
                    BehaviorTreeNodeTypeUtility.IsTask(Tree.GetNode(pair.Key).TypeId))
                {
                    snapshot.RunningTaskNodeIds.Add(pair.Key);
                    if (string.IsNullOrEmpty(snapshot.RunningTaskNodeId))
                    {
                        snapshot.RunningTaskNodeId = pair.Key;
                    }
                }

                AddSubtreeSnapshots(snapshot, pair.Key, state, visited);
            }

            foreach (KeyValuePair<string, BehaviorTreeDecoratorRuntimeState> pair in _decoratorStates)
            {
                if (pair.Value != null && pair.Value.HasResult)
                {
                    snapshot.DecoratorResults[pair.Key] = pair.Value.LastResult;
                }
            }

            foreach (KeyValuePair<string, BehaviorTreeServiceRuntimeState> pair in _serviceStates)
            {
                BehaviorTreeServiceRuntimeState state = pair.Value;
                if (state == null)
                {
                    continue;
                }

                snapshot.ServiceStates[pair.Key] = new BehaviorTreeDebugServiceState
                {
                    Active = state.Active,
                    LastTickTime = state.LastTickTime,
                    NextTickTime = state.NextTickTime
                };
            }

            return snapshot;
        }

        private static void AddSubtreeSnapshots(
            BehaviorTreeDebugSnapshot snapshot,
            string nodeId,
            BehaviorTreeNodeRuntimeState state,
            HashSet<BehaviorTreeRuntime> visited)
        {
            if (snapshot == null || state == null || state.Data == null || string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in state.Data)
            {
                BehaviorTreeRuntime subtreeRuntime = pair.Value as BehaviorTreeRuntime;
                if (subtreeRuntime != null)
                {
                    snapshot.SubtreeSnapshots[nodeId] = subtreeRuntime.CreateDebugSnapshot(visited);
                    return;
                }
            }
        }
    }

    public sealed class BehaviorTreeExecutionContext
    {
        public BehaviorTreeExecutionContext(BehaviorTreeRuntime runtime, float deltaTime)
        {
            Runtime = runtime;
            DeltaTime = deltaTime;
        }

        public BehaviorTreeRuntime Runtime { get; private set; }
        public RuntimeBehaviorTree Tree
        {
            get { return Runtime.Tree; }
        }

        public BehaviorTreeBlackboard Blackboard
        {
            get { return Runtime.Blackboard; }
        }

        public GameObject Owner
        {
            get { return Runtime.Owner; }
        }

        public Component OwnerComponent
        {
            get { return Runtime.OwnerComponent; }
        }

        public IBlueprintLogger Logger
        {
            get { return Runtime.Logger; }
        }

        public float DeltaTime { get; private set; }
        public float TimeSeconds
        {
            get { return Runtime.TimeSeconds; }
        }

        public BehaviorTreeNodeRuntimeState GetNodeState(RuntimeBehaviorTreeNode node)
        {
            return Runtime.GetNodeState(node.Id);
        }

        public BehaviorTreeStatus TickChild(string childNodeId)
        {
            return Runtime.TickNode(this, childNodeId);
        }

        public void AbortChild(string childNodeId, string reason)
        {
            Runtime.AbortSubtree(this, childNodeId, reason);
        }
    }

    public sealed class BehaviorTreeNodeRuntimeState
    {
        public bool HasStatus;
        public BehaviorTreeStatus LastStatus;
        public float LastTickTime;
        public int LastTickIndex;
        public int RunningChildIndex = -1;
        public readonly Dictionary<string, object> Data = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public sealed class BehaviorTreeDecoratorRuntimeState
    {
        public bool HasResult;
        public bool LastResult;
        public float LastTickTime;
        public readonly Dictionary<string, object> Data = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public sealed class BehaviorTreeServiceRuntimeState
    {
        public bool Active;
        public float NextTickTime;
        public float LastTickTime;
    }

    public sealed class BehaviorTreeDebugSnapshot
    {
        public readonly List<string> ActivePath = new List<string>();
        public readonly List<string> RunningTaskNodeIds = new List<string>();
        public readonly Dictionary<string, string> NodeStatuses = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, float> NodeTickTimes = new Dictionary<string, float>(StringComparer.Ordinal);
        public readonly Dictionary<string, bool> DecoratorResults = new Dictionary<string, bool>(StringComparer.Ordinal);
        public readonly Dictionary<string, BehaviorTreeDebugServiceState> ServiceStates = new Dictionary<string, BehaviorTreeDebugServiceState>(StringComparer.Ordinal);
        public readonly Dictionary<string, BehaviorTreeDebugSnapshot> SubtreeSnapshots = new Dictionary<string, BehaviorTreeDebugSnapshot>(StringComparer.Ordinal);
        public Dictionary<string, object> BlackboardValues = new Dictionary<string, object>(StringComparer.Ordinal);
        public string TreeName;
        public string SourceGuid;
        public string SourcePath;
        public int TickIndex;
        public float TimeSeconds;
        public BehaviorTreeStatus LastStatus;
        public string RunningTaskNodeId;
        public string LastAbortReason;
        public string LastFailureReason;
    }

    public sealed class BehaviorTreeDebugServiceState
    {
        public bool Active;
        public float LastTickTime;
        public float NextTickTime;
    }

    public sealed class BehaviorTreeBlackboard
    {
        private readonly Dictionary<string, BehaviorTreeBlackboardKey> _schemaByName = new Dictionary<string, BehaviorTreeBlackboardKey>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _defaults = new Dictionary<string, object>(StringComparer.Ordinal);

        public BehaviorTreeBlackboard(IEnumerable<BehaviorTreeBlackboardKey> schema = null)
        {
            if (schema != null)
            {
                foreach (BehaviorTreeBlackboardKey key in schema)
                {
                    if (key == null || string.IsNullOrEmpty(key.Name))
                    {
                        continue;
                    }

                    _schemaByName[key.Name] = key;
                    _defaults[key.Name] = BehaviorTreeValueUtility.CoerceValue(key.DefaultValue, key.Type);
                }
            }

            ResetToDefaults();
        }

        public bool ContainsKey(string key)
        {
            return !string.IsNullOrEmpty(key) && (_schemaByName.ContainsKey(key) || _values.ContainsKey(key));
        }

        public bool IsSet(string key)
        {
            object value;
            return TryGetValue(key, out value) && value != null;
        }

        public object GetValue(string key)
        {
            object value;
            return TryGetValue(key, out value) ? value : null;
        }

        public bool TryGetValue(string key, out object value)
        {
            value = null;
            return !string.IsNullOrEmpty(key) && _values.TryGetValue(key, out value);
        }

        public T GetValue<T>(string key, T defaultValue)
        {
            object value;
            return TryGetValue(key, out value) ? BlueprintTypeUtility.ConvertValue(value, defaultValue) : defaultValue;
        }

        public void SetValue(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            BehaviorTreeBlackboardKey schema;
            if (_schemaByName.TryGetValue(key, out schema) && schema != null)
            {
                value = BehaviorTreeValueUtility.CoerceValue(value, schema.Type);
            }

            _values[key] = value;
        }

        public bool MergeSchema(IEnumerable<BehaviorTreeBlackboardKey> schema)
        {
            if (schema == null)
            {
                return true;
            }

            bool merged = true;
            foreach (BehaviorTreeBlackboardKey key in schema)
            {
                if (key == null || string.IsNullOrEmpty(key.Name))
                {
                    continue;
                }

                BehaviorTreeBlackboardKey existing;
                if (_schemaByName.TryGetValue(key.Name, out existing))
                {
                    if (!string.Equals(existing == null ? null : existing.Type, key.Type, StringComparison.Ordinal))
                    {
                        merged = false;
                    }

                    continue;
                }

                _schemaByName[key.Name] = key;
                object defaultValue = BehaviorTreeValueUtility.CoerceValue(key.DefaultValue, key.Type);
                _defaults[key.Name] = defaultValue;
                if (!_values.ContainsKey(key.Name))
                {
                    _values[key.Name] = defaultValue;
                }
            }

            return merged;
        }

        public void ClearValue(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                _values[key] = null;
            }
        }

        public void ResetToDefaults()
        {
            _values.Clear();
            foreach (KeyValuePair<string, object> pair in _defaults)
            {
                _values[pair.Key] = pair.Value;
            }
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>(_values, StringComparer.Ordinal);
        }
    }

    public static class BehaviorTreeValueUtility
    {
        public static bool IsKnownBlackboardType(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return true;
            }

            switch (type)
            {
                case "bool":
                case "int":
                case "float":
                case "string":
                case "Vector2":
                case "Vector3":
                case "GameObject":
                case "Transform":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                case BlueprintVariableTypeRegistry.BlueprintRefTypeId:
                    return true;
                default:
                    return false;
            }
        }

        public static object CoerceValue(object value, string type)
        {
            if (value == null || string.IsNullOrEmpty(type))
            {
                return value;
            }

            switch (type)
            {
                case "bool":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(bool), false);
                case "int":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(int), 0);
                case "float":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(float), 0f);
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    return BlueprintTypeUtility.ConvertValue(value, typeof(string), string.Empty);
                case "Vector2":
                    return value is Vector2 ? value : BlueprintTypeUtility.ToVector2(value, Vector2.zero);
                case "Vector3":
                    return value is Vector3 ? value : BlueprintTypeUtility.ToVector3(value, Vector3.zero);
                case "GameObject":
                    return ToGameObject(value);
                case "Transform":
                    return ToTransform(value);
                case BlueprintVariableTypeRegistry.BlueprintRefTypeId:
                    return value is BlueprintRef ? value : null;
                default:
                    return value;
            }
        }

        public static object NormalizeValueForJson(object value, string type)
        {
            if (value == null)
            {
                return null;
            }

            if (value is GameObject || value is Transform || value is Component || value is BlueprintRef)
            {
                return null;
            }

            if (value is Vector2)
            {
                Vector2 vector = (Vector2)value;
                return new List<object> { vector.x, vector.y };
            }

            if (value is Vector3)
            {
                Vector3 vector = (Vector3)value;
                return new List<object> { vector.x, vector.y, vector.z };
            }

            if (value.GetType().IsEnum)
            {
                return value.ToString();
            }

            return value;
        }

        public static bool TryGetVector3(object value, out Vector3 vector)
        {
            vector = Vector3.zero;
            if (value is Vector3)
            {
                vector = (Vector3)value;
                return true;
            }

            if (value is Vector2)
            {
                Vector2 vector2 = (Vector2)value;
                vector = new Vector3(vector2.x, 0f, vector2.y);
                return true;
            }

            Transform transform = ToTransform(value);
            if (transform != null)
            {
                vector = transform.position;
                return true;
            }

            GameObject gameObject = ToGameObject(value);
            if (gameObject != null)
            {
                vector = gameObject.transform.position;
                return true;
            }

            IList list = value as IList;
            if (list != null && list.Count >= 3)
            {
                try
                {
                    vector = new Vector3(
                        Convert.ToSingle(list[0], CultureInfo.InvariantCulture),
                        Convert.ToSingle(list[1], CultureInfo.InvariantCulture),
                        Convert.ToSingle(list[2], CultureInfo.InvariantCulture));
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        public static GameObject ToGameObject(object value)
        {
            GameObject gameObject = value as GameObject;
            if (gameObject != null)
            {
                return gameObject;
            }

            Component component = value as Component;
            if (component != null)
            {
                return component.gameObject;
            }

            Transform transform = value as Transform;
            return transform == null ? null : transform.gameObject;
        }

        public static Transform ToTransform(object value)
        {
            Transform transform = value as Transform;
            if (transform != null)
            {
                return transform;
            }

            GameObject gameObject = value as GameObject;
            if (gameObject != null)
            {
                return gameObject.transform;
            }

            Component component = value as Component;
            return component == null ? null : component.transform;
        }
    }

    internal static class BehaviorTreeNodeTypeUtility
    {
        public const string Root = "BT.Root";
        public const string Selector = "BT.Selector";
        public const string Sequence = "BT.Sequence";
        public const string Parallel = "BT.Parallel";
        public const string RandomSelector = "BT.RandomSelector";
        public const string PrioritySelector = "BT.PrioritySelector";
        public const string WeightedSelector = "BT.WeightedSelector";

        public static bool IsComposite(string typeId)
        {
            return typeId == Root ||
                   typeId == Selector ||
                   typeId == Sequence ||
                   typeId == Parallel ||
                   typeId == RandomSelector ||
                   typeId == PrioritySelector ||
                   typeId == WeightedSelector;
        }

        public static bool IsTask(string typeId)
        {
            return !IsComposite(typeId);
        }
    }
}
