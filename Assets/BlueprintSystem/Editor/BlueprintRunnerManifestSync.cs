using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    public static class BlueprintCompiledAssetCompiler
    {
        private const string CompiledAssetSuffix = ".compiled.asset";

        public static bool CompileBlueprintAtPath(string sourcePath, bool log, out BlueprintCompiledAsset compiledAsset)
        {
            return CompileBlueprintAtPath(sourcePath, log, out compiledAsset, new BlueprintCompilationSession());
        }

        public static bool CompileBlueprintAtPath(
            string sourcePath,
            bool log,
            out BlueprintCompiledAsset compiledAsset,
            BlueprintCompilationSession session)
        {
            compiledAsset = null;
            if (string.IsNullOrEmpty(sourcePath))
            {
                return false;
            }

            sourcePath = NormalizeAssetPath(sourcePath);
            session = session ?? new BlueprintCompilationSession();
            if (session.TryGetCompleted(sourcePath, out compiledAsset))
            {
                return compiledAsset != null;
            }

            TextAsset blueprintJson = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
            if (blueprintJson == null)
            {
                if (log)
                {
                    BlueprintLog.Error("[Blueprint] Select a .blueprint.json TextAsset before compiling.");
                }

                return false;
            }

            return CompileBlueprint(blueprintJson, log, out compiledAsset, session);
        }

        public static bool CompileBlueprint(TextAsset blueprintJson, bool log, out BlueprintCompiledAsset compiledAsset)
        {
            return CompileBlueprint(blueprintJson, log, out compiledAsset, new BlueprintCompilationSession());
        }

        private static bool CompileBlueprint(
            TextAsset blueprintJson,
            bool log,
            out BlueprintCompiledAsset compiledAsset,
            BlueprintCompilationSession session)
        {
            compiledAsset = null;
            if (blueprintJson == null)
            {
                return false;
            }

            string sourcePath = AssetDatabase.GetAssetPath(blueprintJson);
            if (string.IsNullOrEmpty(sourcePath))
            {
                if (log)
                {
                    BlueprintLog.Error("[Blueprint] Blueprint JSON must be an asset before it can be compiled.", blueprintJson);
                }

                return false;
            }

            sourcePath = NormalizeAssetPath(sourcePath);
            session = session ?? new BlueprintCompilationSession();
            if (session.TryGetCompleted(sourcePath, out compiledAsset))
            {
                return compiledAsset != null;
            }

            CompilationData data;
            if (!TryBuildCompilationData(blueprintJson, sourcePath, log, session, out data))
            {
                return false;
            }

            string assetPath = GetCompiledAssetPath(sourcePath);
            if (!session.ForceRecompile)
            {
                BlueprintCompiledAsset currentAsset = AssetDatabase.LoadAssetAtPath<BlueprintCompiledAsset>(assetPath);
                if (currentAsset != null && currentAsset.IsCurrent(data.SourceHash, data.ManifestHash))
                {
                    compiledAsset = currentAsset;
                    session.RecordCompleted(sourcePath, compiledAsset);
                    return true;
                }
            }

            bool created = false;
            compiledAsset = LoadCompiledAssetForWrite(assetPath, log, blueprintJson);

            if (compiledAsset == null)
            {
                compiledAsset = ScriptableObject.CreateInstance<BlueprintCompiledAsset>();
                created = true;
            }

            ApplyCompiledData(compiledAsset, data);
            if (created)
            {
                AssetDatabase.CreateAsset(compiledAsset, assetPath);
            }

            EditorUtility.SetDirty(compiledAsset);
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.SaveAssets();

            if (log)
            {
                BlueprintLog.Log("[Blueprint] Compiled '" + data.Source.Name + "' to " + assetPath + ".", compiledAsset);
            }

            session.RecordCompleted(sourcePath, compiledAsset);
            return true;
        }

        private static BlueprintCompiledAsset LoadCompiledAssetForWrite(string assetPath, bool log, UnityEngine.Object context)
        {
            BlueprintCompiledAsset compiledAsset = AssetDatabase.LoadAssetAtPath<BlueprintCompiledAsset>(assetPath);
            if (compiledAsset != null)
            {
                return compiledAsset;
            }

            string existingGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(existingGuid))
            {
                return null;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            compiledAsset = AssetDatabase.LoadAssetAtPath<BlueprintCompiledAsset>(assetPath);
            if (compiledAsset != null)
            {
                return compiledAsset;
            }

            Type mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (log)
            {
                BlueprintLog.Warning(
                    "[Blueprint] Rebuilding stale compiled blueprint asset record at '" + assetPath +
                    "' (guid " + existingGuid + ", main type " + (mainAssetType == null ? "null" : mainAssetType.FullName) + ").",
                    context);
            }

            DeleteGeneratedCompiledAsset(assetPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return null;
        }

        private static void DeleteGeneratedCompiledAsset(string assetPath)
        {
            if (File.Exists(assetPath))
            {
                File.Delete(assetPath);
            }
            else if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        public static bool IsCompiledAssetCurrent(BlueprintCompiledAsset compiledAsset, TextAsset blueprintJson, out string reason)
        {
            reason = null;
            if (compiledAsset == null)
            {
                reason = "Missing compiled asset.";
                return false;
            }

            if (blueprintJson == null)
            {
                reason = "Missing source blueprint JSON.";
                return false;
            }

            string sourcePath = AssetDatabase.GetAssetPath(blueprintJson);
            if (string.IsNullOrEmpty(sourcePath))
            {
                reason = "Source blueprint JSON is not an asset.";
                return false;
            }

            CompilationData data;
            if (!TryBuildCompilationData(blueprintJson, sourcePath, false, new BlueprintCompilationSession(), out data))
            {
                reason = "Source blueprint cannot be compiled.";
                return false;
            }

            if (!compiledAsset.IsCurrent(data.SourceHash, data.ManifestHash))
            {
                reason = "Compiled asset hash is stale.";
                return false;
            }

            return true;
        }

        public static bool IsCompiledAssetCurrent(BlueprintCompiledAsset compiledAsset, out string reason)
        {
            reason = null;
            if (compiledAsset == null)
            {
                reason = "Missing compiled asset.";
                return false;
            }

            string sourcePath = GetCompiledAssetSourcePath(compiledAsset);
            if (string.IsNullOrEmpty(sourcePath))
            {
                return true;
            }

            TextAsset sourceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
            if (sourceAsset == null)
            {
                return true;
            }

            return IsCompiledAssetCurrent(compiledAsset, sourceAsset, out reason);
        }

        public static string GetCompiledAssetSourcePath(BlueprintCompiledAsset compiledAsset)
        {
            if (compiledAsset == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(compiledAsset.SourcePath))
            {
                return compiledAsset.SourcePath.Replace('\\', '/');
            }

            return string.IsNullOrEmpty(compiledAsset.SourceGuid)
                ? null
                : AssetDatabase.GUIDToAssetPath(compiledAsset.SourceGuid);
        }

        public static string GetCompiledAssetPath(string blueprintPath)
        {
            string directory = Path.GetDirectoryName(blueprintPath);
            string fileName = Path.GetFileName(blueprintPath);
            if (fileName.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName.Substring(0, fileName.Length - ".blueprint.json".Length);
            }
            else
            {
                fileName = Path.GetFileNameWithoutExtension(blueprintPath);
            }

            return string.IsNullOrEmpty(directory)
                ? fileName + CompiledAssetSuffix
                : directory.Replace('\\', '/') + "/" + fileName + CompiledAssetSuffix;
        }

        internal static string ResolveComponentAssetPath(string ownerSourcePath, string componentPath)
        {
            componentPath = NormalizeAssetPath(componentPath);
            if (string.IsNullOrEmpty(componentPath))
            {
                return null;
            }

            if (componentPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                componentPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return componentPath;
            }

            string directory = Path.GetDirectoryName(ownerSourcePath);
            return NormalizeAssetPath(string.IsNullOrEmpty(directory)
                ? componentPath
                : directory + "/" + componentPath);
        }

        internal static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }

        internal static BlueprintNodeManifestCollection LoadProjectManifests(out Dictionary<string, string> manifestTextsByTypeId)
        {
            return BlueprintNodeManifestAssetUtility.LoadManifests(out manifestTextsByTypeId);
        }

        private static bool TryBuildCompilationData(
            TextAsset blueprintJson,
            string sourcePath,
            bool log,
            BlueprintCompilationSession session,
            out CompilationData data)
        {
            data = null;
            sourcePath = NormalizeAssetPath(sourcePath);
            session = session ?? new BlueprintCompilationSession();
            if (session.CompilationStack.Contains(sourcePath))
            {
                if (log)
                {
                    BlueprintLog.Error("[Blueprint] Component blueprint cycle detected at '" + sourcePath + "'.", blueprintJson);
                }

                return false;
            }

            session.CompilationStack.Add(sourcePath);
            string sourceText = blueprintJson.text;
            BlueprintSource source;
            try
            {
                source = BlueprintSource.FromJson(sourceText);
                if (BlueprintVariableIdUtility.EnsureVariableIds(source))
                {
                    sourceText = source.ToJson();
                    File.WriteAllText(sourcePath, sourceText, new UTF8Encoding(false));
                    AssetDatabase.ImportAsset(sourcePath);
                }
            }
            catch (Exception exception)
            {
                if (log)
                {
                    BlueprintLog.Error("[Blueprint] Could not parse blueprint JSON at '" + sourcePath + "': " + exception.Message, blueprintJson);
                }

                session.CompilationStack.Remove(sourcePath);
                return false;
            }

            Dictionary<string, string> manifestTextsByTypeId;
            BlueprintNodeManifestCollection manifests = LoadProjectManifests(out manifestTextsByTypeId);
            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, manifests, BlueprintExecutorRegistry.CreateDefault());
            if (!compileResult.Success)
            {
                if (log)
                {
                    BlueprintLog.Error("[Blueprint] Compile failed for " + blueprintJson.name + "\n" + compileResult.Diagnostics.ToDisplayString(), blueprintJson);
                }

                session.CompilationStack.Remove(sourcePath);
                return false;
            }

            List<BlueprintCompiledComponent> compiledComponents;
            string componentHash;
            if (!BuildComponents(source, sourcePath, log, session, out compiledComponents, out componentHash))
            {
                session.CompilationStack.Remove(sourcePath);
                return false;
            }

            data = new CompilationData();
            data.Source = source;
            data.Runtime = compileResult.Blueprint;
            data.Manifests = manifests;
            data.SourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            data.SourcePath = sourcePath;
            data.SourceHash = ComputeHash(sourceText + "\ncomponents:" + componentHash);
            data.ManifestHash = ComputeRequiredManifestHash(source, manifestTextsByTypeId);
            data.Components = compiledComponents;
            session.CompilationStack.Remove(sourcePath);
            return true;
        }

        private static void ApplyCompiledData(BlueprintCompiledAsset compiledAsset, CompilationData data)
        {
            compiledAsset.SetCompiledData(
                data.Source.SchemaVersion,
                data.Source.Name,
                data.SourceGuid,
                data.SourcePath,
                data.SourceHash,
                data.ManifestHash,
                BuildVariables(data.Source),
                BuildBindings(data.Source),
                data.Components,
                BuildNodes(data.Source, data.Manifests),
                BuildExecEdges(data.Runtime),
                BuildValueEdges(data.Runtime),
                BuildEventEntries(data.Runtime));
        }

        private static List<BlueprintCompiledVariable> BuildVariables(BlueprintSource source)
        {
            List<BlueprintCompiledVariable> result = new List<BlueprintCompiledVariable>();
            for (int i = 0; i < source.Variables.Count; i++)
            {
                BlueprintVariableDeclaration variable = source.Variables[i];
                if (variable == null)
                {
                    continue;
                }

                result.Add(new BlueprintCompiledVariable
                {
                    Id = variable.Id,
                    Name = variable.Name,
                    Type = variable.Type,
                    DefaultValueJson = SerializeValueForType(variable.DefaultValue, variable.Type),
                    Scope = variable.Scope,
                    Exposed = variable.Exposed,
                    Persistent = variable.Persistent,
                    Description = variable.Description
                });
            }

            return result;
        }

        private static List<BlueprintCompiledBinding> BuildBindings(BlueprintSource source)
        {
            List<BlueprintCompiledBinding> result = new List<BlueprintCompiledBinding>();
            for (int i = 0; i < source.Bindings.Count; i++)
            {
                BlueprintBindingDeclaration binding = source.Bindings[i];
                if (binding == null)
                {
                    continue;
                }

                result.Add(new BlueprintCompiledBinding
                {
                    Name = binding.Name,
                    Type = binding.Type,
                    Required = binding.Required
                });
            }

            return result;
        }

        private static bool BuildComponents(
            BlueprintSource source,
            string sourcePath,
            bool log,
            BlueprintCompilationSession session,
            out List<BlueprintCompiledComponent> result,
            out string componentHash)
        {
            result = new List<BlueprintCompiledComponent>();
            componentHash = string.Empty;
            StringBuilder hashBuilder = new StringBuilder();
            for (int i = 0; i < source.Components.Count; i++)
            {
                BlueprintComponentDeclaration component = source.Components[i];
                if (component == null || string.IsNullOrEmpty(component.Name))
                {
                    continue;
                }

                string componentPath = ResolveComponentAssetPath(sourcePath, component.Blueprint);
                BlueprintCompiledAsset compiledComponent = null;
                string componentSourcePath = null;
                if (!string.IsNullOrEmpty(componentPath) && componentPath.EndsWith(CompiledAssetSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    compiledComponent = AssetDatabase.LoadAssetAtPath<BlueprintCompiledAsset>(componentPath);
                    componentSourcePath = GetCompiledAssetSourcePath(compiledComponent);
                }
                else if (!string.IsNullOrEmpty(componentPath))
                {
                    componentSourcePath = componentPath;
                    if (session != null && !session.CompileDependencies)
                    {
                        compiledComponent = AssetDatabase.LoadAssetAtPath<BlueprintCompiledAsset>(
                            GetCompiledAssetPath(componentSourcePath));
                    }
                    else if (!CompileBlueprintAtPath(componentSourcePath, log, out compiledComponent, session))
                    {
                        compiledComponent = null;
                    }
                }

                if (compiledComponent == null)
                {
                    if (component.Required)
                    {
                        if (log)
                        {
                            BlueprintLog.Error("[Blueprint] Required component '" + component.Name + "' could not compile or load blueprint '" + component.Blueprint + "'.");
                        }

                        return false;
                    }

                    continue;
                }

                componentSourcePath = NormalizeAssetPath(string.IsNullOrEmpty(componentSourcePath)
                    ? GetCompiledAssetSourcePath(compiledComponent)
                    : componentSourcePath);

                result.Add(new BlueprintCompiledComponent
                {
                    Name = component.Name,
                    BlueprintPath = componentSourcePath,
                    BlueprintGuid = string.IsNullOrEmpty(componentSourcePath) ? null : AssetDatabase.AssetPathToGUID(componentSourcePath),
                    Required = component.Required,
                    CompiledBlueprint = compiledComponent
                });

                hashBuilder.Append(component.Name);
                hashBuilder.Append('|');
                hashBuilder.Append(componentSourcePath ?? string.Empty);
                hashBuilder.Append('|');
                hashBuilder.Append(compiledComponent.SourceHash ?? string.Empty);
                hashBuilder.Append('|');
                hashBuilder.Append(compiledComponent.ManifestHash ?? string.Empty);
                hashBuilder.Append('\n');
            }

            componentHash = ComputeHash(hashBuilder.ToString());
            return true;
        }

        private static List<BlueprintCompiledNode> BuildNodes(BlueprintSource source, BlueprintNodeManifestCollection manifests)
        {
            List<BlueprintCompiledNode> result = new List<BlueprintCompiledNode>();
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BlueprintNodeSource sourceNode = source.Nodes[i];
                BlueprintNodeManifest manifest;
                manifests.TryGet(sourceNode.TypeId, out manifest);

                Dictionary<string, object> propertyValues = new Dictionary<string, object>(sourceNode.Properties, StringComparer.Ordinal);
                Dictionary<string, string> propertyTypes = new Dictionary<string, string>(StringComparer.Ordinal);
                if (manifest != null)
                {
                    for (int p = 0; p < manifest.Properties.Count; p++)
                    {
                        BlueprintPropertySpec property = manifest.Properties[p];
                        if (property == null || string.IsNullOrEmpty(property.Id))
                        {
                            continue;
                        }

                        propertyTypes[property.Id] = property.Type;
                        if (!propertyValues.ContainsKey(property.Id) && property.DefaultValue != null)
                        {
                            propertyValues[property.Id] = property.DefaultValue;
                        }
                    }
                }

                BlueprintCompiledNode compiledNode = new BlueprintCompiledNode();
                compiledNode.Id = sourceNode.Id;
                compiledNode.TypeId = sourceNode.TypeId;
                compiledNode.ExecutorId = manifest == null ? null : manifest.Executor;

                List<string> propertyIds = new List<string>(propertyValues.Keys);
                propertyIds.Sort(StringComparer.Ordinal);
                for (int p = 0; p < propertyIds.Count; p++)
                {
                    string propertyId = propertyIds[p];
                    string propertyType;
                    propertyTypes.TryGetValue(propertyId, out propertyType);
                    compiledNode.Properties.Add(new BlueprintCompiledProperty
                    {
                        Id = propertyId,
                        JsonValue = SerializeValueForType(propertyValues[propertyId], propertyType)
                    });
                }

                result.Add(compiledNode);
            }

            return result;
        }

        private static List<BlueprintCompiledEdge> BuildExecEdges(RuntimeBlueprint runtime)
        {
            List<RuntimeEdge> edges = new List<RuntimeEdge>();
            foreach (List<RuntimeEdge> edgeList in runtime.ExecOutputs.Values)
            {
                edges.AddRange(edgeList);
            }

            edges.Sort((left, right) => string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal));
            return BuildEdges(edges);
        }

        private static List<BlueprintCompiledEdge> BuildValueEdges(RuntimeBlueprint runtime)
        {
            List<RuntimeEdge> edges = new List<RuntimeEdge>(runtime.ValueInputs.Values);
            edges.Sort((left, right) => string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal));
            return BuildEdges(edges);
        }

        private static List<BlueprintCompiledEdge> BuildEdges(List<RuntimeEdge> edges)
        {
            List<BlueprintCompiledEdge> result = new List<BlueprintCompiledEdge>();
            for (int i = 0; i < edges.Count; i++)
            {
                RuntimeEdge edge = edges[i];
                result.Add(new BlueprintCompiledEdge
                {
                    FromNodeId = edge.From.NodeId,
                    FromPortId = edge.From.PortId,
                    ToNodeId = edge.To.NodeId,
                    ToPortId = edge.To.PortId
                });
            }

            return result;
        }

        private static List<BlueprintCompiledEventEntry> BuildEventEntries(RuntimeBlueprint runtime)
        {
            List<string> eventNames = new List<string>(runtime.EventEntries.Keys);
            eventNames.Sort(StringComparer.Ordinal);

            List<BlueprintCompiledEventEntry> result = new List<BlueprintCompiledEventEntry>();
            for (int i = 0; i < eventNames.Count; i++)
            {
                string eventName = eventNames[i];
                result.Add(new BlueprintCompiledEventEntry
                {
                    EventName = eventName,
                    NodeId = runtime.EventEntries[eventName]
                });
            }

            return result;
        }

        private static string ComputeRequiredManifestHash(BlueprintSource source, Dictionary<string, string> manifestTextsByTypeId)
        {
            List<string> typeIds = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                string typeId = source.Nodes[i].TypeId;
                if (!string.IsNullOrEmpty(typeId) && seen.Add(typeId))
                {
                    typeIds.Add(typeId);
                }
            }

            typeIds.Sort(StringComparer.Ordinal);
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < typeIds.Count; i++)
            {
                string typeId = typeIds[i];
                string text;
                manifestTextsByTypeId.TryGetValue(typeId, out text);
                builder.Append(typeId);
                builder.Append('\n');
                builder.Append(text ?? string.Empty);
                builder.Append('\n');
            }

            return ComputeHash(builder.ToString());
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

        internal static string SerializeValueForType(object value, string blueprintType)
        {
            return BlueprintJson.Serialize(NormalizeValueForJson(value, blueprintType), false);
        }

        private static object NormalizeValueForJson(object value, string blueprintType)
        {
            if (value == null)
            {
                return null;
            }

            object jsonValue;
            if (!string.IsNullOrEmpty(blueprintType))
            {
                if (BlueprintDataTableVariableTypeUtility.IsDataTableType(blueprintType))
                {
                    string tablePath;
                    BlueprintDataTableDefinition definition;
                    if (BlueprintDataTableVariableTypeUtility.TryResolveValue(value, blueprintType, out tablePath, out definition))
                    {
                        return tablePath;
                    }
                }

                if (BlueprintArrayUtility.TryConvertToJsonArray(value, blueprintType, out jsonValue))
                {
                    return jsonValue;
                }

                if (BlueprintStructuredValueUtility.TryConvertToJsonValue(value, blueprintType, out jsonValue))
                {
                    return jsonValue;
                }
            }

            Type valueType = value.GetType();
            if (valueType.IsEnum)
            {
                return value.ToString();
            }

            if (value is Vector2)
            {
                Vector2 vector = (Vector2)value;
                return new List<object> { vector.x, vector.y };
            }

            if (value is Vector3)
            {
                Vector3 vector = (Vector3)value;
                return new List<object> { vector.x, vector.y, vector.z };
            }

            if (value is Vector4)
            {
                Vector4 vector = (Vector4)value;
                return new List<object> { vector.x, vector.y, vector.z, vector.w };
            }

            if (value is Rect)
            {
                Rect rect = (Rect)value;
                return new List<object> { rect.x, rect.y, rect.width, rect.height };
            }

            if (value is Color)
            {
                Color color = (Color)value;
                return new List<object> { color.r, color.g, color.b, color.a };
            }

            return value;
        }

        private sealed class CompilationData
        {
            public BlueprintSource Source;
            public RuntimeBlueprint Runtime;
            public BlueprintNodeManifestCollection Manifests;
            public string SourceGuid;
            public string SourcePath;
            public string SourceHash;
            public string ManifestHash;
            public List<BlueprintCompiledComponent> Components;
        }
    }

    internal static class BlueprintNodeManifestAssetUtility
    {
        internal static BlueprintNodeManifestCollection LoadManifests()
        {
            Dictionary<string, string> manifestTextsByTypeId;
            return LoadManifests(out manifestTextsByTypeId);
        }

        internal static BlueprintNodeManifestCollection LoadManifests(out Dictionary<string, string> manifestTextsByTypeId)
        {
            manifestTextsByTypeId = new Dictionary<string, string>(StringComparer.Ordinal);
            BlueprintNodeManifestCollection manifests = new BlueprintNodeManifestCollection();
            List<string> manifestPaths = FindManifestAssetPaths();

            for (int i = 0; i < manifestPaths.Count; i++)
            {
                string path = manifestPaths[i];
                TextAsset manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (manifestAsset == null)
                {
                    continue;
                }

                try
                {
                    BlueprintNodeManifest manifest = BlueprintNodeManifest.FromJson(manifestAsset.text);
                    if (manifest != null &&
                        !string.IsNullOrEmpty(manifest.TypeId) &&
                        BlueprintModuleSettings.IsNodeTypeEnabled(manifest.TypeId))
                    {
                        manifests.Add(manifest);
                        manifestTextsByTypeId[manifest.TypeId] = manifestAsset.text;
                    }
                }
                catch (Exception exception)
                {
                    BlueprintLog.Warning("[Blueprint] Could not parse node manifest at '" + path + "': " + exception.Message, manifestAsset);
                }
            }

            return manifests;
        }

        internal static bool IsManifestPath(string path)
        {
            path = BlueprintAssetDiscovery.NormalizeAssetPath(path);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".node.json", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!BlueprintModuleSettings.IsAssetPathEnabled(path))
            {
                return false;
            }

            if (BlueprintAssetDiscovery.IsProjectAssetPath(path))
            {
                return true;
            }

            if (IsPackageManifestPath(path, BlueprintAssetDiscovery.PackageAssetRoot))
            {
                return true;
            }

            string[] packageRoots = BlueprintEditorAssetDiscovery.GetBlueprintPackageRoots();
            for (int i = 0; i < packageRoots.Length; i++)
            {
                if (IsPackageManifestPath(path, packageRoots[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPackageManifestPath(string path, string packageRoot)
        {
            return BlueprintEditorAssetDiscovery.IsPathInRoot(path, packageRoot) &&
                   path.IndexOf("/Specs/Nodes/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<string> FindManifestAssetPaths()
        {
            List<string> manifestPaths = BlueprintEditorAssetDiscovery.FindTextAssetPaths(".node.json");
            manifestPaths.RemoveAll(path => !IsManifestPath(path));
            return manifestPaths;
        }
    }

    public static class BlueprintRunnerCompiledAssetMigration
    {
        [MenuItem("Tools/Blueprint System/Migrate Legacy Runner JSON References")]
        public static void MigrateProjectAssets()
        {
            Dictionary<string, string> compiledGuidBySourceGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int migratedFiles = 0;
            int migratedReferences = 0;

            string[] assetPaths = FindSceneAndPrefabAssetPaths();
            for (int i = 0; i < assetPaths.Length; i++)
            {
                int fileReferences;
                if (MigrateAssetFile(assetPaths[i], compiledGuidBySourceGuid, out fileReferences))
                {
                    migratedFiles++;
                    migratedReferences += fileReferences;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BlueprintLog.Log("[Blueprint] Migrated " + migratedReferences + " legacy blueprint JSON references in " + migratedFiles + " asset files.");
        }

        private static string[] FindSceneAndPrefabAssetPaths()
        {
            List<string> assetPaths = new List<string>();
            string dataPath = Application.dataPath.Replace('\\', '/');
            AddAssetPaths(assetPaths, dataPath, "*.unity");
            AddAssetPaths(assetPaths, dataPath, "*.prefab");
            assetPaths.Sort(StringComparer.OrdinalIgnoreCase);
            return assetPaths.ToArray();
        }

        private static void AddAssetPaths(List<string> assetPaths, string dataPath, string pattern)
        {
            string[] files = Directory.GetFiles(dataPath, pattern, SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string fullPath = files[i].Replace('\\', '/');
                if (fullPath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                {
                    assetPaths.Add("Assets" + fullPath.Substring(dataPath.Length));
                }
            }
        }

        private static bool MigrateAssetFile(string assetPath, Dictionary<string, string> compiledGuidBySourceGuid, out int migratedReferences)
        {
            migratedReferences = 0;
            string text = File.ReadAllText(assetPath);
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            StringBuilder builder = new StringBuilder(text.Length);
            bool changed = false;
            bool skippingNodeManifests = false;
            bool skipExistingCompiledBlueprint = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                string indent = line.Substring(0, line.Length - trimmed.Length);

                if (trimmed.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    skipExistingCompiledBlueprint = false;
                }

                if (skippingNodeManifests)
                {
                    if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    skippingNodeManifests = false;
                }

                if (trimmed.StartsWith("nodeManifests:", StringComparison.Ordinal))
                {
                    skippingNodeManifests = true;
                    changed = true;
                    continue;
                }

                if (trimmed.StartsWith("blueprintJson:", StringComparison.Ordinal))
                {
                    string sourceGuid = ExtractGuid(trimmed);
                    string compiledGuid;
                    if (!string.IsNullOrEmpty(sourceGuid) && TryGetCompiledGuid(sourceGuid, compiledGuidBySourceGuid, out compiledGuid))
                    {
                        AppendLine(builder, indent + "compiledBlueprint: {fileID: 11400000, guid: " + compiledGuid + ", type: 2}", i < lines.Length - 1);
                        changed = true;
                        skipExistingCompiledBlueprint = true;
                        migratedReferences++;
                        continue;
                    }
                }

                if (skipExistingCompiledBlueprint && trimmed.StartsWith("compiledBlueprint:", StringComparison.Ordinal))
                {
                    changed = true;
                    continue;
                }

                AppendLine(builder, line, i < lines.Length - 1);
            }

            if (!changed)
            {
                return false;
            }

            File.WriteAllText(assetPath, builder.ToString());
            AssetDatabase.ImportAsset(assetPath);
            return true;
        }

        private static void AppendLine(StringBuilder builder, string line, bool appendNewLine)
        {
            builder.Append(line);
            if (appendNewLine)
            {
                builder.Append('\n');
            }
        }

        private static string ExtractGuid(string text)
        {
            const string marker = "guid: ";
            int start = text.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            int end = text.IndexOfAny(new[] { ',', '}' }, start);
            return end < 0 ? text.Substring(start).Trim() : text.Substring(start, end - start).Trim();
        }

        private static bool TryGetCompiledGuid(string sourceGuid, Dictionary<string, string> compiledGuidBySourceGuid, out string compiledGuid)
        {
            if (compiledGuidBySourceGuid.TryGetValue(sourceGuid, out compiledGuid))
            {
                return !string.IsNullOrEmpty(compiledGuid);
            }

            compiledGuid = null;
            string sourcePath = AssetDatabase.GUIDToAssetPath(sourceGuid);
            if (string.IsNullOrEmpty(sourcePath) || !sourcePath.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase))
            {
                compiledGuidBySourceGuid[sourceGuid] = null;
                return false;
            }

            BlueprintCompiledAsset compiledAsset;
            if (!BlueprintCompiledAssetCompiler.CompileBlueprintAtPath(sourcePath, true, out compiledAsset) || compiledAsset == null)
            {
                compiledGuidBySourceGuid[sourceGuid] = null;
                return false;
            }

            string compiledPath = AssetDatabase.GetAssetPath(compiledAsset);
            compiledGuid = AssetDatabase.AssetPathToGUID(compiledPath);
            compiledGuidBySourceGuid[sourceGuid] = compiledGuid;
            return !string.IsNullOrEmpty(compiledGuid);
        }
    }

    internal sealed class BlueprintHotReloadAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            BlueprintHotReloadService.OnAssetsChanged(importedAssets, deletedAssets, movedAssets);
        }
    }

    internal sealed class BlueprintRegistryRefreshAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            BlueprintRegistryRefreshService.OnAssetsChanged(importedAssets, false);
            BlueprintRegistryRefreshService.OnAssetsChanged(movedAssets, false);
            BlueprintRegistryRefreshService.OnAssetsChanged(deletedAssets, true);
            BlueprintRegistryRefreshService.OnAssetsChanged(movedFromAssetPaths, true);
        }
    }

    internal static class BlueprintRegistryRefreshService
    {
        internal static void OnAssetsChanged(string[] paths, bool deleted)
        {
            if (paths == null || paths.Length == 0)
            {
                return;
            }

            bool userStructChanged = false;
            bool dataTableChanged = false;
            for (int i = 0; i < paths.Length; i++)
            {
                string path = BlueprintAssetDiscovery.NormalizeAssetPath(paths[i]);
                if (IsUserStructDefinitionPath(path, deleted))
                {
                    userStructChanged = true;
                }

                if (IsDataTableDefinitionPath(path, deleted))
                {
                    dataTableChanged = true;
                }
            }

            if (userStructChanged)
            {
                BlueprintUserStructRegistry.Refresh();
            }

            if (dataTableChanged)
            {
                BlueprintDataTableRegistry.Refresh();
            }
        }

        private static bool IsUserStructDefinitionPath(string path, bool deleted)
        {
            if (!BlueprintEditorAssetDiscovery.IsDiscoverableAssetPath(path))
            {
                return false;
            }

            if (path.EndsWith(BlueprintUserStructRegistry.StructAssetExtension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsScriptableRegistryAssetPath(path, deleted, typeof(BlueprintUserStructAsset));
        }

        private static bool IsDataTableDefinitionPath(string path, bool deleted)
        {
            if (!BlueprintEditorAssetDiscovery.IsDiscoverableAssetPath(path))
            {
                return false;
            }

            if (path.EndsWith(BlueprintDataTableRegistry.DataTableAssetExtension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsScriptableRegistryAssetPath(path, deleted, typeof(BlueprintDataTableAsset));
        }

        private static bool IsScriptableRegistryAssetPath(string path, bool deleted, Type expectedType)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (deleted)
            {
                return true;
            }

            Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
            return assetType != null && expectedType.IsAssignableFrom(assetType);
        }
    }

    [InitializeOnLoad]
    internal static class BlueprintHotReloadService
    {
        private const string EditorPrefsKey = "BlueprintSystem.HotReloadInPlayMode";
        private const string MenuPath = "Tools/Blueprint System/Hot Reload In Play Mode";
        private static readonly HashSet<string> PendingBlueprintPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _pendingManifestRecompile;
        private static bool _flushScheduled;
        private static int _suppressDepth;

        static BlueprintHotReloadService()
        {
        }

        public static bool IsEnabled
        {
            get { return !Application.isBatchMode && (!EditorPrefs.HasKey(EditorPrefsKey) || EditorPrefs.GetBool(EditorPrefsKey, true)); }
        }

        [MenuItem(MenuPath)]
        private static void ToggleHotReload()
        {
            EditorPrefs.SetBool(EditorPrefsKey, !IsEnabled);
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleHotReloadValidate()
        {
            Menu.SetChecked(MenuPath, IsEnabled);
            return true;
        }

        internal static void OnAssetsChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (_suppressDepth > 0)
            {
                return;
            }

            bool changed = false;
            changed |= AddChangedPaths(importedAssets, false);
            changed |= AddChangedPaths(movedAssets, false);
            changed |= AddChangedPaths(deletedAssets, true);
            if (changed)
            {
                ScheduleFlush();
            }
        }

        internal static IDisposable SuppressAutoCompile(IEnumerable<string> sourcePaths)
        {
            _suppressDepth++;
            ForgetPendingBlueprintPaths(sourcePaths);
            return new SuppressScope(sourcePaths);
        }

        internal static void ForgetPendingBlueprintPaths(IEnumerable<string> sourcePaths)
        {
            if (sourcePaths == null)
            {
                return;
            }

            foreach (string sourcePath in sourcePaths)
            {
                string path = NormalizeAssetPath(sourcePath);
                if (IsBlueprintJsonPath(path))
                {
                    PendingBlueprintPaths.Remove(path);
                }
            }
        }

        internal static bool CompileAndReload(BlueprintRunner runner, bool triggerReloadEvent)
        {
            if (runner == null)
            {
                return false;
            }

            BlueprintCompiledAsset compiledAsset = runner.CompiledBlueprint;
            if (compiledAsset == null)
            {
                BlueprintLog.Warning("[Blueprint] Cannot compile and reload '" + runner.name + "' because it has no compiled blueprint asset.", runner);
                return false;
            }

            string sourcePath = BlueprintCompiledAssetCompiler.GetCompiledAssetSourcePath(compiledAsset);
            if (string.IsNullOrEmpty(sourcePath))
            {
                BlueprintLog.Warning("[Blueprint] Cannot compile and reload '" + runner.name + "' because the compiled asset has no source blueprint path.", runner);
                return false;
            }

            BlueprintCompiledAsset recompiledAsset;
            if (!BlueprintCompiledAssetCompiler.CompileBlueprintAtPath(sourcePath, true, out recompiledAsset))
            {
                return false;
            }

            if (Application.isPlaying)
            {
                return runner.ReloadBlueprint(new BlueprintReloadOptions
                {
                    PreserveVariables = true,
                    TriggerReloadEvent = triggerReloadEvent,
                    RefreshReactiveBindings = true,
                    Log = true
                });
            }

            BlueprintLog.Log("[Blueprint] Compiled '" + sourcePath + "' for '" + runner.name + "'.", runner);
            return true;
        }

        internal static bool RunnerReferencesAnySourcePath(BlueprintRunner runner, HashSet<string> sourcePaths)
        {
            if (runner == null || sourcePaths == null || sourcePaths.Count == 0)
            {
                return false;
            }

            HashSet<string> normalizedSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string sourcePath in sourcePaths)
            {
                normalizedSourcePaths.Add(NormalizeAssetPath(sourcePath));
            }

            return CompiledAssetReferencesAnySourcePath(
                runner.CompiledBlueprint,
                normalizedSourcePaths,
                new HashSet<int>());
        }

        private static bool AddChangedPaths(string[] paths, bool deleted)
        {
            bool changed = false;
            if (paths == null)
            {
                return false;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                string path = NormalizeAssetPath(paths[i]);
                if (IsBlueprintJsonPath(path) && !deleted)
                {
                    PendingBlueprintPaths.Add(path);
                    changed = true;
                }
                else if (IsNodeManifestPath(path))
                {
                    _pendingManifestRecompile = true;
                    changed = true;
                }
            }

            return changed;
        }

        private static void ScheduleFlush()
        {
            if (_flushScheduled)
            {
                return;
            }

            _flushScheduled = true;
            EditorApplication.delayCall += FlushPendingChanges;
        }

        private static void FlushPendingChanges()
        {
            _flushScheduled = false;
            if (_suppressDepth > 0)
            {
                ScheduleFlush();
                return;
            }

            HashSet<string> blueprintPaths = new HashSet<string>(PendingBlueprintPaths, StringComparer.OrdinalIgnoreCase);
            bool recompileAll = _pendingManifestRecompile;
            PendingBlueprintPaths.Clear();
            _pendingManifestRecompile = false;

            if (recompileAll)
            {
                AddAllBlueprintPaths(blueprintPaths);
            }

            if (blueprintPaths.Count == 0)
            {
                return;
            }

            HashSet<string> recompiledSourcePaths = CompileBlueprints(blueprintPaths);
            if (Application.isPlaying && recompiledSourcePaths.Count > 0)
            {
                ReloadAffectedRunners(recompiledSourcePaths);
            }
        }

        private static HashSet<string> CompileBlueprints(HashSet<string> blueprintPaths)
        {
            HashSet<string> recompiledSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> sortedPaths = new List<string>(blueprintPaths);
            sortedPaths.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sortedPaths.Count; i++)
            {
                string path = sortedPaths[i];
                if (!IsBlueprintJsonPath(path) || AssetDatabase.LoadAssetAtPath<TextAsset>(path) == null)
                {
                    continue;
                }

                BlueprintCompiledAsset compiledAsset;
                if (!BlueprintCompiledAssetCompiler.CompileBlueprintAtPath(path, true, out compiledAsset) || compiledAsset == null)
                {
                    continue;
                }

                string sourcePath = BlueprintCompiledAssetCompiler.GetCompiledAssetSourcePath(compiledAsset);
                recompiledSourcePaths.Add(NormalizeAssetPath(string.IsNullOrEmpty(sourcePath) ? path : sourcePath));
            }

            return recompiledSourcePaths;
        }

        private sealed class SuppressScope : IDisposable
        {
            private readonly IEnumerable<string> _sourcePaths;
            private bool _disposed;

            public SuppressScope(IEnumerable<string> sourcePaths)
            {
                _sourcePaths = sourcePaths;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                ForgetPendingBlueprintPaths(_sourcePaths);
                _suppressDepth = Math.Max(0, _suppressDepth - 1);
            }
        }

        private static void ReloadAffectedRunners(HashSet<string> recompiledSourcePaths)
        {
            BlueprintRunner[] runners = Resources.FindObjectsOfTypeAll<BlueprintRunner>();
            int reloaded = 0;
            for (int i = 0; i < runners.Length; i++)
            {
                BlueprintRunner runner = runners[i];
                if (runner == null || EditorUtility.IsPersistent(runner) || !runner.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (!RunnerReferencesAnySourcePath(runner, recompiledSourcePaths))
                {
                    continue;
                }

                if (runner.ReloadBlueprint(new BlueprintReloadOptions
                    {
                        PreserveVariables = true,
                        TriggerReloadEvent = true,
                        RefreshReactiveBindings = true,
                        Log = true
                    }))
                {
                    reloaded++;
                }
            }

            if (reloaded > 0)
            {
                BlueprintLog.Log("[Blueprint] Hot reloaded " + reloaded + " runner(s).");
            }
        }

        private static void AddAllBlueprintPaths(HashSet<string> paths)
        {
            string[] guids = AssetDatabase.FindAssets("t:TextAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (IsBlueprintJsonPath(path))
                {
                    paths.Add(path);
                }
            }
        }

        private static bool CompiledAssetReferencesAnySourcePath(
            BlueprintCompiledAsset compiledAsset,
            HashSet<string> sourcePaths,
            HashSet<int> visited)
        {
            if (compiledAsset == null)
            {
                return false;
            }

            int instanceId = compiledAsset.GetInstanceID();
            if (visited.Contains(instanceId))
            {
                return false;
            }

            visited.Add(instanceId);

            string sourcePath = NormalizeAssetPath(BlueprintCompiledAssetCompiler.GetCompiledAssetSourcePath(compiledAsset));
            if (!string.IsNullOrEmpty(sourcePath) && sourcePaths.Contains(sourcePath))
            {
                return true;
            }

            IReadOnlyList<BlueprintCompiledComponent> components = compiledAsset.Components;
            for (int i = 0; i < components.Count; i++)
            {
                BlueprintCompiledComponent component = components[i];
                if (component == null)
                {
                    continue;
                }

                string componentPath = NormalizeAssetPath(component.BlueprintPath);
                if (!string.IsNullOrEmpty(componentPath) && sourcePaths.Contains(componentPath))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(component.BlueprintGuid))
                {
                    string guidPath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(component.BlueprintGuid));
                    if (!string.IsNullOrEmpty(guidPath) && sourcePaths.Contains(guidPath))
                    {
                        return true;
                    }
                }

                if (CompiledAssetReferencesAnySourcePath(component.CompiledBlueprint, sourcePaths, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBlueprintJsonPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.EndsWith(".blueprint.json", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNodeManifestPath(string path)
        {
            return BlueprintNodeManifestAssetUtility.IsManifestPath(path);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }
    }

    internal sealed class BlueprintCompiledAssetBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            BlueprintRunner[] runners = Resources.FindObjectsOfTypeAll<BlueprintRunner>();
            List<string> failures = new List<string>();
            for (int i = 0; i < runners.Length; i++)
            {
                BlueprintRunner runner = runners[i];
                if (runner == null || EditorUtility.IsPersistent(runner) || !runner.gameObject.scene.IsValid())
                {
                    continue;
                }

                BlueprintCompiledAsset compiledAsset = runner.CompiledBlueprint;
                if (compiledAsset == null)
                {
                    failures.Add(runner.name + " (missing compiled blueprint)");
                    continue;
                }

                string reason;
                if (!BlueprintCompiledAssetCompiler.IsCompiledAssetCurrent(compiledAsset, out reason))
                {
                    failures.Add(runner.name + " (" + reason + ")");
                }
            }

            if (failures.Count > 0)
            {
                throw new BuildFailedException("[Blueprint] BlueprintRunner components must reference current compiled assets. Recompile in the Blueprint editor: " + string.Join(", ", failures));
            }
        }
    }

    [CustomEditor(typeof(BlueprintRunner), true)]
    [CanEditMultipleObjects]
    internal sealed class BlueprintRunnerInspector : UnityEditor.Editor
    {
        private const float NameWidth = 150f;
        private const float TypeWidth = 95f;
        private const float ModeWidth = 80f;
        private const float ResetWidth = 52f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty compiledProperty = serializedObject.FindProperty("compiledBlueprint");
            EditorGUILayout.PropertyField(compiledProperty);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("triggerOnStart"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("triggerOnTick"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("triggerOnFixedTick"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("triggerOnLateTick"));

            DrawBindingFields();

            EditorGUILayout.Space();
            DrawExposedVariableOverrides(compiledProperty == null ? null : compiledProperty.objectReferenceValue as BlueprintCompiledAsset);

            EditorGUILayout.Space();
            bool syncClicked = GUILayout.Button("Sync Exposed Variable Overrides");
            bool compileReloadClicked = GUILayout.Button(Application.isPlaying ? "Compile & Reload" : "Compile Blueprint");

            serializedObject.ApplyModifiedProperties();
            if (syncClicked)
            {
                SyncVariableOverrides();
            }

            if (compileReloadClicked)
            {
                CompileAndReloadTargets();
            }
        }

        private void DrawBindingFields()
        {
            SerializedProperty bindingsProperty = serializedObject.FindProperty("bindings");
            if (bindingsProperty == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bindings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(bindingsProperty, true);

            SerializedProperty triggerOnEnableProperty = serializedObject.FindProperty("triggerOnEnable");
            SerializedProperty enableEventNameProperty = serializedObject.FindProperty("enableEventName");
            SerializedProperty triggerOnDisableProperty = serializedObject.FindProperty("triggerOnDisable");
            SerializedProperty disableEventNameProperty = serializedObject.FindProperty("disableEventName");

            if (triggerOnEnableProperty == null &&
                enableEventNameProperty == null &&
                triggerOnDisableProperty == null &&
                disableEventNameProperty == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UI Lifecycle", EditorStyles.boldLabel);

            if (triggerOnEnableProperty != null)
            {
                EditorGUILayout.PropertyField(triggerOnEnableProperty);
            }

            if (enableEventNameProperty != null)
            {
                EditorGUILayout.PropertyField(enableEventNameProperty);
            }

            if (triggerOnDisableProperty != null)
            {
                EditorGUILayout.PropertyField(triggerOnDisableProperty);
            }

            if (disableEventNameProperty != null)
            {
                EditorGUILayout.PropertyField(disableEventNameProperty);
            }
        }

        private void DrawExposedVariableOverrides(BlueprintCompiledAsset compiledAsset)
        {
            EditorGUILayout.LabelField("Exposed Variables", EditorStyles.boldLabel);

            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox("Select one BlueprintRunner to edit exposed variable overrides.", MessageType.Info);
                return;
            }

            if (compiledAsset == null)
            {
                EditorGUILayout.HelpBox("Assign a compiled blueprint asset to edit exposed variables.", MessageType.Info);
                return;
            }

            SerializedProperty overridesProperty = serializedObject.FindProperty("variableOverrides");
            if (overridesProperty == null || !overridesProperty.isArray)
            {
                EditorGUILayout.HelpBox("Variable override storage is unavailable.", MessageType.Warning);
                return;
            }

            bool drewAny = false;
            IReadOnlyList<BlueprintCompiledVariable> variables = compiledAsset.Variables;
            for (int i = 0; i < variables.Count; i++)
            {
                BlueprintCompiledVariable variable = variables[i];
                if (variable == null || string.IsNullOrEmpty(variable.Name) || !variable.Exposed)
                {
                    continue;
                }

                drewAny = true;
                DrawVariableOverrideRow(overridesProperty, variable);
            }

            if (!drewAny)
            {
                EditorGUILayout.HelpBox("This blueprint has no exposed variables.", MessageType.None);
            }
        }

        private static void DrawVariableOverrideRow(SerializedProperty overridesProperty, BlueprintCompiledVariable variable)
        {
            SerializedProperty entry = FindOverrideEntry(overridesProperty, variable.Id, variable.Name);
            bool enabled = IsOverrideEnabled(entry);
            string currentJson = enabled ? GetString(entry, "JsonValue") : variable.DefaultValueJson;
            string error;
            string editedJson;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(variable.Name, GUILayout.Width(NameWidth));
                EditorGUILayout.LabelField(variable.Type, GUILayout.Width(TypeWidth));

                if (DrawValueField(variable.Type, currentJson, out editedJson, out error))
                {
                    entry = EnsureOverrideEntry(overridesProperty, variable);
                    SetBool(entry, "Enabled", true);
                    SetString(entry, "JsonValue", editedJson);
                    enabled = true;
                }

                bool newEnabled = GUILayout.Toggle(enabled, enabled ? "Override" : "Inherited", EditorStyles.miniButton, GUILayout.Width(ModeWidth));
                if (newEnabled != enabled)
                {
                    entry = EnsureOverrideEntry(overridesProperty, variable);
                    SetBool(entry, "Enabled", newEnabled);
                    if (newEnabled && string.IsNullOrEmpty(GetString(entry, "JsonValue")))
                    {
                        SetString(entry, "JsonValue", variable.DefaultValueJson ?? string.Empty);
                    }
                    else if (!newEnabled)
                    {
                        SetString(entry, "JsonValue", string.Empty);
                    }
                }

                using (new EditorGUI.DisabledScope(!enabled && entry == null))
                {
                    if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(ResetWidth)))
                    {
                        entry = EnsureOverrideEntry(overridesProperty, variable);
                        SetBool(entry, "Enabled", false);
                        SetString(entry, "JsonValue", string.Empty);
                    }
                }
            }

            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(variable.Name + ": " + error, MessageType.Warning);
            }
        }

        private void SyncVariableOverrides()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                BlueprintRunner runner = targets[i] as BlueprintRunner;
                if (runner == null)
                {
                    continue;
                }

                SerializedObject runnerObject = new SerializedObject(runner);
                SerializedProperty overridesProperty = runnerObject.FindProperty("variableOverrides");
                if (overridesProperty == null || !overridesProperty.isArray)
                {
                    continue;
                }

                BlueprintCompiledAsset compiledAsset = runner.CompiledBlueprint;
                if (compiledAsset == null)
                {
                    BlueprintLog.Warning("[Blueprint] Cannot sync variable overrides for '" + runner.name + "' because it has no compiled blueprint asset.", runner);
                    continue;
                }

                SyncOverrideArray(runnerObject, overridesProperty, compiledAsset.Variables);
                EditorUtility.SetDirty(runner);
                if (!EditorUtility.IsPersistent(runner) && runner.gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
                }
            }
        }

        private void CompileAndReloadTargets()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                BlueprintRunner runner = targets[i] as BlueprintRunner;
                if (runner != null)
                {
                    BlueprintHotReloadService.CompileAndReload(runner, false);
                }
            }
        }

        private static void SyncOverrideArray(SerializedObject runnerObject, SerializedProperty overridesProperty, IReadOnlyList<BlueprintCompiledVariable> variables)
        {
            runnerObject.Update();
            for (int i = 0; i < variables.Count; i++)
            {
                BlueprintCompiledVariable variable = variables[i];
                if (variable == null || string.IsNullOrEmpty(variable.Name) || !variable.Exposed)
                {
                    continue;
                }

                SerializedProperty entry = FindOverrideEntry(overridesProperty, variable.Id, variable.Name);
                if (entry == null)
                {
                    int newIndex = overridesProperty.arraySize;
                    overridesProperty.InsertArrayElementAtIndex(newIndex);
                    entry = overridesProperty.GetArrayElementAtIndex(newIndex);
                    SetBool(entry, "Enabled", false);
                    SetString(entry, "JsonValue", string.Empty);
                }

                SetString(entry, "VariableId", variable.Id);
                SetString(entry, "Name", variable.Name);
                SetString(entry, "Type", variable.Type);
            }

            runnerObject.ApplyModifiedProperties();
        }

        private static SerializedProperty FindOverrideEntry(SerializedProperty overridesProperty, string variableId, string variableName)
        {
            if (!string.IsNullOrEmpty(variableId))
            {
                for (int i = 0; i < overridesProperty.arraySize; i++)
                {
                    SerializedProperty entry = overridesProperty.GetArrayElementAtIndex(i);
                    SerializedProperty idProperty = entry.FindPropertyRelative("VariableId");
                    if (idProperty != null && idProperty.stringValue == variableId)
                    {
                        return entry;
                    }
                }
            }

            for (int i = 0; i < overridesProperty.arraySize; i++)
            {
                SerializedProperty entry = overridesProperty.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = entry.FindPropertyRelative("Name");
                if (nameProperty != null && nameProperty.stringValue == variableName)
                {
                    return entry;
                }
            }

            return null;
        }

        private static SerializedProperty EnsureOverrideEntry(SerializedProperty overridesProperty, BlueprintCompiledVariable variable)
        {
            SerializedProperty entry = FindOverrideEntry(overridesProperty, variable.Id, variable.Name);
            if (entry == null)
            {
                int newIndex = overridesProperty.arraySize;
                overridesProperty.InsertArrayElementAtIndex(newIndex);
                entry = overridesProperty.GetArrayElementAtIndex(newIndex);
            }

            SetString(entry, "VariableId", variable.Id);
            SetString(entry, "Name", variable.Name);
            SetString(entry, "Type", variable.Type);
            return entry;
        }

        private static bool DrawValueField(string blueprintType, string jsonValue, out string editedJson, out string error)
        {
            editedJson = jsonValue ?? string.Empty;
            error = null;

            if (ShouldUseJsonField(blueprintType))
            {
                EditorGUI.BeginChangeCheck();
                string newJson = EditorGUILayout.TextField(jsonValue ?? string.Empty);
                bool changed = EditorGUI.EndChangeCheck();
                if (!IsJsonAssignable(newJson, blueprintType, out error))
                {
                    // Keep the invalid JSON editable so the user can fix it in place.
                }

                if (changed)
                {
                    editedJson = newJson;
                }

                return changed;
            }

            object value;
            bool valid = TryReadEditableValue(jsonValue, blueprintType, out value, out error);
            EditorGUI.BeginChangeCheck();
            object editedValue = DrawTypedValueField(blueprintType, value);
            bool typedChanged = EditorGUI.EndChangeCheck();
            if (typedChanged)
            {
                editedJson = BlueprintCompiledAssetCompiler.SerializeValueForType(editedValue, blueprintType);
                error = null;
            }
            else if (!valid)
            {
                // The row remains editable; changing the field will replace the invalid value.
            }

            return typedChanged;
        }

        private static object DrawTypedValueField(string blueprintType, object value)
        {
            switch (blueprintType)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    return EditorGUILayout.TextField(value as string ?? string.Empty);
                case "bool":
                    return EditorGUILayout.Toggle(value is bool && (bool)value);
                case "int":
                    return EditorGUILayout.IntField(Convert.ToInt32(value ?? 0));
                case "float":
                    return EditorGUILayout.FloatField(Convert.ToSingle(value ?? 0f));
                case "Vector2":
                    return EditorGUILayout.Vector2Field(GUIContent.none, value is Vector2 ? (Vector2)value : Vector2.zero);
                case "Vector3":
                    return EditorGUILayout.Vector3Field(GUIContent.none, value is Vector3 ? (Vector3)value : Vector3.zero);
                case "Vector4":
                    return EditorGUILayout.Vector4Field(GUIContent.none, value is Vector4 ? (Vector4)value : Vector4.zero);
                case "Rect":
                    return EditorGUILayout.RectField(value is Rect ? (Rect)value : Rect.zero);
                case "Color":
                    return EditorGUILayout.ColorField(value is Color ? (Color)value : Color.white);
                default:
                    Type enumType;
                    if (BlueprintVariableTypeRegistry.TryGetClrType(blueprintType, out enumType) && enumType.IsEnum)
                    {
                        string[] names = Enum.GetNames(enumType);
                        int index = 0;
                        if (value != null)
                        {
                            string current = value.ToString();
                            for (int i = 0; i < names.Length; i++)
                            {
                                if (names[i] == current)
                                {
                                    index = i;
                                    break;
                                }
                            }
                        }

                        int selected = EditorGUILayout.Popup(index, names);
                        return Enum.Parse(enumType, names[selected], false);
                    }

                    return value;
            }
        }

        private static bool TryReadEditableValue(string jsonValue, string blueprintType, out object value, out string error)
        {
            value = null;
            error = null;
            object rawValue;
            if (!TryDeserializeJson(jsonValue, out rawValue, out error))
            {
                value = GetFallbackValue(blueprintType);
                return false;
            }

            if (!BlueprintTypeUtility.IsValueAssignableToType(rawValue, blueprintType))
            {
                error = "Value is not assignable to " + blueprintType + ".";
                value = GetFallbackValue(blueprintType);
                return false;
            }

            value = CoerceEditorValue(rawValue, blueprintType);
            return true;
        }

        private static object CoerceEditorValue(object value, string blueprintType)
        {
            switch (blueprintType)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    return BlueprintTypeUtility.ConvertValue(value, typeof(string), string.Empty);
                case "bool":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(bool), false);
                case "int":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(int), 0);
                case "float":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(float), 0f);
                case "Vector2":
                    return value is Vector2 ? value : BlueprintTypeUtility.ToVector2(value, Vector2.zero);
                case "Vector3":
                    return value is Vector3 ? value : BlueprintTypeUtility.ToVector3(value, Vector3.zero);
                case "Vector4":
                    return value is Vector4 ? value : BlueprintTypeUtility.ToVector4(value, Vector4.zero);
                case "Rect":
                    return value is Rect ? value : BlueprintTypeUtility.ToRect(value, Rect.zero);
                case "Color":
                    return value is Color ? value : ToColor(value, Color.white);
                default:
                    Type enumType;
                    if (BlueprintVariableTypeRegistry.TryGetClrType(blueprintType, out enumType) && enumType.IsEnum)
                    {
                        return BlueprintTypeUtility.ConvertValue(value, enumType, Activator.CreateInstance(enumType));
                    }

                    return value;
            }
        }

        private static object GetFallbackValue(string blueprintType)
        {
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
                    return Vector2.zero;
                case "Vector3":
                    return Vector3.zero;
                case "Vector4":
                    return Vector4.zero;
                case "Rect":
                    return Rect.zero;
                case "Color":
                    return Color.white;
                default:
                    Type enumType;
                    if (BlueprintVariableTypeRegistry.TryGetClrType(blueprintType, out enumType) && enumType.IsEnum)
                    {
                        return Activator.CreateInstance(enumType);
                    }

                    return null;
            }
        }

        private static bool ShouldUseJsonField(string blueprintType)
        {
            if (BlueprintArrayUtility.IsArrayType(blueprintType) || BlueprintVariableTypeRegistry.IsCustomType(blueprintType))
            {
                return true;
            }

            Type enumType;
            return !IsBuiltinEditableType(blueprintType) &&
                   (!BlueprintVariableTypeRegistry.TryGetClrType(blueprintType, out enumType) || !enumType.IsEnum);
        }

        private static bool IsBuiltinEditableType(string blueprintType)
        {
            return blueprintType == "string" ||
                   blueprintType == "bool" ||
                   blueprintType == "int" ||
                   blueprintType == "float" ||
                   blueprintType == "Vector2" ||
                   blueprintType == "Vector3" ||
                   blueprintType == "Vector4" ||
                   blueprintType == "Rect" ||
                   blueprintType == "Color" ||
                   blueprintType == BlueprintVariableTypeRegistry.BlueprintAssetTypeId;
        }

        private static bool IsJsonAssignable(string jsonValue, string blueprintType, out string error)
        {
            error = null;
            object value;
            if (!TryDeserializeJson(jsonValue, out value, out error))
            {
                return false;
            }

            if (!BlueprintTypeUtility.IsValueAssignableToType(value, blueprintType))
            {
                error = "Value is not assignable to " + blueprintType + ".";
                return false;
            }

            return true;
        }

        private static bool TryDeserializeJson(string jsonValue, out object value, out string error)
        {
            value = null;
            error = null;
            if (string.IsNullOrEmpty(jsonValue))
            {
                return true;
            }

            try
            {
                value = BlueprintJson.Deserialize(jsonValue);
                return true;
            }
            catch (BlueprintJsonException exception)
            {
                error = "Invalid JSON: " + exception.Message;
                return false;
            }
        }

        private static Color ToColor(object value, Color defaultValue)
        {
            if (value is Color)
            {
                return (Color)value;
            }

            System.Collections.IList list = value as System.Collections.IList;
            if (list == null || (list.Count != 3 && list.Count != 4))
            {
                return defaultValue;
            }

            try
            {
                float r = Convert.ToSingle(list[0]);
                float g = Convert.ToSingle(list[1]);
                float b = Convert.ToSingle(list[2]);
                float a = list.Count == 4 ? Convert.ToSingle(list[3]) : 1f;
                return new Color(r, g, b, a);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static bool IsOverrideEnabled(SerializedProperty entry)
        {
            if (entry == null)
            {
                return false;
            }

            SerializedProperty enabledProperty = entry.FindPropertyRelative("Enabled");
            if (enabledProperty != null && enabledProperty.boolValue)
            {
                return true;
            }

            return string.IsNullOrEmpty(GetString(entry, "VariableId")) &&
                   !string.IsNullOrEmpty(GetString(entry, "Name")) &&
                   !string.IsNullOrEmpty(GetString(entry, "JsonValue"));
        }

        private static string GetString(SerializedProperty parent, string propertyName)
        {
            SerializedProperty property = parent == null ? null : parent.FindPropertyRelative(propertyName);
            return property == null ? null : property.stringValue;
        }

        private static void SetString(SerializedProperty parent, string propertyName, string value)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetBool(SerializedProperty parent, string propertyName, bool value)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }
    }
}
