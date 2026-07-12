using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlueprintLangGraph.Editor;
using BlueprintSystem.Editor;
using NUnit.Framework;
using UnityEditor;

namespace BlueprintSystem.Tests
{
    public sealed class BlueprintMcpDiagnosticsTests
    {
        private const string TempRoot = "Assets/BlueprintSystem/Tests/Editor/TempMcpDiagnostics";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TempRoot);
            AssetDatabase.CreateFolder("Assets/BlueprintSystem/Tests/Editor", "TempMcpDiagnostics");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void DependencyOrderedCompileDryRunPlacesComponentsBeforeOwner()
        {
            string leaf = WriteBlueprint("Leaf.blueprint.json", "Leaf");
            string middle = WriteBlueprint("Middle.blueprint.json", "Middle", "Leaf", "Leaf.blueprint.json");
            string root = WriteBlueprint("Root.blueprint.json", "Root", "Middle", "Middle.blueprint.json");

            object result = BlueprintMcpCompileBridge.CompileDependencyOrdered(new BlueprintCompileDependencyOrderedParams
            {
                SourcePaths = new List<string> { root },
                DryRun = true,
                ImportAssets = false,
                SyncRuntimeRegistry = false
            });

            Assert.True((bool)ReadProperty(result, "success"));
            object data = ReadProperty(result, "data");
            List<string> plannedPaths = AsEnumerable(ReadProperty(data, "compilePlan"))
                .Select(item => (string)ReadProperty(item, "sourcePath"))
                .ToList();
            CollectionAssert.AreEqual(new[] { leaf, middle, root }, plannedPaths);
        }

        [Test]
        public void DependencyOrderedCompileBuildsRootWithCompiledChild()
        {
            WriteBlueprint("Child.blueprint.json", "Child");
            string root = WriteBlueprint("Root.blueprint.json", "Root", "ChildComponent", "Child.blueprint.json");

            object result = BlueprintMcpCompileBridge.CompileDependencyOrdered(new BlueprintCompileDependencyOrderedParams
            {
                SourcePaths = new List<string> { root },
                ImportAssets = true,
                SyncRuntimeRegistry = false,
                ForceRecompile = true
            });

            Assert.True((bool)ReadProperty(result, "success"));
            BlueprintCompiledAsset rootAsset = AssetDatabase.LoadAssetAtPath<BlueprintCompiledAsset>(
                BlueprintCompiledAssetCompiler.GetCompiledAssetPath(root));
            Assert.NotNull(rootAsset);
            Assert.AreEqual(1, rootAsset.Components.Count);
            Assert.NotNull(rootAsset.Components[0].CompiledBlueprint);
            Assert.AreEqual(TempRoot + "/Child.blueprint.json", rootAsset.Components[0].BlueprintPath);
        }

        [Test]
        public void AssetReferenceScanFindsComponentPathAndJsonPointer()
        {
            string target = WriteBlueprint("Target.blueprint.json", "Target");
            string source = WriteBlueprint("Owner.blueprint.json", "Owner", "TargetComponent", "Target.blueprint.json");

            object result = UnityAssetReferenceScanBridge.AssetReferenceScan(new UnityAssetReferenceScanParams
            {
                Targets = new List<UnityAssetReferenceScanTargetSpec>
                {
                    new UnityAssetReferenceScanTargetSpec { Kind = "assetPath", Value = target }
                },
                SearchRoots = new List<string> { TempRoot },
                IncludeUnityDependencies = false,
                IncludeBlueprintJson = true,
                IncludeBehaviorTreeJson = false,
                IncludeDataTables = false,
                MaxMatches = 20
            });

            Assert.True((bool)ReadProperty(result, "success"));
            object data = ReadProperty(result, "data");
            object reference = AsEnumerable(ReadProperty(data, "references"))
                .FirstOrDefault(item => (string)ReadProperty(item, "sourceAssetPath") == source);
            Assert.NotNull(reference);
            Assert.AreEqual("blueprintComponentReference", ReadProperty(reference, "referenceKind"));
            Assert.AreEqual("/components/0/blueprint", ReadProperty(reference, "location"));
            Assert.True((bool)ReadProperty(reference, "blocking"));
        }

        [Test]
        public void RuntimeDebugDescriptorsExposeOnlyDeclaredMetadata()
        {
            var blueprint = new RuntimeBlueprint();
            blueprint.Variables.Add(new BlueprintVariableDeclaration
            {
                Id = "var_public",
                Name = "health",
                Type = "int",
                Scope = "runtime",
                Exposed = true
            });

            IReadOnlyList<BlueprintDebugVariableDescriptor> descriptors =
                BlueprintDebugInspectableUtility.GetVariableDescriptors(blueprint);
            Assert.AreEqual(1, descriptors.Count);
            Assert.AreEqual("health", descriptors[0].Name);
            Assert.True(descriptors[0].Exposed);
            Assert.AreEqual("int", descriptors[0].Type);
        }

        [Test]
        public void VmTraceRecordsEventNodePortAndVariableWrite()
        {
            var blueprint = new RuntimeBlueprint();
            blueprint.Variables.Add(new BlueprintVariableDeclaration
            {
                Name = "counter",
                Type = "int",
                DefaultValue = 0
            });
            blueprint.EventEntries["OnTrace"] = "set_counter";
            blueprint.NodesById["set_counter"] = new RuntimeNode
            {
                Id = "set_counter",
                TypeId = "Test.SetCounter",
                Executor = new SetCounterExecutor()
            };

            var context = new BlueprintExecutionContext(
                blueprint,
                null,
                null,
                null,
                new DictionaryBlueprintVariableStore(blueprint),
                null,
                new TestLogger());
            var sink = new TestTraceSink();
            context.TraceSink = sink;

            new BlueprintVM().TriggerEvent(context, "OnTrace");

            Assert.True(sink.Records.Any(record => record.Kind == BlueprintTraceRecordKind.EventMatched));
            Assert.True(sink.Records.Any(record => record.Kind == BlueprintTraceRecordKind.NodeEnter && record.NodeId == "set_counter"));
            Assert.True(sink.Records.Any(record => record.Kind == BlueprintTraceRecordKind.VariableWrite && record.Message == "counter"));
            Assert.True(sink.Records.Any(record => record.Kind == BlueprintTraceRecordKind.ExecPortSelected && record.PortId == "execOut"));
        }

        private static string WriteBlueprint(string fileName, string name, string componentName = null, string componentPath = null)
        {
            var source = new BlueprintSource
            {
                SchemaVersion = "0.1",
                Name = name
            };
            if (!string.IsNullOrEmpty(componentName))
            {
                source.Components.Add(new BlueprintComponentDeclaration
                {
                    Name = componentName,
                    Blueprint = componentPath,
                    Required = true
                });
            }

            string path = TempRoot + "/" + fileName;
            File.WriteAllText(path, source.ToJson());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return path;
        }

        private static object ReadProperty(object instance, string propertyName)
        {
            return instance.GetType().GetProperty(propertyName)?.GetValue(instance);
        }

        private static IEnumerable<object> AsEnumerable(object value)
        {
            return ((IEnumerable)value).Cast<object>();
        }

        private sealed class SetCounterExecutor : BlueprintNodeExecutor
        {
            public override string ExecutorId
            {
                get { return "Test.SetCounter"; }
            }

            public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
            {
                context.SetVariable("counter", 1);
                return BlueprintExecResult.Continue("execOut");
            }
        }

        private sealed class TestTraceSink : IBlueprintExecutionTraceSink
        {
            public readonly List<BlueprintTraceRecord> Records = new List<BlueprintTraceRecord>();

            public bool IsEnabled
            {
                get { return true; }
            }

            public void Record(BlueprintTraceRecord record)
            {
                Records.Add(record);
            }
        }

        private sealed class TestLogger : IBlueprintLogger
        {
            public void Log(string message) { }
            public void Warning(string message) { }
            public void Error(string message) { }
        }
    }
}
