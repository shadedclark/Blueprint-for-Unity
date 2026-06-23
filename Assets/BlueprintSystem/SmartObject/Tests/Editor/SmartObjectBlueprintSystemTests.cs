using System;
using System.Collections.Generic;
using System.Reflection;
using BlueprintSystem.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlueprintSystem.Tests
{
    public sealed class SmartObjectBlueprintSystemTests
    {
        [Test]
        public void SmartObjectManifestsExecutorsAndVisualNodesAreAvailable()
        {
            string[] typeIds =
            {
                "SmartObject.FindBest",
                "SmartObject.FindBestActor",
                "SmartObject.Reserve",
                "SmartObject.BeginUse",
                "SmartObject.Release",
                "SmartObject.GetReservationInfo",
                "SmartObject.ReleaseByRequester"
            };

            BlueprintNodeManifestCollection manifests = LoadManifests();
            BlueprintExecutorRegistry registry = BlueprintExecutorRegistry.CreateDefault();
            for (int i = 0; i < typeIds.Length; i++)
            {
                BlueprintNodeManifest manifest;
                Assert.True(manifests.TryGet(typeIds[i], out manifest), typeIds[i]);
                IBlueprintNodeExecutor executor;
                Assert.True(registry.TryGet(manifest.Executor, out executor), manifest.Executor);

                BlueprintNodeSource sourceNode = new BlueprintNodeSource
                {
                    Id = "smart_object_" + i,
                    TypeId = typeIds[i]
                };
                BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(sourceNode, manifest);
                Assert.NotNull(visualNode, typeIds[i]);
                Assert.AreEqual(typeIds[i], visualNode.ReadTypeId());
            }
        }

        [Test]
        public void SmartObjectModuleCanBeDisabledForPublicNodeSurfaces()
        {
            using (BlueprintModuleSettings.OverrideSmartObjectEnabledForTests(false))
            {
                Assert.False(BlueprintNodeManifestAssetUtility.IsManifestPath("Packages/com.shadedclark.blueprint-system/SmartObject/Specs/Nodes/SmartObject.Reserve.node.json"));
                Assert.True(BlueprintNodeManifestAssetUtility.IsManifestPath("Packages/com.shadedclark.blueprint-system/Specs/Nodes/Game.Log.node.json"));

                BlueprintNodeManifestCollection manifests = LoadManifests();
                BlueprintNodeManifest manifest;
                Assert.False(manifests.TryGet("SmartObject.Reserve", out manifest));

                IBlueprintNodeExecutor executor;
                Assert.False(BlueprintExecutorRegistry.CreateDefault().TryGet("SmartObject.Reserve", out executor));

                BlueprintVisualNode visualNode = BlueprintVisualNodeFactory.Create("SmartObject.Reserve");
                Assert.AreEqual(typeof(BlueprintVisualNode), visualNode.GetType());
            }
        }

        [Test]
        public void SmartObjectModuleDisabledComponentDoesNotRegister()
        {
            SmartObjectRegistry.ResetForTests();
            List<GameObject> objects = new List<GameObject>();
            try
            {
                using (BlueprintModuleSettings.OverrideSmartObjectEnabledForTests(false))
                {
                    GameObject gameObject = new GameObject("disabled_module_smart_object");
                    objects.Add(gameObject);
                    SmartObjectComponent component = gameObject.AddComponent<SmartObjectComponent>();

                    SmartObjectDebugSnapshot snapshot = SmartObjectRegistry.CreateDebugSnapshot(component);
                    Assert.AreEqual(SmartObjectRegistrationState.NotRegistered, snapshot.RegistrationState);
                }
            }
            finally
            {
                DestroyObjects(objects);
                SmartObjectRegistry.ResetForTests();
            }
        }

        [Test]
        public void SmartObjectPackageManifestPathIsRecognized()
        {
            Assert.True(BlueprintNodeManifestAssetUtility.IsManifestPath("Packages/com.shadedclark.blueprint-system/SmartObject/Specs/Nodes/SmartObject.Reserve.node.json"));
            Assert.True(BlueprintNodeManifestAssetUtility.IsManifestPath("Packages/com.shadedclark.blueprint-system/Specs/Nodes/Game.Log.node.json"));
            Assert.True(BlueprintNodeManifestAssetUtility.IsManifestPath("Assets/BlueprintSystem/SmartObject/Specs/Nodes/SmartObject.Reserve.node.json"));
            Assert.False(BlueprintNodeManifestAssetUtility.IsManifestPath("Packages/com.example.other/SmartObject/Specs/Nodes/SmartObject.Reserve.node.json"));
            Assert.False(BlueprintNodeManifestAssetUtility.IsManifestPath("Packages/com.shadedclark.blueprint-system/SmartObject/Specs/Other/SmartObject.Reserve.node.json"));
        }

        [Test]
        public void SmartObjectFindBestFiltersAndScoresCandidates()
        {
            SmartObjectRegistry.ResetForTests();
            List<GameObject> objects = new List<GameObject>();
            try
            {
                SmartObjectComponent low = CreateSmartObject("desk_low", "Work", new Vector3(1f, 0f, 0f), 0f, 0f, "Office", "Staff");
                objects.Add(low.gameObject);
                SmartObjectComponent best = CreateSmartObject("desk_best", "Work", new Vector3(3f, 0f, 0f), 30f, 0f, "Office", "Staff");
                objects.Add(best.gameObject);
                SmartObjectComponent blocked = CreateSmartObject("desk_blocked", "Work", Vector3.zero, 100f, 0f, "Office", "Staff");
                blocked.Slots[0].Blocked = true;
                objects.Add(blocked.gameObject);

                RuntimeNode find = CreateRuntimeNode("find_best", "SmartObject.FindBest");
                find.Properties["requesterId"] = "npc-a";
                find.Properties["activity"] = "Work";
                find.Properties["center"] = Vector3.zero;
                find.Properties["radius"] = 10f;
                find.Properties["requiredTags"] = "Office";
                find.Properties["forbiddenTags"] = string.Empty;
                find.Properties["accessGroup"] = "Staff";
                find.Properties["needScore"] = 0f;
                find.Properties["maxDistancePenalty"] = 0f;

                SmartObjectFindBestExecutor executor = new SmartObjectFindBestExecutor();
                BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), new TestBindingResolver(), new RecordingBlueprintLogger(), null);

                Assert.True((bool)executor.Evaluate(context, find, "found"));
                Assert.AreEqual(best.ObjectId, executor.Evaluate(context, find, "objectId"));
                Assert.AreEqual(0, executor.Evaluate(context, find, "slotId"));

                find.Properties["activity"] = "Sleep";
                Assert.False((bool)executor.Evaluate(context, find, "found"));
                Assert.AreEqual("NoCandidate", executor.Evaluate(context, find, "failReason"));

                find.Properties["activity"] = "Work";
                find.Properties["radius"] = 0.5f;
                Assert.False((bool)executor.Evaluate(context, find, "found"));
                Assert.AreEqual("OutOfRange", executor.Evaluate(context, find, "failReason"));

                find.Properties["radius"] = 10f;
                find.Properties["accessGroup"] = "Guest";
                Assert.False((bool)executor.Evaluate(context, find, "found"));
                Assert.AreEqual("AccessDenied", executor.Evaluate(context, find, "failReason"));
            }
            finally
            {
                DestroyObjects(objects);
                SmartObjectRegistry.ResetForTests();
            }
        }

        [Test]
        public void SmartObjectFindBestActorExcludesBoundActorAndReturnsTargetGameObject()
        {
            SmartObjectRegistry.ResetForTests();
            List<GameObject> objects = new List<GameObject>();
            try
            {
                GameObject selfActor = new GameObject("npc_self_actor");
                objects.Add(selfActor);
                SmartObjectComponent selfSmartObject = CreateSmartObject("npc_self_smart_object", "Handshake", Vector3.zero, 100f, 0f, "Npc", string.Empty);
                selfSmartObject.transform.SetParent(selfActor.transform, false);
                objects.Add(selfSmartObject.gameObject);

                SmartObjectComponent otherSmartObject = CreateSmartObject("npc_other_smart_object", "Handshake", new Vector3(1f, 0f, 0f), 0f, 0f, "Npc", string.Empty);
                objects.Add(otherSmartObject.gameObject);

                RuntimeNode find = CreateRuntimeNode("find_best_actor", "SmartObject.FindBestActor");
                find.Properties["requesterId"] = "npc-a";
                find.Properties["activity"] = "Handshake";
                find.Properties["center"] = Vector3.zero;
                find.Properties["radius"] = 10f;
                find.Properties["requiredTags"] = "Npc";
                find.Properties["forbiddenTags"] = string.Empty;
                find.Properties["accessGroup"] = string.Empty;
                find.Properties["needScore"] = 0f;
                find.Properties["maxDistancePenalty"] = 0f;
                find.Properties["excludeGameObject"] = "SelfActor";

                TestBindingResolver resolver = new TestBindingResolver();
                resolver.Bind("SelfActor", selfActor);
                SmartObjectFindBestActorExecutor executor = new SmartObjectFindBestActorExecutor();
                BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), resolver, new RecordingBlueprintLogger(), null);

                Assert.True((bool)executor.Evaluate(context, find, "found"));
                Assert.AreEqual(otherSmartObject.ObjectId, executor.Evaluate(context, find, "objectId"));
                Assert.AreEqual(0, executor.Evaluate(context, find, "slotId"));
                Assert.AreSame(otherSmartObject.gameObject, executor.Evaluate(context, find, "targetGameObject"));

                find.Properties["excludeGameObject"] = "MissingActor";
                Assert.True((bool)executor.Evaluate(context, find, "found"));
                Assert.AreEqual(selfSmartObject.ObjectId, executor.Evaluate(context, find, "objectId"));
                Assert.AreSame(selfSmartObject.gameObject, executor.Evaluate(context, find, "targetGameObject"));

                find.Properties["excludeGameObject"] = string.Empty;
                Assert.True((bool)executor.Evaluate(context, find, "found"));
                Assert.AreEqual(selfSmartObject.ObjectId, executor.Evaluate(context, find, "objectId"));
            }
            finally
            {
                DestroyObjects(objects);
                SmartObjectRegistry.ResetForTests();
            }
        }

        [Test]
        public void SmartObjectReserveBeginUseReleaseLifecycle()
        {
            SmartObjectRegistry.ResetForTests();
            List<GameObject> objects = new List<GameObject>();
            try
            {
                SmartObjectComponent smartObject = CreateSmartObject("bench_1", "Relax", Vector3.zero, 0f, 0f, "Park", string.Empty);
                objects.Add(smartObject.gameObject);
                string smartObjectId = smartObject.ObjectId;
                BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), new TestBindingResolver(), new RecordingBlueprintLogger(), null);

                RuntimeNode reserve = CreateRuntimeNode("reserve", "SmartObject.Reserve");
                reserve.Properties["requesterId"] = "npc-a";
                reserve.Properties["objectId"] = smartObjectId;
                reserve.Properties["slotId"] = 0;
                reserve.Properties["activity"] = "Relax";
                reserve.Properties["holdSeconds"] = 30f;
                SmartObjectReserveExecutor reserveExecutor = new SmartObjectReserveExecutor();

                Assert.AreEqual("execOut", reserveExecutor.Execute(context, reserve).NextExecPortId);
                Assert.True((bool)reserveExecutor.Evaluate(context, reserve, "success"));
                string token = (string)reserveExecutor.Evaluate(context, reserve, "reservationToken");
                Assert.False(string.IsNullOrEmpty(token));

                Assert.AreEqual("execOut", reserveExecutor.Execute(context, reserve).NextExecPortId);
                Assert.True((bool)reserveExecutor.Evaluate(context, reserve, "success"));
                Assert.AreEqual(token, reserveExecutor.Evaluate(context, reserve, "reservationToken"));

                RuntimeNode competingReserve = CreateRuntimeNode("competing_reserve", "SmartObject.Reserve");
                competingReserve.Properties["requesterId"] = "npc-b";
                competingReserve.Properties["objectId"] = smartObjectId;
                competingReserve.Properties["slotId"] = 0;
                competingReserve.Properties["activity"] = "Relax";
                SmartObjectReserveExecutor competingReserveExecutor = new SmartObjectReserveExecutor();
                competingReserveExecutor.Execute(context, competingReserve);
                Assert.False((bool)competingReserveExecutor.Evaluate(context, competingReserve, "success"));
                Assert.AreEqual("AlreadyReserved", competingReserveExecutor.Evaluate(context, competingReserve, "failReason"));

                RuntimeNode beginUse = CreateRuntimeNode("begin_use", "SmartObject.BeginUse");
                beginUse.Properties["requesterId"] = "npc-a";
                beginUse.Properties["reservationToken"] = token;
                SmartObjectBeginUseExecutor beginUseExecutor = new SmartObjectBeginUseExecutor();
                beginUseExecutor.Execute(context, beginUse);
                Assert.True((bool)beginUseExecutor.Evaluate(context, beginUse, "success"));
                Assert.AreEqual(smartObjectId, beginUseExecutor.Evaluate(context, beginUse, "objectId"));

                RuntimeNode info = CreateRuntimeNode("reservation_info", "SmartObject.GetReservationInfo");
                info.Properties["reservationToken"] = token;
                SmartObjectGetReservationInfoExecutor infoExecutor = new SmartObjectGetReservationInfoExecutor();
                Assert.True((bool)infoExecutor.Evaluate(context, info, "valid"));
                Assert.AreEqual("Occupied", infoExecutor.Evaluate(context, info, "state"));

                RuntimeNode wrongRelease = CreateRuntimeNode("wrong_release", "SmartObject.Release");
                wrongRelease.Properties["requesterId"] = "npc-b";
                wrongRelease.Properties["reservationToken"] = token;
                SmartObjectReleaseExecutor wrongReleaseExecutor = new SmartObjectReleaseExecutor();
                wrongReleaseExecutor.Execute(context, wrongRelease);
                Assert.False((bool)wrongReleaseExecutor.Evaluate(context, wrongRelease, "success"));
                Assert.AreEqual("TokenOwnerMismatch", wrongReleaseExecutor.Evaluate(context, wrongRelease, "failReason"));

                RuntimeNode release = CreateRuntimeNode("release", "SmartObject.Release");
                release.Properties["requesterId"] = "npc-a";
                release.Properties["reservationToken"] = token;
                release.Properties["reason"] = SmartObjectReleaseReason.Completed;
                SmartObjectReleaseExecutor releaseExecutor = new SmartObjectReleaseExecutor();
                releaseExecutor.Execute(context, release);
                Assert.True((bool)releaseExecutor.Evaluate(context, release, "success"));
                Assert.AreEqual("Occupied", releaseExecutor.Evaluate(context, release, "previousState"));

                RuntimeNode repeatRelease = CreateRuntimeNode("repeat_release", "SmartObject.Release");
                repeatRelease.Properties["requesterId"] = "npc-a";
                repeatRelease.Properties["reservationToken"] = token;
                SmartObjectReleaseExecutor repeatReleaseExecutor = new SmartObjectReleaseExecutor();
                repeatReleaseExecutor.Execute(context, repeatRelease);
                Assert.False((bool)repeatReleaseExecutor.Evaluate(context, repeatRelease, "success"));
                Assert.AreEqual("TokenInvalid", repeatReleaseExecutor.Evaluate(context, repeatRelease, "failReason"));
            }
            finally
            {
                DestroyObjects(objects);
                SmartObjectRegistry.ResetForTests();
            }
        }

        [Test]
        public void SmartObjectAccessClosedTimeoutAndRequesterCleanupReturnStableResults()
        {
            SmartObjectRegistry.ResetForTests();
            SmartObjectRegistry.SetTimeProviderForTests(() => 10f);
            List<GameObject> objects = new List<GameObject>();
            try
            {
                SmartObjectComponent protectedObject = CreateSmartObject("protected_desk", "Work", Vector3.zero, 0f, 0f, "Office", "Staff");
                objects.Add(protectedObject.gameObject);
                string protectedObjectId = protectedObject.ObjectId;
                BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), new TestBindingResolver(), new RecordingBlueprintLogger(), null);

                RuntimeNode deniedReserve = CreateRuntimeNode("denied_reserve", "SmartObject.Reserve");
                deniedReserve.Properties["requesterId"] = "npc-a";
                deniedReserve.Properties["objectId"] = protectedObjectId;
                deniedReserve.Properties["slotId"] = 0;
                deniedReserve.Properties["activity"] = "Work";
                deniedReserve.Properties["accessGroup"] = "Guest";
                SmartObjectReserveExecutor deniedReserveExecutor = new SmartObjectReserveExecutor();
                deniedReserveExecutor.Execute(context, deniedReserve);
                Assert.False((bool)deniedReserveExecutor.Evaluate(context, deniedReserve, "success"));
                Assert.AreEqual("AccessDenied", deniedReserveExecutor.Evaluate(context, deniedReserve, "failReason"));

                SmartObjectComponent closedObject = CreateSmartObject("closed_desk", "Work", Vector3.zero, 0f, 0f, "Office", string.Empty);
                closedObject.Slots[0].Closed = true;
                objects.Add(closedObject.gameObject);
                string closedObjectId = closedObject.ObjectId;
                RuntimeNode closedReserve = CreateRuntimeNode("closed_reserve", "SmartObject.Reserve");
                closedReserve.Properties["requesterId"] = "npc-a";
                closedReserve.Properties["objectId"] = closedObjectId;
                closedReserve.Properties["slotId"] = 0;
                closedReserve.Properties["activity"] = "Work";
                SmartObjectReserveExecutor closedReserveExecutor = new SmartObjectReserveExecutor();
                closedReserveExecutor.Execute(context, closedReserve);
                Assert.False((bool)closedReserveExecutor.Evaluate(context, closedReserve, "success"));
                Assert.AreEqual("Closed", closedReserveExecutor.Evaluate(context, closedReserve, "failReason"));

                RuntimeNode expiringReserve = CreateRuntimeNode("expiring_reserve", "SmartObject.Reserve");
                expiringReserve.Properties["requesterId"] = "npc-a";
                expiringReserve.Properties["objectId"] = protectedObjectId;
                expiringReserve.Properties["slotId"] = 0;
                expiringReserve.Properties["activity"] = "Work";
                expiringReserve.Properties["accessGroup"] = "Staff";
                expiringReserve.Properties["holdSeconds"] = 0f;
                SmartObjectReserveExecutor expiringReserveExecutor = new SmartObjectReserveExecutor();
                expiringReserveExecutor.Execute(context, expiringReserve);
                string expiredToken = (string)expiringReserveExecutor.Evaluate(context, expiringReserve, "reservationToken");

                RuntimeNode expiredBeginUse = CreateRuntimeNode("expired_begin_use", "SmartObject.BeginUse");
                expiredBeginUse.Properties["requesterId"] = "npc-a";
                expiredBeginUse.Properties["reservationToken"] = expiredToken;
                SmartObjectBeginUseExecutor expiredBeginUseExecutor = new SmartObjectBeginUseExecutor();
                expiredBeginUseExecutor.Execute(context, expiredBeginUse);
                Assert.False((bool)expiredBeginUseExecutor.Evaluate(context, expiredBeginUse, "success"));
                Assert.AreEqual("TokenExpired", expiredBeginUseExecutor.Evaluate(context, expiredBeginUse, "failReason"));

                SmartObjectComponent cleanupObject = CreateSmartObject("cleanup_object", "Work", Vector3.zero, 0f, 0f, "Office", string.Empty);
                cleanupObject.Slots.Add(CreateSmartObjectSlot(1, "Work", new Vector3(1f, 0f, 0f)));
                objects.Add(cleanupObject.gameObject);

                string cleanupObjectId = cleanupObject.ObjectId;
                string reservedToken = ReserveForTest(context, "npc-clean", cleanupObjectId, 0);
                string occupiedToken = ReserveForTest(context, "npc-clean", cleanupObjectId, 1);
                RuntimeNode beginUse = CreateRuntimeNode("cleanup_begin_use", "SmartObject.BeginUse");
                beginUse.Properties["requesterId"] = "npc-clean";
                beginUse.Properties["reservationToken"] = occupiedToken;
                new SmartObjectBeginUseExecutor().Execute(context, beginUse);

                RuntimeNode releaseByRequester = CreateRuntimeNode("release_by_requester", "SmartObject.ReleaseByRequester");
                releaseByRequester.Properties["requesterId"] = "npc-clean";
                releaseByRequester.Properties["reason"] = SmartObjectReleaseReason.ForceRelease;
                SmartObjectReleaseByRequesterExecutor releaseByRequesterExecutor = new SmartObjectReleaseByRequesterExecutor();
                releaseByRequesterExecutor.Execute(context, releaseByRequester);
                Assert.AreEqual(2, releaseByRequesterExecutor.Evaluate(context, releaseByRequester, "releasedCount"));
                Assert.AreEqual("None", releaseByRequesterExecutor.Evaluate(context, releaseByRequester, "failReason"));

                RuntimeNode reservedInfo = CreateRuntimeNode("reserved_info", "SmartObject.GetReservationInfo");
                reservedInfo.Properties["reservationToken"] = reservedToken;
                Assert.False((bool)new SmartObjectGetReservationInfoExecutor().Evaluate(context, reservedInfo, "valid"));
            }
            finally
            {
                DestroyObjects(objects);
                SmartObjectRegistry.ResetForTests();
            }
        }

        [Test]
        public void SmartObjectDebugSnapshotReportsRuntimeSlotState()
        {
            SmartObjectRegistry.ResetForTests();
            SmartObjectRegistry.SetTimeProviderForTests(() => 10f);
            List<GameObject> objects = new List<GameObject>();
            try
            {
                SmartObjectComponent smartObject = CreateSmartObject("debug_bench", "Work", new Vector3(2f, 0f, 0f), 1f, 2f, "Office", string.Empty);
                objects.Add(smartObject.gameObject);
                string smartObjectId = smartObject.ObjectId;
                BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), new TestBindingResolver(), new RecordingBlueprintLogger(), null);

                string token = ReserveForTest(context, "npc-debug", smartObjectId, 0);
                SmartObjectDebugSnapshot reservedSnapshot = SmartObjectRegistry.CreateDebugSnapshot(smartObject);
                Assert.AreEqual(SmartObjectRegistrationState.Registered, reservedSnapshot.RegistrationState);
                Assert.AreEqual(1, reservedSnapshot.ReservedSlotCount);
                Assert.AreEqual(1, reservedSnapshot.Slots.Length);

                SmartObjectSlotDebugSnapshot reservedSlot = reservedSnapshot.Slots[0];
                Assert.AreEqual(SmartObjectSlotState.Reserved, reservedSlot.State);
                Assert.AreEqual(SmartObjectSlotState.Reserved, reservedSlot.RuntimeState);
                Assert.AreEqual("npc-debug", reservedSlot.RequesterId);
                Assert.AreEqual(token, reservedSlot.ReservationToken);
                Assert.AreEqual(30f, reservedSlot.RemainingSeconds, 0.001f);
                Assert.AreEqual(new Vector3(2f, 0f, 0f), reservedSlot.TargetPosition);
                Assert.AreEqual(new Vector3(2f, 0f, 1f), reservedSlot.FacingPosition);

                RuntimeNode beginUse = CreateRuntimeNode("debug_begin_use", "SmartObject.BeginUse");
                beginUse.Properties["requesterId"] = "npc-debug";
                beginUse.Properties["reservationToken"] = token;
                new SmartObjectBeginUseExecutor().Execute(context, beginUse);

                SmartObjectDebugSnapshot occupiedSnapshot = SmartObjectRegistry.CreateDebugSnapshot(smartObject);
                SmartObjectSlotDebugSnapshot occupiedSlot = occupiedSnapshot.Slots[0];
                Assert.AreEqual(SmartObjectSlotState.Occupied, occupiedSlot.State);
                Assert.AreEqual("npc-debug", occupiedSlot.RequesterId);
                Assert.AreEqual(token, occupiedSlot.ReservationToken);
                Assert.AreEqual(0f, occupiedSlot.RemainingSeconds, 0.001f);

                RuntimeNode release = CreateRuntimeNode("debug_release", "SmartObject.Release");
                release.Properties["requesterId"] = "npc-debug";
                release.Properties["reservationToken"] = token;
                release.Properties["reason"] = SmartObjectReleaseReason.Completed;
                new SmartObjectReleaseExecutor().Execute(context, release);

                SmartObjectDebugSnapshot releasedSnapshot = SmartObjectRegistry.CreateDebugSnapshot(smartObject);
                SmartObjectSlotDebugSnapshot releasedSlot = releasedSnapshot.Slots[0];
                Assert.AreEqual(SmartObjectSlotState.Free, releasedSlot.State);
                Assert.AreEqual(SmartObjectSlotState.Free, releasedSlot.RuntimeState);
                Assert.AreEqual(string.Empty, releasedSlot.RequesterId);
                Assert.AreEqual(string.Empty, releasedSlot.ReservationToken);
                Assert.AreEqual(SmartObjectReleaseReason.Completed, releasedSlot.LastReleaseReason);
            }
            finally
            {
                DestroyObjects(objects);
                SmartObjectRegistry.ResetForTests();
            }
        }

        [Test]
        public void SmartObjectComponentGeneratesReadOnlyGuidAndRepairsDuplicateIds()
        {
            SmartObjectRegistry.ResetForTests();
            List<GameObject> objects = new List<GameObject>();
            try
            {
                Assert.IsNull(typeof(SmartObjectComponent).GetProperty("ObjectId").SetMethod);

                SmartObjectComponent first = CreateSmartObject("duplicate_debug", "Work", Vector3.zero, 0f, 0f, string.Empty, string.Empty);
                objects.Add(first.gameObject);
                AssertGeneratedObjectId(first.ObjectId);

                GameObject manualObject = new GameObject("manual_debug");
                objects.Add(manualObject);
                SmartObjectComponent manual = manualObject.AddComponent<SmartObjectComponent>();
                SetSerializedObjectIdForTest(manual, "manual_id");
                AssertGeneratedObjectId(manual.ObjectId);
                Assert.AreNotEqual("manual_id", manual.ObjectId);

                GameObject duplicateObject = new GameObject("duplicate_debug_clone");
                duplicateObject.SetActive(false);
                objects.Add(duplicateObject);
                SmartObjectComponent duplicate = duplicateObject.AddComponent<SmartObjectComponent>();
                SetSerializedObjectIdForTest(duplicate, first.ObjectId);
                AssertGeneratedObjectId(duplicate.ObjectId);
                Assert.AreNotEqual(first.ObjectId, duplicate.ObjectId);
                duplicateObject.SetActive(true);

                GameObject runtimeDuplicateObject = new GameObject("runtime_duplicate_debug");
                objects.Add(runtimeDuplicateObject);
                SmartObjectComponent runtimeDuplicate = runtimeDuplicateObject.AddComponent<SmartObjectComponent>();
                SmartObjectRegistry.Unregister(runtimeDuplicate, null);
                SetObjectIdBackingFieldForTest(runtimeDuplicate, first.ObjectId);
                SmartObjectRegistry.Register(runtimeDuplicate);
                AssertGeneratedObjectId(runtimeDuplicate.ObjectId);
                Assert.AreNotEqual(first.ObjectId, runtimeDuplicate.ObjectId);

                GameObject inactiveObject = new GameObject("inactive_debug");
                objects.Add(inactiveObject);
                SmartObjectComponent inactive = inactiveObject.AddComponent<SmartObjectComponent>();
                inactiveObject.SetActive(false);

                Assert.AreEqual(SmartObjectRegistrationState.Registered, SmartObjectRegistry.CreateDebugSnapshot(first).RegistrationState);
                Assert.AreEqual(SmartObjectRegistrationState.Registered, SmartObjectRegistry.CreateDebugSnapshot(duplicate).RegistrationState);
                Assert.AreEqual(SmartObjectRegistrationState.Registered, SmartObjectRegistry.CreateDebugSnapshot(runtimeDuplicate).RegistrationState);

                SmartObjectDebugSnapshot inactiveSnapshot = SmartObjectRegistry.CreateDebugSnapshot(inactive);
                Assert.AreEqual(SmartObjectRegistrationState.NotRegistered, inactiveSnapshot.RegistrationState);
                Assert.False(inactiveSnapshot.ActiveInHierarchy);
                Assert.False(inactiveSnapshot.IsActiveAndEnabled);
            }
            finally
            {
                DestroyObjects(objects);
                SmartObjectRegistry.ResetForTests();
            }
        }

        [Test]
        public void SmartObjectDebuggerSceneQueryIncludesInactiveSceneObjectsAndExcludesPersistentAssets()
        {
            const string PrefabPath = "Assets/BlueprintSystem/SmartObject/Tests/Editor/SmartObjectDebuggerTemp.prefab";

            SmartObjectRegistry.ResetForTests();
            AssetDatabase.DeleteAsset(PrefabPath);
            List<GameObject> objects = new List<GameObject>();
            try
            {
                SmartObjectComponent activeSceneObject = CreateSmartObject("scene_debug_active", "Work", Vector3.zero, 0f, 0f, string.Empty, string.Empty);
                objects.Add(activeSceneObject.gameObject);

                GameObject inactiveObject = new GameObject("scene_debug_inactive");
                objects.Add(inactiveObject);
                SmartObjectComponent inactiveSceneObject = inactiveObject.AddComponent<SmartObjectComponent>();
                inactiveObject.SetActive(false);

                GameObject prefabSource = new GameObject("scene_debug_prefab_source");
                prefabSource.AddComponent<SmartObjectComponent>();
                GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(prefabSource, PrefabPath);
                Object.DestroyImmediate(prefabSource);
                SmartObjectComponent prefabSmartObject = prefabAsset.GetComponent<SmartObjectComponent>();

                List<SmartObjectComponent> found = SmartObjectDebuggerUtility.FindSceneSmartObjects();
                Assert.Contains(activeSceneObject, found);
                Assert.Contains(inactiveSceneObject, found);
                Assert.False(found.Contains(prefabSmartObject));
                Assert.True(SmartObjectDebuggerUtility.IsSceneSmartObject(inactiveSceneObject));
                Assert.False(SmartObjectDebuggerUtility.IsSceneSmartObject(prefabSmartObject));
            }
            finally
            {
                AssetDatabase.DeleteAsset(PrefabPath);
                DestroyObjects(objects);
                SmartObjectRegistry.ResetForTests();
            }
        }

        private static SmartObjectComponent CreateSmartObject(
            string objectName,
            string activities,
            Vector3 localTargetPosition,
            float objectBaseScore,
            float slotBaseScore,
            string tags,
            string accessGroup)
        {
            GameObject gameObject = new GameObject(objectName);
            SmartObjectComponent smartObject = gameObject.AddComponent<SmartObjectComponent>();
            AssertGeneratedObjectId(smartObject.ObjectId);
            smartObject.ObjectBaseScore = objectBaseScore;
            smartObject.Tags = tags;
            smartObject.AccessGroup = accessGroup;
            smartObject.Slots.Add(CreateSmartObjectSlot(0, activities, localTargetPosition, slotBaseScore));
            return smartObject;
        }

        private static SmartObjectSlot CreateSmartObjectSlot(int slotId, string activities, Vector3 localTargetPosition, float slotBaseScore = 0f)
        {
            SmartObjectSlot slot = new SmartObjectSlot();
            slot.SlotId = slotId;
            slot.Activities = activities;
            slot.LocalTargetPosition = localTargetPosition;
            slot.LocalFacingPosition = localTargetPosition + Vector3.forward;
            slot.SlotBaseScore = slotBaseScore;
            slot.UseDuration = 3f;
            return slot;
        }

        private static string ReserveForTest(BlueprintExecutionContext context, string requesterId, string objectId, int slotId)
        {
            RuntimeNode reserve = CreateRuntimeNode("reserve_" + objectId + "_" + slotId, "SmartObject.Reserve");
            reserve.Properties["requesterId"] = requesterId;
            reserve.Properties["objectId"] = objectId;
            reserve.Properties["slotId"] = slotId;
            reserve.Properties["activity"] = "Work";
            reserve.Properties["holdSeconds"] = 30f;
            SmartObjectReserveExecutor executor = new SmartObjectReserveExecutor();
            executor.Execute(context, reserve);
            Assert.True((bool)executor.Evaluate(context, reserve, "success"), (string)executor.Evaluate(context, reserve, "failReason"));
            string token = (string)executor.Evaluate(context, reserve, "reservationToken");
            Assert.False(string.IsNullOrEmpty(token));
            return token;
        }

        private static void AssertGeneratedObjectId(string objectId)
        {
            Guid parsed;
            Assert.True(Guid.TryParseExact(objectId, "N", out parsed), objectId);
        }

        private static void SetSerializedObjectIdForTest(SmartObjectComponent component, string objectId)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty("objectId");
            Assert.NotNull(property);
            property.stringValue = objectId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            InvokeOnValidate(component);
        }

        private static void InvokeOnValidate(SmartObjectComponent component)
        {
            MethodInfo method = typeof(SmartObjectComponent).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(component, null);
        }

        private static void SetObjectIdBackingFieldForTest(SmartObjectComponent component, string objectId)
        {
            FieldInfo field = typeof(SmartObjectComponent).GetField("objectId", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(component, objectId);
        }

        private static void DestroyObjects(List<GameObject> objects)
        {
            if (objects == null)
            {
                return;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }
        }

        private static RuntimeNode CreateRuntimeNode(string id, string typeId)
        {
            return new RuntimeNode
            {
                Id = id,
                TypeId = typeId
            };
        }

        private static BlueprintExecutionContext CreateTestContext(
            RuntimeBlueprint blueprint,
            IBlueprintBindingResolver resolver,
            IBlueprintLogger logger,
            Action<RuntimeNode, string> executeFromOutput)
        {
            return new BlueprintExecutionContext(
                blueprint,
                null,
                null,
                resolver,
                new DictionaryBlueprintVariableStore(blueprint),
                new ActionBlueprintEventBus(eventName => { }),
                logger,
                executeFromOutput);
        }

        private static BlueprintNodeManifestCollection LoadManifests()
        {
            return BlueprintNodeManifestAssetUtility.LoadManifests();
        }

        private sealed class TestBindingResolver : IBlueprintBindingResolver
        {
            private readonly Dictionary<string, Object> _bindings = new Dictionary<string, Object>();

            public void Bind(string bindingName, Object value)
            {
                _bindings[bindingName] = value;
            }

            public T Resolve<T>(string bindingName) where T : Object
            {
                Object value = Resolve(bindingName);
                return value as T;
            }

            public Object Resolve(string bindingName)
            {
                Object value;
                return _bindings.TryGetValue(bindingName, out value) ? value : null;
            }

            public bool HasBinding(string bindingName)
            {
                return _bindings.ContainsKey(bindingName);
            }
        }
    }
}
