using System;

namespace JapaneseDemonHunter.Prototype
{
    /// <summary>Production cart controllers can implement this without referencing monster code.</summary>
    public interface ICartSpeedPenaltyReceiver
    {
        float EffectiveSpeed { get; }
        float SpeedMultiplier { get; }
        void SetMonsterLoadMultiplier(float multiplier);
    }

    /// <summary>Stable integration point for the future horse/whip controller.</summary>
    public interface ICartAccelerationRequester
    {
        void RequestAcceleration();
    }

    /// <summary>Raised once when the player performs the first valid reins gallop.</summary>
    public interface ICartFirstGallopSource
    {
        event Action FirstGallop;
    }
}
