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

        private static bool TryGetSingle(object source, string key, out object value)
        {
            value = null;
            if (source == null || string.IsNullOrEmpty(key))
            {
                return false;
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
