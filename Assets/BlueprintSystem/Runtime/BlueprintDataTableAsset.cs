using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    [Serializable]
    public sealed class BlueprintDataTableAssetRow
    {
        [Tooltip("Unique row key used by DataTable.GetRow.")]
        public string rowName;
        [Tooltip("Row value encoded as JSON matching the selected row struct.")]
        public string valueJson;

        public object ReadValue()
        {
            if (string.IsNullOrEmpty(valueJson))
            {
                return null;
            }

            try
            {
                return BlueprintJson.Deserialize(valueJson);
            }
            catch (BlueprintJsonException)
            {
                return null;
            }
        }
    }

    [CreateAssetMenu(menuName = "Blueprint System/Data Table", fileName = "NewDataTable")]
    public sealed class BlueprintDataTableAsset : ScriptableObject
    {
        [SerializeField, HideInInspector] private string schemaVersion = "0.1";
        [SerializeField, HideInInspector] private string tableId = "Table.NewDataTable";
        [SerializeField, Tooltip("Blueprint user struct type used by every row, such as Struct.ItemRow.")]
        private string rowStructTypeId;
        [SerializeField, Tooltip("Rows stored by this table.")]
        private List<BlueprintDataTableAssetRow> rows = new List<BlueprintDataTableAssetRow>();

        public string SchemaVersion
        {
            get { return schemaVersion; }
            set { schemaVersion = value; }
        }

        public string TableId
        {
            get { return GetDerivedTableId(); }
        }

        public string RowStructTypeId
        {
            get { return rowStructTypeId; }
            set { rowStructTypeId = value; }
        }

        public List<BlueprintDataTableAssetRow> Rows
        {
            get { return rows; }
        }

        public BlueprintDataTableDefinition ToDefinition()
        {
            BlueprintDataTableDefinition definition = new BlueprintDataTableDefinition();
            definition.SchemaVersion = string.IsNullOrEmpty(schemaVersion) ? "0.1" : schemaVersion;
            definition.TableId = TableId;
            definition.RowStructTypeId = rowStructTypeId;

            for (int i = 0; i < rows.Count; i++)
            {
                BlueprintDataTableAssetRow source = rows[i];
                if (source == null)
                {
                    continue;
                }

                definition.Rows.Add(new BlueprintDataTableRowDefinition
                {
                    RowName = source.rowName,
                    Value = source.ReadValue()
                });
            }

            return definition;
        }

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["schemaVersion"] = string.IsNullOrEmpty(schemaVersion) ? "0.1" : schemaVersion;
            data["tableId"] = TableId;
            data["rowStructTypeId"] = rowStructTypeId;

            List<object> rowItems = new List<object>();
            for (int i = 0; i < rows.Count; i++)
            {
                BlueprintDataTableAssetRow source = rows[i];
                if (source == null)
                {
                    continue;
                }

                Dictionary<string, object> item = new Dictionary<string, object>();
                item["rowName"] = source.rowName;
                item["value"] = source.ReadValue();
                rowItems.Add(item);
            }

            data["rows"] = rowItems;
            return data;
        }

        public string ToJson()
        {
            return BlueprintJson.Serialize(ToDictionary(), true);
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(schemaVersion))
            {
                schemaVersion = "0.1";
            }

            RefreshDerivedTableId();
#if UNITY_EDITOR
            BlueprintDataTableRegistry.Refresh();
#endif
        }

        private bool RefreshDerivedTableId()
        {
            string derivedTableId = GetDerivedTableId();
            if (tableId != derivedTableId)
            {
                tableId = derivedTableId;
                return true;
            }

            return false;
        }

        private string GetDerivedTableId()
        {
            string assetName = name;
            if (string.IsNullOrEmpty(assetName))
            {
                assetName = "NewDataTable";
            }

            return "Table." + assetName;
        }
    }
}
