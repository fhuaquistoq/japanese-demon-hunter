using System;
using JapaneseDemonHunter.Prototype;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class PrototypeCandleMonsterTarget : MonoBehaviour, IMonsterTarget
    {
        [SerializeField] private PrototypeCandle candle;

        public MonsterTargetType TargetType => MonsterTargetType.Candle;
        public Transform Root => candle != null ? candle.transform : transform;
        public Transform AttackPoint => candle != null && candle.AttackPoint != null ? candle.AttackPoint : Root;
        public bool IsAvailable => candle != null && candle.IsLit && candle.gameObject.activeInHierarchy;

        public event Action<IMonsterTarget> AvailabilityChanged;

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public bool TryReceiveHit(MonsterBase attacker)
        {
            return IsAvailable && candle.RequestExtinguish();
        }

        public void Configure(PrototypeCandle configuredCandle)
        {
            Unsubscribe();
            candle = configuredCandle;
            Subscribe();
        }

        private void Subscribe()
        {
            if (candle != null)
            {
                candle.Extinguished -= HandleCandleExtinguished;
                candle.Extinguished += HandleCandleExtinguished;
            }
        }

        private void Unsubscribe()
        {
            if (candle != null)
            {
                candle.Extinguished -= HandleCandleExtinguished;
            }
        }

        private void HandleCandleExtinguished(PrototypeCandle extinguishedCandle)
        {
            AvailabilityChanged?.Invoke(this);
        }
    }
}
