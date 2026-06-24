using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    [Serializable]
    public sealed class RoadMaskDefinition
    {
        [Range(0, 31)] public int bit;
        public string name = string.Empty;
        public Color color = Color.white;
    }

    [CreateAssetMenu(menuName = "Vehicle Road/Road Network/Network Settings")]
    public sealed class RoadNetworkSettings : ScriptableObject
    {
        [SerializeField] private List<RoadMaskDefinition> tags = new List<RoadMaskDefinition>();
        [SerializeField] private List<RoadMaskDefinition> agents = new List<RoadMaskDefinition>();

        public IReadOnlyList<RoadMaskDefinition> Tags => tags;
        public IReadOnlyList<RoadMaskDefinition> Agents => agents;

        public void InitializeDefaultsIfEmpty()
        {
            if (tags == null || tags.Count == 0)
            {
                tags = CreateDefaults(new[]
                {
                    "Road", "Vehicle", "Pedestrian", "Sidewalk", "Crosswalk", "Parking",
                    "Service", "Restricted", "Indoor", "Outdoor", "Junction", "Connector"
                });
            }

            if (agents == null || agents.Count == 0)
            {
                agents = CreateDefaults(new[]
                {
                    "Car", "Truck", "Bus", "Emergency", "Service", "Bicycle", "Pedestrian"
                });
            }
        }

        private void Reset()
        {
            tags = new List<RoadMaskDefinition>();
            agents = new List<RoadMaskDefinition>();
            InitializeDefaultsIfEmpty();
        }

        private void OnValidate()
        {
            Normalize(tags);
            Normalize(agents);
        }

        private static List<RoadMaskDefinition> CreateDefaults(string[] names)
        {
            List<RoadMaskDefinition> result = new List<RoadMaskDefinition>();
            for (int i = 0; i < names.Length; i++)
            {
                result.Add(new RoadMaskDefinition
                {
                    bit = i,
                    name = names[i],
                    color = Color.HSVToRGB(i / Mathf.Max(1f, names.Length), 0.65f, 1f)
                });
            }

            return result;
        }

        private static void Normalize(List<RoadMaskDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            HashSet<int> usedBits = new HashSet<int>();
            for (int i = 0; i < definitions.Count; i++)
            {
                RoadMaskDefinition definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                definition.bit = Mathf.Clamp(definition.bit, 0, 31);
                definition.name ??= string.Empty;
                if (!usedBits.Add(definition.bit))
                {
                    Debug.LogWarning("RoadNetworkSettings contains duplicate mask bit " + definition.bit + ".");
                }
            }
        }
    }
}
