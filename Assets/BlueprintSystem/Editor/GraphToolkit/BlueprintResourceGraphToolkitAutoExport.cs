using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    internal sealed class BlueprintResourceGraphToolkitAutoExport : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingGraphPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _scheduled;
        private static bool _exporting;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (_exporting || importedAssets == null)
            {
                return;
            }

            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];
                if (BlueprintResourceGraphToolkitBridge.IsResourceGraphAssetPath(path))
                {
                    PendingGraphPaths.Add(path);
                }
            }

            if (PendingGraphPaths.Count > 0)
            {
                Schedule();
            }
        }

        private static void Schedule()
        {
            if (_scheduled)
            {
                return;
            }

            _scheduled = true;
            EditorApplication.delayCall += Flush;
        }

        private static void Flush()
        {
            _scheduled = false;
            List<string> paths = new List<string>(PendingGraphPaths);
            PendingGraphPaths.Clear();
            _exporting = true;
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    Export(paths[i]);
                }
            }
            finally
            {
                _exporting = false;
            }
        }

        private static void Export(string graphPath)
        {
            if (string.IsNullOrEmpty(graphPath) || !File.Exists(graphPath))
            {
                return;
            }

            string outputPath = BlueprintResourceGraphToolkitBridge.GetDefaultResourceBlueprintJsonPath(graphPath);
            if (File.Exists(outputPath) && File.GetLastWriteTimeUtc(graphPath) <= File.GetLastWriteTimeUtc(outputPath))
            {
                Debug.Log("[Blueprint Resource] Skipped auto-export for older resource graph cache: " + graphPath);
                return;
            }

            try
            {
                string exported = BlueprintResourceGraphToolkitBridge.ExportGraphAtPath(graphPath, null);
                Debug.Log("[Blueprint Resource] Auto-exported resource graph to JSON: " + exported);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Blueprint Resource] Failed to auto-export resource graph '" + graphPath + "': " + exception.Message);
            }
        }
    }
}
