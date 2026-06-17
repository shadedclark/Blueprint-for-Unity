using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    public sealed class BlueprintRuntimeRegistryGenerationReport
    {
        public string CatalogId;
        public string AssetPath;
        public string GeneratedHash;
        public int UserStructCount;
        public int DataTableCount;
        public readonly List<string> Warnings = new List<string>();
    }

    public static class BlueprintRuntimeRegistryAssetManagerUtility
    {
        public const string PackageCatalogId = "BlueprintSystem";
        public const int PackageCatalogPriority = 0;
        public const string ProjectCatalogId = "Project";
        public const int ProjectCatalogPriority = 100;
        public const string CatalogResourceFolder = "BlueprintRuntimeRegistries";
        public const string ProjectOverlayAssetPath = "Assets/Resources/BlueprintRuntimeRegistries/Project.asset";

        private const string PackageCatalogAssetName = "BlueprintSystem.asset";
        private static bool _scheduledPackageSync;
        private static bool _scheduledProjectSync;

        public static string PackageCatalogAssetPath
        {
            get
            {
                string packageRoot = GetPreferredPackageRoot();
                return string.IsNullOrEmpty(packageRoot)
                    ? "Assets/BlueprintSystem/Resources/" + CatalogResourceFolder + "/" + PackageCatalogAssetName
                    : packageRoot + "/Resources/" + CatalogResourceFolder + "/" + PackageCatalogAssetName;
            }
        }

        [MenuItem("Tools/Blueprint System/Runtime Registry/Sync Package Registry")]
        public static void SyncPackageRegistryMenu()
        {
            LogReport(SyncPackageRegistry(true));
        }

        [MenuItem("Tools/Blueprint System/Runtime Registry/Sync Project Overlay")]
        public static void SyncProjectOverlayMenu()
        {
            LogReport(SyncProjectOverlay(true));
        }

        [MenuItem("Tools/Blueprint System/Runtime Registry/Sync All")]
        public static void SyncAllMenu()
        {
            SyncAll(true);
        }

        public static void SyncAll(bool log)
        {
            BlueprintRuntimeRegistryGenerationReport packageReport = SyncPackageRegistry(false);
            BlueprintRuntimeRegistryGenerationReport projectReport = SyncProjectOverlay(false);
            if (log)
            {
                LogReport(packageReport);
                LogReport(projectReport);
            }
        }

        public static BlueprintRuntimeRegistryGenerationReport SyncPackageRegistry(bool log)
        {
            string packageRoot = GetPreferredPackageRoot();
            BlueprintRuntimeRegistryGenerationReport report = SyncRegistry(
                PackageCatalogId,
                PackageCatalogPriority,
                PackageCatalogAssetPath,
                string.IsNullOrEmpty(packageRoot) ? new string[0] : new[] { packageRoot },
                IsPackageRuntimeSourcePath);
            if (log)
            {
                LogReport(report);
            }

            return report;
        }

        public static BlueprintRuntimeRegistryGenerationReport SyncProjectOverlay(bool log)
        {
            BlueprintRuntimeRegistryGenerationReport report = SyncRegistry(
                ProjectCatalogId,
                ProjectCatalogPriority,
                ProjectOverlayAssetPath,
                new[] { BlueprintAssetDiscovery.ProjectAssetRoot },
                IsProjectRuntimeSourcePath);
            if (log)
            {
                LogReport(report);
            }

            return report;
        }

        public static void SchedulePackageRegistrySync()
        {
            if (_scheduledPackageSync)
            {
                return;
            }

            _scheduledPackageSync = true;
            EditorApplication.delayCall += delegate
            {
                _scheduledPackageSync = false;
                SyncPackageRegistry(false);
            };
        }

        public static void ScheduleProjectOverlaySync()
        {
            if (_scheduledProjectSync)
            {
                return;
            }

            _scheduledProjectSync = true;
            EditorApplication.delayCall += delegate
            {
                _scheduledProjectSync = false;
                SyncProjectOverlay(false);
            };
        }

        public static bool IsPackageRuntimeSourcePath(string path)
        {
            path = BlueprintAssetDiscovery.NormalizeAssetPath(path);
            string[] packageRoots = BlueprintEditorAssetDiscovery.GetBlueprintPackageRoots();
            for (int i = 0; i < packageRoots.Length; i++)
            {
                if (BlueprintEditorAssetDiscovery.IsPathInRoot(path, packageRoots[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsProjectRuntimeSourcePath(string path)
        {
            path = BlueprintAssetDiscovery.NormalizeAssetPath(path);
            return BlueprintAssetDiscovery.IsProjectAssetPath(path) && !IsPackageRuntimeSourcePath(path);
        }

        private static BlueprintRuntimeRegistryGenerationReport SyncRegistry(
            string catalogId,
            int priority,
            string assetPath,
            string[] searchRoots,
            Predicate<string> includePath)
        {
            BlueprintRuntimeRegistryGenerationReport report = new BlueprintRuntimeRegistryGenerationReport
            {
                CatalogId = catalogId,
                AssetPath = assetPath
            };

            Dictionary<string, BlueprintRuntimeUserStructRegistryEntry> userStructs =
                new Dictionary<string, BlueprintRuntimeUserStructRegistryEntry>(StringComparer.Ordinal);
            Dictionary<string, BlueprintRuntimeDataTableRegistryEntry> dataTables =
                new Dictionary<string, BlueprintRuntimeDataTableRegistryEntry>(StringComparer.Ordinal);

            AddUserStructJsonEntries(userStructs, report, searchRoots, includePath);
            AddUserStructAssetEntries(userStructs, report, searchRoots, includePath);
            AddDataTableJsonEntries(dataTables, report, searchRoots, includePath);
            AddDataTableAssetEntries(dataTables, report, searchRoots, includePath);

            List<BlueprintRuntimeUserStructRegistryEntry> userStructEntries =
                new List<BlueprintRuntimeUserStructRegistryEntry>(userStructs.Values);
            List<BlueprintRuntimeDataTableRegistryEntry> dataTableEntries =
                new List<BlueprintRuntimeDataTableRegistryEntry>(dataTables.Values);
            userStructEntries.Sort(CompareUserStructEntries);
            dataTableEntries.Sort(CompareDataTableEntries);

            string generatedHash = ComputeHash(userStructEntries, dataTableEntries);
            EnsureFolder(System.IO.Path.GetDirectoryName(assetPath));

            BlueprintRuntimeRegistryAsset registry =
                AssetDatabase.LoadAssetAtPath<BlueprintRuntimeRegistryAsset>(assetPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<BlueprintRuntimeRegistryAsset>();
                AssetDatabase.CreateAsset(registry, assetPath);
            }

            registry.SetGeneratedData(catalogId, priority, generatedHash, userStructEntries, dataTableEntries);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);

            BlueprintRuntimeRegistry.Refresh();
            BlueprintUserStructRegistry.Refresh();
            BlueprintDataTableRegistry.Refresh();

            report.GeneratedHash = generatedHash;
            report.UserStructCount = userStructEntries.Count;
            report.DataTableCount = dataTableEntries.Count;
            return report;
        }

        private static void AddUserStructJsonEntries(
            Dictionary<string, BlueprintRuntimeUserStructRegistryEntry> entries,
            BlueprintRuntimeRegistryGenerationReport report,
            string[] searchRoots,
            Predicate<string> includePath)
        {
            List<string> paths = BlueprintEditorAssetDiscovery.FindAssetPaths(
                "t:TextAsset",
                BlueprintUserStructRegistry.StructAssetExtension,
                searchRoots);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (!ShouldInclude(path, includePath))
                {
                    continue;
                }

                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                try
                {
                    BlueprintUserStructDefinition definition = BlueprintUserStructDefinition.FromJson(asset.text);
                    if (definition == null || string.IsNullOrEmpty(definition.TypeId))
                    {
                        continue;
                    }

                    entries[definition.TypeId] = new BlueprintRuntimeUserStructRegistryEntry
                    {
                        TypeId = definition.TypeId,
                        SourcePath = NormalizePath(path),
                        SourceGuid = AssetDatabase.AssetPathToGUID(path),
                        DefinitionJson = asset.text
                    };
                }
                catch (Exception exception)
                {
                    report.Warnings.Add(path + ": " + exception.Message);
                }
            }
        }

        private static void AddUserStructAssetEntries(
            Dictionary<string, BlueprintRuntimeUserStructRegistryEntry> entries,
            BlueprintRuntimeRegistryGenerationReport report,
            string[] searchRoots,
            Predicate<string> includePath)
        {
            List<string> paths = BlueprintEditorAssetDiscovery.FindAssetPaths("t:BlueprintUserStructAsset", null, searchRoots);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (!ShouldInclude(path, includePath))
                {
                    continue;
                }

                BlueprintUserStructAsset asset = AssetDatabase.LoadAssetAtPath<BlueprintUserStructAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                try
                {
                    BlueprintUserStructDefinition definition = asset.ToDefinition();
                    if (definition == null || string.IsNullOrEmpty(definition.TypeId))
                    {
                        continue;
                    }

                    entries[definition.TypeId] = new BlueprintRuntimeUserStructRegistryEntry
                    {
                        TypeId = definition.TypeId,
                        SourcePath = NormalizePath(path),
                        SourceGuid = AssetDatabase.AssetPathToGUID(path),
                        DefinitionJson = asset.ToJson()
                    };
                }
                catch (Exception exception)
                {
                    report.Warnings.Add(path + ": " + exception.Message);
                }
            }
        }

        private static void AddDataTableJsonEntries(
            Dictionary<string, BlueprintRuntimeDataTableRegistryEntry> entries,
            BlueprintRuntimeRegistryGenerationReport report,
            string[] searchRoots,
            Predicate<string> includePath)
        {
            List<string> paths = BlueprintEditorAssetDiscovery.FindAssetPaths(
                "t:TextAsset",
                BlueprintDataTableRegistry.DataTableAssetExtension,
                searchRoots);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (!ShouldInclude(path, includePath))
                {
                    continue;
                }

                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                try
                {
                    BlueprintDataTableDefinition definition = BlueprintDataTableDefinition.FromJson(asset.text);
                    if (definition == null || string.IsNullOrEmpty(definition.TableId))
                    {
                        continue;
                    }

                    entries[definition.TableId] = new BlueprintRuntimeDataTableRegistryEntry
                    {
                        TableId = definition.TableId,
                        SourcePath = NormalizePath(path),
                        PathAliases = GetEquivalentPackagePaths(path),
                        SourceGuid = AssetDatabase.AssetPathToGUID(path),
                        DefinitionJson = asset.text
                    };
                }
                catch (Exception exception)
                {
                    report.Warnings.Add(path + ": " + exception.Message);
                }
            }
        }

        private static void AddDataTableAssetEntries(
            Dictionary<string, BlueprintRuntimeDataTableRegistryEntry> entries,
            BlueprintRuntimeRegistryGenerationReport report,
            string[] searchRoots,
            Predicate<string> includePath)
        {
            List<string> paths = BlueprintEditorAssetDiscovery.FindAssetPaths("t:BlueprintDataTableAsset", null, searchRoots);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (!ShouldInclude(path, includePath))
                {
                    continue;
                }

                BlueprintDataTableAsset asset = AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                try
                {
                    BlueprintDataTableDefinition definition = asset.ToDefinition();
                    if (definition == null || string.IsNullOrEmpty(definition.TableId))
                    {
                        continue;
                    }

                    string tableJsonPath = BlueprintDataTableRegistry.GetJsonPathForAssetPath(path);
                    entries[definition.TableId] = new BlueprintRuntimeDataTableRegistryEntry
                    {
                        TableId = definition.TableId,
                        SourcePath = NormalizePath(tableJsonPath),
                        AssetPathAlias = NormalizePath(path),
                        PathAliases = CombineAliases(GetEquivalentPackagePaths(tableJsonPath), GetEquivalentPackagePaths(path)),
                        SourceGuid = AssetDatabase.AssetPathToGUID(path),
                        DefinitionJson = asset.ToJson()
                    };
                }
                catch (Exception exception)
                {
                    report.Warnings.Add(path + ": " + exception.Message);
                }
            }
        }

        private static string[] GetEquivalentPackagePaths(string path)
        {
            path = NormalizePath(path);
            List<string> aliases = new List<string>();
            AddAlias(aliases, path);

            string embeddedRoot = BlueprintAssetDiscovery.EmbeddedAssetRoot;
            string packageRoot = BlueprintAssetDiscovery.PackageAssetRoot;
            if (BlueprintEditorAssetDiscovery.IsPathInRoot(path, embeddedRoot))
            {
                AddAlias(aliases, packageRoot + path.Substring(embeddedRoot.Length));
            }
            else if (BlueprintEditorAssetDiscovery.IsPathInRoot(path, packageRoot))
            {
                AddAlias(aliases, embeddedRoot + path.Substring(packageRoot.Length));
            }

            return aliases.ToArray();
        }

        private static string[] CombineAliases(string[] first, string[] second)
        {
            List<string> aliases = new List<string>();
            if (first != null)
            {
                for (int i = 0; i < first.Length; i++)
                {
                    AddAlias(aliases, first[i]);
                }
            }

            if (second != null)
            {
                for (int i = 0; i < second.Length; i++)
                {
                    AddAlias(aliases, second[i]);
                }
            }

            return aliases.ToArray();
        }

        private static void AddAlias(List<string> aliases, string path)
        {
            path = NormalizePath(path);
            if (!string.IsNullOrEmpty(path) && !aliases.Contains(path))
            {
                aliases.Add(path);
            }
        }

        private static string GetPreferredPackageRoot()
        {
            if (AssetDatabase.IsValidFolder(BlueprintAssetDiscovery.EmbeddedAssetRoot))
            {
                return BlueprintAssetDiscovery.EmbeddedAssetRoot;
            }

            return AssetDatabase.IsValidFolder(BlueprintAssetDiscovery.PackageAssetRoot)
                ? BlueprintAssetDiscovery.PackageAssetRoot
                : null;
        }

        private static bool ShouldInclude(string path, Predicate<string> includePath)
        {
            path = NormalizePath(path);
            return !string.IsNullOrEmpty(path) && (includePath == null || includePath(path));
        }

        private static int CompareUserStructEntries(
            BlueprintRuntimeUserStructRegistryEntry left,
            BlueprintRuntimeUserStructRegistryEntry right)
        {
            return string.CompareOrdinal(left == null ? null : left.TypeId, right == null ? null : right.TypeId);
        }

        private static int CompareDataTableEntries(
            BlueprintRuntimeDataTableRegistryEntry left,
            BlueprintRuntimeDataTableRegistryEntry right)
        {
            return string.CompareOrdinal(left == null ? null : left.TableId, right == null ? null : right.TableId);
        }

        private static string ComputeHash(
            List<BlueprintRuntimeUserStructRegistryEntry> userStructEntries,
            List<BlueprintRuntimeDataTableRegistryEntry> dataTableEntries)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < userStructEntries.Count; i++)
            {
                BlueprintRuntimeUserStructRegistryEntry entry = userStructEntries[i];
                builder.Append("S|").Append(entry.TypeId).Append('|')
                    .Append(entry.SourcePath).Append('|')
                    .Append(entry.SourceGuid).Append('|')
                    .Append(entry.DefinitionJson).Append('\n');
            }

            for (int i = 0; i < dataTableEntries.Count; i++)
            {
                BlueprintRuntimeDataTableRegistryEntry entry = dataTableEntries[i];
                builder.Append("T|").Append(entry.TableId).Append('|')
                    .Append(entry.SourcePath).Append('|')
                    .Append(entry.AssetPathAlias).Append('|')
                    .Append(entry.SourceGuid).Append('|')
                    .Append(entry.DefinitionJson).Append('|');
                if (entry.PathAliases != null)
                {
                    for (int a = 0; a < entry.PathAliases.Length; a++)
                    {
                        builder.Append(entry.PathAliases[a]).Append(';');
                    }
                }

                builder.Append('\n');
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                StringBuilder hex = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    hex.Append(hash[i].ToString("x2"));
                }

                return hex.ToString();
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            folderPath = NormalizePath(folderPath);
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts.Length == 0 ? string.Empty : parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string NormalizePath(string path)
        {
            return BlueprintAssetDiscovery.NormalizeAssetPath(path);
        }

        private static void LogReport(BlueprintRuntimeRegistryGenerationReport report)
        {
            if (report == null)
            {
                return;
            }

            BlueprintLog.Log("[Blueprint Runtime Registry] Synced " + report.CatalogId + " to " + report.AssetPath +
                             " (" + report.UserStructCount + " struct(s), " + report.DataTableCount + " table(s)).");
            for (int i = 0; i < report.Warnings.Count; i++)
            {
                BlueprintLog.Warning("[Blueprint Runtime Registry] " + report.Warnings[i]);
            }
        }
    }

    internal sealed class BlueprintRuntimeRegistryAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            bool packageChanged = false;
            bool projectChanged = false;
            CheckPaths(importedAssets, false, ref packageChanged, ref projectChanged);
            CheckPaths(deletedAssets, true, ref packageChanged, ref projectChanged);
            CheckPaths(movedAssets, false, ref packageChanged, ref projectChanged);
            CheckPaths(movedFromAssetPaths, true, ref packageChanged, ref projectChanged);

            if (packageChanged)
            {
                BlueprintRuntimeRegistryAssetManagerUtility.SchedulePackageRegistrySync();
            }

            if (projectChanged)
            {
                BlueprintRuntimeRegistryAssetManagerUtility.ScheduleProjectOverlaySync();
            }
        }

        private static void CheckPaths(string[] paths, bool deletedOrMovedFrom, ref bool packageChanged, ref bool projectChanged)
        {
            if (paths == null)
            {
                return;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                string path = BlueprintAssetDiscovery.NormalizeAssetPath(paths[i]);
                if (!IsRuntimeRegistrySourcePath(path, deletedOrMovedFrom))
                {
                    continue;
                }

                if (BlueprintRuntimeRegistryAssetManagerUtility.IsPackageRuntimeSourcePath(path))
                {
                    packageChanged = true;
                }
                else if (BlueprintRuntimeRegistryAssetManagerUtility.IsProjectRuntimeSourcePath(path))
                {
                    projectChanged = true;
                }
            }
        }

        private static bool IsRuntimeRegistrySourcePath(string path, bool deletedOrMovedFrom)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (path.EndsWith(BlueprintUserStructRegistry.StructAssetExtension, StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(BlueprintDataTableRegistry.DataTableAssetExtension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return deletedOrMovedFrom ||
                   IsScriptableRegistryAsset(path, typeof(BlueprintUserStructAsset)) ||
                   IsScriptableRegistryAsset(path, typeof(BlueprintDataTableAsset));
        }

        private static bool IsScriptableRegistryAsset(string path, Type expectedType)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
            return assetType != null && expectedType.IsAssignableFrom(assetType);
        }
    }

    internal sealed class BlueprintRuntimeRegistryBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder
        {
            get { return 5; }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            BlueprintRuntimeRegistryAssetManagerUtility.SyncProjectOverlay(false);
        }
    }
}
