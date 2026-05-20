using System.Collections;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class VectorMakeVector2Executor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.MakeVector2"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "value")
            {
                return new Vector2(context.GetInputValue(node, "x", 0f), context.GetInputValue(node, "y", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorBreakVector2Executor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.BreakVector2"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Vector2 value = BlueprintVectorUtility.GetVector2(context, node, "value", Vector2.zero);
            if (outputPortId == "x")
            {
                return value.x;
            }

            if (outputPortId == "y")
            {
                return value.y;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorMakeVector3Executor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.MakeVector3"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "value")
            {
                return new Vector3(
                    context.GetInputValue(node, "x", 0f),
                    context.GetInputValue(node, "y", 0f),
                    context.GetInputValue(node, "z", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorBreakVector3Executor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.BreakVector3"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Vector3 value = BlueprintVectorUtility.GetVector3(context, node, "value", Vector3.zero);
            if (outputPortId == "x")
            {
                return value.x;
            }

            if (outputPortId == "y")
            {
                return value.y;
            }

            if (outputPortId == "z")
            {
                return value.z;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorMakeVector4Executor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.MakeVector4"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "value")
            {
                return new Vector4(
                    context.GetInputValue(node, "x", 0f),
                    context.GetInputValue(node, "y", 0f),
                    context.GetInputValue(node, "z", 0f),
                    context.GetInputValue(node, "w", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorBreakVector4Executor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.BreakVector4"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Vector4 value = BlueprintVectorUtility.GetVector4(context, node, "value", Vector4.zero);
            if (outputPortId == "x")
            {
                return value.x;
            }

            if (outputPortId == "y")
            {
                return value.y;
            }

            if (outputPortId == "z")
            {
                return value.z;
            }

            if (outputPortId == "w")
            {
                return value.w;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorAddExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.Add"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return BlueprintVectorUtility.GetVector3(context, node, "a", Vector3.zero) +
                       BlueprintVectorUtility.GetVector3(context, node, "b", Vector3.zero);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorSubtractExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.Subtract"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return BlueprintVectorUtility.GetVector3(context, node, "a", Vector3.zero) -
                       BlueprintVectorUtility.GetVector3(context, node, "b", Vector3.zero);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorMultiplyExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.Multiply"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return BlueprintVectorUtility.GetVector3(context, node, "vector", Vector3.zero) *
                       context.GetInputValue(node, "scalar", 1f);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorDivideExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.Divide"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                float scalar = context.GetInputValue(node, "scalar", 1f);
                return Mathf.Approximately(scalar, 0f)
                    ? Vector3.zero
                    : BlueprintVectorUtility.GetVector3(context, node, "vector", Vector3.zero) / scalar;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorDotExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.Dot"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Vector3.Dot(
                    BlueprintVectorUtility.GetVector3(context, node, "a", Vector3.zero),
                    BlueprintVectorUtility.GetVector3(context, node, "b", Vector3.zero));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorCrossExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.Cross"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Vector3.Cross(
                    BlueprintVectorUtility.GetVector3(context, node, "a", Vector3.zero),
                    BlueprintVectorUtility.GetVector3(context, node, "b", Vector3.zero));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorLengthExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.Length"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return BlueprintVectorUtility.GetVector3(context, node, "value", Vector3.zero).magnitude;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorNormalizeExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.Normalize"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return BlueprintVectorUtility.GetVector3(context, node, "value", Vector3.zero).normalized;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorDistanceExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.Distance"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Vector3.Distance(
                    BlueprintVectorUtility.GetVector3(context, node, "a", Vector3.zero),
                    BlueprintVectorUtility.GetVector3(context, node, "b", Vector3.zero));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class VectorLerpExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Vector.Lerp"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Vector3.Lerp(
                    BlueprintVectorUtility.GetVector3(context, node, "a", Vector3.zero),
                    BlueprintVectorUtility.GetVector3(context, node, "b", Vector3.zero),
                    context.GetInputValue(node, "t", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ColorMakeExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Color.Make"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "value")
            {
                return new Color(
                    context.GetInputValue(node, "r", 1f),
                    context.GetInputValue(node, "g", 1f),
                    context.GetInputValue(node, "b", 1f),
                    context.GetInputValue(node, "a", 1f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ColorBreakExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Color.Break"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            Color value = BlueprintVectorUtility.GetColor(context, node, "value", Color.white);
            if (outputPortId == "r")
            {
                return value.r;
            }

            if (outputPortId == "g")
            {
                return value.g;
            }

            if (outputPortId == "b")
            {
                return value.b;
            }

            if (outputPortId == "a")
            {
                return value.a;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class ColorLerpExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Color.Lerp"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Color.Lerp(
                    BlueprintVectorUtility.GetColor(context, node, "a", Color.white),
                    BlueprintVectorUtility.GetColor(context, node, "b", Color.white),
                    context.GetInputValue(node, "t", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    internal static class BlueprintVectorUtility
    {
        public static Vector2 GetVector2(BlueprintExecutionContext context, RuntimeNode node, string portId, Vector2 defaultValue)
        {
            object value = context.GetInputValue(node, portId);
            if (value is Vector2)
            {
                return (Vector2)value;
            }

            return BlueprintTypeUtility.ToVector2(value, defaultValue);
        }

        public static Vector3 GetVector3(BlueprintExecutionContext context, RuntimeNode node, string portId, Vector3 defaultValue)
        {
            object value = context.GetInputValue(node, portId);
            if (value is Vector3)
            {
                return (Vector3)value;
            }

            return BlueprintTypeUtility.ToVector3(value, defaultValue);
        }

        public static Vector4 GetVector4(BlueprintExecutionContext context, RuntimeNode node, string portId, Vector4 defaultValue)
        {
            object value = context.GetInputValue(node, portId);
            if (value is Vector4)
            {
                return (Vector4)value;
            }

            return BlueprintTypeUtility.ToVector4(value, defaultValue);
        }

        public static Color GetColor(BlueprintExecutionContext context, RuntimeNode node, string portId, Color defaultValue)
        {
            object value = context.GetInputValue(node, portId);
            if (value is Color)
            {
                return (Color)value;
            }

            IList list = value as IList;
            if (list == null || list.Count < 3)
            {
                return defaultValue;
            }

            float r = System.Convert.ToSingle(list[0]);
            float g = System.Convert.ToSingle(list[1]);
            float b = System.Convert.ToSingle(list[2]);
            float a = list.Count >= 4 ? System.Convert.ToSingle(list[3]) : defaultValue.a;
            return new Color(r, g, b, a);
        }
    }
}
