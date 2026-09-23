using UnityEngine;

namespace Reins
{
    /// <summary>Builds a bounded world-space forest road pool that recycles toward negative Z.</summary>
    [ExecuteAlways]
    public sealed class ForestRoad : MonoBehaviour
    {
        [SerializeField, Min(6)] private int tileCount = 8;
        [SerializeField, Min(8f)] private float tileLength = 18f;
        [SerializeField, Min(6f)] private float roadWidth = 9.6f;
        [SerializeField, Min(0.1f)] private float laneWidth = 2.8f;
        [SerializeField] private Transform vehicleRoot;

        private const int TreesPerSide = 3;
        private Tile[] _tiles;
        private Material _roadMaterial;
        private Material _vergeMaterial;
        private Material _dividerMaterial;
        private Material _trunkMaterial;
        private Material _leafMaterial;
        [SerializeField, Min(0.1f)] private float stoneLaneWidth = 1.15f;
        [SerializeField, Min(0.1f)] private float stoneDepth = 0.65f;

        private bool _initialized;
        private int _nextObstacleGroup;
        private Material _stoneMaterial;

        private sealed class Tile
        {
            public Transform root;
            public readonly Transform[] stones = new Transform[3];
            public readonly bool[] consumed = new bool[3];
            public int blockedMask;
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

            var vehicleZ = vehicleRoot.position.z;
            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i].root;
                if (tile.position.z - vehicleZ <= tileLength * 0.5f)
                {
                    continue;
                }

                var frontZ = float.PositiveInfinity;
                for (var j = 0; j < _tiles.Length; j++)
                {
                    if (_tiles[j].root.position.z < frontZ)
                    {
                        frontZ = _tiles[j].root.position.z;
                    }
                }

                var position = tile.position;
                position.z = frontZ - tileLength;
                tile.position = position;
                var recycled = _tiles[i];
                ConfigureObstacleGroup(recycled, _nextObstacleGroup++);
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

            DestroyMaterial(_roadMaterial);
            DestroyMaterial(_vergeMaterial);
            DestroyMaterial(_dividerMaterial);
            DestroyMaterial(_trunkMaterial);
            DestroyMaterial(_leafMaterial);
            DestroyMaterial(_stoneMaterial);
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

            _roadMaterial = CreateMaterial(new Color(0.20f, 0.22f, 0.20f));
            _vergeMaterial = CreateMaterial(new Color(0.16f, 0.34f, 0.12f));
            _dividerMaterial = CreateMaterial(new Color(0.86f, 0.79f, 0.53f));
            _trunkMaterial = CreateMaterial(new Color(0.28f, 0.16f, 0.08f));
            _leafMaterial = CreateMaterial(new Color(0.10f, 0.30f, 0.12f));
            _stoneMaterial = CreateMaterial(new Color(0.34f, 0.32f, 0.29f));
            _tiles = new Tile[Mathf.Clamp(tileCount, 6, 10)];

            for (var i = 0; i < _tiles.Length; i++)
            {
                var tileRoot = new GameObject("RoadTile_" + i).transform;
                tileRoot.SetParent(transform, false);
                tileRoot.localPosition = new Vector3(0f, 0f, -i * tileLength);
                _tiles[i] = new Tile { root = tileRoot };
                BuildTile(tileRoot, i);
                BuildStones(_tiles[i], i);
            }

            _nextObstacleGroup = _tiles.Length;
            _initialized = true;
        }

        private void BuildTile(Transform tile, int index)
        {
            CreatePart(tile, "Road", PrimitiveType.Cube,
                new Vector3(0f, -0.06f, 0f), new Vector3(roadWidth, 0.12f, tileLength), _roadMaterial);
            CreatePart(tile, "GreenVerge_Left", PrimitiveType.Cube,
                new Vector3(-roadWidth * 0.5f - 0.65f, -0.03f, 0f), new Vector3(1.3f, 0.08f, tileLength), _vergeMaterial);
            CreatePart(tile, "GreenVerge_Right", PrimitiveType.Cube,
                new Vector3(roadWidth * 0.5f + 0.65f, -0.03f, 0f), new Vector3(1.3f, 0.08f, tileLength), _vergeMaterial);

            for (var divider = -1; divider <= 1; divider += 2)
            {
                CreatePart(tile, "LaneDivider_" + divider, PrimitiveType.Cube,
                    new Vector3(divider * laneWidth * 0.5f, 0.012f, 0f), new Vector3(0.075f, 0.025f, tileLength - 0.2f), _dividerMaterial);
            }

            for (var tree = 0; tree < TreesPerSide; tree++)
            {
                var z = -tileLength * 0.5f + (tree + 0.5f) * tileLength / TreesPerSide;
                var variation = ((index * 17 + tree * 11) % 5) * 0.25f;
                for (var side = -1; side <= 1; side += 2)
                {
                    var x = side * (roadWidth * 0.5f + 2f + variation);
                    var height = 2.8f + ((index * 7 + tree * 3 + side + 12) % 4) * 0.35f;
                    var treeRoot = new GameObject("Tree_" + side + "_" + tree).transform;
                    treeRoot.SetParent(tile, false);
                    treeRoot.localPosition = new Vector3(x, 0f, z);
                    CreatePart(treeRoot, "Trunk", PrimitiveType.Cylinder,
                        new Vector3(0f, height * 0.25f, 0f), new Vector3(0.22f, height * 0.25f, 0.22f), _trunkMaterial);
                    CreatePart(treeRoot, "Canopy", PrimitiveType.Sphere,
                        new Vector3(0f, height * 0.78f, 0f), new Vector3(1.15f + variation * 0.2f, height * 0.38f, 1.15f + variation * 0.2f), _leafMaterial);
                    CreatePart(treeRoot, "CanopyTop", PrimitiveType.Sphere,
                        new Vector3(0f, height * 0.92f, 0f), new Vector3(0.8f, 0.8f, 0.8f), _leafMaterial);
                }
            }
        }

        private void BuildStones(Tile tile, int index)
        {
            for (var lane = -1; lane <= 1; lane++)
            {
                var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.name = "Stone_Lane_" + lane;
                stone.transform.SetParent(tile.root, false);
                stone.transform.localPosition = new Vector3(lane * laneWidth, 0.34f, -tileLength * 0.25f);
                stone.transform.localScale = new Vector3(0.82f, 0.55f, 0.92f);
                var collider = stone.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                var renderer = stone.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = _stoneMaterial;
                tile.stones[lane + 1] = stone.transform;
            }

            ConfigureObstacleGroup(tile, index);
        }

        private static void ConfigureObstacleGroup(Tile tile, int groupIndex)
        {
            tile.blockedMask = groupIndex < 2 ? 0 : ObstacleSchedule.BlockedLaneMask(groupIndex);
            for (var laneIndex = 0; laneIndex < 3; laneIndex++)
            {
                tile.consumed[laneIndex] = false;
                if (tile.stones[laneIndex] != null)
                {
                    tile.stones[laneIndex].gameObject.SetActive((tile.blockedMask & (1 << laneIndex)) != 0);
                }
            }
        }

        public bool TryStopOnRock(Vector3 previousPosition, Vector3 currentPosition)
        {
            if (_tiles == null) return false;
            for (var i = 0; i < _tiles.Length; i++)
            {
                var tile = _tiles[i];
                for (var laneIndex = 0; laneIndex < 3; laneIndex++)
                {
                    if ((tile.blockedMask & (1 << laneIndex)) == 0) continue;
                    var stone = tile.stones[laneIndex];
                    if (stone == null || !stone.gameObject.activeSelf) continue;
                    if (ObstacleCollisionModel.TrySweep(previousPosition.z, currentPosition.z,
                        previousPosition.x, currentPosition.x, stone.position.z, stone.position.x,
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
            var width = Mathf.Max(roadWidth, laneWidth * 3f);
            for (var i = 0; i < count; i++)
            {
                var center = transform.position + new Vector3(0f, -0.06f, -i * tileLength - tileLength * 0.5f);
                Gizmos.color = new Color(0.24f, 0.25f, 0.22f, 0.85f);
                Gizmos.DrawCube(center, new Vector3(width, 0.08f, tileLength));

                Gizmos.color = new Color(0.92f, 0.82f, 0.50f, 1f);
                for (var divider = -1; divider <= 1; divider += 2)
                {
                    var x = transform.position.x + divider * laneWidth * 0.5f;
                    Gizmos.DrawCube(new Vector3(x, transform.position.y + 0.02f, center.z),
                        new Vector3(0.09f, 0.025f, tileLength - 0.2f));
                }

                Gizmos.color = new Color(0.16f, 0.38f, 0.13f, 0.8f);
                Gizmos.DrawCube(center + Vector3.left * (width * 0.5f + 0.65f), new Vector3(1.3f, 0.06f, tileLength));
                Gizmos.DrawCube(center + Vector3.right * (width * 0.5f + 0.65f), new Vector3(1.3f, 0.06f, tileLength));

                for (var tree = 0; tree < TreesPerSide; tree++)
                {
                    var z = transform.position.z - i * tileLength - tileLength * 0.5f + (tree + 0.5f) * tileLength / TreesPerSide;
                    for (var side = -1; side <= 1; side += 2)
                    {
                        var x = transform.position.x + side * (width * 0.5f + 2.5f);
                        Gizmos.color = new Color(0.32f, 0.19f, 0.09f, 1f);
                        Gizmos.DrawCube(new Vector3(x, transform.position.y + 0.55f, z), new Vector3(0.28f, 1.1f, 0.28f));
                        Gizmos.color = new Color(0.10f, 0.34f, 0.13f, 1f);
                        Gizmos.DrawSphere(new Vector3(x, transform.position.y + 2.15f, z), 0.9f);
                    }
                }
            }
        }
    }
}
