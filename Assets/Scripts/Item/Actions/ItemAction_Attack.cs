using Audio;
using Combat;
using Combat.Data;
using Entity_Components;
using Entity_Components.Character;
using Entity_Components.Player;
using Referencing.Scriptable_Pool;
using System.Collections;
using UnityEngine;
using World.Objects;

namespace Item.Actions
{
    [CreateAssetMenu(fileName = "Item Action Attack", menuName = "Items/Item Actions/Attack")]
    public class ItemAction_Attack : ItemAction
    {
        [SerializeField]
        private ScriptablePoolContainer damageVolumePool;

        [SerializeField]
        private string[] targetTags;

        [SerializeField]
        private int damage;

        [SerializeField]
        private float speed;

        [SerializeField]
        private float attackDistance;

        [SerializeField, Tooltip("Is one attack able to hit multiple targets")]
        private bool hitMultipleTargets;

        private enum AttackType { Smash, Slash };

        [SerializeField]
        private AttackType attackType = AttackType.Smash;

        public override IEnumerator ItemUseAction(Inventory.Inventory userInventory, int itemIndex)
        {
            Aimer getAimer = userInventory.GetComponent<Aimer>();
            Mover getMover = userInventory.GetComponent<Mover>();
            GridSelector getGridSelector = userInventory.GetComponent<GridSelector>();

            if (getMover.IsMovementFrozen)
                yield break;

            Inventory.InventoryItem heldItem = userInventory.GetItem(itemIndex);
            bool isCropAxe = heldItem?.Data?.ItemName == "Axe";
            Crop axeTarget = null;
            World.ClearableOrchardProp orchardPropTarget = null;
            if (isCropAxe)
            {
                if (heldItem.Data.HasEnergy && heldItem.Energy.current <= heldItem.Energy.min)
                    yield break;
                if (getGridSelector == null)
                    yield break;

                Vector3Int selectedCell = getGridSelector.GetGridSelectionPosition();
                orchardPropTarget = World.ClearableOrchardProp.FindAtCell(selectedCell);
                axeTarget = getGridSelector?.GetGridManager()?.GetCrop(selectedCell);
                // The axe is deliberately isolated from scenery trees and from
                // ordinary vegetable crops.
                if (orchardPropTarget == null && (axeTarget == null || !axeTarget.UsesPerennialFootprint))
                    yield break;
            }

            Vector2 aimDirection = getGridSelector != null
                ? getGridSelector.GetMouseLookDirection()
                : getAimer.GetAimDirection();
            Vector2 attackLocation = (Vector2)userInventory.transform.position + (aimDirection * attackDistance);

            getAimer.LookAt(attackLocation);

            FullBodyPlayerSpriteAnimator[] getEntityAnimator = userInventory.GetComponentsInChildren<FullBodyPlayerSpriteAnimator>();

            float animationTime = 0;

            for (int i = 0; i < getEntityAnimator.Length; i++)
            {
                switch (attackType)
                {
                    case AttackType.Smash:
                        getEntityAnimator[i].PlayAction(FullBodyPlayerSpriteAnimator.ActionType.Chop, speed);
                        animationTime = 1 / speed;
                        break;
                    case AttackType.Slash:
                        getEntityAnimator[i].PlayAction(FullBodyPlayerSpriteAnimator.ActionType.Chop, speed);
                        animationTime = 1 / speed;
                        break;
                    default:
                        break;
                }
            }

            GameAudioService.PlayChopSwing();

            getMover.FreezeMovement(true);

            // The release sits in a finally block: an interrupted swing must
            // never leave the player frozen.
            try
            {
                yield return new WaitForSeconds(animationTime * 0.5f);

                if (isCropAxe)
                {
                    bool isTree = axeTarget != null || (orchardPropTarget != null && orchardPropTarget.IsTreeOrWood);
                    bool hitAccepted = orchardPropTarget != null
                        ? orchardPropTarget.TryHit()
                        : axeTarget != null && axeTarget.TryUseAxe();
                    if (hitAccepted)
                    {
                        userInventory.TryConsumeToolDurability(itemIndex);
                        if (isTree)
                            GameAudioService.PlayChopTree();
                        else
                            GameAudioService.PlayChopFoliage();
                    }

                    yield return new WaitForSeconds(animationTime * 0.5f);
                    yield break;
                }

                GameObject damageVolume = damageVolumePool.Retrieve(attackLocation, new Quaternion());

                damageVolume.GetComponent<DamageVolume>().Configure(new DamageVolumeConfiguration()
                {
                    Damage = damage,
                    Owner = userInventory.gameObject,
                    ActiveTime = 0.5f,
                    TargetTags = targetTags,
                    AllowDuplicateDamage = true,
                    CanDamageMultiple = hitMultipleTargets,
                    Size = new Vector2(0.10f, 0.10f)
                });

                yield return new WaitForSeconds(animationTime * 0.5f);
                yield return null;
            }
            finally
            {
                getMover?.FreezeMovement(false);
            }
        }

        public override void ItemAcquisitionAction(Inventory.Inventory userInventory, int itemIndex)
        {
        
        }

        public override bool ItemUseCondition(Inventory.Inventory userInventory, int itemIndex)
        {
            return true;
        }
    }
}
