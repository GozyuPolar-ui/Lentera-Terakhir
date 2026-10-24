#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using JRPG.Core;
using JRPG.Rendering;

namespace JRPG.EditorTools
{
    /// <summary>
    /// Menu: Tools > Lentera > Village Builder
    /// Generates a village with a gently winding road, houses following the road, and varied nature props.
    /// Everything is created under a root called "Village_Generated". Running it again
    /// deletes and rebuilds that root, so do manual edits on a copy.
    /// </summary>
    public class VillageBuilderWindow : EditorWindow
    {
        private const string RootName = "Village_Generated";
        private const float HouseRoofOverhang = 0.45f;

        [SerializeField] private GameObject wallStraight;
        [SerializeField] private GameObject wallDoor;
        [SerializeField] private GameObject wallWindow;
        [SerializeField] private GameObject floorTile;
        [SerializeField] private GameObject roofCover;
        [SerializeField] private GameObject[] interiorFurniture = new GameObject[0];

        [SerializeField] private bool useHousePrefabs;
        [SerializeField] private GameObject[] houses = new GameObject[0];
        [SerializeField] private GameObject[] props = new GameObject[0];
        [SerializeField] private GameObject roadTile;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private Material groundMaterial;
        [SerializeField] private AudioClip villageMusic;

        [SerializeField] private float villageLength = 140f;
        [SerializeField] private float villageDepth = 110f;
        [SerializeField] private float roadWidth = 6f;
        [SerializeField] private float roadBendAmount = 20f;
        [SerializeField] private int roadBendCount = 4;
        [SerializeField] private float setback = 1.5f;
        [SerializeField] private float gapMin = 1f;
        [SerializeField] private float gapMax = 4f;
        [SerializeField] private float emptyChance = 0.1f;
        [SerializeField] private float yawJitter = 3f;
        [SerializeField] private float groundMargin = 10f;
        [SerializeField] private float windowChance = 0.45f;

        [SerializeField] private int propCount = 360;
        [SerializeField] private float propScaleMin = 0.8f;
        [SerializeField] private float propScaleMax = 1.2f;
        [SerializeField] private float propClearance = 1.5f;

        [SerializeField] private int seed = 1;
        [SerializeField] private int settingsVersion;

        private SerializedObject so;
        private Vector2 scroll;
        private System.Random rng;
        private readonly Dictionary<GameObject, Bounds> boundsCache = new Dictionary<GameObject, Bounds>();

        [MenuItem("Tools/Lentera/Village Builder")]
        private static void Open()
        {
            GetWindow<VillageBuilderWindow>("Village Builder");
        }

        private void OnEnable()
        {
            so = new SerializedObject(this);
            UpgradeLayoutDefaults();
            LoadDefaultHousePieces();
            if (interiorFurniture == null || interiorFurniture.Length == 0)
            {
                interiorFurniture = LoadDefaultInteriorFurniture();
            }

            GameObject[] defaultProps = LoadDefaultNatureProps();
            if (props == null || props.Length == 0)
            {
                props = defaultProps;
                if (props.Length > 0)
                {
                    propCount = Mathf.Max(propCount, 360);
                }
            }
            else
            {
                List<GameObject> combinedProps = new List<GameObject>(props);
                foreach (GameObject defaultProp in defaultProps)
                {
                    if (defaultProp != null && !combinedProps.Contains(defaultProp))
                    {
                        combinedProps.Add(defaultProp);
                    }
                }

                props = combinedProps.ToArray();
            }
        }

        private void UpgradeLayoutDefaults()
        {
            if (settingsVersion >= 6)
            {
                return;
            }

            bool previousNatureDefaults = settingsVersion == 5
                && Mathf.Approximately(villageLength, 140f)
                && Mathf.Approximately(villageDepth, 110f)
                && Mathf.Approximately(roadWidth, 6f)
                && Mathf.Approximately(roadBendAmount, 20f)
                && roadBendCount == 4
                && propCount == 200;
            if (previousNatureDefaults)
            {
                propCount = 360;
            }

            bool oldOriginalDefaults = Mathf.Approximately(villageLength, 60f)
                && Mathf.Approximately(villageDepth, 50f)
                && Mathf.Approximately(roadWidth, 4f)
                && Mathf.Approximately(roadBendAmount, 6f)
                && roadBendCount == 3
                && propCount == 80;
            bool previousDefaults = Mathf.Approximately(villageLength, 110f)
                && Mathf.Approximately(villageDepth, 90f)
                && Mathf.Approximately(roadWidth, 5.5f)
                && Mathf.Approximately(roadBendAmount, 10f)
                && roadBendCount == 2
                && propCount == 130;
            bool latestDefaults = Mathf.Approximately(villageLength, 140f)
                && Mathf.Approximately(villageDepth, 110f)
                && Mathf.Approximately(roadWidth, 6f)
                && Mathf.Approximately(roadBendAmount, 12f)
                && roadBendCount == 3
                && propCount == 200;
            bool earlierCurvedDefaults = Mathf.Approximately(villageLength, 140f)
                && Mathf.Approximately(villageDepth, 110f)
                && Mathf.Approximately(roadWidth, 6f)
                && Mathf.Approximately(roadBendAmount, 20f)
                && roadBendCount == 3
                && propCount == 200;
            bool previousWindingDefaults = Mathf.Approximately(villageLength, 140f)
                && Mathf.Approximately(villageDepth, 110f)
                && Mathf.Approximately(roadWidth, 6f)
                && Mathf.Approximately(roadBendAmount, 20f)
                && roadBendCount == 2
                && propCount == 200;

            if (oldOriginalDefaults || previousDefaults || latestDefaults
                || earlierCurvedDefaults || previousWindingDefaults)
            {
                villageLength = 140f;
                villageDepth = 110f;
                roadWidth = 6f;
                roadBendAmount = 20f;
                roadBendCount = 4;
                propCount = 360;
            }

            settingsVersion = 6;
        }

        private void OnGUI()
        {
            if (so == null)
            {
                so = new SerializedObject(this);
            }

            so.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Pieces", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Houses are generated automatically from the modular pieces below with varied footprints, heights, and roofs. " +
                "Existing house prefabs are optional overrides.",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "Desa Hearth uses animated Quaternius outfits for six NPCs: Doran, Tetua, Pedagang, " +
                "Penjaga Lentera, Anak, and Warga. The builder imports their Generic rigs and animations automatically.",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "ONE CLICK: Build Village creates furnished house interiors, sealed roofs, road, props and collisions, " +
                "mountain backdrop, atmosphere, Player and Camera.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "Controls: WASD to move, Left Shift to sprint, Space to jump, and mouse to look.",
                MessageType.None);
            EditorGUILayout.LabelField("Automatic Houses", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("wallStraight"));
            EditorGUILayout.PropertyField(so.FindProperty("wallDoor"));
            EditorGUILayout.PropertyField(so.FindProperty("wallWindow"));
            EditorGUILayout.PropertyField(so.FindProperty("floorTile"));
            EditorGUILayout.PropertyField(so.FindProperty("roofCover"), new GUIContent("Roof cover"));
            EditorGUILayout.PropertyField(so.FindProperty("interiorFurniture"), new GUIContent("Interior furniture"), true);
            windowChance = EditorGUILayout.Slider("Window wall chance", windowChance, 0f, 1f);
            EditorGUILayout.PropertyField(so.FindProperty("useHousePrefabs"), new GUIContent("Use saved house prefabs"));
            if (useHousePrefabs)
            {
                EditorGUILayout.PropertyField(so.FindProperty("houses"), new GUIContent("House prefabs"), true);
            }
            EditorGUILayout.PropertyField(so.FindProperty("roadTile"), new GUIContent("Road tile (optional)"));
            EditorGUILayout.PropertyField(so.FindProperty("roadMaterial"), new GUIContent("Road material (optional)"));
            EditorGUILayout.PropertyField(so.FindProperty("groundMaterial"), new GUIContent("Ground material (optional)"));
            EditorGUILayout.PropertyField(so.FindProperty("props"), new GUIContent("Props (trees, rocks, ...)"), true);
            EditorGUILayout.PropertyField(so.FindProperty("villageMusic"), new GUIContent("Village music (optional, loops)"));
            so.ApplyModifiedProperties();
            if (GUILayout.Button("Load Diverse Tropical Nature Set"))
            {
                props = LoadDefaultNatureProps();
                propCount = Mathf.Max(propCount, 360);
                EditorUtility.SetDirty(this);
            }
            if (GUILayout.Button("Load Default Modular House Pieces"))
            {
                LoadDefaultHousePieces(true);
                interiorFurniture = LoadDefaultInteriorFurniture();
                EditorUtility.SetDirty(this);
            }
            EditorGUILayout.HelpBox(
                "Generated modular houses get furniture on each floor. A winding road and mountains centered around the village are generated automatically.",
                MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
            villageLength = Mathf.Max(10f, EditorGUILayout.FloatField("Village length (X)", villageLength));
            villageDepth = Mathf.Max(10f, EditorGUILayout.FloatField("Min ground depth (Z)", villageDepth));
            roadWidth = Mathf.Max(1f, EditorGUILayout.FloatField("Road width", roadWidth));
            roadBendAmount = Mathf.Clamp(EditorGUILayout.FloatField("Road bend amount", roadBendAmount), 0f, villageDepth * 0.3f);
            roadBendCount = Mathf.Clamp(EditorGUILayout.IntField("Road bend count", roadBendCount), 1, 6);
            setback = Mathf.Max(0f, EditorGUILayout.FloatField("Gap road to houses", setback));
            gapMin = Mathf.Max(0f, EditorGUILayout.FloatField("House gap min", gapMin));
            gapMax = Mathf.Max(gapMin, EditorGUILayout.FloatField("House gap max", gapMax));
            emptyChance = EditorGUILayout.Slider("Empty lot chance", emptyChance, 0f, 0.6f);
            yawJitter = EditorGUILayout.Slider("House rotation jitter", yawJitter, 0f, 10f);
            groundMargin = Mathf.Max(0f, EditorGUILayout.FloatField("Ground margin", groundMargin));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Props", EditorStyles.boldLabel);
            propCount = Mathf.Max(0, EditorGUILayout.IntField("Prop count", propCount));
            propScaleMin = Mathf.Max(0.1f, EditorGUILayout.FloatField("Scale min", propScaleMin));
            propScaleMax = Mathf.Max(propScaleMin, EditorGUILayout.FloatField("Scale max", propScaleMax));
            propClearance = Mathf.Max(0f, EditorGUILayout.FloatField("Clearance from road/houses", propClearance));

            EditorGUILayout.Space();
            seed = EditorGUILayout.IntField("Seed", seed);

            EditorGUILayout.Space();
            if (GUILayout.Button("ONE CLICK: Build Complete Playable Village"))
            {
                Build();
            }

            if (GUILayout.Button("Add 6 Hearth NPCs to Current Village"))
            {
                GameObject village = GameObject.Find(RootName);
                if (village == null)
                {
                    EditorUtility.DisplayDialog(
                        "Village Not Found",
                        "Build the village first, then add the Hearth NPCs.",
                        "OK");
                }
                else
                {
                    BuildVillageNpcs(village.transform);
                    EditorSceneManager.MarkSceneDirty(village.scene);
                    Selection.activeGameObject = village.transform.Find("NPC_Doran")?.gameObject;
                    Debug.Log("Village Builder: added or refreshed all six Hearth NPCs.");
                }
            }

            if (GUILayout.Button("Add Player + Camera to Current Scene"))
            {
                GameObject player = SetupPlayer(null);
                ConfigureFootsteps(player);
                Selection.activeGameObject = player;
            }

            if (GUILayout.Button("Add Player + Camera at Forest Entrance"))
            {
                GameObject player = SetupPlayer(null);
                if (player != null)
                {
                    Transform forest = GameObject.Find("Forest_Generated")?.transform;
                    if (forest != null)
                    {
                        Undo.RecordObject(player.transform, "Place Player at Forest Entrance");
                        player.transform.SetPositionAndRotation(
                            forest.TransformPoint(new Vector3(0f, 0.1f, 2f)),
                            forest.rotation);
                        EditorUtility.SetDirty(player.transform);
                    }
                    else
                    {
                        Debug.LogWarning(
                            "Village Builder: no Forest_Generated object was found. Player was added at the scene origin.");
                    }

                    ConfigureFootsteps(player);
                    EditorSceneManager.MarkSceneDirty(player.scene);
                    Selection.activeGameObject = player;
                }
            }

            if (GUILayout.Button("Add Talking NPC to Current Scene"))
            {
                GameObject npc = SetupNpc(null);
                if (npc != null)
                {
                    Selection.activeGameObject = npc;
                }
            }

            if (GUILayout.Button("Add Atmosphere, Trees + Audio to Current Village"))
            {
                ApplyVillageAtmosphere();
            }
            EditorGUILayout.HelpBox(
                "Use this button to refresh the clouds and sky without rebuilding the village layout.",
                MessageType.None);

            if (GUILayout.Button("Repair House + Prop Collisions"))
            {
                GameObject village = GameObject.Find(RootName);
                if (village == null)
                {
                    Debug.LogWarning("Village Builder: no Village_Generated found. Build the village first.");
                }
                else
                {
                    Transform housesRoot = village.transform.Find("Houses");
                    Transform propsRoot = village.transform.Find("Props");
                    int housesFixed = 0;
                    int propsFixed = 0;
                    if (housesRoot != null)
                    {
                        for (int i = 0; i < housesRoot.childCount; i++)
                        {
                            if (AddHouseCollider(housesRoot.GetChild(i)))
                            {
                                housesFixed++;
                            }
                        }
                    }

                    if (propsRoot != null)
                    {
                        for (int i = 0; i < propsRoot.childCount; i++)
                        {
                            AddPropCollider(propsRoot.GetChild(i).gameObject);
                            propsFixed++;
                        }
                    }

                    Debug.Log("Village Builder: repaired walk-through house/prop colliders for "
                              + housesFixed + " houses and " + propsFixed + " props.");
                }
            }

            if (GUILayout.Button("Randomize Seed + Build"))
            {
                seed = Random.Range(1, 99999);
                Build();
            }

            if (GUILayout.Button("Delete Generated Village"))
            {
                DeleteGenerated();
            }

            EditorGUILayout.HelpBox(
                "Only the object named '" + RootName + "' is deleted/rebuilt. Duplicate it first if you want to keep manual edits.",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        private void Build()
        {
            if (wallStraight == null || wallDoor == null || wallWindow == null
                || floorTile == null || roofCover == null
                || interiorFurniture == null || interiorFurniture.Length == 0)
            {
                LoadDefaultHousePieces();
            }

            if (interiorFurniture == null || interiorFurniture.Length == 0)
            {
                interiorFurniture = LoadDefaultInteriorFurniture();
            }

            if (props == null || props.Length == 0)
            {
                props = LoadDefaultNatureProps();
            }

            boundsCache.Clear();
            DeleteGenerated();
            rng = new System.Random(seed);

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Village");

            Transform groundT = NewGroup(root.transform, "Ground");
            Transform roadT = NewGroup(root.transform, "Road");
            Transform housesT = NewGroup(root.transform, "Houses");
            Transform propsT = NewGroup(root.transform, "Props");

            List<Rect> occupied = new List<Rect>();
            float maxAbsZ = roadBendAmount + roadWidth * 0.5f;

            BuildRoad(roadT);
            BuildHouses(housesT, occupied, ref maxAbsZ);
            Vector2 groundSize = BuildGround(groundT, maxAbsZ, occupied);
            BuildProps(propsT, occupied, groundSize.x, groundSize.y);
            BuildAtmosphere(root.transform, groundSize.x, groundSize.y);
            GameObject player = SetupPlayer(root.transform);
            ConfigureFootsteps(player);
            BuildVillageNpcs(root.transform);

            Selection.activeGameObject = root;
            Debug.Log("Village Builder: complete playable village built with houses, props, collisions, atmosphere, and Player.");
        }

        private static GameObject SetupPlayer(Transform parent)
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                Undo.RegisterCreatedObjectUndo(player, "Create Village Player");
                if (parent != null)
                {
                    player.transform.SetParent(parent, false);
                    player.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                }
                else
                {
                    player.transform.position = new Vector3(0f, 0.1f, 0f);
                }
            }

            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = Undo.AddComponent<CharacterController>(player);
                characterController.height = 1.8f;
                characterController.radius = 0.35f;
                characterController.center = new Vector3(0f, 0.9f, 0f);
                characterController.stepOffset = 0.3f;
            }

            if (player.GetComponent<PlayerController>() == null)
            {
                Undo.AddComponent<PlayerController>(player);
            }

            Transform placeholder = player.transform.Find("Player_Visual");
            if (placeholder != null)
            {
                Undo.DestroyObjectImmediate(placeholder.gameObject);
            }

            GameObject knightModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/KayKit/Characters/fbx/Knight.fbx");
            Avatar knightAvatar = null;
            UnityEngine.Object[] knightAssets = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Art/Characters/KayKit/Characters/fbx/Knight.fbx");
            foreach (UnityEngine.Object asset in knightAssets)
            {
                Avatar avatar = asset as Avatar;
                if (avatar != null)
                {
                    knightAvatar = avatar;
                    break;
                }
            }

            if (knightModel == null)
            {
                Debug.LogWarning("Village Builder: KayKit Knight model was not found. Player will use the camera only.");
            }
            else if (knightAvatar == null || !knightAvatar.isValid)
            {
                Debug.LogWarning(
                    "Village Builder: KayKit Knight Avatar is missing or invalid. Player and camera are still configured; " +
                    "reimport Knight.fbx with Create From This Model enabled to add the animated visual.");
            }
            else
            {
                RuntimeAnimatorController knightAnimator = CreateKnightAnimatorController();
                if (knightAnimator != null)
                {
                    PlayerController playerController = player.GetComponent<PlayerController>();
                    Undo.RecordObject(playerController, "Configure Animated Knight");
                    playerController.ConfigureCharacterModel(knightModel, knightAnimator, knightAvatar);
                    EditorUtility.SetDirty(playerController);
                }
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            }

            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Main Camera");
                mainCamera = Undo.AddComponent<Camera>(cameraObject);
            }

            mainCamera.gameObject.SetActive(true);
            mainCamera.gameObject.tag = "MainCamera";
            mainCamera.enabled = true;
            mainCamera.targetDisplay = 0;

            AudioListener listener = mainCamera.GetComponent<AudioListener>();
            if (listener == null)
            {
                listener = Undo.AddComponent<AudioListener>(mainCamera.gameObject);
            }
            listener.enabled = true;

            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (AudioListener sceneListener in listeners)
            {
                if (sceneListener != listener)
                {
                    sceneListener.enabled = false;
                }
            }

            HD2DCameraFollow cameraFollow = mainCamera.GetComponent<HD2DCameraFollow>();
            if (cameraFollow != null)
            {
                Undo.DestroyObjectImmediate(cameraFollow);
            }

            Undo.SetTransformParent(mainCamera.transform, player.transform, "Attach First Person Camera");
            mainCamera.transform.localPosition = new Vector3(0f, 1.6f, 0.15f);
            mainCamera.transform.localRotation = Quaternion.identity;
            mainCamera.orthographic = false;
            mainCamera.fieldOfView = 65f;
            player.GetComponent<PlayerController>().ConfigureCamera(mainCamera);
            return player;
        }

        private static RuntimeAnimatorController CreateKnightAnimatorController()
        {
            const string movementAnimationsPath =
                "Assets/Art/Characters/KayKit/Animations/fbx/Rig_Medium/Rig_Medium_MovementBasic.fbx";
            const string generalAnimationsPath =
                "Assets/Art/Characters/KayKit/Animations/fbx/Rig_Medium/Rig_Medium_General.fbx";
            const string controllerPath = "Assets/Art/Characters/KayKit/Animations/Knight_Player.controller";

            AnimationClip idle = FindAnimationClip("idle", generalAnimationsPath, movementAnimationsPath);
            AnimationClip walk = FindAnimationClip("walk", movementAnimationsPath, generalAnimationsPath);
            AnimationClip run = FindAnimationClip("run", movementAnimationsPath, generalAnimationsPath);
            if (idle == null || walk == null || run == null)
            {
                Debug.LogError(
                    "Village Builder: KayKit Knight locomotion clips were not found. " +
                    "Expected Idle, Walk, and Run clips in the KayKit Animations/Rig_Medium FBX files.");
                return null;
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                if (controller == null)
                {
                    Debug.LogError("Village Builder: could not create the Knight animator controller.");
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
            }

            return controller;
        }

        private static GameObject SetupNpc(Transform parent)
        {
            Transform player = GameObject.Find("Player")?.transform;
            Vector3 position = player != null
                ? player.position + player.forward * 4f
                : new Vector3(0f, 0f, 3f);
            Quaternion rotation = player != null
                ? Quaternion.LookRotation(player.position - position, Vector3.up)
                : Quaternion.identity;
            return CreateVillageNpc(
                parent, "NPC_Doran", "Male_Peasant.fbx", "Peasant",
                "Hair_Buzzed.fbx", CreateNpcAnimatorController(), "Doran",
                new[]
                {
                    "Aku Doran, penjaga gerbang. Tidak ada seorang pun yang pernah keluar hidup-hidup dari hutan itu.",
                    "Lentera itu... Tetua harus melihatnya. Dia tahu asal cahayanya.",
                    "Kabut pernah menelan desa kami. Aku gagal melindungi mereka. Kali ini aku akan ikut denganmu."
                },
                1f, Color.white, position, rotation);
        }

        private void BuildVillageNpcs(Transform parent)
        {
            if (!EnsureQuaterniusNpcAssets())
            {
                return;
            }

            RuntimeAnimatorController npcAnimatorController = CreateNpcAnimatorController();
            if (npcAnimatorController == null)
            {
                return;
            }

            string[] npcRoles =
            {
                "Doran", "Tetua", "Pedagang", "Penjaga_Lentera", "Anak", "Warga"
            };
            foreach (string role in npcRoles)
            {
                Transform existingNpc = parent.Find("NPC_" + role);
                if (existingNpc != null)
                {
                    Undo.DestroyObjectImmediate(existingNpc.gameObject);
                }
            }

            Transform legacyNpc = parent.Find("NPC_Mira");
            if (legacyNpc != null)
            {
                Undo.DestroyObjectImmediate(legacyNpc.gameObject);
            }

            List<Bounds> occupiedBounds = CollectNpcPlacementObstacles(parent);
            PlaceVillageNpc(parent, occupiedBounds, npcAnimatorController, "Doran", "Male_Ranger.fbx", "Ranger",
                "Hair_Buzzed.fbx", 0.07f, 1, 1f,
                Color.white,
                "Aku Doran, penjaga gerbang. Tidak ada seorang pun yang pernah keluar hidup-hidup dari hutan itu.",
                "Lentera itu... Tetua harus melihatnya. Dia tahu asal cahayanya.",
                "Kabut pernah menelan desa kami. Aku gagal melindungi mereka. Kali ini aku akan ikut denganmu.");
            PlaceVillageNpc(parent, occupiedBounds, npcAnimatorController, "Tetua", "Male_Peasant.fbx", "Peasant",
                "Hair_Beard.fbx", 0.91f, -1, 1f,
                Color.white,
                "Cahaya pada lentera itu adalah cahaya penjaga lama. Aku tak melihatnya sejak malam kabut datang.",
                "Tiga menara suar mengunci kabut di sekitar Hearth. Nyalakan kembali ketiganya.",
                "Ikuti sinar lentera sampai ke sumber kabut. Di sanalah jawaban tentang asalnya menunggu.");
            PlaceVillageNpc(parent, occupiedBounds, npcAnimatorController, "Pedagang", "Male_Peasant.fbx", "Peasant_2",
                "Hair_SimpleParted.fbx", 0.24f, 1, 1f,
                Color.white,
                "Selamat datang di kios Hearth. Perjalanan ke menara suar tak akan ringan.",
                "Aku punya perlengkapan untuk dijual. Kumpulkan bekal sebelum meninggalkan desa.");
            PlaceVillageNpc(parent, occupiedBounds, npcAnimatorController, "Penjaga_Lentera", "Female_Peasant.fbx", "Peasant",
                "Hair_Long.fbx", 0.43f, -1, 1f,
                Color.white,
                "Lentera itu milik penjaga yang datang ke Hearth bertahun-tahun lalu.",
                "Ia pernah bercerita tentang seorang perempuan yang membawa cahaya serupa... mungkin ibumu.",
                "Kalau lentera ini menyala kembali, jalan menuju sumber kabut akan terbuka.");
            PlaceVillageNpc(parent, occupiedBounds, npcAnimatorController, "Anak", "Female_Peasant.fbx", "Peasant_2",
                "Hair_BuzzedFemale.fbx", 0.59f, 1, 0.72f,
                Color.white,
                "Aku melihat cahaya bergerak di bukit dekat menara utara!",
                "Orang dewasa bilang itu cuma kabut, tapi kabut tidak berkelip seperti lentera.");
            PlaceVillageNpc(parent, occupiedBounds, npcAnimatorController, "Warga", "Female_Ranger.fbx", "Ranger_3",
                "Hair_Buns.fbx", 0.75f, -1, 1f,
                Color.white,
                "Doran selalu berjaga paling lama sejak kabut menelan desanya.",
                "Jalan mengikuti lengkungan bukit ke menara pertama. Jangan sampai kehilangan cahaya lentera.");
        }

        private static bool EnsureQuaterniusNpcAssets()
        {
            string[] models =
            {
                "Male_Ranger.fbx", "Male_Peasant.fbx",
                "Female_Peasant.fbx", "Female_Ranger.fbx"
            };
            foreach (string model in models)
            {
                string modelPath =
                    "Assets/Art/Quaternius/Outfits/Exports/FBX (Unity)/Outfits/" + model;
                if (!PrepareQuaterniusFbx(modelPath, false)
                    || AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null)
                {
                    Debug.LogError("Village Builder: Quaternius NPC outfit could not be loaded at " + modelPath + ".");
                    return false;
                }
            }

            string[] baseModels =
            {
                "Superhero_Male_FullBody.fbx", "Superhero_Female_FullBody.fbx"
            };
            foreach (string model in baseModels)
            {
                string modelPath = "Assets/Art/Quaternius/Base/Base Characters/Unity/" + model;
                if (!PrepareQuaterniusFbx(modelPath, false, true)
                    || AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null)
                {
                    Debug.LogError("Village Builder: Quaternius NPC base character could not be loaded at " + modelPath + ".");
                    return false;
                }
            }

            string[] hairstyles =
            {
                "Hair_Buzzed.fbx", "Hair_Beard.fbx", "Hair_SimpleParted.fbx",
                "Hair_Long.fbx", "Hair_BuzzedFemale.fbx", "Hair_Buns.fbx"
            };
            foreach (string hairstyle in hairstyles)
            {
                string hairstylePath =
                    "Assets/Art/Quaternius/Base/Hairstyles/Rigged to Head Bone/FBX (Unity)/" + hairstyle;
                if (!PrepareQuaterniusFbx(hairstylePath, false)
                    || AssetDatabase.LoadAssetAtPath<GameObject>(hairstylePath) == null)
                {
                    Debug.LogError("Village Builder: Quaternius NPC hairstyle could not be loaded at " + hairstylePath + ".");
                    return false;
                }
            }

            string[] textures =
            {
                "Assets/Art/Quaternius/Outfits/Textures/Peasant/T_Peasant_BaseColor.png",
                "Assets/Art/Quaternius/Outfits/Textures/Peasant/T_Peasant_2_BaseColor.png",
                "Assets/Art/Quaternius/Outfits/Textures/Ranger/T_Ranger_BaseColor.png",
                "Assets/Art/Quaternius/Outfits/Textures/Ranger/T_Ranger_3_BaseColor.png",
                "Assets/Art/Quaternius/Base/Base Characters/Textures/T_Superhero_Male_Dark.png",
                "Assets/Art/Quaternius/Base/Base Characters/Textures/T_Superhero_Female_Dark_BaseColor.png",
                "Assets/Art/Quaternius/Base/Base Characters/Textures/T_Eye_Brown.png",
                "Assets/Art/Quaternius/Base/Base Characters/Textures/T_Hair_1_BaseColor.png"
            };
            foreach (string texturePath in textures)
            {
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
                {
                    Debug.LogError(
                        "Village Builder: required Quaternius NPC texture could not be loaded at "
                        + texturePath + ".");
                    return false;
                }
            }

            return true;
        }

        private static RuntimeAnimatorController CreateNpcAnimatorController()
        {
            const string idleAnimationPath =
                "Assets/Art/Quaternius/Animations/Female Mannequin/Unity/UAL2_Standard.fbx";
            const string talkingAnimationPath = "Assets/Art/Quaternius/Animations/Unity/UAL1_Standard.fbx";
            const string controllerPath = "Assets/Art/Quaternius/Animations/Quaternius_Npc.controller";
            if (!PrepareQuaterniusFbx(idleAnimationPath, true)
                || !PrepareQuaterniusFbx(talkingAnimationPath, true))
            {
                return null;
            }

            AnimationClip idle = FindAnimationClip("Idle_FoldArms_Loop", idleAnimationPath);
            AnimationClip talking = FindAnimationClip("Idle_Talking_Loop", talkingAnimationPath);
            if (idle == null || talking == null)
            {
                Debug.LogError(
                    "Village Builder: Quaternius NPC Idle_FoldArms_Loop and Idle_Talking_Loop clips are required.");
                return null;
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                if (controller == null)
                {
                    Debug.LogError("Village Builder: could not create the Quaternius NPC animator controller.");
                    return null;
                }
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = FindOrCreateAnimatorState(stateMachine, "Idle");
            AnimatorState talkingState = FindOrCreateAnimatorState(stateMachine, "Talking");
            idleState.motion = idle;
            talkingState.motion = talking;
            stateMachine.defaultState = idleState;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimatorState FindOrCreateAnimatorState(
            AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state.name == stateName)
                {
                    return childState.state;
                }
            }

            return stateMachine.AddState(stateName);
        }

        private static List<Bounds> CollectNpcPlacementObstacles(Transform village)
        {
            List<Bounds> obstacles = new List<Bounds>();
            string[] obstacleGroups = { "Houses", "Props", "Troops" };
            foreach (string groupName in obstacleGroups)
            {
                Transform group = village.Find(groupName);
                if (group == null)
                {
                    continue;
                }

                foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>(true))
                {
                    obstacles.Add(renderer.bounds);
                }
            }

            return obstacles;
        }

        private void PlaceVillageNpc(
            Transform parent, List<Bounds> occupiedBounds, RuntimeAnimatorController animatorController,
            string role, string modelFile,
            string outfit, string hairstyleFile,
            float roadProgress, int side,
            float scale, Color tint, params string[] lines)
        {
            Vector3 position;
            Quaternion rotation;
            if (!TryFindNpcPosition(parent, occupiedBounds, roadProgress, side, scale,
                out position, out rotation))
            {
                Debug.LogWarning(
                    "Village Builder: no clear position was found for NPC '" + role
                    + "'. Clear space near the road and add the NPCs again.");
                return;
            }

            GameObject npc = CreateVillageNpc(
                parent, "NPC_" + role, modelFile, outfit, hairstyleFile, animatorController,
                role.Replace("_", " "), lines,
                scale, tint, position, rotation);
            if (npc != null)
            {
                occupiedBounds.Add(new Bounds(
                    position + Vector3.up * (scale * 0.9f),
                    new Vector3(scale * 1.3f, scale * 1.8f, scale * 1.3f)));
            }
        }

        private bool TryFindNpcPosition(
            Transform parent, List<Bounds> occupiedBounds, float roadProgress,
            int preferredSide, float scale, out Vector3 position, out Quaternion rotation)
        {
            float[] progressOffsets = { 0f, -0.035f, 0.035f, -0.07f, 0.07f, -0.11f, 0.11f, -0.15f, 0.15f };
            float[] sideDistances =
            {
                roadWidth * 0.5f + 1.25f,
                roadWidth * 0.5f + 3f,
                roadWidth * 0.5f + 5f,
                roadWidth * 0.5f + 7f
            };
            Transform ground = parent.Find("Ground/Ground");
            Renderer groundRenderer = ground != null ? ground.GetComponent<Renderer>() : null;
            Bounds groundBounds = groundRenderer != null
                ? groundRenderer.bounds
                : new Bounds(parent.position,
                    new Vector3(villageLength + groundMargin * 2f, 10f, villageDepth + groundMargin * 2f));

            for (int progressIndex = 0; progressIndex < progressOffsets.Length; progressIndex++)
            {
                float progress = Mathf.Clamp(
                    roadProgress + progressOffsets[progressIndex], 0.04f, 0.96f);
                float x = Mathf.Lerp(-villageLength * 0.5f, villageLength * 0.5f, progress);
                float yaw = GetRoadYaw(x);
                Vector3 roadPoint = parent.TransformPoint(new Vector3(x, 0.05f, GetRoadCenterZ(x)));
                Vector3 normal = parent.TransformDirection(GetRoadNormal(yaw));

                for (int distanceIndex = 0; distanceIndex < sideDistances.Length; distanceIndex++)
                {
                    for (int sideIndex = 0; sideIndex < 2; sideIndex++)
                    {
                        int candidateSide = sideIndex == 0 ? preferredSide : -preferredSide;
                        Vector3 candidate = roadPoint + normal * candidateSide * sideDistances[distanceIndex];
                        Quaternion candidateRotation = Quaternion.LookRotation(
                            roadPoint - candidate, Vector3.up);
                        Bounds footprint = new Bounds(
                            candidate + Vector3.up * (scale * 0.9f),
                            new Vector3(scale * 1.3f + 0.6f, scale * 1.8f, scale * 1.3f + 0.6f));
                        if (!ContainsBoundsXZ(groundBounds, footprint)
                            || IntersectsObstacleXZ(footprint, occupiedBounds))
                        {
                            continue;
                        }

                        position = candidate;
                        rotation = candidateRotation;
                        return true;
                    }
                }
            }

            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        private static bool ContainsBoundsXZ(Bounds container, Bounds candidate)
        {
            return candidate.min.x >= container.min.x && candidate.max.x <= container.max.x
                && candidate.min.z >= container.min.z && candidate.max.z <= container.max.z;
        }

        private static bool IntersectsObstacleXZ(Bounds candidate, List<Bounds> obstacles)
        {
            foreach (Bounds obstacle in obstacles)
            {
                if (candidate.min.x < obstacle.max.x && candidate.max.x > obstacle.min.x
                    && candidate.min.z < obstacle.max.z && candidate.max.z > obstacle.min.z
                    && candidate.max.y > obstacle.min.y && candidate.min.y < obstacle.max.y)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject CreateVillageNpc(
            Transform parent, string objectName, string modelFile, string outfit,
            string hairstyleFile, RuntimeAnimatorController animatorController, string displayName,
            string[] dialogueLines, float scale, Color tint, Vector3 position, Quaternion rotation)
        {
            string outfitModelPath = "Assets/Art/Quaternius/Outfits/Exports/FBX (Unity)/Outfits/" + modelFile;
            if (!PrepareQuaterniusFbx(outfitModelPath, false))
            {
                return null;
            }

            GameObject outfitModel = AssetDatabase.LoadAssetAtPath<GameObject>(outfitModelPath);
            if (outfitModel == null)
            {
                Debug.LogError("Village Builder: NPC outfit model was not found at " + outfitModelPath + ".");
                return null;
            }

            bool isFemale = modelFile.StartsWith("Female_", System.StringComparison.Ordinal);
            string baseFile = isFemale ? "Superhero_Female_FullBody.fbx" : "Superhero_Male_FullBody.fbx";
            string baseModelPath = "Assets/Art/Quaternius/Base/Base Characters/Unity/" + baseFile;
            if (!PrepareQuaterniusFbx(baseModelPath, false, true))
            {
                return null;
            }

            GameObject baseModel = AssetDatabase.LoadAssetAtPath<GameObject>(baseModelPath);
            if (baseModel == null)
            {
                Debug.LogError("Village Builder: NPC base character was not found at " + baseModelPath + ".");
                return null;
            }

            Avatar baseAvatar = null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(baseModelPath))
            {
                baseAvatar = asset as Avatar;
                if (baseAvatar != null)
                {
                    break;
                }
            }
            if (baseAvatar == null)
            {
                Debug.LogError("Village Builder: Generic Avatar was not created for " + baseModelPath + ".");
                return null;
            }

            string hairstylePath =
                "Assets/Art/Quaternius/Base/Hairstyles/Rigged to Head Bone/FBX (Unity)/" + hairstyleFile;
            if (!PrepareQuaterniusFbx(hairstylePath, false))
            {
                return null;
            }

            GameObject hairModel = AssetDatabase.LoadAssetAtPath<GameObject>(hairstylePath);
            if (hairModel == null)
            {
                Debug.LogError("Village Builder: NPC hairstyle was not found at " + hairstylePath + ".");
                return null;
            }

            string textureName;
            string textureDirectory;
            switch (outfit)
            {
                case "Peasant":
                    textureName = "T_Peasant_BaseColor.png";
                    textureDirectory = "Peasant";
                    break;
                case "Peasant_2":
                    textureName = "T_Peasant_2_BaseColor.png";
                    textureDirectory = "Peasant";
                    break;
                case "Ranger":
                    textureName = "T_Ranger_BaseColor.png";
                    textureDirectory = "Ranger";
                    break;
                case "Ranger_3":
                    textureName = "T_Ranger_3_BaseColor.png";
                    textureDirectory = "Ranger";
                    break;
                default:
                    Debug.LogError("Village Builder: unsupported Quaternius NPC outfit '" + outfit + "'.");
                    return null;
            }

            string texturePath = "Assets/Art/Quaternius/Outfits/Textures/"
                + textureDirectory + "/" + textureName;
            Texture2D bodyTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (bodyTexture == null)
            {
                Debug.LogError("Village Builder: Quaternius NPC texture was not found at " + texturePath + ".");
                return null;
            }

            string skinTexturePath = isFemale
                ? "Assets/Art/Quaternius/Base/Base Characters/Textures/T_Superhero_Female_Dark_BaseColor.png"
                : "Assets/Art/Quaternius/Base/Base Characters/Textures/T_Superhero_Male_Dark.png";
            Texture2D faceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(skinTexturePath);
            if (faceTexture == null)
            {
                Debug.LogError("Village Builder: Quaternius NPC skin texture was not found at " + skinTexturePath + ".");
                return null;
            }

            Texture2D eyeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/Quaternius/Base/Base Characters/Textures/T_Eye_Brown.png");
            Texture2D hairTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/Quaternius/Base/Base Characters/Textures/T_Hair_1_BaseColor.png");
            if (eyeTexture == null || hairTexture == null)
            {
                Debug.LogError("Village Builder: Quaternius NPC eye or hair texture could not be loaded.");
                return null;
            }

            GameObject npc = parent != null
                ? parent.Find(objectName)?.gameObject
                : GameObject.Find(objectName);
            if (npc == null)
            {
                npc = new GameObject(objectName);
                Undo.RegisterCreatedObjectUndo(npc, "Create Village NPC");
            }

            npc.transform.SetParent(parent, true);
            npc.transform.SetPositionAndRotation(position, rotation);

            CapsuleCollider capsule = npc.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = Undo.AddComponent<CapsuleCollider>(npc);
            }
            capsule.center = new Vector3(0f, 0.9f * scale, 0f);
            capsule.height = 1.8f * scale;
            capsule.radius = 0.4f * scale;

            NpcDialogue dialogue = npc.GetComponent<NpcDialogue>();
            if (dialogue == null)
            {
                dialogue = Undo.AddComponent<NpcDialogue>(npc);
            }

            Undo.RecordObject(dialogue, "Configure Village NPC");
            dialogue.Configure(
                baseModel, outfitModel, hairModel, bodyTexture, faceTexture,
                eyeTexture, hairTexture, null, baseAvatar, animatorController,
                displayName, dialogueLines, scale, tint);
            EditorUtility.SetDirty(dialogue);
            return npc;
        }

        private static bool PrepareQuaterniusFbx(
            string assetPath, bool importAnimations, bool createGenericAvatar = false)
        {
            string absolutePath = System.IO.Path.Combine(
                Application.dataPath, assetPath.Substring("Assets/".Length));
            if (!System.IO.File.Exists(absolutePath))
            {
                Debug.LogError("Village Builder: required Quaternius FBX is missing at " + assetPath + ".");
                return false;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("Village Builder: Unity could not import Quaternius FBX at " + assetPath + ".");
                return false;
            }

            ModelImporterAvatarSetup avatarSetup = createGenericAvatar
                ? ModelImporterAvatarSetup.CreateFromThisModel
                : ModelImporterAvatarSetup.NoAvatar;
            bool settingsChanged = importer.animationType != ModelImporterAnimationType.Generic
                || importer.avatarSetup != avatarSetup
                || importer.importAnimation != importAnimations;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = avatarSetup;
            importer.importAnimation = importAnimations;
            if (settingsChanged)
            {
                importer.SaveAndReimport();
            }

            return true;
        }

        private static AnimationClip FindAnimationClip(string keyword, params string[] assetPaths)
        {
            AnimationClip partialMatch = null;
            foreach (string assetPath in assetPaths)
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (UnityEngine.Object asset in assets)
                {
                    AnimationClip clip = asset as AnimationClip;
                    if (clip == null
                        || (keyword == "idle"
                            && clip.name.IndexOf("jump", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        continue;
                    }

                    if (string.Equals(clip.name, keyword, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return clip;
                    }

                    if (partialMatch == null
                        && clip.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        partialMatch = clip;
                    }
                }
            }

            return partialMatch;
        }

        private void ApplyVillageAtmosphere()
        {
            GameObject village = GameObject.Find(RootName);
            if (village == null)
            {
                Debug.LogWarning("Village Builder: no Village_Generated found. Build the village first.");
                return;
            }

            Transform propsRoot = village.transform.Find("Props");
            Transform housesRoot = village.transform.Find("Houses");
            if (propsRoot != null && propsRoot.childCount == 0 && props != null && props.Length > 0)
            {
                List<Rect> occupied = new List<Rect>();
                float maxAbsZ = roadWidth * 0.5f;
                if (housesRoot != null)
                {
                    for (int i = 0; i < housesRoot.childCount; i++)
                    {
                        Bounds bounds;
                        if (TryGetLocalRendererBounds(housesRoot.GetChild(i), out bounds))
                        {
                            occupied.Add(new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z));
                            maxAbsZ = Mathf.Max(maxAbsZ, Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z));
                        }
                    }
                }

                rng = new System.Random(seed);
                float groundX = villageLength + groundMargin * 2f;
                float groundZ = Mathf.Max(villageDepth, (maxAbsZ + groundMargin) * 2f);
                BuildProps(propsRoot, occupied, groundX, groundZ);
            }

            BuildAtmosphere(village.transform, villageLength + groundMargin * 2f,
                Mathf.Max(villageDepth, (villageDepth * 0.5f + groundMargin) * 2f));
            BuildVillageNpcs(village.transform);
            ConfigureFootsteps(SetupPlayer(null));
            Debug.Log("Village Builder: applied clouds, village ambience, tree props, and player footsteps.");
        }

        private void BuildAtmosphere(Transform villageRoot, float groundWidth, float groundDepth)
        {
            Transform oldAtmosphere = villageRoot.Find("Village_Atmosphere");
            if (oldAtmosphere != null)
            {
                Undo.DestroyObjectImmediate(oldAtmosphere.gameObject);
            }

            Transform atmosphere = NewGroup(villageRoot, "Village_Atmosphere");
            Transform clouds = NewGroup(atmosphere, "Clouds");
            Undo.AddComponent<VillageCloudDrift>(clouds.gameObject);
            BuildClouds(clouds, groundWidth, groundDepth);
            ForestBuilderWindow.CreateMountainBackdrop(
                atmosphere, groundDepth, groundWidth, seed);
            ApplyVillageLighting();
            ConfigureVillageMusic(atmosphere);
        }

        private static void BuildClouds(Transform parent, float groundWidth, float groundDepth)
        {
            Material cloudMaterial = GetOrCreateMaterial("Village_Cloud", new Color(0.94f, 0.96f, 1f));
            float xLimit = groundWidth * 0.5f + 35f;
            float zLimit = groundDepth * 0.5f + 25f;
            System.Random cloudRng = new System.Random(731);

            for (int i = 0; i < 32; i++)
            {
                GameObject cloud = new GameObject("Cloud_" + (i + 1));
                Undo.RegisterCreatedObjectUndo(cloud, "Create Village Cloud");
                cloud.transform.SetParent(parent, false);
                cloud.transform.localPosition = new Vector3(
                    (float)(cloudRng.NextDouble() * xLimit * 2f - xLimit),
                    55f + (float)cloudRng.NextDouble() * 20f,
                    (float)(cloudRng.NextDouble() * zLimit * 2f - zLimit));

                int puffCount = cloudRng.Next(4, 7);
                for (int puff = 0; puff < puffCount; puff++)
                {
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Undo.RegisterCreatedObjectUndo(sphere, "Create Cloud Puff");
                    sphere.name = "Puff_" + (puff + 1);
                    sphere.transform.SetParent(cloud.transform, false);
                    sphere.transform.localPosition = new Vector3(
                        (float)cloudRng.NextDouble() * 9f - 4.5f,
                        (float)cloudRng.NextDouble() * 0.8f - 0.4f,
                        (float)cloudRng.NextDouble() * 5f - 2.5f);
                    sphere.transform.localScale = new Vector3(
                        3f + (float)cloudRng.NextDouble() * 2f,
                        1f + (float)cloudRng.NextDouble() * 0.6f,
                        2f + (float)cloudRng.NextDouble() * 1.5f);
                    Undo.DestroyObjectImmediate(sphere.GetComponent<SphereCollider>());
                    Renderer renderer = sphere.GetComponent<Renderer>();
                    renderer.sharedMaterial = cloudMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }

            VillageCloudDrift drift = parent.GetComponent<VillageCloudDrift>();
            drift.Configure(0.7f, xLimit);
        }

        private static void ApplyVillageLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.69f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.69f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.31f, 0.28f, 0.22f);
            RenderSettings.ambientIntensity = 1.15f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.69f, 0.77f, 0.84f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 100f;
            RenderSettings.fogEndDistance = 260f;

            Light[] lights = FindObjectsByType<Light>();
            foreach (Light light in lights)
            {
                if (light.type != LightType.Directional)
                {
                    continue;
                }

                RenderSettings.sun = light;
                light.color = new Color(1f, 0.88f, 0.72f);
                light.intensity = 1.35f;
                light.shadowStrength = 0.8f;
                break;
            }
        }

        private void ConfigureVillageMusic(Transform parent)
        {
            if (villageMusic == null)
            {
                villageMusic = FindDefaultVillageMusic();
            }

            if (villageMusic == null)
            {
                Debug.LogWarning("Village Builder: no music clip was found under Assets/Audio. Assign the village .ogg to Village music in this window to enable looping music.");
                return;
            }

            Transform existing = parent.Find("Village_Music");
            GameObject musicObject;
            if (existing == null)
            {
                musicObject = new GameObject("Village_Music");
                Undo.RegisterCreatedObjectUndo(musicObject, "Add Village Music");
                musicObject.transform.SetParent(parent, false);
            }
            else
            {
                musicObject = existing.gameObject;
            }

            AudioSource source = musicObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = Undo.AddComponent<AudioSource>(musicObject);
            }

            source.clip = villageMusic;
            source.loop = true;
            source.playOnAwake = true;
            source.volume = 0.22f;
            source.spatialBlend = 0f;
        }

        private static AudioClip FindDefaultVillageMusic()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(System.IO.Path.GetFileName(path), "villagebg.ogg",
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf("music", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && path.IndexOf("footstep", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null)
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        private static void ConfigureFootsteps(GameObject player)
        {
            if (player == null)
            {
                return;
            }

            Transform footsteps = player.transform.Find("Footsteps");
            GameObject footstepsObject;
            if (footsteps == null)
            {
                footstepsObject = new GameObject("Footsteps");
                Undo.RegisterCreatedObjectUndo(footstepsObject, "Add Player Footsteps");
                Undo.SetTransformParent(footstepsObject.transform, player.transform, "Parent Player Footsteps");
                footstepsObject.transform.localPosition = Vector3.zero;
            }
            else
            {
                footstepsObject = footsteps.gameObject;
            }

            AudioSource source = footstepsObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = Undo.AddComponent<AudioSource>(footstepsObject);
            }

            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0.55f;
            source.spatialBlend = 0.35f;

            List<AudioClip> clips = new List<AudioClip>();
            for (int i = 0; i < 10; i++)
            {
                string path = "Assets/Audio/OGG/footstep" + i.ToString("00") + ".ogg";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }

            if (clips.Count == 0)
            {
                Debug.LogWarning("Village Builder: no footstep clips found under Assets/Audio/OGG.");
            }

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.ConfigureFootsteps(source, clips.ToArray());
            }
        }

        private static GameObject[] LoadDefaultNatureProps()
        {
            List<GameObject> defaultProps = new List<GameObject>();
            string folder = "Assets/Art/Models/nature/FBX (Unity)";
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!ContainsAny(name, "Tree", "Pine", "Bush", "Clover", "Fern", "Flower",
                    "Grass", "Mushroom", "Pebble", "Petal", "Plant", "Rock"))
                {
                    continue;
                }

                GameObject prop = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prop != null)
                {
                    defaultProps.Add(prop);
                }
            }

            defaultProps.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            return defaultProps.ToArray();
        }

        private void LoadDefaultHousePieces(bool overwrite = false)
        {
            if (overwrite || wallStraight == null)
            {
                wallStraight = LoadHouseModel("Wall_Plaster_Straight.fbx");
            }

            if (overwrite || wallDoor == null)
            {
                wallDoor = LoadHouseModel("Wall_Plaster_Door_Round.fbx");
            }

            if (overwrite || wallWindow == null)
            {
                wallWindow = LoadHouseModel("Wall_Plaster_Window_Wide_Flat.fbx");
            }

            if (overwrite || floorTile == null)
            {
                floorTile = LoadHouseModel("Floor_Brick.fbx");
            }

            if (overwrite || roofCover == null)
            {
                roofCover = LoadHouseModel("Roof_RoundTiles_4x4.fbx");
            }

        }

        private static GameObject[] LoadDefaultInteriorFurniture()
        {
            string[] modelNames =
            {
                "Bed_Twin1.fbx",
                "Table_Large.fbx",
                "Chair_1.fbx",
                "Chest_Wood.fbx",
                "Bookcase_2.fbx",
                "Barrel.fbx",
                "Stool.fbx",
                "CandleStick_Stand.fbx"
            };
            List<GameObject> furniture = new List<GameObject>();
            foreach (string modelName in modelNames)
            {
                string path = "Assets/Art/Furniture/FBX/" + modelName;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                {
                    furniture.Add(model);
                }
                else
                {
                    Debug.LogWarning("Village Builder: interior furniture asset was not found at " + path);
                }
            }

            return furniture.ToArray();
        }

        private static GameObject LoadHouseModel(string fileName)
        {
            string path = "Assets/Art/Models/Medieval Village/FBX/" + fileName;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                Debug.LogWarning("Village Builder: default house piece was not found at " + path);
            }

            return model;
        }

        private static Transform NewGroup(Transform parent, string name)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        private static void DeleteGenerated()
        {
            GameObject old = GameObject.Find(RootName);
            int guard = 0;
            while (old != null && guard++ < 10)
            {
                Undo.DestroyObjectImmediate(old);
                old = GameObject.Find(RootName);
            }
        }

        // ---------------------------------------------------------------- road

        private float GetRoadCenterZ(float x)
        {
            float t = Mathf.Clamp01((x + villageLength * 0.5f) / villageLength);
            int segments = Mathf.Max(1, roadBendCount + 1);
            float segmentPosition = t * segments;
            int segment = Mathf.Min(Mathf.FloorToInt(segmentPosition), segments - 1);
            float localT = segmentPosition - segment;
            float p0 = GetRoadWaypointZ(segment - 1);
            float p1 = GetRoadWaypointZ(segment);
            float p2 = GetRoadWaypointZ(segment + 1);
            float p3 = GetRoadWaypointZ(segment + 2);
            float t2 = localT * localT;
            float t3 = t2 * localT;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * localT
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private float GetRoadYaw(float x)
        {
            float t = Mathf.Clamp01((x + villageLength * 0.5f) / villageLength);
            int segments = Mathf.Max(1, roadBendCount + 1);
            float segmentPosition = t * segments;
            int segment = Mathf.Min(Mathf.FloorToInt(segmentPosition), segments - 1);
            float localT = segmentPosition - segment;
            float p0 = GetRoadWaypointZ(segment - 1);
            float p1 = GetRoadWaypointZ(segment);
            float p2 = GetRoadWaypointZ(segment + 1);
            float p3 = GetRoadWaypointZ(segment + 2);
            float t2 = localT * localT;
            float derivative = 0.5f * ((-p0 + p2)
                + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * localT
                + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2)
                * segments / villageLength;
            return -Mathf.Atan(derivative) * Mathf.Rad2Deg;
        }

        private float GetRoadWaypointZ(int index)
        {
            int segments = Mathf.Max(1, roadBendCount + 1);
            if (index <= 0 || index >= segments)
            {
                return 0f;
            }

            float variation = Mathf.PerlinNoise(
                index * 0.73f + seed * 0.013f,
                0.37f + seed * 0.007f);
            float direction = ((index + seed) & 1) == 0 ? -1f : 1f;
            return direction * roadBendAmount * Mathf.Lerp(0.72f, 1f, variation);
        }

        private static Vector3 GetRoadNormal(float roadYaw)
        {
            float radians = roadYaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        private void BuildRoad(Transform parent)
        {
            Bounds tileBounds = new Bounds(Vector3.zero, new Vector3(2f, 0.1f, roadWidth));
            bool hasRoadTile = roadTile != null
                && GetPrefabBounds(roadTile, out tileBounds)
                && tileBounds.size.x > 0.1f
                && tileBounds.size.z > 0.1f;
            if (!hasRoadTile)
            {
                tileBounds = new Bounds(Vector3.zero, new Vector3(2f, 0.1f, roadWidth));
            }

            float segmentLength = Mathf.Max(1f, tileBounds.size.x);
            int segmentCount = Mathf.CeilToInt(villageLength / (segmentLength * 0.7f));
            int rows = Mathf.Max(1, Mathf.CeilToInt(roadWidth / Mathf.Max(tileBounds.size.z, 0.1f)));
            if (segmentCount * rows > 3000)
            {
                Debug.LogWarning("Village Builder: winding road needs too many tiles; reduce village length or use a larger road tile.");
                return;
            }

            float rowWidth = roadWidth / rows;
            for (int segment = 0; segment < segmentCount; segment++)
            {
                float x = -villageLength * 0.5f + (segment + 0.5f) * segmentLength;
                float yaw = GetRoadYaw(x);
                Vector3 center = new Vector3(x, 0f, GetRoadCenterZ(x));
                Vector3 roadNormal = GetRoadNormal(yaw);
                for (int row = 0; row < rows; row++)
                {
                    Vector3 target = center + roadNormal * (-roadWidth * 0.5f + (row + 0.5f) * rowWidth);
                    if (hasRoadTile)
                    {
                        PlaceAligned(parent, roadTile, target, yaw, 1f);
                    }
                    else
                    {
                        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        slab.name = "Road_Segment";
                        slab.transform.SetParent(parent, false);
                        slab.transform.SetPositionAndRotation(target + Vector3.up * 0.05f,
                            Quaternion.Euler(0f, yaw, 0f));
                        slab.transform.localScale = new Vector3(segmentLength + 0.15f, 0.1f, rowWidth + 0.1f);
                        slab.GetComponent<Renderer>().sharedMaterial = roadMaterial != null
                            ? roadMaterial
                            : GetOrCreateMaterial("Village_Road", new Color(0.48f, 0.45f, 0.42f));
                    }
                }
            }

        }

        // -------------------------------------------------------------- houses

        private void BuildHouses(Transform parent, List<Rect> occupied, ref float maxAbsZ)
        {
            List<GameObject> valid = new List<GameObject>();
            foreach (GameObject h in houses)
            {
                if (h != null)
                {
                    valid.Add(h);
                }
            }

            if (!useHousePrefabs || valid.Count == 0)
            {
                if (wallStraight == null)
                {
                    Debug.LogWarning("Village Builder: no modular wall or house prefabs assigned; building ground and road only.");
                    return;
                }

                BuildModularHouses(parent, occupied, ref maxAbsZ);
                return;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                float cursor = -villageLength * 0.5f;
                int guard = 0;

                while (guard++ < 200)
                {
                    GameObject prefab = valid[rng.Next(valid.Count)];
                    Bounds pb;
                    if (!GetPrefabBounds(prefab, out pb))
                    {
                        break;
                    }

                    float sx = pb.size.x;
                    float sz = pb.size.z;
                    if (sx < 0.1f || cursor + sx > villageLength * 0.5f)
                    {
                        break;
                    }

                    if (rng.NextDouble() < emptyChance)
                    {
                        cursor += sx;
                        continue;
                    }

                    float cx = cursor + sx * 0.5f;
                    float roadYaw = GetRoadYaw(cx);
                    Vector3 normal = GetRoadNormal(roadYaw);
                    float houseSideOffset = roadWidth * 0.5f + setback + sz * 0.5f;
                    Vector3 center = new Vector3(cx, 0f, GetRoadCenterZ(cx))
                        + normal * side * houseSideOffset;
                    float yaw = roadYaw + (side > 0 ? 180f : 0f) + Range(-yawJitter, yawJitter);
                    Rect footprint = GetFootprint(center, sx, sz, yaw);
                    if (OverlapsOccupied(footprint, occupied, gapMin))
                    {
                        cursor += sx + Range(gapMin, gapMax);
                        continue;
                    }

                    GameObject house = PlaceAligned(parent, prefab, center, yaw, 1f);
                    AddHouseCollider(house.transform);

                    occupied.Add(footprint);
                    maxAbsZ = Mathf.Max(maxAbsZ, Mathf.Abs(footprint.yMin), Mathf.Abs(footprint.yMax));
                    cursor += sx + Range(gapMin, gapMax);
                }
            }
        }

        private void BuildModularHouses(Transform parent, List<Rect> occupied, ref float maxAbsZ)
        {
            Bounds wallBounds;
            if (!GetPrefabBounds(wallStraight, out wallBounds))
            {
                Debug.LogWarning("Village Builder: couldn't measure the default wall prefab; no houses were generated.");
                return;
            }

            float cell = Mathf.Max(wallBounds.size.x, wallBounds.size.z);
            float wallHeight = wallBounds.size.y;
            if (cell < 0.1f || wallHeight < 0.1f)
            {
                Debug.LogWarning("Village Builder: default wall prefab has invalid dimensions; no houses were generated.");
                return;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                float cursor = -villageLength * 0.5f + cell;
                int guard = 0;
                while (cursor < villageLength * 0.5f - cell && guard++ < 100)
                {
                    int widthCells = rng.Next(3, 8);
                    int depthCells = rng.Next(3, 7);
                    float houseWidth = widthCells * cell;
                    float houseDepth = depthCells * cell;
                    if (cursor + houseWidth > villageLength * 0.5f)
                    {
                        break;
                    }

                    if (rng.NextDouble() < emptyChance)
                    {
                        cursor += houseWidth + Range(gapMin, gapMax);
                        continue;
                    }

                    float centerX = cursor + houseWidth * 0.5f;
                    float roadYaw = GetRoadYaw(centerX);
                    Vector3 normal = GetRoadNormal(roadYaw);
                    Vector3 center = new Vector3(centerX, 0f, GetRoadCenterZ(centerX))
                        + normal * side * (roadWidth * 0.5f + setback + houseDepth * 0.5f);
                    float yaw = roadYaw + (side > 0 ? 180f : 0f) + Range(-yawJitter, yawJitter);
                    Rect footprint = GetFootprint(
                        center,
                        houseWidth + HouseRoofOverhang * 2f,
                        houseDepth + HouseRoofOverhang * 2f,
                        yaw);
                    if (OverlapsOccupied(footprint, occupied, gapMin))
                    {
                        cursor += houseWidth + Range(gapMin, gapMax);
                        continue;
                    }

                    int floorsCount = rng.Next(1, 4);
                    float roofPitch = Range(25f, 42f);

                    GameObject house = BuildModularHouse(
                        parent, widthCells, depthCells, floorsCount, cell, wallHeight, roofPitch);
                    house.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
                    AddHouseCollider(house.transform);

                    occupied.Add(footprint);
                    maxAbsZ = Mathf.Max(maxAbsZ, Mathf.Abs(footprint.yMin), Mathf.Abs(footprint.yMax));
                    cursor += houseWidth + Range(gapMin, gapMax);
                }
            }
        }

        private GameObject BuildModularHouse(
            Transform parent,
            int widthCells,
            int depthCells,
            int floorsCount,
            float cell,
            float wallHeight,
            float roofPitch)
        {
            GameObject house = new GameObject("House_Auto_" + widthCells + "x" + depthCells + "x" + floorsCount);
            Undo.RegisterCreatedObjectUndo(house, "Build Modular House");
            house.transform.SetParent(parent, false);

            float houseWidth = widthCells * cell;
            float houseDepth = depthCells * cell;
            bool hasStairs = floorsCount > 1;
            float stairWidth = Mathf.Min(1.15f, houseWidth * 0.35f);
            int stairCount = Mathf.Max(1, Mathf.CeilToInt(wallHeight / 0.22f));
            float stairRun = Mathf.Min(wallHeight * 1.6f, houseDepth - cell * 0.5f);
            float stairTread = hasStairs ? stairRun / stairCount : 0f;
            float stairStartZ = houseDepth * 0.5f - cell * 0.45f;
            float stairEndZ = stairStartZ - stairRun;
            for (int floor = 0; floor < floorsCount; floor++)
            {
                float y = floor * wallHeight;

                if (floorTile != null)
                {
                    Bounds floorBounds;
                    if (GetPrefabBounds(floorTile, out floorBounds))
                    {
                        int floorColumns = Mathf.Max(1, Mathf.CeilToInt(houseWidth / Mathf.Max(floorBounds.size.x, 0.1f)));
                        int floorRows = Mathf.Max(1, Mathf.CeilToInt(houseDepth / Mathf.Max(floorBounds.size.z, 0.1f)));
                        for (int x = 0; x < floorColumns; x++)
                        {
                            for (int z = 0; z < floorRows; z++)
                            {
                                Vector3 position = new Vector3(
                                    -houseWidth * 0.5f + (x + 0.5f) * houseWidth / floorColumns,
                                    y,
                                    -houseDepth * 0.5f + (z + 0.5f) * houseDepth / floorRows);
                                if (hasStairs && floor == 1
                                    && Mathf.Abs(position.x) < stairWidth * 0.5f + floorBounds.size.x * 0.5f
                                    && position.z <= stairStartZ + floorBounds.size.z * 0.5f
                                    && position.z >= stairEndZ - floorBounds.size.z * 0.5f)
                                {
                                    continue;
                                }

                                PlaceHousePart(house.transform, floorTile, position, 0f);
                            }
                        }
                    }
                }

                for (int x = 0; x < widthCells; x++)
                {
                    float localX = -houseWidth * 0.5f + (x + 0.5f) * cell;
                    bool doorSlot = floor == 0 && x == widthCells / 2;
                    GameObject frontPiece = doorSlot && wallDoor != null
                        ? wallDoor
                        : PickWallPiece();
                    PlaceHousePart(house.transform, frontPiece, new Vector3(localX, y, houseDepth * 0.5f), 0f);
                    PlaceHousePart(house.transform, PickWallPiece(), new Vector3(localX, y, -houseDepth * 0.5f), 180f);
                }

                for (int z = 0; z < depthCells; z++)
                {
                    float localZ = -houseDepth * 0.5f + (z + 0.5f) * cell;
                    PlaceHousePart(house.transform, PickWallPiece(), new Vector3(houseWidth * 0.5f, y, localZ), 90f);
                    PlaceHousePart(house.transform, PickWallPiece(), new Vector3(-houseWidth * 0.5f, y, localZ), 270f);
                }

                BuildHouseInteriorFloor(house.transform, floor, y, houseWidth, houseDepth);
            }

            if (hasStairs)
            {
                BuildInteriorStairs(house.transform, stairCount, stairWidth, stairTread, wallHeight, stairStartZ);
            }

            PlaceFullRoof(house.transform, floorsCount * wallHeight, houseWidth, houseDepth, roofPitch);

            return house;
        }

        private static void BuildInteriorStairs(
            Transform parent,
            int stairCount,
            float stairWidth,
            float stairTread,
            float floorHeight,
            float startZ)
        {
            float run = stairTread * stairCount;
            float angle = Mathf.Atan2(floorHeight, run) * Mathf.Rad2Deg;
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(ramp, "Build House Stair Ramp");
            ramp.name = "Stair_Ramp_Collider";
            ramp.transform.SetParent(parent, false);
            ramp.transform.localPosition = new Vector3(0f, floorHeight * 0.5f, startZ - run * 0.5f);
            ramp.transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
            ramp.transform.localScale = new Vector3(stairWidth, 0.14f, run + 0.04f);
            Renderer rampRenderer = ramp.GetComponent<Renderer>();
            Material stairMaterial = GetPrefabMaterial(parent, "Floor_Brick")
                ?? GetOrCreateMaterial("Village_Stair", new Color(0.42f, 0.3f, 0.22f));
            rampRenderer.sharedMaterial = stairMaterial;

            for (int i = 0; i < stairCount; i++)
            {
                float stepTop = floorHeight * (i + 1f) / stairCount;
                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(step, "Build House Stairs");
                step.name = "Stair_Step_" + (i + 1);
                step.transform.SetParent(parent, false);
                step.transform.localPosition = new Vector3(
                    0f,
                    stepTop - 0.035f,
                    startZ - (i + 0.5f) * stairTread);
                step.transform.localScale = new Vector3(
                    stairWidth,
                    0.07f,
                    stairTread + 0.015f);
                step.GetComponent<Renderer>().sharedMaterial = stairMaterial;
                Collider stepCollider = step.GetComponent<Collider>();
                if (stepCollider != null)
                {
                    Undo.DestroyObjectImmediate(stepCollider);
                }
            }
        }

        private static Material GetPrefabMaterial(Transform parent, string prefabNamePart)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.IndexOf(prefabNamePart, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                Renderer renderer = child.GetComponentInChildren<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    return renderer.sharedMaterial;
                }
            }

            return null;
        }

        private GameObject PickWallPiece()
        {
            if (wallWindow != null && rng.NextDouble() < windowChance)
            {
                return wallWindow;
            }

            return wallStraight;
        }

        private static void PlaceHousePart(Transform parent, GameObject prefab, Vector3 localPosition, float yaw)
        {
            if (prefab == null)
            {
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Quaternion prefabRotation = instance.transform.localRotation;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * prefabRotation;
            Undo.RegisterCreatedObjectUndo(instance, "Build Modular House");
        }

        private void BuildHouseInteriorFloor(
            Transform parent, int floor, float floorY, float houseWidth, float houseDepth)
        {
            if (interiorFurniture == null || interiorFurniture.Length == 0)
            {
                return;
            }

            GameObject bed = FindInteriorFurniture("Bed_Twin1");
            GameObject table = FindInteriorFurniture("Table_Large");
            GameObject chair = FindInteriorFurniture("Chair_1");
            GameObject chest = FindInteriorFurniture("Chest_Wood");
            GameObject shelf = FindInteriorFurniture("Bookcase_2");
            GameObject barrel = FindInteriorFurniture("Barrel");
            GameObject stool = FindInteriorFurniture("Stool");
            GameObject candle = FindInteriorFurniture("CandleStick_Stand");
            float sideX = houseWidth * 0.28f;
            float sideZ = houseDepth * 0.28f;

            if ((floor == 0 || rng.NextDouble() < 0.45d) && bed != null)
            {
                PlaceHouseFurniture(parent, bed, new Vector3(-sideX, floorY, -sideZ), 0f, 0.9f);
            }

            if (table != null)
            {
                PlaceHouseFurniture(parent, table, new Vector3(sideX, floorY, sideZ), 0f, 0.9f);
                if (candle != null)
                {
                    PlaceHouseFurniture(parent, candle,
                        new Vector3(sideX, floorY + 0.8f, sideZ), 0f, 0.8f);
                }
            }

            GameObject seat = rng.NextDouble() < 0.55d ? chair : stool;
            if (seat != null)
            {
                PlaceHouseFurniture(parent, seat,
                    new Vector3(sideX, floorY, sideZ - 1.2f), 180f, 0.85f);
            }

            GameObject storage = rng.NextDouble() < 0.5d ? chest : barrel;
            if (storage != null)
            {
                PlaceHouseFurniture(parent, storage,
                    new Vector3(-sideX, floorY, sideZ), 90f, 0.85f);
            }

            if (shelf != null && (floor > 0 || rng.NextDouble() < 0.5d))
            {
                PlaceHouseFurniture(parent, shelf,
                    new Vector3(houseWidth * 0.2f, floorY, -sideZ), 180f, 0.85f);
            }
        }

        private GameObject FindInteriorFurniture(string nameContains)
        {
            foreach (GameObject furniture in interiorFurniture)
            {
                if (furniture != null
                    && furniture.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return furniture;
                }
            }

            return null;
        }

        private void PlaceHouseFurniture(
            Transform parent, GameObject prefab, Vector3 localPosition, float yaw, float scale)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null)
            {
                Debug.LogError("Village Builder: failed to place interior furniture " + prefab.name + ".");
                return;
            }

            Quaternion prefabRotation = instance.transform.localRotation;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * prefabRotation;
            instance.transform.localScale *= scale;
            Undo.RegisterCreatedObjectUndo(instance, "Build House Interior");

            Bounds bounds;
            if (TryGetInstanceBounds(instance, out bounds))
            {
                Vector3 target = parent.TransformPoint(localPosition);
                instance.transform.position += new Vector3(
                    target.x - bounds.center.x,
                    parent.TransformPoint(new Vector3(0f, localPosition.y, 0f)).y - bounds.min.y,
                    target.z - bounds.center.z);
            }
        }

        private void PlaceFullRoof(
            Transform parent, float roofBaseY, float houseWidth, float houseDepth, float pitch)
        {
            float overhang = HouseRoofOverhang;
            float halfDepth = houseDepth * 0.5f + overhang;
            float ridgeRise = halfDepth * Mathf.Tan(pitch * Mathf.Deg2Rad);
            float slopeLength = Mathf.Sqrt(halfDepth * halfDepth + ridgeRise * ridgeRise);
            Material roofMaterial = GetRoofMaterial();

            float roofWidth = houseWidth + overhang * 2f;
            float roofCenterY = roofBaseY + ridgeRise * 0.5f;
            float roofCenterZ = halfDepth * 0.5f;
            CreateRoofSlope(parent, roofMaterial, roofWidth, slopeLength + 0.3f,
                roofCenterY, roofCenterZ, pitch);
            CreateRoofSlope(parent, roofMaterial, roofWidth, slopeLength + 0.3f,
                roofCenterY, -roofCenterZ, 180f - pitch);
        }

        private static void CreateRoofSlope(
            Transform parent,
            Material material,
            float width,
            float slopeLength,
            float centerY,
            float centerZ,
            float rotationX)
        {
            GameObject slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(slope, "Build Modular House Roof");
            slope.name = "Roof_Slope";
            slope.transform.SetParent(parent, false);
            slope.transform.localPosition = new Vector3(0f, centerY, centerZ);
            slope.transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
            slope.transform.localScale = new Vector3(width, 0.2f, slopeLength);
            slope.GetComponent<Renderer>().sharedMaterial = material;
        }

        private Material GetRoofMaterial()
        {
            if (roofCover != null)
            {
                Renderer roofRenderer = roofCover.GetComponentInChildren<Renderer>();
                if (roofRenderer != null && roofRenderer.sharedMaterial != null)
                {
                    return roofRenderer.sharedMaterial;
                }
            }

            return GetOrCreateMaterial("Village_Roof", new Color(0.48f, 0.16f, 0.09f));
        }

        private bool AddHouseCollider(Transform houseRoot)
        {
            BoxCollider rootCollider = houseRoot.GetComponent<BoxCollider>();
            if (rootCollider != null)
            {
                Undo.DestroyObjectImmediate(rootCollider);
            }

            bool addedCollider = false;
            MeshFilter[] meshFilters = houseRoot.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh == null)
                {
                    continue;
                }

                Collider existingCollider = meshFilter.GetComponent<Collider>();
                if (existingCollider != null && !(existingCollider is MeshCollider))
                {
                    existingCollider.enabled = true;
                    existingCollider.isTrigger = false;
                    addedCollider = true;
                    continue;
                }

                MeshCollider collider = meshFilter.GetComponent<MeshCollider>();
                if (collider == null)
                {
                    collider = Undo.AddComponent<MeshCollider>(meshFilter.gameObject);
                }

                collider.sharedMesh = meshFilter.sharedMesh;
                collider.convex = false;
                collider.isTrigger = false;
                collider.enabled = true;
                addedCollider = true;
            }

            if (!addedCollider)
            {
                Debug.LogWarning("Village Builder: house '" + houseRoot.name
                                 + "' has no mesh to collide with; no house collider was added.");
            }

            return addedCollider;
        }

        private static Rect GetFootprint(Vector3 center, float width, float depth, float yaw)
        {
            float radians = yaw * Mathf.Deg2Rad;
            float extentX = Mathf.Abs(Mathf.Cos(radians)) * width * 0.5f
                + Mathf.Abs(Mathf.Sin(radians)) * depth * 0.5f;
            float extentZ = Mathf.Abs(Mathf.Sin(radians)) * width * 0.5f
                + Mathf.Abs(Mathf.Cos(radians)) * depth * 0.5f;
            return new Rect(center.x - extentX, center.z - extentZ, extentX * 2f, extentZ * 2f);
        }

        private static bool OverlapsOccupied(Rect footprint, List<Rect> occupied, float clearance)
        {
            foreach (Rect other in occupied)
            {
                if (footprint.xMin < other.xMax + clearance
                    && footprint.xMax > other.xMin - clearance
                    && footprint.yMin < other.yMax + clearance
                    && footprint.yMax > other.yMin - clearance)
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<GameObject> FindHouseRoots(Transform generatedHousesRoot)
        {
            HashSet<GameObject> houseRoots = new HashSet<GameObject>();

            for (int i = 0; i < generatedHousesRoot.childCount; i++)
            {
                houseRoots.Add(generatedHousesRoot.GetChild(i).gameObject);
            }

            GameObject[] selectedObjects = Selection.gameObjects;
            HashSet<GameObject> selectedSet = new HashSet<GameObject>(selectedObjects);
            foreach (GameObject selected in selectedObjects)
            {
                if (selected == null || !selected.scene.IsValid() || !selected.scene.isLoaded)
                {
                    continue;
                }

                Transform parent = selected.transform.parent;
                bool hasSelectedParent = false;
                while (parent != null)
                {
                    if (selectedSet.Contains(parent.gameObject))
                    {
                        hasSelectedParent = true;
                        break;
                    }

                    parent = parent.parent;
                }

                if (!hasSelectedParent && selected.GetComponentInChildren<Renderer>(true) != null)
                {
                    houseRoots.Add(selected);
                }
            }

            Renderer[] renderers = FindObjectsByType<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded)
                {
                    continue;
                }

                GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);
                if (instanceRoot != null && IsHouseObject(instanceRoot))
                {
                    houseRoots.Add(instanceRoot);
                    continue;
                }

                Transform current = renderer.transform;
                while (current != null && current != generatedHousesRoot)
                {
                    if (IsHouseObject(current.gameObject))
                    {
                        houseRoots.Add(current.gameObject);
                        break;
                    }

                    current = current.parent;
                }
            }

            return houseRoots;
        }

        private static bool IsHouseObject(GameObject candidate)
        {
            if (candidate == null || candidate.name.IndexOf("house", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return candidate.GetComponentInChildren<Renderer>(true) != null;
        }

        private static bool TryGetLocalRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool foundRenderer = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Bounds rendererBounds = renderer.localBounds;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 localCorner = rendererBounds.center + Vector3.Scale(
                                rendererBounds.extents, new Vector3(x, y, z));
                            Vector3 houseLocalCorner = root.InverseTransformPoint(
                                renderer.transform.TransformPoint(localCorner));
                            if (!foundRenderer)
                            {
                                bounds = new Bounds(houseLocalCorner, Vector3.zero);
                                foundRenderer = true;
                            }
                            else
                            {
                                bounds.Encapsulate(houseLocalCorner);
                            }
                        }
                    }
                }
            }

            return foundRenderer;
        }

        private bool TryGetPrefabLocalRendererBounds(GameObject prefab, out Bounds bounds)
        {
            GameObject temporary = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (temporary == null)
            {
                bounds = new Bounds();
                return false;
            }

            bool foundRenderer = TryGetLocalRendererBounds(temporary.transform, out bounds);
            DestroyImmediate(temporary);
            return foundRenderer;
        }

        // -------------------------------------------------------------- ground

        private Vector2 BuildGround(Transform parent, float maxAbsZ, List<Rect> occupied)
        {
            float gx = villageLength + groundMargin * 2f;
            float gz = Mathf.Max(villageDepth, (maxAbsZ + groundMargin) * 2f);
            int xSegments = Mathf.Max(2, Mathf.CeilToInt(gx / 2f));
            int zSegments = Mathf.Max(2, Mathf.CeilToInt(gz / 2f));
            Vector3[] vertices = new Vector3[(xSegments + 1) * (zSegments + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[xSegments * zSegments * 6];

            for (int z = 0; z <= zSegments; z++)
            {
                float worldZ = Mathf.Lerp(-gz * 0.5f, gz * 0.5f, (float)z / zSegments);
                for (int x = 0; x <= xSegments; x++)
                {
                    float worldX = Mathf.Lerp(-gx * 0.5f, gx * 0.5f, (float)x / xSegments);
                    int index = z * (xSegments + 1) + x;
                    vertices[index] = new Vector3(worldX, GetGroundHeight(worldX, worldZ, occupied) - 0.12f, worldZ);
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

            Mesh mesh = new Mesh { name = "Village_Rolling_Ground" };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            ground.transform.SetParent(parent, false);
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            ground.GetComponent<MeshCollider>().sharedMesh = mesh;
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial != null
                ? groundMaterial
                : GetOrCreateMaterial("Village_Grass", new Color(0.25f, 0.34f, 0.19f));

            return new Vector2(gx, gz);
        }

        private float GetGroundHeight(float x, float z, List<Rect> occupied)
        {
            float noise = Mathf.PerlinNoise(
                (x + seed * 17.31f) * 0.035f,
                (z - seed * 9.17f) * 0.035f);
            float rolling = (noise - 0.5f) * 1.8f
                + Mathf.Sin((x + z * 0.6f + seed) * 0.045f) * 0.22f;

            float flatness = Mathf.SmoothStep(
                0f, 1f,
                (Mathf.Abs(z - GetRoadCenterZ(x)) - roadWidth * 0.5f - 1f) / 7f);
            foreach (Rect bounds in occupied)
            {
                float distanceX = Mathf.Max(bounds.xMin - x, 0f, x - bounds.xMax);
                float distanceZ = Mathf.Max(bounds.yMin - z, 0f, z - bounds.yMax);
                float distance = Mathf.Sqrt(distanceX * distanceX + distanceZ * distanceZ);
                flatness = Mathf.Min(flatness, Mathf.SmoothStep(0f, 1f, (distance - 1f) / 6f));
            }

            return rolling * flatness;
        }

        // --------------------------------------------------------------- props

        private void BuildProps(Transform parent, List<Rect> occupied, float gx, float gz)
        {
            List<GameObject> valid = new List<GameObject>();
            foreach (GameObject p in props)
            {
                if (p != null)
                {
                    valid.Add(p);
                }
            }

            if (valid.Count == 0 || propCount <= 0)
            {
                return;
            }

            float halfX = gx * 0.5f - 2f;
            float halfZ = gz * 0.5f - 2f;
            int placed = 0;
            int tries = propCount * 50;

            while (placed < propCount && tries-- > 0)
            {
                float x = Range(-halfX, halfX);
                float z = Range(-halfZ, halfZ);

                if (Mathf.Abs(x) <= villageLength * 0.5f + propClearance
                    && Mathf.Abs(z - GetRoadCenterZ(x)) < roadWidth * 0.5f + propClearance)
                {
                    continue;
                }

                bool blocked = false;
                foreach (Rect r in occupied)
                {
                    if (x > r.xMin - propClearance && x < r.xMax + propClearance
                        && z > r.yMin - propClearance && z < r.yMax + propClearance)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (blocked)
                {
                    continue;
                }

                GameObject prefab = PickDiverseProp(valid);
                float s = GetPropScale(prefab);
                float groundHeight = GetGroundHeight(x, z, occupied);
                GameObject instance = PlaceAligned(
                    parent, prefab, new Vector3(x, groundHeight - 0.12f, z), Range(0f, 360f), s);
                AddPropCollider(instance);
                placed++;
            }
        }

        private GameObject PickDiverseProp(List<GameObject> valid)
        {
            List<GameObject> trees = new List<GameObject>();
            List<GameObject> bushes = new List<GameObject>();
            List<GameObject> rocks = new List<GameObject>();
            List<GameObject> groundCover = new List<GameObject>();

            foreach (GameObject prefab in valid)
            {
                string name = prefab.name;
                if (ContainsAny(name, "Tree", "Pine"))
                {
                    trees.Add(prefab);
                }
                else if (ContainsAny(name, "Bush"))
                {
                    bushes.Add(prefab);
                }
                else if (ContainsAny(name, "Rock", "Pebble"))
                {
                    rocks.Add(prefab);
                }
                else
                {
                    groundCover.Add(prefab);
                }
            }

            float roll = (float)rng.NextDouble();
            List<GameObject> category = roll < 0.3f ? trees
                : roll < 0.52f ? bushes
                : roll < 0.68f ? rocks
                : groundCover;

            if (category.Count == 0)
            {
                category = valid;
            }

            return category[rng.Next(category.Count)];
        }

        private float GetPropScale(GameObject prefab)
        {
            float scale = Range(propScaleMin, propScaleMax);
            string name = prefab.name;
            if (ContainsAny(name, "Grass", "Flower", "Clover", "Fern", "Mushroom", "Petal"))
            {
                scale *= 0.55f;
            }
            else if (ContainsAny(name, "Pebble", "RockPath"))
            {
                scale *= 0.65f;
            }

            return scale;
        }

        private static bool ContainsAny(string value, params string[] parts)
        {
            foreach (string part in parts)
            {
                if (value.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddPropCollider(GameObject prop)
        {
            Collider[] existingColliders = prop.GetComponentsInChildren<Collider>(true);
            foreach (Collider existingCollider in existingColliders)
            {
                existingCollider.enabled = true;
                existingCollider.isTrigger = false;
            }

            Bounds bounds;
            bool hasBounds = TryGetLocalRendererBounds(prop.transform, out bounds);
            if (ContainsAny(prop.name, "Tree", "Pine"))
            {
                if (!hasBounds)
                {
                    Debug.LogWarning("Village Builder: couldn't add tree collision to '" + prop.name
                                     + "' because it has no renderer.");
                    return;
                }

                CapsuleCollider treeCollider = prop.GetComponent<CapsuleCollider>();
                if (treeCollider == null)
                {
                    treeCollider = Undo.AddComponent<CapsuleCollider>(prop);
                }

                treeCollider.enabled = true;
                treeCollider.isTrigger = false;
                treeCollider.direction = 1;
                treeCollider.center = bounds.center;
                treeCollider.radius = Mathf.Clamp(
                    Mathf.Min(bounds.size.x, bounds.size.z) * 0.32f,
                    0.4f,
                    1.2f);
                treeCollider.height = Mathf.Max(bounds.size.y, treeCollider.radius * 2f);
                return;
            }

            if (existingColliders.Length > 0)
            {
                return;
            }

            if (!hasBounds)
            {
                Debug.LogWarning("Village Builder: couldn't add collision to prop '" + prop.name + "' because it has no renderer.");
                return;
            }

            if (ContainsAny(prop.name, "Tree", "Pine") && bounds.size.y > 2f)
            {
                CapsuleCollider collider = Undo.AddComponent<CapsuleCollider>(prop);
                collider.direction = 1;
                collider.center = bounds.center;
                collider.height = Mathf.Max(bounds.size.y, 0.5f);
                collider.radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.12f, 0.2f, 0.65f);
            }
            else
            {
                BoxCollider collider = Undo.AddComponent<BoxCollider>(prop);
                collider.center = bounds.center;
                collider.size = bounds.size;
            }
        }

        // ------------------------------------------------------------- helpers

        private float Range(float a, float b)
        {
            return a + (float)rng.NextDouble() * (b - a);
        }

        /// <summary>
        /// Instantiates a prefab keeping its built-in root rotation (FBX models are often baked
        /// with -90 X), adds the extra yaw, then aligns the measured bounds so the piece sits
        /// centred on target.x/z with its lowest point at target.y. Works with any pivot.
        /// </summary>
        private static GameObject PlaceAligned(Transform parent, GameObject prefab, Vector3 target, float yaw, float scaleMul)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Quaternion baseRot = go.transform.rotation;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f) * baseRot);

            if (!Mathf.Approximately(scaleMul, 1f))
            {
                go.transform.localScale *= scaleMul;
            }

            Bounds b;
            if (TryGetInstanceBounds(go, out b))
            {
                go.transform.position += new Vector3(
                    target.x - b.center.x,
                    target.y - b.min.y,
                    target.z - b.center.z);
            }

            return go;
        }

        private bool GetPrefabBounds(GameObject prefab, out Bounds bounds)
        {
            if (boundsCache.TryGetValue(prefab, out bounds))
            {
                return true;
            }

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (inst == null)
            {
                bounds = new Bounds(Vector3.zero, Vector3.zero);
                return false;
            }

            inst.transform.position = Vector3.zero;
            bool ok = TryGetInstanceBounds(inst, out bounds);
            DestroyImmediate(inst);

            if (ok)
            {
                boundsCache[prefab] = bounds;
            }

            return ok;
        }

        private static bool TryGetInstanceBounds(GameObject inst, out Bounds bounds)
        {
            bounds = new Bounds(inst.transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
            {
                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return found;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = "Assets/_Project/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            System.IO.Directory.CreateDirectory(Application.dataPath + "/_Project/Materials");
            AssetDatabase.Refresh();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
#endif