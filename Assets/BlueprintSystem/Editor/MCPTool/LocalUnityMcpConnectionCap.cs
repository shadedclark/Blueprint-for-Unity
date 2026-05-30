using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class LocalUnityMcpConnectionCap
{
    const int Cap = 4;
    const int MaxAttempts = 20;

    static int s_Attempts;

    static LocalUnityMcpConnectionCap()
    {
        EditorApplication.delayCall += Apply;
    }

    static void Apply()
    {
        s_Attempts++;

        const string assemblyName = "Unity.AI.MCP.Editor";
        var policyType = Type.GetType("Unity.AI.MCP.Editor.Connection.ConnectionPolicy, " + assemblyName);
        var overrideType = Type.GetType("Unity.AI.MCP.Editor.Connection.ConnectionPolicyOverride, " + assemblyName);

        var constructor = policyType?.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(int), typeof(int) },
            null);
        var setMethod = overrideType?.GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic);

        if (constructor == null || setMethod == null)
        {
            if (s_Attempts < MaxAttempts)
            {
                EditorApplication.delayCall += Apply;
                return;
            }

            Debug.LogWarning("Unity MCP connection cap override API was not found.");
            return;
        }

        var policy = constructor.Invoke(new object[] { Cap, Cap });
        setMethod.Invoke(null, new[] { policy });
        Debug.Log("Unity MCP connection cap set to " + Cap + ".");
    }
}
