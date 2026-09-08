#if UNITY_EDITOR
using Combat;
using Combat.Listeners;
using Item;
using Item.Actions;
using Item.Inventory;
using Referencing;
using Referencing.Scriptable_Assets;
using Referencing.Scriptable_Reference;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using World;
using World.Objects;

public static class SetupTimedCropSystem
{
    private const string FarmingRoot = "Assets/ScriptableObjects/Farming";
    private const string DefinitionRoot = FarmingRoot + "/Crop Definitions";
    private const string CropItemRoot = "Assets/ScriptableObjects/Items/Crops";
    private const string ActionRoot = "Assets/ScriptableObjects/Item Actions";
    private const string PrefabPath = "Assets/Prefabs/World/Outdoors/Crop Timed.prefab";
    private const string SaveablePrefabPath = "Assets/ScriptableObjects/Referencing/Saveable Prefabs/Prefab Crop Timed.asset";
    private const string StatusAssetPath = "Assets/Resources/Farming/Farm Status Sprites.asset";
    private const string StartingItemsPath = "Assets/ScriptableObjects/Start Inventories/StartingItems_Player.asset";
    private const string GridReferenceGuid = "3fae82f7a21ee7147a3130aa736701b1";

    private sealed class CropSpec
    {
        public string key;
        public string displayName;
        public float firstSeconds;
        public float regrowthSeconds;
        public int maximumHarvests;
        public int regrowthStageStart;
        public string growthSheet;
        public string seedSprite;
        public string productSprite;
        public bool perennialTree;
        public int[] growthSpriteOrder;
    }

    private static readonly CropSpec[] Crops =
    {
        new CropSpec { key = "Carrot", displayName = "Carrot", firstSeconds = 15f, maximumHarvests = 1, growthSheet = "Assets/Sprites/cây trồng/carrot/carrot_tree_200.png", seedSprite = "Assets/Sprites/cây trồng/hạt giống/carot.png", productSprite = "Assets/Sprites/cây trồng/carrot/carrot_200.png" },
        new CropSpec { key = "Onion", displayName = "Onion", firstSeconds = 30f, maximumHarvests = 1, growthSheet = "Assets/Sprites/cây trồng/onion/oniontree_200.png", seedSprite = "Assets/Sprites/cây trồng/hạt giống/onion.png", productSprite = "Assets/Sprites/cây trồng/onion/onion_200.png" },
        new CropSpec { key = "Garlic", displayName = "Garlic", firstSeconds = 45f, maximumHarvests = 1, growthSheet = "Assets/Sprites/cây trồng/garlic/garlictree_200.png", seedSprite = "Assets/Sprites/cây trồng/hạt giống/garlic.png", productSprite = "Assets/Sprites/cây trồng/garlic/garlic_200.png" },
        new CropSpec { key = "Cabbage", displayName = "Cabbage", firstSeconds = 60f, maximumHarvests = 1, growthSheet = "Assets/Sprites/cây trồng/cabbage/cabbagetree_200.png", seedSprite = "Assets/Sprites/cây trồng/hạt giống/cabbage.png", productSprite = "Assets/Sprites/cây trồng/cabbage/cabbage_200.png" },
        new CropSpec { key = "Potato", displayName = "Potato", firstSeconds = 90f, maximumHarvests = 1, growthSheet = "Assets/Sprites/cây trồng/potato/potatotree_200.png", seedSprite = "Assets/Sprites/cây trồng/hạt giống/potato.png", productSprite = "Assets/Sprites/cây trồng/potato/potato_200.png" },
        new CropSpec { key = "Tomato", displayName = "Tomato", firstSeconds = 120f, regrowthSeconds = 30f, maximumHarvests = 3, regrowthStageStart = 4, growthSheet = "Assets/Sprites/cây trồng/tomato/tomatotree_200.png", seedSprite = "Assets/Sprites/cây trồng/hạt giống/tomato.png", productSprite = "Assets/Sprites/cây trồng/tomato/tomato_200.png" },
        new CropSpec { key = "Banana", displayName = "Banana", firstSeconds = 180f, regrowthSeconds = 60f, maximumHarvests = int.MaxValue, regrowthStageStart = 4, perennialTree = true, growthSpriteOrder = new[] { 4, 5, 6, 7, 0, 1, 2, 3 }, growthSheet = "Assets/Sprites/cây trồng/cây lâu năm/banana/bananatree_200.png", seedSprite = "Assets/Sprites/cây trồng/hạt giống/banana.png", productSprite = "Assets/Sprites/cây trồng/cây lâu năm/banana/banana_200.png" },
        new CropSpec { key = "Mango", displayName = "Mango", firstSeconds = 300f, regrowthSeconds = 120f, maximumHarvests = int.MaxValue, regrowthStageStart = 4, perennialTree = true, growthSheet = "Assets/Sprites/cây trồng/cây lâu năm/mango/mangotree_200.png", seedSprite = "Assets/Sprites/cây trồng/hạt giống/mango.png", productSprite = "Assets/Sprites/cây trồng/cây lâu năm/mango/mango_200.png" }
    };

    [InitializeOnLoadMethod]
    private static void QueueSetup()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Run();
        };
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Farming/Setup Timed Crop System")]
    public static void Run()
    {
        EnsureFolder(FarmingRoot);
        EnsureFolder(DefinitionRoot);
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Farming");

        ScriptableReference gridReference = AssetDatabase.LoadAssetAtPath<ScriptableReference>(AssetDatabase.GUIDToAssetPath(GridReferenceGuid));
        if (gridReference == null)
        {
            Debug.LogError("Timed crop setup: Grid Manager reference was not found.");
            return;
        }

        CreateStatusSpriteSet();
        GameObject cropPrefab = CreateCropPrefab();
        SaveablePrefab saveableCrop = CreateSaveableCrop(cropPrefab);

        ItemData carrotSeed = null;
        foreach (CropSpec spec in Crops)
        {
            ItemData harvestedItem = CreateHarvestedItem(spec);
            CropDefinition definition = CreateDefinition(spec, harvestedItem);
            ItemAction_PlantSeed action = CreateSeedAction(spec, definition, saveableCrop, gridReference);
            ItemData seed = CreateSeedItem(spec, action);
            if (spec.key == "Carrot") carrotSeed = seed;
        }

        ItemData fertilizer = CreateFertilizer(gridReference);
        UpdateStartingItems(carrotSeed, fertilizer);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Timed crop system setup completed: 8 timed crops and status sprites are ready.");
    }

    private static void CreateStatusSpriteSet()
    {
        FarmStatusSpriteSet set = CreateOrLoad<FarmStatusSpriteSet>(StatusAssetPath);
        SerializedObject serialized = new SerializedObject(set);
        serialized.FindProperty("fertilize").objectReferenceValue = LoadFirstSprite("Assets/Sprites/stat-trạng thái cây trồng/stat-bón phân.png");
        serialized.FindProperty("sowSeed").objectReferenceValue = LoadFirstSprite("Assets/Sprites/stat-trạng thái cây trồng/stat-chưa gieo hạt.png");
        serialized.FindProperty("water").objectReferenceValue = LoadFirstSprite("Assets/Sprites/stat-trạng thái cây trồng/stat-tưới nước.png");
        serialized.FindProperty("harvest").objectReferenceValue = LoadFirstSprite("Assets/Sprites/stat-trạng thái cây trồng/stat-đã có thể thu hoạch.png");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateCropPrefab()
    {
        const string sourcePath = "Assets/Prefabs/World/Outdoors/Crop Kale.prefab";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null) throw new InvalidOperationException("Crop Kale prefab was not found.");

        GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            root.name = "Crop Timed";
            root.transform.position = Vector3.zero;

            foreach (OnDeathListener listener in root.GetComponents<OnDeathListener>())
                UnityEngine.Object.DestroyImmediate(listener);

            Crop crop = root.GetComponent<Crop>();
            Health health = root.GetComponent<Health>();
            ItemDropper dropper = root.GetComponent<ItemDropper>();
            SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>();
            if (crop == null || health == null || dropper == null || renderer == null)
                throw new InvalidOperationException("Crop Kale prefab is missing a required component.");

            SerializedObject cropData = new SerializedObject(crop);
            cropData.FindProperty("health").objectReferenceValue = health;
            cropData.FindProperty("itemDropper").objectReferenceValue = dropper;
            cropData.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            cropData.FindProperty("groundWetTilemapName").stringValue = "Dirt Hole Wet";
            cropData.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject healthData = new SerializedObject(health);
            healthData.FindProperty("startInvulnerable").boolValue = true;
            healthData.ApplyModifiedPropertiesWithoutUndo();

            renderer.sprite = LoadSprites(Crops[0].growthSheet).FirstOrDefault();
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static SaveablePrefab CreateSaveableCrop(GameObject prefab)
    {
        SaveablePrefab asset = CreateOrLoad<SaveablePrefab>(SaveablePrefabPath);
        SerializedObject serialized = new SerializedObject(asset);
        serialized.FindProperty("prefab").objectReferenceValue = prefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return asset;
    }

    private static ItemData CreateHarvestedItem(CropSpec spec)
    {
        string path = $"{CropItemRoot}/Item_{spec.key}.asset";
        ItemData item = CreateOrLoad<ItemData>(path);
        ConfigureItem(item, LoadFirstSprite(spec.productSprite), spec.displayName, $"Harvested {spec.displayName}", true, true, true, null, 0.5f);
        return item;
    }

    private static CropDefinition CreateDefinition(CropSpec spec, ItemData harvestedItem)
    {
        CropDefinition definition = CreateOrLoad<CropDefinition>($"{DefinitionRoot}/Crop {spec.key}.asset");
        SerializedObject serialized = new SerializedObject(definition);
        serialized.FindProperty("displayName").stringValue = spec.displayName;
        serialized.FindProperty("firstGrowthSeconds").floatValue = spec.firstSeconds;
        serialized.FindProperty("regrowthSeconds").floatValue = spec.regrowthSeconds;
        serialized.FindProperty("maximumHarvests").intValue = spec.maximumHarvests;
        serialized.FindProperty("regrowthStageStart").intValue = spec.regrowthStageStart;
        serialized.FindProperty("perennialTree").boolValue = spec.perennialTree;
        Sprite[] growthSprites = LoadSprites(spec.growthSheet);
        if (spec.growthSpriteOrder != null && spec.growthSpriteOrder.Length == growthSprites.Length)
            growthSprites = spec.growthSpriteOrder.Select(index => growthSprites[index]).ToArray();
        SetObjectArray(serialized.FindProperty("growthSprites"), growthSprites);
        SerializedProperty stageOffsets = serialized.FindProperty("stagePositionOffsets");
        if (stageOffsets.arraySize != growthSprites.Length)
            stageOffsets.arraySize = growthSprites.Length;
        serialized.FindProperty("harvestedItem").objectReferenceValue = harvestedItem;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static ItemAction_PlantSeed CreateSeedAction(CropSpec spec, CropDefinition definition, SaveablePrefab prefab, ScriptableReference gridReference)
    {
        ItemAction_PlantSeed action = CreateOrLoad<ItemAction_PlantSeed>($"{ActionRoot}/Item Action Plant Seed {spec.key}.asset");
        SerializedObject serialized = new SerializedObject(action);
        serialized.FindProperty("plantablePrefab").objectReferenceValue = prefab;
        serialized.FindProperty("gridManagerReference").objectReferenceValue = gridReference;
        serialized.FindProperty("cropDefinition").objectReferenceValue = definition;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return action;
    }

    private static ItemData CreateSeedItem(CropSpec spec, ItemAction_PlantSeed action)
    {
        ItemData item = CreateOrLoad<ItemData>($"{CropItemRoot}/Item_Seed_{spec.key}.asset");
        ConfigureItem(item, LoadFirstSprite(spec.seedSprite), $"{spec.displayName} Seed", $"Used to grow {spec.displayName}", true, true, true, action);
        return item;
    }

    private static ItemData CreateFertilizer(ScriptableReference gridReference)
    {
        ItemAction_Fertilizer action = CreateOrLoad<ItemAction_Fertilizer>($"{ActionRoot}/Item Action Fertilizer.asset");
        SerializedObject actionData = new SerializedObject(action);
        actionData.FindProperty("gridManagerReference").objectReferenceValue = gridReference;
        actionData.ApplyModifiedPropertiesWithoutUndo();

        ItemData item = CreateOrLoad<ItemData>("Assets/ScriptableObjects/Items/Tools/Item_Fertilizer.asset");
        ConfigureItem(item, LoadFirstSprite("Assets/Sprites/props-items/Công cụ/phân bón.png"), "Fertilizer", "One bag fertilizes 12 farm plots", true, true, true, action);
        SerializedObject itemData = new SerializedObject(item);
        itemData.FindProperty("canStack").boolValue = false;
        itemData.FindProperty("hasEnergy").boolValue = true;
        SerializedProperty fertilizerEnergy = itemData.FindProperty("energyStartValue");
        fertilizerEnergy.FindPropertyRelative("min").floatValue = 0f;
        fertilizerEnergy.FindPropertyRelative("max").floatValue = 12f;
        fertilizerEnergy.FindPropertyRelative("current").floatValue = 12f;
        itemData.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private static void UpdateStartingItems(ItemData carrotSeed, ItemData fertilizer)
    {
        ItemCollection collection = AssetDatabase.LoadAssetAtPath<ItemCollection>(StartingItemsPath);
        if (collection == null || carrotSeed == null || fertilizer == null) return;

        ItemData gold = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/ScriptableObjects/Items/Tools/Item_Gold.asset");
        ItemData waterCan = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/ScriptableObjects/Items/Tools/Item_Tool_WaterCan.asset");
        ItemData axe = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/ScriptableObjects/Items/Tools/Item_Tool_Axe.asset");
        ItemData hoe = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/ScriptableObjects/Items/Tools/Item_Tool_Shovel.asset");

        ItemData[] data = { gold, waterCan, axe, hoe, carrotSeed, fertilizer };
        int[] amounts = { 200, 0, 0, 0, 15, 0 };
        SerializedObject serialized = new SerializedObject(collection);
        SerializedProperty items = serialized.FindProperty("items");
        items.arraySize = data.Length;
        for (int i = 0; i < data.Length; i++)
        {
            SerializedProperty entry = items.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("Data").objectReferenceValue = data[i];
            entry.FindPropertyRelative("Amount").intValue = amounts[i];
            SerializedProperty energy = entry.FindPropertyRelative("Energy");
            energy.FindPropertyRelative("min").floatValue = 0f;
            energy.FindPropertyRelative("max").floatValue = data[i] == fertilizer ? 12f : 0f;
            energy.FindPropertyRelative("current").floatValue = data[i] == fertilizer ? 12f : 0f;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureItem(ItemData item, Sprite icon, string itemName, string description, bool canStack, bool hasSlot, bool removeable, ItemAction action, float worldDropScale = 1f)
    {
        SerializedObject serialized = new SerializedObject(item);
        serialized.FindProperty("icon").objectReferenceValue = icon;
        serialized.FindProperty("itemName").stringValue = itemName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("canStack").boolValue = canStack;
        serialized.FindProperty("hasSlot").boolValue = hasSlot;
        serialized.FindProperty("hasEnergy").boolValue = false;
        serialized.FindProperty("isRemoveable").boolValue = removeable;
        serialized.FindProperty("action").objectReferenceValue = action;
        serialized.FindProperty("worldDropScale").floatValue = worldDropScale;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T CreateOrLoad<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        if (asset is ScriptableAsset referenceable)
            referenceable.GenerateNewGuid();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static Sprite[] LoadSprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(sprite => NumericSuffix(sprite.name))
            .ToArray();
    }

    private static Sprite LoadFirstSprite(string path) => LoadSprites(path).FirstOrDefault();

    private static int NumericSuffix(string name)
    {
        int separator = name.LastIndexOf('_');
        return separator >= 0 && int.TryParse(name.Substring(separator + 1), out int value) ? value : 0;
    }

    private static void SetObjectArray(SerializedProperty property, UnityEngine.Object[] objects)
    {
        property.arraySize = objects.Length;
        for (int i = 0; i < objects.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        string name = path.Substring(path.LastIndexOf('/') + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
