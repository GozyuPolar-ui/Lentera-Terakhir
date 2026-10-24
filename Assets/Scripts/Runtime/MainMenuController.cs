using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JRPG.Menu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        public string firstGameScene = "ForestPembuka";
        public string continueScene = "ForestPembuka";
        public string saveExistsKey = "LenteraSaveExists";
        public string masterVolumeKey = "LenteraMasterVolume";
        public string musicVolumeKey = "LenteraMusicVolume";
        public string ambienceVolumeKey = "LenteraAmbienceVolume";
        public Button startButton;
        public Button continueButton;
        public Button settingsButton;
        public Button quitButton;
        public Button closeSettingsButton;
        public Slider masterSlider;
        public Slider musicSlider;
        public Slider ambienceSlider;
        public GameObject settingsPanel;
        public TextMeshProUGUI titleText;
        public CanvasGroup titleGroup;
        public Texture2D customCursor;
        public MenuAudio menuAudio;
        public SceneFader sceneFader;
        public float titleFadeDuration = 3f;
        public float titleSpacingStart = 18f;
        public float titleSpacingEnd = 2f;
        public Color normalTextColor = new Color(0.79f, 0.82f, 0.8f, 1f);
        public Color hoverTextColor = new Color(1f, 0.69f, 0.36f, 1f);
        public float hoverEffectDuration = 0.18f;
        public float underlineWidth = 58f;

        private readonly Dictionary<Button, Coroutine> effects = new Dictionary<Button, Coroutine>();
        private readonly Dictionary<Button, RectTransform> underlines = new Dictionary<Button, RectTransform>();
        private bool loadingScene;

        private void Start()
        {
            if (customCursor != null)
            {
                Cursor.SetCursor(customCursor, Vector2.zero, CursorMode.Auto);
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }

            ConfigureButton(startButton, StartGame);
            ConfigureButton(continueButton, ContinueGame);
            ConfigureButton(settingsButton, ToggleSettings);
            ConfigureButton(quitButton, QuitGame);
            ConfigureButton(closeSettingsButton, ToggleSettings);

            if (continueButton != null)
            {
                continueButton.interactable = PlayerPrefs.GetInt(saveExistsKey, 0) != 0;
                SetButtonState(continueButton, false, false);
            }

            InitializeSlider(masterSlider, masterVolumeKey, AudioListener.volume, SetMasterVolume);
            InitializeSlider(musicSlider, musicVolumeKey, menuAudio != null ? menuAudio.musicVolume : 0.7f, SetMusicVolume);
            InitializeSlider(ambienceSlider, ambienceVolumeKey, menuAudio != null ? menuAudio.ambienceVolume : 0.55f, SetAmbienceVolume);

            if (titleGroup != null)
            {
                titleGroup.alpha = 0f;
            }

            if (EventSystem.current != null && startButton != null)
            {
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
            }

            StartCoroutine(AnimateTitle());
        }

        private void Update()
        {
            if (BackPressed() && settingsPanel != null && settingsPanel.activeSelf)
            {
                ToggleSettings();
            }
        }

        private void ConfigureButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (menuAudio != null)
                {
                    menuAudio.PlayClick();
                }

                action.Invoke();
            });

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.color = normalTextColor;
            }

            RectTransform underline = FindUnderline(button.transform);
            if (underline != null)
            {
                underlines[button] = underline;
                underline.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers ??= new List<EventTrigger.Entry>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, _ => SetButtonState(button, true, true));
            AddTrigger(trigger, EventTriggerType.PointerExit, _ => SetButtonState(button, false, false));
            AddTrigger(trigger, EventTriggerType.Select, _ => SetButtonState(button, true, true));
            AddTrigger(trigger, EventTriggerType.Deselect, _ => SetButtonState(button, false, false));
        }

        private static RectTransform FindUnderline(Transform button)
        {
            Transform line = button.Find("Underline");
            return line as RectTransform;
        }

        private static void AddTrigger(
            EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> callback)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => callback(data));
            trigger.triggers.Add(entry);
        }

        private void SetButtonState(Button button, bool highlighted, bool playSound)
        {
            if (button == null)
            {
                return;
            }

            if (highlighted && playSound && menuAudio != null)
            {
                menuAudio.PlayHover();
            }

            if (effects.TryGetValue(button, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
            }

            effects[button] = StartCoroutine(AnimateButton(button, highlighted));
        }

        private IEnumerator AnimateButton(Button button, bool highlighted)
        {
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            underlines.TryGetValue(button, out RectTransform underline);
            float startWidth = underline != null ? underline.rect.width : 0f;
            float targetWidth = highlighted && button.interactable ? underlineWidth : 0f;
            Color startColor = label != null ? label.color : normalTextColor;
            Color targetColor = highlighted && button.interactable ? hoverTextColor : normalTextColor;
            float elapsed = 0f;
            while (elapsed < hoverEffectDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, hoverEffectDuration));
                t = t * t * (3f - 2f * t);
                if (label != null)
                {
                    label.color = Color.Lerp(startColor, targetColor, t);
                }

                if (underline != null)
                {
                    underline.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal, Mathf.Lerp(startWidth, targetWidth, t));
                }

                yield return null;
            }

            if (label != null)
            {
                label.color = targetColor;
            }
            if (underline != null)
            {
                underline.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
            }
            effects.Remove(button);
        }

        private IEnumerator AnimateTitle()
        {
            if (titleText == null || titleGroup == null)
            {
                yield break;
            }

            float elapsed = 0f;
            titleText.characterSpacing = titleSpacingStart;
            while (elapsed < titleFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, titleFadeDuration));
                float eased = t * t * (3f - 2f * t);
                titleGroup.alpha = eased;
                titleText.characterSpacing = Mathf.Lerp(titleSpacingStart, titleSpacingEnd, eased);
                yield return null;
            }

            titleGroup.alpha = 1f;
            titleText.characterSpacing = titleSpacingEnd;
        }

        private void InitializeSlider(
            Slider slider, string key, float fallback, UnityEngine.Events.UnityAction<float> changed)
        {
            if (slider == null)
            {
                return;
            }

            float value = PlayerPrefs.GetFloat(key, fallback);
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            changed.Invoke(slider.value);
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(changed);
        }

        private void SetMasterVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(masterVolumeKey, AudioListener.volume);
        }

        private void SetMusicVolume(float value)
        {
            if (menuAudio != null)
            {
                menuAudio.SetMusicVolume(value);
            }
            PlayerPrefs.SetFloat(musicVolumeKey, value);
        }

        private void SetAmbienceVolume(float value)
        {
            if (menuAudio != null)
            {
                menuAudio.SetAmbienceVolume(value);
            }
            PlayerPrefs.SetFloat(ambienceVolumeKey, value);
        }

        private void ToggleSettings()
        {
            if (settingsPanel == null)
            {
                return;
            }

            bool show = !settingsPanel.activeSelf;
            settingsPanel.SetActive(show);
            if (show && EventSystem.current != null && masterSlider != null)
            {
                EventSystem.current.SetSelectedGameObject(masterSlider.gameObject);
            }
            else if (!show && EventSystem.current != null && settingsButton != null)
            {
                EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
            }
        }

        private void StartGame()
        {
            LoadScene(firstGameScene);
        }

        private void ContinueGame()
        {
            if (PlayerPrefs.GetInt(saveExistsKey, 0) != 0)
            {
                LoadScene(continueScene);
            }
        }

        private void LoadScene(string sceneName)
        {
            if (loadingScene || string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            loadingScene = true;
            StartCoroutine(LoadSceneAfterFade(sceneName));
        }

        private IEnumerator LoadSceneAfterFade(string sceneName)
        {
            if (sceneFader != null)
            {
                yield return sceneFader.FadeOut();
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                loadingScene = false;
                Debug.LogError("Unable to load menu destination scene '" + sceneName + "'.", this);
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Gamepad.current != null
                    && (Gamepad.current.buttonEast.wasPressedThisFrame
                        || Gamepad.current.startButton.wasPressedThisFrame));
#else
            return Input.GetKeyDown(KeyCode.Escape)
                || Input.GetKeyDown(KeyCode.JoystickButton1)
                || Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
        }
    }
}
