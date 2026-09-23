using System;
using System.Collections.Generic;
using JapaneseDemonHunter.Monsters;
using Reins;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>
    /// Victory: when the cart reaches the end of the road (the fortified kingdom), it stops, the
    /// monsters are cleared and the kingdom lights come up. The player is still never teleported.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelVictoryController : MonoBehaviour
    {
        [SerializeField] private ForestRoad road;
        [SerializeField] private CarriageMotor motor;
        [SerializeField] private MonsterSpawner spawner;
        [SerializeField] private GiantZombieSpawner giantSpawner;
        [SerializeField] private GameObject victoryPresentation;
        [SerializeField] private Light[] kingdomLights;
        [SerializeField, Min(1f)] private float kingdomLightMultiplier = 2.5f;

        private float[] baseIntensities;

        public bool IsComplete { get; private set; }
        public bool IsConfigured => road != null && road.LevelDistance > 0f;

        public event Action LevelCompleted;

        private void Start()
        {
            if (kingdomLights != null)
            {
                baseIntensities = new float[kingdomLights.Length];
                for (var i = 0; i < kingdomLights.Length; i++)
                {
                    baseIntensities[i] = kingdomLights[i] != null ? kingdomLights[i].intensity : 0f;
                }
            }

            if (victoryPresentation != null)
            {
                victoryPresentation.SetActive(false);
            }
        }

        private void Update()
        {
            if (IsComplete || road == null || road.LevelDistance <= 0f)
            {
                return;
            }

            if (road.HasReachedEnd)
            {
                Complete();
            }
        }

        /// <summary>Completes the level. Idempotent.</summary>
        public void Complete()
        {
            if (IsComplete)
            {
                return;
            }

            IsComplete = true;

            if (motor != null)
            {
                motor.enabled = false;
            }

            if (spawner != null)
            {
                foreach (MonsterBase monster in new List<MonsterBase>(spawner.ActiveMonsters))
                {
                    if (monster != null)
                    {
                        monster.Retire();
                    }
                }

                spawner.enabled = false;
            }

            if (giantSpawner != null && giantSpawner.SpawnedGiant != null)
            {
                Destroy(giantSpawner.SpawnedGiant.gameObject);
            }

            if (victoryPresentation != null)
            {
                victoryPresentation.SetActive(true);
            }

            if (kingdomLights != null && baseIntensities != null)
            {
                for (var i = 0; i < kingdomLights.Length && i < baseIntensities.Length; i++)
                {
                    if (kingdomLights[i] != null)
                    {
                        kingdomLights[i].intensity = baseIntensities[i] * kingdomLightMultiplier;
                    }
                }
            }

            Debug.Log("Japanese Demon Hunter: the cart reached the fortified kingdom. Victory.", this);
            LevelCompleted?.Invoke();
        }

        public void Configure(
            ForestRoad configuredRoad,
            CarriageMotor configuredMotor,
            MonsterSpawner configuredSpawner,
            GiantZombieSpawner configuredGiantSpawner)
        {
            road = configuredRoad;
            motor = configuredMotor;
            spawner = configuredSpawner;
            giantSpawner = configuredGiantSpawner;
        }

        /// <summary>Assigns the presentation shown on arrival and the lights that come up with it.</summary>
        public void ConfigurePresentation(GameObject presentation, Light[] lights)
        {
            victoryPresentation = presentation;
            kingdomLights = lights;
            if (victoryPresentation != null)
            {
                victoryPresentation.SetActive(false);
            }
        }
    }
}
