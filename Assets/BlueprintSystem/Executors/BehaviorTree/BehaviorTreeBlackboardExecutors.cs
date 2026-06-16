using System;
using UnityEngine;

namespace BlueprintSystem
{
    internal static class BehaviorTreeBlackboardExecutorUtility
    {
        public static object EvaluateGet(
            BlueprintExecutionContext context,
            RuntimeNode node,
            string outputPortId,
            string executorId,
            object defaultValue,
            Func<object, object> convertValue)
        {
            object value;
            if (outputPortId == "success")
            {
                return TryGetBlackboardValue(context, node, executorId, false, out value);
            }

            if (outputPortId == "value")
            {
                return TryGetBlackboardValue(context, node, executorId, true, out value)
                    ? convertValue(value)
                    : defaultValue;
            }

            context.Logger.Error("Node '" + node.Id + "' does not produce value output '" + outputPortId + "'.");
            return null;
        }

        public static bool TryResolveRunner(BlueprintExecutionContext context, RuntimeNode node, string executorId, bool logFailure, out BehaviorTreeRunner runner)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            runner = GameExecutorBindingUtility.ResolveBinding<BehaviorTreeRunner>(context, target);
            if (runner != null)
            {
                return true;
            }

            if (logFailure)
            {
                context.Logger.Error(executorId + " could not resolve BehaviorTreeRunner binding '" + target + "'.");
            }

            return false;
        }

        public static BlueprintExecResult SetValue(BlueprintExecutionContext context, RuntimeNode node, string executorId, object value)
        {
            BehaviorTreeRunner runner;
            if (!TryResolveRunner(context, node, executorId, false, out runner))
            {
                return BlueprintExecResult.Error(executorId + " could not resolve BehaviorTreeRunner binding.");
            }

            string key = context.GetInputValue(node, "key", string.Empty);
            if (string.IsNullOrEmpty(key))
            {
                return BlueprintExecResult.Error(executorId + " requires a Blackboard key.");
            }

            runner.SetBlackboardValue(key, value);
            return BlueprintExecResult.Continue("execOut");
        }

        public static bool TryGetBlackboardValue(BlueprintExecutionContext context, RuntimeNode node, string executorId, bool logFailure, out object value)
        {
            value = null;
            BehaviorTreeRunner runner;
            if (!TryResolveRunner(context, node, executorId, logFailure, out runner))
            {
                return false;
            }

            string key = context.GetInputValue(node, "key", string.Empty);
            if (string.IsNullOrEmpty(key))
            {
                if (logFailure)
                {
                    context.Logger.Error(executorId + " requires a Blackboard key.");
                }

                return false;
            }

            return runner.TryGetBlackboardValue(key, out value);
        }

        public static object ToBoolObject(object value)
        {
            return BlueprintTypeUtility.ConvertValue(value, false);
        }

        public static object ToIntObject(object value)
        {
            return BlueprintTypeUtility.ConvertValue(value, 0);
        }

        public static object ToFloatObject(object value)
        {
            return BlueprintTypeUtility.ConvertValue(value, 0f);
        }

        public static object ToStringObject(object value)
        {
            return BlueprintTypeUtility.ConvertValue(value, string.Empty);
        }

        public static object ToVector3Object(object value)
        {
            if (value is Vector3)
            {
                return (Vector3)value;
            }

            return BlueprintTypeUtility.ToVector3(value, Vector3.zero);
        }

        public static object ToGameObjectObject(object value)
        {
            return BehaviorTreeValueUtility.ToGameObject(value);
        }

        public static GameObject GetGameObjectInput(BlueprintExecutionContext context, RuntimeNode node)
        {
            object value = context.GetInputValue(node, "value");
            return GameExecutorBindingUtility.ResolveBinding<GameObject>(context, value);
        }
    }

    public sealed class BehaviorTreeGetBlackboardBoolExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.GetBlackboardBool"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return BehaviorTreeBlackboardExecutorUtility.EvaluateGet(context, node, outputPortId, ExecutorId, false, BehaviorTreeBlackboardExecutorUtility.ToBoolObject);
        }
    }

    public sealed class BehaviorTreeGetBlackboardIntExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.GetBlackboardInt"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return BehaviorTreeBlackboardExecutorUtility.EvaluateGet(context, node, outputPortId, ExecutorId, 0, BehaviorTreeBlackboardExecutorUtility.ToIntObject);
        }
    }

    public sealed class BehaviorTreeGetBlackboardFloatExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.GetBlackboardFloat"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return BehaviorTreeBlackboardExecutorUtility.EvaluateGet(context, node, outputPortId, ExecutorId, 0f, BehaviorTreeBlackboardExecutorUtility.ToFloatObject);
        }
    }

    public sealed class BehaviorTreeGetBlackboardStringExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.GetBlackboardString"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return BehaviorTreeBlackboardExecutorUtility.EvaluateGet(context, node, outputPortId, ExecutorId, string.Empty, BehaviorTreeBlackboardExecutorUtility.ToStringObject);
        }
    }

    public sealed class BehaviorTreeGetBlackboardVector3Executor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.GetBlackboardVector3"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return BehaviorTreeBlackboardExecutorUtility.EvaluateGet(context, node, outputPortId, ExecutorId, Vector3.zero, BehaviorTreeBlackboardExecutorUtility.ToVector3Object);
        }
    }

    public sealed class BehaviorTreeGetBlackboardGameObjectExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.GetBlackboardGameObject"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return BehaviorTreeBlackboardExecutorUtility.EvaluateGet(context, node, outputPortId, ExecutorId, null, BehaviorTreeBlackboardExecutorUtility.ToGameObjectObject);
        }
    }

    public sealed class BehaviorTreeSetBlackboardBoolExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.SetBlackboardBool"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            return BehaviorTreeBlackboardExecutorUtility.SetValue(context, node, ExecutorId, context.GetInputValue(node, "value", false));
        }
    }

    public sealed class BehaviorTreeSetBlackboardIntExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.SetBlackboardInt"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            return BehaviorTreeBlackboardExecutorUtility.SetValue(context, node, ExecutorId, context.GetInputValue(node, "value", 0));
        }
    }

    public sealed class BehaviorTreeSetBlackboardFloatExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.SetBlackboardFloat"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            return BehaviorTreeBlackboardExecutorUtility.SetValue(context, node, ExecutorId, context.GetInputValue(node, "value", 0f));
        }
    }

    public sealed class BehaviorTreeSetBlackboardStringExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.SetBlackboardString"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            return BehaviorTreeBlackboardExecutorUtility.SetValue(context, node, ExecutorId, context.GetInputValue(node, "value", string.Empty));
        }
    }

    public sealed class BehaviorTreeSetBlackboardVector3Executor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.SetBlackboardVector3"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            return BehaviorTreeBlackboardExecutorUtility.SetValue(context, node, ExecutorId, GameExecutorValueUtility.GetVector3Input(context, node, "value", Vector3.zero));
        }
    }

    public sealed class BehaviorTreeSetBlackboardGameObjectExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.SetBlackboardGameObject"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            GameObject value = BehaviorTreeBlackboardExecutorUtility.GetGameObjectInput(context, node);
            if (value == null)
            {
                return BlueprintExecResult.Error(ExecutorId + " could not resolve GameObject value.");
            }

            return BehaviorTreeBlackboardExecutorUtility.SetValue(context, node, ExecutorId, value);
        }
    }

    public sealed class BehaviorTreeClearRunnerBlackboardExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "BehaviorTree.ClearBlackboard"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            BehaviorTreeRunner runner;
            if (!BehaviorTreeBlackboardExecutorUtility.TryResolveRunner(context, node, ExecutorId, false, out runner))
            {
                return BlueprintExecResult.Error(ExecutorId + " could not resolve BehaviorTreeRunner binding.");
            }

            string key = context.GetInputValue(node, "key", string.Empty);
            if (string.IsNullOrEmpty(key))
            {
                return BlueprintExecResult.Error(ExecutorId + " requires a Blackboard key.");
            }

            runner.ClearBlackboardValue(key);
            return BlueprintExecResult.Continue("execOut");
        }
    }
}
