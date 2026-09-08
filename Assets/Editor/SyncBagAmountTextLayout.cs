#if UNITY_EDITOR
using System;
using System.Linq;
using Item.Inventory;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SyncBagAmountTextLayout
{
    private const string PrefabPath = "Assets/Prefabs/User Interface/Core/Pauze Menu.prefab";

    [InitializeOnLoadMethod]
    private static void QueueSync()
    {
        EditorApplication.delayCall += SyncWhenReady;
    }

    [MenuItem("Tools/UI/Sync Bag Amount Text From References")]
    private static void SyncFromMenu()
    {
        Sync(true, true, true);
    }

    [MenuItem("Tools/UI/Copy Quick Slot 5 Text Amount To All Quick Slots")]
    private static void SyncQuickFromMenu()
    {
        if (!SyncOpenSceneQuickSlots(true))
            Sync(false, true, true);
    }

    [MenuItem("Tools/UI/Copy Bag Slot 6 Energy Slider To All Bag Slots")]
    private static void SyncBagEnergyFromMenu()
    {
        SyncOpenSceneBagEnergy(true);
    }

    private static void SyncWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // Prefer the open scene so a manual Scene Override on Quick Slot 5
        // becomes the source instead of the prefab value on disk.
        if (!SyncOpenSceneQuickSlots(false))
            Sync(false, true, false);

        AlignOpenSceneAmountTextAreas();
        SyncOpenSceneBagEnergy(false);
    }

    private static void AlignOpenSceneAmountTextAreas()
    {
        InventorySlot[] sceneSlots = GetOpenSceneSlots();
        if (sceneSlots.Length == 0)
            return;

        bool changed = false;
        foreach (InventorySlot slot in sceneSlots)
        {
            if (!slot.name.StartsWith("Quick Slot ", StringComparison.Ordinal)
                && !slot.name.StartsWith("Bag Slot ", StringComparison.Ordinal))
                continue;

            changed |= AlignTextAreaToRect(GetAmountText(slot));
        }

        if (changed)
            EditorSceneManager.MarkSceneDirty(sceneSlots[0].gameObject.scene);
    }

    private static bool SyncOpenSceneBagEnergy(bool logResult)
    {
        InventorySlot[] sceneSlots = GetOpenSceneSlots();
        InventorySlot referenceSlot = sceneSlots.FirstOrDefault(slot => slot.name == "Bag Slot 6");
        if (referenceSlot == null)
            return false;

        BagInventoryUIBuilder owner = referenceSlot.GetComponentInParent<BagInventoryUIBuilder>(true);
        InventorySlot[] bagSlots = owner != null
            ? owner.GetComponentsInChildren<InventorySlot>(true)
                .Where(slot => slot.name.StartsWith("Bag Slot ", StringComparison.Ordinal))
                .ToArray()
            : sceneSlots.Where(slot => slot.name.StartsWith("Bag Slot ", StringComparison.Ordinal)).ToArray();

        Slider template = GetEnergySlider(referenceSlot);
        if (template == null)
            return false;

        foreach (InventorySlot slot in bagSlots)
        {
            Slider target = GetEnergySlider(slot);
            if (target == null || target == template)
                continue;

            CopySliderPresentation(template, target);
        }

        EditorSceneManager.MarkSceneDirty(referenceSlot.gameObject.scene);
        if (logResult)
            Debug.Log("Bag Slot 6/Slider_Energy copied to all Bag Slots in the open scene.");
        return true;
    }

    private static InventorySlot[] GetOpenSceneSlots()
    {
        return Resources.FindObjectsOfTypeAll<InventorySlot>()
            .Where(slot => slot.gameObject.scene.IsValid()
                && slot.gameObject.scene.isLoaded
                && !EditorUtility.IsPersistent(slot))
            .ToArray();
    }

    private static bool SyncOpenSceneQuickSlots(bool logResult)
    {
        InventorySlot[] sceneSlots = GetOpenSceneSlots();

        InventorySlot referenceSlot = sceneSlots.FirstOrDefault(slot => slot.name == "Quick Slot 5");
        if (referenceSlot == null)
            return false;

        BagInventoryUIBuilder owner = referenceSlot.GetComponentInParent<BagInventoryUIBuilder>(true);
        InventorySlot[] quickSlots = owner != null
            ? owner.GetComponentsInChildren<InventorySlot>(true)
                .Where(slot => slot.name.StartsWith("Quick Slot ", StringComparison.Ordinal))
                .ToArray()
            : sceneSlots.Where(slot => slot.name.StartsWith("Quick Slot ", StringComparison.Ordinal)).ToArray();

        TextMeshProUGUI template = GetAmountText(referenceSlot);
        if (template == null)
            return false;

        AlignTextAreaToRect(template);

        foreach (InventorySlot slot in quickSlots)
        {
            TextMeshProUGUI target = GetAmountText(slot);
            if (target == null || target == template)
                continue;

            Undo.RecordObjects(new UnityEngine.Object[] { target.rectTransform, target }, "Copy Quick Slot 5 Text Amount");
            CopyLayout(template.rectTransform, target.rectTransform);
            CopyTextStyle(template, target);
            AlignTextAreaToRect(target);
            EditorUtility.SetDirty(target.rectTransform);
            EditorUtility.SetDirty(target);
        }

        EditorSceneManager.MarkSceneDirty(referenceSlot.gameObject.scene);
        if (logResult)
            Debug.Log("Quick Slot 5/Text_Amount copied to Quick Slot 1-4 in the open scene.");
        return true;
    }

    private static void Sync(bool syncBagSlots, bool syncQuickSlots, bool logResult)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            InventorySlot[] slots = root.GetComponentsInChildren<InventorySlot>(true);
            InventorySlot bagReference = slots.FirstOrDefault(slot => slot.name == "Bag Slot 6");
            InventorySlot quickReference = slots.FirstOrDefault(slot => slot.name == "Quick Slot 5");

            if (bagReference == null || quickReference == null)
                throw new InvalidOperationException("Bag Slot 6 or Quick Slot 5 was not found in Pauze Menu.prefab.");

            TextMeshProUGUI bagTemplate = GetAmountText(bagReference);
            TextMeshProUGUI quickTemplate = GetAmountText(quickReference);
            if (bagTemplate == null || quickTemplate == null)
                throw new InvalidOperationException("Text_Amount reference was not found on Bag Slot 6 or Quick Slot 5.");

            bool changed = AlignTextAreaToRect(bagTemplate) | AlignTextAreaToRect(quickTemplate);
            foreach (InventorySlot slot in slots)
            {
                TextMeshProUGUI template = syncQuickSlots && slot.name.StartsWith("Quick Slot ", StringComparison.Ordinal)
                    ? quickTemplate
                    : syncBagSlots && slot.name.StartsWith("Bag Slot ", StringComparison.Ordinal)
                        ? bagTemplate
                        : null;

                TextMeshProUGUI target = GetAmountText(slot);
                if (template == null || target == null || target == template)
                    continue;

                changed |= CopyLayout(template.rectTransform, target.rectTransform);
                changed |= CopyTextStyle(template, target);
                changed |= AlignTextAreaToRect(target);
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            if (logResult)
                Debug.Log(syncBagSlots
                    ? "Bag and Quick Text_Amount layouts synchronized from Bag Slot 6 and Quick Slot 5."
                    : "Quick Text_Amount layout synchronized from Quick Slot 5.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static TextMeshProUGUI GetAmountText(InventorySlot slot)
    {
        SerializedObject serializedSlot = new SerializedObject(slot);
        return serializedSlot.FindProperty("references")
            .FindPropertyRelative("AmountText")
            .objectReferenceValue as TextMeshProUGUI;
    }

    private static Slider GetEnergySlider(InventorySlot slot)
    {
        SerializedObject serializedSlot = new SerializedObject(slot);
        return serializedSlot.FindProperty("references")
            .FindPropertyRelative("energySlider")
            .objectReferenceValue as Slider;
    }

    private static void CopySliderPresentation(Slider source, Slider target)
    {
        Undo.RegisterFullObjectHierarchyUndo(target.gameObject, "Copy Bag Slot 6 Energy Slider");
        RectTransform sourceRoot = source.transform as RectTransform;
        RectTransform targetRoot = target.transform as RectTransform;
        if (sourceRoot != null && targetRoot != null)
            CopyLayout(sourceRoot, targetRoot);
        target.gameObject.SetActive(source.gameObject.activeSelf);

        target.direction = source.direction;
        target.minValue = source.minValue;
        target.maxValue = source.maxValue;
        target.wholeNumbers = source.wholeNumbers;
        target.transition = source.transition;
        target.colors = source.colors;
        target.spriteState = source.spriteState;
        target.animationTriggers = source.animationTriggers;
        target.navigation = source.navigation;

        RectTransform[] sourceRects = source.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform sourceRect in sourceRects)
        {
            if (sourceRect == sourceRoot)
                continue;

            string path = AnimationUtility.CalculateTransformPath(sourceRect, source.transform);
            Transform targetChild = target.transform.Find(path);
            if (targetChild == null || !(targetChild is RectTransform targetRect))
                continue;

            CopyLayout(sourceRect, targetRect);

            Image sourceImage = sourceRect.GetComponent<Image>();
            Image targetImage = targetRect.GetComponent<Image>();
            if (sourceImage != null && targetImage != null)
            {
                EditorUtility.CopySerialized(sourceImage, targetImage);
                EditorUtility.SetDirty(targetImage);
            }

            EditorUtility.SetDirty(targetRect);
        }

        if (targetRoot != null)
            EditorUtility.SetDirty(targetRoot);
        EditorUtility.SetDirty(target);
    }

    private static bool CopyLayout(RectTransform source, RectTransform target)
    {
        bool changed = source.anchorMin != target.anchorMin
            || source.anchorMax != target.anchorMax
            || source.pivot != target.pivot
            || source.anchoredPosition != target.anchoredPosition
            || source.sizeDelta != target.sizeDelta
            || source.localScale != target.localScale
            || source.localEulerAngles != target.localEulerAngles;

        if (!changed)
            return false;

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = source.localScale;
        target.localEulerAngles = source.localEulerAngles;
        return true;
    }

    private static bool CopyTextStyle(TextMeshProUGUI source, TextMeshProUGUI target)
    {
        string originalText = target.text;
        bool originalEnabled = target.enabled;
        bool originalActive = target.gameObject.activeSelf;

        EditorUtility.CopySerialized(source, target);

        target.text = originalText;
        target.enabled = originalEnabled;
        target.gameObject.SetActive(originalActive);
        return true;
    }

    private static bool AlignTextAreaToRect(TextMeshProUGUI text)
    {
        if (text == null || text.margin == Vector4.zero)
            return false;

        Undo.RecordObject(text, "Align Text Amount Area To RectTransform");
        text.margin = Vector4.zero;
        EditorUtility.SetDirty(text);
        return true;
    }
}
#endif
