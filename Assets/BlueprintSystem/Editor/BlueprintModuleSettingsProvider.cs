using System;
using System.Collections.Generic;
using System.IO;
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

    internal static class BlueprintCodexSettingsProvider
    {
        private const string SettingsPath = "Project/Blueprint System/Codex";
        private const string PackageName = "com.shadedclark.blueprint-system";
        private const string InstallScriptRelativePath = "CodexPlugin~/scripts/install_blueprint_codex_plugin.py";

        private static string lastStatusMessage;
        private static string lastOutput;
        private static MessageType lastStatusType = MessageType.None;

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Codex",
                guiHandler = DrawSettings,
                keywords = new HashSet<string>(new[] { "Blueprint", "Codex", "Skill", "Skills", "Plugin", "Install", "Marketplace" })
            };
        }

        private static void DrawSettings(string searchContext)
        {
            EditorGUILayout.LabelField("Blueprint System Codex Skills", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Install or refresh the BlueprintSystem Codex companion plugin for this Unity project. " +
                "This runs the bundled Python installer, refreshes skill cachebusters, registers the project marketplace with Codex, and opens the Codex plugin page when registration succeeds.",
                MessageType.Info);

            string projectRoot = GetProjectRoot();
            string scriptPath = FindInstallScript();
            EditorGUILayout.LabelField("Unity Project Root", projectRoot);

            if (string.IsNullOrEmpty(scriptPath))
            {
                EditorGUILayout.HelpBox(
                    "Could not find " + InstallScriptRelativePath + " in this project, the package path, or Library/PackageCache.",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.LabelField("Installer Script", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(scriptPath, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            EditorGUILayout.Space(8f);
            EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling || string.IsNullOrEmpty(scriptPath));
            if (GUILayout.Button("Install / Refresh Codex Skills", GUILayout.Height(28f)))
            {
                RunInstaller(scriptPath, projectRoot);
            }
            EditorGUI.EndDisabledGroup();

            if (EditorApplication.isCompiling)
            {
                EditorGUILayout.HelpBox("Wait for script compilation to finish before installing Codex skills.", MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(lastStatusMessage))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(lastStatusMessage, lastStatusType);
            }

            if (!string.IsNullOrEmpty(lastOutput))
            {
                EditorGUILayout.LabelField("Last Installer Output", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(lastOutput, GUILayout.MinHeight(80f));
            }
        }

        private static string FindInstallScript()
        {
            List<string> candidates = new List<string>();
            string projectRoot = GetProjectRoot();

            AddCandidate(candidates, Path.Combine(projectRoot, "Assets", "BlueprintSystem", InstallScriptRelativePath));
            AddPackageInfoCandidate(candidates);
            AddCandidate(candidates, Path.Combine(projectRoot, "Packages", PackageName, InstallScriptRelativePath));
            AddPackageCacheCandidates(candidates, projectRoot);

            for (int i = 0; i < candidates.Count; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return NormalizePath(candidates[i]);
                }
            }

            return string.Empty;
        }

        private static void AddPackageInfoCandidate(List<string> candidates)
        {
            try
            {
                UnityEditor.PackageManager.PackageInfo packageInfo =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BlueprintCodexSettingsProvider).Assembly);
                if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
                {
                    AddCandidate(candidates, Path.Combine(packageInfo.resolvedPath, InstallScriptRelativePath));
                }
            }
            catch (Exception)
            {
                // Some asset-based installs are not backed by PackageInfo.
            }
        }

        private static void AddPackageCacheCandidates(List<string> candidates, string projectRoot)
        {
            string packageCache = Path.Combine(projectRoot, "Library", "PackageCache");
            if (!Directory.Exists(packageCache))
            {
                return;
            }

            try
            {
                string[] packageFolders = Directory.GetDirectories(packageCache, PackageName + "*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < packageFolders.Length; i++)
                {
                    AddCandidate(candidates, Path.Combine(packageFolders[i], InstallScriptRelativePath));
                }
            }
            catch (Exception)
            {
                // PackageCache may be unavailable while Unity is refreshing packages.
            }
        }

        private static void AddCandidate(List<string> candidates, string path)
        {
            string normalized = NormalizePath(path);
            if (!candidates.Contains(normalized))
            {
                candidates.Add(normalized);
            }
        }

        private static void RunInstaller(string scriptPath, string projectRoot)
        {
            InstallerProcessResult result = RunPythonInstaller(scriptPath, projectRoot);
            lastOutput = BuildLastOutput(result);

            if (!result.Started)
            {
                lastStatusType = MessageType.Error;
                lastStatusMessage = "Could not start Python. Install Python 3 or make python3/python available on PATH.";
                EditorUtility.DisplayDialog("Install Codex Skills Failed", lastStatusMessage + "\n\n" + result.Error, "OK");
                return;
            }

            if (result.ExitCode != 0)
            {
                lastStatusType = MessageType.Error;
                lastStatusMessage = "Codex skill installer failed with exit code " + result.ExitCode + ".";
                EditorUtility.DisplayDialog("Install Codex Skills Failed", lastStatusMessage + "\n\n" + result.Error, "OK");
                UnityEngine.Debug.LogError("[BlueprintSystem] Codex skill installer failed:\n" + lastOutput);
                return;
            }

            InstallerSummary summary = ParseInstallerSummary(result.Output);
            if (!summary.ScriptSucceeded)
            {
                lastStatusType = MessageType.Warning;
                lastStatusMessage = "Codex skill installer finished, but its JSON summary could not be parsed. Check the output below.";
                EditorUtility.DisplayDialog("Install Codex Skills", lastStatusMessage, "OK");
                UnityEngine.Debug.LogWarning("[BlueprintSystem] Codex skill installer output could not be parsed:\n" + lastOutput);
                return;
            }

            if (!summary.MarketplaceRegistered)
            {
                lastStatusType = MessageType.Warning;
                lastStatusMessage =
                    "BlueprintSystem Codex skills were copied, but Codex marketplace registration did not report success. " +
                    "Check the installer output below and make sure the Codex CLI is available on PATH.";
                EditorUtility.DisplayDialog("Install Codex Skills", lastStatusMessage, "OK");
                UnityEngine.Debug.LogWarning("[BlueprintSystem] Codex skill installer completed with registration warning:\n" + lastOutput);
                return;
            }

            lastStatusType = MessageType.Info;
            lastStatusMessage = summary.Message;
            EditorUtility.DisplayDialog("Install Codex Skills", summary.Message, "OK");
            UnityEngine.Debug.Log("[BlueprintSystem] Codex skills installed:\n" + lastOutput);
        }

        private static InstallerProcessResult RunPythonInstaller(string scriptPath, string projectRoot)
        {
            string[] pythonCommands = Application.platform == RuntimePlatform.WindowsEditor
                ? new[] { "python", "py" }
                : new[] { "python3", "python" };

            InstallerProcessResult lastResult = null;
            for (int i = 0; i < pythonCommands.Length; i++)
            {
                InstallerProcessResult result = RunProcess(
                    pythonCommands[i],
                    QuoteProcessArgument(scriptPath) + " " + QuoteProcessArgument(projectRoot));
                lastResult = result;
                if (result.Started)
                {
                    return result;
                }
            }

            return lastResult ?? new InstallerProcessResult
            {
                Started = false,
                Error = "No Python command was attempted."
            };
        }

        private static InstallerProcessResult RunProcess(string fileName, string arguments)
        {
            System.Diagnostics.Process process = null;
            try
            {
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = GetProjectRoot()
                };
                AddCommonToolPaths(startInfo);

                process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    return new InstallerProcessResult
                    {
                        Started = false,
                        Command = fileName + " " + arguments,
                        Error = "Process.Start returned null."
                    };
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new InstallerProcessResult
                {
                    Started = true,
                    Command = fileName + " " + arguments,
                    ExitCode = process.ExitCode,
                    Output = output.Trim(),
                    Error = error.Trim()
                };
            }
            catch (Exception ex)
            {
                return new InstallerProcessResult
                {
                    Started = false,
                    Command = fileName + " " + arguments,
                    Error = ex.Message
                };
            }
            finally
            {
                if (process != null)
                {
                    process.Dispose();
                }
            }
        }

        private static void AddCommonToolPaths(System.Diagnostics.ProcessStartInfo startInfo)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return;
            }

            string path = startInfo.EnvironmentVariables["PATH"] ?? string.Empty;
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] extraPaths =
            {
                "/opt/homebrew/bin",
                "/usr/local/bin",
                "/usr/bin",
                "/bin",
                "/usr/sbin",
                "/sbin",
                Path.Combine(home, ".local", "bin"),
                Path.Combine(home, ".npm-global", "bin"),
                Path.Combine(home, ".cargo", "bin"),
                Path.Combine(home, "bin")
            };

            for (int i = 0; i < extraPaths.Length; i++)
            {
                if (string.IsNullOrEmpty(extraPaths[i]) || path.Contains(extraPaths[i]))
                {
                    continue;
                }

                path = string.IsNullOrEmpty(path)
                    ? extraPaths[i]
                    : path + Path.PathSeparator + extraPaths[i];
            }

            startInfo.EnvironmentVariables["PATH"] = path;
        }

        private static InstallerSummary ParseInstallerSummary(string output)
        {
            InstallerSummary summary = new InstallerSummary();
            try
            {
                Newtonsoft.Json.Linq.JObject payload = Newtonsoft.Json.Linq.JObject.Parse(ExtractJsonObject(output));
                summary.ScriptSucceeded = payload.Value<bool?>("success") == true;
                summary.MarketplacePath = payload.Value<string>("marketplacePath") ?? string.Empty;
                summary.InstalledSkills = JoinStringArray(payload["installedSkills"] as Newtonsoft.Json.Linq.JArray);

                Newtonsoft.Json.Linq.JObject registration = payload["marketplaceRegistration"] as Newtonsoft.Json.Linq.JObject;
                summary.MarketplaceRegistered = registration != null && registration.Value<bool?>("success") == true;
                summary.Message = BuildSuccessMessage(summary);
            }
            catch (Exception ex)
            {
                summary.ScriptSucceeded = false;
                summary.Message = ex.Message;
            }

            return summary;
        }

        private static string ExtractJsonObject(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return output;
            }

            int start = output.IndexOf('{');
            int end = output.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return output;
            }

            return output.Substring(start, end - start + 1);
        }

        private static string BuildSuccessMessage(InstallerSummary summary)
        {
            string message = "BlueprintSystem Codex skills installed or refreshed.";
            if (!string.IsNullOrEmpty(summary.InstalledSkills))
            {
                message += "\n\nInstalled skills: " + summary.InstalledSkills;
            }

            if (!string.IsNullOrEmpty(summary.MarketplacePath))
            {
                message += "\nMarketplace: " + summary.MarketplacePath;
            }

            message += "\n\nStart a new Codex thread to load newly added or refreshed skills.";
            return message;
        }

        private static string JoinStringArray(Newtonsoft.Json.Linq.JArray array)
        {
            if (array == null || array.Count == 0)
            {
                return string.Empty;
            }

            List<string> values = new List<string>();
            for (int i = 0; i < array.Count; i++)
            {
                string value = array[i]?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    values.Add(value);
                }
            }

            return string.Join(", ", values.ToArray());
        }

        private static string BuildLastOutput(InstallerProcessResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            string output = "Command: " + result.Command;
            if (!string.IsNullOrEmpty(result.Output))
            {
                output += "\n\nstdout:\n" + result.Output;
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                output += "\n\nstderr:\n" + result.Error;
            }

            return output;
        }

        private static string GetProjectRoot()
        {
            return NormalizePath(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
        }

        private static string QuoteProcessArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private sealed class InstallerProcessResult
        {
            public bool Started;
            public int ExitCode;
            public string Command = string.Empty;
            public string Output = string.Empty;
            public string Error = string.Empty;
        }

        private sealed class InstallerSummary
        {
            public bool ScriptSucceeded;
            public bool MarketplaceRegistered;
            public string InstalledSkills = string.Empty;
            public string MarketplacePath = string.Empty;
            public string Message = string.Empty;
        }
    }
}
