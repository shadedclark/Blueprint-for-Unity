using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlueprintSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Toggle))]
    public sealed class BlueprintToggleListener : MonoBehaviour
    {
        private readonly List<ToggleBinding> _bindings = new List<ToggleBinding>();
        private Toggle _toggle;

        public bool Value
        {
            get
            {
                EnsureToggle();
                return _toggle != null && _toggle.isOn;
            }
        }

        private void Awake()
        {
            EnsureToggle();
        }

        private void OnEnable()
        {
            EnsureToggle();
            if (_toggle != null)
            {
                _toggle.onValueChanged.AddListener(OnValueChanged);
            }
        }

        private void OnDisable()
        {
            if (_toggle != null)
            {
                _toggle.onValueChanged.RemoveListener(OnValueChanged);
            }
        }

        public void Register(string id, BlueprintExecutionContext context, RuntimeNode node)
        {
            if (string.IsNullOrEmpty(id) || context == null || node == null)
            {
                return;
            }

            for (int i = 0; i < _bindings.Count; i++)
            {
                ToggleBinding binding = _bindings[i];
                if (binding != null && binding.Id == id && ReferenceEquals(binding.Context, context))
                {
                    return;
                }
            }

            _bindings.Add(new ToggleBinding
            {
                Id = id,
                Context = context,
                Node = node
            });
        }

        private void OnValueChanged(bool value)
        {
            ExecuteAll("changed");
            ExecuteAll(value ? "turnedOn" : "turnedOff");
        }

        private void ExecuteAll(string outputPortId)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                ToggleBinding binding = _bindings[i];
                if (binding != null && binding.Context != null && binding.Node != null)
                {
                    binding.Context.ExecuteFromOutput(binding.Node, outputPortId);
                }
            }
        }

        private void EnsureToggle()
        {
            if (_toggle == null)
            {
                _toggle = GetComponent<Toggle>();
            }
        }

        private sealed class ToggleBinding
        {
            public string Id;
            public BlueprintExecutionContext Context;
            public RuntimeNode Node;
        }
    }
}
