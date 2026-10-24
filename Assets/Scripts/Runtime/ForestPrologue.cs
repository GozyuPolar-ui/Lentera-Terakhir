using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JRPG.Core
{
    public class ForestPrologue : MonoBehaviour
    {
        private const string SetpieceRootName = "Forest_Prologue_Setpieces";
        private const string HearthSceneName = "Desa Hearth";
        private const float AttackDistance = 3.2f;
        private static readonly int SkeletonSpeedParameter = Animator.StringToHash("Speed");
        private static readonly int SkeletonLocomotionState = Animator.StringToHash("Locomotion");
        private static readonly int SkeletonAttackTrigger = Animator.StringToHash("Attack");

        private enum TutorialStep
        {
            TakeLantern,
            FollowSign,
            TakeSword,
            ReadNote,
            ApproachWraith,
            AttackWraith,
            ReleaseLight,
            EnterHearth,
            Complete
        }

        private PlayerController playerController;
        private Camera playerCamera;
        private Transform forestRoot;
        private GameObject lanternPickup;
        private GameObject swordPickup;
        private GameObject parchmentPickup;
        private GameObject fogCreature;
        private GameObject skeletonModelInstance;
        private Animator skeletonAnimator;
        private Transform skeletonWeaponPivot;
        private GameObject gateDoor;
        private Material lanternMaterial;
        private Material warmLightMaterial;
        private Material woodMaterial;
        private Material parchmentMaterial;
        private Material fogMaterial;
        private Material gateMaterial;
        private GUIStyle promptStyle;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle notePanelStyle;
        private GUIStyle noteTitleStyle;
        private GUIStyle noteTextStyle;
        private GUIStyle notePromptStyle;
        private Texture2D panelTexture;
        private Texture2D notePanelTexture;
        private AudioSource swordAudioSource;
        private AudioSource skeletonFootstepSource;
        private readonly List<Light> pathLampLights = new List<Light>();
        private Light carriedLanternLight;
        private AudioClip[] swordSlashClips;
        private AudioClip[] swordEquipClips;
        private AudioClip skeletonFootstepClip;
        private AudioClip skeletonAttackClip;
        private AudioClip skeletonHitClip;
        private Transform swordSwingPivot;
        private Light[] directionalLights;
        private float[] previousDirectionalIntensities;
        private Color[] previousDirectionalColors;
        private UnityEngine.Rendering.AmbientMode previousAmbientMode;
        private Color previousAmbientSkyColor;
        private Color previousAmbientEquatorColor;
        private Color previousAmbientGroundColor;
        private float previousAmbientIntensity;
        private bool previousFog;
        private Color previousFogColor;
        private FogMode previousFogMode;
        private float previousFogStartDistance;
        private float previousFogEndDistance;
        private float previousFogDensity;
        private CameraClearFlags previousCameraClearFlags;
        private Color previousCameraBackgroundColor;
        private bool previousLightingCaptured;
        private Vector3 lanternPosition;
        private Vector3 swordPosition;
        private Vector3 parchmentPosition;
        private Vector3 signPosition;
        private Vector3 creaturePosition;
        private Vector3 gatePosition;
        private float forestLength;
        private float forestStartZ;
        private float forestCenterX;
        private int creatureHits;
        private float lightGauge;
        private float creatureBob;
        private float nextCreatureFootstepTime;
        private float nextCreatureAttackTime;
        private bool initialized;
        private bool lanternTaken;
        private bool swordTaken;
        private bool parchmentOpen;
        private bool gateUnlocked;
        private bool prologueComplete;
        private bool tutorialStarted;
        private bool swordSwinging;
        private bool skeletonAttacking;
        private bool enteringHearth;
        private bool showingHearthLoadingScreen;
        private float hearthLoadingProgress;
        private string hearthLoadingError;
        private Texture2D hearthLoadingImage;
        private GameObject carriedSword;
        private TutorialStep tutorialStep = TutorialStep.TakeLantern;

        public void Initialize(
            PlayerController controller, Camera camera, GameObject lanternAsset, GameObject swordAsset,
            GameObject parchmentAsset, GameObject skeletonAsset, RuntimeAnimatorController skeletonController,
            GameObject skeletonWeaponAsset, AudioClip[] slashClips, AudioClip[] equipClips,
            AudioClip creatureFootstepClip, AudioClip creatureAttackClip, AudioClip creatureHitClip,
            Texture2D loadingImage)
        {
            if (initialized)
            {
                return;
            }

            playerController = controller;
            playerCamera = camera;
            swordSlashClips = slashClips;
            swordEquipClips = equipClips;
            skeletonFootstepClip = creatureFootstepClip;
            skeletonAttackClip = creatureAttackClip;
            skeletonHitClip = creatureHitClip;
            hearthLoadingImage = loadingImage;
            forestRoot = GameObject.Find("Forest_Generated")?.transform;
            if (forestRoot == null || playerController == null || playerCamera == null)
            {
                Debug.LogError("Forest prologue requires Forest_Generated, PlayerController, and a player camera.", this);
                enabled = false;
                return;
            }

            Transform floor = forestRoot.Find("Forest_Floor");
            MeshFilter floorMesh = floor != null ? floor.GetComponent<MeshFilter>() : null;
            if (floorMesh == null || floorMesh.sharedMesh == null)
            {
                Debug.LogError("Forest prologue could not find the generated Forest_Floor mesh.", this);
                enabled = false;
                return;
            }

            initialized = true;
            Vector3 scaledMin = floor.localPosition + Vector3.Scale(
                floorMesh.sharedMesh.bounds.min, floor.localScale);
            Vector3 scaledMax = floor.localPosition + Vector3.Scale(
                floorMesh.sharedMesh.bounds.max, floor.localScale);
            float floorMinX = Mathf.Min(scaledMin.x, scaledMax.x);
            float floorMaxX = Mathf.Max(scaledMin.x, scaledMax.x);
            forestStartZ = Mathf.Min(scaledMin.z, scaledMax.z);
            float forestEndZ = Mathf.Max(scaledMin.z, scaledMax.z);
            forestCenterX = (floorMinX + floorMaxX) * 0.5f;
            float width = floorMaxX - floorMinX;
            forestLength = forestEndZ - forestStartZ;
            float startZ = Mathf.Clamp(5f, 0f, forestLength * 0.1f);
            float signZ = forestLength * 0.17f;
            float campZ = forestLength * 0.46f;
            float noteZ = forestLength * 0.52f;
            float creatureZ = forestLength * 0.72f;
            float gateZ = forestLength - 7f;

            CharacterController characterController = playerController.GetComponent<CharacterController>();
            characterController.enabled = false;
            playerController.transform.SetPositionAndRotation(
                GetPathWorldPosition(startZ, 0f),
                forestRoot.rotation);
            characterController.enabled = true;

            lanternPosition = GetPathWorldPosition(startZ + 2f, 1.1f);
            swordPosition = GetPathWorldPosition(campZ, 1.6f);
            parchmentPosition = GetPathWorldPosition(noteZ, -1.1f);
            signPosition = GetPathWorldPosition(signZ, -3f);
            creaturePosition = GetPathWorldPosition(creatureZ, 0f);
            gatePosition = GetPathWorldPosition(gateZ, 0f);

            ApplyNightAtmosphere();
            CreateMaterials();
            CreatePrologueSetpieces(
                forestRoot, width, campZ, lanternAsset, swordAsset, parchmentAsset, skeletonAsset);
            ConfigureSkeletonAnimator(skeletonController);
            ConfigureSkeletonWeapon(skeletonWeaponAsset);
            swordAudioSource = gameObject.AddComponent<AudioSource>();
            swordAudioSource.playOnAwake = false;
            swordAudioSource.loop = false;
            swordAudioSource.spatialBlend = 0f;
            swordAudioSource.volume = 0.75f;
            skeletonFootstepSource = fogCreature.AddComponent<AudioSource>();
            skeletonFootstepSource.playOnAwake = false;
            skeletonFootstepSource.loop = false;
            skeletonFootstepSource.spatialBlend = 1f;
            skeletonFootstepSource.volume = 0.65f;
        }

        private void ConfigureSkeletonAnimator(RuntimeAnimatorController animatorController)
        {
            if (skeletonModelInstance == null || animatorController == null)
            {
                return;
            }

            skeletonAnimator = skeletonModelInstance.GetComponentInChildren<Animator>(true);
            if (skeletonAnimator == null)
            {
                skeletonAnimator = skeletonModelInstance.AddComponent<Animator>();
            }

            skeletonAnimator.runtimeAnimatorController = animatorController;
            skeletonAnimator.applyRootMotion = false;
            skeletonAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            skeletonAnimator.enabled = true;
        }

        private void ConfigureSkeletonWeapon(GameObject weaponAsset)
        {
            if (skeletonModelInstance == null || weaponAsset == null)
            {
                return;
            }

            Transform hand = FindSkeletonHand(skeletonModelInstance.transform);
            skeletonWeaponPivot = new GameObject("Skeleton_Weapon_Pivot").transform;
            Transform pivotParent = hand != null ? hand : fogCreature.transform;
            skeletonWeaponPivot.SetParent(pivotParent, false);

            if (hand != null)
            {
                skeletonWeaponPivot.localPosition = Vector3.zero;
                skeletonWeaponPivot.localRotation = Quaternion.identity;
            }
            else
            {
                Bounds modelBounds = GetRendererBounds(skeletonModelInstance);
                Vector3 anchor = modelBounds.center
                    + fogCreature.transform.right * (modelBounds.extents.x * 0.55f)
                    + fogCreature.transform.forward * (modelBounds.extents.z * 0.45f)
                    + Vector3.up * (modelBounds.size.y * 0.1f);
                skeletonWeaponPivot.position = anchor;
                skeletonWeaponPivot.localRotation = Quaternion.Euler(0f, 0f, 30f);
            }

            GameObject weapon = Instantiate(weaponAsset, skeletonWeaponPivot);
            weapon.name = "Skeleton_Sword";
            FitVisual(weapon, 0.85f);
            weapon.transform.localRotation *= Quaternion.Euler(0f, 0f, 90f);
            Bounds weaponBounds = GetRendererBounds(weapon);
            weapon.transform.localPosition = new Vector3(0f, -weaponBounds.size.y * 0.38f, 0f);
        }

        private static Transform FindSkeletonHand(Transform root)
        {
            string[] handNames = { "RightHand", "R.Hand", "Hand.R", "R_Hand", "mixamorig:RightHand" };
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                foreach (string handName in handNames)
                {
                    if (string.Equals(child.name, handName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        private void ApplyNightAtmosphere()
        {
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientSkyColor = RenderSettings.ambientSkyColor;
            previousAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            previousAmbientGroundColor = RenderSettings.ambientGroundColor;
            previousAmbientIntensity = RenderSettings.ambientIntensity;
            previousFog = RenderSettings.fog;
            previousFogColor = RenderSettings.fogColor;
            previousFogMode = RenderSettings.fogMode;
            previousFogStartDistance = RenderSettings.fogStartDistance;
            previousFogEndDistance = RenderSettings.fogEndDistance;
            previousFogDensity = RenderSettings.fogDensity;
            previousCameraClearFlags = playerCamera.clearFlags;
            previousCameraBackgroundColor = playerCamera.backgroundColor;
            Transform[] sceneTransforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform sceneTransform in sceneTransforms)
            {
                if (sceneTransform.name.Contains("Forest_Sun_Disc")
                    && sceneTransform.gameObject.scene.IsValid())
                {
                    foreach (Renderer renderer in sceneTransform.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.enabled = false;
                    }
                    Destroy(sceneTransform.gameObject);
                }
            }

            directionalLights = FindObjectsByType<Light>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int directionalCount = 0;
            foreach (Light sceneLight in directionalLights)
            {
                if (sceneLight.type == LightType.Directional)
                {
                    directionalCount++;
                }
            }

            Light[] sceneLights = directionalLights;
            directionalLights = new Light[directionalCount];
            previousDirectionalIntensities = new float[directionalCount];
            previousDirectionalColors = new Color[directionalCount];
            int lightIndex = 0;
            foreach (Light sceneLight in sceneLights)
            {
                if (sceneLight.type != LightType.Directional)
                {
                    continue;
                }

                directionalLights[lightIndex] = sceneLight;
                previousDirectionalIntensities[lightIndex] = sceneLight.intensity;
                previousDirectionalColors[lightIndex] = sceneLight.color;
                lightIndex++;
            }

            previousLightingCaptured = true;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.026f, 0.045f, 0.085f);
            RenderSettings.ambientEquatorColor = new Color(0.035f, 0.047f, 0.065f);
            RenderSettings.ambientGroundColor = new Color(0.012f, 0.016f, 0.022f);
            RenderSettings.ambientIntensity = 0.34f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.052f, 0.067f, 0.09f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.013f;
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = new Color(0.018f, 0.03f, 0.058f);

            for (int i = 0; i < directionalLights.Length; i++)
            {
                directionalLights[i].color = new Color(0.42f, 0.52f, 0.72f);
                directionalLights[i].intensity = 0.14f;
            }
        }

        private void RestoreLighting()
        {
            if (!previousLightingCaptured)
            {
                return;
            }

            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientSkyColor = previousAmbientSkyColor;
            RenderSettings.ambientEquatorColor = previousAmbientEquatorColor;
            RenderSettings.ambientGroundColor = previousAmbientGroundColor;
            RenderSettings.ambientIntensity = previousAmbientIntensity;
            RenderSettings.fog = previousFog;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogStartDistance = previousFogStartDistance;
            RenderSettings.fogEndDistance = previousFogEndDistance;
            RenderSettings.fogDensity = previousFogDensity;

            playerCamera.clearFlags = previousCameraClearFlags;
            playerCamera.backgroundColor = previousCameraBackgroundColor;
            for (int i = 0; directionalLights != null && i < directionalLights.Length; i++)
            {
                if (directionalLights[i] != null)
                {
                    directionalLights[i].color = previousDirectionalColors[i];
                    directionalLights[i].intensity = previousDirectionalIntensities[i];
                }
            }
        }

        public void StartTutorial()
        {
            tutorialStarted = true;
        }

        private void CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError("Forest prologue could not find a supported material shader.", this);
                enabled = false;
                return;
            }

            woodMaterial = CreateMaterial(shader, new Color(0.2f, 0.12f, 0.075f));
            parchmentMaterial = CreateMaterial(shader, new Color(0.72f, 0.61f, 0.39f));
            gateMaterial = CreateMaterial(shader, new Color(0.24f, 0.16f, 0.09f));
            fogMaterial = CreateMaterial(shader, new Color(0.13f, 0.2f, 0.25f));
            lanternMaterial = CreateMaterial(shader, new Color(0.22f, 0.14f, 0.07f));
            warmLightMaterial = CreateMaterial(shader, new Color(1f, 0.62f, 0.16f));

            SetEmission(fogMaterial, new Color(0.12f, 0.32f, 0.55f) * 1.7f);
            SetEmission(warmLightMaterial, new Color(1f, 0.46f, 0.08f) * 2f);
        }

        private static Material CreateMaterial(Shader shader, Color color)
        {
            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private static void SetEmission(Material material, Color color)
        {
            if (material == null || !material.HasProperty("_EmissionColor"))
            {
                return;
            }

            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color);
        }

        private void CreatePrologueSetpieces(
            Transform parent, float width, float campZ,
            GameObject lanternAsset, GameObject swordAsset, GameObject parchmentAsset,
            GameObject skeletonAsset)
        {
            Transform previous = parent.Find(SetpieceRootName);
            if (previous != null)
            {
                Destroy(previous.gameObject);
            }

            Transform root = new GameObject(SetpieceRootName).transform;
            root.SetParent(parent, false);

            CreateLantern(root, lanternAsset);
            CreateBrokenSign(root);
            CreateCampsite(root, campZ);
            CreatePathLamps(root);
            swordPickup = CreatePickup(root, "Rin_Sword", swordPosition, swordAsset, true);
            parchmentPickup = CreatePickup(root, "Traveler_Note", parchmentPosition, parchmentAsset, false);
            CreateFogCreature(root, skeletonAsset);
            CreateHearthGate(root, width);
        }

        private void CreatePathLamps(Transform parent)
        {
            pathLampLights.Clear();
            float[] positions = { forestLength * 0.22f, forestLength * 0.4f, forestLength * 0.63f, forestLength * 0.84f };
            for (int i = 0; i < positions.Length; i++)
            {
                float side = i % 2 == 0 ? -4.4f : 4.4f;
                GameObject lamp = new GameObject("Forest_Path_Lamp_" + (i + 1));
                lamp.transform.SetParent(parent, false);
                lamp.transform.position = GetPathWorldPosition(positions[i], side);

                CreatePrimitive(PrimitiveType.Cylinder, "Lamp_Post", lamp.transform,
                    new Vector3(0f, 1.15f, 0f), new Vector3(0.12f, 1.15f, 0.12f), woodMaterial);
                CreatePrimitive(PrimitiveType.Cube, "Lamp_Bracket", lamp.transform,
                    new Vector3(0f, 2.2f, 0f), new Vector3(0.55f, 0.1f, 0.1f), woodMaterial);
                GameObject flame = CreatePrimitive(PrimitiveType.Sphere, "Lamp_Flame", lamp.transform,
                    new Vector3(0f, 2.05f, 0f), new Vector3(0.22f, 0.34f, 0.22f), warmLightMaterial);
                Light pointLight = flame.AddComponent<Light>();
                pointLight.type = LightType.Spot;
                pointLight.color = new Color(1f, 0.55f, 0.24f);
                pointLight.intensity = 7f;
                pointLight.range = 12f;
                pointLight.spotAngle = 82f;
                pointLight.innerSpotAngle = 48f;
                pointLight.shadows = LightShadows.None;
                pathLampLights.Add(pointLight);
                Vector3 pathTarget = GetPathWorldPosition(positions[i], 0f) + Vector3.up * 2.05f;
                pointLight.transform.rotation = Quaternion.LookRotation(
                    pathTarget - pointLight.transform.position, Vector3.up);
            }
        }

        private void CreateLantern(Transform parent, GameObject lanternAsset)
        {
            GameObject pickup = new GameObject("Last_Lantern_Pickup");
            pickup.transform.SetParent(parent, false);
            pickup.transform.position = lanternPosition;
            lanternPickup = pickup;

            if (lanternAsset != null)
            {
                GameObject modelInstance = Instantiate(lanternAsset, pickup.transform);
                modelInstance.name = "Last_Lantern_Model";
                StandVisualUpright(modelInstance, pickup.transform.position);
                FitVisual(modelInstance, 0.45f);
                Vector3 pickupPosition = pickup.transform.position;
                Bounds modelBounds = GetRendererBounds(modelInstance);
                if (modelBounds.size != Vector3.zero)
                {
                    modelInstance.transform.position += pickupPosition - modelBounds.center;
                    pickup.transform.position += Vector3.up * (modelBounds.size.y * 0.5f);
                }
            }
            else
            {
                CreatePrimitive(PrimitiveType.Cylinder, "Lantern_Body", pickup.transform,
                    new Vector3(0f, 0.28f, 0f), new Vector3(0.22f, 0.28f, 0.22f), lanternMaterial);
                CreatePrimitive(PrimitiveType.Cube, "Lantern_Cap", pickup.transform,
                    new Vector3(0f, 0.58f, 0f), new Vector3(0.34f, 0.08f, 0.34f), woodMaterial);
            }

            GameObject flame = new GameObject("Last_Flame");
            flame.transform.SetParent(pickup.transform, false);
            flame.transform.localPosition = Vector3.zero;
            carriedLanternLight = flame.AddComponent<Light>();
            carriedLanternLight.type = LightType.Point;
            carriedLanternLight.color = new Color(1f, 0.55f, 0.24f);
            carriedLanternLight.intensity = 2.5f;
            carriedLanternLight.range = 10f;
            carriedLanternLight.shadows = LightShadows.None;
        }

        private void CreateBrokenSign(Transform parent)
        {
            GameObject sign = new GameObject("Broken_Hearth_Sign");
            sign.transform.SetParent(parent, false);
            sign.transform.position = signPosition;

            CreatePrimitive(PrimitiveType.Cube, "Sign_Post", sign.transform,
                new Vector3(0f, 0.9f, 0f), new Vector3(0.16f, 1.8f, 0.16f), woodMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Broken_Sign_Board", sign.transform,
                new Vector3(0.28f, 1.35f, 0f), new Vector3(1.1f, 0.28f, 0.12f), woodMaterial)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
            GameObject signTextObject = new GameObject("Sign_Text");
            signTextObject.transform.SetParent(sign.transform, false);
            signTextObject.transform.localPosition = new Vector3(0.25f, 1.35f, -0.08f);
            TextMesh signText = signTextObject.AddComponent<TextMesh>();
            signText.text = "HEARTH >";
            signText.anchor = TextAnchor.MiddleCenter;
            signText.alignment = TextAlignment.Center;
            signText.characterSize = 0.12f;
            signText.fontSize = 40;
            signText.color = new Color(0.84f, 0.72f, 0.48f);
        }

        private void CreateCampsite(Transform parent, float campZ)
        {
            Vector3 campPosition = GetPathWorldPosition(campZ, -2f);
            GameObject camp = new GameObject("Abandoned_Campsite");
            camp.transform.SetParent(parent, false);
            camp.transform.position = campPosition;

            for (int log = 0; log < 3; log++)
            {
                GameObject wood = CreatePrimitive(PrimitiveType.Cylinder, "Cold_Firewood_" + log,
                    camp.transform, new Vector3(0.1f, 0.12f + log * 0.05f, log * 0.16f),
                    new Vector3(0.08f, 0.55f, 0.08f), woodMaterial);
                wood.transform.localRotation = Quaternion.Euler(0f, 0f, 90f + log * 16f);
            }

            CreatePrimitive(PrimitiveType.Sphere, "Cold_Ash", camp.transform,
                new Vector3(0f, 0.06f, 0f), new Vector3(0.9f, 0.1f, 0.65f),
                parchmentMaterial).GetComponent<Renderer>().material.color = new Color(0.2f, 0.2f, 0.17f);
        }

        private GameObject CreatePickup(
            Transform parent, string name, Vector3 position, GameObject model, bool isSword)
        {
            GameObject pickup = new GameObject(name);
            pickup.transform.SetParent(parent, false);
            pickup.transform.position = position + (isSword ? Vector3.zero : Vector3.down * 0.105f);

            if (model != null)
            {
                GameObject visual = Instantiate(model, pickup.transform);
                visual.name = name + "_Model";
                FitVisual(visual, isSword ? 0.95f : 0.8f);
                if (isSword)
                {
                    visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    LiftVisualOntoGround(visual, pickup.transform.position.y);
                }
                else
                {
                    LayVisualFlat(visual);
                    LiftVisualOntoGround(visual, pickup.transform.position.y + 0.015f);
                    Bounds bounds = GetRendererBounds(visual);
                    visual.transform.position += new Vector3(
                        pickup.transform.position.x - bounds.center.x,
                        0f,
                        pickup.transform.position.z - bounds.center.z);
                }
            }
            else
            {
                CreatePrimitive(PrimitiveType.Cube, name + "_Fallback", pickup.transform,
                    isSword ? Vector3.zero : Vector3.up * 0.04f,
                    isSword ? new Vector3(0.12f, 0.8f, 0.08f) : new Vector3(0.5f, 0.08f, 0.35f),
                    isSword ? gateMaterial : parchmentMaterial);
            }

            return pickup;
        }

        private static void LiftVisualOntoGround(GameObject visual, float groundHeight)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            visual.transform.position += Vector3.up * (groundHeight - bounds.min.y);
        }

        private static void LayVisualFlat(GameObject visual)
        {
            Quaternion[] candidates =
            {
                Quaternion.identity,
                Quaternion.Euler(90f, 0f, 0f),
                Quaternion.Euler(-90f, 0f, 0f),
                Quaternion.Euler(0f, 0f, 90f),
                Quaternion.Euler(0f, 0f, -90f)
            };
            Quaternion bestRotation = Quaternion.identity;
            float smallestHeight = float.MaxValue;
            foreach (Quaternion candidate in candidates)
            {
                visual.transform.localRotation = candidate;
                Bounds bounds = GetRendererBounds(visual);
                if (bounds.size.y < smallestHeight)
                {
                    smallestHeight = bounds.size.y;
                    bestRotation = candidate;
                }
            }

            visual.transform.localRotation = bestRotation;
            visual.transform.Rotate(0f, Random.Range(0f, 360f), 0f, Space.Self);
        }

        private static Bounds GetRendererBounds(GameObject visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(visual.transform.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void StandVisualUpright(GameObject visual, Vector3 pivotPosition)
        {
            Quaternion[] candidateRotations =
            {
                Quaternion.Euler(-90f, 0f, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                Quaternion.Euler(0f, 0f, -90f),
                Quaternion.Euler(0f, 0f, 90f),
                Quaternion.identity
            };
            Quaternion bestRotation = candidateRotations[0];
            float tallestBounds = -1f;
            float lowestPointDistance = float.MaxValue;
            foreach (Quaternion candidateRotation in candidateRotations)
            {
                visual.transform.localRotation = candidateRotation;
                Bounds bounds = GetRendererBounds(visual);
                float distanceFromPivot = Mathf.Abs(bounds.min.y - pivotPosition.y);
                if (bounds.size.y > tallestBounds + 0.001f
                    || (Mathf.Abs(bounds.size.y - tallestBounds) <= 0.001f
                        && distanceFromPivot < lowestPointDistance))
                {
                    tallestBounds = bounds.size.y;
                    lowestPointDistance = distanceFromPivot;
                    bestRotation = candidateRotation;
                }
            }

            visual.transform.localRotation = bestRotation;
        }

        private static void FitVisual(GameObject visual, float targetSize)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largestDimension > 0.001f)
            {
                visual.transform.localScale *= targetSize / largestDimension;
            }
        }

        private void CreateFogCreature(Transform parent, GameObject skeletonAsset)
        {
            fogCreature = new GameObject("First_Fog_Skeleton");
            fogCreature.transform.SetParent(parent, false);
            fogCreature.transform.position = creaturePosition;

            if (skeletonAsset != null)
            {
                skeletonModelInstance = Instantiate(skeletonAsset, fogCreature.transform);
                skeletonModelInstance.name = "Skeleton_Model";
                FitVisual(skeletonModelInstance, 2.4f);
                LiftVisualOntoGround(skeletonModelInstance, creaturePosition.y);
            }
            else
            {
                Debug.LogWarning("Skeleton model asset was not assigned; using the fog creature fallback.", this);
                CreatePrimitive(PrimitiveType.Sphere, "Wraith_Core", fogCreature.transform,
                    new Vector3(0f, 1.25f, 0f), new Vector3(0.9f, 1.3f, 0.65f), fogMaterial);
                CreatePrimitive(PrimitiveType.Sphere, "Wraith_Head", fogCreature.transform,
                    new Vector3(0f, 2.05f, 0.04f), new Vector3(0.62f, 0.62f, 0.58f), fogMaterial);
                CreatePrimitive(PrimitiveType.Sphere, "Wraith_Eye_Left", fogCreature.transform,
                    new Vector3(-0.17f, 2.1f, 0.31f), new Vector3(0.08f, 0.08f, 0.04f), warmLightMaterial);
                CreatePrimitive(PrimitiveType.Sphere, "Wraith_Eye_Right", fogCreature.transform,
                    new Vector3(0.17f, 2.1f, 0.31f), new Vector3(0.08f, 0.08f, 0.04f), warmLightMaterial);
                CreatePrimitive(PrimitiveType.Capsule, "Wraith_Arm_Left", fogCreature.transform,
                    new Vector3(-0.58f, 1.25f, 0f), new Vector3(0.24f, 0.85f, 0.24f), fogMaterial)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
                CreatePrimitive(PrimitiveType.Capsule, "Wraith_Arm_Right", fogCreature.transform,
                    new Vector3(0.58f, 1.25f, 0f), new Vector3(0.24f, 0.85f, 0.24f), fogMaterial)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            }

            fogCreature.SetActive(false);
        }

        private void CreateHearthGate(Transform parent, float width)
        {
            GameObject gate = new GameObject("Hearth_Gate");
            gate.transform.SetParent(parent, false);
            gate.transform.position = gatePosition;
            float halfWidth = Mathf.Clamp(width * 0.16f, 6f, 10f);

            CreatePrimitive(PrimitiveType.Cube, "Gate_Left_Post", gate.transform,
                new Vector3(-halfWidth, 3.2f, 0f), new Vector3(1f, 6.4f, 1f), gateMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Gate_Right_Post", gate.transform,
                new Vector3(halfWidth, 3.2f, 0f), new Vector3(1f, 6.4f, 1f), gateMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Gate_Lintel", gate.transform,
                new Vector3(0f, 6.5f, 0f), new Vector3(halfWidth * 2f + 1f, 0.8f, 1.2f), gateMaterial);
            gateDoor = CreatePrimitive(PrimitiveType.Cube, "Fogbound_Gate_Door", gate.transform,
                new Vector3(0f, 2.5f, 0.15f), new Vector3(halfWidth * 1.8f, 5f, 0.3f), fogMaterial);
            BoxCollider gateCollider = gateDoor.AddComponent<BoxCollider>();
            gateCollider.size = new Vector3(1f, 1f, 1f);
            gateDoor.SetActive(true);
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = primitive.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return primitive;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            UpdatePathLampFlicker();
            if (!tutorialStarted || prologueComplete)
            {
                return;
            }

            if (parchmentOpen)
            {
                if (InteractPressed() || EscapePressed())
                {
                    parchmentOpen = false;
                    playerController.SetMovementLocked(false);
                    tutorialStep = TutorialStep.ApproachWraith;
                }
                return;
            }

            UpdateFogCreature();
            UpdatePromptsAndInteraction();
            if (tutorialStep == TutorialStep.AttackWraith && AttackPressed())
            {
                TryAttack();
            }

            if (tutorialStep == TutorialStep.ReleaseLight && LightAttackPressed())
            {
                BanishCreature();
            }

            if (tutorialStep == TutorialStep.EnterHearth && IsNear(gatePosition, 3f) && InteractPressed())
            {
                EnterHearth();
            }
        }

        private void UpdatePathLampFlicker()
        {
            for (int i = 0; i < pathLampLights.Count; i++)
            {
                Light lamp = pathLampLights[i];
                if (lamp == null)
                {
                    continue;
                }

                float phase = Time.time * (5.3f + i * 0.41f) + i * 2.17f;
                float flicker = 0.9f + Mathf.Sin(phase) * 0.055f
                    + Mathf.Sin(phase * 2.37f) * 0.025f;
                lamp.intensity = 7f * flicker;
            }
        }

        private void EnterHearth()
        {
            if (!Application.CanStreamedLevelBeLoaded(HearthSceneName))
            {
                Debug.LogError(
                    "Forest prologue cannot enter Hearth because scene '" + HearthSceneName
                    + "' is not included in Build Settings.", this);
                return;
            }

            if (enteringHearth)
            {
                return;
            }

            enteringHearth = true;
            prologueComplete = true;
            tutorialStep = TutorialStep.Complete;
            playerController.SetMovementLocked(true);
            ForestOpeningCutscene cutscene = GetComponent<ForestOpeningCutscene>();
            if (cutscene != null)
            {
                cutscene.enabled = false;
            }
            if (carriedLanternLight != null)
            {
                carriedLanternLight.enabled = false;
            }
            RestoreLighting();
            previousLightingCaptured = false;

            playerController.transform.SetParent(null, true);
            DontDestroyOnLoad(playerController.gameObject);
            showingHearthLoadingScreen = true;
            SceneManager.sceneLoaded += OnHearthSceneLoaded;
            StartCoroutine(LoadHearthScene());
        }

        private IEnumerator LoadHearthScene()
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(HearthSceneName, LoadSceneMode.Single);
            if (loadOperation == null)
            {
                SceneManager.sceneLoaded -= OnHearthSceneLoaded;
                hearthLoadingError = "Desa Hearth tidak dapat dimuat.";
                Debug.LogError("Forest prologue failed to start asynchronous loading for Hearth.", this);
                yield break;
            }

            while (!loadOperation.isDone)
            {
                hearthLoadingProgress = Mathf.Min(0.9f, loadOperation.progress);
                yield return null;
            }

            hearthLoadingProgress = 0.9f;
        }

        private void OnHearthSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != HearthSceneName)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnHearthSceneLoaded;
            StartCoroutine(FinishHearthTransition(scene));
        }

        private IEnumerator FinishHearthTransition(Scene scene)
        {
            yield return null;

            Transform destinationPlayer = null;
            GameObject[] sceneRoots = scene.GetRootGameObjects();
            foreach (GameObject sceneRoot in sceneRoots)
            {
                Transform[] descendants = sceneRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform descendant in descendants)
                {
                    if (descendant.name == "Player")
                    {
                        destinationPlayer = descendant;
                        break;
                    }
                }

                if (destinationPlayer != null)
                {
                    break;
                }
            }

            Vector3 spawnPosition;
            Quaternion spawnRotation;
            if (TryGetHearthEntrance(scene, out spawnPosition, out spawnRotation))
            {
                if (destinationPlayer != null)
                {
                    Destroy(destinationPlayer.gameObject);
                }
            }
            else if (destinationPlayer != null)
            {
                spawnPosition = destinationPlayer.position;
                spawnRotation = destinationPlayer.rotation;
                Destroy(destinationPlayer.gameObject);
            }
            else
            {
                hearthLoadingError = "Posisi masuk desa tidak ditemukan.";
                Debug.LogError(
                    "Hearth scene has no usable village entrance or Player. "
                    + "The forest Player was kept at its last position.",
                    this);
                yield break;
            }

            CharacterController characterController = playerController.GetComponent<CharacterController>();
            characterController.enabled = false;
            playerController.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            characterController.enabled = true;
            Camera camera = playerController.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                playerController.ConfigureCamera(camera);
                camera.tag = "MainCamera";
                camera.enabled = true;
                AudioListener listener = camera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = true;
                }

                AudioListener[] listeners = FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (AudioListener sceneListener in listeners)
                {
                    if (sceneListener != listener)
                    {
                        sceneListener.enabled = false;
                    }
                }
            }

            hearthLoadingProgress = 0.95f;
            yield return new WaitForEndOfFrame();

            hearthLoadingProgress = 1f;
            showingHearthLoadingScreen = false;
            playerController.enabled = true;
            playerController.SetMovementLocked(false);
            ForestOpeningCutscene cutscene = GetComponent<ForestOpeningCutscene>();
            if (cutscene != null)
            {
                Destroy(cutscene);
            }
            Destroy(this);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static bool TryGetHearthEntrance(
            Scene scene, out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                if (sceneRoot.name != "Village_Generated")
                {
                    continue;
                }

                Transform ground = sceneRoot.transform.Find("Ground/Ground");
                Renderer groundRenderer = ground != null
                    ? ground.GetComponent<Renderer>()
                    : null;
                if (groundRenderer == null)
                {
                    continue;
                }

                Bounds bounds = groundRenderer.bounds;
                Vector3 entrance = new Vector3(bounds.max.x - 12f, bounds.max.y + 10f, bounds.center.z);
                MeshCollider groundCollider = ground.GetComponent<MeshCollider>();
                if (groundCollider == null)
                {
                    groundCollider = ground.GetComponentInChildren<MeshCollider>();
                }

                if (groundCollider != null
                    && groundCollider.Raycast(new Ray(entrance, Vector3.down), out RaycastHit hit, 100f))
                {
                    Vector3 surfacePoint = hit.point;
                    Transform road = sceneRoot.transform.Find("Road");
                    if (road != null)
                    {
                        Collider[] roadColliders = road.GetComponentsInChildren<Collider>(true);
                        foreach (Collider roadCollider in roadColliders)
                        {
                            if (roadCollider.Raycast(
                                    new Ray(entrance, Vector3.down), out RaycastHit roadHit, 100f)
                                && roadHit.point.y > surfacePoint.y)
                            {
                                surfacePoint = roadHit.point;
                            }
                        }
                    }

                    spawnPosition = surfacePoint + Vector3.up * 0.1f;
                }
                else
                {
                    spawnPosition = new Vector3(entrance.x, bounds.center.y + 0.1f, entrance.z);
                }

                spawnRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
                return true;
            }

            spawnPosition = Vector3.zero;
            spawnRotation = Quaternion.identity;
            return false;
        }

        private void UpdateFogCreature()
        {
            if (!lanternTaken || !swordTaken || fogCreature == null)
            {
                return;
            }

            if (tutorialStep == TutorialStep.ApproachWraith && !fogCreature.activeSelf
                && Vector3.Distance(playerController.transform.position, creaturePosition) < 18f)
            {
                fogCreature.SetActive(true);
            }

            if (!fogCreature.activeSelf)
            {
                return;
            }

            creatureBob += Time.deltaTime * 2.2f;
            Vector3 targetPosition = playerController.transform.position;
            targetPosition.y = 0f;
            Vector3 currentPosition = fogCreature.transform.position;
            currentPosition.y = skeletonModelInstance != null
                ? creaturePosition.y
                : Mathf.Sin(creatureBob) * 0.22f;
            float distanceToTarget = Vector3.Distance(currentPosition, targetPosition);
            bool creatureMoving = !skeletonAttacking && distanceToTarget > 2.25f;
            if (skeletonAnimator != null)
            {
                skeletonAnimator.SetFloat(
                    SkeletonSpeedParameter, creatureMoving ? 1f : 0f, 0.12f, Time.deltaTime);
                if (creatureMoving)
                {
                    AnimatorStateInfo state = skeletonAnimator.GetCurrentAnimatorStateInfo(0);
                    if (state.shortNameHash == SkeletonLocomotionState && state.normalizedTime >= 1f)
                    {
                        skeletonAnimator.Play(
                            SkeletonLocomotionState, 0, state.normalizedTime - Mathf.Floor(state.normalizedTime));
                    }
                }
            }

            if (creatureMoving)
            {
                currentPosition = Vector3.MoveTowards(currentPosition, targetPosition, Time.deltaTime * 1.15f);
                if (skeletonModelInstance != null && skeletonFootstepSource != null
                    && skeletonFootstepClip != null && Time.time >= nextCreatureFootstepTime)
                {
                    skeletonFootstepSource.PlayOneShot(skeletonFootstepClip);
                    nextCreatureFootstepTime = Time.time + 0.55f;
                }
            }
            else
            {
                nextCreatureFootstepTime = 0f;
            }

            fogCreature.transform.position = currentPosition;
            fogCreature.transform.LookAt(new Vector3(targetPosition.x, currentPosition.y + 1f, targetPosition.z));
            float distanceToPlayer = Vector3.Distance(playerController.transform.position, currentPosition);
            if (!skeletonAttacking && distanceToPlayer <= 2.5f && Time.time >= nextCreatureAttackTime)
            {
                StartCoroutine(AttackPlayer());
            }

            if (tutorialStep == TutorialStep.ApproachWraith
                && distanceToPlayer < 7f)
            {
                tutorialStep = TutorialStep.AttackWraith;
            }
        }

        private System.Collections.IEnumerator AttackPlayer()
        {
            skeletonAttacking = true;
            nextCreatureAttackTime = Time.time + 1.5f;
            if (skeletonAnimator != null)
            {
                skeletonAnimator.ResetTrigger(SkeletonAttackTrigger);
                skeletonAnimator.SetTrigger(SkeletonAttackTrigger);
            }

            if (skeletonFootstepSource != null && skeletonAttackClip != null)
            {
                skeletonFootstepSource.PlayOneShot(skeletonAttackClip);
            }

            if (skeletonWeaponPivot != null)
            {
                Quaternion restingRotation = skeletonWeaponPivot.localRotation;
                Quaternion windupRotation = restingRotation * Quaternion.Euler(-55f, 0f, -18f);
                Quaternion strikeRotation = restingRotation * Quaternion.Euler(75f, 0f, 32f);
                yield return RotateWeapon(restingRotation, windupRotation, 0.16f);
                yield return RotateWeapon(windupRotation, strikeRotation, 0.12f);
                yield return RotateWeapon(strikeRotation, restingRotation, 0.18f);
            }
            else
            {
                yield return new WaitForSeconds(0.3f);
            }

            if (fogCreature != null && fogCreature.activeSelf
                && Vector3.Distance(playerController.transform.position, fogCreature.transform.position) <= 2.8f)
            {
                playerController.TakeDamage(15f);
                if (skeletonFootstepSource != null && skeletonHitClip != null)
                {
                    skeletonFootstepSource.PlayOneShot(skeletonHitClip);
                }
            }

            yield return new WaitForSeconds(0.1f);
            skeletonAttacking = false;
        }

        private System.Collections.IEnumerator RotateWeapon(
            Quaternion from, Quaternion to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && skeletonWeaponPivot != null)
            {
                elapsed += Time.deltaTime;
                skeletonWeaponPivot.localRotation = Quaternion.Slerp(
                    from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            if (skeletonWeaponPivot != null)
            {
                skeletonWeaponPivot.localRotation = to;
            }
        }

        private void UpdatePromptsAndInteraction()
        {
            if (tutorialStep == TutorialStep.TakeLantern
                && IsNear(lanternPosition, 2.5f) && InteractPressed())
            {
                lanternTaken = true;
                if (carriedLanternLight != null)
                {
                    carriedLanternLight.type = LightType.Spot;
                    carriedLanternLight.intensity = 1400f;
                    carriedLanternLight.range = 25f;
                    carriedLanternLight.spotAngle = 82f;
                    carriedLanternLight.innerSpotAngle = 52f;
                }

                if (lanternPickup != null)
                {
                    lanternPickup.transform.SetParent(playerCamera.transform, false);
                    lanternPickup.transform.localPosition = new Vector3(-0.4f, -0.28f, 0.58f);
                    lanternPickup.transform.localRotation = Quaternion.identity;
                    Transform flame = lanternPickup.transform.Find("Last_Flame");
                    if (flame != null)
                    {
                        flame.localPosition = Vector3.zero;
                    }
                }
                lightGauge = 0.2f;
                tutorialStep = TutorialStep.FollowSign;
                return;
            }

            if (tutorialStep == TutorialStep.FollowSign
                && (IsNear(signPosition, 3.5f) || IsNear(swordPosition, 3.5f)))
            {
                tutorialStep = TutorialStep.TakeSword;
            }

            if (tutorialStep == TutorialStep.TakeSword
                && IsNear(swordPosition, 2.5f) && InteractPressed())
            {
                swordTaken = true;
                PlaySwordEquip();
                if (swordPickup != null)
                {
                    Transform visual = swordPickup.transform.childCount > 0
                        ? swordPickup.transform.GetChild(0)
                        : null;
                    if (visual != null)
                    {
                        visual.transform.localPosition = Vector3.zero;
                        visual.transform.localRotation = Quaternion.identity;
                        GameObject pivot = new GameObject("Sword_Swing_Pivot");
                        pivot.transform.SetParent(playerCamera.transform, false);
                        pivot.transform.localPosition = new Vector3(0.62f, -0.43f, 0.9f);
                        pivot.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
                        swordSwingPivot = pivot.transform;
                        carriedSword = visual.gameObject;
                        visual.SetParent(pivot.transform, false);
                    }
                    swordPickup.transform.DetachChildren();
                    Destroy(swordPickup);
                }
                tutorialStep = TutorialStep.ReadNote;
                return;
            }

            if (tutorialStep == TutorialStep.ReadNote
                && !parchmentOpen && IsNear(parchmentPosition, 2.5f) && InteractPressed())
            {
                parchmentOpen = true;
                playerController.SetMovementLocked(true);
                return;
            }

        }

        private void TryAttack()
        {
            if (swordSwinging)
            {
                return;
            }

            StartCoroutine(AnimateSwordSwing());
            PlaySwordSlash();
            if (tutorialStep != TutorialStep.AttackWraith || fogCreature == null || !fogCreature.activeSelf)
            {
                return;
            }

            Vector3 toCreature = fogCreature.transform.position + Vector3.up - playerCamera.transform.position;
            if (toCreature.magnitude > AttackDistance
                || Vector3.Dot(playerCamera.transform.forward, toCreature.normalized) < 0.45f)
            {
                return;
            }

            creatureHits++;
            lightGauge = Mathf.Clamp01(lightGauge + 0.27f);
            if (creatureHits >= 3)
            {
                lightGauge = 1f;
                tutorialStep = TutorialStep.ReleaseLight;
            }
        }

        private System.Collections.IEnumerator AnimateSwordSwing()
        {
            swordSwinging = true;
            if (swordSwingPivot != null)
            {
                Quaternion restingRotation = Quaternion.Euler(0f, 0f, -35f);
                Quaternion backswingRotation = Quaternion.Euler(-35f, -25f, 65f);
                Quaternion strikeRotation = Quaternion.Euler(45f, 20f, -80f);
                float elapsed = 0f;
                const float backswingDuration = 0.09f;
                const float strikeDuration = 0.11f;
                while (elapsed < backswingDuration)
                {
                    elapsed += Time.deltaTime;
                    swordSwingPivot.localRotation = Quaternion.Slerp(
                        restingRotation, backswingRotation, Mathf.Clamp01(elapsed / backswingDuration));
                    yield return null;
                }

                elapsed = 0f;
                while (elapsed < strikeDuration)
                {
                    elapsed += Time.deltaTime;
                    swordSwingPivot.localRotation = Quaternion.Slerp(
                        backswingRotation, strikeRotation, Mathf.Clamp01(elapsed / strikeDuration));
                    yield return null;
                }

                elapsed = 0f;
                const float recoveryDuration = 0.14f;
                while (elapsed < recoveryDuration)
                {
                    elapsed += Time.deltaTime;
                    swordSwingPivot.localRotation = Quaternion.Slerp(
                        strikeRotation, restingRotation, Mathf.Clamp01(elapsed / recoveryDuration));
                    yield return null;
                }

                swordSwingPivot.localRotation = restingRotation;
            }

            swordSwinging = false;
        }

        private void PlaySwordSlash()
        {
            if (swordAudioSource == null || swordSlashClips == null || swordSlashClips.Length == 0)
            {
                return;
            }

            AudioClip clip = swordSlashClips[Random.Range(0, swordSlashClips.Length)];
            if (clip != null)
            {
                swordAudioSource.PlayOneShot(clip);
            }
        }

        private void PlaySwordEquip()
        {
            if (swordAudioSource == null || swordEquipClips == null || swordEquipClips.Length == 0)
            {
                return;
            }

            AudioClip clip = swordEquipClips[Random.Range(0, swordEquipClips.Length)];
            if (clip != null)
            {
                swordAudioSource.PlayOneShot(clip);
            }
        }

        private void BanishCreature()
        {
            if (lightGauge < 1f || fogCreature == null)
            {
                return;
            }

            Destroy(fogCreature);
            fogCreature = null;
            lightGauge = 0.35f;
            gateUnlocked = true;
            if (gateDoor != null)
            {
                Destroy(gateDoor);
                gateDoor = null;
            }
            tutorialStep = TutorialStep.EnterHearth;
        }

        private bool IsNear(Vector3 position, float distance)
        {
            Vector3 playerPosition = playerController.transform.position;
            playerPosition.y = 0f;
            position.y = 0f;
            return Vector3.Distance(playerPosition, position) <= distance;
        }

        private void OnGUI()
        {
            if (showingHearthLoadingScreen)
            {
                DrawHearthLoadingScreen();
                return;
            }

            if (!initialized || !tutorialStarted)
            {
                return;
            }

            EnsureGuiStyles();
            if (parchmentOpen)
            {
                DrawTravelerNote();
                return;
            }

            if (!prologueComplete && lanternTaken)
            {
                DrawLightGauge();
            }

            switch (tutorialStep)
            {
                case TutorialStep.TakeLantern:
                    DrawPanel("PROLOGUE", "Rin wakes alone. The last flame of the Great Lantern lies nearby.",
                        IsNear(lanternPosition, 2.5f) ? "E   TAKE THE LANTERN" : "WASD   WALK TO THE LANTERN");
                    break;
                case TutorialStep.FollowSign:
                    DrawPanel("FOLLOW THE SIGN", "A broken sign points toward Hearth. Find it along the forest path.",
                        IsNear(signPosition, 3.5f) ? "E   INSPECT THE SIGN" : "WASD   FOLLOW THE PATH");
                    break;
                case TutorialStep.TakeSword:
                    DrawPanel("ABANDONED CAMPSITE", "A sword was left beside the cold campfire. Pick it up before moving on.",
                        IsNear(swordPosition, 2.5f) ? "E   TAKE THE SWORD" : "WASD   SEARCH THE CAMPSITE");
                    break;
                case TutorialStep.ReadNote:
                    DrawPanel("A TRAVELER'S TRACE", "A half-written note rests near the campsite.",
                        IsNear(parchmentPosition, 2.5f) ? "E   READ THE NOTE" : "WASD   FIND THE NOTE");
                    break;
                case TutorialStep.ApproachWraith:
                    DrawPanel("SOMETHING IN THE MIST", "A shape is moving ahead. Keep to the path and approach carefully.",
                        fogCreature != null && fogCreature.activeSelf ? "WASD   MOVE TOWARD THE WRAITH" : "WASD   FOLLOW THE PATH");
                    break;
                case TutorialStep.AttackWraith:
                    DrawPanel("SKELETON IN THE MIST", "Aim at the skeleton and strike it three times. Each hit charges the Light Gauge.",
                        "LEFT CLICK   SWING SWORD");
                    break;
                case TutorialStep.ReleaseLight:
                    DrawPanel("LIGHT GAUGE FULL", "Release the lantern's stored light to banish the wraith and clear the way.",
                        "Q   RELEASE LIGHT");
                    break;
                case TutorialStep.EnterHearth:
                    DrawPanel("THE GATE OF HEARTH", "The mist has parted. Reach the gate and enter the village.",
                        IsNear(gatePosition, 3f) ? "E   ENTER HEARTH" : "WASD   WALK TO THE GATE");
                    break;
                case TutorialStep.Complete:
                    DrawPanel("HEARTH", "The lanterns are still burning. For now, Hearth remembers.",
                        "PROLOGUE COMPLETE");
                    break;
            }
        }

        private void DrawHearthLoadingScreen()
        {
            if (hearthLoadingImage != null)
            {
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    hearthLoadingImage, ScaleMode.ScaleAndCrop);
            }
            else
            {
                GUI.backgroundColor = new Color(0.035f, 0.045f, 0.04f);
                GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
                GUI.backgroundColor = Color.white;
            }

            EnsureGuiStyles();
            float panelWidth = Screen.width * 0.48f;
            float panelHeight = Mathf.Max(100f, Screen.height * 0.17f);
            Rect panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                Screen.height * 0.77f,
                panelWidth,
                panelHeight);
            GUI.Box(panel, GUIContent.none, panelStyle);

            float inset = panelHeight * 0.14f;
            GUI.Label(new Rect(panel.x + inset, panel.y + inset,
                panel.width - inset * 2f, panelHeight * 0.28f),
                hearthLoadingError ?? "MEMUAT DESA HEARTH", titleStyle);

            Rect bar = new Rect(panel.x + inset, panel.y + panelHeight * 0.56f,
                panel.width - inset * 2f, Mathf.Max(12f, panelHeight * 0.18f));
            GUI.color = new Color(0.04f, 0.05f, 0.045f, 0.95f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.62f, 0.17f, 1f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * hearthLoadingProgress, bar.height),
                Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + inset, bar.yMax + inset * 0.15f,
                panel.width - inset * 2f, panelHeight * 0.2f),
                Mathf.RoundToInt(hearthLoadingProgress * 100f) + "%", promptStyle);
        }

        private void DrawLightGauge()
        {
            float width = Screen.width * 0.3f;
            float height = Mathf.Max(22f, Screen.height * 0.025f);
            Rect back = new Rect(Screen.width * 0.035f, Screen.height * 0.06f, width, height);
            GUI.color = new Color(0.02f, 0.025f, 0.035f, 0.86f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.62f, 0.17f, 1f);
            GUI.DrawTexture(new Rect(back.x + 3f, back.y + 3f,
                (back.width - 6f) * lightGauge, back.height - 6f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(back.x, back.y - height * 1.35f, width, height),
                "LIGHT GAUGE", titleStyle);
        }

        private void DrawPanel(string title, string body, string prompt)
        {
            float margin = Screen.width * 0.035f;
            float height = Screen.height * 0.27f;
            Rect panel = new Rect(margin, Screen.height - height - margin, Screen.width - margin * 2f, height);
            GUI.Box(panel, GUIContent.none, panelStyle);
            float inset = height * 0.12f;
            GUI.Label(new Rect(panel.x + inset, panel.y + inset, panel.width - inset * 2f, height * 0.2f),
                title, titleStyle);
            GUI.Label(new Rect(panel.x + inset, panel.y + height * 0.32f,
                panel.width - inset * 2f, height * 0.43f), body, textStyle);
            GUI.Label(new Rect(panel.x + inset, panel.yMax - height * 0.25f,
                panel.width - inset * 2f, height * 0.18f), prompt, promptStyle);
        }

        private void DrawTravelerNote()
        {
            GUI.color = new Color(0.015f, 0.02f, 0.025f, 0.76f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = Mathf.Min(Screen.width * 0.72f, 900f);
            float height = Mathf.Min(Screen.height * 0.42f, 420f);
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            GUI.Box(panel, GUIContent.none, notePanelStyle);
            float inset = Mathf.Clamp(height * 0.12f, 22f, 48f);
            GUI.Label(new Rect(panel.x + inset, panel.y + inset,
                panel.width - inset * 2f, height * 0.2f), "A TRAVELER'S NOTE", noteTitleStyle);
            GUI.Label(new Rect(panel.x + inset, panel.y + height * 0.34f,
                panel.width - inset * 2f, height * 0.4f),
                "\"The fog... I can't remember my name.\"\n\nThe ink trails off here. Whoever wrote this vanished before finishing the note.",
                noteTextStyle);
            GUI.Label(new Rect(panel.x + inset, panel.yMax - height * 0.2f,
                panel.width - inset * 2f, height * 0.12f), "E / ESC   CLOSE", notePromptStyle);
        }

        private void EnsureGuiStyles()
        {
            if (panelStyle == null)
            {
                panelTexture = new Texture2D(1, 1);
                panelTexture.SetPixel(0, 0, new Color(0.025f, 0.035f, 0.03f, 0.94f));
                panelTexture.Apply();
                panelStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(24, 24, 16, 16)
                };
                panelStyle.normal.background = panelTexture;
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.78f, 0.42f) }
                };
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
                promptStyle = new GUIStyle(textStyle)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontStyle = FontStyle.Bold
                };
            }

            if (notePanelStyle == null)
            {
                notePanelTexture = new Texture2D(1, 1);
                notePanelTexture.SetPixel(0, 0, new Color(0.16f, 0.12f, 0.075f, 0.98f));
                notePanelTexture.Apply();
                notePanelStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(28, 28, 20, 20)
                };
                notePanelStyle.normal.background = notePanelTexture;
                noteTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.78f, 0.42f) }
                };
                noteTextStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    normal = { textColor = new Color(0.96f, 0.91f, 0.78f) }
                };
                notePromptStyle = new GUIStyle(noteTextStyle)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontStyle = FontStyle.Bold
                };
            }

            int fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.027f), 22, 48);
            titleStyle.fontSize = fontSize;
            textStyle.fontSize = fontSize;
            promptStyle.fontSize = Mathf.Max(20, Mathf.RoundToInt(fontSize * 0.72f));
            noteTitleStyle.fontSize = fontSize;
            noteTextStyle.fontSize = fontSize;
            notePromptStyle.fontSize = Mathf.Max(20, Mathf.RoundToInt(fontSize * 0.72f));
        }

        private Vector3 GetPathWorldPosition(float forward, float sideOffset)
        {
            float clampedForward = Mathf.Clamp(forward, 0f, forestLength);
            return forestRoot.TransformPoint(new Vector3(
                forestCenterX + GetTrailCenterX(clampedForward, forestLength) + sideOffset,
                0.1f,
                forestStartZ + clampedForward));
        }

        private static float GetTrailCenterX(float forward, float length)
        {
            float t = Mathf.Clamp01(forward / length);
            float envelope = Mathf.Sin(Mathf.PI * t);
            return 7f * envelope
                   * (0.72f * Mathf.Sin(t * Mathf.PI * 2f * 1.4f)
                      + 0.28f * Mathf.Sin(t * Mathf.PI * 2f * 2.4f + 0.8f));
        }

        private static bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private static bool AttackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private static bool LightAttackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Q);
#endif
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnHearthSceneLoaded;
            RestoreLighting();
            Destroy(panelTexture);
            Destroy(notePanelTexture);
            Destroy(lanternMaterial);
            Destroy(warmLightMaterial);
            Destroy(woodMaterial);
            Destroy(parchmentMaterial);
            Destroy(fogMaterial);
            Destroy(gateMaterial);
        }
    }
}
