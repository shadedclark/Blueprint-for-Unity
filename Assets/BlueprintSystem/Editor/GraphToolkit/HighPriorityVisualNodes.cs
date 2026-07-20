using System;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.ForLoop")]
    public sealed class FlowForLoopVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.ForLoop", "For Loop", "Flow", "Executes loopBody for each integer index from firstIndex through lastIndex.");
            AddExecInput("execIn");
            AddValueInput("firstIndex", "int", true, "propertyOrConnection");
            AddValueInput("lastIndex", "int", true, "propertyOrConnection");
            AddExecOutput("loopBody");
            AddExecOutput("completed");
            AddValueOutput("index", "int");
            AddProperty("firstIndex", "int", false, 0);
            AddProperty("lastIndex", "int", false, 0);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.ForLoopWithBreak")]
    public sealed class FlowForLoopWithBreakVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.ForLoopWithBreak", "For Loop with Break", "Flow", "Executes loopBody for each integer index and can be stopped by the break input.");
            AddExecInput("execIn");
            AddExecInput("break");
            AddValueInput("firstIndex", "int", true, "propertyOrConnection");
            AddValueInput("lastIndex", "int", true, "propertyOrConnection");
            AddExecOutput("loopBody");
            AddExecOutput("completed");
            AddValueOutput("index", "int");
            AddProperty("firstIndex", "int", false, 0);
            AddProperty("lastIndex", "int", false, 0);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.DoOnce")]
    public sealed class FlowDoOnceVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.DoOnce", "Do Once", "Flow", "Allows execution through once until reset is triggered.");
            AddExecInput("execIn");
            AddExecInput("reset");
            AddValueInput("startClosed", "bool", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("startClosed", "bool", false, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.DoN")]
    public sealed class FlowDoNVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.DoN", "Do N", "Flow", "Allows execution through a limited number of times until reset is triggered.");
            AddExecInput("execIn");
            AddExecInput("reset");
            AddValueInput("count", "int", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddExecOutput("completed");
            AddProperty("count", "int", false, 1);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.FlipFlop")]
    public sealed class FlowFlipFlopVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.FlipFlop", "Flip Flop", "Flow", "Alternates execution between A and B outputs.");
            AddExecInput("execIn");
            AddExecOutput("a");
            AddExecOutput("b");
            AddValueOutput("isA", "bool");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.Gate")]
    public sealed class FlowGateVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.Gate", "Gate", "Flow", "Lets enter execution pass through exit only while the gate is open.");
            AddExecInput("enter");
            AddExecInput("open");
            AddExecInput("close");
            AddExecInput("toggle");
            AddValueInput("startClosed", "bool", false, "propertyOrConnection");
            AddExecOutput("exit");
            AddProperty("startClosed", "bool", false, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.MultiGate")]
    public sealed class FlowMultiGateVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.MultiGate", "Multi Gate", "Flow", "Routes each execution to one of up to eight output pins in sequence or random order.");
            AddExecInput("execIn");
            AddExecInput("reset");
            AddValueInput("outputCount", "int", true, "propertyOrConnection");
            AddValueInput("startIndex", "int", false, "propertyOrConnection");
            AddValueInput("loop", "bool", false, "propertyOrConnection");
            AddValueInput("random", "bool", false, "propertyOrConnection");
            AddExecOutput("out0");
            AddExecOutput("out1");
            AddExecOutput("out2");
            AddExecOutput("out3");
            AddExecOutput("out4");
            AddExecOutput("out5");
            AddExecOutput("out6");
            AddExecOutput("out7");
            AddProperty("outputCount", "int", false, 2);
            AddProperty("startIndex", "int", false, 0);
            AddProperty("loop", "bool", false, false);
            AddProperty("random", "bool", false, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.SwitchInt")]
    public sealed class FlowSwitchIntVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.SwitchInt", "Switch on Int", "Flow", "Routes execution to the first case whose integer value matches selection.");
            AddExecInput("execIn");
            AddValueInput("selection", "int", true, "propertyOrConnection");
            AddValueInput("caseCount", "int", true, "propertyOrConnection");
            AddValueInput("case0", "int", false, "propertyOrConnection");
            AddValueInput("case1", "int", false, "propertyOrConnection");
            AddValueInput("case2", "int", false, "propertyOrConnection");
            AddValueInput("case3", "int", false, "propertyOrConnection");
            AddValueInput("case4", "int", false, "propertyOrConnection");
            AddValueInput("case5", "int", false, "propertyOrConnection");
            AddValueInput("case6", "int", false, "propertyOrConnection");
            AddValueInput("case7", "int", false, "propertyOrConnection");
            AddExecOutput("case0");
            AddExecOutput("case1");
            AddExecOutput("case2");
            AddExecOutput("case3");
            AddExecOutput("case4");
            AddExecOutput("case5");
            AddExecOutput("case6");
            AddExecOutput("case7");
            AddExecOutput("default");
            AddProperty("selection", "int", false, 0);
            AddProperty("caseCount", "int", false, 4);
            AddProperty("case0", "int", false, 0);
            AddProperty("case1", "int", false, 1);
            AddProperty("case2", "int", false, 2);
            AddProperty("case3", "int", false, 3);
            AddProperty("case4", "int", false, 4);
            AddProperty("case5", "int", false, 5);
            AddProperty("case6", "int", false, 6);
            AddProperty("case7", "int", false, 7);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.SwitchString")]
    public sealed class FlowSwitchStringVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.SwitchString", "Switch on String", "Flow", "Routes execution to the first case whose string value matches selection.");
            AddExecInput("execIn");
            AddValueInput("selection", "string", true, "propertyOrConnection");
            AddValueInput("caseCount", "int", true, "propertyOrConnection");
            AddValueInput("case0", "string", false, "propertyOrConnection");
            AddValueInput("case1", "string", false, "propertyOrConnection");
            AddValueInput("case2", "string", false, "propertyOrConnection");
            AddValueInput("case3", "string", false, "propertyOrConnection");
            AddValueInput("case4", "string", false, "propertyOrConnection");
            AddValueInput("case5", "string", false, "propertyOrConnection");
            AddValueInput("case6", "string", false, "propertyOrConnection");
            AddValueInput("case7", "string", false, "propertyOrConnection");
            AddExecOutput("case0");
            AddExecOutput("case1");
            AddExecOutput("case2");
            AddExecOutput("case3");
            AddExecOutput("case4");
            AddExecOutput("case5");
            AddExecOutput("case6");
            AddExecOutput("case7");
            AddExecOutput("default");
            AddProperty("selection", "string", false, "");
            AddProperty("caseCount", "int", false, 4);
            AddProperty("case0", "string", false, "");
            AddProperty("case1", "string", false, "");
            AddProperty("case2", "string", false, "");
            AddProperty("case3", "string", false, "");
            AddProperty("case4", "string", false, "");
            AddProperty("case5", "string", false, "");
            AddProperty("case6", "string", false, "");
            AddProperty("case7", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.SwitchEnum")]
    public sealed class FlowSwitchEnumVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.SwitchEnum", "Switch on Enum", "Flow", "Routes execution by comparing enum names stored as strings.");
            AddExecInput("execIn");
            AddValueInput("selection", "string", true, "propertyOrConnection");
            AddValueInput("caseCount", "int", true, "propertyOrConnection");
            AddValueInput("case0", "string", false, "propertyOrConnection");
            AddValueInput("case1", "string", false, "propertyOrConnection");
            AddValueInput("case2", "string", false, "propertyOrConnection");
            AddValueInput("case3", "string", false, "propertyOrConnection");
            AddValueInput("case4", "string", false, "propertyOrConnection");
            AddValueInput("case5", "string", false, "propertyOrConnection");
            AddValueInput("case6", "string", false, "propertyOrConnection");
            AddValueInput("case7", "string", false, "propertyOrConnection");
            AddExecOutput("case0");
            AddExecOutput("case1");
            AddExecOutput("case2");
            AddExecOutput("case3");
            AddExecOutput("case4");
            AddExecOutput("case5");
            AddExecOutput("case6");
            AddExecOutput("case7");
            AddExecOutput("default");
            AddProperty("selection", "string", false, "");
            AddProperty("caseCount", "int", false, 4);
            AddProperty("case0", "string", false, "");
            AddProperty("case1", "string", false, "");
            AddProperty("case2", "string", false, "");
            AddProperty("case3", "string", false, "");
            AddProperty("case4", "string", false, "");
            AddProperty("case5", "string", false, "");
            AddProperty("case6", "string", false, "");
            AddProperty("case7", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Add")]
    public sealed class MathAddVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Add", "Add", "Math", "Adds two float values.");
            AddValueInput("a", "float", true, "propertyOrConnection");
            AddValueInput("b", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("a", "float", false, 0.0f);
            AddProperty("b", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Subtract")]
    public sealed class MathSubtractVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Subtract", "Subtract", "Math", "Subtracts b from a.");
            AddValueInput("a", "float", true, "propertyOrConnection");
            AddValueInput("b", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("a", "float", false, 0.0f);
            AddProperty("b", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Multiply")]
    public sealed class MathMultiplyVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Multiply", "Multiply", "Math", "Multiplies two float values.");
            AddValueInput("a", "float", true, "propertyOrConnection");
            AddValueInput("b", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("a", "float", false, 0.0f);
            AddProperty("b", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Divide")]
    public sealed class MathDivideVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Divide", "Divide", "Math", "Divides a by b and returns 0 when b is zero.");
            AddValueInput("a", "float", true, "propertyOrConnection");
            AddValueInput("b", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("a", "float", false, 0.0f);
            AddProperty("b", "float", false, 1.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Modulo")]
    public sealed class MathModuloVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Modulo", "Modulo", "Math", "Returns a modulo b and returns 0 when b is zero.");
            AddValueInput("a", "float", true, "propertyOrConnection");
            AddValueInput("b", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("a", "float", false, 0.0f);
            AddProperty("b", "float", false, 1.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Min")]
    public sealed class MathMinVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Min", "Min", "Math", "Returns the smaller of two float values.");
            AddValueInput("a", "float", true, "propertyOrConnection");
            AddValueInput("b", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("a", "float", false, 0.0f);
            AddProperty("b", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Max")]
    public sealed class MathMaxVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Max", "Max", "Math", "Returns the larger of two float values.");
            AddValueInput("a", "float", true, "propertyOrConnection");
            AddValueInput("b", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("a", "float", false, 0.0f);
            AddProperty("b", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Abs")]
    public sealed class MathAbsVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Abs", "Abs", "Math", "Returns the absolute value.");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("value", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Clamp")]
    public sealed class MathClampVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Clamp", "Clamp", "Math", "Clamps a value between min and max.");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddValueInput("min", "float", true, "propertyOrConnection");
            AddValueInput("max", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("value", "float", false, 0.0f);
            AddProperty("min", "float", false, 0.0f);
            AddProperty("max", "float", false, 1.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Round")]
    public sealed class MathRoundVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Round", "Round", "Math", "Rounds a float to the nearest int.");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddValueOutput("result", "int");
            AddProperty("value", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Floor")]
    public sealed class MathFloorVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Floor", "Floor", "Math", "Rounds a float down to an int.");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddValueOutput("result", "int");
            AddProperty("value", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Ceil")]
    public sealed class MathCeilVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Ceil", "Ceil", "Math", "Rounds a float up to an int.");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddValueOutput("result", "int");
            AddProperty("value", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.Lerp")]
    public sealed class MathLerpVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.Lerp", "Lerp", "Math", "Linearly interpolates between two float values.");
            AddValueInput("a", "float", true, "propertyOrConnection");
            AddValueInput("b", "float", true, "propertyOrConnection");
            AddValueInput("t", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("a", "float", false, 0.0f);
            AddProperty("b", "float", false, 1.0f);
            AddProperty("t", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.MapRangeClamped")]
    public sealed class MathMapRangeClampedVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.MapRangeClamped", "Map Range Clamped", "Math", "Maps a value from one range to another using clamped interpolation.");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddValueInput("inMin", "float", true, "propertyOrConnection");
            AddValueInput("inMax", "float", true, "propertyOrConnection");
            AddValueInput("outMin", "float", true, "propertyOrConnection");
            AddValueInput("outMax", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("value", "float", false, 0.0f);
            AddProperty("inMin", "float", false, 0.0f);
            AddProperty("inMax", "float", false, 1.0f);
            AddProperty("outMin", "float", false, 0.0f);
            AddProperty("outMax", "float", false, 1.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.RandomFloat")]
    public sealed class MathRandomFloatVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.RandomFloat", "Random Float", "Math", "Returns a random float between min and max.");
            AddValueInput("min", "float", true, "propertyOrConnection");
            AddValueInput("max", "float", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("min", "float", false, 0.0f);
            AddProperty("max", "float", false, 1.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.RandomInt")]
    public sealed class MathRandomIntVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.RandomInt", "Random Int", "Math", "Returns a random int between min and max inclusive.");
            AddValueInput("min", "int", true, "propertyOrConnection");
            AddValueInput("max", "int", true, "propertyOrConnection");
            AddValueOutput("result", "int");
            AddProperty("min", "int", false, 0);
            AddProperty("max", "int", false, 1);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.RandomBool")]
    public sealed class MathRandomBoolVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.RandomBool", "Random Bool", "Math", "Returns a random boolean value.");
            AddValueOutput("result", "bool");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.StableRandomFloat")]
    public sealed class MathStableRandomFloatVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.StableRandomFloat", "Stable Random Float", "Math", "Returns a deterministic float in [min,max) for seed, sequence, and stream.");
            AddValueInput("min", "float", true, "propertyOrConnection");
            AddValueInput("max", "float", true, "propertyOrConnection");
            AddValueInput("seed", "int", true, "propertyOrConnection");
            AddValueInput("sequence", "int", true, "propertyOrConnection");
            AddValueInput("stream", "int", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("min", "float", false, 0.0f);
            AddProperty("max", "float", false, 1.0f);
            AddProperty("seed", "int", false, 0);
            AddProperty("sequence", "int", false, 0);
            AddProperty("stream", "int", false, 0);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.StableRandomInt")]
    public sealed class MathStableRandomIntVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.StableRandomInt", "Stable Random Int", "Math", "Returns a deterministic inclusive integer for seed, sequence, and stream.");
            AddValueInput("min", "int", true, "propertyOrConnection");
            AddValueInput("max", "int", true, "propertyOrConnection");
            AddValueInput("seed", "int", true, "propertyOrConnection");
            AddValueInput("sequence", "int", true, "propertyOrConnection");
            AddValueInput("stream", "int", true, "propertyOrConnection");
            AddValueOutput("result", "int");
            AddProperty("min", "int", false, 0);
            AddProperty("max", "int", false, 1);
            AddProperty("seed", "int", false, 0);
            AddProperty("sequence", "int", false, 0);
            AddProperty("stream", "int", false, 0);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Math.StableRandomBool")]
    public sealed class MathStableRandomBoolVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Math.StableRandomBool", "Stable Random Bool", "Math", "Returns a deterministic boolean for seed, sequence, and stream.");
            AddValueInput("seed", "int", true, "propertyOrConnection");
            AddValueInput("sequence", "int", true, "propertyOrConnection");
            AddValueInput("stream", "int", true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("seed", "int", false, 0);
            AddProperty("sequence", "int", false, 0);
            AddProperty("stream", "int", false, 0);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.MakeVector2")]
    public sealed class VectorMakeVector2VisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.MakeVector2", "Make Vector2", "Vector", "Creates a Vector2 from x and y.");
            AddValueInput("x", "float", true, "propertyOrConnection");
            AddValueInput("y", "float", true, "propertyOrConnection");
            AddValueOutput("value", "Vector2");
            AddProperty("x", "float", false, 0.0f);
            AddProperty("y", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.BreakVector2")]
    public sealed class VectorBreakVector2VisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.BreakVector2", "Break Vector2", "Vector", "Splits a Vector2 into x and y.");
            AddValueInput("value", "Vector2", true, "propertyOrConnection");
            AddValueOutput("x", "float");
            AddValueOutput("y", "float");
            AddProperty("value", "Vector2", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.MakeVector3")]
    public sealed class VectorMakeVector3VisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.MakeVector3", "Make Vector3", "Vector", "Creates a Vector3 from x, y, and z.");
            AddValueInput("x", "float", true, "propertyOrConnection");
            AddValueInput("y", "float", true, "propertyOrConnection");
            AddValueInput("z", "float", true, "propertyOrConnection");
            AddValueOutput("value", "Vector3");
            AddProperty("x", "float", false, 0.0f);
            AddProperty("y", "float", false, 0.0f);
            AddProperty("z", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.BreakVector3")]
    public sealed class VectorBreakVector3VisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.BreakVector3", "Break Vector3", "Vector", "Splits a Vector3 into x, y, and z.");
            AddValueInput("value", "Vector3", true, "propertyOrConnection");
            AddValueOutput("x", "float");
            AddValueOutput("y", "float");
            AddValueOutput("z", "float");
            AddProperty("value", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.MakeVector4")]
    public sealed class VectorMakeVector4VisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.MakeVector4", "Make Vector4", "Vector", "Creates a Vector4 from x, y, z, and w.");
            AddValueInput("x", "float", true, "propertyOrConnection");
            AddValueInput("y", "float", true, "propertyOrConnection");
            AddValueInput("z", "float", true, "propertyOrConnection");
            AddValueInput("w", "float", true, "propertyOrConnection");
            AddValueOutput("value", "Vector4");
            AddProperty("x", "float", false, 0.0f);
            AddProperty("y", "float", false, 0.0f);
            AddProperty("z", "float", false, 0.0f);
            AddProperty("w", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.BreakVector4")]
    public sealed class VectorBreakVector4VisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.BreakVector4", "Break Vector4", "Vector", "Splits a Vector4 into x, y, z, and w.");
            AddValueInput("value", "Vector4", true, "propertyOrConnection");
            AddValueOutput("x", "float");
            AddValueOutput("y", "float");
            AddValueOutput("z", "float");
            AddValueOutput("w", "float");
            AddProperty("value", "Vector4", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f, 0.0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.Add")]
    public sealed class VectorAddVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.Add", "Vector Add", "Vector", "Adds two Vector3 values.");
            AddValueInput("a", "Vector3", true, "propertyOrConnection");
            AddValueInput("b", "Vector3", true, "propertyOrConnection");
            AddValueOutput("result", "Vector3");
            AddProperty("a", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
            AddProperty("b", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.Subtract")]
    public sealed class VectorSubtractVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.Subtract", "Vector Subtract", "Vector", "Subtracts b from a.");
            AddValueInput("a", "Vector3", true, "propertyOrConnection");
            AddValueInput("b", "Vector3", true, "propertyOrConnection");
            AddValueOutput("result", "Vector3");
            AddProperty("a", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
            AddProperty("b", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.Multiply")]
    public sealed class VectorMultiplyVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.Multiply", "Vector Multiply", "Vector", "Multiplies a Vector3 by a scalar.");
            AddValueInput("vector", "Vector3", true, "propertyOrConnection");
            AddValueInput("scalar", "float", true, "propertyOrConnection");
            AddValueOutput("result", "Vector3");
            AddProperty("vector", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
            AddProperty("scalar", "float", false, 1.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.Divide")]
    public sealed class VectorDivideVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.Divide", "Vector Divide", "Vector", "Divides a Vector3 by a scalar and returns zero when scalar is zero.");
            AddValueInput("vector", "Vector3", true, "propertyOrConnection");
            AddValueInput("scalar", "float", true, "propertyOrConnection");
            AddValueOutput("result", "Vector3");
            AddProperty("vector", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
            AddProperty("scalar", "float", false, 1.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.Dot")]
    public sealed class VectorDotVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.Dot", "Dot Product", "Vector", "Returns the dot product of two Vector3 values.");
            AddValueInput("a", "Vector3", true, "propertyOrConnection");
            AddValueInput("b", "Vector3", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("a", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
            AddProperty("b", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.Cross")]
    public sealed class VectorCrossVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.Cross", "Cross Product", "Vector", "Returns the cross product of two Vector3 values.");
            AddValueInput("a", "Vector3", true, "propertyOrConnection");
            AddValueInput("b", "Vector3", true, "propertyOrConnection");
            AddValueOutput("result", "Vector3");
            AddProperty("a", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
            AddProperty("b", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.Distance")]
    public sealed class VectorDistanceVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.Distance", "Vector Distance", "Vector", "Returns the distance between two Vector3 values.");
            AddValueInput("a", "Vector3", true, "propertyOrConnection");
            AddValueInput("b", "Vector3", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("a", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
            AddProperty("b", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.Length")]
    public sealed class VectorLengthVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.Length", "Vector Length", "Vector", "Returns the magnitude of a Vector3.");
            AddValueInput("value", "Vector3", true, "propertyOrConnection");
            AddValueOutput("result", "float");
            AddProperty("value", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.Normalize")]
    public sealed class VectorNormalizeVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.Normalize", "Normalize Vector", "Vector", "Returns a normalized Vector3.");
            AddValueInput("value", "Vector3", true, "propertyOrConnection");
            AddValueOutput("result", "Vector3");
            AddProperty("value", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Vector.Lerp")]
    public sealed class VectorLerpVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Vector.Lerp", "Vector Lerp", "Vector", "Linearly interpolates between two Vector3 values.");
            AddValueInput("a", "Vector3", true, "propertyOrConnection");
            AddValueInput("b", "Vector3", true, "propertyOrConnection");
            AddValueInput("t", "float", true, "propertyOrConnection");
            AddValueOutput("result", "Vector3");
            AddProperty("a", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
            AddProperty("b", "Vector3", false, new System.Collections.Generic.List<object> { 0.0f, 0.0f, 0.0f });
            AddProperty("t", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Color.Make")]
    public sealed class ColorMakeVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Color.Make", "Make Color", "Color", "Creates a Color from r, g, b, and a.");
            AddValueInput("r", "float", true, "propertyOrConnection");
            AddValueInput("g", "float", true, "propertyOrConnection");
            AddValueInput("b", "float", true, "propertyOrConnection");
            AddValueInput("a", "float", true, "propertyOrConnection");
            AddValueOutput("value", "Color");
            AddProperty("r", "float", false, 1.0f);
            AddProperty("g", "float", false, 1.0f);
            AddProperty("b", "float", false, 1.0f);
            AddProperty("a", "float", false, 1.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Color.Break")]
    public sealed class ColorBreakVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Color.Break", "Break Color", "Color", "Splits a Color into r, g, b, and a.");
            AddValueInput("value", "Color", true, "propertyOrConnection");
            AddValueOutput("r", "float");
            AddValueOutput("g", "float");
            AddValueOutput("b", "float");
            AddValueOutput("a", "float");
            AddProperty("value", "Color", false, new System.Collections.Generic.List<object> { 1.0f, 1.0f, 1.0f, 1.0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Color.Lerp")]
    public sealed class ColorLerpVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Color.Lerp", "Color Lerp", "Color", "Linearly interpolates between two colors.");
            AddValueInput("a", "Color", true, "propertyOrConnection");
            AddValueInput("b", "Color", true, "propertyOrConnection");
            AddValueInput("t", "float", true, "propertyOrConnection");
            AddValueOutput("result", "Color");
            AddProperty("a", "Color", false, new System.Collections.Generic.List<object> { 1.0f, 1.0f, 1.0f, 1.0f });
            AddProperty("b", "Color", false, new System.Collections.Generic.List<object> { 1.0f, 1.0f, 1.0f, 1.0f });
            AddProperty("t", "float", false, 0.0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("String.Append")]
    public sealed class StringAppendVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("String.Append", "Append", "String", "Concatenates up to four values as text.");
            AddValueInput("a", null, false, "propertyOrConnection");
            AddValueInput("b", null, false, "propertyOrConnection");
            AddValueInput("c", null, false, "propertyOrConnection");
            AddValueInput("d", null, false, "propertyOrConnection");
            AddValueOutput("result", "string");
            AddProperty("a", null, false, "");
            AddProperty("b", null, false, "");
            AddProperty("c", null, false, "");
            AddProperty("d", null, false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("String.Format")]
    public sealed class StringFormatVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("String.Format", "Format", "String", "Formats a string using {0} through {3} placeholders.");
            AddValueInput("format", "string", true, "propertyOrConnection");
            AddValueInput("arg0", null, false, "propertyOrConnection");
            AddValueInput("arg1", null, false, "propertyOrConnection");
            AddValueInput("arg2", null, false, "propertyOrConnection");
            AddValueInput("arg3", null, false, "propertyOrConnection");
            AddValueOutput("result", "string");
            AddProperty("format", "string", false, "");
            AddProperty("arg0", null, false, "");
            AddProperty("arg1", null, false, "");
            AddProperty("arg2", null, false, "");
            AddProperty("arg3", null, false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("String.ToString")]
    public sealed class StringToStringVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("String.ToString", "To String", "String", "Converts any value to a string.");
            AddValueInput("value", null, false, "propertyOrConnection");
            AddValueOutput("result", "string");
            AddProperty("value", null, false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("String.Contains")]
    public sealed class StringContainsVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("String.Contains", "String Contains", "String", "Returns true when value contains search.");
            AddValueInput("value", "string", true, "propertyOrConnection");
            AddValueInput("search", "string", true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("value", "string", false, "");
            AddProperty("search", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("String.StartsWith")]
    public sealed class StringStartsWithVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("String.StartsWith", "Starts With", "String", "Returns true when value starts with prefix.");
            AddValueInput("value", "string", true, "propertyOrConnection");
            AddValueInput("prefix", "string", true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("value", "string", false, "");
            AddProperty("prefix", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("String.EndsWith")]
    public sealed class StringEndsWithVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("String.EndsWith", "Ends With", "String", "Returns true when value ends with suffix.");
            AddValueInput("value", "string", true, "propertyOrConnection");
            AddValueInput("suffix", "string", true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("value", "string", false, "");
            AddProperty("suffix", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("String.Replace")]
    public sealed class StringReplaceVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("String.Replace", "Replace", "String", "Replaces all oldValue occurrences with newValue.");
            AddValueInput("value", "string", true, "propertyOrConnection");
            AddValueInput("oldValue", "string", true, "propertyOrConnection");
            AddValueInput("newValue", "string", false, "propertyOrConnection");
            AddValueOutput("result", "string");
            AddProperty("value", "string", false, "");
            AddProperty("oldValue", "string", false, "");
            AddProperty("newValue", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("String.Split")]
    public sealed class StringSplitVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("String.Split", "Split", "String", "Splits value by separator and returns an Array<string>.");
            AddValueInput("value", "string", true, "propertyOrConnection");
            AddValueInput("separator", "string", true, "propertyOrConnection");
            AddValueOutput("items", "Array<string>");
            AddProperty("value", "string", false, "");
            AddProperty("separator", "string", false, ",");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("String.Length")]
    public sealed class StringLengthVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("String.Length", "String Length", "String", "Returns the number of characters in value.");
            AddValueInput("value", "string", true, "propertyOrConnection");
            AddValueOutput("length", "int");
            AddProperty("value", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("String.Substring")]
    public sealed class StringSubstringVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("String.Substring", "Substring", "String", "Returns a substring starting at start with optional length; negative length means to the end.");
            AddValueInput("value", "string", true, "propertyOrConnection");
            AddValueInput("start", "int", true, "propertyOrConnection");
            AddValueInput("length", "int", false, "propertyOrConnection");
            AddValueOutput("result", "string");
            AddProperty("value", "string", false, "");
            AddProperty("start", "int", false, 0);
            AddProperty("length", "int", false, -1);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("String.EqualIgnoreCase")]
    public sealed class StringEqualIgnoreCaseVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("String.EqualIgnoreCase", "Equal Ignore Case", "String", "Compares two strings using ordinal ignore-case comparison.");
            AddValueInput("a", "string", true, "propertyOrConnection");
            AddValueInput("b", "string", true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("a", "string", false, "");
            AddProperty("b", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.Make")]
    public sealed class ArrayMakeVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.Make", "Make Array", "Array", "Creates an array from up to eight item inputs.");
            AddValueInput("count", "int", true, "propertyOrConnection");
            AddValueInput("item0", null, false, "propertyOrConnection");
            AddValueInput("item1", null, false, "propertyOrConnection");
            AddValueInput("item2", null, false, "propertyOrConnection");
            AddValueInput("item3", null, false, "propertyOrConnection");
            AddValueInput("item4", null, false, "propertyOrConnection");
            AddValueInput("item5", null, false, "propertyOrConnection");
            AddValueInput("item6", null, false, "propertyOrConnection");
            AddValueInput("item7", null, false, "propertyOrConnection");
            AddValueOutput("array", null);
            AddProperty("count", "int", false, 0);
            AddProperty("item0", null, false, null);
            AddProperty("item1", null, false, null);
            AddProperty("item2", null, false, null);
            AddProperty("item3", null, false, null);
            AddProperty("item4", null, false, null);
            AddProperty("item5", null, false, null);
            AddProperty("item6", null, false, null);
            AddProperty("item7", null, false, null);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.Add")]
    public sealed class ArrayAddVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.Add", "Array Add", "Array", "Returns a copy of array with item appended.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("item", null, false, "propertyOrConnection");
            AddValueOutput("array", null);
            AddValueOutput("index", "int");
            AddProperty("item", null, false, null);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.AddUnique")]
    public sealed class ArrayAddUniqueVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.AddUnique", "Array Add Unique", "Array", "Returns a copy of array with item appended only when it is not already present.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("item", null, false, "propertyOrConnection");
            AddValueOutput("array", null);
            AddValueOutput("index", "int");
            AddValueOutput("added", "bool");
            AddProperty("item", null, false, null);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.Insert")]
    public sealed class ArrayInsertVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.Insert", "Array Insert", "Array", "Returns a copy of array with item inserted at index.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("index", "int", true, "propertyOrConnection");
            AddValueInput("item", null, false, "propertyOrConnection");
            AddValueOutput("array", null);
            AddProperty("index", "int", false, 0);
            AddProperty("item", null, false, null);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.RemoveIndex")]
    public sealed class ArrayRemoveIndexVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.RemoveIndex", "Array Remove Index", "Array", "Returns a copy of array with the item at index removed.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("index", "int", true, "propertyOrConnection");
            AddValueOutput("array", null);
            AddValueOutput("removed", "bool");
            AddProperty("index", "int", false, 0);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.RemoveItem")]
    public sealed class ArrayRemoveItemVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.RemoveItem", "Array Remove Item", "Array", "Returns a copy of array with the first matching item removed.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("item", null, false, "propertyOrConnection");
            AddValueOutput("array", null);
            AddValueOutput("removed", "bool");
            AddProperty("item", null, false, null);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.Clear")]
    public sealed class ArrayClearVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.Clear", "Array Clear", "Array", "Returns an empty array.");
            AddValueInput("array", null, false, "connection");
            AddValueOutput("array", null);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.Resize")]
    public sealed class ArrayResizeVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.Resize", "Array Resize", "Array", "Returns a copy resized to size, padding new entries with fillValue.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("size", "int", true, "propertyOrConnection");
            AddValueInput("fillValue", null, false, "propertyOrConnection");
            AddValueOutput("array", null);
            AddProperty("size", "int", false, 0);
            AddProperty("fillValue", null, false, null);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.SetElement")]
    public sealed class ArraySetElementVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.SetElement", "Array Set Element", "Array", "Returns a copy with item assigned at index.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("index", "int", true, "propertyOrConnection");
            AddValueInput("item", null, false, "propertyOrConnection");
            AddValueInput("sizeToFit", "bool", false, "propertyOrConnection");
            AddValueOutput("array", null);
            AddValueOutput("success", "bool");
            AddProperty("index", "int", false, 0);
            AddProperty("item", null, false, null);
            AddProperty("sizeToFit", "bool", false, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.Append")]
    public sealed class ArrayAppendVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.Append", "Array Append", "Array", "Returns a copy of array with another array appended.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("other", null, true, "connection");
            AddValueOutput("array", null);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.RandomItem")]
    public sealed class ArrayRandomItemVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.RandomItem", "Array Random Item", "Array", "Returns a random item, its index, and validity flag from an array.");
            AddValueInput("array", null, true, "connection");
            AddValueOutput("item", null);
            AddValueOutput("index", "int");
            AddValueOutput("isValid", "bool");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.Shuffle")]
    public sealed class ArrayShuffleVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.Shuffle", "Array Shuffle", "Array", "Returns a shuffled copy of array.");
            AddValueInput("array", null, true, "connection");
            AddValueOutput("array", null);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.LastIndex")]
    public sealed class ArrayLastIndexVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.LastIndex", "Array Last Index", "Array", "Returns the last valid index of array or -1 when empty.");
            AddValueInput("array", null, true, "connection");
            AddValueOutput("index", "int");
        }
    }

}
