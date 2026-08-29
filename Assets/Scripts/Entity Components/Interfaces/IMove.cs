using UnityEngine;

namespace Entity_Components.Interfaces
{
    public interface IMove
    {
        void OnMove(Vector2 direction, float velocity);
    }
}
