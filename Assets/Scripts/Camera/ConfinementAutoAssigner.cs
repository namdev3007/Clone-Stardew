using Event.Events;
using Referencing.Scriptable_Reference;
using Unity.Cinemachine;
using UnityEngine;

namespace Camera
{
    /// <summary>
    /// This class will automatically add a camera confinement reference
    /// Each time the scene changes.
    /// </summary>
    public class ConfinementAutoAssigner : MonoBehaviour
    {
        [SerializeField]
        private ScriptableReference cameraConfinement;

        [SerializeField]
        private StringEvent onSceneChanged;

        private CinemachineConfiner2D confiner;

        private void Awake()
        {
            cameraConfinement.AddListener(ApplyConfinement);

            ApplyConfinement(cameraConfinement?.Reference);
        }

        private void OnDestroy()
        { 
            cameraConfinement.RemoveListener(ApplyConfinement);
        }
    
        private void ApplyConfinement(GameObject cameraConfinementReference)
        {
            GameObject getConfinementObject = cameraConfinement?.Reference;

            if (getConfinementObject != null)
            {
                Collider2D getBoundingCollider = getConfinementObject.GetComponent<Collider2D>();

                if (getBoundingCollider == null)
                {
                    return;
                }

                if (confiner == null)
                {
                    confiner = gameObject.GetComponent<CinemachineConfiner2D>();

                    if (confiner == null)
                    {
                        confiner = gameObject.AddComponent<CinemachineConfiner2D>();
                    }
                }

                confiner.BoundingShape2D = getBoundingCollider;
            }
        }
    }
}
