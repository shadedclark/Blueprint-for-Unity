using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BlueprintSystem
{
    public sealed class InputGetAxisExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Input.GetAxis"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return BlueprintAxisUtility.GetAxis(context, node, false);
        }
    }

    public sealed class InputGetAxisRawExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Input.GetAxisRaw"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            return BlueprintAxisUtility.GetAxis(context, node, true);
        }
    }

    public sealed class InputGetActionVector2Executor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Input.GetActionVector2"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            if (outputPortId != "value")
            {
                return base.Evaluate(context, node, outputPortId);
            }

            string actionName = context.GetInputValue(node, "action", string.Empty);
            if (string.IsNullOrEmpty(actionName))
            {
                context.Logger.Error("Input.GetActionVector2 node '" + node.Id + "' has no action.");
                return Vector2.zero;
            }

            string errorMessage;
            InputAction action = BlueprintInputActionUtility.FindProjectAction(actionName, out errorMessage);
            if (action == null)
            {
                context.Logger.Error("Input.GetActionVector2 node '" + node.Id + "' " + errorMessage);
                return Vector2.zero;
            }

            if (!action.enabled)
            {
                action.Enable();
            }

            try
            {
                return action.ReadValue<Vector2>();
            }
            catch (Exception exception)
            {
                context.Logger.Error("Input.GetActionVector2 node '" + node.Id + "' could not read action '" + actionName + "' as Vector2: " + exception.Message);
                return Vector2.zero;
            }
        }
    }

    public sealed class InputListenKeyExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Input.ListenKey"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            object keyValue = context.GetInputValue(node, "key");
            Key key = BlueprintTypeUtility.ConvertValue(keyValue, Key.None);
            if (key == Key.None)
            {
                string keyName = keyValue == null ? string.Empty : keyValue.ToString();
                return BlueprintExecResult.Error("Input.ListenKey node '" + node.Id + "' has unknown key '" + keyName + "'.");
            }

            Keyboard keyboard = Keyboard.current;
            bool isPressed = false;
            if (keyboard != null)
            {
                KeyControl control = keyboard[key];
                isPressed = control != null && control.isPressed;
            }

            string stateKey = "input:key:" + node.Id + ":" + key;
            return BlueprintInputPollingUtility.CreatePollResult(context, stateKey, isPressed);
        }
    }

    public sealed class InputListenActionExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Input.ListenAction"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string actionName = context.GetInputValue(node, "action", string.Empty);
            if (string.IsNullOrEmpty(actionName))
            {
                return BlueprintExecResult.Error("Input.ListenAction node '" + node.Id + "' has no action.");
            }

            string errorMessage;
            InputAction action = BlueprintInputActionUtility.FindProjectAction(actionName, out errorMessage);
            if (action == null)
            {
                return BlueprintExecResult.Error("Input.ListenAction node '" + node.Id + "' " + errorMessage);
            }

            if (!action.enabled)
            {
                action.Enable();
            }

            string stateKey = "input:action:" + node.Id + ":" + action.id;
            bool isPressed = BlueprintInputPollingUtility.IsActionPressed(action);
            return BlueprintInputPollingUtility.CreatePollResult(context, stateKey, isPressed);
        }
    }

    internal static class BlueprintInputActionUtility
    {
        public static InputAction FindProjectAction(string actionName, out string errorMessage)
        {
            errorMessage = null;
            InputActionAsset actions = InputSystem.actions;
            if (actions == null)
            {
                errorMessage = "needs project-wide Input System actions.";
                return null;
            }

            try
            {
                InputAction action = actions.FindAction(actionName, false);
                if (action == null)
                {
                    errorMessage = "could not find action '" + actionName + "'.";
                }

                return action;
            }
            catch (ArgumentException exception)
            {
                errorMessage = "has invalid action '" + actionName + "': " + exception.Message;
                return null;
            }
        }
    }

    internal static class BlueprintAxisUtility
    {
        public static float GetAxis(BlueprintExecutionContext context, RuntimeNode node, bool raw)
        {
            string axisName = context.GetInputValue(node, "axisName", string.Empty);
            if (string.IsNullOrEmpty(axisName))
            {
                context.Logger.Error(GetNodeName(raw) + " node '" + node.Id + "' has no axis name.");
                return 0f;
            }

            try
            {
                return raw ? Input.GetAxisRaw(axisName) : Input.GetAxis(axisName);
            }
            catch (Exception exception)
            {
                context.Logger.Error(GetNodeName(raw) + " node '" + node.Id + "' could not read axis '" + axisName + "': " + exception.Message);
                return 0f;
            }
        }

        private static string GetNodeName(bool raw)
        {
            return raw ? "Input.GetAxisRaw" : "Input.GetAxis";
        }
    }

    internal static class BlueprintInputPollingUtility
    {
        public static BlueprintExecResult CreatePollResult(BlueprintExecutionContext context, string stateKey, bool isPressed)
        {
            object previousValue;
            bool wasPressed = context.TryGetState(stateKey, out previousValue) && previousValue is bool && (bool)previousValue;
            context.SetState(stateKey, isPressed);

            if (isPressed)
            {
                return wasPressed
                    ? BlueprintExecResult.Continue("bound", "held")
                    : BlueprintExecResult.Continue("bound", "pressed");
            }

            return wasPressed
                ? BlueprintExecResult.Continue("bound", "released")
                : BlueprintExecResult.Continue("bound");
        }

        public static bool IsActionPressed(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            if (action.IsPressed())
            {
                return true;
            }

            IReadOnlyList<InputControl> controls = action.controls;
            for (int i = 0; i < controls.Count; i++)
            {
                InputControl control = controls[i];
                ButtonControl button = control as ButtonControl;
                if (button != null && button.isPressed)
                {
                    return true;
                }

                float magnitude = control.EvaluateMagnitude();
                if (!float.IsNaN(magnitude) && magnitude > 0.5f)
                {
                    return true;
                }
            }

            return false;
        }
    }

}
