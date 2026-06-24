using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vehicle Road/Road Network/Polygon Zone")]
    public sealed class RoadPolygonZone : MonoBehaviour
    {
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private List<Vector2> vertices = new List<Vector2>();
        [SerializeField] private float minimumHeight;
        [SerializeField, Min(0.1f)] private float height = 3f;
        [SerializeField] private RoadTagMask tags = RoadTagMask.Pedestrian | RoadTagMask.Outdoor;
        [SerializeField] private RoadAgentMask allowedAgents = RoadAgentMask.Pedestrian | RoadAgentMask.Bicycle;
        [SerializeField] private bool open = true;
        [SerializeField, Min(0f)] private float traversalCost = 1f;

        public string ZoneId
        {
            get => zoneId ?? string.Empty;
            set => zoneId = value ?? string.Empty;
        }

        public List<Vector2> Vertices => vertices;
        public float MinimumHeight
        {
            get => minimumHeight;
            set => minimumHeight = value;
        }

        public float Height
        {
            get => Mathf.Max(0.1f, height);
            set => height = Mathf.Max(0.1f, value);
        }

        public RoadTagMask Tags
        {
            get => tags;
            set => tags = value;
        }

        public RoadAgentMask AllowedAgents
        {
            get => allowedAgents;
            set => allowedAgents = value;
        }

        public bool Open
        {
            get => open;
            set => open = value;
        }

        public float TraversalCost
        {
            get => Mathf.Max(0f, traversalCost);
            set => traversalCost = Mathf.Max(0f, value);
        }

        public Vector3 LocalVertexToWorld(Vector2 vertex, float localHeight = 0f)
        {
            return transform.TransformPoint(new Vector3(vertex.x, minimumHeight + localHeight, vertex.y));
        }

        public Vector2 WorldToLocalXZ(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            return new Vector2(local.x, local.z);
        }

        public RoadPortal[] GetPortals()
        {
            return GetComponentsInChildren<RoadPortal>(true);
        }

        private void Reset()
        {
            zoneId = gameObject.name;
            vertices = new List<Vector2>
            {
                new Vector2(-5f, -5f),
                new Vector2(-5f, 5f),
                new Vector2(5f, 5f),
                new Vector2(5f, -5f)
            };
        }

        private void OnValidate()
        {
            zoneId ??= string.Empty;
            vertices ??= new List<Vector2>();
            height = Mathf.Max(0.1f, height);
            traversalCost = Mathf.Max(0f, traversalCost);
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Vehicle Road/Road Network/Road Portal")]
    public sealed class RoadPortal : MonoBehaviour
    {
        [SerializeField] private string portalId = string.Empty;
        [SerializeField, Min(0.1f)] private float width = 2f;
        [SerializeField] private RoadPortalDirection direction = RoadPortalDirection.Bidirectional;
        [SerializeField] private bool open = true;
        [SerializeField, Min(0f)] private float traversalCost = 1f;
        [SerializeField] private RoadTagMask tags;
        [SerializeField] private RoadAgentMask allowedAgents = RoadAgentMask.All;
        [Header("Target")]
        [SerializeField] private RoadLane linkedLane;
        [SerializeField] private bool linkedLaneReverse;
        [SerializeField] private RoadLaneEndpoint linkedLaneEndpoint = RoadLaneEndpoint.Start;
        [SerializeField] private RoadPortal linkedPortal;

        public string PortalId
        {
            get => portalId ?? string.Empty;
            set => portalId = value ?? string.Empty;
        }

        public float Width
        {
            get => Mathf.Max(0.1f, width);
            set => width = Mathf.Max(0.1f, value);
        }

        public RoadPortalDirection Direction
        {
            get => direction;
            set => direction = value;
        }

        public bool Open
        {
            get => open;
            set => open = value;
        }

        public float TraversalCost
        {
            get => Mathf.Max(0f, traversalCost);
            set => traversalCost = Mathf.Max(0f, value);
        }

        public RoadTagMask Tags
        {
            get => tags;
            set => tags = value;
        }

        public RoadAgentMask AllowedAgents
        {
            get => allowedAgents;
            set => allowedAgents = value;
        }

        public RoadLane LinkedLane
        {
            get => linkedLane;
            set => linkedLane = value;
        }

        public bool LinkedLaneReverse
        {
            get => linkedLaneReverse;
            set => linkedLaneReverse = value;
        }

        public RoadLaneEndpoint LinkedLaneEndpoint
        {
            get => linkedLaneEndpoint;
            set => linkedLaneEndpoint = value;
        }

        public RoadPortal LinkedPortal
        {
            get => linkedPortal;
            set => linkedPortal = value;
        }

        public RoadPolygonZone SourceZone => GetComponentInParent<RoadPolygonZone>();

        private void Reset()
        {
            portalId = gameObject.name;
        }

        private void OnValidate()
        {
            portalId ??= string.Empty;
            width = Mathf.Max(0.1f, width);
            traversalCost = Mathf.Max(0f, traversalCost);
        }
    }
}
