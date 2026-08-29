using Event.Events;
using Unity.Cinemachine;
using UnityEngine;

namespace Camera
{
    public class ForceCameraPositionOnWarp : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private FloatEvent onWarpDoneEvent;

        private void Awake()
        {
            onWarpDoneEvent.AddListener(OnWarped);
        }

        private void OnWarped(float obj)
        {
            virtualCamera.ForceCameraPosition(virtualCamera.Follow.transform.position, new Quaternion());
        }
    }
}
