using System;
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
        public CompiledBlueprintTarget CompiledTarget;
        public readonly Dictionary<string, object> Properties = new Dictionary<string, object>();

        public object GetProperty(string propertyId)
        {
            object value;
            return Properties.TryGetValue(propertyId, out value) ? value : null;
        }
    }

    [Serializable]
    public sealed class CompiledBlueprintTarget
    {
        public int OwnerTraversal = -1;
        public int ComponentIndex = -1;
        public List<int> ComponentIndexPath = new List<int>();
        public string ExpectedSourceGuid;
        public string SourcePath;

        [NonSerialized] internal int RuntimeVersion = -1;
        [NonSerialized] internal int RuntimeRecordIndex = -1;

        public int BoundRuntimeVersion
        {
            get { return RuntimeVersion; }
        }

        public int BoundRuntimeRecordIndex
        {
            get { return RuntimeRecordIndex; }
        }

        internal void SetRuntimeHandle(int runtimeVersion, int runtimeRecordIndex)
        {
            RuntimeVersion = runtimeVersion;
            RuntimeRecordIndex = runtimeRecordIndex;
        }

        internal void ClearRuntimeHandle()
        {
            RuntimeVersion = -1;
            RuntimeRecordIndex = -1;
        }
    }

    internal sealed class ComponentRuntimeRecord
    {
        public int RecordIndex;
        public int OwnerRecordIndex;
        public int ComponentIndex;
        public string SourceGuid;
        public string SourcePath;
        public IBlueprintInstance Instance;
    }

    internal interface IBlueprintTargetHandleResolver
    {
        bool TryResolveBlueprintTarget(
            IBlueprintInstance requester,
            CompiledBlueprintTarget compiledTarget,
            string targetPath,
            out IBlueprintInstance instance,
            out bool ambiguous);
    }
}
