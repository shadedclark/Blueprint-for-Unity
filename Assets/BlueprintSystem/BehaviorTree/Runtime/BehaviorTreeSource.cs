using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace BlueprintSystem
{
    [Serializable]
    public sealed class BehaviorTreeSource
    {
        public string SchemaVersion;
        public string Name;
        public string Category;
        public string Description;
        public string Root;
        public readonly List<BehaviorTreeBlackboardKey> Blackboard = new List<BehaviorTreeBlackboardKey>();
        public readonly List<BehaviorTreeNodeSource> Nodes = new List<BehaviorTreeNodeSource>();
        public readonly List<BehaviorTreeDecoratorSource> Decorators = new List<BehaviorTreeDecoratorSource>();
        public readonly List<BehaviorTreeServiceSource> Services = new List<BehaviorTreeServiceSource>();

        public static BehaviorTreeSource FromJson(string json)
        {
            return BehaviorTreeSourceMapper.FromDictionary(BlueprintJson.DeserializeObject(json));
        }

        public string ToJson()
        {
            return BlueprintJson.Serialize(BehaviorTreeSourceMapper.ToDictionary(this), true);
        }
    }

    [Serializable]
    public sealed class BehaviorTreeBlackboardKey
    {
        public string Name;
        public string Type;
        public object DefaultValue;
        public bool Exposed;
        public bool Persistent;
        public string Description;
    }

    [Serializable]
    public sealed class BehaviorTreeNodeSource
    {
        public string Id;
        public string TypeId;
        public float X;
        public float Y;
        public readonly List<string> Children = new List<string>();
        public readonly List<string> Decorators = new List<string>();
        public readonly List<string> Services = new List<string>();
        public readonly Dictionary<string, string> Inputs = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, object> Properties = new Dictionary<string, object>();
    }

    [Serializable]
    public sealed class BehaviorTreeDecoratorSource
    {
        public string Id;
        public string TypeId;
        public readonly Dictionary<string, string> Inputs = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, object> Properties = new Dictionary<string, object>();
    }

    [Serializable]
    public sealed class BehaviorTreeServiceSource
    {
        public string Id;
        public string TypeId;
        public float Interval;
        public float RandomDeviation;
        public readonly Dictionary<string, object> Properties = new Dictionary<string, object>();
    }

    internal static class BehaviorTreeSourceMapper
    {
        public static BehaviorTreeSource FromDictionary(Dictionary<string, object> data)
        {
            BehaviorTreeSource source = new BehaviorTreeSource();
            source.SchemaVersion = BlueprintSourceMapper.GetString(data, "schemaVersion");
            source.Name = BlueprintSourceMapper.GetString(data, "name");
            source.Category = BlueprintSourceMapper.GetString(data, "category");
            source.Description = BlueprintSourceMapper.GetString(data, "description");
            source.Root = BlueprintSourceMapper.GetString(data, "root");

            foreach (Dictionary<string, object> item in BlueprintSourceMapper.GetObjectArray(data, "blackboard"))
            {
                BehaviorTreeBlackboardKey key = new BehaviorTreeBlackboardKey();
                key.Name = BlueprintSourceMapper.GetString(item, "name");
                key.Type = BlueprintSourceMapper.GetString(item, "type");
                item.TryGetValue("defaultValue", out key.DefaultValue);
                key.Exposed = BlueprintSourceMapper.GetBool(item, "exposed", false);
                key.Persistent = BlueprintSourceMapper.GetBool(item, "persistent", false);
                key.Description = BlueprintSourceMapper.GetString(item, "description");
                source.Blackboard.Add(key);
            }

            foreach (Dictionary<string, object> item in BlueprintSourceMapper.GetObjectArray(data, "nodes"))
            {
                BehaviorTreeNodeSource node = new BehaviorTreeNodeSource();
                node.Id = BlueprintSourceMapper.GetString(item, "id");
                node.TypeId = BlueprintSourceMapper.GetString(item, "typeId");
                ReadPosition(item, node);
                ReadStringArray(item, "children", node.Children);
                ReadStringArray(item, "decorators", node.Decorators);
                ReadStringArray(item, "services", node.Services);
                ReadInputBindings(item, node.Inputs);
                CopyProperties(item, node.Properties);
                source.Nodes.Add(node);
            }

            foreach (Dictionary<string, object> item in BlueprintSourceMapper.GetObjectArray(data, "decorators"))
            {
                BehaviorTreeDecoratorSource decorator = new BehaviorTreeDecoratorSource();
                decorator.Id = BlueprintSourceMapper.GetString(item, "id");
                decorator.TypeId = BlueprintSourceMapper.GetString(item, "typeId");
                ReadInputBindings(item, decorator.Inputs);
                CopyProperties(item, decorator.Properties);
                source.Decorators.Add(decorator);
            }

            foreach (Dictionary<string, object> item in BlueprintSourceMapper.GetObjectArray(data, "services"))
            {
                BehaviorTreeServiceSource service = new BehaviorTreeServiceSource();
                service.Id = BlueprintSourceMapper.GetString(item, "id");
                service.TypeId = BlueprintSourceMapper.GetString(item, "typeId");
                service.Interval = GetFloat(item, "interval", 0f);
                service.RandomDeviation = GetFloat(item, "randomDeviation", 0f);
                CopyProperties(item, service.Properties);
                source.Services.Add(service);
            }

            return source;
        }

        public static Dictionary<string, object> ToDictionary(BehaviorTreeSource source)
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["schemaVersion"] = string.IsNullOrEmpty(source.SchemaVersion) ? "0.1" : source.SchemaVersion;
            data["name"] = source.Name;
            if (!string.IsNullOrEmpty(source.Category))
            {
                data["category"] = source.Category;
            }

            if (!string.IsNullOrEmpty(source.Description))
            {
                data["description"] = source.Description;
            }

            data["blackboard"] = BuildBlackboard(source.Blackboard);
            data["root"] = source.Root;
            data["nodes"] = BuildNodes(source.Nodes);
            data["decorators"] = BuildDecorators(source.Decorators);
            data["services"] = BuildServices(source.Services);
            return data;
        }

        private static List<object> BuildBlackboard(List<BehaviorTreeBlackboardKey> keys)
        {
            List<object> result = new List<object>();
            for (int i = 0; i < keys.Count; i++)
            {
                BehaviorTreeBlackboardKey key = keys[i];
                if (key == null)
                {
                    continue;
                }

                Dictionary<string, object> item = new Dictionary<string, object>();
                item["name"] = key.Name;
                item["type"] = key.Type;
                item["defaultValue"] = BehaviorTreeValueUtility.NormalizeValueForJson(key.DefaultValue, key.Type);
                if (key.Exposed)
                {
                    item["exposed"] = true;
                }

                if (key.Persistent)
                {
                    item["persistent"] = true;
                }

                if (!string.IsNullOrEmpty(key.Description))
                {
                    item["description"] = key.Description;
                }

                result.Add(item);
            }

            return result;
        }

        private static List<object> BuildNodes(List<BehaviorTreeNodeSource> nodes)
        {
            List<object> result = new List<object>();
            for (int i = 0; i < nodes.Count; i++)
            {
                BehaviorTreeNodeSource node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                Dictionary<string, object> item = new Dictionary<string, object>();
                item["id"] = node.Id;
                item["typeId"] = node.TypeId;
                item["position"] = new List<object> { node.X, node.Y };
                item["children"] = new List<object>(node.Children.ToArray());
                item["decorators"] = new List<object>(node.Decorators.ToArray());
                item["services"] = new List<object>(node.Services.ToArray());
                if (node.Inputs.Count > 0)
                {
                    item["inputs"] = BuildInputBindings(node.Inputs);
                }

                item["properties"] = new Dictionary<string, object>(node.Properties);
                result.Add(item);
            }

            return result;
        }

        private static Dictionary<string, object> BuildInputBindings(Dictionary<string, string> inputs)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            if (inputs == null)
            {
                return result;
            }

            List<string> keys = new List<string>(inputs.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string inputId = keys[i];
                string blackboardKey = inputs[inputId];
                if (!string.IsNullOrEmpty(inputId) && !string.IsNullOrEmpty(blackboardKey))
                {
                    result[inputId] = blackboardKey;
                }
            }

            return result;
        }

        private static List<object> BuildDecorators(List<BehaviorTreeDecoratorSource> decorators)
        {
            List<object> result = new List<object>();
            for (int i = 0; i < decorators.Count; i++)
            {
                BehaviorTreeDecoratorSource decorator = decorators[i];
                if (decorator == null)
                {
                    continue;
                }

                Dictionary<string, object> item = new Dictionary<string, object>();
                item["id"] = decorator.Id;
                item["typeId"] = decorator.TypeId;
                if (decorator.Inputs.Count > 0)
                {
                    item["inputs"] = BuildInputBindings(decorator.Inputs);
                }

                item["properties"] = new Dictionary<string, object>(decorator.Properties);
                result.Add(item);
            }

            return result;
        }

        private static List<object> BuildServices(List<BehaviorTreeServiceSource> services)
        {
            List<object> result = new List<object>();
            for (int i = 0; i < services.Count; i++)
            {
                BehaviorTreeServiceSource service = services[i];
                if (service == null)
                {
                    continue;
                }

                Dictionary<string, object> item = new Dictionary<string, object>();
                item["id"] = service.Id;
                item["typeId"] = service.TypeId;
                item["interval"] = service.Interval;
                if (service.RandomDeviation > 0f)
                {
                    item["randomDeviation"] = service.RandomDeviation;
                }

                item["properties"] = new Dictionary<string, object>(service.Properties);
                result.Add(item);
            }

            return result;
        }

        private static void CopyProperties(Dictionary<string, object> item, Dictionary<string, object> properties)
        {
            Dictionary<string, object> sourceProperties = BlueprintSourceMapper.GetObject(item, "properties");
            if (sourceProperties == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in sourceProperties)
            {
                properties[pair.Key] = pair.Value;
            }
        }

        private static void ReadInputBindings(Dictionary<string, object> item, Dictionary<string, string> target)
        {
            Dictionary<string, object> sourceInputs = BlueprintSourceMapper.GetObject(item, "inputs");
            if (sourceInputs == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in sourceInputs)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                string blackboardKey = Convert.ToString(pair.Value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(blackboardKey))
                {
                    target[pair.Key] = blackboardKey;
                }
            }
        }

        private static void ReadStringArray(Dictionary<string, object> item, string key, List<string> target)
        {
            object value;
            if (!item.TryGetValue(key, out value) || value == null)
            {
                return;
            }

            IEnumerable array = value as IEnumerable;
            if (array == null || value is string)
            {
                return;
            }

            foreach (object entry in array)
            {
                if (entry != null)
                {
                    target.Add(Convert.ToString(entry, CultureInfo.InvariantCulture));
                }
            }
        }

        private static void ReadPosition(Dictionary<string, object> item, BehaviorTreeNodeSource node)
        {
            object value;
            if (!item.TryGetValue("position", out value))
            {
                return;
            }

            IList list = value as IList;
            if (list == null || list.Count < 2)
            {
                return;
            }

            node.X = Convert.ToSingle(list[0], CultureInfo.InvariantCulture);
            node.Y = Convert.ToSingle(list[1], CultureInfo.InvariantCulture);
        }

        private static float GetFloat(Dictionary<string, object> item, string key, float defaultValue)
        {
            object value;
            if (!item.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
