using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [CustomEditor(typeof(BlueprintUserStructAsset))]
    public sealed class BlueprintUserStructAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            BlueprintUserStructAsset asset = (BlueprintUserStructAsset)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(new GUIContent("Type Id", "Read-only. Derived from the asset file name as Struct.{FileName}."), asset.TypeId);
            }

            DrawDefaultInspector();
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
                    BlueprintUserStructRegistry.Refresh();
                }
            }

            if (GUILayout.Button("Log JSON Preview"))
            {
                Debug.Log(asset.ToJson(), asset);
            }
        }

        [MenuItem("Assets/Blueprint System/Sync User Struct JSON", true)]
        private static bool CanSyncSelected()
        {
            return Selection.activeObject is BlueprintUserStructAsset;
        }

        [MenuItem("Assets/Blueprint System/Sync User Struct JSON")]
        private static void SyncSelected()
        {
            BlueprintUserStructAsset asset = Selection.activeObject as BlueprintUserStructAsset;
            if (asset != null)
            {
                ExportJson(asset);
            }
        }

        private static void DrawValidation(BlueprintUserStructAsset asset)
        {
            List<string> errors = Validate(asset);
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Struct definition is valid. Editor registry can use this asset directly; Sync JSON writes the generated schema next to the asset.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
        }

        private static List<string> Validate(BlueprintUserStructAsset asset)
        {
            List<string> errors = new List<string>();
            if (asset == null)
            {
                errors.Add("Asset is missing.");
                return errors;
            }

            if (string.IsNullOrEmpty(asset.TypeId))
            {
                errors.Add("Type Id is required.");
            }

            HashSet<string> ids = new HashSet<string>();
            HashSet<string> names = new HashSet<string>();
            for (int i = 0; i < asset.Fields.Count; i++)
            {
                BlueprintUserStructAssetField field = asset.Fields[i];
                string label = string.IsNullOrEmpty(field == null ? null : field.name) ? "Field " + i : field.name;
                if (field == null)
                {
                    errors.Add(label + " is null.");
                    continue;
                }

                if (string.IsNullOrEmpty(field.id))
                {
                    errors.Add(label + " is missing id.");
                }
                else if (!ids.Add(field.id))
                {
                    errors.Add(label + " duplicates field id '" + field.id + "'.");
                }

                if (string.IsNullOrEmpty(field.name))
                {
                    errors.Add(label + " is missing name.");
                }
                else if (!names.Add(field.name))
                {
                    errors.Add(label + " duplicates field name '" + field.name + "'.");
                }

                string fieldType = field.TypeId;
                if (!BlueprintUserStructUtility.IsSupportedFieldType(fieldType))
                {
                    errors.Add(label + " has unsupported type '" + fieldType + "'.");
                }
                else if (!ValidateDefaultValue(field, label, errors))
                {
                    continue;
                }
            }

            return errors;
        }

        private static bool ValidateDefaultValue(BlueprintUserStructAssetField field, string label, List<string> errors)
        {
            if (field == null || string.IsNullOrEmpty(field.defaultValueJson))
            {
                return true;
            }

            string fieldType = field.TypeId;
            object defaultValue = null;
            try
            {
                defaultValue = BlueprintJson.Deserialize(field.defaultValueJson);
            }
            catch (BlueprintJsonException)
            {
                if (fieldType == "string")
                {
                    defaultValue = field.defaultValueJson;
                }
                else
                {
                    errors.Add(label + " defaultValueJson must be valid JSON for type '" + fieldType + "'.");
                    return false;
                }
            }

            if (!BlueprintTypeUtility.IsValueAssignableToType(defaultValue, fieldType))
            {
                errors.Add(label + " defaultValueJson does not match type '" + fieldType + "'.");
                return false;
            }

            return true;
        }

        private static void ExportJson(BlueprintUserStructAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            List<string> errors = Validate(asset);
            if (errors.Count > 0)
            {
                Debug.LogError("[Blueprint] Cannot sync user struct JSON:\n" + string.Join("\n", errors), asset);
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
            Debug.Log("[Blueprint] Synced user struct JSON: " + jsonPath, asset);
        }

        private static string GetJsonPath(string assetPath, BlueprintUserStructAsset asset)
        {
            if (BlueprintAssetDiscovery.IsAssetDatabasePath(assetPath))
            {
                return BlueprintAssetDiscovery.ChangeAssetPathExtension(assetPath, BlueprintUserStructRegistry.StructAssetExtension);
            }

            string fileName = string.IsNullOrEmpty(asset.TypeId)
                ? asset.name
                : asset.TypeId.Replace("Struct.", string.Empty);
            return Path.Combine(BlueprintUserStructRegistry.DefaultAssetRoot, fileName + BlueprintUserStructRegistry.StructAssetExtension);
        }
    }
}
