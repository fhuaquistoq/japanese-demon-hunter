using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Reins.Tests
{
    public sealed class ForestRoadTests
    {
        [Test]
        public void GeneratedRoadTextureBakesInspectorConfiguredLaneDividers()
        {
            var roadObject = new GameObject("ForestRoadTextureTest");
            var road = roadObject.AddComponent<ForestRoad>();
            var markingColor = new Color(0.2f, 0.7f, 0.9f);
            SetPrivateField(road, "laneMarkingColor", markingColor);
            SetPrivateField(road, "laneMarkingStrength", 1f);
            SetPrivateField(road, "laneMarkingWidth", 0.09f);

            var createPixels = typeof(ForestRoad).GetMethod("CreateDirtPixels", BindingFlags.Instance | BindingFlags.NonPublic);
            var pixels = (Color[])createPixels.Invoke(road, new object[] { 128 });
            try
            {
                Assert.AreEqual(markingColor, pixels[64 * 128 + 45], "left lane divider pixel");
                Assert.AreEqual(markingColor, pixels[64 * 128 + 82], "right lane divider pixel");
                Assert.That(pixels[64 * 128 + 64], Is.Not.EqualTo(markingColor), "lane interiors stay unmarked");
                Assert.IsNotNull(typeof(ForestRoad).GetField("laneMarkingWidth", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetCustomAttribute<SerializeField>());
                Assert.IsNotNull(typeof(ForestRoad).GetField("laneMarkingColor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetCustomAttribute<SerializeField>());
                Assert.IsNotNull(typeof(ForestRoad).GetField("laneMarkingStrength", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetCustomAttribute<SerializeField>());
            }
            finally
            {
                Object.DestroyImmediate(roadObject);
            }
        }

        private static void SetPrivateField<T>(ForestRoad road, string fieldName, T value)
        {
            typeof(ForestRoad).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(road, value);
        }

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
