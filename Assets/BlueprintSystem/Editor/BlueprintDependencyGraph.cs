using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlueprintSystem;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    /// <summary>
    /// A compilation-scoped cache. It makes parent compilation reuse components which were already
    /// compiled as part of the same explicit dependency plan.
    /// </summary>
    public sealed class BlueprintCompilationSession
    {
        private readonly Dictionary<string, BlueprintCompiledAsset> _completedBySourcePath =
            new Dictionary<string, BlueprintCompiledAsset>(StringComparer.OrdinalIgnoreCase);

        internal readonly HashSet<string> CompilationStack =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool ForceRecompile { get; set; } = true;
        public bool CompileDependencies { get; set; } = true;

        internal bool TryGetCompleted(string sourcePath, out BlueprintCompiledAsset compiledAsset)
        {
            return _completedBySourcePath.TryGetValue(
                BlueprintCompiledAssetCompiler.NormalizeAssetPath(sourcePath),
                out compiledAsset);
        }

        internal void RecordCompleted(string sourcePath, BlueprintCompiledAsset compiledAsset)
        {
            if (compiledAsset == null)
            {
                return;
            }

            _completedBySourcePath[BlueprintCompiledAssetCompiler.NormalizeAssetPath(sourcePath)] = compiledAsset;
        }
    }

    internal sealed class BlueprintDependencyNode
    {
        public string SourcePath;
        public readonly List<string> Dependencies = new List<string>();
    }

    internal sealed class BlueprintDependencyIssue
    {
        public string Code;
        public string SourcePath;
        public string ComponentName;
        public string TargetPath;
        public bool Required;
        public string Message;
    }

    internal sealed class BlueprintDependencyGraphResult
    {
        public readonly Dictionary<string, BlueprintDependencyNode> Nodes =
            new Dictionary<string, BlueprintDependencyNode>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> RequestedPaths = new List<string>();
        public readonly List<BlueprintDependencyIssue> Issues = new List<BlueprintDependencyIssue>();
        public readonly List<string[]> Cycles = new List<string[]>();
        public readonly List<string> OrderedPaths = new List<string>();
        public bool AssetLimitExceeded;

        public bool HasRequiredFailures
        {
            get
            {
                return AssetLimitExceeded ||
                       Issues.Any(issue => issue.Required &&
                           (issue.Code == "BP_COMPILE_SOURCE_NOT_FOUND" ||
                            issue.Code == "BP_COMPILE_SOURCE_PARSE_FAILED" ||
                            issue.Code == "BP_COMPILE_DEPENDENCY_MISSING"));
            }
        }
    }

    internal static class BlueprintDependencyGraphBuilder
    {
        internal static BlueprintDependencyGraphResult Build(
            IEnumerable<string> requestedPaths,
            bool includeDependencies,
            bool includeDependents,
            int maxAssets)
        {
            var result = new BlueprintDependencyGraphResult();
            int limit = Math.Max(1, maxAssets);
            foreach (string requestedPath in (requestedPaths ?? Enumerable.Empty<string>())
                         .Select(BlueprintCompiledAssetCompiler.NormalizeAssetPath)
                         .Where(path => !string.IsNullOrWhiteSpace(path))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                result.RequestedPaths.Add(requestedPath);
                AddNode(result, requestedPath, includeDependencies, limit);
            }

            if (includeDependents && !result.AssetLimitExceeded)
            {
                Dictionary<string, List<string>> reverseIndex = BuildReverseIndex();
                var pending = new Queue<string>(result.Nodes.Keys.OrderBy(path => path, StringComparer.Ordinal));
                var visited = new HashSet<string>(result.Nodes.Keys, StringComparer.OrdinalIgnoreCase);
                while (pending.Count > 0 && !result.AssetLimitExceeded)
                {
                    string path = pending.Dequeue();
                    List<string> dependents;
                    if (!reverseIndex.TryGetValue(path, out dependents))
                    {
                        continue;
                    }

                    foreach (string dependent in dependents.OrderBy(item => item, StringComparer.Ordinal))
                    {
                        if (!visited.Add(dependent))
                        {
                            continue;
                        }

                        AddNode(result, dependent, includeDependencies, limit);
                        pending.Enqueue(dependent);
                    }
                }
            }

            if (!result.AssetLimitExceeded)
            {
                BuildStableTopologicalOrder(result);
            }

            return result;
        }

        private static bool AddNode(
            BlueprintDependencyGraphResult result,
            string sourcePath,
            bool includeDependencies,
            int maxAssets)
        {
            sourcePath = BlueprintCompiledAssetCompiler.NormalizeAssetPath(sourcePath);
            if (result.Nodes.ContainsKey(sourcePath))
            {
                return true;
            }

            if (result.Nodes.Count >= maxAssets)
            {
                result.AssetLimitExceeded = true;
                return false;
            }

            TextAsset sourceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
            if (sourceAsset == null || !File.Exists(sourcePath))
            {
                result.Issues.Add(new BlueprintDependencyIssue
                {
                    Code = "BP_COMPILE_SOURCE_NOT_FOUND",
                    SourcePath = sourcePath,
                    Required = true,
                    Message = "Blueprint source was not found."
                });
                return false;
            }

            BlueprintSource source;
            try
            {
                source = BlueprintSource.FromJson(sourceAsset.text);
            }
            catch (Exception exception)
            {
                result.Issues.Add(new BlueprintDependencyIssue
                {
                    Code = "BP_COMPILE_SOURCE_PARSE_FAILED",
                    SourcePath = sourcePath,
                    Required = true,
                    Message = exception.Message
                });
                return false;
            }

            var node = new BlueprintDependencyNode { SourcePath = sourcePath };
            result.Nodes[sourcePath] = node;
            for (int i = 0; i < source.Components.Count; i++)
            {
                BlueprintComponentDeclaration component = source.Components[i];
                if (component == null || string.IsNullOrWhiteSpace(component.Blueprint))
                {
                    if (component != null && component.Required)
                    {
                        result.Issues.Add(new BlueprintDependencyIssue
                        {
                            Code = "BP_COMPILE_DEPENDENCY_MISSING",
                            SourcePath = sourcePath,
                            ComponentName = component.Name ?? string.Empty,
                            TargetPath = string.Empty,
                            Required = true,
                            Message = "Required component has no Blueprint path."
                        });
                    }

                    continue;
                }

                string dependencyPath = BlueprintCompiledAssetCompiler.ResolveComponentAssetPath(sourcePath, component.Blueprint);
                if (dependencyPath.EndsWith(".compiled.asset", StringComparison.OrdinalIgnoreCase))
                {
                    if (AssetDatabase.LoadAssetAtPath<BlueprintCompiledAsset>(dependencyPath) == null && component.Required)
                    {
                        result.Issues.Add(new BlueprintDependencyIssue
                        {
                            Code = "BP_COMPILE_DEPENDENCY_MISSING",
                            SourcePath = sourcePath,
                            ComponentName = component.Name ?? string.Empty,
                            TargetPath = dependencyPath,
                            Required = true,
                            Message = "Required compiled component asset was not found."
                        });
                    }

                    continue;
                }

                if (!dependencyPath.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase) ||
                    AssetDatabase.LoadAssetAtPath<TextAsset>(dependencyPath) == null)
                {
                    result.Issues.Add(new BlueprintDependencyIssue
                    {
                        Code = "BP_COMPILE_DEPENDENCY_MISSING",
                        SourcePath = sourcePath,
                        ComponentName = component.Name ?? string.Empty,
                        TargetPath = dependencyPath ?? string.Empty,
                        Required = component.Required,
                        Message = component.Required
                            ? "Required component Blueprint source was not found."
                            : "Optional component Blueprint source was not found."
                    });
                    continue;
                }

                node.Dependencies.Add(dependencyPath);
                if (includeDependencies)
                {
                    AddNode(result, dependencyPath, true, maxAssets);
                }
            }

            node.Dependencies.Sort(StringComparer.Ordinal);
            return true;
        }

        private static Dictionary<string, List<string>> BuildReverseIndex()
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string sourcePath in FindProjectBlueprintSources())
            {
                TextAsset sourceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
                if (sourceAsset == null)
                {
                    continue;
                }

                try
                {
                    BlueprintSource source = BlueprintSource.FromJson(sourceAsset.text);
                    for (int i = 0; i < source.Components.Count; i++)
                    {
                        BlueprintComponentDeclaration component = source.Components[i];
                        if (component == null || string.IsNullOrWhiteSpace(component.Blueprint))
                        {
                            continue;
                        }

                        string dependencyPath = BlueprintCompiledAssetCompiler.ResolveComponentAssetPath(sourcePath, component.Blueprint);
                        if (string.IsNullOrEmpty(dependencyPath))
                        {
                            continue;
                        }

                        List<string> owners;
                        if (!result.TryGetValue(dependencyPath, out owners))
                        {
                            owners = new List<string>();
                            result[dependencyPath] = owners;
                        }

                        owners.Add(sourcePath);
                    }
                }
                catch
                {
                    // A malformed unrelated asset must not make the requested graph unusable.
                }
            }

            return result;
        }

        private static IEnumerable<string> FindProjectBlueprintSources()
        {
            return AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal);
        }

        private static void BuildStableTopologicalOrder(BlueprintDependencyGraphResult result)
        {
            var colors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var stack = new List<string>();
            foreach (string sourcePath in result.Nodes.Keys.OrderBy(path => path, StringComparer.Ordinal))
            {
                Visit(sourcePath, result, colors, stack);
            }
        }

        private static void Visit(
            string sourcePath,
            BlueprintDependencyGraphResult result,
            Dictionary<string, int> colors,
            List<string> stack)
        {
            int color;
            if (colors.TryGetValue(sourcePath, out color))
            {
                if (color == 1)
                {
                    int start = stack.FindIndex(item => string.Equals(item, sourcePath, StringComparison.OrdinalIgnoreCase));
                    if (start >= 0)
                    {
                        var cycle = stack.Skip(start).Concat(new[] { sourcePath }).ToArray();
                        if (!result.Cycles.Any(existing => existing.SequenceEqual(cycle, StringComparer.OrdinalIgnoreCase)))
                        {
                            result.Cycles.Add(cycle);
                        }
                    }
                }

                return;
            }

            colors[sourcePath] = 1;
            stack.Add(sourcePath);
            BlueprintDependencyNode node = result.Nodes[sourcePath];
            foreach (string dependency in node.Dependencies)
            {
                if (result.Nodes.ContainsKey(dependency))
                {
                    Visit(dependency, result, colors, stack);
                }
            }

            stack.RemoveAt(stack.Count - 1);
            colors[sourcePath] = 2;
            if (!result.OrderedPaths.Contains(sourcePath, StringComparer.OrdinalIgnoreCase))
            {
                result.OrderedPaths.Add(sourcePath);
            }
        }
    }
}
