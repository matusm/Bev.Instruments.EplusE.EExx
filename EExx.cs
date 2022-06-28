//*****************************************************************************
// 
// Library for the communication of E+E EE03, EE07, EE08 (and simmilar) 
// transmitters via serial port. The transmitter must be interfaced using the
// "HA011001 E2 to Serial" converter. This version uses undocumented commands.
// 
// Usage:
// 1.) create instance of the EExx class with the COM port name as parameter;
// 2.) you can consume properties like serial number, type designation, etc.; 
// 3.) a call to GetValues() returns a MeasurementValues object which contains
//     properties like temperature, humidity and timestamp.
// 
// Example:
//    var device = new EExx("COM1");
//    Console.Writeline(device.InstrumentID);
// 
//    var values = device.GetValues();
//    Console.Writeline($"{values.Temperature} °C");
//    Console.Writeline($"{values.Humidity} %");
// 
// Author: Michael Matus, 2021
// 
//*****************************************************************************

using System;
using System.IO.Ports;
using System.Threading;
using System.Collections.Generic;
using System.Text;

namespace Bev.Instruments.EplusE.EExx
{

    public class EExx
    {
        private readonly SerialPort comPort;            // this must not be static!
        private const string defaultString = "???";     // returned if something failed
        private const int numberOfTries = 20;           // number of tries before call gives up
        private int delayTimeForRespond = 500;          // rather long delay necessary
        private int delayTimeForRespondE2 = 50;         // specific for E2 bus calls
        private TransmitterGroup transmitterGroup;

        private string cachedInstrumentType;
        private string cachedInstrumentSerialNumber;
        private string cachedInstrumentFirmwareVersion;

        private bool humidityAvailable;
        private bool temperatureAvailable;
        private bool value3Available;
        private bool value4Available;

        public EExx(string portName)
        {
            DevicePort = portName.Trim();
            comPort = new SerialPort(DevicePort, 9600);
            comPort.RtsEnable = true;   // this is essential
            comPort.DtrEnable = true;	// this is essential
            ClearCache();
        }

        public string DevicePort { get; }
        public string InstrumentManufacturer => "E+E Elektronik";
        public string InstrumentType => GetInstrumentType();
        public string InstrumentSerialNumber => GetInstrumentSerialNumber();
        public string InstrumentFirmwareVersion => GetInstrumentVersion();
        public string InstrumentID => $"{InstrumentType} v{InstrumentFirmwareVersion} SN:{InstrumentSerialNumber} @ {DevicePort}";
        public bool NeverUseCache { get; set; } = false;

        private double Temperature { get; set; }
        private double Humidity { get; set; }
        private double Value3 { get; set; }
        private double Value4 { get; set; }

        public MeasurementValues GetValues()
        {
            UpdateValues();
            return new MeasurementValues(Temperature, Humidity, Value3, Value4);
        }

        private void ClearCache()
        {
            cachedInstrumentType = defaultString;
            cachedInstrumentSerialNumber = defaultString;
            cachedInstrumentFirmwareVersion = defaultString;
            transmitterGroup = TransmitterGroup.Unknown;
            ClearCachedValues();
        }

        private void ClearCachedValues()
        {
            Temperature = double.NaN;
            Humidity = double.NaN;
            Value3 = double.NaN;
            Value4 = double.NaN;
            temperatureAvailable = false;
            humidityAvailable = false;
            value4Available = false;
            value3Available = false;
        }

        private void UpdateValues()
        {
            // E2 bus complient
            // E2Interface-RS232_englisch.pdf
            // Specification_E2_Interface.pdf
            if (transmitterGroup == TransmitterGroup.EE03)
                Thread.Sleep(300); // workaround for the EE03
            if (transmitterGroup == TransmitterGroup.EE08)
                Thread.Sleep(300); // workaround for the EE08
            ClearCachedValues();
            // UpdateValuesUndocumented(); return; // uncomment for testing alternative Update
            GetAvailableValues();
            if (humidityAvailable)
            {
                var humLowByte = QueryE2(0x81);
                var humHighByte = QueryE2(0x91);
                if (humLowByte.HasValue && humHighByte.HasValue)
                    Humidity = (humLowByte.Value + humHighByte.Value * 256.0) / 100.0; // in %
            }
            if (temperatureAvailable)
            {
                var tempLowByte = QueryE2(0xA1);
                var tempHighByte = QueryE2(0xB1);
                if (tempLowByte.HasValue && tempHighByte.HasValue)
                    Temperature = (tempLowByte.Value + tempHighByte.Value * 256.0) / 100.0 - 273.15; // in °C
            }
            if (value3Available)
            {
                var value3LowByte = QueryE2(0xC1);
                var value3HighByte = QueryE2(0xD1);
                if (value3LowByte.HasValue && value3HighByte.HasValue)
                    Value3 = value3LowByte.Value + value3HighByte.Value * 256.0; // in ppm or mbar
                if (transmitterGroup == TransmitterGroup.EE894)
                {
                    Value3 *= 0.1; // ambient pressure in mbar (hPa)
                }
            }
            if (value4Available)
            {
                var value4LowByte = QueryE2(0xE1);
                var value4HighByte = QueryE2(0xF1);
                if (value4LowByte.HasValue && value4HighByte.HasValue)
                    Value4 = value4LowByte.Value + value4HighByte.Value * 256.0; // in ppm
            }
            byte? statusByte = QueryE2(0x71);
            if (statusByte != 0x00)
                ClearCachedValues();
        }

        private string GetInstrumentType()
        {
            if (cachedInstrumentType == defaultString || NeverUseCache)
                cachedInstrumentType = RepeatMethod(_GetInstrumentType);
            return cachedInstrumentType;
        }

        private string GetInstrumentVersion()
        {
            if (cachedInstrumentFirmwareVersion == defaultString || NeverUseCache)
            {
                GetInstrumentType(); // to make sure transmitterGroup is set
                cachedInstrumentFirmwareVersion = RepeatMethod(_GetInstrumentVersionUndocumented);
            }
            return cachedInstrumentFirmwareVersion;
        }

        private string GetInstrumentSerialNumber()
        {
            if (cachedInstrumentSerialNumber == defaultString || NeverUseCache)
            {
                GetInstrumentType(); // to make sure transmitterGroup is set
                cachedInstrumentSerialNumber = RepeatMethod(_GetInstrumentSerialNumberUndocumented);
            }
            return cachedInstrumentSerialNumber;
        }

        private void GetAvailableValues()
        {
            // E2 bus complient
            var bitPattern = QueryE2(0x31);
            if (bitPattern is byte bits)
            {
                humidityAvailable = IsBitSetInByte(bits, 0);
                temperatureAvailable = IsBitSetInByte(bits, 1);
                value3Available = IsBitSetInByte(bits, 2);
                value4Available = IsBitSetInByte(bits, 3);
                // this is for the EE08 with 0x21
                if (bits == 0x21)
                {
                    temperatureAvailable = true;
                }
            }
        }

        private void UpdateValuesUndocumented()
        {
            // this works for temperature and humidity only
            ClearCachedValues();
            // see E2Interface-RS232_e1.doc
            if (transmitterGroup == TransmitterGroup.EE07 || transmitterGroup == TransmitterGroup.EE08)
            {
                var reply = Query(0x58, new byte[] { 0x00, 0x30, 0x1E });
                if (reply.Length != 5)
                {
                    return; // we need exactly 5 bytes
                }
                if (reply[4] != 0x00)
                {
                    return; // if status gives an error, return
                }
                Humidity = (reply[0] + reply[1] * 256.0) / 100.0;
                Temperature = (reply[2] + reply[3] * 256.0) / 100.0 - 273.15;
            }
            if (transmitterGroup == TransmitterGroup.EE03)
            {
                var reply = Query(0x58, new byte[] { 0x00, 0x30, 0x3C });
                if (reply.Length != 5)
                {
                    return; // we need exactly 5 bytes
                }
                if (reply[4] != 0x00)
                {
                    return; // if status gives an error, return
                }
                Humidity = (reply[0] + reply[1] * 256.0) / 100.0;
                Temperature = (reply[2] + reply[3] * 256.0) / 100.0 - 273.15;
            }
        }

        private string _GetInstrumentType()
        {
            // E2 bus complient
            transmitterGroup = TransmitterGroup.Unknown;
            byte? groupLowByte = QueryE2(0x11);
            if (!groupLowByte.HasValue)
                return defaultString;

            if (groupLowByte == 0x55 || groupLowByte == 0xFF)
                return defaultString;

            byte? subGroupByte = QueryE2(0x21);
            if (!subGroupByte.HasValue)
                return defaultString;

            byte? groupHighByte = QueryE2(0x41);
            if (!groupHighByte.HasValue)
                return defaultString;

            if (groupHighByte == 0x55 || groupHighByte == 0xFF)
                groupHighByte = 0x00;

            int productSeries = groupHighByte.Value * 256 + groupLowByte.Value;
            int outputType = (subGroupByte.Value >> 4) & 0x0F;
            int ftType = subGroupByte.Value & 0x0F;
            transmitterGroup = IdentifyTransmitterGroup(productSeries);
            string typeAsString = "EE";
            if (productSeries >= 100)
                typeAsString += $"{productSeries}";
            else
                typeAsString += $"{productSeries:00}";
            if (outputType != 0)
                typeAsString += $"-{outputType}";
            typeAsString += $" FT{ftType}";
            return typeAsString;
        }

        private TransmitterGroup IdentifyTransmitterGroup(int series)
        {
            if (series == 3) return TransmitterGroup.EE03;
            if (series == 7) return TransmitterGroup.EE07;
            if (series == 8) return TransmitterGroup.EE08;
            if (series == 871) return TransmitterGroup.EE871;
            if (series == 892) return TransmitterGroup.EE892;
            if (series == 893) return TransmitterGroup.EE893;
            if (series == 894) return TransmitterGroup.EE894;
            return TransmitterGroup.Unknown;
        }

        private string _GetInstrumentVersionUndocumented()
        {
            // undocumented!
            if (transmitterGroup == TransmitterGroup.EE03)
            {
                _ = Query(0x50, new byte[] { 0x80, 0x00, 0x40 });
                byte[] reply = Query(0x55, new byte[] { 0x01, 0x44, 0x01 });
                if (reply.Length != 1)
                    return defaultString;
                string str = $"{reply[0]}.00";
                return str;
            }
            if (transmitterGroup == TransmitterGroup.EE07)
            {
                _ = Query(0x50, new byte[] { 0x80, 0x00, 0x40 });
                byte[] reply = Query(0x55, new byte[] { 0x01, 0x80, 0x04 });
                if (reply.Length != 4)
                    return defaultString;
                string str = Encoding.UTF8.GetString(reply);
                str = str.Insert(2, ".");
                str = str.TrimStart('0');
                return str;
            }
            if (transmitterGroup == TransmitterGroup.EE08)
            {
                byte[] reply = Query(0x55, new byte[] { 0x51, 0x00, 0x02 });
                if (reply.Length != 2)
                    return defaultString;
                string str = $"{reply[0]}.{reply[1]:D2}";
                return str;
            }
            if (transmitterGroup == TransmitterGroup.EE894)
            {
                byte[] reply = Query(0x55, new byte[] { 0x51, 0x00, 0x02 });
                if (reply.Length != 2)
                    return defaultString;
                string str = $"{reply[0]}.{reply[1]:D2}";
                return str;
            }

            return defaultString;
        }

        private string _GetInstrumentSerialNumberUndocumented()
        {
            // undocumented!
            byte[] reply = { };
            if (transmitterGroup == TransmitterGroup.EE03)
            {
                _ = Query(0x50, new byte[] { 0x80, 0x00, 0x40 });   // TODO check function of this line!
                reply = Query(0x55, new byte[] { 0x01, 0x70, 0x10 }, 2 * delayTimeForRespond);
            }
            if (transmitterGroup == TransmitterGroup.EE07)
            {
                _ = Query(0x50, new byte[] { 0x80, 0x00, 0x40 });   // TODO check function of this line!
                reply = Query(0x55, new byte[] { 0x01, 0x84, 0x10 }, 2 * delayTimeForRespond);
            }
            if (transmitterGroup == TransmitterGroup.EE08)
            {
                reply = Query(0x55, new byte[] { 0x51, 0xa0, 0x10 }, 2 * delayTimeForRespond);
            }
            if (transmitterGroup == TransmitterGroup.EE894)
            {
                _ = Query(0x50, new byte[] { 0x80, 0x00, 0x40 });   // TODO check function of this line!
                reply = Query(0x55, new byte[] { 0x51, 0xa0, 0x10 }, 2 * delayTimeForRespond);

            }
            if (reply.Length == 0)
                return defaultString;
            for (int i = 0; i < reply.Length; i++)
            {
                if (reply[i] == 0) reply[i] = 0x20; // substitute 0 by space
                if (reply[i] == 0xFF) reply[i] = 0x20; // substitute FF by space
            }
            return Encoding.UTF8.GetString(reply).Trim();
        }

        private byte[] Query(byte instruction, byte[] DField, int delayTime)
        {
            OpenPort();
            SendSerialBus(ComposeCommand(instruction, DField));
            Thread.Sleep(delayTime);
            var buffer = ReadSerialBus();
            return AnalyzeRespond(buffer);
        }

        private byte[] Query(byte instruction, byte[] DField)
        {
            return Query(instruction, DField, delayTimeForRespond);
        }

        private byte? QueryE2(byte address)
        {
            var reply = Query(0x51, new byte[] { address }, delayTimeForRespondE2);
            if (reply.Length == 1)
            {
                return reply[0];
            }
            return null;
        }

        private byte[] ComposeCommand(byte BField, byte[] DField)
        {
            List<byte> bufferList = new List<byte>();
            bufferList.Add(BField); // [B]
            if (DField == null || DField.Length == 0)
                bufferList.Add((byte)0);
            else
            {
                bufferList.Add((byte)DField.Length); // [L]
                foreach (byte b in DField)
                    bufferList.Add(b); // [D]
            }
            byte bsum = 0;
            foreach (byte b in bufferList)
                bsum += b;
            bufferList.Add((byte)bsum); // [C]
            return bufferList.ToArray();
        }

        private byte[] AnalyzeRespond(byte[] buffer)
        {
            // This method takes the return byte array, checks if [L] is consistent,
            // if [S] is ACK and if the [CRC] is ok.
            // If so [C], [L], [S], [Sd] and [CRC] is stripped and the remaining array returned.
            var syntaxError = Array.Empty<byte>();
            if (buffer.Length < 5 || buffer == null)
            {
                // response too short
                return syntaxError;
            }
            // check CRC [C]
            byte bsum = 0;
            for (int i = 0; i < buffer.Length - 1; i++)
                bsum += buffer[i];
            if (bsum != buffer[buffer.Length - 1])
            {
                // CRC failed
                return syntaxError;
            }
            // check ACK
            if (buffer[2] != 0x06)
            {
                //TODO this is useless!
                if (buffer[2] == 0x15)
                {
                    // NAK
                    return syntaxError;
                }
                else
                {
                    // neither ACK nor NAK
                    return syntaxError;
                }
            }
            // check count of data bytes
            if (buffer[1] + 3 != buffer.Length)
            {
                return syntaxError;
            }
            byte[] tempbuff = new byte[buffer.Length - 5];
            for (int i = 4; i < buffer.Length - 1; i++)
                tempbuff[i - 4] = buffer[i];
            return tempbuff;
        }

        private void OpenPort()
        {
            try
            {
                if (!comPort.IsOpen)
                    comPort.Open();
            }
            catch (Exception)
            { }
        }

        private void ClosePort()
        {
            try
            {
                if (comPort.IsOpen)
                {
                    comPort.Close();
                }
            }
            catch (Exception)
            { }
        }

        private void SendSerialBus(byte[] command)
        {
            try
            {
                comPort.Write(command, 0, command.Length);
                return;
            }
            catch (Exception)
            {
            }
        }

        private byte[] ReadSerialBus()
        {
            byte[] ErrBuffer = { 0xFF };
            try
            {
                byte[] buffer = new byte[comPort.BytesToRead];
                comPort.Read(buffer, 0, buffer.Length);
                return buffer;
            }
            catch (Exception)
            {
                return ErrBuffer;
            }
        }

        private string RepeatMethod(Func<string> getString)
        {
            for (int i = 0; i < numberOfTries; i++)
            {
                string str = getString();
                if (str != defaultString)
                {
                    return str;
                }
            }
            return defaultString;
        }

        private bool IsBitSetInByte(byte bitPattern, int place)
        {
            if (place < 0)
                return false;
            if (place >= 8)
                return false;
            var b = (bitPattern >> place) & 0x01;
            if (b == 0x00)
                return false;
            return true;
        }

        // function for debbuging purposes
        private string BytesToString(byte[] bytes)
        {
            string str = "";
            foreach (byte b in bytes)
                str += $" {b,2:X2}";
            return str;
        }

    }

    internal enum TransmitterGroup
    {
        Unknown,
        EE03,
        EE07,
        EE08,
        EE871,
        EE892,
        EE893,
        EE894
    }

}
