using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [Serializable]
    public struct Array
    {
        public string ElementType;
        public string Json;

        public Array(string elementType, string json)
        {
            ElementType = BlueprintGraphToolkitArrayTypes.NormalizeElementType(elementType);
            Json = BlueprintGraphToolkitArrayTypes.NormalizeJson(json);
        }

        public override string ToString()
        {
            return BlueprintGraphToolkitArrayTypes.MakeBlueprintType(ElementType) + " " + BlueprintGraphToolkitArrayTypes.NormalizeJson(Json);
        }
    }

    [Serializable]
    public struct Array<T>
    {
        public string Json;

        public Array(string json)
        {
            Json = string.IsNullOrEmpty(json) ? "[]" : json;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Json) ? "[]" : Json;
        }
    }

    [CustomPropertyDrawer(typeof(Array), true)]
    internal sealed class BlueprintGraphToolkitArrayDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty elementType = property.FindPropertyRelative("ElementType");
            SerializedProperty json = property.FindPropertyRelative("Json");
            if (elementType == null || json == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            string[] elementTypes = BlueprintGraphToolkitArrayTypes.SupportedElementTypes;
            GUIContent[] elementLabels = new GUIContent[elementTypes.Length];
            for (int i = 0; i < elementTypes.Length; i++)
            {
                elementLabels[i] = new GUIContent(elementTypes[i]);
            }

            string currentElementType = BlueprintGraphToolkitArrayTypes.NormalizeElementType(elementType.stringValue);
            int selected = 0;
            for (int i = 0; i < elementTypes.Length; i++)
            {
                if (elementTypes[i] == currentElementType)
                {
                    selected = i;
                    break;
                }
            }

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect typeRect = new Rect(position.x, position.y, position.width, lineHeight);
            Rect jsonRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

            int newSelected = EditorGUI.Popup(typeRect, new GUIContent(label.text + " Element Type", label.tooltip), selected, elementLabels);
            elementType.stringValue = elementTypes[Mathf.Clamp(newSelected, 0, elementTypes.Length - 1)];
            if (string.IsNullOrEmpty(json.stringValue))
            {
                json.stringValue = BlueprintGraphToolkitArrayTypes.DefaultJson;
            }

            EditorGUI.PropertyField(jsonRect, json, new GUIContent(label.text + " JSON", label.tooltip));
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    [CustomPropertyDrawer(typeof(Array<>), true)]
    internal sealed class BlueprintGraphToolkitLegacyArrayDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty json = property.FindPropertyRelative("Json");
            if (json == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.PropertyField(position, json, new GUIContent(label.text + " JSON", label.tooltip));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty json = property.FindPropertyRelative("Json");
            return json == null
                ? EditorGUI.GetPropertyHeight(property, label, true)
                : EditorGUI.GetPropertyHeight(json, label, true);
        }
    }

    internal static class BlueprintGraphToolkitArrayTypes
    {
        public const string DefaultElementType = "string";
        public const string DefaultJson = "[]";

        public static string[] SupportedElementTypes
        {
            get
            {
                string[] blueprintTypes = BlueprintVariableTypeRegistry.GetSupportedBlueprintTypes();
                List<string> elementTypes = new List<string>();
                for (int i = 0; i < blueprintTypes.Length; i++)
                {
                    if (BlueprintArrayUtility.IsSupportedElementType(blueprintTypes[i]))
                    {
                        elementTypes.Add(blueprintTypes[i]);
                    }
                }

                elementTypes.Sort(StringComparer.Ordinal);
                if (!elementTypes.Contains(DefaultElementType))
                {
                    elementTypes.Insert(0, DefaultElementType);
                }

                return elementTypes.ToArray();
            }
        }

        public static bool IsGraphArrayType(Type graphType)
        {
            return graphType == typeof(Array) ||
                   graphType != null &&
                   graphType.IsGenericType &&
                   graphType.GetGenericTypeDefinition() == typeof(Array<>);
        }

        public static Type MakeGraphArrayType(Type elementType)
        {
            return typeof(Array);
        }

        public static bool TryGetElementType(Type graphType, out Type elementType)
        {
            elementType = null;
            if (graphType == typeof(Array))
            {
                return false;
            }

            if (!IsGraphArrayType(graphType))
            {
                return false;
            }

            elementType = graphType.GetGenericArguments()[0];
            return elementType != null;
        }

        public static bool TryGetElementType(object graphValue, out string elementType)
        {
            elementType = null;
            if (graphValue == null)
            {
                return false;
            }

            Type valueType = graphValue.GetType();
            if (valueType == typeof(Array))
            {
                Array value = (Array)graphValue;
                elementType = NormalizeElementType(value.ElementType);
                return true;
            }

            Type legacyElementType;
            if (TryGetElementType(valueType, out legacyElementType))
            {
                return BlueprintVariableTypeRegistry.TryGetBlueprintType(legacyElementType, out elementType);
            }

            return false;
        }

        public static bool TryGetBlueprintType(object graphValue, out string blueprintType)
        {
            blueprintType = null;

            string elementType;
            if (!TryGetElementType(graphValue, out elementType))
            {
                return false;
            }

            blueprintType = MakeBlueprintType(elementType);
            return true;
        }

        public static object CreateGraphValue(string json, string blueprintType)
        {
            Type graphType;
            string elementType;
            if (!BlueprintArrayUtility.TryGetElementType(blueprintType, out elementType) ||
                !BlueprintGraphToolkitTypeRegistry.TryGetGraphType(blueprintType, out graphType) ||
                graphType != typeof(Array))
            {
                return NormalizeJson(json);
            }

            return new Array(elementType, json);
        }

        public static bool TryGetJson(object graphValue, out string json)
        {
            json = null;
            if (graphValue == null)
            {
                return false;
            }

            Type valueType = graphValue.GetType();
            if (valueType == typeof(Array))
            {
                Array value = (Array)graphValue;
                json = NormalizeJson(value.Json);
                return true;
            }

            if (!IsGraphArrayType(valueType))
            {
                return false;
            }

            System.Reflection.FieldInfo field = valueType.GetField("Json");
            object fieldValue = field == null ? null : field.GetValue(graphValue);
            json = NormalizeJson(fieldValue as string);
            return true;
        }

        public static string NormalizeElementType(string elementType)
        {
            if (BlueprintArrayUtility.IsSupportedElementType(elementType))
            {
                return elementType;
            }

            return DefaultElementType;
        }

        public static string NormalizeJson(string json)
        {
            return string.IsNullOrEmpty(json) ? DefaultJson : json;
        }

        public static string MakeBlueprintType(string elementType)
        {
            return "Array<" + NormalizeElementType(elementType) + ">";
        }
    }
}
