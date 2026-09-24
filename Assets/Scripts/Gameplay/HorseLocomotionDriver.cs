using System.Collections.Generic;
using JapaneseDemonHunter.Prototype;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>
    /// Animates the horse skeleton procedurally: a calm idle when the carriage is standing and a
    /// gallop whose stride frequency follows the real carriage speed, so the horses read as pulling
    /// the cart. Horse.fbx ships a 50 bone rig but no animation clips, so the gait is generated here
    /// instead of played back. Bone names are looked up defensively; a missing bone is skipped.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HorseLocomotionDriver : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour speedSource;
        [SerializeField] private Transform bodyRoot;
        [SerializeField, Min(0f)] private float idleSpeed = 0.15f;
        [SerializeField, Min(0.1f)] private float fullGallopSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float strideLength = 2.4f;
        [SerializeField, Min(0f)] private float idleBob = 0.012f;
        [SerializeField, Min(0f)] private float gallopBob = 0.10f;
        [SerializeField, Min(0f)] private float idleLegSwing = 2f;
        [SerializeField, Min(0f)] private float gallopLegSwing = 34f;
        [SerializeField, Min(0f)] private float gallopLowerLegSwing = 20f;
        [SerializeField, Min(0f)] private float neckSwing = 5f;
        [SerializeField, Min(0f)] private float tailSway = 9f;
        [SerializeField, Min(0f)] private float leftRightPhaseOffset = 0.35f;
        [SerializeField, Min(0.1f)] private float gaitBlendSpeed = 2.5f;
        [SerializeField, Min(0f)] private float kneeLift = 24f;

        private readonly List<Bone> upperLegs = new List<Bone>();
        private readonly List<Bone> lowerLegs = new List<Bone>();
        private Bone neck;
        private Bone tail;
        private Vector3 bodyBasePosition;
        private float phase;
        private float gait;
        private float strideSpeed;

        private sealed class Bone
        {
            public Transform transform;
            public Vector3 basePosition;
            public Quaternion baseRotation;
            public float phaseOffset;
            public bool isForeLeg;
        }

        public float Gait => gait;
        public float Speed => strideSpeed;
        public int AnimatedBoneCount => upperLegs.Count + lowerLegs.Count + (neck != null ? 1 : 0) + (tail != null ? 1 : 0);

        private void Awake()
        {
            if (bodyRoot == null)
            {
                bodyRoot = transform;
            }

            bodyBasePosition = bodyRoot.localPosition;
            CacheBones();
        }

        public void Configure(
            MonoBehaviour configuredSpeedSource,
            Transform configuredBodyRoot,
            float idleSpeedThreshold,
            float gallopSpeed,
            float configuredStrideLength)
        {
            speedSource = configuredSpeedSource;
            bodyRoot = configuredBodyRoot;
            idleSpeed = Mathf.Max(0f, idleSpeedThreshold);
            fullGallopSpeed = Mathf.Max(idleSpeed + 0.1f, gallopSpeed);
            strideLength = Mathf.Max(0.1f, configuredStrideLength);
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            strideSpeed = ResolveSpeed();
            float targetGait = Mathf.Clamp01(Mathf.InverseLerp(idleSpeed, fullGallopSpeed, strideSpeed));
            gait = Mathf.MoveTowards(gait, targetGait, gaitBlendSpeed * deltaTime);

            // The stride advances with distance travelled, so the gait slows down with the carriage.
            float stridesPerSecond = strideSpeed / strideLength;
            phase += stridesPerSecond * 2f * Mathf.PI * deltaTime;
            if (phase > 1e6f)
            {
                phase = 0f;
            }

            float idlePhase = Time.time * 1.1f;
            float bob = idleBob * Mathf.Sin(idlePhase * 0.5f) * (1f - gait) +
                        gallopBob * gait * Mathf.Sin(phase * 2f);
            bodyRoot.localPosition = bodyBasePosition + new Vector3(0f, bob, 0f);

            float legSwing = Mathf.Lerp(idleLegSwing, gallopLegSwing, gait) * gait;
            float lowerSwing = Mathf.Lerp(0f, gallopLowerLegSwing, gait);
            float swingPhase = phase;

            for (var i = 0; i < upperLegs.Count; i++)
            {
                Bone bone = upperLegs[i];
                float offset = bone.phaseOffset + (bone.isForeLeg ? 0f : Mathf.PI);
                float cycle = Mathf.Sin(swingPhase + offset);
                float angle = legSwing * (cycle >= 0f ? cycle : cycle * 0.72f);
                bone.transform.localRotation = bone.baseRotation * Quaternion.Euler(angle, 0f, 0f);
            }

            for (var i = 0; i < lowerLegs.Count; i++)
            {
                Bone bone = lowerLegs[i];
                float offset = bone.phaseOffset + (bone.isForeLeg ? 0f : Mathf.PI);
                float cycle = Mathf.Sin(swingPhase + offset + 0.55f);
                float angle = -lowerSwing * Mathf.Max(0f, cycle) - kneeLift * gait * Mathf.Max(0f, cycle * cycle);
                bone.transform.localRotation = bone.baseRotation * Quaternion.Euler(angle, 0f, 0f);
            }

            if (neck != null)
            {
                float angle = neckSwing * gait * Mathf.Sin(swingPhase + 0.4f) +
                              (1f - gait) * Mathf.Sin(idlePhase * 0.4f);
                neck.transform.localRotation = neck.baseRotation * Quaternion.Euler(angle, 0f, 0f);
            }

            if (tail != null)
            {
                float angle = tailSway * (0.25f + 0.75f * gait) * Mathf.Sin(swingPhase * 0.7f + 1.1f);
                tail.transform.localRotation = tail.baseRotation * Quaternion.Euler(0f, angle, 0f);
            }
        }

        private float ResolveSpeed()
        {
            if (speedSource is ICartSpeedPenaltyReceiver receiver)
            {
                return Mathf.Max(0f, receiver.EffectiveSpeed);
            }

            return 0f;
        }

        private void CacheBones()
        {
            upperLegs.Clear();
            lowerLegs.Clear();

            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null)
                {
                    continue;
                }

                string boneName = candidate.name;
                bool right = boneName.EndsWith(".R");
                float laneOffset = right ? leftRightPhaseOffset : 0f;

                switch (boneName)
                {
                    case "FrontUpperLeg.L":
                    case "FrontUpperLeg.R":
                        AddBone(upperLegs, candidate, laneOffset, true);
                        break;
                    case "BackUpperLeg.L":
                    case "BackUpperLeg.R":
                        AddBone(upperLegs, candidate, laneOffset, false);
                        break;
                    case "FrontLowerLeg.L":
                    case "FrontLowerLeg.R":
                        AddBone(lowerLegs, candidate, laneOffset, true);
                        break;
                    case "BackLowerLeg.L":
                    case "BackLowerLeg.R":
                        AddBone(lowerLegs, candidate, laneOffset, false);
                        break;
                    case "Neck1":
                        neck = CreateBone(candidate, 0f);
                        break;
                    case "Tail1":
                        tail = CreateBone(candidate, 0f);
                        break;
                }
            }

            if (upperLegs.Count == 0)
            {
                Debug.LogWarning(
                    "HorseLocomotionDriver found no horse leg bones: the horses will only bob. " +
                    "Check that the model is rigged with FrontUpperLeg/BackUpperLeg bones.",
                    this);
            }
        }

        private static void AddBone(List<Bone> target, Transform bone, float phaseOffset, bool isForeLeg)
        {
            Bone entry = CreateBone(bone, phaseOffset);
            entry.isForeLeg = isForeLeg;
            target.Add(entry);
        }

        private static Bone CreateBone(Transform bone, float phaseOffset)
        {
            return new Bone
            {
                transform = bone,
                basePosition = bone.localPosition,
                baseRotation = bone.localRotation,
                phaseOffset = phaseOffset
            };
        }
    }
}
