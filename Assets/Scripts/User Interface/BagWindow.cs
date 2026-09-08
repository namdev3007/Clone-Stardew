using Event.Events;
using UnityEngine;
using World.NPC;

namespace User_Interface
{
    /// <summary>
    /// The bag used to be one of the pauze menu tabs, which meant it could only be
    /// reached through the pauze screen. It is its own window now: one key opens it,
    /// the same key closes it, and it is drawn larger than the rest of the menu.
    /// </summary>
    [AddComponentMenu("Farming Kit/User Interface/Bag Window")]
    public class BagWindow : MonoBehaviour
    {
        [SerializeField, Tooltip("The window that gets shown and hidden. Usually Window_Inventory.")]
        private GameObject window;

        [SerializeField, Tooltip("Key that opens and closes the bag.")]
        private KeyCode toggleKey = KeyCode.Tab;

        [SerializeField, Min(0.1f), Tooltip("Scale of the window while the bag is open.")]
        private float windowScale = 1.95f;

        [SerializeField, Tooltip("Gameplay stays frozen while the bag is open.")]
        private BoolEvent pauzeEvent;

        [SerializeField, Tooltip("The bag closes whenever the pauze menu takes over.")]
        private BoolEvent displayPauzeMenu;

        private bool isOpen;
        private bool pauzeMenuVisible;
        private bool invokingPauze;

        public bool IsOpen { get { return isOpen; } }

        private void OnEnable()
        {
            pauzeEvent?.AddListener(OnPauzeStateChanged);
            displayPauzeMenu?.AddListener(OnPauzeMenuDisplayed);
        }

        private void OnDisable()
        {
            pauzeEvent?.RemoveListener(OnPauzeStateChanged);
            displayPauzeMenu?.RemoveListener(OnPauzeMenuDisplayed);
        }

        // Hiding happens in Start instead of Awake so the inventory slots inside the
        // window still get their own Awake during scene load and register themselves.
        private void Start()
        {
            ApplyScale();
            SetOpen(false, false);
        }

        private void Update()
        {
            // Dialogue owns player input and the foreground UI while it is open.
            // Also close a bag that happened to be visible before dialogue began.
            if (DialogueUIController.IsDialogueOpen)
            {
                if (isOpen)
                    SetOpen(false, true);
                return;
            }

            if (!Input.GetKeyDown(toggleKey))
                return;

            // The pauze menu owns the screen while it is up.
            if (pauzeMenuVisible)
                return;

            SetOpen(!isOpen, true);
        }

        public void Toggle()
        {
            if (DialogueUIController.IsDialogueOpen)
                return;

            SetOpen(!isOpen, true);
        }

        public void Close()
        {
            if (isOpen)
                SetOpen(false, true);
        }

        private void SetOpen(bool open, bool affectPauze)
        {
            isOpen = open;

            if (window != null)
                window.SetActive(open);

            if (!affectPauze || pauzeEvent == null)
                return;

            invokingPauze = true;
            pauzeEvent.Invoke(open);
            invokingPauze = false;
        }

        private void OnPauzeStateChanged(bool pauzed)
        {
            // Anything that unpauzes the game (closing the pauze menu, a warp, ...)
            // takes the bag down with it, so it can never hang over running gameplay.
            if (!invokingPauze && !pauzed && isOpen)
                SetOpen(false, false);
        }

        private void OnPauzeMenuDisplayed(bool visible)
        {
            pauzeMenuVisible = visible;

            if (visible && isOpen)
                SetOpen(false, false);
        }

        private void ApplyScale()
        {
            if (window != null)
                window.transform.localScale = new Vector3(windowScale, windowScale, 1f);
        }
    }
}
