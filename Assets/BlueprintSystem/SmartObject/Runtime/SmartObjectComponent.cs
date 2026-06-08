using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace BlueprintSystem
{
    [AddComponentMenu("Blueprint System/Smart Object")]
    [DisallowMultipleComponent]
    public sealed class SmartObjectComponent : MonoBehaviour
    {
        [SerializeField, HideInInspector] private string objectId;
        [SerializeField] private bool smartObjectEnabled = true;
        [SerializeField] private float objectBaseScore;
        [SerializeField, Tooltip("Comma-separated tags contributed by this object.")]
        private string tags;
        [SerializeField, Tooltip("Optional access group required by default for this object.")]
        private string accessGroup;
        [SerializeField] private List<SmartObjectSlot> slots = new List<SmartObjectSlot>();

        public string ObjectId
        {
            get
            {
                EnsureObjectId(false);
                return objectId;
            }
        }

        public bool SmartObjectEnabled
        {
            get { return smartObjectEnabled; }
            set
            {
                if (smartObjectEnabled == value)
                {
                    return;
                }

                smartObjectEnabled = value;
                if (!smartObjectEnabled)
                {
                    SmartObjectRegistry.ReleaseAllForObject(this, SmartObjectReleaseReason.Disabled);
                }
            }
        }

        public float ObjectBaseScore
        {
            get { return objectBaseScore; }
            set { objectBaseScore = value; }
        }

        public string Tags
        {
            get { return tags; }
            set { tags = value; }
        }

        public string AccessGroup
        {
            get { return accessGroup; }
            set { accessGroup = value; }
        }

        public List<SmartObjectSlot> Slots
        {
            get { return slots; }
        }

        public int RegistrationOrder
        {
            get;
            internal set;
        }

        public SmartObjectSlot FindSlot(int slotId)
        {
            if (slots == null)
            {
                return null;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                SmartObjectSlot slot = slots[i];
                if (slot != null && slot.SlotId == slotId)
                {
                    return slot;
                }
            }

            return null;
        }

        private void Reset()
        {
            EnsureObjectId(true);
        }

        private void Awake()
        {
            EnsureObjectId(true);
        }

        private void OnValidate()
        {
            EnsureObjectId(true);
#if UNITY_EDITOR
            EnsureUniqueObjectIdInEditor();
#endif
        }

        private void OnEnable()
        {
            EnsureObjectId(true);
            SmartObjectRegistry.Register(this);
        }

        private void OnDisable()
        {
            SmartObjectRegistry.Unregister(this, SmartObjectReleaseReason.Disabled);
        }

        private void Update()
        {
            SmartObjectRegistry.TickTimeouts();
        }

        private void RefreshRegistration()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            SmartObjectRegistry.Unregister(this, null);
            SmartObjectRegistry.Register(this);
        }

        private void EnsureObjectId(bool refreshRegistration)
        {
            if (IsGeneratedObjectId(objectId))
            {
                return;
            }

            SetObjectId(CreateObjectId(), refreshRegistration);
        }

        private void SetObjectId(string value, bool refreshRegistration)
        {
            if (string.Equals(objectId, value, StringComparison.Ordinal))
            {
                return;
            }

            objectId = value;
            if (refreshRegistration)
            {
                RefreshRegistration();
            }
        }

        internal void RegenerateObjectIdForDuplicate()
        {
            SetObjectId(CreateObjectId(), false);
#if UNITY_EDITOR
            if (!Application.isPlaying && IsSceneObjectForIdUniquenessCheck(this))
            {
                EditorUtility.SetDirty(this);
                if (gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
#endif
        }

        private static string CreateObjectId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static bool IsGeneratedObjectId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            Guid parsed;
            return Guid.TryParseExact(value, "N", out parsed);
        }

#if UNITY_EDITOR
        private void EnsureUniqueObjectIdInEditor()
        {
            if (!IsSceneObjectForIdUniquenessCheck(this) || !HasDuplicateObjectIdWithEarlierEditorKey())
            {
                return;
            }

            SetObjectId(CreateObjectId(), true);
            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }

        private bool HasDuplicateObjectIdWithEarlierEditorKey()
        {
            SmartObjectComponent[] components = Resources.FindObjectsOfTypeAll<SmartObjectComponent>();
            for (int i = 0; i < components.Length; i++)
            {
                SmartObjectComponent other = components[i];
                if (other == null ||
                    other == this ||
                    !IsSceneObjectForIdUniquenessCheck(other) ||
                    !string.Equals(other.objectId, objectId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.CompareOrdinal(GetEditorObjectKey(this), GetEditorObjectKey(other)) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSceneObjectForIdUniquenessCheck(SmartObjectComponent component)
        {
            return component != null &&
                component.gameObject != null &&
                component.gameObject.scene.IsValid() &&
                !EditorUtility.IsPersistent(component);
        }

        private static string GetEditorObjectKey(SmartObjectComponent component)
        {
            string scenePath = component.gameObject.scene.path;
            if (string.IsNullOrEmpty(scenePath))
            {
                scenePath = component.gameObject.scene.name;
            }

            return scenePath + "/" + GetHierarchyPath(component.transform) + "#" + component.GetInstanceID().ToString();
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
#endif
    }
}
