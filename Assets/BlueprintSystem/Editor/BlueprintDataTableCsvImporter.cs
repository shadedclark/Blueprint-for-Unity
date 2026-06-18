using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace BlueprintSystem.Editor
{
    internal sealed class BlueprintDataTableCsvImportResult
    {
        public string CsvPath;
        public string OutputFolderPath;
        public string StructAssetPath;
        public string StructJsonPath;
        public string TableAssetPath;
        public string TableJsonPath;
        public BlueprintUserStructAsset StructAsset;
        public BlueprintDataTableAsset TableAsset;
        public int FieldCount;
        public int RowCount;
    }

    internal static class BlueprintDataTableCsvImporter
    {
        private const string SchemaVersion = "0.1";

        public static bool TryGetConflicts(
            string csvFilePath,
            string outputFolderPath,
            out List<string> conflicts,
            out string error)
        {
            conflicts = new List<string>();
            error = null;

            CsvImportPlan plan;
            if (!TryBuildPlan(csvFilePath, outputFolderPath, out plan, out error))
            {
                return false;
            }

            if (!ValidateExistingAssetTypes(plan, out error))
            {
                return false;
            }

            CollectConflicts(plan, conflicts);
            return true;
        }

        public static bool ImportFromCsv(
            string csvFilePath,
            string outputFolderPath,
            bool overwrite,
            out BlueprintDataTableCsvImportResult result,
            out string error)
        {
            result = null;
            error = null;

            CsvImportPlan plan;
            if (!TryBuildPlan(csvFilePath, outputFolderPath, out plan, out error))
            {
                return false;
            }

            if (!ValidateExistingAssetTypes(plan, out error))
            {
                return false;
            }

            List<string> conflicts = new List<string>();
            CollectConflicts(plan, conflicts);
            if (conflicts.Count > 0 && !overwrite)
            {
                error = "CSV import target already exists: " + string.Join(", ", conflicts.ToArray());
                return false;
            }

            try
            {
                EnsureAssetFolder(Path.GetDirectoryName(plan.StructAssetPath));
                EnsureAssetFolder(Path.GetDirectoryName(plan.TableAssetPath));

                BlueprintUserStructAsset structAsset = WriteStructAsset(plan);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(plan.StructAssetPath);
                BlueprintUserStructAssetEditor.ExportJson(structAsset);
                BlueprintRuntimeRegistryAssetManagerUtility.SyncAll(false);

                BlueprintDataTableAsset tableAsset = WriteDataTableAsset(plan);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(plan.TableAssetPath);
                BlueprintDataTableAssetEditor.ExportJson(tableAsset);
                BlueprintRuntimeRegistryAssetManagerUtility.SyncAll(false);

                result = new BlueprintDataTableCsvImportResult
                {
                    CsvPath = plan.CsvPath,
                    OutputFolderPath = plan.OutputFolderPath,
                    StructAssetPath = plan.StructAssetPath,
                    StructJsonPath = plan.StructJsonPath,
                    TableAssetPath = plan.TableAssetPath,
                    TableJsonPath = plan.TableJsonPath,
                    StructAsset = structAsset,
                    TableAsset = tableAsset,
                    FieldCount = plan.Fields.Count,
                    RowCount = plan.Rows.Count
                };
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryBuildPlan(
            string csvFilePath,
            string outputFolderPath,
            out CsvImportPlan plan,
            out string error)
        {
            plan = null;
            error = null;

            if (string.IsNullOrEmpty(csvFilePath))
            {
                error = "CSV file path is required.";
                return false;
            }

            if (!File.Exists(csvFilePath))
            {
                error = "CSV file does not exist: " + csvFilePath;
                return false;
            }

            string outputAssetFolder;
            if (!TryNormalizeOutputFolder(outputFolderPath, out outputAssetFolder, out error))
            {
                return false;
            }

            List<List<string>> records;
            if (!TryParseCsv(File.ReadAllText(csvFilePath), out records, out error))
            {
                return false;
            }

            if (records.Count < 3)
            {
                error = "CSV must contain a field-name row, a field-type row, and at least one data row.";
                return false;
            }

            List<string> fieldNames = records[0];
            List<string> fieldTypes = records[1];
            if (fieldNames.Count == 0)
            {
                error = "CSV field-name row is empty.";
                return false;
            }

            if (fieldTypes.Count != fieldNames.Count)
            {
                error = "CSV field-type row must have the same column count as the field-name row.";
                return false;
            }

            string csvName = SanitizeAssetName(Path.GetFileNameWithoutExtension(csvFilePath));
            string structAssetName = csvName + "Row";
            string tableAssetName = csvName;
            string structFolder = CombineAssetPath(outputAssetFolder, "Structs");
            string tableFolder = CombineAssetPath(outputAssetFolder, "Tables");

            plan = new CsvImportPlan();
            plan.CsvPath = csvFilePath;
            plan.OutputFolderPath = outputAssetFolder;
            plan.StructAssetName = structAssetName;
            plan.TableAssetName = tableAssetName;
            plan.StructAssetPath = CombineAssetPath(structFolder, structAssetName + ".asset");
            plan.StructJsonPath = BlueprintAssetDiscovery.ChangeAssetPathExtension(
                plan.StructAssetPath,
                BlueprintUserStructRegistry.StructAssetExtension);
            plan.TableAssetPath = CombineAssetPath(tableFolder, tableAssetName + ".asset");
            plan.TableJsonPath = BlueprintDataTableRegistry.GetJsonPathForAssetPath(plan.TableAssetPath);
            plan.RowStructTypeId = "Struct." + structAssetName;

            HashSet<string> fieldNameSet = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> fieldIdSet = new HashSet<string>(StringComparer.Ordinal);
            for (int column = 0; column < fieldNames.Count; column++)
            {
                string fieldName = (fieldNames[column] ?? string.Empty).Trim();
                string fieldType = (fieldTypes[column] ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(fieldName))
                {
                    error = "CSV column " + (column + 1).ToString(CultureInfo.InvariantCulture) + " has an empty field name.";
                    return false;
                }

                if (!fieldNameSet.Add(fieldName))
                {
                    error = "CSV field name '" + fieldName + "' is duplicated.";
                    return false;
                }

                BlueprintUserStructAssetFieldType assetFieldType;
                if (string.IsNullOrEmpty(fieldType) ||
                    !BlueprintUserStructAssetFieldTypes.TryFromTypeId(fieldType, out assetFieldType))
                {
                    error = "CSV field '" + fieldName + "' has unsupported type '" + fieldType + "'.";
                    return false;
                }

                plan.Fields.Add(new CsvFieldPlan
                {
                    Name = fieldName,
                    TypeId = BlueprintUserStructAssetFieldTypes.ToTypeId(assetFieldType),
                    AssetFieldType = assetFieldType,
                    Id = CreateFieldId(fieldName, fieldIdSet)
                });
            }

            HashSet<string> rowNames = new HashSet<string>(StringComparer.Ordinal);
            for (int recordIndex = 2; recordIndex < records.Count; recordIndex++)
            {
                List<string> record = records[recordIndex];
                int csvRowNumber = recordIndex + 1;
                if (record.Count != plan.Fields.Count)
                {
                    error = "CSV row " + csvRowNumber.ToString(CultureInfo.InvariantCulture) +
                            " has " + record.Count.ToString(CultureInfo.InvariantCulture) +
                            " columns; expected " + plan.Fields.Count.ToString(CultureInfo.InvariantCulture) + ".";
                    return false;
                }

                string rowName = record[0] ?? string.Empty;
                if (string.IsNullOrEmpty(rowName))
                {
                    error = "CSV row " + csvRowNumber.ToString(CultureInfo.InvariantCulture) + " has an empty first-column rowName.";
                    return false;
                }

                if (!rowNames.Add(rowName))
                {
                    error = "CSV rowName '" + rowName + "' is duplicated.";
                    return false;
                }

                Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
                for (int column = 0; column < plan.Fields.Count; column++)
                {
                    CsvFieldPlan field = plan.Fields[column];
                    object value;
                    if (!TryConvertCellValue(record[column], field.TypeId, csvRowNumber, field.Name, out value, out error))
                    {
                        return false;
                    }

                    values[field.Name] = value;
                }

                plan.Rows.Add(new CsvRowPlan
                {
                    RowName = rowName,
                    Values = values
                });
            }

            return true;
        }

        private static bool TryConvertCellValue(
            string cell,
            string typeId,
            int csvRowNumber,
            string fieldName,
            out object value,
            out string error)
        {
            value = null;
            error = null;
            string text = cell ?? string.Empty;

            if (typeId == "string" || typeId == BlueprintVariableTypeRegistry.BlueprintAssetTypeId)
            {
                value = text;
                return true;
            }

            if (string.IsNullOrEmpty(text))
            {
                value = null;
                return true;
            }

            switch (typeId)
            {
                case "bool":
                    bool boolValue;
                    if (bool.TryParse(text, out boolValue))
                    {
                        value = boolValue;
                        return true;
                    }

                    if (text == "1" || text == "0")
                    {
                        value = text == "1";
                        return true;
                    }

                    return FailCell(csvRowNumber, fieldName, typeId, text, out error);
                case "int":
                    int intValue;
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                    {
                        value = intValue;
                        return true;
                    }

                    return FailCell(csvRowNumber, fieldName, typeId, text, out error);
                case "float":
                    float floatValue;
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue))
                    {
                        value = floatValue;
                        return true;
                    }

                    return FailCell(csvRowNumber, fieldName, typeId, text, out error);
                case "Vector2":
                case "Vector3":
                case "Vector4":
                case "Color":
                case "Rect":
                    return TryConvertJsonCellValue(text, typeId, csvRowNumber, fieldName, out value, out error);
                default:
                    if (BlueprintTypeUtility.IsValueAssignableToType(text, typeId))
                    {
                        value = text;
                        return true;
                    }

                    return FailCell(csvRowNumber, fieldName, typeId, text, out error);
            }
        }

        private static bool TryConvertJsonCellValue(
            string text,
            string typeId,
            int csvRowNumber,
            string fieldName,
            out object value,
            out string error)
        {
            value = null;
            error = null;

            object rawValue;
            try
            {
                rawValue = BlueprintJson.Deserialize(text);
            }
            catch (BlueprintJsonException exception)
            {
                error = "CSV row " + csvRowNumber.ToString(CultureInfo.InvariantCulture) +
                        " column '" + fieldName + "' must contain JSON for type '" + typeId + "': " + exception.Message;
                return false;
            }

            object normalizedValue;
            if (!BlueprintDataTableAssetEditor.TryNormalizeJsonValue(rawValue, typeId, out normalizedValue) ||
                !BlueprintTypeUtility.IsValueAssignableToType(normalizedValue, typeId))
            {
                return FailCell(csvRowNumber, fieldName, typeId, text, out error);
            }

            value = normalizedValue;
            return true;
        }

        private static bool FailCell(int csvRowNumber, string fieldName, string typeId, string text, out string error)
        {
            error = "CSV row " + csvRowNumber.ToString(CultureInfo.InvariantCulture) +
                    " column '" + fieldName + "' value '" + text + "' is not assignable to " + typeId + ".";
            return false;
        }

        private static BlueprintUserStructAsset WriteStructAsset(CsvImportPlan plan)
        {
            BlueprintUserStructAsset asset = AssetDatabase.LoadAssetAtPath<BlueprintUserStructAsset>(plan.StructAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BlueprintUserStructAsset>();
                asset.name = plan.StructAssetName;
                AssetDatabase.CreateAsset(asset, plan.StructAssetPath);
            }
            else
            {
                Undo.RecordObject(asset, "Import CSV User Struct");
                asset.name = plan.StructAssetName;
            }

            asset.SchemaVersion = SchemaVersion;
            asset.Fields.Clear();
            for (int i = 0; i < plan.Fields.Count; i++)
            {
                CsvFieldPlan field = plan.Fields[i];
                asset.Fields.Add(new BlueprintUserStructAssetField
                {
                    id = field.Id,
                    name = field.Name,
                    fieldType = field.AssetFieldType
                });
            }

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static BlueprintDataTableAsset WriteDataTableAsset(CsvImportPlan plan)
        {
            BlueprintDataTableAsset asset = AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(plan.TableAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BlueprintDataTableAsset>();
                asset.name = plan.TableAssetName;
                AssetDatabase.CreateAsset(asset, plan.TableAssetPath);
            }
            else
            {
                Undo.RecordObject(asset, "Import CSV Data Table");
                asset.name = plan.TableAssetName;
            }

            asset.SchemaVersion = SchemaVersion;
            asset.RowStructTypeId = plan.RowStructTypeId;
            asset.Rows.Clear();
            for (int i = 0; i < plan.Rows.Count; i++)
            {
                CsvRowPlan row = plan.Rows[i];
                asset.Rows.Add(new BlueprintDataTableAssetRow
                {
                    rowName = row.RowName,
                    valueJson = BlueprintJson.Serialize(row.Values, false)
                });
            }

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static bool ValidateExistingAssetTypes(CsvImportPlan plan, out string error)
        {
            error = null;
            UnityObject structObject = AssetDatabase.LoadAssetAtPath<UnityObject>(plan.StructAssetPath);
            if (structObject != null && !(structObject is BlueprintUserStructAsset))
            {
                error = "Existing target is not a BlueprintUserStructAsset: " + plan.StructAssetPath;
                return false;
            }

            UnityObject tableObject = AssetDatabase.LoadAssetAtPath<UnityObject>(plan.TableAssetPath);
            if (tableObject != null && !(tableObject is BlueprintDataTableAsset))
            {
                error = "Existing target is not a BlueprintDataTableAsset: " + plan.TableAssetPath;
                return false;
            }

            return true;
        }

        private static void CollectConflicts(CsvImportPlan plan, List<string> conflicts)
        {
            AddConflictIfExists(plan.StructAssetPath, conflicts);
            AddConflictIfExists(plan.StructJsonPath, conflicts);
            AddConflictIfExists(plan.TableAssetPath, conflicts);
            AddConflictIfExists(plan.TableJsonPath, conflicts);
        }

        private static void AddConflictIfExists(string assetPath, List<string> conflicts)
        {
            if (string.IsNullOrEmpty(assetPath) || conflicts == null)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<UnityObject>(assetPath) != null || File.Exists(assetPath))
            {
                conflicts.Add(assetPath);
            }
        }

        private static void EnsureAssetFolder(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath) || Directory.Exists(assetFolderPath))
            {
                return;
            }

            Directory.CreateDirectory(assetFolderPath);
            AssetDatabase.ImportAsset(assetFolderPath);
        }

        private static bool TryNormalizeOutputFolder(string folderPath, out string assetFolderPath, out string error)
        {
            assetFolderPath = null;
            error = null;
            if (string.IsNullOrEmpty(folderPath))
            {
                error = "Output folder is required.";
                return false;
            }

            string normalized = BlueprintAssetDiscovery.NormalizeAssetPath(folderPath);
            if (string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                assetFolderPath = normalized;
                return true;
            }

            string absoluteFolder = Path.GetFullPath(folderPath).Replace('\\', '/').TrimEnd('/');
            string absoluteAssets = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
            if (string.Equals(absoluteFolder, absoluteAssets, StringComparison.OrdinalIgnoreCase))
            {
                assetFolderPath = "Assets";
                return true;
            }

            if (absoluteFolder.StartsWith(absoluteAssets + "/", StringComparison.OrdinalIgnoreCase))
            {
                assetFolderPath = "Assets" + absoluteFolder.Substring(absoluteAssets.Length);
                assetFolderPath = BlueprintAssetDiscovery.NormalizeAssetPath(assetFolderPath);
                return true;
            }

            error = "Output folder must be inside this project's Assets folder.";
            return false;
        }

        private static bool TryParseCsv(string csvText, out List<List<string>> records, out string error)
        {
            records = new List<List<string>>();
            error = null;

            List<string> record = new List<string>();
            StringBuilder field = new StringBuilder();
            bool inQuotes = false;
            bool quotedField = false;
            int line = 1;

            for (int i = 0; i < csvText.Length; i++)
            {
                char c = csvText[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csvText.Length && csvText[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }

                    continue;
                }

                if (c == ',')
                {
                    AddField(record, field, ref quotedField);
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n')
                    {
                        i++;
                    }

                    AddField(record, field, ref quotedField);
                    AddRecord(records, record);
                    line++;
                    continue;
                }

                if (c == '"')
                {
                    if (field.Length > 0)
                    {
                        error = "Unexpected quote in CSV line " + line.ToString(CultureInfo.InvariantCulture) + ".";
                        return false;
                    }

                    inQuotes = true;
                    quotedField = true;
                    continue;
                }

                field.Append(c);
            }

            if (inQuotes)
            {
                error = "CSV has an unterminated quoted field.";
                return false;
            }

            if (field.Length > 0 || quotedField || record.Count > 0)
            {
                AddField(record, field, ref quotedField);
                AddRecord(records, record);
            }

            return true;
        }

        private static void AddField(List<string> record, StringBuilder field, ref bool quotedField)
        {
            record.Add(field.ToString());
            field.Length = 0;
            quotedField = false;
        }

        private static void AddRecord(List<List<string>> records, List<string> record)
        {
            if (!IsBlankRecord(record))
            {
                records.Add(new List<string>(record));
            }

            record.Clear();
        }

        private static bool IsBlankRecord(List<string> record)
        {
            if (record == null || record.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < record.Count; i++)
            {
                if (!string.IsNullOrEmpty(record[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string CombineAssetPath(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
            {
                return right;
            }

            if (string.IsNullOrEmpty(right))
            {
                return left;
            }

            return left.TrimEnd('/') + "/" + right.TrimStart('/');
        }

        private static string SanitizeAssetName(string name)
        {
            string fallback = "ImportedDataTable";
            if (string.IsNullOrEmpty(name))
            {
                return fallback;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    builder.Append(c);
                }
                else if (c == '-' || char.IsWhiteSpace(c))
                {
                    builder.Append('_');
                }
            }

            string result = builder.ToString().Trim('_');
            if (string.IsNullOrEmpty(result))
            {
                return fallback;
            }

            if (!char.IsLetter(result[0]) && result[0] != '_')
            {
                result = "Csv" + result;
            }

            return result;
        }

        private static string CreateFieldId(string fieldName, HashSet<string> usedIds)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < fieldName.Length; i++)
            {
                char c = fieldName[i];
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    builder.Append(c);
                }
                else if (c == '-' || char.IsWhiteSpace(c))
                {
                    builder.Append('_');
                }
            }

            string baseId = builder.ToString().Trim('_');
            if (string.IsNullOrEmpty(baseId))
            {
                baseId = "field";
            }

            if (!char.IsLetter(baseId[0]) && baseId[0] != '_')
            {
                baseId = "fld_" + baseId;
            }

            string id = baseId;
            int suffix = 2;
            while (usedIds != null && !usedIds.Add(id))
            {
                id = baseId + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }

            return id;
        }

        private sealed class CsvImportPlan
        {
            public string CsvPath;
            public string OutputFolderPath;
            public string StructAssetName;
            public string TableAssetName;
            public string StructAssetPath;
            public string StructJsonPath;
            public string TableAssetPath;
            public string TableJsonPath;
            public string RowStructTypeId;
            public readonly List<CsvFieldPlan> Fields = new List<CsvFieldPlan>();
            public readonly List<CsvRowPlan> Rows = new List<CsvRowPlan>();
        }

        private sealed class CsvFieldPlan
        {
            public string Id;
            public string Name;
            public string TypeId;
            public BlueprintUserStructAssetFieldType AssetFieldType;
        }

        private sealed class CsvRowPlan
        {
            public string RowName;
            public Dictionary<string, object> Values;
        }
    }
}
