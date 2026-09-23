using UnityEngine;

namespace Reins
{
    /// <summary>Allocation-free Verlet simulation for a closed, four-pin rein loop.</summary>
    public static class RopePhysics
    {
        public const int PinCount = 4;

        public static void InitializeLoop(
            Vector3[] positions, Vector3[] previous, Vector3[] anchors, float slack)
        {
            ValidateBuffers(positions, previous, anchors);
            var edgeCount = positions.Length / PinCount;
            for (var edge = 0; edge < PinCount; edge++)
            {
                var start = anchors[edge];
                var end = anchors[(edge + 1) % PinCount];
                for (var point = 0; point < edgeCount; point++)
                {
                    var index = edge * edgeCount + point;
                    var t = point / (float)edgeCount;
                    var sag = Mathf.Sin(t * Mathf.PI) * slack;
                    positions[index] = Vector3.Lerp(start, end, t) + Vector3.down * sag;
                    previous[index] = positions[index];
                }
            }

            PinAnchors(positions, previous, anchors);
        }

        public static void SimulateLoop(
            Vector3[] positions,
            Vector3[] previous,
            Vector3[] anchors,
            float[] spanSegmentLengths,
            Vector3 gravity,
            float deltaTime,
            float minimumY,
            int constraintIterations)
        {
            ValidateBuffers(positions, previous, anchors);
            ValidateSpanLengths(spanSegmentLengths);
            var edgeCount = positions.Length / PinCount;
            deltaTime = Mathf.Max(0f, deltaTime);
            var gravityStep = gravity * (deltaTime * deltaTime);
            for (var i = 0; i < positions.Length; i++)
            {
                if (IsPin(i, positions.Length))
                {
                    positions[i] = anchors[PinIndex(i, positions.Length)];
                    previous[i] = positions[i];
                    continue;
                }

                var current = positions[i];
                positions[i] += positions[i] - previous[i] + gravityStep;
                previous[i] = current;
                if (positions[i].y < minimumY)
                {
                    positions[i].y = minimumY;
                    previous[i].y = minimumY;
                }
            }

            for (var iteration = 0; iteration < Mathf.Max(1, constraintIterations); iteration++)
            {
                for (var i = 0; i < positions.Length; i++)
                {
                    var next = (i + 1) % positions.Length;
                    var delta = positions[next] - positions[i];
                    var distance = delta.magnitude;
                    if (distance <= Mathf.Epsilon)
                    {
                        continue;
                    }

                    var segmentLength = spanSegmentLengths[i / edgeCount];
                    var correction = delta * ((distance - segmentLength) / distance);
                    var firstPinned = IsPin(i, positions.Length);
                    var secondPinned = IsPin(next, positions.Length);
                    if (!firstPinned && !secondPinned)
                    {
                        positions[i] += correction * 0.5f;
                        positions[next] -= correction * 0.5f;
                    }
                    else if (firstPinned)
                    {
                        positions[next] -= correction;
                    }
                    else
                    {
                        positions[i] += correction;
                    }
                }

                PinAnchors(positions, previous, anchors);
                for (var i = 0; i < positions.Length; i++)
                {
                    if (!IsPin(i, positions.Length) && positions[i].y < minimumY)
                    {
                        positions[i].y = minimumY;
                    }
                }
            }

            PinAnchors(positions, previous, anchors);
        }

        public static void CalculateSpanSegmentLengths(
            Vector3[] anchors, int pointCount, float slack, float[] spanSegmentLengths)
        {
            if (anchors == null || anchors.Length != PinCount || pointCount < PinCount * 2 ||
                pointCount % PinCount != 0 || spanSegmentLengths == null ||
                spanSegmentLengths.Length != PinCount)
            {
                throw new System.ArgumentException("A closed rein loop needs four anchors, four span lengths, and a point count divisible by four.");
            }

            var edgeCount = pointCount / PinCount;
            var spanSlack = Mathf.Max(1f, slack);
            for (var span = 0; span < PinCount; span++)
            {
                spanSegmentLengths[span] = Vector3.Distance(
                    anchors[span], anchors[(span + 1) % PinCount]) * spanSlack / edgeCount;
            }
        }

        private static void PinAnchors(Vector3[] positions, Vector3[] previous, Vector3[] anchors)
        {
            for (var i = 0; i < PinCount; i++)
            {
                var index = i * (positions.Length / PinCount);
                positions[index] = anchors[i];
                previous[index] = anchors[i];
            }
        }

        private static bool IsPin(int index, int pointCount) => index % (pointCount / PinCount) == 0;
        private static int PinIndex(int index, int pointCount) => index / (pointCount / PinCount);

        private static void ValidateSpanLengths(float[] spanSegmentLengths)
        {
            if (spanSegmentLengths == null || spanSegmentLengths.Length != PinCount)
            {
                throw new System.ArgumentException("A closed rein loop needs exactly four span segment lengths.");
            }
        }

        private static void ValidateBuffers(Vector3[] positions, Vector3[] previous, Vector3[] anchors)
        {
            if (positions == null || previous == null || anchors == null ||
                positions.Length != previous.Length || positions.Length < PinCount * 2 ||
                positions.Length % PinCount != 0 || anchors.Length != PinCount)
            {
                throw new System.ArgumentException("Loop buffers must have matching divisible point counts and exactly four anchors.");
            }
        }
    }
}
