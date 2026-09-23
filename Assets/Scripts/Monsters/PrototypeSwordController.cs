using UnityEngine;
using UnityEngine.InputSystem;

namespace JapaneseDemonHunter.Monsters
{
    /// <summary>Desktop-only driver. A future XR interactor can call the same SwordDamage API.</summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeSwordController : MonoBehaviour
    {
        [SerializeField] private SwordDamage swordDamage;
        [SerializeField, Min(0.05f)] private float swingDuration = 0.42f;
        [SerializeField] private Vector3 restingEuler = new Vector3(0f, 0f, -55f);
        [SerializeField] private Vector3 impactEuler = new Vector3(0f, 0f, 75f);
        [SerializeField] private bool desktopInputEnabled = true;

        private bool swinging;
        private float swingElapsed;

        private void Awake()
        {
            if (swordDamage == null)
            {
                swordDamage = GetComponentInChildren<SwordDamage>();
            }
            transform.localRotation = Quaternion.Euler(restingEuler);
        }

        private void Update()
        {
            if (desktopInputEnabled && !swinging &&
                ((Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                 (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)))
            {
                StartSwing();
            }

            TickSwing(Time.deltaTime);
        }

        [ContextMenu("Start Sword Swing (Prototype)")]
        public void StartSwing()
        {
            if (swinging || swordDamage == null)
            {
                return;
            }

            swinging = true;
            swingElapsed = 0f;
            swordDamage.BeginAttackWindow();
        }

        public void TickSwing(float deltaTime)
        {
            if (!swinging || deltaTime <= 0f)
            {
                return;
            }

            swingElapsed += deltaTime;
            float t = Mathf.Clamp01(swingElapsed / swingDuration);
            float eased = t * t * (3f - 2f * t);
            transform.localRotation = Quaternion.Slerp(
                Quaternion.Euler(restingEuler),
                Quaternion.Euler(impactEuler),
                eased);
            if (t >= 1f)
            {
                swinging = false;
                swordDamage.EndAttackWindow();
                transform.localRotation = Quaternion.Euler(restingEuler);
            }
        }

        public void Configure(SwordDamage configuredSwordDamage)
        {
            swordDamage = configuredSwordDamage;
        }
    }
}
