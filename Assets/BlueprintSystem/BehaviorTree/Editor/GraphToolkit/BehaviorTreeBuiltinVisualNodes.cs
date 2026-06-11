using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    internal static class BehaviorTreeVisualNodeMetadata
    {
        public const string Root = "BT.Root";
        public const string Selector = "BT.Selector";
        public const string Sequence = "BT.Sequence";
        public const string Parallel = "BT.Parallel";
        public const string RandomSelector = "BT.RandomSelector";
        public const string PrioritySelector = "BT.PrioritySelector";
        public const string WeightedSelector = "BT.WeightedSelector";
        public const string BlackboardCondition = "BT.BlackboardCondition";
        public const string CompareFloat = "BT.CompareFloat";
        public const string CompareBool = "BT.CompareBool";
        public const string ObjectIsSet = "BT.ObjectIsSet";
        public const string DistanceLessThan = "BT.DistanceLessThan";
        public const string Cooldown = "BT.Cooldown";
        public const string SetRunnerBlackboard = "BT.SetRunnerBlackboard";
        public const string GetRunnerBlackboard = "BT.GetRunnerBlackboard";
        public const string ClearRunnerBlackboard = "BT.ClearRunnerBlackboard";
        public const string CopyRunnerBlackboard = "BT.CopyRunnerBlackboard";
        public const string RunSubtree = "BT.RunSubtree";

        public static BehaviorTreeVisualNode Create(string typeId)
        {
            switch (typeId)
            {
                case Root:
                    return new BTCompositeRootNode();
                case Selector:
                    return new BTCompositeSelectorNode();
                case Sequence:
                    return new BTCompositeSequenceNode();
                case Parallel:
                    return new BTCompositeParallelNode();
                case RandomSelector:
                    return new BTCompositeRandomSelectorNode();
                case PrioritySelector:
                    return new BTCompositePrioritySelectorNode();
                case WeightedSelector:
                    return new BTCompositeWeightedSelectorNode();
                case "BT.Wait":
                    return new BTTaskWaitNode();
                case "BT.SetBlackboard":
                    return new BTTaskSetBlackboardNode();
                case "BT.ClearBlackboard":
                    return new BTTaskClearBlackboardNode();
                case SetRunnerBlackboard:
                    return new BTTaskSetRunnerBlackboardNode();
                case GetRunnerBlackboard:
                    return new BTTaskGetRunnerBlackboardNode();
                case ClearRunnerBlackboard:
                    return new BTTaskClearRunnerBlackboardNode();
                case CopyRunnerBlackboard:
                    return new BTTaskCopyRunnerBlackboardNode();
                case RunSubtree:
                    return new BTTaskRunSubtreeNode();
                case "BT.MoveTo":
                    return new BTTaskMoveToNode();
                case "BT.StopNavigation":
                    return new BTTaskStopNavigationNode();
                case "BT.RotateTo":
                    return new BTTaskRotateToNode();
                case "BT.TriggerBlueprintEvent":
                    return new BTTaskTriggerBlueprintEventNode();
                case "BT.RunBlueprintTask":
                    return new BTTaskRunBlueprintTaskNode();
                case "BT.Log":
                    return new BTTaskLogNode();
                default:
                    return new BehaviorTreeVisualNode();
            }
        }

        public static BehaviorTreeVisualDecoratorNode CreateDecorator(string typeId)
        {
            switch (typeId)
            {
                case BlackboardCondition:
                    return new BTDecoratorBlackboardConditionNode();
                case CompareFloat:
                    return new BTDecoratorCompareFloatNode();
                case CompareBool:
                    return new BTDecoratorCompareBoolNode();
                case ObjectIsSet:
                    return new BTDecoratorObjectIsSetNode();
                case DistanceLessThan:
                    return new BTDecoratorDistanceLessThanNode();
                case Cooldown:
                    return new BTDecoratorCooldownNode();
                default:
                    return new BehaviorTreeVisualDecoratorNode();
            }
        }

        public static bool IsDecorator(string typeId)
        {
            return typeId == BlackboardCondition ||
                   typeId == CompareFloat ||
                   typeId == CompareBool ||
                   typeId == ObjectIsSet ||
                   typeId == DistanceLessThan ||
                   typeId == Cooldown;
        }

        public static string CreateTitle(string typeId)
        {
            switch (typeId)
            {
                case Root:
                    return "Composite: Root";
                case Selector:
                    return "Composite: Selector";
                case Sequence:
                    return "Composite: Sequence";
                case Parallel:
                    return "Composite: Parallel";
                case RandomSelector:
                    return "Composite: Random Selector";
                case PrioritySelector:
                    return "Composite: Priority Selector";
                case WeightedSelector:
                    return "Composite: Weighted Selector";
                case "BT.Wait":
                    return "Task: Wait";
                case "BT.SetBlackboard":
                    return "Task: Set Blackboard";
                case "BT.ClearBlackboard":
                    return "Task: Clear Blackboard";
                case SetRunnerBlackboard:
                    return "Task: Set Runner Blackboard";
                case GetRunnerBlackboard:
                    return "Task: Get Runner Blackboard";
                case ClearRunnerBlackboard:
                    return "Task: Clear Runner Blackboard";
                case CopyRunnerBlackboard:
                    return "Task: Copy Runner Blackboard";
                case RunSubtree:
                    return "Task: Run Subtree";
                case "BT.MoveTo":
                    return "Task: Move To";
                case "BT.StopNavigation":
                    return "Task: Stop Navigation";
                case "BT.RotateTo":
                    return "Task: Rotate To";
                case "BT.TriggerBlueprintEvent":
                    return "Task: Trigger Blueprint Event";
                case "BT.RunBlueprintTask":
                    return "Task: Run Blueprint Task";
                case "BT.Log":
                    return "Task: Log";
                case BlackboardCondition:
                    return "Decorator: Blackboard Condition";
                case CompareFloat:
                    return "Decorator: Compare Float";
                case CompareBool:
                    return "Decorator: Compare Bool";
                case ObjectIsSet:
                    return "Decorator: Object Is Set";
                case DistanceLessThan:
                    return "Decorator: Distance Less Than";
                case Cooldown:
                    return "Decorator: Cooldown";
                case "BT.UpdateDistance":
                    return "Service: Update Distance";
                case "BT.PerceptionSphere":
                    return "Service: Perception Sphere";
                case "BT.PerceptionRaycast":
                    return "Service: Perception Raycast";
                case "BT.SetBlackboardFromBlueprint":
                    return "Service: Set Blackboard From Blueprint";
                case "BT.TriggerBlueprintService":
                    return "Service: Trigger Blueprint Service";
                default:
                    return CreateFallbackTitle(typeId);
            }
        }

        public static bool CanHaveChildren(string typeId)
        {
            return typeId == Root ||
                   typeId == Selector ||
                   typeId == Sequence ||
                   typeId == Parallel ||
                   typeId == RandomSelector ||
                   typeId == PrioritySelector ||
                   typeId == WeightedSelector;
        }

        public static string GetLegacyValueProperty(string typeId, string inputId)
        {
            if (typeId == "BT.Wait" && inputId == "duration")
            {
                return "seconds";
            }

            return null;
        }

        public static string GetLegacyInputBindingProperty(string typeId, string inputId)
        {
            switch (inputId)
            {
                case "key":
                    if (typeId == "BT.SetBlackboard" || typeId == "BT.ClearBlackboard")
                    {
                        return "key";
                    }

                    break;
                case "value":
                    if (typeId == "BT.SetBlackboard")
                    {
                        return "valueKey";
                    }

                    break;
                case "target":
                    if (typeId == "BT.MoveTo" ||
                        typeId == "BT.RotateTo" ||
                        typeId == "BT.TriggerBlueprintEvent" ||
                        typeId == "BT.RunBlueprintTask")
                    {
                        return "targetKey";
                    }

                    break;
                case "complete":
                    if (typeId == "BT.RunBlueprintTask")
                    {
                        return "completeKey";
                    }

                    break;
                case "failure":
                    if (typeId == "BT.RunBlueprintTask")
                    {
                        return "failureKey";
                    }

                    break;
            }

            return null;
        }

        public static bool TryGetDefaultInputValue(string typeId, string inputId, out object value)
        {
            value = null;
            switch (typeId)
            {
                case "BT.Wait":
                    if (inputId == "duration")
                    {
                        value = 0f;
                        return true;
                    }

                    break;
                case "BT.MoveTo":
                    if (inputId == "targetPosition")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 0f);
                        return true;
                    }

                    if (inputId == "acceptableRadius")
                    {
                        value = 0.25f;
                        return true;
                    }

                    if (inputId == "speed")
                    {
                        value = 3f;
                        return true;
                    }

                    if (inputId == "allowTransformFallback" || inputId == "stopOnAbort")
                    {
                        value = true;
                        return true;
                    }

                    break;
                case "BT.StopNavigation":
                    if (inputId == "stopAgent")
                    {
                        value = true;
                        return true;
                    }

                    break;
                case "BT.RotateTo":
                    if (inputId == "targetPosition")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 0f);
                        return true;
                    }

                    if (inputId == "ignoreY")
                    {
                        value = true;
                        return true;
                    }

                    if (inputId == "angleTolerance")
                    {
                        value = 2f;
                        return true;
                    }

                    if (inputId == "rotationSpeed")
                    {
                        value = 360f;
                        return true;
                    }

                    break;
                case "BT.TriggerBlueprintEvent":
                    if (inputId == "successOnMissing")
                    {
                        value = false;
                        return true;
                    }

                    break;
                case "BT.RunBlueprintTask":
                    if (inputId == "timeout")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "complete" || inputId == "failure")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "timeoutStatus")
                    {
                        value = "Failure";
                        return true;
                    }

                    break;
                case BlackboardCondition:
                    if (inputId == "operator")
                    {
                        value = BehaviorTreeComparisonOperator.IsSet;
                        return true;
                    }

                    break;
                case CompareFloat:
                    if (inputId == "left" || inputId == "right")
                    {
                        value = 0f;
                        return true;
                    }

                    if (inputId == "operator")
                    {
                        value = BehaviorTreeComparisonOperator.LessOrEqual;
                        return true;
                    }

                    break;
                case CompareBool:
                    if (inputId == "value")
                    {
                        value = false;
                        return true;
                    }

                    if (inputId == "expected")
                    {
                        value = true;
                        return true;
                    }

                    if (inputId == "operator")
                    {
                        value = BehaviorTreeComparisonOperator.Equals;
                        return true;
                    }

                    break;
                case DistanceLessThan:
                    if (inputId == "sourcePosition" || inputId == "targetPosition")
                    {
                        value = new UnityEngine.Vector3(0f, 0f, 0f);
                        return true;
                    }

                    if (inputId == "distance" || inputId == "maxDistance")
                    {
                        value = 0f;
                        return true;
                    }

                    break;
                case Cooldown:
                    if (inputId == "duration")
                    {
                        value = 0f;
                        return true;
                    }

                    break;
            }

            if (inputId == "eventName" ||
                inputId == "startEventName" ||
                inputId == "abortEventName" ||
                inputId == "message" ||
                inputId == "key")
            {
                value = string.Empty;
                return true;
            }

            return false;
        }

        private static string CreateFallbackTitle(string typeId)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                return "Behavior Tree Node";
            }

            const string prefix = "BT.";
            return typeId.StartsWith(prefix, StringComparison.Ordinal) ? typeId.Substring(prefix.Length) : typeId;
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeRootNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.Root, "Composite: Root", 1);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeSelectorNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.Selector, "Composite: Selector", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeSequenceNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.Sequence, "Composite: Sequence", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeParallelNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.Parallel, "Composite: Parallel", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeRandomSelectorNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.RandomSelector, "Composite: Random Selector", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositePrioritySelectorNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.PrioritySelector, "Composite: Priority Selector", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTCompositeWeightedSelectorNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.WeightedSelector, "Composite: Weighted Selector", 2);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskWaitNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.Wait", "Task: Wait", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("duration", "float", "Duration", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskSetBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.SetBlackboard", "Task: Set Blackboard", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("key", null, "Key", false);
            AddBlackboardInput("value", null, "Value", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskClearBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.ClearBlackboard", "Task: Clear Blackboard", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("key", null, "Key", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskSetRunnerBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.SetRunnerBlackboard, "Task: Set Runner Blackboard", 0);
            PropertiesJson = "{\"sourceKey\":\"\",\"targetKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target Runner", false);
            AddBlackboardInput("value", null, "Value", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskGetRunnerBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.GetRunnerBlackboard, "Task: Get Runner Blackboard", 0);
            PropertiesJson = "{\"sourceKey\":\"\",\"targetKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target Runner", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskClearRunnerBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.ClearRunnerBlackboard, "Task: Clear Runner Blackboard", 0);
            PropertiesJson = "{\"targetKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target Runner", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskCopyRunnerBlackboardNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.CopyRunnerBlackboard, "Task: Copy Runner Blackboard", 0);
            PropertiesJson = "{\"sourceKey\":\"\",\"targetKey\":\"\"}";
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("sourceTarget", null, "Source Runner", false);
            AddBlackboardInput("target", null, "Target Runner", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskRunSubtreeNode : BehaviorTreeVisualNode
    {
        private const string BehaviorTreeOptionName = "behaviorTree";

        public override string ReadPropertiesJson()
        {
            Dictionary<string, object> properties = ReadProperties(base.ReadPropertiesJson());
            string behaviorTreePath;
            if (!TryReadBehaviorTreeOptionPath(out behaviorTreePath))
            {
                behaviorTreePath = ReadBehaviorTreePath(properties);
            }

            if (!string.IsNullOrEmpty(behaviorTreePath) || properties.ContainsKey(BehaviorTreeOptionName))
            {
                properties[BehaviorTreeOptionName] = behaviorTreePath ?? string.Empty;
            }

            return BlueprintJson.Serialize(properties, false);
        }

        protected override void ConfigureDefaultNode()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.RunSubtree, "Task: Run Subtree", 0);
            PropertiesJson = "{\"behaviorTree\":\"\",\"blackboardMode\":\"Shared\",\"inputMappings\":[],\"outputMappings\":[]}";
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            string behaviorTreePath = ReadBehaviorTreePathForDisplay();
            Title = CreateTitle(behaviorTreePath);
            base.OnDefineOptions(context);

            context.AddOption<BehaviorTreeAsset>(BehaviorTreeOptionName)
                .WithDisplayName("Subtree")
                .WithDefaultValue(BehaviorTreeGraphToolkitBehaviorTreeTypes.CreateGraphValue(behaviorTreePath))
                .Delayed();
        }

        private string ReadBehaviorTreePathForDisplay()
        {
            string behaviorTreePath;
            if (TryReadBehaviorTreeOptionPath(out behaviorTreePath))
            {
                return behaviorTreePath;
            }

            return ReadBehaviorTreePath(ReadProperties(PropertiesJson));
        }

        private bool TryReadBehaviorTreeOptionPath(out string behaviorTreePath)
        {
            behaviorTreePath = null;
            INodeOption option = null;
            try
            {
                option = GetNodeOptionByName(BehaviorTreeOptionName);
            }
            catch
            {
            }

            if (option != null)
            {
                BehaviorTreeAsset asset;
                if (option.TryGetValue(out asset))
                {
                    behaviorTreePath = BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(asset.Path);
                    return true;
                }

                string text;
                if (option.TryGetValue(out text))
                {
                    behaviorTreePath = BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(text);
                    return true;
                }
            }

            return false;
        }

        private static string ReadBehaviorTreePath(Dictionary<string, object> properties)
        {
            object value;
            return properties != null && properties.TryGetValue(BehaviorTreeOptionName, out value) && value != null
                ? BehaviorTreeGraphToolkitBehaviorTreeTypes.NormalizePath(Convert.ToString(value))
                : string.Empty;
        }

        private static string CreateTitle(string behaviorTreePath)
        {
            string displayName = BehaviorTreeGraphToolkitBehaviorTreeTypes.GetDisplayName(behaviorTreePath);
            return string.IsNullOrEmpty(displayName) ? "Task: Run Subtree" : "Task: Run Subtree: " + displayName;
        }

        private static Dictionary<string, object> ReadProperties(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            try
            {
                return BlueprintJson.DeserializeObject(json);
            }
            catch (BlueprintJsonException)
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskMoveToNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.MoveTo", "Task: Move To", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target", false);
            AddBlackboardInput("targetPosition", "Vector3", "Target Position", true);
            AddBlackboardInput("acceptableRadius", "float", "Acceptable Radius", true);
            AddBlackboardInput("speed", "float", "Speed", true);
            AddBlackboardInput("allowTransformFallback", "bool", "Allow Transform Fallback", true);
            AddBlackboardInput("stopOnAbort", "bool", "Stop On Abort", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskStopNavigationNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.StopNavigation", "Task: Stop Navigation", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("stopAgent", "bool", "Stop Agent", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskRotateToNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.RotateTo", "Task: Rotate To", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("target", null, "Target", false);
            AddBlackboardInput("targetPosition", "Vector3", "Target Position", true);
            AddBlackboardInput("ignoreY", "bool", "Ignore Y", true);
            AddBlackboardInput("angleTolerance", "float", "Angle Tolerance", true);
            AddBlackboardInput("rotationSpeed", "float", "Rotation Speed", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskTriggerBlueprintEventNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.TriggerBlueprintEvent", "Task: Trigger Blueprint Event", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("eventName", "string", "Event Name", true);
            AddBlackboardInput("target", null, "Target", false);
            AddBlackboardInput("successOnMissing", "bool", "Success On Missing", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskRunBlueprintTaskNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.RunBlueprintTask", "Task: Run Blueprint Task", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("startEventName", "string", "Start Event", true);
            AddBlackboardInput("eventName", "string", "Event Name", true);
            AddBlackboardInput("target", null, "Target", false);
            AddBlackboardInput("timeout", "float", "Timeout", true);
            AddBlackboardInput("complete", "bool", "Complete", true);
            AddBlackboardInput("failure", "bool", "Failure", true);
            AddBlackboardInput("timeoutStatus", "string", "Timeout Status", true);
            AddBlackboardInput("abortEventName", "string", "Abort Event", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTTaskLogNode : BehaviorTreeVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("BT.Log", "Task: Log", 0);
        }

        protected override void ApplyDefaultMetadata()
        {
            AddBlackboardInput("message", "string", "Message", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorBlackboardConditionNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.BlackboardCondition, "Decorator: Blackboard Condition");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("value", null, "Value", false);
            AddDecoratorInput("operator", nameof(BehaviorTreeComparisonOperator), "Operator", true);
            AddDecoratorInput("expected", null, "Expected", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorCompareFloatNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.CompareFloat, "Decorator: Compare Float");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("left", "float", "Left", true);
            AddDecoratorInput("right", "float", "Right", true);
            AddDecoratorInput("operator", nameof(BehaviorTreeComparisonOperator), "Operator", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorCompareBoolNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.CompareBool, "Decorator: Compare Bool");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("value", "bool", "Value", true);
            AddDecoratorInput("expected", "bool", "Expected", true);
            AddDecoratorInput("operator", nameof(BehaviorTreeComparisonOperator), "Operator", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorObjectIsSetNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.ObjectIsSet, "Decorator: Object Is Set");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("value", null, "Value", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorDistanceLessThanNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.DistanceLessThan, "Decorator: Distance Less Than");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("distance", "float", "Distance", true);
            AddDecoratorInput("source", null, "Source", false);
            AddDecoratorInput("sourcePosition", "Vector3", "Source Position", true);
            AddDecoratorInput("target", null, "Target", false);
            AddDecoratorInput("targetPosition", "Vector3", "Target Position", true);
            AddDecoratorInput("maxDistance", "float", "Max Distance", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BehaviorTreeVisualGraph))]
    public sealed class BTDecoratorCooldownNode : BehaviorTreeVisualDecoratorNode
    {
        protected override void ConfigureDefaultDecorator()
        {
            SetIdentity(BehaviorTreeVisualNodeMetadata.Cooldown, "Decorator: Cooldown");
        }

        protected override void ApplyDefaultMetadata()
        {
            AddDecoratorInput("duration", "float", "Duration", true);
        }
    }
}
