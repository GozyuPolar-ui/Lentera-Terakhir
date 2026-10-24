using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace JRPG.Core
{
    public class ForestOpeningCutscene : MonoBehaviour
    {
        [SerializeField, TextArea(2, 4)] private string[] monologueLines =
        {
            "Rin... wake up.",
            "Why am I in the forest? I was on watch at Hearth...",
            "The last flame of the Great Lantern... it's still here. I have to protect it."
        };
        [SerializeField] private float wakingDuration = 3.5f;
        [SerializeField] private GameObject lanternModel;
        [SerializeField] private GameObject swordModel;
        [SerializeField] private GameObject parchmentModel;
        [SerializeField] private GameObject skeletonModel;
        [SerializeField] private RuntimeAnimatorController skeletonAnimatorController;
        [SerializeField] private GameObject skeletonWeaponModel;
        [SerializeField] private AudioClip[] swordSlashClips = new AudioClip[0];
        [SerializeField] private AudioClip[] swordEquipClips = new AudioClip[0];
        [SerializeField] private AudioClip skeletonFootstepClip;
        [SerializeField] private AudioClip skeletonAttackClip;
        [SerializeField] private AudioClip skeletonHitClip;
        [SerializeField] private Texture2D hearthLoadingImage;

        private PlayerController playerController;
        private Camera playerCamera;
        private Texture2D panelTexture;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle promptStyle;
        private int currentLine;
        private bool showingMonologue;
        private bool showingEyelids;
        private float eyelidClosedAmount = 1f;

        private void Start()
        {
            playerController = GetComponent<PlayerController>();
            playerCamera = GetComponentInChildren<Camera>(true);
            if (playerController == null || playerCamera == null)
            {
                Debug.LogError("Forest opening cutscene requires a PlayerController and child camera.", this);
                enabled = false;
                return;
            }

            playerController.SetMovementLocked(true);
            playerController.enabled = false;
            ForestPrologue prologue = GetComponent<ForestPrologue>();
            if (prologue == null)
            {
                prologue = gameObject.AddComponent<ForestPrologue>();
            }
            prologue.Initialize(
                playerController, playerCamera, lanternModel, swordModel,
                parchmentModel, skeletonModel, skeletonAnimatorController, skeletonWeaponModel,
                swordSlashClips, swordEquipClips, skeletonFootstepClip, skeletonAttackClip, skeletonHitClip,
                hearthLoadingImage);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.025f, 0.035f, 0.03f, 0.92f));
            panelTexture.Apply();
            StartCoroutine(PlayOpening());
        }

        private IEnumerator PlayOpening()
        {
            Transform cameraTransform = playerCamera.transform;
            Vector3 awakePosition = new Vector3(0f, 1.6f, 0.15f);
            Quaternion awakeRotation = Quaternion.identity;
            Quaternion lyingRotation = Quaternion.Euler(-78f, 0f, 0f);
            cameraTransform.localPosition = new Vector3(0f, 0.48f, -0.12f);
            cameraTransform.localRotation = lyingRotation;

            showingEyelids = true;
            yield return new WaitForSecondsRealtime(0.45f);
            yield return AnimateEyelids(1f, 0f, 1.1f);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return AnimateEyelids(0f, 1f, 0.12f);
            yield return AnimateEyelids(1f, 0f, 0.3f);
            yield return new WaitForSecondsRealtime(0.2f);
            showingEyelids = false;

            float elapsed = 0f;
            while (elapsed < wakingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / wakingDuration);
                float easedProgress = progress * progress * (3f - 2f * progress);
                cameraTransform.localPosition = Vector3.Lerp(
                    new Vector3(0f, 0.48f, -0.12f), awakePosition, easedProgress);
                cameraTransform.localRotation = Quaternion.Slerp(
                    lyingRotation, awakeRotation, easedProgress);
                yield return null;
            }

            cameraTransform.localPosition = awakePosition;
            cameraTransform.localRotation = awakeRotation;
            if (monologueLines == null || monologueLines.Length == 0)
            {
                FinishCutscene();
                yield break;
            }

            showingMonologue = true;
        }

        private void Update()
        {
            if (!showingMonologue || !ContinuePressed())
            {
                return;
            }

            currentLine++;
            if (monologueLines == null || currentLine >= monologueLines.Length)
            {
                FinishCutscene();
            }
        }

        private void FinishCutscene()
        {
            showingMonologue = false;
            playerController.SetMovementLocked(false);
            playerController.enabled = true;
            ForestPrologue prologue = GetComponent<ForestPrologue>();
            if (prologue != null)
            {
                prologue.StartTutorial();
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnGUI()
        {
            if (!showingMonologue || monologueLines == null || currentLine >= monologueLines.Length)
            {
                if (!showingEyelids)
                {
                    return;
                }
            }

            if (showingEyelids)
            {
                float lidHeight = Screen.height * 0.5f * eyelidClosedAmount;
                Color previousColor = GUI.color;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, lidHeight), Texture2D.whiteTexture);
                GUI.DrawTexture(
                    new Rect(0f, Screen.height - lidHeight, Screen.width, lidHeight),
                    Texture2D.whiteTexture);
                GUI.color = previousColor;
                return;
            }

            EnsureGuiStyles();
            float margin = Screen.width * 0.035f;
            float panelHeight = Screen.height * 0.27f;
            Rect panel = new Rect(margin, Screen.height - panelHeight - margin,
                Screen.width - margin * 2f, panelHeight);
            GUI.Box(panel, GUIContent.none, panelStyle);

            float inset = panelHeight * 0.12f;
            GUI.Label(new Rect(panel.x + inset, panel.y + inset,
                panel.width - inset * 2f, panelHeight * 0.2f), "Rin", titleStyle);
            GUI.Label(new Rect(panel.x + inset, panel.y + panelHeight * 0.33f,
                panel.width - inset * 2f, panelHeight * 0.42f),
                monologueLines[currentLine], textStyle);
            GUI.Label(new Rect(panel.x + inset, panel.yMax - panelHeight * 0.25f,
                panel.width - inset * 2f, panelHeight * 0.18f),
                "E / SPACE   CONTINUE", promptStyle);
        }

        private IEnumerator AnimateEyelids(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                eyelidClosedAmount = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                eyelidClosedAmount = Mathf.Lerp(from, to, progress);
                yield return null;
            }

            eyelidClosedAmount = to;
        }

        private void EnsureGuiStyles()
        {
            if (panelStyle == null)
            {
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
                promptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.82f, 0.85f, 0.8f) }
                };
            }

            int fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.03f), 24, 54);
            titleStyle.fontSize = fontSize;
            textStyle.fontSize = fontSize;
            promptStyle.fontSize = Mathf.Max(20, Mathf.RoundToInt(fontSize * 0.72f));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (lanternModel == null)
            {
                lanternModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Models/RPGITEMS/FBX/Lantern.fbx");
            }

            if (swordModel == null)
            {
                swordModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Models/RPGITEMS/FBX/Sword.fbx");
            }

            if (parchmentModel == null)
            {
                parchmentModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Models/RPGITEMS/FBX/Parchment.fbx");
            }

            if (skeletonModel == null)
            {
                skeletonModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Enemies/FBX/Skeleton.fbx");
            }

            if (skeletonWeaponModel == null)
            {
                skeletonWeaponModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Models/RPGITEMS/FBX/Sword.fbx");
            }

            EnsureSkeletonAnimationLoops();

            skeletonAnimatorController = CreateSkeletonAnimatorController();

            if (swordSlashClips == null || swordSlashClips.Length == 0)
            {
                swordSlashClips = new[]
                {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/OGG/knifeSlice.ogg"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/OGG/knifeSlice2.ogg")
                };
            }

            if (swordEquipClips == null || swordEquipClips.Length == 0)
            {
                swordEquipClips = new[]
                {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/OGG/drawKnife1.ogg"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/OGG/drawKnife2.ogg"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/OGG/drawKnife3.ogg")
                };
            }

            if (skeletonFootstepClip == null)
            {
                skeletonFootstepClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/OGG/Skeleton step.ogg");
            }

            if (skeletonAttackClip == null)
            {
                skeletonAttackClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/OGG/knifeSlice2.ogg");
            }

            if (skeletonHitClip == null)
            {
                skeletonHitClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/OGG/chop.ogg");
            }
        }

        private static RuntimeAnimatorController CreateSkeletonAnimatorController()
        {
            const string skeletonPath = "Assets/Art/Enemies/FBX/Skeleton.fbx";
            const string controllerPath = "Assets/Art/Enemies/Animations/Skeleton_Locomotion.controller";

            AnimationClip idle = FindAnimationClip("idle", skeletonPath);
            AnimationClip running = FindAnimationClip("running", skeletonPath);
            AnimationClip attack = FindAnimationClip("attack", skeletonPath);
            if (idle == null || running == null || attack == null)
            {
                Debug.LogError(
                    "Forest opening: Skeleton_Idle, Skeleton_Running, and Skeleton_Attack clips " +
                    "were not found in Skeleton.fbx.");
                return null;
            }

            const string folderPath = "Assets/Art/Enemies/Animations";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/Art/Enemies", "Animations");
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            if (controller == null)
            {
                Debug.LogError("Forest opening: could not create the skeleton locomotion controller.");
                return null;
            }

            if (controller.layers.Length == 0 || controller.layers[0].stateMachine == null)
            {
                if (controller.layers.Length == 0)
                {
                    controller.AddLayer("Base Layer");
                }

                if (controller.layers[0].stateMachine == null)
                {
                    AnimatorStateMachine newStateMachine = new AnimatorStateMachine
                    {
                        name = "Base Layer"
                    };
                    AssetDatabase.AddObjectToAsset(newStateMachine, controller);
                    AnimatorControllerLayer[] layers = controller.layers;
                    layers[0].stateMachine = newStateMachine;
                    controller.layers = layers;
                }
            }

            EnsureControllerParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureControllerParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindState(stateMachine, "Locomotion");
            if (locomotion == null)
            {
                locomotion = stateMachine.AddState("Locomotion");
            }

            stateMachine.defaultState = locomotion;
            BlendTree blendTree = locomotion.motion as BlendTree;
            if (blendTree == null)
            {
                blendTree = new BlendTree
                {
                    name = "Skeleton Locomotion",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = "Speed",
                    useAutomaticThresholds = false
                };
                AssetDatabase.AddObjectToAsset(blendTree, controller);
                locomotion.motion = blendTree;
            }

            blendTree.children = new ChildMotion[0];
            blendTree.AddChild(idle, 0f);
            blendTree.AddChild(running, 1f);

            AnimatorState attackState = FindState(stateMachine, "Attack");
            if (attackState == null)
            {
                attackState = stateMachine.AddState("Attack");
            }

            attackState.motion = attack;
            EnsureAttackTransitions(stateMachine, locomotion, attackState);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void EnsureControllerParameter(
            AnimatorController controller, string parameterName, AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == parameterName)
                {
                    if (parameters[i].type != type)
                    {
                        controller.RemoveParameter(i);
                        controller.AddParameter(parameterName, type);
                    }
                    return;
                }
            }

            controller.AddParameter(parameterName, type);
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.name == stateName)
                {
                    return child.state;
                }
            }

            return null;
        }

        private static void EnsureAttackTransitions(
            AnimatorStateMachine stateMachine, AnimatorState locomotion, AnimatorState attack)
        {
            bool hasAttackTransition = false;
            foreach (AnimatorStateTransition transition in locomotion.transitions)
            {
                if (transition.destinationState == attack)
                {
                    hasAttackTransition = true;
                    break;
                }
            }

            if (!hasAttackTransition)
            {
                AnimatorStateTransition transition = locomotion.AddTransition(attack);
                transition.hasExitTime = false;
                transition.duration = 0.08f;
                transition.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            }

            bool returnsToLocomotion = false;
            foreach (AnimatorStateTransition transition in attack.transitions)
            {
                if (transition.destinationState == locomotion)
                {
                    returnsToLocomotion = true;
                    break;
                }
            }

            if (!returnsToLocomotion)
            {
                AnimatorStateTransition transition = attack.AddTransition(locomotion);
                transition.hasExitTime = true;
                transition.exitTime = 0.95f;
                transition.duration = 0.1f;
            }
        }

        private static void EnsureSkeletonAnimationLoops()
        {
            const string skeletonPath = "Assets/Art/Enemies/FBX/Skeleton.fbx";
            ModelImporter importer = AssetImporter.GetAtPath(skeletonPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if ((clip.name.IndexOf("idle", System.StringComparison.OrdinalIgnoreCase) >= 0
                     || clip.name.IndexOf("running", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    && !clip.loopTime)
                {
                    clip.loopTime = true;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip FindAnimationClip(string keyword, string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (UnityEngine.Object asset in assets)
            {
                AnimationClip clip = asset as AnimationClip;
                if (clip != null && clip.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return clip;
                }
            }

            return null;
        }
#endif

        private void OnDestroy()
        {
            if (panelTexture != null)
            {
                Destroy(panelTexture);
            }
        }

        private static bool ContinuePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
#endif
        }
    }
}
