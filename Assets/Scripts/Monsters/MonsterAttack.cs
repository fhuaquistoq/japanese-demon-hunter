using System;
using System.Linq;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class MonsterAttack : MonoBehaviour
    {
        [SerializeField] private Transform attackOrigin;
        [SerializeField, Min(0.05f)] private float attackRange = 1.65f;
        [SerializeField, Min(0f)] private float impactDelay = 0.45f;
        [SerializeField, Min(0f)] private float cooldown = 2f;
        [SerializeField, Min(0.05f)] private float impactConfirmationRadius = 0.8f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        private MonsterBase owner;
        private IMonsterTarget pendingTarget;
        private float impactTime;
        private float nextAttackTime;

        public bool IsAttacking => pendingTarget != null;
        public bool IsReady => !IsAttacking && Time.time >= nextAttackTime;
        public float AttackRange => attackRange;

        public event Action<IMonsterTarget, bool> AttackResolved;

        public void Initialize(MonsterBase configuredOwner)
        {
            owner = configuredOwner;
            CancelAttack();
            nextAttackTime = 0f;
        }

        public bool TryBeginAttack(IMonsterTarget target)
        {
            if (owner == null || owner.IsDead || target == null || !target.IsAvailable || !IsReady)
            {
                return false;
            }

            pendingTarget = target;
            impactTime = Time.time + impactDelay;
            return true;
        }

        public MonsterAttackResult TickAttack()
        {
            if (!IsAttacking)
            {
                return MonsterAttackResult.Cancelled;
            }

            if (owner == null || owner.IsDead || pendingTarget == null || !pendingTarget.IsAvailable)
            {
                CancelAttack();
                return MonsterAttackResult.Cancelled;
            }

            if (Time.time < impactTime)
            {
                return MonsterAttackResult.Pending;
            }

            IMonsterTarget target = pendingTarget;
            bool hit = ConfirmImpact(target) && target.TryReceiveHit(owner);
            pendingTarget = null;
            nextAttackTime = Time.time + cooldown;
            AttackResolved?.Invoke(target, hit);
            return hit ? MonsterAttackResult.Hit : MonsterAttackResult.Missed;
        }

        public void CancelAttack()
        {
            pendingTarget = null;
        }

        public void Configure(
            Transform configuredAttackOrigin,
            float configuredAttackRange,
            float configuredImpactDelay,
            float configuredCooldown,
            LayerMask configuredObstructionMask)
        {
            attackOrigin = configuredAttackOrigin;
            attackRange = Mathf.Max(0.05f, configuredAttackRange);
            impactDelay = Mathf.Max(0f, configuredImpactDelay);
            cooldown = Mathf.Max(0f, configuredCooldown);
            obstructionMask = configuredObstructionMask;
        }

        private bool ConfirmImpact(IMonsterTarget target)
        {
            Transform originTransform = attackOrigin != null ? attackOrigin : transform;
            Vector3 origin = originTransform.position;
            Vector3 targetPoint = target.AttackPoint.position;
            Vector3 toTarget = targetPoint - origin;
            float distance = toTarget.magnitude;

            if (distance > attackRange || distance <= 0.0001f)
            {
                return false;
            }

            if (IsObstructed(origin, toTarget / distance, distance, target.Root))
            {
                return false;
            }

            Collider[] nearbyColliders = Physics.OverlapSphere(
                targetPoint,
                impactConfirmationRadius,
                ~0,
                QueryTriggerInteraction.Ignore);

            return nearbyColliders.Any(collider =>
                collider != null &&
                (collider.transform == target.Root || collider.transform.IsChildOf(target.Root)));
        }

        private bool IsObstructed(Vector3 origin, Vector3 direction, float distance, Transform targetRoot)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                distance,
                obstructionMask,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (hitTransform == targetRoot || hitTransform.IsChildOf(targetRoot))
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
