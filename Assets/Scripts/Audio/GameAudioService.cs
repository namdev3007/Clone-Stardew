using Referencing;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio
{
    public static class GameAudioService
    {
        private const string ConfigPath = "Audio/Game Audio Config";
        private static GameAudioConfig config;
        private static AudioSource audioSource;
        private static float lastHoverTime;
        private static float lastFootstepTime;
        private const float HoverCooldown = 0.05f;
        private const float FootstepCooldown = 0.22f;

        private static void EnsureInitialized()
        {
            if (config == null)
            {
                config = Resources.Load<GameAudioConfig>(ConfigPath);
            }

            if (audioSource == null)
            {
                GameObject root = new GameObject("Game Audio Service Source");
                Object.DontDestroyOnLoad(root);
                audioSource = root.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 0f; // 2D sound

                if (config != null && config.fxMixerGroup != null)
                {
                    audioSource.outputAudioMixerGroup = config.fxMixerGroup;
                }
            }
            else if (audioSource.outputAudioMixerGroup == null && config != null && config.fxMixerGroup != null)
            {
                audioSource.outputAudioMixerGroup = config.fxMixerGroup;
            }
        }

        private static void PlayOneShot(AudioClip clip, float volume = 1f, float pitchMin = 1f, float pitchMax = 1f)
        {
            if (clip == null)
                return;

            EnsureInitialized();
            if (audioSource == null || !audioSource.enabled)
                return;

            if (Mathf.Approximately(pitchMin, 1f) && Mathf.Approximately(pitchMax, 1f))
            {
                audioSource.pitch = 1f;
            }
            else
            {
                audioSource.pitch = Random.Range(pitchMin, pitchMax);
            }

            audioSource.PlayOneShot(clip, volume);
        }

        public static void PlayDialogue()
        {
            EnsureInitialized();
            PlayOneShot(config?.dialogueSfx, 1f, 0.98f, 1.02f);
        }

        public static void PlayFertilize()
        {
            EnsureInitialized();
            PlayOneShot(config?.fertilizeSfx, 1f, 0.95f, 1.05f);
        }

        public static void PlayWatering()
        {
            EnsureInitialized();
            PlayOneShot(config?.waterSfx, 1f);
        }

        public static void PlayRefillWater()
        {
            EnsureInitialized();
            PlayOneShot(config?.refillWaterSfx, 1f);
        }

        public static void PlayPlantSeed()
        {
            EnsureInitialized();
            PlayOneShot(config?.plantSeedSfx, 1f, 0.95f, 1.05f);
        }

        public static void PlayHarvest()
        {
            EnsureInitialized();
            PlayOneShot(config?.harvestSfx, 1f);
        }

        public static void PlayItemPickup()
        {
            EnsureInitialized();
            PlayOneShot(config?.itemPickupSfx, 0.85f, 0.96f, 1.04f);
        }

        public static void PlaySlotClick()
        {
            EnsureInitialized();
            PlayOneShot(config?.slotClickSfx, 1f, 0.98f, 1.02f);
        }

        public static void PlaySlotHover()
        {
            float now = UnityEngine.Time.unscaledTime;
            if (now - lastHoverTime < HoverCooldown)
                return;
            lastHoverTime = now;

            EnsureInitialized();
            PlayOneShot(config?.slotHoverSfx, 0.7f, 0.98f, 1.02f);
        }

        public static void PlayMenuButtonClick()
        {
            EnsureInitialized();
            PlayOneShot(config?.menuButtonClickSfx, 1f);
        }

        public static void PlayUnlockLand()
        {
            EnsureInitialized();
            PlayOneShot(config?.unlockLandSfx, 1f);
        }

        public static void PlayCoin()
        {
            EnsureInitialized();
            PlayOneShot(config?.coinSfx, 1f, 0.96f, 1.04f);
        }

        public static void PlayChopSwing()
        {
            EnsureInitialized();
            PlayOneShot(config?.chopSwingSfx, 1f, 0.94f, 1.06f);
        }

        public static void PlayChopTree()
        {
            EnsureInitialized();
            PlayOneShot(config?.chopTreeSfx, 1f, 0.95f, 1.05f);
        }

        public static void PlayChopFoliage()
        {
            EnsureInitialized();
            PlayOneShot(config?.chopFoliageSfx, 1f, 0.95f, 1.05f);
        }

        public static void PlayFootstep()
        {
            float now = UnityEngine.Time.time;
            if (now - lastFootstepTime < FootstepCooldown)
                return;
            lastFootstepTime = now;

            EnsureInitialized();
            if (config != null && config.footstepCollection != null && audioSource != null)
            {
                config.footstepCollection.Play(audioSource);
            }
        }
    }
}
