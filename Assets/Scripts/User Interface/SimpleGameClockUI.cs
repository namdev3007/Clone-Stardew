using System;
using GameSystem.Systems;
using TMPro;
using UnityEngine;

namespace User_Interface
{
    /// <summary>Displays elapsed game time as HH:MM:SS.</summary>
    public sealed class SimpleGameClockUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private TimeSystem timeSystem;
        private int lastDisplayedSecond = -1;

        private void Awake()
        {
            FindClock();
            Refresh(true);
        }

        private void Update()
        {
            if (timeSystem == null)
                FindClock();

            Refresh(false);
        }

        private void FindClock()
        {
            timeSystem = FindFirstObjectByType<TimeSystem>();
        }

        private void Refresh(bool force)
        {
            if (label == null || timeSystem == null)
                return;

            TimeSpan elapsed = timeSystem.ElapsedTime;
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt((float)elapsed.TotalSeconds));
            if (!force && totalSeconds == lastDisplayedSecond)
                return;

            int hours = totalSeconds / 3600;
            int minutes = totalSeconds / 60 % 60;
            int seconds = totalSeconds % 60;
            label.text = $"{hours:00}:{minutes:00}:{seconds:00}";
            lastDisplayedSecond = totalSeconds;
        }
    }
}
