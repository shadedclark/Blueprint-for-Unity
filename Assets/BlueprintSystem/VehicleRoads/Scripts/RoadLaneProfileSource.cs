using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads
{
    public enum RoadLaneProfileAlignment
    {
        Center,
        LeftEdge,
        RightEdge
    }

    [Serializable]
    public sealed class RoadLaneProfileControlPoint
    {
        public string pointId = string.Empty;
        public RoadLaneProfile profileOverride;
        public bool forceTopologyBreak;
        [HideInInspector] public Vector3 sourceLocalPosition;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplineContainer))]
    [AddComponentMenu("Vehicle Road/Road Network/Lane Profile Source")]
    public sealed class RoadLaneProfileSource : MonoBehaviour
    {
        [SerializeField] private string sourceId = string.Empty;
        [SerializeField] private RoadLaneProfile profile;
        [SerializeField] private RoadLaneProfileAlignment alignment = RoadLaneProfileAlignment.Center;
        [SerializeField] private bool refreshBeforeBake = true;
        [SerializeField] private List<RoadLaneProfileControlPoint> controlPoints =
            new List<RoadLaneProfileControlPoint>();

        private SplineContainer splineContainer;

        public string SourceId
        {
            get => sourceId ?? string.Empty;
            set => sourceId = value ?? string.Empty;
        }

        public RoadLaneProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        public RoadLaneProfileAlignment Alignment
        {
            get => alignment;
            set => alignment = value;
        }

        public bool RefreshBeforeBake
        {
            get => refreshBeforeBake;
            set => refreshBeforeBake = value;
        }

        public IList<RoadLaneProfileControlPoint> ControlPoints => controlPoints;

        public SplineContainer SplineContainer
        {
            get
            {
                if (splineContainer == null)
                {
                    splineContainer = GetComponent<SplineContainer>();
                }

                return splineContainer;
            }
        }

        public bool RefreshManagedLanes(
            Action<GameObject> registerCreatedObject,
            Action<UnityEngine.Object> registerModifiedObject,
            out string error)
        {
            using RoadNetworkProfiler.Scope ignored = RoadNetworkProfiler.Sample(RoadNetworkProfiler.RefreshProfile);
            error = string.Empty;
            if (profile == null)
            {
                error = "Lane Profile Source has no profile.";
                return false;
            }

            Spline sourceSpline = SplineContainer == null ? null : SplineContainer.Spline;
            if (sourceSpline == null || sourceSpline.Count < 2)
            {
                error = "Lane Profile Source spline must contain at least two knots.";
                return false;
            }

            RoadLaneNetwork network = GetComponentInParent<RoadLaneNetwork>();
            if (network == null)
            {
                error = "Lane Profile Source must be a child of a RoadLaneNetwork.";
                return false;
            }

            string safeSourceId = RoadLaneNetwork.SanitizeId(
                string.IsNullOrWhiteSpace(sourceId) ? gameObject.name : sourceId);
            sourceId = safeSourceId;
            SynchronizeControlPoints();
            if (UsesVariableProfiles())
            {
                return RefreshVariableProfileLanes(
                    safeSourceId,
                    registerCreatedObject,
                    registerModifiedObject,
                    out error);
            }

            Dictionary<string, RoadLane> existing = CollectManagedLanes(safeSourceId);
            HashSet<string> activeEntryIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> activeKeys = new HashSet<string>(StringComparer.Ordinal);
            float totalWidth = profile.TotalWidth;
            float cursor = GetInitialCursor(totalWidth);

            for (int i = 0; i < profile.Entries.Count; i++)
            {
                RoadLaneProfileEntry entry = profile.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.entryId))
                {
                    continue;
                }

                string entryId = RoadLaneNetwork.SanitizeId(entry.entryId);
                if (!activeEntryIds.Add(entryId))
                {
                    error = "Lane Profile contains duplicate entryId '" + entryId + "'.";
                    return false;
                }
                string managedKey = CreateManagedLaneKey(entryId, string.Empty);
                activeKeys.Add(managedKey);

                float laneWidth = Mathf.Max(0.1f, entry.width);
                float offset = cursor + laneWidth * 0.5f;
                cursor += laneWidth;
                if (!existing.TryGetValue(managedKey, out RoadLane lane) || lane == null)
                {
                    GameObject laneObject = new GameObject(safeSourceId + "_" + entryId);
                    laneObject.transform.SetParent(transform, false);
                    registerCreatedObject?.Invoke(laneObject);
                    laneObject.AddComponent<SplineContainer>();
                    lane = laneObject.AddComponent<RoadLane>();
                    existing[managedKey] = lane;
                }

                lane.ClearManagedProfileOrphaned();
                if (lane.ManagedProfileLocked)
                {
                    registerModifiedObject?.Invoke(lane);
                    lane.MarkManagedProfileStale();
                    continue;
                }

                registerModifiedObject?.Invoke(lane);
                registerModifiedObject?.Invoke(lane.SplineContainer);
                lane.LaneId = safeSourceId + "_" + entryId;
                lane.SetKind(RoadLaneKind.Standard);
                lane.TravelDirection = entry.direction;
                lane.SpeedLimit = entry.speedLimit;
                lane.Open = entry.open;
                lane.ConnectionMode = entry.connectionMode;
                lane.Width = laneWidth;
                lane.TagMask = entry.tags;
                lane.AllowedAgents = entry.allowedAgents;
                lane.AllowLaneChangeLeft = entry.allowLaneChangeLeft;
                lane.AllowLaneChangeRight = entry.allowLaneChangeRight;
                lane.ConfigureManagedProfile(safeSourceId, entryId, laneWidth, entry.tags, entry.allowedAgents);
                lane.SetWidthKeys(null);
                lane.SplineContainer.Spline = CreateOffsetSpline(lane.SplineContainer.transform, offset);
            }

            foreach (KeyValuePair<string, RoadLane> pair in existing)
            {
                if (!activeKeys.Contains(pair.Key) && pair.Value != null)
                {
                    registerModifiedObject?.Invoke(pair.Value);
                    pair.Value.MarkManagedProfileOrphaned();
                }
            }

            return true;
        }

        public void SynchronizeControlPoints()
        {
            controlPoints ??= new List<RoadLaneProfileControlPoint>();
            Spline spline = SplineContainer == null ? null : SplineContainer.Spline;
            if (spline == null)
            {
                controlPoints.Clear();
                return;
            }

            List<RoadLaneProfileControlPoint> previous =
                new List<RoadLaneProfileControlPoint>(controlPoints);
            HashSet<RoadLaneProfileControlPoint> used = new HashSet<RoadLaneProfileControlPoint>();
            List<RoadLaneProfileControlPoint> synchronized =
                new List<RoadLaneProfileControlPoint>(spline.Count);
            bool samePointCount = previous.Count == spline.Count;
            for (int knotIndex = 0; knotIndex < spline.Count; knotIndex++)
            {
                Vector3 localPosition = spline[knotIndex].Position;
                RoadLaneProfileControlPoint point = null;
                float bestDistance = 0.0001f;
                for (int i = 0; i < previous.Count; i++)
                {
                    RoadLaneProfileControlPoint candidate = previous[i];
                    if (candidate == null || used.Contains(candidate))
                    {
                        continue;
                    }

                    float distance = (candidate.sourceLocalPosition - localPosition).sqrMagnitude;
                    if (distance <= bestDistance)
                    {
                        bestDistance = distance;
                        point = candidate;
                    }
                }

                if (point == null &&
                    samePointCount &&
                    knotIndex < previous.Count &&
                    !used.Contains(previous[knotIndex]))
                {
                    point = previous[knotIndex];
                }

                point ??= new RoadLaneProfileControlPoint();
                used.Add(point);
                point.pointId = string.IsNullOrWhiteSpace(point.pointId)
                    ? CreateUniquePointId(previous, synchronized)
                    : RoadLaneNetwork.SanitizeId(point.pointId);
                point.sourceLocalPosition = localPosition;
                synchronized.Add(point);
            }

            controlPoints = synchronized;
        }

        public bool SetControlPoint(
            int knotIndex,
            RoadLaneProfile profileOverride,
            bool forceTopologyBreak,
            out string error)
        {
            SynchronizeControlPoints();
            if (knotIndex < 0 || knotIndex >= controlPoints.Count)
            {
                error = "Profile control point knot index is out of range.";
                return false;
            }

            controlPoints[knotIndex].profileOverride = profileOverride;
            controlPoints[knotIndex].forceTopologyBreak = forceTopologyBreak;
            error = string.Empty;
            return true;
        }

        public bool TryGetEntry(string entryId, out RoadLaneProfileEntry entry)
        {
            entry = null;
            if (profile == null)
            {
                return false;
            }

            string safeId = RoadLaneNetwork.SanitizeId(entryId);
            for (int i = 0; i < profile.Entries.Count; i++)
            {
                RoadLaneProfileEntry candidate = profile.Entries[i];
                if (candidate != null &&
                    string.Equals(RoadLaneNetwork.SanitizeId(candidate.entryId), safeId, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        private Dictionary<string, RoadLane> CollectManagedLanes(string safeSourceId)
        {
            Dictionary<string, RoadLane> result = new Dictionary<string, RoadLane>(StringComparer.Ordinal);
            RoadLane[] lanes = GetComponentsInChildren<RoadLane>(true);
            for (int i = 0; i < lanes.Length; i++)
            {
                RoadLane lane = lanes[i];
                if (lane != null &&
                    string.Equals(lane.ManagedProfileSourceId, safeSourceId, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(lane.ManagedProfileEntryId))
                {
                    string key = CreateManagedLaneKey(
                        lane.ManagedProfileEntryId,
                        lane.ManagedProfileRunStartPointId);
                    result[key] = lane;
                }
            }

            return result;
        }

        private bool RefreshVariableProfileLanes(
            string safeSourceId,
            Action<GameObject> registerCreatedObject,
            Action<UnityEngine.Object> registerModifiedObject,
            out string error)
        {
            error = string.Empty;
            Spline sourceSpline = SplineContainer.Spline;
            Dictionary<string, RoadLane> existing = CollectManagedLanes(safeSourceId);
            HashSet<string> activeKeys = new HashSet<string>(StringComparer.Ordinal);
            List<ProfilePointState> states = BuildProfilePointStates(out error);
            if (states == null)
            {
                return false;
            }
            HashSet<string> entryIds = new HashSet<string>(StringComparer.Ordinal);
            for (int pointIndex = 0; pointIndex < states.Count; pointIndex++)
            {
                foreach (string entryId in states[pointIndex].entries.Keys)
                {
                    entryIds.Add(entryId);
                }
            }

            foreach (string entryId in entryIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                int pointIndex = 0;
                while (pointIndex < states.Count)
                {
                    while (pointIndex < states.Count && !states[pointIndex].entries.ContainsKey(entryId))
                    {
                        pointIndex++;
                    }

                    if (pointIndex >= states.Count)
                    {
                        break;
                    }

                    int firstActive = pointIndex;
                    int lastActive = pointIndex;
                    pointIndex++;
                    while (pointIndex < states.Count &&
                           states[pointIndex].entries.ContainsKey(entryId) &&
                           !controlPoints[pointIndex].forceTopologyBreak)
                    {
                        lastActive = pointIndex;
                        pointIndex++;
                    }

                    bool taperFromPrevious =
                        firstActive > 0 &&
                        !states[firstActive - 1].entries.ContainsKey(entryId);
                    bool taperToNext =
                        lastActive + 1 < states.Count &&
                        !states[lastActive + 1].entries.ContainsKey(entryId);
                    bool breakAtNext =
                        lastActive + 1 < states.Count &&
                        controlPoints[lastActive + 1].forceTopologyBreak &&
                        states[lastActive + 1].entries.ContainsKey(entryId);
                    int startPoint = taperFromPrevious ? firstActive - 1 : firstActive;
                    int endPoint = taperToNext || breakAtNext ? lastActive + 1 : lastActive;
                    if (endPoint <= startPoint)
                    {
                        continue;
                    }

                    string runStartPointId = controlPoints[firstActive].pointId;
                    string managedKey = CreateManagedLaneKey(entryId, runStartPointId);
                    activeKeys.Add(managedKey);
                    RoadLane lane = GetOrCreateManagedLane(
                        safeSourceId,
                        entryId,
                        runStartPointId,
                        managedKey,
                        existing,
                        registerCreatedObject);
                    lane.ClearManagedProfileOrphaned();
                    if (lane.ManagedProfileLocked)
                    {
                        registerModifiedObject?.Invoke(lane);
                        lane.MarkManagedProfileStale();
                        continue;
                    }

                    RoadLaneProfileEntry representative = states[firstActive].entries[entryId];
                    registerModifiedObject?.Invoke(lane);
                    registerModifiedObject?.Invoke(lane.SplineContainer);
                    lane.LaneId = safeSourceId + "_" + entryId + "_" + runStartPointId;
                    ApplyEntryProperties(lane, representative);
                    lane.ConfigureManagedProfile(
                        safeSourceId,
                        entryId,
                        representative.width,
                        representative.tags,
                        representative.allowedAgents,
                        runStartPointId);
                    BuildVariableLaneSpline(
                        lane,
                        sourceSpline,
                        states,
                        entryId,
                        firstActive,
                        lastActive,
                        startPoint,
                        endPoint);
                }
            }

            foreach (KeyValuePair<string, RoadLane> pair in existing)
            {
                if (!activeKeys.Contains(pair.Key) && pair.Value != null)
                {
                    registerModifiedObject?.Invoke(pair.Value);
                    pair.Value.MarkManagedProfileOrphaned();
                }
            }

            return true;
        }

        private List<ProfilePointState> BuildProfilePointStates(out string error)
        {
            error = string.Empty;
            List<ProfilePointState> states = new List<ProfilePointState>(controlPoints.Count);
            for (int i = 0; i < controlPoints.Count; i++)
            {
                RoadLaneProfile pointProfile = controlPoints[i].profileOverride != null
                    ? controlPoints[i].profileOverride
                    : profile;
                if (!TryValidateProfileEntryIds(pointProfile, i, out error))
                {
                    return null;
                }

                states.Add(new ProfilePointState(pointProfile, alignment));
            }

            return states;
        }

        private static bool TryValidateProfileEntryIds(
            RoadLaneProfile pointProfile,
            int pointIndex,
            out string error)
        {
            error = string.Empty;
            if (pointProfile == null)
            {
                return true;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < pointProfile.Entries.Count; i++)
            {
                RoadLaneProfileEntry entry = pointProfile.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.entryId))
                {
                    continue;
                }

                string entryId = RoadLaneNetwork.SanitizeId(entry.entryId);
                if (ids.Add(entryId))
                {
                    continue;
                }

                error = "Lane Profile control point " + pointIndex +
                        " contains duplicate entryId '" + entryId + "'.";
                return false;
            }

            return true;
        }

        private RoadLane GetOrCreateManagedLane(
            string safeSourceId,
            string entryId,
            string runStartPointId,
            string managedKey,
            Dictionary<string, RoadLane> existing,
            Action<GameObject> registerCreatedObject)
        {
            if (existing.TryGetValue(managedKey, out RoadLane lane) && lane != null)
            {
                return lane;
            }

            GameObject laneObject = new GameObject(
                safeSourceId + "_" + entryId + "_" + runStartPointId);
            laneObject.transform.SetParent(transform, false);
            registerCreatedObject?.Invoke(laneObject);
            laneObject.AddComponent<SplineContainer>();
            lane = laneObject.AddComponent<RoadLane>();
            existing[managedKey] = lane;
            return lane;
        }

        private static void ApplyEntryProperties(RoadLane lane, RoadLaneProfileEntry entry)
        {
            lane.SetKind(RoadLaneKind.Standard);
            lane.TravelDirection = entry.direction;
            lane.SpeedLimit = entry.speedLimit;
            lane.Open = entry.open;
            lane.ConnectionMode = entry.connectionMode;
            lane.Width = Mathf.Max(0.1f, entry.width);
            lane.TagMask = entry.tags;
            lane.AllowedAgents = entry.allowedAgents;
            lane.AllowLaneChangeLeft = entry.allowLaneChangeLeft;
            lane.AllowLaneChangeRight = entry.allowLaneChangeRight;
        }

        private void BuildVariableLaneSpline(
            RoadLane lane,
            Spline sourceSpline,
            IReadOnlyList<ProfilePointState> states,
            string entryId,
            int firstActive,
            int lastActive,
            int startPoint,
            int endPoint)
        {
            Spline result = new Spline(endPoint - startPoint + 1, false);
            List<RoadLaneWidthKey> widthKeys = new List<RoadLaneWidthKey>();
            List<float> widths = new List<float>();
            float previousOffset = states[firstActive].offsets[entryId];
            for (int sourceIndex = startPoint; sourceIndex <= endPoint; sourceIndex++)
            {
                bool active = states[sourceIndex].entries.TryGetValue(
                    entryId,
                    out RoadLaneProfileEntry entry);
                float offset = active
                    ? states[sourceIndex].offsets[entryId]
                    : states[sourceIndex].GetNearestOffset(previousOffset);
                previousOffset = offset;
                float width = active ? Mathf.Max(0.1f, entry.width) : 0.1f;
                BezierKnot sourceKnot = sourceSpline[sourceIndex];
                float normalizedT = SplineUtility.GetNormalizedInterpolation(
                    sourceSpline,
                    sourceIndex,
                    PathIndexUnit.Knot);
                Vector3 position = SplineContainer.transform.TransformPoint(sourceKnot.Position);
                Vector3 forward = SplineContainer.EvaluateTangent(normalizedT);
                Vector3 up = SplineContainer.EvaluateUpVector(normalizedT);
                if (!UnitySplineRoadLaneGeometry.IsFinite(forward) ||
                    forward.sqrMagnitude <= 0.000001f)
                {
                    forward = GetFallbackForward(sourceSpline, sourceIndex);
                }

                if (!UnitySplineRoadLaneGeometry.IsFinite(up) || up.sqrMagnitude <= 0.000001f)
                {
                    up = Vector3.up;
                }

                Vector3 right = Vector3.Cross(up.normalized, forward.normalized);
                if (right.sqrMagnitude <= 0.000001f)
                {
                    right = Vector3.right;
                }

                BezierKnot copied = sourceKnot;
                Vector3 localPosition = lane.SplineContainer.transform.InverseTransformPoint(
                    position + right.normalized * offset);
                copied.Position = new float3(localPosition.x, localPosition.y, localPosition.z);
                result.Add(
                    copied,
                    sourceSpline.GetTangentMode(sourceIndex),
                    sourceSpline.GetAutoSmoothTension(sourceIndex));
                widths.Add(width);
            }

            result.Closed = false;
            for (int i = 0; i < result.Count; i++)
            {
                widthKeys.Add(new RoadLaneWidthKey
                {
                    normalizedDistance = SplineUtility.GetNormalizedInterpolation(
                        result,
                        i,
                        PathIndexUnit.Knot),
                    width = widths[i]
                });
            }
            lane.SplineContainer.Spline = result;
            lane.SetWidthKeys(widthKeys);
        }

        private bool UsesVariableProfiles()
        {
            if (controlPoints == null)
            {
                return false;
            }

            for (int i = 0; i < controlPoints.Count; i++)
            {
                RoadLaneProfileControlPoint point = controlPoints[i];
                if (point != null && (point.profileOverride != null || point.forceTopologyBreak))
                {
                    return true;
                }
            }

            return false;
        }

        private static string CreateManagedLaneKey(string entryId, string runStartPointId)
        {
            return RoadLaneNetwork.SanitizeId(entryId) + "|" +
                   RoadLaneNetwork.SanitizeId(runStartPointId);
        }

        private static string CreateUniquePointId(
            IReadOnlyList<RoadLaneProfileControlPoint> previous,
            IReadOnlyList<RoadLaneProfileControlPoint> synchronized)
        {
            HashSet<string> ids = new HashSet<string>(
                previous
                    .Where(point => point != null && !string.IsNullOrWhiteSpace(point.pointId))
                    .Select(point => point.pointId),
                StringComparer.Ordinal);
            for (int i = 0; i < synchronized.Count; i++)
            {
                if (synchronized[i] != null && !string.IsNullOrWhiteSpace(synchronized[i].pointId))
                {
                    ids.Add(synchronized[i].pointId);
                }
            }

            int suffix = 0;
            string candidate;
            do
            {
                candidate = "p_" + suffix.ToString("D3");
                suffix++;
            }
            while (ids.Contains(candidate));
            return candidate;
        }

        private float GetInitialCursor(float totalWidth)
        {
            return alignment switch
            {
                RoadLaneProfileAlignment.LeftEdge => 0f,
                RoadLaneProfileAlignment.RightEdge => -totalWidth,
                _ => -totalWidth * 0.5f
            };
        }

        private Spline CreateOffsetSpline(Transform targetTransform, float offset)
        {
            Spline source = SplineContainer.Spline;
            Spline result = new Spline(source.Count, false);
            for (int i = 0; i < source.Count; i++)
            {
                BezierKnot knot = source[i];
                float normalizedT = SplineUtility.GetNormalizedInterpolation(source, i, PathIndexUnit.Knot);
                Vector3 position = SplineContainer.transform.TransformPoint(knot.Position);
                Vector3 forward = SplineContainer.EvaluateTangent(normalizedT);
                Vector3 up = SplineContainer.EvaluateUpVector(normalizedT);
                if (!UnitySplineRoadLaneGeometry.IsFinite(forward) || forward.sqrMagnitude <= 0.000001f)
                {
                    forward = GetFallbackForward(source, i);
                }

                if (!UnitySplineRoadLaneGeometry.IsFinite(up) || up.sqrMagnitude <= 0.000001f)
                {
                    up = Vector3.up;
                }

                Vector3 right = Vector3.Cross(up.normalized, forward.normalized);
                if (right.sqrMagnitude <= 0.000001f)
                {
                    right = Vector3.right;
                }

                Vector3 targetPosition = targetTransform.InverseTransformPoint(position + right.normalized * offset);
                BezierKnot copied = knot;
                copied.Position = new float3(targetPosition.x, targetPosition.y, targetPosition.z);
                result.Add(copied, source.GetTangentMode(i), source.GetAutoSmoothTension(i));
            }

            result.Closed = false;
            return result;
        }

        private Vector3 GetFallbackForward(Spline source, int index)
        {
            int previous = Mathf.Max(0, index - 1);
            int next = Mathf.Min(source.Count - 1, index + 1);
            Vector3 a = SplineContainer.transform.TransformPoint(source[previous].Position);
            Vector3 b = SplineContainer.transform.TransformPoint(source[next].Position);
            Vector3 delta = b - a;
            return delta.sqrMagnitude > 0.000001f ? delta.normalized : transform.forward;
        }

        private void Reset()
        {
            splineContainer = GetComponent<SplineContainer>();
            sourceId = gameObject.name;
            if (splineContainer != null && splineContainer.Spline != null)
            {
                splineContainer.Spline.Closed = false;
            }
        }

        private void OnValidate()
        {
            sourceId ??= string.Empty;
            controlPoints ??= new List<RoadLaneProfileControlPoint>();
            if (SplineContainer != null && SplineContainer.Spline != null)
            {
                SplineContainer.Spline.Closed = false;
            }
        }

        private sealed class ProfilePointState
        {
            public readonly Dictionary<string, RoadLaneProfileEntry> entries =
                new Dictionary<string, RoadLaneProfileEntry>(StringComparer.Ordinal);
            public readonly Dictionary<string, float> offsets =
                new Dictionary<string, float>(StringComparer.Ordinal);

            public ProfilePointState(
                RoadLaneProfile pointProfile,
                RoadLaneProfileAlignment pointAlignment)
            {
                if (pointProfile == null)
                {
                    return;
                }

                float totalWidth = pointProfile.TotalWidth;
                float cursor = pointAlignment switch
                {
                    RoadLaneProfileAlignment.LeftEdge => 0f,
                    RoadLaneProfileAlignment.RightEdge => -totalWidth,
                    _ => -totalWidth * 0.5f
                };
                for (int i = 0; i < pointProfile.Entries.Count; i++)
                {
                    RoadLaneProfileEntry entry = pointProfile.Entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.entryId))
                    {
                        continue;
                    }

                    string entryId = RoadLaneNetwork.SanitizeId(entry.entryId);
                    float width = Mathf.Max(0.1f, entry.width);
                    entries[entryId] = entry;
                    offsets[entryId] = cursor + width * 0.5f;
                    cursor += width;
                }
            }

            public float GetNearestOffset(float reference)
            {
                float result = reference;
                float best = float.PositiveInfinity;
                foreach (float candidate in offsets.Values)
                {
                    float distance = Mathf.Abs(candidate - reference);
                    if (distance < best)
                    {
                        best = distance;
                        result = candidate;
                    }
                }

                return result;
            }
        }
    }
}
