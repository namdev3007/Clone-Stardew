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

        public string RegionName => regionName;
        public Vector3Int SourceCell => sourceCell;

        public void Initialize(string owningRegion, Vector3Int cell)
        {
            regionName = owningRegion;
            sourceCell = cell;
        }
    }
}
