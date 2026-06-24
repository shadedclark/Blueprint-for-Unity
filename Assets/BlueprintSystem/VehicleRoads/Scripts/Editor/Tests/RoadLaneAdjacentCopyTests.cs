using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VehicleRoads;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads.Editor.Tests
{
    public sealed class RoadLaneAdjacentCopyTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void CopyUsesTravelDirectionForLeftAndRight()
        {
            RoadLaneNetwork network = CreateNetwork();
            RoadLane forward = CreateLane(
                "forward",
                new[] { Vector3.zero, Vector3.forward * 10f },
                network.transform);
            RoadLane reverse = CreateLane(
                "reverse",
                new[] { Vector3.right * 10f, Vector3.right * 10f + Vector3.forward * 10f },
                network.transform);
            reverse.TravelDirection = RoadLaneTravelDirection.Reverse;
            RoadLane bidirectional = CreateLane(
                "bidirectional",
                new[] { Vector3.right * 20f, Vector3.right * 20f + Vector3.forward * 10f },
                network.transform);
            bidirectional.TravelDirection = RoadLaneTravelDirection.Bidirectional;

            AssertCopy(forward, RoadLaneAdjacentSide.Left, 3.5f, new Vector3(-3.5f, 0f, 0f));
            AssertCopy(forward, RoadLaneAdjacentSide.Right, 3.5f, new Vector3(3.5f, 0f, 0f));
            AssertCopy(reverse, RoadLaneAdjacentSide.Left, 3.5f, new Vector3(3.5f, 0f, 0f));
            AssertCopy(reverse, RoadLaneAdjacentSide.Right, 3.5f, new Vector3(-3.5f, 0f, 0f));
            AssertCopy(bidirectional, RoadLaneAdjacentSide.Left, 3.5f, new Vector3(-3.5f, 0f, 0f));
        }

        [Test]
        public void CopyPreservesSplineMetadataAndLaneSettingsWithoutChangingSource()
        {
            RoadLaneNetwork network = CreateNetwork();
            RoadLane source = CreateLane(
                "curved_slope",
                new[]
                {
                    new Vector3(0f, 1f, 0f),
                    new Vector3(2f, 2f, 5f),
                    new Vector3(5f, 4f, 10f)
                },
                network.transform);
            source.TravelDirection = RoadLaneTravelDirection.Forward;
            source.SpeedLimit = 17f;
            source.TagMask = RoadTagMask.Road | RoadTagMask.Vehicle | RoadTagMask.Service;
            source.AllowedAgents = RoadAgentMask.Car | RoadAgentMask.Bus;
            source.Open = false;
            source.ConnectionMode = RoadLaneConnectionMode.Manual;
            source.ManualNextLaneIds = "other_lane";
            source.LateralOffset = 0.4f;
            source.VerticalOffset = 0.2f;
            source.SampleSpacingOverride = 0.75f;

            Spline sourceSpline = source.Spline;
            sourceSpline.SetTangentMode(0, TangentMode.Broken);
            BezierKnot firstKnot = sourceSpline[0];
            firstKnot.TangentOut = new float3(1f, 0.5f, 2f);
            firstKnot.Rotation = quaternion.EulerXYZ(0.1f, 0.2f, 0.3f);
            sourceSpline.SetKnot(0, firstKnot);
            sourceSpline.SetTangentMode(1, TangentMode.AutoSmooth);
            sourceSpline.SetAutoSmoothTension(1, 0.7f);
            sourceSpline.SetTangentMode(2, TangentMode.Mirrored);

            Vector3[] sourcePositions = GetWorldKnotPositions(source);
            BezierKnot originalFirstKnot = sourceSpline[0];

            Assert.True(
                RoadLaneAdjacentCopyUtility.TryCopyAdjacentLane(
                    source,
                    RoadLaneAdjacentSide.Right,
                    3.5f,
                    out RoadLane copied,
                    out string error,
                    false),
                error);

            Assert.AreEqual(RoadLaneTravelDirection.Forward, copied.TravelDirection);
            Assert.AreEqual(17f, copied.SpeedLimit);
            Assert.AreEqual(source.TagMask, copied.TagMask);
            Assert.AreEqual(source.AllowedAgents, copied.AllowedAgents);
            Assert.False(copied.Open);
            Assert.AreEqual(RoadLaneConnectionMode.Automatic, copied.ConnectionMode);
            Assert.AreEqual(string.Empty, copied.ManualNextLaneIds);
            Assert.AreEqual(0.4f, copied.LateralOffset);
            Assert.AreEqual(0.2f, copied.VerticalOffset);
            Assert.AreEqual(0.75f, copied.SampleSpacingOverride);
            Assert.AreEqual(sourceSpline.Count, copied.Spline.Count);

            for (int i = 0; i < sourceSpline.Count; i++)
            {
                Assert.AreEqual(sourceSpline.GetTangentMode(i), copied.Spline.GetTangentMode(i));
                Assert.That(
                    copied.Spline.GetAutoSmoothTension(i),
                    Is.EqualTo(sourceSpline.GetAutoSmoothTension(i)).Within(0.0001f));
                Assert.That(
                    Vector3.Distance(sourcePositions[i], GetWorldKnotPosition(copied, i)),
                    Is.EqualTo(3.5f).Within(0.001f));
                Assert.That(
                    Vector3.Distance(sourcePositions[i], GetWorldKnotPosition(source, i)),
                    Is.LessThan(0.0001f));
            }

            AssertFloat3(copied.Spline[0].TangentIn, originalFirstKnot.TangentIn);
            AssertFloat3(copied.Spline[0].TangentOut, originalFirstKnot.TangentOut);
            AssertFloat4(copied.Spline[0].Rotation.value, originalFirstKnot.Rotation.value);
            AssertFloat3(sourceSpline[0].Position, originalFirstKnot.Position);
            AssertFloat3(sourceSpline[0].TangentOut, originalFirstKnot.TangentOut);
        }

        [Test]
        public void CopyGeneratesUniqueIdsAndPreservesBlockedMode()
        {
            RoadLaneNetwork network = CreateNetwork();
            RoadLane source = CreateLane(
                "main",
                new[] { Vector3.zero, Vector3.forward * 10f },
                network.transform);
            source.ConnectionMode = RoadLaneConnectionMode.Blocked;

            Assert.True(
                RoadLaneAdjacentCopyUtility.TryCopyAdjacentLane(
                    source,
                    RoadLaneAdjacentSide.Right,
                    3.5f,
                    out RoadLane first,
                    out string firstError,
                    false),
                firstError);
            Assert.True(
                RoadLaneAdjacentCopyUtility.TryCopyAdjacentLane(
                    source,
                    RoadLaneAdjacentSide.Right,
                    3.5f,
                    out RoadLane second,
                    out string secondError,
                    false),
                secondError);

            Assert.AreEqual("main_right", first.LaneId);
            Assert.AreEqual("main_right_2", second.LaneId);
            Assert.AreEqual(RoadLaneConnectionMode.Blocked, first.ConnectionMode);
            Assert.AreEqual(RoadLaneConnectionMode.Blocked, second.ConnectionMode);
        }

        [Test]
        public void CopyRejectsConnectorsAndInvalidSplines()
        {
            RoadLaneNetwork network = CreateNetwork();
            RoadLane connector = CreateLane(
                "connector",
                new[] { Vector3.zero, Vector3.forward * 5f },
                network.transform);
            connector.SetKind(RoadLaneKind.Connector);

            Assert.False(
                RoadLaneAdjacentCopyUtility.TryCopyAdjacentLane(
                    connector,
                    RoadLaneAdjacentSide.Left,
                    3.5f,
                    out RoadLane connectorCopy,
                    out string connectorError,
                    false));
            Assert.IsNull(connectorCopy);
            StringAssert.Contains("Connector", connectorError);

            RoadLane invalid = CreateLane(
                "invalid",
                new[] { Vector3.zero, Vector3.forward * 5f },
                network.transform);
            invalid.Spline.Closed = true;

            Assert.False(
                RoadLaneAdjacentCopyUtility.TryCopyAdjacentLane(
                    invalid,
                    RoadLaneAdjacentSide.Left,
                    3.5f,
                    out RoadLane invalidCopy,
                    out string invalidError,
                    false));
            Assert.IsNull(invalidCopy);
            StringAssert.Contains("open Spline", invalidError);
        }

        [Test]
        public void DefaultSpacingBakesAsAdjacentLinks()
        {
            RoadLaneNetwork network = CreateNetwork();
            RoadLane source = CreateLane(
                "source",
                new[] { Vector3.zero, Vector3.forward * 20f },
                network.transform);

            Assert.True(
                RoadLaneAdjacentCopyUtility.TryCopyAdjacentLane(
                    source,
                    RoadLaneAdjacentSide.Right,
                    RoadLaneAdjacentCopyUtility.DefaultLaneSpacing,
                    out RoadLane copied,
                    out string error,
                    false),
                error);

            BakedLaneNetwork baked = Track(network.BakeNetwork());
            BakedLaneAdjacentLinkRecord sourceToCopy = baked.AdjacentLinks.SingleOrDefault(
                link => link.fromLaneId == source.LaneId &&
                        link.toLaneId == copied.LaneId &&
                        link.side == RoadLaneAdjacentSide.Right);
            BakedLaneAdjacentLinkRecord copyToSource = baked.AdjacentLinks.SingleOrDefault(
                link => link.fromLaneId == copied.LaneId &&
                        link.toLaneId == source.LaneId &&
                        link.side == RoadLaneAdjacentSide.Left);

            Assert.NotNull(sourceToCopy);
            Assert.NotNull(copyToSource);
            Assert.That(sourceToCopy.minLateralDistance, Is.EqualTo(3.5f).Within(0.02f));
            Assert.That(sourceToCopy.maxLateralDistance, Is.EqualTo(3.5f).Within(0.02f));
        }

        [Test]
        public void CopyCanBeUndoneWithoutRemovingSourceLane()
        {
            RoadLaneNetwork network = CreateNetwork();
            RoadLane source = CreateLane(
                "undo_source",
                new[] { Vector3.zero, Vector3.forward * 10f },
                network.transform);

            Assert.True(
                RoadLaneAdjacentCopyUtility.TryCopyAdjacentLane(
                    source,
                    RoadLaneAdjacentSide.Left,
                    3.5f,
                    out RoadLane copied,
                    out string error,
                    false),
                error);
            Assert.NotNull(copied);
            Assert.AreEqual(2, network.GetAuthoredLanes().Length);

            Undo.PerformUndo();

            Assert.IsTrue(copied == null);
            RoadLane[] remainingLanes = network.GetAuthoredLanes();
            Assert.AreEqual(1, remainingLanes.Length);
            Assert.AreSame(source, remainingLanes[0]);
        }

        private void AssertCopy(
            RoadLane source,
            RoadLaneAdjacentSide side,
            float spacing,
            Vector3 expectedWorldOffset)
        {
            Vector3 sourcePosition = GetWorldKnotPosition(source, 0);
            Assert.True(
                RoadLaneAdjacentCopyUtility.TryCopyAdjacentLane(
                    source,
                    side,
                    spacing,
                    out RoadLane copied,
                    out string error,
                    false),
                error);
            Vector3 copiedPosition = GetWorldKnotPosition(copied, 0);
            Assert.That(
                Vector3.Distance(copiedPosition, sourcePosition + expectedWorldOffset),
                Is.LessThan(0.0001f));
        }

        private RoadLaneNetwork CreateNetwork()
        {
            GameObject root = Track(new GameObject("Vehicle Road Network"));
            RoadLaneNetwork network = root.AddComponent<RoadLaneNetwork>();
            network.SampleSpacing = 1f;
            network.ConnectionRadius = 0.1f;
            return network;
        }

        private RoadLane CreateLane(
            string id,
            IReadOnlyList<Vector3> points,
            Transform parent)
        {
            GameObject laneObject = new GameObject(id);
            laneObject.transform.SetParent(parent, false);
            SplineContainer container = laneObject.AddComponent<SplineContainer>();
            Spline spline = new Spline(points.Count, false);
            for (int i = 0; i < points.Count; i++)
            {
                spline.Add(container.transform.InverseTransformPoint(points[i]), TangentMode.Linear);
            }

            container.Spline = spline;
            RoadLane lane = laneObject.AddComponent<RoadLane>();
            lane.LaneId = id;
            return lane;
        }

        private static Vector3[] GetWorldKnotPositions(RoadLane lane)
        {
            Vector3[] positions = new Vector3[lane.Spline.Count];
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = GetWorldKnotPosition(lane, i);
            }

            return positions;
        }

        private static Vector3 GetWorldKnotPosition(RoadLane lane, int index)
        {
            return lane.SplineContainer.transform.TransformPoint(lane.Spline[index].Position);
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static void AssertFloat3(float3 actual, float3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }

        private static void AssertFloat4(float4 actual, float4 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
            Assert.That(actual.w, Is.EqualTo(expected.w).Within(0.0001f));
        }
    }
}
