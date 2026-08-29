using System.Collections.Generic;
using UnityEngine;

namespace Plugins.Scriptable_Object_Updater.Tickers
{
    [AddComponentMenu("")]
    public class Ticker : MonoBehaviour
    {
        public List<UpdateableEvent> Events = new List<UpdateableEvent>();

        protected void DispatchTick()
        {
            for (int i = 0; i < Events.Count; i++)
            {
                Events[i].Tick();
            }
        }
    }
}