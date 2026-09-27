using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JapaneseDemonHunter.Prototype.Tests
{
    public sealed class PrototypePhaseOneTests
    {
        private const string ScenePath = "Assets/Scenes/MonstersPrototype.unity";

        [Test]
        public void GeneratedSceneOpensWithCompleteReferences()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);

            GameObject cart = GameObject.Find("SimulatedCart");
            Assert.That(cart, Is.Not.Null);

            PrototypeSceneReferences references = cart.GetComponent<PrototypeSceneReferences>();
            Assert.That(references, Is.Not.Null);
            Assert.That(references.IsConfigured, Is.True);
            Assert.That(references.HunterTransform.parent, Is.EqualTo(cart.transform));
            Assert.That(references.HunterAttackPoint.IsChildOf(references.HunterTransform), Is.True);
            Assert.That(references.Candles.Count, Is.EqualTo(4));

            foreach (PrototypeCandle candle in references.Candles)
            {
                Assert.That(candle, Is.Not.Null);
                Assert.That(candle.transform.parent, Is.EqualTo(cart.transform));
                Assert.That(candle.AttackPoint, Is.Not.Null);
                Assert.That(candle.IsLit, Is.True);
                Assert.That(candle.FlameVisual.activeSelf, Is.True);
                Assert.That(candle.FlameLight.enabled, Is.True);
            }

            SmoothFollowCamera followCamera = Object.FindAnyObjectByType<SmoothFollowCamera>();
            Assert.That(followCamera, Is.Not.Null);
            Assert.That(followCamera.Target, Is.EqualTo(references.HunterTransform));
        }

        [Test]
        public void FrontLaneMonsterSpawnUsesOneCenteredLaneAheadOfTheTravellingDirection()
        {
            Type spawnerType = Type.GetType("JapaneseDemonHunter.Monsters.MonsterSpawner, JapaneseDemonHunter.Monsters");
            Type entryType = Type.GetType("JapaneseDemonHunter.Monsters.MonsterSpawnEntry, JapaneseDemonHunter.Monsters");
            Assert.That(spawnerType, Is.Not.Null);
            Assert.That(entryType, Is.Not.Null);

            GameObject cart = new GameObject("FrontLaneSpawnCart");
            GameObject spawnerObject = new GameObject("FrontLaneSpawnSpawner");
            try
            {
                cart.transform.position = new Vector3(2f, 3f, -4f);
                cart.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
                Component spawner = spawnerObject.AddComponent(spawnerType);
                SetPrivateField(spawnerType, spawner, "cartTransform", cart.transform);
                SetPrivateField(spawnerType, spawner, "minimumPlayerDistance", 0f);
                SetPrivateField(spawnerType, spawner, "obstacleMask", (LayerMask)0);

                object entry = Activator.CreateInstance(entryType);
                SetPublicField(entryType, entry, "spawnDirection", Enum.Parse(
                    Type.GetType("JapaneseDemonHunter.Monsters.MonsterSpawnDirection, JapaneseDemonHunter.Monsters"),
                    "FrontLane"));
                SetPublicField(entryType, entry, "movementType", Enum.Parse(
                    Type.GetType("JapaneseDemonHunter.Monsters.MonsterMovementType, JapaneseDemonHunter.Monsters"),
                    "Flying"));
                SetPublicField(entryType, entry, "frontLaneForwardRadius", 10f);
                SetPublicField(entryType, entry, "frontLaneFlyingAltitude", 4.5f);

                MethodInfo findPosition = spawnerType.GetMethod("TryFindSpawnPosition",
                    new[] { entryType, typeof(Vector3).MakeByRefType() });
                Assert.That(findPosition, Is.Not.Null);
                for (int i = 0; i < 20; i++)
                {
                    object[] arguments = { entry, Vector3.zero };
                    Assert.That(findPosition.Invoke(spawner, arguments), Is.True);
                    Vector3 offset = (Vector3)arguments[1] - cart.transform.position;
                    // The carriage rides along -forward, so the front lane must sit against that
                    // direction: spawning on +forward put the bat behind a cart moving away from it.
                    Assert.That(Vector3.Dot(offset, cart.transform.forward), Is.EqualTo(-10f).Within(0.001f));
                    float laneCoordinate = Vector3.Dot(offset, cart.transform.right) / 2.8f;
                    Assert.That(laneCoordinate, Is.EqualTo(Mathf.Round(laneCoordinate)).Within(0.001f));
                    Assert.That(Mathf.RoundToInt(laneCoordinate), Is.InRange(-1, 1));
                    Assert.That(((Vector3)arguments[1]).y, Is.EqualTo(7.5f).Within(0.001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(spawnerObject);
                Object.DestroyImmediate(cart);
            }
        }

        private static void SetPrivateField(Type type, object instance, string name, object value)
        {
            type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        }

        private static void SetPublicField(Type type, object instance, string name, object value)
        {
            type.GetField(name, BindingFlags.Instance | BindingFlags.Public).SetValue(instance, value);
        }

        [Test]
        public void SimulatedCartAdvancesAndStopsDeterministically()
        {
            GameObject cart = new GameObject("MovementTestCart");
            try
            {
                SimulatedCartMovement movement = cart.AddComponent<SimulatedCartMovement>();
                movement.Speed = 3f;
                movement.StartMovement();
                movement.Advance(2f);

                Assert.That(cart.transform.position, Is.EqualTo(new Vector3(0f, 0f, 6f)));

                movement.StopMovement();
                movement.Advance(2f);
                Assert.That(cart.transform.position, Is.EqualTo(new Vector3(0f, 0f, 6f)));
            }
            finally
            {
                Object.DestroyImmediate(cart);
            }
        }

        [Test]
        public void CandleExtinguishesOnceAndDisablesItsPresentation()
        {
            GameObject candleObject = new GameObject("CandleTest");
            GameObject flame = new GameObject("Flame");
            flame.transform.SetParent(candleObject.transform);
            Light flameLight = flame.AddComponent<Light>();
            GameObject attackPoint = new GameObject("AttackPoint");
            attackPoint.transform.SetParent(candleObject.transform);

            try
            {
                PrototypeCandle candle = candleObject.AddComponent<PrototypeCandle>();
                candle.ConfigurePrototypeReferences(flame, flameLight, attackPoint.transform);

                int eventCount = 0;
                candle.Extinguished += _ => eventCount++;

                Assert.That(candle.RequestExtinguish(), Is.True);
                Assert.That(candle.RequestExtinguish(), Is.False);
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(candle.IsLit, Is.False);
                Assert.That(flame.activeSelf, Is.False);
                Assert.That(flameLight.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(candleObject);
            }
        }
    }
}
