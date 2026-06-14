using System;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Resource.LoadAsync")]
    public sealed class ResourceLoadAsyncVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Resource.LoadAsync", "Load Resource Async", "Resource", "Loads a Resource Blueprint primary asset asynchronously.");
            AddExecInput("execIn");
            AddValueInput("resourceType", "string", true, "propertyOrConnection", "Type");
            AddValueInput("resourceName", "string", true, "propertyOrConnection", "Name");
            AddValueInput("scope", "BlueprintResourceScope", false, "propertyOrConnection");
            AddExecOutput("loaded");
            AddExecOutput("failed");
            AddExecOutput("cancelled");
            AddValueOutput("asset", null);
            AddValueOutput("state", "BlueprintResourceLoadState");
            AddValueOutput("error", "string");
            AddProperty("resourceType", "string", true, null, "Type");
            AddProperty("resourceName", "string", true, null, "Name");
            AddProperty("scope", "BlueprintResourceScope", false, "Manual");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Resource.PreloadGroupAsync")]
    public sealed class ResourcePreloadGroupAsyncVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Resource.PreloadGroupAsync", "Preload Resource Group Async", "Resource", "Preloads a named Resource Blueprint group asynchronously.");
            AddExecInput("execIn");
            AddValueInput("preloadGroup", "string", true, "propertyOrConnection", "Group");
            AddValueInput("scope", "BlueprintResourceScope", false, "propertyOrConnection");
            AddExecOutput("completed");
            AddExecOutput("failed");
            AddValueOutput("state", "BlueprintResourceLoadState");
            AddValueOutput("error", "string");
            AddProperty("preloadGroup", "string", true, null, "Group");
            AddProperty("scope", "BlueprintResourceScope", false, "Manual");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Resource.Release")]
    public sealed class ResourceReleaseVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Resource.Release", "Release Resource", "Resource", "Releases one resource reference or all references in a scope.");
            AddExecInput("execIn");
            AddValueInput("resourceType", "string", false, "propertyOrConnection", "Type");
            AddValueInput("resourceName", "string", false, "propertyOrConnection", "Name");
            AddValueInput("scope", "BlueprintResourceScope", false, "propertyOrConnection");
            AddValueInput("releaseScope", "bool", false, "propertyOrConnection", "Release Scope");
            AddExecOutput("execOut");
            AddProperty("resourceType", "string", false, null, "Type");
            AddProperty("resourceName", "string", false, null, "Name");
            AddProperty("scope", "BlueprintResourceScope", false, "Manual");
            AddProperty("releaseScope", "bool", false, false, "Release Scope");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Resource.GetLoadState")]
    public sealed class ResourceGetLoadStateVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Resource.GetLoadState", "Get Resource Load State", "Resource", "Returns Resource Manager state for a primary resource id.");
            AddValueInput("resourceType", "string", true, "propertyOrConnection", "Type");
            AddValueInput("resourceName", "string", true, "propertyOrConnection", "Name");
            AddValueOutput("state", "BlueprintResourceLoadState");
            AddValueOutput("loaded", null);
            AddValueOutput("error", "string");
            AddProperty("resourceType", "string", true, null, "Type");
            AddProperty("resourceName", "string", true, null, "Name");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Resource.GetMetadata")]
    public sealed class ResourceGetMetadataVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Resource.GetMetadata", "Get Resource Metadata", "Resource", "Reads metadata from the generated Resource Blueprint registry.");
            AddValueInput("resourceType", "string", true, "propertyOrConnection", "Type");
            AddValueInput("resourceName", "string", true, "propertyOrConnection", "Name");
            AddValueInput("key", "string", false, "propertyOrConnection");
            AddValueOutput("value", "string");
            AddProperty("resourceType", "string", true, null, "Type");
            AddProperty("resourceName", "string", true, null, "Name");
            AddProperty("key", "string", false, string.Empty);
        }
    }
}
