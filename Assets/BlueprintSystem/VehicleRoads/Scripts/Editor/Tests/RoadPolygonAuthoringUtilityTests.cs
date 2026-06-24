using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VehicleRoads;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads.Editor.Tests
{
    public sealed class RoadPolygonAuthoringUtilityTests
    {
        private readonly List<Object> created = new List<Object>();
        private readonly List<RoadLaneNetwork> previewNetworks = new List<RoadLaneNetwork>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < previewNetworks.Count; i++)
            {
                RoadNetworkLivePreviewCoordinator.Unregister(previewNetworks[i]);
            }

            previewNetworks.Clear();
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        [Test]
        public void InsertAndRemoveVertexPreservesOrderAndMinimumCount()
        {
            RoadPolygonZone zone = CreateZone(CreateNetwork(), "plaza");
            Vector3 midpoint = zone.LocalVertexToWorld(new Vector2(5f, 0f));

            Assert.True(RoadPolygonAuthoringUtility.InsertVertexAfterEdge(zone, 1, midpoint, out int insertedIndex));
            Assert.AreEqual(2, insertedIndex);
            Assert.AreEqual(5, zone.Vertices.Count);
            Assert.That(zone.Vertices[insertedIndex].x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(zone.Vertices[insertedIndex].y, Is.EqualTo(0f).Within(0.001f));

            Assert.True(RoadPolygonAuthoringUtility.RemoveVertexAt(zone, insertedIndex));
            Assert.AreEqual(4, zone.Vertices.Count);
            Assert.True(RoadPolygonAuthoringUtility.RemoveVertexAt(zone, 0));
            Assert.AreEqual(3, zone.Vertices.Count);
            Assert.False(RoadPolygonAuthoringUtility.RemoveVertexAt(zone, 0));
            Assert.AreEqual(3, zone.Vertices.Count);
        }

        [Test]
        public void PortalProjectionSnapsToBoundaryWithinBakeTolerance()
        {
            RoadPolygonZone zone = CreateZone(CreateNetwork(), "plaza");
            Vector3 nearBoundary = zone.LocalVertexToWorld(new Vector2(1f, 5.35f));

            Assert.True(RoadPolygonAuthoringUtility.TryProjectToBoundary(
                zone,
                nearBoundary,
                out RoadPolygonBoundaryProjection projection));
            Assert.That(projection.distance, Is.EqualTo(0.35f).Within(0.01f));
            Assert.That(projection.localPoint.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(projection.localPoint.y, Is.EqualTo(5f).Within(0.001f));

            RoadPolygonGeometry.ClosestPointOnBoundary(
                zone.Vertices,
                zone.WorldToLocalXZ(projection.worldPoint),
                out float distance);
            Assert.That(distance, Is.LessThanOrEqualTo(RoadPolygonAuthoringUtility.PortalBoundaryTolerance));
        }

        [Test]
        public void PortalSuggestionDoesNotMutateUntilAppliedAndClearsExclusiveTarget()
        {
            RoadLaneNetwork network = CreateNetwork();
            RoadPolygonZone zone = CreateZone(network, "plaza");
            RoadLane lane = CreateLane(network, "lane", new Vector3(0f, 0f, 7f), new Vector3(0f, 0f, 12f));
            RoadPortal source = CreatePortal(zone, "source", new Vector2(0f, 5f));

            Assert.True(RoadPolygonAuthoringUtility.TryFindPortalSuggestion(
                network,
                source,
                4f,
                out RoadPolygonPortalSuggestion suggestion));
            Assert.AreEqual(RoadPolygonPortalSuggestionKind.Lane, suggestion.kind);
            Assert.AreEqual(lane, suggestion.lane);
            Assert.AreEqual(RoadLaneEndpoint.Start, suggestion.endpoint);
            Assert.Null(source.LinkedLane);
            Assert.Null(source.LinkedPortal);

            Assert.True(RoadPolygonAuthoringUtility.ApplyPortalSuggestion(source, suggestion, true));
            Assert.AreEqual(lane, source.LinkedLane);
            Assert.True(source.LinkedLaneReverse);
            Assert.Null(source.LinkedPortal);

            RoadPortal targetPortal = CreatePortal(zone, "target", new Vector2(1f, 5f));
            Assert.True(RoadPolygonAuthoringUtility.TryFindPortalSuggestion(
                network,
                source,
                4f,
                out suggestion));
            Assert.AreEqual(RoadPolygonPortalSuggestionKind.Portal, suggestion.kind);
            Assert.AreEqual(targetPortal, suggestion.portal);

            Assert.True(RoadPolygonAuthoringUtility.ApplyPortalSuggestion(source, suggestion, false));
            Assert.AreEqual(targetPortal, source.LinkedPortal);
            Assert.Null(source.LinkedLane);
            Assert.False(source.LinkedLaneReverse);
        }

        [Test]
        public void ValidateAndBakeIncludeLinkedPolygonPortal()
        {
            RoadLaneNetwork network = CreateNetwork();
            RoadPolygonZone zone = CreateZone(network, "plaza");
            RoadLane lane = CreateLane(network, "lane", new Vector3(0f, 0f, 7f), new Vector3(0f, 0f, 12f));
            RoadPortal portal = CreatePortal(zone, "entry", new Vector2(0f, 5f));
            portal.LinkedLane = lane;
            portal.LinkedLaneEndpoint = RoadLaneEndpoint.Start;

            List<RoadLaneValidationIssue> issues = network.ValidateNetwork();
            Assert.False(issues.Any(issue => issue.code == "InvalidPolygon" || issue.code == "InvalidPortal"));

            BakedLaneNetwork baked = Track(network.BakeNetwork());
            Assert.AreEqual(1, baked.Polygons.Count);
            Assert.AreEqual(1, baked.Portals.Count);
            Assert.AreEqual("plaza", baked.Polygons[0].zoneId);
            Assert.AreEqual("plaza_entry", baked.Portals[0].portalId);
        }

        [Test]
        public void LivePreviewIncludesPolygonPortalAndKeepsInvalidPortalIssue()
        {
            RoadLaneNetwork network = CreateNetwork();
            previewNetworks.Add(network);
            RoadPolygonZone zone = CreateZone(network, "plaza");
            RoadLane lane = CreateLane(network, "lane", new Vector3(0f, 0f, 7f), new Vector3(0f, 0f, 12f));
            RoadPortal portal = CreatePortal(zone, "entry", new Vector2(0f, 5f));
            portal.LinkedLane = lane;
            portal.LinkedLaneEndpoint = RoadLaneEndpoint.Start;

            BakedLaneNetwork preview = RoadNetworkLivePreviewCoordinator.RebuildNowForTests(network);
            Assert.NotNull(preview);
            Assert.AreEqual(1, preview.Polygons.Count);
            Assert.AreEqual(1, preview.Portals.Count);

            portal.LinkedLane = null;
            preview = RoadNetworkLivePreviewCoordinator.RebuildNowForTests(network);
            Assert.Null(preview);
            Assert.True(RoadNetworkLivePreviewCoordinator
                .GetIssues(network)
                .Any(issue => issue.code == "InvalidPortal"));
        }

        private RoadLaneNetwork CreateNetwork()
        {
            GameObject root = Track(new GameObject("Network"));
            return root.AddComponent<RoadLaneNetwork>();
        }

        private RoadPolygonZone CreateZone(RoadLaneNetwork network, string zoneId)
        {
            GameObject zoneObject = Track(new GameObject(zoneId));
            zoneObject.transform.SetParent(network.transform, false);
            RoadPolygonZone zone = zoneObject.AddComponent<RoadPolygonZone>();
            zone.ZoneId = zoneId;
            zone.Vertices.Clear();
            zone.Vertices.Add(new Vector2(-5f, -5f));
            zone.Vertices.Add(new Vector2(5f, -5f));
            zone.Vertices.Add(new Vector2(5f, 5f));
            zone.Vertices.Add(new Vector2(-5f, 5f));
            zone.MinimumHeight = 0f;
            zone.Height = 3f;
            return zone;
        }

        private RoadLane CreateLane(RoadLaneNetwork network, string laneId, Vector3 start, Vector3 end)
        {
            GameObject laneObject = Track(new GameObject(laneId));
            laneObject.transform.SetParent(network.transform, false);
            SplineContainer container = laneObject.AddComponent<SplineContainer>();
            Spline spline = new Spline();
            spline.Add(start, TangentMode.Linear);
            spline.Add(end, TangentMode.Linear);
            container.Spline = spline;
            RoadLane lane = laneObject.AddComponent<RoadLane>();
            lane.LaneId = laneId;
            lane.TravelDirection = RoadLaneTravelDirection.Forward;
            lane.Width = 3f;
            return lane;
        }

        private RoadPortal CreatePortal(RoadPolygonZone zone, string portalId, Vector2 localPosition)
        {
            GameObject portalObject = Track(new GameObject(portalId));
            portalObject.transform.SetParent(zone.transform, false);
            RoadPortal portal = portalObject.AddComponent<RoadPortal>();
            portal.PortalId = portalId;
            Assert.True(RoadPolygonAuthoringUtility.TryProjectToBoundary(
                zone,
                zone.LocalVertexToWorld(localPosition),
                out RoadPolygonBoundaryProjection projection));
            portalObject.transform.position = projection.worldPoint;
            portalObject.transform.rotation = RoadPolygonAuthoringUtility.GetPortalRotation(
                zone,
                projection.worldTangent);
            return portal;
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
