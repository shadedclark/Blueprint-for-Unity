using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [CustomEditor(typeof(SmartObjectComponent))]
    [CanEditMultipleObjects]
    internal sealed class SmartObjectComponentEditor : UnityEditor.Editor
    {
        private SerializedProperty objectIdProperty;

        private void OnEnable()
        {
            objectIdProperty = serializedObject.FindProperty("objectId");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(objectIdProperty, new GUIContent("Object Id"));
            }

            using (new EditorGUI.DisabledScope(objectIdProperty == null || objectIdProperty.hasMultipleDifferentValues))
            {
                if (GUILayout.Button("Copy Object Id"))
                {
                    EditorGUIUtility.systemCopyBuffer = objectIdProperty.stringValue ?? string.Empty;
                }
            }

            EditorGUILayout.Space();
            DrawPropertiesExcluding(serializedObject, "m_Script", "objectId");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
