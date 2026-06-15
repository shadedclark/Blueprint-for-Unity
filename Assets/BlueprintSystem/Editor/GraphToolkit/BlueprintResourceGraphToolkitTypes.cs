using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class BlueprintResourceTypePopupAttribute : PropertyAttribute
    {
    }

    [Serializable]
    public struct BlueprintResourceTypeReference
    {
        public string ResourceType;

        public BlueprintResourceTypeReference(string resourceType)
        {
            ResourceType = BlueprintResourceGraphToolkitTypes.NormalizeResourceType(resourceType);
        }

        public override string ToString()
        {
            return ResourceType ?? string.Empty;
        }
    }

    public static class BlueprintResourceGraphToolkitTypes
    {
        public static string[] GetResourceTypes()
        {
            return GetResourceTypes(null);
        }

        public static string[] GetResourceTypes(string currentValue)
        {
            SortedSet<string> result = new SortedSet<string>(StringComparer.Ordinal);
            List<string> catalogPaths = BlueprintResourceAssetManagerUtility.FindResourceTypeCatalogAssetPaths();
            for (int i = 0; i < catalogPaths.Count; i++)
            {
                BlueprintResourceTypeCatalogAsset catalog =
                    AssetDatabase.LoadAssetAtPath<BlueprintResourceTypeCatalogAsset>(catalogPaths[i]);
                if (catalog == null || catalog.ResourceTypes == null)
                {
                    continue;
                }

                for (int typeIndex = 0; typeIndex < catalog.ResourceTypes.Count; typeIndex++)
                {
                    AddResourceType(result, catalog.ResourceTypes[typeIndex]);
                }
            }

            if (currentValue != null)
            {
                result.Add(NormalizeResourceType(currentValue));
            }

            return ToArray(result);
        }

        private static void AddResourceType(SortedSet<string> result, BlueprintResourceTypeDefinition definition)
        {
            if (definition != null)
            {
                AddResourceType(result, definition.ResourceType);
            }
        }

        private static void AddResourceType(SortedSet<string> result, string resourceType)
        {
            string normalized = NormalizeResourceType(resourceType);
            if (!string.IsNullOrEmpty(normalized))
            {
                result.Add(normalized);
            }
        }

        public static string NormalizeResourceType(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        }

        public static BlueprintResourceTypeReference CreateResourceTypeReference(string value)
        {
            return new BlueprintResourceTypeReference(value);
        }

        public static bool TryGetResourceType(object value, out string resourceType)
        {
            if (value is BlueprintResourceTypeReference)
            {
                resourceType = NormalizeResourceType(((BlueprintResourceTypeReference)value).ResourceType);
                return true;
            }

            resourceType = NormalizeResourceType(value == null ? null : Convert.ToString(value));
            return true;
        }

        public static BlueprintPrimaryResourceId[] GetResourceIds(string resourceType)
        {
            List<BlueprintPrimaryResourceId> result = new List<BlueprintPrimaryResourceId>();
            if (string.IsNullOrEmpty(resourceType))
            {
                return result.ToArray();
            }

            HashSet<BlueprintPrimaryResourceId> seen = new HashSet<BlueprintPrimaryResourceId>();
            List<string> paths = BlueprintEditorAssetDiscovery.FindTextAssetPaths(BlueprintResourceBlueprintSource.AssetExtension);
            for (int i = 0; i < paths.Count; i++)
            {
                try
                {
                    TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(paths[i]);
                    string json = textAsset == null ? System.IO.File.ReadAllText(paths[i]) : textAsset.text;
                    BlueprintResourceBlueprintSource source = BlueprintResourceBlueprintSource.FromJson(json);
                    if (source == null || !source.Id.IsValid ||
                        !string.Equals(source.ResourceType, resourceType, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (seen.Add(source.Id))
                    {
                        result.Add(source.Id);
                    }
                }
                catch (Exception)
                {
                    // Ignore invalid sources in editor dropdowns; validation reports them elsewhere.
                }
            }

            result.Sort(delegate(BlueprintPrimaryResourceId left, BlueprintPrimaryResourceId right)
            {
                return string.CompareOrdinal(left.ResourceName, right.ResourceName);
            });
            return result.ToArray();
        }

        public static string[] GetResourceNames(string resourceType)
        {
            return GetResourceNames(resourceType, null);
        }

        public static string[] GetResourceNames(string resourceType, string currentValue)
        {
            SortedSet<string> result = new SortedSet<string>(StringComparer.Ordinal);
            BlueprintPrimaryResourceId[] ids = GetResourceIds(resourceType);
            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrEmpty(ids[i].ResourceName))
                {
                    result.Add(ids[i].ResourceName);
                }
            }

            if (!string.IsNullOrEmpty(currentValue))
            {
                result.Add(currentValue.Trim());
            }

            return ToArray(result);
        }

        public static UnityEngine.Object ResolveAssetReference(BlueprintResourceAssetReference reference)
        {
            if (reference == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(reference.Guid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(reference.Guid);
                UnityEngine.Object guidAsset = string.IsNullOrEmpty(guidPath)
                    ? null
                    : AssetDatabase.LoadMainAssetAtPath(guidPath);
                if (guidAsset != null)
                {
                    return guidAsset;
                }
            }

            string path = BlueprintAssetDiscovery.NormalizeAssetPath(reference.Path);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
        }

        public static void ApplyAssetReference(BlueprintResourceAssetReference reference, UnityEngine.Object asset)
        {
            if (reference == null)
            {
                return;
            }

            if (asset == null)
            {
                reference.Guid = string.Empty;
                reference.Path = string.Empty;
                reference.Address = string.Empty;
                reference.AssetType = string.Empty;
                return;
            }

            string assetPath = BlueprintAssetDiscovery.NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            reference.Path = assetPath;
            reference.Guid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            reference.Address = string.Empty;
            reference.AssetType = asset.GetType().Name;
        }

        public static string GetAssetReferenceName(BlueprintResourceAssetReference reference)
        {
            if (reference == null)
            {
                return string.Empty;
            }

            UnityEngine.Object asset = ResolveAssetReference(reference);
            if (asset != null && !string.IsNullOrEmpty(asset.name))
            {
                return asset.name;
            }

            string path = BlueprintAssetDiscovery.NormalizeAssetPath(reference.Path);
            return string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileNameWithoutExtension(path);
        }

        private static string[] ToArray(SortedSet<string> values)
        {
            string[] result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }
    }

    [CustomPropertyDrawer(typeof(BlueprintResourceTypePopupAttribute))]
    internal sealed class BlueprintResourceTypePopupDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            BlueprintResourceGraphToolkitDrawerUtility.DrawResourceTypeField(position, label, property);
        }
    }

    [CustomPropertyDrawer(typeof(BlueprintResourceTypeReference), true)]
    internal sealed class BlueprintResourceTypeReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty resourceTypeProperty = property.FindPropertyRelative("ResourceType");
            if (resourceTypeProperty == null || resourceTypeProperty.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            BlueprintResourceGraphToolkitDrawerUtility.DrawResourceTypeField(position, label, resourceTypeProperty);
        }
    }

    [CustomPropertyDrawer(typeof(BlueprintResourceAssetReference), true)]
    internal sealed class BlueprintResourceAssetReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty guidProperty = property.FindPropertyRelative("Guid");
            SerializedProperty pathProperty = property.FindPropertyRelative("Path");
            SerializedProperty addressProperty = property.FindPropertyRelative("Address");
            SerializedProperty assetTypeProperty = property.FindPropertyRelative("AssetType");
            if (guidProperty == null || pathProperty == null || addressProperty == null || assetTypeProperty == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect assetRect = new Rect(position.x, position.y, position.width, lineHeight);
            Rect pathRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);
            Rect guidRect = new Rect(position.x, position.y + (lineHeight + spacing) * 2f, position.width, lineHeight);
            Rect typeRect = new Rect(position.x, position.y + (lineHeight + spacing) * 3f, position.width, lineHeight);
            Rect addressRect = new Rect(position.x, position.y + (lineHeight + spacing) * 4f, position.width, lineHeight);

            BlueprintResourceAssetReference reference = ReadReference(guidProperty, pathProperty, addressProperty, assetTypeProperty);
            UnityEngine.Object currentAsset = BlueprintResourceGraphToolkitTypes.ResolveAssetReference(reference);
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object selectedAsset = EditorGUI.ObjectField(assetRect, label, currentAsset, typeof(UnityEngine.Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                BlueprintResourceGraphToolkitTypes.ApplyAssetReference(reference, selectedAsset);
                WriteReference(reference, guidProperty, pathProperty, addressProperty, assetTypeProperty);
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.TextField(pathRect, "Path", pathProperty.stringValue);
            EditorGUI.TextField(guidRect, "GUID", guidProperty.stringValue);
            EditorGUI.TextField(typeRect, "Asset Type", assetTypeProperty.stringValue);
            EditorGUI.TextField(addressRect, "Address (Asset Manager)", addressProperty.stringValue);
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 5f + EditorGUIUtility.standardVerticalSpacing * 4f;
        }

        private static BlueprintResourceAssetReference ReadReference(
            SerializedProperty guidProperty,
            SerializedProperty pathProperty,
            SerializedProperty addressProperty,
            SerializedProperty assetTypeProperty)
        {
            return new BlueprintResourceAssetReference
            {
                Guid = guidProperty.stringValue,
                Path = pathProperty.stringValue,
                Address = addressProperty.stringValue,
                AssetType = assetTypeProperty.stringValue
            };
        }

        private static void WriteReference(
            BlueprintResourceAssetReference reference,
            SerializedProperty guidProperty,
            SerializedProperty pathProperty,
            SerializedProperty addressProperty,
            SerializedProperty assetTypeProperty)
        {
            guidProperty.stringValue = reference.Guid ?? string.Empty;
            pathProperty.stringValue = reference.Path ?? string.Empty;
            addressProperty.stringValue = reference.Address ?? string.Empty;
            assetTypeProperty.stringValue = reference.AssetType ?? string.Empty;
        }
    }

    [CustomPropertyDrawer(typeof(BlueprintResourceDependency), true)]
    internal sealed class BlueprintResourceDependencyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty resourceTypeProperty = property.FindPropertyRelative("ResourceType");
            SerializedProperty resourceNameProperty = property.FindPropertyRelative("ResourceName");
            SerializedProperty requiredProperty = property.FindPropertyRelative("Required");
            SerializedProperty preloadGroupProperty = property.FindPropertyRelative("PreloadGroup");
            if (resourceTypeProperty == null || resourceNameProperty == null ||
                requiredProperty == null || preloadGroupProperty == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                Rect typeRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);
                Rect nameRect = new Rect(position.x, position.y + (lineHeight + spacing) * 2f, position.width, lineHeight);
                Rect requiredRect = new Rect(position.x, position.y + (lineHeight + spacing) * 3f, position.width, lineHeight);
                Rect preloadRect = new Rect(position.x, position.y + (lineHeight + spacing) * 4f, position.width, lineHeight);

                BlueprintResourceGraphToolkitDrawerUtility.DrawResourceTypeField(
                    typeRect,
                    new GUIContent("Resource Type"),
                    resourceTypeProperty);
                BlueprintResourceGraphToolkitDrawerUtility.DrawResourceNameField(
                    nameRect,
                    new GUIContent("Resource Name"),
                    resourceTypeProperty.stringValue,
                    resourceNameProperty);
                EditorGUI.PropertyField(requiredRect, requiredProperty);
                EditorGUI.PropertyField(preloadRect, preloadGroupProperty);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUIUtility.singleLineHeight * 5f + EditorGUIUtility.standardVerticalSpacing * 4f;
        }
    }

    internal static class BlueprintResourceGraphToolkitDrawerUtility
    {
        public static void DrawResourceTypeField(Rect position, GUIContent label, SerializedProperty property)
        {
            DrawPopupAndTextField(
                position,
                label,
                property,
                BlueprintResourceGraphToolkitTypes.GetResourceTypes(property.stringValue),
                "No Types",
                delegate(string resourceType)
                {
                    BlueprintResourceAssetManagerUtility.RegisterResourceType(resourceType);
                });
        }

        public static void DrawResourceNameField(Rect position, GUIContent label, string resourceType, SerializedProperty property)
        {
            DrawPopupAndTextField(
                position,
                label,
                property,
                BlueprintResourceGraphToolkitTypes.GetResourceNames(resourceType, property.stringValue),
                string.IsNullOrEmpty(resourceType) ? "No Type" : "No Resources",
                null);
        }

        private static void DrawPopupAndTextField(
            Rect position,
            GUIContent label,
            SerializedProperty property,
            string[] options,
            string emptyLabel,
            Action<string> valueCommitted)
        {
            Rect controlRect = EditorGUI.PrefixLabel(position, label);
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float popupWidth = Mathf.Max(80f, controlRect.width * 0.45f);
            Rect popupRect = new Rect(controlRect.x, controlRect.y, popupWidth, controlRect.height);
            Rect textRect = new Rect(
                popupRect.xMax + spacing,
                controlRect.y,
                Mathf.Max(0f, controlRect.width - popupWidth - spacing),
                controlRect.height);

            if (options.Length == 0)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.Popup(popupRect, 0, new[] { new GUIContent(emptyLabel) });
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                GUIContent[] labels = new GUIContent[options.Length];
                for (int i = 0; i < options.Length; i++)
                {
                    labels[i] = new GUIContent(string.IsNullOrEmpty(options[i]) ? "(Empty)" : options[i]);
                }

                int selected = FindIndex(options, property.stringValue);
                EditorGUI.BeginChangeCheck();
                int next = EditorGUI.Popup(popupRect, Mathf.Max(0, selected), labels);
                if (EditorGUI.EndChangeCheck() && next >= 0 && next < options.Length)
                {
                    property.stringValue = options[next];
                    if (valueCommitted != null)
                    {
                        valueCommitted(property.stringValue);
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            string edited = valueCommitted == null
                ? EditorGUI.TextField(textRect, property.stringValue)
                : EditorGUI.DelayedTextField(textRect, property.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = edited == null ? string.Empty : edited.Trim();
                if (valueCommitted != null)
                {
                    valueCommitted(property.stringValue);
                }
            }
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
}
