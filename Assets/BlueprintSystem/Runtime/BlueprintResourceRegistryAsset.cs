using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    [CreateAssetMenu(menuName = "Blueprint System/Resource Registry", fileName = "BlueprintResourceRegistry")]
    public sealed class BlueprintResourceRegistryAsset : ScriptableObject
    {
        [SerializeField] private string schemaVersion = "0.1";
        [SerializeField] private string generatedHash;
        [SerializeField] private int maxConcurrentLoads = 4;
        [SerializeField] private float maxLoadedMemoryMb = 512f;
        [SerializeField] private List<BlueprintResourceRegistryEntry> entries = new List<BlueprintResourceRegistryEntry>();

        private Dictionary<BlueprintPrimaryResourceId, BlueprintResourceRegistryEntry> entriesById;

        public string SchemaVersion
        {
            get { return schemaVersion; }
        }

        public string GeneratedHash
        {
            get { return generatedHash; }
        }

        public int MaxConcurrentLoads
        {
            get { return Mathf.Max(1, maxConcurrentLoads); }
        }

        public float MaxLoadedMemoryMb
        {
            get { return Mathf.Max(0f, maxLoadedMemoryMb); }
        }

        public IReadOnlyList<BlueprintResourceRegistryEntry> Entries
        {
            get { return entries; }
        }

        public bool TryGet(BlueprintPrimaryResourceId id, out BlueprintResourceRegistryEntry entry)
        {
            EnsureLookup();
            return entriesById.TryGetValue(id, out entry);
        }

        public BlueprintResourceRegistryEntry[] GetEntriesInPreloadGroup(string preloadGroup)
        {
            if (string.IsNullOrEmpty(preloadGroup))
            {
                return new BlueprintResourceRegistryEntry[0];
            }

            List<BlueprintResourceRegistryEntry> result = new List<BlueprintResourceRegistryEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                BlueprintResourceRegistryEntry entry = entries[i];
                if (entry == null || entry.PreloadGroups == null)
                {
                    continue;
                }

                for (int p = 0; p < entry.PreloadGroups.Length; p++)
                {
                    if (entry.PreloadGroups[p] == preloadGroup)
                    {
                        result.Add(entry);
                        break;
                    }
                }
            }

            result.Sort(delegate(BlueprintResourceRegistryEntry left, BlueprintResourceRegistryEntry right)
            {
                int priority = right.Priority.CompareTo(left.Priority);
                return priority != 0 ? priority : string.CompareOrdinal(left.Id.ToString(), right.Id.ToString());
            });
            return result.ToArray();
        }

        public void SetGeneratedData(
            string newSchemaVersion,
            string newGeneratedHash,
            IEnumerable<BlueprintResourceRegistryEntry> newEntries,
            int newMaxConcurrentLoads,
            float newMaxLoadedMemoryMb)
        {
            schemaVersion = string.IsNullOrEmpty(newSchemaVersion) ? "0.1" : newSchemaVersion;
            generatedHash = newGeneratedHash;
            maxConcurrentLoads = Mathf.Max(1, newMaxConcurrentLoads);
            maxLoadedMemoryMb = Mathf.Max(0f, newMaxLoadedMemoryMb);
            entries.Clear();
            if (newEntries != null)
            {
                entries.AddRange(newEntries);
            }

            entriesById = null;
        }

        private void EnsureLookup()
        {
            if (entriesById != null)
            {
                return;
            }

            entriesById = new Dictionary<BlueprintPrimaryResourceId, BlueprintResourceRegistryEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                BlueprintResourceRegistryEntry entry = entries[i];
                if (entry == null || !entry.Id.IsValid)
                {
                    continue;
                }

                entriesById[entry.Id] = entry;
            }
        }
    }
}
