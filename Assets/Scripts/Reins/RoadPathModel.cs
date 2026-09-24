using UnityEngine;

namespace Reins
{
    /// <summary>
    /// Deterministic centreline of the forest road. It integrates a fixed curvature schedule, adds
    /// gentle vertical undulation and can split into two branches that diverge and rejoin. Pure and
    /// allocation-free per query, so it can be unit tested and read every frame.
    /// </summary>
    public sealed class RoadPathModel
    {
        public const float DefaultChunkLength = 18f;
        public const float DefaultTurnRadius = 100f;
        public const float DefaultHeightAmplitude = 0f;
        public const float DefaultHeightWavelength = 55f;

        /// <summary>A fork starts every this many chunks and lasts <see cref="ForkLengthChunks"/>.</summary>
        public const int ForkPeriodChunks = 13;
        public const int ForkLengthChunks = 5;
        public const int ForkFirstChunk = 5;

        private const int StraightChunks = 5;
        private const int CurvingChunks = 2;

        private readonly float[] chunkX;
        private readonly float[] chunkY;
        private readonly float[] chunkZ;
        private readonly float[] chunkHeadingDegrees;

        public RoadPathModel(
            int chunkCount,
            float chunkLength = DefaultChunkLength,
            float curvatureScale = 1f,
            float turnRadius = DefaultTurnRadius,
            float heightAmplitude = DefaultHeightAmplitude,
            float heightWavelength = DefaultHeightWavelength,
            float forkDivergence = 0f)
        {
            ChunkLength = Mathf.Max(0.01f, chunkLength);
            CurvatureScale = curvatureScale;
            TurnRadius = Mathf.Max(1f, turnRadius);
            HeightAmplitude = heightAmplitude;
            HeightWavelength = Mathf.Max(4f, heightWavelength);
            ForkDivergence = Mathf.Max(0f, forkDivergence);

            chunkCount = Mathf.Max(1, chunkCount);
            chunkX = new float[chunkCount];
            chunkY = new float[chunkCount];
            chunkZ = new float[chunkCount];
            chunkHeadingDegrees = new float[chunkCount];

            float x = 0f;
            float z = 0f;
            float heading = 0f;
            for (int index = 0; index < chunkCount; index++)
            {
                chunkX[index] = x;
                chunkZ[index] = z;
                chunkY[index] = HeightAtDistance(index * ChunkLength, heightAmplitude, heightWavelength);
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
        public float HeightAmplitude { get; }
        public float HeightWavelength { get; }
        public float ForkDivergence { get; }
        public int ChunkCount => chunkHeadingDegrees.Length;
        public bool HasForks => ForkDivergence > 0.01f;
        public bool HasSlopes => Mathf.Abs(HeightAmplitude) > 0.01f;

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

        /// <summary>
        /// Gentle rise and fall along the road, kept at or above the flat ground plane so the terrain
        /// never pokes through the carriageway. Two out of phase waves stop it feeling like a
        /// metronome, and the amplitude stays small enough to be comfortable in a headset.
        /// </summary>
        public static float HeightAtDistance(float distance, float amplitude, float wavelength)
        {
            if (Mathf.Abs(amplitude) <= 0.001f || wavelength <= 0.1f)
            {
                return 0f;
            }

            float primary = Mathf.Sin(distance / wavelength * 2f * Mathf.PI);
            float secondary = Mathf.Sin(distance / (wavelength * 0.41f) * 2f * Mathf.PI + 1.7f);
            float profile = 0.5f + 0.5f * (0.72f * primary + 0.28f * secondary);
            return amplitude * Mathf.Clamp01(profile);
        }

        public float HeightAtDistance(float distance)
        {
            return HeightAtDistance(distance, HeightAmplitude, HeightWavelength);
        }

        /// <summary>Pose of a chunk, clamped to the built range so lookups never fail.</summary>
        public void GetChunkPose(int chunkIndex, out Vector3 position, out float headingDegrees)
        {
            int index = Mathf.Clamp(chunkIndex, 0, ChunkCount - 1);
            position = GetChunkPositionRaw(index);
            headingDegrees = chunkHeadingDegrees[index];
        }

        /// <summary>
        /// Rotation of a chunk, including the pitch of the slope. The tile's local -Z points along
        /// the direction of travel, which is the convention the road tiles are built with.
        /// </summary>
        public Quaternion GetChunkRotation(int chunkIndex)
        {
            int index = Mathf.Clamp(chunkIndex, 0, ChunkCount - 1);
            Vector3 behind = GetChunkPositionRaw(Mathf.Max(index - 1, 0));
            Vector3 ahead = GetChunkPositionRaw(Mathf.Min(index + 1, ChunkCount - 1));
            Vector3 direction = ahead - behind;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return Quaternion.Euler(0f, chunkHeadingDegrees[index], 0f);
            }

            return Quaternion.LookRotation(-direction.normalized, Vector3.up);
        }

        public Vector3 GetChunkPosition(int chunkIndex)
        {
            return GetChunkPositionRaw(Mathf.Clamp(chunkIndex, 0, ChunkCount - 1));
        }

        /// <summary>Continuous centreline pose, shared by the road and the cart.</summary>
        public void GetPoseAtDistance(float distance, out Vector3 position, out float headingDegrees)
        {
            if (ChunkCount == 1)
            {
                position = GetChunkPositionRaw(0);
                headingDegrees = chunkHeadingDegrees[0];
                return;
            }

            float chunk = Mathf.Clamp(distance / ChunkLength, 0f, ChunkCount - 1f);
            int index = Mathf.Min(Mathf.FloorToInt(chunk), ChunkCount - 2);
            float fraction = chunk - index;
            position = Vector3.Lerp(GetChunkPositionRaw(index), GetChunkPositionRaw(index + 1), fraction);
            headingDegrees = Mathf.LerpAngle(chunkHeadingDegrees[index], chunkHeadingDegrees[index + 1], fraction);
        }

        private Vector3 GetChunkPositionRaw(int index)
        {
            return new Vector3(chunkX[index], chunkY[index], chunkZ[index]);
        }

        public float GetChunkHeading(int chunkIndex)
        {
            return chunkHeadingDegrees[Mathf.Clamp(chunkIndex, 0, ChunkCount - 1)];
        }

        // ------------------------------------------------------------------ forks

        /// <summary>Chunk the fork containing <paramref name="chunkIndex"/> starts at, or -1.</summary>
        public static int ForkIndexForChunk(int chunkIndex)
        {
            int shifted = chunkIndex - ForkFirstChunk;
            if (shifted < 0)
            {
                return -1;
            }

            int period = ForkPeriodChunks + ForkLengthChunks;
            int within = shifted % period;
            return within < ForkLengthChunks ? chunkIndex - within : -1;
        }

        public static bool IsForkChunk(int chunkIndex)
        {
            return ForkIndexForChunk(chunkIndex) >= 0;
        }

        /// <summary>0 at the start of a fork, 1 at the end, negative when outside a fork.</summary>
        public static float ForkProgress(int chunkIndex)
        {
            int start = ForkIndexForChunk(chunkIndex);
            return start < 0 ? -1f : (chunkIndex - start) / (float)ForkLengthChunks;
        }

        /// <summary>Fork start chunk that owns a distance, or -1 outside a fork.</summary>
        public int ForkStartAtDistance(float distance)
        {
            if (!HasForks)
            {
                return -1;
            }

            int chunkIndex = Mathf.FloorToInt(Mathf.Max(0f, distance) / ChunkLength);
            return ForkIndexForChunk(chunkIndex);
        }

        /// <summary>Lateral separation of a branch from the centreline, in metres.</summary>
        public float BranchOffset(int chunkIndex, int branch)
        {
            if (!HasForks)
            {
                return 0f;
            }

            int start = ForkIndexForChunk(chunkIndex);
            if (start < 0)
            {
                return 0f;
            }

            float progress = (chunkIndex - start) / (float)ForkLengthChunks;
            return branch * ForkDivergence * Shape(progress);
        }

        public float BranchOffsetAtDistance(float distance, int branch)
        {
            if (!HasForks)
            {
                return 0f;
            }

            float chunkPosition = Mathf.Max(0f, distance) / ChunkLength;
            int chunkIndex = Mathf.FloorToInt(chunkPosition);
            float fraction = chunkPosition - chunkIndex;
            int start = ForkIndexForChunk(chunkIndex);
            if (start < 0)
            {
                // The tail of a fork can still overlap the chunk we are standing on.
                start = ForkIndexForChunk(chunkIndex - 1);
                if (start < 0)
                {
                    return 0f;
                }

                fraction += 1f;
            }

            float progress = (chunkIndex - start + fraction) / ForkLengthChunks;
            return branch * ForkDivergence * Shape(progress);
        }

        /// <summary>Heading change, in degrees, that the branch adds at a given distance.</summary>
        public float BranchYawAtDistance(float distance, int branch)
        {
            if (!HasForks)
            {
                return 0f;
            }

            const float probe = 0.75f;
            float behind = BranchOffsetAtDistance(distance - probe, branch);
            float ahead = BranchOffsetAtDistance(distance + probe, branch);
            return Mathf.Atan((ahead - behind) / (2f * probe)) * Mathf.Rad2Deg;
        }

        /// <summary>Where the fork sits inside its own length: 0 -> 1 -> 0, so branches rejoin.</summary>
        private static float Shape(float progress)
        {
            return Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
        }
    }
}
