using JapaneseDemonHunter.Monsters;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>Brief physical fall after a weapon's final hit; normal AI movement stays unchanged.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonsterDamageable))]
    public sealed class MonsterDeathPhysics : MonoBehaviour
    {
        private MonsterDamageable damageable;
        private Rigidbody body;
        private Component lastAttacker;
        private float mass = 12f;
        private float knockbackImpulse = 4f;
        private float upwardImpulse = 1.5f;

        public void Configure(float configuredMass, float configuredKnockback, float configuredUpward)
        {
            mass = Mathf.Max(0.1f, configuredMass);
            knockbackImpulse = Mathf.Max(0f, configuredKnockback);
            upwardImpulse = Mathf.Max(0f, configuredUpward);
        }

        private void OnEnable()
        {
            damageable = GetComponent<MonsterDamageable>();
            if (damageable == null) return;
            damageable.Damaged += RememberAttacker;
            damageable.Killed += FallOnDeath;
        }

        private void OnDisable()
        {
            if (damageable != null)
            {
                damageable.Damaged -= RememberAttacker;
                damageable.Killed -= FallOnDeath;
            }
        }

        private void RememberAttacker(MonsterDamageable monster, float amount, Component attacker)
        {
            lastAttacker = attacker;
        }

        private void FallOnDeath(MonsterDamageable monster)
        {
            Collider collision = GetComponent<Collider>();
            if (collision == null) return;
            collision.enabled = true; // MonsterBase disables its hit collider after death.
            collision.isTrigger = false;

            body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.useGravity = true;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            Vector3 away = lastAttacker != null
                ? transform.position - lastAttacker.transform.position : transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) away = transform.forward;
            body.AddForce(away.normalized * knockbackImpulse + Vector3.up * upwardImpulse,
                ForceMode.Impulse);
        }
    }
}
