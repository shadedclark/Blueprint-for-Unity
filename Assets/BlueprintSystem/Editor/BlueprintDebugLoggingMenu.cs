using UnityEditor;

namespace BlueprintSystem.Editor
{
    [InitializeOnLoad]
    internal static class BlueprintDebugLoggingMenu
    {
        private const string EditorPrefsKey = "BlueprintSystem.DebugLogging";
        private const string MenuPath = "Tools/Blueprint System/Debug Logging";

        static BlueprintDebugLoggingMenu()
        {
            BlueprintLog.DebugEnabled = !EditorPrefs.HasKey(EditorPrefsKey) || EditorPrefs.GetBool(EditorPrefsKey, true);
        }

        [MenuItem(MenuPath)]
        private static void ToggleDebugLogging()
        {
            BlueprintLog.DebugEnabled = !BlueprintLog.DebugEnabled;
            EditorPrefs.SetBool(EditorPrefsKey, BlueprintLog.DebugEnabled);
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleDebugLoggingValidate()
        {
            Menu.SetChecked(MenuPath, BlueprintLog.DebugEnabled);
            return true;
        }
    }
}
