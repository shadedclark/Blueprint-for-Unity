using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    public static class BehaviorTreeCompiledAssetCompiler
    {
        private const string BehaviorTreeAssetSuffix = ".btree";
        private const string BehaviorTreeJsonAssetSuffix = ".btree.json";
        private const string CompiledAssetSuffix = ".compiled.asset";

        [MenuItem("Tools/Blueprint System/Behavior Tree/Compile Selected Behavior Tree")]
        public static void CompileSelectedBehaviorTree()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            BehaviorTreeCompiledAsset compiledAsset;
            if (CompileBehaviorTreeAtPath(path, true, out compiledAsset))
            {
                Selection.activeObject = compiledAsset;
            }
        }

        [MenuItem("Tools/Blueprint System/Behavior Tree/Compile Selected Behavior Tree", true)]
        private static bool CanCompileSelectedBehaviorTree()
        {
            return IsBehaviorTreeJsonPath(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [MenuItem("Assets/Blueprint System/Compile Behavior Tree", false, 2125)]
        public static void CompileSelectedBehaviorTreeAssetMenu()
        {
            CompileSelectedBehaviorTree();
        }

        [MenuItem("Assets/Blueprint System/Compile Behavior Tree", true)]
        private static bool CanCompileSelectedBehaviorTreeAssetMenu()
        {
            return CanCompileSelectedBehaviorTree();
        }

        public static bool CompileBehaviorTreeAtPath(string sourcePath, bool log, out BehaviorTreeCompiledAsset compiledAsset)
        {
            compiledAsset = null;
            sourcePath = NormalizeAssetPath(sourcePath);
            if (!IsBehaviorTreeJsonPath(sourcePath))
            {
                if (log)
                {
                    Debug.LogError("[BehaviorTree] Select a .btree.json or .btree TextAsset before compiling.");
                }

                return false;
            }

            TextAsset sourceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
            if (sourceAsset == null)
            {
                if (log)
                {
                    Debug.LogError("[BehaviorTree] Could not load behavior tree JSON at '" + sourcePath + "'.");
                }

                return false;
            }

            return CompileBehaviorTree(sourceAsset, log, out compiledAsset);
        }

        public static bool CompileBehaviorTree(TextAsset sourceAsset, bool log, out BehaviorTreeCompiledAsset compiledAsset)
        {
            compiledAsset = null;
            if (sourceAsset == null)
            {
                return false;
            }

            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(sourceAsset));
            if (string.IsNullOrEmpty(sourcePath))
            {
                if (log)
                {
                    Debug.LogError("[BehaviorTree] Behavior tree JSON must be an asset before it can be compiled.", sourceAsset);
                }

                return false;
            }

            CompilationData data;
            if (!TryBuildCompilationData(sourceAsset, sourcePath, log, out data))
            {
                return false;
            }

            string assetPath = GetCompiledAssetPath(sourcePath);
            bool created = false;
            compiledAsset = AssetDatabase.LoadAssetAtPath<BehaviorTreeCompiledAsset>(assetPath);
            if (compiledAsset == null)
            {
                compiledAsset = ScriptableObject.CreateInstance<BehaviorTreeCompiledAsset>();
                created = true;
            }

            compiledAsset.SetCompiledData(
                data.Source.SchemaVersion,
                data.Source.Name,
                data.SourceGuid,
                data.SourcePath,
                data.SourceHash,
                data.Source.Root,
                BehaviorTreeCompiler.BuildBlackboard(data.Source),
                BehaviorTreeCompiler.BuildNodes(data.Source),
                BehaviorTreeCompiler.BuildDecorators(data.Source),
                BehaviorTreeCompiler.BuildServices(data.Source));

            if (created)
            {
                AssetDatabase.CreateAsset(compiledAsset, assetPath);
            }

            EditorUtility.SetDirty(compiledAsset);
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.SaveAssets();

            if (log)
            {
                Debug.Log("[BehaviorTree] Compiled '" + data.Source.Name + "' to " + assetPath + ".", compiledAsset);
            }

            return true;
        }

        public static bool IsCompiledAssetCurrent(BehaviorTreeCompiledAsset compiledAsset, TextAsset sourceAsset, out string reason)
        {
            reason = null;
            if (compiledAsset == null)
            {
                reason = "Missing compiled behavior tree asset.";
                return false;
            }

            if (sourceAsset == null)
            {
                reason = "Missing source behavior tree JSON.";
                return false;
            }

            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(sourceAsset));
            CompilationData data;
            if (!TryBuildCompilationData(sourceAsset, sourcePath, false, out data))
            {
                reason = "Source behavior tree cannot be compiled.";
                return false;
            }

            if (!compiledAsset.IsCurrent(data.SourceHash))
            {
                reason = "Compiled behavior tree hash is stale.";
                return false;
            }

            return true;
        }

        public static string GetCompiledAssetSourcePath(BehaviorTreeCompiledAsset compiledAsset)
        {
            if (compiledAsset == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(compiledAsset.SourcePath))
            {
                return NormalizeAssetPath(compiledAsset.SourcePath);
            }

            return string.IsNullOrEmpty(compiledAsset.SourceGuid)
                ? null
                : AssetDatabase.GUIDToAssetPath(compiledAsset.SourceGuid);
        }

        public static string GetCompiledAssetPath(string behaviorTreePath)
        {
            string directory = Path.GetDirectoryName(behaviorTreePath);
            string fileName = Path.GetFileName(behaviorTreePath);
            if (fileName.EndsWith(BehaviorTreeJsonAssetSuffix, StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName.Substring(0, fileName.Length - BehaviorTreeJsonAssetSuffix.Length);
            }
            else if (fileName.EndsWith(BehaviorTreeAssetSuffix, StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName.Substring(0, fileName.Length - BehaviorTreeAssetSuffix.Length);
            }
            else
            {
                fileName = Path.GetFileNameWithoutExtension(behaviorTreePath);
            }

            return string.IsNullOrEmpty(directory)
                ? fileName + CompiledAssetSuffix
                : directory.Replace('\\', '/') + "/" + fileName + CompiledAssetSuffix;
        }

        public static bool IsBehaviorTreeJsonPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (path.EndsWith(BehaviorTreeJsonAssetSuffix, StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(BehaviorTreeAssetSuffix, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryBuildCompilationData(TextAsset sourceAsset, string sourcePath, bool log, out CompilationData data)
        {
            data = null;
            if (sourceAsset == null || string.IsNullOrEmpty(sourcePath))
            {
                return false;
            }

            string sourceText = sourceAsset.text;
            BehaviorTreeSource source;
            try
            {
                source = BehaviorTreeSource.FromJson(sourceText);
            }
            catch (Exception exception)
            {
                if (log)
                {
                    Debug.LogError("[BehaviorTree] Could not parse behavior tree JSON at '" + sourcePath + "': " + exception.Message, sourceAsset);
                }

                return false;
            }

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, BehaviorTreeExecutorRegistry.CreateDefault());
            if (!compileResult.Success)
            {
                if (log)
                {
                    Debug.LogError("[BehaviorTree] Compile failed for " + sourceAsset.name + "\n" + compileResult.Diagnostics.ToDisplayString(), sourceAsset);
                }

                return false;
            }

            data = new CompilationData();
            data.Source = source;
            data.SourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            data.SourcePath = sourcePath;
            data.SourceHash = ComputeHash(sourceText);
            return true;
        }

        private static string ComputeHash(string text)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }

        private sealed class CompilationData
        {
            public BehaviorTreeSource Source;
            public string SourceGuid;
            public string SourcePath;
            public string SourceHash;
        }
    }
}
