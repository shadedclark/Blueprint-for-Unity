using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    [Serializable]
    public sealed class BlueprintRuntimeUserStructRegistryEntry
    {
        public string TypeId;
        public string SourcePath;
        public string SourceGuid;
        public string DefinitionJson;
    }

    [Serializable]
    public sealed class BlueprintRuntimeDataTableRegistryEntry
    {
        public string TableId;
        public string SourcePath;
        public string AssetPathAlias;
        public string[] PathAliases = new string[0];
        public string SourceGuid;
        public string DefinitionJson;
    }

    [CreateAssetMenu(menuName = "Blueprint System/Runtime Registry", fileName = "BlueprintRuntimeRegistry")]
    public sealed class BlueprintRuntimeRegistryAsset : ScriptableObject
    {
        [SerializeField] private string catalogId = "Project";
        [SerializeField] private int priority = 100;
        [SerializeField] private string generatedHash;
        [SerializeField] private List<BlueprintRuntimeUserStructRegistryEntry> userStructs =
            new List<BlueprintRuntimeUserStructRegistryEntry>();
        [SerializeField] private List<BlueprintRuntimeDataTableRegistryEntry> dataTables =
            new List<BlueprintRuntimeDataTableRegistryEntry>();

        public string CatalogId
        {
            get { return catalogId; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public string GeneratedHash
        {
            get { return generatedHash; }
        }

        public IReadOnlyList<BlueprintRuntimeUserStructRegistryEntry> UserStructs
        {
            get { return userStructs; }
        }

        public IReadOnlyList<BlueprintRuntimeDataTableRegistryEntry> DataTables
        {
            get { return dataTables; }
        }

        public void SetGeneratedData(
            string newCatalogId,
            int newPriority,
            string newGeneratedHash,
            IEnumerable<BlueprintRuntimeUserStructRegistryEntry> newUserStructs,
            IEnumerable<BlueprintRuntimeDataTableRegistryEntry> newDataTables)
        {
            catalogId = string.IsNullOrEmpty(newCatalogId) ? "Project" : newCatalogId;
            priority = newPriority;
            generatedHash = newGeneratedHash ?? string.Empty;
            userStructs.Clear();
            dataTables.Clear();

            if (newUserStructs != null)
            {
                userStructs.AddRange(newUserStructs);
            }

            if (newDataTables != null)
            {
                dataTables.AddRange(newDataTables);
            }
        }
    }

    public static class BlueprintRuntimeRegistry
    {
        public const string ResourceFolder = "BlueprintRuntimeRegistries";

        private static readonly object CacheLock = new object();
        private static bool loaded;
        private static Dictionary<string, BlueprintUserStructDefinition> userStructsByTypeId;
        private static Dictionary<string, BlueprintRuntimeUserStructRegistryEntry> userStructEntriesByGuid;
        private static Dictionary<string, BlueprintDataTableDefinition> dataTablesByPath;
        private static Dictionary<string, BlueprintDataTableDefinition> dataTablesByTableId;
        private static Dictionary<string, BlueprintRuntimeDataTableRegistryEntry> dataTableEntriesByGuid;

        public static void Refresh()
        {
            lock (CacheLock)
            {
                loaded = false;
                userStructsByTypeId = null;
                userStructEntriesByGuid = null;
                dataTablesByPath = null;
                dataTablesByTableId = null;
                dataTableEntriesByGuid = null;
            }
        }

        public static bool TryGetUserStructDefinition(string typeId, out BlueprintUserStructDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(typeId))
            {
                return false;
            }

            EnsureLoaded();
            return userStructsByTypeId.TryGetValue(typeId, out definition);
        }

        public static bool TryResolveUserStructGuid(
            string sourceGuid,
            out string typeId,
            out BlueprintUserStructDefinition definition)
        {
            typeId = null;
            definition = null;
            if (string.IsNullOrEmpty(sourceGuid))
            {
                return false;
            }

            EnsureLoaded();
            BlueprintRuntimeUserStructRegistryEntry entry;
            if (!userStructEntriesByGuid.TryGetValue(sourceGuid, out entry) || entry == null ||
                string.IsNullOrEmpty(entry.TypeId))
            {
                return false;
            }

            typeId = entry.TypeId;
            return userStructsByTypeId.TryGetValue(typeId, out definition);
        }

        public static string[] GetUserStructTypeIds()
        {
            EnsureLoaded();
            List<string> result = new List<string>(userStructsByTypeId.Keys);
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        public static bool TryGetDataTableByPath(string path, out BlueprintDataTableDefinition definition)
        {
            definition = null;
            path = NormalizePath(path);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            EnsureLoaded();
            return dataTablesByPath.TryGetValue(path, out definition);
        }

        public static bool TryGetDataTableByTableId(string tableId, out BlueprintDataTableDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(tableId))
            {
                return false;
            }

            EnsureLoaded();
            return dataTablesByTableId.TryGetValue(tableId, out definition);
        }

        public static bool TryResolveDataTableGuid(
            string sourceGuid,
            out string tablePath,
            out BlueprintDataTableDefinition definition)
        {
            tablePath = null;
            definition = null;
            if (string.IsNullOrEmpty(sourceGuid))
            {
                return false;
            }

            EnsureLoaded();
            BlueprintRuntimeDataTableRegistryEntry entry;
            if (!dataTableEntriesByGuid.TryGetValue(sourceGuid, out entry) || entry == null)
            {
                return false;
            }

            tablePath = NormalizePath(entry.SourcePath);
            return !string.IsNullOrEmpty(tablePath) && dataTablesByPath.TryGetValue(tablePath, out definition);
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            lock (CacheLock)
            {
                if (loaded)
                {
                    return;
                }

                LoadCatalogs();
                loaded = true;
            }
        }

        private static void LoadCatalogs()
        {
            userStructsByTypeId = new Dictionary<string, BlueprintUserStructDefinition>(StringComparer.Ordinal);
            userStructEntriesByGuid = new Dictionary<string, BlueprintRuntimeUserStructRegistryEntry>(StringComparer.Ordinal);
            dataTablesByPath = new Dictionary<string, BlueprintDataTableDefinition>(StringComparer.OrdinalIgnoreCase);
            dataTablesByTableId = new Dictionary<string, BlueprintDataTableDefinition>(StringComparer.Ordinal);
            dataTableEntriesByGuid = new Dictionary<string, BlueprintRuntimeDataTableRegistryEntry>(StringComparer.Ordinal);

            BlueprintRuntimeRegistryAsset[] catalogs = Resources.LoadAll<BlueprintRuntimeRegistryAsset>(ResourceFolder);
            Array.Sort(catalogs, CompareCatalogs);
            for (int i = 0; i < catalogs.Length; i++)
            {
                MergeCatalog(catalogs[i]);
            }
        }

        private static void MergeCatalog(BlueprintRuntimeRegistryAsset catalog)
        {
            if (catalog == null)
            {
                return;
            }

            IReadOnlyList<BlueprintRuntimeUserStructRegistryEntry> userStructs = catalog.UserStructs;
            for (int i = 0; i < userStructs.Count; i++)
            {
                BlueprintRuntimeUserStructRegistryEntry entry = userStructs[i];
                if (entry == null || string.IsNullOrEmpty(entry.TypeId) || string.IsNullOrEmpty(entry.DefinitionJson))
                {
                    continue;
                }

                try
                {
                    BlueprintUserStructDefinition definition = BlueprintUserStructDefinition.FromJson(entry.DefinitionJson);
                    if (IsValidUserStructDefinition(definition))
                    {
                        userStructsByTypeId[definition.TypeId] = definition;
                        if (!string.IsNullOrEmpty(entry.SourceGuid))
                        {
                            userStructEntriesByGuid[entry.SourceGuid] = entry;
                        }
                    }
                }
                catch
                {
                }
            }

            IReadOnlyList<BlueprintRuntimeDataTableRegistryEntry> dataTables = catalog.DataTables;
            for (int i = 0; i < dataTables.Count; i++)
            {
                BlueprintRuntimeDataTableRegistryEntry entry = dataTables[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefinitionJson))
                {
                    continue;
                }

                try
                {
                    BlueprintDataTableDefinition definition = BlueprintDataTableDefinition.FromJson(entry.DefinitionJson);
                    if (!IsValidDataTableDefinition(definition))
                    {
                        continue;
                    }

                    string sourcePath = NormalizePath(entry.SourcePath);
                    if (!string.IsNullOrEmpty(sourcePath))
                    {
                        definition.SourcePath = sourcePath;
                        dataTablesByPath[sourcePath] = definition;
                    }

                    string assetAlias = NormalizePath(entry.AssetPathAlias);
                    if (!string.IsNullOrEmpty(assetAlias))
                    {
                        dataTablesByPath[assetAlias] = definition;
                    }

                    if (entry.PathAliases != null)
                    {
                        for (int a = 0; a < entry.PathAliases.Length; a++)
                        {
                            string alias = NormalizePath(entry.PathAliases[a]);
                            if (!string.IsNullOrEmpty(alias))
                            {
                                dataTablesByPath[alias] = definition;
                            }
                        }
                    }

                    dataTablesByTableId[definition.TableId] = definition;
                    if (!string.IsNullOrEmpty(entry.SourceGuid))
                    {
                        dataTableEntriesByGuid[entry.SourceGuid] = entry;
                    }
                }
                catch
                {
                }
            }
        }

        private static int CompareCatalogs(BlueprintRuntimeRegistryAsset left, BlueprintRuntimeRegistryAsset right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int priority = left.Priority.CompareTo(right.Priority);
            return priority != 0 ? priority : string.CompareOrdinal(left.CatalogId, right.CatalogId);
        }

        private static bool IsValidUserStructDefinition(BlueprintUserStructDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.TypeId))
            {
                return false;
            }

            HashSet<string> fieldIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> fieldNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Fields.Count; i++)
            {
                BlueprintUserStructField field = definition.Fields[i];
                if (field == null || string.IsNullOrEmpty(field.Id) ||
                    string.IsNullOrEmpty(field.Name) || string.IsNullOrEmpty(field.Type))
                {
                    return false;
                }

                if (!fieldIds.Add(field.Id) || !fieldNames.Add(field.Name))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidDataTableDefinition(BlueprintDataTableDefinition definition)
        {
            if (definition == null ||
                string.IsNullOrEmpty(definition.TableId) ||
                string.IsNullOrEmpty(definition.RowStructTypeId))
            {
                return false;
            }

            HashSet<string> rowNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Rows.Count; i++)
            {
                BlueprintDataTableRowDefinition row = definition.Rows[i];
                if (row == null || string.IsNullOrEmpty(row.RowName) || !rowNames.Add(row.RowName))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizePath(string path)
        {
            return BlueprintAssetDiscovery.NormalizeAssetPath(path);
        }
    }
}
