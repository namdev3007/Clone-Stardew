#if UNITY_EDITOR
using System;
using System.Linq;
using Entity_Components.Character;
using UnityEditor;
using UnityEngine;

public static class SetupFullBodyPlayer
{
    private const string PrefabPath = "Assets/Prefabs/World/Player.prefab";
    private const string SpriteRoot = "Assets/Sprites/Player/";

    [InitializeOnLoadMethod]
    private static void RunWhenScriptsReload()
    {
        EditorApplication.delayCall += () =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                RemoveLegacyVisuals();
                Run();
            }
        };
    }

    private static void RemoveLegacyVisuals()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        bool changed = false;
        try
        {
            Transform body = Find(root.transform, "CharacterVisualization_Body");
            if (body != null)
            {
                for (int i = body.childCount - 1; i >= 0; i--)
                {
                    Transform child = body.GetChild(i);
                    if (child.name == "FullBody Player Sprite") continue;
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    changed = true;
                }
            }

            Transform clothes = Find(root.transform, "CharacterVisualization_Clothes");
            if (clothes != null)
            {
                UnityEngine.Object.DestroyImmediate(clothes.gameObject);
                changed = true;
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (changed)
            AssetDatabase.SaveAssets();
    }

    public static void Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform body = Find(root.transform, "CharacterVisualization_Body");
            if (body == null) throw new Exception("CharacterVisualization_Body was not found.");

            Transform display = body.Find("FullBody Player Sprite");
            if (display == null)
            {
                display = new GameObject("FullBody Player Sprite").transform;
                display.SetParent(body, false);
            }

            SpriteRenderer renderer = display.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = display.gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;

            FullBodyPlayerSpriteAnimator skin = display.GetComponent<FullBodyPlayerSpriteAnimator>();
            if (skin == null) skin = display.gameObject.AddComponent<FullBodyPlayerSpriteAnimator>();

            SerializedObject so = new SerializedObject(skin);
            so.FindProperty("target").objectReferenceValue = renderer;
            Assign(so, "idle", "idel/idel-1-bên dưới.png", "idel/idel-2-bên phải.png", "idel/idel-3-bên trên.png");
            Assign(so, "walk", "walk/walk-1-bên dưới.png", "walk/walk-2-bên phải.png", "walk/walk-2-bên trên.png");
            Assign(so, "chop", "chat-cay/chatcay-1-góc bên dưới.png", "chat-cay/chatcay-2-góc bên phải.png", "chat-cay/chatcay-3-góc bên trên.png");
            Assign(so, "hoe", "cuoc-dat/cuocdat-1-bên dưới.png", "cuoc-dat/cuocdat-2-bên phải.png", "cuoc-dat/cuocdat-3-bên trên.png");
            Assign(so, "water", "tuoi-nuoc/tuoi-cay-1-bên dưới.png", "tuoi-nuoc/tuoi-cay-2-bên phải.png", "tuoi-nuoc/tuoi-cay-3-bên trên.png");
            Assign(so, "harvest", "thu-hoach/thu-hoach-1-bên dưới.png", "thu-hoach/thu-hoach-2-bên phải.png", "thu-hoach/thu-hoach-3-bên trên.png");
            so.ApplyModifiedPropertiesWithoutUndo();

            renderer.sprite = LoadSprites(SpriteRoot + "idel/idel-1-bên dưới.png").FirstOrDefault();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("Full-body player Sprite setup completed for Core 1.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

    private static void Assign(SerializedObject so, string property, string down, string right, string up)
    {
        SerializedProperty set = so.FindProperty(property);
        SetArray(set.FindPropertyRelative("down"), LoadSprites(SpriteRoot + down));
        SetArray(set.FindPropertyRelative("right"), LoadSprites(SpriteRoot + right));
        SetArray(set.FindPropertyRelative("up"), LoadSprites(SpriteRoot + up));
    }

    private static UnityEngine.Sprite[] LoadSprites(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<UnityEngine.Sprite>().OrderBy(s => NumericSuffix(s.name)).ToArray();

    private static int NumericSuffix(string name)
    {
        int underscore = name.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out int n) ? n : 0;
    }

    private static void SetArray(SerializedProperty property, UnityEngine.Sprite[] sprites)
    {
        property.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    private static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
