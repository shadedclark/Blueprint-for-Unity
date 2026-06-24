using System;

namespace BlueprintSystem
{
    public static class BlueprintModuleSettings
    {
        public const string DisableSmartObjectDefine = "BLUEPRINTSYSTEM_DISABLE_SMARTOBJECT";
        public const string DisableBehaviorTreeDefine = "BLUEPRINTSYSTEM_DISABLE_BEHAVIOR_TREE";
        public const string DisableVehicleRoadsDefine = "BLUEPRINTSYSTEM_DISABLE_VEHICLE_ROADS";

        private const string SmartObjectNodePrefix = "SmartObject.";
        private const string SmartObjectManifestPathFragment = "/SmartObject/Specs/Nodes/";
        private const string BehaviorTreeBlueprintNodePrefix = "BehaviorTree.";
        private const string BehaviorTreeRuntimeNodePrefix = "BT.";
        private const string BehaviorTreeManifestPathFragment = "/Specs/Nodes/BehaviorTree.";
        private const string BehaviorTreeModulePathFragment = "/BehaviorTree/";
        private const string VehicleRoadsNodePrefix = "VehicleRoad.";
        private const string VehicleRoadsBehaviorTreeNodePrefix = "BT.VehicleRoad.";
        private const string VehicleRoadsModulePathFragment = "/VehicleRoads/";

        private static bool? smartObjectEnabledOverride;
        private static bool? behaviorTreeEnabledOverride;
        private static bool? vehicleRoadsEnabledOverride;

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

        public static bool VehicleRoadsEnabled
        {
            get
            {
                if (vehicleRoadsEnabledOverride.HasValue)
                {
                    return vehicleRoadsEnabledOverride.Value;
                }

                return IsVehicleRoadsEnabledByDefine();
            }
        }

        public static bool IsNodeTypeEnabled(string typeId)
        {
            if (IsVehicleRoadsNodeType(typeId))
            {
                return VehicleRoadsEnabled &&
                       (!IsBehaviorTreeNodeType(typeId) || BehaviorTreeEnabled);
            }

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
            if (IsVehicleRoadsAssetPath(path))
            {
                return VehicleRoadsEnabled;
            }

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

        public static IDisposable OverrideVehicleRoadsEnabledForTests(bool enabled)
        {
            bool? previous = vehicleRoadsEnabledOverride;
            vehicleRoadsEnabledOverride = enabled;
            return new VehicleRoadsModuleOverrideScope(previous);
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

        private static bool IsVehicleRoadsNodeType(string typeId)
        {
            return !string.IsNullOrEmpty(typeId) &&
                   (typeId.StartsWith(VehicleRoadsNodePrefix, StringComparison.Ordinal) ||
                    typeId.StartsWith(VehicleRoadsBehaviorTreeNodePrefix, StringComparison.Ordinal));
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

        private static bool IsVehicleRoadsAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.IndexOf(VehicleRoadsModulePathFragment, StringComparison.OrdinalIgnoreCase) >= 0;
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

        private static bool IsVehicleRoadsEnabledByDefine()
        {
#if BLUEPRINTSYSTEM_DISABLE_VEHICLE_ROADS
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

        private sealed class VehicleRoadsModuleOverrideScope : IDisposable
        {
            private readonly bool? previous;
            private bool disposed;

            public VehicleRoadsModuleOverrideScope(bool? previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                vehicleRoadsEnabledOverride = previous;
                disposed = true;
            }
        }
    }
}
