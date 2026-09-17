using UnityEngine;

namespace World
{
    /// <summary>
    /// Marker for authored special-crop geometry. It deliberately does not hide
    /// itself on Play Mode entry: the runtime bootstrap hides it only after the
    /// additive farm scene has loaded and replacement interaction objects exist.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SpecialCropEditorPreview : MonoBehaviour { }
}
