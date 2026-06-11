using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEngine;
using UnityEngine.AI;

namespace BlueprintSystem.Editor
{
    internal static class BehaviorTreeGraphToolkitBlackboardSync
    {
        private static readonly Type[] SupportedGraphTypes =
        {
            typeof(string),
            typeof(bool),
            typeof(int),
            typeof(float),
            typeof(Vector2),
            typeof(Vector3),
            typeof(GameObject),
            typeof(Transform),
            typeof(NavMeshPath)
        };

        public static bool SyncBlackboardToGraph(BehaviorTreeVisualGraph graph)
        {
            if (graph == null)
            {
                return false;
            }

            BehaviorTreeGraphToolkitReflection.EnsureSupportedVariableTypes(graph, SupportedGraphTypes);
            if (graph.Blackboard == null)
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
            for (int i = 0; i < graph.Blackboard.Count; i++)
            {
                BehaviorTreeVisualBlackboardKeyData key = graph.Blackboard[i];
                if (key == null || string.IsNullOrEmpty(key.Name) || existingNames.Contains(key.Name))
                {
                    continue;
                }

                Type graphType;
                if (!TryGetGraphType(key.Type, out graphType))
                {
                    continue;
                }

                object defaultValue = null;
                if (key.HasDefaultValue)
                {
                    defaultValue = ConvertDefaultForGraph(key.DefaultValueJson, key.Type);
                }

                BehaviorTreeGraphToolkitReflection.CreateBlackboardVariable(graph, key.Name, graphType, defaultValue);
                existingNames.Add(key.Name);
                changed = true;
            }

            return changed;
        }

        public static List<BehaviorTreeVisualBlackboardKeyData> ExtractBlackboard(BehaviorTreeVisualGraph graph)
        {
            List<BehaviorTreeVisualBlackboardKeyData> result = new List<BehaviorTreeVisualBlackboardKeyData>();
            if (graph == null)
            {
                return result;
            }

            Dictionary<string, BehaviorTreeVisualBlackboardKeyData> metadataByName = BuildMetadataIndex(graph.Blackboard);
            List<IVariable> variables = new List<IVariable>(graph.GetVariables());
            if (variables.Count == 0)
            {
                return CloneBlackboard(graph.Blackboard);
            }

            for (int i = 0; i < variables.Count; i++)
            {
                IVariable variable = variables[i];
                if (variable == null || string.IsNullOrEmpty(variable.name))
                {
                    continue;
                }

                BehaviorTreeVisualBlackboardKeyData metadata;
                metadataByName.TryGetValue(variable.name, out metadata);

                string behaviorType;
                if (!TryGetBehaviorTreeType(variable, metadata, out behaviorType))
                {
                    continue;
                }

                BehaviorTreeVisualBlackboardKeyData key = new BehaviorTreeVisualBlackboardKeyData
                {
                    Name = variable.name,
                    Type = behaviorType,
                    Exposed = metadata != null && metadata.Exposed,
                    Persistent = metadata != null && metadata.Persistent,
                    Description = metadata == null ? ReadTooltip(variable) : metadata.Description
                };

                object defaultValue;
                if (TryReadBehaviorTreeDefaultValue(variable, behaviorType, out defaultValue))
                {
                    object normalized = BehaviorTreeValueUtility.NormalizeValueForJson(defaultValue, behaviorType);
                    if (normalized != null)
                    {
                        key.HasDefaultValue = true;
                        key.DefaultValueJson = BlueprintJson.Serialize(normalized, false);
                    }
                }

                result.Add(key);
            }

            return result;
        }

        private static Dictionary<string, BehaviorTreeVisualBlackboardKeyData> BuildMetadataIndex(List<BehaviorTreeVisualBlackboardKeyData> blackboard)
        {
            Dictionary<string, BehaviorTreeVisualBlackboardKeyData> result = new Dictionary<string, BehaviorTreeVisualBlackboardKeyData>(StringComparer.Ordinal);
            if (blackboard == null)
            {
                return result;
            }

            for (int i = 0; i < blackboard.Count; i++)
            {
                BehaviorTreeVisualBlackboardKeyData key = blackboard[i];
                if (key != null && !string.IsNullOrEmpty(key.Name))
                {
                    result[key.Name] = key;
                }
            }

            return result;
        }

        private static List<BehaviorTreeVisualBlackboardKeyData> CloneBlackboard(List<BehaviorTreeVisualBlackboardKeyData> blackboard)
        {
            List<BehaviorTreeVisualBlackboardKeyData> result = new List<BehaviorTreeVisualBlackboardKeyData>();
            if (blackboard == null)
            {
                return result;
            }

            for (int i = 0; i < blackboard.Count; i++)
            {
                BehaviorTreeVisualBlackboardKeyData key = CloneKey(blackboard[i]);
                if (key != null)
                {
                    result.Add(key);
                }
            }

            return result;
        }

        private static BehaviorTreeVisualBlackboardKeyData CloneKey(BehaviorTreeVisualBlackboardKeyData key)
        {
            if (key == null)
            {
                return null;
            }

            return new BehaviorTreeVisualBlackboardKeyData
            {
                Name = key.Name,
                Type = key.Type,
                HasDefaultValue = key.HasDefaultValue,
                DefaultValueJson = key.DefaultValueJson,
                Exposed = key.Exposed,
                Persistent = key.Persistent,
                Description = key.Description
            };
        }

        private static bool TryGetGraphType(string behaviorType, out Type graphType)
        {
            switch (behaviorType)
            {
                case "bool":
                    graphType = typeof(bool);
                    return true;
                case "int":
                    graphType = typeof(int);
                    return true;
                case "float":
                    graphType = typeof(float);
                    return true;
                case "Vector2":
                    graphType = typeof(Vector2);
                    return true;
                case "Vector3":
                    graphType = typeof(Vector3);
                    return true;
                case "GameObject":
                    graphType = typeof(GameObject);
                    return true;
                case "Transform":
                    graphType = typeof(Transform);
                    return true;
                case BehaviorTreeValueUtility.NavMeshPathTypeId:
                    graphType = typeof(NavMeshPath);
                    return true;
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                case BlueprintVariableTypeRegistry.BlueprintRefTypeId:
                default:
                    graphType = typeof(string);
                    return true;
            }
        }

        private static bool TryGetBehaviorTreeType(IVariable variable, BehaviorTreeVisualBlackboardKeyData metadata, out string behaviorType)
        {
            behaviorType = null;
            if (variable == null)
            {
                return false;
            }

            Type metadataGraphType;
            if (metadata != null &&
                !string.IsNullOrEmpty(metadata.Type) &&
                TryGetGraphType(metadata.Type, out metadataGraphType) &&
                metadataGraphType == variable.dataType)
            {
                behaviorType = metadata.Type;
                return true;
            }

            if (variable.dataType == typeof(bool))
            {
                behaviorType = "bool";
                return true;
            }

            if (variable.dataType == typeof(int))
            {
                behaviorType = "int";
                return true;
            }

            if (variable.dataType == typeof(float))
            {
                behaviorType = "float";
                return true;
            }

            if (variable.dataType == typeof(Vector2))
            {
                behaviorType = "Vector2";
                return true;
            }

            if (variable.dataType == typeof(Vector3))
            {
                behaviorType = "Vector3";
                return true;
            }

            if (variable.dataType == typeof(GameObject))
            {
                behaviorType = "GameObject";
                return true;
            }

            if (variable.dataType == typeof(Transform))
            {
                behaviorType = "Transform";
                return true;
            }

            if (variable.dataType == typeof(NavMeshPath))
            {
                behaviorType = BehaviorTreeValueUtility.NavMeshPathTypeId;
                return true;
            }

            if (variable.dataType == typeof(string))
            {
                behaviorType = metadata == null || string.IsNullOrEmpty(metadata.Type) ? "string" : metadata.Type;
                return true;
            }

            return false;
        }

        private static object ConvertDefaultForGraph(string json, string behaviorType)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            object value;
            try
            {
                value = BlueprintJson.Deserialize(json);
            }
            catch (BlueprintJsonException)
            {
                return null;
            }

            switch (behaviorType)
            {
                case "Vector2":
                    return BlueprintTypeUtility.ToVector2(value, Vector2.zero);
                case "Vector3":
                    return BlueprintTypeUtility.ToVector3(value, Vector3.zero);
                case "GameObject":
                case "Transform":
                case BehaviorTreeValueUtility.NavMeshPathTypeId:
                case BlueprintVariableTypeRegistry.BlueprintRefTypeId:
                    return null;
                default:
                    return BehaviorTreeValueUtility.CoerceValue(value, behaviorType);
            }
        }

        private static bool TryReadBehaviorTreeDefaultValue(IVariable variable, string behaviorType, out object value)
        {
            value = null;
            switch (behaviorType)
            {
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
                        value = vector2Value;
                        return true;
                    }

                    return false;
                case "Vector3":
                    Vector3 vector3Value;
                    if (variable.TryGetDefaultValue(out vector3Value))
                    {
                        value = vector3Value;
                        return true;
                    }

                    return false;
                case "GameObject":
                case "Transform":
                case BehaviorTreeValueUtility.NavMeshPathTypeId:
                case BlueprintVariableTypeRegistry.BlueprintRefTypeId:
                    return false;
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                default:
                    string stringValue;
                    if (variable.TryGetDefaultValue(out stringValue))
                    {
                        value = stringValue;
                        return true;
                    }

                    return false;
            }
        }

        private static string ReadTooltip(IVariable variable)
        {
            if (variable == null)
            {
                return null;
            }

            PropertyInfo property = variable.GetType().GetProperty("Tooltip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object value = property == null ? null : property.GetValue(variable, null);
            return value as string;
        }
    }
}
