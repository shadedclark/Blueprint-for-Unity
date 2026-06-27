using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using VehicleRoads;

namespace BlueprintSystem
{
    public static class VehicleRoadExecutorRegistrar
    {
        public static void Register(BlueprintExecutorRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            registry.Register(new VehicleRoadFindNearestLaneExecutor());
            registry.Register(new VehicleRoadFindLaneRouteExecutor());
            registry.Register(new VehicleRoadGetLaneIdsExecutor());
            registry.Register(new VehicleRoadGetRouteCandidateLaneIdsExecutor());
            registry.Register(new VehicleRoadFindSpawnLaneAroundTransformExecutor());
            registry.Register(new VehicleRoadSelectReachableRouteTargetExecutor());
            registry.Register(new VehicleRoadFilterLaneIdsExecutor());
            registry.Register(new VehicleRoadGetLaneInfoExecutor());
            registry.Register(new VehicleRoadSetLaneClosedExecutor());
            registry.Register(new VehicleRoadSetLaneCongestionCostExecutor());
            registry.Register(new VehicleRoadUpdateVehicleExecutor());
            registry.Register(new VehicleRoadUnregisterVehicleExecutor());
            registry.Register(new VehicleRoadEvaluateTrafficControlExecutor());
            registry.Register(new VehicleRoadEvaluateLaneOccupancyExecutor());
            registry.Register(new VehicleRoadEvaluateLaneChangeRouteExecutor());
            registry.Register(new VehicleRoadRequestLaneChangeExecutor());
            registry.Register(new VehicleRoadCompleteLaneChangeExecutor());
            registry.Register(new VehicleRoadSetFollowerRouteExecutor());
            registry.Register(new VehicleRoadComputeFollowerControlExecutor());
            registry.Register(new VehicleRoadGetSubsystemSnapshotExecutor());
        }
    }

    public sealed class VehicleRoadFindNearestLaneExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.FindNearestLane"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            if (subsystem == null)
            {
                return VehicleRoadExecutorUtility.ReadNearestResult(false, default, outputPortId);
            }

            VehicleRoadNearestResult result;
            bool found = subsystem.TryFindNearestLane(
                GameExecutorValueUtility.GetVector3Input(context, node, "position", Vector3.zero),
                GameExecutorValueUtility.GetVector3Input(context, node, "heading", Vector3.forward),
                VehicleRoadExecutorUtility.GetAgentMask(context, node),
                context.GetInputValue(node, "maxDistance", 0f),
                context.GetInputValue(node, "maxHeightDifference", 0f),
                out result);
            return VehicleRoadExecutorUtility.ReadNearestResult(found, result, outputPortId);
        }
    }

    public sealed class VehicleRoadFindLaneRouteExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.FindLaneRoute"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            if (subsystem == null)
            {
                return VehicleRoadExecutorUtility.ReadRouteResult(false, null, outputPortId);
            }

            VehicleRoadRouteResult result;
            bool success = subsystem.TryFindRoute(
                new LaneRouteQuery(
                    context.GetInputValue(node, "startLaneId", string.Empty),
                    context.GetInputValue(node, "destinationLaneId", string.Empty),
                    VehicleRoadExecutorUtility.GetAgentMask(context, node)),
                out result);
            return VehicleRoadExecutorUtility.ReadRouteResult(success, result, outputPortId);
        }
    }

    public sealed class VehicleRoadGetLaneIdsExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.GetLaneIds"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            List<string> laneIds = subsystem == null
                ? new List<string>()
                : subsystem.GetLaneIds(new VehicleRoadLaneQuery
                {
                    agentMask = VehicleRoadExecutorUtility.GetAgentMask(context, node, RoadAgentMask.Car),
                    includeConnectors = context.GetInputValue(node, "includeConnectors", false),
                    onlyOpen = context.GetInputValue(node, "onlyOpen", true),
                    excludeOrphaned = context.GetInputValue(node, "excludeOrphaned", true),
                    requireOutgoingConnection = context.GetInputValue(node, "requireOutgoingConnection", false),
                    requireRouteNode = context.GetInputValue(node, "requireRouteNode", true),
                    sortMode = context.GetInputValue(node, "sortMode", VehicleRoadLaneSortMode.Stable)
                });
            return VehicleRoadExecutorUtility.ReadLaneIdsResult(subsystem != null, laneIds, outputPortId);
        }
    }

    public sealed class VehicleRoadGetRouteCandidateLaneIdsExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.GetRouteCandidateLaneIds"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            List<string> laneIds = subsystem == null
                ? new List<string>()
                : subsystem.GetRouteCandidateLaneIds(new VehicleRoadRouteCandidateLaneQuery
                {
                    agentMask = VehicleRoadExecutorUtility.GetAgentMask(context, node, RoadAgentMask.Car),
                    includeTerminalLanes = context.GetInputValue(node, "includeTerminalLanes", true),
                    includeDeadEnds = context.GetInputValue(node, "includeDeadEnds", false),
                    minLength = context.GetInputValue(node, "minLength", 3f),
                    excludeConnectors = context.GetInputValue(node, "excludeConnectors", true),
                    onlyOpen = context.GetInputValue(node, "onlyOpen", true),
                    excludeOrphaned = context.GetInputValue(node, "excludeOrphaned", true)
                });
            return VehicleRoadExecutorUtility.ReadLaneIdsResult(subsystem != null, laneIds, outputPortId);
        }
    }

    public sealed class VehicleRoadFindSpawnLaneAroundTransformExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.FindSpawnLaneAroundTransform"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            if (subsystem == null)
            {
                VehicleRoadExecutorUtility.StoreResult(
                    context,
                    node,
                    new VehicleRoadSpawnLaneResult { failureReason = "Missing VehicleRoadSubsystem target." });
                return BlueprintExecResult.Continue("execOut");
            }

            Transform anchor = VehicleRoadExecutorUtility.ResolveBinding<Transform>(context, node, "anchor");
            VehicleRoadSpawnLaneResult result = subsystem.FindSpawnLaneAroundTransform(
                anchor,
                new VehicleRoadSpawnLaneQuery
                {
                    agentMask = VehicleRoadExecutorUtility.GetAgentMask(context, node, RoadAgentMask.Car),
                    minDistance = context.GetInputValue(node, "minDistance", 35f),
                    maxDistance = context.GetInputValue(node, "maxDistance", 235f),
                    laneSearchDistance = context.GetInputValue(node, "laneSearchDistance", 25f),
                    maxHeightDifference = context.GetInputValue(node, "maxHeightDifference", 3f),
                    candidateLaneIds = VehicleRoadExecutorUtility.GetStringListInput(context, node, "candidateLaneIds"),
                    requireReachableCandidate = context.GetInputValue(node, "requireReachableCandidate", true),
                    excludeConnectors = context.GetInputValue(node, "excludeConnectors", true),
                    maxTrials = context.GetInputValue(node, "maxTrials", 32)
                });
            VehicleRoadExecutorUtility.StoreResult(context, node, result);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return VehicleRoadExecutorUtility.ReadSpawnLaneResult(
                VehicleRoadExecutorUtility.GetStoredResult<VehicleRoadSpawnLaneResult>(context, node),
                outputPortId);
        }
    }

    public sealed class VehicleRoadSelectReachableRouteTargetExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.SelectReachableRouteTarget"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            VehicleRoadRouteTargetSelectionResult result = subsystem == null
                ? new VehicleRoadRouteTargetSelectionResult { failureReason = "Missing VehicleRoadSubsystem target." }
                : subsystem.SelectReachableRouteTarget(new VehicleRoadRouteTargetSelectionQuery
                {
                    currentLaneId = context.GetInputValue(node, "currentLaneId", string.Empty),
                    agentMask = VehicleRoadExecutorUtility.GetAgentMask(context, node, RoadAgentMask.Car),
                    candidateLaneIds = VehicleRoadExecutorUtility.GetStringListInput(context, node, "candidateLaneIds"),
                    selectionMode = context.GetInputValue(node, "selectionMode", VehicleRoadRouteTargetSelectionMode.Random),
                    previousIndex = context.GetInputValue(node, "previousIndex", -1),
                    minimumRouteCost = context.GetInputValue(node, "minimumRouteCost", 0.001f),
                    allowSameLane = context.GetInputValue(node, "allowSameLane", false),
                    excludeConnectors = context.GetInputValue(node, "excludeConnectors", true)
                });
            VehicleRoadExecutorUtility.StoreResult(context, node, result);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return VehicleRoadExecutorUtility.ReadRouteTargetSelectionResult(
                VehicleRoadExecutorUtility.GetStoredResult<VehicleRoadRouteTargetSelectionResult>(context, node),
                outputPortId);
        }
    }

    public sealed class VehicleRoadFilterLaneIdsExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.FilterLaneIds"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            int removedCount = 0;
            List<string> filteredLaneIds = subsystem == null
                ? new List<string>()
                : subsystem.FilterLaneIds(
                    VehicleRoadExecutorUtility.GetStringListInput(context, node, "laneIds"),
                    new VehicleRoadLaneFilterQuery
                    {
                        agentMask = VehicleRoadExecutorUtility.GetAgentMask(context, node, RoadAgentMask.Car),
                        excludeConnectors = context.GetInputValue(node, "excludeConnectors", true),
                        onlyOpen = context.GetInputValue(node, "onlyOpen", true),
                        excludeOrphaned = context.GetInputValue(node, "excludeOrphaned", true),
                        requireOutgoingConnection = context.GetInputValue(node, "requireOutgoingConnection", false),
                        minLength = context.GetInputValue(node, "minLength", 0f)
                    },
                    out removedCount);
            return VehicleRoadExecutorUtility.ReadFilterLaneIdsResult(
                subsystem != null,
                filteredLaneIds,
                removedCount,
                outputPortId);
        }
    }

    public sealed class VehicleRoadGetLaneInfoExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.GetLaneInfo"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            VehicleRoadLaneInfoResult result = subsystem == null
                ? new VehicleRoadLaneInfoResult()
                : subsystem.GetLaneInfo(context.GetInputValue(node, "laneId", string.Empty));
            return VehicleRoadExecutorUtility.ReadLaneInfoResult(result, outputPortId);
        }
    }

    public sealed class VehicleRoadEvaluateLaneOccupancyExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.EvaluateLaneOccupancy"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            VehicleRoadLaneOccupancyResult result = subsystem == null
                ? new VehicleRoadLaneOccupancyResult
                {
                    status = VehicleRoadLaneOccupancyStatus.InvalidInput,
                    failureReason = "VehicleRoad.EvaluateLaneOccupancy requires a VehicleRoadSubsystem target."
                }
                : subsystem.EvaluateLaneOccupancy(new VehicleRoadLaneOccupancyQuery
                {
                    vehicleId = context.GetInputValue(node, "vehicleId", string.Empty),
                    laneId = context.GetInputValue(node, "laneId", string.Empty),
                    distanceAlongLane = context.GetInputValue(node, "distanceAlongLane", 0f),
                    agentMask = VehicleRoadExecutorUtility.GetAgentMask(context, node),
                    vehicleLength = context.GetInputValue(node, "vehicleLength", 0f),
                    lookAheadDistance = context.GetInputValue(node, "lookAheadDistance", 0f),
                    requiredGap = context.GetInputValue(node, "requiredGap", 0f),
                    maxOccupancyRatio = context.GetInputValue(node, "maxOccupancyRatio", 0f)
                });
            return VehicleRoadExecutorUtility.ReadLaneOccupancyResult(result, outputPortId);
        }
    }

    public sealed class VehicleRoadEvaluateLaneChangeRouteExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.EvaluateLaneChangeRoute"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            VehicleRoadLaneChangeRouteResult result = subsystem == null
                ? new VehicleRoadLaneChangeRouteResult
                {
                    routeLaneIds = new List<string>(),
                    reason = VehicleRoadLaneChangeDecisionReason.NoCurrentRoute,
                    failureReason = "VehicleRoad.EvaluateLaneChangeRoute requires a VehicleRoadSubsystem target."
                }
                : subsystem.EvaluateLaneChangeRoute(new VehicleRoadLaneChangeRouteQuery
                {
                    vehicleId = context.GetInputValue(node, "vehicleId", string.Empty),
                    currentLaneId = context.GetInputValue(node, "currentLaneId", string.Empty),
                    destinationLaneId = context.GetInputValue(node, "destinationLaneId", string.Empty),
                    currentRouteLaneIds = VehicleRoadExecutorUtility.GetStringListInput(context, node, "currentRouteLaneIds"),
                    distanceAlongLane = context.GetInputValue(node, "distanceAlongLane", 0f),
                    agentMask = VehicleRoadExecutorUtility.GetAgentMask(context, node),
                    vehicleLength = context.GetInputValue(node, "vehicleLength", 0f),
                    preferredSide = context.GetInputValue(node, "preferredSide", RoadLaneAdjacentSide.Right),
                    allowOppositeSide = context.GetInputValue(node, "allowOppositeSide", true),
                    lookAheadDistance = context.GetInputValue(node, "lookAheadDistance", 0f),
                    requiredGap = context.GetInputValue(node, "requiredGap", 0f),
                    maxOccupancyRatio = context.GetInputValue(node, "maxOccupancyRatio", 0f)
                });
            return VehicleRoadExecutorUtility.ReadLaneChangeRouteResult(result, outputPortId);
        }
    }

    public sealed class VehicleRoadSetLaneClosedExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.SetLaneClosed"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            if (subsystem == null)
            {
                return BlueprintExecResult.Error("VehicleRoad.SetLaneClosed requires a VehicleRoadSubsystem target.");
            }

            string laneId = context.GetInputValue(node, "laneId", string.Empty);
            if (string.IsNullOrWhiteSpace(laneId))
            {
                return BlueprintExecResult.Error("VehicleRoad.SetLaneClosed requires a non-empty laneId.");
            }

            subsystem.SetLaneClosed(laneId, context.GetInputValue(node, "closed", false));
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class VehicleRoadSetLaneCongestionCostExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.SetLaneCongestionCost"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            if (subsystem == null)
            {
                return BlueprintExecResult.Error("VehicleRoad.SetLaneCongestionCost requires a VehicleRoadSubsystem target.");
            }

            string laneId = context.GetInputValue(node, "laneId", string.Empty);
            if (string.IsNullOrWhiteSpace(laneId))
            {
                return BlueprintExecResult.Error("VehicleRoad.SetLaneCongestionCost requires a non-empty laneId.");
            }

            subsystem.SetLaneCongestionCost(laneId, context.GetInputValue(node, "cost", 0f));
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class VehicleRoadUpdateVehicleExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.UpdateVehicle"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            if (subsystem == null)
            {
                return BlueprintExecResult.Error("VehicleRoad.UpdateVehicle requires a VehicleRoadSubsystem target.");
            }

            string vehicleId = context.GetInputValue(node, "vehicleId", string.Empty);
            string laneId = context.GetInputValue(node, "laneId", string.Empty);
            if (string.IsNullOrWhiteSpace(vehicleId) || string.IsNullOrWhiteSpace(laneId))
            {
                return BlueprintExecResult.Error("VehicleRoad.UpdateVehicle requires non-empty vehicleId and laneId.");
            }

            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = vehicleId,
                laneId = laneId,
                agentMask = VehicleRoadExecutorUtility.GetAgentMask(context, node),
                distanceAlongLane = context.GetInputValue(node, "distanceAlongLane", 0f),
                speed = context.GetInputValue(node, "speed", 0f),
                length = context.GetInputValue(node, "length", 0f),
                routeLaneIds = VehicleRoadExecutorUtility.GetStringListInput(context, node, "routeLaneIds")
            });
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class VehicleRoadUnregisterVehicleExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.UnregisterVehicle"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            if (subsystem == null)
            {
                return BlueprintExecResult.Error("VehicleRoad.UnregisterVehicle requires a VehicleRoadSubsystem target.");
            }

            string vehicleId = context.GetInputValue(node, "vehicleId", string.Empty);
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                return BlueprintExecResult.Error("VehicleRoad.UnregisterVehicle requires a non-empty vehicleId.");
            }

            VehicleRoadExecutorUtility.StoreResult(context, node, subsystem.UnregisterVehicle(vehicleId));
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "removed")
            {
                return VehicleRoadExecutorUtility.GetStoredResult<bool>(context, node);
            }

            return null;
        }
    }

    public sealed class VehicleRoadEvaluateTrafficControlExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.EvaluateTrafficControl"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            if (subsystem == null)
            {
                return BlueprintExecResult.Error("VehicleRoad.EvaluateTrafficControl requires a VehicleRoadSubsystem target.");
            }

            string vehicleId = context.GetInputValue(node, "vehicleId", string.Empty);
            string laneId = context.GetInputValue(node, "laneId", string.Empty);
            if (string.IsNullOrWhiteSpace(vehicleId) || string.IsNullOrWhiteSpace(laneId))
            {
                return BlueprintExecResult.Error("VehicleRoad.EvaluateTrafficControl requires non-empty vehicleId and laneId.");
            }

            VehicleRoadTrafficControlResult result = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = vehicleId,
                laneId = laneId,
                agentMask = VehicleRoadExecutorUtility.GetAgentMask(context, node),
                distanceAlongLane = context.GetInputValue(node, "distanceAlongLane", 0f),
                speed = context.GetInputValue(node, "speed", 0f),
                vehicleLength = context.GetInputValue(node, "vehicleLength", 0f),
                routeLaneIds = VehicleRoadExecutorUtility.GetStringListInput(context, node, "routeLaneIds")
            });
            VehicleRoadExecutorUtility.StoreResult(context, node, result);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return VehicleRoadExecutorUtility.ReadTrafficControlResult(
                VehicleRoadExecutorUtility.GetStoredResult<VehicleRoadTrafficControlResult>(context, node),
                outputPortId);
        }
    }

    public sealed class VehicleRoadRequestLaneChangeExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.RequestLaneChange"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            if (subsystem == null)
            {
                return BlueprintExecResult.Error("VehicleRoad.RequestLaneChange requires a VehicleRoadSubsystem target.");
            }

            string vehicleId = context.GetInputValue(node, "vehicleId", string.Empty);
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                return BlueprintExecResult.Error("VehicleRoad.RequestLaneChange requires a non-empty vehicleId.");
            }

            VehicleRoadLaneChangeRequestResult result = subsystem.RequestLaneChange(
                vehicleId,
                context.GetInputValue(node, "side", RoadLaneAdjacentSide.Right));
            VehicleRoadExecutorUtility.StoreResult(context, node, result);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return VehicleRoadExecutorUtility.ReadLaneChangeRequestResult(
                VehicleRoadExecutorUtility.GetStoredResult<VehicleRoadLaneChangeRequestResult>(context, node),
                outputPortId);
        }
    }

    public sealed class VehicleRoadCompleteLaneChangeExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.CompleteLaneChange"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            if (subsystem == null)
            {
                return BlueprintExecResult.Error("VehicleRoad.CompleteLaneChange requires a VehicleRoadSubsystem target.");
            }

            string vehicleId = context.GetInputValue(node, "vehicleId", string.Empty);
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                return BlueprintExecResult.Error("VehicleRoad.CompleteLaneChange requires a non-empty vehicleId.");
            }

            VehicleRoadExecutorUtility.StoreResult(context, node, subsystem.CompleteLaneChange(vehicleId));
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "completed")
            {
                return VehicleRoadExecutorUtility.GetStoredResult<bool>(context, node);
            }

            return null;
        }
    }

    public sealed class VehicleRoadSetFollowerRouteExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.SetFollowerRoute"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            VehicleLaneFollower follower = VehicleRoadExecutorUtility.ResolveBinding<VehicleLaneFollower>(context, node, "target");
            if (follower == null)
            {
                return BlueprintExecResult.Error("VehicleRoad.SetFollowerRoute requires a VehicleLaneFollower target.");
            }

            follower.SetRoute(VehicleRoadExecutorUtility.GetStringListInput(context, node, "laneIds"));
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class VehicleRoadComputeFollowerControlExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.ComputeFollowerControl"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            VehicleLaneFollower follower = VehicleRoadExecutorUtility.ResolveBinding<VehicleLaneFollower>(context, node, "target");
            if (follower == null)
            {
                return BlueprintExecResult.Error("VehicleRoad.ComputeFollowerControl requires a VehicleLaneFollower target.");
            }

            VehicleLaneFollowerOutput output = follower.ComputeControl(VehicleRoadExecutorUtility.CreateFollowerInput(context, node));
            VehicleRoadExecutorUtility.StoreResult(context, node, output);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return VehicleRoadExecutorUtility.ReadFollowerOutput(
                VehicleRoadExecutorUtility.GetStoredResult<VehicleLaneFollowerOutput>(context, node),
                outputPortId);
        }
    }

    public sealed class VehicleRoadGetSubsystemSnapshotExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "VehicleRoad.GetSubsystemSnapshot"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            VehicleRoadSubsystem subsystem = VehicleRoadExecutorUtility.ResolveSubsystem(context, node);
            VehicleRoadSubsystemSnapshot snapshot = subsystem == null ? null : subsystem.GetSnapshot();
            return VehicleRoadExecutorUtility.ReadSnapshot(snapshot, outputPortId);
        }
    }

    internal static class VehicleRoadExecutorUtility
    {
        private const string ResultPrefix = "vehicleRoadResult:";

        public static VehicleRoadSubsystem ResolveSubsystem(BlueprintExecutionContext context, RuntimeNode node)
        {
            return ResolveBinding<VehicleRoadSubsystem>(context, node, "target");
        }

        public static T ResolveBinding<T>(BlueprintExecutionContext context, RuntimeNode node, string portId) where T : UnityEngine.Object
        {
            return GameExecutorBindingUtility.ResolveBinding<T>(context, context.GetInputValue(node, portId));
        }

        public static RoadAgentMask GetAgentMask(BlueprintExecutionContext context, RuntimeNode node)
        {
            RoadAgentMask mask = context.GetInputValue(node, "agentMask", RoadAgentMask.MotorVehicles);
            return mask == RoadAgentMask.None ? RoadAgentMask.MotorVehicles : mask;
        }

        public static RoadAgentMask GetAgentMask(BlueprintExecutionContext context, RuntimeNode node, RoadAgentMask defaultValue)
        {
            RoadAgentMask mask = context.GetInputValue(node, "agentMask", defaultValue);
            return mask == RoadAgentMask.None ? defaultValue : mask;
        }

        public static List<string> GetStringListInput(BlueprintExecutionContext context, RuntimeNode node, string portId)
        {
            return ToStringList(context.GetInputValue(node, portId));
        }

        public static VehicleLaneFollowerInput CreateFollowerInput(BlueprintExecutionContext context, RuntimeNode node)
        {
            return new VehicleLaneFollowerInput
            {
                vehicleId = context.GetInputValue(node, "vehicleId", string.Empty),
                position = GameExecutorValueUtility.GetVector3Input(context, node, "position", Vector3.zero),
                forward = GameExecutorValueUtility.GetVector3Input(context, node, "forward", Vector3.forward),
                speed = context.GetInputValue(node, "speed", 0f),
                wheelBase = context.GetInputValue(node, "wheelBase", 2.7f),
                vehicleLength = context.GetInputValue(node, "vehicleLength", 4.5f),
                agentMask = GetAgentMask(context, node),
                leadVehicleDistance = context.GetInputValue(node, "leadVehicleDistance", 0f),
                leadVehicleSpeed = context.GetInputValue(node, "leadVehicleSpeed", 0f),
                requestLaneChange = context.GetInputValue(node, "requestLaneChange", false),
                requestedLaneChangeSide = context.GetInputValue(node, "requestedLaneChangeSide", RoadLaneAdjacentSide.Right)
            };
        }

        public static void StoreResult<T>(BlueprintExecutionContext context, RuntimeNode node, T result)
        {
            context.SetState(ResultKey(node), result);
        }

        public static T GetStoredResult<T>(BlueprintExecutionContext context, RuntimeNode node)
        {
            object stored;
            return context.TryGetState(ResultKey(node), out stored) && stored is T ? (T)stored : default;
        }

        public static object ReadNearestResult(bool found, VehicleRoadNearestResult result, string outputPortId)
        {
            switch (outputPortId)
            {
                case "found":
                    return found;
                case "laneId":
                    return found ? result.LaneId : string.Empty;
                case "position":
                    return found ? result.Position : Vector3.zero;
                case "forward":
                    return found ? result.Forward : Vector3.forward;
                case "up":
                    return found ? result.Up : Vector3.up;
                case "distanceAlongLane":
                    return found ? result.DistanceAlongLane : 0f;
                case "distanceToLane":
                    return found ? result.DistanceToLane : 0f;
                default:
                    return null;
            }
        }

        public static object ReadRouteResult(bool success, VehicleRoadRouteResult result, string outputPortId)
        {
            switch (outputPortId)
            {
                case "success":
                    return success;
                case "laneIds":
                    return result == null || result.laneIds == null ? new List<string>() : new List<string>(result.laneIds);
                case "totalCost":
                    return result == null ? 0f : result.totalCost;
                default:
                    return null;
            }
        }

        public static object ReadLaneIdsResult(bool validTarget, List<string> laneIds, string outputPortId)
        {
            int count = laneIds == null ? 0 : laneIds.Count;
            switch (outputPortId)
            {
                case "laneIds":
                    return laneIds == null ? new List<string>() : new List<string>(laneIds);
                case "count":
                    return count;
                case "success":
                    return validTarget && count > 0;
                default:
                    return null;
            }
        }

        public static object ReadFilterLaneIdsResult(
            bool validTarget,
            List<string> filteredLaneIds,
            int removedCount,
            string outputPortId)
        {
            int count = filteredLaneIds == null ? 0 : filteredLaneIds.Count;
            switch (outputPortId)
            {
                case "filteredLaneIds":
                    return filteredLaneIds == null ? new List<string>() : new List<string>(filteredLaneIds);
                case "removedCount":
                    return removedCount;
                case "success":
                    return validTarget && count > 0;
                default:
                    return null;
            }
        }

        public static object ReadSpawnLaneResult(VehicleRoadSpawnLaneResult result, string outputPortId)
        {
            if (result == null)
            {
                result = new VehicleRoadSpawnLaneResult { failureReason = "No spawn result has been executed." };
            }

            switch (outputPortId)
            {
                case "found":
                    return result.found;
                case "laneId":
                    return result.laneId ?? string.Empty;
                case "position":
                    return result.position;
                case "forward":
                    return result.forward;
                case "up":
                    return result.up;
                case "distanceFromAnchor":
                    return result.distanceFromAnchor;
                case "failureReason":
                    return result.failureReason ?? string.Empty;
                default:
                    return null;
            }
        }

        public static object ReadRouteTargetSelectionResult(
            VehicleRoadRouteTargetSelectionResult result,
            string outputPortId)
        {
            if (result == null)
            {
                result = new VehicleRoadRouteTargetSelectionResult
                {
                    failureReason = "No route target selection has been executed."
                };
            }

            switch (outputPortId)
            {
                case "success":
                    return result.success;
                case "destinationLaneId":
                    return result.destinationLaneId ?? string.Empty;
                case "selectedIndex":
                    return result.selectedIndex;
                case "routeLaneIds":
                    return result.routeLaneIds == null ? new List<string>() : new List<string>(result.routeLaneIds);
                case "totalCost":
                    return result.totalCost;
                case "failureReason":
                    return result.failureReason ?? string.Empty;
                default:
                    return null;
            }
        }

        public static object ReadLaneInfoResult(VehicleRoadLaneInfoResult result, string outputPortId)
        {
            if (result == null)
            {
                result = new VehicleRoadLaneInfoResult();
            }

            switch (outputPortId)
            {
                case "found":
                    return result.found;
                case "laneId":
                    return result.laneId ?? string.Empty;
                case "kind":
                    return result.kind;
                case "length":
                    return result.length;
                case "open":
                    return result.open;
                case "orphaned":
                    return result.orphaned;
                case "agentMask":
                    return result.agentMask;
                case "outgoingConnectionCount":
                    return result.outgoingConnectionCount;
                case "adjacentLinkCount":
                    return result.adjacentLinkCount;
                default:
                    return null;
            }
        }

        public static object ReadTrafficControlResult(VehicleRoadTrafficControlResult result, string outputPortId)
        {
            switch (outputPortId)
            {
                case "hasConstraint":
                    return result.hasConstraint;
                case "stopReason":
                    return result.stopReason;
                case "passageStatus":
                    return result.passageStatus;
                case "signalState":
                    return result.signalState;
                case "junctionId":
                    return result.junctionId ?? string.Empty;
                case "connectorLaneId":
                    return result.connectorLaneId ?? string.Empty;
                case "connectionId":
                    return result.connectionId ?? string.Empty;
                case "queueIndex":
                    return result.queueIndex;
                case "distanceToStopLine":
                    return result.distanceToStopLine;
                case "targetSpeedLimit":
                    return result.targetSpeedLimit;
                case "hasStopPosition":
                    return result.hasStopPosition;
                case "stopPosition":
                    return result.stopPosition;
                case "leadVehicleId":
                    return result.leadVehicle.vehicleId ?? string.Empty;
                case "leadVehicleLaneId":
                    return result.leadVehicle.laneId ?? string.Empty;
                case "leadVehicleDistance":
                    return result.leadVehicle.distanceAlongRoute;
                case "leadVehicleSpeed":
                    return result.leadVehicle.speed;
                case "leadVehicleLength":
                    return result.leadVehicle.length;
                case "laneChangeStatus":
                    return result.laneChangeStatus;
                case "laneChangeTargetLaneId":
                    return result.laneChangeTargetLaneId ?? string.Empty;
                case "hasLaneChangeTargetPoint":
                    return result.hasLaneChangeTargetPoint;
                case "laneChangeTargetPoint":
                    return result.laneChangeTargetPoint;
                case "failureReason":
                    return result.failureReason ?? string.Empty;
                default:
                    return null;
            }
        }

        public static object ReadLaneChangeRequestResult(VehicleRoadLaneChangeRequestResult result, string outputPortId)
        {
            switch (outputPortId)
            {
                case "status":
                    return result.status;
                case "fromLaneId":
                    return result.fromLaneId ?? string.Empty;
                case "targetLaneId":
                    return result.targetLaneId ?? string.Empty;
                case "reservedDistanceAlongLane":
                    return result.reservedDistanceAlongLane;
                case "failureReason":
                    return result.failureReason ?? string.Empty;
                default:
                    return null;
            }
        }

        public static object ReadLaneOccupancyResult(VehicleRoadLaneOccupancyResult result, string outputPortId)
        {
            switch (outputPortId)
            {
                case "valid":
                    return result.valid;
                case "status":
                    return result.status;
                case "isEnterable":
                    return result.isEnterable;
                case "vehicleCount":
                    return result.vehicleCount;
                case "reservationCount":
                    return result.reservationCount;
                case "occupancyRatio":
                    return result.occupancyRatio;
                case "nearestForwardVehicleId":
                    return result.nearestForwardVehicleId ?? string.Empty;
                case "nearestForwardDistance":
                    return result.nearestForwardDistance;
                case "nearestRearVehicleId":
                    return result.nearestRearVehicleId ?? string.Empty;
                case "nearestRearDistance":
                    return result.nearestRearDistance;
                case "availableForwardGap":
                    return result.availableForwardGap;
                case "availableRearGap":
                    return result.availableRearGap;
                case "failureReason":
                    return result.failureReason ?? string.Empty;
                default:
                    return null;
            }
        }

        public static object ReadLaneChangeRouteResult(VehicleRoadLaneChangeRouteResult result, string outputPortId)
        {
            switch (outputPortId)
            {
                case "shouldRequestLaneChange":
                    return result.shouldRequestLaneChange;
                case "side":
                    return result.side;
                case "targetLaneId":
                    return result.targetLaneId ?? string.Empty;
                case "targetDistanceAlongLane":
                    return result.targetDistanceAlongLane;
                case "routeLaneIds":
                    return result.routeLaneIds == null ? new List<string>() : new List<string>(result.routeLaneIds);
                case "totalCost":
                    return result.totalCost;
                case "currentRouteFound":
                    return result.currentRouteFound;
                case "currentNextLaneId":
                    return result.currentNextLaneId ?? string.Empty;
                case "currentOccupancyStatus":
                    return result.currentOccupancyStatus;
                case "targetOccupancyStatus":
                    return result.targetOccupancyStatus;
                case "reason":
                    return result.reason;
                case "failureReason":
                    return result.failureReason ?? string.Empty;
                default:
                    return null;
            }
        }

        public static object ReadFollowerOutput(VehicleLaneFollowerOutput output, string outputPortId)
        {
            switch (outputPortId)
            {
                case "valid":
                    return output.valid;
                case "currentLaneId":
                    return output.currentLaneId ?? string.Empty;
                case "distanceAlongLane":
                    return output.distanceAlongLane;
                case "targetSteeringAngle":
                    return output.targetSteeringAngle;
                case "targetSpeed":
                    return output.targetSpeed;
                case "lookAheadPoint":
                    return output.lookAheadPoint;
                case "recoveryMode":
                    return output.recoveryMode;
                case "recoveryPosition":
                    return output.recoveryPosition;
                case "lateralError":
                    return output.lateralError;
                case "stopReason":
                    return output.stopReason;
                case "passageStatus":
                    return output.passageStatus;
                case "signalState":
                    return output.signalState;
                case "hasStopPoint":
                    return output.hasStopPoint;
                case "stopPoint":
                    return output.stopPoint;
                case "distanceToStopLine":
                    return output.distanceToStopLine;
                case "queueIndex":
                    return output.queueIndex;
                case "junctionId":
                    return output.junctionId ?? string.Empty;
                case "connectorLaneId":
                    return output.connectorLaneId ?? string.Empty;
                case "laneChangeStatus":
                    return output.laneChangeStatus;
                case "laneChangeTargetLaneId":
                    return output.laneChangeTargetLaneId ?? string.Empty;
                default:
                    return null;
            }
        }

        public static object ReadSnapshot(VehicleRoadSubsystemSnapshot snapshot, string outputPortId)
        {
            if (snapshot == null)
            {
                return outputPortId == "lastTrafficFailureReason" ? string.Empty : (object)0;
            }

            switch (outputPortId)
            {
                case "registeredNetworkCount":
                    return snapshot.registeredNetworkCount;
                case "laneCount":
                    return snapshot.laneCount;
                case "connectionCount":
                    return snapshot.connectionCount;
                case "adjacentLinkCount":
                    return snapshot.adjacentLinkCount;
                case "polygonCount":
                    return snapshot.polygonCount;
                case "portalCount":
                    return snapshot.portalCount;
                case "closedLaneCount":
                    return snapshot.closedLaneCount;
                case "congestionCostCount":
                    return snapshot.congestionCostCount;
                case "signalCostCount":
                    return snapshot.signalCostCount;
                case "registeredVehicleCount":
                    return snapshot.registeredVehicleCount;
                case "queuedVehicleCount":
                    return snapshot.queuedVehicleCount;
                case "activeTokenCount":
                    return snapshot.activeTokenCount;
                case "laneChangeReservationCount":
                    return snapshot.laneChangeReservationCount;
                case "signalPhaseCount":
                    return snapshot.signalPhaseCount;
                case "registeredRoadAgentCount":
                    return snapshot.registeredRoadAgentCount;
                case "queriesThisFrame":
                    return snapshot.queriesThisFrame;
                case "routesThisFrame":
                    return snapshot.routesThisFrame;
                case "replansThisFrame":
                    return snapshot.replansThisFrame;
                case "failuresThisFrame":
                    return snapshot.failuresThisFrame;
                case "lastCandidateCount":
                    return snapshot.lastCandidateCount;
                case "peakCandidateCount":
                    return snapshot.peakCandidateCount;
                case "lastVisitedNodeCount":
                    return snapshot.lastVisitedNodeCount;
                case "peakVisitedNodeCount":
                    return snapshot.peakVisitedNodeCount;
                case "lastRouteSegmentCount":
                    return snapshot.lastRouteSegmentCount;
                case "peakRouteSegmentCount":
                    return snapshot.peakRouteSegmentCount;
                case "diagnosticHistoryCount":
                    return snapshot.diagnosticHistoryCount;
                case "diagnosticHistoryCapacity":
                    return snapshot.diagnosticHistoryCapacity;
                case "diagnosticDroppedCount":
                    return snapshot.diagnosticDroppedCount;
                case "lastTrafficFailureReason":
                    return snapshot.lastTrafficFailureReason ?? string.Empty;
                default:
                    return null;
            }
        }

        private static List<string> ToStringList(object value)
        {
            List<string> result = new List<string>();
            IList list = BlueprintArrayUtility.ReadList(value);
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null)
                    {
                        result.Add(Convert.ToString(list[i], CultureInfo.InvariantCulture));
                    }
                }

                return result;
            }

            string text = value as string;
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            string[] parts = text.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i] == null ? string.Empty : parts[i].Trim();
                if (!string.IsNullOrEmpty(part))
                {
                    result.Add(part);
                }
            }

            return result;
        }

        private static string ResultKey(RuntimeNode node)
        {
            return ResultPrefix + (node == null ? string.Empty : node.Id);
        }
    }
}
