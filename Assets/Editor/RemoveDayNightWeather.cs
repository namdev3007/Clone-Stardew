#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using User_Interface;

[InitializeOnLoad]
public static class RemoveDayNightWeather
{
    private const string CoreScenePath = "Assets/MainScenes/Core 1.unity";
    private const string FarmScenePath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/World/Player.prefab";
    private const string ClockFontPath = "Assets/fonts/Dùng cho text khác/dearpix-1.94 Ygygfu SDF.asset";

    static RemoveDayNightWeather()
    {
        EditorApplication.delayCall += RunOnceAfterCompile;
    }

    [MenuItem("Tools/Game/Remove Day-Night And Weather")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before removing day/night and weather.");
            return;
        }

        try
        {
            CleanPlayerPrefab();
            CleanFarmScene();
            CleanCoreScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Day/night and weather removed. Simple HH:MM:SS clock installed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void RunOnceAfterCompile()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && NeedsCleanup())
            Apply();
    }

    private static bool NeedsCleanup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == CoreScenePath)
        {
            return FindNamed(activeScene, "Simple Game Clock") == null ||
                   FindNamed(activeScene, "Weather System") != null ||
                   FindNamed(activeScene, "Weather Effect Rain") != null ||
                   FindNamed(activeScene, "Time and Gold Indicator") != null;
        }

        // Run once when the scripts compile even if another scene is currently open.
        return !SessionState.GetBool("Meadom.SimpleClockCleanupChecked", false);
    }

    private static void CleanCoreScene()
    {
        Scene scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Single);

        DestroyNamed(scene, "Weather System");
        DestroyNamed(scene, "Weather Effect Rain");
        DestroyNamed(scene, "Time and Gold Indicator");

        GameObject timeSystem = FindNamed(scene, "Time System");
        if (timeSystem != null)
        {
            timeSystem.name = "Simple Game Clock System";
            EditorUtility.SetDirty(timeSystem);
        }

        if (FindNamed(scene, "Simple Game Clock") == null)
            CreateSimpleClock();

        RefreshSaveableCaches(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SessionState.SetBool("Meadom.SimpleClockCleanupChecked", true);
    }

    private static void CleanPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform timeLight = FindChild(root.transform, "TimeBasedLight (1)");
            if (timeLight != null)
            {
                UnityEngine.Object.DestroyImmediate(timeLight.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CleanFarmScene()
    {
        Scene scene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Single);
        bool changed = false;

        GameObject daylight = FindNamed(scene, "TimeBasedLight");
        if (daylight != null)
        {
            Component listener = FindComponentByFullName(daylight, "Event.Listeners.TimeEventListener");
            if (listener != null)
            {
                UnityEngine.Object.DestroyImmediate(listener);
                changed = true;
            }

            if (daylight.name != "Static Daylight")
            {
                daylight.name = "Static Daylight";
                changed = true;
            }

            Light light = daylight.GetComponent<Light>();
            if (light != null)
            {
                light.enabled = true;
                light.color = Color.white;
                light.intensity = 1f;
                EditorUtility.SetDirty(light);
                changed = true;
            }
        }

        foreach (GameObject gameObject in EnumerateSceneObjects(scene))
        {
            Component weatherDirtSetter = FindComponentByFullName(gameObject, "Weather.WeatherDirtTileSetter");
            if (weatherDirtSetter == null)
                continue;

            UnityEngine.Object.DestroyImmediate(weatherDirtSetter);
            changed = true;
        }

        RefreshSaveableCaches(scene);
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void CreateSimpleClock()
    {
        GameObject canvasObject = new GameObject(
            "Simple Game Clock",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textObject = new GameObject("Text_Time", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = canvasObject.layer;
        textObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-24f, -20f);
        rect.sizeDelta = new Vector2(260f, 54f);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = "00:00:00";
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ClockFontPath);
        label.fontSize = 30f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.TopRight;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableWordWrapping = false;

        SimpleGameClockUI clockUI = canvasObject.AddComponent<SimpleGameClockUI>();
        SerializedObject serializedClock = new SerializedObject(clockUI);
        serializedClock.FindProperty("label").objectReferenceValue = label;
        serializedClock.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void DestroyNamed(Scene scene, string objectName)
    {
        GameObject gameObject = FindNamed(scene, objectName);
        if (gameObject != null)
            UnityEngine.Object.DestroyImmediate(gameObject);
    }

    private static GameObject FindNamed(Scene scene, string objectName)
    {
        foreach (GameObject gameObject in EnumerateSceneObjects(scene))
        {
            if (gameObject.name == objectName)
                return gameObject;
        }

        return null;
    }

    private static IEnumerable<GameObject> EnumerateSceneObjects(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                yield return transform.gameObject;
        }
    }

    private static Transform FindChild(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private static Component FindComponentByFullName(GameObject gameObject, string typeName)
    {
        foreach (Component component in gameObject.GetComponents<Component>())
        {
            if (component != null && component.GetType().FullName == typeName)
                return component;
        }

        return null;
    }

    private static void RefreshSaveableCaches(Scene scene)
    {
        foreach (GameObject gameObject in EnumerateSceneObjects(scene))
        {
            foreach (MonoBehaviour component in gameObject.GetComponents<MonoBehaviour>())
            {
                if (component == null || component.GetType().FullName != "Plugins.Lowscope.ComponentSaveSystem.Components.Saveable")
                    continue;

                MethodInfo onValidate = component.GetType().GetMethod(
                    "OnValidate",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                onValidate?.Invoke(component, null);
                EditorUtility.SetDirty(component);
            }
        }
    }
}
#endif
