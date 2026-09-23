using UnityEngine;
using UnityEngine.InputSystem;

namespace JapaneseDemonHunter.Prototype
{
    /// <summary>Desktop-only local movement. It never moves a camera or XR Origin.</summary>
    [DisallowMultipleComponent]
    public sealed class SimulatedHunterMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 2.2f;
        [SerializeField] private Vector2 localHalfExtents = new Vector2(1.05f, 1.75f);
        [SerializeField] private bool desktopInputEnabled = true;

        private Vector3 initialLocalPosition;

        private void Awake()
        {
            initialLocalPosition = transform.localPosition;
        }

        private void Update()
        {
            if (!desktopInputEnabled || Keyboard.current == null)
            {
                return;
            }

            Vector2 input = Vector2.zero;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
            MoveLocal(input, Time.deltaTime);
        }

        public void MoveLocal(Vector2 input, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector2 normalized = Vector2.ClampMagnitude(input, 1f);
            Vector3 position = transform.localPosition + new Vector3(normalized.x, 0f, normalized.y) * (speed * deltaTime);
            position.x = Mathf.Clamp(position.x, initialLocalPosition.x - localHalfExtents.x, initialLocalPosition.x + localHalfExtents.x);
            position.z = Mathf.Clamp(position.z, initialLocalPosition.z - localHalfExtents.y, initialLocalPosition.z + localHalfExtents.y);
            transform.localPosition = position;
        }

        public void Configure(float configuredSpeed, Vector2 configuredLocalHalfExtents)
        {
            speed = Mathf.Max(0f, configuredSpeed);
            localHalfExtents = new Vector2(
                Mathf.Max(0f, configuredLocalHalfExtents.x),
                Mathf.Max(0f, configuredLocalHalfExtents.y));
        }
    }
}
