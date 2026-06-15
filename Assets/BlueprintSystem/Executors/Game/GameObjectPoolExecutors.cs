using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class BlueprintGameObjectPoolHost : MonoBehaviour
    {
        private readonly BlueprintGameObjectPoolRegistry _registry = new BlueprintGameObjectPoolRegistry();

        public BlueprintGameObjectPoolRegistry Registry
        {
            get { return _registry; }
        }

        private void OnDestroy()
        {
            _registry.ClearAll();
        }
    }

    public sealed class GameObjectPrewarmPoolExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "GameObject.PrewarmPool"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string poolId = GameObjectPoolExecutorUtility.GetPoolId(context, node);
            GameObject prefab = GameObjectPoolExecutorUtility.ResolveRequiredPrefab(context, node);
            if (prefab == null)
            {
                return BlueprintExecResult.Error("GameObject.PrewarmPool node '" + node.Id + "' could not resolve prefab.");
            }

            Transform parent;
            string parentError;
            if (!GameObjectPoolExecutorUtility.TryResolveOptionalParent(context, node, out parent, out parentError))
            {
                return BlueprintExecResult.Error("GameObject.PrewarmPool node '" + node.Id + "' " + parentError);
            }

            BlueprintGameObjectPoolRegistry registry = GameObjectPoolExecutorUtility.GetRegistry(context);
            string error;
            BlueprintGameObjectPool pool = registry.GetOrCreate(poolId, prefab, parent, out error);
            if (pool == null)
            {
                return BlueprintExecResult.Error("GameObject.PrewarmPool node '" + node.Id + "' " + error);
            }

            int capacity = Mathf.Max(0, context.GetInputValue(node, "capacity", 10));
            pool.Prewarm(capacity, parent);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameObjectAcquireFromPoolExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "GameObject.AcquireFromPool"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string poolId = GameObjectPoolExecutorUtility.GetPoolId(context, node);

            GameObject prefab;
            string prefabError;
            if (!GameObjectPoolExecutorUtility.TryResolveOptionalPrefab(context, node, out prefab, out prefabError))
            {
                return BlueprintExecResult.Error("GameObject.AcquireFromPool node '" + node.Id + "' " + prefabError);
            }

            Transform parent;
            string parentError;
            if (!GameObjectPoolExecutorUtility.TryResolveOptionalParent(context, node, out parent, out parentError))
            {
                return BlueprintExecResult.Error("GameObject.AcquireFromPool node '" + node.Id + "' " + parentError);
            }

            BlueprintGameObjectPoolRegistry registry = GameObjectPoolExecutorUtility.GetRegistry(context);
            BlueprintGameObjectPool pool;
            if (!registry.TryGet(poolId, out pool))
            {
                if (prefab == null)
                {
                    return BlueprintExecResult.Error("GameObject.AcquireFromPool node '" + node.Id + "' could not resolve prefab for new pool '" + poolId + "'.");
                }

                string createError;
                pool = registry.GetOrCreate(poolId, prefab, parent, out createError);
                if (pool == null)
                {
                    return BlueprintExecResult.Error("GameObject.AcquireFromPool node '" + node.Id + "' " + createError);
                }
            }
            else if (prefab != null && pool.Prefab != prefab)
            {
                return BlueprintExecResult.Error("GameObject.AcquireFromPool node '" + node.Id + "' pool '" + poolId + "' already uses a different prefab.");
            }
            else if (parent != null)
            {
                pool.SetParent(parent);
            }

            bool activate = context.GetInputValue(node, "activate", true);
            bool expandIfEmpty = context.GetInputValue(node, "expandIfEmpty", true);
            GameObject instance;
            bool success = pool.Acquire(activate, expandIfEmpty, parent, out instance);

            context.SetState(GameObjectPoolExecutorUtility.StateKey(node, "instance"), instance);
            context.SetState(GameObjectPoolExecutorUtility.StateKey(node, "transform"), instance == null ? null : instance.transform);
            context.SetState(GameObjectPoolExecutorUtility.StateKey(node, "success"), success);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            object value;
            if ((outputPortId == "instance" || outputPortId == "transform" || outputPortId == "success") &&
                context.TryGetState(GameObjectPoolExecutorUtility.StateKey(node, outputPortId), out value))
            {
                return value;
            }

            if (outputPortId == "success")
            {
                return false;
            }

            return null;
        }
    }

    public sealed class GameObjectReleaseToPoolExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "GameObject.ReleaseToPool"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string poolId = GameObjectPoolExecutorUtility.GetPoolId(context, node);
            GameObject target = context.GetInputValue(node, "target") as GameObject;
            if (target == null)
            {
                return BlueprintExecResult.Error("GameObject.ReleaseToPool node '" + node.Id + "' requires a runtime GameObject target.");
            }

            BlueprintGameObjectPool pool;
            if (!GameObjectPoolExecutorUtility.GetRegistry(context).TryGet(poolId, out pool))
            {
                return BlueprintExecResult.Error("GameObject.ReleaseToPool node '" + node.Id + "' could not find pool '" + poolId + "'.");
            }

            string error;
            bool released = pool.BeginRelease(target, out error);
            if (!string.IsNullOrEmpty(error))
            {
                return BlueprintExecResult.Error("GameObject.ReleaseToPool node '" + node.Id + "' " + error);
            }

            context.SetState(GameObjectPoolExecutorUtility.StateKey(node, "released"), released);
            context.SetState(GameObjectPoolExecutorUtility.StateKey(node, "target"), target);
            if (released)
            {
                bool deactivate = context.GetInputValue(node, "deactivate", true);
                try
                {
                    if (GameObjectPoolExecutorUtility.HasExecConnections(context, node, "reset"))
                    {
                        context.ExecuteFromOutput(node, "reset");
                    }
                }
                finally
                {
                    pool.CompleteRelease(target, deactivate);
                }
            }

            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            object value;
            if ((outputPortId == "released" || outputPortId == "target") &&
                context.TryGetState(GameObjectPoolExecutorUtility.StateKey(node, outputPortId), out value))
            {
                return value;
            }

            if (outputPortId == "released")
            {
                return false;
            }

            if (outputPortId == "target")
            {
                return null;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class GameObjectClearPoolExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "GameObject.ClearPool"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string poolId = GameObjectPoolExecutorUtility.GetPoolId(context, node);
            BlueprintGameObjectPoolRegistry registry = GameObjectPoolExecutorUtility.GetRegistry(context);
            int destroyedCount = registry.Clear(poolId);
            context.SetState(GameObjectPoolExecutorUtility.StateKey(node, "destroyedCount"), destroyedCount);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            object value;
            if (outputPortId == "destroyedCount" &&
                context.TryGetState(GameObjectPoolExecutorUtility.StateKey(node, outputPortId), out value))
            {
                return value;
            }

            return 0;
        }
    }

    public sealed class GameObjectGetPoolStatsExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "GameObject.GetPoolStats"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            BlueprintGameObjectPool pool;
            bool exists = GameObjectPoolExecutorUtility.TryGetPool(context, node, out pool);
            if (outputPortId == "exists")
            {
                return exists;
            }

            if (outputPortId == "activeCount")
            {
                return exists ? pool.ActiveCount : 0;
            }

            if (outputPortId == "availableCount")
            {
                return exists ? pool.AvailableCount : 0;
            }

            if (outputPortId == "managedCount")
            {
                return exists ? pool.ManagedCount : 0;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class GameObjectGetPoolActiveInstancesExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "GameObject.GetPoolActiveInstances"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "instances")
            {
                return base.Evaluate(context, node, outputPortId);
            }

            BlueprintGameObjectPool pool;
            return GameObjectPoolExecutorUtility.TryGetPool(context, node, out pool)
                ? pool.GetActiveInstances()
                : new List<GameObject>();
        }
    }

    public sealed class BlueprintGameObjectPoolRegistry
    {
        private readonly Dictionary<string, BlueprintGameObjectPool> _pools =
            new Dictionary<string, BlueprintGameObjectPool>(StringComparer.Ordinal);

        public bool TryGet(string poolId, out BlueprintGameObjectPool pool)
        {
            return _pools.TryGetValue(GameObjectPoolExecutorUtility.NormalizePoolId(poolId), out pool);
        }

        public BlueprintGameObjectPool GetOrCreate(string poolId, GameObject prefab, Transform parent, out string error)
        {
            string normalizedPoolId = GameObjectPoolExecutorUtility.NormalizePoolId(poolId);
            BlueprintGameObjectPool pool;
            if (_pools.TryGetValue(normalizedPoolId, out pool))
            {
                if (prefab != null && pool.Prefab != prefab)
                {
                    error = "pool '" + normalizedPoolId + "' already uses a different prefab.";
                    return null;
                }

                if (parent != null)
                {
                    pool.SetParent(parent);
                }

                error = null;
                return pool;
            }

            if (prefab == null)
            {
                error = "requires a prefab for new pool '" + normalizedPoolId + "'.";
                return null;
            }

            pool = new BlueprintGameObjectPool(normalizedPoolId, prefab, parent);
            _pools[normalizedPoolId] = pool;
            error = null;
            return pool;
        }

        public int Clear(string poolId)
        {
            string normalizedPoolId = GameObjectPoolExecutorUtility.NormalizePoolId(poolId);
            BlueprintGameObjectPool pool;
            if (!_pools.TryGetValue(normalizedPoolId, out pool))
            {
                return 0;
            }

            int destroyedCount = pool.Clear();
            _pools.Remove(normalizedPoolId);
            return destroyedCount;
        }

        public void ClearAll()
        {
            foreach (BlueprintGameObjectPool pool in _pools.Values)
            {
                if (pool != null)
                {
                    pool.Clear();
                }
            }

            _pools.Clear();
        }
    }

    public sealed class BlueprintGameObjectPool
    {
        private readonly string _poolId;
        private readonly List<GameObject> _available = new List<GameObject>();
        private readonly HashSet<GameObject> _active = new HashSet<GameObject>();
        private readonly HashSet<GameObject> _managed = new HashSet<GameObject>();
        private readonly HashSet<GameObject> _pendingRelease = new HashSet<GameObject>();
        private Transform _parent;

        public BlueprintGameObjectPool(string poolId, GameObject prefab, Transform parent)
        {
            _poolId = poolId;
            Prefab = prefab;
            _parent = parent;
        }

        public GameObject Prefab { get; private set; }

        public int ActiveCount
        {
            get
            {
                CleanupDestroyed();
                return _active.Count;
            }
        }

        public int AvailableCount
        {
            get
            {
                CleanupDestroyed();
                return _available.Count;
            }
        }

        public int ManagedCount
        {
            get
            {
                CleanupDestroyed();
                return _managed.Count;
            }
        }

        public void SetParent(Transform parent)
        {
            if (parent != null)
            {
                _parent = parent;
            }
        }

        public void Prewarm(int capacity, Transform parent)
        {
            SetParent(parent);
            CleanupDestroyed();
            while (_managed.Count < capacity)
            {
                GameObject instance = CreateInstance(false, parent);
                if (instance == null)
                {
                    return;
                }

                _available.Add(instance);
            }
        }

        public bool Acquire(bool activate, bool expandIfEmpty, Transform parent, out GameObject instance)
        {
            SetParent(parent);
            CleanupDestroyed();
            instance = PopAvailable();
            if (instance == null)
            {
                if (!expandIfEmpty)
                {
                    return false;
                }

                instance = CreateInstance(false, parent);
                if (instance == null)
                {
                    return false;
                }
            }

            _active.Add(instance);
            instance.SetActive(activate);
            return true;
        }

        public bool BeginRelease(GameObject target, out string error)
        {
            CleanupDestroyed();
            if (!_managed.Contains(target))
            {
                error = "target is not managed by pool '" + _poolId + "'.";
                return false;
            }

            if (_pendingRelease.Contains(target))
            {
                error = null;
                return false;
            }

            bool wasActive = _active.Contains(target);
            if (!wasActive && _available.Contains(target))
            {
                error = null;
                return false;
            }

            _pendingRelease.Add(target);
            error = null;
            return true;
        }

        public void CompleteRelease(GameObject target, bool deactivate)
        {
            _pendingRelease.Remove(target);
            if (target == null)
            {
                CleanupDestroyed();
                return;
            }

            CleanupDestroyed();
            if (!_managed.Contains(target))
            {
                return;
            }

            _active.Remove(target);
            if (deactivate)
            {
                target.SetActive(false);
            }

            if (!_available.Contains(target))
            {
                _available.Add(target);
            }
        }

        public bool Release(GameObject target, bool deactivate, out string error)
        {
            bool released = BeginRelease(target, out error);
            if (!released || !string.IsNullOrEmpty(error))
            {
                return false;
            }

            CompleteRelease(target, deactivate);
            return true;
        }

        public int Clear()
        {
            CleanupDestroyed();
            List<GameObject> instances = new List<GameObject>(_managed);
            int destroyedCount = 0;
            for (int i = 0; i < instances.Count; i++)
            {
                GameObject instance = instances[i];
                if (instance == null)
                {
                    continue;
                }

                DestroyInstance(instance);
                destroyedCount++;
            }

            _available.Clear();
            _active.Clear();
            _managed.Clear();
            _pendingRelease.Clear();
            return destroyedCount;
        }

        public List<GameObject> GetActiveInstances()
        {
            CleanupDestroyed();
            return new List<GameObject>(_active);
        }

        private GameObject PopAvailable()
        {
            while (_available.Count > 0)
            {
                int index = _available.Count - 1;
                GameObject instance = _available[index];
                _available.RemoveAt(index);
                if (instance != null)
                {
                    return instance;
                }
            }

            return null;
        }

        private GameObject CreateInstance(bool activate, Transform parent)
        {
            if (Prefab == null)
            {
                return null;
            }

            Transform instanceParent = parent == null ? _parent : parent;
            GameObject instance = instanceParent == null
                ? UnityEngine.Object.Instantiate(Prefab)
                : UnityEngine.Object.Instantiate(Prefab, instanceParent, false);

            if (instanceParent == null)
            {
                instance.transform.position = Vector3.zero;
            }
            else
            {
                instance.transform.localPosition = Vector3.zero;
            }

            instance.SetActive(activate);
            _managed.Add(instance);
            return instance;
        }

        private void CleanupDestroyed()
        {
            for (int i = _available.Count - 1; i >= 0; i--)
            {
                if (_available[i] == null)
                {
                    _available.RemoveAt(i);
                }
            }

            _active.RemoveWhere(IsDestroyed);
            _managed.RemoveWhere(IsDestroyed);
            _pendingRelease.RemoveWhere(IsDestroyed);
        }

        private static bool IsDestroyed(GameObject instance)
        {
            return instance == null;
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }

    internal static class GameObjectPoolExecutorUtility
    {
        private const string RegistryStateKey = "gameObjectPool:registry";

        public static string GetPoolId(BlueprintExecutionContext context, RuntimeNode node)
        {
            return NormalizePoolId(context.GetInputValue(node, "poolId", "default"));
        }

        public static string NormalizePoolId(string poolId)
        {
            return string.IsNullOrEmpty(poolId) ? "default" : poolId;
        }

        public static string StateKey(RuntimeNode node, string outputPortId)
        {
            return "gameObjectPool:" + node.Id + ":" + outputPortId;
        }

        public static BlueprintGameObjectPoolRegistry GetRegistry(BlueprintExecutionContext context)
        {
            if (context != null && context.Owner != null)
            {
                BlueprintGameObjectPoolHost host = context.Owner.GetComponent<BlueprintGameObjectPoolHost>();
                if (host == null)
                {
                    host = context.Owner.AddComponent<BlueprintGameObjectPoolHost>();
                }

                return host.Registry;
            }

            object value;
            if (context != null && context.TryGetState(RegistryStateKey, out value))
            {
                BlueprintGameObjectPoolRegistry existing = value as BlueprintGameObjectPoolRegistry;
                if (existing != null)
                {
                    return existing;
                }
            }

            BlueprintGameObjectPoolRegistry registry = new BlueprintGameObjectPoolRegistry();
            if (context != null)
            {
                context.SetState(RegistryStateKey, registry);
            }

            return registry;
        }

        public static bool TryGetPool(BlueprintExecutionContext context, RuntimeNode node, out BlueprintGameObjectPool pool)
        {
            pool = null;
            BlueprintGameObjectPoolRegistry registry;
            if (!TryGetRegistry(context, out registry))
            {
                return false;
            }

            return registry.TryGet(GetPoolId(context, node), out pool);
        }

        public static bool HasExecConnections(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (context == null || context.Blueprint == null || node == null || string.IsNullOrEmpty(outputPortId))
            {
                return false;
            }

            List<RuntimeEdge> edges = context.Blueprint.GetExecEdges(new BlueprintPortKey(node.Id, outputPortId));
            return edges != null && edges.Count > 0;
        }

        private static bool TryGetRegistry(BlueprintExecutionContext context, out BlueprintGameObjectPoolRegistry registry)
        {
            registry = null;
            if (context != null && context.Owner != null)
            {
                BlueprintGameObjectPoolHost host = context.Owner.GetComponent<BlueprintGameObjectPoolHost>();
                if (host != null)
                {
                    registry = host.Registry;
                    return true;
                }
            }

            object value;
            if (context != null && context.TryGetState(RegistryStateKey, out value))
            {
                registry = value as BlueprintGameObjectPoolRegistry;
                return registry != null;
            }

            return false;
        }

        public static GameObject ResolveRequiredPrefab(BlueprintExecutionContext context, RuntimeNode node)
        {
            object value = context.GetInputValue(node, "prefab");
            if (IsEmpty(value))
            {
                return null;
            }

            return GameExecutorBindingUtility.ResolveBinding<GameObject>(context, value);
        }

        public static bool TryResolveOptionalPrefab(
            BlueprintExecutionContext context,
            RuntimeNode node,
            out GameObject prefab,
            out string error)
        {
            object value = context.GetInputValue(node, "prefab");
            if (IsEmpty(value))
            {
                prefab = null;
                error = null;
                return true;
            }

            prefab = GameExecutorBindingUtility.ResolveBinding<GameObject>(context, value);
            if (prefab == null)
            {
                error = "could not resolve prefab.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryResolveOptionalParent(
            BlueprintExecutionContext context,
            RuntimeNode node,
            out Transform parent,
            out string error)
        {
            object value = context.GetInputValue(node, "parent");
            if (IsEmpty(value))
            {
                parent = null;
                error = null;
                return true;
            }

            parent = GameExecutorBindingUtility.ResolveBinding<Transform>(context, value);
            if (parent == null)
            {
                error = "could not resolve parent Transform.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsEmpty(object value)
        {
            string text = value as string;
            return value == null || (text != null && string.IsNullOrEmpty(text));
        }
    }
}
