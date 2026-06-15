using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    public enum BlueprintResourceValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class BlueprintResourceValidationIssue
    {
        public BlueprintResourceValidationSeverity Severity;
        public string SourcePath;
        public string ResourceId;
        public string Message;
    }

    public sealed class BlueprintResourceAssetRecord
    {
        public string SourcePath;
        public string SourceGuid;
        public string SourceHash;
        public BlueprintResourceBlueprintSource Source;
        public BlueprintResourceResolvedPackaging Packaging;
        public readonly List<BlueprintResourceValidationIssue> Issues = new List<BlueprintResourceValidationIssue>();
    }

    public sealed class BlueprintResourceAssetManagerReport
    {
        public readonly List<BlueprintResourceAssetRecord> Records = new List<BlueprintResourceAssetRecord>();
        public readonly List<BlueprintResourceValidationIssue> Issues = new List<BlueprintResourceValidationIssue>();
        public readonly List<BlueprintResourceSharedDependencyCandidate> SharedDependencyCandidates =
            new List<BlueprintResourceSharedDependencyCandidate>();
        public BlueprintResourcePackagingPolicyAsset PackagingPolicy;
        public string GeneratedHash;

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i] != null && Issues[i].Severity == BlueprintResourceValidationSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    public static class BlueprintResourceAssetManagerUtility
    {
        public const string RegistryAssetPath = "Assets/Resources/BlueprintResourceRegistry.asset";
        public const string ResourceTypeCatalogAssetPath = "Assets/BlueprintSystem/Resources/BlueprintResourceTypeCatalog.asset";
        public const string PackagingPolicyAssetPath = BlueprintResourcePackagingUtility.PackagingPolicyAssetPath;
        private const string RegistryResourceFolder = "Assets/Resources";
        private const string BlueprintSystemResourceFolder = "Assets/BlueprintSystem/Resources";
        private const string AddressableLabel = "ResourceBlueprint";
        private static bool _syncing;

        [MenuItem("Tools/Blueprint System/Resource Asset Manager/Sync All")]
        public static void SyncAllMenu()
        {
            BlueprintResourceAssetManagerReport report = SyncAll(true);
            LogReport(report);
        }

        [MenuItem("Tools/Blueprint System/Resource Asset Manager/Validate")]
        public static void ValidateMenu()
        {
            BlueprintResourceAssetManagerReport report = ScanProject(true);
            LogReport(report);
        }

        public static BlueprintResourceAssetManagerReport SyncAll(bool log)
        {
            if (_syncing)
            {
                return ScanProject(false);
            }

            _syncing = true;
            try
            {
                GetOrCreateResourcePackagingPolicyAsset();
                BlueprintResourceAssetManagerReport report = ScanProject(false);
                if (!report.HasErrors)
                {
                    RegisterResourceTypes(report);
                    NormalizeAndWriteSources(report);
                    report = ScanProject(false);
                    SyncAddressables(report);
                    report = ScanProject(true);
                    if (!report.HasErrors)
                    {
                        WriteRegistry(report);
                    }
                }

                if (log)
                {
                    LogReport(report);
                }

                return report;
            }
            finally
            {
                _syncing = false;
            }
        }

        public static BlueprintResourcePackagingPolicyAsset GetOrCreateResourcePackagingPolicyAsset()
        {
            return BlueprintResourcePackagingUtility.GetOrCreatePolicyAsset();
        }

        public static BlueprintResourcePackagingPolicyAsset LoadResourcePackagingPolicyAsset()
        {
            return BlueprintResourcePackagingUtility.LoadPolicyAsset();
        }

        public static List<BlueprintResourceSharedDependencyCandidate> ScanSharedDependencies(
            BlueprintResourceAssetManagerReport report)
        {
            return BlueprintResourcePackagingUtility.ScanSharedDependencies(report);
        }

        public static List<BlueprintResourceSharedDependencyCandidate> ExtractSharedDependencies(
            BlueprintResourceAssetManagerReport report)
        {
            return BlueprintResourcePackagingUtility.ExtractSharedDependencies(report);
        }

        public static BlueprintResourceTypeCatalogAsset GetOrCreateResourceTypeCatalogAsset()
        {
            BlueprintResourceTypeCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<BlueprintResourceTypeCatalogAsset>(ResourceTypeCatalogAssetPath);
            if (catalog != null)
            {
                return catalog;
            }

            EnsureFolder(BlueprintSystemResourceFolder);
            catalog = ScriptableObject.CreateInstance<BlueprintResourceTypeCatalogAsset>();
            AssetDatabase.CreateAsset(catalog, ResourceTypeCatalogAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ResourceTypeCatalogAssetPath);
            return AssetDatabase.LoadAssetAtPath<BlueprintResourceTypeCatalogAsset>(ResourceTypeCatalogAssetPath);
        }

        public static bool RegisterResourceType(string resourceType)
        {
            if (string.IsNullOrEmpty(NormalizeResourceTypeName(resourceType)))
            {
                return false;
            }

            return RegisterResourceType(GetOrCreateResourceTypeCatalogAsset(), resourceType, true);
        }

        internal static bool RegisterResourceType(BlueprintResourceTypeCatalogAsset catalog, string resourceType, bool saveAssets)
        {
            string normalized = NormalizeResourceTypeName(resourceType);
            if (catalog == null || string.IsNullOrEmpty(normalized) || catalog.ResourceTypes == null)
            {
                return false;
            }

            for (int i = 0; i < catalog.ResourceTypes.Count; i++)
            {
                BlueprintResourceTypeDefinition existing = catalog.ResourceTypes[i];
                if (existing != null &&
                    string.Equals(NormalizeResourceTypeName(existing.ResourceType), normalized, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            catalog.ResourceTypes.Add(new BlueprintResourceTypeDefinition { ResourceType = normalized });
            catalog.ResourceTypes.Sort(CompareResourceTypeDefinitions);
            EditorUtility.SetDirty(catalog);
            if (saveAssets)
            {
                AssetDatabase.SaveAssets();
                string catalogPath = AssetDatabase.GetAssetPath(catalog);
                if (!string.IsNullOrEmpty(catalogPath))
                {
                    AssetDatabase.ImportAsset(catalogPath);
                }
            }

            return true;
        }

        public static List<string> FindResourceTypeCatalogAssetPaths()
        {
            List<string> paths = BlueprintEditorAssetDiscovery.FindAssetPaths("t:BlueprintResourceTypeCatalogAsset");
            AddResourceTypeCatalogAssetPath(paths, ResourceTypeCatalogAssetPath);
            return paths;
        }

        public static BlueprintResourceAssetManagerReport ScanProject(bool validateAddressables)
        {
            BlueprintResourceAssetManagerReport report = new BlueprintResourceAssetManagerReport();
            report.PackagingPolicy = LoadResourcePackagingPolicyAsset();
            Dictionary<string, BlueprintResourceAssetRecord> byId = new Dictionary<string, BlueprintResourceAssetRecord>(StringComparer.Ordinal);
            Dictionary<string, BlueprintResourceTypeDefinition> typeDefinitions = LoadTypeDefinitions();
            List<string> paths = BlueprintEditorAssetDiscovery.FindTextAssetPaths(BlueprintResourceBlueprintSource.AssetExtension);
            for (int i = 0; i < paths.Count; i++)
            {
                BlueprintResourceAssetRecord record = LoadRecord(paths[i]);
                report.Records.Add(record);
                if (record.Source == null)
                {
                    AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Resource Blueprint JSON could not be parsed.");
                    continue;
                }

                record.Packaging = BlueprintResourcePackagingUtility.Resolve(record.Source, report.PackagingPolicy);
                ValidateRecord(report, record, report.PackagingPolicy, typeDefinitions, validateAddressables);
                string id = record.Source.Id.ToString();
                if (record.Source.Id.IsValid)
                {
                    BlueprintResourceAssetRecord duplicate;
                    if (byId.TryGetValue(id, out duplicate))
                    {
                        AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Duplicate resource id also used by " + duplicate.SourcePath + ".");
                    }
                    else
                    {
                        byId[id] = record;
                    }
                }
            }

            ValidateDependencies(report, byId);
            report.GeneratedHash = ComputeAggregateHash(report);
            return report;
        }

        public static BlueprintResourceRegistryEntry ToRegistryEntry(BlueprintResourceAssetRecord record)
        {
            BlueprintResourceBlueprintSource source = record.Source;
            BlueprintResourceResolvedPackaging packaging = record.Packaging;
            BlueprintResourceRegistryEntry entry = new BlueprintResourceRegistryEntry();
            entry.ResourceType = source.ResourceType;
            entry.ResourceName = source.ResourceName;
            entry.DisplayName = source.DisplayName;
            entry.Description = source.Description;
            entry.SourcePath = record.SourcePath;
            entry.SourceGuid = record.SourceGuid;
            entry.SourceHash = record.SourceHash;
            entry.MainAssetGuid = source.MainAsset == null ? null : source.MainAsset.Guid;
            entry.MainAssetPath = source.MainAsset == null ? null : source.MainAsset.Path;
            entry.MainAssetAddress = source.MainAsset == null ? null : source.MainAsset.Address;
            entry.MainAssetType = source.MainAsset == null ? null : source.MainAsset.AssetType;
            entry.Tags = source.Tags.ToArray();
            entry.Dependencies = source.Dependencies.ToArray();
            entry.PreloadGroups = source.PreloadGroups.ToArray();
            entry.Priority = packaging == null ? source.Priority : packaging.LoadPriority;
            entry.MemoryBudgetMb = Mathf.Max(0f, source.MemoryBudgetMb);
            entry.RemoteCatalog = source.RemoteCatalog;
            entry.ContentVersion = source.ContentVersion;
            entry.MetadataJson = BuildMetadataJson(source);
            return entry;
        }

        private static BlueprintResourceAssetRecord LoadRecord(string path)
        {
            BlueprintResourceAssetRecord record = new BlueprintResourceAssetRecord();
            record.SourcePath = BlueprintAssetDiscovery.NormalizeAssetPath(path);
            record.SourceGuid = AssetDatabase.AssetPathToGUID(record.SourcePath);
            try
            {
                TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(record.SourcePath);
                string text = textAsset == null ? File.ReadAllText(record.SourcePath) : textAsset.text;
                record.SourceHash = ComputeHash(text);
                record.Source = BlueprintResourceBlueprintSource.FromJson(text);
            }
            catch (Exception exception)
            {
                record.SourceHash = string.Empty;
                record.Source = null;
                record.Issues.Add(new BlueprintResourceValidationIssue
                {
                    Severity = BlueprintResourceValidationSeverity.Error,
                    SourcePath = record.SourcePath,
                    Message = exception.Message
                });
            }

            return record;
        }

        private static Dictionary<string, BlueprintResourceTypeDefinition> LoadTypeDefinitions()
        {
            Dictionary<string, BlueprintResourceTypeDefinition> result =
                new Dictionary<string, BlueprintResourceTypeDefinition>(StringComparer.Ordinal);
            List<string> catalogPaths = FindResourceTypeCatalogAssetPaths();
            for (int i = 0; i < catalogPaths.Count; i++)
            {
                BlueprintResourceTypeCatalogAsset catalog =
                    AssetDatabase.LoadAssetAtPath<BlueprintResourceTypeCatalogAsset>(catalogPaths[i]);
                if (catalog == null || catalog.ResourceTypes == null)
                {
                    continue;
                }

                for (int typeIndex = 0; typeIndex < catalog.ResourceTypes.Count; typeIndex++)
                {
                    AddTypeDefinition(result, catalog.ResourceTypes[typeIndex], true);
                }
            }

            return result;
        }

        private static void RegisterResourceTypes(BlueprintResourceAssetManagerReport report)
        {
            if (report == null || report.Records.Count == 0)
            {
                return;
            }

            BlueprintResourceTypeCatalogAsset catalog = GetOrCreateResourceTypeCatalogAsset();
            bool changed = false;
            for (int i = 0; i < report.Records.Count; i++)
            {
                BlueprintResourceAssetRecord record = report.Records[i];
                if (record != null && record.Source != null)
                {
                    changed |= RegisterResourceType(catalog, record.Source.ResourceType, false);
                }
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(ResourceTypeCatalogAssetPath);
            }
        }

        private static void AddResourceTypeCatalogAssetPath(List<string> paths, string path)
        {
            if (paths == null)
            {
                return;
            }

            string normalized = BlueprintAssetDiscovery.NormalizeAssetPath(path);
            if (string.IsNullOrEmpty(normalized) ||
                AssetDatabase.LoadAssetAtPath<BlueprintResourceTypeCatalogAsset>(normalized) == null)
            {
                return;
            }

            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(BlueprintAssetDiscovery.NormalizeAssetPath(paths[i]), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            paths.Add(normalized);
            paths.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static void AddTypeDefinition(
            Dictionary<string, BlueprintResourceTypeDefinition> result,
            BlueprintResourceTypeDefinition definition,
            bool replaceExisting)
        {
            if (result == null || definition == null || string.IsNullOrEmpty(definition.ResourceType))
            {
                return;
            }

            if (!replaceExisting && result.ContainsKey(definition.ResourceType))
            {
                return;
            }

            result[definition.ResourceType] = definition;
        }

        private static string NormalizeResourceTypeName(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        }

        private static int CompareResourceTypeDefinitions(
            BlueprintResourceTypeDefinition left,
            BlueprintResourceTypeDefinition right)
        {
            return string.CompareOrdinal(
                left == null ? string.Empty : NormalizeResourceTypeName(left.ResourceType),
                right == null ? string.Empty : NormalizeResourceTypeName(right.ResourceType));
        }

        private static void ValidateRecord(
            BlueprintResourceAssetManagerReport report,
            BlueprintResourceAssetRecord record,
            BlueprintResourcePackagingPolicyAsset policy,
            Dictionary<string, BlueprintResourceTypeDefinition> typeDefinitions,
            bool validateAddressables)
        {
            BlueprintResourceBlueprintSource source = record.Source;
            if (!source.Id.IsValid)
            {
                AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Resource id must include resourceType and resourceName.");
            }

            if (source.MemoryBudgetMb < 0f)
            {
                AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "memoryBudgetMb cannot be negative.");
            }

            if (source.MainAsset == null || string.IsNullOrEmpty(source.MainAsset.Path) && string.IsNullOrEmpty(source.MainAsset.Guid))
            {
                AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Resource Blueprint must reference a main asset.");
            }
            else
            {
                string assetPath = ResolveMainAssetPath(source.MainAsset);
                UnityEngine.Object asset = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset == null)
                {
                    AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Main asset cannot be loaded.");
                }
                else if (!IsAllowedMainAssetType(asset))
                {
                    AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Main asset type '" + asset.GetType().Name + "' is not supported by V1 resource validation.");
                }

                if (validateAddressables && record.Packaging != null && record.Packaging.IncludeInBuild)
                {
                    ValidateAddressableEntry(report, record, assetPath);
                }
            }

            ValidatePackaging(report, record, policy);

            BlueprintResourceTypeDefinition typeDefinition;
            if (typeDefinitions.TryGetValue(source.ResourceType ?? string.Empty, out typeDefinition))
            {
                ValidateMetadata(report, record, typeDefinition);
            }
        }

        private static void ValidatePackaging(
            BlueprintResourceAssetManagerReport report,
            BlueprintResourceAssetRecord record,
            BlueprintResourcePackagingPolicyAsset policy)
        {
            BlueprintResourceResolvedPackaging packaging = record.Packaging;
            if (packaging == null || !packaging.IncludeInBuild)
            {
                return;
            }

            if (packaging.ContentLocation != BlueprintResourceContentLocation.DLC)
            {
                return;
            }

            if (string.IsNullOrEmpty(packaging.DlcId))
            {
                AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "DLC resource packaging requires a dlcId.");
                return;
            }

            BlueprintResourceDlcDefinition dlc = policy == null ? null : policy.FindDlc(packaging.DlcId);
            if (dlc == null)
            {
                AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "DLC id '" + packaging.DlcId + "' is not defined in the Resource Packaging Policy.");
                return;
            }

            if (!dlc.IncludeInBuild)
            {
                AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "DLC id '" + packaging.DlcId + "' is disabled in the Resource Packaging Policy.");
            }
        }

        private static void ValidateMetadata(
            BlueprintResourceAssetManagerReport report,
            BlueprintResourceAssetRecord record,
            BlueprintResourceTypeDefinition typeDefinition)
        {
            Dictionary<string, object> metadata = ReadMetadata(record.Source);
            for (int i = 0; i < typeDefinition.Fields.Count; i++)
            {
                BlueprintResourceTypeField field = typeDefinition.Fields[i];
                if (field == null || string.IsNullOrEmpty(field.Name))
                {
                    continue;
                }

                object value;
                if (!metadata.TryGetValue(field.Name, out value) || value == null)
                {
                    if (field.Required)
                    {
                        AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Required metadata field '" + field.Name + "' is missing.");
                    }

                    continue;
                }

                if (!string.IsNullOrEmpty(field.Type) && BlueprintVariableTypeRegistry.IsKnownType(field.Type) &&
                    !BlueprintTypeUtility.IsValueAssignableToType(value, field.Type))
                {
                    AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Metadata field '" + field.Name + "' is not assignable to " + field.Type + ".");
                }
            }
        }

        private static void ValidateDependencies(
            BlueprintResourceAssetManagerReport report,
            Dictionary<string, BlueprintResourceAssetRecord> byId)
        {
            foreach (KeyValuePair<string, BlueprintResourceAssetRecord> pair in byId)
            {
                BlueprintResourceAssetRecord record = pair.Value;
                if (record == null || record.Source == null)
                {
                    continue;
                }

                if (record.Packaging != null && !record.Packaging.IncludeInBuild)
                {
                    continue;
                }

                for (int i = 0; i < record.Source.Dependencies.Count; i++)
                {
                    BlueprintResourceDependency dependency = record.Source.Dependencies[i];
                    if (dependency == null || !dependency.ToId().IsValid)
                    {
                        AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Dependency must include resourceType and resourceName.");
                        continue;
                    }

                    if (dependency.Required && !byId.ContainsKey(dependency.ToId().ToString()))
                    {
                        AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Required dependency '" + dependency.ToId() + "' is missing.");
                    }

                    BlueprintResourceAssetRecord dependencyRecord;
                    if (dependency.Required &&
                        byId.TryGetValue(dependency.ToId().ToString(), out dependencyRecord) &&
                        dependencyRecord != null &&
                        dependencyRecord.Packaging != null &&
                        !dependencyRecord.Packaging.IncludeInBuild)
                    {
                        AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Required dependency '" + dependency.ToId() + "' is excluded by resource packaging.");
                    }
                }
            }

            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, BlueprintResourceAssetRecord> pair in byId)
            {
                DetectCycle(report, pair.Value, byId, visiting, visited);
            }
        }

        private static bool DetectCycle(
            BlueprintResourceAssetManagerReport report,
            BlueprintResourceAssetRecord record,
            Dictionary<string, BlueprintResourceAssetRecord> byId,
            HashSet<string> visiting,
            HashSet<string> visited)
        {
            if (record == null || record.Source == null || !record.Source.Id.IsValid)
            {
                return false;
            }

            string id = record.Source.Id.ToString();
            if (visited.Contains(id))
            {
                return false;
            }

            if (visiting.Contains(id))
            {
                AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Dependency cycle includes '" + id + "'.");
                return true;
            }

            visiting.Add(id);
            for (int i = 0; i < record.Source.Dependencies.Count; i++)
            {
                BlueprintResourceDependency dependency = record.Source.Dependencies[i];
                BlueprintResourceAssetRecord dependencyRecord;
                if (dependency != null && byId.TryGetValue(dependency.ToId().ToString(), out dependencyRecord))
                {
                    DetectCycle(report, dependencyRecord, byId, visiting, visited);
                }
            }

            visiting.Remove(id);
            visited.Add(id);
            return false;
        }

        private static void ValidateAddressableEntry(
            BlueprintResourceAssetManagerReport report,
            BlueprintResourceAssetRecord record,
            string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            AddressableAssetEntry entry = settings == null || string.IsNullOrEmpty(guid)
                ? null
                : settings.FindAssetEntry(guid, true);
            if (entry == null)
            {
                AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Main asset is not Addressable. Run Resource Asset Manager Sync All.");
                return;
            }

            string expectedAddress = CreateAddress(record.Source);
            if (entry.address != expectedAddress)
            {
                AddIssue(report, record, BlueprintResourceValidationSeverity.Error, "Addressables address is '" + entry.address + "' but expected '" + expectedAddress + "'.");
            }
        }

        private static void NormalizeAndWriteSources(BlueprintResourceAssetManagerReport report)
        {
            for (int i = 0; i < report.Records.Count; i++)
            {
                BlueprintResourceAssetRecord record = report.Records[i];
                if (record == null || record.Source == null)
                {
                    continue;
                }

                bool changed = NormalizeSource(record.Source);
                if (!changed)
                {
                    continue;
                }

                File.WriteAllText(record.SourcePath, record.Source.ToJson());
                AssetDatabase.ImportAsset(record.SourcePath);
            }
        }

        private static bool NormalizeSource(BlueprintResourceBlueprintSource source)
        {
            bool changed = false;
            if (string.IsNullOrEmpty(source.SchemaVersion))
            {
                source.SchemaVersion = BlueprintResourceBlueprintSource.SchemaVersionValue;
                changed = true;
            }

            if (source.MainAsset == null)
            {
                source.MainAsset = new BlueprintResourceAssetReference();
                changed = true;
            }

            string path = ResolveMainAssetPath(source.MainAsset);
            if (!string.IsNullOrEmpty(path))
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                string address = CreateAddress(source);
                if (source.MainAsset.Path != path)
                {
                    source.MainAsset.Path = path;
                    changed = true;
                }

                if (source.MainAsset.Guid != guid)
                {
                    source.MainAsset.Guid = guid;
                    changed = true;
                }

                if (source.MainAsset.Address != address)
                {
                    source.MainAsset.Address = address;
                    changed = true;
                }

                string assetType = asset == null ? null : asset.GetType().Name;
                if (source.MainAsset.AssetType != assetType)
                {
                    source.MainAsset.AssetType = assetType;
                    changed = true;
                }
            }

            return changed;
        }

        private static void SyncAddressables(BlueprintResourceAssetManagerReport report)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                return;
            }

            EnsureLabel(settings, AddressableLabel);
            EnsureLabel(settings, BlueprintResourcePackagingUtility.ResourceContentBaseLabel);
            EnsureLabel(settings, BlueprintResourcePackagingUtility.ResourceContentDlcLabel);
            for (int i = 0; i < report.Records.Count; i++)
            {
                BlueprintResourceAssetRecord record = report.Records[i];
                if (record == null || record.Source == null || !record.Source.Id.IsValid || record.Source.MainAsset == null)
                {
                    continue;
                }

                string path = ResolveMainAssetPath(record.Source.MainAsset);
                string guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                if (record.Packaging != null && !record.Packaging.IncludeInBuild)
                {
                    AddressableAssetEntry existing = settings.FindAssetEntry(guid, true);
                    if (BlueprintResourcePackagingUtility.IsBlueprintManagedEntry(existing))
                    {
                        settings.RemoveAssetEntry(guid, false);
                    }

                    continue;
                }

                BlueprintResourceResolvedPackaging packaging = record.Packaging ??
                    BlueprintResourcePackagingUtility.Resolve(record.Source, report.PackagingPolicy);
                AddressableAssetGroup group = EnsureGroup(settings, packaging.GroupName);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = CreateAddress(record.Source);
                BlueprintResourcePackagingUtility.ClearBlueprintPackagingLabels(entry);
                SetLabel(entry, settings, AddressableLabel);
                SetLabel(entry, settings, "Resource." + SanitizeLabelPart(record.Source.ResourceType));
                SetLabel(entry, settings, packaging.ContentLabel);
                if (!string.IsNullOrEmpty(packaging.DlcLabel))
                {
                    SetLabel(entry, settings, packaging.DlcLabel);
                }

                for (int t = 0; t < record.Source.Tags.Count; t++)
                {
                    if (!string.IsNullOrEmpty(record.Source.Tags[t]))
                    {
                        SetLabel(entry, settings, "ResourceTag." + SanitizeLabelPart(record.Source.Tags[t]));
                    }
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group != null)
            {
                return group;
            }

            return settings.CreateGroup(
                groupName,
                false,
                false,
                false,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
        }

        private static void SetLabel(AddressableAssetEntry entry, AddressableAssetSettings settings, string label)
        {
            EnsureLabel(settings, label);
            entry.SetLabel(label, true, true);
        }

        private static void EnsureLabel(AddressableAssetSettings settings, string label)
        {
            if (settings != null && !string.IsNullOrEmpty(label))
            {
                settings.AddLabel(label, false);
            }
        }

        private static void WriteRegistry(BlueprintResourceAssetManagerReport report)
        {
            if (!AssetDatabase.IsValidFolder(RegistryResourceFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            BlueprintResourceRegistryAsset registry =
                AssetDatabase.LoadAssetAtPath<BlueprintResourceRegistryAsset>(RegistryAssetPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<BlueprintResourceRegistryAsset>();
                AssetDatabase.CreateAsset(registry, RegistryAssetPath);
            }

            List<BlueprintResourceRegistryEntry> entries = new List<BlueprintResourceRegistryEntry>();
            for (int i = 0; i < report.Records.Count; i++)
            {
                BlueprintResourceAssetRecord record = report.Records[i];
                if (record != null &&
                    record.Source != null &&
                    record.Source.Id.IsValid &&
                    (record.Packaging == null || record.Packaging.IncludeInBuild))
                {
                    entries.Add(ToRegistryEntry(record));
                }
            }

            entries.Sort(delegate(BlueprintResourceRegistryEntry left, BlueprintResourceRegistryEntry right)
            {
                return string.CompareOrdinal(left.Id.ToString(), right.Id.ToString());
            });
            registry.SetGeneratedData("0.1", report.GeneratedHash, entries, 4, 512f);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts.Length == 0 ? string.Empty : parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string ResolveMainAssetPath(BlueprintResourceAssetReference reference)
        {
            if (reference == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(reference.Guid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(reference.Guid);
                if (!string.IsNullOrEmpty(guidPath))
                {
                    return BlueprintAssetDiscovery.NormalizeAssetPath(guidPath);
                }
            }

            return BlueprintAssetDiscovery.NormalizeAssetPath(reference.Path);
        }

        private static bool IsAllowedMainAssetType(UnityEngine.Object asset)
        {
            return asset is GameObject ||
                   asset is Sprite ||
                   asset is AudioClip ||
                   asset is Material ||
                   asset is Texture ||
                   asset is SceneAsset ||
                   asset != null && asset.GetType().IsSubclassOf(typeof(UnityEngine.Object));
        }

        private static string CreateAddress(BlueprintResourceBlueprintSource source)
        {
            return BlueprintResourcePackagingUtility.CreateResourceAddress(source.ResourceType, source.ResourceName);
        }

        private static string SanitizeLabelPart(string value)
        {
            return BlueprintResourcePackagingUtility.SanitizeLabelPart(value);
        }

        private static Dictionary<string, object> ReadMetadata(BlueprintResourceBlueprintSource source)
        {
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (source == null)
            {
                return result;
            }

            for (int i = 0; i < source.Metadata.Count; i++)
            {
                BlueprintResourceMetadataField field = source.Metadata[i];
                if (field == null || string.IsNullOrEmpty(field.Name))
                {
                    continue;
                }

                try
                {
                    result[field.Name] = string.IsNullOrEmpty(field.ValueJson)
                        ? null
                        : BlueprintJson.Deserialize(field.ValueJson);
                }
                catch (BlueprintJsonException)
                {
                    result[field.Name] = null;
                }
            }

            return result;
        }

        private static string BuildMetadataJson(BlueprintResourceBlueprintSource source)
        {
            return BlueprintJson.Serialize(ReadMetadata(source), true);
        }

        private static void AddIssue(
            BlueprintResourceAssetManagerReport report,
            BlueprintResourceAssetRecord record,
            BlueprintResourceValidationSeverity severity,
            string message)
        {
            BlueprintResourceValidationIssue issue = new BlueprintResourceValidationIssue
            {
                Severity = severity,
                SourcePath = record == null ? null : record.SourcePath,
                ResourceId = record == null || record.Source == null ? null : record.Source.Id.ToString(),
                Message = message
            };
            report.Issues.Add(issue);
            if (record != null)
            {
                record.Issues.Add(issue);
            }
        }

        private static string ComputeAggregateHash(BlueprintResourceAssetManagerReport report)
        {
            StringBuilder builder = new StringBuilder();
            List<BlueprintResourceAssetRecord> records = report == null
                ? null
                : report.Records;
            for (int i = 0; records != null && i < records.Count; i++)
            {
                BlueprintResourceAssetRecord record = records[i];
                if (record == null)
                {
                    continue;
                }

                builder.Append(record.SourcePath).Append(':').Append(record.SourceHash).Append('\n');
            }

            builder.Append("packaging:").Append(BlueprintResourcePackagingUtility.ComputePolicyHash(report == null ? null : report.PackagingPolicy));
            return ComputeHash(builder.ToString());
        }

        private static string ComputeHash(string text)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static void LogReport(BlueprintResourceAssetManagerReport report)
        {
            if (report == null)
            {
                return;
            }

            if (report.Issues.Count == 0)
            {
                Debug.Log("[Blueprint Resource] Validated " + report.Records.Count + " resource blueprint(s).");
                return;
            }

            for (int i = 0; i < report.Issues.Count; i++)
            {
                BlueprintResourceValidationIssue issue = report.Issues[i];
                string message = "[Blueprint Resource] " + issue.Severity + " " + issue.ResourceId + " " + issue.SourcePath + ": " + issue.Message;
                if (issue.Severity == BlueprintResourceValidationSeverity.Error)
                {
                    Debug.LogError(message);
                }
                else if (issue.Severity == BlueprintResourceValidationSeverity.Warning)
                {
                    Debug.LogWarning(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }
    }

    internal sealed class BlueprintResourceAssetPostprocessor : AssetPostprocessor
    {
        private static bool _scheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (TouchesResourceBlueprint(importedAssets) ||
                TouchesResourceBlueprint(deletedAssets) ||
                TouchesResourceBlueprint(movedAssets) ||
                TouchesResourceBlueprint(movedFromAssetPaths))
            {
                ScheduleSync();
            }
        }

        private static bool TouchesResourceBlueprint(string[] paths)
        {
            if (paths == null)
            {
                return false;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (path.EndsWith(BlueprintResourceBlueprintSource.AssetExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(
                        BlueprintAssetDiscovery.NormalizeAssetPath(path),
                        BlueprintResourceAssetManagerUtility.ResourceTypeCatalogAssetPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(
                        BlueprintAssetDiscovery.NormalizeAssetPath(path),
                        BlueprintResourceAssetManagerUtility.PackagingPolicyAssetPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
                    (AssetDatabase.LoadAssetAtPath<BlueprintResourceTypeCatalogAsset>(path) != null ||
                     AssetDatabase.LoadAssetAtPath<BlueprintResourcePackagingPolicyAsset>(path) != null))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ScheduleSync()
        {
            if (_scheduled)
            {
                return;
            }

            _scheduled = true;
            EditorApplication.delayCall += delegate
            {
                _scheduled = false;
                BlueprintResourceAssetManagerUtility.SyncAll(false);
            };
        }
    }

    internal sealed class BlueprintResourceBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder
        {
            get { return 10; }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            BlueprintResourceAssetManagerReport scan = BlueprintResourceAssetManagerUtility.ScanProject(true);
            if (scan.Records.Count == 0)
            {
                return;
            }

            BlueprintResourceRegistryAsset registry =
                AssetDatabase.LoadAssetAtPath<BlueprintResourceRegistryAsset>(BlueprintResourceAssetManagerUtility.RegistryAssetPath);
            if (registry == null)
            {
                throw new BuildFailedException("[Blueprint Resource] Missing generated resource registry. Run Tools/Blueprint System/Resource Asset Manager/Sync All.");
            }

            if (registry.GeneratedHash != scan.GeneratedHash)
            {
                throw new BuildFailedException("[Blueprint Resource] Generated resource registry is stale. Run Resource Asset Manager Sync All.");
            }

            if (scan.HasErrors)
            {
                List<string> errors = new List<string>();
                for (int i = 0; i < scan.Issues.Count; i++)
                {
                    BlueprintResourceValidationIssue issue = scan.Issues[i];
                    if (issue != null && issue.Severity == BlueprintResourceValidationSeverity.Error)
                    {
                        errors.Add(issue.ResourceId + ": " + issue.Message);
                    }
                }

                throw new BuildFailedException("[Blueprint Resource] Resource Blueprint validation failed: " + string.Join("; ", errors.ToArray()));
            }
        }
    }
}
