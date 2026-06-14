using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    [CreateAssetMenu(menuName = "Blueprint System/Resource Type Catalog", fileName = "BlueprintResourceTypeCatalog")]
    public sealed class BlueprintResourceTypeCatalogAsset : ScriptableObject
    {
        [SerializeField] private List<BlueprintResourceTypeDefinition> resourceTypes =
            new List<BlueprintResourceTypeDefinition>();

        public List<BlueprintResourceTypeDefinition> ResourceTypes
        {
            get { return resourceTypes; }
        }
    }
}
