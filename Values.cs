using System;

namespace Bev.Instruments.EplusE.EExx
{
    public class Values
    {
        public double Temperature { get; }
        public double Humidity { get; }
        public DateTime TimeStamp { get; }

        public Values(double temperature, double humidity)
        {
            TimeStamp = DateTime.UtcNow;
            Temperature = temperature;
            Humidity = humidity;
        }

    }
}
