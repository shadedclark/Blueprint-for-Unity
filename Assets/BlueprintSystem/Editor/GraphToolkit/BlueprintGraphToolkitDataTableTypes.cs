using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [Serializable]
    public struct DataTable
    {
        public string RowStructType;
        public string Path;
        public string AssetGuid;

        public DataTable(string rowStructType, string path, string assetGuid = null)
        {
            RowStructType = rowStructType;
            Path = BlueprintAssetDiscovery.NormalizeAssetPath(path);
            AssetGuid = assetGuid;
        }

        public override string ToString()
        {
            return BlueprintDataTableVariableTypeUtility.MakeType(RowStructType) + " " + (Path ?? string.Empty);
        }
    }

    [CustomPropertyDrawer(typeof(DataTable), true)]
    internal sealed class BlueprintGraphToolkitDataTableDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty rowStructTypeProperty = property.FindPropertyRelative("RowStructType");
            SerializedProperty pathProperty = property.FindPropertyRelative("Path");
            SerializedProperty assetGuidProperty = property.FindPropertyRelative("AssetGuid");
            if (rowStructTypeProperty == null || pathProperty == null || assetGuidProperty == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect typeRect = new Rect(position.x, position.y, position.width, lineHeight);
            Rect assetRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);
            Rect pathRect = new Rect(position.x, position.y + (lineHeight + spacing) * 2f, position.width, lineHeight);

            string[] rowStructTypes = BlueprintGraphToolkitDataTableTypes.SupportedRowStructTypes;
            if (rowStructTypes.Length > 0)
            {
                GUIContent[] rowStructLabels = new GUIContent[rowStructTypes.Length];
                for (int i = 0; i < rowStructTypes.Length; i++)
                {
                    rowStructLabels[i] = new GUIContent(rowStructTypes[i]);
                }

                int selected = FindIndex(rowStructTypes, rowStructTypeProperty.stringValue);
                EditorGUI.BeginChangeCheck();
                int nextSelected = EditorGUI.Popup(
                    typeRect,
                    new GUIContent(label.text + " Row Type", label.tooltip),
                    Mathf.Max(0, selected),
                    rowStructLabels);
                if (EditorGUI.EndChangeCheck())
                {
                    rowStructTypeProperty.stringValue = rowStructTypes[nextSelected];
                    BlueprintDataTableDefinition currentDefinition;
                    if (!string.IsNullOrEmpty(pathProperty.stringValue) &&
                        (!BlueprintDataTableRegistry.TryGetByPath(pathProperty.stringValue, out currentDefinition) ||
                         currentDefinition.RowStructTypeId != rowStructTypeProperty.stringValue))
                    {
                        pathProperty.stringValue = string.Empty;
                    }
                }
            }
            else
            {
                EditorGUI.LabelField(typeRect, label.text + " Row Type", "No Blueprint user structs found");
            }

            BlueprintDataTableAsset currentAsset = BlueprintGraphToolkitDataTableTypes.LoadAsset(
                pathProperty.stringValue,
                assetGuidProperty.stringValue);
            EditorGUI.BeginChangeCheck();
            BlueprintDataTableAsset selectedAsset = EditorGUI.ObjectField(
                assetRect,
                label,
                currentAsset,
                typeof(BlueprintDataTableAsset),
                false) as BlueprintDataTableAsset;
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedAsset == null)
                {
                    pathProperty.stringValue = string.Empty;
                    assetGuidProperty.stringValue = string.Empty;
                }
                else
                {
                    string assetPath = AssetDatabase.GetAssetPath(selectedAsset);
                    pathProperty.stringValue = BlueprintDataTableRegistry.GetJsonPathForAssetPath(assetPath);
                    rowStructTypeProperty.stringValue = selectedAsset.RowStructTypeId;
                    assetGuidProperty.stringValue = AssetDatabase.AssetPathToGUID(assetPath);
                }
            }

            EditorGUI.BeginChangeCheck();
            string editedPath = EditorGUI.TextField(pathRect, label.text + " Path", pathProperty.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                pathProperty.stringValue = BlueprintAssetDiscovery.NormalizeAssetPath(editedPath);
                BlueprintDataTableDefinition definition;
                if (BlueprintDataTableRegistry.TryGetByPath(pathProperty.stringValue, out definition) && definition != null)
                {
                    rowStructTypeProperty.stringValue = definition.RowStructTypeId;
                    BlueprintDataTableAsset resolvedAsset = BlueprintGraphToolkitDataTableTypes.LoadAsset(
                        pathProperty.stringValue,
                        null);
                    assetGuidProperty.stringValue = resolvedAsset == null
                        ? string.Empty
                        : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(resolvedAsset));
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 3f + EditorGUIUtility.standardVerticalSpacing * 2f;
        }

        private static int FindIndex(string[] values, string value)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                {
                    return i;
                }
            }

            return 0;
        }
    }

    internal static class BlueprintGraphToolkitDataTableTypes
    {
        public const string TypeId = "DataTable";

        public static string[] SupportedRowStructTypes
        {
            get
            {
                string[] typeIds = BlueprintUserStructRegistry.GetTypeIds();
                List<string> result = new List<string>(typeIds);
                result.Sort(StringComparer.Ordinal);
                return result.ToArray();
            }
        }

        public static bool IsGraphDataTableType(Type graphType)
        {
            return graphType == typeof(DataTable);
        }

        public static object CreateGraphValue(object value, string blueprintType)
        {
            string rowStructTypeId;
            BlueprintDataTableVariableTypeUtility.TryGetRowStructType(blueprintType, out rowStructTypeId);
            string path = value == null ? string.Empty : Convert.ToString(value);
            return new DataTable(rowStructTypeId, path);
        }

        public static bool TryGetBlueprintType(object graphValue, out string blueprintType)
        {
            blueprintType = null;
            if (!(graphValue is DataTable))
            {
                return false;
            }

            DataTable value = (DataTable)graphValue;
            blueprintType = BlueprintDataTableVariableTypeUtility.MakeType(value.RowStructType);
            return BlueprintDataTableVariableTypeUtility.IsSupportedType(blueprintType);
        }

        public static bool TryGetPath(object graphValue, out string path)
        {
            path = null;
            if (graphValue is DataTable)
            {
                DataTable value = (DataTable)graphValue;
                if (!string.IsNullOrEmpty(value.AssetGuid))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(value.AssetGuid);
                    BlueprintDataTableAsset asset = string.IsNullOrEmpty(assetPath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(assetPath);
                    if (asset != null)
                    {
                        path = BlueprintDataTableRegistry.GetJsonPathForAssetPath(assetPath);
                        return true;
                    }
                }

                path = BlueprintAssetDiscovery.NormalizeAssetPath(value.Path);
                return true;
            }

            string stringValue = graphValue as string;
            if (stringValue != null)
            {
                path = BlueprintAssetDiscovery.NormalizeAssetPath(stringValue);
                return true;
            }

            return false;
        }

        public static BlueprintDataTableAsset LoadAsset(string tablePath, string assetGuid = null)
        {
            if (!string.IsNullOrEmpty(assetGuid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                BlueprintDataTableAsset guidAsset = string.IsNullOrEmpty(guidPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(guidPath);
                if (guidAsset != null)
                {
                    return guidAsset;
                }
            }

            string normalizedPath = BlueprintAssetDiscovery.NormalizeAssetPath(tablePath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return null;
            }

            List<string> assetPaths = BlueprintEditorAssetDiscovery.FindAssetPaths("t:BlueprintDataTableAsset");
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string assetPath = assetPaths[i];
                if (BlueprintDataTableRegistry.GetJsonPathForAssetPath(assetPath) == normalizedPath ||
                    BlueprintAssetDiscovery.NormalizeAssetPath(assetPath) == normalizedPath)
                {
                    return AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(assetPath);
                }
            }

            return null;
        }
    }
}
