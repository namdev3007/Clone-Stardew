using Entity_Components;
using Entity_Components.Character;
using Entity_Components.Player;
using Event.Events;
using Referencing.Scriptable_Reference;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Weather;
using World;
using World.NPC;

namespace Item.Actions
{
    [CreateAssetMenu(fileName = "Item Action Dig Hole", menuName = "Items/Item Actions/Dig Hole")]
    public class ItemAction_DigHole : ItemAction
    {
        [SerializeField]
        private float speed;

        [SerializeField]
        private ScriptableReference gridManagerReference;

        [SerializeField]
        private UnityEvent onSuccess;

        [SerializeField]
        private WeatherEvent weatherEvent;

        // Cache repeated use cases
        [System.NonSerialized]
        private GridManager gridManager;

        [System.NonSerialized]
        private EWeather currentWeather;
    
        public override IEnumerator ItemUseAction(Inventory.Inventory userInventory, int itemIndex)
        {
            GridSelector gridSelector = userInventory.GetComponent<GridSelector>();

            if (gridManager == null)
            {
                gridManager = gridManagerReference.Reference?.GetComponent<GridManager>();
            }

            if (gridManager != null)
            {
                if (weatherEvent.HasParameter)
                {
                    currentWeather = weatherEvent.LastParameter;
                }
            }

            if (gridSelector != null && gridManager != null)
            {
                Vector3Int selectionLocation = gridSelector.GetGridSelectionPosition();

                Aimer aimer = userInventory.GetComponent<Aimer>();
                aimer?.SetAimDirection(gridSelector.GetMouseLookDirection());

                Mover getMover = userInventory.GetComponent<Mover>();
                getMover?.FreezeMovement(true);

                FullBodyPlayerSpriteAnimator[] getEntityAnimator = userInventory.GetComponentsInChildren<FullBodyPlayerSpriteAnimator>();

                float animationTime = 0;

                for (int i = 0; i < getEntityAnimator.Length; i++)
                {
                    getEntityAnimator[i].PlayAction(FullBodyPlayerSpriteAnimator.ActionType.Hoe, speed);
                    animationTime = 1 / speed;
                }

                gridSelector.SetFrozen(true);

                yield return new WaitForSeconds(animationTime * 0.5f);

                if (!gridManager.HasDirtHole(selectionLocation) && !gridManager.HasWater(selectionLocation) && gridManager.HasDirt(selectionLocation))
                {
                    gridManager.SetDirtHoleTile(selectionLocation);
                    gridManager.RegisterFarmPlot(selectionLocation);
                    TutorialProgressService.Instance.RecordHoedCell(selectionLocation);

                    if (currentWeather == EWeather.Rainy)
                    {
                        gridManager.SetWateredDirtTile(selectionLocation);
                    }

                    onSuccess.Invoke();
                }

                yield return new WaitForSeconds(animationTime * 0.5f);

                getMover.FreezeMovement(false);
                gridSelector.SetFrozen(false);

            }
        }

        public override bool ItemUseCondition(Inventory.Inventory userInventory, int itemIndex)
        {
            return true;
        }

        public override void ItemActiveAction(Inventory.Inventory userInventory, int itemIndex)
        {
            //userInventory.GetComponent<GridSelector>()?.Display(true);
        }

        public override void ItemUnactiveAction(Inventory.Inventory userInventory, int itemIndex)
        {
            //userInventory.GetComponent<GridSelector>()?.Display(false);
        }
    }
}
