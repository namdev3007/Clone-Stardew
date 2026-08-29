using UnityEngine;

namespace Action.Actions
{
    [CreateAssetMenu(menuName = "Actions/Set Cursor Sprite")]
    public class ActionSetCursorSprite : ScriptableObject
    {
        public void Execute (Texture2D cursorTexture)
        {
            if (cursorTexture != null)
            {
                UnityEngine.Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.ForceSoftware);
            }
        }

    }
}
