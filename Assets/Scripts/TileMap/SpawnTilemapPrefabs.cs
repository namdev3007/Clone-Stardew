using Plugins.Lowscope.ComponentSaveSystem.Interfaces;
using Referencing;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TileMap
{
    public class SpawnTilemapPrefabs : MonoBehaviour, ISaveable
    {
        [SerializeField]
        private Tilemap targetTileMap = null;

        [System.Serializable]
        public class SpawnPrefab
        {
            public SaveablePrefab prefab;
            public int amount;
        }

        [SerializeField]
        private SpawnPrefab[] prefabs;

        [SerializeField]
        private Vector2 placementOffset;

        [SerializeField]
        private Tilemap[] dontAllowOverlapTileMaps;

        private bool hasPlacedPrefabs = false;

        private void Start()
        {
            TrimExcessSpawnedPrefabs();

            if (hasPlacedPrefabs)
                return;

            List<Vector3> tileWorldLocations = new List<Vector3>();

            foreach (var pos in targetTileMap.cellBounds.allPositionsWithin)
            {
                Vector3Int localPlace = new Vector3Int(pos.x, pos.y, pos.z);
                Vector3 place = targetTileMap.CellToWorld(localPlace) + (Vector3)placementOffset;

                if (targetTileMap.HasTile(localPlace))
                {
                    tileWorldLocations.Add(place);
                }
            }

            foreach (SpawnPrefab spawnConfig in prefabs)
            {
                HashSet<Vector3> selectLocations = new HashSet<Vector3>();

                for (int i = 0; i < spawnConfig.amount; i++)
                {
                    if (tileWorldLocations.Count <= 0)
                    {
                        Debug.Log("No more locations to spawn object.");
                        break;
                    }

                    int randomLocationIndex = Random.Range(0, tileWorldLocations.Count);
                    Vector3Int location = targetTileMap.WorldToCell(tileWorldLocations[randomLocationIndex]);

                    bool overlapFound = false;

                    for (int i2 = 0; i2 < dontAllowOverlapTileMaps.Length; i2++)
                    {
                        if (dontAllowOverlapTileMaps[i2].HasTile(location))
                        {
                            tileWorldLocations.RemoveAt(randomLocationIndex);
                            i--;
                            overlapFound = true;
                            break;
                        }
                    }

                    if (overlapFound)
                        continue;

                    if (selectLocations.Add(tileWorldLocations[randomLocationIndex]))
                    {
                        tileWorldLocations.RemoveAt(randomLocationIndex);
                    }
                }

                foreach (Vector3 location in selectLocations)
                {
                    GameObject spawnedGameObject = spawnConfig.prefab.Retrieve<GameObject>(scene: this.gameObject.scene);

                    if (spawnedGameObject == null)
                        continue;

                    spawnedGameObject.transform.position = location;
                }
            }

            hasPlacedPrefabs = true;
        }

        private void TrimExcessSpawnedPrefabs()
        {
            GameObject[] sceneRoots = gameObject.scene.GetRootGameObjects();

            foreach (SpawnPrefab spawnConfig in prefabs)
            {
                GameObject sourcePrefab = spawnConfig.prefab?.GetPrefab();
                if (sourcePrefab == null)
                {
                    continue;
                }

                string cloneName = sourcePrefab.name + "(Clone)";
                int keptCount = 0;

                foreach (GameObject sceneRoot in sceneRoots)
                {
                    Transform[] sceneObjects = sceneRoot.GetComponentsInChildren<Transform>(true);

                    foreach (Transform sceneObject in sceneObjects)
                    {
                        if (sceneObject.gameObject == gameObject || sceneObject.name != cloneName)
                        {
                            continue;
                        }

                        if (keptCount < spawnConfig.amount)
                        {
                            keptCount++;
                            continue;
                        }

                        Destroy(sceneObject.gameObject);
                    }
                }
            }
        }

        public string OnSave()
        {
            return hasPlacedPrefabs.ToString();
        }

        public void OnLoad(string data)
        {
            bool.TryParse(data, out hasPlacedPrefabs);
        }

        public bool OnSaveCondition()
        {
            return true;
        }
    }
}
