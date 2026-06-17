using System;
using System.IO;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    public static class BlueprintResourceGraphToolkitBridge
    {
        private const string ImportMenu = "Tools/Blueprint System/Resource Graph Toolkit/Import Selected Resource Blueprint JSON";
        private const string ExportMenu = "Tools/Blueprint System/Resource Graph Toolkit/Export Selected Resource Graph To JSON";

        [MenuItem(ImportMenu)]
        public static void ImportSelectedResourceBlueprintJson()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            string graphPath = ImportResourceBlueprintAtPath(path, true);
            BlueprintLog.Log("[Blueprint Resource] Imported resource graph: " + graphPath);
        }

        [MenuItem(ImportMenu, true)]
        private static bool CanImportSelectedResourceBlueprintJson()
        {
            return IsResourceBlueprintJsonPath(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [MenuItem(ExportMenu)]
        public static void ExportSelectedResourceGraph()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            string outputPath = ExportGraphAtPath(path, null);
            BlueprintLog.Log("[Blueprint Resource] Exported resource blueprint JSON: " + outputPath);
        }

        [MenuItem(ExportMenu, true)]
        private static bool CanExportSelectedResourceGraph()
        {
            return IsResourceGraphAssetPath(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [OnOpenAsset(0)]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            return OpenAssetAtPath(BlueprintEditorWindow.GetAssetPathFromOpenAssetId(instanceId));
        }

        public static bool OpenAssetAtPath(string assetPath)
        {
            if (!IsResourceBlueprintJsonPath(assetPath))
            {
                return false;
            }

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null)
            {
                return false;
            }

            try
            {
                ImportResourceBlueprintAtPath(assetPath, true);
                return true;
            }
            catch (Exception ex)
            {
                BlueprintLog.Error("[Blueprint Resource] Failed to open visual graph for '" + assetPath + "': " + ex.Message, asset);
                return false;
            }
        }

        public static string ImportResourceBlueprintAtPath(string resourceBlueprintPath, bool openAsset)
        {
            return ImportResourceBlueprintAtPath(resourceBlueprintPath, GetDefaultGraphPath(resourceBlueprintPath), openAsset);
        }

        public static string ImportResourceBlueprintAtPath(string resourceBlueprintPath, string graphPath, bool openAsset)
        {
            if (!IsResourceBlueprintJsonPath(resourceBlueprintPath))
            {
                throw new ArgumentException("Expected a " + BlueprintResourceBlueprintSource.AssetExtension + " asset path.", "resourceBlueprintPath");
            }

            BlueprintResourceBlueprintSource source = BlueprintResourceBlueprintSource.FromJson(File.ReadAllText(resourceBlueprintPath));
            BlueprintResourceAssetManagerUtility.RegisterResourceType(source.ResourceType);
            string directory = Path.GetDirectoryName(graphPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            BlueprintResourceVisualGraph graph = GraphDatabase.CreateGraph<BlueprintResourceVisualGraph>(graphPath);
            ApplySourceToGraph(source, resourceBlueprintPath, graph);
            BlueprintResourceGraphToolkitBlackboardSync.SyncGraphFieldsToBlackboard(graph);
            BlueprintGraphToolkitReflection.MarkDirty(graph);
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.ImportAsset(graphPath);

            if (openAsset && !Application.isBatchMode)
            {
                UnityEngine.Object graphAsset = AssetDatabase.LoadMainAssetAtPath(graphPath);
                if (graphAsset != null)
                {
                    AssetDatabase.OpenAsset(graphAsset);
                }
            }

            return graphPath;
        }

        public static string ExportGraphAtPath(string graphPath, string outputResourceBlueprintPath)
        {
            if (!IsResourceGraphAssetPath(graphPath))
            {
                throw new ArgumentException("Expected a ." + BlueprintResourceVisualGraph.AssetExtension + " asset path.", "graphPath");
            }

            BlueprintResourceVisualGraph graph = GraphDatabase.LoadGraph<BlueprintResourceVisualGraph>(graphPath);
            if (graph == null)
            {
                throw new InvalidOperationException("Unable to load resource graph at " + graphPath);
            }

            if (string.IsNullOrEmpty(outputResourceBlueprintPath))
            {
                outputResourceBlueprintPath = string.IsNullOrEmpty(graph.SourceResourceBlueprintAssetPath)
                    ? GetDefaultResourceBlueprintJsonPath(graphPath)
                    : graph.SourceResourceBlueprintAssetPath;
            }

            if (BlueprintResourceGraphToolkitBlackboardSync.SyncBlackboardToGraphFields(graph))
            {
                GraphDatabase.SaveGraphIfDirty(graph);
            }

            BlueprintResourceBlueprintSource source = ToSource(graph);
            BlueprintResourceAssetManagerUtility.RegisterResourceType(source.ResourceType);
            File.WriteAllText(outputResourceBlueprintPath, source.ToJson());
            AssetDatabase.ImportAsset(outputResourceBlueprintPath);
            BlueprintResourceAssetManagerUtility.SyncAll(false);
            return outputResourceBlueprintPath;
        }

        public static string GetDefaultGraphPath(string resourceBlueprintPath)
        {
            if (resourceBlueprintPath.EndsWith(BlueprintResourceBlueprintSource.AssetExtension, StringComparison.OrdinalIgnoreCase))
            {
                return resourceBlueprintPath.Substring(
                    0,
                    resourceBlueprintPath.Length - BlueprintResourceBlueprintSource.AssetExtension.Length) +
                    "." + BlueprintResourceVisualGraph.AssetExtension;
            }

            return Path.ChangeExtension(resourceBlueprintPath, "." + BlueprintResourceVisualGraph.AssetExtension);
        }

        public static string GetDefaultResourceBlueprintJsonPath(string graphPath)
        {
            if (graphPath.EndsWith("." + BlueprintResourceVisualGraph.AssetExtension, StringComparison.OrdinalIgnoreCase))
            {
                return graphPath.Substring(0, graphPath.Length - BlueprintResourceVisualGraph.AssetExtension.Length - 1) +
                       BlueprintResourceBlueprintSource.AssetExtension;
            }

            return Path.ChangeExtension(graphPath, BlueprintResourceBlueprintSource.AssetExtension);
        }

        public static bool IsResourceBlueprintJsonPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.EndsWith(BlueprintResourceBlueprintSource.AssetExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsResourceGraphAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.EndsWith("." + BlueprintResourceVisualGraph.AssetExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplySourceToGraph(BlueprintResourceBlueprintSource source, string path, BlueprintResourceVisualGraph graph)
        {
            graph.SourceResourceBlueprintAssetPath = path;
            graph.SchemaVersion = source.SchemaVersion;
            graph.ResourceType = source.ResourceType;
            graph.ResourceName = source.ResourceName;
            graph.DisplayName = source.DisplayName;
            graph.Description = source.Description;
            graph.Tags = new System.Collections.Generic.List<string>(source.Tags);
            graph.MainAsset = source.MainAsset ?? new BlueprintResourceAssetReference();
            graph.Dependencies = new System.Collections.Generic.List<BlueprintResourceDependency>(source.Dependencies);
            graph.PreloadGroups = new System.Collections.Generic.List<string>(source.PreloadGroups);
            graph.Priority = source.Priority;
            graph.MemoryBudgetMb = source.MemoryBudgetMb;
            graph.RemoteCatalog = source.RemoteCatalog;
            graph.ContentVersion = source.ContentVersion;
            graph.Metadata = new System.Collections.Generic.List<BlueprintResourceMetadataField>(source.Metadata);
        }

        private static BlueprintResourceBlueprintSource ToSource(BlueprintResourceVisualGraph graph)
        {
            BlueprintResourceBlueprintSource source = new BlueprintResourceBlueprintSource();
            source.SchemaVersion = graph.SchemaVersion;
            source.ResourceType = graph.ResourceType;
            source.ResourceName = graph.ResourceName;
            source.DisplayName = graph.DisplayName;
            source.Description = graph.Description;
            source.Tags.AddRange(graph.Tags ?? new System.Collections.Generic.List<string>());
            source.MainAsset = graph.MainAsset ?? new BlueprintResourceAssetReference();
            source.Dependencies.AddRange(graph.Dependencies ?? new System.Collections.Generic.List<BlueprintResourceDependency>());
            source.PreloadGroups.AddRange(graph.PreloadGroups ?? new System.Collections.Generic.List<string>());
            source.Priority = graph.Priority;
            source.MemoryBudgetMb = graph.MemoryBudgetMb;
            source.RemoteCatalog = graph.RemoteCatalog;
            source.ContentVersion = graph.ContentVersion;
            source.Metadata.AddRange(graph.Metadata ?? new System.Collections.Generic.List<BlueprintResourceMetadataField>());
            return source;
        }
    }
}
