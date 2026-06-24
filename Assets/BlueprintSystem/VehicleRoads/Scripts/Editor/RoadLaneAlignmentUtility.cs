using VehicleRoads;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads.Editor
{
    internal enum RoadLaneKnotHeightReference
    {
        FirstKnot,
        LastKnot,
        Average,
        Custom
    }

    internal static class RoadLaneAlignmentUtility
    {
        public static Vector3 SnapPointToGrid(Vector3 point, float gridSize, bool snapX, bool snapY, bool snapZ)
        {
            if (gridSize <= 0.0001f)
            {
                return point;
            }

            if (snapX)
            {
                point.x = Mathf.Round(point.x / gridSize) * gridSize;
            }

            if (snapY)
            {
                point.y = Mathf.Round(point.y / gridSize) * gridSize;
            }

            if (snapZ)
            {
                point.z = Mathf.Round(point.z / gridSize) * gridSize;
            }

            return point;
        }

        public static bool TryProjectPointToRoad(
            Vector3 point,
            LayerMask mask,
            float rayHeight,
            out Vector3 projectedPoint)
        {
            float safeHeight = Mathf.Max(0.1f, rayHeight);
            Ray ray = new Ray(point + Vector3.up * safeHeight, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, safeHeight * 2f, mask, QueryTriggerInteraction.Ignore))
            {
                projectedPoint = hit.point;
                return true;
            }

            projectedPoint = point;
            return false;
        }

        public static int FlattenKnotHeights(
            RoadLane lane,
            RoadLaneKnotHeightReference reference,
            float customHeight,
            bool flattenTangentHeights)
        {
            SplineContainer container = lane == null ? null : lane.SplineContainer;
            Spline spline = container == null ? null : container.Spline;
            if (spline == null || spline.Count == 0 ||
                !TryResolveHeight(container, spline, reference, customHeight, out float targetHeight))
            {
                return 0;
            }

            Undo.RecordObject(container, "Flatten Road Lane Knots");
            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];
                Vector3 world = container.transform.TransformPoint(knot.Position);
                world.y = targetHeight;
                SetKnotWorldPosition(spline, container.transform, i, world, flattenTangentHeights);
            }

            MarkLaneDirty(lane, container);
            return spline.Count;
        }

        public static int SnapKnotPositionsToGrid(
            RoadLane lane,
            float gridSize,
            bool snapX,
            bool snapY,
            bool snapZ)
        {
            SplineContainer container = lane == null ? null : lane.SplineContainer;
            Spline spline = container == null ? null : container.Spline;
            if (spline == null || spline.Count == 0 || gridSize <= 0.0001f)
            {
                return 0;
            }

            Undo.RecordObject(container, "Snap Road Lane Knots To Grid");
            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];
                Vector3 world = container.transform.TransformPoint(knot.Position);
                Vector3 snapped = SnapPointToGrid(world, gridSize, snapX, snapY, snapZ);
                SetKnotWorldPosition(spline, container.transform, i, snapped, false);
            }

            MarkLaneDirty(lane, container);
            return spline.Count;
        }

        public static int SnapKnotsToRoadColliders(RoadLane lane, LayerMask mask, float rayHeight)
        {
            SplineContainer container = lane == null ? null : lane.SplineContainer;
            Spline spline = container == null ? null : container.Spline;
            if (spline == null || spline.Count == 0)
            {
                return 0;
            }

            Undo.RecordObject(container, "Snap Road Lane Knots To Road Colliders");
            int changed = 0;
            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];
                Vector3 world = container.transform.TransformPoint(knot.Position);
                if (!TryProjectPointToRoad(world, mask, rayHeight, out Vector3 projected))
                {
                    continue;
                }

                SetKnotWorldPosition(spline, container.transform, i, projected, false);
                changed++;
            }

            if (changed > 0)
            {
                MarkLaneDirty(lane, container);
            }

            return changed;
        }

        private static bool TryResolveHeight(
            SplineContainer container,
            Spline spline,
            RoadLaneKnotHeightReference reference,
            float customHeight,
            out float height)
        {
            if (reference == RoadLaneKnotHeightReference.Custom)
            {
                height = customHeight;
                return true;
            }

            if (spline.Count == 0)
            {
                height = 0f;
                return false;
            }

            if (reference == RoadLaneKnotHeightReference.LastKnot)
            {
                height = container.transform.TransformPoint(spline[spline.Count - 1].Position).y;
                return true;
            }

            if (reference == RoadLaneKnotHeightReference.Average)
            {
                float sum = 0f;
                for (int i = 0; i < spline.Count; i++)
                {
                    sum += container.transform.TransformPoint(spline[i].Position).y;
                }

                height = sum / spline.Count;
                return true;
            }

            height = container.transform.TransformPoint(spline[0].Position).y;
            return true;
        }

        private static void SetKnotWorldPosition(
            Spline spline,
            Transform transform,
            int knotIndex,
            Vector3 worldPosition,
            bool flattenTangentHeights)
        {
            BezierKnot knot = spline[knotIndex];
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            knot.Position = new float3(localPosition.x, localPosition.y, localPosition.z);
            if (flattenTangentHeights)
            {
                knot.TangentIn = FlattenWorldTangentHeight(transform, knot.TangentIn);
                knot.TangentOut = FlattenWorldTangentHeight(transform, knot.TangentOut);
            }

            spline.SetKnot(knotIndex, knot);
        }

        private static float3 FlattenWorldTangentHeight(Transform transform, float3 tangent)
        {
            Vector3 localTangent = new Vector3(tangent.x, tangent.y, tangent.z);
            Vector3 worldTangent = transform.TransformVector(localTangent);
            worldTangent.y = 0f;
            Vector3 flattenedLocal = transform.InverseTransformVector(worldTangent);
            return new float3(flattenedLocal.x, flattenedLocal.y, flattenedLocal.z);
        }

        private static void MarkLaneDirty(RoadLane lane, SplineContainer container)
        {
            EditorUtility.SetDirty(container);
            if (lane != null)
            {
                EditorUtility.SetDirty(lane);
                if (lane.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(lane.gameObject.scene);
                }
            }
        }
    }
}
