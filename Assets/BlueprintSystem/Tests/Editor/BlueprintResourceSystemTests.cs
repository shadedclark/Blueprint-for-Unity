using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using BlueprintSystem.Editor;

namespace BlueprintSystem.Tests
{
    public sealed class BlueprintResourceSystemTests
    {
        private const string TempRoot = "Assets/BlueprintSystem/Tests/Editor/TempResourceSystem";
        private const string CanonicalResourceFolder = "Assets/BlueprintSystem/Resources";
        private bool _hadCanonicalCatalogBeforeTest;
        private bool _hadCanonicalPackagingPolicyBeforeTest;
        private bool _hadCanonicalResourceFolderBeforeTest;
        private List<BlueprintResourceTypeDefinition> _canonicalCatalogSnapshot;
        private string _canonicalPackagingPolicySnapshot;

        [SetUp]
        public void SetUp()
        {
            _hadCanonicalResourceFolderBeforeTest = AssetDatabase.IsValidFolder(CanonicalResourceFolder);
            BlueprintResourceTypeCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<BlueprintResourceTypeCatalogAsset>(
                    BlueprintResourceAssetManagerUtility.ResourceTypeCatalogAssetPath);
            _hadCanonicalCatalogBeforeTest = catalog != null;
            _canonicalCatalogSnapshot = CloneResourceTypeDefinitions(catalog);
            BlueprintResourcePackagingPolicyAsset policy =
                AssetDatabase.LoadAssetAtPath<BlueprintResourcePackagingPolicyAsset>(
                    BlueprintResourceAssetManagerUtility.PackagingPolicyAssetPath);
            _hadCanonicalPackagingPolicyBeforeTest = policy != null;
            _canonicalPackagingPolicySnapshot = policy == null ? null : EditorJsonUtility.ToJson(policy, true);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempRoot);
            RestoreCanonicalResourcePackagingPolicy();
            RestoreCanonicalResourceTypeCatalog();

            _hadCanonicalCatalogBeforeTest = false;
            _hadCanonicalPackagingPolicyBeforeTest = false;
            _hadCanonicalResourceFolderBeforeTest = false;
            _canonicalCatalogSnapshot = null;
            _canonicalPackagingPolicySnapshot = null;
            BlueprintResourceManager.Instance.ClearRuntimeState();
            BlueprintResourceManager.Instance.Provider = null;
            BlueprintResourceManager.Instance.SetRegistry(null);
        }

        [Test]
        public void ResourceBlueprintSourceRoundTripsPrimaryAssetData()
        {
            BlueprintResourceBlueprintSource source = new BlueprintResourceBlueprintSource();
            source.ResourceType = "Item";
            source.ResourceName = "Potion";
            source.DisplayName = "Potion";
            source.Tags.Add("Consumable");
            source.MainAsset.Path = "Assets/Game/Items/Potion.prefab";
            source.MainAsset.Guid = "guid";
            source.MainAsset.Address = "Resource/Item/Potion";
            source.MainAsset.AssetType = "GameObject";
            source.PreloadGroups.Add("Inventory");
            source.Dependencies.Add(new BlueprintResourceDependency
            {
                ResourceType = "Icon",
                ResourceName = "PotionIcon",
                Required = true
            });
            source.Metadata.Add(new BlueprintResourceMetadataField
            {
                Name = "price",
                ValueJson = "25"
            });

            BlueprintResourceBlueprintSource loaded = BlueprintResourceBlueprintSource.FromJson(source.ToJson());
            Assert.AreEqual("Item", loaded.ResourceType);
            Assert.AreEqual("Potion", loaded.ResourceName);
            Assert.AreEqual("Resource/Item/Potion", loaded.MainAsset.Address);
            Assert.AreEqual("Inventory", loaded.PreloadGroups[0]);
            Assert.AreEqual("Icon:PotionIcon", loaded.Dependencies[0].ToId().ToString());
            Assert.AreEqual("price", loaded.Metadata[0].Name);
            Assert.AreEqual("25", loaded.Metadata[0].ValueJson);
        }

        [Test]
        public void ResourceManagerSharesInFlightLoadsAndReleasesAfterLastReference()
        {
            BlueprintResourceRegistryAsset registry = ScriptableObject.CreateInstance<BlueprintResourceRegistryAsset>();
            BlueprintResourceRegistryEntry entry = new BlueprintResourceRegistryEntry
            {
                ResourceType = "Item",
                ResourceName = "Potion",
                MainAssetAddress = "Resource/Item/Potion",
                MemoryBudgetMb = 2f
            };
            registry.SetGeneratedData("0.1", "hash", new[] { entry }, 4, 512f);

            FakeResourceProvider provider = new FakeResourceProvider();
            BlueprintResourceManager manager = new BlueprintResourceManager();
            manager.SetRegistry(registry);
            manager.Provider = provider;

            BlueprintPrimaryResourceId id = new BlueprintPrimaryResourceId("Item", "Potion");
            int completed = 0;
            BlueprintResourceLoadHandle first = manager.LoadAsync(id, BlueprintResourceScope.Manual, delegate { completed++; });
            BlueprintResourceLoadHandle second = manager.LoadAsync(id, BlueprintResourceScope.Screen, delegate { completed++; });

            Assert.AreEqual(1, provider.LoadCount);
            Assert.AreEqual(BlueprintResourceLoadState.Loading, first.State);
            Assert.AreEqual(BlueprintResourceLoadState.Loading, second.State);

            BlueprintResourceTestAsset asset = ScriptableObject.CreateInstance<BlueprintResourceTestAsset>();
            provider.LastOperation.Complete(asset, null);

            Assert.AreEqual(2, completed);
            Assert.AreEqual(BlueprintResourceLoadState.Loaded, first.State);
            Assert.AreEqual(asset, second.Asset);

            first.Release();
            Assert.AreEqual(0, provider.LastOperation.ReleaseCount);
            second.Release();
            Assert.AreEqual(1, provider.LastOperation.ReleaseCount);
            Assert.AreEqual(BlueprintResourceLoadState.Unloaded, manager.GetLoadState(id));

            UnityEngine.Object.DestroyImmediate(asset);
            UnityEngine.Object.DestroyImmediate(registry);
        }

        [Test]
        public void ResourceManagerPreloadGroupReportsMemberFailure()
        {
            BlueprintResourceRegistryAsset registry = ScriptableObject.CreateInstance<BlueprintResourceRegistryAsset>();
            BlueprintResourceRegistryEntry firstEntry = new BlueprintResourceRegistryEntry
            {
                ResourceType = "Item",
                ResourceName = "Potion",
                MainAssetAddress = "Resource/Item/Potion",
                PreloadGroups = new[] { "Inventory" }
            };
            BlueprintResourceRegistryEntry secondEntry = new BlueprintResourceRegistryEntry
            {
                ResourceType = "Item",
                ResourceName = "Elixir",
                MainAssetAddress = "Resource/Item/Elixir",
                PreloadGroups = new[] { "Inventory" }
            };
            registry.SetGeneratedData("0.1", "hash", new[] { firstEntry, secondEntry }, 4, 512f);

            FakeResourceProvider provider = new FakeResourceProvider();
            BlueprintResourceManager manager = new BlueprintResourceManager();
            manager.SetRegistry(registry);
            manager.Provider = provider;

            BlueprintResourceGroupLoadHandle group = null;
            int completed = 0;
            group = manager.PreloadGroupAsync("Inventory", BlueprintResourceScope.Screen, delegate { completed++; });

            Assert.AreEqual(2, provider.LoadCount);
            Assert.False(group.IsDone);

            BlueprintResourceTestAsset asset = ScriptableObject.CreateInstance<BlueprintResourceTestAsset>();
            provider.Operations[0].Complete(asset, null);
            Assert.False(group.IsDone);

            provider.Operations[1].Complete(null, "missing asset");
            Assert.AreEqual(1, completed);
            Assert.True(group.IsDone);
            Assert.False(group.Succeeded);
            Assert.That(group.Error, Does.Contain("failed"));

            group.Cancel();
            UnityEngine.Object.DestroyImmediate(asset);
            UnityEngine.Object.DestroyImmediate(registry);
        }

        [Test]
        public void ResourceManagerCancelledQueuedHandleCanLoadAgain()
        {
            BlueprintResourceRegistryAsset registry = ScriptableObject.CreateInstance<BlueprintResourceRegistryAsset>();
            BlueprintResourceRegistryEntry activeEntry = new BlueprintResourceRegistryEntry
            {
                ResourceType = "Item",
                ResourceName = "Potion",
                MainAssetAddress = "Resource/Item/Potion"
            };
            BlueprintResourceRegistryEntry queuedEntry = new BlueprintResourceRegistryEntry
            {
                ResourceType = "Item",
                ResourceName = "Elixir",
                MainAssetAddress = "Resource/Item/Elixir"
            };
            registry.SetGeneratedData("0.1", "hash", new[] { activeEntry, queuedEntry }, 1, 512f);

            FakeResourceProvider provider = new FakeResourceProvider();
            BlueprintResourceManager manager = new BlueprintResourceManager();
            manager.SetRegistry(registry);
            manager.Provider = provider;

            BlueprintPrimaryResourceId activeId = new BlueprintPrimaryResourceId("Item", "Potion");
            BlueprintPrimaryResourceId queuedId = new BlueprintPrimaryResourceId("Item", "Elixir");
            int cancelledCallbacks = 0;
            BlueprintResourceLoadHandle active = manager.LoadAsync(activeId, BlueprintResourceScope.Manual);
            BlueprintResourceLoadHandle queued = manager.LoadAsync(queuedId, BlueprintResourceScope.Manual, delegate(BlueprintResourceLoadHandle handle)
            {
                if (handle.State == BlueprintResourceLoadState.Cancelled)
                {
                    cancelledCallbacks++;
                }
            });

            Assert.AreEqual(1, provider.LoadCount);
            Assert.AreEqual(BlueprintResourceLoadState.Loading, active.State);
            Assert.AreEqual(BlueprintResourceLoadState.Queued, queued.State);

            queued.Cancel();
            Assert.AreEqual(BlueprintResourceLoadState.Cancelled, queued.State);
            Assert.AreEqual(BlueprintResourceLoadState.Unloaded, manager.GetLoadState(queuedId));
            Assert.AreEqual(1, cancelledCallbacks);

            BlueprintResourceLoadHandle queuedAgain = manager.LoadAsync(queuedId, BlueprintResourceScope.Manual, delegate(BlueprintResourceLoadHandle handle)
            {
                if (handle.State == BlueprintResourceLoadState.Cancelled)
                {
                    cancelledCallbacks++;
                }
            });
            Assert.AreEqual(BlueprintResourceLoadState.Queued, queuedAgain.State);

            BlueprintResourceTestAsset firstAsset = ScriptableObject.CreateInstance<BlueprintResourceTestAsset>();
            provider.Operations[0].Complete(firstAsset, null);
            Assert.AreEqual(2, provider.LoadCount);
            Assert.AreEqual(BlueprintResourceLoadState.Loading, queuedAgain.State);
            Assert.AreEqual(1, cancelledCallbacks);

            BlueprintResourceTestAsset secondAsset = ScriptableObject.CreateInstance<BlueprintResourceTestAsset>();
            provider.Operations[1].Complete(secondAsset, null);
            Assert.AreEqual(BlueprintResourceLoadState.Loaded, queuedAgain.State);

            active.Release();
            queuedAgain.Release();
            UnityEngine.Object.DestroyImmediate(firstAsset);
            UnityEngine.Object.DestroyImmediate(secondAsset);
            UnityEngine.Object.DestroyImmediate(registry);
        }

        [Test]
        public void AssetManagerScanValidatesProjectResourceBlueprint()
        {
            EnsureTempRoot();
            string assetPath = TempRoot + "/Potion.asset";
            BlueprintResourceTestAsset asset = ScriptableObject.CreateInstance<BlueprintResourceTestAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.ImportAsset(assetPath);

            BlueprintResourceBlueprintSource source = new BlueprintResourceBlueprintSource();
            source.ResourceType = "Item";
            source.ResourceName = "Potion";
            source.MainAsset.Path = assetPath;
            source.MemoryBudgetMb = 1f;
            string sourcePath = TempRoot + "/Potion.resourceblueprint.json";
            File.WriteAllText(sourcePath, source.ToJson());
            AssetDatabase.ImportAsset(sourcePath);

            BlueprintResourceAssetManagerReport report = BlueprintResourceAssetManagerUtility.ScanProject(false);
            BlueprintResourceAssetRecord record = FindRecord(report, sourcePath);
            Assert.NotNull(record);
            Assert.False(HasErrors(record));
        }

        [Test]
        public void ResourceAssetManagerCreatesCanonicalTypeCatalogWhenMissing()
        {
            BlueprintResourceTypeCatalogAsset catalog =
                BlueprintResourceAssetManagerUtility.GetOrCreateResourceTypeCatalogAsset();

            Assert.NotNull(catalog);
            Assert.AreEqual(
                BlueprintResourceAssetManagerUtility.ResourceTypeCatalogAssetPath,
                AssetDatabase.GetAssetPath(catalog));
            Assert.True(AssetDatabase.IsValidFolder(CanonicalResourceFolder));
        }

        [Test]
        public void ResourceAssetManagerReusesExistingCanonicalTypeCatalog()
        {
            BlueprintResourceTypeCatalogAsset first =
                BlueprintResourceAssetManagerUtility.GetOrCreateResourceTypeCatalogAsset();
            int countBefore = CountResourceTypeCatalogAssets();
            BlueprintResourceTypeCatalogAsset second =
                BlueprintResourceAssetManagerUtility.GetOrCreateResourceTypeCatalogAsset();
            int countAfter = CountResourceTypeCatalogAssets();

            Assert.NotNull(first);
            Assert.AreEqual(first, second);
            Assert.AreEqual(countBefore, countAfter);
        }

        [Test]
        public void ResourceAssetManagerRegistersResourceTypeInCatalogAsset()
        {
            EnsureTempRoot();
            string catalogPath = TempRoot + "/RegisteredResourceTypeCatalog.asset";
            BlueprintResourceTypeCatalogAsset catalog = ScriptableObject.CreateInstance<BlueprintResourceTypeCatalogAsset>();
            AssetDatabase.CreateAsset(catalog, catalogPath);

            Assert.True(BlueprintResourceAssetManagerUtility.RegisterResourceType(catalog, "  Weapon  ", true));
            Assert.False(BlueprintResourceAssetManagerUtility.RegisterResourceType(catalog, "Weapon", true));

            AssetDatabase.ImportAsset(catalogPath);
            BlueprintResourceTypeCatalogAsset loaded =
                AssetDatabase.LoadAssetAtPath<BlueprintResourceTypeCatalogAsset>(catalogPath);

            Assert.NotNull(loaded);
            Assert.AreEqual(1, loaded.ResourceTypes.Count);
            Assert.AreEqual("Weapon", loaded.ResourceTypes[0].ResourceType);
        }

        [Test]
        public void AssetManagerScanValidatesRequiredMetadataFromResourceTypeCatalog()
        {
            EnsureTempRoot();
            BlueprintResourceTypeCatalogAsset catalog = ScriptableObject.CreateInstance<BlueprintResourceTypeCatalogAsset>();
            BlueprintResourceTypeDefinition itemType = new BlueprintResourceTypeDefinition { ResourceType = "RequiredMetadataType" };
            itemType.Fields.Add(new BlueprintResourceTypeField
            {
                Name = "rarity",
                Type = "string",
                Required = true
            });
            catalog.ResourceTypes.Add(itemType);
            AssetDatabase.CreateAsset(catalog, TempRoot + "/ResourceTypeCatalog.asset");

            string assetPath = TempRoot + "/Potion.asset";
            BlueprintResourceTestAsset asset = ScriptableObject.CreateInstance<BlueprintResourceTestAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.ImportAsset(assetPath);

            BlueprintResourceBlueprintSource source = new BlueprintResourceBlueprintSource();
            source.ResourceType = "RequiredMetadataType";
            source.ResourceName = "Potion";
            source.MainAsset.Path = assetPath;
            string sourcePath = TempRoot + "/Potion.resourceblueprint.json";
            File.WriteAllText(sourcePath, source.ToJson());
            AssetDatabase.ImportAsset(sourcePath);

            BlueprintResourceAssetManagerReport report = BlueprintResourceAssetManagerUtility.ScanProject(false);
            BlueprintResourceAssetRecord record = FindRecord(report, sourcePath);

            Assert.NotNull(record);
            Assert.True(HasIssueContaining(record, "Required metadata field 'rarity' is missing."));
        }

        [Test]
        public void PackagingPolicyResolvesTypeRuleAndResourceOverride()
        {
            EnsureTempRoot();
            string heroAPath = CreateTestAsset(TempRoot + "/HeroA.asset");
            string heroBPath = CreateTestAsset(TempRoot + "/HeroB.asset");
            string heroAJsonPath = TempRoot + "/HeroA.resourceblueprint.json";
            string heroBJsonPath = TempRoot + "/HeroB.resourceblueprint.json";
            WriteResourceSourceWithMainAsset(heroAJsonPath, "Hero", "HeroA", heroAPath);
            WriteResourceSourceWithMainAsset(heroBJsonPath, "Hero", "HeroB", heroBPath);

            BlueprintResourcePackagingPolicyAsset policy =
                BlueprintResourceAssetManagerUtility.GetOrCreateResourcePackagingPolicyAsset();
            ResetPackagingPolicy(policy);
            policy.Dlcs.Add(new BlueprintResourceDlcDefinition
            {
                DlcId = "heroes",
                DisplayName = "Heroes",
                IncludeInBuild = true
            });
            BlueprintResourceTypePackagingRule typeRule = new BlueprintResourceTypePackagingRule();
            typeRule.ResourceType = "Hero";
            typeRule.Rule.ContentLocation = BlueprintResourceContentLocation.DLC;
            typeRule.Rule.DlcId = "heroes";
            typeRule.Rule.LoadPriority = 10;
            policy.TypeRules.Add(typeRule);

            BlueprintResourceOverridePackagingRule resourceOverride = new BlueprintResourceOverridePackagingRule();
            resourceOverride.ResourceType = "Hero";
            resourceOverride.ResourceName = "HeroA";
            resourceOverride.Rule.ContentLocation = BlueprintResourceContentLocation.Base;
            resourceOverride.Rule.DlcId = string.Empty;
            resourceOverride.Rule.LoadPriority = 99;
            policy.ResourceOverrides.Add(resourceOverride);
            EditorUtility.SetDirty(policy);
            AssetDatabase.SaveAssets();

            BlueprintResourceAssetManagerReport report = BlueprintResourceAssetManagerUtility.ScanProject(false);
            BlueprintResourceAssetRecord heroA = FindRecord(report, heroAJsonPath);
            BlueprintResourceAssetRecord heroB = FindRecord(report, heroBJsonPath);

            Assert.NotNull(heroA);
            Assert.NotNull(heroB);
            Assert.AreEqual(BlueprintResourceContentLocation.Base, heroA.Packaging.ContentLocation);
            Assert.AreEqual("BlueprintResources_Base_Hero", heroA.Packaging.GroupName);
            Assert.AreEqual(99, heroA.Packaging.LoadPriority);
            Assert.AreEqual(BlueprintResourceContentLocation.DLC, heroB.Packaging.ContentLocation);
            Assert.AreEqual("heroes", heroB.Packaging.DlcId);
            Assert.AreEqual("BlueprintResources_DLC_heroes_Hero", heroB.Packaging.GroupName);
            Assert.AreEqual(10, heroB.Packaging.LoadPriority);
        }

        [Test]
        public void PackagingValidationFailsForMissingDlc()
        {
            EnsureTempRoot();
            string assetPath = CreateTestAsset(TempRoot + "/HeroMissingDlc.asset");
            string sourcePath = TempRoot + "/HeroMissingDlc.resourceblueprint.json";
            WriteResourceSourceWithMainAsset(sourcePath, "Hero", "MissingDlcHero", assetPath);

            BlueprintResourcePackagingPolicyAsset policy =
                BlueprintResourceAssetManagerUtility.GetOrCreateResourcePackagingPolicyAsset();
            ResetPackagingPolicy(policy);
            BlueprintResourceTypePackagingRule typeRule = new BlueprintResourceTypePackagingRule();
            typeRule.ResourceType = "Hero";
            typeRule.Rule.ContentLocation = BlueprintResourceContentLocation.DLC;
            typeRule.Rule.DlcId = "missing";
            policy.TypeRules.Add(typeRule);
            EditorUtility.SetDirty(policy);
            AssetDatabase.SaveAssets();

            BlueprintResourceAssetManagerReport report = BlueprintResourceAssetManagerUtility.ScanProject(false);
            BlueprintResourceAssetRecord record = FindRecord(report, sourcePath);

            Assert.NotNull(record);
            Assert.True(HasIssueContaining(record, "DLC id 'missing' is not defined"));
        }

        [Test]
        public void PackagingValidationRejectsRequiredDependencyExcludedFromBuild()
        {
            EnsureTempRoot();
            string ownerAssetPath = CreateTestAsset(TempRoot + "/Owner.asset");
            string dependencyAssetPath = CreateTestAsset(TempRoot + "/Dependency.asset");
            string ownerPath = TempRoot + "/Owner.resourceblueprint.json";
            string dependencyPath = TempRoot + "/Dependency.resourceblueprint.json";

            BlueprintResourceBlueprintSource owner = new BlueprintResourceBlueprintSource();
            owner.ResourceType = "Item";
            owner.ResourceName = "Owner";
            owner.MainAsset.Path = ownerAssetPath;
            owner.Dependencies.Add(new BlueprintResourceDependency
            {
                ResourceType = "Item",
                ResourceName = "Dependency",
                Required = true
            });
            File.WriteAllText(ownerPath, owner.ToJson());
            AssetDatabase.ImportAsset(ownerPath);
            WriteResourceSourceWithMainAsset(dependencyPath, "Item", "Dependency", dependencyAssetPath);

            BlueprintResourcePackagingPolicyAsset policy =
                BlueprintResourceAssetManagerUtility.GetOrCreateResourcePackagingPolicyAsset();
            ResetPackagingPolicy(policy);
            BlueprintResourceOverridePackagingRule dependencyOverride = new BlueprintResourceOverridePackagingRule();
            dependencyOverride.ResourceType = "Item";
            dependencyOverride.ResourceName = "Dependency";
            dependencyOverride.Rule.IncludeInBuild = false;
            policy.ResourceOverrides.Add(dependencyOverride);
            EditorUtility.SetDirty(policy);
            AssetDatabase.SaveAssets();

            BlueprintResourceAssetManagerReport report = BlueprintResourceAssetManagerUtility.ScanProject(false);
            BlueprintResourceAssetRecord record = FindRecord(report, ownerPath);

            Assert.NotNull(record);
            Assert.True(HasIssueContaining(record, "Required dependency 'Item:Dependency' is excluded by resource packaging."));
        }

        [Test]
        public void SharedDependencyScanFindsProjectAssetUsedByMultipleGroups()
        {
            EnsureTempRoot();
            BlueprintResourceTestAsset shared = ScriptableObject.CreateInstance<BlueprintResourceTestAsset>();
            string sharedPath = TempRoot + "/Shared.asset";
            AssetDatabase.CreateAsset(shared, sharedPath);

            string firstAssetPath = CreateTestAssetWithReference(TempRoot + "/First.asset", shared);
            string secondAssetPath = CreateTestAssetWithReference(TempRoot + "/Second.asset", shared);
            string firstJsonPath = TempRoot + "/First.resourceblueprint.json";
            string secondJsonPath = TempRoot + "/Second.resourceblueprint.json";
            WriteResourceSourceWithMainAsset(firstJsonPath, "Hero", "First", firstAssetPath);
            WriteResourceSourceWithMainAsset(secondJsonPath, "Quest", "Second", secondAssetPath);

            BlueprintResourcePackagingPolicyAsset policy =
                BlueprintResourceAssetManagerUtility.GetOrCreateResourcePackagingPolicyAsset();
            ResetPackagingPolicy(policy);
            policy.Dlcs.Add(new BlueprintResourceDlcDefinition
            {
                DlcId = "chapter1",
                DisplayName = "Chapter 1",
                IncludeInBuild = true
            });
            BlueprintResourceTypePackagingRule questRule = new BlueprintResourceTypePackagingRule();
            questRule.ResourceType = "Quest";
            questRule.Rule.ContentLocation = BlueprintResourceContentLocation.DLC;
            questRule.Rule.DlcId = "chapter1";
            policy.TypeRules.Add(questRule);
            EditorUtility.SetDirty(policy);
            AssetDatabase.SaveAssets();

            BlueprintResourceAssetManagerReport report = BlueprintResourceAssetManagerUtility.ScanProject(false);
            List<BlueprintResourceSharedDependencyCandidate> candidates =
                BlueprintResourceAssetManagerUtility.ScanSharedDependencies(report);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(sharedPath, candidates[0].AssetPath);
            Assert.AreEqual("BlueprintResources_Shared_Base", candidates[0].SharedGroupName);
            Assert.Contains("Hero:First", candidates[0].OwnerResourceIds);
            Assert.Contains("Quest:Second", candidates[0].OwnerResourceIds);
        }

        [Test]
        public void ResourceGraphToolkitBridgeOpenAssetCallbackHandlesResourceBlueprintJson()
        {
            EnsureTempRoot();
            string sourcePath = TempRoot + "/OpenCallbackPotion.resourceblueprint.json";
            string graphPath = BlueprintResourceGraphToolkitBridge.GetDefaultGraphPath(sourcePath);
            string plainJsonPath = TempRoot + "/OpenCallbackPlain.json";
            AssetDatabase.DeleteAsset(graphPath);
            AssetDatabase.DeleteAsset(plainJsonPath);

            BlueprintResourceBlueprintSource source = new BlueprintResourceBlueprintSource();
            source.ResourceType = "Item";
            source.ResourceName = "OpenCallbackPotion";
            source.DisplayName = "Open Callback Potion";
            source.MemoryBudgetMb = 2f;
            File.WriteAllText(sourcePath, source.ToJson());
            AssetDatabase.ImportAsset(sourcePath);

            File.WriteAllText(plainJsonPath, "{}");
            AssetDatabase.ImportAsset(plainJsonPath);

            TextAsset sourceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
            TextAsset plainJsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(plainJsonPath);
            Assert.NotNull(sourceAsset);
            Assert.NotNull(plainJsonAsset);

            Assert.True(BlueprintResourceGraphToolkitBridge.OnOpenAsset(sourceAsset.GetInstanceID(), 0));
            Assert.True(File.Exists(graphPath));

            BlueprintResourceVisualGraph graph = GraphDatabase.LoadGraph<BlueprintResourceVisualGraph>(graphPath);
            Assert.NotNull(graph);
            Assert.AreEqual(sourcePath, graph.SourceResourceBlueprintAssetPath);
            Assert.AreEqual("Item", graph.ResourceType);
            Assert.AreEqual("OpenCallbackPotion", graph.ResourceName);
            Assert.AreEqual("Open Callback Potion", graph.DisplayName);
            Assert.AreEqual(2f, graph.MemoryBudgetMb);
            Assert.False(BlueprintResourceGraphToolkitBridge.OnOpenAsset(plainJsonAsset.GetInstanceID(), 0));
        }

        [Test]
        public void ResourceGraphToolkitTypesReturnsSortedTypesAndPreservesMissingType()
        {
            EnsureTempRoot();
            BlueprintResourceTypeCatalogAsset catalog = ScriptableObject.CreateInstance<BlueprintResourceTypeCatalogAsset>();
            catalog.ResourceTypes.Add(new BlueprintResourceTypeDefinition { ResourceType = "TestResourceType_Z" });
            catalog.ResourceTypes.Add(new BlueprintResourceTypeDefinition { ResourceType = "TestResourceType_A" });
            AssetDatabase.CreateAsset(catalog, TempRoot + "/ResourceTypeCatalog.asset");
            AssetDatabase.ImportAsset(TempRoot);

            string[] types = BlueprintResourceGraphToolkitTypes.GetResourceTypes();
            Assert.GreaterOrEqual(IndexOf(types, "TestResourceType_A"), 0);
            Assert.GreaterOrEqual(IndexOf(types, "TestResourceType_Z"), 0);
            Assert.Less(IndexOf(types, "TestResourceType_A"), IndexOf(types, "TestResourceType_Z"));

            string[] withMissing = BlueprintResourceGraphToolkitTypes.GetResourceTypes("TestResourceType_Missing");
            Assert.GreaterOrEqual(IndexOf(withMissing, "TestResourceType_Missing"), 0);

            string[] withEmpty = BlueprintResourceGraphToolkitTypes.GetResourceTypes(string.Empty);
            Assert.GreaterOrEqual(IndexOf(withEmpty, string.Empty), 0);
        }

        [Test]
        public void ResourceGraphToolkitTypesAppliesResolvesAndClearsSoftReference()
        {
            EnsureTempRoot();
            string assetPath = TempRoot + "/SoftReference.asset";
            BlueprintResourceTestAsset asset = ScriptableObject.CreateInstance<BlueprintResourceTestAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.ImportAsset(assetPath);

            BlueprintResourceAssetReference reference = new BlueprintResourceAssetReference
            {
                Address = "stale-address"
            };
            BlueprintResourceGraphToolkitTypes.ApplyAssetReference(reference, asset);

            Assert.AreEqual(assetPath, reference.Path);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(assetPath), reference.Guid);
            Assert.AreEqual(nameof(BlueprintResourceTestAsset), reference.AssetType);
            Assert.AreEqual(string.Empty, reference.Address);
            Assert.AreEqual(asset, BlueprintResourceGraphToolkitTypes.ResolveAssetReference(reference));

            BlueprintResourceGraphToolkitTypes.ApplyAssetReference(reference, null);
            Assert.AreEqual(string.Empty, reference.Path);
            Assert.AreEqual(string.Empty, reference.Guid);
            Assert.AreEqual(string.Empty, reference.AssetType);
            Assert.AreEqual(string.Empty, reference.Address);
            Assert.Null(BlueprintResourceGraphToolkitTypes.ResolveAssetReference(reference));
        }

        [Test]
        public void ResourceGraphToolkitTypesFiltersResourceIdsByTypeAndPreservesMissingName()
        {
            EnsureTempRoot();
            WriteResourceSource(TempRoot + "/Potion.resourceblueprint.json", "TestDependencyType_A", "Potion");
            WriteResourceSource(TempRoot + "/Elixir.resourceblueprint.json", "TestDependencyType_A", "Elixir");
            WriteResourceSource(TempRoot + "/Icon.resourceblueprint.json", "TestDependencyType_B", "Icon");

            BlueprintPrimaryResourceId[] ids = BlueprintResourceGraphToolkitTypes.GetResourceIds("TestDependencyType_A");
            Assert.AreEqual(2, ids.Length);
            Assert.AreEqual("Elixir", ids[0].ResourceName);
            Assert.AreEqual("Potion", ids[1].ResourceName);

            string[] names = BlueprintResourceGraphToolkitTypes.GetResourceNames("TestDependencyType_A", "MissingResource");
            Assert.GreaterOrEqual(IndexOf(names, "Elixir"), 0);
            Assert.GreaterOrEqual(IndexOf(names, "Potion"), 0);
            Assert.GreaterOrEqual(IndexOf(names, "MissingResource"), 0);
            Assert.AreEqual(0, BlueprintResourceGraphToolkitTypes.GetResourceIds("TestDependencyType_B_Missing").Length);
        }

        [Test]
        public void ResourceGraphToolkitBridgeExportsInspectorConfiguredTypeReferenceAndDependency()
        {
            EnsureTempRoot();
            string graphPath = TempRoot + "/InspectorConfigured.resourcebpgraph";
            string jsonPath = TempRoot + "/InspectorConfigured.resourceblueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            AssetDatabase.DeleteAsset(jsonPath);

            BlueprintResourceVisualGraph graph = GraphDatabase.CreateGraph<BlueprintResourceVisualGraph>(graphPath);
            graph.SourceResourceBlueprintAssetPath = jsonPath;
            graph.SchemaVersion = "0.1";
            graph.ResourceType = "Prefab";
            graph.ResourceName = "InspectorConfigured";
            graph.MainAsset = new BlueprintResourceAssetReference
            {
                Guid = "export-guid",
                Path = "Assets/Missing/Export.asset",
                AssetType = nameof(BlueprintResourceTestAsset)
            };
            graph.Dependencies = new List<BlueprintResourceDependency>
            {
                new BlueprintResourceDependency
                {
                    ResourceType = "TestDependencyType_A",
                    ResourceName = "Potion",
                    Required = true,
                    PreloadGroup = "Inventory"
                }
            };
            BlueprintResourceGraphToolkitBlackboardSync.SyncGraphFieldsToBlackboard(graph);
            BlueprintGraphToolkitReflection.MarkDirty(graph);
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.ImportAsset(graphPath);

            BlueprintResourceGraphToolkitBridge.ExportGraphAtPath(graphPath, jsonPath);
            BlueprintResourceBlueprintSource exported = BlueprintResourceBlueprintSource.FromJson(File.ReadAllText(jsonPath));

            Assert.AreEqual("Prefab", exported.ResourceType);
            Assert.AreEqual("InspectorConfigured", exported.ResourceName);
            Assert.AreEqual("export-guid", exported.MainAsset.Guid);
            Assert.AreEqual("Assets/Missing/Export.asset", exported.MainAsset.Path);
            Assert.AreEqual(nameof(BlueprintResourceTestAsset), exported.MainAsset.AssetType);
            Assert.AreEqual(1, exported.Dependencies.Count);
            Assert.AreEqual("TestDependencyType_A", exported.Dependencies[0].ResourceType);
            Assert.AreEqual("Potion", exported.Dependencies[0].ResourceName);
            Assert.AreEqual("Inventory", exported.Dependencies[0].PreloadGroup);
        }

        [Test]
        public void ResourceGraphToolkitBlackboardSyncCreatesFixedVariables()
        {
            EnsureTempRoot();
            string graphPath = TempRoot + "/BlackboardCreated.resourcebpgraph";
            AssetDatabase.DeleteAsset(graphPath);

            BlueprintResourceVisualGraph graph = GraphDatabase.CreateGraph<BlueprintResourceVisualGraph>(graphPath);
            BlueprintResourceGraphToolkitBlackboardSync.EnsureResourceBlackboard(graph);

            Assert.True(BlueprintResourceGraphToolkitBlackboardSync.HasResourceBlackboardVariable(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceTypeVariableName));
            Assert.True(BlueprintResourceGraphToolkitBlackboardSync.HasResourceBlackboardVariable(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceNameVariableName));
            Assert.True(BlueprintResourceGraphToolkitBlackboardSync.HasResourceBlackboardVariable(graph, BlueprintResourceGraphToolkitBlackboardSync.DisplayNameVariableName));
            Assert.True(BlueprintResourceGraphToolkitBlackboardSync.HasResourceBlackboardVariable(graph, BlueprintResourceGraphToolkitBlackboardSync.MainAssetVariableName));
            AssertBlackboardVariableType(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceTypeVariableName, typeof(BlueprintResourceTypeReference));
            AssertBlackboardVariableType(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceNameVariableName, typeof(string));
            AssertBlackboardVariableType(graph, BlueprintResourceGraphToolkitBlackboardSync.DisplayNameVariableName, typeof(string));
            AssertBlackboardVariableType(graph, BlueprintResourceGraphToolkitBlackboardSync.MainAssetVariableName, typeof(BlueprintResourceAssetReference));
            Assert.False(BlueprintResourceGraphToolkitBlackboardSync.HasResourceBlackboardVariable(graph, BlueprintResourceGraphToolkitBlackboardSync.MainAssetPathVariableName));
            Assert.False(BlueprintResourceGraphToolkitBlackboardSync.HasResourceBlackboardVariable(graph, BlueprintResourceGraphToolkitBlackboardSync.MainAssetGuidVariableName));
            Assert.False(BlueprintResourceGraphToolkitBlackboardSync.HasResourceBlackboardVariable(graph, BlueprintResourceGraphToolkitBlackboardSync.MainAssetTypeVariableName));
        }

        [Test]
        public void ResourceGraphToolkitBlackboardSyncCopiesGraphFieldsToVariables()
        {
            EnsureTempRoot();
            string graphPath = TempRoot + "/BlackboardFromGraph.resourcebpgraph";
            AssetDatabase.DeleteAsset(graphPath);

            BlueprintResourceVisualGraph graph = GraphDatabase.CreateGraph<BlueprintResourceVisualGraph>(graphPath);
            graph.ResourceType = "Item";
            graph.ResourceName = "Potion";
            graph.DisplayName = "Potion Display";
            graph.MainAsset = new BlueprintResourceAssetReference
            {
                Path = "Assets/Game/Potion.prefab",
                Guid = "graph-guid",
                AssetType = "GameObject"
            };

            BlueprintResourceGraphToolkitBlackboardSync.SyncGraphFieldsToBlackboard(graph);

            AssertBlackboardValue(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceTypeVariableName, "Item");
            AssertBlackboardValue(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceNameVariableName, "Potion");
            AssertBlackboardValue(graph, BlueprintResourceGraphToolkitBlackboardSync.DisplayNameVariableName, "Potion Display");
            AssertBlackboardAssetReference(graph, "Assets/Game/Potion.prefab", "graph-guid", "GameObject");
        }

        [Test]
        public void ResourceGraphToolkitBridgeExportsCatalogResourceTypeAndMainAssetAsJson()
        {
            EnsureTempRoot();
            BlueprintResourceTypeCatalogAsset catalog = ScriptableObject.CreateInstance<BlueprintResourceTypeCatalogAsset>();
            catalog.ResourceTypes.Add(new BlueprintResourceTypeDefinition { ResourceType = "Weapon" });
            AssetDatabase.CreateAsset(catalog, TempRoot + "/ResourceTypeCatalog.asset");
            AssetDatabase.ImportAsset(TempRoot + "/ResourceTypeCatalog.asset");

            string graphPath = TempRoot + "/BlackboardExport.resourcebpgraph";
            string jsonPath = TempRoot + "/BlackboardExport.resourceblueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            AssetDatabase.DeleteAsset(jsonPath);

            BlueprintResourceVisualGraph graph = GraphDatabase.CreateGraph<BlueprintResourceVisualGraph>(graphPath);
            graph.SourceResourceBlueprintAssetPath = jsonPath;
            graph.ResourceType = "StaleType";
            graph.ResourceName = "StaleName";
            graph.DisplayName = "Stale Display";
            graph.MainAsset = new BlueprintResourceAssetReference
            {
                Path = "Assets/Stale.asset",
                Guid = "stale-guid",
                AssetType = "StaleType"
            };
            BlueprintResourceGraphToolkitBlackboardSync.EnsureResourceBlackboard(graph);
            BlueprintResourceGraphToolkitBlackboardSync.TrySetBlackboardValue(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceTypeVariableName, "Weapon");
            BlueprintResourceGraphToolkitBlackboardSync.TrySetBlackboardValue(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceNameVariableName, "Potion");
            BlueprintResourceGraphToolkitBlackboardSync.TrySetBlackboardValue(graph, BlueprintResourceGraphToolkitBlackboardSync.DisplayNameVariableName, "Potion Display");
            BlueprintResourceGraphToolkitBlackboardSync.TrySetBlackboardAssetReference(
                graph,
                new BlueprintResourceAssetReference
                {
                    Path = "Assets/Game/Potion.prefab",
                    Guid = "blackboard-guid",
                    AssetType = "GameObject"
                });
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.ImportAsset(graphPath);

            BlueprintResourceGraphToolkitBridge.ExportGraphAtPath(graphPath, jsonPath);
            BlueprintResourceBlueprintSource exported = BlueprintResourceBlueprintSource.FromJson(File.ReadAllText(jsonPath));

            Assert.AreEqual("Weapon", exported.ResourceType);
            Assert.AreEqual("Potion", exported.ResourceName);
            Assert.AreEqual("Potion Display", exported.DisplayName);
            Assert.AreEqual("Assets/Game/Potion.prefab", exported.MainAsset.Path);
            Assert.AreEqual("blackboard-guid", exported.MainAsset.Guid);
            Assert.AreEqual("GameObject", exported.MainAsset.AssetType);
        }

        [Test]
        public void ResourceGraphToolkitBridgeAutofillsResourceNameFromMainAssetWhenEmpty()
        {
            EnsureTempRoot();
            string assetPath = TempRoot + "/PotionAutoName.asset";
            BlueprintResourceTestAsset asset = ScriptableObject.CreateInstance<BlueprintResourceTestAsset>();
            asset.name = "PotionAutoName";
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.ImportAsset(assetPath);

            string graphPath = TempRoot + "/BlackboardAutoName.resourcebpgraph";
            string jsonPath = TempRoot + "/BlackboardAutoName.resourceblueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            AssetDatabase.DeleteAsset(jsonPath);

            BlueprintResourceAssetReference reference = new BlueprintResourceAssetReference();
            BlueprintResourceGraphToolkitTypes.ApplyAssetReference(reference, asset);

            BlueprintResourceVisualGraph graph = GraphDatabase.CreateGraph<BlueprintResourceVisualGraph>(graphPath);
            graph.SourceResourceBlueprintAssetPath = jsonPath;
            graph.ResourceType = "Item";
            graph.ResourceName = string.Empty;
            BlueprintResourceGraphToolkitBlackboardSync.EnsureResourceBlackboard(graph);
            BlueprintResourceGraphToolkitBlackboardSync.TrySetBlackboardAssetReference(graph, reference);
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.ImportAsset(graphPath);

            BlueprintResourceGraphToolkitBridge.ExportGraphAtPath(graphPath, jsonPath);
            BlueprintResourceBlueprintSource exported = BlueprintResourceBlueprintSource.FromJson(File.ReadAllText(jsonPath));

            Assert.AreEqual("PotionAutoName", exported.ResourceName);
            Assert.AreEqual(assetPath, exported.MainAsset.Path);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(assetPath), exported.MainAsset.Guid);
            Assert.AreEqual(nameof(BlueprintResourceTestAsset), exported.MainAsset.AssetType);
        }

        [Test]
        public void ResourceGraphToolkitBlackboardSyncMigratesLegacyMainAssetVariables()
        {
            EnsureTempRoot();
            string graphPath = TempRoot + "/LegacyMainAsset.resourcebpgraph";
            AssetDatabase.DeleteAsset(graphPath);

            BlueprintResourceVisualGraph graph = GraphDatabase.CreateGraph<BlueprintResourceVisualGraph>(graphPath);
            BlueprintResourceGraphToolkitReflection.CreateBlackboardVariable(
                graph,
                BlueprintResourceGraphToolkitBlackboardSync.ResourceTypeVariableName,
                typeof(string),
                "Audio");
            BlueprintResourceGraphToolkitReflection.CreateBlackboardVariable(
                graph,
                BlueprintResourceGraphToolkitBlackboardSync.MainAssetPathVariableName,
                typeof(string),
                "Assets/Game/Audio/Click.wav");
            BlueprintResourceGraphToolkitReflection.CreateBlackboardVariable(
                graph,
                BlueprintResourceGraphToolkitBlackboardSync.MainAssetGuidVariableName,
                typeof(string),
                "legacy-guid");
            BlueprintResourceGraphToolkitReflection.CreateBlackboardVariable(
                graph,
                BlueprintResourceGraphToolkitBlackboardSync.MainAssetTypeVariableName,
                typeof(string),
                "AudioClip");

            BlueprintResourceGraphToolkitBlackboardSync.EnsureResourceBlackboard(graph);

            AssertBlackboardValue(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceTypeVariableName, "Audio");
            AssertBlackboardVariableType(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceTypeVariableName, typeof(BlueprintResourceTypeReference));
            AssertBlackboardValue(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceNameVariableName, "Click");
            AssertBlackboardAssetReference(graph, "Assets/Game/Audio/Click.wav", "legacy-guid", "AudioClip");
            Assert.False(BlueprintResourceGraphToolkitBlackboardSync.HasResourceBlackboardVariable(graph, BlueprintResourceGraphToolkitBlackboardSync.MainAssetPathVariableName));
            Assert.False(BlueprintResourceGraphToolkitBlackboardSync.HasResourceBlackboardVariable(graph, BlueprintResourceGraphToolkitBlackboardSync.MainAssetGuidVariableName));
            Assert.False(BlueprintResourceGraphToolkitBlackboardSync.HasResourceBlackboardVariable(graph, BlueprintResourceGraphToolkitBlackboardSync.MainAssetTypeVariableName));
        }

        [Test]
        public void ResourceGraphToolkitBridgeFallsBackToGraphFieldWhenBlackboardVariableMissing()
        {
            EnsureTempRoot();
            string graphPath = TempRoot + "/BlackboardFallback.resourcebpgraph";
            string jsonPath = TempRoot + "/BlackboardFallback.resourceblueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            AssetDatabase.DeleteAsset(jsonPath);

            BlueprintResourceVisualGraph graph = GraphDatabase.CreateGraph<BlueprintResourceVisualGraph>(graphPath);
            graph.SourceResourceBlueprintAssetPath = jsonPath;
            graph.ResourceType = "Audio";
            graph.ResourceName = "GraphFallbackName";
            BlueprintResourceGraphToolkitBlackboardSync.SyncGraphFieldsToBlackboard(graph);
            BlueprintResourceGraphToolkitBlackboardSync.TrySetBlackboardValue(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceNameVariableName, "BlackboardName");
            DeleteBlackboardVariable(graph, BlueprintResourceGraphToolkitBlackboardSync.ResourceTypeVariableName);
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.ImportAsset(graphPath);

            BlueprintResourceGraphToolkitBridge.ExportGraphAtPath(graphPath, jsonPath);
            BlueprintResourceBlueprintSource exported = BlueprintResourceBlueprintSource.FromJson(File.ReadAllText(jsonPath));

            Assert.AreEqual("Audio", exported.ResourceType);
            Assert.AreEqual("BlackboardName", exported.ResourceName);
        }

        private void RestoreCanonicalResourceTypeCatalog()
        {
            if (_hadCanonicalCatalogBeforeTest)
            {
                BlueprintResourceTypeCatalogAsset catalog =
                    AssetDatabase.LoadAssetAtPath<BlueprintResourceTypeCatalogAsset>(
                        BlueprintResourceAssetManagerUtility.ResourceTypeCatalogAssetPath);
                if (catalog == null)
                {
                    catalog = BlueprintResourceAssetManagerUtility.GetOrCreateResourceTypeCatalogAsset();
                }

                catalog.ResourceTypes.Clear();
                catalog.ResourceTypes.AddRange(CloneResourceTypeDefinitions(_canonicalCatalogSnapshot));
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(BlueprintResourceAssetManagerUtility.ResourceTypeCatalogAssetPath);
            }
            else if (AssetDatabase.LoadAssetAtPath<BlueprintResourceTypeCatalogAsset>(
                         BlueprintResourceAssetManagerUtility.ResourceTypeCatalogAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(BlueprintResourceAssetManagerUtility.ResourceTypeCatalogAssetPath);
            }

            if (!_hadCanonicalResourceFolderBeforeTest && AssetDatabase.IsValidFolder(CanonicalResourceFolder))
            {
                AssetDatabase.DeleteAsset(CanonicalResourceFolder);
            }
        }

        private void RestoreCanonicalResourcePackagingPolicy()
        {
            if (_hadCanonicalPackagingPolicyBeforeTest)
            {
                BlueprintResourcePackagingPolicyAsset policy =
                    AssetDatabase.LoadAssetAtPath<BlueprintResourcePackagingPolicyAsset>(
                        BlueprintResourceAssetManagerUtility.PackagingPolicyAssetPath);
                if (policy == null)
                {
                    policy = BlueprintResourceAssetManagerUtility.GetOrCreateResourcePackagingPolicyAsset();
                }

                EditorJsonUtility.FromJsonOverwrite(_canonicalPackagingPolicySnapshot, policy);
                EditorUtility.SetDirty(policy);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(BlueprintResourceAssetManagerUtility.PackagingPolicyAssetPath);
            }
            else if (AssetDatabase.LoadAssetAtPath<BlueprintResourcePackagingPolicyAsset>(
                         BlueprintResourceAssetManagerUtility.PackagingPolicyAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(BlueprintResourceAssetManagerUtility.PackagingPolicyAssetPath);
            }
        }

        private static List<BlueprintResourceTypeDefinition> CloneResourceTypeDefinitions(
            BlueprintResourceTypeCatalogAsset catalog)
        {
            return catalog == null ? new List<BlueprintResourceTypeDefinition>() : CloneResourceTypeDefinitions(catalog.ResourceTypes);
        }

        private static List<BlueprintResourceTypeDefinition> CloneResourceTypeDefinitions(
            IEnumerable<BlueprintResourceTypeDefinition> definitions)
        {
            List<BlueprintResourceTypeDefinition> result = new List<BlueprintResourceTypeDefinition>();
            if (definitions == null)
            {
                return result;
            }

            foreach (BlueprintResourceTypeDefinition definition in definitions)
            {
                if (definition == null)
                {
                    continue;
                }

                BlueprintResourceTypeDefinition copy = new BlueprintResourceTypeDefinition
                {
                    ResourceType = definition.ResourceType
                };
                for (int i = 0; i < definition.Fields.Count; i++)
                {
                    BlueprintResourceTypeField field = definition.Fields[i];
                    if (field == null)
                    {
                        continue;
                    }

                    copy.Fields.Add(new BlueprintResourceTypeField
                    {
                        Name = field.Name,
                        Type = field.Type,
                        Required = field.Required,
                        DefaultValueJson = field.DefaultValueJson
                    });
                }

                result.Add(copy);
            }

            return result;
        }

        private static void EnsureTempRoot()
        {
            if (!AssetDatabase.IsValidFolder("Assets/BlueprintSystem/Tests/Editor/TempResourceSystem"))
            {
                AssetDatabase.CreateFolder("Assets/BlueprintSystem/Tests/Editor", "TempResourceSystem");
            }
        }

        private static BlueprintResourceAssetRecord FindRecord(BlueprintResourceAssetManagerReport report, string path)
        {
            for (int i = 0; i < report.Records.Count; i++)
            {
                if (report.Records[i].SourcePath == path)
                {
                    return report.Records[i];
                }
            }

            return null;
        }

        private static bool HasErrors(BlueprintResourceAssetRecord record)
        {
            for (int i = 0; i < record.Issues.Count; i++)
            {
                if (record.Issues[i].Severity == BlueprintResourceValidationSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasIssueContaining(BlueprintResourceAssetRecord record, string text)
        {
            if (record == null)
            {
                return false;
            }

            for (int i = 0; i < record.Issues.Count; i++)
            {
                if (record.Issues[i] != null &&
                    !string.IsNullOrEmpty(record.Issues[i].Message) &&
                    record.Issues[i].Message.Contains(text))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountResourceTypeCatalogAssets()
        {
            return BlueprintEditorAssetDiscovery.FindAssetPaths("t:BlueprintResourceTypeCatalogAsset").Count;
        }

        private static void WriteResourceSource(string path, string resourceType, string resourceName)
        {
            BlueprintResourceBlueprintSource source = new BlueprintResourceBlueprintSource();
            source.ResourceType = resourceType;
            source.ResourceName = resourceName;
            File.WriteAllText(path, source.ToJson());
            AssetDatabase.ImportAsset(path);
        }

        private static void WriteResourceSourceWithMainAsset(
            string path,
            string resourceType,
            string resourceName,
            string mainAssetPath)
        {
            BlueprintResourceBlueprintSource source = new BlueprintResourceBlueprintSource();
            source.ResourceType = resourceType;
            source.ResourceName = resourceName;
            source.MainAsset.Path = mainAssetPath;
            File.WriteAllText(path, source.ToJson());
            AssetDatabase.ImportAsset(path);
        }

        private static void ResetPackagingPolicy(BlueprintResourcePackagingPolicyAsset policy)
        {
            policy.DefaultRule.IncludeInBuild = true;
            policy.DefaultRule.ContentLocation = BlueprintResourceContentLocation.Base;
            policy.DefaultRule.DlcId = string.Empty;
            policy.DefaultRule.LoadPriority = 0;
            policy.Dlcs.Clear();
            policy.TypeRules.Clear();
            policy.ResourceOverrides.Clear();
        }

        private static string CreateTestAsset(string path)
        {
            BlueprintResourceTestAsset asset = ScriptableObject.CreateInstance<BlueprintResourceTestAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.ImportAsset(path);
            return path;
        }

        private static string CreateTestAssetWithReference(string path, UnityEngine.Object reference)
        {
            BlueprintResourceTestAsset asset = ScriptableObject.CreateInstance<BlueprintResourceTestAsset>();
            asset.Reference = reference;
            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            return path;
        }

        private static void AssertBlackboardValue(BlueprintResourceVisualGraph graph, string variableName, string expected)
        {
            string value;
            Assert.True(BlueprintResourceGraphToolkitBlackboardSync.TryGetBlackboardValue(graph, variableName, out value), "Missing blackboard variable: " + variableName);
            Assert.AreEqual(expected, value);
        }

        private static void AssertBlackboardVariableType(BlueprintResourceVisualGraph graph, string variableName, Type expectedType)
        {
            foreach (IVariable variable in graph.GetVariables())
            {
                if (variable != null && variable.name == variableName)
                {
                    Assert.AreEqual(expectedType, variable.dataType, "Unexpected blackboard variable type: " + variableName);
                    return;
                }
            }

            Assert.Fail("Missing blackboard variable: " + variableName);
        }

        private static void AssertBlackboardAssetReference(
            BlueprintResourceVisualGraph graph,
            string path,
            string guid,
            string assetType)
        {
            BlueprintResourceAssetReference reference;
            Assert.True(BlueprintResourceGraphToolkitBlackboardSync.TryGetBlackboardAssetReference(graph, out reference), "Missing blackboard variable: " + BlueprintResourceGraphToolkitBlackboardSync.MainAssetVariableName);
            Assert.AreEqual(path, reference.Path);
            Assert.AreEqual(guid, reference.Guid);
            Assert.AreEqual(assetType, reference.AssetType);
        }

        private static void DeleteBlackboardVariable(BlueprintResourceVisualGraph graph, string variableName)
        {
            IVariable variableToDelete = null;
            foreach (IVariable variable in graph.GetVariables())
            {
                if (variable != null && variable.name == variableName)
                {
                    variableToDelete = variable;
                    break;
                }
            }

            Assert.NotNull(variableToDelete, "Missing blackboard variable to delete: " + variableName);
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;
            System.Reflection.FieldInfo implementationField = typeof(Graph).GetField("m_Implementation", flags);
            object implementation = implementationField == null ? null : implementationField.GetValue(graph);
            Assert.NotNull(implementation);

            System.Reflection.MethodInfo deleteMethod = null;
            System.Reflection.MethodInfo[] methods = implementation.GetType().GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                System.Reflection.MethodInfo method = methods[i];
                System.Reflection.ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == "DeleteVariableDeclaration" && parameters.Length == 2)
                {
                    deleteMethod = method;
                    break;
                }
            }

            Assert.NotNull(deleteMethod);
            deleteMethod.Invoke(implementation, new object[] { variableToDelete, false });
            GraphDatabase.SaveGraphIfDirty(graph);
        }

        private static int IndexOf(string[] values, string value)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        private sealed class FakeResourceProvider : IBlueprintResourceLoadProvider
        {
            public int LoadCount;
            public FakeOperation LastOperation;
            public readonly List<FakeOperation> Operations = new List<FakeOperation>();

            public IBlueprintResourceLoadOperation LoadAsync(BlueprintResourceRegistryEntry entry)
            {
                LoadCount++;
                LastOperation = new FakeOperation();
                Operations.Add(LastOperation);
                return LastOperation;
            }
        }

        private sealed class FakeOperation : IBlueprintResourceLoadOperation
        {
            public int ReleaseCount;
            public bool IsDone { get; private set; }
            public float PercentComplete { get { return IsDone ? 1f : 0f; } }
            public UnityEngine.Object Result { get; private set; }
            public string Error { get; private set; }
            public event Action<IBlueprintResourceLoadOperation> Completed;

            public void Complete(UnityEngine.Object result, string error)
            {
                Result = result;
                Error = error;
                IsDone = true;
                if (Completed != null)
                {
                    Completed(this);
                }
            }

            public void Release()
            {
                ReleaseCount++;
            }
        }

    }

    public sealed class BlueprintResourceTestAsset : ScriptableObject
    {
        public UnityEngine.Object Reference;
    }
}
