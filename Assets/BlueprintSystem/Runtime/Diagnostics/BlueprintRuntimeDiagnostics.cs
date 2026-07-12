using System;
using System.Collections.Generic;

namespace BlueprintSystem
{
    /// <summary>
    /// Read-only diagnostics surface for a compiled Blueprint instance. This deliberately exposes
    /// declared Blueprint data only; it is not a reflection or arbitrary runtime-object API.
    /// </summary>
    public interface IBlueprintDebugInspectable
    {
        IReadOnlyList<BlueprintDebugVariableDescriptor> GetVariableDescriptors();
        IReadOnlyList<BlueprintDebugComponentDescriptor> GetComponentDescriptors();
    }

    [Serializable]
    public sealed class BlueprintDebugVariableDescriptor
    {
        public string Id;
        public string Name;
        public string Type;
        public string Scope;
        public bool Exposed;
    }

    [Serializable]
    public sealed class BlueprintDebugComponentDescriptor
    {
        public string Name;
        public string SourcePath;
        public bool Compiled;
    }

    public static class BlueprintDebugInspectableUtility
    {
        public static IReadOnlyList<BlueprintDebugVariableDescriptor> GetVariableDescriptors(RuntimeBlueprint blueprint)
        {
            var result = new List<BlueprintDebugVariableDescriptor>();
            if (blueprint == null)
            {
                return result;
            }

            for (int i = 0; i < blueprint.Variables.Count; i++)
            {
                BlueprintVariableDeclaration variable = blueprint.Variables[i];
                if (variable == null || string.IsNullOrEmpty(variable.Name))
                {
                    continue;
                }

                result.Add(new BlueprintDebugVariableDescriptor
                {
                    Id = variable.Id ?? string.Empty,
                    Name = variable.Name,
                    Type = variable.Type ?? string.Empty,
                    Scope = variable.Scope ?? string.Empty,
                    Exposed = variable.Exposed
                });
            }

            return result;
        }

        public static IReadOnlyList<BlueprintDebugComponentDescriptor> GetComponentDescriptors(
            IDictionary<string, IBlueprintInstance> componentsByName)
        {
            var result = new List<BlueprintDebugComponentDescriptor>();
            if (componentsByName == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, IBlueprintInstance> pair in componentsByName)
            {
                IBlueprintInstance component = pair.Value;
                result.Add(new BlueprintDebugComponentDescriptor
                {
                    Name = pair.Key ?? string.Empty,
                    SourcePath = component == null ? string.Empty : component.SourcePath ?? string.Empty,
                    Compiled = component != null && component.RuntimeBlueprint != null
                });
            }

            result.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return result;
        }
    }
}
