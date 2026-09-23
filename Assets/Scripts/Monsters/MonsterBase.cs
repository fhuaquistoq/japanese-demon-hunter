using System;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class MonsterBase : MonoBehaviour
    {
        [Header("Required components")]
        [SerializeField] private MonsterMovement movement;
        [SerializeField] private MonsterTargetSelector targetSelector;
        [SerializeField] private MonsterAttack attack;
        [SerializeField] private MonsterAnimationController animationController;
        [SerializeField] private Collider bodyCollider;
        [SerializeField] private MonsterAttachment attachment;

        [Header("Finite state machine")]
        [SerializeField, Min(0f)] private float spawnDuration = 0.25f;
        [SerializeField, Min(0.5f)] private float patrolRadius = 8f;
        [SerializeField, Min(0.5f)] private float detectionRadius = 40f;
        [SerializeField, Min(0.1f)] private float attackDistance = 1.5f;
        [SerializeField, Min(1f)] private float maximumChaseDistance = 65f;
        [SerializeField, Min(0f)] private float retreatTimeout = 8f;
        [SerializeField, Min(0f)] private float deathDisableDelay = 2.2f;

        private MonsterSpawnContext spawnContext;
        private float stateElapsed;
        private bool initialized;
        private bool inactiveNotificationSent;

        public MonsterState State { get; private set; } = MonsterState.Spawn;
        public bool IsDead => State == MonsterState.Dead;
        public bool IsInitialized => initialized;
        public IMonsterTarget CurrentTarget => targetSelector != null ? targetSelector.CurrentTarget : null;
        public MonsterMovementType MovementType => movement != null ? movement.MovementType : MonsterMovementType.Ground;
        public MonsterAttachment Attachment => attachment;
        public bool UsesCartAttachment => attachment != null;

        public event Action<MonsterBase, MonsterState, MonsterState> StateChanged;
        public event Action<MonsterBase> BecameInactive;
        public event Action<MonsterBase> Died;

        private void Awake()
        {
            ResolveLocalReferences();
        }

        private void OnEnable()
        {
            inactiveNotificationSent = false;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            if (initialized)
            {
                NotifyInactiveOnce();
            }
        }

        private void OnDestroy()
        {
            if (initialized)
            {
                NotifyInactiveOnce();
            }
        }

        public void Initialize(MonsterSpawnContext context)
        {
            ResolveLocalReferences();
            spawnContext = context;
            initialized = true;
            inactiveNotificationSent = false;

            if (bodyCollider != null)
            {
                bodyCollider.enabled = true;
            }

            targetSelector.Initialize(context.targetRegistry);
            movement.Initialize(context.patrolCenter);
            attack.Initialize(this);
            if (attachment != null)
            {
                attachment.Initialize(this, context.attachmentPoints, context.cartLoad);
                AttachedMonsterAttack attachedAttack = GetComponent<AttachedMonsterAttack>();
                if (attachedAttack != null)
                {
                    attachedAttack.Initialize(this, attachment, context.hunterTarget, animationController);
                }
            }
            SetState(MonsterState.Spawn, true);
        }

        public void Tick(float deltaTime)
        {
            if (!initialized || deltaTime <= 0f)
            {
                return;
            }

            if (spawnContext.cartTransform != null)
            {
                movement.UpdatePatrolCenter(spawnContext.cartTransform.position);
            }

            stateElapsed += deltaTime;
            switch (State)
            {
                case MonsterState.Spawn:
                    TickSpawn();
                    break;
                case MonsterState.Patrol:
                    TickPatrol(deltaTime);
                    break;
                case MonsterState.SelectTarget:
                    TickSelectTarget();
                    break;
                case MonsterState.Chase:
                    TickChase(deltaTime);
                    break;
                case MonsterState.Approach:
                    TickApproach(deltaTime);
                    break;
                case MonsterState.Attack:
                    TickAttack();
                    break;
                case MonsterState.Retreat:
                    TickRetreat(deltaTime);
                    break;
                case MonsterState.SelectAttachment:
                    TickSelectAttachment(deltaTime);
                    break;
                case MonsterState.ChaseAttachment:
                    TickChaseAttachment(deltaTime);
                    break;
                case MonsterState.Attach:
                    TickAttach(deltaTime);
                    break;
                case MonsterState.Attached:
                    TickAttached();
                    break;
                case MonsterState.Dead:
                    TickDead();
                    break;
            }
        }

        /// <summary>Public Phase 3 integration point for the future pronunciation system.</summary>
        [ContextMenu("Kill (Phase 2 Test)")]
        public void Kill()
        {
            if (IsDead)
            {
                return;
            }

            attack.CancelAttack();
            GetComponent<AttachedMonsterAttack>()?.CancelAttack();
            targetSelector.ClearTarget();
            movement.Stop();
            if (attachment != null)
            {
                attachment.Detach();
            }
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            SetState(MonsterState.Dead, true);
            Died?.Invoke(this);
        }

        public void Retire()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            attack.CancelAttack();
            GetComponent<AttachedMonsterAttack>()?.CancelAttack();
            movement.Stop();
            if (attachment != null)
            {
                attachment.Detach();
            }
            NotifyInactiveOnce();
            gameObject.SetActive(false);
        }

        public void Configure(
            MonsterMovement configuredMovement,
            MonsterTargetSelector configuredTargetSelector,
            MonsterAttack configuredAttack,
            MonsterAnimationController configuredAnimationController,
            Collider configuredBodyCollider,
            float configuredDetectionRadius,
            float configuredAttackDistance,
            float configuredMaximumChaseDistance,
            float configuredDeathDisableDelay)
        {
            movement = configuredMovement;
            targetSelector = configuredTargetSelector;
            attack = configuredAttack;
            animationController = configuredAnimationController;
            bodyCollider = configuredBodyCollider;
            detectionRadius = Mathf.Max(0.5f, configuredDetectionRadius);
            attackDistance = Mathf.Max(0.1f, configuredAttackDistance);
            maximumChaseDistance = Mathf.Max(1f, configuredMaximumChaseDistance);
            deathDisableDelay = Mathf.Max(0f, configuredDeathDisableDelay);
        }

        private void TickSpawn()
        {
            movement.Stop();
            if (stateElapsed >= spawnDuration)
            {
                SetState(attachment != null ? MonsterState.SelectAttachment : MonsterState.Patrol);
            }
        }

        private void TickPatrol(float deltaTime)
        {
            movement.TickPatrol(patrolRadius, deltaTime);
            if (targetSelector.HasAvailableTargetWithin(transform.position, detectionRadius))
            {
                SetState(MonsterState.SelectTarget);
            }
        }

        private void TickSelectTarget()
        {
            IMonsterTarget selected = targetSelector.SelectTarget(transform.position, true);
            SetState(selected != null ? MonsterState.Chase : MonsterState.Patrol);
        }

        private void TickChase(float deltaTime)
        {
            if (!EnsureValidTarget())
            {
                return;
            }

            if (HasExceededChaseDistance())
            {
                SetState(MonsterState.Retreat);
                return;
            }

            float distance = Vector3.Distance(transform.position, CurrentTarget.AttackPoint.position);
            if (distance <= attackDistance * 1.55f)
            {
                SetState(MonsterState.Approach);
                return;
            }

            movement.TickMoveTowards(CurrentTarget.AttackPoint.position, movement.ChaseSpeed, deltaTime);
        }

        private void TickApproach(float deltaTime)
        {
            if (!EnsureValidTarget())
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, CurrentTarget.AttackPoint.position);
            if (distance > attackDistance * 2.25f)
            {
                SetState(MonsterState.Chase);
                return;
            }

            if (distance > attackDistance)
            {
                movement.TickMoveTowards(CurrentTarget.AttackPoint.position, movement.ApproachSpeed, deltaTime);
                return;
            }

            movement.Stop();
            if (attack.TryBeginAttack(CurrentTarget))
            {
                SetState(MonsterState.Attack);
            }
        }

        private void TickAttack()
        {
            movement.Stop();
            MonsterAttackResult result = attack.TickAttack();
            if (result == MonsterAttackResult.Pending)
            {
                return;
            }

            if (CurrentTarget == null || !CurrentTarget.IsAvailable)
            {
                SetState(MonsterState.SelectTarget);
            }
            else
            {
                SetState(MonsterState.Approach);
            }
        }

        private void TickRetreat(float deltaTime)
        {
            targetSelector.ClearTarget();
            Vector3 retreatPoint = CurrentPatrolCenter;
            if (MovementType == MonsterMovementType.Flying)
            {
                retreatPoint += Vector3.up * 3f;
            }

            movement.TickMoveTowards(retreatPoint, movement.PatrolSpeed, deltaTime);
            if (Vector3.Distance(transform.position, retreatPoint) <= 1.5f)
            {
                SetState(MonsterState.Patrol);
            }
            else if (retreatTimeout > 0f && stateElapsed >= retreatTimeout)
            {
                Retire();
            }
        }

        private void TickDead()
        {
            movement.Stop();
            if (stateElapsed >= deathDisableDelay)
            {
                Retire();
            }
        }

        private void TickSelectAttachment(float deltaTime)
        {
            if (attachment == null)
            {
                SetState(MonsterState.Patrol);
                return;
            }

            if (attachment.TrySelectPoint())
            {
                SetState(MonsterState.ChaseAttachment);
                return;
            }

            movement.TickPatrol(patrolRadius, deltaTime);
        }

        private void TickChaseAttachment(float deltaTime)
        {
            if (attachment == null || !attachment.CanReachReservedPoint())
            {
                attachment?.Detach();
                SetState(MonsterState.SelectAttachment);
                return;
            }

            if (attachment.IsWithinReach())
            {
                movement.Stop();
                SetState(MonsterState.Attach);
                return;
            }

            movement.TickMoveTowards(attachment.Destination, movement.ChaseSpeed, deltaTime);
        }

        private void TickAttach(float deltaTime)
        {
            movement.Stop();
            if (attachment == null || !attachment.CanReachReservedPoint())
            {
                attachment?.Detach();
                SetState(MonsterState.SelectAttachment);
                return;
            }

            if (attachment.TickAttach(deltaTime))
            {
                SetState(MonsterState.Attached);
            }
        }

        private void TickAttached()
        {
            movement.Stop();
            if (attachment == null || !attachment.IsAttached)
            {
                SetState(MonsterState.SelectAttachment);
                return;
            }

            attachment.TickAttached();
        }

        private bool EnsureValidTarget()
        {
            if (CurrentTarget != null && CurrentTarget.IsAvailable)
            {
                return true;
            }

            attack.CancelAttack();
            SetState(MonsterState.SelectTarget);
            return false;
        }

        private bool HasExceededChaseDistance()
        {
            return (transform.position - CurrentPatrolCenter).sqrMagnitude >
                   maximumChaseDistance * maximumChaseDistance;
        }

        private Vector3 CurrentPatrolCenter =>
            spawnContext.cartTransform != null ? spawnContext.cartTransform.position : spawnContext.patrolCenter;

        private void SetState(MonsterState nextState, bool force = false)
        {
            if (!force && State == nextState)
            {
                return;
            }

            MonsterState previousState = State;
            State = nextState;
            stateElapsed = 0f;

            switch (State)
            {
                case MonsterState.Patrol:
                case MonsterState.Chase:
                case MonsterState.Approach:
                case MonsterState.Retreat:
                case MonsterState.SelectAttachment:
                case MonsterState.ChaseAttachment:
                    animationController.PlayLocomotion();
                    break;
                case MonsterState.Attack:
                case MonsterState.Attach:
                    animationController.PlayAttack();
                    break;
                case MonsterState.Attached:
                    animationController.PlayLocomotion();
                    break;
                case MonsterState.Dead:
                    animationController.PlayDeath();
                    break;
            }

            StateChanged?.Invoke(this, previousState, State);
        }

        private void ResolveLocalReferences()
        {
            if (movement == null) movement = GetComponent<MonsterMovement>();
            if (targetSelector == null) targetSelector = GetComponent<MonsterTargetSelector>();
            if (attack == null) attack = GetComponent<MonsterAttack>();
            if (animationController == null) animationController = GetComponent<MonsterAnimationController>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider>();
            if (attachment == null) attachment = GetComponent<MonsterAttachment>();
        }

        private void NotifyInactiveOnce()
        {
            if (inactiveNotificationSent)
            {
                return;
            }

            inactiveNotificationSent = true;
            BecameInactive?.Invoke(this);
        }
    }
}
