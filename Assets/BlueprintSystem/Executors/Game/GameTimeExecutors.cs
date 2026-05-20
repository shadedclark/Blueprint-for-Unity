using UnityEngine;

namespace BlueprintSystem
{
    public sealed class GameGetDeltaTimeExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetDeltaTime"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return Time.deltaTime;
        }
    }

    public sealed class GameGetFixedDeltaTimeExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetFixedDeltaTime"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return Time.fixedDeltaTime;
        }
    }

    public sealed class GameGetTimeSecondsExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetTimeSeconds"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return Time.time;
        }
    }

    public sealed class GameGetUnscaledTimeExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetUnscaledTime"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return Time.unscaledTime;
        }
    }

    public sealed class GameGetTimeScaleExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetTimeScale"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return Time.timeScale;
        }
    }

    public sealed class GameSetTimeScaleExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetTimeScale"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            float value = context.GetInputValue(node, "value", 1f);
            Time.timeScale = Mathf.Max(0f, value);
            return BlueprintExecResult.Continue("execOut");
        }
    }
}
