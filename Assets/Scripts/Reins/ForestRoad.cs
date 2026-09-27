using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        [SerializeField, Min(0.1f)] private float laneMarkingWidth = 0.09f;
        [SerializeField] private Color laneMarkingColor = new Color(0.66f, 0.57f, 0.39f);
        [SerializeField, Range(0f, 1f)] private float laneMarkingStrength = 0.72f;
        [SerializeField] private bool buildLaneDividers;
        [SerializeField] private Transform vehicleRoot;
        [SerializeField, Min(0.1f)] private float stoneLaneWidth = 1.15f;
        [SerializeField, Min(0.1f)] private float stoneDepth = 0.65f;
        [Tooltip("Las rocas del camino tapan la vista del jugador sobre la carreta; apagado deja el camino limpio.")]
        [SerializeField] private bool buildRoadRocks;

        [Header("Curvas")]
        [SerializeField, Range(0f, 2f)] private float curvatureScale = 1f;
        [SerializeField, Min(10f)] private float turnRadius = RoadPathModel.DefaultTurnRadius;
        [SerializeField, Range(0f, 1f)] private float heightAmplitude = 0.55f;
        [SerializeField, Min(25f)] private float heightWavelength = 90f;
        [SerializeField, Range(0f, 10f)] private float forkDivergence = 6f;
        [SerializeField] private bool buildRoadForks;

        [Header("Modelos reales (opcional)")]
        [SerializeField] private GameObject[] treePrefabs;
        [SerializeField] private GameObject rockPrefab;
        [SerializeField, Min(1f)] private float treeHeight = 7f;
        [SerializeField, Range(1, 6)] private int forestRows = 3;
        [SerializeField, Range(0, 4)] private int farForestRows = 2;
        [SerializeField, Range(3, 10)] private int treesPerRow = 5;
        [SerializeField, Min(1f)] private float forestRowSpacing = 5f;
        [SerializeField] private bool buildDecorativeEdgeStones = true;
        [SerializeField, Range(1, 3)] private int edgeStonePairsPerTile = 2;
        [SerializeField, Min(0.05f)] private float rockFootprintPadding = 1f;
        [Tooltip("Foliage is the densest thing in the scene, so its shadows are switched off: hundreds " +
                 "of extra shadow casters cost far more than they add at night.")]
        [SerializeField] private bool castSceneryShadows;
        [Tooltip("How many tiles either side of the carriage keep their trees enabled. Beyond this they " +
                 "are switched off in one go, per tile, instead of being culled one by one.")]
        [SerializeField, Range(0, 6)] private int treeBatchChunkSpan = 3;
        [Tooltip("How close to the end of the road the kingdom is switched on. It is far more geometry " +
                 "than anything else in the scene and is invisible until the last stretch.")]
        [SerializeField, Min(0f)] private float kingdomRevealDistance = 90f;

        [Header("Faroles de la carretera (opcional)")]
        [SerializeField] private GameObject postLanternPrefab;
        [SerializeField, Min(4f)] private float postLanternSpacing = 18f;
        [SerializeField, Min(0f)] private float postLanternOffset = 1.1f;
        [SerializeField, Min(0.1f)] private float postLanternHeight = 3f;
        [SerializeField] private Color postLanternColor = new Color(1f, 0.70f, 0.34f);
        [SerializeField, Min(0f)] private float postLanternIntensity = 4.5f;
        [SerializeField, Min(0f)] private float postLanternRange = 22f;
        [Tooltip("How many tiles either side of the carriage keep their lanterns lit. Real-time point " +
                 "lights are far too expensive to keep every one of them switched on.")]
        [SerializeField, Range(0, 6)] private int lanternLitChunkSpan = 3;

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
        private Texture2D _barkTexture;
        private Texture2D _leafTexture;
        private Texture2D _owlEyesTexture;
        private Sprite _owlEyesSprite;

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

        private int TotalForestRows => Mathf.Max(1, forestRows) + Mathf.Max(0, farForestRows);

        private float GetForestRowOffset(int row)
        {
            int nearRows = Mathf.Max(1, forestRows);
            return row < nearRows
                ? row * forestRowSpacing
                : nearRows * forestRowSpacing + (row - nearRows + 1) * forestRowSpacing * 1.25f;
        }

        private sealed class Tile
        {
            public Transform root;
            public int chunkIndex;
            public readonly Transform[] stones = new Transform[3];
            public readonly Transform[] branches = new Transform[2];
            public Transform mainRoad;
            public Transform treeBatch;
            public readonly bool[] consumed = new bool[3];
            public int blockedMask;
            public readonly List<Light> lanternLights = new List<Light>();

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

            UpdateDistanceCulling();
        }

        /// <summary>
        /// Keeps only what is near the carriage alive. The fog hides anything past it, so the foliage of
        /// distant tiles is switched off as one object per tile, and the road lanterns only keep their
        /// real lights switched on while they are close enough to matter. The rear forest is parked once
        /// the carriage has left it behind.
        /// </summary>
        private void UpdateDistanceCulling()
        {
            for (var i = 0; i < _tiles.Length; i++)
            {
                Tile tile = _tiles[i];
                if (!tile.IsBuilt)
                {
                    continue;
                }

                int distanceInChunks = Mathf.Abs(tile.chunkIndex - _cartChunkIndex);
                bool nearCart = distanceInChunks <= treeBatchChunkSpan;

                if (tile.treeBatch != null && tile.treeBatch.gameObject.activeSelf != nearCart)
                {
                    tile.treeBatch.gameObject.SetActive(nearCart);
                }

                if (tile.lanternLights.Count == 0)
                {
                    continue;
                }

                bool lit = distanceInChunks <= lanternLitChunkSpan;
                for (var l = 0; l < tile.lanternLights.Count; l++)
                {
                    Light light = tile.lanternLights[l];
                    if (light != null && light.enabled != lit)
                    {
                        light.enabled = lit;
                    }
                }
            }

            if (_rearForest != null)
            {
                // One check for the whole rear wall: it stays parked once the ride has moved on.
                bool behindTheStart = _cartChunkIndex <= treeBatchChunkSpan;
                if (_rearForest.activeSelf != behindTheStart)
                {
                    _rearForest.SetActive(behindTheStart);
                }
            }

            if (_kingdom != null)
            {
                // The kingdom is a lot of geometry parked at the very end of the road. Fog is not culling,
                // so without this it is drawn for the whole ride while being invisible; it is switched on
                // only once it is close enough to emerge through the mist.
                bool nearEnd = LevelDistance <= 0f ||
                               TraveledDistance >= LevelDistance - kingdomRevealDistance;
                if (_kingdom.activeSelf != nearEnd)
                {
                    _kingdom.SetActive(nearEnd);
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
            if (_barkTexture != null) Destroy(_barkTexture);
            if (_leafTexture != null) Destroy(_leafTexture);
            if (_owlEyesSprite != null) Destroy(_owlEyesSprite);
            if (_owlEyesTexture != null) Destroy(_owlEyesTexture);
            _dirtTexture = null;
            _barkTexture = null;
            _leafTexture = null;
            _owlEyesSprite = null;
            _owlEyesTexture = null;
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

            // Earth carriageway against a dark green verge: under a night this dim the two otherwise
            // wash into the same colour and the road stops reading as a road.
            _roadMaterial = CreateMaterial(new Color(0.44f, 0.30f, 0.17f));
            _dirtTexture = CreateDirtTexture();
            _roadMaterial.mainTexture = _dirtTexture;
            _roadMaterial.mainTextureScale = new Vector2(1f, 3f);
            _vergeMaterial = CreateMaterial(new Color(0.09f, 0.20f, 0.08f));
            _dividerMaterial = CreateMaterial(new Color(0.86f, 0.79f, 0.53f));
            _trunkMaterial = CreateMaterial(new Color(0.24f, 0.14f, 0.07f));
            _leafMaterial = CreateMaterial(new Color(0.07f, 0.19f, 0.09f));
            _stoneMaterial = CreateMaterial(new Color(0.34f, 0.32f, 0.29f));
            _barkTexture = CreateBarkTexture();
            _leafTexture = CreateLeafTexture();
            _trunkMaterial.mainTexture = _barkTexture;
            _leafMaterial.mainTexture = _leafTexture;
            _owlEyesTexture = CreateOwlEyesTexture();
            _owlEyesSprite = Sprite.Create(
                _owlEyesTexture, new Rect(0f, 0f, _owlEyesTexture.width, _owlEyesTexture.height),
                new Vector2(0.5f, 0.5f), 128f);
            _owlEyesSprite.name = "GeneratedOwlEyes";

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
            for (int row = 0; row < TotalForestRows; row++)
            {
                // One more column than a road tile so the wall behind the carriage has no seam.
                int columns = Mathf.Max(4, treesPerRow + 1);
                for (int column = 0; column < columns; column++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int seed = row * 37 + column * 13 + side + 7;
                        var tree = new GameObject("RearTree_" + side + "_" + row + "_" + column).transform;
                        tree.SetParent(_rearForest.transform, false);
                        tree.localPosition = new Vector3(
                            side * (roadWidth * 0.5f + 2f + GetForestRowOffset(row) + seed % 3),
                            0f, 4f + column * 7.5f + row * 2f);
                        if (TryCreateTreeVisual(tree, seed)) continue;
                        // Only reached when no tree model is assigned: the block tree below is the
                        // bare-bones stand-in for a project that ships without the fbx art.
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

            DisableSceneryShadows(_rearForest.transform);
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

            // Every tree of the tile lives under one object. Switching a whole tile's worth of foliage
            // on and off is then a single SetActive instead of walking hundreds of renderers, which is
            // what makes it affordable to keep only the tiles near the carriage populated.
            var treeBatch = new GameObject("TreeBatch").transform;
            treeBatch.SetParent(tileRoot, false);
            tile.treeBatch = treeBatch;

            for (var row = 0; row < TotalForestRows; row++)
            {
                for (var tree = 0; tree < treesPerRow; tree++)
                {
                    for (var side = -1; side <= 1; side += 2)
                    {
                        int seed = Mathf.Abs(index * 53 + row * 17 + tree * 11 + side * 3);
                        float stagger = ((seed % 7) - 3) * 0.26f;
                        float z = -(tree + 0.5f) * tileLength / treesPerRow + stagger;
                        float forkClearance = _path.HasForks ? forkDivergence : 0f;
                        float x = side * (roadWidth * 0.5f + 2f + forkClearance +
                                          GetForestRowOffset(row) + (seed % 4) * 0.55f);
                        var treeRoot = new GameObject("Tree_" + side + "_" + row + "_" + tree).transform;
                        treeRoot.SetParent(treeBatch, false);
                        treeRoot.localPosition = new Vector3(x, 0f, z);

                        if (TryCreateTreeVisual(treeRoot, seed)) continue;
                        // Only reached when no tree model is assigned: the block tree below is the
                        // bare-bones stand-in for a project that ships without the fbx art.
                        float height = 3.5f + (seed % 4) * 0.55f;
                        CreatePart(treeRoot, "Trunk", PrimitiveType.Cylinder,
                            new Vector3(0f, height * 0.25f, 0f), new Vector3(0.22f, height * 0.25f, 0.22f), _trunkMaterial);
                        CreatePart(treeRoot, "Canopy", PrimitiveType.Sphere,
                            new Vector3(0f, height * 0.72f, 0f), new Vector3(1.1f, height * 0.40f, 1.1f), _leafMaterial);
                    }
                }
            }

            BuildOwlEyes(treeBatch, index);
            BuildPostLanterns(tileRoot, tile);
            BuildStones(tile, index);
            BuildDecorativeEdgeStones(tileRoot, index);
            // Last, so it covers the road, its markings, the posts and the foliage in one pass.
            DisableSceneryShadows(tileRoot);
        }

        /// <summary>
        /// Turns off shadow casting for a whole group of scenery in one pass. At night the only shadows
        /// that read are the carriage's own, and every extra caster costs a shadow pass over hundreds of
        /// objects, so the road, its markings, the posts and the foliage all opt out.
        /// </summary>
        private void DisableSceneryShadows(Transform group)
        {
            if (castSceneryShadows || group == null)
            {
                return;
            }

            foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// A lit post on each side of the carriageway. They belong to the tile so they recycle with the
        /// road instead of lining its whole length at once, and only the tiles near the carriage keep
        /// their real lights switched on.
        /// </summary>
        private void BuildPostLanterns(Transform tileRoot, Tile tile)
        {
            tile.lanternLights.Clear();
            if (postLanternPrefab == null)
            {
                return;
            }

            int chunksBetweenPosts = Mathf.Max(1, Mathf.RoundToInt(postLanternSpacing / tileLength));
            if (tile.chunkIndex % chunksBetweenPosts != 0)
            {
                return;
            }

            int posts = Mathf.Max(1, Mathf.RoundToInt(tileLength / Mathf.Max(4f, postLanternSpacing)));
            for (var post = 0; post < posts; post++)
            {
                float z = -(post + 0.5f) * tileLength / posts;
                for (var side = -1; side <= 1; side += 2)
                {
                    var holder = new GameObject("PostLantern_" + side + "_" + post).transform;
                    holder.SetParent(tileRoot, false);
                    holder.localPosition = new Vector3(
                        side * (roadWidth * 0.5f + postLanternOffset), 0f, z);
                    holder.localRotation = Quaternion.identity;

                    GameObject instance = Instantiate(postLanternPrefab, holder, false);
                    instance.name = "PostLantern_Model";
                    FitUniformHeight(instance, postLanternHeight);
                    // The head is on the model's +Z, so the post is turned a quarter turn to aim it at
                    // the middle of the road instead of down the road.
                    instance.transform.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f);

                    tile.lanternLights.Add(CreatePostLanternLight(holder, side));
                }
            }
        }

        private Light CreatePostLanternLight(Transform holder, int side)
        {
            // Measured off the source model: the lantern head sits at 78% of the post's height and its
            // centre is 41% of that height out to one side, which after the quarter turn points inward.
            float headHeight = postLanternHeight * 0.78f;
            float headReach = postLanternHeight * 0.41f;

            var lightObject = new GameObject("LanternLight");
            lightObject.transform.SetParent(holder, false);
            lightObject.transform.localPosition = new Vector3(-side * headReach, headHeight, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = postLanternColor;
            light.intensity = postLanternIntensity;
            light.range = postLanternRange;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
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
            ApplyTreeMaterials(instance);
            return true;
        }

        private void ApplyTreeMaterials(GameObject tree)
        {
            foreach (Renderer renderer in tree.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < materials.Length; index++)
                {
                    if (materials[index] == null)
                    {
                        continue;
                    }

                    string name = materials[index].name;
                    if (name.IndexOf("Bark", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        materials[index] = _trunkMaterial;
                        changed = true;
                    }
                    else if (name.IndexOf("Leaf", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        materials[index] = _leafMaterial;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private void BuildDecorativeEdgeStones(Transform tileRoot, int index)
        {
            if (!buildDecorativeEdgeStones)
            {
                return;
            }

            int count = Mathf.Max(1, edgeStonePairsPerTile);
            for (int stoneIndex = 0; stoneIndex < count; stoneIndex++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    int seed = Mathf.Abs(index * 31 + stoneIndex * 7 + side * 3);
                    GameObject stone = rockPrefab != null
                        ? Instantiate(rockPrefab, tileRoot, false)
                        : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    stone.name = "DecorativeEdgeStone_" + side + "_" + stoneIndex;
                    stone.transform.SetParent(tileRoot, false);
                    if (rockPrefab != null)
                    {
                        FitHorizontalFootprint(stone, 0.55f, 0.50f);
                    }
                    else
                    {
                        stone.transform.localScale = new Vector3(0.42f, 0.22f, 0.38f);
                        Renderer renderer = stone.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.sharedMaterial = _stoneMaterial;
                        }
                    }

                    float z = -(stoneIndex + 0.5f) * tileLength / count + ((seed % 7) - 3) * 0.24f;
                    float x = side * (roadWidth * 0.5f + 0.85f + (seed % 3) * 0.12f);
                    stone.transform.localPosition = new Vector3(x, 0.12f, z);
                    stone.transform.localRotation = Quaternion.Euler(0f, seed % 360, 0f);
                    foreach (Collider collider in stone.GetComponentsInChildren<Collider>(true))
                    {
                        Destroy(collider);
                    }
                }
            }
        }

        private void BuildStones(Tile tile, int index)
        {
            if (!buildRoadRocks)
            {
                return;
            }

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

        private void ConfigureObstacleGroup(Tile tile, int groupIndex)
        {
            tile.blockedMask = !buildRoadRocks || groupIndex < 2 || RoadPathModel.IsForkChunk(groupIndex)
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
                heightAmplitude, heightWavelength, buildRoadForks ? forkDivergence : 0f);
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

        private Texture2D CreateBarkTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false)
            {
                name = "GeneratedTreeBark",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float groove = 0.84f + 0.12f * Mathf.Sin((u + 0.012f * Mathf.Sin(v * Mathf.PI * 6f)) * Mathf.PI * 42f);
                    float noise = TileablePerlin(u, v, 8f) * 0.12f;
                    float shade = Mathf.Clamp01(groove + noise - 0.04f);
                    pixels[y * size + x] = new Color(shade, shade, shade, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private Texture2D CreateLeafTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false)
            {
                name = "GeneratedTreeLeaves",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float broad = TileablePerlin(u, v, 6f);
                    float fine = TileablePerlin(u, v, 20f);
                    float shade = Mathf.Lerp(0.58f, 1f, broad * 0.72f + fine * 0.28f);
                    pixels[y * size + x] = new Color(shade, shade, shade, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateOwlEyesTexture()
        {
            const int width = 48;
            const int height = 24;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "GeneratedOwlEyesTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float leftDistance = Mathf.Sqrt(Mathf.Pow((x - 14f) / 8f, 2f) + Mathf.Pow((y - 12f) / 9f, 2f));
                    float rightDistance = Mathf.Sqrt(Mathf.Pow((x - 34f) / 8f, 2f) + Mathf.Pow((y - 12f) / 9f, 2f));
                    float distance = Mathf.Min(leftDistance, rightDistance);
                    if (distance <= 1f)
                    {
                        pixels[y * width + x] = distance <= 0.34f
                            ? new Color(0.035f, 0.020f, 0.012f, 1f)
                            : new Color(1f, 0.70f, 0.26f, 1f);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void BuildOwlEyes(Transform treeBatch, int chunkIndex)
        {
            if (_owlEyesSprite == null || chunkIndex % 2 != 0)
            {
                return;
            }

            int side = chunkIndex % 4 == 0 ? -1 : 1;
            var eyes = new GameObject("OwlEyes");
            eyes.transform.SetParent(treeBatch, false);
            eyes.transform.localPosition = new Vector3(
                side * (roadWidth * 0.5f + forestRowSpacing * 0.7f), 3.8f, -tileLength * 0.5f);
            eyes.transform.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f);
            SpriteRenderer renderer = eyes.AddComponent<SpriteRenderer>();
            renderer.sprite = _owlEyesSprite;
            renderer.color = new Color(1f, 0.80f, 0.45f, 0.95f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private Texture2D CreateDirtTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false)
            {
                name = "GeneratedDirtRoad",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(CreateDirtPixels(size));
            texture.Apply(false, true);
            return texture;
        }

        private Color[] CreateDirtPixels(int size)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float noise = TileablePerlin(u, v, 16f) * 0.26f +
                                  TileablePerlin(u, v, 4f) * 0.28f;
                    float tracks = Mathf.Abs(x - size * 0.25f) < 7f ||
                                   Mathf.Abs(x - size * 0.75f) < 7f ? -0.09f : 0f;
                    float shade = Mathf.Clamp01(0.45f + noise + tracks);
                    Color dirt = new Color(shade, shade * 0.88f, shade * 0.69f);
                    float localX = ((x + 0.5f) / size - 0.5f) * roadWidth;
                    float leftDividerDistance = Mathf.Abs(localX + laneWidth * 0.5f);
                    float rightDividerDistance = Mathf.Abs(localX - laneWidth * 0.5f);
                    if (buildLaneDividers &&
                        Mathf.Min(leftDividerDistance, rightDividerDistance) <= laneMarkingWidth * 0.5f)
                    {
                        dirt = Color.Lerp(dirt, laneMarkingColor, laneMarkingStrength);
                    }

                    pixels[y * size + x] = dirt;
                }
            }

            return pixels;
        }

        /// <summary>
        /// Perlin noise made periodic over the whole texture. Raw Perlin does not line up across its
        /// own edges, and the road repeats this texture three times per tile, which drew a hard
        /// horizontal band across the entire carriageway every few metres. Blending the four shifted
        /// copies makes the pattern wrap, so the repeats are invisible.
        /// </summary>
        private static float TileablePerlin(float u, float v, float frequency)
        {
            float a = Mathf.PerlinNoise(u * frequency, v * frequency);
            float b = Mathf.PerlinNoise((u - 1f) * frequency, v * frequency);
            float c = Mathf.PerlinNoise(u * frequency, (v - 1f) * frequency);
            float d = Mathf.PerlinNoise((u - 1f) * frequency, (v - 1f) * frequency);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
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

                if (buildLaneDividers)
                {
                    Gizmos.color = new Color(0.92f, 0.82f, 0.50f, 1f);
                    for (var divider = -1; divider <= 1; divider += 2)
                    {
                        Gizmos.DrawCube(new Vector3(divider * laneWidth * 0.5f, 0.02f, 0f),
                            new Vector3(laneMarkingWidth, 0.025f, tileLength - 0.2f));
                    }
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
