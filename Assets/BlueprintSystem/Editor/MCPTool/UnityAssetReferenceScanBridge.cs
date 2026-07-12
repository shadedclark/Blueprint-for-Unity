using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Unity.AI.MCP.Editor.ToolRegistry;
using UnityEditor;

namespace BlueprintLangGraph.Editor
{
    public static class UnityAssetReferenceScanBridge
    {
        [McpTool(
            "unity_asset_reference_scan",
            "Scan Unity and BlueprintSystem assets for reverse references to paths, GUIDs, and identifiers.",
            EnabledByDefault = true)]
        public static object AssetReferenceScan(UnityAssetReferenceScanParams parameters)
        {
            parameters = parameters ?? new UnityAssetReferenceScanParams();
            if (parameters.Targets == null || parameters.Targets.Count == 0)
            {
                return BlueprintMcpCommon.Failure("ASSET_SCAN_TARGET_NOT_FOUND", "At least one scan target is required.");
            }

            List<string> roots = NormalizeRoots(parameters.SearchRoots, parameters.IncludePackages, out string invalidRoot);
            if (!string.IsNullOrEmpty(invalidRoot))
            {
                return BlueprintMcpCommon.Failure(
                    "ASSET_SCAN_ROOT_NOT_FOUND",
                    "searchRoots must exist inside Assets or enabled Packages.",
                    new { searchRoot = invalidRoot });
            }

            List<ScanTarget> targets;
            string targetError;
            if (!TryResolveTargets(parameters.Targets, out targets, out targetError))
            {
                return BlueprintMcpCommon.Failure("ASSET_SCAN_UNSUPPORTED_TARGET_KIND", targetError);
            }

            int maxMatches = Math.Max(1, parameters.MaxMatches);
            var records = new List<ReferenceRecord>();
            var dedupe = new HashSet<string>(StringComparer.Ordinal);
            var parseErrors = new List<object>();
            int scannedAssets = 0;
            bool truncated = false;

            List<string> assetPaths = FindAssetPaths(roots);
            if (parameters.IncludeUnityDependencies)
            {
                foreach (string sourcePath in assetPaths)
                {
                    scannedAssets++;
                    if (ShouldSkipTargetSource(sourcePath, targets))
                    {
                        continue;
                    }

                    string[] dependencies;
                    try
                    {
                        dependencies = AssetDatabase.GetDependencies(sourcePath, true);
                    }
                    catch (Exception exception)
                    {
                        parseErrors.Add(new { path = sourcePath, error = exception.Message, scanner = "unityDependencies" });
                        continue;
                    }

                    foreach (ScanTarget target in targets)
                    {
                        if (!target.HasAssetPathTarget || !dependencies.Any(target.MatchesAssetPath))
                        {
                            continue;
                        }

                        AddRecord(records, dedupe, new ReferenceRecord
                        {
                            Target = target.DisplayValue,
                            SourceAssetPath = sourcePath,
                            SourceKind = "unity",
                            ReferenceKind = "hardUnityReference",
                            Location = string.Empty,
                            MatchedValue = target.DisplayValue,
                            Blocking = true,
                            DependencyChain = parameters.IncludeDependencyChains
                                ? new[] { sourcePath, target.DisplayValue }
                                : Array.Empty<string>()
                        }, maxMatches, ref truncated);
                        if (truncated)
                        {
                            break;
                        }
                    }

                    if (truncated)
                    {
                        break;
                    }
                }
            }

            if (!truncated)
            {
                foreach (string sourcePath in FindScannableTextPaths(roots, parameters))
                {
                    if (ShouldSkipTargetSource(sourcePath, targets))
                    {
                        continue;
                    }

                    scannedAssets++;
                    string sourceKind = GetSourceKind(sourcePath);
                    try
                    {
                        string text = File.ReadAllText(BlueprintMcpCommon.ToProjectFilePath(sourcePath));
                        if (sourceKind == "text")
                        {
                            ScanPlainText(sourcePath, text, targets, parameters, records, dedupe, maxMatches, ref truncated);
                        }
                        else
                        {
                            JToken document = JToken.Parse(text);
                            ScanJson(sourcePath, sourceKind, document, string.Empty, targets, parameters, records, dedupe, maxMatches, ref truncated);
                        }
                    }
                    catch (Exception exception)
                    {
                        parseErrors.Add(new { path = sourcePath, error = exception.Message, scanner = sourceKind });
                    }

                    if (truncated)
                    {
                        break;
                    }
                }
            }

            int blockingReferences = records.Count(record => record.Blocking);
            int nonBlockingReferences = records.Count - blockingReferences;
            bool incomplete = truncated || parseErrors.Count > 0;
            bool safeToDelete = !incomplete && blockingReferences == 0;
            var summary = new
            {
                scannedAssets,
                blockingReferences,
                nonBlockingReferences,
                truncated,
                safeToDelete
            };
            object[] references = records.Select(record => record.ToPayload()).ToArray();
            object[] targetPayloads = targets.Select(target => target.ToPayload()).ToArray();

            if (truncated)
            {
                return BlueprintMcpCommon.Failure(
                    "ASSET_SCAN_RESULT_LIMIT",
                    "Asset reference scan reached maxMatches; safeToDelete is false because the result is incomplete.",
                    new { targets = targetPayloads, summary, parseErrors = parseErrors.ToArray() },
                    references);
            }

            if (parseErrors.Count > 0)
            {
                return BlueprintMcpCommon.Failure(
                    "ASSET_SCAN_INCOMPLETE",
                    "Asset reference scan could not parse every selected asset; safeToDelete is false.",
                    new { targets = targetPayloads, summary, parseErrors = parseErrors.ToArray() },
                    references);
            }

            return BlueprintMcpCommon.Success("Asset reference scan complete.", new
            {
                targets = targetPayloads,
                summary,
                references
            });
        }

        private static List<string> NormalizeRoots(IEnumerable<string> roots, bool includePackages, out string invalidRoot)
        {
            invalidRoot = string.Empty;
            List<string> normalized = (roots ?? new[] { "Assets" })
                .Select(BlueprintMcpCommon.NormalizeAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (normalized.Count == 0)
            {
                normalized.Add("Assets");
            }

            if (includePackages && !normalized.Contains("Packages", StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add("Packages");
            }

            foreach (string root in normalized)
            {
                if (!BlueprintMcpCommon.IsProjectAssetPath(root, includePackages) ||
                    !Directory.Exists(BlueprintMcpCommon.ToProjectFilePath(root)))
                {
                    invalidRoot = root;
                    return normalized;
                }
            }

            return normalized;
        }

        private static bool TryResolveTargets(
            IEnumerable<UnityAssetReferenceScanTargetSpec> specs,
            out List<ScanTarget> targets,
            out string error)
        {
            targets = new List<ScanTarget>();
            error = string.Empty;
            foreach (UnityAssetReferenceScanTargetSpec spec in specs)
            {
                if (spec == null || string.IsNullOrWhiteSpace(spec.Kind) || string.IsNullOrWhiteSpace(spec.Value))
                {
                    error = "Every target requires kind and value.";
                    return false;
                }

                string kind = spec.Kind.Trim();
                var target = new ScanTarget
                {
                    Kind = kind.ToLowerInvariant(),
                    DisplayValue = spec.Value.Trim(),
                    Recursive = spec.Recursive
                };
                if (string.Equals(kind, "assetPath", StringComparison.OrdinalIgnoreCase))
                {
                    string path = BlueprintMcpCommon.NormalizeAssetPath(spec.Value);
                    if (!BlueprintMcpCommon.IsProjectAssetPath(path, true))
                    {
                        error = "assetPath targets must be inside Assets or Packages.";
                        return false;
                    }

                    target.DisplayValue = path;
                    target.AssetPaths.Add(path);
                    if (Directory.Exists(BlueprintMcpCommon.ToProjectFilePath(path)) && spec.Recursive)
                    {
                        foreach (string childPath in FindAssetPaths(new[] { path }))
                        {
                            target.AssetPaths.Add(childPath);
                        }
                    }

                    foreach (string assetPath in target.AssetPaths)
                    {
                        string guid = AssetDatabase.AssetPathToGUID(assetPath);
                        if (!string.IsNullOrEmpty(guid))
                        {
                            target.Guids.Add(guid);
                        }
                    }
                }
                else if (string.Equals(kind, "assetGuid", StringComparison.OrdinalIgnoreCase))
                {
                    string path = AssetDatabase.GUIDToAssetPath(spec.Value.Trim());
                    if (string.IsNullOrEmpty(path))
                    {
                        error = "assetGuid target was not found: " + spec.Value;
                        return false;
                    }

                    target.Guids.Add(spec.Value.Trim());
                    target.AssetPaths.Add(path);
                    target.DisplayValue = spec.Value.Trim();
                }
                else if (string.Equals(kind, "identifier", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(kind, "blueprintTypeId", StringComparison.OrdinalIgnoreCase))
                {
                    // Scalar matching is handled by the semantic JSON scanner.
                }
                else if (string.Equals(kind, "datatableRow", StringComparison.OrdinalIgnoreCase))
                {
                    int separator = spec.Value.LastIndexOf('#');
                    if (separator <= 0 || separator == spec.Value.Length - 1)
                    {
                        error = "datatableRow value must use Assets/path.bpdatatable.json#RowName.";
                        return false;
                    }

                    target.TablePath = BlueprintMcpCommon.NormalizeAssetPath(spec.Value.Substring(0, separator));
                    target.RowName = spec.Value.Substring(separator + 1);
                    target.AssetPaths.Add(target.TablePath);
                    target.DisplayValue = target.TablePath + "#" + target.RowName;
                }
                else
                {
                    error = "Unsupported target kind: " + spec.Kind;
                    return false;
                }

                targets.Add(target);
            }

            return true;
        }

        private static List<string> FindAssetPaths(IEnumerable<string> roots)
        {
            return AssetDatabase.FindAssets(string.Empty, roots.ToArray())
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<string> FindScannableTextPaths(
            IEnumerable<string> roots,
            UnityAssetReferenceScanParams parameters)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                string rootPath = BlueprintMcpCommon.ToProjectFilePath(root);
                foreach (string filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
                {
                    string assetPath = BlueprintMcpCommon.NormalizeAssetPath(filePath);
                    if (ShouldScanTextPath(assetPath, parameters))
                    {
                        result.Add(assetPath);
                    }
                }
            }

            return result.OrderBy(path => path, StringComparer.Ordinal);
        }

        private static bool ShouldScanTextPath(string path, UnityAssetReferenceScanParams parameters)
        {
            if (path.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase))
            {
                return parameters.IncludeBlueprintJson;
            }

            if (path.EndsWith(".btree.json", StringComparison.OrdinalIgnoreCase))
            {
                return parameters.IncludeBehaviorTreeJson;
            }

            if (path.EndsWith(".bpdatatable.json", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".bpstruct.json", StringComparison.OrdinalIgnoreCase))
            {
                return parameters.IncludeDataTables;
            }

            if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                return parameters.IncludeMetaFiles;
            }

            return parameters.IncludeTextAssets &&
                   (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        }

        private static string GetSourceKind(string sourcePath)
        {
            if (sourcePath.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase))
            {
                return "blueprint";
            }

            if (sourcePath.EndsWith(".btree.json", StringComparison.OrdinalIgnoreCase))
            {
                return "behaviorTree";
            }

            if (sourcePath.EndsWith(".bpdatatable.json", StringComparison.OrdinalIgnoreCase))
            {
                return "dataTable";
            }

            if (sourcePath.EndsWith(".bpstruct.json", StringComparison.OrdinalIgnoreCase))
            {
                return "struct";
            }

            return "text";
        }

        private static void ScanPlainText(
            string sourcePath,
            string text,
            IEnumerable<ScanTarget> targets,
            UnityAssetReferenceScanParams parameters,
            List<ReferenceRecord> records,
            HashSet<string> dedupe,
            int maxMatches,
            ref bool truncated)
        {
            foreach (ScanTarget target in targets)
            {
                if (!TextMatchesTarget(text, target, parameters))
                {
                    continue;
                }

                AddRecord(records, dedupe, new ReferenceRecord
                {
                    Target = target.DisplayValue,
                    SourceAssetPath = sourcePath,
                    SourceKind = "text",
                    ReferenceKind = "textReference",
                    Location = string.Empty,
                    MatchedValue = target.DisplayValue,
                    Blocking = false,
                    DependencyChain = Array.Empty<string>()
                }, maxMatches, ref truncated);
                if (truncated)
                {
                    return;
                }
            }
        }

        private static void ScanJson(
            string sourcePath,
            string sourceKind,
            JToken token,
            string pointer,
            IEnumerable<ScanTarget> targets,
            UnityAssetReferenceScanParams parameters,
            List<ReferenceRecord> records,
            HashSet<string> dedupe,
            int maxMatches,
            ref bool truncated)
        {
            if (truncated || token == null)
            {
                return;
            }

            JObject obj = token as JObject;
            if (obj != null)
            {
                foreach (JProperty property in obj.Properties())
                {
                    string childPointer = pointer + "/" + EscapePointer(property.Name);
                    ScanJson(sourcePath, sourceKind, property.Value, childPointer, targets, parameters, records, dedupe, maxMatches, ref truncated);
                    if (truncated)
                    {
                        return;
                    }
                }

                return;
            }

            JArray array = token as JArray;
            if (array != null)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    ScanJson(sourcePath, sourceKind, array[i], pointer + "/" + i, targets, parameters, records, dedupe, maxMatches, ref truncated);
                    if (truncated)
                    {
                        return;
                    }
                }

                return;
            }

            JValue value = token as JValue;
            if (value == null || value.Type != JTokenType.String)
            {
                return;
            }

            string text = value.Value<string>() ?? string.Empty;
            string propertyName = LastPointerSegment(pointer);
            foreach (ScanTarget target in targets)
            {
                ReferenceRecord record;
                if (!TryCreateJsonReference(sourcePath, sourceKind, pointer, propertyName, text, target, parameters, out record))
                {
                    continue;
                }

                AddRecord(records, dedupe, record, maxMatches, ref truncated);
                if (truncated)
                {
                    return;
                }
            }
        }

        private static bool TryCreateJsonReference(
            string sourcePath,
            string sourceKind,
            string pointer,
            string propertyName,
            string value,
            ScanTarget target,
            UnityAssetReferenceScanParams parameters,
            out ReferenceRecord record)
        {
            record = null;
            bool semantic = string.Equals(parameters.MatchMode, "semantic", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(parameters.MatchMode, "both", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(parameters.MatchMode);
            bool exactText = string.Equals(parameters.MatchMode, "exactText", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(parameters.MatchMode, "both", StringComparison.OrdinalIgnoreCase);
            if (!semantic && !exactText)
            {
                return false;
            }

            string referenceKind = GetJsonReferenceKind(sourceKind, pointer, propertyName, value);
            string resolvedPathValue = ResolveAssetReferencePath(sourcePath, value);
            bool pathMatch = target.MatchesPathValue(value) ||
                             target.MatchesPathValue(resolvedPathValue) ||
                             target.Guids.Contains(value);
            bool identifierMatch = target.MatchesIdentifier(value, parameters.CaseSensitiveIdentifiers) &&
                                   (exactText || IsKnownIdentifierField(propertyName));
            bool rowMatch = target.MatchesRow(value, sourcePath, propertyName);
            bool textPathMatch = exactText && pathMatch;
            if (!(semantic && pathMatch) && !textPathMatch && !identifierMatch && !rowMatch)
            {
                return false;
            }

            if (rowMatch)
            {
                referenceKind = "dataTableReference";
            }
            else if (identifierMatch && referenceKind == "softPathReference")
            {
                referenceKind = "identifierReference";
            }

            record = new ReferenceRecord
            {
                Target = target.DisplayValue,
                SourceAssetPath = sourcePath,
                SourceKind = sourceKind,
                ReferenceKind = referenceKind,
                Location = pointer,
                MatchedValue = value,
                Blocking = referenceKind != "identifierReference" && referenceKind != "textReference",
                DependencyChain = Array.Empty<string>()
            };
            return true;
        }

        private static string GetJsonReferenceKind(string sourceKind, string pointer, string propertyName, string value)
        {
            if (sourceKind == "blueprint" && pointer.Contains("/components/") && propertyName == "blueprint")
            {
                return "blueprintComponentReference";
            }

            if (sourceKind == "behaviorTree" &&
                (propertyName == "subtree" || propertyName == "subtreePath" || propertyName == "blueprint"))
            {
                return "behaviorTreeSubtreeReference";
            }

            if (propertyName == "tablePath" || propertyName == "dataTable" ||
                propertyName == "rowStructTypeId" || propertyName == "structTypeId")
            {
                return "dataTableReference";
            }

            if (propertyName == "target" || propertyName == "blueprint" ||
                propertyName == "blueprintPath" || propertyName == "resourcePath")
            {
                return "blueprintRuntimeReference";
            }

            return "softPathReference";
        }

        private static string ResolveAssetReferencePath(string sourcePath, string value)
        {
            string normalized = BlueprintMcpCommon.NormalizeAssetPath(value);
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(normalized))
            {
                return normalized;
            }

            string directory = Path.GetDirectoryName(sourcePath);
            return BlueprintMcpCommon.NormalizeAssetPath(
                string.IsNullOrEmpty(directory) ? normalized : directory + "/" + normalized);
        }

        private static bool IsKnownIdentifierField(string propertyName)
        {
            return propertyName == "id" || propertyName == "name" || propertyName == "typeId" ||
                   propertyName == "structTypeId" || propertyName == "rowStructTypeId" ||
                   propertyName == "eventName" || propertyName == "rowName" || propertyName == "tableId";
        }

        private static bool TextMatchesTarget(string text, ScanTarget target, UnityAssetReferenceScanParams parameters)
        {
            StringComparison comparison = parameters.CaseSensitiveIdentifiers
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            if (target.HasAssetPathTarget)
            {
                return target.AssetPaths.Any(path => text.IndexOf(path, comparison) >= 0) ||
                       target.Guids.Any(guid => text.IndexOf(guid, comparison) >= 0);
            }

            return text.IndexOf(target.DisplayValue, comparison) >= 0;
        }

        private static bool ShouldSkipTargetSource(string sourcePath, IEnumerable<ScanTarget> targets)
        {
            return targets.Any(target =>
                (target.Kind == "assetpath" || target.Kind == "assetguid") &&
                target.AssetPaths.Contains(sourcePath));
        }

        private static void AddRecord(
            List<ReferenceRecord> records,
            HashSet<string> dedupe,
            ReferenceRecord record,
            int maxMatches,
            ref bool truncated)
        {
            string key = record.Target + "|" + record.SourceAssetPath + "|" + record.ReferenceKind + "|" + record.Location + "|" + record.MatchedValue;
            if (!dedupe.Add(key))
            {
                return;
            }

            if (records.Count >= maxMatches)
            {
                truncated = true;
                return;
            }

            records.Add(record);
        }

        private static string EscapePointer(string segment)
        {
            return (segment ?? string.Empty).Replace("~", "~0").Replace("/", "~1");
        }

        private static string LastPointerSegment(string pointer)
        {
            int index = (pointer ?? string.Empty).LastIndexOf('/');
            return index < 0 ? pointer ?? string.Empty : pointer.Substring(index + 1).Replace("~1", "/").Replace("~0", "~");
        }

        private sealed class ScanTarget
        {
            public string Kind;
            public string DisplayValue;
            public bool Recursive;
            public string TablePath;
            public string RowName;
            public readonly HashSet<string> AssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> Guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public bool HasAssetPathTarget
            {
                get { return AssetPaths.Count > 0 || Guids.Count > 0; }
            }

            public bool MatchesAssetPath(string assetPath)
            {
                return AssetPaths.Contains(assetPath) ||
                       (Recursive && AssetPaths.Any(path => assetPath.StartsWith(path.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)));
            }

            public bool MatchesPathValue(string value)
            {
                string normalized = BlueprintMcpCommon.NormalizeAssetPath(value);
                return MatchesAssetPath(normalized) || Guids.Contains(value ?? string.Empty);
            }

            public bool MatchesIdentifier(string value, bool caseSensitive)
            {
                if (Kind != "identifier" && Kind != "blueprinttypeid")
                {
                    return false;
                }

                return string.Equals(
                    value ?? string.Empty,
                    DisplayValue ?? string.Empty,
                    caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            }

            public bool MatchesRow(string value, string sourcePath, string propertyName)
            {
                return Kind == "datatablerow" &&
                       string.Equals(propertyName, "rowName", StringComparison.Ordinal) &&
                       string.Equals(value, RowName, StringComparison.Ordinal) &&
                       string.Equals(sourcePath, TablePath, StringComparison.OrdinalIgnoreCase);
            }

            public object ToPayload()
            {
                return new
                {
                    kind = Kind,
                    value = DisplayValue,
                    recursive = Recursive,
                    resolvedAssetPaths = AssetPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                    guids = Guids.OrderBy(guid => guid, StringComparer.Ordinal).ToArray()
                };
            }
        }

        private sealed class ReferenceRecord
        {
            public string Target;
            public string SourceAssetPath;
            public string SourceKind;
            public string ReferenceKind;
            public string Location;
            public string MatchedValue;
            public bool Blocking;
            public string[] DependencyChain;

            public object ToPayload()
            {
                return new
                {
                    target = Target,
                    sourceAssetPath = SourceAssetPath,
                    sourceKind = SourceKind,
                    referenceKind = ReferenceKind,
                    location = Location,
                    matchedValue = MatchedValue,
                    blocking = Blocking,
                    dependencyChain = DependencyChain
                };
            }
        }
    }

    public sealed class UnityAssetReferenceScanParams
    {
        [McpDescription("Paths, GUIDs, identifiers, Blueprint type ids, or DataTable rows to search for.", Required = true)]
        public List<UnityAssetReferenceScanTargetSpec> Targets { get; set; } = new List<UnityAssetReferenceScanTargetSpec>();

        [McpDescription("Assets-relative folders to scan. Defaults to Assets.")]
        public List<string> SearchRoots { get; set; } = new List<string> { "Assets" };

        [McpDescription("Scan reverse Unity serialized dependencies from Scene, Prefab, ScriptableObject, and other assets.")]
        public bool IncludeUnityDependencies { get; set; } = true;

        [McpDescription("Scan .blueprint.json semantic references.")]
        public bool IncludeBlueprintJson { get; set; } = true;

        [McpDescription("Scan .btree.json semantic references.")]
        public bool IncludeBehaviorTreeJson { get; set; } = true;

        [McpDescription("Scan .bpdatatable.json and .bpstruct.json semantic references.")]
        public bool IncludeDataTables { get; set; } = true;

        [McpDescription("Scan Markdown, text, asmdef, and other plain text references as non-blocking evidence.")]
        public bool IncludeTextAssets { get; set; }

        [McpDescription("Include .meta files in text scanning.")]
        public bool IncludeMetaFiles { get; set; }

        [McpDescription("Matching mode: semantic, exactText, or both.")]
        public string MatchMode { get; set; } = "semantic";

        [McpDescription("Use case-sensitive matching for identifier targets.")]
        public bool CaseSensitiveIdentifiers { get; set; }

        [McpDescription("Allow Packages in searchRoots and scan Packages in addition to explicit roots.")]
        public bool IncludePackages { get; set; }

        [McpDescription("Maximum reference matches before returning an incomplete result.")]
        public int MaxMatches { get; set; } = 5000;

        [McpDescription("Include direct source-to-target chains for Unity dependency matches.")]
        public bool IncludeDependencyChains { get; set; }
    }

    public sealed class UnityAssetReferenceScanTargetSpec
    {
        [McpDescription("assetPath, assetGuid, identifier, datatableRow, or blueprintTypeId.", Required = true)]
        public string Kind { get; set; }

        [McpDescription("Target value. datatableRow uses Assets/path.bpdatatable.json#RowName.", Required = true)]
        public string Value { get; set; }

        [McpDescription("Expand an assetPath directory target recursively.")]
        public bool Recursive { get; set; } = true;
    }
}
