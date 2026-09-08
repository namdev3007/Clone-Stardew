using System;
using System.Collections.Generic;
using Entity_Components;
using Entity_Components.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace World.NPC
{
    [DisallowMultipleComponent]
    public sealed class DialogueUIController : MonoBehaviour
    {
        private const float CharactersPerSecond = 48f;

        private static DialogueUIController instance;
        public static DialogueUIController InstanceOrNull => instance;
        public static bool IsDialogueOpen => instance != null && instance.dialogueOpen;

        [Header("Visual setup")]
        [SerializeField] private DialogueVisualLibrary visuals;
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject dialogueRoot;
        [SerializeField] private GameObject choiceRoot;
        [SerializeField] private GameObject npcDialogueGroup;
        [SerializeField] private GameObject playerDialogueGroup;
        [SerializeField] private Image npcDialogueBoxImage;
        [SerializeField] private Image playerDialogueBoxImage;
        [SerializeField] private Image dialogueBox;
        [SerializeField] private Image playerPortrait;
        [SerializeField] private Image npcPortrait;
        [SerializeField] private Image npcNamePlate;
        [SerializeField] private Image playerNamePlate;
        [SerializeField] private Image namePlate;
        [SerializeField] private Image npcContinueIcon;
        [SerializeField] private Image playerContinueIcon;
        [SerializeField] private Image continueIcon;
        [SerializeField] private TextMeshProUGUI npcBodyText;
        [SerializeField] private TextMeshProUGUI playerBodyText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI npcNameText;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private TextMeshProUGUI choicePrompt;
        [SerializeField] private RectTransform choiceList;
        [SerializeField, HideInInspector] private int editorLayoutVersion;

        private DialogueSequence currentSequence;
        private int lineIndex;
        private string fullText;
        private float visibleCharacters;
        private bool lineComplete;
        private bool dialogueOpen;
        private System.Action onComplete;
        private Sprite activeNpcPortrait;
        private string activeNpcName;
        private Mover frozenMover;
        private GridSelector frozenSelector;

        public static DialogueUIController Ensure(DialogueVisualLibrary library)
        {
            if (instance == null)
            {
                // The authored Dialogue UI is intentionally inactive in Edit Mode.
                // Include inactive objects so interaction reuses it instead of
                // creating a second runtime canvas.
                instance = FindFirstObjectByType<DialogueUIController>(FindObjectsInactive.Include);
                if (instance == null)
                {
                    GameObject root = new GameObject("Dialogue UI");
                    instance = root.AddComponent<DialogueUIController>();
                    DontDestroyOnLoad(root);
                }
            }

            if (!instance.gameObject.activeSelf)
                instance.gameObject.SetActive(true);

            instance.Initialize(library);
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            if (dialogueRoot != null)
                dialogueRoot.SetActive(false);
            if (choiceRoot != null)
                choiceRoot.SetActive(false);

            BindRuntimeButtons();
        }

        private void Update()
        {
            if (!dialogueOpen || choiceRoot.activeSelf)
                return;

            if (!lineComplete)
            {
                visibleCharacters += CharactersPerSecond * UnityEngine.Time.unscaledDeltaTime;
                int count = Mathf.Clamp(Mathf.FloorToInt(visibleCharacters), 0, fullText.Length);
                bodyText.text = fullText.Substring(0, count);
                if (count >= fullText.Length)
                {
                    lineComplete = true;
                    continueIcon.gameObject.SetActive(true);
                }
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
                Advance();
        }

        public void Play(DialogueSequence sequence, string npcName, Sprite npcImage, System.Action completed = null)
        {
            if (sequence == null || sequence.Lines.Count == 0)
            {
                completed?.Invoke();
                return;
            }

            activeNpcName = npcName;
            activeNpcPortrait = npcImage;
            currentSequence = sequence;
            lineIndex = 0;
            onComplete = completed;
            dialogueOpen = true;
            dialogueRoot.SetActive(true);
            choiceRoot.SetActive(false);
            LockPlayer(true);
            ShowLine();
        }

        public void ShowChoices(string prompt, IReadOnlyList<string> choices, System.Action<int> selected)
        {
            dialogueOpen = true;
            dialogueRoot.SetActive(false);
            choiceRoot.SetActive(true);
            choicePrompt.text = prompt;
            ClearChoiceButtons();
            LockPlayer(true);

            for (int i = 0; i < choices.Count; i++)
            {
                int selectedIndex = i;
                Button button = CreateChoiceButton(choices[i]);
                button.onClick.AddListener(() =>
                {
                    choiceRoot.SetActive(false);
                    selected?.Invoke(selectedIndex);
                });
            }
        }

        public void ShowShopCatalog(NpcShopCatalog catalog, System.Action closed)
        {
            List<string> labels = new List<string>();
            List<NpcShopCatalog.Entry> visibleEntries = new List<NpcShopCatalog.Entry>();
            if (catalog != null)
            {
                foreach (NpcShopCatalog.Entry entry in catalog.Entries)
                {
                    if (entry != null && entry.visible && entry.item != null)
                    {
                        visibleEntries.Add(entry);
                        labels.Add(entry.item.ItemName);
                    }
                }
            }
            labels.Add("Close");

            ShowChoices("UNCLE HAI'S SHOP\nCatalog preview — purchases will be enabled when coins return.", labels, index =>
            {
                if (index >= visibleEntries.Count)
                {
                    CloseAll();
                    closed?.Invoke();
                    return;
                }

                NpcShopCatalog.Entry entry = visibleEntries[index];
                ShowChoices(entry.item.ItemName + "\n" + entry.item.Description,
                    new[] { "Back to catalog", "Close" }, detailChoice =>
                    {
                        if (detailChoice == 0)
                            ShowShopCatalog(catalog, closed);
                        else
                        {
                            CloseAll();
                            closed?.Invoke();
                        }
                    });
            });
        }

        public void RefreshTutorialObjective()
        {
            if (objectiveText == null)
                return;

            TutorialProgressService progress = TutorialProgressService.Instance;
            bool show = progress.MetGrandpa && !progress.CompletedGrandpaLesson;
            objectiveText.gameObject.SetActive(show);
            if (show)
            {
                int count = Mathf.Min(3, progress.HoedCellCount);
                objectiveText.text = progress.HoeObjectiveComplete
                    ? "The soil is ready. Go talk to Grandpa."
                    : "GRANDPA'S FIRST LESSON\nTill at least 3 soil tiles  " + count + " / 3";
            }
        }

        private void Initialize(DialogueVisualLibrary library)
        {
            if (visuals == null)
                visuals = library;
            if (canvas != null)
                return;

            BuildInterface();
            BindRuntimeButtons();
            if (Application.isPlaying)
                RefreshTutorialObjective();
        }

        /// <summary>
        /// Buttons added while authoring the dialogue canvas only keep persistent
        /// UnityEvents. Reconnect their code listeners whenever the scene starts so
        /// the authored UI remains clickable after reopening Unity.
        /// </summary>
        private void BindRuntimeButtons()
        {
            BindButton(npcDialogueBoxImage, Advance);
            BindButton(playerDialogueBoxImage, Advance);

            Transform skipTransform = dialogueRoot != null ? dialogueRoot.transform.Find("Skip") : null;
            Button skipButton = skipTransform != null ? skipTransform.GetComponent<Button>() : null;
            if (skipButton != null)
            {
                skipButton.onClick.RemoveListener(Skip);
                skipButton.onClick.AddListener(Skip);
            }
        }

        private static void BindButton(Image image, UnityEngine.Events.UnityAction action)
        {
            Button button = image != null ? image.GetComponent<Button>() : null;
            if (button == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        public void BuildForEditor(DialogueVisualLibrary library)
        {
            if (visuals == null)
                visuals = library;
            if (canvas != null && (npcDialogueGroup == null || playerDialogueGroup == null))
            {
                if (dialogueRoot != null)
                    DestroyImmediate(dialogueRoot);
                if (choiceRoot != null)
                    DestroyImmediate(choiceRoot);
                dialogueRoot = null;
                choiceRoot = null;
                choiceList = null;
            }
            BuildInterface();
            if (editorLayoutVersion < 2)
            {
                ApplyCompactSpeakerLayout();
                editorLayoutVersion = 2;
            }
            DisablePreserveAspect();
            if (objectiveText != null)
                objectiveText.gameObject.SetActive(false);
            if (dialogueRoot != null)
                dialogueRoot.SetActive(true);
            if (npcDialogueGroup != null)
                npcDialogueGroup.SetActive(true);
            if (playerDialogueGroup != null)
                playerDialogueGroup.SetActive(false);
            if (choiceRoot != null)
                choiceRoot.SetActive(false);

            // Keep the complete dialogue canvas hidden in the authored scene.
            // Ensure() activates it when an NPC interaction needs it at runtime.
            gameObject.SetActive(false);
        }

        private void ApplyCompactSpeakerLayout()
        {
            if (npcDialogueBoxImage != null)
                SetRect(npcDialogueBoxImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 80f), new Vector2(1280f, 720f), new Vector2(0.5f, 0f));
            if (playerDialogueBoxImage != null)
                SetRect(playerDialogueBoxImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 80f), new Vector2(1280f, 720f), new Vector2(0.5f, 0f));
            if (npcNamePlate != null)
                SetRect(npcNamePlate.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-250f, 360f), new Vector2(300f, 86f), new Vector2(0.5f, 0.5f));
            if (playerNamePlate != null)
                SetRect(playerNamePlate.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(250f, 360f), new Vector2(300f, 86f), new Vector2(0.5f, 0.5f));
            if (npcBodyText != null)
                SetRect(npcBodyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(130f, 230f), new Vector2(820f, 150f), new Vector2(0.5f, 0.5f));
            if (playerBodyText != null)
                SetRect(playerBodyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-130f, 230f), new Vector2(820f, 150f), new Vector2(0.5f, 0.5f));
            if (npcContinueIcon != null)
                SetRect(npcContinueIcon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(600f, 175f), new Vector2(54f, 44f), new Vector2(0.5f, 0.5f));
            if (playerContinueIcon != null)
                SetRect(playerContinueIcon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(340f, 175f), new Vector2(54f, 44f), new Vector2(0.5f, 0.5f));
        }

        private void DisablePreserveAspect()
        {
            if (playerPortrait != null) playerPortrait.preserveAspect = false;
            if (npcPortrait != null) npcPortrait.preserveAspect = false;
            if (playerDialogueBoxImage != null) playerDialogueBoxImage.preserveAspect = false;
            if (npcDialogueBoxImage != null) npcDialogueBoxImage.preserveAspect = false;
            if (playerNamePlate != null) playerNamePlate.preserveAspect = false;
            if (npcNamePlate != null) npcNamePlate.preserveAspect = false;
            if (playerContinueIcon != null) playerContinueIcon.preserveAspect = false;
            if (npcContinueIcon != null) npcContinueIcon.preserveAspect = false;
            if (dialogueRoot != null)
            {
                Transform skip = dialogueRoot.transform.Find("Skip");
                if (skip != null && skip.TryGetComponent(out Image skipImage))
                    skipImage.preserveAspect = false;
            }
        }

        private void BuildInterface()
        {
            if (canvas == null)
            {
                canvas = gameObject.GetComponent<Canvas>();
                if (canvas == null)
                    canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
                CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
                if (scaler == null)
                    scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                if (gameObject.GetComponent<GraphicRaycaster>() == null)
                    gameObject.AddComponent<GraphicRaycaster>();

                objectiveText = CreateText("Tutorial Objective", transform, 25f, TextAlignmentOptions.TopLeft);
                SetRect(objectiveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(28f, -28f), new Vector2(620f, 90f), new Vector2(0f, 1f));
                objectiveText.color = new Color(1f, 0.95f, 0.75f, 1f);
                objectiveText.outlineWidth = 0.18f;
            }

            if (dialogueRoot == null)
            {
                dialogueRoot = new GameObject("Dialogue Window", typeof(RectTransform));
                dialogueRoot.transform.SetParent(transform, false);
                Stretch(dialogueRoot.GetComponent<RectTransform>());

                Image dimmer = CreateImage("Dimmer", dialogueRoot.transform, null);
                Stretch(dimmer.rectTransform);
                dimmer.color = new Color(0f, 0f, 0f, 0.26f);

                BuildNpcDialogueGroup();
                BuildPlayerDialogueGroup();

                Image skipImage = CreateImage("Skip", dialogueRoot.transform, visuals?.skipButton);
                skipImage.preserveAspect = false;
                SetRect(skipImage.rectTransform, Vector2.one, Vector2.one,
                    new Vector2(-75f, -45f), new Vector2(170f, 76f), Vector2.one);
                Button skip = skipImage.gameObject.AddComponent<Button>();
                skip.transition = Selectable.Transition.ColorTint;
                skip.onClick.AddListener(Skip);

                npcDialogueGroup.SetActive(true);
                playerDialogueGroup.SetActive(false);
            }

            if (choiceRoot == null)
                BuildChoiceInterface();

            dialogueRoot.SetActive(false);
            choiceRoot.SetActive(false);
        }

        private void BuildNpcDialogueGroup()
        {
            npcDialogueGroup = new GameObject("NPC Dialogue Group", typeof(RectTransform));
            npcDialogueGroup.transform.SetParent(dialogueRoot.transform, false);
            Stretch(npcDialogueGroup.GetComponent<RectTransform>());

            npcPortrait = CreateImage("NPC Portrait", npcDialogueGroup.transform, null);
            SetRect(npcPortrait.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(285f, -7f), new Vector2(456f, 576f), new Vector2(0.5f, 0f));

            npcDialogueBoxImage = CreateImage("NPC Dialogue Box", npcDialogueGroup.transform, visuals?.npcDialogueBox);
            SetRect(npcDialogueBoxImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 80f), new Vector2(1280f, 720f), new Vector2(0.5f, 0f));
            Button advanceButton = npcDialogueBoxImage.gameObject.AddComponent<Button>();
            advanceButton.transition = Selectable.Transition.None;
            advanceButton.onClick.AddListener(Advance);

            npcNamePlate = CreateImage("NPC Name Plate", npcDialogueGroup.transform, visuals?.namePlate);
            SetRect(npcNamePlate.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-250f, 360f), new Vector2(300f, 86f), new Vector2(0.5f, 0.5f));
            npcNameText = CreateText("NPC Name", npcNamePlate.transform, 28f, TextAlignmentOptions.Center);
            Stretch(npcNameText.rectTransform);
            npcNameText.color = new Color(0.22f, 0.12f, 0.06f, 1f);

            npcBodyText = CreateText("NPC Dialogue Text", npcDialogueGroup.transform, 31f, TextAlignmentOptions.TopLeft);
            SetRect(npcBodyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(130f, 230f), new Vector2(820f, 150f), new Vector2(0.5f, 0.5f));
            ConfigureBodyText(npcBodyText);

            npcContinueIcon = CreateImage("NPC Continue Icon", npcDialogueGroup.transform, visuals?.continueIcon);
            SetRect(npcContinueIcon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(600f, 175f), new Vector2(54f, 44f), new Vector2(0.5f, 0.5f));
        }

        private void BuildPlayerDialogueGroup()
        {
            playerDialogueGroup = new GameObject("Player Dialogue Group", typeof(RectTransform));
            playerDialogueGroup.transform.SetParent(dialogueRoot.transform, false);
            Stretch(playerDialogueGroup.GetComponent<RectTransform>());

            playerPortrait = CreateImage("Player Portrait", playerDialogueGroup.transform, visuals?.playerPortrait);
            SetRect(playerPortrait.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-285f, -3f), new Vector2(456f, 576f), new Vector2(0.5f, 0f));

            playerDialogueBoxImage = CreateImage("Player Dialogue Box", playerDialogueGroup.transform, visuals?.playerDialogueBox);
            SetRect(playerDialogueBoxImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 80f), new Vector2(1280f, 720f), new Vector2(0.5f, 0f));
            Button advanceButton = playerDialogueBoxImage.gameObject.AddComponent<Button>();
            advanceButton.transition = Selectable.Transition.None;
            advanceButton.onClick.AddListener(Advance);

            playerNamePlate = CreateImage("Player Name Plate", playerDialogueGroup.transform, visuals?.namePlate);
            SetRect(playerNamePlate.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(250f, 360f), new Vector2(300f, 86f), new Vector2(0.5f, 0.5f));
            playerNameText = CreateText("Player Name", playerNamePlate.transform, 28f, TextAlignmentOptions.Center);
            Stretch(playerNameText.rectTransform);
            playerNameText.color = new Color(0.22f, 0.12f, 0.06f, 1f);

            playerBodyText = CreateText("Player Dialogue Text", playerDialogueGroup.transform, 31f, TextAlignmentOptions.TopLeft);
            SetRect(playerBodyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-130f, 230f), new Vector2(820f, 150f), new Vector2(0.5f, 0.5f));
            ConfigureBodyText(playerBodyText);

            playerContinueIcon = CreateImage("Player Continue Icon", playerDialogueGroup.transform, visuals?.continueIcon);
            SetRect(playerContinueIcon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(340f, 175f), new Vector2(54f, 44f), new Vector2(0.5f, 0.5f));
        }

        private static void ConfigureBodyText(TextMeshProUGUI text)
        {
            text.color = new Color(0.19f, 0.1f, 0.055f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        private void BuildChoiceInterface()
        {
            choiceRoot = new GameObject("Dialogue Choices", typeof(RectTransform));
            choiceRoot.transform.SetParent(transform, false);
            Stretch(choiceRoot.GetComponent<RectTransform>());

            Image panel = CreateImage("Panel", choiceRoot.transform, visuals?.npcDialogueBox);
            panel.type = Image.Type.Sliced;
            SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 620f), new Vector2(0.5f, 0.5f));

            choicePrompt = CreateText("Prompt", panel.transform, 30f, TextAlignmentOptions.TopLeft);
            SetRect(choicePrompt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -85f), new Vector2(620f, 130f), new Vector2(0.5f, 0.5f));
            choicePrompt.color = new Color(0.2f, 0.1f, 0.05f, 1f);

            GameObject list = new GameObject("Choice List", typeof(RectTransform), typeof(VerticalLayoutGroup));
            list.transform.SetParent(panel.transform, false);
            choiceList = list.GetComponent<RectTransform>();
            SetRect(choiceList, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -65f), new Vector2(590f, 330f), new Vector2(0.5f, 0.5f));
            VerticalLayoutGroup layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
        }

        private Button CreateChoiceButton(string label)
        {
            Image image = CreateImage("Choice " + label, choiceList, visuals?.namePlate);
            image.type = Image.Type.Sliced;
            LayoutElement element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 62f;
            element.minHeight = 62f;
            Button button = image.gameObject.AddComponent<Button>();
            TextMeshProUGUI text = CreateText("Label", image.transform, 25f, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.color = new Color(0.2f, 0.1f, 0.05f, 1f);
            text.text = label;
            return button;
        }

        private void ShowLine()
        {
            DialogueSequence.Line line = currentSequence.Lines[lineIndex];
            bool playerSpeaking = line.speaker == DialogueSequence.Speaker.Player;
            npcDialogueGroup.SetActive(!playerSpeaking);
            playerDialogueGroup.SetActive(playerSpeaking);

            bodyText = playerSpeaking ? playerBodyText : npcBodyText;
            nameText = playerSpeaking ? playerNameText : npcNameText;
            continueIcon = playerSpeaking ? playerContinueIcon : npcContinueIcon;
            dialogueBox = playerSpeaking ? playerDialogueBoxImage : npcDialogueBoxImage;
            namePlate = playerSpeaking ? playerNamePlate : npcNamePlate;

            fullText = line.GetText(false);
            visibleCharacters = 0f;
            lineComplete = string.IsNullOrEmpty(fullText);
            bodyText.text = string.Empty;
            nameText.text = playerSpeaking ? "Player" : activeNpcName;
            npcPortrait.sprite = activeNpcPortrait;
            playerPortrait.sprite = visuals.playerPortrait;
            playerPortrait.color = Color.white;
            npcPortrait.color = Color.white;
            continueIcon.gameObject.SetActive(lineComplete);
        }

        private void Advance()
        {
            if (!dialogueOpen || choiceRoot.activeSelf)
                return;
            if (!lineComplete)
            {
                bodyText.text = fullText;
                visibleCharacters = fullText.Length;
                lineComplete = true;
                continueIcon.gameObject.SetActive(true);
                return;
            }

            lineIndex++;
            if (lineIndex < currentSequence.Lines.Count)
                ShowLine();
            else
                FinishSequence();
        }

        private void Skip()
        {
            if (dialogueOpen)
                FinishSequence();
        }

        private void FinishSequence()
        {
            dialogueRoot.SetActive(false);
            dialogueOpen = false;
            currentSequence = null;
            LockPlayer(false);
            System.Action callback = onComplete;
            onComplete = null;
            callback?.Invoke();
        }

        public void CloseAll()
        {
            dialogueRoot.SetActive(false);
            choiceRoot.SetActive(false);
            dialogueOpen = false;
            currentSequence = null;
            onComplete = null;
            LockPlayer(false);
        }

        private void LockPlayer(bool locked)
        {
            if (locked)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                frozenMover = player != null ? player.GetComponent<Mover>() : null;
                frozenSelector = player != null ? player.GetComponent<GridSelector>() : null;
            }
            frozenMover?.FreezeMovement(locked);
            frozenSelector?.SetFrozen(locked);
        }

        private void ClearChoiceButtons()
        {
            for (int i = choiceList.childCount - 1; i >= 0; i--)
                Destroy(choiceList.GetChild(i).gameObject);
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            return image;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            if (visuals != null && visuals.textFont != null)
                text.font = visuals.textFont;
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
