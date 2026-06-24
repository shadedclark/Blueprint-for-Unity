using System;
using Unity.Profiling;

namespace VehicleRoads
{
    public static class RoadNetworkProfiler
    {
        public static readonly ProfilerMarker BakeLanes = new ProfilerMarker("RoadNetwork.Bake.Lanes");
        public static readonly ProfilerMarker BakeBoundaries = new ProfilerMarker("RoadNetwork.Bake.Boundaries");
        public static readonly ProfilerMarker RefreshProfile = new ProfilerMarker("RoadNetwork.Authoring.RefreshProfile");
        public static readonly ProfilerMarker TriangulatePolygons = new ProfilerMarker("RoadNetwork.Bake.TriangulatePolygons");
        public static readonly ProfilerMarker BuildPortalPaths = new ProfilerMarker("RoadNetwork.Bake.BuildPortalPaths");
        public static readonly ProfilerMarker BuildSpatialIndex = new ProfilerMarker("RoadNetwork.Runtime.BuildSpatialIndex");
        public static readonly ProfilerMarker NearestElementQuery = new ProfilerMarker("RoadNetwork.Query.NearestElement");
        public static readonly ProfilerMarker PointQuery = new ProfilerMarker("RoadNetwork.Query.Point");
        public static readonly ProfilerMarker SphereQuery = new ProfilerMarker("RoadNetwork.Query.Sphere");
        public static readonly ProfilerMarker BoundsQuery = new ProfilerMarker("RoadNetwork.Query.Bounds");
        public static readonly ProfilerMarker FilterElement = new ProfilerMarker("RoadNetwork.Query.Filter");
        public static readonly ProfilerMarker RouteSearch = new ProfilerMarker("RoadNetwork.Route.Search");
        public static readonly ProfilerMarker PolygonFunnel = new ProfilerMarker("RoadNetwork.Route.PolygonFunnel");
        public static readonly ProfilerMarker AgentEvaluate = new ProfilerMarker("RoadNetwork.Agent.Evaluate");
        public static readonly ProfilerMarker AgentReplan = new ProfilerMarker("RoadNetwork.Agent.Replan");
        public static readonly ProfilerMarker LeadVehicleQuery = new ProfilerMarker("RoadNetwork.Traffic.LeadVehicle");
        public static readonly ProfilerMarker LaneChangeQuery = new ProfilerMarker("RoadNetwork.Traffic.LaneChange");
        public static readonly ProfilerMarker JunctionControl = new ProfilerMarker("RoadNetwork.Traffic.JunctionControl");

        private static bool enabled;

        public static bool Enabled => enabled;

        public static void Configure(RoadNetworkRuntimeSettings settings)
        {
            enabled = settings != null && settings.EnableRuntimeProfilerMarkers;
        }

        public static Scope Sample(ProfilerMarker marker)
        {
            return new Scope(marker, enabled);
        }

        public readonly struct Scope : IDisposable
        {
            private readonly ProfilerMarker marker;
            private readonly bool active;

            public Scope(ProfilerMarker marker, bool active)
            {
                this.marker = marker;
                this.active = active;
                if (active)
                {
                    marker.Begin();
                }
            }

            public void Dispose()
            {
                if (active)
                {
                    marker.End();
                }
            }
        }
    }

    public sealed class RoadDiagnosticRingBuffer
    {
        private RoadDiagnosticEvent[] events = Array.Empty<RoadDiagnosticEvent>();
        private int start;
        private int count;
        private int droppedCount;

        public int Count => count;
        public int Capacity => events.Length;
        public int DroppedCount => droppedCount;

        public void Configure(int capacity)
        {
            capacity = Math.Max(16, Math.Min(2048, capacity));
            if (events.Length == capacity)
            {
                return;
            }

            events = new RoadDiagnosticEvent[capacity];
            start = 0;
            count = 0;
            droppedCount = 0;
        }

        public void Clear()
        {
            start = 0;
            count = 0;
            droppedCount = 0;
        }

        public void Add(in RoadDiagnosticEvent value)
        {
            if (events.Length == 0)
            {
                return;
            }

            if (count < events.Length)
            {
                events[(start + count) % events.Length] = value;
                count++;
                return;
            }

            events[start] = value;
            start = (start + 1) % events.Length;
            droppedCount++;
        }

        public int CopyTo(RoadDiagnosticEvent[] destination, int destinationIndex = 0)
        {
            if (destination == null || destinationIndex < 0 || destinationIndex >= destination.Length)
            {
                return 0;
            }

            int copyCount = Math.Min(count, destination.Length - destinationIndex);
            for (int i = 0; i < copyCount; i++)
            {
                destination[destinationIndex + i] = events[(start + i) % events.Length];
            }

            return copyCount;
        }
    }
}
