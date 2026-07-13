using System;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Blueprint.IsValid")]
    public sealed class BlueprintIsValidVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Blueprint.IsValid", "Is Blueprint Valid", "Blueprint", "Returns true when a Blueprint asset path or BlueprintRef resolves inside the current Blueprint instance tree.");
            AddValueInput("target", BlueprintVariableTypeRegistry.BlueprintAssetTypeId, true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("target", BlueprintVariableTypeRegistry.BlueprintAssetTypeId, false, null, null, true);
        }

        protected override void ApplyDefaultMetadata()
        {
            SetPropertyInspectorOnly("target", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Blueprint.GetOwner")]
    public sealed class BlueprintGetOwnerVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Blueprint.GetOwner", "Get Blueprint Owner", "Blueprint", "Returns the owner BlueprintRef for the current component or a supplied BlueprintRef target.");
            AddValueInput("target", BlueprintVariableTypeRegistry.BlueprintRefTypeId, false, "connection");
            AddValueOutput("target", BlueprintVariableTypeRegistry.BlueprintRefTypeId);
            AddValueOutput("isValid", "bool");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Blueprint.GetComponent")]
    public sealed class BlueprintGetComponentVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Blueprint.GetComponent", "Get Blueprint Component", "Blueprint", "Finds a named runtime Blueprint component from the current instance or supplied BlueprintRef, walking owner instances outward.");
            AddValueInput("target", BlueprintVariableTypeRegistry.BlueprintRefTypeId, false, "connection");
            AddValueInput("name", "string", true, "propertyOrConnection");
            AddValueOutput("target", BlueprintVariableTypeRegistry.BlueprintRefTypeId);
            AddValueOutput("isValid", "bool");
            AddProperty("name", "string", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Blueprint.TriggerEvent")]
    public sealed class BlueprintTriggerEventVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Blueprint.TriggerEvent", "Trigger Blueprint Event", "Blueprint", "Triggers a named event on a Blueprint component resolved by asset path or BlueprintRef.");
            AddExecInput("execIn");
            AddValueInput("target", BlueprintVariableTypeRegistry.BlueprintAssetTypeId, true, "propertyOrConnection");
            AddValueInput("eventName", "string", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", BlueprintVariableTypeRegistry.BlueprintAssetTypeId, false, null, null, true);
            AddProperty("eventName", "string", false);
        }

        protected override void ApplyDefaultMetadata()
        {
            SetPropertyInspectorOnly("target", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Blueprint.TriggerEventFromGameObject")]
    public sealed class BlueprintTriggerEventFromGameObjectVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Blueprint.TriggerEventFromGameObject", "Trigger Event From GameObject", "Blueprint", "Triggers a named event on the BlueprintRunner attached to a target GameObject.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<GameObject>", true, "propertyOrConnection");
            AddValueInput("eventName", "string", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<GameObject>", false);
            AddProperty("eventName", "string", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Blueprint.GetVariable")]
    public sealed class BlueprintGetVariableVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Blueprint.GetVariable", "Get Blueprint Variable", "Blueprint", "Reads an exposed variable from a Blueprint component resolved by asset path or BlueprintRef.");
            AddValueInput("target", BlueprintVariableTypeRegistry.BlueprintAssetTypeId, true, "propertyOrConnection");
            AddValueInput("name", "string", true, "propertyOrConnection");
            AddValueOutput("value", null);
            AddValueOutput("success", "bool");
            AddProperty("target", BlueprintVariableTypeRegistry.BlueprintAssetTypeId, false, null, null, true);
            AddProperty("name", "string", false);
        }

        protected override void ApplyDefaultMetadata()
        {
            SetPropertyInspectorOnly("target", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Blueprint.SetVariable")]
    public sealed class BlueprintSetVariableVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Blueprint.SetVariable", "Set Blueprint Variable", "Blueprint", "Writes an exposed variable on a Blueprint component resolved by asset path or BlueprintRef.");
            AddExecInput("execIn");
            AddValueInput("target", BlueprintVariableTypeRegistry.BlueprintAssetTypeId, true, "propertyOrConnection");
            AddValueInput("name", "string", true, "propertyOrConnection");
            AddValueInput("value", null, true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", BlueprintVariableTypeRegistry.BlueprintAssetTypeId, false, null, null, true);
            AddProperty("name", "string", false);
            AddProperty("value", null, false);
        }

        protected override void ApplyDefaultMetadata()
        {
            SetPropertyInspectorOnly("target", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Blueprint.GetVariableFromGameObject")]
    public sealed class BlueprintGetVariableFromGameObjectVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Blueprint.GetVariableFromGameObject", "Get Variable From GameObject", "Blueprint", "Reads an exposed variable from the BlueprintRunner on a target GameObject.");
            AddValueInput("target", "Binding<GameObject>", true, "propertyOrConnection");
            AddValueInput("name", "string", true, "propertyOrConnection");
            AddValueOutput("value", null);
            AddValueOutput("success", "bool");
            AddProperty("target", "Binding<GameObject>", false);
            AddProperty("name", "string", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Blueprint.SetVariableFromGameObject")]
    public sealed class BlueprintSetVariableFromGameObjectVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Blueprint.SetVariableFromGameObject", "Set Variable From GameObject", "Blueprint", "Writes an exposed variable on the BlueprintRunner on a target GameObject.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<GameObject>", true, "propertyOrConnection");
            AddValueInput("name", "string", true, "propertyOrConnection");
            AddValueInput("value", null, true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<GameObject>", false);
            AddProperty("name", "string", false);
            AddProperty("value", null, false);
        }
    }
}
