using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [Serializable]
    public struct BehaviorTreeAsset
    {
        public string Path;

        public BehaviorTreeAsset(string path)
        {
            Path = BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(path);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Path) ? string.Empty : Path;
        }
    }

    [CustomPropertyDrawer(typeof(BehaviorTreeAsset), true)]
    internal sealed class BehaviorTreeGraphToolkitBehaviorTreeAssetDrawer : PropertyDrawer
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

            BehaviorTreeVisualGraph graph = null;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            const float openButtonWidth = 54f;

            Rect assetRect = new Rect(
                position.x,
                position.y,
                Mathf.Max(0f, position.width - openButtonWidth - spacing),
                lineHeight);
            Rect openRect = new Rect(
                assetRect.xMax + spacing,
                position.y,
                openButtonWidth,
                lineHeight);

            UnityEngine.Object currentAsset =
                BehaviorTreeGraphToolkitBehaviorTreeTypes.LoadAsset(pathProperty.stringValue, graph);
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object selectedAsset = EditorGUI.ObjectField(assetRect, label, currentAsset, typeof(UnityEngine.Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                string selectedPath = BehaviorTreeGraphToolkitBehaviorTreeTypes.GetBehaviorTreeAssetPath(selectedAsset);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    pathProperty.stringValue = selectedPath;
                }
                else if (selectedAsset == null)
                {
                    pathProperty.stringValue = string.Empty;
                }
            }

            using (new EditorGUI.DisabledScope(!BehaviorTreeGraphToolkitBehaviorTreeTypes.CanOpen(pathProperty.stringValue, graph)))
            {
                if (GUI.Button(openRect, "Open"))
                {
                    BehaviorTreeGraphToolkitBehaviorTreeTypes.OpenAsset(pathProperty.stringValue, graph);
                }
            }

            pathProperty.stringValue = BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(
                pathProperty.stringValue);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }

    internal static class BehaviorTreeGraphToolkitBehaviorTreeTypes
    {
        public static BehaviorTreeAsset CreateGraphValue(object value)
        {
            if (value is BehaviorTreeAsset)
            {
                return (BehaviorTreeAsset)value;
            }

            return new BehaviorTreeAsset(value == null ? string.Empty : Convert.ToString(value));
        }

        public static bool TryGetPath(object graphValue, out string path)
        {
            path = null;
            if (graphValue == null)
            {
                return false;
            }

            if (graphValue is BehaviorTreeAsset)
            {
                BehaviorTreeAsset value = (BehaviorTreeAsset)graphValue;
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

        public static UnityEngine.Object LoadAsset(string path, BehaviorTreeVisualGraph ownerGraph)
        {
            string resolvedPath = ResolveAssetPath(path, ownerGraph);
            if (string.IsNullOrEmpty(resolvedPath))
            {
                return null;
            }

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<TextAsset>(resolvedPath);
            if (asset != null)
            {
                return asset;
            }

            return AssetDatabase.LoadAssetAtPath<BehaviorTreeCompiledAsset>(resolvedPath);
        }

        public static string GetBehaviorTreeAssetPath(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return null;
            }

            string path = NormalizePath(AssetDatabase.GetAssetPath(asset));
            if (BehaviorTreeCompiledAssetCompiler.IsBehaviorTreeJsonPath(path))
            {
                return path;
            }

            return asset is BehaviorTreeCompiledAsset ? path : null;
        }

        public static bool CanOpen(string path, BehaviorTreeVisualGraph ownerGraph)
        {
            return !string.IsNullOrEmpty(ResolveAssetPath(path, ownerGraph));
        }

        public static bool OpenAsset(string path, BehaviorTreeVisualGraph ownerGraph)
        {
            string resolvedPath = ResolveAssetPath(path, ownerGraph);
            return !string.IsNullOrEmpty(resolvedPath) && BehaviorTreeGraphToolkitBridge.OpenAssetAtPath(resolvedPath);
        }

        public static string GetDisplayName(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return string.Empty;
            }

            string fileName = Path.GetFileName(normalizedPath);
            if (string.IsNullOrEmpty(fileName))
            {
                return normalizedPath;
            }

            if (fileName.EndsWith(".btree.json", StringComparison.OrdinalIgnoreCase))
            {
                return fileName.Substring(0, fileName.Length - ".btree.json".Length);
            }

            if (fileName.EndsWith(".compiled.asset", StringComparison.OrdinalIgnoreCase))
            {
                return fileName.Substring(0, fileName.Length - ".compiled.asset".Length);
            }

            return Path.GetFileNameWithoutExtension(fileName);
        }

        public static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static string ResolveAssetPath(string path, BehaviorTreeVisualGraph ownerGraph)
        {
            string normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return null;
            }

            if (IsDirectAssetPath(normalizedPath) &&
                IsBehaviorTreeAssetPath(normalizedPath) &&
                AssetDatabase.LoadMainAssetAtPath(normalizedPath) != null)
            {
                return normalizedPath;
            }

            string ownerSourcePath = ownerGraph == null ? null : NormalizePath(ownerGraph.SourceBehaviorTreeAssetPath);
            if (!string.IsNullOrEmpty(ownerSourcePath))
            {
                string ownerDirectory = NormalizePath(Path.GetDirectoryName(ownerSourcePath));
                if (!string.IsNullOrEmpty(ownerDirectory))
                {
                    string relativePath = NormalizePath(Path.Combine(ownerDirectory, normalizedPath));
                    if (IsBehaviorTreeAssetPath(relativePath) && AssetDatabase.LoadMainAssetAtPath(relativePath) != null)
                    {
                        return relativePath;
                    }
                }
            }

            string assetName = GetDisplayName(normalizedPath);
            if (!string.IsNullOrEmpty(assetName))
            {
                string[] guids = AssetDatabase.FindAssets(assetName + " t:Object");
                for (int i = 0; i < guids.Length; i++)
                {
                    string candidatePath = NormalizePath(AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (Path.GetFileName(candidatePath) == Path.GetFileName(normalizedPath) &&
                        IsBehaviorTreeAssetPath(candidatePath))
                    {
                        return candidatePath;
                    }
                }
            }

            return null;
        }

        private static bool IsDirectAssetPath(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal) ||
                   path.StartsWith("Packages/", StringComparison.Ordinal);
        }

        private static bool IsBehaviorTreeAssetPath(string path)
        {
            return BehaviorTreeCompiledAssetCompiler.IsBehaviorTreeJsonPath(path) ||
                   path.EndsWith(".compiled.asset", StringComparison.OrdinalIgnoreCase);
        }
    }
}
