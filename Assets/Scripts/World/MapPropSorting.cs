using System;
using UnityEngine;

namespace World
{
    /// <summary>
    /// Sorting rules for map decoration props on the "Dynamic" layer.
    /// Trees and bushes sort by the Y of their base like the player; grass is
    /// flat ground cover and always stays underneath the player and crops.
    /// </summary>
    public static class MapPropSorting
    {
        public const string SortingLayer = "Dynamic";

        /// <summary>Lowest order on the Dynamic layer, below every Y-sorted object.</summary>
        public const int GrassSortingOrder = short.MinValue;

        /// <summary>Grass decoration sprites are exported as co-cao, co-nho-1, co-nho-2.</summary>
        public static bool IsGrass(Sprite sprite)
        {
            return sprite != null && sprite.name.StartsWith("co-", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Sorting order for a prop whose base touches the ground at <paramref name="groundY"/>.</summary>
        public static int GetSortingOrder(Sprite sprite, float groundY)
        {
            return IsGrass(sprite) ? GrassSortingOrder : Mathf.RoundToInt(-groundY * 100f);
        }
    }
}
