#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using JRPG.Menu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JRPG.EditorTools
{
    public static class LoadingScreenPrefabBuilder
    {
        private const string ImageFolder = "Assets/LoadingScreen";
        private const string ResourcesFolder = "Assets/Resources";
        private const string PrefabPath = "Assets/Resources/LoadingScreen.prefab";
        private const string FogTexturePath = "Assets/Resources/LoadingScreenFog.png";
        private const string VignetteTexturePath = "Assets/Resources/LoadingScreenVignette.png";
        private const string DefaultFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        [MenuItem("Tools/Lentera/Build Loading Screen Prefab")]
        private static void BuildFromMenu()
        {
            Build();
        }

        public static GameObject Build()
        {
            EnsureFolder(ResourcesFolder);
            ConfigureLoadingImageImports();
            Texture2D fogTexture = LoadOrCreateFogTexture();
            Sprite vignetteSprite = LoadOrCreateVignetteSprite();
            Sprite[] loadingSprites = LoadLoadingSprites();
            TMP_FontAsset font = LoadFont();

            GameObject canvasObject = new GameObject(
                "LoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            CanvasGroup canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            Image background = CreateImage(canvasObject.transform, "Dark_Background",
                new Color(0.018f, 0.026f, 0.038f));
            Stretch(background.rectTransform);

            RectTransform viewportRect = CreateRect(
                canvasObject.transform, "ArtworkViewport",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewportRect.gameObject.AddComponent<RectMask2D>();
            Image firstArtwork = CreateArtwork(viewportRect, "Artwork_A");
            Image secondArtwork = CreateArtwork(viewportRect, "Artwork_B");
            secondArtwork.color = new Color(1f, 1f, 1f, 0f);

            Image vignette = CreateImage(canvasObject.transform, "Vignette", Color.white);
            Stretch(vignette.rectTransform);
            vignette.sprite = vignetteSprite;
            vignette.type = Image.Type.Simple;
            vignette.raycastTarget = false;

            RawImage fog = CreateRawImage(canvasObject.transform, "Drifting_Fog",
                new Color(0.72f, 0.8f, 0.9f, 0.17f));
            Stretch(fog.rectTransform);
            fog.texture = fogTexture;
            fog.raycastTarget = false;
            fog.uvRect = new Rect(0f, 0f, 2f, 1f);

            CreateImage(canvasObject.transform, "Lower_Shade",
                new Color(0.005f, 0.009f, 0.015f, 0.62f), new Vector2(0f, 0f),
                new Vector2(1f, 0.34f));

            TextMeshProUGUI hint = CreateText(
                canvasObject.transform, "Hint", string.Empty, font, 26f,
                new Color(0.87f, 0.88f, 0.86f), TextAlignmentOptions.Left,
                new Vector2(0.095f, 0.155f), new Vector2(0.83f, 0.235f));
            CanvasGroup hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintGroup.alpha = 0f;

            TextMeshProUGUI prompt = CreateText(
                canvasObject.transform, "ContinuePrompt",
                "Tekan tombol apa saja untuk melanjutkan", font, 22f,
                new Color(1f, 0.82f, 0.57f), TextAlignmentOptions.Center,
                new Vector2(0.23f, 0.255f), new Vector2(0.77f, 0.315f));
            prompt.gameObject.SetActive(false);

            CreateImage(canvasObject.transform, "Progress_Background",
                new Color(0.72f, 0.74f, 0.78f, 0.28f), new Vector2(0.095f, 0.095f),
                new Vector2(0.905f, 0.099f));
            Image progress = CreateImage(canvasObject.transform, "Progress_Fill",
                new Color(0.91f, 0.58f, 0.32f), new Vector2(0.095f, 0.095f),
                new Vector2(0.905f, 0.099f));
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillOrigin = (int)Image.OriginHorizontal.Left;
            progress.fillAmount = 0f;

            Image glow = CreateImage(canvasObject.transform, "Progress_Lantern_Glow",
                new Color(1f, 0.63f, 0.28f, 0.18f), new Vector2(0.089f, 0.075f),
                new Vector2(0.115f, 0.125f));
            glow.color = new Color(1f, 0.58f, 0.22f, 0.2f);
            glow.raycastTarget = false;

            TextMeshProUGUI progressText = CreateText(
                canvasObject.transform, "Progress_Text", "0%", font, 16f,
                new Color(0.82f, 0.83f, 0.82f), TextAlignmentOptions.Right,
                new Vector2(0.83f, 0.115f), new Vector2(0.905f, 0.155f));

            Image fadeToBlack = CreateImage(canvasObject.transform, "Fade_To_Black",
                Color.black, Vector2.zero, Vector2.one);
            fadeToBlack.raycastTarget = true;
            fadeToBlack.transform.SetAsLastSibling();

            AudioSource ambienceSource = canvasObject.AddComponent<AudioSource>();
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.volume = 0f;

            LoadingScreenController controller =
                canvasObject.AddComponent<LoadingScreenController>();
            controller.defaultSceneName = "ForestPembuka";
            controller.firstArtwork = firstArtwork;
            controller.secondArtwork = secondArtwork;
            controller.artworkViewport = viewportRect;
            controller.loadingImages = loadingSprites;
            controller.vignetteOverlay = fadeToBlack;
            controller.fogOverlay = fog;
            controller.progressFill = progress;
            controller.lanternGlow = glow;
            controller.progressText = progressText;
            controller.hintText = hint;
            controller.hintGroup = hintGroup;
            controller.continuePrompt = prompt;
            controller.ambienceSource = ambienceSource;
            controller.ambienceClip = FindAmbienceClip();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
            UnityEngine.Object.DestroyImmediate(canvasObject);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab == null)
            {
                throw new InvalidOperationException("Failed to create " + PrefabPath + ".");
            }

            if (loadingSprites.Length == 0)
            {
                Debug.LogWarning(
                    "Loading Screen Builder: no images were found in Assets/LoadingScreen. " +
                    "The prefab will use its dark fallback background.");
            }

            Debug.Log("Loading Screen Builder: created " + PrefabPath
                + " with " + loadingSprites.Length + " loading image(s).");
            return prefab;
        }

        private static void ConfigureLoadingImageImports()
        {
            if (!AssetDatabase.IsValidFolder(ImageFolder))
            {
                Debug.LogWarning("Loading Screen Builder: Assets/LoadingScreen folder was not found.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ImageFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, 4096);
                importer.SaveAndReimport();
            }
        }

        private static Sprite[] LoadLoadingSprites()
        {
            if (!AssetDatabase.IsValidFolder(ImageFolder))
            {
                return Array.Empty<Sprite>();
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ImageFolder });
            List<Sprite> sprites = new List<Sprite>(guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }

            return sprites.ToArray();
        }

        private static TMP_FontAsset LoadFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
            if (font != null && font.material != null && font.atlasTextures != null
                && font.atlasTextures.Length > 0
                && font.atlasTextures[0] != null)
            {
                return font;
            }

            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (string guid in guids)
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null && font.material != null && font.atlasTextures != null
                    && font.atlasTextures.Length > 0 && font.atlasTextures[0] != null)
                {
                    return font;
                }
            }

            throw new InvalidOperationException(
                "Loading Screen Builder: import TMP Essential Resources before creating the prefab.");
        }

        private static Texture2D LoadOrCreateFogTexture()
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Textures/MenuGroundFog.png");
            if (existing != null)
            {
                return existing;
            }

            return CreateRadialTexture(FogTexturePath, false, 96);
        }

        private static Sprite LoadOrCreateVignetteSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(VignetteTexturePath) == null)
            {
                CreateRadialTexture(VignetteTexturePath, true, 256);
            }

            TextureImporter importer = AssetImporter.GetAtPath(VignetteTexturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Loading Screen Builder: vignette texture importer could not be found.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(VignetteTexturePath);
            if (sprite == null)
            {
                throw new InvalidOperationException("Loading Screen Builder: vignette sprite could not be imported.");
            }

            return sprite;
        }

        private static Texture2D CreateRadialTexture(string path, bool vignette, int size)
        {
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = ((x + 0.5f) / size) * 2f - 1f;
                    float v = ((y + 0.5f) / size) * 2f - 1f;
                    float radius = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v) / 1.4142f);
                    float alpha = vignette
                        ? Mathf.SmoothStep(0f, 0.7f, radius) * 0.74f
                        : Mathf.Pow(Mathf.Clamp01(1f - radius), 2.4f) * 0.65f;
                    float color = vignette ? 0f : 1f;
                    pixels[y * size + x] = new Color(color, color, color, alpha);
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = vignette
                    ? TextureImporterType.Sprite
                    : TextureImporterType.Default;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static AudioClip FindAmbienceClip()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.IndexOf("ambience", StringComparison.OrdinalIgnoreCase) >= 0
                    || fileName.IndexOf("wind", StringComparison.OrdinalIgnoreCase) >= 0
                    || fileName.IndexOf("forestbgm", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
            }

            return null;
        }

        private static Image CreateArtwork(Transform parent, string name)
        {
            Image image = CreateImage(parent, name, Color.white);
            image.raycastTarget = false;
            image.preserveAspect = false;
            return image;
        }

        private static Image CreateImage(
            Transform parent, string name, Color color,
            Vector2? anchorMin = null, Vector2? anchorMax = null)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchorMin ?? Vector2.zero;
            rect.anchorMax = anchorMax ?? Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        private static RawImage CreateRawImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(parent, false);
            RawImage image = imageObject.GetComponent<RawImage>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent, string name, string text, TMP_FontAsset font,
            float fontSize, Color color, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
            textComponent.font = font;
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.color = color;
            textComponent.alignment = alignment;
            textComponent.raycastTarget = false;
            textComponent.textWrappingMode = TextWrappingModes.Normal;
            Stretch(textComponent.rectTransform, anchorMin, anchorMax);
            return textComponent;
        }

        private static RectTransform CreateRect(
            Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.transform.SetParent(parent, false);
            RectTransform rect = rectObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.one);
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrEmpty(parent))
            {
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }
    }
}
#endif
