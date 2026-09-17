using System;
using Event.Events;
using Item;
using Item.Inventory;
using Plugins.Lowscope.ComponentSaveSystem.Interfaces;
using Referencing.Scriptable_Variables.Variables;
using UnityEngine;

namespace GameSystem.Systems
{
    /// <summary>
    /// A simple elapsed game clock. One system tick equals one elapsed second.
    /// There is deliberately no calendar, day/night cycle, or daily weather here.
    /// </summary>
    [AddComponentMenu("Farming Kit/Systems/Simple Game Clock")]
    public class TimeSystem : GameSystem, ISaveable
    {
        private static readonly DateTime ClockEpoch = new DateTime(2000, 1, 1, 0, 0, 0);

        [SerializeField] private TimeEvent timeTickEvent;
        [SerializeField] private BoolEvent pauzeEvent;

        [Header("Passive coin reward")]
        [SerializeField] private ItemData currencyItem;
        [SerializeField, Min(1)] private int coinRewardAmount = 3;
        [SerializeField, Min(1f)] private float coinRewardIntervalSeconds = 12f * 60f * 60f;

        public DateTime startTime { get; private set; } = ClockEpoch;
        public DateTime currentTime { get; private set; } = ClockEpoch;
        public TimeSpan ElapsedTime => TimeSpan.FromSeconds(elapsedSeconds);

        private double elapsedSeconds;
        private long rewardedCoinIntervals;
        private bool loadedSave;
        private Inventory playerInventory;

        public override void OnLoadSystem()
        {
            if (!loadedSave)
                SetElapsedSeconds(0d, false);

            pauzeEvent?.AddListener(OnGamePauzed);
        }

        private void Start()
        {
            timeTickEvent?.Invoke(currentTime);
        }

        public override void OnTick()
        {
            SetElapsedSeconds(elapsedSeconds + 1d, true);
            GrantPendingCoinRewards();
        }

        private void GrantPendingCoinRewards()
        {
            if (currencyItem == null || coinRewardAmount <= 0 || coinRewardIntervalSeconds <= 0f)
                return;

            long completedIntervals = (long)Math.Floor(elapsedSeconds / coinRewardIntervalSeconds);
            long pendingIntervals = completedIntervals - rewardedCoinIntervals;
            if (pendingIntervals <= 0)
                return;

            if (playerInventory == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                playerInventory = player != null ? player.GetComponent<Inventory>() : null;
            }

            if (playerInventory == null)
                return;

            long totalReward = pendingIntervals * coinRewardAmount;
            while (totalReward > 0)
            {
                int rewardChunk = (int)Math.Min(totalReward, int.MaxValue);
                if (!playerInventory.AddItem(currencyItem, rewardChunk))
                    return;

                totalReward -= rewardChunk;
            }

            rewardedCoinIntervals = completedIntervals;
            Debug.Log($"Passive income: +{pendingIntervals * coinRewardAmount} coins after {completedIntervals * 12} elapsed hours.");
        }

        private void OnDisable()
        {
            pauzeEvent?.RemoveListener(OnGamePauzed);
        }

        private void OnGamePauzed(bool state)
        {
            Pauze(state);
        }

        private void SetElapsedSeconds(double value, bool notify)
        {
            elapsedSeconds = Math.Max(0d, value);
            startTime = ClockEpoch;
            currentTime = ClockEpoch.AddSeconds(elapsedSeconds);

            if (notify)
                timeTickEvent?.Invoke(currentTime);
        }

        [Serializable]
        private struct SaveData
        {
            public int version;
            public double elapsedSeconds;
            public long rewardedCoinIntervals;

            // Kept only so existing save files can be migrated to the simple clock.
            public int year;
            public int month;
            public int day;
            public int hour;
            public int minute;

            public bool HasLegacyTime()
            {
                return year != 0 || month != 0 || day != 0 || hour != 0 || minute != 0;
            }
        }

        public string OnSave()
        {
            return JsonUtility.ToJson(new SaveData
            {
                version = 2,
                elapsedSeconds = elapsedSeconds,
                rewardedCoinIntervals = rewardedCoinIntervals
            });
        }

        public void OnLoad(string dataString)
        {
            SaveData data = JsonUtility.FromJson<SaveData>(dataString);
            double savedSeconds = data.version >= 1
                ? data.elapsedSeconds
                : data.HasLegacyTime() ? data.hour * 3600d + data.minute * 60d : 0d;

            rewardedCoinIntervals = data.version >= 2
                ? Math.Max(0L, data.rewardedCoinIntervals)
                : (long)Math.Floor(savedSeconds / Math.Max(1f, coinRewardIntervalSeconds));

            SetElapsedSeconds(savedSeconds, true);
            loadedSave = true;
        }

        public bool OnSaveCondition()
        {
            return true;
        }
    }
}
