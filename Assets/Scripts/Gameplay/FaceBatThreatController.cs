using System.Collections.Generic;
using JapaneseDemonHunter.Monsters;
using JapaneseDemonHunter.Prototype;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using Reins;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class FaceBatThreatController : MonoBehaviour
    {
        [SerializeField] private MonsterSpawner spawner;
        [SerializeField] private Transform eyeAnchor;
        [SerializeField, Min(0.2f)] private float standOffDistance = 0.9f;
        [SerializeField, Range(0f, 0.5f)] private float laneOffset = 0.22f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.18f;
        [SerializeField, Range(0.08f, 0.35f)] private float maximumWorldSize = 0.24f;
        [SerializeField, Min(0.05f)] private float clearProximity = 0.65f;
        [SerializeField, Min(0.05f)] private float minimumWaveSpeed = 0.8f;
        [SerializeField, Min(0.5f)] private float maximumDuration = 4f;
        [SerializeField, Range(1, 3)] private int batsPerRound = 3;
        [SerializeField, Min(0f)] private float betweenRounds = 5f;
        [SerializeField, Min(0.05f)] private float crouchDrop = 0.3f;
        [SerializeField, Min(0.05f)] private float crouchDodgeHold = 0.4f;
        [SerializeField, Min(0.1f)] private float lowFlightDelay = 2f;

        private readonly List<IHand> hands = new List<IHand>(2);
        private readonly List<MonsterBase> threats = new List<MonsterBase>(3);
        private FaceBatThreatModel model;
        private FaceBatCrouchModel crouch;
        private ICartInputBlocker inputBlocker;
        private bool roundClockStarted;
        private bool waveTargetsLow;
        private float nextRoundTime;

        public MonsterBase ActiveThreat => threats.Count > 0 ? threats[0] : null;
        public int ActiveThreatCount => threats.Count;
        public bool IsRoundStaged => model != null;
        public bool IsFlyingLow => waveTargetsLow;
        public Transform EyeAnchor => eyeAnchor;

        private void OnEnable()
        {
            if (spawner == null) return;
            spawner.MonsterSpawned += HandleMonsterSpawned;
            spawner.ReserveFaceThreatSlots(batsPerRound);
        }

        private void Start()
        {
            DiscoverHands();
            crouch = new FaceBatCrouchModel(crouchDrop, crouchDodgeHold, lowFlightDelay);
            FindInputBlocker();
        }

        private void OnDisable()
        {
            if (spawner != null)
            {
                spawner.MonsterSpawned -= HandleMonsterSpawned;
                spawner.ReserveFaceThreatSlots(0);
            }
            ClearThreats(false);
        }

        private void LateUpdate()
        {
            if (spawner == null || eyeAnchor == null)
            {
                if (threats.Count > 0) ClearThreats(false);
                return;
            }

            float relativeEyeHeight = eyeAnchor.position.y -
                (spawner.CartTransform != null ? spawner.CartTransform.position.y : 0f);
            crouch.Step(relativeEyeHeight, Time.deltaTime);
            if (threats.Count == 0)
            {
                TryStartRound();
                return;
            }

            for (int index = 0; index < threats.Count; index++)
            {
                if (threats[index] == null || !threats[index].gameObject.activeInHierarchy)
                {
                    ClearThreats(true);
                    return;
                }
            }

            if (model == null && crouch.ShouldFlyLow) waveTargetsLow = true;
            Vector3 eyePosition = eyeAnchor.position;
            if (!waveTargetsLow && spawner.CartTransform != null)
            {
                eyePosition.y = spawner.CartTransform.position.y + crouch.StandingHeight;
            }

            foreach (MonsterBase threat in threats)
            {
                if (threat.IsStaged) continue;
                Vector3 target = FaceBatThreatModel.GetStagingPosition(eyePosition,
                    eyeAnchor.forward, eyeAnchor.right, threat.FrontLaneIndex, laneOffset, standOffDistance);
                threat.TickFaceThreatApproach(target, Time.deltaTime);
                if (FaceBatThreatModel.IsWithinStandOff(threat.transform.position, target, arrivalDistance))
                {
                    StageThreat(threat);
                }
            }

            if (model == null) return;
            if (!waveTargetsLow && crouch.CanDodge)
            {
                ClearThreats(true);
                return;
            }

            Vector3 leftPalm = default;
            Vector3 rightPalm = default;
            bool leftValid = false;
            bool rightValid = false;
            foreach (IHand hand in hands)
            {
                Pose palm = default;
                Pose middleTip = default;
                bool valid = hand != null && hand.IsConnected && hand.IsTrackedDataValid &&
                             hand.GetJointPose(HandJointId.HandPalm, out palm) &&
                             hand.GetJointPose(HandJointId.HandMiddleTip, out middleTip);
                if (!valid) continue;

                Vector3 position = eyeAnchor.InverseTransformPoint((palm.position + middleTip.position) * 0.5f);
                if (hand.Handedness == Handedness.Left)
                {
                    leftPalm = position;
                    leftValid = true;
                }
                else
                {
                    rightPalm = position;
                    rightValid = true;
                }
            }

            FaceBatThreatResult result = model.Step(leftPalm, leftValid, rightPalm, rightValid, Time.deltaTime);
            if (result == FaceBatThreatResult.TimedOut) ApplyTimeoutPenaltyAndRetire();
            else if (result == FaceBatThreatResult.HandCleared) ClearThreats(true);
        }

        public void Configure(MonsterSpawner configuredSpawner, Transform configuredEyeAnchor)
        {
            if (spawner != null)
            {
                spawner.MonsterSpawned -= HandleMonsterSpawned;
                spawner.ReserveFaceThreatSlots(0);
            }

            spawner = configuredSpawner;
            eyeAnchor = configuredEyeAnchor;
            inputBlocker = null;
            if (isActiveAndEnabled && spawner != null)
            {
                spawner.MonsterSpawned += HandleMonsterSpawned;
                spawner.ReserveFaceThreatSlots(batsPerRound);
            }
        }

        private void TryStartRound()
        {
            if (spawner.IsWaitingForFirstGallop)
            {
                roundClockStarted = false;
                return;
            }

            if (!roundClockStarted)
            {
                nextRoundTime = Time.time + spawner.InitialSpawnDelay;
                roundClockStarted = true;
            }

            if (Time.time < nextRoundTime) return;
            if (spawner.TrySpawnFaceThreatRound(batsPerRound))
            {
                waveTargetsLow = crouch.ShouldFlyLow;
            }
            else
            {
                nextRoundTime = Time.time + betweenRounds;
            }
        }

        private void DiscoverHands()
        {
            hands.Clear();
            foreach (HandGrabInteractor interactor in FindObjectsByType<HandGrabInteractor>(FindObjectsSortMode.None))
            {
                IHand hand = interactor != null ? interactor.Hand : null;
                if (hand == null) continue;
                bool duplicate = false;
                foreach (IHand known in hands)
                {
                    if (known != null && known.Handedness == hand.Handedness) duplicate = true;
                }
                if (!duplicate) hands.Add(hand);
            }
        }

        private void FindInputBlocker()
        {
            if (spawner == null || spawner.CartTransform == null) return;
            foreach (MonoBehaviour behaviour in spawner.CartTransform.GetComponents<MonoBehaviour>())
            {
                if (behaviour is ICartInputBlocker blocker)
                {
                    inputBlocker = blocker;
                    return;
                }
            }
        }

        private void HandleMonsterSpawned(MonsterBase monster)
        {
            if (monster == null || !monster.IsFaceThreatSpawn ||
                monster.SpawnDirection != MonsterSpawnDirection.FrontLane || eyeAnchor == null)
            {
                return;
            }

            if (threats.Count >= batsPerRound)
            {
                monster.Retire();
                return;
            }

            threats.Add(monster);
            monster.BeginFaceThreatApproach();
            Renderer[] renderers = monster.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (hasBounds && bounds.size.magnitude > 0.0001f)
            {
                float scale = maximumWorldSize / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                monster.transform.localScale *= scale;
            }
        }

        private void StageThreat(MonsterBase monster)
        {
            monster.StageForFaceThreat();
            monster.transform.rotation = Quaternion.LookRotation(-eyeAnchor.forward, eyeAnchor.up);
            foreach (Collider collider in monster.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            if (spawner.CartTransform != null) monster.transform.SetParent(spawner.CartTransform, true);
            if (model != null) return;

            model = new FaceBatThreatModel(clearProximity, minimumWaveSpeed, maximumDuration);
            if (inputBlocker == null) FindInputBlocker();
            inputBlocker?.SetInputBlocked(true);
        }

        private void ApplyTimeoutPenaltyAndRetire()
        {
            Transform cart = ActiveThreat != null ? ActiveThreat.CartTransform : null;
            while (cart != null)
            {
                foreach (MonoBehaviour component in cart.GetComponents<MonoBehaviour>())
                {
                    if (component is CarriageMotor motor)
                    {
                        motor.ApplyHitPenalty();
                        ClearThreats(true);
                        return;
                    }
                }
                cart = cart.parent;
            }

            ClearThreats(true);
        }

        private void ClearThreats(bool scheduleNext)
        {
            if (inputBlocker is MonoBehaviour receiver && receiver != null)
            {
                inputBlocker.SetInputBlocked(false);
            }

            foreach (MonsterBase threat in threats)
            {
                if (threat != null) threat.Retire();
            }
            threats.Clear();
            model = null;
            waveTargetsLow = false;
            if (scheduleNext) nextRoundTime = Time.time + betweenRounds;
        }
    }
}
