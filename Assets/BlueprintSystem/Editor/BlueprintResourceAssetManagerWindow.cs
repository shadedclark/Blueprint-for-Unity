using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    public sealed class BlueprintResourceAssetManagerWindow : EditorWindow
    {
        private enum ViewMode
        {
            Resources,
            ResourceTypes,
            Packaging
        }

        private BlueprintResourceAssetManagerReport _report;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private Vector2 _resourceTypeScroll;
        private Vector2 _packagingScroll;
        private Vector2 _packagingPreviewScroll;
        private string _search = string.Empty;
        private int _selectedIndex = -1;
        private ViewMode _view;
        private BlueprintResourceTypeCatalogAsset _resourceTypeCatalog;
        private SerializedObject _resourceTypeCatalogObject;
        private BlueprintResourcePackagingPolicyAsset _packagingPolicy;
        private SerializedObject _packagingPolicyObject;

        [MenuItem("Tools/Blueprint System/Resource Asset Manager/Open")]
        public static void Open()
        {
            BlueprintResourceAssetManagerWindow window = GetWindow<BlueprintResourceAssetManagerWindow>("Resource Asset Manager");
            window.EnsureResourceTypeCatalogEditor();
            window.Refresh();
            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_view == ViewMode.ResourceTypes)
            {
                DrawResourceTypesEditor();
                return;
            }

            if (_view == ViewMode.Packaging)
            {
                DrawPackagingEditor();
                return;
            }

            if (_report == null)
            {
                EditorGUILayout.HelpBox("No scan has been run.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetails();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                Refresh();
            }

            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                _report = BlueprintResourceAssetManagerUtility.ScanProject(true);
            }

            if (GUILayout.Button("Sync All", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                _report = BlueprintResourceAssetManagerUtility.SyncAll(true);
                EnsurePackagingPolicyEditor(false);
            }

            GUILayout.Space(8);
            bool showResources = GUILayout.Toggle(_view == ViewMode.Resources, "Resources", EditorStyles.toolbarButton, GUILayout.Width(80));
            if (showResources)
            {
                _view = ViewMode.Resources;
            }

            bool showResourceTypes = GUILayout.Toggle(_view == ViewMode.ResourceTypes, "Resource Types", EditorStyles.toolbarButton, GUILayout.Width(110));
            if (showResourceTypes)
            {
                _view = ViewMode.ResourceTypes;
                EnsureResourceTypeCatalogEditor();
            }

            bool showPackaging = GUILayout.Toggle(_view == ViewMode.Packaging, "Packaging", EditorStyles.toolbarButton, GUILayout.Width(90));
            if (showPackaging)
            {
                _view = ViewMode.Packaging;
                EnsurePackagingPolicyEditor(false);
            }

            if (_view == ViewMode.ResourceTypes && GUILayout.Button("Ping Catalog", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                Ping(BlueprintResourceAssetManagerUtility.ResourceTypeCatalogAssetPath);
            }

            GUILayout.Space(12);
            if (_view == ViewMode.Resources)
            {
                GUILayout.Label("Search", GUILayout.Width(46));
                _search = GUILayout.TextField(_search ?? string.Empty, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));
            }

            GUILayout.FlexibleSpace();
            string summary = _view == ViewMode.ResourceTypes
                ? BlueprintResourceAssetManagerUtility.ResourceTypeCatalogAssetPath
                : _view == ViewMode.Packaging
                    ? BlueprintResourceAssetManagerUtility.PackagingPolicyAssetPath
                    : _report == null ? string.Empty : _report.Records.Count + " resources, " + CountErrors(_report) + " errors";
            GUILayout.Label(summary, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawResourceTypesEditor()
        {
            EnsureResourceTypeCatalogEditor();
            if (_resourceTypeCatalog == null || _resourceTypeCatalogObject == null)
            {
                EditorGUILayout.HelpBox("Resource Type Catalog could not be created.", MessageType.Error);
                return;
            }

            _resourceTypeScroll = EditorGUILayout.BeginScrollView(_resourceTypeScroll);
            EditorGUILayout.LabelField("Resource Types", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Catalog", BlueprintResourceAssetManagerUtility.ResourceTypeCatalogAssetPath);
            EditorGUILayout.Space();

            SerializedProperty resourceTypesProperty = _resourceTypeCatalogObject.FindProperty("resourceTypes");
            if (resourceTypesProperty == null)
            {
                EditorGUILayout.HelpBox("Catalog asset is missing the ResourceTypes serialized property.", MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }

            _resourceTypeCatalogObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(resourceTypesProperty, new GUIContent("Resource Types"), true);
            if (EditorGUI.EndChangeCheck())
            {
                _resourceTypeCatalogObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(_resourceTypeCatalog);
                AssetDatabase.SaveAssets();
                _report = BlueprintResourceAssetManagerUtility.ScanProject(true);
            }
            else
            {
                _resourceTypeCatalogObject.ApplyModifiedProperties();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPackagingEditor()
        {
            EnsurePackagingPolicyEditor(false);
            _packagingScroll = EditorGUILayout.BeginScrollView(_packagingScroll);
            EditorGUILayout.LabelField("Resource Packaging", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Policy", BlueprintResourceAssetManagerUtility.PackagingPolicyAssetPath);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (_packagingPolicy == null)
            {
                if (GUILayout.Button("Create Policy", GUILayout.Width(110)))
                {
                    EnsurePackagingPolicyEditor(true);
                    Refresh();
                }
            }
            else if (GUILayout.Button("Ping Policy", GUILayout.Width(100)))
            {
                Ping(BlueprintResourceAssetManagerUtility.PackagingPolicyAssetPath);
            }

            if (GUILayout.Button("Validate Packaging", GUILayout.Width(130)))
            {
                _report = BlueprintResourceAssetManagerUtility.ScanProject(true);
            }

            if (GUILayout.Button("Sync Addressables", GUILayout.Width(135)))
            {
                _report = BlueprintResourceAssetManagerUtility.SyncAll(true);
                EnsurePackagingPolicyEditor(false);
            }

            if (GUILayout.Button("Scan Shared Dependencies", GUILayout.Width(170)))
            {
                EnsureReport();
                BlueprintResourceAssetManagerUtility.ScanSharedDependencies(_report);
            }

            if (GUILayout.Button("Extract Shared Dependencies", GUILayout.Width(180)))
            {
                EnsureReport();
                BlueprintResourceAssetManagerUtility.ExtractSharedDependencies(_report);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            if (_packagingPolicy == null || _packagingPolicyObject == null)
            {
                EditorGUILayout.HelpBox("Create a Resource Packaging Policy to edit DLCs and packaging rules.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            _packagingPolicyObject.Update();
            EditorGUI.BeginChangeCheck();
            SerializedProperty dlcsProperty = _packagingPolicyObject.FindProperty("dlcs");
            EditorGUILayout.PropertyField(dlcsProperty, new GUIContent("DLCs"), true);
            DrawDefaultPackagingRule(_packagingPolicyObject.FindProperty("defaultRule"), dlcsProperty);
            DrawTypePackagingRules(_packagingPolicyObject.FindProperty("typeRules"), dlcsProperty);
            DrawResourcePackagingOverrides(_packagingPolicyObject.FindProperty("resourceOverrides"), dlcsProperty);
            if (EditorGUI.EndChangeCheck())
            {
                _packagingPolicyObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(_packagingPolicy);
                AssetDatabase.SaveAssets();
                _report = BlueprintResourceAssetManagerUtility.ScanProject(true);
            }
            else
            {
                _packagingPolicyObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space();
            DrawSelectedPackagingActions();
            EditorGUILayout.Space();
            DrawPackagingPreview();
            EditorGUILayout.Space();
            DrawSharedDependencyPreview();
            EditorGUILayout.EndScrollView();
        }

        private void DrawDefaultPackagingRule(SerializedProperty ruleProperty, SerializedProperty dlcsProperty)
        {
            if (ruleProperty == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Default Rule", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawPackagingRule(ruleProperty, dlcsProperty);
            EditorGUILayout.EndVertical();
        }

        private void DrawTypePackagingRules(SerializedProperty rulesProperty, SerializedProperty dlcsProperty)
        {
            if (rulesProperty == null)
            {
                return;
            }

            EditorGUILayout.Space();
            rulesProperty.isExpanded = EditorGUILayout.Foldout(rulesProperty.isExpanded, "Type Rules", true);
            if (!rulesProperty.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            rulesProperty.arraySize = Mathf.Max(0, EditorGUILayout.IntField("Size", rulesProperty.arraySize));
            for (int i = 0; i < rulesProperty.arraySize; i++)
            {
                SerializedProperty item = rulesProperty.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Type Rule " + i, EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    rulesProperty.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    i--;
                    continue;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(item.FindPropertyRelative("ResourceType"));
                DrawPackagingRule(item.FindPropertyRelative("Rule"), dlcsProperty);
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Type Rule", GUILayout.Width(120)))
            {
                int index = rulesProperty.arraySize;
                rulesProperty.InsertArrayElementAtIndex(index);
                SerializedProperty item = rulesProperty.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("ResourceType").stringValue = string.Empty;
                ResetRuleProperty(item.FindPropertyRelative("Rule"));
            }

            EditorGUI.indentLevel--;
        }

        private void DrawResourcePackagingOverrides(SerializedProperty overridesProperty, SerializedProperty dlcsProperty)
        {
            if (overridesProperty == null)
            {
                return;
            }

            EditorGUILayout.Space();
            overridesProperty.isExpanded = EditorGUILayout.Foldout(overridesProperty.isExpanded, "Resource Overrides", true);
            if (!overridesProperty.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            overridesProperty.arraySize = Mathf.Max(0, EditorGUILayout.IntField("Size", overridesProperty.arraySize));
            for (int i = 0; i < overridesProperty.arraySize; i++)
            {
                SerializedProperty item = overridesProperty.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Resource Override " + i, EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    overridesProperty.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    i--;
                    continue;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(item.FindPropertyRelative("ResourceType"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("ResourceName"));
                DrawPackagingRule(item.FindPropertyRelative("Rule"), dlcsProperty);
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Resource Override", GUILayout.Width(160)))
            {
                int index = overridesProperty.arraySize;
                overridesProperty.InsertArrayElementAtIndex(index);
                SerializedProperty item = overridesProperty.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("ResourceType").stringValue = string.Empty;
                item.FindPropertyRelative("ResourceName").stringValue = string.Empty;
                ResetRuleProperty(item.FindPropertyRelative("Rule"));
            }

            EditorGUI.indentLevel--;
        }

        private void DrawPackagingRule(SerializedProperty ruleProperty, SerializedProperty dlcsProperty)
        {
            if (ruleProperty == null)
            {
                return;
            }

            SerializedProperty includeProperty = ruleProperty.FindPropertyRelative("IncludeInBuild");
            SerializedProperty locationProperty = ruleProperty.FindPropertyRelative("ContentLocation");
            SerializedProperty dlcIdProperty = ruleProperty.FindPropertyRelative("DlcId");
            SerializedProperty priorityProperty = ruleProperty.FindPropertyRelative("LoadPriority");

            EditorGUILayout.PropertyField(includeProperty);
            EditorGUILayout.PropertyField(locationProperty);
            if (locationProperty.enumValueIndex == (int)BlueprintResourceContentLocation.DLC)
            {
                DrawDlcPopup(dlcIdProperty, dlcsProperty);
            }
            else if (dlcIdProperty != null)
            {
                dlcIdProperty.stringValue = string.Empty;
            }

            EditorGUILayout.PropertyField(priorityProperty);
        }

        private void DrawDlcPopup(SerializedProperty dlcIdProperty, SerializedProperty dlcsProperty)
        {
            if (dlcIdProperty == null)
            {
                return;
            }

            List<string> ids = new List<string>();
            List<string> labels = new List<string>();
            BuildDlcPopupOptions(dlcsProperty, ids, labels);
            if (ids.Count == 0)
            {
                EditorGUILayout.HelpBox("Add a DLC entry before assigning DLC packaging rules.", MessageType.Warning);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("DLC", dlcIdProperty.stringValue ?? string.Empty);
                EditorGUI.EndDisabledGroup();
                return;
            }

            string current = dlcIdProperty.stringValue ?? string.Empty;
            int selected = IndexOf(ids, current);
            if (selected < 0 && !string.IsNullOrEmpty(current))
            {
                ids.Add(current);
                labels.Add("Missing: " + current);
                selected = ids.Count - 1;
            }
            else if (selected < 0)
            {
                selected = 0;
                dlcIdProperty.stringValue = ids[0];
            }

            int next = EditorGUILayout.Popup("DLC", selected, labels.ToArray());
            if (next >= 0 && next < ids.Count)
            {
                dlcIdProperty.stringValue = ids[next];
            }
        }

        private static void BuildDlcPopupOptions(SerializedProperty dlcsProperty, List<string> ids, List<string> labels)
        {
            if (dlcsProperty == null || !dlcsProperty.isArray)
            {
                return;
            }

            for (int i = 0; i < dlcsProperty.arraySize; i++)
            {
                SerializedProperty item = dlcsProperty.GetArrayElementAtIndex(i);
                string id = item.FindPropertyRelative("DlcId").stringValue;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                string name = item.FindPropertyRelative("DisplayName").stringValue;
                string label = string.IsNullOrEmpty(name) ? id : name + " (" + id + ")";
                ids.Add(id);
                labels.Add(label);
            }
        }

        private static void ResetRuleProperty(SerializedProperty ruleProperty)
        {
            if (ruleProperty == null)
            {
                return;
            }

            ruleProperty.FindPropertyRelative("IncludeInBuild").boolValue = true;
            ruleProperty.FindPropertyRelative("ContentLocation").enumValueIndex = (int)BlueprintResourceContentLocation.Base;
            ruleProperty.FindPropertyRelative("DlcId").stringValue = string.Empty;
            ruleProperty.FindPropertyRelative("LoadPriority").intValue = 0;
        }

        private static int IndexOf(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        private void DrawSelectedPackagingActions()
        {
            BlueprintResourceAssetRecord record = GetSelectedRecord();
            if (record == null || record.Source == null || _packagingPolicy == null)
            {
                EditorGUILayout.HelpBox("Select a resource in the Resources view to add a resource override from its resolved rule.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Selected Resource Override", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Selected", record.Source.Id.ToString());
            BlueprintResourceOverridePackagingRule existing =
                _packagingPolicy.FindResourceOverride(record.Source.ResourceType, record.Source.ResourceName);
            if (existing != null)
            {
                EditorGUILayout.HelpBox("This resource already has an override.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Add Override From Effective Rule", GUILayout.Width(220)))
            {
                BlueprintResourceOverridePackagingRule resourceOverride = new BlueprintResourceOverridePackagingRule();
                resourceOverride.ResourceType = record.Source.ResourceType;
                resourceOverride.ResourceName = record.Source.ResourceName;
                resourceOverride.Rule.CopyFrom(BlueprintResourcePackagingUtility.ResolveRule(record.Source, _packagingPolicy));
                _packagingPolicy.ResourceOverrides.Add(resourceOverride);
                EditorUtility.SetDirty(_packagingPolicy);
                AssetDatabase.SaveAssets();
                EnsurePackagingPolicyEditor(false);
                _report = BlueprintResourceAssetManagerUtility.ScanProject(true);
            }
        }

        private void DrawPackagingPreview()
        {
            EnsureReport();
            EditorGUILayout.LabelField("Resolved Preview", EditorStyles.boldLabel);
            if (_report == null || _report.Records.Count == 0)
            {
                EditorGUILayout.LabelField("No resources.");
                return;
            }

            _packagingPreviewScroll = EditorGUILayout.BeginScrollView(_packagingPreviewScroll, GUI.skin.box, GUILayout.MinHeight(180), GUILayout.MaxHeight(260));
            for (int i = 0; i < _report.Records.Count; i++)
            {
                BlueprintResourceAssetRecord record = _report.Records[i];
                if (record == null || record.Source == null)
                {
                    continue;
                }

                BlueprintResourceResolvedPackaging packaging = record.Packaging;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(record.Source.Id.ToString(), GUILayout.MinWidth(170));
                EditorGUILayout.LabelField(packaging == null || packaging.IncludeInBuild ? "Include" : "Exclude", GUILayout.Width(60));
                EditorGUILayout.LabelField(packaging == null ? string.Empty : packaging.ContentLocation.ToString(), GUILayout.Width(45));
                EditorGUILayout.LabelField(packaging == null ? string.Empty : packaging.DlcId, GUILayout.Width(90));
                EditorGUILayout.LabelField(packaging == null ? record.Source.Priority.ToString() : packaging.LoadPriority.ToString(), GUILayout.Width(48));
                EditorGUILayout.LabelField(packaging == null ? string.Empty : packaging.GroupName);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSharedDependencyPreview()
        {
            EnsureReport();
            EditorGUILayout.LabelField("Shared Dependency Candidates", EditorStyles.boldLabel);
            if (_report == null || _report.SharedDependencyCandidates.Count == 0)
            {
                EditorGUILayout.LabelField("No candidates scanned.");
                return;
            }

            for (int i = 0; i < _report.SharedDependencyCandidates.Count; i++)
            {
                BlueprintResourceSharedDependencyCandidate candidate = _report.SharedDependencyCandidates[i];
                if (candidate == null)
                {
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(candidate.AssetPath, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Target Group", candidate.SharedGroupName);
                EditorGUILayout.LabelField("Address", candidate.Address);
                EditorGUILayout.LabelField("Owners", string.Join(", ", candidate.OwnerResourceIds.ToArray()));
                if (!string.IsNullOrEmpty(candidate.Warning))
                {
                    EditorGUILayout.HelpBox(candidate.Warning, MessageType.Warning);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(320));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUI.skin.box);
            for (int i = 0; i < _report.Records.Count; i++)
            {
                BlueprintResourceAssetRecord record = _report.Records[i];
                if (!MatchesSearch(record))
                {
                    continue;
                }

                bool selected = i == _selectedIndex;
                GUIStyle style = selected ? EditorStyles.helpBox : EditorStyles.label;
                EditorGUILayout.BeginHorizontal(style);
                if (GUILayout.Button(record.Source == null ? record.SourcePath : record.Source.Id.ToString(), GUIStyle.none))
                {
                    _selectedIndex = i;
                    GUI.FocusControl(null);
                }

                int errors = CountErrors(record);
                if (errors > 0)
                {
                    GUILayout.Label("E" + errors, EditorStyles.miniLabel, GUILayout.Width(28));
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetails()
        {
            EditorGUILayout.BeginVertical();
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            BlueprintResourceAssetRecord record = GetSelectedRecord();
            if (record == null)
            {
                EditorGUILayout.HelpBox("Select a Resource Blueprint.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            DrawSelectedHeader(record);
            DrawIssues(record);
            DrawSourceDetails(record);
            DrawDependencies(record);
            DrawReverseDependencies(record);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedHeader(BlueprintResourceAssetRecord record)
        {
            EditorGUILayout.LabelField(record.Source == null ? "Invalid Resource Blueprint" : record.Source.Id.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ping Source", GUILayout.Width(100)))
            {
                Ping(record.SourcePath);
            }

            if (GUILayout.Button("Open JSON", GUILayout.Width(100)))
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(record.SourcePath);
                if (asset != null)
                {
                    AssetDatabase.OpenAsset(asset);
                }
            }

            if (GUILayout.Button("Open Graph", GUILayout.Width(100)))
            {
                BlueprintResourceGraphToolkitBridge.ImportResourceBlueprintAtPath(record.SourcePath, true);
            }

            if (record.Source != null && record.Source.MainAsset != null && GUILayout.Button("Ping Main Asset", GUILayout.Width(120)))
            {
                Ping(record.Source.MainAsset.Path);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void DrawIssues(BlueprintResourceAssetRecord record)
        {
            if (record.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No validation issues.", MessageType.Info);
                return;
            }

            for (int i = 0; i < record.Issues.Count; i++)
            {
                BlueprintResourceValidationIssue issue = record.Issues[i];
                MessageType type = issue.Severity == BlueprintResourceValidationSeverity.Error
                    ? MessageType.Error
                    : issue.Severity == BlueprintResourceValidationSeverity.Warning ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.HelpBox(issue.Message, type);
            }
        }

        private void DrawSourceDetails(BlueprintResourceAssetRecord record)
        {
            if (record.Source == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Source", record.SourcePath);
            EditorGUILayout.LabelField("Display Name", record.Source.DisplayName ?? string.Empty);
            EditorGUILayout.LabelField("Main Asset", record.Source.MainAsset == null ? string.Empty : record.Source.MainAsset.Path);
            EditorGUILayout.LabelField("Address", record.Source.MainAsset == null ? string.Empty : record.Source.MainAsset.Address);
            EditorGUILayout.LabelField("Type", record.Source.MainAsset == null ? string.Empty : record.Source.MainAsset.AssetType);
            EditorGUILayout.LabelField("Priority", record.Source.Priority.ToString());
            if (record.Packaging != null)
            {
                EditorGUILayout.LabelField("Packaging", record.Packaging.IncludeInBuild ? "Included" : "Excluded");
                EditorGUILayout.LabelField("Packaging Source", record.Packaging.RuleSource ?? string.Empty);
                EditorGUILayout.LabelField("Packaging Group", record.Packaging.GroupName ?? string.Empty);
                EditorGUILayout.LabelField("Packaging Priority", record.Packaging.LoadPriority.ToString());
            }

            EditorGUILayout.LabelField("Memory Budget MB", record.Source.MemoryBudgetMb.ToString("0.##"));
            EditorGUILayout.LabelField("Preload Groups", string.Join(", ", record.Source.PreloadGroups.ToArray()));
            EditorGUILayout.LabelField("Tags", string.Join(", ", record.Source.Tags.ToArray()));
            EditorGUILayout.Space();
        }

        private void DrawDependencies(BlueprintResourceAssetRecord record)
        {
            if (record.Source == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);
            if (record.Source.Dependencies.Count == 0)
            {
                EditorGUILayout.LabelField("None");
                return;
            }

            for (int i = 0; i < record.Source.Dependencies.Count; i++)
            {
                BlueprintResourceDependency dependency = record.Source.Dependencies[i];
                EditorGUILayout.LabelField(dependency.ToId().ToString(), dependency.Required ? "required" : "optional");
            }
        }

        private void DrawReverseDependencies(BlueprintResourceAssetRecord selected)
        {
            if (selected.Source == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Referenced By", EditorStyles.boldLabel);
            string selectedId = selected.Source.Id.ToString();
            List<string> references = new List<string>();
            for (int i = 0; i < _report.Records.Count; i++)
            {
                BlueprintResourceAssetRecord record = _report.Records[i];
                if (record == null || record.Source == null || record == selected)
                {
                    continue;
                }

                for (int d = 0; d < record.Source.Dependencies.Count; d++)
                {
                    if (record.Source.Dependencies[d].ToId().ToString() == selectedId)
                    {
                        references.Add(record.Source.Id.ToString());
                    }
                }
            }

            if (references.Count == 0)
            {
                EditorGUILayout.LabelField("None");
                return;
            }

            for (int i = 0; i < references.Count; i++)
            {
                EditorGUILayout.LabelField(references[i]);
            }
        }

        private void Refresh()
        {
            _report = BlueprintResourceAssetManagerUtility.ScanProject(true);
            EnsurePackagingPolicyEditor(false);
            if (_selectedIndex >= _report.Records.Count)
            {
                _selectedIndex = -1;
            }
        }

        private void EnsureReport()
        {
            if (_report == null)
            {
                _report = BlueprintResourceAssetManagerUtility.ScanProject(true);
            }
        }

        private void EnsureResourceTypeCatalogEditor()
        {
            if (_resourceTypeCatalog == null)
            {
                _resourceTypeCatalog = BlueprintResourceAssetManagerUtility.GetOrCreateResourceTypeCatalogAsset();
            }

            if (_resourceTypeCatalog != null &&
                (_resourceTypeCatalogObject == null || _resourceTypeCatalogObject.targetObject != _resourceTypeCatalog))
            {
                _resourceTypeCatalogObject = new SerializedObject(_resourceTypeCatalog);
            }
        }

        private void EnsurePackagingPolicyEditor(bool create)
        {
            if (_packagingPolicy == null)
            {
                _packagingPolicy = create
                    ? BlueprintResourceAssetManagerUtility.GetOrCreateResourcePackagingPolicyAsset()
                    : BlueprintResourceAssetManagerUtility.LoadResourcePackagingPolicyAsset();
            }

            if (_packagingPolicy != null &&
                (_packagingPolicyObject == null || _packagingPolicyObject.targetObject != _packagingPolicy))
            {
                _packagingPolicyObject = new SerializedObject(_packagingPolicy);
            }
        }

        private BlueprintResourceAssetRecord GetSelectedRecord()
        {
            return _report != null && _selectedIndex >= 0 && _selectedIndex < _report.Records.Count
                ? _report.Records[_selectedIndex]
                : null;
        }

        private bool MatchesSearch(BlueprintResourceAssetRecord record)
        {
            if (string.IsNullOrEmpty(_search))
            {
                return true;
            }

            string text = (record.SourcePath ?? string.Empty) + " " +
                          (record.Source == null ? string.Empty : record.Source.Id + " " + record.Source.DisplayName);
            return text.ToLowerInvariant().Contains(_search.ToLowerInvariant());
        }

        private static int CountErrors(BlueprintResourceAssetManagerReport report)
        {
            int count = 0;
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i] != null && report.Issues[i].Severity == BlueprintResourceValidationSeverity.Error)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountErrors(BlueprintResourceAssetRecord record)
        {
            int count = 0;
            for (int i = 0; i < record.Issues.Count; i++)
            {
                if (record.Issues[i] != null && record.Issues[i].Severity == BlueprintResourceValidationSeverity.Error)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Ping(string path)
        {
            UnityEngine.Object asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
        }
    }
}
