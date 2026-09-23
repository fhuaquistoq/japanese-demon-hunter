using System;
using System.Collections.Generic;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class MonsterTargetSelector : MonoBehaviour
    {
        [SerializeField] private MonsterTargetStrategy strategy = MonsterTargetStrategy.ClosestLitCandle;
        [SerializeField, Min(0f)] private float minimumSelectionInterval = 1.25f;

        private readonly List<IMonsterTarget> candidates = new List<IMonsterTarget>(5);
        private MonsterTargetRegistry registry;
        private float nextSelectionTime;

        public MonsterTargetStrategy Strategy
        {
            get => strategy;
            set => strategy = value;
        }

        public IMonsterTarget CurrentTarget { get; private set; }
        public bool HasValidTarget => CurrentTarget != null && CurrentTarget.IsAvailable;

        public event Action<IMonsterTarget> TargetChanged;

        public void Initialize(MonsterTargetRegistry configuredRegistry)
        {
            registry = configuredRegistry;
            ClearTarget();
            nextSelectionTime = 0f;
        }

        public IMonsterTarget SelectTarget(Vector3 origin, bool force = false)
        {
            if (!force && HasValidTarget && Time.time < nextSelectionTime)
            {
                return CurrentTarget;
            }

            if (registry == null)
            {
                SetTarget(null);
                return null;
            }

            registry.GetAvailableTargets(candidates);
            IMonsterTarget selected = strategy switch
            {
                MonsterTargetStrategy.Closest => FindClosest(origin, null),
                MonsterTargetStrategy.ClosestLitCandle => FindClosest(origin, MonsterTargetType.Candle),
                MonsterTargetStrategy.RandomLitCandle => FindRandomCandle(),
                MonsterTargetStrategy.PrioritizeHunter => FindClosest(origin, MonsterTargetType.Hunter) ??
                                                          FindClosest(origin, MonsterTargetType.Candle),
                _ => FindClosest(origin, null)
            };

            SetTarget(selected);
            nextSelectionTime = Time.time + minimumSelectionInterval;
            return CurrentTarget;
        }

        public bool HasAvailableTargetWithin(Vector3 origin, float radius)
        {
            if (registry == null)
            {
                return false;
            }

            float radiusSquared = radius * radius;
            registry.GetAvailableTargets(candidates);
            foreach (IMonsterTarget candidate in candidates)
            {
                if ((candidate.AttackPoint.position - origin).sqrMagnitude <= radiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        public void ClearTarget()
        {
            SetTarget(null);
        }

        private IMonsterTarget FindClosest(Vector3 origin, MonsterTargetType? requiredType)
        {
            IMonsterTarget closest = null;
            float closestDistanceSquared = float.PositiveInfinity;

            foreach (IMonsterTarget candidate in candidates)
            {
                if (requiredType.HasValue && candidate.TargetType != requiredType.Value)
                {
                    continue;
                }

                float distanceSquared = (candidate.AttackPoint.position - origin).sqrMagnitude;
                if (distanceSquared < closestDistanceSquared)
                {
                    closest = candidate;
                    closestDistanceSquared = distanceSquared;
                }
            }

            return closest;
        }

        private IMonsterTarget FindRandomCandle()
        {
            int candleCount = 0;
            foreach (IMonsterTarget candidate in candidates)
            {
                if (candidate.TargetType == MonsterTargetType.Candle)
                {
                    candleCount++;
                }
            }

            if (candleCount == 0)
            {
                return null;
            }

            int chosenIndex = UnityEngine.Random.Range(0, candleCount);
            foreach (IMonsterTarget candidate in candidates)
            {
                if (candidate.TargetType != MonsterTargetType.Candle)
                {
                    continue;
                }

                if (chosenIndex-- == 0)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void SetTarget(IMonsterTarget target)
        {
            if (ReferenceEquals(CurrentTarget, target))
            {
                return;
            }

            CurrentTarget = target;
            TargetChanged?.Invoke(CurrentTarget);
        }
    }
}
