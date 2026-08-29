using Plugins.Lowscope.ComponentSaveSystem;
using Plugins.Lowscope.ComponentSaveSystem.Enums;
using Referencing.Scriptable_Assets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Referencing
{
    /// <summary>
    /// This scriptable object is used to obtain unique references to prefabs.
    /// </summary>
    [CreateAssetMenu(fileName = "Saveable Prefab", menuName = "Referencing/Saveable prefab")]
    public class SaveablePrefab : ScriptableAsset
    {
        [SerializeField]
        private GameObject prefab;

        public T Retrieve<T>(string identification = "", Scene scene = default) where T : UnityEngine.Object
        {
            var spawnedPrefab = SaveMaster.SpawnSavedPrefab(InstanceSource.Custom, GetGuid(), scene: scene, customSource: "ScriptableAssetDatabase");

            if (typeof(T) == typeof(GameObject))
            {
                return spawnedPrefab as T;
            }

            return spawnedPrefab.GetComponent<T>();
        }

        public GameObject GetPrefab()
        {
            return prefab;
        }
    }
}