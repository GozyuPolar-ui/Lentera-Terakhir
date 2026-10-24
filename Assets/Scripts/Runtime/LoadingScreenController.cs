using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JRPG.Menu
{
    public sealed class LoadingScreenController : MonoBehaviour
    {
        [Header("Scene")]
        public string defaultSceneName = "ForestPembuka";
        public float minimumDisplayTime = 3f;
        public float fadeDuration = 1f;

        [Header("Artwork")]
        public Image firstArtwork;
        public Image secondArtwork;
        public RectTransform artworkViewport;
        public Sprite[] loadingImages = Array.Empty<Sprite>();
        public float imageDurationMinimum = 6f;
        public float imageDurationMaximum = 8f;
        public float imageCrossfadeDuration = 1.4f;
        [Range(1f, 1.2f)] public float kenBurnsScale = 1.08f;
        public float kenBurnsPan = 24f;
        public Image vignetteOverlay;
        public RawImage fogOverlay;
        public float fogDriftSpeed = 5f;

        [Header("Progress")]
        public Image progressFill;
        public Image lanternGlow;
        public TMP_Text progressText;
        public string progressFormat = "{0:0}%";

        [Header("Hints")]
        [TextArea(2, 4)]
        public string[] hints =
        {
            "Jaga nyala lentera agar kegelapan tidak mendekat.",
            "Dengarkan suara di sekelilingmu. Tidak semua yang terdengar terlihat.",
            "Cahaya lentera dapat membantu menemukan jalan."
        };
        public TMP_Text hintText;
        public CanvasGroup hintGroup;
        public TMP_Text continuePrompt;
        public float hintInterval = 5f;
        public float hintFadeDuration = 0.55f;

        [Header("Audio")]
        public AudioSource ambienceSource;
        public AudioClip ambienceClip;
        [Range(0f, 1f)] public float ambienceVolume = 0.32f;

        private readonly List<int> imageOrder = new List<int>();
        private Coroutine loadingRoutine;
        private Coroutine artworkRoutine;
        private Coroutine hintRoutine;
        private AsyncOperation sceneOperation;
        private float displayedProgress;
        private float elapsedLoadingTime;
        private float presentationStartedAt;
        private float imageStartedAt;
        private float imageLifetime;
        private float fogStartX;
        private float fogStartY;
        private int hintIndex;
        private bool isLoading;
        private bool waitingForContinue;
        private static LoadingScreenController instance;

        public static LoadingScreenController Instance => instance;
        public event Action LoadingFinished;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            if (artworkViewport == null)
            {
                artworkViewport = transform as RectTransform;
            }

            if (fogOverlay != null)
            {
                fogStartX = fogOverlay.rectTransform.anchoredPosition.x;
                fogStartY = fogOverlay.rectTransform.anchoredPosition.y;
            }

            if (ambienceSource != null)
            {
                ambienceSource.playOnAwake = false;
                ambienceSource.loop = true;
                ambienceSource.spatialBlend = 0f;
                ambienceSource.clip = ambienceClip;
            }

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            if (!isLoading)
            {
                return;
            }

            elapsedLoadingTime += Time.unscaledDeltaTime;
            UpdateProgress();
            UpdateFog();
        }

        public void BeginLoading(string sceneName)
        {
            if (isLoading)
            {
                return;
            }

            string destination = string.IsNullOrWhiteSpace(sceneName) ? defaultSceneName : sceneName;
            if (string.IsNullOrWhiteSpace(destination))
            {
                Debug.LogError("Loading screen requires a destination scene name.", this);
                return;
            }

            isLoading = true;
            waitingForContinue = false;
            elapsedLoadingTime = 0f;
            presentationStartedAt = 0f;
            displayedProgress = 0f;
            gameObject.SetActive(true);
            if (loadingRoutine != null)
            {
                StopCoroutine(loadingRoutine);
            }

            loadingRoutine = StartCoroutine(LoadScene(destination));
        }

        private IEnumerator LoadScene(string sceneName)
        {
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                Debug.LogError("Loading screen prefab requires a CanvasGroup.", this);
                isLoading = false;
                LoadingFinished?.Invoke();
                yield break;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            yield return FadeCanvas(canvasGroup, 1f, fadeDuration);

            PrepareArtwork();
            StartPresentation();
            if (vignetteOverlay != null)
            {
                yield return FadeImageAlpha(vignetteOverlay, 0f, fadeDuration);
            }

            presentationStartedAt = elapsedLoadingTime;
            PlayAmbience();

            sceneOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (sceneOperation == null)
            {
                Debug.LogError("Unable to load destination scene '" + sceneName + "'.", this);
                isLoading = false;
                yield return FadeCanvas(canvasGroup, 0f, fadeDuration);
                gameObject.SetActive(false);
                LoadingFinished?.Invoke();
                yield break;
            }

            sceneOperation.allowSceneActivation = false;
            while (sceneOperation.progress < 0.9f
                || elapsedLoadingTime - presentationStartedAt < Mathf.Max(3f, minimumDisplayTime))
            {
                yield return null;
            }

            displayedProgress = 1f;
            SetProgressVisuals();
            waitingForContinue = true;
            if (continuePrompt != null)
            {
                continuePrompt.gameObject.SetActive(true);
            }

            while (!ContinuePressed())
            {
                yield return null;
            }

            waitingForContinue = false;
            if (continuePrompt != null)
            {
                continuePrompt.gameObject.SetActive(false);
            }

            yield return FadeArtworkToBlack();
            if (ambienceSource != null && ambienceSource.isPlaying)
            {
                yield return FadeAudio(ambienceSource, 0f, fadeDuration);
                ambienceSource.Stop();
            }

            sceneOperation.allowSceneActivation = true;
            while (!sceneOperation.isDone)
            {
                yield return null;
            }

            yield return FadeCanvas(canvasGroup, 0f, fadeDuration);
            isLoading = false;
            sceneOperation = null;
            gameObject.SetActive(false);
            LoadingFinished?.Invoke();
        }

        private void PrepareArtwork()
        {
            loadingImages = CompactSprites(loadingImages);
            RebuildImageOrder();
            if (firstArtwork != null)
            {
                firstArtwork.preserveAspect = false;
                firstArtwork.color = Color.white;
            }

            if (secondArtwork != null)
            {
                secondArtwork.preserveAspect = false;
                secondArtwork.color = Color.white;
            }

            if (continuePrompt != null)
            {
                continuePrompt.gameObject.SetActive(false);
            }

            if (progressFill != null)
            {
                progressFill.fillAmount = 0f;
            }

            if (hintGroup != null)
            {
                hintGroup.alpha = 0f;
            }

            if (firstArtwork != null && loadingImages.Length > 0)
            {
                firstArtwork.sprite = loadingImages[imageOrder[0]];
                firstArtwork.gameObject.SetActive(true);
                FitArtwork(firstArtwork);
                firstArtwork.rectTransform.localScale = Vector3.one;
                if (secondArtwork != null)
                {
                    secondArtwork.gameObject.SetActive(true);
                    secondArtwork.color = new Color(1f, 1f, 1f, 0f);
                }

                imageStartedAt = elapsedLoadingTime;
                imageLifetime = NextImageLifetime();
            }
        }

        private void StartPresentation()
        {
            if (artworkRoutine != null)
            {
                StopCoroutine(artworkRoutine);
            }
            if (hintRoutine != null)
            {
                StopCoroutine(hintRoutine);
            }

            if (loadingImages.Length > 1 && firstArtwork != null && secondArtwork != null)
            {
                artworkRoutine = StartCoroutine(AnimateArtwork());
            }

            hintIndex = -1;
            if (hints != null && hints.Length > 0 && hintText != null && hintGroup != null)
            {
                hintRoutine = StartCoroutine(RotateHints());
            }
        }

        private IEnumerator AnimateArtwork()
        {
            int orderPosition = 0;
            int currentIndex = imageOrder[0];
            Image visible = firstArtwork;
            Image incoming = secondArtwork;
            while (isLoading && !waitingForContinue)
            {
                AnimateKenBurns(visible, elapsedLoadingTime - imageStartedAt);
                if (elapsedLoadingTime - imageStartedAt < imageLifetime)
                {
                    yield return null;
                    continue;
                }

                orderPosition++;
                if (orderPosition >= imageOrder.Count)
                {
                    int previousIndex = currentIndex;
                    RebuildImageOrder(previousIndex);
                    orderPosition = 0;
                }

                currentIndex = imageOrder[orderPosition];
                incoming.sprite = loadingImages[currentIndex];
                incoming.color = Color.white;
                incoming.rectTransform.localScale = Vector3.one;
                FitArtwork(incoming);
                imageStartedAt = elapsedLoadingTime;
                imageLifetime = NextImageLifetime();
                yield return CrossfadeImages(visible, incoming, imageCrossfadeDuration);

                Image previousVisible = visible;
                visible = incoming;
                incoming = previousVisible;
                incoming.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        private IEnumerator CrossfadeImages(Image outgoing, Image incoming, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && isLoading && !waitingForContinue)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                float eased = t * t * (3f - 2f * t);
                outgoing.color = new Color(1f, 1f, 1f, 1f - eased);
                incoming.color = new Color(1f, 1f, 1f, eased);
                yield return null;
            }

            outgoing.color = new Color(1f, 1f, 1f, 0f);
            incoming.color = Color.white;
        }

        private void AnimateKenBurns(Image image, float elapsed)
        {
            if (image == null)
            {
                return;
            }

            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, imageLifetime));
            RectTransform rect = image.rectTransform;
            rect.localScale = Vector3.one * Mathf.Lerp(1f, kenBurnsScale, t);
            float angle = imageOrder.Count == 0 ? 0f : imageOrder[Mathf.Abs(imageOrder[0]) % imageOrder.Count];
            float pan = elapsed * (0.5f + angle * 0.12f);
            rect.anchoredPosition = new Vector2(
                Mathf.Sin(pan) * kenBurnsPan * t,
                Mathf.Cos(pan * 0.73f) * kenBurnsPan * 0.45f * t);
        }

        private IEnumerator RotateHints()
        {
            while (isLoading && !waitingForContinue)
            {
                hintIndex = (hintIndex + 1) % hints.Length;
                hintText.text = hints[hintIndex];
                yield return FadeCanvas(hintGroup, 1f, hintFadeDuration);
                float elapsed = 0f;
                while (elapsed < hintInterval && isLoading && !waitingForContinue)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return FadeCanvas(hintGroup, 0f, hintFadeDuration);
            }
        }

        private IEnumerator FadeArtworkToBlack()
        {
            if (vignetteOverlay == null)
            {
                CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    yield return FadeCanvas(canvasGroup, 1f, fadeDuration);
                }
                yield break;
            }

            Color color = vignetteOverlay.color;
            float startAlpha = color.a;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDuration));
                vignetteOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(startAlpha, 1f, t));
                yield return null;
            }

            vignetteOverlay.color = Color.black;
        }

        private void UpdateProgress()
        {
            if (sceneOperation == null || waitingForContinue)
            {
                return;
            }

            float actualProgress = Mathf.Clamp01(sceneOperation.progress / 0.9f);
            float nextProgress = Mathf.MoveTowards(
                displayedProgress, actualProgress, Time.unscaledDeltaTime * 0.24f);
            displayedProgress = Mathf.Max(displayedProgress, nextProgress);
            SetProgressVisuals();
        }

        private void SetProgressVisuals()
        {
            if (progressFill != null)
            {
                progressFill.fillAmount = displayedProgress;
            }

            if (progressText != null)
            {
                progressText.text = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture, progressFormat,
                    Mathf.RoundToInt(displayedProgress * 100f));
            }

            if (lanternGlow != null)
            {
                Color glow = lanternGlow.color;
                lanternGlow.color = new Color(glow.r, glow.g, glow.b, Mathf.Lerp(0.18f, 1f, displayedProgress));
                lanternGlow.transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.12f, displayedProgress);
            }
        }

        private void UpdateFog()
        {
            if (fogOverlay == null)
            {
                return;
            }

            RectTransform rect = fogOverlay.rectTransform;
            rect.anchoredPosition = new Vector2(
                fogStartX + Mathf.Repeat(elapsedLoadingTime * fogDriftSpeed, 180f),
                fogStartY + Mathf.Sin(elapsedLoadingTime * 0.08f) * 16f);
        }

        private void FitArtwork(Image image)
        {
            if (image == null || image.sprite == null || artworkViewport == null)
            {
                return;
            }

            Rect viewport = artworkViewport.rect;
            Vector2 spriteSize = image.sprite.rect.size;
            if (viewport.width <= 0f || viewport.height <= 0f || spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            float viewportAspect = viewport.width / viewport.height;
            float spriteAspect = spriteSize.x / spriteSize.y;
            float width = viewport.width;
            float height = viewport.height;
            if (spriteAspect > viewportAspect)
            {
                width = viewport.height * spriteAspect;
            }
            else
            {
                height = viewport.width / spriteAspect;
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }

        private void RebuildImageOrder(int avoidFirstIndex = -1)
        {
            imageOrder.Clear();
            for (int i = 0; i < loadingImages.Length; i++)
            {
                if (loadingImages[i] != null)
                {
                    imageOrder.Add(i);
                }
            }

            for (int i = imageOrder.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                int current = imageOrder[i];
                imageOrder[i] = imageOrder[swapIndex];
                imageOrder[swapIndex] = current;
            }

            if (imageOrder.Count > 1 && imageOrder[0] == avoidFirstIndex)
            {
                int first = imageOrder[0];
                imageOrder[0] = imageOrder[1];
                imageOrder[1] = first;
            }
        }

        private static Sprite[] CompactSprites(Sprite[] sprites)
        {
            if (sprites == null || sprites.Length == 0)
            {
                return Array.Empty<Sprite>();
            }

            List<Sprite> validSprites = new List<Sprite>(sprites.Length);
            foreach (Sprite sprite in sprites)
            {
                if (sprite != null)
                {
                    validSprites.Add(sprite);
                }
            }

            return validSprites.ToArray();
        }

        private float NextImageLifetime()
        {
            float minimum = Mathf.Max(0.1f, imageDurationMinimum);
            float maximum = Mathf.Max(minimum, imageDurationMaximum);
            return UnityEngine.Random.Range(minimum, maximum);
        }

        private void PlayAmbience()
        {
            if (ambienceSource == null || ambienceClip == null)
            {
                return;
            }

            ambienceSource.clip = ambienceClip;
            ambienceSource.volume = ambienceVolume;
            ambienceSource.loop = true;
            ambienceSource.Play();
        }

        private IEnumerator FadeAudio(AudioSource source, float targetVolume, float duration)
        {
            float initialVolume = source.volume;
            float elapsed = 0f;
            while (elapsed < duration && source != null)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(initialVolume, targetVolume,
                    Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
                yield return null;
            }
        }

        private static IEnumerator FadeCanvas(CanvasGroup group, float targetAlpha, float duration)
        {
            if (group == null)
            {
                yield break;
            }

            float initialAlpha = group.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                group.alpha = Mathf.Lerp(initialAlpha, targetAlpha, t * t * (3f - 2f * t));
                yield return null;
            }

            group.alpha = targetAlpha;
        }

        private static IEnumerator FadeImageAlpha(Image image, float targetAlpha, float duration)
        {
            if (image == null)
            {
                yield break;
            }

            Color initialColor = image.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                image.color = new Color(
                    initialColor.r, initialColor.g, initialColor.b,
                    Mathf.Lerp(initialColor.a, targetAlpha, t * t * (3f - 2f * t)));
                yield return null;
            }

            image.color = new Color(
                initialColor.r, initialColor.g, initialColor.b, targetAlpha);
        }

        private static bool ContinuePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame
                || Mouse.current.rightButton.wasPressedThisFrame
                || Mouse.current.middleButton.wasPressedThisFrame))
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame
                || gamepad.buttonNorth.wasPressedThisFrame
                || gamepad.buttonEast.wasPressedThisFrame
                || gamepad.buttonWest.wasPressedThisFrame
                || gamepad.startButton.wasPressedThisFrame
                || gamepad.selectButton.wasPressedThisFrame
                || gamepad.leftShoulder.wasPressedThisFrame
                || gamepad.rightShoulder.wasPressedThisFrame);
#else
            return Input.anyKeyDown || Input.GetMouseButtonDown(0)
                || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
#endif
        }
    }
}
