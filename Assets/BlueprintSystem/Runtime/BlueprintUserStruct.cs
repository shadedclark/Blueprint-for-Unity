using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

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
    public sealed class CompiledStructLayout
    {
        private readonly string typeId;
        private readonly List<CompiledStructFieldLayoutRecord> fieldRecords;

        internal CompiledStructLayout(BlueprintUserStructDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            typeId = definition.TypeId;
            fieldRecords = new List<CompiledStructFieldLayoutRecord>(definition.Fields.Count);
            for (int i = 0; i < definition.Fields.Count; i++)
            {
                BlueprintUserStructField field = definition.Fields[i];
                fieldRecords.Add(new CompiledStructFieldLayoutRecord
                {
                    StableIndex = i,
                    IdStableId = BlueprintStableId.FromString(field == null ? null : field.Id),
                    NameStableId = BlueprintStableId.FromString(field == null ? null : field.Name),
                    Definition = field
                });
            }
        }

        public string TypeId
        {
            get { return typeId; }
        }

        public int FieldCount
        {
            get { return fieldRecords.Count; }
        }

        public IReadOnlyList<BlueprintUserStructField> FieldDefinitions
        {
            get
            {
                List<BlueprintUserStructField> definitions = new List<BlueprintUserStructField>(fieldRecords.Count);
                for (int i = 0; i < fieldRecords.Count; i++) definitions.Add(fieldRecords[i].Definition);
                return definitions;
            }
        }

        public bool TryGetFieldIndex(string nameOrId, out int fieldIndex)
        {
            fieldIndex = -1;
            if (string.IsNullOrEmpty(nameOrId)) return false;
            int stableId = BlueprintStableId.FromString(nameOrId);
            for (int i = 0; i < fieldRecords.Count; i++)
            {
                CompiledStructFieldLayoutRecord record = fieldRecords[i];
                BlueprintUserStructField field = record.Definition;
                if (field != null &&
                    ((record.IdStableId == stableId && string.Equals(field.Id, nameOrId, StringComparison.Ordinal)) ||
                     (record.NameStableId == stableId && string.Equals(field.Name, nameOrId, StringComparison.Ordinal))))
                {
                    fieldIndex = record.StableIndex;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetFieldIndexById(string fieldId, out int fieldIndex)
        {
            fieldIndex = -1;
            if (string.IsNullOrEmpty(fieldId)) return false;
            int stableId = BlueprintStableId.FromString(fieldId);
            for (int i = 0; i < fieldRecords.Count; i++)
            {
                CompiledStructFieldLayoutRecord record = fieldRecords[i];
                BlueprintUserStructField field = record.Definition;
                if (field != null && record.IdStableId == stableId && string.Equals(field.Id, fieldId, StringComparison.Ordinal))
                {
                    fieldIndex = record.StableIndex;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetFieldDefinition(int fieldIndex, out BlueprintUserStructField field)
        {
            field = null;
            if (fieldIndex < 0 || fieldIndex >= fieldRecords.Count)
            {
                return false;
            }

            field = fieldRecords[fieldIndex].Definition;
            return field != null;
        }

        internal static CompiledStructLayout CreateFallback(string fallbackTypeId, IDictionary<string, object> valuesByFieldId)
        {
            BlueprintUserStructDefinition definition = new BlueprintUserStructDefinition();
            definition.TypeId = fallbackTypeId;
            List<string> fieldIds = valuesByFieldId == null
                ? new List<string>()
                : new List<string>(valuesByFieldId.Keys);
            fieldIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < fieldIds.Count; i++)
            {
                definition.Fields.Add(new BlueprintUserStructField
                {
                    Id = fieldIds[i],
                    Name = fieldIds[i],
                    Type = "object"
                });
            }

            return new CompiledStructLayout(definition);
        }
    }

    internal sealed class CompiledStructFieldLayoutRecord
    {
        public int StableIndex;
        public int IdStableId;
        public int NameStableId;
        public BlueprintUserStructField Definition;
    }

    [Serializable]
    public class BlueprintStructValue
    {
        private readonly CompiledStructLayout layout;
        private readonly object[] fieldValueRecords;
        [NonSerialized] private BlueprintStructFieldValue[] legacyValues;

        public BlueprintStructValue(string typeId)
            : this(typeId, null)
        {
        }

        public BlueprintStructValue(string typeId, IDictionary<string, object> valuesByFieldId)
        {
            CompiledStructLayout resolvedLayout;
            if (!BlueprintUserStructRegistry.TryGetLayout(typeId, out resolvedLayout))
            {
                resolvedLayout = CompiledStructLayout.CreateFallback(typeId, valuesByFieldId);
            }

            layout = resolvedLayout;
            fieldValueRecords = new object[layout.FieldCount];
            if (valuesByFieldId != null)
            {
                foreach (KeyValuePair<string, object> pair in valuesByFieldId)
                {
                    int fieldIndex;
                    if (layout.TryGetFieldIndexById(pair.Key, out fieldIndex))
                    {
                        fieldValueRecords[fieldIndex] = pair.Value;
                    }
                }
            }
        }

        internal BlueprintStructValue(CompiledStructLayout layout, object[] fieldValueRecords, bool takeOwnership)
        {
            if (layout == null)
            {
                throw new ArgumentNullException("layout");
            }

            if (fieldValueRecords == null || fieldValueRecords.Length != layout.FieldCount)
            {
                throw new ArgumentException("Field value record count must match the compiled struct layout.", "fieldValueRecords");
            }

            this.layout = layout;
            this.fieldValueRecords = takeOwnership ? fieldValueRecords : (object[])fieldValueRecords.Clone();
        }

        public string TypeId
        {
            get { return layout.TypeId; }
        }

        public CompiledStructLayout Layout
        {
            get { return layout; }
        }

        public IReadOnlyList<object> FieldValueRecords
        {
            get { return fieldValueRecords; }
        }

        public IReadOnlyList<BlueprintStructFieldValue> Values
        {
            get
            {
                if (legacyValues == null)
                {
                    legacyValues = new BlueprintStructFieldValue[layout.FieldCount];
                    for (int i = 0; i < legacyValues.Length; i++)
                    {
                        BlueprintUserStructField field;
                        layout.TryGetFieldDefinition(i, out field);
                        legacyValues[i] = new BlueprintStructFieldValue
                        {
                            FieldId = field == null ? string.Empty : field.Id,
                            Value = fieldValueRecords[i]
                        };
                    }
                }

                return legacyValues;
            }
        }

        public bool TryGetValue(string nameOrId, out object value)
        {
            value = null;
            int fieldIndex;
            return layout.TryGetFieldIndex(nameOrId, out fieldIndex) && TryGetValue(fieldIndex, out value);
        }

        public bool TryGetValue(int fieldIndex, out object value)
        {
            value = null;
            if (fieldIndex < 0 || fieldIndex >= fieldValueRecords.Length)
            {
                return false;
            }

            value = fieldValueRecords[fieldIndex];
            return true;
        }

        public BlueprintStructValue WithValue(string nameOrId, object value)
        {
            int fieldIndex;
            return layout.TryGetFieldIndex(nameOrId, out fieldIndex) ? WithValue(fieldIndex, value) : null;
        }

        public BlueprintStructValue WithValue(int fieldIndex, object value)
        {
            if (fieldIndex < 0 || fieldIndex >= fieldValueRecords.Length)
            {
                return null;
            }

            object[] copiedValues = (object[])fieldValueRecords.Clone();
            copiedValues[fieldIndex] = value;
            return CreateCopy(copiedValues);
        }

        public Dictionary<string, object> ToFieldIdDictionary()
        {
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < layout.FieldCount; i++)
            {
                BlueprintUserStructField field;
                if (layout.TryGetFieldDefinition(i, out field) && !string.IsNullOrEmpty(field.Id))
                {
                    result[field.Id] = fieldValueRecords[i];
                }
            }

            return result;
        }

        public override bool Equals(object obj)
        {
            BlueprintStructValue other = obj as BlueprintStructValue;
            if (other == null || TypeId != other.TypeId || fieldValueRecords.Length != other.fieldValueRecords.Length)
            {
                return false;
            }

            if (ReferenceEquals(layout, other.layout))
            {
                for (int i = 0; i < fieldValueRecords.Length; i++)
                {
                    if (!object.Equals(fieldValueRecords[i], other.fieldValueRecords[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            for (int i = 0; i < layout.FieldCount; i++)
            {
                BlueprintUserStructField field;
                int otherIndex;
                if (!layout.TryGetFieldDefinition(i, out field) ||
                    !other.layout.TryGetFieldIndexById(field.Id, out otherIndex) ||
                    !object.Equals(fieldValueRecords[i], other.fieldValueRecords[otherIndex]))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TypeId == null ? 0 : TypeId.GetHashCode();
                for (int i = 0; i < fieldValueRecords.Length; i++)
                {
                    BlueprintUserStructField field;
                    layout.TryGetFieldDefinition(i, out field);
                    int fieldHash = field == null || field.Id == null ? 0 : field.Id.GetHashCode();
                    int valueHash = fieldValueRecords[i] == null ? 0 : fieldValueRecords[i].GetHashCode();
                    hash ^= fieldHash * 397 ^ valueHash;
                }

                return hash;
            }
        }

        protected virtual BlueprintStructValue CreateCopy(object[] copiedValues)
        {
            return new BlueprintStructValue(layout, copiedValues, true);
        }
    }

    [Serializable]
    public sealed class RuntimeStructRecord : BlueprintStructValue
    {
        internal RuntimeStructRecord(CompiledStructLayout layout, object[] fieldValueRecords, bool takeOwnership)
            : base(layout, fieldValueRecords, takeOwnership)
        {
        }

        protected override BlueprintStructValue CreateCopy(object[] copiedValues)
        {
            return new RuntimeStructRecord(Layout, copiedValues, true);
        }
    }

    public static class BlueprintUserStructRegistry
    {
        public const string StructAssetExtension = ".bpstruct.json";
        public const string DefaultAssetRoot = "Assets/BlueprintSystem/Specs/Structs";

        private static readonly object CacheLock = new object();
        private static Dictionary<string, BlueprintUserStructDefinition> definitionsByTypeId;
        private static Dictionary<string, CompiledStructLayout> layoutsByTypeId;

        public static bool TryGet(string typeId, out BlueprintUserStructDefinition definition)
        {
            definition = null;
            EnsureLoaded();
            return !string.IsNullOrEmpty(typeId) && definitionsByTypeId.TryGetValue(typeId, out definition);
        }

        public static bool TryGetLayout(string typeId, out CompiledStructLayout layout)
        {
            layout = null;
            EnsureLoaded();
            return !string.IsNullOrEmpty(typeId) && layoutsByTypeId.TryGetValue(typeId, out layout);
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
                layoutsByTypeId = null;
            }

            BlueprintRuntimeRegistry.Refresh();
            BlueprintVariableTypeRegistry.Refresh();
        }

        private static void EnsureLoaded()
        {
            if (definitionsByTypeId != null && layoutsByTypeId != null)
            {
                return;
            }

            lock (CacheLock)
            {
                if (definitionsByTypeId != null && layoutsByTypeId != null)
                {
                    return;
                }

                Dictionary<string, BlueprintUserStructDefinition> loadedDefinitions = LoadDefinitions();
                Dictionary<string, CompiledStructLayout> compiledLayouts = CompileLayouts(loadedDefinitions);
                layoutsByTypeId = compiledLayouts;
                definitionsByTypeId = loadedDefinitions;
            }
        }

        private static Dictionary<string, CompiledStructLayout> CompileLayouts(
            IDictionary<string, BlueprintUserStructDefinition> definitions)
        {
            Dictionary<string, CompiledStructLayout> result =
                new Dictionary<string, CompiledStructLayout>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, BlueprintUserStructDefinition> pair in definitions)
            {
                result[pair.Key] = new CompiledStructLayout(pair.Value);
            }

            return result;
        }

        private static Dictionary<string, BlueprintUserStructDefinition> LoadDefinitions()
        {
            Dictionary<string, BlueprintUserStructDefinition> result = new Dictionary<string, BlueprintUserStructDefinition>(StringComparer.Ordinal);
            string[] typeIds = BlueprintRuntimeRegistry.GetUserStructTypeIds();
            for (int i = 0; i < typeIds.Length; i++)
            {
                BlueprintUserStructDefinition definition;
                if (BlueprintRuntimeRegistry.TryGetUserStructDefinition(typeIds[i], out definition) &&
                    IsValidDefinition(definition))
                {
                    result[definition.TypeId] = definition;
                }
            }

            return result;
        }

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
            object assetGuidValue;
            if (properties != null &&
                properties.TryGetValue(StructAssetGuidPropertyId, out assetGuidValue) &&
                assetGuidValue != null)
            {
                string assetGuid = Convert.ToString(assetGuidValue, CultureInfo.InvariantCulture);
                if (BlueprintRuntimeRegistry.TryResolveUserStructGuid(assetGuid, out structTypeId, out definition))
                {
                    return true;
                }
            }

            structTypeId = GetStructTypeId(properties);
            if (string.IsNullOrEmpty(structTypeId))
            {
                definition = null;
                return false;
            }

            return BlueprintUserStructRegistry.TryGet(structTypeId, out definition);
        }

        public static bool TryResolveLayout(
            IDictionary<string, object> properties,
            out string structTypeId,
            out CompiledStructLayout layout)
        {
            layout = null;
            BlueprintUserStructDefinition ignoredDefinition;
            return TryResolveDefinition(properties, out structTypeId, out ignoredDefinition) &&
                   BlueprintUserStructRegistry.TryGetLayout(structTypeId, out layout);
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
            CompiledStructLayout layout;
            if (!BlueprintUserStructRegistry.TryGetLayout(typeId, out layout))
            {
                return false;
            }

            return TryConvertToRuntimeValue(value, layout, out runtimeValue);
        }

        public static bool TryConvertToRuntimeValue(object value, CompiledStructLayout layout, out object runtimeValue)
        {
            runtimeValue = null;
            if (layout == null)
            {
                return false;
            }

            string typeId = layout.TypeId;

            RuntimeStructRecord runtimeRecord = value as RuntimeStructRecord;
            if (runtimeRecord != null && runtimeRecord.TypeId == typeId)
            {
                runtimeValue = runtimeRecord;
                return true;
            }

            BlueprintStructValue legacyStructValue = value as BlueprintStructValue;
            if (legacyStructValue != null && legacyStructValue.TypeId != typeId)
            {
                return false;
            }

            IDictionary<string, object> dictionary = null;
            if (legacyStructValue == null && value == null)
            {
                dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
            }
            else if (legacyStructValue == null)
            {
                dictionary = NormalizeDictionary(value);
                if (dictionary == null)
                {
                    return false;
                }
            }

            object typeValue;
            if (dictionary != null && dictionary.TryGetValue(TypeKey, out typeValue) && typeValue != null &&
                Convert.ToString(typeValue, CultureInfo.InvariantCulture) != typeId)
            {
                return false;
            }

            IDictionary<string, object> fieldsDictionary = dictionary;
            object nestedFields;
            if (dictionary != null && dictionary.TryGetValue(FieldsKey, out nestedFields))
            {
                fieldsDictionary = NormalizeDictionary(nestedFields);
                if (fieldsDictionary == null)
                {
                    return false;
                }
            }

            if (fieldsDictionary != null)
            {
                foreach (string key in fieldsDictionary.Keys)
                {
                    if (key == TypeKey || key == FieldsKey)
                    {
                        continue;
                    }

                    int ignoredIndex;
                    if (!layout.TryGetFieldIndex(key, out ignoredIndex))
                    {
                        return false;
                    }
                }
            }

            object[] runtimeValues = new object[layout.FieldCount];
            for (int i = 0; i < layout.FieldCount; i++)
            {
                BlueprintUserStructField field;
                if (!layout.TryGetFieldDefinition(i, out field))
                {
                    return false;
                }

                if (field.Deprecated)
                {
                    continue;
                }

                object rawValue;
                if (legacyStructValue != null)
                {
                    if (!legacyStructValue.TryGetValue(field.Id, out rawValue))
                    {
                        rawValue = field.DefaultValue;
                    }
                }
                else if (!fieldsDictionary.TryGetValue(field.Id, out rawValue) &&
                         !fieldsDictionary.TryGetValue(field.Name, out rawValue))
                {
                    rawValue = field.DefaultValue;
                }

                object fieldValue;
                if (!TryConvertFieldToRuntimeValue(rawValue, field.Type, out fieldValue))
                {
                    return false;
                }

                runtimeValues[i] = fieldValue;
            }

            runtimeValue = new RuntimeStructRecord(layout, runtimeValues, true);
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

            RuntimeStructRecord structValue = runtimeValue as RuntimeStructRecord;
            if (structValue == null)
            {
                return false;
            }

            Dictionary<string, object> dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < structValue.Layout.FieldCount; i++)
            {
                BlueprintUserStructField field;
                if (!structValue.Layout.TryGetFieldDefinition(i, out field))
                {
                    return false;
                }

                if (field.Deprecated)
                {
                    continue;
                }

                object fieldValue;
                if (!structValue.TryGetValue(i, out fieldValue))
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

        public static bool TrySetFieldValue(
            RuntimeStructRecord source,
            string nameOrId,
            object value,
            out RuntimeStructRecord updated)
        {
            updated = null;
            if (source == null)
            {
                return false;
            }

            int fieldIndex;
            BlueprintUserStructField field;
            if (!source.Layout.TryGetFieldIndex(nameOrId, out fieldIndex) ||
                !source.Layout.TryGetFieldDefinition(fieldIndex, out field) ||
                field.Deprecated)
            {
                return false;
            }

            object convertedValue;
            if (!TryConvertFieldToRuntimeValue(value, field.Type, out convertedValue))
            {
                return false;
            }

            updated = source.WithValue(fieldIndex, convertedValue) as RuntimeStructRecord;
            return updated != null;
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
