using System;
using System.Collections.Generic;
using UnityEditor;

namespace BlueprintSystem.Editor
{
    public static class BlueprintEditorAssetDiscovery
    {
        public static string[] GetRegistrySearchRoots()
        {
            List<string> roots = new List<string>();
            AddAssetRootIfValid(roots, BlueprintAssetDiscovery.PackageAssetRoot);
            AddAssetRootIfValid(roots, BlueprintAssetDiscovery.EmbeddedAssetRoot);
            AddAssetRootIfValid(roots, BlueprintAssetDiscovery.ProjectAssetRoot);
            return roots.ToArray();
        }

        public static string[] GetBlueprintPackageRoots()
        {
            List<string> roots = new List<string>();
            AddAssetRootIfValid(roots, BlueprintAssetDiscovery.PackageAssetRoot);
            AddAssetRootIfValid(roots, BlueprintAssetDiscovery.EmbeddedAssetRoot);
            return roots.ToArray();
        }

        public static bool IsDiscoverableAssetPath(string path)
        {
            path = NormalizeAssetPath(path);
            if (BlueprintAssetDiscovery.IsProjectAssetPath(path))
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

        private static string NormalizeAssetPath(string path)
        {
            return BlueprintAssetDiscovery.NormalizeAssetPath(path);
        }
    }
}
