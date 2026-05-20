using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    public sealed class BlueprintEditorWindow : EditorWindow
    {
        private string _assetPath;
        private string _jsonText;
        private string _diagnosticsText = "No blueprint loaded.";
        private Vector2 _jsonScroll;
        private Vector2 _diagnosticsScroll;

        internal string CurrentAssetPath
        {
            get { return _assetPath; }
        }

        internal string CurrentJsonText
        {
            get { return _jsonText; }
        }

        [MenuItem("Tools/Blueprint System/Blueprint JSON Editor")]
        public static void Open()
        {
            GetWindow<BlueprintEditorWindow>("Blueprint JSON");
        }

        [MenuItem("Assets/Blueprint System/Open Blueprint JSON", false, 2100)]
        public static void OpenSelectedBlueprintJson()
        {
            if (!TryOpenSelectedBlueprintJson())
            {
                Debug.LogWarning("[Blueprint] Select a .blueprint.json TextAsset first.");
            }
        }

        [MenuItem("Assets/Blueprint System/Open Blueprint JSON", true)]
        private static bool CanOpenSelectedBlueprintJson()
        {
            return IsBlueprintJsonPath(GetSingleSelectedAssetPath());
        }

        [MenuItem("Tools/Blueprint System/Validate Selected Blueprint")]
        public static void ValidateSelectedBlueprint()
        {
            TextAsset asset = Selection.activeObject as TextAsset;
            string path = asset == null ? null : AssetDatabase.GetAssetPath(asset);
            if (!IsBlueprintJsonPath(path))
            {
                Debug.LogWarning("[Blueprint] Select a .blueprint.json TextAsset first.");
                return;
            }

            BlueprintDiagnosticList diagnostics = ValidateJson(asset.text);
            string message = path + "\n" + diagnostics.ToDisplayString();
            if (diagnostics.HasErrors)
            {
                Debug.LogError("[Blueprint] Validation failed:\n" + message);
            }
            else
            {
                Debug.Log("[Blueprint] Validation passed:\n" + message);
            }
        }

        [OnOpenAsset(0)]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            string path = GetAssetPathFromOpenAssetId(instanceId);
            if (IsBlueprintJsonPath(path))
            {
                return OpenAssetAtPath(path);
            }

            return OpenCompiledAssetAtPath(path);
        }

        internal static string GetAssetPathFromOpenAssetId(int instanceId)
        {
#if UNITY_6000_3_OR_NEWER
            string entityPath = AssetDatabase.GetAssetPath((EntityId)instanceId);
            if (!string.IsNullOrEmpty(entityPath))
            {
                return entityPath;
            }
#endif

#pragma warning disable 0618
            Object asset = EditorUtility.InstanceIDToObject(instanceId);
#pragma warning restore 0618
            return asset == null ? null : AssetDatabase.GetAssetPath(asset);
        }

        internal static bool OpenAssetAtPath(string assetPath)
        {
            if (!IsBlueprintJsonPath(assetPath))
            {
                return false;
            }

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null)
            {
                return false;
            }

            BlueprintEditorWindow window = GetWindow<BlueprintEditorWindow>("Blueprint JSON");
            window.LoadAsset(assetPath, asset.text);
            window.Focus();
            return true;
        }

        internal static bool OpenCompiledAssetAtPath(string assetPath)
        {
            BlueprintCompiledAsset compiledAsset = AssetDatabase.LoadAssetAtPath<BlueprintCompiledAsset>(assetPath);
            if (compiledAsset == null)
            {
                return false;
            }

            string sourcePath = BlueprintCompiledAssetCompiler.GetCompiledAssetSourcePath(compiledAsset);
            if (OpenAssetAtPath(sourcePath))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(compiledAsset.SourceGuid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(compiledAsset.SourceGuid);
                if (!string.Equals(guidPath, sourcePath, System.StringComparison.OrdinalIgnoreCase))
                {
                    return OpenAssetAtPath(guidPath);
                }
            }

            return false;
        }

        internal static bool TryOpenSelectedBlueprintJson()
        {
            return OpenAssetAtPath(GetSingleSelectedAssetPath());
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Open Selected", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                if (!TryOpenSelectedBlueprintJson())
                {
                    _diagnosticsText = "Select a .blueprint.json TextAsset in the Project window.";
                }
            }

            GUI.enabled = !string.IsNullOrEmpty(_assetPath);
            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                ValidateCurrent();
            }

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                SaveCurrent();
            }

            if (GUILayout.Button("Compile", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                CompileCurrent();
            }

            if (GUILayout.Button("Visual Graph", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                if (IsBlueprintJsonPath(_assetPath))
                {
                    BlueprintGraphToolkitBridge.ImportBlueprintAtPath(_assetPath, true);
                }
                else
                {
                    _diagnosticsText = "Current file must use the .blueprint.json extension to create a visual graph.";
                }
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(_assetPath) ? "No file" : _assetPath, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(position.width * 0.6f));
            EditorGUILayout.LabelField("Blueprint JSON", EditorStyles.boldLabel);
            _jsonScroll = EditorGUILayout.BeginScrollView(_jsonScroll);
            _jsonText = EditorGUILayout.TextArea(_jsonText ?? string.Empty, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.35f));
            EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);
            _diagnosticsScroll = EditorGUILayout.BeginScrollView(_diagnosticsScroll);
            EditorGUILayout.TextArea(_diagnosticsText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void LoadAsset(string assetPath, string jsonText)
        {
            _assetPath = assetPath;
            _jsonText = jsonText;
            ValidateCurrent();
        }

        private void SaveCurrent()
        {
            if (string.IsNullOrEmpty(_assetPath))
            {
                return;
            }

            File.WriteAllText(_assetPath, _jsonText ?? string.Empty);
            AssetDatabase.ImportAsset(_assetPath);
            ValidateCurrent();
        }

        private void CompileCurrent()
        {
            if (string.IsNullOrEmpty(_assetPath))
            {
                return;
            }

            SaveCurrent();

            BlueprintCompiledAsset compiledAsset;
            if (BlueprintCompiledAssetCompiler.CompileBlueprintAtPath(_assetPath, true, out compiledAsset))
            {
                _diagnosticsText = "Compiled blueprint asset:\n" + AssetDatabase.GetAssetPath(compiledAsset);
            }
            else
            {
                ValidateCurrent();
            }
        }

        private void ValidateCurrent()
        {
            BlueprintDiagnosticList diagnostics = ValidateJson(_jsonText);
            _diagnosticsText = diagnostics.ToDisplayString();
        }

        private static BlueprintDiagnosticList ValidateJson(string json)
        {
            BlueprintCompileResult result = new BlueprintCompileResult();
            try
            {
                BlueprintCompiler compiler = new BlueprintCompiler();
                BlueprintSource source = BlueprintSource.FromJson(json);
                BlueprintNodeManifestCollection manifests = LoadProjectManifests();
                BlueprintCompileResult compileResult = compiler.Compile(source, manifests, BlueprintExecutorRegistry.CreateDefault());
                return compileResult.Diagnostics;
            }
            catch (BlueprintJsonException ex)
            {
                result.Diagnostics.Add(BlueprintDiagnostic.Error("BP010", ex.Message));
                return result.Diagnostics;
            }
        }

        private static BlueprintNodeManifestCollection LoadProjectManifests()
        {
            string root = Path.Combine(Application.dataPath, "BlueprintSystem/Specs/Nodes");
            List<string> jsonTexts = new List<string>();
            if (Directory.Exists(root))
            {
                string[] files = Directory.GetFiles(root, "*.node.json", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    jsonTexts.Add(File.ReadAllText(files[i]));
                }
            }

            return BlueprintNodeManifestCollection.FromJsonTexts(jsonTexts);
        }

        internal static bool IsBlueprintJsonPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.EndsWith(".blueprint.json", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSingleSelectedAssetPath()
        {
            Object[] objects = Selection.objects;
            if (objects == null || objects.Length != 1 || objects[0] == null)
            {
                return null;
            }

            return AssetDatabase.GetAssetPath(objects[0]);
        }
    }
}
