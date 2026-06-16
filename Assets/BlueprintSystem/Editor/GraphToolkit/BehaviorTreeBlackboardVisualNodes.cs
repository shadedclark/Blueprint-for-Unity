using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    public abstract class BehaviorTreeBlackboardGetVisualNode : BlueprintVisualNode
    {
        protected void ConfigureGet(string typeId, string title, string valueType, string description)
        {
            SetIdentity(typeId, title, "BehaviorTree/Blackboard", description);
            AddValueInput("target", "Binding<BehaviorTreeRunner>", true, "property");
            AddValueInput("key", "string", true, "propertyOrConnection");
            AddValueOutput("value", valueType);
            AddValueOutput("success", "bool");
            AddProperty("target", "Binding<BehaviorTreeRunner>", true);
            AddProperty("key", "string", false);
        }
    }

    public abstract class BehaviorTreeBlackboardSetVisualNode : BlueprintVisualNode
    {
        protected void ConfigureSet(string typeId, string title, string valueType, object defaultValue, string description)
        {
            SetIdentity(typeId, title, "BehaviorTree/Blackboard", description);
            AddExecInput("execIn");
            AddValueInput("target", "Binding<BehaviorTreeRunner>", true, "property");
            AddValueInput("key", "string", true, "propertyOrConnection");
            AddValueInput("value", valueType, true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<BehaviorTreeRunner>", true);
            AddProperty("key", "string", false);
            AddProperty("value", valueType, false, defaultValue);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.GetBlackboardBool")]
    public sealed class BehaviorTreeGetBlackboardBoolVisualNode : BehaviorTreeBlackboardGetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGet("BehaviorTree.GetBlackboardBool", "Get Blackboard Bool", "bool", "Reads a bool value from a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.GetBlackboardInt")]
    public sealed class BehaviorTreeGetBlackboardIntVisualNode : BehaviorTreeBlackboardGetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGet("BehaviorTree.GetBlackboardInt", "Get Blackboard Int", "int", "Reads an int value from a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.GetBlackboardFloat")]
    public sealed class BehaviorTreeGetBlackboardFloatVisualNode : BehaviorTreeBlackboardGetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGet("BehaviorTree.GetBlackboardFloat", "Get Blackboard Float", "float", "Reads a float value from a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.GetBlackboardString")]
    public sealed class BehaviorTreeGetBlackboardStringVisualNode : BehaviorTreeBlackboardGetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGet("BehaviorTree.GetBlackboardString", "Get Blackboard String", "string", "Reads a string value from a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.GetBlackboardVector3")]
    public sealed class BehaviorTreeGetBlackboardVector3VisualNode : BehaviorTreeBlackboardGetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGet("BehaviorTree.GetBlackboardVector3", "Get Blackboard Vector3", "Vector3", "Reads a Vector3 value from a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.GetBlackboardGameObject")]
    public sealed class BehaviorTreeGetBlackboardGameObjectVisualNode : BehaviorTreeBlackboardGetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGet("BehaviorTree.GetBlackboardGameObject", "Get Blackboard GameObject", "GameObject", "Reads a GameObject value from a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.SetBlackboardBool")]
    public sealed class BehaviorTreeSetBlackboardBoolVisualNode : BehaviorTreeBlackboardSetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureSet("BehaviorTree.SetBlackboardBool", "Set Blackboard Bool", "bool", false, "Writes a bool value to a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.SetBlackboardInt")]
    public sealed class BehaviorTreeSetBlackboardIntVisualNode : BehaviorTreeBlackboardSetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureSet("BehaviorTree.SetBlackboardInt", "Set Blackboard Int", "int", 0, "Writes an int value to a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.SetBlackboardFloat")]
    public sealed class BehaviorTreeSetBlackboardFloatVisualNode : BehaviorTreeBlackboardSetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureSet("BehaviorTree.SetBlackboardFloat", "Set Blackboard Float", "float", 0f, "Writes a float value to a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.SetBlackboardString")]
    public sealed class BehaviorTreeSetBlackboardStringVisualNode : BehaviorTreeBlackboardSetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureSet("BehaviorTree.SetBlackboardString", "Set Blackboard String", "string", string.Empty, "Writes a string value to a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.SetBlackboardVector3")]
    public sealed class BehaviorTreeSetBlackboardVector3VisualNode : BehaviorTreeBlackboardSetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureSet("BehaviorTree.SetBlackboardVector3", "Set Blackboard Vector3", "Vector3", new List<object> { 0f, 0f, 0f }, "Writes a Vector3 value to a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.SetBlackboardGameObject")]
    public sealed class BehaviorTreeSetBlackboardGameObjectVisualNode : BehaviorTreeBlackboardSetVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureSet("BehaviorTree.SetBlackboardGameObject", "Set Blackboard GameObject", "Binding<GameObject>", null, "Writes a GameObject value to a bound BehaviorTreeRunner Blackboard.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("BehaviorTree.ClearBlackboard")]
    public sealed class BehaviorTreeClearBlackboardVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BehaviorTree.ClearBlackboard", "Clear Blackboard", "BehaviorTree/Blackboard", "Clears one key on a bound BehaviorTreeRunner Blackboard.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<BehaviorTreeRunner>", true, "property");
            AddValueInput("key", "string", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<BehaviorTreeRunner>", true);
            AddProperty("key", "string", false);
        }
    }
}
