using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class MonsterAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string locomotionState = "Locomotion";
        [SerializeField] private string attackState = "Attack";
        [SerializeField] private string deathState = "Death";
        [SerializeField, Min(0f)] private float transitionDuration = 0.15f;

        private int locomotionHash;
        private int attackHash;
        private int deathHash;
        private int currentHash;

        public Animator Animator => animator;
        public bool HasAttackAnimation => HasState(attackHash);
        public bool HasDeathAnimation => HasState(deathHash);

        private void Awake()
        {
            CacheHashes();
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        public void PlayLocomotion(float playbackSpeed = 1f)
        {
            SetPlaybackSpeed(playbackSpeed);
            PlayState(locomotionHash);
        }

        public void PlayAttack(float playbackSpeed = 1f)
        {
            SetPlaybackSpeed(playbackSpeed);
            // Attacks are repeatable while a monster remains attached. Restart the clip even
            // when the animator is already in Attack; otherwise only the first swing is visible.
            PlayState(attackHash, true);
        }

        public void PlayDeath()
        {
            SetPlaybackSpeed(1f);
            PlayState(deathHash);
        }

        public void Configure(Animator configuredAnimator, string configuredLocomotionState, string configuredAttackState, string configuredDeathState)
        {
            animator = configuredAnimator;
            locomotionState = configuredLocomotionState ?? string.Empty;
            attackState = configuredAttackState ?? string.Empty;
            deathState = configuredDeathState ?? string.Empty;
            CacheHashes();

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        private void CacheHashes()
        {
            locomotionHash = string.IsNullOrWhiteSpace(locomotionState) ? 0 : Animator.StringToHash(locomotionState);
            attackHash = string.IsNullOrWhiteSpace(attackState) ? 0 : Animator.StringToHash(attackState);
            deathHash = string.IsNullOrWhiteSpace(deathState) ? 0 : Animator.StringToHash(deathState);
        }

        private bool HasState(int stateHash)
        {
            return animator != null && animator.runtimeAnimatorController != null && stateHash != 0 && animator.HasState(0, stateHash);
        }

        private void SetPlaybackSpeed(float playbackSpeed)
        {
            if (animator != null)
            {
                animator.speed = Mathf.Clamp(playbackSpeed, 0.1f, 3f);
            }
        }

        private void PlayState(int stateHash, bool restart = false)
        {
            if ((!restart && stateHash == currentHash) || !HasState(stateHash))
            {
                return;
            }

            currentHash = stateHash;
            if (restart)
            {
                animator.CrossFadeInFixedTime(stateHash, transitionDuration, 0, 0f);
            }
            else
            {
                animator.CrossFadeInFixedTime(stateHash, transitionDuration, 0);
            }
        }
    }
}
