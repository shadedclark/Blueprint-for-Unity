using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class ArrayMakeExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.Make"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "array")
            {
                int count = Mathf.Clamp(context.GetInputValue(node, "count", 0), 0, 8);
                List<object> result = new List<object>();
                for (int i = 0; i < count; i++)
                {
                    result.Add(context.GetInputValue(node, "item" + i));
                }

                return result;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayAddExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.Add"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            List<object> result = BlueprintArrayMutationUtility.Copy(context.GetInputValue(node, "array"));
            result.Add(context.GetInputValue(node, "item"));

            if (outputPortId == "array")
            {
                return result;
            }

            if (outputPortId == "index")
            {
                return result.Count - 1;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayAddUniqueExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.AddUnique"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            List<object> result = BlueprintArrayMutationUtility.Copy(context.GetInputValue(node, "array"));
            object item = context.GetInputValue(node, "item");
            int index = BlueprintArrayComparisonUtility.IndexOf(result, item);
            bool added = false;
            if (index < 0)
            {
                result.Add(item);
                index = result.Count - 1;
                added = true;
            }

            if (outputPortId == "array")
            {
                return result;
            }

            if (outputPortId == "index")
            {
                return index;
            }

            if (outputPortId == "added")
            {
                return added;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayInsertExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.Insert"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            List<object> result = BlueprintArrayMutationUtility.Copy(context.GetInputValue(node, "array"));
            int index = Mathf.Clamp(context.GetInputValue(node, "index", 0), 0, result.Count);
            result.Insert(index, context.GetInputValue(node, "item"));

            if (outputPortId == "array")
            {
                return result;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayRemoveIndexExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.RemoveIndex"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            List<object> result = BlueprintArrayMutationUtility.Copy(context.GetInputValue(node, "array"));
            int index = context.GetInputValue(node, "index", 0);
            bool removed = index >= 0 && index < result.Count;
            if (removed)
            {
                result.RemoveAt(index);
            }

            if (outputPortId == "array")
            {
                return result;
            }

            if (outputPortId == "removed")
            {
                return removed;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayRemoveItemExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.RemoveItem"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            List<object> result = BlueprintArrayMutationUtility.Copy(context.GetInputValue(node, "array"));
            int index = BlueprintArrayComparisonUtility.IndexOf(result, context.GetInputValue(node, "item"));
            bool removed = index >= 0;
            if (removed)
            {
                result.RemoveAt(index);
            }

            if (outputPortId == "array")
            {
                return result;
            }

            if (outputPortId == "removed")
            {
                return removed;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayClearExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.Clear"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "array")
            {
                return new List<object>();
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayResizeExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.Resize"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            List<object> result = BlueprintArrayMutationUtility.Copy(context.GetInputValue(node, "array"));
            int size = Mathf.Max(0, context.GetInputValue(node, "size", 0));
            object fillValue = context.GetInputValue(node, "fillValue");
            while (result.Count > size)
            {
                result.RemoveAt(result.Count - 1);
            }

            while (result.Count < size)
            {
                result.Add(fillValue);
            }

            if (outputPortId == "array")
            {
                return result;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArraySetElementExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.SetElement"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            List<object> result = BlueprintArrayMutationUtility.Copy(context.GetInputValue(node, "array"));
            int index = context.GetInputValue(node, "index", 0);
            bool sizeToFit = context.GetInputValue(node, "sizeToFit", false);
            bool success = index >= 0 && (index < result.Count || sizeToFit);
            if (success)
            {
                while (result.Count <= index)
                {
                    result.Add(null);
                }

                result[index] = context.GetInputValue(node, "item");
            }

            if (outputPortId == "array")
            {
                return result;
            }

            if (outputPortId == "success")
            {
                return success;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayAppendExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.Append"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            List<object> result = BlueprintArrayMutationUtility.Copy(context.GetInputValue(node, "array"));
            IList other = BlueprintArrayUtility.ReadList(context.GetInputValue(node, "other"));
            if (other != null)
            {
                for (int i = 0; i < other.Count; i++)
                {
                    result.Add(other[i]);
                }
            }

            if (outputPortId == "array")
            {
                return result;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayRandomItemExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.RandomItem"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            IList list = BlueprintArrayUtility.ReadList(context.GetInputValue(node, "array"));
            bool isValid = list != null && list.Count > 0;
            int index = isValid ? UnityEngine.Random.Range(0, list.Count) : -1;

            if (outputPortId == "item")
            {
                return isValid ? list[index] : null;
            }

            if (outputPortId == "index")
            {
                return index;
            }

            if (outputPortId == "isValid")
            {
                return isValid;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayShuffleExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.Shuffle"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            List<object> result = BlueprintArrayMutationUtility.Copy(context.GetInputValue(node, "array"));
            for (int i = result.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                object temp = result[i];
                result[i] = result[swapIndex];
                result[swapIndex] = temp;
            }

            if (outputPortId == "array")
            {
                return result;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ArrayLastIndexExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Array.LastIndex"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "index")
            {
                IList list = BlueprintArrayUtility.ReadList(context.GetInputValue(node, "array"));
                return list == null || list.Count == 0 ? -1 : list.Count - 1;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    internal static class BlueprintArrayMutationUtility
    {
        public static List<object> Copy(object value)
        {
            IList list = BlueprintArrayUtility.ReadList(value);
            List<object> result = new List<object>();
            if (list == null)
            {
                return result;
            }

            for (int i = 0; i < list.Count; i++)
            {
                result.Add(list[i]);
            }

            return result;
        }
    }
}
