using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    internal sealed class BlueprintGraphToolkitAutoExport : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingGraphPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _flushScheduled;
        private static bool _exporting;
        private static int _suppressDepth;

        internal static IDisposable SuppressAutoExport()
        {
            _suppressDepth++;
            return new SuppressScope();
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (_exporting || _suppressDepth > 0 || importedAssets == null)
            {
                return;
            }

            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];
                if (BlueprintGraphToolkitBridge.IsBlueprintGraphAssetPath(path))
                {
                    PendingGraphPaths.Add(path);
                }
            }

            if (PendingGraphPaths.Count > 0)
            {
                ScheduleFlush();
            }
        }

        private static void ScheduleFlush()
        {
            if (_flushScheduled)
            {
                return;
            }

            _flushScheduled = true;
            EditorApplication.delayCall += FlushPendingExports;
        }

        private static void FlushPendingExports()
        {
            _flushScheduled = false;
            if (PendingGraphPaths.Count == 0)
            {
                return;
            }

            List<string> paths = new List<string>(PendingGraphPaths);
            PendingGraphPaths.Clear();

            _exporting = true;
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    ExportGraph(paths[i]);
                }
            }
            finally
            {
                _exporting = false;
            }
        }

        private static void ExportGraph(string graphPath)
        {
            if (string.IsNullOrEmpty(graphPath) || !File.Exists(graphPath))
            {
                return;
            }

            string outputPath = BlueprintGraphToolkitBridge.GetDefaultBlueprintJsonPath(graphPath);
            if (File.Exists(outputPath) && File.GetLastWriteTimeUtc(graphPath) <= File.GetLastWriteTimeUtc(outputPath))
            {
                BlueprintLog.Log("[Blueprint] Skipped auto-export for older visual graph cache: " + graphPath);
                return;
            }

            try
            {
                string exportedPath = BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, null);
                BlueprintLog.Log("[Blueprint] Auto-exported visual graph to JSON: " + exportedPath);
                RefreshOpenGraphToolkit(graphPath);
            }
            catch (Exception exception)
            {
                BlueprintLog.Warning("[Blueprint] Failed to auto-export visual graph '" + graphPath + "': " + exception.Message);
            }
        }

        private static void RefreshOpenGraphToolkit(string graphPath)
        {
            try
            {
                using (SuppressAutoExport())
                {
                    if (BlueprintGraphToolkitBridge.RefreshOpenGraphToolkitAtPath(graphPath))
                    {
                        BlueprintLog.Log("[Blueprint] Refreshed open Graph Toolkit view: " + graphPath);
                    }
                }
            }
            catch (Exception exception)
            {
                BlueprintLog.Warning("[Blueprint] Failed to refresh Graph Toolkit view '" + graphPath + "': " + exception.Message);
            }
        }

        private sealed class SuppressScope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _suppressDepth = Math.Max(0, _suppressDepth - 1);
            }
        }
    }

}
