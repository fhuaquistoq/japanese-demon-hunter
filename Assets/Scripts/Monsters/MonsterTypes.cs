using System;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    public enum MonsterState
    {
        Spawn,
        Patrol,
        SelectTarget,
        Chase,
        Approach,
        Attack,
        Retreat,
        SelectAttachment,
        ChaseAttachment,
        Attach,
        Attached,
        Dead
    }

    public enum MonsterMovementType
    {
        Ground,
        Flying
    }

    public enum MonsterTargetType
    {
        Hunter,
        Candle
    }

    public enum MonsterTargetStrategy
    {
        Closest,
        ClosestLitCandle,
        RandomLitCandle,
        PrioritizeHunter
    }

    public enum MonsterAttackResult
    {
        Pending,
        Hit,
        Missed,
        Cancelled
    }

    [Serializable]
    public struct MonsterSpawnContext
    {
        public MonsterTargetRegistry targetRegistry;
        public Vector3 patrolCenter;
        public Transform cartTransform;
        public Transform rearReachPoint;
        public CartAttachmentPoints attachmentPoints;
        public CartMonsterLoad cartLoad;
        public PrototypeHunterMonsterTarget hunterTarget;
    }
}
