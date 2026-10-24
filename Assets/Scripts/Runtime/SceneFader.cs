using System.Collections;
using UnityEngine;

namespace JRPG.Menu
{
    public sealed class SceneFader : MonoBehaviour
    {
        public CanvasGroup overlayGroup;
        public float fadeInDuration = 1.4f;
        public float fadeOutDuration = 0.9f;

        private Coroutine activeFade;

        private void Start()
        {
            if (overlayGroup != null)
            {
                overlayGroup.alpha = 1f;
                FadeIn();
            }
        }

        public void FadeIn()
        {
            FadeTo(0f, fadeInDuration);
        }

        public IEnumerator FadeOut()
        {
            yield return FadeTo(1f, fadeOutDuration);
        }

        public Coroutine FadeTo(float targetAlpha, float duration)
        {
            if (overlayGroup == null)
            {
                return null;
            }

            if (activeFade != null)
            {
                StopCoroutine(activeFade);
            }

            activeFade = StartCoroutine(Animate(targetAlpha, duration));
            return activeFade;
        }

        private IEnumerator Animate(float targetAlpha, float duration)
        {
            float startAlpha = overlayGroup.alpha;
            float elapsed = 0f;
            overlayGroup.blocksRaycasts = true;
            overlayGroup.interactable = true;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                t = t * t * (3f - 2f * t);
                overlayGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            overlayGroup.alpha = targetAlpha;
            bool opaque = targetAlpha > 0.99f;
            overlayGroup.blocksRaycasts = opaque;
            overlayGroup.interactable = opaque;
            activeFade = null;
        }
    }
}
