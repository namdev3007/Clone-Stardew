using UnityEngine;

namespace Plugins.Scriptable_Object_Updater.Tickers
{
    [AddComponentMenu("")]
    public class FixedUpdateTicker : Ticker
    {
        private void FixedUpdate()
        {
            DispatchTick();
        }
    }
}
