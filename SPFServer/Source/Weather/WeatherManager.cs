using System;
using System.Linq;
using System.Threading;

namespace SPFServer
{
    public delegate void WeatherChangedHandler(WeatherType lastWeather, WeatherType newWeather);

    public sealed class WeatherManager
    {
        public bool AutoChange { get; private set; } = true;

        private const int MinTimeForChange = 600000; // 10min

        private WeatherType[] weatherTypes;

        public WeatherType CurrentWeather { get { return currentWeather; } }

        private WeatherType lastWeather, currentWeather;
    
        public event WeatherChangedHandler OnServerWeatherChanged;

        private DateTime lastChanged;

        private Timer updateTimer;

        internal WeatherManager(WeatherType initialWeather)
        {
            updateTimer = new Timer(new TimerCallback(UpdateTimerCallback), null, MinTimeForChange, Timeout.Infinite);
            weatherTypes = Enum.GetValues(typeof(WeatherType)).Cast<WeatherType>().ToArray();
            currentWeather = initialWeather;
        }

        internal WeatherManager() : this(WeatherType.Clear)
        { }

        /*public void SetAllowedWeatherTypes(params WeatherType[] weatherTypes)
        {
            this.weatherTypes = weatherTypes;
        }*/

        internal WeatherType GetRandomWeatherType(WeatherType[] allowedTypes)
        {
            for (int i = 0; i < allowedTypes.Length; i++)
            {
                var rdm = new Random();
                var num1 = rdm.Next(0, 9999);
                var num2 = rdm.Next(4000, 9999);

                if (num1 > num2 && weatherTypes[i] != currentWeather &&
                    allowedTypes[i] != lastWeather &&
                    DateTime.Now - lastChanged > TimeSpan.FromMilliseconds(MinTimeForChange))
                    return allowedTypes[i];
            }
            return WeatherType.Clear;
        }

        public void SetRandomWeatherType()
        {
            updateTimer.Change(0, Timeout.Infinite);
        }

        public void SetWeatherType(WeatherType weather)
        {
            lastWeather = currentWeather;
            currentWeather = weather;
            lastChanged = DateTime.Now;
            OnServerWeatherChanged?.Invoke(lastWeather, currentWeather);
        }

        internal void UpdateTimerCallback(object state)
        {
            if (!AutoChange) return;

            var weather = GetRandomWeatherType(weatherTypes);

            while (weather == currentWeather)
                weather = GetRandomWeatherType(weatherTypes);

            lastWeather = currentWeather;
            currentWeather = weather;
            lastChanged = DateTime.Now;

            OnServerWeatherChanged?.Invoke(lastWeather, currentWeather);

            updateTimer = new Timer(new TimerCallback(UpdateTimerCallback), null, MinTimeForChange, Timeout.Infinite);
        }
    }
}
