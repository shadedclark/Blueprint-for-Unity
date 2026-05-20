using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlueprintSystem
{
    public sealed class UIRefreshLoopScrollViewExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.RefreshLoopScrollView"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            BlueprintLoopScrollView scrollView = context.BindingResolver.Resolve<BlueprintLoopScrollView>(target);
            if (scrollView == null)
            {
                return BlueprintExecResult.Error("UI.RefreshLoopScrollView could not resolve BlueprintLoopScrollView binding '" + target + "'.");
            }

            IList items = BlueprintUIRuntimeUtility.ResolveItems(context, node);
            scrollView.Refresh(items, context);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class UIBindButtonEventsExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.BindButtonEvents"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Button button = context.BindingResolver.Resolve<Button>(target);
            if (button == null)
            {
                return BlueprintExecResult.Error("UI.BindButtonEvents could not resolve Button binding '" + target + "'.");
            }

            BlueprintButtonGestureListener listener = button.GetComponent<BlueprintButtonGestureListener>();
            if (listener == null)
            {
                listener = button.gameObject.AddComponent<BlueprintButtonGestureListener>();
            }

            float longPressSeconds = context.GetInputValue(node, "longPressSeconds", 0.5f);
            float doubleClickSeconds = context.GetInputValue(node, "doubleClickSeconds", 0.3f);
            listener.Register("button-events:" + node.Id + ":" + target, context, node, longPressSeconds, doubleClickSeconds);
            return BlueprintExecResult.Continue("bound");
        }
    }

    public sealed class UIBindToggleChangedExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "UI.BindToggleChanged"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Toggle toggle = context.BindingResolver.Resolve<Toggle>(target);
            if (toggle == null)
            {
                return BlueprintExecResult.Error("UI.BindToggleChanged could not resolve Toggle binding '" + target + "'.");
            }

            BlueprintToggleListener listener = toggle.GetComponent<BlueprintToggleListener>();
            if (listener == null)
            {
                listener = toggle.gameObject.AddComponent<BlueprintToggleListener>();
            }

            listener.Register("toggle:" + node.Id + ":" + target, context, node);
            return BlueprintExecResult.Continue("bound");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "value")
            {
                return base.Evaluate(context, node, outputPortId);
            }

            string target = context.GetInputValue(node, "target", string.Empty);
            Toggle toggle = context.BindingResolver.Resolve<Toggle>(target);
            return toggle != null && toggle.isOn;
        }
    }
}
