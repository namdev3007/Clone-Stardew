using UnityEngine;

namespace Referencing.Scriptable_Reference
{
    public class ScriptableReferenceSetter : MonoBehaviour
    {
        [SerializeField]
        private ScriptableReference target;

        private void Awake()
        {
            target.Reference = this.gameObject;
        }
    }
}
