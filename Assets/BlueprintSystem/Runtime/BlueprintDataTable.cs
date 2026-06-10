using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BlueprintSystem
{
    [Serializable]
    public sealed class BlueprintDataTableRowDefinition
    {
        public string RowName;
        public object Value;
    }

    [Serializable]
    public sealed class BlueprintDataTableDefinition
    {
        public string SchemaVersion;
        public string TableId;
        public string RowStructTypeId;
        public string SourcePath;
        public readonly List<BlueprintDataTableRowDefinition> Rows = new List<BlueprintDataTableRowDefinition>();

        public static BlueprintDataTableDefinition FromJson(string json)
        {
            return FromDictionary(BlueprintJson.DeserializeObject(json));
        }

        internal static BlueprintDataTableDefinition FromDictionary(Dictionary<string, object> data)
        {
            BlueprintDataTableDefinition definition = new BlueprintDataTableDefinition();
            definition.SchemaVersion = BlueprintSourceMapper.GetString(data, "schemaVersion");
            definition.TableId = BlueprintSourceMapper.GetString(data, "tableId");
            definition.RowStructTypeId = BlueprintSourceMapper.GetString(data, "rowStructTypeId");

            foreach (Dictionary<string, object> item in BlueprintSourceMapper.GetObjectArray(data, "rows"))
            {
                BlueprintDataTableRowDefinition row = new BlueprintDataTableRowDefinition();
                row.RowName = BlueprintSourceMapper.GetString(item, "rowName");
                item.TryGetValue("value", out row.Value);
                definition.Rows.Add(row);
            }

            return definition;
        }

        public bool TryGetRow(string rowName, out BlueprintDataTableRowDefinition row)
        {
            row = null;
            if (string.IsNullOrEmpty(rowName))
            {
                return false;
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                BlueprintDataTableRowDefinition candidate = Rows[i];
                if (candidate != null && candidate.RowName == rowName)
                {
                    row = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    public static class BlueprintDataTableRegistry
    {
        public const string DataTableAssetExtension = ".bpdatatable.json";
        public const string DefaultAssetRoot = "Assets/BlueprintSystem/Specs/Tables";

        private static readonly object CacheLock = new object();
        private static Dictionary<string, BlueprintDataTableDefinition> definitionsByPath;
        private static Dictionary<string, BlueprintDataTableDefinition> definitionsByTableId;

        public static bool TryGetByPath(string path, out BlueprintDataTableDefinition definition)
        {
            definition = null;
            EnsureLoaded();
            string normalizedPath = NormalizeAssetPath(path);
            return !string.IsNullOrEmpty(normalizedPath) && definitionsByPath.TryGetValue(normalizedPath, out definition);
        }

        public static bool TryGetByTableId(string tableId, out BlueprintDataTableDefinition definition)
        {
            definition = null;
            EnsureLoaded();
            return !string.IsNullOrEmpty(tableId) && definitionsByTableId.TryGetValue(tableId, out definition);
        }

        public static void Refresh()
        {
            lock (CacheLock)
            {
                definitionsByPath = null;
                definitionsByTableId = null;
            }
        }

        internal static string NormalizeAssetPath(string path)
        {
            return BlueprintAssetDiscovery.NormalizeAssetPath(path);
        }

        public static string GetJsonPathForAssetPath(string assetPath)
        {
            return BlueprintAssetDiscovery.ChangeAssetPathExtension(assetPath, DataTableAssetExtension);
        }

        private static void EnsureLoaded()
        {
            if (definitionsByPath != null && definitionsByTableId != null)
            {
                return;
            }

            lock (CacheLock)
            {
                if (definitionsByPath != null && definitionsByTableId != null)
                {
                    return;
                }

                LoadDefinitions(out definitionsByPath, out definitionsByTableId);
            }
        }

        private static void LoadDefinitions(
            out Dictionary<string, BlueprintDataTableDefinition> byPath,
            out Dictionary<string, BlueprintDataTableDefinition> byTableId)
        {
            byPath = new Dictionary<string, BlueprintDataTableDefinition>(StringComparer.OrdinalIgnoreCase);
            byTableId = new Dictionary<string, BlueprintDataTableDefinition>(StringComparer.Ordinal);

#if UNITY_EDITOR
            LoadEditorJsonDefinitions(byPath, byTableId);
            LoadEditorAssetDefinitions(byPath, byTableId);
#else
            LoadRuntimeJsonDefinitions(byPath, byTableId);
#endif
        }

        private static void LoadRuntimeJsonDefinitions(
            Dictionary<string, BlueprintDataTableDefinition> byPath,
            Dictionary<string, BlueprintDataTableDefinition> byTableId)
        {
            string root = GetAbsoluteTableRoot();
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                string[] files;
                try
                {
                    files = Directory.GetFiles(root, "*" + DataTableAssetExtension, SearchOption.AllDirectories);
                }
                catch
                {
                    files = new string[0];
                }

                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        BlueprintDataTableDefinition definition = BlueprintDataTableDefinition.FromJson(File.ReadAllText(files[i]));
                        definition.SourcePath = AbsolutePathToAssetPath(files[i]);
                        AddDefinition(definition, byPath, byTableId);
                    }
                    catch
                    {
                    }
                }
            }
        }

#if UNITY_EDITOR
        private static void LoadEditorJsonDefinitions(
            Dictionary<string, BlueprintDataTableDefinition> byPath,
            Dictionary<string, BlueprintDataTableDefinition> byTableId)
        {
            List<string> paths = BlueprintAssetDiscovery.FindTextAssetPaths(DataTableAssetExtension);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                TextAsset tableJson = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (tableJson == null)
                {
                    continue;
                }

                try
                {
                    BlueprintDataTableDefinition definition = BlueprintDataTableDefinition.FromJson(tableJson.text);
                    definition.SourcePath = path;
                    AddDefinition(definition, byPath, byTableId);
                }
                catch
                {
                }
            }
        }

        private static void LoadEditorAssetDefinitions(
            Dictionary<string, BlueprintDataTableDefinition> byPath,
            Dictionary<string, BlueprintDataTableDefinition> byTableId)
        {
            List<string> paths = BlueprintAssetDiscovery.FindAssetPaths("t:BlueprintDataTableAsset");
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                BlueprintDataTableAsset asset = AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                BlueprintDataTableDefinition definition = asset.ToDefinition();
                definition.SourcePath = GetJsonPathForAssetPath(path);
                if (AddDefinition(definition, byPath, byTableId))
                {
                    byPath[NormalizeAssetPath(path)] = definition;
                }
            }
        }
#endif

        private static bool AddDefinition(
            BlueprintDataTableDefinition definition,
            Dictionary<string, BlueprintDataTableDefinition> byPath,
            Dictionary<string, BlueprintDataTableDefinition> byTableId)
        {
            if (!IsValidDefinition(definition))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(definition.SourcePath))
            {
                byPath[NormalizeAssetPath(definition.SourcePath)] = definition;
            }

            if (!string.IsNullOrEmpty(definition.TableId))
            {
                byTableId[definition.TableId] = definition;
            }

            return true;
        }

        private static bool IsValidDefinition(BlueprintDataTableDefinition definition)
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

        private static string GetAbsoluteTableRoot()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
            {
                return null;
            }

            return Path.Combine(dataPath, "BlueprintSystem/Specs/Tables");
        }

        private static string AbsolutePathToAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return null;
            }

            string normalized = NormalizeAssetPath(Path.GetFullPath(absolutePath));
            string dataPath = NormalizeAssetPath(Path.GetFullPath(Application.dataPath));
            if (normalized.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + normalized.Substring(dataPath.Length + 1);
            }

            return normalized;
        }
    }

    public static class BlueprintDataTableNodeUtility
    {
        public const string GetRowNodeTypeId = "DataTable.GetRow";
        public const string GetRowNamesNodeTypeId = "DataTable.GetRowNames";
        public const string GetAllRowsNodeTypeId = "DataTable.GetAllRows";
        public const string TablePathPropertyId = "tablePath";
        public const string TableAssetGuidPropertyId = "tableAssetGuid";
        public const string RowStructTypePropertyId = "rowStructTypeId";

        public static bool IsDataTableNode(string typeId)
        {
            return typeId == GetRowNodeTypeId ||
                   typeId == GetRowNamesNodeTypeId ||
                   typeId == GetAllRowsNodeTypeId;
        }

        public static string GetRowStructTypeId(BlueprintNodeSource node)
        {
            return node == null ? null : GetRowStructTypeId(node.Properties);
        }

        public static string GetRowStructTypeId(IDictionary<string, object> properties)
        {
            object value;
            if (properties != null && properties.TryGetValue(RowStructTypePropertyId, out value) && value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return null;
        }

        public static string GetTablePath(BlueprintNodeSource node)
        {
            return node == null ? null : GetTablePath(node.Properties);
        }

        public static string GetTablePath(IDictionary<string, object> properties)
        {
            object value;
            if (properties != null && properties.TryGetValue(TablePathPropertyId, out value) && value != null)
            {
                return BlueprintDataTableRegistry.NormalizeAssetPath(Convert.ToString(value, CultureInfo.InvariantCulture));
            }

            return null;
        }

        public static bool TryResolveDefinition(BlueprintNodeSource node, out string tablePath, out BlueprintDataTableDefinition definition)
        {
            return TryResolveDefinition(node == null ? null : node.Properties, out tablePath, out definition);
        }

        public static bool TryResolveDefinition(IDictionary<string, object> properties, out string tablePath, out BlueprintDataTableDefinition definition)
        {
#if UNITY_EDITOR
            object assetGuidValue;
            if (properties != null &&
                properties.TryGetValue(TableAssetGuidPropertyId, out assetGuidValue) &&
                assetGuidValue != null)
            {
                string assetGuid = Convert.ToString(assetGuidValue, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(assetGuid))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                    BlueprintDataTableAsset asset = string.IsNullOrEmpty(assetPath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(assetPath);
                    if (asset != null)
                    {
                        tablePath = BlueprintDataTableRegistry.GetJsonPathForAssetPath(assetPath);
                        definition = asset.ToDefinition();
                        definition.SourcePath = tablePath;
                        return !string.IsNullOrEmpty(tablePath) && definition != null;
                    }
                }
            }
#endif

            tablePath = GetTablePath(properties);
            if (string.IsNullOrEmpty(tablePath))
            {
                definition = null;
                return false;
            }

            return BlueprintDataTableRegistry.TryGetByPath(tablePath, out definition);
        }
    }

    public static class BlueprintDataTableUtility
    {
        public static bool TryGetRow(
            BlueprintDataTableDefinition definition,
            string rowName,
            out object runtimeValue,
            out bool found)
        {
            runtimeValue = null;
            found = false;
            if (!IsValidTableForRuntime(definition))
            {
                return false;
            }

            BlueprintDataTableRowDefinition row;
            if (definition.TryGetRow(rowName, out row))
            {
                found = true;
                return BlueprintUserStructUtility.TryConvertToRuntimeValue(row.Value, definition.RowStructTypeId, out runtimeValue);
            }

            return BlueprintUserStructUtility.TryCreateDefaultRuntimeValue(definition.RowStructTypeId, out runtimeValue);
        }

        public static bool TryGetAllRows(BlueprintDataTableDefinition definition, out List<object> runtimeRows)
        {
            runtimeRows = null;
            if (!IsValidTableForRuntime(definition))
            {
                return false;
            }

            List<object> rows = new List<object>();
            for (int i = 0; i < definition.Rows.Count; i++)
            {
                BlueprintDataTableRowDefinition row = definition.Rows[i];
                object runtimeValue;
                if (row == null ||
                    !BlueprintUserStructUtility.TryConvertToRuntimeValue(row.Value, definition.RowStructTypeId, out runtimeValue))
                {
                    return false;
                }

                rows.Add(runtimeValue);
            }

            runtimeRows = rows;
            return true;
        }

        public static List<object> GetRowNames(BlueprintDataTableDefinition definition)
        {
            List<object> rowNames = new List<object>();
            if (definition == null)
            {
                return rowNames;
            }

            for (int i = 0; i < definition.Rows.Count; i++)
            {
                BlueprintDataTableRowDefinition row = definition.Rows[i];
                if (row != null && !string.IsNullOrEmpty(row.RowName))
                {
                    rowNames.Add(row.RowName);
                }
            }

            return rowNames;
        }

        public static bool TryGetRowNames(BlueprintDataTableDefinition definition, out List<object> rowNames)
        {
            rowNames = null;
            if (!IsValidTableForRuntime(definition))
            {
                return false;
            }

            rowNames = GetRowNames(definition);
            return true;
        }

        private static bool IsValidTableForRuntime(BlueprintDataTableDefinition definition)
        {
            if (definition == null ||
                string.IsNullOrEmpty(definition.RowStructTypeId) ||
                !BlueprintUserStructRegistry.IsUserStructType(definition.RowStructTypeId))
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
    }
}
