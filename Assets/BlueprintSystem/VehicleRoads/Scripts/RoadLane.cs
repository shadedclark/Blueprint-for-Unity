using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads
{
    [Serializable]
    public sealed class RoadLaneWidthKey
    {
        [Range(0f, 1f)] public float normalizedDistance;
        [Min(0.1f)] public float width = 3.5f;
    }

    public enum RoadLaneKind
    {
        Standard,
        Connector
    }

    public enum RoadLaneTravelDirection
    {
        Forward,
        Reverse,
        Bidirectional
    }

    public enum RoadLaneConnectionMode
    {
        Automatic,
        Manual,
        Blocked
    }

    public enum RoadLaneTurn
    {
        None,
        Straight,
        Left,
        Right,
        UTurn
    }

    [Flags]
    public enum RoadLaneTurnMask
    {
        None = 0,
        Straight = 1 << 0,
        Left = 1 << 1,
        Right = 1 << 2,
        UTurn = 1 << 3,
        Default = Straight | Left | Right
    }

    public enum RoadLaneEndpoint
    {
        Start,
        End
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplineContainer))]
    [AddComponentMenu("Vehicle Road/Road Lane")]
    public sealed class RoadLane : MonoBehaviour
    {
        [SerializeField] private string laneId = string.Empty;
        [SerializeField] private RoadLaneKind kind;
        [SerializeField] private RoadLaneTravelDirection travelDirection;
        [SerializeField, Min(0f)] private float speedLimit = 12f;
        [SerializeField, Min(0.1f)] private float width = 3.5f;
        [SerializeField] private List<RoadLaneWidthKey> widthKeys = new List<RoadLaneWidthKey>();
        [SerializeField] private RoadTagMask tagMask = RoadTagMask.Road | RoadTagMask.Vehicle;
        [SerializeField] private RoadAgentMask allowedAgents = RoadAgentMask.MotorVehicles;
        [SerializeField] private bool allowLaneChangeLeft = true;
        [SerializeField] private bool allowLaneChangeRight = true;
        [SerializeField] private bool open = true;
        [SerializeField] private RoadLaneConnectionMode connectionMode;
        [SerializeField] private string manualNextLaneIds = string.Empty;
        [SerializeField] private float lateralOffset;
        [SerializeField] private float verticalOffset;
        [SerializeField, Min(0f)] private float sampleSpacingOverride;

        [Header("Lane Profile")]
        [SerializeField] private string managedProfileSourceId = string.Empty;
        [SerializeField] private string managedProfileEntryId = string.Empty;
        [SerializeField] private string managedProfileRunStartPointId = string.Empty;
        [SerializeField] private bool managedProfileLocked;
        [SerializeField] private bool managedProfileOrphaned;
        [SerializeField] private bool managedProfileStale;

        [Header("Connector")]
        [SerializeField] private string connectorGenerationKey = string.Empty;
        [SerializeField] private string connectorJunctionId = string.Empty;
        [SerializeField] private string sourceLaneId = string.Empty;
        [SerializeField] private string targetLaneId = string.Empty;
        [SerializeField] private RoadLaneTurn turnType;
        [SerializeField, Min(0f)] private float traversalCost;
        [SerializeField] private bool connectorLocked;
        [SerializeField] private bool orphaned;

        private SplineContainer splineContainer;

        public string LaneId
        {
            get => laneId;
            set => laneId = value ?? string.Empty;
        }

        public RoadLaneKind Kind => kind;
        public RoadLaneTravelDirection TravelDirection
        {
            get => travelDirection;
            set => travelDirection = value;
        }

        public float SpeedLimit
        {
            get => Mathf.Max(0f, speedLimit);
            set => speedLimit = Mathf.Max(0f, value);
        }

        public float Width
        {
            get => Mathf.Max(0.1f, width);
            set => width = Mathf.Max(0.1f, value);
        }

        public IList<RoadLaneWidthKey> WidthKeys => widthKeys;

        public RoadTagMask TagMask
        {
            get => tagMask;
            set => tagMask = value;
        }

        public RoadAgentMask AllowedAgents
        {
            get => allowedAgents;
            set => allowedAgents = value;
        }

        public bool AllowLaneChangeLeft
        {
            get => allowLaneChangeLeft;
            set => allowLaneChangeLeft = value;
        }

        public bool AllowLaneChangeRight
        {
            get => allowLaneChangeRight;
            set => allowLaneChangeRight = value;
        }

        public bool Open
        {
            get => open;
            set => open = value;
        }

        public RoadLaneConnectionMode ConnectionMode
        {
            get => connectionMode;
            set => connectionMode = value;
        }

        public string ManualNextLaneIds
        {
            get => manualNextLaneIds ?? string.Empty;
            set => manualNextLaneIds = value ?? string.Empty;
        }

        public float LateralOffset
        {
            get => lateralOffset;
            set => lateralOffset = value;
        }

        public float VerticalOffset
        {
            get => verticalOffset;
            set => verticalOffset = value;
        }

        public float SampleSpacingOverride
        {
            get => Mathf.Max(0f, sampleSpacingOverride);
            set => sampleSpacingOverride = Mathf.Max(0f, value);
        }

        public string ConnectorGenerationKey => connectorGenerationKey ?? string.Empty;
        public string ConnectorJunctionId => connectorJunctionId ?? string.Empty;
        public string SourceLaneId => sourceLaneId ?? string.Empty;
        public string TargetLaneId => targetLaneId ?? string.Empty;
        public RoadLaneTurn TurnType => turnType;
        public float TraversalCost
        {
            get => Mathf.Max(0f, traversalCost);
            set => traversalCost = Mathf.Max(0f, value);
        }

        public bool ConnectorLocked
        {
            get => connectorLocked;
            set => connectorLocked = value;
        }

        public bool Orphaned => orphaned;
        public string ManagedProfileSourceId => managedProfileSourceId ?? string.Empty;
        public string ManagedProfileEntryId => managedProfileEntryId ?? string.Empty;
        public string ManagedProfileRunStartPointId => managedProfileRunStartPointId ?? string.Empty;
        public bool ManagedProfileLocked
        {
            get => managedProfileLocked;
            set => managedProfileLocked = value;
        }

        public bool ManagedProfileOrphaned => managedProfileOrphaned;
        public bool ManagedProfileStale => managedProfileStale;

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

        public Spline Spline => SplineContainer == null ? null : SplineContainer.Spline;

        public bool AllowsAgent(RoadAgentMask agentMask)
        {
            return RoadAgentMaskUtility.Allows(AllowedAgents, agentMask);
        }

        public float EvaluateWidth(float normalizedDistance)
        {
            float t = Mathf.Clamp01(normalizedDistance);
            if (widthKeys == null || widthKeys.Count == 0)
            {
                return Width;
            }

            RoadLaneWidthKey previous = null;
            RoadLaneWidthKey next = null;
            for (int i = 0; i < widthKeys.Count; i++)
            {
                RoadLaneWidthKey key = widthKeys[i];
                if (key == null)
                {
                    continue;
                }

                if (key.normalizedDistance <= t &&
                    (previous == null || key.normalizedDistance > previous.normalizedDistance))
                {
                    previous = key;
                }

                if (key.normalizedDistance >= t &&
                    (next == null || key.normalizedDistance < next.normalizedDistance))
                {
                    next = key;
                }
            }

            if (previous == null)
            {
                return next == null ? Width : Mathf.Max(0.1f, next.width);
            }

            if (next == null)
            {
                return Mathf.Max(0.1f, previous.width);
            }

            float range = next.normalizedDistance - previous.normalizedDistance;
            if (range <= 0.000001f)
            {
                return Mathf.Max(0.1f, next.width);
            }

            float blend = Mathf.InverseLerp(previous.normalizedDistance, next.normalizedDistance, t);
            return Mathf.Lerp(
                Mathf.Max(0.1f, previous.width),
                Mathf.Max(0.1f, next.width),
                blend);
        }

        public void SetWidthKeys(IEnumerable<RoadLaneWidthKey> keys)
        {
            widthKeys ??= new List<RoadLaneWidthKey>();
            widthKeys.Clear();
            if (keys != null)
            {
                foreach (RoadLaneWidthKey key in keys)
                {
                    if (key == null)
                    {
                        continue;
                    }

                    widthKeys.Add(new RoadLaneWidthKey
                    {
                        normalizedDistance = Mathf.Clamp01(key.normalizedDistance),
                        width = Mathf.Max(0.1f, key.width)
                    });
                }
            }

            widthKeys.Sort((a, b) => a.normalizedDistance.CompareTo(b.normalizedDistance));
        }

        public void ConfigureConnector(
            string stableKey,
            string connectorId,
            string sourceId,
            string targetId,
            RoadLaneTurn turn,
            float suggestedSpeed,
            float baseCost,
            string junctionId = "")
        {
            kind = RoadLaneKind.Connector;
            travelDirection = RoadLaneTravelDirection.Forward;
            connectorGenerationKey = stableKey ?? string.Empty;
            connectorJunctionId = junctionId ?? string.Empty;
            laneId = connectorId ?? string.Empty;
            sourceLaneId = sourceId ?? string.Empty;
            targetLaneId = targetId ?? string.Empty;
            turnType = turn;
            speedLimit = Mathf.Max(0f, suggestedSpeed);
            traversalCost = Mathf.Max(0f, baseCost);
            connectionMode = RoadLaneConnectionMode.Manual;
            manualNextLaneIds = targetLaneId;
            orphaned = false;
            managedProfileOrphaned = false;
            managedProfileStale = false;
            open = true;
        }

        public void ConfigureManagedProfile(
            string profileSourceId,
            string profileEntryId,
            float laneWidth,
            RoadTagMask laneTags,
            RoadAgentMask agents,
            string runStartPointId = "")
        {
            managedProfileSourceId = profileSourceId ?? string.Empty;
            managedProfileEntryId = profileEntryId ?? string.Empty;
            managedProfileRunStartPointId = runStartPointId ?? string.Empty;
            width = Mathf.Max(0.1f, laneWidth);
            tagMask = laneTags;
            allowedAgents = agents;
            managedProfileOrphaned = false;
            managedProfileStale = false;
        }

        public void MarkManagedProfileOrphaned()
        {
            if (string.IsNullOrWhiteSpace(managedProfileSourceId))
            {
                return;
            }

            managedProfileOrphaned = true;
            managedProfileStale = true;
            open = false;
        }

        public void ClearManagedProfileOrphaned()
        {
            managedProfileOrphaned = false;
        }

        public void MarkManagedProfileStale()
        {
            if (!string.IsNullOrWhiteSpace(managedProfileSourceId))
            {
                managedProfileStale = true;
            }
        }

        public void MarkConnectorOrphaned()
        {
            if (kind != RoadLaneKind.Connector)
            {
                return;
            }

            orphaned = true;
            open = false;
        }

        public void SetKind(RoadLaneKind value)
        {
            kind = value;
            if (kind == RoadLaneKind.Connector)
            {
                travelDirection = RoadLaneTravelDirection.Forward;
            }
        }

        private void Reset()
        {
            splineContainer = GetComponent<SplineContainer>();
            EnsureSplineIsOpen();
            if (string.IsNullOrWhiteSpace(laneId))
            {
                laneId = gameObject.name;
            }
        }

        private void OnValidate()
        {
            speedLimit = Mathf.Max(0f, speedLimit);
            width = Mathf.Max(0.1f, width);
            widthKeys ??= new List<RoadLaneWidthKey>();
            for (int i = widthKeys.Count - 1; i >= 0; i--)
            {
                RoadLaneWidthKey key = widthKeys[i];
                if (key == null)
                {
                    widthKeys.RemoveAt(i);
                    continue;
                }

                key.normalizedDistance = Mathf.Clamp01(key.normalizedDistance);
                key.width = Mathf.Max(0.1f, key.width);
            }

            widthKeys.Sort((a, b) => a.normalizedDistance.CompareTo(b.normalizedDistance));
            sampleSpacingOverride = Mathf.Max(0f, sampleSpacingOverride);
            traversalCost = Mathf.Max(0f, traversalCost);
            laneId ??= string.Empty;
            manualNextLaneIds ??= string.Empty;
            connectorGenerationKey ??= string.Empty;
            connectorJunctionId ??= string.Empty;
            sourceLaneId ??= string.Empty;
            targetLaneId ??= string.Empty;
            managedProfileSourceId ??= string.Empty;
            managedProfileEntryId ??= string.Empty;
            managedProfileRunStartPointId ??= string.Empty;
            if (kind == RoadLaneKind.Connector)
            {
                travelDirection = RoadLaneTravelDirection.Forward;
            }

            EnsureSplineIsOpen();
        }

        private void EnsureSplineIsOpen()
        {
            Spline spline = Spline;
            if (spline != null)
            {
                spline.Closed = false;
            }
        }
    }
}
