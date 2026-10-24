#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using JRPG.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace JRPG.EditorTools
{
    public sealed class MainMenuSceneBuilderWindow : EditorWindow
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string MenuRootName = "MainMenu_Generated";
        private const int Seed = 471103;
        private Texture2D cursorTexture;

        [MenuItem("Tools/Lentera/Main Menu Scene Builder")]
        private static void Open()
        {
            GetWindow<MainMenuSceneBuilderWindow>("Main Menu Builder");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Live Horror Main Menu", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates a dusk forest diorama, atmospheric post-processing, menu UI, audio and transitions. " +
                "Rebuilding replaces only the generated menu scene and keeps it first in Build Settings.",
                MessageType.Info);
            cursorTexture = (Texture2D)EditorGUILayout.ObjectField(
                "Optional cursor", cursorTexture, typeof(Texture2D), false);
            GUILayout.Space(8f);
            if (GUILayout.Button("Build and Save Main Menu Scene", GUILayout.Height(36f)))
            {
                BuildMainMenuScene(cursorTexture);
            }
        }

        public static void BuildMainMenuScene()
        {
            BuildMainMenuScene(null);
        }

        public static void BuildMainMenuScene(Texture2D cursor)
        {
            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder("Assets/_Project/Materials");
            EnsureFolder("Assets/_Project/Textures");
            EnsureFolder("Assets/_Project/Settings");

            Texture2D fogTexture = CreateFogTexture();
            Material groundMaterial = CreateMaterial(
                "Assets/_Project/Materials/Menu_Ground.mat", new Color(0.075f, 0.095f, 0.075f));
            Material logMaterial = CreateMaterial(
                "Assets/_Project/Materials/Menu_Log.mat", new Color(0.105f, 0.071f, 0.043f));
            Material stoneMaterial = CreateMaterial(
                "Assets/_Project/Materials/Menu_Stone.mat", new Color(0.12f, 0.14f, 0.15f));
            Material flameMaterial = CreateMaterial(
                "Assets/_Project/Materials/Menu_Flame.mat", new Color(1f, 0.27f, 0.045f), true);
            Material fogMaterial = CreateParticleMaterial(
                "Assets/_Project/Materials/Menu_Fog.mat", fogTexture, new Color(0.62f, 0.7f, 0.79f, 0.23f));
            Material moteMaterial = CreateParticleMaterial(
                "Assets/_Project/Materials/Menu_Motes.mat", fogTexture, Color.white);
            VolumeProfile profile = CreateVolumeProfile();
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject(MenuRootName);
            CreateEnvironment(root.transform, groundMaterial, logMaterial, stoneMaterial, flameMaterial, fogMaterial, moteMaterial);
            GameObject cameraObject = CreateCamera(root.transform);
            Transform lanternTarget = GameObject.Find("MenuLantern").transform;
            MenuCameraRig cameraRig = cameraObject.AddComponent<MenuCameraRig>();
            cameraRig.lookTarget = lanternTarget;

            CreateVolume(root.transform, profile, cameraObject.GetComponent<Camera>());
            CreateMenuAudio(root.transform);
            CreateUi(root.transform, cursor);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Main Menu Scene Builder: saved " + ScenePath + " and updated Build Settings.");
        }

        private static void CreateEnvironment(
            Transform parent,
            Material groundMaterial,
            Material logMaterial,
            Material stoneMaterial,
            Material flameMaterial,
            Material fogMaterial,
            Material moteMaterial)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.055f, 0.075f, 0.11f);
            RenderSettings.fogDensity = 0.025f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.105f, 0.125f, 0.17f);
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.1f;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Diorama_Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(0f, -0.08f, 7f);
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "Diorama_Edge";
            edge.transform.SetParent(parent, false);
            edge.transform.localPosition = new Vector3(0f, -0.5f, 7f);
            edge.transform.localScale = new Vector3(49.8f, 0.85f, 49.8f);
            edge.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            CreateMoonlight(parent);
            PlaceForestModels(parent);
            CreateVillageLights(parent, flameMaterial);
            CreateStumpAndLantern(parent, logMaterial, stoneMaterial, flameMaterial);
            CreateParticleSystem(parent, "GroundFog", fogMaterial, new Vector3(0f, 0.13f, 7f),
                new Vector3(23f, 0.35f, 25f), 5.2f, 0.35f, 1.8f, 3.2f, new Color(0.55f, 0.65f, 0.78f, 0.19f));
            CreateParticleSystem(parent, "Fireflies", moteMaterial, new Vector3(0f, 1.35f, 9f),
                new Vector3(18f, 3.5f, 23f), 6f, 0.05f, 1.3f, 0.7f, new Color(1f, 0.7f, 0.28f, 0.9f));
            CreateParticleSystem(parent, "LanternDust", moteMaterial, new Vector3(1.5f, 1.2f, 1.5f),
                new Vector3(2.7f, 2.3f, 2.2f), 4.4f, 0.025f, 2.1f, 4f, new Color(1f, 0.76f, 0.48f, 0.55f));
        }

        private static void CreateMoonlight(Transform parent)
        {
            GameObject moon = new GameObject("Cold_Moonlight");
            moon.transform.SetParent(parent, false);
            moon.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            Light light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.43f, 0.55f, 0.8f);
            light.intensity = 0.42f;
            light.shadows = LightShadows.Soft;
        }

        private static void PlaceForestModels(Transform parent)
        {
            GameObject[] trees =
            {
                FindModel("CommonTree_1"),
                FindModel("CommonTree_3"),
                FindModel("Pine_2"),
                FindModel("DeadTree_2"),
                FindModel("Pine_4")
            };
            GameObject[] foliage =
            {
                FindModel("Bush_Common"),
                FindModel("Fern_1"),
                FindModel("Grass_Common_Tall"),
                FindModel("Plant_1")
            };
            System.Random random = new System.Random(Seed);
            Transform treesRoot = new GameObject("Forest_Edge_Trees").transform;
            treesRoot.SetParent(parent, false);
            for (int i = 0; i < 31; i++)
            {
                GameObject source = trees[random.Next(trees.Length)];
                if (source == null)
                {
                    continue;
                }

                float side = (i % 2 == 0 ? -1f : 1f) * RandomRange(random, 5.7f, 13f);
                float depth = RandomRange(random, 4.5f, 24f);
                if (depth < 10f && Mathf.Abs(side) < 7f)
                {
                    side = Mathf.Sign(side) * 7.2f;
                }

                GameObject instance = InstantiateModel(source, treesRoot);
                instance.transform.localPosition = new Vector3(side, 0f, depth);
                instance.transform.localRotation = Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f);
                instance.transform.localScale = Vector3.one * RandomRange(random, 1.45f, 2.7f);
            }

            Transform foliageRoot = new GameObject("Forest_Edge_Undergrowth").transform;
            foliageRoot.SetParent(parent, false);
            for (int i = 0; i < 65; i++)
            {
                float side = (i % 2 == 0 ? -1f : 1f) * RandomRange(random, 3.8f, 12f);
                float depth = RandomRange(random, -0.2f, 20f);
                if (depth < 3.5f && Mathf.Abs(side) < 4.8f)
                {
                    side = Mathf.Sign(side) * 5.1f;
                }

                GameObject source = foliage[random.Next(foliage.Length)];
                if (source == null)
                {
                    continue;
                }

                GameObject instance = InstantiateModel(source, foliageRoot);
                instance.transform.localPosition = new Vector3(side, 0f, depth);
                instance.transform.localRotation = Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f);
                instance.transform.localScale = Vector3.one * RandomRange(random, 0.65f, 1.45f);
            }
        }

        private static void CreateVillageLights(Transform parent, Material glowMaterial)
        {
            GameObject village = new GameObject("Distant_Village_Lights");
            village.transform.SetParent(parent, false);
            Vector3[] positions =
            {
                new Vector3(-8.5f, 2.1f, 22f),
                new Vector3(-4.8f, 1.6f, 24f),
                new Vector3(7.7f, 2f, 21.5f),
                new Vector3(10.5f, 1.45f, 24f),
                new Vector3(3.8f, 1.75f, 25f)
            };
            foreach (Vector3 position in positions)
            {
                GameObject glow = new GameObject("Village_Lantern_Glow");
                glow.transform.SetParent(village.transform, false);
                glow.transform.localPosition = position;
                GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Distant_Warm_Glow";
                bulb.transform.SetParent(glow.transform, false);
                bulb.transform.localPosition = Vector3.zero;
                bulb.transform.localScale = Vector3.one * 0.18f;
                bulb.GetComponent<Renderer>().sharedMaterial = glowMaterial;
                Collider bulbCollider = bulb.GetComponent<Collider>();
                if (bulbCollider != null)
                {
                    UnityEngine.Object.DestroyImmediate(bulbCollider);
                }

                Light light = glow.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.51f, 0.21f);
                light.intensity = 1.6f;
                light.range = 4.5f;
                light.shadows = LightShadows.None;
            }
        }

        private static void CreateStumpAndLantern(
            Transform parent, Material logMaterial, Material stoneMaterial, Material flameMaterial)
        {
            GameObject stumpRoot = new GameObject("Lantern_Stump");
            stumpRoot.transform.SetParent(parent, false);
            stumpRoot.transform.localPosition = new Vector3(2f, 0f, 1.4f);
            GameObject stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stump.name = "Cut_Log";
            stump.transform.SetParent(stumpRoot.transform, false);
            stump.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            stump.transform.localScale = new Vector3(0.78f, 0.37f, 0.72f);
            stump.GetComponent<Renderer>().sharedMaterial = logMaterial;
            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "Fallen_Log";
            log.transform.SetParent(stumpRoot.transform, false);
            log.transform.localPosition = new Vector3(0.58f, 0.28f, -0.32f);
            log.transform.localRotation = Quaternion.Euler(3f, 0f, 78f);
            log.transform.localScale = new Vector3(0.34f, 0.95f, 0.34f);
            log.GetComponent<Renderer>().sharedMaterial = logMaterial;
            GameObject stones = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stones.name = "Stump_Root_Stone";
            stones.transform.SetParent(stumpRoot.transform, false);
            stones.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            stones.transform.localScale = new Vector3(1.02f, 0.11f, 0.93f);
            stones.GetComponent<Renderer>().sharedMaterial = stoneMaterial;

            GameObject lantern = new GameObject("MenuLantern");
            lantern.transform.SetParent(parent, false);
            lantern.transform.localPosition = new Vector3(2f, 0.84f, 1.4f);
            GameObject lanternModel = FindModel("Lantern");
            if (lanternModel != null)
            {
                GameObject model = InstantiateModel(lanternModel, lantern.transform);
                model.name = "Lantern_Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * 0.55f;
            }
            else
            {
                CreateProceduralLanternBody(lantern.transform, logMaterial, stoneMaterial);
            }

            GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Flame";
            flame.transform.SetParent(lantern.transform, false);
            flame.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            flame.transform.localScale = new Vector3(0.16f, 0.27f, 0.16f);
            flame.GetComponent<Renderer>().sharedMaterial = flameMaterial;
            Collider flameCollider = flame.GetComponent<Collider>();
            if (flameCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(flameCollider);
            }

            GameObject lightObject = new GameObject("Lantern_Warm_Light");
            lightObject.transform.SetParent(lantern.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.54f, 0.24f);
            light.intensity = 2.7f;
            light.range = 8.5f;
            light.shadows = LightShadows.None;
            LanternFlicker flicker = lightObject.AddComponent<LanternFlicker>();
            flicker.lanternLight = light;
            flicker.flameRenderer = flame.GetComponent<Renderer>();
        }

        private static void CreateProceduralLanternBody(Transform parent, Material body, Material glass)
        {
            GameObject baseCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseCylinder.name = "Lantern_Body";
            baseCylinder.transform.SetParent(parent, false);
            baseCylinder.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            baseCylinder.transform.localScale = new Vector3(0.26f, 0.34f, 0.26f);
            baseCylinder.GetComponent<Renderer>().sharedMaterial = body;

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Lantern_Cap";
            cap.transform.SetParent(parent, false);
            cap.transform.localPosition = new Vector3(0f, 0.79f, 0f);
            cap.transform.localScale = new Vector3(0.33f, 0.06f, 0.33f);
            cap.GetComponent<Renderer>().sharedMaterial = glass;
        }

        private static GameObject CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2.65f, -8.6f);
            cameraObject.transform.rotation = Quaternion.LookRotation(
                new Vector3(2f, 1.3f, 1.4f) - cameraObject.transform.position, Vector3.up);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 47f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.048f, 0.075f);
            camera.allowHDR = true;
            camera.allowMSAA = true;
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData additionalData =
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
            additionalData.renderPostProcessing = true;
            additionalData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            return cameraObject;
        }

        private static void CreateVolume(Transform parent, VolumeProfile profile, Camera camera)
        {
            GameObject volumeObject = new GameObject("Menu_Post_Processing");
            volumeObject.transform.SetParent(parent, false);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            UniversalAdditionalCameraData additionalData =
                camera.GetComponent<UniversalAdditionalCameraData>();
            additionalData.volumeLayerMask = ~0;
        }

        private static VolumeProfile CreateVolumeProfile()
        {
            const string path = "Assets/_Project/Settings/MainMenuVolumeProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            Bloom bloom = GetOrAdd< Bloom >(profile);
            bloom.active = true;
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.42f);
            bloom.scatter.Override(0.65f);

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            DepthOfField depthOfField = GetOrAdd<DepthOfField>(profile);
            depthOfField.active = true;
            depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
            depthOfField.focusDistance.Override(8f);
            depthOfField.focalLength.Override(42f);
            depthOfField.aperture.Override(5.6f);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.72f);
            vignette.color.Override(new Color(0.015f, 0.021f, 0.035f));

            FilmGrain grain = GetOrAdd<FilmGrain>(profile);
            grain.active = true;
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);

            ChromaticAberration chromatic = GetOrAdd<ChromaticAberration>(profile);
            chromatic.active = true;
            chromatic.intensity.Override(0.035f);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.postExposure.Override(-0.35f);
            color.saturation.Override(-18f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                return component;
            }

            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return component;
        }

        private static void CreateMenuAudio(Transform parent)
        {
            GameObject audioRoot = new GameObject("Menu_Audio");
            audioRoot.transform.SetParent(parent, false);
            MenuAudio menuAudio = audioRoot.AddComponent<MenuAudio>();
            menuAudio.musicClip = FindAudioClip("forestbgm");
            menuAudio.ambienceClip = FindAudioClip("forestambience", "forest_ambience", "windloop", "wind_loop");
            menuAudio.hoverClip = FindAudioClip("select_001", "bong_001", "metalClick", "hover");
            menuAudio.clickClip = FindAudioClip("confirmation_001", "click_001", "metalClick", "click");
            menuAudio.musicSource = CreateAudioSource(audioRoot.transform, "Menu_Music");
            menuAudio.ambienceSource = CreateAudioSource(audioRoot.transform, "Menu_Ambience");
            menuAudio.uiSource = CreateAudioSource(audioRoot.transform, "Menu_UI_Sounds");
        }

        private static AudioSource CreateAudioSource(Transform parent, string name)
        {
            GameObject sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(parent, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = true;
            return source;
        }

        private static void CreateUi(Transform parent, Texture2D cursor)
        {
            GameObject canvasObject = new GameObject("MainMenu_UI");
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.SetParent(parent, false);
            eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputModule =
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif

            TMP_FontAsset font = ResolveFont();
            if (font == null || !HasFontAtlas(font))
            {
                throw new InvalidOperationException(
                    "Main Menu Scene Builder: a valid TextMeshPro font atlas is required to build the UI.");
            }

            TextMeshProUGUI title = CreateText(
                canvasObject.transform, "Title", "LENTERA TERAKHIR", font, 57f,
                new Color(0.88f, 0.87f, 0.79f), TextAlignmentOptions.Left);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.075f, 0.755f);
            titleRect.anchorMax = new Vector2(0.66f, 0.89f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.sizeDelta = Vector2.zero;
            CanvasGroup titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;

            TextMeshProUGUI subtitle = CreateText(
                canvasObject.transform, "Subtitle", "THE LAST LIGHT FADES", font, 15f,
                new Color(0.62f, 0.68f, 0.7f), TextAlignmentOptions.Left);
            subtitle.characterSpacing = 3f;
            SetAnchoredRect(subtitle.rectTransform, new Vector2(0.078f, 0.735f), new Vector2(0.58f, 0.78f));

            Button start = CreateMenuButton(canvasObject.transform, "StartButton", "Mulai", font, 0.565f);
            Button proceed = CreateMenuButton(canvasObject.transform, "ContinueButton", "Lanjutkan", font, 0.47f);
            Button settings = CreateMenuButton(canvasObject.transform, "SettingsButton", "Pengaturan", font, 0.375f);
            Button quit = CreateMenuButton(canvasObject.transform, "QuitButton", "Keluar", font, 0.28f);

            GameObject settingsPanel = CreateSettingsPanel(canvasObject.transform, font);
            Button closeSettings = CreateCloseButton(settingsPanel.transform, font);
            Slider master = settingsPanel.transform.Find("MasterSlider")?.GetComponent<Slider>();
            Slider music = settingsPanel.transform.Find("MusicSlider")?.GetComponent<Slider>();
            Slider ambience = settingsPanel.transform.Find("AmbienceSlider")?.GetComponent<Slider>();

            GameObject faderObject = new GameObject("Scene_Fade");
            faderObject.transform.SetParent(canvasObject.transform, false);
            RectTransform faderRect = faderObject.AddComponent<RectTransform>();
            StretchToParent(faderRect);
            Image faderImage = faderObject.AddComponent<Image>();
            faderImage.color = Color.black;
            CanvasGroup fadeGroup = faderObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 1f;
            fadeGroup.blocksRaycasts = true;
            SceneFader fader = faderObject.AddComponent<SceneFader>();

            MainMenuController controller = canvasObject.AddComponent<MainMenuController>();
            controller.startButton = start;
            controller.continueButton = proceed;
            controller.settingsButton = settings;
            controller.quitButton = quit;
            controller.closeSettingsButton = closeSettings;
            controller.masterSlider = master;
            controller.musicSlider = music;
            controller.ambienceSlider = ambience;
            controller.settingsPanel = settingsPanel;
            controller.titleText = title;
            controller.titleGroup = titleGroup;
            controller.customCursor = cursor;
            controller.menuAudio = parent.GetComponentInChildren<MenuAudio>(true);
            controller.sceneFader = fader;
            fader.overlayGroup = fadeGroup;
        }

        private static GameObject CreateSettingsPanel(Transform parent, TMP_FontAsset font)
        {
            GameObject panel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.07f, 0.08f);
            rect.anchorMax = new Vector2(0.39f, 0.3f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.018f, 0.027f, 0.041f, 0.92f);
            CreateText(panel.transform, "SettingsTitle", "PENGATURAN", font, 18f,
                new Color(0.92f, 0.72f, 0.47f), TextAlignmentOptions.Left,
                new Vector2(0.06f, 0.72f), new Vector2(0.72f, 0.94f));
            CreateSliderRow(panel.transform, "MasterSlider", "Master", font, 0.57f);
            CreateSliderRow(panel.transform, "MusicSlider", "Musik", font, 0.36f);
            CreateSliderRow(panel.transform, "AmbienceSlider", "Suasana", font, 0.15f);
            return panel;
        }

        private static void CreateSliderRow(
            Transform parent, string name, string label, TMP_FontAsset font, float y)
        {
            CreateText(parent, name + "Label", label, font, 14f, Color.white, TextAlignmentOptions.Left,
                new Vector2(0.06f, y), new Vector2(0.37f, y + 0.17f));
            GameObject sliderObject = new GameObject(name, typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.41f, y + 0.04f);
            sliderRect.anchorMax = new Vector2(0.94f, y + 0.14f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;
            Slider slider = sliderObject.GetComponent<Slider>();
            Image background = CreateImage(sliderObject.transform, "Background", new Color(0.13f, 0.16f, 0.19f));
            StretchToParent(background.rectTransform);
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5f, 0f);
            fillAreaRect.offsetMax = new Vector2(-5f, 0f);
            Image fill = CreateImage(fillArea.transform, "Fill", new Color(0.87f, 0.55f, 0.3f));
            StretchToParent(fill.rectTransform);
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(5f, 0f);
            handleAreaRect.offsetMax = new Vector2(-5f, 0f);
            Image handle = CreateImage(handleArea.transform, "Handle", new Color(0.93f, 0.75f, 0.5f));
            handle.raycastTarget = true;
            RectTransform handleRect = handle.rectTransform;
            handleRect.sizeDelta = new Vector2(12f, 0f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.7f;
        }

        private static Button CreateCloseButton(Transform parent, TMP_FontAsset font)
        {
            Button button = CreateMenuButton(parent, "CloseSettingsButton", "Tutup", font, 0.04f);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.69f, 0.72f);
            rect.anchorMax = new Vector2(0.96f, 0.94f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return button;
        }

        private static Button CreateMenuButton(
            Transform parent, string name, string caption, TMP_FontAsset font, float y)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.075f, y);
            rect.anchorMax = new Vector2(0.36f, y + 0.07f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.012f, 0.018f, 0.028f, 0.12f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };

            TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", caption, font, 25f,
                new Color(0.79f, 0.82f, 0.8f), TextAlignmentOptions.Left);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0.15f);
            textRect.anchorMax = new Vector2(1f, 0.94f);
            textRect.offsetMin = new Vector2(13f, 0f);
            textRect.offsetMax = Vector2.zero;

            Image underline = CreateImage(buttonObject.transform, "Underline",
                new Color(1f, 0.64f, 0.34f, 0.95f));
            RectTransform lineRect = underline.rectTransform;
            lineRect.anchorMin = new Vector2(0f, 0.1f);
            lineRect.anchorMax = new Vector2(0f, 0.1f);
            lineRect.pivot = new Vector2(0f, 0.5f);
            lineRect.anchoredPosition = new Vector2(13f, 0f);
            lineRect.sizeDelta = new Vector2(0f, 2f);
            return button;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string value,
            TMP_FontAsset font,
            float size,
            Color color,
            TextAlignmentOptions alignment,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            if (anchorMin.HasValue && anchorMax.HasValue)
            {
                SetAnchoredRect(text.rectTransform, anchorMin.Value, anchorMax.Value);
            }
            return text;
        }

        private static void SetAnchoredRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TMP_FontAsset ResolveFont()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/Art/Main Menu/Font/MedievalSharp-Regular.ttf");
            const string generatedFontPath = "Assets/_Project/Settings/MedievalSharp-Regular.asset";
            TMP_FontAsset generatedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(generatedFontPath);
            if (sourceFont != null)
            {
                if (!HasFontAtlas(generatedFont))
                {
                    if (generatedFont != null)
                    {
                        AssetDatabase.DeleteAsset(generatedFontPath);
                    }

                    generatedFont = TMP_FontAsset.CreateFontAsset(sourceFont);
                    if (generatedFont == null)
                    {
                        Debug.LogError("Main Menu Scene Builder: could not generate a TextMeshPro font asset.");
                        return null;
                    }

                    AssetDatabase.CreateAsset(generatedFont, generatedFontPath);
                    foreach (Texture2D atlasTexture in generatedFont.atlasTextures)
                    {
                        if (atlasTexture != null)
                        {
                            AssetDatabase.AddObjectToAsset(atlasTexture, generatedFont);
                        }
                    }

                    if (generatedFont.material != null)
                    {
                        AssetDatabase.AddObjectToAsset(generatedFont.material, generatedFont);
                    }

                    EditorUtility.SetDirty(generatedFont);
                    AssetDatabase.SaveAssets();
                }

                return generatedFont;
            }

            TMP_FontAsset fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (HasFontAtlas(fallbackFont))
            {
                return fallbackFont;
            }

            string[] fontIds = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (string fontId in fontIds)
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    AssetDatabase.GUIDToAssetPath(fontId));
                if (HasFontAtlas(font))
                {
                    return font;
                }
            }

            Debug.LogError("Main Menu Scene Builder: no TextMeshPro font or source font was found.");
            return null;
        }

        private static bool HasFontAtlas(TMP_FontAsset font)
        {
            if (font == null || font.material == null || font.atlasTextures == null
                || font.atlasTextures.Length == 0)
            {
                return false;
            }

            foreach (Texture2D atlasTexture in font.atlasTextures)
            {
                if (atlasTexture == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static Texture2D CreateFogTexture()
        {
            const string path = "Assets/_Project/Textures/MenuGroundFog.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
            {
                return texture;
            }

            const int size = 128;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2.3f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            Texture2D generated = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            generated.SetPixels32(pixels);
            generated.Apply();
            File.WriteAllBytes(path, generated.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CreateMaterial(string path, Color color, bool emission = false)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (emission && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 3f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateParticleMaterial(string path, Texture2D texture, Color tint)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", tint);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateParticleSystem(
            Transform parent,
            string name,
            Material material,
            Vector3 position,
            Vector3 shapeSize,
            float lifetime,
            float startSize,
            float rate,
            float speed,
            Color color)
        {
            GameObject particleObject = new GameObject(name);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = position;
            ParticleSystem system = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.55f, startSize);
            main.startColor = color;
            main.maxParticles = Mathf.CeilToInt(rate * lifetime * 1.8f) + 12;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = shapeSize;
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = name == "GroundFog" ? 0.25f : 0.8f;
            noise.frequency = 0.16f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.12f);

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = material;
            renderer.sortingFudge = name == "GroundFog" ? -2f : 0f;
        }

        private static GameObject FindModel(string name)
        {
            string[] ids = AssetDatabase.FindAssets(name + " t:Model");
            foreach (string id in ids)
            {
                string path = AssetDatabase.GUIDToAssetPath(id);
                bool exactName = Path.GetFileNameWithoutExtension(path)
                    .Equals(name, StringComparison.OrdinalIgnoreCase);
                bool expectedFolder = path.IndexOf("/nature/", StringComparison.OrdinalIgnoreCase) >= 0;
                bool lanternAsset = name.Equals("Lantern", StringComparison.OrdinalIgnoreCase);
                if (exactName && (expectedFolder || lanternAsset))
                {
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model != null)
                    {
                        return model;
                    }
                }
            }

            return null;
        }

        private static GameObject InstantiateModel(GameObject model, Transform parent)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(model);
            }

            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static AudioClip FindAudioClip(params string[] hints)
        {
            string[] ids = AssetDatabase.FindAssets("t:AudioClip", new[]
            {
                "Assets/Audio",
                "Assets/Art/Main Menu/Audio"
            });
            foreach (string id in ids)
            {
                string path = AssetDatabase.GUIDToAssetPath(id);
                string file = Path.GetFileNameWithoutExtension(path);
                foreach (string hint in hints)
                {
                    if (file.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    }
                }
            }

            return null;
        }

        private static void UpdateBuildSettings()
        {
            const string gameplayScene = "Assets/Scenes/ForestPembuka.unity";
            if (!File.Exists(gameplayScene))
            {
                Debug.LogError("Main Menu Scene Builder: " + gameplayScene + " was not found; Build Settings were not changed.");
                return;
            }

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(gameplayScene, true)
            };
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path != ScenePath && scene.path != gameplayScene)
                {
                    scenes.Add(scene);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Material CreateMaterial(string path, Color color)
        {
            return CreateMaterial(path, color, false);
        }

        private static float RandomRange(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrEmpty(parent))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
#endif
