using UnityEngine;

namespace Reins
{
    /// <summary>
    /// Swaps the SDK hand mesh for a fixed-position glove proxy only while its expected tracked hand
    /// validly holds the rein. It never changes tracking objects, interactors, or hand poses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SharedReinGripVisuals : MonoBehaviour
    {
        [SerializeField] private ReinHandle reinHandle;
        [SerializeField] private Renderer[] originalHandVisuals = new Renderer[0];
        [SerializeField] private GameObject gloveProxy;

        private bool[] originalEnabledStates;
        private bool isProxyShown;

        private void Awake()
        {
            EnsureStateBuffer();
            if (gloveProxy != null)
            {
                gloveProxy.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            SetGripSelected(reinHandle != null && reinHandle.IsHeldByExpectedHand);
        }

        private void OnDisable()
        {
            SetGripSelected(false);
        }

        private void OnDestroy()
        {
            SetGripSelected(false);
        }

        /// <summary>Wires stable scene references once; callers must not perform per-frame lookup.</summary>
        public void Configure(ReinHandle source, Renderer[] trackedVisuals, GameObject fixedGripProxy)
        {
            SetGripSelected(false);
            reinHandle = source;
            originalHandVisuals = trackedVisuals ?? new Renderer[0];
            gloveProxy = fixedGripProxy;
            originalEnabledStates = new bool[originalHandVisuals.Length];
            if (gloveProxy != null)
            {
                gloveProxy.SetActive(false);
            }
        }

        /// <summary>Applies only the visual swap; exposed for deterministic EditMode coverage.</summary>
        public void SetGripSelected(bool selected)
        {
            EnsureStateBuffer();
            if (selected == isProxyShown)
            {
                return;
            }

            if (selected)
            {
                for (var i = 0; i < originalHandVisuals.Length; i++)
                {
                    Renderer visual = originalHandVisuals[i];
                    if (visual == null)
                    {
                        continue;
                    }

                    originalEnabledStates[i] = visual.enabled;
                    visual.enabled = false;
                }

                if (gloveProxy != null)
                {
                    gloveProxy.SetActive(true);
                }

                isProxyShown = true;
                return;
            }

            RestoreOriginals();
            if (gloveProxy != null)
            {
                gloveProxy.SetActive(false);
            }

            isProxyShown = false;
        }

        private void EnsureStateBuffer()
        {
            if (originalEnabledStates == null || originalEnabledStates.Length != originalHandVisuals.Length)
            {
                originalEnabledStates = new bool[originalHandVisuals.Length];
            }
        }

        private void RestoreOriginals()
        {
            for (var i = 0; i < originalHandVisuals.Length; i++)
            {
                if (originalHandVisuals[i] != null)
                {
                    originalHandVisuals[i].enabled = originalEnabledStates[i];
                }
            }
        }
    }
}
