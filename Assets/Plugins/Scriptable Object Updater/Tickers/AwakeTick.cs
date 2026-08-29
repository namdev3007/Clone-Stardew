using UnityEngine;

namespace Plugins.Scriptable_Object_Updater.Tickers
{
    [AddComponentMenu("")]
    public class AwakeTick : Ticker
    {
        public void Awake()
        {
            DispatchTick();
        }
    }
}