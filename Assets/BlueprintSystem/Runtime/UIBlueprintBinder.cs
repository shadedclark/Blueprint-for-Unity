using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    [Serializable]
    public sealed class UIBindingEntry
    {
        public string Name;
        public UnityEngine.Object Target;
    }

    public sealed class UIBlueprintBinder : BlueprintRunner, IBlueprintBindingResolver
    {
        [SerializeField] private List<UIBindingEntry> bindings = new List<UIBindingEntry>();
        [SerializeField] private bool triggerOnEnable = true;
        [SerializeField] private string enableEventName = "OnOpen";
        [SerializeField] private bool triggerOnDisable = true;
        [SerializeField] private string disableEventName = "OnClose";

        private readonly Dictionary<string, UnityEngine.Object> _bindingsByName = new Dictionary<string, UnityEngine.Object>();

        protected override IBlueprintBindingResolver BindingResolver
        {
            get { return this; }
        }

        protected override void Awake()
        {
            RebuildBindingCache();
            base.Awake();
        }

        private void OnEnable()
        {
            if (triggerOnEnable)
            {
                TriggerEvent(enableEventName);
            }
        }

        private void OnDisable()
        {
            if (triggerOnDisable)
            {
                TriggerEvent(disableEventName);
            }

            ClearReactiveBindings();
        }

        public void RebuildBindingCache()
        {
            _bindingsByName.Clear();
            for (int i = 0; i < bindings.Count; i++)
            {
                UIBindingEntry entry = bindings[i];
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
    }
}
