using UnityEditor;
using UnityEngine;

namespace GameSystem.Systems.Editor
{
    [CustomEditor(typeof(SaveSystem))]
    public class SaveSystemInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Go to persistent data folder"))
            {
                EditorUtility.OpenWithDefaultApp( Application.persistentDataPath);
            }
        }
    }
}
