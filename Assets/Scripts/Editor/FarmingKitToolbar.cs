#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace FarmingKitEditor
{
    [Overlay(typeof(SceneView), "Farming Kit Toolbar", true)]
    public class FarmingKitToolbar : Overlay
    {
        private bool toggleState = false;

        public override void OnCreated()
        {
            // Ensure the overlay is pinned and visible by default
            collapsed = false;
        }

        public override VisualElement CreatePanelContent()
        {
            // Create the root container
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;

            // Add a toggle button
            var toggleButton = new Toggle("Warp Player To View On Play");
            toggleButton.value = EditorPrefs.GetBool("WarpPlayerToViewOnPlay", false);
            toggleButton.style.marginLeft = 5;
            toggleButton.RegisterValueChangedCallback(evt =>
            {
                toggleState = evt.newValue;
                EditorPrefs.SetBool("WarpPlayerToViewOnPlay", toggleState);
            });
            root.Add(toggleButton);
            return root;
        }
    }
}
#endif
