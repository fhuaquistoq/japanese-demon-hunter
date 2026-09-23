using NUnit.Framework;

namespace Reins.Tests
{
    public sealed class ForestRoadTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void ObstacleScheduleIsDeterministicAndAlwaysLeavesAnOpenLane(int group)
        {
            var mask = ForestRoad.GetObstacleBlockedLaneMask(group);
            Assert.AreEqual(mask, ForestRoad.GetObstacleBlockedLaneMask(group));

            var blockedCount = 0;
            for (var lane = -1; lane <= 1; lane++)
            {
                if (ObstacleSchedule.IsLaneBlocked(mask, lane)) blockedCount++;
            }

            Assert.That(blockedCount, Is.InRange(1, 2));
            Assert.That(3 - blockedCount, Is.GreaterThanOrEqualTo(1));
        }
    }
}
