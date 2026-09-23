using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    [DisallowMultipleComponent]
    public sealed class CartAttachmentPoints : MonoBehaviour
    {
        [SerializeField] private List<MonsterAttachmentPoint> points = new List<MonsterAttachmentPoint>();

        public IReadOnlyList<MonsterAttachmentPoint> Points => points;
        public int OccupiedCount => points.Where(point => point != null).Sum(point => point.OccupiedCount);
        public int TotalCapacity => points.Where(point => point != null).Sum(point => point.Capacity);
        public bool IsConfigured => points.Any(point => point != null);

        public bool TryReserveClosest(
            MonsterAttachment monster,
            MonsterAttachmentKind kind,
            Vector3 worldPosition,
            out MonsterAttachmentPoint selected)
        {
            selected = null;
            foreach (MonsterAttachmentPoint candidate in points
                         .Where(point => point != null && point.HasCapacity && point.Accepts(kind))
                         .OrderBy(point => (point.transform.position - worldPosition).sqrMagnitude))
            {
                if (candidate.TryReserve(monster))
                {
                    selected = candidate;
                    return true;
                }
            }

            return false;
        }

        public void Configure(IEnumerable<MonsterAttachmentPoint> configuredPoints)
        {
            points = configuredPoints != null
                ? configuredPoints.Where(point => point != null).Distinct().ToList()
                : new List<MonsterAttachmentPoint>();
        }

        private void OnValidate()
        {
            points = points.Where(point => point != null).Distinct().ToList();
        }
    }
}
