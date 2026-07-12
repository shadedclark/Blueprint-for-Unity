using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintSystem;
using BlueprintSystem.Editor;
using Unity.AI.MCP.Editor.ToolRegistry;
using UnityEditor;

namespace BlueprintLangGraph.Editor
{
    public static class BlueprintMcpCompileBridge
    {
        [McpTool(
            "blueprint_compile_dependency_ordered",
            "Compile Blueprint sources in stable component dependency order so nested components are rebuilt before their owners.",
            EnabledByDefault = true)]
        public static object CompileDependencyOrdered(BlueprintCompileDependencyOrderedParams parameters)
        {
            parameters = parameters ?? new BlueprintCompileDependencyOrderedParams();
            List<string> sourcePaths = (parameters.SourcePaths ?? new List<string>())
                .Select(BlueprintMcpCommon.NormalizeAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (sourcePaths.Count == 0)
            {
                return BlueprintMcpCommon.Failure(
                    "BP_COMPILE_SOURCE_NOT_FOUND",
                    "At least one Blueprint source path is required.");
            }

            string invalidPath = sourcePaths.FirstOrDefault(path =>
                !BlueprintMcpCommon.IsProjectAssetPath(path, true) ||
                !path.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(invalidPath))
            {
                return BlueprintMcpCommon.Failure(
                    "BP_COMPILE_SOURCE_NOT_FOUND",
                    "Blueprint source paths must be Assets- or Packages-relative .blueprint.json files.",
                    new { sourcePath = invalidPath });
            }

            BlueprintDependencyGraphResult graph = BlueprintDependencyGraphBuilder.Build(
                sourcePaths,
                parameters.IncludeDependencies,
                parameters.IncludeDependents,
                parameters.MaxAssets <= 0 ? 1000 : parameters.MaxAssets);
            object[] plan = BuildCompilePlan(graph).ToArray();
            object[] issues = graph.Issues.Select(ToIssuePayload).ToArray();
            if (graph.AssetLimitExceeded)
            {
                return BlueprintMcpCommon.Failure(
                    "BP_COMPILE_ASSET_LIMIT",
                    "Dependency graph exceeded maxAssets.",
                    new { requestedPaths = graph.RequestedPaths, compilePlan = plan, issues },
                    plan);
            }

            if (graph.Cycles.Count > 0)
            {
                return BlueprintMcpCommon.Failure(
                    "BP_COMPILE_DEPENDENCY_CYCLE",
                    "Blueprint component dependency cycle detected.",
                    new
                    {
                        requestedPaths = graph.RequestedPaths,
                        compilePlan = plan,
                        cycles = graph.Cycles,
                        issues
                    },
                    plan);
            }

            if (graph.HasRequiredFailures)
            {
                return BlueprintMcpCommon.Failure(
                    "BP_COMPILE_DEPENDENCY_MISSING",
                    "One or more required Blueprint sources or components could not be resolved.",
                    new { requestedPaths = graph.RequestedPaths, compilePlan = plan, issues },
                    plan);
            }

            if (parameters.DryRun)
            {
                return BlueprintMcpCommon.Success("Blueprint dependency compilation plan complete.", new
                {
                    dryRun = true,
                    requestedPaths = graph.RequestedPaths,
                    compilePlan = plan,
                    results = System.Array.Empty<object>(),
                    cycles = graph.Cycles,
                    missingDependencies = issues
                });
            }

            if (parameters.ImportAssets)
            {
                foreach (string sourcePath in graph.OrderedPaths)
                {
                    AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            object[] registryReports = parameters.SyncRuntimeRegistry
                ? SyncRuntimeRegistries(parameters.RuntimeRegistryMode, parameters.Log).Select(ToRegistryPayload).ToArray()
                : System.Array.Empty<object>();
            var results = new List<object>();
            var failedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var session = new BlueprintCompilationSession
            {
                ForceRecompile = parameters.ForceRecompile,
                CompileDependencies = parameters.IncludeDependencies
            };

            BlueprintHotReloadService.ForgetPendingBlueprintPaths(graph.OrderedPaths);
            using (BlueprintHotReloadService.SuppressAutoCompile(graph.OrderedPaths))
            {
                foreach (string sourcePath in graph.OrderedPaths)
                {
                    BlueprintDependencyNode node = graph.Nodes[sourcePath];
                    string failedDependency = node.Dependencies.FirstOrDefault(failedPaths.Contains);
                    if (!string.IsNullOrEmpty(failedDependency))
                    {
                        failedPaths.Add(sourcePath);
                        results.Add(new
                        {
                            sourcePath,
                            success = false,
                            status = "skippedDependencyFailed",
                            dependency = failedDependency,
                            compiledPath = BlueprintCompiledAssetCompiler.GetCompiledAssetPath(sourcePath)
                        });
                        if (!parameters.ContinueOnError)
                        {
                            break;
                        }

                        continue;
                    }

                    bool success = BlueprintCompiledAssetCompiler.CompileBlueprintAtPath(
                        sourcePath,
                        parameters.Log,
                        out BlueprintCompiledAsset compiledAsset,
                        session);
                    bool completed = success && compiledAsset != null;
                    results.Add(new
                    {
                        sourcePath,
                        success = completed,
                        status = completed ? "compiled" : "failed",
                        compiledPath = BlueprintCompiledAssetCompiler.GetCompiledAssetPath(sourcePath),
                        compiledAssetGuid = compiledAsset == null
                            ? string.Empty
                            : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(compiledAsset))
                    });
                    if (!completed)
                    {
                        failedPaths.Add(sourcePath);
                        if (!parameters.ContinueOnError)
                        {
                            break;
                        }
                    }
                }
            }

            BlueprintHotReloadService.ForgetPendingBlueprintPaths(graph.OrderedPaths);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (failedPaths.Count > 0)
            {
                return BlueprintMcpCommon.Failure(
                    "BP_COMPILE_FAILED",
                    "One or more Blueprint compilations failed.",
                    new
                    {
                        dryRun = false,
                        requestedPaths = graph.RequestedPaths,
                        compilePlan = plan,
                        registryReports,
                        missingDependencies = issues,
                        failedPaths = failedPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray()
                    },
                    results);
            }

            return BlueprintMcpCommon.Success("Dependency-ordered Blueprint compilation complete.", new
            {
                dryRun = false,
                requestedPaths = graph.RequestedPaths,
                compilePlan = plan,
                results = results.ToArray(),
                registryReports,
                cycles = graph.Cycles,
                missingDependencies = issues
            });
        }

        private static IEnumerable<object> BuildCompilePlan(BlueprintDependencyGraphResult graph)
        {
            Dictionary<string, int> depths = BuildDepths(graph);
            for (int i = 0; i < graph.OrderedPaths.Count; i++)
            {
                string sourcePath = graph.OrderedPaths[i];
                BlueprintDependencyNode node = graph.Nodes[sourcePath];
                yield return new
                {
                    order = i,
                    sourcePath,
                    depth = depths.TryGetValue(sourcePath, out int depth) ? depth : 0,
                    dependencies = node.Dependencies.ToArray()
                };
            }
        }

        private static Dictionary<string, int> BuildDepths(BlueprintDependencyGraphResult graph)
        {
            var depths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>();
            foreach (string sourcePath in graph.RequestedPaths)
            {
                if (graph.Nodes.ContainsKey(sourcePath))
                {
                    depths[sourcePath] = 0;
                    pending.Enqueue(sourcePath);
                }
            }

            while (pending.Count > 0)
            {
                string sourcePath = pending.Dequeue();
                int depth = depths[sourcePath];
                foreach (string dependency in graph.Nodes[sourcePath].Dependencies)
                {
                    if (!graph.Nodes.ContainsKey(dependency))
                    {
                        continue;
                    }

                    int currentDepth;
                    if (!depths.TryGetValue(dependency, out currentDepth) || currentDepth < depth + 1)
                    {
                        depths[dependency] = depth + 1;
                        pending.Enqueue(dependency);
                    }
                }
            }

            return depths;
        }

        private static IEnumerable<BlueprintRuntimeRegistryGenerationReport> SyncRuntimeRegistries(string mode, bool log)
        {
            string normalized = string.IsNullOrWhiteSpace(mode) ? "project" : mode.Trim().ToLowerInvariant();
            if (normalized == "none")
            {
                yield break;
            }

            if (normalized == "package" || normalized == "all")
            {
                yield return BlueprintRuntimeRegistryAssetManagerUtility.SyncPackageRegistry(log);
            }

            if (normalized == "project" || normalized == "all" ||
                (normalized != "package" && normalized != "all"))
            {
                yield return BlueprintRuntimeRegistryAssetManagerUtility.SyncProjectOverlay(log);
            }
        }

        private static object ToRegistryPayload(BlueprintRuntimeRegistryGenerationReport report)
        {
            return new
            {
                catalogId = report.CatalogId,
                assetPath = report.AssetPath,
                generatedHash = report.GeneratedHash,
                userStructCount = report.UserStructCount,
                dataTableCount = report.DataTableCount,
                warnings = report.Warnings.ToArray()
            };
        }

        private static object ToIssuePayload(BlueprintDependencyIssue issue)
        {
            return new
            {
                code = issue.Code,
                sourcePath = issue.SourcePath,
                componentName = issue.ComponentName,
                targetPath = issue.TargetPath,
                required = issue.Required,
                message = issue.Message
            };
        }
    }

    public sealed class BlueprintCompileDependencyOrderedParams
    {
        [McpDescription("Root or nested Assets-relative .blueprint.json source paths.", Required = true)]
        public List<string> SourcePaths { get; set; } = new List<string>();

        [McpDescription("Recursively compile declared Component Blueprint dependencies before their owners.")]
        public bool IncludeDependencies { get; set; } = true;

        [McpDescription("Also compile project Blueprint owners which depend on the requested sources.")]
        public bool IncludeDependents { get; set; }

        [McpDescription("Import source assets before building the dependency graph and compiling.")]
        public bool ImportAssets { get; set; } = true;

        [McpDescription("Synchronize Blueprint Struct and DataTable runtime registries before compilation.")]
        public bool SyncRuntimeRegistry { get; set; } = true;

        [McpDescription("Runtime registry scope: project, package, all, or none.")]
        public string RuntimeRegistryMode { get; set; } = "project";

        [McpDescription("Return the stable compile plan without importing, registry synchronization, or writing compiled assets.")]
        public bool DryRun { get; set; }

        [McpDescription("Continue compiling independent graph branches after a source fails.")]
        public bool ContinueOnError { get; set; }

        [McpDescription("Rebuild current compiled assets instead of reusing matching source and manifest hashes.")]
        public bool ForceRecompile { get; set; } = true;

        [McpDescription("Record compiler diagnostics in the Unity Console.")]
        public bool Log { get; set; } = true;

        [McpDescription("Maximum Blueprint source assets allowed in the dependency graph.")]
        public int MaxAssets { get; set; } = 1000;
    }
}
