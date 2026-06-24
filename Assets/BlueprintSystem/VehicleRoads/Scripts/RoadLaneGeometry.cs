using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads
{
    [Serializable]
    public struct RoadLanePose
    {
        public Vector3 splinePosition;
        public Vector3 position;
        public Vector3 forward;
        public Vector3 up;
        public float curvature;
        public float distance;
        public float normalizedT;
        public float lateralOffset;
        public float verticalOffset;
    }

    public struct RoadLaneNearestPoint
    {
        public RoadLane lane;
        public Vector3 position;
        public Vector3 forward;
        public Vector3 up;
        public float distanceAlongLane;
        public float distanceToLane;
        public float normalizedT;
    }

    public interface IRoadLaneGeometry
    {
        float GetLength(RoadLane lane);
        bool TryEvaluate(RoadLane lane, float distance, bool reverse, out RoadLanePose pose);
        bool TryGetNearestPoint(RoadLane lane, Vector3 worldPosition, bool reverse, out RoadLaneNearestPoint nearest);
        List<RoadLanePose> SampleEqualDistance(RoadLane lane, float spacing, bool reverse);
    }

    public sealed class UnitySplineRoadLaneGeometry : IRoadLaneGeometry
    {
        private const int MinimumLutSteps = 32;
        private const int MaximumLutSteps = 1024;

        public float GetLength(RoadLane lane)
        {
            return lane == null || lane.SplineContainer == null || lane.Spline == null || lane.Spline.Count < 2
                ? 0f
                : lane.SplineContainer.CalculateLength();
        }

        public bool TryEvaluate(RoadLane lane, float distance, bool reverse, out RoadLanePose pose)
        {
            pose = default;
            if (!TryCreateLut(lane, out ArcLengthLut lut))
            {
                return false;
            }

            float directedDistance = Mathf.Clamp(distance, 0f, lut.length);
            float sourceDistance = reverse ? lut.length - directedDistance : directedDistance;
            float t = lut.DistanceToT(sourceDistance);
            if (!lane.SplineContainer.Evaluate(t, out float3 position, out float3 tangent, out float3 up))
            {
                return false;
            }

            Vector3 tangentVector = tangent;
            if (!IsFinite(tangentVector) || tangentVector.sqrMagnitude <= 0.000001f)
            {
                float fallbackT = t <= 0.5f ? Mathf.Min(1f, t + 0.001f) : Mathf.Max(0f, t - 0.001f);
                tangentVector = lane.SplineContainer.EvaluateTangent(fallbackT);
            }

            Vector3 forward = SafeNormalize(tangentVector, Vector3.forward);
            Vector3 upVector = SafeNormalize((Vector3)up, Vector3.up);
            if (reverse)
            {
                forward = -forward;
            }

            Vector3 right = SafeNormalize(Vector3.Cross(upVector, forward), Vector3.right);
            float curvature = EvaluateWorldCurvature(lane, lut, sourceDistance);
            pose = new RoadLanePose
            {
                splinePosition = position,
                position = (Vector3)position + right * lane.LateralOffset + upVector * lane.VerticalOffset,
                forward = forward,
                up = upVector,
                curvature = curvature,
                distance = directedDistance,
                normalizedT = reverse ? 1f - t : t,
                lateralOffset = lane.LateralOffset,
                verticalOffset = lane.VerticalOffset
            };
            return IsFinite(pose.position) && IsFinite(pose.forward) && IsFinite(pose.up);
        }

        public bool TryGetNearestPoint(
            RoadLane lane,
            Vector3 worldPosition,
            bool reverse,
            out RoadLaneNearestPoint nearest)
        {
            nearest = default;
            if (!TryCreateLut(lane, out ArcLengthLut lut))
            {
                return false;
            }

            float3 nearestSpline;
            float t;
            using (NativeSpline nativeSpline = new NativeSpline(
                       lane.Spline,
                       lane.SplineContainer.transform.localToWorldMatrix,
                       true,
                       Allocator.Temp))
            {
                SplineUtility.GetNearestPoint(nativeSpline, worldPosition, out nearestSpline, out t, 8, 3);
            }

            float sourceDistance = lut.TToDistance(t);
            float directedDistance = reverse ? lut.length - sourceDistance : sourceDistance;
            if (!TryEvaluate(lane, directedDistance, reverse, out RoadLanePose pose))
            {
                return false;
            }

            nearest = new RoadLaneNearestPoint
            {
                lane = lane,
                position = pose.position,
                forward = pose.forward,
                up = pose.up,
                distanceAlongLane = pose.distance,
                distanceToLane = Vector3.Distance(worldPosition, pose.position),
                normalizedT = pose.normalizedT
            };
            return true;
        }

        public List<RoadLanePose> SampleEqualDistance(RoadLane lane, float spacing, bool reverse)
        {
            List<RoadLanePose> result = new List<RoadLanePose>();
            float length = GetLength(lane);
            if (length <= 0.0001f)
            {
                return result;
            }

            spacing = Mathf.Max(0.1f, spacing);
            for (float distance = 0f; distance < length; distance += spacing)
            {
                if (TryEvaluate(lane, distance, reverse, out RoadLanePose pose))
                {
                    result.Add(pose);
                }
            }

            if (result.Count == 0 || Mathf.Abs(result[result.Count - 1].distance - length) > 0.001f)
            {
                if (TryEvaluate(lane, length, reverse, out RoadLanePose endPose))
                {
                    result.Add(endPose);
                }
            }

            return result;
        }

        private static float EvaluateWorldCurvature(RoadLane lane, ArcLengthLut lut, float sourceDistance)
        {
            float sampleStep = Mathf.Min(Mathf.Clamp(lut.length * 0.005f, 0.25f, 1f), lut.length * 0.5f);
            if (sampleStep <= 0.0001f)
            {
                return 0f;
            }

            float beforeDistance = sourceDistance - sampleStep;
            float centerDistance = sourceDistance;
            float afterDistance = sourceDistance + sampleStep;
            if (beforeDistance < 0f)
            {
                beforeDistance = 0f;
                centerDistance = Mathf.Min(lut.length, sampleStep);
                afterDistance = Mathf.Min(lut.length, sampleStep * 2f);
            }
            else if (afterDistance > lut.length)
            {
                afterDistance = lut.length;
                centerDistance = Mathf.Max(0f, lut.length - sampleStep);
                beforeDistance = Mathf.Max(0f, lut.length - sampleStep * 2f);
            }

            if (centerDistance - beforeDistance <= 0.0001f ||
                afterDistance - centerDistance <= 0.0001f)
            {
                return 0f;
            }

            Vector3 before = lane.SplineContainer.EvaluatePosition(lut.DistanceToT(beforeDistance));
            Vector3 center = lane.SplineContainer.EvaluatePosition(lut.DistanceToT(centerDistance));
            Vector3 after = lane.SplineContainer.EvaluatePosition(lut.DistanceToT(afterDistance));
            Vector3 incoming = center - before;
            Vector3 outgoing = after - center;
            float incomingLength = incoming.magnitude;
            float outgoingLength = outgoing.magnitude;
            if (!IsFinite(before) ||
                !IsFinite(center) ||
                !IsFinite(after) ||
                incomingLength <= 0.0001f ||
                outgoingLength <= 0.0001f)
            {
                return 0f;
            }

            float angleRadians = Vector3.Angle(incoming, outgoing) * Mathf.Deg2Rad;
            return angleRadians / ((incomingLength + outgoingLength) * 0.5f);
        }

        private static bool TryCreateLut(RoadLane lane, out ArcLengthLut lut)
        {
            lut = default;
            if (lane == null || lane.SplineContainer == null || lane.Spline == null || lane.Spline.Count < 2)
            {
                return false;
            }

            float estimatedLength = lane.SplineContainer.CalculateLength();
            if (!float.IsFinite(estimatedLength) || estimatedLength <= 0.0001f)
            {
                return false;
            }

            int steps = Mathf.Clamp(Mathf.CeilToInt(estimatedLength * 4f), MinimumLutSteps, MaximumLutSteps);
            float[] times = new float[steps + 1];
            float[] distances = new float[steps + 1];
            Vector3 previous = lane.SplineContainer.EvaluatePosition(0f);
            float accumulated = 0f;
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 current = lane.SplineContainer.EvaluatePosition(t);
                if (!IsFinite(current))
                {
                    return false;
                }

                accumulated += Vector3.Distance(previous, current);
                times[i] = t;
                distances[i] = accumulated;
                previous = current;
            }

            if (accumulated <= 0.0001f)
            {
                return false;
            }

            lut = new ArcLengthLut(times, distances, accumulated);
            return true;
        }

        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            return IsFinite(value) && value.sqrMagnitude > 0.000001f ? value.normalized : fallback;
        }

        public static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private readonly struct ArcLengthLut
        {
            private readonly float[] times;
            private readonly float[] distances;
            public readonly float length;

            public ArcLengthLut(float[] times, float[] distances, float length)
            {
                this.times = times;
                this.distances = distances;
                this.length = length;
            }

            public float DistanceToT(float distance)
            {
                distance = Mathf.Clamp(distance, 0f, length);
                int high = Array.BinarySearch(distances, distance);
                if (high >= 0)
                {
                    return times[high];
                }

                high = Mathf.Clamp(~high, 1, distances.Length - 1);
                int low = high - 1;
                float range = distances[high] - distances[low];
                float alpha = range <= 0.000001f ? 0f : (distance - distances[low]) / range;
                return Mathf.Lerp(times[low], times[high], alpha);
            }

            public float TToDistance(float t)
            {
                t = Mathf.Clamp01(t);
                float scaled = t * (times.Length - 1);
                int low = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, times.Length - 1);
                int high = Mathf.Min(low + 1, times.Length - 1);
                return Mathf.Lerp(distances[low], distances[high], scaled - low);
            }
        }
    }
}
