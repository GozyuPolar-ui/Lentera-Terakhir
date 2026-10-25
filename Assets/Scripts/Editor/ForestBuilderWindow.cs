#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using JRPG.Core;

namespace JRPG.EditorTools
{
    public class ForestBuilderWindow : EditorWindow
    {
        private const string RootName = "Forest_Generated";
        private const string HorizonRootName = "Forest_Mountain_Backdrop";
        private const string LegacyHorizonRootName = "Forest_Horizon_Ring";
        private const string NatureFolder = "Assets/Art/Models/nature/FBX (Unity)";
        private const string TerrainFolder = "Assets/_Project/Terrain";
        private const float TerrainMargin = 20f;
        private const float TerrainBaseY = -0.7f;
        private const float TerrainHeight = 1.4f;
        private static readonly Color HorizonFogColor = new Color(0.38f, 0.45f, 0.4f);
        private float forestLength = 140f;
        private float forestWidth = 110f;
        private float pathWidth = 6f;
        private float pathFeather = 2.5f;
        private float hillHeight = 0.5f;
        private float hillScale = 32f;
        private float detailHeight = 0.08f;
        private float clearingRadius = 5f;
        private int treeCount = 500;
        private int undergrowthCount = 700;
        private float grassDensity = 0.75f;
        private int maxGrassCount = 10000;
        private float detailDistance = 60f;
        private float dirtAmount = 0.08f;
        private float leafAmount = 0.24f;
        private float darken = 0.72f;
        private float tileSize = 5f;
        private float pathWinding = 7f;
        private int seed = 17;
        private readonly Dictionary<string, Material> vegetationTintCache =
            new Dictionary<string, Material>();

        private sealed class GrassMeshBatch
        {
            public Material material;
            public readonly List<CombineInstance> instances = new List<CombineInstance>();
            public int vertexCount;
        }

        [MenuItem("Tools/Lentera/Forest Generator")]
        private static void Open()
        {
            GetWindow<ForestBuilderWindow>("Forest Generator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Starting Scene Forest", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Builds a complete forest stage with rolling, textured Terrain, a painted dirt trail, trees, and clustered grass, flowers, rocks, and bushes. " +
                "Works in an empty scene and does not require a Player or village. Regenerating replaces the forest and distant mountain backdrop.",
                MessageType.Info);

            forestLength = Mathf.Max(20f, EditorGUILayout.FloatField("Forest length", forestLength));
            forestWidth = Mathf.Max(20f, EditorGUILayout.FloatField("Forest width", forestWidth));
            pathWidth = Mathf.Clamp(EditorGUILayout.FloatField("Clear path width", pathWidth), 3f, forestWidth - 4f);
            pathWinding = Mathf.Clamp(EditorGUILayout.FloatField("Path winding", pathWinding), 0f, forestWidth * 0.2f);
            pathFeather = Mathf.Clamp(EditorGUILayout.FloatField("Path feather", pathFeather), 0.5f, 8f);
            hillHeight = Mathf.Clamp(EditorGUILayout.FloatField("Hill height", hillHeight), 0.1f, 0.5f);
            hillScale = Mathf.Clamp(EditorGUILayout.FloatField("Hill scale", hillScale), 25f, 40f);
            detailHeight = Mathf.Clamp(EditorGUILayout.FloatField("Detail height", detailHeight), 0f, 0.08f);
            clearingRadius = Mathf.Clamp(EditorGUILayout.FloatField("Clearing radius", clearingRadius), 3f, 12f);
            treeCount = Mathf.Clamp(EditorGUILayout.IntField("Tree count", treeCount), 0, 1000);
            undergrowthCount = Mathf.Clamp(EditorGUILayout.IntField("Undergrowth count", undergrowthCount), 0, 1500);
            grassDensity = Mathf.Clamp(EditorGUILayout.FloatField("Grass density / m2", grassDensity), 0f, 1.5f);
            maxGrassCount = Mathf.Clamp(EditorGUILayout.IntField("Max grass count", maxGrassCount), 0, 30000);
            detailDistance = Mathf.Clamp(EditorGUILayout.FloatField("Detail distance", detailDistance), 20f, 120f);
            dirtAmount = Mathf.Clamp(EditorGUILayout.FloatField("Dirt amount", dirtAmount), 0f, 0.1f);
            leafAmount = Mathf.Clamp01(EditorGUILayout.FloatField("Leaf amount", leafAmount));
            darken = Mathf.Clamp(EditorGUILayout.FloatField("Terrain darken", darken), 0.35f, 1f);
            tileSize = Mathf.Clamp(EditorGUILayout.FloatField("Terrain tile size", tileSize), 4f, 6f);
            seed = EditorGUILayout.IntField("Seed", seed);

            if (GUILayout.Button("Re-ground Objects To Terrain"))
            {
                RegroundObjectsToTerrain();
            }

            if (GUILayout.Button("ONE CLICK: Generate Complete Dense Forest"))
            {
                BuildForest();
            }

        }

        [MenuItem("Tools/Lentera/Re-ground Objects To Terrain")]
        private static void RegroundObjectsToTerrain()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Terrain forestTerrain = null;
            foreach (Terrain candidate in terrains)
            {
                if (candidate.name == "Forest_Terrain")
                {
                    forestTerrain = candidate;
                    break;
                }
            }

            if (forestTerrain == null)
            {
                Debug.LogWarning("Forest Generator: no Forest_Terrain exists in the open scene.");
                return;
            }

            TerrainSnap.SnapKnownObjects(forestTerrain);
        }

        private void BuildForest()
        {
            GameObject[] trees = LoadTreeModels();
            if (trees.Length == 0)
            {
                Debug.LogError("Forest Generator: no CommonTree, Pine, TwistedTree, or DeadTree models were found.");
                return;
            }

            GameObject[] undergrowth = LoadUndergrowthModels();
            vegetationTintCache.Clear();
            DeleteGeneratedAssets("t:Material", "Forest_VegTint_");
            Transform villageGround = GameObject.Find("Village_Generated/Ground")?.transform;
            GameObject player = GameObject.Find("Player");
            Vector3 start;
            Quaternion rotation;
            if (villageGround != null)
            {
                Renderer groundRenderer = villageGround.GetComponentInChildren<Renderer>();
                if (groundRenderer == null)
                {
                    Debug.LogError("Forest Generator: Village_Generated/Ground has no renderer to determine the forest entrance.");
                    return;
                }

                Bounds bounds = groundRenderer.bounds;
                start = new Vector3(bounds.max.x - 12f, 0f, bounds.center.z);
                rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            }
            else if (player != null)
            {
                start = player.transform.position + player.transform.forward * 5f;
                rotation = Quaternion.LookRotation(player.transform.forward, Vector3.up);
            }
            else
            {
                start = Vector3.zero;
                rotation = Quaternion.identity;
            }

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject existingHorizon = GameObject.Find(HorizonRootName);
            if (existingHorizon != null)
            {
                Undo.DestroyObjectImmediate(existingHorizon);
            }

            GameObject legacyHorizon = GameObject.Find(LegacyHorizonRootName);
            if (legacyHorizon != null)
            {
                Undo.DestroyObjectImmediate(legacyHorizon);
            }

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Generate Starting Scene Forest");
            root.transform.SetPositionAndRotation(start, rotation);

            Terrain terrain = CreateForestTerrain(root.transform);
            if (terrain == null)
            {
                Undo.DestroyObjectImmediate(root);
                return;
            }

            TerrainSnap terrainSnap = Undo.AddComponent<TerrainSnap>(root);
            terrainSnap.terrain = terrain;
            terrainSnap.snapOnGenerate = true;

            SetupForestLighting(root.transform, forestLength, forestWidth);
            CreateClouds(root.transform);
            CreateMountainBackdrop(root.transform);
            ConfigureForestMusic(root.transform);
            SetupPlayer(root.transform, forestLength, terrain);
            GameObject generatedPlayer = GameObject.Find("Player");
            TerrainFloorGuard floorGuard = generatedPlayer != null
                ? generatedPlayer.GetComponent<TerrainFloorGuard>()
                : null;
            if (floorGuard == null && generatedPlayer != null)
            {
                floorGuard = Undo.AddComponent<TerrainFloorGuard>(generatedPlayer);
            }
            if (floorGuard != null)
            {
                floorGuard.terrain = terrain;
            }

            Transform treeRoot = new GameObject("Trees").transform;
            treeRoot.SetParent(root.transform, false);
            System.Random random = new System.Random(seed);
            List<Vector2> treePositions = new List<Vector2>(treeCount);
            int placed = 0;
            int attempts = treeCount * 24;
            while (placed < treeCount && attempts-- > 0)
            {
                float forward = GetClusteredForward(random, 16f, 8f, forestLength - 8f);
                float side = GetForestSide(random, forestWidth, pathWidth, 4f);
                if (Mathf.Abs(side) > forestWidth * 0.5f - 3f)
                {
                    continue;
                }

                float localX = GetTrailCenterX(forward) + side;
                if (IsInsideGameplayClearing(localX, forward, clearingRadius + 5f))
                {
                    continue;
                }

                float treeNoise = Mathf.PerlinNoise(
                    (localX + seed * 43f) * 0.035f, (forward - seed * 37f) * 0.035f);
                float pathDistance = Mathf.Abs(localX - GetTrailCenterX(forward));
                float density = Mathf.Lerp(0.58f, 1f, treeNoise)
                    * Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(pathWidth * 0.5f, pathWidth * 0.5f + 18f, pathDistance));
                if (random.NextDouble() > density)
                {
                    continue;
                }

                GameObject model = trees[random.Next(trees.Length)];
                GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (tree == null)
                {
                    Debug.LogError("Forest Generator: failed to instantiate tree model " + model.name + ".");
                    continue;
                }
                Undo.RegisterCreatedObjectUndo(tree, "Place Forest Tree");
                tree.transform.SetParent(treeRoot, false);
                float terrainHeight = SampleTerrainHeight(terrain, root.transform, localX, forward);
                tree.transform.localPosition = new Vector3(localX, terrainHeight, forward);
                tree.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                float scale = 0.85f + (float)random.NextDouble() * 0.7f;
                tree.transform.localScale = Vector3.one * scale;
                ApplyVegetationTint(tree, random);
                MarkStaticRecursively(tree.transform);
                AddTreeCollider(tree);
                treePositions.Add(new Vector2(localX, forward));
                placed++;
            }

            PaintTerrainLayers(terrain, treePositions);

            Transform undergrowthRoot = new GameObject("Undergrowth").transform;
            undergrowthRoot.SetParent(root.transform, false);
            List<GameObject> rocks = new List<GameObject>();
            List<GameObject> foliage = new List<GameObject>();
            foreach (GameObject model in undergrowth)
            {
                if (model.name.StartsWith("Pebble_") || model.name.StartsWith("Rock"))
                {
                    rocks.Add(model);
                }
                else
                {
                    foliage.Add(model);
                }
            }

            int undergrowthPlaced = 0;
            attempts = undergrowthCount * 12;
            while (undergrowthPlaced < undergrowthCount && undergrowth.Length > 0 && attempts-- > 0)
            {
                float forward = GetClusteredForward(random, 8f, 3f, forestLength - 3f);
                float side = GetForestSide(random, forestWidth, pathWidth, 1.5f);
                if (Mathf.Abs(side) > forestWidth * 0.5f - 1f)
                {
                    continue;
                }

                float localX = GetTrailCenterX(forward) + side;
                float pathDistance = Mathf.Abs(localX - GetTrailCenterX(forward));
                if (IsInsideGameplayClearing(localX, forward, clearingRadius + 1f)
                    || pathDistance < pathWidth * 0.5f + 0.75f)
                {
                    continue;
                }

                float clusterNoise = Mathf.PerlinNoise(
                    (localX + seed * 31f) * 0.08f, (forward - seed * 19f) * 0.08f);
                if (random.NextDouble() > Mathf.Lerp(0.4f, 1f, clusterNoise))
                {
                    continue;
                }

                List<GameObject> category = foliage.Count > 0
                    && (rocks.Count == 0 || random.NextDouble() >= 0.18)
                    ? foliage
                    : rocks;
                if (category.Count == 0)
                {
                    continue;
                }

                GameObject model = category[random.Next(category.Count)];
                GameObject plant = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (plant == null)
                {
                    Debug.LogError("Forest Generator: failed to instantiate undergrowth model " + model.name + ".");
                    continue;
                }

                Undo.RegisterCreatedObjectUndo(plant, "Place Forest Undergrowth");
                plant.transform.SetParent(undergrowthRoot, false);
                float terrainHeight = SampleTerrainHeight(terrain, root.transform, localX, forward);
                plant.transform.localPosition = new Vector3(localX, terrainHeight, forward);
                plant.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                float scale = 0.65f + (float)random.NextDouble() * 0.8f;
                if (model.name.StartsWith("Grass_") || model.name.StartsWith("Flower_")
                    || model.name.StartsWith("Clover_") || model.name.StartsWith("Mushroom_"))
                {
                    scale *= model.name.StartsWith("Grass_") ? 0.45f : 0.65f;
                }
                else if (model.name.StartsWith("Pebble_") || model.name.StartsWith("Rock"))
                {
                    scale *= 0.55f;
                }
                plant.transform.localScale = Vector3.one * scale;
                if (!model.name.StartsWith("Rock", System.StringComparison.Ordinal)
                    && !model.name.StartsWith("Pebble_", System.StringComparison.Ordinal))
                {
                    ApplyVegetationTint(plant, random);
                }
                MarkStaticRecursively(plant.transform);
                if (model.name.StartsWith("Bush_"))
                {
                    AddBushCollider(plant);
                }
                undergrowthPlaced++;
            }

            int grassInstances = CreateBatchedGrass(root.transform, terrain);
            Selection.activeGameObject = root;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Forest Generator: generated rolling terrain, painted dirt trail, " + placed
                      + " trees, " + undergrowthPlaced + " nature props, " + grassInstances
                      + " batched grass instances, clouds, sun, and forest music setup.");
        }

        private static GameObject[] LoadTreeModels()
        {
            List<GameObject> trees = new List<GameObject>();
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { NatureFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!(name.StartsWith("CommonTree") || name.StartsWith("Pine_")
                    || name.StartsWith("TwistedTree") || name.StartsWith("DeadTree")))
                {
                    continue;
                }

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                {
                    trees.Add(model);
                }
            }

            return trees.ToArray();
        }

        private static GameObject[] LoadUndergrowthModels()
        {
            List<GameObject> plants = new List<GameObject>();
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { NatureFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!(name.StartsWith("Bush_") || name.StartsWith("Fern_")
                    || name.StartsWith("Plant_") || name.StartsWith("Grass_")
                    || name.StartsWith("Flower_") || name.StartsWith("Clover_")
                    || name.StartsWith("Mushroom_") || name.StartsWith("Pebble_")
                    || name.StartsWith("Rock_") || name.StartsWith("RockPath_")
                    || name.StartsWith("Log_") || name.StartsWith("Stump_")))
                {
                    continue;
                }

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                {
                    plants.Add(model);
                }
            }

            return plants.ToArray();
        }

        private float GetForestSide(System.Random random, float width, float clearPathWidth, float clearance)
        {
            float halfWidth = width * 0.5f;
            float sideWidth = halfWidth - clearPathWidth * 0.5f - clearance;
            float spread = Mathf.Pow((float)random.NextDouble(), 1.45f);
            float distance = clearPathWidth * 0.5f + clearance + spread * Mathf.Max(0f, sideWidth);
            return random.Next(0, 2) == 0 ? -distance : distance;
        }

        private float GetClusteredForward(System.Random random, float spacing, float minimum, float maximum)
        {
            int clusterCount = Mathf.Max(1, Mathf.CeilToInt(forestLength / spacing));
            float clusterWidth = forestLength / clusterCount;
            float clusterCenter = (random.Next(0, clusterCount) + 0.5f) * clusterWidth;
            float jitter = ((float)random.NextDouble() + (float)random.NextDouble() - 1f)
                * clusterWidth * 0.5f;
            return Mathf.Clamp(clusterCenter + jitter, minimum, maximum);
        }

        private float GetTrailCenterX(float forward)
        {
            float t = Mathf.Clamp01(forward / forestLength);
            float envelope = Mathf.Sin(Mathf.PI * t);
            return pathWinding * envelope
                   * (0.72f * Mathf.Sin(t * Mathf.PI * 2f * 1.4f)
                      + 0.28f * Mathf.Sin(t * Mathf.PI * 2f * 2.4f + 0.8f));
        }

        private Terrain CreateForestTerrain(Transform parent)
        {
            EnsureAssetFolder(TerrainFolder);

            TerrainData terrainData = new TerrainData
            {
                name = "Forest_TerrainData",
                heightmapResolution = 513,
                alphamapResolution = 512,
                baseMapResolution = 512,
                size = new Vector3(
                    forestWidth + TerrainMargin * 2f,
                    TerrainHeight,
                    forestLength + TerrainMargin * 2f)
            };
            float[,] heights = BuildTerrainHeights(terrainData.heightmapResolution);
            terrainData.SetHeights(0, 0, heights);

            TerrainLayer[] layers =
            {
                CreateTerrainLayer(
                    "Rumput", "leafy_grass_diff_2k.jpg", "leafy_grass_nor_gl_2k.png",
                    new Color32(104, 117, 67, 255)),
                CreateTerrainLayer(
                    "Tanah", "dirt_floor_diff_2k.jpg", "dirt_floor_nor_gl_2k.png",
                    new Color32(111, 91, 66, 255)),
                CreateTerrainLayer(
                    "Serasah", "brown_mud_leaves_01_diff_2k.jpg",
                    "brown_mud_leaves_01_nor_gl_2k.png", new Color32(104, 82, 55, 255))
            };
            terrainData.terrainLayers = layers;

            const string terrainDataPath = TerrainFolder + "/Forest_TerrainData.asset";
            AssetDatabase.DeleteAsset(terrainDataPath);
            AssetDatabase.CreateAsset(terrainData, terrainDataPath);
            EditorUtility.SetDirty(terrainData);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "Forest_Terrain";
            Undo.RegisterCreatedObjectUndo(terrainObject, "Create Forest Terrain");
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.localPosition = new Vector3(
                -forestWidth * 0.5f - TerrainMargin, TerrainBaseY, -TerrainMargin);

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            TerrainCollider terrainCollider = terrainObject.GetComponent<TerrainCollider>();
            if (terrainCollider == null)
            {
                terrainCollider = Undo.AddComponent<TerrainCollider>(terrainObject);
            }
            terrainCollider.terrainData = terrainData;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5f;
            terrain.detailObjectDistance = detailDistance;
            terrain.treeDistance = detailDistance;
            terrain.materialTemplate = CreateTerrainMaterial();
            if (terrain.materialTemplate == null)
            {
                Undo.DestroyObjectImmediate(terrainObject);
                return null;
            }
            terrain.Flush();
            CreateForestFloorBoundsMesh(parent);
            AssetDatabase.SaveAssets();
            return terrain;
        }

        private float[,] BuildTerrainHeights(int resolution)
        {
            float[,] heights = new float[resolution, resolution];
            List<Vector2> gameplayClearings = GetGameplayClearingCenters();
            float terrainWidth = forestWidth + TerrainMargin * 2f;
            float terrainLength = forestLength + TerrainMargin * 2f;
            float left = -forestWidth * 0.5f - TerrainMargin;
            float startForward = -TerrainMargin;

            for (int z = 0; z < resolution; z++)
            {
                float forward = startForward + terrainLength * z / (resolution - 1);
                for (int x = 0; x < resolution; x++)
                {
                    float localX = left + terrainWidth * x / (resolution - 1);
                    float naturalHeight = GetNaturalTerrainHeight(localX, forward);
                    float trailDistance = Mathf.Abs(localX - GetTrailCenterX(forward));
                    float trailBlend = 1f - Mathf.SmoothStep(
                        pathWidth * 0.5f, pathWidth * 0.5f + pathFeather, trailDistance);
                    float trailHeight = GetNaturalTerrainHeight(GetTrailCenterX(forward), forward) - 0.04f;
                    float clearingBlend = 0f;
                    foreach (Vector2 clearing in gameplayClearings)
                    {
                        float clearingDistance = Vector2.Distance(
                            new Vector2(localX, forward), clearing);
                        float clearingWeight = 1f - Mathf.SmoothStep(
                            clearingRadius * 0.72f, clearingRadius + 2f, clearingDistance);
                        clearingBlend = Mathf.Max(clearingBlend, clearingWeight);
                    }
                    naturalHeight = Mathf.Lerp(naturalHeight, 0f, clearingBlend);
                    trailHeight = Mathf.Lerp(trailHeight, 0f, clearingBlend);
                    float edgeX = Mathf.Min(localX - left, left + terrainWidth - localX);
                    float edgeZ = Mathf.Min(forward - startForward, startForward + terrainLength - forward);
                    float edgeBlend = Mathf.Min(
                        Mathf.SmoothStep(0f, TerrainMargin, edgeX),
                        Mathf.SmoothStep(0f, TerrainMargin, edgeZ));
                    float height = Mathf.Lerp(naturalHeight, trailHeight, trailBlend) * edgeBlend;
                    heights[z, x] = Mathf.Clamp01((height - TerrainBaseY) / TerrainHeight);
                }
            }

            return heights;
        }

        private float GetNaturalTerrainHeight(float x, float forward)
        {
            float broad = (Mathf.PerlinNoise(
                (x + seed * 17.3f) / hillScale, (forward - seed * 9.1f) / hillScale) - 0.5f)
                * 2f * hillHeight;
            float detailScale = Mathf.Max(5f, hillScale * 0.22f);
            float detail = (Mathf.PerlinNoise(
                (x - seed * 5.7f) / detailScale, (forward + seed * 13.9f) / detailScale) - 0.5f)
                * 2f * detailHeight;
            return broad + detail;
        }

        private List<Vector2> GetGameplayClearingCenters()
        {
            float signForward = forestLength * 0.17f;
            float campsiteForward = forestLength * 0.46f;
            float noteForward = forestLength * 0.52f;
            float monsterForward = forestLength * 0.72f;
            return new List<Vector2>
            {
                new Vector2(GetTrailCenterX(5f), 5f),
                new Vector2(GetTrailCenterX(signForward) - 2f, signForward),
                new Vector2(GetTrailCenterX(campsiteForward), campsiteForward),
                new Vector2(GetTrailCenterX(noteForward), noteForward),
                new Vector2(GetTrailCenterX(monsterForward), monsterForward),
                new Vector2(GetTrailCenterX(forestLength - 7f), forestLength - 7f)
            };
        }

        private bool IsInsideGameplayClearing(float localX, float forward, float radius)
        {
            Vector2 position = new Vector2(localX, forward);
            foreach (Vector2 center in GetGameplayClearingCenters())
            {
                if (Vector2.Distance(position, center) < radius)
                {
                    return true;
                }
            }

            return false;
        }

        private TerrainLayer CreateTerrainLayer(
            string layerName, string diffuseFileName, string normalFileName, Color32 fallbackColor)
        {
            string layerPath = TerrainFolder + "/Forest_" + layerName + ".terrainlayer";
            AssetDatabase.DeleteAsset(layerPath);

            Texture2D diffuse = LoadGroundTexture(diffuseFileName, false);
            if (diffuse == null)
            {
                Debug.LogWarning(
                    "Forest Generator: diffuse texture '" + diffuseFileName
                    + "' was not found. A solid fallback will be used for the " + layerName + " layer.");
                diffuse = CreateFallbackTexture(layerName, fallbackColor);
            }

            Texture2D normal = LoadGroundTexture(normalFileName, true);
            if (normal == null)
            {
                Debug.LogWarning(
                    "Forest Generator: normal map '" + normalFileName
                    + "' was not found. The " + layerName + " layer will use flat shading.");
            }

            float tint = Mathf.Clamp01(darken);
            TerrainLayer layer = new TerrainLayer
            {
                name = "Forest_" + layerName,
                diffuseTexture = diffuse,
                normalMapTexture = normal,
                tileSize = new Vector2(tileSize, tileSize),
                tileOffset = Vector2.zero,
                normalScale = 1f,
                metallic = 0f,
                smoothness = 0f,
                diffuseRemapMin = Vector4.zero,
                diffuseRemapMax = new Vector4(tint, tint, tint, 1f)
            };
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        private static Texture2D LoadGroundTexture(string fileName, bool normalMap)
        {
            string[] guids = AssetDatabase.FindAssets(
                System.IO.Path.GetFileNameWithoutExtension(fileName) + " t:Texture2D",
                new[] { "Assets/Art/Textures" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(
                    System.IO.Path.GetFileName(path), fileName,
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = normalMap
                        ? TextureImporterType.NormalMap
                        : TextureImporterType.Default;
                    importer.sRGBTexture = !normalMap;
                    importer.mipmapEnabled = true;
                    importer.anisoLevel = 8;
                    importer.SaveAndReimport();
                }

                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            return null;
        }

        private static Texture2D CreateFallbackTexture(string layerName, Color32 color)
        {
            string path = TerrainFolder + "/Forest_" + layerName + "_Fallback.png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, false);
            Color32[] pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private Material CreateTerrainMaterial()
        {
            const string materialPath = TerrainFolder + "/Forest_Terrain_Lit.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null)
            {
                Debug.LogError(
                    "Forest Generator: Universal Render Pipeline/Terrain/Lit was not found. "
                    + "The Terrain cannot use its required URP Terrain Lit material.");
                return null;
            }

            AssetDatabase.DeleteAsset(materialPath);
            Material material = new Material(shader) { name = "Forest_Terrain_Lit" };
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        private void PaintTerrainLayers(Terrain terrain, List<Vector2> treePositions)
        {
            TerrainData data = terrain.terrainData;
            int resolution = data.alphamapResolution;
            int heightResolution = data.heightmapResolution;
            float[,,] alphamaps = new float[resolution, resolution, 3];
            float[,] leafWeights = new float[resolution, resolution];
            float terrainWidth = data.size.x;
            float terrainLength = data.size.z;
            float left = -forestWidth * 0.5f - TerrainMargin;
            float startForward = -TerrainMargin;

            foreach (Vector2 tree in treePositions)
            {
                float centerX = (tree.x - left) / terrainWidth * (resolution - 1);
                float centerZ = (tree.y - startForward) / terrainLength * (resolution - 1);
                float radius = 3.5f + Mathf.PerlinNoise(
                    (tree.x + seed * 23f) * 0.19f, (tree.y - seed * 11f) * 0.19f) * 3f;
                int radiusX = Mathf.CeilToInt(radius / terrainWidth * (resolution - 1));
                int radiusZ = Mathf.CeilToInt(radius / terrainLength * (resolution - 1));
                for (int z = Mathf.Max(0, Mathf.FloorToInt(centerZ) - radiusZ);
                    z <= Mathf.Min(resolution - 1, Mathf.CeilToInt(centerZ) + radiusZ); z++)
                {
                    float forward = startForward + terrainLength * z / (resolution - 1);
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(centerX) - radiusX);
                        x <= Mathf.Min(resolution - 1, Mathf.CeilToInt(centerX) + radiusX); x++)
                    {
                        float localX = left + terrainWidth * x / (resolution - 1);
                        float distance = Vector2.Distance(new Vector2(localX, forward), tree);
                        float edgeNoise = Mathf.Lerp(0.78f, 1.22f, Mathf.PerlinNoise(
                            (localX + seed * 19f) * 0.31f, (forward - seed * 7f) * 0.31f));
                        float falloff = 1f - Mathf.SmoothStep(0.58f, 1f, distance / (radius * edgeNoise));
                        leafWeights[z, x] = Mathf.Max(leafWeights[z, x], falloff);
                    }
                }
            }

            float[,] heights = data.GetHeights(0, 0, heightResolution, heightResolution);
            float heightStepX = terrainWidth / (heightResolution - 1);
            float heightStepZ = terrainLength / (heightResolution - 1);
            for (int z = 0; z < resolution; z++)
            {
                float forward = startForward + terrainLength * z / (resolution - 1);
                int heightZ = Mathf.RoundToInt((float)z / (resolution - 1) * (heightResolution - 1));
                for (int x = 0; x < resolution; x++)
                {
                    float localX = left + terrainWidth * x / (resolution - 1);
                    int heightX = Mathf.RoundToInt((float)x / (resolution - 1) * (heightResolution - 1));
                    float dx = (heights[heightZ, Mathf.Min(heightResolution - 1, heightX + 1)]
                        - heights[heightZ, Mathf.Max(0, heightX - 1)]) * data.size.y / (2f * heightStepX);
                    float dz = (heights[Mathf.Min(heightResolution - 1, heightZ + 1), heightX]
                        - heights[Mathf.Max(0, heightZ - 1), heightX]) * data.size.y / (2f * heightStepZ);
                    float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
                    float steepSoil = Mathf.SmoothStep(5f, 8f, slope) * dirtAmount;
                    float patchNoise = Mathf.PerlinNoise(
                        (localX + seed * 29f) * 0.045f, (forward - seed * 17f) * 0.045f);
                    float dryPatch = Mathf.SmoothStep(0.62f, 0.88f, patchNoise) * dirtAmount;
                    float leafPatchNoise = Mathf.PerlinNoise(
                        (localX - seed * 13f) * 0.032f, (forward + seed * 31f) * 0.032f);
                    float litter = Mathf.Max(
                        leafWeights[z, x] * Mathf.Lerp(0.55f, 1f, leafAmount),
                        Mathf.SmoothStep(0.66f, 0.9f, leafPatchNoise) * leafAmount);
                    litter = Mathf.Clamp(litter, 0f, 0.42f);
                    float soil = Mathf.Clamp(Mathf.Max(dryPatch, steepSoil), 0f, 0.1f);
                    float grass = Mathf.Max(0.01f, 1f - soil - litter);

                    float trailDistance = Mathf.Abs(localX - GetTrailCenterX(forward));
                    float trailWeight = 1f - Mathf.SmoothStep(
                        pathWidth * 0.5f,
                        pathWidth * 0.5f + pathFeather,
                        trailDistance);
                    grass *= 1f - trailWeight;
                    litter *= 1f - trailWeight;
                    soil = Mathf.Lerp(soil, 1f, trailWeight);

                    float total = grass + soil + litter;
                    if (total <= Mathf.Epsilon)
                    {
                        alphamaps[z, x, 0] = 1f;
                        continue;
                    }

                    alphamaps[z, x, 0] = grass / total;
                    alphamaps[z, x, 1] = soil / total;
                    alphamaps[z, x, 2] = litter / total;
                }
            }

            data.SetAlphamaps(0, 0, alphamaps);
            EditorUtility.SetDirty(data);
        }

        private int CreateBatchedGrass(Transform parent, Terrain terrain)
        {
            DeleteGeneratedAssets("t:Mesh", "Forest_Grass_Batch_");
            DeleteGeneratedGrassTintMaterials();
            GameObject[] grassModels = LoadGrassModels();
            if (grassModels.Length == 0)
            {
                Debug.LogWarning(
                    "Forest Generator: no Grass_ models with meshes were found; no batched 3D grass was created.");
                return 0;
            }

            Dictionary<Material, List<GrassMeshBatch>> batches =
                new Dictionary<Material, List<GrassMeshBatch>>();
            Dictionary<Material, Material[]> tintCache = new Dictionary<Material, Material[]>();
            System.Random random = new System.Random(seed + 2917);
            int targetInstances = Mathf.Min(
                maxGrassCount, Mathf.CeilToInt(forestWidth * forestLength * grassDensity));
            int maxAttempts = targetInstances * 5;
            int instancesAdded = 0;
            float localLeft = -forestWidth * 0.5f;

            while (instancesAdded < targetInstances && maxAttempts-- > 0)
            {
                float localX = localLeft + (float)random.NextDouble() * forestWidth;
                float forward = (float)random.NextDouble() * forestLength;
                float distanceFromTrail = Mathf.Abs(localX - GetTrailCenterX(forward));
                if (distanceFromTrail < pathWidth * 0.5f + pathFeather
                    && random.NextDouble() > Mathf.Lerp(0.015f, 0.18f,
                        Mathf.InverseLerp(pathWidth * 0.5f, pathWidth * 0.5f + pathFeather,
                            distanceFromTrail)))
                {
                    continue;
                }

                float clusterNoise = Mathf.PerlinNoise(
                    (localX + seed * 41f) * 0.075f, (forward - seed * 23f) * 0.075f);
                if (clusterNoise < 0.31f
                    || random.NextDouble() > Mathf.Lerp(0.12f, 0.88f, clusterNoise))
                {
                    continue;
                }

                GameObject model = grassModels[random.Next(grassModels.Length)];
                MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
                float height = SampleTerrainHeight(terrain, parent, localX, forward);
                Matrix4x4 placement = Matrix4x4.TRS(
                    new Vector3(localX, height, forward),
                    Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f),
                    Vector3.one * Mathf.Lerp(0.35f, 0.75f, (float)random.NextDouble()));
                Matrix4x4 modelLocalToPlacement = placement * model.transform.worldToLocalMatrix;
                int tintIndex = random.Next(3);
                bool addedModel = false;

                foreach (MeshFilter filter in filters)
                {
                    Mesh mesh = filter.sharedMesh;
                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                    if (mesh == null || renderer == null)
                    {
                        continue;
                    }

                    Material[] materials = renderer.sharedMaterials;
                    for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                    {
                        Material material = materials.Length == 0
                            ? null
                            : materials[Mathf.Min(subMesh, materials.Length - 1)];
                        if (material == null)
                        {
                            continue;
                        }

                        if (!tintCache.TryGetValue(material, out Material[] tintMaterials))
                        {
                            tintMaterials = CreateGrassTintMaterials(material);
                            tintCache.Add(material, tintMaterials);
                        }

                        Material tintedMaterial = tintMaterials[tintIndex];
                        if (!batches.TryGetValue(tintedMaterial, out List<GrassMeshBatch> materialBatches))
                        {
                            materialBatches = new List<GrassMeshBatch>();
                            batches.Add(tintedMaterial, materialBatches);
                        }

                        GrassMeshBatch batch = materialBatches.Count == 0
                            ? null
                            : materialBatches[materialBatches.Count - 1];
                        if (batch == null || batch.vertexCount + mesh.vertexCount > 50000)
                        {
                            batch = new GrassMeshBatch { material = tintedMaterial };
                            materialBatches.Add(batch);
                        }

                        batch.instances.Add(new CombineInstance
                        {
                            mesh = mesh,
                            subMeshIndex = subMesh,
                            transform = modelLocalToPlacement * filter.transform.localToWorldMatrix
                        });
                        batch.vertexCount += mesh.vertexCount;
                        addedModel = true;
                    }
                }

                if (addedModel)
                {
                    instancesAdded++;
                }
            }

            if (instancesAdded == 0)
            {
                Debug.LogWarning("Forest Generator: the Grass_ models contained no batchable meshes.");
                return 0;
            }

            Transform grassRoot = new GameObject("Grass_Batched").transform;
            grassRoot.SetParent(parent, false);
            int batchIndex = 0;
            foreach (KeyValuePair<Material, List<GrassMeshBatch>> materialBatches in batches)
            {
                foreach (GrassMeshBatch batch in materialBatches.Value)
                {
                    if (batch.instances.Count == 0)
                    {
                        continue;
                    }

                    Mesh combinedMesh = new Mesh
                    {
                        name = "Forest_Grass_Batch_" + batchIndex.ToString("D3"),
                        indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
                    };
                    combinedMesh.CombineMeshes(batch.instances.ToArray(), true, true);
                    combinedMesh.RecalculateBounds();
                    string meshPath = TerrainFolder + "/" + combinedMesh.name + ".asset";
                    AssetDatabase.CreateAsset(combinedMesh, meshPath);

                    GameObject batchObject = new GameObject(
                        "Grass_Batch_" + batch.material.name + "_" + batchIndex.ToString("D3"),
                        typeof(MeshFilter), typeof(MeshRenderer));
                    Undo.RegisterCreatedObjectUndo(batchObject, "Create Batched Forest Grass");
                    batchObject.transform.SetParent(grassRoot, false);
                    batchObject.GetComponent<MeshFilter>().sharedMesh = combinedMesh;
                    batchObject.GetComponent<MeshRenderer>().sharedMaterial = batch.material;
                    GameObjectUtility.SetStaticEditorFlags(
                        batchObject, StaticEditorFlags.BatchingStatic);
                    batchIndex++;
                }
            }

            return instancesAdded;
        }

        private static void DeleteGeneratedGrassTintMaterials()
        {
            DeleteGeneratedAssets("t:Material", "Forest_GrassTint_");
        }

        private static void DeleteGeneratedAssets(string filter, string namePrefix)
        {
            string[] guids = AssetDatabase.FindAssets(filter, new[] { TerrainFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith(namePrefix, System.StringComparison.Ordinal))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private void ApplyVegetationTint(GameObject vegetation, System.Random random)
        {
            Renderer[] renderers = vegetation.GetComponentsInChildren<Renderer>(true);
            int tintIndex = random.Next(4);
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || (!source.HasProperty("_BaseColor") && !source.HasProperty("_Color")))
                    {
                        continue;
                    }

                    bool foliage = source.name.IndexOf("leaf", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || source.name.IndexOf("foliage", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || source.name.IndexOf("plant", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || source.name.IndexOf("grass", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    int style = foliage ? 0 : 1;
                    string key = AssetDatabase.GetAssetPath(source) + ":" + style + ":" + tintIndex;
                    if (!vegetationTintCache.TryGetValue(key, out Material variant))
                    {
                        variant = CreateVegetationTintMaterial(source, style, tintIndex);
                        if (variant == null)
                        {
                            continue;
                        }
                        vegetationTintCache.Add(key, variant);
                    }

                    materials[i] = variant;
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private static Material CreateVegetationTintMaterial(Material source, int style, int tintIndex)
        {
            Color[] foliageTints =
            {
                new Color(0.78f, 0.91f, 0.67f, 1f),
                new Color(0.91f, 0.86f, 0.58f, 1f),
                new Color(0.65f, 0.79f, 0.58f, 1f),
                new Color(0.87f, 0.73f, 0.51f, 1f)
            };
            Color[] woodTints =
            {
                new Color(0.94f, 0.88f, 0.76f, 1f),
                new Color(0.82f, 0.77f, 0.65f, 1f),
                new Color(1f, 0.89f, 0.71f, 1f),
                new Color(0.88f, 0.82f, 0.7f, 1f)
            };
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (string.IsNullOrEmpty(sourceGuid))
            {
                Debug.LogWarning("Forest Generator: could not create a color variant for " + source.name + ".");
                return null;
            }

            Material variant = new Material(source)
            {
                name = "Forest_VegTint_" + source.name.Replace('/', '_').Replace('\\', '_')
                    + "_" + style + "_" + tintIndex
            };
            Color tint = style == 0 ? foliageTints[tintIndex] : woodTints[tintIndex];
            if (source.HasProperty("_BaseColor"))
            {
                variant.SetColor("_BaseColor", source.GetColor("_BaseColor") * tint);
            }
            if (source.HasProperty("_Color"))
            {
                variant.SetColor("_Color", source.GetColor("_Color") * tint);
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(
                TerrainFolder + "/" + variant.name + "_" + sourceGuid + ".mat");
            AssetDatabase.CreateAsset(variant, path);
            return variant;
        }

        private static void MarkStaticRecursively(Transform root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform target in transforms)
            {
                GameObjectUtility.SetStaticEditorFlags(
                    target.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        private static Material[] CreateGrassTintMaterials(Material source)
        {
            Color[] tints =
            {
                new Color(0.92f, 1f, 0.88f, 1f),
                new Color(1f, 0.96f, 0.85f, 1f),
                new Color(0.86f, 0.94f, 0.82f, 1f)
            };
            string sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
            string safeName = source.name.Replace('/', '_').Replace('\\', '_');
            Material[] variants = new Material[tints.Length];
            for (int i = 0; i < variants.Length; i++)
            {
                Material variant = new Material(source)
                {
                    name = "Forest_GrassTint_" + safeName + "_" + i
                };
                if (source.HasProperty("_BaseColor"))
                {
                    variant.SetColor("_BaseColor", source.GetColor("_BaseColor") * tints[i]);
                }
                if (source.HasProperty("_Color"))
                {
                    variant.SetColor("_Color", source.GetColor("_Color") * tints[i]);
                }

                string assetPath = TerrainFolder + "/Forest_GrassTint_"
                    + sourceGuid + "_" + i + ".mat";
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                AssetDatabase.CreateAsset(variant, assetPath);
                variants[i] = variant;
            }

            return variants;
        }

        private static GameObject[] LoadGrassModels()
        {
            List<GameObject> models = new List<GameObject>();
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { NatureFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("Grass_", System.StringComparison.Ordinal))
                {
                    continue;
                }

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null && model.GetComponentInChildren<MeshFilter>(true) != null)
                {
                    models.Add(model);
                }
            }

            return models.ToArray();
        }

        private static float SampleTerrainHeight(
            Terrain terrain, Transform forestRoot, float localX, float forward)
        {
            Vector3 worldPosition = forestRoot.TransformPoint(new Vector3(localX, 0f, forward));
            return terrain.SampleHeight(worldPosition) + terrain.transform.position.y
                - forestRoot.position.y;
        }

        private void CreateForestFloorBoundsMesh(Transform parent)
        {
            const string meshPath = TerrainFolder + "/Forest_Floor_Bounds.asset";
            AssetDatabase.DeleteAsset(meshPath);
            Mesh boundsMesh = new Mesh
            {
                name = "Forest_Floor_Bounds",
                vertices = new[]
                {
                    new Vector3(-forestWidth * 0.5f, 0f, 0f),
                    new Vector3(-forestWidth * 0.5f, 0f, forestLength),
                    new Vector3(forestWidth * 0.5f, 0f, forestLength),
                    new Vector3(forestWidth * 0.5f, 0f, 0f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            boundsMesh.RecalculateBounds();
            AssetDatabase.CreateAsset(boundsMesh, meshPath);

            GameObject boundsObject = new GameObject("Forest_Floor");
            Undo.RegisterCreatedObjectUndo(boundsObject, "Create Forest Bounds Marker");
            boundsObject.transform.SetParent(parent, false);
            boundsObject.AddComponent<MeshFilter>().sharedMesh = boundsMesh;
            MeshRenderer boundsRenderer = boundsObject.AddComponent<MeshRenderer>();
            boundsRenderer.enabled = false;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            if (separator <= 0)
            {
                return;
            }

            string parent = path.Substring(0, separator);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureAssetFolder(parent);
            }

            if (AssetDatabase.IsValidFolder(parent))
            {
                AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
            }
        }

        private static void SetupForestLighting(Transform parent, float length, float width)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.53f, 0.68f, 0.83f);
            RenderSettings.ambientEquatorColor = new Color(0.44f, 0.52f, 0.31f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.21f, 0.12f);
            RenderSettings.ambientIntensity = 0.95f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = HorizonFogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.01f;

            Light[] existingLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            Light sun = null;
            foreach (Light existingLight in existingLights)
            {
                if (existingLight.type == LightType.Directional)
                {
                    sun = existingLight;
                    break;
                }
            }

            if (sun == null)
            {
                GameObject sunObject = new GameObject("Forest_Sun");
                Undo.RegisterCreatedObjectUndo(sunObject, "Create Forest Sun");
                sunObject.transform.SetParent(parent, false);
                sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
                sun = Undo.AddComponent<Light>(sunObject);
                sun.type = LightType.Directional;
            }

            sun.transform.rotation = Quaternion.Euler(36f, -28f, 0f);
            sun.color = new Color(1f, 0.84f, 0.61f);
            sun.intensity = 1.65f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            RenderSettings.sun = sun;
            Material sunMaterial = GetOrCreateMaterial("Forest_Sun", new Color(1f, 0.78f, 0.38f));
            if (sunMaterial != null && sunMaterial.HasProperty("_EmissionColor"))
            {
                sunMaterial.EnableKeyword("_EMISSION");
                sunMaterial.SetColor("_EmissionColor", new Color(1f, 0.65f, 0.24f) * 3f);
            }

            GameObject sunDisc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sunDisc.name = "Forest_Sun_Disc";
            Undo.RegisterCreatedObjectUndo(sunDisc, "Create Visible Forest Sun");
            sunDisc.transform.SetParent(parent, false);
            sunDisc.transform.localPosition = new Vector3(-width * 0.42f, length * 0.52f, length * 0.82f);
            sunDisc.transform.localScale = Vector3.one * Mathf.Clamp(length * 0.09f, 8f, 16f);
            Object.DestroyImmediate(sunDisc.GetComponent<SphereCollider>());
            sunDisc.GetComponent<Renderer>().sharedMaterial = sunMaterial;
        }

        private void CreateClouds(Transform parent)
        {
            Material cloudMaterial = GetOrCreateMaterial("Forest_Cloud", new Color(0.91f, 0.94f, 0.92f));
            if (cloudMaterial == null)
            {
                return;
            }

            Transform clouds = new GameObject("Forest_Clouds").transform;
            clouds.SetParent(parent, false);
            System.Random random = new System.Random(seed + 431);
            for (int i = 0; i < 7; i++)
            {
                Transform cloud = new GameObject("Cloud_" + (i + 1)).transform;
                cloud.SetParent(clouds, false);
                cloud.localPosition = new Vector3(
                    (float)random.NextDouble() * forestWidth * 1.6f - forestWidth * 0.8f,
                    forestLength * (0.42f + (float)random.NextDouble() * 0.16f),
                    forestLength * (0.15f + i * 0.14f));

                int puffs = random.Next(4, 7);
                for (int puff = 0; puff < puffs; puff++)
                {
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.name = "Puff_" + (puff + 1);
                    Undo.RegisterCreatedObjectUndo(sphere, "Create Forest Cloud");
                    sphere.transform.SetParent(cloud, false);
                    sphere.transform.localPosition = new Vector3(
                        (float)random.NextDouble() * 14f - 7f,
                        (float)random.NextDouble() * 2f,
                        (float)random.NextDouble() * 8f - 4f);
                    sphere.transform.localScale = new Vector3(
                        10f + (float)random.NextDouble() * 9f,
                        3f + (float)random.NextDouble() * 3f,
                        7f + (float)random.NextDouble() * 7f);
                    Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());
                    Renderer renderer = sphere.GetComponent<Renderer>();
                    renderer.sharedMaterial = cloudMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
        }

        private void CreateMountainBackdrop(Transform parent)
        {
            CreateMountainBackdrop(parent, forestLength, forestWidth, seed, HorizonRootName, 0f);
        }

        internal static void CreateMountainBackdrop(Transform parent, float length, float width, int seed)
        {
            CreateMountainBackdrop(parent, length, width, seed, "Village_Mountain_Backdrop", -length * 0.5f);
        }

        private static void CreateMountainBackdrop(
            Transform parent, float length, float width, int seed, string rootName, float centerZ)
        {
            Transform backdrop = new GameObject(rootName).transform;
            backdrop.SetParent(parent, false);
            backdrop.localPosition = new Vector3(0f, 0f, centerZ);

            // Three stacked ranges on every side. Farther ranges are taller, paler and hazier,
            // so the mountains read as atmospheric depth instead of one wall at the map edge.
            const int layerCount = 3;
            float[] offsets = { 20f, 44f, 72f };
            float[] depths = { 30f, 40f, 52f };
            float[] heights = { 24f, 36f, 50f };
            float[] haze = { 0.10f, 0.34f, 0.60f };
            float span = Mathf.Max(length, width) * 1.1f;

            CreateDistantGround(
                backdrop, length, width, offsets[layerCount - 1] + depths[layerCount - 1] + 40f);

            Color foot = new Color(0.17f, 0.29f, 0.17f);
            Color rock = new Color(0.34f, 0.39f, 0.36f);
            Color summit = new Color(0.68f, 0.74f, 0.74f);

            for (int layer = 0; layer < layerCount; layer++)
            {
                string tag = "Forest_Peak_L" + (layer + 1);
                Material[] bands =
                {
                    GetOrCreatePeakMaterial(tag + "_Foot", Color.Lerp(foot, HorizonFogColor, haze[layer])),
                    GetOrCreatePeakMaterial(tag + "_Rock", Color.Lerp(rock, HorizonFogColor, haze[layer])),
                    GetOrCreatePeakMaterial(tag + "_Summit", Color.Lerp(summit, HorizonFogColor, haze[layer] * 0.8f))
                };

                if (bands[0] == null || bands[1] == null || bands[2] == null)
                {
                    continue;
                }

                float offset = offsets[layer];
                float depth = depths[layer];
                float height = heights[layer];
                int s = seed + 71 + layer * 13;

                // Local +Z of every range points away from the forest, so the ridge rises
                // from the map edge outward and its sun-facing side faces the player.
                CreateMountainRange(backdrop, "Rear_Range_" + (layer + 1),
                    new Vector3(0f, 0f, length + offset), Quaternion.identity,
                    width + span, height, depth, s, bands);
                CreateMountainRange(backdrop, "Front_Range_" + (layer + 1),
                    new Vector3(0f, 0f, -offset), Quaternion.Euler(0f, 180f, 0f),
                    width + span, height * 0.85f, depth, s + 5, bands);
                CreateMountainRange(backdrop, "Left_Range_" + (layer + 1),
                    new Vector3(-width * 0.5f - offset, 0f, length * 0.5f), Quaternion.Euler(0f, -90f, 0f),
                    length + span, height * 0.9f, depth, s + 9, bands);
                CreateMountainRange(backdrop, "Right_Range_" + (layer + 1),
                    new Vector3(width * 0.5f + offset, 0f, length * 0.5f), Quaternion.Euler(0f, 90f, 0f),
                    length + span, height * 0.9f, depth, s + 13, bands);
            }
        }

        // Big flat plane under everything beyond the forest floor, so the gap between the
        // forest edge and the first mountains is dark green ground instead of empty sky.
        private static void CreateDistantGround(Transform parent, float length, float width, float reach)
        {
            Material ground = GetOrCreatePeakMaterial("Forest_Distant_Ground",
                Color.Lerp(new Color(0.17f, 0.27f, 0.16f), HorizonFogColor, 0.25f));
            if (ground == null)
            {
                return;
            }

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Forest_Distant_Ground";
            Undo.RegisterCreatedObjectUndo(plane, "Create Distant Forest Ground");
            plane.transform.SetParent(parent, false);
            float sizeX = width + reach * 2f + 40f;
            float sizeZ = length + reach * 2f + 40f;
            plane.transform.localPosition = new Vector3(0f, -0.02f, length * 0.5f);
            plane.transform.localScale = new Vector3(sizeX / 10f, 1f, sizeZ / 10f);
            Object.DestroyImmediate(plane.GetComponent<MeshCollider>());
            Renderer renderer = plane.GetComponent<Renderer>();
            renderer.sharedMaterial = ground;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // One mountain range: a flat-shaded height field (ridged noise, bell-shaped across its
        // depth so it rises and falls like a ridge). Each triangle gets its own vertices, so the
        // sun lights it facet by facet. Triangles are sorted into three materials by height:
        // forested foothills, bare rock, pale summits.
        private static void CreateMountainRange(
            Transform parent, string name, Vector3 position, Quaternion rotation,
            float span, float height, float depth, int rangeSeed, Material[] bands)
        {
            const float cell = 7f;
            int cols = Mathf.Max(8, Mathf.RoundToInt(span / cell));
            int rows = Mathf.Max(4, Mathf.RoundToInt(depth / cell));
            float cellX = span / cols;
            float cellZ = depth / rows;
            float seedX = rangeSeed * 0.37f;
            float seedZ = rangeSeed * 0.61f;
            System.Random random = new System.Random(rangeSeed);

            Vector3[,] grid = new Vector3[cols + 1, rows + 1];
            for (int i = 0; i <= cols; i++)
            {
                float t = i / (float)cols;
                float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.12f))
                                 * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / 0.12f));
                for (int j = 0; j <= rows; j++)
                {
                    float zn = j / (float)rows;
                    float bell = Mathf.Pow(Mathf.Sin(zn * Mathf.PI), 0.85f);
                    float x = (t - 0.5f) * span;
                    float z = zn * depth;

                    // Irregular facets: nudge inner vertices sideways.
                    if (i > 0 && i < cols)
                    {
                        x += ((float)random.NextDouble() - 0.5f) * cellX * 0.55f;
                    }
                    if (j > 0 && j < rows)
                    {
                        z += ((float)random.NextDouble() - 0.5f) * cellZ * 0.55f;
                    }

                    float ridged = RidgedNoise(x * 0.018f + seedX, z * 0.018f + seedZ);
                    float broad = Mathf.PerlinNoise(x * 0.006f + seedX * 0.5f, 3.7f + seedZ * 0.3f);
                    float y = height * bell * edgeFade
                              * Mathf.Lerp(0.35f, 1f, ridged) * Mathf.Lerp(0.55f, 1.15f, broad);

                    // Edges sink 1 unit below the ground so there is never a visible seam.
                    grid[i, j] = new Vector3(x, y - 1f, z);
                }
            }

            List<Vector3> verts = new List<Vector3>();
            List<int>[] tris = { new List<int>(), new List<int>(), new List<int>() };
            for (int i = 0; i < cols; i++)
            {
                for (int j = 0; j < rows; j++)
                {
                    Vector3 a = grid[i, j];
                    Vector3 b = grid[i, j + 1];
                    Vector3 c = grid[i + 1, j + 1];
                    Vector3 d = grid[i + 1, j];
                    if (((i + j) & 1) == 0)
                    {
                        AddFacet(verts, tris, a, b, c, height);
                        AddFacet(verts, tris, a, c, d, height);
                    }
                    else
                    {
                        AddFacet(verts, tris, a, b, d, height);
                        AddFacet(verts, tris, b, c, d, height);
                    }
                }
            }

            Mesh mesh = new Mesh { name = name + "_Mesh" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = tris.Length;
            for (int band = 0; band < tris.Length; band++)
            {
                mesh.SetTriangles(tris[band], band);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject range = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(range, "Create Mountain Range");
            range.transform.SetParent(parent, false);
            range.transform.localPosition = position;
            range.transform.localRotation = rotation;
            MeshFilter filter = Undo.AddComponent<MeshFilter>(range);
            filter.sharedMesh = mesh;
            MeshRenderer renderer = Undo.AddComponent<MeshRenderer>(range);
            renderer.sharedMaterials = bands;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void AddFacet(
            List<Vector3> verts, List<int>[] tris, Vector3 a, Vector3 b, Vector3 c, float height)
        {
            float frac = (a.y + b.y + c.y) / (3f * Mathf.Max(0.01f, height));
            int band = frac < 0.35f ? 0 : frac < 0.62f ? 1 : 2;
            int start = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            tris[band].Add(start);
            tris[band].Add(start + 1);
            tris[band].Add(start + 2);
        }

        private static float RidgedNoise(float x, float z)
        {
            float sum = 0f;
            float norm = 0f;
            float amplitude = 0.55f;
            float frequency = 1f;
            for (int octave = 0; octave < 3; octave++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * frequency, z * frequency) * 2f - 1f);
                n *= n;
                sum += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.1f;
            }

            return sum / norm;
        }

        // Lit material with no shine, so mountains look matte and low-poly instead of plastic.
        private static Material GetOrCreatePeakMaterial(string name, Color color)
        {
            Material material = GetOrCreateMaterial(name, color);
            if (material == null)
            {
                return null;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0f);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            if (material.HasProperty("_EnvironmentReflections"))
            {
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AddQuad(List<int> triangles, int bottomLeft, int topLeft, int topRight, int bottomRight)
        {
            triangles.Add(bottomLeft);
            triangles.Add(topLeft);
            triangles.Add(topRight);
            triangles.Add(bottomLeft);
            triangles.Add(topRight);
            triangles.Add(bottomRight);
        }

        private static void ConfigureForestMusic(Transform parent)
        {
            AudioClip forestMusic = null;
            string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
            foreach (string guid in audioGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(System.IO.Path.GetFileName(path), "forestbgm.ogg",
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    forestMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    break;
                }
            }

            if (forestMusic == null)
            {
                Debug.LogWarning(
                    "Forest Generator: forestbgm.ogg was not found. Put it anywhere under Assets/Audio " +
                    "and generate the forest again to enable looping forest music.");
                return;
            }

            GameObject musicObject = new GameObject("Forest_BGM");
            Undo.RegisterCreatedObjectUndo(musicObject, "Add Forest Background Music");
            musicObject.transform.SetParent(parent, false);
            AudioSource source = Undo.AddComponent<AudioSource>(musicObject);
            source.clip = forestMusic;
            source.loop = true;
            source.playOnAwake = true;
            source.volume = 0.5f;
            source.spatialBlend = 0f;
        }

        private void SetupPlayer(Transform forestRoot, float length, Terrain terrain)
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                Undo.RegisterCreatedObjectUndo(player, "Create Forest Player");
            }

            player.SetActive(true);
            player.transform.SetParent(null, true);
            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = Undo.AddComponent<CharacterController>(player);
            }
            characterController.enabled = false;
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.stepOffset = 0.3f;
            float startForward = Mathf.Clamp(5f, 1f, length - 1f);
            float startX = GetTrailCenterX(startForward);
            float startHeight = SampleTerrainHeight(terrain, forestRoot, startX, startForward);
            player.transform.SetPositionAndRotation(
                forestRoot.TransformPoint(new Vector3(startX, startHeight + 0.15f, startForward)),
                forestRoot.rotation);
            characterController.enabled = true;

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<PlayerController>(player);
            }

            ForestOpeningCutscene cutscene = player.GetComponent<ForestOpeningCutscene>();
            if (cutscene == null)
            {
                cutscene = Undo.AddComponent<ForestOpeningCutscene>(player);
            }
            else
            {
                Undo.RecordObject(cutscene, "Enable Forest Opening Cutscene");
            }
            cutscene.enabled = true;
            EditorUtility.SetDirty(cutscene);

            GameObject knightModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/KayKit/Characters/fbx/Knight.fbx");
            RuntimeAnimatorController animatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Art/Characters/KayKit/Animations/Knight_Player.controller");
            if (animatorController == null)
            {
                animatorController = CreateKnightAnimatorController();
            }

            Avatar knightAvatar = LoadAvatar("Assets/Art/Characters/KayKit/Characters/fbx/Knight.fbx");
            if (knightModel != null && animatorController != null && knightAvatar != null && knightAvatar.isValid)
            {
                Undo.RecordObject(controller, "Configure Forest Knight");
                controller.ConfigureCharacterModel(knightModel, animatorController, knightAvatar);
                EditorUtility.SetDirty(controller);
            }
            else
            {
                Debug.LogWarning(
                    "Forest Generator: Player was created, but the Knight visual is not configured yet. " +
                    "Check that Knight.fbx has a valid Avatar and the KayKit locomotion clips are imported.");
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
            }

            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Forest Main Camera");
                camera = Undo.AddComponent<Camera>(cameraObject);
            }

            camera.gameObject.SetActive(true);
            camera.gameObject.tag = "MainCamera";
            camera.enabled = true;
            camera.targetDisplay = 0;
            camera.orthographic = false;
            camera.fieldOfView = 65f;
            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = new Vector3(0f, 1.6f, 0.15f);
            camera.transform.localRotation = Quaternion.identity;

            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener == null)
            {
                listener = Undo.AddComponent<AudioListener>(camera.gameObject);
            }
            listener.enabled = true;

            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include);
            foreach (AudioListener existingListener in listeners)
            {
                if (existingListener != listener)
                {
                    existingListener.enabled = false;
                }
            }

            controller.ConfigureCamera(camera);
        }

        private static RuntimeAnimatorController CreateKnightAnimatorController()
        {
            const string generalPath =
                "Assets/Art/Characters/KayKit/Animations/fbx/Rig_Medium/Rig_Medium_General.fbx";
            const string movementPath =
                "Assets/Art/Characters/KayKit/Animations/fbx/Rig_Medium/Rig_Medium_MovementBasic.fbx";
            const string controllerPath = "Assets/Art/Characters/KayKit/Animations/Knight_Player.controller";

            AnimationClip idle = FindAnimationClip("Idle_A", generalPath);
            AnimationClip walk = FindAnimationClip("Walking_A", movementPath);
            AnimationClip run = FindAnimationClip("Running_A", movementPath);
            if (idle == null || walk == null || run == null)
            {
                Debug.LogError("Forest Generator: KayKit Idle_A, Walking_A, and Running_A clips must be imported.");
                return null;
            }

            const string folder = "Assets/Art/Characters/KayKit/Animations";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogError("Forest Generator: KayKit Animations folder was not found.");
                return null;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller == null)
            {
                Debug.LogError("Forest Generator: failed to create Knight_Player.controller.");
                return null;
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            AnimatorState state = controller.layers[0].stateMachine.AddState("Locomotion");
            BlendTree blendTree = new BlendTree
            {
                name = "Knight Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            blendTree.AddChild(idle, 0f);
            blendTree.AddChild(walk, 4f);
            blendTree.AddChild(run, 7f);
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            state.motion = blendTree;
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip FindAnimationClip(string clipName, string assetPath)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                AnimationClip clip = asset as AnimationClip;
                if (clip != null && string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            return null;
        }

        private static Avatar LoadAvatar(string assetPath)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                Avatar avatar = asset as Avatar;
                if (avatar != null)
                {
                    return avatar;
                }
            }

            return null;
        }

        private static Bounds GetLocalRendererBounds(GameObject target, Renderer[] renderers)
        {
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                Bounds worldBounds = renderer.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 worldPoint = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 localPoint = target.transform.InverseTransformPoint(worldPoint);
                    if (!hasBounds)
                    {
                        bounds = new Bounds(localPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localPoint);
                    }
                }
            }

            return bounds;
        }

        private static void AddTreeCollider(GameObject tree)
        {
            Renderer[] renderers = tree.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("Forest Generator: tree has no renderers; could not create its trunk collider.");
                return;
            }

            Bounds bounds = GetLocalRendererBounds(tree, renderers);
            CapsuleCollider capsule = tree.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = Undo.AddComponent<CapsuleCollider>(tree);
            }

            capsule.direction = 1;
            capsule.height = Mathf.Max(1.8f, bounds.size.y * 0.82f);
            capsule.radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.16f, 0.3f, 0.85f);
            capsule.center = new Vector3(bounds.center.x, bounds.min.y + capsule.height * 0.5f, bounds.center.z);
        }

        private static void AddBushCollider(GameObject bush)
        {
            Renderer[] renderers = bush.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("Forest Generator: bush has no renderers; could not create its collider.");
                return;
            }

            Bounds bounds = GetLocalRendererBounds(bush, renderers);
            BoxCollider box = bush.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(bush);
            }

            box.center = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.4f, bounds.center.z);
            box.size = new Vector3(
                Mathf.Clamp(bounds.size.x * 0.65f, 0.6f, 2.4f),
                Mathf.Clamp(bounds.size.y * 0.65f, 0.6f, 1.8f),
                Mathf.Clamp(bounds.size.z * 0.65f, 0.6f, 2.4f));
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            const string folder = "Assets/_Project/Materials";
            string path = folder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                {
                    AssetDatabase.CreateFolder("Assets", "_Project");
                }
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError("Forest Generator: could not find a supported material shader.");
                return null;
            }

            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material GetOrCreateUnlitMaterial(string name, Color color)
        {
            const string folder = "Assets/_Project/Materials";
            string path = folder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                Debug.LogError("Forest Generator: could not find a supported unlit shader for the horizon ring.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                {
                    AssetDatabase.CreateFolder("Assets", "_Project");
                }
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
#endif