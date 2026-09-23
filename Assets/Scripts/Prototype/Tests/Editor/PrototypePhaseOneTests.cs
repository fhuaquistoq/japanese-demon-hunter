using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
