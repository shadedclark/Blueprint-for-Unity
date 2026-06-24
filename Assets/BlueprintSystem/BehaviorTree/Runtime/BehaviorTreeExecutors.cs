using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.AI;
using VehicleRoads;

namespace BlueprintSystem
{
    public interface IBehaviorTreeNodeExecutor
    {
        string TypeId { get; }
        BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node);
        void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node);
    }

    public interface IBehaviorTreeDecoratorExecutor
    {
        string TypeId { get; }
        bool Evaluate(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeDecorator decorator);
    }

    public interface IBehaviorTreeServiceExecutor
    {
        string TypeId { get; }
        void OnEnter(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service);
        void Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service);
        void OnExit(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service);
    }

    public abstract class BehaviorTreeNodeExecutor : IBehaviorTreeNodeExecutor
    {
        public abstract string TypeId { get; }
        public abstract BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node);

        public virtual void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
        }
    }

    public abstract class BehaviorTreeDecoratorExecutor : IBehaviorTreeDecoratorExecutor
    {
        public abstract string TypeId { get; }
        public abstract bool Evaluate(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeDecorator decorator);
    }

    public abstract class BehaviorTreeServiceExecutor : IBehaviorTreeServiceExecutor
    {
        public abstract string TypeId { get; }

        public virtual void OnEnter(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service)
        {
        }

        public abstract void Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service);

        public virtual void OnExit(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service)
        {
        }
    }

    public sealed class BehaviorTreeExecutorRegistry
    {
        private readonly Dictionary<string, IBehaviorTreeNodeExecutor> _nodes = new Dictionary<string, IBehaviorTreeNodeExecutor>(StringComparer.Ordinal);
        private readonly Dictionary<string, IBehaviorTreeDecoratorExecutor> _decorators = new Dictionary<string, IBehaviorTreeDecoratorExecutor>(StringComparer.Ordinal);
        private readonly Dictionary<string, IBehaviorTreeServiceExecutor> _services = new Dictionary<string, IBehaviorTreeServiceExecutor>(StringComparer.Ordinal);

        public void Register(IBehaviorTreeNodeExecutor executor)
        {
            if (executor != null && !string.IsNullOrEmpty(executor.TypeId))
            {
                _nodes[executor.TypeId] = executor;
            }
        }

        public void Register(IBehaviorTreeDecoratorExecutor executor)
        {
            if (executor != null && !string.IsNullOrEmpty(executor.TypeId))
            {
                _decorators[executor.TypeId] = executor;
            }
        }

        public void Register(IBehaviorTreeServiceExecutor executor)
        {
            if (executor != null && !string.IsNullOrEmpty(executor.TypeId))
            {
                _services[executor.TypeId] = executor;
            }
        }

        public bool TryGetNode(string typeId, out IBehaviorTreeNodeExecutor executor)
        {
            return _nodes.TryGetValue(typeId, out executor);
        }

        public bool TryGetDecorator(string typeId, out IBehaviorTreeDecoratorExecutor executor)
        {
            return _decorators.TryGetValue(typeId, out executor);
        }

        public bool TryGetService(string typeId, out IBehaviorTreeServiceExecutor executor)
        {
            return _services.TryGetValue(typeId, out executor);
        }

        public bool HasNode(string typeId)
        {
            return _nodes.ContainsKey(typeId);
        }

        public bool HasDecorator(string typeId)
        {
            return _decorators.ContainsKey(typeId);
        }

        public bool HasService(string typeId)
        {
            return _services.ContainsKey(typeId);
        }

        public static BehaviorTreeExecutorRegistry CreateDefault()
        {
            BehaviorTreeExecutorRegistry registry = new BehaviorTreeExecutorRegistry();
            if (!BlueprintModuleSettings.BehaviorTreeEnabled)
            {
                return registry;
            }

            registry.Register(new BehaviorTreeRootExecutor());
            registry.Register(new BehaviorTreeSelectorExecutor());
            registry.Register(new BehaviorTreeSequenceExecutor());
            registry.Register(new BehaviorTreeParallelExecutor());
            registry.Register(new BehaviorTreeRandomSelectorExecutor());
            registry.Register(new BehaviorTreePrioritySelectorExecutor());
            registry.Register(new BehaviorTreeWeightedSelectorExecutor());
            registry.Register(new BehaviorTreeWaitExecutor());
            registry.Register(new BehaviorTreeSetBlackboardExecutor());
            registry.Register(new BehaviorTreeClearBlackboardExecutor());
            registry.Register(new BehaviorTreeSetRunnerBlackboardExecutor());
            registry.Register(new BehaviorTreeGetRunnerBlackboardExecutor());
            registry.Register(new BehaviorTreeClearRunnerBlackboardTaskExecutor());
            registry.Register(new BehaviorTreeCopyRunnerBlackboardExecutor());
            registry.Register(new BehaviorTreeRunSubtreeExecutor());
            registry.Register(new BehaviorTreeMoveToExecutor());
            registry.Register(new BehaviorTreeStopNavigationExecutor());
            registry.Register(new BehaviorTreeSetNavigationDestinationExecutor());
            registry.Register(new BehaviorTreeCalculateNavigationPathExecutor());
            registry.Register(new BehaviorTreeSetNavigationPathExecutor());
            registry.Register(new BehaviorTreeWaitForNavigationExecutor());
            registry.Register(new BehaviorTreePauseNavigationExecutor());
            registry.Register(new BehaviorTreeResumeNavigationExecutor());
            registry.Register(new BehaviorTreeSampleNavMeshPositionExecutor());
            registry.Register(new BehaviorTreeWarpNavigationExecutor());
            registry.Register(new BehaviorTreeTraverseOffMeshLinkExecutor());
            registry.Register(new BehaviorTreeRotateToExecutor());
            registry.Register(new BehaviorTreeTriggerBlueprintEventExecutor());
            registry.Register(new BehaviorTreeRunBlueprintTaskExecutor());
            registry.Register(new BehaviorTreeLogExecutor());
            registry.Register(new BehaviorTreeBlackboardConditionDecorator());
            registry.Register(new BehaviorTreeCompareFloatDecorator());
            registry.Register(new BehaviorTreeCompareBoolDecorator());
            registry.Register(new BehaviorTreeObjectIsSetDecorator());
            registry.Register(new BehaviorTreeDistanceLessThanDecorator());
            registry.Register(new BehaviorTreeCooldownDecorator());
            registry.Register(new BehaviorTreeNavigationConditionDecorator());
            registry.Register(new BehaviorTreeUpdateDistanceService());
            registry.Register(new BehaviorTreeUpdateNavigationStateService());
            registry.Register(new BehaviorTreePerceptionSphereService());
            registry.Register(new BehaviorTreePerceptionRaycastService());
            registry.Register(new BehaviorTreeSetBlackboardFromBlueprintService());
            registry.Register(new BehaviorTreeTriggerBlueprintService());
            if (BlueprintModuleSettings.VehicleRoadsEnabled)
            {
                registry.Register(new BehaviorTreeVehicleRoadFindNearestLaneExecutor());
                registry.Register(new BehaviorTreeVehicleRoadFindLaneRouteExecutor());
                registry.Register(new BehaviorTreeVehicleRoadComputeFollowerControlExecutor());
                registry.Register(new BehaviorTreeVehicleRoadDriveFollowerExecutor());
                registry.Register(new BehaviorTreeVehicleRoadUpdateRoadAgentService());
            }
            return registry;
        }
    }

    internal sealed class BehaviorTreeRootExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return BehaviorTreeNodeTypeUtility.Root; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            if (node.Children.Count != 1)
            {
                context.Runtime.MarkFailure("Root node must have exactly one child.");
                return BehaviorTreeStatus.Failure;
            }

            return context.TickChild(node.Children[0]);
        }
    }

    internal sealed class BehaviorTreeSelectorExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return BehaviorTreeNodeTypeUtility.Selector; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            if (node.Children.Count == 0)
            {
                context.Runtime.MarkFailure("Selector node '" + node.Id + "' has no children.");
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            int startIndex = state.RunningChildIndex >= 0 && state.RunningChildIndex < node.Children.Count
                ? state.RunningChildIndex
                : 0;

            for (int i = startIndex; i < node.Children.Count; i++)
            {
                BehaviorTreeStatus childStatus = context.TickChild(node.Children[i]);
                if (childStatus == BehaviorTreeStatus.Success)
                {
                    state.RunningChildIndex = -1;
                    return BehaviorTreeStatus.Success;
                }

                if (childStatus == BehaviorTreeStatus.Running)
                {
                    state.RunningChildIndex = i;
                    return BehaviorTreeStatus.Running;
                }
            }

            state.RunningChildIndex = -1;
            return BehaviorTreeStatus.Failure;
        }
    }

    internal sealed class BehaviorTreeSequenceExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return BehaviorTreeNodeTypeUtility.Sequence; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            if (node.Children.Count == 0)
            {
                context.Runtime.MarkFailure("Sequence node '" + node.Id + "' has no children.");
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            int startIndex = state.RunningChildIndex >= 0 && state.RunningChildIndex < node.Children.Count
                ? state.RunningChildIndex
                : 0;

            for (int i = startIndex; i < node.Children.Count; i++)
            {
                BehaviorTreeStatus childStatus = context.TickChild(node.Children[i]);
                if (childStatus == BehaviorTreeStatus.Failure)
                {
                    state.RunningChildIndex = -1;
                    return BehaviorTreeStatus.Failure;
                }

                if (childStatus == BehaviorTreeStatus.Running)
                {
                    state.RunningChildIndex = i;
                    return BehaviorTreeStatus.Running;
                }
            }

            state.RunningChildIndex = -1;
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeParallelExecutor : BehaviorTreeNodeExecutor
    {
        private const string CompletedChildrenKey = "completedChildren";

        public override string TypeId
        {
            get { return BehaviorTreeNodeTypeUtility.Parallel; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            if (node.Children.Count == 0)
            {
                context.Runtime.MarkFailure("Parallel node '" + node.Id + "' has no children.");
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            HashSet<int> completedChildren = GetCompletedChildren(state, node.Children.Count);
            bool hasRunningChild = false;

            for (int i = 0; i < node.Children.Count; i++)
            {
                if (completedChildren.Contains(i))
                {
                    continue;
                }

                BehaviorTreeStatus childStatus = context.TickChild(node.Children[i]);
                if (childStatus == BehaviorTreeStatus.Failure)
                {
                    ClearCompletedChildren(state);
                    state.RunningChildIndex = -1;
                    AbortRunningSiblings(context, node, i, "Parallel node '" + node.Id + "' failed.");
                    return BehaviorTreeStatus.Failure;
                }

                if (childStatus == BehaviorTreeStatus.Running)
                {
                    hasRunningChild = true;
                    state.RunningChildIndex = i;
                    continue;
                }

                completedChildren.Add(i);
            }

            if (hasRunningChild || completedChildren.Count < node.Children.Count)
            {
                return BehaviorTreeStatus.Running;
            }

            ClearCompletedChildren(state);
            state.RunningChildIndex = -1;
            return BehaviorTreeStatus.Success;
        }

        public override void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            ClearCompletedChildren(context.GetNodeState(node));
        }

        private static HashSet<int> GetCompletedChildren(BehaviorTreeNodeRuntimeState state, int childCount)
        {
            object value;
            HashSet<int> completedChildren = state.Data.TryGetValue(CompletedChildrenKey, out value)
                ? value as HashSet<int>
                : null;

            if (completedChildren == null)
            {
                completedChildren = new HashSet<int>();
                state.Data[CompletedChildrenKey] = completedChildren;
            }

            List<int> invalidIndices = null;
            foreach (int index in completedChildren)
            {
                if (index < 0 || index >= childCount)
                {
                    if (invalidIndices == null)
                    {
                        invalidIndices = new List<int>();
                    }

                    invalidIndices.Add(index);
                }
            }

            if (invalidIndices != null)
            {
                for (int i = 0; i < invalidIndices.Count; i++)
                {
                    completedChildren.Remove(invalidIndices[i]);
                }
            }

            return completedChildren;
        }

        private static void ClearCompletedChildren(BehaviorTreeNodeRuntimeState state)
        {
            state.Data.Remove(CompletedChildrenKey);
        }

        private static void AbortRunningSiblings(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            int completedChildIndex,
            string reason)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (i == completedChildIndex)
                {
                    continue;
                }

                context.AbortChild(node.Children[i], reason);
            }
        }
    }

    internal sealed class BehaviorTreeRandomSelectorExecutor : BehaviorTreeNodeExecutor
    {
        private const string RandomOrderKey = "randomOrder";

        public override string TypeId
        {
            get { return BehaviorTreeNodeTypeUtility.RandomSelector; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            if (node.Children.Count == 0)
            {
                context.Runtime.MarkFailure("RandomSelector node '" + node.Id + "' has no children.");
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            List<int> order = GetOrCreateOrder(state, node.Children.Count);
            int startOrderIndex = 0;
            if (state.RunningChildIndex >= 0)
            {
                int runningOrderIndex = order.IndexOf(state.RunningChildIndex);
                if (runningOrderIndex >= 0)
                {
                    startOrderIndex = runningOrderIndex;
                }
            }

            for (int i = startOrderIndex; i < order.Count; i++)
            {
                int childIndex = order[i];
                BehaviorTreeStatus childStatus = context.TickChild(node.Children[childIndex]);
                if (childStatus == BehaviorTreeStatus.Success)
                {
                    ClearRandomOrder(state);
                    state.RunningChildIndex = -1;
                    return BehaviorTreeStatus.Success;
                }

                if (childStatus == BehaviorTreeStatus.Running)
                {
                    state.RunningChildIndex = childIndex;
                    return BehaviorTreeStatus.Running;
                }
            }

            ClearRandomOrder(state);
            state.RunningChildIndex = -1;
            return BehaviorTreeStatus.Failure;
        }

        public override void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            ClearRandomOrder(context.GetNodeState(node));
        }

        private static List<int> GetOrCreateOrder(BehaviorTreeNodeRuntimeState state, int childCount)
        {
            object value;
            List<int> order = state.Data.TryGetValue(RandomOrderKey, out value) ? value as List<int> : null;
            if (!IsValidOrder(order, childCount))
            {
                order = new List<int>();
                for (int i = 0; i < childCount; i++)
                {
                    order.Add(i);
                }

                Shuffle(order);
                state.Data[RandomOrderKey] = order;
            }

            return order;
        }

        private static bool IsValidOrder(List<int> order, int childCount)
        {
            if (order == null || order.Count != childCount)
            {
                return false;
            }

            bool[] seen = new bool[childCount];
            for (int i = 0; i < order.Count; i++)
            {
                int index = order[i];
                if (index < 0 || index >= childCount || seen[index])
                {
                    return false;
                }

                seen[index] = true;
            }

            return true;
        }

        private static void Shuffle(List<int> order)
        {
            for (int i = order.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                int value = order[i];
                order[i] = order[swapIndex];
                order[swapIndex] = value;
            }
        }

        private static void ClearRandomOrder(BehaviorTreeNodeRuntimeState state)
        {
            state.Data.Remove(RandomOrderKey);
        }
    }

    internal sealed class BehaviorTreePrioritySelectorExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return BehaviorTreeNodeTypeUtility.PrioritySelector; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            if (node.Children.Count == 0)
            {
                context.Runtime.MarkFailure("PrioritySelector node '" + node.Id + "' has no children.");
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            for (int i = 0; i < node.Children.Count; i++)
            {
                BehaviorTreeStatus childStatus = context.TickChild(node.Children[i]);
                if (childStatus == BehaviorTreeStatus.Success)
                {
                    state.RunningChildIndex = -1;
                    return BehaviorTreeStatus.Success;
                }

                if (childStatus == BehaviorTreeStatus.Running)
                {
                    state.RunningChildIndex = i;
                    return BehaviorTreeStatus.Running;
                }
            }

            state.RunningChildIndex = -1;
            return BehaviorTreeStatus.Failure;
        }
    }

    internal sealed class BehaviorTreeWeightedSelectorExecutor : BehaviorTreeNodeExecutor
    {
        private const string WeightedOrderKey = "weightedOrder";

        public override string TypeId
        {
            get { return BehaviorTreeNodeTypeUtility.WeightedSelector; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            if (node.Children.Count == 0)
            {
                context.Runtime.MarkFailure("WeightedSelector node '" + node.Id + "' has no children.");
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            List<int> order = GetOrCreateOrder(state, node);
            if (order.Count == 0)
            {
                ClearWeightedOrder(state);
                state.RunningChildIndex = -1;
                return BehaviorTreeStatus.Failure;
            }

            int startOrderIndex = 0;
            if (state.RunningChildIndex >= 0)
            {
                int runningOrderIndex = order.IndexOf(state.RunningChildIndex);
                if (runningOrderIndex >= 0)
                {
                    startOrderIndex = runningOrderIndex;
                }
            }

            for (int i = startOrderIndex; i < order.Count; i++)
            {
                int childIndex = order[i];
                BehaviorTreeStatus childStatus = context.TickChild(node.Children[childIndex]);
                if (childStatus == BehaviorTreeStatus.Success)
                {
                    ClearWeightedOrder(state);
                    state.RunningChildIndex = -1;
                    return BehaviorTreeStatus.Success;
                }

                if (childStatus == BehaviorTreeStatus.Running)
                {
                    state.RunningChildIndex = childIndex;
                    return BehaviorTreeStatus.Running;
                }
            }

            ClearWeightedOrder(state);
            state.RunningChildIndex = -1;
            return BehaviorTreeStatus.Failure;
        }

        public override void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            ClearWeightedOrder(context.GetNodeState(node));
        }

        private static List<int> GetOrCreateOrder(BehaviorTreeNodeRuntimeState state, RuntimeBehaviorTreeNode node)
        {
            object value;
            List<int> order = state.Data.TryGetValue(WeightedOrderKey, out value) ? value as List<int> : null;
            if (!IsValidOrder(order, node.Children.Count))
            {
                order = CreateWeightedOrder(node);
                state.Data[WeightedOrderKey] = order;
            }

            return order;
        }

        private static bool IsValidOrder(List<int> order, int childCount)
        {
            if (order == null || order.Count == 0 || order.Count > childCount)
            {
                return false;
            }

            bool[] seen = new bool[childCount];
            for (int i = 0; i < order.Count; i++)
            {
                int index = order[i];
                if (index < 0 || index >= childCount || seen[index])
                {
                    return false;
                }

                seen[index] = true;
            }

            return true;
        }

        private static List<int> CreateWeightedOrder(RuntimeBehaviorTreeNode node)
        {
            List<float> weights = CreateWeights(node);
            if (!HasSelectableWeight(weights))
            {
                weights.Clear();
                for (int i = 0; i < node.Children.Count; i++)
                {
                    weights.Add(1f);
                }
            }

            List<int> remaining = new List<int>();
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0f)
                {
                    remaining.Add(i);
                }
            }

            List<int> order = new List<int>();
            while (remaining.Count > 0)
            {
                int selectedRemainingIndex = PickWeightedIndex(remaining, weights);
                order.Add(remaining[selectedRemainingIndex]);
                remaining.RemoveAt(selectedRemainingIndex);
            }

            return order;
        }

        private static List<float> CreateWeights(RuntimeBehaviorTreeNode node)
        {
            List<float> weights = new List<float>();
            object rawWeightsValue = null;
            bool hasWeightsProperty = node.Properties != null &&
                                      node.Properties.TryGetValue("weights", out rawWeightsValue);
            IList rawWeights = hasWeightsProperty ? rawWeightsValue as IList : null;

            for (int i = 0; i < node.Children.Count; i++)
            {
                float weight = 1f;
                if (hasWeightsProperty)
                {
                    weight = rawWeights != null && i < rawWeights.Count ? ConvertToWeight(rawWeights[i]) : 0f;
                    if (rawWeights != null && i >= rawWeights.Count)
                    {
                        weight = 1f;
                    }
                }

                weights.Add(IsSelectableWeight(weight) ? weight : 0f);
            }

            return weights;
        }

        private static int PickWeightedIndex(List<int> remaining, List<float> weights)
        {
            float totalWeight = 0f;
            for (int i = 0; i < remaining.Count; i++)
            {
                totalWeight += weights[remaining[i]];
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulativeWeight = 0f;
            for (int i = 0; i < remaining.Count; i++)
            {
                cumulativeWeight += weights[remaining[i]];
                if (roll <= cumulativeWeight)
                {
                    return i;
                }
            }

            return remaining.Count - 1;
        }

        private static bool HasSelectableWeight(List<float> weights)
        {
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSelectableWeight(float weight)
        {
            return weight > 0f && !float.IsNaN(weight) && !float.IsInfinity(weight);
        }

        private static float ConvertToWeight(object value)
        {
            if (value == null)
            {
                return 0f;
            }

            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch (InvalidCastException)
            {
                return 0f;
            }
            catch (FormatException)
            {
                return 0f;
            }
            catch (OverflowException)
            {
                return 0f;
            }
        }

        private static void ClearWeightedOrder(BehaviorTreeNodeRuntimeState state)
        {
            state.Data.Remove(WeightedOrderKey);
        }
    }

    internal sealed class BehaviorTreeWaitExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.Wait"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            object endTimeValue;
            if (!state.Data.TryGetValue("endTime", out endTimeValue))
            {
                float duration = BehaviorTreePropertyUtility.ResolveFloat(context, node, "duration", "duration",
                    BehaviorTreePropertyUtility.GetFloat(node.Properties, "seconds", 0f));
                state.Data["endTime"] = context.TimeSeconds + Mathf.Max(0f, duration);
            }

            float endTime = Convert.ToSingle(state.Data["endTime"], CultureInfo.InvariantCulture);
            if (context.TimeSeconds + 0.0001f >= endTime)
            {
                state.Data.Clear();
                return BehaviorTreeStatus.Success;
            }

            return BehaviorTreeStatus.Running;
        }

        public override void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            context.GetNodeState(node).Data.Clear();
        }
    }

    internal sealed class BehaviorTreeSetBlackboardExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.SetBlackboard"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            string key = BehaviorTreePropertyUtility.GetBoundBlackboardKey(node, "key", "key", null);
            if (string.IsNullOrEmpty(key))
            {
                context.Runtime.MarkFailure("SetBlackboard requires a key.");
                return BehaviorTreeStatus.Failure;
            }

            object value = null;
            if (!BehaviorTreePropertyUtility.TryResolveValue(context, node, "value", "value", out value))
            {
                string valueKey = BehaviorTreePropertyUtility.GetString(node.Properties, "valueKey", null);
                if (!string.IsNullOrEmpty(valueKey))
                {
                    context.Blackboard.TryGetValue(valueKey, out value);
                }
            }

            context.Blackboard.SetValue(key, value);
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeClearBlackboardExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.ClearBlackboard"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            string key = BehaviorTreePropertyUtility.GetBoundBlackboardKey(node, "key", "key", null);
            if (string.IsNullOrEmpty(key))
            {
                context.Runtime.MarkFailure("ClearBlackboard requires a key.");
                return BehaviorTreeStatus.Failure;
            }

            context.Blackboard.ClearValue(key);
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeSetRunnerBlackboardExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.SetRunnerBlackboard"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            BehaviorTreeBlackboard targetBlackboard;
            if (!BehaviorTreeRunnerBlackboardUtility.TryResolveRunnerBlackboard(context, node, "target", TypeId, out targetBlackboard))
            {
                return BehaviorTreeStatus.Failure;
            }

            string targetKey = BehaviorTreePropertyUtility.GetString(node.Properties, "targetKey", null);
            if (string.IsNullOrEmpty(targetKey))
            {
                context.Runtime.MarkFailure(TypeId + " requires targetKey.");
                return BehaviorTreeStatus.Failure;
            }

            object value;
            if (!BehaviorTreePropertyUtility.TryGetInputValue(context, node, "value", out value))
            {
                string sourceKey = BehaviorTreePropertyUtility.GetString(node.Properties, "sourceKey", null);
                if (string.IsNullOrEmpty(sourceKey))
                {
                    context.Runtime.MarkFailure(TypeId + " requires value input or sourceKey.");
                    return BehaviorTreeStatus.Failure;
                }

                if (!context.Blackboard.TryGetValue(sourceKey, out value))
                {
                    context.Runtime.MarkFailure(TypeId + " sourceKey '" + sourceKey + "' is not set.");
                    return BehaviorTreeStatus.Failure;
                }
            }

            targetBlackboard.SetValue(targetKey, value);
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeGetRunnerBlackboardExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.GetRunnerBlackboard"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            BehaviorTreeBlackboard sourceBlackboard;
            if (!BehaviorTreeRunnerBlackboardUtility.TryResolveRunnerBlackboard(context, node, "target", TypeId, out sourceBlackboard))
            {
                return BehaviorTreeStatus.Failure;
            }

            string sourceKey = BehaviorTreePropertyUtility.GetString(node.Properties, "sourceKey", null);
            string targetKey = BehaviorTreePropertyUtility.GetString(node.Properties, "targetKey", null);
            if (string.IsNullOrEmpty(sourceKey) || string.IsNullOrEmpty(targetKey))
            {
                context.Runtime.MarkFailure(TypeId + " requires sourceKey and targetKey.");
                return BehaviorTreeStatus.Failure;
            }

            if (!context.Blackboard.ContainsKey(targetKey))
            {
                context.Runtime.MarkFailure(TypeId + " targetKey '" + targetKey + "' is not declared.");
                return BehaviorTreeStatus.Failure;
            }

            object value;
            if (!sourceBlackboard.TryGetValue(sourceKey, out value))
            {
                context.Runtime.MarkFailure(TypeId + " sourceKey '" + sourceKey + "' is not set on target runner.");
                return BehaviorTreeStatus.Failure;
            }

            context.Blackboard.SetValue(targetKey, value);
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeClearRunnerBlackboardTaskExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.ClearRunnerBlackboard"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            BehaviorTreeBlackboard targetBlackboard;
            if (!BehaviorTreeRunnerBlackboardUtility.TryResolveRunnerBlackboard(context, node, "target", TypeId, out targetBlackboard))
            {
                return BehaviorTreeStatus.Failure;
            }

            string targetKey = BehaviorTreePropertyUtility.GetString(node.Properties, "targetKey", null);
            if (string.IsNullOrEmpty(targetKey))
            {
                context.Runtime.MarkFailure(TypeId + " requires targetKey.");
                return BehaviorTreeStatus.Failure;
            }

            targetBlackboard.ClearValue(targetKey);
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeCopyRunnerBlackboardExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.CopyRunnerBlackboard"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            BehaviorTreeBlackboard targetBlackboard;
            if (!BehaviorTreeRunnerBlackboardUtility.TryResolveRunnerBlackboard(context, node, "target", TypeId, out targetBlackboard))
            {
                return BehaviorTreeStatus.Failure;
            }

            string sourceKey = BehaviorTreePropertyUtility.GetString(node.Properties, "sourceKey", null);
            string targetKey = BehaviorTreePropertyUtility.GetString(node.Properties, "targetKey", null);
            if (string.IsNullOrEmpty(sourceKey) || string.IsNullOrEmpty(targetKey))
            {
                context.Runtime.MarkFailure(TypeId + " requires sourceKey and targetKey.");
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeBlackboard sourceBlackboard;
            string sourceTargetBinding;
            if (BehaviorTreePropertyUtility.TryGetInputBinding(node, "sourceTarget", out sourceTargetBinding))
            {
                if (!BehaviorTreeRunnerBlackboardUtility.TryResolveRunnerBlackboard(context, node, "sourceTarget", TypeId, out sourceBlackboard))
                {
                    return BehaviorTreeStatus.Failure;
                }
            }
            else
            {
                sourceBlackboard = context.Blackboard;
            }

            object value;
            if (sourceBlackboard == null || !sourceBlackboard.TryGetValue(sourceKey, out value))
            {
                context.Runtime.MarkFailure(TypeId + " sourceKey '" + sourceKey + "' is not set.");
                return BehaviorTreeStatus.Failure;
            }

            targetBlackboard.SetValue(targetKey, value);
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeRunSubtreeExecutor : BehaviorTreeNodeExecutor
    {
        private const string RuntimeStateKey = "subtreeRuntime";
        private const string BlackboardModeStateKey = "subtreeBlackboardMode";
        private const string SharedMode = "Shared";
        private const string IsolatedMode = "Isolated";

        public override string TypeId
        {
            get { return "BT.RunSubtree"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            RuntimeBehaviorTreeComponent subtreeComponent = ResolveSubtreeComponent(context, node);
            if (subtreeComponent == null)
            {
                return BehaviorTreeStatus.Failure;
            }

            string blackboardMode = ResolveBlackboardMode(context, node);
            if (string.IsNullOrEmpty(blackboardMode))
            {
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            BehaviorTreeRuntime subtreeRuntime = GetOrCreateRuntime(context, node, state, blackboardMode, subtreeComponent);
            if (subtreeRuntime == null)
            {
                return BehaviorTreeStatus.Failure;
            }

            if (blackboardMode == IsolatedMode &&
                !CopyMappings(context, node, "inputMappings", context.Blackboard, subtreeRuntime.Blackboard, "input"))
            {
                ClearSubtreeState(state, true);
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeStatus status = subtreeRuntime.Tick(context.DeltaTime);

            if (blackboardMode == IsolatedMode &&
                !CopyMappings(context, node, "outputMappings", subtreeRuntime.Blackboard, context.Blackboard, "output"))
            {
                ClearSubtreeState(state, true);
                return BehaviorTreeStatus.Failure;
            }

            if (status == BehaviorTreeStatus.Failure && !string.IsNullOrEmpty(subtreeRuntime.LastFailureReason))
            {
                context.Runtime.MarkFailure(TypeId + " subtree '" + node.Id + "' failed: " + subtreeRuntime.LastFailureReason);
            }

            if (status != BehaviorTreeStatus.Running)
            {
                state.Data.Clear();
            }

            return status;
        }

        public override void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            ClearSubtreeState(context.GetNodeState(node), true);
        }

        private static RuntimeBehaviorTreeComponent ResolveSubtreeComponent(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node)
        {
            RuntimeBehaviorTreeComponent component = context.Tree == null ? null : context.Tree.GetComponent(node.Id);
            if (component != null && component.CompiledBehaviorTree != null)
            {
                return component;
            }

            string path = component != null && !string.IsNullOrEmpty(component.BehaviorTreePath)
                ? component.BehaviorTreePath
                : BehaviorTreePropertyUtility.GetString(node.Properties, "behaviorTree", null);
            context.Runtime.MarkFailure("BT.RunSubtree requires a compiled subtree asset" + (string.IsNullOrEmpty(path) ? "." : " for '" + path + "'."));
            return null;
        }

        private static BehaviorTreeRuntime GetOrCreateRuntime(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            BehaviorTreeNodeRuntimeState state,
            string blackboardMode,
            RuntimeBehaviorTreeComponent subtreeComponent)
        {
            object runtimeValue;
            BehaviorTreeRuntime runtime = null;
            if (state.Data.TryGetValue(RuntimeStateKey, out runtimeValue))
            {
                runtime = runtimeValue as BehaviorTreeRuntime;
            }

            if (runtime != null)
            {
                object previousModeValue;
                string previousMode = state.Data.TryGetValue(BlackboardModeStateKey, out previousModeValue)
                    ? previousModeValue as string
                    : null;
                if (string.Equals(previousMode, blackboardMode, StringComparison.Ordinal))
                {
                    return runtime;
                }

                ClearSubtreeState(state, true);
            }

            RuntimeBehaviorTree subtree = subtreeComponent.CompiledBehaviorTree.CreateRuntimeTree(context.Tree == null ? null : context.Tree.Registry);
            BehaviorTreeBlackboard blackboard;
            if (blackboardMode == SharedMode)
            {
                if (context.Blackboard == null || !context.Blackboard.MergeSchema(subtree.BlackboardSchema))
                {
                    context.Runtime.MarkFailure("BT.RunSubtree shared Blackboard schema conflicts with subtree '" + node.Id + "'.");
                    return null;
                }

                blackboard = context.Blackboard;
            }
            else
            {
                blackboard = new BehaviorTreeBlackboard(subtree.BlackboardSchema);
            }

            runtime = new BehaviorTreeRuntime(subtree, context.Owner, context.OwnerComponent, blackboard, context.Logger);
            state.Data[RuntimeStateKey] = runtime;
            state.Data[BlackboardModeStateKey] = blackboardMode;
            return runtime;
        }

        private static string ResolveBlackboardMode(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            string mode = BehaviorTreePropertyUtility.GetString(node.Properties, "blackboardMode", SharedMode);
            if (string.IsNullOrEmpty(mode) || string.Equals(mode, SharedMode, StringComparison.OrdinalIgnoreCase))
            {
                return SharedMode;
            }

            if (string.Equals(mode, IsolatedMode, StringComparison.OrdinalIgnoreCase))
            {
                return IsolatedMode;
            }

            context.Runtime.MarkFailure("BT.RunSubtree blackboardMode must be Shared or Isolated.");
            return null;
        }

        private static bool CopyMappings(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string propertyName,
            BehaviorTreeBlackboard source,
            BehaviorTreeBlackboard target,
            string label)
        {
            List<SubtreeBlackboardMapping> mappings = ReadMappings(node, propertyName);
            for (int i = 0; i < mappings.Count; i++)
            {
                SubtreeBlackboardMapping mapping = mappings[i];
                if (string.IsNullOrEmpty(mapping.SourceKey) || string.IsNullOrEmpty(mapping.TargetKey))
                {
                    context.Runtime.MarkFailure("BT.RunSubtree " + label + " mapping requires sourceKey and targetKey.");
                    return false;
                }

                if (source == null || !source.ContainsKey(mapping.SourceKey))
                {
                    context.Runtime.MarkFailure("BT.RunSubtree " + label + " mapping sourceKey '" + mapping.SourceKey + "' is not declared.");
                    return false;
                }

                if (target == null || !target.ContainsKey(mapping.TargetKey))
                {
                    context.Runtime.MarkFailure("BT.RunSubtree " + label + " mapping targetKey '" + mapping.TargetKey + "' is not declared.");
                    return false;
                }

                target.SetValue(mapping.TargetKey, source.GetValue(mapping.SourceKey));
            }

            return true;
        }

        private static List<SubtreeBlackboardMapping> ReadMappings(RuntimeBehaviorTreeNode node, string propertyName)
        {
            List<SubtreeBlackboardMapping> result = new List<SubtreeBlackboardMapping>();
            object value;
            if (node == null || !node.Properties.TryGetValue(propertyName, out value) || value == null || value is string)
            {
                return result;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return result;
            }

            foreach (object item in enumerable)
            {
                Dictionary<string, object> dictionary = item as Dictionary<string, object>;
                if (dictionary == null)
                {
                    continue;
                }

                result.Add(new SubtreeBlackboardMapping
                {
                    SourceKey = BehaviorTreePropertyUtility.GetString(dictionary, "sourceKey", null),
                    TargetKey = BehaviorTreePropertyUtility.GetString(dictionary, "targetKey", null)
                });
            }

            return result;
        }

        private static void ClearSubtreeState(BehaviorTreeNodeRuntimeState state, bool stopRuntime)
        {
            if (state == null)
            {
                return;
            }

            if (stopRuntime)
            {
                object runtimeValue;
                BehaviorTreeRuntime runtime = state.Data.TryGetValue(RuntimeStateKey, out runtimeValue)
                    ? runtimeValue as BehaviorTreeRuntime
                    : null;
                if (runtime != null)
                {
                    runtime.Stop();
                }
            }

            state.Data.Clear();
        }

        private sealed class SubtreeBlackboardMapping
        {
            public string SourceKey;
            public string TargetKey;
        }
    }

    internal sealed class BehaviorTreeSetNavigationDestinationExecutor : BehaviorTreeNodeExecutor
    {
        private readonly BehaviorTreeSetNavigationDestinationImplementation _implementation =
            new BehaviorTreeSetNavigationDestinationImplementation();

        public override string TypeId
        {
            get { return "BT.SetNavigationDestination"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            return _implementation.Tick(context, node);
        }
    }

    internal sealed class BehaviorTreeCalculateNavigationPathExecutor : BehaviorTreeNodeExecutor
    {
        private readonly BehaviorTreeCalculateNavigationPathImplementation _implementation =
            new BehaviorTreeCalculateNavigationPathImplementation();

        public override string TypeId
        {
            get { return "BT.CalculateNavigationPath"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            return _implementation.Tick(context, node);
        }
    }

    internal sealed class BehaviorTreeSetNavigationPathExecutor : BehaviorTreeNodeExecutor
    {
        private readonly BehaviorTreeSetNavigationPathImplementation _implementation =
            new BehaviorTreeSetNavigationPathImplementation();

        public override string TypeId
        {
            get { return "BT.SetNavigationPath"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            return _implementation.Tick(context, node);
        }
    }

    internal sealed class BehaviorTreeWaitForNavigationExecutor : BehaviorTreeNodeExecutor
    {
        private readonly BehaviorTreeWaitForNavigationImplementation _implementation =
            new BehaviorTreeWaitForNavigationImplementation();

        public override string TypeId
        {
            get { return "BT.WaitForNavigation"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            return _implementation.Tick(context, node);
        }
    }

    internal sealed class BehaviorTreePauseNavigationExecutor : BehaviorTreeNodeExecutor
    {
        private readonly BehaviorTreePauseNavigationImplementation _implementation =
            new BehaviorTreePauseNavigationImplementation();

        public override string TypeId
        {
            get { return "BT.PauseNavigation"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            return _implementation.Tick(context, node);
        }
    }

    internal sealed class BehaviorTreeResumeNavigationExecutor : BehaviorTreeNodeExecutor
    {
        private readonly BehaviorTreeResumeNavigationImplementation _implementation =
            new BehaviorTreeResumeNavigationImplementation();

        public override string TypeId
        {
            get { return "BT.ResumeNavigation"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            return _implementation.Tick(context, node);
        }
    }

    internal sealed class BehaviorTreeSampleNavMeshPositionExecutor : BehaviorTreeNodeExecutor
    {
        private readonly BehaviorTreeSampleNavMeshPositionImplementation _implementation =
            new BehaviorTreeSampleNavMeshPositionImplementation();

        public override string TypeId
        {
            get { return "BT.SampleNavMeshPosition"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            return _implementation.Tick(context, node);
        }
    }

    internal sealed class BehaviorTreeWarpNavigationExecutor : BehaviorTreeNodeExecutor
    {
        private readonly BehaviorTreeWarpNavigationImplementation _implementation =
            new BehaviorTreeWarpNavigationImplementation();

        public override string TypeId
        {
            get { return "BT.WarpNavigation"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            return _implementation.Tick(context, node);
        }
    }

    internal sealed class BehaviorTreeTraverseOffMeshLinkExecutor : BehaviorTreeNodeExecutor
    {
        private readonly BehaviorTreeTraverseOffMeshLinkImplementation _implementation =
            new BehaviorTreeTraverseOffMeshLinkImplementation();

        public override string TypeId
        {
            get { return "BT.TraverseOffMeshLink"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            return _implementation.Tick(context, node);
        }

        public override void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            _implementation.Abort(context, node);
        }
    }

    internal sealed class BehaviorTreeMoveToExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.MoveTo"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            Vector3 destination;
            if (!BehaviorTreePropertyUtility.TryResolveVector3(context, node, "target", "targetKey", "targetPosition", out destination))
            {
                context.Runtime.MarkFailure("MoveTo could not resolve a target.");
                return BehaviorTreeStatus.Failure;
            }

            float acceptableRadius = BehaviorTreePropertyUtility.ResolveFloat(context, node, "acceptableRadius", "acceptableRadius",
                BehaviorTreePropertyUtility.GetFloat(node.Properties, "stoppingDistance", 0.25f));

            NavMeshAgent agent = context.Owner == null ? null : context.Owner.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                return TickNavMeshAgent(context, node, agent, destination, acceptableRadius);
            }

            if (!BehaviorTreePropertyUtility.ResolveBool(context, node, "allowTransformFallback", "allowTransformFallback", true) || context.Owner == null)
            {
                context.Runtime.MarkFailure("MoveTo requires a NavMeshAgent or transform fallback.");
                return BehaviorTreeStatus.Failure;
            }

            Transform transform = context.Owner.transform;
            float speed = BehaviorTreePropertyUtility.ResolveFloat(context, node, "speed", "speed", 3f);
            transform.position = Vector3.MoveTowards(transform.position, destination, Mathf.Max(0f, speed) * Mathf.Max(0f, context.DeltaTime));
            return Vector3.Distance(transform.position, destination) <= acceptableRadius
                ? BehaviorTreeStatus.Success
                : BehaviorTreeStatus.Running;
        }

        public override void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            if (!BehaviorTreePropertyUtility.ResolveBool(context, node, "stopOnAbort", "stopOnAbort", true) || context.Owner == null)
            {
                return;
            }

            NavMeshAgent agent = context.Owner.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
        }

        private static BehaviorTreeStatus TickNavMeshAgent(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            NavMeshAgent agent,
            Vector3 destination,
            float acceptableRadius)
        {
            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            Vector3 previousDestination = Vector3.zero;
            bool hasDestination = state.Data.ContainsKey("destination") &&
                                  BehaviorTreeValueUtility.TryGetVector3(state.Data["destination"], out previousDestination);

            if (!hasDestination || Vector3.Distance(previousDestination, destination) > 0.05f)
            {
                agent.isStopped = false;
                agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, acceptableRadius);
                if (!agent.SetDestination(destination))
                {
                    context.Runtime.MarkFailure("NavMeshAgent rejected MoveTo destination.");
                    return BehaviorTreeStatus.Failure;
                }

                state.Data["destination"] = destination;
            }

            if (agent.pathPending)
            {
                return BehaviorTreeStatus.Running;
            }

            NavMeshPathStatus pathStatus = agent.pathStatus;
            if (pathStatus != NavMeshPathStatus.PathComplete)
            {
                context.Runtime.MarkFailure("MoveTo NavMesh path is " + pathStatus + ".");
                return BehaviorTreeStatus.Failure;
            }

            float remainingDistance = agent.remainingDistance;
            if (float.IsInfinity(remainingDistance) || float.IsNaN(remainingDistance))
            {
                return BehaviorTreeStatus.Running;
            }

            return remainingDistance <= Mathf.Max(acceptableRadius, agent.stoppingDistance)
                ? BehaviorTreeStatus.Success
                : BehaviorTreeStatus.Running;
        }
    }

    internal sealed class BehaviorTreeStopNavigationExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.StopNavigation"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            if (context.Owner == null)
            {
                context.Runtime.MarkFailure("StopNavigation requires an owner GameObject.");
                return BehaviorTreeStatus.Failure;
            }

            NavMeshAgent agent = context.Owner.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                context.Runtime.MarkFailure("StopNavigation requires an enabled NavMeshAgent on the NavMesh.");
                return BehaviorTreeStatus.Failure;
            }

            bool stopAgent = BehaviorTreePropertyUtility.ResolveBool(context, node, "stopAgent", "stopAgent", true);
            agent.ResetPath();
            agent.isStopped = stopAgent;
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeRotateToExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.RotateTo"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            if (context.Owner == null)
            {
                return BehaviorTreeStatus.Failure;
            }

            Vector3 destination;
            if (!BehaviorTreePropertyUtility.TryResolveVector3(context, node, "target", "targetKey", "targetPosition", out destination))
            {
                context.Runtime.MarkFailure("RotateTo could not resolve a target.");
                return BehaviorTreeStatus.Failure;
            }

            Transform transform = context.Owner.transform;
            Vector3 direction = destination - transform.position;
            direction.y = BehaviorTreePropertyUtility.ResolveBool(context, node, "ignoreY", "ignoreY", true) ? 0f : direction.y;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return BehaviorTreeStatus.Success;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            float angle = Quaternion.Angle(transform.rotation, targetRotation);
            float tolerance = BehaviorTreePropertyUtility.ResolveFloat(context, node, "angleTolerance", "angleTolerance", 2f);
            if (angle <= tolerance)
            {
                return BehaviorTreeStatus.Success;
            }

            float speed = BehaviorTreePropertyUtility.ResolveFloat(context, node, "rotationSpeed", "rotationSpeed", 360f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Mathf.Max(0f, speed) * Mathf.Max(0f, context.DeltaTime));
            return BehaviorTreeStatus.Running;
        }
    }

    internal sealed class BehaviorTreeTriggerBlueprintEventExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.TriggerBlueprintEvent"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            string eventName = BehaviorTreePropertyUtility.ResolveString(context, node, "eventName", "eventName", null);
            if (string.IsNullOrEmpty(eventName))
            {
                context.Runtime.MarkFailure("TriggerBlueprintEvent requires eventName.");
                return BehaviorTreeStatus.Failure;
            }

            IBlueprintInstance target = BehaviorTreeBlueprintTargetUtility.ResolveTarget(context, node, true);
            if (target == null)
            {
                bool successOnMissing = BehaviorTreePropertyUtility.ResolveBool(context, node, "successOnMissing", "successOnMissing", false);
                return successOnMissing ? BehaviorTreeStatus.Success : BehaviorTreeStatus.Failure;
            }

            target.TriggerEvent(eventName);
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeRunBlueprintTaskExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.RunBlueprintTask"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            if (!state.Data.ContainsKey("started"))
            {
                string eventName = BehaviorTreePropertyUtility.ResolveString(context, node, "startEventName", "startEventName",
                    BehaviorTreePropertyUtility.ResolveString(context, node, "eventName", "eventName", null));
                if (!string.IsNullOrEmpty(eventName))
                {
                    IBlueprintInstance target = BehaviorTreeBlueprintTargetUtility.ResolveTarget(context, node, true);
                    if (target == null)
                    {
                        return BehaviorTreeStatus.Failure;
                    }

                    target.TriggerEvent(eventName);
                }

                float timeout = BehaviorTreePropertyUtility.ResolveFloat(context, node, "timeout", "timeout", 0f);
                if (timeout > 0f)
                {
                    state.Data["timeoutAt"] = context.TimeSeconds + timeout;
                }

                state.Data["started"] = true;
            }

            object failureValue;
            string failureKey = BehaviorTreePropertyUtility.GetString(node.Properties, "failureKey", null);
            if ((BehaviorTreePropertyUtility.TryResolveValue(context, node, "failure", "failure", out failureValue) && IsTruthy(failureValue)) ||
                (!string.IsNullOrEmpty(failureKey) && IsTruthy(context.Blackboard.GetValue(failureKey))))
            {
                state.Data.Clear();
                return BehaviorTreeStatus.Failure;
            }

            object completeValue;
            string completeKey = BehaviorTreePropertyUtility.GetString(node.Properties, "completeKey", null);
            if (!BehaviorTreePropertyUtility.TryResolveValue(context, node, "complete", "complete", out completeValue) &&
                string.IsNullOrEmpty(completeKey))
            {
                state.Data.Clear();
                return BehaviorTreeStatus.Success;
            }

            if (IsTruthy(completeValue) ||
                (!string.IsNullOrEmpty(completeKey) && IsTruthy(context.Blackboard.GetValue(completeKey))))
            {
                state.Data.Clear();
                return BehaviorTreeStatus.Success;
            }

            object timeoutValue;
            if (state.Data.TryGetValue("timeoutAt", out timeoutValue) &&
                context.TimeSeconds >= Convert.ToSingle(timeoutValue, CultureInfo.InvariantCulture))
            {
                state.Data.Clear();
                return BehaviorTreePropertyUtility.ResolveString(context, node, "timeoutStatus", "timeoutStatus", "Failure") == "Success"
                    ? BehaviorTreeStatus.Success
                    : BehaviorTreeStatus.Failure;
            }

            return BehaviorTreeStatus.Running;
        }

        public override void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            string abortEventName = BehaviorTreePropertyUtility.ResolveString(context, node, "abortEventName", "abortEventName", null);
            if (!string.IsNullOrEmpty(abortEventName))
            {
                IBlueprintInstance target = BehaviorTreeBlueprintTargetUtility.ResolveTarget(context, node, false);
                if (target != null)
                {
                    target.TriggerEvent(abortEventName);
                }
            }

            context.GetNodeState(node).Data.Clear();
        }

        private static bool IsTruthy(object value)
        {
            if (value == null)
            {
                return false;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return !string.IsNullOrEmpty(text) &&
                   !string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) &&
                   text != "0";
        }
    }

    internal sealed class BehaviorTreeLogExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.Log"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            string message = BehaviorTreePropertyUtility.ResolveString(context, node, "message", "message", node.Id);
            context.Logger.Log("[BehaviorTree] " + message);
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeNavigationConditionDecorator : BehaviorTreeDecoratorExecutor
    {
        private readonly BehaviorTreeNavigationConditionImplementation _implementation =
            new BehaviorTreeNavigationConditionImplementation();

        public override string TypeId
        {
            get { return "BT.NavigationCondition"; }
        }

        public override bool Evaluate(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            RuntimeBehaviorTreeDecorator decorator)
        {
            return _implementation.Evaluate(context, node, decorator);
        }
    }

    internal sealed class BehaviorTreeBlackboardConditionDecorator : BehaviorTreeDecoratorExecutor
    {
        public override string TypeId
        {
            get { return "BT.BlackboardCondition"; }
        }

        public override bool Evaluate(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeDecorator decorator)
        {
            BehaviorTreeComparisonOperator op = BehaviorTreeComparisonUtility.GetOperator(
                decorator.Properties,
                "operator",
                BehaviorTreeComparisonOperator.IsSet);
            string valueKey;
            object value = null;
            bool hasValueBinding = BehaviorTreePropertyUtility.TryGetInputBinding(decorator, "value", out valueKey);
            if (hasValueBinding)
            {
                if (op == BehaviorTreeComparisonOperator.IsSet)
                {
                    return context.Blackboard.IsSet(valueKey);
                }

                if (op == BehaviorTreeComparisonOperator.IsNotSet)
                {
                    return !context.Blackboard.IsSet(valueKey);
                }

                context.Blackboard.TryGetValue(valueKey, out value);
            }
            else
            {
                string key = BehaviorTreePropertyUtility.GetString(decorator.Properties, "key", null);
                value = context.Blackboard.GetValue(key);
            }

            object expected;
            if (!BehaviorTreePropertyUtility.TryGetInputValue(context, decorator, "expected", out expected) &&
                !decorator.Properties.TryGetValue("expected", out expected))
            {
                decorator.Properties.TryGetValue("value", out expected);
            }

            return BehaviorTreeComparisonUtility.Evaluate(op, value, expected);
        }
    }

    internal sealed class BehaviorTreeCompareFloatDecorator : BehaviorTreeDecoratorExecutor
    {
        public override string TypeId
        {
            get { return "BT.CompareFloat"; }
        }

        public override bool Evaluate(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeDecorator decorator)
        {
            float left = BehaviorTreePropertyUtility.ResolveFloat(context, decorator, "left", "leftKey", "left", 0f);
            float right = BehaviorTreePropertyUtility.ResolveFloat(context, decorator, "right", "rightKey", "value", 0f);
            BehaviorTreeComparisonOperator op = BehaviorTreeComparisonUtility.GetOperator(
                decorator.Properties,
                "operator",
                BehaviorTreeComparisonOperator.LessOrEqual);
            return BehaviorTreeComparisonUtility.CompareFloats(left, right, op);
        }
    }

    internal sealed class BehaviorTreeCompareBoolDecorator : BehaviorTreeDecoratorExecutor
    {
        public override string TypeId
        {
            get { return "BT.CompareBool"; }
        }

        public override bool Evaluate(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeDecorator decorator)
        {
            bool actual = BehaviorTreePropertyUtility.ResolveBool(context, decorator, "value", "key", null, false);
            bool expected = BehaviorTreePropertyUtility.ResolveBool(context, decorator, "expected", null, "value", true);
            BehaviorTreeComparisonOperator op = BehaviorTreeComparisonUtility.GetOperator(
                decorator.Properties,
                "operator",
                BehaviorTreeComparisonOperator.Equals);
            return op == BehaviorTreeComparisonOperator.NotEquals ? actual != expected : actual == expected;
        }
    }

    internal sealed class BehaviorTreeObjectIsSetDecorator : BehaviorTreeDecoratorExecutor
    {
        public override string TypeId
        {
            get { return "BT.ObjectIsSet"; }
        }

        public override bool Evaluate(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeDecorator decorator)
        {
            string key;
            if (!BehaviorTreePropertyUtility.TryGetInputBinding(decorator, "value", out key))
            {
                key = BehaviorTreePropertyUtility.GetString(decorator.Properties, "key", null);
            }

            return context.Blackboard.IsSet(key);
        }
    }

    internal sealed class BehaviorTreeDistanceLessThanDecorator : BehaviorTreeDecoratorExecutor
    {
        public override string TypeId
        {
            get { return "BT.DistanceLessThan"; }
        }

        public override bool Evaluate(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeDecorator decorator)
        {
            float distance;
            if (!BehaviorTreePropertyUtility.TryResolveFloat(context, decorator, "distance", "distanceKey", "distance", float.PositiveInfinity, out distance))
            {
                Vector3 source;
                Vector3 target;
                if (!BehaviorTreePropertyUtility.TryResolveVector3(context, decorator, "source", "sourceKey", "sourcePosition", out source))
                {
                    source = context.Owner == null ? Vector3.zero : context.Owner.transform.position;
                }

                if (!BehaviorTreePropertyUtility.TryResolveVector3(context, decorator, "target", "targetKey", "targetPosition", out target))
                {
                    return false;
                }

                distance = Vector3.Distance(source, target);
            }

            float maxDistance = BehaviorTreePropertyUtility.ResolveFloat(context, decorator, "maxDistance", null, "maxDistance", 0f);
            return distance <= maxDistance;
        }
    }

    internal sealed class BehaviorTreeCooldownDecorator : BehaviorTreeDecoratorExecutor
    {
        public override string TypeId
        {
            get { return "BT.Cooldown"; }
        }

        public override bool Evaluate(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeDecorator decorator)
        {
            BehaviorTreeNodeRuntimeState nodeState = context.GetNodeState(node);
            if (nodeState.HasStatus && nodeState.LastStatus == BehaviorTreeStatus.Running)
            {
                return true;
            }

            BehaviorTreeDecoratorRuntimeState state = context.Runtime.GetDecoratorState(decorator.Id);
            float readyAt = 0f;
            object readyAtValue;
            if (state.Data.TryGetValue("readyAt", out readyAtValue))
            {
                readyAt = Convert.ToSingle(readyAtValue, CultureInfo.InvariantCulture);
            }

            if (context.TimeSeconds + 0.0001f < readyAt)
            {
                return false;
            }

            float duration = BehaviorTreePropertyUtility.GetFloat(decorator.Properties, "duration",
                BehaviorTreePropertyUtility.GetFloat(decorator.Properties, "cooldown", 0f));
            state.Data["readyAt"] = context.TimeSeconds + Mathf.Max(0f, duration);
            return true;
        }
    }

    internal sealed class BehaviorTreeUpdateNavigationStateService : BehaviorTreeServiceExecutor
    {
        private readonly BehaviorTreeUpdateNavigationStateImplementation _implementation =
            new BehaviorTreeUpdateNavigationStateImplementation();

        public override string TypeId
        {
            get { return "BT.UpdateNavigationState"; }
        }

        public override void Tick(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            RuntimeBehaviorTreeService service)
        {
            _implementation.Tick(context, node, service);
        }
    }

    internal sealed class BehaviorTreeUpdateDistanceService : BehaviorTreeServiceExecutor
    {
        public override string TypeId
        {
            get { return "BT.UpdateDistance"; }
        }

        public override void Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service)
        {
            Vector3 source;
            Vector3 target;
            if (!BehaviorTreePropertyUtility.TryResolveVector3(context, service.Properties, "sourceKey", "sourcePosition", out source))
            {
                source = context.Owner == null ? Vector3.zero : context.Owner.transform.position;
            }

            string distanceKey = BehaviorTreePropertyUtility.GetString(service.Properties, "distanceKey", "DistanceToTarget");
            if (!BehaviorTreePropertyUtility.TryResolveVector3(context, service.Properties, "targetKey", "targetPosition", out target))
            {
                context.Blackboard.SetValue(distanceKey, float.PositiveInfinity);
                return;
            }

            context.Blackboard.SetValue(distanceKey, Vector3.Distance(source, target));
        }
    }

    internal sealed class BehaviorTreePerceptionSphereService : BehaviorTreeServiceExecutor
    {
        public override string TypeId
        {
            get { return "BT.PerceptionSphere"; }
        }

        public override void Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service)
        {
            if (context.Owner == null)
            {
                return;
            }

            float radius = BehaviorTreePropertyUtility.GetFloat(service.Properties, "radius", 10f);
            int layerMask = BehaviorTreePropertyUtility.GetInt(service.Properties, "layerMask", -1);
            string targetKey = BehaviorTreePropertyUtility.GetString(service.Properties, "targetKey", "Target");
            bool clearOnMiss = BehaviorTreePropertyUtility.GetBool(service.Properties, "clearOnMiss", false);
            Collider[] colliders = Physics.OverlapSphere(context.Owner.transform.position, Mathf.Max(0f, radius), layerMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.gameObject == context.Owner)
                {
                    continue;
                }

                context.Blackboard.SetValue(targetKey, collider.gameObject);
                return;
            }

            if (clearOnMiss)
            {
                context.Blackboard.ClearValue(targetKey);
            }
        }
    }

    internal sealed class BehaviorTreePerceptionRaycastService : BehaviorTreeServiceExecutor
    {
        public override string TypeId
        {
            get { return "BT.PerceptionRaycast"; }
        }

        public override void Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service)
        {
            if (context.Owner == null)
            {
                return;
            }

            float maxDistance = BehaviorTreePropertyUtility.GetFloat(service.Properties, "maxDistance", 30f);
            int layerMask = BehaviorTreePropertyUtility.GetInt(service.Properties, "layerMask", -1);
            string targetKey = BehaviorTreePropertyUtility.GetString(service.Properties, "targetKey", "Target");
            string hitPointKey = BehaviorTreePropertyUtility.GetString(service.Properties, "hitPointKey", null);
            bool clearOnMiss = BehaviorTreePropertyUtility.GetBool(service.Properties, "clearOnMiss", false);
            RaycastHit hit;
            if (Physics.Raycast(context.Owner.transform.position, context.Owner.transform.forward, out hit, maxDistance, layerMask, QueryTriggerInteraction.Collide))
            {
                context.Blackboard.SetValue(targetKey, hit.collider == null ? null : hit.collider.gameObject);
                if (!string.IsNullOrEmpty(hitPointKey))
                {
                    context.Blackboard.SetValue(hitPointKey, hit.point);
                }

                return;
            }

            if (clearOnMiss)
            {
                context.Blackboard.ClearValue(targetKey);
                if (!string.IsNullOrEmpty(hitPointKey))
                {
                    context.Blackboard.ClearValue(hitPointKey);
                }
            }
        }
    }

    internal sealed class BehaviorTreeSetBlackboardFromBlueprintService : BehaviorTreeServiceExecutor
    {
        public override string TypeId
        {
            get { return "BT.SetBlackboardFromBlueprint"; }
        }

        public override void Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service)
        {
            string variableName = BehaviorTreePropertyUtility.GetString(service.Properties, "variableName", null);
            string blackboardKey = BehaviorTreePropertyUtility.GetString(service.Properties, "blackboardKey", null);
            if (string.IsNullOrEmpty(variableName) || string.IsNullOrEmpty(blackboardKey))
            {
                return;
            }

            IBlueprintInstance target = BehaviorTreeBlueprintTargetUtility.ResolveTarget(context, service.Properties, false);
            object value;
            if (target != null && target.TryGetVariable(variableName, out value))
            {
                context.Blackboard.SetValue(blackboardKey, value);
            }
        }
    }

    internal sealed class BehaviorTreeTriggerBlueprintService : BehaviorTreeServiceExecutor
    {
        public override string TypeId
        {
            get { return "BT.TriggerBlueprintService"; }
        }

        public override void Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service)
        {
            string eventName = BehaviorTreePropertyUtility.GetString(service.Properties, "eventName", null);
            if (string.IsNullOrEmpty(eventName))
            {
                return;
            }

            IBlueprintInstance target = BehaviorTreeBlueprintTargetUtility.ResolveTarget(context, service.Properties, false);
            if (target != null)
            {
                target.TriggerEvent(eventName);
            }
        }
    }

    internal sealed class BehaviorTreeVehicleRoadFindNearestLaneExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.VehicleRoad.FindNearestLane"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            VehicleRoadSubsystem subsystem = BehaviorTreeVehicleRoadUtility.ResolveRequiredInputComponent<VehicleRoadSubsystem>(
                context,
                node,
                "subsystem",
                TypeId);
            if (subsystem == null)
            {
                BehaviorTreeVehicleRoadUtility.WriteBool(context, node, "foundKey", false);
                return BehaviorTreeStatus.Failure;
            }

            Vector3 position;
            if (!BehaviorTreeVehicleRoadUtility.TryResolveVector3OrOwner(
                    context,
                    node,
                    "position",
                    "positionKey",
                    "position",
                    true,
                    out position))
            {
                context.Runtime.MarkFailure(TypeId + " requires a position input or owner transform.");
                BehaviorTreeVehicleRoadUtility.WriteBool(context, node, "foundKey", false);
                return BehaviorTreeStatus.Failure;
            }

            Vector3 heading;
            BehaviorTreeVehicleRoadUtility.TryResolveVector3OrOwner(
                context,
                node,
                "heading",
                "headingKey",
                "heading",
                false,
                out heading);

            VehicleRoadNearestResult result;
            bool found = subsystem.TryFindNearestLane(
                position,
                heading.sqrMagnitude <= 0.0001f ? Vector3.forward : heading,
                BehaviorTreeVehicleRoadUtility.ResolveAgentMask(context, node),
                BehaviorTreePropertyUtility.ResolveFloat(context, node, "maxDistance", "maxDistance", 0f),
                BehaviorTreePropertyUtility.ResolveFloat(context, node, "maxHeightDifference", "maxHeightDifference", 0f),
                out result);

            BehaviorTreeVehicleRoadUtility.WriteBool(context, node, "foundKey", found);
            if (!found)
            {
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeVehicleRoadUtility.WriteValue(context, node, "laneIdKey", result.LaneId);
            BehaviorTreeVehicleRoadUtility.WriteValue(context, node, "positionKey", result.Position);
            BehaviorTreeVehicleRoadUtility.WriteValue(context, node, "forwardKey", result.Forward);
            BehaviorTreeVehicleRoadUtility.WriteValue(context, node, "upKey", result.Up);
            BehaviorTreeVehicleRoadUtility.WriteValue(context, node, "distanceAlongLaneKey", result.DistanceAlongLane);
            BehaviorTreeVehicleRoadUtility.WriteValue(context, node, "distanceToLaneKey", result.DistanceToLane);
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeVehicleRoadFindLaneRouteExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.VehicleRoad.FindLaneRoute"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            VehicleRoadSubsystem subsystem = BehaviorTreeVehicleRoadUtility.ResolveRequiredInputComponent<VehicleRoadSubsystem>(
                context,
                node,
                "subsystem",
                TypeId);
            if (subsystem == null)
            {
                BehaviorTreeVehicleRoadUtility.WriteBool(context, node, "successKey", false);
                return BehaviorTreeStatus.Failure;
            }

            string startLaneId = BehaviorTreePropertyUtility.ResolveString(context, node, "startLaneId", "startLaneId", string.Empty);
            string destinationLaneId = BehaviorTreePropertyUtility.ResolveString(context, node, "destinationLaneId", "destinationLaneId", string.Empty);
            if (string.IsNullOrWhiteSpace(startLaneId) || string.IsNullOrWhiteSpace(destinationLaneId))
            {
                context.Runtime.MarkFailure(TypeId + " requires non-empty startLaneId and destinationLaneId.");
                BehaviorTreeVehicleRoadUtility.WriteBool(context, node, "successKey", false);
                return BehaviorTreeStatus.Failure;
            }

            VehicleRoadRouteResult result;
            bool success = subsystem.TryFindRoute(
                new LaneRouteQuery(startLaneId, destinationLaneId, BehaviorTreeVehicleRoadUtility.ResolveAgentMask(context, node)),
                out result);
            BehaviorTreeVehicleRoadUtility.WriteBool(context, node, "successKey", success);
            if (!success || result == null)
            {
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeVehicleRoadUtility.WriteValue(context, node, "routeLaneIdsKey", new List<string>(result.laneIds));
            BehaviorTreeVehicleRoadUtility.WriteValue(context, node, "totalCostKey", result.totalCost);
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeVehicleRoadComputeFollowerControlExecutor : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.VehicleRoad.ComputeFollowerControl"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            VehicleLaneFollower follower = BehaviorTreeVehicleRoadUtility.ResolveInputOrOwnerComponent<VehicleLaneFollower>(
                context,
                node,
                "follower");
            if (follower == null)
            {
                context.Runtime.MarkFailure(TypeId + " requires a VehicleLaneFollower input or owner component.");
                BehaviorTreeVehicleRoadUtility.WriteBool(context, node, "validKey", false);
                return BehaviorTreeStatus.Failure;
            }

            VehicleLaneFollowerInput input = BehaviorTreeVehicleRoadUtility.CreateFollowerInput(context, node);
            VehicleLaneFollowerOutput output = follower.ComputeControl(input);
            BehaviorTreeVehicleRoadUtility.WriteFollowerOutput(context, node, output);
            return output.valid ? BehaviorTreeStatus.Success : BehaviorTreeStatus.Failure;
        }
    }

    internal sealed class BehaviorTreeVehicleRoadDriveFollowerExecutor : BehaviorTreeNodeExecutor
    {
        private const string CurrentSpeedStateKey = "currentSpeed";
        private const string InvalidOutputDurationStateKey = "invalidOutputDuration";
        private const string LoopStartPositionStateKey = "loopStartPosition";
        private const string LoopStartRotationStateKey = "loopStartRotation";
        private const string AutoVehicleIdStateKey = "autoVehicleId";

        public override string TypeId
        {
            get { return "BT.VehicleRoad.DriveFollower"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            VehicleLaneFollower follower = BehaviorTreeVehicleRoadUtility.ResolveInputOrOwnerComponent<VehicleLaneFollower>(
                context,
                node,
                "follower");
            if (follower == null)
            {
                context.Runtime.MarkFailure(TypeId + " requires a VehicleLaneFollower input or owner component.");
                BehaviorTreeVehicleRoadUtility.WriteBool(context, node, "validKey", false);
                return BehaviorTreeStatus.Failure;
            }

            if (context.Owner == null)
            {
                context.Runtime.MarkFailure(TypeId + " requires an owner GameObject to move.");
                BehaviorTreeVehicleRoadUtility.WriteBool(context, node, "validKey", false);
                return BehaviorTreeStatus.Failure;
            }

            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            Transform transform = context.Owner.transform;
            CaptureLoopStart(state, transform);

            float deltaTime = Mathf.Max(0f, context.DeltaTime);
            float acceleration = Mathf.Max(0f, BehaviorTreePropertyUtility.ResolveFloat(context, node, "acceleration", "acceleration", 6f));
            float vehicleLength = Mathf.Max(0.1f, BehaviorTreePropertyUtility.ResolveFloat(context, node, "vehicleLength", "vehicleLength", 4.5f));
            float wheelBase = BehaviorTreePropertyUtility.ResolveFloat(context, node, "wheelBase", "wheelBase", 0f);
            if (wheelBase <= 0.0001f)
            {
                wheelBase = vehicleLength * 0.55f;
            }

            float currentSpeed = GetStateFloat(state, CurrentSpeedStateKey, 0f);
            string vehicleId = ResolveVehicleId(context, node, state, true);
            VehicleLaneFollowerOutput output = follower.ComputeControl(new VehicleLaneFollowerInput
            {
                vehicleId = vehicleId,
                position = transform.position,
                forward = transform.forward,
                speed = currentSpeed,
                wheelBase = Mathf.Max(0.1f, wheelBase),
                vehicleLength = vehicleLength,
                agentMask = ResolveAgentMask(context, node),
                leadVehicleDistance = BehaviorTreePropertyUtility.ResolveFloat(context, node, "leadVehicleDistance", "leadVehicleDistance", 0f),
                leadVehicleSpeed = BehaviorTreePropertyUtility.ResolveFloat(context, node, "leadVehicleSpeed", "leadVehicleSpeed", 0f),
                requestLaneChange = BehaviorTreePropertyUtility.ResolveBool(context, node, "requestLaneChange", "requestLaneChange", false),
                requestedLaneChangeSide = ResolveLaneChangeSide(context, node)
            });

            BehaviorTreeVehicleRoadUtility.WriteFollowerOutput(context, node, output);
            WriteDriveOutput(context, node, "arrivedKey", false);
            WriteDriveOutput(context, node, "loopResetKey", false);

            bool loopRoute = BehaviorTreePropertyUtility.ResolveBool(context, node, "loopRoute", "loopRoute", false);
            float loopResetDelay = Mathf.Max(0.1f, BehaviorTreePropertyUtility.ResolveFloat(context, node, "loopResetDelay", "loopResetDelay", 2f));
            if (!output.valid)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * deltaTime);
                SetCurrentSpeed(context, node, state, currentSpeed);
                if (!loopRoute)
                {
                    context.Runtime.MarkFailure(TypeId + " follower output was invalid.");
                    return BehaviorTreeStatus.Failure;
                }

                return TickLoopResetDelay(context, node, state, follower, transform, vehicleId, loopResetDelay);
            }

            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                Mathf.Max(0f, output.targetSpeed),
                acceleration * deltaTime);

            bool followBakedLanePose = BehaviorTreePropertyUtility.ResolveBool(context, node, "followBakedLanePose", "followBakedLanePose", false);
            if (followBakedLanePose && follower.IsAtRouteEnd(output.currentLaneId, output.distanceAlongLane))
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * deltaTime);
                SetCurrentSpeed(context, node, state, currentSpeed);
                WriteDriveOutput(context, node, "arrivedKey", true);
                if (loopRoute)
                {
                    return TickLoopResetDelay(context, node, state, follower, transform, vehicleId, loopResetDelay);
                }

                return BehaviorTreeStatus.Success;
            }

            state.Data[InvalidOutputDurationStateKey] = 0f;
            float requestedTravelDistance = currentSpeed * deltaTime;
            float travelDistance = requestedTravelDistance;
            bool reachedExplicitStopPoint = false;
            if (output.hasStopPoint && float.IsFinite(output.distanceToStopLine))
            {
                float distanceToStop = Mathf.Max(0f, output.distanceToStopLine);
                if (output.targetSpeed <= 0.01f && distanceToStop > 0.001f)
                {
                    requestedTravelDistance = Mathf.Max(
                        requestedTravelDistance,
                        Mathf.Max(0.1f, BehaviorTreePropertyUtility.ResolveFloat(context, node, "stopPointApproachSpeed", "stopPointApproachSpeed", 2f)) * deltaTime);
                    travelDistance = Mathf.Max(travelDistance, requestedTravelDistance);
                }

                reachedExplicitStopPoint = output.targetSpeed <= 0.01f &&
                                           distanceToStop <= requestedTravelDistance + 0.001f;
                travelDistance = Mathf.Min(travelDistance, distanceToStop);
            }

            if (reachedExplicitStopPoint)
            {
                transform.position = output.stopPoint;
                currentSpeed = 0f;
                SetCurrentSpeed(context, node, state, currentSpeed);
                return BehaviorTreeStatus.Running;
            }

            if (followBakedLanePose && TryMoveAlongBakedRoute(follower, transform, output, travelDistance))
            {
                SetCurrentSpeed(context, node, state, currentSpeed);
                return BehaviorTreeStatus.Running;
            }

            Vector3 toTarget = output.lookAheadPoint - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    Mathf.Max(0f, BehaviorTreePropertyUtility.ResolveFloat(context, node, "turnSpeed", "turnSpeed", 180f)) * deltaTime);
            }

            transform.position += transform.forward * travelDistance;
            SetCurrentSpeed(context, node, state, currentSpeed);
            return BehaviorTreeStatus.Running;
        }

        public override void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            if (BehaviorTreePropertyUtility.ResolveBool(context, node, "unregisterOnAbort", "unregisterOnAbort", true))
            {
                VehicleLaneFollower follower = BehaviorTreeVehicleRoadUtility.ResolveInputOrOwnerComponent<VehicleLaneFollower>(
                    context,
                    node,
                    "follower");
                UnregisterVehicle(follower, ResolveVehicleId(context, node, state, false));
            }

            state.Data.Clear();
        }

        private BehaviorTreeStatus TickLoopResetDelay(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            BehaviorTreeNodeRuntimeState state,
            VehicleLaneFollower follower,
            Transform transform,
            string vehicleId,
            float loopResetDelay)
        {
            float invalidOutputDuration = GetStateFloat(state, InvalidOutputDurationStateKey, 0f) + Mathf.Max(0f, context.DeltaTime);
            state.Data[InvalidOutputDurationStateKey] = invalidOutputDuration;
            if (invalidOutputDuration >= loopResetDelay)
            {
                ResetLoop(context, node, state, follower, transform, vehicleId);
            }

            return BehaviorTreeStatus.Running;
        }

        private static void CaptureLoopStart(BehaviorTreeNodeRuntimeState state, Transform transform)
        {
            if (state.Data.ContainsKey(LoopStartPositionStateKey))
            {
                return;
            }

            state.Data[LoopStartPositionStateKey] = transform.position;
            state.Data[LoopStartRotationStateKey] = transform.rotation;
        }

        private static void ResetLoop(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            BehaviorTreeNodeRuntimeState state,
            VehicleLaneFollower follower,
            Transform transform,
            string vehicleId)
        {
            UnregisterVehicle(follower, vehicleId);
            Vector3 position = state.Data.ContainsKey(LoopStartPositionStateKey)
                ? (Vector3)state.Data[LoopStartPositionStateKey]
                : transform.position;
            Quaternion rotation = state.Data.ContainsKey(LoopStartRotationStateKey)
                ? (Quaternion)state.Data[LoopStartRotationStateKey]
                : transform.rotation;
            transform.SetPositionAndRotation(position, rotation);
            state.Data[CurrentSpeedStateKey] = 0f;
            state.Data[InvalidOutputDurationStateKey] = 0f;
            WriteDriveOutput(context, node, "currentSpeedKey", 0f);
            WriteDriveOutput(context, node, "loopResetKey", true);
        }

        private static void UnregisterVehicle(VehicleLaneFollower follower, string vehicleId)
        {
            if (follower != null &&
                follower.RoadSubsystem != null &&
                !string.IsNullOrWhiteSpace(vehicleId))
            {
                follower.RoadSubsystem.UnregisterVehicle(vehicleId);
            }
        }

        private static bool TryMoveAlongBakedRoute(
            VehicleLaneFollower follower,
            Transform transform,
            VehicleLaneFollowerOutput output,
            float travelDistance)
        {
            RoadLanePose pose;
            string laneId;
            if (follower == null ||
                !follower.TryEvaluateRoutePose(
                    output.currentLaneId,
                    output.distanceAlongLane + Mathf.Max(0f, travelDistance),
                    out laneId,
                    out pose))
            {
                return false;
            }

            Vector3 forward = pose.forward.sqrMagnitude > 0.0001f ? pose.forward.normalized : transform.forward;
            Vector3 up = pose.up.sqrMagnitude > 0.0001f ? pose.up.normalized : Vector3.up;
            transform.SetPositionAndRotation(pose.position, Quaternion.LookRotation(forward, up));
            return true;
        }

        private static string ResolveVehicleId(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            BehaviorTreeNodeRuntimeState state,
            bool createIfMissing)
        {
            string vehicleId = BehaviorTreePropertyUtility.ResolveString(context, node, "vehicleId", "vehicleId", string.Empty);
            if (!string.IsNullOrWhiteSpace(vehicleId))
            {
                return vehicleId;
            }

            object stored;
            if (state.Data.TryGetValue(AutoVehicleIdStateKey, out stored) && stored != null)
            {
                return Convert.ToString(stored, CultureInfo.InvariantCulture);
            }

            if (!createIfMissing)
            {
                return string.Empty;
            }

            vehicleId = "bt_vehicle_" + (context.Owner == null ? node.Id : context.Owner.GetInstanceID().ToString(CultureInfo.InvariantCulture));
            state.Data[AutoVehicleIdStateKey] = vehicleId;
            return vehicleId;
        }

        private static RoadAgentMask ResolveAgentMask(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            RoadAgentMask mask = ResolveEnum(context, node, "agentMask", "agentMask", RoadAgentMask.Car);
            return mask == RoadAgentMask.None ? RoadAgentMask.Car : mask;
        }

        private static RoadLaneAdjacentSide ResolveLaneChangeSide(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            return ResolveEnum(context, node, "requestedLaneChangeSide", "requestedLaneChangeSide", RoadLaneAdjacentSide.Right);
        }

        private static T ResolveEnum<T>(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            string propertyKey,
            T defaultValue) where T : struct
        {
            object value;
            if (BehaviorTreePropertyUtility.TryResolveValue(context, node, inputId, propertyKey, out value) && value != null)
            {
                if (value is T)
                {
                    return (T)value;
                }

                return BlueprintTypeUtility.ConvertValue(value, defaultValue);
            }

            return defaultValue;
        }

        private static float GetStateFloat(BehaviorTreeNodeRuntimeState state, string key, float defaultValue)
        {
            object value;
            return state.Data.TryGetValue(key, out value) && value != null
                ? Convert.ToSingle(value, CultureInfo.InvariantCulture)
                : defaultValue;
        }

        private static void SetCurrentSpeed(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            BehaviorTreeNodeRuntimeState state,
            float currentSpeed)
        {
            state.Data[CurrentSpeedStateKey] = currentSpeed;
            WriteDriveOutput(context, node, "currentSpeedKey", currentSpeed);
        }

        private static void WriteDriveOutput(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string keyProperty,
            object value)
        {
            BehaviorTreeVehicleRoadUtility.WriteValue(context, node, keyProperty, value);
        }
    }

    internal sealed class BehaviorTreeVehicleRoadUpdateRoadAgentService : BehaviorTreeServiceExecutor
    {
        public override string TypeId
        {
            get { return "BT.VehicleRoad.UpdateRoadAgent"; }
        }

        public override void Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, RuntimeBehaviorTreeService service)
        {
            RoadAgent agent = BehaviorTreeVehicleRoadUtility.ResolveServiceInputOrOwnerComponent<RoadAgent>(
                context,
                service,
                "agent");
            if (agent == null)
            {
                BehaviorTreeVehicleRoadUtility.WriteBool(context, service.Properties, "validKey", false);
                context.Runtime.MarkFailure(TypeId + " requires a RoadAgent input or owner component.");
                return;
            }

            Vector3 position;
            BehaviorTreeVehicleRoadUtility.TryResolveServiceVector3OrOwner(
                context,
                service,
                "position",
                "positionKey",
                "position",
                true,
                out position);

            Vector3 forward;
            BehaviorTreeVehicleRoadUtility.TryResolveServiceVector3OrOwner(
                context,
                service,
                "forward",
                "forwardKey",
                "forward",
                false,
                out forward);

            float deltaTime = BehaviorTreeVehicleRoadUtility.ResolveServiceFloat(context, service, "deltaTime", "deltaTime", context.DeltaTime);
            RoadAgentControlOutput output = agent.Evaluate(
                position,
                forward.sqrMagnitude <= 0.0001f ? Vector3.forward : forward,
                BehaviorTreeVehicleRoadUtility.ResolveServiceFloat(context, service, "speed", "speed", 0f),
                deltaTime);
            BehaviorTreeVehicleRoadUtility.WriteRoadAgentOutput(context, service.Properties, output);
        }
    }

    internal static class BehaviorTreeVehicleRoadUtility
    {
        public static T ResolveRequiredInputComponent<T>(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            string typeId) where T : Component
        {
            object value;
            if (!BehaviorTreePropertyUtility.TryGetInputValue(context, node, inputId, out value))
            {
                context.Runtime.MarkFailure(typeId + " requires " + inputId + " input.");
                return null;
            }

            T component = ResolveComponent<T>(value);
            if (component == null)
            {
                context.Runtime.MarkFailure(typeId + " could not resolve " + typeof(T).Name + " from " + inputId + ".");
            }

            return component;
        }

        public static T ResolveInputOrOwnerComponent<T>(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId) where T : Component
        {
            object value;
            if (BehaviorTreePropertyUtility.TryGetInputValue(context, node, inputId, out value))
            {
                T inputComponent = ResolveComponent<T>(value);
                if (inputComponent != null)
                {
                    return inputComponent;
                }
            }

            return context.Owner == null ? null : context.Owner.GetComponent<T>();
        }

        public static T ResolveServiceInputOrOwnerComponent<T>(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeService service,
            string inputId) where T : Component
        {
            object value;
            if (TryGetServiceInputValue(context, service, inputId, out value))
            {
                T inputComponent = ResolveComponent<T>(value);
                if (inputComponent != null)
                {
                    return inputComponent;
                }
            }

            return context.Owner == null ? null : context.Owner.GetComponent<T>();
        }

        public static RoadAgentMask ResolveAgentMask(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            RoadAgentMask mask = ResolveEnum(context, node, "agentMask", "agentMask", RoadAgentMask.MotorVehicles);
            return mask == RoadAgentMask.None ? RoadAgentMask.MotorVehicles : mask;
        }

        public static VehicleLaneFollowerInput CreateFollowerInput(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            Vector3 position;
            TryResolveVector3OrOwner(context, node, "position", "positionKey", "position", true, out position);
            Vector3 forward;
            TryResolveVector3OrOwner(context, node, "forward", "forwardKey", "forward", false, out forward);

            return new VehicleLaneFollowerInput
            {
                vehicleId = BehaviorTreePropertyUtility.ResolveString(context, node, "vehicleId", "vehicleId", string.Empty),
                position = position,
                forward = forward.sqrMagnitude <= 0.0001f ? Vector3.forward : forward,
                speed = BehaviorTreePropertyUtility.ResolveFloat(context, node, "speed", "speed", 0f),
                wheelBase = BehaviorTreePropertyUtility.ResolveFloat(context, node, "wheelBase", "wheelBase", 2.7f),
                vehicleLength = BehaviorTreePropertyUtility.ResolveFloat(context, node, "vehicleLength", "vehicleLength", 4.5f),
                agentMask = ResolveAgentMask(context, node),
                leadVehicleDistance = BehaviorTreePropertyUtility.ResolveFloat(context, node, "leadVehicleDistance", "leadVehicleDistance", 0f),
                leadVehicleSpeed = BehaviorTreePropertyUtility.ResolveFloat(context, node, "leadVehicleSpeed", "leadVehicleSpeed", 0f),
                requestLaneChange = BehaviorTreePropertyUtility.ResolveBool(context, node, "requestLaneChange", "requestLaneChange", false),
                requestedLaneChangeSide = ResolveEnum(context, node, "requestedLaneChangeSide", "requestedLaneChangeSide", RoadLaneAdjacentSide.Right)
            };
        }

        public static void WriteFollowerOutput(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, VehicleLaneFollowerOutput output)
        {
            WriteBool(context, node, "validKey", output.valid);
            WriteValue(context, node, "currentLaneIdKey", output.currentLaneId ?? string.Empty);
            WriteValue(context, node, "distanceAlongLaneKey", output.distanceAlongLane);
            WriteValue(context, node, "targetSteeringAngleKey", output.targetSteeringAngle);
            WriteValue(context, node, "targetSpeedKey", output.targetSpeed);
            WriteValue(context, node, "lookAheadPointKey", output.lookAheadPoint);
            WriteValue(context, node, "recoveryModeKey", output.recoveryMode);
            WriteValue(context, node, "recoveryPositionKey", output.recoveryPosition);
            WriteValue(context, node, "lateralErrorKey", output.lateralError);
            WriteValue(context, node, "stopReasonKey", output.stopReason);
            WriteValue(context, node, "passageStatusKey", output.passageStatus);
            WriteValue(context, node, "signalStateKey", output.signalState);
            WriteBool(context, node, "hasStopPointKey", output.hasStopPoint);
            WriteValue(context, node, "stopPointKey", output.stopPoint);
            WriteValue(context, node, "distanceToStopLineKey", output.distanceToStopLine);
            WriteValue(context, node, "queueIndexKey", output.queueIndex);
            WriteValue(context, node, "junctionIdKey", output.junctionId ?? string.Empty);
            WriteValue(context, node, "connectorLaneIdKey", output.connectorLaneId ?? string.Empty);
            WriteValue(context, node, "laneChangeStatusKey", output.laneChangeStatus);
            WriteValue(context, node, "laneChangeTargetLaneIdKey", output.laneChangeTargetLaneId ?? string.Empty);
        }

        public static void WriteRoadAgentOutput(BehaviorTreeExecutionContext context, Dictionary<string, object> properties, RoadAgentControlOutput output)
        {
            WriteValue(context, properties, "validKey", output.valid);
            WriteValue(context, properties, "agentStateKey", output.agentState);
            WriteValue(context, properties, "routeStateKey", output.routeState);
            WriteValue(context, properties, "failureReasonKey", output.failureReason);
            WriteValue(context, properties, "currentElementKindKey", output.currentElementKind);
            WriteValue(context, properties, "currentElementIdKey", output.currentElementId ?? string.Empty);
            WriteValue(context, properties, "routeSegmentIndexKey", output.routeSegmentIndex);
            WriteValue(context, properties, "targetPositionKey", output.targetPosition);
            WriteValue(context, properties, "targetForwardKey", output.targetForward);
            WriteValue(context, properties, "targetUpKey", output.targetUp);
            WriteValue(context, properties, "targetSpeedKey", output.targetSpeed);
            WriteValue(context, properties, "remainingDistanceKey", output.remainingDistance);
            WriteValue(context, properties, "distanceToBoundaryKey", output.distanceToBoundary);
            WriteValue(context, properties, "arrivedKey", output.arrived);
            WriteValue(context, properties, "shouldRecoverKey", output.shouldRecover);
            WriteValue(context, properties, "recoveryPositionKey", output.recoveryPosition);
        }

        public static bool TryResolveVector3OrOwner(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            string keyProperty,
            string valueProperty,
            bool useOwnerPosition,
            out Vector3 value)
        {
            if (BehaviorTreePropertyUtility.TryResolveVector3(context, node, inputId, keyProperty, valueProperty, out value))
            {
                return true;
            }

            if (context.Owner != null)
            {
                value = useOwnerPosition ? context.Owner.transform.position : context.Owner.transform.forward;
                return true;
            }

            value = useOwnerPosition ? Vector3.zero : Vector3.forward;
            return false;
        }

        public static bool TryResolveServiceVector3OrOwner(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeService service,
            string inputId,
            string keyProperty,
            string valueProperty,
            bool useOwnerPosition,
            out Vector3 value)
        {
            object inputValue;
            if (TryGetServiceInputValue(context, service, inputId, out inputValue) &&
                BehaviorTreeValueUtility.TryGetVector3(inputValue, out value))
            {
                return true;
            }

            if (service != null &&
                BehaviorTreePropertyUtility.TryResolveVector3(context, service.Properties, keyProperty, valueProperty, out value))
            {
                return true;
            }

            if (context.Owner != null)
            {
                value = useOwnerPosition ? context.Owner.transform.position : context.Owner.transform.forward;
                return true;
            }

            value = useOwnerPosition ? Vector3.zero : Vector3.forward;
            return false;
        }

        public static float ResolveServiceFloat(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeService service,
            string inputId,
            string propertyKey,
            float defaultValue)
        {
            object value;
            if (TryGetServiceInputValue(context, service, inputId, out value) && value != null)
            {
                return BlueprintTypeUtility.ConvertValue(value, defaultValue);
            }

            return BehaviorTreePropertyUtility.GetFloat(service == null ? null : service.Properties, propertyKey, defaultValue);
        }

        public static void WriteBool(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, string keyProperty, bool value)
        {
            WriteValue(context, node, keyProperty, value);
        }

        public static void WriteBool(BehaviorTreeExecutionContext context, Dictionary<string, object> properties, string keyProperty, bool value)
        {
            WriteValue(context, properties, keyProperty, value);
        }

        public static void WriteValue(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, string keyProperty, object value)
        {
            WriteValue(context, node == null ? null : node.Properties, keyProperty, value);
        }

        public static void WriteValue(BehaviorTreeExecutionContext context, Dictionary<string, object> properties, string keyProperty, object value)
        {
            string key = BehaviorTreePropertyUtility.GetString(properties, keyProperty, null);
            if (!string.IsNullOrEmpty(key) && context != null && context.Blackboard != null)
            {
                context.Blackboard.SetValue(key, value);
            }
        }

        private static bool TryGetServiceInputValue(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeService service,
            string inputId,
            out object value)
        {
            value = null;
            if (service == null || string.IsNullOrEmpty(inputId))
            {
                return false;
            }

            string key = BehaviorTreePropertyUtility.GetString(service.Properties, inputId + "Key", null);
            if (!string.IsNullOrEmpty(key))
            {
                if (context != null && context.Blackboard != null)
                {
                    context.Blackboard.TryGetValue(key, out value);
                }

                return true;
            }

            if (service.Properties != null && service.Properties.TryGetValue(inputId, out value))
            {
                return true;
            }

            return false;
        }

        private static T ResolveEnum<T>(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            string propertyKey,
            T defaultValue) where T : struct
        {
            object value;
            if (BehaviorTreePropertyUtility.TryResolveValue(context, node, inputId, propertyKey, out value) && value != null)
            {
                if (value is T)
                {
                    return (T)value;
                }

                string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        return (T)Enum.Parse(typeof(T), text, false);
                    }
                    catch
                    {
                    }
                }
            }

            return defaultValue;
        }

        private static T ResolveComponent<T>(object value) where T : Component
        {
            if (value == null)
            {
                return null;
            }

            T direct = value as T;
            if (direct != null)
            {
                return direct;
            }

            GameObject gameObject = BehaviorTreeValueUtility.ToGameObject(value);
            if (gameObject != null)
            {
                return gameObject.GetComponent<T>();
            }

            Component component = value as Component;
            return component == null ? null : component.GetComponent<T>();
        }
    }

    internal static class BehaviorTreePropertyUtility
    {
        public static string GetString(Dictionary<string, object> properties, string key, string defaultValue)
        {
            object value;
            if (properties == null || !properties.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static bool GetBool(Dictionary<string, object> properties, string key, bool defaultValue)
        {
            object value;
            if (properties == null || !properties.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            return BlueprintTypeUtility.ConvertValue(value, defaultValue);
        }

        public static int GetInt(Dictionary<string, object> properties, string key, int defaultValue)
        {
            object value;
            if (properties == null || !properties.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            return BlueprintTypeUtility.ConvertValue(value, defaultValue);
        }

        public static float GetFloat(Dictionary<string, object> properties, string key, float defaultValue)
        {
            object value;
            if (properties == null || !properties.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            return BlueprintTypeUtility.ConvertValue(value, defaultValue);
        }

        public static bool TryGetInputBinding(RuntimeBehaviorTreeNode node, string inputId, out string blackboardKey)
        {
            blackboardKey = null;
            return node != null &&
                   node.Inputs != null &&
                   !string.IsNullOrEmpty(inputId) &&
                   node.Inputs.TryGetValue(inputId, out blackboardKey) &&
                   !string.IsNullOrEmpty(blackboardKey);
        }

        public static bool TryGetInputBinding(RuntimeBehaviorTreeDecorator decorator, string inputId, out string blackboardKey)
        {
            blackboardKey = null;
            return decorator != null &&
                   decorator.Inputs != null &&
                   !string.IsNullOrEmpty(inputId) &&
                   decorator.Inputs.TryGetValue(inputId, out blackboardKey) &&
                   !string.IsNullOrEmpty(blackboardKey);
        }

        public static bool TryGetInputValue(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            out object value)
        {
            value = null;
            string blackboardKey;
            if (!TryGetInputBinding(node, inputId, out blackboardKey))
            {
                return false;
            }

            if (context != null && context.Blackboard != null)
            {
                context.Blackboard.TryGetValue(blackboardKey, out value);
            }

            return true;
        }

        public static bool TryGetInputValue(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeDecorator decorator,
            string inputId,
            out object value)
        {
            value = null;
            string blackboardKey;
            if (!TryGetInputBinding(decorator, inputId, out blackboardKey))
            {
                return false;
            }

            if (context != null && context.Blackboard != null)
            {
                context.Blackboard.TryGetValue(blackboardKey, out value);
            }

            return true;
        }

        public static bool TryResolveValue(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            string propertyKey,
            out object value)
        {
            if (TryGetInputValue(context, node, inputId, out value))
            {
                return true;
            }

            if (node != null && node.Properties != null && node.Properties.TryGetValue(propertyKey, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        public static bool TryResolveValue(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeDecorator decorator,
            string inputId,
            string propertyKey,
            out object value)
        {
            if (TryGetInputValue(context, decorator, inputId, out value))
            {
                return true;
            }

            if (decorator != null &&
                decorator.Properties != null &&
                !string.IsNullOrEmpty(propertyKey) &&
                decorator.Properties.TryGetValue(propertyKey, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        public static string GetBoundBlackboardKey(RuntimeBehaviorTreeNode node, string inputId, string propertyKey, string defaultValue)
        {
            string blackboardKey;
            if (TryGetInputBinding(node, inputId, out blackboardKey))
            {
                return blackboardKey;
            }

            return node == null ? defaultValue : GetString(node.Properties, propertyKey, defaultValue);
        }

        public static string ResolveString(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            string propertyKey,
            string defaultValue)
        {
            object value;
            if (TryGetInputValue(context, node, inputId, out value) && value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return node == null ? defaultValue : GetString(node.Properties, propertyKey, defaultValue);
        }

        public static bool ResolveBool(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            string propertyKey,
            bool defaultValue)
        {
            object value;
            if (TryGetInputValue(context, node, inputId, out value) && value != null)
            {
                return BlueprintTypeUtility.ConvertValue(value, defaultValue);
            }

            return node == null ? defaultValue : GetBool(node.Properties, propertyKey, defaultValue);
        }

        public static bool ResolveBool(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeDecorator decorator,
            string inputId,
            string keyProperty,
            string valueProperty,
            bool defaultValue)
        {
            object value;
            return TryResolveDecoratorValue(context, decorator, inputId, keyProperty, valueProperty, defaultValue, out value) && value != null
                ? BlueprintTypeUtility.ConvertValue(value, defaultValue)
                : defaultValue;
        }

        public static float ResolveFloat(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            string propertyKey,
            float defaultValue)
        {
            object value;
            if (TryGetInputValue(context, node, inputId, out value) && value != null)
            {
                return BlueprintTypeUtility.ConvertValue(value, defaultValue);
            }

            return node == null ? defaultValue : GetFloat(node.Properties, propertyKey, defaultValue);
        }

        public static float ResolveFloat(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeDecorator decorator,
            string inputId,
            string keyProperty,
            string valueProperty,
            float defaultValue)
        {
            float value;
            return TryResolveFloat(context, decorator, inputId, keyProperty, valueProperty, defaultValue, out value)
                ? value
                : defaultValue;
        }

        public static bool TryResolveFloat(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeDecorator decorator,
            string inputId,
            string keyProperty,
            string valueProperty,
            float defaultValue,
            out float value)
        {
            object rawValue;
            if (TryResolveDecoratorValue(context, decorator, inputId, keyProperty, valueProperty, defaultValue, out rawValue))
            {
                value = rawValue == null ? defaultValue : BlueprintTypeUtility.ConvertValue(rawValue, defaultValue);
                return true;
            }

            value = defaultValue;
            return false;
        }

        public static float ResolveFloat(BehaviorTreeExecutionContext context, Dictionary<string, object> properties, string keyProperty, string valueProperty, float defaultValue)
        {
            string key = GetString(properties, keyProperty, null);
            if (!string.IsNullOrEmpty(key))
            {
                object blackboardValue = context.Blackboard.GetValue(key);
                return blackboardValue == null ? defaultValue : BlueprintTypeUtility.ConvertValue(blackboardValue, defaultValue);
            }

            return GetFloat(properties, valueProperty, defaultValue);
        }

        public static bool TryResolveVector3(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            string keyProperty,
            string valueProperty,
            out Vector3 value)
        {
            value = Vector3.zero;
            object inputValue;
            if (TryGetInputValue(context, node, inputId, out inputValue))
            {
                return BehaviorTreeValueUtility.TryGetVector3(inputValue, out value);
            }

            if (!string.IsNullOrEmpty(valueProperty) &&
                valueProperty != inputId &&
                TryGetInputValue(context, node, valueProperty, out inputValue))
            {
                return BehaviorTreeValueUtility.TryGetVector3(inputValue, out value);
            }

            return node != null && TryResolveVector3(context, node.Properties, keyProperty, valueProperty, out value);
        }

        public static bool TryResolveVector3(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeDecorator decorator,
            string inputId,
            string keyProperty,
            string valueProperty,
            out Vector3 value)
        {
            value = Vector3.zero;
            object inputValue;
            if (TryGetInputValue(context, decorator, inputId, out inputValue))
            {
                return BehaviorTreeValueUtility.TryGetVector3(inputValue, out value);
            }

            if (!string.IsNullOrEmpty(valueProperty) &&
                valueProperty != inputId &&
                TryGetInputValue(context, decorator, valueProperty, out inputValue))
            {
                return BehaviorTreeValueUtility.TryGetVector3(inputValue, out value);
            }

            return decorator != null && TryResolveVector3(context, decorator.Properties, keyProperty, valueProperty, out value);
        }

        public static bool TryResolveVector3(
            BehaviorTreeExecutionContext context,
            Dictionary<string, object> properties,
            string keyProperty,
            string valueProperty,
            out Vector3 value)
        {
            value = Vector3.zero;
            string key = GetString(properties, keyProperty, null);
            if (!string.IsNullOrEmpty(key))
            {
                object blackboardValue = context.Blackboard.GetValue(key);
                return BehaviorTreeValueUtility.TryGetVector3(blackboardValue, out value);
            }

            object directValue;
            if (properties != null && properties.TryGetValue(valueProperty, out directValue))
            {
                return BehaviorTreeValueUtility.TryGetVector3(directValue, out value);
            }

            return false;
        }

        private static bool TryResolveDecoratorValue(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeDecorator decorator,
            string inputId,
            string keyProperty,
            string valueProperty,
            object defaultValue,
            out object value)
        {
            if (TryGetInputValue(context, decorator, inputId, out value))
            {
                return true;
            }

            Dictionary<string, object> properties = decorator == null ? null : decorator.Properties;
            if (!string.IsNullOrEmpty(keyProperty))
            {
                string blackboardKey = GetString(properties, keyProperty, null);
                if (!string.IsNullOrEmpty(blackboardKey))
                {
                    value = context == null || context.Blackboard == null
                        ? defaultValue
                        : context.Blackboard.GetValue(blackboardKey);
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(valueProperty) && properties != null && properties.TryGetValue(valueProperty, out value))
            {
                return true;
            }

            value = defaultValue;
            return false;
        }
    }

    internal static class BehaviorTreeComparisonUtility
    {
        public static BehaviorTreeComparisonOperator GetOperator(
            Dictionary<string, object> properties,
            string key,
            BehaviorTreeComparisonOperator defaultValue)
        {
            object value;
            if (properties == null || string.IsNullOrEmpty(key) || !properties.TryGetValue(key, out value))
            {
                return defaultValue;
            }

            return ConvertOperator(value, defaultValue);
        }

        public static BehaviorTreeComparisonOperator ConvertOperator(object value, BehaviorTreeComparisonOperator defaultValue)
        {
            if (value is BehaviorTreeComparisonOperator)
            {
                return (BehaviorTreeComparisonOperator)value;
            }

            string text = value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(text))
            {
                return defaultValue;
            }

            try
            {
                return (BehaviorTreeComparisonOperator)Enum.Parse(typeof(BehaviorTreeComparisonOperator), text, false);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static bool Evaluate(BehaviorTreeComparisonOperator op, object actual, object expected)
        {
            switch (op)
            {
                case BehaviorTreeComparisonOperator.IsSet:
                    return actual != null;
                case BehaviorTreeComparisonOperator.IsNotSet:
                    return actual == null;
                case BehaviorTreeComparisonOperator.IsTrue:
                    return actual != null && BlueprintTypeUtility.ConvertValue(actual, false);
                case BehaviorTreeComparisonOperator.IsFalse:
                    return actual == null || !BlueprintTypeUtility.ConvertValue(actual, false);
                case BehaviorTreeComparisonOperator.Equals:
                    return AreEqual(actual, expected);
                case BehaviorTreeComparisonOperator.NotEquals:
                    return !AreEqual(actual, expected);
                case BehaviorTreeComparisonOperator.Greater:
                case BehaviorTreeComparisonOperator.GreaterOrEqual:
                case BehaviorTreeComparisonOperator.Less:
                case BehaviorTreeComparisonOperator.LessOrEqual:
                    return CompareFloats(ToFloat(actual), ToFloat(expected), op);
                default:
                    return actual != null;
            }
        }

        public static bool CompareFloats(float left, float right, BehaviorTreeComparisonOperator op)
        {
            switch (op)
            {
                case BehaviorTreeComparisonOperator.Greater:
                    return left > right;
                case BehaviorTreeComparisonOperator.GreaterOrEqual:
                    return left >= right;
                case BehaviorTreeComparisonOperator.Less:
                    return left < right;
                case BehaviorTreeComparisonOperator.LessOrEqual:
                    return left <= right;
                case BehaviorTreeComparisonOperator.NotEquals:
                    return Math.Abs(left - right) > 0.0001f;
                case BehaviorTreeComparisonOperator.Equals:
                default:
                    return Math.Abs(left - right) <= 0.0001f;
            }
        }

        private static bool AreEqual(object left, object right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left is string || right is string)
            {
                return string.Equals(
                    Convert.ToString(left, CultureInfo.InvariantCulture),
                    Convert.ToString(right, CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);
            }

            if (IsNumeric(left) || IsNumeric(right))
            {
                return Math.Abs(ToFloat(left) - ToFloat(right)) <= 0.0001f;
            }

            return left.Equals(right);
        }

        private static float ToFloat(object value)
        {
            return value == null ? 0f : BlueprintTypeUtility.ConvertValue(value, 0f);
        }

        private static bool IsNumeric(object value)
        {
            return value is byte ||
                   value is sbyte ||
                   value is short ||
                   value is ushort ||
                   value is int ||
                   value is uint ||
                   value is long ||
                   value is ulong ||
                   value is float ||
                   value is double ||
                   value is decimal;
        }
    }

    internal static class BehaviorTreeRunnerBlackboardUtility
    {
        public static bool TryResolveRunnerBlackboard(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            string executorId,
            out BehaviorTreeBlackboard blackboard)
        {
            blackboard = null;
            object targetValue;
            if (!BehaviorTreePropertyUtility.TryGetInputValue(context, node, inputId, out targetValue))
            {
                context.Runtime.MarkFailure(executorId + " requires " + inputId + " input.");
                return false;
            }

            BehaviorTreeRunner runner = ResolveRunner(targetValue);
            if (runner == null)
            {
                context.Runtime.MarkFailure(executorId + " could not resolve BehaviorTreeRunner from " + inputId + ".");
                return false;
            }

            blackboard = runner.Blackboard;
            if (blackboard == null)
            {
                context.Runtime.MarkFailure(executorId + " target BehaviorTreeRunner has no initialized Blackboard.");
                return false;
            }

            return true;
        }

        private static BehaviorTreeRunner ResolveRunner(object value)
        {
            BehaviorTreeRunner runner = value as BehaviorTreeRunner;
            if (runner != null)
            {
                return runner;
            }

            GameObject gameObject = BehaviorTreeValueUtility.ToGameObject(value);
            return gameObject == null ? null : gameObject.GetComponent<BehaviorTreeRunner>();
        }
    }

    internal static class BehaviorTreeBlueprintTargetUtility
    {
        public static IBlueprintInstance ResolveTarget(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node, bool logWarnings)
        {
            object targetValue;
            if (BehaviorTreePropertyUtility.TryGetInputValue(context, node, "target", out targetValue))
            {
                return ResolveTargetValue(context, targetValue, logWarnings);
            }

            return ResolveTarget(context, node == null ? null : node.Properties, logWarnings);
        }

        public static IBlueprintInstance ResolveTarget(BehaviorTreeExecutionContext context, Dictionary<string, object> properties, bool logWarnings)
        {
            object targetValue = null;
            string targetKey = BehaviorTreePropertyUtility.GetString(properties, "targetKey", null);
            if (!string.IsNullOrEmpty(targetKey))
            {
                context.Blackboard.TryGetValue(targetKey, out targetValue);
            }
            else if (properties != null && properties.ContainsKey("target"))
            {
                targetValue = properties["target"];
            }
            else if (properties != null && properties.ContainsKey("targetBlueprint"))
            {
                targetValue = properties["targetBlueprint"];
            }

            if (targetValue == null)
            {
                return ResolveDefaultOwner(context);
            }

            return ResolveTargetValue(context, targetValue, logWarnings);
        }

        private static IBlueprintInstance ResolveTargetValue(BehaviorTreeExecutionContext context, object targetValue, bool logWarnings)
        {
            if (targetValue == null)
            {
                return ResolveDefaultOwner(context);
            }

            BlueprintRef blueprintRef = targetValue as BlueprintRef;
            if (blueprintRef != null)
            {
                return blueprintRef.Instance;
            }

            IBlueprintInstance instance = targetValue as IBlueprintInstance;
            if (instance != null)
            {
                return instance;
            }

            GameObject gameObject = BehaviorTreeValueUtility.ToGameObject(targetValue);
            if (gameObject != null)
            {
                BlueprintRunner runner = gameObject.GetComponent<BlueprintRunner>();
                if (runner != null)
                {
                    return runner;
                }
            }

            string targetPath = targetValue as string;
            if (!string.IsNullOrEmpty(targetPath))
            {
                IBlueprintInstance root = ResolveRoot(context);
                if (root != null)
                {
                    List<IBlueprintInstance> matches = new List<IBlueprintInstance>();
                    CollectMatchingTargets(root, NormalizePath(targetPath), matches);
                    if (matches.Count == 1)
                    {
                        return matches[0];
                    }

                    if (matches.Count > 1 && logWarnings)
                    {
                        context.Logger.Warning("Behavior tree target '" + targetPath + "' matched multiple Blueprint instances.");
                    }
                }
            }

            if (logWarnings)
            {
                context.Logger.Warning("Behavior tree could not resolve Blueprint target.");
            }

            return null;
        }

        private static IBlueprintInstance ResolveDefaultOwner(BehaviorTreeExecutionContext context)
        {
            IBlueprintInstance instance = context.OwnerComponent as IBlueprintInstance;
            if (instance != null)
            {
                return instance;
            }

            return context.Owner == null ? null : context.Owner.GetComponent<BlueprintRunner>();
        }

        private static IBlueprintInstance ResolveRoot(BehaviorTreeExecutionContext context)
        {
            IBlueprintInstance instance = ResolveDefaultOwner(context);
            while (instance != null && instance.OwnerInstance != null)
            {
                instance = instance.OwnerInstance;
            }

            return instance;
        }

        private static void CollectMatchingTargets(IBlueprintInstance instance, string targetPath, List<IBlueprintInstance> matches)
        {
            if (instance == null)
            {
                return;
            }

            if (PathEquals(instance.SourcePath, targetPath))
            {
                AddUnique(matches, instance);
            }

            RuntimeBlueprint blueprint = instance.RuntimeBlueprint;
            if (blueprint == null)
            {
                return;
            }

            for (int i = 0; i < blueprint.Components.Count; i++)
            {
                BlueprintComponentDeclaration declaration = blueprint.Components[i];
                if (declaration == null || string.IsNullOrEmpty(declaration.Name))
                {
                    continue;
                }

                IBlueprintInstance component;
                if (instance.TryGetBlueprintComponent(declaration.Name, out component) && component != null)
                {
                    if (PathEquals(declaration.Blueprint, targetPath))
                    {
                        AddUnique(matches, component);
                    }

                    CollectMatchingTargets(component, targetPath, matches);
                }
            }
        }

        private static void AddUnique(List<IBlueprintInstance> matches, IBlueprintInstance instance)
        {
            if (instance != null && !matches.Contains(instance))
            {
                matches.Add(instance);
            }
        }

        private static bool PathEquals(string left, string right)
        {
            left = NormalizePath(left);
            right = NormalizePath(right);
            return !string.IsNullOrEmpty(left) &&
                   !string.IsNullOrEmpty(right) &&
                   string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/').Trim();
        }
    }
}
