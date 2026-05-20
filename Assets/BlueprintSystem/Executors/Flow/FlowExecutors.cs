namespace BlueprintSystem
{
    public sealed class FlowEventExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.Event"; }
        }
    }

    public sealed class FlowPassExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.Pass"; }
        }
    }

    public sealed class FlowBranchExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.Branch"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            bool condition = context.GetInputValue(node, "condition", false);
            return BlueprintExecResult.Continue(condition ? "true" : "false");
        }
    }

    public sealed class FlowSequenceExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.Sequence"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            return BlueprintExecResult.Continue("then0", "then1", "then2", "then3");
        }
    }

    public sealed class FlowDelayExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Flow.Delay"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            float seconds = context.GetInputValue(node, "seconds", 0f);
            return BlueprintExecResult.Suspend(seconds, "execOut");
        }
    }
}
