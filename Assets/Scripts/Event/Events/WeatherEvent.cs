using UnityEngine;
using Weather;

namespace Event.Events
{
    [CreateAssetMenu(menuName = "Events/Weather Event")]
    public class WeatherEvent : ScriptableEvent<EWeather>
    {

    }
}
