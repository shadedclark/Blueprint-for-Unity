using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    internal static class BlueprintGraphToolkitBlackboardSync
    {
        public static bool SyncVariablesToBlackboard(BlueprintVisualGraph graph)
        {
            if (graph == null)
            {
                return false;
            }

            EnsureSupportedVariableTypes(graph);
            if (graph.Variables == null)
            {
                return false;
            }

            HashSet<string> existingNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (IVariable variable in graph.GetVariables())
            {
                if (variable != null && !string.IsNullOrEmpty(variable.name))
                {
                    existingNames.Add(variable.name);
                }
            }

            bool changed = false;
            for (int i = 0; i < graph.Variables.Count; i++)
            {
                BlueprintVisualVariableData visualVariable = graph.Variables[i];
                if (visualVariable == null || string.IsNullOrEmpty(visualVariable.Name) || existingNames.Contains(visualVariable.Name))
                {
                    continue;
                }

                Type graphType;
                if (!BlueprintGraphToolkitTypeRegistry.TryGetGraphType(visualVariable.Type, out graphType))
                {
                    continue;
                }

                object defaultValue = null;
                if (visualVariable.HasDefaultValue)
                {
                    defaultValue = BlueprintVisualValueUtility.ConvertForGraphField(
                        BlueprintVisualValueUtility.FromJson(visualVariable.JsonDefaultValue),
                        visualVariable.Type);
                }

                BlueprintGraphToolkitReflection.CreateBlackboardVariable(graph, visualVariable.Name, graphType, defaultValue);
                existingNames.Add(visualVariable.Name);
                changed = true;
            }

            return changed;
        }

        public static List<BlueprintVisualVariableData> ExtractVariables(BlueprintVisualGraph graph)
        {
            List<BlueprintVisualVariableData> result = new List<BlueprintVisualVariableData>();
            if (graph == null)
            {
                return result;
            }

            Dictionary<string, BlueprintVisualVariableData> metadataByName = BuildMetadataIndex(graph.Variables);
            List<IVariable> blackboardVariables = new List<IVariable>(graph.GetVariables());
            if (blackboardVariables.Count == 0)
            {
                return CloneVariables(graph.Variables);
            }

            HashSet<string> exportedNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < blackboardVariables.Count; i++)
            {
                IVariable variable = blackboardVariables[i];
                if (variable == null || string.IsNullOrEmpty(variable.name))
                {
                    continue;
                }

                string blueprintType;
                BlueprintVisualVariableData metadata;
                metadataByName.TryGetValue(variable.name, out metadata);
                if (!TryGetBlueprintType(variable, metadata, out blueprintType))
                {
                    continue;
                }

                BlueprintVisualVariableData visualVariable = new BlueprintVisualVariableData
                {
                    Id = metadata == null ? null : metadata.Id,
                    Name = variable.name,
                    Type = blueprintType,
                    Scope = metadata == null ? "runtime" : metadata.Scope,
                    Exposed = metadata != null && metadata.Exposed,
                    Persistent = metadata != null && metadata.Persistent,
                    Description = metadata == null ? ReadTooltip(variable) : metadata.Description
                };

                object defaultValue;
                if (TryReadDefaultValue(variable, blueprintType, out defaultValue))
                {
                    visualVariable.HasDefaultValue = true;
                    visualVariable.JsonDefaultValue = BlueprintVisualValueUtility.ToJson(defaultValue);
                }

                result.Add(visualVariable);
                exportedNames.Add(variable.name);
            }

            if (graph.Variables != null)
            {
                for (int i = 0; i < graph.Variables.Count; i++)
                {
                    BlueprintVisualVariableData metadata = graph.Variables[i];
                    if (metadata == null || string.IsNullOrEmpty(metadata.Name) || exportedNames.Contains(metadata.Name))
                    {
                        continue;
                    }

                    Type ignored;
                    if (!BlueprintGraphToolkitTypeRegistry.TryGetGraphType(metadata.Type, out ignored))
                    {
                        result.Add(CloneVariable(metadata));
                    }
                }
            }

            return result;
        }

        public static bool TryGetBlueprintType(BlueprintVisualGraph graph, IVariable variable, out string blueprintType)
        {
            BlueprintVisualVariableData metadata;
            TryFindVariableMetadata(graph, variable == null ? null : variable.name, out metadata);
            return TryGetBlueprintType(variable, metadata, out blueprintType);
        }

        private static bool TryFindVariableMetadata(BlueprintVisualGraph graph, string variableName, out BlueprintVisualVariableData metadata)
        {
            metadata = null;
            if (graph == null || graph.Variables == null || string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            for (int i = 0; i < graph.Variables.Count; i++)
            {
                BlueprintVisualVariableData candidate = graph.Variables[i];
                if (candidate != null && candidate.Name == variableName)
                {
                    metadata = candidate;
                    return true;
                }
            }

            return false;
        }

        public static List<BlueprintVariableDeclaration> ExtractSourceVariables(BlueprintVisualGraph graph)
        {
            List<BlueprintVariableDeclaration> result = new List<BlueprintVariableDeclaration>();
            List<BlueprintVisualVariableData> variables = ExtractVariables(graph);
            for (int i = 0; i < variables.Count; i++)
            {
                BlueprintVisualVariableData visualVariable = variables[i];
                if (visualVariable == null)
                {
                    continue;
                }

                BlueprintVariableDeclaration variable = new BlueprintVariableDeclaration
                {
                    Id = visualVariable.Id,
                    Name = visualVariable.Name,
                    Type = visualVariable.Type,
                    Scope = visualVariable.Scope,
                    Exposed = visualVariable.Exposed,
                    Persistent = visualVariable.Persistent,
                    Description = visualVariable.Description
                };

                if (visualVariable.HasDefaultValue)
                {
                    variable.DefaultValue = BlueprintVisualValueUtility.FromJson(visualVariable.JsonDefaultValue);
                }

                result.Add(variable);
            }

            return result;
        }

        private static Dictionary<string, BlueprintVisualVariableData> BuildMetadataIndex(List<BlueprintVisualVariableData> variables)
        {
            Dictionary<string, BlueprintVisualVariableData> result = new Dictionary<string, BlueprintVisualVariableData>(StringComparer.Ordinal);
            if (variables == null)
            {
                return result;
            }

            for (int i = 0; i < variables.Count; i++)
            {
                BlueprintVisualVariableData variable = variables[i];
                if (variable != null && !string.IsNullOrEmpty(variable.Name))
                {
                    result[variable.Name] = variable;
                }
            }

            return result;
        }

        private static List<BlueprintVisualVariableData> CloneVariables(List<BlueprintVisualVariableData> variables)
        {
            List<BlueprintVisualVariableData> result = new List<BlueprintVisualVariableData>();
            if (variables == null)
            {
                return result;
            }

            for (int i = 0; i < variables.Count; i++)
            {
                BlueprintVisualVariableData variable = CloneVariable(variables[i]);
                if (variable != null)
                {
                    result.Add(variable);
                }
            }

            return result;
        }

        private static BlueprintVisualVariableData CloneVariable(BlueprintVisualVariableData variable)
        {
            if (variable == null)
            {
                return null;
            }

            return new BlueprintVisualVariableData
            {
                Id = variable.Id,
                Name = variable.Name,
                Type = variable.Type,
                HasDefaultValue = variable.HasDefaultValue,
                JsonDefaultValue = variable.JsonDefaultValue,
                Scope = variable.Scope,
                Exposed = variable.Exposed,
                Persistent = variable.Persistent,
                Description = variable.Description
            };
        }

        private static bool TryGetBlueprintType(IVariable variable, BlueprintVisualVariableData metadata, out string blueprintType)
        {
            blueprintType = null;
            if (variable == null)
            {
                return false;
            }

            if (BlueprintGraphToolkitArrayTypes.IsGraphArrayType(variable.dataType))
            {
                if (TryGetArrayBlueprintType(variable, out blueprintType))
                {
                    return true;
                }

                if (metadata != null && BlueprintArrayUtility.IsArrayType(metadata.Type))
                {
                    blueprintType = metadata.Type;
                    return true;
                }

                blueprintType = BlueprintGraphToolkitArrayTypes.MakeBlueprintType(BlueprintGraphToolkitArrayTypes.DefaultElementType);
                return true;
            }

            if (metadata != null && !string.IsNullOrEmpty(metadata.Type))
            {
                Type graphType;
                if (BlueprintGraphToolkitTypeRegistry.TryGetGraphType(metadata.Type, out graphType) &&
                    (graphType == variable.dataType ||
                     BlueprintArrayUtility.IsArrayType(metadata.Type) && variable.dataType == typeof(string)))
                {
                    blueprintType = metadata.Type;
                    return true;
                }
            }

            return BlueprintGraphToolkitTypeRegistry.TryGetBlueprintType(variable.dataType, out blueprintType);
        }

        private static bool TryGetArrayBlueprintType(IVariable variable, out string blueprintType)
        {
            blueprintType = null;
            if (variable == null || !BlueprintGraphToolkitArrayTypes.IsGraphArrayType(variable.dataType))
            {
                return false;
            }

            object graphValue;
            if (TryReadDefaultValue(variable, variable.dataType, out graphValue) &&
                TryGetExplicitArrayBlueprintType(graphValue, out blueprintType))
            {
                return true;
            }

            return variable.dataType != typeof(Array) &&
                   BlueprintGraphToolkitTypeRegistry.TryGetBlueprintType(variable.dataType, out blueprintType);
        }

        private static bool TryGetExplicitArrayBlueprintType(object graphValue, out string blueprintType)
        {
            blueprintType = null;
            if (graphValue == null)
            {
                return false;
            }

            if (graphValue.GetType() == typeof(Array))
            {
                Array value = (Array)graphValue;
                if (BlueprintArrayUtility.IsSupportedElementType(value.ElementType))
                {
                    blueprintType = BlueprintGraphToolkitArrayTypes.MakeBlueprintType(value.ElementType);
                    return true;
                }

                return false;
            }

            return BlueprintGraphToolkitArrayTypes.TryGetBlueprintType(graphValue, out blueprintType);
        }

        public static bool TryReadDefaultValue(IVariable variable, string blueprintType, out object value)
        {
            value = null;
            switch (blueprintType)
            {
                case "string":
                    string stringValue;
                    if (variable.TryGetDefaultValue(out stringValue))
                    {
                        value = stringValue;
                        return true;
                    }

                    return false;
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    Type blueprintGraphType;
                    object blueprintGraphValue;
                    if (BlueprintGraphToolkitTypeRegistry.TryGetGraphType(blueprintType, out blueprintGraphType) &&
                        TryReadDefaultValue(variable, blueprintGraphType, out blueprintGraphValue))
                    {
                        value = BlueprintVisualValueUtility.ConvertFromGraphField(blueprintGraphValue, blueprintType);
                        return true;
                    }

                    string blueprintPath;
                    if (variable.TryGetDefaultValue(out blueprintPath))
                    {
                        value = blueprintPath;
                        return true;
                    }

                    return false;
                case "bool":
                    bool boolValue;
                    if (variable.TryGetDefaultValue(out boolValue))
                    {
                        value = boolValue;
                        return true;
                    }

                    return false;
                case "int":
                    int intValue;
                    if (variable.TryGetDefaultValue(out intValue))
                    {
                        value = intValue;
                        return true;
                    }

                    return false;
                case "float":
                    float floatValue;
                    if (variable.TryGetDefaultValue(out floatValue))
                    {
                        value = floatValue;
                        return true;
                    }

                    return false;
                case "Vector2":
                    Vector2 vector2Value;
                    if (variable.TryGetDefaultValue(out vector2Value))
                    {
                        value = BlueprintVisualValueUtility.ConvertFromGraphField(vector2Value, blueprintType);
                        return true;
                    }

                    return false;
                case "Vector3":
                    Vector3 vector3Value;
                    if (variable.TryGetDefaultValue(out vector3Value))
                    {
                        value = BlueprintVisualValueUtility.ConvertFromGraphField(vector3Value, blueprintType);
                        return true;
                    }

                    return false;
                case "Vector4":
                    Vector4 vector4Value;
                    if (variable.TryGetDefaultValue(out vector4Value))
                    {
                        value = BlueprintVisualValueUtility.ConvertFromGraphField(vector4Value, blueprintType);
                        return true;
                    }

                    return false;
                case "Color":
                    Color colorValue;
                    if (variable.TryGetDefaultValue(out colorValue))
                    {
                        value = BlueprintVisualValueUtility.ConvertFromGraphField(colorValue, blueprintType);
                        return true;
                    }

                    return false;
                case "Rect":
                    Rect rectValue;
                    if (variable.TryGetDefaultValue(out rectValue))
                    {
                        value = BlueprintVisualValueUtility.ConvertFromGraphField(rectValue, blueprintType);
                        return true;
                    }

                    return false;
                default:
                    if (BlueprintArrayUtility.IsArrayType(blueprintType))
                    {
                        Type arrayGraphType;
                        object graphValue;
                        if (BlueprintGraphToolkitTypeRegistry.TryGetGraphType(blueprintType, out arrayGraphType) &&
                            TryReadDefaultValue(variable, arrayGraphType, out graphValue))
                        {
                            value = BlueprintVisualValueUtility.ConvertFromGraphField(graphValue, blueprintType);
                            return true;
                        }

                        if (BlueprintGraphToolkitArrayTypes.IsGraphArrayType(variable.dataType) &&
                            variable.dataType != arrayGraphType &&
                            TryReadDefaultValue(variable, variable.dataType, out graphValue))
                        {
                            value = BlueprintVisualValueUtility.ConvertFromGraphField(graphValue, blueprintType);
                            return true;
                        }

                        string legacyArrayJson;
                        if (variable.TryGetDefaultValue(out legacyArrayJson))
                        {
                            value = BlueprintVisualValueUtility.ConvertFromGraphField(legacyArrayJson, blueprintType);
                            return true;
                        }
                    }

                    Type graphType;
                    if (BlueprintGraphToolkitTypeRegistry.TryGetGraphType(blueprintType, out graphType) && graphType.IsEnum)
                    {
                        object enumValue;
                        if (TryReadDefaultValue(variable, graphType, out enumValue))
                        {
                            value = BlueprintVisualValueUtility.ConvertFromGraphField(enumValue, blueprintType);
                            return true;
                        }
                    }

                    if (BlueprintVariableTypeRegistry.IsCustomType(blueprintType) &&
                        BlueprintGraphToolkitTypeRegistry.TryGetGraphType(blueprintType, out graphType))
                    {
                        object typedValue;
                        if (TryReadDefaultValue(variable, graphType, out typedValue))
                        {
                            return BlueprintStructuredValueUtility.TryConvertToJsonValue(typedValue, blueprintType, out value);
                        }
                    }

                    return false;
            }
        }

        public static void EnsureSupportedVariableTypes(BlueprintVisualGraph graph)
        {
            BlueprintGraphToolkitReflection.EnsureSupportedVariableTypes(graph, BlueprintGraphToolkitTypeRegistry.SupportedGraphTypes);
        }

        private static bool TryReadDefaultValue(IVariable variable, Type graphType, out object value)
        {
            value = null;
            if (variable == null || graphType == null)
            {
                return false;
            }

            System.Reflection.MethodInfo method = FindGenericTryGetDefaultValueMethod(variable.GetType());
            if (method == null)
            {
                method = FindGenericTryGetDefaultValueMethod(typeof(IVariable));
            }

            if (method == null)
            {
                return false;
            }

            object[] arguments = new object[] { null };
            try
            {
                if ((bool)method.MakeGenericMethod(graphType).Invoke(variable, arguments))
                {
                    value = arguments[0];
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static System.Reflection.MethodInfo FindGenericTryGetDefaultValueMethod(Type type)
        {
            System.Reflection.MethodInfo[] methods = type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                System.Reflection.MethodInfo candidate = methods[i];
                if (candidate.Name != "TryGetDefaultValue" || !candidate.IsGenericMethodDefinition)
                {
                    continue;
                }

                System.Reflection.ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsByRef)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string ReadTooltip(IVariable variable)
        {
            if (variable == null)
            {
                return null;
            }

            System.Reflection.PropertyInfo property = variable.GetType().GetProperty("Tooltip", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            object value = property == null ? null : property.GetValue(variable, null);
            return value as string;
        }
    }
}
