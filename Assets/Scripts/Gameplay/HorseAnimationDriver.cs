using JapaneseDemonHunter.Prototype;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>
    /// Drives the horse state machine from the real carriage speed: the imported idle clip plays
    /// while the cart is nearly stopped and the gallop clip takes over as it accelerates. The
    /// state names and the imported clips are whatever the fbx actually contains, so nothing here
    /// assumes a clip is present. If the rig turns out to be unusable, the procedural
    /// <see cref="HorseLocomotionDriver"/> still on the same object takes over.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HorseAnimationDriver : MonoBehaviour
    {
        private const string SpeedParameter = "Speed";
        private const string GallopParameter = "Gallop";

        [SerializeField] private MonoBehaviour speedSource;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float idleSpeed = 0.15f;
        [SerializeField, Min(0.1f)] private float fullGallopSpeed = 3.2f;
        [SerializeField, Min(0.01f)] private float gaitBlendSpeed = 2.5f;
        [Tooltip("Turns off to hand the horse back to the procedural gait driver.")]
        [SerializeField] private bool useImportedClips = true;

        private HorseLocomotionDriver fallback;
        private float gallop;
        private float speed;

        public float Gallop => gallop;
        public float Speed => speed;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            fallback = GetComponent<HorseLocomotionDriver>();
        }

        public void Configure(MonoBehaviour configuredSpeedSource, Animator configuredAnimator)
        {
            speedSource = configuredSpeedSource;
            animator = configuredAnimator;
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            speed = ResolveSpeed();
            gallop = Mathf.MoveTowards(
                gallop,
                Mathf.Clamp01(Mathf.InverseLerp(idleSpeed, fullGallopSpeed, speed)),
                gaitBlendSpeed * deltaTime);

            if (!useImportedClips || animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            animator.SetFloat(SpeedParameter, speed);
            animator.SetFloat(GallopParameter, gallop);

            // The clips keep their authored rate and just loop. Scaling the playback by the carriage
            // speed made the gallop race ahead of itself once the horses were made faster, so the
            // animator's own speed is deliberately left alone: the cart goes faster, the gait does not.
        }

        private float ResolveSpeed()
        {
            if (speedSource is ICartSpeedPenaltyReceiver receiver)
            {
                return Mathf.Max(0f, receiver.EffectiveSpeed);
            }

            return 0f;
        }
    }
}
