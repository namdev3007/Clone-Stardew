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

        public static void AttachNamedRegionProps(MapRegionDefinition orchard,
            SpecialCropProgressService progressService)
        {
            if (orchard == null || progressService == null)
                return;
            MapRegionGeneratedProp[] generated = Object.FindObjectsByType<MapRegionGeneratedProp>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < generated.Length; i++)
            {
                MapRegionGeneratedProp source = generated[i];
                if (source == null || !orchard.Contains(source.SourceCell))
                    continue;
                ClearableOrchardProp clearable = source.GetComponent<ClearableOrchardProp>() ??
                                                source.gameObject.AddComponent<ClearableOrchardProp>();
                clearable.Configure(source, progressService);
            }
        }

        private void Configure(MapRegionGeneratedProp source, SpecialCropProgressService progressService)
        {
            cell = source.SourceCell;
            SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
            string spriteName = renderer?.sprite?.name ?? gameObject.name;
            requiredHits = DetermineRequiredHits(spriteName, renderer);
            propId = $"HomeOrchard/{cell.x}_{cell.y}/{spriteName}";
            progress = progressService;
            Register();
            RefreshState();
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
            if (progress == null || !progress.HomeOrchardUnlocked || progress.IsOrchardPropCleared(propId))
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
            if (name.Contains("banana") || name.Contains("chuoi") || name.Contains("chu\u1ed1i")) return 3;
            if (name.Contains("flower") || name.Contains("hoa") || name.Contains("bush") || name.Contains("bui")) return 2;
            if (name.Contains("grass") || name.Contains("weed") || name.Contains("co") || name.Contains("c\u1ecf")) return 1;
            float height = renderer != null ? renderer.bounds.size.y : 0f;
            return height >= 0.9f ? 6 : 4;
        }
    }
}
