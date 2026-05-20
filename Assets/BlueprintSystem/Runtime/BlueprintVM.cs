using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class BlueprintVM
    {
        private const int MaxStepsPerEvent = 1024;

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

            string entryNodeId;
            if (!context.Blueprint.EventEntries.TryGetValue(eventName, out entryNodeId))
            {
                context.Logger.Warning("No blueprint event entry named '" + eventName + "'.");
                return;
            }

            ExecuteNodeQueue(context, new List<string> { entryNodeId });
        }

        public void ExecuteNodeQueue(BlueprintExecutionContext context, List<string> nodeIds)
        {
            Queue<QueuedExec> queue = new Queue<QueuedExec>();
            if (nodeIds != null)
            {
                for (int i = 0; i < nodeIds.Count; i++)
                {
                    queue.Enqueue(new QueuedExec(nodeIds[i], null));
                }
            }

            ExecuteNodeQueue(context, queue);
        }

        private void ExecuteNodeQueue(BlueprintExecutionContext context, Queue<QueuedExec> queue)
        {
            int steps = 0;
            while (queue.Count > 0)
            {
                if (++steps > MaxStepsPerEvent)
                {
                    context.Logger.Error("Blueprint execution exceeded " + MaxStepsPerEvent + " steps.");
                    return;
                }

                QueuedExec queued = queue.Dequeue();
                RuntimeNode node = context.Blueprint.GetNode(queued.NodeId);
                if (node == null)
                {
                    context.Logger.Error("Missing runtime node '" + queued.NodeId + "'.");
                    continue;
                }

                if (node.Executor == null)
                {
                    context.Logger.Error("Node '" + node.Id + "' has no runtime executor.");
                    continue;
                }

                context.ClearValueCache();
                context.Logger.Log("Execute " + node.Id + " (" + node.TypeId + ")");
                string previousInputPortId = context.CurrentExecInputPortId;
                context.SetCurrentExecInputPort(queued.InputPortId);
                BlueprintExecResult result;
                try
                {
                    result = node.Executor.Execute(context, node);
                }
                finally
                {
                    context.SetCurrentExecInputPort(previousInputPortId);
                }
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    context.Logger.Error(result.ErrorMessage);
                    continue;
                }

                if (result.IsSuspended)
                {
                    ScheduleResume(context, node, result);
                    continue;
                }

                EnqueueNext(context, node, result, queue);
            }
        }

        public void ExecuteFromOutput(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (context == null || node == null || string.IsNullOrEmpty(outputPortId))
            {
                return;
            }

            Queue<QueuedExec> queue = new Queue<QueuedExec>();
            EnqueueOutput(context, node, outputPortId, queue);
            ExecuteNodeQueue(context, queue);
        }

        private void EnqueueNext(BlueprintExecutionContext context, RuntimeNode node, BlueprintExecResult result, Queue<QueuedExec> queue)
        {
            if (result.NextExecPortIds != null && result.NextExecPortIds.Count > 0)
            {
                for (int i = 0; i < result.NextExecPortIds.Count; i++)
                {
                    EnqueueOutput(context, node, result.NextExecPortIds[i], queue);
                }

                return;
            }

            if (!string.IsNullOrEmpty(result.NextExecPortId))
            {
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

                Queue<QueuedExec> queue = new Queue<QueuedExec>();
                EnqueueNext(context, node, result, queue);
                ExecuteNodeQueue(context, queue);
                return;
            }

            coroutineHost.StartCoroutine(ResumeAfterDelay(context, node, result));
        }

        private IEnumerator ResumeAfterDelay(BlueprintExecutionContext context, RuntimeNode node, BlueprintExecResult result)
        {
            yield return new WaitForSeconds(result.DelaySeconds);
            Queue<QueuedExec> queue = new Queue<QueuedExec>();
            EnqueueNext(context, node, result, queue);
            ExecuteNodeQueue(context, queue);
        }
    }
}
