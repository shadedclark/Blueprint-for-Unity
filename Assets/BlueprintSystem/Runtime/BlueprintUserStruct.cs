using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BlueprintSystem
{
    [Serializable]
    public sealed class BlueprintUserStructField
    {
        public string Id;
        public string Name;
        public string Type;
        public object DefaultValue;
        public string Description;
        public bool Deprecated;
    }

    [Serializable]
    public sealed class BlueprintUserStructDefinition
    {
        public string SchemaVersion;
        public string TypeId;
        public string DisplayName;
        public string Description;
        public readonly List<BlueprintUserStructField> Fields = new List<BlueprintUserStructField>();

        public static BlueprintUserStructDefinition FromJson(string json)
        {
            return FromDictionary(BlueprintJson.DeserializeObject(json));
        }

        public bool TryGetField(string nameOrId, out BlueprintUserStructField field)
        {
            field = null;
            if (string.IsNullOrEmpty(nameOrId))
            {
                return false;
            }

            for (int i = 0; i < Fields.Count; i++)
            {
                BlueprintUserStructField candidate = Fields[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.Id == nameOrId || candidate.Name == nameOrId)
                {
                    field = candidate;
                    return true;
                }
            }

            return false;
        }

        internal static BlueprintUserStructDefinition FromDictionary(Dictionary<string, object> data)
        {
            BlueprintUserStructDefinition definition = new BlueprintUserStructDefinition();
            definition.SchemaVersion = BlueprintSourceMapper.GetString(data, "schemaVersion");
            definition.TypeId = BlueprintSourceMapper.GetString(data, "typeId");
            definition.DisplayName = definition.TypeId;
            definition.Description = BlueprintSourceMapper.GetString(data, "description");

            foreach (Dictionary<string, object> item in BlueprintSourceMapper.GetObjectArray(data, "fields"))
            {
                BlueprintUserStructField field = new BlueprintUserStructField();
                field.Id = BlueprintSourceMapper.GetString(item, "id");
                field.Name = BlueprintSourceMapper.GetString(item, "name");
                field.Type = BlueprintSourceMapper.GetString(item, "type");
                item.TryGetValue("defaultValue", out field.DefaultValue);
                field.Description = BlueprintSourceMapper.GetString(item, "description");
                field.Deprecated = BlueprintSourceMapper.GetBool(item, "deprecated", false);
                definition.Fields.Add(field);
            }

            return definition;
        }
    }

    [Serializable]
    public sealed class BlueprintStructFieldValue
    {
        public string FieldId;
        public object Value;
    }

    [Serializable]
    public sealed class BlueprintStructValue
    {
        [SerializeField] private string typeId;
        [SerializeField] private List<BlueprintStructFieldValue> values = new List<BlueprintStructFieldValue>();

        public BlueprintStructValue(string typeId)
        {
            this.typeId = typeId;
        }

        public BlueprintStructValue(string typeId, IDictionary<string, object> valuesByFieldId)
            : this(typeId)
        {
            if (valuesByFieldId == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in valuesByFieldId)
            {
                SetByFieldId(pair.Key, pair.Value);
            }
        }

        public string TypeId
        {
            get { return typeId; }
        }

        public IReadOnlyList<BlueprintStructFieldValue> Values
        {
            get { return values; }
        }

        public bool TryGetValue(string nameOrId, out object value)
        {
            value = null;
            BlueprintUserStructDefinition definition;
            if (BlueprintUserStructRegistry.TryGet(typeId, out definition))
            {
                BlueprintUserStructField field;
                if (!definition.TryGetField(nameOrId, out field))
                {
                    return false;
                }

                return TryGetByFieldId(field.Id, out value);
            }

            return TryGetByFieldId(nameOrId, out value);
        }

        public BlueprintStructValue WithValue(string nameOrId, object value)
        {
            BlueprintUserStructDefinition definition;
            if (BlueprintUserStructRegistry.TryGet(typeId, out definition))
            {
                BlueprintUserStructField field;
                if (!definition.TryGetField(nameOrId, out field))
                {
                    return null;
                }

                return WithFieldIdValue(field.Id, value);
            }

            return WithFieldIdValue(nameOrId, value);
        }

        public Dictionary<string, object> ToFieldIdDictionary()
        {
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                BlueprintStructFieldValue fieldValue = values[i];
                if (fieldValue != null && !string.IsNullOrEmpty(fieldValue.FieldId))
                {
                    result[fieldValue.FieldId] = fieldValue.Value;
                }
            }

            return result;
        }

        private BlueprintStructValue WithFieldIdValue(string fieldId, object value)
        {
            if (string.IsNullOrEmpty(fieldId))
            {
                return null;
            }

            BlueprintStructValue clone = new BlueprintStructValue(typeId, ToFieldIdDictionary());
            clone.SetByFieldId(fieldId, value);
            return clone;
        }

        private bool TryGetByFieldId(string fieldId, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(fieldId))
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                BlueprintStructFieldValue fieldValue = values[i];
                if (fieldValue != null && fieldValue.FieldId == fieldId)
                {
                    value = fieldValue.Value;
                    return true;
                }
            }

            return false;
        }

        private void SetByFieldId(string fieldId, object value)
        {
            if (string.IsNullOrEmpty(fieldId))
            {
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                BlueprintStructFieldValue fieldValue = values[i];
                if (fieldValue != null && fieldValue.FieldId == fieldId)
                {
                    fieldValue.Value = value;
                    return;
                }
            }

            values.Add(new BlueprintStructFieldValue
            {
                FieldId = fieldId,
                Value = value
            });
        }
    }

    public static class BlueprintUserStructRegistry
    {
        public const string StructAssetExtension = ".bpstruct.json";
        public const string DefaultAssetRoot = "Assets/BlueprintSystem/Specs/Structs";

        private static readonly object CacheLock = new object();
        private static Dictionary<string, BlueprintUserStructDefinition> definitionsByTypeId;

        public static bool TryGet(string typeId, out BlueprintUserStructDefinition definition)
        {
            definition = null;
            EnsureLoaded();
            return !string.IsNullOrEmpty(typeId) && definitionsByTypeId.TryGetValue(typeId, out definition);
        }

        public static bool IsUserStructType(string typeId)
        {
            BlueprintUserStructDefinition ignored;
            return TryGet(typeId, out ignored);
        }

        public static string[] GetTypeIds()
        {
            EnsureLoaded();
            List<string> typeIds = new List<string>(definitionsByTypeId.Keys);
            typeIds.Sort(StringComparer.Ordinal);
            return typeIds.ToArray();
        }

        public static void Refresh()
        {
            lock (CacheLock)
            {
                definitionsByTypeId = null;
            }

            BlueprintVariableTypeRegistry.Refresh();
        }

        private static void EnsureLoaded()
        {
            if (definitionsByTypeId != null)
            {
                return;
            }

            lock (CacheLock)
            {
                if (definitionsByTypeId != null)
                {
                    return;
                }

                definitionsByTypeId = LoadDefinitions();
            }
        }

        private static Dictionary<string, BlueprintUserStructDefinition> LoadDefinitions()
        {
            Dictionary<string, BlueprintUserStructDefinition> result = new Dictionary<string, BlueprintUserStructDefinition>(StringComparer.Ordinal);
#if UNITY_EDITOR
            LoadEditorJsonDefinitions(result);
            LoadEditorAssetDefinitions(result);
#else
            LoadRuntimeJsonDefinitions(result);
#endif
            return result;
        }

        private static void LoadRuntimeJsonDefinitions(Dictionary<string, BlueprintUserStructDefinition> result)
        {
            string root = GetAbsoluteStructRoot();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(root, "*" + StructAssetExtension, SearchOption.AllDirectories);
            }
            catch
            {
                return;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    BlueprintUserStructDefinition definition = BlueprintUserStructDefinition.FromJson(File.ReadAllText(files[i]));
                    if (IsValidDefinition(definition) && !result.ContainsKey(definition.TypeId))
                    {
                        result.Add(definition.TypeId, definition);
                    }
                }
                catch
                {
                }
            }
        }

#if UNITY_EDITOR
        private static void LoadEditorJsonDefinitions(Dictionary<string, BlueprintUserStructDefinition> result)
        {
            if (result == null)
            {
                return;
            }

            List<string> paths = BlueprintAssetDiscovery.FindTextAssetPaths(StructAssetExtension);
            for (int i = 0; i < paths.Count; i++)
            {
                TextAsset structJson = AssetDatabase.LoadAssetAtPath<TextAsset>(paths[i]);
                if (structJson == null)
                {
                    continue;
                }

                try
                {
                    BlueprintUserStructDefinition definition = BlueprintUserStructDefinition.FromJson(structJson.text);
                    if (IsValidDefinition(definition))
                    {
                        result[definition.TypeId] = definition;
                    }
                }
                catch
                {
                }
            }
        }

        private static void LoadEditorAssetDefinitions(Dictionary<string, BlueprintUserStructDefinition> result)
        {
            if (result == null)
            {
                return;
            }

            List<string> paths = BlueprintAssetDiscovery.FindAssetPaths("t:BlueprintUserStructAsset");
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                BlueprintUserStructAsset asset = AssetDatabase.LoadAssetAtPath<BlueprintUserStructAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                BlueprintUserStructDefinition definition = asset.ToDefinition();
                if (IsValidDefinition(definition))
                {
                    result[definition.TypeId] = definition;
                }
            }
        }
#endif

        private static bool IsValidDefinition(BlueprintUserStructDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.TypeId))
            {
                return false;
            }

            HashSet<string> fieldIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> fieldNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Fields.Count; i++)
            {
                BlueprintUserStructField field = definition.Fields[i];
                if (field == null || string.IsNullOrEmpty(field.Id) || string.IsNullOrEmpty(field.Name) || string.IsNullOrEmpty(field.Type))
                {
                    return false;
                }

                if (!fieldIds.Add(field.Id) || !fieldNames.Add(field.Name))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetAbsoluteStructRoot()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
            {
                return null;
            }

            return Path.Combine(dataPath, "BlueprintSystem/Specs/Structs");
        }
    }

    public static class BlueprintBreakStructNodeUtility
    {
        public const string NodeTypeId = "Variable.BreakStruct";
        public const string ExecutorId = "Variable.BreakStruct";
        public const string TargetPortId = "target";
        public const string StructTypePropertyId = "structTypeId";
        public const string StructAssetGuidPropertyId = "structAssetGuid";

        public static string GetStructTypeId(BlueprintNodeSource node)
        {
            return node == null ? null : GetStructTypeId(node.Properties);
        }

        public static string GetStructTypeId(IDictionary<string, object> properties)
        {
            object value;
            if (properties != null && properties.TryGetValue(StructTypePropertyId, out value) && value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return null;
        }

        public static bool TryResolveDefinition(BlueprintNodeSource node, out string structTypeId, out BlueprintUserStructDefinition definition)
        {
            return TryResolveDefinition(node == null ? null : node.Properties, out structTypeId, out definition);
        }

        public static bool TryResolveDefinition(IDictionary<string, object> properties, out string structTypeId, out BlueprintUserStructDefinition definition)
        {
#if UNITY_EDITOR
            object assetGuidValue;
            if (properties != null &&
                properties.TryGetValue(StructAssetGuidPropertyId, out assetGuidValue) &&
                assetGuidValue != null)
            {
                string assetGuid = Convert.ToString(assetGuidValue, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(assetGuid))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                    BlueprintUserStructAsset asset = string.IsNullOrEmpty(assetPath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<BlueprintUserStructAsset>(assetPath);
                    if (asset != null)
                    {
                        structTypeId = asset.TypeId;
                        definition = asset.ToDefinition();
                        return !string.IsNullOrEmpty(structTypeId) && definition != null;
                    }
                }
            }
#endif

            structTypeId = GetStructTypeId(properties);
            if (string.IsNullOrEmpty(structTypeId))
            {
                definition = null;
                return false;
            }

            return BlueprintUserStructRegistry.TryGet(structTypeId, out definition);
        }

        public static bool TryCreateOutputPort(BlueprintNodeSource node, string outputPortId, out BlueprintPortSpec port)
        {
            return TryCreateOutputPort(node == null ? null : node.Properties, outputPortId, out port);
        }

        public static bool TryCreateOutputPort(IDictionary<string, object> properties, string outputPortId, out BlueprintPortSpec port)
        {
            port = null;
            string structTypeId;
            BlueprintUserStructDefinition definition;
            if (!TryResolveDefinition(properties, out structTypeId, out definition))
            {
                return false;
            }

            BlueprintUserStructField field;
            if (!TryGetFieldById(definition, outputPortId, out field))
            {
                return false;
            }

            port = new BlueprintPortSpec
            {
                Id = field.Id,
                Kind = BlueprintPortKind.Value,
                Direction = BlueprintPortDirection.Output,
                Type = field.Type,
                Required = false,
                Source = BlueprintValueSource.None,
                AllowMultiple = false
            };
            return true;
        }

        public static bool TryGetFieldById(BlueprintUserStructDefinition definition, string fieldId, out BlueprintUserStructField field)
        {
            field = null;
            if (definition == null || string.IsNullOrEmpty(fieldId))
            {
                return false;
            }

            for (int i = 0; i < definition.Fields.Count; i++)
            {
                BlueprintUserStructField candidate = definition.Fields[i];
                if (candidate != null && !candidate.Deprecated && candidate.Id == fieldId)
                {
                    field = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    public static class BlueprintUserStructUtility
    {
        private const string TypeKey = "$type";
        private const string FieldsKey = "$fields";

        public static bool TryConvertToRuntimeValue(object value, string typeId, out object runtimeValue)
        {
            runtimeValue = null;
            BlueprintUserStructDefinition definition;
            if (!BlueprintUserStructRegistry.TryGet(typeId, out definition))
            {
                return false;
            }

            BlueprintStructValue structValue = value as BlueprintStructValue;
            if (structValue != null && structValue.TypeId == typeId)
            {
                value = structValue.ToFieldIdDictionary();
            }

            IDictionary<string, object> dictionary = null;
            if (value == null)
            {
                dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
            }
            else
            {
                dictionary = NormalizeDictionary(value);
                if (dictionary == null)
                {
                    return false;
                }
            }

            object typeValue;
            if (dictionary.TryGetValue(TypeKey, out typeValue) && typeValue != null &&
                Convert.ToString(typeValue, CultureInfo.InvariantCulture) != typeId)
            {
                return false;
            }

            IDictionary<string, object> fieldsDictionary = dictionary;
            object nestedFields;
            if (dictionary.TryGetValue(FieldsKey, out nestedFields))
            {
                fieldsDictionary = NormalizeDictionary(nestedFields);
                if (fieldsDictionary == null)
                {
                    return false;
                }
            }

            Dictionary<string, BlueprintUserStructField> fieldsById = new Dictionary<string, BlueprintUserStructField>(StringComparer.Ordinal);
            Dictionary<string, BlueprintUserStructField> fieldsByName = new Dictionary<string, BlueprintUserStructField>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Fields.Count; i++)
            {
                BlueprintUserStructField field = definition.Fields[i];
                fieldsById[field.Id] = field;
                fieldsByName[field.Name] = field;
            }

            foreach (string key in fieldsDictionary.Keys)
            {
                if (key == TypeKey || key == FieldsKey)
                {
                    continue;
                }

                if (!fieldsById.ContainsKey(key) && !fieldsByName.ContainsKey(key))
                {
                    return false;
                }
            }

            Dictionary<string, object> runtimeValues = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Fields.Count; i++)
            {
                BlueprintUserStructField field = definition.Fields[i];
                if (field.Deprecated)
                {
                    continue;
                }

                object rawValue;
                if (!fieldsDictionary.TryGetValue(field.Id, out rawValue) &&
                    !fieldsDictionary.TryGetValue(field.Name, out rawValue))
                {
                    rawValue = field.DefaultValue;
                }

                object fieldValue;
                if (!TryConvertFieldToRuntimeValue(rawValue, field.Type, out fieldValue))
                {
                    return false;
                }

                runtimeValues[field.Id] = fieldValue;
            }

            runtimeValue = new BlueprintStructValue(typeId, runtimeValues);
            return true;
        }

        public static bool TryConvertToJsonValue(object value, string typeId, out object jsonValue)
        {
            jsonValue = null;
            object runtimeValue;
            if (!TryConvertToRuntimeValue(value, typeId, out runtimeValue))
            {
                return false;
            }

            BlueprintStructValue structValue = runtimeValue as BlueprintStructValue;
            if (structValue == null)
            {
                return false;
            }

            BlueprintUserStructDefinition definition;
            if (!BlueprintUserStructRegistry.TryGet(typeId, out definition))
            {
                return false;
            }

            Dictionary<string, object> dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Fields.Count; i++)
            {
                BlueprintUserStructField field = definition.Fields[i];
                if (field.Deprecated)
                {
                    continue;
                }

                object fieldValue;
                if (!structValue.TryGetValue(field.Id, out fieldValue))
                {
                    fieldValue = field.DefaultValue;
                }

                object fieldJsonValue;
                if (!TryConvertFieldToJsonValue(fieldValue, field.Type, out fieldJsonValue))
                {
                    return false;
                }

                dictionary[field.Name] = fieldJsonValue;
            }

            jsonValue = dictionary;
            return true;
        }

        public static bool TryCreateDefaultRuntimeValue(string typeId, out object runtimeValue)
        {
            return TryConvertToRuntimeValue(null, typeId, out runtimeValue);
        }

        public static bool TryCreateDefaultJsonValue(string typeId, out object jsonValue)
        {
            object runtimeValue;
            if (!TryCreateDefaultRuntimeValue(typeId, out runtimeValue))
            {
                jsonValue = null;
                return false;
            }

            return TryConvertToJsonValue(runtimeValue, typeId, out jsonValue);
        }

        public static bool IsSupportedFieldType(string type)
        {
            if (string.IsNullOrEmpty(type) ||
                type == BlueprintVariableTypeRegistry.BlueprintRefTypeId ||
                type.StartsWith("Binding<", StringComparison.Ordinal))
            {
                return false;
            }

            string elementType;
            if (BlueprintArrayUtility.TryGetElementType(type, out elementType))
            {
                return BlueprintArrayUtility.IsSupportedElementType(elementType);
            }

            return BlueprintVariableTypeRegistry.IsKnownType(type);
        }

        private static bool TryConvertFieldToRuntimeValue(object value, string type, out object runtimeValue)
        {
            runtimeValue = value;
            if (value == null)
            {
                return TryGetDefaultRuntimeValue(type, out runtimeValue);
            }

            switch (type)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    if (!BlueprintTypeUtility.IsValueAssignableToType(value, type))
                    {
                        return false;
                    }

                    runtimeValue = BlueprintTypeUtility.ConvertValue(value, typeof(string), string.Empty);
                    return runtimeValue != null;
                case "bool":
                    if (!BlueprintTypeUtility.IsValueAssignableToType(value, type))
                    {
                        return false;
                    }

                    runtimeValue = BlueprintTypeUtility.ConvertValue(value, typeof(bool), false);
                    return true;
                case "int":
                    if (!BlueprintTypeUtility.IsValueAssignableToType(value, type))
                    {
                        return false;
                    }

                    runtimeValue = BlueprintTypeUtility.ConvertValue(value, typeof(int), 0);
                    return true;
                case "float":
                    if (!BlueprintTypeUtility.IsValueAssignableToType(value, type))
                    {
                        return false;
                    }

                    runtimeValue = BlueprintTypeUtility.ConvertValue(value, typeof(float), 0f);
                    return true;
                case "Vector2":
                    runtimeValue = value is Vector2 ? value : BlueprintTypeUtility.ToVector2(value, Vector2.zero);
                    return value is Vector2 || IsListLength(value, 2);
                case "Vector3":
                    runtimeValue = value is Vector3 ? value : BlueprintTypeUtility.ToVector3(value, Vector3.zero);
                    return value is Vector3 || IsListLength(value, 3);
                case "Vector4":
                    runtimeValue = value is Vector4 ? value : BlueprintTypeUtility.ToVector4(value, Vector4.zero);
                    return value is Vector4 || IsListLength(value, 4);
                case "Rect":
                    runtimeValue = value is Rect ? value : BlueprintTypeUtility.ToRect(value, Rect.zero);
                    return value is Rect || IsListLength(value, 4);
                case "Color":
                    runtimeValue = value is Color ? value : ToColor(value, Color.clear);
                    return value is Color || IsListLength(value, 3) || IsListLength(value, 4);
                default:
                    object arrayValue;
                    if (BlueprintArrayUtility.TryConvertToRuntimeArray(value, type, out arrayValue))
                    {
                        runtimeValue = arrayValue;
                        return true;
                    }

                    Type clrType;
                    if (BlueprintVariableTypeRegistry.TryGetClrType(type, out clrType) && clrType.IsEnum)
                    {
                        runtimeValue = BlueprintTypeUtility.ConvertValue(value, clrType, Activator.CreateInstance(clrType));
                        return runtimeValue != null;
                    }

                    object structuredValue;
                    if (BlueprintStructuredValueUtility.TryConvertToRuntimeValue(value, type, out structuredValue))
                    {
                        runtimeValue = structuredValue;
                        return true;
                    }

                    return false;
            }
        }

        private static bool TryConvertFieldToJsonValue(object value, string type, out object jsonValue)
        {
            jsonValue = value;
            if (value == null)
            {
                return true;
            }

            object arrayValue;
            if (BlueprintArrayUtility.TryConvertToJsonArray(value, type, out arrayValue))
            {
                jsonValue = arrayValue;
                return true;
            }

            object structuredValue;
            if (BlueprintStructuredValueUtility.TryConvertToJsonValue(value, type, out structuredValue))
            {
                jsonValue = structuredValue;
                return true;
            }

            if (value.GetType().IsEnum)
            {
                jsonValue = value.ToString();
                return true;
            }

            if (value is Vector2)
            {
                Vector2 vector = (Vector2)value;
                jsonValue = new List<object> { vector.x, vector.y };
                return true;
            }

            if (value is Vector3)
            {
                Vector3 vector = (Vector3)value;
                jsonValue = new List<object> { vector.x, vector.y, vector.z };
                return true;
            }

            if (value is Vector4)
            {
                Vector4 vector = (Vector4)value;
                jsonValue = new List<object> { vector.x, vector.y, vector.z, vector.w };
                return true;
            }

            if (value is Rect)
            {
                Rect rect = (Rect)value;
                jsonValue = new List<object> { rect.x, rect.y, rect.width, rect.height };
                return true;
            }

            if (value is Color)
            {
                Color color = (Color)value;
                jsonValue = new List<object> { color.r, color.g, color.b, color.a };
                return true;
            }

            return BlueprintTypeUtility.IsValueAssignableToType(value, type);
        }

        private static bool TryGetDefaultRuntimeValue(string type, out object value)
        {
            value = null;
            switch (type)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    value = string.Empty;
                    return true;
                case "bool":
                    value = false;
                    return true;
                case "int":
                    value = 0;
                    return true;
                case "float":
                    value = 0f;
                    return true;
                case "Vector2":
                    value = Vector2.zero;
                    return true;
                case "Vector3":
                    value = Vector3.zero;
                    return true;
                case "Vector4":
                    value = Vector4.zero;
                    return true;
                case "Rect":
                    value = new Rect();
                    return true;
                case "Color":
                    value = Color.clear;
                    return true;
                default:
                    string elementType;
                    if (BlueprintArrayUtility.TryGetElementType(type, out elementType) &&
                        BlueprintArrayUtility.IsSupportedElementType(elementType))
                    {
                        value = new List<object>();
                        return true;
                    }

                    Type clrType;
                    if (BlueprintVariableTypeRegistry.TryGetClrType(type, out clrType) && clrType.IsEnum)
                    {
                        value = Activator.CreateInstance(clrType);
                        return true;
                    }

                    return BlueprintStructuredValueUtility.TryCreateDefaultRuntimeValue(type, out value);
            }
        }

        private static IDictionary<string, object> NormalizeDictionary(object value)
        {
            IDictionary<string, object> typedDictionary = value as IDictionary<string, object>;
            if (typedDictionary != null)
            {
                return typedDictionary;
            }

            IDictionary genericDictionary = value as IDictionary;
            if (genericDictionary == null)
            {
                return null;
            }

            Dictionary<string, object> normalized = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in genericDictionary)
            {
                normalized[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value;
            }

            return normalized;
        }

        private static Color ToColor(object value, Color defaultValue)
        {
            IList list = value as IList;
            if (list == null || list.Count < 3)
            {
                return defaultValue;
            }

            float r = Convert.ToSingle(list[0], CultureInfo.InvariantCulture);
            float g = Convert.ToSingle(list[1], CultureInfo.InvariantCulture);
            float b = Convert.ToSingle(list[2], CultureInfo.InvariantCulture);
            float a = list.Count >= 4 ? Convert.ToSingle(list[3], CultureInfo.InvariantCulture) : defaultValue.a;
            return new Color(r, g, b, a);
        }

        private static bool IsListLength(object value, int length)
        {
            IList list = value as IList;
            return list != null && list.Count == length;
        }
    }
}
