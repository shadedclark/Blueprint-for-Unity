using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class BlueprintRuntimeComponent : IBlueprintInstance
    {
        private readonly string _name;
        private readonly BlueprintCompiledAsset _compiledBlueprint;
        private readonly IBlueprintInstance _ownerInstance;
        private readonly GameObject _owner;
        private readonly Component _ownerComponent;
        private readonly IBlueprintBindingResolver _bindingResolver;
        private readonly IBlueprintLogger _logger;
        private readonly Dictionary<string, IBlueprintInstance> _componentsByName = new Dictionary<string, IBlueprintInstance>();

        private RuntimeBlueprint _blueprint;
        private BlueprintExecutionContext _context;
        private BlueprintVM _vm;

        public BlueprintRuntimeComponent(
            string name,
            BlueprintCompiledAsset compiledBlueprint,
            IBlueprintInstance ownerInstance,
            GameObject owner,
            Component ownerComponent,
            IBlueprintBindingResolver bindingResolver,
            IBlueprintLogger logger)
        {
            _name = name;
            _compiledBlueprint = compiledBlueprint;
            _ownerInstance = ownerInstance;
            _owner = owner;
            _ownerComponent = ownerComponent;
            _bindingResolver = bindingResolver ?? new NullBlueprintBindingResolver();
            _logger = logger ?? new UnityBlueprintLogger();
        }

        public string InstanceName
        {
            get { return _name; }
        }

        public RuntimeBlueprint RuntimeBlueprint
        {
            get { return _blueprint; }
        }

        public BlueprintCompiledAsset CompiledBlueprint
        {
            get { return _compiledBlueprint; }
        }

        public string SourcePath
        {
            get { return _compiledBlueprint == null ? null : _compiledBlueprint.SourcePath; }
        }

        public IBlueprintInstance OwnerInstance
        {
            get { return _ownerInstance; }
        }

        public GameObject Owner
        {
            get { return _owner; }
        }

        public Component OwnerComponent
        {
            get { return _ownerComponent; }
        }

        public bool Compile()
        {
            if (_compiledBlueprint == null)
            {
                _logger.Warning("[Blueprint] Missing compiled component blueprint asset for " + _name + ".");
                return false;
            }

            RuntimeBlueprint runtimeBlueprint = _compiledBlueprint.CreateRuntimeBlueprint(BlueprintExecutorRegistry.CreateDefault());
            BlueprintDiagnosticList diagnostics = ValidateRuntimeBlueprint(runtimeBlueprint);
            if (diagnostics.HasErrors)
            {
                _logger.Error("[Blueprint] Compile failed for component " + _name + "\n" + diagnostics.ToDisplayString());
                return false;
            }

            _blueprint = runtimeBlueprint;
            _vm = new BlueprintVM();
            _context = new BlueprintExecutionContext(
                _blueprint,
                _owner,
                _ownerComponent,
                _bindingResolver,
                new DictionaryBlueprintVariableStore(_blueprint),
                new ActionBlueprintEventBus(TriggerEvent),
                _logger,
                ExecuteFromOutput,
                this,
                _ownerInstance);

            BuildComponents();
            return true;
        }

        public bool TryGetVariable(string variableName, out object value)
        {
            value = null;
            if (_context == null || _context.Variables == null || string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            return _context.Variables.TryGet(variableName, out value);
        }

        public bool TrySetVariable(string variableName, object value)
        {
            if (_context == null || _context.Variables == null || string.IsNullOrEmpty(variableName) || !_context.Variables.Contains(variableName))
            {
                return false;
            }

            _context.Variables.Set(variableName, value);
            return true;
        }

        public void TriggerEvent(string eventName)
        {
            if (_blueprint == null || _context == null || _vm == null)
            {
                return;
            }

            _vm.TriggerEvent(_context, eventName);
        }

        public bool HasEvent(string eventName)
        {
            return _blueprint != null &&
                   !string.IsNullOrEmpty(eventName) &&
                   _blueprint.EventEntries.ContainsKey(eventName);
        }

        public bool TryGetBlueprintComponent(string componentName, out IBlueprintInstance component)
        {
            component = null;
            if (string.IsNullOrEmpty(componentName))
            {
                return false;
            }

            return _componentsByName.TryGetValue(componentName, out component);
        }

        public void TriggerLifecycleEvent(string eventName)
        {
            if (HasEvent(eventName))
            {
                TriggerEvent(eventName);
            }

            foreach (IBlueprintInstance component in _componentsByName.Values)
            {
                BlueprintRuntimeComponent runtimeComponent = component as BlueprintRuntimeComponent;
                if (runtimeComponent != null)
                {
                    runtimeComponent.TriggerLifecycleEvent(eventName);
                }
            }
        }

        private void BuildComponents()
        {
            _componentsByName.Clear();
            if (_blueprint == null)
            {
                return;
            }

            for (int i = 0; i < _blueprint.Components.Count; i++)
            {
                BlueprintComponentDeclaration declaration = _blueprint.Components[i];
                if (declaration == null || string.IsNullOrEmpty(declaration.Name) || declaration.CompiledBlueprint == null)
                {
                    continue;
                }

                BlueprintRuntimeComponent component = new BlueprintRuntimeComponent(
                    declaration.Name,
                    declaration.CompiledBlueprint,
                    this,
                    _owner,
                    _ownerComponent,
                    _bindingResolver,
                    _logger);

                if (component.Compile())
                {
                    _componentsByName[declaration.Name] = component;
                }
            }
        }

        private void ExecuteFromOutput(RuntimeNode node, string outputPortId)
        {
            if (_context == null || _vm == null)
            {
                return;
            }

            _vm.ExecuteFromOutput(_context, node, outputPortId);
        }

        private static BlueprintDiagnosticList ValidateRuntimeBlueprint(RuntimeBlueprint blueprint)
        {
            BlueprintDiagnosticList diagnostics = new BlueprintDiagnosticList();
            if (blueprint == null)
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BP010", "Compiled component blueprint asset produced no runtime blueprint."));
                return diagnostics;
            }

            foreach (RuntimeNode node in blueprint.NodesById.Values)
            {
                if (node == null)
                {
                    continue;
                }

                if (node.Executor == null)
                {
                    diagnostics.Add(BlueprintDiagnostic.Error("BP009", "No executor registered for node type '" + node.TypeId + "'.", node.Id));
                }
            }

            return diagnostics;
        }
    }
}
