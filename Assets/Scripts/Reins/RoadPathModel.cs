using UnityEngine;

namespace Reins
{
    /// <summary>The three routes exposed by the single road intersection.</summary>
    public enum RoadRouteDirection
    {
        Left = -1,
        Straight = 0,
        Right = 1
    }

    /// <summary>
    /// Deterministic centreline of the forest road. It integrates a fixed curvature schedule,
    /// adds gentle vertical undulation and exposes one three-way intersection. Pure and
    /// allocation-free per query, so the road renderer and a future route selector can share it.
    /// </summary>
    public sealed class RoadPathModel
    {
        public const float DefaultChunkLength = 18f;
        public const float DefaultTurnRadius = 100f;
        public const float DefaultHeightAmplitude = 0f;
        public const float DefaultHeightWavelength = 55f;
        public const int DefaultIntersectionStartChunk = 7;
        public const int DefaultIntersectionBranchLengthChunks = 5;
        public const float DefaultIntersectionTurnDegrees = 55f;

        // Compatibility aliases for tooling that still describes the old repeated forks.
        public const int ForkFirstChunk = DefaultIntersectionStartChunk;
        public const int ForkLengthChunks = DefaultIntersectionBranchLengthChunks;

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
            float forkDivergence = 0f,
            bool enableThreeWayIntersection = true,
            int intersectionStartChunk = DefaultIntersectionStartChunk,
            int intersectionBranchLengthChunks = DefaultIntersectionBranchLengthChunks,
            float intersectionTurnDegrees = DefaultIntersectionTurnDegrees)
        {
            ChunkLength = Mathf.Max(0.01f, chunkLength);
            CurvatureScale = curvatureScale;
            TurnRadius = Mathf.Max(1f, turnRadius);
            HeightAmplitude = heightAmplitude;
            HeightWavelength = Mathf.Max(4f, heightWavelength);
            ForkDivergence = Mathf.Max(0f, forkDivergence);

            chunkCount = Mathf.Max(1, chunkCount);
            IntersectionStartChunk = Mathf.Clamp(intersectionStartChunk, 1, Mathf.Max(1, chunkCount - 1));
            IntersectionBranchLengthChunks = Mathf.Max(2, intersectionBranchLengthChunks);
            IntersectionTurnDegrees = Mathf.Clamp(intersectionTurnDegrees, 15f, 75f);
            HasThreeWayIntersection = enableThreeWayIntersection && ForkDivergence > 0.01f &&
                                      IntersectionStartChunk < chunkCount;

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

        /// <summary>
        /// Legacy serialized value retained to avoid breaking existing scenes. A positive value
        /// enables the intersection and remains useful as decoration clearance in ForestRoad.
        /// </summary>
        public float ForkDivergence { get; }

        public int ChunkCount => chunkHeadingDegrees.Length;
        public bool HasSlopes => Mathf.Abs(HeightAmplitude) > 0.01f;
        public bool HasThreeWayIntersection { get; }
        public bool HasForks => HasThreeWayIntersection;
        public int IntersectionStartChunk { get; }
        public int IntersectionBranchLengthChunks { get; }
        public float IntersectionTurnDegrees { get; }
        public float IntersectionStartDistance => IntersectionStartChunk * ChunkLength;
        public float IntersectionBranchLength => IntersectionBranchLengthChunks * ChunkLength;

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
        /// never pokes through the carriageway. Two out of phase waves stop it feeling repetitive.
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

        /// <summary>
        /// Pose of LEFT, STRAIGHT or RIGHT. Before the intersection all three queries return the
        /// same main road. Side routes follow a broad circular arc and then continue on their final
        /// heading, making them usable path data rather than decorative meshes.
        /// </summary>
        public void GetRoutePoseAtDistance(float distance, RoadRouteDirection route,
            out Vector3 position, out float headingDegrees)
        {
            distance = Mathf.Max(0f, distance);
            if (route == RoadRouteDirection.Straight || !HasThreeWayIntersection ||
                distance <= IntersectionStartDistance)
            {
                GetPoseAtDistance(distance, out position, out headingDegrees);
                return;
            }

            GetChunkPose(IntersectionStartChunk, out Vector3 origin, out float originHeading);
            float routeDistance = distance - IntersectionStartDistance;
            float turnLength = Mathf.Max(ChunkLength, IntersectionBranchLength);
            float clampedTurnDistance = Mathf.Min(routeDistance, turnLength);
            float turnRadians = IntersectionTurnDegrees * Mathf.Deg2Rad;
            float radius = turnLength / turnRadians;
            float angle = clampedTurnDistance / radius;
            int side = (int)route;

            var localOffset = new Vector3(
                side * radius * (1f - Mathf.Cos(angle)),
                0f,
                -radius * Mathf.Sin(angle));

            float extraDistance = Mathf.Max(0f, routeDistance - turnLength);
            float localHeading = -side * IntersectionTurnDegrees;
            if (extraDistance > 0f)
            {
                localOffset += Quaternion.Euler(0f, localHeading, 0f) * Vector3.back * extraDistance;
            }

            position = origin + Quaternion.Euler(0f, originHeading, 0f) * localOffset;
            position.y = HeightAtDistance(distance);
            headingDegrees = originHeading - side * angle * Mathf.Rad2Deg;
        }

        public Quaternion GetRouteRotationAtDistance(float distance, RoadRouteDirection route)
        {
            const float probe = 0.5f;
            GetRoutePoseAtDistance(Mathf.Max(0f, distance - probe), route, out Vector3 behind, out _);
            GetRoutePoseAtDistance(distance + probe, route, out Vector3 ahead, out float heading);
            Vector3 direction = ahead - behind;
            return direction.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(-direction.normalized, Vector3.up)
                : Quaternion.Euler(0f, heading, 0f);
        }

        public bool IsIntersectionBranchChunk(int chunkIndex)
        {
            return HasThreeWayIntersection && chunkIndex >= IntersectionStartChunk &&
                   chunkIndex < IntersectionStartChunk + IntersectionBranchLengthChunks;
        }

        private Vector3 GetChunkPositionRaw(int index)
        {
            return new Vector3(chunkX[index], chunkY[index], chunkZ[index]);
        }

        public float GetChunkHeading(int chunkIndex)
        {
            return chunkHeadingDegrees[Mathf.Clamp(chunkIndex, 0, ChunkCount - 1)];
        }

        // Compatibility helpers used by existing tooling. They now describe the one intersection.
        public static int ForkIndexForChunk(int chunkIndex)
        {
            return chunkIndex >= DefaultIntersectionStartChunk &&
                   chunkIndex < DefaultIntersectionStartChunk + DefaultIntersectionBranchLengthChunks
                ? DefaultIntersectionStartChunk
                : -1;
        }

        public static bool IsForkChunk(int chunkIndex)
        {
            return ForkIndexForChunk(chunkIndex) >= 0;
        }

        public static float ForkProgress(int chunkIndex)
        {
            int start = ForkIndexForChunk(chunkIndex);
            return start < 0 ? -1f : (chunkIndex - start) / (float)DefaultIntersectionBranchLengthChunks;
        }

        public int ForkStartAtDistance(float distance)
        {
            int chunkIndex = Mathf.FloorToInt(Mathf.Max(0f, distance) / ChunkLength);
            return IsIntersectionBranchChunk(chunkIndex) ? IntersectionStartChunk : -1;
        }

        public float BranchOffset(int chunkIndex, int branch)
        {
            return BranchOffsetAtDistance(chunkIndex * ChunkLength, branch);
        }

        public float BranchOffsetAtDistance(float distance, int branch)
        {
            if (branch == 0 || !HasThreeWayIntersection)
            {
                return 0f;
            }

            GetPoseAtDistance(distance, out Vector3 centre, out float centreHeading);
            GetRoutePoseAtDistance(distance, branch < 0 ? RoadRouteDirection.Left : RoadRouteDirection.Right,
                out Vector3 route, out _);
            Vector3 right = Quaternion.Euler(0f, centreHeading, 0f) * Vector3.right;
            return Vector3.Dot(route - centre, right);
        }

        public float BranchYawAtDistance(float distance, int branch)
        {
            if (branch == 0 || !HasThreeWayIntersection)
            {
                return 0f;
            }

            GetPoseAtDistance(distance, out _, out float centreHeading);
            GetRoutePoseAtDistance(distance, branch < 0 ? RoadRouteDirection.Left : RoadRouteDirection.Right,
                out _, out float routeHeading);
            return Mathf.DeltaAngle(centreHeading, routeHeading);
        }
    }
}
