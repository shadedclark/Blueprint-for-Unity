using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace BlueprintSystem
{
    public static class BlueprintTypeUtility
    {
        public static bool IsCompatible(string fromType, string toType)
        {
            if (string.IsNullOrEmpty(toType) || string.IsNullOrEmpty(fromType))
            {
                return true;
            }

            if (fromType == toType)
            {
                return true;
            }

            if ((fromType == BlueprintVariableTypeRegistry.BlueprintAssetTypeId && toType == "string") ||
                (fromType == "string" && toType == BlueprintVariableTypeRegistry.BlueprintAssetTypeId))
            {
                return true;
            }

            if (fromType == BlueprintVariableTypeRegistry.BlueprintRefTypeId &&
                toType == BlueprintVariableTypeRegistry.BlueprintAssetTypeId)
            {
                return true;
            }

            if (BlueprintArrayUtility.IsArrayType(fromType) || BlueprintArrayUtility.IsArrayType(toType))
            {
                return fromType == toType;
            }

            if (fromType == "int" && toType == "float")
            {
                return true;
            }

            if ((fromType == "GameObject" || fromType == "Sprite" || fromType == "Component") && toType == "Object")
            {
                return true;
            }

            if (fromType.StartsWith("Binding<", StringComparison.Ordinal) && toType == fromType)
            {
                return true;
            }

            return false;
        }

        public static bool IsValueAssignableToType(object value, string type)
        {
            if (value == null || string.IsNullOrEmpty(type))
            {
                return true;
            }

            switch (type)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    return value is string;
                case BlueprintVariableTypeRegistry.BlueprintRefTypeId:
                    return value is BlueprintRef;
                case "bool":
                    return value is bool;
                case "int":
                    return IsInteger(value);
                case "float":
                    return IsNumber(value);
                case "Vector2":
                    return value is Vector2 || IsListLength(value, 2);
                case "Vector3":
                    return value is Vector3 || IsListLength(value, 3);
                case "Vector4":
                    return value is Vector4 || IsListLength(value, 4);
                case "Rect":
                    return value is Rect || IsListLength(value, 4);
                case "Color":
                    return value is Color || IsListLength(value, 3) || IsListLength(value, 4);
                default:
                    if (type.StartsWith("Binding<", StringComparison.Ordinal))
                    {
                        return value is string;
                    }

                    if (BlueprintArrayUtility.IsArrayType(type))
                    {
                        return BlueprintArrayUtility.IsValueAssignableToArrayType(value, type);
                    }

                    Type clrType;
                    if (BlueprintVariableTypeRegistry.TryGetClrType(type, out clrType) && clrType.IsEnum)
                    {
                        return IsEnumValueAssignable(value, clrType);
                    }

                    if (BlueprintVariableTypeRegistry.IsCustomType(type))
                    {
                        return BlueprintStructuredValueUtility.IsValueAssignableToStructuredType(value, type);
                    }

                    return false;
            }
        }

        public static T ConvertValue<T>(object value, T defaultValue)
        {
            object converted = ConvertValue(value, typeof(T), defaultValue);
            if (converted == null)
            {
                return defaultValue;
            }

            return (T)converted;
        }

        public static object ConvertValue(object value, Type targetType, object defaultValue)
        {
            if (value == null)
            {
                return defaultValue;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            try
            {
                if (targetType == typeof(string))
                {
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
                }

                if (targetType == typeof(bool))
                {
                    if (value is bool)
                    {
                        return value;
                    }

                    string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                    if (text == "1")
                    {
                        return true;
                    }

                    if (text == "0")
                    {
                        return false;
                    }

                    return bool.Parse(text);
                }

                if (targetType == typeof(int))
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }

                if (targetType == typeof(float))
                {
                    return Convert.ToSingle(value, CultureInfo.InvariantCulture);
                }

                if (targetType.IsEnum)
                {
                    object enumValue;
                    return TryParseEnumName(value as string, targetType, out enumValue) ? enumValue : defaultValue;
                }
            }
            catch
            {
                return defaultValue;
            }

            return defaultValue;
        }

        private static bool IsEnumValueAssignable(object value, Type enumType)
        {
            if (value == null)
            {
                return true;
            }

            if (enumType.IsInstanceOfType(value))
            {
                return true;
            }

            try
            {
                object enumValue;
                if (TryParseEnumName(value as string, enumType, out enumValue))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryParseEnumName(string value, Type enumType, out object enumValue)
        {
            enumValue = null;
            if (string.IsNullOrEmpty(value) || enumType == null || !enumType.IsEnum)
            {
                return false;
            }

            string[] names = Enum.GetNames(enumType);
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == value)
                {
                    enumValue = Enum.Parse(enumType, value, false);
                    return true;
                }
            }

            return false;
        }

        public static Vector2 ToVector2(object value, Vector2 defaultValue)
        {
            IList list = value as IList;
            if (list == null || list.Count < 2)
            {
                return defaultValue;
            }

            return new Vector2(Convert.ToSingle(list[0], CultureInfo.InvariantCulture), Convert.ToSingle(list[1], CultureInfo.InvariantCulture));
        }

        public static Vector3 ToVector3(object value, Vector3 defaultValue)
        {
            IList list = value as IList;
            if (list == null || list.Count < 3)
            {
                return defaultValue;
            }

            return new Vector3(
                Convert.ToSingle(list[0], CultureInfo.InvariantCulture),
                Convert.ToSingle(list[1], CultureInfo.InvariantCulture),
                Convert.ToSingle(list[2], CultureInfo.InvariantCulture));
        }

        public static Vector4 ToVector4(object value, Vector4 defaultValue)
        {
            IList list = value as IList;
            if (list == null || list.Count < 4)
            {
                return defaultValue;
            }

            return new Vector4(
                Convert.ToSingle(list[0], CultureInfo.InvariantCulture),
                Convert.ToSingle(list[1], CultureInfo.InvariantCulture),
                Convert.ToSingle(list[2], CultureInfo.InvariantCulture),
                Convert.ToSingle(list[3], CultureInfo.InvariantCulture));
        }

        public static Rect ToRect(object value, Rect defaultValue)
        {
            IList list = value as IList;
            if (list == null || list.Count < 4)
            {
                return defaultValue;
            }

            return new Rect(
                Convert.ToSingle(list[0], CultureInfo.InvariantCulture),
                Convert.ToSingle(list[1], CultureInfo.InvariantCulture),
                Convert.ToSingle(list[2], CultureInfo.InvariantCulture),
                Convert.ToSingle(list[3], CultureInfo.InvariantCulture));
        }

        private static bool IsInteger(object value)
        {
            return value is byte ||
                   value is sbyte ||
                   value is short ||
                   value is ushort ||
                   value is int ||
                   value is uint ||
                   value is long ||
                   value is ulong;
        }

        private static bool IsNumber(object value)
        {
            return IsInteger(value) ||
                   value is float ||
                   value is double ||
                   value is decimal;
        }

        private static bool IsListLength(object value, int length)
        {
            IList list = value as IList;
            return list != null && list.Count == length;
        }
    }
}
