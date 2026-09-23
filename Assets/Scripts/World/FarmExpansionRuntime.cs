using System.Collections;
using Audio;
using System.Collections.Generic;
using UnityEngine;
using World.Objects;

namespace World
{
    /// <summary>Runtime policy and reveal sequence for the named initial farm and home orchard regions.</summary>
    public static class FarmExpansionRuntime
    {
        private static GridManager grid;
        private static SpecialCropProgressService progress;
        private static MapRegionDefinition initialFarm;
        private static MapRegionDefinition homeOrchard;

        public static bool IsRevealActive { get; private set; }
        public static bool QuestAvailable => progress != null && progress.DragonFruitRepaired &&
                                             !progress.HomeOrchardUnlocked && !IsRevealActive;
        public static bool HomeOrchardUnlocked => progress != null && progress.HomeOrchardUnlocked;

        public static void Configure(GridManager gridManager, SpecialCropProgressService progressService,
            MapRegionDefinition initialFarmRegion, MapRegionDefinition homeOrchardRegion)
        {
            grid = gridManager;
            progress = progressService;
            initialFarm = initialFarmRegion;
            homeOrchard = homeOrchardRegion;
            // A scene load cancels any reveal that was still running. The flag is
            // static, and leaving it on would block pause, the bag and dialogue.
            IsRevealActive = false;
        }

        public static bool CanHoe(Vector3Int cell)
        {
            if (initialFarm == null)
                return false;

            // Only the named normal farm accepts ordinary hoeing. Special crop
            // areas are handled before this method by SpecialCropRuntime.
            return initialFarm.Contains(cell);
        }

        public static bool CanPlant(Vector3Int cell, CropDefinition definition)
        {
            if (initialFarm == null)
                return false;
            if (definition == null)
                return false;

            // Vegetables and other ordinary crops are exclusive to the normal
            // farm. Banana/Mango keep their progression gate and may use either
            // the normal farm or the unlocked perennial orchard, inset by at least 1 tile from the border.
            if (!definition.IsPerennialTree)
                return initialFarm.Contains(cell);

            return HomeOrchardUnlocked && CanPlantPerennialFootprint(cell);
        }

        public static bool CanPlantPerennialFootprint(Vector3Int center)
        {
            if (!HomeOrchardUnlocked)
                return false;
            MapRegionDefinition owner = initialFarm != null && initialFarm.Contains(center)
                ? initialFarm
                : homeOrchard != null && homeOrchard.Contains(center) ? homeOrchard : null;
            if (owner == null)
                return false;

            // Perennial trees (banana, mango) cannot be planted on the outer edge/border of the soil/region;
            // they must be indented at least 1 tile deep inwards into the area.
            // With a 1-tile inset/padding around the 3x3 footprint, radius 2 [-2..2] must stay inside the owner region.
            for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
                if (!owner.Contains(center + new Vector3Int(x, y, 0)))
                    return false;
            return true;
        }

        public static void BeginReveal(MonoBehaviour host, System.Action completed = null)
        {
            if (grid == null || progress == null || homeOrchard == null || IsRevealActive)
            {
                completed?.Invoke();
                return;
            }

            // Run on an own object instead of the NPC: if the NPC is disabled or
            // destroyed mid-sequence the coroutine dies, and the player would be
            // left with the camera locked and every input blocked.
            GameObject runner = new GameObject("Home Orchard Reveal");
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<RevealRunner>().StartCoroutine(RevealRoutine(completed, runner));
        }

        /// <summary>Host for the reveal coroutine; nothing else in the scene owns it.</summary>
        private sealed class RevealRunner : MonoBehaviour
        {
        }

        private static IEnumerator RevealRoutine(System.Action completed, GameObject runner)
        {
            IsRevealActive = true;
            Vector3 min = grid.GetWorldLocation(homeOrchard.MinCell);
            Vector3 max = grid.GetWorldLocation(homeOrchard.MaxCell + new Vector3Int(1, 1, 0));
            Vector3 target = (min + max) * 0.5f;
            target.z = -10f;

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            Vector3 oldPosition = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
            float oldSize = mainCamera != null ? mainCamera.orthographicSize : 1f;
            List<Behaviour> suspended = SuspendCinemachineCameras();
            GameObject markers = BuildCornerMarkers(min, max);

            // Whatever happens in between, the camera, the cameras' scripts and the
            // input-blocking flag are always restored.
            try
            {
                if (mainCamera != null)
                {
                    float revealSize = Mathf.Max((max.y - min.y) * 0.6f,
                        (max.x - min.x) / Mathf.Max(0.1f, mainCamera.aspect) * 0.6f);
                    float elapsed = 0f;
                    while (elapsed < 0.7f)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.7f));
                        mainCamera.transform.position = Vector3.Lerp(oldPosition, target, p);
                        mainCamera.orthographicSize = Mathf.Lerp(oldSize, revealSize, p);
                        yield return null;
                    }
                }

                float flashTime = 0f;
                while (flashTime < 5f)
                {
                    flashTime += UnityEngine.Time.unscaledDeltaTime;
                    if (markers != null)
                        markers.SetActive(Mathf.Repeat(flashTime, 0.6f) < 0.4f);
                    yield return null;
                }

                progress.UnlockHomeOrchard();
                GameAudioService.PlayUnlockLand();
            }
            finally
            {
                if (mainCamera != null)
                {
                    mainCamera.transform.position = oldPosition;
                    mainCamera.orthographicSize = oldSize;
                }
                for (int i = 0; i < suspended.Count; i++)
                    if (suspended[i] != null)
                        suspended[i].enabled = true;
                if (markers != null)
                    Object.Destroy(markers);
                if (runner != null)
                    Object.Destroy(runner);
                IsRevealActive = false;
            }

            completed?.Invoke();
        }

        private static List<Behaviour> SuspendCinemachineCameras()
        {
            List<Behaviour> result = new List<Behaviour>();
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.enabled && behaviour.GetType().Name == "CinemachineCamera")
                {
                    behaviour.enabled = false;
                    result.Add(behaviour);
                }
            }
            return result;
        }

        private static GameObject BuildCornerMarkers(Vector3 min, Vector3 max)
        {
            GameObject root = new GameObject("Home Orchard Corner Highlight");
            Vector3[] corners =
            {
                new Vector3(min.x, min.y), new Vector3(max.x, min.y),
                new Vector3(min.x, max.y), new Vector3(max.x, max.y)
            };
            for (int i = 0; i < corners.Length; i++)
            {
                bool right = (i & 1) != 0;
                bool top = (i & 2) != 0;
                GameObject corner = new GameObject("Corner " + (i + 1));
                corner.transform.SetParent(root.transform, false);
                LineRenderer line = corner.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 3;
                line.startWidth = line.endWidth = 0.035f;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = line.endColor = Color.white;
                line.sortingOrder = 32000;
                float dx = right ? -0.35f : 0.35f;
                float dy = top ? -0.35f : 0.35f;
                line.SetPosition(0, corners[i] + new Vector3(dx, 0f));
                line.SetPosition(1, corners[i]);
                line.SetPosition(2, corners[i] + new Vector3(0f, dy));
            }
            return root;
        }
    }
}
