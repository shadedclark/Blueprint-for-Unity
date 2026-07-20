namespace BlueprintSystem
{
    public sealed class PersistenceSaveExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Persistence.Save"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            BlueprintRunner runner = BlueprintPersistenceRuntime.ResolveRunner(context);
            string slot = context.GetInputValue(node, "slot", string.Empty);
            string error = string.Empty;
            BlueprintPersistenceStatus status = runner == null
                ? BlueprintPersistenceStatus.Failed
                : runner.SavePersistentVariables(slot, out error);
            if (runner == null)
            {
                error = "Persistence.Save requires a BlueprintRunner context.";
            }

            PersistenceExecutorUtility.SetError(context, node, error);
            return BlueprintExecResult.Continue(status == BlueprintPersistenceStatus.Success ? "saved" : "failed");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return PersistenceExecutorUtility.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class PersistenceLoadExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Persistence.Load"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            BlueprintRunner runner = BlueprintPersistenceRuntime.ResolveRunner(context);
            string slot = context.GetInputValue(node, "slot", string.Empty);
            string error = string.Empty;
            BlueprintPersistenceStatus status = runner == null
                ? BlueprintPersistenceStatus.Failed
                : runner.LoadPersistentVariables(slot, out error);
            if (runner == null)
            {
                error = "Persistence.Load requires a BlueprintRunner context.";
            }

            PersistenceExecutorUtility.SetError(context, node, error);
            if (status == BlueprintPersistenceStatus.Success)
            {
                return BlueprintExecResult.Continue("loaded");
            }
            return BlueprintExecResult.Continue(status == BlueprintPersistenceStatus.Missing ? "missing" : "failed");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return PersistenceExecutorUtility.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class PersistenceDeleteExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Persistence.Delete"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            BlueprintRunner runner = BlueprintPersistenceRuntime.ResolveRunner(context);
            string slot = context.GetInputValue(node, "slot", string.Empty);
            string error = string.Empty;
            BlueprintPersistenceStatus status = runner == null
                ? BlueprintPersistenceStatus.Failed
                : runner.DeletePersistentVariables(slot, out error);
            if (runner == null)
            {
                error = "Persistence.Delete requires a BlueprintRunner context.";
            }

            PersistenceExecutorUtility.SetError(context, node, error);
            if (status == BlueprintPersistenceStatus.Success)
            {
                return BlueprintExecResult.Continue("deleted");
            }
            return BlueprintExecResult.Continue(status == BlueprintPersistenceStatus.Missing ? "missing" : "failed");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return PersistenceExecutorUtility.Evaluate(context, node, outputPortId);
        }
    }

    internal static class PersistenceExecutorUtility
    {
        public static void SetError(BlueprintExecutionContext context, RuntimeNode node, string error)
        {
            context.SetState(StateKey(node), error ?? string.Empty);
        }

        public static object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "error")
            {
                return null;
            }

            object value;
            return context.TryGetState(StateKey(node), out value) ? value : string.Empty;
        }

        private static string StateKey(RuntimeNode node)
        {
            return "persistence:" + (node == null ? string.Empty : node.Id) + ":error";
        }
    }
}
