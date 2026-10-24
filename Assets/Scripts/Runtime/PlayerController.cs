using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JRPG.Core
{
    /// <summary>Movement and mouse look for a CharacterController player.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private Camera cam;
        [SerializeField] private GameObject characterModel;
        [SerializeField] private RuntimeAnimatorController characterAnimatorController;
        [SerializeField] private Avatar characterAvatar;
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float maxLookAngle = 80f;
        [SerializeField] private AudioSource footstepSource;
        [SerializeField] private AudioClip[] footstepClips = new AudioClip[0];
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [SerializeField, Min(0f)] private float sprintStaminaDrain = 25f;
        [SerializeField, Min(0f)] private float staminaRecoveryRate = 20f;
        [SerializeField, Min(0f)] private float staminaRecoveryDelay = 0.8f;

        private CharacterController controller;
        private Animator characterAnimator;
        private GUIStyle statusBarLabelStyle;
        private bool movementLocked;
        private bool isDead;
        private float currentHealth;
        private float currentStamina;
        private float staminaRecoveryTime;
        private float verticalVelocity;
        private float pitch;
        private float nextFootstepTime;
        private int lastFootstepIndex = -1;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            currentHealth = maxHealth;
            currentStamina = maxStamina;
            EnsureFootstepSource();

            if (characterModel != null)
            {
                GameObject model = Instantiate(characterModel, transform);
                characterAnimator = model.GetComponent<Animator>();
                if (characterAnimator == null)
                {
                    characterAnimator = model.AddComponent<Animator>();
                }

                characterAnimator.avatar = characterAvatar;
                characterAnimator.runtimeAnimatorController = characterAnimatorController;
                characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                Renderer[] modelRenderers = model.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer modelRenderer in modelRenderers)
                {
                    modelRenderer.enabled = false;
                }
            }

            EnsurePlayerCamera();
            EnsureForestEndWalls();

            LockCursor();
        }

        private void EnsureForestEndWalls()
        {
            GameObject forest = GameObject.Find("Forest_Generated");
            Transform floor = forest != null ? forest.transform.Find("Forest_Floor") : null;
            Renderer floorRenderer = floor != null ? floor.GetComponent<Renderer>() : null;
            if (forest == null || floorRenderer == null)
            {
                return;
            }

            Bounds floorBounds = floorRenderer.bounds;
            float wallHeight = 32f;
            float wallThickness = 2f;
            float wallY = floorBounds.min.y + wallHeight * 0.5f;
            CreateForestEndWall(
                forest.transform,
                "Forest_Boundary_Start",
                new Vector3(floorBounds.center.x, wallY, floorBounds.min.z - wallThickness * 0.5f),
                new Vector3(floorBounds.size.x + 16f, wallHeight, wallThickness));
            CreateForestEndWall(
                forest.transform,
                "Forest_Boundary_End",
                new Vector3(floorBounds.center.x, wallY, floorBounds.max.z + wallThickness * 0.5f),
                new Vector3(floorBounds.size.x + 16f, wallHeight, wallThickness));
        }

        private static void CreateForestEndWall(Transform parent, string wallName, Vector3 position, Vector3 size)
        {
            Transform existingWall = parent.Find(wallName);
            GameObject wall = existingWall != null ? existingWall.gameObject : new GameObject(wallName);
            wall.transform.SetParent(parent, true);
            wall.transform.SetPositionAndRotation(position, Quaternion.identity);
            BoxCollider collider = wall.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = wall.AddComponent<BoxCollider>();
            }

            collider.center = Vector3.zero;
            collider.size = size;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (footstepClips != null && footstepClips.Length > 0)
            {
                return;
            }

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

            if (clips.Count > 0)
            {
                footstepClips = clips.ToArray();
                EditorUtility.SetDirty(this);
                if (gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
        }
#endif

        private void EnsureFootstepSource()
        {
            if (footstepSource == null)
            {
                Transform footsteps = transform.Find("Footsteps");
                GameObject footstepsObject;
                if (footsteps == null)
                {
                    footstepsObject = new GameObject("Footsteps");
                    footstepsObject.transform.SetParent(transform, false);
                }
                else
                {
                    footstepsObject = footsteps.gameObject;
                }

                footstepSource = footstepsObject.GetComponent<AudioSource>();
                if (footstepSource == null)
                {
                    footstepSource = footstepsObject.AddComponent<AudioSource>();
                }
            }

            footstepSource.playOnAwake = false;
            footstepSource.loop = false;
            footstepSource.volume = 0.55f;
            footstepSource.spatialBlend = 0.35f;
        }

        private void EnsurePlayerCamera()
        {
            if (cam == null)
            {
                cam = Camera.main;
            }

            if (cam == null)
            {
                cam = GetComponentInChildren<Camera>(true);
            }

            if (cam == null)
            {
                cam = FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            }

            if (cam == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.transform.SetParent(transform, false);
                cam = cameraObject.AddComponent<Camera>();
            }

            cam.gameObject.SetActive(true);
            cam.gameObject.tag = "MainCamera";
            cam.enabled = true;
            cam.targetDisplay = 0;
            cam.orthographic = false;
            cam.fieldOfView = 65f;
            cam.transform.SetParent(transform, false);
            cam.transform.localPosition = new Vector3(0f, 1.6f, 0.15f);
            cam.transform.localRotation = Quaternion.identity;

            AudioListener listener = cam.GetComponent<AudioListener>();
            if (listener == null)
            {
                listener = cam.gameObject.AddComponent<AudioListener>();
            }

            listener.enabled = true;

            AudioListener[] listeners = FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (AudioListener sceneListener in listeners)
            {
                if (sceneListener != listener)
                {
                    sceneListener.enabled = false;
                }
            }

            pitch = 0f;
        }

        public void ConfigureCamera(Camera playerCamera)
        {
            cam = playerCamera;
        }

        public void ConfigureCharacterModel(GameObject model, RuntimeAnimatorController animatorController, Avatar avatar)
        {
            characterModel = model;
            characterAnimatorController = animatorController;
            characterAvatar = avatar;
        }

        public void ConfigureFootsteps(AudioSource source, AudioClip[] clips)
        {
            footstepSource = source;
            footstepClips = clips;
        }

        public void SetMovementLocked(bool locked)
        {
            movementLocked = locked;
        }

        public void TakeDamage(float damage)
        {
            if (damage <= 0f || currentHealth <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            if (currentHealth <= 0f)
            {
                isDead = true;
            }
        }

        public void Heal(float amount)
        {
            if (amount > 0f)
            {
                currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
                if (currentHealth > 0f)
                {
                    isDead = false;
                }
            }
        }

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public bool IsDead => isDead;

        private void Update()
        {
            UpdateCursorLock();
            if (cam != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = ReadLook() * mouseSensitivity;
                transform.Rotate(Vector3.up, look.x, Space.World);
                pitch = Mathf.Clamp(pitch - look.y, -maxLookAngle, maxLookAngle);
                cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            if (movementLocked || isDead)
            {
                return;
            }

            Vector2 input = ReadMove();
            bool sprinting = input.sqrMagnitude > 0.01f && IsSprinting() && currentStamina > 0f;
            if (sprinting)
            {
                currentStamina = Mathf.Max(0f, currentStamina - sprintStaminaDrain * Time.deltaTime);
                staminaRecoveryTime = Time.time + staminaRecoveryDelay;
            }
            else if (Time.time >= staminaRecoveryTime)
            {
                currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRecoveryRate * Time.deltaTime);
            }

            float currentSpeed = sprinting ? sprintSpeed : moveSpeed;
            Vector3 move = (transform.forward * input.y + transform.right * input.x) * currentSpeed;
            if (characterAnimator != null)
            {
                characterAnimator.SetFloat("Speed", input.magnitude * currentSpeed);
            }

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (controller.isGrounded && JumpPressed())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);
            UpdateFootsteps(input);
        }

        private void OnGUI()
        {
            float width = Mathf.Clamp(Screen.width * 0.3f, 280f, 480f);
            float height = Mathf.Clamp(Screen.height * 0.034f, 30f, 44f);
            float x = Screen.width - width - Screen.width * 0.04f;
            float y = Screen.height * 0.055f;
            if (statusBarLabelStyle == null)
            {
                statusBarLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(height * 0.55f), 16, 24),
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }

            DrawStatusBar(new Rect(x, y, width, height), currentHealth / maxHealth,
                new Color(0.72f, 0.12f, 0.12f), "HEALTH");
            DrawStatusBar(new Rect(x, y + height * 1.8f, width, height), currentStamina / maxStamina,
                new Color(0.2f, 0.62f, 0.82f), "STAMINA");
            if (currentHealth <= 0f)
            {
                GUIStyle deathStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Screen.height / 18, 24, 52),
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, height * 2f),
                    "YOU HAVE FALLEN", deathStyle);
            }
        }

        private void DrawStatusBar(Rect rect, float value, Color fillColor, string label)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.02f, 0.025f, 0.035f, 0.88f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fillColor;
            Rect fill = new Rect(rect.x + 3f, rect.y + 3f,
                Mathf.Max(0f, (rect.width - 6f) * Mathf.Clamp01(value)), rect.height - 6f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x, rect.y - rect.height * 0.9f, rect.width, rect.height),
                label + "  " + Mathf.CeilToInt(value * 100f) + "%", statusBarLabelStyle);
            GUI.color = previousColor;
        }

        private void UpdateFootsteps(Vector2 input)
        {
            if (footstepSource == null || footstepClips == null || footstepClips.Length == 0
                || !controller.isGrounded || verticalVelocity > 0f || input.sqrMagnitude < 0.01f)
            {
                return;
            }

            if (Time.time < nextFootstepTime)
            {
                return;
            }

            int clipIndex = Random.Range(0, footstepClips.Length);
            if (footstepClips.Length > 1 && clipIndex == lastFootstepIndex)
            {
                clipIndex = (clipIndex + Random.Range(1, footstepClips.Length)) % footstepClips.Length;
            }

            lastFootstepIndex = clipIndex;
            AudioClip clip = footstepClips[clipIndex];
            if (clip != null)
            {
                footstepSource.pitch = Random.Range(0.92f, 1.08f);
                footstepSource.PlayOneShot(clip);
            }

            nextFootstepTime = Time.time + (IsSprinting() ? 0.34f : 0.5f);
        }

        private void UpdateCursorLock()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Cursor.lockState != CursorLockMode.Locked
                     && Mouse.current != null
                     && Mouse.current.leftButton.wasPressedThisFrame)
            {
                LockCursor();
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            {
                LockCursor();
            }
#endif
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static Vector2 ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 move = Vector2.zero;

            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) move.y += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) move.y -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) move.x += 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) move.x -= 1f;
            }

            Gamepad g = Gamepad.current;
            if (g != null)
            {
                move += g.leftStick.ReadValue();
            }

            return Vector2.ClampMagnitude(move, 1f);
#else
            Vector2 move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            return Vector2.ClampMagnitude(move, 1f);
#endif
        }

        private static Vector2 ReadLook()
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 look = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            if (Gamepad.current != null)
            {
                look += Gamepad.current.rightStick.ReadValue() * 10f;
            }

            return look;
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#endif
        }

        private static bool IsSprinting()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        }

        private static bool JumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }
    }
}
