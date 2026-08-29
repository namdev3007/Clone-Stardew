using Unity.Cinemachine;
using UnityEngine;

namespace Camera
{
    [ExecuteInEditMode]
    [AddComponentMenu("")] // Hide in menu
    [SaveDuringPlay]
    public class PixelPerfectCamera : CinemachineExtension
    {
        [SerializeField]
        private float unitSize = 400;

        [SerializeField]
        private bool pixelPerfectMovement = false;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage == CinemachineCore.Stage.Body)
            {

                ((CinemachineCamera)vcam).Lens.OrthographicSize = Screen.height / (unitSize * 2.0f);

                if (pixelPerfectMovement)
                {
                    Vector3 rawPosition = state.RawPosition;
                    for (int i = 0; i < 3; i++)
                    {
                        rawPosition[i] = (Mathf.Ceil(rawPosition[i] * unitSize) / unitSize);
                    }
                    state.RawPosition = rawPosition;
                }

            }
        }

        int GetNearestMultiple(int value, int multiple)
        {
            int remainder = value % multiple;

            int result = value - remainder;

            if (remainder > (multiple / 2))
            {
                result += multiple;
            }

            return result;
        }
    }
}