using System.Collections.Generic;
using Event.Events;
using Interactions;
using Item;
using Item.Inventory;
using User_Interface;
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
        private DialogueSequence homeOrchardDialogue;

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

        private void LateUpdate()
        {
            if (role != NpcRole.Grandpa || DialogueUIController.IsDialogueOpen)
                return;
            if (TutorialProgressService.Instance.CompletedGrandpaLesson && FarmExpansionRuntime.QuestAvailable)
                status?.ShowExclamation();
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

            if (FarmExpansionRuntime.QuestAvailable)
            {
                Play(GetHomeOrchardDialogue(), () =>
                    FarmExpansionRuntime.BeginReveal(this, () => status?.ShowEllipsis()));
                return;
            }

            ShowGrandpaMenu();
        }

        private DialogueSequence GetHomeOrchardDialogue()
        {
            if (homeOrchardDialogue != null)
                return homeOrchardDialogue;
            homeOrchardDialogue = ScriptableObject.CreateInstance<DialogueSequence>();
            homeOrchardDialogue.Configure("grandpa_home_orchard_unlock", new[]
            {
                new DialogueSequence.Line
                {
                    speaker = DialogueSequence.Speaker.Npc,
                    english = "You're actually pretty handy, huh? Alright, listen! We've got a small plot left in the backyard. Go clear out the weeds, then grab a few mango and banana saplings to plant so we'll have some fresh fruit to eat.",
                    vietnamese = "Thằng cu này được việc phết nhỉ? Thôi! Phần đất nhà mình vẫn còn một ít sau vườn, cháu ra đó dọn đống cỏ dại rồi đi mua ít xoài với chuối giống về mà trồng cho có ít quả mà ăn."
                }
            });
            return homeOrchardDialogue;
        }

        private void ShowGrandpaMenu()
        {
            DialogueUIController ui = DialogueUIController.Ensure(visuals);
            bool canReplayOrchard = FarmExpansionRuntime.HomeOrchardUnlocked;
            bool vietnamese = SettingsSoundUI.UseVietnamese;
            ui.ShowChoices(vietnamese ? "Cháu cần ông giúp gì?" : "What can I help you with?", new[]
            {
                vietnamese ? "Nhắc lại việc cháu cần làm" : "Remind me what to do",
                vietnamese ? "Kể cháu nghe về việc làm vườn" : "Tell me about farming",
                canReplayOrchard
                    ? (vietnamese ? "Chỉ lại khu đất sau nhà" : "Show me the backyard plot again")
                    : (vietnamese ? "Mở rộng khu vườn" : "Expand the farm"),
                vietnamese ? "Thôi, không có gì ạ" : "Never mind"
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
                        if (canReplayOrchard)
                            Play(GetHomeOrchardDialogue(), () =>
                                FarmExpansionRuntime.BeginReveal(this, () => status?.ShowEllipsis()));
                        else
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
                    OpenSellerShop();
                });
                return;
            }

            PlayRandomRevisitThenOpenShop();
        }

        private void PlayRandomRevisitThenOpenShop()
        {
            if (randomRevisitLines == null || randomRevisitLines.Length == 0)
            {
                OpenSellerShop();
                return;
            }
            int index = Random.Range(0, randomRevisitLines.Length);
            if (randomRevisitLines.Length > 1 && index == lastRandomRevisit)
                index = (index + 1) % randomRevisitLines.Length;
            lastRandomRevisit = index;
            DialogueSequence selected = randomRevisitLines[index];
            Play(selected, OpenSellerShop);
        }

        private void OpenSellerShop()
        {
            DialogueUIController.Ensure(visuals)
                .ShowShopCatalog(shopCatalog, () => status?.ShowEllipsis());
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
