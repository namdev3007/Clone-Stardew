using Entity_Components.Character;
using Plugins.Lowscope.ComponentSaveSystem;
using System.Collections.Generic;
using UnityEngine;

namespace Saving
{
    /// <summary>
    /// Used to store metadata for the character looks
    /// This eliminates the use of loading a savegame beforehand, and instead
    /// looks a small portion that contains some metadata about the save game.
    /// </summary>
    public class StoreCharacterBodyMetaData : MonoBehaviour
    {
        [SerializeField] private BodySpriteSwapper[] bodySpriteSwappers;

        [System.Serializable]
        public class SaveData
        {
            public List<BodyData> bodyInfo = new List<BodyData>();
        }

        [System.Serializable]
        public class BodyData
        {
            public string saveId;
            public string data;
        }

        private void Reset()
        {
            bodySpriteSwappers = GetComponentsInChildren<BodySpriteSwapper>(true);
        }

        private void OnDestroy()
        {
            if (bodySpriteSwappers == null || bodySpriteSwappers.Length == 0)
                return;

            SaveData data = new SaveData();
        
            foreach (var item in bodySpriteSwappers)
            {
                // The full-body player no longer uses the old modular body
                // renderers, so legacy prefab arrays can contain missing entries.
                if (item == null)
                    continue;

                Transform parent = item.transform.parent;
                data.bodyInfo.Add(new BodyData()
                {
                    data = item.OnSave(),
                    saveId = string.Format("{0}/{1}", parent != null ? parent.name : item.transform.root.name, item.name)
                });
            }

            // Do not overwrite existing character metadata with an empty legacy
            // payload during scene changes or an Editor domain reload.
            if (data.bodyInfo.Count > 0)
                SaveMaster.SetMetaData("character", JsonUtility.ToJson(data));
        }
    }
}
