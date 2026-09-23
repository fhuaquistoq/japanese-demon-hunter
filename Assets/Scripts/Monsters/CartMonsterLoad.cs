using System;
using System.Collections.Generic;
using System.Linq;
using JapaneseDemonHunter.Prototype;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class CartMonsterLoad : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float referenceMaximumLoad = 60f;
        [SerializeField, Range(0f, 1f)] private float minimumSpeedMultiplier = 0.3f;
        [SerializeField] private MonoBehaviour speedPenaltyReceiver;

        private readonly Dictionary<MonsterAttachment, float> registrations = new Dictionary<MonsterAttachment, float>();
        private ICartSpeedPenaltyReceiver receiver;

        public float TotalLoad => registrations.Values.Sum();
        public int RegisteredMonsterCount => registrations.Count;

        /// <summary>True when a speed penalty receiver is wired, even before Awake resolves the cache.</summary>
        public bool HasReceiver => receiver != null || speedPenaltyReceiver is ICartSpeedPenaltyReceiver;
        public float ReferenceMaximumLoad => referenceMaximumLoad;
        public float MinimumSpeedMultiplier => minimumSpeedMultiplier;
        public float SpeedMultiplier => Mathf.Clamp(
            1f - TotalLoad / Mathf.Max(0.01f, referenceMaximumLoad),
            minimumSpeedMultiplier,
            1f);

        public event Action<float, float> LoadChanged;

        private void Awake()
        {
            ResolveReceiver();
            ApplyMultiplier();
        }

        private void OnDisable()
        {
            ClearAll();
        }

        public bool Register(MonsterAttachment monster, float load)
        {
            if (monster == null || registrations.ContainsKey(monster))
            {
                return false;
            }

            registrations.Add(monster, Mathf.Max(0f, load));
            ApplyMultiplier();
            return true;
        }

        public bool Unregister(MonsterAttachment monster)
        {
            if (monster == null || !registrations.Remove(monster))
            {
                return false;
            }

            ApplyMultiplier();
            return true;
        }

        public void ClearAll()
        {
            if (registrations.Count == 0)
            {
                ApplyMultiplier();
                return;
            }

            registrations.Clear();
            ApplyMultiplier();
        }

        public void Configure(float configuredMaximumLoad, float configuredMinimumMultiplier, MonoBehaviour configuredReceiver)
        {
            referenceMaximumLoad = Mathf.Max(0.01f, configuredMaximumLoad);
            minimumSpeedMultiplier = Mathf.Clamp01(configuredMinimumMultiplier);
            speedPenaltyReceiver = configuredReceiver;
            ResolveReceiver();
            ApplyMultiplier();
        }

        private void ResolveReceiver()
        {
            receiver = speedPenaltyReceiver as ICartSpeedPenaltyReceiver;
            if (receiver == null)
            {
                foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                {
                    if (component is ICartSpeedPenaltyReceiver found)
                    {
                        receiver = found;
                        speedPenaltyReceiver = component;
                        break;
                    }
                }
            }
        }

        private void ApplyMultiplier()
        {
            receiver?.SetMonsterLoadMultiplier(SpeedMultiplier);
            LoadChanged?.Invoke(TotalLoad, SpeedMultiplier);
        }
    }
}
