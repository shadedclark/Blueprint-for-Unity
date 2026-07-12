using System;
using System.Collections.Generic;

namespace BlueprintSystem
{
    public enum BlueprintTraceRecordKind
    {
        EventRequested,
        EventMatched,
        EventMissing,
        NodeEnter,
        NodeExit,
        ExecPortSelected,
        VariableWrite,
        CrossBlueprintEnter,
        CrossBlueprintExit,
        Warning,
        Error,
        TraceCompleted,
        TraceTruncated
    }

    /// <summary>
    /// Runtime trace data. Values remain raw here and are serialized by the Editor MCP bridge so
    /// runtime code does not depend on AssetDatabase or Editor-only types.
    /// </summary>
    [Serializable]
    public sealed class BlueprintTraceRecord
    {
        public BlueprintTraceRecordKind Kind;
        public int Frame;
        public float TimeSeconds;
        public string InstancePath;
        public string BlueprintPath;
        public string EventName;
        public string NodeId;
        public string TypeId;
        public string PortId;
        public string Status;
        public object Value;
        public string Message;
    }

    public interface IBlueprintExecutionTraceSink
    {
        bool IsEnabled { get; }
        void Record(BlueprintTraceRecord record);
    }

    /// <summary>
    /// Attaches one sink to a root instance and every currently compiled nested component. The
    /// bridge owns the sink lifetime and clears it after a trace session completes.
    /// </summary>
    public static class BlueprintExecutionTraceUtility
    {
        public static void SetTraceSink(IBlueprintInstance root, IBlueprintExecutionTraceSink sink)
        {
            var visited = new HashSet<IBlueprintInstance>();
            SetTraceSinkRecursive(root, sink, visited);
        }

        private static void SetTraceSinkRecursive(
            IBlueprintInstance instance,
            IBlueprintExecutionTraceSink sink,
            HashSet<IBlueprintInstance> visited)
        {
            if (instance == null || !visited.Add(instance))
            {
                return;
            }

            BlueprintRunner runner = instance as BlueprintRunner;
            if (runner != null)
            {
                if (runner.ReactiveBindingContext != null)
                {
                    runner.ReactiveBindingContext.TraceSink = sink;
                }
            }
            else
            {
                BlueprintRuntimeComponent component = instance as BlueprintRuntimeComponent;
                if (component != null && component.ReactiveBindingContext != null)
                {
                    component.ReactiveBindingContext.TraceSink = sink;
                }
            }

            IBlueprintDebugInspectable inspectable = instance as IBlueprintDebugInspectable;
            if (inspectable == null)
            {
                return;
            }

            IReadOnlyList<BlueprintDebugComponentDescriptor> descriptors = inspectable.GetComponentDescriptors();
            for (int i = 0; i < descriptors.Count; i++)
            {
                BlueprintDebugComponentDescriptor descriptor = descriptors[i];
                IBlueprintInstance child;
                if (descriptor != null && instance.TryGetBlueprintComponent(descriptor.Name, out child))
                {
                    SetTraceSinkRecursive(child, sink, visited);
                }
            }
        }
    }
}
