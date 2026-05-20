using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace BlueprintSystem.Editor
{
    [Graph(AssetExtension)]
    [Serializable]
    public sealed class BlueprintVisualGraph : Graph
    {
        public const string AssetExtension = "bpgraph";

        public string SourceBlueprintAssetPath;
        public string SchemaVersion = "0.1";
        public string BlueprintName;
        public string Category;
        public string Description;
        public List<BlueprintVisualVariableData> Variables = new List<BlueprintVisualVariableData>();
        public List<BlueprintVisualBindingData> Bindings = new List<BlueprintVisualBindingData>();
        public List<BlueprintVisualComponentData> Components = new List<BlueprintVisualComponentData>();

        [MenuItem("Assets/Create/Blueprint System/Blueprint Visual Graph", false, 2110)]
        private static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<BlueprintVisualGraph>("New Blueprint Graph");
        }

        public override void OnEnable()
        {
            base.OnEnable();
            if (BlueprintGraphToolkitBlackboardSync.SyncVariablesToBlackboard(this))
            {
                BlueprintGraphToolkitReflection.MarkDirty(this);
            }
        }

        public override void OnGraphChanged(GraphLogger graphLogger)
        {
            List<BlueprintVisualNode> nodes = GetNodes().OfType<BlueprintVisualNode>().ToList();
            HashSet<string> nodeIds = new HashSet<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                BlueprintVisualNode node = nodes[i];
                string nodeId = node.ReadNodeId();
                if (string.IsNullOrEmpty(nodeId))
                {
                    graphLogger.LogWarning("Blueprint node is missing an id.", node);
                    continue;
                }

                if (!nodeIds.Add(nodeId))
                {
                    graphLogger.LogError("Duplicate blueprint node id '" + nodeId + "'.", node);
                }
            }
        }
    }
}
