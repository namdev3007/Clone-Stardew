using System;
using System.Linq;
using Action.Actions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace User_Interface
{
    [DisallowMultipleComponent]
    public sealed class SettingsSoundUI : MonoBehaviour
    {
        private const string MasterVolumeKey = "Master Volume";
        private const string LanguageKey = "Game Language";
        private const float MutedThreshold = 0.001f;

        public static event System.Action<bool> LanguageChanged;
        public static bool UseVietnamese => PlayerPrefs.GetInt(LanguageKey, 0) == 1;

        [SerializeField] private Slider mainSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Image mainHandleIcon;
        [SerializeField] private Image musicHandleIcon;
        [SerializeField] private Image sfxHandleIcon;
        [SerializeField] private Sprite soundOnSprite;
        [SerializeField] private Sprite soundOffSprite;
        [SerializeField] private ActionPlaySound soundAction;
        [SerializeField] private Button vietnameseButton;
        [SerializeField] private Button englishButton;
        [SerializeField] private Image vietnameseButtonImage;
        [SerializeField] private Image englishButtonImage;
        [SerializeField] private TMP_Text vietnameseLabel;
        [SerializeField] private TMP_Text englishLabel;
        [SerializeField] private TMP_Text controlsText;

        [System.Serializable]
        private sealed class SavedSoundConfig
        {
            public bool fxEnabled = true;
            public bool musicEnabled = true;
            public float fxVolume = 1f;
            public float musicVolume = 0.5f;
        }

        private float lastMainVolume = 1f;
        private float lastMusicVolume = 0.5f;
        private float lastSfxVolume = 1f;
        private bool valuesLoaded;

        public void Configure(Slider main, Slider music, Slider sfx, Sprite onSprite, Sprite offSprite,
            ActionPlaySound action,
            Image mainHandle = null, Image musicHandle = null, Image sfxHandle = null)
        {
            mainSlider = main;
            musicSlider = music;
            sfxSlider = sfx;
            mainHandleIcon = mainHandle;
            musicHandleIcon = musicHandle;
            sfxHandleIcon = sfxHandle;
            soundOnSprite = onSprite;
            soundOffSprite = offSprite;
            soundAction = action;
        }

        private void Awake()
        {
            FindInteractiveReferences();
        }

        private void OnEnable()
        {
            if (vietnameseButton == null || englishButton == null)
            {
                FindInteractiveReferences();
            }
            LoadValues();
            BindListeners();
        }

        private void OnDisable()
        {
            UnbindListeners();
            SaveSettings();
        }

        public void SaveSettings()
        {
            if (!valuesLoaded)
                return;
            if (mainSlider != null)
                PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(mainSlider.value));
            SavedSoundConfig saved = ReadSoundConfig();
            if (musicSlider != null)
            {
                saved.musicVolume = Mathf.Clamp01(musicSlider.value);
                saved.musicEnabled = saved.musicVolume > MutedThreshold;
            }
            if (sfxSlider != null)
            {
                saved.fxVolume = Mathf.Clamp01(sfxSlider.value);
                saved.fxEnabled = saved.fxVolume > MutedThreshold;
            }
            PlayerPrefs.SetString("config", JsonUtility.ToJson(saved));
            PlayerPrefs.Save();
        }

        private void BindListeners()
        {
            UnbindListeners();
            mainSlider?.onValueChanged.AddListener(SetMainVolume);
            musicSlider?.onValueChanged.AddListener(SetMusicVolume);
            sfxSlider?.onValueChanged.AddListener(SetSfxVolume);
            vietnameseButton?.onClick.AddListener(SelectVietnamese);
            englishButton?.onClick.AddListener(SelectEnglish);
        }

        private void UnbindListeners()
        {
            mainSlider?.onValueChanged.RemoveListener(SetMainVolume);
            musicSlider?.onValueChanged.RemoveListener(SetMusicVolume);
            sfxSlider?.onValueChanged.RemoveListener(SetSfxVolume);
            vietnameseButton?.onClick.RemoveListener(SelectVietnamese);
            englishButton?.onClick.RemoveListener(SelectEnglish);
        }

        private void LoadValues()
        {
            float main = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            SavedSoundConfig saved = ReadSoundConfig();

            mainSlider?.SetValueWithoutNotify(main);
            musicSlider?.SetValueWithoutNotify(saved.musicEnabled ? Mathf.Clamp01(saved.musicVolume) : 0f);
            sfxSlider?.SetValueWithoutNotify(saved.fxEnabled ? Mathf.Clamp01(saved.fxVolume) : 0f);

            AudioListener.volume = main;
            lastMainVolume = main > MutedThreshold ? main : 1f;
            lastMusicVolume = saved.musicVolume > MutedThreshold ? saved.musicVolume : 0.5f;
            lastSfxVolume = saved.fxVolume > MutedThreshold ? saved.fxVolume : 1f;
            RefreshIcons();
            ApplyLanguageVisuals(UseVietnamese, false);
            valuesLoaded = true;
        }

        private static SavedSoundConfig ReadSoundConfig()
        {
            string json = PlayerPrefs.GetString("config", string.Empty);
            if (string.IsNullOrEmpty(json))
                return new SavedSoundConfig();

            SavedSoundConfig saved = JsonUtility.FromJson<SavedSoundConfig>(json);
            return saved ?? new SavedSoundConfig();
        }

        private void SetMainVolume(float value)
        {
            value = Mathf.Clamp01(value);
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
            if (value > MutedThreshold) lastMainVolume = value;
            SetStateIcon(mainHandleIcon, value);
        }

        private void SetMusicVolume(float value)
        {
            soundAction?.SetMusicEnabled(value > MutedThreshold);
            soundAction?.SetMusicVolume(Mathf.Clamp01(value));
            if (value > MutedThreshold) lastMusicVolume = value;
            SetStateIcon(musicHandleIcon, value);
            SaveSettings();
        }

        private void SetSfxVolume(float value)
        {
            soundAction?.SetFXEnabled(value > MutedThreshold);
            soundAction?.SetFXVolume(Mathf.Clamp01(value));
            if (value > MutedThreshold) lastSfxVolume = value;
            SetStateIcon(sfxHandleIcon, value);
            SaveSettings();
        }

        private void SelectVietnamese() => SetLanguage(true);
        private void SelectEnglish() => SetLanguage(false);

        private void SetLanguage(bool vietnamese)
        {
            PlayerPrefs.SetInt(LanguageKey, vietnamese ? 1 : 0);
            PlayerPrefs.Save();
            ApplyLanguageVisuals(vietnamese, true);
        }

        private void ApplyLanguageVisuals(bool vietnamese, bool notify)
        {
            foreach (LocalizedSpriteButton title in GetComponentsInChildren<LocalizedSpriteButton>(true))
            {
                title.RefreshLanguage();
            }

            Color selected = new Color32(255, 232, 145, 255);
            Color normal = new Color32(40, 27, 18, 255);
            if (vietnameseButtonImage != null)
                vietnameseButtonImage.color = vietnamese ? Color.white : new Color32(170, 170, 170, 255);
            if (englishButtonImage != null)
                englishButtonImage.color = vietnamese ? new Color32(170, 170, 170, 255) : Color.white;
            if (vietnameseLabel != null)
            {
                vietnameseLabel.color = vietnamese ? selected : normal;
                vietnameseLabel.fontStyle = vietnamese ? FontStyles.Bold : FontStyles.Normal;
            }
            if (englishLabel != null)
            {
                englishLabel.color = vietnamese ? normal : selected;
                englishLabel.fontStyle = vietnamese ? FontStyles.Normal : FontStyles.Bold;
            }
            if (controlsText != null)
            {
                controlsText.text = vietnamese
                    ? "W/A/S/D   DI CHUYỂN\n1/2/3/4/5   Ô ĐỒ\nB   TÚI\nESC   TẠM DỪNG / LÙI\nO   TƯƠNG TÁC\nCHUỘT TRÁI   DÙNG CÔNG CỤ\nCHUỘT PHẢI   TƯƠNG TÁC / HỦY Ô ĐẤT"
                    : "W/A/S/D   MOVEMENT\n1/2/3/4/5   SLOTS\nB   BAG\nESC   PAUSE / BACK\nO   INTERACT\nLEFT MOUSE   USE TOOL\nRIGHT MOUSE   INTERACT / CANCEL SOIL";

            }
            if (notify)
                LanguageChanged?.Invoke(vietnamese);
        }

        private void ToggleMainMute() => ToggleSlider(mainSlider, ref lastMainVolume);
        private void ToggleMusicMute() => ToggleSlider(musicSlider, ref lastMusicVolume);
        private void ToggleSfxMute() => ToggleSlider(sfxSlider, ref lastSfxVolume);

        private static void ToggleSlider(Slider slider, ref float previousVolume)
        {
            if (slider == null) return;
            if (slider.value <= MutedThreshold)
                slider.value = Mathf.Max(0.01f, previousVolume);
            else
            {
                previousVolume = slider.value;
                slider.value = 0f;
            }
        }

        private void FindInteractiveReferences()
        {
            Transform vietnamese = FindDescendant("Language Vietnamese");
            Transform english = FindDescendant("Language English");
            vietnameseLabel = vietnamese?.GetComponent<TMP_Text>();
            englishLabel = english?.GetComponent<TMP_Text>();
            vietnameseButtonImage = vietnamese?.GetComponent<Image>();
            englishButtonImage = english?.GetComponent<Image>();
            controlsText = FindDescendant("Controls List")?.GetComponent<TMP_Text>();
            mainHandleIcon = GetSliderHandleImage(mainSlider, mainHandleIcon);
            musicHandleIcon = GetSliderHandleImage(musicSlider, musicHandleIcon);
            sfxHandleIcon = sfxSlider != null && sfxSlider.handleRect != null
                ? sfxSlider.handleRect.GetComponent<Image>()
                : sfxHandleIcon;
            vietnameseButton = EnsureLanguageButton(vietnamese, vietnameseLabel, vietnameseButtonImage);
            englishButton = EnsureLanguageButton(english, englishLabel, englishButtonImage);
        }

        private static Button EnsureLanguageButton(Transform target, TMP_Text label, Image image)
        {
            if (target == null) return null;
            if (label != null) label.raycastTarget = true;
            if (image != null) image.raycastTarget = true;
            Button button = target.GetComponent<Button>() ?? target.gameObject.AddComponent<Button>();
            button.targetGraphic = image != null ? image : label;
            return button;
        }

        private static Image GetSliderHandleImage(Slider slider, Image fallback)
        {
            return slider != null && slider.handleRect != null
                ? slider.handleRect.GetComponent<Image>()
                : fallback;
        }

        private Transform FindDescendant(string objectName)
        {
            return GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == objectName);
        }

        private void RefreshIcons()
        {
            if (mainSlider != null) SetStateIcon(mainHandleIcon, mainSlider.value);
            if (musicSlider != null) SetStateIcon(musicHandleIcon, musicSlider.value);
            if (sfxSlider != null) SetStateIcon(sfxHandleIcon, sfxSlider.value);
        }

        private void SetStateIcon(Image icon, float volume)
        {
            if (icon != null)
                icon.sprite = volume <= MutedThreshold ? soundOffSprite : soundOnSprite;
        }
    }
}
