using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Utility
{
    public class Timer : MonoBehaviour
    {
        [SerializeField]
        private float WaitTime;

        [SerializeField]
        private UnityEvent eventToHappen;

        public void StartTimer()
        {
            StopAllCoroutines();
            StartCoroutine(TimerCoroutine());
        }

        private IEnumerator TimerCoroutine()
        {
            yield return new WaitForSeconds(WaitTime);
            eventToHappen.Invoke();
        }

    }
}
