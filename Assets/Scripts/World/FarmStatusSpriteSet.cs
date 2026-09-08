using UnityEngine;

namespace World
{
    [CreateAssetMenu(fileName = "Farm Status Sprites", menuName = "Farming/Status Sprite Set")]
    public class FarmStatusSpriteSet : ScriptableObject
    {
        [SerializeField] private Sprite fertilize;
        [SerializeField] private Sprite sowSeed;
        [SerializeField] private Sprite water;
        [SerializeField] private Sprite harvest;

        public Sprite Fertilize => fertilize;
        public Sprite SowSeed => sowSeed;
        public Sprite Water => water;
        public Sprite Harvest => harvest;
    }
}
