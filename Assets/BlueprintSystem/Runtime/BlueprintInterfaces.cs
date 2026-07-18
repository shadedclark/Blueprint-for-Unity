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

    public interface IBlueprintIndexedVariableStore : IBlueprintVariableStore
    {
        object Get(int variableIndex);
        bool TryGet(int variableIndex, out object value);
        void Set(int variableIndex, object value);
        bool Contains(int variableIndex);
        BlueprintVariableDeclaration GetDeclaration(int variableIndex);
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

    public sealed class DictionaryBlueprintVariableStore : IBlueprintIndexedVariableStore
    {
        private sealed class VariableValueRecord
        {
            public int NameStableId;
            public int DeclarationStableId;
            public string Name;
            public BlueprintVariableDeclaration Declaration;
            public CompiledStructLayout Layout;
            public object Value;
            public object InitialValue;
            public bool Dirty;
        }

        private readonly List<VariableValueRecord> _records = new List<VariableValueRecord>();

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

                _records.Add(new VariableValueRecord
                {
                    NameStableId = BlueprintStableId.FromString(variable.Name),
                    DeclarationStableId = BlueprintStableId.FromString(variable.Id),
                    Name = variable.Name,
                    Declaration = variable,
                    Layout = blueprint.GetStructLayout(variable.CompiledLayoutConstantIndex)
                });
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
            return TryGet(name, out value) ? value : null;
        }

        public bool TryGet(string name, out object value)
        {
            int index = FindByName(name);
            return TryGet(index, out value);
        }

        public bool TryGetInitial(string name, out object value)
        {
            int index = FindByName(name);
            if (index >= 0)
            {
                value = _records[index].InitialValue;
                return true;
            }
            value = null;
            return false;
        }

        public bool IsDirty(string name)
        {
            int index = FindByName(name);
            return index >= 0 && _records[index].Dirty;
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
            int index = FindByName(name);
            if (index < 0)
            {
                _records.Add(new VariableValueRecord
                {
                    NameStableId = BlueprintStableId.FromString(name),
                    Name = name,
                    Value = value,
                    InitialValue = null,
                    Dirty = dirty
                });
                return;
            }

            SetValue(index, value, dirty);
        }

        public bool Contains(string name)
        {
            return FindByName(name) >= 0;
        }

        public void ResetToDefaults()
        {
            for (int i = 0; i < _records.Count; i++)
            {
                VariableValueRecord record = _records[i];
                record.Value = CloneValueForVariable(i, record.InitialValue);
                record.Dirty = false;
            }
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
            for (int i = 0; i < _records.Count; i++)
            {
                BlueprintVariableDeclaration variable = _records[i].Declaration;
                _records[i].InitialValue = variable == null
                    ? null
                    : CoerceValue(variable.DefaultValue, variable.Type, null, _records[i].Layout);
            }
        }

        private void CaptureInitialValuesFromCurrentValues()
        {
            for (int i = 0; i < _records.Count; i++)
            {
                _records[i].InitialValue = CloneValueForVariable(i, _records[i].Value);
                _records[i].Dirty = false;
            }
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

            int index = FindByDeclarationId(variableOverride.VariableId);
            if (index >= 0)
            {
                declaration = _records[index].Declaration;
                variableName = declaration.Name;
                return true;
            }

            index = FindByName(variableOverride.Name);
            if (index >= 0)
            {
                declaration = _records[index].Declaration;
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

        private static object CoerceValue(object value, string type, object defaultValue, CompiledStructLayout layout)
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
                    if (layout != null)
                    {
                        string elementType;
                        if (BlueprintArrayUtility.TryGetElementType(type, out elementType))
                        {
                            System.Collections.IList source = BlueprintArrayUtility.ReadList(value);
                            if (source == null) return defaultValue;
                            List<object> records = new List<object>(source.Count);
                            for (int i = 0; i < source.Count; i++)
                            {
                                object record;
                                if (!BlueprintUserStructUtility.TryConvertToRuntimeValue(source[i], layout, out record)) return defaultValue;
                                records.Add(record);
                            }
                            return records;
                        }

                        object recordValue;
                        if (BlueprintUserStructUtility.TryConvertToRuntimeValue(value, layout, out recordValue)) return recordValue;
                    }

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

        private object CloneValueForVariable(int index, object value)
        {
            BlueprintVariableDeclaration declaration = GetDeclaration(index);
            if (declaration == null)
            {
                return value;
            }

            if (_records[index].Layout != null)
            {
                return CoerceValue(value, declaration.Type, declaration.DefaultValue, _records[index].Layout);
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

            return CoerceValue(value, declaration.Type, declaration.DefaultValue, _records[index].Layout);
        }

        public object Get(int variableIndex)
        {
            object value;
            return TryGet(variableIndex, out value) ? value : null;
        }

        public bool TryGet(int variableIndex, out object value)
        {
            if (variableIndex >= 0 && variableIndex < _records.Count)
            {
                value = _records[variableIndex].Value;
                return true;
            }
            value = null;
            return false;
        }

        public void Set(int variableIndex, object value)
        {
            SetValue(variableIndex, value, true);
        }

        public bool Contains(int variableIndex)
        {
            return variableIndex >= 0 && variableIndex < _records.Count;
        }

        public BlueprintVariableDeclaration GetDeclaration(int variableIndex)
        {
            return variableIndex >= 0 && variableIndex < _records.Count ? _records[variableIndex].Declaration : null;
        }

        private void SetValue(int index, object value, bool dirty)
        {
            if (index < 0 || index >= _records.Count) return;
            VariableValueRecord record = _records[index];
            BlueprintVariableDeclaration declaration = record.Declaration;
            if (declaration != null) value = CoerceValue(value, declaration.Type, declaration.DefaultValue, record.Layout);
            record.Value = value;
            record.Dirty = dirty;
        }

        private int FindByName(string name)
        {
            int stableId = BlueprintStableId.FromString(name);
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].NameStableId == stableId && string.Equals(_records[i].Name, name, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private int FindByDeclarationId(string declarationId)
        {
            if (string.IsNullOrEmpty(declarationId)) return -1;
            int stableId = BlueprintStableId.FromString(declarationId);
            for (int i = 0; i < _records.Count; i++)
            {
                BlueprintVariableDeclaration declaration = _records[i].Declaration;
                if (_records[i].DeclarationStableId == stableId && declaration != null && string.Equals(declaration.Id, declarationId, StringComparison.Ordinal)) return i;
            }
            return -1;
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
            if (!BlueprintLog.DebugEnabled)
            {
                return;
            }

            BlueprintLog.Log("[Blueprint] " + message);
        }

        public void Warning(string message)
        {
            if (!BlueprintLog.DebugEnabled)
            {
                return;
            }

            BlueprintLog.Warning("[Blueprint] " + message);
        }

        public void Error(string message)
        {
            BlueprintLog.Error("[Blueprint] " + message);
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
