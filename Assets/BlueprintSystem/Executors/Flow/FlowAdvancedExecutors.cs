using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class FlowForLoopExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.ForLoop"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            int firstIndex = context.GetInputValue(node, "firstIndex", 0);
            int lastIndex = context.GetInputValue(node, "lastIndex", 0);
            int step = firstIndex <= lastIndex ? 1 : -1;
            for (int index = firstIndex; step > 0 ? index <= lastIndex : index >= lastIndex; index += step)
            {
                context.SetLoopValue(node, "index", index);
                context.ExecuteFromOutput(node, "loopBody");
            }

            context.ClearLoopValues(node);
            return BlueprintExecResult.Continue("completed");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            object value;
            if (outputPortId == "index")
            {
                return context.TryGetLoopValue(node, outputPortId, out value) ? value : -1;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class FlowForLoopWithBreakExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.ForLoopWithBreak"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            if (context.CurrentExecInputPortId == "break")
            {
                FlowLoopBreakUtility.RequestBreak(context, node);
                return BlueprintExecResult.Stop();
            }

            int firstIndex = context.GetInputValue(node, "firstIndex", 0);
            int lastIndex = context.GetInputValue(node, "lastIndex", 0);
            int step = firstIndex <= lastIndex ? 1 : -1;
            FlowLoopBreakUtility.Begin(context, node);
            try
            {
                for (int index = firstIndex; step > 0 ? index <= lastIndex : index >= lastIndex; index += step)
                {
                    context.SetLoopValue(node, "index", index);
                    context.ExecuteFromOutput(node, "loopBody");
                    if (FlowLoopBreakUtility.IsBreakRequested(context, node))
                    {
                        break;
                    }
                }
            }
            finally
            {
                context.ClearLoopValues(node);
                FlowLoopBreakUtility.End(context, node);
            }

            return BlueprintExecResult.Continue("completed");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            object value;
            if (outputPortId == "index")
            {
                return context.TryGetLoopValue(node, outputPortId, out value) ? value : -1;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class FlowDoOnceExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.DoOnce"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string key = FlowStateUtility.Key(node, "doOnceDone");
            if (context.CurrentExecInputPortId == "reset")
            {
                context.SetState(key, false);
                return BlueprintExecResult.Stop();
            }

            bool done;
            if (!FlowStateUtility.TryGetBool(context, key, out done))
            {
                done = context.GetInputValue(node, "startClosed", false);
            }

            if (done)
            {
                return BlueprintExecResult.Stop();
            }

            context.SetState(key, true);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class FlowDoNExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.DoN"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string key = FlowStateUtility.Key(node, "doNCount");
            if (context.CurrentExecInputPortId == "reset")
            {
                context.SetState(key, 0);
                return BlueprintExecResult.Stop();
            }

            int limit = Mathf.Max(0, context.GetInputValue(node, "count", 1));
            int current;
            if (!FlowStateUtility.TryGetInt(context, key, out current))
            {
                current = 0;
            }

            if (current >= limit)
            {
                return BlueprintExecResult.Continue("completed");
            }

            context.SetState(key, current + 1);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class FlowFlipFlopExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.FlipFlop"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string lastKey = FlowStateUtility.Key(node, "flipFlopLastA");
            string currentKey = FlowStateUtility.Key(node, "flipFlopIsA");
            bool lastA;
            FlowStateUtility.TryGetBool(context, lastKey, out lastA);
            bool isA = !lastA;
            context.SetState(lastKey, isA);
            context.SetState(currentKey, isA);
            return BlueprintExecResult.Continue(isA ? "a" : "b");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "isA")
            {
                bool value;
                return FlowStateUtility.TryGetBool(context, FlowStateUtility.Key(node, "flipFlopIsA"), out value) && value;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class FlowGateExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.Gate"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string key = FlowStateUtility.Key(node, "gateOpen");
            bool isOpen;
            if (!FlowStateUtility.TryGetBool(context, key, out isOpen))
            {
                isOpen = !context.GetInputValue(node, "startClosed", false);
            }

            if (context.CurrentExecInputPortId == "open")
            {
                context.SetState(key, true);
                return BlueprintExecResult.Stop();
            }

            if (context.CurrentExecInputPortId == "close")
            {
                context.SetState(key, false);
                return BlueprintExecResult.Stop();
            }

            if (context.CurrentExecInputPortId == "toggle")
            {
                context.SetState(key, !isOpen);
                return BlueprintExecResult.Stop();
            }

            return isOpen ? BlueprintExecResult.Continue("exit") : BlueprintExecResult.Stop();
        }
    }

    public sealed class FlowMultiGateExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.MultiGate"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            if (context.CurrentExecInputPortId == "reset")
            {
                Reset(context, node);
                return BlueprintExecResult.Stop();
            }

            int outputCount = Mathf.Clamp(context.GetInputValue(node, "outputCount", 2), 1, 8);
            bool loop = context.GetInputValue(node, "loop", false);
            bool random = context.GetInputValue(node, "random", false);
            int selected = random ? SelectRandom(context, node, outputCount, loop) : SelectSequential(context, node, outputCount, loop);
            if (selected < 0)
            {
                return BlueprintExecResult.Stop();
            }

            return BlueprintExecResult.Continue("out" + selected);
        }

        private static int SelectSequential(BlueprintExecutionContext context, RuntimeNode node, int outputCount, bool loop)
        {
            string key = FlowStateUtility.Key(node, "multiGateIndex");
            int startIndex = Mathf.Clamp(context.GetInputValue(node, "startIndex", 0), 0, outputCount - 1);
            int index;
            if (!FlowStateUtility.TryGetInt(context, key, out index))
            {
                index = startIndex;
            }

            if (index >= outputCount)
            {
                if (!loop)
                {
                    return -1;
                }

                index = 0;
            }

            context.SetState(key, index + 1);
            return index;
        }

        private static int SelectRandom(BlueprintExecutionContext context, RuntimeNode node, int outputCount, bool loop)
        {
            string key = FlowStateUtility.Key(node, "multiGateVisited");
            HashSet<int> visited;
            object value;
            if (context.TryGetState(key, out value))
            {
                visited = value as HashSet<int>;
            }
            else
            {
                visited = null;
            }

            if (visited == null || visited.Count >= outputCount)
            {
                if (visited != null && !loop)
                {
                    return -1;
                }

                visited = new HashSet<int>();
                context.SetState(key, visited);
            }

            List<int> candidates = new List<int>();
            for (int i = 0; i < outputCount; i++)
            {
                if (!visited.Contains(i))
                {
                    candidates.Add(i);
                }
            }

            int selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            visited.Add(selected);
            return selected;
        }

        private static void Reset(BlueprintExecutionContext context, RuntimeNode node)
        {
            context.RemoveState(FlowStateUtility.Key(node, "multiGateIndex"));
            context.RemoveState(FlowStateUtility.Key(node, "multiGateVisited"));
        }
    }

    public sealed class FlowSwitchIntExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.SwitchInt"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            int selection = context.GetInputValue(node, "selection", 0);
            int caseCount = Mathf.Clamp(context.GetInputValue(node, "caseCount", 4), 1, 8);
            for (int i = 0; i < caseCount; i++)
            {
                if (selection == context.GetInputValue(node, "case" + i, i))
                {
                    return BlueprintExecResult.Continue("case" + i);
                }
            }

            return BlueprintExecResult.Continue("default");
        }
    }

    public sealed class FlowSwitchStringExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.SwitchString"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            return SwitchStringLike(context, node);
        }

        internal static BlueprintExecResult SwitchStringLike(BlueprintExecutionContext context, RuntimeNode node)
        {
            string selection = context.GetInputValue(node, "selection", string.Empty);
            int caseCount = Mathf.Clamp(context.GetInputValue(node, "caseCount", 4), 1, 8);
            for (int i = 0; i < caseCount; i++)
            {
                string value = context.GetInputValue(node, "case" + i, string.Empty);
                if (string.Equals(selection, value, StringComparison.Ordinal))
                {
                    return BlueprintExecResult.Continue("case" + i);
                }
            }

            return BlueprintExecResult.Continue("default");
        }
    }

    public sealed class FlowSwitchEnumExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.SwitchEnum"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            return FlowSwitchStringExecutor.SwitchStringLike(context, node);
        }
    }

    internal static class FlowLoopBreakUtility
    {
        private static string ActiveKey(RuntimeNode node)
        {
            return FlowStateUtility.Key(node, "loopActive");
        }

        private static string BreakKey(RuntimeNode node)
        {
            return FlowStateUtility.Key(node, "loopBreak");
        }

        public static void Begin(BlueprintExecutionContext context, RuntimeNode node)
        {
            context.SetState(ActiveKey(node), true);
            context.SetState(BreakKey(node), false);
        }

        public static void End(BlueprintExecutionContext context, RuntimeNode node)
        {
            context.RemoveState(ActiveKey(node));
            context.RemoveState(BreakKey(node));
        }

        public static void RequestBreak(BlueprintExecutionContext context, RuntimeNode node)
        {
            if (context.HasState(ActiveKey(node)))
            {
                context.SetState(BreakKey(node), true);
            }
        }

        public static bool IsBreakRequested(BlueprintExecutionContext context, RuntimeNode node)
        {
            bool value;
            return FlowStateUtility.TryGetBool(context, BreakKey(node), out value) && value;
        }
    }

    internal static class FlowStateUtility
    {
        public static string Key(RuntimeNode node, string suffix)
        {
            return "flow:" + node.Id + ":" + suffix;
        }

        public static bool TryGetBool(BlueprintExecutionContext context, string key, out bool value)
        {
            value = false;
            object state;
            if (!context.TryGetState(key, out state) || !(state is bool))
            {
                return false;
            }

            value = (bool)state;
            return true;
        }

        public static bool TryGetInt(BlueprintExecutionContext context, string key, out int value)
        {
            value = 0;
            object state;
            if (!context.TryGetState(key, out state))
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(state);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
