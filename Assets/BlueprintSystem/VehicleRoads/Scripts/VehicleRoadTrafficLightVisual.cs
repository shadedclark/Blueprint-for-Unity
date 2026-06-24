using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleRoads
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vehicle Road/Vehicle Road Traffic Light Visual")]
    public sealed class VehicleRoadTrafficLightVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private VehicleRoadSubsystem roadSubsystem;
        [SerializeField] private string junctionId = string.Empty;
        [SerializeField] private RoadLaneTurn controlledTurn = RoadLaneTurn.Straight;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField, Min(0f)] private float inactiveBrightness = 0.08f;
        [SerializeField, Min(0f)] private float activeBrightness = 1.35f;
        [SerializeField, Min(0f)] private float activeEmission = 3f;

        private readonly List<SignalMaterialSlot> slots = new List<SignalMaterialSlot>();
        private MaterialPropertyBlock propertyBlock;
        private VehicleRoadSignalState displayedState = (VehicleRoadSignalState)(-1);

        public VehicleRoadSubsystem RoadSubsystem
        {
            get => roadSubsystem;
            set => roadSubsystem = value;
        }

        public string JunctionId
        {
            get => junctionId;
            set => junctionId = value ?? string.Empty;
        }

        public RoadLaneTurn ControlledTurn
        {
            get => controlledTurn;
            set => controlledTurn = value;
        }

        public Renderer TargetRenderer
        {
            get => targetRenderer;
            set
            {
                targetRenderer = value;
                CacheSlots();
            }
        }

        private void Awake()
        {
            CacheSlots();
            ApplyState(VehicleRoadSignalState.Red);
        }

        private void Update()
        {
            VehicleRoadSignalState state = VehicleRoadSignalState.Red;
            if (roadSubsystem != null &&
                roadSubsystem.TryGetJunctionSignalState(junctionId, controlledTurn, out VehicleRoadSignalState queried))
            {
                state = queried;
            }

            ApplyState(state);
        }

        private void OnDisable()
        {
            if (targetRenderer == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                targetRenderer.SetPropertyBlock(null, slots[i].materialIndex);
            }

            displayedState = (VehicleRoadSignalState)(-1);
        }

        private void OnValidate()
        {
            junctionId ??= string.Empty;
            inactiveBrightness = Mathf.Max(0f, inactiveBrightness);
            activeBrightness = Mathf.Max(0f, activeBrightness);
            activeEmission = Mathf.Max(0f, activeEmission);
        }

        private void CacheSlots()
        {
            slots.Clear();
            displayedState = (VehicleRoadSignalState)(-1);
            propertyBlock ??= new MaterialPropertyBlock();
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>(true);
            }

            if (targetRenderer == null)
            {
                return;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || !TryGetSignalState(material.name, out VehicleRoadSignalState state))
                {
                    continue;
                }

                Color baseColor = material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId)
                    : material.HasProperty(ColorId)
                        ? material.GetColor(ColorId)
                        : Color.white;
                slots.Add(new SignalMaterialSlot(i, state, baseColor));
            }
        }

        private void ApplyState(VehicleRoadSignalState state)
        {
            if (targetRenderer == null || slots.Count == 0)
            {
                CacheSlots();
            }

            if (targetRenderer == null || displayedState == state)
            {
                return;
            }

            displayedState = state;
            for (int i = 0; i < slots.Count; i++)
            {
                SignalMaterialSlot slot = slots[i];
                bool active = slot.state == state;
                Color color = slot.baseColor * (active ? activeBrightness : inactiveBrightness);
                color.a = slot.baseColor.a;
                Color emission = active ? slot.baseColor * activeEmission : Color.black;

                targetRenderer.GetPropertyBlock(propertyBlock, slot.materialIndex);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(ColorId, color);
                propertyBlock.SetColor(EmissiveColorId, emission);
                propertyBlock.SetColor(EmissionColorId, emission);
                targetRenderer.SetPropertyBlock(propertyBlock, slot.materialIndex);
                propertyBlock.Clear();
            }
        }

        private static bool TryGetSignalState(string materialName, out VehicleRoadSignalState state)
        {
            materialName ??= string.Empty;
            if (materialName.IndexOf("red", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                state = VehicleRoadSignalState.Red;
                return true;
            }

            if (materialName.IndexOf("yellow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                state = VehicleRoadSignalState.Yellow;
                return true;
            }

            if (materialName.IndexOf("green", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                state = VehicleRoadSignalState.Green;
                return true;
            }

            state = VehicleRoadSignalState.None;
            return false;
        }

        private readonly struct SignalMaterialSlot
        {
            public readonly int materialIndex;
            public readonly VehicleRoadSignalState state;
            public readonly Color baseColor;

            public SignalMaterialSlot(int materialIndex, VehicleRoadSignalState state, Color baseColor)
            {
                this.materialIndex = materialIndex;
                this.state = state;
                this.baseColor = baseColor;
            }
        }
    }
}
