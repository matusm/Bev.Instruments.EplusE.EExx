using System;

namespace Bev.Instruments.EplusE.EExx {
    public class MeasurementValues : IComparable<MeasurementValues> {

        public DateTime TimeStamp { get; }
        public double Temperature { get; }
        public double Humidity { get; }
        public double Value3 { get; }
        public double Value4 { get; }

        // alias
        public double Value1 => Temperature;
        public double Value2 => Humidity;

        internal MeasurementValues(double temperature, double humidity, double value3, double value4) {
            TimeStamp = DateTime.UtcNow;
            Temperature = temperature;
            Humidity = humidity;
            Value3 = value3;
            Value4 = value4;
        }

        public int CompareTo(MeasurementValues other) => TimeStamp.CompareTo(other.TimeStamp);

        public override string ToString() => $"MeasurementValues[TimeStamp={TimeStamp} Value1={Value1} Value2={Value2} Value3={Value3} Value4={Value4}]";

    }
}
