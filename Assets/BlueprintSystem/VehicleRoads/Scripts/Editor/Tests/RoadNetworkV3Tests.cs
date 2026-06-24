using System.Collections.Generic;
using NUnit.Framework;
using VehicleRoads;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads.Editor.Tests
{
    public sealed class RoadNetworkV3Tests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
            RoadNetworkProfiler.Configure(null);
        }

        [Test]
        public void TagFilterImplementsAllAnyAndNone()
        {
            RoadTagFilter filter = new RoadTagFilter
            {
                all = RoadTagMask.Road | RoadTagMask.Vehicle,
                any = RoadTagMask.Outdoor | RoadTagMask.Parking,
                none = RoadTagMask.Restricted
            };

            Assert.True(filter.Matches(
                RoadTagMask.Road | RoadTagMask.Vehicle | RoadTagMask.Outdoor));
            Assert.False(filter.Matches(RoadTagMask.Road | RoadTagMask.Outdoor));
            Assert.False(filter.Matches(RoadTagMask.Road | RoadTagMask.Vehicle));
            Assert.False(filter.Matches(
                RoadTagMask.Road |
                RoadTagMask.Vehicle |
                RoadTagMask.Outdoor |
                RoadTagMask.Restricted));
        }

        [Test]
        public void LaneQueryReportsBoundaryLateralRatioAndHeightSeparatedLane()
        {
            BakedLaneNetwork network = Track(CreateParallelHeightNetwork());
            Assert.True(network.TryFindNearestElement(
                new Vector3(1.5f, 0f, 5f),
                RoadAgentMask.Car,
                RoadTagFilter.MatchAll,
                0.4f,
                5f,
                2f,
                out RoadLocation ground));
            Assert.AreEqual("ground", ground.elementId);
            Assert.True(ground.inside);
            Assert.That(ground.distanceToBoundary, Is.EqualTo(0.5f).Within(0.05f));
            Assert.That(ground.lateralRatio, Is.EqualTo(0.75f).Within(0.05f));

            Assert.True(network.TryFindNearestElement(
                new Vector3(0f, 9.5f, 5f),
                RoadAgentMask.Car,
                RoadTagFilter.MatchAll,
                0.4f,
                5f,
                2f,
                out RoadLocation elevated));
            Assert.AreEqual("elevated", elevated.elementId);

            List<RoadAreaQueryResult> results = new List<RoadAreaQueryResult>();
            RoadAreaQuery point = RoadAreaQuery.Point(
                new Vector3(1.8f, 0f, 5f),
                RoadAgentMask.Truck,
                RoadTagFilter.MatchAll,
                0.3f,
                1f);
            Assert.AreEqual(0, network.QueryArea(point, results));
        }

        [Test]
        public void PolygonTriangulationSupportsConcaveAndRejectsSelfIntersection()
        {
            List<int> triangles = new List<int>();
            List<Vector2> concave = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(4f, 0f),
                new Vector2(4f, 4f),
                new Vector2(2f, 2f),
                new Vector2(0f, 4f)
            };
            Assert.True(RoadPolygonGeometry.TryTriangulate(concave, triangles, out string error), error);
            Assert.AreEqual(9, triangles.Count);

            List<Vector2> selfIntersecting = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(4f, 4f),
                new Vector2(0f, 4f),
                new Vector2(4f, 0f)
            };
            Assert.False(RoadPolygonGeometry.TryTriangulate(selfIntersecting, triangles, out error));
            StringAssert.Contains("intersect", error.ToLowerInvariant());
        }

        [Test]
        public void UnifiedRouteTraversesLanePolygonAndLane()
        {
            BakedLaneNetwork network = Track(CreateMixedRouteNetwork());
            RoadRouteQuery query = new RoadRouteQuery
            {
                startPosition = new Vector3(0f, 0f, -5f),
                destinationPosition = new Vector3(0f, 0f, 15f),
                agentMask = RoadAgentMask.Car,
                agentRadius = 0.5f,
                maximumSearchDistance = 30f,
                maximumHeightDifference = 2f
            };

            Assert.True(network.TryFindRoute(query, out RoadNetworkRouteResult route));
            Assert.AreEqual(RoadRouteState.Valid, route.state);
            Assert.AreEqual(3, route.segments.Count);
            Assert.AreEqual(RoadElementKind.Lane, route.segments[0].kind);
            Assert.AreEqual(RoadElementKind.Polygon, route.segments[1].kind);
            Assert.AreEqual(RoadElementKind.Lane, route.segments[2].kind);

            List<Vector3> polygonPath = new List<Vector3>();
            Assert.True(network.TryBuildPolygonPath(
                "plaza",
                route.segments[1].entryPosition,
                route.segments[1].exitPosition,
                polygonPath));
            Assert.That(polygonPath.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void RoadAgentPlansEvaluatesCancelsAndExposesSnapshot()
        {
            BakedLaneNetwork network = Track(CreateMixedRouteNetwork());
            GameObject gameObject = Track(new GameObject("Road Agent"));
            RoadAgent agent = gameObject.AddComponent<RoadAgent>();
            agent.FallbackNetwork = network;

            Assert.True(agent.SetDestination(
                new Vector3(0f, 0f, -5f),
                new Vector3(0f, 0f, 15f)));
            Assert.AreEqual(RoadAgentState.Following, agent.State);
            RoadAgentControlOutput output = agent.Evaluate(
                new Vector3(0f, 0f, -5f),
                Vector3.forward,
                0f,
                0.02f);
            Assert.True(output.valid);
            Assert.AreEqual(RoadRouteState.Valid, output.routeState);
            Assert.That(output.remainingDistance, Is.GreaterThan(0f));

            RoadAgentDebugSnapshot snapshot = agent.GetDebugSnapshot();
            Assert.AreEqual(RoadAgentState.Following, snapshot.state);
            Assert.AreEqual(3, snapshot.routeSegmentCount);
            Assert.IsNotEmpty(snapshot.agentId);

            agent.Cancel(Vector3.zero);
            Assert.AreEqual(RoadAgentState.Idle, agent.State);
            Assert.AreEqual(RoadRouteState.Cancelled, agent.RouteState);
        }

        [Test]
        public void ProfileRefreshPreservesStableIdsLocksAndMarksRemovedEntryOrphaned()
        {
            GameObject root = Track(new GameObject("Network"));
            root.AddComponent<RoadLaneNetwork>();
            GameObject sourceObject = Track(new GameObject("Profile Source"));
            sourceObject.transform.SetParent(root.transform);
            SplineContainer container = sourceObject.AddComponent<SplineContainer>();
            container.Spline = new Spline(new[]
            {
                new BezierKnot(new float3(0f, 0f, 0f)),
                new BezierKnot(new float3(0f, 0f, 10f))
            });
            RoadLaneProfileSource source = sourceObject.AddComponent<RoadLaneProfileSource>();
            source.SourceId = "main";
            RoadLaneProfile profile = Track(ScriptableObject.CreateInstance<RoadLaneProfile>());
            profile.Entries.Clear();
            profile.Entries.Add(new RoadLaneProfileEntry { entryId = "left", width = 3f });
            profile.Entries.Add(new RoadLaneProfileEntry { entryId = "right", width = 4f });
            source.Profile = profile;

            Assert.True(source.RefreshManagedLanes(null, null, out string error), error);
            RoadLane[] lanes = source.GetComponentsInChildren<RoadLane>(true);
            Assert.AreEqual(2, lanes.Length);
            RoadLane left = System.Array.Find(lanes, lane => lane.ManagedProfileEntryId == "left");
            RoadLane right = System.Array.Find(lanes, lane => lane.ManagedProfileEntryId == "right");
            Assert.AreEqual("main_left", left.LaneId);
            Assert.AreEqual("main_right", right.LaneId);

            left.ManagedProfileLocked = true;
            left.Width = 7f;
            profile.Entries[0].width = 5f;
            profile.Entries.RemoveAt(1);
            Assert.True(source.RefreshManagedLanes(null, null, out error), error);
            Assert.AreEqual(7f, left.Width);
            Assert.True(left.ManagedProfileStale);
            Assert.True(right.ManagedProfileOrphaned);
            Assert.False(right.Open);
        }

        [Test]
        public void ProfileControlPointsGenerateStableTaperedRuns()
        {
            GameObject root = Track(new GameObject("Network"));
            root.AddComponent<RoadLaneNetwork>();
            GameObject sourceObject = Track(new GameObject("Profile Source"));
            sourceObject.transform.SetParent(root.transform);
            SplineContainer container = sourceObject.AddComponent<SplineContainer>();
            container.Spline = new Spline(new[]
            {
                new BezierKnot(new float3(0f, 0f, 0f)),
                new BezierKnot(new float3(0f, 0f, 10f)),
                new BezierKnot(new float3(0f, 0f, 20f))
            });
            RoadLaneProfileSource source = sourceObject.AddComponent<RoadLaneProfileSource>();
            source.SourceId = "arterial";
            RoadLaneProfile twoLane = Track(ScriptableObject.CreateInstance<RoadLaneProfile>());
            twoLane.Entries.Clear();
            twoLane.Entries.Add(new RoadLaneProfileEntry { entryId = "through", width = 3.5f });
            twoLane.Entries.Add(new RoadLaneProfileEntry { entryId = "merge", width = 3.5f });
            RoadLaneProfile oneLane = Track(ScriptableObject.CreateInstance<RoadLaneProfile>());
            oneLane.Entries.Clear();
            oneLane.Entries.Add(new RoadLaneProfileEntry { entryId = "through", width = 4f });
            source.Profile = twoLane;
            source.SynchronizeControlPoints();
            Assert.True(source.SetControlPoint(2, oneLane, false, out string error), error);

            Assert.True(source.RefreshManagedLanes(null, null, out error), error);
            RoadLane through = System.Array.Find(
                source.GetComponentsInChildren<RoadLane>(true),
                lane => lane.ManagedProfileEntryId == "through");
            RoadLane merge = System.Array.Find(
                source.GetComponentsInChildren<RoadLane>(true),
                lane => lane.ManagedProfileEntryId == "merge");
            Assert.NotNull(through);
            Assert.NotNull(merge);
            StringAssert.StartsWith("arterial_through_p_", through.LaneId);
            Assert.That(through.EvaluateWidth(0f), Is.EqualTo(3.5f).Within(0.01f));
            Assert.That(through.EvaluateWidth(1f), Is.EqualTo(4f).Within(0.01f));
            Assert.That(merge.EvaluateWidth(1f), Is.EqualTo(0.1f).Within(0.01f));

            string stableId = through.LaneId;
            Assert.True(source.RefreshManagedLanes(null, null, out error), error);
            Assert.AreEqual(stableId, through.LaneId);
        }

        [Test]
        public void ProfileTaperMinimumWidthSurvivesBakeUpgrade()
        {
            GameObject root = Track(new GameObject("Network"));
            RoadLaneNetwork network = root.AddComponent<RoadLaneNetwork>();
            GameObject sourceObject = Track(new GameObject("Profile Source"));
            sourceObject.transform.SetParent(root.transform);
            SplineContainer container = sourceObject.AddComponent<SplineContainer>();
            container.Spline = new Spline(new[]
            {
                new BezierKnot(new float3(0f, 0f, 0f)),
                new BezierKnot(new float3(0f, 0f, 10f)),
                new BezierKnot(new float3(0f, 0f, 20f))
            });
            RoadLaneProfileSource source = sourceObject.AddComponent<RoadLaneProfileSource>();
            source.SourceId = "arterial";
            RoadLaneProfile twoLane = Track(ScriptableObject.CreateInstance<RoadLaneProfile>());
            twoLane.Entries.Clear();
            twoLane.Entries.Add(new RoadLaneProfileEntry { entryId = "through", width = 3.5f });
            twoLane.Entries.Add(new RoadLaneProfileEntry { entryId = "merge", width = 3.5f });
            RoadLaneProfile oneLane = Track(ScriptableObject.CreateInstance<RoadLaneProfile>());
            oneLane.Entries.Clear();
            oneLane.Entries.Add(new RoadLaneProfileEntry { entryId = "through", width = 3.5f });
            source.Profile = twoLane;
            source.SynchronizeControlPoints();
            Assert.True(source.SetControlPoint(2, oneLane, false, out string error), error);

            BakedLaneNetwork baked = Track(network.BakeNetwork());
            BakedLaneRecord merge = null;
            for (int i = 0; i < baked.Lanes.Count; i++)
            {
                if (baked.Lanes[i].laneId.Contains("_merge_"))
                {
                    merge = baked.Lanes[i];
                    break;
                }
            }

            Assert.NotNull(merge);
            Assert.That(merge.minimumWidth, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(
                baked.Samples[merge.firstSampleIndex + merge.sampleCount - 1].width,
                Is.EqualTo(0.1f).Within(0.001f));
        }

        [Test]
        public void VariableProfileRejectsDuplicateEntryIdsAtControlPoint()
        {
            GameObject root = Track(new GameObject("Network"));
            root.AddComponent<RoadLaneNetwork>();
            GameObject sourceObject = Track(new GameObject("Profile Source"));
            sourceObject.transform.SetParent(root.transform);
            SplineContainer container = sourceObject.AddComponent<SplineContainer>();
            container.Spline = new Spline(new[]
            {
                new BezierKnot(new float3(0f, 0f, 0f)),
                new BezierKnot(new float3(0f, 0f, 10f))
            });
            RoadLaneProfileSource source = sourceObject.AddComponent<RoadLaneProfileSource>();
            source.SourceId = "arterial";
            RoadLaneProfile baseProfile = Track(ScriptableObject.CreateInstance<RoadLaneProfile>());
            baseProfile.Entries.Clear();
            baseProfile.Entries.Add(new RoadLaneProfileEntry { entryId = "through", width = 3.5f });
            RoadLaneProfile duplicateProfile = Track(ScriptableObject.CreateInstance<RoadLaneProfile>());
            duplicateProfile.Entries.Clear();
            duplicateProfile.Entries.Add(new RoadLaneProfileEntry { entryId = "merge", width = 3.5f });
            duplicateProfile.Entries.Add(new RoadLaneProfileEntry { entryId = "merge", width = 3.5f });
            source.Profile = baseProfile;
            source.SynchronizeControlPoints();
            Assert.True(source.SetControlPoint(1, duplicateProfile, false, out string error), error);

            Assert.False(source.RefreshManagedLanes(null, null, out error));
            StringAssert.Contains("duplicate entryId 'merge'", error);
        }

        [Test]
        public void DiagnosticRingBufferUsesFixedCapacityAndReportsOverwrite()
        {
            RoadDiagnosticRingBuffer buffer = new RoadDiagnosticRingBuffer();
            buffer.Configure(16);
            for (int i = 0; i < 20; i++)
            {
                buffer.Add(new RoadDiagnosticEvent { frame = i });
            }

            RoadDiagnosticEvent[] copied = new RoadDiagnosticEvent[16];
            Assert.AreEqual(16, buffer.CopyTo(copied));
            Assert.AreEqual(4, copied[0].frame);
            Assert.AreEqual(19, copied[15].frame);
            Assert.AreEqual(4, buffer.DroppedCount);
        }

        [Test]
        public void MissingRuntimeSettingsDisableProfiler()
        {
            RoadNetworkProfiler.Configure(null);
            Assert.False(RoadNetworkProfiler.Enabled);
        }

        private BakedLaneNetwork CreateParallelHeightNetwork()
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord>();
            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>();
            AddLane(
                lanes,
                samples,
                "ground",
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 10f),
                4f,
                RoadAgentMask.Car,
                RoadTagMask.Road | RoadTagMask.Vehicle);
            AddLane(
                lanes,
                samples,
                "elevated",
                new Vector3(0f, 10f, 0f),
                new Vector3(0f, 10f, 10f),
                4f,
                RoadAgentMask.Car,
                RoadTagMask.Road | RoadTagMask.Vehicle);
            return CreateNetwork(lanes, samples, null, null);
        }

        private BakedLaneNetwork CreateMixedRouteNetwork()
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord>();
            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>();
            AddLane(
                lanes,
                samples,
                "approach",
                new Vector3(0f, 0f, -10f),
                Vector3.zero,
                3.5f,
                RoadAgentMask.Car,
                RoadTagMask.Road);
            AddLane(
                lanes,
                samples,
                "exit",
                new Vector3(0f, 0f, 10f),
                new Vector3(0f, 0f, 20f),
                3.5f,
                RoadAgentMask.Car,
                RoadTagMask.Road);
            BakedPolygonRecord polygon = new BakedPolygonRecord
            {
                zoneId = "plaza",
                open = true,
                tagMask = RoadTagMask.Road,
                allowedAgents = RoadAgentMask.Car,
                minimumWorldHeight = -1f,
                maximumWorldHeight = 2f,
                bounds = new Bounds(new Vector3(0f, 0.5f, 5f), new Vector3(8f, 3f, 10f)),
                vertices = new List<Vector3>
                {
                    new Vector3(-4f, 0f, 0f),
                    new Vector3(4f, 0f, 0f),
                    new Vector3(4f, 0f, 10f),
                    new Vector3(-4f, 0f, 10f)
                },
                triangles = new List<int> { 0, 1, 2, 0, 2, 3 }
            };
            List<BakedPortalRecord> portals = new List<BakedPortalRecord>
            {
                new BakedPortalRecord
                {
                    portalId = "entry",
                    sourceZoneId = "plaza",
                    targetKind = RoadElementKind.Lane,
                    targetElementId = "approach",
                    direction = RoadPortalDirection.Bidirectional,
                    width = 2f,
                    allowedAgents = RoadAgentMask.Car,
                    tagMask = RoadTagMask.Road,
                    sourcePosition = Vector3.zero,
                    targetPosition = Vector3.zero,
                    targetLaneDistance = 10f
                },
                new BakedPortalRecord
                {
                    portalId = "exit",
                    sourceZoneId = "plaza",
                    targetKind = RoadElementKind.Lane,
                    targetElementId = "exit",
                    direction = RoadPortalDirection.Bidirectional,
                    width = 2f,
                    allowedAgents = RoadAgentMask.Car,
                    tagMask = RoadTagMask.Road,
                    sourcePosition = new Vector3(0f, 0f, 10f),
                    targetPosition = new Vector3(0f, 0f, 10f),
                    targetLaneDistance = 0f
                }
            };
            return CreateNetwork(
                lanes,
                samples,
                new List<BakedPolygonRecord> { polygon },
                portals);
        }

        private static BakedLaneNetwork CreateNetwork(
            List<BakedLaneRecord> lanes,
            List<BakedLaneSampleRecord> samples,
            List<BakedPolygonRecord> polygons,
            List<BakedPortalRecord> portals)
        {
            BakedLaneNetwork network = ScriptableObject.CreateInstance<BakedLaneNetwork>();
            network.SetData(
                string.Empty,
                1f,
                new BakedLaneSummary
                {
                    directedLaneCount = lanes.Count,
                    sampleCount = samples.Count,
                    polygonCount = polygons == null ? 0 : polygons.Count,
                    portalCount = portals == null ? 0 : portals.Count
                },
                lanes,
                samples,
                new List<BakedLaneConnectionRecord>(),
                new List<BakedLaneAdjacentLinkRecord>(),
                new List<BakedJunctionTrafficRecord>(),
                new List<BakedConnectorTrafficRecord>(),
                polygons ?? new List<BakedPolygonRecord>(),
                portals ?? new List<BakedPortalRecord>());
            return network;
        }

        private static void AddLane(
            List<BakedLaneRecord> lanes,
            List<BakedLaneSampleRecord> samples,
            string id,
            Vector3 start,
            Vector3 end,
            float width,
            RoadAgentMask agents,
            RoadTagMask tags)
        {
            int first = samples.Count;
            Vector3 forward = (end - start).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float length = Vector3.Distance(start, end);
            samples.Add(new BakedLaneSampleRecord
            {
                sampleId = id + "_0",
                laneId = id,
                order = 0,
                splinePosition = start,
                finalPosition = start,
                leftBoundary = start - right * width * 0.5f,
                rightBoundary = start + right * width * 0.5f,
                forward = forward,
                up = Vector3.up,
                distanceAlongLane = 0f
            });
            samples.Add(new BakedLaneSampleRecord
            {
                sampleId = id + "_1",
                laneId = id,
                order = 1,
                splinePosition = end,
                finalPosition = end,
                leftBoundary = end - right * width * 0.5f,
                rightBoundary = end + right * width * 0.5f,
                forward = forward,
                up = Vector3.up,
                distanceAlongLane = length
            });
            Bounds bounds = new Bounds(start, Vector3.zero);
            bounds.Encapsulate(end);
            bounds.Expand(new Vector3(width, 0.2f, width));
            lanes.Add(new BakedLaneRecord
            {
                laneId = id,
                sourceLaneId = id,
                kind = RoadLaneKind.Standard,
                direction = RoadLaneTravelDirection.Forward,
                open = true,
                length = length,
                speedLimit = 10f,
                width = width,
                tagMask = tags,
                allowedAgents = agents,
                bounds = bounds,
                firstSampleIndex = first,
                sampleCount = 2
            });
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
