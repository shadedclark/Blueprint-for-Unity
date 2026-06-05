using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public enum BehaviorTreeTickMode
    {
        Update,
        FixedUpdate,
        Manual,
        Interval
    }

    public sealed class BehaviorTreeRunner : MonoBehaviour
    {
        [SerializeField] private BehaviorTreeCompiledAsset compiledBehaviorTree;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private BehaviorTreeTickMode tickMode = BehaviorTreeTickMode.Update;
        [SerializeField] private float maxTickRate = 10f;
        [SerializeField] private float intervalSeconds = 0.1f;
        [SerializeField] private bool logMissingAsset = true;
        [SerializeField] private List<BlueprintVariableOverride> blackboardOverrides = new List<BlueprintVariableOverride>();

        private BehaviorTreeRuntime _runtime;
        private bool _running;
        private float _tickAccumulator;

        public BehaviorTreeCompiledAsset CompiledBehaviorTree
        {
            get { return compiledBehaviorTree; }
            set { compiledBehaviorTree = value; }
        }

        public BehaviorTreeRuntime Runtime
        {
            get { return _runtime; }
        }

        public BehaviorTreeBlackboard Blackboard
        {
            get { return _runtime == null ? null : _runtime.Blackboard; }
        }

        public bool IsRunning
        {
            get { return _running; }
        }

        private void Start()
        {
            if (playOnStart)
            {
                StartTree();
            }
        }

        private void Update()
        {
            if (tickMode == BehaviorTreeTickMode.Update)
            {
                TickWithMaxRate(Time.deltaTime);
            }
            else if (tickMode == BehaviorTreeTickMode.Interval)
            {
                TickAtInterval(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (tickMode == BehaviorTreeTickMode.FixedUpdate)
            {
                TickTree(Time.fixedDeltaTime);
            }
        }

        private void OnDisable()
        {
            StopTree();
        }

        public bool StartTree()
        {
            if (compiledBehaviorTree == null)
            {
                if (logMissingAsset)
                {
                    Debug.LogWarning("[BehaviorTree] Missing compiled behavior tree asset on " + name + ".", this);
                }

                return false;
            }

            RuntimeBehaviorTree tree = compiledBehaviorTree.CreateRuntimeTree(BehaviorTreeExecutorRegistry.CreateDefault());
            BehaviorTreeBlackboard blackboard = new BehaviorTreeBlackboard(tree.BlackboardSchema);
            ApplyOverrides(blackboard, tree.BlackboardSchema);
            _runtime = new BehaviorTreeRuntime(tree, gameObject, this, blackboard, new UnityBlueprintLogger());
            _running = true;
            _tickAccumulator = 0f;
            return true;
        }

        public void StopTree()
        {
            if (_runtime != null)
            {
                _runtime.Stop();
            }

            _running = false;
        }

        public bool RestartTree()
        {
            StopTree();
            return StartTree();
        }

        public BehaviorTreeStatus ManualTick(float deltaTime)
        {
            return TickTree(deltaTime);
        }

        public BehaviorTreeStatus TickTree(float deltaTime)
        {
            if (!_running || _runtime == null)
            {
                return BehaviorTreeStatus.Failure;
            }

            return _runtime.Tick(deltaTime);
        }

        public bool TryGetBlackboardValue(string key, out object value)
        {
            value = null;
            return _runtime != null && _runtime.Blackboard != null && _runtime.Blackboard.TryGetValue(key, out value);
        }

        public object GetBlackboardValue(string key)
        {
            return _runtime == null || _runtime.Blackboard == null ? null : _runtime.Blackboard.GetValue(key);
        }

        public void SetBlackboardValue(string key, object value)
        {
            if (_runtime != null && _runtime.Blackboard != null)
            {
                _runtime.Blackboard.SetValue(key, value);
            }
        }

        public void ClearBlackboardValue(string key)
        {
            if (_runtime != null && _runtime.Blackboard != null)
            {
                _runtime.Blackboard.ClearValue(key);
            }
        }

        public BehaviorTreeDebugSnapshot GetDebugSnapshot()
        {
            return _runtime == null ? new BehaviorTreeDebugSnapshot() : _runtime.CreateDebugSnapshot();
        }

        private void TickWithMaxRate(float deltaTime)
        {
            if (maxTickRate <= 0f)
            {
                TickTree(deltaTime);
                return;
            }

            float tickInterval = 1f / Mathf.Max(0.001f, maxTickRate);
            _tickAccumulator += deltaTime;
            if (_tickAccumulator + 0.0001f < tickInterval)
            {
                return;
            }

            float consumed = _tickAccumulator;
            _tickAccumulator = 0f;
            TickTree(consumed);
        }

        private void TickAtInterval(float deltaTime)
        {
            float tickInterval = Mathf.Max(0.001f, intervalSeconds);
            _tickAccumulator += deltaTime;
            if (_tickAccumulator + 0.0001f < tickInterval)
            {
                return;
            }

            float consumed = _tickAccumulator;
            _tickAccumulator = 0f;
            TickTree(consumed);
        }

        private void ApplyOverrides(BehaviorTreeBlackboard blackboard, List<BehaviorTreeBlackboardKey> schema)
        {
            if (blackboard == null || blackboardOverrides == null)
            {
                return;
            }

            Dictionary<string, BehaviorTreeBlackboardKey> keysByName = new Dictionary<string, BehaviorTreeBlackboardKey>(StringComparer.Ordinal);
            for (int i = 0; i < schema.Count; i++)
            {
                BehaviorTreeBlackboardKey key = schema[i];
                if (key != null && !string.IsNullOrEmpty(key.Name))
                {
                    keysByName[key.Name] = key;
                }
            }

            for (int i = 0; i < blackboardOverrides.Count; i++)
            {
                BlueprintVariableOverride variableOverride = blackboardOverrides[i];
                if (variableOverride == null || string.IsNullOrEmpty(variableOverride.Name) || !IsOverrideEnabled(variableOverride))
                {
                    continue;
                }

                BehaviorTreeBlackboardKey key;
                if (!keysByName.TryGetValue(variableOverride.Name, out key))
                {
                    continue;
                }

                object value;
                if (!TryReadOverrideValue(variableOverride, key, out value))
                {
                    continue;
                }

                blackboard.SetValue(variableOverride.Name, value);
            }
        }

        private static bool TryReadOverrideValue(BlueprintVariableOverride variableOverride, BehaviorTreeBlackboardKey key, out object value)
        {
            value = null;
            string type = key == null ? variableOverride.Type : key.Type;
            if (type == "GameObject")
            {
                value = BehaviorTreeValueUtility.ToGameObject(variableOverride.ObjectValue);
                return value != null || string.IsNullOrEmpty(variableOverride.JsonValue);
            }

            if (type == "Transform")
            {
                value = BehaviorTreeValueUtility.ToTransform(variableOverride.ObjectValue);
                return value != null || string.IsNullOrEmpty(variableOverride.JsonValue);
            }

            if (string.IsNullOrEmpty(variableOverride.JsonValue))
            {
                return true;
            }

            try
            {
                value = BlueprintJson.Deserialize(variableOverride.JsonValue);
                return true;
            }
            catch (BlueprintJsonException)
            {
                if (type == "string" || type == BlueprintVariableTypeRegistry.BlueprintAssetTypeId)
                {
                    value = variableOverride.JsonValue;
                    return true;
                }
            }

            return false;
        }

        private static bool IsOverrideEnabled(BlueprintVariableOverride variableOverride)
        {
            if (variableOverride.Enabled)
            {
                return true;
            }

            return string.IsNullOrEmpty(variableOverride.VariableId) &&
                   !string.IsNullOrEmpty(variableOverride.Name) &&
                   (!string.IsNullOrEmpty(variableOverride.JsonValue) || variableOverride.ObjectValue != null);
        }
    }
}
