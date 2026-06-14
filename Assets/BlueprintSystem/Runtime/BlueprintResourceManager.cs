using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BlueprintSystem
{
    public interface IBlueprintResourceLoadOperation
    {
        bool IsDone { get; }
        float PercentComplete { get; }
        UnityEngine.Object Result { get; }
        string Error { get; }
        event Action<IBlueprintResourceLoadOperation> Completed;
        void Release();
    }

    public interface IBlueprintResourceLoadProvider
    {
        IBlueprintResourceLoadOperation LoadAsync(BlueprintResourceRegistryEntry entry);
    }

    public sealed class BlueprintResourceLoadHandle
    {
        private readonly BlueprintResourceManager _manager;

        internal BlueprintResourceLoadHandle(
            BlueprintResourceManager manager,
            BlueprintPrimaryResourceId id,
            BlueprintResourceScope scope)
        {
            _manager = manager;
            Id = id;
            Scope = scope;
        }

        public BlueprintPrimaryResourceId Id { get; private set; }
        public BlueprintResourceScope Scope { get; private set; }
        public bool IsReleased { get; private set; }
        public bool IsCancelled { get; private set; }
        public UnityEngine.Object Asset { get; internal set; }
        public string Error { get; internal set; }
        public BlueprintResourceLoadState State { get; internal set; }

        public void Cancel()
        {
            if (IsReleased)
            {
                return;
            }

            IsCancelled = true;
            State = BlueprintResourceLoadState.Cancelled;
            if (_manager != null)
            {
                _manager.CancelHandle(this);
            }
            else
            {
                IsReleased = true;
            }
        }

        public void Release()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            if (_manager != null)
            {
                _manager.ReleaseHandle(this);
            }
        }

        internal void MarkReleased()
        {
            IsReleased = true;
        }
    }

    public sealed class BlueprintResourceGroupLoadHandle
    {
        private int _remaining;
        private int _failed;

        internal BlueprintResourceGroupLoadHandle(string preloadGroup, List<BlueprintResourceLoadHandle> handles, int expectedCount)
        {
            PreloadGroup = preloadGroup;
            Handles = handles == null ? new List<BlueprintResourceLoadHandle>() : handles;
            _remaining = Mathf.Max(0, expectedCount);
        }

        public string PreloadGroup { get; private set; }
        public List<BlueprintResourceLoadHandle> Handles { get; private set; }
        public bool IsDone { get; private set; }
        public bool Succeeded { get; private set; }
        public string Error { get; private set; }
        public event Action<BlueprintResourceGroupLoadHandle> Completed;

        internal void MarkOneComplete(BlueprintResourceLoadHandle handle)
        {
            if (IsDone)
            {
                return;
            }

            if (_remaining <= 0)
            {
                IsDone = true;
                Succeeded = _failed == 0;
                Error = Succeeded ? string.Empty : _failed + " resource(s) failed or were cancelled.";
                if (Completed != null)
                {
                    Completed(this);
                }
                return;
            }

            if (handle == null || handle.State == BlueprintResourceLoadState.Failed || handle.State == BlueprintResourceLoadState.Cancelled)
            {
                _failed++;
            }

            _remaining--;
            if (_remaining > 0)
            {
                return;
            }

            IsDone = true;
            Succeeded = _failed == 0;
            Error = Succeeded ? string.Empty : _failed + " resource(s) failed or were cancelled.";
            if (Completed != null)
            {
                Completed(this);
            }
        }

        public void Cancel()
        {
            for (int i = 0; i < Handles.Count; i++)
            {
                if (Handles[i] != null)
                {
                    Handles[i].Cancel();
                }
            }
        }
    }

    public sealed class BlueprintResourceManager
    {
        private const string DefaultRegistryResourcePath = "BlueprintResourceRegistry";

        private sealed class LoadRecord
        {
            public BlueprintResourceRegistryEntry Entry;
            public BlueprintResourceLoadState State;
            public UnityEngine.Object Asset;
            public string Error;
            public int RefCount;
            public IBlueprintResourceLoadOperation Operation;
            public readonly List<BlueprintResourceLoadHandle> Handles = new List<BlueprintResourceLoadHandle>();
        }

        private static BlueprintResourceManager _instance;

        private readonly Dictionary<BlueprintPrimaryResourceId, LoadRecord> _records =
            new Dictionary<BlueprintPrimaryResourceId, LoadRecord>();

        private readonly List<LoadRecord> _queue = new List<LoadRecord>();
        private readonly Dictionary<BlueprintResourceScope, List<BlueprintResourceLoadHandle>> _handlesByScope =
            new Dictionary<BlueprintResourceScope, List<BlueprintResourceLoadHandle>>();

        private BlueprintResourceRegistryAsset _registry;
        private IBlueprintResourceLoadProvider _provider;
        private int _activeLoads;
        private float _loadedMemoryMb;

        public static BlueprintResourceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new BlueprintResourceManager();
                }

                return _instance;
            }
        }

        public BlueprintResourceRegistryAsset Registry
        {
            get
            {
                EnsureRegistry();
                return _registry;
            }
        }

        public IBlueprintResourceLoadProvider Provider
        {
            get
            {
                if (_provider == null)
                {
                    _provider = new AddressablesBlueprintResourceLoadProvider();
                }

                return _provider;
            }
            set { _provider = value; }
        }

        public void SetRegistry(BlueprintResourceRegistryAsset registry)
        {
            _registry = registry;
            ClearRuntimeState();
        }

        public void ClearRuntimeState()
        {
            foreach (KeyValuePair<BlueprintPrimaryResourceId, LoadRecord> pair in _records)
            {
                if (pair.Value != null && pair.Value.Operation != null)
                {
                    pair.Value.Operation.Release();
                }
            }

            _records.Clear();
            _queue.Clear();
            _handlesByScope.Clear();
            _activeLoads = 0;
            _loadedMemoryMb = 0f;
        }

        public BlueprintResourceLoadHandle LoadAsync(
            BlueprintPrimaryResourceId id,
            BlueprintResourceScope scope,
            Action<BlueprintResourceLoadHandle> completed = null)
        {
            EnsureRegistry();
            BlueprintResourceLoadHandle handle = new BlueprintResourceLoadHandle(this, id, scope);

            if (_registry == null)
            {
                CompleteHandle(handle, BlueprintResourceLoadState.Failed, null, "No BlueprintResourceRegistry is loaded.", completed);
                return handle;
            }

            BlueprintResourceRegistryEntry entry;
            if (!_registry.TryGet(id, out entry) || entry == null)
            {
                CompleteHandle(handle, BlueprintResourceLoadState.Failed, null, "Unknown resource '" + id + "'.", completed);
                return handle;
            }

            LoadRecord record;
            if (!_records.TryGetValue(id, out record))
            {
                record = new LoadRecord
                {
                    Entry = entry,
                    State = BlueprintResourceLoadState.Unloaded
                };
                _records[id] = record;
            }

            record.RefCount++;
            record.Handles.Add(handle);
            AddHandleToScope(handle);

            if (record.State == BlueprintResourceLoadState.Loaded)
            {
                CompleteHandle(handle, BlueprintResourceLoadState.Loaded, record.Asset, string.Empty, completed);
                return handle;
            }

            if (record.State == BlueprintResourceLoadState.Failed)
            {
                CompleteHandle(handle, BlueprintResourceLoadState.Failed, null, record.Error, completed);
                return handle;
            }

            if (completed != null)
            {
                Action<BlueprintResourceLoadHandle> callback = completed;
                ResourceHandleCompletionStore.Add(handle, callback);
            }

            if (record.State == BlueprintResourceLoadState.Unloaded)
            {
                Enqueue(record);
            }

            handle.State = record.State;
            PumpQueue();
            return handle;
        }

        public BlueprintResourceGroupLoadHandle PreloadGroupAsync(
            string preloadGroup,
            BlueprintResourceScope scope,
            Action<BlueprintResourceGroupLoadHandle> completed = null)
        {
            EnsureRegistry();
            List<BlueprintResourceLoadHandle> handles = new List<BlueprintResourceLoadHandle>();
            BlueprintResourceRegistryEntry[] entries = _registry == null
                ? new BlueprintResourceRegistryEntry[0]
                : _registry.GetEntriesInPreloadGroup(preloadGroup);
            BlueprintResourceGroupLoadHandle groupHandle = new BlueprintResourceGroupLoadHandle(preloadGroup, handles, entries.Length);
            if (completed != null)
            {
                groupHandle.Completed += completed;
            }

            if (entries.Length == 0)
            {
                groupHandle.MarkOneComplete(null);
                return groupHandle;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                BlueprintPrimaryResourceId id = entries[i].Id;
                BlueprintResourceLoadHandle handle = LoadAsync(id, scope, groupHandle.MarkOneComplete);
                handles.Add(handle);
            }

            return groupHandle;
        }

        public void Release(BlueprintPrimaryResourceId id)
        {
            LoadRecord record;
            if (!_records.TryGetValue(id, out record) || record == null)
            {
                return;
            }

            for (int i = 0; i < record.Handles.Count; i++)
            {
                BlueprintResourceLoadHandle handle = record.Handles[i];
                if (handle != null && !handle.IsReleased)
                {
                    handle.Release();
                    return;
                }
            }

            ReleaseRecordReference(record, null);
        }

        public void ReleaseScope(BlueprintResourceScope scope)
        {
            List<BlueprintResourceLoadHandle> handles;
            if (!_handlesByScope.TryGetValue(scope, out handles))
            {
                return;
            }

            BlueprintResourceLoadHandle[] copy = handles.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i] != null)
                {
                    copy[i].Release();
                }
            }
        }

        public BlueprintResourceLoadState GetLoadState(BlueprintPrimaryResourceId id)
        {
            LoadRecord record;
            return _records.TryGetValue(id, out record) && record != null ? record.State : BlueprintResourceLoadState.Unloaded;
        }

        public UnityEngine.Object GetLoadedAsset(BlueprintPrimaryResourceId id)
        {
            LoadRecord record;
            return _records.TryGetValue(id, out record) && record != null ? record.Asset : null;
        }

        public string GetLastError(BlueprintPrimaryResourceId id)
        {
            LoadRecord record;
            return _records.TryGetValue(id, out record) && record != null ? record.Error : string.Empty;
        }

        public string GetMetadata(BlueprintPrimaryResourceId id, string key)
        {
            EnsureRegistry();
            BlueprintResourceRegistryEntry entry;
            if (_registry == null || !_registry.TryGet(id, out entry) || entry == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(key))
            {
                return entry.MetadataJson ?? string.Empty;
            }

            try
            {
                Dictionary<string, object> metadata = BlueprintJson.DeserializeObject(entry.MetadataJson);
                object value;
                return metadata != null && metadata.TryGetValue(key, out value) && value != null
                    ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
                    : string.Empty;
            }
            catch (BlueprintJsonException)
            {
                return string.Empty;
            }
        }

        internal void ReleaseHandle(BlueprintResourceLoadHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            RemoveHandleFromScope(handle);
            ResourceHandleCompletionStore.Remove(handle);

            LoadRecord record;
            if (!_records.TryGetValue(handle.Id, out record) || record == null)
            {
                return;
            }

            ReleaseRecordReference(record, handle);
        }

        internal void CancelHandle(BlueprintResourceLoadHandle handle)
        {
            if (handle == null || handle.IsReleased)
            {
                return;
            }

            Action<BlueprintResourceLoadHandle> completed = ResourceHandleCompletionStore.Get(handle);
            ResourceHandleCompletionStore.Remove(handle);
            CompleteHandle(handle, BlueprintResourceLoadState.Cancelled, null, "Resource load cancelled.", completed);
            handle.MarkReleased();
            RemoveHandleFromScope(handle);

            LoadRecord record;
            if (!_records.TryGetValue(handle.Id, out record) || record == null)
            {
                return;
            }

            ReleaseRecordReference(record, handle);
        }

        private void Enqueue(LoadRecord record)
        {
            if (record == null || record.State != BlueprintResourceLoadState.Unloaded)
            {
                return;
            }

            record.State = BlueprintResourceLoadState.Queued;
            _queue.Add(record);
            _queue.Sort(CompareQueuedRecords);
        }

        private void PumpQueue()
        {
            EnsureRegistry();
            int maxConcurrent = _registry == null ? 4 : _registry.MaxConcurrentLoads;
            while (_queue.Count > 0 && _activeLoads < maxConcurrent)
            {
                LoadRecord record = PopNextLoadableRecord();
                if (record == null)
                {
                    return;
                }

                StartLoad(record);
            }
        }

        private LoadRecord PopNextLoadableRecord()
        {
            float maxMemory = _registry == null ? 0f : _registry.MaxLoadedMemoryMb;
            for (int i = 0; i < _queue.Count; i++)
            {
                LoadRecord record = _queue[i];
                if (record == null || record.RefCount <= 0)
                {
                    _queue.RemoveAt(i--);
                    continue;
                }

                float cost = Mathf.Max(0f, record.Entry == null ? 0f : record.Entry.MemoryBudgetMb);
                if (maxMemory > 0f && cost > 0f && _loadedMemoryMb + cost > maxMemory)
                {
                    continue;
                }

                _queue.RemoveAt(i);
                return record;
            }

            return null;
        }

        private void StartLoad(LoadRecord record)
        {
            if (record == null || record.Entry == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(record.Entry.MainAssetAddress))
            {
                CompleteRecord(record, BlueprintResourceLoadState.Failed, null, "Resource '" + record.Entry.Id + "' has no Addressables address.");
                return;
            }

            record.State = BlueprintResourceLoadState.Loading;
            _activeLoads++;
            for (int i = 0; i < record.Handles.Count; i++)
            {
                if (record.Handles[i] != null && !record.Handles[i].IsReleased)
                {
                    record.Handles[i].State = BlueprintResourceLoadState.Loading;
                }
            }

            try
            {
                record.Operation = Provider.LoadAsync(record.Entry);
                if (record.Operation == null)
                {
                    CompleteRecord(record, BlueprintResourceLoadState.Failed, null, "Resource provider returned no operation.");
                    return;
                }

                record.Operation.Completed += delegate(IBlueprintResourceLoadOperation operation)
                {
                    if (record.State != BlueprintResourceLoadState.Loading)
                    {
                        return;
                    }

                    _activeLoads = Mathf.Max(0, _activeLoads - 1);
                    if (operation != null && string.IsNullOrEmpty(operation.Error) && operation.Result != null)
                    {
                        CompleteRecord(record, BlueprintResourceLoadState.Loaded, operation.Result, string.Empty);
                    }
                    else
                    {
                        string error = operation == null ? "Resource provider failed." : operation.Error;
                        CompleteRecord(record, BlueprintResourceLoadState.Failed, null, string.IsNullOrEmpty(error) ? "Resource provider failed." : error);
                    }

                    PumpQueue();
                };

                if (record.Operation.IsDone)
                {
                    _activeLoads = Mathf.Max(0, _activeLoads - 1);
                    if (string.IsNullOrEmpty(record.Operation.Error) && record.Operation.Result != null)
                    {
                        CompleteRecord(record, BlueprintResourceLoadState.Loaded, record.Operation.Result, string.Empty);
                    }
                    else
                    {
                        string error = record.Operation.Error;
                        CompleteRecord(record, BlueprintResourceLoadState.Failed, null, string.IsNullOrEmpty(error) ? "Resource provider failed." : error);
                    }

                    PumpQueue();
                }
            }
            catch (Exception exception)
            {
                _activeLoads = Mathf.Max(0, _activeLoads - 1);
                CompleteRecord(record, BlueprintResourceLoadState.Failed, null, exception.Message);
                PumpQueue();
            }
        }

        private void CompleteRecord(LoadRecord record, BlueprintResourceLoadState state, UnityEngine.Object asset, string error)
        {
            if (record == null)
            {
                return;
            }

            record.State = state;
            record.Asset = asset;
            record.Error = error;
            if (state == BlueprintResourceLoadState.Loaded && record.Entry != null)
            {
                _loadedMemoryMb += Mathf.Max(0f, record.Entry.MemoryBudgetMb);
            }

            BlueprintResourceLoadHandle[] handles = record.Handles.ToArray();
            for (int i = 0; i < handles.Length; i++)
            {
                BlueprintResourceLoadHandle handle = handles[i];
                if (handle == null || handle.IsReleased)
                {
                    continue;
                }

                CompleteHandle(handle, state, asset, error, ResourceHandleCompletionStore.Get(handle));
                ResourceHandleCompletionStore.Remove(handle);
            }

            if (record.RefCount <= 0)
            {
                ReleaseLoadedRecord(record);
            }
        }

        private void CompleteHandle(
            BlueprintResourceLoadHandle handle,
            BlueprintResourceLoadState state,
            UnityEngine.Object asset,
            string error,
            Action<BlueprintResourceLoadHandle> completed)
        {
            if (handle == null || handle.IsReleased)
            {
                return;
            }

            handle.State = state;
            handle.Asset = asset;
            handle.Error = error;
            if (completed != null)
            {
                completed(handle);
            }
        }

        private void ReleaseRecordReference(LoadRecord record, BlueprintResourceLoadHandle handle)
        {
            if (record == null)
            {
                return;
            }

            if (handle != null)
            {
                record.Handles.Remove(handle);
            }

            record.RefCount = Mathf.Max(0, record.RefCount - 1);
            if (record.RefCount > 0)
            {
                return;
            }

            if (record.State == BlueprintResourceLoadState.Queued)
            {
                _queue.Remove(record);
                record.Error = string.Empty;
                record.State = BlueprintResourceLoadState.Unloaded;
                return;
            }

            if (record.State == BlueprintResourceLoadState.Loaded || record.State == BlueprintResourceLoadState.Failed)
            {
                ReleaseLoadedRecord(record);
            }
        }

        private void ReleaseLoadedRecord(LoadRecord record)
        {
            if (record == null)
            {
                return;
            }

            if (record.Operation != null)
            {
                record.Operation.Release();
                record.Operation = null;
            }

            if (record.State == BlueprintResourceLoadState.Loaded && record.Entry != null)
            {
                _loadedMemoryMb = Mathf.Max(0f, _loadedMemoryMb - Mathf.Max(0f, record.Entry.MemoryBudgetMb));
            }

            record.Asset = null;
            record.Error = string.Empty;
            record.State = BlueprintResourceLoadState.Unloaded;
        }

        private void AddHandleToScope(BlueprintResourceLoadHandle handle)
        {
            List<BlueprintResourceLoadHandle> handles;
            if (!_handlesByScope.TryGetValue(handle.Scope, out handles))
            {
                handles = new List<BlueprintResourceLoadHandle>();
                _handlesByScope[handle.Scope] = handles;
            }

            handles.Add(handle);
        }

        private void RemoveHandleFromScope(BlueprintResourceLoadHandle handle)
        {
            List<BlueprintResourceLoadHandle> handles;
            if (_handlesByScope.TryGetValue(handle.Scope, out handles))
            {
                handles.Remove(handle);
            }
        }

        private void EnsureRegistry()
        {
            if (_registry != null)
            {
                return;
            }

            _registry = Resources.Load<BlueprintResourceRegistryAsset>(DefaultRegistryResourcePath);
        }

        private static int CompareQueuedRecords(LoadRecord left, LoadRecord right)
        {
            int priority = right.Entry.Priority.CompareTo(left.Entry.Priority);
            return priority != 0 ? priority : string.CompareOrdinal(left.Entry.Id.ToString(), right.Entry.Id.ToString());
        }

        private sealed class AddressablesBlueprintResourceLoadOperation : IBlueprintResourceLoadOperation
        {
            private AsyncOperationHandle<UnityEngine.Object> _handle;

            public AddressablesBlueprintResourceLoadOperation(AsyncOperationHandle<UnityEngine.Object> handle)
            {
                _handle = handle;
                _handle.Completed += OnCompleted;
            }

            public bool IsDone
            {
                get { return _handle.IsDone; }
            }

            public float PercentComplete
            {
                get { return _handle.PercentComplete; }
            }

            public UnityEngine.Object Result
            {
                get { return _handle.Status == AsyncOperationStatus.Succeeded ? _handle.Result : null; }
            }

            public string Error
            {
                get
                {
                    if (_handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        return string.Empty;
                    }

                    return _handle.OperationException == null ? string.Empty : _handle.OperationException.Message;
                }
            }

            public event Action<IBlueprintResourceLoadOperation> Completed;

            public void Release()
            {
                if (_handle.IsValid())
                {
                    Addressables.Release(_handle);
                }
            }

            private void OnCompleted(AsyncOperationHandle<UnityEngine.Object> handle)
            {
                if (Completed != null)
                {
                    Completed(this);
                }
            }
        }

        private sealed class AddressablesBlueprintResourceLoadProvider : IBlueprintResourceLoadProvider
        {
            public IBlueprintResourceLoadOperation LoadAsync(BlueprintResourceRegistryEntry entry)
            {
                return new AddressablesBlueprintResourceLoadOperation(Addressables.LoadAssetAsync<UnityEngine.Object>(entry.MainAssetAddress));
            }
        }

        private static class ResourceHandleCompletionStore
        {
            private static readonly Dictionary<BlueprintResourceLoadHandle, Action<BlueprintResourceLoadHandle>> Callbacks =
                new Dictionary<BlueprintResourceLoadHandle, Action<BlueprintResourceLoadHandle>>();

            public static void Add(BlueprintResourceLoadHandle handle, Action<BlueprintResourceLoadHandle> callback)
            {
                if (handle != null && callback != null)
                {
                    Callbacks[handle] = callback;
                }
            }

            public static Action<BlueprintResourceLoadHandle> Get(BlueprintResourceLoadHandle handle)
            {
                Action<BlueprintResourceLoadHandle> callback;
                return handle != null && Callbacks.TryGetValue(handle, out callback) ? callback : null;
            }

            public static void Remove(BlueprintResourceLoadHandle handle)
            {
                if (handle != null)
                {
                    Callbacks.Remove(handle);
                }
            }
        }
    }
}
