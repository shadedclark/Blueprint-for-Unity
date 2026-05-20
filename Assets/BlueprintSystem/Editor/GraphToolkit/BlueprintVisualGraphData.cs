using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace BlueprintSystem.Editor
{
    [Serializable]
    public sealed class BlueprintVisualPortData
    {
        public string Id;
        public string DisplayName;
        public string Kind;
        public string Type;
        public bool Required;
        public string Source;
        public bool AllowMultiple;
    }

    [Serializable]
    public sealed class BlueprintVisualPropertyData
    {
        public string Id;
        public string DisplayName;
        public string Type;
        public bool Required;
        public bool HasValue;
        public string JsonValue;
        public bool ShowInInspectorOnly;
    }

    [Serializable]
    public sealed class BlueprintVisualVariableData
    {
        public string Id;
        public string Name;
        public string Type;
        public bool HasDefaultValue;
        public string JsonDefaultValue;
        public string Scope;
        public bool Exposed;
        public bool Persistent;
        public string Description;
    }

    [Serializable]
    public sealed class BlueprintVisualBindingData
    {
        public string Name;
        public string Type;
        public bool Required;
    }

    [Serializable]
    public sealed class BlueprintVisualComponentData
    {
        public string Name;
        public string Blueprint;
        public bool Required;
    }

    internal static class BlueprintVisualValueUtility
    {
        public static string ToJson(object value)
        {
            return BlueprintJson.Serialize(value, false);
        }

        public static object FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return BlueprintJson.Deserialize(json);
        }

        public static Type ToGraphType(string blueprintType)
        {
            if (string.IsNullOrEmpty(blueprintType))
            {
                return typeof(object);
            }

            Type registeredType;
            if (BlueprintGraphToolkitTypeRegistry.TryGetGraphType(blueprintType, out registeredType))
            {
                return registeredType;
            }

            switch (blueprintType)
            {
                case "bool":
                    return typeof(bool);
                case "int":
                    return typeof(int);
                case "float":
                    return typeof(float);
                case "Vector2":
                    return typeof(Vector2);
                case "Vector3":
                    return typeof(Vector3);
                case "Vector4":
                    return typeof(Vector4);
                case "Rect":
                    return typeof(Rect);
                case "Color":
                    return typeof(Color);
                case "string":
                    return typeof(string);
                default:
                    if (blueprintType.StartsWith("UIBinding<", StringComparison.Ordinal))
                    {
                        return typeof(string);
                    }

                    return typeof(string);
            }
        }

        public static object ConvertForGraphField(object value, string blueprintType)
        {
            if (blueprintType == BlueprintGraphToolkitBlueprintTypes.TypeId)
            {
                return BlueprintGraphToolkitBlueprintTypes.CreateGraphValue(value);
            }

            if (BlueprintArrayUtility.IsArrayType(blueprintType))
            {
                object jsonValue;
                if (BlueprintArrayUtility.TryConvertToJsonArray(value, blueprintType, out jsonValue))
                {
                    return BlueprintGraphToolkitArrayTypes.CreateGraphValue(ToJson(jsonValue), blueprintType);
                }

                return BlueprintGraphToolkitArrayTypes.CreateGraphValue(
                    value == null ? "[]" : Convert.ToString(value, CultureInfo.InvariantCulture),
                    blueprintType);
            }

            if (BlueprintVariableTypeRegistry.IsCustomType(blueprintType))
            {
                object structuredValue;
                if (BlueprintStructuredValueUtility.TryConvertToRuntimeValue(value, blueprintType, out structuredValue))
                {
                    return structuredValue;
                }

                return value;
            }

            Type graphType = ToGraphType(blueprintType);
            if (graphType == typeof(string))
            {
                return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (graphType.IsEnum)
            {
                return ConvertToEnumValue(value, graphType);
            }

            if (graphType == typeof(bool))
            {
                return BlueprintTypeUtility.ConvertValue(value, typeof(bool), false);
            }

            if (graphType == typeof(int))
            {
                return BlueprintTypeUtility.ConvertValue(value, typeof(int), 0);
            }

            if (graphType == typeof(float))
            {
                return BlueprintTypeUtility.ConvertValue(value, typeof(float), 0f);
            }

            if (graphType == typeof(Vector2))
            {
                return BlueprintTypeUtility.ToVector2(value, Vector2.zero);
            }

            if (graphType == typeof(Vector3))
            {
                return BlueprintTypeUtility.ToVector3(value, Vector3.zero);
            }

            if (graphType == typeof(Vector4))
            {
                return BlueprintTypeUtility.ToVector4(value, Vector4.zero);
            }

            if (graphType == typeof(Rect))
            {
                return BlueprintTypeUtility.ToRect(value, Rect.zero);
            }

            if (graphType == typeof(Color))
            {
                IList list = value as IList;
                if (list != null && list.Count >= 3)
                {
                    float r = Convert.ToSingle(list[0], CultureInfo.InvariantCulture);
                    float g = Convert.ToSingle(list[1], CultureInfo.InvariantCulture);
                    float b = Convert.ToSingle(list[2], CultureInfo.InvariantCulture);
                    float a = list.Count >= 4 ? Convert.ToSingle(list[3], CultureInfo.InvariantCulture) : 1f;
                    return new Color(r, g, b, a);
                }

                return Color.white;
            }

            return value;
        }

        public static object ConvertFromGraphField(object value, string blueprintType)
        {
            if (blueprintType == BlueprintGraphToolkitBlueprintTypes.TypeId)
            {
                string blueprintPath;
                return BlueprintGraphToolkitBlueprintTypes.TryGetPath(value, out blueprintPath) ? blueprintPath : string.Empty;
            }

            if (BlueprintArrayUtility.IsArrayType(blueprintType))
            {
                string json;
                if (BlueprintGraphToolkitArrayTypes.TryGetJson(value, out json))
                {
                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            return BlueprintJson.Deserialize(json);
                        }
                        catch (BlueprintJsonException)
                        {
                            return new List<object>();
                        }
                    }

                    return new List<object>();
                }

                object jsonValue;
                if (BlueprintArrayUtility.TryConvertToJsonArray(value, blueprintType, out jsonValue))
                {
                    return jsonValue;
                }

                string text = value as string;
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        return BlueprintJson.Deserialize(text);
                    }
                    catch (BlueprintJsonException)
                    {
                        return new List<object>();
                    }
                }

                return new List<object>();
            }

            object structuredValue;
            if (BlueprintStructuredValueUtility.TryConvertToJsonValue(value, blueprintType, out structuredValue))
            {
                return structuredValue;
            }

            if (value != null && value.GetType().IsEnum)
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

            if (blueprintType == "int")
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }

            return value;
        }

        public static bool TryReadPortValue(Unity.GraphToolkit.Editor.IPort port, string blueprintType, out object value)
        {
            Type graphType = ToGraphType(blueprintType);
            if (graphType == typeof(bool))
            {
                bool typed;
                if (port.TryGetValue(out typed))
                {
                    value = typed;
                    return true;
                }
            }
            else if (graphType == typeof(int))
            {
                int typed;
                if (port.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType == typeof(float))
            {
                float typed;
                if (port.TryGetValue(out typed))
                {
                    value = typed;
                    return true;
                }
            }
            else if (graphType == typeof(Vector2))
            {
                Vector2 typed;
                if (port.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType == typeof(Vector3))
            {
                Vector3 typed;
                if (port.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType == typeof(Vector4))
            {
                Vector4 typed;
                if (port.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType == typeof(Rect))
            {
                Rect typed;
                if (port.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType == typeof(Color))
            {
                Color typed;
                if (port.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType.IsEnum)
            {
                object typed;
                if (TryReadGraphValue(port, graphType, out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (BlueprintArrayUtility.IsArrayType(blueprintType))
            {
                object typed;
                if (TryReadGraphValue(port, graphType, out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (blueprintType == BlueprintGraphToolkitBlueprintTypes.TypeId)
            {
                object typed;
                if (TryReadGraphValue(port, graphType, out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (BlueprintVariableTypeRegistry.IsCustomType(blueprintType))
            {
                object typed;
                if (TryReadGraphValue(port, graphType, out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else
            {
                string typed;
                if (port.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }

            value = null;
            return false;
        }

        public static bool TryReadOptionValue(Unity.GraphToolkit.Editor.INodeOption option, string blueprintType, out object value)
        {
            Type graphType = ToGraphType(blueprintType);
            if (graphType == typeof(bool))
            {
                bool typed;
                if (option.TryGetValue(out typed))
                {
                    value = typed;
                    return true;
                }
            }
            else if (graphType == typeof(int))
            {
                int typed;
                if (option.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType == typeof(float))
            {
                float typed;
                if (option.TryGetValue(out typed))
                {
                    value = typed;
                    return true;
                }
            }
            else if (graphType == typeof(Vector2))
            {
                Vector2 typed;
                if (option.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType == typeof(Vector3))
            {
                Vector3 typed;
                if (option.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType == typeof(Vector4))
            {
                Vector4 typed;
                if (option.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType == typeof(Rect))
            {
                Rect typed;
                if (option.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType == typeof(Color))
            {
                Color typed;
                if (option.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (graphType.IsEnum)
            {
                object typed;
                if (TryReadGraphValue(option, graphType, out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (BlueprintArrayUtility.IsArrayType(blueprintType))
            {
                object typed;
                if (TryReadGraphValue(option, graphType, out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (blueprintType == BlueprintGraphToolkitBlueprintTypes.TypeId)
            {
                object typed;
                if (TryReadGraphValue(option, graphType, out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else if (BlueprintVariableTypeRegistry.IsCustomType(blueprintType))
            {
                object typed;
                if (TryReadGraphValue(option, graphType, out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }
            else
            {
                string typed;
                if (option.TryGetValue(out typed))
                {
                    value = ConvertFromGraphField(typed, blueprintType);
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static object ConvertToEnumValue(object value, Type enumType)
        {
            if (value == null)
            {
                return Activator.CreateInstance(enumType);
            }

            if (enumType.IsInstanceOfType(value))
            {
                return value;
            }

            try
            {
                string text = value as string;
                if (!string.IsNullOrEmpty(text) && IsEnumName(text, enumType))
                {
                    return Enum.Parse(enumType, text, false);
                }
            }
            catch
            {
            }

            return Activator.CreateInstance(enumType);
        }

        private static bool IsEnumName(string value, Type enumType)
        {
            if (string.IsNullOrEmpty(value) || enumType == null || !enumType.IsEnum)
            {
                return false;
            }

            string[] names = Enum.GetNames(enumType);
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadGraphValue(object model, Type graphType, out object value)
        {
            value = null;
            if (model == null || graphType == null)
            {
                return false;
            }

            MethodInfo method = FindGenericTryGetValueMethod(model.GetType());
            if (method == null && model is Unity.GraphToolkit.Editor.IPort)
            {
                method = FindGenericTryGetValueMethod(typeof(Unity.GraphToolkit.Editor.IPort));
            }

            if (method == null && model is Unity.GraphToolkit.Editor.INodeOption)
            {
                method = FindGenericTryGetValueMethod(typeof(Unity.GraphToolkit.Editor.INodeOption));
            }

            if (method == null)
            {
                return false;
            }

            object[] arguments = new object[] { null };
            bool success = false;
            try
            {
                success = (bool)method.MakeGenericMethod(graphType).Invoke(model, arguments);
            }
            catch
            {
                return false;
            }

            if (success)
            {
                value = arguments[0];
                return true;
            }

            return false;
        }

        private static MethodInfo FindGenericTryGetValueMethod(Type type)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo candidate = methods[i];
                if (candidate.Name != "TryGetValue" || !candidate.IsGenericMethodDefinition)
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
    }
}
