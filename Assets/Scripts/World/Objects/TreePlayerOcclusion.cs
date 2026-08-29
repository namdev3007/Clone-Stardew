using Entity_Components.Character;
using UnityEngine;

namespace World.Objects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class TreePlayerOcclusion : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)]
        private float hiddenAlpha = 0.35f;

        [SerializeField]
        private float fadeSpeed = 8f;

        [SerializeField, Range(0.1f, 1f)]
        private float horizontalCoverage = 0.72f;

        [SerializeField]
        private float behindThreshold = 0.03f;

        private SpriteRenderer treeRenderer;
        private Transform player;
        private Color normalColor;

        private void Awake()
        {
            treeRenderer = GetComponent<SpriteRenderer>();
            normalColor = treeRenderer.color;
            FindPlayer();
        }

        private void LateUpdate()
        {
            if (treeRenderer == null)
            {
                return;
            }

            if (player == null)
            {
                FindPlayer();
            }

            float targetAlpha = IsPlayerBehindTree() ? hiddenAlpha : normalColor.a;
            Color color = treeRenderer.color;
            color.r = normalColor.r;
            color.g = normalColor.g;
            color.b = normalColor.b;
            color.a = Mathf.MoveTowards(color.a, targetAlpha, fadeSpeed * UnityEngine.Time.deltaTime);
            treeRenderer.color = color;
        }

        private bool IsPlayerBehindTree()
        {
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                return false;
            }

            Bounds treeBounds = treeRenderer.bounds;
            Vector3 playerPosition = player.position;
            float halfVisibleWidth = treeBounds.extents.x * horizontalCoverage;

            bool insideTreeWidth = Mathf.Abs(playerPosition.x - treeBounds.center.x) <= halfVisibleWidth;
            bool behindTrunk = playerPosition.y > transform.position.y + behindThreshold;
            bool belowTreeTop = playerPosition.y < treeBounds.max.y;

            return insideTreeWidth && behindTrunk && belowTreeTop;
        }

        private void FindPlayer()
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                player = taggedPlayer.transform;
                return;
            }

            FullBodyPlayerSpriteAnimator playerAnimator = FindFirstObjectByType<FullBodyPlayerSpriteAnimator>();
            player = playerAnimator != null ? playerAnimator.transform.root : null;
        }

        private void OnDisable()
        {
            if (treeRenderer != null)
            {
                treeRenderer.color = normalColor;
            }
        }
    }
}
