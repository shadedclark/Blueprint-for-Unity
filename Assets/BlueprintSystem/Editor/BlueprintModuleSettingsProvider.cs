using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    internal static class BlueprintModuleSettingsProvider
    {
        private const string SettingsPath = "Project/Blueprint System/Modules";

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Modules",
                guiHandler = DrawSettings,
                keywords = new HashSet<string>(new[] { "Blueprint", "SmartObject", "BehaviorTree", "Behavior Tree", "VehicleRoads", "Vehicle Roads", "Modules" })
            };
        }

        private static void DrawSettings(string searchContext)
        {
            EditorGUILayout.LabelField("Blueprint System Modules", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            NamedBuildTarget buildTarget = GetActiveNamedBuildTarget();
            EditorGUILayout.LabelField("Active Build Target", buildTarget.TargetName);
            EditorGUILayout.HelpBox(
                "Module settings are stored as scripting define symbols for the active build target. Changing a module writes or removes " +
                BlueprintModuleSettings.DisableSmartObjectDefine +
                " / " +
                BlueprintModuleSettings.DisableBehaviorTreeDefine +
                " / " +
                BlueprintModuleSettings.DisableVehicleRoadsDefine +
                " and triggers a script recompile. Existing blueprints or behavior trees that still use a disabled module report compile or validation errors after they are recompiled.",
                MessageType.Info);

            EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling);
            DrawModuleCard(
                buildTarget,
                "SmartObject",
                "World interaction module for authoring SmartObjectComponent slots and using SmartObject.* Blueprint nodes such as FindBest, Reserve, BeginUse, and Release.",
                "When disabled, SmartObject manifests, executors, visual nodes, and debugger entry points are unavailable. Existing SmartObjectComponent instances remain serialized, but do not register at runtime.",
                BlueprintModuleSettings.DisableSmartObjectDefine);
            DrawModuleCard(
                buildTarget,
                "Behavior Tree",
                "AI decision module for .btree.json assets, BT.* executors, BehaviorTreeRunner, Behavior Tree Graph Toolkit, debugger, and BehaviorTree.* Blackboard bridge nodes.",
                "When disabled, BehaviorTree.* Blueprint nodes are filtered out, BT.* executors are not registered, editor compile/open/debug entry points are unavailable, and existing runners do not start or tick.",
                BlueprintModuleSettings.DisableBehaviorTreeDefine);
            DrawModuleCard(
                buildTarget,
                "Vehicle Roads",
                "Road, route, traffic-control, lane-change, and follower-control module for VehicleRoad.* Blueprint nodes and BT.VehicleRoad.* Behavior Tree nodes.",
                "When disabled, VehicleRoad manifests are not loaded, VehicleRoad executors are not registered, VehicleRoad Graph Toolkit nodes fall back to generic nodes, and BT.VehicleRoad.* executors are unavailable.",
                BlueprintModuleSettings.DisableVehicleRoadsDefine);
            EditorGUI.EndDisabledGroup();
        }

        private static NamedBuildTarget GetActiveNamedBuildTarget()
        {
            return NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        }

        private static void DrawModuleCard(
            NamedBuildTarget buildTarget,
            string label,
            string description,
            string disabledBehavior,
            string disableDefine)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool enabled = IsModuleEnabled(buildTarget, disableDefine);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                bool nextEnabled = EditorGUILayout.ToggleLeft(new GUIContent("Enabled"), enabled, GUILayout.Width(84f));
                if (nextEnabled != enabled)
                {
                    SetModuleEnabled(buildTarget, disableDefine, nextEnabled);
                }
            }

            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(disabledBehavior, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Disabled define: " + disableDefine, EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private static bool IsModuleEnabled(NamedBuildTarget buildTarget, string disableDefine)
        {
            List<string> defines = GetDefines(buildTarget);
            return !defines.Contains(disableDefine);
        }

        private static void SetModuleEnabled(NamedBuildTarget buildTarget, string disableDefine, bool enabled)
        {
            List<string> defines = GetDefines(buildTarget);
            bool changed = enabled
                ? defines.Remove(disableDefine)
                : AddDefine(defines, disableDefine);

            if (!changed)
            {
                return;
            }

            PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.Join(";", defines.ToArray()));
        }

        private static List<string> GetDefines(NamedBuildTarget buildTarget)
        {
            string rawDefines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            string[] parts = rawDefines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> defines = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string define = parts[i].Trim();
                if (!string.IsNullOrEmpty(define) && !defines.Contains(define))
                {
                    defines.Add(define);
                }
            }

            return defines;
        }

        private static bool AddDefine(List<string> defines, string define)
        {
            if (defines.Contains(define))
            {
                return false;
            }

            defines.Add(define);
            return true;
        }
    }
}
