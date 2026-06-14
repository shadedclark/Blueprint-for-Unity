using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BlueprintSystem
{
    public enum BlueprintResourceLoadState
    {
        Unloaded,
        Queued,
        Loading,
        Loaded,
        Failed,
        Cancelled
    }

    public enum BlueprintResourceScope
    {
        Scene,
        Screen,
        Gameplay,
        Global,
        Manual
    }

    [Serializable]
    public struct BlueprintPrimaryResourceId : IEquatable<BlueprintPrimaryResourceId>
    {
        [SerializeField] private string resourceType;
        [SerializeField] private string resourceName;

        public BlueprintPrimaryResourceId(string resourceType, string resourceName)
        {
            this.resourceType = NormalizePart(resourceType);
            this.resourceName = NormalizePart(resourceName);
        }

        public string ResourceType
        {
            get { return resourceType; }
        }

        public string ResourceName
        {
            get { return resourceName; }
        }

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(resourceType) && !string.IsNullOrEmpty(resourceName); }
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(resourceType) ? resourceName : resourceType + ":" + resourceName;
        }

        public bool Equals(BlueprintPrimaryResourceId other)
        {
            return string.Equals(resourceType, other.resourceType, StringComparison.Ordinal) &&
                   string.Equals(resourceName, other.resourceName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BlueprintPrimaryResourceId && Equals((BlueprintPrimaryResourceId)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (resourceType == null ? 0 : resourceType.GetHashCode());
                hash = hash * 31 + (resourceName == null ? 0 : resourceName.GetHashCode());
                return hash;
            }
        }

        public static string NormalizePart(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        }

        public static bool TryParse(string value, out BlueprintPrimaryResourceId id)
        {
            id = new BlueprintPrimaryResourceId();
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            int separator = value.IndexOf(':');
            if (separator < 0)
            {
                return false;
            }

            id = new BlueprintPrimaryResourceId(value.Substring(0, separator), value.Substring(separator + 1));
            return id.IsValid;
        }
    }

    [Serializable]
    public sealed class BlueprintResourceAssetReference
    {
        public string Guid;
        public string Path;
        public string Address;
        public string AssetType;

        public bool HasLoadAddress
        {
            get { return !string.IsNullOrEmpty(Address); }
        }
    }

    [Serializable]
    public sealed class BlueprintResourceDependency
    {
        public string ResourceType;
        public string ResourceName;
        public bool Required = true;
        public string PreloadGroup;

        public BlueprintPrimaryResourceId ToId()
        {
            return new BlueprintPrimaryResourceId(ResourceType, ResourceName);
        }
    }

    [Serializable]
    public sealed class BlueprintResourceMetadataField
    {
        public string Name;
        public string ValueJson;
    }

    [Serializable]
    public sealed class BlueprintResourceTypeField
    {
        public string Name;
        public string Type = "string";
        public bool Required;
        public string DefaultValueJson;
    }

    [Serializable]
    public sealed class BlueprintResourceTypeDefinition
    {
        [SerializeField] private string resourceType = "Resource";
        [SerializeField] private List<BlueprintResourceTypeField> fields = new List<BlueprintResourceTypeField>();

        public string ResourceType
        {
            get { return string.IsNullOrEmpty(resourceType) ? string.Empty : resourceType.Trim(); }
            set { resourceType = value; }
        }

        public List<BlueprintResourceTypeField> Fields
        {
            get { return fields; }
        }
    }

    [Serializable]
    public sealed class BlueprintResourceBlueprintSource
    {
        public const string AssetExtension = ".resourceblueprint.json";
        public const string SchemaVersionValue = "0.1";

        public string SchemaVersion = SchemaVersionValue;
        public string ResourceType;
        public string ResourceName;
        public string DisplayName;
        public string Description;
        public readonly List<string> Tags = new List<string>();
        public BlueprintResourceAssetReference MainAsset = new BlueprintResourceAssetReference();
        public readonly List<BlueprintResourceDependency> Dependencies = new List<BlueprintResourceDependency>();
        public readonly List<string> PreloadGroups = new List<string>();
        public int Priority;
        public float MemoryBudgetMb;
        public string RemoteCatalog;
        public string ContentVersion;
        public readonly List<BlueprintResourceMetadataField> Metadata = new List<BlueprintResourceMetadataField>();

        public BlueprintPrimaryResourceId Id
        {
            get { return new BlueprintPrimaryResourceId(ResourceType, ResourceName); }
        }

        public static BlueprintResourceBlueprintSource FromJson(string json)
        {
            return FromDictionary(BlueprintJson.DeserializeObject(json));
        }

        public string ToJson()
        {
            return BlueprintJson.Serialize(ToDictionary(), true);
        }

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["schemaVersion"] = string.IsNullOrEmpty(SchemaVersion) ? SchemaVersionValue : SchemaVersion;
            data["resourceType"] = ResourceType;
            data["resourceName"] = ResourceName;
            data["displayName"] = DisplayName;
            data["description"] = Description;
            data["tags"] = new List<object>(Tags.ToArray());
            data["mainAsset"] = AssetReferenceToDictionary(MainAsset);

            List<object> dependencies = new List<object>();
            for (int i = 0; i < Dependencies.Count; i++)
            {
                BlueprintResourceDependency dependency = Dependencies[i];
                if (dependency == null)
                {
                    continue;
                }

                Dictionary<string, object> item = new Dictionary<string, object>();
                item["resourceType"] = dependency.ResourceType;
                item["resourceName"] = dependency.ResourceName;
                item["required"] = dependency.Required;
                item["preloadGroup"] = dependency.PreloadGroup;
                dependencies.Add(item);
            }
            data["dependencies"] = dependencies;

            data["preloadGroups"] = new List<object>(PreloadGroups.ToArray());
            data["priority"] = Priority;
            data["memoryBudgetMb"] = MemoryBudgetMb;
            data["remoteCatalog"] = RemoteCatalog;
            data["contentVersion"] = ContentVersion;

            Dictionary<string, object> metadata = new Dictionary<string, object>();
            for (int i = 0; i < Metadata.Count; i++)
            {
                BlueprintResourceMetadataField field = Metadata[i];
                if (field == null || string.IsNullOrEmpty(field.Name))
                {
                    continue;
                }

                metadata[field.Name] = string.IsNullOrEmpty(field.ValueJson) ? null : BlueprintJson.Deserialize(field.ValueJson);
            }
            data["metadata"] = metadata;
            return data;
        }

        private static Dictionary<string, object> AssetReferenceToDictionary(BlueprintResourceAssetReference reference)
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            if (reference == null)
            {
                return data;
            }

            data["guid"] = reference.Guid;
            data["path"] = reference.Path;
            data["address"] = reference.Address;
            data["assetType"] = reference.AssetType;
            return data;
        }

        private static BlueprintResourceBlueprintSource FromDictionary(Dictionary<string, object> data)
        {
            BlueprintResourceBlueprintSource source = new BlueprintResourceBlueprintSource();
            if (data == null)
            {
                return source;
            }

            source.SchemaVersion = BlueprintSourceMapper.GetString(data, "schemaVersion");
            source.ResourceType = BlueprintSourceMapper.GetString(data, "resourceType");
            source.ResourceName = BlueprintSourceMapper.GetString(data, "resourceName");
            source.DisplayName = BlueprintSourceMapper.GetString(data, "displayName");
            source.Description = BlueprintSourceMapper.GetString(data, "description");
            ReadStringArray(data, "tags", source.Tags);
            source.MainAsset = ReadAssetReference(BlueprintSourceMapper.GetObject(data, "mainAsset"));

            foreach (Dictionary<string, object> item in BlueprintSourceMapper.GetObjectArray(data, "dependencies"))
            {
                BlueprintResourceDependency dependency = new BlueprintResourceDependency();
                dependency.ResourceType = BlueprintSourceMapper.GetString(item, "resourceType");
                dependency.ResourceName = BlueprintSourceMapper.GetString(item, "resourceName");
                dependency.Required = BlueprintSourceMapper.GetBool(item, "required", true);
                dependency.PreloadGroup = BlueprintSourceMapper.GetString(item, "preloadGroup");
                source.Dependencies.Add(dependency);
            }

            ReadStringArray(data, "preloadGroups", source.PreloadGroups);
            source.Priority = ReadInt(data, "priority", 0);
            source.MemoryBudgetMb = ReadFloat(data, "memoryBudgetMb", 0f);
            source.RemoteCatalog = BlueprintSourceMapper.GetString(data, "remoteCatalog");
            source.ContentVersion = BlueprintSourceMapper.GetString(data, "contentVersion");
            ReadMetadata(BlueprintSourceMapper.GetObject(data, "metadata"), source.Metadata);
            return source;
        }

        private static BlueprintResourceAssetReference ReadAssetReference(Dictionary<string, object> data)
        {
            BlueprintResourceAssetReference reference = new BlueprintResourceAssetReference();
            if (data == null)
            {
                return reference;
            }

            reference.Guid = BlueprintSourceMapper.GetString(data, "guid");
            reference.Path = BlueprintSourceMapper.GetString(data, "path");
            reference.Address = BlueprintSourceMapper.GetString(data, "address");
            reference.AssetType = BlueprintSourceMapper.GetString(data, "assetType");
            return reference;
        }

        private static void ReadStringArray(Dictionary<string, object> data, string key, List<string> output)
        {
            object value;
            if (data == null || !data.TryGetValue(key, out value) || value == null)
            {
                return;
            }

            IEnumerable array = value as IEnumerable;
            if (array == null || value is string)
            {
                return;
            }

            foreach (object item in array)
            {
                if (item != null)
                {
                    output.Add(Convert.ToString(item, CultureInfo.InvariantCulture));
                }
            }
        }

        private static void ReadMetadata(Dictionary<string, object> data, List<BlueprintResourceMetadataField> output)
        {
            if (data == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in data)
            {
                output.Add(new BlueprintResourceMetadataField
                {
                    Name = pair.Key,
                    ValueJson = BlueprintJson.Serialize(pair.Value, false)
                });
            }
        }

        private static int ReadInt(Dictionary<string, object> data, string key, int defaultValue)
        {
            object value;
            if (data == null || !data.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            int parsed;
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : defaultValue;
        }

        private static float ReadFloat(Dictionary<string, object> data, string key, float defaultValue)
        {
            object value;
            if (data == null || !data.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            float parsed;
            return float.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : defaultValue;
        }
    }

    [Serializable]
    public sealed class BlueprintResourceRegistryEntry
    {
        public string ResourceType;
        public string ResourceName;
        public string DisplayName;
        public string Description;
        public string SourcePath;
        public string SourceGuid;
        public string SourceHash;
        public string MainAssetGuid;
        public string MainAssetPath;
        public string MainAssetAddress;
        public string MainAssetType;
        public string[] Tags = new string[0];
        public BlueprintResourceDependency[] Dependencies = new BlueprintResourceDependency[0];
        public string[] PreloadGroups = new string[0];
        public int Priority;
        public float MemoryBudgetMb;
        public string RemoteCatalog;
        public string ContentVersion;
        public string MetadataJson;

        public BlueprintPrimaryResourceId Id
        {
            get { return new BlueprintPrimaryResourceId(ResourceType, ResourceName); }
        }
    }

}
