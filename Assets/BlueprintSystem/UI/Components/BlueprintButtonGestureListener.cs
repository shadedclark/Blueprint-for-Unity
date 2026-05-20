using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlueprintSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class BlueprintButtonGestureListener : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private readonly List<ButtonGestureBinding> _bindings = new List<ButtonGestureBinding>();
        private Button _button;
        private Coroutine _longPressCoroutine;
        private Coroutine _singleClickCoroutine;
        private bool _isPressed;
        private bool _longPressTriggered;
        private float _lastClickTime = -999f;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        public void Register(string id, BlueprintExecutionContext context, RuntimeNode node, float longPressSeconds, float doubleClickSeconds)
        {
            if (string.IsNullOrEmpty(id) || context == null || node == null)
            {
                return;
            }

            for (int i = 0; i < _bindings.Count; i++)
            {
                ButtonGestureBinding binding = _bindings[i];
                if (binding != null && binding.Id == id && ReferenceEquals(binding.Context, context))
                {
                    binding.LongPressSeconds = Mathf.Max(0.05f, longPressSeconds);
                    binding.DoubleClickSeconds = Mathf.Max(0.05f, doubleClickSeconds);
                    return;
                }
            }

            _bindings.Add(new ButtonGestureBinding
            {
                Id = id,
                Context = context,
                Node = node,
                LongPressSeconds = Mathf.Max(0.05f, longPressSeconds),
                DoubleClickSeconds = Mathf.Max(0.05f, doubleClickSeconds)
            });
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable())
            {
                return;
            }

            _isPressed = true;
            _longPressTriggered = false;
            if (_longPressCoroutine != null)
            {
                StopCoroutine(_longPressCoroutine);
            }

            _longPressCoroutine = StartCoroutine(WaitForLongPress());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPressed)
            {
                return;
            }

            _isPressed = false;
            StopLongPressCoroutine();
            if (_longPressTriggered)
            {
                return;
            }

            RegisterClick();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPressed = false;
            StopLongPressCoroutine();
        }

        private IEnumerator WaitForLongPress()
        {
            yield return new WaitForSecondsRealtime(GetLongPressSeconds());
            if (!_isPressed || !IsInteractable())
            {
                yield break;
            }

            _longPressTriggered = true;
            CancelSingleClick();
            ExecuteAll("longPressed");
        }

        private void RegisterClick()
        {
            float now = Time.unscaledTime;
            if (_singleClickCoroutine != null && now - _lastClickTime <= GetDoubleClickSeconds())
            {
                CancelSingleClick();
                _lastClickTime = -999f;
                ExecuteAll("doubleClicked");
                return;
            }

            _lastClickTime = now;
            _singleClickCoroutine = StartCoroutine(WaitForSingleClick());
        }

        private IEnumerator WaitForSingleClick()
        {
            yield return new WaitForSecondsRealtime(GetDoubleClickSeconds());
            _singleClickCoroutine = null;
            _lastClickTime = -999f;
            ExecuteAll("clicked");
        }

        private void ExecuteAll(string outputPortId)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                ButtonGestureBinding binding = _bindings[i];
                if (binding != null && binding.Context != null && binding.Node != null)
                {
                    binding.Context.ExecuteFromOutput(binding.Node, outputPortId);
                }
            }
        }

        private void CancelSingleClick()
        {
            if (_singleClickCoroutine != null)
            {
                StopCoroutine(_singleClickCoroutine);
                _singleClickCoroutine = null;
            }
        }

        private void StopLongPressCoroutine()
        {
            if (_longPressCoroutine != null)
            {
                StopCoroutine(_longPressCoroutine);
                _longPressCoroutine = null;
            }
        }

        private float GetLongPressSeconds()
        {
            float result = -1f;
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i] != null)
                {
                    result = result < 0f ? _bindings[i].LongPressSeconds : Mathf.Min(result, _bindings[i].LongPressSeconds);
                }
            }

            return Mathf.Max(0.05f, result < 0f ? 0.5f : result);
        }

        private float GetDoubleClickSeconds()
        {
            float result = -1f;
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i] != null)
                {
                    result = result < 0f ? _bindings[i].DoubleClickSeconds : Mathf.Max(result, _bindings[i].DoubleClickSeconds);
                }
            }

            return Mathf.Max(0.05f, result < 0f ? 0.3f : result);
        }

        private bool IsInteractable()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            return _button != null && _button.IsInteractable();
        }

        private sealed class ButtonGestureBinding
        {
            public string Id;
            public BlueprintExecutionContext Context;
            public RuntimeNode Node;
            public float LongPressSeconds;
            public float DoubleClickSeconds;
        }
    }
}
