using UnityEngine;
using UnityEngine.UI;

namespace User_Interface
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LocalizedSpriteButton : MonoBehaviour
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private Button targetButton;
        [SerializeField] private Sprite englishNormal;
        [SerializeField] private Sprite englishSelected;
        [SerializeField] private Sprite vietnameseNormal;
        [SerializeField] private Sprite vietnameseSelected;
        [SerializeField] private bool useNativeSize;
        [Header("Optional per-language size (zero keeps the authored size)")]
        [SerializeField] private Vector2 englishSize;
        [SerializeField] private Vector2 vietnameseSize;
        [Header("Optional per-language position (if set, overrides anchoredPosition)")]
        [SerializeField] private Vector2 englishPosition;
        [SerializeField] private Vector2 vietnamesePosition;

        public void Configure(Image image, Button button, Sprite enNormal, Sprite enSelected,
            Sprite viNormal, Sprite viSelected, bool resize)
        {
            targetImage = image;
            targetButton = button;
            englishNormal = enNormal;
            englishSelected = enSelected;
            vietnameseNormal = viNormal;
            vietnameseSelected = viSelected;
            useNativeSize = resize;
            Apply(SettingsSoundUI.UseVietnamese);
        }

        /// <summary>
        /// Sizes for artwork whose two language versions have different
        /// proportions, so neither of them ends up stretched.
        /// </summary>
        public void ConfigureSizes(Vector2 english, Vector2 vietnamese)
        {
            englishSize = english;
            vietnameseSize = vietnamese;
            Apply(SettingsSoundUI.UseVietnamese);
        }

        public void ConfigurePositions(Vector2 english, Vector2 vietnamese)
        {
            englishPosition = english;
            vietnamesePosition = vietnamese;
            Apply(SettingsSoundUI.UseVietnamese);
        }

        public void ConfigureHeaderSizesIfUnset(Vector2 authoredEnglishSize)
        {
            if (englishSize.x > 0f && englishSize.y > 0f &&
                vietnameseSize.x > 0f && vietnameseSize.y > 0f)
                return;

            englishSize = authoredEnglishSize;
            float height = Mathf.Max(1f, authoredEnglishSize.y);
            vietnameseSize = vietnameseNormal != null && vietnameseNormal.rect.height > 0f
                ? new Vector2(height * vietnameseNormal.rect.width / vietnameseNormal.rect.height, height)
                : authoredEnglishSize;
            Apply(SettingsSoundUI.UseVietnamese);
        }

        public void RefreshLanguage() => Apply(SettingsSoundUI.UseVietnamese);

        private void Awake()
        {
            if (targetImage == null)
                targetImage = GetComponent<Image>();
            if (targetButton == null)
                targetButton = GetComponent<Button>();
        }

        private void OnEnable()
        {
            SettingsSoundUI.LanguageChanged += Apply;
            Apply(SettingsSoundUI.UseVietnamese);
        }

        private void OnDisable()
        {
            SettingsSoundUI.LanguageChanged -= Apply;
        }

        private void Apply(bool vietnamese)
        {
            if (targetImage == null)
                return;

            Sprite normal = vietnamese && vietnameseNormal != null ? vietnameseNormal : englishNormal;
            Sprite selected = vietnamese && vietnameseSelected != null ? vietnameseSelected : englishSelected;
            if (normal != null)
                targetImage.sprite = normal;

            if (targetButton != null)
            {
                SpriteState state = targetButton.spriteState;
                state.highlightedSprite = selected != null ? selected : normal;
                state.selectedSprite = selected != null ? selected : normal;
                state.pressedSprite = selected != null ? selected : normal;
                targetButton.spriteState = state;
            }

            Vector2 languageSize = vietnamese ? vietnameseSize : englishSize;
            if (!useNativeSize &&
                languageSize.x > 0f && languageSize.y > 0f &&
                targetImage.rectTransform != null)
            {
                targetImage.rectTransform.sizeDelta = languageSize;
            }

            // Preserving authored layout: do not override anchoredPosition with absolute coordinates
            // at runtime, so manual adjustments made in Edit Mode remain intact.

            if (useNativeSize && normal != null && targetImage.rectTransform != null)
            {
                targetImage.rectTransform.sizeDelta = normal.rect.size;
                RectTransform indicator = transform.Find("Hover Indicator") as RectTransform;
                if (indicator != null)
                    indicator.anchoredPosition = new Vector2(
                        -(targetImage.rectTransform.sizeDelta.x * 0.5f + indicator.sizeDelta.x * 0.5f + 4f), 0f);
            }
        }
    }
}
