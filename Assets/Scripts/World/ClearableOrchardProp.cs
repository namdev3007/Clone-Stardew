using System.Collections.Generic;
using UnityEngine;

namespace World
{
    [DisallowMultipleComponent]
    public sealed class ClearableOrchardProp : MonoBehaviour
    {
        private static readonly Dictionary<Vector3Int, List<ClearableOrchardProp>> ByCell =
            new Dictionary<Vector3Int, List<ClearableOrchardProp>>();

        [SerializeField] private string propId;
        [SerializeField] private Vector3Int cell;
        [SerializeField, Min(1)] private int requiredHits = 1;
        [SerializeField] private bool requiresHomeOrchardUnlock = true;
        private SpecialCropProgressService progress;

        public static ClearableOrchardProp FindAtCell(Vector3Int targetCell)
        {
            if (!ByCell.TryGetValue(targetCell, out List<ClearableOrchardProp> props))
                return null;
            for (int i = props.Count - 1; i >= 0; i--)
            {
                if (props[i] == null)
                    props.RemoveAt(i);
                else if (props[i].gameObject.activeInHierarchy)
                    return props[i];
            }
            return null;
        }

        public static void AttachNamedRegionProps(MapRegionDefinition region,
            SpecialCropProgressService progressService, bool requiresUnlock = true,
            MapRegionDefinition excludedRegion = null, string idPrefix = "HomeOrchard")
        {
            if (region == null || progressService == null)
                return;
            MapRegionGeneratedProp[] generated = Object.FindObjectsByType<MapRegionGeneratedProp>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < generated.Length; i++)
            {
                MapRegionGeneratedProp source = generated[i];
                if (source == null)
                    continue;
                Vector3Int placementCell = GetPlacementCell(source);
                if (!region.Contains(placementCell) ||
                    (excludedRegion != null && excludedRegion.Contains(placementCell)))
                    continue;
                SpriteRenderer renderer = source.GetComponentInChildren<SpriteRenderer>();
                if (!IsRemovableVegetation(renderer?.sprite?.name ?? source.gameObject.name))
                    continue;
                ClearableOrchardProp clearable = source.GetComponent<ClearableOrchardProp>() ??
                                                source.gameObject.AddComponent<ClearableOrchardProp>();
                clearable.Configure(source, placementCell, progressService, requiresUnlock, idPrefix);
            }
        }

        private void Configure(MapRegionGeneratedProp source, Vector3Int placementCell,
            SpecialCropProgressService progressService, bool requiresUnlock, string idPrefix)
        {
            cell = placementCell;
            SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
            string spriteName = renderer?.sprite?.name ?? gameObject.name;
            requiredHits = DetermineRequiredHits(spriteName, renderer);
            requiresHomeOrchardUnlock = requiresUnlock;
            propId = $"{idPrefix}/{cell.x}_{cell.y}/{spriteName}";
            progress = progressService;
            Register();
            RefreshState();
        }

        private static Vector3Int GetPlacementCell(MapRegionGeneratedProp source)
        {
            // Reference-map props preserve their original source-image cell separately;
            // region ownership and gameplay always use the actual target map cell.
            return source.TargetCell != default ? source.TargetCell : source.SourceCell;
        }

        private void OnEnable() => Register();

        private void OnDisable()
        {
            if (!ByCell.TryGetValue(cell, out List<ClearableOrchardProp> props))
                return;
            props.Remove(this);
            if (props.Count == 0)
                ByCell.Remove(cell);
        }

        public bool TryHit()
        {
            if (progress == null ||
                (requiresHomeOrchardUnlock && !progress.HomeOrchardUnlocked) ||
                progress.IsOrchardPropCleared(propId))
                return false;
            bool accepted = progress.RecordOrchardPropHit(propId, requiredHits);
            if (accepted)
            {
                transform.localScale *= 0.96f;
                RefreshState();
            }
            return accepted;
        }

        private void Register()
        {
            if (string.IsNullOrEmpty(propId))
                return;
            if (!ByCell.TryGetValue(cell, out List<ClearableOrchardProp> props))
                ByCell[cell] = props = new List<ClearableOrchardProp>();
            if (!props.Contains(this))
                props.Add(this);
        }

        private void RefreshState()
        {
            if (progress != null && progress.IsOrchardPropCleared(propId))
                gameObject.SetActive(false);
        }

        private static int DetermineRequiredHits(string sourceName, SpriteRenderer renderer)
        {
            string name = (sourceName ?? string.Empty).ToLowerInvariant();

            // Exact gameplay rules for removable props inside the unlocked home orchard.
            if (name.Contains("banana") || name.Contains("chuoi") || name.Contains("chu\u1ed1i")) return 3;
            if (name.Contains("cay-lon") || name.Contains("cây-lớn") || name.Contains("cay lon")) return 6;
            if (name.Contains("cay-nho") || name.Contains("cây-nhỏ") || name.Contains("cay nho") ||
                name.Contains("cay-vua") || name.Contains("cây-vừa") || name.Contains("cay vua")) return 4;
            if (name.Contains("co-cao") || name.Contains("cỏ-cao") || name.Contains("long-grass") ||
                name.Contains("grass-row")) return 2;
            if (name.Contains("bui-cay") || name.Contains("bụi-cây") || name.Contains("flower") ||
                name.Contains("hoa") || name.Contains("bush") || name.Contains("bui") || name.Contains("bụi")) return 2;
            if (name.Contains("co-nho") || name.Contains("cỏ-nhỏ") || name.Contains("grass") ||
                name.Contains("weed") || name.Contains("cỏ")) return 1;

            // Compatibility fallback for any older decoration sprites without standard names.
            float height = renderer != null ? renderer.bounds.size.y : 0f;
            return height >= 0.9f ? 6 : 4;
        }

        private static bool IsRemovableVegetation(string sourceName)
        {
            string name = (sourceName ?? string.Empty).ToLowerInvariant();
            return name.Contains("banana") || name.Contains("chuoi") || name.Contains("chu\u1ed1i") ||
                   name.Contains("cay-") || name.Contains("cây-") ||
                   name.Contains("co-") || name.Contains("cỏ-") ||
                   name.Contains("bui-") || name.Contains("bụi-");
        }
    }
}
