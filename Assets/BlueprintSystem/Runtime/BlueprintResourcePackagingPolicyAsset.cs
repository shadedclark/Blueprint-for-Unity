using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public enum BlueprintResourceContentLocation
    {
        Base,
        DLC
    }

    [Serializable]
    public sealed class BlueprintResourceDlcDefinition
    {
        public string DlcId;
        public string DisplayName;
        public bool IncludeInBuild = true;

        public string NormalizedDlcId
        {
            get { return string.IsNullOrEmpty(DlcId) ? string.Empty : DlcId.Trim(); }
        }
    }

    [Serializable]
    public sealed class BlueprintResourcePackagingRule
    {
        public bool IncludeInBuild = true;
        public BlueprintResourceContentLocation ContentLocation = BlueprintResourceContentLocation.Base;
        public string DlcId;
        public int LoadPriority;

        public BlueprintResourcePackagingRule Clone()
        {
            BlueprintResourcePackagingRule rule = new BlueprintResourcePackagingRule();
            rule.CopyFrom(this);
            return rule;
        }

        public void CopyFrom(BlueprintResourcePackagingRule source)
        {
            if (source == null)
            {
                IncludeInBuild = true;
                ContentLocation = BlueprintResourceContentLocation.Base;
                DlcId = string.Empty;
                LoadPriority = 0;
                return;
            }

            IncludeInBuild = source.IncludeInBuild;
            ContentLocation = source.ContentLocation;
            DlcId = source.DlcId;
            LoadPriority = source.LoadPriority;
        }
    }

    [Serializable]
    public sealed class BlueprintResourceTypePackagingRule
    {
        public string ResourceType;
        public BlueprintResourcePackagingRule Rule = new BlueprintResourcePackagingRule();
    }

    [Serializable]
    public sealed class BlueprintResourceOverridePackagingRule
    {
        public string ResourceType;
        public string ResourceName;
        public BlueprintResourcePackagingRule Rule = new BlueprintResourcePackagingRule();
    }

    [CreateAssetMenu(menuName = "Blueprint System/Resource Packaging Policy", fileName = "BlueprintResourcePackagingPolicy")]
    public sealed class BlueprintResourcePackagingPolicyAsset : ScriptableObject
    {
        [SerializeField] private string schemaVersion = "0.1";
        [SerializeField] private BlueprintResourcePackagingRule defaultRule = new BlueprintResourcePackagingRule();
        [SerializeField] private List<BlueprintResourceDlcDefinition> dlcs = new List<BlueprintResourceDlcDefinition>();
        [SerializeField] private List<BlueprintResourceTypePackagingRule> typeRules = new List<BlueprintResourceTypePackagingRule>();
        [SerializeField] private List<BlueprintResourceOverridePackagingRule> resourceOverrides = new List<BlueprintResourceOverridePackagingRule>();

        public string SchemaVersion
        {
            get { return string.IsNullOrEmpty(schemaVersion) ? "0.1" : schemaVersion; }
            set { schemaVersion = string.IsNullOrEmpty(value) ? "0.1" : value; }
        }

        public BlueprintResourcePackagingRule DefaultRule
        {
            get
            {
                if (defaultRule == null)
                {
                    defaultRule = new BlueprintResourcePackagingRule();
                }

                return defaultRule;
            }
        }

        public List<BlueprintResourceDlcDefinition> Dlcs
        {
            get { return dlcs ?? (dlcs = new List<BlueprintResourceDlcDefinition>()); }
        }

        public List<BlueprintResourceTypePackagingRule> TypeRules
        {
            get { return typeRules ?? (typeRules = new List<BlueprintResourceTypePackagingRule>()); }
        }

        public List<BlueprintResourceOverridePackagingRule> ResourceOverrides
        {
            get { return resourceOverrides ?? (resourceOverrides = new List<BlueprintResourceOverridePackagingRule>()); }
        }

        public BlueprintResourceDlcDefinition FindDlc(string dlcId)
        {
            string normalized = NormalizeId(dlcId);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            for (int i = 0; i < Dlcs.Count; i++)
            {
                BlueprintResourceDlcDefinition dlc = Dlcs[i];
                if (dlc != null && string.Equals(NormalizeId(dlc.DlcId), normalized, StringComparison.Ordinal))
                {
                    return dlc;
                }
            }

            return null;
        }

        public BlueprintResourceTypePackagingRule FindTypeRule(string resourceType)
        {
            string normalized = NormalizeId(resourceType);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            for (int i = 0; i < TypeRules.Count; i++)
            {
                BlueprintResourceTypePackagingRule rule = TypeRules[i];
                if (rule != null && string.Equals(NormalizeId(rule.ResourceType), normalized, StringComparison.Ordinal))
                {
                    return rule;
                }
            }

            return null;
        }

        public BlueprintResourceOverridePackagingRule FindResourceOverride(string resourceType, string resourceName)
        {
            string normalizedType = NormalizeId(resourceType);
            string normalizedName = NormalizeId(resourceName);
            if (string.IsNullOrEmpty(normalizedType) || string.IsNullOrEmpty(normalizedName))
            {
                return null;
            }

            for (int i = 0; i < ResourceOverrides.Count; i++)
            {
                BlueprintResourceOverridePackagingRule rule = ResourceOverrides[i];
                if (rule != null &&
                    string.Equals(NormalizeId(rule.ResourceType), normalizedType, StringComparison.Ordinal) &&
                    string.Equals(NormalizeId(rule.ResourceName), normalizedName, StringComparison.Ordinal))
                {
                    return rule;
                }
            }

            return null;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        }
    }
}
