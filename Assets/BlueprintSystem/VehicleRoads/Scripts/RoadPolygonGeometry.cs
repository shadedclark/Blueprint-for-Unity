using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    public static class RoadPolygonGeometry
    {
        private const float Epsilon = 0.00001f;

        public static bool TryTriangulate(IReadOnlyList<Vector2> vertices, List<int> triangles, out string error)
        {
            triangles.Clear();
            error = string.Empty;
            if (vertices == null || vertices.Count < 3)
            {
                error = "Polygon requires at least three vertices.";
                return false;
            }

            if (HasSelfIntersection(vertices))
            {
                error = "Polygon edges intersect.";
                return false;
            }

            List<int> remaining = new List<int>(vertices.Count);
            bool clockwise = SignedArea(vertices) < 0f;
            for (int i = 0; i < vertices.Count; i++)
            {
                remaining.Add(clockwise ? vertices.Count - 1 - i : i);
            }

            int safety = vertices.Count * vertices.Count;
            while (remaining.Count > 3 && safety-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int previous = remaining[(i - 1 + remaining.Count) % remaining.Count];
                    int current = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];
                    Vector2 a = vertices[previous];
                    Vector2 b = vertices[current];
                    Vector2 c = vertices[next];
                    if (Cross(b - a, c - b) <= Epsilon || ContainsOtherVertex(vertices, remaining, previous, current, next))
                    {
                        continue;
                    }

                    triangles.Add(previous);
                    triangles.Add(current);
                    triangles.Add(next);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped)
                {
                    error = "Polygon could not be triangulated.";
                    triangles.Clear();
                    return false;
                }
            }

            if (remaining.Count == 3)
            {
                triangles.Add(remaining[0]);
                triangles.Add(remaining[1]);
                triangles.Add(remaining[2]);
            }

            return triangles.Count >= 3;
        }

        public static bool ContainsPoint(IReadOnlyList<Vector2> vertices, Vector2 point)
        {
            if (vertices == null || vertices.Count < 3)
            {
                return false;
            }

            bool inside = false;
            int previous = vertices.Count - 1;
            for (int i = 0; i < vertices.Count; previous = i++)
            {
                Vector2 a = vertices[i];
                Vector2 b = vertices[previous];
                if (DistanceSquaredToSegment(point, a, b, out _) <= Epsilon * Epsilon)
                {
                    return true;
                }

                bool intersects = (a.y > point.y) != (b.y > point.y) &&
                                  point.x < (b.x - a.x) * (point.y - a.y) /
                                  Mathf.Max(Epsilon, b.y - a.y) + a.x;
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static Vector2 ClosestPointOnBoundary(
            IReadOnlyList<Vector2> vertices,
            Vector2 point,
            out float distance)
        {
            Vector2 best = point;
            float bestSquared = float.PositiveInfinity;
            if (vertices != null)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector2 a = vertices[i];
                    Vector2 b = vertices[(i + 1) % vertices.Count];
                    float squared = DistanceSquaredToSegment(point, a, b, out Vector2 candidate);
                    if (squared < bestSquared)
                    {
                        bestSquared = squared;
                        best = candidate;
                    }
                }
            }

            distance = Mathf.Sqrt(bestSquared);
            return best;
        }

        public static bool TryFindContainingTriangle(
            IReadOnlyList<Vector2> vertices,
            IReadOnlyList<int> triangles,
            Vector2 point,
            out int triangleIndex)
        {
            triangleIndex = -1;
            if (vertices == null || triangles == null)
            {
                return false;
            }

            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                if (PointInTriangle(
                        point,
                        vertices[triangles[i]],
                        vertices[triangles[i + 1]],
                        vertices[triangles[i + 2]]))
                {
                    triangleIndex = i / 3;
                    return true;
                }
            }

            return false;
        }

        public static bool HasSelfIntersection(IReadOnlyList<Vector2> vertices)
        {
            if (vertices == null || vertices.Count < 4)
            {
                return false;
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 a0 = vertices[i];
                Vector2 a1 = vertices[(i + 1) % vertices.Count];
                for (int j = i + 1; j < vertices.Count; j++)
                {
                    if (j == i ||
                        (j + 1) % vertices.Count == i ||
                        (i + 1) % vertices.Count == j)
                    {
                        continue;
                    }

                    Vector2 b0 = vertices[j];
                    Vector2 b1 = vertices[(j + 1) % vertices.Count];
                    if (SegmentsIntersect(a0, a1, b0, b1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsOtherVertex(
            IReadOnlyList<Vector2> vertices,
            List<int> remaining,
            int a,
            int b,
            int c)
        {
            for (int i = 0; i < remaining.Count; i++)
            {
                int index = remaining[i];
                if (index != a && index != b && index != c &&
                    PointInTriangle(vertices[index], vertices[a], vertices[b], vertices[c]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float c0 = Cross(b - a, p - a);
            float c1 = Cross(c - b, p - b);
            float c2 = Cross(a - c, p - c);
            bool hasNegative = c0 < -Epsilon || c1 < -Epsilon || c2 < -Epsilon;
            bool hasPositive = c0 > Epsilon || c1 > Epsilon || c2 > Epsilon;
            return !(hasNegative && hasPositive);
        }

        private static float SignedArea(IReadOnlyList<Vector2> vertices)
        {
            float area = 0f;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 a = vertices[i];
                Vector2 b = vertices[(i + 1) % vertices.Count];
                area += a.x * b.y - b.x * a.y;
            }

            return area * 0.5f;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float abC = Cross(b - a, c - a);
            float abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c);
            float cdB = Cross(d - c, b - c);
            return abC * abD < -Epsilon && cdA * cdB < -Epsilon;
        }

        private static float DistanceSquaredToSegment(Vector2 point, Vector2 a, Vector2 b, out Vector2 closest)
        {
            Vector2 delta = b - a;
            float denominator = delta.sqrMagnitude;
            float t = denominator <= Epsilon ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, delta) / denominator);
            closest = a + delta * t;
            return (point - closest).sqrMagnitude;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
