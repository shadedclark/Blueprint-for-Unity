using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace BlueprintSystem.Editor
{
    public sealed class BlueprintResourceResolvedPackaging
    {
        public bool IncludeInBuild;
        public BlueprintResourceContentLocation ContentLocation;
        public string DlcId;
        public string DlcDisplayName;
        public int LoadPriority;
        public string RuleSource;
        public string GroupName;
        public string Address;
        public string ContentLabel;
        public string DlcLabel;
    }

    public sealed class BlueprintResourceSharedDependencyCandidate
    {
        public string AssetPath;
        public string AssetGuid;
        public string Address;
        public string SharedGroupName;
        public string Warning;
        public readonly List<string> OwnerResourceIds = new List<string>();
        public readonly List<string> OwnerGroups = new List<string>();
        public readonly List<string> OwnerDlcIds = new List<string>();
        public bool HasBaseOwner;
    }

    internal static class BlueprintResourcePackagingUtility
    {
        public const string PackagingPolicyAssetPath = "Assets/BlueprintSystem/Resources/BlueprintResourcePackagingPolicy.asset";
        public const string ResourceContentBaseLabel = "ResourceContent.Base";
        public const string ResourceContentDlcLabel = "ResourceContent.DLC";
        public const string ResourceSharedLabel = "ResourceShared";

        private const string BlueprintSystemResourceFolder = "Assets/BlueprintSystem/Resources";
        private const string ResourceDlcLabelPrefix = "ResourceDLC.";
        private const string SharedBaseGroupName = "BlueprintResources_Shared_Base";
        private const string SharedDlcCommonGroupName = "BlueprintResources_Shared_DLCCommon";

        public static BlueprintResourcePackagingPolicyAsset GetOrCreatePolicyAsset()
        {
            BlueprintResourcePackagingPolicyAsset policy =
                AssetDatabase.LoadAssetAtPath<BlueprintResourcePackagingPolicyAsset>(PackagingPolicyAssetPath);
            if (policy != null)
            {
                return policy;
            }

            EnsureFolder(BlueprintSystemResourceFolder);
            policy = UnityEngine.ScriptableObject.CreateInstance<BlueprintResourcePackagingPolicyAsset>();
            policy.DefaultRule.IncludeInBuild = true;
            policy.DefaultRule.ContentLocation = BlueprintResourceContentLocation.Base;
            policy.DefaultRule.DlcId = string.Empty;
            policy.DefaultRule.LoadPriority = 0;
            AssetDatabase.CreateAsset(policy, PackagingPolicyAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PackagingPolicyAssetPath);
            return AssetDatabase.LoadAssetAtPath<BlueprintResourcePackagingPolicyAsset>(PackagingPolicyAssetPath);
        }

        public static BlueprintResourcePackagingPolicyAsset LoadPolicyAsset()
        {
            return AssetDatabase.LoadAssetAtPath<BlueprintResourcePackagingPolicyAsset>(PackagingPolicyAssetPath);
        }

        public static BlueprintResourceResolvedPackaging Resolve(
            BlueprintResourceBlueprintSource source,
            BlueprintResourcePackagingPolicyAsset policy)
        {
            BlueprintResourcePackagingRule rule = ResolveRule(source, policy);
            string resourceType = source == null ? string.Empty : source.ResourceType;
            string resourceName = source == null ? string.Empty : source.ResourceName;
            string dlcId = NormalizeId(rule.DlcId);
            BlueprintResourceDlcDefinition dlc = rule.ContentLocation == BlueprintResourceContentLocation.DLC && policy != null
                ? policy.FindDlc(dlcId)
                : null;

            BlueprintResourceResolvedPackaging resolved = new BlueprintResourceResolvedPackaging();
            resolved.IncludeInBuild = rule.IncludeInBuild;
            resolved.ContentLocation = rule.ContentLocation;
            resolved.DlcId = dlcId;
            resolved.DlcDisplayName = dlc == null ? string.Empty : dlc.DisplayName;
            resolved.LoadPriority = rule.LoadPriority;
            resolved.RuleSource = FindRuleSource(source, policy);
            resolved.Address = CreateResourceAddress(resourceType, resourceName);
            resolved.ContentLabel = rule.ContentLocation == BlueprintResourceContentLocation.DLC
                ? ResourceContentDlcLabel
                : ResourceContentBaseLabel;
            resolved.DlcLabel = rule.ContentLocation == BlueprintResourceContentLocation.DLC && !string.IsNullOrEmpty(dlcId)
                ? ResourceDlcLabelPrefix + SanitizeLabelPart(dlcId)
                : string.Empty;
            resolved.GroupName = CreateResourceGroupName(resourceType, rule.ContentLocation, dlcId);
            return resolved;
        }

        public static BlueprintResourcePackagingRule ResolveRule(
            BlueprintResourceBlueprintSource source,
            BlueprintResourcePackagingPolicyAsset policy)
        {
            BlueprintResourcePackagingRule resolved = policy == null
                ? new BlueprintResourcePackagingRule()
                : policy.DefaultRule.Clone();

            if (source == null)
            {
                return resolved;
            }

            BlueprintResourceTypePackagingRule typeRule = policy == null ? null : policy.FindTypeRule(source.ResourceType);
            if (typeRule != null && typeRule.Rule != null)
            {
                resolved.CopyFrom(typeRule.Rule);
            }

            BlueprintResourceOverridePackagingRule resourceOverride = policy == null
                ? null
                : policy.FindResourceOverride(source.ResourceType, source.ResourceName);
            if (resourceOverride != null && resourceOverride.Rule != null)
            {
                resolved.CopyFrom(resourceOverride.Rule);
            }

            return resolved;
        }

        public static string CreateResourceAddress(string resourceType, string resourceName)
        {
            return "Resource/" + SanitizeLabelPart(resourceType) + "/" + SanitizeLabelPart(resourceName);
        }

        public static string CreateResourceGroupName(
            string resourceType,
            BlueprintResourceContentLocation location,
            string dlcId)
        {
            if (location == BlueprintResourceContentLocation.DLC)
            {
                return "BlueprintResources_DLC_" + SanitizeLabelPart(dlcId) + "_" + SanitizeLabelPart(resourceType);
            }

            return "BlueprintResources_Base_" + SanitizeLabelPart(resourceType);
        }

        public static List<BlueprintResourceSharedDependencyCandidate> ScanSharedDependencies(
            BlueprintResourceAssetManagerReport report)
        {
            List<BlueprintResourceSharedDependencyCandidate> candidates =
                new List<BlueprintResourceSharedDependencyCandidate>();
            if (report == null)
            {
                return candidates;
            }

            Dictionary<string, bool> resourceMainAssetPaths = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < report.Records.Count; i++)
            {
                BlueprintResourceAssetRecord record = report.Records[i];
                string path = GetIncludedMainAssetPath(record);
                if (!string.IsNullOrEmpty(path))
                {
                    resourceMainAssetPaths[path] = true;
                }
            }

            Dictionary<string, SharedDependencyAccumulator> byPath =
                new Dictionary<string, SharedDependencyAccumulator>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < report.Records.Count; i++)
            {
                BlueprintResourceAssetRecord record = report.Records[i];
                string ownerPath = GetIncludedMainAssetPath(record);
                if (string.IsNullOrEmpty(ownerPath))
                {
                    continue;
                }

                string[] dependencies = AssetDatabase.GetDependencies(ownerPath, true);
                for (int d = 0; d < dependencies.Length; d++)
                {
                    string dependencyPath = BlueprintAssetDiscovery.NormalizeAssetPath(dependencies[d]);
                    if (!IsEligibleSharedDependencyPath(dependencyPath, ownerPath, resourceMainAssetPaths))
                    {
                        continue;
                    }

                    SharedDependencyAccumulator accumulator;
                    if (!byPath.TryGetValue(dependencyPath, out accumulator))
                    {
                        accumulator = new SharedDependencyAccumulator(dependencyPath);
                        byPath[dependencyPath] = accumulator;
                    }

                    accumulator.AddOwner(record);
                }
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (KeyValuePair<string, SharedDependencyAccumulator> pair in byPath)
            {
                if (!pair.Value.ShouldExtract)
                {
                    continue;
                }

                BlueprintResourceSharedDependencyCandidate candidate = pair.Value.ToCandidate();
                AddressableAssetEntry existing = settings == null || string.IsNullOrEmpty(candidate.AssetGuid)
                    ? null
                    : settings.FindAssetEntry(candidate.AssetGuid, true);
                if (existing != null && !IsBlueprintManagedEntry(existing))
                {
                    candidate.Warning = "Existing Addressables entry is not managed by Blueprint Resources and will not be moved automatically.";
                }

                candidates.Add(candidate);
            }

            candidates.Sort(delegate(BlueprintResourceSharedDependencyCandidate left, BlueprintResourceSharedDependencyCandidate right)
            {
                return string.CompareOrdinal(left.AssetPath, right.AssetPath);
            });
            report.SharedDependencyCandidates.Clear();
            report.SharedDependencyCandidates.AddRange(candidates);
            return candidates;
        }

        public static List<BlueprintResourceSharedDependencyCandidate> ExtractSharedDependencies(
            BlueprintResourceAssetManagerReport report)
        {
            List<BlueprintResourceSharedDependencyCandidate> candidates = report == null
                ? new List<BlueprintResourceSharedDependencyCandidate>()
                : report.SharedDependencyCandidates;
            if (report != null && candidates.Count == 0)
            {
                candidates = ScanSharedDependencies(report);
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                return candidates;
            }

            EnsureLabel(settings, ResourceSharedLabel);
            EnsureLabel(settings, ResourceContentBaseLabel);
            EnsureLabel(settings, ResourceContentDlcLabel);

            for (int i = 0; i < candidates.Count; i++)
            {
                BlueprintResourceSharedDependencyCandidate candidate = candidates[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.AssetGuid))
                {
                    continue;
                }

                AddressableAssetEntry existing = settings.FindAssetEntry(candidate.AssetGuid, true);
                if (existing != null && !IsBlueprintManagedEntry(existing))
                {
                    candidate.Warning = "Skipped because the existing Addressables entry is not managed by Blueprint Resources.";
                    continue;
                }

                AddressableAssetGroup group = EnsureGroup(settings, candidate.SharedGroupName);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(candidate.AssetGuid, group, false, false);
                entry.address = candidate.Address;
                ClearBlueprintPackagingLabels(entry);
                SetLabel(entry, settings, ResourceSharedLabel);
                SetLabel(entry, settings, candidate.HasBaseOwner ? ResourceContentBaseLabel : ResourceContentDlcLabel);
                if (!candidate.HasBaseOwner && candidate.OwnerDlcIds.Count == 1)
                {
                    SetLabel(entry, settings, ResourceDlcLabelPrefix + SanitizeLabelPart(candidate.OwnerDlcIds[0]));
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            return candidates;
        }

        public static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName)
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

        public static void SetLabel(AddressableAssetEntry entry, AddressableAssetSettings settings, string label)
        {
            if (entry == null || string.IsNullOrEmpty(label))
            {
                return;
            }

            EnsureLabel(settings, label);
            entry.SetLabel(label, true, true);
        }

        public static void EnsureLabel(AddressableAssetSettings settings, string label)
        {
            if (settings != null && !string.IsNullOrEmpty(label))
            {
                settings.AddLabel(label, false);
            }
        }

        public static void ClearBlueprintPackagingLabels(AddressableAssetEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            List<string> labelsToRemove = new List<string>();
            foreach (string label in entry.labels)
            {
                if (label == ResourceContentBaseLabel ||
                    label == ResourceContentDlcLabel ||
                    label == ResourceSharedLabel ||
                    label.StartsWith(ResourceDlcLabelPrefix, StringComparison.Ordinal) ||
                    label.StartsWith("ResourceTag.", StringComparison.Ordinal) ||
                    label.StartsWith("Resource.", StringComparison.Ordinal))
                {
                    labelsToRemove.Add(label);
                }
            }

            for (int i = 0; i < labelsToRemove.Count; i++)
            {
                entry.SetLabel(labelsToRemove[i], false, false);
            }
        }

        public static bool IsBlueprintManagedEntry(AddressableAssetEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            foreach (string label in entry.labels)
            {
                if (label == "ResourceBlueprint" || label == ResourceSharedLabel)
                {
                    return true;
                }
            }

            return !string.IsNullOrEmpty(entry.address) &&
                   (entry.address.StartsWith("Resource/", StringComparison.Ordinal) ||
                    entry.address.StartsWith("Shared/", StringComparison.Ordinal));
        }

        public static string SanitizeLabelPart(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "None";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }

            return builder.Length == 0 ? "None" : builder.ToString();
        }

        public static string ComputePolicyHash(BlueprintResourcePackagingPolicyAsset policy)
        {
            if (policy == null)
            {
                return "default-packaging-policy";
            }

            return ComputeHash(EditorJsonUtility.ToJson(policy, false));
        }

        private static string FindRuleSource(
            BlueprintResourceBlueprintSource source,
            BlueprintResourcePackagingPolicyAsset policy)
        {
            if (source == null || policy == null)
            {
                return "Default";
            }

            if (policy.FindResourceOverride(source.ResourceType, source.ResourceName) != null)
            {
                return "Resource Override";
            }

            if (policy.FindTypeRule(source.ResourceType) != null)
            {
                return "Type Rule";
            }

            return "Default";
        }

        private static string GetIncludedMainAssetPath(BlueprintResourceAssetRecord record)
        {
            if (record == null ||
                record.Source == null ||
                record.Packaging == null ||
                !record.Packaging.IncludeInBuild ||
                record.Source.MainAsset == null)
            {
                return null;
            }

            string path = record.Source.MainAsset.Path;
            if (!string.IsNullOrEmpty(record.Source.MainAsset.Guid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(record.Source.MainAsset.Guid);
                if (!string.IsNullOrEmpty(guidPath))
                {
                    path = guidPath;
                }
            }

            return BlueprintAssetDiscovery.NormalizeAssetPath(path);
        }

        private static bool IsEligibleSharedDependencyPath(
            string dependencyPath,
            string ownerPath,
            Dictionary<string, bool> resourceMainAssetPaths)
        {
            if (string.IsNullOrEmpty(dependencyPath) ||
                string.Equals(dependencyPath, ownerPath, StringComparison.OrdinalIgnoreCase) ||
                resourceMainAssetPaths.ContainsKey(dependencyPath) ||
                !dependencyPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.IsValidFolder(dependencyPath))
            {
                return false;
            }

            string extension = Path.GetExtension(dependencyPath).ToLowerInvariant();
            if (extension == ".cs" ||
                extension == ".asmdef" ||
                extension == ".unity" ||
                extension == BlueprintResourceBlueprintSource.AssetExtension ||
                extension == ".resourcebpgraph")
            {
                return false;
            }

            return AssetDatabase.LoadMainAssetAtPath(dependencyPath) != null;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
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

        private sealed class SharedDependencyAccumulator
        {
            private readonly string _path;
            private readonly Dictionary<string, bool> _ownerResources = new Dictionary<string, bool>(StringComparer.Ordinal);
            private readonly Dictionary<string, bool> _ownerGroups = new Dictionary<string, bool>(StringComparer.Ordinal);
            private readonly Dictionary<string, bool> _ownerDlcIds = new Dictionary<string, bool>(StringComparer.Ordinal);
            private bool _hasBaseOwner;

            public SharedDependencyAccumulator(string path)
            {
                _path = path;
            }

            public bool ShouldExtract
            {
                get
                {
                    if (_ownerResources.Count <= 1)
                    {
                        return false;
                    }

                    if (_ownerGroups.Count > 1)
                    {
                        return true;
                    }

                    return !_hasBaseOwner && _ownerDlcIds.Count == 1;
                }
            }

            public void AddOwner(BlueprintResourceAssetRecord record)
            {
                if (record == null || record.Source == null || record.Packaging == null)
                {
                    return;
                }

                _ownerResources[record.Source.Id.ToString()] = true;
                _ownerGroups[record.Packaging.GroupName] = true;
                if (record.Packaging.ContentLocation == BlueprintResourceContentLocation.Base)
                {
                    _hasBaseOwner = true;
                }
                else if (!string.IsNullOrEmpty(record.Packaging.DlcId))
                {
                    _ownerDlcIds[record.Packaging.DlcId] = true;
                }
            }

            public BlueprintResourceSharedDependencyCandidate ToCandidate()
            {
                BlueprintResourceSharedDependencyCandidate candidate = new BlueprintResourceSharedDependencyCandidate();
                candidate.AssetPath = _path;
                candidate.AssetGuid = AssetDatabase.AssetPathToGUID(_path);
                candidate.Address = "Shared/" + ComputeHash(_path).Substring(0, 16);
                candidate.HasBaseOwner = _hasBaseOwner;
                AddKeys(candidate.OwnerResourceIds, _ownerResources);
                AddKeys(candidate.OwnerGroups, _ownerGroups);
                AddKeys(candidate.OwnerDlcIds, _ownerDlcIds);

                if (_hasBaseOwner)
                {
                    candidate.SharedGroupName = SharedBaseGroupName;
                }
                else if (candidate.OwnerDlcIds.Count == 1)
                {
                    candidate.SharedGroupName = "BlueprintResources_Shared_DLC_" + SanitizeLabelPart(candidate.OwnerDlcIds[0]);
                }
                else
                {
                    candidate.SharedGroupName = SharedDlcCommonGroupName;
                }

                return candidate;
            }

            private static void AddKeys(List<string> output, Dictionary<string, bool> values)
            {
                foreach (KeyValuePair<string, bool> pair in values)
                {
                    output.Add(pair.Key);
                }

                output.Sort(StringComparer.Ordinal);
            }
        }
    }
}
