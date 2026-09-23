using System.Collections.Generic;
using UnityEngine;

namespace JapaneseDemonHunter.Prototype
{
    /// <summary>
    /// Stable integration surface for the next prototype phase. Future monster logic can receive
    /// this component or its individual references without depending on geometry or a VR camera.
    /// </summary>
    public sealed class PrototypeSceneReferences : MonoBehaviour
    {
        [Header("Cart and hunter")]
        [SerializeField] private Transform cartTransform;
        [SerializeField] private SimulatedCartMovement cartMovement;
        [SerializeField] private Transform hunterTransform;
        [SerializeField] private Transform hunterAttackPoint;

        [Header("Candles")]
        [SerializeField] private PrototypeCandle frontLeftCandle;
        [SerializeField] private PrototypeCandle frontRightCandle;
        [SerializeField] private PrototypeCandle backLeftCandle;
        [SerializeField] private PrototypeCandle backRightCandle;

        public Transform CartTransform => cartTransform;
        public SimulatedCartMovement CartMovement => cartMovement;
        public Transform HunterTransform => hunterTransform;
        public Transform HunterAttackPoint => hunterAttackPoint;
        public PrototypeCandle FrontLeftCandle => frontLeftCandle;
        public PrototypeCandle FrontRightCandle => frontRightCandle;
        public PrototypeCandle BackLeftCandle => backLeftCandle;
        public PrototypeCandle BackRightCandle => backRightCandle;

        public IReadOnlyList<PrototypeCandle> Candles => new[]
        {
            frontLeftCandle,
            frontRightCandle,
            backLeftCandle,
            backRightCandle
        };

        public bool IsConfigured =>
            cartTransform != null &&
            cartMovement != null &&
            hunterTransform != null &&
            hunterAttackPoint != null &&
            frontLeftCandle != null &&
            frontRightCandle != null &&
            backLeftCandle != null &&
            backRightCandle != null;

#if UNITY_EDITOR
        public void ConfigurePrototypeReferences(
            Transform configuredCartTransform,
            SimulatedCartMovement configuredCartMovement,
            Transform configuredHunterTransform,
            Transform configuredHunterAttackPoint,
            PrototypeCandle configuredFrontLeftCandle,
            PrototypeCandle configuredFrontRightCandle,
            PrototypeCandle configuredBackLeftCandle,
            PrototypeCandle configuredBackRightCandle)
        {
            cartTransform = configuredCartTransform;
            cartMovement = configuredCartMovement;
            hunterTransform = configuredHunterTransform;
            hunterAttackPoint = configuredHunterAttackPoint;
            frontLeftCandle = configuredFrontLeftCandle;
            frontRightCandle = configuredFrontRightCandle;
            backLeftCandle = configuredBackLeftCandle;
            backRightCandle = configuredBackRightCandle;
        }
#endif
    }
}
