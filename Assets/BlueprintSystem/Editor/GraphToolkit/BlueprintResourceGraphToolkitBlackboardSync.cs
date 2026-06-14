using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    public static class BlueprintResourceGraphToolkitBlackboardSync
    {
        public const string ResourceTypeVariableName = "resourceType";
        public const string ResourceNameVariableName = "resourceName";
        public const string DisplayNameVariableName = "displayName";
        public const string MainAssetVariableName = "mainAsset";
        public const string MainAssetPathVariableName = "mainAssetPath";
        public const string MainAssetGuidVariableName = "mainAssetGuid";
        public const string MainAssetTypeVariableName = "mainAssetType";

        private static readonly string[] ResourceVariableNames =
        {
            ResourceTypeVariableName,
            ResourceNameVariableName,
            DisplayNameVariableName,
            MainAssetVariableName
        };

        private static readonly string[] LegacyMainAssetVariableNames =
        {
            MainAssetPathVariableName,
            MainAssetGuidVariableName,
            MainAssetTypeVariableName
        };

        public static bool EnsureResourceBlackboard(BlueprintResourceVisualGraph graph)
        {
            if (graph == null)
            {
                return false;
            }

            BlueprintResourceGraphToolkitReflection.EnsureSupportedVariableTypes(
                graph,
                new[]
                {
                    typeof(string),
                    typeof(BlueprintResourceTypeReference),
                    typeof(BlueprintResourceAssetReference)
                });

            Dictionary<string, IVariable> variables = GetVariableMap(graph);
            bool changed = false;
            changed |= MigrateLegacyMainAssetVariables(graph, variables);
            changed |= TryAutofillResourceNameFromMainAsset(graph, null);

            for (int i = 0; i < LegacyMainAssetVariableNames.Length; i++)
            {
                changed |= DeleteBlackboardVariable(graph, variables, LegacyMainAssetVariableNames[i]);
            }

            for (int i = 0; i < ResourceVariableNames.Length; i++)
            {
                changed |= EnsureBlackboardVariable(graph, variables, ResourceVariableNames[i]);
            }

            if (changed)
            {
                BlueprintGraphToolkitReflection.MarkDirty(graph);
            }

            return changed;
        }

        public static bool SyncGraphFieldsToBlackboard(BlueprintResourceVisualGraph graph)
        {
            if (graph == null)
            {
                return false;
            }

            bool changed = TryAutofillResourceNameFromMainAsset(graph, null);
            changed |= EnsureResourceBlackboard(graph);
            Dictionary<string, IVariable> variables = GetVariableMap(graph);
            for (int i = 0; i < ResourceVariableNames.Length; i++)
            {
                string variableName = ResourceVariableNames[i];
                IVariable variable;
                if (!variables.TryGetValue(variableName, out variable) || variable == null)
                {
                    continue;
                }

                Type valueType = GetVariableType(variableName);
                object current;
                object next = GetGraphFieldValue(graph, variableName);
                if (!TryReadFieldValue(variable, variableName, out current) ||
                    !AreFieldValuesEqual(variableName, current, next))
                {
                    if (BlueprintResourceGraphToolkitReflection.TrySetDefaultValue(
                        variable,
                        valueType,
                        CloneDefaultValue(variableName, next)))
                    {
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                BlueprintGraphToolkitReflection.MarkDirty(graph);
            }

            return changed;
        }

        public static bool SyncBlackboardToGraphFields(BlueprintResourceVisualGraph graph)
        {
            if (graph == null)
            {
                return false;
            }

            bool changed = EnsureResourceBlackboard(graph);
            Dictionary<string, IVariable> variables = GetVariableMap(graph);
            if (variables.Count == 0)
            {
                return changed;
            }

            changed |= ApplyBlackboardValue(graph, variables, ResourceTypeVariableName);
            changed |= ApplyBlackboardValue(graph, variables, ResourceNameVariableName);
            changed |= ApplyBlackboardValue(graph, variables, DisplayNameVariableName);
            changed |= ApplyBlackboardValue(graph, variables, MainAssetVariableName);
            changed |= TryAutofillResourceNameFromMainAsset(graph, variables);
            if (changed)
            {
                BlueprintGraphToolkitReflection.MarkDirty(graph);
            }

            return changed;
        }

        public static bool HasResourceBlackboardVariable(BlueprintResourceVisualGraph graph, string variableName)
        {
            if (graph == null || string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            return GetVariableMap(graph).ContainsKey(variableName);
        }

        public static bool TryGetBlackboardValue(BlueprintResourceVisualGraph graph, string variableName, out string value)
        {
            value = null;
            if (graph == null || string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            Dictionary<string, IVariable> variables = GetVariableMap(graph);
            IVariable variable;
            if (!variables.TryGetValue(variableName, out variable) || variable == null)
            {
                return false;
            }

            object fieldValue;
            if (IsResourceVariableName(variableName) && TryReadFieldValue(variable, variableName, out fieldValue))
            {
                value = FieldValueToString(variableName, fieldValue);
                return true;
            }

            return BlueprintResourceGraphToolkitReflection.TryReadDefaultValue(variable, out value);
        }

        public static bool TrySetBlackboardValue(BlueprintResourceVisualGraph graph, string variableName, string value)
        {
            if (graph == null || string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            EnsureResourceBlackboard(graph);
            Dictionary<string, IVariable> variables = GetVariableMap(graph);
            IVariable variable;
            if (!variables.TryGetValue(variableName, out variable) || variable == null)
            {
                return false;
            }

            object nextValue = value ?? string.Empty;
            Type valueType = typeof(string);
            if (variableName == ResourceTypeVariableName)
            {
                valueType = typeof(BlueprintResourceTypeReference);
                nextValue = BlueprintResourceGraphToolkitTypes.CreateResourceTypeReference(value);
            }
            else if (variableName == MainAssetVariableName)
            {
                valueType = typeof(BlueprintResourceAssetReference);
                nextValue = new BlueprintResourceAssetReference
                {
                    Path = BlueprintAssetDiscovery.NormalizeAssetPath(value),
                    Guid = string.Empty,
                    Address = string.Empty,
                    AssetType = string.Empty
                };
            }

            object current;
            if (IsResourceVariableName(variableName) &&
                TryReadFieldValue(variable, variableName, out current) &&
                AreFieldValuesEqual(variableName, current, nextValue))
            {
                return false;
            }

            bool changed = BlueprintResourceGraphToolkitReflection.TrySetDefaultValue(
                variable,
                valueType,
                CloneDefaultValue(variableName, nextValue));
            if (changed)
            {
                BlueprintGraphToolkitReflection.MarkDirty(graph);
            }

            return changed;
        }

        public static bool TryGetBlackboardAssetReference(
            BlueprintResourceVisualGraph graph,
            out BlueprintResourceAssetReference value)
        {
            value = null;
            if (graph == null)
            {
                return false;
            }

            Dictionary<string, IVariable> variables = GetVariableMap(graph);
            IVariable variable;
            object fieldValue;
            if (!variables.TryGetValue(MainAssetVariableName, out variable) ||
                !TryReadFieldValue(variable, MainAssetVariableName, out fieldValue))
            {
                return false;
            }

            value = CloneAssetReference(fieldValue as BlueprintResourceAssetReference);
            return value != null;
        }

        public static bool TrySetBlackboardAssetReference(
            BlueprintResourceVisualGraph graph,
            BlueprintResourceAssetReference value)
        {
            if (graph == null)
            {
                return false;
            }

            EnsureResourceBlackboard(graph);
            Dictionary<string, IVariable> variables = GetVariableMap(graph);
            IVariable variable;
            if (!variables.TryGetValue(MainAssetVariableName, out variable) || variable == null)
            {
                return false;
            }

            BlueprintResourceAssetReference next = CloneAssetReference(value);
            object current;
            if (TryReadFieldValue(variable, MainAssetVariableName, out current) &&
                AreAssetReferencesEqual(current as BlueprintResourceAssetReference, next))
            {
                return false;
            }

            bool changed = BlueprintResourceGraphToolkitReflection.TrySetDefaultValue(
                variable,
                typeof(BlueprintResourceAssetReference),
                next);
            if (changed)
            {
                BlueprintGraphToolkitReflection.MarkDirty(graph);
            }

            return changed;
        }

        private static bool EnsureBlackboardVariable(
            BlueprintResourceVisualGraph graph,
            Dictionary<string, IVariable> variables,
            string variableName)
        {
            bool changed = false;
            Type expectedType = GetVariableType(variableName);
            IVariable variable;
            if (variables.TryGetValue(variableName, out variable) && variable != null)
            {
                if (variable.dataType == expectedType)
                {
                    return false;
                }

                ApplyBlackboardValue(graph, variables, variableName);
                if (BlueprintResourceGraphToolkitReflection.DeleteBlackboardVariable(graph, variable))
                {
                    variables.Remove(variableName);
                    changed = true;
                }
            }

            if (!variables.ContainsKey(variableName))
            {
                IVariable created = BlueprintResourceGraphToolkitReflection.CreateBlackboardVariable(
                    graph,
                    variableName,
                    expectedType,
                    CloneDefaultValue(variableName, GetGraphFieldValue(graph, variableName)));
                if (created != null)
                {
                    variables[variableName] = created;
                }

                changed = true;
            }

            return changed;
        }

        private static bool MigrateLegacyMainAssetVariables(
            BlueprintResourceVisualGraph graph,
            Dictionary<string, IVariable> variables)
        {
            if (graph == null || variables == null)
            {
                return false;
            }

            IVariable existingMainAssetVariable;
            object existingMainAsset;
            if (variables.TryGetValue(MainAssetVariableName, out existingMainAssetVariable) &&
                TryReadFieldValue(existingMainAssetVariable, MainAssetVariableName, out existingMainAsset) &&
                !IsAssetReferenceEmpty(existingMainAsset as BlueprintResourceAssetReference))
            {
                return false;
            }

            BlueprintResourceAssetReference legacy = new BlueprintResourceAssetReference();
            bool hasLegacy = false;
            string value;
            if (TryReadStringValue(variables, MainAssetPathVariableName, out value))
            {
                legacy.Path = value;
                hasLegacy |= !string.IsNullOrEmpty(value);
            }

            if (TryReadStringValue(variables, MainAssetGuidVariableName, out value))
            {
                legacy.Guid = value;
                hasLegacy |= !string.IsNullOrEmpty(value);
            }

            if (TryReadStringValue(variables, MainAssetTypeVariableName, out value))
            {
                legacy.AssetType = value;
                hasLegacy |= !string.IsNullOrEmpty(value);
            }

            if (!hasLegacy)
            {
                return false;
            }

            if (!AreAssetReferencesEqual(graph.MainAsset, legacy))
            {
                graph.MainAsset = legacy;
                return true;
            }

            return false;
        }

        private static bool DeleteBlackboardVariable(
            BlueprintResourceVisualGraph graph,
            Dictionary<string, IVariable> variables,
            string variableName)
        {
            IVariable variable;
            if (variables == null || !variables.TryGetValue(variableName, out variable) || variable == null)
            {
                return false;
            }

            if (!BlueprintResourceGraphToolkitReflection.DeleteBlackboardVariable(graph, variable))
            {
                return false;
            }

            variables.Remove(variableName);
            return true;
        }

        private static bool ApplyBlackboardValue(
            BlueprintResourceVisualGraph graph,
            Dictionary<string, IVariable> variables,
            string variableName)
        {
            IVariable variable;
            object value;
            if (!variables.TryGetValue(variableName, out variable) ||
                !TryReadFieldValue(variable, variableName, out value))
            {
                return false;
            }

            return SetGraphFieldValue(graph, variableName, value);
        }

        private static Dictionary<string, IVariable> GetVariableMap(BlueprintResourceVisualGraph graph)
        {
            Dictionary<string, IVariable> result = new Dictionary<string, IVariable>(StringComparer.Ordinal);
            if (graph == null)
            {
                return result;
            }

            foreach (IVariable variable in graph.GetVariables())
            {
                if (variable != null && !string.IsNullOrEmpty(variable.name) && !result.ContainsKey(variable.name))
                {
                    result.Add(variable.name, variable);
                }
            }

            return result;
        }

        private static object GetGraphFieldValue(BlueprintResourceVisualGraph graph, string variableName)
        {
            if (graph == null)
            {
                return GetEmptyFieldValue(variableName);
            }

            switch (variableName)
            {
                case ResourceTypeVariableName:
                    return BlueprintResourceGraphToolkitTypes.CreateResourceTypeReference(graph.ResourceType);
                case ResourceNameVariableName:
                    return graph.ResourceName ?? string.Empty;
                case DisplayNameVariableName:
                    return graph.DisplayName ?? string.Empty;
                case MainAssetVariableName:
                    return CloneAssetReference(graph.MainAsset);
                default:
                    return GetEmptyFieldValue(variableName);
            }
        }

        private static object GetEmptyFieldValue(string variableName)
        {
            switch (variableName)
            {
                case ResourceTypeVariableName:
                    return BlueprintResourceGraphToolkitTypes.CreateResourceTypeReference(string.Empty);
                case MainAssetVariableName:
                    return new BlueprintResourceAssetReference();
                default:
                    return string.Empty;
            }
        }

        private static bool SetGraphFieldValue(BlueprintResourceVisualGraph graph, string variableName, object value)
        {
            if (graph == null)
            {
                return false;
            }

            switch (variableName)
            {
                case ResourceTypeVariableName:
                    return SetString(ref graph.ResourceType, FieldValueToString(variableName, value));
                case ResourceNameVariableName:
                    return SetString(ref graph.ResourceName, value == null ? string.Empty : Convert.ToString(value));
                case DisplayNameVariableName:
                    return SetString(ref graph.DisplayName, value == null ? string.Empty : Convert.ToString(value));
                case MainAssetVariableName:
                    BlueprintResourceAssetReference reference = CloneAssetReference(value as BlueprintResourceAssetReference);
                    if (AreAssetReferencesEqual(graph.MainAsset, reference))
                    {
                        return false;
                    }

                    graph.MainAsset = reference;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadFieldValue(IVariable variable, string variableName, out object value)
        {
            value = null;
            if (variable == null)
            {
                return false;
            }

            switch (variableName)
            {
                case ResourceTypeVariableName:
                    return TryReadResourceTypeValue(variable, out value);
                case MainAssetVariableName:
                    return TryReadMainAssetValue(variable, out value);
                case ResourceNameVariableName:
                case DisplayNameVariableName:
                    string text;
                    if (BlueprintResourceGraphToolkitReflection.TryReadDefaultValue(variable, out text))
                    {
                        value = text ?? string.Empty;
                        return true;
                    }

                    return false;
                default:
                    return BlueprintResourceGraphToolkitReflection.TryReadDefaultValue(variable, out value);
            }
        }

        private static bool TryReadResourceTypeValue(IVariable variable, out object value)
        {
            value = null;
            object typed;
            if (BlueprintResourceGraphToolkitReflection.TryReadDefaultValue(
                    variable,
                    typeof(BlueprintResourceTypeReference),
                    out typed))
            {
                value = CreateResourceTypeReferenceFromBlackboard(variable, typed);
                return true;
            }

            string text;
            if (BlueprintResourceGraphToolkitReflection.TryReadDefaultValue(variable, out text))
            {
                value = BlueprintResourceGraphToolkitTypes.CreateResourceTypeReference(text);
                return true;
            }

            if (BlueprintResourceGraphToolkitReflection.TryReadDefaultValue(variable, out typed))
            {
                value = CreateResourceTypeReferenceFromBlackboard(variable, typed);
                return true;
            }

            return false;
        }

        private static BlueprintResourceTypeReference CreateResourceTypeReferenceFromBlackboard(
            IVariable variable,
            object rawValue)
        {
            string resourceType;
            BlueprintResourceGraphToolkitTypes.TryGetResourceType(rawValue, out resourceType);
            if (string.Equals(resourceType, "None", StringComparison.Ordinal) &&
                IsLegacyResourceTypeEnumValue(variable, rawValue))
            {
                resourceType = string.Empty;
            }

            return BlueprintResourceGraphToolkitTypes.CreateResourceTypeReference(resourceType);
        }

        private static bool IsLegacyResourceTypeEnumValue(IVariable variable, object rawValue)
        {
            Type rawType = rawValue == null ? null : rawValue.GetType();
            if (rawType != null && rawType.Name == "BlueprintResourceType")
            {
                return true;
            }

            Type dataType = variable == null ? null : variable.dataType;
            return dataType != null && dataType.Name == "BlueprintResourceType";
        }

        private static bool TryReadMainAssetValue(IVariable variable, out object value)
        {
            value = null;
            object typed;
            if (BlueprintResourceGraphToolkitReflection.TryReadDefaultValue(
                    variable,
                    typeof(BlueprintResourceAssetReference),
                    out typed))
            {
                BlueprintResourceAssetReference reference = typed as BlueprintResourceAssetReference;
                value = CloneAssetReference(reference);
                return reference != null;
            }

            return false;
        }

        private static bool TryReadStringValue(
            Dictionary<string, IVariable> variables,
            string variableName,
            out string value)
        {
            value = null;
            IVariable variable;
            return variables != null &&
                   variables.TryGetValue(variableName, out variable) &&
                   BlueprintResourceGraphToolkitReflection.TryReadDefaultValue(variable, out value);
        }

        private static bool TryAutofillResourceNameFromMainAsset(
            BlueprintResourceVisualGraph graph,
            Dictionary<string, IVariable> variables)
        {
            if (graph == null || !string.IsNullOrEmpty(graph.ResourceName))
            {
                return false;
            }

            string assetName = BlueprintResourceGraphToolkitTypes.GetAssetReferenceName(graph.MainAsset);
            if (string.IsNullOrEmpty(assetName))
            {
                return false;
            }

            bool changed = SetString(ref graph.ResourceName, assetName);
            IVariable resourceNameVariable;
            if (changed &&
                variables != null &&
                variables.TryGetValue(ResourceNameVariableName, out resourceNameVariable) &&
                resourceNameVariable != null)
            {
                BlueprintResourceGraphToolkitReflection.TrySetDefaultValue(resourceNameVariable, assetName);
            }

            return changed;
        }

        private static Type GetVariableType(string variableName)
        {
            switch (variableName)
            {
                case ResourceTypeVariableName:
                    return typeof(BlueprintResourceTypeReference);
                case MainAssetVariableName:
                    return typeof(BlueprintResourceAssetReference);
                default:
                    return typeof(string);
            }
        }

        private static bool IsResourceVariableName(string variableName)
        {
            for (int i = 0; i < ResourceVariableNames.Length; i++)
            {
                if (ResourceVariableNames[i] == variableName)
                {
                    return true;
                }
            }

            return false;
        }

        private static object CloneDefaultValue(string variableName, object value)
        {
            if (variableName == MainAssetVariableName)
            {
                return CloneAssetReference(value as BlueprintResourceAssetReference);
            }

            return value;
        }

        private static bool AreFieldValuesEqual(string variableName, object left, object right)
        {
            if (variableName == MainAssetVariableName)
            {
                return AreAssetReferencesEqual(
                    left as BlueprintResourceAssetReference,
                    right as BlueprintResourceAssetReference);
            }

            return string.Equals(
                FieldValueToString(variableName, left),
                FieldValueToString(variableName, right),
                StringComparison.Ordinal);
        }

        private static string FieldValueToString(string variableName, object value)
        {
            if (variableName == ResourceTypeVariableName)
            {
                string resourceType;
                BlueprintResourceGraphToolkitTypes.TryGetResourceType(value, out resourceType);
                return resourceType;
            }

            if (variableName == MainAssetVariableName)
            {
                BlueprintResourceAssetReference reference = value as BlueprintResourceAssetReference;
                return reference == null ? string.Empty : reference.Path ?? string.Empty;
            }

            return value == null ? string.Empty : Convert.ToString(value);
        }

        private static BlueprintResourceAssetReference CloneAssetReference(BlueprintResourceAssetReference reference)
        {
            if (reference == null)
            {
                return new BlueprintResourceAssetReference();
            }

            return new BlueprintResourceAssetReference
            {
                Guid = reference.Guid ?? string.Empty,
                Path = reference.Path ?? string.Empty,
                Address = reference.Address ?? string.Empty,
                AssetType = reference.AssetType ?? string.Empty
            };
        }

        private static bool AreAssetReferencesEqual(
            BlueprintResourceAssetReference left,
            BlueprintResourceAssetReference right)
        {
            left = left ?? new BlueprintResourceAssetReference();
            right = right ?? new BlueprintResourceAssetReference();
            return string.Equals(left.Guid ?? string.Empty, right.Guid ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(left.Path ?? string.Empty, right.Path ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(left.Address ?? string.Empty, right.Address ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(left.AssetType ?? string.Empty, right.AssetType ?? string.Empty, StringComparison.Ordinal);
        }

        private static bool IsAssetReferenceEmpty(BlueprintResourceAssetReference reference)
        {
            return AreAssetReferencesEqual(reference, new BlueprintResourceAssetReference());
        }

        private static bool SetString(ref string target, string value)
        {
            if (string.Equals(target ?? string.Empty, value ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            target = value;
            return true;
        }
    }

    internal static class BlueprintResourceGraphToolkitReflection
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static IVariable CreateBlackboardVariable(BlueprintResourceVisualGraph graph, string name, Type valueType, object defaultValue)
        {
            object implementation = GetGraphImplementation(graph);
            MethodInfo createVariableMethod = FindMethod(implementation.GetType(), "CreateVariable", method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 4 &&
                       parameters[0].ParameterType == typeof(string) &&
                       parameters[1].ParameterType == typeof(Type) &&
                       parameters[3].ParameterType == typeof(VariableKind);
            });

            if (createVariableMethod == null)
            {
                throw new MissingMethodException(implementation.GetType().FullName, "CreateVariable");
            }

            return createVariableMethod.Invoke(
                implementation,
                new object[] { name, valueType, defaultValue, VariableKind.Local }) as IVariable;
        }

        public static bool DeleteBlackboardVariable(BlueprintResourceVisualGraph graph, IVariable variable)
        {
            if (graph == null || variable == null)
            {
                return false;
            }

            object implementation = GetGraphImplementation(graph);
            MethodInfo deleteMethod = FindMethod(implementation.GetType(), "DeleteVariableDeclaration", method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 2;
            });
            if (deleteMethod == null)
            {
                return false;
            }

            deleteMethod.Invoke(implementation, new object[] { variable, false });
            return true;
        }

        public static void EnsureSupportedVariableTypes(BlueprintResourceVisualGraph graph, IEnumerable<Type> supportedTypes)
        {
            if (graph == null || supportedTypes == null)
            {
                return;
            }

            object implementation = GetGraphImplementation(graph);
            PropertyInfo supportedTypesProperty = FindProperty(implementation.GetType(), "SupportedTypes");
            if (supportedTypesProperty != null)
            {
                supportedTypesProperty.GetValue(implementation, null);
            }

            FieldInfo supportedTypesField = FindField(implementation.GetType(), "m_SupportedTypes");
            List<Type> list = supportedTypesField == null ? null : supportedTypesField.GetValue(implementation) as List<Type>;
            if (list == null)
            {
                return;
            }

            foreach (Type type in supportedTypes)
            {
                if (type != null && !list.Contains(type))
                {
                    list.Add(type);
                }
            }

            list.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
        }

        public static bool TryReadDefaultValue(IVariable variable, out string value)
        {
            value = null;
            object objectValue;
            if (!TryReadDefaultValue(variable, typeof(string), out objectValue))
            {
                return false;
            }

            value = objectValue == null ? string.Empty : Convert.ToString(objectValue);
            return true;
        }

        public static bool TryReadDefaultValue(IVariable variable, out object value)
        {
            return TryReadDefaultValue(variable, typeof(object), out value);
        }

        public static bool TryReadDefaultValue(IVariable variable, Type valueType, out object value)
        {
            value = null;
            if (variable == null || valueType == null)
            {
                return false;
            }

            MethodInfo method = FindGenericTryGetDefaultValueMethod(variable.GetType());
            if (method == null)
            {
                method = FindGenericTryGetDefaultValueMethod(typeof(IVariable));
            }

            if (method != null)
            {
                object[] arguments = new object[] { null };
                try
                {
                    if ((bool)method.MakeGenericMethod(valueType).Invoke(variable, arguments))
                    {
                        value = arguments[0];
                        return true;
                    }
                }
                catch
                {
                }
            }

            return TryReadInitializationObjectValue(variable, out value);
        }

        public static bool TrySetDefaultValue(IVariable variable, string value)
        {
            return TrySetDefaultValue(variable, typeof(string), value ?? string.Empty);
        }

        public static bool TrySetDefaultValue(IVariable variable, Type valueType, object value)
        {
            if (variable == null || valueType == null)
            {
                return false;
            }

            MethodInfo method = FindGenericSetDefaultValueMethod(variable.GetType());
            if (method == null)
            {
                method = FindGenericSetDefaultValueMethod(typeof(IVariable));
            }

            if (method != null)
            {
                try
                {
                    object normalized = valueType == typeof(string) && value == null ? string.Empty : value;
                    method.MakeGenericMethod(valueType).Invoke(variable, new[] { normalized });
                    return true;
                }
                catch
                {
                }
            }

            return TrySetInitializationObjectValue(variable, valueType == typeof(string) && value == null ? string.Empty : value);
        }

        private static MethodInfo FindGenericTryGetDefaultValueMethod(Type type)
        {
            MethodInfo[] methods = type.GetMethods(Flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo candidate = methods[i];
                if (!candidate.IsGenericMethodDefinition || candidate.Name != "TryGetDefaultValue")
                {
                    continue;
                }

                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsByRef)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static MethodInfo FindGenericSetDefaultValueMethod(Type type)
        {
            MethodInfo[] methods = type.GetMethods(Flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo candidate = methods[i];
                if (!candidate.IsGenericMethodDefinition ||
                    candidate.Name != "SetDefaultValue" && candidate.Name != "SetValue")
                {
                    continue;
                }

                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length == 1)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool TryReadInitializationObjectValue(IVariable variable, out object value)
        {
            value = null;
            object initializationModel = GetInitializationModel(variable, false);
            if (initializationModel == null)
            {
                return false;
            }

            PropertyInfo objectValueProperty = FindProperty(initializationModel.GetType(), "ObjectValue");
            if (objectValueProperty == null || !objectValueProperty.CanRead)
            {
                return false;
            }

            value = objectValueProperty.GetValue(initializationModel, null);
            return true;
        }

        private static bool TrySetInitializationObjectValue(IVariable variable, object value)
        {
            object initializationModel = GetInitializationModel(variable, true);
            if (initializationModel == null)
            {
                return false;
            }

            PropertyInfo objectValueProperty = FindProperty(initializationModel.GetType(), "ObjectValue");
            if (objectValueProperty == null || !objectValueProperty.CanWrite)
            {
                return false;
            }

            object current = objectValueProperty.GetValue(initializationModel, null);
            if (Equals(current, value))
            {
                return false;
            }

            objectValueProperty.SetValue(initializationModel, value, null);
            return true;
        }

        private static object GetInitializationModel(IVariable variable, bool createIfMissing)
        {
            if (variable == null)
            {
                return null;
            }

            PropertyInfo initializationProperty = FindProperty(variable.GetType(), "InitializationModel");
            object initializationModel = initializationProperty == null ? null : initializationProperty.GetValue(variable, null);
            if (initializationModel == null && createIfMissing)
            {
                MethodInfo createInitializationValueMethod = FindMethod(variable.GetType(), "CreateInitializationValue", method => method.GetParameters().Length == 0);
                if (createInitializationValueMethod != null)
                {
                    createInitializationValueMethod.Invoke(variable, null);
                    initializationModel = initializationProperty == null ? null : initializationProperty.GetValue(variable, null);
                }
            }

            return initializationModel;
        }

        private static object GetGraphImplementation(Graph graph)
        {
            FieldInfo implementationField = typeof(Graph).GetField("m_Implementation", Flags);
            object implementation = implementationField == null ? null : implementationField.GetValue(graph);
            if (implementation == null)
            {
                throw new InvalidOperationException("Graph has no Graph Toolkit implementation. Load or create it through GraphDatabase first.");
            }

            return implementation;
        }

        private static MethodInfo FindMethod(Type type, string name, Predicate<MethodInfo> predicate)
        {
            MethodInfo[] methods = type.GetMethods(Flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name == name && (predicate == null || predicate(method)))
                {
                    return method;
                }
            }

            return null;
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            while (type != null)
            {
                PropertyInfo property = type.GetProperty(name, Flags);
                if (property != null)
                {
                    return property;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, Flags);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
