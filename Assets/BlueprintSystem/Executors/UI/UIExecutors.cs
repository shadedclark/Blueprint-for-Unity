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

    public sealed class UISetVisibleExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.SetVisible"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            bool value = context.GetInputValue(node, "value", true);
            Object resolved = context.BindingResolver.Resolve(target);
            GameObject gameObject = ResolveGameObject(resolved);
            if (gameObject == null)
            {
                return BlueprintExecResult.Error("UI.SetVisible could not resolve binding '" + target + "'.");
            }

            gameObject.SetActive(value);
            return BlueprintExecResult.Continue("execOut");
        }

        private static GameObject ResolveGameObject(Object resolved)
        {
            GameObject gameObject = resolved as GameObject;
            if (gameObject != null)
            {
                return gameObject;
            }

            Component component = resolved as Component;
            return component == null ? null : component.gameObject;
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
