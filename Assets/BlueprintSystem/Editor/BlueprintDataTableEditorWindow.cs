using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    internal sealed class BlueprintDataTableEditorDocument : ScriptableObject
    {
        [NonSerialized] internal BlueprintDataTableAsset sourceAsset;
        [SerializeField] internal string sourceAssetPath;
        [SerializeField] internal string sourceJsonPath;
        [SerializeField] internal bool jsonOnly;
        [SerializeField] internal string schemaVersion = "0.1";
        [SerializeField] internal string tableId;
        [SerializeField] internal string rowStructTypeId;
        [SerializeField] internal List<BlueprintDataTableAssetRow> rows = new List<BlueprintDataTableAssetRow>();
        [NonSerialized] internal bool dirty;

        internal string DisplayPath
        {
            get { return string.IsNullOrEmpty(sourceAssetPath) ? sourceJsonPath : sourceAssetPath; }
        }

        internal string ToJson()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["schemaVersion"] = string.IsNullOrEmpty(schemaVersion) ? "0.1" : schemaVersion;
            data["tableId"] = tableId;
            data["rowStructTypeId"] = rowStructTypeId;

            List<object> rowItems = new List<object>();
            for (int i = 0; i < rows.Count; i++)
            {
                BlueprintDataTableAssetRow row = rows[i];
                if (row == null)
                {
                    continue;
                }

                Dictionary<string, object> item = new Dictionary<string, object>();
                item["rowName"] = row.rowName;
                item["value"] = row.ReadValue();
                rowItems.Add(item);
            }

            data["rows"] = rowItems;
            return BlueprintJson.Serialize(data, true);
        }
    }

    public sealed class BlueprintDataTableEditorWindow : EditorWindow
    {
        private const float RowNameColumnWidth = 180f;
        private const float FieldColumnWidth = 170f;
        private const float ValueJsonColumnWidth = 520f;
        private const float CellHeight = 24f;
        private const float HeaderHeight = 26f;

        private BlueprintDataTableEditorDocument _document;
        private Vector2 _tableScroll;
        private int _selectedRow = -1;
        private int _selectedColumn = -1;
        private string _statusText = "No data table loaded.";

        internal string CurrentAssetPath
        {
            get { return _document == null ? null : _document.sourceAssetPath; }
        }

        internal string CurrentJsonPath
        {
            get { return _document == null ? null : _document.sourceJsonPath; }
        }

        internal bool IsJsonOnly
        {
            get { return _document != null && _document.jsonOnly; }
        }

        internal BlueprintDataTableEditorDocument CurrentDocument
        {
            get { return _document; }
        }

        [MenuItem("Tools/Blueprint System/Data Table Editor")]
        public static void Open()
        {
            GetWindow<BlueprintDataTableEditorWindow>("Data Table");
        }

        [MenuItem("Assets/Blueprint System/Open Data Table Editor", false, 2101)]
        public static void OpenSelectedDataTable()
        {
            if (!TryOpenSelectedDataTable())
            {
                BlueprintLog.Warning("[Blueprint] Select a BlueprintDataTableAsset or .bpdatatable.json TextAsset first.");
            }
        }

        [MenuItem("Assets/Blueprint System/Open Data Table Editor", true)]
        private static bool CanOpenSelectedDataTable()
        {
            return CanOpenPath(GetSingleSelectedAssetPath());
        }

        [OnOpenAsset(0)]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            return OpenAssetAtPath(BlueprintEditorWindow.GetAssetPathFromOpenAssetId(instanceId));
        }

        internal static bool TryOpenSelectedDataTable()
        {
            return OpenAssetAtPath(GetSingleSelectedAssetPath());
        }

        internal static bool OpenAssetAtPath(string assetPath)
        {
            BlueprintDataTableEditorDocument document;
            if (!TryCreateDocumentForPath(assetPath, out document))
            {
                return false;
            }

            BlueprintDataTableEditorWindow window = GetWindow<BlueprintDataTableEditorWindow>("Data Table");
            window.LoadDocument(document);
            window.Focus();
            return true;
        }

        internal static bool CanOpenPath(string assetPath)
        {
            assetPath = BlueprintAssetDiscovery.NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(assetPath) != null)
            {
                return true;
            }

            return IsDataTableJsonPath(assetPath) && AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath) != null;
        }

        internal static bool IsDataTableJsonPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.EndsWith(BlueprintDataTableRegistry.DataTableAssetExtension, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryCreateDocumentForPath(string assetPath, out BlueprintDataTableEditorDocument document)
        {
            document = null;
            assetPath = BlueprintAssetDiscovery.NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            BlueprintDataTableAsset sourceAsset = AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(assetPath);
            if (sourceAsset != null)
            {
                document = CreateDocumentFromAsset(sourceAsset, assetPath);
                return true;
            }

            if (!IsDataTableJsonPath(assetPath))
            {
                return false;
            }

            string sourceAssetPath;
            if (TryFindSourceAssetForJsonPath(assetPath, out sourceAssetPath, out sourceAsset))
            {
                document = CreateDocumentFromAsset(sourceAsset, sourceAssetPath);
                return true;
            }

            TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (jsonAsset == null)
            {
                return false;
            }

            try
            {
                BlueprintDataTableDefinition definition = BlueprintDataTableDefinition.FromJson(jsonAsset.text);
                document = CreateDocumentFromDefinition(definition, assetPath);
                return true;
            }
            catch (Exception exception)
            {
                BlueprintLog.Error("[Blueprint] Failed to open data table JSON '" + assetPath + "': " + exception.Message, jsonAsset);
                return false;
            }
        }

        internal static BlueprintDataTableEditorDocument CreateDocumentFromAsset(BlueprintDataTableAsset asset, string assetPath)
        {
            BlueprintDataTableEditorDocument document = CreateDocument();
            document.sourceAsset = asset;
            document.sourceAssetPath = BlueprintAssetDiscovery.NormalizeAssetPath(assetPath);
            document.sourceJsonPath = BlueprintDataTableRegistry.GetJsonPathForAssetPath(document.sourceAssetPath);
            document.jsonOnly = false;
            document.schemaVersion = string.IsNullOrEmpty(asset.SchemaVersion) ? "0.1" : asset.SchemaVersion;
            document.tableId = asset.TableId;
            document.rowStructTypeId = asset.RowStructTypeId;
            document.rows = CopyRows(asset.Rows);
            document.dirty = false;
            return document;
        }

        internal static BlueprintDataTableEditorDocument CreateDocumentFromDefinition(BlueprintDataTableDefinition definition, string jsonPath)
        {
            BlueprintDataTableEditorDocument document = CreateDocument();
            document.sourceJsonPath = BlueprintAssetDiscovery.NormalizeAssetPath(jsonPath);
            document.jsonOnly = true;
            document.schemaVersion = definition == null || string.IsNullOrEmpty(definition.SchemaVersion) ? "0.1" : definition.SchemaVersion;
            document.tableId = definition == null ? string.Empty : definition.TableId;
            document.rowStructTypeId = definition == null ? string.Empty : definition.RowStructTypeId;
            document.rows = new List<BlueprintDataTableAssetRow>();

            if (definition != null)
            {
                for (int i = 0; i < definition.Rows.Count; i++)
                {
                    BlueprintDataTableRowDefinition row = definition.Rows[i];
                    BlueprintDataTableAssetRow editableRow = new BlueprintDataTableAssetRow();
                    editableRow.rowName = row == null ? string.Empty : row.RowName;
                    editableRow.valueJson = row == null || row.Value == null ? string.Empty : BlueprintJson.Serialize(row.Value, false);
                    document.rows.Add(editableRow);
                }
            }

            document.dirty = false;
            return document;
        }

        internal static bool TryFindSourceAssetForJsonPath(
            string jsonPath,
            out string sourceAssetPath,
            out BlueprintDataTableAsset sourceAsset)
        {
            sourceAssetPath = null;
            sourceAsset = null;
            jsonPath = BlueprintAssetDiscovery.NormalizeAssetPath(jsonPath);
            if (string.IsNullOrEmpty(jsonPath))
            {
                return false;
            }

            string directAssetPath = GetSourceAssetPathForJsonPath(jsonPath);
            sourceAsset = AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(directAssetPath);
            if (sourceAsset != null)
            {
                sourceAssetPath = directAssetPath;
                return true;
            }

            List<string> assetPaths = BlueprintEditorAssetDiscovery.FindAssetPaths("t:BlueprintDataTableAsset");
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string candidatePath = assetPaths[i];
                string candidateJsonPath = BlueprintDataTableRegistry.GetJsonPathForAssetPath(candidatePath);
                if (!string.Equals(candidateJsonPath, jsonPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sourceAsset = AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(candidatePath);
                if (sourceAsset != null)
                {
                    sourceAssetPath = candidatePath;
                    return true;
                }
            }

            return false;
        }

        internal static string GetSourceAssetPathForJsonPath(string jsonPath)
        {
            jsonPath = BlueprintAssetDiscovery.NormalizeAssetPath(jsonPath);
            if (!IsDataTableJsonPath(jsonPath))
            {
                return jsonPath;
            }

            return jsonPath.Substring(0, jsonPath.Length - BlueprintDataTableRegistry.DataTableAssetExtension.Length) + ".asset";
        }

        internal static bool TryApplyRowNameEdit(
            BlueprintDataTableEditorDocument document,
            int rowIndex,
            string rowName,
            out string error)
        {
            error = null;
            BlueprintDataTableAssetRow row;
            if (!TryGetRow(document, rowIndex, out row, out error))
            {
                return false;
            }

            Undo.RecordObject(document, "Edit Data Table Row Name");
            row.rowName = rowName ?? string.Empty;
            MarkDocumentDirty(document);
            return true;
        }

        internal static bool TryApplyRowStructTypeEdit(
            BlueprintDataTableEditorDocument document,
            string rowStructTypeId,
            out string error)
        {
            error = null;
            if (document == null)
            {
                error = "No data table is loaded.";
                return false;
            }

            Undo.RecordObject(document, "Edit Data Table Row Struct");
            document.rowStructTypeId = rowStructTypeId ?? string.Empty;
            MarkDocumentDirty(document);
            return true;
        }

        internal static bool TryApplyFieldEdit(
            BlueprintDataTableEditorDocument document,
            int rowIndex,
            BlueprintUserStructField field,
            object value,
            out string error)
        {
            error = null;
            BlueprintDataTableAssetRow row;
            if (!TryGetRow(document, rowIndex, out row, out error))
            {
                return false;
            }

            if (field == null || field.Deprecated || string.IsNullOrEmpty(field.Name))
            {
                error = "Field is not editable.";
                return false;
            }

            object normalizedValue;
            if (!BlueprintDataTableAssetEditor.TryNormalizeJsonValue(value, field.Type, out normalizedValue))
            {
                error = field.Name + " value is not assignable to " + field.Type + ".";
                return false;
            }

            string readError;
            Dictionary<string, object> values = BlueprintDataTableAssetEditor.ReadEditableRowValue(
                row.valueJson,
                document.rowStructTypeId,
                out readError);
            values[field.Name] = normalizedValue;

            Undo.RecordObject(document, "Edit Data Table Cell");
            row.valueJson = BlueprintJson.Serialize(values, false);
            MarkDocumentDirty(document);
            return true;
        }

        internal static bool TryApplyValueJsonEdit(
            BlueprintDataTableEditorDocument document,
            int rowIndex,
            string valueJson,
            out string error)
        {
            error = null;
            BlueprintDataTableAssetRow row;
            if (!TryGetRow(document, rowIndex, out row, out error))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(valueJson))
            {
                try
                {
                    BlueprintJson.Deserialize(valueJson);
                }
                catch (BlueprintJsonException exception)
                {
                    error = "Value JSON is invalid: " + exception.Message;
                    return false;
                }
            }

            Undo.RecordObject(document, "Edit Data Table JSON Cell");
            row.valueJson = valueJson ?? string.Empty;
            MarkDocumentDirty(document);
            return true;
        }

        internal static bool SaveDocument(BlueprintDataTableEditorDocument document, out string message)
        {
            message = null;
            if (document == null)
            {
                message = "No data table is loaded.";
                return false;
            }

            List<string> errors = BlueprintDataTableAssetEditor.Validate(
                document.tableId,
                document.rowStructTypeId,
                document.rows);
            if (errors.Count > 0)
            {
                message = string.Join("\n", errors.ToArray());
                return false;
            }

            try
            {
                if (!document.jsonOnly && document.sourceAsset != null)
                {
                    Undo.RecordObject(document.sourceAsset, "Save Data Table");
                    document.sourceAsset.SchemaVersion = string.IsNullOrEmpty(document.schemaVersion) ? "0.1" : document.schemaVersion;
                    document.sourceAsset.RowStructTypeId = document.rowStructTypeId;
                    document.sourceAsset.Rows.Clear();
                    document.sourceAsset.Rows.AddRange(CopyRows(document.rows));
                    EditorUtility.SetDirty(document.sourceAsset);
                    AssetDatabase.SaveAssets();
                    BlueprintDataTableAssetEditor.ExportJson(document.sourceAsset);
                    document.tableId = document.sourceAsset.TableId;
                    document.sourceJsonPath = BlueprintDataTableRegistry.GetJsonPathForAssetPath(document.sourceAssetPath);
                    document.dirty = false;
                    message = "Saved data table asset and synced JSON: " + document.sourceJsonPath;
                    return true;
                }

                if (string.IsNullOrEmpty(document.sourceJsonPath))
                {
                    message = "Data table JSON path is missing.";
                    return false;
                }

                string directory = Path.GetDirectoryName(document.sourceJsonPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(document.sourceJsonPath, document.ToJson());
                AssetDatabase.ImportAsset(document.sourceJsonPath);
                AssetDatabase.SaveAssets();
                BlueprintDataTableRegistry.Refresh();
                document.dirty = false;
                message = "Saved data table JSON: " + document.sourceJsonPath;
                return true;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }
        }

        internal static List<string> GetVisibleColumnNames(BlueprintDataTableEditorDocument document)
        {
            List<string> columns = new List<string>();
            columns.Add("rowName");

            BlueprintUserStructDefinition definition;
            if (TryGetRowStructDefinition(document, out definition))
            {
                for (int i = 0; i < definition.Fields.Count; i++)
                {
                    BlueprintUserStructField field = definition.Fields[i];
                    if (field != null && !field.Deprecated)
                    {
                        columns.Add(field.Name);
                    }
                }
            }
            else
            {
                columns.Add("Value JSON");
            }

            return columns;
        }

        internal static bool TryGetRowStructDefinition(
            BlueprintDataTableEditorDocument document,
            out BlueprintUserStructDefinition definition)
        {
            definition = null;
            return document != null &&
                   !string.IsNullOrEmpty(document.rowStructTypeId) &&
                   BlueprintUserStructRegistry.TryGet(document.rowStructTypeId, out definition);
        }

        private static BlueprintDataTableEditorDocument CreateDocument()
        {
            BlueprintDataTableEditorDocument document = CreateInstance<BlueprintDataTableEditorDocument>();
            document.hideFlags = HideFlags.HideAndDontSave;
            return document;
        }

        private static List<BlueprintDataTableAssetRow> CopyRows(IList<BlueprintDataTableAssetRow> sourceRows)
        {
            List<BlueprintDataTableAssetRow> result = new List<BlueprintDataTableAssetRow>();
            if (sourceRows == null)
            {
                return result;
            }

            for (int i = 0; i < sourceRows.Count; i++)
            {
                BlueprintDataTableAssetRow source = sourceRows[i];
                BlueprintDataTableAssetRow copy = new BlueprintDataTableAssetRow();
                copy.rowName = source == null ? string.Empty : source.rowName;
                copy.valueJson = source == null ? string.Empty : source.valueJson;
                result.Add(copy);
            }

            return result;
        }

        private static bool TryGetRow(
            BlueprintDataTableEditorDocument document,
            int rowIndex,
            out BlueprintDataTableAssetRow row,
            out string error)
        {
            row = null;
            error = null;
            if (document == null)
            {
                error = "No data table is loaded.";
                return false;
            }

            if (document.rows == null || rowIndex < 0 || rowIndex >= document.rows.Count)
            {
                error = "Row index is out of range.";
                return false;
            }

            row = document.rows[rowIndex];
            if (row == null)
            {
                error = "Row is missing.";
                return false;
            }

            return true;
        }

        private static void MarkDocumentDirty(BlueprintDataTableEditorDocument document)
        {
            if (document == null)
            {
                return;
            }

            document.dirty = true;
            EditorUtility.SetDirty(document);
        }

        private static string GetSingleSelectedAssetPath()
        {
            UnityEngine.Object[] objects = Selection.objects;
            if (objects == null || objects.Length != 1 || objects[0] == null)
            {
                return null;
            }

            return AssetDatabase.GetAssetPath(objects[0]);
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += RepaintAfterUndo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RepaintAfterUndo;
            if (_document != null)
            {
                DestroyImmediate(_document);
                _document = null;
            }
        }

        private void RepaintAfterUndo()
        {
            Repaint();
        }

        private void LoadDocument(BlueprintDataTableEditorDocument document)
        {
            if (_document != null && _document != document)
            {
                DestroyImmediate(_document);
            }

            _document = document;
            _selectedRow = -1;
            _selectedColumn = -1;
            _tableScroll = Vector2.zero;
            _statusText = "Loaded " + (_document == null ? "data table." : _document.DisplayPath);
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_document == null)
            {
                EditorGUILayout.HelpBox("Open a BlueprintDataTableAsset or .bpdatatable.json asset.", MessageType.Info);
                return;
            }

            DrawDocumentHeader();
            DrawValidation();
            DrawTable();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Open Selected", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                if (!TryOpenSelectedDataTable())
                {
                    _statusText = "Select a BlueprintDataTableAsset or .bpdatatable.json TextAsset in the Project window.";
                }
            }

            if (GUILayout.Button("Import CSV", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                ImportCsv();
            }

            using (new EditorGUI.DisabledScope(_document == null))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    SaveCurrent();
                }

                if (GUILayout.Button("Undo", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    Undo.PerformUndo();
                }

                if (GUILayout.Button("Redo", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    Undo.PerformRedo();
                }
            }

            GUILayout.FlexibleSpace();
            string label = _document == null ? "No file" : (_document.dirty ? "* " : string.Empty) + _document.DisplayPath;
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDocumentHeader()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(new GUIContent("Table Id"), _document.tableId);
                }

                string nextRowStructType = DrawRowStructSelector(_document.rowStructTypeId);
                if (nextRowStructType != _document.rowStructTypeId)
                {
                    string error;
                    if (!TryApplyRowStructTypeEdit(_document, nextRowStructType, out error))
                    {
                        _statusText = error;
                    }
                }
            }

            EditorGUILayout.LabelField(_document.jsonOnly ? "Mode: JSON-only" : "Mode: DataTable asset source", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(_statusText))
            {
                EditorGUILayout.LabelField(_statusText, EditorStyles.miniLabel);
            }
        }

        private static string DrawRowStructSelector(string currentValue)
        {
            string[] typeIds = BlueprintUserStructRegistry.GetTypeIds();
            if (typeIds.Length == 0)
            {
                return EditorGUILayout.TextField(new GUIContent("Row Struct Type Id"), currentValue);
            }

            int selected = -1;
            for (int i = 0; i < typeIds.Length; i++)
            {
                if (typeIds[i] == currentValue)
                {
                    selected = i;
                    break;
                }
            }

            if (selected < 0)
            {
                return EditorGUILayout.TextField(new GUIContent("Row Struct Type Id"), currentValue);
            }

            int nextSelected = EditorGUILayout.Popup(new GUIContent("Row Struct Type Id"), selected, typeIds);
            return typeIds[Mathf.Clamp(nextSelected, 0, typeIds.Length - 1)];
        }

        private void DrawValidation()
        {
            List<string> errors = BlueprintDataTableAssetEditor.Validate(
                _document.tableId,
                _document.rowStructTypeId,
                _document.rows);
            if (errors.Count == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(string.Join("\n", errors.ToArray()), MessageType.Warning);
        }

        private void DrawTable()
        {
            BlueprintUserStructDefinition definition;
            bool hasRowStruct = TryGetRowStructDefinition(_document, out definition);
            List<BlueprintUserStructField> fields = hasRowStruct
                ? GetEditableFields(definition)
                : new List<BlueprintUserStructField>();

            float totalWidth = RowNameColumnWidth + (hasRowStruct ? Mathf.Max(1, fields.Count) * FieldColumnWidth : ValueJsonColumnWidth);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rows", EditorStyles.boldLabel);
            _tableScroll = EditorGUILayout.BeginScrollView(_tableScroll, GUILayout.ExpandHeight(true));

            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(totalWidth), GUILayout.Height(HeaderHeight)))
            {
                DrawHeaderCell("rowName", RowNameColumnWidth);
                if (hasRowStruct)
                {
                    for (int i = 0; i < fields.Count; i++)
                    {
                        DrawHeaderCell(fields[i].Name, FieldColumnWidth);
                    }
                }
                else
                {
                    DrawHeaderCell("Value JSON", ValueJsonColumnWidth);
                }
            }

            if (_document.rows != null)
            {
                for (int rowIndex = 0; rowIndex < _document.rows.Count; rowIndex++)
                {
                    BlueprintDataTableAssetRow row = _document.rows[rowIndex];
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(totalWidth), GUILayout.Height(CellHeight)))
                    {
                        DrawCell(rowIndex, 0, row == null ? string.Empty : row.rowName, RowNameColumnWidth, null, false);
                        if (hasRowStruct)
                        {
                            for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                            {
                                BlueprintUserStructField field = fields[fieldIndex];
                                DrawCell(
                                    rowIndex,
                                    fieldIndex + 1,
                                    FormatFieldValue(row, field),
                                    FieldColumnWidth,
                                    field,
                                    false);
                            }
                        }
                        else
                        {
                            DrawCell(rowIndex, 1, row == null ? string.Empty : row.valueJson, ValueJsonColumnWidth, null, true);
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static List<BlueprintUserStructField> GetEditableFields(BlueprintUserStructDefinition definition)
        {
            List<BlueprintUserStructField> fields = new List<BlueprintUserStructField>();
            if (definition == null)
            {
                return fields;
            }

            for (int i = 0; i < definition.Fields.Count; i++)
            {
                BlueprintUserStructField field = definition.Fields[i];
                if (field != null && !field.Deprecated)
                {
                    fields.Add(field);
                }
            }

            return fields;
        }

        private static void DrawHeaderCell(string label, float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, HeaderHeight, GUILayout.Width(width), GUILayout.Height(HeaderHeight));
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbarButton);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f), label, EditorStyles.boldLabel);
        }

        private void DrawCell(
            int rowIndex,
            int columnIndex,
            string text,
            float width,
            BlueprintUserStructField field,
            bool valueJsonCell)
        {
            Rect rect = GUILayoutUtility.GetRect(width, CellHeight, GUILayout.Width(width), GUILayout.Height(CellHeight));
            bool selected = _selectedRow == rowIndex && _selectedColumn == columnIndex;
            GUI.Box(rect, GUIContent.none, "box");
            if (selected)
            {
                EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), new Color(0.24f, 0.48f, 0.9f, 0.22f));
            }

            GUI.Label(new Rect(rect.x + 5f, rect.y + 3f, rect.width - 10f, rect.height - 6f), text ?? string.Empty, EditorStyles.label);

            Event current = Event.current;
            if (current.type != EventType.MouseDown || !rect.Contains(current.mousePosition))
            {
                return;
            }

            _selectedRow = rowIndex;
            _selectedColumn = columnIndex;
            if (current.clickCount == 2)
            {
                OpenCellEditor(rect, rowIndex, columnIndex, field, valueJsonCell);
            }

            current.Use();
            Repaint();
        }

        private string FormatFieldValue(BlueprintDataTableAssetRow row, BlueprintUserStructField field)
        {
            if (row == null || field == null)
            {
                return string.Empty;
            }

            object value = GetFieldValue(row, field);
            if (value == null)
            {
                return string.Empty;
            }

            string stringValue = value as string;
            if (stringValue != null)
            {
                return stringValue;
            }

            IList list = value as IList;
            if (list != null)
            {
                List<string> parts = new List<string>();
                for (int i = 0; i < list.Count; i++)
                {
                    parts.Add(Convert.ToString(list[i], CultureInfo.InvariantCulture));
                }

                return "[" + string.Join(", ", parts.ToArray()) + "]";
            }

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                return BlueprintJson.Serialize(value, false);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private object GetFieldValue(BlueprintDataTableAssetRow row, BlueprintUserStructField field)
        {
            string error;
            Dictionary<string, object> values = BlueprintDataTableAssetEditor.ReadEditableRowValue(
                row.valueJson,
                _document.rowStructTypeId,
                out error);
            object value;
            if (!values.TryGetValue(field.Name, out value))
            {
                value = field.DefaultValue;
            }

            return value;
        }

        private void OpenCellEditor(
            Rect activatorRect,
            int rowIndex,
            int columnIndex,
            BlueprintUserStructField field,
            bool valueJsonCell)
        {
            BlueprintDataTableAssetRow row = _document.rows[rowIndex];
            Vector2 screenPoint = GUIUtility.GUIToScreenPoint(new Vector2(activatorRect.x, activatorRect.yMax));
            Rect windowRect = new Rect(screenPoint.x, screenPoint.y, 380f, valueJsonCell ? 260f : 150f);

            if (columnIndex == 0)
            {
                BlueprintDataTableCellEditWindow.ShowCell(
                    "Edit rowName",
                    row == null ? string.Empty : row.rowName,
                    null,
                    windowRect,
                    delegate(object value)
                    {
                        string error;
                        if (!TryApplyRowNameEdit(_document, rowIndex, Convert.ToString(value, CultureInfo.InvariantCulture), out error))
                        {
                            _statusText = error;
                        }
                    });
                return;
            }

            if (valueJsonCell)
            {
                BlueprintDataTableCellEditWindow.ShowCell(
                    "Edit Value JSON",
                    row == null ? string.Empty : row.valueJson,
                    null,
                    windowRect,
                    delegate(object value)
                    {
                        string error;
                        if (!TryApplyValueJsonEdit(_document, rowIndex, Convert.ToString(value, CultureInfo.InvariantCulture), out error))
                        {
                            _statusText = error;
                        }
                    });
                return;
            }

            if (field == null)
            {
                return;
            }

            object currentValue = GetFieldValue(row, field);
            BlueprintDataTableCellEditWindow.ShowCell(
                "Edit " + field.Name,
                currentValue,
                field,
                windowRect,
                delegate(object value)
                {
                    string error;
                    if (!TryApplyFieldEdit(_document, rowIndex, field, value, out error))
                    {
                        _statusText = error;
                    }
                });
        }

        private void SaveCurrent()
        {
            string message;
            if (SaveDocument(_document, out message))
            {
                _statusText = message;
            }
            else
            {
                _statusText = "Save failed: " + message;
            }
        }

        private void ImportCsv()
        {
            string csvPath = EditorUtility.OpenFilePanel("Import Blueprint Data Table CSV", string.Empty, "csv");
            if (string.IsNullOrEmpty(csvPath))
            {
                return;
            }

            string outputFolder = EditorUtility.OpenFolderPanel(
                "Choose Blueprint Data Table Output Folder",
                Application.dataPath,
                string.Empty);
            if (string.IsNullOrEmpty(outputFolder))
            {
                return;
            }

            List<string> conflicts;
            string error;
            if (!BlueprintDataTableCsvImporter.TryGetConflicts(csvPath, outputFolder, out conflicts, out error))
            {
                _statusText = "CSV import failed: " + error;
                EditorUtility.DisplayDialog("Import CSV Failed", _statusText, "OK");
                return;
            }

            bool overwrite = false;
            if (conflicts.Count > 0)
            {
                string message = "The import will overwrite these assets:\n\n" +
                                 string.Join("\n", conflicts.ToArray()) +
                                 "\n\nContinue?";
                overwrite = EditorUtility.DisplayDialog("Overwrite CSV Import", message, "Overwrite", "Cancel");
                if (!overwrite)
                {
                    _statusText = "CSV import cancelled.";
                    return;
                }
            }

            BlueprintDataTableCsvImportResult result;
            if (!BlueprintDataTableCsvImporter.ImportFromCsv(csvPath, outputFolder, overwrite, out result, out error))
            {
                _statusText = "CSV import failed: " + error;
                EditorUtility.DisplayDialog("Import CSV Failed", _statusText, "OK");
                return;
            }

            BlueprintDataTableEditorDocument document;
            if (TryCreateDocumentForPath(result.TableAssetPath, out document))
            {
                LoadDocument(document);
            }

            _statusText = "Imported CSV: " + result.TableJsonPath;
            Repaint();
        }
    }

    internal sealed class BlueprintDataTableCellEditWindow : EditorWindow
    {
        private string _title;
        private object _value;
        private string _textValue;
        private BlueprintUserStructField _field;
        private Action<object> _saveCallback;
        private Vector2 _scroll;

        internal static void ShowCell(
            string title,
            object value,
            BlueprintUserStructField field,
            Rect position,
            Action<object> saveCallback)
        {
            BlueprintDataTableCellEditWindow window = CreateInstance<BlueprintDataTableCellEditWindow>();
            window._title = title;
            window._value = value;
            window._textValue = value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
            window._field = field;
            window._saveCallback = saveCallback;
            window.titleContent = new GUIContent(title);
            window.position = position;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(_title, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (_field != null)
            {
                object editedValue;
                if (BlueprintDataTableAssetEditor.DrawFieldValue(_field, _value, out editedValue))
                {
                    _value = editedValue;
                }
            }
            else if (_title != null && _title.IndexOf("JSON", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
                _textValue = EditorGUILayout.TextArea(_textValue, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            else
            {
                _textValue = EditorGUILayout.TextField(_textValue);
            }

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Save", GUILayout.Width(84f)))
                {
                    if (_saveCallback != null)
                    {
                        _saveCallback(_field == null ? (object)_textValue : _value);
                    }

                    Close();
                }

                if (GUILayout.Button("Cancel", GUILayout.Width(84f)))
                {
                    Close();
                }
            }
        }
    }
}
