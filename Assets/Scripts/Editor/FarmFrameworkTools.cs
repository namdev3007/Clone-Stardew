using Item;
using Item.Inventory;
using Plugins.Scriptable_Object_Updater.Attribute;
using Plugins.Scriptable_Object_Updater.Enums;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace FarmingKitEditor
{
    [CreateAssetMenu(fileName = "Farm Framework Tools", menuName = "Tools/Farm Framework Tools")]
    public class FarmFrameworkTools : ScriptableObject
    {
        private static readonly string assetName = "Farm Framework Tools.asset";
        private static readonly string folderPath = "Editor Default Resources";

        [MenuItem("Tools/RPG Farm Framework Tools")]
        static void Open()
        {
            Selection.activeObject = ObtainTools();
        }

        public static FarmFrameworkTools ObtainTools()
        {
            FarmFrameworkTools toolData = EditorGUIUtility.Load(assetName) as FarmFrameworkTools;

            if (toolData == null)
            {
                if (!AssetDatabase.IsValidFolder($"Assets/{folderPath}"))
                {
                    AssetDatabase.CreateFolder("Assets", folderPath);
                }

                toolData = new FarmFrameworkTools();

                AssetDatabase.CreateAsset(toolData, $"Assets/{folderPath}/{assetName}");
            }

            return toolData;
        }
    
        [UpdateScriptableObject(eventType = EEventType.Start, Delay = 0.25f, editorOnly = true)]
        public void SetPlayerLocation()
        {
            if (EditorPrefs.GetBool("WarpPlayerToViewOnPlay", false))
            {
                GameObject getPlayer = GameObject.FindWithTag("Player");

                if (getPlayer != null)
                {
                    Vector2 cameraLocation = new Vector2()
                    {
                        x = EditorPrefs.GetFloat("SceneViewXPosition", 0),
                        y = EditorPrefs.GetFloat("SceneViewYPosition", 0)
                    };

                    getPlayer.transform.position = cameraLocation;
                }
            }
        }

        [System.Serializable]
        public class GivePlayerItem
        {
            public ItemData item;
            public int amount;
            public bool give;
        }

        [SerializeField]
        GivePlayerItem givePlayerItem;

        public void OnValidate()
        {
            if (!Application.isPlaying)
                return;

            if (givePlayerItem.give)
            {
                if (givePlayerItem.item == null)
                    return;

                GameObject getPlayer = GameObject.FindWithTag("Player");

                Debug.Log("Cannot find player");

                if (getPlayer != null)
                {
                    Debug.Log("Added item");

                    Inventory getInventory = getPlayer.GetComponent<Inventory>();
                    getInventory.AddItem(givePlayerItem.item, givePlayerItem.amount);
                }

                givePlayerItem.give = false;
            }
        }
    }

    public class CallbackObject : MonoBehaviour
    {
        public System.Action OnDestroyAction = delegate { };

        public IEnumerator Start()
        {
            yield return new WaitForSeconds(0.25f);
            GameObject.Destroy(this.gameObject);
        }

        public void OnDestroy()
        {
            OnDestroyAction.Invoke();
        }
    }
}