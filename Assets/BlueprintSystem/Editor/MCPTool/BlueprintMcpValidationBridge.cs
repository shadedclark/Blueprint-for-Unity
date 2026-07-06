using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlueprintSystem;
using BlueprintSystem.Editor;
using Newtonsoft.Json.Linq;
using Unity.AI.MCP.Editor.ToolRegistry;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlueprintLangGraph.Editor
{
    public static class BlueprintMcpValidationBridge
    {
        [McpTool("blueprint_validate_assets", "Validate BlueprintSystem source assets with JSON parsing, optional runtime registry sync, Blueprint/Behavior Tree compile, and captured operation logs.", EnabledByDefault = true)]
        public static object ValidateAssets(ValidateBlueprintAssetsParams parameters)
        {
            parameters ??= new ValidateBlueprintAssetsParams();
            List<string> sourcePaths = NormalizeAssetPaths(parameters.SourcePaths);
            var parseResults = new List<object>();
            var compileResults = new List<object>();
            var registryReports = new List<object>();

            using var logCapture = new EditorLogCapture();
            logCapture.Start();

            if (parameters.ImportAssets)
            {
                foreach (string sourcePath in sourcePaths)
                {
                    if (AssetExists(sourcePath))
                        AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            foreach (string sourcePath in sourcePaths)
            {
                parseResults.Add(ParseJsonAsset(sourcePath, out bool parsed));
                if (!parsed)
                    continue;
            }

            if (parameters.SyncRuntimeRegistry)
            {
                foreach (BlueprintRuntimeRegistryGenerationReport report in SyncRuntimeRegistries(parameters.RuntimeRegistryMode, parameters.Log))
                    registryReports.Add(BuildRegistryReport(report));
            }

            if (parameters.Compile)
            {
                List<string> blueprintPaths = sourcePaths
                    .Where(path => path.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                BlueprintHotReloadService.ForgetPendingBlueprintPaths(blueprintPaths);

                using (BlueprintHotReloadService.SuppressAutoCompile(blueprintPaths))
                {
                    foreach (string sourcePath in sourcePaths)
                    {
                        string kind = GetSourceKind(sourcePath);
                        if (kind == "blueprint")
                        {
                            bool success = BlueprintCompiledAssetCompiler.CompileBlueprintAtPath(sourcePath, parameters.Log, out BlueprintCompiledAsset compiledAsset);
                            string compiledPath = BlueprintCompiledAssetCompiler.GetCompiledAssetPath(sourcePath);
                            compileResults.Add(new
                            {
                                sourcePath,
                                kind,
                                compiledPath,
                                success = success && compiledAsset != null,
                                compiledAssetGuid = compiledAsset == null ? "" : AssetDatabase.AssetPathToGUID(compiledPath)
                            });
                        }
                        else if (kind == "behaviorTree")
                        {
                            compileResults.Add(new
                            {
                                sourcePath,
                                kind,
                                compiledPath = "",
                                success = true,
                                skipped = true,
                                reason = "Behavior Tree compiler lives in the BlueprintSystem.BehaviorTree.Editor assembly. Use the Behavior Tree editor compile tool for .btree.json assets; this MCP validation pass still parses and contract-checks them."
                            });
                        }
                        else
                        {
                            compileResults.Add(new
                            {
                                sourcePath,
                                kind,
                                compiledPath = "",
                                success = true,
                                skipped = true,
                                reason = "Asset kind is parsed and registry-synced, not compiled by this tool."
                            });
                        }
                    }
                }

                BlueprintHotReloadService.ForgetPendingBlueprintPaths(blueprintPaths);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            object[] logs = logCapture.StopAndBuild(parameters.MaxLogEntries <= 0 ? 50 : parameters.MaxLogEntries);
            bool parseSucceeded = parseResults.All(IsSuccessPayload);
            bool compileSucceeded = compileResults.All(IsSuccessPayload);
            bool hasErrorLogs = logCapture.ErrorCount > 0 || logCapture.ExceptionCount > 0;
            bool successOverall = parseSucceeded && compileSucceeded && (!parameters.FailOnCapturedErrors || !hasErrorLogs);

            return new
            {
                success = successOverall,
                message = successOverall ? "BlueprintSystem asset validation complete." : "BlueprintSystem asset validation found issues.",
                data = new
                {
                    sourcePaths,
                    parseResults,
                    registryReports,
                    compileResults,
                    capturedLogSummary = new
                    {
                        errors = logCapture.ErrorCount,
                        exceptions = logCapture.ExceptionCount,
                        warnings = logCapture.WarningCount
                    },
                    capturedLogs = logs
                }
            };
        }

        [McpTool("blueprint_contract_check", "Check .blueprint.json and .btree.json files against declarative node, edge, variable, binding, and blackboard contract rules.", EnabledByDefault = true)]
        public static object CheckContracts(BlueprintContractCheckParams parameters)
        {
            parameters ??= new BlueprintContractCheckParams();
            var results = new List<object>();
            var allIssues = new List<ContractIssue>();

            foreach (BlueprintContractAssetSpec spec in parameters.Assets ?? new List<BlueprintContractAssetSpec>())
            {
                string path = NormalizeAssetPath(spec.Path);
                List<ContractIssue> issues = CheckSingleContract(path, spec);
                allIssues.AddRange(issues);
                results.Add(new
                {
                    path,
                    success = issues.All(issue => issue.Severity != "error"),
                    issues = issues.Select(issue => issue.ToPayload()).ToArray()
                });
            }

            bool success = allIssues.All(issue => issue.Severity != "error") &&
                           (!parameters.FailOnWarnings || allIssues.All(issue => issue.Severity != "warning"));
            return new
            {
                success,
                message = success ? "BlueprintSystem contract checks passed." : "BlueprintSystem contract checks found issues.",
                data = new
                {
                    results,
                    issues = allIssues.Select(issue => issue.ToPayload()).ToArray()
                }
            };
        }

        [McpTool("blueprint_binding_snapshot", "Inspect a prefab or loaded scene object for BlueprintRunner, UIBlueprintBinder, BehaviorTreeRunner, bindings, and missing scripts.", EnabledByDefault = true)]
        public static object BindingSnapshot(BlueprintBindingSnapshotParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            string prefabPath = NormalizeAssetPath(parameters.PrefabPath);
            string rootObjectPath = NormalizeHierarchyPath(parameters.RootObjectPath);
            GameObject root;
            bool unloadPrefab;

            if (!string.IsNullOrWhiteSpace(prefabPath))
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                unloadPrefab = true;
            }
            else
            {
                root = FindLoadedSceneObject(rootObjectPath);
                unloadPrefab = false;
                if (root == null)
                    throw new InvalidOperationException("Loaded scene object was not found: " + rootObjectPath);
            }

            try
            {
                SnapshotData snapshot = BuildSnapshot(root, parameters.IncludeInactive, parameters.IncludeHierarchy);
                return new
                {
                    success = snapshot.MissingScripts.Count == 0 && snapshot.RunnerWarnings.Count == 0,
                    message = "BlueprintSystem binding snapshot complete.",
                    data = new
                    {
                        prefabPath,
                        rootObjectPath = string.IsNullOrWhiteSpace(prefabPath) ? GetHierarchyPath(root.transform.root, root.transform) : "",
                        rootName = root.name,
                        missingScripts = snapshot.MissingScripts,
                        runnerWarnings = snapshot.RunnerWarnings,
                        blueprintRunners = snapshot.BlueprintRunners,
                        behaviorTreeRunners = snapshot.BehaviorTreeRunners,
                        hierarchy = snapshot.Hierarchy
                    }
                };
            }
            finally
            {
                if (unloadPrefab)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [McpTool("blueprint_runtime_snapshot", "Capture a generic Play Mode runtime snapshot from BlueprintRunner variables and BehaviorTreeRunner debug state on a loaded scene object.", EnabledByDefault = true)]
        public static object RuntimeSnapshot(BlueprintRuntimeSnapshotParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            string rootObjectPath = NormalizeHierarchyPath(parameters.RootObjectPath);
            GameObject root = FindLoadedSceneObject(rootObjectPath);
            if (root == null)
                throw new InvalidOperationException("Loaded scene object was not found: " + rootObjectPath);

            var blueprintSnapshots = root.GetComponentsInChildren<BlueprintRunner>(parameters.IncludeInactive)
                .Select(runner => BuildRuntimeBlueprintRunnerSnapshot(root.transform, runner, parameters.BlueprintVariableNames))
                .ToArray();
            var behaviorTreeSnapshots = root.GetComponentsInChildren<BehaviorTreeRunner>(parameters.IncludeInactive)
                .Select(runner => BuildRuntimeBehaviorTreeSnapshot(root.transform, runner, parameters.BlackboardKeys, parameters.IncludeFullBehaviorTreeSnapshot))
                .ToArray();

            return new
            {
                success = Application.isPlaying,
                message = Application.isPlaying
                    ? "BlueprintSystem runtime snapshot complete."
                    : "Unity is not in Play Mode; returned editor-time runner references without live runtime values.",
                data = new
                {
                    isPlaying = Application.isPlaying,
                    rootObjectPath = GetHierarchyPath(root.transform.root, root.transform),
                    rootName = root.name,
                    position = ToArray(root.transform.position),
                    rotation = ToArray(root.transform.rotation.eulerAngles),
                    blueprintRunners = blueprintSnapshots,
                    behaviorTreeRunners = behaviorTreeSnapshots
                }
            };
        }

        private static List<ContractIssue> CheckSingleContract(string path, BlueprintContractAssetSpec spec)
        {
            var issues = new List<ContractIssue>();
            JObject json;
            try
            {
                json = JObject.Parse(File.ReadAllText(ToProjectFilePath(path)));
            }
            catch (Exception ex)
            {
                issues.Add(ContractIssue.Error(path, "parse", "Could not parse JSON: " + ex.Message));
                return issues;
            }

            Dictionary<string, JObject> nodesById = ReadObjectsById(json["nodes"]);
            JArray edges = json["edges"] as JArray ?? new JArray();
            JArray variables = json["variables"] as JArray ?? new JArray();
            JArray bindings = json["bindings"] as JArray ?? new JArray();
            JArray components = json["components"] as JArray ?? new JArray();
            JArray blackboard = json["blackboard"] as JArray ?? new JArray();

            CheckNodeRequirements(path, "requiredNode", nodesById, spec.RequiredNodes, true, issues);
            CheckNodeRequirements(path, "forbiddenNode", nodesById, spec.ForbiddenNodes, false, issues);
            CheckNamedRequirements(path, "requiredVariable", variables, spec.RequiredVariables, true, issues);
            CheckNamedRequirements(path, "forbiddenVariable", variables, spec.ForbiddenVariables, false, issues);
            CheckNamedRequirements(path, "requiredBinding", bindings, spec.RequiredBindings, true, issues);
            CheckNamedRequirements(path, "forbiddenBinding", bindings, spec.ForbiddenBindings, false, issues);
            CheckNamedRequirements(path, "requiredComponent", components, spec.RequiredComponents, true, issues);
            CheckNamedRequirements(path, "forbiddenComponent", components, spec.ForbiddenComponents, false, issues);
            CheckNamedRequirements(path, "requiredBlackboardKey", blackboard, spec.RequiredBlackboardKeys, true, issues);
            CheckNamedRequirements(path, "forbiddenBlackboardKey", blackboard, spec.ForbiddenBlackboardKeys, false, issues);
            CheckEdgeRequirements(path, "requiredEdge", edges, spec.RequiredEdges, true, issues);
            CheckEdgeRequirements(path, "forbiddenEdge", edges, spec.ForbiddenEdges, false, issues);

            if (spec.CheckUnknownEdgeNodes)
                CheckUnknownEdgeNodes(path, nodesById, edges, issues);
            if (spec.CheckExecFanIn)
                CheckExecFanIn(path, edges, issues);

            return issues;
        }

        private static void CheckNodeRequirements(
            string path,
            string rule,
            Dictionary<string, JObject> nodesById,
            List<BlueprintNodeRequirement> requirements,
            bool mustExist,
            List<ContractIssue> issues)
        {
            foreach (BlueprintNodeRequirement requirement in requirements ?? new List<BlueprintNodeRequirement>())
            {
                JObject match = FindNode(nodesById, requirement);
                bool exists = match != null;
                if (mustExist && !exists)
                {
                    issues.Add(ContractIssue.Error(path, rule, "Missing required node " + DescribeNodeRequirement(requirement) + "."));
                }
                else if (!mustExist && exists)
                {
                    issues.Add(ContractIssue.Error(path, rule, "Forbidden node exists " + DescribeNodeRequirement(requirement) + "."));
                }
            }
        }

        private static JObject FindNode(Dictionary<string, JObject> nodesById, BlueprintNodeRequirement requirement)
        {
            IEnumerable<JObject> candidates = nodesById.Values;
            if (!string.IsNullOrWhiteSpace(requirement.Id))
                candidates = candidates.Where(node => StringEquals(ReadString(node, "id"), requirement.Id));
            if (!string.IsNullOrWhiteSpace(requirement.TypeId))
                candidates = candidates.Where(node => StringEquals(ReadString(node, "typeId"), requirement.TypeId));

            foreach (JObject candidate in candidates)
            {
                if (PropertiesMatch(candidate["properties"] as JObject, requirement.Properties))
                    return candidate;
            }

            return null;
        }

        private static bool PropertiesMatch(JObject properties, Dictionary<string, object> expected)
        {
            if (expected == null || expected.Count == 0)
                return true;
            properties ??= new JObject();
            foreach (KeyValuePair<string, object> pair in expected)
            {
                JToken actual = properties[pair.Key];
                JToken expectedToken = pair.Value == null ? JValue.CreateNull() : JToken.FromObject(pair.Value);
                if (actual == null || !JToken.DeepEquals(actual, expectedToken))
                    return false;
            }

            return true;
        }

        private static void CheckNamedRequirements(
            string path,
            string rule,
            JArray array,
            List<BlueprintNamedRequirement> requirements,
            bool mustExist,
            List<ContractIssue> issues)
        {
            foreach (BlueprintNamedRequirement requirement in requirements ?? new List<BlueprintNamedRequirement>())
            {
                bool exists = array.OfType<JObject>().Any(item => NamedRequirementMatches(item, requirement));
                if (mustExist && !exists)
                {
                    issues.Add(ContractIssue.Error(path, rule, "Missing required entry " + DescribeNamedRequirement(requirement) + "."));
                }
                else if (!mustExist && exists)
                {
                    issues.Add(ContractIssue.Error(path, rule, "Forbidden entry exists " + DescribeNamedRequirement(requirement) + "."));
                }
            }
        }

        private static bool NamedRequirementMatches(JObject item, BlueprintNamedRequirement requirement)
        {
            if (!string.IsNullOrWhiteSpace(requirement.Name) && !StringEquals(ReadString(item, "name"), requirement.Name))
                return false;
            if (!string.IsNullOrWhiteSpace(requirement.Id) && !StringEquals(ReadString(item, "id"), requirement.Id))
                return false;
            if (!string.IsNullOrWhiteSpace(requirement.Type) && !StringEquals(ReadString(item, "type"), requirement.Type))
                return false;
            return PropertiesMatch(item["properties"] as JObject, requirement.Properties);
        }

        private static void CheckEdgeRequirements(
            string path,
            string rule,
            JArray edges,
            List<BlueprintEdgeRequirement> requirements,
            bool mustExist,
            List<ContractIssue> issues)
        {
            foreach (BlueprintEdgeRequirement requirement in requirements ?? new List<BlueprintEdgeRequirement>())
            {
                string from = BuildEndpoint(requirement.From, requirement.FromNode, requirement.FromPort);
                string to = BuildEndpoint(requirement.To, requirement.ToNode, requirement.ToPort);
                bool exists = edges.OfType<JObject>()
                    .Any(edge => StringEquals(ReadString(edge, "from"), from) && StringEquals(ReadString(edge, "to"), to));
                if (mustExist && !exists)
                {
                    issues.Add(ContractIssue.Error(path, rule, "Missing required edge " + from + " -> " + to + "."));
                }
                else if (!mustExist && exists)
                {
                    issues.Add(ContractIssue.Error(path, rule, "Forbidden edge exists " + from + " -> " + to + "."));
                }
            }
        }

        private static void CheckUnknownEdgeNodes(string path, Dictionary<string, JObject> nodesById, JArray edges, List<ContractIssue> issues)
        {
            foreach (JObject edge in edges.OfType<JObject>())
            {
                string fromNode = EndpointNodeId(ReadString(edge, "from"));
                string toNode = EndpointNodeId(ReadString(edge, "to"));
                if (!string.IsNullOrWhiteSpace(fromNode) && !nodesById.ContainsKey(fromNode))
                    issues.Add(ContractIssue.Error(path, "unknownEdgeNode", "Edge references unknown source node '" + fromNode + "'."));
                if (!string.IsNullOrWhiteSpace(toNode) && !nodesById.ContainsKey(toNode))
                    issues.Add(ContractIssue.Error(path, "unknownEdgeNode", "Edge references unknown target node '" + toNode + "'."));
            }
        }

        private static void CheckExecFanIn(string path, JArray edges, List<ContractIssue> issues)
        {
            var incoming = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (JObject edge in edges.OfType<JObject>())
            {
                string to = ReadString(edge, "to");
                if (!to.EndsWith(".execIn", StringComparison.Ordinal))
                    continue;
                if (!incoming.TryGetValue(to, out List<string> sources))
                {
                    sources = new List<string>();
                    incoming[to] = sources;
                }

                sources.Add(ReadString(edge, "from"));
            }

            foreach (KeyValuePair<string, List<string>> pair in incoming)
            {
                if (pair.Value.Count > 1)
                {
                    issues.Add(ContractIssue.Error(
                        path,
                        "execFanIn",
                        "Exec input '" + pair.Key + "' has " + pair.Value.Count + " incoming edges: " + string.Join(", ", pair.Value) + "."));
                }
            }
        }

        private static SnapshotData BuildSnapshot(GameObject root, bool includeInactive, bool includeHierarchy)
        {
            var snapshot = new SnapshotData();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(includeInactive);
            foreach (Transform transform in transforms)
            {
                Component[] components = transform.GetComponents<Component>();
                string path = GetHierarchyPath(root.transform, transform);
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        snapshot.MissingScripts.Add(new { path, componentIndex = i });
                    }
                }

                if (includeHierarchy)
                {
                    snapshot.Hierarchy.Add(new
                    {
                        path,
                        name = transform.name,
                        activeSelf = transform.gameObject.activeSelf,
                        components = components.Where(component => component != null).Select(component => component.GetType().FullName).ToArray()
                    });
                }
            }

            foreach (BlueprintRunner runner in root.GetComponentsInChildren<BlueprintRunner>(includeInactive))
                snapshot.BlueprintRunners.Add(BuildBlueprintRunnerSummary(root.transform, runner, snapshot.RunnerWarnings));
            foreach (BehaviorTreeRunner runner in root.GetComponentsInChildren<BehaviorTreeRunner>(includeInactive))
                snapshot.BehaviorTreeRunners.Add(BuildBehaviorTreeRunnerSummary(root.transform, runner, snapshot.RunnerWarnings));

            return snapshot;
        }

        private static object BuildBlueprintRunnerSummary(Transform root, BlueprintRunner runner, List<object> warnings)
        {
            var so = new SerializedObject(runner);
            SerializedProperty compiledProperty = so.FindProperty("compiledBlueprint");
            SerializedProperty ownerProperty = so.FindProperty("ownerRunner");
            SerializedProperty bindingsProperty = so.FindProperty("bindings");
            BlueprintCompiledAsset compiledAsset = compiledProperty?.objectReferenceValue as BlueprintCompiledAsset;
            BlueprintRunner owner = ownerProperty?.objectReferenceValue as BlueprintRunner;
            string runnerPath = GetHierarchyPath(root, runner.transform);

            var bindings = new List<object>();
            if (bindingsProperty != null && bindingsProperty.isArray)
            {
                for (int i = 0; i < bindingsProperty.arraySize; i++)
                {
                    SerializedProperty entry = bindingsProperty.GetArrayElementAtIndex(i);
                    SerializedProperty name = entry.FindPropertyRelative("Name");
                    SerializedProperty target = entry.FindPropertyRelative("Target");
                    UnityEngine.Object targetObject = target?.objectReferenceValue;
                    if (targetObject == null)
                    {
                        warnings.Add(new
                        {
                            runnerPath,
                            rule = "missingBindingTarget",
                            bindingName = name?.stringValue ?? "",
                            message = "Binding target is missing."
                        });
                    }

                    bindings.Add(new
                    {
                        name = name?.stringValue ?? "",
                        targetName = targetObject == null ? "" : targetObject.name,
                        targetType = targetObject == null ? "" : targetObject.GetType().FullName,
                        targetPath = targetObject is Component component
                            ? GetHierarchyPath(root, component.transform)
                            : targetObject is GameObject gameObject
                                ? GetHierarchyPath(root, gameObject.transform)
                                : ""
                    });
                }
            }

            if (compiledAsset == null)
            {
                warnings.Add(new
                {
                    runnerPath,
                    rule = "missingCompiledBlueprint",
                    message = "BlueprintRunner has no compiled Blueprint asset."
                });
            }

            return new
            {
                path = runnerPath,
                componentType = runner.GetType().Name,
                blueprintPath = compiledAsset == null ? "" : compiledAsset.SourcePath,
                compiledAssetPath = compiledAsset == null ? "" : AssetDatabase.GetAssetPath(compiledAsset),
                ownerRunnerPath = owner == null ? "" : GetHierarchyPath(root, owner.transform),
                bindings
            };
        }

        private static object BuildBehaviorTreeRunnerSummary(Transform root, BehaviorTreeRunner runner, List<object> warnings)
        {
            var so = new SerializedObject(runner);
            SerializedProperty compiledProperty = so.FindProperty("compiledBehaviorTree");
            BehaviorTreeCompiledAsset compiledAsset = compiledProperty?.objectReferenceValue as BehaviorTreeCompiledAsset;
            string runnerPath = GetHierarchyPath(root, runner.transform);
            if (compiledAsset == null)
            {
                warnings.Add(new
                {
                    runnerPath,
                    rule = "missingCompiledBehaviorTree",
                    message = "BehaviorTreeRunner has no compiled Behavior Tree asset."
                });
            }

            return new
            {
                path = runnerPath,
                behaviorTreePath = compiledAsset == null ? "" : compiledAsset.SourcePath,
                compiledAssetPath = compiledAsset == null ? "" : AssetDatabase.GetAssetPath(compiledAsset),
                isRunning = runner.IsRunning,
                playOnStart = ReadBool(so, "playOnStart"),
                restartOnEnable = ReadBool(so, "restartOnEnable"),
                tickMode = ReadEnum(so, "tickMode"),
                maxTickRate = ReadFloat(so, "maxTickRate"),
                intervalSeconds = ReadFloat(so, "intervalSeconds")
            };
        }

        private static object BuildRuntimeBlueprintRunnerSnapshot(Transform root, BlueprintRunner runner, List<string> requestedVariables)
        {
            var variables = new Dictionary<string, object>(StringComparer.Ordinal);
            IEnumerable<string> names = requestedVariables == null || requestedVariables.Count == 0
                ? runner.CompiledBlueprint == null ? Enumerable.Empty<string>() : runner.CompiledBlueprint.Variables.Select(variable => variable.Name)
                : requestedVariables;
            foreach (string name in names.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal))
            {
                if (runner.TryGetVariable(name, out object value))
                    variables[name] = ToSerializableValue(value);
            }

            return new
            {
                path = GetHierarchyPath(root, runner.transform),
                componentType = runner.GetType().Name,
                blueprintPath = runner.SourcePath ?? "",
                compiled = runner.RuntimeBlueprint != null,
                variables
            };
        }

        private static object BuildRuntimeBehaviorTreeSnapshot(
            Transform root,
            BehaviorTreeRunner runner,
            List<string> requestedKeys,
            bool includeFullSnapshot)
        {
            BehaviorTreeDebugSnapshot snapshot = runner.GetDebugSnapshot();
            Dictionary<string, object> blackboard = new Dictionary<string, object>(StringComparer.Ordinal);
            if (requestedKeys == null || requestedKeys.Count == 0)
            {
                foreach (KeyValuePair<string, object> pair in snapshot.BlackboardValues)
                    blackboard[pair.Key] = ToSerializableValue(pair.Value);
            }
            else
            {
                foreach (string key in requestedKeys.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal))
                {
                    if (runner.TryGetBlackboardValue(key, out object value))
                        blackboard[key] = ToSerializableValue(value);
                }
            }

            object fullSnapshot = includeFullSnapshot
                ? new
                {
                    treeName = snapshot.TreeName,
                    sourcePath = snapshot.SourcePath,
                    tickIndex = snapshot.TickIndex,
                    timeSeconds = snapshot.TimeSeconds,
                    lastStatus = snapshot.LastStatus.ToString(),
                    activePath = snapshot.ActivePath.ToArray(),
                    runningTaskNodeId = snapshot.RunningTaskNodeId,
                    runningTaskNodeIds = snapshot.RunningTaskNodeIds.ToArray(),
                    lastAbortReason = snapshot.LastAbortReason,
                    lastFailureReason = snapshot.LastFailureReason,
                    nodeStatuses = snapshot.NodeStatuses,
                    decoratorResults = snapshot.DecoratorResults
                }
                : null;

            return new
            {
                path = GetHierarchyPath(root, runner.transform),
                behaviorTreePath = runner.CompiledBehaviorTree == null ? "" : runner.CompiledBehaviorTree.SourcePath,
                isRunning = runner.IsRunning,
                blackboard,
                debugSnapshot = fullSnapshot
            };
        }

        private static object ParseJsonAsset(string sourcePath, out bool success)
        {
            string filePath = ToProjectFilePath(sourcePath);
            string kind = GetSourceKind(sourcePath);
            try
            {
                string json = File.ReadAllText(filePath);
                JObject parsed = JObject.Parse(json);
                success = true;
                return new
                {
                    sourcePath,
                    kind,
                    success = true,
                    name = ReadString(parsed, "name"),
                    schemaVersion = ReadString(parsed, "schemaVersion")
                };
            }
            catch (Exception ex)
            {
                success = false;
                return new
                {
                    sourcePath,
                    kind,
                    success = false,
                    error = ex.Message
                };
            }
        }

        private static List<BlueprintRuntimeRegistryGenerationReport> SyncRuntimeRegistries(string mode, bool log)
        {
            string normalized = string.IsNullOrWhiteSpace(mode) ? "project" : mode.Trim().ToLowerInvariant();
            var reports = new List<BlueprintRuntimeRegistryGenerationReport>();
            if (normalized == "none")
                return reports;
            if (normalized == "package" || normalized == "all")
                reports.Add(BlueprintRuntimeRegistryAssetManagerUtility.SyncPackageRegistry(log));
            if (normalized == "project" || normalized == "all")
                reports.Add(BlueprintRuntimeRegistryAssetManagerUtility.SyncProjectOverlay(log));
            if (reports.Count == 0)
                reports.Add(BlueprintRuntimeRegistryAssetManagerUtility.SyncProjectOverlay(log));
            return reports;
        }

        private static object BuildRegistryReport(BlueprintRuntimeRegistryGenerationReport report)
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

        private static Dictionary<string, JObject> ReadObjectsById(JToken token)
        {
            var result = new Dictionary<string, JObject>(StringComparer.Ordinal);
            if (token is not JArray array)
                return result;
            foreach (JObject item in array.OfType<JObject>())
            {
                string id = ReadString(item, "id");
                if (!string.IsNullOrWhiteSpace(id))
                    result[id] = item;
            }

            return result;
        }

        private static GameObject FindLoadedSceneObject(string hierarchyPath)
        {
            if (string.IsNullOrWhiteSpace(hierarchyPath))
                return null;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    GameObject match = FindGameObjectByPath(root, hierarchyPath);
                    if (match != null)
                        return match;
                }
            }

            return null;
        }

        private static GameObject FindGameObjectByPath(GameObject root, string path)
        {
            string normalized = NormalizeHierarchyPath(path);
            if (string.IsNullOrEmpty(normalized) || normalized == root.name)
                return root;
            if (normalized.StartsWith(root.name + "/", StringComparison.Ordinal))
                normalized = normalized.Substring(root.name.Length + 1);

            Transform current = root.transform;
            foreach (string segment in normalized.Split('/'))
            {
                current = FindDirectChild(current, segment);
                if (current == null)
                    return null;
            }

            return current.gameObject;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }

        private static object ToSerializableValue(object value)
        {
            if (value == null)
                return null;
            if (value is Vector2 v2)
                return ToArray(v2);
            if (value is Vector3 v3)
                return ToArray(v3);
            if (value is Quaternion quaternion)
                return ToArray(quaternion.eulerAngles);
            if (value is UnityEngine.Object unityObject)
                return new
                {
                    name = unityObject.name,
                    type = unityObject.GetType().FullName,
                    assetPath = AssetDatabase.GetAssetPath(unityObject)
                };
            return value;
        }

        private static string BuildEndpoint(string endpoint, string node, string port)
        {
            if (!string.IsNullOrWhiteSpace(endpoint))
                return endpoint.Trim();
            if (!string.IsNullOrWhiteSpace(node) && !string.IsNullOrWhiteSpace(port))
                return node.Trim() + "." + port.Trim();
            return "";
        }

        private static string EndpointNodeId(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return "";
            int dot = endpoint.IndexOf('.');
            return dot < 0 ? endpoint : endpoint.Substring(0, dot);
        }

        private static string DescribeNodeRequirement(BlueprintNodeRequirement requirement)
        {
            return "{id='" + (requirement.Id ?? "") + "', typeId='" + (requirement.TypeId ?? "") + "'}";
        }

        private static string DescribeNamedRequirement(BlueprintNamedRequirement requirement)
        {
            return "{name='" + (requirement.Name ?? "") + "', id='" + (requirement.Id ?? "") + "', type='" + (requirement.Type ?? "") + "'}";
        }

        private static string GetSourceKind(string path)
        {
            path = NormalizeAssetPath(path);
            if (path.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase))
                return "blueprint";
            if (path.EndsWith(".btree.json", StringComparison.OrdinalIgnoreCase))
                return "behaviorTree";
            if (path.EndsWith(".bpstruct.json", StringComparison.OrdinalIgnoreCase))
                return "struct";
            if (path.EndsWith(".bpdatatable.json", StringComparison.OrdinalIgnoreCase))
                return "dataTable";
            if (path.EndsWith(".resourceblueprint.json", StringComparison.OrdinalIgnoreCase))
                return "resourceBlueprint";
            return "json";
        }

        private static bool AssetExists(string assetPath)
        {
            return File.Exists(ToProjectFilePath(assetPath));
        }

        private static bool IsSuccessPayload(object payload)
        {
            return payload != null &&
                   payload.GetType().GetProperty("success")?.GetValue(payload) is bool success &&
                   success;
        }

        private static string ReadString(JObject obj, string key)
        {
            return obj == null ? "" : obj[key]?.ToString() ?? "";
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left ?? "", right ?? "", StringComparison.Ordinal);
        }

        private static bool ReadBool(SerializedObject so, string propertyName)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            return property != null && property.boolValue;
        }

        private static string ReadEnum(SerializedObject so, string propertyName)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            return property == null ? "" : property.enumDisplayNames[property.enumValueIndex];
        }

        private static float ReadFloat(SerializedObject so, string propertyName)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            return property == null ? 0f : property.floatValue;
        }

        private static float[] ToArray(Vector2 value)
        {
            return new[] { value.x, value.y };
        }

        private static float[] ToArray(Vector3 value)
        {
            return new[] { value.x, value.y, value.z };
        }

        private static string ToProjectFilePath(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            string projectRoot = Directory.GetCurrentDirectory();
            return Path.Combine(projectRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            string normalized = path.Replace('\\', '/').Trim();
            string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/').TrimEnd('/');
            if (normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(projectRoot.Length + 1);
            return normalized;
        }

        private static List<string> NormalizeAssetPaths(IEnumerable<string> paths)
        {
            return (paths ?? Enumerable.Empty<string>())
                .Select(NormalizeAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeHierarchyPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? "" : path.Replace('\\', '/').Trim('/');
        }

        private static string GetHierarchyPath(Transform root, Transform transform)
        {
            var segments = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                segments.Push(current.name);
                if (current == root)
                    break;
                current = current.parent;
            }

            return string.Join("/", segments);
        }

        private sealed class SnapshotData
        {
            public readonly List<object> MissingScripts = new List<object>();
            public readonly List<object> RunnerWarnings = new List<object>();
            public readonly List<object> BlueprintRunners = new List<object>();
            public readonly List<object> BehaviorTreeRunners = new List<object>();
            public readonly List<object> Hierarchy = new List<object>();
        }

        private sealed class ContractIssue
        {
            public string Severity;
            public string Path;
            public string Rule;
            public string Message;

            public static ContractIssue Error(string path, string rule, string message)
            {
                return new ContractIssue { Severity = "error", Path = path, Rule = rule, Message = message };
            }

            public object ToPayload()
            {
                return new
                {
                    severity = Severity,
                    path = Path,
                    rule = Rule,
                    message = Message
                };
            }
        }

        private sealed class EditorLogCapture : IDisposable
        {
            private readonly List<object> _entries = new List<object>();
            private bool _started;

            public int ErrorCount { get; private set; }
            public int ExceptionCount { get; private set; }
            public int WarningCount { get; private set; }

            public void Start()
            {
                if (_started)
                    return;
                _started = true;
                Application.logMessageReceived += OnLogMessageReceived;
            }

            public object[] StopAndBuild(int maxEntries)
            {
                Stop();
                return _entries.Take(Mathf.Max(0, maxEntries)).ToArray();
            }

            public void Dispose()
            {
                Stop();
            }

            private void Stop()
            {
                if (!_started)
                    return;
                Application.logMessageReceived -= OnLogMessageReceived;
                _started = false;
            }

            private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Assert)
                    ErrorCount++;
                else if (type == LogType.Exception)
                    ExceptionCount++;
                else if (type == LogType.Warning)
                    WarningCount++;

                _entries.Add(new
                {
                    type = type.ToString(),
                    message = condition,
                    stackTrace = string.IsNullOrWhiteSpace(stackTrace) ? "" : stackTrace
                });
            }
        }
    }

    public sealed class ValidateBlueprintAssetsParams
    {
        [McpDescription("Assets-relative .blueprint.json, .btree.json, .bpstruct.json, .bpdatatable.json, or resource blueprint paths.", Required = true)]
        public List<string> SourcePaths { get; set; } = new List<string>();

        [McpDescription("Whether to import/refresh assets before validation.")]
        public bool ImportAssets { get; set; } = true;

        [McpDescription("Whether to sync BlueprintRuntimeRegistry assets before compile.")]
        public bool SyncRuntimeRegistry { get; set; } = true;

        [McpDescription("Runtime registry sync mode: project, package, all, or none.")]
        public string RuntimeRegistryMode { get; set; } = "project";

        [McpDescription("Whether to compile .blueprint.json and .btree.json sources.")]
        public bool Compile { get; set; } = true;

        [McpDescription("Whether compiler and registry flows should log details.")]
        public bool Log { get; set; } = true;

        [McpDescription("Whether captured errors/exceptions should make the tool fail.")]
        public bool FailOnCapturedErrors { get; set; } = true;

        [McpDescription("Maximum captured log entries to include in the response.")]
        public int MaxLogEntries { get; set; } = 50;
    }

    public sealed class BlueprintContractCheckParams
    {
        [McpDescription("Per-asset contract specs to check.", Required = true)]
        public List<BlueprintContractAssetSpec> Assets { get; set; } = new List<BlueprintContractAssetSpec>();

        [McpDescription("Whether warnings should fail the aggregate result.")]
        public bool FailOnWarnings { get; set; }
    }

    public sealed class BlueprintContractAssetSpec
    {
        [McpDescription("Assets-relative .blueprint.json or .btree.json path.", Required = true)]
        public string Path { get; set; }

        public List<BlueprintNodeRequirement> RequiredNodes { get; set; } = new List<BlueprintNodeRequirement>();
        public List<BlueprintNodeRequirement> ForbiddenNodes { get; set; } = new List<BlueprintNodeRequirement>();
        public List<BlueprintEdgeRequirement> RequiredEdges { get; set; } = new List<BlueprintEdgeRequirement>();
        public List<BlueprintEdgeRequirement> ForbiddenEdges { get; set; } = new List<BlueprintEdgeRequirement>();
        public List<BlueprintNamedRequirement> RequiredVariables { get; set; } = new List<BlueprintNamedRequirement>();
        public List<BlueprintNamedRequirement> ForbiddenVariables { get; set; } = new List<BlueprintNamedRequirement>();
        public List<BlueprintNamedRequirement> RequiredBindings { get; set; } = new List<BlueprintNamedRequirement>();
        public List<BlueprintNamedRequirement> ForbiddenBindings { get; set; } = new List<BlueprintNamedRequirement>();
        public List<BlueprintNamedRequirement> RequiredComponents { get; set; } = new List<BlueprintNamedRequirement>();
        public List<BlueprintNamedRequirement> ForbiddenComponents { get; set; } = new List<BlueprintNamedRequirement>();
        public List<BlueprintNamedRequirement> RequiredBlackboardKeys { get; set; } = new List<BlueprintNamedRequirement>();
        public List<BlueprintNamedRequirement> ForbiddenBlackboardKeys { get; set; } = new List<BlueprintNamedRequirement>();

        [McpDescription("Check that each *.execIn endpoint has at most one incoming edge.")]
        public bool CheckExecFanIn { get; set; } = true;

        [McpDescription("Check that all edge endpoints reference existing node ids.")]
        public bool CheckUnknownEdgeNodes { get; set; } = true;
    }

    public sealed class BlueprintNodeRequirement
    {
        [McpDescription("Node id to match.")]
        public string Id { get; set; }

        [McpDescription("Node typeId to match.")]
        public string TypeId { get; set; }

        [McpDescription("Property key/value pairs that must match exactly.")]
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public sealed class BlueprintEdgeRequirement
    {
        [McpDescription("Full edge source endpoint, such as event_start.execOut.")]
        public string From { get; set; }

        [McpDescription("Full edge target endpoint, such as set_value.execIn.")]
        public string To { get; set; }

        [McpDescription("Source node id, used with FromPort when From is omitted.")]
        public string FromNode { get; set; }

        [McpDescription("Source port id, used with FromNode when From is omitted.")]
        public string FromPort { get; set; }

        [McpDescription("Target node id, used with ToPort when To is omitted.")]
        public string ToNode { get; set; }

        [McpDescription("Target port id, used with ToNode when To is omitted.")]
        public string ToPort { get; set; }
    }

    public sealed class BlueprintNamedRequirement
    {
        [McpDescription("Entry name to match.")]
        public string Name { get; set; }

        [McpDescription("Entry id to match.")]
        public string Id { get; set; }

        [McpDescription("Entry type to match.")]
        public string Type { get; set; }

        [McpDescription("Optional property key/value pairs that must match exactly when the entry has properties.")]
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public sealed class BlueprintBindingSnapshotParams
    {
        [McpDescription("Optional Assets-relative prefab path to inspect.")]
        public string PrefabPath { get; set; }

        [McpDescription("Optional loaded scene hierarchy path to inspect when PrefabPath is omitted.")]
        public string RootObjectPath { get; set; }

        [McpDescription("Whether inactive child objects should be included.")]
        public bool IncludeInactive { get; set; } = true;

        [McpDescription("Whether to include a compact hierarchy component list.")]
        public bool IncludeHierarchy { get; set; } = true;
    }

    public sealed class BlueprintRuntimeSnapshotParams
    {
        [McpDescription("Loaded scene hierarchy path to inspect.", Required = true)]
        public string RootObjectPath { get; set; }

        [McpDescription("Whether inactive child objects should be included.")]
        public bool IncludeInactive { get; set; } = true;

        [McpDescription("Optional Blueprint variable names to read. Empty reads all compiled variable names that are available at runtime.")]
        public List<string> BlueprintVariableNames { get; set; } = new List<string>();

        [McpDescription("Optional Behavior Tree blackboard keys to read. Empty reads the full debug snapshot blackboard dictionary.")]
        public List<string> BlackboardKeys { get; set; } = new List<string>();

        [McpDescription("Whether to include active path, node status, decorator result, and failure reason details from BehaviorTreeRunner.GetDebugSnapshot().")]
        public bool IncludeFullBehaviorTreeSnapshot { get; set; } = true;
    }
}
