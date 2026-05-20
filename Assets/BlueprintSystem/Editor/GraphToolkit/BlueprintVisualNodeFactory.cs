using System;
using System.Collections.Generic;
using UnityEditor;

namespace BlueprintSystem.Editor
{
    internal static class BlueprintVisualNodeFactory
    {
        private static Dictionary<string, Type> _nodeTypesByTypeId;

        public static BlueprintVisualNode Create(string typeId)
        {
            EnsureCache();
            Type nodeType;
            if (!string.IsNullOrEmpty(typeId) && _nodeTypesByTypeId.TryGetValue(typeId, out nodeType))
            {
                return (BlueprintVisualNode)Activator.CreateInstance(nodeType);
            }

            return new BlueprintVisualNode();
        }

        private static void EnsureCache()
        {
            if (_nodeTypesByTypeId != null)
            {
                return;
            }

            _nodeTypesByTypeId = new Dictionary<string, Type>();
            TypeCache.TypeCollection nodeTypes = TypeCache.GetTypesDerivedFrom<BlueprintVisualNode>();
            foreach (Type nodeType in nodeTypes)
            {
                if (nodeType.IsAbstract)
                {
                    continue;
                }

                object[] attributes = nodeType.GetCustomAttributes(typeof(BlueprintVisualNodeTypeAttribute), false);
                if (attributes.Length == 0)
                {
                    continue;
                }

                BlueprintVisualNodeTypeAttribute attribute = (BlueprintVisualNodeTypeAttribute)attributes[0];
                if (!string.IsNullOrEmpty(attribute.BlueprintTypeId))
                {
                    _nodeTypesByTypeId[attribute.BlueprintTypeId] = nodeType;
                }
            }
        }
    }
}
