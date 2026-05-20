using System;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [Serializable]
    public struct Blueprint
    {
        public string Path;

        public Blueprint(string path)
        {
            Path = BlueprintGraphToolkitBlueprintTypes.NormalizePath(path);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Path) ? string.Empty : Path;
        }
    }

    [CustomPropertyDrawer(typeof(Blueprint), true)]
    internal sealed class BlueprintGraphToolkitBlueprintDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty pathProperty = property.FindPropertyRelative("Path");
            if (pathProperty == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect assetRect = new Rect(position.x, position.y, position.width, lineHeight);
            Rect pathRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

            TextAsset currentAsset = BlueprintGraphToolkitBlueprintTypes.LoadAsset(pathProperty.stringValue);
            EditorGUI.BeginChangeCheck();
            TextAsset selectedAsset = EditorGUI.ObjectField(assetRect, label, currentAsset, typeof(TextAsset), false) as TextAsset;
            if (EditorGUI.EndChangeCheck())
            {
                string selectedPath = BlueprintGraphToolkitBlueprintTypes.GetBlueprintAssetPath(selectedAsset);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    pathProperty.stringValue = selectedPath;
                }
                else if (selectedAsset == null)
                {
                    pathProperty.stringValue = string.Empty;
                }
            }

            pathProperty.stringValue = BlueprintGraphToolkitBlueprintTypes.NormalizePath(
                EditorGUI.TextField(pathRect, label.text + " Path", pathProperty.stringValue));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    internal static class BlueprintGraphToolkitBlueprintTypes
    {
        public const string TypeId = BlueprintVariableTypeRegistry.BlueprintAssetTypeId;

        public static object CreateGraphValue(object value)
        {
            if (value is Blueprint)
            {
                return value;
            }

            return new Blueprint(value == null ? string.Empty : Convert.ToString(value));
        }

        public static bool TryGetPath(object graphValue, out string path)
        {
            path = null;
            if (graphValue == null)
            {
                return false;
            }

            if (graphValue is Blueprint)
            {
                Blueprint value = (Blueprint)graphValue;
                path = NormalizePath(value.Path);
                return true;
            }

            string text = graphValue as string;
            if (text != null)
            {
                path = NormalizePath(text);
                return true;
            }

            return false;
        }

        public static TextAsset LoadAsset(string path)
        {
            string normalizedPath = NormalizePath(path);
            return string.IsNullOrEmpty(normalizedPath) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(normalizedPath);
        }

        public static string GetBlueprintAssetPath(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return IsBlueprintJsonPath(path) ? NormalizePath(path) : null;
        }

        public static bool IsBlueprintJsonPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   NormalizePath(path).EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
