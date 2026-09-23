using UnityEngine;

namespace JapaneseDemonHunter.Prototype
{
    /// <summary>
    /// Temporary straight-line cart movement. This component can be removed when the
    /// production cart system is integrated; no monster code should depend on it.
    /// </summary>
    public sealed class SimulatedCartMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 2f;
        [SerializeField] private bool startMoving = true;

        private bool isMoving;

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0f, value);
        }

        public bool IsMoving => isMoving;
        public float EffectiveSpeed => isMoving ? speed : 0f;

        private void Awake()
        {
            isMoving = startMoving;
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        public void StartMovement()
        {
            isMoving = true;
        }

        public void StopMovement()
        {
            isMoving = false;
        }

        public void SetMovementEnabled(bool shouldMove)
        {
            isMoving = shouldMove;
        }

        /// <summary>
        /// Advances the simulation by a known time interval. Public to support deterministic
        /// prototype validation without requiring a headset or physics simulation.
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (!isMoving || deltaTime <= 0f || speed <= 0f)
            {
                return;
            }

            transform.Translate(Vector3.forward * (speed * deltaTime), Space.Self);
        }
    }
}
