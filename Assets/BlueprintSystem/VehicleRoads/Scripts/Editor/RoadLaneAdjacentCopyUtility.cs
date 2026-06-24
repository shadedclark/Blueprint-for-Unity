using System;
using System.Collections.Generic;
using System.Linq;
using VehicleRoads;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads.Editor
{
    internal static class RoadLaneAdjacentCopyUtility
    {
        public const float DefaultLaneSpacing = 3.5f;
        private const float MinimumLaneSpacing = 0.01f;

        public static bool CanCopy(RoadLane source, out string reason)
        {
            if (source == null)
            {
                reason = "Select a RoadLane to copy.";
                return false;
            }

            if (source.Kind != RoadLaneKind.Standard)
            {
                reason = "Connector lanes cannot be copied as adjacent lanes.";
                return false;
            }

            if (source.GetComponentInParent<RoadLaneNetwork>() == null)
            {
                reason = "The source lane must belong to a RoadLaneNetwork.";
                return false;
            }

            Spline spline = source.Spline;
            if (source.SplineContainer == null || spline == null || spline.Closed || spline.Count < 2)
            {
                reason = "The source lane must have an open Spline with at least two knots.";
                return false;
            }

            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];
                if (!IsFinite(knot.Position) ||
                    !IsFinite(knot.TangentIn) ||
                    !IsFinite(knot.TangentOut) ||
                    !IsFinite(knot.Rotation.value))
                {
                    reason = "The source lane contains a non-finite knot, tangent, or rotation.";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        public static bool TryCopyAdjacentLane(
            RoadLane source,
            RoadLaneAdjacentSide side,
            float spacing,
            out RoadLane copiedLane,
            out string error,
            bool selectCreatedObject = true)
        {
            copiedLane = null;
            if (!CanCopy(source, out error))
            {
                return false;
            }

            if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing < MinimumLaneSpacing)
            {
                error = "Lane spacing must be a finite value greater than zero.";
                return false;
            }

            RoadLaneNetwork network = source.GetComponentInParent<RoadLaneNetwork>();
            string id = GetUniqueAdjacentLaneId(network, source.LaneId, side);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Copy Adjacent Road Lane");

            GameObject copiedObject = null;
            try
            {
                copiedObject = new GameObject(id);
                CopyGameObjectPlacement(source.gameObject, copiedObject);
                SplineContainer copiedContainer = copiedObject.AddComponent<SplineContainer>();
                copiedContainer.Spline = CreateOffsetSpline(source, copiedContainer.transform, side, spacing);

                copiedLane = copiedObject.AddComponent<RoadLane>();
                CopyLaneSettings(source, copiedLane, id);

                Undo.RegisterCreatedObjectUndo(copiedObject, "Copy Adjacent Road Lane");
                EditorUtility.SetDirty(copiedContainer);
                EditorUtility.SetDirty(copiedLane);
                EditorUtility.SetDirty(network);
                if (network.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(network.gameObject.scene);
                }

                if (selectCreatedObject)
                {
                    Selection.activeObject = copiedObject;
                }

                Undo.CollapseUndoOperations(undoGroup);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (copiedObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(copiedObject);
                }

                copiedLane = null;
                error = "Failed to copy adjacent lane: " + exception.Message;
                return false;
            }
        }

        public static bool IsSpacingInsideInferenceRange(
            RoadLaneNetwork network,
            float spacing)
        {
            return network != null &&
                   spacing >= network.AdjacentMinLateralDistance &&
                   spacing <= network.AdjacentMaxLateralDistance;
        }

        private static Spline CreateOffsetSpline(
            RoadLane source,
            Transform targetTransform,
            RoadLaneAdjacentSide side,
            float spacing)
        {
            Spline sourceSpline = source.Spline;
            Spline targetSpline = new Spline(sourceSpline.Count, false);
            float sideSign = side == RoadLaneAdjacentSide.Left ? -1f : 1f;

            for (int i = 0; i < sourceSpline.Count; i++)
            {
                BezierKnot sourceKnot = sourceSpline[i];
                Vector3 worldPosition = source.SplineContainer.transform.TransformPoint(sourceKnot.Position);
                GetKnotFrame(source, i, out Vector3 forward, out Vector3 up);
                Vector3 right = GetSafeRight(up, forward);
                Vector3 offsetWorldPosition = worldPosition + right * (spacing * sideSign);
                Vector3 targetLocalPosition = targetTransform.InverseTransformPoint(offsetWorldPosition);

                BezierKnot copiedKnot = sourceKnot;
                copiedKnot.Position = new float3(
                    targetLocalPosition.x,
                    targetLocalPosition.y,
                    targetLocalPosition.z);
                targetSpline.Add(
                    copiedKnot,
                    sourceSpline.GetTangentMode(i),
                    sourceSpline.GetAutoSmoothTension(i));
            }

            targetSpline.Closed = false;
            return targetSpline;
        }

        private static void GetKnotFrame(
            RoadLane source,
            int knotIndex,
            out Vector3 forward,
            out Vector3 up)
        {
            Spline spline = source.Spline;
            SplineContainer container = source.SplineContainer;
            float normalizedT = SplineUtility.GetNormalizedInterpolation(
                spline,
                knotIndex,
                PathIndexUnit.Knot);

            forward = container.EvaluateTangent(normalizedT);
            if (!UnitySplineRoadLaneGeometry.IsFinite(forward) || forward.sqrMagnitude <= 0.000001f)
            {
                forward = GetFallbackForward(container.transform, spline, knotIndex);
            }

            forward.Normalize();
            if (source.TravelDirection == RoadLaneTravelDirection.Reverse)
            {
                forward = -forward;
            }

            up = container.EvaluateUpVector(normalizedT);
            if (!UnitySplineRoadLaneGeometry.IsFinite(up) || up.sqrMagnitude <= 0.000001f)
            {
                float3 localUp = math.rotate(spline[knotIndex].Rotation, math.up());
                up = container.transform.TransformDirection((Vector3)localUp);
            }

            if (!UnitySplineRoadLaneGeometry.IsFinite(up) || up.sqrMagnitude <= 0.000001f)
            {
                up = Vector3.up;
            }

            up.Normalize();
        }

        private static Vector3 GetFallbackForward(
            Transform transform,
            Spline spline,
            int knotIndex)
        {
            int previousIndex = Mathf.Max(0, knotIndex - 1);
            int nextIndex = Mathf.Min(spline.Count - 1, knotIndex + 1);
            Vector3 previous = transform.TransformPoint(spline[previousIndex].Position);
            Vector3 next = transform.TransformPoint(spline[nextIndex].Position);
            Vector3 forward = next - previous;
            if (UnitySplineRoadLaneGeometry.IsFinite(forward) && forward.sqrMagnitude > 0.000001f)
            {
                return forward.normalized;
            }

            return transform.forward.sqrMagnitude > 0.000001f
                ? transform.forward.normalized
                : Vector3.forward;
        }

        private static Vector3 GetSafeRight(Vector3 up, Vector3 forward)
        {
            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude <= 0.000001f)
            {
                right = Vector3.Cross(Vector3.up, forward);
            }

            if (right.sqrMagnitude <= 0.000001f)
            {
                right = Vector3.Cross(Vector3.forward, forward);
            }

            return right.sqrMagnitude > 0.000001f ? right.normalized : Vector3.right;
        }

        private static void CopyGameObjectPlacement(GameObject source, GameObject target)
        {
            Transform sourceTransform = source.transform;
            Transform targetTransform = target.transform;
            targetTransform.SetParent(sourceTransform.parent, false);
            targetTransform.localPosition = sourceTransform.localPosition;
            targetTransform.localRotation = sourceTransform.localRotation;
            targetTransform.localScale = sourceTransform.localScale;
            targetTransform.SetSiblingIndex(sourceTransform.GetSiblingIndex() + 1);
            target.layer = source.layer;
            target.tag = source.tag;
            GameObjectUtility.SetStaticEditorFlags(
                target,
                GameObjectUtility.GetStaticEditorFlags(source));
        }

        private static void CopyLaneSettings(RoadLane source, RoadLane target, string id)
        {
            target.SetKind(RoadLaneKind.Standard);
            target.LaneId = id;
            target.TravelDirection = source.TravelDirection;
            target.SpeedLimit = source.SpeedLimit;
            target.TagMask = source.TagMask;
            target.AllowedAgents = source.AllowedAgents;
            target.Open = source.Open;
            target.ConnectionMode = source.ConnectionMode == RoadLaneConnectionMode.Manual
                ? RoadLaneConnectionMode.Automatic
                : source.ConnectionMode;
            target.ManualNextLaneIds = string.Empty;
            target.LateralOffset = source.LateralOffset;
            target.VerticalOffset = source.VerticalOffset;
            target.SampleSpacingOverride = source.SampleSpacingOverride;
        }

        private static string GetUniqueAdjacentLaneId(
            RoadLaneNetwork network,
            string sourceLaneId,
            RoadLaneAdjacentSide side)
        {
            HashSet<string> existingIds = new HashSet<string>(
                network.GetAuthoredLanes()
                    .Where(lane => lane != null)
                    .Select(lane => RoadLaneNetwork.SanitizeId(lane.LaneId)),
                StringComparer.Ordinal);

            string sourceId = RoadLaneNetwork.SanitizeId(sourceLaneId);
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                sourceId = "lane";
            }

            string suffix = side == RoadLaneAdjacentSide.Left ? "_left" : "_right";
            string baseId = RoadLaneNetwork.SanitizeId(sourceId + suffix);
            if (!existingIds.Contains(baseId))
            {
                return baseId;
            }

            for (int i = 2; i < 100000; i++)
            {
                string candidate = baseId + "_" + i;
                if (!existingIds.Contains(candidate))
                {
                    return candidate;
                }
            }

            return baseId + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFinite(float4 value)
        {
            return math.all(math.isfinite(value));
        }
    }
}
