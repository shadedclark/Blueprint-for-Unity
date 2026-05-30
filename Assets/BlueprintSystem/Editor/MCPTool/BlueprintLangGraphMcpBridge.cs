using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlueprintSystem;
using BlueprintSystem.Editor;
using Unity.AI.Assistant.Editor.Api;
using Unity.AI.MCP.Editor.ToolRegistry;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BlueprintLangGraph.Editor
{
    public static class BlueprintLangGraphMcpBridge
    {
        [McpTool("blueprint_run_unity_figma_to_ui", "Run Unity Assistant's internal Figma-to-Unity UI workflow and return prefab candidates from the requested output folder.", EnabledByDefault = true)]
        public static async Task<object> RunUnityFigmaToUi(RunUnityFigmaToUiParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            string featureName = string.IsNullOrWhiteSpace(parameters.FeatureName) ? "FigmaScreen" : parameters.FeatureName.Trim();
            string figmaUrl = Require(parameters.FigmaUrl, nameof(parameters.FigmaUrl));
            string outputRoot = NormalizeAssetPath(string.IsNullOrWhiteSpace(parameters.UiOutputRoot)
                ? $"Assets/UI/FigmaImport/{SanitizePathSegment(featureName)}"
                : parameters.UiOutputRoot);

            EnsureAssetFolder(outputRoot);
            AssetDatabase.Refresh();

            var before = FindPrefabs(outputRoot);
            string prompt = BuildFigmaToUiPrompt(figmaUrl, featureName, outputRoot);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Mathf.Max(30, parameters.TimeoutSeconds)));
            try
            {
                await AssistantApi.Run(prompt, cancellationToken: timeout.Token);
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    message = "Unity Assistant Figma-to-UI run failed or required interaction.",
                    data = new
                    {
                        blocker = "Unity Assistant Figma-to-UI run failed or required interaction.",
                        error = ex.Message,
                        outputRoot,
                        prefabCandidates = System.Array.Empty<object>()
                    }
                };
            }

            AssetDatabase.Refresh();
            var after = FindPrefabs(outputRoot);
            var layoutRepairs = after.Select(RepairNestedRectOffsetsIfNeeded).ToList();
            if (layoutRepairs.Any(summary => summary.Applied))
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                after = FindPrefabs(outputRoot);
            }
            var created = after.Where(path => !before.Contains(path, StringComparer.OrdinalIgnoreCase)).ToList();

            return new
            {
                success = after.Count > 0,
                message = after.Count == 0 ? "Unity Assistant completed but no prefab was found in the requested output folder." : "Unity Assistant Figma-to-UI completed.",
                data = new
                {
                        blocker = after.Count == 0 ? "Unity Assistant completed but no prefab was found in the requested output folder." : "",
                        outputRoot,
                        prefabCandidates = after.Select(BuildPrefabSummary).ToArray(),
                        createdPrefabCandidates = created.Select(BuildPrefabSummary).ToArray(),
                        layoutRepairs = layoutRepairs.Select(summary => summary.ToPayload()).ToArray()
                    }
                };
        }

        [McpTool("blueprint_inspect_prefab_ui", "Inspect a prefab hierarchy for UI anchors, components, Blueprint runners, and UIBlueprintBinder bindings.", EnabledByDefault = true)]
        public static object InspectPrefabUi(InspectPrefabUiParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            string prefabPath = NormalizeAssetPath(Require(parameters.PrefabPath, nameof(parameters.PrefabPath)));
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var binders = root.GetComponentsInChildren<UIBlueprintBinder>(true)
                    .Select(BuildBinderSummary)
                    .ToArray();

                var nodes = root.GetComponentsInChildren<Transform>(true)
                    .Select(t => BuildTransformSummary(root.transform, t))
                    .ToArray();

                return new
                {
                    success = true,
                    message = "Prefab inspection complete.",
                    data = new
                    {
                        prefabPath,
                        rootName = root.name,
                        binders,
                        nodes
                    }
                };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [McpTool("blueprint_compile_blueprints", "Compile Blueprint .blueprint.json files into .compiled.asset files through the project Blueprint compiler.", EnabledByDefault = true)]
        public static object CompileBlueprints(CompileBlueprintsParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var sourcePaths = (parameters.SourcePaths ?? new List<string>())
                .Select(NormalizeAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var results = new List<object>();
            bool allSucceeded = true;

            BlueprintHotReloadService.ForgetPendingBlueprintPaths(sourcePaths);
            foreach (string sourcePath in sourcePaths)
            {
                AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            using (BlueprintHotReloadService.SuppressAutoCompile(sourcePaths))
            {
                foreach (string sourcePath in sourcePaths)
                {
                    bool success = BlueprintCompiledAssetCompiler.CompileBlueprintAtPath(sourcePath, parameters.Log, out BlueprintCompiledAsset compiledAsset);
                    string compiledPath = BlueprintCompiledAssetCompiler.GetCompiledAssetPath(sourcePath);
                    allSucceeded &= success && compiledAsset != null;

                    results.Add(new
                    {
                        sourcePath,
                        compiledPath,
                        success = success && compiledAsset != null,
                        compiledAssetGuid = compiledAsset == null ? "" : AssetDatabase.AssetPathToGUID(compiledPath)
                    });
                }
            }
            BlueprintHotReloadService.ForgetPendingBlueprintPaths(sourcePaths);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            return new
            {
                success = allSucceeded,
                message = allSucceeded ? "Blueprint compilation complete." : "One or more Blueprint compilations failed.",
                data = new
                {
                    results
                }
            };
        }

        [McpTool("blueprint_apply_ui_bindings", "Apply Blueprint runner assets and UIBlueprintBinder target bindings to a prefab without changing its visual layout.", EnabledByDefault = true)]
        public static object ApplyUiBindings(ApplyUiBindingsParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            string prefabPath = NormalizeAssetPath(Require(parameters.PrefabPath, nameof(parameters.PrefabPath)));
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            var applied = new List<object>();

            try
            {
                UIBlueprintBinder rootBinder = null;
                if (!string.IsNullOrWhiteSpace(parameters.RootBlueprintPath))
                {
                    rootBinder = EnsureBinder(root);
                    ApplyRunnerSerializedState(rootBinder, parameters.RootBlueprintPath, null);
                }

                foreach (BindingApplication binderSpec in parameters.Binders ?? new List<BindingApplication>())
                {
                    string binderPath = NormalizeHierarchyPath(binderSpec.BinderPath);
                    GameObject binderObject = FindGameObjectByPath(root, binderPath) ?? root;
                    UIBlueprintBinder binder = EnsureBinder(binderObject);
                    BlueprintRunner ownerRunner = ResolveOwnerRunner(root, binderSpec.OwnerRunnerPath, rootBinder, binder);

                    string blueprintPath = string.IsNullOrWhiteSpace(binderSpec.BlueprintPath)
                        ? parameters.RootBlueprintPath
                        : binderSpec.BlueprintPath;
                    ApplyRunnerSerializedState(binder, blueprintPath, ownerRunner);
                    ApplyBinderLifecycle(binder, binderSpec);

                    int bindingCount = ApplyBindingEntries(root, binder, binderSpec.Bindings ?? new List<BindingTargetSpec>());
                    EditorUtility.SetDirty(binder);

                    applied.Add(new
                    {
                        binderPath = GetHierarchyPath(root.transform, binder.transform),
                        blueprintPath = NormalizeAssetPath(blueprintPath),
                        ownerRunnerPath = ownerRunner == null ? "" : GetHierarchyPath(root.transform, ownerRunner.transform),
                        bindingCount
                    });
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new
            {
                success = true,
                message = "Prefab bindings applied.",
                data = new
                {
                    prefabPath,
                    applied
                }
            };
        }

        private static string BuildFigmaToUiPrompt(string figmaUrl, string featureName, string outputRoot)
        {
            return string.Join("\n", new[]
            {
                $"Create UI based on Figma design URL {figmaUrl}",
                $"Feature name: {featureName}",
                $"Output folder: {outputRoot}",
                "Requirements:",
                "- Use the node-id in the URL directly when present.",
                "- Create or update assets only under the requested output folder.",
                "- Produce a prefab asset in that folder or a Prefabs subfolder.",
                "- Preserve the visual hierarchy and names from Figma where practical.",
                "- Do not call Unity.GetImageAssetContent. It is not required for prefab generation; use the Figma summary, node data, downloaded PNG asset paths, and Unity asset metadata instead.",
                "- If another tool response recommends calling Unity.GetImageAssetContent for a reference screenshot, treat that recommendation as optional and skip it unless the user explicitly asks for visual image inspection.",
                "- When nesting RectTransforms under Figma groups, convert Figma absoluteBoundingBox coordinates to local parent-space anchoredPosition values. Do not repeat root-space positions on nested children.",
                "- After the prefab is created, run a sprite-only verification pass without changing hierarchy, RectTransform anchors, pivots, sizes, positions, or layout.",
                "- Verify nested RectTransforms: children under state/group objects must remain inside their parent bounds unless the Figma design intentionally overflows.",
                "- Inspect every UnityEngine.UI.Image in the prefab. Images created from PNG or sliced image assets must have a non-null sprite reference.",
                "- Ensure PNG assets under the output folder are imported as Sprite assets. If a PNG is imported as Multiple Sprite but has no sprite sub-assets, switch it to Single Sprite and reimport it.",
                "- For any Image with a missing sprite, match the Sprite by the Image GameObject name or original Figma image name, assign only Image.sprite, and save the prefab again.",
                "- Before answering, include image count, missing sprite count, repaired sprite count, and unresolved Image hierarchy paths.",
                "- Do not add BlueprintRunner, UIBlueprintBinder, or Blueprint runtime components.",
                "- Do not enter Play Mode.",
                "When done, answer with the prefab asset path."
            });
        }

        private static object BuildPrefabSummary(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return new
            {
                prefabPath,
                name = prefab == null ? Path.GetFileNameWithoutExtension(prefabPath) : prefab.name,
                guid = AssetDatabase.AssetPathToGUID(prefabPath)
            };
        }

        private static object BuildTransformSummary(Transform root, Transform transform)
        {
            Component[] components = transform.GetComponents<Component>();
            RectTransform rectTransform = transform as RectTransform;
            return new
            {
                path = GetHierarchyPath(root, transform),
                name = transform.name,
                activeSelf = transform.gameObject.activeSelf,
                components = components.Where(c => c != null).Select(c => c.GetType().FullName).ToArray(),
                uiRole = GetUiRole(transform.gameObject),
                text = GetTextValue(transform.gameObject),
                spritePath = GetSpritePath(transform.gameObject),
                rectTransform = rectTransform == null ? null : new
                {
                    anchorMin = ToArray(rectTransform.anchorMin),
                    anchorMax = ToArray(rectTransform.anchorMax),
                    pivot = ToArray(rectTransform.pivot),
                    anchoredPosition = ToArray(rectTransform.anchoredPosition),
                    sizeDelta = ToArray(rectTransform.sizeDelta),
                    localScale = ToArray(rectTransform.localScale)
                }
            };
        }

        private enum LayoutRepairMode
        {
            None,
            SubtractParent,
            AddParentTopDown
        }

        public static LayoutRepairSummary RepairNestedRectOffsetsIfNeeded(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                if (rootRect == null)
                    return new LayoutRepairSummary(prefabPath, 0, 0, 0, false, LayoutRepairMode.None.ToString(), 0d, 0d);

                RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
                Dictionary<RectTransform, Vector2> originalPositions = rects.ToDictionary(rt => rt, rt => rt.anchoredPosition);
                int candidateCount = rects.Count(rect =>
                {
                    RectTransform parent = rect.parent as RectTransform;
                    return rect != rootRect && parent != null && parent != rootRect;
                });
                double currentScore = ScoreLayout(rects, rootRect, originalPositions);
                int suspiciousCount = CountLayoutOverflows(rects, rootRect, originalPositions);

                Dictionary<RectTransform, Vector2> subtractPositions = BuildSubtractParentPositions(rects, rootRect, originalPositions);
                double subtractScore = ScoreLayout(rects, rootRect, subtractPositions);
                Dictionary<RectTransform, Vector2> addPositions = BuildAddParentTopDownPositions(rects, rootRect, originalPositions);
                double addScore = ScoreLayout(rects, rootRect, addPositions);

                LayoutRepairMode bestMode = LayoutRepairMode.None;
                double bestScore = currentScore;
                Dictionary<RectTransform, Vector2> bestPositions = originalPositions;
                if (subtractScore < bestScore)
                {
                    bestMode = LayoutRepairMode.SubtractParent;
                    bestScore = subtractScore;
                    bestPositions = subtractPositions;
                }

                if (addScore < bestScore)
                {
                    bestMode = LayoutRepairMode.AddParentTopDown;
                    bestScore = addScore;
                    bestPositions = addPositions;
                }

                double improvementThreshold = Math.Max(32d, currentScore * 0.2d);
                bool shouldRepair = candidateCount > 0 && currentScore > 8d && bestMode != LayoutRepairMode.None &&
                    bestScore + improvementThreshold < currentScore;
                int repairedCount = 0;
                if (shouldRepair)
                {
                    foreach (RectTransform rect in rects)
                    {
                        if (rect == rootRect)
                            continue;

                        Vector2 repairedPosition;
                        if (!bestPositions.TryGetValue(rect, out repairedPosition))
                            continue;

                        if ((rect.anchoredPosition - repairedPosition).sqrMagnitude > 0.001f)
                        {
                            rect.anchoredPosition = repairedPosition;
                            EditorUtility.SetDirty(rect);
                            repairedCount++;
                        }
                    }

                    EditorUtility.SetDirty(root);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }

                return new LayoutRepairSummary(
                    prefabPath,
                    candidateCount,
                    suspiciousCount,
                    repairedCount,
                    shouldRepair,
                    bestMode.ToString(),
                    currentScore,
                    bestScore);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Dictionary<RectTransform, Vector2> BuildSubtractParentPositions(
            RectTransform[] rects,
            RectTransform rootRect,
            IReadOnlyDictionary<RectTransform, Vector2> originalPositions)
        {
            Dictionary<RectTransform, Vector2> positions = originalPositions.ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (RectTransform rect in rects)
            {
                RectTransform parent = rect.parent as RectTransform;
                if (rect == rootRect || parent == null)
                    continue;

                positions[rect] = originalPositions[rect] - originalPositions[parent];
            }

            return positions;
        }

        private static Dictionary<RectTransform, Vector2> BuildAddParentTopDownPositions(
            RectTransform[] rects,
            RectTransform rootRect,
            IReadOnlyDictionary<RectTransform, Vector2> originalPositions)
        {
            Dictionary<RectTransform, Vector2> positions = new Dictionary<RectTransform, Vector2>();
            foreach (RectTransform rect in rects.OrderBy(GetTransformDepth))
            {
                RectTransform parent = rect.parent as RectTransform;
                if (rect == rootRect || parent == null || parent == rootRect)
                {
                    positions[rect] = originalPositions[rect];
                    continue;
                }

                Vector2 parentPosition;
                if (!positions.TryGetValue(parent, out parentPosition))
                    parentPosition = originalPositions[parent];

                positions[rect] = originalPositions[rect] + parentPosition;
            }

            return positions;
        }

        private static int GetTransformDepth(Transform transform)
        {
            int depth = 0;
            Transform cursor = transform;
            while (cursor.parent != null)
            {
                depth++;
                cursor = cursor.parent;
            }

            return depth;
        }

        private static double ScoreLayout(
            RectTransform[] rects,
            RectTransform rootRect,
            IReadOnlyDictionary<RectTransform, Vector2> positions)
        {
            double score = 0d;
            foreach (RectTransform rect in rects)
            {
                RectTransform parent = rect.parent as RectTransform;
                if (rect == rootRect || parent == null)
                    continue;

                Vector2 position;
                if (!positions.TryGetValue(rect, out position))
                    position = rect.anchoredPosition;

                float penalty = RectOverflowPenalty(rect, parent, position);
                score += penalty * penalty;
            }

            return score;
        }

        private static int CountLayoutOverflows(
            RectTransform[] rects,
            RectTransform rootRect,
            IReadOnlyDictionary<RectTransform, Vector2> positions)
        {
            int count = 0;
            foreach (RectTransform rect in rects)
            {
                RectTransform parent = rect.parent as RectTransform;
                if (rect == rootRect || parent == null || parent == rootRect)
                    continue;

                Vector2 position;
                if (!positions.TryGetValue(rect, out position))
                    position = rect.anchoredPosition;

                if (RectOverflowPenalty(rect, parent, position) > 2f)
                    count++;
            }

            return count;
        }

        private static float RectOverflowPenalty(RectTransform rect, RectTransform parent, Vector2 anchoredPosition)
        {
            if (!Approximately(rect.anchorMin, rect.anchorMax))
                return 0f;

            float parentWidth = Mathf.Max(1f, parent.rect.width);
            float parentHeight = Mathf.Max(1f, parent.rect.height);
            float rectWidth = Mathf.Max(1f, rect.rect.width);
            float rectHeight = Mathf.Max(1f, rect.rect.height);
            float parentMinX = -parent.pivot.x * parentWidth;
            float parentMaxX = (1f - parent.pivot.x) * parentWidth;
            float parentMinY = -parent.pivot.y * parentHeight;
            float parentMaxY = (1f - parent.pivot.y) * parentHeight;
            float anchorX = Mathf.Lerp(parentMinX, parentMaxX, rect.anchorMin.x);
            float anchorY = Mathf.Lerp(parentMinY, parentMaxY, rect.anchorMin.y);
            float pivotX = anchorX + anchoredPosition.x;
            float pivotY = anchorY + anchoredPosition.y;
            float childMinX = pivotX - rect.pivot.x * rectWidth;
            float childMaxX = pivotX + (1f - rect.pivot.x) * rectWidth;
            float childMinY = pivotY - rect.pivot.y * rectHeight;
            float childMaxY = pivotY + (1f - rect.pivot.y) * rectHeight;
            float tolerance = Mathf.Max(4f, Mathf.Min(parentWidth, parentHeight) * 0.02f);

            float overflow = 0f;
            overflow += Mathf.Max(0f, parentMinX - childMinX - tolerance);
            overflow += Mathf.Max(0f, childMaxX - parentMaxX - tolerance);
            overflow += Mathf.Max(0f, parentMinY - childMinY - tolerance);
            overflow += Mathf.Max(0f, childMaxY - parentMaxY - tolerance);
            return overflow;
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return (left - right).sqrMagnitude <= 0.0001f;
        }

        private static bool LooksLikeRootSpaceOffset(RectTransform rect, RectTransform parent, Vector2 anchoredPosition)
        {
            if (RectOverflowPenalty(rect, parent, anchoredPosition) > 2f)
                return true;

            if (Approximately(rect.anchorMin, rect.anchorMax) && Approximately(parent.anchorMin, parent.anchorMax))
            {
                float parentSimilarity = Mathf.Max(8f, Mathf.Max(parent.rect.width, parent.rect.height) * 0.08f);
                return Vector2.Distance(anchoredPosition, parent.anchoredPosition) <= parentSimilarity;
            }

            return false;
        }

        private static float[] ToArray(Vector2 value)
        {
            return new[] { value.x, value.y };
        }

        private static float[] ToArray(Vector3 value)
        {
            return new[] { value.x, value.y, value.z };
        }

        private static object BuildBinderSummary(UIBlueprintBinder binder)
        {
            var so = new SerializedObject(binder);
            var compiledProperty = so.FindProperty("compiledBlueprint");
            var ownerProperty = so.FindProperty("ownerRunner");
            var bindingsProperty = so.FindProperty("bindings");
            var compiledAsset = compiledProperty?.objectReferenceValue as BlueprintCompiledAsset;
            var owner = ownerProperty?.objectReferenceValue as BlueprintRunner;

            var bindings = new List<object>();
            if (bindingsProperty != null && bindingsProperty.isArray)
            {
                for (int i = 0; i < bindingsProperty.arraySize; i++)
                {
                    SerializedProperty entry = bindingsProperty.GetArrayElementAtIndex(i);
                    SerializedProperty name = entry.FindPropertyRelative("Name");
                    SerializedProperty target = entry.FindPropertyRelative("Target");
                    UnityEngine.Object targetObject = target?.objectReferenceValue;
                    bindings.Add(new
                    {
                        name = name?.stringValue ?? "",
                        targetName = targetObject == null ? "" : targetObject.name,
                        targetType = targetObject == null ? "" : targetObject.GetType().FullName
                    });
                }
            }

            return new
            {
                path = GetHierarchyPath(binder.transform.root, binder.transform),
                blueprintPath = BlueprintCompiledAssetCompiler.GetCompiledAssetSourcePath(compiledAsset),
                compiledAssetPath = compiledAsset == null ? "" : AssetDatabase.GetAssetPath(compiledAsset),
                ownerRunnerPath = owner == null ? "" : GetHierarchyPath(binder.transform.root, owner.transform),
                bindings
            };
        }

        private static string GetUiRole(GameObject gameObject)
        {
            if (gameObject.GetComponent<Button>() != null)
                return "Button";
            if (gameObject.GetComponent<Image>() != null)
                return "Image";
            if (!string.IsNullOrEmpty(GetTextValue(gameObject)))
                return "Text";
            if (gameObject.GetComponent<RectTransform>() != null)
                return "RectTransform";
            return "";
        }

        private static string GetTextValue(GameObject gameObject)
        {
            Component[] components = gameObject.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                    continue;

                Type type = component.GetType();
                PropertyInfo property = type.GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (property != null && property.PropertyType == typeof(string))
                    return property.GetValue(component) as string ?? "";
            }

            return "";
        }

        private static string GetSpritePath(GameObject gameObject)
        {
            Image image = gameObject.GetComponent<Image>();
            return image == null || image.sprite == null ? "" : AssetDatabase.GetAssetPath(image.sprite);
        }

        private static int ApplyBindingEntries(GameObject root, UIBlueprintBinder binder, List<BindingTargetSpec> bindings)
        {
            var so = new SerializedObject(binder);
            SerializedProperty bindingsProperty = so.FindProperty("bindings");
            if (bindingsProperty == null || !bindingsProperty.isArray)
                throw new InvalidOperationException("UIBlueprintBinder serialized bindings field was not found.");

            bindingsProperty.ClearArray();
            for (int i = 0; i < bindings.Count; i++)
            {
                BindingTargetSpec spec = bindings[i];
                if (string.IsNullOrWhiteSpace(spec.Name))
                    continue;

                UnityEngine.Object target = ResolveBindingTarget(root, spec);
                if (target == null)
                    throw new InvalidOperationException($"Binding target was not found: {spec.TargetPath} ({spec.TargetComponentType})");

                int index = bindingsProperty.arraySize;
                bindingsProperty.InsertArrayElementAtIndex(index);
                SerializedProperty entry = bindingsProperty.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("Name").stringValue = spec.Name;
                entry.FindPropertyRelative("Target").objectReferenceValue = target;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            binder.RebuildBindingCache();
            return bindingsProperty.arraySize;
        }

        private static UnityEngine.Object ResolveBindingTarget(GameObject root, BindingTargetSpec spec)
        {
            GameObject targetObject = string.IsNullOrWhiteSpace(spec.TargetPath)
                ? root
                : FindGameObjectByPath(root, spec.TargetPath);
            if (targetObject == null)
                return null;

            if (string.IsNullOrWhiteSpace(spec.TargetComponentType) ||
                string.Equals(spec.TargetComponentType, "GameObject", StringComparison.OrdinalIgnoreCase))
            {
                return targetObject;
            }

            if (IsButtonType(spec.TargetComponentType))
                return EnsureButton(targetObject);

            return targetObject.GetComponents<Component>()
                .FirstOrDefault(component => ComponentTypeMatches(component, spec.TargetComponentType));
        }

        private static bool IsButtonType(string requestedType)
        {
            return string.Equals(requestedType, "Button", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestedType, typeof(Button).FullName, StringComparison.OrdinalIgnoreCase);
        }

        private static Button EnsureButton(GameObject targetObject)
        {
            Button button = targetObject.GetComponent<Button>();
            if (button == null)
                button = targetObject.AddComponent<Button>();

            if (button.targetGraphic == null)
                button.targetGraphic = targetObject.GetComponent<Graphic>() ?? targetObject.GetComponentInChildren<Graphic>(true);

            EditorUtility.SetDirty(button);
            return button;
        }

        private static bool ComponentTypeMatches(Component component, string requestedType)
        {
            if (component == null || string.IsNullOrWhiteSpace(requestedType))
                return false;

            Type type = component.GetType();
            while (type != null)
            {
                if (TypeNameMatches(type, requestedType))
                    return true;
                type = type.BaseType;
            }

            return component.GetType().GetInterfaces().Any(typeInterface => TypeNameMatches(typeInterface, requestedType));
        }

        private static bool TypeNameMatches(Type type, string requestedType)
        {
            return type != null &&
                (string.Equals(type.Name, requestedType, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type.FullName, requestedType, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(type.FullName) && type.FullName.EndsWith("." + requestedType, StringComparison.OrdinalIgnoreCase)));
        }

        private static BlueprintRunner ResolveOwnerRunner(GameObject root, string ownerRunnerPath, BlueprintRunner rootBinder, BlueprintRunner binder)
        {
            if (!string.IsNullOrWhiteSpace(ownerRunnerPath))
            {
                GameObject ownerObject = FindGameObjectByPath(root, ownerRunnerPath);
                BlueprintRunner owner = ownerObject == null ? null : ownerObject.GetComponent<BlueprintRunner>();
                if (owner != null && owner != binder)
                    return owner;
            }

            return rootBinder != null && rootBinder != binder ? rootBinder : null;
        }

        private static UIBlueprintBinder EnsureBinder(GameObject gameObject)
        {
            UIBlueprintBinder binder = gameObject.GetComponent<UIBlueprintBinder>();
            if (binder == null)
                binder = gameObject.AddComponent<UIBlueprintBinder>();
            return binder;
        }

        private static void ApplyRunnerSerializedState(BlueprintRunner runner, string blueprintPath, BlueprintRunner ownerRunner)
        {
            var so = new SerializedObject(runner);
            SerializedProperty compiled = so.FindProperty("compiledBlueprint");
            SerializedProperty owner = so.FindProperty("ownerRunner");

            if (!string.IsNullOrWhiteSpace(blueprintPath))
            {
                string sourcePath = NormalizeAssetPath(blueprintPath);
                string compiledPath = BlueprintCompiledAssetCompiler.GetCompiledAssetPath(sourcePath);
                BlueprintCompiledAsset compiledAsset = AssetDatabase.LoadAssetAtPath<BlueprintCompiledAsset>(compiledPath);
                if (compiledAsset == null)
                    throw new InvalidOperationException($"Compiled Blueprint asset was not found for {sourcePath}. Expected {compiledPath}.");
                compiled.objectReferenceValue = compiledAsset;
            }

            if (owner != null)
                owner.objectReferenceValue = ownerRunner;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyBinderLifecycle(UIBlueprintBinder binder, BindingApplication binderSpec)
        {
            var so = new SerializedObject(binder);
            so.FindProperty("triggerOnEnable").boolValue = binderSpec.TriggerOnEnable;
            so.FindProperty("enableEventName").stringValue = string.IsNullOrWhiteSpace(binderSpec.EnableEventName) ? "OnOpen" : binderSpec.EnableEventName;
            so.FindProperty("triggerOnDisable").boolValue = binderSpec.TriggerOnDisable;
            so.FindProperty("disableEventName").stringValue = string.IsNullOrWhiteSpace(binderSpec.DisableEventName) ? "OnClose" : binderSpec.DisableEventName;
            so.ApplyModifiedPropertiesWithoutUndo();
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
                if (string.IsNullOrEmpty(segment))
                    continue;

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

        private static string NormalizeHierarchyPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? "" : path.Replace('\\', '/').Trim('/');
        }

        private static HashSet<string> FindPrefabs(string outputRoot)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { outputRoot });
            return guids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string normalized = NormalizeAssetPath(assetFolder);
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            string[] segments = normalized.Split('/');
            if (segments.Length == 0 || segments[0] != "Assets")
                throw new InvalidOperationException($"Output folder must be under Assets: {assetFolder}");

            string current = "Assets";
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
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

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Missing required parameter: {name}");
            return value.Trim();
        }

        private static string SanitizePathSegment(string value)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
            string sanitized = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "FigmaScreen" : sanitized;
        }
    }

    public sealed class LayoutRepairSummary
    {
        public LayoutRepairSummary(
            string prefabPath,
            int candidateCount,
            int suspiciousCount,
            int repairedCount,
            bool applied,
            string mode,
            double currentScore,
            double bestScore)
        {
            PrefabPath = prefabPath;
            CandidateCount = candidateCount;
            SuspiciousCount = suspiciousCount;
            RepairedCount = repairedCount;
            Applied = applied;
            Mode = mode;
            CurrentScore = currentScore;
            BestScore = bestScore;
        }

        public string PrefabPath { get; }
        public int CandidateCount { get; }
        public int SuspiciousCount { get; }
        public int RepairedCount { get; }
        public bool Applied { get; }
        public string Mode { get; }
        public double CurrentScore { get; }
        public double BestScore { get; }

        public object ToPayload()
        {
            return new
            {
                prefabPath = PrefabPath,
                candidateCount = CandidateCount,
                suspiciousCount = SuspiciousCount,
                repairedCount = RepairedCount,
                applied = Applied,
                mode = Mode,
                currentScore = CurrentScore,
                bestScore = BestScore
            };
        }
    }

    public sealed class RunUnityFigmaToUiParams
    {
        [McpDescription("Full Figma design URL.", Required = true)]
        public string FigmaUrl { get; set; }

        [McpDescription("Feature name used in the Unity Assistant prompt.")]
        public string FeatureName { get; set; }

        [McpDescription("Assets-relative output folder for generated UI assets and prefab.")]
        public string UiOutputRoot { get; set; }

        [McpDescription("Timeout in seconds for the Unity Assistant run.")]
        public int TimeoutSeconds { get; set; } = 300;
    }

    public sealed class InspectPrefabUiParams
    {
        [McpDescription("Assets-relative prefab path.", Required = true)]
        public string PrefabPath { get; set; }
    }

    public sealed class CompileBlueprintsParams
    {
        [McpDescription("Assets-relative .blueprint.json paths to compile.", Required = true)]
        public List<string> SourcePaths { get; set; } = new List<string>();

        [McpDescription("Whether the Blueprint compiler should log detailed errors.")]
        public bool Log { get; set; } = true;
    }

    public sealed class ApplyUiBindingsParams
    {
        [McpDescription("Assets-relative prefab path.", Required = true)]
        public string PrefabPath { get; set; }

        [McpDescription("Optional root Blueprint .blueprint.json path.")]
        public string RootBlueprintPath { get; set; }

        [McpDescription("Binder applications to apply to prefab objects.")]
        public List<BindingApplication> Binders { get; set; } = new List<BindingApplication>();
    }

    public sealed class BindingApplication
    {
        [McpDescription("Hierarchy path to the GameObject that owns this UIBlueprintBinder.")]
        public string BinderPath { get; set; }

        [McpDescription("Blueprint .blueprint.json source path for this binder.")]
        public string BlueprintPath { get; set; }

        [McpDescription("Optional hierarchy path to the owner BlueprintRunner.")]
        public string OwnerRunnerPath { get; set; }

        [McpDescription("Whether this binder should trigger its enable event.")]
        public bool TriggerOnEnable { get; set; } = true;

        [McpDescription("Enable event name.")]
        public string EnableEventName { get; set; } = "OnOpen";

        [McpDescription("Whether this binder should trigger its disable event.")]
        public bool TriggerOnDisable { get; set; } = true;

        [McpDescription("Disable event name.")]
        public string DisableEventName { get; set; } = "OnClose";

        [McpDescription("Named UI binding targets.")]
        public List<BindingTargetSpec> Bindings { get; set; } = new List<BindingTargetSpec>();
    }

    public sealed class BindingTargetSpec
    {
        [McpDescription("Binding key used by Blueprint UI nodes.", Required = true)]
        public string Name { get; set; }

        [McpDescription("Hierarchy path to the target GameObject.")]
        public string TargetPath { get; set; }

        [McpDescription("Optional component type to bind instead of the GameObject, such as Button, Text, TMP_Text, or Image.")]
        public string TargetComponentType { get; set; }
    }
}
