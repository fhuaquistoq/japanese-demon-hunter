using UnityEngine;
using UnityEngine.InputSystem;

namespace JapaneseDemonHunter.Prototype
{
    /// <summary>
    /// Desktop-only hunter-view camera. Its target is a simulated hunter parented to the cart,
    /// so it follows the vehicle without moving the cart, a camera rig, or an XR Origin.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SmoothFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 headOffset = new Vector3(0f, 0.38f, 0f);

        [Header("Desktop mouse look")]
        [SerializeField, Min(0.001f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private bool invertY;
        [SerializeField] private float minimumPitch = -80f;
        [SerializeField] private float maximumPitch = 80f;
        [SerializeField] private bool lockCursorOnPlay = true;
        [SerializeField] private bool mouseControlEnabled = true;

        [Header("Follow")]
        [SerializeField, Min(0.01f)] private float positionSharpness = 30f;

        private float yaw;
        private float pitch;
        private bool cursorCaptured;

        public Transform Target => target;
        public float Yaw => yaw;
        public float Pitch => pitch;
        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Max(0.001f, value);
        }
        public bool InvertY
        {
            get => invertY;
            set => invertY = value;
        }

        private void Awake()
        {
            Vector3 euler = transform.rotation.eulerAngles;
            yaw = euler.y;
            pitch = Mathf.Clamp(NormalizeAngle(euler.x), minimumPitch, maximumPitch);
        }

        private void Start()
        {
            SnapToTarget();
            if (lockCursorOnPlay)
            {
                CaptureCursor();
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReleaseCursor();
            }

            if (!cursorCaptured && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                CaptureCursor();
            }

            if (!mouseControlEnabled || !cursorCaptured || Mouse.current == null)
            {
                return;
            }

            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * mouseSensitivity;
            float verticalSign = invertY ? 1f : -1f;
            pitch = Mathf.Clamp(
                pitch + delta.y * mouseSensitivity * verticalSign,
                minimumPitch,
                maximumPitch);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            float blend = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, HeadPosition, blend);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void OnDisable()
        {
            ReleaseCursor();
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            transform.position = HeadPosition;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        public void CaptureCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            cursorCaptured = true;
        }

        public void ReleaseCursor()
        {
            if (!cursorCaptured && Cursor.lockState == CursorLockMode.None)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            cursorCaptured = false;
        }

        public void ConfigureDesktopHunterView(
            Transform configuredHunter,
            Vector3 configuredHeadOffset,
            float configuredSensitivity,
            bool configuredInvertY)
        {
            target = configuredHunter;
            headOffset = configuredHeadOffset;
            mouseSensitivity = Mathf.Max(0.001f, configuredSensitivity);
            invertY = configuredInvertY;
            minimumPitch = -80f;
            maximumPitch = 80f;
            positionSharpness = Mathf.Max(positionSharpness, 30f);
            if (target != null)
            {
                yaw = target.eulerAngles.y;
                pitch = 0f;
                SnapToTarget();
            }
        }

        private Vector3 HeadPosition => target.TransformPoint(headOffset);

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

#if UNITY_EDITOR
        public void ConfigurePrototypeTarget(Transform configuredTarget)
        {
            ConfigureDesktopHunterView(configuredTarget, new Vector3(0f, 0.38f, 0f), mouseSensitivity, invertY);
        }
#endif
    }
}
