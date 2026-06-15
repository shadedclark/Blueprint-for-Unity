using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlueprintSystem
{
    public sealed class GameLogExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.Log"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string message = context.GetInputValue(node, "message", string.Empty);
            context.Logger.Log(message);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameSendEventExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SendEvent"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string eventName = context.GetInputValue(node, "eventName", string.Empty);
            if (string.IsNullOrEmpty(eventName))
            {
                return BlueprintExecResult.Error("Game.SendEvent node '" + node.Id + "' has no eventName.");
            }

            context.EventBus.Publish(eventName);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameLoadSceneExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.LoadScene"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string sceneName = context.GetInputValue(node, "sceneName", string.Empty);
            if (string.IsNullOrEmpty(sceneName))
            {
                return BlueprintExecResult.Error("Game.LoadScene node '" + node.Id + "' has no sceneName.");
            }

            LoadSceneMode mode = context.GetInputValue(node, "mode", LoadSceneMode.Single);
            try
            {
                SceneManager.LoadScene(sceneName, mode);
            }
            catch (Exception exception)
            {
                return BlueprintExecResult.Error("Game.LoadScene could not load scene '" + sceneName + "': " + exception.Message);
            }

            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameLoadSceneAsyncExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.LoadSceneAsync"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string sceneName = context.GetInputValue(node, "sceneName", string.Empty);
            if (string.IsNullOrEmpty(sceneName))
            {
                return BlueprintExecResult.Error("Game.LoadSceneAsync node '" + node.Id + "' has no sceneName.");
            }

            LoadSceneMode mode = context.GetInputValue(node, "mode", LoadSceneMode.Single);
            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, mode);
            }
            catch (Exception exception)
            {
                return BlueprintExecResult.Error("Game.LoadSceneAsync could not load scene '" + sceneName + "': " + exception.Message);
            }

            if (operation == null)
            {
                return BlueprintExecResult.Error("Game.LoadSceneAsync could not start scene load '" + sceneName + "'.");
            }

            operation.completed += delegate { context.ExecuteFromOutput(node, "complete"); };
            return BlueprintExecResult.Stop();
        }
    }

    public sealed class GameInstantiateObjectExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.InstantiateObject"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            object prefabValue = context.GetInputValue(node, "prefab");
            GameObject prefab = GameExecutorBindingUtility.ResolveBinding<GameObject>(context, prefabValue);
            if (prefab == null)
            {
                return BlueprintExecResult.Error("Game.InstantiateObject node '" + node.Id + "' could not resolve prefab.");
            }

            object parentValue = context.GetInputValue(node, "parent");
            Transform parent = null;
            if (!IsEmpty(parentValue))
            {
                parent = GameExecutorBindingUtility.ResolveBinding<Transform>(context, parentValue);
                if (parent == null)
                {
                    return BlueprintExecResult.Error("Game.InstantiateObject node '" + node.Id + "' could not resolve parent Transform.");
                }
            }

            GameObject instance = parent == null
                ? UnityEngine.Object.Instantiate(prefab)
                : UnityEngine.Object.Instantiate(prefab, parent, false);

            if (parent == null)
            {
                instance.transform.position = Vector3.zero;
            }
            else
            {
                instance.transform.localPosition = Vector3.zero;
            }

            context.SetState(StateKey(node, "instance"), instance);
            context.SetState(StateKey(node, "transform"), instance.transform);
            return BlueprintExecResult.Continue("execOut");
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            object value;
            if ((outputPortId == "instance" || outputPortId == "transform") &&
                context.TryGetState(StateKey(node, outputPortId), out value))
            {
                return value;
            }

            return null;
        }

        private static bool IsEmpty(object value)
        {
            string text = value as string;
            return value == null || (text != null && string.IsNullOrEmpty(text));
        }

        private static string StateKey(RuntimeNode node, string value)
        {
            return "instantiateObject:" + node.Id + ":" + value;
        }
    }

    public sealed class GameObjectSetActiveExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "GameObject.SetActive"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            GameObject target = context.GetInputValue(node, "target") as GameObject;
            if (target == null)
            {
                return BlueprintExecResult.Error("GameObject.SetActive node '" + node.Id + "' requires a runtime GameObject target.");
            }

            bool active = context.GetInputValue(node, "active", true);
            target.SetActive(active);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameObjectDestroyExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "GameObject.Destroy"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            GameObject target = context.GetInputValue(node, "target") as GameObject;
            if (target == null)
            {
                return BlueprintExecResult.Error("GameObject.Destroy node '" + node.Id + "' requires a runtime GameObject target.");
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }

            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameIsCollidingExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.IsColliding"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            GameObject target = ResolveGameObject(context, context.GetInputValue(node, "target"));
            GameObject other = ResolveGameObject(context, context.GetInputValue(node, "other"));
            if (target == null || other == null)
            {
                return false;
            }

            Collider2D[] target2D = target.GetComponentsInChildren<Collider2D>(true);
            Collider2D[] other2D = other.GetComponentsInChildren<Collider2D>(true);
            if (Any2DOverlap(target2D, other2D))
            {
                return true;
            }

            Collider[] target3D = target.GetComponentsInChildren<Collider>(true);
            Collider[] other3D = other.GetComponentsInChildren<Collider>(true);
            return Any3DOverlap(target3D, other3D);
        }

        private static GameObject ResolveGameObject(BlueprintExecutionContext context, object value)
        {
            GameObject gameObject = value as GameObject;
            if (gameObject != null)
            {
                return gameObject;
            }

            Component component = value as Component;
            if (component != null)
            {
                return component.gameObject;
            }

            string bindingName = value as string;
            if (string.IsNullOrEmpty(bindingName) || context.BindingResolver == null)
            {
                return null;
            }

            gameObject = context.BindingResolver.Resolve<GameObject>(bindingName);
            if (gameObject != null)
            {
                return gameObject;
            }

            UnityEngine.Object resolved = context.BindingResolver.Resolve(bindingName);
            component = resolved as Component;
            return component != null ? component.gameObject : null;
        }

        private static bool Any2DOverlap(Collider2D[] targets, Collider2D[] others)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                Collider2D target = targets[i];
                if (target == null || !target.enabled)
                {
                    continue;
                }

                for (int j = 0; j < others.Length; j++)
                {
                    Collider2D other = others[j];
                    if (other == null || !other.enabled)
                    {
                        continue;
                    }

                    if (target.Distance(other).isOverlapped)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Any3DOverlap(Collider[] targets, Collider[] others)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                Collider target = targets[i];
                if (target == null || !target.enabled)
                {
                    continue;
                }

                for (int j = 0; j < others.Length; j++)
                {
                    Collider other = others[j];
                    if (other == null || !other.enabled)
                    {
                        continue;
                    }

                    Vector3 direction;
                    float distance;
                    if (Physics.ComputePenetration(
                        target,
                        target.transform.position,
                        target.transform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out direction,
                        out distance))
                    {
                        return true;
                    }

                    if (target.bounds.Intersects(other.bounds))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
