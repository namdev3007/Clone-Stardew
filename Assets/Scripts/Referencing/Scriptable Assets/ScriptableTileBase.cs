using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Referencing.Scriptable_Assets
{
    [System.Serializable]
    public class ScriptableTileBase : TileBase, IReferenceableAsset
    {
        [SerializeField, HideInInspector]
        private string guid;

        public void GenerateNewGuid()
        {
            guid = System.Guid.NewGuid().ToString();

#if UNITY_EDITOR
            Debug.Log($"Guid has been set on asset {this.name}");
            EditorUtility.SetDirty(this);
#endif
        }

        public string GetGuid()
        {
            if (string.IsNullOrEmpty(guid))
            {
                GenerateNewGuid();
            }

            return guid;
        }
    }
}