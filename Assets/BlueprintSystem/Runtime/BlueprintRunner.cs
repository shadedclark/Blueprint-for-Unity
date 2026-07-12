using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    [Serializable]
    public sealed class BlueprintBindingEntry
    {
        public string Name;
        public UnityEngine.Object Target;
    }

    public sealed class BlueprintReloadOptions
    {
        public bool PreserveVariables = true;
        public bool TriggerReloadEvent = true;
        public bool RefreshReactiveBindings = true;
        public bool Log = true;
    }

    internal sealed class BlueprintReloadSnapshot
    {
        public readonly Dictionary<string, object> ValuesById = new Dictionary<string, object>(StringComparer.Ordinal);
        public readonly Dictionary<string, object> ValuesByName = new Dictionary<string, object>(StringComparer.Ordinal);
        public readonly Dictionary<string, BlueprintReloadSnapshot> ComponentsByName = new Dictionary<string, BlueprintReloadSnapshot>(StringComparer.Ordinal);
        private readonly HashSet<string> _dirtyValueIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _dirtyValueNames = new HashSet<string>(StringComparer.Ordinal);
        private bool _usesDirtyTracking;

        public bool TryGetValue(BlueprintVariableDeclaration variable, out object value)
        {
            value = null;
            if (variable == null)
            {
                return false;
            }

            if (_usesDirtyTracking && !IsDirtyValue(variable))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(variable.Id) && ValuesById.TryGetValue(variable.Id, out value))
            {
                return true;
            }

            return !string.IsNullOrEmpty(variable.Name) && ValuesByName.TryGetValue(variable.Name, out value);
        }

        public void UseDirtyTracking()
        {
            _usesDirtyTracking = true;
        }

        public void MarkDirtyValue(BlueprintVariableDeclaration variable)
        {
            if (variable == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(variable.Id))
            {
                _dirtyValueIds.Add(variable.Id);
            }

            if (!string.IsNullOrEmpty(variable.Name))
            {
                _dirtyValueNames.Add(variable.Name);
            }
        }

        public bool IsDirtyValue(BlueprintVariableDeclaration variable)
        {
            if (variable == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(variable.Id) && _dirtyValueIds.Contains(variable.Id))
            {
                return true;
            }

            return !string.IsNullOrEmpty(variable.Name) && _dirtyValueNames.Contains(variable.Name);
        }

        public bool TryGetComponent(string componentName, out BlueprintReloadSnapshot snapshot)
        {
            snapshot = null;
            return !string.IsNullOrEmpty(componentName) && ComponentsByName.TryGetValue(componentName, out snapshot);
        }
    }

    internal sealed class BlueprintRuntimeState
    {
        public RuntimeBlueprint Blueprint;
        public BlueprintExecutionContext Context;
        public BlueprintVM Vm;
        public readonly Dictionary<string, IBlueprintInstance> ComponentsByName = new Dictionary<string, IBlueprintInstance>(StringComparer.Ordinal);
    }

    internal static class BlueprintReloadUtility
    {
        public static BlueprintReloadSnapshot Capture(
            RuntimeBlueprint blueprint,
            IBlueprintVariableStore variables,
            Dictionary<string, IBlueprintInstance> componentsByName)
        {
            BlueprintReloadSnapshot snapshot = new BlueprintReloadSnapshot();
            DictionaryBlueprintVariableStore dictionaryStore = variables as DictionaryBlueprintVariableStore;
            if (dictionaryStore != null)
            {
                snapshot.UseDirtyTracking();
            }

            if (blueprint != null && variables != null)
            {
                for (int i = 0; i < blueprint.Variables.Count; i++)
                {
                    BlueprintVariableDeclaration variable = blueprint.Variables[i];
                    if (variable == null || string.IsNullOrEmpty(variable.Name))
                    {
                        continue;
                    }

                    object value;
                    if (!variables.TryGet(variable.Name, out value))
                    {
                        continue;
                    }

                    if (dictionaryStore != null && dictionaryStore.IsDirty(variable.Name))
                    {
                        snapshot.MarkDirtyValue(variable);
                    }

                    if (!string.IsNullOrEmpty(variable.Id))
                    {
                        snapshot.ValuesById[variable.Id] = value;
                    }

                    snapshot.ValuesByName[variable.Name] = value;
                }
            }

            if (componentsByName != null)
            {
                foreach (KeyValuePair<string, IBlueprintInstance> pair in componentsByName)
                {
                    BlueprintRuntimeComponent runtimeComponent = pair.Value as BlueprintRuntimeComponent;
                    if (runtimeComponent != null)
                    {
                        snapshot.ComponentsByName[pair.Key] = runtimeComponent.CaptureReloadSnapshot();
                    }
                }
            }

            return snapshot;
        }

        public static void RestoreVariables(RuntimeBlueprint blueprint, IBlueprintVariableStore variables, BlueprintReloadSnapshot snapshot)
        {
            if (blueprint == null || variables == null || snapshot == null)
            {
                return;
            }

            for (int i = 0; i < blueprint.Variables.Count; i++)
            {
                BlueprintVariableDeclaration variable = blueprint.Variables[i];
                if (variable == null || string.IsNullOrEmpty(variable.Name))
                {
                    continue;
                }

                object value;
                if (snapshot.TryGetValue(variable, out value))
                {
                    DictionaryBlueprintVariableStore dictionaryStore = variables as DictionaryBlueprintVariableStore;
                    if (dictionaryStore != null)
                    {
                        dictionaryStore.SetPreserved(variable.Name, value, snapshot.IsDirtyValue(variable));
                    }
                    else
                    {
                        variables.Set(variable.Name, value);
                    }
                }
            }
        }

        public static void ReplaceComponents(
            Dictionary<string, IBlueprintInstance> target,
            Dictionary<string, IBlueprintInstance> source)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            foreach (KeyValuePair<string, IBlueprintInstance> pair in source)
            {
                target[pair.Key] = pair.Value;
            }
        }
    }

    public class BlueprintRunner : MonoBehaviour, IBlueprintInstance, IBlueprintBindingResolver, IBlueprintDebugInspectable
    {
        private const string ReloadEventName = "OnReload";

        [SerializeField] private BlueprintCompiledAsset compiledBlueprint;
        [SerializeField] private bool triggerOnStart = true;
        [SerializeField, HideInInspector] private string startEventName = "OnStart";
        [SerializeField] private bool triggerOnTick = true;
        [SerializeField, HideInInspector] private string tickEventName = "OnTick";
        [SerializeField] private bool triggerOnFixedTick = true;
        [SerializeField, HideInInspector] private string fixedTickEventName = "OnFixedTick";
        [SerializeField] private bool triggerOnLateTick = true;
        [SerializeField, HideInInspector] private string lateTickEventName = "OnLateTick";
        [SerializeField] private BlueprintRunner ownerRunner;
        [SerializeField] private List<BlueprintVariableOverride> variableOverrides = new List<BlueprintVariableOverride>();
        [SerializeField] private List<BlueprintBindingEntry> bindings = new List<BlueprintBindingEntry>();

        private RuntimeBlueprint _blueprint;
        private BlueprintExecutionContext _context;
        private BlueprintVM _vm;
        private readonly Dictionary<string, IBlueprintInstance> _componentsByName = new Dictionary<string, IBlueprintInstance>();
        private readonly Dictionary<string, UnityEngine.Object> _bindingsByName = new Dictionary<string, UnityEngine.Object>();

        public string InstanceName
        {
            get { return name; }
        }

        public RuntimeBlueprint RuntimeBlueprint
        {
            get { return _blueprint; }
        }

        public IBlueprintInstance OwnerInstance
        {
            get { return ownerRunner == this ? null : ownerRunner; }
        }

        GameObject IBlueprintInstance.Owner
        {
            get { return gameObject; }
        }

        Component IBlueprintInstance.OwnerComponent
        {
            get { return this; }
        }

        public BlueprintCompiledAsset CompiledBlueprint
        {
            get { return compiledBlueprint; }
        }

        internal BlueprintExecutionContext ReactiveBindingContext
        {
            get { return _context; }
        }

        public string SourcePath
        {
            get { return compiledBlueprint == null ? null : compiledBlueprint.SourcePath; }
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

            _context.SetVariable(variableName, value);
            return true;
        }

        public void ResetVariables()
        {
            if (_context != null && _context.Variables != null)
            {
                _context.Variables.ResetToDefaults();
            }
        }

        protected virtual IBlueprintBindingResolver BindingResolver
        {
            get { return this; }
        }

        protected virtual void Awake()
        {
            RebuildBindingCache();
            Compile();
        }

        protected virtual void Start()
        {
            if (triggerOnStart)
            {
                TriggerEvent(startEventName);
                TriggerComponentLifecycleEvent(startEventName);
            }
        }

        protected virtual void Update()
        {
            if (triggerOnTick && HasEvent(tickEventName))
            {
                TriggerEvent(tickEventName);
            }

            if (triggerOnTick)
            {
                TriggerComponentLifecycleEvent(tickEventName);
            }
        }

        protected virtual void FixedUpdate()
        {
            if (triggerOnFixedTick && HasEvent(fixedTickEventName))
            {
                TriggerEvent(fixedTickEventName);
            }

            if (triggerOnFixedTick)
            {
                TriggerComponentLifecycleEvent(fixedTickEventName);
            }
        }

        protected virtual void LateUpdate()
        {
            if (triggerOnLateTick && HasEvent(lateTickEventName))
            {
                TriggerEvent(lateTickEventName);
            }

            if (triggerOnLateTick)
            {
                TriggerComponentLifecycleEvent(lateTickEventName);
            }
        }

        protected virtual void OnDestroy()
        {
            InvalidateRuntimeState();
        }

        public bool Compile()
        {
            BlueprintRuntimeState state;
            if (!TryCreateRuntimeState(null, false, true, out state))
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
                BlueprintLog.Log("[Blueprint] Hot reloaded " + name + ".", this);
            }

            if (options.TriggerReloadEvent)
            {
                TriggerReloadLifecycleEvent();
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

        private bool TryCreateRuntimeState(
            BlueprintReloadSnapshot snapshot,
            bool preserveVariables,
            bool log,
            out BlueprintRuntimeState state)
        {
            state = null;
            if (compiledBlueprint == null)
            {
                if (log)
                {
                    BlueprintLog.Warning("[Blueprint] Missing compiled blueprint asset on " + name + ".");
                }

                return false;
            }

            RuntimeBlueprint runtimeBlueprint;
            try
            {
                runtimeBlueprint = compiledBlueprint.CreateRuntimeBlueprint(BlueprintExecutorRegistry.CreateDefault());
            }
            catch (Exception exception)
            {
                if (log)
                {
                    BlueprintLog.Error("[Blueprint] Compile failed for " + compiledBlueprint.name + "\n" + exception.Message, this);
                }

                return false;
            }

            BlueprintDiagnosticList diagnostics = ValidateRuntimeBlueprint(runtimeBlueprint);
            if (diagnostics.HasErrors)
            {
                if (log)
                {
                    BlueprintLog.Error("[Blueprint] Compile failed for " + compiledBlueprint.name + "\n" + diagnostics.ToDisplayString(), this);
                }

                return false;
            }

            IBlueprintVariableStore variables = CreateVariableStore(runtimeBlueprint);
            if (preserveVariables)
            {
                BlueprintReloadUtility.RestoreVariables(runtimeBlueprint, variables, snapshot);
            }

            BlueprintRuntimeState newState = new BlueprintRuntimeState();
            newState.Blueprint = runtimeBlueprint;
            newState.Vm = new BlueprintVM();
            newState.Context = new BlueprintExecutionContext(
                runtimeBlueprint,
                gameObject,
                this,
                BindingResolver,
                variables,
                new ActionBlueprintEventBus(TriggerEvent),
                new UnityBlueprintLogger(),
                ExecuteFromOutput,
                this,
                OwnerInstance);

            BuildComponents(newState, snapshot, preserveVariables, log);
            state = newState;
            return true;
        }

        protected virtual IBlueprintVariableStore CreateVariableStore(RuntimeBlueprint blueprint)
        {
            return new DictionaryBlueprintVariableStore(blueprint, variableOverrides);
        }

        private static BlueprintDiagnosticList ValidateRuntimeBlueprint(RuntimeBlueprint blueprint)
        {
            BlueprintDiagnosticList diagnostics = new BlueprintDiagnosticList();
            if (blueprint == null)
            {
                diagnostics.Add(BlueprintDiagnostic.Error("BP010", "Compiled blueprint asset produced no runtime blueprint."));
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

        public void RebuildBindingCache()
        {
            _bindingsByName.Clear();
            for (int i = 0; i < bindings.Count; i++)
            {
                BlueprintBindingEntry entry = bindings[i];
                if (entry != null && !string.IsNullOrEmpty(entry.Name) && entry.Target != null)
                {
                    _bindingsByName[entry.Name] = entry.Target;
                }
            }
        }

        public T Resolve<T>(string bindingName) where T : UnityEngine.Object
        {
            UnityEngine.Object resolved = Resolve(bindingName);
            if (resolved == null)
            {
                return null;
            }

            T direct = resolved as T;
            if (direct != null)
            {
                return direct;
            }

            GameObject gameObject = resolved as GameObject;
            if (gameObject != null)
            {
                return gameObject.GetComponent<T>();
            }

            Component component = resolved as Component;
            if (component != null)
            {
                return component.GetComponent<T>();
            }

            return null;
        }

        public UnityEngine.Object Resolve(string bindingName)
        {
            UnityEngine.Object target;
            return _bindingsByName.TryGetValue(bindingName, out target) ? target : null;
        }

        public bool HasBinding(string bindingName)
        {
            return _bindingsByName.ContainsKey(bindingName);
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

        public IReadOnlyList<BlueprintDebugVariableDescriptor> GetVariableDescriptors()
        {
            return BlueprintDebugInspectableUtility.GetVariableDescriptors(_blueprint);
        }

        public IReadOnlyList<BlueprintDebugComponentDescriptor> GetComponentDescriptors()
        {
            return BlueprintDebugInspectableUtility.GetComponentDescriptors(_componentsByName);
        }

        private void ApplyRuntimeState(BlueprintRuntimeState state)
        {
            _blueprint = state.Blueprint;
            _vm = state.Vm;
            _context = state.Context;
            BlueprintReloadUtility.ReplaceComponents(_componentsByName, state.ComponentsByName);
        }

        private void InvalidateRuntimeState()
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

        protected void ClearReactiveBindings()
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
                    gameObject,
                    this,
                    BindingResolver,
                    state.Context == null ? new UnityBlueprintLogger() : state.Context.Logger);

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

        private void TriggerComponentLifecycleEvent(string eventName)
        {
            foreach (IBlueprintInstance component in _componentsByName.Values)
            {
                BlueprintRuntimeComponent runtimeComponent = component as BlueprintRuntimeComponent;
                if (runtimeComponent != null)
                {
                    runtimeComponent.TriggerLifecycleEvent(eventName);
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

        private void TriggerReloadLifecycleEvent()
        {
            if (HasEvent(ReloadEventName))
            {
                TriggerEvent(ReloadEventName);
            }

            TriggerComponentLifecycleEvent(ReloadEventName);
        }
    }
}
