using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

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

    public class BlueprintRunner : MonoBehaviour, IBlueprintInstance, IBlueprintBindingResolver, IBlueprintDebugInspectable, IBlueprintTargetHandleResolver
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
        [SerializeField] private string persistenceKey = string.Empty;
        [SerializeField] private string defaultPersistenceSlot = "default";
        [SerializeField] private bool autoLoadPersistentVariables = true;
        [SerializeField] private bool autoSavePersistentVariables = true;
        [SerializeField] private float persistentSaveDebounceSeconds = 0.75f;

        private RuntimeBlueprint _blueprint;
        private BlueprintExecutionContext _context;
        private BlueprintVM _vm;
        private readonly Dictionary<string, IBlueprintInstance> _componentsByName = new Dictionary<string, IBlueprintInstance>();
        private readonly Dictionary<string, UnityEngine.Object> _bindingsByName = new Dictionary<string, UnityEngine.Object>();
        private readonly List<ComponentRuntimeRecord> _componentRuntimeRecords = new List<ComponentRuntimeRecord>();
        private sealed class DynamicBlueprintTargetRecord
        {
            public int StablePathId;
            public string DebugPath;
            public int RuntimeRecordIndex;
        }

        private readonly List<DynamicBlueprintTargetRecord> _dynamicBlueprintTargetCache = new List<DynamicBlueprintTargetRecord>();
        private int _componentRuntimeVersion;
        private bool _persistenceDirty;
        private float _nextPersistenceSaveTime;

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

        public string PersistenceKey
        {
            get { return persistenceKey ?? string.Empty; }
        }

        public string DefaultPersistenceSlot
        {
            get { return string.IsNullOrEmpty(defaultPersistenceSlot) ? "default" : defaultPersistenceSlot; }
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
            if (Compile() && autoLoadPersistentVariables && !string.IsNullOrEmpty(PersistenceKey))
            {
                string error;
                BlueprintPersistenceStatus status = LoadPersistentVariables(string.Empty, out error);
                if (status == BlueprintPersistenceStatus.Failed)
                {
                    BlueprintLog.Warning("[Blueprint] Persistent variables could not be loaded for '" + PersistenceKey + "': " + error, this);
                }
            }
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

            if (autoSavePersistentVariables && _persistenceDirty && Time.unscaledTime >= _nextPersistenceSaveTime)
            {
                string error;
                BlueprintPersistenceStatus status = SavePersistentVariables(string.Empty, out error);
                if (status == BlueprintPersistenceStatus.Failed)
                {
                    _nextPersistenceSaveTime = Time.unscaledTime + Mathf.Max(0.1f, persistentSaveDebounceSeconds);
                    BlueprintLog.Warning("[Blueprint] Persistent variables could not be saved for '" + PersistenceKey + "': " + error, this);
                }
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
            FlushPersistentVariables();
            InvalidateRuntimeState();
        }

        protected virtual void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                FlushPersistentVariables();
            }
        }

        protected virtual void OnApplicationQuit()
        {
            FlushPersistentVariables();
        }

        public BlueprintPersistenceStatus SavePersistentVariables(string slot, out string error)
        {
            BlueprintPersistenceStatus status = BlueprintPersistenceRuntime.Save(this, slot, out error);
            if (status == BlueprintPersistenceStatus.Success)
            {
                ClearPersistenceDirty();
            }
            return status;
        }

        public BlueprintPersistenceStatus LoadPersistentVariables(string slot, out string error)
        {
            return BlueprintPersistenceRuntime.Load(this, slot, out error);
        }

        public BlueprintPersistenceStatus DeletePersistentVariables(string slot, out string error)
        {
            return BlueprintPersistenceRuntime.Delete(this, slot, out error);
        }

        internal void MarkPersistenceDirty()
        {
            if (string.IsNullOrEmpty(PersistenceKey))
            {
                return;
            }

            _persistenceDirty = true;
            _nextPersistenceSaveTime = Time.unscaledTime + Mathf.Max(0f, persistentSaveDebounceSeconds);
        }

        internal void ClearPersistenceDirty()
        {
            _persistenceDirty = false;
        }

        private void FlushPersistentVariables()
        {
            if (!_persistenceDirty || string.IsNullOrEmpty(PersistenceKey))
            {
                return;
            }

            string error;
            BlueprintPersistenceStatus status = SavePersistentVariables(string.Empty, out error);
            if (status == BlueprintPersistenceStatus.Failed)
            {
                BlueprintLog.Warning("[Blueprint] Persistent variables could not be flushed for '" + PersistenceKey + "': " + error, this);
            }
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
            RebuildComponentRuntimeRecords();
        }

        public int ComponentRuntimeVersion
        {
            get { return _componentRuntimeVersion; }
        }

        public int ComponentRuntimeRecordCount
        {
            get { return _componentRuntimeRecords.Count; }
        }

        public int DynamicBlueprintTargetCacheCount
        {
            get { return _dynamicBlueprintTargetCache.Count; }
        }

        bool IBlueprintTargetHandleResolver.TryResolveBlueprintTarget(
            IBlueprintInstance requester,
            CompiledBlueprintTarget compiledTarget,
            string targetPath,
            out IBlueprintInstance instance,
            out bool ambiguous)
        {
            instance = null;
            ambiguous = false;
            targetPath = BlueprintCompiledTargetUtility.NormalizeAssetPath(targetPath);

            if (string.IsNullOrEmpty(targetPath))
            {
                return false;
            }

            BlueprintRunner current = this;
            HashSet<BlueprintRunner> visited = null;
            try
            {
                while (current != null)
                {
                    if (current.TryResolveBlueprintTargetInCurrentTree(
                        compiledTarget,
                        targetPath,
                        out instance,
                        out ambiguous))
                    {
                        return true;
                    }

                    // Ambiguity in the nearest tree is an error, not an inherited miss.
                    if (ambiguous)
                    {
                        return false;
                    }

                    BlueprintRunner owner = current.OwnerInstance as BlueprintRunner;
                    if (owner == null)
                    {
                        return false;
                    }

                    if (visited == null)
                    {
                        visited = HashSetPool<BlueprintRunner>.Get();
                        visited.Add(current);
                    }

                    if (!visited.Add(owner))
                    {
                        return false;
                    }

                    current = owner;
                }

                return false;
            }
            finally
            {
                if (visited != null)
                {
                    HashSetPool<BlueprintRunner>.Release(visited);
                }
            }
        }

        private bool TryResolveBlueprintTargetInCurrentTree(
            CompiledBlueprintTarget compiledTarget,
            string targetPath,
            out IBlueprintInstance instance,
            out bool ambiguous)
        {
            instance = null;
            ambiguous = false;

            if (compiledTarget != null &&
                compiledTarget.RuntimeVersion == _componentRuntimeVersion &&
                compiledTarget.RuntimeRecordIndex >= 0 &&
                BlueprintCompiledTargetUtility.PathEquals(compiledTarget.SourcePath, targetPath) &&
                TryGetVerifiedRecord(compiledTarget.RuntimeRecordIndex, compiledTarget, out instance))
            {
                return true;
            }

            int cachedRecordIndex;
            if (TryGetDynamicBlueprintTarget(targetPath, out cachedRecordIndex))
            {
                ambiguous = cachedRecordIndex == -2;
                if (cachedRecordIndex >= 0 && cachedRecordIndex < _componentRuntimeRecords.Count)
                {
                    instance = _componentRuntimeRecords[cachedRecordIndex].Instance;
                    return instance != null;
                }

                return false;
            }

            cachedRecordIndex = FindUniqueRecordIndex(null, targetPath, out ambiguous);
            _dynamicBlueprintTargetCache.Add(new DynamicBlueprintTargetRecord
            {
                StablePathId = BlueprintStableId.FromString(targetPath.ToLowerInvariant()),
                DebugPath = targetPath,
                RuntimeRecordIndex = ambiguous ? -2 : cachedRecordIndex
            });
            if (cachedRecordIndex >= 0)
            {
                instance = _componentRuntimeRecords[cachedRecordIndex].Instance;
                return instance != null;
            }

            return false;
        }

        private bool TryGetDynamicBlueprintTarget(string targetPath, out int recordIndex)
        {
            int stablePathId = BlueprintStableId.FromString(targetPath == null ? null : targetPath.ToLowerInvariant());
            for (int i = 0; i < _dynamicBlueprintTargetCache.Count; i++)
            {
                DynamicBlueprintTargetRecord record = _dynamicBlueprintTargetCache[i];
                if (record.StablePathId == stablePathId &&
                    string.Equals(record.DebugPath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    recordIndex = record.RuntimeRecordIndex;
                    return true;
                }
            }
            recordIndex = -1;
            return false;
        }

        private void RebuildComponentRuntimeRecords()
        {
            unchecked
            {
                _componentRuntimeVersion++;
                if (_componentRuntimeVersion <= 0)
                {
                    _componentRuntimeVersion = 1;
                }
            }

            _componentRuntimeRecords.Clear();
            _dynamicBlueprintTargetCache.Clear();
            AddRuntimeRecord(this, -1, -1);
            CollectComponentRuntimeRecords(this, 0);
            BindCompiledBlueprintTargets();
        }

        internal void RefreshComponentRuntimeRecords()
        {
            if (_blueprint != null)
            {
                RebuildComponentRuntimeRecords();
            }
        }

        private void CollectComponentRuntimeRecords(IBlueprintInstance owner, int ownerRecordIndex)
        {
            RuntimeBlueprint ownerBlueprint = owner == null ? null : owner.RuntimeBlueprint;
            if (ownerBlueprint == null)
            {
                return;
            }

            for (int componentIndex = 0; componentIndex < ownerBlueprint.Components.Count; componentIndex++)
            {
                BlueprintComponentDeclaration declaration = ownerBlueprint.Components[componentIndex];
                if (declaration == null || string.IsNullOrEmpty(declaration.Name))
                {
                    continue;
                }

                IBlueprintInstance component;
                if (!owner.TryGetBlueprintComponent(declaration.Name, out component) || component == null)
                {
                    continue;
                }

                int recordIndex = AddRuntimeRecord(component, ownerRecordIndex, componentIndex);
                CollectComponentRuntimeRecords(component, recordIndex);
            }
        }

        private int AddRuntimeRecord(IBlueprintInstance instance, int ownerRecordIndex, int componentIndex)
        {
            int recordIndex = _componentRuntimeRecords.Count;
            BlueprintCompiledAsset asset = instance == null ? null : instance.CompiledBlueprint;
            _componentRuntimeRecords.Add(new ComponentRuntimeRecord
            {
                RecordIndex = recordIndex,
                OwnerRecordIndex = ownerRecordIndex,
                ComponentIndex = componentIndex,
                SourceGuid = asset == null ? null : asset.SourceGuid,
                SourcePath = instance == null ? null : instance.SourcePath,
                Instance = instance
            });
            return recordIndex;
        }

        private void BindCompiledBlueprintTargets()
        {
            for (int requesterIndex = 0; requesterIndex < _componentRuntimeRecords.Count; requesterIndex++)
            {
                IBlueprintInstance requester = _componentRuntimeRecords[requesterIndex].Instance;
                RuntimeBlueprint blueprint = requester == null ? null : requester.RuntimeBlueprint;
                if (blueprint == null)
                {
                    continue;
                }

                foreach (RuntimeNode node in blueprint.NodesById.Values)
                {
                    CompiledBlueprintTarget target = node == null ? null : node.CompiledTarget;
                    if (target == null)
                    {
                        continue;
                    }

                    target.ClearRuntimeHandle();
                    int targetRecordIndex = ResolveCompiledHint(requesterIndex, target);
                    if (targetRecordIndex < 0)
                    {
                        bool ambiguous;
                        targetRecordIndex = FindUniqueRecordIndex(target.ExpectedSourceGuid, target.SourcePath, out ambiguous);
                    }

                    if (targetRecordIndex >= 0 && TryGetVerifiedRecord(targetRecordIndex, target, out _))
                    {
                        target.SetRuntimeHandle(_componentRuntimeVersion, targetRecordIndex);
                        UpdateRuntimeTraversal(requesterIndex, targetRecordIndex, target);
                    }
                }
            }
        }

        private int ResolveCompiledHint(int requesterRecordIndex, CompiledBlueprintTarget target)
        {
            if (target == null || target.OwnerTraversal < 0 || requesterRecordIndex < 0)
            {
                return -1;
            }

            int recordIndex = requesterRecordIndex;
            for (int i = 0; i < target.OwnerTraversal; i++)
            {
                recordIndex = recordIndex < 0 ? -1 : _componentRuntimeRecords[recordIndex].OwnerRecordIndex;
                if (recordIndex < 0)
                {
                    return -1;
                }
            }

            IReadOnlyList<int> path = target.ComponentIndexPath;
            if ((path == null || path.Count == 0) && target.ComponentIndex >= 0)
            {
                recordIndex = FindChildRecordIndex(recordIndex, target.ComponentIndex);
                return recordIndex;
            }

            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    recordIndex = FindChildRecordIndex(recordIndex, path[i]);
                    if (recordIndex < 0)
                    {
                        return -1;
                    }
                }
            }

            return recordIndex;
        }

        private int FindChildRecordIndex(int ownerRecordIndex, int componentIndex)
        {
            for (int i = 0; i < _componentRuntimeRecords.Count; i++)
            {
                ComponentRuntimeRecord record = _componentRuntimeRecords[i];
                if (record.OwnerRecordIndex == ownerRecordIndex && record.ComponentIndex == componentIndex)
                {
                    return record.RecordIndex;
                }
            }

            return -1;
        }

        private int FindUniqueRecordIndex(string expectedSourceGuid, string targetPath, out bool ambiguous)
        {
            ambiguous = false;
            int match = -1;
            for (int i = 0; i < _componentRuntimeRecords.Count; i++)
            {
                ComponentRuntimeRecord record = _componentRuntimeRecords[i];
                bool matches = !string.IsNullOrEmpty(expectedSourceGuid)
                    ? string.Equals(record.SourceGuid, expectedSourceGuid, StringComparison.OrdinalIgnoreCase)
                    : BlueprintCompiledTargetUtility.PathEquals(record.SourcePath, targetPath);
                if (!matches)
                {
                    continue;
                }

                if (match >= 0)
                {
                    ambiguous = true;
                    return -1;
                }

                match = i;
            }

            return match;
        }

        private bool TryGetVerifiedRecord(
            int recordIndex,
            CompiledBlueprintTarget target,
            out IBlueprintInstance instance)
        {
            instance = null;
            if (recordIndex < 0 || recordIndex >= _componentRuntimeRecords.Count)
            {
                return false;
            }

            instance = _componentRuntimeRecords[recordIndex].Instance;
            if (instance == null)
            {
                return false;
            }

            BlueprintCompiledAsset asset = instance.CompiledBlueprint;
            if (!string.IsNullOrEmpty(target.ExpectedSourceGuid) &&
                (asset == null || !string.Equals(asset.SourceGuid, target.ExpectedSourceGuid, StringComparison.OrdinalIgnoreCase)))
            {
                instance = null;
                return false;
            }

            if (string.IsNullOrEmpty(target.ExpectedSourceGuid) &&
                !string.IsNullOrEmpty(target.SourcePath) &&
                !BlueprintCompiledTargetUtility.PathEquals(instance.SourcePath, target.SourcePath))
            {
                instance = null;
                return false;
            }

            return true;
        }

        private void UpdateRuntimeTraversal(int requesterRecordIndex, int targetRecordIndex, CompiledBlueprintTarget target)
        {
            HashSet<int> targetAncestors = new HashSet<int>();
            int current = targetRecordIndex;
            while (current >= 0)
            {
                targetAncestors.Add(current);
                current = _componentRuntimeRecords[current].OwnerRecordIndex;
            }

            int ownerTraversal = 0;
            current = requesterRecordIndex;
            while (current >= 0 && !targetAncestors.Contains(current))
            {
                current = _componentRuntimeRecords[current].OwnerRecordIndex;
                ownerTraversal++;
            }

            if (current < 0)
            {
                return;
            }

            List<int> reversePath = new List<int>();
            int pathRecord = targetRecordIndex;
            while (pathRecord != current)
            {
                reversePath.Add(_componentRuntimeRecords[pathRecord].ComponentIndex);
                pathRecord = _componentRuntimeRecords[pathRecord].OwnerRecordIndex;
            }

            reversePath.Reverse();
            target.OwnerTraversal = ownerTraversal;
            target.ComponentIndexPath = reversePath;
            target.ComponentIndex = reversePath.Count == 0 ? -1 : reversePath[0];
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
