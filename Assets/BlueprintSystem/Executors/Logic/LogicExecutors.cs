namespace BlueprintSystem
{
    public sealed class LogicAndExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Logic.And"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            bool left = context.GetInputValue(node, "left", false);
            bool right = context.GetInputValue(node, "right", false);
            return left && right;
        }
    }

    public sealed class LogicOrExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Logic.Or"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            bool left = context.GetInputValue(node, "left", false);
            bool right = context.GetInputValue(node, "right", false);
            return left || right;
        }
    }

    public sealed class LogicNotExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Logic.Not"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            bool value = context.GetInputValue(node, "value", false);
            return !value;
        }
    }
}
