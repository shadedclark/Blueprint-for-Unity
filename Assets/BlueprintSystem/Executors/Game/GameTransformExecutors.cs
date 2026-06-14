using UnityEngine;

namespace BlueprintSystem
{
    public sealed class GameGetTransformPositionExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetTransformPosition"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId, true);
            return transform == null ? Vector3.zero : transform.position;
        }
    }

    public sealed class GameGetTransformEulerAnglesExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetTransformEulerAngles"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId, true);
            return transform == null ? Vector3.zero : transform.eulerAngles;
        }
    }

    public sealed class GameGetTransformLocalPositionExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetTransformLocalPosition"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId, true);
            return transform == null ? Vector3.zero : transform.localPosition;
        }
    }

    public sealed class GameGetTransformLocalEulerAnglesExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetTransformLocalEulerAngles"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId, true);
            return transform == null ? Vector3.zero : transform.localEulerAngles;
        }
    }

    public sealed class GameGetTransformLocalScaleExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetTransformLocalScale"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId, true);
            return transform == null ? Vector3.one : transform.localScale;
        }
    }

    public sealed class GameGetTransformForwardExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetTransformForward"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId, true);
            return transform == null ? Vector3.forward : transform.forward;
        }
    }

    public sealed class GameGetTransformRightExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetTransformRight"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId, true);
            return transform == null ? Vector3.right : transform.right;
        }
    }

    public sealed class GameGetTransformUpExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.GetTransformUp"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId, true);
            return transform == null ? Vector3.up : transform.up;
        }
    }

    public sealed class GameSetTransformPositionExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetTransformPosition"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            object target = context.GetInputValue(node, "target");
            Transform transform = GameExecutorBindingUtility.ResolveBinding<Transform>(context, target);
            if (transform == null)
            {
                return BlueprintExecResult.Error("Game.SetTransformPosition could not resolve Transform binding '" + target + "'.");
            }

            transform.position = GameExecutorValueUtility.GetVector3Input(context, node, "value", Vector3.zero);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameSetTransformEulerAnglesExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetTransformEulerAngles"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            object target = context.GetInputValue(node, "target");
            Transform transform = GameExecutorBindingUtility.ResolveBinding<Transform>(context, target);
            if (transform == null)
            {
                return BlueprintExecResult.Error("Game.SetTransformEulerAngles could not resolve Transform binding '" + target + "'.");
            }

            transform.eulerAngles = GameExecutorValueUtility.GetVector3Input(context, node, "value", Vector3.zero);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameSetTransformLocalPositionExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetTransformLocalPosition"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId);
            if (transform == null)
            {
                return GameTransformExecutorUtility.ResolveError(context, node, ExecutorId);
            }

            transform.localPosition = GameExecutorValueUtility.GetVector3Input(context, node, "value", Vector3.zero);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameSetTransformLocalEulerAnglesExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetTransformLocalEulerAngles"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId);
            if (transform == null)
            {
                return GameTransformExecutorUtility.ResolveError(context, node, ExecutorId);
            }

            transform.localEulerAngles = GameExecutorValueUtility.GetVector3Input(context, node, "value", Vector3.zero);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameSetTransformLocalScaleExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetTransformLocalScale"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            object target = context.GetInputValue(node, "target");
            Transform transform = GameExecutorBindingUtility.ResolveBinding<Transform>(context, target);
            if (transform == null)
            {
                return BlueprintExecResult.Error("Game.SetTransformLocalScale could not resolve Transform binding '" + target + "'.");
            }

            transform.localScale = GameExecutorValueUtility.GetVector3Input(context, node, "value", Vector3.one);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameTranslateTransformExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.TranslateTransform"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId);
            if (transform == null)
            {
                return GameTransformExecutorUtility.ResolveError(context, node, ExecutorId);
            }

            Vector3 translation = GameExecutorValueUtility.GetVector3Input(context, node, "translation", Vector3.zero);
            Space space = context.GetInputValue(node, "relativeToSelf", true) ? Space.Self : Space.World;
            transform.Translate(translation, space);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameRotateTransformExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.RotateTransform"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId);
            if (transform == null)
            {
                return GameTransformExecutorUtility.ResolveError(context, node, ExecutorId);
            }

            Vector3 eulerAngles = GameExecutorValueUtility.GetVector3Input(context, node, "eulerAngles", Vector3.zero);
            Space space = context.GetInputValue(node, "relativeToSelf", true) ? Space.Self : Space.World;
            transform.Rotate(eulerAngles, space);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameLookAtTransformExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.LookAtTransform"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId);
            if (transform == null)
            {
                return GameTransformExecutorUtility.ResolveError(context, node, ExecutorId);
            }

            object lookTarget = context.GetInputValue(node, "lookTarget");
            Transform lookTargetTransform = GameExecutorBindingUtility.ResolveBinding<Transform>(context, lookTarget);
            Vector3 position = lookTargetTransform == null
                ? GameExecutorValueUtility.GetVector3Input(context, node, "targetPosition", Vector3.zero)
                : lookTargetTransform.position;
            Vector3 worldUp = GameExecutorValueUtility.GetVector3Input(context, node, "worldUp", Vector3.up);
            transform.LookAt(position, worldUp.sqrMagnitude <= 0.0001f ? Vector3.up : worldUp.normalized);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameSetTransformParentExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetTransformParent"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId);
            if (transform == null)
            {
                return GameTransformExecutorUtility.ResolveError(context, node, ExecutorId);
            }

            object parentValue = context.GetInputValue(node, "parent");
            Transform parent = GameExecutorBindingUtility.ResolveBinding<Transform>(context, parentValue);
            if (parent == null)
            {
                return BlueprintExecResult.Error("Game.SetTransformParent could not resolve parent Transform binding '" + parentValue + "'.");
            }

            bool worldPositionStays = context.GetInputValue(node, "worldPositionStays", true);
            transform.SetParent(parent, worldPositionStays);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameDetachTransformExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.DetachTransform"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            Transform transform = GameTransformExecutorUtility.ResolveTransform(context, node, ExecutorId);
            if (transform == null)
            {
                return GameTransformExecutorUtility.ResolveError(context, node, ExecutorId);
            }

            bool worldPositionStays = context.GetInputValue(node, "worldPositionStays", true);
            transform.SetParent(null, worldPositionStays);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    internal static class GameTransformExecutorUtility
    {
        public static Transform ResolveTransform(BlueprintExecutionContext context, RuntimeNode node, string executorId, bool logMissing = false)
        {
            object target = context.GetInputValue(node, "target");
            Transform transform = GameExecutorBindingUtility.ResolveBinding<Transform>(context, target);
            if (logMissing && transform == null && context != null && context.Logger != null)
            {
                context.Logger.Error(executorId + " could not resolve Transform binding '" + target + "'.");
            }

            return transform;
        }

        public static BlueprintExecResult ResolveError(BlueprintExecutionContext context, RuntimeNode node, string executorId)
        {
            object target = context.GetInputValue(node, "target");
            return BlueprintExecResult.Error(executorId + " could not resolve Transform binding '" + target + "'.");
        }
    }
}
