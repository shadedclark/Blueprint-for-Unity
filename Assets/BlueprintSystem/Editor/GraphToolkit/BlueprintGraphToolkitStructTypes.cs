using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [Serializable]
    public struct Struct
    {
        public string TypeId;
        public string Json;

        public Struct(string typeId, string json)
        {
            TypeId = BlueprintGraphToolkitStructTypes.NormalizeTypeId(typeId);
            Json = BlueprintGraphToolkitStructTypes.NormalizeJson(json, TypeId);
        }

        public override string ToString()
        {
            return TypeId + " " + Json;
        }
    }

    [CustomPropertyDrawer(typeof(Struct), true)]
    internal sealed class BlueprintGraphToolkitStructDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty typeId = property.FindPropertyRelative("TypeId");
            SerializedProperty json = property.FindPropertyRelative("Json");
            if (typeId == null || json == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            string[] typeIds = BlueprintGraphToolkitStructTypes.SupportedStructTypes;
            GUIContent[] labels = new GUIContent[typeIds.Length];
            for (int i = 0; i < typeIds.Length; i++)
            {
                labels[i] = new GUIContent(typeIds[i]);
            }

            string currentTypeId = BlueprintGraphToolkitStructTypes.NormalizeTypeId(typeId.stringValue);
            int selected = 0;
            for (int i = 0; i < typeIds.Length; i++)
            {
                if (typeIds[i] == currentTypeId)
                {
                    selected = i;
                    break;
                }
            }

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect typeRect = new Rect(position.x, position.y, position.width, lineHeight);
            Rect jsonRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

            int newSelected = EditorGUI.Popup(typeRect, new GUIContent(label.text + " Type", label.tooltip), selected, labels);
            typeId.stringValue = typeIds[Mathf.Clamp(newSelected, 0, typeIds.Length - 1)];
            if (string.IsNullOrEmpty(json.stringValue) || typeId.stringValue != currentTypeId)
            {
                json.stringValue = BlueprintGraphToolkitStructTypes.NormalizeJson(json.stringValue, typeId.stringValue);
            }

            EditorGUI.PropertyField(jsonRect, json, new GUIContent(label.text + " JSON", label.tooltip));
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    internal static class BlueprintGraphToolkitStructTypes
    {
        public static string[] SupportedStructTypes
        {
            get
            {
                string[] typeIds = BlueprintUserStructRegistry.GetTypeIds();
                if (typeIds.Length > 0)
                {
                    return typeIds;
                }

                return new[] { string.Empty };
            }
        }

        public static string DefaultTypeId
        {
            get
            {
                string[] typeIds = BlueprintUserStructRegistry.GetTypeIds();
                return typeIds.Length == 0 ? string.Empty : typeIds[0];
            }
        }

        public static bool IsGraphStructType(Type graphType)
        {
            return graphType == typeof(Struct);
        }

        public static object CreateGraphValue(object value, string blueprintType)
        {
            string json = null;
            object jsonValue;
            if (BlueprintStructuredValueUtility.TryConvertToJsonValue(value, blueprintType, out jsonValue))
            {
                json = BlueprintVisualValueUtility.ToJson(jsonValue);
            }
            else if (value != null)
            {
                json = Convert.ToString(value);
            }

            return new Struct(blueprintType, NormalizeJson(json, blueprintType));
        }

        public static bool TryGetJson(object graphValue, out string typeId, out string json)
        {
            typeId = null;
            json = null;
            if (graphValue == null)
            {
                return false;
            }

            if (graphValue.GetType() == typeof(Struct))
            {
                Struct value = (Struct)graphValue;
                typeId = NormalizeTypeId(value.TypeId);
                json = NormalizeJson(value.Json, typeId);
                return true;
            }

            string text = graphValue as string;
            if (text != null)
            {
                typeId = DefaultTypeId;
                json = NormalizeJson(text, typeId);
                return true;
            }

            return false;
        }

        public static string NormalizeTypeId(string typeId)
        {
            if (BlueprintUserStructRegistry.IsUserStructType(typeId))
            {
                return typeId;
            }

            return DefaultTypeId;
        }

        public static string NormalizeJson(string json, string typeId)
        {
            if (!string.IsNullOrEmpty(json))
            {
                return json;
            }

            object defaultJson;
            if (!string.IsNullOrEmpty(typeId) && BlueprintStructuredValueUtility.TryCreateDefaultJsonValue(typeId, out defaultJson))
            {
                return BlueprintVisualValueUtility.ToJson(defaultJson);
            }

            return "{}";
        }
    }
}
