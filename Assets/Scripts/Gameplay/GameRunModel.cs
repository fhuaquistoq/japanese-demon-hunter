using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    public enum GameRunResult { Playing, Victory, Defeat }

    /// <summary>Deterministic victory and defeat rules; ride duration is recorded without a limit.</summary>
    public sealed class GameRunModel
    {
        public float ElapsedSeconds { get; private set; }
        public int RemainingHits { get; private set; }
        public GameRunResult Result { get; private set; }

        public GameRunModel(int maximumHits)
        {
            RemainingHits = Mathf.Max(1, maximumHits);
            Result = GameRunResult.Playing;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (Result == GameRunResult.Playing)
                ElapsedSeconds += Mathf.Max(0f, unscaledDeltaTime);
        }

        public bool ReceiveHit()
        {
            if (Result != GameRunResult.Playing) return false;
            RemainingHits = Mathf.Max(0, RemainingHits - 1);
            if (RemainingHits > 0) return false;
            Result = GameRunResult.Defeat;
            return true;
        }

        public bool Win()
        {
            if (Result != GameRunResult.Playing) return false;
            Result = GameRunResult.Victory;
            return true;
        }

        public bool Lose()
        {
            if (Result != GameRunResult.Playing) return false;
            Result = GameRunResult.Defeat;
            return true;
        }
    }
}
