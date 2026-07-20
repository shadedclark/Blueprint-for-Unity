using System;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Persistence.Save")]
    public sealed class PersistenceSaveVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Persistence.Save", "Save Persistent Variables", "Persistence", "Immediately saves persistent variables for the current BlueprintRunner.");
            AddExecInput("execIn");
            AddValueInput("slot", "string", false, "propertyOrConnection");
            AddExecOutput("saved");
            AddExecOutput("failed");
            AddValueOutput("error", "string");
            AddProperty("slot", "string", false, string.Empty);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Persistence.Load")]
    public sealed class PersistenceLoadVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Persistence.Load", "Load Persistent Variables", "Persistence", "Loads persistent variables and refreshes reactive bindings.");
            AddExecInput("execIn");
            AddValueInput("slot", "string", false, "propertyOrConnection");
            AddExecOutput("loaded");
            AddExecOutput("missing");
            AddExecOutput("failed");
            AddValueOutput("error", "string");
            AddProperty("slot", "string", false, string.Empty);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Persistence.Delete")]
    public sealed class PersistenceDeleteVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Persistence.Delete", "Delete Persistent Variables", "Persistence", "Deletes saved persistent variables for the current BlueprintRunner.");
            AddExecInput("execIn");
            AddValueInput("slot", "string", false, "propertyOrConnection");
            AddExecOutput("deleted");
            AddExecOutput("missing");
            AddExecOutput("failed");
            AddValueOutput("error", "string");
            AddProperty("slot", "string", false, string.Empty);
        }
    }
}
