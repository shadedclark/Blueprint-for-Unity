using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlueprintSystem;
using Unity.AI.MCP.Editor.ToolRegistry;
using UnityEditor;
using UnityEngine;

namespace BlueprintLangGraph.Editor
{
    public static class BlueprintMcpDiagnosticsBridge
    {
        private static readonly Dictionary<int, TraceSession> ActiveTraceSessions = new Dictionary<int, TraceSession>();

        static BlueprintMcpDiagnosticsBridge()
        {
            AssemblyReloadEvents.beforeAssemblyReload += InterruptAllTraceSessions;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [McpTool(
            "blueprint_runtime_component_snapshot",
            "Read public runtime variables and ownership metadata from Blueprint components inside a loaded BlueprintRunner.",
            EnabledByDefault = true)]
        public static object RuntimeComponentSnapshot(BlueprintRuntimeComponentSnapshotParams parameters)
        {
            parameters = parameters ?? new BlueprintRuntimeComponentSnapshotParams();
            if (!Application.isPlaying)
            {
                return BlueprintMcpCommon.Failure(
                    "BP_PLAY_MODE_REQUIRED",
                    "Blueprint component runtime snapshots require Play Mode.");
            }

            GameObject root = BlueprintMcpCommon.FindLoadedSceneObject(parameters.RootObjectPath);
            if (root == null)
            {
                return BlueprintMcpCommon.Failure(
                    "BP_RUNNER_NOT_FOUND",
                    "Loaded rootObjectPath was not found.",
                    new { rootObjectPath = parameters.RootObjectPath ?? string.Empty });
            }

            BlueprintRunner runner = BlueprintMcpCommon.FindRunner(root, parameters.RunnerPath);
            if (runner == null)
            {
                return BlueprintMcpCommon.Failure(
                    "BP_RUNNER_NOT_FOUND",
                    "BlueprintRunner was not found at runnerPath.",
                    new { rootObjectPath = BlueprintMcpCommon.GetHierarchyPath(root.transform), runnerPath = parameters.RunnerPath ?? string.Empty });
            }

            if (runner.RuntimeBlueprint == null)
            {
                return BlueprintMcpCommon.Failure(
                    "BP_RUNTIME_NOT_COMPILED",
                    "BlueprintRunner has no compiled runtime instance.",
                    new { runnerPath = BlueprintMcpCommon.GetHierarchyPath(runner.transform), blueprintPath = runner.SourcePath ?? string.Empty });
            }

            IBlueprintInstance target;
            string componentError;
            if (!TryResolveComponentTarget(runner, parameters.ComponentPath, parameters.ComponentName, parameters.MaxDepth, out target, out componentError))
            {
                return BlueprintMcpCommon.Failure(
                    componentError == "ambiguous" ? "BP_RUNTIME_COMPONENT_AMBIGUOUS" : "BP_COMPONENT_NOT_FOUND",
                    componentError == "ambiguous"
                        ? "More than one nested Blueprint Component matches componentName; provide componentPath."
                        : "Requested Blueprint Component was not found.",
                    new
                    {
                        componentName = parameters.ComponentName ?? string.Empty,
                        componentPath = parameters.ComponentPath ?? new List<string>()
                    });
            }

            if (ReferenceEquals(target, (IBlueprintInstance)runner) && string.IsNullOrWhiteSpace(parameters.ComponentName) &&
                (parameters.ComponentPath == null || parameters.ComponentPath.Count == 0))
            {
                return BlueprintMcpCommon.Success("Blueprint component list complete.", new
                {
                    isPlaying = true,
                    runnerPath = BlueprintMcpCommon.GetHierarchyPath(runner.transform),
                    rootBlueprintPath = runner.SourcePath ?? string.Empty,
                    components = DescribeComponentTree(
                        runner,
                        Array.Empty<string>(),
                        parameters.IncludeNestedComponents ? Math.Max(0, parameters.MaxDepth) : 1,
                        true)
                });
            }

            object componentPayload;
            bool limitExceeded;
            if (!TryBuildComponentPayload(target, parameters, out componentPayload, out limitExceeded))
            {
                return BlueprintMcpCommon.Failure(
                    "BP_RUNTIME_NOT_COMPILED",
                    "Requested Blueprint Component has no compiled runtime instance.");
            }

            object rootPayload = null;
            if (parameters.IncludeRootInstance && !ReferenceEquals(target, (IBlueprintInstance)runner))
            {
                bool ignored;
                TryBuildComponentPayload(runner, parameters, out rootPayload, out ignored);
            }

            var data = new
            {
                isPlaying = true,
                runnerPath = BlueprintMcpCommon.GetHierarchyPath(runner.transform),
                rootBlueprintPath = runner.SourcePath ?? string.Empty,
                component = componentPayload,
                rootInstance = rootPayload
            };
            if (limitExceeded)
            {
                return BlueprintMcpCommon.Failure(
                    "BP_RESULT_LIMIT_EXCEEDED",
                    "Snapshot exceeded maxVariables; return a narrower variableNames selection.",
                    data,
                    new[] { componentPayload });
            }

            return BlueprintMcpCommon.Success("Blueprint runtime component snapshot complete.", data);
        }

        [McpTool(
            "blueprint_event_trace",
            "Trace Blueprint event delivery and node execution for a loaded BlueprintRunner.",
            EnabledByDefault = true)]
        public static async Task<object> EventTrace(BlueprintEventTraceParams parameters)
        {
            parameters = parameters ?? new BlueprintEventTraceParams();
            if (!Application.isPlaying)
            {
                return BlueprintMcpCommon.Failure("BP_PLAY_MODE_REQUIRED", "Blueprint event tracing requires Play Mode.");
            }

            GameObject root = BlueprintMcpCommon.FindLoadedSceneObject(parameters.RootObjectPath);
            BlueprintRunner runner = BlueprintMcpCommon.FindRunner(root, parameters.RunnerPath);
            if (root == null || runner == null || runner.RuntimeBlueprint == null)
            {
                return BlueprintMcpCommon.Failure(
                    "BP_TRACE_TARGET_NOT_FOUND",
                    "A loaded, compiled BlueprintRunner was not found at the requested path.");
            }

            IBlueprintInstance target;
            string componentError;
            if (!TryResolveComponentTarget(runner, parameters.ComponentPath, string.Empty, 64, out target, out componentError))
            {
                return BlueprintMcpCommon.Failure("BP_TRACE_TARGET_NOT_FOUND", "Requested Blueprint Component trace target was not found.");
            }

            string mode = string.IsNullOrWhiteSpace(parameters.Mode) ? "observe" : parameters.Mode.Trim().ToLowerInvariant();
            if (mode != "observe" && mode != "trigger")
            {
                return BlueprintMcpCommon.Failure("BP_TRACE_TARGET_NOT_FOUND", "mode must be observe or trigger.");
            }

            if (string.IsNullOrWhiteSpace(parameters.EventName))
            {
                return BlueprintMcpCommon.Failure("BP_TRACE_EVENT_NOT_FOUND", "eventName is required.");
            }

            int runnerId = runner.GetInstanceID();
            if (ActiveTraceSessions.ContainsKey(runnerId))
            {
                return BlueprintMcpCommon.Failure("BP_TRACE_ALREADY_ACTIVE", "BlueprintRunner already has an active trace session.");
            }

            var session = new TraceSession(
                Guid.NewGuid().ToString("N"),
                runner,
                Math.Max(1, parameters.MaxRecords),
                parameters.StopOnError);
            ActiveTraceSessions[runnerId] = session;
            BlueprintExecutionTraceUtility.SetTraceSink(runner, session);
            try
            {
                if (mode == "trigger")
                {
                    target.TriggerEvent(parameters.EventName);
                }

                TraceWaitOutcome outcome = await WaitForTraceFrames(
                    session,
                    Math.Max(1, parameters.DurationFrames),
                    Math.Max(1, parameters.TimeoutMs));
                return BuildTraceResult(session, mode, parameters, outcome);
            }
            finally
            {
                BlueprintExecutionTraceUtility.SetTraceSink(runner, null);
                ActiveTraceSessions.Remove(runnerId);
            }
        }

        private static bool TryBuildComponentPayload(
            IBlueprintInstance instance,
            BlueprintRuntimeComponentSnapshotParams parameters,
            out object payload,
            out bool limitExceeded)
        {
            payload = null;
            limitExceeded = false;
            IBlueprintDebugInspectable inspectable = instance as IBlueprintDebugInspectable;
            if (instance == null || instance.RuntimeBlueprint == null || inspectable == null)
            {
                return false;
            }

            HashSet<string> requested = new HashSet<string>(
                (parameters.VariableNames ?? new List<string>()).Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.Ordinal);
            var variables = new Dictionary<string, object>(StringComparer.Ordinal);
            var missingVariables = new List<string>();
            IReadOnlyList<BlueprintDebugVariableDescriptor> descriptors = inspectable.GetVariableDescriptors();
            foreach (string variableName in requested)
            {
                if (!descriptors.Any(descriptor => descriptor != null && descriptor.Name == variableName))
                {
                    missingVariables.Add(variableName);
                }
            }

            int maxVariables = Math.Max(1, parameters.MaxVariables);
            int included = 0;
            foreach (BlueprintDebugVariableDescriptor descriptor in descriptors.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                if (descriptor == null ||
                    (!parameters.IncludeNonExposed && !descriptor.Exposed) ||
                    (requested.Count > 0 && !requested.Contains(descriptor.Name)))
                {
                    continue;
                }

                if (included++ >= maxVariables)
                {
                    limitExceeded = true;
                    break;
                }

                object value;
                if (instance.TryGetVariable(descriptor.Name, out value))
                {
                    variables[descriptor.Name] = new
                    {
                        type = descriptor.Type,
                        exposed = descriptor.Exposed,
                        value = BlueprintMcpCommon.ToSerializableValue(value, Math.Max(1, parameters.MaxCollectionItems))
                    };
                }
                else
                {
                    missingVariables.Add(descriptor.Name);
                }
            }

            payload = new
            {
                name = instance.InstanceName ?? string.Empty,
                componentPath = BuildComponentPath(instance),
                blueprintPath = instance.SourcePath ?? string.Empty,
                compiledAssetPath = instance.CompiledBlueprint == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(instance.CompiledBlueprint),
                ownerChain = parameters.IncludeOwnerChain ? BuildOwnerChain(instance) : Array.Empty<string>(),
                variables,
                missingVariables = missingVariables.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                nestedComponents = parameters.IncludeNestedComponents
                    ? DescribeComponentTree(instance, BuildComponentPath(instance), Math.Max(0, parameters.MaxDepth), true)
                    : Array.Empty<object>(),
                truncated = limitExceeded
            };
            return true;
        }

        private static bool TryResolveComponentTarget(
            BlueprintRunner runner,
            List<string> componentPath,
            string componentName,
            int maxDepth,
            out IBlueprintInstance target,
            out string error)
        {
            target = runner;
            error = null;
            List<string> path = (componentPath ?? new List<string>())
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToList();
            if (path.Count > 0)
            {
                foreach (string segment in path)
                {
                    IBlueprintInstance child;
                    if (!target.TryGetBlueprintComponent(segment, out child))
                    {
                        error = "notFound";
                        return false;
                    }

                    target = child;
                }

                if (!string.IsNullOrWhiteSpace(componentName) &&
                    !string.Equals(target.InstanceName, componentName, StringComparison.Ordinal))
                {
                    error = "notFound";
                    return false;
                }

                return true;
            }

            if (string.IsNullOrWhiteSpace(componentName))
            {
                return true;
            }

            var matches = new List<IBlueprintInstance>();
            FindComponentsByName(runner, componentName, 0, Math.Max(0, maxDepth), matches);
            if (matches.Count == 1)
            {
                target = matches[0];
                return true;
            }

            error = matches.Count > 1 ? "ambiguous" : "notFound";
            return false;
        }

        private static void FindComponentsByName(
            IBlueprintInstance instance,
            string componentName,
            int depth,
            int maxDepth,
            List<IBlueprintInstance> matches)
        {
            if (instance == null || depth >= maxDepth)
            {
                return;
            }

            IBlueprintDebugInspectable inspectable = instance as IBlueprintDebugInspectable;
            if (inspectable == null)
            {
                return;
            }

            foreach (BlueprintDebugComponentDescriptor descriptor in inspectable.GetComponentDescriptors())
            {
                IBlueprintInstance child;
                if (descriptor == null || !instance.TryGetBlueprintComponent(descriptor.Name, out child))
                {
                    continue;
                }

                if (string.Equals(child.InstanceName, componentName, StringComparison.Ordinal))
                {
                    matches.Add(child);
                }

                FindComponentsByName(child, componentName, depth + 1, maxDepth, matches);
            }
        }

        private static object[] DescribeComponentTree(
            IBlueprintInstance instance,
            IReadOnlyList<string> path,
            int maxDepth,
            bool recursive)
        {
            if (!recursive || instance == null || maxDepth <= 0)
            {
                return Array.Empty<object>();
            }

            IBlueprintDebugInspectable inspectable = instance as IBlueprintDebugInspectable;
            if (inspectable == null)
            {
                return Array.Empty<object>();
            }

            var result = new List<object>();
            foreach (BlueprintDebugComponentDescriptor descriptor in inspectable.GetComponentDescriptors())
            {
                IBlueprintInstance child;
                if (descriptor == null || !instance.TryGetBlueprintComponent(descriptor.Name, out child))
                {
                    continue;
                }

                string[] childPath = path.Concat(new[] { descriptor.Name }).ToArray();
                result.Add(new
                {
                    name = descriptor.Name,
                    componentPath = childPath,
                    blueprintPath = descriptor.SourcePath,
                    compiled = descriptor.Compiled,
                    children = DescribeComponentTree(child, childPath, maxDepth - 1, true)
                });
            }

            return result.ToArray();
        }

        private static string[] BuildComponentPath(IBlueprintInstance instance)
        {
            var names = new List<string>();
            IBlueprintInstance current = instance;
            while (current != null && !(current is BlueprintRunner))
            {
                names.Add(current.InstanceName ?? string.Empty);
                current = current.OwnerInstance;
            }

            names.Reverse();
            return names.ToArray();
        }

        private static string[] BuildOwnerChain(IBlueprintInstance instance)
        {
            var names = new List<string>();
            IBlueprintInstance current = instance == null ? null : instance.OwnerInstance;
            while (current != null)
            {
                names.Add(current.InstanceName ?? string.Empty);
                current = current.OwnerInstance;
            }

            names.Reverse();
            return names.ToArray();
        }

        private static Task<TraceWaitOutcome> WaitForTraceFrames(TraceSession session, int frames, int timeoutMs)
        {
            var completion = new TaskCompletionSource<TraceWaitOutcome>();
            int startFrame = Time.frameCount;
            double startTime = EditorApplication.timeSinceStartup;
            void Update()
            {
                if (session.Interrupted || !Application.isPlaying || session.Runner == null)
                {
                    EditorApplication.update -= Update;
                    completion.TrySetResult(TraceWaitOutcome.Interrupted);
                    return;
                }

                if (session.Truncated)
                {
                    EditorApplication.update -= Update;
                    completion.TrySetResult(TraceWaitOutcome.RecordLimit);
                    return;
                }

                if (session.StoppedOnError)
                {
                    EditorApplication.update -= Update;
                    completion.TrySetResult(TraceWaitOutcome.StoppedOnError);
                    return;
                }

                if (Time.frameCount - startFrame >= frames)
                {
                    EditorApplication.update -= Update;
                    completion.TrySetResult(TraceWaitOutcome.Completed);
                    return;
                }

                if ((EditorApplication.timeSinceStartup - startTime) * 1000d >= timeoutMs)
                {
                    EditorApplication.update -= Update;
                    completion.TrySetResult(TraceWaitOutcome.Timeout);
                }
            }

            EditorApplication.update += Update;
            return completion.Task;
        }

        private static object BuildTraceResult(
            TraceSession session,
            string mode,
            BlueprintEventTraceParams parameters,
            TraceWaitOutcome outcome)
        {
            session.RecordCompletion(outcome);
            object[] records = session.Records
                .Where(record => parameters.IncludeNodeOutputs || record.Kind != BlueprintTraceRecordKind.ExecPortSelected)
                .Where(record => parameters.IncludeVariableWrites || record.Kind != BlueprintTraceRecordKind.VariableWrite)
                .Select((record, index) => new
            {
                sequence = index + 1,
                frame = record.Frame,
                timeSeconds = record.TimeSeconds,
                instancePath = record.InstancePath,
                blueprintPath = record.BlueprintPath,
                eventName = record.EventName,
                nodeId = record.NodeId,
                typeId = record.TypeId,
                recordKind = record.Kind.ToString(),
                portId = record.PortId,
                status = record.Status,
                valueSummary = parameters.IncludeValues
                    ? BlueprintMcpCommon.ToSerializableValue(record.Value, 25)
                    : SummarizeTraceValue(record.Value),
                message = record.Message
            }).Cast<object>().ToArray();
            var data = new
            {
                traceId = session.TraceId,
                mode,
                stateMutation = mode == "trigger",
                eventReceived = session.EventReceived,
                completed = outcome == TraceWaitOutcome.Completed || outcome == TraceWaitOutcome.StoppedOnError,
                finalStatus = session.FinalStatus(outcome),
                lastNodeId = session.LastNodeId,
                lastFailureReason = session.LastFailureReason,
                records
            };

            if (outcome == TraceWaitOutcome.Timeout)
            {
                return BlueprintMcpCommon.Failure("BP_TRACE_TIMEOUT", "Blueprint event trace timed out.", data, records, true);
            }

            if (outcome == TraceWaitOutcome.Interrupted)
            {
                return BlueprintMcpCommon.Failure("BP_TRACE_INTERRUPTED", "Blueprint event trace was interrupted.", data, records, true);
            }

            if (outcome == TraceWaitOutcome.RecordLimit)
            {
                return BlueprintMcpCommon.Failure("BP_TRACE_RECORD_LIMIT", "Blueprint event trace reached maxRecords.", data, records);
            }

            if (!session.EventReceived)
            {
                return BlueprintMcpCommon.Failure("BP_TRACE_EVENT_NOT_FOUND", "Blueprint event did not match an Event Entry.", data, records);
            }

            if (session.HasError)
            {
                return BlueprintMcpCommon.Failure("BP_TRACE_EXECUTION_FAILED", "Blueprint event trace captured an execution error.", data, records);
            }

            return BlueprintMcpCommon.Success("Blueprint event trace complete.", data);
        }

        private static object SummarizeTraceValue(object value)
        {
            return value == null ? null : new { type = value.GetType().FullName };
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                InterruptAllTraceSessions();
            }
        }

        private static void InterruptAllTraceSessions()
        {
            foreach (TraceSession session in ActiveTraceSessions.Values.ToArray())
            {
                session.Interrupted = true;
                if (session.Runner != null)
                {
                    BlueprintExecutionTraceUtility.SetTraceSink(session.Runner, null);
                }
            }

            ActiveTraceSessions.Clear();
        }

        private enum TraceWaitOutcome
        {
            Completed,
            Timeout,
            Interrupted,
            RecordLimit,
            StoppedOnError
        }

        private sealed class TraceSession : IBlueprintExecutionTraceSink
        {
            public readonly string TraceId;
            public readonly BlueprintRunner Runner;
            public readonly List<BlueprintTraceRecord> Records = new List<BlueprintTraceRecord>();
            private readonly int _maxRecords;
            private readonly bool _stopOnError;

            public bool EventReceived;
            public bool HasError;
            public bool Truncated;
            public bool StoppedOnError;
            public bool Interrupted;
            public string LastNodeId = string.Empty;
            public string LastFailureReason = string.Empty;

            public TraceSession(string traceId, BlueprintRunner runner, int maxRecords, bool stopOnError)
            {
                TraceId = traceId;
                Runner = runner;
                _maxRecords = maxRecords;
                _stopOnError = stopOnError;
            }

            public bool IsEnabled
            {
                get { return !Truncated && !StoppedOnError && !Interrupted; }
            }

            public void Record(BlueprintTraceRecord record)
            {
                if (!IsEnabled || record == null)
                {
                    return;
                }

                if (Records.Count >= _maxRecords)
                {
                    Truncated = true;
                    Records.Add(new BlueprintTraceRecord
                    {
                        Kind = BlueprintTraceRecordKind.TraceTruncated,
                        Frame = Time.frameCount,
                        TimeSeconds = Time.realtimeSinceStartup,
                        Message = "maxRecords reached"
                    });
                    return;
                }

                Records.Add(record);
                if (record.Kind == BlueprintTraceRecordKind.EventMatched)
                {
                    EventReceived = true;
                }

                if (!string.IsNullOrEmpty(record.NodeId))
                {
                    LastNodeId = record.NodeId;
                }

                if (record.Kind == BlueprintTraceRecordKind.Error)
                {
                    HasError = true;
                    LastFailureReason = record.Message ?? string.Empty;
                    if (_stopOnError)
                    {
                        StoppedOnError = true;
                    }
                }
            }

            public void RecordCompletion(TraceWaitOutcome outcome)
            {
                if (Records.Any(record => record.Kind == BlueprintTraceRecordKind.TraceCompleted))
                {
                    return;
                }

                Records.Add(new BlueprintTraceRecord
                {
                    Kind = BlueprintTraceRecordKind.TraceCompleted,
                    Frame = Time.frameCount,
                    TimeSeconds = Time.realtimeSinceStartup,
                    Status = FinalStatus(outcome),
                    Message = "Trace session completed."
                });
            }

            public string FinalStatus(TraceWaitOutcome outcome)
            {
                if (outcome == TraceWaitOutcome.Timeout)
                {
                    return "Timeout";
                }

                if (outcome == TraceWaitOutcome.Interrupted)
                {
                    return "Interrupted";
                }

                if (outcome == TraceWaitOutcome.RecordLimit)
                {
                    return "Truncated";
                }

                if (HasError)
                {
                    return "Error";
                }

                return EventReceived ? "Completed" : "EventMissing";
            }
        }
    }

    public sealed class BlueprintRuntimeComponentSnapshotParams
    {
        [McpDescription("Loaded scene hierarchy path that contains the target BlueprintRunner.", Required = true)]
        public string RootObjectPath { get; set; }

        [McpDescription("Optional path relative to rootObjectPath for the GameObject that owns BlueprintRunner.")]
        public string RunnerPath { get; set; }

        [McpDescription("Exact Component name. Omit to return a Component list.")]
        public string ComponentName { get; set; }

        [McpDescription("Exact nested Component-name path, used to disambiguate repeated names.")]
        public List<string> ComponentPath { get; set; } = new List<string>();

        [McpDescription("Variable names to read. Empty reads all allowed public variables.")]
        public List<string> VariableNames { get; set; } = new List<string>();

        [McpDescription("Include the root Runner variable snapshot when targeting a Component.")]
        public bool IncludeRootInstance { get; set; }

        [McpDescription("Include nested Component metadata in the result.")]
        public bool IncludeNestedComponents { get; set; }

        [McpDescription("Include the Component owner chain.")]
        public bool IncludeOwnerChain { get; set; } = true;

        [McpDescription("Include declared non-exposed variables. Defaults to false.")]
        public bool IncludeNonExposed { get; set; }

        [McpDescription("Maximum recursive Component depth.")]
        public int MaxDepth { get; set; } = 8;

        [McpDescription("Maximum variables returned per instance.")]
        public int MaxVariables { get; set; } = 200;

        [McpDescription("Maximum array or dictionary entries returned for one variable value.")]
        public int MaxCollectionItems { get; set; } = 50;
    }

    public sealed class BlueprintEventTraceParams
    {
        [McpDescription("Loaded scene hierarchy path that contains the target BlueprintRunner.", Required = true)]
        public string RootObjectPath { get; set; }

        [McpDescription("Optional path relative to rootObjectPath for the GameObject that owns BlueprintRunner.")]
        public string RunnerPath { get; set; }

        [McpDescription("Nested Component-name path. Empty traces the root Runner.")]
        public List<string> ComponentPath { get; set; } = new List<string>();

        [McpDescription("Event Entry name to observe or trigger.", Required = true)]
        public string EventName { get; set; }

        [McpDescription("Trace mode: observe waits for a natural event; trigger invokes the event once.")]
        public string Mode { get; set; } = "observe";

        [McpDescription("Frames to keep the temporary trace sink attached.")]
        public int DurationFrames { get; set; } = 1;

        [McpDescription("Maximum wall-clock duration for the trace session.")]
        public int TimeoutMs { get; set; } = 5000;

        [McpDescription("Maximum trace records before the session stops and returns BP_TRACE_RECORD_LIMIT.")]
        public int MaxRecords { get; set; } = 1000;

        [McpDescription("Reserved for node input summaries; default false prevents value capture.")]
        public bool IncludeNodeInputs { get; set; }

        [McpDescription("Include selected execution output ports.")]
        public bool IncludeNodeOutputs { get; set; } = true;

        [McpDescription("Include Variable.Set and cross-Blueprint variable write records.")]
        public bool IncludeVariableWrites { get; set; } = true;

        [McpDescription("Include serialized values for trace records. Default returns type-only summaries.")]
        public bool IncludeValues { get; set; }

        [McpDescription("Stop recording after the first Blueprint execution error.")]
        public bool StopOnError { get; set; } = true;
    }
}
