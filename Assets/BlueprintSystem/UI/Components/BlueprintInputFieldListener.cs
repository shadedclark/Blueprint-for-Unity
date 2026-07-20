using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BlueprintSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_InputField))]
    public sealed class BlueprintInputFieldListener : MonoBehaviour
    {
        private readonly List<InputFieldBinding> _bindings = new List<InputFieldBinding>();
        private TMP_InputField _inputField;

        public string Value
        {
            get
            {
                EnsureInputField();
                return _inputField == null ? string.Empty : _inputField.text ?? string.Empty;
            }
        }

        private void Awake()
        {
            EnsureInputField();
        }

        private void OnEnable()
        {
            EnsureInputField();
            if (_inputField == null)
            {
                return;
            }

            _inputField.onValueChanged.AddListener(OnValueChanged);
            _inputField.onEndEdit.AddListener(OnEndEdit);
        }

        private void OnDisable()
        {
            if (_inputField == null)
            {
                return;
            }

            _inputField.onValueChanged.RemoveListener(OnValueChanged);
            _inputField.onEndEdit.RemoveListener(OnEndEdit);
        }

        public void Register(string id, BlueprintExecutionContext context, RuntimeNode node)
        {
            if (string.IsNullOrEmpty(id) || context == null || node == null)
            {
                return;
            }

            for (int i = 0; i < _bindings.Count; i++)
            {
                InputFieldBinding binding = _bindings[i];
                if (binding == null || binding.Id != id)
                {
                    continue;
                }

                if (ReferenceEquals(binding.Context, context) || binding.Context == null ||
                    !binding.Context.IsExecutionGenerationCurrent(binding.ExecutionGeneration))
                {
                    binding.Context = context;
                    binding.Node = node;
                    binding.ExecutionGeneration = context.ExecutionGeneration;
                    return;
                }
            }

            _bindings.Add(new InputFieldBinding
            {
                Id = id,
                Context = context,
                Node = node,
                ExecutionGeneration = context.ExecutionGeneration
            });
        }

        private void OnValueChanged(string value)
        {
            ExecuteAll("changed");
        }

        private void OnEndEdit(string value)
        {
            ExecuteAll("endEdit");
        }

        private void ExecuteAll(string outputPortId)
        {
            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                InputFieldBinding binding = _bindings[i];
                if (binding == null || binding.Context == null || binding.Node == null ||
                    !binding.Context.IsExecutionGenerationCurrent(binding.ExecutionGeneration))
                {
                    _bindings.RemoveAt(i);
                    continue;
                }

                binding.Context.ClearValueCache();
                binding.Context.ExecuteFromOutput(binding.Node, outputPortId);
            }
        }

        private void EnsureInputField()
        {
            if (_inputField == null)
            {
                _inputField = GetComponent<TMP_InputField>();
            }
        }

        private sealed class InputFieldBinding
        {
            public string Id;
            public BlueprintExecutionContext Context;
            public RuntimeNode Node;
            public int ExecutionGeneration;
        }
    }
}
