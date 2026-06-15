using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace BlueprintSystem
{
    public enum BlueprintUserStructAssetFieldType
    {
        String,
        Bool,
        Int,
        Float,
        Vector2,
        Vector3,
        Vector4,
        Color,
        Rect,
        ForceMode,
        ForceMode2D,
        LoadSceneMode,
        Key,
        ComparisonMode,
        TickPhase,
        Blueprint
    }

    public static class BlueprintUserStructAssetFieldTypes
    {
        public static string ToTypeId(BlueprintUserStructAssetFieldType type)
        {
            switch (type)
            {
                case BlueprintUserStructAssetFieldType.String:
                    return "string";
                case BlueprintUserStructAssetFieldType.Bool:
                    return "bool";
                case BlueprintUserStructAssetFieldType.Int:
                    return "int";
                case BlueprintUserStructAssetFieldType.Float:
                    return "float";
                case BlueprintUserStructAssetFieldType.Vector2:
                    return "Vector2";
                case BlueprintUserStructAssetFieldType.Vector3:
                    return "Vector3";
                case BlueprintUserStructAssetFieldType.Vector4:
                    return "Vector4";
                case BlueprintUserStructAssetFieldType.Color:
                    return "Color";
                case BlueprintUserStructAssetFieldType.Rect:
                    return "Rect";
                case BlueprintUserStructAssetFieldType.ForceMode:
                    return "ForceMode";
                case BlueprintUserStructAssetFieldType.ForceMode2D:
                    return "ForceMode2D";
                case BlueprintUserStructAssetFieldType.LoadSceneMode:
                    return "LoadSceneMode";
                case BlueprintUserStructAssetFieldType.Key:
                    return "Key";
                case BlueprintUserStructAssetFieldType.ComparisonMode:
                    return "ComparisonMode";
                case BlueprintUserStructAssetFieldType.TickPhase:
                    return "TickPhase";
                case BlueprintUserStructAssetFieldType.Blueprint:
                    return BlueprintVariableTypeRegistry.BlueprintAssetTypeId;
                default:
                    return "string";
            }
        }

        public static bool TryFromTypeId(string typeId, out BlueprintUserStructAssetFieldType type)
        {
            switch (typeId)
            {
                case "string":
                    type = BlueprintUserStructAssetFieldType.String;
                    return true;
                case "bool":
                    type = BlueprintUserStructAssetFieldType.Bool;
                    return true;
                case "int":
                    type = BlueprintUserStructAssetFieldType.Int;
                    return true;
                case "float":
                    type = BlueprintUserStructAssetFieldType.Float;
                    return true;
                case "Vector2":
                    type = BlueprintUserStructAssetFieldType.Vector2;
                    return true;
                case "Vector3":
                    type = BlueprintUserStructAssetFieldType.Vector3;
                    return true;
                case "Vector4":
                    type = BlueprintUserStructAssetFieldType.Vector4;
                    return true;
                case "Color":
                    type = BlueprintUserStructAssetFieldType.Color;
                    return true;
                case "Rect":
                    type = BlueprintUserStructAssetFieldType.Rect;
                    return true;
                case "ForceMode":
                    type = BlueprintUserStructAssetFieldType.ForceMode;
                    return true;
                case "ForceMode2D":
                    type = BlueprintUserStructAssetFieldType.ForceMode2D;
                    return true;
                case "LoadSceneMode":
                    type = BlueprintUserStructAssetFieldType.LoadSceneMode;
                    return true;
                case "Key":
                    type = BlueprintUserStructAssetFieldType.Key;
                    return true;
                case "ComparisonMode":
                    type = BlueprintUserStructAssetFieldType.ComparisonMode;
                    return true;
                case "TickPhase":
                    type = BlueprintUserStructAssetFieldType.TickPhase;
                    return true;
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    type = BlueprintUserStructAssetFieldType.Blueprint;
                    return true;
                default:
                    type = BlueprintUserStructAssetFieldType.String;
                    return false;
            }
        }
    }

    [Serializable]
    public sealed class BlueprintUserStructAssetField : ISerializationCallbackReceiver
    {
        [HideInInspector] public string id;
        [Tooltip("Field path used by Variable.GetField and Variable.SetField.")]
        public string name;
        [InspectorName("Type"), Tooltip("Blueprint value type stored by this field.")]
        public BlueprintUserStructAssetFieldType fieldType = BlueprintUserStructAssetFieldType.String;
        [Tooltip("Optional default value encoded as JSON, such as 1, \"Sword\", or [0, 0].")]
        public string defaultValueJson;
        [SerializeField, HideInInspector, FormerlySerializedAs("type")] private string legacyTypeId;

        public string TypeId
        {
            get { return BlueprintUserStructAssetFieldTypes.ToTypeId(fieldType); }
        }

        public object ReadDefaultValue()
        {
            if (string.IsNullOrEmpty(defaultValueJson))
            {
                return null;
            }

            try
            {
                return BlueprintJson.Deserialize(defaultValueJson);
            }
            catch (BlueprintJsonException)
            {
                return TypeId == "string" ? defaultValueJson : null;
            }
        }

        public void OnBeforeSerialize()
        {
            legacyTypeId = null;
        }

        public void OnAfterDeserialize()
        {
            BlueprintUserStructAssetFieldType migratedType;
            if (BlueprintUserStructAssetFieldTypes.TryFromTypeId(legacyTypeId, out migratedType))
            {
                fieldType = migratedType;
                legacyTypeId = null;
            }
        }
    }

    [CreateAssetMenu(menuName = "Blueprint System/User Struct Definition", fileName = "NewUserStruct")]
    public sealed class BlueprintUserStructAsset : ScriptableObject
    {
        [SerializeField, HideInInspector] private string schemaVersion = "0.1";
        [SerializeField, HideInInspector]
        private string typeId = "Struct.NewStruct";
        [SerializeField, Tooltip("Fields stored by this user struct.")]
        private List<BlueprintUserStructAssetField> fields = new List<BlueprintUserStructAssetField>();

        public string SchemaVersion
        {
            get { return schemaVersion; }
            set { schemaVersion = value; }
        }

        public string TypeId
        {
            get { return GetDerivedTypeId(); }
        }

        public List<BlueprintUserStructAssetField> Fields
        {
            get { return fields; }
        }

        public BlueprintUserStructDefinition ToDefinition()
        {
            BlueprintUserStructDefinition definition = new BlueprintUserStructDefinition();
            definition.SchemaVersion = string.IsNullOrEmpty(schemaVersion) ? "0.1" : schemaVersion;
            definition.TypeId = TypeId;
            definition.DisplayName = definition.TypeId;

            for (int i = 0; i < fields.Count; i++)
            {
                BlueprintUserStructAssetField source = fields[i];
                if (source == null)
                {
                    continue;
                }

                BlueprintUserStructField field = new BlueprintUserStructField();
                field.Id = source.id;
                field.Name = source.name;
                field.Type = source.TypeId;
                field.DefaultValue = source.ReadDefaultValue();
                definition.Fields.Add(field);
            }

            return definition;
        }

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["schemaVersion"] = string.IsNullOrEmpty(schemaVersion) ? "0.1" : schemaVersion;
            data["typeId"] = TypeId;

            List<object> fieldItems = new List<object>();
            for (int i = 0; i < fields.Count; i++)
            {
                BlueprintUserStructAssetField source = fields[i];
                if (source == null)
                {
                    continue;
                }

                Dictionary<string, object> item = new Dictionary<string, object>();
                item["id"] = source.id;
                item["name"] = source.name;
                item["type"] = source.TypeId;
                object defaultValue = source.ReadDefaultValue();
                if (defaultValue != null)
                {
                    item["defaultValue"] = defaultValue;
                }

                fieldItems.Add(item);
            }

            data["fields"] = fieldItems;
            return data;
        }

        public string ToJson()
        {
            return BlueprintJson.Serialize(ToDictionary(), true);
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(schemaVersion))
            {
                schemaVersion = "0.1";
            }

            bool typeIdChanged = RefreshDerivedTypeId();

            EnsureUniqueFieldIds();

#if UNITY_EDITOR
            if (typeIdChanged)
            {
                BlueprintUserStructRegistry.Refresh();
            }
#endif
        }

        private bool RefreshDerivedTypeId()
        {
            string derivedTypeId = GetDerivedTypeId();
            if (typeId != derivedTypeId)
            {
                typeId = derivedTypeId;
                return true;
            }

            return false;
        }

        private void EnsureUniqueFieldIds()
        {
            HashSet<string> fieldIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < fields.Count; i++)
            {
                BlueprintUserStructAssetField field = fields[i];
                if (field == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(field.id) && fieldIds.Add(field.id))
                {
                    continue;
                }

                field.id = GenerateFieldId(fieldIds);
            }
        }

        private static string GenerateFieldId(HashSet<string> existingIds)
        {
            string fieldId;
            do
            {
                fieldId = "fld_" + Guid.NewGuid().ToString("N");
            }
            while (existingIds != null && !existingIds.Add(fieldId));

            return fieldId;
        }

        private string GetDerivedTypeId()
        {
            string assetName = name;
            if (string.IsNullOrEmpty(assetName))
            {
                assetName = "NewUserStruct";
            }

            return "Struct." + assetName;
        }
    }
}
