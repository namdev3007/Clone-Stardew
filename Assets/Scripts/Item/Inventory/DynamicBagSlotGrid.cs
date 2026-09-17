using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Item.Inventory
{
    /// <summary>
    /// Turns the authored Bag Slots region into a vertical scroll view and
    /// appends cloned slots in rows as the Inventory capacity grows.
    /// Quick Slots stay outside this scroll view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DynamicBagSlotGrid : MonoBehaviour
    {
        private const int QuickSlotCount = 5;
        private const int SlotsPerRow = 7;
        private const float SlotSize = 22f;
        private const float Spacing = 3f;

        private RectTransform viewport;
        private RectTransform content;
        private InventorySlot template;
        private Sprite normalSprite;
        private Sprite selectedSprite;
        private bool configured;

        public void EnsureCapacity(int inventorySize)
        {
            if (!TryConfigure())
                return;

            int requiredBagSlots = Mathf.Max(0, inventorySize - QuickSlotCount);
            List<InventorySlot> bagSlots = GetBagSlots();

            while (bagSlots.Count < requiredBagSlots)
            {
                InventorySlot clone = Instantiate(template.gameObject, content).GetComponent<InventorySlot>();
                clone.gameObject.SetActive(true);
                bagSlots.Add(clone);
            }

            int rowCount = Mathf.Max(1, Mathf.CeilToInt(requiredBagSlots / (float)SlotsPerRow));
            List<RectTransform> rows = EnsureRows(rowCount);

            for (int i = 0; i < bagSlots.Count; i++)
            {
                InventorySlot slot = bagSlots[i];
                int inventoryIndex = QuickSlotCount + i;
                int rowIndex = i / SlotsPerRow;

                slot.transform.SetParent(rows[rowIndex], false);
                slot.transform.SetAsLastSibling();
                slot.gameObject.SetActive(i < requiredBagSlots);
                slot.gameObject.name = $"Bag Slot {i + 1}";

                RectTransform rect = (RectTransform)slot.transform;
                rect.sizeDelta = new Vector2(SlotSize, SlotSize);
                rect.localScale = Vector3.one;
                LayoutElement slotLayout = slot.GetComponent<LayoutElement>();
                if (slotLayout == null)
                    slotLayout = slot.gameObject.AddComponent<LayoutElement>();
                slotLayout.minWidth = SlotSize;
                slotLayout.preferredWidth = SlotSize;
                slotLayout.flexibleWidth = 0f;
                slotLayout.minHeight = SlotSize;
                slotLayout.preferredHeight = SlotSize;
                slotLayout.flexibleHeight = 0f;
                slot.ConfigureBagSlot(inventoryIndex, normalSprite, selectedSprite, false);
            }

            for (int i = 0; i < rows.Count; i++)
                rows[i].gameObject.SetActive(i < rowCount);

            float height = rowCount * SlotSize + Mathf.Max(0, rowCount - 1) * Spacing;
            content.sizeDelta = new Vector2(0f, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private bool TryConfigure()
        {
            if (configured)
                return viewport != null && content != null && template != null;

            viewport = transform.Find("Bag Slots") as RectTransform;
            if (viewport == null)
                return false;

            List<InventorySlot> existingSlots = GetComponentsInChildren<InventorySlot>(true)
                .Where(slot => slot.name.StartsWith("Bag Slot ", StringComparison.Ordinal))
                .OrderBy(GetBagNumber)
                .ToList();
            if (existingSlots.Count == 0)
                return false;

            template = existingSlots.FirstOrDefault(slot => slot.name == "Bag Slot 6") ?? existingSlots[0];
            normalSprite = template.GetComponent<Image>()?.sprite;
            Transform highlight = template.transform.Find("Image_Highlight");
            selectedSprite = highlight != null ? highlight.GetComponent<Image>()?.sprite : normalSprite;

            VerticalLayoutGroup oldLayout = viewport.GetComponent<VerticalLayoutGroup>();
            if (oldLayout != null)
                oldLayout.enabled = false;

            if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();

            Transform existingContent = viewport.Find("Content");
            if (existingContent == null)
            {
                GameObject contentObject = new GameObject("Content", typeof(RectTransform));
                contentObject.layer = viewport.gameObject.layer;
                content = contentObject.GetComponent<RectTransform>();
                content.SetParent(viewport, false);
            }
            else
            {
                content = (RectTransform)existingContent;
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            if (contentLayout == null)
                contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset();
            contentLayout.spacing = Spacing;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            foreach (Transform child in viewport.Cast<Transform>().ToArray())
            {
                if (child == content)
                    continue;
                if (child.name.StartsWith("Bag Row ", StringComparison.Ordinal))
                    child.SetParent(content, false);
            }

            ScrollRect scrollRect = viewport.GetComponent<ScrollRect>();
            if (scrollRect == null)
                scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 18f;
            // No visible scrollbar: extra rows are scrolled with the mouse wheel.
            scrollRect.verticalScrollbar = null;

            configured = true;
            return true;
        }

        private List<RectTransform> EnsureRows(int count)
        {
            List<RectTransform> rows = content.Cast<Transform>()
                .Where(child => child.name.StartsWith("Bag Row ", StringComparison.Ordinal))
                .Select(child => (RectTransform)child)
                .OrderBy(row => ParseTrailingNumber(row.name))
                .ToList();

            while (rows.Count < count)
            {
                GameObject rowObject = new GameObject($"Bag Row {rows.Count + 1}", typeof(RectTransform));
                rowObject.layer = content.gameObject.layer;
                RectTransform row = rowObject.GetComponent<RectTransform>();
                row.SetParent(content, false);
                rows.Add(row);
            }

            foreach (RectTransform row in rows)
            {
                LayoutElement element = row.GetComponent<LayoutElement>();
                if (element == null)
                    element = row.gameObject.AddComponent<LayoutElement>();
                element.minHeight = SlotSize;
                element.preferredHeight = SlotSize;
                element.flexibleHeight = 0f;

                HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
                if (layout == null)
                    layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset();
                layout.spacing = Spacing;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

            return rows;
        }

        private List<InventorySlot> GetBagSlots()
        {
            return GetComponentsInChildren<InventorySlot>(true)
                .Where(slot => slot.name.StartsWith("Bag Slot ", StringComparison.Ordinal))
                .OrderBy(GetBagNumber)
                .ToList();
        }

        private static int GetBagNumber(InventorySlot slot)
        {
            return ParseTrailingNumber(slot.name);
        }

        private static int ParseTrailingNumber(string value)
        {
            int separator = value.LastIndexOf(' ');
            return separator >= 0 && int.TryParse(value.Substring(separator + 1), out int number)
                ? number
                : int.MaxValue;
        }
    }
}
