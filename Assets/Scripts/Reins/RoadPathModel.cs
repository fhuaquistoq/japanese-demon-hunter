using UnityEngine;

namespace Reins
{
    /// <summary>
    /// Deterministic centreline of the forest road, integrated chunk by chunk from a fixed
    /// curvature schedule. Pure and allocation-free per query, so it can be unit tested and read
    /// every frame. A zero scale reproduces the original straight road toward negative Z.
    /// </summary>
    public sealed class RoadPathModel
    {
        public const float DefaultChunkLength = 18f;
        public const float DefaultTurnRadius = 100f;

        private const int StraightChunks = 5;
        private const int CurvingChunks = 2;

        private readonly float[] chunkX;
        private readonly float[] chunkZ;
        private readonly float[] chunkHeadingDegrees;

        public RoadPathModel(
            int chunkCount,
            float chunkLength = DefaultChunkLength,
            float curvatureScale = 1f,
            float turnRadius = DefaultTurnRadius)
        {
            ChunkLength = Mathf.Max(0.01f, chunkLength);
            CurvatureScale = curvatureScale;
            TurnRadius = Mathf.Max(1f, turnRadius);
            chunkCount = Mathf.Max(1, chunkCount);
            chunkX = new float[chunkCount];
            chunkZ = new float[chunkCount];
            chunkHeadingDegrees = new float[chunkCount];

            float x = 0f;
            float z = 0f;
            float heading = 0f;
            for (int index = 0; index < chunkCount; index++)
            {
                chunkX[index] = x;
                chunkZ[index] = z;
                chunkHeadingDegrees[index] = heading;

                float curvature = CurvatureForChunk(index, CurvatureScale, TurnRadius);
                float turn = ChunkLength * curvature;
                float halfTurnDegrees = 0.5f * turn * Mathf.Rad2Deg;
                Vector3 chordDirection = Quaternion.Euler(0f, heading + halfTurnDegrees, 0f) * Vector3.back;
                float chordLength = Mathf.Abs(turn) > 1e-5f
                    ? ChunkLength * Mathf.Abs(Mathf.Sin(0.5f * turn) / (0.5f * turn))
                    : ChunkLength;

                Vector3 displacement = chordDirection * chordLength;
                x += displacement.x;
                z += displacement.z;
                heading += turn * Mathf.Rad2Deg;
            }
        }

        public float ChunkLength { get; }
        public float CurvatureScale { get; }
        public float TurnRadius { get; }
        public int ChunkCount => chunkHeadingDegrees.Length;

        /// <summary>Signed curvature (1/radius) of a chunk. Positive turns left, negative right.</summary>
        public static float CurvatureForChunk(int chunkIndex, float curvatureScale, float turnRadius)
        {
            int period = StraightChunks * 2 + CurvingChunks * 2;
            int slot = Mathf.Abs(chunkIndex) % period;
            float magnitude = Mathf.Max(0f, curvatureScale) / Mathf.Max(1f, turnRadius);
            if (slot >= StraightChunks && slot < StraightChunks + CurvingChunks)
            {
                return magnitude;
            }

            if (slot >= StraightChunks + CurvingChunks + StraightChunks &&
                slot < StraightChunks + CurvingChunks + StraightChunks + CurvingChunks)
            {
                return -magnitude;
            }

            return 0f;
        }

        /// <summary>Pose of a chunk, clamped to the built range so lookups never fail.</summary>
        public void GetChunkPose(int chunkIndex, out Vector3 position, out float headingDegrees)
        {
            int index = Mathf.Clamp(chunkIndex, 0, ChunkCount - 1);
            position = new Vector3(chunkX[index], 0f, chunkZ[index]);
            headingDegrees = chunkHeadingDegrees[index];
        }

        public Vector3 GetChunkPosition(int chunkIndex)
        {
            int index = Mathf.Clamp(chunkIndex, 0, ChunkCount - 1);
            return new Vector3(chunkX[index], 0f, chunkZ[index]);
        }

        public float GetChunkHeading(int chunkIndex)
        {
            return chunkHeadingDegrees[Mathf.Clamp(chunkIndex, 0, ChunkCount - 1)];
        }
    }
}
