using UnityEngine;
using UnityEngine.UI;

namespace User_Interface
{
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
