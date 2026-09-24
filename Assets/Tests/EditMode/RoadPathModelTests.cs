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

        [Test]
        public void HeightProfileRaisesAndLowersTheRoadGentlyAndNeverSinks()
        {
            var flat = new RoadPathModel(32, 18f, 0f);
            for (int chunk = 0; chunk < flat.ChunkCount; chunk++)
            {
                Assert.AreEqual(0f, flat.GetChunkPosition(chunk).y, Tolerance, "a flat road must stay at y=0");
            }

            var rolling = new RoadPathModel(48, 18f, 0f, RoadPathModel.DefaultTurnRadius, 1.2f, 55f);
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            for (int chunk = 0; chunk < rolling.ChunkCount; chunk++)
            {
                float y = rolling.GetChunkPosition(chunk).y;
                Assert.GreaterOrEqual(y, 0f, "the road must not sink below the ground plane");
                lowest = Mathf.Min(lowest, y);
                highest = Mathf.Max(highest, y);
            }

            Assert.Greater(highest - lowest, 0.5f, "the road should actually rise and fall");
            Assert.Less(highest - lowest, 2.5f, "the slopes must stay gentle for a headset");
        }

        [Test]
        public void ChunkRotationPitchesWithTheSlopeAndPointsAlongTravel()
        {
            var model = new RoadPathModel(48, 18f, 0f, RoadPathModel.DefaultTurnRadius, 1.5f, 40f);
            int sloped = 0;
            for (int chunk = 1; chunk < model.ChunkCount - 1; chunk++)
            {
                Vector3 previous = model.GetChunkPosition(chunk - 1);
                Vector3 next = model.GetChunkPosition(chunk + 1);
                float rise = next.y - previous.y;
                Vector3 forward = model.GetChunkRotation(chunk) * Vector3.back;
                Assert.AreEqual(0f, forward.x, 0.02f, "a straight road must not point sideways");
                if (Mathf.Abs(rise) > 0.4f)
                {
                    sloped++;
                    Assert.AreEqual(Mathf.Sign(rise), Mathf.Sign(forward.y), 0.05f,
                        "the tile must tilt the way the road rises or falls");
                }
            }

            Assert.Greater(sloped, 0, "the test needs at least one slope");
        }

        [Test]
        public void ForksAppearDeterministically()
        {
            Assert.IsFalse(RoadPathModel.IsForkChunk(RoadPathModel.ForkFirstChunk - 1));
            Assert.IsTrue(RoadPathModel.IsForkChunk(RoadPathModel.ForkFirstChunk));
            Assert.IsTrue(RoadPathModel.IsForkChunk(
                RoadPathModel.ForkFirstChunk + RoadPathModel.ForkLengthChunks - 1));
            Assert.IsFalse(RoadPathModel.IsForkChunk(
                RoadPathModel.ForkFirstChunk + RoadPathModel.ForkLengthChunks));

            int period = RoadPathModel.ForkPeriodChunks + RoadPathModel.ForkLengthChunks;
            Assert.IsTrue(RoadPathModel.IsForkChunk(RoadPathModel.ForkFirstChunk + period));
        }

        [Test]
        public void BranchOffsetsSeparateAndComeBackTogether()
        {
            var model = new RoadPathModel(64, 18f, 0.5f, 100f, 0f, 55f, 4.6f);
            int start = RoadPathModel.ForkFirstChunk;

            Assert.AreEqual(0f, model.BranchOffset(start, 1), 0.01f, "a fork starts on the centreline");
            Assert.AreEqual(0f, model.BranchOffset(start + RoadPathModel.ForkLengthChunks, 1), 0.01f,
                "branches must rejoin");

            float left = model.BranchOffset(start + 2, -1);
            float right = model.BranchOffset(start + 2, 1);
            Assert.Less(left, -1f);
            Assert.Greater(right, 1f);
            Assert.AreEqual(-left, right, 0.001f, "both branches must mirror each other");
        }

        [Test]
        public void BranchOffsetsAreZeroWithoutForks()
        {
            var model = new RoadPathModel(64, 18f, 1f);
            for (int chunk = 0; chunk < model.ChunkCount; chunk++)
            {
                Assert.AreEqual(0f, model.BranchOffset(chunk, 1), Tolerance);
                Assert.AreEqual(0f, model.BranchOffsetAtDistance(chunk * 18f, -1), Tolerance);
            }
        }

        [Test]
        public void BranchOffsetAtDistanceIsContinuousAcrossChunkBoundaries()
        {
            var model = new RoadPathModel(64, 18f, 0.5f, 100f, 0f, 55f, 4.6f);
            float previous = model.BranchOffsetAtDistance(0f, 1);
            for (float distance = 1f; distance < 40f * 18f; distance += 1f)
            {
                float current = model.BranchOffsetAtDistance(distance, 1);
                Assert.Less(Mathf.Abs(current - previous), 1.2f, "the branch must not jump between chunks");
                previous = current;
            }
        }
    }
}
