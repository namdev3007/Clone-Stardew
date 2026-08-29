
using Combat.Data;

namespace Combat.Interfaces
{
    public interface IDamageCallback
    {
        void OnDamageDone(Health _target, DamageInfo _info);
    }
}
