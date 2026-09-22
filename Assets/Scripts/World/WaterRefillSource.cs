using System;
using System.Collections.Generic;
using UnityEngine;

namespace World
{
    [DisallowMultipleComponent]
    public sealed class WaterRefillSource : MonoBehaviour
    {
        [SerializeField] private Vector3Int[] refillCells = Array.Empty<Vector3Int>();
        private GridManager gridManager;

        public IReadOnlyList<Vector3Int> RefillCells => refillCells;

        public void Configure(GridManager grid, IEnumerable<Vector3Int> cells)
        {
            if (Application.isPlaying)
                Unregister();

            gridManager = grid;
            refillCells = cells != null ? new List<Vector3Int>(cells).ToArray() : Array.Empty<Vector3Int>();

            if (Application.isPlaying)
                Register();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                Register();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
                Unregister();
        }

        private void Register()
        {
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();
            if (gridManager == null)
                return;

            for (int i = 0; i < refillCells.Length; i++)
                gridManager.RegisterWaterRefillCell(refillCells[i]);
        }

        private void Unregister()
        {
            if (gridManager == null)
                return;

            for (int i = 0; i < refillCells.Length; i++)
                gridManager.UnregisterWaterRefillCell(refillCells[i]);
        }
    }
}
