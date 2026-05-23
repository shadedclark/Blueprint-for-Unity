using System;
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

        internal BlueprintExecutionContext ReactiveBindingContext
        {
            get { return _context; }
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
            return Compile(null, false, true);
        }

        internal bool Compile(BlueprintReloadSnapshot snapshot, bool preserveVariables, bool log)
        {
            BlueprintRuntimeState state;
            if (!TryCreateRuntimeState(snapshot, preserveVariables, log, out state))
            {
                return false;
            }

            InvalidateRuntimeState();
            ApplyRuntimeState(state);
            return true;
        }

        public bool ReloadBlueprint(BlueprintReloadOptions options = null)
        {
            options = options ?? new BlueprintReloadOptions();
            BlueprintReloadSnapshot snapshot = options.PreserveVariables ? CaptureReloadSnapshot() : null;

            BlueprintRuntimeState state;
            if (!TryCreateRuntimeState(snapshot, options.PreserveVariables, options.Log, out state))
            {
                return false;
            }

            BlueprintReactiveBindingSnapshot reactiveBindingSnapshot = options.RefreshReactiveBindings
                ? BlueprintReactiveBindingRuntime.CaptureForInstance(this)
                : null;

            InvalidateRuntimeState();
            ApplyRuntimeState(state);
            BlueprintReactiveBindingRuntime.RestoreForInstance(reactiveBindingSnapshot, this);

            if (options.Log)
            {
                _logger.Log("Hot reloaded component " + _name + ".");
            }

            if (options.TriggerReloadEvent)
            {
                TriggerLifecycleEvent("OnReload");
            }

            if (options.RefreshReactiveBindings)
            {
                BlueprintReactiveBindingRuntime.RefreshInstance(this);
            }

            return true;
        }

        internal BlueprintReloadSnapshot CaptureReloadSnapshot()
        {
            return BlueprintReloadUtility.Capture(
                _blueprint,
                _context == null ? null : _context.Variables,
                _componentsByName);
        }

        internal void InvalidateRuntimeState()
        {
            if (_context != null)
            {
                _context.InvalidateScheduledExecution();
                BlueprintReactiveBindingRuntime.Clear(_context);
            }

            foreach (IBlueprintInstance component in _componentsByName.Values)
            {
                BlueprintRuntimeComponent runtimeComponent = component as BlueprintRuntimeComponent;
                if (runtimeComponent != null)
                {
                    runtimeComponent.InvalidateRuntimeState();
                }
            }
        }

        internal void ClearReactiveBindings()
        {
            if (_context != null)
            {
                BlueprintReactiveBindingRuntime.Clear(_context);
            }

            foreach (IBlueprintInstance component in _componentsByName.Values)
            {
                BlueprintRuntimeComponent runtimeComponent = component as BlueprintRuntimeComponent;
                if (runtimeComponent != null)
                {
                    runtimeComponent.ClearReactiveBindings();
                }
            }
        }

        private bool TryCreateRuntimeState(
            BlueprintReloadSnapshot snapshot,
            bool preserveVariables,
            bool log,
            out BlueprintRuntimeState state)
        {
            state = null;
            if (_compiledBlueprint == null)
            {
                if (log)
                {
                    _logger.Warning("[Blueprint] Missing compiled component blueprint asset for " + _name + ".");
                }

                return false;
            }

            RuntimeBlueprint runtimeBlueprint;
            try
            {
                runtimeBlueprint = _compiledBlueprint.CreateRuntimeBlueprint(BlueprintExecutorRegistry.CreateDefault());
            }
            catch (Exception exception)
            {
                if (log)
                {
                    _logger.Error("[Blueprint] Compile failed for component " + _name + "\n" + exception.Message);
                }

                return false;
            }

            BlueprintDiagnosticList diagnostics = ValidateRuntimeBlueprint(runtimeBlueprint);
            if (diagnostics.HasErrors)
            {
                if (log)
                {
                    _logger.Error("[Blueprint] Compile failed for component " + _name + "\n" + diagnostics.ToDisplayString());
                }

                return false;
            }

            IBlueprintVariableStore variables = new DictionaryBlueprintVariableStore(runtimeBlueprint);
            if (preserveVariables)
            {
                BlueprintReloadUtility.RestoreVariables(runtimeBlueprint, variables, snapshot);
            }

            BlueprintRuntimeState newState = new BlueprintRuntimeState();
            newState.Blueprint = runtimeBlueprint;
            newState.Vm = new BlueprintVM();
            newState.Context = new BlueprintExecutionContext(
                runtimeBlueprint,
                _owner,
                _ownerComponent,
                _bindingResolver,
                variables,
                new ActionBlueprintEventBus(TriggerEvent),
                _logger,
                ExecuteFromOutput,
                this,
                _ownerInstance);

            BuildComponents(newState, snapshot, preserveVariables, log);
            state = newState;
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

        private void ApplyRuntimeState(BlueprintRuntimeState state)
        {
            _blueprint = state.Blueprint;
            _vm = state.Vm;
            _context = state.Context;
            BlueprintReloadUtility.ReplaceComponents(_componentsByName, state.ComponentsByName);
        }

        private void BuildComponents(
            BlueprintRuntimeState state,
            BlueprintReloadSnapshot snapshot,
            bool preserveVariables,
            bool log)
        {
            if (state == null || state.Blueprint == null)
            {
                return;
            }

            for (int i = 0; i < state.Blueprint.Components.Count; i++)
            {
                BlueprintComponentDeclaration declaration = state.Blueprint.Components[i];
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

                BlueprintReloadSnapshot componentSnapshot = null;
                if (preserveVariables && snapshot != null)
                {
                    snapshot.TryGetComponent(declaration.Name, out componentSnapshot);
                }

                if (component.Compile(componentSnapshot, preserveVariables, log))
                {
                    state.ComponentsByName[declaration.Name] = component;
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
