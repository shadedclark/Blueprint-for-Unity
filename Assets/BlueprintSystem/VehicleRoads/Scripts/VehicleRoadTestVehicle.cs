using UnityEngine;

namespace VehicleRoads
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleLaneFollower))]
    [AddComponentMenu("Vehicle Road/Vehicle Road Test Vehicle")]
    public sealed class VehicleRoadTestVehicle : MonoBehaviour
    {
        private const float StopPointBehindEpsilon = 0.001f;

        [SerializeField] private string vehicleId = string.Empty;
        [SerializeField] private VehicleRoadSubsystem roadSubsystem;
        [SerializeField, Min(0.1f)] private float vehicleLength = 4.5f;
        [SerializeField, Min(0f)] private float acceleration = 6f;
        [SerializeField, Min(0f)] private float turnSpeed = 180f;
        [SerializeField] private RoadAgentMask agentMask = RoadAgentMask.Car;
        [Header("Demo Movement")]
        [SerializeField] private bool followBakedLanePose;
        [SerializeField, Min(0.1f)] private float stopPointApproachSpeed = 2f;
        [Header("Demo Loop")]
        [SerializeField] private bool loopRoute;
        [SerializeField, Min(0.1f)] private float loopResetDelay = 2f;

        private VehicleLaneFollower follower;
        private float currentSpeed;
        private float invalidOutputDuration;
        private Vector3 loopStartPosition;
        private Quaternion loopStartRotation;
        private bool loopStartCaptured;

        public string VehicleId
        {
            get => vehicleId;
            set => vehicleId = value ?? string.Empty;
        }

        public VehicleRoadSubsystem RoadSubsystem
        {
            get => roadSubsystem;
            set
            {
                roadSubsystem = value;
                if (follower != null)
                {
                    follower.RoadSubsystem = roadSubsystem;
                }
            }
        }

        public bool LoopRoute
        {
            get => loopRoute;
            set => loopRoute = value;
        }

        public bool FollowBakedLanePose
        {
            get => followBakedLanePose;
            set => followBakedLanePose = value;
        }

        public float LoopResetDelay
        {
            get => Mathf.Max(0.1f, loopResetDelay);
            set => loopResetDelay = Mathf.Max(0.1f, value);
        }

        public VehicleLaneFollowerOutput LastOutput { get; private set; }

        private void Awake()
        {
            follower = GetComponent<VehicleLaneFollower>();
            CaptureLoopStart();
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                vehicleId = "test_vehicle_" + GetInstanceID();
            }

            if (roadSubsystem == null)
            {
                roadSubsystem = follower.RoadSubsystem;
            }

            if (roadSubsystem != null)
            {
                follower.RoadSubsystem = roadSubsystem;
            }
        }

        private void Update()
        {
            if (follower == null)
            {
                return;
            }

            LastOutput = follower.ComputeControl(new VehicleLaneFollowerInput
            {
                vehicleId = vehicleId,
                position = transform.position,
                forward = transform.forward,
                speed = currentSpeed,
                wheelBase = Mathf.Max(0.1f, vehicleLength * 0.55f),
                vehicleLength = vehicleLength,
                agentMask = agentMask
            });

            if (!LastOutput.valid)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * Time.deltaTime);
                if (loopRoute)
                {
                    invalidOutputDuration += Time.deltaTime;
                    if (invalidOutputDuration >= LoopResetDelay)
                    {
                        ResetLoop();
                    }
                }

                return;
            }

            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                Mathf.Max(0f, LastOutput.targetSpeed),
                acceleration * Time.deltaTime);
            if (followBakedLanePose &&
                follower.IsAtRouteEnd(LastOutput.currentLaneId, LastOutput.distanceAlongLane))
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * Time.deltaTime);
                if (loopRoute)
                {
                    invalidOutputDuration += Time.deltaTime;
                    if (invalidOutputDuration >= LoopResetDelay)
                    {
                        ResetLoop();
                    }
                }

                return;
            }

            invalidOutputDuration = 0f;
            float requestedTravelDistance = currentSpeed * Time.deltaTime;
            float travelDistance = requestedTravelDistance;
            bool reachedExplicitStopPoint = false;
            if (LastOutput.hasStopPoint && float.IsFinite(LastOutput.distanceToStopLine))
            {
                if (LastOutput.distanceToStopLine < -StopPointBehindEpsilon)
                {
                    if (followBakedLanePose && TryMoveAlongBakedRoute(LastOutput, travelDistance))
                    {
                        return;
                    }

                    MoveTowardLookAhead(travelDistance);
                    return;
                }

                float distanceToStop = Mathf.Max(0f, LastOutput.distanceToStopLine);
                if (LastOutput.targetSpeed <= 0.01f && distanceToStop > 0.001f)
                {
                    requestedTravelDistance = Mathf.Max(
                        requestedTravelDistance,
                        stopPointApproachSpeed * Time.deltaTime);
                    travelDistance = Mathf.Max(travelDistance, requestedTravelDistance);
                }

                reachedExplicitStopPoint = LastOutput.targetSpeed <= 0.01f &&
                                           distanceToStop <= requestedTravelDistance + 0.001f;
                travelDistance = Mathf.Min(travelDistance, distanceToStop);
            }

            if (reachedExplicitStopPoint && IsStopPointAhead(LastOutput.stopPoint))
            {
                transform.position = LastOutput.stopPoint;
                currentSpeed = 0f;
                return;
            }

            if (followBakedLanePose && TryMoveAlongBakedRoute(LastOutput, travelDistance))
            {
                return;
            }

            MoveTowardLookAhead(travelDistance);
        }

        private void MoveTowardLookAhead(float travelDistance)
        {
            Vector3 toTarget = LastOutput.lookAheadPoint - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime);
            }

            transform.position += transform.forward * travelDistance;
        }

        private bool IsStopPointAhead(Vector3 stopPoint)
        {
            Vector3 forward = transform.forward;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            return Vector3.Dot(stopPoint - transform.position, forward.normalized) >= -StopPointBehindEpsilon;
        }

        private bool TryMoveAlongBakedRoute(VehicleLaneFollowerOutput output, float travelDistance)
        {
            if (follower == null ||
                !follower.TryEvaluateRoutePose(
                    output.currentLaneId,
                    output.distanceAlongLane + Mathf.Max(0f, travelDistance),
                    out _,
                    out RoadLanePose pose))
            {
                return false;
            }

            Vector3 forward = pose.forward.sqrMagnitude > 0.0001f ? pose.forward.normalized : transform.forward;
            Vector3 up = pose.up.sqrMagnitude > 0.0001f ? pose.up.normalized : Vector3.up;
            transform.SetPositionAndRotation(pose.position, Quaternion.LookRotation(forward, up));
            return true;
        }

        private void CaptureLoopStart()
        {
            loopStartPosition = transform.position;
            loopStartRotation = transform.rotation;
            loopStartCaptured = true;
        }

        private void ResetLoop()
        {
            if (!loopStartCaptured)
            {
                CaptureLoopStart();
            }

            if (roadSubsystem != null && !string.IsNullOrWhiteSpace(vehicleId))
            {
                roadSubsystem.UnregisterVehicle(vehicleId);
            }

            transform.SetPositionAndRotation(loopStartPosition, loopStartRotation);
            currentSpeed = 0f;
            invalidOutputDuration = 0f;
            LastOutput = default;
        }

        private void OnDisable()
        {
            if (roadSubsystem != null && !string.IsNullOrWhiteSpace(vehicleId))
            {
                roadSubsystem.UnregisterVehicle(vehicleId);
            }
        }

        private void OnValidate()
        {
            vehicleId ??= string.Empty;
            if (agentMask == RoadAgentMask.None)
            {
                agentMask = RoadAgentMask.Car;
            }

            vehicleLength = Mathf.Max(0.1f, vehicleLength);
            acceleration = Mathf.Max(0f, acceleration);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            stopPointApproachSpeed = Mathf.Max(0.1f, stopPointApproachSpeed);
            loopResetDelay = Mathf.Max(0.1f, loopResetDelay);
        }
    }
}
