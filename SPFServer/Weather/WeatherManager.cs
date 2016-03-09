using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace SPFServer.Weather
{
    public delegate void WeatherChangedHandler(WeatherType lastWeather, WeatherType newWeather);

    public class WeatherManager
    {
        public const int MinTimeForChange = 600000; // 10min

        private DateTime lastChanged;

        private Timer updateTimer;

        private WeatherType[] weatherTypes;

        private WeatherType lastWeather, currentWeather;

        public WeatherType CurrentWeather { get { return currentWeather; } }

        public event WeatherChangedHandler OnServerWeatherChanged;

        public WeatherManager(WeatherType initialWeather)
        {
            updateTimer = new Timer(new TimerCallback(UpdateTimerCallback), null, MinTimeForChange, Timeout.Infinite);
            weatherTypes = Enum.GetValues(typeof(WeatherType)).Cast<WeatherType>().ToArray();
            currentWeather = initialWeather;
        }

        public WeatherManager() : this(WeatherType.Clear)
        { }

        public void SetAllowedWeatherTypes(params WeatherType[] weatherTypes)
        {
            this.weatherTypes = weatherTypes;
        }

        private WeatherType GetRandomWeatherType(WeatherType[] allowedTypes)
        {
            for (int i = 0; i < allowedTypes.Length; i++)
            {
                var rdm = new Random();
                var num1 = rdm.Next(0, 9999);
                var num2 = rdm.Next(4000, 9999);

                if (num1 > num2 && weatherTypes[i] != currentWeather &&
                    allowedTypes[i] != lastWeather &&
                    DateTime.Now - lastChanged > TimeSpan.FromMilliseconds(MinTimeForChange))
                {
                    return allowedTypes[i];
                }
            }
            return WeatherType.Clear;
        }

        public void ForceWeatherChange()
        {
            updateTimer.Change(0, Timeout.Infinite);
        }

        private void UpdateTimerCallback(object state)
        {
            var weather = GetRandomWeatherType(weatherTypes);
            lastWeather = currentWeather;
            currentWeather = weather;
            lastChanged = DateTime.Now;

            OnServerWeatherChanged?.Invoke(lastWeather, currentWeather);

            updateTimer = new Timer(new TimerCallback(UpdateTimerCallback), null, MinTimeForChange, Timeout.Infinite);
        }
    }
}
