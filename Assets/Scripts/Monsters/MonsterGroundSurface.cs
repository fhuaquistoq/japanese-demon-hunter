using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    /// <summary>
    /// Explicitly marks geometry that may support ground monsters. This avoids treating the
    /// moving cart, candles, or other Default-layer colliders as walkable ground.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterGroundSurface : MonoBehaviour
    {
    }
}
