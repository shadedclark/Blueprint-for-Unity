using System;
using System.Collections.Generic;

namespace BlueprintSystem
{
    public enum BlueprintPortKind
    {
        Exec,
        Value
    }

    public enum BlueprintPortDirection
    {
        Input,
        Output
    }

    public enum BlueprintValueSource
    {
        None,
        Property,
        Connection,
        PropertyOrConnection
    }

    [Serializable]
    public sealed class BlueprintNodeManifest
    {
        public string SchemaVersion;
        public string TypeId;
        public string Title;
        public string Category;
        public string Description;
        public string Executor;
        public readonly List<BlueprintPortSpec> Inputs = new List<BlueprintPortSpec>();
        public readonly List<BlueprintPortSpec> Outputs = new List<BlueprintPortSpec>();
        public readonly List<BlueprintPropertySpec> Properties = new List<BlueprintPropertySpec>();
        public readonly List<string> Tags = new List<string>();

        public static BlueprintNodeManifest FromJson(string json)
        {
            return BlueprintManifestMapper.FromDictionary(BlueprintJson.DeserializeObject(json));
        }

        public BlueprintPortSpec FindInput(string portId)
        {
            return FindPort(Inputs, portId);
        }

        public BlueprintPortSpec FindOutput(string portId)
        {
            return FindPort(Outputs, portId);
        }

        public BlueprintPropertySpec FindProperty(string propertyId)
        {
            for (int i = 0; i < Properties.Count; i++)
            {
                if (Properties[i].Id == propertyId)
                {
                    return Properties[i];
                }
            }

            return null;
        }

        private static BlueprintPortSpec FindPort(List<BlueprintPortSpec> ports, string portId)
        {
            for (int i = 0; i < ports.Count; i++)
            {
                if (ports[i].Id == portId)
                {
                    return ports[i];
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class BlueprintPortSpec
    {
        public string Id;
        public BlueprintPortKind Kind;
        public BlueprintPortDirection Direction;
        public string Type;
        public bool Required;
        public BlueprintValueSource Source;
        public bool AllowMultiple;
    }

    [Serializable]
    public sealed class BlueprintPropertySpec
    {
        public string Id;
        public string Type;
        public bool Required;
        public object DefaultValue;
    }

    public sealed class BlueprintNodeManifestCollection
    {
        private readonly Dictionary<string, BlueprintNodeManifest> _manifests = new Dictionary<string, BlueprintNodeManifest>();

        public IReadOnlyDictionary<string, BlueprintNodeManifest> ManifestsByTypeId
        {
            get { return _manifests; }
        }

        public void Add(BlueprintNodeManifest manifest)
        {
            if (manifest == null || string.IsNullOrEmpty(manifest.TypeId))
            {
                return;
            }

            _manifests[manifest.TypeId] = manifest;
        }

        public bool TryGet(string typeId, out BlueprintNodeManifest manifest)
        {
            return _manifests.TryGetValue(typeId, out manifest);
        }

        public static BlueprintNodeManifestCollection FromJsonTexts(IEnumerable<string> manifestJsonTexts)
        {
            BlueprintNodeManifestCollection collection = new BlueprintNodeManifestCollection();
            foreach (string json in manifestJsonTexts)
            {
                if (string.IsNullOrEmpty(json))
                {
                    continue;
                }

                collection.Add(BlueprintNodeManifest.FromJson(json));
            }

            return collection;
        }
    }
}
