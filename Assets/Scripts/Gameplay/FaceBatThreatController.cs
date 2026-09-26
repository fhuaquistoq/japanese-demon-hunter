using System.Collections.Generic;
using JapaneseDemonHunter.Monsters;
using JapaneseDemonHunter.Prototype;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using Reins;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>Stages one front-lane bat in a small near-field silhouette and lets tracked hand waves clear it.</summary>
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

        private readonly List<IHand> hands = new List<IHand>(2);
        private MonsterBase activeThreat;
        private Renderer[] threatRenderers;
        private FaceBatThreatModel model;
        private Vector3 stagingPosition;
        private bool isStaged;

        public MonsterBase ActiveThreat => activeThreat;
        public Transform EyeAnchor => eyeAnchor;

        private void OnEnable()
        {
            if (spawner != null) spawner.MonsterSpawned += HandleMonsterSpawned;
        }

        private void Start()
        {
            DiscoverHands();
        }

        private void OnDisable()
        {
            if (spawner != null) spawner.MonsterSpawned -= HandleMonsterSpawned;
            ClearThreat();
        }

        private void LateUpdate()
        {
            if (activeThreat == null) return;
            if (eyeAnchor == null || !activeThreat.gameObject.activeInHierarchy)
            {
                ClearThreat();
                return;
            }

            if (!isStaged)
            {
                activeThreat.TickFaceThreatApproach(stagingPosition, Time.deltaTime);
                if (!FaceBatThreatModel.IsWithinStandOff(activeThreat.transform.position, stagingPosition, arrivalDistance))
                {
                    return;
                }

                StageThreat();
            }

            Vector3 leftPalm = default;
            Vector3 rightPalm = default;
            bool leftValid = false;
            bool rightValid = false;
            for (int index = 0; index < hands.Count; index++)
            {
                IHand hand = hands[index];
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

            // Time advances exactly once per frame; either tracked hand may clear, while missing data cannot.
            FaceBatThreatResult result = model.Step(leftPalm, leftValid, rightPalm, rightValid, Time.deltaTime);
            if (result == FaceBatThreatResult.TimedOut) ApplyTimeoutPenaltyAndRetire();
            else if (result == FaceBatThreatResult.HandCleared) ClearThreat();
        }

        public void Configure(MonsterSpawner configuredSpawner, Transform configuredEyeAnchor)
        {
            if (spawner != null) spawner.MonsterSpawned -= HandleMonsterSpawned;
            spawner = configuredSpawner;
            eyeAnchor = configuredEyeAnchor;
            if (isActiveAndEnabled && spawner != null) spawner.MonsterSpawned += HandleMonsterSpawned;
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

        private void HandleMonsterSpawned(MonsterBase monster)
        {
            if (activeThreat != null || monster == null || !monster.IsFaceThreatSpawn ||
                monster.SpawnDirection != MonsterSpawnDirection.FrontLane || eyeAnchor == null)
            {
                return;
            }

            activeThreat = monster;
            stagingPosition = FaceBatThreatModel.GetStagingPosition(
                eyeAnchor.position, eyeAnchor.forward, eyeAnchor.right, monster.FrontLaneIndex,
                laneOffset, standOffDistance);
            threatRenderers = monster.GetComponentsInChildren<Renderer>(true);
            monster.BeginFaceThreatApproach();

            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in threatRenderers)
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

        private void StageThreat()
        {
            if (activeThreat == null || isStaged) return;
            isStaged = true;
            model = new FaceBatThreatModel(clearProximity, minimumWaveSpeed, maximumDuration);
            activeThreat.StageForFaceThreat();
            activeThreat.transform.rotation = Quaternion.LookRotation(-eyeAnchor.forward, eyeAnchor.up);
            foreach (Collider collider in activeThreat.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        private void ApplyTimeoutPenaltyAndRetire()
        {
            Transform cart = activeThreat != null ? activeThreat.CartTransform : null;
            while (cart != null)
            {
                foreach (MonoBehaviour component in cart.GetComponents<MonoBehaviour>())
                {
                    if (component is CarriageMotor motor)
                    {
                        motor.ApplyHitPenalty();
                        ClearThreat();
                        return;
                    }
                }
                cart = cart.parent;
            }

            // No temporary hit-penalty integration is available; retire safely without changing load state.
            ClearThreat();
        }

        private void ClearThreat()
        {
            MonsterBase threat = activeThreat;
            activeThreat = null;
            model = null;
            threatRenderers = null;
            isStaged = false;
            if (threat != null) threat.Retire();
        }
    }
}
