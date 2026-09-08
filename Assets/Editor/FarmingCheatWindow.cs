using Item;
using Item.Actions;
using Item.Inventory;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using World;

public class FarmingCheatWindow : EditorWindow
{
    private static readonly string[] CropNames =
    {
        "Carrot", "Onion", "Garlic", "Cabbage", "Potato", "Tomato", "Banana", "Mango"
    };

    private static readonly string[] AgriculturalCropNames =
    {
        "Carrot", "Onion", "Garlic", "Cabbage", "Potato", "Tomato"
    };

    private static readonly string[] FruitTreeNames =
    {
        "Banana", "Mango"
    };

    private const string CropItemRoot = "Assets/ScriptableObjects/Items/Crops";
    private const string ToolItemRoot = "Assets/ScriptableObjects/Items/Tools";

    private int seedIndex;
    private int amount = 99;
    private float growthSkipSeconds = 30f;
    private string message = "Mở Play Mode để dùng cheat.";
    private MessageType messageType = MessageType.Info;

    [MenuItem("Tools/Farming/Cheat Tool")]
    private static void Open()
    {
        GetWindow<FarmingCheatWindow>("Farming Cheats");
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            FarmingCheats.InfiniteWater = false;
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Farming Cheat Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Hãy chạy game (Play Mode) rồi mới dùng các nút cheat.", MessageType.Warning);
            return;
        }

        DrawInventoryCheats();
        EditorGUILayout.Space();
        DrawWorldCheats();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(message, messageType);
    }

    private void DrawInventoryCheats()
    {
        EditorGUILayout.LabelField("Vật phẩm", EditorStyles.boldLabel);
        seedIndex = EditorGUILayout.Popup("Loại hạt", seedIndex, CropNames);
        amount = Mathf.Max(1, EditorGUILayout.IntField("Số lượng", amount));

        if (GUILayout.Button("Đưa hạt đã chọn vào ô số 5"))
            PutSelectedSeedInSlotFive();

        if (GUILayout.Button("Thêm tất cả loại hạt"))
            AddAllSeeds();

        if (GUILayout.Button("Thêm phân bón"))
            AddItem(LoadTool("Item_Fertilizer"), amount, "phân bón");

        if (GUILayout.Button("Nạp đầy bình tưới"))
            RefillWaterCans();

        bool infiniteWater = EditorGUILayout.Toggle("Nước vô hạn", FarmingCheats.InfiniteWater);
        if (infiniteWater != FarmingCheats.InfiniteWater)
        {
            FarmingCheats.InfiniteWater = infiniteWater;
            SetMessage(infiniteWater ? "Đã bật nước vô hạn." : "Đã tắt nước vô hạn.");
        }
    }

    private void DrawWorldCheats()
    {
        EditorGUILayout.LabelField("Ruộng và cây trồng", EditorStyles.boldLabel);
        GridManager grid = Object.FindFirstObjectByType<GridManager>();
        using (new EditorGUI.DisabledScope(grid == null))
        {
            bool showStatuses = EditorGUILayout.Toggle("Hiện status cây trồng", grid != null && grid.FarmStatusIndicatorsVisible);
            if (grid != null && showStatuses != grid.FarmStatusIndicatorsVisible)
            {
                grid.CheatSetFarmStatusVisible(showStatuses);
                SetMessage(showStatuses ? "Đã bật status cây trồng." : "Đã tắt status cây trồng.");
            }

            if (GUILayout.Button("Đưa ruộng về đất bình thường"))
                SetMessage($"Đã đưa {grid.CheatResetAllFarmLand()} ô về đất bình thường.");

            if (GUILayout.Button("Cuốc xen kẽ: 1 hàng cuốc / 1 hàng giữ nguyên"))
                SetMessage($"Đã cuốc xen kẽ {grid.CheatHoeAlternatingRows()} ô đất.");

            if (GUILayout.Button("Trưng bày cây nông nghiệp và mọi giai đoạn"))
                ShowCropStages(grid, AgriculturalCropNames, "cây nông nghiệp");

            if (GUILayout.Button("Trưng bày cây ăn quả và mọi giai đoạn"))
                ShowCropStages(grid, FruitTreeNames, "cây ăn quả");

            if (GUILayout.Button("Tưới toàn bộ ô ruộng"))
                SetMessage($"Đã tưới {grid.CheatWaterAllPlots()} ô ruộng.");

            if (GUILayout.Button("Bón phân toàn bộ"))
                SetMessage($"Đã đánh dấu bón phân cho {grid.CheatFertilizeAllPlots()} ô. Cơ chế phân bón thường hiện đang tạm bỏ qua.");

            growthSkipSeconds = Mathf.Max(1f, EditorGUILayout.FloatField("Số giây tua nhanh", growthSkipSeconds));
            if (GUILayout.Button("Tua nhanh thời gian trồng"))
                SetMessage($"Đã tua nhanh {growthSkipSeconds:0.##} giây cho {grid.CheatAdvanceAllCropGrowth(growthSkipSeconds)} cây.");

            if (GUILayout.Button("Cho toàn bộ cây lớn hoàn toàn"))
                SetMessage($"Đã cho {grid.CheatGrowAllCrops()} cây phát triển đến lúc thu hoạch.");

            if (GUILayout.Button("Thu hoạch toàn bộ cây đã chín"))
                SetMessage($"Đã thu hoạch {grid.CheatHarvestAllCrops()} cây.");
        }

        if (grid == null)
            EditorGUILayout.HelpBox("Không tìm thấy GridManager trong scene đang chạy.", MessageType.Warning);
    }

    private void PutSelectedSeedInSlotFive()
    {
        Inventory inventory = FindInventory();
        ItemData seed = LoadSeed(CropNames[seedIndex]);
        if (inventory == null || seed == null)
            return;

        const int slotIndex = 4;
        InventoryItem current = inventory.GetItem(slotIndex);
        if (current != null)
        {
            if (!current.Data.IsRemoveable)
            {
                SetMessage("Ô số 5 đang chứa vật phẩm không thể thay thế.", MessageType.Warning);
                return;
            }
            inventory.RemoveItem(slotIndex, true);
        }

        bool added = inventory.AddItem(seed, amount, slotIndex, false);
        SetMessage(added
            ? $"Đã đưa {amount} hạt {CropNames[seedIndex]} vào ô số 5."
            : "Không thể đưa hạt vào ô số 5.", added ? MessageType.Info : MessageType.Error);
    }

    private void ShowCropStages(GridManager grid, string[] cropNames, string groupDisplayName)
    {
        List<ItemAction_PlantSeed> seedActions = new List<ItemAction_PlantSeed>();
        int requiredPlots = 0;

        for (int i = 0; i < cropNames.Length; i++)
        {
            ItemData seed = LoadSeed(cropNames[i]);
            ItemAction_PlantSeed action = seed != null ? seed.Action as ItemAction_PlantSeed : null;
            if (action == null || action.ShowcaseStageCount <= 0)
                continue;

            seedActions.Add(action);
            requiredPlots += action.ShowcaseStageCount;
        }

        Inventory playerInventory = Object.FindFirstObjectByType<Inventory>();
        Vector3 showcaseCenter = playerInventory != null ? playerInventory.transform.position : grid.transform.position;
        int[] stageCounts = new int[seedActions.Count];
        bool[] perennialRows = new bool[seedActions.Count];
        for (int i = 0; i < seedActions.Count; i++)
        {
            stageCounts[i] = seedActions[i].ShowcaseStageCount;
            perennialRows[i] = seedActions[i].UsesPerennialFootprint;
        }
        List<List<Vector3Int>> rows = grid.CheatPrepareCropShowcaseRows(stageCounts, perennialRows, showcaseCenter);
        int planted = 0;
        for (int actionIndex = 0; actionIndex < seedActions.Count; actionIndex++)
        {
            ItemAction_PlantSeed action = seedActions[actionIndex];
            List<Vector3Int> row = actionIndex < rows.Count ? rows[actionIndex] : null;
            if (row == null)
                continue;

            for (int stageIndex = 0; stageIndex < action.ShowcaseStageCount && stageIndex < row.Count; stageIndex++)
            {
                if (action.CheatPlantShowcase(grid, row[stageIndex], stageIndex))
                    planted++;
            }
        }

        MessageType type = planted == requiredPlots ? MessageType.Info : MessageType.Warning;
        SetMessage($"Đã trưng bày {planted}/{requiredPlots} sprite {groupDisplayName}. Mỗi loại nằm trên một hàng; cây cuối hàng có thể thu hoạch.", type);
    }

    private void AddAllSeeds()
    {
        Inventory inventory = FindInventory();
        if (inventory == null)
            return;

        int added = 0;
        for (int i = 0; i < CropNames.Length; i++)
        {
            ItemData seed = LoadSeed(CropNames[i]);
            if (seed != null && inventory.AddItem(seed, amount))
                added++;
        }
        SetMessage($"Đã thêm {added}/{CropNames.Length} loại hạt, mỗi loại {amount}.");
    }

    private void AddItem(ItemData item, int itemAmount, string displayName)
    {
        Inventory inventory = FindInventory();
        if (inventory == null || item == null)
            return;

        bool added = inventory.AddItem(item, itemAmount);
        SetMessage(added ? $"Đã thêm {itemAmount} {displayName}." : $"Không thể thêm {displayName}.",
            added ? MessageType.Info : MessageType.Error);
    }

    private void RefillWaterCans()
    {
        Inventory inventory = FindInventory();
        ItemData waterCan = LoadTool("Item_Tool_WaterCan");
        if (inventory == null || waterCan == null)
            return;

        int count = 0;
        for (int i = 0; i < inventory.InventorySize; i++)
        {
            InventoryItem item = inventory.GetItem(i);
            if (item == null || item.Data != waterCan)
                continue;

            ItemEnergy energy = item.Energy;
            energy.current = energy.max;
            item.Energy = energy;
            inventory.ReloadItemSlot(i);
            count++;
        }
        SetMessage(count > 0 ? $"Đã nạp đầy {count} bình tưới." : "Không tìm thấy bình tưới trong hành trang.",
            count > 0 ? MessageType.Info : MessageType.Warning);
    }

    private Inventory FindInventory()
    {
        Inventory inventory = Object.FindFirstObjectByType<Inventory>();
        if (inventory == null)
            SetMessage("Không tìm thấy Inventory của Player trong scene đang chạy.", MessageType.Error);
        return inventory;
    }

    private ItemData LoadSeed(string cropName)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>($"{CropItemRoot}/Item_Seed_{cropName}.asset");
        if (item == null)
            SetMessage($"Không tìm thấy asset hạt giống {cropName}.", MessageType.Error);
        return item;
    }

    private ItemData LoadTool(string assetName)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>($"{ToolItemRoot}/{assetName}.asset");
        if (item == null)
            SetMessage($"Không tìm thấy asset {assetName}.", MessageType.Error);
        return item;
    }

    private void SetMessage(string value, MessageType type = MessageType.Info)
    {
        message = value;
        messageType = type;
        Repaint();
    }
}
