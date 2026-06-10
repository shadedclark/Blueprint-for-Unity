using System;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BlueprintSystem
{
    public static class BlueprintAssetDiscovery
    {
        public const string PackageName = "com.shadedclark.blueprint-system";
        public const string PackageAssetRoot = "Packages/" + PackageName;
        public const string EmbeddedAssetRoot = "Assets/BlueprintSystem";
        public const string ProjectAssetRoot = "Assets";

        public static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }

        public static bool IsAssetDatabasePath(string path)
        {
            path = NormalizeAssetPath(path);
            return !string.IsNullOrEmpty(path) &&
                   (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsProjectAssetPath(string path)
        {
            path = NormalizeAssetPath(path);
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith(ProjectAssetRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        public static string ChangeAssetPathExtension(string assetPath, string extension)
        {
            string normalizedPath = NormalizeAssetPath(assetPath);
            if (IsAssetDatabasePath(normalizedPath))
            {
                return NormalizeAssetPath(Path.ChangeExtension(normalizedPath, extension));
            }

            return normalizedPath;
        }

#if UNITY_EDITOR
        public static string[] GetRegistrySearchRoots()
        {
            List<string> roots = new List<string>();
            AddAssetRootIfValid(roots, PackageAssetRoot);
            AddAssetRootIfValid(roots, EmbeddedAssetRoot);
            AddAssetRootIfValid(roots, ProjectAssetRoot);
            return roots.ToArray();
        }

        public static string[] GetBlueprintPackageRoots()
        {
            List<string> roots = new List<string>();
            AddAssetRootIfValid(roots, PackageAssetRoot);
            AddAssetRootIfValid(roots, EmbeddedAssetRoot);
            return roots.ToArray();
        }

        public static bool IsDiscoverableAssetPath(string path)
        {
            path = NormalizeAssetPath(path);
            if (IsProjectAssetPath(path))
            {
                return true;
            }

            string[] packageRoots = GetBlueprintPackageRoots();
            for (int i = 0; i < packageRoots.Length; i++)
            {
                if (IsPathInRoot(path, packageRoots[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static List<string> FindTextAssetPaths(string extension)
        {
            return FindAssetPaths("t:TextAsset", extension, GetRegistrySearchRoots());
        }

        public static List<string> FindAssetPaths(string filter)
        {
            return FindAssetPaths(filter, null, GetRegistrySearchRoots());
        }

        public static List<string> FindAssetPaths(string filter, string extension, IEnumerable<string> searchRoots)
        {
            HashSet<string> rootPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> result = new List<string>();
            if (searchRoots == null)
            {
                return result;
            }

            foreach (string root in searchRoots)
            {
                string normalizedRoot = NormalizeAssetPath(root);
                if (string.IsNullOrEmpty(normalizedRoot) || !AssetDatabase.IsValidFolder(normalizedRoot))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets(filter, new[] { normalizedRoot });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (string.IsNullOrEmpty(path) || !IsPathInRoot(path, normalizedRoot))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(extension) && !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    rootPathSet.Add(path);
                }

                List<string> rootPaths = new List<string>(rootPathSet);
                rootPaths.Sort(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < rootPaths.Count; i++)
                {
                    if (seenPaths.Add(rootPaths[i]))
                    {
                        result.Add(rootPaths[i]);
                    }
                }
                rootPathSet.Clear();
            }

            return result;
        }

        public static bool IsPathInRoot(string path, string root)
        {
            path = NormalizeAssetPath(path);
            root = NormalizeAssetPath(root);
            return !string.IsNullOrEmpty(path) &&
                   !string.IsNullOrEmpty(root) &&
                   path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddAssetRootIfValid(List<string> roots, string root)
        {
            root = NormalizeAssetPath(root);
            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root) || roots.Contains(root))
            {
                return;
            }

            roots.Add(root);
        }
#endif
    }
}
