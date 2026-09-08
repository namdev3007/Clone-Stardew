using System.Collections.Generic;
using Event.Events;
using Interactions;
using Item;
using Item.Inventory;
using UnityEngine;

namespace World.NPC
{
    [DisallowMultipleComponent]
    public sealed class NpcDialogueInteractor : MonoBehaviour
    {
        public enum NpcRole
        {
            Grandpa,
            SeedSeller
        }

        [Header("Identity")]
        [SerializeField] private NpcRole role;
        [SerializeField] private string englishName;
        [SerializeField] private Sprite portrait;
        [SerializeField] private DialogueVisualLibrary visuals;

        [Header("Dialogue")]
        [SerializeField] private DialogueSequence introduction;
        [SerializeField] private DialogueSequence reminder;
        [SerializeField] private DialogueSequence completion;
        [SerializeField] private DialogueSequence farmingAdvice;
        [SerializeField] private DialogueSequence expansionUnavailable;
        [SerializeField] private DialogueSequence[] randomRevisitLines;

        [Header("Seller")]
        [SerializeField] private NpcShopCatalog shopCatalog;

        [Header("Grandpa starter gift")]
        [SerializeField] private ItemData starterHoe;
        [SerializeField] private ItemData starterSeeds;
        [SerializeField, Min(1)] private int starterSeedAmount = 5;

        [Header("Interaction")]
        [SerializeField] private InteractionEvent hoverInteractionEvent;
        [SerializeField, Min(0.1f)] private float interactionDistance = 0.8f;

        private NpcDialogueStatus status;
        private InteractionField interactionField;
        private int lastRandomRevisit = -1;

        public void Configure(NpcRole npcRole, string displayName, Sprite characterPortrait,
            DialogueVisualLibrary visualLibrary, DialogueSequence intro, DialogueSequence remind,
            DialogueSequence completed, DialogueSequence advice, DialogueSequence expandUnavailable,
            DialogueSequence[] revisitLines, NpcShopCatalog catalog, ItemData hoe, ItemData seeds,
            InteractionEvent hoverEvent)
        {
            role = npcRole;
            englishName = displayName;
            portrait = characterPortrait;
            visuals = visualLibrary;
            introduction = intro;
            reminder = remind;
            completion = completed;
            farmingAdvice = advice;
            expansionUnavailable = expandUnavailable;
            randomRevisitLines = revisitLines;
            shopCatalog = catalog;
            starterHoe = hoe;
            starterSeeds = seeds;
            hoverInteractionEvent = hoverEvent;
        }

        private void Awake()
        {
            status = GetComponent<NpcDialogueStatus>();
            EnsureInteractionField();
        }

        private void Start()
        {
            status?.ShowEllipsis();
            DialogueUIController.Ensure(visuals).RefreshTutorialObjective();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                status?.ShowEllipsis();
        }

        public void StartInteraction()
        {
            if (DialogueUIController.IsDialogueOpen)
                return;

            status?.Hide();
            if (role == NpcRole.Grandpa)
                InteractWithGrandpa();
            else
                InteractWithSeller();
        }

        private void InteractWithGrandpa()
        {
            TutorialProgressService progress = TutorialProgressService.Instance;
            if (!progress.MetGrandpa)
            {
                Play(introduction, () =>
                {
                    AwardStarterItems();
                    progress.MarkMetGrandpa();
                    status?.ShowEllipsis();
                });
                return;
            }

            if (progress.HoeObjectiveComplete && !progress.CompletedGrandpaLesson)
            {
                Play(completion, () =>
                {
                    progress.MarkGrandpaLessonComplete();
                    status?.ShowEllipsis();
                });
                return;
            }

            if (!progress.CompletedGrandpaLesson)
            {
                Play(reminder, () => status?.ShowEllipsis());
                return;
            }

            ShowGrandpaMenu();
        }

        private void ShowGrandpaMenu()
        {
            DialogueUIController ui = DialogueUIController.Ensure(visuals);
            ui.ShowChoices("What can I help you with?", new[]
            {
                "Remind me what to do",
                "Tell me about farming",
                "Expand the farm",
                "Never mind"
            }, selected =>
            {
                switch (selected)
                {
                    case 0:
                        Play(reminder, () => status?.ShowEllipsis());
                        break;
                    case 1:
                        Play(farmingAdvice, () => status?.ShowEllipsis());
                        break;
                    case 2:
                        Play(expansionUnavailable, () => status?.ShowEllipsis());
                        break;
                    default:
                        ui.CloseAll();
                        status?.ShowEllipsis();
                        break;
                }
            });
        }

        private void InteractWithSeller()
        {
            TutorialProgressService progress = TutorialProgressService.Instance;
            if (!progress.MetSeller)
            {
                Play(introduction, () =>
                {
                    progress.MarkMetSeller();
                    status?.ShowEllipsis();
                });
                return;
            }

            DialogueUIController ui = DialogueUIController.Ensure(visuals);
            ui.ShowChoices("What can I get for you, kid?", new[]
            {
                "Browse the shop",
                "Talk",
                "Never mind"
            }, selected =>
            {
                if (selected == 0)
                    ui.ShowShopCatalog(shopCatalog, () => status?.ShowEllipsis());
                else if (selected == 1)
                    PlayRandomRevisit();
                else
                {
                    ui.CloseAll();
                    status?.ShowEllipsis();
                }
            });
        }

        private void PlayRandomRevisit()
        {
            if (randomRevisitLines == null || randomRevisitLines.Length == 0)
            {
                DialogueUIController.Ensure(visuals).CloseAll();
                status?.ShowEllipsis();
                return;
            }
            int index = Random.Range(0, randomRevisitLines.Length);
            if (randomRevisitLines.Length > 1 && index == lastRandomRevisit)
                index = (index + 1) % randomRevisitLines.Length;
            lastRandomRevisit = index;
            DialogueSequence selected = randomRevisitLines[index];
            Play(selected, () => status?.ShowEllipsis());
        }

        private void Play(DialogueSequence sequence, System.Action completed)
        {
            DialogueUIController.Ensure(visuals).Play(sequence, englishName, portrait, completed);
        }

        private void AwardStarterItems()
        {
            TutorialProgressService progress = TutorialProgressService.Instance;
            if (progress.ReceivedStarterTools)
                return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
            if (inventory != null)
            {
                if (starterHoe != null && inventory.GetItem(starterHoe, out _) == null)
                    inventory.AddItem(starterHoe, 1);
                if (starterSeeds != null && inventory.GetItem(starterSeeds, out _) == null)
                    inventory.AddItem(starterSeeds, starterSeedAmount);
            }
            progress.MarkStarterToolsReceived();
        }

        private void EnsureInteractionField()
        {
            interactionField = GetComponentInChildren<InteractionField>(true);
            if (interactionField == null)
            {
                GameObject fieldObject = new GameObject("Interaction Field");
                fieldObject.transform.SetParent(transform, false);
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer >= 0)
                    fieldObject.layer = interactableLayer;

                BoxCollider2D trigger = fieldObject.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                SpriteRenderer renderer = GetComponent<SpriteRenderer>();
                trigger.size = renderer != null
                    ? new Vector2(Mathf.Max(0.3f, renderer.bounds.size.x * 1.8f), Mathf.Max(0.35f, renderer.bounds.size.y))
                    : new Vector2(0.4f, 0.45f);
                interactionField = fieldObject.AddComponent<InteractionField>();
            }
            interactionField.Configure(interactionDistance, hoverInteractionEvent, StartInteraction);
        }
    }
}
