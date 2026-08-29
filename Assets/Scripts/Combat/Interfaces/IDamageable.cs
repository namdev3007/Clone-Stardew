
using Combat.Data;

namespace Combat.Interfaces
{
    public interface IDamageable
    {
        void OnDamaged(DamageInfo _damageInfo);
    }
}