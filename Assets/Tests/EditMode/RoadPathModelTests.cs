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
        public void ExactlyOneIntersectionAppearsAfterTheInitialRoad()
        {
            int start = RoadPathModel.DefaultIntersectionStartChunk;
            int length = RoadPathModel.DefaultIntersectionBranchLengthChunks;

            Assert.IsFalse(RoadPathModel.IsForkChunk(start - 1));
            for (int chunk = start; chunk < start + length; chunk++)
            {
                Assert.IsTrue(RoadPathModel.IsForkChunk(chunk), $"chunk {chunk} should form the intersection");
            }

            Assert.IsFalse(RoadPathModel.IsForkChunk(start + length));
            Assert.IsFalse(RoadPathModel.IsForkChunk(start + length + 20),
                "the level must not create repeated intersections");
        }

        [Test]
        public void InitialRoadIsSharedThenRoutesSeparateAndDoNotRejoin()
        {
            var model = new RoadPathModel(64, 18f, 0f, 100f, 0f, 55f, 6f);
            float before = model.IntersectionStartDistance - 1f;
            model.GetRoutePoseAtDistance(before, RoadRouteDirection.Left, out Vector3 beforeLeft, out _);
            model.GetRoutePoseAtDistance(before, RoadRouteDirection.Straight, out Vector3 beforeCentre, out _);
            model.GetRoutePoseAtDistance(before, RoadRouteDirection.Right, out Vector3 beforeRight, out _);
            Assert.AreEqual(beforeCentre, beforeLeft);
            Assert.AreEqual(beforeCentre, beforeRight);

            float afterTurn = model.IntersectionStartDistance + model.IntersectionBranchLength + 18f;
            model.GetRoutePoseAtDistance(afterTurn, RoadRouteDirection.Left, out Vector3 left, out float leftHeading);
            model.GetRoutePoseAtDistance(afterTurn, RoadRouteDirection.Straight, out Vector3 centre, out float centreHeading);
            model.GetRoutePoseAtDistance(afterTurn, RoadRouteDirection.Right, out Vector3 right, out float rightHeading);

            Assert.Less(left.x, centre.x - 10f);
            Assert.Greater(right.x, centre.x + 10f);
            Assert.Greater(Mathf.DeltaAngle(centreHeading, leftHeading), 40f);
            Assert.Less(Mathf.DeltaAngle(centreHeading, rightHeading), -40f);
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
        public void SideRouteTurnsAreContinuousAndNeverAbrupt()
        {
            var model = new RoadPathModel(64, 18f, 0f, 100f, 0f, 55f, 6f);
            float previousHeading = 0f;
            model.GetRoutePoseAtDistance(model.IntersectionStartDistance, RoadRouteDirection.Left,
                out Vector3 previousPosition, out previousHeading);
            float end = model.IntersectionStartDistance + model.IntersectionBranchLength;
            for (float distance = model.IntersectionStartDistance + 1f; distance <= end; distance += 1f)
            {
                model.GetRoutePoseAtDistance(distance, RoadRouteDirection.Left,
                    out Vector3 currentPosition, out float currentHeading);
                Assert.That(Vector3.Distance(previousPosition, currentPosition), Is.EqualTo(1f).Within(0.02f));
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(previousHeading, currentHeading)), 1f,
                    "the route must use a broad, smooth turn rather than a sharp corner");
                previousPosition = currentPosition;
                previousHeading = currentHeading;
            }
        }

        [Test]
        public void RouteDataExposesLeftStraightAndRightForFutureSelection()
        {
            var model = new RoadPathModel(64, 18f, 0.4f, 100f, 0.4f, 90f, 6f);
            Assert.IsTrue(model.HasThreeWayIntersection);

            float distance = model.IntersectionStartDistance + model.IntersectionBranchLength;
            model.GetRoutePoseAtDistance(distance, RoadRouteDirection.Left, out Vector3 left, out _);
            model.GetRoutePoseAtDistance(distance, RoadRouteDirection.Straight, out Vector3 straight, out _);
            model.GetRoutePoseAtDistance(distance, RoadRouteDirection.Right, out Vector3 right, out _);

            Assert.Greater(Vector3.Distance(left, straight), 20f);
            Assert.Greater(Vector3.Distance(right, straight), 20f);
            Assert.Greater(Vector3.Distance(left, right), 40f);
            Assert.That(left.y, Is.InRange(0f, 0.4f));
            Assert.That(right.y, Is.InRange(0f, 0.4f));
        }
    }
}
