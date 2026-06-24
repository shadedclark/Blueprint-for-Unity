using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    [Serializable]
    public sealed class RoadLaneProfileEntry
    {
        public string entryId = "lane";
        [Min(0.1f)] public float width = 3.5f;
        public RoadLaneTravelDirection direction = RoadLaneTravelDirection.Forward;
        [Min(0f)] public float speedLimit = 12f;
        public RoadTagMask tags = RoadTagMask.Road | RoadTagMask.Vehicle;
        public RoadAgentMask allowedAgents = RoadAgentMask.MotorVehicles;
        public bool open = true;
        public RoadLaneConnectionMode connectionMode = RoadLaneConnectionMode.Automatic;
        public bool allowLaneChangeLeft = true;
        public bool allowLaneChangeRight = true;
    }

    [CreateAssetMenu(menuName = "Vehicle Road/Road Network/Lane Profile")]
    public sealed class RoadLaneProfile : ScriptableObject
    {
        [SerializeField] private List<RoadLaneProfileEntry> entries = new List<RoadLaneProfileEntry>();

        public IList<RoadLaneProfileEntry> Entries => entries;

        public float TotalWidth
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null)
                    {
                        total += Mathf.Max(0.1f, entries[i].width);
                    }
                }

                return total;
            }
        }

        private void Reset()
        {
            entries = new List<RoadLaneProfileEntry>
            {
                new RoadLaneProfileEntry { entryId = "left", direction = RoadLaneTravelDirection.Reverse },
                new RoadLaneProfileEntry { entryId = "right", direction = RoadLaneTravelDirection.Forward }
            };
        }

        private void OnValidate()
        {
            entries ??= new List<RoadLaneProfileEntry>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                RoadLaneProfileEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                entry.entryId = string.IsNullOrWhiteSpace(entry.entryId) ? "lane_" + i : entry.entryId.Trim();
                entry.width = Mathf.Max(0.1f, entry.width);
                entry.speedLimit = Mathf.Max(0f, entry.speedLimit);
                if (!ids.Add(entry.entryId))
                {
                    Debug.LogWarning("RoadLaneProfile '" + name + "' contains duplicate entryId '" + entry.entryId + "'.", this);
                }
            }
        }
    }
}
