using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.AI;

namespace BlueprintSystem
{
    public enum BehaviorTreeNavigationCondition
    {
        AgentAvailable,
        IsOnNavMesh,
        HasPath,
        PathPending,
        PathComplete,
        PathPartial,
        PathInvalid,
        IsStopped,
        IsMoving,
        HasArrived,
        IsPathStale,
        IsOnOffMeshLink
    }

    public enum BehaviorTreeOffMeshLinkTraversalMode
    {
        Teleport,
        Linear,
        Parabola
    }

    internal sealed class BehaviorTreeSetNavigationDestinationImplementation : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.SetNavigationDestination"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            NavMeshAgent agent;
            if (!BehaviorTreeNavigationUtility.TryGetUsableAgent(context, TypeId, out agent))
            {
                return BehaviorTreeStatus.Failure;
            }

            Vector3 destination;
            if (!BehaviorTreeNavigationUtility.TryResolveTarget(context, node, out destination))
            {
                context.Runtime.MarkFailure(TypeId + " could not resolve a target.");
                return BehaviorTreeStatus.Failure;
            }

            if (!agent.SetDestination(destination))
            {
                context.Runtime.MarkFailure(TypeId + " destination request was rejected.");
                return BehaviorTreeStatus.Failure;
            }

            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeCalculateNavigationPathImplementation : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.CalculateNavigationPath"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            NavMeshAgent agent;
            if (!BehaviorTreeNavigationUtility.TryGetUsableAgent(context, TypeId, out agent))
            {
                return BehaviorTreeStatus.Failure;
            }

            Vector3 destination;
            if (!BehaviorTreeNavigationUtility.TryResolveTarget(context, node, out destination))
            {
                context.Runtime.MarkFailure(TypeId + " could not resolve a target.");
                return BehaviorTreeStatus.Failure;
            }

            string pathKey = BehaviorTreePropertyUtility.GetString(node.Properties, "pathKey", null);
            if (!BehaviorTreeNavigationUtility.IsDeclaredBlackboardKey(context, pathKey))
            {
                context.Runtime.MarkFailure(TypeId + " requires a declared pathKey.");
                return BehaviorTreeStatus.Failure;
            }

            NavMeshPath path = new NavMeshPath();
            bool found = agent.CalculatePath(destination, path);
            context.Blackboard.SetValue(pathKey, path);

            bool allowPartial = BehaviorTreePropertyUtility.ResolveBool(context, node, "allowPartial", "allowPartial", false);
            if (!found ||
                path.status == NavMeshPathStatus.PathInvalid ||
                (path.status == NavMeshPathStatus.PathPartial && !allowPartial))
            {
                context.Runtime.MarkFailure(TypeId + " path is " + path.status + ".");
                return BehaviorTreeStatus.Failure;
            }

            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeSetNavigationPathImplementation : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.SetNavigationPath"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            NavMeshAgent agent;
            if (!BehaviorTreeNavigationUtility.TryGetUsableAgent(context, TypeId, out agent))
            {
                return BehaviorTreeStatus.Failure;
            }

            NavMeshPath path;
            if (!BehaviorTreeNavigationUtility.TryResolvePath(context, node, out path))
            {
                context.Runtime.MarkFailure(TypeId + " could not resolve a NavMeshPath.");
                return BehaviorTreeStatus.Failure;
            }

            bool allowPartial = BehaviorTreePropertyUtility.ResolveBool(context, node, "allowPartial", "allowPartial", false);
            if (path.status == NavMeshPathStatus.PathInvalid ||
                (path.status == NavMeshPathStatus.PathPartial && !allowPartial))
            {
                context.Runtime.MarkFailure(TypeId + " path is " + path.status + ".");
                return BehaviorTreeStatus.Failure;
            }

            if (!agent.SetPath(path))
            {
                context.Runtime.MarkFailure(TypeId + " could not assign the path.");
                return BehaviorTreeStatus.Failure;
            }

            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeWaitForNavigationImplementation : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.WaitForNavigation"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            NavMeshAgent agent;
            if (!BehaviorTreeNavigationUtility.TryGetUsableAgent(context, TypeId, out agent))
            {
                return BehaviorTreeStatus.Failure;
            }

            object speedValue;
            if (BehaviorTreePropertyUtility.TryResolveValue(context, node, "speed", "speed", out speedValue) &&
                speedValue != null)
            {
                float speed = BlueprintTypeUtility.ConvertValue(speedValue, -1f);
                if (speed >= 0f && !float.IsInfinity(speed))
                {
                    agent.speed = speed;
                }
            }

            float acceptableRadius = Mathf.Max(0f,
                BehaviorTreePropertyUtility.ResolveFloat(context, node, "acceptableRadius", "acceptableRadius", 0.25f));
            float velocityThreshold = Mathf.Max(0f,
                BehaviorTreePropertyUtility.ResolveFloat(context, node, "velocityThreshold", "velocityThreshold", 0.05f));
            BehaviorTreeNavigationState state = BehaviorTreeNavigationUtility.ReadState(agent, acceptableRadius, velocityThreshold);

            if (state.PathPending)
            {
                return BehaviorTreeStatus.Running;
            }

            if (!state.HasPath)
            {
                context.Runtime.MarkFailure(TypeId + " requires an active path.");
                return BehaviorTreeStatus.Failure;
            }

            if (state.IsStopped)
            {
                return BehaviorTreeStatus.Running;
            }

            if (state.IsPathStale || state.PathStatus != NavMeshPathStatus.PathComplete)
            {
                context.Runtime.MarkFailure(TypeId + " path is " +
                    (state.IsPathStale ? "stale" : state.PathStatus.ToString()) + ".");
                return BehaviorTreeStatus.Failure;
            }

            return state.HasArrived ? BehaviorTreeStatus.Success : BehaviorTreeStatus.Running;
        }
    }

    internal sealed class BehaviorTreePauseNavigationImplementation : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.PauseNavigation"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            NavMeshAgent agent;
            if (!BehaviorTreeNavigationUtility.TryGetUsableAgent(context, TypeId, out agent))
            {
                return BehaviorTreeStatus.Failure;
            }

            agent.isStopped = true;
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeResumeNavigationImplementation : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.ResumeNavigation"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            NavMeshAgent agent;
            if (!BehaviorTreeNavigationUtility.TryGetUsableAgent(context, TypeId, out agent))
            {
                return BehaviorTreeStatus.Failure;
            }

            agent.isStopped = false;
            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeSampleNavMeshPositionImplementation : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.SampleNavMeshPosition"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            NavMeshAgent agent;
            if (!BehaviorTreeNavigationUtility.TryGetEnabledAgent(context, TypeId, out agent))
            {
                return BehaviorTreeStatus.Failure;
            }

            string positionKey = BehaviorTreePropertyUtility.GetString(node.Properties, "positionKey", null);
            if (!BehaviorTreeNavigationUtility.IsDeclaredBlackboardKey(context, positionKey))
            {
                context.Runtime.MarkFailure(TypeId + " requires a declared positionKey.");
                return BehaviorTreeStatus.Failure;
            }

            string areaMaskKey = BehaviorTreePropertyUtility.GetString(node.Properties, "areaMaskKey", null);
            if (!string.IsNullOrEmpty(areaMaskKey) &&
                !BehaviorTreeNavigationUtility.IsDeclaredBlackboardKey(context, areaMaskKey))
            {
                context.Runtime.MarkFailure(TypeId + " areaMaskKey is not declared.");
                return BehaviorTreeStatus.Failure;
            }

            Vector3 source;
            if (!BehaviorTreePropertyUtility.TryResolveVector3(context, node, "source", "sourceKey", "sourcePosition", out source))
            {
                source = context.Owner.transform.position;
            }

            float defaultDistance = Mathf.Max(0.01f, agent.height * 2f);
            float maxDistance = Mathf.Max(0f,
                BehaviorTreePropertyUtility.ResolveFloat(context, node, "maxDistance", "maxDistance", defaultDistance));
            int areaMask = BehaviorTreeNavigationUtility.ResolveInt(context, node, "areaMask", "areaMask", -1);
            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = areaMask
            };

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(source, out hit, maxDistance, filter))
            {
                context.Runtime.MarkFailure(TypeId + " could not find a NavMesh position.");
                return BehaviorTreeStatus.Failure;
            }

            context.Blackboard.SetValue(positionKey, hit.position);
            if (!string.IsNullOrEmpty(areaMaskKey))
            {
                context.Blackboard.SetValue(areaMaskKey, hit.mask);
            }

            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeWarpNavigationImplementation : BehaviorTreeNodeExecutor
    {
        public override string TypeId
        {
            get { return "BT.WarpNavigation"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            NavMeshAgent agent;
            if (!BehaviorTreeNavigationUtility.TryGetEnabledAgent(context, TypeId, out agent))
            {
                return BehaviorTreeStatus.Failure;
            }

            Vector3 destination;
            if (!BehaviorTreeNavigationUtility.TryResolveTarget(context, node, out destination))
            {
                context.Runtime.MarkFailure(TypeId + " could not resolve a target.");
                return BehaviorTreeStatus.Failure;
            }

            if (!agent.Warp(destination))
            {
                context.Runtime.MarkFailure(TypeId + " could not warp to the target.");
                return BehaviorTreeStatus.Failure;
            }

            return BehaviorTreeStatus.Success;
        }
    }

    internal sealed class BehaviorTreeTraverseOffMeshLinkImplementation : BehaviorTreeNodeExecutor
    {
        private const string StartPositionKey = "startPosition";
        private const string EndPositionKey = "endPosition";
        private const string ElapsedKey = "elapsed";
        private const string AutoTraverseKey = "autoTraverse";

        public override string TypeId
        {
            get { return "BT.TraverseOffMeshLink"; }
        }

        public override BehaviorTreeStatus Tick(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            NavMeshAgent agent;
            if (!BehaviorTreeNavigationUtility.TryGetUsableAgent(context, TypeId, out agent))
            {
                if (state.Data.ContainsKey(StartPositionKey))
                {
                    AbortTraversal(context, agent, state);
                }

                return BehaviorTreeStatus.Failure;
            }

            if (!state.Data.ContainsKey(StartPositionKey))
            {
                if (!agent.isOnOffMeshLink)
                {
                    context.Runtime.MarkFailure(TypeId + " requires the agent to be on an OffMeshLink.");
                    return BehaviorTreeStatus.Failure;
                }

                OffMeshLinkData data = agent.currentOffMeshLinkData;
                state.Data[StartPositionKey] = agent.transform.position;
                state.Data[EndPositionKey] = data.endPos + Vector3.up * agent.baseOffset;
                state.Data[ElapsedKey] = 0f;
                state.Data[AutoTraverseKey] = agent.autoTraverseOffMeshLink;
                agent.autoTraverseOffMeshLink = false;
            }

            Vector3 start = (Vector3)state.Data[StartPositionKey];
            Vector3 end = (Vector3)state.Data[EndPositionKey];
            BehaviorTreeOffMeshLinkTraversalMode mode = BehaviorTreeNavigationUtility.ResolveTraversalMode(context, node);
            if (mode == BehaviorTreeOffMeshLinkTraversalMode.Teleport)
            {
                agent.transform.position = end;
                Complete(agent, state);
                return BehaviorTreeStatus.Success;
            }

            if (!agent.isOnOffMeshLink)
            {
                AbortTraversal(context, agent, state);
                context.Runtime.MarkFailure(TypeId + " lost the active OffMeshLink.");
                return BehaviorTreeStatus.Failure;
            }

            float duration = Mathf.Max(0f,
                BehaviorTreePropertyUtility.ResolveFloat(context, node, "duration", "duration", 0.5f));
            float height = Mathf.Max(0f,
                BehaviorTreePropertyUtility.ResolveFloat(context, node, "height", "height", 1f));
            float elapsed = Convert.ToSingle(state.Data[ElapsedKey], CultureInfo.InvariantCulture) + Mathf.Max(0f, context.DeltaTime);
            state.Data[ElapsedKey] = elapsed;
            float normalizedTime = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(start, end, normalizedTime);
            if (mode == BehaviorTreeOffMeshLinkTraversalMode.Parabola)
            {
                position += Vector3.up * (height * 4f * normalizedTime * (1f - normalizedTime));
            }

            agent.transform.position = position;
            if (normalizedTime < 1f)
            {
                return BehaviorTreeStatus.Running;
            }

            agent.transform.position = end;
            Complete(agent, state);
            return BehaviorTreeStatus.Success;
        }

        public override void Abort(BehaviorTreeExecutionContext context, RuntimeBehaviorTreeNode node)
        {
            BehaviorTreeNodeRuntimeState state = context.GetNodeState(node);
            if (context.Owner == null || !state.Data.ContainsKey(StartPositionKey))
            {
                state.Data.Clear();
                return;
            }

            NavMeshAgent agent = context.Owner.GetComponent<NavMeshAgent>();
            AbortTraversal(context, agent, state);
        }

        private static void Complete(NavMeshAgent agent, BehaviorTreeNodeRuntimeState state)
        {
            if (agent.isOnOffMeshLink)
            {
                agent.CompleteOffMeshLink();
            }

            RestoreAutoTraverse(agent, state);
            state.Data.Clear();
        }

        private static void RestoreAutoTraverse(NavMeshAgent agent, BehaviorTreeNodeRuntimeState state)
        {
            if (agent == null)
            {
                return;
            }

            object value;
            if (state.Data.TryGetValue(AutoTraverseKey, out value))
            {
                agent.autoTraverseOffMeshLink = BlueprintTypeUtility.ConvertValue(value, true);
            }
        }

        private static void AbortTraversal(
            BehaviorTreeExecutionContext context,
            NavMeshAgent agent,
            BehaviorTreeNodeRuntimeState state)
        {
            Vector3 start = (Vector3)state.Data[StartPositionKey];
            if (context.Owner != null)
            {
                context.Owner.transform.position = start;
            }

            RestoreAutoTraverse(agent, state);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            state.Data.Clear();
        }
    }

    internal sealed class BehaviorTreeNavigationConditionImplementation : BehaviorTreeDecoratorExecutor
    {
        public override string TypeId
        {
            get { return "BT.NavigationCondition"; }
        }

        public override bool Evaluate(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            RuntimeBehaviorTreeDecorator decorator)
        {
            NavMeshAgent agent = context.Owner == null ? null : context.Owner.GetComponent<NavMeshAgent>();
            bool available = agent != null && agent.enabled;
            BehaviorTreeNavigationCondition condition =
                BehaviorTreeNavigationUtility.ResolveCondition(context, decorator);
            bool result;

            if (condition == BehaviorTreeNavigationCondition.AgentAvailable)
            {
                result = available;
            }
            else if (condition == BehaviorTreeNavigationCondition.IsOnNavMesh)
            {
                result = available && agent.isOnNavMesh;
            }
            else if (!available || !agent.isOnNavMesh)
            {
                result = false;
            }
            else
            {
                float acceptableRadius = Mathf.Max(0f,
                    BehaviorTreePropertyUtility.ResolveFloat(
                        context,
                        decorator,
                        "acceptableRadius",
                        null,
                        "acceptableRadius",
                        0.25f));
                float velocityThreshold = Mathf.Max(0f,
                    BehaviorTreePropertyUtility.ResolveFloat(
                        context,
                        decorator,
                        "velocityThreshold",
                        null,
                        "velocityThreshold",
                        0.05f));
                BehaviorTreeNavigationState state =
                    BehaviorTreeNavigationUtility.ReadState(agent, acceptableRadius, velocityThreshold);
                result = BehaviorTreeNavigationUtility.EvaluateCondition(state, condition);
            }

            bool invert = BehaviorTreePropertyUtility.ResolveBool(
                context,
                decorator,
                "invert",
                null,
                "invert",
                false);
            return invert ? !result : result;
        }
    }

    internal sealed class BehaviorTreeUpdateNavigationStateImplementation : BehaviorTreeServiceExecutor
    {
        public override string TypeId
        {
            get { return "BT.UpdateNavigationState"; }
        }

        public override void Tick(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            RuntimeBehaviorTreeService service)
        {
            NavMeshAgent agent = context.Owner == null ? null : context.Owner.GetComponent<NavMeshAgent>();
            bool available = agent != null && agent.enabled;
            float acceptableRadius = Mathf.Max(0f,
                BehaviorTreePropertyUtility.GetFloat(service.Properties, "acceptableRadius", 0.25f));
            float velocityThreshold = Mathf.Max(0f,
                BehaviorTreePropertyUtility.GetFloat(service.Properties, "velocityThreshold", 0.05f));
            BehaviorTreeNavigationState state = available && agent.isOnNavMesh
                ? BehaviorTreeNavigationUtility.ReadState(agent, acceptableRadius, velocityThreshold)
                : BehaviorTreeNavigationState.CreateInvalid(available);

            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "agentAvailableKey", state.AgentAvailable);
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "isOnNavMeshKey", state.IsOnNavMesh);
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "hasPathKey", state.HasPath);
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "pathPendingKey", state.PathPending);
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "pathStatusKey", state.PathStatus.ToString());
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "remainingDistanceKey", state.RemainingDistance);
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "velocityKey", state.Velocity);
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "destinationKey", state.Destination);
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "isStoppedKey", state.IsStopped);
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "isMovingKey", state.IsMoving);
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "hasArrivedKey", state.HasArrived);
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "isPathStaleKey", state.IsPathStale);
            BehaviorTreeNavigationUtility.WriteServiceValue(
                context, service.Properties, "isOnOffMeshLinkKey", state.IsOnOffMeshLink);
        }
    }

    internal struct BehaviorTreeNavigationState
    {
        public bool AgentAvailable;
        public bool IsOnNavMesh;
        public bool HasPath;
        public bool PathPending;
        public NavMeshPathStatus PathStatus;
        public float RemainingDistance;
        public Vector3 Velocity;
        public Vector3 Destination;
        public bool IsStopped;
        public bool IsMoving;
        public bool HasArrived;
        public bool IsPathStale;
        public bool IsOnOffMeshLink;

        public static BehaviorTreeNavigationState CreateInvalid(bool available)
        {
            return new BehaviorTreeNavigationState
            {
                AgentAvailable = available,
                IsOnNavMesh = false,
                HasPath = false,
                PathPending = false,
                PathStatus = NavMeshPathStatus.PathInvalid,
                RemainingDistance = float.PositiveInfinity,
                Velocity = Vector3.zero,
                Destination = Vector3.zero,
                IsStopped = true,
                IsMoving = false,
                HasArrived = false,
                IsPathStale = false,
                IsOnOffMeshLink = false
            };
        }
    }

    internal static class BehaviorTreeNavigationUtility
    {
        public static bool TryGetEnabledAgent(
            BehaviorTreeExecutionContext context,
            string typeId,
            out NavMeshAgent agent)
        {
            agent = context.Owner == null ? null : context.Owner.GetComponent<NavMeshAgent>();
            if (context.Owner == null)
            {
                context.Runtime.MarkFailure(typeId + " requires an owner GameObject.");
                return false;
            }

            if (agent == null || !agent.enabled)
            {
                context.Runtime.MarkFailure(typeId + " requires an enabled NavMeshAgent.");
                return false;
            }

            return true;
        }

        public static bool TryGetUsableAgent(
            BehaviorTreeExecutionContext context,
            string typeId,
            out NavMeshAgent agent)
        {
            if (!TryGetEnabledAgent(context, typeId, out agent))
            {
                return false;
            }

            if (!agent.isOnNavMesh)
            {
                context.Runtime.MarkFailure(typeId + " requires the NavMeshAgent to be on the NavMesh.");
                return false;
            }

            return true;
        }

        public static bool TryResolveTarget(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            out Vector3 destination)
        {
            return BehaviorTreePropertyUtility.TryResolveVector3(
                context, node, "target", "targetKey", "targetPosition", out destination);
        }

        public static bool TryResolvePath(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            out NavMeshPath path)
        {
            path = null;
            object value;
            if (BehaviorTreePropertyUtility.TryResolveValue(context, node, "path", "path", out value))
            {
                path = value as NavMeshPath;
                if (path != null)
                {
                    return true;
                }
            }

            string pathKey = BehaviorTreePropertyUtility.GetString(node.Properties, "pathKey", null);
            path = string.IsNullOrEmpty(pathKey) ? null : context.Blackboard.GetValue(pathKey) as NavMeshPath;
            return path != null;
        }

        public static bool IsDeclaredBlackboardKey(BehaviorTreeExecutionContext context, string key)
        {
            return !string.IsNullOrEmpty(key) &&
                   context.Blackboard != null &&
                   context.Blackboard.ContainsKey(key);
        }

        public static int ResolveInt(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node,
            string inputId,
            string propertyKey,
            int defaultValue)
        {
            object value;
            if (BehaviorTreePropertyUtility.TryResolveValue(context, node, inputId, propertyKey, out value) &&
                value != null)
            {
                return BlueprintTypeUtility.ConvertValue(value, defaultValue);
            }

            return defaultValue;
        }

        public static BehaviorTreeNavigationState ReadState(
            NavMeshAgent agent,
            float acceptableRadius,
            float velocityThreshold)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return BehaviorTreeNavigationState.CreateInvalid(agent != null && agent.enabled);
            }

            float remainingDistance = agent.remainingDistance;
            bool finiteDistance = !float.IsInfinity(remainingDistance) && !float.IsNaN(remainingDistance);
            float arrivalRadius = Mathf.Max(agent.stoppingDistance, acceptableRadius);
            float velocityLimit = velocityThreshold * velocityThreshold;
            Vector3 velocity = agent.velocity;
            bool hasArrived = agent.hasPath &&
                              !agent.pathPending &&
                              !agent.isPathStale &&
                              agent.pathStatus == NavMeshPathStatus.PathComplete &&
                              finiteDistance &&
                              remainingDistance <= arrivalRadius &&
                              velocity.sqrMagnitude <= velocityLimit;

            return new BehaviorTreeNavigationState
            {
                AgentAvailable = true,
                IsOnNavMesh = true,
                HasPath = agent.hasPath,
                PathPending = agent.pathPending,
                PathStatus = agent.pathStatus,
                RemainingDistance = finiteDistance ? remainingDistance : float.PositiveInfinity,
                Velocity = velocity,
                Destination = agent.destination,
                IsStopped = agent.isStopped,
                IsMoving = !agent.isStopped && velocity.sqrMagnitude > velocityLimit,
                HasArrived = hasArrived,
                IsPathStale = agent.isPathStale,
                IsOnOffMeshLink = agent.isOnOffMeshLink
            };
        }

        public static bool EvaluateCondition(
            BehaviorTreeNavigationState state,
            BehaviorTreeNavigationCondition condition)
        {
            switch (condition)
            {
                case BehaviorTreeNavigationCondition.AgentAvailable:
                    return state.AgentAvailable;
                case BehaviorTreeNavigationCondition.IsOnNavMesh:
                    return state.IsOnNavMesh;
                case BehaviorTreeNavigationCondition.HasPath:
                    return state.HasPath;
                case BehaviorTreeNavigationCondition.PathPending:
                    return state.PathPending;
                case BehaviorTreeNavigationCondition.PathComplete:
                    return state.PathStatus == NavMeshPathStatus.PathComplete;
                case BehaviorTreeNavigationCondition.PathPartial:
                    return state.PathStatus == NavMeshPathStatus.PathPartial;
                case BehaviorTreeNavigationCondition.PathInvalid:
                    return state.PathStatus == NavMeshPathStatus.PathInvalid;
                case BehaviorTreeNavigationCondition.IsStopped:
                    return state.IsStopped;
                case BehaviorTreeNavigationCondition.IsMoving:
                    return state.IsMoving;
                case BehaviorTreeNavigationCondition.HasArrived:
                    return state.HasArrived;
                case BehaviorTreeNavigationCondition.IsPathStale:
                    return state.IsPathStale;
                case BehaviorTreeNavigationCondition.IsOnOffMeshLink:
                    return state.IsOnOffMeshLink;
                default:
                    return false;
            }
        }

        public static BehaviorTreeNavigationCondition ResolveCondition(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeDecorator decorator)
        {
            object value;
            if (!BehaviorTreePropertyUtility.TryResolveValue(context, decorator, "condition", "condition", out value) ||
                value == null)
            {
                return BehaviorTreeNavigationCondition.AgentAvailable;
            }

            if (value is BehaviorTreeNavigationCondition)
            {
                return (BehaviorTreeNavigationCondition)value;
            }

            BehaviorTreeNavigationCondition condition;
            return Enum.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), true, out condition)
                ? condition
                : BehaviorTreeNavigationCondition.AgentAvailable;
        }

        public static BehaviorTreeOffMeshLinkTraversalMode ResolveTraversalMode(
            BehaviorTreeExecutionContext context,
            RuntimeBehaviorTreeNode node)
        {
            object value;
            if (!BehaviorTreePropertyUtility.TryResolveValue(context, node, "mode", "mode", out value) ||
                value == null)
            {
                return BehaviorTreeOffMeshLinkTraversalMode.Linear;
            }

            if (value is BehaviorTreeOffMeshLinkTraversalMode)
            {
                return (BehaviorTreeOffMeshLinkTraversalMode)value;
            }

            BehaviorTreeOffMeshLinkTraversalMode mode;
            return Enum.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), true, out mode)
                ? mode
                : BehaviorTreeOffMeshLinkTraversalMode.Linear;
        }

        public static void WriteServiceValue(
            BehaviorTreeExecutionContext context,
            System.Collections.Generic.Dictionary<string, object> properties,
            string keyProperty,
            object value)
        {
            string key = BehaviorTreePropertyUtility.GetString(properties, keyProperty, null);
            if (!string.IsNullOrEmpty(key) && context.Blackboard.ContainsKey(key))
            {
                context.Blackboard.SetValue(key, value);
            }
        }
    }
}
