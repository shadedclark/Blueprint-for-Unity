using System.Collections;
using System.Collections.Generic;

namespace BlueprintSystem
{
    internal static class BlueprintManifestMapper
    {
        public static BlueprintNodeManifest FromDictionary(Dictionary<string, object> data)
        {
            BlueprintNodeManifest manifest = new BlueprintNodeManifest();
            manifest.SchemaVersion = BlueprintSourceMapper.GetString(data, "schemaVersion");
            manifest.TypeId = BlueprintSourceMapper.GetString(data, "typeId");
            manifest.Title = BlueprintSourceMapper.GetString(data, "title");
            manifest.Category = BlueprintSourceMapper.GetString(data, "category");
            manifest.Description = BlueprintSourceMapper.GetString(data, "description");
            manifest.Executor = BlueprintSourceMapper.GetString(data, "executor");

            foreach (Dictionary<string, object> item in BlueprintSourceMapper.GetObjectArray(data, "inputs"))
            {
                manifest.Inputs.Add(ReadPort(item, BlueprintPortDirection.Input));
            }

            foreach (Dictionary<string, object> item in BlueprintSourceMapper.GetObjectArray(data, "outputs"))
            {
                manifest.Outputs.Add(ReadPort(item, BlueprintPortDirection.Output));
            }

            foreach (Dictionary<string, object> item in BlueprintSourceMapper.GetObjectArray(data, "properties"))
            {
                BlueprintPropertySpec property = new BlueprintPropertySpec();
                property.Id = BlueprintSourceMapper.GetString(item, "id");
                property.Type = BlueprintSourceMapper.GetString(item, "type");
                property.Required = BlueprintSourceMapper.GetBool(item, "required", false);
                item.TryGetValue("defaultValue", out property.DefaultValue);
                manifest.Properties.Add(property);
            }

            object tagsValue;
            if (data.TryGetValue("tags", out tagsValue))
            {
                IEnumerable tags = tagsValue as IEnumerable;
                if (tags != null && !(tagsValue is string))
                {
                    foreach (object tag in tags)
                    {
                        if (tag != null)
                        {
                            manifest.Tags.Add(tag.ToString());
                        }
                    }
                }
            }

            return manifest;
        }

        private static BlueprintPortSpec ReadPort(Dictionary<string, object> data, BlueprintPortDirection direction)
        {
            BlueprintPortSpec port = new BlueprintPortSpec();
            port.Id = BlueprintSourceMapper.GetString(data, "id");
            port.Kind = ParseKind(BlueprintSourceMapper.GetString(data, "kind"));
            port.Direction = direction;
            port.Type = BlueprintSourceMapper.GetString(data, "type");
            port.Required = BlueprintSourceMapper.GetBool(data, "required", false);
            port.Source = ParseSource(BlueprintSourceMapper.GetString(data, "source"));
            port.AllowMultiple = BlueprintSourceMapper.GetBool(data, "allowMultiple", false);
            return port;
        }

        private static BlueprintPortKind ParseKind(string value)
        {
            return value == "exec" ? BlueprintPortKind.Exec : BlueprintPortKind.Value;
        }

        private static BlueprintValueSource ParseSource(string value)
        {
            switch (value)
            {
                case "property":
                    return BlueprintValueSource.Property;
                case "connection":
                    return BlueprintValueSource.Connection;
                case "propertyOrConnection":
                    return BlueprintValueSource.PropertyOrConnection;
                default:
                    return BlueprintValueSource.None;
            }
        }
    }
}
