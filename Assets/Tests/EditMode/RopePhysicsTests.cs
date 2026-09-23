using NUnit.Framework;
using UnityEngine;

namespace Reins.Tests
{
    public sealed class RopePhysicsTests
    {
        private const int PointCount = 32;
        private const int EdgesPerSpan = PointCount / RopePhysics.PinCount;

        [Test]
        public void ClosedLoopKeepsAllFourHorseAndGripPinsAttached()
        {
            var anchors = CreateAsymmetricAnchors();
            var positions = new Vector3[PointCount];
            var previous = new Vector3[PointCount];
            var spanSegmentLengths = new float[RopePhysics.PinCount];
            RopePhysics.InitializeLoop(positions, previous, anchors, 1.18f);
            RopePhysics.CalculateSpanSegmentLengths(anchors, PointCount, 1.18f, spanSegmentLengths);

            SimulateFrames(positions, previous, anchors, spanSegmentLengths, 180);

            for (var pin = 0; pin < RopePhysics.PinCount; pin++)
            {
                var index = pin * EdgesPerSpan;
                Assert.AreEqual(anchors[pin], positions[index]);
            }
        }

        [Test]
        public void AsymmetricSpansUseTheirOwnRestLengthsIncludingPinnedClosure()
        {
            var anchors = CreateAsymmetricAnchors();
            var positions = new Vector3[PointCount];
            var previous = new Vector3[PointCount];
            var spanSegmentLengths = new float[RopePhysics.PinCount];
            const float slack = 1.12f;
            RopePhysics.InitializeLoop(positions, previous, anchors, slack);
            RopePhysics.CalculateSpanSegmentLengths(anchors, PointCount, slack, spanSegmentLengths);

            for (var span = 0; span < RopePhysics.PinCount; span++)
            {
                var expected = Vector3.Distance(anchors[span], anchors[(span + 1) % RopePhysics.PinCount]) *
                    slack / EdgesPerSpan;
                Assert.That(spanSegmentLengths[span], Is.EqualTo(expected).Within(0.0001f));
            }
            Assert.That(spanSegmentLengths[3], Is.GreaterThan(spanSegmentLengths[1] * 3f),
                "The long horse-to-horse closure span must not inherit the short hand-to-hand rest length.");

            SimulateFrames(positions, previous, anchors, spanSegmentLengths, 180);

            for (var span = 0; span < RopePhysics.PinCount; span++)
            {
                var lastInSpan = (span + 1) * EdgesPerSpan - 1;
                var nextPin = ((span + 1) * EdgesPerSpan) % PointCount;
                var finalSegmentLength = Vector3.Distance(positions[lastInSpan], positions[nextPin]);
                Assert.That(finalSegmentLength, Is.EqualTo(spanSegmentLengths[span]).Within(0.12f),
                    $"Span {span}, including its pinned endpoint, should use its own rest length.");
            }
            Assert.AreEqual(anchors[0], positions[0], "The last-to-first edge must close onto the first pin.");
        }

        [Test]
        public void GravityProducesFiniteSagBetweenPinsAndRespectsFloor()
        {
            var anchors = CreateAsymmetricAnchors();
            var positions = new Vector3[PointCount];
            var previous = new Vector3[PointCount];
            var spanSegmentLengths = new float[RopePhysics.PinCount];
            RopePhysics.InitializeLoop(positions, previous, anchors, 1.2f);
            RopePhysics.CalculateSpanSegmentLengths(anchors, PointCount, 1.2f, spanSegmentLengths);

            SimulateFrames(positions, previous, anchors, spanSegmentLengths, 120);

            Assert.That(positions[4].y, Is.LessThan(Mathf.Max(anchors[0].y, anchors[1].y)));
            foreach (var position in positions)
            {
                Assert.IsFalse(float.IsNaN(position.x) || float.IsInfinity(position.x));
                Assert.IsFalse(float.IsNaN(position.y) || float.IsInfinity(position.y));
                Assert.IsFalse(float.IsNaN(position.z) || float.IsInfinity(position.z));
                Assert.That(position.y, Is.GreaterThanOrEqualTo(-0.4001f));
            }
        }

        private static void SimulateFrames(
            Vector3[] positions, Vector3[] previous, Vector3[] anchors,
            float[] spanSegmentLengths, int frameCount)
        {
            for (var frame = 0; frame < frameCount; frame++)
            {
                RopePhysics.SimulateLoop(positions, previous, anchors, spanSegmentLengths,
                    Vector3.down * 2.2f, 1f / 60f, -0.4f, 12);
            }
        }

        private static Vector3[] CreateAsymmetricAnchors()
        {
            return new[]
            {
                new Vector3(-1.5f, 1.2f, 1f),
                new Vector3(-0.2f, 0.8f, 0.5f),
                new Vector3(0.2f, 0.8f, 0.5f),
                new Vector3(1.5f, 1.2f, 1f)
            };
        }
    }
}
