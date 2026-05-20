using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace BlueprintSystem
{
    internal static class BlueprintSourceMapper
    {
        public static BlueprintSource FromDictionary(Dictionary<string, object> data)
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = GetString(data, "schemaVersion");
            source.Name = GetString(data, "name");
            source.Category = GetString(data, "category");
            source.Description = GetString(data, "description");

            foreach (Dictionary<string, object> item in GetObjectArray(data, "variables"))
            {
                BlueprintVariableDeclaration variable = new BlueprintVariableDeclaration();
                variable.Id = GetString(item, "id");
                variable.Name = GetString(item, "name");
                variable.Type = GetString(item, "type");
                item.TryGetValue("defaultValue", out variable.DefaultValue);
                variable.Scope = GetString(item, "scope");
                variable.Exposed = GetBool(item, "exposed", false);
                variable.Persistent = GetBool(item, "persistent", false);
                variable.Description = GetString(item, "description");
                source.Variables.Add(variable);
            }

            foreach (Dictionary<string, object> item in GetObjectArray(data, "bindings"))
            {
                BlueprintBindingDeclaration binding = new BlueprintBindingDeclaration();
                binding.Name = GetString(item, "name");
                binding.Type = GetString(item, "type");
                binding.Required = GetBool(item, "required", false);
                source.Bindings.Add(binding);
            }

            foreach (Dictionary<string, object> item in GetObjectArray(data, "components"))
            {
                BlueprintComponentDeclaration component = new BlueprintComponentDeclaration();
                component.Name = GetString(item, "name");
                component.Blueprint = GetString(item, "blueprint");
                component.Required = GetBool(item, "required", true);
                source.Components.Add(component);
            }

            foreach (Dictionary<string, object> item in GetObjectArray(data, "nodes"))
            {
                BlueprintNodeSource node = new BlueprintNodeSource();
                node.Id = GetString(item, "id");
                node.TypeId = GetString(item, "typeId");
                ReadPosition(item, node);

                Dictionary<string, object> properties = GetObject(item, "properties");
                if (properties != null)
                {
                    foreach (KeyValuePair<string, object> pair in properties)
                    {
                        node.Properties[pair.Key] = pair.Value;
                    }
                }

                source.Nodes.Add(node);
            }

            foreach (Dictionary<string, object> item in GetObjectArray(data, "edges"))
            {
                BlueprintEdgeSource edge = new BlueprintEdgeSource();
                edge.From = GetString(item, "from");
                edge.To = GetString(item, "to");
                source.Edges.Add(edge);
            }

            return source;
        }

        public static Dictionary<string, object> ToDictionary(BlueprintSource source)
        {
            BlueprintVariableIdUtility.EnsureVariableIds(source);

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["schemaVersion"] = source.SchemaVersion;
            data["name"] = source.Name;
            if (!string.IsNullOrEmpty(source.Category))
            {
                data["category"] = source.Category;
            }

            if (!string.IsNullOrEmpty(source.Description))
            {
                data["description"] = source.Description;
            }

            List<object> variables = new List<object>();
            foreach (BlueprintVariableDeclaration variable in source.Variables)
            {
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["id"] = variable.Id;
                item["name"] = variable.Name;
                item["type"] = variable.Type;
                object defaultValue = variable.DefaultValue;
                object structuredDefaultValue;
                if (BlueprintArrayUtility.TryConvertToJsonArray(variable.DefaultValue, variable.Type, out structuredDefaultValue))
                {
                    defaultValue = structuredDefaultValue;
                }
                else if (BlueprintStructuredValueUtility.TryConvertToJsonValue(variable.DefaultValue, variable.Type, out structuredDefaultValue))
                {
                    defaultValue = structuredDefaultValue;
                }

                item["defaultValue"] = defaultValue;
                if (!string.IsNullOrEmpty(variable.Scope))
                {
                    item["scope"] = variable.Scope;
                }

                if (variable.Exposed)
                {
                    item["exposed"] = true;
                }

                if (variable.Persistent)
                {
                    item["persistent"] = true;
                }

                if (!string.IsNullOrEmpty(variable.Description))
                {
                    item["description"] = variable.Description;
                }

                variables.Add(item);
            }

            data["variables"] = variables;

            List<object> bindings = new List<object>();
            foreach (BlueprintBindingDeclaration binding in source.Bindings)
            {
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["name"] = binding.Name;
                item["type"] = binding.Type;
                item["required"] = binding.Required;
                bindings.Add(item);
            }

            data["bindings"] = bindings;

            List<object> components = new List<object>();
            foreach (BlueprintComponentDeclaration component in source.Components)
            {
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["name"] = component.Name;
                item["blueprint"] = component.Blueprint;
                item["required"] = component.Required;
                components.Add(item);
            }

            data["components"] = components;

            List<object> nodes = new List<object>();
            foreach (BlueprintNodeSource node in source.Nodes)
            {
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["id"] = node.Id;
                item["typeId"] = node.TypeId;
                item["position"] = new List<object> { node.X, node.Y };
                item["properties"] = new Dictionary<string, object>(node.Properties);
                nodes.Add(item);
            }

            data["nodes"] = nodes;

            List<object> edges = new List<object>();
            foreach (BlueprintEdgeSource edge in source.Edges)
            {
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["from"] = edge.From;
                item["to"] = edge.To;
                edges.Add(item);
            }

            data["edges"] = edges;
            return data;
        }

        public static string GetString(Dictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null)
            {
                return null;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static bool GetBool(Dictionary<string, object> data, string key, bool defaultValue)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            bool parsed;
            return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) ? parsed : defaultValue;
        }

        public static Dictionary<string, object> GetObject(Dictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value))
            {
                return null;
            }

            return value as Dictionary<string, object>;
        }

        public static List<Dictionary<string, object>> GetObjectArray(Dictionary<string, object> data, string key)
        {
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            object value;
            if (!data.TryGetValue(key, out value) || value == null)
            {
                return result;
            }

            IEnumerable array = value as IEnumerable;
            if (array == null || value is string)
            {
                return result;
            }

            foreach (object item in array)
            {
                Dictionary<string, object> dictionary = item as Dictionary<string, object>;
                if (dictionary != null)
                {
                    result.Add(dictionary);
                }
            }

            return result;
        }

        private static void ReadPosition(Dictionary<string, object> item, BlueprintNodeSource node)
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
    }
}
