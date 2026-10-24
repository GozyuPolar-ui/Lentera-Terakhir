using System.Collections;
using UnityEngine;

namespace JRPG.Menu
{
    public sealed class MenuAudio : MonoBehaviour
    {
        public AudioSource musicSource;
        public AudioSource ambienceSource;
        public AudioSource uiSource;
        public AudioClip musicClip;
        public AudioClip ambienceClip;
        public AudioClip hoverClip;
        public AudioClip clickClip;
        [Range(0f, 1f)] public float musicVolume = 0.7f;
        [Range(0f, 1f)] public float ambienceVolume = 0.55f;
        [Range(0f, 1f)] public float uiVolume = 0.8f;
        public float fadeInDuration = 2.5f;

        private float musicFadeProgress;
        private float ambienceFadeProgress;

        private void Awake()
        {
            if (ambienceClip == null)
            {
                ambienceClip = CreateProceduralAmbience();
            }
        }

        private void Start()
        {
            ConfigureSource(musicSource, musicClip);
            ConfigureSource(ambienceSource, ambienceClip);
            if (musicSource != null && musicClip != null)
            {
                musicSource.volume = 0f;
                musicSource.Play();
                StartCoroutine(FadeMusic());
            }

            if (ambienceSource != null && ambienceClip != null)
            {
                ambienceSource.volume = 0f;
                ambienceSource.Play();
                StartCoroutine(FadeAmbience());
            }
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicSource != null)
            {
                musicSource.volume = musicVolume * musicFadeProgress;
            }
        }

        public void SetAmbienceVolume(float volume)
        {
            ambienceVolume = Mathf.Clamp01(volume);
            if (ambienceSource != null)
            {
                ambienceSource.volume = ambienceVolume * ambienceFadeProgress;
            }
        }

        public void PlayHover()
        {
            PlayUi(hoverClip);
        }

        public void PlayClick()
        {
            PlayUi(clickClip);
        }

        private void PlayUi(AudioClip clip)
        {
            if (uiSource != null && clip != null)
            {
                uiSource.PlayOneShot(clip, uiVolume);
            }
        }

        private void ConfigureSource(AudioSource source, AudioClip clip)
        {
            if (source == null)
            {
                return;
            }

            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        private IEnumerator FadeMusic()
        {
            yield return FadeSource(musicSource, true);
        }

        private IEnumerator FadeAmbience()
        {
            yield return FadeSource(ambienceSource, false);
        }

        private IEnumerator FadeSource(AudioSource source, bool music)
        {
            float elapsed = 0f;
            if (music)
            {
                musicFadeProgress = 0f;
            }
            else
            {
                ambienceFadeProgress = 0f;
            }

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeInDuration));
                float targetVolume = music ? musicVolume : ambienceVolume;
                source.volume = targetVolume * progress;
                if (music)
                {
                    musicFadeProgress = progress;
                }
                else
                {
                    ambienceFadeProgress = progress;
                }
                yield return null;
            }

            if (music)
            {
                musicFadeProgress = 1f;
                source.volume = musicVolume;
            }
            else
            {
                ambienceFadeProgress = 1f;
                source.volume = ambienceVolume;
            }
        }

        private static AudioClip CreateProceduralAmbience()
        {
            const int sampleRate = 22050;
            const int durationSeconds = 6;
            int sampleCount = sampleRate * durationSeconds;
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(9401);
            float lowNoise = 0f;
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                lowNoise += (noise - lowNoise) * 0.035f;
                phase += 2f * Mathf.PI * 0.17f / sampleRate;
                float swell = 0.62f + 0.38f * Mathf.Sin(phase);
                samples[i] = (lowNoise * 0.78f + Mathf.Sin(phase * 2.7f) * 0.22f) * swell * 0.16f;
            }

            int crossfade = sampleRate / 3;
            for (int i = 0; i < crossfade; i++)
            {
                float t = (float)i / crossfade;
                float blend = Mathf.Lerp(samples[sampleCount - crossfade + i], samples[i], t);
                samples[i] = blend;
                samples[sampleCount - crossfade + i] = blend;
            }

            AudioClip clip = AudioClip.Create(
                "Procedural Forest Night Ambience", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
