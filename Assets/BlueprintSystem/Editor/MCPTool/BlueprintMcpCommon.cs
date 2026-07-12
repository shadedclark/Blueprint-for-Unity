using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlueprintSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlueprintLangGraph.Editor
{
    internal static class BlueprintMcpCommon
    {
        internal static object Success(string message, object data)
        {
            return new
            {
                success = true,
                message,
                data
            };
        }

        internal static object Failure(
            string code,
            string error,
            object details = null,
            IEnumerable<object> partialResults = null,
            bool retryable = false)
        {
            return new
            {
                success = false,
                error,
                data = new
                {
                    code,
                    details = details ?? new { },
                    partialResults = partialResults == null ? Array.Empty<object>() : partialResults.ToArray(),
                    retryable
                }
            };
        }

        internal static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/').Trim();
            string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/').TrimEnd('/');
            if (normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(projectRoot.Length + 1);
            }

            while (normalized.Contains("//"))
            {
                normalized = normalized.Replace("//", "/");
            }

            return normalized.TrimStart('/');
        }

        internal static bool IsProjectAssetPath(string path, bool includePackages = false)
        {
            string normalized = NormalizeAssetPath(path);
            return normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase) ||
                   (includePackages && (normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(normalized, "Packages", StringComparison.OrdinalIgnoreCase)));
        }

        internal static string ToProjectFilePath(string assetPath)
        {
            return Path.Combine(
                Directory.GetCurrentDirectory(),
                NormalizeAssetPath(assetPath).Replace('/', Path.DirectorySeparatorChar));
        }

        internal static bool AssetOrDirectoryExists(string path)
        {
            string normalized = NormalizeAssetPath(path);
            return File.Exists(ToProjectFilePath(normalized)) || Directory.Exists(ToProjectFilePath(normalized));
        }

        internal static string NormalizeHierarchyPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim('/');
        }

        internal static GameObject FindLoadedSceneObject(string hierarchyPath)
        {
            string normalized = NormalizeHierarchyPath(hierarchyPath);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    GameObject match = FindGameObjectByPath(root, normalized);
                    if (match != null)
                    {
                        return match;
                    }
                }
            }

            return null;
        }

        internal static GameObject FindGameObjectByPath(GameObject root, string hierarchyPath)
        {
            if (root == null)
            {
                return null;
            }

            string normalized = NormalizeHierarchyPath(hierarchyPath);
            if (string.IsNullOrEmpty(normalized) || string.Equals(normalized, root.name, StringComparison.Ordinal))
            {
                return root;
            }

            if (normalized.StartsWith(root.name + "/", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(root.name.Length + 1);
            }

            Transform current = root.transform;
            foreach (string segment in normalized.Split('/'))
            {
                Transform next = null;
                for (int i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);
                    if (string.Equals(child.name, segment, StringComparison.Ordinal))
                    {
                        next = child;
                        break;
                    }
                }

                if (next == null)
                {
                    return null;
                }

                current = next;
            }

            return current.gameObject;
        }

        internal static BlueprintRunner FindRunner(GameObject root, string runnerPath)
        {
            if (root == null)
            {
                return null;
            }

            GameObject runnerObject = string.IsNullOrWhiteSpace(runnerPath)
                ? root
                : FindGameObjectByPath(root, runnerPath);
            return runnerObject == null ? null : runnerObject.GetComponent<BlueprintRunner>();
        }

        internal static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var segments = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                segments.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", segments);
        }

        internal static object ToSerializableValue(object value, int maxCollectionItems = 50, int depth = 0)
        {
            if (value == null)
            {
                return null;
            }

            if (depth >= 4)
            {
                return new { truncated = true, reason = "maxDepth" };
            }

            if (value is Vector2 vector2)
            {
                return new[] { vector2.x, vector2.y };
            }

            if (value is Vector3 vector3)
            {
                return new[] { vector3.x, vector3.y, vector3.z };
            }

            if (value is Vector4 vector4)
            {
                return new[] { vector4.x, vector4.y, vector4.z, vector4.w };
            }

            if (value is Quaternion quaternion)
            {
                Vector3 euler = quaternion.eulerAngles;
                return new[] { euler.x, euler.y, euler.z };
            }

            if (value is Color color)
            {
                return new[] { color.r, color.g, color.b, color.a };
            }

            if (value is BlueprintRef blueprintRef)
            {
                IBlueprintInstance instance = blueprintRef.Instance;
                return new
                {
                    type = "BlueprintRef",
                    isValid = blueprintRef.IsValid,
                    instanceName = instance == null ? string.Empty : instance.InstanceName ?? string.Empty,
                    blueprintPath = instance == null ? string.Empty : instance.SourcePath ?? string.Empty
                };
            }

            if (value is UnityEngine.Object unityObject)
            {
                return SerializeUnityObject(unityObject);
            }

            if (value is string || value.GetType().IsPrimitive || value is decimal || value is Enum)
            {
                return value;
            }

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                var serialized = new Dictionary<string, object>(StringComparer.Ordinal);
                int count = 0;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (count++ >= Mathf.Max(1, maxCollectionItems))
                    {
                        serialized["_truncated"] = true;
                        break;
                    }

                    serialized[Convert.ToString(entry.Key) ?? string.Empty] = ToSerializableValue(entry.Value, maxCollectionItems, depth + 1);
                }

                return serialized;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                var serialized = new List<object>();
                int count = 0;
                bool truncated = false;
                foreach (object item in enumerable)
                {
                    if (count++ >= Mathf.Max(1, maxCollectionItems))
                    {
                        truncated = true;
                        break;
                    }

                    serialized.Add(ToSerializableValue(item, maxCollectionItems, depth + 1));
                }

                return truncated ? new { items = serialized.ToArray(), truncated = true } : (object)serialized.ToArray();
            }

            return Convert.ToString(value);
        }

        private static object SerializeUnityObject(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                return null;
            }

            string scenePath = string.Empty;
            GameObject gameObject = unityObject as GameObject;
            if (gameObject != null)
            {
                scenePath = GetHierarchyPath(gameObject.transform);
            }
            else
            {
                Component component = unityObject as Component;
                if (component != null)
                {
                    scenePath = GetHierarchyPath(component.transform);
                }
            }

            return new
            {
                type = unityObject.GetType().FullName,
                name = unityObject.name,
                instanceId = unityObject.GetInstanceID(),
                assetPath = AssetDatabase.GetAssetPath(unityObject) ?? string.Empty,
                scenePath
            };
        }
    }
}
