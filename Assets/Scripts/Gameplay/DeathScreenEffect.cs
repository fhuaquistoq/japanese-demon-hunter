using System;
using JapaneseDemonHunter.Monsters;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>
    /// Defeat feedback: when the giant zombie catches the cart, a dark red overlay that is a child
    /// of the player's eye camera fades in. It never moves or rotates the camera. The giant is
    /// spawned at runtime, so this component latches onto it as soon as it exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathScreenEffect : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private GiantZombieSpawner giantSpawner;
        [SerializeField] private GiantZombieController giant;
        [SerializeField] private Renderer overlayRenderer;
        [SerializeField, Min(0f)] private float fadeInDuration = 1.2f;
        [SerializeField, Range(0f, 1f)] private float maximumAlpha = 0.85f;
        [SerializeField] private Color defeatColor = new Color(0.30f, 0.01f, 0.01f, 1f);

        private MaterialPropertyBlock propertyBlock;
        private float alpha;
        private bool defeated;

        public bool IsDefeated => defeated;
        public bool HasGiant => giant != null;
        public float Alpha => alpha;
        public Renderer OverlayRenderer => overlayRenderer;

        public event Action DefeatTriggered;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            ApplyAlpha();
        }

        private void OnEnable()
        {
            SubscribeToGiant();
        }

        private void OnDisable()
        {
            UnsubscribeFromGiant();
        }

        private void Update()
        {
            SubscribeToGiant();

            if (!defeated)
            {
                return;
            }

            float previous = alpha;
            alpha = fadeInDuration <= 0f
                ? maximumAlpha
                : Mathf.Min(maximumAlpha, alpha + (maximumAlpha / fadeInDuration) * Time.deltaTime);
            if (!Mathf.Approximately(previous, alpha))
            {
                ApplyAlpha();
            }
        }

        /// <summary>Starts the defeat fade. Safe to call more than once.</summary>
        public void TriggerDefeat()
        {
            if (defeated)
            {
                return;
            }

            defeated = true;
            if (fadeInDuration <= 0f)
            {
                alpha = maximumAlpha;
                ApplyAlpha();
            }

            DefeatTriggered?.Invoke();
        }

        public void ResetDefeat()
        {
            defeated = false;
            alpha = 0f;
            ApplyAlpha();
        }

        public void Configure(GiantZombieSpawner configuredSpawner, Renderer configuredOverlay, float duration)
        {
            UnsubscribeFromGiant();
            giantSpawner = configuredSpawner;
            overlayRenderer = configuredOverlay;
            fadeInDuration = Mathf.Max(0f, duration);
            defeated = false;
            alpha = 0f;
            ApplyAlpha();
            SubscribeToGiant();
        }

        private void SubscribeToGiant()
        {
            if (giant != null || giantSpawner == null)
            {
                return;
            }

            giant = giantSpawner.SpawnedGiant;
            if (giant != null)
            {
                giant.GiantCaughtCart += HandleGiantCaughtCart;
            }
        }

        private void UnsubscribeFromGiant()
        {
            if (giant != null)
            {
                giant.GiantCaughtCart -= HandleGiantCaughtCart;
            }
        }

        private void HandleGiantCaughtCart(GiantZombieController controller)
        {
            TriggerDefeat();
        }

        private void ApplyAlpha()
        {
            if (overlayRenderer == null)
            {
                return;
            }

            overlayRenderer.enabled = alpha > 0.001f;
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            var color = new Color(defeatColor.r, defeatColor.g, defeatColor.b, alpha);
            var material = overlayRenderer.sharedMaterial;
            overlayRenderer.GetPropertyBlock(propertyBlock);
            if (material != null && material.HasProperty(BaseColorId))
            {
                propertyBlock.SetColor(BaseColorId, color);
            }

            if (material != null && material.HasProperty(ColorId))
            {
                propertyBlock.SetColor(ColorId, color);
            }

            overlayRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
