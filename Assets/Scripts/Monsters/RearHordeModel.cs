using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    /// <summary>
    /// Decides how many rear monsters must stay alive behind the cart and which of them are the few
    /// fast enough to catch it. Pure and allocation free, so the horde rule is unit tested instead of
    /// being discovered by playing.
    /// </summary>
    public sealed class RearHordeModel
    {
        private readonly float fastFraction;
        private readonly float fastSpeedMultiplier;

        public RearHordeModel(float configuredFastFraction, float configuredFastSpeedMultiplier)
        {
            fastFraction = Mathf.Clamp01(configuredFastFraction);
            fastSpeedMultiplier = Mathf.Max(0.1f, configuredFastSpeedMultiplier);
        }

        public float FastFraction => fastFraction;
        public float FastSpeedMultiplier => fastSpeedMultiplier;

        /// <summary>Fast runners in a horde of the given size: at least one once the horde is non empty.</summary>
        public int FastRunnerCount(int hordeSize)
        {
            if (hordeSize <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.RoundToInt(hordeSize * fastFraction), 1, hordeSize);
        }

        /// <summary>How many rear monsters are still missing, never spawning past the active limit.</summary>
        public int MissingCount(int activeRearCount, int minimumHordeSize, int activeTotal, int maximumActive)
        {
            if (minimumHordeSize <= 0)
            {
                return 0;
            }

            int available = Mathf.Max(0, maximumActive - activeTotal);
            int missing = minimumHordeSize - activeRearCount;
            return missing <= 0 ? 0 : Mathf.Min(missing, available);
        }

        /// <summary>
        /// The speed multiplier for a horde member, given how many runners are still alive. The
        /// fastest ones are promoted only while there is room, so a horde of slow zombies never
        /// suddenly sprints.
        /// </summary>
        public float SpeedMultiplierFor(int hordeIndex, int fastRunnersAlive)
        {
            return hordeIndex >= 0 && hordeIndex < fastRunnersAlive ? fastSpeedMultiplier : 1f;
        }

        public static float ApplySpeedMultiplier(float baseSpeed, float multiplier)
        {
            return Mathf.Max(0f, baseSpeed) * Mathf.Max(1f, multiplier);
        }
    }
}
