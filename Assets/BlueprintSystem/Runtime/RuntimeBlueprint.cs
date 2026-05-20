using System.Collections.Generic;

namespace BlueprintSystem
{
    public sealed class RuntimeBlueprint
    {
        public string Name;
        public readonly Dictionary<string, RuntimeNode> NodesById = new Dictionary<string, RuntimeNode>();
        public readonly Dictionary<BlueprintPortKey, List<RuntimeEdge>> ExecOutputs = new Dictionary<BlueprintPortKey, List<RuntimeEdge>>();
        public readonly Dictionary<BlueprintPortKey, RuntimeEdge> ValueInputs = new Dictionary<BlueprintPortKey, RuntimeEdge>();
        public readonly Dictionary<string, string> EventEntries = new Dictionary<string, string>();
        public readonly List<BlueprintVariableDeclaration> Variables = new List<BlueprintVariableDeclaration>();
        public readonly List<BlueprintBindingDeclaration> Bindings = new List<BlueprintBindingDeclaration>();
        public readonly List<BlueprintComponentDeclaration> Components = new List<BlueprintComponentDeclaration>();

        public RuntimeNode GetNode(string nodeId)
        {
            RuntimeNode node;
            return NodesById.TryGetValue(nodeId, out node) ? node : null;
        }

        public List<RuntimeEdge> GetExecEdges(BlueprintPortKey output)
        {
            List<RuntimeEdge> edges;
            return ExecOutputs.TryGetValue(output, out edges) ? edges : null;
        }
    }

    public sealed class RuntimeNode
    {
        public string Id;
        public string TypeId;
        public BlueprintNodeManifest Manifest;
        public IBlueprintNodeExecutor Executor;
        public readonly Dictionary<string, object> Properties = new Dictionary<string, object>();

        public object GetProperty(string propertyId)
        {
            object value;
            return Properties.TryGetValue(propertyId, out value) ? value : null;
        }
    }
}
