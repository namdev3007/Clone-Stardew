using UnityEngine;

namespace Combat.Data
{
    public struct DamageVolumeConfiguration
    {
        public float Damage;
        public float ActiveTime;
        public string[] TargetTags;
        public GameObject Owner;
        public bool AllowDuplicateDamage;
        public bool CanDamageMultiple;
        public Vector2 Size;
    }
}