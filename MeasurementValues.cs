using System;

namespace Bev.Instruments.EplusE.EExx {
    public class MeasurementValues : IComparable<MeasurementValues> {

        public DateTime TimeStamp { get; }
        public double Value1 { get; }
        public double Value2 { get; }
        public double Value3 { get; }
        public double Value4 { get; }

        // aliases
        public double Temperature => Value1;    // for EE03 EE07 EE08 EE894
        public double Humidity => Value2;       // for EE03 EE07 EE08 EE894
        public double Pressure => Value3;       // for EE894
        public double CO2actual => Value3;      // for EE871 EE892 EE893
        public double CO2 => Value4;            // for EE871 EE892 EE893 EE894

        internal MeasurementValues(double temperature, double humidity, double value3, double value4) {
            TimeStamp = DateTime.UtcNow;
            Value1 = temperature;
            Value2 = humidity;
            Value3 = value3;
            Value4 = value4;
        }

        public int CompareTo(MeasurementValues other) => TimeStamp.CompareTo(other.TimeStamp);

        public override string ToString() => $"MeasurementValues[TimeStamp={TimeStamp} Value1={Value1} Value2={Value2} Value3={Value3} Value4={Value4}]";

    }
}
