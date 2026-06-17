using System;
using System.Collections;
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
            return CompileBehaviorTreeAtPath(sourcePath, log, out compiledAsset, new HashSet<string>(StringComparer.Ordinal));
        }

        private static bool CompileBehaviorTreeAtPath(
            string sourcePath,
            bool log,
            out BehaviorTreeCompiledAsset compiledAsset,
            HashSet<string> compilationStack)
        {
            compiledAsset = null;
            sourcePath = NormalizeAssetPath(sourcePath);
            if (!IsBehaviorTreeJsonPath(sourcePath))
            {
                if (log)
                {
                    BlueprintLog.Error("[BehaviorTree] Select a .btree.json or .btree TextAsset before compiling.");
                }

                return false;
            }

            TextAsset sourceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
            if (sourceAsset == null)
            {
                if (log)
                {
                    BlueprintLog.Error("[BehaviorTree] Could not load behavior tree JSON at '" + sourcePath + "'.");
                }

                return false;
            }

            return CompileBehaviorTree(sourceAsset, log, out compiledAsset, compilationStack);
        }

        public static bool CompileBehaviorTree(TextAsset sourceAsset, bool log, out BehaviorTreeCompiledAsset compiledAsset)
        {
            return CompileBehaviorTree(sourceAsset, log, out compiledAsset, new HashSet<string>(StringComparer.Ordinal));
        }

        private static bool CompileBehaviorTree(
            TextAsset sourceAsset,
            bool log,
            out BehaviorTreeCompiledAsset compiledAsset,
            HashSet<string> compilationStack)
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
                    BlueprintLog.Error("[BehaviorTree] Behavior tree JSON must be an asset before it can be compiled.", sourceAsset);
                }

                return false;
            }

            CompilationData data;
            if (compilationStack.Contains(sourcePath))
            {
                if (log)
                {
                    BlueprintLog.Error("[BehaviorTree] Subtree cycle detected at '" + sourcePath + "'.", sourceAsset);
                }

                return false;
            }

            compilationStack.Add(sourcePath);
            if (!TryBuildCompilationData(sourceAsset, sourcePath, log, compilationStack, out data))
            {
                compilationStack.Remove(sourcePath);
                return false;
            }

            compilationStack.Remove(sourcePath);

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
                data.Components,
                data.Nodes,
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
                BlueprintLog.Log("[BehaviorTree] Compiled '" + data.Source.Name + "' to " + assetPath + ".", compiledAsset);
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
            if (!TryBuildCompilationData(sourceAsset, sourcePath, false, new HashSet<string>(StringComparer.Ordinal), out data))
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

        private static bool TryBuildCompilationData(
            TextAsset sourceAsset,
            string sourcePath,
            bool log,
            HashSet<string> compilationStack,
            out CompilationData data)
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
                    BlueprintLog.Error("[BehaviorTree] Could not parse behavior tree JSON at '" + sourcePath + "': " + exception.Message, sourceAsset);
                }

                return false;
            }

            BehaviorTreeCompileResult compileResult = new BehaviorTreeCompiler().Compile(source, BehaviorTreeExecutorRegistry.CreateDefault());
            if (!compileResult.Success)
            {
                if (log)
                {
                    BlueprintLog.Error("[BehaviorTree] Compile failed for " + sourceAsset.name + "\n" + compileResult.Diagnostics.ToDisplayString(), sourceAsset);
                }

                return false;
            }

            List<BehaviorTreeCompiledComponent> components;
            string componentHash;
            if (!BuildSubtreeComponents(source, sourcePath, log, compilationStack, out components, out componentHash))
            {
                return false;
            }

            data = new CompilationData();
            data.Source = source;
            data.SourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            data.SourcePath = sourcePath;
            data.SourceHash = components.Count == 0
                ? ComputeHash(sourceText)
                : ComputeHash(sourceText + "\ncomponents:" + componentHash);
            data.Components = components;
            data.Nodes = BehaviorTreeCompiler.BuildNodes(source);
            return true;
        }

        private static bool BuildSubtreeComponents(
            BehaviorTreeSource source,
            string sourcePath,
            bool log,
            HashSet<string> compilationStack,
            out List<BehaviorTreeCompiledComponent> components,
            out string componentHash)
        {
            components = new List<BehaviorTreeCompiledComponent>();
            componentHash = string.Empty;
            StringBuilder hashBuilder = new StringBuilder();
            if (source == null)
            {
                return true;
            }

            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BehaviorTreeNodeSource node = source.Nodes[i];
                if (node == null || node.TypeId != "BT.RunSubtree")
                {
                    continue;
                }

                string behaviorTreePath = GetStringProperty(node.Properties, "behaviorTree", null);
                string subtreeSourcePath;
                BehaviorTreeCompiledAsset compiledSubtree;
                if (!TryCompileSubtree(sourcePath, behaviorTreePath, log, compilationStack, out subtreeSourcePath, out compiledSubtree))
                {
                    return false;
                }

                if (!ValidateSubtreeNode(source, node, compiledSubtree, log))
                {
                    return false;
                }

                string normalizedSubtreeSourcePath = NormalizeAssetPath(string.IsNullOrEmpty(subtreeSourcePath)
                    ? GetCompiledAssetSourcePath(compiledSubtree)
                    : subtreeSourcePath);

                components.Add(new BehaviorTreeCompiledComponent
                {
                    Name = node.Id,
                    BehaviorTreePath = normalizedSubtreeSourcePath,
                    BehaviorTreeGuid = string.IsNullOrEmpty(normalizedSubtreeSourcePath) ? null : AssetDatabase.AssetPathToGUID(normalizedSubtreeSourcePath),
                    Required = true,
                    CompiledBehaviorTree = compiledSubtree
                });

                hashBuilder.Append(node.Id);
                hashBuilder.Append('|');
                hashBuilder.Append(normalizedSubtreeSourcePath ?? string.Empty);
                hashBuilder.Append('|');
                hashBuilder.Append(compiledSubtree == null ? string.Empty : compiledSubtree.SourceHash ?? string.Empty);
                hashBuilder.Append('\n');
            }

            componentHash = ComputeHash(hashBuilder.ToString());
            return true;
        }

        private static bool TryCompileSubtree(
            string ownerSourcePath,
            string behaviorTreePath,
            bool log,
            HashSet<string> compilationStack,
            out string subtreeSourcePath,
            out BehaviorTreeCompiledAsset compiledSubtree)
        {
            subtreeSourcePath = null;
            compiledSubtree = null;
            string assetPath = ResolveSubtreeAssetPath(ownerSourcePath, behaviorTreePath);
            if (string.IsNullOrEmpty(assetPath))
            {
                LogSubtreeError(log, "BT.RunSubtree requires behaviorTree.");
                return false;
            }

            if (assetPath.EndsWith(CompiledAssetSuffix, StringComparison.OrdinalIgnoreCase))
            {
                BehaviorTreeCompiledAsset existingCompiled = AssetDatabase.LoadAssetAtPath<BehaviorTreeCompiledAsset>(assetPath);
                if (existingCompiled == null)
                {
                    LogSubtreeError(log, "Could not load compiled subtree asset at '" + assetPath + "'.");
                    return false;
                }

                subtreeSourcePath = GetCompiledAssetSourcePath(existingCompiled);
                if (string.IsNullOrEmpty(subtreeSourcePath))
                {
                    LogSubtreeError(log, "Compiled subtree asset '" + assetPath + "' is missing sourcePath/sourceGuid.");
                    return false;
                }
            }
            else
            {
                subtreeSourcePath = assetPath;
            }

            subtreeSourcePath = NormalizeAssetPath(subtreeSourcePath);
            if (!IsBehaviorTreeJsonPath(subtreeSourcePath))
            {
                LogSubtreeError(log, "Subtree path must reference a .btree.json, .btree, or .compiled.asset asset: '" + subtreeSourcePath + "'.");
                return false;
            }

            return CompileBehaviorTreeAtPath(subtreeSourcePath, log, out compiledSubtree, compilationStack);
        }

        private static bool ValidateSubtreeNode(
            BehaviorTreeSource parentSource,
            BehaviorTreeNodeSource node,
            BehaviorTreeCompiledAsset compiledSubtree,
            bool log)
        {
            if (compiledSubtree == null)
            {
                LogSubtreeError(log, "BT.RunSubtree could not resolve compiled subtree for node '" + (node == null ? string.Empty : node.Id) + "'.");
                return false;
            }

            string mode = GetStringProperty(node.Properties, "blackboardMode", "Shared");
            if (string.IsNullOrEmpty(mode) || string.Equals(mode, "Shared", StringComparison.OrdinalIgnoreCase))
            {
                return ValidateSharedSubtreeBlackboard(parentSource, node, compiledSubtree, log);
            }

            if (string.Equals(mode, "Isolated", StringComparison.OrdinalIgnoreCase))
            {
                return ValidateIsolatedSubtreeMappings(parentSource, node, compiledSubtree, log);
            }

            LogSubtreeError(log, "BT.RunSubtree blackboardMode must be Shared or Isolated on node '" + node.Id + "'.");
            return false;
        }

        private static bool ValidateSharedSubtreeBlackboard(
            BehaviorTreeSource parentSource,
            BehaviorTreeNodeSource node,
            BehaviorTreeCompiledAsset compiledSubtree,
            bool log)
        {
            Dictionary<string, string> parentTypes = BuildParentBlackboardTypeMap(parentSource);
            for (int i = 0; i < compiledSubtree.Blackboard.Count; i++)
            {
                BehaviorTreeCompiledBlackboardKey childKey = compiledSubtree.Blackboard[i];
                if (childKey == null || string.IsNullOrEmpty(childKey.Name))
                {
                    continue;
                }

                string parentType;
                if (parentTypes.TryGetValue(childKey.Name, out parentType) &&
                    !string.Equals(parentType ?? string.Empty, childKey.Type ?? string.Empty, StringComparison.Ordinal))
                {
                    LogSubtreeError(log, "BT.RunSubtree shared Blackboard key '" + childKey.Name + "' has conflicting types on node '" + node.Id + "'.");
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateIsolatedSubtreeMappings(
            BehaviorTreeSource parentSource,
            BehaviorTreeNodeSource node,
            BehaviorTreeCompiledAsset compiledSubtree,
            bool log)
        {
            Dictionary<string, string> parentTypes = BuildParentBlackboardTypeMap(parentSource);
            Dictionary<string, string> childTypes = BuildCompiledBlackboardTypeMap(compiledSubtree);
            if (!ValidateMappingKeys(node, "inputMappings", parentTypes, childTypes, log))
            {
                return false;
            }

            return ValidateMappingKeys(node, "outputMappings", childTypes, parentTypes, log);
        }

        private static bool ValidateMappingKeys(
            BehaviorTreeNodeSource node,
            string propertyName,
            Dictionary<string, string> sourceTypes,
            Dictionary<string, string> targetTypes,
            bool log)
        {
            List<Dictionary<string, object>> mappings = GetObjectArrayProperty(node.Properties, propertyName);
            for (int i = 0; i < mappings.Count; i++)
            {
                string sourceKey = GetStringProperty(mappings[i], "sourceKey", null);
                string targetKey = GetStringProperty(mappings[i], "targetKey", null);
                if (string.IsNullOrEmpty(sourceKey) || string.IsNullOrEmpty(targetKey))
                {
                    LogSubtreeError(log, "BT.RunSubtree " + propertyName + " entries require sourceKey and targetKey on node '" + node.Id + "'.");
                    return false;
                }

                string sourceType;
                if (!sourceTypes.TryGetValue(sourceKey, out sourceType))
                {
                    LogSubtreeError(log, "BT.RunSubtree " + propertyName + " sourceKey '" + sourceKey + "' is missing on node '" + node.Id + "'.");
                    return false;
                }

                string targetType;
                if (!targetTypes.TryGetValue(targetKey, out targetType))
                {
                    LogSubtreeError(log, "BT.RunSubtree " + propertyName + " targetKey '" + targetKey + "' is missing on node '" + node.Id + "'.");
                    return false;
                }

                if (!string.Equals(sourceType ?? string.Empty, targetType ?? string.Empty, StringComparison.Ordinal))
                {
                    LogSubtreeError(log, "BT.RunSubtree " + propertyName + " maps incompatible Blackboard types on node '" + node.Id + "'.");
                    return false;
                }
            }

            return true;
        }

        private static string ResolveSubtreeAssetPath(string ownerSourcePath, string behaviorTreePath)
        {
            behaviorTreePath = NormalizeAssetPath(behaviorTreePath);
            if (string.IsNullOrEmpty(behaviorTreePath))
            {
                return null;
            }

            if (behaviorTreePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                behaviorTreePath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return behaviorTreePath;
            }

            string directory = Path.GetDirectoryName(ownerSourcePath);
            return NormalizeAssetPath(string.IsNullOrEmpty(directory)
                ? behaviorTreePath
                : Path.Combine(directory, behaviorTreePath));
        }

        private static Dictionary<string, string> BuildParentBlackboardTypeMap(BehaviorTreeSource source)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (source == null)
            {
                return result;
            }

            for (int i = 0; i < source.Blackboard.Count; i++)
            {
                BehaviorTreeBlackboardKey key = source.Blackboard[i];
                if (key != null && !string.IsNullOrEmpty(key.Name))
                {
                    result[key.Name] = key.Type;
                }
            }

            return result;
        }

        private static Dictionary<string, string> BuildCompiledBlackboardTypeMap(BehaviorTreeCompiledAsset compiledAsset)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (compiledAsset == null)
            {
                return result;
            }

            for (int i = 0; i < compiledAsset.Blackboard.Count; i++)
            {
                BehaviorTreeCompiledBlackboardKey key = compiledAsset.Blackboard[i];
                if (key != null && !string.IsNullOrEmpty(key.Name))
                {
                    result[key.Name] = key.Type;
                }
            }

            return result;
        }

        private static List<Dictionary<string, object>> GetObjectArrayProperty(Dictionary<string, object> properties, string key)
        {
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            if (properties == null)
            {
                return result;
            }

            object value;
            if (!properties.TryGetValue(key, out value) || value == null || value is string)
            {
                return result;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return result;
            }

            foreach (object item in enumerable)
            {
                Dictionary<string, object> dictionary = item as Dictionary<string, object>;
                if (dictionary != null)
                {
                    result.Add(dictionary);
                }
            }

            return result;
        }

        private static string GetStringProperty(Dictionary<string, object> properties, string key, string defaultValue)
        {
            object value;
            if (properties == null || !properties.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            return Convert.ToString(value);
        }

        private static void LogSubtreeError(bool log, string message)
        {
            if (log)
            {
                BlueprintLog.Error("[BehaviorTree] " + message);
            }
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
            public List<BehaviorTreeCompiledComponent> Components;
            public List<BehaviorTreeCompiledNode> Nodes;
        }
    }
}
