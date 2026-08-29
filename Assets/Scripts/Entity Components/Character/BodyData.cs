using Referencing.Scriptable_Assets;
using System.Collections.Generic;
using UnityEngine;

namespace Entity_Components.Character
{
    [System.Serializable, CreateAssetMenu]
    public class BodyData : ScriptableAsset
    {
        public List<UnityEngine.Sprite> side = new List<UnityEngine.Sprite>();
        public List<UnityEngine.Sprite> front = new List<UnityEngine.Sprite>();
        public List<UnityEngine.Sprite> back = new List<UnityEngine.Sprite>();

        public BodyData subItem;
    }
}