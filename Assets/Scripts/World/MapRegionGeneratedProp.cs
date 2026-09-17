using UnityEngine;

namespace World
{
    /// <summary>
    /// Identifies an editor-authored prop and the named map region that owns it.
    /// Kept in its own file so Unity can serialize a stable MonoScript reference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapRegionGeneratedProp : MonoBehaviour
    {
        [SerializeField] private string regionName;
        [SerializeField] private Vector3Int sourceCell;
        [SerializeField] private Vector3Int targetCell;
        [SerializeField] private string propId;

        public string RegionName => regionName;
        public Vector3Int SourceCell => sourceCell;
        public Vector3Int TargetCell => targetCell;
        public string PropId => propId;

        public void Initialize(string owningRegion, Vector3Int cell, Vector3Int target = default, string id = null)
        {
            regionName = owningRegion;
            sourceCell = cell;
            targetCell = target;
            propId = id ?? string.Empty;
        }
    }
}
