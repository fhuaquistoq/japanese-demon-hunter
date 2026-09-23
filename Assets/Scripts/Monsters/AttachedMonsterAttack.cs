using System;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class AttachedMonsterAttack : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float attackRange = 3.2f;
        [SerializeField, Min(0f)] private float impactDelay = 0.45f;
        [SerializeField, Min(0f)] private float cooldown = 2.5f;
        [SerializeField, Range(0.5f, 2f)] private float attackAnimationSpeed = 1f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        private MonsterBase owner;
        private MonsterAttachment attachment;
        private PrototypeHunterMonsterTarget hunter;
        private MonsterAnimationController animationController;
        private bool pending;
        private float impactAt;
        private float nextAttackAt;

        public bool IsPreparingAttack => pending;
        public int ResolvedAttackCount { get; private set; }
        public int SuccessfulAttackCount { get; private set; }
        public float ImpactDelay => impactDelay;
        public float Cooldown => cooldown;
        public float AttackAnimationSpeed => attackAnimationSpeed;

        public event Action<MonsterBase, bool> ThreatResolved;

        private void Update()
        {
            TickAttack(Time.time);
        }

        /// <summary>
        /// Runs one repeatable attack-cycle step. Public so the behaviour can be verified
        /// deterministically without waiting for wall-clock time.
        /// </summary>
        public void TickAttack(float currentTime)
        {
            if (owner == null || attachment == null || hunter == null || owner.IsDead || !attachment.IsAttached)
            {
                pending = false;
                return;
            }

            if (!pending)
            {
                if (currentTime >= nextAttackAt && IsHunterInRange())
                {
                    pending = true;
                    impactAt = currentTime + impactDelay;
                    animationController?.PlayAttack(attackAnimationSpeed);
                }
                return;
            }

            if (currentTime < impactAt)
            {
                return;
            }

            bool hit = IsHunterInRange() && HasClearImpact() && hunter.TryReceiveHit(owner);
            pending = false;
            nextAttackAt = currentTime + cooldown;
            ResolvedAttackCount++;
            if (hit)
            {
                SuccessfulAttackCount++;
            }
            animationController?.PlayLocomotion();
            ThreatResolved?.Invoke(owner, hit);
        }

        public void Initialize(
            MonsterBase configuredOwner,
            MonsterAttachment configuredAttachment,
            PrototypeHunterMonsterTarget configuredHunter,
            MonsterAnimationController configuredAnimation)
        {
            owner = configuredOwner;
            attachment = configuredAttachment;
            hunter = configuredHunter;
            animationController = configuredAnimation;
            pending = false;
            nextAttackAt = 0f;
            ResolvedAttackCount = 0;
            SuccessfulAttackCount = 0;
        }

        public void CancelAttack()
        {
            pending = false;
        }

        public void Configure(
            float configuredRange,
            float configuredImpactDelay,
            float configuredCooldown,
            LayerMask configuredMask,
            float configuredAnimationSpeed = 1f)
        {
            attackRange = Mathf.Max(0.1f, configuredRange);
            impactDelay = Mathf.Max(0f, configuredImpactDelay);
            cooldown = Mathf.Max(0f, configuredCooldown);
            obstructionMask = configuredMask;
            attackAnimationSpeed = Mathf.Clamp(configuredAnimationSpeed, 0.5f, 2f);
        }

        private bool IsHunterInRange()
        {
            return hunter.IsAvailable &&
                   (hunter.AttackPoint.position - transform.position).sqrMagnitude <= attackRange * attackRange;
        }

        private bool HasClearImpact()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 target = hunter.AttackPoint.position;
            Vector3 direction = target - origin;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                return true;
            }

            Transform cartRoot = attachment.ReservedPoint != null ? attachment.ReservedPoint.transform.root : null;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction / distance, distance, obstructionMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform.IsChildOf(transform) ||
                    hitTransform == hunter.Root || hitTransform.IsChildOf(hunter.Root) ||
                    (cartRoot != null && hitTransform.IsChildOf(cartRoot)))
                {
                    continue;
                }
                return false;
            }

            return true;
        }
    }
}
