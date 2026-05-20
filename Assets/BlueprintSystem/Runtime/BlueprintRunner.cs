using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public class BlueprintRunner : MonoBehaviour, IBlueprintInstance
    {
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

        private RuntimeBlueprint _blueprint;
        private BlueprintExecutionContext _context;
        private BlueprintVM _vm;
        private readonly Dictionary<string, IBlueprintInstance> _componentsByName = new Dictionary<string, IBlueprintInstance>();

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

            _context.Variables.Set(variableName, value);
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
            get { return new NullBlueprintBindingResolver(); }
        }

        protected virtual void Awake()
        {
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

        public bool Compile()
        {
            if (compiledBlueprint == null)
            {
                Debug.LogWarning("[Blueprint] Missing compiled blueprint asset on " + name + ".");
                return false;
            }

            RuntimeBlueprint runtimeBlueprint = compiledBlueprint.CreateRuntimeBlueprint(BlueprintExecutorRegistry.CreateDefault());
            BlueprintDiagnosticList diagnostics = ValidateRuntimeBlueprint(runtimeBlueprint);
            if (diagnostics.HasErrors)
            {
                Debug.LogError("[Blueprint] Compile failed for " + compiledBlueprint.name + "\n" + diagnostics.ToDisplayString());
                return false;
            }

            _blueprint = runtimeBlueprint;
            _vm = new BlueprintVM();
            _context = new BlueprintExecutionContext(
                _blueprint,
                gameObject,
                this,
                BindingResolver,
                CreateVariableStore(_blueprint),
                new ActionBlueprintEventBus(TriggerEvent),
                new UnityBlueprintLogger(),
                ExecuteFromOutput,
                this,
                OwnerInstance);
            BuildComponents();
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

        public bool TryGetBlueprintComponent(string componentName, out IBlueprintInstance component)
        {
            component = null;
            if (string.IsNullOrEmpty(componentName))
            {
                return false;
            }

            return _componentsByName.TryGetValue(componentName, out component);
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
                    gameObject,
                    this,
                    BindingResolver,
                    _context == null ? new UnityBlueprintLogger() : _context.Logger);

                if (component.Compile())
                {
                    _componentsByName[declaration.Name] = component;
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
    }
}
