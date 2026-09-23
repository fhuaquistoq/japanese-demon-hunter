using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class MonsterTargetRegistry : MonoBehaviour
    {
        [SerializeField] private List<MonoBehaviour> targetComponents = new List<MonoBehaviour>();

        private readonly List<IMonsterTarget> targets = new List<IMonsterTarget>();

        public IReadOnlyList<IMonsterTarget> Targets
        {
            get
            {
                EnsureCacheIsCurrent();
                return targets;
            }
        }

        public bool IsConfigured => targetComponents.Count > 0 &&
                                    targetComponents.All(component => component is IMonsterTarget);

        public event Action TargetsChanged;

        private void Awake()
        {
            RebuildCache();
        }

        private void OnEnable()
        {
            RebuildCache();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        public void GetAvailableTargets(List<IMonsterTarget> results)
        {
            EnsureCacheIsCurrent();
            results.Clear();
            foreach (IMonsterTarget target in targets)
            {
                if (target != null && target.IsAvailable)
                {
                    results.Add(target);
                }
            }
        }

        public void Configure(IEnumerable<MonoBehaviour> configuredTargets)
        {
            targetComponents = configuredTargets != null
                ? configuredTargets.Where(component => component is IMonsterTarget).Distinct().ToList()
                : new List<MonoBehaviour>();
            RebuildCache();
        }

        public void RebuildCache()
        {
            UnsubscribeAll();
            targets.Clear();

            foreach (MonoBehaviour component in targetComponents)
            {
                if (component is IMonsterTarget target && !targets.Contains(target))
                {
                    targets.Add(target);
                    target.AvailabilityChanged += HandleAvailabilityChanged;
                }
            }

            TargetsChanged?.Invoke();
        }

        private void UnsubscribeAll()
        {
            foreach (IMonsterTarget target in targets)
            {
                if (target != null)
                {
                    target.AvailabilityChanged -= HandleAvailabilityChanged;
                }
            }
        }

        private void HandleAvailabilityChanged(IMonsterTarget target)
        {
            TargetsChanged?.Invoke();
        }

        private void EnsureCacheIsCurrent()
        {
            if (targets.Count != targetComponents.Count ||
                targets.Any(target => target == null))
            {
                RebuildCache();
            }
        }
    }
}
