using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TMPro;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LegacyUIText = UnityEngine.UI.Text;
using Object = UnityEngine.Object;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;
using UISelectable = UnityEngine.UI.Selectable;
using UIToggle = UnityEngine.UI.Toggle;

namespace BlueprintSystem.Editor
{
    [InitializeOnLoad]
    internal static class BlueprintGraphToolkitUIDragDrop
    {
        private const string GraphViewTypeName = "Unity.GraphToolkit.Editor.GraphView";
        private const string GraphToolkitSelectionDragKey = "SelectionDropperElements";
        private static readonly BindingFlags ReflectionFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly ConditionalWeakTable<VisualElement, GraphViewDropHandler> Handlers = new ConditionalWeakTable<VisualElement, GraphViewDropHandler>();
        private static double _nextScanTime;

        static BlueprintGraphToolkitUIDragDrop()
        {
            EditorApplication.update += RegisterOpenGraphViews;
        }

        private static void RegisterOpenGraphViews()
        {
            if (EditorApplication.timeSinceStartup < _nextScanTime)
            {
                return;
            }

            _nextScanTime = EditorApplication.timeSinceStartup + 1.0d;
            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                if (window == null || window.rootVisualElement == null)
                {
                    continue;
                }

                RegisterGraphViews(window.rootVisualElement);
            }
        }

        private static void RegisterGraphViews(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            RefreshCustomEventNodeTitle(element);

            if (IsGraphView(element) && !Handlers.TryGetValue(element, out _))
            {
                Handlers.Add(element, new GraphViewDropHandler(element));
            }

            foreach (VisualElement child in element.Children())
            {
                RegisterGraphViews(child);
            }
        }

        private static bool IsGraphView(VisualElement element)
        {
            for (Type type = element.GetType(); type != null; type = type.BaseType)
            {
                if (type.FullName == GraphViewTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RefreshCustomEventNodeTitle(VisualElement element)
        {
            BlueprintVisualNode node;
            if (!TryGetBlueprintVisualNode(element, out node) || node.TypeId != "Game.Event.Custom")
            {
                return;
            }

            string eventName = ReadCustomEventName(node);
            string title = string.IsNullOrEmpty(eventName) ? "Custom Event" : "Custom Event: " + eventName;
            node.Title = title;

            VisualElement titleElement = FindNamedElement(element, "title");
            if (titleElement == null)
            {
                return;
            }

            SetTitleElementText(titleElement, title);
        }

        private static bool TryGetBlueprintVisualNode(VisualElement element, out BlueprintVisualNode node)
        {
            node = null;
            object model = GetPropertyValue(element, "Model");
            if (model == null)
            {
                return false;
            }

            node = GetPropertyValue(model, "Node") as BlueprintVisualNode;
            return node != null;
        }

        private static string ReadCustomEventName(BlueprintVisualNode node)
        {
            if (node == null || node.Properties == null)
            {
                return null;
            }

            for (int i = 0; i < node.Properties.Count; i++)
            {
                BlueprintVisualPropertyData property = node.Properties[i];
                if (property == null || property.Id != "eventName")
                {
                    continue;
                }

                object value;
                if (node.TryReadPropertyValue(property, out value) && value != null)
                {
                    return value.ToString();
                }
            }

            return null;
        }

        private static VisualElement FindNamedElement(VisualElement element, string name)
        {
            if (element == null)
            {
                return null;
            }

            if (element.name == name)
            {
                return element;
            }

            foreach (VisualElement child in element.Children())
            {
                VisualElement result = FindNamedElement(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void SetTitleElementText(VisualElement titleElement, string title)
        {
            Label label = titleElement as Label;
            if (label != null)
            {
                label.text = title;
                return;
            }

            TextElement textElement = titleElement as TextElement;
            if (textElement != null)
            {
                textElement.text = title;
                return;
            }

            MethodInfo setValueMethod = titleElement.GetType().GetMethod(
                "SetValueWithoutNotify",
                ReflectionFlags,
                null,
                new[] { typeof(string), typeof(bool) },
                null);
            if (setValueMethod != null)
            {
                setValueMethod.Invoke(titleElement, new object[] { title, true });
                return;
            }

            setValueMethod = titleElement.GetType().GetMethod(
                "SetValueWithoutNotify",
                ReflectionFlags,
                null,
                new[] { typeof(string) },
                null);
            if (setValueMethod != null)
            {
                setValueMethod.Invoke(titleElement, new object[] { title });
            }
        }

        private sealed class GraphViewDropHandler
        {
            private readonly VisualElement _graphView;

            public GraphViewDropHandler(VisualElement graphView)
            {
                _graphView = graphView;
                _graphView.RegisterCallback<DragUpdatedEvent>(OnDragUpdated, TrickleDown.TrickleDown);
                _graphView.RegisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);
            }

            private void OnDragUpdated(DragUpdatedEvent evt)
            {
                BlueprintVisualGraph graph;
                if (!TryGetBlueprintGraph(out graph))
                {
                    return;
                }

                List<DropNodeChoice> choices = BuildDropChoices(graph);
                if (choices.Count == 0)
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            }

            private void OnDragPerform(DragPerformEvent evt)
            {
                BlueprintVisualGraph graph;
                if (!TryGetBlueprintGraph(out graph))
                {
                    return;
                }

                List<DropNodeChoice> choices = BuildDropChoices(graph);
                if (choices.Count == 0)
                {
                    return;
                }

                Vector2 graphPosition = GetGraphPosition(evt.mousePosition);
                DragAndDrop.AcceptDrag();
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();

                GenericMenu menu = new GenericMenu();
                for (int i = 0; i < choices.Count; i++)
                {
                    DropNodeChoice choice = choices[i];
                    menu.AddItem(new GUIContent(choice.MenuPath), false, delegate
                    {
                        try
                        {
                            CreateNodeFromChoice(graph, choice, graphPosition);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                        }
                    });
                }

                menu.ShowAsContext();
            }

            private bool TryGetBlueprintGraph(out BlueprintVisualGraph graph)
            {
                graph = null;

                object graphModel = GetPropertyValue(_graphView, "GraphModel");
                if (graphModel == null)
                {
                    return false;
                }

                graph = GetPropertyValue(graphModel, "Graph") as BlueprintVisualGraph;
                return graph != null;
            }

            private Vector2 GetGraphPosition(Vector2 mousePosition)
            {
                VisualElement contentViewContainer = GetPropertyValue(_graphView, "ContentViewContainer") as VisualElement;
                return contentViewContainer == null ? mousePosition : contentViewContainer.WorldToLocal(mousePosition);
            }
        }

        private sealed class DropNodeChoice
        {
            public BlueprintNodeManifest Manifest;
            public string BindingPropertyId;
            public string BindingType;
            public Object BindingTarget;
            public string SuggestedBindingName;
            public IVariable Variable;
            public string VariableName;
            public string BlueprintAssetPath;
            public string BlueprintVariableName;
            public string StructTypeId;
            public string StructAssetGuid;
            public BlueprintUserStructAsset StructAsset;
            public string DataTableAssetPath;
            public string DataTableAssetGuid;
            public string DataTableRowStructTypeId;
            public string DataTableVariableName;
            public BlueprintDataTableAsset DataTableAsset;
            public string MenuPath;

            public bool IsVariable
            {
                get { return Variable != null && !string.IsNullOrEmpty(VariableName); }
            }

            public bool IsBlueprintAsset
            {
                get { return !string.IsNullOrEmpty(BlueprintAssetPath) && !string.IsNullOrEmpty(BlueprintVariableName); }
            }

            public bool IsUserStructAsset
            {
                get { return StructAsset != null && !string.IsNullOrEmpty(StructTypeId); }
            }

            public bool IsDataTableAsset
            {
                get { return DataTableAsset != null && !string.IsNullOrEmpty(DataTableAssetPath); }
            }

            public bool IsDataTableVariable
            {
                get
                {
                    return IsDataTableAsset &&
                           !string.IsNullOrEmpty(DataTableVariableName) &&
                           Manifest != null &&
                           (Manifest.TypeId == "Variable.Get" || Manifest.TypeId == "Variable.Set");
                }
            }
        }

        private static List<DropNodeChoice> BuildDropChoices(BlueprintVisualGraph graph)
        {
            List<DropNodeChoice> choices = new List<DropNodeChoice>();
            AddVariableDropChoices(graph, choices);
            AddBlueprintAssetDropChoices(graph, choices);
            AddDataTableAssetDropChoices(graph, choices);
            AddUserStructAssetDropChoices(choices);
            AddSpriteBindingDropChoices(choices);
            AddBindingDropChoices(graph, choices);
            choices.Sort(CompareChoices);
            return choices;
        }

        private static void AddVariableDropChoices(BlueprintVisualGraph graph, List<DropNodeChoice> choices)
        {
            List<IVariable> variables = GetDraggedBlackboardVariables(graph);
            if (variables.Count == 0)
            {
                return;
            }

            BlueprintNodeManifestCollection manifests = BlueprintGraphToolkitBridge.LoadProjectManifests();
            for (int i = 0; i < variables.Count; i++)
            {
                IVariable variable = variables[i];
                string blueprintType;
                if (!BlueprintGraphToolkitBlackboardSync.TryGetBlueprintType(graph, variable, out blueprintType))
                {
                    continue;
                }

                AddVariableDropChoice(manifests, "Variable.Get", variable, choices);
                AddVariableDropChoice(manifests, "Variable.Set", variable, choices);
            }
        }

        private static void AddVariableDropChoice(
            BlueprintNodeManifestCollection manifests,
            string typeId,
            IVariable variable,
            List<DropNodeChoice> choices)
        {
            BlueprintNodeManifest manifest;
            if (!manifests.TryGet(typeId, out manifest))
            {
                return;
            }

            choices.Add(new DropNodeChoice
            {
                Manifest = manifest,
                Variable = variable,
                VariableName = variable.name,
                MenuPath = CreateVariableMenuPath(manifest, variable.name)
            });
        }

        private static void AddSpriteBindingDropChoices(List<DropNodeChoice> choices)
        {
            Object[] draggedObjects = DragAndDrop.objectReferences;
            List<Sprite> sprites = ResolveSpriteAssets(draggedObjects);
            if (sprites.Count == 0)
            {
                return;
            }

            BlueprintNodeManifest manifest;
            if (!BlueprintGraphToolkitBridge.LoadProjectManifests().TryGet("UI.SpriteBinding", out manifest))
            {
                return;
            }

            for (int i = 0; i < sprites.Count; i++)
            {
                Sprite sprite = sprites[i];
                choices.Add(new DropNodeChoice
                {
                    Manifest = manifest,
                    BindingPropertyId = "sprite",
                    BindingType = "Sprite",
                    BindingTarget = sprite,
                    SuggestedBindingName = sprite.name,
                    MenuPath = CreateMenuPath(manifest, "Sprite", sprite.name)
                });
            }
        }

        private static void AddBlueprintAssetDropChoices(BlueprintVisualGraph graph, List<DropNodeChoice> choices)
        {
            List<string> blueprintPaths = ResolveBlueprintAssetPaths(DragAndDrop.objectReferences);
            if (blueprintPaths.Count == 0)
            {
                return;
            }

            BlueprintNodeManifestCollection manifests = BlueprintGraphToolkitBridge.LoadProjectManifests();
            BlueprintNodeManifest getManifest;
            BlueprintNodeManifest setManifest;
            manifests.TryGet("Variable.Get", out getManifest);
            manifests.TryGet("Variable.Set", out setManifest);

            HashSet<string> reservedNames = CollectVariableNames(graph);
            for (int i = 0; i < blueprintPaths.Count; i++)
            {
                string blueprintPath = blueprintPaths[i];
                string variableName = CreateUniqueVariableName(GetBlueprintVariableBaseName(blueprintPath), reservedNames);

                if (getManifest != null)
                {
                    choices.Add(new DropNodeChoice
                    {
                        Manifest = getManifest,
                        BlueprintAssetPath = blueprintPath,
                        BlueprintVariableName = variableName,
                        MenuPath = "Variables/Get " + variableName + " (Blueprint)"
                    });
                }

                if (setManifest != null)
                {
                    choices.Add(new DropNodeChoice
                    {
                        Manifest = setManifest,
                        BlueprintAssetPath = blueprintPath,
                        BlueprintVariableName = variableName,
                        MenuPath = "Variables/Set " + variableName + " (Blueprint)"
                    });
                }
            }
        }

        private static void AddUserStructAssetDropChoices(List<DropNodeChoice> choices)
        {
            List<BlueprintUserStructAsset> structAssets = ResolveUserStructAssets(DragAndDrop.objectReferences);
            if (structAssets.Count == 0)
            {
                return;
            }

            BlueprintNodeManifest manifest;
            if (!BlueprintGraphToolkitBridge.LoadProjectManifests().TryGet(BlueprintBreakStructNodeUtility.NodeTypeId, out manifest))
            {
                return;
            }

            for (int i = 0; i < structAssets.Count; i++)
            {
                BlueprintUserStructAsset asset = structAssets[i];
                string assetPath = AssetDatabase.GetAssetPath(asset);
                choices.Add(new DropNodeChoice
                {
                    Manifest = manifest,
                    StructAsset = asset,
                    StructTypeId = asset.TypeId,
                    StructAssetGuid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath),
                    MenuPath = "Variables/Break " + asset.TypeId
                });
            }
        }

        private static void AddDataTableAssetDropChoices(BlueprintVisualGraph graph, List<DropNodeChoice> choices)
        {
            List<BlueprintDataTableAsset> tableAssets = ResolveDataTableAssets(DragAndDrop.objectReferences);
            if (tableAssets.Count == 0)
            {
                return;
            }

            BlueprintNodeManifestCollection manifests = BlueprintGraphToolkitBridge.LoadProjectManifests();
            BlueprintNodeManifest getRowManifest;
            BlueprintNodeManifest getRowNamesManifest;
            BlueprintNodeManifest getAllRowsManifest;
            BlueprintNodeManifest variableGetManifest;
            BlueprintNodeManifest variableSetManifest;
            manifests.TryGet(BlueprintDataTableNodeUtility.GetRowNodeTypeId, out getRowManifest);
            manifests.TryGet(BlueprintDataTableNodeUtility.GetRowNamesNodeTypeId, out getRowNamesManifest);
            manifests.TryGet(BlueprintDataTableNodeUtility.GetAllRowsNodeTypeId, out getAllRowsManifest);
            manifests.TryGet("Variable.Get", out variableGetManifest);
            manifests.TryGet("Variable.Set", out variableSetManifest);

            HashSet<string> reservedNames = CollectVariableNames(graph);
            for (int i = 0; i < tableAssets.Count; i++)
            {
                BlueprintDataTableAsset asset = tableAssets[i];
                string assetPath = AssetDatabase.GetAssetPath(asset);
                string tablePath = BlueprintDataTableRegistry.GetJsonPathForAssetPath(assetPath);
                string guid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
                string variableType = BlueprintDataTableVariableTypeUtility.MakeType(asset.RowStructTypeId);
                IVariable existingVariable;
                string variableName = TryFindDataTableAssetVariable(graph, tablePath, variableType, out existingVariable)
                    ? existingVariable.name
                    : CreateUniqueVariableName(GetDataTableVariableBaseName(asset), reservedNames);

                AddDataTableChoice(choices, getRowManifest, asset, tablePath, guid, "DataTable/Get Row " + asset.TableId);
                AddDataTableChoice(choices, getRowNamesManifest, asset, tablePath, guid, "DataTable/Get Row Names " + asset.TableId);
                AddDataTableChoice(choices, getAllRowsManifest, asset, tablePath, guid, "DataTable/Get All Rows " + asset.TableId);
                AddDataTableVariableChoice(
                    choices,
                    variableGetManifest,
                    asset,
                    tablePath,
                    guid,
                    variableName,
                    "Variables/Get " + variableName + " (DataTable)");
                AddDataTableVariableChoice(
                    choices,
                    variableSetManifest,
                    asset,
                    tablePath,
                    guid,
                    variableName,
                    "Variables/Set " + variableName + " (DataTable)");
            }
        }

        private static void AddDataTableVariableChoice(
            List<DropNodeChoice> choices,
            BlueprintNodeManifest manifest,
            BlueprintDataTableAsset asset,
            string assetPath,
            string guid,
            string variableName,
            string menuPath)
        {
            if (manifest == null || asset == null || string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            choices.Add(new DropNodeChoice
            {
                Manifest = manifest,
                DataTableAsset = asset,
                DataTableAssetPath = assetPath,
                DataTableAssetGuid = guid,
                DataTableRowStructTypeId = asset.RowStructTypeId,
                DataTableVariableName = variableName,
                MenuPath = menuPath
            });
        }

        private static void AddDataTableChoice(
            List<DropNodeChoice> choices,
            BlueprintNodeManifest manifest,
            BlueprintDataTableAsset asset,
            string assetPath,
            string guid,
            string menuPath)
        {
            if (manifest == null || asset == null || string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            choices.Add(new DropNodeChoice
            {
                Manifest = manifest,
                DataTableAsset = asset,
                DataTableAssetPath = assetPath,
                DataTableAssetGuid = guid,
                DataTableRowStructTypeId = asset.RowStructTypeId,
                MenuPath = menuPath
            });
        }

        private static void AddBindingDropChoices(BlueprintVisualGraph graph, List<DropNodeChoice> choices)
        {
            Object[] draggedObjects = DragAndDrop.objectReferences;
            if (draggedObjects == null || draggedObjects.Length == 0)
            {
                return;
            }

            BlueprintNodeManifestCollection manifests = BlueprintGraphToolkitBridge.LoadProjectManifests();
            foreach (KeyValuePair<string, BlueprintNodeManifest> pair in manifests.ManifestsByTypeId)
            {
                BlueprintNodeManifest manifest = pair.Value;
                for (int propertyIndex = 0; propertyIndex < manifest.Properties.Count; propertyIndex++)
                {
                    BlueprintPropertySpec property = manifest.Properties[propertyIndex];
                    string bindingType;
                    if (!TryGetBindingType(property.Type, out bindingType))
                    {
                        continue;
                    }

                    if (bindingType == "Sprite")
                    {
                        continue;
                    }

                    for (int objectIndex = 0; objectIndex < draggedObjects.Length; objectIndex++)
                    {
                        Object bindingTarget;
                        string suggestedName;
                        if (!TryResolveBindingTarget(draggedObjects[objectIndex], bindingType, out bindingTarget, out suggestedName))
                        {
                            continue;
                        }

                        choices.Add(new DropNodeChoice
                        {
                            Manifest = manifest,
                            BindingPropertyId = property.Id,
                            BindingType = bindingType,
                            BindingTarget = bindingTarget,
                            SuggestedBindingName = suggestedName,
                            MenuPath = CreateMenuPath(manifest, bindingType, suggestedName)
                        });
                        break;
                    }
                }
            }
        }

        private static int CompareChoices(DropNodeChoice left, DropNodeChoice right)
        {
            int category = string.Compare(left.Manifest.Category, right.Manifest.Category, StringComparison.OrdinalIgnoreCase);
            if (category != 0)
            {
                return category;
            }

            return string.Compare(left.Manifest.Title, right.Manifest.Title, StringComparison.OrdinalIgnoreCase);
        }

        private static List<IVariable> GetDraggedBlackboardVariables(BlueprintVisualGraph graph)
        {
            List<IVariable> result = new List<IVariable>();
            if (graph == null)
            {
                return result;
            }

            object dragData = DragAndDrop.GetGenericData(GraphToolkitSelectionDragKey);
            IEnumerable draggedItems = dragData as IEnumerable;
            if (draggedItems == null)
            {
                return result;
            }

            HashSet<string> addedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (object draggedItem in draggedItems)
            {
                IVariable draggedVariable = draggedItem as IVariable;
                IVariable graphVariable;
                if (draggedVariable != null &&
                    TryFindGraphVariable(graph, draggedVariable, out graphVariable) &&
                    addedNames.Add(graphVariable.name))
                {
                    result.Add(graphVariable);
                }
            }

            return result;
        }

        private static bool TryFindGraphVariable(BlueprintVisualGraph graph, IVariable draggedVariable, out IVariable graphVariable)
        {
            graphVariable = null;
            if (graph == null || draggedVariable == null || string.IsNullOrEmpty(draggedVariable.name))
            {
                return false;
            }

            foreach (IVariable candidate in graph.GetVariables())
            {
                if (candidate == null)
                {
                    continue;
                }

                if (object.ReferenceEquals(candidate, draggedVariable) || candidate.name == draggedVariable.name)
                {
                    graphVariable = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string CreateMenuPath(BlueprintNodeManifest manifest, string bindingType, string suggestedName)
        {
            string category = string.IsNullOrEmpty(manifest.Category) ? "Nodes" : manifest.Category;
            string title = string.IsNullOrEmpty(manifest.Title) ? manifest.TypeId : manifest.Title;
            string objectName = string.IsNullOrEmpty(suggestedName) ? bindingType : suggestedName;
            return category + "/" + title + " -> " + objectName + " (" + bindingType + ")";
        }

        private static string CreateVariableMenuPath(BlueprintNodeManifest manifest, string variableName)
        {
            string category = string.IsNullOrEmpty(manifest.Category) ? "Variables" : manifest.Category;
            string title = string.IsNullOrEmpty(manifest.Title) ? manifest.TypeId : manifest.Title;
            if (manifest.TypeId == "Variable.Get")
            {
                title = "Get " + variableName;
            }
            else if (manifest.TypeId == "Variable.Set")
            {
                title = "Set " + variableName;
            }

            return category + "/" + title;
        }

        private static void CreateNodeFromChoice(BlueprintVisualGraph graph, DropNodeChoice choice, Vector2 graphPosition)
        {
            if (choice.IsVariable)
            {
                CreateVariableNodeFromChoice(graph, choice, graphPosition);
                return;
            }

            if (choice.IsBlueprintAsset)
            {
                CreateBlueprintAssetNodeFromChoice(graph, choice, graphPosition);
                return;
            }

            if (choice.IsUserStructAsset)
            {
                CreateBreakStructNodeFromChoice(graph, choice, graphPosition);
                return;
            }

            if (choice.IsDataTableVariable)
            {
                CreateDataTableVariableNodeFromChoice(graph, choice, graphPosition);
                return;
            }

            if (choice.IsDataTableAsset)
            {
                CreateDataTableNodeFromChoice(graph, choice, graphPosition);
                return;
            }

            CreateBindingNodeFromChoice(graph, choice, graphPosition);
        }

        private static void CreateBindingNodeFromChoice(BlueprintVisualGraph graph, DropNodeChoice choice, Vector2 graphPosition)
        {
            BlueprintRunner runner = FindBindingOwnerRunner(graph, choice.BindingTarget);
            string existingBindingName = FindExistingBindingName(runner, choice.BindingTarget);
            string bindingName = string.IsNullOrEmpty(existingBindingName)
                ? CreateUniqueBindingName(choice.SuggestedBindingName, graph, runner)
                : existingBindingName;

            EnsureGraphBinding(graph, bindingName, choice.BindingType, true);
            bool runnerUpdated = EnsureRunnerBinding(runner, bindingName, choice.BindingTarget);

            BlueprintNodeSource source = new BlueprintNodeSource
            {
                Id = CreateUniqueNodeId(choice.Manifest.TypeId, bindingName, graph),
                TypeId = choice.Manifest.TypeId,
                X = graphPosition.x,
                Y = graphPosition.y
            };

            source.Properties[choice.BindingPropertyId] = bindingName;
            ApplyContextDefaults(graph, runner, source, choice, bindingName);

            BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(source, choice.Manifest);
            BlueprintGraphToolkitReflection.CreateNodeWithUndo(graph, visualNode, graphPosition, "Create Blueprint Node");
            BlueprintGraphToolkitReflection.MarkDirty(graph);
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.SaveAssets();

            if (!runnerUpdated)
            {
                Debug.LogWarning(CreateMissingRunnerMessage(choice, bindingName));
            }
            else
            {
                Debug.Log("[Blueprint] Added '" + choice.Manifest.TypeId + "' and bound '" + bindingName + "' to '" + choice.BindingTarget.name + "'.");
            }
        }

        private static string CreateMissingRunnerMessage(DropNodeChoice choice, string bindingName)
        {
            string targetName = choice.BindingTarget == null ? bindingName : choice.BindingTarget.name;
            if (choice.BindingTarget is Sprite)
            {
                return "[Blueprint] Added '" + choice.Manifest.TypeId + "' with Sprite binding '" + bindingName +
                    "', but no unique BlueprintRunner was found for the current graph. Add '" + targetName +
                    "' to a BlueprintRunner before running the blueprint.";
            }

            return "[Blueprint] Added '" + choice.Manifest.TypeId + "' with binding '" + bindingName +
                "', but no parent BlueprintRunner was found for '" + targetName +
                "'. Add that binding to a BlueprintRunner before running the blueprint.";
        }

        private static void CreateVariableNodeFromChoice(BlueprintVisualGraph graph, DropNodeChoice choice, Vector2 graphPosition)
        {
            if (choice.Manifest.TypeId == "Variable.Get")
            {
                BlueprintGraphToolkitReflection.CreateBlackboardVariableNodeWithUndo(
                    graph,
                    choice.Variable,
                    graphPosition,
                    "Create Blueprint Get Variable Node");
                BlueprintGraphToolkitReflection.MarkDirty(graph);
                GraphDatabase.SaveGraphIfDirty(graph);
                AssetDatabase.SaveAssets();
                ClearVariableDragData();
                Debug.Log("[Blueprint] Added 'Variable.Get' for variable '" + choice.VariableName + "'.");
                return;
            }

            CreateVariableSetNodeFromBlackboard(graph, choice.Variable, graphPosition);
            ClearVariableDragData();
        }

        private static void CreateBlueprintAssetNodeFromChoice(BlueprintVisualGraph graph, DropNodeChoice choice, Vector2 graphPosition)
        {
            IVariable variable = EnsureBlueprintAssetVariable(graph, choice.BlueprintVariableName, choice.BlueprintAssetPath);
            if (choice.Manifest.TypeId == "Variable.Get")
            {
                BlueprintGraphToolkitReflection.CreateBlackboardVariableNodeWithUndo(
                    graph,
                    variable,
                    graphPosition,
                    "Create Blueprint Get Variable Node");
                BlueprintGraphToolkitReflection.MarkDirty(graph);
                GraphDatabase.SaveGraphIfDirty(graph);
                AssetDatabase.SaveAssets();
                Debug.Log("[Blueprint] Added Blueprint variable '" + variable.name + "' from '" + choice.BlueprintAssetPath + "'.");
                return;
            }

            CreateVariableSetNodeFromBlackboard(graph, variable, graphPosition);
        }

        private static void CreateBreakStructNodeFromChoice(BlueprintVisualGraph graph, DropNodeChoice choice, Vector2 graphPosition)
        {
            BlueprintNodeSource source = CreateBreakStructNodeSource(graph, choice.StructTypeId, choice.StructAssetGuid, graphPosition);
            BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(source, choice.Manifest, ConvertGraphVariables(graph));
            BlueprintGraphToolkitReflection.CreateNodeWithUndo(graph, visualNode, graphPosition, "Create Blueprint Break Struct Node");
            BlueprintGraphToolkitReflection.MarkDirty(graph);
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log("[Blueprint] Added 'Variable.BreakStruct' for '" + choice.StructTypeId + "'.");
        }

        private static void CreateDataTableNodeFromChoice(BlueprintVisualGraph graph, DropNodeChoice choice, Vector2 graphPosition)
        {
            BlueprintNodeSource source = CreateDataTableNodeSource(
                graph,
                choice.Manifest.TypeId,
                choice.DataTableAssetPath,
                choice.DataTableAssetGuid,
                choice.DataTableRowStructTypeId,
                graphPosition);
            BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(source, choice.Manifest, ConvertGraphVariables(graph));
            BlueprintGraphToolkitReflection.CreateNodeWithUndo(graph, visualNode, graphPosition, "Create Blueprint Data Table Node");
            BlueprintGraphToolkitReflection.MarkDirty(graph);
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log("[Blueprint] Added '" + choice.Manifest.TypeId + "' for '" + choice.DataTableAssetPath + "'.");
        }

        private static void CreateDataTableVariableNodeFromChoice(
            BlueprintVisualGraph graph,
            DropNodeChoice choice,
            Vector2 graphPosition)
        {
            IVariable variable = EnsureDataTableAssetVariable(
                graph,
                choice.DataTableVariableName,
                choice.DataTableAssetPath,
                choice.DataTableRowStructTypeId);
            if (choice.Manifest.TypeId == "Variable.Get")
            {
                BlueprintGraphToolkitReflection.CreateBlackboardVariableNodeWithUndo(
                    graph,
                    variable,
                    graphPosition,
                    "Create Blueprint Data Table Get Variable Node");
                BlueprintGraphToolkitReflection.MarkDirty(graph);
                GraphDatabase.SaveGraphIfDirty(graph);
                AssetDatabase.SaveAssets();
                Debug.Log("[Blueprint] Added DataTable variable '" + variable.name + "' from '" + choice.DataTableAssetPath + "'.");
                return;
            }

            CreateVariableSetNodeFromBlackboard(graph, variable, graphPosition);
        }

        internal static BlueprintNodeSource CreateBreakStructNodeSource(
            BlueprintVisualGraph graph,
            string structTypeId,
            string structAssetGuid,
            Vector2 graphPosition)
        {
            if (graph == null)
            {
                throw new ArgumentNullException("graph");
            }

            if (string.IsNullOrEmpty(structTypeId))
            {
                throw new ArgumentException("Struct type id is required.", "structTypeId");
            }

            BlueprintNodeSource source = new BlueprintNodeSource
            {
                Id = CreateUniqueNodeId(BlueprintBreakStructNodeUtility.NodeTypeId, structTypeId, graph),
                TypeId = BlueprintBreakStructNodeUtility.NodeTypeId,
                X = graphPosition.x,
                Y = graphPosition.y
            };

            source.Properties[BlueprintBreakStructNodeUtility.StructTypePropertyId] = structTypeId;
            if (!string.IsNullOrEmpty(structAssetGuid))
            {
                source.Properties[BlueprintBreakStructNodeUtility.StructAssetGuidPropertyId] = structAssetGuid;
            }

            return source;
        }

        internal static BlueprintNodeSource CreateDataTableNodeSource(
            BlueprintVisualGraph graph,
            string nodeTypeId,
            string tablePath,
            string tableAssetGuid,
            string rowStructTypeId,
            Vector2 graphPosition)
        {
            if (graph == null)
            {
                throw new ArgumentNullException("graph");
            }

            if (!BlueprintDataTableNodeUtility.IsDataTableNode(nodeTypeId))
            {
                throw new ArgumentException("Expected a DataTable node type id.", "nodeTypeId");
            }

            if (string.IsNullOrEmpty(tablePath))
            {
                throw new ArgumentException("Data table asset path is required.", "tablePath");
            }

            BlueprintNodeSource source = new BlueprintNodeSource
            {
                Id = CreateUniqueNodeId(nodeTypeId, tablePath, graph),
                TypeId = nodeTypeId,
                X = graphPosition.x,
                Y = graphPosition.y
            };

            source.Properties[BlueprintDataTableNodeUtility.TablePathPropertyId] = tablePath;
            source.Properties[BlueprintDataTableNodeUtility.DataTableInputId] = tablePath;
            source.Properties[BlueprintDataTableNodeUtility.RowStructTypePropertyId] = rowStructTypeId;
            if (!string.IsNullOrEmpty(tableAssetGuid))
            {
                source.Properties[BlueprintDataTableNodeUtility.TableAssetGuidPropertyId] = tableAssetGuid;
            }

            return source;
        }

        internal static IVariable EnsureDataTableAssetVariable(
            BlueprintVisualGraph graph,
            string suggestedName,
            string tablePath,
            string rowStructTypeId)
        {
            if (graph == null)
            {
                throw new ArgumentNullException("graph");
            }

            string normalizedPath = BlueprintAssetDiscovery.NormalizeAssetPath(tablePath);
            string blueprintType = BlueprintDataTableVariableTypeUtility.MakeType(rowStructTypeId);
            if (!BlueprintDataTableVariableTypeUtility.IsSupportedType(blueprintType))
            {
                throw new ArgumentException("Expected a supported DataTable row struct type.", "rowStructTypeId");
            }

            BlueprintDataTableDefinition definition;
            if (!BlueprintDataTableRegistry.TryGetByPath(normalizedPath, out definition) ||
                definition == null ||
                definition.RowStructTypeId != rowStructTypeId)
            {
                throw new ArgumentException("Expected a DataTable path with row type '" + rowStructTypeId + "'.", "tablePath");
            }

            IVariable existingVariable;
            if (TryFindDataTableAssetVariable(graph, normalizedPath, blueprintType, out existingVariable))
            {
                return existingVariable;
            }

            HashSet<string> usedNames = CollectVariableNames(graph);
            string variableName = CreateUniqueVariableName(suggestedName, usedNames);
            IVariable variable = BlueprintGraphToolkitReflection.CreateBlackboardVariable(
                graph,
                variableName,
                typeof(DataTable),
                new DataTable(
                    rowStructTypeId,
                    normalizedPath,
                    GetDataTableAssetGuid(normalizedPath)));

            EnsureGraphVariableMetadata(graph, variableName, normalizedPath, blueprintType);
            BlueprintGraphToolkitReflection.MarkDirty(graph);
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.SaveAssets();
            return variable;
        }

        private static bool TryFindDataTableAssetVariable(
            BlueprintVisualGraph graph,
            string tablePath,
            string blueprintType,
            out IVariable variable)
        {
            variable = null;
            foreach (IVariable candidate in graph.GetVariables())
            {
                if (candidate == null || candidate.dataType != typeof(DataTable))
                {
                    continue;
                }

                string candidateType;
                object defaultValue;
                if (BlueprintGraphToolkitBlackboardSync.TryGetBlueprintType(graph, candidate, out candidateType) &&
                    candidateType == blueprintType &&
                    BlueprintGraphToolkitBlackboardSync.TryReadDefaultValue(candidate, candidateType, out defaultValue) &&
                    BlueprintAssetDiscovery.NormalizeAssetPath(defaultValue as string) == tablePath)
                {
                    variable = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string GetDataTableAssetGuid(string tablePath)
        {
            BlueprintDataTableAsset asset = BlueprintGraphToolkitDataTableTypes.LoadAsset(tablePath);
            return asset == null
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        }

        internal static IVariable EnsureBlueprintAssetVariable(BlueprintVisualGraph graph, string suggestedName, string blueprintAssetPath)
        {
            if (graph == null)
            {
                throw new ArgumentNullException("graph");
            }

            string normalizedPath = BlueprintGraphToolkitBlueprintTypes.NormalizePath(blueprintAssetPath);
            if (!BlueprintGraphToolkitBlueprintTypes.IsBlueprintJsonPath(normalizedPath))
            {
                throw new ArgumentException("Expected a .blueprint.json asset path.", "blueprintAssetPath");
            }

            IVariable existingVariable;
            if (TryFindBlueprintAssetVariable(graph, normalizedPath, out existingVariable))
            {
                return existingVariable;
            }

            HashSet<string> usedNames = CollectVariableNames(graph);
            string variableName = CreateUniqueVariableName(suggestedName, usedNames);
            IVariable variable = BlueprintGraphToolkitReflection.CreateBlackboardVariable(
                graph,
                variableName,
                typeof(Blueprint),
                new Blueprint(normalizedPath));

            EnsureGraphVariableMetadata(graph, variableName, normalizedPath);
            BlueprintGraphToolkitReflection.MarkDirty(graph);
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.SaveAssets();
            return variable;
        }

        private static bool TryFindBlueprintAssetVariable(BlueprintVisualGraph graph, string blueprintAssetPath, out IVariable variable)
        {
            variable = null;
            if (graph == null || string.IsNullOrEmpty(blueprintAssetPath))
            {
                return false;
            }

            foreach (IVariable candidate in graph.GetVariables())
            {
                if (candidate == null)
                {
                    continue;
                }

                string blueprintType;
                object defaultValue;
                if (BlueprintGraphToolkitBlackboardSync.TryGetBlueprintType(graph, candidate, out blueprintType) &&
                    blueprintType == BlueprintGraphToolkitBlueprintTypes.TypeId &&
                    BlueprintGraphToolkitBlackboardSync.TryReadDefaultValue(candidate, blueprintType, out defaultValue) &&
                    BlueprintGraphToolkitBlueprintTypes.NormalizePath(defaultValue as string) == blueprintAssetPath)
                {
                    variable = candidate;
                    return true;
                }
            }

            return false;
        }

        internal static BlueprintVisualNode CreateVariableSetNodeFromBlackboard(BlueprintVisualGraph graph, IVariable variable, Vector2 graphPosition)
        {
            if (graph == null)
            {
                throw new ArgumentNullException("graph");
            }

            if (variable == null)
            {
                throw new ArgumentNullException("variable");
            }

            string blueprintType;
            if (!BlueprintGraphToolkitBlackboardSync.TryGetBlueprintType(graph, variable, out blueprintType))
            {
                throw new InvalidOperationException("Variable '" + variable.name + "' uses unsupported type '" + variable.dataType + "'.");
            }

            BlueprintNodeManifest manifest;
            BlueprintNodeManifestCollection manifests = BlueprintGraphToolkitBridge.LoadProjectManifests();
            if (!manifests.TryGet("Variable.Set", out manifest))
            {
                throw new InvalidOperationException("Variable.Set manifest is missing.");
            }

            BlueprintNodeSource source = new BlueprintNodeSource
            {
                Id = CreateUniqueNodeId("Variable.Set", variable.name, graph),
                TypeId = "Variable.Set",
                X = graphPosition.x,
                Y = graphPosition.y
            };

            source.Properties["name"] = variable.name;
            source.Properties["value"] = GetVariableSetInitialValue(variable, blueprintType);

            BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(source, manifest, ConvertGraphVariables(graph));
            BlueprintGraphToolkitReflection.CreateNodeWithUndo(graph, visualNode, graphPosition, "Create Blueprint Variable Node");
            BlueprintGraphToolkitReflection.MarkDirty(graph);
            GraphDatabase.SaveGraphIfDirty(graph);
            AssetDatabase.SaveAssets();
            Debug.Log("[Blueprint] Added 'Variable.Set' for variable '" + variable.name + "'.");
            return visualNode;
        }

        private static object GetVariableSetInitialValue(IVariable variable, string blueprintType)
        {
            object value;
            if (BlueprintGraphToolkitBlackboardSync.TryReadDefaultValue(variable, blueprintType, out value))
            {
                return value;
            }

            if (BlueprintDataTableVariableTypeUtility.IsDataTableType(blueprintType))
            {
                return null;
            }

            switch (blueprintType)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    return string.Empty;
                case "bool":
                    return false;
                case "int":
                    return 0;
                case "float":
                    return 0f;
                case "Vector2":
                    return new List<object> { 0f, 0f };
                case "Vector3":
                    return new List<object> { 0f, 0f, 0f };
                case "Vector4":
                    return new List<object> { 0f, 0f, 0f, 0f };
                case "Rect":
                    return new List<object> { 0f, 0f, 0f, 0f };
                case "Color":
                    return new List<object> { 1f, 1f, 1f, 1f };
                default:
                    if (BlueprintArrayUtility.IsArrayType(blueprintType))
                    {
                        return new List<object>();
                    }

                    Type graphType;
                    if (BlueprintGraphToolkitTypeRegistry.TryGetGraphType(blueprintType, out graphType) && graphType.IsEnum)
                    {
                        return Activator.CreateInstance(graphType).ToString();
                    }

                    object structuredDefaultValue;
                    if (BlueprintStructuredValueUtility.TryCreateDefaultJsonValue(blueprintType, out structuredDefaultValue))
                    {
                        return structuredDefaultValue;
                    }

                    return null;
            }
        }

        private static void ClearVariableDragData()
        {
            DragAndDrop.SetGenericData(GraphToolkitSelectionDragKey, null);
        }

        private static List<BlueprintVariableDeclaration> ConvertGraphVariables(BlueprintVisualGraph graph)
        {
            return BlueprintGraphToolkitBlackboardSync.ExtractSourceVariables(graph);
        }

        private static void ApplyContextDefaults(BlueprintVisualGraph graph, BlueprintRunner runner, BlueprintNodeSource source, DropNodeChoice choice, string bindingName)
        {
            if (choice.Manifest.TypeId == "UI.SetText")
            {
                TMP_Text text = choice.BindingTarget as TMP_Text;
                source.Properties["value"] = text == null ? string.Empty : text.text;
            }
            else if (choice.Manifest.TypeId == "UI.SetVisible")
            {
                GameObject gameObject = choice.BindingTarget as GameObject;
                Component component = choice.BindingTarget as Component;
                source.Properties["value"] = gameObject != null ? gameObject.activeSelf : component != null && component.gameObject.activeSelf;
            }
            else if (choice.Manifest.TypeId == "UI.SetInteractable")
            {
                UISelectable selectable = choice.BindingTarget as UISelectable;
                source.Properties["value"] = selectable == null || selectable.interactable;
            }
            else if (choice.Manifest.TypeId == "UI.SetImageSprite" && choice.BindingPropertyId == "target")
            {
                UIImage image = choice.BindingTarget as UIImage;
                if (image != null && image.sprite != null)
                {
                    string existingSpriteBindingName = FindExistingBindingName(runner, image.sprite);
                    string spriteBindingName = string.IsNullOrEmpty(existingSpriteBindingName)
                        ? CreateUniqueBindingName(image.sprite.name, graph, runner)
                        : existingSpriteBindingName;

                    EnsureGraphBinding(graph, spriteBindingName, "Sprite", true);
                    EnsureRunnerBinding(runner, spriteBindingName, image.sprite);
                    source.Properties["value"] = spriteBindingName;
                }
            }
        }

        private static bool TryGetBindingType(string propertyType, out string bindingType)
        {
            const string prefix = "Binding<";
            bindingType = null;
            if (string.IsNullOrEmpty(propertyType) || !propertyType.StartsWith(prefix, StringComparison.Ordinal) || !propertyType.EndsWith(">", StringComparison.Ordinal))
            {
                return false;
            }

            bindingType = propertyType.Substring(prefix.Length, propertyType.Length - prefix.Length - 1).Trim();
            return !string.IsNullOrEmpty(bindingType);
        }

        internal static List<Sprite> ResolveSpriteAssets(Object[] draggedObjects)
        {
            List<Sprite> sprites = new List<Sprite>();
            if (draggedObjects == null || draggedObjects.Length == 0)
            {
                return sprites;
            }

            HashSet<int> addedInstanceIds = new HashSet<int>();
            for (int i = 0; i < draggedObjects.Length; i++)
            {
                Object draggedObject = draggedObjects[i];
                Sprite directSprite = draggedObject as Sprite;
                if (directSprite != null)
                {
                    AddSpriteIfNeeded(directSprite, sprites, addedInstanceIds);
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    AddSpritesAtPath(assetPath, sprites, addedInstanceIds);
                }
            }

            return sprites;
        }

        internal static List<string> ResolveBlueprintAssetPaths(Object[] draggedObjects)
        {
            List<string> paths = new List<string>();
            if (draggedObjects == null || draggedObjects.Length == 0)
            {
                return paths;
            }

            HashSet<string> addedPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < draggedObjects.Length; i++)
            {
                string path = BlueprintGraphToolkitBlueprintTypes.GetBlueprintAssetPath(draggedObjects[i]);
                if (!string.IsNullOrEmpty(path) && addedPaths.Add(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        internal static List<BlueprintUserStructAsset> ResolveUserStructAssets(Object[] draggedObjects)
        {
            List<BlueprintUserStructAsset> assets = new List<BlueprintUserStructAsset>();
            if (draggedObjects == null || draggedObjects.Length == 0)
            {
                return assets;
            }

            HashSet<int> addedInstanceIds = new HashSet<int>();
            for (int i = 0; i < draggedObjects.Length; i++)
            {
                BlueprintUserStructAsset asset = draggedObjects[i] as BlueprintUserStructAsset;
                if (asset != null && addedInstanceIds.Add(asset.GetInstanceID()))
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        internal static List<BlueprintDataTableAsset> ResolveDataTableAssets(Object[] draggedObjects)
        {
            List<BlueprintDataTableAsset> assets = new List<BlueprintDataTableAsset>();
            if (draggedObjects == null || draggedObjects.Length == 0)
            {
                return assets;
            }

            HashSet<int> addedInstanceIds = new HashSet<int>();
            for (int i = 0; i < draggedObjects.Length; i++)
            {
                BlueprintDataTableAsset asset = draggedObjects[i] as BlueprintDataTableAsset;
                if (asset != null && addedInstanceIds.Add(asset.GetInstanceID()))
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        private static void AddSpritesAtPath(string assetPath, List<Sprite> sprites, HashSet<int> addedInstanceIds)
        {
            Sprite mainSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            AddSpriteIfNeeded(mainSprite, sprites, addedInstanceIds);

            Object[] representations = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            for (int i = 0; i < representations.Length; i++)
            {
                AddSpriteIfNeeded(representations[i] as Sprite, sprites, addedInstanceIds);
            }

            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < allAssets.Length; i++)
            {
                AddSpriteIfNeeded(allAssets[i] as Sprite, sprites, addedInstanceIds);
            }
        }

        private static void AddSpriteIfNeeded(Sprite sprite, List<Sprite> sprites, HashSet<int> addedInstanceIds)
        {
            if (sprite == null)
            {
                return;
            }

            int instanceId = sprite.GetInstanceID();
            if (addedInstanceIds.Add(instanceId))
            {
                sprites.Add(sprite);
            }
        }

        private static bool TryResolveBindingTarget(Object draggedObject, string bindingType, out Object bindingTarget, out string suggestedName)
        {
            bindingTarget = null;
            suggestedName = null;

            Type requiredType = ResolveUnityObjectType(bindingType);
            if (requiredType == null)
            {
                return false;
            }

            if (draggedObject != null && requiredType.IsInstanceOfType(draggedObject))
            {
                bindingTarget = draggedObject;
                suggestedName = draggedObject.name;
                return true;
            }

            GameObject gameObject = draggedObject as GameObject;
            Component draggedComponent = draggedObject as Component;
            if (gameObject == null && draggedComponent != null)
            {
                gameObject = draggedComponent.gameObject;
            }

            if (gameObject == null)
            {
                return false;
            }

            suggestedName = gameObject.name;
            if (requiredType == typeof(GameObject))
            {
                bindingTarget = gameObject;
                return true;
            }

            if (typeof(Component).IsAssignableFrom(requiredType))
            {
                if (draggedComponent != null && requiredType.IsInstanceOfType(draggedComponent))
                {
                    bindingTarget = draggedComponent;
                    return true;
                }

                Component component = gameObject.GetComponent(requiredType);
                if (component != null)
                {
                    bindingTarget = component;
                    return true;
                }
            }

            if (requiredType == typeof(Object))
            {
                bindingTarget = draggedObject;
                return true;
            }

            return false;
        }

        private static Type ResolveUnityObjectType(string bindingType)
        {
            if (string.IsNullOrEmpty(bindingType))
            {
                return null;
            }

            string shortName = bindingType;
            int dotIndex = shortName.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < shortName.Length - 1)
            {
                shortName = shortName.Substring(dotIndex + 1);
            }

            switch (shortName)
            {
                case "TMP_Text":
                    return typeof(TMP_Text);
                case "Text":
                    return typeof(LegacyUIText);
                case "Button":
                    return typeof(UIButton);
                case "Toggle":
                    return typeof(UIToggle);
                case "BlueprintLoopScrollView":
                    return typeof(BlueprintLoopScrollView);
                case "Image":
                    return typeof(UIImage);
                case "Selectable":
                    return typeof(UISelectable);
                case "Sprite":
                    return typeof(Sprite);
                case "GameObject":
                    return typeof(GameObject);
                case "Component":
                    return typeof(Component);
                case "Object":
                    return typeof(Object);
                default:
                    return ResolveTypeFromCache(bindingType, shortName);
            }
        }

        private static Type ResolveTypeFromCache(string bindingType, string shortName)
        {
            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<Object>();
            foreach (Type type in types)
            {
                if (type.FullName == bindingType || type.Name == shortName)
                {
                    return type;
                }
            }

            return null;
        }

        private static BlueprintRunner FindNearestRunner(Object target)
        {
            GameObject gameObject = target as GameObject;
            Component component = target as Component;
            if (gameObject == null && component != null)
            {
                gameObject = component.gameObject;
            }

            Transform transform = gameObject == null ? null : gameObject.transform;
            while (transform != null)
            {
                BlueprintRunner runner = transform.GetComponent<BlueprintRunner>();
                if (runner != null)
                {
                    return runner;
                }

                transform = transform.parent;
            }

            return null;
        }

        private static BlueprintRunner FindBindingOwnerRunner(BlueprintVisualGraph graph, Object target)
        {
            BlueprintRunner runner = FindNearestRunner(target);
            if (runner != null)
            {
                return runner;
            }

            return target is Sprite ? FindUniqueRunnerForGraph(graph) : null;
        }

        private static BlueprintRunner FindUniqueRunnerForGraph(BlueprintVisualGraph graph)
        {
            string blueprintPath = GetGraphBlueprintPath(graph);
            if (string.IsNullOrEmpty(blueprintPath))
            {
                return null;
            }

            BlueprintRunner result = null;
            BlueprintRunner[] runners = Resources.FindObjectsOfTypeAll<BlueprintRunner>();
            for (int i = 0; i < runners.Length; i++)
            {
                BlueprintRunner runner = runners[i];
                if (runner == null || EditorUtility.IsPersistent(runner))
                {
                    continue;
                }

                if (!RunnerUsesBlueprint(runner, blueprintPath))
                {
                    continue;
                }

                if (result != null)
                {
                    return null;
                }

                result = runner;
            }

            return result;
        }

        private static string GetGraphBlueprintPath(BlueprintVisualGraph graph)
        {
            if (graph == null)
            {
                return null;
            }

            return string.IsNullOrEmpty(graph.SourceBlueprintAssetPath) ? null : NormalizeAssetPath(graph.SourceBlueprintAssetPath);
        }

        private static bool RunnerUsesBlueprint(BlueprintRunner runner, string blueprintPath)
        {
            BlueprintCompiledAsset compiledAsset = runner == null ? null : runner.CompiledBlueprint;
            if (compiledAsset == null)
            {
                return false;
            }

            return NormalizeAssetPath(BlueprintCompiledAssetCompiler.GetCompiledAssetSourcePath(compiledAsset)) == blueprintPath;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }

        private static string FindExistingBindingName(BlueprintRunner runner, Object target)
        {
            if (runner == null || target == null)
            {
                return null;
            }

            SerializedObject serializedObject = new SerializedObject(runner);
            SerializedProperty bindings = serializedObject.FindProperty("bindings");
            if (bindings == null || !bindings.isArray)
            {
                return null;
            }

            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty entry = bindings.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = entry.FindPropertyRelative("Name");
                SerializedProperty targetProperty = entry.FindPropertyRelative("Target");
                if (nameProperty != null && targetProperty != null &&
                    targetProperty.objectReferenceValue == target &&
                    !string.IsNullOrEmpty(nameProperty.stringValue))
                {
                    return nameProperty.stringValue;
                }
            }

            return null;
        }

        private static string CreateUniqueBindingName(string suggestedName, BlueprintVisualGraph graph, BlueprintRunner runner)
        {
            string baseName = SanitizeIdentifier(suggestedName);
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "Binding";
            }

            HashSet<string> usedNames = new HashSet<string>(StringComparer.Ordinal);
            if (graph.Bindings != null)
            {
                for (int i = 0; i < graph.Bindings.Count; i++)
                {
                    if (graph.Bindings[i] != null && !string.IsNullOrEmpty(graph.Bindings[i].Name))
                    {
                        usedNames.Add(graph.Bindings[i].Name);
                    }
                }
            }

            CollectRunnerBindingNames(runner, usedNames);
            if (!usedNames.Contains(baseName))
            {
                return baseName;
            }

            int suffix = 2;
            while (usedNames.Contains(baseName + suffix.ToString()))
            {
                suffix++;
            }

            return baseName + suffix.ToString();
        }

        private static HashSet<string> CollectVariableNames(BlueprintVisualGraph graph)
        {
            HashSet<string> usedNames = new HashSet<string>(StringComparer.Ordinal);
            if (graph == null)
            {
                return usedNames;
            }

            if (graph.Variables != null)
            {
                for (int i = 0; i < graph.Variables.Count; i++)
                {
                    BlueprintVisualVariableData variable = graph.Variables[i];
                    if (variable != null && !string.IsNullOrEmpty(variable.Name))
                    {
                        usedNames.Add(variable.Name);
                    }
                }
            }

            foreach (IVariable variable in graph.GetVariables())
            {
                if (variable != null && !string.IsNullOrEmpty(variable.name))
                {
                    usedNames.Add(variable.name);
                }
            }

            return usedNames;
        }

        private static string CreateUniqueVariableName(string suggestedName, HashSet<string> usedNames)
        {
            string baseName = SanitizeIdentifier(suggestedName);
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "Blueprint";
            }

            if (usedNames == null)
            {
                usedNames = new HashSet<string>(StringComparer.Ordinal);
            }

            if (usedNames.Add(baseName))
            {
                return baseName;
            }

            int suffix = 2;
            while (!usedNames.Add(baseName + suffix.ToString()))
            {
                suffix++;
            }

            return baseName + suffix.ToString();
        }

        private static string GetBlueprintVariableBaseName(string blueprintAssetPath)
        {
            string normalizedPath = BlueprintGraphToolkitBlueprintTypes.NormalizePath(blueprintAssetPath);
            int slashIndex = normalizedPath.LastIndexOf('/');
            string fileName = slashIndex >= 0 ? normalizedPath.Substring(slashIndex + 1) : normalizedPath;
            const string suffix = ".blueprint.json";
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return fileName.Substring(0, fileName.Length - suffix.Length);
            }

            return fileName;
        }

        private static string GetDataTableVariableBaseName(BlueprintDataTableAsset asset)
        {
            if (asset == null)
            {
                return "DataTable";
            }

            string tableId = asset.TableId;
            const string prefix = "Table.";
            return !string.IsNullOrEmpty(tableId) && tableId.StartsWith(prefix, StringComparison.Ordinal)
                ? tableId.Substring(prefix.Length)
                : asset.name;
        }

        private static void EnsureGraphVariableMetadata(BlueprintVisualGraph graph, string variableName, string blueprintAssetPath)
        {
            EnsureGraphVariableMetadata(
                graph,
                variableName,
                blueprintAssetPath,
                BlueprintGraphToolkitBlueprintTypes.TypeId);
        }

        private static void EnsureGraphVariableMetadata(
            BlueprintVisualGraph graph,
            string variableName,
            string defaultPath,
            string blueprintType)
        {
            if (graph.Variables == null)
            {
                graph.Variables = new List<BlueprintVisualVariableData>();
            }

            for (int i = 0; i < graph.Variables.Count; i++)
            {
                BlueprintVisualVariableData variable = graph.Variables[i];
                if (variable != null && variable.Name == variableName)
                {
                    variable.Type = blueprintType;
                    variable.HasDefaultValue = true;
                    variable.JsonDefaultValue = BlueprintVisualValueUtility.ToJson(defaultPath);
                    return;
                }
            }

            graph.Variables.Add(new BlueprintVisualVariableData
            {
                Name = variableName,
                Type = blueprintType,
                HasDefaultValue = true,
                JsonDefaultValue = BlueprintVisualValueUtility.ToJson(defaultPath),
                Scope = "runtime"
            });
        }

        private static void CollectRunnerBindingNames(BlueprintRunner runner, HashSet<string> usedNames)
        {
            if (runner == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(runner);
            SerializedProperty bindings = serializedObject.FindProperty("bindings");
            if (bindings == null || !bindings.isArray)
            {
                return;
            }

            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty entry = bindings.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = entry.FindPropertyRelative("Name");
                if (nameProperty != null && !string.IsNullOrEmpty(nameProperty.stringValue))
                {
                    usedNames.Add(nameProperty.stringValue);
                }
            }
        }

        private static void EnsureGraphBinding(BlueprintVisualGraph graph, string bindingName, string bindingType, bool required)
        {
            if (graph.Bindings == null)
            {
                graph.Bindings = new List<BlueprintVisualBindingData>();
            }

            for (int i = 0; i < graph.Bindings.Count; i++)
            {
                BlueprintVisualBindingData binding = graph.Bindings[i];
                if (binding != null && binding.Name == bindingName)
                {
                    if (string.IsNullOrEmpty(binding.Type))
                    {
                        binding.Type = bindingType;
                    }

                    binding.Required = binding.Required || required;
                    return;
                }
            }

            graph.Bindings.Add(new BlueprintVisualBindingData
            {
                Name = bindingName,
                Type = bindingType,
                Required = required
            });
        }

        private static bool EnsureRunnerBinding(BlueprintRunner runner, string bindingName, Object target)
        {
            if (runner == null || target == null)
            {
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(runner);
            SerializedProperty bindings = serializedObject.FindProperty("bindings");
            if (bindings == null || !bindings.isArray)
            {
                return false;
            }

            serializedObject.Update();
            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty entry = bindings.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = entry.FindPropertyRelative("Name");
                SerializedProperty targetProperty = entry.FindPropertyRelative("Target");
                if (nameProperty != null && targetProperty != null && nameProperty.stringValue == bindingName)
                {
                    targetProperty.objectReferenceValue = target;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(runner);
                    runner.RebuildBindingCache();
                    return true;
                }
            }

            int newIndex = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newEntry = bindings.GetArrayElementAtIndex(newIndex);
            SerializedProperty newName = newEntry.FindPropertyRelative("Name");
            SerializedProperty newTarget = newEntry.FindPropertyRelative("Target");
            if (newName != null)
            {
                newName.stringValue = bindingName;
            }

            if (newTarget != null)
            {
                newTarget.objectReferenceValue = target;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(runner);
            runner.RebuildBindingCache();
            return true;
        }

        private static string CreateUniqueNodeId(string typeId, string bindingName, BlueprintVisualGraph graph)
        {
            string baseId = GetNodeIdPrefix(typeId) + "_" + ToSnakeCase(bindingName);
            baseId = SanitizeIdentifier(baseId).ToLowerInvariant();
            if (string.IsNullOrEmpty(baseId))
            {
                baseId = "node";
            }

            HashSet<string> usedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (INode node in graph.GetNodes())
            {
                BlueprintVisualNode blueprintNode = node as BlueprintVisualNode;
                if (blueprintNode != null)
                {
                    string nodeId = blueprintNode.ReadNodeId();
                    if (!string.IsNullOrEmpty(nodeId))
                    {
                        usedIds.Add(nodeId);
                    }
                }
            }

            if (!usedIds.Contains(baseId))
            {
                return baseId;
            }

            int suffix = 2;
            while (usedIds.Contains(baseId + "_" + suffix.ToString()))
            {
                suffix++;
            }

            return baseId + "_" + suffix.ToString();
        }

        private static string GetNodeIdPrefix(string typeId)
        {
            switch (typeId)
            {
                case "UI.SetText":
                    return "set_text";
                case "UI.SetVisible":
                    return "set_visible";
                case "UI.SetImageSprite":
                    return "set_image_sprite";
                case "UI.SpriteBinding":
                    return "sprite";
                case "UI.SetInteractable":
                    return "set_interactable";
                case "UI.BindButtonClick":
                    return "bind_click";
                case "UI.BindButtonEvents":
                    return "bind_button_events";
                case "UI.BindToggleChanged":
                    return "bind_toggle";
                case "UI.RefreshLoopScrollView":
                    return "refresh_loop_scroll";
                case "Variable.Get":
                    return "get";
                case "Variable.Set":
                    return "set";
                case "Variable.GetField":
                    return "get_field";
                case "Variable.BreakStruct":
                    return "break_struct";
                case "DataTable.GetRow":
                    return "datatable_get_row";
                case "DataTable.GetRowNames":
                    return "datatable_row_names";
                case "DataTable.GetAllRows":
                    return "datatable_all_rows";
                case "Array.Count":
                    return "array_count";
                case "Array.Get":
                    return "array_get";
                case "Array.ForEachLoop":
                    return "for_each";
                case "Array.ForEachLoopWithBreak":
                    return "for_each_break";
                case "Array.IsValidIndex":
                    return "array_valid_index";
                case "Array.Contains":
                    return "array_contains";
                case "Array.IndexOf":
                    return "array_index_of";
                case "Array.First":
                    return "array_first";
                case "Array.Last":
                    return "array_last";
                default:
                    return string.IsNullOrEmpty(typeId) ? "node" : typeId.Replace('.', '_');
            }
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            char[] chars = value.ToCharArray();
            List<char> result = new List<char>(chars.Length);
            bool previousWasSeparator = false;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    result.Add(c);
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator && result.Count > 0)
                {
                    result.Add('_');
                    previousWasSeparator = true;
                }
            }

            while (result.Count > 0 && result[result.Count - 1] == '_')
            {
                result.RemoveAt(result.Count - 1);
            }

            if (result.Count > 0 && char.IsDigit(result[0]))
            {
                result.Insert(0, '_');
            }

            return new string(result.ToArray());
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            List<char> result = new List<char>(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsUpper(c) && i > 0 && result.Count > 0 && result[result.Count - 1] != '_')
                {
                    result.Add('_');
                }

                result.Add(char.ToLowerInvariant(c));
            }

            return new string(result.ToArray());
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null)
            {
                return null;
            }

            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                PropertyInfo property = type.GetProperty(propertyName, ReflectionFlags | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property.GetValue(target, null);
                }
            }

            return null;
        }
    }
}
