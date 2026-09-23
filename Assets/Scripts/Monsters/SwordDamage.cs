using System;
using System.Collections.Generic;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class SwordDamage : MonoBehaviour
    {
        [SerializeField] private Transform sweepTip;
        [SerializeField] private Transform velocityReference;
        [SerializeField, Min(0.01f)] private float damage = 35f;
        [SerializeField, Min(0.01f)] private float sweepRadius = 0.18f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private bool allowVelocityActivatedWindows = true;
        [SerializeField, Min(0.1f)] private float minimumSwingSpeed = 2.5f;
        [SerializeField, Min(0.02f)] private float velocityWindowGrace = 0.12f;

        private readonly HashSet<MonsterDamageable> hitThisWindow = new HashSet<MonsterDamageable>();
        private Vector3 previousWorldTipPosition;
        private Vector3 previousRelativeTipPosition;
        private bool explicitWindow;
        private float velocityWindowUntil;

        public bool IsAttackWindowOpen => explicitWindow || Time.time < velocityWindowUntil;
        public int UniqueHitsThisWindow => hitThisWindow.Count;

        public event Action AttackWindowOpened;
        public event Action AttackWindowClosed;
        public event Action<MonsterDamageable> MonsterHit;

        private void OnEnable()
        {
            previousWorldTipPosition = TipPosition;
            previousRelativeTipPosition = RelativeTipPosition;
            explicitWindow = false;
            velocityWindowUntil = 0f;
            hitThisWindow.Clear();
        }

        private void Update()
        {
            Vector3 currentWorld = TipPosition;
            Vector3 currentRelative = RelativeTipPosition;
            float speed = (currentRelative - previousRelativeTipPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            if (allowVelocityActivatedWindows && speed >= minimumSwingSpeed)
            {
                if (!IsAttackWindowOpen)
                {
                    OpenNewWindow();
                }
                velocityWindowUntil = Time.time + velocityWindowGrace;
            }

            if (IsAttackWindowOpen)
            {
                Sweep(previousWorldTipPosition, currentWorld);
            }

            previousWorldTipPosition = currentWorld;
            previousRelativeTipPosition = currentRelative;
        }

        public void BeginAttackWindow()
        {
            if (!explicitWindow)
            {
                OpenNewWindow();
            }
            explicitWindow = true;
        }

        public void EndAttackWindow()
        {
            bool wasOpen = IsAttackWindowOpen;
            explicitWindow = false;
            velocityWindowUntil = 0f;
            if (wasOpen)
            {
                AttackWindowClosed?.Invoke();
            }
        }

        public int Sweep(Vector3 from, Vector3 to)
        {
            if (!IsAttackWindowOpen)
            {
                return 0;
            }

            int hitCount = 0;
            Collider[] colliders = Physics.OverlapCapsule(
                from,
                to,
                sweepRadius,
                targetMask,
                QueryTriggerInteraction.Collide);
            foreach (Collider candidate in colliders)
            {
                MonsterDamageable damageable = candidate != null
                    ? candidate.GetComponentInParent<MonsterDamageable>()
                    : null;
                if (damageable == null || hitThisWindow.Contains(damageable))
                {
                    continue;
                }

                hitThisWindow.Add(damageable);
                if (damageable.ApplyDamage(damage, this))
                {
                    hitCount++;
                    MonsterHit?.Invoke(damageable);
                }
            }

            return hitCount;
        }

        public void Configure(Transform configuredTip, float configuredDamage, float configuredRadius, LayerMask configuredMask)
        {
            sweepTip = configuredTip;
            damage = Mathf.Max(0.01f, configuredDamage);
            sweepRadius = Mathf.Max(0.01f, configuredRadius);
            targetMask = configuredMask;
        }

        /// <summary>
        /// The reference whose motion must be ignored when measuring swing speed. A sword carried by
        /// a moving cart would otherwise always look like it is being swung.
        /// </summary>
        public void ConfigureVelocityReference(Transform configuredReference)
        {
            velocityReference = configuredReference;
            previousWorldTipPosition = TipPosition;
            previousRelativeTipPosition = RelativeTipPosition;
        }

        private Vector3 TipPosition => sweepTip != null ? sweepTip.position : transform.position;

        private Vector3 RelativeTipPosition => velocityReference != null
            ? TipPosition - velocityReference.position
            : TipPosition;

        private void OpenNewWindow()
        {
            hitThisWindow.Clear();
            AttackWindowOpened?.Invoke();
        }
    }
}
