#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using World.NPC;
using Event.Events;
using Item;
using TMPro;

public static class SetupNpcAssets
{
    private const string PrefabFolder = "Assets/Prefabs/World/NPC";
    private const string SeedSellerPrefab = PrefabFolder + "/NPC Seed Seller.prefab";
    private const string OldManPrefab = PrefabFolder + "/NPC Old Man.prefab";
    private const string SpriteFolder = "Assets/Sprites/npc";
    private const string FarmScenePath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string SeedSellerTestName = "NPC Seed Seller Test";
    private const string OldManTestName = "NPC Old Man Test";
    private const string NpcDataFolder = "Assets/ScriptableObjects/NPC";
    private const string DialogueDataFolder = NpcDataFolder + "/Dialogue";
    private const string DialogueFontSource = "Assets/fonts/Dùng cho text khác/dearpix-1.94 Ygygfu.ttf";
    private const string DialogueFontAsset = "Assets/fonts/Dùng cho text khác/dearpix-1.94 Ygygfu SDF.asset";
    private const string CoreScenePath = "Assets/MainScenes/Core 1.unity";
    private const string StatusFolder = SpriteFolder + "/npc-thoại stastus";

    [InitializeOnLoadMethod]
    private static void QueueSetup()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += () =>
        {
            Setup(false);
            EnsureNpcPrefabColliders();
            EnsureNpcDialogueSetup();
            SpawnTestNpcs(false);
        };
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += () =>
            {
                EnsureNpcPrefabColliders();
                EnsureNpcDialogueSetup();
                SpawnTestNpcs(false);
            };
    }

    [MenuItem("Tools/NPC/Setup NPC Assets")]
    public static void RunFromMenu()
    {
        Setup(true);
        EnsureNpcPrefabColliders();
        EnsureNpcDialogueSetup();
    }

    [MenuItem("Tools/NPC/Spawn Test NPCs By House")]
    public static void SpawnTestNpcsFromMenu()
    {
        SpawnTestNpcs(true);
    }

    private static void Setup(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(SeedSellerPrefab) != null &&
            AssetDatabase.LoadAssetAtPath<GameObject>(OldManPrefab) != null)
            return;

        EnsureFolder("Assets/Prefabs/World", "NPC");

        UnityEngine.Sprite ellipsis = LoadFirst(StatusFolder + "/npc-thoại ba chấm.png");
        UnityEngine.Sprite question = LoadFirst(StatusFolder + "/npc-thoại chấm hỏi.png");
        UnityEngine.Sprite exclamation = LoadFirst(StatusFolder + "/npc-thoại chấm than.png");

        CreateNpc("NPC Seed Seller", SeedSellerPrefab,
            LoadSprites(SpriteFolder + "/npc-banhatgiong-Sheet.png"), ellipsis, question, exclamation);
        CreateNpc("NPC Old Man", OldManPrefab,
            LoadSprites(SpriteFolder + "/ong-character-Sheet.png"), ellipsis, question, exclamation);

        AssetDatabase.SaveAssets();
        Debug.Log("NPC assets prepared. No NPC was added to a scene.");
    }

    private static void SpawnTestNpcs(bool selectSpawnedNpc)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        GameObject seedSellerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SeedSellerPrefab);
        GameObject oldManPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OldManPrefab);
        if (seedSellerPrefab == null || oldManPrefab == null)
        {
            Setup(true);
            seedSellerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SeedSellerPrefab);
            oldManPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OldManPrefab);
        }

        Scene farmScene = SceneManager.GetSceneByPath(FarmScenePath);
        bool openedForSetup = !farmScene.IsValid() || !farmScene.isLoaded;
        if (openedForSetup)
            farmScene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Additive);

        try
        {
            Transform house = FindTransform(farmScene, "House");
            if (house == null)
            {
                Debug.LogError($"Cannot spawn test NPCs: House was not found in {FarmScenePath}.");
                return;
            }

            GameObject seedSeller = EnsureNpcInstance(
                farmScene, house, seedSellerPrefab, SeedSellerTestName, new Vector3(-0.48f, -0.4f, 0f));
            GameObject oldMan = EnsureNpcInstance(
                farmScene, house, oldManPrefab, OldManTestName, new Vector3(0.48f, -0.4f, 0f));

            EditorSceneManager.MarkSceneDirty(farmScene);
            EditorSceneManager.SaveScene(farmScene);

            if (selectSpawnedNpc)
                Selection.activeGameObject = seedSeller != null ? seedSeller : oldMan;

            Debug.Log("Spawned the seed seller and old man beside the farmhouse for testing.");
        }
        finally
        {
            if (openedForSetup && farmScene.IsValid() && farmScene.isLoaded)
                EditorSceneManager.CloseScene(farmScene, true);
        }
    }

    private static GameObject EnsureNpcInstance(Scene scene, Transform house, GameObject prefab,
        string instanceName, Vector3 offsetFromHouse)
    {
        Transform existing = FindTransform(scene, instanceName);
        GameObject instance = existing != null
            ? existing.gameObject
            : PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;

        if (instance == null)
            return null;

        instance.name = instanceName;
        instance.transform.SetParent(house.parent, true);
        Vector3 position = house.position + offsetFromHouse;
        position.z = house.position.z;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static Transform FindTransform(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = FindTransform(root.transform, objectName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static Transform FindTransform(Transform current, string objectName)
    {
        if (current.name == objectName)
            return current;

        foreach (Transform child in current)
        {
            Transform match = FindTransform(child, objectName);
            if (match != null)
                return match;
        }

        return null;
    }

    [MenuItem("Tools/NPC/Setup English Dialogue")]
    public static void EnsureNpcDialogueSetupFromMenu()
    {
        EnsureNpcDialogueSetup();
    }

    private static void EnsureNpcDialogueSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureAssetFolder(NpcDataFolder);
        EnsureAssetFolder(DialogueDataFolder);

        DialogueVisualLibrary visuals = CreateOrLoad<DialogueVisualLibrary>(NpcDataFolder + "/Dialogue Visual Library.asset");
        visuals.npcDialogueBox = LoadFirst(SpriteFolder + "/dialogue-box/dialogue-box-npcs-Sheet.png");
        visuals.playerDialogueBox = LoadFirst(SpriteFolder + "/dialogue-box/dialogue-box-player-Sheet.png");
        visuals.namePlate = LoadFirst(SpriteFolder + "/dialogue-box/dialogue-box-bảng tên.png");
        visuals.continueIcon = LoadFirst(SpriteFolder + "/dialogue-box/dialogue-box-icon.png");
        visuals.skipButton = LoadFirst("Assets/Sprites/Buttons/tieng-anh/skip.png");
        visuals.textFont = CreateOrLoadTmpFont(DialogueFontSource, DialogueFontAsset);
        visuals.playerPortrait = LoadFirst(SpriteFolder + "/nhân vật chi tiết/nhân vật player_800.png");
        visuals.grandpaPortrait = LoadFirst(SpriteFolder + "/nhân vật chi tiết/nhân vật người ông_800.png");
        visuals.sellerPortrait = LoadFirst(SpriteFolder + "/nhân vật chi tiết/nhân vật người bán_800.png");
        EditorUtility.SetDirty(visuals);

        DialogueSequence grandpaIntro = SaveSequence("Grandpa Intro", "grandpa.intro", new[]
        {
            L(DialogueSequence.Speaker.Npc, "grandpa.intro.001", "There you are. The sun's already over the treetops. City life taught you to sleep this late, did it?"),
            L(DialogueSequence.Speaker.Player, "grandpa.intro.002", "Yeah... I haven't been sleeping well lately. I guess I finally caught up."),
            L(DialogueSequence.Speaker.Npc, "grandpa.intro.003", "That country breeze will do it. Still, daylight's wasting, and I've got a small job for you."),
            L(DialogueSequence.Speaker.Npc, "grandpa.intro.004", "Here. Take this hat, this hoe, and a packet of seeds. Keep the hat on—the sun out here is harsher than you remember."),
            L(DialogueSequence.Speaker.Player, "grandpa.intro.005", "You want me to start right now?"),
            L(DialogueSequence.Speaker.Npc, "grandpa.intro.006", "Would you rather wait until noon and roast out there?"),
            L(DialogueSequence.Speaker.Npc, "grandpa.intro.007", "The weeds have taken over the plot beside the house. Clear a little ground and loosen the soil for me."),
            L(DialogueSequence.Speaker.Npc, "grandpa.intro.008", "Plant the blade firmly, then pull it back toward you. Don't overthink it. Try three patches first.")
        });

        DialogueSequence grandpaReminder = SaveSequence("Grandpa Reminder", "grandpa.reminder", new[]
        {
            L(DialogueSequence.Speaker.Npc, "grandpa.reminder.001", "Take things one step at a time. The plot beside the house is a good place to begin."),
            L(DialogueSequence.Speaker.Npc, "grandpa.reminder.002", "Three patches will do for now. Mind where you swing that hoe.")
        });

        DialogueSequence grandpaCompletion = SaveSequence("Grandpa After Hoe", "grandpa.after_hoe", new[]
        {
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.001", "Not bad. Your hands still remember more than you think."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.002", "Now tell me the truth. What happened in the city that sent you home without a word?"),
            L(DialogueSequence.Speaker.Player, "grandpa.after_hoe.003", "Nothing happened. Work slowed down, so I took a few days off. I just thought I'd visit."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.004", "You might fool someone else with that, but not me."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.005", "I've seen the shadows under your eyes. I've heard you sighing since morning."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.006", "Were they working you to the bone again? Or pushing you around while paying you next to nothing?"),
            L(DialogueSequence.Speaker.Player, "grandpa.after_hoe.007", "..."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.008", "When I was your age, I thought strong shoulders were enough to carry anything."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.009", "I joined the youth volunteers, worked at the cooperative, and tried to prove I could handle every burden put in front of me."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.010", "But life doesn't care how stubborn you are."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.011", "You young people are always racing somewhere—trying to get ahead, trying to prove yourselves."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.012", "Then something knocks you down, and pride makes you carry the hurt alone."),
            L(DialogueSequence.Speaker.Player, "grandpa.after_hoe.013", "I just feel like I've fallen behind."),
            L(DialogueSequence.Speaker.Player, "grandpa.after_hoe.014", "My friends are getting promoted. They're buying homes and cars, and I'm... back where I started."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.015", "Stop measuring your life against theirs."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.016", "Different people start in different places. They move at different speeds."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.017", "If you try to leap straight to the top, all you'll earn is a harder fall."),
            L(DialogueSequence.Speaker.Player, "grandpa.after_hoe.018", "So what am I supposed to do?"),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.019", "Look at this soil. You don't throw down a seed and demand a harvest the next morning."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.020", "First, you prepare a place where something can grow. Then you plant. After that, you tend it a little at a time."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.021", "People aren't so different."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.022", "You're exhausted, and your thoughts are tangled. Give yourself room to breathe before deciding where to run next."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.023", "Slow doesn't mean you've failed. It means you're still growing."),
            L(DialogueSequence.Speaker.Player, "grandpa.after_hoe.024", "I think I understand."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.025", "Good. For now, eat properly, get some sleep, and work the land at your own pace."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.026", "When you need seeds or proper tools, talk to Uncle Hai. He's the round fellow nearby—you won't miss him."),
            L(DialogueSequence.Speaker.Npc, "grandpa.after_hoe.027", "And keep an eye around the garden. I left my watering can somewhere out there."),
            L(DialogueSequence.Speaker.Player, "grandpa.after_hoe.028", "Alright, Grandpa. I'll remember.")
        });

        DialogueSequence grandpaAdvice = SaveSequence("Grandpa Farming Advice", "grandpa.farming_advice", new[]
        {
            L(DialogueSequence.Speaker.Npc, "grandpa.farming_advice.001", "Prepare the soil, plant your seeds, and give them time."),
            L(DialogueSequence.Speaker.Npc, "grandpa.farming_advice.002", "Good care helps, but patience does most of the work."),
            L(DialogueSequence.Speaker.Player, "grandpa.farming_advice.003", "Got it, Grandpa.")
        });

        DialogueSequence expansionUnavailable = SaveSequence("Farm Expansion Unavailable", "grandpa.expand_unavailable", new[]
        {
            L(DialogueSequence.Speaker.Npc, "grandpa.expand_unavailable.001", "Let's get the first plot in shape before we clear any more land."),
            L(DialogueSequence.Speaker.Npc, "grandpa.expand_unavailable.002", "Come back when the village is ready to handle land permits and payments again.")
        });

        DialogueSequence sellerIntro = SaveSequence("Uncle Hai Intro", "hai.intro", new[]
        {
            L(DialogueSequence.Speaker.Player, "hai.intro.001", "Hello, Uncle. I'm Old Man Tam's grandkid from down the road. I came back yesterday."),
            L(DialogueSequence.Speaker.Npc, "hai.intro.002", "Old Tam's grandkid? Well, look at you!"),
            L(DialogueSequence.Speaker.Npc, "hai.intro.003", "You came home yesterday and only now decided to say hello?"),
            L(DialogueSequence.Speaker.Npc, "hai.intro.004", "Fair skin, clean hands—you look every bit like a city boy."),
            L(DialogueSequence.Speaker.Npc, "hai.intro.005", "So, what brings you here? Wine for your grandpa? Something sweet for yourself?"),
            L(DialogueSequence.Speaker.Player, "hai.intro.006", "Neither. Grandpa sent me to buy seeds and a few farming tools."),
            L(DialogueSequence.Speaker.Npc, "hai.intro.007", "You're taking up farming? Good!"),
            L(DialogueSequence.Speaker.Npc, "hai.intro.008", "It's nice to see a young person willing to get some dirt under their nails."),
            L(DialogueSequence.Speaker.Npc, "hai.intro.009", "Better than staring at a phone all day until your brain turns to porridge."),
            L(DialogueSequence.Speaker.Npc, "hai.intro.010", "I carry seeds, tools, and whatever else a small farm needs. Take a look.")
        });

        DialogueSequence[] sellerRandom = new[]
        {
            SaveSequence("Uncle Hai Revisit 1", "hai.revisit.001", new[] { L(DialogueSequence.Speaker.Npc, "hai.revisit.001.001", "Back for more seeds, kid? Or are you finally ready to stock up on proper tools?") }),
            SaveSequence("Uncle Hai Revisit 2", "hai.revisit.002", new[] { L(DialogueSequence.Speaker.Npc, "hai.revisit.002.001", "How's that hoe treating you? Still smooth, or have you already managed to bend it?") }),
            SaveSequence("Uncle Hai Revisit 3", "hai.revisit.003", new[] { L(DialogueSequence.Speaker.Npc, "hai.revisit.003.001", "Give me a moment to trim this branch. If you know what you need, go ahead and take a look.") }),
            SaveSequence("Uncle Hai Revisit 4", "hai.revisit.004", new[] { L(DialogueSequence.Speaker.Npc, "hai.revisit.004.001", "Your grandpa never bought one seed when he could bargain for three. Don't let him teach you that habit.") }),
            SaveSequence("Uncle Hai Revisit 5", "hai.revisit.005", new[] { L(DialogueSequence.Speaker.Npc, "hai.revisit.005.001", "Farming's simple until the weather decides it has other plans.") })
        };

        NpcShopCatalog shopCatalog = CreateOrLoad<NpcShopCatalog>(NpcDataFolder + "/Uncle Hai Shop Catalog.asset");
        shopCatalog.Configure(LoadShopItems());
        EditorUtility.SetDirty(shopCatalog);

        InteractionEvent hoverEvent = AssetDatabase.FindAssets("t:InteractionEvent")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<InteractionEvent>)
            .FirstOrDefault();
        ItemData hoe = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/ScriptableObjects/Items/Tools/Item_Tool_Shovel.asset");
        ItemData seeds = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/ScriptableObjects/Items/Crops/Item_Seed_Carrot.asset");

        ConfigureNpcDialoguePrefab(OldManPrefab, NpcDialogueInteractor.NpcRole.Grandpa, "Grandpa",
            visuals.grandpaPortrait, visuals, grandpaIntro, grandpaReminder, grandpaCompletion,
            grandpaAdvice, expansionUnavailable, Array.Empty<DialogueSequence>(), null, hoe, seeds, hoverEvent);
        ConfigureNpcDialoguePrefab(SeedSellerPrefab, NpcDialogueInteractor.NpcRole.SeedSeller, "Uncle Hai",
            visuals.sellerPortrait, visuals, sellerIntro, null, null, null, null,
            sellerRandom, shopCatalog, null, null, hoverEvent);

        EnsureDialogueUiInCoreScene(visuals, false);
        AssetDatabase.SaveAssets();
        Debug.Log("English NPC dialogue, tutorial and shop catalog are ready.");
    }

    [MenuItem("Tools/NPC/Create Dialogue UI In Core 1")]
    public static void CreateDialogueUiInCoreSceneFromMenu()
    {
        DialogueVisualLibrary visuals = AssetDatabase.LoadAssetAtPath<DialogueVisualLibrary>(
            NpcDataFolder + "/Dialogue Visual Library.asset");
        if (visuals == null)
        {
            EnsureNpcDialogueSetup();
            visuals = AssetDatabase.LoadAssetAtPath<DialogueVisualLibrary>(
                NpcDataFolder + "/Dialogue Visual Library.asset");
        }
        EnsureDialogueUiInCoreScene(visuals, true);
    }

    private static void EnsureDialogueUiInCoreScene(DialogueVisualLibrary visuals, bool selectObject)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || visuals == null)
            return;

        Scene coreScene = SceneManager.GetSceneByPath(CoreScenePath);
        bool openedForSetup = !coreScene.IsValid() || !coreScene.isLoaded;
        if (openedForSetup)
            coreScene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Additive);

        try
        {
            Transform existing = FindTransform(coreScene, "Dialogue UI");
            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                root = new GameObject("Dialogue UI");
                SceneManager.MoveGameObjectToScene(root, coreScene);
            }

            DialogueUIController controller = root.GetComponent<DialogueUIController>();
            if (controller == null)
                controller = root.AddComponent<DialogueUIController>();
            controller.BuildForEditor(visuals);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(coreScene);
            EditorSceneManager.SaveScene(coreScene);

            if (selectObject && !openedForSetup)
                Selection.activeGameObject = root;
        }
        finally
        {
            if (openedForSetup && coreScene.IsValid() && coreScene.isLoaded)
                EditorSceneManager.CloseScene(coreScene, true);
        }
    }

    private static void ConfigureNpcDialoguePrefab(string prefabPath, NpcDialogueInteractor.NpcRole role,
        string displayName, UnityEngine.Sprite portrait, DialogueVisualLibrary visuals, DialogueSequence intro,
        DialogueSequence reminder, DialogueSequence completion, DialogueSequence advice,
        DialogueSequence expansionUnavailable, DialogueSequence[] revisit, NpcShopCatalog catalog,
        ItemData hoe, ItemData seeds, InteractionEvent hoverEvent)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            NpcDialogueInteractor interactor = root.GetComponent<NpcDialogueInteractor>();
            if (interactor == null)
                interactor = root.AddComponent<NpcDialogueInteractor>();
            interactor.Configure(role, displayName, portrait, visuals, intro, reminder, completion, advice,
                expansionUnavailable, revisit, catalog, hoe, seeds, hoverEvent);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static DialogueSequence SaveSequence(string assetName, string id, DialogueSequence.Line[] lines)
    {
        DialogueSequence sequence = CreateOrLoad<DialogueSequence>(DialogueDataFolder + "/" + assetName + ".asset");
        sequence.Configure(id, lines);
        EditorUtility.SetDirty(sequence);
        return sequence;
    }

    private static DialogueSequence.Line L(DialogueSequence.Speaker speaker, string key, string english)
    {
        return new DialogueSequence.Line
        {
            speaker = speaker,
            localizationKey = key,
            english = english,
            vietnamese = string.Empty
        };
    }

    private static ItemData[] LoadShopItems()
    {
        return AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/ScriptableObjects/Items" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.Contains("/Item_Seed_") ||
                (path.Contains("/Tools/") && !path.EndsWith("Item_Gold.asset") && !path.EndsWith("Item_Fertilizer.asset")))
            .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
            .Where(item => item != null)
            .OrderBy(item => item.ItemName)
            .ToArray();
    }

    private static T CreateOrLoad<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static TMP_FontAsset CreateOrLoadTmpFont(string sourcePath, string assetPath)
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fontAsset != null)
            return fontAsset;

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (sourceFont == null)
        {
            Debug.LogError("Dialogue font source was not found at " + sourcePath);
            return null;
        }

        fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
        fontAsset.name = "dearpix-1.94 Ygygfu SDF";
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        AssetDatabase.CreateAsset(fontAsset, assetPath);
        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        if (fontAsset.atlasTexture != null && !AssetDatabase.Contains(fontAsset.atlasTexture))
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void CreateNpc(string npcName, string prefabPath, UnityEngine.Sprite[] frames,
        UnityEngine.Sprite ellipsis, UnityEngine.Sprite question, UnityEngine.Sprite exclamation)
    {
        if (frames.Length == 0)
            throw new InvalidOperationException($"No animation frames found for {npcName}.");

        GameObject root = new GameObject(npcName);
        try
        {
            SpriteRenderer body = root.AddComponent<SpriteRenderer>();
            body.sprite = frames[0];
            body.sortingOrder = 0;

            BoxCollider2D bodyCollider = root.AddComponent<BoxCollider2D>();
            ConfigureNpcCollider(bodyCollider, frames[0]);

            NpcSpriteAnimator animator = root.AddComponent<NpcSpriteAnimator>();
            animator.Configure(body, frames, 6f);

            GameObject statusObject = new GameObject("Dialogue Status");
            statusObject.transform.SetParent(root.transform, false);
            statusObject.transform.localPosition = new Vector3(0f, 0.29f, -0.01f);
            SpriteRenderer statusRenderer = statusObject.AddComponent<SpriteRenderer>();
            statusRenderer.sortingLayerID = body.sortingLayerID;
            statusRenderer.sortingOrder = body.sortingOrder + 1;
            statusRenderer.enabled = false;

            NpcDialogueStatus dialogueStatus = root.AddComponent<NpcDialogueStatus>();
            dialogueStatus.Configure(statusRenderer, ellipsis, question, exclamation);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void EnsureNpcPrefabColliders()
    {
        EnsureNpcPrefabCollider(SeedSellerPrefab);
        EnsureNpcPrefabCollider(OldManPrefab);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureNpcPrefabCollider(string prefabPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
            return;

        try
        {
            SpriteRenderer body = prefabRoot.GetComponent<SpriteRenderer>();
            if (body == null || body.sprite == null)
                return;

            BoxCollider2D bodyCollider = prefabRoot.GetComponent<BoxCollider2D>();
            if (bodyCollider == null)
                bodyCollider = prefabRoot.AddComponent<BoxCollider2D>();

            ConfigureNpcCollider(bodyCollider, body.sprite);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureNpcCollider(BoxCollider2D bodyCollider, UnityEngine.Sprite sprite)
    {
        Bounds bounds = sprite.bounds;
        float colliderWidth = bounds.size.x * 0.55f;
        float colliderHeight = bounds.size.y * 0.28f;

        bodyCollider.isTrigger = false;
        bodyCollider.size = new Vector2(colliderWidth, colliderHeight);
        bodyCollider.offset = new Vector2(
            bounds.center.x,
            bounds.min.y + colliderHeight * 0.5f + bounds.size.y * 0.04f);
    }

    private static UnityEngine.Sprite[] LoadSprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<UnityEngine.Sprite>()
            .OrderBy(sprite => NumericSuffix(sprite.name))
            .ToArray();
    }

    private static UnityEngine.Sprite LoadFirst(string path)
    {
        return LoadSprites(path).FirstOrDefault();
    }

    private static int NumericSuffix(string name)
    {
        int separator = name.LastIndexOf('_');
        return separator >= 0 && int.TryParse(name.Substring(separator + 1), out int value) ? value : 0;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
