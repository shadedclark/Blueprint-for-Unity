using System;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class MathAddExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Add"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return context.GetInputValue(node, "a", 0f) + context.GetInputValue(node, "b", 0f);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathSubtractExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Subtract"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return context.GetInputValue(node, "a", 0f) - context.GetInputValue(node, "b", 0f);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathMultiplyExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Multiply"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return context.GetInputValue(node, "a", 0f) * context.GetInputValue(node, "b", 0f);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathDivideExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Divide"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                float divisor = context.GetInputValue(node, "b", 1f);
                return Mathf.Approximately(divisor, 0f) ? 0f : context.GetInputValue(node, "a", 0f) / divisor;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathModuloExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Modulo"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                float divisor = context.GetInputValue(node, "b", 1f);
                return Mathf.Approximately(divisor, 0f) ? 0f : context.GetInputValue(node, "a", 0f) % divisor;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathAbsExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Abs"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Mathf.Abs(context.GetInputValue(node, "value", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathClampExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Clamp"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Mathf.Clamp(
                    context.GetInputValue(node, "value", 0f),
                    context.GetInputValue(node, "min", 0f),
                    context.GetInputValue(node, "max", 1f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathMinExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Min"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Mathf.Min(context.GetInputValue(node, "a", 0f), context.GetInputValue(node, "b", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathMaxExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Max"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Mathf.Max(context.GetInputValue(node, "a", 0f), context.GetInputValue(node, "b", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathRoundExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Round"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Mathf.RoundToInt(context.GetInputValue(node, "value", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathFloorExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Floor"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Mathf.FloorToInt(context.GetInputValue(node, "value", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathCeilExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Ceil"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Mathf.CeilToInt(context.GetInputValue(node, "value", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathLerpExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.Lerp"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return Mathf.Lerp(
                    context.GetInputValue(node, "a", 0f),
                    context.GetInputValue(node, "b", 1f),
                    context.GetInputValue(node, "t", 0f));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathMapRangeClampedExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.MapRangeClamped"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                float value = context.GetInputValue(node, "value", 0f);
                float inA = context.GetInputValue(node, "inMin", 0f);
                float inB = context.GetInputValue(node, "inMax", 1f);
                float outA = context.GetInputValue(node, "outMin", 0f);
                float outB = context.GetInputValue(node, "outMax", 1f);
                if (Mathf.Approximately(inA, inB))
                {
                    return outA;
                }

                float t = Mathf.InverseLerp(inA, inB, value);
                return Mathf.Lerp(outA, outB, t);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathRandomFloatExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.RandomFloat"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                float min = context.GetInputValue(node, "min", 0f);
                float max = context.GetInputValue(node, "max", 1f);
                return UnityEngine.Random.Range(Mathf.Min(min, max), Mathf.Max(min, max));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathRandomIntExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.RandomInt"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                int min = context.GetInputValue(node, "min", 0);
                int max = context.GetInputValue(node, "max", 1);
                int low = Math.Min(min, max);
                int high = Math.Max(min, max);
                return UnityEngine.Random.Range(low, high + 1);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathRandomBoolExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.RandomBool"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                return UnityEngine.Random.value >= 0.5f;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathStableRandomFloatExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.StableRandomFloat"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                float min = context.GetInputValue(node, "min", 0f);
                float max = context.GetInputValue(node, "max", 1f);
                int seed = context.GetInputValue(node, "seed", 0);
                int sequence = context.GetInputValue(node, "sequence", 0);
                int stream = context.GetInputValue(node, "stream", 0);
                float low = Mathf.Min(min, max);
                float high = Mathf.Max(min, max);
                return low + ((high - low) * BlueprintStableRandom.Value01(seed, sequence, stream));
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathStableRandomIntExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.StableRandomInt"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                int min = context.GetInputValue(node, "min", 0);
                int max = context.GetInputValue(node, "max", 1);
                int seed = context.GetInputValue(node, "seed", 0);
                int sequence = context.GetInputValue(node, "sequence", 0);
                int stream = context.GetInputValue(node, "stream", 0);
                return BlueprintStableRandom.RangeInclusive(seed, sequence, stream, min, max);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class MathStableRandomBoolExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Math.StableRandomBool"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "result")
            {
                int seed = context.GetInputValue(node, "seed", 0);
                int sequence = context.GetInputValue(node, "sequence", 0);
                int stream = context.GetInputValue(node, "stream", 0);
                return (BlueprintStableRandom.Sample(seed, sequence, stream, 0) & 1UL) != 0UL;
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    internal static class BlueprintStableRandom
    {
        public static ulong Sample(int seed, int sequence, int stream, int attempt)
        {
            unchecked
            {
                ulong value = ((ulong)(uint)seed << 32) | (uint)sequence;
                value ^= ((ulong)(uint)stream * 0xD2B74407B1CE6E93UL);
                value ^= ((ulong)(uint)attempt * 0xCA5A826395121157UL);
                value += 0x9E3779B97F4A7C15UL;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        public static float Value01(int seed, int sequence, int stream)
        {
            uint mantissa = (uint)(Sample(seed, sequence, stream, 0) >> 40);
            return mantissa * (1f / 16777216f);
        }

        public static int RangeInclusive(int seed, int sequence, int stream, int min, int max)
        {
            int low = Math.Min(min, max);
            int high = Math.Max(min, max);
            ulong range = (ulong)((long)high - low) + 1UL;
            ulong threshold = unchecked(0UL - range) % range;
            int attempt = 0;
            ulong sample;
            do
            {
                sample = Sample(seed, sequence, stream, attempt);
                attempt++;
            }
            while (sample < threshold);

            long result = low + (long)(sample % range);
            return (int)result;
        }
    }
}
