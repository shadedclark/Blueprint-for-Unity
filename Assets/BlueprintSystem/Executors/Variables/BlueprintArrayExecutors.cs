using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class ArrayCountExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.Count"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "count")
            {
                return BlueprintArrayUtility.Count(context.GetInputValue(node, "array"));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayGetExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.Get"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "item")
            {
                return base.Evaluate(context, node, outputPortId);
            }

            object item;
            int index = context.GetInputValue(node, "index", 0);
            return BlueprintArrayUtility.TryGetElement(context.GetInputValue(node, "array"), index, out item) ? item : null;
        }
    }

    public sealed class ArrayForEachLoopExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.ForEachLoop"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            return BlueprintArrayLoopUtility.ExecuteForEach(context, node, false);
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return BlueprintArrayLoopUtility.EvaluateLoopValue(context, node, outputPortId);
        }
    }

    public sealed class ArrayForEachLoopWithBreakExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.ForEachLoopWithBreak"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            if (context.CurrentExecInputPortId == "break")
            {
                BlueprintArrayLoopUtility.RequestBreak(context, node);
                return BlueprintExecResult.Stop();
            }

            return BlueprintArrayLoopUtility.ExecuteForEach(context, node, true);
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return BlueprintArrayLoopUtility.EvaluateLoopValue(context, node, outputPortId);
        }
    }

    public sealed class ArrayIsValidIndexExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.IsValidIndex"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                IList list = BlueprintArrayUtility.ReadList(context.GetInputValue(node, "array"));
                int index = context.GetInputValue(node, "index", 0);
                return list != null && index >= 0 && index < list.Count;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayContainsExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.Contains"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                IList list = BlueprintArrayUtility.ReadList(context.GetInputValue(node, "array"));
                object item = context.GetInputValue(node, "item");
                return BlueprintArrayComparisonUtility.IndexOf(list, item) >= 0;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayIndexOfExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.IndexOf"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            IList list = BlueprintArrayUtility.ReadList(context.GetInputValue(node, "array"));
            object item = context.GetInputValue(node, "item");
            int index = BlueprintArrayComparisonUtility.IndexOf(list, item);

            if (outputPortId == "index")
            {
                return index;
            }

            if (outputPortId == "found")
            {
                return index >= 0;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayFirstExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.First"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            IList list = BlueprintArrayUtility.ReadList(context.GetInputValue(node, "array"));
            bool isValid = list != null && list.Count > 0;

            if (outputPortId == "item")
            {
                return isValid ? list[0] : null;
            }

            if (outputPortId == "isValid")
            {
                return isValid;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayLastExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.Last"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            IList list = BlueprintArrayUtility.ReadList(context.GetInputValue(node, "array"));
            bool isValid = list != null && list.Count > 0;

            if (outputPortId == "item")
            {
                return isValid ? list[list.Count - 1] : null;
            }

            if (outputPortId == "isValid")
            {
                return isValid;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VariableGetFieldExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Variable.GetField"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "value")
            {
                return base.Evaluate(context, node, outputPortId);
            }

            string path = context.GetInputValue(node, "path", string.Empty);
            object value = context.GetInputValue(node, "target");
            if (string.IsNullOrEmpty(path))
            {
                context.Logger.Error("Variable.GetField node '" + node.Id + "' has no field path.");
                return null;
            }

            object result;
            if (BlueprintFieldUtility.TryGetValue(value, path, out result))
            {
                return result;
            }

            context.Logger.Error("Variable.GetField node '" + node.Id + "' could not read path '" + path + "'.");
            return null;
        }
    }

    public sealed class VariableSetFieldExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Variable.SetField"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "result")
            {
                return base.Evaluate(context, node, outputPortId);
            }

            string path = context.GetInputValue(node, "path", string.Empty);
            object target = context.GetInputValue(node, "target");
            object value = context.GetInputValue(node, "value");
            if (string.IsNullOrEmpty(path))
            {
                context.Logger.Error("Variable.SetField node '" + node.Id + "' has no field path.");
                return target;
            }

            object result;
            if (BlueprintFieldUtility.TrySetValue(target, path, value, out result))
            {
                return result;
            }

            context.Logger.Error("Variable.SetField node '" + node.Id + "' could not write path '" + path + "'.");
            return target;
        }
    }

    public sealed class VariableBreakStructExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Variable.BreakStruct"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            string structTypeId;
            CompiledStructLayout layout;
            if (!BlueprintBreakStructNodeUtility.TryResolveLayout(node.Properties, out structTypeId, out layout))
            {
                context.Logger.Error("Variable.BreakStruct node '" + node.Id + "' has unknown struct type '" +
                    BlueprintBreakStructNodeUtility.GetStructTypeId(node.Properties) + "'.");
                return null;
            }

            int fieldIndex;
            if (!layout.TryGetFieldIndexById(outputPortId, out fieldIndex))
            {
                context.Logger.Error("Variable.BreakStruct node '" + node.Id + "' has unknown field output '" + outputPortId + "'.");
                return null;
            }

            object target = context.GetInputValue(node, BlueprintBreakStructNodeUtility.TargetPortId);
            if (target == null)
            {
                context.Logger.Error("Variable.BreakStruct node '" + node.Id + "' has no target value.");
                return null;
            }

            object runtimeValue;
            if (!BlueprintUserStructUtility.TryConvertToRuntimeValue(target, structTypeId, out runtimeValue))
            {
                context.Logger.Error("Variable.BreakStruct node '" + node.Id + "' expected target type '" + structTypeId + "'.");
                return null;
            }

            RuntimeStructRecord structValue = runtimeValue as RuntimeStructRecord;
            object fieldValue;
            if (structValue != null && structValue.TryGetValue(fieldIndex, out fieldValue))
            {
                return fieldValue;
            }

            context.Logger.Error("Variable.BreakStruct node '" + node.Id + "' could not read field '" + outputPortId + "'.");
            return null;
        }
    }

    public static class BlueprintFieldUtility
    {
        public static bool TryGetValue(object source, string path, out object value)
        {
            value = source;
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            string[] parts = path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (string.IsNullOrEmpty(part) || !TryGetSingle(value, part, out value))
                {
                    value = null;
                    return false;
                }
            }

            return true;
        }

        public static bool TrySetValue(object source, string path, object newValue, out object value)
        {
            value = source;
            if (string.IsNullOrEmpty(path))
            {
                value = newValue;
                return true;
            }

            string[] parts = path.Split('.');
            return TrySetPath(source, parts, 0, newValue, out value);
        }

        private static bool TrySetPath(object source, string[] parts, int index, object newValue, out object value)
        {
            value = null;
            if (parts == null || index < 0 || index >= parts.Length)
            {
                return false;
            }

            string part = parts[index];
            if (string.IsNullOrEmpty(part))
            {
                return false;
            }

            if (index == parts.Length - 1)
            {
                return TrySetSingle(source, part, newValue, out value);
            }

            object child;
            if (!TryGetSingle(source, part, out child))
            {
                return false;
            }

            object updatedChild;
            if (!TrySetPath(child, parts, index + 1, newValue, out updatedChild))
            {
                return false;
            }

            return TrySetSingle(source, part, updatedChild, out value);
        }

        private static bool TryGetSingle(object source, string key, out object value)
        {
            value = null;
            if (source == null || string.IsNullOrEmpty(key))
            {
                return false;
            }

            BlueprintStructValue structValue = source as BlueprintStructValue;
            if (structValue != null)
            {
                return structValue.TryGetValue(key, out value);
            }

            IDictionary<string, object> typedDictionary = source as IDictionary<string, object>;
            if (typedDictionary != null)
            {
                return typedDictionary.TryGetValue(key, out value);
            }

            IDictionary dictionary = source as IDictionary;
            if (dictionary != null && dictionary.Contains(key))
            {
                value = dictionary[key];
                return true;
            }

            IList list = source as IList;
            int index;
            if (list != null && int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                if (index < 0 || index >= list.Count)
                {
                    return false;
                }

                value = list[index];
                return true;
            }

            Type type = source.GetType();
            FieldInfo field = type.GetField(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                value = field.GetValue(source);
                return true;
            }

            PropertyInfo property = type.GetProperty(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                value = property.GetValue(source, null);
                return true;
            }

            Vector2 vector2;
            if (TryReadVector2(source, out vector2))
            {
                if (key == "x")
                {
                    value = vector2.x;
                    return true;
                }

                if (key == "y")
                {
                    value = vector2.y;
                    return true;
                }
            }

            Vector3 vector3;
            if (TryReadVector3(source, out vector3))
            {
                if (key == "x")
                {
                    value = vector3.x;
                    return true;
                }

                if (key == "y")
                {
                    value = vector3.y;
                    return true;
                }

                if (key == "z")
                {
                    value = vector3.z;
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetSingle(object source, string key, object newValue, out object value)
        {
            value = null;
            if (source == null || string.IsNullOrEmpty(key))
            {
                return false;
            }

            RuntimeStructRecord structValue = source as RuntimeStructRecord;
            if (structValue != null)
            {
                RuntimeStructRecord updated;
                if (!BlueprintUserStructUtility.TrySetFieldValue(structValue, key, newValue, out updated))
                {
                    return false;
                }

                value = updated;
                return true;
            }

            BlueprintStructValue legacyStructValue = source as BlueprintStructValue;
            if (legacyStructValue != null)
            {
                BlueprintStructValue updated = legacyStructValue.WithValue(key, newValue);
                return updated != null &&
                       BlueprintUserStructUtility.TryConvertToRuntimeValue(updated, updated.TypeId, out value);
            }

            IDictionary<string, object> typedDictionary = source as IDictionary<string, object>;
            if (typedDictionary != null)
            {
                if (!typedDictionary.ContainsKey(key))
                {
                    return false;
                }

                Dictionary<string, object> copy = new Dictionary<string, object>(typedDictionary, StringComparer.Ordinal);
                copy[key] = newValue;
                value = copy;
                return true;
            }

            IDictionary dictionary = source as IDictionary;
            if (dictionary != null && dictionary.Contains(key))
            {
                Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary)
                {
                    copy[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value;
                }

                copy[key] = newValue;
                value = copy;
                return true;
            }

            IList list = source as IList;
            int index;
            if (list != null && int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                if (index < 0 || index >= list.Count)
                {
                    return false;
                }

                List<object> copy = new List<object>();
                for (int i = 0; i < list.Count; i++)
                {
                    copy.Add(list[i]);
                }

                copy[index] = newValue;
                value = copy;
                return true;
            }

            Vector2 vector2;
            if (TryReadVector2(source, out vector2))
            {
                float component;
                if (TryReadFloat(newValue, out component))
                {
                    if (key == "x")
                    {
                        value = new Vector2(component, vector2.y);
                        return true;
                    }

                    if (key == "y")
                    {
                        value = new Vector2(vector2.x, component);
                        return true;
                    }
                }
            }

            Vector3 vector3;
            if (TryReadVector3(source, out vector3))
            {
                float component;
                if (TryReadFloat(newValue, out component))
                {
                    if (key == "x")
                    {
                        value = new Vector3(component, vector3.y, vector3.z);
                        return true;
                    }

                    if (key == "y")
                    {
                        value = new Vector3(vector3.x, component, vector3.z);
                        return true;
                    }

                    if (key == "z")
                    {
                        value = new Vector3(vector3.x, vector3.y, component);
                        return true;
                    }
                }
            }

            Type type = source.GetType();
            FieldInfo field = type.GetField(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && !field.IsInitOnly)
            {
                object converted = ConvertForMember(newValue, field.FieldType, field.GetValue(source));
                object target = source;
                field.SetValue(target, converted);
                value = target;
                return true;
            }

            PropertyInfo property = type.GetProperty(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                object converted = ConvertForMember(newValue, property.PropertyType, property.GetValue(source, null));
                object target = source;
                property.SetValue(target, converted, null);
                value = target;
                return true;
            }

            return false;
        }

        private static bool TryReadVector2(object source, out Vector2 value)
        {
            if (source is Vector2)
            {
                value = (Vector2)source;
                return true;
            }

            value = Vector2.zero;
            return false;
        }

        private static bool TryReadVector3(object source, out Vector3 value)
        {
            if (source is Vector3)
            {
                value = (Vector3)source;
                return true;
            }

            value = Vector3.zero;
            return false;
        }

        private static object ConvertForMember(object value, Type memberType, object defaultValue)
        {
            if (memberType == null || value == null)
            {
                return value;
            }

            if (memberType.IsInstanceOfType(value))
            {
                return value;
            }

            if (memberType == typeof(Vector2))
            {
                return BlueprintTypeUtility.ToVector2(value, defaultValue is Vector2 ? (Vector2)defaultValue : Vector2.zero);
            }

            if (memberType == typeof(Vector3))
            {
                return BlueprintTypeUtility.ToVector3(value, defaultValue is Vector3 ? (Vector3)defaultValue : Vector3.zero);
            }

            return BlueprintTypeUtility.ConvertValue(value, memberType, defaultValue);
        }

        private static bool TryReadFloat(object value, out float result)
        {
            result = 0f;
            if (value == null)
            {
                return false;
            }

            try
            {
                result = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class BlueprintArrayLoopUtility
    {
        private static string ActiveKey(RuntimeNode node)
        {
            return "arrayLoopActive:" + node.Id;
        }

        private static string BreakKey(RuntimeNode node)
        {
            return "arrayLoopBreak:" + node.Id;
        }

        public static BlueprintExecResult ExecuteForEach(BlueprintExecutionContext context, RuntimeNode node, bool supportsBreak)
        {
            IList list = BlueprintArrayUtility.ReadList(context.GetInputValue(node, "array"));
            context.ClearLoopValues(node);
            context.SetState(ActiveKey(node), true);
            context.SetState(BreakKey(node), false);

            try
            {
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        context.SetLoopValue(node, "arrayElement", list[i]);
                        context.SetLoopValue(node, "arrayIndex", i);
                        context.ExecuteFromOutput(node, "loopBody");

                        if (supportsBreak && IsBreakRequested(context, node))
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                context.ClearLoopValues(node);
                context.RemoveState(ActiveKey(node));
                context.RemoveState(BreakKey(node));
            }

            return BlueprintExecResult.Continue("completed");
        }

        public static void RequestBreak(BlueprintExecutionContext context, RuntimeNode node)
        {
            if (context.HasState(ActiveKey(node)))
            {
                context.SetState(BreakKey(node), true);
            }
        }

        public static object EvaluateLoopValue(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            object value;
            if (outputPortId == "arrayElement")
            {
                return context.TryGetLoopValue(node, outputPortId, out value) ? value : null;
            }

            if (outputPortId == "arrayIndex")
            {
                return context.TryGetLoopValue(node, outputPortId, out value) ? value : -1;
            }

            return null;
        }

        private static bool IsBreakRequested(BlueprintExecutionContext context, RuntimeNode node)
        {
            object value;
            if (!context.TryGetState(BreakKey(node), out value) || !(value is bool))
            {
                return false;
            }

            return (bool)value;
        }
    }

    internal static class BlueprintArrayComparisonUtility
    {
        public static int IndexOf(IList list, object item)
        {
            if (list == null)
            {
                return -1;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (AreEqual(list[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool AreEqual(object left, object right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            if (IsStructuredValue(left) || IsStructuredValue(right))
            {
                return object.ReferenceEquals(left, right);
            }

            double leftNumber;
            double rightNumber;
            if (TryReadNumber(left, out leftNumber) && TryReadNumber(right, out rightNumber))
            {
                return leftNumber == rightNumber;
            }

            if (left.GetType().IsEnum || right.GetType().IsEnum)
            {
                return left.ToString() == right.ToString();
            }

            if (left is string || right is string)
            {
                return Convert.ToString(left, CultureInfo.InvariantCulture) == Convert.ToString(right, CultureInfo.InvariantCulture);
            }

            return object.Equals(left, right);
        }

        private static bool IsStructuredValue(object value)
        {
            if (value == null)
            {
                return false;
            }

            if (value is IDictionary)
            {
                return true;
            }

            Type type = value.GetType();
            if (type.IsEnum ||
                type == typeof(string) ||
                type == typeof(bool) ||
                type == typeof(byte) ||
                type == typeof(sbyte) ||
                type == typeof(short) ||
                type == typeof(ushort) ||
                type == typeof(int) ||
                type == typeof(uint) ||
                type == typeof(long) ||
                type == typeof(ulong) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(decimal) ||
                type == typeof(Vector2) ||
                type == typeof(Vector3) ||
                type == typeof(Vector4) ||
                type == typeof(Rect) ||
                type == typeof(Color))
            {
                return false;
            }

            return true;
        }

        private static bool TryReadNumber(object value, out double result)
        {
            result = 0d;
            if (value == null || value is bool || value is string)
            {
                return false;
            }

            if (value is byte ||
                value is sbyte ||
                value is short ||
                value is ushort ||
                value is int ||
                value is uint ||
                value is long ||
                value is ulong ||
                value is float ||
                value is double ||
                value is decimal)
            {
                result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            }

            return false;
        }
    }
}
