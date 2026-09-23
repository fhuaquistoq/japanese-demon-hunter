using System;
using UnityEngine;

namespace JapaneseDemonHunter.Prototype
{
    /// <summary>
    /// Owns only the state and presentation of a prototype candle. Enemy systems may request
    /// an extinguish operation, but this class has no knowledge of enemies or combat.
    /// </summary>
    public sealed class PrototypeCandle : MonoBehaviour
    {
        [SerializeField] private bool startsLit = true;
        [SerializeField] private GameObject flameVisual;
        [SerializeField] private Light flameLight;
        [SerializeField] private Transform attackPoint;
        [SerializeField] private bool isLit;

        public event Action<PrototypeCandle> Extinguished;

        public bool IsLit => isLit;
        public GameObject FlameVisual => flameVisual;
        public Light FlameLight => flameLight;
        public Transform AttackPoint => attackPoint;

        private void Awake()
        {
            ApplyState(startsLit);
        }

        /// <summary>
        /// Attempts to extinguish the candle. Returns true only on the first successful request.
        /// </summary>
        public bool RequestExtinguish()
        {
            if (!isLit)
            {
                return false;
            }

            ApplyState(false);
            Extinguished?.Invoke(this);
            return true;
        }

        [ContextMenu("Extinguish (Prototype)")]
        private void ExtinguishFromContextMenu()
        {
            RequestExtinguish();
        }

        private void ApplyState(bool lit)
        {
            isLit = lit;

            if (flameVisual != null)
            {
                flameVisual.SetActive(lit);
            }

            if (flameLight != null)
            {
                flameLight.enabled = lit;
            }
        }

#if UNITY_EDITOR
        public void ConfigurePrototypeReferences(
            GameObject configuredFlameVisual,
            Light configuredFlameLight,
            Transform configuredAttackPoint)
        {
            startsLit = true;
            flameVisual = configuredFlameVisual;
            flameLight = configuredFlameLight;
            attackPoint = configuredAttackPoint;
            ApplyState(true);
        }
#endif
    }
}
