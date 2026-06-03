using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BlueprintSystem.Editor;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BlueprintSystem.Tests
{
    public sealed class BlueprintSystemTests
    {
        [Test]
        public void ValidatorAcceptsInventorySample()
        {
            BlueprintSource source = LoadBlueprint("Assets/BlueprintSystem/Sources/UI/InventoryPanel.blueprint.json");
            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
        }

        [Test]
        public void ValidatorReportsUnknownBinding()
        {
            BlueprintSource source = LoadBlueprint("Assets/BlueprintSystem/Sources/UI/InventoryPanel.blueprint.json");
            source.Nodes.Find(node => node.Id == "set_title").Properties["target"] = "MissingTitle";

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP005"), diagnostics.ToDisplayString());
        }

        [Test]
        public void ValidatorAcceptsEmptyOptionalBinding()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "OptionalBindingTest";
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "Actor",
                Type = "Transform",
                Required = true
            });

            BlueprintNodeSource lookAt = AddNode(source, "look_at_position", "Game.LookAtTransform");
            lookAt.Properties["target"] = "Actor";
            lookAt.Properties["lookTarget"] = string.Empty;
            lookAt.Properties["targetPosition"] = new List<object> { 0f, 0f, 1f };
            lookAt.Properties["worldUp"] = new List<object> { 0f, 1f, 0f };

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
        }

        [Test]
        public void ValidatorReportsEmptyRequiredBinding()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "RequiredBindingTest";

            BlueprintNodeSource setPosition = AddNode(source, "set_position", "Game.SetTransformPosition");
            setPosition.Properties["target"] = string.Empty;
            setPosition.Properties["value"] = new List<object> { 0f, 0f, 0f };

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP005"), diagnostics.ToDisplayString());
        }

        [Test]
        public void SourceMapperPreservesVariableMetadata()
        {
            BlueprintSource source = LoadBlueprint("Assets/BlueprintSystem/Sources/UI/InventoryPanel.blueprint.json");
            BlueprintVariableDeclaration variable = source.Variables.Find(item => item.Name == "selectedItemId");

            Assert.NotNull(variable);
            Assert.AreEqual("runtime", variable.Scope);
            Assert.True(variable.Exposed);
            Assert.False(variable.Persistent);
            Assert.AreEqual("Currently selected inventory item id.", variable.Description);

            BlueprintSource roundTripped = BlueprintSource.FromJson(source.ToJson());
            BlueprintVariableDeclaration copied = roundTripped.Variables.Find(item => item.Name == "selectedItemId");
            Assert.NotNull(copied);
            Assert.False(string.IsNullOrEmpty(copied.Id));
            Assert.AreEqual(variable.Id, copied.Id);
            Assert.True(copied.Exposed);
            Assert.AreEqual(variable.Description, copied.Description);
        }

        [Test]
        public void BlueprintEditorWindowRecognizesBlueprintJsonPaths()
        {
            Assert.True(BlueprintEditorWindow.IsBlueprintJsonPath("Assets/Test/Foo.blueprint.json"));
            Assert.True(BlueprintEditorWindow.IsBlueprintJsonPath("Assets/Test/Foo.BLUEPRINT.JSON"));
            Assert.False(BlueprintEditorWindow.IsBlueprintJsonPath("Assets/Test/Foo.json"));
            Assert.False(BlueprintEditorWindow.IsBlueprintJsonPath(null));
        }

        [Test]
        public void BlueprintEditorWindowOpensBlueprintJsonAsset()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/EditorOpenTest.blueprint.json";
            AssetDatabase.DeleteAsset(blueprintPath);

            try
            {
                BlueprintSource source = CreateVariableTestSource();
                File.WriteAllText(blueprintPath, source.ToJson());
                AssetDatabase.ImportAsset(blueprintPath);

                Assert.True(BlueprintEditorWindow.OpenAssetAtPath(blueprintPath));
                BlueprintEditorWindow window = EditorWindow.GetWindow<BlueprintEditorWindow>("Blueprint JSON");
                BlueprintSource opened = BlueprintSource.FromJson(window.CurrentJsonText);

                Assert.AreEqual(blueprintPath, window.CurrentAssetPath);
                Assert.AreEqual("VariableTest", opened.Name);
            }
            finally
            {
                EditorWindow window = EditorWindow.GetWindow<BlueprintEditorWindow>("Blueprint JSON");
                if (window != null)
                {
                    window.Close();
                }

                AssetDatabase.DeleteAsset(blueprintPath);
            }
        }

        [Test]
        public void BlueprintEditorWindowOpenAssetCallbackHandlesBlueprintJsonAndCompiledAsset()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/EditorOpenCallbackTest.blueprint.json";
            string plainJsonPath = "Assets/BlueprintSystem/Tests/Editor/EditorOpenCallbackTest.json";
            DeleteTemporaryCompiledArtifacts(blueprintPath);
            AssetDatabase.DeleteAsset(plainJsonPath);

            try
            {
                TextAsset blueprintAsset = WriteTemporaryBlueprintAsset(blueprintPath, CreateVariableTestSource());
                BlueprintCompiledAsset compiledAsset;
                Assert.True(BlueprintCompiledAssetCompiler.CompileBlueprint(blueprintAsset, false, out compiledAsset));

                File.WriteAllText(plainJsonPath, "{}");
                AssetDatabase.ImportAsset(plainJsonPath);

                TextAsset plainJsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(plainJsonPath);

                Assert.NotNull(blueprintAsset);
                Assert.NotNull(compiledAsset);
                Assert.NotNull(plainJsonAsset);
                Assert.AreEqual(blueprintPath, BlueprintEditorWindow.GetAssetPathFromOpenAssetId(blueprintAsset.GetInstanceID()));
                Assert.True(BlueprintEditorWindow.OnOpenAsset(blueprintAsset.GetInstanceID(), 0));
                Assert.True(BlueprintEditorWindow.OnOpenAsset(compiledAsset.GetInstanceID(), 0));
                Assert.False(BlueprintEditorWindow.OnOpenAsset(plainJsonAsset.GetInstanceID(), 0));

                BlueprintEditorWindow window = EditorWindow.GetWindow<BlueprintEditorWindow>("Blueprint JSON");
                Assert.AreEqual(blueprintPath, window.CurrentAssetPath);
            }
            finally
            {
                EditorWindow window = EditorWindow.GetWindow<BlueprintEditorWindow>("Blueprint JSON");
                if (window != null)
                {
                    window.Close();
                }

                DeleteTemporaryCompiledArtifacts(blueprintPath);
                AssetDatabase.DeleteAsset(plainJsonPath);
            }
        }

        [Test]
        public void ValidatorReportsVariableReferenceProblems()
        {
            BlueprintSource source = CreateVariableTestSource();
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "count",
                Type = "int",
                DefaultValue = 1
            });
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "hasItem",
                Type = "bool",
                DefaultValue = "not a bool"
            });

            BlueprintNodeSource missingName = AddNode(source, "get_missing_name", "Variable.Get");
            BlueprintNodeSource unknown = AddNode(source, "get_unknown", "Variable.Get");
            unknown.Properties["name"] = "missing";
            BlueprintNodeSource wrongSet = AddNode(source, "set_count", "Variable.Set");
            wrongSet.Properties["name"] = "count";
            wrongSet.Properties["value"] = "not an int";

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP020"), diagnostics.ToDisplayString());
            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP021"), diagnostics.ToDisplayString());
            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP022"), diagnostics.ToDisplayString());
            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP023"), diagnostics.ToDisplayString());
            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP024"), diagnostics.ToDisplayString());
        }

        [Test]
        public void ValidatorUsesVariableTypesForPorts()
        {
            BlueprintSource source = CreateVariableTestSource();
            BlueprintNodeSource getTitle = AddNode(source, "get_title", "Variable.Get");
            getTitle.Properties["name"] = "title";
            BlueprintNodeSource branch = AddNode(source, "branch_title", "Flow.Branch");
            source.Edges.Add(new BlueprintEdgeSource
            {
                From = "get_title.value",
                To = "branch_title.condition"
            });

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP003"), diagnostics.ToDisplayString());
        }

        [Test]
        public void RuntimeComparesAllComparisonModes()
        {
            Assert.True(EvaluateComparison("Equals", 3, 3));
            Assert.False(EvaluateComparison("Equals", 3, 4));
            Assert.True(EvaluateComparison("NotEquals", "sword", "shield"));
            Assert.True(EvaluateComparison("Greater", 5, 4));
            Assert.True(EvaluateComparison("GreaterOrEqual", 5, 5));
            Assert.True(EvaluateComparison("Less", 3, 4));
            Assert.True(EvaluateComparison("LessOrEqual", 4, 4));
        }

        [Test]
        public void CompilerBuildsRuntimeIndexes()
        {
            RuntimeBlueprint blueprint = CompileInventoryBlueprint();

            Assert.AreEqual("InventoryPanel", blueprint.Name);
            Assert.AreEqual("event_open", blueprint.EventEntries["OnOpen"]);
            Assert.True(blueprint.NodesById.ContainsKey("set_title"));
            Assert.True(blueprint.ExecOutputs.ContainsKey(new BlueprintPortKey("event_open", "execOut")));
        }

        [Test]
        public void CompiledAssetHydratesRuntimeIndexes()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/CompiledInventoryTest.blueprint.json";
            DeleteTemporaryCompiledArtifacts(blueprintPath);

            try
            {
                BlueprintSource source = LoadBlueprint("Assets/BlueprintSystem/Sources/UI/InventoryPanel.blueprint.json");
                TextAsset sourceAsset = WriteTemporaryBlueprintAsset(blueprintPath, source);

                BlueprintCompiledAsset compiledAsset;
                Assert.True(BlueprintCompiledAssetCompiler.CompileBlueprint(sourceAsset, false, out compiledAsset));

                RuntimeBlueprint expected = CompileSource(source);
                RuntimeBlueprint hydrated = compiledAsset.CreateRuntimeBlueprint(BlueprintExecutorRegistry.CreateDefault());

                Assert.AreEqual(expected.Name, hydrated.Name);
                Assert.AreEqual(expected.NodesById.Count, hydrated.NodesById.Count);
                Assert.AreEqual(expected.ExecOutputs.Count, hydrated.ExecOutputs.Count);
                Assert.AreEqual(expected.ValueInputs.Count, hydrated.ValueInputs.Count);
                Assert.AreEqual(expected.EventEntries["OnOpen"], hydrated.EventEntries["OnOpen"]);
                Assert.AreEqual(expected.Variables.Count, hydrated.Variables.Count);
                Assert.AreEqual(expected.Bindings.Count, hydrated.Bindings.Count);
                Assert.True(hydrated.NodesById.ContainsKey("set_title"));
                Assert.NotNull(hydrated.NodesById["set_title"].Executor);
            }
            finally
            {
                DeleteTemporaryCompiledArtifacts(blueprintPath);
            }
        }

        [Test]
        public void CompiledAssetBakesManifestDefaults()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/CompiledDefaultsTest.blueprint.json";
            DeleteTemporaryCompiledArtifacts(blueprintPath);

            try
            {
                BlueprintSource source = new BlueprintSource();
                source.SchemaVersion = "0.1";
                source.Name = "CompiledDefaultsTest";
                AddNode(source, "not_value", "Logic.Not");
                TextAsset sourceAsset = WriteTemporaryBlueprintAsset(blueprintPath, source);

                BlueprintCompiledAsset compiledAsset;
                Assert.True(BlueprintCompiledAssetCompiler.CompileBlueprint(sourceAsset, false, out compiledAsset));

                RuntimeBlueprint hydrated = compiledAsset.CreateRuntimeBlueprint(BlueprintExecutorRegistry.CreateDefault());
                RuntimeNode node = hydrated.GetNode("not_value");

                Assert.NotNull(node);
                Assert.True(node.Properties.ContainsKey("value"));
                Assert.AreEqual(false, node.GetProperty("value"));
                Assert.Null(node.Manifest);
            }
            finally
            {
                DeleteTemporaryCompiledArtifacts(blueprintPath);
            }
        }

        [Test]
        public void RunnerUsesCompiledAssetWithoutRuntimeJsonCompile()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/CompiledRunnerTest.blueprint.json";
            DeleteTemporaryCompiledArtifacts(blueprintPath);
            GameObject gameObject = null;

            try
            {
                BlueprintSource source = CreateVariableTestSource();
                source.Variables.Add(new BlueprintVariableDeclaration
                {
                    Name = "fired",
                    Type = "bool",
                    DefaultValue = false
                });
                AddNode(source, "event_start", "Game.Event.OnStart");
                BlueprintNodeSource setFired = AddNode(source, "set_fired", "Variable.Set");
                setFired.Properties["name"] = "fired";
                setFired.Properties["value"] = true;
                source.Edges.Add(new BlueprintEdgeSource
                {
                    From = "event_start.execOut",
                    To = "set_fired.execIn"
                });
                TextAsset sourceAsset = WriteTemporaryBlueprintAsset(blueprintPath, source);

                BlueprintCompiledAsset compiledAsset;
                Assert.True(BlueprintCompiledAssetCompiler.CompileBlueprint(sourceAsset, false, out compiledAsset));

                gameObject = new GameObject("CompiledRunner");
                gameObject.SetActive(false);
                BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", compiledAsset);

                Assert.True(runner.Compile());
                runner.TriggerEvent("OnStart");

                object fired;
                Assert.True(runner.TryGetVariable("fired", out fired));
                Assert.AreEqual(true, fired);
            }
            finally
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }

                DeleteTemporaryCompiledArtifacts(blueprintPath);
            }
        }

        [Test]
        public void RunnerRequiresCompiledAssetAtRuntime()
        {
            GameObject gameObject = null;
            try
            {
                gameObject = new GameObject("CompiledRunnerRequired");
                gameObject.SetActive(false);
                BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
                LogAssert.Expect(LogType.Warning, "[Blueprint] Missing compiled blueprint asset on CompiledRunnerRequired.");

                Assert.False(runner.Compile());
            }
            finally
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }

        [Test]
        public void RunnerReloadPreservesVariablesById()
        {
            GameObject gameObject = null;
            BlueprintCompiledAsset compiledAsset = null;

            try
            {
                compiledAsset = CreateVariableOnlyCompiledAsset(
                    "ReloadVariableTest",
                    "Assets/BlueprintSystem/Tests/Editor/ReloadVariableTest.blueprint.json",
                    new BlueprintCompiledVariable
                    {
                        Id = "score-id",
                        Name = "score",
                        Type = "int",
                        DefaultValueJson = BlueprintJson.Serialize(1, false)
                    });

                gameObject = new GameObject("ReloadVariableRunner");
                gameObject.SetActive(false);
                BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", compiledAsset);

                Assert.True(runner.Compile());
                Assert.True(runner.TrySetVariable("score", 42));

                SetVariableOnlyCompiledData(
                    compiledAsset,
                    "ReloadVariableTest",
                    "Assets/BlueprintSystem/Tests/Editor/ReloadVariableTest.blueprint.json",
                    new BlueprintCompiledVariable
                    {
                        Id = "score-id",
                        Name = "renamedScore",
                        Type = "int",
                        DefaultValueJson = BlueprintJson.Serialize(0, false)
                    });

                Assert.True(runner.ReloadBlueprint(new BlueprintReloadOptions { PreserveVariables = true, Log = false }));

                object value;
                Assert.True(runner.TryGetVariable("renamedScore", out value));
                Assert.AreEqual(42, value);
            }
            finally
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }

                if (compiledAsset != null)
                {
                    Object.DestroyImmediate(compiledAsset);
                }
            }
        }

        [Test]
        public void RunnerReloadUsesNewDefaultForUnchangedVariable()
        {
            GameObject gameObject = null;
            BlueprintCompiledAsset compiledAsset = null;

            try
            {
                compiledAsset = CreateVariableOnlyCompiledAsset(
                    "ReloadDefaultTest",
                    "Assets/BlueprintSystem/Tests/Editor/ReloadDefaultTest.blueprint.json",
                    new BlueprintCompiledVariable
                    {
                        Id = "speed-id",
                        Name = "move_speed",
                        Type = "float",
                        DefaultValueJson = BlueprintJson.Serialize(10f, false)
                    });

                gameObject = new GameObject("ReloadDefaultRunner");
                gameObject.SetActive(false);
                BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", compiledAsset);

                Assert.True(runner.Compile());

                SetVariableOnlyCompiledData(
                    compiledAsset,
                    "ReloadDefaultTest",
                    "Assets/BlueprintSystem/Tests/Editor/ReloadDefaultTest.blueprint.json",
                    new BlueprintCompiledVariable
                    {
                        Id = "speed-id",
                        Name = "move_speed",
                        Type = "float",
                        DefaultValueJson = BlueprintJson.Serialize(1f, false)
                    });

                Assert.True(runner.ReloadBlueprint(new BlueprintReloadOptions { PreserveVariables = true, Log = false }));

                object value;
                Assert.True(runner.TryGetVariable("move_speed", out value));
                Assert.AreEqual(1f, value);
            }
            finally
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }

                if (compiledAsset != null)
                {
                    Object.DestroyImmediate(compiledAsset);
                }
            }
        }

        [Test]
        public void RunnerReloadFailureKeepsPreviousRuntime()
        {
            GameObject gameObject = null;
            BlueprintCompiledAsset compiledAsset = null;

            try
            {
                compiledAsset = CreateSetBoolCompiledAsset(
                    "ReloadFailureTest",
                    "Assets/BlueprintSystem/Tests/Editor/ReloadFailureTest.blueprint.json",
                    "fired",
                    "OnStart");

                gameObject = new GameObject("ReloadFailureRunner");
                gameObject.SetActive(false);
                BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", compiledAsset);

                Assert.True(runner.Compile());
                SetInvalidCompiledData(compiledAsset, "ReloadFailureTest", "Assets/BlueprintSystem/Tests/Editor/ReloadFailureTest.blueprint.json");

                Assert.False(runner.ReloadBlueprint(new BlueprintReloadOptions { PreserveVariables = true, Log = false }));
                runner.TriggerEvent("OnStart");

                object fired;
                Assert.True(runner.TryGetVariable("fired", out fired));
                Assert.AreEqual(true, fired);
            }
            finally
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }

                if (compiledAsset != null)
                {
                    Object.DestroyImmediate(compiledAsset);
                }
            }
        }

        [Test]
        public void RunnerReloadPreservesComponentVariables()
        {
            GameObject gameObject = null;
            BlueprintCompiledAsset componentAsset = null;
            BlueprintCompiledAsset ownerAsset = null;
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/ReloadComponent.blueprint.json";

            try
            {
                componentAsset = CreateVariableOnlyCompiledAsset(
                    "ReloadComponent",
                    componentPath,
                    new BlueprintCompiledVariable
                    {
                        Id = "count-id",
                        Name = "count",
                        Type = "int",
                        DefaultValueJson = BlueprintJson.Serialize(1, false)
                    });
                ownerAsset = CreateOwnerCompiledAsset(
                    "Assets/BlueprintSystem/Tests/Editor/ReloadComponentOwner.blueprint.json",
                    componentAsset,
                    componentPath,
                    "Child");

                gameObject = new GameObject("ReloadComponentRunner");
                gameObject.SetActive(false);
                BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", ownerAsset);

                Assert.True(runner.Compile());
                IBlueprintInstance component;
                Assert.True(runner.TryGetBlueprintComponent("Child", out component));
                Assert.True(component.TrySetVariable("count", 12));

                SetVariableOnlyCompiledData(
                    componentAsset,
                    "ReloadComponent",
                    componentPath,
                    new BlueprintCompiledVariable
                    {
                        Id = "count-id",
                        Name = "renamedCount",
                        Type = "int",
                        DefaultValueJson = BlueprintJson.Serialize(0, false)
                    });

                Assert.True(runner.ReloadBlueprint(new BlueprintReloadOptions { PreserveVariables = true, Log = false }));
                Assert.True(runner.TryGetBlueprintComponent("Child", out component));

                object value;
                Assert.True(component.TryGetVariable("renamedCount", out value));
                Assert.AreEqual(12, value);
            }
            finally
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }

                if (componentAsset != null)
                {
                    Object.DestroyImmediate(componentAsset);
                }

                if (ownerAsset != null)
                {
                    Object.DestroyImmediate(ownerAsset);
                }
            }
        }

        [Test]
        public void RunnerReloadUsesNewComponentDefaultForUnchangedVariable()
        {
            GameObject gameObject = null;
            BlueprintCompiledAsset componentAsset = null;
            BlueprintCompiledAsset ownerAsset = null;
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/ReloadComponentDefault.blueprint.json";

            try
            {
                componentAsset = CreateVariableOnlyCompiledAsset(
                    "ReloadComponentDefault",
                    componentPath,
                    new BlueprintCompiledVariable
                    {
                        Id = "speed-id",
                        Name = "move_speed",
                        Type = "float",
                        DefaultValueJson = BlueprintJson.Serialize(10f, false)
                    });
                ownerAsset = CreateOwnerCompiledAsset(
                    "Assets/BlueprintSystem/Tests/Editor/ReloadComponentDefaultOwner.blueprint.json",
                    componentAsset,
                    componentPath,
                    "Data");

                gameObject = new GameObject("ReloadComponentDefaultRunner");
                gameObject.SetActive(false);
                BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", ownerAsset);

                Assert.True(runner.Compile());

                SetVariableOnlyCompiledData(
                    componentAsset,
                    "ReloadComponentDefault",
                    componentPath,
                    new BlueprintCompiledVariable
                    {
                        Id = "speed-id",
                        Name = "move_speed",
                        Type = "float",
                        DefaultValueJson = BlueprintJson.Serialize(1f, false)
                    });

                Assert.True(runner.ReloadBlueprint(new BlueprintReloadOptions { PreserveVariables = true, Log = false }));
                IBlueprintInstance component;
                Assert.True(runner.TryGetBlueprintComponent("Data", out component));

                object value;
                Assert.True(component.TryGetVariable("move_speed", out value));
                Assert.AreEqual(1f, value);
            }
            finally
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }

                if (componentAsset != null)
                {
                    Object.DestroyImmediate(componentAsset);
                }

                if (ownerAsset != null)
                {
                    Object.DestroyImmediate(ownerAsset);
                }
            }
        }

        [UnityTest]
        public IEnumerator RunnerReloadInvalidatesDelayedResume()
        {
            GameObject gameObject = null;
            BlueprintCompiledAsset compiledAsset = null;

            try
            {
                compiledAsset = CreateDelayedSetBoolCompiledAsset(
                    "ReloadDelayTest",
                    "Assets/BlueprintSystem/Tests/Editor/ReloadDelayTest.blueprint.json",
                    "fired",
                    0.05f);

                gameObject = new GameObject("ReloadDelayRunner");
                gameObject.SetActive(false);
                BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", compiledAsset);
                SetPrivateField(runner, "triggerOnStart", false);
                gameObject.SetActive(true);
                yield return null;

                runner.TriggerEvent("OnStart");
                Assert.True(runner.ReloadBlueprint(new BlueprintReloadOptions { PreserveVariables = true, Log = false }));

                yield return new WaitForSeconds(0.12f);

                object fired;
                Assert.True(runner.TryGetVariable("fired", out fired));
                Assert.AreEqual(false, fired);
            }
            finally
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }

                if (compiledAsset != null)
                {
                    Object.DestroyImmediate(compiledAsset);
                }
            }
        }

        [Test]
        public void HotReloadServiceDetectsComponentSourceReferences()
        {
            GameObject gameObject = null;
            BlueprintCompiledAsset componentAsset = null;
            BlueprintCompiledAsset ownerAsset = null;
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/HotReloadComponent.blueprint.json";

            try
            {
                componentAsset = CreateVariableOnlyCompiledAsset(
                    "HotReloadComponent",
                    componentPath,
                    new BlueprintCompiledVariable
                    {
                        Id = "value-id",
                        Name = "value",
                        Type = "int",
                        DefaultValueJson = BlueprintJson.Serialize(1, false)
                    });
                ownerAsset = CreateOwnerCompiledAsset(
                    "Assets/BlueprintSystem/Tests/Editor/HotReloadOwner.blueprint.json",
                    componentAsset,
                    componentPath,
                    "Child");

                gameObject = new GameObject("HotReloadReferenceRunner");
                gameObject.SetActive(false);
                BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", ownerAsset);

                HashSet<string> changedSources = new HashSet<string> { componentPath };
                Assert.True(BlueprintHotReloadService.RunnerReferencesAnySourcePath(runner, changedSources));
            }
            finally
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }

                if (componentAsset != null)
                {
                    Object.DestroyImmediate(componentAsset);
                }

                if (ownerAsset != null)
                {
                    Object.DestroyImmediate(ownerAsset);
                }
            }
        }

        [Test]
        public void RunnerSerializesCompiledAssetReferenceOnly()
        {
            GameObject gameObject = null;
            try
            {
                gameObject = new GameObject("CompiledRunnerSerializedFields");
                BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
                SerializedObject serializedObject = new SerializedObject(runner);

                Assert.NotNull(serializedObject.FindProperty("compiledBlueprint"));
                Assert.Null(serializedObject.FindProperty("blueprintJson"));
            }
            finally
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }

        [Test]
        public void CompiledAssetDetectsStaleSourceHash()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/CompiledStaleTest.blueprint.json";
            DeleteTemporaryCompiledArtifacts(blueprintPath);

            try
            {
                BlueprintSource source = CreateVariableTestSource();
                TextAsset sourceAsset = WriteTemporaryBlueprintAsset(blueprintPath, source);

                BlueprintCompiledAsset compiledAsset;
                Assert.True(BlueprintCompiledAssetCompiler.CompileBlueprint(sourceAsset, false, out compiledAsset));

                string reason;
                Assert.True(BlueprintCompiledAssetCompiler.IsCompiledAssetCurrent(compiledAsset, sourceAsset, out reason), reason);

                source.Variables.Add(new BlueprintVariableDeclaration
                {
                    Name = "changed",
                    Type = "string",
                    DefaultValue = "changed"
                });
                File.WriteAllText(blueprintPath, source.ToJson());
                AssetDatabase.ImportAsset(blueprintPath);
                TextAsset changedAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(blueprintPath);

                Assert.False(BlueprintCompiledAssetCompiler.IsCompiledAssetCurrent(compiledAsset, changedAsset, out reason));
                Assert.AreEqual("Compiled asset hash is stale.", reason);
            }
            finally
            {
                DeleteTemporaryCompiledArtifacts(blueprintPath);
            }
        }

        [Test]
        public void CompiledAssetDetectsStaleManifestHash()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/CompiledManifestStaleTest.blueprint.json";
            string manifestPath = "Assets/BlueprintSystem/Specs/Nodes/Test.CompiledManifestStale.node.json";
            DeleteTemporaryCompiledArtifacts(blueprintPath);
            AssetDatabase.DeleteAsset(manifestPath);

            try
            {
                WriteManifest(manifestPath, "Original temp manifest.");

                BlueprintSource source = new BlueprintSource();
                source.SchemaVersion = "0.1";
                source.Name = "CompiledManifestStaleTest";
                AddNode(source, "temp_node", "Test.CompiledManifestStale");
                TextAsset sourceAsset = WriteTemporaryBlueprintAsset(blueprintPath, source);

                BlueprintCompiledAsset compiledAsset;
                Assert.True(BlueprintCompiledAssetCompiler.CompileBlueprint(sourceAsset, false, out compiledAsset));

                string reason;
                Assert.True(BlueprintCompiledAssetCompiler.IsCompiledAssetCurrent(compiledAsset, sourceAsset, out reason), reason);

                WriteManifest(manifestPath, "Changed temp manifest.");

                Assert.False(BlueprintCompiledAssetCompiler.IsCompiledAssetCurrent(compiledAsset, sourceAsset, out reason));
                Assert.AreEqual("Compiled asset hash is stale.", reason);
            }
            finally
            {
                DeleteTemporaryCompiledArtifacts(blueprintPath);
                AssetDatabase.DeleteAsset(manifestPath);
            }
        }

        [Test]
        public void CompilerMigratesLegacyButtonClickEventNames()
        {
            BlueprintSource source = LoadBlueprint("Assets/BlueprintSystem/Sources/UI/InventoryPanel.blueprint.json");
            source.Edges.RemoveAll(edge => edge.From == "bind_close.clicked");
            source.Nodes.Find(node => node.Id == "bind_close").Properties["eventName"] = "CloseClicked";
            BlueprintNodeSource legacyEvent = new BlueprintNodeSource
            {
                Id = "event_close_clicked",
                TypeId = "Game.Event.Custom",
                X = 100,
                Y = 320
            };
            legacyEvent.Properties["eventName"] = "CloseClicked";
            source.Nodes.Add(legacyEvent);
            source.Edges.Add(new BlueprintEdgeSource
            {
                From = "event_close_clicked.execOut",
                To = "log_close_clicked.execIn"
            });

            BlueprintCompileResult result = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.True(result.Success, result.Diagnostics.ToDisplayString());
            List<RuntimeEdge> clickedEdges = result.Blueprint.GetExecEdges(new BlueprintPortKey("bind_close", "clicked"));
            Assert.NotNull(clickedEdges);
            Assert.AreEqual("log_close_clicked", clickedEdges[0].To.NodeId);
        }

        [Test]
        public void RuntimeSetsTextAndExecutesButtonClickOutput()
        {
            RuntimeBlueprint blueprint = CompileInventoryBlueprint();
            GameObject titleObject = new GameObject("TitleText");
            GameObject buttonObject = new GameObject("CloseButton");
            TMP_Text title = titleObject.AddComponent<TextMeshProUGUI>();
            Button button = buttonObject.AddComponent<Button>();
            TestBindingResolver resolver = new TestBindingResolver();
            resolver.Add("TitleText", title);
            resolver.Add("CloseButton", button);

            RecordingBlueprintLogger logger = new RecordingBlueprintLogger();
            BlueprintVM vm = new BlueprintVM();
            BlueprintExecutionContext context = null;
            context = new BlueprintExecutionContext(
                blueprint,
                titleObject,
                null,
                resolver,
                new DictionaryBlueprintVariableStore(blueprint),
                new ActionBlueprintEventBus(eventName => vm.TriggerEvent(context, eventName)),
                logger,
                (node, outputPortId) => vm.ExecuteFromOutput(context, node, outputPortId));

            vm.TriggerEvent(context, "OnOpen");
            button.onClick.Invoke();

            Assert.AreEqual("Inventory", title.text);
            Assert.True(logger.Entries.Exists(entry => entry.Contains("Inventory close clicked")), string.Join("\n", logger.Entries.ToArray()));

            Object.DestroyImmediate(titleObject);
            Object.DestroyImmediate(buttonObject);
        }

        [Test]
        public void ValidatorAcceptsInputPollingNodes()
        {
            BlueprintSource source = CreateVariableTestSource();
            BlueprintNodeSource keyNode = AddNode(source, "listen_space", "Input.ListenKey");
            keyNode.Properties["key"] = "Space";
            BlueprintNodeSource actionNode = AddNode(source, "listen_jump", "Input.ListenAction");
            actionNode.Properties["action"] = "Player/Jump";

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
        }

        [Test]
        public void ValidatorRejectsInvalidEnumProperties()
        {
            BlueprintSource source = CreateVariableTestSource();
            BlueprintNodeSource lowerKey = AddNode(source, "listen_lower_w", "Input.ListenKey");
            lowerKey.Properties["key"] = "w";
            BlueprintNodeSource aliasKey = AddNode(source, "listen_alias_esc", "Input.ListenKey");
            aliasKey.Properties["key"] = "esc";
            BlueprintNodeSource compare = AddNode(source, "compare_invalid", "Variable.Compare");
            compare.Properties["left"] = 1;
            compare.Properties["right"] = 2;
            compare.Properties["comparison"] = "GreaterThan";

            BlueprintNodeSource getTitle = AddNode(source, "get_title_for_key", "Variable.Get");
            getTitle.Properties["name"] = "title";
            AddNode(source, "listen_key_from_string", "Input.ListenKey");
            source.Edges.Add(new BlueprintEdgeSource
            {
                From = "get_title_for_key.value",
                To = "listen_key_from_string.key"
            });

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP012" && diagnostic.NodeId == "listen_lower_w"), diagnostics.ToDisplayString());
            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP012" && diagnostic.NodeId == "listen_alias_esc"), diagnostics.ToDisplayString());
            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP012" && diagnostic.NodeId == "compare_invalid"), diagnostics.ToDisplayString());
            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP003" && diagnostic.NodeId == "listen_key_from_string"), diagnostics.ToDisplayString());
        }

        [Test]
        public void ValidatorReportsMissingInputPollingProperties()
        {
            BlueprintSource source = CreateVariableTestSource();
            AddNode(source, "listen_space", "Input.ListenKey");
            AddNode(source, "listen_jump", "Input.ListenAction");

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP002" && diagnostic.NodeId == "listen_space"), diagnostics.ToDisplayString());
            Assert.True(diagnostics.Exists(diagnostic => diagnostic.Code == "BP002" && diagnostic.NodeId == "listen_jump"), diagnostics.ToDisplayString());
        }

        [Test]
        public void RuntimePollsKeyboardKeyOutputs()
        {
            bool createdKeyboard;
            Keyboard keyboard = GetCurrentKeyboard(out createdKeyboard);
            GameObject owner = new GameObject("InputKeyTestOwner");
            try
            {
                QueueKeyboardState();
                BlueprintSource source = CreateInputPollingRuntimeSource("Input.ListenKey", "key", "Space");
                BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

                BlueprintExecutionContext context;
                RuntimeNode listener;
                RecordingBlueprintLogger logger = CreateInputPollingContext(owner, compileResult.Blueprint, out context, out listener);

                BlueprintExecResult idleResult = PollInputNode(context, listener);
                Assert.AreEqual("bound", idleResult.NextExecPortId);

                QueueKeyboardState(Key.Space);
                AssertInputOutputs(PollInputNode(context, listener), "bound", "pressed");
                AssertInputOutputs(PollInputNode(context, listener), "bound", "held");
                QueueKeyboardState();
                AssertInputOutputs(PollInputNode(context, listener), "bound", "released");

                Assert.AreEqual(1, CountLogEntries(logger, "pressed"), string.Join("\n", logger.Entries.ToArray()));
                Assert.AreEqual(1, CountLogEntries(logger, "held"), string.Join("\n", logger.Entries.ToArray()));
                Assert.AreEqual(1, CountLogEntries(logger, "released"), string.Join("\n", logger.Entries.ToArray()));
            }
            finally
            {
                if (createdKeyboard)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RuntimePollsProjectWideInputActionOutputs()
        {
            InputActionAsset previousActions = InputSystem.actions;
            string actionAssetPath = "Assets/BlueprintSystem/Tests/Editor/InputActionRuntimeTest.inputactions";
            AssetDatabase.DeleteAsset(actionAssetPath);
            bool createdKeyboard;
            Keyboard keyboard = GetCurrentKeyboard(out createdKeyboard);
            GameObject owner = new GameObject("InputActionTestOwner");
            try
            {
                InputActionAsset actions = ScriptableObject.CreateInstance<InputActionAsset>();
                InputActionMap map = new InputActionMap("Player");
                map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space", null, null, null, "Button");
                actions.AddActionMap(map);
                File.WriteAllText(actionAssetPath, actions.ToJson());
                AssetDatabase.ImportAsset(actionAssetPath);
                InputSystem.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(actionAssetPath);
                Object.DestroyImmediate(actions);
                Assert.NotNull(InputSystem.actions);

                QueueKeyboardState();
                BlueprintSource source = CreateInputPollingRuntimeSource("Input.ListenAction", "action", "Player/Jump");
                BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

                BlueprintExecutionContext context;
                RuntimeNode listener;
                RecordingBlueprintLogger logger = CreateInputPollingContext(owner, compileResult.Blueprint, out context, out listener);

                BlueprintExecResult idleResult = PollInputNode(context, listener);
                Assert.AreEqual("bound", idleResult.NextExecPortId);

                QueueKeyboardState(Key.Space);
                AssertInputOutputs(PollInputNode(context, listener), "bound", "pressed");
                AssertInputOutputs(PollInputNode(context, listener), "bound", "held");
                QueueKeyboardState();
                AssertInputOutputs(PollInputNode(context, listener), "bound", "released");

                Assert.AreEqual(1, CountLogEntries(logger, "pressed"), string.Join("\n", logger.Entries.ToArray()));
                Assert.AreEqual(1, CountLogEntries(logger, "held"), string.Join("\n", logger.Entries.ToArray()));
                Assert.AreEqual(1, CountLogEntries(logger, "released"), string.Join("\n", logger.Entries.ToArray()));
            }
            finally
            {
                if (InputSystem.actions != null)
                {
                    InputSystem.actions.Disable();
                }

                InputSystem.actions = previousActions;
                if (createdKeyboard)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                Object.DestroyImmediate(owner);
                AssetDatabase.DeleteAsset(actionAssetPath);
            }
        }

        [Test]
        public void ValidatorAcceptsSetImageSpriteBindings()
        {
            BlueprintSource source = CreateSetImageSpriteTestSource();

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
        }

        [Test]
        public void ValidatorReportsSetImageSpriteBindingProblems()
        {
            BlueprintSource missingValue = CreateSetImageSpriteTestSource();
            missingValue.Nodes.Find(node => node.Id == "set_item_icon").Properties.Remove("value");
            BlueprintDiagnosticList missingValueDiagnostics = new BlueprintValidator().Validate(missingValue, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            BlueprintSource unknownSprite = CreateSetImageSpriteTestSource();
            unknownSprite.Nodes.Find(node => node.Id == "set_item_icon").Properties["value"] = "MissingSprite";
            BlueprintDiagnosticList unknownSpriteDiagnostics = new BlueprintValidator().Validate(unknownSprite, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.True(missingValueDiagnostics.Exists(diagnostic => diagnostic.Code == "BP002"), missingValueDiagnostics.ToDisplayString());
            Assert.True(unknownSpriteDiagnostics.Exists(diagnostic => diagnostic.Code == "BP005"), unknownSpriteDiagnostics.ToDisplayString());
        }

        [Test]
        public void RuntimeSetsImageSprite()
        {
            BlueprintSource source = CreateSetImageSpriteTestSource();
            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject imageObject = new GameObject("ItemIcon");
            Texture2D texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            try
            {
                Image image = imageObject.AddComponent<Image>();
                TestBindingResolver resolver = new TestBindingResolver();
                resolver.Add("ItemIcon", image);
                resolver.Add("SwordSprite", sprite);

                BlueprintExecutionContext context = new BlueprintExecutionContext(
                    compileResult.Blueprint,
                    imageObject,
                    null,
                    resolver,
                    new DictionaryBlueprintVariableStore(compileResult.Blueprint),
                    null,
                    new RecordingBlueprintLogger());

                RuntimeNode node = compileResult.Blueprint.GetNode("set_item_icon");
                BlueprintExecResult result = node.Executor.Execute(context, node);

                Assert.AreEqual("execOut", result.NextExecPortId);
                Assert.AreSame(sprite, image.sprite);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(imageObject);
            }
        }

        [Test]
        public void RuntimeSetsImageSpriteFromSpriteBindingOutput()
        {
            BlueprintSource source = CreateSetImageSpriteWithSpriteBindingTestSource();
            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject imageObject = new GameObject("ItemIcon");
            Texture2D texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            try
            {
                Image image = imageObject.AddComponent<Image>();
                TestBindingResolver resolver = new TestBindingResolver();
                resolver.Add("ItemIcon", image);
                resolver.Add("SwordSprite", sprite);

                BlueprintExecutionContext context = new BlueprintExecutionContext(
                    compileResult.Blueprint,
                    imageObject,
                    null,
                    resolver,
                    new DictionaryBlueprintVariableStore(compileResult.Blueprint),
                    null,
                    new RecordingBlueprintLogger());

                RuntimeNode spriteBinding = compileResult.Blueprint.GetNode("sprite_sword");
                RuntimeNode setSprite = compileResult.Blueprint.GetNode("set_item_icon");
                object bindingName = spriteBinding.Executor.Evaluate(context, spriteBinding, "value");
                BlueprintExecResult result = setSprite.Executor.Execute(context, setSprite);

                Assert.AreEqual("SwordSprite", bindingName);
                Assert.AreEqual("execOut", result.NextExecPortId);
                Assert.AreSame(sprite, image.sprite);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(imageObject);
            }
        }

        [Test]
        public void SpriteAssetDropResolverAcceptsDirectSprite()
        {
            Texture2D texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            try
            {
                List<Sprite> sprites = BlueprintGraphToolkitUIDragDrop.ResolveSpriteAssets(new Object[] { sprite });

                Assert.AreEqual(1, sprites.Count);
                Assert.AreSame(sprite, sprites[0]);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SpriteAssetDropResolverAcceptsSpriteTextureAsset()
        {
            string texturePath = "Assets/BlueprintSystem/Tests/Editor/SpriteDropResolverTest.png";
            AssetDatabase.DeleteAsset(texturePath);
            Texture2D texture = new Texture2D(4, 4);
            try
            {
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(texturePath);
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                Assert.NotNull(importer);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();

                Texture2D textureAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                List<Sprite> sprites = BlueprintGraphToolkitUIDragDrop.ResolveSpriteAssets(new Object[] { textureAsset });

                Assert.AreEqual(1, sprites.Count);
                Assert.AreEqual(Path.GetFileNameWithoutExtension(texturePath), sprites[0].name);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                AssetDatabase.DeleteAsset(texturePath);
            }
        }

        [Test]
        public void VariableStoreAppliesExposedOverrides()
        {
            RuntimeBlueprint blueprint = new RuntimeBlueprint();
            blueprint.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "title",
                Type = "string",
                DefaultValue = "Default",
                Exposed = true
            });
            blueprint.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "hidden",
                Type = "string",
                DefaultValue = "Hidden",
                Exposed = false
            });

            List<BlueprintVariableOverride> overrides = new List<BlueprintVariableOverride>
            {
                new BlueprintVariableOverride { Name = "title", Type = "string", JsonValue = "\"Override\"" },
                new BlueprintVariableOverride { Name = "hidden", Type = "string", JsonValue = "\"Changed\"" }
            };

            DictionaryBlueprintVariableStore store = new DictionaryBlueprintVariableStore(blueprint, overrides);

            Assert.AreEqual("Override", store.Get("title"));
            Assert.AreEqual("Hidden", store.Get("hidden"));
            object titleValue;
            Assert.True(store.TryGet("title", out titleValue));
        }

        [Test]
        public void VariableStoreMatchesOverridesByStableId()
        {
            RuntimeBlueprint blueprint = new RuntimeBlueprint();
            blueprint.Variables.Add(new BlueprintVariableDeclaration
            {
                Id = "var_title",
                Name = "renamedTitle",
                Type = "string",
                DefaultValue = "Default",
                Exposed = true
            });

            DictionaryBlueprintVariableStore store = new DictionaryBlueprintVariableStore(blueprint, new[]
            {
                new BlueprintVariableOverride
                {
                    VariableId = "var_title",
                    Name = "oldTitle",
                    Type = "string",
                    Enabled = true,
                    JsonValue = "\"Override\""
                }
            });

            Assert.AreEqual("Override", store.Get("renamedTitle"));
        }

        [Test]
        public void VariableStoreSkipsDisabledOverridesAndResetsToInstanceInitialValues()
        {
            RuntimeBlueprint blueprint = new RuntimeBlueprint();
            blueprint.Variables.Add(new BlueprintVariableDeclaration
            {
                Id = "var_title",
                Name = "title",
                Type = "string",
                DefaultValue = "Default",
                Exposed = true
            });
            blueprint.Variables.Add(new BlueprintVariableDeclaration
            {
                Id = "var_subtitle",
                Name = "subtitle",
                Type = "string",
                DefaultValue = "Inherited",
                Exposed = true
            });

            DictionaryBlueprintVariableStore store = new DictionaryBlueprintVariableStore(blueprint, new[]
            {
                new BlueprintVariableOverride
                {
                    VariableId = "var_title",
                    Name = "title",
                    Type = "string",
                    Enabled = true,
                    JsonValue = "\"Override\""
                },
                new BlueprintVariableOverride
                {
                    VariableId = "var_subtitle",
                    Name = "subtitle",
                    Type = "string",
                    Enabled = false,
                    JsonValue = "\"Ignored\""
                }
            });

            Assert.AreEqual("Override", store.Get("title"));
            Assert.AreEqual("Inherited", store.Get("subtitle"));

            store.Set("title", "Runtime");
            store.Set("subtitle", "Runtime");
            store.ResetToDefaults();

            Assert.AreEqual("Override", store.Get("title"));
            Assert.AreEqual("Inherited", store.Get("subtitle"));
        }

        [Test]
        public void BlueprintRunnerHidesEventNameFieldsInInspector()
        {
            Assert.NotNull(typeof(BlueprintRunner).GetField("startEventName", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttribute<HideInInspector>());
            Assert.NotNull(typeof(BlueprintRunner).GetField("tickEventName", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttribute<HideInInspector>());
            Assert.NotNull(typeof(BlueprintRunner).GetField("fixedTickEventName", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttribute<HideInInspector>());
            Assert.NotNull(typeof(BlueprintRunner).GetField("lateTickEventName", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttribute<HideInInspector>());
            Assert.Null(typeof(BlueprintRunner).GetField("triggerOnStart", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttribute<HideInInspector>());
        }

        [Test]
        public void UserStructAssetHidesInternalIdentityFieldsInInspector()
        {
            Assert.NotNull(typeof(BlueprintUserStructAsset).GetField("schemaVersion", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttribute<HideInInspector>());
            Assert.NotNull(typeof(BlueprintUserStructAsset).GetField("typeId", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttribute<HideInInspector>());
            Assert.NotNull(typeof(BlueprintUserStructAssetField).GetField("id", BindingFlags.Instance | BindingFlags.Public).GetCustomAttribute<HideInInspector>());
            Assert.Null(typeof(BlueprintUserStructAsset).GetProperty("DisplayName"));
        }

        [Test]
        public void UserStructAssetOnValidateRepairsDuplicatedFieldIds()
        {
            BlueprintUserStructAsset asset = ScriptableObject.CreateInstance<BlueprintUserStructAsset>();
            try
            {
                asset.Fields.Add(new BlueprintUserStructAssetField
                {
                    id = "fld_power",
                    name = "power",
                    fieldType = BlueprintUserStructAssetFieldType.Float
                });
                asset.Fields.Add(new BlueprintUserStructAssetField
                {
                    id = "fld_power",
                    name = "powerCopy",
                    fieldType = BlueprintUserStructAssetFieldType.Float
                });

                MethodInfo onValidate = typeof(BlueprintUserStructAsset).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(onValidate);
                onValidate.Invoke(asset, null);

                Assert.AreEqual("fld_power", asset.Fields[0].id);
                Assert.False(string.IsNullOrEmpty(asset.Fields[1].id));
                Assert.AreNotEqual(asset.Fields[0].id, asset.Fields[1].id);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void VariableSetExecutorWritesRuntimeStoreWithoutChangingDeclarationDefault()
        {
            RuntimeBlueprint blueprint = new RuntimeBlueprint();
            BlueprintVariableDeclaration declaration = new BlueprintVariableDeclaration
            {
                Name = "count",
                Type = "int",
                DefaultValue = 0
            };
            blueprint.Variables.Add(declaration);

            BlueprintNodeManifest setManifest;
            LoadManifests().TryGet("Variable.Set", out setManifest);
            RuntimeNode node = new RuntimeNode
            {
                Id = "set_count",
                TypeId = "Variable.Set",
                Manifest = setManifest,
                Executor = new VariableSetExecutor()
            };
            node.Properties["name"] = "count";
            node.Properties["value"] = 12;

            GameObject owner = new GameObject("VariableSetTestOwner");
            DictionaryBlueprintVariableStore store = new DictionaryBlueprintVariableStore(blueprint);
            try
            {
                BlueprintExecutionContext context = new BlueprintExecutionContext(
                    blueprint,
                    owner,
                    null,
                    new NullBlueprintBindingResolver(),
                    store,
                    null,
                    new RecordingBlueprintLogger());

                BlueprintExecResult result = new VariableSetExecutor().Execute(context, node);

                Assert.AreEqual("execOut", result.NextExecPortId);
                Assert.AreEqual(12, store.Get("count"));
                Assert.AreEqual(0, declaration.DefaultValue);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ValidatorAcceptsLogicAndCollisionNodes()
        {
            BlueprintSource source = CreateCollisionTestSource();
            BlueprintNodeSource andNode = AddNode(source, "and_condition", "Logic.And");
            andNode.Properties["right"] = true;
            BlueprintNodeSource branch = AddNode(source, "branch_collision", "Flow.Branch");
            source.Edges.Add(new BlueprintEdgeSource
            {
                From = "is_colliding.result",
                To = "and_condition.left"
            });
            source.Edges.Add(new BlueprintEdgeSource
            {
                From = "and_condition.result",
                To = "branch_collision.condition"
            });

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
        }

        [Test]
        public void RuntimeEvaluatesLogicNodes()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "LogicRuntimeTest";
            BlueprintNodeSource andNode = AddNode(source, "and_node", "Logic.And");
            andNode.Properties["left"] = true;
            andNode.Properties["right"] = false;
            BlueprintNodeSource orNode = AddNode(source, "or_node", "Logic.Or");
            orNode.Properties["left"] = true;
            orNode.Properties["right"] = false;
            BlueprintNodeSource notNode = AddNode(source, "not_node", "Logic.Not");
            notNode.Properties["value"] = false;

            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());
            BlueprintExecutionContext context = new BlueprintExecutionContext(
                compileResult.Blueprint,
                null,
                null,
                new NullBlueprintBindingResolver(),
                new DictionaryBlueprintVariableStore(compileResult.Blueprint),
                null,
                new RecordingBlueprintLogger());

            Assert.AreEqual(false, compileResult.Blueprint.GetNode("and_node").Executor.Evaluate(context, compileResult.Blueprint.GetNode("and_node"), "result"));
            Assert.AreEqual(true, compileResult.Blueprint.GetNode("or_node").Executor.Evaluate(context, compileResult.Blueprint.GetNode("or_node"), "result"));
            Assert.AreEqual(true, compileResult.Blueprint.GetNode("not_node").Executor.Evaluate(context, compileResult.Blueprint.GetNode("not_node"), "result"));
        }

        [Test]
        public void RuntimeDetects2DColliderOverlap()
        {
            BlueprintSource source = CreateCollisionTestSource();
            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject player = new GameObject("Player");
            GameObject enemy = new GameObject("Enemy");
            try
            {
                player.AddComponent<BoxCollider2D>();
                enemy.AddComponent<BoxCollider2D>();
                TestBindingResolver resolver = new TestBindingResolver();
                resolver.Add("Player", player);
                resolver.Add("Enemy", enemy);
                BlueprintExecutionContext context = new BlueprintExecutionContext(
                    compileResult.Blueprint,
                    player,
                    null,
                    resolver,
                    new DictionaryBlueprintVariableStore(compileResult.Blueprint),
                    null,
                    new RecordingBlueprintLogger());

                RuntimeNode collisionNode = compileResult.Blueprint.GetNode("is_colliding");
                Assert.AreEqual(true, collisionNode.Executor.Evaluate(context, collisionNode, "result"));

                enemy.transform.position = new Vector3(10f, 0f, 0f);
                Physics2D.SyncTransforms();
                context.ClearValueCache();

                Assert.AreEqual(false, collisionNode.Executor.Evaluate(context, collisionNode, "result"));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void RuntimeSetsTransformProperties()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "TransformRuntimeTest";
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "Actor",
                Type = "Transform",
                Required = true
            });

            BlueprintNodeSource position = AddNode(source, "set_position", "Game.SetTransformPosition");
            position.Properties["target"] = "Actor";
            position.Properties["value"] = new List<object> { 1f, 2f, 3f };
            BlueprintNodeSource rotation = AddNode(source, "set_rotation", "Game.SetTransformEulerAngles");
            rotation.Properties["target"] = "Actor";
            rotation.Properties["value"] = new List<object> { 10f, 20f, 30f };
            BlueprintNodeSource scale = AddNode(source, "set_scale", "Game.SetTransformLocalScale");
            scale.Properties["target"] = "Actor";
            scale.Properties["value"] = new List<object> { 2f, 3f, 4f };

            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject actor = new GameObject("Actor");
            try
            {
                TestBindingResolver resolver = new TestBindingResolver();
                resolver.Add("Actor", actor);
                BlueprintExecutionContext context = CreateTestExecutionContext(compileResult.Blueprint, actor, resolver);

                ExecuteNode(compileResult.Blueprint, context, "set_position");
                ExecuteNode(compileResult.Blueprint, context, "set_rotation");
                ExecuteNode(compileResult.Blueprint, context, "set_scale");

                Assert.AreEqual(new Vector3(1f, 2f, 3f), actor.transform.position);
                Assert.AreEqual(new Vector3(10f, 20f, 30f), actor.transform.eulerAngles);
                Assert.AreEqual(new Vector3(2f, 3f, 4f), actor.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void RuntimeSets3DPhysicsProperties()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "Physics3DRuntimeTest";
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "Body",
                Type = "Rigidbody",
                Required = true
            });
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "Collider",
                Type = "Collider",
                Required = true
            });

            BlueprintNodeSource velocity = AddNode(source, "set_velocity", "Game.SetRigidbodyLinearVelocity");
            velocity.Properties["target"] = "Body";
            velocity.Properties["value"] = new List<object> { 1f, 2f, 3f };
            BlueprintNodeSource force = AddNode(source, "add_force", "Game.AddRigidbodyForce");
            force.Properties["target"] = "Body";
            force.Properties["force"] = new List<object> { 0f, 1f, 0f };
            force.Properties["mode"] = "VelocityChange";
            BlueprintNodeSource enabled = AddNode(source, "set_collider_enabled", "Game.SetColliderEnabled");
            enabled.Properties["target"] = "Collider";
            enabled.Properties["value"] = false;
            BlueprintNodeSource trigger = AddNode(source, "set_collider_trigger", "Game.SetColliderIsTrigger");
            trigger.Properties["target"] = "Collider";
            trigger.Properties["value"] = true;

            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject bodyObject = new GameObject("Body");
            try
            {
                Rigidbody body = bodyObject.AddComponent<Rigidbody>();
                BoxCollider boxCollider = bodyObject.AddComponent<BoxCollider>();
                TestBindingResolver resolver = new TestBindingResolver();
                resolver.Add("Body", bodyObject);
                resolver.Add("Collider", bodyObject);
                BlueprintExecutionContext context = CreateTestExecutionContext(compileResult.Blueprint, bodyObject, resolver);

                ExecuteNode(compileResult.Blueprint, context, "set_velocity");
                Assert.AreEqual(new Vector3(1f, 2f, 3f), body.linearVelocity);

                ExecuteNode(compileResult.Blueprint, context, "add_force");
                ExecuteNode(compileResult.Blueprint, context, "set_collider_enabled");
                ExecuteNode(compileResult.Blueprint, context, "set_collider_trigger");
                Assert.False(boxCollider.enabled);
                Assert.True(boxCollider.isTrigger);
            }
            finally
            {
                Object.DestroyImmediate(bodyObject);
            }
        }

        [Test]
        public void RuntimeSets2DPhysicsProperties()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "Physics2DRuntimeTest";
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "Body2D",
                Type = "Rigidbody2D",
                Required = true
            });
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "Collider2D",
                Type = "Collider2D",
                Required = true
            });

            BlueprintNodeSource velocity = AddNode(source, "set_velocity_2d", "Game.SetRigidbody2DLinearVelocity");
            velocity.Properties["target"] = "Body2D";
            velocity.Properties["value"] = new List<object> { 4f, 5f };
            BlueprintNodeSource force = AddNode(source, "add_force_2d", "Game.AddRigidbody2DForce");
            force.Properties["target"] = "Body2D";
            force.Properties["force"] = new List<object> { 1f, 0f };
            force.Properties["mode"] = "Impulse";
            BlueprintNodeSource enabled = AddNode(source, "set_collider_2d_enabled", "Game.SetCollider2DEnabled");
            enabled.Properties["target"] = "Collider2D";
            enabled.Properties["value"] = false;
            BlueprintNodeSource trigger = AddNode(source, "set_collider_2d_trigger", "Game.SetCollider2DIsTrigger");
            trigger.Properties["target"] = "Collider2D";
            trigger.Properties["value"] = true;

            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            GameObject bodyObject = new GameObject("Body2D");
            try
            {
                Rigidbody2D body = bodyObject.AddComponent<Rigidbody2D>();
                BoxCollider2D boxCollider = bodyObject.AddComponent<BoxCollider2D>();
                TestBindingResolver resolver = new TestBindingResolver();
                resolver.Add("Body2D", bodyObject);
                resolver.Add("Collider2D", bodyObject);
                BlueprintExecutionContext context = CreateTestExecutionContext(compileResult.Blueprint, bodyObject, resolver);

                ExecuteNode(compileResult.Blueprint, context, "set_velocity_2d");
                Assert.AreEqual(new Vector2(4f, 5f), body.linearVelocity);

                ExecuteNode(compileResult.Blueprint, context, "add_force_2d");
                ExecuteNode(compileResult.Blueprint, context, "set_collider_2d_enabled");
                ExecuteNode(compileResult.Blueprint, context, "set_collider_2d_trigger");
                Assert.False(boxCollider.enabled);
                Assert.True(boxCollider.isTrigger);
            }
            finally
            {
                Object.DestroyImmediate(bodyObject);
            }
        }

        [Test]
        public void RuntimeSetsRendererMaterialColorAndTexture()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "RenderingRuntimeTest";
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "Renderer",
                Type = "Renderer",
                Required = true
            });
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "ReplacementMaterial",
                Type = "Material",
                Required = true
            });
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "RuntimeTexture",
                Type = "Texture",
                Required = true
            });

            BlueprintNodeSource materialNode = AddNode(source, "set_material", "Game.SetRendererMaterial");
            materialNode.Properties["target"] = "Renderer";
            materialNode.Properties["value"] = "ReplacementMaterial";
            materialNode.Properties["materialIndex"] = 0;
            BlueprintNodeSource colorNode = AddNode(source, "set_color", "Game.SetRendererMaterialColor");
            colorNode.Properties["target"] = "Renderer";
            colorNode.Properties["value"] = new List<object> { 0.2f, 0.3f, 0.4f, 0.5f };
            colorNode.Properties["propertyName"] = "_Color";
            BlueprintNodeSource textureNode = AddNode(source, "set_texture", "Game.SetRendererTexture");
            textureNode.Properties["target"] = "Renderer";
            textureNode.Properties["value"] = "RuntimeTexture";
            textureNode.Properties["propertyName"] = "_MainTex";

            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("UI/Default");
            }

            Assert.NotNull(shader);

            GameObject renderObject = new GameObject("Renderable");
            Material initialMaterial = new Material(shader);
            Material replacementMaterial = new Material(shader);
            Texture2D texture = new Texture2D(2, 2);
            try
            {
                MeshRenderer renderer = renderObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = initialMaterial;
                TestBindingResolver resolver = new TestBindingResolver();
                resolver.Add("Renderer", renderObject);
                resolver.Add("ReplacementMaterial", replacementMaterial);
                resolver.Add("RuntimeTexture", texture);
                BlueprintExecutionContext context = CreateTestExecutionContext(compileResult.Blueprint, renderObject, resolver);

                ExecuteNode(compileResult.Blueprint, context, "set_material");
                ExecuteNode(compileResult.Blueprint, context, "set_color");
                ExecuteNode(compileResult.Blueprint, context, "set_texture");

                Material activeMaterial = renderer.material;
                Assert.AreEqual(new Color(0.2f, 0.3f, 0.4f, 0.5f), activeMaterial.GetColor("_Color"));
                Assert.AreEqual(texture, activeMaterial.GetTexture("_MainTex"));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(initialMaterial);
                Object.DestroyImmediate(replacementMaterial);
                Object.DestroyImmediate(renderObject);
            }
        }

        [Test]
        public void BlueprintAccessNodesUseBlueprintTargetsInManifestAndGraphToolkit()
        {
            BlueprintNodeManifestCollection manifests = LoadManifests();
            string[] targetNodeIds =
            {
                "Blueprint.IsValid",
                "Blueprint.TriggerEvent",
                "Blueprint.GetVariable",
                "Blueprint.SetVariable"
            };

            for (int i = 0; i < targetNodeIds.Length; i++)
            {
                BlueprintNodeManifest manifest;
                Assert.True(manifests.TryGet(targetNodeIds[i], out manifest), targetNodeIds[i]);
                BlueprintPortSpec targetInput = manifest.FindInput("target");
                BlueprintPropertySpec targetProperty = manifest.FindProperty("target");
                Assert.NotNull(targetInput, targetNodeIds[i]);
                Assert.AreEqual(BlueprintVariableTypeRegistry.BlueprintAssetTypeId, targetInput.Type, targetNodeIds[i]);
                Assert.AreEqual(BlueprintValueSource.PropertyOrConnection, targetInput.Source, targetNodeIds[i]);
                Assert.NotNull(targetProperty, targetNodeIds[i]);
                Assert.AreEqual(BlueprintVariableTypeRegistry.BlueprintAssetTypeId, targetProperty.Type, targetNodeIds[i]);

                BlueprintVisualNode visualNode = BlueprintVisualNodeFactory.Create(targetNodeIds[i]);
                Assert.AreEqual(targetNodeIds[i], visualNode.ReadTypeId());
                BlueprintVisualPortData visualTargetInput = visualNode.Inputs.Find(port => port.Id == "target");
                BlueprintVisualPropertyData visualTargetProperty = visualNode.Properties.Find(property => property.Id == "target");
                Assert.NotNull(visualTargetInput, targetNodeIds[i]);
                Assert.AreEqual(BlueprintVariableTypeRegistry.BlueprintAssetTypeId, visualTargetInput.Type, targetNodeIds[i]);
                Assert.AreEqual("propertyOrConnection", visualTargetInput.Source, targetNodeIds[i]);
                Assert.NotNull(visualTargetProperty, targetNodeIds[i]);
                Assert.AreEqual(BlueprintVariableTypeRegistry.BlueprintAssetTypeId, visualTargetProperty.Type, targetNodeIds[i]);
                Assert.True(visualTargetProperty.ShowInInspectorOnly, targetNodeIds[i]);
            }

            string[] runtimeRefNodeIds =
            {
                "Blueprint.GetOwner",
                "Blueprint.GetComponent"
            };

            for (int i = 0; i < runtimeRefNodeIds.Length; i++)
            {
                BlueprintNodeManifest manifest;
                Assert.True(manifests.TryGet(runtimeRefNodeIds[i], out manifest), runtimeRefNodeIds[i]);
                BlueprintPortSpec targetOutput = manifest.FindOutput("target");
                Assert.NotNull(targetOutput, runtimeRefNodeIds[i]);
                Assert.AreEqual(BlueprintVariableTypeRegistry.BlueprintRefTypeId, targetOutput.Type, runtimeRefNodeIds[i]);

                BlueprintVisualNode visualNode = BlueprintVisualNodeFactory.Create(runtimeRefNodeIds[i]);
                Assert.AreEqual(runtimeRefNodeIds[i], visualNode.ReadTypeId());
                Assert.AreNotEqual(typeof(BlueprintVisualNode), visualNode.GetType(), runtimeRefNodeIds[i]);
                BlueprintVisualPortData visualTargetOutput = visualNode.Outputs.Find(port => port.Id == "target");
                Assert.NotNull(visualTargetOutput, runtimeRefNodeIds[i]);
                Assert.AreEqual(BlueprintVariableTypeRegistry.BlueprintRefTypeId, visualTargetOutput.Type, runtimeRefNodeIds[i]);
            }

            BlueprintNodeManifest getComponentManifest;
            Assert.True(manifests.TryGet("Blueprint.GetComponent", out getComponentManifest));
            BlueprintPortSpec componentNameInput = getComponentManifest.FindInput("name");
            Assert.NotNull(componentNameInput);
            Assert.AreEqual("string", componentNameInput.Type);
            Assert.AreEqual(BlueprintValueSource.PropertyOrConnection, componentNameInput.Source);
            Assert.True(BlueprintTypeUtility.IsCompatible(BlueprintVariableTypeRegistry.BlueprintRefTypeId, BlueprintVariableTypeRegistry.BlueprintAssetTypeId));
            Assert.False(BlueprintTypeUtility.IsCompatible(BlueprintVariableTypeRegistry.BlueprintAssetTypeId, BlueprintVariableTypeRegistry.BlueprintRefTypeId));

            string[] removedNodeIds =
            {
                "Blueprint.GetByBinding",
                "Blueprint.FindByTag"
            };

            for (int i = 0; i < removedNodeIds.Length; i++)
            {
                BlueprintNodeManifest manifest;
                Assert.False(manifests.TryGet(removedNodeIds[i], out manifest), removedNodeIds[i]);
                Assert.AreEqual(typeof(BlueprintVisualNode), BlueprintVisualNodeFactory.Create(removedNodeIds[i]).GetType(), removedNodeIds[i]);
            }
        }

        [Test]
        public void ValidatorAcceptsBlueprintVariableTargetConnections()
        {
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/CrossBlueprintTarget.blueprint.json";
            BlueprintSource source = CreateBlueprintAssetTargetConnectionSource(componentPath);

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
        }

        [Test]
        public void GraphToolkitRoundTripsBlueprintVariableTargetConnections()
        {
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/CrossBlueprintTarget.blueprint.json";
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/BlueprintAssetTargetConnectionTest.blueprint.json";
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/BlueprintAssetTargetConnectionTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/BlueprintAssetTargetConnectionTest.export.blueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(blueprintPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                WriteTemporaryBlueprintAsset(blueprintPath, CreateBlueprintAssetTargetConnectionSource(componentPath));
                BlueprintGraphToolkitBridge.ImportBlueprintAtPath(blueprintPath, graphPath, false);
                BlueprintVisualGraph graph = GraphDatabase.LoadGraph<BlueprintVisualGraph>(graphPath);
                Assert.True(graph.GetVariables().Any(variable => variable.name == "targetBlueprint"));
                BlueprintVisualNode blueprintGetVariable = graph.GetNodes().OfType<BlueprintVisualNode>().First(node => node.ReadNodeId() == "read_target_count");
                BlueprintVisualPropertyData targetProperty = blueprintGetVariable.Properties.Find(property => property.Id == "target");
                Assert.NotNull(targetProperty);
                Assert.True(targetProperty.ShowInInspectorOnly);

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);

                Assert.True(exported.Nodes.Exists(node => node.TypeId == "Variable.Get" && (string)node.Properties["name"] == "targetBlueprint"));
                Assert.True(exported.Nodes.Exists(node => node.TypeId == "Blueprint.GetVariable" && (string)node.Properties["name"] == "publicCount"));
                Assert.True(exported.Edges.Exists(edge => edge.From.EndsWith(".value") && edge.To == "read_target_count.target"));

                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(exported, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(blueprintPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitRoundTripsBlueprintRefTargetConnections()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/BlueprintRefTargetConnectionTest.blueprint.json";
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/BlueprintRefTargetConnectionTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/BlueprintRefTargetConnectionTest.export.blueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(blueprintPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                BlueprintSource source = new BlueprintSource();
                source.SchemaVersion = "0.1";
                source.Name = "BlueprintRefTargetConnectionTest";
                BlueprintNodeSource getComponent = AddNode(source, "get_target_component", "Blueprint.GetComponent");
                getComponent.Properties["name"] = "TargetComponent";
                BlueprintNodeSource readTarget = AddNode(source, "read_target_count", "Blueprint.GetVariable");
                readTarget.Properties["name"] = "publicCount";
                source.Edges.Add(new BlueprintEdgeSource
                {
                    From = "get_target_component.target",
                    To = "read_target_count.target"
                });

                WriteTemporaryBlueprintAsset(blueprintPath, source);
                BlueprintGraphToolkitBridge.ImportBlueprintAtPath(blueprintPath, graphPath, false);
                BlueprintVisualGraph graph = GraphDatabase.LoadGraph<BlueprintVisualGraph>(graphPath);
                BlueprintVisualNode getComponentNode = graph.GetNodes().OfType<BlueprintVisualNode>().First(node => node.ReadNodeId() == "get_target_component");
                BlueprintVisualNode getVariableNode = graph.GetNodes().OfType<BlueprintVisualNode>().First(node => node.ReadNodeId() == "read_target_count");
                Assert.AreEqual(BlueprintVariableTypeRegistry.BlueprintRefTypeId, getComponentNode.Outputs.Find(port => port.Id == "target").Type);
                Assert.AreEqual(BlueprintVariableTypeRegistry.BlueprintAssetTypeId, getVariableNode.Inputs.Find(port => port.Id == "target").Type);

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);

                Assert.True(exported.Nodes.Exists(node => node.TypeId == "Blueprint.GetComponent" && (string)node.Properties["name"] == "TargetComponent"));
                Assert.True(exported.Nodes.Exists(node => node.TypeId == "Blueprint.GetVariable" && (string)node.Properties["name"] == "publicCount"));
                Assert.True(exported.Edges.Exists(edge => edge.From == "get_target_component.target" && edge.To == "read_target_count.target"));

                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(exported, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(blueprintPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitBlueprintAccessTargetHidesEmbeddedPathEditor()
        {
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/CrossBlueprintTarget.blueprint.json";
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/BlueprintAccessDirectTargetTest.blueprint.json";
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/BlueprintAccessDirectTargetTest.bpgraph";
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(blueprintPath);

            try
            {
                BlueprintSource source = new BlueprintSource();
                source.SchemaVersion = "0.1";
                source.Name = "BlueprintAccessDirectTargetTest";
                BlueprintNodeSource setTarget = AddNode(source, "set_target_count", "Blueprint.SetVariable");
                setTarget.Properties["target"] = componentPath;
                setTarget.Properties["name"] = "publicCount";
                setTarget.Properties["value"] = 7;

                WriteTemporaryBlueprintAsset(blueprintPath, source);
                BlueprintGraphToolkitBridge.ImportBlueprintAtPath(blueprintPath, graphPath, false);
                BlueprintVisualGraph graph = GraphDatabase.LoadGraph<BlueprintVisualGraph>(graphPath);
                BlueprintVisualNode setTargetNode = graph.GetNodes().OfType<BlueprintVisualNode>().First(node => node.ReadNodeId() == "set_target_count");
                BlueprintVisualPropertyData targetProperty = setTargetNode.Properties.Find(property => property.Id == "target");

                Assert.NotNull(targetProperty);
                Assert.True(targetProperty.ShowInInspectorOnly);

                BlueprintSystem.Editor.Blueprint embeddedTarget;
                Assert.False(setTargetNode.GetInputPortByName("target").TryGetValue(out embeddedTarget));

                object targetValue;
                Assert.True(setTargetNode.TryReadPropertyValue(targetProperty, out targetValue));
                Assert.AreEqual(componentPath, targetValue);
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(blueprintPath);
            }
        }

        [Test]
        public void RuntimeBlueprintAssetTargetsAccessCurrentComponentTree()
        {
            string ownerPath = "Assets/BlueprintSystem/Tests/Editor/Owner.blueprint.json";
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/CrossBlueprintTarget.blueprint.json";
            BlueprintCompiledAsset componentAsset = CreateCrossBlueprintTargetCompiledAsset(componentPath);
            BlueprintCompiledAsset ownerAsset = CreateOwnerCompiledAsset(ownerPath, componentAsset, componentPath, "TargetComponent");
            GameObject ownerObject = new GameObject("BlueprintAssetTargetOwner");

            try
            {
                BlueprintRunner runner = ownerObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", ownerAsset);
                Assert.True(runner.Compile());

                IBlueprintInstance component;
                Assert.True(runner.TryGetBlueprintComponent("TargetComponent", out component));
                RecordingBlueprintLogger logger = new RecordingBlueprintLogger();
                BlueprintExecutionContext context = CreateBlueprintInstanceContext(runner, logger);

                RuntimeNode validNode = CreateRuntimeNode("is_valid", "Blueprint.IsValid");
                validNode.Properties["target"] = componentPath;
                Assert.True((bool)new BlueprintIsValidExecutor().Evaluate(context, validNode, "result"));

                BlueprintGetVariableExecutor getExecutor = new BlueprintGetVariableExecutor();
                RuntimeNode getNode = CreateRuntimeNode("get_public_count", "Blueprint.GetVariable");
                getNode.Properties["target"] = componentPath;
                getNode.Properties["name"] = "publicCount";
                Assert.AreEqual(3, getExecutor.Evaluate(context, getNode, "value"));
                Assert.True((bool)getExecutor.Evaluate(context, getNode, "success"));

                BlueprintSetVariableExecutor setExecutor = new BlueprintSetVariableExecutor();
                RuntimeNode setNode = CreateRuntimeNode("set_public_count", "Blueprint.SetVariable");
                setNode.Properties["target"] = componentPath;
                setNode.Properties["name"] = "publicCount";
                setNode.Properties["value"] = 9;
                BlueprintExecResult setResult = setExecutor.Execute(context, setNode);
                Assert.AreEqual("execOut", setResult.NextExecPortId);

                object publicValue;
                Assert.True(component.TryGetVariable("publicCount", out publicValue));
                Assert.AreEqual(9, publicValue);

                RuntimeNode triggerNode = CreateRuntimeNode("trigger_ping", "Blueprint.TriggerEvent");
                triggerNode.Properties["target"] = componentPath;
                triggerNode.Properties["eventName"] = "Ping";
                BlueprintExecResult triggerResult = new BlueprintTriggerEventExecutor().Execute(context, triggerNode);
                Assert.AreEqual("execOut", triggerResult.NextExecPortId);

                object fired;
                Assert.True(component.TryGetVariable("fired", out fired));
                Assert.AreEqual(true, fired);

                getNode.Properties["name"] = "hiddenCount";
                Assert.False((bool)getExecutor.Evaluate(context, getNode, "success"));
                Assert.Null(getExecutor.Evaluate(context, getNode, "value"));

                setNode.Properties["name"] = "hiddenCount";
                setNode.Properties["value"] = 12;
                BlueprintExecResult hiddenSetResult = setExecutor.Execute(context, setNode);
                Assert.False(string.IsNullOrEmpty(hiddenSetResult.ErrorMessage));

                object hiddenValue;
                Assert.True(component.TryGetVariable("hiddenCount", out hiddenValue));
                Assert.AreEqual(2, hiddenValue);
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
                Object.DestroyImmediate(ownerAsset);
                Object.DestroyImmediate(componentAsset);
            }
        }

        [Test]
        public void RuntimeBlueprintAssetTargetsAcceptBlueprintRefTargets()
        {
            string ownerPath = "Assets/BlueprintSystem/Tests/Editor/Owner.blueprint.json";
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/CrossBlueprintTarget.blueprint.json";
            BlueprintCompiledAsset componentAsset = CreateCrossBlueprintTargetCompiledAsset(componentPath);
            BlueprintCompiledAsset ownerAsset = CreateOwnerCompiledAsset(ownerPath, componentAsset, componentPath, "TargetComponent");
            GameObject ownerObject = new GameObject("BlueprintAssetTargetOwner");

            try
            {
                BlueprintRunner runner = ownerObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", ownerAsset);
                Assert.True(runner.Compile());

                IBlueprintInstance component;
                Assert.True(runner.TryGetBlueprintComponent("TargetComponent", out component));
                BlueprintExecutionContext context = CreateBlueprintInstanceContext(runner, new RecordingBlueprintLogger());
                BlueprintRef runtimeReference = new BlueprintRef(component);

                RuntimeNode validNode = CreateRuntimeNode("is_valid", "Blueprint.IsValid");
                validNode.Properties["target"] = runtimeReference;
                Assert.True((bool)new BlueprintIsValidExecutor().Evaluate(context, validNode, "result"));

                BlueprintGetVariableExecutor getExecutor = new BlueprintGetVariableExecutor();
                RuntimeNode getNode = CreateRuntimeNode("get_public_count", "Blueprint.GetVariable");
                getNode.Properties["target"] = runtimeReference;
                getNode.Properties["name"] = "publicCount";
                Assert.AreEqual(3, getExecutor.Evaluate(context, getNode, "value"));
                Assert.True((bool)getExecutor.Evaluate(context, getNode, "success"));

                RuntimeNode setNode = CreateRuntimeNode("set_public_count", "Blueprint.SetVariable");
                setNode.Properties["target"] = runtimeReference;
                setNode.Properties["name"] = "publicCount";
                setNode.Properties["value"] = 9;
                BlueprintExecResult setResult = new BlueprintSetVariableExecutor().Execute(context, setNode);
                Assert.AreEqual("execOut", setResult.NextExecPortId);

                object publicValue;
                Assert.True(component.TryGetVariable("publicCount", out publicValue));
                Assert.AreEqual(9, publicValue);
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
                Object.DestroyImmediate(ownerAsset);
                Object.DestroyImmediate(componentAsset);
            }
        }

        [Test]
        public void RuntimeBlueprintGetOwnerReturnsParentInstanceRef()
        {
            string ownerPath = "Assets/BlueprintSystem/Tests/Editor/OwnerAccess.blueprint.json";
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/CrossBlueprintTarget.blueprint.json";
            BlueprintCompiledAsset componentAsset = CreateCrossBlueprintTargetCompiledAsset(componentPath);
            BlueprintCompiledAsset ownerAsset = CreateOwnerAccessCompiledAsset(ownerPath, componentAsset, componentPath, "ChildComponent");
            GameObject ownerObject = new GameObject("BlueprintOwnerRefTarget");

            try
            {
                BlueprintRunner runner = ownerObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", ownerAsset);
                Assert.True(runner.Compile());

                IBlueprintInstance component;
                Assert.True(runner.TryGetBlueprintComponent("ChildComponent", out component));
                BlueprintExecutionContext componentContext = CreateBlueprintComponentContext(component, new RecordingBlueprintLogger());
                RuntimeNode getOwnerNode = CreateRuntimeNode("get_owner", "Blueprint.GetOwner");
                BlueprintRef ownerRef = (BlueprintRef)new BlueprintGetOwnerExecutor().Evaluate(componentContext, getOwnerNode, "target");
                Assert.NotNull(ownerRef);
                Assert.AreSame(runner, ownerRef.Instance);
                Assert.True((bool)new BlueprintGetOwnerExecutor().Evaluate(componentContext, getOwnerNode, "isValid"));

                RuntimeNode setOwnerNode = CreateRuntimeNode("set_owner_count", "Blueprint.SetVariable");
                setOwnerNode.Properties["target"] = ownerRef;
                setOwnerNode.Properties["name"] = "ownerCount";
                setOwnerNode.Properties["value"] = 17;
                BlueprintExecResult setResult = new BlueprintSetVariableExecutor().Execute(componentContext, setOwnerNode);
                Assert.AreEqual("execOut", setResult.NextExecPortId);

                object ownerCount;
                Assert.True(runner.TryGetVariable("ownerCount", out ownerCount));
                Assert.AreEqual(17, ownerCount);

                RuntimeNode triggerOwnerNode = CreateRuntimeNode("trigger_owner", "Blueprint.TriggerEvent");
                triggerOwnerNode.Properties["target"] = ownerRef;
                triggerOwnerNode.Properties["eventName"] = "PingOwner";
                BlueprintExecResult triggerResult = new BlueprintTriggerEventExecutor().Execute(componentContext, triggerOwnerNode);
                Assert.AreEqual("execOut", triggerResult.NextExecPortId);

                object ownerFired;
                Assert.True(runner.TryGetVariable("ownerFired", out ownerFired));
                Assert.AreEqual(true, ownerFired);
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
                Object.DestroyImmediate(ownerAsset);
                Object.DestroyImmediate(componentAsset);
            }
        }

        [Test]
        public void RuntimeBlueprintGetComponentFindsSiblingThroughOwnerChain()
        {
            string ownerPath = "Assets/BlueprintSystem/Tests/Editor/Owner.blueprint.json";
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/CrossBlueprintTarget.blueprint.json";
            BlueprintCompiledAsset componentAsset = CreateCrossBlueprintTargetCompiledAsset(componentPath);
            BlueprintCompiledAsset ownerAsset = CreateOwnerCompiledAsset(ownerPath, componentAsset, componentPath, "SourceComponent", "TargetComponent");
            GameObject ownerObject = new GameObject("BlueprintSiblingComponentTarget");

            try
            {
                BlueprintRunner runner = ownerObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", ownerAsset);
                Assert.True(runner.Compile());

                IBlueprintInstance sourceComponent;
                IBlueprintInstance targetComponent;
                Assert.True(runner.TryGetBlueprintComponent("SourceComponent", out sourceComponent));
                Assert.True(runner.TryGetBlueprintComponent("TargetComponent", out targetComponent));

                BlueprintExecutionContext sourceContext = CreateBlueprintComponentContext(sourceComponent, new RecordingBlueprintLogger());
                RuntimeNode getComponentNode = CreateRuntimeNode("get_target_component", "Blueprint.GetComponent");
                getComponentNode.Properties["name"] = "TargetComponent";
                BlueprintRef targetRef = (BlueprintRef)new BlueprintGetComponentExecutor().Evaluate(sourceContext, getComponentNode, "target");
                Assert.NotNull(targetRef);
                Assert.AreSame(targetComponent, targetRef.Instance);
                Assert.True((bool)new BlueprintGetComponentExecutor().Evaluate(sourceContext, getComponentNode, "isValid"));

                RuntimeNode getNode = CreateRuntimeNode("get_public_count", "Blueprint.GetVariable");
                getNode.Properties["target"] = targetRef;
                getNode.Properties["name"] = "publicCount";
                Assert.AreEqual(3, new BlueprintGetVariableExecutor().Evaluate(sourceContext, getNode, "value"));
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
                Object.DestroyImmediate(ownerAsset);
                Object.DestroyImmediate(componentAsset);
            }
        }

        [Test]
        public void RuntimeBlueprintAssetTargetsDoNotSearchSceneRunners()
        {
            string ownerPath = "Assets/BlueprintSystem/Tests/Editor/Owner.blueprint.json";
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/OwnedComponent.blueprint.json";
            string sceneOnlyPath = "Assets/BlueprintSystem/Tests/Editor/SceneOnlyTarget.blueprint.json";
            BlueprintCompiledAsset componentAsset = CreateCrossBlueprintTargetCompiledAsset(componentPath);
            BlueprintCompiledAsset ownerAsset = CreateOwnerCompiledAsset(ownerPath, componentAsset, componentPath, "TargetComponent");
            BlueprintCompiledAsset sceneAsset = CreateCrossBlueprintTargetCompiledAsset(sceneOnlyPath);
            GameObject ownerObject = new GameObject("BlueprintAssetTargetOwner");
            GameObject sceneObject = new GameObject("SceneOnlyBlueprintTarget");

            try
            {
                BlueprintRunner runner = ownerObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", ownerAsset);
                Assert.True(runner.Compile());

                BlueprintRunner sceneRunner = sceneObject.AddComponent<BlueprintRunner>();
                SetPrivateField(sceneRunner, "compiledBlueprint", sceneAsset);
                Assert.True(sceneRunner.Compile());

                BlueprintExecutionContext context = CreateBlueprintInstanceContext(runner, new RecordingBlueprintLogger());
                RuntimeNode validNode = CreateRuntimeNode("is_scene_target_valid", "Blueprint.IsValid");
                validNode.Properties["target"] = sceneOnlyPath;
                Assert.False((bool)new BlueprintIsValidExecutor().Evaluate(context, validNode, "result"));
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
                Object.DestroyImmediate(sceneObject);
                Object.DestroyImmediate(ownerAsset);
                Object.DestroyImmediate(componentAsset);
                Object.DestroyImmediate(sceneAsset);
            }
        }

        [Test]
        public void RuntimeBlueprintAssetTargetsFailOnDuplicateComponentPaths()
        {
            string ownerPath = "Assets/BlueprintSystem/Tests/Editor/Owner.blueprint.json";
            string componentPath = "Assets/BlueprintSystem/Tests/Editor/DuplicateTarget.blueprint.json";
            BlueprintCompiledAsset componentAsset = CreateCrossBlueprintTargetCompiledAsset(componentPath);
            BlueprintCompiledAsset ownerAsset = CreateOwnerCompiledAsset(ownerPath, componentAsset, componentPath, "FirstTarget", "SecondTarget");
            GameObject ownerObject = new GameObject("BlueprintAssetTargetOwner");

            try
            {
                BlueprintRunner runner = ownerObject.AddComponent<BlueprintRunner>();
                SetPrivateField(runner, "compiledBlueprint", ownerAsset);
                Assert.True(runner.Compile());

                RecordingBlueprintLogger logger = new RecordingBlueprintLogger();
                BlueprintExecutionContext context = CreateBlueprintInstanceContext(runner, logger);
                RuntimeNode validNode = CreateRuntimeNode("is_duplicate_valid", "Blueprint.IsValid");
                validNode.Properties["target"] = componentPath;

                Assert.False((bool)new BlueprintIsValidExecutor().Evaluate(context, validNode, "result"));
                Assert.True(logger.Entries.Exists(entry => entry.Contains("matched multiple Blueprint components")), string.Join("\n", logger.Entries.ToArray()));
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
                Object.DestroyImmediate(ownerAsset);
                Object.DestroyImmediate(componentAsset);
            }
        }

        [Test]
        public void EveryManifestHasDedicatedGraphToolkitNode()
        {
            foreach (BlueprintNodeManifest manifest in LoadManifests().ManifestsByTypeId.Values)
            {
                BlueprintVisualNode node = BlueprintVisualNodeFactory.Create(manifest.TypeId);
                Assert.AreNotEqual(typeof(BlueprintVisualNode), node.GetType(), manifest.TypeId);
                Assert.AreEqual(manifest.TypeId, node.ReadTypeId(), manifest.TypeId);
            }
        }

        [Test]
        public void GraphToolkitBridgeRoundTripsInventoryBlueprint()
        {
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/InventoryPanelTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/InventoryPanelTest.blueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                BlueprintGraphToolkitBridge.ImportBlueprintAtPath("Assets/BlueprintSystem/Sources/UI/InventoryPanel.blueprint.json", graphPath, false);
                BlueprintVisualGraph graph = GraphDatabase.LoadGraph<BlueprintVisualGraph>(graphPath);
                BlueprintVisualNode setTitleNode = graph.GetNodes().OfType<BlueprintVisualNode>().First(node => node.ReadNodeId() == "set_title");

                Assert.IsInstanceOf<UISetTextVisualNode>(setTitleNode);
                Assert.True(graph.GetVariables().Any(variable => variable.name == "selectedItemId"));

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);

                BlueprintSource source = LoadBlueprint(exportPath);
                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
                Assert.AreEqual(1, source.Variables.Count);
                Assert.AreEqual("selectedItemId", source.Variables[0].Name);
                Assert.True(source.Variables[0].Exposed);
                Assert.AreEqual(4, source.Nodes.Count);
                Assert.AreEqual(3, source.Edges.Count);
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitVariableNodesUseVariableTitleAndType()
        {
            BlueprintSource source = CreateVariableTestSource();
            BlueprintNodeSource getTitle = AddNode(source, "get_title", "Variable.Get");
            getTitle.Properties["name"] = "title";
            BlueprintNodeManifest getManifest;
            LoadManifests().TryGet("Variable.Get", out getManifest);

            BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(getTitle, getManifest, source.Variables);

            Assert.AreEqual("Get title", visualNode.Title);
            Assert.AreEqual("string", visualNode.Outputs.Find(port => port.Id == "value").Type);
        }

        [Test]
        public void GraphToolkitCustomEventNodesShowEventName()
        {
            BlueprintNodeSource eventNode = AddNode(new BlueprintSource(), "event_ping", "Game.Event.Custom");
            eventNode.Properties["eventName"] = "Ping";
            BlueprintNodeManifest eventManifest;
            LoadManifests().TryGet("Game.Event.Custom", out eventManifest);

            BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(eventNode, eventManifest);
            BlueprintVisualPropertyData eventProperty = visualNode.Properties.Find(property => property.Id == "eventName");
            BlueprintVisualPortData execOut = visualNode.Outputs.Find(port => port.Id == "execOut");

            Assert.AreEqual("Custom Event: Ping", visualNode.Title);
            Assert.NotNull(eventProperty);
            Assert.AreEqual("Event", eventProperty.DisplayName);
            Assert.NotNull(execOut);
            Assert.AreEqual("Ping", execOut.DisplayName);
        }

        [Test]
        public void GraphToolkitEnumInputsUseEnumTypes()
        {
            BlueprintNodeManifestCollection manifests = LoadManifests();
            BlueprintNodeManifest forceManifest;
            BlueprintNodeManifest force2DManifest;
            BlueprintNodeManifest keyManifest;
            BlueprintNodeManifest compareManifest;
            manifests.TryGet("Game.AddRigidbodyForce", out forceManifest);
            manifests.TryGet("Game.AddRigidbody2DForce", out force2DManifest);
            manifests.TryGet("Input.ListenKey", out keyManifest);
            manifests.TryGet("Variable.Compare", out compareManifest);

            BlueprintNodeSource forceNode = new BlueprintNodeSource
            {
                Id = "add_force",
                TypeId = "Game.AddRigidbodyForce"
            };
            forceNode.Properties["mode"] = "Impulse";
            BlueprintVisualNode forceVisual = BlueprintGraphToolkitBridge.CreateVisualNode(forceNode, forceManifest);
            BlueprintVisualPropertyData forceMode = forceVisual.Properties.Find(property => property.Id == "mode");
            object forceGraphValue = BlueprintVisualValueUtility.ConvertForGraphField(
                BlueprintVisualValueUtility.FromJson(forceMode.JsonValue),
                forceMode.Type);

            Assert.AreEqual("ForceMode", forceManifest.FindInput("mode").Type);
            Assert.AreEqual("ForceMode", forceMode.Type);
            Assert.AreEqual(ForceMode.Impulse, forceGraphValue);
            Assert.AreEqual("Impulse", BlueprintVisualValueUtility.ConvertFromGraphField(forceGraphValue, forceMode.Type));

            BlueprintNodeSource force2DNode = new BlueprintNodeSource
            {
                Id = "add_force_2d",
                TypeId = "Game.AddRigidbody2DForce"
            };
            force2DNode.Properties["mode"] = "Impulse";
            BlueprintVisualNode force2DVisual = BlueprintGraphToolkitBridge.CreateVisualNode(force2DNode, force2DManifest);
            BlueprintVisualPropertyData force2DMode = force2DVisual.Properties.Find(property => property.Id == "mode");
            object force2DGraphValue = BlueprintVisualValueUtility.ConvertForGraphField(
                BlueprintVisualValueUtility.FromJson(force2DMode.JsonValue),
                force2DMode.Type);

            Assert.AreEqual("ForceMode2D", force2DManifest.FindInput("mode").Type);
            Assert.AreEqual("ForceMode2D", force2DMode.Type);
            Assert.AreEqual(ForceMode2D.Impulse, force2DGraphValue);
            Assert.AreEqual("Impulse", BlueprintVisualValueUtility.ConvertFromGraphField(force2DGraphValue, force2DMode.Type));

            BlueprintNodeSource keyNode = new BlueprintNodeSource
            {
                Id = "listen_key",
                TypeId = "Input.ListenKey"
            };
            keyNode.Properties["key"] = "W";
            BlueprintVisualNode keyVisual = BlueprintGraphToolkitBridge.CreateVisualNode(keyNode, keyManifest);
            BlueprintVisualPropertyData keyProperty = keyVisual.Properties.Find(property => property.Id == "key");
            object keyGraphValue = BlueprintVisualValueUtility.ConvertForGraphField(
                BlueprintVisualValueUtility.FromJson(keyProperty.JsonValue),
                keyProperty.Type);

            Assert.AreEqual("Key", keyManifest.FindInput("key").Type);
            Assert.AreEqual("Key", keyProperty.Type);
            Assert.AreEqual(Key.W, keyGraphValue);
            Assert.AreEqual("W", BlueprintVisualValueUtility.ConvertFromGraphField(keyGraphValue, keyProperty.Type));

            BlueprintNodeSource compareNode = new BlueprintNodeSource
            {
                Id = "compare",
                TypeId = "Variable.Compare"
            };
            compareNode.Properties["comparison"] = "GreaterOrEqual";
            BlueprintVisualNode compareVisual = BlueprintGraphToolkitBridge.CreateVisualNode(compareNode, compareManifest);
            BlueprintVisualPortData comparisonInput = compareVisual.Inputs.Find(port => port.Id == "comparison");
            BlueprintVisualPropertyData comparisonProperty = compareVisual.Properties.Find(property => property.Id == "comparison");
            object comparisonGraphValue = BlueprintVisualValueUtility.ConvertForGraphField(
                BlueprintVisualValueUtility.FromJson(comparisonProperty.JsonValue),
                comparisonProperty.Type);

            Assert.AreEqual("ComparisonMode", compareManifest.FindInput("comparison").Type);
            Assert.AreEqual("ComparisonMode", comparisonInput.Type);
            Assert.AreEqual("ComparisonMode", comparisonProperty.Type);
            Assert.AreEqual(ComparisonMode.GreaterOrEqual, comparisonGraphValue);
            Assert.AreEqual("GreaterOrEqual", BlueprintVisualValueUtility.ConvertFromGraphField(comparisonGraphValue, comparisonProperty.Type));
        }

        [Test]
        public void GraphToolkitArrayTypesUseSingleBlackboardType()
        {
            System.Type stringArrayType;
            System.Type intArrayType;
            string blueprintType;
            System.Type[] graphTypes = BlueprintGraphToolkitTypeRegistry.SupportedGraphTypes;

            Assert.True(BlueprintGraphToolkitTypeRegistry.TryGetGraphType("Array<string>", out stringArrayType));
            Assert.True(BlueprintGraphToolkitTypeRegistry.TryGetGraphType("Array<int>", out intArrayType));
            Assert.AreEqual(typeof(BlueprintSystem.Editor.Array), stringArrayType);
            Assert.AreEqual(stringArrayType, intArrayType);
            Assert.True(BlueprintGraphToolkitArrayTypes.IsGraphArrayType(stringArrayType));
            Assert.AreEqual(1, graphTypes.Count(type => type == typeof(BlueprintSystem.Editor.Array)));
            Assert.False(graphTypes.Any(type =>
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(BlueprintSystem.Editor.Array<>)));
            Assert.True(BlueprintGraphToolkitTypeRegistry.TryGetBlueprintType(stringArrayType, out blueprintType));
            Assert.AreEqual("Array<string>", blueprintType);
        }

        [Test]
        public void GraphToolkitVariableSetNodeUsesVariableTitleTypeAndLabels()
        {
            BlueprintSource source = CreateVariableTestSource();
            BlueprintNodeSource setCount = AddNode(source, "set_count", "Variable.Set");
            setCount.Properties["name"] = "count";
            setCount.Properties["value"] = 5;
            BlueprintNodeManifest setManifest;
            LoadManifests().TryGet("Variable.Set", out setManifest);

            BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(setCount, setManifest, source.Variables);
            BlueprintVisualPortData valueInput = visualNode.Inputs.Find(port => port.Id == "value");
            BlueprintVisualPropertyData nameProperty = visualNode.Properties.Find(property => property.Id == "name");
            BlueprintVisualPropertyData valueProperty = visualNode.Properties.Find(property => property.Id == "value");

            Assert.AreEqual("Set count", visualNode.Title);
            Assert.AreEqual("int", valueInput.Type);
            Assert.AreEqual("New Value", valueInput.DisplayName);
            Assert.AreEqual("Variable", nameProperty.DisplayName);
            Assert.True(nameProperty.ShowInInspectorOnly);
            Assert.AreEqual("int", valueProperty.Type);
        }

        [Test]
        public void GraphToolkitCreatesVariableSetFromBlackboardVariable()
        {
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/VariableSetFromBlackboardTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/VariableSetFromBlackboardTest.blueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
                graph.BlueprintName = "VariableSetFromBlackboardTest";
                graph.Variables.Add(new BlueprintVisualVariableData
                {
                    Name = "count",
                    Type = "int",
                    HasDefaultValue = true,
                    JsonDefaultValue = BlueprintVisualValueUtility.ToJson(7)
                });
                BlueprintGraphToolkitBlackboardSync.SyncVariablesToBlackboard(graph);

                IVariable variable = graph.GetVariables().First(item => item.name == "count");
                BlueprintVisualNode setNode = BlueprintGraphToolkitUIDragDrop.CreateVariableSetNodeFromBlackboard(graph, variable, new Vector2(100, 200));
                object readValue;

                Assert.AreEqual("Set count", setNode.Title);
                Assert.AreEqual("int", setNode.Inputs.Find(port => port.Id == "value").Type);
                Assert.True(setNode.TryReadPropertyValue(setNode.Properties.Find(property => property.Id == "value"), out readValue));
                Assert.AreEqual(7, System.Convert.ToInt32(readValue));

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);
                BlueprintNodeSource exportedSet = exported.Nodes.Find(node => node.TypeId == "Variable.Set");
                BlueprintCompiledAsset compiledAsset = AssetDatabase.LoadAssetAtPath<BlueprintCompiledAsset>(BlueprintCompiledAssetCompiler.GetCompiledAssetPath(exportPath));

                Assert.NotNull(compiledAsset);
                Assert.NotNull(exportedSet);
                Assert.AreEqual("count", exportedSet.Properties["name"]);
                Assert.AreEqual(7, System.Convert.ToInt32(exportedSet.Properties["value"]));

                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(exported, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitOnEnableSyncsMetadataVariablesToBlackboard()
        {
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/OnEnableVariableBlackboardSyncTest.bpgraph";
            AssetDatabase.DeleteAsset(graphPath);

            try
            {
                BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
                graph.BlueprintName = "OnEnableVariableBlackboardSyncTest";
                graph.Variables.Add(new BlueprintVisualVariableData
                {
                    Name = "configuredName",
                    Type = "string",
                    HasDefaultValue = true,
                    JsonDefaultValue = BlueprintVisualValueUtility.ToJson("starter")
                });

                Assert.False(graph.GetVariables().Any(item => item.name == "configuredName"));

                graph.OnEnable();
                IVariable variable = graph.GetVariables().FirstOrDefault(item => item.name == "configuredName");
                string blueprintType;
                object defaultValue;

                Assert.NotNull(variable);
                Assert.AreEqual(typeof(string), variable.dataType);
                Assert.True(BlueprintGraphToolkitBlackboardSync.TryGetBlueprintType(graph, variable, out blueprintType));
                Assert.AreEqual("string", blueprintType);
                Assert.True(BlueprintGraphToolkitBlackboardSync.TryReadDefaultValue(variable, blueprintType, out defaultValue));
                Assert.AreEqual("starter", defaultValue);

                SetBlackboardDefaultValue(variable, "edited");
                Assert.False(BlueprintGraphToolkitBlackboardSync.SyncVariablesToBlackboard(graph));
                Assert.True(BlueprintGraphToolkitBlackboardSync.TryReadDefaultValue(variable, blueprintType, out defaultValue));
                Assert.AreEqual("edited", defaultValue);
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
            }
        }

        [Test]
        public void GraphToolkitAutoSyncedBlackboardVariableExportsGetAndSetNodes()
        {
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/AutoSyncedVariableDragExportTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/AutoSyncedVariableDragExportTest.blueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
                graph.BlueprintName = "AutoSyncedVariableDragExportTest";
                graph.Variables.Add(new BlueprintVisualVariableData
                {
                    Name = "displayName",
                    Type = "string",
                    HasDefaultValue = true,
                    JsonDefaultValue = BlueprintVisualValueUtility.ToJson("starter")
                });
                graph.OnEnable();

                IVariable variable = graph.GetVariables().First(item => item.name == "displayName");
                BlueprintGraphToolkitReflection.CreateBlackboardVariableNode(graph, variable, new Vector2(100, 200));
                BlueprintGraphToolkitUIDragDrop.CreateVariableSetNodeFromBlackboard(graph, variable, new Vector2(320, 200));
                BlueprintGraphToolkitReflection.MarkDirty(graph);

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);
                BlueprintNodeSource exportedGet = exported.Nodes.Find(node => node.TypeId == "Variable.Get");
                BlueprintNodeSource exportedSet = exported.Nodes.Find(node => node.TypeId == "Variable.Set");

                Assert.NotNull(exportedGet);
                Assert.NotNull(exportedSet);
                Assert.AreEqual("displayName", exportedGet.Properties["name"]);
                Assert.AreEqual("displayName", exportedSet.Properties["name"]);
                Assert.AreEqual("starter", exportedSet.Properties["value"]);

                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(exported, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitSupportsBlueprintAssetVariables()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/BlueprintAssetVariableTarget.blueprint.json";
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/BlueprintAssetVariableTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/BlueprintAssetVariableTest.export.blueprint.json";
            AssetDatabase.DeleteAsset(blueprintPath);
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                WriteTemporaryBlueprintAsset(blueprintPath, CreateVariableTestSource());

                System.Type graphType;
                Assert.Contains(BlueprintVariableTypeRegistry.BlueprintAssetTypeId, BlueprintGraphToolkitTypeRegistry.SupportedBlueprintTypes);
                Assert.True(BlueprintGraphToolkitTypeRegistry.TryGetGraphType(BlueprintVariableTypeRegistry.BlueprintAssetTypeId, out graphType));
                Assert.AreEqual(typeof(BlueprintSystem.Editor.Blueprint), graphType);
                Assert.True(BlueprintTypeUtility.IsValueAssignableToType(blueprintPath, BlueprintVariableTypeRegistry.BlueprintAssetTypeId));
                Assert.False(BlueprintVariableTypeRegistry.GetSupportedBlueprintTypes().Contains(BlueprintVariableTypeRegistry.BlueprintRefTypeId));

                BlueprintSource source = new BlueprintSource();
                source.SchemaVersion = "0.1";
                source.Name = "BlueprintAssetVariableTest";
                source.Variables.Add(new BlueprintVariableDeclaration
                {
                    Name = "targetBlueprint",
                    Type = BlueprintVariableTypeRegistry.BlueprintAssetTypeId,
                    DefaultValue = blueprintPath
                });

                BlueprintDiagnosticList sourceDiagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.False(sourceDiagnostics.HasErrors, sourceDiagnostics.ToDisplayString());

                BlueprintSource runtimeRefVariableSource = new BlueprintSource();
                runtimeRefVariableSource.SchemaVersion = "0.1";
                runtimeRefVariableSource.Name = "BlueprintRefVariableRejected";
                runtimeRefVariableSource.Variables.Add(new BlueprintVariableDeclaration
                {
                    Name = "runtimeTarget",
                    Type = BlueprintVariableTypeRegistry.BlueprintRefTypeId
                });
                BlueprintDiagnosticList runtimeRefDiagnostics = new BlueprintValidator().Validate(runtimeRefVariableSource, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.True(runtimeRefDiagnostics.Exists(diagnostic => diagnostic.Code == "BP025"), runtimeRefDiagnostics.ToDisplayString());

                BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
                graph.BlueprintName = "BlueprintAssetVariableTest";
                graph.Variables.Add(new BlueprintVisualVariableData
                {
                    Name = "targetBlueprint",
                    Type = BlueprintVariableTypeRegistry.BlueprintAssetTypeId,
                    HasDefaultValue = true,
                    JsonDefaultValue = BlueprintVisualValueUtility.ToJson(blueprintPath)
                });
                BlueprintGraphToolkitBlackboardSync.SyncVariablesToBlackboard(graph);

                IVariable variable = graph.GetVariables().First(item => item.name == "targetBlueprint");
                object defaultValue;
                Assert.AreEqual(typeof(BlueprintSystem.Editor.Blueprint), variable.dataType);
                Assert.True(BlueprintGraphToolkitBlackboardSync.TryReadDefaultValue(variable, BlueprintVariableTypeRegistry.BlueprintAssetTypeId, out defaultValue));
                Assert.AreEqual(blueprintPath, defaultValue);

                BlueprintGraphToolkitUIDragDrop.CreateVariableSetNodeFromBlackboard(graph, variable, new Vector2(100, 200));
                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);
                BlueprintVariableDeclaration exportedVariable = exported.Variables.Find(item => item.Name == "targetBlueprint");
                BlueprintNodeSource exportedSet = exported.Nodes.Find(node => node.TypeId == "Variable.Set");

                Assert.NotNull(exportedVariable);
                Assert.AreEqual(BlueprintVariableTypeRegistry.BlueprintAssetTypeId, exportedVariable.Type);
                Assert.AreEqual(blueprintPath, exportedVariable.DefaultValue);
                Assert.NotNull(exportedSet);
                Assert.AreEqual(blueprintPath, exportedSet.Properties["value"]);
            }
            finally
            {
                AssetDatabase.DeleteAsset(blueprintPath);
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitCreatesBlueprintVariableFromDraggedBlueprintJson()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/DraggedBlueprintTarget.blueprint.json";
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/DraggedBlueprintVariableTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/DraggedBlueprintVariableTest.export.blueprint.json";
            AssetDatabase.DeleteAsset(blueprintPath);
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                TextAsset blueprintAsset = WriteTemporaryBlueprintAsset(blueprintPath, CreateVariableTestSource());
                BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
                graph.BlueprintName = "DraggedBlueprintVariableTest";

                List<string> resolvedPaths = BlueprintGraphToolkitUIDragDrop.ResolveBlueprintAssetPaths(new Object[] { blueprintAsset });
                Assert.AreEqual(1, resolvedPaths.Count);
                Assert.AreEqual(blueprintPath, resolvedPaths[0]);

                IVariable variable = BlueprintGraphToolkitUIDragDrop.EnsureBlueprintAssetVariable(graph, "DraggedBlueprintTarget", blueprintPath);
                BlueprintGraphToolkitReflection.CreateBlackboardVariableNode(graph, variable, new Vector2(100, 200));
                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);
                BlueprintVariableDeclaration exportedVariable = exported.Variables.Find(item => item.Name == "DraggedBlueprintTarget");
                BlueprintNodeSource exportedGet = exported.Nodes.Find(node => node.TypeId == "Variable.Get");

                Assert.NotNull(exportedVariable);
                Assert.AreEqual(BlueprintVariableTypeRegistry.BlueprintAssetTypeId, exportedVariable.Type);
                Assert.AreEqual(blueprintPath, exportedVariable.DefaultValue);
                Assert.NotNull(exportedGet);
                Assert.AreEqual("DraggedBlueprintTarget", exportedGet.Properties["name"]);
            }
            finally
            {
                AssetDatabase.DeleteAsset(blueprintPath);
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitImportsArrayVariableGetAsBlackboardVariableNode()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/ArrayVariableGetImportTest.blueprint.json";
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/ArrayVariableGetImportTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/ArrayVariableGetImportTest.export.blueprint.json";
            AssetDatabase.DeleteAsset(blueprintPath);
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                BlueprintSource source = new BlueprintSource();
                source.SchemaVersion = "0.1";
                source.Name = "ArrayVariableGetImportTest";
                source.Variables.Add(new BlueprintVariableDeclaration
                {
                    Name = "items",
                    Type = "Array<string>",
                    DefaultValue = new List<object> { "A", "B" }
                });
                BlueprintNodeSource getItems = AddNode(source, "get_items", "Variable.Get");
                getItems.Properties["name"] = "items";

                File.WriteAllText(blueprintPath, source.ToJson());
                AssetDatabase.ImportAsset(blueprintPath);

                BlueprintGraphToolkitBridge.ImportBlueprintAtPath(blueprintPath, graphPath, false);
                BlueprintVisualGraph graph = GraphDatabase.LoadGraph<BlueprintVisualGraph>(graphPath);
                IVariable variable = graph.GetVariables().FirstOrDefault(item => item.name == "items");
                string blueprintType;
                object defaultValue;

                Assert.NotNull(variable);
                Assert.AreEqual(typeof(BlueprintSystem.Editor.Array), variable.dataType);
                Assert.True(BlueprintGraphToolkitBlackboardSync.TryGetBlueprintType(graph, variable, out blueprintType));
                Assert.AreEqual("Array<string>", blueprintType);
                Assert.True(BlueprintGraphToolkitBlackboardSync.TryReadDefaultValue(variable, blueprintType, out defaultValue));
                IList defaultItems = (IList)defaultValue;
                Assert.AreEqual(2, defaultItems.Count);
                Assert.AreEqual("A", defaultItems[0]);
                Assert.True(graph.GetNodes().Any(node => node is IVariableNode && ((IVariableNode)node).variable.name == "items"));
                Assert.False(graph.GetNodes().OfType<BlueprintVisualNode>().Any(node => node.ReadNodeId() == "get_items"));

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);
                BlueprintVariableDeclaration exportedVariable = exported.Variables.Find(item => item.Name == "items");
                IList exportedDefault = (IList)exportedVariable.DefaultValue;

                Assert.NotNull(exportedVariable);
                Assert.AreEqual("Array<string>", exportedVariable.Type);
                Assert.AreEqual(2, exportedDefault.Count);
                Assert.AreEqual("A", exportedDefault[0]);
                Assert.True(exported.Nodes.Exists(node => node.TypeId == "Variable.Get" && (string)node.Properties["name"] == "items"));
                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(exported, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(blueprintPath);
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitCreatesVariableSetFromArrayBlackboardVariable()
        {
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/ArrayVariableSetFromBlackboardTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/ArrayVariableSetFromBlackboardTest.blueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
                graph.BlueprintName = "ArrayVariableSetFromBlackboardTest";
                graph.Variables.Add(new BlueprintVisualVariableData
                {
                    Name = "items",
                    Type = "Array<string>",
                    HasDefaultValue = true,
                    JsonDefaultValue = BlueprintVisualValueUtility.ToJson(new List<object> { "A", "B" })
                });
                BlueprintGraphToolkitBlackboardSync.SyncVariablesToBlackboard(graph);

                IVariable variable = graph.GetVariables().First(item => item.name == "items");
                Assert.AreEqual(typeof(BlueprintSystem.Editor.Array), variable.dataType);
                BlueprintVisualNode setNode = BlueprintGraphToolkitUIDragDrop.CreateVariableSetNodeFromBlackboard(graph, variable, new Vector2(100, 200));
                BlueprintVisualPortData valueInput = setNode.Inputs.Find(port => port.Id == "value");
                BlueprintVisualPropertyData valueProperty = setNode.Properties.Find(property => property.Id == "value");
                object readValue;

                Assert.AreEqual("Set items", setNode.Title);
                Assert.AreEqual("Array<string>", valueInput.Type);
                Assert.AreEqual("Array<string>", valueProperty.Type);
                Assert.True(setNode.TryReadPropertyValue(valueProperty, out readValue));
                IList items = (IList)readValue;
                Assert.AreEqual(2, items.Count);
                Assert.AreEqual("A", items[0]);

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);
                BlueprintNodeSource exportedSet = exported.Nodes.Find(node => node.TypeId == "Variable.Set");
                IList exportedValue = (IList)exportedSet.Properties["value"];

                Assert.NotNull(exportedSet);
                Assert.AreEqual("items", exportedSet.Properties["name"]);
                Assert.AreEqual(2, exportedValue.Count);
                Assert.AreEqual("A", exportedValue[0]);
                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(exported, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitExportsEditedArrayBlackboardDefaultJson()
        {
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/ArrayBlackboardDefaultEditTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/ArrayBlackboardDefaultEditTest.blueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
                graph.BlueprintName = "ArrayBlackboardDefaultEditTest";
                graph.Variables.Add(new BlueprintVisualVariableData
                {
                    Name = "items",
                    Type = "Array<string>",
                    HasDefaultValue = true,
                    JsonDefaultValue = BlueprintVisualValueUtility.ToJson(new List<object> { "A", "B" })
                });
                BlueprintGraphToolkitBlackboardSync.SyncVariablesToBlackboard(graph);

                IVariable variable = graph.GetVariables().First(item => item.name == "items");
                SetBlackboardDefaultValue(variable, new BlueprintSystem.Editor.Array("string", "[\"C\"]"));
                BlueprintGraphToolkitReflection.MarkDirty(graph);
                GraphDatabase.SaveGraphIfDirty(graph);
                AssetDatabase.SaveAssets();

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);
                BlueprintVariableDeclaration exportedVariable = exported.Variables.Find(item => item.Name == "items");
                IList exportedDefault = (IList)exportedVariable.DefaultValue;

                Assert.NotNull(exportedVariable);
                Assert.AreEqual("Array<string>", exportedVariable.Type);
                Assert.AreEqual(1, exportedDefault.Count);
                Assert.AreEqual("C", exportedDefault[0]);
                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(exported, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitExportsEditedArrayBlackboardElementType()
        {
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/ArrayBlackboardElementTypeEditTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/ArrayBlackboardElementTypeEditTest.blueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
                graph.BlueprintName = "ArrayBlackboardElementTypeEditTest";
                graph.Variables.Add(new BlueprintVisualVariableData
                {
                    Name = "items",
                    Type = "Array<string>",
                    HasDefaultValue = true,
                    JsonDefaultValue = BlueprintVisualValueUtility.ToJson(new List<object> { "A", "B" })
                });
                BlueprintGraphToolkitBlackboardSync.SyncVariablesToBlackboard(graph);

                IVariable variable = graph.GetVariables().First(item => item.name == "items");
                SetBlackboardDefaultValue(variable, new BlueprintSystem.Editor.Array("int", "[3]"));
                BlueprintGraphToolkitReflection.MarkDirty(graph);
                GraphDatabase.SaveGraphIfDirty(graph);
                AssetDatabase.SaveAssets();

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);
                BlueprintVariableDeclaration exportedVariable = exported.Variables.Find(item => item.Name == "items");
                IList exportedDefault = (IList)exportedVariable.DefaultValue;

                Assert.NotNull(exportedVariable);
                Assert.AreEqual("Array<int>", exportedVariable.Type);
                Assert.AreEqual(1, exportedDefault.Count);
                Assert.AreEqual(3, System.Convert.ToInt32(exportedDefault[0]));
                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(exported, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitImportsVariableGetAsBlackboardVariableNode()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/VariableGetImportTest.blueprint.json";
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/VariableGetImportTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/VariableGetImportTest.export.blueprint.json";
            AssetDatabase.DeleteAsset(blueprintPath);
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                BlueprintSource source = CreateVariableTestSource();
                BlueprintNodeSource getTitle = AddNode(source, "get_title", "Variable.Get");
                getTitle.Properties["name"] = "title";
                BlueprintNodeSource setTitle = AddNode(source, "set_title", "UI.SetText");
                setTitle.Properties["target"] = "TitleText";
                source.Bindings.Add(new BlueprintBindingDeclaration
                {
                    Name = "TitleText",
                    Type = "TMP_Text",
                    Required = true
                });
                source.Edges.Add(new BlueprintEdgeSource
                {
                    From = "get_title.value",
                    To = "set_title.value"
                });

                File.WriteAllText(blueprintPath, source.ToJson());
                AssetDatabase.ImportAsset(blueprintPath);

                BlueprintGraphToolkitBridge.ImportBlueprintAtPath(blueprintPath, graphPath, false);
                BlueprintVisualGraph graph = GraphDatabase.LoadGraph<BlueprintVisualGraph>(graphPath);

                Assert.True(graph.GetNodes().Any(node => node is IVariableNode && ((IVariableNode)node).variable.name == "title"));
                Assert.False(graph.GetNodes().OfType<BlueprintVisualNode>().Any(node => node.ReadNodeId() == "get_title"));

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);

                Assert.True(exported.Nodes.Exists(node => node.TypeId == "Variable.Get" && (string)node.Properties["name"] == "title"));
                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(exported, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(blueprintPath);
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void GraphToolkitExportsSpriteBindingNode()
        {
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/SpriteBindingExportTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/SpriteBindingExportTest.blueprint.json";
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
                graph.BlueprintName = "SpriteBindingExportTest";
                graph.Bindings.Add(new BlueprintVisualBindingData
                {
                    Name = "SwordSprite",
                    Type = "Sprite",
                    Required = true
                });

                BlueprintNodeManifest manifest;
                Assert.True(LoadManifests().TryGet("UI.SpriteBinding", out manifest));
                BlueprintNodeSource sourceNode = AddNode(new BlueprintSource(), "sprite_sword", "UI.SpriteBinding");
                sourceNode.Properties["sprite"] = "SwordSprite";
                BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(sourceNode, manifest);

                BlueprintGraphToolkitReflection.CreateNode(graph, visualNode, new Vector2(100, 200));
                BlueprintGraphToolkitReflection.MarkDirty(graph);
                GraphDatabase.SaveGraphIfDirty(graph);
                AssetDatabase.SaveAssets();

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);

                Assert.True(exported.Bindings.Exists(binding => binding.Name == "SwordSprite" && binding.Type == "Sprite"));
                Assert.True(exported.Nodes.Exists(node => node.Id == "sprite_sword" && node.TypeId == "UI.SpriteBinding"));

                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(exported, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void VariableTypesSupportVector4AndRect()
        {
            Assert.True(BlueprintTypeUtility.IsValueAssignableToType(new List<object> { 1, 2, 3, 4 }, "Vector4"));
            Assert.True(BlueprintTypeUtility.IsValueAssignableToType(new List<object> { 1, 2, 3, 4 }, "Rect"));
            Assert.AreEqual(typeof(Vector4), BlueprintVisualValueUtility.ToGraphType("Vector4"));
            Assert.AreEqual(typeof(Rect), BlueprintVisualValueUtility.ToGraphType("Rect"));
        }

        [Test]
        public void ValidatorAcceptsRegisteredStructuredVariableDefaults()
        {
            BlueprintSource source = CreateStructuredVariableTestSource();

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
        }

        [Test]
        public void ValidatorReportsStructuredVariableProblems()
        {
            BlueprintSource unknownType = CreateStructuredVariableTestSource();
            unknownType.Variables[0].Type = "Missing.Struct";

            BlueprintSource unknownField = CreateStructuredVariableTestSource();
            ((Dictionary<string, object>)unknownField.Variables[0].DefaultValue)["missing"] = 1;

            BlueprintSource wrongFieldType = CreateStructuredVariableTestSource();
            ((Dictionary<string, object>)wrongFieldType.Variables[0].DefaultValue)["count"] = "not an int";

            BlueprintDiagnosticList unknownTypeDiagnostics = new BlueprintValidator().Validate(unknownType, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            BlueprintDiagnosticList unknownFieldDiagnostics = new BlueprintValidator().Validate(unknownField, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            BlueprintDiagnosticList wrongFieldTypeDiagnostics = new BlueprintValidator().Validate(wrongFieldType, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.True(unknownTypeDiagnostics.Exists(diagnostic => diagnostic.Code == "BP025"), unknownTypeDiagnostics.ToDisplayString());
            Assert.True(unknownFieldDiagnostics.Exists(diagnostic => diagnostic.Code == "BP024"), unknownFieldDiagnostics.ToDisplayString());
            Assert.True(wrongFieldTypeDiagnostics.Exists(diagnostic => diagnostic.Code == "BP024"), wrongFieldTypeDiagnostics.ToDisplayString());
        }

        [Test]
        public void ValidatorAcceptsUserDefinedStructVariableDefaults()
        {
            WriteUserStructDefinition();

            try
            {
                BlueprintSource source = CreateUserStructVariableTestSource();

                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                DictionaryBlueprintVariableStore store = new DictionaryBlueprintVariableStore(compileResult.Blueprint);
                object selectedItem = store.Get("selectedItem");
                object count;

                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
                Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());
                Assert.IsInstanceOf<BlueprintStructValue>(selectedItem);
                Assert.True(BlueprintFieldUtility.TryGetValue(selectedItem, "count", out count));
                Assert.AreEqual(1, System.Convert.ToInt32(count));
            }
            finally
            {
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void VariableSetFieldWritesUserDefinedStructCopies()
        {
            WriteUserStructDefinition();

            try
            {
                object selectedItem;
                Assert.True(BlueprintStructuredValueUtility.TryConvertToRuntimeValue(
                    CreateUserStructDefaultValue("sword_01", 1),
                    "Struct.TestInventoryItem",
                    out selectedItem));

                BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), new TestBindingResolver(), new RecordingBlueprintLogger(), null);
                RuntimeNode setFieldNode = CreateRuntimeNode("set_count", "Variable.SetField");
                setFieldNode.Properties["target"] = selectedItem;
                setFieldNode.Properties["path"] = "count";
                setFieldNode.Properties["value"] = 5;

                object result = new VariableSetFieldExecutor().Evaluate(context, setFieldNode, "result");
                object originalCount;
                object updatedCount;

                Assert.IsInstanceOf<BlueprintStructValue>(result);
                Assert.True(BlueprintFieldUtility.TryGetValue(selectedItem, "count", out originalCount));
                Assert.True(BlueprintFieldUtility.TryGetValue(result, "count", out updatedCount));
                Assert.AreEqual(1, System.Convert.ToInt32(originalCount));
                Assert.AreEqual(5, System.Convert.ToInt32(updatedCount));
            }
            finally
            {
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void VariableBreakStructReadsUserDefinedStructFields()
        {
            WriteUserStructDefinition();

            try
            {
                object selectedItem;
                Assert.True(BlueprintStructuredValueUtility.TryConvertToRuntimeValue(
                    CreateUserStructDefaultValue("sword_01", 1),
                    "Struct.TestInventoryItem",
                    out selectedItem));

                BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), new TestBindingResolver(), new RecordingBlueprintLogger(), null);
                RuntimeNode breakNode = CreateRuntimeNode("break_item", "Variable.BreakStruct");
                breakNode.Properties["structTypeId"] = "Struct.TestInventoryItem";
                breakNode.Properties["target"] = selectedItem;

                VariableBreakStructExecutor executor = new VariableBreakStructExecutor();

                Assert.AreEqual("sword_01", executor.Evaluate(context, breakNode, "fld_item_id"));
                Assert.AreEqual(1, System.Convert.ToInt32(executor.Evaluate(context, breakNode, "fld_count")));
            }
            finally
            {
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void ValidatorAcceptsBreakStructDynamicOutputsAndReportsTypeMismatch()
        {
            WriteUserStructDefinition();

            try
            {
                BlueprintSource valid = CreateBreakStructValidationSource("int");
                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(valid, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(valid, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
                Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

                BlueprintSource mismatch = CreateBreakStructValidationSource("string");
                BlueprintDiagnosticList mismatchDiagnostics = new BlueprintValidator().Validate(mismatch, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

                Assert.True(mismatchDiagnostics.Exists(diagnostic => diagnostic.Code == "BP003"), mismatchDiagnostics.ToDisplayString());
            }
            finally
            {
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void ValidatorAllowsUnusedBreakStructNodeWithoutTarget()
        {
            WriteUserStructDefinition();

            try
            {
                BlueprintSource previewOnly = CreateUserStructVariableTestSource();
                previewOnly.Name = "BreakStructPreviewOnly";
                BlueprintNodeSource previewBreak = AddNode(previewOnly, "break_item", "Variable.BreakStruct");
                previewBreak.Properties["structTypeId"] = "Struct.TestInventoryItem";

                BlueprintDiagnosticList previewDiagnostics = new BlueprintValidator().Validate(previewOnly, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                BlueprintCompileResult previewCompile = new BlueprintCompiler().Compile(previewOnly, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

                Assert.False(previewDiagnostics.HasErrors, previewDiagnostics.ToDisplayString());
                Assert.True(previewCompile.Success, previewCompile.Diagnostics.ToDisplayString());

                BlueprintSource usedMissingTarget = CreateBreakStructValidationSource("int");
                usedMissingTarget.Edges.RemoveAll(edge => edge.To == "break_item.target");

                BlueprintDiagnosticList usedDiagnostics = new BlueprintValidator().Validate(usedMissingTarget, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

                Assert.True(usedDiagnostics.Exists(diagnostic =>
                    diagnostic.Code == "BP002" &&
                    diagnostic.NodeId == "break_item" &&
                    diagnostic.PortId == "target"), usedDiagnostics.ToDisplayString());
            }
            finally
            {
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void GraphToolkitCreatesBreakStructOutputsFromDefinition()
        {
            WriteUserStructDefinition();

            try
            {
                BlueprintNodeManifest manifest;
                Assert.True(LoadManifests().TryGet("Variable.BreakStruct", out manifest));

                BlueprintNodeSource sourceNode = new BlueprintNodeSource
                {
                    Id = "break_item",
                    TypeId = "Variable.BreakStruct"
                };
                sourceNode.Properties["structTypeId"] = "Struct.TestInventoryItem";

                BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(sourceNode, manifest);
                BlueprintVisualPortData target = visualNode.Inputs.Find(port => port.Id == "target");
                BlueprintVisualPortData count = visualNode.Outputs.Find(port => port.Id == "fld_count");

                Assert.AreEqual("Break Struct.TestInventoryItem", visualNode.Title);
                Assert.NotNull(target);
                Assert.IsNull(target.Type);
                Assert.AreEqual(3, visualNode.Outputs.Count);
                Assert.NotNull(count);
                Assert.AreEqual("count", count.DisplayName);
                Assert.AreEqual("int", count.Type);
            }
            finally
            {
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void GraphToolkitArrayLoopElementCanConnectToBreakStructTarget()
        {
            WriteUserStructDefinition();

            try
            {
                BlueprintNodeManifest loopManifest;
                BlueprintNodeManifest loopWithBreakManifest;
                BlueprintNodeManifest breakManifest;
                BlueprintNodeManifestCollection manifests = LoadManifests();
                Assert.True(manifests.TryGet("Array.ForEachLoop", out loopManifest));
                Assert.True(manifests.TryGet("Array.ForEachLoopWithBreak", out loopWithBreakManifest));
                Assert.True(manifests.TryGet("Variable.BreakStruct", out breakManifest));

                BlueprintVisualNode loopNode = BlueprintGraphToolkitBridge.CreateVisualNode(
                    new BlueprintNodeSource { Id = "loop_items", TypeId = "Array.ForEachLoop" },
                    loopManifest);
                BlueprintVisualNode loopWithBreakNode = BlueprintGraphToolkitBridge.CreateVisualNode(
                    new BlueprintNodeSource { Id = "loop_items_with_break", TypeId = "Array.ForEachLoopWithBreak" },
                    loopWithBreakManifest);
                BlueprintNodeSource breakSource = new BlueprintNodeSource
                {
                    Id = "break_item",
                    TypeId = "Variable.BreakStruct"
                };
                breakSource.Properties["structTypeId"] = "Struct.TestInventoryItem";
                BlueprintVisualNode breakNode = BlueprintGraphToolkitBridge.CreateVisualNode(breakSource, breakManifest);

                BlueprintVisualPortData loopElement = loopNode.Outputs.Find(port => port.Id == "arrayElement");
                BlueprintVisualPortData loopWithBreakElement = loopWithBreakNode.Outputs.Find(port => port.Id == "arrayElement");
                BlueprintVisualPortData breakTarget = breakNode.Inputs.Find(port => port.Id == "target");

                Assert.NotNull(loopElement);
                Assert.NotNull(loopWithBreakElement);
                Assert.NotNull(breakTarget);
                Assert.IsNull(loopElement.Type);
                Assert.IsNull(loopWithBreakElement.Type);
                Assert.IsNull(breakTarget.Type);
                Assert.AreEqual(typeof(object), BlueprintVisualValueUtility.ToGraphType(loopElement.Type));
                Assert.AreEqual(typeof(object), BlueprintVisualValueUtility.ToGraphType(loopWithBreakElement.Type));
                Assert.AreEqual(typeof(object), BlueprintVisualValueUtility.ToGraphType(breakTarget.Type));
            }
            finally
            {
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void UserStructAssetDragCreatesBreakStructNodeSource()
        {
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/BreakStructDragTest.bpgraph";
            string assetPath = "Assets/BlueprintSystem/Specs/Structs/DragBreakStruct.asset";
            AssetDatabase.DeleteAsset(graphPath);
            AssetDatabase.DeleteAsset(assetPath);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
                BlueprintUserStructAsset asset = ScriptableObject.CreateInstance<BlueprintUserStructAsset>();
                asset.Fields.Add(new BlueprintUserStructAssetField
                {
                    id = "fld_power",
                    name = "power",
                    fieldType = BlueprintUserStructAssetFieldType.Float,
                    defaultValueJson = "1"
                });
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.ImportAsset(assetPath);

                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                BlueprintNodeSource sourceNode = BlueprintGraphToolkitUIDragDrop.CreateBreakStructNodeSource(
                    graph,
                    asset.TypeId,
                    guid,
                    new Vector2(10, 20));

                Assert.AreEqual("Variable.BreakStruct", sourceNode.TypeId);
                Assert.AreEqual("Struct.DragBreakStruct", sourceNode.Properties["structTypeId"]);
                Assert.AreEqual(guid, sourceNode.Properties["structAssetGuid"]);
                Assert.AreEqual(10f, sourceNode.X);
                Assert.AreEqual(20f, sourceNode.Y);
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                AssetDatabase.DeleteAsset(assetPath);
                BlueprintUserStructRegistry.Refresh();
            }
        }

        [Test]
        public void RegistryLoadsUserStructScriptableObjectAssets()
        {
            string assetPath = "Assets/BlueprintSystem/Specs/Structs/TestSpellStruct.asset";
            string renamedAssetPath = "Assets/BlueprintSystem/Specs/Structs/RenamedSpellStruct.asset";
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.DeleteAsset(renamedAssetPath);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                BlueprintUserStructAsset asset = ScriptableObject.CreateInstance<BlueprintUserStructAsset>();
                asset.Fields.Add(new BlueprintUserStructAssetField
                {
                    id = "fld_power",
                    name = "power",
                    fieldType = BlueprintUserStructAssetFieldType.Float,
                    defaultValueJson = "10"
                });
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.ImportAsset(assetPath);
                BlueprintUserStructRegistry.Refresh();

                BlueprintUserStructDefinition definition;
                object runtimeValue;
                object power;

                Assert.AreEqual("Struct.TestSpellStruct", asset.TypeId);
                Assert.True(BlueprintUserStructRegistry.TryGet("Struct.TestSpellStruct", out definition));
                Assert.AreEqual("Struct.TestSpellStruct", definition.DisplayName);
                Assert.True(BlueprintStructuredValueUtility.TryConvertToRuntimeValue(null, "Struct.TestSpellStruct", out runtimeValue));
                Assert.True(BlueprintFieldUtility.TryGetValue(runtimeValue, "power", out power));
                Assert.AreEqual(10f, System.Convert.ToSingle(power));

                string renameError = AssetDatabase.RenameAsset(assetPath, "RenamedSpellStruct");
                Assert.True(string.IsNullOrEmpty(renameError), renameError);
                assetPath = renamedAssetPath;
                AssetDatabase.ImportAsset(assetPath);
                BlueprintUserStructRegistry.Refresh();

                Assert.AreEqual("Struct.RenamedSpellStruct", asset.TypeId);
                Assert.True(BlueprintUserStructRegistry.TryGet("Struct.RenamedSpellStruct", out definition));
                Assert.AreEqual("Struct.RenamedSpellStruct", definition.DisplayName);
                Assert.True(BlueprintStructuredValueUtility.TryConvertToRuntimeValue(null, "Struct.RenamedSpellStruct", out runtimeValue));
                Assert.True(BlueprintFieldUtility.TryGetValue(runtimeValue, "power", out power));
                Assert.AreEqual(10f, System.Convert.ToSingle(power));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.DeleteAsset(renamedAssetPath);
                BlueprintUserStructRegistry.Refresh();
            }
        }

        [Test]
        public void DataTableRegistryLoadsJsonAndScriptableObjectAssets()
        {
            WriteUserStructDefinition();
            string tableJsonPath = "Assets/BlueprintSystem/Specs/Tables/TestItems.bpdatatable.json";
            string tableAssetPath = "Assets/BlueprintSystem/Specs/Tables/LiveItems.asset";
            AssetDatabase.DeleteAsset(tableJsonPath);
            AssetDatabase.DeleteAsset(tableAssetPath);

            try
            {
                WriteDataTableDefinition(tableJsonPath);
                BlueprintDataTableRegistry.Refresh();

                BlueprintDataTableDefinition jsonDefinition;
                Assert.True(BlueprintDataTableRegistry.TryGetByPath(tableJsonPath, out jsonDefinition));
                Assert.AreEqual("Table.TestItems", jsonDefinition.TableId);
                Assert.AreEqual("Struct.TestInventoryItem", jsonDefinition.RowStructTypeId);
                Assert.AreEqual(2, jsonDefinition.Rows.Count);

                BlueprintDataTableAsset asset = ScriptableObject.CreateInstance<BlueprintDataTableAsset>();
                asset.RowStructTypeId = "Struct.TestInventoryItem";
                asset.Rows.Add(new BlueprintDataTableAssetRow
                {
                    rowName = "wand_01",
                    valueJson = "{\"itemId\":\"wand_01\",\"count\":4,\"position\":[5,6]}"
                });
                Directory.CreateDirectory(Path.GetDirectoryName(tableAssetPath));
                AssetDatabase.CreateAsset(asset, tableAssetPath);
                AssetDatabase.ImportAsset(tableAssetPath);
                BlueprintDataTableRegistry.Refresh();

                BlueprintDataTableDefinition assetDefinition;
                Assert.True(BlueprintDataTableRegistry.TryGetByPath(tableAssetPath, out assetDefinition));
                Assert.AreEqual("Table.LiveItems", assetDefinition.TableId);
                Assert.AreEqual(1, assetDefinition.Rows.Count);
                Assert.AreEqual("wand_01", assetDefinition.Rows[0].RowName);
            }
            finally
            {
                AssetDatabase.DeleteAsset(tableJsonPath);
                AssetDatabase.DeleteAsset(tableJsonPath + ".meta");
                AssetDatabase.DeleteAsset(tableAssetPath);
                BlueprintDataTableRegistry.Refresh();
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void DataTableAssetValidationReportsInvalidRows()
        {
            WriteUserStructDefinition();

            try
            {
                BlueprintDataTableAsset missingStruct = ScriptableObject.CreateInstance<BlueprintDataTableAsset>();
                missingStruct.RowStructTypeId = "Struct.Missing";
                missingStruct.Rows.Add(new BlueprintDataTableAssetRow { rowName = "item", valueJson = "{}" });

                List<string> missingStructErrors = BlueprintDataTableAssetEditor.Validate(missingStruct);
                Assert.True(missingStructErrors.Exists(error => error.Contains("Unknown row struct type")), string.Join("\n", missingStructErrors.ToArray()));

                BlueprintDataTableAsset invalidRows = ScriptableObject.CreateInstance<BlueprintDataTableAsset>();
                invalidRows.RowStructTypeId = "Struct.TestInventoryItem";
                invalidRows.Rows.Add(new BlueprintDataTableAssetRow { rowName = "item", valueJson = "{\"itemId\":\"item\",\"count\":1,\"position\":[1,2]}" });
                invalidRows.Rows.Add(new BlueprintDataTableAssetRow { rowName = "item", valueJson = "not-json" });
                invalidRows.Rows.Add(new BlueprintDataTableAssetRow { rowName = "bad_count", valueJson = "{\"itemId\":\"bad_count\",\"count\":\"many\",\"position\":[1,2]}" });

                List<string> invalidRowErrors = BlueprintDataTableAssetEditor.Validate(invalidRows);
                Assert.True(invalidRowErrors.Exists(error => error.Contains("duplicates rowName")), string.Join("\n", invalidRowErrors.ToArray()));
                Assert.True(invalidRowErrors.Exists(error => error.Contains("must be valid JSON")), string.Join("\n", invalidRowErrors.ToArray()));
                Assert.True(invalidRowErrors.Exists(error => error.Contains("does not match type")), string.Join("\n", invalidRowErrors.ToArray()));
            }
            finally
            {
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void DataTableExecutorsReadRowsNamesAndAllRows()
        {
            WriteUserStructDefinition();
            string tablePath = "Assets/BlueprintSystem/Specs/Tables/TestItems.bpdatatable.json";

            try
            {
                WriteDataTableDefinition(tablePath);
                BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), new TestBindingResolver(), new RecordingBlueprintLogger(), null);
                RuntimeNode getRow = CreateDataTableRuntimeNode("get_row", "DataTable.GetRow", tablePath);
                getRow.Properties["rowName"] = "shield_01";

                object row = new DataTableGetRowExecutor().Evaluate(context, getRow, "row");
                object count;

                Assert.True((bool)new DataTableGetRowExecutor().Evaluate(context, getRow, "found"));
                Assert.IsInstanceOf<BlueprintStructValue>(row);
                Assert.True(BlueprintFieldUtility.TryGetValue(row, "count", out count));
                Assert.AreEqual(2, System.Convert.ToInt32(count));

                getRow.Properties["rowName"] = "missing";
                object missingRow = new DataTableGetRowExecutor().Evaluate(context, getRow, "row");
                object defaultCount;

                Assert.False((bool)new DataTableGetRowExecutor().Evaluate(context, getRow, "found"));
                Assert.True(BlueprintFieldUtility.TryGetValue(missingRow, "count", out defaultCount));
                Assert.AreEqual(1, System.Convert.ToInt32(defaultCount));

                RuntimeNode rowNames = CreateDataTableRuntimeNode("row_names", "DataTable.GetRowNames", tablePath);
                IList names = (IList)new DataTableGetRowNamesExecutor().Evaluate(context, rowNames, "rowNames");

                Assert.AreEqual(2, names.Count);
                Assert.AreEqual("sword_01", names[0]);
                Assert.AreEqual("shield_01", names[1]);

                RuntimeNode allRows = CreateDataTableRuntimeNode("all_rows", "DataTable.GetAllRows", tablePath);
                IList rows = (IList)new DataTableGetAllRowsExecutor().Evaluate(context, allRows, "rows");

                Assert.AreEqual(2, rows.Count);
                Assert.IsInstanceOf<BlueprintStructValue>(rows[0]);
            }
            finally
            {
                AssetDatabase.DeleteAsset(tablePath);
                AssetDatabase.DeleteAsset(tablePath + ".meta");
                BlueprintDataTableRegistry.Refresh();
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void ValidatorTypesDataTableDynamicOutputs()
        {
            WriteUserStructDefinition();
            string tablePath = "Assets/BlueprintSystem/Specs/Tables/TestItems.bpdatatable.json";

            try
            {
                WriteDataTableDefinition(tablePath);
                BlueprintSource valid = CreateDataTableValidationSource(tablePath, "Struct.TestInventoryItem");
                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(valid, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
                BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(valid, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
                Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

                BlueprintSource mismatch = CreateDataTableValidationSource(tablePath, "string");
                BlueprintDiagnosticList mismatchDiagnostics = new BlueprintValidator().Validate(mismatch, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

                Assert.True(mismatchDiagnostics.Exists(diagnostic => diagnostic.Code == "BP003"), mismatchDiagnostics.ToDisplayString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(tablePath);
                AssetDatabase.DeleteAsset(tablePath + ".meta");
                BlueprintDataTableRegistry.Refresh();
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void DataTableAssetDragCreatesTypedNodeSource()
        {
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/DataTableDragTest.bpgraph";
            string assetPath = "Assets/BlueprintSystem/Specs/Tables/DragItems.asset";
            AssetDatabase.DeleteAsset(graphPath);
            AssetDatabase.DeleteAsset(assetPath);
            WriteUserStructDefinition();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                BlueprintVisualGraph graph = GraphDatabase.CreateGraph<BlueprintVisualGraph>(graphPath);
                BlueprintDataTableAsset asset = ScriptableObject.CreateInstance<BlueprintDataTableAsset>();
                asset.RowStructTypeId = "Struct.TestInventoryItem";
                asset.Rows.Add(new BlueprintDataTableAssetRow
                {
                    rowName = "sword_01",
                    valueJson = "{\"itemId\":\"sword_01\",\"count\":1,\"position\":[1,2]}"
                });
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.ImportAsset(assetPath);

                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                string tablePath = BlueprintDataTableRegistry.GetJsonPathForAssetPath(assetPath);
                BlueprintNodeSource sourceNode = BlueprintGraphToolkitUIDragDrop.CreateDataTableNodeSource(
                    graph,
                    "DataTable.GetRow",
                    tablePath,
                    guid,
                    asset.RowStructTypeId,
                    new Vector2(10, 20));

                BlueprintNodeManifest manifest;
                Assert.True(LoadManifests().TryGet("DataTable.GetRow", out manifest));
                BlueprintVisualNode visualNode = BlueprintGraphToolkitBridge.CreateVisualNode(sourceNode, manifest);
                BlueprintVisualPortData row = visualNode.Outputs.Find(port => port.Id == "row");

                Assert.AreEqual("DataTable.GetRow", sourceNode.TypeId);
                Assert.AreEqual(tablePath, sourceNode.Properties["tablePath"]);
                Assert.AreEqual(guid, sourceNode.Properties["tableAssetGuid"]);
                Assert.AreEqual("Struct.TestInventoryItem", sourceNode.Properties["rowStructTypeId"]);
                Assert.NotNull(row);
                Assert.AreEqual("Struct.TestInventoryItem", row.Type);
            }
            finally
            {
                AssetDatabase.DeleteAsset(graphPath);
                AssetDatabase.DeleteAsset(assetPath);
                BlueprintDataTableRegistry.Refresh();
                DeleteUserStructDefinition();
            }
        }

        [Test]
        public void VariableStoreCoercesStructuredDefaultsAndOverrides()
        {
            RuntimeBlueprint blueprint = new RuntimeBlueprint();
            blueprint.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "selectedItem",
                Type = "Test.InventoryItem",
                DefaultValue = CreateStructuredDefaultValue("sword_01", 1, TestInventoryItemRarity.Rare),
                Exposed = true
            });

            DictionaryBlueprintVariableStore store = new DictionaryBlueprintVariableStore(blueprint, new[]
            {
                new BlueprintVariableOverride
                {
                    Name = "selectedItem",
                    Type = "Test.InventoryItem",
                    JsonValue = "{\"id\":\"shield_01\",\"count\":2,\"rarity\":\"Common\",\"position\":[3,4]}"
                }
            });

            TestInventoryItemData item = (TestInventoryItemData)store.Get("selectedItem");

            Assert.AreEqual("shield_01", item.id);
            Assert.AreEqual(2, item.count);
            Assert.AreEqual(TestInventoryItemRarity.Common, item.rarity);
            Assert.AreEqual(new Vector2(3, 4), item.position);
        }

        [Test]
        public void SourceMapperWritesStructuredDefaultObjects()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "StructuredSource";
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "selectedItem",
                Type = "Test.InventoryItem",
                DefaultValue = new TestInventoryItemData
                {
                    id = "potion_01",
                    count = 3,
                    rarity = TestInventoryItemRarity.Common,
                    position = new Vector2(5, 6)
                }
            });

            BlueprintSource roundTripped = BlueprintSource.FromJson(source.ToJson());
            Dictionary<string, object> defaultValue = (Dictionary<string, object>)roundTripped.Variables[0].DefaultValue;

            Assert.AreEqual("potion_01", defaultValue["id"]);
            Assert.AreEqual(3, System.Convert.ToInt32(defaultValue["count"]));
            Assert.AreEqual("Common", defaultValue["rarity"]);
            Assert.AreEqual(2, ((List<object>)defaultValue["position"]).Count);
        }

        [Test]
        public void GraphToolkitRoundTripsStructuredBlackboardVariables()
        {
            string blueprintPath = "Assets/BlueprintSystem/Tests/Editor/StructuredVariableTest.blueprint.json";
            string graphPath = "Assets/BlueprintSystem/Tests/Editor/StructuredVariableTest.bpgraph";
            string exportPath = "Assets/BlueprintSystem/Tests/Editor/StructuredVariableTest.export.blueprint.json";
            AssetDatabase.DeleteAsset(blueprintPath);
            AssetDatabase.DeleteAsset(graphPath);
            DeleteTemporaryCompiledArtifacts(exportPath);

            try
            {
                BlueprintSource source = CreateStructuredVariableTestSource();
                File.WriteAllText(blueprintPath, source.ToJson());
                AssetDatabase.ImportAsset(blueprintPath);

                BlueprintGraphToolkitBridge.ImportBlueprintAtPath(blueprintPath, graphPath, false);
                BlueprintVisualGraph graph = GraphDatabase.LoadGraph<BlueprintVisualGraph>(graphPath);
                IVariable variable = graph.GetVariables().First(item => item.name == "selectedItem");
                TestInventoryItemData defaultValue;

                Assert.AreEqual(typeof(TestInventoryItemData), variable.dataType);
                Assert.True(variable.TryGetDefaultValue(out defaultValue));
                Assert.AreEqual("sword_01", defaultValue.id);
                Assert.AreEqual(1, defaultValue.count);

                BlueprintGraphToolkitBridge.ExportGraphAtPath(graphPath, exportPath);
                BlueprintSource exported = LoadBlueprint(exportPath);
                BlueprintVariableDeclaration exportedVariable = exported.Variables.Find(item => item.Name == "selectedItem");
                Assert.NotNull(exportedVariable);
                Dictionary<string, object> exportedDefault = (Dictionary<string, object>)exportedVariable.DefaultValue;
                BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(exported, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

                Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
                Assert.AreEqual("Test.InventoryItem", exportedVariable.Type);
                Assert.AreEqual("sword_01", exportedDefault["id"]);
                Assert.AreEqual(1, System.Convert.ToInt32(exportedDefault["count"]));
            }
            finally
            {
                AssetDatabase.DeleteAsset(blueprintPath);
                AssetDatabase.DeleteAsset(graphPath);
                DeleteTemporaryCompiledArtifacts(exportPath);
            }
        }

        [Test]
        public void ArrayVariablesSupportStructuredElements()
        {
            RuntimeBlueprint blueprint = new RuntimeBlueprint();
            blueprint.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "items",
                Type = "Array<Test.InventoryItem>",
                DefaultValue = new List<object>
                {
                    CreateStructuredDefaultValue("sword_01", 1, TestInventoryItemRarity.Rare),
                    CreateStructuredDefaultValue("potion_01", 3, TestInventoryItemRarity.Common)
                },
                Exposed = true
            });

            DictionaryBlueprintVariableStore store = new DictionaryBlueprintVariableStore(blueprint, new[]
            {
                new BlueprintVariableOverride
                {
                    Name = "items",
                    Type = "Array<Test.InventoryItem>",
                    JsonValue = "[{\"id\":\"shield_01\",\"count\":2,\"rarity\":\"Common\",\"position\":[3,4]}]"
                }
            });

            IList items = (IList)store.Get("items");
            TestInventoryItemData item = (TestInventoryItemData)items[0];

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("shield_01", item.id);
            Assert.AreEqual(2, item.count);
            Assert.AreEqual(TestInventoryItemRarity.Common, item.rarity);
        }

        [Test]
        public void ArrayAndFieldExecutorsReadStructuredItems()
        {
            List<object> items = new List<object>
            {
                CreateStructuredDefaultValue("sword_01", 1, TestInventoryItemRarity.Rare),
                CreateStructuredDefaultValue("potion_01", 3, TestInventoryItemRarity.Common)
            };
            BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), new TestBindingResolver(), new RecordingBlueprintLogger(), null);

            RuntimeNode countNode = CreateRuntimeNode("array_count", "Array.Count");
            countNode.Properties["array"] = items;
            RuntimeNode getNode = CreateRuntimeNode("array_get", "Array.Get");
            getNode.Properties["array"] = items;
            getNode.Properties["index"] = 1;
            RuntimeNode fieldNode = CreateRuntimeNode("get_count", "Variable.GetField");
            fieldNode.Properties["target"] = new ArrayGetExecutor().Evaluate(context, getNode, "item");
            fieldNode.Properties["path"] = "count";

            Assert.AreEqual(2, new ArrayCountExecutor().Evaluate(context, countNode, "count"));
            Assert.AreEqual(3, System.Convert.ToInt32(new VariableGetFieldExecutor().Evaluate(context, fieldNode, "value")));
        }

        [Test]
        public void ArrayForEachLoopExecutesEachItemAndCompletes()
        {
            BlueprintSource source = CreateArrayLoopSource("Array.ForEachLoop", false, new List<object> { "A", "B", "C" });
            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            DictionaryBlueprintVariableStore variables;
            RecordingBlueprintLogger logger = ExecuteBlueprintStartEvent(compileResult.Blueprint, out variables);

            Assert.Contains("Log: A", logger.Entries);
            Assert.Contains("Log: B", logger.Entries);
            Assert.Contains("Log: C", logger.Entries);
            Assert.AreEqual(2, System.Convert.ToInt32(variables.Get("lastIndex")));
            Assert.Greater(FindLogIndex(logger, "done"), FindLogIndex(logger, "C"));
        }

        [Test]
        public void ArrayForEachLoopWithBreakStopsEarlyAndCompletes()
        {
            BlueprintSource source = CreateArrayLoopSource("Array.ForEachLoopWithBreak", true, new List<object> { "A", "B", "C" });
            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            DictionaryBlueprintVariableStore variables;
            RecordingBlueprintLogger logger = ExecuteBlueprintStartEvent(compileResult.Blueprint, out variables);

            Assert.Contains("Log: A", logger.Entries);
            Assert.False(logger.Entries.Contains("Log: B"), string.Join("\n", logger.Entries.ToArray()));
            Assert.False(logger.Entries.Contains("Log: C"), string.Join("\n", logger.Entries.ToArray()));
            Assert.AreEqual(0, System.Convert.ToInt32(variables.Get("lastIndex")));
            Assert.Greater(FindLogIndex(logger, "done"), FindLogIndex(logger, "A"));
        }

        [Test]
        public void ArrayForEachLoopCompletesEmptyOrInvalidArrays()
        {
            BlueprintSource emptySource = CreateArrayLoopSource("Array.ForEachLoop", false, new List<object>());
            BlueprintCompileResult emptyCompile = new BlueprintCompiler().Compile(emptySource, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(emptyCompile.Success, emptyCompile.Diagnostics.ToDisplayString());

            RecordingBlueprintLogger emptyLogger = ExecuteBlueprintStartEvent(emptyCompile.Blueprint);
            Assert.False(emptyLogger.Entries.Contains("Log: A"), string.Join("\n", emptyLogger.Entries.ToArray()));
            Assert.Contains("Log: done", emptyLogger.Entries);

            RuntimeNode invalidLoop = CreateRuntimeNode("for_each", "Array.ForEachLoop");
            invalidLoop.Properties["array"] = "not-json";
            List<string> outputs = new List<string>();
            BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), new TestBindingResolver(), new RecordingBlueprintLogger(), (node, output) => outputs.Add(output));

            BlueprintExecResult result = new ArrayForEachLoopExecutor().Execute(context, invalidLoop);

            Assert.AreEqual("completed", result.NextExecPortId);
            Assert.AreEqual(0, outputs.Count);
        }

        [Test]
        public void ArrayQueryExecutorsHandleBoundsAndBasicMatches()
        {
            List<object> items = new List<object> { "A", 2, 3f };
            BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), new TestBindingResolver(), new RecordingBlueprintLogger(), null);

            RuntimeNode validIndex = CreateRuntimeNode("valid_index", "Array.IsValidIndex");
            validIndex.Properties["array"] = items;
            validIndex.Properties["index"] = 2;
            RuntimeNode invalidIndex = CreateRuntimeNode("invalid_index", "Array.IsValidIndex");
            invalidIndex.Properties["array"] = items;
            invalidIndex.Properties["index"] = 3;
            RuntimeNode contains = CreateRuntimeNode("contains", "Array.Contains");
            contains.Properties["array"] = items;
            contains.Properties["item"] = 2.0f;
            RuntimeNode indexOf = CreateRuntimeNode("index_of", "Array.IndexOf");
            indexOf.Properties["array"] = items;
            indexOf.Properties["item"] = "missing";
            RuntimeNode first = CreateRuntimeNode("first", "Array.First");
            first.Properties["array"] = items;
            RuntimeNode last = CreateRuntimeNode("last", "Array.Last");
            last.Properties["array"] = items;
            RuntimeNode emptyFirst = CreateRuntimeNode("empty_first", "Array.First");
            emptyFirst.Properties["array"] = new List<object>();

            Assert.True((bool)new ArrayIsValidIndexExecutor().Evaluate(context, validIndex, "result"));
            Assert.False((bool)new ArrayIsValidIndexExecutor().Evaluate(context, invalidIndex, "result"));
            Assert.True((bool)new ArrayContainsExecutor().Evaluate(context, contains, "result"));
            Assert.AreEqual(-1, new ArrayIndexOfExecutor().Evaluate(context, indexOf, "index"));
            Assert.False((bool)new ArrayIndexOfExecutor().Evaluate(context, indexOf, "found"));
            Assert.AreEqual("A", new ArrayFirstExecutor().Evaluate(context, first, "item"));
            Assert.True((bool)new ArrayFirstExecutor().Evaluate(context, first, "isValid"));
            Assert.AreEqual(3f, new ArrayLastExecutor().Evaluate(context, last, "item"));
            Assert.False((bool)new ArrayFirstExecutor().Evaluate(context, emptyFirst, "isValid"));
            Assert.IsNull(new ArrayFirstExecutor().Evaluate(context, emptyFirst, "item"));
        }

        [Test]
        public void ValidatorAcceptsNewUiComponentNodes()
        {
            BlueprintSource source = CreateVariableTestSource();
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "items",
                Type = "Array<string>",
                DefaultValue = new List<object> { "A", "B" }
            });
            source.Bindings.Add(new BlueprintBindingDeclaration { Name = "InventoryList", Type = "BlueprintLoopScrollView", Required = true });
            source.Bindings.Add(new BlueprintBindingDeclaration { Name = "ActionButton", Type = "Button", Required = true });
            source.Bindings.Add(new BlueprintBindingDeclaration { Name = "EnabledToggle", Type = "Toggle", Required = true });

            BlueprintNodeSource refresh = AddNode(source, "refresh_list", "UI.RefreshLoopScrollView");
            refresh.Properties["target"] = "InventoryList";
            refresh.Properties["itemsVariable"] = "items";
            BlueprintNodeSource button = AddNode(source, "bind_button_events", "UI.BindButtonEvents");
            button.Properties["target"] = "ActionButton";
            BlueprintNodeSource toggle = AddNode(source, "bind_toggle", "UI.BindToggleChanged");
            toggle.Properties["target"] = "EnabledToggle";

            BlueprintDiagnosticList diagnostics = new BlueprintValidator().Validate(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());

            Assert.False(diagnostics.HasErrors, diagnostics.ToDisplayString());
        }

        [Test]
        public void UiComponentExecutorsRegisterRuntimeListenersAndRefreshList()
        {
            GameObject root = new GameObject("Root");
            GameObject buttonObject = new GameObject("ActionButton");
            GameObject toggleObject = new GameObject("EnabledToggle");
            GameObject scrollObject = new GameObject("InventoryList");
            GameObject viewportObject = new GameObject("Viewport");
            GameObject contentObject = new GameObject("Content");
            GameObject itemTemplate = new GameObject("Item");

            try
            {
                buttonObject.transform.SetParent(root.transform);
                toggleObject.transform.SetParent(root.transform);
                scrollObject.transform.SetParent(root.transform);
                viewportObject.transform.SetParent(scrollObject.transform);
                contentObject.transform.SetParent(viewportObject.transform);
                itemTemplate.transform.SetParent(contentObject.transform);

                Button button = buttonObject.AddComponent<Button>();
                Toggle toggle = toggleObject.AddComponent<Toggle>();
                ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
                RectTransform viewport = viewportObject.AddComponent<RectTransform>();
                RectTransform content = contentObject.AddComponent<RectTransform>();
                itemTemplate.AddComponent<RectTransform>();
                BlueprintLoopScrollView loopScrollView = scrollObject.AddComponent<BlueprintLoopScrollView>();
                scrollRect.viewport = viewport;
                scrollRect.content = content;
                SetPrivateField(loopScrollView, "scrollRect", scrollRect);
                SetPrivateField(loopScrollView, "content", content);
                SetPrivateField(loopScrollView, "itemTemplate", itemTemplate);

                TestBindingResolver resolver = new TestBindingResolver();
                resolver.Add("ActionButton", button);
                resolver.Add("EnabledToggle", toggle);
                resolver.Add("InventoryList", loopScrollView);

                List<string> outputs = new List<string>();
                BlueprintExecutionContext context = CreateTestContext(new RuntimeBlueprint(), resolver, new RecordingBlueprintLogger(), (node, output) => outputs.Add(output));
                RuntimeNode buttonNode = CreateRuntimeNode("bind_button", "UI.BindButtonEvents");
                buttonNode.Properties["target"] = "ActionButton";
                RuntimeNode toggleNode = CreateRuntimeNode("bind_toggle", "UI.BindToggleChanged");
                toggleNode.Properties["target"] = "EnabledToggle";
                RuntimeNode refreshNode = CreateRuntimeNode("refresh_list", "UI.RefreshLoopScrollView");
                refreshNode.Properties["target"] = "InventoryList";
                refreshNode.Properties["items"] = new List<object> { "A", "B", "C" };

                Assert.IsFalse(toggle.isOn);
                new UIBindButtonEventsExecutor().Execute(context, buttonNode);
                new UIBindToggleChangedExecutor().Execute(context, toggleNode);
                new UIRefreshLoopScrollViewExecutor().Execute(context, refreshNode);
                toggle.isOn = true;

                Assert.NotNull(buttonObject.GetComponent<BlueprintButtonGestureListener>());
                Assert.NotNull(toggleObject.GetComponent<BlueprintToggleListener>());
                Assert.Greater(loopScrollView.PoolCount, 0);
                Assert.Contains("changed", outputs);
                Assert.Contains("turnedOn", outputs);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RuntimeBlueprint CompileInventoryBlueprint()
        {
            BlueprintSource source = LoadBlueprint("Assets/BlueprintSystem/Sources/UI/InventoryPanel.blueprint.json");
            return CompileSource(source);
        }

        private static RuntimeBlueprint CompileSource(BlueprintSource source)
        {
            BlueprintCompileResult result = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(result.Success, result.Diagnostics.ToDisplayString());
            return result.Blueprint;
        }

        private static BlueprintCompiledAsset CreateVariableOnlyCompiledAsset(
            string blueprintName,
            string sourcePath,
            params BlueprintCompiledVariable[] variables)
        {
            BlueprintCompiledAsset compiledAsset = ScriptableObject.CreateInstance<BlueprintCompiledAsset>();
            SetVariableOnlyCompiledData(compiledAsset, blueprintName, sourcePath, variables);
            return compiledAsset;
        }

        private static void SetVariableOnlyCompiledData(
            BlueprintCompiledAsset compiledAsset,
            string blueprintName,
            string sourcePath,
            params BlueprintCompiledVariable[] variables)
        {
            compiledAsset.SetCompiledData(
                "0.1",
                blueprintName,
                null,
                sourcePath,
                blueprintName + "-source",
                blueprintName + "-manifest",
                variables,
                new BlueprintCompiledBinding[0],
                new BlueprintCompiledComponent[0],
                new BlueprintCompiledNode[0],
                new BlueprintCompiledEdge[0],
                new BlueprintCompiledEdge[0],
                new BlueprintCompiledEventEntry[0]);
        }

        private static BlueprintCompiledAsset CreateSetBoolCompiledAsset(
            string blueprintName,
            string sourcePath,
            string variableName,
            string eventName)
        {
            BlueprintCompiledAsset compiledAsset = ScriptableObject.CreateInstance<BlueprintCompiledAsset>();
            compiledAsset.SetCompiledData(
                "0.1",
                blueprintName,
                null,
                sourcePath,
                blueprintName + "-source",
                blueprintName + "-manifest",
                new[]
                {
                    new BlueprintCompiledVariable
                    {
                        Id = variableName + "-id",
                        Name = variableName,
                        Type = "bool",
                        DefaultValueJson = BlueprintJson.Serialize(false, false)
                    }
                },
                new BlueprintCompiledBinding[0],
                new BlueprintCompiledComponent[0],
                new[]
                {
                    new BlueprintCompiledNode
                    {
                        Id = "event_start",
                        TypeId = "Game.Event.Custom",
                        ExecutorId = "Flow.Event",
                        Properties = new List<BlueprintCompiledProperty>
                        {
                            new BlueprintCompiledProperty { Id = "eventName", JsonValue = BlueprintJson.Serialize(eventName, false) }
                        }
                    },
                    new BlueprintCompiledNode
                    {
                        Id = "set_flag",
                        TypeId = "Variable.Set",
                        ExecutorId = "Variable.Set",
                        Properties = new List<BlueprintCompiledProperty>
                        {
                            new BlueprintCompiledProperty { Id = "name", JsonValue = BlueprintJson.Serialize(variableName, false) },
                            new BlueprintCompiledProperty { Id = "value", JsonValue = BlueprintJson.Serialize(true, false) }
                        }
                    }
                },
                new[]
                {
                    new BlueprintCompiledEdge { FromNodeId = "event_start", FromPortId = "execOut", ToNodeId = "set_flag", ToPortId = "execIn" }
                },
                new BlueprintCompiledEdge[0],
                new[]
                {
                    new BlueprintCompiledEventEntry { EventName = eventName, NodeId = "event_start" }
                });
            return compiledAsset;
        }

        private static BlueprintCompiledAsset CreateDelayedSetBoolCompiledAsset(
            string blueprintName,
            string sourcePath,
            string variableName,
            float delaySeconds)
        {
            BlueprintCompiledAsset compiledAsset = ScriptableObject.CreateInstance<BlueprintCompiledAsset>();
            compiledAsset.SetCompiledData(
                "0.1",
                blueprintName,
                null,
                sourcePath,
                blueprintName + "-source",
                blueprintName + "-manifest",
                new[]
                {
                    new BlueprintCompiledVariable
                    {
                        Id = variableName + "-id",
                        Name = variableName,
                        Type = "bool",
                        DefaultValueJson = BlueprintJson.Serialize(false, false)
                    }
                },
                new BlueprintCompiledBinding[0],
                new BlueprintCompiledComponent[0],
                new[]
                {
                    new BlueprintCompiledNode
                    {
                        Id = "event_start",
                        TypeId = "Game.Event.OnStart",
                        ExecutorId = "Flow.Event"
                    },
                    new BlueprintCompiledNode
                    {
                        Id = "delay",
                        TypeId = "Flow.Delay",
                        ExecutorId = "Flow.Delay",
                        Properties = new List<BlueprintCompiledProperty>
                        {
                            new BlueprintCompiledProperty { Id = "seconds", JsonValue = BlueprintJson.Serialize(delaySeconds, false) }
                        }
                    },
                    new BlueprintCompiledNode
                    {
                        Id = "set_flag",
                        TypeId = "Variable.Set",
                        ExecutorId = "Variable.Set",
                        Properties = new List<BlueprintCompiledProperty>
                        {
                            new BlueprintCompiledProperty { Id = "name", JsonValue = BlueprintJson.Serialize(variableName, false) },
                            new BlueprintCompiledProperty { Id = "value", JsonValue = BlueprintJson.Serialize(true, false) }
                        }
                    }
                },
                new[]
                {
                    new BlueprintCompiledEdge { FromNodeId = "event_start", FromPortId = "execOut", ToNodeId = "delay", ToPortId = "execIn" },
                    new BlueprintCompiledEdge { FromNodeId = "delay", FromPortId = "execOut", ToNodeId = "set_flag", ToPortId = "execIn" }
                },
                new BlueprintCompiledEdge[0],
                new[]
                {
                    new BlueprintCompiledEventEntry { EventName = "OnStart", NodeId = "event_start" }
                });
            return compiledAsset;
        }

        private static void SetInvalidCompiledData(BlueprintCompiledAsset compiledAsset, string blueprintName, string sourcePath)
        {
            compiledAsset.SetCompiledData(
                "0.1",
                blueprintName,
                null,
                sourcePath,
                blueprintName + "-invalid-source",
                blueprintName + "-invalid-manifest",
                new[]
                {
                    new BlueprintCompiledVariable
                    {
                        Id = "fired-id",
                        Name = "fired",
                        Type = "bool",
                        DefaultValueJson = BlueprintJson.Serialize(false, false)
                    }
                },
                new BlueprintCompiledBinding[0],
                new BlueprintCompiledComponent[0],
                new[]
                {
                    new BlueprintCompiledNode
                    {
                        Id = "bad_node",
                        TypeId = "Test.MissingExecutor",
                        ExecutorId = "Test.MissingExecutor"
                    }
                },
                new BlueprintCompiledEdge[0],
                new BlueprintCompiledEdge[0],
                new[]
                {
                    new BlueprintCompiledEventEntry { EventName = "OnStart", NodeId = "bad_node" }
                });
        }

        private static BlueprintCompiledAsset CreateCrossBlueprintTargetCompiledAsset(string sourcePath)
        {
            BlueprintCompiledAsset componentAsset = ScriptableObject.CreateInstance<BlueprintCompiledAsset>();
            componentAsset.SetCompiledData(
                "0.1",
                "CrossBlueprintTarget",
                null,
                sourcePath,
                "component-source",
                "component-manifest",
                new[]
                {
                    new BlueprintCompiledVariable
                    {
                        Name = "publicCount",
                        Type = "int",
                        DefaultValueJson = BlueprintJson.Serialize(3, false),
                        Exposed = true
                    },
                    new BlueprintCompiledVariable
                    {
                        Name = "hiddenCount",
                        Type = "int",
                        DefaultValueJson = BlueprintJson.Serialize(2, false),
                        Exposed = false
                    },
                    new BlueprintCompiledVariable
                    {
                        Name = "fired",
                        Type = "bool",
                        DefaultValueJson = BlueprintJson.Serialize(false, false),
                        Exposed = true
                    }
                },
                new BlueprintCompiledBinding[0],
                new BlueprintCompiledComponent[0],
                new[]
                {
                    new BlueprintCompiledNode
                    {
                        Id = "event_ping",
                        TypeId = "Game.Event.Custom",
                        ExecutorId = "Flow.Event",
                        Properties = new List<BlueprintCompiledProperty>
                        {
                            new BlueprintCompiledProperty { Id = "eventName", JsonValue = BlueprintJson.Serialize("Ping", false) }
                        }
                    },
                    new BlueprintCompiledNode
                    {
                        Id = "set_fired",
                        TypeId = "Variable.Set",
                        ExecutorId = "Variable.Set",
                        Properties = new List<BlueprintCompiledProperty>
                        {
                            new BlueprintCompiledProperty { Id = "name", JsonValue = BlueprintJson.Serialize("fired", false) },
                            new BlueprintCompiledProperty { Id = "value", JsonValue = BlueprintJson.Serialize(true, false) }
                        }
                    }
                },
                new[]
                {
                    new BlueprintCompiledEdge { FromNodeId = "event_ping", FromPortId = "execOut", ToNodeId = "set_fired", ToPortId = "execIn" }
                },
                new BlueprintCompiledEdge[0],
                new[]
                {
                    new BlueprintCompiledEventEntry { EventName = "Ping", NodeId = "event_ping" }
                });
            return componentAsset;
        }

        private static BlueprintCompiledAsset CreateOwnerCompiledAsset(
            string sourcePath,
            BlueprintCompiledAsset componentAsset,
            string componentPath,
            params string[] componentNames)
        {
            List<BlueprintCompiledComponent> components = new List<BlueprintCompiledComponent>();
            for (int i = 0; i < componentNames.Length; i++)
            {
                components.Add(new BlueprintCompiledComponent
                {
                    Name = componentNames[i],
                    BlueprintPath = componentPath,
                    Required = true,
                    CompiledBlueprint = componentAsset
                });
            }

            BlueprintCompiledAsset ownerAsset = ScriptableObject.CreateInstance<BlueprintCompiledAsset>();
            ownerAsset.SetCompiledData(
                "0.1",
                "ComponentOwner",
                null,
                sourcePath,
                "owner-source",
                "owner-manifest",
                new BlueprintCompiledVariable[0],
                new BlueprintCompiledBinding[0],
                components,
                new BlueprintCompiledNode[0],
                new BlueprintCompiledEdge[0],
                new BlueprintCompiledEdge[0],
                new BlueprintCompiledEventEntry[0]);
            return ownerAsset;
        }

        private static BlueprintCompiledAsset CreateOwnerAccessCompiledAsset(
            string sourcePath,
            BlueprintCompiledAsset componentAsset,
            string componentPath,
            params string[] componentNames)
        {
            List<BlueprintCompiledComponent> components = new List<BlueprintCompiledComponent>();
            for (int i = 0; i < componentNames.Length; i++)
            {
                components.Add(new BlueprintCompiledComponent
                {
                    Name = componentNames[i],
                    BlueprintPath = componentPath,
                    Required = true,
                    CompiledBlueprint = componentAsset
                });
            }

            BlueprintCompiledAsset ownerAsset = ScriptableObject.CreateInstance<BlueprintCompiledAsset>();
            ownerAsset.SetCompiledData(
                "0.1",
                "ComponentOwner",
                null,
                sourcePath,
                "owner-source",
                "owner-manifest",
                new[]
                {
                    new BlueprintCompiledVariable
                    {
                        Name = "ownerCount",
                        Type = "int",
                        DefaultValueJson = BlueprintJson.Serialize(0, false),
                        Exposed = true
                    },
                    new BlueprintCompiledVariable
                    {
                        Name = "ownerFired",
                        Type = "bool",
                        DefaultValueJson = BlueprintJson.Serialize(false, false),
                        Exposed = true
                    }
                },
                new BlueprintCompiledBinding[0],
                components,
                new[]
                {
                    new BlueprintCompiledNode
                    {
                        Id = "event_ping_owner",
                        TypeId = "Game.Event.Custom",
                        ExecutorId = "Flow.Event",
                        Properties = new List<BlueprintCompiledProperty>
                        {
                            new BlueprintCompiledProperty { Id = "eventName", JsonValue = BlueprintJson.Serialize("PingOwner", false) }
                        }
                    },
                    new BlueprintCompiledNode
                    {
                        Id = "set_owner_fired",
                        TypeId = "Variable.Set",
                        ExecutorId = "Variable.Set",
                        Properties = new List<BlueprintCompiledProperty>
                        {
                            new BlueprintCompiledProperty { Id = "name", JsonValue = BlueprintJson.Serialize("ownerFired", false) },
                            new BlueprintCompiledProperty { Id = "value", JsonValue = BlueprintJson.Serialize(true, false) }
                        }
                    }
                },
                new[]
                {
                    new BlueprintCompiledEdge { FromNodeId = "event_ping_owner", FromPortId = "execOut", ToNodeId = "set_owner_fired", ToPortId = "execIn" }
                },
                new BlueprintCompiledEdge[0],
                new[]
                {
                    new BlueprintCompiledEventEntry { EventName = "PingOwner", NodeId = "event_ping_owner" }
                });
            return ownerAsset;
        }

        private static BlueprintExecutionContext CreateBlueprintInstanceContext(BlueprintRunner runner, RecordingBlueprintLogger logger)
        {
            return new BlueprintExecutionContext(
                runner == null ? new RuntimeBlueprint() : runner.RuntimeBlueprint,
                runner == null ? null : runner.gameObject,
                runner,
                new NullBlueprintBindingResolver(),
                new DictionaryBlueprintVariableStore(runner == null ? null : runner.RuntimeBlueprint),
                null,
                logger,
                null,
                runner,
                null);
        }

        private static BlueprintExecutionContext CreateBlueprintComponentContext(IBlueprintInstance instance, RecordingBlueprintLogger logger)
        {
            return new BlueprintExecutionContext(
                instance == null ? new RuntimeBlueprint() : instance.RuntimeBlueprint,
                instance == null ? null : instance.Owner,
                instance == null ? null : instance.OwnerComponent,
                new NullBlueprintBindingResolver(),
                new DictionaryBlueprintVariableStore(instance == null ? null : instance.RuntimeBlueprint),
                null,
                logger,
                null,
                instance,
                instance == null ? null : instance.OwnerInstance);
        }

        private static BlueprintSource CreateBlueprintAssetTargetConnectionSource(string componentPath)
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "BlueprintAssetTargetConnectionTest";
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "targetBlueprint",
                Type = BlueprintVariableTypeRegistry.BlueprintAssetTypeId,
                DefaultValue = componentPath
            });

            BlueprintNodeSource getTarget = AddNode(source, "get_target_blueprint", "Variable.Get");
            getTarget.Properties["name"] = "targetBlueprint";
            BlueprintNodeSource readTarget = AddNode(source, "read_target_count", "Blueprint.GetVariable");
            readTarget.Properties["name"] = "publicCount";
            source.Edges.Add(new BlueprintEdgeSource
            {
                From = "get_target_blueprint.value",
                To = "read_target_count.target"
            });

            return source;
        }

        private static BlueprintSource CreateVariableTestSource()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "VariableTest";
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "count",
                Type = "int",
                DefaultValue = 0
            });
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "title",
                Type = "string",
                DefaultValue = "Title"
            });
            return source;
        }

        private static BlueprintSource CreateCrossBlueprintTargetSource()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "CrossBlueprintTarget";
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "publicCount",
                Type = "int",
                DefaultValue = 3,
                Exposed = true
            });
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "hiddenCount",
                Type = "int",
                DefaultValue = 2,
                Exposed = false
            });
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "fired",
                Type = "bool",
                DefaultValue = false,
                Exposed = true
            });

            BlueprintNodeSource ping = AddNode(source, "event_ping", "Game.Event.Custom");
            ping.Properties["eventName"] = "Ping";
            BlueprintNodeSource setFired = AddNode(source, "set_fired", "Variable.Set");
            setFired.Properties["name"] = "fired";
            setFired.Properties["value"] = true;
            source.Edges.Add(new BlueprintEdgeSource
            {
                From = "event_ping.execOut",
                To = "set_fired.execIn"
            });

            return source;
        }

        private static BlueprintSource CreateArrayLoopSource(string loopTypeId, bool connectBreak, List<object> items)
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "ArrayLoopTest";
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "items",
                Type = "Array<string>",
                DefaultValue = items
            });
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "lastIndex",
                Type = "int",
                DefaultValue = -1
            });

            AddNode(source, "event_start", "Game.Event.OnStart");
            BlueprintNodeSource getItems = AddNode(source, "get_items", "Variable.Get");
            getItems.Properties["name"] = "items";
            AddNode(source, "for_each", loopTypeId);
            BlueprintNodeSource logItem = AddNode(source, "log_item", "Game.Log");
            BlueprintNodeSource setIndex = AddNode(source, "set_index", "Variable.Set");
            setIndex.Properties["name"] = "lastIndex";
            BlueprintNodeSource logDone = AddNode(source, "log_done", "Game.Log");
            logDone.Properties["message"] = "done";

            source.Edges.Add(new BlueprintEdgeSource { From = "event_start.execOut", To = "for_each.execIn" });
            source.Edges.Add(new BlueprintEdgeSource { From = "get_items.value", To = "for_each.array" });
            source.Edges.Add(new BlueprintEdgeSource { From = "for_each.loopBody", To = "log_item.execIn" });
            source.Edges.Add(new BlueprintEdgeSource { From = "for_each.arrayElement", To = "log_item.message" });
            source.Edges.Add(new BlueprintEdgeSource { From = "for_each.loopBody", To = "set_index.execIn" });
            source.Edges.Add(new BlueprintEdgeSource { From = "for_each.arrayIndex", To = "set_index.value" });
            if (connectBreak)
            {
                source.Edges.Add(new BlueprintEdgeSource { From = "for_each.loopBody", To = "for_each.break" });
            }

            source.Edges.Add(new BlueprintEdgeSource { From = "for_each.completed", To = "log_done.execIn" });
            return source;
        }

        private static RecordingBlueprintLogger ExecuteBlueprintStartEvent(RuntimeBlueprint blueprint)
        {
            DictionaryBlueprintVariableStore ignored;
            return ExecuteBlueprintStartEvent(blueprint, out ignored);
        }

        private static RecordingBlueprintLogger ExecuteBlueprintStartEvent(RuntimeBlueprint blueprint, out DictionaryBlueprintVariableStore variables)
        {
            RecordingBlueprintLogger logger = new RecordingBlueprintLogger();
            BlueprintVM vm = new BlueprintVM();
            BlueprintExecutionContext context = null;
            variables = new DictionaryBlueprintVariableStore(blueprint);
            context = new BlueprintExecutionContext(
                blueprint,
                null,
                null,
                new NullBlueprintBindingResolver(),
                variables,
                new ActionBlueprintEventBus(eventName => vm.TriggerEvent(context, eventName)),
                logger,
                (node, outputPortId) => vm.ExecuteFromOutput(context, node, outputPortId));

            vm.TriggerEvent(context, "OnStart");
            return logger;
        }

        private static int FindLogIndex(RecordingBlueprintLogger logger, string message)
        {
            return logger.Entries.FindIndex(entry => entry == "Log: " + message);
        }

        private static BlueprintSource CreateStructuredVariableTestSource()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "StructuredVariableTest";
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "selectedItem",
                Type = "Test.InventoryItem",
                DefaultValue = CreateStructuredDefaultValue("sword_01", 1, TestInventoryItemRarity.Rare),
                Scope = "runtime",
                Exposed = true
            });
            return source;
        }

        private static Dictionary<string, object> CreateStructuredDefaultValue(string id, int count, TestInventoryItemRarity rarity)
        {
            return new Dictionary<string, object>
            {
                { "id", id },
                { "count", count },
                { "rarity", rarity.ToString() },
                { "position", new List<object> { 1f, 2f } }
            };
        }

        private static BlueprintSource CreateUserStructVariableTestSource()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "UserStructVariableTest";
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "selectedItem",
                Type = "Struct.TestInventoryItem",
                DefaultValue = CreateUserStructDefaultValue("sword_01", 1),
                Scope = "runtime",
                Exposed = true
            });
            return source;
        }

        private static BlueprintSource CreateBreakStructValidationSource(string sinkType)
        {
            BlueprintSource source = CreateUserStructVariableTestSource();
            source.Name = "BreakStructValidationTest";
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "countCopy",
                Type = sinkType,
                Scope = "runtime"
            });

            BlueprintNodeSource getItem = AddNode(source, "get_item", "Variable.Get");
            getItem.Properties["name"] = "selectedItem";

            BlueprintNodeSource breakItem = AddNode(source, "break_item", "Variable.BreakStruct");
            breakItem.Properties["structTypeId"] = "Struct.TestInventoryItem";

            BlueprintNodeSource setCount = AddNode(source, "set_count", "Variable.Set");
            setCount.Properties["name"] = "countCopy";

            source.Edges.Add(new BlueprintEdgeSource { From = "get_item.value", To = "break_item.target" });
            source.Edges.Add(new BlueprintEdgeSource { From = "break_item.fld_count", To = "set_count.value" });
            return source;
        }

        private static Dictionary<string, object> CreateUserStructDefaultValue(string id, int count)
        {
            return new Dictionary<string, object>
            {
                { "itemId", id },
                { "count", count },
                { "position", new List<object> { 1f, 2f } }
            };
        }

        private static void WriteUserStructDefinition()
        {
            string assetPath = "Assets/BlueprintSystem/Specs/Structs/TestInventoryItem.bpstruct.json";
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            File.WriteAllText(assetPath, "{\n" +
                "  \"schemaVersion\": \"0.1\",\n" +
                "  \"typeId\": \"Struct.TestInventoryItem\",\n" +
                "  \"fields\": [\n" +
                "    { \"id\": \"fld_item_id\", \"name\": \"itemId\", \"type\": \"string\", \"defaultValue\": \"\" },\n" +
                "    { \"id\": \"fld_count\", \"name\": \"count\", \"type\": \"int\", \"defaultValue\": 1 },\n" +
                "    { \"id\": \"fld_position\", \"name\": \"position\", \"type\": \"Vector2\", \"defaultValue\": [0, 0] }\n" +
                "  ]\n" +
                "}\n");
            AssetDatabase.ImportAsset(assetPath);
            BlueprintUserStructRegistry.Refresh();
        }

        private static void WriteDataTableDefinition(string assetPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            File.WriteAllText(assetPath, "{\n" +
                "  \"schemaVersion\": \"0.1\",\n" +
                "  \"tableId\": \"Table.TestItems\",\n" +
                "  \"rowStructTypeId\": \"Struct.TestInventoryItem\",\n" +
                "  \"rows\": [\n" +
                "    { \"rowName\": \"sword_01\", \"value\": { \"itemId\": \"sword_01\", \"count\": 1, \"position\": [1, 2] } },\n" +
                "    { \"rowName\": \"shield_01\", \"value\": { \"itemId\": \"shield_01\", \"count\": 2, \"position\": [3, 4] } }\n" +
                "  ]\n" +
                "}\n");
            AssetDatabase.ImportAsset(assetPath);
            BlueprintDataTableRegistry.Refresh();
        }

        private static RuntimeNode CreateDataTableRuntimeNode(string id, string typeId, string tablePath)
        {
            RuntimeNode node = CreateRuntimeNode(id, typeId);
            node.Properties["tablePath"] = tablePath;
            node.Properties["rowStructTypeId"] = "Struct.TestInventoryItem";
            return node;
        }

        private static BlueprintSource CreateDataTableValidationSource(string tablePath, string sinkType)
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "DataTableValidationTest";
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "selectedItem",
                Type = sinkType,
                Scope = "runtime"
            });
            source.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "items",
                Type = "Array<Struct.TestInventoryItem>",
                Scope = "runtime"
            });

            BlueprintNodeSource getRow = AddNode(source, "get_row", "DataTable.GetRow");
            getRow.Properties["tablePath"] = tablePath;
            getRow.Properties["rowStructTypeId"] = "Struct.TestInventoryItem";
            getRow.Properties["rowName"] = "sword_01";

            BlueprintNodeSource setItem = AddNode(source, "set_item", "Variable.Set");
            setItem.Properties["name"] = "selectedItem";

            BlueprintNodeSource getAllRows = AddNode(source, "get_all_rows", "DataTable.GetAllRows");
            getAllRows.Properties["tablePath"] = tablePath;
            getAllRows.Properties["rowStructTypeId"] = "Struct.TestInventoryItem";

            BlueprintNodeSource setRows = AddNode(source, "set_rows", "Variable.Set");
            setRows.Properties["name"] = "items";

            source.Edges.Add(new BlueprintEdgeSource { From = "get_row.row", To = "set_item.value" });
            source.Edges.Add(new BlueprintEdgeSource { From = "get_all_rows.rows", To = "set_rows.value" });
            return source;
        }

        private static void DeleteUserStructDefinition()
        {
            AssetDatabase.DeleteAsset("Assets/BlueprintSystem/Specs/Structs/TestInventoryItem.bpstruct.json");
            AssetDatabase.DeleteAsset("Assets/BlueprintSystem/Specs/Structs/TestInventoryItem.bpstruct.json.meta");
            BlueprintUserStructRegistry.Refresh();
        }

        private static BlueprintSource CreateSetImageSpriteTestSource()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "SetImageSpriteTest";
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "ItemIcon",
                Type = "Image",
                Required = true
            });
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "SwordSprite",
                Type = "Sprite",
                Required = true
            });

            BlueprintNodeSource node = AddNode(source, "set_item_icon", "UI.SetImageSprite");
            node.Properties["target"] = "ItemIcon";
            node.Properties["value"] = "SwordSprite";
            return source;
        }

        private static BlueprintSource CreateSetImageSpriteWithSpriteBindingTestSource()
        {
            BlueprintSource source = CreateSetImageSpriteTestSource();
            source.Nodes.Find(node => node.Id == "set_item_icon").Properties.Remove("value");
            BlueprintNodeSource spriteBinding = AddNode(source, "sprite_sword", "UI.SpriteBinding");
            spriteBinding.Properties["sprite"] = "SwordSprite";
            source.Edges.Add(new BlueprintEdgeSource
            {
                From = "sprite_sword.value",
                To = "set_item_icon.value"
            });
            return source;
        }

        private static BlueprintSource CreateCollisionTestSource()
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "CollisionTest";
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "Player",
                Type = "GameObject",
                Required = true
            });
            source.Bindings.Add(new BlueprintBindingDeclaration
            {
                Name = "Enemy",
                Type = "GameObject",
                Required = true
            });

            BlueprintNodeSource collision = AddNode(source, "is_colliding", "Game.IsColliding");
            collision.Properties["target"] = "Player";
            collision.Properties["other"] = "Enemy";
            return source;
        }

        private static BlueprintSource CreateInputPollingRuntimeSource(string typeId, string propertyId, string propertyValue)
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "InputPollingTest";

            BlueprintNodeSource listener = AddNode(source, "listen_input", typeId);
            listener.Properties[propertyId] = propertyValue;

            BlueprintNodeSource pressed = AddNode(source, "log_pressed", "Game.Log");
            pressed.Properties["message"] = "pressed";
            BlueprintNodeSource held = AddNode(source, "log_held", "Game.Log");
            held.Properties["message"] = "held";
            BlueprintNodeSource released = AddNode(source, "log_released", "Game.Log");
            released.Properties["message"] = "released";

            source.Edges.Add(new BlueprintEdgeSource
            {
                From = "listen_input.pressed",
                To = "log_pressed.execIn"
            });
            source.Edges.Add(new BlueprintEdgeSource
            {
                From = "listen_input.held",
                To = "log_held.execIn"
            });
            source.Edges.Add(new BlueprintEdgeSource
            {
                From = "listen_input.released",
                To = "log_released.execIn"
            });
            return source;
        }

        private static RecordingBlueprintLogger CreateInputPollingContext(
            GameObject owner,
            RuntimeBlueprint blueprint,
            out BlueprintExecutionContext context,
            out RuntimeNode listener)
        {
            RecordingBlueprintLogger logger = new RecordingBlueprintLogger();
            context = CreateInputContext(owner, blueprint, logger);
            listener = blueprint.GetNode("listen_input");
            Assert.NotNull(listener);
            return logger;
        }

        private static BlueprintExecResult PollInputNode(BlueprintExecutionContext context, RuntimeNode listener)
        {
            BlueprintExecResult result = listener.Executor.Execute(context, listener);
            Assert.True(string.IsNullOrEmpty(result.ErrorMessage), result.ErrorMessage);
            if (result.NextExecPortIds != null)
            {
                for (int i = 0; i < result.NextExecPortIds.Count; i++)
                {
                    context.ExecuteFromOutput(listener, result.NextExecPortIds[i]);
                }
            }
            else if (!string.IsNullOrEmpty(result.NextExecPortId))
            {
                context.ExecuteFromOutput(listener, result.NextExecPortId);
            }

            return result;
        }

        private static void AssertInputOutputs(BlueprintExecResult result, params string[] expectedOutputs)
        {
            Assert.NotNull(result.NextExecPortIds);
            CollectionAssert.AreEqual(expectedOutputs, result.NextExecPortIds);
        }

        private static BlueprintExecutionContext CreateInputContext(GameObject owner, RuntimeBlueprint blueprint, RecordingBlueprintLogger logger)
        {
            BlueprintVM vm = new BlueprintVM();
            BlueprintExecutionContext context = null;
            context = new BlueprintExecutionContext(
                blueprint,
                owner,
                null,
                new NullBlueprintBindingResolver(),
                new DictionaryBlueprintVariableStore(blueprint),
                null,
                logger,
                (node, outputPortId) => vm.ExecuteFromOutput(context, node, outputPortId));
            return context;
        }

        private static void QueueKeyboardState(params Key[] pressedKeys)
        {
            Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(pressedKeys));
            InputSystem.Update();
        }

        private static Keyboard GetCurrentKeyboard(out bool createdKeyboard)
        {
            InputSystem.Update();
            Keyboard keyboard = Keyboard.current;
            createdKeyboard = keyboard == null;
            return keyboard != null ? keyboard : InputSystem.AddDevice<Keyboard>();
        }

        private static int CountLogEntries(RecordingBlueprintLogger logger, string message)
        {
            return logger.Entries.Count(entry => entry == "Log: " + message);
        }

        private static BlueprintRunner CreateInitializedRunner(string name, RuntimeBlueprint blueprint)
        {
            GameObject gameObject = new GameObject(name);
            BlueprintRunner runner = gameObject.AddComponent<BlueprintRunner>();
            BlueprintVM vm = new BlueprintVM();
            BlueprintExecutionContext context = null;
            context = new BlueprintExecutionContext(
                blueprint,
                gameObject,
                runner,
                new NullBlueprintBindingResolver(),
                new DictionaryBlueprintVariableStore(blueprint),
                new ActionBlueprintEventBus(eventName => vm.TriggerEvent(context, eventName)),
                new RecordingBlueprintLogger(),
                (node, outputPortId) => vm.ExecuteFromOutput(context, node, outputPortId),
                runner,
                null);

            SetPrivateField(runner, "_blueprint", blueprint);
            SetPrivateField(runner, "_vm", vm);
            SetPrivateField(runner, "_context", context);
            return runner;
        }

        private static BlueprintExecutionContext CreateTestExecutionContext(RuntimeBlueprint blueprint, GameObject owner, IBlueprintBindingResolver resolver)
        {
            return new BlueprintExecutionContext(
                blueprint,
                owner,
                null,
                resolver,
                new DictionaryBlueprintVariableStore(blueprint),
                null,
                new RecordingBlueprintLogger());
        }

        private static void ExecuteNode(RuntimeBlueprint blueprint, BlueprintExecutionContext context, string nodeId)
        {
            RuntimeNode node = blueprint.GetNode(nodeId);
            Assert.NotNull(node, nodeId);
            BlueprintExecResult result = node.Executor.Execute(context, node);
            Assert.True(string.IsNullOrEmpty(result.ErrorMessage), result.ErrorMessage);
            Assert.AreEqual("execOut", result.NextExecPortId);
        }

        private static bool EvaluateComparison(string comparison, object left, object right)
        {
            BlueprintSource source = new BlueprintSource();
            source.SchemaVersion = "0.1";
            source.Name = "ComparisonRuntimeTest";
            BlueprintNodeSource compare = AddNode(source, "compare", "Variable.Compare");
            compare.Properties["left"] = left;
            compare.Properties["right"] = right;
            compare.Properties["comparison"] = comparison;

            BlueprintCompileResult compileResult = new BlueprintCompiler().Compile(source, LoadManifests(), BlueprintExecutorRegistry.CreateDefault());
            Assert.True(compileResult.Success, compileResult.Diagnostics.ToDisplayString());

            BlueprintExecutionContext context = new BlueprintExecutionContext(
                compileResult.Blueprint,
                null,
                null,
                new NullBlueprintBindingResolver(),
                new DictionaryBlueprintVariableStore(compileResult.Blueprint),
                null,
                new RecordingBlueprintLogger());

            RuntimeNode node = compileResult.Blueprint.GetNode("compare");
            Assert.NotNull(node);
            return (bool)node.Executor.Evaluate(context, node, "result");
        }


        private static BlueprintNodeSource AddNode(BlueprintSource source, string id, string typeId)
        {
            BlueprintNodeSource node = new BlueprintNodeSource
            {
                Id = id,
                TypeId = typeId
            };
            source.Nodes.Add(node);
            return node;
        }

        private static RuntimeNode CreateRuntimeNode(string id, string typeId)
        {
            return new RuntimeNode
            {
                Id = id,
                TypeId = typeId
            };
        }

        private static BlueprintExecutionContext CreateTestContext(
            RuntimeBlueprint blueprint,
            IBlueprintBindingResolver resolver,
            IBlueprintLogger logger,
            System.Action<RuntimeNode, string> executeFromOutput)
        {
            return new BlueprintExecutionContext(
                blueprint,
                null,
                null,
                resolver,
                new DictionaryBlueprintVariableStore(blueprint),
                new ActionBlueprintEventBus(eventName => { }),
                logger,
                executeFromOutput);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void SetBlackboardDefaultValue(IVariable variable, object value)
        {
            PropertyInfo initializationProperty = variable.GetType().GetProperty("InitializationModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(initializationProperty, "InitializationModel");
            object initializationModel = initializationProperty.GetValue(variable, null);
            Assert.NotNull(initializationModel, "InitializationModel value");

            PropertyInfo objectValueProperty = initializationModel.GetType().GetProperty("ObjectValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(objectValueProperty, "ObjectValue");
            objectValueProperty.SetValue(initializationModel, value, null);
        }

        private static BlueprintSource LoadBlueprint(string assetPath)
        {
            return BlueprintSource.FromJson(File.ReadAllText(assetPath));
        }

        private static TextAsset WriteTemporaryBlueprintAsset(string assetPath, BlueprintSource source)
        {
            File.WriteAllText(assetPath, source.ToJson());
            AssetDatabase.ImportAsset(assetPath);
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            Assert.NotNull(asset, assetPath);
            return asset;
        }

        private static void DeleteTemporaryCompiledArtifacts(string blueprintPath)
        {
            string compiledPath = BlueprintCompiledAssetCompiler.GetCompiledAssetPath(blueprintPath);
            AssetDatabase.DeleteAsset(compiledPath);
            AssetDatabase.DeleteAsset(blueprintPath);
        }

        private static void WriteManifest(string assetPath, string description)
        {
            string directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(assetPath, "{\n" +
                "  \"schemaVersion\": \"0.1\",\n" +
                "  \"typeId\": \"Test.CompiledManifestStale\",\n" +
                "  \"title\": \"Compiled Manifest Stale\",\n" +
                "  \"category\": \"Tests\",\n" +
                "  \"description\": \"" + description + "\",\n" +
                "  \"executor\": \"Game.Log\",\n" +
                "  \"inputs\": [],\n" +
                "  \"outputs\": [],\n" +
                "  \"properties\": []\n" +
                "}\n");
            AssetDatabase.ImportAsset(assetPath);
        }

        private static BlueprintNodeManifestCollection LoadManifests()
        {
            return BlueprintNodeManifestAssetUtility.LoadManifests();
        }

        private enum TestInventoryItemRarity
        {
            Common,
            Rare
        }

        [System.Serializable]
        [BlueprintVariableType("Test.InventoryItem")]
        private struct TestInventoryItemData
        {
            public string id;
            public int count;
            public TestInventoryItemRarity rarity;
            public Vector2 position;
        }

        [CustomPropertyDrawer(typeof(TestInventoryItemData))]
        private sealed class TestInventoryItemDataDrawer : BlueprintStructuredValueDrawer
        {
        }

        private sealed class TestBindingResolver : IBlueprintBindingResolver
        {
            private readonly Dictionary<string, Object> _bindings = new Dictionary<string, Object>();

            public void Add(string name, Object target)
            {
                _bindings[name] = target;
            }

            public T Resolve<T>(string bindingName) where T : Object
            {
                Object value = Resolve(bindingName);
                return value as T;
            }

            public Object Resolve(string bindingName)
            {
                Object value;
                return _bindings.TryGetValue(bindingName, out value) ? value : null;
            }

            public bool HasBinding(string bindingName)
            {
                return _bindings.ContainsKey(bindingName);
            }
        }
    }
}
