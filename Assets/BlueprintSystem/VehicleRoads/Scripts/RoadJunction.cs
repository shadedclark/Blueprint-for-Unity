using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    [Serializable]
    public sealed class RoadJunctionBinding
    {
        public RoadLane lane;
        public RoadLaneEndpoint endpoint = RoadLaneEndpoint.End;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Vehicle Road/Road Junction")]
    public sealed class RoadJunction : MonoBehaviour
    {
        [SerializeField] private string junctionId = string.Empty;
        [SerializeField] private RoadLaneTurnMask allowedTurns = RoadLaneTurnMask.Default;
        [SerializeField, Min(0.1f)] private float connectorHandleScale = 0.35f;
        [SerializeField, Min(0f)] private float connectorBaseCost = 1f;
        [SerializeField, Min(0f)] private float connectorSpeedLimit = 8f;
        [Header("Traffic Control")]
        [SerializeField] private RoadJunctionTrafficControlMode trafficControlMode;
        [SerializeField] private float defaultStopLineDistance = 2f;
        [SerializeField, Min(0.5f)] private float queueSpacing = 6f;
        [SerializeField, Min(1f)] private float approachDetectionDistance = 18f;
        [SerializeField, Min(0.1f)] private float passageTokenDuration = 8f;
        [SerializeField, Min(0f)] private float releaseDistance = 2f;
        [SerializeField, Min(0f)] private float straightPriority = 4f;
        [SerializeField, Min(0f)] private float rightPriority = 4f;
        [SerializeField, Min(0f)] private float leftPriority = 2f;
        [SerializeField, Min(0f)] private float uTurnPriority = 1f;
        [SerializeField] private List<RoadJunctionSignalPhase> signalPhases = new List<RoadJunctionSignalPhase>();
        [SerializeField] private List<RoadJunctionBinding> bindings = new List<RoadJunctionBinding>();

        public string JunctionId
        {
            get => junctionId;
            set => junctionId = value ?? string.Empty;
        }

        public RoadLaneTurnMask AllowedTurns
        {
            get => allowedTurns;
            set => allowedTurns = value;
        }

        public float ConnectorHandleScale
        {
            get => Mathf.Max(0.1f, connectorHandleScale);
            set => connectorHandleScale = Mathf.Max(0.1f, value);
        }
        public float ConnectorBaseCost => Mathf.Max(0f, connectorBaseCost);
        public float ConnectorSpeedLimit => Mathf.Max(0f, connectorSpeedLimit);
        public RoadJunctionTrafficControlMode TrafficControlMode
        {
            get => trafficControlMode;
            set => trafficControlMode = value;
        }

        public float DefaultStopLineDistance
        {
            get => defaultStopLineDistance;
            set => defaultStopLineDistance = float.IsFinite(value) ? value : 0f;
        }

        public float QueueSpacing
        {
            get => Mathf.Max(0.5f, queueSpacing);
            set => queueSpacing = Mathf.Max(0.5f, value);
        }

        public float ApproachDetectionDistance
        {
            get => Mathf.Max(1f, approachDetectionDistance);
            set => approachDetectionDistance = Mathf.Max(1f, value);
        }

        public float PassageTokenDuration
        {
            get => Mathf.Max(0.1f, passageTokenDuration);
            set => passageTokenDuration = Mathf.Max(0.1f, value);
        }

        public float ReleaseDistance
        {
            get => Mathf.Max(0f, releaseDistance);
            set => releaseDistance = Mathf.Max(0f, value);
        }

        public float StraightPriority
        {
            get => Mathf.Max(0f, straightPriority);
            set => straightPriority = Mathf.Max(0f, value);
        }

        public float RightPriority
        {
            get => Mathf.Max(0f, rightPriority);
            set => rightPriority = Mathf.Max(0f, value);
        }

        public float LeftPriority
        {
            get => Mathf.Max(0f, leftPriority);
            set => leftPriority = Mathf.Max(0f, value);
        }

        public float UTurnPriority
        {
            get => Mathf.Max(0f, uTurnPriority);
            set => uTurnPriority = Mathf.Max(0f, value);
        }

        public List<RoadJunctionSignalPhase> SignalPhases => signalPhases;
        public List<RoadJunctionBinding> Bindings => bindings;

        private void Reset()
        {
            if (string.IsNullOrWhiteSpace(junctionId))
            {
                junctionId = gameObject.name;
            }
        }

        private void OnValidate()
        {
            junctionId ??= string.Empty;
            connectorHandleScale = Mathf.Max(0.1f, connectorHandleScale);
            connectorBaseCost = Mathf.Max(0f, connectorBaseCost);
            connectorSpeedLimit = Mathf.Max(0f, connectorSpeedLimit);
            defaultStopLineDistance = float.IsFinite(defaultStopLineDistance) ? defaultStopLineDistance : 0f;
            queueSpacing = Mathf.Max(0.5f, queueSpacing);
            approachDetectionDistance = Mathf.Max(1f, approachDetectionDistance);
            passageTokenDuration = Mathf.Max(0.1f, passageTokenDuration);
            releaseDistance = Mathf.Max(0f, releaseDistance);
            straightPriority = Mathf.Max(0f, straightPriority);
            rightPriority = Mathf.Max(0f, rightPriority);
            leftPriority = Mathf.Max(0f, leftPriority);
            uTurnPriority = Mathf.Max(0f, uTurnPriority);
            signalPhases ??= new List<RoadJunctionSignalPhase>();
            for (int i = 0; i < signalPhases.Count; i++)
            {
                RoadJunctionSignalPhase phase = signalPhases[i];
                if (phase == null)
                {
                    continue;
                }

                phase.phaseId ??= string.Empty;
                phase.greenDuration = Mathf.Max(0.1f, phase.greenDuration);
                phase.yellowDuration = Mathf.Max(0f, phase.yellowDuration);
                phase.allRedDuration = Mathf.Max(0f, phase.allRedDuration);
            }

            bindings ??= new List<RoadJunctionBinding>();
            if (allowedTurns == RoadLaneTurnMask.None)
            {
                allowedTurns = RoadLaneTurnMask.Default;
            }
        }
    }
}
