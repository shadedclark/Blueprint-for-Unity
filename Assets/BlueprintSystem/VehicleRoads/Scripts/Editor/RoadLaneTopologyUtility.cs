using System;
using System.Collections.Generic;
using System.Linq;
using VehicleRoads;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads.Editor
{
    public enum RoadLaneTopologyTargetKind
    {
        None,
        Endpoint,
        LaneInterior,
        ProfileInterior
    }

    public readonly struct RoadLaneTopologyBuildResult
    {
        public readonly bool changed;
        public readonly RoadLaneTopologyTargetKind targetKind;
        public readonly RoadLane splitLane;
        public readonly RoadJunction junction;
        public readonly string message;

        public RoadLaneTopologyBuildResult(
            bool changed,
            RoadLaneTopologyTargetKind targetKind,
            RoadLane splitLane,
            RoadJunction junction,
            string message)
        {
            this.changed = changed;
            this.targetKind = targetKind;
            this.splitLane = splitLane;
            this.junction = junction;
            this.message = message ?? string.Empty;
        }
    }

    public static class RoadLaneTopologyUtility
    {
        private const float MinimumInteriorT = 0.01f;

        public static bool TryAutoConnect(
            RoadLaneNetwork network,
            RoadLane movingLane,
            RoadLaneEndpoint movingEndpoint,
            float endpointSnapRadius,
            float interiorSnapRadius,
            float directionTolerance,
            bool autoCreateJunction,
            out RoadLaneTopologyBuildResult result)
        {
            result = default;
            if (network == null || movingLane == null || movingLane.Kind == RoadLaneKind.Connector)
            {
                return false;
            }

            if (!TryGetEndpointPose(movingLane, movingEndpoint, out Vector3 movingPosition, out Vector3 movingForward))
            {
                return false;
            }

            if (TryFindEndpointCandidate(
                    network,
                    movingLane,
                    movingPosition,
                    Mathf.Max(0.01f, endpointSnapRadius),
                    out RoadLane endpointLane,
                    out RoadLaneEndpoint endpoint,
                    out Vector3 endpointPosition))
            {
                int undoGroup = BeginTopologyUndo(network, "Auto Connect Road Lane Endpoint");
                SetEndpointPosition(movingLane, movingEndpoint, endpointPosition);
                RoadJunction existingJunction = FindJunctionForEndpoint(
                    network,
                    endpointLane,
                    endpoint);
                if (existingJunction == null &&
                    CanUseDirectConnection(
                        movingLane,
                        movingEndpoint,
                        endpointLane,
                        endpoint,
                        movingForward,
                        directionTolerance))
                {
                    Undo.RecordObject(movingLane, "Enable Automatic Lane Connection");
                    Undo.RecordObject(endpointLane, "Enable Automatic Lane Connection");
                    movingLane.ConnectionMode = RoadLaneConnectionMode.Automatic;
                    endpointLane.ConnectionMode = RoadLaneConnectionMode.Automatic;
                    EditorUtility.SetDirty(movingLane);
                    EditorUtility.SetDirty(endpointLane);
                    Undo.CollapseUndoOperations(undoGroup);
                    result = new RoadLaneTopologyBuildResult(
                        true,
                        RoadLaneTopologyTargetKind.Endpoint,
                        null,
                        null,
                        "Snapped compatible lane endpoints and enabled automatic connection.");
                    return true;
                }

                if (!autoCreateJunction)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                    result = new RoadLaneTopologyBuildResult(
                        true,
                        RoadLaneTopologyTargetKind.Endpoint,
                        null,
                        null,
                        "Snapped lane endpoints.");
                    return true;
                }

                RoadJunction junction = existingJunction ?? CreateJunction(network, endpointPosition);
                AddBinding(junction, movingLane, movingEndpoint);
                AddBinding(junction, endpointLane, endpoint);
                RefreshJunction(network, junction);
                Undo.CollapseUndoOperations(undoGroup);
                result = new RoadLaneTopologyBuildResult(
                    true,
                    RoadLaneTopologyTargetKind.Endpoint,
                    null,
                    junction,
                    "Snapped endpoints and refreshed the Junction.");
                return true;
            }

            if (!TryFindInteriorCandidate(
                    network,
                    movingLane,
                    movingPosition,
                    Mathf.Max(0.01f, interiorSnapRadius),
                    out RoadLane targetLane,
                    out float targetT,
                    out Vector3 targetPosition))
            {
                return false;
            }

            int group = BeginTopologyUndo(network, "Split Road Lane And Create Junction");
            SetEndpointPosition(movingLane, movingEndpoint, targetPosition);
            RoadLaneProfileSource profileSource = targetLane.GetComponentInParent<RoadLaneProfileSource>();
            if (profileSource != null && !targetLane.ManagedProfileLocked)
            {
                if (!TrySplitProfileSource(
                        network,
                        profileSource,
                        targetPosition,
                        movingLane,
                        movingEndpoint,
                        autoCreateJunction,
                        out RoadJunction profileJunction,
                        out string profileError))
                {
                    Undo.RevertAllDownToGroup(group);
                    result = new RoadLaneTopologyBuildResult(
                        false,
                        RoadLaneTopologyTargetKind.ProfileInterior,
                        null,
                        null,
                        profileError);
                    return false;
                }

                Undo.CollapseUndoOperations(group);
                result = new RoadLaneTopologyBuildResult(
                    true,
                    RoadLaneTopologyTargetKind.ProfileInterior,
                    null,
                    profileJunction,
                    "Inserted a Profile topology break and refreshed managed lanes.");
                return true;
            }

            if (!TrySplitLane(
                    network,
                    targetLane,
                    targetT,
                    out RoadLane splitLane,
                    out string splitError))
            {
                Undo.RevertAllDownToGroup(group);
                result = new RoadLaneTopologyBuildResult(
                    false,
                    RoadLaneTopologyTargetKind.LaneInterior,
                    null,
                    null,
                    splitError);
                return false;
            }

            RoadJunction createdJunction = null;
            if (autoCreateJunction)
            {
                createdJunction = CreateJunction(network, targetPosition);
                AddBinding(createdJunction, movingLane, movingEndpoint);
                AddBinding(createdJunction, targetLane, RoadLaneEndpoint.End);
                AddBinding(createdJunction, splitLane, RoadLaneEndpoint.Start);
                RefreshJunction(network, createdJunction);
            }

            Undo.CollapseUndoOperations(group);
            result = new RoadLaneTopologyBuildResult(
                true,
                RoadLaneTopologyTargetKind.LaneInterior,
                splitLane,
                createdJunction,
                "Split the target lane and created the Junction topology.");
            return true;
        }

        public static bool TrySplitLane(
            RoadLaneNetwork network,
            RoadLane lane,
            float normalizedT,
            out RoadLane splitLane,
            out string error)
        {
            splitLane = null;
            error = string.Empty;
            if (network == null || lane == null || lane.Kind == RoadLaneKind.Connector)
            {
                error = "Only standard lanes can be split.";
                return false;
            }

            if (lane.Orphaned || lane.ManagedProfileLocked || lane.GetComponentInParent<RoadLaneProfileSource>() != null)
            {
                error = "Locked, orphaned, or Profile-managed lanes must be split through their source.";
                return false;
            }

            SplineContainer sourceContainer = lane.SplineContainer;
            Spline source = sourceContainer == null ? null : sourceContainer.Spline;
            float t = Mathf.Clamp(normalizedT, MinimumInteriorT, 1f - MinimumInteriorT);
            if (source == null || source.Count < 2)
            {
                error = "Lane spline must contain at least two knots.";
                return false;
            }

            SplitSpline(source, t, out Spline firstSpline, out Spline secondSpline, out float splitDistanceRatio);
            GameObject splitObject = new GameObject(GetUniqueLaneId(network, lane.LaneId + "_split"));
            Undo.RegisterCreatedObjectUndo(splitObject, "Create Split Road Lane");
            Undo.SetTransformParent(splitObject.transform, lane.transform.parent, "Parent Split Road Lane");
            splitObject.transform.SetPositionAndRotation(lane.transform.position, lane.transform.rotation);
            splitObject.transform.localScale = lane.transform.localScale;
            SplineContainer splitContainer = Undo.AddComponent<SplineContainer>(splitObject);
            splitLane = Undo.AddComponent<RoadLane>(splitObject);

            Undo.RecordObject(sourceContainer, "Split Road Lane Spline");
            Undo.RecordObject(lane, "Split Road Lane");
            splitContainer.Spline = secondSpline;
            sourceContainer.Spline = firstSpline;
            CopyLaneProperties(lane, splitLane);
            splitLane.LaneId = splitObject.name;

            string previousManualTargets = lane.ManualNextLaneIds;
            RoadLaneConnectionMode previousMode = lane.ConnectionMode;
            lane.ConnectionMode = RoadLaneConnectionMode.Automatic;
            lane.ManualNextLaneIds = string.Empty;
            splitLane.ConnectionMode = previousMode;
            splitLane.ManualNextLaneIds = previousManualTargets;
            SplitWidthKeys(lane, splitLane, splitDistanceRatio);
            MoveEndReferences(network, lane, splitLane);
            EditorUtility.SetDirty(sourceContainer);
            EditorUtility.SetDirty(splitContainer);
            EditorUtility.SetDirty(lane);
            EditorUtility.SetDirty(splitLane);
            return true;
        }

        private static bool TrySplitProfileSource(
            RoadLaneNetwork network,
            RoadLaneProfileSource source,
            Vector3 worldPosition,
            RoadLane movingLane,
            RoadLaneEndpoint movingEndpoint,
            bool autoCreateJunction,
            out RoadJunction junction,
            out string error)
        {
            junction = null;
            error = string.Empty;
            Spline spline = source.SplineContainer == null ? null : source.SplineContainer.Spline;
            if (spline == null || spline.Count < 2)
            {
                error = "Profile source spline is invalid.";
                return false;
            }

            Vector3 local = source.SplineContainer.transform.InverseTransformPoint(worldPosition);
            SplineUtility.GetNearestPoint(spline, (float3)local, out _, out float t);
            t = Mathf.Clamp(t, MinimumInteriorT, 1f - MinimumInteriorT);
            Undo.RegisterFullObjectHierarchyUndo(source.gameObject, "Split Road Profile Source");
            InsertExactKnot(spline, t, out int insertedIndex);
            source.SynchronizeControlPoints();
            if (!source.SetControlPoint(insertedIndex, null, true, out error))
            {
                return false;
            }

            if (!source.RefreshManagedLanes(
                    created => Undo.RegisterCreatedObjectUndo(created, "Create Profile Split Lane"),
                    modified => Undo.RecordObject(modified, "Refresh Profile Split Lane"),
                    out error))
            {
                return false;
            }

            if (!autoCreateJunction)
            {
                return true;
            }

            junction = CreateJunction(network, worldPosition);
            AddBinding(junction, movingLane, movingEndpoint);
            RoadLane[] managed = source.GetComponentsInChildren<RoadLane>(true);
            float endpointRadius = Mathf.Max(
                0.35f,
                source.Profile == null ? 0.35f : source.Profile.TotalWidth + 0.5f);
            for (int i = 0; i < managed.Length; i++)
            {
                RoadLane candidate = managed[i];
                if (candidate == null || candidate.ManagedProfileOrphaned)
                {
                    continue;
                }

                if (TryGetEndpointPose(candidate, RoadLaneEndpoint.Start, out Vector3 start, out _) &&
                    Vector3.Distance(start, worldPosition) <= endpointRadius)
                {
                    AddBinding(junction, candidate, RoadLaneEndpoint.Start);
                }

                if (TryGetEndpointPose(candidate, RoadLaneEndpoint.End, out Vector3 end, out _) &&
                    Vector3.Distance(end, worldPosition) <= endpointRadius)
                {
                    AddBinding(junction, candidate, RoadLaneEndpoint.End);
                }
            }

            RefreshJunction(network, junction);
            return true;
        }

        private static void InsertExactKnot(Spline spline, float normalizedT, out int insertedIndex)
        {
            int curveIndex = spline.SplineToCurveT(normalizedT, out float curveT);
            int nextIndex = curveIndex + 1;
            BezierKnot a = spline[curveIndex];
            BezierKnot b = spline[nextIndex];
            BezierCurve sourceCurve = spline.GetCurve(curveIndex);
            CurveUtility.Split(sourceCurve, curveT, out BezierCurve left, out BezierCurve right);
            quaternion rotation = math.slerp(a.Rotation, b.Rotation, curveT);
            quaternion inverse = math.inverse(rotation);
            a.TangentOut = math.mul(math.inverse(a.Rotation), left.Tangent0);
            b.TangentIn = math.mul(math.inverse(b.Rotation), right.Tangent1);
            BezierKnot split = new BezierKnot(
                left.P3,
                math.mul(inverse, left.Tangent1),
                math.mul(inverse, right.Tangent0),
                rotation);
            spline.SetKnot(curveIndex, a);
            spline.SetKnot(nextIndex, b);
            spline.Insert(nextIndex, split, TangentMode.Continuous);
            insertedIndex = nextIndex;
        }

        private static void SplitSpline(
            Spline source,
            float normalizedT,
            out Spline first,
            out Spline second,
            out float splitDistanceRatio)
        {
            int curveIndex = source.SplineToCurveT(normalizedT, out float curveT);
            BezierKnot a = source[curveIndex];
            BezierKnot b = source[curveIndex + 1];
            BezierCurve curve = source.GetCurve(curveIndex);
            CurveUtility.Split(curve, curveT, out BezierCurve left, out BezierCurve right);
            quaternion rotation = math.slerp(a.Rotation, b.Rotation, curveT);
            quaternion inverse = math.inverse(rotation);
            a.TangentOut = math.mul(math.inverse(a.Rotation), left.Tangent0);
            b.TangentIn = math.mul(math.inverse(b.Rotation), right.Tangent1);
            BezierKnot split = new BezierKnot(
                left.P3,
                math.mul(inverse, left.Tangent1),
                math.mul(inverse, right.Tangent0),
                rotation);

            first = new Spline(curveIndex + 2, false);
            for (int i = 0; i <= curveIndex; i++)
            {
                BezierKnot knot = i == curveIndex ? a : source[i];
                first.Add(knot, source.GetTangentMode(i), source.GetAutoSmoothTension(i));
            }
            first.Add(split, TangentMode.Continuous);

            second = new Spline(source.Count - curveIndex, false);
            second.Add(split, TangentMode.Continuous);
            for (int i = curveIndex + 1; i < source.Count; i++)
            {
                BezierKnot knot = i == curveIndex + 1 ? b : source[i];
                second.Add(knot, source.GetTangentMode(i), source.GetAutoSmoothTension(i));
            }

            float firstLength = first.GetLength();
            float secondLength = second.GetLength();
            splitDistanceRatio = firstLength / Mathf.Max(0.0001f, firstLength + secondLength);
        }

        private static void SplitWidthKeys(RoadLane firstLane, RoadLane secondLane, float splitRatio)
        {
            List<RoadLaneWidthKey> first = new List<RoadLaneWidthKey>();
            List<RoadLaneWidthKey> second = new List<RoadLaneWidthKey>();
            float splitWidth = firstLane.EvaluateWidth(splitRatio);
            first.Add(new RoadLaneWidthKey { normalizedDistance = 1f, width = splitWidth });
            second.Add(new RoadLaneWidthKey { normalizedDistance = 0f, width = splitWidth });
            IList<RoadLaneWidthKey> keys = firstLane.WidthKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                RoadLaneWidthKey key = keys[i];
                if (key == null)
                {
                    continue;
                }

                if (key.normalizedDistance < splitRatio)
                {
                    first.Add(new RoadLaneWidthKey
                    {
                        normalizedDistance = key.normalizedDistance / Mathf.Max(0.0001f, splitRatio),
                        width = key.width
                    });
                }
                else if (key.normalizedDistance > splitRatio)
                {
                    second.Add(new RoadLaneWidthKey
                    {
                        normalizedDistance = (key.normalizedDistance - splitRatio) /
                                             Mathf.Max(0.0001f, 1f - splitRatio),
                        width = key.width
                    });
                }
            }

            firstLane.SetWidthKeys(first);
            secondLane.SetWidthKeys(second);
        }

        private static void CopyLaneProperties(RoadLane source, RoadLane target)
        {
            target.TravelDirection = source.TravelDirection;
            target.SpeedLimit = source.SpeedLimit;
            target.Width = source.Width;
            target.TagMask = source.TagMask;
            target.AllowedAgents = source.AllowedAgents;
            target.AllowLaneChangeLeft = source.AllowLaneChangeLeft;
            target.AllowLaneChangeRight = source.AllowLaneChangeRight;
            target.Open = source.Open;
            target.LateralOffset = source.LateralOffset;
            target.VerticalOffset = source.VerticalOffset;
            target.SampleSpacingOverride = source.SampleSpacingOverride;
            target.TraversalCost = source.TraversalCost;
        }

        private static void MoveEndReferences(
            RoadLaneNetwork network,
            RoadLane firstLane,
            RoadLane secondLane)
        {
            RoadJunction[] junctions = network.GetJunctions();
            for (int i = 0; i < junctions.Length; i++)
            {
                RoadJunction junction = junctions[i];
                for (int bindingIndex = 0; bindingIndex < junction.Bindings.Count; bindingIndex++)
                {
                    RoadJunctionBinding binding = junction.Bindings[bindingIndex];
                    if (binding != null &&
                        binding.lane == firstLane &&
                        binding.endpoint == RoadLaneEndpoint.End)
                    {
                        Undo.RecordObject(junction, "Move Junction Binding To Split Lane");
                        binding.lane = secondLane;
                        EditorUtility.SetDirty(junction);
                    }
                }
            }

            RoadPortal[] portals = network.GetComponentsInChildren<RoadPortal>(true);
            for (int i = 0; i < portals.Length; i++)
            {
                RoadPortal portal = portals[i];
                if (portal.LinkedLane == firstLane &&
                    portal.LinkedLaneEndpoint == RoadLaneEndpoint.End)
                {
                    Undo.RecordObject(portal, "Move Portal To Split Lane");
                    portal.LinkedLane = secondLane;
                    EditorUtility.SetDirty(portal);
                }
            }
        }

        private static bool TryFindEndpointCandidate(
            RoadLaneNetwork network,
            RoadLane movingLane,
            Vector3 position,
            float radius,
            out RoadLane targetLane,
            out RoadLaneEndpoint targetEndpoint,
            out Vector3 targetPosition)
        {
            targetLane = null;
            targetEndpoint = default;
            targetPosition = default;
            float best = radius;
            RoadLane[] lanes = network.GetAuthoredLanes();
            for (int i = 0; i < lanes.Length; i++)
            {
                RoadLane lane = lanes[i];
                if (lane == null || lane == movingLane || lane.Kind == RoadLaneKind.Connector || lane.Orphaned)
                {
                    continue;
                }

                for (int endpointIndex = 0; endpointIndex < 2; endpointIndex++)
                {
                    RoadLaneEndpoint endpoint = endpointIndex == 0
                        ? RoadLaneEndpoint.Start
                        : RoadLaneEndpoint.End;
                    if (!TryGetEndpointPose(lane, endpoint, out Vector3 candidate, out _))
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(position, candidate);
                    if (distance > best)
                    {
                        continue;
                    }

                    best = distance;
                    targetLane = lane;
                    targetEndpoint = endpoint;
                    targetPosition = candidate;
                }
            }

            return targetLane != null;
        }

        private static bool TryFindInteriorCandidate(
            RoadLaneNetwork network,
            RoadLane movingLane,
            Vector3 position,
            float radius,
            out RoadLane targetLane,
            out float targetT,
            out Vector3 targetPosition)
        {
            targetLane = null;
            targetT = 0f;
            targetPosition = default;
            float best = radius;
            RoadLane[] lanes = network.GetAuthoredLanes();
            for (int i = 0; i < lanes.Length; i++)
            {
                RoadLane lane = lanes[i];
                SplineContainer container = lane == null ? null : lane.SplineContainer;
                Spline spline = container == null ? null : container.Spline;
                if (lane == null ||
                    lane == movingLane ||
                    lane.Kind == RoadLaneKind.Connector ||
                    lane.Orphaned ||
                    spline == null ||
                    spline.Count < 2)
                {
                    continue;
                }

                Vector3 local = container.transform.InverseTransformPoint(position);
                SplineUtility.GetNearestPoint(spline, (float3)local, out float3 nearest, out float t);
                if (t <= MinimumInteriorT || t >= 1f - MinimumInteriorT)
                {
                    continue;
                }

                Vector3 world = container.transform.TransformPoint(nearest);
                float distance = Vector3.Distance(position, world);
                if (distance > best)
                {
                    continue;
                }

                best = distance;
                targetLane = lane;
                targetT = t;
                targetPosition = world;
            }

            return targetLane != null;
        }

        private static bool CanUseDirectConnection(
            RoadLane movingLane,
            RoadLaneEndpoint movingEndpoint,
            RoadLane targetLane,
            RoadLaneEndpoint targetEndpoint,
            Vector3 movingForward,
            float directionTolerance)
        {
            if (!TryGetEndpointPose(targetLane, targetEndpoint, out _, out Vector3 targetForward))
            {
                return false;
            }

            bool movingToTarget =
                IsExitEndpoint(movingLane, movingEndpoint) &&
                IsEntryEndpoint(targetLane, targetEndpoint);
            bool targetToMoving =
                IsExitEndpoint(targetLane, targetEndpoint) &&
                IsEntryEndpoint(movingLane, movingEndpoint);
            return (movingToTarget || targetToMoving) &&
                   Vector3.Angle(movingForward, targetForward) <=
                   Mathf.Clamp(directionTolerance, 0f, 180f);
        }

        private static bool IsExitEndpoint(RoadLane lane, RoadLaneEndpoint endpoint)
        {
            return lane.TravelDirection == RoadLaneTravelDirection.Bidirectional ||
                   lane.TravelDirection == RoadLaneTravelDirection.Forward && endpoint == RoadLaneEndpoint.End ||
                   lane.TravelDirection == RoadLaneTravelDirection.Reverse && endpoint == RoadLaneEndpoint.Start;
        }

        private static bool IsEntryEndpoint(RoadLane lane, RoadLaneEndpoint endpoint)
        {
            return lane.TravelDirection == RoadLaneTravelDirection.Bidirectional ||
                   lane.TravelDirection == RoadLaneTravelDirection.Forward && endpoint == RoadLaneEndpoint.Start ||
                   lane.TravelDirection == RoadLaneTravelDirection.Reverse && endpoint == RoadLaneEndpoint.End;
        }

        private static bool TryGetEndpointPose(
            RoadLane lane,
            RoadLaneEndpoint endpoint,
            out Vector3 position,
            out Vector3 forward)
        {
            position = default;
            forward = Vector3.forward;
            SplineContainer container = lane == null ? null : lane.SplineContainer;
            Spline spline = container == null ? null : container.Spline;
            if (spline == null || spline.Count < 2)
            {
                return false;
            }

            float t = endpoint == RoadLaneEndpoint.Start ? 0f : 1f;
            position = container.EvaluatePosition(t);
            forward = container.EvaluateTangent(t);
            if (endpoint == RoadLaneEndpoint.Start && lane.TravelDirection == RoadLaneTravelDirection.Reverse ||
                endpoint == RoadLaneEndpoint.End && lane.TravelDirection == RoadLaneTravelDirection.Reverse)
            {
                forward = -forward;
            }
            return true;
        }

        private static void SetEndpointPosition(
            RoadLane lane,
            RoadLaneEndpoint endpoint,
            Vector3 worldPosition)
        {
            SplineContainer container = lane.SplineContainer;
            Spline spline = container.Spline;
            int knotIndex = endpoint == RoadLaneEndpoint.Start ? 0 : spline.Count - 1;
            Undo.RecordObject(container, "Snap Road Lane Endpoint");
            BezierKnot knot = spline[knotIndex];
            knot.Position = container.transform.InverseTransformPoint(worldPosition);
            spline.SetKnot(knotIndex, knot);
            EditorUtility.SetDirty(container);
            EditorUtility.SetDirty(lane);
        }

        private static RoadJunction FindJunctionForEndpoint(
            RoadLaneNetwork network,
            RoadLane lane,
            RoadLaneEndpoint endpoint)
        {
            RoadJunction[] junctions = network.GetJunctions();
            for (int i = 0; i < junctions.Length; i++)
            {
                RoadJunction junction = junctions[i];
                if (junction.Bindings.Any(binding =>
                        binding != null && binding.lane == lane && binding.endpoint == endpoint))
                {
                    return junction;
                }
            }

            return null;
        }

        private static RoadJunction CreateJunction(RoadLaneNetwork network, Vector3 position)
        {
            string id = GetUniqueJunctionId(network, "junction_auto");
            GameObject junctionObject = new GameObject(id);
            Undo.RegisterCreatedObjectUndo(junctionObject, "Create Automatic Road Junction");
            Undo.SetTransformParent(junctionObject.transform, network.transform, "Parent Automatic Road Junction");
            junctionObject.transform.position = position;
            RoadJunction junction = Undo.AddComponent<RoadJunction>(junctionObject);
            junction.JunctionId = id;
            return junction;
        }

        private static void AddBinding(
            RoadJunction junction,
            RoadLane lane,
            RoadLaneEndpoint endpoint)
        {
            if (junction == null || lane == null ||
                junction.Bindings.Any(binding =>
                    binding != null && binding.lane == lane && binding.endpoint == endpoint))
            {
                return;
            }

            Undo.RecordObject(junction, "Add Automatic Junction Binding");
            junction.Bindings.Add(new RoadJunctionBinding { lane = lane, endpoint = endpoint });
            EditorUtility.SetDirty(junction);
        }

        private static void RefreshJunction(RoadLaneNetwork network, RoadJunction junction)
        {
            network.RefreshConnectors(
                junction,
                created => Undo.RegisterCreatedObjectUndo(created, "Create Automatic Road Connector"));
            RoadLane[] connectors = junction.GetComponentsInChildren<RoadLane>(true);
            for (int i = 0; i < connectors.Length; i++)
            {
                EditorUtility.SetDirty(connectors[i]);
                if (connectors[i].SplineContainer != null)
                {
                    EditorUtility.SetDirty(connectors[i].SplineContainer);
                }
            }
            EditorUtility.SetDirty(junction);
            EditorUtility.SetDirty(network);
        }

        private static int BeginTopologyUndo(RoadLaneNetwork network, string name)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
            Undo.RegisterFullObjectHierarchyUndo(network.gameObject, name);
            return group;
        }

        private static string GetUniqueLaneId(RoadLaneNetwork network, string seed)
        {
            HashSet<string> ids = new HashSet<string>(
                network.GetAuthoredLanes()
                    .Where(lane => lane != null)
                    .Select(lane => lane.LaneId),
                StringComparer.Ordinal);
            string safe = RoadLaneNetwork.SanitizeId(seed);
            string candidate = safe;
            int suffix = 1;
            while (ids.Contains(candidate))
            {
                candidate = safe + "_" + suffix.ToString("D2");
                suffix++;
            }

            return candidate;
        }

        private static string GetUniqueJunctionId(RoadLaneNetwork network, string seed)
        {
            HashSet<string> ids = new HashSet<string>(
                network.GetJunctions()
                    .Where(junction => junction != null)
                    .Select(junction => junction.JunctionId),
                StringComparer.Ordinal);
            string candidate = RoadLaneNetwork.SanitizeId(seed);
            int suffix = 1;
            while (ids.Contains(candidate))
            {
                candidate = RoadLaneNetwork.SanitizeId(seed) + "_" + suffix.ToString("D2");
                suffix++;
            }

            return candidate;
        }
    }
}
