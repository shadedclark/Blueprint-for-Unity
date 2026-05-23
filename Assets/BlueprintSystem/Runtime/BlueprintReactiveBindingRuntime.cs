using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BlueprintSystem
{
    public interface IBlueprintReactiveBinding
    {
        string Key { get; }
        BlueprintExecutionContext Context { get; }
        void Apply();
        bool IsAlive();
    }

    public interface IBlueprintReactiveBindingDependency
    {
        bool DependsOnInstance(IBlueprintInstance instance);
    }

    public interface IBlueprintReactiveBindingSource
    {
        string SourceNodeId { get; }
    }

    public interface IBlueprintReactiveBindingRestorer
    {
        BlueprintExecResult RestoreReactiveBinding(BlueprintExecutionContext context, RuntimeNode node);
    }

    internal sealed class BlueprintReactiveBindingSnapshot
    {
        private readonly List<BlueprintReactiveBindingSnapshotEntry> _entries =
            new List<BlueprintReactiveBindingSnapshotEntry>();

        internal IReadOnlyList<BlueprintReactiveBindingSnapshotEntry> Entries
        {
            get { return _entries; }
        }

        internal bool HasEntries
        {
            get { return _entries.Count > 0; }
        }

        internal void Add(List<string> instancePath, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            _entries.Add(new BlueprintReactiveBindingSnapshotEntry(instancePath, nodeId));
        }
    }

    internal sealed class BlueprintReactiveBindingSnapshotEntry
    {
        internal BlueprintReactiveBindingSnapshotEntry(List<string> instancePath, string nodeId)
        {
            InstancePath = instancePath == null ? new List<string>() : new List<string>(instancePath);
            NodeId = nodeId;
        }

        internal List<string> InstancePath { get; private set; }
        internal string NodeId { get; private set; }
    }

    public static class BlueprintReactiveBindingRuntime
    {
        private static readonly Dictionary<BlueprintExecutionContext, Dictionary<string, IBlueprintReactiveBinding>> BindingsByContext =
            new Dictionary<BlueprintExecutionContext, Dictionary<string, IBlueprintReactiveBinding>>();

        public static void Register(BlueprintExecutionContext context, IBlueprintReactiveBinding binding)
        {
            if (context == null || binding == null || string.IsNullOrEmpty(binding.Key))
            {
                return;
            }

            Dictionary<string, IBlueprintReactiveBinding> bindings;
            if (!BindingsByContext.TryGetValue(context, out bindings))
            {
                bindings = new Dictionary<string, IBlueprintReactiveBinding>();
                BindingsByContext[context] = bindings;
            }

            bindings[binding.Key] = binding;
            ApplyIfAlive(binding);
        }

        public static void Clear(BlueprintExecutionContext context)
        {
            if (context == null)
            {
                return;
            }

            BindingsByContext.Remove(context);
        }

        public static void ClearInstance(IBlueprintInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            List<BlueprintExecutionContext> contexts = new List<BlueprintExecutionContext>();
            foreach (KeyValuePair<BlueprintExecutionContext, Dictionary<string, IBlueprintReactiveBinding>> pair in BindingsByContext)
            {
                if (IsInstanceOrDescendant(pair.Key.Instance, instance) ||
                    HasBindingDependency(pair.Value, instance))
                {
                    contexts.Add(pair.Key);
                }
            }

            for (int i = 0; i < contexts.Count; i++)
            {
                BindingsByContext.Remove(contexts[i]);
            }
        }

        public static void Refresh(BlueprintExecutionContext context)
        {
            if (context == null)
            {
                return;
            }

            Dictionary<string, IBlueprintReactiveBinding> bindings;
            if (!BindingsByContext.TryGetValue(context, out bindings))
            {
                return;
            }

            RefreshBindings(context, bindings);
        }

        public static void RefreshInstance(IBlueprintInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            List<BlueprintExecutionContext> contexts = new List<BlueprintExecutionContext>();
            foreach (KeyValuePair<BlueprintExecutionContext, Dictionary<string, IBlueprintReactiveBinding>> pair in BindingsByContext)
            {
                if (IsInstanceOrDescendant(pair.Key.Instance, instance) ||
                    HasBindingDependency(pair.Value, instance))
                {
                    contexts.Add(pair.Key);
                }
            }

            for (int i = 0; i < contexts.Count; i++)
            {
                Refresh(contexts[i]);
            }
        }

        public static void RefreshForContext(BlueprintExecutionContext context)
        {
            if (context == null)
            {
                return;
            }

            if (context.Instance != null)
            {
                RefreshInstance(context.Instance);
                return;
            }

            Refresh(context);
        }

        public static string CreateBindingKey(BlueprintExecutionContext context, RuntimeNode node, string targetBindingName, string propertyName)
        {
            string instanceId = CreateInstanceId(context);
            string nodeId = node == null ? string.Empty : node.Id;
            return instanceId + ":" + nodeId + ":" + (targetBindingName ?? string.Empty) + ":" + (propertyName ?? string.Empty);
        }

        internal static BlueprintReactiveBindingSnapshot CaptureForInstance(IBlueprintInstance instance)
        {
            BlueprintReactiveBindingSnapshot snapshot = new BlueprintReactiveBindingSnapshot();
            if (instance == null)
            {
                return snapshot;
            }

            foreach (KeyValuePair<BlueprintExecutionContext, Dictionary<string, IBlueprintReactiveBinding>> pair in BindingsByContext)
            {
                BlueprintExecutionContext context = pair.Key;
                if (context == null || pair.Value == null)
                {
                    continue;
                }

                List<string> instancePath;
                if (!TryCreateInstancePath(instance, context.Instance, out instancePath))
                {
                    continue;
                }

                foreach (IBlueprintReactiveBinding binding in pair.Value.Values)
                {
                    if (!IsAlive(binding))
                    {
                        continue;
                    }

                    IBlueprintReactiveBindingSource source = binding as IBlueprintReactiveBindingSource;
                    if (source == null || string.IsNullOrEmpty(source.SourceNodeId))
                    {
                        continue;
                    }

                    snapshot.Add(instancePath, source.SourceNodeId);
                }
            }

            return snapshot;
        }

        internal static void RestoreForInstance(BlueprintReactiveBindingSnapshot snapshot, IBlueprintInstance instance)
        {
            if (snapshot == null || !snapshot.HasEntries || instance == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                BlueprintReactiveBindingSnapshotEntry entry = snapshot.Entries[i];
                IBlueprintInstance targetInstance = ResolveInstancePath(instance, entry.InstancePath);
                BlueprintExecutionContext context = GetExecutionContext(targetInstance);
                if (context == null || context.Blueprint == null)
                {
                    continue;
                }

                RuntimeNode node = context.Blueprint.GetNode(entry.NodeId);
                if (node == null || node.Executor == null)
                {
                    continue;
                }

                IBlueprintReactiveBindingRestorer restorer = node.Executor as IBlueprintReactiveBindingRestorer;
                if (restorer == null)
                {
                    continue;
                }

                BlueprintExecResult result = restorer.RestoreReactiveBinding(context, node);
                if (!string.IsNullOrEmpty(result.ErrorMessage) && context.Logger != null)
                {
                    context.Logger.Error("Reactive binding restore failed for node '" + node.Id + "': " + result.ErrorMessage);
                }
            }
        }

        private static void RefreshBindings(BlueprintExecutionContext context, Dictionary<string, IBlueprintReactiveBinding> bindings)
        {
            List<IBlueprintReactiveBinding> snapshot = new List<IBlueprintReactiveBinding>(bindings.Values);
            List<string> staleKeys = new List<string>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                IBlueprintReactiveBinding binding = snapshot[i];
                if (binding == null || string.IsNullOrEmpty(binding.Key) || !IsAlive(binding))
                {
                    if (binding != null)
                    {
                        staleKeys.Add(binding.Key);
                    }

                    continue;
                }

                binding.Apply();
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                bindings.Remove(staleKeys[i]);
            }

            if (bindings.Count == 0)
            {
                BindingsByContext.Remove(context);
            }
        }

        private static void ApplyIfAlive(IBlueprintReactiveBinding binding)
        {
            if (IsAlive(binding))
            {
                binding.Apply();
            }
        }

        private static bool IsAlive(IBlueprintReactiveBinding binding)
        {
            try
            {
                return binding != null && binding.IsAlive();
            }
            catch
            {
                return false;
            }
        }

        private static bool HasBindingDependency(Dictionary<string, IBlueprintReactiveBinding> bindings, IBlueprintInstance instance)
        {
            if (bindings == null || instance == null)
            {
                return false;
            }

            foreach (IBlueprintReactiveBinding binding in bindings.Values)
            {
                IBlueprintReactiveBindingDependency dependency = binding as IBlueprintReactiveBindingDependency;
                if (dependency != null && dependency.DependsOnInstance(instance))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryCreateInstancePath(IBlueprintInstance root, IBlueprintInstance instance, out List<string> path)
        {
            path = new List<string>();
            if (root == null || instance == null)
            {
                return false;
            }

            IBlueprintInstance current = instance;
            while (current != null && !object.ReferenceEquals(current, root))
            {
                path.Add(current.InstanceName);
                current = current.OwnerInstance;
            }

            if (!object.ReferenceEquals(current, root))
            {
                path.Clear();
                return false;
            }

            path.Reverse();
            return true;
        }

        private static IBlueprintInstance ResolveInstancePath(IBlueprintInstance root, List<string> path)
        {
            IBlueprintInstance current = root;
            if (current == null || path == null)
            {
                return current;
            }

            for (int i = 0; i < path.Count; i++)
            {
                IBlueprintInstance child;
                if (current == null || !current.TryGetBlueprintComponent(path[i], out child))
                {
                    return null;
                }

                current = child;
            }

            return current;
        }

        private static BlueprintExecutionContext GetExecutionContext(IBlueprintInstance instance)
        {
            BlueprintRunner runner = instance as BlueprintRunner;
            if (runner != null)
            {
                return runner.ReactiveBindingContext;
            }

            BlueprintRuntimeComponent runtimeComponent = instance as BlueprintRuntimeComponent;
            return runtimeComponent == null ? null : runtimeComponent.ReactiveBindingContext;
        }

        private static bool IsInstanceOrDescendant(IBlueprintInstance candidate, IBlueprintInstance root)
        {
            while (candidate != null)
            {
                if (object.ReferenceEquals(candidate, root))
                {
                    return true;
                }

                candidate = candidate.OwnerInstance;
            }

            return false;
        }

        private static string CreateInstanceId(BlueprintExecutionContext context)
        {
            if (context == null)
            {
                return "0";
            }

            if (context.Instance != null)
            {
                Object unityObject = context.Instance as Object;
                if (unityObject != null)
                {
                    return unityObject.GetInstanceID().ToString(CultureInfo.InvariantCulture);
                }

                return RuntimeHelpers.GetHashCode(context.Instance).ToString(CultureInfo.InvariantCulture);
            }

            if (context.OwnerComponent != null)
            {
                return context.OwnerComponent.GetInstanceID().ToString(CultureInfo.InvariantCulture);
            }

            return RuntimeHelpers.GetHashCode(context).ToString(CultureInfo.InvariantCulture);
        }
    }
}
