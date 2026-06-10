using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [CustomEditor(typeof(BlueprintDataTableAsset))]
    public sealed class BlueprintDataTableAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            BlueprintDataTableAsset asset = (BlueprintDataTableAsset)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(new GUIContent("Table Id", "Read-only. Derived from the asset file name as Table.{FileName}."), asset.TableId);
            }

            SerializedProperty rowStructTypeProperty = serializedObject.FindProperty("rowStructTypeId");
            DrawRowStructType(rowStructTypeProperty);
            DrawRows(serializedObject.FindProperty("rows"), rowStructTypeProperty == null ? null : rowStructTypeProperty.stringValue);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawValidation(asset);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync JSON"))
                {
                    ExportJson(asset);
                }

                if (GUILayout.Button("Refresh Registry"))
                {
                    BlueprintDataTableRegistry.Refresh();
                }
            }

            if (GUILayout.Button("Log JSON Preview"))
            {
                Debug.Log(asset.ToJson(), asset);
            }
        }

        [MenuItem("Assets/Blueprint System/Sync Data Table JSON", true)]
        private static bool CanSyncSelected()
        {
            return Selection.activeObject is BlueprintDataTableAsset;
        }

        [MenuItem("Assets/Blueprint System/Sync Data Table JSON")]
        private static void SyncSelected()
        {
            BlueprintDataTableAsset asset = Selection.activeObject as BlueprintDataTableAsset;
            if (asset != null)
            {
                ExportJson(asset);
            }
        }

        private static void DrawRowStructType(SerializedProperty property)
        {
            if (property == null)
            {
                return;
            }

            string[] typeIds = BlueprintUserStructRegistry.GetTypeIds();
            if (typeIds.Length == 0)
            {
                EditorGUILayout.PropertyField(property, new GUIContent("Row Struct Type Id"));
                return;
            }

            int selected = 0;
            for (int i = 0; i < typeIds.Length; i++)
            {
                if (typeIds[i] == property.stringValue)
                {
                    selected = i;
                    break;
                }
            }

            int newSelected = EditorGUILayout.Popup(new GUIContent("Row Struct Type Id"), selected, typeIds);
            property.stringValue = typeIds[Mathf.Clamp(newSelected, 0, typeIds.Length - 1)];
        }

        private static void DrawRows(SerializedProperty rowsProperty, string rowStructTypeId)
        {
            if (rowsProperty == null)
            {
                return;
            }

            BlueprintUserStructDefinition definition = null;
            bool hasRowStruct = !string.IsNullOrEmpty(rowStructTypeId) &&
                BlueprintUserStructRegistry.TryGet(rowStructTypeId, out definition);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rows", EditorStyles.boldLabel);

            int newSize = Mathf.Max(0, EditorGUILayout.IntField("Size", rowsProperty.arraySize));
            if (newSize != rowsProperty.arraySize)
            {
                int previousSize = rowsProperty.arraySize;
                rowsProperty.arraySize = newSize;
                for (int i = previousSize; i < newSize; i++)
                {
                    InitializeRow(rowsProperty.GetArrayElementAtIndex(i), rowStructTypeId);
                }
            }

            for (int i = 0; i < rowsProperty.arraySize; i++)
            {
                SerializedProperty rowProperty = rowsProperty.GetArrayElementAtIndex(i);
                if (rowProperty == null)
                {
                    continue;
                }

                SerializedProperty rowNameProperty = rowProperty.FindPropertyRelative("rowName");
                string rowName = rowNameProperty == null ? null : rowNameProperty.stringValue;
                string label = string.IsNullOrEmpty(rowName) ? "Element " + i : rowName;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                rowProperty.isExpanded = EditorGUILayout.Foldout(rowProperty.isExpanded, label, true);
                if (rowProperty.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    if (rowNameProperty != null)
                    {
                        EditorGUILayout.PropertyField(rowNameProperty, new GUIContent("Row Name"));
                    }

                    SerializedProperty valueJsonProperty = rowProperty.FindPropertyRelative("valueJson");
                    if (hasRowStruct)
                    {
                        DrawRowValue(valueJsonProperty, rowStructTypeId, definition);
                    }
                    else if (valueJsonProperty != null)
                    {
                        EditorGUILayout.PropertyField(valueJsonProperty, new GUIContent("Value JSON"));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (hasRowStruct && valueJsonProperty != null && GUILayout.Button("Reset Value", GUILayout.Width(96f)))
                        {
                            valueJsonProperty.stringValue = CreateDefaultRowJson(rowStructTypeId);
                        }

                        if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                        {
                            rowsProperty.DeleteArrayElementAtIndex(i);
                            EditorGUI.indentLevel--;
                            EditorGUILayout.EndVertical();
                            return;
                        }
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Row"))
            {
                int index = rowsProperty.arraySize;
                rowsProperty.arraySize++;
                InitializeRow(rowsProperty.GetArrayElementAtIndex(index), rowStructTypeId);
            }
        }

        private static void DrawRowValue(SerializedProperty valueJsonProperty, string rowStructTypeId, BlueprintUserStructDefinition definition)
        {
            if (valueJsonProperty == null || definition == null)
            {
                return;
            }

            string error;
            Dictionary<string, object> values = ReadEditableRowValue(valueJsonProperty.stringValue, rowStructTypeId, out error);
            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            }

            EditorGUILayout.LabelField("Value", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;

            bool changed = false;
            for (int i = 0; i < definition.Fields.Count; i++)
            {
                BlueprintUserStructField field = definition.Fields[i];
                if (field == null || field.Deprecated)
                {
                    continue;
                }

                object currentValue;
                if (!values.TryGetValue(field.Name, out currentValue))
                {
                    currentValue = field.DefaultValue;
                }

                object editedValue;
                if (DrawFieldValue(field, currentValue, out editedValue))
                {
                    values[field.Name] = editedValue;
                    changed = true;
                }
            }

            EditorGUI.indentLevel--;

            if (changed)
            {
                valueJsonProperty.stringValue = BlueprintJson.Serialize(values, false);
            }
        }

        private static bool DrawFieldValue(BlueprintUserStructField field, object currentValue, out object editedJsonValue)
        {
            editedJsonValue = currentValue;
            string type = field.Type;
            GUIContent label = new GUIContent(field.Name, CreateFieldTooltip(field));

            EditorGUI.BeginChangeCheck();
            switch (type)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    editedJsonValue = EditorGUILayout.TextField(label, currentValue == null ? string.Empty : Convert.ToString(currentValue, CultureInfo.InvariantCulture));
                    return EditorGUI.EndChangeCheck();
                case "bool":
                    editedJsonValue = EditorGUILayout.Toggle(label, currentValue is bool && (bool)currentValue);
                    return EditorGUI.EndChangeCheck();
                case "int":
                    editedJsonValue = EditorGUILayout.IntField(label, ToInt(currentValue));
                    return EditorGUI.EndChangeCheck();
                case "float":
                    editedJsonValue = EditorGUILayout.FloatField(label, ToFloat(currentValue));
                    return EditorGUI.EndChangeCheck();
                case "Vector2":
                    Vector2 vector2 = EditorGUILayout.Vector2Field(label, currentValue is Vector2 ? (Vector2)currentValue : BlueprintTypeUtility.ToVector2(currentValue, Vector2.zero));
                    editedJsonValue = new List<object> { vector2.x, vector2.y };
                    return EditorGUI.EndChangeCheck();
                case "Vector3":
                    Vector3 vector3 = EditorGUILayout.Vector3Field(label, currentValue is Vector3 ? (Vector3)currentValue : BlueprintTypeUtility.ToVector3(currentValue, Vector3.zero));
                    editedJsonValue = new List<object> { vector3.x, vector3.y, vector3.z };
                    return EditorGUI.EndChangeCheck();
                case "Vector4":
                    Vector4 vector4 = EditorGUILayout.Vector4Field(label, currentValue is Vector4 ? (Vector4)currentValue : BlueprintTypeUtility.ToVector4(currentValue, Vector4.zero));
                    editedJsonValue = new List<object> { vector4.x, vector4.y, vector4.z, vector4.w };
                    return EditorGUI.EndChangeCheck();
                case "Rect":
                    Rect rect = EditorGUILayout.RectField(label, currentValue is Rect ? (Rect)currentValue : BlueprintTypeUtility.ToRect(currentValue, Rect.zero));
                    editedJsonValue = new List<object> { rect.x, rect.y, rect.width, rect.height };
                    return EditorGUI.EndChangeCheck();
                case "Color":
                    Color color = EditorGUILayout.ColorField(label, currentValue is Color ? (Color)currentValue : ToColor(currentValue, Color.white));
                    editedJsonValue = new List<object> { color.r, color.g, color.b, color.a };
                    return EditorGUI.EndChangeCheck();
                default:
                    Type enumType;
                    if (BlueprintVariableTypeRegistry.TryGetClrType(type, out enumType) && enumType.IsEnum)
                    {
                        string[] names = Enum.GetNames(enumType);
                        int selected = FindEnumIndex(names, currentValue);
                        int newSelected = EditorGUILayout.Popup(label, selected, names);
                        editedJsonValue = names[Mathf.Clamp(newSelected, 0, names.Length - 1)];
                        return EditorGUI.EndChangeCheck();
                    }

                    EditorGUI.EndChangeCheck();
                    return DrawJsonBackedField(label, type, currentValue, out editedJsonValue);
            }
        }

        private static bool DrawJsonBackedField(GUIContent label, string type, object currentValue, out object editedJsonValue)
        {
            editedJsonValue = currentValue;
            string json = currentValue == null ? string.Empty : BlueprintJson.Serialize(currentValue, false);
            EditorGUI.BeginChangeCheck();
            string editedJson = EditorGUILayout.TextField(label, json);
            bool changed = EditorGUI.EndChangeCheck();
            if (!changed)
            {
                return false;
            }

            object rawValue = null;
            if (!string.IsNullOrEmpty(editedJson))
            {
                try
                {
                    rawValue = BlueprintJson.Deserialize(editedJson);
                }
                catch (BlueprintJsonException exception)
                {
                    EditorGUILayout.HelpBox("Invalid JSON for " + label.text + ": " + exception.Message, MessageType.Warning);
                    return false;
                }
            }

            object normalizedValue;
            if (!TryNormalizeJsonValue(rawValue, type, out normalizedValue))
            {
                EditorGUILayout.HelpBox(label.text + " value is not assignable to " + type + ".", MessageType.Warning);
                return false;
            }

            editedJsonValue = normalizedValue;
            return true;
        }

        private static bool TryNormalizeJsonValue(object rawValue, string type, out object normalizedValue)
        {
            normalizedValue = rawValue;
            object arrayValue;
            if (BlueprintArrayUtility.TryConvertToJsonArray(rawValue, type, out arrayValue))
            {
                normalizedValue = arrayValue;
                return true;
            }

            object structuredValue;
            if (BlueprintStructuredValueUtility.TryConvertToJsonValue(rawValue, type, out structuredValue))
            {
                normalizedValue = structuredValue;
                return true;
            }

            if (BlueprintTypeUtility.IsValueAssignableToType(rawValue, type))
            {
                normalizedValue = rawValue;
                return true;
            }

            return false;
        }

        private static Dictionary<string, object> ReadEditableRowValue(string valueJson, string rowStructTypeId, out string error)
        {
            error = null;
            Dictionary<string, object> fallback = CreateDefaultRowDictionary(rowStructTypeId);
            if (string.IsNullOrEmpty(valueJson))
            {
                return fallback;
            }

            object rawValue;
            try
            {
                rawValue = BlueprintJson.Deserialize(valueJson);
            }
            catch (BlueprintJsonException exception)
            {
                error = "valueJson is invalid. Editing any field will replace it with a valid row value. " + exception.Message;
                return fallback;
            }

            object runtimeValue;
            if (!BlueprintUserStructUtility.TryConvertToRuntimeValue(rawValue, rowStructTypeId, out runtimeValue))
            {
                error = "valueJson does not match " + rowStructTypeId + ". Editing any field will replace it with defaults for this row type.";
                return fallback;
            }

            object jsonValue;
            if (!BlueprintUserStructUtility.TryConvertToJsonValue(runtimeValue, rowStructTypeId, out jsonValue))
            {
                error = "valueJson could not be normalized for " + rowStructTypeId + ".";
                return fallback;
            }

            Dictionary<string, object> values = NormalizeDictionary(jsonValue);
            return values ?? fallback;
        }

        private static Dictionary<string, object> CreateDefaultRowDictionary(string rowStructTypeId)
        {
            object defaultValue;
            if (BlueprintUserStructUtility.TryCreateDefaultJsonValue(rowStructTypeId, out defaultValue))
            {
                Dictionary<string, object> dictionary = NormalizeDictionary(defaultValue);
                if (dictionary != null)
                {
                    return dictionary;
                }
            }

            return new Dictionary<string, object>();
        }

        private static string CreateDefaultRowJson(string rowStructTypeId)
        {
            return BlueprintJson.Serialize(CreateDefaultRowDictionary(rowStructTypeId), false);
        }

        private static Dictionary<string, object> NormalizeDictionary(object value)
        {
            Dictionary<string, object> dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                return new Dictionary<string, object>(dictionary);
            }

            IDictionary genericDictionary = value as IDictionary;
            if (genericDictionary == null)
            {
                return null;
            }

            Dictionary<string, object> normalized = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in genericDictionary)
            {
                normalized[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value;
            }

            return normalized;
        }

        private static void InitializeRow(SerializedProperty rowProperty, string rowStructTypeId)
        {
            if (rowProperty == null)
            {
                return;
            }

            SerializedProperty rowNameProperty = rowProperty.FindPropertyRelative("rowName");
            if (rowNameProperty != null)
            {
                rowNameProperty.stringValue = string.Empty;
            }

            SerializedProperty valueJsonProperty = rowProperty.FindPropertyRelative("valueJson");
            if (valueJsonProperty != null)
            {
                valueJsonProperty.stringValue = CreateDefaultRowJson(rowStructTypeId);
            }
        }

        private static string CreateFieldTooltip(BlueprintUserStructField field)
        {
            if (field == null)
            {
                return string.Empty;
            }

            string tooltip = field.Id + " : " + field.Type;
            if (!string.IsNullOrEmpty(field.Description))
            {
                tooltip += "\n" + field.Description;
            }

            return tooltip;
        }

        private static int FindEnumIndex(string[] names, object value)
        {
            if (names == null || names.Length == 0)
            {
                return 0;
            }

            string current = value == null ? names[0] : Convert.ToString(value, CultureInfo.InvariantCulture);
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == current)
                {
                    return i;
                }
            }

            return 0;
        }

        private static int ToInt(object value)
        {
            try
            {
                return Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static float ToFloat(object value)
        {
            try
            {
                return Convert.ToSingle(value ?? 0f, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0f;
            }
        }

        private static Color ToColor(object value, Color defaultValue)
        {
            if (value is Color)
            {
                return (Color)value;
            }

            IList list = value as IList;
            if (list == null || (list.Count != 3 && list.Count != 4))
            {
                return defaultValue;
            }

            try
            {
                float r = Convert.ToSingle(list[0], CultureInfo.InvariantCulture);
                float g = Convert.ToSingle(list[1], CultureInfo.InvariantCulture);
                float b = Convert.ToSingle(list[2], CultureInfo.InvariantCulture);
                float a = list.Count == 4 ? Convert.ToSingle(list[3], CultureInfo.InvariantCulture) : defaultValue.a;
                return new Color(r, g, b, a);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static void DrawValidation(BlueprintDataTableAsset asset)
        {
            List<string> errors = Validate(asset);
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Data table is valid. Sync JSON writes the generated table next to the asset.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
        }

        internal static List<string> Validate(BlueprintDataTableAsset asset)
        {
            List<string> errors = new List<string>();
            if (asset == null)
            {
                errors.Add("Asset is missing.");
                return errors;
            }

            if (string.IsNullOrEmpty(asset.TableId))
            {
                errors.Add("Table Id is required.");
            }

            if (string.IsNullOrEmpty(asset.RowStructTypeId))
            {
                errors.Add("Row Struct Type Id is required.");
            }
            else if (!BlueprintUserStructRegistry.IsUserStructType(asset.RowStructTypeId))
            {
                errors.Add("Unknown row struct type '" + asset.RowStructTypeId + "'.");
            }

            HashSet<string> rowNames = new HashSet<string>();
            for (int i = 0; i < asset.Rows.Count; i++)
            {
                BlueprintDataTableAssetRow row = asset.Rows[i];
                string label = row == null || string.IsNullOrEmpty(row.rowName) ? "Row " + i : row.rowName;
                if (row == null)
                {
                    errors.Add(label + " is null.");
                    continue;
                }

                if (string.IsNullOrEmpty(row.rowName))
                {
                    errors.Add(label + " is missing rowName.");
                }
                else if (!rowNames.Add(row.rowName))
                {
                    errors.Add(label + " duplicates rowName '" + row.rowName + "'.");
                }

                ValidateRowValue(asset.RowStructTypeId, row, label, errors);
            }

            return errors;
        }

        private static void ValidateRowValue(string rowStructTypeId, BlueprintDataTableAssetRow row, string label, List<string> errors)
        {
            if (row == null || string.IsNullOrEmpty(rowStructTypeId) || !BlueprintUserStructRegistry.IsUserStructType(rowStructTypeId))
            {
                return;
            }

            object rawValue = null;
            if (!string.IsNullOrEmpty(row.valueJson))
            {
                try
                {
                    rawValue = BlueprintJson.Deserialize(row.valueJson);
                }
                catch (BlueprintJsonException)
                {
                    errors.Add(label + " valueJson must be valid JSON for type '" + rowStructTypeId + "'.");
                    return;
                }
            }

            object runtimeValue;
            if (!BlueprintUserStructUtility.TryConvertToRuntimeValue(rawValue, rowStructTypeId, out runtimeValue))
            {
                errors.Add(label + " valueJson does not match type '" + rowStructTypeId + "'.");
            }
        }

        private static void ExportJson(BlueprintDataTableAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            List<string> errors = Validate(asset);
            if (errors.Count > 0)
            {
                Debug.LogError("[Blueprint] Cannot sync data table JSON:\n" + string.Join("\n", errors), asset);
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(asset);
            string jsonPath = GetJsonPath(assetPath, asset);
            string directory = Path.GetDirectoryName(jsonPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(jsonPath, asset.ToJson());
            AssetDatabase.ImportAsset(jsonPath);
            AssetDatabase.SaveAssets();
            BlueprintDataTableRegistry.Refresh();
            Debug.Log("[Blueprint] Synced data table JSON: " + jsonPath, asset);
        }

        private static string GetJsonPath(string assetPath, BlueprintDataTableAsset asset)
        {
            if (BlueprintAssetDiscovery.IsAssetDatabasePath(assetPath))
            {
                return BlueprintDataTableRegistry.GetJsonPathForAssetPath(assetPath);
            }

            string fileName = string.IsNullOrEmpty(asset.TableId)
                ? asset.name
                : asset.TableId.Replace("Table.", string.Empty);
            return Path.Combine(BlueprintDataTableRegistry.DefaultAssetRoot, fileName + BlueprintDataTableRegistry.DataTableAssetExtension);
        }
    }
}
