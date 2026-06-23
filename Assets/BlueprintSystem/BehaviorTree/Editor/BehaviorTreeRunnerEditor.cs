using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [CustomEditor(typeof(BehaviorTreeRunner), true)]
    [CanEditMultipleObjects]
    internal sealed class BehaviorTreeRunnerInspector : UnityEditor.Editor
    {
        private const float NameWidth = 150f;
        private const float TypeWidth = 95f;
        private const float ModeWidth = 80f;
        private const float ResetWidth = 52f;
        private double _nextRuntimeDebugRepaintTime;

        private void OnEnable()
        {
            EditorApplication.update += RepaintRuntimeDebug;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintRuntimeDebug;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "blackboardOverrides");
            serializedObject.ApplyModifiedProperties();

            if (!BlueprintModuleSettings.BehaviorTreeEnabled)
            {
                EditorGUILayout.HelpBox(
                    "The Behavior Tree module is disabled in Project Settings > Blueprint System > Modules. Existing BehaviorTreeRunner components stay serialized, but they will not start or tick until the module is enabled.",
                    MessageType.Warning);
                return;
            }

            SyncBlackboardOverridesForTargets();

            serializedObject.Update();
            EditorGUILayout.Space();
            SerializedProperty compiledProperty = serializedObject.FindProperty("compiledBehaviorTree");
            DrawBlackboardOverrides(compiledProperty == null ? null : compiledProperty.objectReferenceValue as BehaviorTreeCompiledAsset);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Debug", EditorStyles.boldLabel);
            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox("Select one BehaviorTreeRunner to inspect runtime debug state.", MessageType.Info);
            }
            else
            {
                BehaviorTreeRuntimeDebugEditorUtility.DrawRunnerDebugPanel(target as BehaviorTreeRunner);
            }
        }

        private void RepaintRuntimeDebug()
        {
            if (!Application.isPlaying || target == null || EditorApplication.timeSinceStartup < _nextRuntimeDebugRepaintTime)
            {
                return;
            }

            BehaviorTreeRunner runner = target as BehaviorTreeRunner;
            if (runner == null || !runner.IsRunning)
            {
                return;
            }

            _nextRuntimeDebugRepaintTime = EditorApplication.timeSinceStartup + 0.1d;
            Repaint();
        }

        private void SyncBlackboardOverridesForTargets()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                BehaviorTreeRunner runner = targets[i] as BehaviorTreeRunner;
                if (runner == null)
                {
                    continue;
                }

                SerializedObject runnerObject = new SerializedObject(runner);
                SerializedProperty overridesProperty = runnerObject.FindProperty("blackboardOverrides");
                if (overridesProperty == null || !overridesProperty.isArray)
                {
                    continue;
                }

                BehaviorTreeCompiledAsset compiledAsset = runner.CompiledBehaviorTree;
                BehaviorTreeRunnerBlackboardOverrideEditorUtility.SyncOverrideArray(
                    runnerObject,
                    overridesProperty,
                    compiledAsset == null ? null : compiledAsset.Blackboard);
            }
        }

        private void DrawBlackboardOverrides(BehaviorTreeCompiledAsset compiledAsset)
        {
            EditorGUILayout.LabelField("Blackboard Overrides", EditorStyles.boldLabel);

            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox("Select one BehaviorTreeRunner to edit blackboard overrides.", MessageType.Info);
                return;
            }

            if (compiledAsset == null)
            {
                EditorGUILayout.HelpBox("Assign a compiled behavior tree asset to edit blackboard overrides.", MessageType.Info);
                return;
            }

            SerializedProperty overridesProperty = serializedObject.FindProperty("blackboardOverrides");
            if (overridesProperty == null || !overridesProperty.isArray)
            {
                EditorGUILayout.HelpBox("Blackboard override storage is unavailable.", MessageType.Warning);
                return;
            }

            bool drewAny = false;
            IReadOnlyList<BehaviorTreeCompiledBlackboardKey> blackboard = compiledAsset.Blackboard;
            for (int i = 0; i < blackboard.Count; i++)
            {
                BehaviorTreeCompiledBlackboardKey key = blackboard[i];
                if (!BehaviorTreeRunnerBlackboardOverrideEditorUtility.IsVisibleKey(key))
                {
                    continue;
                }

                drewAny = true;
                DrawBlackboardOverrideRow(overridesProperty, key, CanAssignSceneObjects());
            }

            if (!drewAny)
            {
                EditorGUILayout.HelpBox("This behavior tree has no exposed blackboard keys.", MessageType.None);
            }
        }

        private bool CanAssignSceneObjects()
        {
            BehaviorTreeRunner runner = target as BehaviorTreeRunner;
            return runner != null && !EditorUtility.IsPersistent(runner);
        }

        private static void DrawBlackboardOverrideRow(
            SerializedProperty overridesProperty,
            BehaviorTreeCompiledBlackboardKey key,
            bool allowSceneObjects)
        {
            SerializedProperty entry = BehaviorTreeRunnerBlackboardOverrideEditorUtility.FindOverrideEntry(overridesProperty, key.Name);
            bool enabled = BehaviorTreeRunnerBlackboardOverrideEditorUtility.IsOverrideEnabled(entry);
            string currentJson = enabled
                ? BehaviorTreeRunnerBlackboardOverrideEditorUtility.GetString(entry, "JsonValue")
                : key.DefaultValueJson;
            UnityEngine.Object currentObject = enabled
                ? BehaviorTreeRunnerBlackboardOverrideEditorUtility.GetObject(entry, "ObjectValue")
                : null;
            string error;
            string editedJson;
            UnityEngine.Object editedObject;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent keyLabel = string.IsNullOrEmpty(key.Description)
                    ? new GUIContent(key.Name)
                    : new GUIContent(key.Name, key.Description);
                EditorGUILayout.LabelField(keyLabel, GUILayout.Width(NameWidth));
                EditorGUILayout.LabelField(key.Type, GUILayout.Width(TypeWidth));

                if (DrawValueField(key.Type, currentJson, currentObject, allowSceneObjects, out editedJson, out editedObject, out error))
                {
                    entry = BehaviorTreeRunnerBlackboardOverrideEditorUtility.EnsureOverrideEntry(overridesProperty, key);
                    BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetBool(entry, "Enabled", true);
                    if (BehaviorTreeRunnerBlackboardOverrideEditorUtility.IsObjectReferenceType(key.Type))
                    {
                        BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetString(entry, "JsonValue", string.Empty);
                        BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetObject(entry, "ObjectValue", editedObject);
                    }
                    else
                    {
                        BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetString(entry, "JsonValue", editedJson);
                        BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetObject(entry, "ObjectValue", null);
                    }

                    enabled = true;
                }

                bool newEnabled = GUILayout.Toggle(enabled, enabled ? "Override" : "Inherited", EditorStyles.miniButton, GUILayout.Width(ModeWidth));
                if (newEnabled != enabled)
                {
                    entry = BehaviorTreeRunnerBlackboardOverrideEditorUtility.EnsureOverrideEntry(overridesProperty, key);
                    BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetBool(entry, "Enabled", newEnabled);
                    if (newEnabled)
                    {
                        if (!BehaviorTreeRunnerBlackboardOverrideEditorUtility.IsObjectReferenceType(key.Type) &&
                            string.IsNullOrEmpty(BehaviorTreeRunnerBlackboardOverrideEditorUtility.GetString(entry, "JsonValue")))
                        {
                            BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetString(entry, "JsonValue", key.DefaultValueJson ?? string.Empty);
                        }
                    }
                    else
                    {
                        BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetString(entry, "JsonValue", string.Empty);
                        BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetObject(entry, "ObjectValue", null);
                    }
                }

                using (new EditorGUI.DisabledScope(!enabled && entry == null))
                {
                    if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(ResetWidth)))
                    {
                        entry = BehaviorTreeRunnerBlackboardOverrideEditorUtility.EnsureOverrideEntry(overridesProperty, key);
                        BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetBool(entry, "Enabled", false);
                        BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetString(entry, "JsonValue", string.Empty);
                        BehaviorTreeRunnerBlackboardOverrideEditorUtility.SetObject(entry, "ObjectValue", null);
                    }
                }
            }

            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(key.Name + ": " + error, MessageType.Warning);
            }
        }

        private static bool DrawValueField(
            string blackboardType,
            string jsonValue,
            UnityEngine.Object objectValue,
            bool allowSceneObjects,
            out string editedJson,
            out UnityEngine.Object editedObject,
            out string error)
        {
            editedJson = jsonValue ?? string.Empty;
            editedObject = objectValue;
            error = null;

            if (BehaviorTreeRunnerBlackboardOverrideEditorUtility.IsObjectReferenceType(blackboardType))
            {
                Type objectType = blackboardType == "Transform" ? typeof(Transform) : typeof(GameObject);
                EditorGUI.BeginChangeCheck();
                UnityEngine.Object newObject = EditorGUILayout.ObjectField(objectValue, objectType, allowSceneObjects);
                bool changed = EditorGUI.EndChangeCheck();
                if (changed)
                {
                    editedObject = newObject;
                }

                return changed;
            }

            if (ShouldUseJsonField(blackboardType))
            {
                EditorGUI.BeginChangeCheck();
                string newJson = EditorGUILayout.TextField(jsonValue ?? string.Empty);
                bool changed = EditorGUI.EndChangeCheck();
                if (!IsJsonAssignable(newJson, blackboardType, out error))
                {
                    // Keep invalid JSON editable so it can be fixed in place.
                }

                if (changed)
                {
                    editedJson = newJson;
                }

                return changed;
            }

            object value;
            bool valid = TryReadEditableValue(jsonValue, blackboardType, out value, out error);
            EditorGUI.BeginChangeCheck();
            object editedValue = DrawTypedValueField(blackboardType, value);
            bool typedChanged = EditorGUI.EndChangeCheck();
            if (typedChanged)
            {
                editedJson = BehaviorTreeCompiler.SerializeValueForType(editedValue, blackboardType);
                error = null;
            }
            else if (!valid)
            {
                // The row remains editable; changing the field will replace the invalid value.
            }

            return typedChanged;
        }

        private static object DrawTypedValueField(string blackboardType, object value)
        {
            switch (blackboardType)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    return EditorGUILayout.TextField(value as string ?? string.Empty);
                case "bool":
                    return EditorGUILayout.Toggle(value is bool && (bool)value);
                case "int":
                    return EditorGUILayout.IntField(Convert.ToInt32(value ?? 0));
                case "float":
                    return EditorGUILayout.FloatField(Convert.ToSingle(value ?? 0f));
                case "Vector2":
                    return EditorGUILayout.Vector2Field(GUIContent.none, value is Vector2 ? (Vector2)value : Vector2.zero);
                case "Vector3":
                    return EditorGUILayout.Vector3Field(GUIContent.none, value is Vector3 ? (Vector3)value : Vector3.zero);
                default:
                    return value;
            }
        }

        private static bool TryReadEditableValue(string jsonValue, string blackboardType, out object value, out string error)
        {
            value = null;
            error = null;
            object rawValue;
            if (!TryDeserializeJson(jsonValue, out rawValue, out error))
            {
                value = GetFallbackValue(blackboardType);
                return false;
            }

            if (!BlueprintTypeUtility.IsValueAssignableToType(rawValue, blackboardType))
            {
                error = "Value is not assignable to " + blackboardType + ".";
                value = GetFallbackValue(blackboardType);
                return false;
            }

            value = CoerceEditorValue(rawValue, blackboardType);
            return true;
        }

        private static object CoerceEditorValue(object value, string blackboardType)
        {
            switch (blackboardType)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    return BlueprintTypeUtility.ConvertValue(value, typeof(string), string.Empty);
                case "bool":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(bool), false);
                case "int":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(int), 0);
                case "float":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(float), 0f);
                case "Vector2":
                    return value is Vector2 ? value : BlueprintTypeUtility.ToVector2(value, Vector2.zero);
                case "Vector3":
                    return value is Vector3 ? value : BlueprintTypeUtility.ToVector3(value, Vector3.zero);
                default:
                    return value;
            }
        }

        private static object GetFallbackValue(string blackboardType)
        {
            switch (blackboardType)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    return string.Empty;
                case "bool":
                    return false;
                case "int":
                    return 0;
                case "float":
                    return 0f;
                case "Vector2":
                    return Vector2.zero;
                case "Vector3":
                    return Vector3.zero;
                default:
                    return null;
            }
        }

        private static bool ShouldUseJsonField(string blackboardType)
        {
            return !IsBuiltinEditableType(blackboardType);
        }

        private static bool IsBuiltinEditableType(string blackboardType)
        {
            return blackboardType == "string" ||
                   blackboardType == "bool" ||
                   blackboardType == "int" ||
                   blackboardType == "float" ||
                   blackboardType == "Vector2" ||
                   blackboardType == "Vector3" ||
                   blackboardType == BlueprintVariableTypeRegistry.BlueprintAssetTypeId;
        }

        private static bool IsJsonAssignable(string jsonValue, string blackboardType, out string error)
        {
            error = null;
            object value;
            if (!TryDeserializeJson(jsonValue, out value, out error))
            {
                return false;
            }

            if (!BlueprintTypeUtility.IsValueAssignableToType(value, blackboardType))
            {
                error = "Value is not assignable to " + blackboardType + ".";
                return false;
            }

            return true;
        }

        private static bool TryDeserializeJson(string jsonValue, out object value, out string error)
        {
            value = null;
            error = null;
            if (string.IsNullOrEmpty(jsonValue))
            {
                return true;
            }

            try
            {
                value = BlueprintJson.Deserialize(jsonValue);
                return true;
            }
            catch (BlueprintJsonException exception)
            {
                error = "Invalid JSON: " + exception.Message;
                return false;
            }
        }
    }

    internal static class BehaviorTreeRunnerBlackboardOverrideEditorUtility
    {
        public static void SyncOverrideArray(
            SerializedObject runnerObject,
            SerializedProperty overridesProperty,
            IReadOnlyList<BehaviorTreeCompiledBlackboardKey> blackboard)
        {
            if (runnerObject == null || overridesProperty == null || !overridesProperty.isArray)
            {
                return;
            }

            runnerObject.Update();
            Dictionary<string, BehaviorTreeCompiledBlackboardKey> visibleKeys = BuildVisibleKeyIndex(blackboard);
            HashSet<string> retainedNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = overridesProperty.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty entry = overridesProperty.GetArrayElementAtIndex(i);
                string name = GetString(entry, "Name");
                if (string.IsNullOrEmpty(name) || !visibleKeys.ContainsKey(name) || retainedNames.Contains(name))
                {
                    overridesProperty.DeleteArrayElementAtIndex(i);
                    continue;
                }

                retainedNames.Add(name);
            }

            if (blackboard != null)
            {
                for (int i = 0; i < blackboard.Count; i++)
                {
                    BehaviorTreeCompiledBlackboardKey key = blackboard[i];
                    if (!IsVisibleKey(key))
                    {
                        continue;
                    }

                    SerializedProperty entry = FindOverrideEntry(overridesProperty, key.Name);
                    if (entry == null)
                    {
                        int newIndex = overridesProperty.arraySize;
                        overridesProperty.InsertArrayElementAtIndex(newIndex);
                        entry = overridesProperty.GetArrayElementAtIndex(newIndex);
                        SetBool(entry, "Enabled", false);
                        SetString(entry, "JsonValue", string.Empty);
                        SetObject(entry, "ObjectValue", null);
                    }

                    string previousType = GetString(entry, "Type");
                    if (!string.IsNullOrEmpty(previousType) && previousType != key.Type)
                    {
                        SetBool(entry, "Enabled", false);
                        SetString(entry, "JsonValue", string.Empty);
                        SetObject(entry, "ObjectValue", null);
                    }

                    SetString(entry, "VariableId", string.Empty);
                    SetString(entry, "Name", key.Name);
                    SetString(entry, "Type", key.Type);
                    if (!IsObjectReferenceType(key.Type))
                    {
                        SetObject(entry, "ObjectValue", null);
                    }
                }
            }

            runnerObject.ApplyModifiedProperties();
        }

        internal static bool IsVisibleKey(BehaviorTreeCompiledBlackboardKey key)
        {
            return key != null &&
                   !string.IsNullOrEmpty(key.Name) &&
                   key.Exposed &&
                   key.Type != BehaviorTreeValueUtility.NavMeshPathTypeId;
        }

        internal static bool IsObjectReferenceType(string blackboardType)
        {
            return blackboardType == "GameObject" || blackboardType == "Transform";
        }

        internal static SerializedProperty FindOverrideEntry(SerializedProperty overridesProperty, string keyName)
        {
            if (overridesProperty == null || string.IsNullOrEmpty(keyName))
            {
                return null;
            }

            for (int i = 0; i < overridesProperty.arraySize; i++)
            {
                SerializedProperty entry = overridesProperty.GetArrayElementAtIndex(i);
                if (GetString(entry, "Name") == keyName)
                {
                    return entry;
                }
            }

            return null;
        }

        internal static SerializedProperty EnsureOverrideEntry(
            SerializedProperty overridesProperty,
            BehaviorTreeCompiledBlackboardKey key)
        {
            SerializedProperty entry = FindOverrideEntry(overridesProperty, key.Name);
            if (entry == null)
            {
                int newIndex = overridesProperty.arraySize;
                overridesProperty.InsertArrayElementAtIndex(newIndex);
                entry = overridesProperty.GetArrayElementAtIndex(newIndex);
            }

            SetString(entry, "VariableId", string.Empty);
            SetString(entry, "Name", key.Name);
            SetString(entry, "Type", key.Type);
            return entry;
        }

        internal static bool IsOverrideEnabled(SerializedProperty entry)
        {
            if (entry == null)
            {
                return false;
            }

            SerializedProperty enabledProperty = entry.FindPropertyRelative("Enabled");
            if (enabledProperty != null && enabledProperty.boolValue)
            {
                return true;
            }

            return string.IsNullOrEmpty(GetString(entry, "VariableId")) &&
                   !string.IsNullOrEmpty(GetString(entry, "Name")) &&
                   (!string.IsNullOrEmpty(GetString(entry, "JsonValue")) || GetObject(entry, "ObjectValue") != null);
        }

        internal static string GetString(SerializedProperty parent, string propertyName)
        {
            SerializedProperty property = parent == null ? null : parent.FindPropertyRelative(propertyName);
            return property == null ? null : property.stringValue;
        }

        internal static UnityEngine.Object GetObject(SerializedProperty parent, string propertyName)
        {
            SerializedProperty property = parent == null ? null : parent.FindPropertyRelative(propertyName);
            return property == null ? null : property.objectReferenceValue;
        }

        internal static void SetString(SerializedProperty parent, string propertyName, string value)
        {
            SerializedProperty property = parent == null ? null : parent.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        internal static void SetBool(SerializedProperty parent, string propertyName, bool value)
        {
            SerializedProperty property = parent == null ? null : parent.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        internal static void SetObject(SerializedProperty parent, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = parent == null ? null : parent.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static Dictionary<string, BehaviorTreeCompiledBlackboardKey> BuildVisibleKeyIndex(
            IReadOnlyList<BehaviorTreeCompiledBlackboardKey> blackboard)
        {
            Dictionary<string, BehaviorTreeCompiledBlackboardKey> result =
                new Dictionary<string, BehaviorTreeCompiledBlackboardKey>(StringComparer.Ordinal);
            if (blackboard == null)
            {
                return result;
            }

            for (int i = 0; i < blackboard.Count; i++)
            {
                BehaviorTreeCompiledBlackboardKey key = blackboard[i];
                if (IsVisibleKey(key))
                {
                    result[key.Name] = key;
                }
            }

            return result;
        }
    }
}
