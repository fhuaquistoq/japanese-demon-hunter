using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>
    /// Keeps a large ground collider centred under the carriage so ground monsters always find a
    /// surface marked with <c>MonsterGroundSurface</c> while the road scrolls. It only moves the
    /// ground, never the player rig.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CartFollowGround : MonoBehaviour
    {
        [SerializeField] private Transform cartTransform;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, -0.01f, 0f);
        [SerializeField] private bool followYaw;

        public Transform CartTransform => cartTransform;

        public void Configure(Transform configuredCart, Vector3 configuredOffset, bool configuredFollowYaw = false)
        {
            cartTransform = configuredCart;
            localOffset = configuredOffset;
            followYaw = configuredFollowYaw;
            SnapNow();
        }

        private void LateUpdate()
        {
            SnapNow();
        }

        public void SnapNow()
        {
            if (cartTransform == null)
            {
                return;
            }

            var rotation = followYaw ? Quaternion.Euler(0f, cartTransform.eulerAngles.y, 0f) : Quaternion.identity;
            transform.position = cartTransform.position + rotation * localOffset;
            transform.rotation = rotation;
        }
    }
}
