using System;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class PrototypeHunterMonsterTarget : MonoBehaviour, IMonsterTarget
    {
        [SerializeField] private Transform hunterRoot;
        [SerializeField] private Transform attackPoint;

        public MonsterTargetType TargetType => MonsterTargetType.Hunter;
        public Transform Root => hunterRoot != null ? hunterRoot : transform;
        public Transform AttackPoint => attackPoint != null ? attackPoint : Root;
        public bool IsAvailable => Root != null && Root.gameObject.activeInHierarchy;

        public event Action<IMonsterTarget> AvailabilityChanged;
        public event Action<MonsterBase> SimulatedHit;

        public bool TryReceiveHit(MonsterBase attacker)
        {
            if (!IsAvailable)
            {
                return false;
            }

            Debug.Log($"{attacker.name} hit the hunter.", this);
            SimulatedHit?.Invoke(attacker);
            return true;
        }

        public void Configure(Transform configuredHunterRoot, Transform configuredAttackPoint)
        {
            hunterRoot = configuredHunterRoot;
            attackPoint = configuredAttackPoint;
            AvailabilityChanged?.Invoke(this);
        }
    }
}
