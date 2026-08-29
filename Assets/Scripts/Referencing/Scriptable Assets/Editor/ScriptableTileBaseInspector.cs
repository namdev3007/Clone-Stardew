using UnityEditor;

namespace Referencing.Scriptable_Assets.Editor
{
    /// <summary>
    /// Editor functionality prevents duplicate guids.
    /// </summary>
    [CustomEditor(typeof(ScriptableTileBase), true)]
    public class ScriptableTileBaseInspector : UnityEditor.Editor
    {
        //private static ScriptableAsset currentTarget;
        private string currentGUID;

        public override void OnInspectorGUI()
        {
            if (string.IsNullOrEmpty(currentGUID))
            {
                currentGUID = $"GUID: {((IReferenceableAsset)target).GetGuid()}";
            }

            EditorGUILayout.TextArea(currentGUID, EditorStyles.centeredGreyMiniLabel);

            base.DrawDefaultInspector();
        }
    }
}
