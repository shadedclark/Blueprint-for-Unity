using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BlueprintSystem.Tests
{
    public sealed class BlueprintRuntimeDiagnosticsPlayModeTests
    {
        [UnityTest]
        public IEnumerator RunnerExposesNestedComponentPublicDiagnosticsInPlayMode()
        {
            BlueprintCompiledAsset childAsset = CreateCompiledAsset(
                "Child",
                "Assets/Tests/Child.blueprint.json",
                new[]
                {
                    new BlueprintCompiledVariable
                    {
                        Id = "var_public_count",
                        Name = "publicCount",
                        Type = "int",
                        DefaultValueJson = "3",
                        Scope = "runtime",
                        Exposed = true
                    },
                    new BlueprintCompiledVariable
                    {
                        Id = "var_private_note",
                        Name = "privateNote",
                        Type = "string",
                        DefaultValueJson = "\"hidden\"",
                        Scope = "runtime",
                        Exposed = false
                    }
                },
                Array.Empty<BlueprintCompiledComponent>());
            BlueprintCompiledAsset rootAsset = CreateCompiledAsset(
                "Root",
                "Assets/Tests/Root.blueprint.json",
                Array.Empty<BlueprintCompiledVariable>(),
                new[]
                {
                    new BlueprintCompiledComponent
                    {
                        Name = "ChildComponent",
                        BlueprintPath = "Assets/Tests/Child.blueprint.json",
                        Required = true,
                        CompiledBlueprint = childAsset
                    }
                });

            GameObject gameObject = new GameObject("DiagnosticsRunner");
            BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
            typeof(BlueprintRunner)
                .GetField("compiledBlueprint", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(runner, rootAsset);
            Assert.True(runner.Compile());

            yield return null;

            IBlueprintInstance child;
            Assert.True(runner.TryGetBlueprintComponent("ChildComponent", out child));
            IBlueprintDebugInspectable inspectable = child as IBlueprintDebugInspectable;
            Assert.NotNull(inspectable);
            Assert.AreEqual(2, inspectable.GetVariableDescriptors().Count);
            Assert.True(inspectable.GetVariableDescriptors()[0].Exposed || inspectable.GetVariableDescriptors()[1].Exposed);
            object value;
            Assert.True(child.TryGetVariable("publicCount", out value));
            Assert.AreEqual(3L, Convert.ToInt64(value));

            UnityEngine.Object.Destroy(rootAsset);
            UnityEngine.Object.Destroy(childAsset);
            UnityEngine.Object.Destroy(gameObject);
        }

        private static BlueprintCompiledAsset CreateCompiledAsset(
            string name,
            string sourcePath,
            BlueprintCompiledVariable[] variables,
            BlueprintCompiledComponent[] components)
        {
            BlueprintCompiledAsset asset = ScriptableObject.CreateInstance<BlueprintCompiledAsset>();
            asset.SetCompiledData(
                "0.1",
                name,
                string.Empty,
                sourcePath,
                "sourceHash",
                "manifestHash",
                variables,
                Array.Empty<BlueprintCompiledBinding>(),
                components,
                Array.Empty<BlueprintCompiledNode>(),
                Array.Empty<BlueprintCompiledEdge>(),
                Array.Empty<BlueprintCompiledEdge>(),
                Array.Empty<BlueprintCompiledEventEntry>());
            return asset;
        }
    }
}
