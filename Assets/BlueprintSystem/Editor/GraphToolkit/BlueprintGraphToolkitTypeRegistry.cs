using System;
using System.Collections.Generic;

namespace BlueprintSystem.Editor
{
    internal static class BlueprintGraphToolkitTypeRegistry
    {
        public static string[] SupportedBlueprintTypes
        {
            get
            {
                List<string> types = new List<string>(BlueprintVariableTypeRegistry.GetSupportedBlueprintTypes());
                types.Add("Array");
                types.Add(BlueprintGraphToolkitDataTableTypes.TypeId);
                return types.ToArray();
            }
        }

        public static Type[] SupportedGraphTypes
        {
            get
            {
                List<Type> types = new List<Type>(BlueprintVariableTypeRegistry.GetSupportedClrTypes());
                types.Remove(typeof(BlueprintStructValue));
                if (BlueprintUserStructRegistry.GetTypeIds().Length > 0)
                {
                    types.Add(typeof(Struct));
                }

                types.Add(typeof(Array));
                types.Add(typeof(Blueprint));
                types.Add(typeof(DataTable));
                return types.ToArray();
            }
        }

        public static bool TryGetGraphType(string blueprintType, out Type graphType)
        {
            if (blueprintType == BlueprintGraphToolkitBlueprintTypes.TypeId)
            {
                graphType = typeof(Blueprint);
                return true;
            }

            if (blueprintType == "Array")
            {
                graphType = typeof(Array);
                return true;
            }

            if (blueprintType == BlueprintGraphToolkitDataTableTypes.TypeId ||
                BlueprintDataTableVariableTypeUtility.IsSupportedType(blueprintType))
            {
                graphType = typeof(DataTable);
                return true;
            }

            if (BlueprintUserStructRegistry.IsUserStructType(blueprintType))
            {
                graphType = typeof(Struct);
                return true;
            }

            string elementType;
            if (BlueprintArrayUtility.TryGetElementType(blueprintType, out elementType) &&
                BlueprintArrayUtility.IsSupportedElementType(elementType))
            {
                graphType = typeof(Array);
                return true;
            }

            return BlueprintVariableTypeRegistry.TryGetClrType(blueprintType, out graphType);
        }

        public static bool TryGetBlueprintType(Type graphType, out string blueprintType)
        {
            if (graphType == typeof(Blueprint))
            {
                blueprintType = BlueprintGraphToolkitBlueprintTypes.TypeId;
                return true;
            }

            if (graphType == typeof(Array))
            {
                blueprintType = BlueprintGraphToolkitArrayTypes.MakeBlueprintType(BlueprintGraphToolkitArrayTypes.DefaultElementType);
                return true;
            }

            if (graphType == typeof(DataTable))
            {
                string[] rowStructTypes = BlueprintGraphToolkitDataTableTypes.SupportedRowStructTypes;
                blueprintType = rowStructTypes.Length == 0
                    ? null
                    : BlueprintDataTableVariableTypeUtility.MakeType(rowStructTypes[0]);
                return !string.IsNullOrEmpty(blueprintType);
            }

            if (graphType == typeof(Struct))
            {
                blueprintType = BlueprintGraphToolkitStructTypes.DefaultTypeId;
                return !string.IsNullOrEmpty(blueprintType);
            }

            Type elementClrType;
            if (BlueprintGraphToolkitArrayTypes.TryGetElementType(graphType, out elementClrType))
            {
                string elementType;
                if (BlueprintVariableTypeRegistry.TryGetBlueprintType(elementClrType, out elementType))
                {
                    blueprintType = MakeArrayType(elementType);
                    return true;
                }
            }

            return BlueprintVariableTypeRegistry.TryGetBlueprintType(graphType, out blueprintType);
        }

        private static string MakeArrayType(string elementType)
        {
            return "Array<" + elementType + ">";
        }
    }
}
