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
        private static readonly Color HorizonFogColor = new Color(0.38f, 0.45f, 0.4f);
        private float forestLength = 140f;
        private float forestWidth = 110f;
        private float pathWidth = 6f;
        private int treeCount = 800;
        private int undergrowthCount = 1350;
        private float pathWinding = 7f;
        private int seed = 17;

        [MenuItem("Tools/Lentera/Forest Generator")]
        private static void Open()
        {
            GetWindow<ForestBuilderWindow>("Forest Generator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Starting Scene Forest", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Builds a complete, dense forest stage with rolling ground, a clear dirt trail, trees, and clustered grass, flowers, rocks, and bushes. " +
                "Works in an empty scene and does not require a Player or village. Regenerating replaces the forest and distant mountain backdrop.",
                MessageType.Info);

            forestLength = Mathf.Max(20f, EditorGUILayout.FloatField("Forest length", forestLength));
            forestWidth = Mathf.Max(20f, EditorGUILayout.FloatField("Forest width", forestWidth));
            pathWidth = Mathf.Clamp(EditorGUILayout.FloatField("Clear path width", pathWidth), 3f, forestWidth - 4f);
            pathWinding = Mathf.Clamp(EditorGUILayout.FloatField("Path winding", pathWinding), 0f, forestWidth * 0.2f);
            treeCount = Mathf.Clamp(EditorGUILayout.IntField("Dense tree count", treeCount), 10, 1500);
            undergrowthCount = Mathf.Clamp(EditorGUILayout.IntField("Undergrowth props", undergrowthCount), 0, 2000);
            seed = EditorGUILayout.IntField("Seed", seed);

            if (GUILayout.Button("ONE CLICK: Generate Complete Dense Forest"))
            {
                BuildForest();
            }

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

            Material groundMaterial = GetOrCreateMaterial("Forest_Ground", new Color(0.30f, 0.38f, 0.16f));
            Material pathMaterial = GetOrCreateMaterial("Forest_Path", new Color(0.43f, 0.34f, 0.21f));
            if (groundMaterial == null || pathMaterial == null)
            {
                Undo.DestroyObjectImmediate(root);
                return;
            }

            CreateFloor(root.transform, "Forest_Floor", forestLength, forestWidth, -0.04f, groundMaterial);
            CreateDirtTrail(root.transform, pathMaterial);
            SetupForestLighting(root.transform, forestLength, forestWidth);
            CreateClouds(root.transform);
            CreateMountainBackdrop(root.transform);
            ConfigureForestMusic(root.transform);
            SetupPlayer(root.transform, forestLength);

            Transform treeRoot = new GameObject("Trees").transform;
            treeRoot.SetParent(root.transform, false);
            System.Random random = new System.Random(seed);
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

                GameObject model = trees[random.Next(trees.Length)];
                GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (tree == null)
                {
                    Debug.LogError("Forest Generator: failed to instantiate tree model " + model.name + ".");
                    continue;
                }

                Undo.RegisterCreatedObjectUndo(tree, "Place Forest Tree");
                tree.transform.SetParent(treeRoot, false);
                float terrainHeight = GetTerrainHeight(GetTrailCenterX(forward) + side, forward);
                tree.transform.localPosition = new Vector3(GetTrailCenterX(forward) + side, terrainHeight, forward);
                tree.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                float scale = 0.85f + (float)random.NextDouble() * 0.7f;
                tree.transform.localScale = Vector3.one * scale;
                AddTreeCollider(tree);
                placed++;
            }

            Transform undergrowthRoot = new GameObject("Undergrowth").transform;
            undergrowthRoot.SetParent(root.transform, false);
            List<GameObject> rocks = new List<GameObject>();
            List<GameObject> foliage = new List<GameObject>();
            foreach (GameObject model in undergrowth)
            {
                if (model.name.StartsWith("Pebble_") || model.name.StartsWith("Rock_"))
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

                List<GameObject> category = foliage.Count > 0
                    && (rocks.Count == 0 || random.NextDouble() >= 0.18)
                    ? foliage
                    : rocks;
                GameObject model = category[random.Next(category.Count)];
                GameObject plant = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (plant == null)
                {
                    Debug.LogError("Forest Generator: failed to instantiate undergrowth model " + model.name + ".");
                    continue;
                }

                Undo.RegisterCreatedObjectUndo(plant, "Place Forest Undergrowth");
                plant.transform.SetParent(undergrowthRoot, false);
                float terrainHeight = GetTerrainHeight(GetTrailCenterX(forward) + side, forward);
                plant.transform.localPosition = new Vector3(GetTrailCenterX(forward) + side, terrainHeight, forward);
                plant.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                float scale = 0.65f + (float)random.NextDouble() * 0.8f;
                if (model.name.StartsWith("Grass_") || model.name.StartsWith("Flower_")
                    || model.name.StartsWith("Clover_") || model.name.StartsWith("Mushroom_"))
                {
                    scale *= 0.65f;
                }
                else if (model.name.StartsWith("Pebble_"))
                {
                    scale *= 0.55f;
                }
                plant.transform.localScale = Vector3.one * scale;
                if (model.name.StartsWith("Bush_"))
                {
                    AddBushCollider(plant);
                }
                undergrowthPlaced++;
            }

            Selection.activeGameObject = root;
            Debug.Log("Forest Generator: generated winding dirt trail, " + placed + " trees, "
                      + undergrowthPlaced + " clustered nature props, clouds, sun, and forest music setup.");
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
                    || name.StartsWith("Rock_Medium_")))
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

        private float GetTerrainHeight(float x, float forward)
        {
            float noise = Mathf.PerlinNoise((x + seed * 13.7f) * 0.032f, (forward - seed * 5.3f) * 0.032f);
            float fineNoise = Mathf.PerlinNoise((x - seed * 7.1f) * 0.11f, (forward + seed * 3.9f) * 0.11f);
            float rolling = (noise - 0.5f) * 2.6f
                + (fineNoise - 0.5f) * 0.42f
                + Mathf.Sin((x - forward * 0.45f + seed) * 0.04f) * 0.32f;
            float distanceFromTrail = Mathf.Abs(x - GetTrailCenterX(forward));
            float naturalTerrain = Mathf.SmoothStep(0f, 1f,
                (distanceFromTrail - pathWidth * 0.5f - 1f) / 8f);
            float endBlend = Mathf.SmoothStep(0f, 1f, Mathf.Min(forward, forestLength - forward) / 12f);
            return rolling * naturalTerrain * endBlend;
        }

        private void CreateDirtTrail(Transform parent, Material material)
        {
            const string meshFolder = "Assets/_Project/Meshes";
            const string meshPath = meshFolder + "/Forest_DirtTrail.asset";
            if (!AssetDatabase.IsValidFolder(meshFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                {
                    AssetDatabase.CreateFolder("Assets", "_Project");
                }

                AssetDatabase.CreateFolder("Assets/_Project", "Meshes");
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Forest_DirtTrail" };
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            mesh.Clear();
            const int segments = 96;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float forward = forestLength * i / segments;
                float widthVariation = Mathf.Sin(i * 0.31f + seed) * 0.06f
                    + Mathf.Sin(i * 0.83f + seed * 0.7f) * 0.035f;
                float halfWidth = pathWidth * (0.48f + widthVariation);
                float centerX = GetTrailCenterX(forward);
                vertices[i * 2] = new Vector3(centerX - halfWidth, -0.015f, forward);
                vertices[i * 2 + 1] = new Vector3(centerX + halfWidth, -0.015f, forward);
                if (i == segments)
                {
                    continue;
                }

                int vertex = i * 2;
                int triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            GameObject trail = new GameObject("Forest_DirtTrail");
            Undo.RegisterCreatedObjectUndo(trail, "Create Forest Dirt Trail");
            trail.transform.SetParent(parent, false);
            MeshFilter filter = trail.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = trail.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            MeshCollider collider = trail.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
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
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = Mathf.Max(length, width) * 0.55f;
            RenderSettings.fogEndDistance = Mathf.Max(length, width) * 1.8f;

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
            plane.transform.localPosition = new Vector3(0f, -0.09f, length * 0.5f);
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

        private void SetupPlayer(Transform forestRoot, float length)
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                Undo.RegisterCreatedObjectUndo(player, "Create Forest Player");
            }

            player.SetActive(true);
            player.transform.SetParent(null, true);
            float startForward = Mathf.Clamp(length * 0.5f, 8f, length - 8f);
            player.transform.SetPositionAndRotation(
                forestRoot.TransformPoint(new Vector3(GetTrailCenterX(startForward), 0.1f, startForward)),
                forestRoot.rotation);

            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = Undo.AddComponent<CharacterController>(player);
            }
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.stepOffset = 0.3f;

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

        private void CreateFloor(
            Transform parent, string name, float length, float width, float height, Material material)
        {
            int xSegments = Mathf.Max(2, Mathf.CeilToInt(width / 2f));
            int zSegments = Mathf.Max(2, Mathf.CeilToInt(length / 2f));
            Vector3[] vertices = new Vector3[(xSegments + 1) * (zSegments + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[xSegments * zSegments * 6];
            for (int z = 0; z <= zSegments; z++)
            {
                float forward = length * z / zSegments;
                for (int x = 0; x <= xSegments; x++)
                {
                    float localX = Mathf.Lerp(-width * 0.5f, width * 0.5f, (float)x / xSegments);
                    int index = z * (xSegments + 1) + x;
                    vertices[index] = new Vector3(
                        localX, height + GetTerrainHeight(localX, forward) - 0.08f, forward);
                    uv[index] = new Vector2((float)x / xSegments, (float)z / zSegments);
                }
            }

            int triangle = 0;
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int bottomLeft = z * (xSegments + 1) + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + xSegments + 1;
                    int topRight = topLeft + 1;
                    triangles[triangle++] = bottomLeft;
                    triangles[triangle++] = topLeft;
                    triangles[triangle++] = bottomRight;
                    triangles[triangle++] = bottomRight;
                    triangles[triangle++] = topLeft;
                    triangles[triangle++] = topRight;
                }
            }

            Mesh mesh = new Mesh { name = name + "_Mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject floor = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            floor.name = name;
            Undo.RegisterCreatedObjectUndo(floor, "Create " + name);
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = Vector3.zero;
            floor.GetComponent<MeshFilter>().sharedMesh = mesh;
            floor.GetComponent<MeshCollider>().sharedMesh = mesh;
            floor.GetComponent<Renderer>().sharedMaterial = material;
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