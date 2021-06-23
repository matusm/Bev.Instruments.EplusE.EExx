//*****************************************************************************
// 
// Library for the communication of E+E EE03, EE07, EE08 (and simmilar) 
// transmitters via serial port. The transmitter must be interfaced using the
// HA011001 E2 to serial converter. This version uses undocumented commands.
// 
// Usage:
// 1.) create instance of the EExx class with the COM port as parameter;
// 2.) you can consume properties like serial number, type designation, etc.; 
// 3.) a call to GetValues() returns a MeasurementValues object which contains
//     properties like temperature, humidity and timestamp
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
        private int delayTimeForRespondE2 = 45;         // specific for E2 bus calls
        private const int waitOnClose = 50;             // No actual value is given, experimental
        private bool avoidPortClose = true;

        private string cachedInstrumentType;
        private string cachedInstrumentSerialNumber;
        private string cachedInstrumentFirmwareVersion;

        private bool humidityAvailable;
        private bool temperatureAvailable;
        private bool airVelocityAvailable;
        private bool co2Available;

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
        public string InstrumentID => $"{InstrumentType} {InstrumentFirmwareVersion} SN:{InstrumentSerialNumber} @ {DevicePort}";

        public double Temperature { get; private set; }
        public double Humidity { get; private set; }
        public double Value3 { get; private set; }
        public double Value4 { get; private set; }

        public bool NeverUseCache { get; set; } = false;

        public MeasurementValues GetValues()
        {
            UpdateValues();
            return new MeasurementValues(Temperature, Humidity, Value3, Value4);
        }
        
        public void UpdateValues()
        {
            // E2 bus complient
            // E2Interface-RS232_englisch.pdf
            // Specification_E2_Interface.pdf
            ClearCachedValues();
            GetAvailableValues();
            if (humidityAvailable)
            {
                var humLowByte = QueryE2(0x81);
                var humHighByte = QueryE2(0x91);
                if (humLowByte.HasValue && humHighByte.HasValue)
                    Humidity = (humLowByte.Value + humHighByte.Value * 256.0) / 100.0;
            }
            if (temperatureAvailable)
            {
                var tempLowByte = QueryE2(0xA1);
                var tempHighByte = QueryE2(0xB1);
                if (tempLowByte.HasValue && tempHighByte.HasValue)
                    Temperature = (tempLowByte.Value + tempHighByte.Value * 256.0) / 100.0 - 273.15;
            }
            if (co2Available || airVelocityAvailable)
            {
                var value3LowByte = QueryE2(0xC1);
                var value3HighByte = QueryE2(0xD1);
                var value4LowByte = QueryE2(0xE1);
                var value4HighByte = QueryE2(0xD1);
                if (value3LowByte.HasValue && value3HighByte.HasValue)
                    Value3 = value3LowByte.Value + value3HighByte.Value * 256.0;
                if (value4LowByte.HasValue && value4HighByte.HasValue)
                    Value4 = value4LowByte.Value + value4HighByte.Value * 256.0;
            }
            byte? statusByte = QueryE2(0x71);
            if (statusByte != 0x00)
                ClearCachedValues();
        }
        
        public void ClearCache()
        {
            cachedInstrumentType = defaultString;
            cachedInstrumentSerialNumber = defaultString;
            cachedInstrumentFirmwareVersion = defaultString;
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
            co2Available = false;
            airVelocityAvailable = false;
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
                cachedInstrumentFirmwareVersion = RepeatMethod(_GetInstrumentVersionUndocumented);
            return cachedInstrumentFirmwareVersion;
        }

        private string GetInstrumentSerialNumber()
        {
            if (cachedInstrumentSerialNumber == defaultString || NeverUseCache)
                cachedInstrumentSerialNumber = RepeatMethod(_GetInstrumentSerialNumberUndocumented);
            return cachedInstrumentSerialNumber;
        }

        private void GetAvailableValues()
        {
            // E2 bus complient
            var bitPattern = QueryE2(0x31);
            if (bitPattern is byte bits)
            {
                humidityAvailable = BitIsSet(bits, 0);
                temperatureAvailable = BitIsSet(bits, 1);
                airVelocityAvailable = BitIsSet(bits, 2);
                co2Available = BitIsSet(bits, 3);
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

        private string _GetInstrumentType()
        {
            // E2 bus complient
            byte? groupLowByte = QueryE2(0x11);
            if (!groupLowByte.HasValue)
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

        private string _GetInstrumentVersionUndocumented()
        {
            // undocumented!
            var reply = Query(0x55, new byte[] { 0x01, 0x80, 0x04 });
            if (reply.Length != 4)
                return defaultString;
            var str = Encoding.UTF8.GetString(reply);
            str = str.Insert(2, ".");
            str = str.TrimStart('0');
            return str;
        }

        private string _GetInstrumentSerialNumberUndocumented()
        {
            // undocumented!
            var reply = Query(0x55, new byte[] { 0x01, 0x84, 0x10 }, 2 * delayTimeForRespond);
            if (reply.Length == 0)
                return defaultString;
            for (int i = 0; i < reply.Length; i++)
            {
                if (reply[i] == 0) reply[i] = 0x20; // substitute 0 by space
            }
            return Encoding.UTF8.GetString(reply).Trim();
        }

        private byte[] Query(byte instruction, byte[] DField, int delayTime)
        {
            OpenPort();
            SendSerialBus(ComposeCommand(instruction, DField));
            Thread.Sleep(delayTime);
            var buffer = ReadSerialBus();
            ClosePort();
            return AnalyzeRespond(buffer);
        }

        private byte[] Query(byte instruction, byte[] DField)
        {
            return Query(instruction, DField, delayTimeForRespond);
        }

        private byte? QueryE2(byte address)
        {
            var reply = Query(0x51, new byte[] { address }, delayTimeForRespondE2);
            if(reply.Length == 1)
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
            if (avoidPortClose)
                return;
            try
            {
                if (comPort.IsOpen)
                {
                    comPort.Close();
                    Thread.Sleep(waitOnClose);
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
                //Console.WriteLine("***** SendEE07 failed: ", e);
                return;
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
                // Console.WriteLine("***** ReadEE07 failed: ", e);
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
                    //if (i > 0) Console.WriteLine($"***** {i + 1} tries!");
                    return str;
                }
            }
            //Console.WriteLine($"***** {numberTries}, unsuccessfull!");
            return defaultString;
        }

        private bool BitIsSet(byte bitPattern, int place)
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

}
