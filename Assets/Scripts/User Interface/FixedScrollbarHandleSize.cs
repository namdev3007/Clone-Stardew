using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Scrollbar))]
[DefaultExecutionOrder(10000)]
public sealed class FixedScrollbarHandleSize : MonoBehaviour
{
    [SerializeField, Min(1f)] private float fixedHandleHeight = 50f;

    private Scrollbar scrollbar;

    private void OnEnable()
    {
        scrollbar = GetComponent<Scrollbar>();
        ApplyFixedSize();
    }

    private void OnValidate()
    {
        ApplyFixedSize();
    }

    private void LateUpdate()
    {
        ApplyFixedSize();
    }

    private void ApplyFixedSize()
    {
        if (scrollbar == null)
            scrollbar = GetComponent<Scrollbar>();

        RectTransform handle = scrollbar.handleRect;
        if (handle == null || handle.parent is not RectTransform slidingArea)
            return;

        float trackHeight = slidingArea.rect.height;
        if (trackHeight <= 0f)
            return;

        float fixedNormalizedSize = Mathf.Clamp01(fixedHandleHeight / trackHeight);
        if (!Mathf.Approximately(scrollbar.size, fixedNormalizedSize))
            scrollbar.size = fixedNormalizedSize;
    }
}
