using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay.Tests
{
    public sealed class FaceBatThreatModelTests
    {
        [Test]
        public void NearFaceTrackedWaveClearsThreat()
        {
            var model = new FaceBatThreatModel(0.65f, 0.8f, 4f);
            Assert.AreEqual(FaceBatThreatResult.None,
                model.Step(new Vector3(0.1f, 0f, 0.4f), true, Vector3.zero, false, 0.05f));
            Assert.AreEqual(FaceBatThreatResult.HandCleared,
                model.Step(new Vector3(-0.1f, 0f, 0.4f), true, Vector3.zero, false, 0.05f));
        }

        [Test]
        public void DistantHandDoesNotClearThreat()
        {
            var model = new FaceBatThreatModel(0.65f, 0.8f, 4f);
            model.Step(new Vector3(1.2f, 0f, 0.4f), true, Vector3.zero, false, 0.05f);
            Assert.AreEqual(FaceBatThreatResult.None,
                model.Step(new Vector3(0.8f, 0f, 0.4f), true, Vector3.zero, false, 0.05f));
        }

        [Test]
        public void InvalidTrackingCannotClearThreatAndResetsWaveHistory()
        {
            var model = new FaceBatThreatModel(0.65f, 0.8f, 4f);
            model.Step(new Vector3(0.1f, 0f, 0.4f), true, Vector3.zero, false, 0.05f);
            Assert.AreEqual(FaceBatThreatResult.None,
                model.Step(Vector3.zero, false, Vector3.zero, false, 0.05f));
            Assert.AreEqual(FaceBatThreatResult.None,
                model.Step(new Vector3(-0.1f, 0f, 0.4f), true, Vector3.zero, false, 0.05f));
        }

        [Test]
        public void LaneSelectsFixedStandOffPointInFrontOfEyes()
        {
            Vector3 centerLane = FaceBatThreatModel.GetStagingPosition(
                Vector3.zero, Vector3.forward, Vector3.right, 0, 0.22f, 0.9f);
            Vector3 rightLane = FaceBatThreatModel.GetStagingPosition(
                Vector3.zero, Vector3.forward, Vector3.right, 1, 0.22f, 0.9f);

            Assert.AreEqual(new Vector3(0f, 0f, 0.9f), centerLane);
            Assert.AreEqual(new Vector3(0.22f, 0f, 0.9f), rightLane);
            Assert.IsTrue(FaceBatThreatModel.IsWithinStandOff(rightLane + Vector3.forward * 0.1f, rightLane, 0.18f));
            Assert.IsFalse(FaceBatThreatModel.IsWithinStandOff(rightLane + Vector3.forward * 0.2f, rightLane, 0.18f));
        }

        [Test]
        public void TimeoutIsDistinctFromHandClear()
        {
            var model = new FaceBatThreatModel(0.65f, 0.8f, 0.2f);
            Assert.AreEqual(FaceBatThreatResult.None, model.Step(Vector3.zero, false, Vector3.zero, false, 0.1f));
            Assert.AreEqual(FaceBatThreatResult.TimedOut, model.Step(Vector3.zero, false, Vector3.zero, false, 0.1f));
            Assert.IsTrue(model.IsCleared);
        }

        [Test]
        public void EitherHandCanClearUsingItsOwnWaveHistory()
        {
            var model = new FaceBatThreatModel(0.65f, 0.8f, 4f);
            Assert.AreEqual(FaceBatThreatResult.None,
                model.Step(Vector3.zero, false, new Vector3(0.1f, 0f, 0.4f), true, 0.05f));
            Assert.AreEqual(FaceBatThreatResult.HandCleared,
                model.Step(Vector3.zero, false, new Vector3(-0.1f, 0f, 0.4f), true, 0.05f));
        }

        [Test]
        public void InvalidDataCannotCompleteTheSameHandsWave()
        {
            var model = new FaceBatThreatModel(0.65f, 0.8f, 4f);
            model.Step(new Vector3(0.1f, 0f, 0.4f), true, Vector3.zero, false, 0.05f);
            Assert.AreEqual(FaceBatThreatResult.None,
                model.Step(Vector3.zero, false, Vector3.zero, false, 0.05f));
            Assert.AreEqual(FaceBatThreatResult.None,
                model.Step(new Vector3(-0.1f, 0f, 0.4f), true, Vector3.zero, false, 0.05f));
        }

        [Test]
        public void StagingPausesMonsterWithoutDisablingItsLifecycleComponent()
        {
            var monsterObject = new GameObject("Staged bat test");
            try
            {
                Type monsterType = Type.GetType("JapaneseDemonHunter.Monsters.MonsterBase, JapaneseDemonHunter.Monsters");
                Assert.IsNotNull(monsterType, "MonsterBase type should be available in the loaded Monsters assembly.");
                Component monster = monsterObject.AddComponent(monsterType);
                monsterType.GetMethod("StageForFaceThreat", BindingFlags.Instance | BindingFlags.Public).Invoke(monster, null);

                Assert.IsTrue((bool)monsterType.GetProperty("IsStaged").GetValue(monster));
                Assert.IsTrue(((Behaviour)monster).enabled);
                Assert.IsTrue(monsterObject.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(monsterObject);
            }
        }

        [Test]
        public void FrontLaneDoesNotImplicitlyMarkEveryMonsterAsFaceThreat()
        {
            var monsterObject = new GameObject("Front lane monster test");
            try
            {
                Type monsterType = Type.GetType("JapaneseDemonHunter.Monsters.MonsterBase, JapaneseDemonHunter.Monsters");
                Assert.IsNotNull(monsterType);
                Component monster = monsterObject.AddComponent(monsterType);
                monsterType.GetMethod("ConfigureSpawnDirection").Invoke(monster,
                    new object[] { Enum.Parse(Type.GetType("JapaneseDemonHunter.Monsters.MonsterSpawnDirection, JapaneseDemonHunter.Monsters"), "FrontLane"), 1 });

                Assert.IsFalse((bool)monsterType.GetProperty("IsFaceThreatSpawn").GetValue(monster));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(monsterObject);
            }
        }

        [Test]
        public void HandMustWaveInTheNearFrontRegion()
        {
            var model = new FaceBatThreatModel(0.65f, 0.8f, 4f);
            model.Step(new Vector3(0.1f, 0f, -0.3f), true, Vector3.zero, false, 0.05f);
            Assert.AreEqual(FaceBatThreatResult.None,
                model.Step(new Vector3(-0.1f, 0f, -0.3f), true, Vector3.zero, false, 0.05f));
        }
    }
}
