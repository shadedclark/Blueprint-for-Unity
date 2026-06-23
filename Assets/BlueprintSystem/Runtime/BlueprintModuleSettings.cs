using System;

namespace BlueprintSystem
{
    public static class BlueprintModuleSettings
    {
        public const string DisableSmartObjectDefine = "BLUEPRINTSYSTEM_DISABLE_SMARTOBJECT";
        public const string DisableBehaviorTreeDefine = "BLUEPRINTSYSTEM_DISABLE_BEHAVIOR_TREE";

        private const string SmartObjectNodePrefix = "SmartObject.";
        private const string SmartObjectManifestPathFragment = "/SmartObject/Specs/Nodes/";
        private const string BehaviorTreeBlueprintNodePrefix = "BehaviorTree.";
        private const string BehaviorTreeRuntimeNodePrefix = "BT.";
        private const string BehaviorTreeManifestPathFragment = "/Specs/Nodes/BehaviorTree.";
        private const string BehaviorTreeModulePathFragment = "/BehaviorTree/";

        private static bool? smartObjectEnabledOverride;
        private static bool? behaviorTreeEnabledOverride;

        public static bool SmartObjectEnabled
        {
            get
            {
                if (smartObjectEnabledOverride.HasValue)
                {
                    return smartObjectEnabledOverride.Value;
                }

                return IsSmartObjectEnabledByDefine();
            }
        }

        public static bool BehaviorTreeEnabled
        {
            get
            {
                if (behaviorTreeEnabledOverride.HasValue)
                {
                    return behaviorTreeEnabledOverride.Value;
                }

                return IsBehaviorTreeEnabledByDefine();
            }
        }

        public static bool IsNodeTypeEnabled(string typeId)
        {
            if (IsSmartObjectNodeType(typeId))
            {
                return SmartObjectEnabled;
            }

            if (IsBehaviorTreeNodeType(typeId))
            {
                return BehaviorTreeEnabled;
            }

            return true;
        }

        public static bool IsAssetPathEnabled(string path)
        {
            path = BlueprintAssetDiscovery.NormalizeAssetPath(path);
            if (IsSmartObjectManifestPath(path))
            {
                return SmartObjectEnabled;
            }

            if (IsBehaviorTreeAssetPath(path))
            {
                return BehaviorTreeEnabled;
            }

            return true;
        }

        public static IDisposable OverrideSmartObjectEnabledForTests(bool enabled)
        {
            bool? previous = smartObjectEnabledOverride;
            smartObjectEnabledOverride = enabled;
            return new SmartObjectModuleOverrideScope(previous);
        }

        public static IDisposable OverrideBehaviorTreeEnabledForTests(bool enabled)
        {
            bool? previous = behaviorTreeEnabledOverride;
            behaviorTreeEnabledOverride = enabled;
            return new BehaviorTreeModuleOverrideScope(previous);
        }

        private static bool IsSmartObjectNodeType(string typeId)
        {
            return !string.IsNullOrEmpty(typeId) &&
                   typeId.StartsWith(SmartObjectNodePrefix, StringComparison.Ordinal);
        }

        private static bool IsBehaviorTreeNodeType(string typeId)
        {
            return !string.IsNullOrEmpty(typeId) &&
                   (typeId.StartsWith(BehaviorTreeBlueprintNodePrefix, StringComparison.Ordinal) ||
                    typeId.StartsWith(BehaviorTreeRuntimeNodePrefix, StringComparison.Ordinal));
        }

        private static bool IsSmartObjectManifestPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.IndexOf(SmartObjectManifestPathFragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsBehaviorTreeAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (path.IndexOf(BehaviorTreeManifestPathFragment, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf(BehaviorTreeModulePathFragment, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsSmartObjectEnabledByDefine()
        {
#if BLUEPRINTSYSTEM_DISABLE_SMARTOBJECT
            return false;
#else
            return true;
#endif
        }

        private static bool IsBehaviorTreeEnabledByDefine()
        {
#if BLUEPRINTSYSTEM_DISABLE_BEHAVIOR_TREE
            return false;
#else
            return true;
#endif
        }

        private sealed class SmartObjectModuleOverrideScope : IDisposable
        {
            private readonly bool? previous;
            private bool disposed;

            public SmartObjectModuleOverrideScope(bool? previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                smartObjectEnabledOverride = previous;
                disposed = true;
            }
        }

        private sealed class BehaviorTreeModuleOverrideScope : IDisposable
        {
            private readonly bool? previous;
            private bool disposed;

            public BehaviorTreeModuleOverrideScope(bool? previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                behaviorTreeEnabledOverride = previous;
                disposed = true;
            }
        }
    }
}
