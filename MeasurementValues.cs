using System;

namespace Bev.Instruments.EplusE.EExx
{
    public class MeasurementValues
    {
        public double Temperature { get; }
        public double Humidity { get; }
        public DateTime TimeStamp { get; }

        public MeasurementValues(double temperature, double humidity)
        {
            TimeStamp = DateTime.UtcNow;
            Temperature = temperature;
            Humidity = humidity;
        }

    }
}
