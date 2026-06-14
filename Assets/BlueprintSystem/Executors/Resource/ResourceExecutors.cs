using System;
using UnityEngine;

namespace BlueprintSystem
{
    internal static class ResourceExecutorUtility
    {
        public static BlueprintPrimaryResourceId ReadId(BlueprintExecutionContext context, RuntimeNode node)
        {
            return new BlueprintPrimaryResourceId(
                context.GetInputValue(node, "resourceType", string.Empty),
                context.GetInputValue(node, "resourceName", string.Empty));
        }

        public static BlueprintResourceScope ReadScope(BlueprintExecutionContext context, RuntimeNode node)
        {
            return context.GetInputValue(node, "scope", BlueprintResourceScope.Manual);
        }

        public static string StateKey(RuntimeNode node, string value)
        {
            return "resource:" + node.Id + ":" + value;
        }

        public static void StoreLoadResult(BlueprintExecutionContext context, RuntimeNode node, BlueprintResourceLoadHandle handle)
        {
            if (context == null || node == null || handle == null)
            {
                return;
            }

            context.SetState(StateKey(node, "asset"), handle.Asset);
            context.SetState(StateKey(node, "state"), handle.State);
            context.SetState(StateKey(node, "error"), handle.Error ?? string.Empty);
        }
    }

    public sealed class ResourceLoadAsyncExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Resource.LoadAsync"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            BlueprintPrimaryResourceId id = ResourceExecutorUtility.ReadId(context, node);
            if (!id.IsValid)
            {
                return BlueprintExecResult.Error("Resource.LoadAsync node '" + node.Id + "' has no valid resource id.");
            }

            BlueprintResourceScope scope = ResourceExecutorUtility.ReadScope(context, node);
            BlueprintResourceManager.Instance.LoadAsync(id, scope, delegate(BlueprintResourceLoadHandle handle)
            {
                ResourceExecutorUtility.StoreLoadResult(context, node, handle);
                if (handle.State == BlueprintResourceLoadState.Loaded)
                {
                    context.ExecuteFromOutput(node, "loaded");
                }
                else if (handle.State == BlueprintResourceLoadState.Cancelled)
                {
                    context.ExecuteFromOutput(node, "cancelled");
                }
                else
                {
                    context.ExecuteFromOutput(node, "failed");
                }
            });

            return BlueprintExecResult.Stop();
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            object value;
            if (outputPortId == "asset" && context.TryGetState(ResourceExecutorUtility.StateKey(node, "asset"), out value))
            {
                return value;
            }

            if (outputPortId == "state" && context.TryGetState(ResourceExecutorUtility.StateKey(node, "state"), out value))
            {
                return value;
            }

            if (outputPortId == "error" && context.TryGetState(ResourceExecutorUtility.StateKey(node, "error"), out value))
            {
                return value;
            }

            if (outputPortId == "state")
            {
                return BlueprintResourceLoadState.Unloaded;
            }

            if (outputPortId == "error")
            {
                return string.Empty;
            }

            return null;
        }
    }

    public sealed class ResourcePreloadGroupAsyncExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Resource.PreloadGroupAsync"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string preloadGroup = context.GetInputValue(node, "preloadGroup", string.Empty);
            if (string.IsNullOrEmpty(preloadGroup))
            {
                return BlueprintExecResult.Error("Resource.PreloadGroupAsync node '" + node.Id + "' has no preloadGroup.");
            }

            BlueprintResourceScope scope = ResourceExecutorUtility.ReadScope(context, node);
            BlueprintResourceManager.Instance.PreloadGroupAsync(preloadGroup, scope, delegate(BlueprintResourceGroupLoadHandle handle)
            {
                context.SetState(ResourceExecutorUtility.StateKey(node, "error"), handle.Error ?? string.Empty);
                context.SetState(ResourceExecutorUtility.StateKey(node, "state"), handle.Succeeded
                    ? BlueprintResourceLoadState.Loaded
                    : BlueprintResourceLoadState.Failed);
                context.ExecuteFromOutput(node, handle.Succeeded ? "completed" : "failed");
            });

            return BlueprintExecResult.Stop();
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            object value;
            if (outputPortId == "state" && context.TryGetState(ResourceExecutorUtility.StateKey(node, "state"), out value))
            {
                return value;
            }

            if (outputPortId == "error" && context.TryGetState(ResourceExecutorUtility.StateKey(node, "error"), out value))
            {
                return value;
            }

            return outputPortId == "error" ? string.Empty : (object)BlueprintResourceLoadState.Unloaded;
        }
    }

    public sealed class ResourceReleaseExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Resource.Release"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            bool releaseScope = context.GetInputValue(node, "releaseScope", false);
            BlueprintResourceScope scope = ResourceExecutorUtility.ReadScope(context, node);
            if (releaseScope)
            {
                BlueprintResourceManager.Instance.ReleaseScope(scope);
                return BlueprintExecResult.Continue("execOut");
            }

            BlueprintPrimaryResourceId id = ResourceExecutorUtility.ReadId(context, node);
            if (!id.IsValid)
            {
                return BlueprintExecResult.Error("Resource.Release node '" + node.Id + "' has no valid resource id.");
            }

            BlueprintResourceManager.Instance.Release(id);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class ResourceGetLoadStateExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Resource.GetLoadState"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            BlueprintPrimaryResourceId id = ResourceExecutorUtility.ReadId(context, node);
            if (!id.IsValid)
            {
                return outputPortId == "error" ? "Invalid resource id." : (object)BlueprintResourceLoadState.Failed;
            }

            if (outputPortId == "error")
            {
                return BlueprintResourceManager.Instance.GetLastError(id);
            }

            if (outputPortId == "loaded")
            {
                return BlueprintResourceManager.Instance.GetLoadedAsset(id);
            }

            return BlueprintResourceManager.Instance.GetLoadState(id);
        }
    }

    public sealed class ResourceGetMetadataExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Resource.GetMetadata"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "value")
            {
                return null;
            }

            BlueprintPrimaryResourceId id = ResourceExecutorUtility.ReadId(context, node);
            if (!id.IsValid)
            {
                return string.Empty;
            }

            string key = context.GetInputValue(node, "key", string.Empty);
            return BlueprintResourceManager.Instance.GetMetadata(id, key);
        }
    }
}
