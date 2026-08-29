using UnityEngine;

namespace Plugins.Scriptable_Object_Updater.Tickers
{
    [AddComponentMenu("")]
    public class StartTick : Ticker
    {
        public void Start()
        {
            DispatchTick();
        }
    }
}