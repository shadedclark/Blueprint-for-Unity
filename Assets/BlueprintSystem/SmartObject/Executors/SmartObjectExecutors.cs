using UnityEngine;

namespace BlueprintSystem
{
    public static class SmartObjectExecutorRegistrar
    {
        public static void Register(BlueprintExecutorRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            registry.Register(new SmartObjectFindBestExecutor());
            registry.Register(new SmartObjectFindBestActorExecutor());
            registry.Register(new SmartObjectReserveExecutor());
            registry.Register(new SmartObjectBeginUseExecutor());
            registry.Register(new SmartObjectReleaseExecutor());
            registry.Register(new SmartObjectGetReservationInfoExecutor());
            registry.Register(new SmartObjectReleaseByRequesterExecutor());
        }
    }

    public sealed class SmartObjectFindBestExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "SmartObject.FindBest"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            SmartObjectResult result = SmartObjectRegistry.FindBest(
                context.GetInputValue(node, "requesterId", string.Empty),
                context.GetInputValue(node, "activity", string.Empty),
                GameExecutorValueUtility.GetVector3Input(context, node, "center", Vector3.zero),
                context.GetInputValue(node, "radius", 10f),
                context.GetInputValue(node, "requiredTags", string.Empty),
                context.GetInputValue(node, "forbiddenTags", string.Empty),
                context.GetInputValue(node, "accessGroup", string.Empty),
                context.GetInputValue(node, "needScore", 0f),
                context.GetInputValue(node, "maxDistancePenalty", 0f));
            return SmartObjectExecutorUtility.ReadResult(result, outputPortId);
        }
    }

    public sealed class SmartObjectFindBestActorExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "SmartObject.FindBestActor"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            GameObject excludeGameObject = GameExecutorBindingUtility.ResolveBinding<GameObject>(
                context,
                context.GetInputValue(node, "excludeGameObject"));
            SmartObjectResult result = SmartObjectRegistry.FindBestActor(
                context.GetInputValue(node, "requesterId", string.Empty),
                context.GetInputValue(node, "activity", string.Empty),
                GameExecutorValueUtility.GetVector3Input(context, node, "center", Vector3.zero),
                context.GetInputValue(node, "radius", 10f),
                context.GetInputValue(node, "requiredTags", string.Empty),
                context.GetInputValue(node, "forbiddenTags", string.Empty),
                context.GetInputValue(node, "accessGroup", string.Empty),
                context.GetInputValue(node, "needScore", 0f),
                context.GetInputValue(node, "maxDistancePenalty", 0f),
                excludeGameObject);
            return SmartObjectExecutorUtility.ReadResult(result, outputPortId);
        }
    }

    public sealed class SmartObjectReserveExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "SmartObject.Reserve"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            SmartObjectResult result = SmartObjectRegistry.Reserve(
                context.GetInputValue(node, "requesterId", string.Empty),
                context.GetInputValue(node, "objectId", string.Empty),
                context.GetInputValue(node, "slotId", -1),
                context.GetInputValue(node, "activity", string.Empty),
                context.GetInputValue(node, "holdSeconds", 30f),
                context.GetInputValue(node, "accessGroup", string.Empty));
            SmartObjectExecutorUtility.StoreResult(context, node, result);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return SmartObjectExecutorUtility.ReadResult(SmartObjectExecutorUtility.GetStoredResult(context, node), outputPortId);
        }
    }

    public sealed class SmartObjectBeginUseExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "SmartObject.BeginUse"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            SmartObjectResult result = SmartObjectRegistry.BeginUse(
                context.GetInputValue(node, "requesterId", string.Empty),
                context.GetInputValue(node, "reservationToken", string.Empty));
            SmartObjectExecutorUtility.StoreResult(context, node, result);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return SmartObjectExecutorUtility.ReadResult(SmartObjectExecutorUtility.GetStoredResult(context, node), outputPortId);
        }
    }

    public sealed class SmartObjectReleaseExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "SmartObject.Release"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            SmartObjectResult result = SmartObjectRegistry.Release(
                context.GetInputValue(node, "requesterId", string.Empty),
                context.GetInputValue(node, "reservationToken", string.Empty),
                context.GetInputValue(node, "reason", SmartObjectReleaseReason.Completed));
            SmartObjectExecutorUtility.StoreResult(context, node, result);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return SmartObjectExecutorUtility.ReadResult(SmartObjectExecutorUtility.GetStoredResult(context, node), outputPortId);
        }
    }

    public sealed class SmartObjectGetReservationInfoExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "SmartObject.GetReservationInfo"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            SmartObjectResult result = SmartObjectRegistry.GetReservationInfo(
                context.GetInputValue(node, "reservationToken", string.Empty));
            return SmartObjectExecutorUtility.ReadResult(result, outputPortId);
        }
    }

    public sealed class SmartObjectReleaseByRequesterExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "SmartObject.ReleaseByRequester"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            SmartObjectResult result = SmartObjectRegistry.ReleaseByRequester(
                context.GetInputValue(node, "requesterId", string.Empty),
                context.GetInputValue(node, "reason", SmartObjectReleaseReason.ForceRelease));
            SmartObjectExecutorUtility.StoreResult(context, node, result);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return SmartObjectExecutorUtility.ReadResult(SmartObjectExecutorUtility.GetStoredResult(context, node), outputPortId);
        }
    }

    internal static class SmartObjectExecutorUtility
    {
        public static void StoreResult(BlueprintExecutionContext context, RuntimeNode node, SmartObjectResult result)
        {
            context.SetState(ResultKey(node), result);
        }

        public static SmartObjectResult GetStoredResult(BlueprintExecutionContext context, RuntimeNode node)
        {
            object stored;
            return context.TryGetState(ResultKey(node), out stored) && stored is SmartObjectResult
                ? (SmartObjectResult)stored
                : SmartObjectResult.Default();
        }

        public static object ReadResult(SmartObjectResult result, string outputPortId)
        {
            switch (outputPortId)
            {
                case "found":
                    return result.Found;
                case "success":
                    return result.Success;
                case "valid":
                    return result.Valid;
                case "objectId":
                    return result.ObjectId ?? string.Empty;
                case "slotId":
                    return result.SlotId;
                case "reservationToken":
                    return result.ReservationToken ?? string.Empty;
                case "requesterId":
                    return result.RequesterId ?? string.Empty;
                case "state":
                    return result.State ?? string.Empty;
                case "previousState":
                    return result.PreviousState ?? string.Empty;
                case "targetPosition":
                    return result.TargetPosition;
                case "facingPosition":
                    return result.FacingPosition;
                case "targetGameObject":
                    return result.TargetGameObject;
                case "useDuration":
                    return result.UseDuration;
                case "score":
                    return result.Score;
                case "remainingSeconds":
                    return result.RemainingSeconds;
                case "releasedCount":
                    return result.ReleasedCount;
                case "failReason":
                    return result.FailReason ?? SmartObjectFailReason.None.ToString();
                default:
                    return null;
            }
        }

        private static string ResultKey(RuntimeNode node)
        {
            return "smartObjectResult:" + (node == null ? string.Empty : node.Id);
        }
    }
}
