using System;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonsterBase))]
    public sealed class MonsterDamageable : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float maximumHealth = 30f;
        [SerializeField] private bool vulnerableToNormalSword = true;

        private MonsterBase owner;
        private float currentHealth;

        public float MaximumHealth => maximumHealth;
        public float CurrentHealth => currentHealth;
        public bool IsAlive => owner != null && !owner.IsDead && currentHealth > 0f;
        public bool VulnerableToNormalSword => vulnerableToNormalSword;

        public event Action<MonsterDamageable, float, Component> Damaged;
        public event Action<MonsterDamageable> Killed;

        private void Awake()
        {
            owner = GetComponent<MonsterBase>();
            currentHealth = maximumHealth;
        }

        private void OnEnable()
        {
            currentHealth = maximumHealth;
        }

        public bool ApplyDamage(float amount, Component source)
        {
            if (!vulnerableToNormalSword || !IsAlive || amount <= 0f)
            {
                return false;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Damaged?.Invoke(this, amount, source);
            if (currentHealth <= 0f)
            {
                owner.Kill();
                Killed?.Invoke(this);
            }

            return true;
        }

        public void Configure(float configuredMaximumHealth, bool configuredSwordVulnerability)
        {
            maximumHealth = Mathf.Max(0.01f, configuredMaximumHealth);
            vulnerableToNormalSword = configuredSwordVulnerability;
            currentHealth = maximumHealth;
        }
    }
}
