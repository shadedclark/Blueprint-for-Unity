using UnityEngine;

namespace VehicleRoads
{
    [CreateAssetMenu(menuName = "Vehicle Road/Road Network/Road Agent Profile")]
    public sealed class RoadAgentProfile : ScriptableObject
    {
        [SerializeField] private RoadAgentMask agentMask = RoadAgentMask.Car;
        [SerializeField] private RoadTagFilter tagFilter;
        [SerializeField, Min(0f)] private float radius = 0.9f;
        [SerializeField, Min(0.1f)] private float maximumSpeed = 12f;
        [SerializeField, Min(0.1f)] private float lookAheadDistance = 3f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.5f;
        [SerializeField, Min(0.1f)] private float recoveryDistance = 2f;
        [SerializeField, Min(0.1f)] private float routeSearchDistance = 30f;
        [SerializeField, Min(0f)] private float maximumHeightDifference = 3f;

        public RoadAgentMask AgentMask => agentMask;
        public RoadTagFilter TagFilter => tagFilter;
        public float Radius => Mathf.Max(0f, radius);
        public float MaximumSpeed => Mathf.Max(0.1f, maximumSpeed);
        public float LookAheadDistance => Mathf.Max(0.1f, lookAheadDistance);
        public float ArrivalDistance => Mathf.Max(0.01f, arrivalDistance);
        public float RecoveryDistance => Mathf.Max(0.1f, recoveryDistance);
        public float RouteSearchDistance => Mathf.Max(0.1f, routeSearchDistance);
        public float MaximumHeightDifference => Mathf.Max(0f, maximumHeightDifference);
    }
}
