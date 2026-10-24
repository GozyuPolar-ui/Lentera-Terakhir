using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JRPG.Core
{
    public class NpcDialogue : MonoBehaviour
    {
        [SerializeField] private GameObject characterModel;
        [SerializeField] private GameObject outfitModel;
        [SerializeField] private GameObject hairModel;
        [SerializeField] private Avatar npcAvatar;
        [SerializeField] private RuntimeAnimatorController npcAnimatorController;
        [SerializeField] private Texture2D characterTexture;
        [SerializeField] private Texture2D skinTexture;
        [SerializeField] private Texture2D eyeTexture;
        [SerializeField] private Texture2D hairTexture;
        [SerializeField] private Texture2D accessoryTexture;
        [SerializeField] private string npcName = "Warga";
        [SerializeField, TextArea(2, 4)] private string[] dialogueLines =
        {
            "Thank goodness, someone's here! The road to the north isn't safe.",
            "If you're heading into the forest, take your sword and watch your surroundings."
        };
        [SerializeField] private float interactionDistance = 3.5f;
        [SerializeField, Range(0.75f, 1.25f)] private float characterScale = 1f;
        [SerializeField] private Color characterTint = Color.white;

        private static readonly int IdleAnimationState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int TalkingAnimationState = Animator.StringToHash("Base Layer.Talking");
        private Animator npcAnimator;
        private Transform characterVisual;
        private Vector3 characterVisualRestPosition;
        private Quaternion characterVisualRestRotation;
        private float idlePhase;
        private Camera playerCamera;
        private PlayerController playerController;
        private Texture2D panelTexture;
        private GUIStyle panelStyle;
        private GUIStyle textStyle;
        private GUIStyle nameStyle;
        private GUIStyle promptStyle;
        private int currentLine;
        private bool dialogueOpen;

        private void Awake()
        {
            if (characterModel != null)
            {
                GameObject model = Instantiate(characterModel, transform);
                characterVisual = model.transform;
                GameObject player = GameObject.Find("Player");
                CharacterController playerCharacter =
                    player != null ? player.GetComponent<CharacterController>() : null;
                float playerHeight = playerCharacter != null
                    ? playerCharacter.height * playerCharacter.transform.lossyScale.y
                    : 1.8f;
                Renderer[] baseRenderers = model.GetComponentsInChildren<Renderer>(true);
                if (baseRenderers.Length > 0)
                {
                    Bounds characterBounds = baseRenderers[0].bounds;
                    foreach (Renderer renderer in baseRenderers)
                    {
                        characterBounds.Encapsulate(renderer.bounds);
                    }

                    if (characterBounds.size.y > 0.01f)
                    {
                        float modelScale = playerHeight * characterScale / characterBounds.size.y;
                        model.transform.localScale = Vector3.one * modelScale;
                        Bounds scaledBounds = baseRenderers[0].bounds;
                        foreach (Renderer renderer in baseRenderers)
                        {
                            scaledBounds.Encapsulate(renderer.bounds);
                        }
                        model.transform.position += Vector3.up * (transform.position.y - scaledBounds.min.y);
                    }
                    else
                    {
                        Debug.LogError("NPC base character '" + model.name + "' has no measurable height.");
                    }
                }
                else
                {
                    Debug.LogError("NPC base character '" + model.name + "' has no renderers.");
                }

                ApplyBaseCharacterAppearance(model);

                if (outfitModel != null)
                {
                    GameObject outfit = Instantiate(outfitModel, model.transform, false);
                    RebindOutfitSkeleton(model.transform, outfit);
                    ApplyCharacterAppearance(outfit, characterTexture, true);
                }

                if (hairModel != null)
                {
                    GameObject hair = Instantiate(hairModel, model.transform, false);
                    RebindOutfitSkeleton(model.transform, hair);
                    ApplyCharacterAppearance(hair, hairTexture, false);
                }

                npcAnimator = model.GetComponent<Animator>();
                if (npcAnimator == null)
                {
                    npcAnimator = model.AddComponent<Animator>();
                }
                npcAnimator.avatar = npcAvatar;
                npcAnimator.runtimeAnimatorController = npcAnimatorController;
                npcAnimator.applyRootMotion = false;
                npcAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (npcAnimatorController == null)
                {
                    Debug.LogError("NPC '" + npcName + "' has no animation controller. Rebuild the Hearth NPCs.");
                }

                characterVisualRestPosition = characterVisual.localPosition;
                characterVisualRestRotation = characterVisual.localRotation;
                idlePhase = Mathf.Abs(GetEntityId().GetHashCode() % 1000) * 0.137f;
            }

            playerCamera = Camera.main;
            playerController = FindFirstObjectByType<PlayerController>();
            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.035f, 0.045f, 0.06f, 0.94f));
            panelTexture.Apply();
        }

        public void Configure(
            GameObject model, GameObject outfit, GameObject hairstyleModel,
            Texture2D outfitTexture, Texture2D faceTexture, Texture2D eyes, Texture2D hair,
            Texture2D equipmentTexture, Avatar avatar, RuntimeAnimatorController animatorController,
            string displayName, string[] lines, float scale, Color tint)
        {
            characterModel = model;
            outfitModel = outfit;
            hairModel = hairstyleModel;
            npcAvatar = avatar;
            npcAnimatorController = animatorController;
            characterTexture = outfitTexture;
            skinTexture = faceTexture;
            eyeTexture = eyes;
            hairTexture = hair;
            accessoryTexture = equipmentTexture;
            npcName = displayName;
            dialogueLines = lines;
            characterScale = scale;
            characterTint = tint;
        }

        private static void RebindOutfitSkeleton(Transform characterRoot, GameObject outfit)
        {
            Dictionary<string, Transform> characterBones = new Dictionary<string, Transform>();
            foreach (Transform bone in characterRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!characterBones.ContainsKey(bone.name))
                {
                    characterBones.Add(bone.name, bone);
                }
            }

            int reboundRenderers = 0;
            foreach (SkinnedMeshRenderer renderer in outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform[] sourceBones = renderer.bones;
                Transform[] mappedBones = new Transform[sourceBones.Length];
                bool allBonesFound = true;
                for (int i = 0; i < sourceBones.Length; i++)
                {
                    if (sourceBones[i] == null || !characterBones.TryGetValue(sourceBones[i].name, out mappedBones[i]))
                    {
                        allBonesFound = false;
                        break;
                    }
                }

                Transform mappedRoot;
                if (!allBonesFound
                    || renderer.rootBone == null
                    || !characterBones.TryGetValue(renderer.rootBone.name, out mappedRoot))
                {
                    Debug.LogError(
                        "NPC outfit mesh '" + renderer.name
                        + "' could not bind to the character skeleton; it will keep its imported rig. "
                        + "Check that both FBXs use the same Quaternius rig.");
                    continue;
                }

                renderer.bones = mappedBones;
                renderer.rootBone = mappedRoot;
                reboundRenderers++;
            }

            if (reboundRenderers == 0)
            {
                Debug.LogError("NPC outfit '" + outfit.name + "' contains no compatible skinned meshes.");
            }
        }

        private void ApplyCharacterAppearance(GameObject model, Texture2D fallbackTexture, bool isOutfit)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                string meshName = renderer is SkinnedMeshRenderer skinned
                    && skinned.sharedMesh != null
                    ? skinned.sharedMesh.name
                    : renderer.name;
                string rendererName = renderer.name + " " + meshName;
                bool isFace = ContainsAny(rendererName, "face", "head");
                bool isFacialFeature = ContainsAny(rendererName, "eye", "brow", "hair");
                if (!isOutfit && !isFace && !isFacialFeature)
                {
                    renderer.enabled = false;
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null)
                    {
                        continue;
                    }

                    renderer.GetPropertyBlock(properties, i);
                    if (material.HasProperty("_BaseColor") && characterTint != Color.white && !isFacialFeature)
                    {
                        properties.SetColor("_BaseColor", characterTint);
                    }
                    if (material.HasProperty("_Color") && characterTint != Color.white && !isFacialFeature)
                    {
                        properties.SetColor("_Color", characterTint);
                    }
                    bool materialIsSkin = ContainsAny(material.name, "skin", "regular");
                    bool materialIsEye = ContainsAny(material.name, "eye");
                    bool materialIsHair = ContainsAny(material.name, "hair", "brow");
                    Texture2D texture = isOutfit
                        ? materialIsSkin ? skinTexture : fallbackTexture
                        : materialIsEye || ContainsAny(rendererName, "eye") ? eyeTexture
                        : materialIsHair || ContainsAny(rendererName, "brow", "hair") ? hairTexture
                        : isFace ? skinTexture
                        : null;
                    if (IsEquipmentMaterial(renderer, material) && accessoryTexture != null)
                    {
                        texture = accessoryTexture;
                    }

                    if (texture != null)
                    {
                        if (material.HasProperty("_BaseMap"))
                        {
                            properties.SetTexture("_BaseMap", texture);
                        }
                        if (material.HasProperty("_MainTex"))
                        {
                            properties.SetTexture("_MainTex", texture);
                        }
                    }
                    renderer.SetPropertyBlock(properties, i);
                }
            }
        }

        private void ApplyBaseCharacterAppearance(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError("NPC base character '" + model.name + "' has no renderers.");
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                for (Transform parent = renderer.transform; parent != null; parent = parent.parent)
                {
                    parent.gameObject.SetActive(true);
                    if (parent == model.transform)
                    {
                        break;
                    }
                }
                renderer.enabled = true;
                string rendererName = GetRendererName(renderer);
                bool isEyes = ContainsAny(rendererName, "eye");
                bool isEyebrows = ContainsAny(rendererName, "brow");
                Texture2D texture = isEyes
                    ? eyeTexture
                    : isEyebrows ? hairTexture : skinTexture;
                ApplyTexture(renderer, texture);
            }
        }

        private static string GetRendererName(Renderer renderer)
        {
            string meshName = renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null
                ? skinned.sharedMesh.name
                : string.Empty;
            return renderer.name + " " + meshName;
        }

        private static void ApplyTexture(Renderer renderer, Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(properties, i);
                if (material.HasProperty("_BaseMap"))
                {
                    properties.SetTexture("_BaseMap", texture);
                }
                if (material.HasProperty("_MainTex"))
                {
                    properties.SetTexture("_MainTex", texture);
                }
                renderer.SetPropertyBlock(properties, i);
            }
        }

        private static bool ContainsAny(string value, params string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (value.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEquipmentMaterial(Renderer renderer, Material material)
        {
            string materialName = material.name;
            string rendererName = renderer.name;
            return ContainsEquipmentName(materialName) || ContainsEquipmentName(rendererName);
        }

        private static bool ContainsEquipmentName(string value)
        {
            return value.IndexOf("weapon", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("sword", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("staff", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("bow", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("dagger", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Update()
        {
            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerController>();
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null)
                {
                    return;
                }
            }

            if (dialogueOpen)
            {
                if (InteractPressed())
                {
                    currentLine++;
                    if (dialogueLines == null || currentLine >= dialogueLines.Length)
                    {
                        dialogueOpen = false;
                        PlayDialogueAnimation(false);
                        if (playerController != null)
                        {
                            playerController.SetMovementLocked(false);
                        }
                    }
                }

                return;
            }

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance)
                && hit.collider.GetComponentInParent<NpcDialogue>() == this
                && InteractPressed())
            {
                dialogueOpen = true;
                currentLine = 0;
                PlayDialogueAnimation(true);
                if (playerController != null)
                {
                    playerController.SetMovementLocked(true);
                }
            }
        }

        private void LateUpdate()
        {
            if (characterVisual == null)
            {
                return;
            }

            float time = Time.time * 1.7f + idlePhase;
            float breath = Mathf.Sin(time);
            float sway = Mathf.Sin(time * 0.55f + idlePhase) * (dialogueOpen ? 1.8f : 1.2f);
            float breathingAngle = Mathf.Sin(time * 0.55f) * 0.45f;
            characterVisual.localPosition = characterVisualRestPosition + Vector3.up * (breath * 0.015f);
            characterVisual.localRotation =
                characterVisualRestRotation * Quaternion.Euler(breathingAngle, sway, 0f);
        }

        private void PlayDialogueAnimation(bool talking)
        {
            if (npcAnimator == null || npcAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            int state = talking ? TalkingAnimationState : IdleAnimationState;
            npcAnimator.CrossFadeInFixedTime(state, 0.18f, 0, 0f);
        }

        private void OnGUI()
        {
            if (playerCamera == null)
            {
                return;
            }

            EnsureGuiStyles();

            if (dialogueOpen && dialogueLines != null && currentLine < dialogueLines.Length)
            {
                float margin = Screen.width * 0.035f;
                float panelHeight = Screen.height * 0.24f;
                Rect panel = new Rect(margin, Screen.height - panelHeight - margin,
                    Screen.width - margin * 2f, panelHeight);
                GUI.Box(panel, GUIContent.none, panelStyle);
                float inset = panelHeight * 0.12f;
                GUI.Label(new Rect(panel.x + inset, panel.y + inset,
                    panel.width - inset * 2f, nameStyle.fontSize * 1.5f), npcName, nameStyle);
                GUI.Label(new Rect(panel.x + inset, panel.y + panelHeight * 0.38f,
                    panel.width - inset * 2f, panelHeight * 0.38f),
                    dialogueLines[currentLine], textStyle);
                GUI.Label(new Rect(panel.x + inset, panel.yMax - panelHeight * 0.28f,
                    panel.width - inset * 2f, panelHeight * 0.2f), "E   LANJUT / TUTUP", promptStyle);
                return;
            }

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance)
                && hit.collider.GetComponentInParent<NpcDialogue>() == this)
            {
                float width = Screen.width * 0.62f;
                float height = Mathf.Max(52f, Screen.height * 0.075f);
                Rect prompt = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.72f, width, height);
                GUI.Box(prompt, GUIContent.none, panelStyle);
                GUI.Label(prompt, "TEKAN E UNTUK BICARA: " + npcName, promptStyle);
            }
        }

        private void EnsureGuiStyles()
        {
            if (panelStyle == null)
            {
                panelStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true,
                    padding = new RectOffset(24, 24, 16, 16)
                };
                panelStyle.normal.background = panelTexture;
                panelStyle.normal.textColor = Color.white;
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
                nameStyle = new GUIStyle(textStyle)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.78f, 0.35f) }
                };
                promptStyle = new GUIStyle(textStyle)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontStyle = FontStyle.Bold
                };
            }

            int fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.028f), 24, 54);
            panelStyle.fontSize = fontSize;
            textStyle.fontSize = fontSize;
            nameStyle.fontSize = fontSize;
            promptStyle.fontSize = Mathf.Max(20, Mathf.RoundToInt(fontSize * 0.7f));
        }

        private static bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }
    }
}
