using System;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class MonsterAttachment : MonoBehaviour
    {
        [SerializeField] private MonsterAttachmentKind attachmentKind = MonsterAttachmentKind.Ground;
        [SerializeField, Min(0f)] private float loadContribution = 8f;
        [SerializeField, Min(0.05f)] private float reachDistance = 0.65f;
        [SerializeField, Min(0f)] private float attachmentDuration = 0.3f;
        [SerializeField] private Vector3 attachedLocalOffset;

        private MonsterBase owner;
        private CartAttachmentPoints pointRegistry;
        private CartMonsterLoad loadRegistry;
        private MonsterAttachmentPoint reservedPoint;
        private Transform originalParent;
        private float attachElapsed;
        private bool loadRegistered;
        private bool isAttached;

        public MonsterAttachmentKind AttachmentKind => attachmentKind;
        public float LoadContribution => loadContribution;
        public float ReachDistance => reachDistance;
        public bool IsAttached => isAttached;
        public bool HasReservedPoint => reservedPoint != null;
        public MonsterAttachmentPoint ReservedPoint => reservedPoint;
        public Vector3 Destination => reservedPoint != null ? reservedPoint.transform.position : transform.position;

        public event Action<MonsterAttachment> Attached;
        public event Action<MonsterAttachment> Detached;

        public void Initialize(MonsterBase configuredOwner, CartAttachmentPoints configuredPoints, CartMonsterLoad configuredLoad)
        {
            owner = configuredOwner;
            pointRegistry = configuredPoints;
            loadRegistry = configuredLoad;
            originalParent = transform.parent;
            attachElapsed = 0f;
            isAttached = false;
            loadRegistered = false;
            reservedPoint = null;
        }

        public bool TrySelectPoint()
        {
            if (isAttached)
            {
                return true;
            }

            if (reservedPoint != null)
            {
                return true;
            }

            return pointRegistry != null &&
                   pointRegistry.TryReserveClosest(this, attachmentKind, transform.position,
                       owner != null ? owner.CartTransform : null,
                       owner != null ? owner.RearDirection : Vector3.zero, out reservedPoint);
        }

        public bool CanReachReservedPoint()
        {
            return reservedPoint != null && reservedPoint.gameObject.activeInHierarchy;
        }

        public bool IsWithinReach()
        {
            return CanReachReservedPoint() &&
                   Vector3.Distance(transform.position, reservedPoint.transform.position) <= reachDistance;
        }

        public bool TickAttach(float deltaTime)
        {
            if (!CanReachReservedPoint() || owner == null || owner.IsDead)
            {
                Detach();
                return false;
            }

            attachElapsed += Mathf.Max(0f, deltaTime);
            if (attachElapsed < attachmentDuration)
            {
                return false;
            }

            isAttached = true;
            transform.SetParent(reservedPoint.transform, true);
            transform.localPosition = attachedLocalOffset;
            Vector3 facing = reservedPoint.WorldFacingDirection;
            if (facing.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            }

            if (!loadRegistered && loadRegistry != null)
            {
                loadRegistered = loadRegistry.Register(this, loadContribution);
            }

            Attached?.Invoke(this);
            return true;
        }

        public void TickAttached()
        {
            if (!isAttached || reservedPoint == null || owner == null || owner.IsDead)
            {
                Detach();
            }
        }

        public void Detach()
        {
            bool hadReservation = reservedPoint != null || isAttached || loadRegistered;
            if (loadRegistered && loadRegistry != null)
            {
                loadRegistry.Unregister(this);
            }

            loadRegistered = false;
            isAttached = false;
            attachElapsed = 0f;
            if (reservedPoint != null)
            {
                if (transform.parent == reservedPoint.transform && originalParent != null)
                {
                    transform.SetParent(originalParent, true);
                }

                reservedPoint.Release(this);
                reservedPoint = null;
            }

            if (hadReservation)
            {
                Detached?.Invoke(this);
            }
        }

        public void Configure(
            MonsterAttachmentKind configuredKind,
            float configuredLoad,
            float configuredReachDistance,
            float configuredDuration,
            Vector3 configuredLocalOffset)
        {
            attachmentKind = configuredKind;
            loadContribution = Mathf.Max(0f, configuredLoad);
            reachDistance = Mathf.Max(0.05f, configuredReachDistance);
            attachmentDuration = Mathf.Max(0f, configuredDuration);
            attachedLocalOffset = configuredLocalOffset;
        }

        public void SetLoadContribution(float configuredLoad)
        {
            loadContribution = Mathf.Max(0f, configuredLoad);
        }

        private void OnDisable()
        {
            Detach();
        }

        private void OnDestroy()
        {
            Detach();
        }
    }
}
