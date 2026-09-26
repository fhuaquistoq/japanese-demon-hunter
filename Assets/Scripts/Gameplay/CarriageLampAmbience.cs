using JapaneseDemonHunter.Prototype;
using Reins;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>Tunes the four existing carriage lamps; it never creates lights or materials.</summary>
    [DisallowMultipleComponent]
    public sealed class CarriageLampAmbience : MonoBehaviour
    {
        [Header("Existing lights (front left/right, rear left/right)")]
        [SerializeField] private Light[] frontLights = new Light[2];
        [SerializeField] private Light[] rearLights = new Light[2];
        [SerializeField] private PrototypeCandle[] frontCandles = new PrototypeCandle[2];
        [SerializeField] private CarriageMotor carriageMotor;
        [SerializeField] private LevelVictoryController victoryController;
        [SerializeField] private GameSessionController sessionController;

        [Header("Front lane and gallop cues")]
        [SerializeField] private Color frontColor = new Color(1f, 0.78f, 0.44f);
        [SerializeField, Min(0f)] private float frontIntensity = 6f;
        [SerializeField, Min(0f)] private float frontRange = 24f;
        [SerializeField, Min(0f)] private float gallopCueStartSpeed = 3.4f;
        [SerializeField, Min(0f)] private float gallopCueMaximumSpeed = 8f;
        [SerializeField, Range(0f, 0.5f)] private float gallopIntensityBoost = 0.18f;

        [Header("Rear threat visibility")]
        [SerializeField] private Color rearColor = new Color(1f, 0.78f, 0.44f);
        [SerializeField, Min(0f)] private float rearIntensity = 5f;
        [SerializeField, Min(0f)] private float rearRange = 22f;

        [Header("Slow flame")]
        [SerializeField, Range(0f, 0.4f)] private float modulationAmplitude = 0.14f;
        [SerializeField, Min(0f)] private float modulationFrequency = 0.16f;

        private void Start()
        {
            // Both are created later in the scene build than the lamps, so they are resolved here
            // rather than left as the nulls the generator happened to see.
            if (victoryController == null) victoryController = FindAnyObjectByType<LevelVictoryController>();
            if (sessionController == null) sessionController = FindAnyObjectByType<GameSessionController>();
        }

        private void Update()
        {
            if (IsRunFinished()) return;

            float time = Time.time;
            float speed = carriageMotor != null ? carriageMotor.Speed : 0f;
            float frontCue = CarriageLampModel.GallopFactor(
                speed, gallopCueStartSpeed, gallopCueMaximumSpeed, gallopIntensityBoost);
            ApplyLights(frontLights, frontCandles, frontColor, frontIntensity * frontCue, frontRange, time, 0f);
            ApplyLights(rearLights, null, rearColor, rearIntensity, rearRange, time, 0.5f);
        }

        /// <summary>Wires only references; ambience values remain editable in the Inspector.</summary>
        public void Configure(
            Light[] configuredFrontLights,
            Light[] configuredRearLights,
            PrototypeCandle[] configuredFrontCandles,
            CarriageMotor configuredMotor,
            LevelVictoryController configuredVictory,
            GameSessionController configuredSession)
        {
            frontLights = configuredFrontLights;
            rearLights = configuredRearLights;
            frontCandles = configuredFrontCandles;
            carriageMotor = configuredMotor;
            victoryController = configuredVictory;
            sessionController = configuredSession;
        }

        private bool IsRunFinished()
        {
            return (victoryController != null && victoryController.IsComplete) ||
                   (sessionController != null && sessionController.Result != GameRunResult.Playing);
        }

        private void ApplyLights(
            Light[] lights,
            PrototypeCandle[] candles,
            Color color,
            float baseIntensity,
            float range,
            float time,
            float phaseOffset)
        {
            if (lights == null) return;
            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light == null || !light.enabled) continue;
                if (candles != null && index < candles.Length && candles[index] != null && !candles[index].IsLit)
                    continue;

                float phase = phaseOffset + index * 0.25f;
                float factor = CarriageLampModel.FlameFactor(
                    time, phase, modulationFrequency, modulationAmplitude);
                light.color = color;
                light.range = range;
                light.intensity = baseIntensity * factor;
            }
        }
    }
}
