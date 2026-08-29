using System.Collections;
using UnityEngine;

namespace Utility
{
    [AddComponentMenu("Farming Kit/User Interface/Sprite Fader")]
    [RequireComponent(typeof(SpriteRenderer)), DisallowMultipleComponent]
    public class SpriteFader : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [SerializeField]
        private float transitionTime = 0.5f;

        private Coroutine fadeCoroutine;

        private void OnValidate()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        public void SetAlpha(float alpha)
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
        
            if (!gameObject.activeInHierarchy)
                return;
            
            fadeCoroutine = StartCoroutine(TransitionColor(new Color(1, 1, 1, alpha)));
        }

        public void SetAlphaInstant(float alpha)
        {
            StopAllCoroutines();
            spriteRenderer.color = new Color(1, 1, 1, alpha);
        }

        IEnumerator TransitionColor(Color targetColor)
        {
            float t = 0;

            Color currentColor = spriteRenderer.color;

            while (t != transitionTime)
            {
                spriteRenderer.color = Color.Lerp(currentColor, targetColor, t / transitionTime);
                t += UnityEngine.Time.deltaTime;
                yield return null;
            }
        }
    }
}
