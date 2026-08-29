using UnityEngine;
using UnityEngine.Events;

namespace Main_Menu
{
    public class UICharacterRenderer : MonoBehaviour
    {
        private RenderTexture renderTexture;

        [SerializeField]
        private UnityEventRenderTexture onTextureMade;

        [SerializeField]
        private new UnityEngine.Camera camera;

        [System.Serializable]
        public class UnityEventRenderTexture : UnityEvent<RenderTexture> { }

        // Use this for initialization
        void Awake()
        {
            renderTexture = new RenderTexture(64, 64, 16);
            renderTexture.Create();
            renderTexture.filterMode = FilterMode.Point;

            if (camera != null)
            {
                camera.targetTexture = renderTexture;
                camera.enabled = true;

                onTextureMade?.Invoke(renderTexture);
            }
        }

        public RenderTexture GetTexture()
        {
            return renderTexture;
        }
    }
}
