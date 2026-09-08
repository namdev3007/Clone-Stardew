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
            RelocateSpawnedPrefabsFromBlockedTiles();

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

                GameObject sourcePrefab = spawnConfig.prefab?.GetPrefab();
                string cloneName = sourcePrefab != null ? sourcePrefab.name + "(Clone)" : "Spawned(Clone)";
                int spawnedNumber = CountSpawnedPrefabs(cloneName);

                foreach (Vector3 location in selectLocations)
                {
                    GameObject spawnedGameObject = spawnConfig.prefab.Retrieve<GameObject>(scene: this.gameObject.scene);

                    if (spawnedGameObject == null)
                        continue;

                    spawnedGameObject.transform.position = location;
                    spawnedNumber++;
                    spawnedGameObject.name = $"{cloneName} {spawnedNumber}";
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
                        if (sceneObject.gameObject == gameObject || !IsSpawnedPrefabName(sceneObject.name, cloneName))
                        {
                            continue;
                        }

                        if (keptCount < spawnConfig.amount)
                        {
                            keptCount++;
                            sceneObject.name = $"{cloneName} {keptCount}";
                            continue;
                        }

                        Destroy(sceneObject.gameObject);
                    }
                }
            }
        }

        private int CountSpawnedPrefabs(string cloneName)
        {
            int count = 0;
            GameObject[] sceneRoots = gameObject.scene.GetRootGameObjects();
            foreach (GameObject sceneRoot in sceneRoots)
            {
                Transform[] sceneObjects = sceneRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform sceneObject in sceneObjects)
                {
                    if (sceneObject.gameObject != gameObject && IsSpawnedPrefabName(sceneObject.name, cloneName))
                        count++;
                }
            }
            return count;
        }

        private void RelocateSpawnedPrefabsFromBlockedTiles()
        {
            if (targetTileMap == null)
                return;

            List<Vector3> availableLocations = new List<Vector3>();
            foreach (Vector3Int location in targetTileMap.cellBounds.allPositionsWithin)
            {
                if (!targetTileMap.HasTile(location) || IsBlockedLocation(location))
                    continue;

                availableLocations.Add(targetTileMap.CellToWorld(location) + (Vector3)placementOffset);
            }

            GameObject[] sceneRoots = gameObject.scene.GetRootGameObjects();
            foreach (SpawnPrefab spawnConfig in prefabs)
            {
                GameObject sourcePrefab = spawnConfig.prefab?.GetPrefab();
                if (sourcePrefab == null)
                    continue;

                string cloneName = sourcePrefab.name + "(Clone)";
                List<Transform> spawnedObjects = new List<Transform>();
                foreach (GameObject sceneRoot in sceneRoots)
                {
                    foreach (Transform sceneObject in sceneRoot.GetComponentsInChildren<Transform>(true))
                    {
                        if (sceneObject.gameObject != gameObject && IsSpawnedPrefabName(sceneObject.name, cloneName))
                            spawnedObjects.Add(sceneObject);
                    }
                }

                HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
                for (int i = 0; i < spawnedObjects.Count; i++)
                {
                    Vector3Int currentCell = targetTileMap.WorldToCell(spawnedObjects[i].position);
                    bool valid = targetTileMap.HasTile(currentCell) && !IsBlockedLocation(currentCell) && occupiedCells.Add(currentCell);
                    if (valid)
                        continue;

                    for (int locationIndex = availableLocations.Count - 1; locationIndex >= 0; locationIndex--)
                    {
                        Vector3Int candidateCell = targetTileMap.WorldToCell(availableLocations[locationIndex]);
                        if (occupiedCells.Contains(candidateCell))
                            continue;

                        spawnedObjects[i].position = availableLocations[locationIndex];
                        occupiedCells.Add(candidateCell);
                        availableLocations.RemoveAt(locationIndex);
                        break;
                    }
                }
            }
        }

        private bool IsBlockedLocation(Vector3Int location)
        {
            for (int i = 0; i < dontAllowOverlapTileMaps.Length; i++)
            {
                if (dontAllowOverlapTileMaps[i] != null && dontAllowOverlapTileMaps[i].HasTile(location))
                    return true;
            }

            return false;
        }

        private static bool IsSpawnedPrefabName(string objectName, string cloneName)
        {
            return objectName == cloneName || objectName.StartsWith(cloneName + " ");
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
