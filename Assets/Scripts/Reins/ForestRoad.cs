using UnityEngine;

namespace Reins
{
    /// <summary>Builds a bounded world-space forest road pool that recycles along a curved path.</summary>
    [ExecuteAlways]
    public sealed class ForestRoad : MonoBehaviour
    {
        [SerializeField, Min(6)] private int tileCount = 8;
        [SerializeField, Min(8f)] private float tileLength = 18f;
        [SerializeField, Min(6f)] private float roadWidth = 9.6f;
        [SerializeField, Min(0.1f)] private float laneWidth = 2.8f;
        [SerializeField] private Transform vehicleRoot;
        [SerializeField, Min(0.1f)] private float stoneLaneWidth = 1.15f;
        [SerializeField, Min(0.1f)] private float stoneDepth = 0.65f;

        [Header("Curvas")]
        [SerializeField, Range(0f, 2f)] private float curvatureScale = 1f;
        [SerializeField, Min(10f)] private float turnRadius = RoadPathModel.DefaultTurnRadius;
        [SerializeField, Range(0f, 1f)] private float heightAmplitude = 0.55f;
        [SerializeField, Min(25f)] private float heightWavelength = 90f;
        [SerializeField, Range(0f, 10f)] private float forkDivergence = 6f;

        [Header("Modelos reales (opcional)")]
        [SerializeField] private GameObject[] treePrefabs;
        [SerializeField] private GameObject rockPrefab;
        [SerializeField, Min(1f)] private float treeHeight = 7f;
        [SerializeField, Range(1, 4)] private int forestRows = 3;
        [SerializeField, Range(3, 7)] private int treesPerRow = 5;
        [SerializeField, Min(1f)] private float forestRowSpacing = 5f;
        [SerializeField, Min(0.05f)] private float rockFootprintPadding = 1f;

        [Header("Final del camino (opcional)")]
        [SerializeField, Min(0)] private int totalTiles;
        [SerializeField] private GameObject kingdomPrefab;

        private const int RecycleBehindChunks = 2;
        private const int MaximumChunks = 4096;

        private Tile[] _tiles;
        private RoadPathModel _path;
        private Material _roadMaterial;
        private Material _vergeMaterial;
        private Material _dividerMaterial;
        private Material _trunkMaterial;
        private Material _leafMaterial;
        private Material _stoneMaterial;
        private Texture2D _dirtTexture;

        private bool _initialized;
        private int _nextChunkIndex;
        private int _cartChunkIndex;
        private GameObject _kingdom;
        private GameObject _rearForest;

        public float TraveledDistance { get; private set; }
        public float LevelDistance => totalTiles > 0 ? totalTiles * tileLength : 0f;
        public bool HasReachedEnd => LevelDistance > 0f && TraveledDistance >= LevelDistance;
        public int CurrentChunkIndex => _cartChunkIndex;
        public int TotalTiles => totalTiles;
        public bool HasKingdom => _kingdom != null;

        private sealed class Tile
        {
            public Transform root;
            public int chunkIndex;
            public readonly Transform[] stones = new Transform[3];
            public readonly Transform[] branches = new Transform[2];
            public Transform mainRoad;
            public readonly bool[] consumed = new bool[3];
            public int blockedMask;

            public bool IsBuilt => root != null && root.gameObject.activeSelf;
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                Initialize();
            }
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                Initialize();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !_initialized)
            {
                return;
            }

            if (vehicleRoot == null)
            {
                var vehicle = GameObject.Find("VehicleRoot");
                if (vehicle != null)
                {
                    vehicleRoot = vehicle.transform;
                }
            }

            if (vehicleRoot == null)
            {
                return;
            }

            int nearest = FindNearestTile(vehicleRoot.position, out var localPosition);
            if (nearest >= 0)
            {
                _cartChunkIndex = _tiles[nearest].chunkIndex;
                float advance = Mathf.Max(0f, -localPosition.z);
                TraveledDistance = _cartChunkIndex * tileLength + advance;
            }

            for (var i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i].IsBuilt && _tiles[i].chunkIndex < _cartChunkIndex - RecycleBehindChunks)
                {
                    RecycleTile(_tiles[i]);
                }
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_tiles != null)
            {
                for (var i = 0; i < _tiles.Length; i++)
                {
                    if (_tiles[i] != null && _tiles[i].root != null)
                    {
                        Destroy(_tiles[i].root.gameObject);
                    }
                }

                _tiles = null;
            }

            if (_kingdom != null)
            {
                Destroy(_kingdom);
                _kingdom = null;
            }
            if (_rearForest != null)
            {
                Destroy(_rearForest);
                _rearForest = null;
            }

            DestroyMaterial(_roadMaterial);
            DestroyMaterial(_vergeMaterial);
            DestroyMaterial(_dividerMaterial);
            DestroyMaterial(_trunkMaterial);
            DestroyMaterial(_leafMaterial);
            DestroyMaterial(_stoneMaterial);
            if (_dirtTexture != null) Destroy(_dirtTexture);
            _dirtTexture = null;
            _roadMaterial = null;
            _vergeMaterial = null;
            _dividerMaterial = null;
            _trunkMaterial = null;
            _leafMaterial = null;
            _stoneMaterial = null;
            _initialized = false;
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            if (vehicleRoot == null)
            {
                var vehicle = GameObject.Find("VehicleRoot");
                if (vehicle != null)
                {
                    vehicleRoot = vehicle.transform;
                }
            }

            int poolSize = Mathf.Clamp(tileCount, 6, 10);
            _path = CreatePathModel();

            _roadMaterial = CreateMaterial(new Color(0.43f, 0.34f, 0.24f));
            _dirtTexture = CreateDirtTexture();
            _roadMaterial.mainTexture = _dirtTexture;
            _roadMaterial.mainTextureScale = new Vector2(1.5f, 3f);
            _vergeMaterial = CreateMaterial(new Color(0.16f, 0.34f, 0.12f));
            _dividerMaterial = CreateMaterial(new Color(0.86f, 0.79f, 0.53f));
            _trunkMaterial = CreateMaterial(new Color(0.28f, 0.16f, 0.08f));
            _leafMaterial = CreateMaterial(new Color(0.10f, 0.30f, 0.12f));
            _stoneMaterial = CreateMaterial(new Color(0.34f, 0.32f, 0.29f));

            _tiles = new Tile[poolSize];
            for (var i = 0; i < poolSize; i++)
            {
                var tileRoot = new GameObject("RoadTile_" + i).transform;
                tileRoot.SetParent(transform, false);
                var tile = new Tile { root = tileRoot, chunkIndex = i };
                _tiles[i] = tile;

                if (totalTiles > 0 && i >= totalTiles)
                {
                    // The road ends before the pool is exhausted: keep the spare tiles parked.
                    tile.chunkIndex = int.MaxValue;
                    tileRoot.gameObject.SetActive(false);
                    continue;
                }

                PlaceTile(tile, i);
            }

            _nextChunkIndex = poolSize;
            _cartChunkIndex = 0;
            BuildRearForest();
            BuildKingdom();
            _initialized = true;
        }

        private void BuildRearForest()
        {
            _rearForest = new GameObject("RearForest");
            _rearForest.transform.SetParent(transform, false);
            for (int row = 0; row < forestRows; row++)
            {
                for (int column = 0; column < 7; column++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int seed = row * 37 + column * 13 + side + 7;
                        var tree = new GameObject("RearTree_" + side + "_" + row + "_" + column).transform;
                        tree.SetParent(_rearForest.transform, false);
                        tree.localPosition = new Vector3(
                            side * (roadWidth * 0.5f + 2f + row * forestRowSpacing + seed % 3),
                            0f, 4f + column * 7.5f + row * 2f);
                        if (row == 0 && TryCreateTreeVisual(tree, seed)) continue;
                        float height = 4f + seed % 4 * 0.45f;
                        CreatePart(tree, "Trunk", PrimitiveType.Cylinder,
                            new Vector3(0f, height * 0.25f, 0f),
                            new Vector3(0.22f, height * 0.25f, 0.22f), _trunkMaterial);
                        CreatePart(tree, "Canopy", PrimitiveType.Sphere,
                            new Vector3(0f, height * 0.72f, 0f),
                            new Vector3(1.2f, height * 0.4f, 1.2f), _leafMaterial);
                    }
                }
            }
        }

        private void BuildKingdom()
        {
            if (kingdomPrefab == null || totalTiles <= 0)
            {
                return;
            }

            _path.GetChunkPose(totalTiles, out var position, out var headingDegrees);
            _kingdom = Instantiate(
                kingdomPrefab,
                position,
                Quaternion.Euler(0f, headingDegrees + 180f, 0f),
                transform);
            _kingdom.name = "Kingdom";
        }

        private void RecycleTile(Tile tile)
        {
            if (_nextChunkIndex >= _path.ChunkCount || (totalTiles > 0 && _nextChunkIndex >= totalTiles))
            {
                return;
            }

            tile.chunkIndex = _nextChunkIndex;
            _nextChunkIndex++;
            if (!tile.root.gameObject.activeSelf)
            {
                tile.root.gameObject.SetActive(true);
            }

            PlaceTile(tile, tile.chunkIndex);
        }

        private void PlaceTile(Tile tile, int chunkIndex)
        {
            _path.GetChunkPose(chunkIndex, out var position, out var headingDegrees);
            tile.root.position = position;
            tile.root.rotation = _path.GetChunkRotation(chunkIndex);
            tile.root.localScale = Vector3.one;

            if (tile.stones[0] == null)
            {
                BuildTileContent(tile, chunkIndex);
            }

            ConfigureBranches(tile, chunkIndex);
            ConfigureObstacleGroup(tile, chunkIndex);
        }

        private void BuildTileContent(Tile tile, int index)
        {
            Transform tileRoot = tile.root;
            CreatePart(tileRoot, "Road", PrimitiveType.Cube,
                new Vector3(0f, -0.06f, -tileLength * 0.5f), new Vector3(roadWidth, 0.12f, tileLength + 0.2f), _roadMaterial);
            tile.mainRoad = tileRoot.Find("Road");
            CreatePart(tileRoot, "GreenVerge_Left", PrimitiveType.Cube,
                new Vector3(-roadWidth * 0.5f - 0.65f, -0.03f, -tileLength * 0.5f), new Vector3(1.3f, 0.08f, tileLength), _vergeMaterial);
            CreatePart(tileRoot, "GreenVerge_Right", PrimitiveType.Cube,
                new Vector3(roadWidth * 0.5f + 0.65f, -0.03f, -tileLength * 0.5f), new Vector3(1.3f, 0.08f, tileLength), _vergeMaterial);

            for (int branch = -1; branch <= 1; branch += 2)
            {
                CreatePart(tileRoot, "Branch_" + branch, PrimitiveType.Cube,
                    new Vector3(0f, -0.055f, -tileLength * 0.5f),
                    new Vector3(roadWidth, 0.11f, tileLength + 0.2f), _roadMaterial);
                tile.branches[(branch + 1) / 2] = tileRoot.Find("Branch_" + branch);
            }

            for (var row = 0; row < forestRows; row++)
            {
                for (var tree = 0; tree < treesPerRow; tree++)
                {
                    for (var side = -1; side <= 1; side += 2)
                    {
                        int seed = Mathf.Abs(index * 53 + row * 17 + tree * 11 + side * 3);
                        float stagger = ((seed % 7) - 3) * 0.26f;
                        float z = -(tree + 0.5f) * tileLength / treesPerRow + stagger;
                        float forkClearance = forkDivergence;
                        float x = side * (roadWidth * 0.5f + 2f + forkClearance +
                                          row * forestRowSpacing + (seed % 4) * 0.55f);
                        var treeRoot = new GameObject("Tree_" + side + "_" + row + "_" + tree).transform;
                        treeRoot.SetParent(tileRoot, false);
                        treeRoot.localPosition = new Vector3(x, 0f, z);

                        if (row == 0 && TryCreateTreeVisual(treeRoot, seed)) continue;
                        float height = 3.5f + (seed % 4) * 0.55f;
                        CreatePart(treeRoot, "Trunk", PrimitiveType.Cylinder,
                            new Vector3(0f, height * 0.25f, 0f), new Vector3(0.22f, height * 0.25f, 0.22f), _trunkMaterial);
                        CreatePart(treeRoot, "Canopy", PrimitiveType.Sphere,
                            new Vector3(0f, height * 0.72f, 0f), new Vector3(1.1f, height * 0.40f, 1.1f), _leafMaterial);
                    }
                }
            }

            BuildStones(tile, index);
        }

        private bool TryCreateTreeVisual(Transform parent, int variationSeed)
        {
            if (treePrefabs == null || treePrefabs.Length == 0)
            {
                return false;
            }

            var prefab = treePrefabs[Mathf.Abs(variationSeed) % treePrefabs.Length];
            if (prefab == null)
            {
                return false;
            }

            var instance = Instantiate(prefab, parent, false);
            instance.name = "TreeModel";
            float height = treeHeight * (0.85f + (Mathf.Abs(variationSeed) % 5) * 0.06f);
            FitUniformHeight(instance, height);
            return true;
        }

        private void BuildStones(Tile tile, int index)
        {
            for (var lane = -1; lane <= 1; lane++)
            {
                GameObject stone;
                if (rockPrefab != null)
                {
                    stone = Instantiate(rockPrefab);
                    stone.name = "Stone_Lane_" + lane;
                    stone.transform.SetParent(tile.root, false);
                    FitHorizontalFootprint(
                        stone,
                        stoneLaneWidth * 2f * rockFootprintPadding,
                        stoneDepth * 2f * rockFootprintPadding);
                }
                else
                {
                    stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    stone.name = "Stone_Lane_" + lane;
                    stone.transform.SetParent(tile.root, false);
                    stone.transform.localScale = new Vector3(0.82f, 0.55f, 0.92f);
                    var renderer = stone.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = _stoneMaterial;
                    }
                }

                stone.transform.localPosition = new Vector3(lane * laneWidth, 0.34f, -tileLength * 0.25f);
                foreach (var collider in stone.GetComponentsInChildren<Collider>())
                {
                    Destroy(collider);
                }

                tile.stones[lane + 1] = stone.transform;
            }

            ConfigureObstacleGroup(tile, index);
        }

        private void ConfigureBranches(Tile tile, int chunkIndex)
        {
            bool fork = _path.HasForks && RoadPathModel.IsForkChunk(chunkIndex);
            if (tile.mainRoad != null) tile.mainRoad.gameObject.SetActive(!fork);
            float distance = (chunkIndex + 0.5f) * tileLength;
            for (int side = 0; side < tile.branches.Length; side++)
            {
                Transform branch = tile.branches[side];
                if (branch == null) continue;
                int sign = side == 0 ? -1 : 1;
                branch.gameObject.SetActive(fork);
                if (fork)
                {
                    branch.localPosition = new Vector3(
                        _path.BranchOffsetAtDistance(distance, sign), -0.055f, -tileLength * 0.5f);
                }
            }
        }

        private static void ConfigureObstacleGroup(Tile tile, int groupIndex)
        {
            tile.blockedMask = groupIndex < 2 || RoadPathModel.IsForkChunk(groupIndex)
                ? 0 : ObstacleSchedule.BlockedLaneMask(groupIndex);
            for (var laneIndex = 0; laneIndex < 3; laneIndex++)
            {
                tile.consumed[laneIndex] = false;
                if (tile.stones[laneIndex] != null)
                {
                    tile.stones[laneIndex].gameObject.SetActive((tile.blockedMask & (1 << laneIndex)) != 0);
                }
            }
        }

        /// <summary>
        /// Nearest built tile to the point. It also returns the point in that tile's local space,
        /// which yields a continuous distance along the road (even past the last tile).
        /// </summary>
        private int FindNearestTile(Vector3 worldPosition, out Vector3 localPosition)
        {
            localPosition = default;
            if (_tiles == null)
            {
                return -1;
            }

            var bestIndex = -1;
            var bestScore = float.MaxValue;
            for (var i = 0; i < _tiles.Length; i++)
            {
                if (!_tiles[i].IsBuilt)
                {
                    continue;
                }

                var local = _tiles[i].root.InverseTransformPoint(worldPosition);
                if (Mathf.Abs(local.x) <= roadWidth * 0.5f + 2f && local.z <= 0f && local.z >= -tileLength)
                {
                    localPosition = local;
                    return i;
                }

                var clampedX = Mathf.Max(0f, Mathf.Abs(local.x) - roadWidth * 0.5f);
                var clampedZ = local.z > 0f
                    ? local.z
                    : (local.z < -tileLength ? -tileLength - local.z : 0f);
                var score = clampedX * clampedX + clampedZ * clampedZ;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                    localPosition = local;
                }
            }

            return bestIndex;
        }

        /// <summary>Forward direction of the road at the given position, used to ride around curves.</summary>
        public bool TryGetRoadFrame(Vector3 worldPosition, out Vector3 forward)
        {
            forward = Vector3.forward;
            int index = FindNearestTile(worldPosition, out _);
            if (index < 0)
            {
                return false;
            }

            forward = _tiles[index].root.forward;
            return true;
        }

        /// <summary>Builds the same path used at runtime so tests and tooling can reason about it.</summary>
        public RoadPathModel CreatePathModel()
        {
            int poolSize = Mathf.Clamp(tileCount, 6, 10);
            int chunks = totalTiles > 0
                ? Mathf.Clamp(totalTiles + 1, poolSize + 1, MaximumChunks)
                : MaximumChunks;
            return new RoadPathModel(chunks, tileLength, curvatureScale, turnRadius,
                heightAmplitude, heightWavelength, forkDivergence);
        }

        public bool TryStopOnRock(Vector3 previousPosition, Vector3 currentPosition)
        {
            if (_tiles == null) return false;
            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i];
                if (!tile.IsBuilt)
                {
                    continue;
                }

                var localPrevious = tile.root.InverseTransformPoint(previousPosition);
                var localCurrent = tile.root.InverseTransformPoint(currentPosition);
                for (var laneIndex = 0; laneIndex < 3; laneIndex++)
                {
                    if ((tile.blockedMask & (1 << laneIndex)) == 0) continue;
                    var stone = tile.stones[laneIndex];
                    if (stone == null || !stone.gameObject.activeSelf) continue;
                    var localStone = tile.root.InverseTransformPoint(stone.position);
                    if (ObstacleCollisionModel.TrySweep(localPrevious.z, localCurrent.z,
                        localPrevious.x, localCurrent.x, localStone.z, localStone.x,
                        stoneDepth, stoneLaneWidth, ref tile.consumed[laneIndex]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static int GetObstacleBlockedLaneMask(int groupIndex)
        {
            return ObstacleSchedule.BlockedLaneMask(groupIndex);
        }

        private static void FitUniformHeight(GameObject instance, float targetHeight)
        {
            if (!TryGetRendererBounds(instance, out var bounds) || bounds.size.y <= 0.0001f)
            {
                return;
            }

            instance.transform.localScale *= targetHeight / bounds.size.y;
        }

        private static void FitHorizontalFootprint(GameObject instance, float targetWidth, float targetDepth)
        {
            if (!TryGetRendererBounds(instance, out var bounds))
            {
                return;
            }

            var scale = instance.transform.localScale;
            if (bounds.size.x > 0.0001f)
            {
                scale.x *= targetWidth / bounds.size.x;
            }

            if (bounds.size.z > 0.0001f)
            {
                scale.z *= targetDepth / bounds.size.z;
            }

            if (bounds.size.y > 0.0001f)
            {
                scale.y *= Mathf.Max(scale.x, scale.z);
            }

            instance.transform.localScale = scale;
        }

        private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static void CreatePart(Transform parent, string name, PrimitiveType primitive,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { color = color };
            return material;
        }

        private static Texture2D CreateDirtTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false)
            {
                name = "GeneratedDirtRoad",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.13f, y * 0.13f) * 0.26f +
                                  Mathf.PerlinNoise(x * 0.035f, y * 0.035f) * 0.28f;
                    float tracks = Mathf.Abs(x - size * 0.25f) < 7f ||
                                   Mathf.Abs(x - size * 0.75f) < 7f ? -0.09f : 0f;
                    float shade = Mathf.Clamp01(0.45f + noise + tracks);
                    pixels[y * size + x] = new Color(shade, shade * 0.88f, shade * 0.69f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void OnDrawGizmos()
        {
            var count = Mathf.Clamp(tileCount, 6, 10);
            var model = CreatePathModel();
            var width = Mathf.Max(roadWidth, laneWidth * 3f);
            var rootRotation = transform.rotation;
            var rootPosition = transform.position;
            for (var i = 0; i < count; i++)
            {
                if (totalTiles > 0 && i >= totalTiles)
                {
                    break;
                }

                model.GetChunkPose(i, out var localPosition, out var headingDegrees);
                var rotation = rootRotation * Quaternion.Euler(0f, headingDegrees, 0f);
                var center = rootPosition + rootRotation * (localPosition + rotation * new Vector3(0f, -0.06f, -tileLength * 0.5f));

                Gizmos.color = new Color(0.24f, 0.25f, 0.22f, 0.85f);
                Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
                Gizmos.DrawCube(Vector3.zero, new Vector3(width, 0.08f, tileLength));

                Gizmos.color = new Color(0.92f, 0.82f, 0.50f, 1f);
                for (var divider = -1; divider <= 1; divider += 2)
                {
                    Gizmos.DrawCube(new Vector3(divider * laneWidth * 0.5f, 0.02f, 0f),
                        new Vector3(0.09f, 0.025f, tileLength - 0.2f));
                }

                Gizmos.color = new Color(0.16f, 0.38f, 0.13f, 0.8f);
                Gizmos.DrawCube(new Vector3(-(width * 0.5f + 0.65f), 0f, 0f), new Vector3(1.3f, 0.06f, tileLength));
                Gizmos.DrawCube(new Vector3(width * 0.5f + 0.65f, 0f, 0f), new Vector3(1.3f, 0.06f, tileLength));

                Gizmos.matrix = Matrix4x4.identity;
            }

            if (totalTiles > 0)
            {
                model.GetChunkPose(totalTiles, out var endPosition, out var endHeading);
                var endCenter = rootPosition + rootRotation * endPosition;
                Gizmos.color = new Color(0.95f, 0.55f, 0.15f, 1f);
                Gizmos.DrawWireSphere(endCenter, 3f);
                Gizmos.DrawLine(endCenter,
                    endCenter + rootRotation * Quaternion.Euler(0f, endHeading, 0f) * Vector3.back * 8f);
            }
        }
    }
}
