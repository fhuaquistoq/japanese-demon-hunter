using NUnit.Framework;
using UnityEngine;

namespace Reins.Tests
{
    public sealed class SharedReinGripVisualsTests
    {
        [Test]
        public void SelectionHidesAndReleaseRestoresEachOriginalRendererState()
        {
            var owner = new GameObject("Grip visuals test");
            var visibleObject = new GameObject("Initially visible hand");
            var hiddenObject = new GameObject("Initially hidden hand");
            var proxy = new GameObject("Glove proxy");
            visibleObject.transform.SetParent(owner.transform);
            hiddenObject.transform.SetParent(owner.transform);
            proxy.transform.SetParent(owner.transform);
            var visible = visibleObject.AddComponent<MeshRenderer>();
            var hidden = hiddenObject.AddComponent<MeshRenderer>();
            hidden.enabled = false;

            try
            {
                var visuals = owner.AddComponent<SharedReinGripVisuals>();
                visuals.Configure(null, new Renderer[] { visible, hidden }, proxy);

                visuals.SetGripSelected(true);
                Assert.IsFalse(visible.enabled);
                Assert.IsFalse(hidden.enabled);
                Assert.IsTrue(proxy.activeSelf);

                visuals.SetGripSelected(false);
                Assert.IsTrue(visible.enabled, "A renderer enabled before selection must be restored.");
                Assert.IsFalse(hidden.enabled, "A renderer disabled before selection must stay disabled.");
                Assert.IsFalse(proxy.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DisableCallbackRestoresOriginalHandVisualsAndHidesTheProxy()
        {
            var owner = new GameObject("Grip visuals disable test");
            var handObject = new GameObject("Tracked hand");
            var proxy = new GameObject("Glove proxy");
            handObject.transform.SetParent(owner.transform);
            proxy.transform.SetParent(owner.transform);
            var hand = handObject.AddComponent<MeshRenderer>();

            try
            {
                var visuals = owner.AddComponent<SharedReinGripVisuals>();
                visuals.Configure(null, new Renderer[] { hand }, proxy);
                visuals.SetGripSelected(true);
                // EditMode cannot dispatch the ordinary MonoBehaviour callback through
                // SendMessage; invoke its body directly to verify the restoration path.
                var onDisable = typeof(SharedReinGripVisuals).GetMethod(
                    "OnDisable", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(onDisable);
                onDisable.Invoke(visuals, null);
                owner.SetActive(false);

                Assert.IsTrue(hand.enabled);
                Assert.IsFalse(proxy.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
