using Entity_Components;
using Entity_Components.Character;
using Entity_Components.Player;
using Item.Inventory;
using Referencing.Scriptable_Reference;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using World;
using World.Objects;

namespace Item.Actions
{
    [CreateAssetMenu(fileName = "Item Action Water Can", menuName = "Items/Item Actions/Water Can")]
    public class ItemAction_WaterCan : ItemAction
    {
        [SerializeField]
        private float speed;

        // Manipulate the tile on this tilemap
        [SerializeField]
        private string targetTilemapName;

        // Only do the action the current tile coordinate has the tile map name
        [SerializeField]
        private string requiredTileMapName;

        // In case the current target is water, which we want to refill
        [SerializeField]
        private string waterTileMapName;

        [SerializeField]
        private ScriptableReference gridManagerReference;

        [SerializeField]
        private float energyCost;

        [Header("Water gauge above player")]
        [SerializeField]
        private Sprite waterGaugeBackground;

        [SerializeField]
        private Sprite waterGaugeFill;

        [SerializeField]
        private Vector2 waterGaugeOffset = new Vector2(0f, 0.385f);

        [SerializeField]
        private UnityEvent OnWateredGround;

        [SerializeField]
        private UnityEvent OnObtainedWater;

        // Cache repeated use cases

        [System.NonSerialized]
        private GridManager gridManager;

        public override IEnumerator ItemUseAction(Inventory.Inventory userInventory, int itemIndex)
        {
            InventoryItem getInventoryItem = userInventory.GetItem(itemIndex);

            GridSelector gridSelector = userInventory.GetComponent<GridSelector>();

            if (gridManager == null)
            {
                gridManager = gridManagerReference.Reference?.GetComponent<GridManager>();
            }

            if (gridSelector != null && gridManager != null)
            {
                Vector3Int location = gridSelector.GetGridSelectionPosition();

                bool obtainingWater = gridManager.CanRefillWaterAt(location);
                Crop targetCrop = gridManager.GetCrop(location);
                bool cropNeedsWater = targetCrop != null && targetCrop.NeedsWater;
                bool dryFarmGround = !gridManager.HasWateredDirt(location) && gridManager.HasDirtHole(location);
                bool canWaterGround = gridManager.HasDirtHole(location) && (cropNeedsWater || dryFarmGround);

                // Validate before starting the animation. Held mouse input can ask
                // to use the can again just after a tile became wet; previously the
                // second request still played the full animation before doing nothing.
                if (!obtainingWater && !canWaterGround)
                    yield break;

                if (!obtainingWater && getInventoryItem != null && !FarmingCheats.InfiniteWater
                    && getInventoryItem.Energy.current < energyCost)
                    yield break;

                // Refilling is immediate: no watering pose, movement lock or wait.
                if (obtainingWater)
                {
                    if (getInventoryItem == null)
                        yield break;

                    ItemEnergy currentItemEnergy = getInventoryItem.Energy;
                    getInventoryItem.Energy = new ItemEnergy
                    {
                        min = currentItemEnergy.min,
                        max = currentItemEnergy.max,
                        current = currentItemEnergy.max
                    };
                    OnObtainedWater.Invoke();
                    UpdateWorldGauge(userInventory, getInventoryItem.Energy, true);
                    userInventory.ReloadItemSlot(itemIndex);
                    yield break;
                }

                Aimer aimer = userInventory.GetComponent<Aimer>();
                aimer?.SetAimDirection(gridSelector.GetMouseLookDirection());

                Mover getMover = userInventory.GetComponent<Mover>();
                getMover?.FreezeMovement(true);

                FullBodyPlayerSpriteAnimator[] getEntityAnimator = userInventory.GetComponentsInChildren<FullBodyPlayerSpriteAnimator>();

                float animationTime = 0;

                for (int i = 0; i < getEntityAnimator.Length; i++)
                {
                    getEntityAnimator[i].PlayAction(FullBodyPlayerSpriteAnimator.ActionType.Water, speed);
                    animationTime = 1 / speed;
                }

                gridSelector.SetFrozen(true);

                // The release sits in a finally block: an interrupted watering
                // must never leave the player frozen.
                try
                {

                    yield return new WaitForSeconds(animationTime * 0.5f);

                    if (!gridManager.CanRefillWaterAt(location))
                    {
                        Crop crop = gridManager.GetCrop(location);
                        cropNeedsWater = crop != null && crop.NeedsWater;
                        dryFarmGround = !gridManager.HasWateredDirt(location) && gridManager.HasDirtHole(location);

                        if (gridManager.HasDirtHole(location) && (cropNeedsWater || dryFarmGround))
                        {
                            ItemEnergy currentItemEnergy = getInventoryItem.Energy;

                            if (FarmingCheats.InfiniteWater || currentItemEnergy.current >= energyCost)
                            {
                                if (!gridManager.HasWateredDirt(location))
                                    gridManager.SetWateredDirtTile(location);

                                crop?.TryWater();

                                float newEnergy = FarmingCheats.InfiniteWater
                                    ? currentItemEnergy.max
                                    : currentItemEnergy.current - energyCost;
                                if (!FarmingCheats.InfiniteWater && newEnergy < energyCost)
                                {
                                    newEnergy = 0;
                                }

                                getInventoryItem.Energy = new ItemEnergy()
                                {
                                    min = currentItemEnergy.min,
                                    max = currentItemEnergy.max,
                                    current = newEnergy
                                };

                                OnWateredGround.Invoke();

                                UpdateWorldGauge(userInventory, getInventoryItem.Energy, true);
                                userInventory.ReloadItemSlot(itemIndex);
                            }
                        }
                    }
                    yield return new WaitForSeconds(animationTime * 0.5f);
                }
                finally
                {
                    getMover?.FreezeMovement(false);
                    if (gridSelector != null)
                        gridSelector.SetFrozen(false);
                }
            }
        }

        public override bool ItemUseCondition(Inventory.Inventory userInventory, int itemIndex)
        {
            return true;
        }

        public override void ItemActiveAction(Inventory.Inventory userInventory, int itemIndex)
        {
            InventoryItem item = userInventory.GetItem(itemIndex);
            if (item != null)
                UpdateWorldGauge(userInventory, item.Energy, true);
        }

        public override void ItemUnactiveAction(Inventory.Inventory userInventory, int itemIndex)
        {
            WaterCanWorldGauge gauge = userInventory.GetComponent<WaterCanWorldGauge>();
            if (gauge != null)
                gauge.SetVisible(false);
        }

        private void UpdateWorldGauge(Inventory.Inventory inventory, ItemEnergy energy, bool visible)
        {
            WaterCanWorldGauge gauge = inventory.GetComponent<WaterCanWorldGauge>();
            if (gauge == null)
                gauge = inventory.gameObject.AddComponent<WaterCanWorldGauge>();

            gauge.Configure(waterGaugeBackground, waterGaugeFill, waterGaugeOffset);
            gauge.SetValue(energy.min, energy.max, energy.current);
            gauge.SetVisible(visible);
        }
    }

    [DisallowMultipleComponent]
    public sealed class WaterCanWorldGauge : MonoBehaviour
    {
        private const float BobAmplitude = 0.015f;
        private const float BobCyclesPerSecond = 1.2f;
        private Vector2 baseOffset = new Vector2(0f, 0.385f);
        private RectTransform gaugeRoot;
        private Image trackImage;
        private Image fillImage;
        private Slider slider;

        public void Configure(Sprite trackSprite, Sprite fillSprite, Vector2 offset)
        {
            EnsureInterface();
            baseOffset = offset;
            gaugeRoot.localPosition = new Vector3(offset.x, offset.y, 0f);
            trackImage.sprite = trackSprite;
            fillImage.sprite = fillSprite;
            trackImage.color = Color.white;
            fillImage.color = Color.white;
        }

        public void SetValue(float minimum, float maximum, float current)
        {
            EnsureInterface();
            slider.minValue = minimum;
            slider.maxValue = Mathf.Max(minimum + 0.0001f, maximum);
            slider.value = Mathf.Clamp(current, minimum, maximum);
        }

        public void SetVisible(bool visible)
        {
            EnsureInterface();
            gaugeRoot.gameObject.SetActive(visible);
            if (!visible)
                gaugeRoot.localPosition = new Vector3(baseOffset.x, baseOffset.y, 0f);
        }

        private void Update()
        {
            if (gaugeRoot == null || !gaugeRoot.gameObject.activeSelf)
                return;

            float bob = Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f * BobCyclesPerSecond) * BobAmplitude;
            gaugeRoot.localPosition = new Vector3(baseOffset.x, baseOffset.y + bob, 0f);
        }

        private void EnsureInterface()
        {
            if (gaugeRoot != null)
                return;

            GameObject canvasObject = new GameObject(
                "Water Can Gauge", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            gaugeRoot = canvasObject.GetComponent<RectTransform>();
            gaugeRoot.sizeDelta = new Vector2(50f, 6f);
            gaugeRoot.localScale = Vector3.one * 0.01f;
            gaugeRoot.pivot = new Vector2(0.5f, 0.5f);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;
            scaler.referencePixelsPerUnit = 100f;

            GameObject sliderObject = new GameObject("Water", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            Stretch(sliderObject.GetComponent<RectTransform>());
            slider = sliderObject.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;

            GameObject trackObject = new GameObject(
                "Fill Area", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            trackObject.transform.SetParent(sliderObject.transform, false);
            RectTransform trackRect = trackObject.GetComponent<RectTransform>();
            Stretch(trackRect);
            trackImage = trackObject.GetComponent<Image>();
            trackImage.raycastTarget = false;
            trackImage.preserveAspect = false;

            GameObject fillObject = new GameObject(
                "Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(trackObject.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(0f, 1f);
            fillRect.offsetMax = new Vector2(0f, -1f);
            fillRect.localScale = Vector3.one;
            fillObject.GetComponent<CanvasRenderer>().cullTransparentMesh = true;
            fillImage = fillObject.GetComponent<Image>();
            fillImage.raycastTarget = false;
            fillImage.maskable = true;
            fillImage.preserveAspect = false;
            slider.fillRect = fillRect;

            gaugeRoot.gameObject.SetActive(false);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }
}
