#if UNITY_EDITOR
using System.Linq;
using Main_Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Uses the unsuffixed Save Slot Displayer in StartMenu as the artist-edited template
/// and copies its remove button to the other scene rows and to the source prefab.
/// </summary>
public static class SetupSaveSlotRemoveButton
{
    private const string ScenePath = "Assets/MainScenes/StartMenu 1.unity";
    private const string PrefabPath = "Assets/Prefabs/User Interface/Start Menu/UI Save Slot Displayer.prefab";
    private const string ButtonName = "Button_RemoveSlot";
    private const int SetupVersion = 2;

    private static string VersionKey =>
        "Meadom.SaveSlotRemoveButton." + Application.dataPath.GetHashCode();

    [InitializeOnLoadMethod]
    private static void QueueSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorPrefs.GetInt(VersionKey, 0) >= SetupVersion)
                return;

            Sync(false);
        };
    }

    [MenuItem("Tools/UI/Sync Save Slot Remove Button")]
    private static void SyncFromMenu() => Sync(true);

    private static void Sync(bool selectSource)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedByTool = !scene.IsValid() || !scene.isLoaded;
        if (openedByTool)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            UISaveSlotDisplayer[] displayers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<UISaveSlotDisplayer>(true))
                .OrderBy(displayer => displayer.transform.GetSiblingIndex())
                .ToArray();

            if (displayers.Length < 2)
            {
                Debug.LogWarning("Save Slot remove button sync: at least two scene rows are required.");
                return;
            }

            // The explicitly edited source is "UI Save Slot Displayer". Rows with
            // suffixes such as "(1)" are the old copies that must be replaced.
            UISaveSlotDisplayer sourceDisplayer = displayers.FirstOrDefault(displayer =>
                displayer.name == "UI Save Slot Displayer");
            if (sourceDisplayer == null)
            {
                Debug.LogError("Save Slot remove button sync: UI Save Slot Displayer was not found.");
                return;
            }
            Button sourceButton = FindRemoveButton(sourceDisplayer.transform);
            if (sourceButton == null)
            {
                Debug.LogError("Save Slot remove button sync: the new Button_RemoveSlot was not found.");
                return;
            }

            foreach (UISaveSlotDisplayer target in displayers)
            {
                if (target == sourceDisplayer)
                {
                    AssignButtonReference(target, sourceButton);
                    continue;
                }

                ReplaceRemoveButton(target, sourceButton);
            }

            SyncPrefab(sourceButton);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            EditorPrefs.SetInt(VersionKey, SetupVersion);

            if (selectSource)
                Selection.activeGameObject = sourceButton.gameObject;

            Debug.Log("Save Slot remove button: UI Save Slot Displayer (no suffix) now replaces all suffixed rows and the prefab version.");
        }
        finally
        {
            if (openedByTool && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void SyncPrefab(Button sourceButton)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            UISaveSlotDisplayer prefabDisplayer = prefabRoot.GetComponent<UISaveSlotDisplayer>();
            if (prefabDisplayer == null)
            {
                Debug.LogError("Save Slot remove button sync: UISaveSlotDisplayer is missing on the prefab.");
                return;
            }

            ReplaceRemoveButton(prefabDisplayer, sourceButton);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ReplaceRemoveButton(UISaveSlotDisplayer target, Button sourceButton)
    {
        Button oldButton = FindRemoveButton(target.transform);
        if (oldButton == null || oldButton.transform.parent == null)
        {
            Debug.LogWarning($"Save Slot remove button sync: old button was not found below {target.name}.");
            return;
        }

        Transform parent = oldButton.transform.parent;
        int siblingIndex = oldButton.transform.GetSiblingIndex();

        GameObject replacement = Object.Instantiate(sourceButton.gameObject, parent, false);
        replacement.name = ButtonName;
        replacement.transform.SetSiblingIndex(siblingIndex);

        Object.DestroyImmediate(oldButton.gameObject);
        AssignButtonReference(target, replacement.GetComponent<Button>());
        EditorUtility.SetDirty(target);
    }

    private static void AssignButtonReference(UISaveSlotDisplayer displayer, Button button)
    {
        SerializedObject serialized = new SerializedObject(displayer);
        SerializedProperty property = serialized.FindProperty("buttonRequestRemove");
        property.objectReferenceValue = button;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Button FindRemoveButton(Transform root) =>
        root.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => button.name == ButtonName);
}
#endif
