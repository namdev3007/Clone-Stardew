using System.Collections.Generic;
using UnityEngine;

namespace World
{
    [DisallowMultipleComponent]
    public sealed class ClearableOrchardProp : MonoBehaviour
    {
        private static readonly Dictionary<Vector3Int, List<ClearableOrchardProp>> ByCell =
            new Dictionary<Vector3Int, List<ClearableOrchardProp>>();
        private static readonly List<ClearableOrchardProp> AllActiveProps = new List<ClearableOrchardProp>();

        [SerializeField] private string propId;
        [SerializeField] private Vector3Int cell;
        [SerializeField, Min(1)] private int requiredHits = 1;
        [SerializeField] private bool requiresHomeOrchardUnlock = true;
        private SpecialCropProgressService progress;
        private readonly List<Vector3Int> registeredCells = new List<Vector3Int>();

        public static ClearableOrchardProp FindAtCell(Vector3Int targetCell)
        {
            // 1. Direct cell lookup
            if (ByCell.TryGetValue(targetCell, out List<ClearableOrchardProp> props))
            {
                for (int i = props.Count - 1; i >= 0; i--)
                {
                    if (props[i] == null)
                        props.RemoveAt(i);
                    else if (props[i].gameObject.activeInHierarchy)
                        return props[i];
                }
            }

            // 2. Fallback check for taller trees/props where mouse clicked adjacent trunk or canopy
            Vector3 targetWorld = new Vector3((targetCell.x + 0.5f) * 0.16f, (targetCell.y + 0.5f) * 0.16f, 0f);
            for (int i = AllActiveProps.Count - 1; i >= 0; i--)
            {
                ClearableOrchardProp prop = AllActiveProps[i];
                if (prop == null || !prop.gameObject.activeInHierarchy)
                    continue;

                // Check distance
                if (Mathf.Abs(prop.cell.x - targetCell.x) <= 1 && Mathf.Abs(prop.cell.y - targetCell.y) <= 2)
                {
                    Collider2D col = prop.GetComponent<Collider2D>();
                    if (col != null && col.bounds.Contains(targetWorld))
                        return prop;

                    SpriteRenderer sr = prop.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && sr.bounds.Contains(targetWorld))
                        return prop;
                }
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
            HashSet<GameObject> processed = new HashSet<GameObject>();

            for (int i = 0; i < generated.Length; i++)
            {
                MapRegionGeneratedProp source = generated[i];
                if (source == null)
                    continue;

                processed.Add(source.gameObject);
                Vector3Int placementCell = GetPlacementCell(source);
                if (!region.Contains(placementCell) ||
                    (excludedRegion != null && excludedRegion.Contains(placementCell)))
                    continue;

                SpriteRenderer renderer = source.GetComponentInChildren<SpriteRenderer>();
                if (!IsRemovableVegetation(renderer?.sprite?.name ?? source.gameObject.name))
                    continue;

                ClearableOrchardProp clearable = source.GetComponent<ClearableOrchardProp>() ??
                                                source.gameObject.AddComponent<ClearableOrchardProp>();
                clearable.Configure(placementCell, progressService, requiresUnlock, idPrefix);
            }

            // Also support any duplicated vegetation props that might not have MapRegionGeneratedProp
            SpriteRenderer[] allRenderers = Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                SpriteRenderer sr = allRenderers[i];
                if (sr == null || processed.Contains(sr.gameObject))
                    continue;

                if (sr.gameObject.layer != 0 ||
                    sr.CompareTag("Player") ||
                    sr.GetComponentInParent<Entity_Components.Player.GridSelector>() != null ||
                    sr.GetComponentInParent<UnityEngine.Tilemaps.Tilemap>() != null)
                    continue;

                string spriteName = sr.sprite != null ? sr.sprite.name : sr.gameObject.name;
                if (!IsRemovableVegetation(spriteName))
                    continue;

                Vector3 worldPos = sr.transform.position;
                BoxCollider2D box = sr.GetComponent<BoxCollider2D>();
                Vector2 basePos = (Vector2)worldPos + (box != null ? box.offset : Vector2.zero);
                Vector3Int placementCell = new Vector3Int(
                    Mathf.FloorToInt(basePos.x / 0.16f),
                    Mathf.FloorToInt(basePos.y / 0.16f),
                    0);

                if (!region.Contains(placementCell) ||
                    (excludedRegion != null && excludedRegion.Contains(placementCell)))
                    continue;

                ClearableOrchardProp clearable = sr.GetComponent<ClearableOrchardProp>() ??
                                                sr.gameObject.AddComponent<ClearableOrchardProp>();
                clearable.Configure(placementCell, progressService, requiresUnlock, idPrefix);
            }
        }

        private void Configure(Vector3Int placementCell,
            SpecialCropProgressService progressService, bool requiresUnlock, string idPrefix)
        {
            if (progress != null)
                progress.StateChanged -= RefreshState;

            cell = placementCell;
            SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
            string spriteName = renderer?.sprite?.name ?? gameObject.name;
            requiredHits = DetermineRequiredHits(spriteName, renderer);
            requiresHomeOrchardUnlock = requiresUnlock;
            // Include gameObject.name to guarantee unique ID even if cloned via Ctrl+D
            propId = $"{idPrefix}/{cell.x}_{cell.y}/{gameObject.name}_{spriteName}";
            progress = progressService;

            if (progress != null)
                progress.StateChanged += RefreshState;

            Register();
            RefreshState();
        }

        private static Vector3Int GetPlacementCell(MapRegionGeneratedProp source)
        {
            Vector3 worldPos = source.transform.position;
            BoxCollider2D box = source.GetComponent<BoxCollider2D>();
            Vector2 basePos = (Vector2)worldPos + (box != null ? box.offset : Vector2.zero);
            Vector3Int actualBaseCell = new Vector3Int(
                Mathf.FloorToInt(basePos.x / 0.16f),
                Mathf.FloorToInt(basePos.y / 0.16f),
                0);

            Vector3Int target = source.TargetCell != default ? source.TargetCell : source.SourceCell;
            // If target is unassigned or differs from actual world position (e.g. duplicated via Ctrl+D)
            if (target == default || Mathf.Abs(target.x - actualBaseCell.x) > 1 || Mathf.Abs(target.y - actualBaseCell.y) > 1)
            {
                return actualBaseCell;
            }

            return target;
        }

        private void OnEnable()
        {
            Register();
            if (progress != null)
                progress.StateChanged += RefreshState;
            RefreshState();
        }

        private void OnDisable()
        {
            if (progress != null)
                progress.StateChanged -= RefreshState;

            for (int i = 0; i < registeredCells.Count; i++)
            {
                Vector3Int c = registeredCells[i];
                if (ByCell.TryGetValue(c, out List<ClearableOrchardProp> props))
                {
                    props.Remove(this);
                    if (props.Count == 0)
                        ByCell.Remove(c);
                }
            }
            registeredCells.Clear();
            AllActiveProps.Remove(this);
        }

        private void OnDestroy()
        {
            if (progress != null)
                progress.StateChanged -= RefreshState;
        }

        public bool IsTreeOrWood
        {
            get
            {
                string name = (gameObject.name + "_" + (GetComponentInChildren<SpriteRenderer>()?.sprite?.name ?? "")).ToLowerInvariant();
                return name.Contains("cay") || name.Contains("cây") || name.Contains("tree") ||
                       name.Contains("go") || name.Contains("gỗ") || name.Contains("wood") ||
                       name.Contains("log") || name.Contains("stump") || name.Contains("banana") ||
                       name.Contains("chuoi") || name.Contains("chuối");
            }
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

            if (!AllActiveProps.Contains(this))
                AllActiveProps.Add(this);

            AddCellRegistration(cell);

            // Also register center cell (transform.position) if distinct from base cell
            Vector3 worldPos = transform.position;
            Vector3Int centerCell = new Vector3Int(
                Mathf.FloorToInt(worldPos.x / 0.16f),
                Mathf.FloorToInt(worldPos.y / 0.16f),
                0);
            AddCellRegistration(centerCell);

            // Also register cells covered by BoxCollider2D if present
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                Bounds b = box.bounds;
                int minX = Mathf.FloorToInt(b.min.x / 0.16f);
                int maxX = Mathf.FloorToInt(b.max.x / 0.16f);
                int minY = Mathf.FloorToInt(b.min.y / 0.16f);
                int maxY = Mathf.FloorToInt(b.max.y / 0.16f);
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    AddCellRegistration(new Vector3Int(x, y, 0));
                }
            }
        }

        private void AddCellRegistration(Vector3Int targetCell)
        {
            if (!registeredCells.Contains(targetCell))
                registeredCells.Add(targetCell);

            if (!ByCell.TryGetValue(targetCell, out List<ClearableOrchardProp> props))
                ByCell[targetCell] = props = new List<ClearableOrchardProp>();
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
            if (name.Contains("cay-lon") || name.Contains("cây-lớn") || name.Contains("cay lon") || name.Contains("cây lớn")) return 6;
            if (name.Contains("cay-vua") || name.Contains("cây-vừa") || name.Contains("cay vua") || name.Contains("cây vừa")) return 4;
            if (name.Contains("cay-nho") || name.Contains("cây-nhỏ") || name.Contains("cay nho") || name.Contains("cây nhỏ")) return 3;
            if (name.Contains("go") || name.Contains("gỗ") || name.Contains("wood") || name.Contains("stump") || name.Contains("log")) return 4;
            if (name.Contains("co-cao") || name.Contains("cỏ-cao") || name.Contains("long-grass") || name.Contains("grass-row")) return 2;
            if (name.Contains("bui-cay") || name.Contains("bụi-cây") || name.Contains("flower") ||
                name.Contains("hoa") || name.Contains("bush") || name.Contains("bui") || name.Contains("bụi")) return 2;
            if (name.Contains("co-nho") || name.Contains("cỏ-nhỏ") || name.Contains("grass") ||
                name.Contains("weed") || name.Contains("cỏ")) return 1;

            // Compatibility fallback for any older decoration sprites without standard names.
            float height = renderer != null ? renderer.bounds.size.y : 0f;
            return height >= 0.9f ? 6 : (height >= 0.4f ? 4 : 2);
        }

        private static bool IsRemovableVegetation(string sourceName)
        {
            string name = (sourceName ?? string.Empty).ToLowerInvariant();
            return name.Contains("banana") || name.Contains("chuoi") || name.Contains("chu\u1ed1i") ||
                   name.Contains("cay") || name.Contains("cây") ||
                   name.Contains("tree") ||
                   name.Contains("co") || name.Contains("cỏ") || name.Contains("grass") || name.Contains("weed") ||
                   name.Contains("bui") || name.Contains("bụi") || name.Contains("bush") ||
                   name.Contains("hoa") || name.Contains("flower") ||
                   name.Contains("go") || name.Contains("gỗ") || name.Contains("wood") || name.Contains("log") || name.Contains("stump");
        }
    }
}
