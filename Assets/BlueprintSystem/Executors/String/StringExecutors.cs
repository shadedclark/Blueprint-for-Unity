using System;
using System.Collections.Generic;
using System.Globalization;

namespace BlueprintSystem
{
    public sealed class StringAppendExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "String.Append"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return ToText(context.GetInputValue(node, "a")) +
                       ToText(context.GetInputValue(node, "b")) +
                       ToText(context.GetInputValue(node, "c")) +
                       ToText(context.GetInputValue(node, "d"));
            }

            return base.Evaluate(context, node, outputPortId);
        }

        private static string ToText(object value)
        {
            return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    public sealed class StringFormatExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "String.Format"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                string format = context.GetInputValue(node, "format", string.Empty);
                object[] args =
                {
                    context.GetInputValue(node, "arg0"),
                    context.GetInputValue(node, "arg1"),
                    context.GetInputValue(node, "arg2"),
                    context.GetInputValue(node, "arg3")
                };

                try
                {
                    return string.Format(CultureInfo.InvariantCulture, format, args);
                }
                catch
                {
                    return format;
                }
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class StringToStringExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "String.ToString"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                object value = context.GetInputValue(node, "value");
                return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class StringContainsExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "String.Contains"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                string value = context.GetInputValue(node, "value", string.Empty);
                string search = context.GetInputValue(node, "search", string.Empty);
                return value.IndexOf(search, StringComparison.Ordinal) >= 0;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class StringStartsWithExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "String.StartsWith"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return context.GetInputValue(node, "value", string.Empty)
                    .StartsWith(context.GetInputValue(node, "prefix", string.Empty), StringComparison.Ordinal);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class StringEndsWithExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "String.EndsWith"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return context.GetInputValue(node, "value", string.Empty)
                    .EndsWith(context.GetInputValue(node, "suffix", string.Empty), StringComparison.Ordinal);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class StringReplaceExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "String.Replace"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                string value = context.GetInputValue(node, "value", string.Empty);
                string oldValue = context.GetInputValue(node, "oldValue", string.Empty);
                if (string.IsNullOrEmpty(oldValue))
                {
                    return value;
                }

                return value.Replace(oldValue, context.GetInputValue(node, "newValue", string.Empty));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class StringSplitExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "String.Split"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "items")
            {
                string value = context.GetInputValue(node, "value", string.Empty);
                string separator = context.GetInputValue(node, "separator", ",");
                string[] parts = string.IsNullOrEmpty(separator)
                    ? new[] { value }
                    : value.Split(new[] { separator }, StringSplitOptions.None);
                List<object> result = new List<object>();
                for (int i = 0; i < parts.Length; i++)
                {
                    result.Add(parts[i]);
                }

                return result;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class StringLengthExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "String.Length"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "length")
            {
                return context.GetInputValue(node, "value", string.Empty).Length;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class StringSubstringExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "String.Substring"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                string value = context.GetInputValue(node, "value", string.Empty);
                int start = Math.Max(0, context.GetInputValue(node, "start", 0));
                int length = context.GetInputValue(node, "length", -1);
                if (start >= value.Length)
                {
                    return string.Empty;
                }

                if (length < 0)
                {
                    return value.Substring(start);
                }

                return value.Substring(start, Math.Min(length, value.Length - start));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class StringEqualIgnoreCaseExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "String.EqualIgnoreCase"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return string.Equals(
                    context.GetInputValue(node, "a", string.Empty),
                    context.GetInputValue(node, "b", string.Empty),
                    StringComparison.OrdinalIgnoreCase);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }
}
