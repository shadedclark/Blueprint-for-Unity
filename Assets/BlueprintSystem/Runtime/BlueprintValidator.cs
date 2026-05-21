using System;
using System.Collections.Generic;

namespace BlueprintSystem
{
    public sealed class BlueprintValidator
    {
        public BlueprintDiagnosticList Validate(BlueprintSource source, BlueprintNodeManifestCollection manifests, BlueprintExecutorRegistry registry)
        {
            BlueprintDiagnosticList diagnostics = new BlueprintDiagnosticList();
            if (source == null)
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BP010", "Blueprint source is null."));
                return diagnostics;
            }

            ValidateRequiredRootFields(source, diagnostics);
            ValidateComponents(source, diagnostics);

            Dictionary<string, BlueprintNodeSource> nodesById = BuildNodeIndex(source, diagnostics);
            Dictionary<string, BlueprintBindingDeclaration> bindingsByName = BuildBindingIndex(source);
            Dictionary<string, BlueprintVariableDeclaration> variablesByName = BuildVariableIndex(source, diagnostics);
            Dictionary<BlueprintPortKey, List<RuntimeEdge>> execEdges = new Dictionary<BlueprintPortKey, List<RuntimeEdge>>();
            Dictionary<BlueprintPortKey, RuntimeEdge> valueInputs = new Dictionary<BlueprintPortKey, RuntimeEdge>();

            ValidateNodes(source, manifests, registry, bindingsByName, variablesByName, valueInputs, diagnostics);
            ValidateEdges(source, manifests, nodesById, variablesByName, execEdges, valueInputs, diagnostics);
            ValidateRequiredValueInputs(source, manifests, valueInputs, diagnostics);
            ValidateVariableSetValues(source, variablesByName, valueInputs, diagnostics);
            ValidateEvents(source, manifests, diagnostics);
            ValidateValueCycles(valueInputs, diagnostics);

            return diagnostics;
        }

        private static void ValidateRequiredRootFields(BlueprintSource source, BlueprintDiagnosticList diagnostics)
        {
            if (string.IsNullOrEmpty(source.SchemaVersion))
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BP010", "Missing schemaVersion."));
            }

            if (string.IsNullOrEmpty(source.Name))
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BP010", "Missing blueprint name."));
            }
        }

        private static Dictionary<string, BlueprintNodeSource> BuildNodeIndex(BlueprintSource source, BlueprintDiagnosticList diagnostics)
        {
            Dictionary<string, BlueprintNodeSource> nodesById = new Dictionary<string, BlueprintNodeSource>();
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BlueprintNodeSource node = source.Nodes[i];
                if (string.IsNullOrEmpty(node.Id))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP010", "Node is missing id."));
                    continue;
                }

                if (nodesById.ContainsKey(node.Id))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP006", "Duplicate node id '" + node.Id + "'.", node.Id));
                    continue;
                }

                nodesById.Add(node.Id, node);
            }

            return nodesById;
        }

        private static void ValidateComponents(BlueprintSource source, BlueprintDiagnosticList diagnostics)
        {
            HashSet<string> componentNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.Components.Count; i++)
            {
                BlueprintComponentDeclaration component = source.Components[i];
                if (component == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(component.Name))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP030", "Blueprint component is missing a name."));
                    continue;
                }

                if (!componentNames.Add(component.Name))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP031", "Duplicate blueprint component '" + component.Name + "'."));
                    continue;
                }

                if (string.IsNullOrEmpty(component.Blueprint))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP032", "Blueprint component '" + component.Name + "' is missing a blueprint asset path."));
                }
            }
        }

        private static Dictionary<string, BlueprintBindingDeclaration> BuildBindingIndex(BlueprintSource source)
        {
            Dictionary<string, BlueprintBindingDeclaration> bindings = new Dictionary<string, BlueprintBindingDeclaration>();
            for (int i = 0; i < source.Bindings.Count; i++)
            {
                BlueprintBindingDeclaration binding = source.Bindings[i];
                if (!string.IsNullOrEmpty(binding.Name))
                {
                    bindings[binding.Name] = binding;
                }
            }

            return bindings;
        }

        private static Dictionary<string, BlueprintVariableDeclaration> BuildVariableIndex(BlueprintSource source, BlueprintDiagnosticList diagnostics)
        {
            Dictionary<string, BlueprintVariableDeclaration> variables = new Dictionary<string, BlueprintVariableDeclaration>();
            for (int i = 0; i < source.Variables.Count; i++)
            {
                BlueprintVariableDeclaration variable = source.Variables[i];
                if (variable == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(variable.Name))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP020", "Blueprint variable is missing a name."));
                    continue;
                }

                if (variables.ContainsKey(variable.Name))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP023", "Duplicate variable '" + variable.Name + "'."));
                    continue;
                }

                if (variable.Type == BlueprintVariableTypeRegistry.BlueprintRefTypeId)
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP025", "BlueprintRef is a runtime-only handle and cannot be declared as a persisted variable '" + variable.Name + "'."));
                }
                else if (!BlueprintVariableTypeRegistry.IsKnownType(variable.Type))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP025", "Unknown variable type '" + variable.Type + "' for variable '" + variable.Name + "'."));
                }
                else if (variable.DefaultValue != null && !BlueprintTypeUtility.IsValueAssignableToType(variable.DefaultValue, variable.Type))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP024", "Default value of variable '" + variable.Name + "' must be " + variable.Type + "."));
                }

                variables.Add(variable.Name, variable);
            }

            return variables;
        }

        private static void ValidateNodes(
            BlueprintSource source,
            BlueprintNodeManifestCollection manifests,
            BlueprintExecutorRegistry registry,
            Dictionary<string, BlueprintBindingDeclaration> bindingsByName,
            Dictionary<string, BlueprintVariableDeclaration> variablesByName,
            Dictionary<BlueprintPortKey, RuntimeEdge> valueInputs,
            BlueprintDiagnosticList diagnostics)
        {
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BlueprintNodeSource node = source.Nodes[i];
                BlueprintNodeManifest manifest;
                if (!manifests.TryGet(node.TypeId, out manifest))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP001", "Unknown node type '" + node.TypeId + "'.", node.Id));
                    continue;
                }

                IBlueprintNodeExecutor executor;
                if (string.IsNullOrEmpty(manifest.Executor) || !registry.TryGet(manifest.Executor, out executor))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP009", "No executor registered for '" + manifest.Executor + "'.", node.Id));
                }

                ValidateProperties(node, manifest, bindingsByName, diagnostics);
                ValidateVariableReference(node, variablesByName, diagnostics);
            }
        }

        private static void ValidateProperties(
            BlueprintNodeSource node,
            BlueprintNodeManifest manifest,
            Dictionary<string, BlueprintBindingDeclaration> bindingsByName,
            BlueprintDiagnosticList diagnostics)
        {
            for (int i = 0; i < manifest.Properties.Count; i++)
            {
                BlueprintPropertySpec property = manifest.Properties[i];
                object value;
                bool hasValue = node.Properties.TryGetValue(property.Id, out value);
                if (property.Required && !hasValue)
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP002", "Missing required property '" + property.Id + "'.", node.Id, property.Id));
                    continue;
                }

                if (hasValue && !BlueprintTypeUtility.IsValueAssignableToType(value, property.Type))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP012", "Property '" + property.Id + "' must be type '" + property.Type + "'.", node.Id, property.Id));
                }

                if (hasValue && property.Type != null && property.Type.StartsWith("UIBinding<", System.StringComparison.Ordinal))
                {
                    string bindingName = value as string;
                    if (string.IsNullOrEmpty(bindingName))
                    {
                        if (property.Required)
                        {
                            diagnostics.Add(BlueprintDiagnostic.Error("BP005", "Unknown UI binding '" + bindingName + "'.", node.Id, property.Id));
                        }

                        continue;
                    }

                    if (!bindingsByName.ContainsKey(bindingName))
                    {
                        diagnostics.Add(BlueprintDiagnostic.Error("BP005", "Unknown UI binding '" + bindingName + "'.", node.Id, property.Id));
                    }
                }
            }
        }

        private static void ValidateEdges(
            BlueprintSource source,
            BlueprintNodeManifestCollection manifests,
            Dictionary<string, BlueprintNodeSource> nodesById,
            Dictionary<string, BlueprintVariableDeclaration> variablesByName,
            Dictionary<BlueprintPortKey, List<RuntimeEdge>> execEdges,
            Dictionary<BlueprintPortKey, RuntimeEdge> valueInputs,
            BlueprintDiagnosticList diagnostics)
        {
            for (int i = 0; i < source.Edges.Count; i++)
            {
                BlueprintEdgeSource edgeSource = source.Edges[i];
                BlueprintPortKey from;
                BlueprintPortKey to;
                string edgeText = edgeSource.From + " -> " + edgeSource.To;
                if (!BlueprintPortKey.TryParse(edgeSource.From, out from) || !BlueprintPortKey.TryParse(edgeSource.To, out to))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP010", "Invalid edge port format.", null, null, edgeText));
                    continue;
                }

                BlueprintNodeSource fromNode;
                BlueprintNodeSource toNode;
                if (!nodesById.TryGetValue(from.NodeId, out fromNode) || !nodesById.TryGetValue(to.NodeId, out toNode))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP004", "Edge references unknown node.", null, null, edgeText));
                    continue;
                }

                BlueprintNodeManifest fromManifest;
                BlueprintNodeManifest toManifest;
                if (!manifests.TryGet(fromNode.TypeId, out fromManifest) || !manifests.TryGet(toNode.TypeId, out toManifest))
                {
                    continue;
                }

                BlueprintPortSpec output = fromManifest.FindOutput(from.PortId);
                BlueprintPortSpec input = toManifest.FindInput(to.PortId);
                if (output == null || input == null)
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP004", "Edge references unknown port.", to.NodeId, to.PortId, edgeText));
                    continue;
                }

                if (output.Kind != input.Kind)
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP004", "Cannot connect " + output.Kind + " output to " + input.Kind + " input.", to.NodeId, to.PortId, edgeText));
                    continue;
                }

                RuntimeEdge edge = new RuntimeEdge(from, to);
                if (output.Kind == BlueprintPortKind.Exec)
                {
                    AddExecEdge(execEdges, from, edge);
                    if (!input.AllowMultiple && CountIncomingExec(source, to) > 1)
                    {
                        diagnostics.Add(BlueprintDiagnostic.Error("BP004", "Exec input can only have one incoming edge.", to.NodeId, to.PortId, edgeText));
                    }
                }
                else
                {
                    string outputType = GetEffectiveOutputType(fromNode, output, variablesByName);
                    string inputType = GetEffectiveInputType(toNode, input, variablesByName);
                    if (!BlueprintTypeUtility.IsCompatible(outputType, inputType))
                    {
                        diagnostics.Add(BlueprintDiagnostic.Error("BP003", "Port type mismatch. Expected " + inputType + ", got " + outputType + ".", to.NodeId, to.PortId, edgeText));
                    }

                    if (valueInputs.ContainsKey(to))
                    {
                        diagnostics.Add(BlueprintDiagnostic.Error("BP004", "Value input can only have one incoming edge.", to.NodeId, to.PortId, edgeText));
                    }
                    else
                    {
                        valueInputs[to] = edge;
                    }
                }
            }
        }

        private static void ValidateVariableReference(
            BlueprintNodeSource node,
            Dictionary<string, BlueprintVariableDeclaration> variablesByName,
            BlueprintDiagnosticList diagnostics)
        {
            if (!IsVariableAccessNode(node))
            {
                return;
            }

            string variableName = GetVariableName(node);
            if (string.IsNullOrEmpty(variableName))
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BP020", node.TypeId + " node '" + node.Id + "' has no variable name.", node.Id, "name"));
                return;
            }

            if (!variablesByName.ContainsKey(variableName))
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BP021", "Unknown variable '" + variableName + "'.", node.Id, "name"));
            }
        }

        private static void ValidateVariableSetValues(
            BlueprintSource source,
            Dictionary<string, BlueprintVariableDeclaration> variablesByName,
            Dictionary<BlueprintPortKey, RuntimeEdge> valueInputs,
            BlueprintDiagnosticList diagnostics)
        {
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BlueprintNodeSource node = source.Nodes[i];
                if (node.TypeId != "Variable.Set")
                {
                    continue;
                }

                string variableName = GetVariableName(node);
                BlueprintVariableDeclaration variable;
                if (string.IsNullOrEmpty(variableName) || !variablesByName.TryGetValue(variableName, out variable))
                {
                    continue;
                }

                object value;
                if (valueInputs.ContainsKey(new BlueprintPortKey(node.Id, "value")) || !node.Properties.TryGetValue("value", out value))
                {
                    continue;
                }

                if (!BlueprintTypeUtility.IsValueAssignableToType(value, variable.Type))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP022", "Variable '" + variableName + "' expects " + variable.Type + ".", node.Id, "value"));
                }
            }
        }

        private static string GetEffectiveOutputType(
            BlueprintNodeSource node,
            BlueprintPortSpec output,
            Dictionary<string, BlueprintVariableDeclaration> variablesByName)
        {
            if (node != null && output != null && node.TypeId == "Variable.Get" && output.Id == "value")
            {
                BlueprintVariableDeclaration variable;
                if (variablesByName.TryGetValue(GetVariableName(node), out variable))
                {
                    return variable.Type;
                }
            }

            return output == null ? null : output.Type;
        }

        private static string GetEffectiveInputType(
            BlueprintNodeSource node,
            BlueprintPortSpec input,
            Dictionary<string, BlueprintVariableDeclaration> variablesByName)
        {
            if (node != null && input != null && node.TypeId == "Variable.Set" && input.Id == "value")
            {
                BlueprintVariableDeclaration variable;
                if (variablesByName.TryGetValue(GetVariableName(node), out variable))
                {
                    return variable.Type;
                }
            }

            return input == null ? null : input.Type;
        }

        private static bool IsVariableAccessNode(BlueprintNodeSource node)
        {
            return node != null && (node.TypeId == "Variable.Get" || node.TypeId == "Variable.Set");
        }

        private static string GetVariableName(BlueprintNodeSource node)
        {
            object value;
            if (node != null && node.Properties.TryGetValue("name", out value) && value != null)
            {
                return Convert.ToString(value);
            }

            return null;
        }

        private static void ValidateRequiredValueInputs(
            BlueprintSource source,
            BlueprintNodeManifestCollection manifests,
            Dictionary<BlueprintPortKey, RuntimeEdge> valueInputs,
            BlueprintDiagnosticList diagnostics)
        {
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BlueprintNodeSource node = source.Nodes[i];
                BlueprintNodeManifest manifest;
                if (!manifests.TryGet(node.TypeId, out manifest))
                {
                    continue;
                }

                for (int p = 0; p < manifest.Inputs.Count; p++)
                {
                    BlueprintPortSpec input = manifest.Inputs[p];
                    if (input.Kind != BlueprintPortKind.Value || !input.Required)
                    {
                        continue;
                    }

                    BlueprintPropertySpec property = manifest.FindProperty(input.Id);
                    bool hasConnection = valueInputs.ContainsKey(new BlueprintPortKey(node.Id, input.Id));
                    bool hasProperty = node.Properties.ContainsKey(input.Id);
                    bool hasDefault = property != null && property.DefaultValue != null;
                    if (!hasConnection && !hasProperty && !hasDefault)
                    {
                        diagnostics.Add(BlueprintDiagnostic.Error("BP002", "Missing required value input '" + input.Id + "'.", node.Id, input.Id));
                    }
                }
            }
        }

        private static void ValidateEvents(BlueprintSource source, BlueprintNodeManifestCollection manifests, BlueprintDiagnosticList diagnostics)
        {
            Dictionary<string, string> events = new Dictionary<string, string>();
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                BlueprintNodeSource node = source.Nodes[i];
                BlueprintNodeManifest manifest;
                if (!manifests.TryGet(node.TypeId, out manifest) || !BlueprintEventUtility.IsEventNode(manifest))
                {
                    continue;
                }

                string eventName = BlueprintEventUtility.GetEventName(node);
                if (string.IsNullOrEmpty(eventName))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP011", "Event node has no event name.", node.Id));
                    continue;
                }

                if (events.ContainsKey(eventName))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP011", "Duplicate event entry '" + eventName + "'.", node.Id));
                }
                else
                {
                    events[eventName] = node.Id;
                }
            }
        }

        private static void ValidateValueCycles(Dictionary<BlueprintPortKey, RuntimeEdge> valueInputs, BlueprintDiagnosticList diagnostics)
        {
            Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>();
            foreach (RuntimeEdge edge in valueInputs.Values)
            {
                List<string> list;
                if (!dependencies.TryGetValue(edge.To.NodeId, out list))
                {
                    list = new List<string>();
                    dependencies[edge.To.NodeId] = list;
                }

                list.Add(edge.From.NodeId);
            }

            HashSet<string> visiting = new HashSet<string>();
            HashSet<string> visited = new HashSet<string>();
            foreach (string nodeId in dependencies.Keys)
            {
                if (HasCycle(nodeId, dependencies, visiting, visited))
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP008", "Value dependency cycle detected.", nodeId));
                    return;
                }
            }
        }

        private static bool HasCycle(string nodeId, Dictionary<string, List<string>> dependencies, HashSet<string> visiting, HashSet<string> visited)
        {
            if (visited.Contains(nodeId))
            {
                return false;
            }

            if (visiting.Contains(nodeId))
            {
                return true;
            }

            visiting.Add(nodeId);
            List<string> next;
            if (dependencies.TryGetValue(nodeId, out next))
            {
                for (int i = 0; i < next.Count; i++)
                {
                    if (HasCycle(next[i], dependencies, visiting, visited))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(nodeId);
            visited.Add(nodeId);
            return false;
        }

        private static void AddExecEdge(Dictionary<BlueprintPortKey, List<RuntimeEdge>> execEdges, BlueprintPortKey output, RuntimeEdge edge)
        {
            List<RuntimeEdge> list;
            if (!execEdges.TryGetValue(output, out list))
            {
                list = new List<RuntimeEdge>();
                execEdges[output] = list;
            }

            list.Add(edge);
        }

        private static int CountIncomingExec(BlueprintSource source, BlueprintPortKey input)
        {
            int count = 0;
            for (int i = 0; i < source.Edges.Count; i++)
            {
                BlueprintPortKey to;
                if (BlueprintPortKey.TryParse(source.Edges[i].To, out to) && to.Equals(input))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
