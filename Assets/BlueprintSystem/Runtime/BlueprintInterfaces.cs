using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public interface IBlueprintInstance
    {
        string InstanceName { get; }
        RuntimeBlueprint RuntimeBlueprint { get; }
        BlueprintCompiledAsset CompiledBlueprint { get; }
        string SourcePath { get; }
        IBlueprintInstance OwnerInstance { get; }
        GameObject Owner { get; }
        Component OwnerComponent { get; }
        bool TryGetVariable(string variableName, out object value);
        bool TrySetVariable(string variableName, object value);
        void TriggerEvent(string eventName);
        bool HasEvent(string eventName);
        bool TryGetBlueprintComponent(string componentName, out IBlueprintInstance component);
    }

    public interface IBlueprintBindingResolver
    {
        T Resolve<T>(string bindingName) where T : UnityEngine.Object;
        UnityEngine.Object Resolve(string bindingName);
        bool HasBinding(string bindingName);
    }

    public interface IBlueprintVariableStore
    {
        object Get(string name);
        bool TryGet(string name, out object value);
        void Set(string name, object value);
        bool Contains(string name);
        void ResetToDefaults();
    }

    public interface IBlueprintEventBus
    {
        void Publish(string eventName);
    }

    public interface IBlueprintLogger
    {
        void Log(string message);
        void Warning(string message);
        void Error(string message);
    }

    public sealed class NullBlueprintBindingResolver : IBlueprintBindingResolver
    {
        public T Resolve<T>(string bindingName) where T : UnityEngine.Object
        {
            return null;
        }

        public UnityEngine.Object Resolve(string bindingName)
        {
            return null;
        }

        public bool HasBinding(string bindingName)
        {
            return false;
        }
    }

    public sealed class DictionaryBlueprintVariableStore : IBlueprintVariableStore
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        private readonly Dictionary<string, object> _initialValues = new Dictionary<string, object>();
        private readonly Dictionary<string, BlueprintVariableDeclaration> _declarationsByName = new Dictionary<string, BlueprintVariableDeclaration>();
        private readonly Dictionary<string, BlueprintVariableDeclaration> _declarationsById = new Dictionary<string, BlueprintVariableDeclaration>();
        private readonly HashSet<string> _dirtyNames = new HashSet<string>(StringComparer.Ordinal);

        public DictionaryBlueprintVariableStore()
        {
        }

        public DictionaryBlueprintVariableStore(RuntimeBlueprint blueprint)
        {
            if (blueprint == null)
            {
                return;
            }

            for (int i = 0; i < blueprint.Variables.Count; i++)
            {
                BlueprintVariableDeclaration variable = blueprint.Variables[i];
                if (variable == null || string.IsNullOrEmpty(variable.Name))
                {
                    continue;
                }

                _declarationsByName[variable.Name] = variable;
                if (!string.IsNullOrEmpty(variable.Id))
                {
                    _declarationsById[variable.Id] = variable;
                }
            }

            BuildInitialValuesFromDefaults();
            ResetToDefaults();
        }

        public DictionaryBlueprintVariableStore(RuntimeBlueprint blueprint, IEnumerable<BlueprintVariableOverride> overrides)
            : this(blueprint)
        {
            ApplyOverrides(overrides, true);
            CaptureInitialValuesFromCurrentValues();
        }

        public object Get(string name)
        {
            object value;
            return _values.TryGetValue(name, out value) ? value : null;
        }

        public bool TryGet(string name, out object value)
        {
            return _values.TryGetValue(name, out value);
        }

        public bool TryGetInitial(string name, out object value)
        {
            return _initialValues.TryGetValue(name, out value);
        }

        public bool IsDirty(string name)
        {
            return !string.IsNullOrEmpty(name) && _dirtyNames.Contains(name);
        }

        public void Set(string name, object value)
        {
            SetValue(name, value, true);
        }

        public void SetPreserved(string name, object value, bool dirty)
        {
            SetValue(name, value, dirty);
        }

        private void SetValue(string name, object value, bool dirty)
        {
            BlueprintVariableDeclaration declaration;
            if (_declarationsByName.TryGetValue(name, out declaration) && declaration != null)
            {
                value = CoerceValue(value, declaration.Type, declaration.DefaultValue);
            }

            _values[name] = value;
            if (dirty)
            {
                _dirtyNames.Add(name);
            }
            else
            {
                _dirtyNames.Remove(name);
            }
        }

        public bool Contains(string name)
        {
            return _values.ContainsKey(name);
        }

        public void ResetToDefaults()
        {
            _values.Clear();
            foreach (KeyValuePair<string, object> pair in _initialValues)
            {
                _values[pair.Key] = CloneValueForVariable(pair.Key, pair.Value);
            }

            _dirtyNames.Clear();
        }

        public void ApplyOverrides(IEnumerable<BlueprintVariableOverride> overrides, bool exposedOnly)
        {
            if (overrides == null)
            {
                return;
            }

            foreach (BlueprintVariableOverride variableOverride in overrides)
            {
                if (variableOverride == null || string.IsNullOrEmpty(variableOverride.Name))
                {
                    continue;
                }

                if (!IsOverrideEnabled(variableOverride))
                {
                    continue;
                }

                BlueprintVariableDeclaration declaration;
                string variableName;
                if (!TryFindDeclaration(variableOverride, out variableName, out declaration))
                {
                    continue;
                }

                if (exposedOnly && declaration != null && !declaration.Exposed)
                {
                    continue;
                }

                object value;
                if (!TryReadOverrideValue(variableOverride, declaration, out value))
                {
                    continue;
                }

                SetValue(variableName, value, false);
            }
        }

        private void BuildInitialValuesFromDefaults()
        {
            _initialValues.Clear();
            foreach (KeyValuePair<string, BlueprintVariableDeclaration> pair in _declarationsByName)
            {
                BlueprintVariableDeclaration variable = pair.Value;
                _initialValues[pair.Key] = CoerceValue(variable.DefaultValue, variable.Type, null);
            }
        }

        private void CaptureInitialValuesFromCurrentValues()
        {
            _initialValues.Clear();
            foreach (KeyValuePair<string, object> pair in _values)
            {
                _initialValues[pair.Key] = CloneValueForVariable(pair.Key, pair.Value);
            }

            _dirtyNames.Clear();
        }

        private static bool IsOverrideEnabled(BlueprintVariableOverride variableOverride)
        {
            if (variableOverride.Enabled)
            {
                return true;
            }

            return string.IsNullOrEmpty(variableOverride.VariableId) &&
                   !string.IsNullOrEmpty(variableOverride.Name) &&
                   !string.IsNullOrEmpty(variableOverride.JsonValue);
        }

        private bool TryFindDeclaration(BlueprintVariableOverride variableOverride, out string variableName, out BlueprintVariableDeclaration declaration)
        {
            variableName = null;
            declaration = null;
            if (variableOverride == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(variableOverride.VariableId) &&
                _declarationsById.TryGetValue(variableOverride.VariableId, out declaration) &&
                declaration != null &&
                !string.IsNullOrEmpty(declaration.Name))
            {
                variableName = declaration.Name;
                return true;
            }

            if (!string.IsNullOrEmpty(variableOverride.Name) &&
                _declarationsByName.TryGetValue(variableOverride.Name, out declaration) &&
                declaration != null)
            {
                variableName = variableOverride.Name;
                return true;
            }

            return false;
        }

        private static bool TryReadOverrideValue(BlueprintVariableOverride variableOverride, BlueprintVariableDeclaration declaration, out object value)
        {
            value = null;
            string jsonValue = variableOverride.JsonValue;
            string type = !string.IsNullOrEmpty(variableOverride.Type)
                ? variableOverride.Type
                : declaration == null ? null : declaration.Type;

            if (string.IsNullOrEmpty(jsonValue))
            {
                return true;
            }

            try
            {
                value = BlueprintJson.Deserialize(jsonValue);
            }
            catch (BlueprintJsonException)
            {
                if (type == "string")
                {
                    value = jsonValue;
                }
                else
                {
                    return false;
                }
            }

            return BlueprintTypeUtility.IsValueAssignableToType(value, type);
        }

        private static object CoerceValue(object value, string type, object defaultValue)
        {
            if (string.IsNullOrEmpty(type) || value == null)
            {
                return value;
            }

            switch (type)
            {
                case "string":
                case BlueprintVariableTypeRegistry.BlueprintAssetTypeId:
                    return BlueprintTypeUtility.ConvertValue(value, typeof(string), defaultValue);
                case "bool":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(bool), defaultValue);
                case "int":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(int), defaultValue);
                case "float":
                    return BlueprintTypeUtility.ConvertValue(value, typeof(float), defaultValue);
                case "Vector2":
                    return value is Vector2 ? value : BlueprintTypeUtility.ToVector2(value, defaultValue is Vector2 ? (Vector2)defaultValue : Vector2.zero);
                case "Vector3":
                    return value is Vector3 ? value : BlueprintTypeUtility.ToVector3(value, defaultValue is Vector3 ? (Vector3)defaultValue : Vector3.zero);
                case "Vector4":
                    return value is Vector4 ? value : BlueprintTypeUtility.ToVector4(value, defaultValue is Vector4 ? (Vector4)defaultValue : Vector4.zero);
                case "Rect":
                    return value is Rect ? value : BlueprintTypeUtility.ToRect(value, defaultValue is Rect ? (Rect)defaultValue : Rect.zero);
                case "Color":
                    return value is Color ? value : ToColor(value, defaultValue is Color ? (Color)defaultValue : Color.white);
                default:
                    if (BlueprintDataTableVariableTypeUtility.IsDataTableType(type))
                    {
                        string tablePath;
                        BlueprintDataTableDefinition definition;
                        return BlueprintDataTableVariableTypeUtility.TryResolveValue(value, type, out tablePath, out definition)
                            ? tablePath
                            : defaultValue;
                    }

                    object arrayValue;
                    if (BlueprintArrayUtility.TryConvertToRuntimeArray(value, type, out arrayValue))
                    {
                        return arrayValue;
                    }

                    Type clrType;
                    if (BlueprintVariableTypeRegistry.TryGetClrType(type, out clrType) && clrType.IsEnum)
                    {
                        return BlueprintTypeUtility.ConvertValue(value, clrType, defaultValue);
                    }

                    object structuredValue;
                    if (BlueprintStructuredValueUtility.TryConvertToRuntimeValue(value, type, out structuredValue))
                    {
                        return structuredValue;
                    }

                    return value;
            }
        }

        private object CloneValueForVariable(string name, object value)
        {
            BlueprintVariableDeclaration declaration;
            if (!_declarationsByName.TryGetValue(name, out declaration) || declaration == null)
            {
                return value;
            }

            object jsonValue;
            object runtimeValue;
            if (BlueprintArrayUtility.TryConvertToJsonArray(value, declaration.Type, out jsonValue) &&
                BlueprintArrayUtility.TryConvertToRuntimeArray(jsonValue, declaration.Type, out runtimeValue))
            {
                return runtimeValue;
            }

            if (BlueprintStructuredValueUtility.TryConvertToJsonValue(value, declaration.Type, out jsonValue) &&
                BlueprintStructuredValueUtility.TryConvertToRuntimeValue(jsonValue, declaration.Type, out runtimeValue))
            {
                return runtimeValue;
            }

            return CoerceValue(value, declaration.Type, declaration.DefaultValue);
        }

        private static Color ToColor(object value, Color defaultValue)
        {
            if (value is Color)
            {
                return (Color)value;
            }

            System.Collections.IList list = value as System.Collections.IList;
            if (list == null || (list.Count != 3 && list.Count != 4))
            {
                return defaultValue;
            }

            try
            {
                float r = Convert.ToSingle(list[0], System.Globalization.CultureInfo.InvariantCulture);
                float g = Convert.ToSingle(list[1], System.Globalization.CultureInfo.InvariantCulture);
                float b = Convert.ToSingle(list[2], System.Globalization.CultureInfo.InvariantCulture);
                float a = list.Count == 4 ? Convert.ToSingle(list[3], System.Globalization.CultureInfo.InvariantCulture) : 1f;
                return new Color(r, g, b, a);
            }
            catch
            {
                return defaultValue;
            }
        }
    }

    public sealed class ActionBlueprintEventBus : IBlueprintEventBus
    {
        private readonly Action<string> _publish;

        public ActionBlueprintEventBus(Action<string> publish)
        {
            _publish = publish;
        }

        public void Publish(string eventName)
        {
            if (_publish != null)
            {
                _publish(eventName);
            }
        }
    }

    public sealed class UnityBlueprintLogger : IBlueprintLogger
    {
        public void Log(string message)
        {
            Debug.Log("[Blueprint] " + message);
        }

        public void Warning(string message)
        {
            Debug.LogWarning("[Blueprint] " + message);
        }

        public void Error(string message)
        {
            Debug.LogError("[Blueprint] " + message);
        }
    }

    public sealed class RecordingBlueprintLogger : IBlueprintLogger
    {
        public readonly System.Collections.Generic.List<string> Entries = new System.Collections.Generic.List<string>();

        public void Log(string message)
        {
            Entries.Add("Log: " + message);
        }

        public void Warning(string message)
        {
            Entries.Add("Warning: " + message);
        }

        public void Error(string message)
        {
            Entries.Add("Error: " + message);
        }
    }
}
