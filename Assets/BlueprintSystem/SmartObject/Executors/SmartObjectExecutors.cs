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
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.RequesterId, string.Empty),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.Activity, string.Empty),
                SmartObjectCompiledQueryUtility.GetVector3(context, node, SmartObjectCompiledQueryUtility.Center, Vector3.zero),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.Radius, 10f),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.RequiredTags, string.Empty),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.ForbiddenTags, string.Empty),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.AccessGroup, string.Empty),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.NeedScore, 0f),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.MaxDistancePenalty, 0f));
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
                SmartObjectCompiledQueryUtility.GetRaw(context, node, SmartObjectCompiledQueryUtility.ExcludeGameObject));
            SmartObjectResult result = SmartObjectRegistry.FindBestActor(
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.RequesterId, string.Empty),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.Activity, string.Empty),
                SmartObjectCompiledQueryUtility.GetVector3(context, node, SmartObjectCompiledQueryUtility.Center, Vector3.zero),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.Radius, 10f),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.RequiredTags, string.Empty),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.ForbiddenTags, string.Empty),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.AccessGroup, string.Empty),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.NeedScore, 0f),
                SmartObjectCompiledQueryUtility.Get(context, node, SmartObjectCompiledQueryUtility.MaxDistancePenalty, 0f),
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

    internal static class SmartObjectCompiledQueryUtility
    {
        public static readonly int RequesterId = BlueprintStableId.FromString("requesterId");
        public static readonly int Activity = BlueprintStableId.FromString("activity");
        public static readonly int Center = BlueprintStableId.FromString("center");
        public static readonly int Radius = BlueprintStableId.FromString("radius");
        public static readonly int RequiredTags = BlueprintStableId.FromString("requiredTags");
        public static readonly int ForbiddenTags = BlueprintStableId.FromString("forbiddenTags");
        public static readonly int AccessGroup = BlueprintStableId.FromString("accessGroup");
        public static readonly int NeedScore = BlueprintStableId.FromString("needScore");
        public static readonly int MaxDistancePenalty = BlueprintStableId.FromString("maxDistancePenalty");
        public static readonly int ExcludeGameObject = BlueprintStableId.FromString("excludeGameObject");

        public static object GetRaw(BlueprintExecutionContext context, RuntimeNode node, int portStableId)
        {
            CompiledSmartObjectQueryDescription query = context == null || context.Blueprint == null || node == null
                ? null
                : context.Blueprint.GetConstant(node.SpecializedConstantIndex) as CompiledSmartObjectQueryDescription;
            if (query != null)
            {
                for (int i = 0; i < query.Inputs.Count; i++)
                {
                    if (query.Inputs[i].PortStableId == portStableId)
                    {
                        return context.GetInputValue(node, portStableId);
                    }
                }
            }
            return context == null ? null : context.GetInputValue(node, portStableId);
        }

        public static T Get<T>(BlueprintExecutionContext context, RuntimeNode node, int portStableId, T defaultValue)
        {
            return BlueprintTypeUtility.ConvertValue(GetRaw(context, node, portStableId), defaultValue);
        }

        public static Vector3 GetVector3(BlueprintExecutionContext context, RuntimeNode node, int portStableId, Vector3 defaultValue)
        {
            return BlueprintTypeUtility.ToVector3(GetRaw(context, node, portStableId), defaultValue);
        }
    }
}
