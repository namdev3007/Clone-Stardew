using System;
using UnityEngine;
using World;

namespace Event.Events
{
    [CreateAssetMenu(menuName = "Events/Warp Event")]
    public class WarpEvent : ScriptableEvent<WarpLocation>
    {
        internal void AddListener(WarpEvent onWarpRequest)
        {
            throw new NotImplementedException();
        }
    }
}
