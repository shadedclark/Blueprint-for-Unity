using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace BlueprintSystem
{
    public enum BlueprintPersistenceStatus
    {
        Success,
        Missing,
        Failed
    }

    public interface IBlueprintPersistenceStore
    {
        BlueprintPersistenceStatus Save(string slot, string persistenceKey, IDictionary<string, object> document, out string error);
        BlueprintPersistenceStatus Load(string slot, string persistenceKey, out Dictionary<string, object> document, out string error);
        BlueprintPersistenceStatus Delete(string slot, string persistenceKey, out string error);
    }

    public sealed class JsonFileBlueprintPersistenceStore : IBlueprintPersistenceStore
    {
        private const string DirectoryName = "BlueprintSystem";

        public BlueprintPersistenceStatus Save(
            string slot,
            string persistenceKey,
            IDictionary<string, object> document,
            out string error)
        {
            error = string.Empty;
            string path = GetPath(slot, persistenceKey);
            string temporaryPath = path + ".tmp";
            string backupPath = path + ".bak";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporaryPath, BlueprintJson.Serialize(document, true), new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporaryPath, path, backupPath);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        ReplaceWithFallback(temporaryPath, path, backupPath);
                    }
                    catch (IOException)
                    {
                        ReplaceWithFallback(temporaryPath, path, backupPath);
                    }
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                return BlueprintPersistenceStatus.Success;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                TryDeleteFile(temporaryPath);
                return BlueprintPersistenceStatus.Failed;
            }
        }

        public BlueprintPersistenceStatus Load(
            string slot,
            string persistenceKey,
            out Dictionary<string, object> document,
            out string error)
        {
            document = null;
            error = string.Empty;
            string path = GetPath(slot, persistenceKey);
            if (!File.Exists(path))
            {
                return BlueprintPersistenceStatus.Missing;
            }

            try
            {
                document = BlueprintJson.DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
                return BlueprintPersistenceStatus.Success;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return BlueprintPersistenceStatus.Failed;
            }
        }

        public BlueprintPersistenceStatus Delete(string slot, string persistenceKey, out string error)
        {
            error = string.Empty;
            string path = GetPath(slot, persistenceKey);
            if (!File.Exists(path))
            {
                return BlueprintPersistenceStatus.Missing;
            }

            try
            {
                File.Delete(path);
                TryDeleteFile(path + ".tmp");
                TryDeleteFile(path + ".bak");
                return BlueprintPersistenceStatus.Success;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return BlueprintPersistenceStatus.Failed;
            }
        }

        private static string GetPath(string slot, string persistenceKey)
        {
            string identity = (slot ?? string.Empty) + "\n" + (persistenceKey ?? string.Empty);
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
            }

            StringBuilder fileName = new StringBuilder(digest.Length * 2);
            for (int i = 0; i < digest.Length; i++)
            {
                fileName.Append(digest[i].ToString("x2"));
            }

            return Path.Combine(Application.persistentDataPath, DirectoryName, fileName + ".json");
        }

        private static void ReplaceWithFallback(string temporaryPath, string path, string backupPath)
        {
            File.Copy(path, backupPath, true);
            File.Delete(path);
            File.Move(temporaryPath, path);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    public static class BlueprintPersistenceRuntime
    {
        private const int SchemaVersion = 1;
        private static IBlueprintPersistenceStore _store = new JsonFileBlueprintPersistenceStore();

        public static IBlueprintPersistenceStore Store
        {
            get { return _store; }
            set { _store = value ?? new JsonFileBlueprintPersistenceStore(); }
        }

        public static BlueprintPersistenceStatus Save(BlueprintRunner runner, string slot, out string error)
        {
            error = string.Empty;
            if (!ValidateRunner(runner, out error))
            {
                return BlueprintPersistenceStatus.Failed;
            }

            Dictionary<string, object> document = new Dictionary<string, object>();
            document["schemaVersion"] = SchemaVersion;
            document["slot"] = NormalizeSlot(slot, runner.DefaultPersistenceSlot);
            document["persistenceKey"] = runner.PersistenceKey;
            List<object> values = new List<object>();
            CaptureInstance(runner, "root", values, runner);
            document["values"] = values;
            return Store.Save((string)document["slot"], runner.PersistenceKey, document, out error);
        }

        public static BlueprintPersistenceStatus Load(BlueprintRunner runner, string slot, out string error)
        {
            error = string.Empty;
            if (!ValidateRunner(runner, out error))
            {
                return BlueprintPersistenceStatus.Failed;
            }

            Dictionary<string, object> document;
            BlueprintPersistenceStatus status = Store.Load(
                NormalizeSlot(slot, runner.DefaultPersistenceSlot),
                runner.PersistenceKey,
                out document,
                out error);
            if (status != BlueprintPersistenceStatus.Success)
            {
                return status;
            }

            object schemaValue;
            if (!document.TryGetValue("schemaVersion", out schemaValue) ||
                BlueprintTypeUtility.ConvertValue(schemaValue, 0) != SchemaVersion)
            {
                error = "Unsupported persistence schema version.";
                return BlueprintPersistenceStatus.Failed;
            }

            object keyValue;
            if (!document.TryGetValue("persistenceKey", out keyValue) ||
                !string.Equals(BlueprintTypeUtility.ConvertValue(keyValue, string.Empty), runner.PersistenceKey, StringComparison.Ordinal))
            {
                error = "Persistence key does not match the runner.";
                return BlueprintPersistenceStatus.Failed;
            }

            object valuesValue;
            IList values = document.TryGetValue("values", out valuesValue) ? valuesValue as IList : null;
            if (values == null)
            {
                error = "Persistence document has no values array.";
                return BlueprintPersistenceStatus.Failed;
            }

            for (int i = 0; i < values.Count; i++)
            {
                IDictionary<string, object> entry = values[i] as IDictionary<string, object>;
                if (entry != null)
                {
                    RestoreEntry(runner, entry, runner);
                }
            }

            runner.ClearPersistenceDirty();
            BlueprintReactiveBindingRuntime.RefreshInstance(runner);
            return BlueprintPersistenceStatus.Success;
        }

        public static BlueprintPersistenceStatus Delete(BlueprintRunner runner, string slot, out string error)
        {
            error = string.Empty;
            if (!ValidateRunner(runner, out error))
            {
                return BlueprintPersistenceStatus.Failed;
            }

            BlueprintPersistenceStatus status = Store.Delete(
                NormalizeSlot(slot, runner.DefaultPersistenceSlot),
                runner.PersistenceKey,
                out error);
            if (status != BlueprintPersistenceStatus.Failed)
            {
                runner.ClearPersistenceDirty();
            }
            return status;
        }

        public static void MarkDirty(BlueprintExecutionContext context, string variableName)
        {
            if (context == null || context.Blueprint == null || string.IsNullOrEmpty(variableName))
            {
                return;
            }

            BlueprintVariableDeclaration declaration = FindDeclaration(context.Blueprint, string.Empty, variableName);
            if (declaration == null || !declaration.Persistent)
            {
                return;
            }

            BlueprintRunner runner = context.OwnerComponent as BlueprintRunner;
            if (runner != null)
            {
                runner.MarkPersistenceDirty();
            }
        }

        public static BlueprintRunner ResolveRunner(BlueprintExecutionContext context)
        {
            return context == null ? null : context.OwnerComponent as BlueprintRunner;
        }

        private static void CaptureInstance(
            IBlueprintInstance instance,
            string instancePath,
            List<object> values,
            BlueprintRunner runner)
        {
            RuntimeBlueprint blueprint = instance == null ? null : instance.RuntimeBlueprint;
            if (blueprint == null)
            {
                return;
            }

            for (int i = 0; i < blueprint.Variables.Count; i++)
            {
                BlueprintVariableDeclaration declaration = blueprint.Variables[i];
                if (declaration == null || !declaration.Persistent || string.IsNullOrEmpty(declaration.Name))
                {
                    continue;
                }

                object runtimeValue;
                object jsonValue;
                string conversionError = string.Empty;
                if (!instance.TryGetVariable(declaration.Name, out runtimeValue) ||
                    !TryConvertToJsonValue(runtimeValue, declaration, out jsonValue, out conversionError))
                {
                    if (!string.IsNullOrEmpty(conversionError))
                    {
                        BlueprintLog.Warning("[Blueprint] Persistent variable '" + declaration.Name + "' was skipped: " + conversionError, runner);
                    }
                    continue;
                }

                Dictionary<string, object> entry = new Dictionary<string, object>();
                entry["instance"] = instancePath;
                entry["id"] = declaration.Id ?? string.Empty;
                entry["name"] = declaration.Name;
                entry["type"] = declaration.Type ?? string.Empty;
                entry["value"] = jsonValue;
                values.Add(entry);
            }

            for (int i = 0; i < blueprint.Components.Count; i++)
            {
                BlueprintComponentDeclaration declaration = blueprint.Components[i];
                IBlueprintInstance child;
                if (declaration != null && !string.IsNullOrEmpty(declaration.Name) &&
                    instance.TryGetBlueprintComponent(declaration.Name, out child) && child != null)
                {
                    CaptureInstance(child, instancePath + "/" + declaration.Name, values, runner);
                }
            }
        }

        private static void RestoreEntry(
            BlueprintRunner root,
            IDictionary<string, object> entry,
            BlueprintRunner loggerContext)
        {
            string instancePath = GetString(entry, "instance");
            IBlueprintInstance instance = ResolveInstance(root, instancePath);
            if (instance == null || instance.RuntimeBlueprint == null)
            {
                return;
            }

            string id = GetString(entry, "id");
            string name = GetString(entry, "name");
            BlueprintVariableDeclaration declaration = FindDeclaration(instance.RuntimeBlueprint, id, name);
            if (declaration == null || !declaration.Persistent)
            {
                return;
            }

            object jsonValue;
            if (!entry.TryGetValue("value", out jsonValue))
            {
                return;
            }

            object runtimeValue;
            string conversionError;
            if (!TryConvertToRuntimeValue(jsonValue, declaration, out runtimeValue, out conversionError))
            {
                BlueprintLog.Warning("[Blueprint] Persistent variable '" + declaration.Name + "' was not loaded: " + conversionError, loggerContext);
                return;
            }

            instance.TrySetVariable(declaration.Name, runtimeValue);
        }

        private static IBlueprintInstance ResolveInstance(IBlueprintInstance root, string instancePath)
        {
            if (root == null || string.IsNullOrEmpty(instancePath) || instancePath == "root")
            {
                return root;
            }

            string[] parts = instancePath.Split('/');
            if (parts.Length == 0 || parts[0] != "root")
            {
                return null;
            }

            IBlueprintInstance current = root;
            for (int i = 1; i < parts.Length; i++)
            {
                IBlueprintInstance child;
                if (string.IsNullOrEmpty(parts[i]) || !current.TryGetBlueprintComponent(parts[i], out child) || child == null)
                {
                    return null;
                }
                current = child;
            }
            return current;
        }

        private static BlueprintVariableDeclaration FindDeclaration(RuntimeBlueprint blueprint, string id, string name)
        {
            if (blueprint == null)
            {
                return null;
            }

            for (int i = 0; i < blueprint.Variables.Count; i++)
            {
                BlueprintVariableDeclaration declaration = blueprint.Variables[i];
                if (declaration == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(id) && string.Equals(declaration.Id, id, StringComparison.Ordinal))
                {
                    return declaration;
                }

                if (!string.IsNullOrEmpty(name) && string.Equals(declaration.Name, name, StringComparison.Ordinal))
                {
                    return declaration;
                }
            }
            return null;
        }

        private static bool TryConvertToJsonValue(
            object value,
            BlueprintVariableDeclaration declaration,
            out object jsonValue,
            out string error)
        {
            jsonValue = value;
            error = string.Empty;
            if (value == null)
            {
                return true;
            }

            if ((!string.IsNullOrEmpty(declaration.Type) && declaration.Type.StartsWith("Binding<", StringComparison.Ordinal)) ||
                string.Equals(declaration.Type, "BlueprintRef", StringComparison.Ordinal))
            {
                error = "type '" + declaration.Type + "' is a runtime reference and cannot be persisted.";
                jsonValue = null;
                return false;
            }

            if (BlueprintArrayUtility.TryConvertToJsonArray(value, declaration.Type, out jsonValue) ||
                BlueprintStructuredValueUtility.TryConvertToJsonValue(value, declaration.Type, out jsonValue))
            {
                return true;
            }

            Type clrType;
            if (BlueprintVariableTypeRegistry.TryGetClrType(declaration.Type, out clrType) && clrType.IsEnum)
            {
                jsonValue = value.ToString();
                return true;
            }

            if (value is Vector2)
            {
                Vector2 vector = (Vector2)value;
                jsonValue = new List<object> { vector.x, vector.y };
                return true;
            }
            if (value is Vector3)
            {
                Vector3 vector = (Vector3)value;
                jsonValue = new List<object> { vector.x, vector.y, vector.z };
                return true;
            }
            if (value is Vector4)
            {
                Vector4 vector = (Vector4)value;
                jsonValue = new List<object> { vector.x, vector.y, vector.z, vector.w };
                return true;
            }
            if (value is Color)
            {
                Color color = (Color)value;
                jsonValue = new List<object> { color.r, color.g, color.b, color.a };
                return true;
            }
            if (value is Rect)
            {
                Rect rect = (Rect)value;
                jsonValue = new List<object> { rect.x, rect.y, rect.width, rect.height };
                return true;
            }

            if (value is string || value is bool || value is byte || value is sbyte ||
                value is short || value is ushort || value is int || value is uint ||
                value is long || value is ulong || value is float || value is double || value is decimal)
            {
                return true;
            }

            error = "type '" + declaration.Type + "' is not portable.";
            jsonValue = null;
            return false;
        }

        private static bool TryConvertToRuntimeValue(
            object value,
            BlueprintVariableDeclaration declaration,
            out object runtimeValue,
            out string error)
        {
            runtimeValue = value;
            error = string.Empty;
            if (BlueprintArrayUtility.TryConvertToRuntimeArray(value, declaration.Type, out runtimeValue) ||
                BlueprintStructuredValueUtility.TryConvertToRuntimeValue(value, declaration.Type, out runtimeValue))
            {
                return true;
            }

            Type clrType;
            if (!BlueprintVariableTypeRegistry.TryGetClrType(declaration.Type, out clrType))
            {
                error = "unknown type '" + declaration.Type + "'.";
                return false;
            }

            try
            {
                runtimeValue = BlueprintTypeUtility.ConvertValue(value, clrType, declaration.DefaultValue);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                runtimeValue = null;
                return false;
            }
        }

        private static bool ValidateRunner(BlueprintRunner runner, out string error)
        {
            if (runner == null)
            {
                error = "No BlueprintRunner is available for persistence.";
                return false;
            }
            if (string.IsNullOrEmpty(runner.PersistenceKey))
            {
                error = "BlueprintRunner persistenceKey is empty.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static string NormalizeSlot(string slot, string fallback)
        {
            return string.IsNullOrEmpty(slot) ? (string.IsNullOrEmpty(fallback) ? "default" : fallback) : slot;
        }

        private static string GetString(IDictionary<string, object> dictionary, string key)
        {
            object value;
            return dictionary != null && dictionary.TryGetValue(key, out value)
                ? BlueprintTypeUtility.ConvertValue(value, string.Empty)
                : string.Empty;
        }
    }
}
