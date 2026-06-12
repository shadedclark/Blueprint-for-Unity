using System.Collections.Generic;

namespace BlueprintSystem
{
    public sealed class DataTableGetRowExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "DataTable.GetRow"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "row" && outputPortId != "found")
            {
                return base.Evaluate(context, node, outputPortId);
            }

            string tablePath;
            BlueprintDataTableDefinition definition;
            object dataTableValue = context.GetInputValue(node, BlueprintDataTableNodeUtility.DataTableInputId);
            if (!BlueprintDataTableNodeUtility.TryResolveDefinition(node.Properties, dataTableValue, out tablePath, out definition))
            {
                context.Logger.Error("DataTable.GetRow node '" + node.Id + "' has unknown table '" +
                    BlueprintDataTableNodeUtility.GetTablePath(node.Properties) + "'.");
                return outputPortId == "found" ? (object)false : null;
            }

            string rowName = context.GetInputValue(node, "rowName", string.Empty);
            object row;
            bool found;
            if (!BlueprintDataTableUtility.TryGetRow(definition, rowName, out row, out found))
            {
                context.Logger.Error("DataTable.GetRow node '" + node.Id + "' could not read table '" + tablePath + "'.");
                return outputPortId == "found" ? (object)false : null;
            }

            return outputPortId == "found" ? (object)found : row;
        }
    }

    public sealed class DataTableGetRowNamesExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "DataTable.GetRowNames"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "rowNames")
            {
                return base.Evaluate(context, node, outputPortId);
            }

            string tablePath;
            BlueprintDataTableDefinition definition;
            object dataTableValue = context.GetInputValue(node, BlueprintDataTableNodeUtility.DataTableInputId);
            if (!BlueprintDataTableNodeUtility.TryResolveDefinition(node.Properties, dataTableValue, out tablePath, out definition))
            {
                context.Logger.Error("DataTable.GetRowNames node '" + node.Id + "' has unknown table '" +
                    BlueprintDataTableNodeUtility.GetTablePath(node.Properties) + "'.");
                return new List<object>();
            }

            List<object> rowNames;
            if (!BlueprintDataTableUtility.TryGetRowNames(definition, out rowNames))
            {
                context.Logger.Error("DataTable.GetRowNames node '" + node.Id + "' could not read table '" + tablePath + "'.");
                return new List<object>();
            }

            return rowNames;
        }
    }

    public sealed class DataTableGetAllRowsExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "DataTable.GetAllRows"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "rows")
            {
                return base.Evaluate(context, node, outputPortId);
            }

            string tablePath;
            BlueprintDataTableDefinition definition;
            object dataTableValue = context.GetInputValue(node, BlueprintDataTableNodeUtility.DataTableInputId);
            if (!BlueprintDataTableNodeUtility.TryResolveDefinition(node.Properties, dataTableValue, out tablePath, out definition))
            {
                context.Logger.Error("DataTable.GetAllRows node '" + node.Id + "' has unknown table '" +
                    BlueprintDataTableNodeUtility.GetTablePath(node.Properties) + "'.");
                return new List<object>();
            }

            List<object> rows;
            if (!BlueprintDataTableUtility.TryGetAllRows(definition, out rows))
            {
                context.Logger.Error("DataTable.GetAllRows node '" + node.Id + "' could not read table '" + tablePath + "'.");
                return new List<object>();
            }

            return rows;
        }
    }
}
