using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VehicleRoads;

namespace BlueprintSystem
{
    public enum ComparisonMode
    {
        Equals,
        NotEquals,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual
    }

    public enum TickPhase
    {
        Update,
        FixedUpdate,
        LateUpdate
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class BlueprintVariableTypeAttribute : Attribute
    {
        public BlueprintVariableTypeAttribute(string typeId)
        {
            TypeId = typeId;
        }

        public new string TypeId { get; private set; }
    }

    public static class BlueprintVariableTypeRegistry
    {
        public const string BlueprintAssetTypeId = "Blueprint";
        public const string BlueprintRefTypeId = "BlueprintRef";

        private static readonly Dictionary<string, Type> BuiltinTypesById = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            { "string", typeof(string) },
            { "bool", typeof(bool) },
            { "int", typeof(int) },
            { "float", typeof(float) },
            { "Vector2", typeof(Vector2) },
            { "Vector3", typeof(Vector3) },
            { "Vector4", typeof(Vector4) },
            { "Color", typeof(Color) },
            { "Rect", typeof(Rect) },
            { "ForceMode", typeof(ForceMode) },
            { "ForceMode2D", typeof(ForceMode2D) },
            { "LoadSceneMode", typeof(LoadSceneMode) },
            { "Key", typeof(Key) },
            { "ComparisonMode", typeof(ComparisonMode) },
            { "TickPhase", typeof(TickPhase) },
            { "BlueprintResourceScope", typeof(BlueprintResourceScope) },
            { "BlueprintResourceLoadState", typeof(BlueprintResourceLoadState) },
            { "RoadAgentMask", typeof(RoadAgentMask) },
            { "RoadLaneAdjacentSide", typeof(RoadLaneAdjacentSide) },
            { "RoadElementKind", typeof(RoadElementKind) },
            { "RoadAgentState", typeof(RoadAgentState) },
            { "RoadRouteState", typeof(RoadRouteState) },
            { "RoadQueryFailureReason", typeof(RoadQueryFailureReason) },
            { "VehicleRoadStopReason", typeof(VehicleRoadStopReason) },
            { "VehicleRoadPassageStatus", typeof(VehicleRoadPassageStatus) },
            { "VehicleRoadSignalState", typeof(VehicleRoadSignalState) },
            { "VehicleRoadLaneChangeStatus", typeof(VehicleRoadLaneChangeStatus) },
            { "VehicleLaneRecoveryMode", typeof(VehicleLaneRecoveryMode) }
        };

        private static readonly Dictionary<Type, string> BuiltinIdsByType = new Dictionary<Type, string>
        {
            { typeof(string), "string" },
            { typeof(bool), "bool" },
            { typeof(int), "int" },
            { typeof(float), "float" },
            { typeof(Vector2), "Vector2" },
            { typeof(Vector3), "Vector3" },
            { typeof(Vector4), "Vector4" },
            { typeof(Color), "Color" },
            { typeof(Rect), "Rect" },
            { typeof(ForceMode), "ForceMode" },
            { typeof(ForceMode2D), "ForceMode2D" },
            { typeof(LoadSceneMode), "LoadSceneMode" },
            { typeof(Key), "Key" },
            { typeof(ComparisonMode), "ComparisonMode" },
            { typeof(TickPhase), "TickPhase" },
            { typeof(BlueprintResourceScope), "BlueprintResourceScope" },
            { typeof(BlueprintResourceLoadState), "BlueprintResourceLoadState" },
            { typeof(RoadAgentMask), "RoadAgentMask" },
            { typeof(RoadLaneAdjacentSide), "RoadLaneAdjacentSide" },
            { typeof(RoadElementKind), "RoadElementKind" },
            { typeof(RoadAgentState), "RoadAgentState" },
            { typeof(RoadRouteState), "RoadRouteState" },
            { typeof(RoadQueryFailureReason), "RoadQueryFailureReason" },
            { typeof(VehicleRoadStopReason), "VehicleRoadStopReason" },
            { typeof(VehicleRoadPassageStatus), "VehicleRoadPassageStatus" },
            { typeof(VehicleRoadSignalState), "VehicleRoadSignalState" },
            { typeof(VehicleRoadLaneChangeStatus), "VehicleRoadLaneChangeStatus" },
            { typeof(VehicleLaneRecoveryMode), "VehicleLaneRecoveryMode" }
        };

        private static readonly object CacheLock = new object();
        private static Dictionary<string, Type> customTypesById;
        private static Dictionary<Type, string> customIdsByType;

        public static bool TryGetClrType(string typeId, out Type clrType)
        {
            clrType = null;
            if (BlueprintDataTableVariableTypeUtility.IsSupportedType(typeId))
            {
                clrType = typeof(string);
                return true;
            }

            if (typeId == BlueprintAssetTypeId)
            {
                clrType = typeof(string);
                return true;
            }

            if (typeId == BlueprintRefTypeId)
            {
                clrType = typeof(BlueprintRef);
                return true;
            }

            if (!string.IsNullOrEmpty(typeId) && BuiltinTypesById.TryGetValue(typeId, out clrType))
            {
                return true;
            }

            string arrayElementType;
            if (BlueprintArrayUtility.TryGetElementType(typeId, out arrayElementType) &&
                BlueprintArrayUtility.IsSupportedElementType(arrayElementType))
            {
                clrType = typeof(string);
                return true;
            }

            if (BlueprintUserStructRegistry.IsUserStructType(typeId))
            {
                clrType = typeof(BlueprintStructValue);
                return true;
            }

            EnsureCustomTypes();
            return !string.IsNullOrEmpty(typeId) && customTypesById.TryGetValue(typeId, out clrType);
        }

        public static bool TryGetBlueprintType(Type clrType, out string typeId)
        {
            typeId = null;
            if (clrType != null && BuiltinIdsByType.TryGetValue(clrType, out typeId))
            {
                return true;
            }

            EnsureCustomTypes();
            return clrType != null && customIdsByType.TryGetValue(clrType, out typeId);
        }

        public static bool IsKnownType(string typeId)
        {
            string arrayElementType;
            if (BlueprintArrayUtility.TryGetElementType(typeId, out arrayElementType))
            {
                return BlueprintArrayUtility.IsSupportedElementType(arrayElementType);
            }

            Type ignored;
            return TryGetClrType(typeId, out ignored);
        }

        public static bool IsBuiltInType(string typeId)
        {
            return typeId == BlueprintAssetTypeId ||
                   typeId == BlueprintRefTypeId ||
                   BlueprintDataTableVariableTypeUtility.IsDataTableType(typeId) ||
                   !string.IsNullOrEmpty(typeId) && BuiltinTypesById.ContainsKey(typeId);
        }

        public static bool IsBuiltInType(Type type)
        {
            return type == typeof(BlueprintRef) ||
                   type != null && BuiltinIdsByType.ContainsKey(type);
        }

        public static bool IsCustomType(string typeId)
        {
            if (string.IsNullOrEmpty(typeId) || IsBuiltInType(typeId))
            {
                return false;
            }

            if (BlueprintUserStructRegistry.IsUserStructType(typeId))
            {
                return true;
            }

            EnsureCustomTypes();
            return customTypesById.ContainsKey(typeId);
        }

        public static Type[] GetSupportedClrTypes()
        {
            EnsureCustomTypes();
            List<Type> types = new List<Type>();
            foreach (Type type in BuiltinTypesById.Values)
            {
                types.Add(type);
            }

            if (BlueprintUserStructRegistry.GetTypeIds().Length > 0)
            {
                types.Add(typeof(BlueprintStructValue));
            }

            foreach (Type type in customTypesById.Values)
            {
                types.Add(type);
            }

            return types.ToArray();
        }

        public static string[] GetSupportedBlueprintTypes()
        {
            EnsureCustomTypes();
            List<string> types = new List<string>();
            foreach (string typeId in BuiltinTypesById.Keys)
            {
                types.Add(typeId);
            }

            types.Add(BlueprintAssetTypeId);

            string[] userStructTypeIds = BlueprintUserStructRegistry.GetTypeIds();
            for (int i = 0; i < userStructTypeIds.Length; i++)
            {
                types.Add(userStructTypeIds[i]);
            }

            foreach (string typeId in customTypesById.Keys)
            {
                types.Add(typeId);
            }

            return types.ToArray();
        }

        public static void Refresh()
        {
            lock (CacheLock)
            {
                customTypesById = null;
                customIdsByType = null;
            }
        }

        private static void EnsureCustomTypes()
        {
            if (customTypesById != null && customIdsByType != null)
            {
                return;
            }

            lock (CacheLock)
            {
                if (customTypesById != null && customIdsByType != null)
                {
                    return;
                }

                Dictionary<string, Type> byId = new Dictionary<string, Type>(StringComparer.Ordinal);
                Dictionary<Type, string> byType = new Dictionary<Type, string>();
                List<Type> candidates = new List<Type>();
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Type[] assemblyTypes = GetLoadableTypes(assemblies[i]);
                    for (int t = 0; t < assemblyTypes.Length; t++)
                    {
                        Type type = assemblyTypes[t];
                        if (type != null && IsValidCustomVariableType(type))
                        {
                            candidates.Add(type);
                        }
                    }
                }

                candidates.Sort((left, right) => string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
                for (int i = 0; i < candidates.Count; i++)
                {
                    Type type = candidates[i];
                    BlueprintVariableTypeAttribute attribute = GetVariableTypeAttribute(type);
                    if (attribute == null || string.IsNullOrEmpty(attribute.TypeId) ||
                        BuiltinTypesById.ContainsKey(attribute.TypeId) ||
                        attribute.TypeId == BlueprintAssetTypeId ||
                        attribute.TypeId == BlueprintRefTypeId)
                    {
                        continue;
                    }

                    if (byId.ContainsKey(attribute.TypeId) || byType.ContainsKey(type))
                    {
                        continue;
                    }

                    byId.Add(attribute.TypeId, type);
                    byType.Add(type, attribute.TypeId);
                }

                customTypesById = byId;
                customIdsByType = byType;
            }
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            if (assembly == null)
            {
                return new Type[0];
            }

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types ?? new Type[0];
            }
            catch
            {
                return new Type[0];
            }
        }

        private static bool IsValidCustomVariableType(Type type)
        {
            if (type == null || type.IsAbstract || type.IsGenericTypeDefinition || typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return false;
            }

            if (!type.IsClass && !type.IsValueType)
            {
                return false;
            }

            return GetVariableTypeAttribute(type) != null && type.IsSerializable;
        }

        private static BlueprintVariableTypeAttribute GetVariableTypeAttribute(Type type)
        {
            object[] attributes = type.GetCustomAttributes(typeof(BlueprintVariableTypeAttribute), false);
            return attributes == null || attributes.Length == 0 ? null : attributes[0] as BlueprintVariableTypeAttribute;
        }
    }

    public static class BlueprintArrayUtility
    {
        public static bool TryGetElementType(string blueprintType, out string elementType)
        {
            elementType = null;
            if (string.IsNullOrEmpty(blueprintType) ||
                !blueprintType.StartsWith("Array<", StringComparison.Ordinal) ||
                !blueprintType.EndsWith(">", StringComparison.Ordinal))
            {
                return false;
            }

            elementType = blueprintType.Substring(6, blueprintType.Length - 7).Trim();
            return !string.IsNullOrEmpty(elementType) && !IsArrayType(elementType);
        }

        public static bool IsArrayType(string blueprintType)
        {
            string ignored;
            return TryGetElementType(blueprintType, out ignored);
        }

        public static bool IsSupportedElementType(string elementType)
        {
            if (string.IsNullOrEmpty(elementType) || IsArrayType(elementType) ||
                BlueprintDataTableVariableTypeUtility.IsDataTableType(elementType) ||
                elementType.StartsWith("Binding<", StringComparison.Ordinal))
            {
                return false;
            }

            Type clrType;
            if (!BlueprintVariableTypeRegistry.TryGetClrType(elementType, out clrType))
            {
                return false;
            }

            return clrType == typeof(string) ||
                   clrType == typeof(bool) ||
                   clrType == typeof(int) ||
                   clrType == typeof(float) ||
                   clrType == typeof(Vector2) ||
                   clrType == typeof(Vector3) ||
                   clrType == typeof(Vector4) ||
                   clrType == typeof(Rect) ||
                   clrType == typeof(Color) ||
                   clrType.IsEnum ||
                   BlueprintVariableTypeRegistry.IsCustomType(elementType);
        }

        public static bool IsValueAssignableToArrayType(object value, string blueprintType)
        {
            string elementType;
            if (!TryGetElementType(blueprintType, out elementType) || !IsSupportedElementType(elementType))
            {
                return false;
            }

            IList list = ReadList(value);
            if (list == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (!BlueprintTypeUtility.IsValueAssignableToType(list[i], elementType))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryConvertToRuntimeArray(object value, string blueprintType, out object runtimeValue)
        {
            runtimeValue = null;
            string elementType;
            if (!TryGetElementType(blueprintType, out elementType) || !IsSupportedElementType(elementType))
            {
                return false;
            }

            IList list = ReadList(value);
            if (list == null)
            {
                return false;
            }

            List<object> result = new List<object>();
            for (int i = 0; i < list.Count; i++)
            {
                object element;
                if (!TryConvertElementToRuntimeValue(list[i], elementType, out element))
                {
                    return false;
                }

                result.Add(element);
            }

            runtimeValue = result;
            return true;
        }

        public static bool TryConvertToJsonArray(object value, string blueprintType, out object jsonValue)
        {
            jsonValue = null;
            string elementType;
            if (!TryGetElementType(blueprintType, out elementType) || !IsSupportedElementType(elementType))
            {
                return false;
            }

            IList list = ReadList(value);
            if (list == null)
            {
                return false;
            }

            List<object> result = new List<object>();
            for (int i = 0; i < list.Count; i++)
            {
                object element;
                if (!TryConvertElementToJsonValue(list[i], elementType, out element))
                {
                    return false;
                }

                result.Add(element);
            }

            jsonValue = result;
            return true;
        }

        public static IList ReadList(object value)
        {
            if (value == null)
            {
                return new List<object>();
            }

            string text = value as string;
            if (text != null)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return new List<object>();
                }

                try
                {
                    return BlueprintJson.Deserialize(text) as IList;
                }
                catch (BlueprintJsonException)
                {
                    return null;
                }
            }

            return value as IList;
        }

        public static int Count(object value)
        {
            IList list = ReadList(value);
            return list == null ? 0 : list.Count;
        }

        public static bool TryGetElement(object value, int index, out object element)
        {
            element = null;
            IList list = ReadList(value);
            if (list == null || index < 0 || index >= list.Count)
            {
                return false;
            }

            element = list[index];
            return true;
        }

        private static bool TryConvertElementToRuntimeValue(object value, string elementType, out object runtimeValue)
        {
            runtimeValue = value;
            if (value == null)
            {
                return true;
            }

            switch (elementType)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    runtimeValue = BlueprintTypeUtility.ConvertValue(value, typeof(string), null);
                    return runtimeValue != null;
                case "bool":
                    runtimeValue = BlueprintTypeUtility.ConvertValue(value, typeof(bool), false);
                    return true;
                case "int":
                    runtimeValue = BlueprintTypeUtility.ConvertValue(value, typeof(int), 0);
                    return true;
                case "float":
                    runtimeValue = BlueprintTypeUtility.ConvertValue(value, typeof(float), 0f);
                    return true;
                case "Vector2":
                    runtimeValue = value is Vector2 ? value : BlueprintTypeUtility.ToVector2(value, Vector2.zero);
                    return value is Vector2 || IsListLength(value, 2);
                case "Vector3":
                    runtimeValue = value is Vector3 ? value : BlueprintTypeUtility.ToVector3(value, Vector3.zero);
                    return value is Vector3 || IsListLength(value, 3);
                case "Vector4":
                    runtimeValue = value is Vector4 ? value : BlueprintTypeUtility.ToVector4(value, Vector4.zero);
                    return value is Vector4 || IsListLength(value, 4);
                case "Rect":
                    runtimeValue = value is Rect ? value : BlueprintTypeUtility.ToRect(value, Rect.zero);
                    return value is Rect || IsListLength(value, 4);
                case "Color":
                    runtimeValue = value is Color ? value : ToColor(value, Color.white);
                    return value is Color || IsListLength(value, 3) || IsListLength(value, 4);
                default:
                    Type clrType;
                    if (BlueprintVariableTypeRegistry.TryGetClrType(elementType, out clrType) && clrType.IsEnum)
                    {
                        runtimeValue = BlueprintTypeUtility.ConvertValue(value, clrType, null);
                        return runtimeValue != null;
                    }

                    if (BlueprintVariableTypeRegistry.IsCustomType(elementType))
                    {
                        return BlueprintStructuredValueUtility.TryConvertToRuntimeValue(value, elementType, out runtimeValue);
                    }

                    return false;
            }
        }

        private static bool TryConvertElementToJsonValue(object value, string elementType, out object jsonValue)
        {
            jsonValue = value;
            if (value == null)
            {
                return true;
            }

            if (BlueprintVariableTypeRegistry.IsCustomType(elementType))
            {
                return BlueprintStructuredValueUtility.TryConvertToJsonValue(value, elementType, out jsonValue);
            }

            if (value.GetType().IsEnum)
            {
                jsonValue = value.ToString();
                return true;
            }

            if (value is Vector2)
            {
                Vector2 vector = (Vector2)value;
                jsonValue = new List<object> { vector.x, vector.y };
                return true;
            }

            if (value is Vector3)
            {
                Vector3 vector = (Vector3)value;
                jsonValue = new List<object> { vector.x, vector.y, vector.z };
                return true;
            }

            if (value is Vector4)
            {
                Vector4 vector = (Vector4)value;
                jsonValue = new List<object> { vector.x, vector.y, vector.z, vector.w };
                return true;
            }

            if (value is Rect)
            {
                Rect rect = (Rect)value;
                jsonValue = new List<object> { rect.x, rect.y, rect.width, rect.height };
                return true;
            }

            if (value is Color)
            {
                Color color = (Color)value;
                jsonValue = new List<object> { color.r, color.g, color.b, color.a };
                return true;
            }

            return BlueprintTypeUtility.IsValueAssignableToType(value, elementType);
        }

        private static Color ToColor(object value, Color defaultValue)
        {
            IList list = value as IList;
            if (list == null || list.Count < 3)
            {
                return defaultValue;
            }

            float r = Convert.ToSingle(list[0], CultureInfo.InvariantCulture);
            float g = Convert.ToSingle(list[1], CultureInfo.InvariantCulture);
            float b = Convert.ToSingle(list[2], CultureInfo.InvariantCulture);
            float a = list.Count >= 4 ? Convert.ToSingle(list[3], CultureInfo.InvariantCulture) : defaultValue.a;
            return new Color(r, g, b, a);
        }

        private static bool IsListLength(object value, int length)
        {
            IList list = value as IList;
            return list != null && list.Count == length;
        }
    }

    public static class BlueprintStructuredValueUtility
    {
        private const int MaxDepth = 16;
        private static readonly object FieldCacheLock = new object();
        private static readonly Dictionary<Type, FieldInfo[]> SerializableFieldsByType = new Dictionary<Type, FieldInfo[]>();

        public static bool TryConvertToRuntimeValue(object value, string blueprintType, out object runtimeValue)
        {
            runtimeValue = value;
            if (BlueprintUserStructRegistry.IsUserStructType(blueprintType))
            {
                return BlueprintUserStructUtility.TryConvertToRuntimeValue(value, blueprintType, out runtimeValue);
            }

            Type clrType;
            if (!BlueprintVariableTypeRegistry.TryGetClrType(blueprintType, out clrType) || BlueprintVariableTypeRegistry.IsBuiltInType(clrType))
            {
                return false;
            }

            if (value == null)
            {
                runtimeValue = clrType.IsValueType ? Activator.CreateInstance(clrType) : null;
                return true;
            }

            if (clrType.IsInstanceOfType(value))
            {
                runtimeValue = value;
                return true;
            }

            IDictionary<string, object> dictionary = value as IDictionary<string, object>;
            if (dictionary == null)
            {
                IDictionary genericDictionary = value as IDictionary;
                if (genericDictionary != null)
                {
                    dictionary = NormalizeDictionary(genericDictionary);
                }
            }

            if (dictionary == null)
            {
                runtimeValue = null;
                return false;
            }

            return TryDictionaryToObject(dictionary, clrType, out runtimeValue, 0);
        }

        public static bool TryConvertToJsonValue(object value, string blueprintType, out object jsonValue)
        {
            jsonValue = value;
            if (BlueprintUserStructRegistry.IsUserStructType(blueprintType))
            {
                return BlueprintUserStructUtility.TryConvertToJsonValue(value, blueprintType, out jsonValue);
            }

            Type clrType;
            if (!BlueprintVariableTypeRegistry.TryGetClrType(blueprintType, out clrType) || BlueprintVariableTypeRegistry.IsBuiltInType(clrType))
            {
                return false;
            }

            if (value == null)
            {
                jsonValue = null;
                return true;
            }

            object runtimeValue;
            if (!TryConvertToRuntimeValue(value, blueprintType, out runtimeValue))
            {
                jsonValue = null;
                return false;
            }

            return TryObjectToDictionary(runtimeValue, clrType, out jsonValue, 0);
        }

        public static bool IsValueAssignableToStructuredType(object value, string blueprintType)
        {
            if (BlueprintUserStructRegistry.IsUserStructType(blueprintType))
            {
                object userStructRuntimeValue;
                return BlueprintUserStructUtility.TryConvertToRuntimeValue(value, blueprintType, out userStructRuntimeValue);
            }

            Type ignored;
            if (!BlueprintVariableTypeRegistry.TryGetClrType(blueprintType, out ignored) || BlueprintVariableTypeRegistry.IsBuiltInType(ignored))
            {
                return false;
            }

            object runtimeValue;
            return TryConvertToRuntimeValue(value, blueprintType, out runtimeValue);
        }

        public static bool TryCreateDefaultRuntimeValue(string blueprintType, out object value)
        {
            value = null;
            if (BlueprintUserStructRegistry.IsUserStructType(blueprintType))
            {
                return BlueprintUserStructUtility.TryCreateDefaultRuntimeValue(blueprintType, out value);
            }

            Type clrType;
            if (!BlueprintVariableTypeRegistry.TryGetClrType(blueprintType, out clrType) || BlueprintVariableTypeRegistry.IsBuiltInType(clrType))
            {
                return false;
            }

            try
            {
                value = Activator.CreateInstance(clrType);
                return true;
            }
            catch
            {
                value = null;
                return !clrType.IsValueType;
            }
        }

        public static bool TryCreateDefaultJsonValue(string blueprintType, out object value)
        {
            value = null;
            if (BlueprintUserStructRegistry.IsUserStructType(blueprintType))
            {
                return BlueprintUserStructUtility.TryCreateDefaultJsonValue(blueprintType, out value);
            }

            object runtimeValue;
            if (!TryCreateDefaultRuntimeValue(blueprintType, out runtimeValue))
            {
                return false;
            }

            return TryConvertToJsonValue(runtimeValue, blueprintType, out value);
        }

        private static bool TryDictionaryToObject(IDictionary<string, object> dictionary, Type targetType, out object value, int depth)
        {
            value = null;
            if (dictionary == null || depth > MaxDepth)
            {
                return false;
            }

            FieldInfo[] fields = GetSerializableFields(targetType);
            Dictionary<string, FieldInfo> fieldsByName = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            for (int i = 0; i < fields.Length; i++)
            {
                fieldsByName[fields[i].Name] = fields[i];
            }

            foreach (string key in dictionary.Keys)
            {
                if (!fieldsByName.ContainsKey(key))
                {
                    return false;
                }
            }

            object instance;
            try
            {
                instance = Activator.CreateInstance(targetType);
            }
            catch
            {
                return false;
            }

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object jsonFieldValue;
                if (!dictionary.TryGetValue(field.Name, out jsonFieldValue))
                {
                    continue;
                }

                object typedFieldValue;
                if (!TryJsonToFieldValue(jsonFieldValue, field.FieldType, out typedFieldValue, depth + 1))
                {
                    return false;
                }

                field.SetValue(instance, typedFieldValue);
            }

            value = instance;
            return true;
        }

        private static bool TryObjectToDictionary(object value, Type sourceType, out object jsonValue, int depth)
        {
            jsonValue = null;
            if (depth > MaxDepth)
            {
                return false;
            }

            if (value == null)
            {
                return true;
            }

            if (!sourceType.IsInstanceOfType(value))
            {
                return false;
            }

            Dictionary<string, object> dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
            FieldInfo[] fields = GetSerializableFields(sourceType);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object fieldJsonValue;
                if (!TryFieldValueToJson(field.GetValue(value), field.FieldType, out fieldJsonValue, depth + 1))
                {
                    return false;
                }

                dictionary[field.Name] = fieldJsonValue;
            }

            jsonValue = dictionary;
            return true;
        }

        private static bool TryJsonToFieldValue(object value, Type fieldType, out object fieldValue, int depth)
        {
            fieldValue = null;
            if (depth > MaxDepth)
            {
                return false;
            }

            if (value == null)
            {
                if (!fieldType.IsValueType || Nullable.GetUnderlyingType(fieldType) != null)
                {
                    fieldValue = null;
                    return true;
                }

                return false;
            }

            if (fieldType == typeof(string))
            {
                if (!(value is string))
                {
                    return false;
                }

                fieldValue = value;
                return true;
            }

            if (fieldType == typeof(bool))
            {
                if (!(value is bool))
                {
                    return false;
                }

                fieldValue = value;
                return true;
            }

            if (fieldType.IsEnum)
            {
                return TryJsonToEnum(value, fieldType, out fieldValue);
            }

            if (IsIntegerFieldType(fieldType))
            {
                if (!IsInteger(value))
                {
                    return false;
                }

                fieldValue = Convert.ChangeType(value, fieldType, CultureInfo.InvariantCulture);
                return true;
            }

            if (IsFloatFieldType(fieldType))
            {
                if (!IsNumber(value))
                {
                    return false;
                }

                fieldValue = Convert.ChangeType(value, fieldType, CultureInfo.InvariantCulture);
                return true;
            }

            if (fieldType == typeof(Vector2))
            {
                fieldValue = BlueprintTypeUtility.ToVector2(value, Vector2.zero);
                return IsListLength(value, 2);
            }

            if (fieldType == typeof(Vector3))
            {
                fieldValue = BlueprintTypeUtility.ToVector3(value, Vector3.zero);
                return IsListLength(value, 3);
            }

            if (fieldType == typeof(Vector4))
            {
                fieldValue = BlueprintTypeUtility.ToVector4(value, Vector4.zero);
                return IsListLength(value, 4);
            }

            if (fieldType == typeof(Rect))
            {
                fieldValue = BlueprintTypeUtility.ToRect(value, Rect.zero);
                return IsListLength(value, 4);
            }

            if (fieldType == typeof(Color))
            {
                IList colorList = value as IList;
                if (colorList == null || (colorList.Count != 3 && colorList.Count != 4))
                {
                    return false;
                }

                float r = Convert.ToSingle(colorList[0], CultureInfo.InvariantCulture);
                float g = Convert.ToSingle(colorList[1], CultureInfo.InvariantCulture);
                float b = Convert.ToSingle(colorList[2], CultureInfo.InvariantCulture);
                float a = colorList.Count == 4 ? Convert.ToSingle(colorList[3], CultureInfo.InvariantCulture) : 1f;
                fieldValue = new Color(r, g, b, a);
                return true;
            }

            string nestedBlueprintType;
            if (BlueprintVariableTypeRegistry.TryGetBlueprintType(fieldType, out nestedBlueprintType) &&
                BlueprintVariableTypeRegistry.IsCustomType(nestedBlueprintType))
            {
                object nestedValue;
                if (fieldType.IsInstanceOfType(value))
                {
                    fieldValue = value;
                    return true;
                }

                IDictionary<string, object> dictionary = value as IDictionary<string, object>;
                if (dictionary == null)
                {
                    IDictionary genericDictionary = value as IDictionary;
                    dictionary = genericDictionary == null ? null : NormalizeDictionary(genericDictionary);
                }

                if (dictionary != null && TryDictionaryToObject(dictionary, fieldType, out nestedValue, depth + 1))
                {
                    fieldValue = nestedValue;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFieldValueToJson(object value, Type fieldType, out object jsonValue, int depth)
        {
            jsonValue = null;
            if (depth > MaxDepth)
            {
                return false;
            }

            if (value == null)
            {
                return true;
            }

            if (fieldType == typeof(string) || fieldType == typeof(bool))
            {
                jsonValue = value;
                return true;
            }

            if (fieldType.IsEnum)
            {
                jsonValue = value.ToString();
                return true;
            }

            if (IsIntegerFieldType(fieldType))
            {
                jsonValue = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (IsFloatFieldType(fieldType))
            {
                jsonValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (value is Vector2)
            {
                Vector2 vector = (Vector2)value;
                jsonValue = new List<object> { vector.x, vector.y };
                return true;
            }

            if (value is Vector3)
            {
                Vector3 vector = (Vector3)value;
                jsonValue = new List<object> { vector.x, vector.y, vector.z };
                return true;
            }

            if (value is Vector4)
            {
                Vector4 vector = (Vector4)value;
                jsonValue = new List<object> { vector.x, vector.y, vector.z, vector.w };
                return true;
            }

            if (value is Rect)
            {
                Rect rect = (Rect)value;
                jsonValue = new List<object> { rect.x, rect.y, rect.width, rect.height };
                return true;
            }

            if (value is Color)
            {
                Color color = (Color)value;
                jsonValue = new List<object> { color.r, color.g, color.b, color.a };
                return true;
            }

            string nestedBlueprintType;
            if (BlueprintVariableTypeRegistry.TryGetBlueprintType(fieldType, out nestedBlueprintType) &&
                BlueprintVariableTypeRegistry.IsCustomType(nestedBlueprintType))
            {
                return TryObjectToDictionary(value, fieldType, out jsonValue, depth + 1);
            }

            return false;
        }

        private static bool TryJsonToEnum(object value, Type enumType, out object enumValue)
        {
            enumValue = null;
            try
            {
                string text = value as string;
                if (!string.IsNullOrEmpty(text))
                {
                    enumValue = Enum.Parse(enumType, text, false);
                    return true;
                }

                if (IsInteger(value))
                {
                    enumValue = Enum.ToObject(enumType, value);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static Dictionary<string, object> NormalizeDictionary(IDictionary dictionary)
        {
            Dictionary<string, object> normalized = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                normalized[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value;
            }

            return normalized;
        }

        private static FieldInfo[] GetSerializableFields(Type type)
        {
            lock (FieldCacheLock)
            {
                FieldInfo[] cached;
                if (SerializableFieldsByType.TryGetValue(type, out cached))
                {
                    return cached;
                }
            }

            List<FieldInfo> result = new List<FieldInfo>();
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                FieldInfo[] fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (IsSerializableField(field))
                    {
                        result.Add(field);
                    }
                }
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            FieldInfo[] array = result.ToArray();
            lock (FieldCacheLock)
            {
                SerializableFieldsByType[type] = array;
            }

            return array;
        }

        private static bool IsSerializableField(FieldInfo field)
        {
            if (field == null || field.IsStatic || field.IsNotSerialized || field.IsInitOnly)
            {
                return false;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
            {
                return false;
            }

            if (field.IsPublic)
            {
                return true;
            }

            return field.GetCustomAttributes(typeof(SerializeField), true).Length > 0;
        }

        private static bool IsIntegerFieldType(Type type)
        {
            return type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(long) ||
                   type == typeof(ulong);
        }

        private static bool IsFloatFieldType(Type type)
        {
            return type == typeof(float) ||
                   type == typeof(double);
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
