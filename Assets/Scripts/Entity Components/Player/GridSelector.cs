using Entity_Components.Interfaces;
using Event.Events;
using Referencing.Scriptable_Reference;
using UnityEngine;
using UnityEngine.EventSystems;
using World;

namespace Entity_Components.Player
{
    [AddComponentMenu("Farming Kit/Entity Components/Player/Grid Selector")]
    public class GridSelector : MonoBehaviour, IMove
    {
        [SerializeField]
        private ScriptableReference gridManagerReference;

        private GridManager gridManager;

        [SerializeField]
        private BoolEvent onGamePauzed;

        [SerializeField]
        private Vector2Event mouseWorldInput;

        [SerializeField]
        private UnityEngine.Sprite cursorSprite;

        private GameObject selectionGameObject;
        private SpriteRenderer selectionSpriteRenderer;

        [SerializeField]
        private float selectionTileDistance;

        [SerializeField]
        private Vector2 gridOffset = new Vector2(0.08f, 0.08f);

        [SerializeField]
        private Vector2 selectionViewOffset;

        private Vector3Int characterForwardGridLocation;
        private Vector3Int characterGridLocation;
        private Vector3Int mouseGridLocation;

        private Vector3Int currentSelectionGridPosition;

        private Vector2 lastMousePosition;

        private bool displaySelectionView;
        private bool frozen;
        private bool isPaused;
        private Vector2 lastMoveDirection;
        private UnityEngine.Camera mainCamera;

        private const float PulseSpeed = 5.5f;
        private const float MinAlpha = 0.80f;
        private const float MaxAlpha = 1.0f;

        private void Awake()
        {
            mouseWorldInput?.AddListener(OnMouseMove);
            onGamePauzed?.AddListener(OnGamePauze);

            selectionGameObject = new GameObject("SelectionCursor");
            // Do not parent to this.transform: Player's SortingGroup traps children
            // and blocks independent depth-sorting against tiles and props.
            selectionSpriteRenderer = selectionGameObject.AddComponent<SpriteRenderer>();
            selectionSpriteRenderer.sprite = cursorSprite;
            selectionSpriteRenderer.sortingLayerName = "Dynamic";
            selectionSpriteRenderer.sortingOrder = 0;
            selectionSpriteRenderer.color = Color.white;
            selectionGameObject.SetActive(false);

            gridManagerReference.AddListener(OnFoundGridReference);
        }

        private void OnDisable()
        {
            if (selectionGameObject != null)
            {
                selectionGameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            gridManagerReference.RemoveListener(OnFoundGridReference);
            mouseWorldInput?.RemoveListener(OnMouseMove);
            onGamePauzed?.RemoveListener(OnGamePauze);

            if (selectionGameObject != null)
            {
                Destroy(selectionGameObject);
            }
        }

        private void OnFoundGridReference(GameObject obj)
        {
            gridManager = obj.GetComponent<GridManager>();
        }

        private void OnGamePauze(bool state)
        {
            isPaused = state;
            if (selectionGameObject != null)
            {
                selectionGameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (gridManager == null || frozen || isPaused)
            {
                if (selectionGameObject != null && selectionGameObject.activeSelf)
                    selectionGameObject.SetActive(false);
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = UnityEngine.Camera.main;
            }

            if (mainCamera != null)
            {
                Vector2 currentMouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                Vector3Int currentCell = gridManager.Grid.WorldToCell(currentMouseWorld);
                if (currentCell != mouseGridLocation)
                {
                    OnMouseMove(currentMouseWorld);
                }
                else
                {
                    UpdateHighlight();
                }
            }

            if (selectionGameObject != null && selectionGameObject.activeSelf && selectionSpriteRenderer != null)
            {
                float t = 0.5f + 0.5f * Mathf.Sin(UnityEngine.Time.unscaledTime * PulseSpeed);
                float alpha = Mathf.Lerp(MinAlpha, MaxAlpha, t);
                selectionSpriteRenderer.color = new Color(1f, 1f, 0.92f, alpha);
            }
        }

        private void OnMouseMove(Vector2 location)
        {
            lastMousePosition = location;

            if (gridManager == null || frozen || isPaused)
            {
                if (selectionGameObject != null && selectionGameObject.activeSelf)
                    selectionGameObject.SetActive(false);
                return;
            }

            mouseGridLocation = gridManager.Grid.WorldToCell(location);

            if (Vector3Int.Distance(mouseGridLocation, characterGridLocation) <= selectionTileDistance)
            {
                currentSelectionGridPosition = mouseGridLocation;
            }
            else
            {
                Vector2 mouseDirection = location - (Vector2)transform.position;
                if (mouseDirection.sqrMagnitude > 0.001f)
                {
                    characterForwardGridLocation = gridManager.Grid.WorldToCell(
                        (Vector2)transform.position
                        + Vector2.ClampMagnitude(mouseDirection, gridManager.Grid.cellSize.x));
                }
                currentSelectionGridPosition = characterForwardGridLocation;
            }

            UpdateHighlight();
        }

        private void UpdateHighlight()
        {
            if (selectionGameObject == null || gridManager == null || frozen || isPaused)
            {
                if (selectionGameObject != null && selectionGameObject.activeSelf)
                    selectionGameObject.SetActive(false);
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (selectionGameObject.activeSelf)
                    selectionGameObject.SetActive(false);
                return;
            }

            if (World.NPC.DialogueUIController.IsDialogueOpen ||
                User_Interface.BagWindow.AnyOpen ||
                World.NPC.ShopWindowController.AnyOpen)
            {
                if (selectionGameObject.activeSelf)
                    selectionGameObject.SetActive(false);
                return;
            }

            bool isSoil = gridManager.IsSoilCell(mouseGridLocation);
            if (isSoil)
            {
                if (!selectionGameObject.activeSelf)
                    selectionGameObject.SetActive(true);

                Vector2 cellCenter = (Vector2)gridManager.Grid.CellToWorld(mouseGridLocation) + gridOffset;
                Vector3 targetWorldPos = new Vector3(cellCenter.x, cellCenter.y, 0f);
                selectionGameObject.transform.position = targetWorldPos;

                selectionSpriteRenderer.sortingLayerName = "Dynamic";
                selectionSpriteRenderer.sortingOrder = Mathf.RoundToInt(targetWorldPos.y * -100f) + 5;
            }
            else
            {
                if (selectionGameObject.activeSelf)
                    selectionGameObject.SetActive(false);
            }
        }

        public void OnMove(Vector2 direction, float velocity)
        {
            lastMoveDirection = direction;

            if (frozen)
            {
                return;
            }

            if (gridManager != null)
            {
                characterGridLocation = gridManager.Grid.WorldToCell((Vector2)this.transform.position);

                if (direction != Vector2.zero)
                {
                    characterForwardGridLocation = gridManager.Grid.WorldToCell(((Vector2)this.transform.position + Vector2.ClampMagnitude(direction, gridManager.Grid.cellSize.x)));
                }

                OnMouseMove(lastMousePosition += (direction * velocity) * UnityEngine.Time.deltaTime);
            }
        }

        public Vector2 GetGridLookDirection()
        {
            if (Vector3Int.Distance(mouseGridLocation, characterGridLocation) <= selectionTileDistance)
            {
                return ((Vector3)lastMousePosition - (Vector3)this.transform.position).normalized;
            }
            else
            {
                return lastMoveDirection;
            }
        }

        public Vector2 GetMouseLookDirection()
        {
            Vector2 direction = lastMousePosition - (Vector2)transform.position;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : lastMoveDirection;
        }

        public Vector3Int GetGridSelectionPosition()
        {
            return currentSelectionGridPosition;
        }

        public Vector2 GetGridWorldSelectionPosition()
        {
            if (gridManager == null)
            {
                return this.transform.position;
            }

            return (Vector2)gridManager.Grid.CellToWorld(GetGridSelectionPosition()) + gridOffset;
        }

        public GridManager GetGridManager()
        {
            return gridManager;
        }

        public bool IsStandingOnSelectedTile()
        {
            return currentSelectionGridPosition == characterGridLocation;
        }

        public void Display(bool display)
        {
            displaySelectionView = display;
            if (!display && selectionGameObject != null)
            {
                selectionGameObject.SetActive(false);
            }
        }

        public void SetFrozen(bool frozen)
        {
            this.frozen = frozen;

            if (frozen && selectionGameObject != null)
            {
                selectionGameObject.SetActive(false);
            }
            else if (!frozen)
            {
                OnMouseMove(lastMousePosition);
            }
        }
    }
}
