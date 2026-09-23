using System;
using System.Collections.Generic;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [Flags]
    public enum MonsterAttachmentKind
    {
        Ground = 1,
        Flying = 2,
        Any = Ground | Flying
    }

    [DisallowMultipleComponent]
    public sealed class MonsterAttachmentPoint : MonoBehaviour
    {
        [SerializeField] private MonsterAttachmentKind acceptedKinds = MonsterAttachmentKind.Any;
        [SerializeField, Min(1)] private int capacity = 1;
        [SerializeField] private Vector3 localFacingDirection = Vector3.forward;

        private readonly HashSet<MonsterAttachment> occupants = new HashSet<MonsterAttachment>();

        public MonsterAttachmentKind AcceptedKinds => acceptedKinds;
        public int Capacity => capacity;
        public int OccupiedCount
        {
            get
            {
                RemoveStaleOccupants();
                return occupants.Count;
            }
        }
        public bool HasCapacity => OccupiedCount < capacity;
        public Vector3 WorldFacingDirection => transform.TransformDirection(localFacingDirection.normalized);

        public bool Accepts(MonsterAttachmentKind kind)
        {
            return (acceptedKinds & kind) != 0;
        }

        public bool TryReserve(MonsterAttachment monster)
        {
            if (monster == null)
            {
                return false;
            }

            RemoveStaleOccupants();
            if (occupants.Contains(monster))
            {
                return true;
            }

            if (occupants.Count >= capacity || !Accepts(monster.AttachmentKind))
            {
                return false;
            }

            occupants.Add(monster);
            return true;
        }

        public void Release(MonsterAttachment monster)
        {
            if (monster != null)
            {
                occupants.Remove(monster);
            }
        }

        public void Configure(MonsterAttachmentKind configuredKinds, int configuredCapacity, Vector3 configuredFacing)
        {
            acceptedKinds = configuredKinds;
            capacity = Mathf.Max(1, configuredCapacity);
            localFacingDirection = configuredFacing.sqrMagnitude > 0.001f ? configuredFacing.normalized : Vector3.forward;
        }

        private void RemoveStaleOccupants()
        {
            occupants.RemoveWhere(monster => monster == null || !monster.gameObject.activeInHierarchy);
        }
    }
}
