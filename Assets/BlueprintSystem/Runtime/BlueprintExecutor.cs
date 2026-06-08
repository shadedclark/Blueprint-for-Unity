using System.Collections.Generic;

namespace BlueprintSystem
{
    public interface IBlueprintNodeExecutor
    {
        string ExecutorId { get; }
        BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node);
        object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId);
    }

    public abstract class BlueprintNodeExecutor : IBlueprintNodeExecutor
    {
        public abstract string ExecutorId { get; }

        public virtual BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            return BlueprintExecResult.Continue("execOut");
        }

        public virtual object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            context.Logger.Error("Node '" + node.Id + "' does not produce value output '" + outputPortId + "'.");
            return null;
        }
    }

    public struct BlueprintExecResult
    {
        public string NextExecPortId;
        public List<string> NextExecPortIds;
        public bool IsSuspended;
        public float DelaySeconds;
        public string ErrorMessage;

        public static BlueprintExecResult Continue(string nextExecPortId)
        {
            BlueprintExecResult result = new BlueprintExecResult();
            result.NextExecPortId = nextExecPortId;
            return result;
        }

        public static BlueprintExecResult Continue(params string[] nextExecPortIds)
        {
            BlueprintExecResult result = new BlueprintExecResult();
            result.NextExecPortIds = new List<string>(nextExecPortIds);
            return result;
        }

        public static BlueprintExecResult Stop()
        {
            return new BlueprintExecResult();
        }

        public static BlueprintExecResult Suspend(float delaySeconds, string nextExecPortId)
        {
            BlueprintExecResult result = Continue(nextExecPortId);
            result.IsSuspended = true;
            result.DelaySeconds = delaySeconds;
            return result;
        }

        public static BlueprintExecResult Error(string message)
        {
            BlueprintExecResult result = new BlueprintExecResult();
            result.ErrorMessage = message;
            return result;
        }
    }

    public sealed class BlueprintExecutorRegistry
    {
        private readonly Dictionary<string, IBlueprintNodeExecutor> _executors = new Dictionary<string, IBlueprintNodeExecutor>();

        public void Register(IBlueprintNodeExecutor executor)
        {
            if (executor == null || string.IsNullOrEmpty(executor.ExecutorId))
            {
                return;
            }

            _executors[executor.ExecutorId] = executor;
        }

        public bool TryGet(string executorId, out IBlueprintNodeExecutor executor)
        {
            return _executors.TryGetValue(executorId, out executor);
        }

        public static BlueprintExecutorRegistry CreateDefault()
        {
            BlueprintExecutorRegistry registry = new BlueprintExecutorRegistry();
            registry.Register(new FlowEventExecutor());
            registry.Register(new FlowBranchExecutor());
            registry.Register(new FlowSequenceExecutor());
            registry.Register(new FlowDelayExecutor());
            registry.Register(new FlowForLoopExecutor());
            registry.Register(new FlowForLoopWithBreakExecutor());
            registry.Register(new FlowDoOnceExecutor());
            registry.Register(new FlowDoNExecutor());
            registry.Register(new FlowFlipFlopExecutor());
            registry.Register(new FlowGateExecutor());
            registry.Register(new FlowMultiGateExecutor());
            registry.Register(new FlowSwitchIntExecutor());
            registry.Register(new FlowSwitchStringExecutor());
            registry.Register(new FlowSwitchEnumExecutor());
            registry.Register(new GameGetDeltaTimeExecutor());
            registry.Register(new GameGetFixedDeltaTimeExecutor());
            registry.Register(new GameGetTimeSecondsExecutor());
            registry.Register(new GameGetUnscaledTimeExecutor());
            registry.Register(new GameGetTimeScaleExecutor());
            registry.Register(new GameSetTimeScaleExecutor());
            registry.Register(new MathAddExecutor());
            registry.Register(new MathSubtractExecutor());
            registry.Register(new MathMultiplyExecutor());
            registry.Register(new MathDivideExecutor());
            registry.Register(new MathModuloExecutor());
            registry.Register(new MathAbsExecutor());
            registry.Register(new MathClampExecutor());
            registry.Register(new MathMinExecutor());
            registry.Register(new MathMaxExecutor());
            registry.Register(new MathRoundExecutor());
            registry.Register(new MathFloorExecutor());
            registry.Register(new MathCeilExecutor());
            registry.Register(new MathLerpExecutor());
            registry.Register(new MathMapRangeClampedExecutor());
            registry.Register(new MathRandomFloatExecutor());
            registry.Register(new MathRandomIntExecutor());
            registry.Register(new MathRandomBoolExecutor());
            registry.Register(new VectorMakeVector2Executor());
            registry.Register(new VectorBreakVector2Executor());
            registry.Register(new VectorMakeVector3Executor());
            registry.Register(new VectorBreakVector3Executor());
            registry.Register(new VectorMakeVector4Executor());
            registry.Register(new VectorBreakVector4Executor());
            registry.Register(new VectorAddExecutor());
            registry.Register(new VectorSubtractExecutor());
            registry.Register(new VectorMultiplyExecutor());
            registry.Register(new VectorDivideExecutor());
            registry.Register(new VectorDotExecutor());
            registry.Register(new VectorCrossExecutor());
            registry.Register(new VectorLengthExecutor());
            registry.Register(new VectorNormalizeExecutor());
            registry.Register(new VectorDistanceExecutor());
            registry.Register(new VectorLerpExecutor());
            registry.Register(new ColorMakeExecutor());
            registry.Register(new ColorBreakExecutor());
            registry.Register(new ColorLerpExecutor());
            registry.Register(new StringAppendExecutor());
            registry.Register(new StringFormatExecutor());
            registry.Register(new StringToStringExecutor());
            registry.Register(new StringContainsExecutor());
            registry.Register(new StringStartsWithExecutor());
            registry.Register(new StringEndsWithExecutor());
            registry.Register(new StringReplaceExecutor());
            registry.Register(new StringSplitExecutor());
            registry.Register(new StringLengthExecutor());
            registry.Register(new StringSubstringExecutor());
            registry.Register(new StringEqualIgnoreCaseExecutor());
            registry.Register(new LogicAndExecutor());
            registry.Register(new LogicOrExecutor());
            registry.Register(new LogicNotExecutor());
            registry.Register(new InputGetAxisExecutor());
            registry.Register(new InputGetAxisRawExecutor());
            registry.Register(new InputGetActionVector2Executor());
            registry.Register(new InputListenKeyExecutor());
            registry.Register(new InputListenActionExecutor());
            registry.Register(new VariableGetExecutor());
            registry.Register(new VariableSetExecutor());
            registry.Register(new VariableCompareExecutor());
            registry.Register(new ArrayCountExecutor());
            registry.Register(new ArrayGetExecutor());
            registry.Register(new ArrayForEachLoopExecutor());
            registry.Register(new ArrayForEachLoopWithBreakExecutor());
            registry.Register(new ArrayIsValidIndexExecutor());
            registry.Register(new ArrayContainsExecutor());
            registry.Register(new ArrayIndexOfExecutor());
            registry.Register(new ArrayFirstExecutor());
            registry.Register(new ArrayLastExecutor());
            registry.Register(new ArrayMakeExecutor());
            registry.Register(new ArrayAddExecutor());
            registry.Register(new ArrayAddUniqueExecutor());
            registry.Register(new ArrayInsertExecutor());
            registry.Register(new ArrayRemoveIndexExecutor());
            registry.Register(new ArrayRemoveItemExecutor());
            registry.Register(new ArrayClearExecutor());
            registry.Register(new ArrayResizeExecutor());
            registry.Register(new ArraySetElementExecutor());
            registry.Register(new ArrayAppendExecutor());
            registry.Register(new ArrayRandomItemExecutor());
            registry.Register(new ArrayShuffleExecutor());
            registry.Register(new ArrayLastIndexExecutor());
            registry.Register(new VariableGetFieldExecutor());
            registry.Register(new VariableSetFieldExecutor());
            registry.Register(new VariableBreakStructExecutor());
            registry.Register(new DataTableGetRowExecutor());
            registry.Register(new DataTableGetRowNamesExecutor());
            registry.Register(new DataTableGetAllRowsExecutor());
            registry.Register(new UISetTextExecutor());
            registry.Register(new UIBindTextExecutor());
            registry.Register(new UISetVisibleExecutor());
            registry.Register(new UISetImageSpriteExecutor());
            registry.Register(new UISpriteBindingExecutor());
            registry.Register(new UISetInteractableExecutor());
            registry.Register(new UISetGraphicColorExecutor());
            registry.Register(new UISetGraphicEnabledExecutor());
            registry.Register(new UISetGraphicRaycastTargetExecutor());
            registry.Register(new UISetImageFillAmountExecutor());
            registry.Register(new UISetCanvasGroupAlphaExecutor());
            registry.Register(new UISetCanvasGroupInteractableExecutor());
            registry.Register(new UISetCanvasGroupBlocksRaycastsExecutor());
            registry.Register(new UISetRectAnchoredPositionExecutor());
            registry.Register(new UISetRectSizeDeltaExecutor());
            registry.Register(new UISetRectLocalScaleExecutor());
            registry.Register(new UIBindButtonClickExecutor());
            registry.Register(new UIRefreshLoopScrollViewExecutor());
            registry.Register(new UIBindButtonEventsExecutor());
            registry.Register(new UIBindToggleChangedExecutor());
            registry.Register(new BlueprintIsValidExecutor());
            registry.Register(new BlueprintGetOwnerExecutor());
            registry.Register(new BlueprintGetComponentExecutor());
            registry.Register(new BlueprintTriggerEventExecutor());
            registry.Register(new BlueprintGetVariableExecutor());
            registry.Register(new BlueprintSetVariableExecutor());
            registry.Register(new BehaviorTreeGetBlackboardBoolExecutor());
            registry.Register(new BehaviorTreeGetBlackboardIntExecutor());
            registry.Register(new BehaviorTreeGetBlackboardFloatExecutor());
            registry.Register(new BehaviorTreeGetBlackboardStringExecutor());
            registry.Register(new BehaviorTreeGetBlackboardVector3Executor());
            registry.Register(new BehaviorTreeSetBlackboardBoolExecutor());
            registry.Register(new BehaviorTreeSetBlackboardIntExecutor());
            registry.Register(new BehaviorTreeSetBlackboardFloatExecutor());
            registry.Register(new BehaviorTreeSetBlackboardStringExecutor());
            registry.Register(new BehaviorTreeSetBlackboardVector3Executor());
            registry.Register(new BehaviorTreeClearRunnerBlackboardExecutor());
            SmartObjectExecutorRegistrar.Register(registry);
            registry.Register(new GameLogExecutor());
            registry.Register(new GameSendEventExecutor());
            registry.Register(new GameLoadSceneExecutor());
            registry.Register(new GameLoadSceneAsyncExecutor());
            registry.Register(new GameIsCollidingExecutor());
            registry.Register(new GameGetTransformPositionExecutor());
            registry.Register(new GameGetTransformEulerAnglesExecutor());
            registry.Register(new GameGetTransformLocalPositionExecutor());
            registry.Register(new GameGetTransformLocalEulerAnglesExecutor());
            registry.Register(new GameGetTransformLocalScaleExecutor());
            registry.Register(new GameGetTransformForwardExecutor());
            registry.Register(new GameGetTransformRightExecutor());
            registry.Register(new GameGetTransformUpExecutor());
            registry.Register(new GameSetTransformPositionExecutor());
            registry.Register(new GameSetTransformEulerAnglesExecutor());
            registry.Register(new GameSetTransformLocalPositionExecutor());
            registry.Register(new GameSetTransformLocalEulerAnglesExecutor());
            registry.Register(new GameSetTransformLocalScaleExecutor());
            registry.Register(new GameTranslateTransformExecutor());
            registry.Register(new GameRotateTransformExecutor());
            registry.Register(new GameLookAtTransformExecutor());
            registry.Register(new GameSetTransformParentExecutor());
            registry.Register(new GameDetachTransformExecutor());
            registry.Register(new GameSetRigidbodyLinearVelocityExecutor());
            registry.Register(new GameAddRigidbodyForceExecutor());
            registry.Register(new GameSetColliderEnabledExecutor());
            registry.Register(new GameSetColliderIsTriggerExecutor());
            registry.Register(new GameSetRigidbody2DLinearVelocityExecutor());
            registry.Register(new GameAddRigidbody2DForceExecutor());
            registry.Register(new GameSetCollider2DEnabledExecutor());
            registry.Register(new GameSetCollider2DIsTriggerExecutor());
            registry.Register(new GameSetRendererMaterialExecutor());
            registry.Register(new GameSetRendererMaterialColorExecutor());
            registry.Register(new GameSetRendererTextureExecutor());
            registry.Register(new GameRaycastExecutor());
            registry.Register(new GameSphereCastExecutor());
            registry.Register(new GameBoxCastExecutor());
            registry.Register(new GameOverlapSphereExecutor());
            registry.Register(new GameOverlapBoxExecutor());
            registry.Register(new GameRaycast2DExecutor());
            registry.Register(new GameOverlapCircle2DExecutor());
            registry.Register(new GameOverlapBox2DExecutor());
            return registry;
        }
    }
}
