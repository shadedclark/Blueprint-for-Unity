using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace BlueprintSystem.Editor
{
    [Graph(AssetExtension)]
    [Serializable]
    public sealed class BlueprintResourceVisualGraph : Graph
    {
        public const string AssetExtension = "resourcebpgraph";

        public string SourceResourceBlueprintAssetPath;
        public string SchemaVersion = "0.1";
        [BlueprintResourceTypePopup]
        public string ResourceType;
        public string ResourceName;
        public string DisplayName;
        public string Description;
        public List<string> Tags = new List<string>();
        public BlueprintResourceAssetReference MainAsset = new BlueprintResourceAssetReference();
        public List<BlueprintResourceDependency> Dependencies = new List<BlueprintResourceDependency>();
        public List<string> PreloadGroups = new List<string>();
        public int Priority;
        public float MemoryBudgetMb;
        public string RemoteCatalog;
        public string ContentVersion;
        public List<BlueprintResourceMetadataField> Metadata = new List<BlueprintResourceMetadataField>();

        [MenuItem("Assets/Create/Blueprint System/Resource Blueprint Graph", false, 2112)]
        private static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<BlueprintResourceVisualGraph>("New Resource Blueprint Graph");
        }

        public override void OnEnable()
        {
            base.OnEnable();
            if (BlueprintResourceGraphToolkitBlackboardSync.EnsureResourceBlackboard(this))
            {
                BlueprintGraphToolkitReflection.MarkDirty(this);
            }
        }
    }
}
