using System;
using UnityEngine;

namespace BlueprintSystem
{
    public static class BlueprintLog
    {
        private static bool _debugEnabled = true;

        public static bool DebugEnabled
        {
            get { return _debugEnabled; }
            set { _debugEnabled = value; }
        }

        public static void Log(string message, UnityEngine.Object context = null)
        {
            if (!DebugEnabled)
            {
                return;
            }

            if (context != null)
            {
                Debug.Log(message, context);
                return;
            }

            Debug.Log(message);
        }

        public static void Warning(string message, UnityEngine.Object context = null)
        {
            if (!DebugEnabled)
            {
                return;
            }

            if (context != null)
            {
                Debug.LogWarning(message, context);
                return;
            }

            Debug.LogWarning(message);
        }

        public static void Error(string message, UnityEngine.Object context = null)
        {
            if (context != null)
            {
                Debug.LogError(message, context);
                return;
            }

            Debug.LogError(message);
        }

        public static void Exception(Exception exception, UnityEngine.Object context = null)
        {
            if (exception == null)
            {
                return;
            }

            if (context != null)
            {
                Debug.LogException(exception, context);
                return;
            }

            Debug.LogException(exception);
        }
    }
}
