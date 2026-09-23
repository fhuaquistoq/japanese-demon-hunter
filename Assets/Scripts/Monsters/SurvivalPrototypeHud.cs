using JapaneseDemonHunter.Prototype;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class SurvivalPrototypeHud : MonoBehaviour
    {
        [SerializeField] private CartMonsterLoad cartLoad;
        [SerializeField] private SimulatedCartSurvivalController cartSpeed;
        [SerializeField] private GiantZombieSpawner giantSpawner;
        [SerializeField] private PrototypeHunterMonsterTarget hunterTarget;

        private float threatFlashUntil;

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(16f, 16f, 360f, 140f), "SURVIVAL PROTOTYPE (desktop)");
            float load = cartLoad != null ? cartLoad.TotalLoad : 0f;
            float multiplier = cartLoad != null ? cartLoad.SpeedMultiplier : 1f;
            float speed = cartSpeed != null ? cartSpeed.EffectiveSpeed : 0f;
            string giantDistance = giantSpawner != null && giantSpawner.SpawnedGiant != null
                ? $"{giantSpawner.SpawnedGiant.DistanceToRear:0.0} m"
                : "waiting";
            GUI.Label(new Rect(28f, 43f, 340f, 22f), $"Speed: {speed:0.00} | Load: {load:0.0} | x{multiplier:0.00}");
            GUI.Label(new Rect(28f, 65f, 340f, 22f), $"Giant distance: {giantDistance}");
            GUI.Label(new Rect(28f, 87f, 340f, 22f), "WASD/arrows move | Click/Space sword | LShift whip");
            GUI.Label(new Rect(28f, 109f, 340f, 22f), "Camera: hold right mouse to orbit | wheel to zoom");
            if (Time.time < threatFlashUntil)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(28f, 131f, 340f, 22f), "MONSTER THREAT HIT (simulated)");
                GUI.color = Color.white;
            }
        }

        public void Configure(
            CartMonsterLoad configuredLoad,
            SimulatedCartSurvivalController configuredSpeed,
            GiantZombieSpawner configuredGiant,
            PrototypeHunterMonsterTarget configuredHunter)
        {
            Unsubscribe();
            cartLoad = configuredLoad;
            cartSpeed = configuredSpeed;
            giantSpawner = configuredGiant;
            hunterTarget = configuredHunter;
            Subscribe();
        }

        private void Subscribe()
        {
            if (hunterTarget != null)
            {
                hunterTarget.SimulatedHit -= HandleHunterHit;
                hunterTarget.SimulatedHit += HandleHunterHit;
            }
        }

        private void Unsubscribe()
        {
            if (hunterTarget != null)
            {
                hunterTarget.SimulatedHit -= HandleHunterHit;
            }
        }

        private void HandleHunterHit(MonsterBase attacker)
        {
            threatFlashUntil = Time.time + 0.8f;
        }
    }
}
