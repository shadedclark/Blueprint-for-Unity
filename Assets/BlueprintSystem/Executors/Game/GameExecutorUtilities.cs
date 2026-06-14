using System.Collections;
using System.Globalization;
using UnityEngine;

namespace BlueprintSystem
{
    internal static class GameExecutorBindingUtility
    {
        public static T ResolveBinding<T>(BlueprintExecutionContext context, string bindingName) where T : Object
        {
            return ResolveBinding<T>(context, (object)bindingName);
        }

        public static T ResolveBinding<T>(BlueprintExecutionContext context, object value) where T : Object
        {
            T direct = ResolveDirect<T>(value);
            if (direct != null)
            {
                return direct;
            }

            string bindingName = value as string;
            if (context == null || context.BindingResolver == null || string.IsNullOrEmpty(bindingName))
            {
                return null;
            }

            direct = context.BindingResolver.Resolve<T>(bindingName);
            if (direct != null)
            {
                return direct;
            }

            return ResolveDirect<T>(context.BindingResolver.Resolve(bindingName));
        }

        private static T ResolveDirect<T>(object value) where T : Object
        {
            if (value == null)
            {
                return null;
            }

            T direct = value as T;
            if (direct != null)
            {
                return direct;
            }

            GameObject gameObject = value as GameObject;
            if (gameObject != null)
            {
                if (typeof(T) == typeof(GameObject))
                {
                    return gameObject as T;
                }

                if (typeof(Component).IsAssignableFrom(typeof(T)))
                {
                    return gameObject.GetComponent(typeof(T)) as T;
                }
            }

            Component component = value as Component;
            if (component == null)
            {
                return null;
            }

            if (typeof(T) == typeof(GameObject))
            {
                return component.gameObject as T;
            }

            if (typeof(Component).IsAssignableFrom(typeof(T)))
            {
                return component.GetComponent(typeof(T)) as T;
            }

            return null;
        }
    }

    internal static class GameExecutorValueUtility
    {
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
    }
}
