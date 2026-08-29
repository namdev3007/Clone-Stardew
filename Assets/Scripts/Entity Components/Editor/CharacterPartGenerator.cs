using Entity_Components.Character;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Entity_Components.Editor
{
    public enum ECategory { chest, legs, head, eye, hair, arms, none };
    public enum EDirection { side, front, back };

    [CreateAssetMenu]
    public class CharacterPartGenerator : ScriptableObject
    {
        public bool sliceTexture = true;
        public bool generateAssets = true;
        public bool overWriteData = false;
        public Vector2Int gridSize = new Vector2Int(16, 16);

        public SliceConfigurationData sliceConfigurationData;
        public List<SliceConfigurationData.Configuration> sliceConfigurations => sliceConfigurationData.configurations;

        public List<Texture2D> textures = new List<Texture2D>();

        public string assetName;
    }

    [CustomEditor(typeof(CharacterPartGenerator))]
    public class CharacterPartGeneratorEditor : UnityEditor.Editor
    {
        CharacterPartGenerator data => (CharacterPartGenerator)target;

        public void OnEnable()
        {
            EditorUtility.SetDirty(data);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.Space();

            data.assetName = EditorGUILayout.TextField("Asset Name", data.assetName);
            data.gridSize.x = EditorGUILayout.IntField("Grid Size X", data.gridSize.x);
            data.gridSize.y = EditorGUILayout.IntField("Grid Size Y", data.gridSize.y);

            data.sliceConfigurationData = EditorGUILayout.ObjectField("Slice Configuration", data.sliceConfigurationData, typeof(SliceConfigurationData), false) as SliceConfigurationData;

            EditorGUI.BeginChangeCheck();

            var serializedObject = new SerializedObject(data);
            var property = serializedObject.FindProperty("textures");
            serializedObject.Update();
            EditorGUILayout.PropertyField(property, true);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            data.overWriteData = EditorGUILayout.Toggle("Overwrite asset files", data.overWriteData);

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            data.sliceTexture = EditorGUILayout.Toggle("Slice Texture", data.sliceTexture);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            data.generateAssets = EditorGUILayout.Toggle("Generate Assets", data.generateAssets);

            EditorGUILayout.EndHorizontal();

            bool hasTexture = data.textures.Count != 0;
            bool hasBodyText = !string.IsNullOrEmpty(data.assetName);
            bool hasSliceConfiguration = data.sliceConfigurationData != null;

            if (!hasTexture)
                EditorGUILayout.HelpBox("Texture Sheet needs to be set.", MessageType.Error);

            if (!hasBodyText)
                EditorGUILayout.HelpBox("Sheet item name cannot be empty.", MessageType.Error);

            if (!hasSliceConfiguration)
                EditorGUILayout.HelpBox("A Slice Configuration needs to be set .", MessageType.Error);

            if (!hasTexture || !hasBodyText || !hasSliceConfiguration)
                GUI.enabled = false;

            if (GUILayout.Button("Process"))
            {
                if (data.sliceTexture)
                {
                    ProcessCharacterSheet();
                }
            }

            GUI.enabled = true;
        }

        private void ProcessCharacterSheet()
        {
            foreach (Texture2D texture in data.textures)
            {
                string path = AssetDatabase.GetAssetPath(texture);
                TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(path) as TextureImporter;
                importer.isReadable = true;
            
                SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
                factory.Init();
                ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
                dataProvider.InitSpriteEditorDataProvider();
            
                if (importer.spriteImportMode == SpriteImportMode.Multiple)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                }
                else
                {
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                }
            
                ISpriteNameFileIdDataProvider spriteNameFileIdDataProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                List<SpriteRect> spriteRects = new List<SpriteRect>();
                List<SpriteNameFileIdPair> nameFileIdPairs = new List<SpriteNameFileIdPair>();

                for (int i = 0; i < data.sliceConfigurations.Count; i++)
                {
                    for (int x = data.sliceConfigurations[i].startLocation.x; x < data.sliceConfigurations[i].Cells + data.sliceConfigurations[i].startLocation.x; x++)
                    {
                        int rectX = data.gridSize.x * x;
                        int rectY = texture.height - data.gridSize.y - data.gridSize.y * data.sliceConfigurations[i].startLocation.y;

                        string categoryName = Enum.GetName(typeof(ECategory), (int)data.sliceConfigurations[i].category);
                        string directionName = Enum.GetName(typeof(EDirection), (int)data.sliceConfigurations[i].direction);
                    
                        GUID spriteId = GUID.Generate();
                        string spriteName = $"{categoryName}_{directionName}{(x - data.sliceConfigurations[i].startLocation.x).ToString()}";
                    
                        spriteRects.Add(new SpriteRect()
                        {
                            alignment = SpriteAlignment.Custom,
                            name = $"{categoryName}_{directionName}{(x - data.sliceConfigurations[i].startLocation.x).ToString()}",
                            pivot = new Vector2(0.5f, 0.5f),
                            rect = new Rect(rectX, rectY, data.gridSize.x, data.gridSize.y),
                            spriteID = spriteId
                        });
                    
                        nameFileIdPairs.Add(new SpriteNameFileIdPair(spriteName, spriteId));
                    }
                }
            
                spriteNameFileIdDataProvider.SetNameFileIdPairs(nameFileIdPairs);
                dataProvider.SetSpriteRects(spriteRects.ToArray());
            
                dataProvider.Apply();
                importer.SaveAndReimport();

                if (data.generateAssets)
                {
                    GenerateAssets(path);
                }
            }
        }

        private bool HasPixels(Color[] colors)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i].a != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void GenerateAssets(string path)
        {
            string targetPath = AssetDatabase.GetAssetPath(data);

            targetPath = targetPath.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(data)), "");

            Dictionary<string, UnityEngine.Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<UnityEngine.Sprite>().ToDictionary(v => v.name, v => v);

            ECategory currentCatagory = ECategory.none;
            BodyData characterBodyData = ScriptableObject.CreateInstance<BodyData>();

            for (int i = 0; i < data.sliceConfigurations.Count; i++)
            {
                if (data.sliceConfigurations[i].category != currentCatagory)
                {
                    // Save current data, and create another once.
                    currentCatagory = data.sliceConfigurations[i].category;
                    characterBodyData = ScriptableObject.CreateInstance<BodyData>();

                    string categoryName = Enum.GetName(typeof(ECategory), (int)data.sliceConfigurations[i].category);
                    categoryName = char.ToUpper(categoryName[0]) + categoryName.Substring(1).ToLower();

                    string assetPathAndName;

                    if (!data.overWriteData)
                    {
                        assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(targetPath + $"{data.assetName}" + ".asset");
                    }
                    else
                    {
                        assetPathAndName = targetPath + $"{data.assetName}" + ".asset";
                    }

                    AssetDatabase.CreateAsset(characterBodyData, assetPathAndName);

                    EditorUtility.SetDirty(characterBodyData);
                }

                // Storage in SO is based on category, once it changes. There is a new SO

                for (int x = 0; x < data.sliceConfigurations[i].Cells; x++)
                {
                    string categoryName = Enum.GetName(typeof(ECategory), (int)data.sliceConfigurations[i].category);
                    string directionName = Enum.GetName(typeof(EDirection), (int)data.sliceConfigurations[i].direction);
                    string keyPath = $"{categoryName}_{directionName}{(x).ToString()}";

                    if (sprites.ContainsKey(keyPath))
                    {
                        UnityEngine.Sprite sprite;
                        sprites.TryGetValue(keyPath, out sprite);

                        switch (data.sliceConfigurations[i].direction)
                        {
                            case EDirection.side:
                                characterBodyData.side.Add(sprite);
                                break;
                            case EDirection.front:
                                characterBodyData.front.Add(sprite);
                                break;
                            case EDirection.back:
                                characterBodyData.back.Add(sprite);
                                break;
                            default:
                                break;
                        }

                    }
                }
            }
        }

    }
}