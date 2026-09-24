using NUnit.Framework;

namespace JapaneseDemonHunter.Gameplay.Tests
{
    public sealed class GameRunModelTests
    {
        [Test]
        public void ASlowRideCanStillWinAfterSixMinutes()
        {
            var run = new GameRunModel(5);
            run.Tick(360f);
            Assert.AreEqual(360f, run.ElapsedSeconds);
            Assert.IsTrue(run.Win());
            Assert.AreEqual(GameRunResult.Victory, run.Result);
        }

        [Test]
        public void RepeatedMonsterHitsCauseDefeatOnce()
        {
            var run = new GameRunModel(3);
            Assert.IsFalse(run.ReceiveHit());
            Assert.IsFalse(run.ReceiveHit());
            Assert.IsTrue(run.ReceiveHit());
            Assert.AreEqual(0, run.RemainingHits);
            Assert.IsFalse(run.ReceiveHit());
            Assert.AreEqual(GameRunResult.Defeat, run.Result);
        }

        [Test]
        public void ArrivalLocksInVictory()
        {
            var run = new GameRunModel(5);
            run.Tick(240f);
            Assert.IsTrue(run.Win());
            run.Tick(100f);
            Assert.AreEqual(240f, run.ElapsedSeconds);
            Assert.IsFalse(run.Lose());
            Assert.AreEqual(GameRunResult.Victory, run.Result);
        }
    }
}
