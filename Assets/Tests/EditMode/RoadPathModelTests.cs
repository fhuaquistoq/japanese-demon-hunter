using NUnit.Framework;
using UnityEngine;

namespace Reins.Tests
{
    public sealed class RoadPathModelTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void ZeroCurvatureKeepsTheRoadStraightTowardNegativeZ()
        {
            var model = new RoadPathModel(8, 18f, 0f);

            for (int chunk = 0; chunk < model.ChunkCount; chunk++)
            {
                Vector3 position = model.GetChunkPosition(chunk);
                Assert.AreEqual(0f, position.x, Tolerance, $"chunk {chunk} drifted sideways.");
                Assert.AreEqual(-chunk * 18f, position.z, Tolerance, $"chunk {chunk} advanced incorrectly.");
                Assert.AreEqual(0f, model.GetChunkHeading(chunk), Tolerance, $"chunk {chunk} turned.");
            }
        }

        [Test]
        public void CurvatureScheduleIsDeterministicAndAlternates()
        {
            var first = new RoadPathModel(32, 18f, 1f);
            var second = new RoadPathModel(32, 18f, 1f);

            for (int chunk = 0; chunk < first.ChunkCount; chunk++)
            {
                Assert.AreEqual(first.GetChunkPosition(chunk), second.GetChunkPosition(chunk));
                Assert.AreEqual(first.GetChunkHeading(chunk), second.GetChunkHeading(chunk), Tolerance);
            }
        }

        [Test]
        public void CurvedRoadStaysFiniteAndActuallyTurns()
        {
            var model = new RoadPathModel(64, 18f, 1f);

            for (int chunk = 0; chunk < model.ChunkCount; chunk++)
            {
                Vector3 position = model.GetChunkPosition(chunk);
                Assert.IsFalse(float.IsNaN(position.x) || float.IsNaN(position.z));
                Assert.IsFalse(float.IsInfinity(position.x) || float.IsInfinity(position.z));
            }

            Assert.Greater(Mathf.Abs(model.GetChunkHeading(model.ChunkCount - 1)), 1f);
        }

        [Test]
        public void ArcLengthStaysCloseToTheStraightDistance()
        {
            var model = new RoadPathModel(32, 18f, 1f);
            float traveled = 0f;
            Vector3 previous = model.GetChunkPosition(0);
            for (int chunk = 1; chunk < model.ChunkCount; chunk++)
            {
                Vector3 current = model.GetChunkPosition(chunk);
                traveled += Vector3.Distance(previous, current);
                previous = current;
            }

            float straightDistance = (model.ChunkCount - 1) * 18f;
            Assert.That(traveled, Is.EqualTo(straightDistance).Within(straightDistance * 0.05f));
        }

        [Test]
        public void CurvatureForChunkReturnsStraightsThenBothTurns()
        {
            Assert.AreEqual(0f, RoadPathModel.CurvatureForChunk(0, 1f, 100f));
            Assert.AreEqual(0f, RoadPathModel.CurvatureForChunk(4, 1f, 100f));
            Assert.Greater(RoadPathModel.CurvatureForChunk(5, 1f, 100f), 0f);
            Assert.Less(RoadPathModel.CurvatureForChunk(12, 1f, 100f), 0f);
            Assert.AreEqual(
                RoadPathModel.CurvatureForChunk(5, 1f, 100f),
                RoadPathModel.CurvatureForChunk(5 + 14, 1f, 100f));
            Assert.AreEqual(0f, RoadPathModel.CurvatureForChunk(5, 0f, 100f));
        }

        [Test]
        public void OutOfRangeChunkLookupsAreClamped()
        {
            var model = new RoadPathModel(4, 18f, 1f);
            Assert.AreEqual(model.GetChunkPosition(0), model.GetChunkPosition(-5));
            Assert.AreEqual(model.GetChunkPosition(3), model.GetChunkPosition(99));
        }
    }
}
