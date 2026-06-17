using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public enum SmartObjectFailReason
    {
        None,
        InvalidRequester,
        InvalidActivity,
        ObjectNotFound,
        SlotNotFound,
        ActivityMismatch,
        ObjectDisabled,
        SlotBlocked,
        AlreadyReserved,
        AlreadyOccupied,
        AccessDenied,
        Closed,
        OutOfRange,
        NoCandidate,
        TokenInvalid,
        TokenExpired,
        TokenOwnerMismatch,
        StateMismatch,
        SystemError
    }

    public enum SmartObjectSlotState
    {
        Free,
        Reserved,
        Occupied,
        Blocked
    }

    public enum SmartObjectRegistrationState
    {
        MissingComponent,
        MissingObjectId,
        Registered,
        NotRegistered,
        DuplicateObjectId
    }

    public sealed class SmartObjectSlotDebugSnapshot
    {
        internal SmartObjectSlotDebugSnapshot(
            int index,
            bool isMissing,
            int slotId,
            string activities,
            string tags,
            string accessGroup,
            float slotBaseScore,
            float useDuration,
            bool blocked,
            bool closed,
            SmartObjectSlotState state,
            SmartObjectSlotState runtimeState,
            string requesterId,
            string reservationToken,
            float reservedUntil,
            float occupiedSince,
            float remainingSeconds,
            string lastReleaseReason,
            Vector3 targetPosition,
            Vector3 facingPosition)
        {
            Index = index;
            IsMissing = isMissing;
            SlotId = slotId;
            Activities = activities ?? string.Empty;
            Tags = tags ?? string.Empty;
            AccessGroup = accessGroup ?? string.Empty;
            SlotBaseScore = slotBaseScore;
            UseDuration = useDuration;
            Blocked = blocked;
            Closed = closed;
            State = state;
            RuntimeState = runtimeState;
            RequesterId = requesterId ?? string.Empty;
            ReservationToken = reservationToken ?? string.Empty;
            ReservedUntil = reservedUntil;
            OccupiedSince = occupiedSince;
            RemainingSeconds = remainingSeconds;
            LastReleaseReason = lastReleaseReason ?? string.Empty;
            TargetPosition = targetPosition;
            FacingPosition = facingPosition;
        }

        public int Index { get; private set; }
        public bool IsMissing { get; private set; }
        public int SlotId { get; private set; }
        public string Activities { get; private set; }
        public string Tags { get; private set; }
        public string AccessGroup { get; private set; }
        public float SlotBaseScore { get; private set; }
        public float UseDuration { get; private set; }
        public bool Blocked { get; private set; }
        public bool Closed { get; private set; }
        public SmartObjectSlotState State { get; private set; }
        public SmartObjectSlotState RuntimeState { get; private set; }
        public string RequesterId { get; private set; }
        public string ReservationToken { get; private set; }
        public float ReservedUntil { get; private set; }
        public float OccupiedSince { get; private set; }
        public float RemainingSeconds { get; private set; }
        public string LastReleaseReason { get; private set; }
        public Vector3 TargetPosition { get; private set; }
        public Vector3 FacingPosition { get; private set; }
    }

    public sealed class SmartObjectDebugSnapshot
    {
        internal SmartObjectDebugSnapshot(
            SmartObjectComponent component,
            SmartObjectRegistrationState registrationState,
            string registeredObjectId,
            SmartObjectSlotDebugSnapshot[] slots)
        {
            Component = component;
            GameObject = component == null ? null : component.gameObject;
            ObjectId = component == null ? string.Empty : component.ObjectId ?? string.Empty;
            SmartObjectEnabled = component != null && component.SmartObjectEnabled;
            ComponentEnabled = component != null && component.enabled;
            ActiveInHierarchy = component != null && component.gameObject.activeInHierarchy;
            IsActiveAndEnabled = component != null && component.isActiveAndEnabled;
            ObjectBaseScore = component == null ? 0f : component.ObjectBaseScore;
            Tags = component == null ? string.Empty : component.Tags ?? string.Empty;
            AccessGroup = component == null ? string.Empty : component.AccessGroup ?? string.Empty;
            RegistrationOrder = component == null ? 0 : component.RegistrationOrder;
            RegistrationState = registrationState;
            RegisteredObjectId = registeredObjectId ?? string.Empty;
            Slots = slots ?? new SmartObjectSlotDebugSnapshot[0];

            for (int i = 0; i < Slots.Length; i++)
            {
                SmartObjectSlotDebugSnapshot slot = Slots[i];
                if (slot == null || slot.IsMissing)
                {
                    MissingSlotCount++;
                    continue;
                }

                if (slot.Closed)
                {
                    ClosedSlotCount++;
                }

                switch (slot.State)
                {
                    case SmartObjectSlotState.Reserved:
                        ReservedSlotCount++;
                        break;
                    case SmartObjectSlotState.Occupied:
                        OccupiedSlotCount++;
                        break;
                    case SmartObjectSlotState.Blocked:
                        BlockedSlotCount++;
                        break;
                    default:
                        if (!slot.Closed)
                        {
                            FreeSlotCount++;
                        }
                        break;
                }
            }
        }

        public SmartObjectComponent Component { get; private set; }
        public GameObject GameObject { get; private set; }
        public string ObjectId { get; private set; }
        public bool SmartObjectEnabled { get; private set; }
        public bool ComponentEnabled { get; private set; }
        public bool ActiveInHierarchy { get; private set; }
        public bool IsActiveAndEnabled { get; private set; }
        public float ObjectBaseScore { get; private set; }
        public string Tags { get; private set; }
        public string AccessGroup { get; private set; }
        public int RegistrationOrder { get; private set; }
        public SmartObjectRegistrationState RegistrationState { get; private set; }
        public string RegisteredObjectId { get; private set; }
        public SmartObjectSlotDebugSnapshot[] Slots { get; private set; }
        public int FreeSlotCount { get; private set; }
        public int ReservedSlotCount { get; private set; }
        public int OccupiedSlotCount { get; private set; }
        public int BlockedSlotCount { get; private set; }
        public int ClosedSlotCount { get; private set; }
        public int MissingSlotCount { get; private set; }
    }

    public static class SmartObjectReleaseReason
    {
        public const string Completed = "Completed";
        public const string Aborted = "Aborted";
        public const string Interrupted = "Interrupted";
        public const string Timeout = "Timeout";
        public const string Dead = "Dead";
        public const string Disabled = "Disabled";
        public const string ForceRelease = "ForceRelease";
    }

    [Serializable]
    public sealed class SmartObjectSlot
    {
        [SerializeField] private int slotId;
        [SerializeField, Tooltip("Comma-separated activity ids supported by this slot. Use * to match any activity.")]
        private string activities;
        [SerializeField, Tooltip("Comma-separated tags contributed by this slot.")]
        private string tags;
        [SerializeField, Tooltip("Optional access group required for this slot. Overrides the object access group when set.")]
        private string accessGroup;
        [SerializeField] private float slotBaseScore;
        [SerializeField] private float useDuration = 1f;
        [SerializeField] private Transform targetTransform;
        [SerializeField] private Transform facingTransform;
        [SerializeField] private Vector3 localTargetPosition = Vector3.zero;
        [SerializeField] private Vector3 localFacingPosition = Vector3.forward;
        [SerializeField] private bool blocked;
        [SerializeField] private bool closed;

        [NonSerialized] internal SmartObjectSlotState RuntimeState;
        [NonSerialized] internal string ReservedBy;
        [NonSerialized] internal string OccupiedBy;
        [NonSerialized] internal string ReservationToken;
        [NonSerialized] internal float ReservedUntil;
        [NonSerialized] internal float OccupiedSince;
        [NonSerialized] internal string LastReleaseReason;

        public int SlotId
        {
            get { return slotId; }
            set { slotId = value; }
        }

        public string Activities
        {
            get { return activities; }
            set { activities = value; }
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

        public float SlotBaseScore
        {
            get { return slotBaseScore; }
            set { slotBaseScore = value; }
        }

        public float UseDuration
        {
            get { return useDuration; }
            set { useDuration = value; }
        }

        public Transform TargetTransform
        {
            get { return targetTransform; }
            set { targetTransform = value; }
        }

        public Transform FacingTransform
        {
            get { return facingTransform; }
            set { facingTransform = value; }
        }

        public Vector3 LocalTargetPosition
        {
            get { return localTargetPosition; }
            set { localTargetPosition = value; }
        }

        public Vector3 LocalFacingPosition
        {
            get { return localFacingPosition; }
            set { localFacingPosition = value; }
        }

        public bool Blocked
        {
            get { return blocked; }
            set { blocked = value; }
        }

        public bool Closed
        {
            get { return closed; }
            set { closed = value; }
        }

        public SmartObjectSlotState CurrentState
        {
            get
            {
                if (RuntimeState == SmartObjectSlotState.Reserved || RuntimeState == SmartObjectSlotState.Occupied)
                {
                    return RuntimeState;
                }

                return blocked ? SmartObjectSlotState.Blocked : SmartObjectSlotState.Free;
            }
        }

        public bool SupportsActivity(string activity)
        {
            if (string.IsNullOrWhiteSpace(activity))
            {
                return false;
            }

            string[] values = SmartObjectTextUtility.SplitList(activities);
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == "*" || string.Equals(values[i], activity, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public Vector3 GetTargetPosition(Transform owner)
        {
            if (targetTransform != null)
            {
                return targetTransform.position;
            }

            return owner == null ? localTargetPosition : owner.TransformPoint(localTargetPosition);
        }

        public Vector3 GetFacingPosition(Transform owner)
        {
            if (facingTransform != null)
            {
                return facingTransform.position;
            }

            return owner == null ? localFacingPosition : owner.TransformPoint(localFacingPosition);
        }

        internal void ClearRuntimeState(string releaseReason)
        {
            RuntimeState = SmartObjectSlotState.Free;
            ReservedBy = string.Empty;
            OccupiedBy = string.Empty;
            ReservationToken = string.Empty;
            ReservedUntil = 0f;
            OccupiedSince = 0f;
            LastReleaseReason = string.IsNullOrEmpty(releaseReason) ? string.Empty : releaseReason;
        }
    }

    public struct SmartObjectResult
    {
        public bool Found;
        public bool Success;
        public bool Valid;
        public string ObjectId;
        public int SlotId;
        public string ReservationToken;
        public string RequesterId;
        public string State;
        public string PreviousState;
        public Vector3 TargetPosition;
        public Vector3 FacingPosition;
        public GameObject TargetGameObject;
        public float UseDuration;
        public float Score;
        public float RemainingSeconds;
        public int ReleasedCount;
        public string FailReason;

        public static SmartObjectResult Failure(SmartObjectFailReason reason)
        {
            SmartObjectResult result = Default();
            result.FailReason = reason.ToString();
            return result;
        }

        public static SmartObjectResult Default()
        {
            SmartObjectResult result = new SmartObjectResult();
            result.ObjectId = string.Empty;
            result.SlotId = -1;
            result.ReservationToken = string.Empty;
            result.RequesterId = string.Empty;
            result.State = string.Empty;
            result.PreviousState = string.Empty;
            result.TargetPosition = Vector3.zero;
            result.FacingPosition = Vector3.zero;
            result.FailReason = SmartObjectFailReason.None.ToString();
            return result;
        }
    }

    public static class SmartObjectRegistry
    {
        private const float TimeoutScanInterval = 1f;

        private static readonly Dictionary<string, SmartObjectComponent> ObjectsById =
            new Dictionary<string, SmartObjectComponent>(StringComparer.Ordinal);
        private static readonly Dictionary<SmartObjectComponent, string> RegisteredIdsByComponent =
            new Dictionary<SmartObjectComponent, string>();
        private static readonly List<SmartObjectComponent> RegisteredObjects = new List<SmartObjectComponent>();
        private static readonly Dictionary<string, SmartObjectReservation> ReservationsByToken =
            new Dictionary<string, SmartObjectReservation>(StringComparer.Ordinal);

        private static int nextRegistrationOrder = 1;
        private static float lastTimeoutScanTime;
        private static Func<float> timeProviderForTests;

        public static void Register(SmartObjectComponent component)
        {
            if (component == null)
            {
                return;
            }

            string id = component.ObjectId;
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            string registeredId;
            if (RegisteredIdsByComponent.TryGetValue(component, out registeredId))
            {
                if (string.Equals(registeredId, id, StringComparison.Ordinal))
                {
                    return;
                }

                Unregister(component, null);
            }

            SmartObjectComponent existing;
            for (int attempts = 0; attempts < 4 && ObjectsById.TryGetValue(id, out existing) && existing != component; attempts++)
            {
                component.RegenerateObjectIdForDuplicate();
                id = component.ObjectId;
            }

            if (ObjectsById.TryGetValue(id, out existing) && existing != component)
            {
                BlueprintLog.Warning("[Blueprint] Duplicate SmartObject objectId '" + id + "' ignored on " + component.name + ".");
                return;
            }

            component.RegistrationOrder = nextRegistrationOrder++;
            ObjectsById[id] = component;
            RegisteredIdsByComponent[component] = id;
            RegisteredObjects.Add(component);
        }

        public static void Unregister(SmartObjectComponent component, string releaseReason)
        {
            if (component == null)
            {
                return;
            }

            ReleaseAllForObject(component, releaseReason);

            string registeredId;
            if (RegisteredIdsByComponent.TryGetValue(component, out registeredId))
            {
                SmartObjectComponent existing;
                if (ObjectsById.TryGetValue(registeredId, out existing) && existing == component)
                {
                    ObjectsById.Remove(registeredId);
                }

                RegisteredIdsByComponent.Remove(component);
            }

            RegisteredObjects.Remove(component);
        }

        public static void ReleaseAllForObject(SmartObjectComponent component, string releaseReason)
        {
            if (component == null || component.Slots == null)
            {
                return;
            }

            for (int i = 0; i < component.Slots.Count; i++)
            {
                SmartObjectSlot slot = component.Slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (slot.RuntimeState == SmartObjectSlotState.Reserved || slot.RuntimeState == SmartObjectSlotState.Occupied)
                {
                    ReleaseSlot(component, slot, string.IsNullOrEmpty(releaseReason) ? SmartObjectReleaseReason.Disabled : releaseReason);
                }
            }
        }

        public static SmartObjectDebugSnapshot CreateDebugSnapshot(SmartObjectComponent component)
        {
            string registeredId;
            SmartObjectRegistrationState registrationState = ResolveRegistrationState(component, out registeredId);
            SmartObjectSlotDebugSnapshot[] slots = CreateSlotDebugSnapshots(component);
            return new SmartObjectDebugSnapshot(component, registrationState, registeredId, slots);
        }

        public static SmartObjectResult FindBest(
            string requesterId,
            string activity,
            Vector3 center,
            float radius,
            string requiredTags,
            string forbiddenTags,
            string accessGroup,
            float needScore,
            float maxDistancePenalty)
        {
            return FindBestInternal(
                requesterId,
                activity,
                center,
                radius,
                requiredTags,
                forbiddenTags,
                accessGroup,
                needScore,
                maxDistancePenalty,
                null);
        }

        public static SmartObjectResult FindBestActor(
            string requesterId,
            string activity,
            Vector3 center,
            float radius,
            string requiredTags,
            string forbiddenTags,
            string accessGroup,
            float needScore,
            float maxDistancePenalty,
            GameObject excludeGameObject)
        {
            return FindBestInternal(
                requesterId,
                activity,
                center,
                radius,
                requiredTags,
                forbiddenTags,
                accessGroup,
                needScore,
                maxDistancePenalty,
                excludeGameObject);
        }

        private static SmartObjectResult FindBestInternal(
            string requesterId,
            string activity,
            Vector3 center,
            float radius,
            string requiredTags,
            string forbiddenTags,
            string accessGroup,
            float needScore,
            float maxDistancePenalty,
            GameObject excludeGameObject)
        {
            TickTimeouts();

            if (string.IsNullOrWhiteSpace(requesterId))
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.InvalidRequester);
            }

            if (string.IsNullOrWhiteSpace(activity))
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.InvalidActivity);
            }

            if (RegisteredObjects.Count == 0)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.NoCandidate);
            }

            float searchRadius = Mathf.Max(0f, radius);
            HashSet<string> required = SmartObjectTextUtility.ToSet(requiredTags);
            HashSet<string> forbidden = SmartObjectTextUtility.ToSet(forbiddenTags);
            SmartObjectCandidateFailureTracker failures = new SmartObjectCandidateFailureTracker();
            SmartObjectResult best = SmartObjectResult.Failure(SmartObjectFailReason.NoCandidate);
            float bestDistance = float.MaxValue;
            bool hasBest = false;

            for (int i = 0; i < RegisteredObjects.Count; i++)
            {
                SmartObjectComponent smartObject = RegisteredObjects[i];
                if (smartObject == null)
                {
                    continue;
                }

                if (IsExcludedSmartObject(smartObject, excludeGameObject))
                {
                    continue;
                }

                if (!smartObject.SmartObjectEnabled || !smartObject.isActiveAndEnabled)
                {
                    failures.Record(SmartObjectFailReason.ObjectDisabled);
                    continue;
                }

                List<SmartObjectSlot> slots = smartObject.Slots;
                if (slots == null)
                {
                    continue;
                }

                for (int s = 0; s < slots.Count; s++)
                {
                    SmartObjectSlot slot = slots[s];
                    if (slot == null)
                    {
                        continue;
                    }

                    if (!slot.SupportsActivity(activity))
                    {
                        continue;
                    }

                    SmartObjectFailReason availabilityFailure;
                    if (!CanUseSlot(smartObject, slot, accessGroup, out availabilityFailure))
                    {
                        failures.Record(availabilityFailure);
                        continue;
                    }

                    if (!MatchesTags(smartObject, slot, required, forbidden))
                    {
                        failures.Record(SmartObjectFailReason.NoCandidate);
                        continue;
                    }

                    Vector3 targetPosition = slot.GetTargetPosition(smartObject.transform);
                    float distance = Vector3.Distance(center, targetPosition);
                    if (distance > searchRadius)
                    {
                        failures.Record(SmartObjectFailReason.OutOfRange);
                        continue;
                    }

                    float distancePenalty = distance * 2f;
                    if (maxDistancePenalty > 0f)
                    {
                        distancePenalty = Mathf.Min(distancePenalty, maxDistancePenalty);
                    }

                    float score = 100f + smartObject.ObjectBaseScore + slot.SlotBaseScore + needScore - distancePenalty;
                    if (!hasBest || score > best.Score || Mathf.Approximately(score, best.Score) && IsCloserOrEarlier(smartObject, distance, bestDistance, best))
                    {
                        hasBest = true;
                        bestDistance = distance;
                        best = SmartObjectResult.Default();
                        best.Found = true;
                        best.ObjectId = smartObject.ObjectId;
                        best.SlotId = slot.SlotId;
                        best.TargetPosition = targetPosition;
                        best.FacingPosition = slot.GetFacingPosition(smartObject.transform);
                        best.TargetGameObject = smartObject.gameObject;
                        best.UseDuration = Mathf.Max(0f, slot.UseDuration);
                        best.Score = score;
                    }
                }
            }

            return hasBest ? best : SmartObjectResult.Failure(failures.GetFindFailure());
        }

        public static SmartObjectResult Reserve(
            string requesterId,
            string objectId,
            int slotId,
            string activity,
            float holdSeconds,
            string accessGroup)
        {
            TickTimeouts();

            SmartObjectResult validation = ValidateRequesterObjectSlotActivity(requesterId, objectId, slotId, activity, out SmartObjectComponent smartObject, out SmartObjectSlot slot);
            if (validation.FailReason != SmartObjectFailReason.None.ToString())
            {
                return validation;
            }

            if (!HasAccess(smartObject, slot, accessGroup))
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.AccessDenied);
            }

            float now = Now();
            if (slot.RuntimeState == SmartObjectSlotState.Reserved)
            {
                if (IsExpired(slot, now))
                {
                    ReleaseSlot(smartObject, slot, SmartObjectReleaseReason.Timeout);
                }
                else if (string.Equals(slot.ReservedBy, requesterId, StringComparison.Ordinal))
                {
                    slot.ReservedUntil = now + Mathf.Max(0f, holdSeconds);
                    SmartObjectReservation existingReservation;
                    if (!string.IsNullOrEmpty(slot.ReservationToken) && ReservationsByToken.TryGetValue(slot.ReservationToken, out existingReservation))
                    {
                        return ToReserveResult(existingReservation);
                    }

                    return SmartObjectResult.Failure(SmartObjectFailReason.TokenInvalid);
                }
                else
                {
                    return SmartObjectResult.Failure(SmartObjectFailReason.AlreadyReserved);
                }
            }

            if (slot.RuntimeState == SmartObjectSlotState.Occupied)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.AlreadyOccupied);
            }

            string token = Guid.NewGuid().ToString("N");
            slot.RuntimeState = SmartObjectSlotState.Reserved;
            slot.ReservedBy = requesterId;
            slot.OccupiedBy = string.Empty;
            slot.ReservationToken = token;
            slot.ReservedUntil = now + Mathf.Max(0f, holdSeconds);
            slot.OccupiedSince = 0f;

            SmartObjectReservation reservation = new SmartObjectReservation(smartObject, slot, requesterId, token);
            ReservationsByToken[token] = reservation;
            return ToReserveResult(reservation);
        }

        public static SmartObjectResult BeginUse(string requesterId, string reservationToken)
        {
            if (string.IsNullOrWhiteSpace(requesterId))
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.InvalidRequester);
            }

            SmartObjectReservation reservation;
            SmartObjectResult tokenResult = TryGetReservation(reservationToken, out reservation);
            if (tokenResult.FailReason != SmartObjectFailReason.None.ToString())
            {
                return tokenResult;
            }

            float now = Now();
            if (IsExpired(reservation.Slot, now))
            {
                ReleaseSlot(reservation.Component, reservation.Slot, SmartObjectReleaseReason.Timeout);
                return SmartObjectResult.Failure(SmartObjectFailReason.TokenExpired);
            }

            if (!string.Equals(reservation.RequesterId, requesterId, StringComparison.Ordinal))
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.TokenOwnerMismatch);
            }

            if (reservation.Slot.RuntimeState != SmartObjectSlotState.Reserved)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.StateMismatch);
            }

            if (!reservation.Component.SmartObjectEnabled || !reservation.Component.isActiveAndEnabled)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.ObjectDisabled);
            }

            if (reservation.Slot.Blocked)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.SlotBlocked);
            }

            if (reservation.Slot.Closed)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.Closed);
            }

            reservation.Slot.RuntimeState = SmartObjectSlotState.Occupied;
            reservation.Slot.OccupiedBy = requesterId;
            reservation.Slot.OccupiedSince = now;
            reservation.Slot.ReservedUntil = 0f;

            SmartObjectResult result = SmartObjectResult.Default();
            result.Success = true;
            result.ObjectId = reservation.Component.ObjectId;
            result.SlotId = reservation.Slot.SlotId;
            result.UseDuration = Mathf.Max(0f, reservation.Slot.UseDuration);
            return result;
        }

        public static SmartObjectResult Release(string requesterId, string reservationToken, string reason)
        {
            if (string.IsNullOrWhiteSpace(requesterId))
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.InvalidRequester);
            }

            SmartObjectReservation reservation;
            SmartObjectResult tokenResult = TryGetReservation(reservationToken, out reservation);
            if (tokenResult.FailReason != SmartObjectFailReason.None.ToString())
            {
                return tokenResult;
            }

            if (reservation.Slot.RuntimeState == SmartObjectSlotState.Reserved && IsExpired(reservation.Slot, Now()))
            {
                ReleaseSlot(reservation.Component, reservation.Slot, SmartObjectReleaseReason.Timeout);
                return SmartObjectResult.Failure(SmartObjectFailReason.TokenExpired);
            }

            if (!string.Equals(reservation.RequesterId, requesterId, StringComparison.Ordinal))
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.TokenOwnerMismatch);
            }

            if (reservation.Slot.RuntimeState != SmartObjectSlotState.Reserved && reservation.Slot.RuntimeState != SmartObjectSlotState.Occupied)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.StateMismatch);
            }

            SmartObjectSlotState previousState = reservation.Slot.RuntimeState;
            SmartObjectResult result = ReleaseSlot(reservation.Component, reservation.Slot, string.IsNullOrEmpty(reason) ? SmartObjectReleaseReason.Completed : reason);
            result.Success = true;
            result.PreviousState = previousState.ToString();
            return result;
        }

        public static SmartObjectResult GetReservationInfo(string reservationToken)
        {
            SmartObjectReservation reservation;
            SmartObjectResult tokenResult = TryGetReservation(reservationToken, out reservation);
            if (tokenResult.FailReason != SmartObjectFailReason.None.ToString())
            {
                return tokenResult;
            }

            if (reservation.Slot.RuntimeState == SmartObjectSlotState.Reserved && IsExpired(reservation.Slot, Now()))
            {
                ReleaseSlot(reservation.Component, reservation.Slot, SmartObjectReleaseReason.Timeout);
                return SmartObjectResult.Failure(SmartObjectFailReason.TokenExpired);
            }

            SmartObjectResult result = SmartObjectResult.Default();
            result.Valid = true;
            result.ObjectId = reservation.Component.ObjectId;
            result.SlotId = reservation.Slot.SlotId;
            result.RequesterId = reservation.RequesterId;
            result.State = reservation.Slot.CurrentState.ToString();
            result.TargetPosition = reservation.Slot.GetTargetPosition(reservation.Component.transform);
            result.FacingPosition = reservation.Slot.GetFacingPosition(reservation.Component.transform);
            result.RemainingSeconds = reservation.Slot.RuntimeState == SmartObjectSlotState.Reserved
                ? Mathf.Max(0f, reservation.Slot.ReservedUntil - Now())
                : 0f;
            return result;
        }

        public static SmartObjectResult ReleaseByRequester(string requesterId, string reason)
        {
            if (string.IsNullOrWhiteSpace(requesterId))
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.InvalidRequester);
            }

            SmartObjectResult result = SmartObjectResult.Default();
            string releaseReason = string.IsNullOrEmpty(reason) ? SmartObjectReleaseReason.ForceRelease : reason;
            List<SmartObjectReservation> matches = new List<SmartObjectReservation>();
            foreach (SmartObjectReservation reservation in ReservationsByToken.Values)
            {
                if (reservation == null || reservation.Slot == null)
                {
                    continue;
                }

                bool reservedByRequester = reservation.Slot.RuntimeState == SmartObjectSlotState.Reserved &&
                                           string.Equals(reservation.Slot.ReservedBy, requesterId, StringComparison.Ordinal);
                bool occupiedByRequester = reservation.Slot.RuntimeState == SmartObjectSlotState.Occupied &&
                                           string.Equals(reservation.Slot.OccupiedBy, requesterId, StringComparison.Ordinal);
                if (reservedByRequester || occupiedByRequester)
                {
                    matches.Add(reservation);
                }
            }

            for (int i = 0; i < matches.Count; i++)
            {
                ReleaseSlot(matches[i].Component, matches[i].Slot, releaseReason);
            }

            result.ReleasedCount = matches.Count;
            return result;
        }

        public static void TickTimeouts()
        {
            float now = Now();
            if (now - lastTimeoutScanTime < TimeoutScanInterval)
            {
                return;
            }

            lastTimeoutScanTime = now;
            List<SmartObjectReservation> expired = new List<SmartObjectReservation>();
            foreach (SmartObjectReservation reservation in ReservationsByToken.Values)
            {
                if (reservation != null && reservation.Slot != null &&
                    reservation.Slot.RuntimeState == SmartObjectSlotState.Reserved &&
                    IsExpired(reservation.Slot, now))
                {
                    expired.Add(reservation);
                }
            }

            for (int i = 0; i < expired.Count; i++)
            {
                ReleaseSlot(expired[i].Component, expired[i].Slot, SmartObjectReleaseReason.Timeout);
            }
        }

        public static void ResetForTests()
        {
            ObjectsById.Clear();
            RegisteredIdsByComponent.Clear();
            RegisteredObjects.Clear();
            ReservationsByToken.Clear();
            nextRegistrationOrder = 1;
            lastTimeoutScanTime = 0f;
            timeProviderForTests = null;
        }

        public static void SetTimeProviderForTests(Func<float> timeProvider)
        {
            timeProviderForTests = timeProvider;
        }

        private static SmartObjectRegistrationState ResolveRegistrationState(
            SmartObjectComponent component,
            out string registeredId)
        {
            registeredId = string.Empty;
            if (component == null)
            {
                return SmartObjectRegistrationState.MissingComponent;
            }

            string objectId = component.ObjectId;
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return SmartObjectRegistrationState.MissingObjectId;
            }

            if (RegisteredIdsByComponent.TryGetValue(component, out registeredId) &&
                string.Equals(registeredId, objectId, StringComparison.Ordinal))
            {
                return SmartObjectRegistrationState.Registered;
            }

            SmartObjectComponent existing;
            if (ObjectsById.TryGetValue(objectId, out existing) && existing != null && existing != component)
            {
                return SmartObjectRegistrationState.DuplicateObjectId;
            }

            registeredId = string.Empty;
            return SmartObjectRegistrationState.NotRegistered;
        }

        private static SmartObjectSlotDebugSnapshot[] CreateSlotDebugSnapshots(SmartObjectComponent component)
        {
            if (component == null || component.Slots == null)
            {
                return new SmartObjectSlotDebugSnapshot[0];
            }

            List<SmartObjectSlotDebugSnapshot> snapshots = new List<SmartObjectSlotDebugSnapshot>();
            for (int i = 0; i < component.Slots.Count; i++)
            {
                snapshots.Add(CreateSlotDebugSnapshot(component, component.Slots[i], i));
            }

            return snapshots.ToArray();
        }

        private static SmartObjectSlotDebugSnapshot CreateSlotDebugSnapshot(
            SmartObjectComponent component,
            SmartObjectSlot slot,
            int index)
        {
            if (slot == null)
            {
                return new SmartObjectSlotDebugSnapshot(
                    index,
                    true,
                    -1,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0f,
                    0f,
                    false,
                    false,
                    SmartObjectSlotState.Free,
                    SmartObjectSlotState.Free,
                    string.Empty,
                    string.Empty,
                    0f,
                    0f,
                    0f,
                    string.Empty,
                    Vector3.zero,
                    Vector3.zero);
            }

            float remainingSeconds = slot.RuntimeState == SmartObjectSlotState.Reserved
                ? Mathf.Max(0f, slot.ReservedUntil - Now())
                : 0f;
            string requesterId = string.Empty;
            if (slot.RuntimeState == SmartObjectSlotState.Reserved)
            {
                requesterId = slot.ReservedBy;
            }
            else if (slot.RuntimeState == SmartObjectSlotState.Occupied)
            {
                requesterId = slot.OccupiedBy;
            }

            Transform owner = component == null ? null : component.transform;
            return new SmartObjectSlotDebugSnapshot(
                index,
                false,
                slot.SlotId,
                slot.Activities,
                slot.Tags,
                slot.AccessGroup,
                slot.SlotBaseScore,
                slot.UseDuration,
                slot.Blocked,
                slot.Closed,
                slot.CurrentState,
                slot.RuntimeState,
                requesterId,
                slot.ReservationToken,
                slot.ReservedUntil,
                slot.OccupiedSince,
                remainingSeconds,
                slot.LastReleaseReason,
                slot.GetTargetPosition(owner),
                slot.GetFacingPosition(owner));
        }

        private static SmartObjectResult ValidateRequesterObjectSlotActivity(
            string requesterId,
            string objectId,
            int slotId,
            string activity,
            out SmartObjectComponent smartObject,
            out SmartObjectSlot slot)
        {
            smartObject = null;
            slot = null;
            if (string.IsNullOrWhiteSpace(requesterId))
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.InvalidRequester);
            }

            if (string.IsNullOrWhiteSpace(objectId) || !ObjectsById.TryGetValue(objectId, out smartObject) || smartObject == null)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.ObjectNotFound);
            }

            slot = smartObject.FindSlot(slotId);
            if (slot == null)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.SlotNotFound);
            }

            if (string.IsNullOrWhiteSpace(activity))
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.InvalidActivity);
            }

            if (!smartObject.SmartObjectEnabled || !smartObject.isActiveAndEnabled)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.ObjectDisabled);
            }

            if (slot.Blocked)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.SlotBlocked);
            }

            if (slot.Closed)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.Closed);
            }

            if (!slot.SupportsActivity(activity))
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.ActivityMismatch);
            }

            return SmartObjectResult.Default();
        }

        private static bool CanUseSlot(SmartObjectComponent smartObject, SmartObjectSlot slot, string accessGroup, out SmartObjectFailReason failReason)
        {
            if (smartObject == null || !smartObject.SmartObjectEnabled || !smartObject.isActiveAndEnabled)
            {
                failReason = SmartObjectFailReason.ObjectDisabled;
                return false;
            }

            if (slot == null)
            {
                failReason = SmartObjectFailReason.SlotNotFound;
                return false;
            }

            if (slot.Blocked)
            {
                failReason = SmartObjectFailReason.SlotBlocked;
                return false;
            }

            if (slot.Closed)
            {
                failReason = SmartObjectFailReason.Closed;
                return false;
            }

            string requiredAccessGroup = string.IsNullOrWhiteSpace(slot.AccessGroup) ? smartObject.AccessGroup : slot.AccessGroup;
            if (!string.IsNullOrWhiteSpace(requiredAccessGroup) &&
                !string.Equals(requiredAccessGroup, accessGroup, StringComparison.OrdinalIgnoreCase))
            {
                failReason = SmartObjectFailReason.AccessDenied;
                return false;
            }

            if (slot.RuntimeState == SmartObjectSlotState.Reserved)
            {
                if (IsExpired(slot, Now()))
                {
                    ReleaseSlot(smartObject, slot, SmartObjectReleaseReason.Timeout);
                }
                else
                {
                    failReason = SmartObjectFailReason.AlreadyReserved;
                    return false;
                }
            }

            if (slot.RuntimeState == SmartObjectSlotState.Reserved)
            {
                failReason = SmartObjectFailReason.AlreadyReserved;
                return false;
            }

            if (slot.RuntimeState == SmartObjectSlotState.Occupied)
            {
                failReason = SmartObjectFailReason.AlreadyOccupied;
                return false;
            }

            failReason = SmartObjectFailReason.None;
            return true;
        }

        private static bool HasAccess(SmartObjectComponent smartObject, SmartObjectSlot slot, string accessGroup)
        {
            string requiredAccessGroup = string.IsNullOrWhiteSpace(slot.AccessGroup) ? smartObject.AccessGroup : slot.AccessGroup;
            return string.IsNullOrWhiteSpace(requiredAccessGroup) ||
                   string.Equals(requiredAccessGroup, accessGroup, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesTags(
            SmartObjectComponent smartObject,
            SmartObjectSlot slot,
            HashSet<string> requiredTags,
            HashSet<string> forbiddenTags)
        {
            HashSet<string> tags = SmartObjectTextUtility.ToSet(smartObject == null ? null : smartObject.Tags);
            SmartObjectTextUtility.AddAll(tags, slot == null ? null : slot.Tags);

            foreach (string required in requiredTags)
            {
                if (!tags.Contains(required))
                {
                    return false;
                }
            }

            foreach (string forbidden in forbiddenTags)
            {
                if (tags.Contains(forbidden))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsExcludedSmartObject(SmartObjectComponent smartObject, GameObject excludeGameObject)
        {
            if (smartObject == null || excludeGameObject == null)
            {
                return false;
            }

            Transform smartObjectTransform = smartObject.transform;
            Transform excludeTransform = excludeGameObject.transform;
            if (smartObjectTransform == null || excludeTransform == null)
            {
                return false;
            }

            return smartObjectTransform == excludeTransform ||
                smartObjectTransform.IsChildOf(excludeTransform) ||
                excludeTransform.IsChildOf(smartObjectTransform);
        }

        private static bool IsCloserOrEarlier(SmartObjectComponent smartObject, float distance, float bestDistance, SmartObjectResult best)
        {
            if (!Mathf.Approximately(distance, bestDistance))
            {
                return distance < bestDistance;
            }

            SmartObjectComponent bestObject;
            if (!string.IsNullOrEmpty(best.ObjectId) && ObjectsById.TryGetValue(best.ObjectId, out bestObject) && bestObject != null)
            {
                return smartObject.RegistrationOrder < bestObject.RegistrationOrder;
            }

            return true;
        }

        private static SmartObjectResult TryGetReservation(string reservationToken, out SmartObjectReservation reservation)
        {
            reservation = null;
            if (string.IsNullOrWhiteSpace(reservationToken) ||
                !ReservationsByToken.TryGetValue(reservationToken, out reservation) ||
                reservation == null ||
                reservation.Slot == null ||
                reservation.Component == null)
            {
                return SmartObjectResult.Failure(SmartObjectFailReason.TokenInvalid);
            }

            return SmartObjectResult.Default();
        }

        private static SmartObjectResult ToReserveResult(SmartObjectReservation reservation)
        {
            SmartObjectResult result = SmartObjectResult.Default();
            result.Success = true;
            result.ReservationToken = reservation.Token;
            result.ObjectId = reservation.Component.ObjectId;
            result.SlotId = reservation.Slot.SlotId;
            result.TargetPosition = reservation.Slot.GetTargetPosition(reservation.Component.transform);
            result.FacingPosition = reservation.Slot.GetFacingPosition(reservation.Component.transform);
            result.UseDuration = Mathf.Max(0f, reservation.Slot.UseDuration);
            return result;
        }

        private static SmartObjectResult ReleaseSlot(SmartObjectComponent smartObject, SmartObjectSlot slot, string reason)
        {
            SmartObjectResult result = SmartObjectResult.Default();
            if (smartObject != null)
            {
                result.ObjectId = smartObject.ObjectId;
            }

            if (slot != null)
            {
                result.SlotId = slot.SlotId;
                result.PreviousState = slot.RuntimeState.ToString();
                if (!string.IsNullOrEmpty(slot.ReservationToken))
                {
                    ReservationsByToken.Remove(slot.ReservationToken);
                }

                slot.ClearRuntimeState(reason);
            }

            return result;
        }

        private static bool IsExpired(SmartObjectSlot slot, float now)
        {
            return slot != null && slot.RuntimeState == SmartObjectSlotState.Reserved && slot.ReservedUntil <= now;
        }

        private static float Now()
        {
            if (timeProviderForTests != null)
            {
                return timeProviderForTests();
            }

            return Time.realtimeSinceStartup;
        }
    }

    internal sealed class SmartObjectReservation
    {
        public readonly SmartObjectComponent Component;
        public readonly SmartObjectSlot Slot;
        public readonly string RequesterId;
        public readonly string Token;

        public SmartObjectReservation(SmartObjectComponent component, SmartObjectSlot slot, string requesterId, string token)
        {
            Component = component;
            Slot = slot;
            RequesterId = requesterId;
            Token = token;
        }
    }

    internal sealed class SmartObjectCandidateFailureTracker
    {
        private readonly HashSet<SmartObjectFailReason> reasons = new HashSet<SmartObjectFailReason>();

        public void Record(SmartObjectFailReason reason)
        {
            if (reason != SmartObjectFailReason.None && reason != SmartObjectFailReason.ActivityMismatch)
            {
                reasons.Add(reason);
            }
        }

        public SmartObjectFailReason GetFindFailure()
        {
            if (reasons.Contains(SmartObjectFailReason.AlreadyOccupied))
            {
                return SmartObjectFailReason.AlreadyOccupied;
            }

            if (reasons.Contains(SmartObjectFailReason.AlreadyReserved))
            {
                return SmartObjectFailReason.AlreadyReserved;
            }

            if (reasons.Contains(SmartObjectFailReason.AccessDenied))
            {
                return SmartObjectFailReason.AccessDenied;
            }

            if (reasons.Contains(SmartObjectFailReason.Closed))
            {
                return SmartObjectFailReason.Closed;
            }

            if (reasons.Contains(SmartObjectFailReason.OutOfRange))
            {
                return SmartObjectFailReason.OutOfRange;
            }

            if (reasons.Contains(SmartObjectFailReason.ObjectDisabled))
            {
                return SmartObjectFailReason.ObjectDisabled;
            }

            if (reasons.Contains(SmartObjectFailReason.SlotBlocked))
            {
                return SmartObjectFailReason.SlotBlocked;
            }

            return SmartObjectFailReason.NoCandidate;
        }
    }

    internal static class SmartObjectTextUtility
    {
        private static readonly char[] Separators = { ',', ';', '|' };

        public static string[] SplitList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new string[0];
            }

            string[] rawValues = value.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            List<string> result = new List<string>();
            for (int i = 0; i < rawValues.Length; i++)
            {
                string item = rawValues[i].Trim();
                if (!string.IsNullOrEmpty(item))
                {
                    result.Add(item);
                }
            }

            return result.ToArray();
        }

        public static HashSet<string> ToSet(string value)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddAll(result, value);
            return result;
        }

        public static void AddAll(HashSet<string> target, string value)
        {
            if (target == null)
            {
                return;
            }

            string[] values = SplitList(value);
            for (int i = 0; i < values.Length; i++)
            {
                target.Add(values[i]);
            }
        }
    }
}
