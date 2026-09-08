using UnityEngine;
using TMPro;

namespace World.NPC
{
    [CreateAssetMenu(fileName = "Dialogue Visual Library", menuName = "NPC/Dialogue Visual Library")]
    public sealed class DialogueVisualLibrary : ScriptableObject
    {
        [Header("Dialogue")]
        public Sprite npcDialogueBox;
        public Sprite playerDialogueBox;
        public Sprite namePlate;
        public Sprite continueIcon;
        public Sprite skipButton;
        public TMP_FontAsset textFont;

        [Header("Portraits")]
        public Sprite playerPortrait;
        public Sprite grandpaPortrait;
        public Sprite sellerPortrait;
    }
}
