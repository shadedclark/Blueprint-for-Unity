using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public static class BlueprintUIRuntimeUtility
    {
        private static readonly IList EmptyItems = new List<object>();

        public static IList ResolveItems(BlueprintExecutionContext context, RuntimeNode node)
        {
            object items = context.GetInputValue(node, "items");
            IList list = BlueprintArrayUtility.ReadList(items);
            if (list != null)
            {
                return list;
            }

            string variableName = context.GetInputValue(node, "itemsVariable", string.Empty);
            if (!string.IsNullOrEmpty(variableName))
            {
                object variableValue;
                if (context.Variables.TryGet(variableName, out variableValue))
                {
                    list = BlueprintArrayUtility.ReadList(variableValue);
                    return list ?? EmptyItems;
                }

                context.Logger.Warning("UI.RefreshLoopScrollView could not find variable '" + variableName + "'.");
            }

            return EmptyItems;
        }

        public static void BindRow(BlueprintRunner runner, object item, int index, int count, string bindEventName)
        {
            if (runner == null)
            {
                return;
            }

            runner.TrySetVariable("item", item);
            runner.TrySetVariable("index", index);
            runner.TrySetVariable("count", count);
            runner.TriggerEvent(string.IsNullOrEmpty(bindEventName) ? "OnBindItem" : bindEventName);
        }

        public static GameObject ResolveGameObject(Object target)
        {
            GameObject gameObject = target as GameObject;
            if (gameObject != null)
            {
                return gameObject;
            }

            Component component = target as Component;
            return component == null ? null : component.gameObject;
        }
    }
}
