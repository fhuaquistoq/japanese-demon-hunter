using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>Deterministic, allocation-free lamp modulation helpers.</summary>
    public static class CarriageLampModel
    {
        /// <summary>Returns a normalized slow flame wave with a stable per-lamp phase offset.</summary>
        public static float FlameFactor(float time, float phase, float frequency, float amplitude)
        {
            float wave = Mathf.Sin((time * Mathf.Max(0f, frequency) + phase) * (2f * Mathf.PI));
            return Mathf.Clamp01(1f + wave * Mathf.Clamp01(amplitude));
        }

        /// <summary>Scales front lamps gently with the normalized cart speed.</summary>
        public static float GallopFactor(float speed, float cueStartSpeed, float maximumSpeed, float maximumBoost)
        {
            float range = Mathf.Max(0.001f, maximumSpeed - cueStartSpeed);
            float progress = Mathf.Clamp01((speed - cueStartSpeed) / range);
            return 1f + progress * Mathf.Max(0f, maximumBoost);
        }
    }
}
