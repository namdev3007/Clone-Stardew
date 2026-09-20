using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace User_Interface
{
    /// <summary>
    /// Builds a gameplay confirmation window that matches the pause menu's Quit
    /// Confirmation UI: the same panel art, the same yes/no buttons and the same
    /// text style. Gameplay prompts use this instead of the old system window.
    /// </summary>
    public static class YesNoDialogFactory
    {
        private static readonly Vector2 PanelSize = new Vector2(205f, 119f);
        private static readonly Vector2 QuestionSize = new Vector2(174f, 28f);
        private static readonly Vector2 ButtonSize = new Vector2(66f, 33f);
        private static readonly Vector2 QuestionPosition = new Vector2(0f, 24f);
        private static readonly Vector2 YesPosition = new Vector2(-43f, -27f);
        private static readonly Vector2 NoPosition = new Vector2(43f, -27f);
        private static readonly Vector2 AcceptPosition = new Vector2(0f, -27f);
        private static readonly Color32 QuestionColor = new Color32(0xF7, 0xCA, 0x92, 0xFF);
        private const int CanvasSortingOrder = 900;
        private const float CanvasScale = 2f;

        /// <summary>Returns null when the art is missing, so the caller can fall back.</summary>
        public static ConfirmationWindow Create(string name, Sprite panel, Sprite yesSprite, Sprite noSprite,
            TMP_FontAsset font)
        {
            if (panel == null || yesSprite == null || noSprite == null)
                return null;

            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasRenderer), typeof(Image));
            root.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = CanvasScale;

            Image shade = root.GetComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.55f);
            shade.raycastTarget = true;

            RectTransform panelRect = CreateChild(root.transform, "Panel Yes No", out Image panelImage);
            Center(panelRect, Vector2.zero, PanelSize);
            panelImage.sprite = panel;
            panelImage.raycastTarget = true;

            GameObject textObject = new GameObject("Question Text",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.layer = root.layer;
            textObject.transform.SetParent(panelRect, false);
            RectTransform textRect = (RectTransform)textObject.transform;
            Center(textRect, QuestionPosition, QuestionSize);
            TextMeshProUGUI question = textObject.GetComponent<TextMeshProUGUI>();
            question.font = font;
            question.fontSize = 18f;
            question.fontStyle = FontStyles.Bold;
            question.color = QuestionColor;
            question.alignment = TextAlignmentOptions.Center;
            question.textWrappingMode = TextWrappingModes.Normal;
            question.raycastTarget = false;

            Button yes = CreateButton(panelRect, "Button Yes", yesSprite, YesPosition);
            Button no = CreateButton(panelRect, "Button No", noSprite, NoPosition);
            Button accept = CreateButton(panelRect, "Button Accept", yesSprite, AcceptPosition);

            ConfirmationWindow window = root.AddComponent<ConfirmationWindow>();
            window.Initialize(question, yes, no, accept);
            root.SetActive(false);
            return window;
        }

        private static Button CreateButton(Transform parent, string name, Sprite sprite, Vector2 position)
        {
            RectTransform rect = CreateChild(parent, name, out Image image);
            Center(rect, position, ButtonSize);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.86f);
            colors.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            button.colors = colors;
            return button;
        }

        private static RectTransform CreateChild(Transform parent, string name, out Image image)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            image = child.GetComponent<Image>();
            return (RectTransform)child.transform;
        }

        private static void Center(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }
    }
}
