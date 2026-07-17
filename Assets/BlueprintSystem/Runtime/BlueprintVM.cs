using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class BlueprintVM
    {
        private const int MaxStepsPerEvent = 1024;
        private readonly Stack<Queue<QueuedExec>> _executionQueuePool = new Stack<Queue<QueuedExec>>();

        private struct QueuedExec
        {
            public string NodeId;
            public string InputPortId;

            public QueuedExec(string nodeId, string inputPortId)
            {
                NodeId = nodeId;
                InputPortId = inputPortId;
            }
        }

        public void TriggerEvent(BlueprintExecutionContext context, string eventName)
        {
            if (context == null || context.Blueprint == null)
            {
                return;
            }

            bool traceEnabled = context.IsTraceEnabled;
            string previousTraceEvent = context.CurrentTraceEventName;
            RuntimeNode previousTraceNode = context.CurrentTraceNode;
            if (traceEnabled)
            {
                context.SetTraceExecutionState(eventName, null);
                context.RecordTrace(BlueprintTraceRecordKind.EventRequested);
            }

            string entryNodeId;
            if (!context.Blueprint.EventEntries.TryGetValue(eventName, out entryNodeId))
            {
                if (IsDebugLogEnabled(context.Logger))
                {
                    context.Logger.Warning("No blueprint event entry named '" + eventName + "'.");
                }
                if (traceEnabled)
                {
                    context.RecordTrace(BlueprintTraceRecordKind.EventMissing, message: "No matching event entry.");
                    context.SetTraceExecutionState(previousTraceEvent, previousTraceNode);
                }
                return;
            }

            if (traceEnabled)
            {
                context.RecordTrace(BlueprintTraceRecordKind.EventMatched, status: "matched", value: entryNodeId);
            }

            try
            {
                Queue<QueuedExec> queue = AcquireExecutionQueue();
                try
                {
                    queue.Enqueue(new QueuedExec(entryNodeId, null));
                    ExecuteNodeQueue(context, queue);
                }
                finally
                {
                    ReleaseExecutionQueue(queue);
                }
            }
            finally
            {
                if (traceEnabled)
                {
                    context.SetTraceExecutionState(previousTraceEvent, previousTraceNode);
                }
            }
        }

        public void ExecuteNodeQueue(BlueprintExecutionContext context, List<string> nodeIds)
        {
            Queue<QueuedExec> queue = AcquireExecutionQueue();
            try
            {
                if (nodeIds != null)
                {
                    for (int i = 0; i < nodeIds.Count; i++)
                    {
                        queue.Enqueue(new QueuedExec(nodeIds[i], null));
                    }
                }

                ExecuteNodeQueue(context, queue);
            }
            finally
            {
                ReleaseExecutionQueue(queue);
            }
        }

        private void ExecuteNodeQueue(BlueprintExecutionContext context, Queue<QueuedExec> queue)
        {
            int steps = 0;
            while (queue.Count > 0)
            {
                if (++steps > MaxStepsPerEvent)
                {
                    context.Logger.Error("Blueprint execution exceeded " + MaxStepsPerEvent + " steps.");
                    context.RecordTrace(BlueprintTraceRecordKind.Error, status: "stepLimit", message: "Blueprint execution exceeded " + MaxStepsPerEvent + " steps.");
                    return;
                }

                QueuedExec queued = queue.Dequeue();
                RuntimeNode node = context.Blueprint.GetNode(queued.NodeId);
                if (node == null)
                {
                    context.Logger.Error("Missing runtime node '" + queued.NodeId + "'.");
                    context.RecordTrace(BlueprintTraceRecordKind.Error, status: "missingNode", message: "Missing runtime node '" + queued.NodeId + "'.");
                    continue;
                }

                if (node.Executor == null)
                {
                    context.Logger.Error("Node '" + node.Id + "' has no runtime executor.");
                    context.RecordTrace(BlueprintTraceRecordKind.Error, status: "missingExecutor", message: "Node '" + node.Id + "' has no runtime executor.");
                    continue;
                }

                context.ClearValueCache();
                if (IsDebugLogEnabled(context.Logger))
                {
                    context.Logger.Log("Execute " + node.Id + " (" + node.TypeId + ")");
                }
                string previousInputPortId = context.CurrentExecInputPortId;
                bool traceEnabled = context.IsTraceEnabled;
                string previousTraceEvent = context.CurrentTraceEventName;
                RuntimeNode previousTraceNode = context.CurrentTraceNode;
                if (traceEnabled)
                {
                    context.SetTraceExecutionState(previousTraceEvent, node);
                    context.RecordTrace(BlueprintTraceRecordKind.NodeEnter, queued.InputPortId, "entered");
                }
                context.SetCurrentExecInputPort(queued.InputPortId);
                BlueprintExecResult result;
                try
                {
                    result = node.Executor.Execute(context, node);
                }
                catch (System.Exception exception)
                {
                    result = BlueprintExecResult.Error(
                        "Node '" + node.Id + "' threw " + exception.GetType().Name + ": " + exception.Message);
                }
                finally
                {
                    context.SetCurrentExecInputPort(previousInputPortId);
                }
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    context.Logger.Error(result.ErrorMessage);
                    context.RecordTrace(BlueprintTraceRecordKind.Error, status: "error", message: result.ErrorMessage);
                    context.RecordTrace(BlueprintTraceRecordKind.NodeExit, status: "error", message: result.ErrorMessage);
                    if (traceEnabled)
                    {
                        context.SetTraceExecutionState(previousTraceEvent, previousTraceNode);
                    }
                    continue;
                }

                if (result.IsSuspended)
                {
                    context.RecordTrace(BlueprintTraceRecordKind.NodeExit, status: "suspended");
                    ScheduleResume(context, node, result);
                    if (traceEnabled)
                    {
                        context.SetTraceExecutionState(previousTraceEvent, previousTraceNode);
                    }
                    continue;
                }

                context.RecordTrace(
                    BlueprintTraceRecordKind.NodeExit,
                    status: HasNextExecutionPort(result) ? "continued" : "stopped");
                EnqueueNext(context, node, result, queue);
                if (traceEnabled)
                {
                    context.SetTraceExecutionState(previousTraceEvent, previousTraceNode);
                }
            }
        }

        public void ExecuteFromOutput(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (context == null || node == null || string.IsNullOrEmpty(outputPortId))
            {
                return;
            }

            context.RecordTrace(BlueprintTraceRecordKind.ExecPortSelected, outputPortId, "selected");
            Queue<QueuedExec> queue = AcquireExecutionQueue();
            try
            {
                EnqueueOutput(context, node, outputPortId, queue);
                ExecuteNodeQueue(context, queue);
            }
            finally
            {
                ReleaseExecutionQueue(queue);
            }
        }

        private void EnqueueNext(BlueprintExecutionContext context, RuntimeNode node, BlueprintExecResult result, Queue<QueuedExec> queue)
        {
            if (result.NextExecPortIds != null && result.NextExecPortIds.Count > 0)
            {
                for (int i = 0; i < result.NextExecPortIds.Count; i++)
                {
                    context.RecordTrace(BlueprintTraceRecordKind.ExecPortSelected, result.NextExecPortIds[i], "selected");
                    EnqueueOutput(context, node, result.NextExecPortIds[i], queue);
                }

                return;
            }

            if (!string.IsNullOrEmpty(result.NextExecPortId))
            {
                context.RecordTrace(BlueprintTraceRecordKind.ExecPortSelected, result.NextExecPortId, "selected");
                EnqueueOutput(context, node, result.NextExecPortId, queue);
            }
        }

        private void EnqueueOutput(BlueprintExecutionContext context, RuntimeNode node, string outputPortId, Queue<QueuedExec> queue)
        {
            List<RuntimeEdge> edges = context.Blueprint.GetExecEdges(new BlueprintPortKey(node.Id, outputPortId));
            if (edges == null)
            {
                return;
            }

            for (int i = 0; i < edges.Count; i++)
            {
                queue.Enqueue(new QueuedExec(edges[i].To.NodeId, edges[i].To.PortId));
            }
        }

        private void ScheduleResume(BlueprintExecutionContext context, RuntimeNode node, BlueprintExecResult result)
        {
            MonoBehaviour coroutineHost = context.OwnerComponent as MonoBehaviour;
            if (coroutineHost == null || result.DelaySeconds <= 0f)
            {
                if (result.DelaySeconds > 0f)
                {
                    context.Logger.Warning("Delay requested without a MonoBehaviour coroutine host; continuing immediately.");
                }

                Queue<QueuedExec> queue = AcquireExecutionQueue();
                try
                {
                    EnqueueNext(context, node, result, queue);
                    ExecuteNodeQueue(context, queue);
                }
                finally
                {
                    ReleaseExecutionQueue(queue);
                }
                return;
            }

            coroutineHost.StartCoroutine(ResumeAfterDelay(
                context,
                node,
                result,
                context.ExecutionGeneration,
                context.CurrentTraceEventName));
        }

        private IEnumerator ResumeAfterDelay(
            BlueprintExecutionContext context,
            RuntimeNode node,
            BlueprintExecResult result,
            int executionGeneration,
            string traceEventName)
        {
            yield return new WaitForSeconds(result.DelaySeconds);
            if (!context.IsExecutionGenerationCurrent(executionGeneration))
            {
                yield break;
            }

            bool traceEnabled = context.IsTraceEnabled;
            string previousTraceEvent = context.CurrentTraceEventName;
            RuntimeNode previousTraceNode = context.CurrentTraceNode;
            if (traceEnabled)
            {
                context.SetTraceExecutionState(traceEventName, node);
            }

            Queue<QueuedExec> queue = AcquireExecutionQueue();
            try
            {
                EnqueueNext(context, node, result, queue);
                ExecuteNodeQueue(context, queue);
            }
            finally
            {
                ReleaseExecutionQueue(queue);
                if (traceEnabled)
                {
                    context.SetTraceExecutionState(previousTraceEvent, previousTraceNode);
                }
            }
        }

        private static bool HasNextExecutionPort(BlueprintExecResult result)
        {
            return (result.NextExecPortIds != null && result.NextExecPortIds.Count > 0) ||
                   !string.IsNullOrEmpty(result.NextExecPortId);
        }

        private Queue<QueuedExec> AcquireExecutionQueue()
        {
            return _executionQueuePool.Count > 0
                ? _executionQueuePool.Pop()
                : new Queue<QueuedExec>();
        }

        private void ReleaseExecutionQueue(Queue<QueuedExec> queue)
        {
            queue.Clear();
            _executionQueuePool.Push(queue);
        }

        private static bool IsDebugLogEnabled(IBlueprintLogger logger)
        {
            return !(logger is UnityBlueprintLogger) || BlueprintLog.DebugEnabled;
        }
    }
}
