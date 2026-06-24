using System.IO;
using UnityEditor;
using UnityEngine;

namespace VehicleRoads.Editor
{
    public static class RoadNetworkProjectSettingsAssets
    {
        public const string SettingsFolder = "Assets/VehicleRoads/Settings";
        public const string NetworkSettingsPath = SettingsFolder + "/RoadNetworkSettings.asset";
        public const string RuntimeSettingsPath = SettingsFolder + "/RoadNetworkRuntimeSettings.asset";
        private const string ModuleSettingsFolder = "Assets/BlueprintSystem/VehicleRoads/Settings";
        private const string VehicleRoadSettingsFolderSuffix = "/VehicleRoads/Settings/";

        public static RoadNetworkSettings GetNetworkSettings(bool create)
        {
            return GetSettingsAsset<RoadNetworkSettings>(
                NetworkSettingsPath,
                "RoadNetworkSettings.asset",
                create,
                settings => settings.InitializeDefaultsIfEmpty());
        }

        public static RoadNetworkRuntimeSettings GetRuntimeSettings(bool create)
        {
            return GetSettingsAsset<RoadNetworkRuntimeSettings>(
                RuntimeSettingsPath,
                "RoadNetworkRuntimeSettings.asset",
                create,
                null);
        }

        private static T GetSettingsAsset<T>(
            string projectPath,
            string fileName,
            bool create,
            System.Action<T> initialize)
            where T : ScriptableObject
        {
            T settings = AssetDatabase.LoadAssetAtPath<T>(projectPath);
            if (settings == null)
            {
                settings = FindFallbackSettingsAsset<T>(fileName);
            }

            if (settings != null || !create)
            {
                return settings;
            }

            EnsureFolder(SettingsFolder);
            settings = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(settings);
            AssetDatabase.CreateAsset(settings, projectPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static T FindFallbackSettingsAsset<T>(string fileName)
            where T : ScriptableObject
        {
            T settings = AssetDatabase.LoadAssetAtPath<T>(ModuleSettingsFolder + "/" + fileName);
            if (settings != null)
            {
                return settings;
            }

            string[] guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName), new[] { "Packages" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                if (!path.EndsWith(VehicleRoadSettingsFolderSuffix + fileName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                settings = AssetDatabase.LoadAssetAtPath<T>(path);
                if (settings != null)
                {
                    return settings;
                }
            }

            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }

    public sealed class RoadNetworkProjectSettingsProvider : SettingsProvider
    {
        private UnityEditor.Editor networkSettingsEditor;
        private UnityEditor.Editor runtimeSettingsEditor;

        private RoadNetworkProjectSettingsProvider()
            : base("Project/Vehicle Road/Road Network", SettingsScope.Project)
        {
            keywords = new[]
            {
                "Road", "Lane", "Network", "Profiler", "Diagnostics", "History", "Tag", "Agent"
            };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new RoadNetworkProjectSettingsProvider();
        }

        public override void OnGUI(string searchContext)
        {
            DrawSettingsAsset(
                "Tag and Agent Definitions",
                RoadNetworkProjectSettingsAssets.GetNetworkSettings(false),
                () => RoadNetworkProjectSettingsAssets.GetNetworkSettings(true),
                ref networkSettingsEditor);
            EditorGUILayout.Space(12f);
            DrawSettingsAsset(
                "Runtime Profiler and Diagnostics",
                RoadNetworkProjectSettingsAssets.GetRuntimeSettings(false),
                () => RoadNetworkProjectSettingsAssets.GetRuntimeSettings(true),
                ref runtimeSettingsEditor);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "RoadLaneNetwork and VehicleRoadSubsystem keep explicit references to these assets. " +
                "If a runtime settings reference is missing, profiler markers and detailed history remain disabled.",
                MessageType.Info);
        }

        private static void DrawSettingsAsset<T>(
            string title,
            T asset,
            System.Func<T> create,
            ref UnityEditor.Editor cachedEditor)
            where T : ScriptableObject
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (asset == null)
            {
                EditorGUILayout.HelpBox("The project settings asset has not been created.", MessageType.Warning);
                if (GUILayout.Button("Create " + typeof(T).Name))
                {
                    asset = create();
                    Selection.activeObject = asset;
                }
            }

            if (asset == null)
            {
                return;
            }

            EditorGUILayout.ObjectField("Asset", asset, typeof(T), false);
            UnityEditor.Editor.CreateCachedEditor(asset, null, ref cachedEditor);
            cachedEditor.OnInspectorGUI();
        }
    }
}
