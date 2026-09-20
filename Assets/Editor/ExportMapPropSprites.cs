#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports the authored map-decoration sheets into independent, lossless PNG
/// assets. Each output keeps its original pixel dimensions, so large trees stay
/// large and medium/small decorations retain their intended relative scale.
/// </summary>
public static class ExportMapPropSprites
{
    private const string VersionKey = "Meadom.MapProps.ExportedIndividualSprites";
    private const int Version = 2;

    private sealed class ExportSet
    {
        public string source;
        public string outputFolder;
        public string[] names;
    }

    private static readonly ExportSet[] Sets =
    {
        new ExportSet
        {
            source = "Assets/Sprites/props-items/trang trí map.png",
            outputFolder = "Assets/Sprites/props-items/trang trí map-tách riêng",
            names = new[]
            {
                "cay-nho-1", "cay-vua-1", "cay-lon-1", "cay-lon-2",
                "co-cao", "co-nho-1", "co-nho-2", "bui-cay"
            }
        },
        new ExportSet
        {
            source = "Assets/Sprites/props-items/ụ lúa.png",
            outputFolder = "Assets/Sprites/props-items/ụ lúa-tách riêng",
            names = new[] { "u-lua-vang", "u-lua-xanh" }
        },
        new ExportSet
        {
            source = "Assets/Sprites/cây trồng/cây lâu năm/banana/bananatree_200.png",
            outputFolder = "Assets/Sprites/cây trồng/cây lâu năm/banana/bananatree_200-tách riêng",
            names = new[]
            {
                "bananatree_200_0", "bananatree_200_1", "bananatree_200_2", "bananatree_200_3",
                "bananatree_200_4", "bananatree_200_5", "bananatree_200_6", "bananatree_200_7"
            }
        }
    };

    [InitializeOnLoadMethod]
    private static void ExportAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorPrefs.GetInt(VersionKey, 0) < Version)
                ExportAll();
        };
    }

    [MenuItem("Tools/Map/Export Map Props As Separate PNGs")]
    private static void ExportFromMenu()
    {
        ExportAll();
    }

    private static void ExportAll()
    {
        int exported = 0;
        foreach (ExportSet set in Sets)
            exported += Export(set);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (ExportSet set in Sets)
        {
            foreach (string name in set.names)
                ConfigureOutput($"{set.outputFolder}/{name}.png");
        }

        AssetDatabase.SaveAssets();
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log($"Exported {exported} map decoration sprites into individual PNG files.");
    }

    private static int Export(ExportSet set)
    {
        TextureImporter sourceImporter = AssetImporter.GetAtPath(set.source) as TextureImporter;
        if (sourceImporter == null)
            throw new InvalidOperationException($"Missing source sprite sheet: {set.source}");

        bool restoreReadable = !sourceImporter.isReadable;
        if (restoreReadable)
        {
            sourceImporter.isReadable = true;
            sourceImporter.SaveAndReimport();
        }

        try
        {
            List<Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(set.source)
                .OfType<Sprite>()
                .OrderBy(sprite => ParseTrailingNumber(sprite.name))
                .ToList();
            if (sprites.Count != set.names.Length)
                throw new InvalidOperationException(
                    $"{set.source} contains {sprites.Count} sprites, expected {set.names.Length}.");

            Directory.CreateDirectory(set.outputFolder);
            for (int i = 0; i < sprites.Count; i++)
            {
                Sprite sprite = sprites[i];
                Rect rect = sprite.rect;
                int width = Mathf.RoundToInt(rect.width);
                int height = Mathf.RoundToInt(rect.height);
                Color[] pixels = sprite.texture.GetPixels(
                    Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);

                Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
                output.name = set.names[i];
                output.filterMode = FilterMode.Point;
                output.SetPixels(pixels);
                output.Apply(false, false);
                File.WriteAllBytes($"{set.outputFolder}/{set.names[i]}.png", output.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(output);
            }
            return sprites.Count;
        }
        finally
        {
            if (restoreReadable)
            {
                sourceImporter.isReadable = false;
                sourceImporter.SaveAndReimport();
            }
        }
    }

    private static void ConfigureOutput(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePivot = new Vector2(0.5f, 0f);
        importer.SaveAndReimport();
    }

    private static int ParseTrailingNumber(string value)
    {
        int separator = value.LastIndexOf('_');
        return separator >= 0 && int.TryParse(value.Substring(separator + 1), out int number)
            ? number
            : int.MaxValue;
    }
}
#endif
