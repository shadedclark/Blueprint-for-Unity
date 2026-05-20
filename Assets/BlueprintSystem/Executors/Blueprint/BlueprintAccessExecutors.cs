using System;
using System.Collections.Generic;

namespace BlueprintSystem
{
    internal static class BlueprintAccessUtility
    {
        public static BlueprintRef CreateRef(IBlueprintInstance instance)
        {
            return instance == null ? null : new BlueprintRef(instance);
        }

        public static IBlueprintInstance ResolveRuntimeInstanceTarget(BlueprintExecutionContext context, object targetValue, bool logWarnings)
        {
            IBlueprintInstance referencedInstance;
            if (TryResolveBlueprintRef(targetValue, out referencedInstance))
            {
                if (referencedInstance != null)
                {
                    return referencedInstance;
                }

                Warn(context, logWarnings, "Cross-blueprint target BlueprintRef is invalid.");
                return null;
            }

            IBlueprintInstance directInstance = targetValue as IBlueprintInstance;
            if (directInstance != null)
            {
                return directInstance;
            }

            string targetPath = targetValue as string;
            if (string.IsNullOrEmpty(targetPath))
            {
                if (targetValue != null)
                {
                    Warn(context, logWarnings, "Cross-blueprint target expects a Blueprint asset path string or BlueprintRef.");
                }

                return null;
            }

            targetPath = NormalizeAssetPath(targetPath);
            if (string.IsNullOrEmpty(targetPath))
            {
                return null;
            }

            IBlueprintInstance root = ResolveRootInstance(context);
            if (root == null)
            {
                Warn(context, logWarnings, "Cross-blueprint target '" + targetPath + "' has no current Blueprint instance tree.");
                return null;
            }

            List<IBlueprintInstance> matches = new List<IBlueprintInstance>();
            CollectMatchingBlueprintTargets(root, targetPath, matches);
            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count > 1)
            {
                Warn(context, logWarnings, "Cross-blueprint target '" + targetPath + "' matched multiple Blueprint components in the current instance tree.");
                return null;
            }

            return null;
        }

        public static IBlueprintInstance ResolveOwner(BlueprintExecutionContext context, object targetValue, bool logWarnings)
        {
            IBlueprintInstance instance = ResolveOptionalInstance(context, targetValue, logWarnings);
            return instance == null ? null : instance.OwnerInstance;
        }

        public static IBlueprintInstance ResolveComponent(BlueprintExecutionContext context, object targetValue, string componentName, bool logWarnings)
        {
            if (string.IsNullOrEmpty(componentName))
            {
                Warn(context, logWarnings, "Blueprint.GetComponent requires a component name.");
                return null;
            }

            IBlueprintInstance instance = ResolveOptionalInstance(context, targetValue, logWarnings);
            while (instance != null)
            {
                IBlueprintInstance component;
                if (instance.TryGetBlueprintComponent(componentName, out component) && component != null)
                {
                    return component;
                }

                instance = instance.OwnerInstance;
            }

            return null;
        }

        private static IBlueprintInstance ResolveOptionalInstance(BlueprintExecutionContext context, object targetValue, bool logWarnings)
        {
            if (targetValue == null)
            {
                return ResolveCurrentInstance(context);
            }

            IBlueprintInstance referencedInstance;
            if (TryResolveBlueprintRef(targetValue, out referencedInstance))
            {
                if (referencedInstance != null)
                {
                    return referencedInstance;
                }

                Warn(context, logWarnings, "BlueprintRef target is invalid.");
                return null;
            }

            IBlueprintInstance directInstance = targetValue as IBlueprintInstance;
            if (directInstance != null)
            {
                return directInstance;
            }

            Warn(context, logWarnings, "Blueprint instance target expects a BlueprintRef.");
            return null;
        }

        private static bool TryResolveBlueprintRef(object targetValue, out IBlueprintInstance instance)
        {
            BlueprintRef reference = targetValue as BlueprintRef;
            if (reference != null)
            {
                instance = reference.Instance;
                return true;
            }

            instance = null;
            return false;
        }

        private static IBlueprintInstance ResolveCurrentInstance(BlueprintExecutionContext context)
        {
            if (context == null)
            {
                return null;
            }

            if (context.Instance != null)
            {
                return context.Instance;
            }

            if (context.OwnerInstance != null)
            {
                return context.OwnerInstance;
            }

            BlueprintRunner ownerRunner = context.OwnerComponent as BlueprintRunner;
            if (ownerRunner == null && context.Owner != null)
            {
                ownerRunner = context.Owner.GetComponent<BlueprintRunner>();
            }

            return ownerRunner;
        }

        private static IBlueprintInstance ResolveRootInstance(BlueprintExecutionContext context)
        {
            IBlueprintInstance instance = ResolveCurrentInstance(context);
            while (instance != null && instance.OwnerInstance != null)
            {
                instance = instance.OwnerInstance;
            }

            return instance;
        }

        private static void CollectMatchingBlueprintTargets(IBlueprintInstance instance, string targetPath, List<IBlueprintInstance> matches)
        {
            if (instance == null)
            {
                return;
            }

            string sourcePath = instance.SourcePath;
            if (PathEquals(sourcePath, targetPath))
            {
                AddUnique(matches, instance);
            }

            RuntimeBlueprint blueprint = instance.RuntimeBlueprint;
            if (blueprint == null)
            {
                return;
            }

            for (int i = 0; i < blueprint.Components.Count; i++)
            {
                BlueprintComponentDeclaration declaration = blueprint.Components[i];
                if (declaration == null || string.IsNullOrEmpty(declaration.Name))
                {
                    continue;
                }

                IBlueprintInstance component;
                if (!instance.TryGetBlueprintComponent(declaration.Name, out component) || component == null)
                {
                    continue;
                }

                if (PathEquals(declaration.Blueprint, targetPath))
                {
                    AddUnique(matches, component);
                }

                CollectMatchingBlueprintTargets(component, targetPath, matches);
            }
        }

        private static void AddUnique(List<IBlueprintInstance> matches, IBlueprintInstance instance)
        {
            if (instance != null && !matches.Contains(instance))
            {
                matches.Add(instance);
            }
        }

        private static bool PathEquals(string left, string right)
        {
            left = NormalizeAssetPath(left);
            right = NormalizeAssetPath(right);
            return !string.IsNullOrEmpty(left) &&
                   !string.IsNullOrEmpty(right) &&
                   string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/').Trim();
        }

        public static bool TryGetExposedVariable(IBlueprintInstance instance, string variableName, out BlueprintVariableDeclaration declaration)
        {
            declaration = null;
            if (instance == null || instance.RuntimeBlueprint == null || string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            for (int i = 0; i < instance.RuntimeBlueprint.Variables.Count; i++)
            {
                BlueprintVariableDeclaration variable = instance.RuntimeBlueprint.Variables[i];
                if (variable != null && variable.Name == variableName)
                {
                    declaration = variable;
                    return variable.Exposed;
                }
            }

            return false;
        }

        public static bool TryGetExposedVariableValue(
            BlueprintExecutionContext context,
            IBlueprintInstance instance,
            string variableName,
            out object value,
            bool logWarnings)
        {
            value = null;
            if (instance == null)
            {
                Warn(context, logWarnings, "Cross-blueprint variable read has no valid target.");
                return false;
            }

            if (instance.RuntimeBlueprint == null)
            {
                Warn(context, logWarnings, "Cross-blueprint variable read target '" + instance.InstanceName + "' is not compiled.");
                return false;
            }

            BlueprintVariableDeclaration declaration;
            if (!TryGetExposedVariable(instance, variableName, out declaration))
            {
                Warn(context, logWarnings, "Cross-blueprint variable read cannot access '" + variableName + "' on '" + instance.InstanceName + "'.");
                return false;
            }

            if (!instance.TryGetVariable(variableName, out value))
            {
                Warn(context, logWarnings, "Cross-blueprint variable read could not resolve '" + variableName + "' on '" + instance.InstanceName + "'.");
                return false;
            }

            return true;
        }

        public static bool TrySetExposedVariableValue(
            IBlueprintInstance instance,
            string variableName,
            object value,
            out string error)
        {
            error = null;
            if (instance == null)
            {
                error = "Cross-blueprint variable write has no valid target.";
                return false;
            }

            if (instance.RuntimeBlueprint == null)
            {
                error = "Cross-blueprint variable write target '" + instance.InstanceName + "' is not compiled.";
                return false;
            }

            BlueprintVariableDeclaration declaration;
            if (!TryGetExposedVariable(instance, variableName, out declaration))
            {
                error = "Cross-blueprint variable write cannot access '" + variableName + "' on '" + instance.InstanceName + "'.";
                return false;
            }

            if (!instance.TrySetVariable(variableName, value))
            {
                error = "Cross-blueprint variable write could not set '" + variableName + "' on '" + instance.InstanceName + "'.";
                return false;
            }

            return true;
        }

        private static void Warn(BlueprintExecutionContext context, string message)
        {
            Warn(context, true, message);
        }

        private static void Warn(BlueprintExecutionContext context, bool enabled, string message)
        {
            if (enabled && context != null && context.Logger != null)
            {
                context.Logger.Warning(message);
            }
        }
    }

    public sealed class BlueprintIsValidExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Blueprint.IsValid"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            IBlueprintInstance instance = BlueprintAccessUtility.ResolveRuntimeInstanceTarget(context, context.GetInputValue(node, "target"), true);
            return instance != null;
        }
    }

    public sealed class BlueprintGetOwnerExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Blueprint.GetOwner"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            bool logWarnings = outputPortId == "target";
            IBlueprintInstance owner = BlueprintAccessUtility.ResolveOwner(context, context.GetInputValue(node, "target"), logWarnings);
            if (outputPortId == "isValid")
            {
                return owner != null;
            }

            if (outputPortId == "target")
            {
                return BlueprintAccessUtility.CreateRef(owner);
            }

            return null;
        }
    }

    public sealed class BlueprintGetComponentExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Blueprint.GetComponent"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            bool logWarnings = outputPortId == "target";
            string componentName = context.GetInputValue(node, "name", string.Empty);
            IBlueprintInstance component = BlueprintAccessUtility.ResolveComponent(
                context,
                context.GetInputValue(node, "target"),
                componentName,
                logWarnings);
            if (outputPortId == "isValid")
            {
                return component != null;
            }

            if (outputPortId == "target")
            {
                return BlueprintAccessUtility.CreateRef(component);
            }

            return null;
        }
    }

    public sealed class BlueprintTriggerEventExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Blueprint.TriggerEvent"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            IBlueprintInstance instance = BlueprintAccessUtility.ResolveRuntimeInstanceTarget(context, context.GetInputValue(node, "target"), true);
            if (instance == null)
            {
                return BlueprintExecResult.Error("Blueprint.TriggerEvent node '" + node.Id + "' has no valid target.");
            }

            string eventName = context.GetInputValue(node, "eventName", string.Empty);
            if (string.IsNullOrEmpty(eventName))
            {
                return BlueprintExecResult.Error("Blueprint.TriggerEvent node '" + node.Id + "' has no eventName.");
            }

            instance.TriggerEvent(eventName);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class BlueprintGetVariableExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Blueprint.GetVariable"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            bool logWarnings = outputPortId == "value";
            IBlueprintInstance instance = BlueprintAccessUtility.ResolveRuntimeInstanceTarget(context, context.GetInputValue(node, "target"), logWarnings);
            string variableName = context.GetInputValue(node, "name", string.Empty);
            object value;
            bool success = BlueprintAccessUtility.TryGetExposedVariableValue(
                context,
                instance,
                variableName,
                out value,
                logWarnings);

            if (outputPortId == "success")
            {
                return success;
            }

            if (outputPortId == "value")
            {
                return success ? value : null;
            }

            return null;
        }
    }

    public sealed class BlueprintSetVariableExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Blueprint.SetVariable"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            IBlueprintInstance instance = BlueprintAccessUtility.ResolveRuntimeInstanceTarget(context, context.GetInputValue(node, "target"), true);
            string variableName = context.GetInputValue(node, "name", string.Empty);
            object value = context.GetInputValue(node, "value");
            string error;
            if (!BlueprintAccessUtility.TrySetExposedVariableValue(instance, variableName, value, out error))
            {
                return BlueprintExecResult.Error(error);
            }

            return BlueprintExecResult.Continue("execOut");
        }
    }
}
