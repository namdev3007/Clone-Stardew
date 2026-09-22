using UnityEngine;
using UnityEngine.EventSystems;

namespace World.NPC
{
    // Only handles dragging. Unlike EventTrigger, this does not intercept the
    // Button's pointer-down/up events used to select an item for selling.
    [DisallowMultipleComponent]
    public sealed class ShopBagSlotDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private ShopWindowController shop;
        private int slotIndex;

        public void Configure(ShopWindowController controller, int index)
        {
            shop = controller;
            slotIndex = index;
        }

        public void OnBeginDrag(PointerEventData eventData) =>
            shop?.BeginBagDrag(slotIndex, eventData);

        public void OnDrag(PointerEventData eventData) =>
            shop?.UpdateBagDrag(eventData);

        public void OnEndDrag(PointerEventData eventData) =>
            shop?.EndBagDrag(eventData);
    }
}
