using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlueprintSystem
{
    public sealed class UISetTextExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetText"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            string value = context.GetInputValue(node, "value", string.Empty);
            TMP_Text text = context.BindingResolver.Resolve<TMP_Text>(target);
            if (text == null)
            {
                return BlueprintExecResult.Error("UI.SetText could not resolve TMP_Text binding '" + target + "'.");
            }

            text.text = value;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetInputFieldTextExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetInputFieldText"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            string value = context.GetInputValue(node, "value", string.Empty);
            bool notify = context.GetInputValue(node, "notify", false);
            TMP_InputField inputField = context.BindingResolver.Resolve<TMP_InputField>(target);
            if (inputField == null)
            {
                return BlueprintExecResult.Error("UI.SetInputFieldText could not resolve TMP_InputField binding '" + target + "'.");
            }

            if (notify)
            {
                inputField.text = value;
            }
            else
            {
                inputField.SetTextWithoutNotify(value);
            }

            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UIBindInputFieldChangedExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.BindInputFieldChanged"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            TMP_InputField inputField = context.BindingResolver.Resolve<TMP_InputField>(target);
            if (inputField == null)
            {
                return BlueprintExecResult.Error("UI.BindInputFieldChanged could not resolve TMP_InputField binding '" + target + "'.");
            }

            BlueprintInputFieldListener listener = inputField.GetComponent<BlueprintInputFieldListener>();
            if (listener == null)
            {
                listener = inputField.gameObject.AddComponent<BlueprintInputFieldListener>();
            }

            listener.Register("input-field:" + node.Id + ":" + target, context, node);
            return BlueprintExecResult.Continue("bound");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "value")
            {
                return base.Evaluate(context, node, outputPortId);
            }

            string target = context.GetInputValue(node, "target", string.Empty);
            TMP_InputField inputField = context.BindingResolver.Resolve<TMP_InputField>(target);
            return inputField == null ? string.Empty : inputField.text ?? string.Empty;
        }
    }

    public sealed class UIBindTextExecutor : BlueprintNodeExecutor, IBlueprintReactiveBindingRestorer
    {
        public override string ExecutorId
        {
            get { return "UI.BindText"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            TMP_Text text = context.BindingResolver.Resolve<TMP_Text>(target);
            if (text == null)
            {
                return BlueprintExecResult.Error("UI.BindText could not resolve TMP_Text binding '" + target + "'.");
            }

            BlueprintReactiveBindingRuntime.Register(
                context,
                new UITextReactiveBinding(context, node, target, "value", "variableName", "variableTarget"));
            return BlueprintExecResult.Continue("bound");
        }

        public BlueprintExecResult RestoreReactiveBinding(BlueprintExecutionContext context, RuntimeNode node)
        {
            return Execute(context, node);
        }
    }

    internal sealed class UITextReactiveBinding : IBlueprintReactiveBinding, IBlueprintReactiveBindingDependency, IBlueprintReactiveBindingSource
    {
        private readonly RuntimeNode _node;
        private readonly string _targetBindingName;
        private readonly string _valuePortId;
        private readonly string _variableNamePortId;
        private readonly string _variableTargetPortId;
        private readonly int _executionGeneration;

        public UITextReactiveBinding(
            BlueprintExecutionContext context,
            RuntimeNode node,
            string targetBindingName,
            string valuePortId,
            string variableNamePortId,
            string variableTargetPortId)
        {
            Context = context;
            _node = node;
            _targetBindingName = targetBindingName;
            _valuePortId = valuePortId;
            _variableNamePortId = variableNamePortId;
            _variableTargetPortId = variableTargetPortId;
            _executionGeneration = context == null ? 0 : context.ExecutionGeneration;
            Key = BlueprintReactiveBindingRuntime.CreateBindingKey(context, node, targetBindingName, "text");
        }

        public string Key { get; private set; }
        public BlueprintExecutionContext Context { get; private set; }
        public string SourceNodeId
        {
            get { return _node == null ? null : _node.Id; }
        }

        public void Apply()
        {
            if (!IsAlive())
            {
                return;
            }

            TMP_Text text = Context.BindingResolver.Resolve<TMP_Text>(_targetBindingName);
            if (text == null)
            {
                Context.Logger.Error("UI.BindText could not resolve TMP_Text binding '" + _targetBindingName + "'.");
                return;
            }

            Context.ClearValueCache();
            object value;
            if (!TryGetVariableValue(out value))
            {
                value = Context.GetInputValue(_node, _valuePortId);
            }

            text.text = BlueprintTypeUtility.ConvertValue(value, string.Empty);
        }

        public bool IsAlive()
        {
            return Context != null &&
                   _node != null &&
                   Context.BindingResolver != null &&
                   Context.IsExecutionGenerationCurrent(_executionGeneration) &&
                   Context.BindingResolver.Resolve<TMP_Text>(_targetBindingName) != null;
        }

        public bool DependsOnInstance(IBlueprintInstance instance)
        {
            if (instance == null || !HasVariableName())
            {
                return false;
            }

            IBlueprintInstance variableTarget = ResolveVariableTargetInstance(false);
            while (variableTarget != null)
            {
                if (object.ReferenceEquals(variableTarget, instance))
                {
                    return true;
                }

                variableTarget = variableTarget.OwnerInstance;
            }

            return false;
        }

        private bool TryGetVariableValue(out object value)
        {
            value = null;
            string variableName = GetVariableName();
            if (string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            object targetValue = Context.GetInputValue(_node, _variableTargetPortId);
            if (IsEmptyVariableTarget(targetValue))
            {
                if (Context.Variables != null && Context.Variables.TryGet(variableName, out value))
                {
                    return true;
                }

                Context.Logger.Error("UI.BindText node '" + _node.Id + "' references unknown variable '" + variableName + "'.");
                return true;
            }

            IBlueprintInstance targetInstance = BlueprintAccessUtility.ResolveRuntimeInstanceTarget(Context, _node, targetValue, true);
            if (targetInstance == null)
            {
                return true;
            }

            BlueprintAccessUtility.TryGetExposedVariableValue(
                Context,
                targetInstance,
                variableName,
                out value,
                true);
            return true;
        }

        private bool HasVariableName()
        {
            return !string.IsNullOrEmpty(GetVariableName());
        }

        private string GetVariableName()
        {
            return Context == null || _node == null ? string.Empty : Context.GetInputValue(_node, _variableNamePortId, string.Empty);
        }

        private IBlueprintInstance ResolveVariableTargetInstance(bool logWarnings)
        {
            if (Context == null)
            {
                return null;
            }

            object targetValue = Context.GetInputValue(_node, _variableTargetPortId);
            if (!IsEmptyVariableTarget(targetValue))
            {
                return BlueprintAccessUtility.ResolveRuntimeInstanceTarget(Context, _node, targetValue, logWarnings);
            }

            if (Context.Instance != null)
            {
                return Context.Instance;
            }

            if (Context.OwnerInstance != null)
            {
                return Context.OwnerInstance;
            }

            BlueprintRunner runner = Context.OwnerComponent as BlueprintRunner;
            if (runner == null && Context.Owner != null)
            {
                runner = Context.Owner.GetComponent<BlueprintRunner>();
            }

            return runner;
        }

        private static bool IsEmptyVariableTarget(object value)
        {
            string text = value as string;
            return value == null || (text != null && string.IsNullOrEmpty(text));
        }
    }

    public sealed class UISetVisibleExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetVisible"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            object target = context.GetInputValue(node, "target");
            bool value = context.GetInputValue(node, "value", true);
            GameObject gameObject = GameExecutorBindingUtility.ResolveBinding<GameObject>(context, target);
            if (gameObject == null)
            {
                return BlueprintExecResult.Error("UI.SetVisible could not resolve binding '" + target + "'.");
            }

            gameObject.SetActive(value);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetImageSpriteExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetImageSprite"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            string value = context.GetInputValue(node, "value", string.Empty);
            Image image = context.BindingResolver.Resolve<Image>(target);
            if (image == null)
            {
                return BlueprintExecResult.Error("UI.SetImageSprite could not resolve Image binding '" + target + "'.");
            }

            Sprite sprite = context.BindingResolver.Resolve<Sprite>(value);
            if (sprite == null)
            {
                return BlueprintExecResult.Error("UI.SetImageSprite could not resolve Sprite binding '" + value + "'.");
            }

            image.sprite = sprite;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISpriteBindingExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SpriteBinding"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId == "value")
            {
                return context.GetInputValue(node, "sprite", string.Empty);
            }

            return base.Evaluate(context, node, outputPortId);
        }
    }

    public sealed class UISetInteractableExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetInteractable"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            bool value = context.GetInputValue(node, "value", true);
            Selectable selectable = context.BindingResolver.Resolve<Selectable>(target);
            if (selectable == null)
            {
                return BlueprintExecResult.Error("UI.SetInteractable could not resolve Selectable binding '" + target + "'.");
            }

            selectable.interactable = value;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetGraphicColorExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetGraphicColor"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Color value = UIExecutorValueUtility.GetColorInput(context, node, "value", Color.white);
            Graphic graphic = context.BindingResolver.Resolve<Graphic>(target);
            if (graphic == null)
            {
                return BlueprintExecResult.Error("UI.SetGraphicColor could not resolve Graphic binding '" + target + "'.");
            }

            graphic.color = value;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetGraphicEnabledExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetGraphicEnabled"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            bool value = context.GetInputValue(node, "value", true);
            Graphic graphic = context.BindingResolver.Resolve<Graphic>(target);
            if (graphic == null)
            {
                return BlueprintExecResult.Error("UI.SetGraphicEnabled could not resolve Graphic binding '" + target + "'.");
            }

            graphic.enabled = value;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetGraphicRaycastTargetExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetGraphicRaycastTarget"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            bool value = context.GetInputValue(node, "value", true);
            Graphic graphic = context.BindingResolver.Resolve<Graphic>(target);
            if (graphic == null)
            {
                return BlueprintExecResult.Error("UI.SetGraphicRaycastTarget could not resolve Graphic binding '" + target + "'.");
            }

            graphic.raycastTarget = value;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetImageFillAmountExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetImageFillAmount"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            float value = context.GetInputValue(node, "value", 1f);
            Image image = context.BindingResolver.Resolve<Image>(target);
            if (image == null)
            {
                return BlueprintExecResult.Error("UI.SetImageFillAmount could not resolve Image binding '" + target + "'.");
            }

            image.fillAmount = Mathf.Clamp01(value);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetCanvasGroupAlphaExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetCanvasGroupAlpha"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            float value = context.GetInputValue(node, "value", 1f);
            CanvasGroup canvasGroup = context.BindingResolver.Resolve<CanvasGroup>(target);
            if (canvasGroup == null)
            {
                return BlueprintExecResult.Error("UI.SetCanvasGroupAlpha could not resolve CanvasGroup binding '" + target + "'.");
            }

            canvasGroup.alpha = Mathf.Clamp01(value);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetCanvasGroupInteractableExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetCanvasGroupInteractable"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            bool value = context.GetInputValue(node, "value", true);
            CanvasGroup canvasGroup = context.BindingResolver.Resolve<CanvasGroup>(target);
            if (canvasGroup == null)
            {
                return BlueprintExecResult.Error("UI.SetCanvasGroupInteractable could not resolve CanvasGroup binding '" + target + "'.");
            }

            canvasGroup.interactable = value;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetCanvasGroupBlocksRaycastsExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetCanvasGroupBlocksRaycasts"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            bool value = context.GetInputValue(node, "value", true);
            CanvasGroup canvasGroup = context.BindingResolver.Resolve<CanvasGroup>(target);
            if (canvasGroup == null)
            {
                return BlueprintExecResult.Error("UI.SetCanvasGroupBlocksRaycasts could not resolve CanvasGroup binding '" + target + "'.");
            }

            canvasGroup.blocksRaycasts = value;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetRectAnchoredPositionExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetRectAnchoredPosition"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Vector2 value = UIExecutorValueUtility.GetVector2Input(context, node, "value", Vector2.zero);
            RectTransform rectTransform = context.BindingResolver.Resolve<RectTransform>(target);
            if (rectTransform == null)
            {
                return BlueprintExecResult.Error("UI.SetRectAnchoredPosition could not resolve RectTransform binding '" + target + "'.");
            }

            rectTransform.anchoredPosition = value;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetRectSizeDeltaExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetRectSizeDelta"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Vector2 value = UIExecutorValueUtility.GetVector2Input(context, node, "value", Vector2.zero);
            RectTransform rectTransform = context.BindingResolver.Resolve<RectTransform>(target);
            if (rectTransform == null)
            {
                return BlueprintExecResult.Error("UI.SetRectSizeDelta could not resolve RectTransform binding '" + target + "'.");
            }

            rectTransform.sizeDelta = value;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UISetRectLocalScaleExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetRectLocalScale"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Vector3 value = UIExecutorValueUtility.GetVector3Input(context, node, "value", Vector3.one);
            RectTransform rectTransform = context.BindingResolver.Resolve<RectTransform>(target);
            if (rectTransform == null)
            {
                return BlueprintExecResult.Error("UI.SetRectLocalScale could not resolve RectTransform binding '" + target + "'.");
            }

            rectTransform.localScale = value;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UIBindButtonClickExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.BindButtonClick"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Button button = context.BindingResolver.Resolve<Button>(target);
            if (button == null)
            {
                return BlueprintExecResult.Error("UI.BindButtonClick could not resolve Button binding '" + target + "'.");
            }

            string stateKey = "button:" + node.Id + ":" + target;
            if (!context.HasState(stateKey))
            {
                button.onClick.AddListener(delegate { context.ExecuteFromOutput(node, "clicked"); });
                context.SetState(stateKey, true);
            }

            return BlueprintExecResult.Continue("bound");
        }
    }

    internal static class UIExecutorValueUtility
    {
        public static Color GetColorInput(BlueprintExecutionContext context, RuntimeNode node, string portId, Color defaultValue)
        {
            object value = context.GetInputValue(node, portId);
            if (value is Color)
            {
                return (Color)value;
            }

            IList list = value as IList;
            if (list == null || list.Count < 3)
            {
                return defaultValue;
            }

            float r = System.Convert.ToSingle(list[0], CultureInfo.InvariantCulture);
            float g = System.Convert.ToSingle(list[1], CultureInfo.InvariantCulture);
            float b = System.Convert.ToSingle(list[2], CultureInfo.InvariantCulture);
            float a = list.Count >= 4 ? System.Convert.ToSingle(list[3], CultureInfo.InvariantCulture) : defaultValue.a;
            return new Color(r, g, b, a);
        }

        public static Vector2 GetVector2Input(BlueprintExecutionContext context, RuntimeNode node, string portId, Vector2 defaultValue)
        {
            object value = context.GetInputValue(node, portId);
            if (value is Vector2)
            {
                return (Vector2)value;
            }

            return BlueprintTypeUtility.ToVector2(value, defaultValue);
        }

        public static Vector3 GetVector3Input(BlueprintExecutionContext context, RuntimeNode node, string portId, Vector3 defaultValue)
        {
            object value = context.GetInputValue(node, portId);
            if (value is Vector3)
            {
                return (Vector3)value;
            }

            return BlueprintTypeUtility.ToVector3(value, defaultValue);
        }
    }
}
