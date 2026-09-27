using JapaneseDemonHunter.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay.Tests
{
    public sealed class GiantZombieTrailTests
    {
        [Test]
        public void GiantStaysVisibleBehindCartUntilFinalChaseCanCatchIt()
        {
            GameObject cart = new GameObject("Cart");
            GameObject giantObject = new GameObject("Giant");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject rearPoint = new GameObject("RearPoint");
            try
            {
                rearPoint.transform.SetParent(cart.transform, false);
                rearPoint.transform.localPosition = new Vector3(0f, 0f, -3f);
                visual.transform.SetParent(giantObject.transform, false);
                GiantZombieController giant = giantObject.AddComponent<GiantZombieController>();
                Renderer renderer = visual.GetComponent<Renderer>();
                int catchEvents = 0;
                giant.GiantCaughtCart += _ => catchEvents++;

                giant.Initialize(cart.transform, rearPoint.transform);
                Assert.IsFalse(giant.IsVisible);
                Assert.IsFalse(renderer.enabled);

                giant.BeginTrailing(22f);
                giant.TickChase(0.02f);
                Assert.IsTrue(giant.IsVisible);
                Assert.IsTrue(renderer.enabled);
                Assert.IsTrue(giant.IsTrailingBehindCart);
                Assert.AreEqual(-22f, giantObject.transform.position.z, 0.001f);
                Assert.AreEqual(0, catchEvents);

                cart.transform.position = new Vector3(2f, 0f, 10f);
                giant.TickChase(0.02f);
                Assert.AreEqual(2f, giantObject.transform.position.x, 0.001f);
                Assert.AreEqual(-12f, giantObject.transform.position.z, 0.001f);
                Assert.AreEqual(0, catchEvents);

                giant.BeginFinalChase();
                Assert.IsFalse(giant.IsTrailingBehindCart);
                giantObject.transform.position = rearPoint.transform.position;
                giant.TickChase(0.02f);
                giant.TickChase(0.02f);
                Assert.AreEqual(1, catchEvents);
            }
            finally
            {
                Object.DestroyImmediate(rearPoint);
                Object.DestroyImmediate(visual);
                Object.DestroyImmediate(giantObject);
                Object.DestroyImmediate(cart);
            }
        }
    }
}
