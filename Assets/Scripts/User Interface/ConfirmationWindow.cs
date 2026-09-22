using System;
using Referencing.Scriptable_Reference;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace User_Interface
{
    [DefaultExecutionOrder(-1300)]
    public class ConfirmationWindow : MonoBehaviour
    {
        private static readonly Color32 QuestionColor = new Color32(0xF7, 0xCA, 0x92, 0xFF);
        private static ConfirmationWindow activeWindow;
        public static bool AnyOpen => activeWindow != null && activeWindow.gameObject.activeInHierarchy;

        [SerializeField]
        private TextMeshProUGUI textQuestion;

        [SerializeField]
        private TextMeshProUGUI textAnswerYes;

        [SerializeField]
        private TextMeshProUGUI textAnswerNo;

        [SerializeField]
        private TextMeshProUGUI textAccept;

        [SerializeField]
        private Button buttonYes;

        [SerializeField]
        private Button buttonNo;

        [SerializeField]
        private Button buttonAccept;

        [SerializeField]
        private ScriptableReference sceneReference;

        [SerializeField]
        private bool hideSceneInstanceOnAwake;

        private System.Action actionYes;
        private System.Action actionNo;

        public struct Configuration
        {
            public bool acceptOnly;
            public string question;
            public string answerYes;
            public string answerNo;
            public System.Action actionYes;
            public System.Action actionNo;
        }

        private void Awake()
        {
            if (hideSceneInstanceOnAwake && sceneReference != null)
                sceneReference.Reference = gameObject;

            if (textQuestion != null)
                textQuestion.color = QuestionColor;
            WireButtons();

            // Remains visible while authoring the scene, but starts hidden in
            // Play Mode. Configure() enables this same scene object when needed.
            if (hideSceneInstanceOnAwake)
                gameObject.SetActive(false);
        }

        /// <summary>
        /// Wires a window that was built at runtime instead of authored in a
        /// prefab, so gameplay dialogs can reuse this window's behaviour.
        /// </summary>
        public void Initialize(TextMeshProUGUI question, Button yes, Button no, Button accept)
        {
            textQuestion = question;
            if (textQuestion != null)
                textQuestion.color = QuestionColor;
            buttonYes = yes;
            buttonNo = no;
            buttonAccept = accept;
            WireButtons();
        }

        private void WireButtons()
        {
            buttonYes?.onClick.RemoveListener(OnClickYesButton);
            buttonAccept?.onClick.RemoveListener(OnClickYesButton);
            buttonNo?.onClick.RemoveListener(OnClickNoButton);
            buttonYes?.onClick.AddListener(OnClickYesButton);
            buttonAccept?.onClick.AddListener(OnClickYesButton);
            buttonNo?.onClick.AddListener(OnClickNoButton);
        }

        private void OnEnable() => activeWindow = this;

        private void OnDisable()
        {
            if (activeWindow == this)
                activeWindow = null;
        }

        public static bool TryCloseOpen()
        {
            if (!AnyOpen)
                return false;
            // SetActive(false) runs OnDisable, which clears activeWindow.
            ConfirmationWindow window = activeWindow;
            window.gameObject.SetActive(false);
            window.ClearEvents();
            return true;
        }

        private void OnClickNoButton()
        {
            if (actionNo != null)
            {
                actionNo.Invoke();
            }

            this.gameObject.SetActive(false);
            ClearEvents();
        }

        private void OnClickYesButton()
        {
            if (actionYes != null)
            {
                actionYes.Invoke();
            }

            this.gameObject.SetActive(false);
            ClearEvents();
        }

        public void Configure(Configuration configuration)
        {
            textQuestion?.SetText(configuration.question);
            textAnswerYes?.SetText(configuration.answerYes);
            textAnswerNo?.SetText(configuration.answerNo);
            textAccept?.SetText(configuration.answerYes);

            buttonYes?.gameObject.SetActive(true);
            buttonNo?.gameObject.SetActive(!configuration.acceptOnly);
            buttonAccept?.gameObject.SetActive(false);

            // A one-button message reuses Button Yes instead of maintaining a
            // third duplicate button in the hierarchy.
            if (buttonYes != null && buttonYes.transform is RectTransform yesRect)
                yesRect.anchoredPosition = configuration.acceptOnly
                    ? new Vector2(0f, -27f)
                    : new Vector2(-43f, -27f);

            if (configuration.actionYes != null)
            {
                this.actionYes += configuration.actionYes;
            }

            if (configuration.actionNo != null)
            {
                this.actionNo += configuration.actionNo;
            }

            this.gameObject.SetActive(true);
        }

        private void ClearEvents()
        {
            if (actionYes != null)
            {
                foreach (Delegate d in actionYes.GetInvocationList())
                {
                    actionYes -= (System.Action)d;
                }
            }

            if (actionNo != null)
            {
                foreach (Delegate d in actionNo.GetInvocationList())
                {
                    actionNo -= (System.Action)d;
                }
            }
        }
    }
}
