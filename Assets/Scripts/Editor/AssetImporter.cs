using UnityEditor;
using UnityEngine;

namespace FarmingKitEditor
{
    public class AssetImporter : AssetPostprocessor
    {
        // Texture import override, intended for pixel art.

        private void OnPreprocessTexture()
        {
            var importer = assetImporter as TextureImporter;

            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
        }
    }
}
