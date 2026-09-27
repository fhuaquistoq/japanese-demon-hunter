using System;
using System.Reflection;
using JapaneseDemonHunter.Monsters;
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

        [Test]
        public void ShortCrouchDodgesButSustainedCrouchMakesTheNextWaveFlyLow()
        {
            var model = new FaceBatCrouchModel(0.3f, 0.4f, 2f);
            model.Step(1.6f, 0.1f);
            model.Step(1.25f, 0.2f);
            Assert.IsFalse(model.CanDodge);
            model.Step(1.25f, 0.2f);
            Assert.IsTrue(model.CanDodge);
            Assert.IsFalse(model.ShouldFlyLow);

            model.Step(1.25f, 1.6f);
            Assert.IsTrue(model.ShouldFlyLow);
            Assert.IsFalse(model.CanDodge);
            model.Step(1.65f, 0.1f);
            Assert.IsFalse(model.ShouldFlyLow);
            Assert.IsFalse(model.IsDucking);
        }

        [Test]
        public void SeatedCalibrationRequiresAnActualDropFromTheHighestTrackedHeadHeight()
        {
            var model = new FaceBatCrouchModel(0.3f, 0.4f, 2f);
            model.Step(1.1f, 5f);
            Assert.IsFalse(model.IsDucking);
            model.Step(1.6f, 0.1f);
            model.Step(1.2f, 0.4f);
            Assert.IsTrue(model.CanDodge);
            Assert.AreEqual(1.6f, model.StandingHeight, 0.001f);
        }

        [Test]
        public void FrontBatSpawnsAheadOfCartTravelRatherThanBehindIt()
        {
            GameObject cart = new GameObject("CartFacingForward");
            GameObject host = new GameObject("FrontSpawner");
            try
            {
                MonsterSpawner spawner = host.AddComponent<MonsterSpawner>();
                var entry = new MonsterSpawnEntry
                {
                    movementType = MonsterMovementType.Flying,
                    spawnDirection = MonsterSpawnDirection.FrontLane,
                    frontLaneForwardRadius = 80f,
                    frontLaneFlyingAltitude = 4f
                };
                spawner.Configure(cart.transform, cart.transform, null, new[] { entry }, 0, 0);

                Assert.IsTrue(spawner.TryFindSpawnPosition(entry, out Vector3 position));
                Assert.AreEqual(-80f, position.z, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(cart);
            }
        }

        [Test]
        public void ReservedRoundExcludesFaceBatsFromThePeriodicSpawnLottery()
        {
            GameObject host = new GameObject("RoundSpawner");
            GameObject rearPrefab = new GameObject("RearPrefab");
            GameObject facePrefab = new GameObject("FacePrefab");
            try
            {
                MonsterSpawner spawner = host.AddComponent<MonsterSpawner>();
                var rear = new MonsterSpawnEntry { prefab = rearPrefab, weight = 1f };
                var face = new MonsterSpawnEntry
                {
                    prefab = facePrefab, weight = 1f,
                    spawnDirection = MonsterSpawnDirection.FrontLane, isFaceThreat = true
                };
                spawner.Configure(null, null, null, new[] { rear, face }, ~0, ~0);
                spawner.ReserveFaceThreatSlots(3);

                MethodInfo select = typeof(MonsterSpawner).GetMethod("SelectWeightedEntry",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(select);
                Assert.AreSame(rear, select.Invoke(spawner, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(rearPrefab);
                UnityEngine.Object.DestroyImmediate(facePrefab);
            }
        }

        [Test]
        public void FaceApproachCanFlyBelowTheNormalPatrolFloor()
        {
            GameObject flyerObject = new GameObject("FaceFlyer");
            try
            {
                flyerObject.transform.position = Vector3.up * 3f;
                FlyingMonsterMovement movement = flyerObject.AddComponent<FlyingMonsterMovement>();
                movement.ConfigureSpeeds(7.2f, 7.2f, 6.8f);
                movement.ConfigureFlight(1.7f, 7.5f, 0);
                movement.Initialize(Vector3.zero);
                for (int frame = 0; frame < 150; frame++)
                {
                    movement.TickMoveTowardsFace(Vector3.up * 0.9f, 6.8f, 0.02f);
                }

                Assert.Less(flyerObject.transform.position.y, 1.4f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(flyerObject);
            }
        }

        [Test]
        public void FaceThreatApproachKeepsAccelerationAcrossMonsterTicks()
        {
            GameObject flyerObject = new GameObject("FaceApproachingBat");
            try
            {
                FlyingMonsterMovement movement = flyerObject.AddComponent<FlyingMonsterMovement>();
                movement.ConfigureSpeeds(7.2f, 7.2f, 6.8f);
                movement.ConfigureFlight(0f, 7.5f, 0);
                movement.Initialize(Vector3.zero);
                MonsterBase monster = flyerObject.AddComponent<MonsterBase>();
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(MonsterBase).GetField("movement", flags).SetValue(monster, movement);
                typeof(MonsterBase).GetField("initialized", flags).SetValue(monster, true);
                monster.BeginFaceThreatApproach();
                monster.Tick(0.1f);
                monster.TickFaceThreatApproach(Vector3.forward * 20f, 0.1f);
                float firstSpeed = movement.Velocity.magnitude;

                monster.Tick(0.1f);
                monster.TickFaceThreatApproach(Vector3.forward * 20f, 0.1f);
                Assert.Greater(movement.Velocity.magnitude, firstSpeed + 0.1f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(flyerObject);
            }
        }

        [Test]
        public void FaceApproachDoesNotOrbitAroundCartObstacles()
        {
            GameObject flyerObject = new GameObject("FaceFlyerWithObstacle");
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                flyerObject.transform.position = Vector3.up * 2f;
                obstacle.transform.position = new Vector3(0f, 2f, 1f);
                obstacle.transform.localScale = new Vector3(2f, 2f, 0.5f);
                Physics.SyncTransforms();
                FlyingMonsterMovement movement = flyerObject.AddComponent<FlyingMonsterMovement>();
                movement.ConfigureFlight(0f, 7f, ~0);
                movement.Initialize(Vector3.zero);
                for (int frame = 0; frame < 100; frame++)
                {
                    movement.TickMoveTowardsFace(new Vector3(0f, 2f, 3f), 6.8f, 0.02f);
                }

                Assert.Greater(flyerObject.transform.position.z, 2.5f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obstacle);
                UnityEngine.Object.DestroyImmediate(flyerObject);
            }
        }
    }
}
