using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using VehicleRoads;
using VehicleRoads.Editor;
using UnityEditor;
using UnityEditor.Splines;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace VehicleRoads.Editor.Tests
{
    public sealed class RoadLaneNetworkTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            SplineSelection.Clear();
            Selection.activeObject = null;
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void ProjectSettingsProviderUsesVehicleRoadPathAndFindsModuleDefaults()
        {
            SettingsProvider provider = RoadNetworkProjectSettingsProvider.CreateProvider();
            Assert.AreEqual("Project/Vehicle Road/Road Network", provider.settingsPath);

            RoadNetworkSettings networkSettings = RoadNetworkProjectSettingsAssets.GetNetworkSettings(false);
            Assert.NotNull(networkSettings);
            Assert.AreEqual(
                "Assets/BlueprintSystem/VehicleRoads/Settings/RoadNetworkSettings.asset",
                AssetDatabase.GetAssetPath(networkSettings));

            RoadNetworkRuntimeSettings runtimeSettings = RoadNetworkProjectSettingsAssets.GetRuntimeSettings(false);
            Assert.NotNull(runtimeSettings);
            Assert.AreEqual(
                "Assets/BlueprintSystem/VehicleRoads/Settings/RoadNetworkRuntimeSettings.asset",
                AssetDatabase.GetAssetPath(runtimeSettings));
        }

        [Test]
        public void VehicleRoadUserFacingMenusUseVehicleRoadNaming()
        {
            AssertAddComponentMenu<RoadLaneNetwork>("Vehicle Road/Road Lane Network");
            AssertAddComponentMenu<RoadLane>("Vehicle Road/Road Lane");
            AssertAddComponentMenu<RoadJunction>("Vehicle Road/Road Junction");
            AssertAddComponentMenu<VehicleRoadSubsystem>("Vehicle Road/Vehicle Road Subsystem");
            AssertAddComponentMenu<VehicleLaneFollower>("Vehicle Road/Vehicle Lane Follower");
            AssertAddComponentMenu<VehicleRoadTrafficLightVisual>("Vehicle Road/Vehicle Road Traffic Light Visual");
            AssertAddComponentMenu<VehicleRoadTestVehicle>("Vehicle Road/Vehicle Road Test Vehicle");
            AssertAddComponentMenu<RoadLaneProfileSource>("Vehicle Road/Road Network/Lane Profile Source");
            AssertAddComponentMenu<RoadAgent>("Vehicle Road/Road Network/Road Agent");
            AssertAddComponentMenu<RoadPolygonZone>("Vehicle Road/Road Network/Polygon Zone");
            AssertAddComponentMenu<RoadPortal>("Vehicle Road/Road Network/Road Portal");

            AssertCreateAssetMenu<RoadNetworkSettings>("Vehicle Road/Road Network/Network Settings");
            AssertCreateAssetMenu<RoadNetworkRuntimeSettings>("Vehicle Road/Road Network/Runtime Diagnostics Settings");
            AssertCreateAssetMenu<RoadLaneProfile>("Vehicle Road/Road Network/Lane Profile");
            AssertCreateAssetMenu<RoadAgentProfile>("Vehicle Road/Road Network/Road Agent Profile");
            AssertCreateAssetMenu<BakedLaneNetwork>("Vehicle Road/Road Network/Baked Lane Network");

            List<string> menuPaths = GetEditorMenuPaths();
            Assert.Contains("GameObject/Vehicle Road/Vehicle Road Network", menuPaths);
            Assert.Contains("Tools/Blueprint System/Vehicle Road/Scene Authoring Tool", menuPaths);
            Assert.Contains("Tools/Blueprint System/Vehicle Road/Road Network Runtime Debug", menuPaths);

            for (int i = 0; i < menuPaths.Count; i++)
            {
                AssertNoLegacyRoadBrand(menuPaths[i]);
            }
        }

        [Test]
        public void UnitySplineGeometrySamplesWorldSpaceForwardUpCurvatureAndOffsets()
        {
            RoadLane lane = CreateLane(
                "slope",
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0f, 2f, 10f)
                });
            lane.LateralOffset = 1f;
            lane.VerticalOffset = 0.5f;
            UnitySplineRoadLaneGeometry geometry = new UnitySplineRoadLaneGeometry();

            Assert.That(geometry.GetLength(lane), Is.EqualTo(Mathf.Sqrt(104f)).Within(0.02f));
            Assert.True(geometry.TryEvaluate(lane, geometry.GetLength(lane) * 0.5f, false, out RoadLanePose pose));
            Assert.That(pose.forward.y, Is.GreaterThan(0f));
            Assert.That(pose.up.sqrMagnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(pose.position, Is.Not.EqualTo(pose.splinePosition));
            Assert.That(pose.curvature, Is.LessThan(0.001f));
        }

        [Test]
        public void UnitySplineGeometryDoesNotReportEndpointCurvatureForLinearLane()
        {
            RoadLane lane = CreateLane(
                "linear",
                new[]
                {
                    new Vector3(-333.68185f, 69.76448f, 13.723901f),
                    new Vector3(-389.63696f, 69.76448f, 13.423538f)
                });
            UnitySplineRoadLaneGeometry geometry = new UnitySplineRoadLaneGeometry();
            float length = geometry.GetLength(lane);
            float[] distances = { 0f, 0.05f, 0.25f, length - 0.05f, length };

            for (int i = 0; i < distances.Length; i++)
            {
                Assert.True(geometry.TryEvaluate(lane, distances[i], false, out RoadLanePose pose));
                Assert.That(
                    pose.curvature,
                    Is.LessThan(0.001f),
                    "Linear lane curvature should stay near zero at distance " + distances[i].ToString("0.###"));
            }
        }

        [Test]
        public void BakeCreatesSchemaThreeForwardReverseAndBoundarySamples()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            RoadLane lane = CreateLane(
                "two_way",
                new[] { Vector3.zero, Vector3.forward * 10f },
                authoringNetwork.transform);
            lane.TravelDirection = RoadLaneTravelDirection.Bidirectional;
            BakedLaneNetwork network = Track(authoringNetwork.BakeNetwork());

            Assert.AreEqual(2, network.Lanes.Count);
            Assert.AreEqual("two_way", network.Lanes[0].laneId);
            Assert.AreEqual("two_way_rev", network.Lanes[1].laneId);
            Assert.That(network.Samples[0].forward.z, Is.GreaterThan(0.99f));
            Assert.That(network.Samples[network.Lanes[1].firstSampleIndex].forward.z, Is.LessThan(-0.99f));
            Assert.AreEqual("3.2", network.SchemaVersion);
            Assert.That(
                Vector3.Distance(network.Samples[0].leftBoundary, network.Samples[0].rightBoundary),
                Is.EqualTo(3.5f).Within(0.05f));
        }

        [Test]
        public void BakeStoresLocalLaneWidthsAndQueriesUseInterpolatedWidth()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            authoringNetwork.SampleSpacing = 1f;
            RoadLane lane = CreateLane(
                "taper",
                new[] { Vector3.zero, Vector3.forward * 10f },
                authoringNetwork.transform);
            lane.Width = 4f;
            lane.SetWidthKeys(new[]
            {
                new RoadLaneWidthKey { normalizedDistance = 0f, width = 6f },
                new RoadLaneWidthKey { normalizedDistance = 1f, width = 2f }
            });

            BakedLaneNetwork baked = Track(authoringNetwork.BakeNetwork());
            BakedLaneRecord record = baked.Lanes.Single(item => item.laneId == "taper");
            Assert.That(record.minimumWidth, Is.EqualTo(2f).Within(0.05f));
            Assert.That(record.maximumWidth, Is.EqualTo(6f).Within(0.05f));
            Assert.That(baked.Samples[record.firstSampleIndex].width, Is.EqualTo(6f).Within(0.05f));
            Assert.That(
                baked.Samples[record.firstSampleIndex + record.sampleCount - 1].width,
                Is.EqualTo(2f).Within(0.05f));

            Assert.True(baked.TryFindNearestElement(
                new Vector3(0f, 0f, 0.5f),
                RoadAgentMask.Car,
                RoadTagFilter.MatchAll,
                1.4f,
                3f,
                2f,
                out RoadLocation wideLocation));
            Assert.AreEqual("taper", wideLocation.elementId);
            Assert.False(baked.TryFindNearestElement(
                new Vector3(0f, 0f, 9.5f),
                RoadAgentMask.Car,
                RoadTagFilter.MatchAll,
                1.4f,
                0.3f,
                2f,
                out _));
        }

        [Test]
        public void LaneSplitPreservesCurveAndMovesEndReferences()
        {
            RoadLaneNetwork network = CreateNetwork();
            RoadLane lane = CreateLane(
                "main",
                new[]
                {
                    Vector3.zero,
                    new Vector3(3f, 0f, 5f),
                    new Vector3(0f, 0f, 10f)
                },
                network.transform);
            lane.Spline.SetTangentMode(1, TangentMode.AutoSmooth);
            GameObject portalObject = Track(new GameObject("Portal"));
            portalObject.transform.SetParent(network.transform);
            RoadPortal portal = portalObject.AddComponent<RoadPortal>();
            portal.LinkedLane = lane;
            portal.LinkedLaneEndpoint = RoadLaneEndpoint.End;

            Vector3 expected = lane.SplineContainer.EvaluatePosition(0.45f);
            Assert.True(RoadLaneTopologyUtility.TrySplitLane(
                network,
                lane,
                0.45f,
                out RoadLane second,
                out string error), error);

            Assert.NotNull(second);
            Assert.AreSame(second, portal.LinkedLane);
            Assert.That(
                Vector3.Distance(lane.SplineContainer.EvaluatePosition(1f), expected),
                Is.LessThan(0.02f));
            Assert.That(
                Vector3.Distance(second.SplineContainer.EvaluatePosition(0f), expected),
                Is.LessThan(0.02f));
            Assert.AreEqual(RoadLaneConnectionMode.Automatic, lane.ConnectionMode);
        }

        [Test]
        public void EndpointAutoConnectSplitsInteriorAndCreatesJunction()
        {
            RoadLaneNetwork network = CreateNetwork();
            RoadLane target = CreateLane(
                "target",
                new[] { new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f) },
                network.transform);
            RoadLane branch = CreateLane(
                "branch",
                new[] { new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, 0.2f) },
                network.transform);

            Assert.True(RoadLaneTopologyUtility.TryAutoConnect(
                network,
                branch,
                RoadLaneEndpoint.End,
                0.1f,
                1f,
                25f,
                true,
                out RoadLaneTopologyBuildResult result));

            Assert.AreEqual(RoadLaneTopologyTargetKind.LaneInterior, result.targetKind);
            Assert.NotNull(result.splitLane);
            Assert.NotNull(result.junction);
            Assert.That(result.junction.Bindings.Count, Is.GreaterThanOrEqualTo(3));
            Assert.AreEqual(2, network.GetAuthoredLanes().Count(item => item.Kind == RoadLaneKind.Standard && item != branch));
            Assert.That(
                Vector3.Distance(branch.SplineContainer.EvaluatePosition(1f), Vector3.zero),
                Is.LessThan(0.02f));
        }

        [Test]
        public void EndpointAutoConnectSplitsProfileSourceAtControlPoint()
        {
            RoadLaneNetwork network = CreateNetwork();
            GameObject sourceObject = Track(new GameObject("ProfileSource"));
            sourceObject.transform.SetParent(network.transform);
            SplineContainer sourceContainer = sourceObject.AddComponent<SplineContainer>();
            sourceContainer.Spline = new Spline(new[]
            {
                new BezierKnot(new float3(-5f, 0f, 0f)),
                new BezierKnot(new float3(5f, 0f, 0f))
            });
            RoadLaneProfileSource source = sourceObject.AddComponent<RoadLaneProfileSource>();
            source.SourceId = "profile";
            RoadLaneProfile profile = Track(ScriptableObject.CreateInstance<RoadLaneProfile>());
            profile.Entries.Clear();
            profile.Entries.Add(new RoadLaneProfileEntry { entryId = "through", width = 3.5f });
            source.Profile = profile;
            Assert.True(source.RefreshManagedLanes(null, null, out string error), error);
            RoadLane target = source.GetComponentsInChildren<RoadLane>(true).Single();
            RoadLane branch = CreateLane(
                "profile_branch",
                new[] { new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, 0.2f) },
                network.transform);
            int originalKnotCount = sourceContainer.Spline.Count;

            Assert.True(RoadLaneTopologyUtility.TryAutoConnect(
                network,
                branch,
                RoadLaneEndpoint.End,
                0.1f,
                1f,
                25f,
                true,
                out RoadLaneTopologyBuildResult result));

            Assert.AreEqual(RoadLaneTopologyTargetKind.ProfileInterior, result.targetKind);
            Assert.AreEqual(originalKnotCount + 1, sourceContainer.Spline.Count);
            Assert.NotNull(result.junction);
            Assert.That(result.junction.Bindings.Count, Is.GreaterThanOrEqualTo(3));
            Assert.True(source.ControlPoints.Any(point => point.forceTopologyBreak));
        }

        [Test]
        public void LivePreviewBuildsHiddenTransientNetworkWithoutAssetPath()
        {
            RoadLaneNetwork network = CreateNetwork();
            CreateLane(
                "preview",
                new[] { Vector3.zero, Vector3.forward * 5f },
                network.transform);

            BakedLaneNetwork preview = RoadNetworkLivePreviewCoordinator.RebuildNowForTests(network);
            try
            {
                Assert.NotNull(preview);
                Assert.AreEqual(HideFlags.HideAndDontSave, preview.hideFlags);
                Assert.AreEqual(string.Empty, AssetDatabase.GetAssetPath(preview));
            }
            finally
            {
                RoadNetworkLivePreviewCoordinator.Unregister(network);
            }
        }

        [Test]
        public void ConnectorGenerationCreatesContinuousEditableLane()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            RoadLane incoming = CreateLane(
                "incoming",
                new[] { new Vector3(0f, 0f, -10f), Vector3.zero },
                authoringNetwork.transform);
            RoadLane outgoing = CreateLane(
                "outgoing",
                new[] { Vector3.zero, new Vector3(10f, 0f, 0f) },
                authoringNetwork.transform);
            GameObject junctionObject = Track(new GameObject("junction"));
            junctionObject.transform.SetParent(authoringNetwork.transform);
            RoadJunction junction = junctionObject.AddComponent<RoadJunction>();
            junction.JunctionId = "junction";
            junction.Bindings.Add(new RoadJunctionBinding { lane = incoming, endpoint = RoadLaneEndpoint.End });
            junction.Bindings.Add(new RoadJunctionBinding { lane = outgoing, endpoint = RoadLaneEndpoint.Start });

            RoadLaneConnectorReport report = authoringNetwork.GenerateConnectors();
            RoadLane connector = authoringNetwork.GetAuthoredLanes().Single(item => item.Kind == RoadLaneKind.Connector);
            UnitySplineRoadLaneGeometry geometry = new UnitySplineRoadLaneGeometry();
            Assert.AreEqual(1, report.created);
            Assert.True(geometry.TryEvaluate(connector, 0f, false, out RoadLanePose start));
            Assert.True(geometry.TryEvaluate(connector, geometry.GetLength(connector), false, out RoadLanePose end));
            Assert.That(Vector3.Distance(start.position, Vector3.zero), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(end.position, Vector3.zero), Is.LessThan(0.001f));
            Assert.AreEqual(RoadLaneTurn.Right, connector.TurnType);
            Assert.AreEqual("junction", connector.ConnectorJunctionId);

            connector.ConnectorLocked = true;
            Assert.AreEqual(1, authoringNetwork.GenerateConnectors().locked);
        }

        [Test]
        public void JunctionRefreshUpdatesOnlyUnlockedConnectorHandleLength()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            RoadLane incoming = CreateLane(
                "incoming",
                new[] { new Vector3(0f, 0f, -10f), Vector3.zero },
                authoringNetwork.transform);
            RoadLane outgoing = CreateLane(
                "outgoing",
                new[] { new Vector3(10f, 0f, 0f), new Vector3(20f, 0f, 0f) },
                authoringNetwork.transform);
            GameObject junctionObject = Track(new GameObject("junction"));
            junctionObject.transform.SetParent(authoringNetwork.transform);
            RoadJunction junction = junctionObject.AddComponent<RoadJunction>();
            junction.JunctionId = "junction";
            junction.Bindings.Add(new RoadJunctionBinding { lane = incoming, endpoint = RoadLaneEndpoint.End });
            junction.Bindings.Add(new RoadJunctionBinding { lane = outgoing, endpoint = RoadLaneEndpoint.Start });

            junction.ConnectorHandleScale = 0.25f;
            authoringNetwork.GenerateConnectors();
            RoadLane connector = authoringNetwork.GetAuthoredLanes().Single(item => item.Kind == RoadLaneKind.Connector);
            Vector3 initialTangent = connector.Spline[0].TangentOut;

            junction.ConnectorHandleScale = 0.8f;
            RoadLaneConnectorReport refreshed = authoringNetwork.RefreshConnectors(junction);
            Vector3 refreshedTangent = connector.Spline[0].TangentOut;
            Assert.AreEqual(1, refreshed.updated);
            Assert.That(refreshedTangent.magnitude, Is.GreaterThan(initialTangent.magnitude));

            connector.ConnectorLocked = true;
            junction.ConnectorHandleScale = 0.4f;
            RoadLaneConnectorReport lockedRefresh = authoringNetwork.RefreshConnectors(junction);
            Vector3 lockedTangent = connector.Spline[0].TangentOut;
            Assert.AreEqual(1, lockedRefresh.locked);
            Assert.That(lockedTangent, Is.EqualTo(refreshedTangent));
        }

        [TestCase(RoadLaneTurnMask.Straight, RoadLaneTurn.Straight, true)]
        [TestCase(RoadLaneTurnMask.Left, RoadLaneTurn.Left, true)]
        [TestCase(RoadLaneTurnMask.Right, RoadLaneTurn.Right, true)]
        [TestCase(RoadLaneTurnMask.UTurn, RoadLaneTurn.UTurn, true)]
        [TestCase(RoadLaneTurnMask.Default, RoadLaneTurn.UTurn, false)]
        [TestCase(RoadLaneTurnMask.Straight, RoadLaneTurn.Left, false)]
        public void EditorTurnMaskMatchesConnectorTurn(
            RoadLaneTurnMask mask,
            RoadLaneTurn turn,
            bool expected)
        {
            Assert.AreEqual(expected, RoadLaneEditorVisualUtility.AllowsTurn(mask, turn));
        }

        [Test]
        public void ConnectorPreviewTurnsRedWhenParentJunctionDisallowsItsTurn()
        {
            GameObject junctionObject = Track(new GameObject("junction"));
            RoadJunction junction = junctionObject.AddComponent<RoadJunction>();
            RoadLane connector = CreateLane(
                "connector",
                new[] { Vector3.zero, Vector3.right * 5f },
                junction.transform);
            connector.ConfigureConnector(
                "key",
                "connector",
                "incoming",
                "outgoing",
                RoadLaneTurn.Right,
                8f,
                1f);

            Color connectorColor = new Color(0.2f, 0.9f, 0.72f, 0.42f);
            junction.AllowedTurns = RoadLaneTurnMask.Right;
            Assert.AreEqual(
                connectorColor,
                RoadLaneEditorVisualUtility.GetLanePreviewColor(connector, Color.blue, connectorColor));

            junction.AllowedTurns = RoadLaneTurnMask.Straight;
            connector.MarkConnectorOrphaned();
            Color disallowedColor =
                RoadLaneEditorVisualUtility.GetLanePreviewColor(connector, Color.blue, connectorColor);
            Assert.AreEqual(RoadLaneEditorVisualUtility.DisallowedConnectorColor.r, disallowedColor.r);
            Assert.AreEqual(RoadLaneEditorVisualUtility.DisallowedConnectorColor.g, disallowedColor.g);
            Assert.AreEqual(RoadLaneEditorVisualUtility.DisallowedConnectorColor.b, disallowedColor.b);
            Assert.AreEqual(connectorColor.a, disallowedColor.a);

            junction.AllowedTurns = RoadLaneTurnMask.Right;
            Assert.AreEqual(
                connectorColor,
                RoadLaneEditorVisualUtility.GetLanePreviewColor(connector, Color.blue, connectorColor));
        }

        [Test]
        public void SelectUtilitiesDoNotModifyJunctionBindingsOrConnectorObjects()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            RoadLane incoming = CreateLane(
                "incoming",
                new[] { new Vector3(0f, 0f, -5f), Vector3.zero },
                authoringNetwork.transform);
            RoadLane outgoing = CreateLane(
                "outgoing",
                new[] { Vector3.zero, Vector3.right * 5f },
                authoringNetwork.transform);
            GameObject junctionObject = Track(new GameObject("junction"));
            junctionObject.transform.SetParent(authoringNetwork.transform);
            RoadJunction junction = junctionObject.AddComponent<RoadJunction>();
            junction.JunctionId = "junction";
            junction.Bindings.Add(new RoadJunctionBinding
            {
                lane = incoming,
                endpoint = RoadLaneEndpoint.End
            });
            junction.Bindings.Add(new RoadJunctionBinding
            {
                lane = outgoing,
                endpoint = RoadLaneEndpoint.Start
            });
            authoringNetwork.GenerateConnectors();

            int bindingCount = junction.Bindings.Count;
            int authoredLaneCount = authoringNetwork.GetAuthoredLanes().Length;

            RoadLaneEditorSelectionUtility.SelectLane(incoming);
            Assert.AreEqual(incoming.gameObject, Selection.activeObject);
            Assert.AreEqual(0, SplineSelection.Count);

            RoadLaneEditorSelectionUtility.SelectKnot(incoming, 1);
            Assert.AreEqual(incoming.gameObject, Selection.activeObject);
            Assert.AreEqual(1, SplineSelection.Count);

            RoadLaneEditorSelectionUtility.SelectJunction(junction);
            Assert.AreEqual(junction.gameObject, Selection.activeObject);
            Assert.AreEqual(0, SplineSelection.Count);

            Assert.AreEqual(bindingCount, junction.Bindings.Count);
            Assert.AreEqual(authoredLaneCount, authoringNetwork.GetAuthoredLanes().Length);
        }

        [Test]
        public void ConnectorTopologyRoutesThroughConnectorInsteadOfJumpingAcrossJunction()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            RoadLane incoming = CreateLane("a", new[] { new Vector3(0f, 0f, -5f), Vector3.zero }, authoringNetwork.transform);
            RoadLane outgoing = CreateLane("b", new[] { Vector3.zero, Vector3.forward * 5f }, authoringNetwork.transform);
            GameObject junctionObject = Track(new GameObject("junction"));
            junctionObject.transform.SetParent(authoringNetwork.transform);
            RoadJunction junction = junctionObject.AddComponent<RoadJunction>();
            junction.Bindings.Add(new RoadJunctionBinding { lane = incoming, endpoint = RoadLaneEndpoint.End });
            junction.Bindings.Add(new RoadJunctionBinding { lane = outgoing, endpoint = RoadLaneEndpoint.Start });
            authoringNetwork.GenerateConnectors();

            BakedLaneNetwork network = Track(authoringNetwork.BakeNetwork());
            string connectorId = network.Lanes.Single(item => item.kind == RoadLaneKind.Connector).laneId;
            Assert.True(network.Connections.Any(item => item.fromLaneId == "a" && item.toLaneId == connectorId));
            Assert.True(network.Connections.Any(item => item.fromLaneId == connectorId && item.toLaneId == "b"));
            Assert.False(network.Connections.Any(item => item.fromLaneId == "a" && item.toLaneId == "b"));
        }

        [Test]
        public void BakeExportsJunctionTrafficAndConnectorTraffic()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            RoadLane incoming = CreateLane("incoming", new[] { new Vector3(0f, 0f, -8f), Vector3.zero }, authoringNetwork.transform);
            RoadLane outgoing = CreateLane("outgoing", new[] { Vector3.zero, Vector3.forward * 8f }, authoringNetwork.transform);
            GameObject junctionObject = Track(new GameObject("signal_junction"));
            junctionObject.transform.SetParent(authoringNetwork.transform);
            RoadJunction junction = junctionObject.AddComponent<RoadJunction>();
            junction.JunctionId = "signal_junction";
            junction.TrafficControlMode = RoadJunctionTrafficControlMode.FixedSignal;
            junction.DefaultStopLineDistance = -1.5f;
            junction.SignalPhases.Add(new RoadJunctionSignalPhase
            {
                phaseId = "straight",
                allowedTurns = RoadLaneTurnMask.Straight,
                greenDuration = 4f,
                yellowDuration = 1f,
                allRedDuration = 1f
            });
            junction.Bindings.Add(new RoadJunctionBinding { lane = incoming, endpoint = RoadLaneEndpoint.End });
            junction.Bindings.Add(new RoadJunctionBinding { lane = outgoing, endpoint = RoadLaneEndpoint.Start });
            authoringNetwork.GenerateConnectors();

            BakedLaneNetwork network = Track(authoringNetwork.BakeNetwork());

            Assert.AreEqual(1, network.JunctionTraffic.Count);
            Assert.AreEqual(1, network.ConnectorTraffic.Count);
            Assert.True(network.TryGetJunctionTraffic("signal_junction", out BakedJunctionTrafficRecord junctionTraffic));
            Assert.AreEqual(RoadJunctionTrafficControlMode.FixedSignal, junctionTraffic.controlMode);
            Assert.AreEqual(-1.5f, junctionTraffic.defaultStopLineDistance);
            Assert.AreEqual(1, junctionTraffic.signalPhases.Count);
            Assert.True(network.TryGetConnectorTraffic(network.ConnectorTraffic[0].connectorLaneId, out BakedConnectorTrafficRecord connectorTraffic));
            Assert.AreEqual("signal_junction", connectorTraffic.junctionId);
            Assert.AreEqual(-1.5f, connectorTraffic.stopLineDistance);
        }

        [Test]
        public void BakeDoesNotConflictSeparatedConnectorsInSameJunction()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            CreateLane("source_a", new[] { new Vector3(-2f, 0f, 0f), Vector3.zero }, authoringNetwork.transform);
            CreateLane("target_a", new[] { new Vector3(10f, 0f, 0f), new Vector3(12f, 0f, 0f) }, authoringNetwork.transform);
            CreateLane("source_b", new[] { new Vector3(-2f, 0f, 10f), new Vector3(0f, 0f, 10f) }, authoringNetwork.transform);
            CreateLane("target_b", new[] { new Vector3(10f, 0f, 10f), new Vector3(12f, 0f, 10f) }, authoringNetwork.transform);
            CreateConfiguredConnector(
                "connector_a",
                "source_a",
                "target_a",
                "junction",
                RoadLaneTurn.Straight,
                new[] { Vector3.zero, new Vector3(10f, 0f, 0f) },
                authoringNetwork.transform,
                1f);
            CreateConfiguredConnector(
                "connector_b",
                "source_b",
                "target_b",
                "junction",
                RoadLaneTurn.Straight,
                new[] { new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 10f) },
                authoringNetwork.transform,
                1f);
            GameObject junctionObject = Track(new GameObject("junction"));
            junctionObject.transform.SetParent(authoringNetwork.transform);
            RoadJunction junction = junctionObject.AddComponent<RoadJunction>();
            junction.JunctionId = "junction";
            junction.ConnectorConflictSafetyMargin = 0.5f;

            BakedLaneNetwork network = Track(authoringNetwork.BakeNetwork());

            BakedConnectorTrafficRecord connectorA = GetConnectorTraffic(network, "connector_a");
            BakedConnectorTrafficRecord connectorB = GetConnectorTraffic(network, "connector_b");
            Assert.AreEqual(0, connectorA.conflicts.Count);
            Assert.AreEqual(0, connectorB.conflicts.Count);
            Assert.That(connectorA.conflictConnectorLaneIds, Is.Empty);
            Assert.That(connectorB.conflictConnectorLaneIds, Is.Empty);
        }

        [Test]
        public void BakeLimitsSameSourceConnectorConflictToEntryInterval()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            CreateLane("source", new[] { new Vector3(-2f, 0f, 0f), Vector3.zero }, authoringNetwork.transform);
            CreateLane("straight_target", new[] { new Vector3(10f, 0f, 0f), new Vector3(12f, 0f, 0f) }, authoringNetwork.transform);
            CreateLane("right_target", new[] { new Vector3(4f, 0f, 8f), new Vector3(4f, 0f, 10f) }, authoringNetwork.transform);
            CreateConfiguredConnector(
                "straight_connector",
                "source",
                "straight_target",
                "junction",
                RoadLaneTurn.Straight,
                new[] { Vector3.zero, new Vector3(10f, 0f, 0f) },
                authoringNetwork.transform,
                1f);
            CreateConfiguredConnector(
                "right_connector",
                "source",
                "right_target",
                "junction",
                RoadLaneTurn.Right,
                new[] { Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(4f, 0f, 8f) },
                authoringNetwork.transform,
                1f);
            GameObject junctionObject = Track(new GameObject("junction"));
            junctionObject.transform.SetParent(authoringNetwork.transform);
            RoadJunction junction = junctionObject.AddComponent<RoadJunction>();
            junction.JunctionId = "junction";
            junction.ConnectorConflictSafetyMargin = 0.5f;

            BakedLaneNetwork network = Track(authoringNetwork.BakeNetwork());
            BakedConnectorTrafficRecord straight = GetConnectorTraffic(network, "straight_connector");
            BakedLaneRecord straightLane = network.Lanes.Single(lane => lane.laneId == "straight_connector");

            Assert.True(straight.TryGetConflict("right_connector", out BakedConnectorConflictRecord conflict));
            Assert.AreEqual(BakedConnectorConflictReason.SameSource, conflict.reason);
            Assert.That(conflict.selfStartDistance, Is.EqualTo(0f).Within(0.001f));
            Assert.That(conflict.selfEndDistance, Is.GreaterThan(0f));
            Assert.That(conflict.selfEndDistance, Is.LessThan(straightLane.length));
            Assert.True(straight.ConflictsWith("right_connector"));
        }

        [Test]
        public void BakeLimitsCrossingConnectorConflictToCrossingInterval()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            CreateLane("west_source", new[] { new Vector3(-7f, 0f, 0f), new Vector3(-5f, 0f, 0f) }, authoringNetwork.transform);
            CreateLane("east_target", new[] { new Vector3(5f, 0f, 0f), new Vector3(7f, 0f, 0f) }, authoringNetwork.transform);
            CreateLane("south_source", new[] { new Vector3(0f, 0f, -7f), new Vector3(0f, 0f, -5f) }, authoringNetwork.transform);
            CreateLane("north_target", new[] { new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 7f) }, authoringNetwork.transform);
            CreateConfiguredConnector(
                "east_connector",
                "west_source",
                "east_target",
                "junction",
                RoadLaneTurn.Straight,
                new[] { new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f) },
                authoringNetwork.transform,
                1f);
            CreateConfiguredConnector(
                "north_connector",
                "south_source",
                "north_target",
                "junction",
                RoadLaneTurn.Straight,
                new[] { new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, 5f) },
                authoringNetwork.transform,
                1f);
            GameObject junctionObject = Track(new GameObject("junction"));
            junctionObject.transform.SetParent(authoringNetwork.transform);
            RoadJunction junction = junctionObject.AddComponent<RoadJunction>();
            junction.JunctionId = "junction";
            junction.ConnectorConflictSafetyMargin = 0.25f;

            BakedLaneNetwork network = Track(authoringNetwork.BakeNetwork());
            BakedConnectorTrafficRecord east = GetConnectorTraffic(network, "east_connector");
            BakedLaneRecord eastLane = network.Lanes.Single(lane => lane.laneId == "east_connector");

            Assert.True(east.TryGetConflict("north_connector", out BakedConnectorConflictRecord conflict));
            Assert.AreEqual(BakedConnectorConflictReason.Crossing, conflict.reason);
            Assert.That(conflict.selfStartDistance, Is.GreaterThan(0f));
            Assert.That(conflict.selfEndDistance, Is.LessThan(eastLane.length));
            Assert.That(conflict.selfEndDistance - conflict.selfStartDistance, Is.LessThan(eastLane.length * 0.75f));
            Assert.True(east.ConflictsWith("north_connector"));
        }

        [Test]
        public void AdjacentLaneInferenceBuildsLeftRightLinksWithoutRouteConnections()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            authoringNetwork.AdjacentMinimumOverlapLength = 2f;
            CreateLane("left", new[] { Vector3.zero, Vector3.forward * 20f }, authoringNetwork.transform);
            CreateLane("right", new[] { Vector3.right * 3f, Vector3.right * 3f + Vector3.forward * 20f }, authoringNetwork.transform);

            BakedLaneNetwork network = Track(authoringNetwork.BakeNetwork());

            Assert.AreEqual(0, network.Connections.Count);
            Assert.AreEqual(2, network.AdjacentLinks.Count);
            Assert.True(network.TryGetAdjacentLane("left", RoadLaneAdjacentSide.Right, out BakedLaneAdjacentLinkRecord rightLink));
            Assert.AreEqual("right", rightLink.toLaneId);
            Assert.True((rightLink.flags & RoadLaneAdjacentFlags.Auto) != 0);
            Assert.True((rightLink.flags & RoadLaneAdjacentFlags.LaneChangeAllowed) != 0);
            Assert.True(network.TryGetAdjacentLane("right", RoadLaneAdjacentSide.Left, out BakedLaneAdjacentLinkRecord leftLink));
            Assert.AreEqual("left", leftLink.toLaneId);
            Assert.AreEqual(1, network.GetLaneChangeLinks("left", RoadAgentMask.Car).Count);
            Assert.AreEqual(2, network.Summary.adjacentLinkCount);
            Assert.AreEqual(2, network.Summary.laneChangeLinkCount);
        }

        [Test]
        public void AdjacentLaneInferenceRejectsReverseHeightAngleAndDistance()
        {
            AssertNoAdjacentLinks("reverse", new[] { new Vector3(3f, 0f, 20f), new Vector3(3f, 0f, 0f) });
            AssertNoAdjacentLinks("height", new[] { new Vector3(3f, 2f, 0f), new Vector3(3f, 2f, 20f) });
            AssertNoAdjacentLinks("angle", new[] { new Vector3(3f, 0f, 0f), new Vector3(23f, 0f, 0f) });
            AssertNoAdjacentLinks("distance", new[] { new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 20f) });
        }

        [Test]
        public void AdjacentLaneInferenceMarksPartialOverlapAsMergeAndSplit()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            authoringNetwork.AdjacentMinimumOverlapLength = 4f;
            CreateLane("source", new[] { Vector3.zero, Vector3.forward * 20f }, authoringNetwork.transform);
            CreateLane(
                "partial",
                new[] { new Vector3(3f, 0f, 6f), new Vector3(3f, 0f, 14f) },
                authoringNetwork.transform);

            BakedLaneNetwork network = Track(authoringNetwork.BakeNetwork());

            Assert.True(network.TryGetAdjacentLane("source", RoadLaneAdjacentSide.Right, out BakedLaneAdjacentLinkRecord link));
            Assert.AreEqual("partial", link.toLaneId);
            Assert.True((link.flags & RoadLaneAdjacentFlags.Merge) != 0);
            Assert.True((link.flags & RoadLaneAdjacentFlags.Split) != 0);
            Assert.That(link.overlapStartDistance, Is.GreaterThan(4f));
            Assert.That(link.overlapEndDistance, Is.LessThan(16f));
        }

        [Test]
        public void BakeStoresAdjacentLinksAndSummaryCountsInAsset()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            authoringNetwork.AdjacentMinimumOverlapLength = 2f;
            CreateLane("left", new[] { Vector3.zero, Vector3.forward * 20f }, authoringNetwork.transform);
            CreateLane("right", new[] { Vector3.right * 3f, Vector3.right * 3f + Vector3.forward * 20f }, authoringNetwork.transform);
            BakedLaneNetwork network = Track(authoringNetwork.BakeNetwork());

            Assert.AreEqual(2, network.AdjacentLinks.Count);
            Assert.AreEqual(2, network.Summary.adjacentLinkCount);
            Assert.AreEqual(2, network.Summary.laneChangeLinkCount);
        }

        [Test]
        public void LaneGraphRespectsClosureAndDynamicCongestion()
        {
            BakedLaneNetwork network = Track(CreateRouteNetwork());
            MutableLaneTrafficCostProvider traffic = new MutableLaneTrafficCostProvider();
            LaneGraph graph = new LaneGraph(network, new[] { traffic });

            Assert.True(graph.TryFindRoute(new LaneRouteQuery("start", "goal", RoadAgentMask.Car), out List<string> firstRoute, out _));
            Assert.AreEqual("short", firstRoute[1]);

            traffic.SetLaneClosed("short", true);
            Assert.True(graph.TryFindRoute(new LaneRouteQuery("start", "goal", RoadAgentMask.Car), out List<string> secondRoute, out _));
            Assert.AreEqual("long", secondRoute[1]);

            traffic.SetLaneClosed("short", false);
            traffic.SetCongestionCost("short", 100f);
            Assert.True(graph.TryFindRoute(new LaneRouteQuery("start", "goal", RoadAgentMask.Car), out List<string> thirdRoute, out _));
            Assert.AreEqual("long", thirdRoute[1]);
        }

        [Test]
        public void NearestLaneRecoverySeparatesVerticallyOverlappingRoads()
        {
            BakedLaneNetwork network = Track(CreateOverpassNetwork());
            Assert.True(network.TryFindNearestLane(
                new Vector3(2f, 9.8f, 0.2f),
                Vector3.right,
                RoadAgentMask.Car,
                20f,
                2f,
                out BakedLaneNearestResult result));
            Assert.AreEqual("upper", result.lane.laneId);
        }

        [Test]
        public void VehicleFollowerOutputsControlAndTwoLevelRecoveryWithoutMovingVehicle()
        {
            BakedLaneNetwork network = Track(CreateStraightNetwork());
            GameObject followerObject = Track(new GameObject("follower"));
            VehicleLaneFollower follower = followerObject.AddComponent<VehicleLaneFollower>();
            follower.LaneNetwork = network;
            follower.SetRoute(new[] { "lane" });

            VehicleLaneFollowerOutput centered = follower.ComputeControl(new VehicleLaneFollowerInput
            {
                position = new Vector3(0.1f, 0f, 1f),
                forward = Vector3.forward,
                speed = 4f,
                wheelBase = 2.5f,
                agentMask = RoadAgentMask.Car
            });
            Assert.True(centered.valid);
            Assert.AreEqual(VehicleLaneRecoveryMode.None, centered.recoveryMode);
            Assert.That(centered.targetSpeed, Is.GreaterThan(0f));

            VehicleLaneFollowerOutput displaced = follower.ComputeControl(new VehicleLaneFollowerInput
            {
                position = new Vector3(10f, 0f, 1f),
                forward = Vector3.forward,
                speed = 4f,
                wheelBase = 2.5f,
                agentMask = RoadAgentMask.Car
            });
            Assert.True(displaced.valid);
            Assert.AreEqual(VehicleLaneRecoveryMode.Reset, displaced.recoveryMode);
        }

        [Test]
        public void VehicleFollowerIgnoresCurvatureBelowSpeedLimitThreshold()
        {
            BakedLaneNetwork network = Track(CreateStraightNetwork());
            network.Lanes[0].speedLimit = 100f;
            for (int i = 0; i < network.Samples.Count; i++)
            {
                network.Samples[i].curvature = 0.0005f;
            }

            GameObject followerObject = Track(new GameObject("curvature threshold follower"));
            VehicleLaneFollower follower = followerObject.AddComponent<VehicleLaneFollower>();
            follower.LaneNetwork = network;
            follower.SetRoute(new[] { "lane" });

            VehicleLaneFollowerInput input = new VehicleLaneFollowerInput
            {
                position = new Vector3(0f, 0f, 1f),
                forward = Vector3.forward,
                speed = 0f,
                wheelBase = 2.5f,
                agentMask = RoadAgentMask.Car
            };

            VehicleLaneFollowerOutput belowThreshold = follower.ComputeControl(input);
            Assert.True(belowThreshold.valid);
            Assert.That(belowThreshold.targetSpeed, Is.EqualTo(100f).Within(0.001f));

            for (int i = 0; i < network.Samples.Count; i++)
            {
                network.Samples[i].curvature = 0.002f;
            }

            VehicleLaneFollowerOutput aboveThreshold = follower.ComputeControl(input);
            Assert.True(aboveThreshold.valid);
            Assert.That(aboveThreshold.targetSpeed, Is.EqualTo(Mathf.Sqrt(4f / 0.002f)).Within(0.001f));
        }

        [Test]
        public void VehicleRoadSubsystemRegistersNetworksAndAggregatesSnapshot()
        {
            BakedLaneNetwork routeNetwork = Track(CreateRouteNetwork());
            BakedLaneNetwork straightNetwork = Track(CreateSingleStraightNetwork("other", Vector3.right * 10f));
            VehicleRoadSubsystem subsystem = CreateSubsystem();

            Assert.True(subsystem.RegisterNetwork(routeNetwork));
            Assert.True(subsystem.RegisterNetwork(straightNetwork));

            VehicleRoadSubsystemSnapshot snapshot = subsystem.GetSnapshot();
            Assert.AreEqual(2, snapshot.registeredNetworkCount);
            Assert.AreEqual(5, snapshot.laneCount);
            Assert.AreEqual(4, snapshot.connectionCount);
            Assert.AreEqual(0, snapshot.adjacentLinkCount);
            Assert.AreEqual(0, snapshot.invalidRegistrationMessages.Count);
        }

        [Test]
        public void VehicleRoadSubsystemRejectsDuplicateLaneIds()
        {
            BakedLaneNetwork first = Track(CreateSingleStraightNetwork("same", Vector3.zero));
            BakedLaneNetwork second = Track(CreateSingleStraightNetwork("same", Vector3.right * 5f));
            VehicleRoadSubsystem subsystem = CreateSubsystem();

            Assert.True(subsystem.RegisterNetwork(first));
            Assert.False(subsystem.RegisterNetwork(second));

            VehicleRoadSubsystemSnapshot snapshot = subsystem.GetSnapshot();
            Assert.AreEqual(1, snapshot.registeredNetworkCount);
            Assert.Contains("same", snapshot.duplicateLaneIds);
            Assert.That(snapshot.invalidRegistrationMessages.Count, Is.GreaterThan(0));
        }

        [Test]
        public void VehicleRoadSubsystemFindsNearestLaneAcrossRegisteredNetworks()
        {
            BakedLaneNetwork far = Track(CreateSingleStraightNetwork("far", Vector3.right * 10f));
            BakedLaneNetwork near = Track(CreateSingleStraightNetwork("near", Vector3.right));
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(far);
            subsystem.RegisterNetwork(near);

            Assert.True(subsystem.TryFindNearestLane(
                Vector3.zero,
                Vector3.forward,
                RoadAgentMask.Car,
                20f,
                2f,
                out VehicleRoadNearestResult result));

            Assert.AreEqual(near, result.network);
            Assert.AreEqual("near", result.LaneId);
        }

        [Test]
        public void VehicleRoadSubsystemRoutesOnlyInsideOneNetwork()
        {
            BakedLaneNetwork routeNetwork = Track(CreateRouteNetwork());
            BakedLaneNetwork otherNetwork = Track(CreateSingleStraightNetwork("other", Vector3.right * 10f));
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(routeNetwork);
            subsystem.RegisterNetwork(otherNetwork);

            Assert.True(subsystem.TryFindRoute(
                new LaneRouteQuery("start", "goal", RoadAgentMask.Car),
                out VehicleRoadRouteResult route));
            Assert.AreEqual(routeNetwork, route.network);
            Assert.AreEqual("short", route.laneIds[1]);

            Assert.False(subsystem.TryFindRoute(
                new LaneRouteQuery("start", "other", RoadAgentMask.Car),
                out VehicleRoadRouteResult crossNetworkRoute));
            Assert.Null(crossNetworkRoute);
        }

        [Test]
        public void VehicleRoadSubsystemDynamicTrafficStateAffectsRoutesAndSnapshot()
        {
            BakedLaneNetwork network = Track(CreateRouteNetwork());
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            Assert.True(subsystem.TryFindRoute(new LaneRouteQuery("start", "goal", RoadAgentMask.Car), out VehicleRoadRouteResult firstRoute));
            Assert.AreEqual("short", firstRoute.laneIds[1]);

            subsystem.SetLaneClosed("short", true);
            Assert.True(subsystem.TryFindRoute(new LaneRouteQuery("start", "goal", RoadAgentMask.Car), out VehicleRoadRouteResult closedRoute));
            Assert.AreEqual("long", closedRoute.laneIds[1]);
            Assert.AreEqual(1, subsystem.GetSnapshot().closedLaneCount);

            subsystem.SetLaneClosed("short", false);
            subsystem.SetLaneCongestionCost("short", 100f);
            Assert.True(subsystem.TryFindRoute(new LaneRouteQuery("start", "goal", RoadAgentMask.Car), out VehicleRoadRouteResult congestedRoute));
            Assert.AreEqual("long", congestedRoute.laneIds[1]);
            Assert.AreEqual(1, subsystem.GetSnapshot().congestionCostCount);

            subsystem.SetLaneCongestionCost("short", 0f);
            subsystem.SetConnectionSignalCost("start_to_short", 100f);
            Assert.True(subsystem.TryFindRoute(new LaneRouteQuery("start", "goal", RoadAgentMask.Car), out VehicleRoadRouteResult signalRoute));
            Assert.AreEqual("long", signalRoute.laneIds[1]);
            Assert.AreEqual(1, subsystem.GetSnapshot().signalCostCount);

            subsystem.ClearDynamicTrafficState();
            VehicleRoadSubsystemSnapshot cleared = subsystem.GetSnapshot();
            Assert.AreEqual(0, cleared.closedLaneCount);
            Assert.AreEqual(0, cleared.congestionCostCount);
            Assert.AreEqual(0, cleared.signalCostCount);
        }

        [Test]
        public void VehicleRoadSubsystemSignalCostsAffectRoutesAndSnapshot()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(false));
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            Assert.True(subsystem.TryFindRoute(new LaneRouteQuery("start", "goal", RoadAgentMask.Car), out VehicleRoadRouteResult redRoute));
            Assert.AreEqual("bypass", redRoute.laneIds[1]);
            Assert.AreEqual(2, subsystem.GetSnapshot().signalPhaseCount);

            subsystem.AdvanceTraffic(1.1f);
            Assert.True(subsystem.TryFindRoute(new LaneRouteQuery("start", "goal", RoadAgentMask.Car), out VehicleRoadRouteResult greenRoute));
            Assert.AreEqual("connector", greenRoute.laneIds[1]);
        }

        [Test]
        public void VehicleRoadSubsystemExposesTimedJunctionSignalState()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(true));
            BakedJunctionSignalPhaseRecord phase = network.JunctionTraffic[0].signalPhases[0];
            phase.greenDuration = 6f;
            phase.yellowDuration = 2f;
            phase.allRedDuration = 4f;
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            Assert.True(subsystem.TryGetJunctionSignalState(
                "junction",
                RoadLaneTurn.Straight,
                out VehicleRoadSignalState green));
            Assert.AreEqual(VehicleRoadSignalState.Green, green);

            subsystem.AdvanceTraffic(6.1f);
            Assert.True(subsystem.TryGetJunctionSignalState(
                "junction",
                RoadLaneTurn.Straight,
                out VehicleRoadSignalState yellow));
            Assert.AreEqual(VehicleRoadSignalState.Yellow, yellow);

            subsystem.AdvanceTraffic(2f);
            Assert.True(subsystem.TryGetJunctionSignalState(
                "junction",
                RoadLaneTurn.Straight,
                out VehicleRoadSignalState red));
            Assert.AreEqual(VehicleRoadSignalState.Red, red);

            subsystem.AdvanceTraffic(4f);
            Assert.True(subsystem.TryGetJunctionSignalState(
                "junction",
                RoadLaneTurn.Straight,
                out VehicleRoadSignalState nextGreen));
            Assert.AreEqual(VehicleRoadSignalState.Green, nextGreen);
            Assert.False(subsystem.TryGetJunctionSignalState(
                "missing",
                RoadLaneTurn.Straight,
                out VehicleRoadSignalState missing));
            Assert.AreEqual(VehicleRoadSignalState.None, missing);
        }

        [Test]
        public void VehicleRoadSubsystemReportsUncontrolledJunctionAsGreen()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(true));
            network.JunctionTraffic[0].controlMode = RoadJunctionTrafficControlMode.Uncontrolled;
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            Assert.True(subsystem.TryGetJunctionSignalState(
                "junction",
                RoadLaneTurn.Left,
                out VehicleRoadSignalState state));
            Assert.AreEqual(VehicleRoadSignalState.Green, state);
        }

        [Test]
        public void VehicleRoadSubsystemDoesNotGrantPassageBeforeStopLine()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(true));
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            VehicleRoadTrafficControlResult result = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "far_green_car",
                laneId = "start",
                distanceAlongLane = 11f,
                speed = 4f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });

            Assert.AreEqual(VehicleRoadPassageStatus.NotRequired, result.passageStatus);
            Assert.AreEqual(VehicleRoadStopReason.None, result.stopReason);
            Assert.False(result.hasStopPosition);
            Assert.AreEqual(0, subsystem.GetSnapshot().activeTokenCount);
        }

        [Test]
        public void VehicleRoadSubsystemRevokesApproachTokenWhenSignalTurnsRedBeforeConnector()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(true));
            BakedJunctionSignalPhaseRecord phase = network.JunctionTraffic[0].signalPhases[0];
            phase.greenDuration = 0.1f;
            phase.yellowDuration = 0f;
            phase.allRedDuration = 10f;
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            VehicleRoadTrafficControlResult granted = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "slow_green_car",
                laneId = "start",
                distanceAlongLane = 18f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Granted, granted.passageStatus);

            subsystem.AdvanceTraffic(0.2f);
            VehicleRoadTrafficControlResult stopped = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "slow_green_car",
                laneId = "start",
                distanceAlongLane = 18f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });

            Assert.AreEqual(VehicleRoadPassageStatus.Waiting, stopped.passageStatus);
            Assert.AreEqual(VehicleRoadStopReason.TrafficSignal, stopped.stopReason);
            Assert.AreEqual(VehicleRoadSignalState.Red, stopped.signalState);
            Assert.True(stopped.hasStopPosition);
        }

        [Test]
        public void VehicleRoadSubsystemQueuesVehiclesAndReleasesPassageTokens()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(true));
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            VehicleRoadTrafficControlResult first = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "car_a",
                laneId = "start",
                distanceAlongLane = 18f,
                speed = 4f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Granted, first.passageStatus);

            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "car_a",
                laneId = "connector",
                distanceAlongLane = 1f,
                speed = 3f,
                length = 4f,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });

            VehicleRoadTrafficControlResult second = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "car_b",
                laneId = "start",
                distanceAlongLane = 18f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Waiting, second.passageStatus);
            Assert.AreEqual(VehicleRoadStopReason.JunctionConflict, second.stopReason);

            VehicleRoadSubsystemSnapshot queued = subsystem.GetSnapshot();
            Assert.AreEqual(1, queued.activeTokenCount);
            Assert.AreEqual(1, queued.queuedVehicleCount);

            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "car_a",
                laneId = "goal",
                distanceAlongLane = 3f,
                speed = 3f,
                length = 4f,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });

            VehicleRoadTrafficControlResult released = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "car_b",
                laneId = "start",
                distanceAlongLane = 18f,
                speed = 4f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Granted, released.passageStatus);
        }

        [Test]
        public void VehicleRoadSubsystemReleasesSameSourceConnectorConflictAfterEntryInterval()
        {
            BakedLaneNetwork network = Track(CreateSameSourceConflictRuntimeNetwork(false));
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            VehicleRoadTrafficControlResult straight = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "straight_car",
                laneId = "start",
                distanceAlongLane = 8f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "straight_connector", "straight_goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Granted, straight.passageStatus);

            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "straight_car",
                laneId = "straight_connector",
                distanceAlongLane = 1f,
                speed = 1f,
                length = 4f,
                routeLaneIds = new[] { "start", "straight_connector", "straight_goal" }
            });

            VehicleRoadTrafficControlResult blocked = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "right_car",
                laneId = "start",
                distanceAlongLane = 8f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "right_connector", "right_goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Waiting, blocked.passageStatus);
            Assert.AreEqual(VehicleRoadStopReason.JunctionConflict, blocked.stopReason);

            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "straight_car",
                laneId = "straight_connector",
                distanceAlongLane = 4f,
                speed = 1f,
                length = 4f,
                routeLaneIds = new[] { "start", "straight_connector", "straight_goal" }
            });

            VehicleRoadTrafficControlResult released = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "right_car",
                laneId = "start",
                distanceAlongLane = 8f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "right_connector", "right_goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Granted, released.passageStatus);
        }

        [Test]
        public void VehicleRoadSubsystemBlocksCrossingConnectorOnlyInsideConflictInterval()
        {
            BakedLaneNetwork network = Track(CreateCrossingConflictRuntimeNetwork(false));
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            VehicleRoadTrafficControlResult east = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "east_car",
                laneId = "west_start",
                distanceAlongLane = 8f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "west_start", "east_connector", "east_goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Granted, east.passageStatus);

            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "east_car",
                laneId = "east_connector",
                distanceAlongLane = 5f,
                speed = 1f,
                length = 4f,
                routeLaneIds = new[] { "west_start", "east_connector", "east_goal" }
            });

            VehicleRoadTrafficControlResult blocked = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "north_car",
                laneId = "south_start",
                distanceAlongLane = 8f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "south_start", "north_connector", "north_goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Waiting, blocked.passageStatus);
            Assert.AreEqual(VehicleRoadStopReason.JunctionConflict, blocked.stopReason);

            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "east_car",
                laneId = "east_connector",
                distanceAlongLane = 8f,
                speed = 1f,
                length = 4f,
                routeLaneIds = new[] { "west_start", "east_connector", "east_goal" }
            });

            VehicleRoadTrafficControlResult released = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "north_car",
                laneId = "south_start",
                distanceAlongLane = 8f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "south_start", "north_connector", "north_goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Granted, released.passageStatus);
        }

        [Test]
        public void VehicleRoadSubsystemUsesLegacyFullConnectorConflictWhenStructuredConflictsAreMissing()
        {
            BakedLaneNetwork network = Track(CreateCrossingConflictRuntimeNetwork(true));
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            VehicleRoadTrafficControlResult east = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "east_car",
                laneId = "west_start",
                distanceAlongLane = 8f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "west_start", "east_connector", "east_goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Granted, east.passageStatus);

            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "east_car",
                laneId = "east_connector",
                distanceAlongLane = 8f,
                speed = 1f,
                length = 4f,
                routeLaneIds = new[] { "west_start", "east_connector", "east_goal" }
            });

            VehicleRoadTrafficControlResult blocked = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "north_car",
                laneId = "south_start",
                distanceAlongLane = 8f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "south_start", "north_connector", "north_goal" }
            });
            Assert.AreEqual(VehicleRoadPassageStatus.Waiting, blocked.passageStatus);
            Assert.AreEqual(VehicleRoadStopReason.JunctionConflict, blocked.stopReason);
        }

        [Test]
        public void VehicleRoadSubsystemUsesVehicleFrontAndQueueSpacingForStopPositions()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(false));
            network.JunctionTraffic[0].queueSpacing = 2f;
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            VehicleRoadTrafficControlResult first = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "front_stop_car",
                laneId = "start",
                distanceAlongLane = 14f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });

            Assert.AreEqual(VehicleRoadPassageStatus.Waiting, first.passageStatus);
            Assert.AreEqual(VehicleRoadStopReason.TrafficSignal, first.stopReason);
            Assert.AreEqual(0, first.queueIndex);
            Assert.That(first.distanceToStopLine, Is.EqualTo(2f).Within(0.001f));
            Assert.True(first.hasStopPosition);
            Assert.That(first.stopPosition.z, Is.EqualTo(16f).Within(0.001f));

            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "front_stop_car",
                laneId = "start",
                distanceAlongLane = 16f,
                speed = 0f,
                length = 4f,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });

            VehicleRoadTrafficControlResult second = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "queued_stop_car",
                laneId = "start",
                distanceAlongLane = 8f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });

            Assert.AreEqual(VehicleRoadPassageStatus.Waiting, second.passageStatus);
            Assert.AreEqual(VehicleRoadStopReason.TrafficSignal, second.stopReason);
            Assert.AreEqual(1, second.queueIndex);
            Assert.That(second.distanceToStopLine, Is.EqualTo(2f).Within(0.001f));
            Assert.True(second.hasStopPosition);
            Assert.That(second.stopPosition.z, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void VehicleRoadSubsystemQueuesSharedApproachConnectorsTogether()
        {
            BakedLaneNetwork network = Track(CreateSharedApproachSignalNetwork());
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            VehicleRoadTrafficControlResult straight = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "straight_car",
                laneId = "start",
                distanceAlongLane = 14f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "straight_connector", "straight_goal" }
            });

            Assert.AreEqual(VehicleRoadStopReason.TrafficSignal, straight.stopReason);
            Assert.AreEqual(0, straight.queueIndex);
            Assert.True(straight.hasStopPosition);
            Assert.That(straight.stopPosition.z, Is.EqualTo(16f).Within(0.001f));

            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "straight_car",
                laneId = "start",
                distanceAlongLane = 16f,
                speed = 0f,
                length = 4f,
                routeLaneIds = new[] { "start", "straight_connector", "straight_goal" }
            });

            VehicleRoadTrafficControlResult right = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "right_car",
                laneId = "start",
                distanceAlongLane = 8f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "right_connector", "right_goal" }
            });

            Assert.AreEqual(VehicleRoadStopReason.TrafficSignal, right.stopReason);
            Assert.AreEqual(1, right.queueIndex);
            Assert.True(right.hasStopPosition);
            Assert.That(right.stopPosition.z, Is.EqualTo(11.5f).Within(0.001f));
            Assert.AreEqual(2, subsystem.GetSnapshot().queuedVehicleCount);
        }

        [Test]
        public void VehicleRoadSubsystemSupportsNegativeStopLineDistance()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(false));
            network.JunctionTraffic[0].defaultStopLineDistance = -2f;
            network.ConnectorTraffic[0].stopLineDistance = -2f;
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            VehicleRoadTrafficControlResult result = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "negative_stop_line_car",
                laneId = "start",
                distanceAlongLane = 18f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });

            Assert.AreEqual(VehicleRoadStopReason.TrafficSignal, result.stopReason);
            Assert.AreEqual(VehicleRoadPassageStatus.Waiting, result.passageStatus);
            Assert.AreEqual(2f, result.distanceToStopLine);
            Assert.True(result.hasStopPosition);
            Assert.That(result.stopPosition.z, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void VehicleRoadSubsystemKeepsNegativeStopLineConstraintOnConnectorLane()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(false));
            network.JunctionTraffic[0].defaultStopLineDistance = -2f;
            network.ConnectorTraffic[0].stopLineDistance = -2f;
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            VehicleRoadTrafficControlResult result = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "connector_stop_line_car",
                laneId = "connector",
                distanceAlongLane = 0.5f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });

            Assert.AreEqual(VehicleRoadStopReason.TrafficSignal, result.stopReason);
            Assert.AreEqual(VehicleRoadPassageStatus.Waiting, result.passageStatus);
            Assert.AreEqual(VehicleRoadSignalState.Red, result.signalState);
            Assert.That(result.distanceToStopLine, Is.EqualTo(0f).Within(0.001f));
            Assert.True(result.hasStopPosition);
            Assert.That(result.stopPosition.z, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void RuntimeBakeRefreshRebuildsSubsystemTrafficStopLines()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(false));
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.Networks.Add(network);
            subsystem.RebuildIndexes();

            VehicleRoadTrafficControlResult original = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "original_stop_line_car",
                laneId = "start",
                distanceAlongLane = 18f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });
            Assert.That(original.distanceToStopLine, Is.EqualTo(0f).Within(0.001f));

            BakedConnectorTrafficRecord connector = network.ConnectorTraffic[0];
            network.SetData(
                network.ScenePath,
                network.SampleSpacing,
                network.Summary,
                network.Lanes.ToList(),
                network.Samples.ToList(),
                network.Connections.ToList(),
                network.AdjacentLinks.ToList(),
                network.JunctionTraffic.ToList(),
                new List<BakedConnectorTrafficRecord>
                {
                    new BakedConnectorTrafficRecord
                    {
                        junctionId = connector.junctionId,
                        connectorLaneId = connector.connectorLaneId,
                        connectionId = connector.connectionId,
                        fromLaneId = connector.fromLaneId,
                        toLaneId = connector.toLaneId,
                        turnType = connector.turnType,
                        stopLineDistance = -2f,
                        conflictConnectorLaneIds = connector.conflictConnectorLaneIds
                    }
                });

            VehicleRoadTrafficControlResult stale = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "stale_stop_line_car",
                laneId = "start",
                distanceAlongLane = 18f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });
            Assert.That(stale.distanceToStopLine, Is.EqualTo(0f).Within(0.001f));

            int refreshed = RoadLaneNetworkEditor.RefreshVehicleRoadSubsystemsAfterBake();
            Assert.That(refreshed, Is.GreaterThanOrEqualTo(1));

            VehicleRoadTrafficControlResult updated = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "updated_stop_line_car",
                laneId = "start",
                distanceAlongLane = 18f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });
            Assert.That(updated.distanceToStopLine, Is.EqualTo(2f).Within(0.001f));
            Assert.True(updated.hasStopPosition);
            Assert.That(updated.stopPosition.z, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void VehicleRoadSubsystemAppliesSignalStopLineOnlyOnConnectorApproachLane()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(false));
            List<BakedLaneConnectionRecord> connections = network.Connections.ToList();
            connections.Add(Connection("goal", "connector"));
            network.SetData(
                string.Empty,
                network.SampleSpacing,
                new BakedLaneSummary
                {
                    authoredLaneCount = network.Lanes.Count,
                    directedLaneCount = network.Lanes.Count,
                    sampleCount = network.Samples.Count,
                    connectionCount = connections.Count,
                    adjacentLinkCount = network.AdjacentLinks.Count,
                    junctionTrafficCount = network.JunctionTraffic.Count,
                    connectorTrafficCount = network.ConnectorTraffic.Count
                },
                network.Lanes.ToList(),
                network.Samples.ToList(),
                connections,
                network.AdjacentLinks.ToList(),
                network.JunctionTraffic.ToList(),
                network.ConnectorTraffic.ToList());
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            VehicleRoadTrafficControlResult result = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "exit_lane_car",
                laneId = "goal",
                distanceAlongLane = 16f,
                speed = 0f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car
            });

            Assert.AreEqual(VehicleRoadPassageStatus.NotRequired, result.passageStatus);
            Assert.AreEqual(VehicleRoadStopReason.None, result.stopReason);
            Assert.False(result.hasStopPosition);
        }

        [Test]
        public void VehicleRoadSubsystemFindsLeadVehiclesAndFollowerStops()
        {
            BakedLaneNetwork network = Track(CreateStraightNetwork());
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);
            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "lead",
                laneId = "lane",
                distanceAlongLane = 6f,
                speed = 0f,
                length = 4f,
                routeLaneIds = new[] { "lane" }
            });

            Assert.True(subsystem.TryGetLeadVehicle("ego", "lane", 1f, new[] { "lane" }, 20f, out VehicleRoadLeadVehicleResult lead));
            Assert.AreEqual("lead", lead.vehicleId);

            GameObject followerObject = Track(new GameObject("traffic follower"));
            VehicleLaneFollower follower = followerObject.AddComponent<VehicleLaneFollower>();
            follower.RoadSubsystem = subsystem;
            follower.SetRoute(new[] { "lane" });
            VehicleLaneFollowerOutput output = follower.ComputeControl(new VehicleLaneFollowerInput
            {
                vehicleId = "ego",
                position = new Vector3(0f, 0f, 1f),
                forward = Vector3.forward,
                speed = 4f,
                wheelBase = 2.5f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car
            });

            Assert.True(output.valid);
            Assert.AreEqual(VehicleRoadStopReason.LeadVehicle, output.stopReason);
            Assert.That(output.targetSpeed, Is.LessThan(0.1f));
        }

        [Test]
        public void VehicleRoadSubsystemIgnoresVehiclesOnPassedRouteLanes()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(true));
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);
            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "passed_vehicle",
                laneId = "start",
                distanceAlongLane = 6f,
                speed = 0f,
                length = 4f,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });

            VehicleRoadTrafficControlResult result = subsystem.EvaluateTrafficControl(new VehicleRoadTrafficQuery
            {
                vehicleId = "ego",
                laneId = "connector",
                distanceAlongLane = 1f,
                speed = 4f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car,
                routeLaneIds = new[] { "start", "connector", "goal" }
            });

            Assert.AreEqual(VehicleRoadStopReason.None, result.stopReason);
            Assert.AreEqual(VehicleRoadPassageStatus.Granted, result.passageStatus);
            Assert.False(result.hasStopPosition);
        }

        [Test]
        public void VehicleRoadSubsystemLaneChangeRequiresSafeTargetGap()
        {
            BakedLaneNetwork network = Track(CreateAdjacentRuntimeNetwork());
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);
            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "ego",
                laneId = "left",
                distanceAlongLane = 10f,
                speed = 5f,
                length = 4f,
                agentMask = RoadAgentMask.Car
            });

            VehicleRoadLaneChangeRequestResult granted = subsystem.RequestLaneChange("ego", RoadLaneAdjacentSide.Right);
            Assert.AreEqual(VehicleRoadLaneChangeStatus.Granted, granted.status);
            Assert.AreEqual("right", granted.targetLaneId);
            Assert.AreEqual(1, subsystem.GetSnapshot().laneChangeReservationCount);

            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "blocked",
                laneId = "left",
                distanceAlongLane = 30f,
                speed = 5f,
                length = 4f,
                agentMask = RoadAgentMask.Car
            });
            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = "target_car",
                laneId = "right",
                distanceAlongLane = 30f,
                speed = 5f,
                length = 4f,
                agentMask = RoadAgentMask.Car
            });

            VehicleRoadLaneChangeRequestResult denied = subsystem.RequestLaneChange("blocked", RoadLaneAdjacentSide.Right);
            Assert.AreEqual(VehicleRoadLaneChangeStatus.Denied, denied.status);
        }

        [Test]
        public void VehicleFollowerOutputsTrafficSignalStopState()
        {
            BakedLaneNetwork network = Track(CreateSignalRouteNetwork(false));
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);
            GameObject followerObject = Track(new GameObject("signal follower"));
            VehicleLaneFollower follower = followerObject.AddComponent<VehicleLaneFollower>();
            follower.RoadSubsystem = subsystem;
            follower.SetRoute(new[] { "start", "connector", "goal" });

            VehicleLaneFollowerOutput output = follower.ComputeControl(new VehicleLaneFollowerInput
            {
                vehicleId = "signal_car",
                position = new Vector3(0f, 0f, 18f),
                forward = Vector3.forward,
                speed = 4f,
                wheelBase = 2.5f,
                vehicleLength = 4f,
                agentMask = RoadAgentMask.Car
            });

            Assert.True(output.valid);
            Assert.AreEqual(VehicleRoadStopReason.TrafficSignal, output.stopReason);
            Assert.AreEqual(VehicleRoadPassageStatus.Waiting, output.passageStatus);
            Assert.AreEqual(VehicleRoadSignalState.Red, output.signalState);
            Assert.True(output.hasStopPoint);
            Assert.That(output.targetSpeed, Is.LessThan(0.1f));
        }

        [Test]
        public void VehicleRoadSubsystemExposesAdjacentAndLaneChangeLinks()
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            authoringNetwork.AdjacentMinimumOverlapLength = 2f;
            CreateLane("left", new[] { Vector3.zero, Vector3.forward * 20f }, authoringNetwork.transform);
            CreateLane("right", new[] { Vector3.right * 3f, Vector3.right * 3f + Vector3.forward * 20f }, authoringNetwork.transform);
            BakedLaneNetwork network = Track(authoringNetwork.BakeNetwork());
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);

            IReadOnlyList<BakedLaneAdjacentLinkRecord> links = subsystem.GetAdjacentLinks("left");
            Assert.AreEqual(1, links.Count);
            Assert.AreEqual("right", links[0].toLaneId);
            Assert.AreEqual(1, subsystem.GetLaneChangeLinks("left", RoadAgentMask.Car).Count);

            subsystem.SetLaneClosed("right", true);
            Assert.AreEqual(0, subsystem.GetLaneChangeLinks("left", RoadAgentMask.Car).Count);
        }

        [Test]
        public void VehicleFollowerCanUseVehicleRoadSubsystemWithoutDirectLaneNetwork()
        {
            BakedLaneNetwork network = Track(CreateStraightNetwork());
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);
            GameObject followerObject = Track(new GameObject("subsystem follower"));
            VehicleLaneFollower follower = followerObject.AddComponent<VehicleLaneFollower>();
            follower.RoadSubsystem = subsystem;
            follower.SetRoute(new[] { "lane" });

            VehicleLaneFollowerOutput output = follower.ComputeControl(new VehicleLaneFollowerInput
            {
                position = new Vector3(0.1f, 0f, 1f),
                forward = Vector3.forward,
                speed = 4f,
                wheelBase = 2.5f,
                agentMask = RoadAgentMask.Car
            });

            Assert.True(output.valid);
            Assert.AreEqual("lane", output.currentLaneId);
            Assert.That(output.targetSpeed, Is.GreaterThan(0f));
        }

        [Test]
        public void VehicleRoadTestVehicleLoopResetClearsTrafficStateAndRestoresPose()
        {
            BakedLaneNetwork network = Track(CreateStraightNetwork());
            VehicleRoadSubsystem subsystem = CreateSubsystem();
            subsystem.RegisterNetwork(network);
            GameObject vehicleObject = Track(new GameObject("loop vehicle"));
            VehicleLaneFollower follower = vehicleObject.AddComponent<VehicleLaneFollower>();
            follower.RoadSubsystem = subsystem;
            VehicleRoadTestVehicle vehicle = vehicleObject.AddComponent<VehicleRoadTestVehicle>();
            vehicle.VehicleId = "loop_car";
            vehicle.RoadSubsystem = subsystem;
            vehicle.LoopRoute = true;

            Vector3 startPosition = new Vector3(1f, 2f, 3f);
            Quaternion startRotation = Quaternion.Euler(0f, 35f, 0f);
            SetPrivateField(vehicle, "loopStartPosition", startPosition);
            SetPrivateField(vehicle, "loopStartRotation", startRotation);
            SetPrivateField(vehicle, "loopStartCaptured", true);
            subsystem.UpdateVehicle(new VehicleRoadVehicleUpdate
            {
                vehicleId = vehicle.VehicleId,
                laneId = "lane",
                distanceAlongLane = 5f,
                speed = 2f,
                length = 4f,
                agentMask = RoadAgentMask.Car
            });
            vehicleObject.transform.SetPositionAndRotation(Vector3.one * 20f, Quaternion.identity);

            InvokePrivateMethod(vehicle, "ResetLoop");

            AssertVector3(vehicleObject.transform.position, startPosition);
            Assert.That(Quaternion.Angle(vehicleObject.transform.rotation, startRotation), Is.LessThan(0.001f));
            Assert.AreEqual(0, subsystem.GetSnapshot().registeredVehicleCount);
        }

        [Test]
        public void VehicleRoadTestVehicleCanFollowBakedConnectorCurveExactly()
        {
            BakedLaneNetwork network = Track(CreateCurvedConnectorNetwork());
            GameObject vehicleObject = Track(new GameObject("curve vehicle"));
            VehicleLaneFollower follower = vehicleObject.AddComponent<VehicleLaneFollower>();
            follower.LaneNetwork = network;
            follower.SetRoute(new[] { "start", "curve", "goal" });
            VehicleRoadTestVehicle vehicle = vehicleObject.AddComponent<VehicleRoadTestVehicle>();
            vehicle.FollowBakedLanePose = true;
            SetPrivateField(vehicle, "follower", follower);
            VehicleLaneFollowerOutput output = new VehicleLaneFollowerOutput
            {
                valid = true,
                currentLaneId = "start",
                distanceAlongLane = 9f
            };

            MethodInfo method = typeof(VehicleRoadTestVehicle).GetMethod(
                "TryMoveAlongBakedRoute",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            bool moved = (bool)method.Invoke(vehicle, new object[] { output, 3f });

            Assert.True(moved);
            Assert.True(follower.TryEvaluateRoutePose("start", 12f, out string laneId, out RoadLanePose expected));
            Assert.AreEqual("curve", laneId);
            AssertVector3(vehicleObject.transform.position, expected.position);
            Assert.That(Vector3.Angle(vehicleObject.transform.forward, expected.forward), Is.LessThan(0.001f));
        }

        [Test]
        public void RoadLaneAlignmentFlattensHeightsAndVerticalTangents()
        {
            RoadLane lane = CreateLane(
                "uneven",
                new[]
                {
                    new Vector3(0f, 1f, 0f),
                    new Vector3(0f, 3f, 5f),
                    new Vector3(0f, 2f, 10f)
                });
            Spline spline = lane.Spline;
            spline.SetTangentMode(0, TangentMode.Broken);
            BezierKnot knot = spline[0];
            knot.TangentOut = new float3(0f, 2f, 4f);
            spline.SetKnot(0, knot);

            int changed = RoadLaneAlignmentUtility.FlattenKnotHeights(
                lane,
                RoadLaneKnotHeightReference.FirstKnot,
                0f,
                true);

            Assert.AreEqual(3, changed);
            for (int i = 0; i < spline.Count; i++)
            {
                Vector3 world = lane.SplineContainer.transform.TransformPoint(spline[i].Position);
                Assert.That(world.y, Is.EqualTo(1f).Within(0.0001f));
            }

            Vector3 worldTangentOut = lane.SplineContainer.transform.TransformVector(ToVector3(spline[0].TangentOut));
            Assert.That(worldTangentOut.y, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void RoadLaneAlignmentSnapsKnotXzToGridWithoutChangingHeight()
        {
            RoadLane lane = CreateLane(
                "grid",
                new[]
                {
                    new Vector3(0.24f, 1.25f, 1.76f),
                    new Vector3(1.74f, 2.5f, 3.26f)
                });

            int changed = RoadLaneAlignmentUtility.SnapKnotPositionsToGrid(
                lane,
                0.5f,
                true,
                false,
                true);

            Assert.AreEqual(2, changed);
            Vector3 first = lane.SplineContainer.transform.TransformPoint(lane.Spline[0].Position);
            Vector3 second = lane.SplineContainer.transform.TransformPoint(lane.Spline[1].Position);
            AssertVector3(first, new Vector3(0f, 1.25f, 2f));
            AssertVector3(second, new Vector3(1.5f, 2.5f, 3.5f));
        }

        [Test]
        public void VehicleRoadScriptsDoNotReferenceNavigationRuntimeApis()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            List<string> files = Directory.GetFiles(
                    Path.Combine(projectRoot, "Assets/BlueprintSystem/VehicleRoads/Scripts"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !path.Replace('\\', '/').Contains("/Editor/Tests/", StringComparison.Ordinal))
                .ToList();
            foreach (string file in files)
            {
                string source = File.ReadAllText(file);
                Assert.False(source.Contains("using UnityEngine.AI", StringComparison.Ordinal), file);
                Assert.False(source.Contains("using Unity.AI.Navigation", StringComparison.Ordinal), file);
                Assert.False(source.Contains("NavMesh.SamplePosition", StringComparison.Ordinal), file);
            }
        }

        private RoadLaneNetwork CreateNetwork()
        {
            GameObject root = Track(new GameObject("Vehicle Road Network"));
            RoadLaneNetwork authoringNetwork = root.AddComponent<RoadLaneNetwork>();
            authoringNetwork.SampleSpacing = 1f;
            authoringNetwork.ConnectionRadius = 0.1f;
            return authoringNetwork;
        }

        private VehicleRoadSubsystem CreateSubsystem()
        {
            GameObject gameObject = Track(new GameObject("Vehicle Road Subsystem"));
            VehicleRoadSubsystem subsystem = gameObject.AddComponent<VehicleRoadSubsystem>();
            subsystem.AutoRegisterSceneRoadLaneNetworks = false;
            subsystem.ClearNetworks();
            return subsystem;
        }

        private RoadLane CreateLane(
            string id,
            IReadOnlyList<Vector3> points,
            Transform parent = null)
        {
            GameObject gameObject = Track(new GameObject(id));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent);
            }

            SplineContainer container = gameObject.AddComponent<SplineContainer>();
            Spline spline = new Spline(points.Count, false);
            for (int i = 0; i < points.Count; i++)
            {
                spline.Add(container.transform.InverseTransformPoint(points[i]), TangentMode.Linear);
            }

            container.Spline = spline;
            RoadLane lane = gameObject.AddComponent<RoadLane>();
            lane.LaneId = id;
            return lane;
        }

        private RoadLane CreateConfiguredConnector(
            string id,
            string sourceLaneId,
            string targetLaneId,
            string junctionId,
            RoadLaneTurn turn,
            IReadOnlyList<Vector3> points,
            Transform parent,
            float width)
        {
            RoadLane connector = CreateLane(id, points, parent);
            connector.ConfigureConnector(
                id + "_key",
                id,
                sourceLaneId,
                targetLaneId,
                turn,
                8f,
                1f,
                junctionId);
            connector.Width = width;
            return connector;
        }

        private static BakedConnectorTrafficRecord GetConnectorTraffic(BakedLaneNetwork network, string connectorLaneId)
        {
            Assert.True(network.TryGetConnectorTraffic(connectorLaneId, out BakedConnectorTrafficRecord connectorTraffic));
            return connectorTraffic;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, methodName);
            method.Invoke(target, null);
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }

        private static BakedLaneNetwork CreateRouteNetwork()
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord>
            {
                Lane("start", 0, 2, 1f),
                Lane("short", 2, 2, 1f),
                Lane("long", 4, 2, 10f),
                Lane("goal", 6, 2, 1f)
            };
            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>();
            AddStraightSamples(samples, "start", Vector3.zero, Vector3.forward, 1f);
            AddStraightSamples(samples, "short", Vector3.forward, Vector3.forward, 1f);
            AddStraightSamples(samples, "long", Vector3.right, Vector3.forward, 10f);
            AddStraightSamples(samples, "goal", Vector3.forward * 2f, Vector3.forward, 1f);
            List<BakedLaneConnectionRecord> connections = new List<BakedLaneConnectionRecord>
            {
                Connection("start", "short"),
                Connection("short", "goal"),
                Connection("start", "long"),
                Connection("long", "goal")
            };
            return Network(lanes, samples, connections);
        }

        private static BakedLaneNetwork CreateSignalRouteNetwork(bool straightInitiallyGreen)
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord>
            {
                Lane("start", 0, 3, 20f),
                Lane("connector", 3, 2, 2f),
                Lane("goal", 5, 2, 20f),
                Lane("bypass", 7, 2, 10f)
            };
            lanes[1].kind = RoadLaneKind.Connector;
            lanes[1].turnType = RoadLaneTurn.Straight;
            lanes[1].connectorSourceLaneId = "start";
            lanes[1].connectorTargetLaneId = "goal";
            lanes[1].connectorJunctionId = "junction";

            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>
            {
                Sample("start", 0, new Vector3(0f, 0f, 0f), 0f),
                Sample("start", 1, new Vector3(0f, 0f, 10f), 10f),
                Sample("start", 2, new Vector3(0f, 0f, 20f), 20f),
                Sample("connector", 0, new Vector3(0f, 0f, 20f), 0f),
                Sample("connector", 1, new Vector3(0f, 0f, 22f), 2f),
                Sample("goal", 0, new Vector3(0f, 0f, 22f), 0f),
                Sample("goal", 1, new Vector3(0f, 0f, 42f), 20f),
                Sample("bypass", 0, new Vector3(4f, 0f, 20f), 0f),
                Sample("bypass", 1, new Vector3(4f, 0f, 30f), 10f)
            };
            List<BakedLaneConnectionRecord> connections = new List<BakedLaneConnectionRecord>
            {
                Connection("start", "connector"),
                Connection("connector", "goal"),
                Connection("start", "bypass"),
                Connection("bypass", "goal")
            };
            List<BakedJunctionTrafficRecord> junctions = new List<BakedJunctionTrafficRecord>
            {
                new BakedJunctionTrafficRecord
                {
                    junctionId = "junction",
                    controlMode = RoadJunctionTrafficControlMode.FixedSignal,
                    defaultStopLineDistance = 2f,
                    approachDetectionDistance = 8f,
                    passageTokenDuration = 5f,
                    releaseDistance = 2f,
                    signalPhases = straightInitiallyGreen
                        ? new List<BakedJunctionSignalPhaseRecord>
                        {
                            new BakedJunctionSignalPhaseRecord
                            {
                                phaseId = "straight",
                                allowedTurns = RoadLaneTurnMask.Straight,
                                greenDuration = 10f,
                                yellowDuration = 0f,
                                allRedDuration = 0f
                            }
                        }
                        : new List<BakedJunctionSignalPhaseRecord>
                        {
                            new BakedJunctionSignalPhaseRecord
                            {
                                phaseId = "left",
                                allowedTurns = RoadLaneTurnMask.Left,
                                greenDuration = 1f,
                                yellowDuration = 0f,
                                allRedDuration = 0f
                            },
                            new BakedJunctionSignalPhaseRecord
                            {
                                phaseId = "straight",
                                allowedTurns = RoadLaneTurnMask.Straight,
                                greenDuration = 10f,
                                yellowDuration = 0f,
                                allRedDuration = 0f
                            }
                        }
                }
            };
            List<BakedConnectorTrafficRecord> connectors = new List<BakedConnectorTrafficRecord>
            {
                new BakedConnectorTrafficRecord
                {
                    junctionId = "junction",
                    connectorLaneId = "connector",
                    connectionId = "start_to_connector",
                    fromLaneId = "start",
                    toLaneId = "goal",
                    turnType = RoadLaneTurn.Straight,
                    stopLineDistance = 2f
                }
            };
            return Network(
                lanes,
                samples,
                connections,
                new List<BakedLaneAdjacentLinkRecord>(),
                junctions,
                connectors);
        }

        private static BakedLaneNetwork CreateSharedApproachSignalNetwork()
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord>
            {
                Lane("start", 0, 3, 20f),
                Lane("straight_connector", 3, 2, 2f),
                Lane("straight_goal", 5, 2, 20f),
                Lane("right_connector", 7, 2, 2f),
                Lane("right_goal", 9, 2, 20f)
            };
            lanes[1].kind = RoadLaneKind.Connector;
            lanes[1].turnType = RoadLaneTurn.Straight;
            lanes[1].connectorSourceLaneId = "start";
            lanes[1].connectorTargetLaneId = "straight_goal";
            lanes[1].connectorJunctionId = "junction";
            lanes[3].kind = RoadLaneKind.Connector;
            lanes[3].turnType = RoadLaneTurn.Right;
            lanes[3].connectorSourceLaneId = "start";
            lanes[3].connectorTargetLaneId = "right_goal";
            lanes[3].connectorJunctionId = "junction";

            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>
            {
                Sample("start", 0, new Vector3(0f, 0f, 0f), 0f),
                Sample("start", 1, new Vector3(0f, 0f, 10f), 10f),
                Sample("start", 2, new Vector3(0f, 0f, 20f), 20f),
                Sample("straight_connector", 0, new Vector3(0f, 0f, 20f), 0f),
                Sample("straight_connector", 1, new Vector3(0f, 0f, 22f), 2f),
                Sample("straight_goal", 0, new Vector3(0f, 0f, 22f), 0f),
                Sample("straight_goal", 1, new Vector3(0f, 0f, 42f), 20f),
                Sample("right_connector", 0, new Vector3(0f, 0f, 20f), 0f, Vector3.right),
                Sample("right_connector", 1, new Vector3(2f, 0f, 20f), 2f, Vector3.right),
                Sample("right_goal", 0, new Vector3(2f, 0f, 20f), 0f, Vector3.right),
                Sample("right_goal", 1, new Vector3(22f, 0f, 20f), 20f, Vector3.right)
            };
            List<BakedLaneConnectionRecord> connections = new List<BakedLaneConnectionRecord>
            {
                Connection("start", "straight_connector"),
                Connection("straight_connector", "straight_goal"),
                Connection("start", "right_connector"),
                Connection("right_connector", "right_goal")
            };
            List<BakedJunctionTrafficRecord> junctions = new List<BakedJunctionTrafficRecord>
            {
                new BakedJunctionTrafficRecord
                {
                    junctionId = "junction",
                    controlMode = RoadJunctionTrafficControlMode.FixedSignal,
                    defaultStopLineDistance = 2f,
                    queueSpacing = 0.5f,
                    approachDetectionDistance = 8f,
                    passageTokenDuration = 5f,
                    releaseDistance = 2f,
                    signalPhases = new List<BakedJunctionSignalPhaseRecord>
                    {
                        new BakedJunctionSignalPhaseRecord
                        {
                            phaseId = "left",
                            allowedTurns = RoadLaneTurnMask.Left,
                            greenDuration = 10f,
                            yellowDuration = 0f,
                            allRedDuration = 0f
                        }
                    }
                }
            };
            List<BakedConnectorTrafficRecord> connectors = new List<BakedConnectorTrafficRecord>
            {
                new BakedConnectorTrafficRecord
                {
                    junctionId = "junction",
                    connectorLaneId = "straight_connector",
                    connectionId = "start_to_straight_connector",
                    fromLaneId = "start",
                    toLaneId = "straight_goal",
                    turnType = RoadLaneTurn.Straight,
                    stopLineDistance = 2f,
                    conflictConnectorLaneIds = "right_connector"
                },
                new BakedConnectorTrafficRecord
                {
                    junctionId = "junction",
                    connectorLaneId = "right_connector",
                    connectionId = "start_to_right_connector",
                    fromLaneId = "start",
                    toLaneId = "right_goal",
                    turnType = RoadLaneTurn.Right,
                    stopLineDistance = 2f,
                    conflictConnectorLaneIds = "straight_connector"
                }
            };
            return Network(
                lanes,
                samples,
                connections,
                new List<BakedLaneAdjacentLinkRecord>(),
                junctions,
                connectors);
        }

        private static BakedLaneNetwork CreateSameSourceConflictRuntimeNetwork(bool legacyOnly)
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord>
            {
                Lane("start", 0, 2, 10f),
                Lane("straight_connector", 2, 2, 10f),
                Lane("straight_goal", 4, 2, 10f),
                Lane("right_connector", 6, 2, 10f),
                Lane("right_goal", 8, 2, 10f)
            };
            lanes[1].kind = RoadLaneKind.Connector;
            lanes[1].turnType = RoadLaneTurn.Straight;
            lanes[1].connectorSourceLaneId = "start";
            lanes[1].connectorTargetLaneId = "straight_goal";
            lanes[1].connectorJunctionId = "junction";
            lanes[3].kind = RoadLaneKind.Connector;
            lanes[3].turnType = RoadLaneTurn.Right;
            lanes[3].connectorSourceLaneId = "start";
            lanes[3].connectorTargetLaneId = "right_goal";
            lanes[3].connectorJunctionId = "junction";

            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>();
            AddStraightSamples(samples, "start", Vector3.zero, Vector3.forward, 10f);
            AddStraightSamples(samples, "straight_connector", Vector3.forward * 10f, Vector3.right, 10f);
            AddStraightSamples(samples, "straight_goal", Vector3.forward * 10f + Vector3.right * 10f, Vector3.right, 10f);
            AddStraightSamples(samples, "right_connector", Vector3.forward * 10f, Vector3.forward, 10f);
            AddStraightSamples(samples, "right_goal", Vector3.forward * 20f, Vector3.forward, 10f);
            List<BakedLaneConnectionRecord> connections = new List<BakedLaneConnectionRecord>
            {
                Connection("start", "straight_connector"),
                Connection("straight_connector", "straight_goal"),
                Connection("start", "right_connector"),
                Connection("right_connector", "right_goal")
            };
            List<BakedConnectorConflictRecord> straightConflicts = legacyOnly
                ? new List<BakedConnectorConflictRecord>()
                : new List<BakedConnectorConflictRecord>
                {
                    new BakedConnectorConflictRecord
                    {
                        otherConnectorLaneId = "right_connector",
                        selfStartDistance = 0f,
                        selfEndDistance = 2f,
                        otherStartDistance = 0f,
                        otherEndDistance = 2f,
                        reason = BakedConnectorConflictReason.SameSource
                    }
                };
            List<BakedConnectorConflictRecord> rightConflicts = legacyOnly
                ? new List<BakedConnectorConflictRecord>()
                : new List<BakedConnectorConflictRecord>
                {
                    new BakedConnectorConflictRecord
                    {
                        otherConnectorLaneId = "straight_connector",
                        selfStartDistance = 0f,
                        selfEndDistance = 2f,
                        otherStartDistance = 0f,
                        otherEndDistance = 2f,
                        reason = BakedConnectorConflictReason.SameSource
                    }
                };
            return Network(
                lanes,
                samples,
                connections,
                new List<BakedLaneAdjacentLinkRecord>(),
                CreateUncontrolledJunctions(),
                new List<BakedConnectorTrafficRecord>
                {
                    new BakedConnectorTrafficRecord
                    {
                        junctionId = "junction",
                        connectorLaneId = "straight_connector",
                        connectionId = "start_to_straight_connector",
                        fromLaneId = "start",
                        toLaneId = "straight_goal",
                        turnType = RoadLaneTurn.Straight,
                        stopLineDistance = 0f,
                        conflictConnectorLaneIds = "right_connector",
                        conflicts = straightConflicts
                    },
                    new BakedConnectorTrafficRecord
                    {
                        junctionId = "junction",
                        connectorLaneId = "right_connector",
                        connectionId = "start_to_right_connector",
                        fromLaneId = "start",
                        toLaneId = "right_goal",
                        turnType = RoadLaneTurn.Right,
                        stopLineDistance = 0f,
                        conflictConnectorLaneIds = "straight_connector",
                        conflicts = rightConflicts
                    }
                });
        }

        private static BakedLaneNetwork CreateCrossingConflictRuntimeNetwork(bool legacyOnly)
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord>
            {
                Lane("west_start", 0, 2, 10f),
                Lane("east_connector", 2, 2, 10f),
                Lane("east_goal", 4, 2, 10f),
                Lane("south_start", 6, 2, 10f),
                Lane("north_connector", 8, 2, 10f),
                Lane("north_goal", 10, 2, 10f)
            };
            lanes[1].kind = RoadLaneKind.Connector;
            lanes[1].turnType = RoadLaneTurn.Straight;
            lanes[1].connectorSourceLaneId = "west_start";
            lanes[1].connectorTargetLaneId = "east_goal";
            lanes[1].connectorJunctionId = "junction";
            lanes[4].kind = RoadLaneKind.Connector;
            lanes[4].turnType = RoadLaneTurn.Straight;
            lanes[4].connectorSourceLaneId = "south_start";
            lanes[4].connectorTargetLaneId = "north_goal";
            lanes[4].connectorJunctionId = "junction";

            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>();
            AddStraightSamples(samples, "west_start", new Vector3(-15f, 0f, 0f), Vector3.right, 10f);
            AddStraightSamples(samples, "east_connector", new Vector3(-5f, 0f, 0f), Vector3.right, 10f);
            AddStraightSamples(samples, "east_goal", new Vector3(5f, 0f, 0f), Vector3.right, 10f);
            AddStraightSamples(samples, "south_start", new Vector3(0f, 0f, -15f), Vector3.forward, 10f);
            AddStraightSamples(samples, "north_connector", new Vector3(0f, 0f, -5f), Vector3.forward, 10f);
            AddStraightSamples(samples, "north_goal", new Vector3(0f, 0f, 5f), Vector3.forward, 10f);
            List<BakedLaneConnectionRecord> connections = new List<BakedLaneConnectionRecord>
            {
                Connection("west_start", "east_connector"),
                Connection("east_connector", "east_goal"),
                Connection("south_start", "north_connector"),
                Connection("north_connector", "north_goal")
            };
            List<BakedConnectorConflictRecord> eastConflicts = legacyOnly
                ? new List<BakedConnectorConflictRecord>()
                : new List<BakedConnectorConflictRecord>
                {
                    new BakedConnectorConflictRecord
                    {
                        otherConnectorLaneId = "north_connector",
                        selfStartDistance = 4f,
                        selfEndDistance = 6f,
                        otherStartDistance = 4f,
                        otherEndDistance = 6f,
                        reason = BakedConnectorConflictReason.Crossing
                    }
                };
            List<BakedConnectorConflictRecord> northConflicts = legacyOnly
                ? new List<BakedConnectorConflictRecord>()
                : new List<BakedConnectorConflictRecord>
                {
                    new BakedConnectorConflictRecord
                    {
                        otherConnectorLaneId = "east_connector",
                        selfStartDistance = 4f,
                        selfEndDistance = 6f,
                        otherStartDistance = 4f,
                        otherEndDistance = 6f,
                        reason = BakedConnectorConflictReason.Crossing
                    }
                };
            return Network(
                lanes,
                samples,
                connections,
                new List<BakedLaneAdjacentLinkRecord>(),
                CreateUncontrolledJunctions(),
                new List<BakedConnectorTrafficRecord>
                {
                    new BakedConnectorTrafficRecord
                    {
                        junctionId = "junction",
                        connectorLaneId = "east_connector",
                        connectionId = "west_start_to_east_connector",
                        fromLaneId = "west_start",
                        toLaneId = "east_goal",
                        turnType = RoadLaneTurn.Straight,
                        stopLineDistance = 0f,
                        conflictConnectorLaneIds = "north_connector",
                        conflicts = eastConflicts
                    },
                    new BakedConnectorTrafficRecord
                    {
                        junctionId = "junction",
                        connectorLaneId = "north_connector",
                        connectionId = "south_start_to_north_connector",
                        fromLaneId = "south_start",
                        toLaneId = "north_goal",
                        turnType = RoadLaneTurn.Straight,
                        stopLineDistance = 0f,
                        conflictConnectorLaneIds = "east_connector",
                        conflicts = northConflicts
                    }
                });
        }

        private static List<BakedJunctionTrafficRecord> CreateUncontrolledJunctions()
        {
            return new List<BakedJunctionTrafficRecord>
            {
                new BakedJunctionTrafficRecord
                {
                    junctionId = "junction",
                    controlMode = RoadJunctionTrafficControlMode.Uncontrolled,
                    defaultStopLineDistance = 0f,
                    queueSpacing = 0.5f,
                    approachDetectionDistance = 8f,
                    passageTokenDuration = 5f,
                    releaseDistance = 2f,
                    connectorConflictSafetyMargin = 0.5f
                }
            };
        }

        private static BakedLaneNetwork CreateAdjacentRuntimeNetwork()
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord>
            {
                Lane("left", 0, 2, 50f),
                Lane("right", 2, 2, 50f)
            };
            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>();
            AddStraightSamples(samples, "left", Vector3.zero, Vector3.forward, 50f);
            AddStraightSamples(samples, "right", Vector3.right * 3f, Vector3.forward, 50f);
            List<BakedLaneAdjacentLinkRecord> adjacent = new List<BakedLaneAdjacentLinkRecord>
            {
                new BakedLaneAdjacentLinkRecord
                {
                    linkId = "left_to_right",
                    fromLaneId = "left",
                    toLaneId = "right",
                    side = RoadLaneAdjacentSide.Right,
                    flags = RoadLaneAdjacentFlags.LaneChangeAllowed,
                    open = true,
                    overlapStartDistance = 0f,
                    overlapEndDistance = 50f,
                    minLateralDistance = 3f,
                    maxLateralDistance = 3f
                }
            };
            return Network(
                lanes,
                samples,
                new List<BakedLaneConnectionRecord>(),
                adjacent,
                new List<BakedJunctionTrafficRecord>(),
                new List<BakedConnectorTrafficRecord>());
        }

        private static BakedLaneNetwork CreateCurvedConnectorNetwork()
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord>
            {
                Lane("start", 0, 2, 10f),
                Lane("curve", 2, 4, 6f),
                Lane("goal", 6, 2, 10f)
            };
            lanes[1].kind = RoadLaneKind.Connector;
            lanes[1].turnType = RoadLaneTurn.Right;
            lanes[1].connectorSourceLaneId = "start";
            lanes[1].connectorTargetLaneId = "goal";
            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>
            {
                Sample("start", 0, Vector3.zero, 0f, Vector3.forward),
                Sample("start", 1, Vector3.forward * 10f, 10f, Vector3.forward),
                Sample("curve", 0, new Vector3(0f, 0f, 10f), 0f, Vector3.forward),
                Sample("curve", 1, new Vector3(0.75f, 0f, 11.85f), 2f, new Vector3(0.7f, 0f, 0.7f)),
                Sample("curve", 2, new Vector3(2.15f, 0f, 13.25f), 4f, new Vector3(0.9f, 0f, 0.35f)),
                Sample("curve", 3, new Vector3(4f, 0f, 14f), 6f, Vector3.right),
                Sample("goal", 0, new Vector3(4f, 0f, 14f), 0f, Vector3.right),
                Sample("goal", 1, new Vector3(14f, 0f, 14f), 10f, Vector3.right)
            };
            List<BakedLaneConnectionRecord> connections = new List<BakedLaneConnectionRecord>
            {
                Connection("start", "curve"),
                Connection("curve", "goal")
            };
            return Network(lanes, samples, connections);
        }

        private static BakedLaneNetwork CreateOverpassNetwork()
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord>
            {
                Lane("lower", 0, 2, 10f),
                Lane("upper", 2, 2, 10f)
            };
            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>();
            AddStraightSamples(samples, "lower", Vector3.zero, Vector3.right, 10f);
            AddStraightSamples(samples, "upper", Vector3.up * 10f, Vector3.right, 10f);
            return Network(lanes, samples, new List<BakedLaneConnectionRecord>());
        }

        private static BakedLaneNetwork CreateStraightNetwork()
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord> { Lane("lane", 0, 3, 20f) };
            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>
            {
                Sample("lane", 0, new Vector3(0f, 0f, 0f), 0f),
                Sample("lane", 1, new Vector3(0f, 0f, 10f), 10f),
                Sample("lane", 2, new Vector3(0f, 0f, 20f), 20f)
            };
            return Network(lanes, samples, new List<BakedLaneConnectionRecord>());
        }

        private static BakedLaneNetwork CreateSingleStraightNetwork(string laneId, Vector3 start)
        {
            List<BakedLaneRecord> lanes = new List<BakedLaneRecord> { Lane(laneId, 0, 2, 10f) };
            List<BakedLaneSampleRecord> samples = new List<BakedLaneSampleRecord>();
            AddStraightSamples(samples, laneId, start, Vector3.forward, 10f);
            return Network(lanes, samples, new List<BakedLaneConnectionRecord>());
        }

        private static BakedLaneNetwork Network(
            List<BakedLaneRecord> lanes,
            List<BakedLaneSampleRecord> samples,
            List<BakedLaneConnectionRecord> connections)
        {
            return Network(
                lanes,
                samples,
                connections,
                new List<BakedLaneAdjacentLinkRecord>(),
                new List<BakedJunctionTrafficRecord>(),
                new List<BakedConnectorTrafficRecord>());
        }

        private static BakedLaneNetwork Network(
            List<BakedLaneRecord> lanes,
            List<BakedLaneSampleRecord> samples,
            List<BakedLaneConnectionRecord> connections,
            List<BakedLaneAdjacentLinkRecord> adjacentLinks,
            List<BakedJunctionTrafficRecord> junctionTraffic,
            List<BakedConnectorTrafficRecord> connectorTraffic)
        {
            BakedLaneNetwork network = ScriptableObject.CreateInstance<BakedLaneNetwork>();
            network.SetData(
                string.Empty,
                1f,
                new BakedLaneSummary
                {
                    authoredLaneCount = lanes.Count,
                    directedLaneCount = lanes.Count,
                    sampleCount = samples.Count,
                    connectionCount = connections.Count,
                    adjacentLinkCount = adjacentLinks.Count,
                    junctionTrafficCount = junctionTraffic.Count,
                    connectorTrafficCount = connectorTraffic.Count
                },
                lanes,
                samples,
                connections,
                adjacentLinks,
                junctionTraffic,
                connectorTraffic);
            return network;
        }

        private void AssertNoAdjacentLinks(string targetId, IReadOnlyList<Vector3> targetPoints)
        {
            RoadLaneNetwork authoringNetwork = CreateNetwork();
            authoringNetwork.AdjacentMinimumOverlapLength = 2f;
            CreateLane("source_" + targetId, new[] { Vector3.zero, Vector3.forward * 20f }, authoringNetwork.transform);
            CreateLane(targetId, targetPoints, authoringNetwork.transform);

            BakedLaneNetwork network = Track(authoringNetwork.BakeNetwork());

            Assert.AreEqual(0, network.AdjacentLinks.Count, targetId);
        }

        private static BakedLaneRecord Lane(string id, int firstSample, int sampleCount, float length)
        {
            return new BakedLaneRecord
            {
                laneId = id,
                sourceLaneId = id,
                open = true,
                speedLimit = 10f,
                length = length,
                firstSampleIndex = firstSample,
                sampleCount = sampleCount
            };
        }

        private static BakedLaneConnectionRecord Connection(string from, string to)
        {
            return new BakedLaneConnectionRecord
            {
                connectionId = from + "_to_" + to,
                fromLaneId = from,
                toLaneId = to,
                open = true
            };
        }

        private static void AddStraightSamples(
            List<BakedLaneSampleRecord> output,
            string laneId,
            Vector3 start,
            Vector3 direction,
            float length)
        {
            output.Add(Sample(laneId, 0, start, 0f, direction));
            output.Add(Sample(laneId, 1, start + direction.normalized * length, length, direction));
        }

        private static BakedLaneSampleRecord Sample(
            string laneId,
            int order,
            Vector3 position,
            float distance,
            Vector3? direction = null)
        {
            return new BakedLaneSampleRecord
            {
                sampleId = laneId + "_" + order,
                laneId = laneId,
                order = order,
                splinePosition = position,
                finalPosition = position,
                forward = direction ?? Vector3.forward,
                up = Vector3.up,
                distanceAlongLane = distance,
                valid = true
            };
        }

        private static void AssertAddComponentMenu<T>(string expected)
            where T : Component
        {
            AddComponentMenu attribute = typeof(T).GetCustomAttribute<AddComponentMenu>();
            Assert.NotNull(attribute, typeof(T).Name);
            Assert.AreEqual(expected, attribute.componentMenu);
            AssertNoLegacyRoadBrand(attribute.componentMenu);
        }

        private static void AssertCreateAssetMenu<T>(string expected)
            where T : ScriptableObject
        {
            CreateAssetMenuAttribute attribute = typeof(T).GetCustomAttribute<CreateAssetMenuAttribute>();
            Assert.NotNull(attribute, typeof(T).Name);
            Assert.AreEqual(expected, attribute.menuName);
            AssertNoLegacyRoadBrand(attribute.menuName);
        }

        private static List<string> GetEditorMenuPaths()
        {
            List<string> menuPaths = new List<string>();
            Type[] types = typeof(RoadNetworkProjectSettingsProvider).Assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                MethodInfo[] methods = types[i].GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int j = 0; j < methods.Length; j++)
                {
                    object[] attributes = methods[j].GetCustomAttributes(typeof(MenuItem), false);
                    for (int k = 0; k < attributes.Length; k++)
                    {
                        menuPaths.Add(((MenuItem)attributes[k]).menuItem);
                    }
                }
            }

            return menuPaths;
        }

        private static void AssertNoLegacyRoadBrand(string value)
        {
            Assert.NotNull(value);
            Assert.False(value.IndexOf("Traffic" + " Police", StringComparison.OrdinalIgnoreCase) >= 0, value);
            Assert.False(value.IndexOf("Traffic" + "Police", StringComparison.OrdinalIgnoreCase) >= 0, value);
        }
    }
}
