using System;

namespace BlueprintSystem
{
    public sealed class VariableGetExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Variable.Get"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            string name = context.GetInputValue(node, "name", string.Empty);
            if (string.IsNullOrEmpty(name))
            {
                context.Logger.Error("Variable.Get node '" + node.Id + "' has no variable name.");
                return null;
            }

            object value;
            if (context.Variables.TryGet(name, out value))
            {
                return value;
            }

            context.Logger.Error("Variable.Get node '" + node.Id + "' references unknown variable '" + name + "'.");
            return null;
        }
    }

    public sealed class VariableSetExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Variable.Set"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string name = context.GetInputValue(node, "name", string.Empty);
            object value = context.GetInputValue(node, "value");
            if (string.IsNullOrEmpty(name))
            {
                return BlueprintExecResult.Error("Variable.Set node '" + node.Id + "' has no variable name.");
            }

            if (!context.Variables.Contains(name))
            {
                return BlueprintExecResult.Error("Variable.Set node '" + node.Id + "' references unknown variable '" + name + "'.");
            }

            BlueprintVariableDeclaration declaration = context.Blueprint == null
                ? null
                : context.Blueprint.Variables.Find(item => item != null && item.Name == name);
            if (declaration != null &&
                BlueprintDataTableVariableTypeUtility.IsDataTableType(declaration.Type) &&
                !BlueprintTypeUtility.IsValueAssignableToType(value, declaration.Type))
            {
                return BlueprintExecResult.Error(
                    "Variable.Set node '" + node.Id + "' expects " + declaration.Type + " for variable '" + name + "'.");
            }

            context.SetVariable(name, value);
            BlueprintReactiveBindingRuntime.RefreshForContext(context);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class VariableCompareExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Variable.Compare"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            object left = context.GetInputValue(node, "left");
            object right = context.GetInputValue(node, "right");
            ComparisonMode comparison = context.GetInputValue(node, "comparison", ComparisonMode.Equals);

            switch (comparison)
            {
                case ComparisonMode.NotEquals:
                    return !object.Equals(Normalize(left), Normalize(right));
                case ComparisonMode.Greater:
                    return ToDouble(left) > ToDouble(right);
                case ComparisonMode.GreaterOrEqual:
                    return ToDouble(left) >= ToDouble(right);
                case ComparisonMode.Less:
                    return ToDouble(left) < ToDouble(right);
                case ComparisonMode.LessOrEqual:
                    return ToDouble(left) <= ToDouble(right);
                default:
                    return object.Equals(Normalize(left), Normalize(right));
            }
        }

        private static object Normalize(object value)
        {
            if (IsNumber(value))
            {
                return ToDouble(value);
            }

            return value;
        }

        private static bool IsNumber(object value)
        {
            return value is byte ||
                   value is sbyte ||
                   value is short ||
                   value is ushort ||
                   value is int ||
                   value is uint ||
                   value is long ||
                   value is ulong ||
                   value is float ||
                   value is double ||
                   value is decimal;
        }

        private static double ToDouble(object value)
        {
            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return 0d;
            }
        }
    }

}
