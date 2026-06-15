using System;
using System.IO;

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
    }
}
