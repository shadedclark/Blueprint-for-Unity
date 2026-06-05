using System;
using System.Collections.Generic;

namespace BlueprintSystem
{
    [Serializable]
    public sealed class BlueprintSource
    {
        public string SchemaVersion;
        public string Name;
        public string Category;
        public string Description;
        public readonly List<BlueprintVariableDeclaration> Variables = new List<BlueprintVariableDeclaration>();
        public readonly List<BlueprintBindingDeclaration> Bindings = new List<BlueprintBindingDeclaration>();
        public readonly List<BlueprintComponentDeclaration> Components = new List<BlueprintComponentDeclaration>();
        public readonly List<BlueprintNodeSource> Nodes = new List<BlueprintNodeSource>();
        public readonly List<BlueprintEdgeSource> Edges = new List<BlueprintEdgeSource>();

        public static BlueprintSource FromJson(string json)
        {
            return BlueprintSourceMapper.FromDictionary(BlueprintJson.DeserializeObject(json));
        }

        public string ToJson()
        {
            return BlueprintJson.Serialize(BlueprintSourceMapper.ToDictionary(this), true);
        }
    }

    [Serializable]
    public sealed class BlueprintVariableDeclaration
    {
        public string Id;
        public string Name;
        public string Type;
        public object DefaultValue;
        public string Scope;
        public bool Exposed;
        public bool Persistent;
        public string Description;
    }

    [Serializable]
    public sealed class BlueprintVariableOverride
    {
        public string VariableId;
        public string Name;
        public string Type;
        public bool Enabled;
        public string JsonValue;
        public UnityEngine.Object ObjectValue;
    }

    [Serializable]
    public sealed class BlueprintBindingDeclaration
    {
        public string Name;
        public string Type;
        public bool Required;
    }

    [Serializable]
    public sealed class BlueprintComponentDeclaration
    {
        public string Name;
        public string Blueprint;
        public bool Required;
        public BlueprintCompiledAsset CompiledBlueprint;
    }

    [Serializable]
    public sealed class BlueprintNodeSource
    {
        public string Id;
        public string TypeId;
        public float X;
        public float Y;
        public readonly Dictionary<string, object> Properties = new Dictionary<string, object>();
    }

    [Serializable]
    public sealed class BlueprintEdgeSource
    {
        public string From;
        public string To;
    }

    public static class BlueprintVariableIdUtility
    {
        public static bool EnsureVariableIds(BlueprintSource source)
        {
            if (source == null || source.Variables == null)
            {
                return false;
            }

            bool changed = false;
            HashSet<string> usedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.Variables.Count; i++)
            {
                BlueprintVariableDeclaration variable = source.Variables[i];
                if (variable == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(variable.Id) || usedIds.Contains(variable.Id))
                {
                    variable.Id = CreateVariableId();
                    changed = true;
                }

                usedIds.Add(variable.Id);
            }

            return changed;
        }

        private static string CreateVariableId()
        {
            return "var_" + Guid.NewGuid().ToString("N");
        }
    }
}
