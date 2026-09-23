using Referencing;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio
{
    [CreateAssetMenu(fileName = "Game Audio Config", menuName = "Audio/Game Audio Config")]
    public class GameAudioConfig : ScriptableObject
    {
        [Header("Mixer Group")]
        public AudioMixerGroup fxMixerGroup;

        [Header("Dialogue & NPC")]
        public AudioClip dialogueSfx;

        [Header("Farming Actions")]
        public AudioClip fertilizeSfx;
        public AudioClip waterSfx;
        public AudioClip refillWaterSfx;
        public AudioClip plantSeedSfx;
        public AudioClip harvestSfx;
        public AudioClip itemPickupSfx;

        [Header("UI Interactions")]
        public AudioClip slotClickSfx;
        public AudioClip slotHoverSfx;
        public AudioClip menuButtonClickSfx;
        public AudioClip unlockLandSfx;
        public AudioClip coinSfx;

        [Header("Axe / Chopping")]
        public AudioClip chopSwingSfx;
        public AudioClip chopTreeSfx;
        public AudioClip chopFoliageSfx;

        [Header("Footsteps")]
        public SoundCollection footstepCollection;
    }
}
