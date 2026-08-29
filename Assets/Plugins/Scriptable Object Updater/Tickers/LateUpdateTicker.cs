using UnityEngine;

namespace Plugins.Scriptable_Object_Updater.Tickers
{
    [AddComponentMenu("")]
    public class LateUpdateTicker : Ticker
    {
        private void LateUpdate()
        {
            DispatchTick();
        }
    }
}