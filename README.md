Bev.Instruments.EplusE.EExx
===========================

A lightweight C# library for controlling transmitters like EE03, EE07 and EE08 via the serial bus.

## Overview

The compact humidity and temperature probes EE03, EE07 and EE08 by [E+E Elektronik](https://www.epluse.com/) are modules with a digital 2-wire bus. The probes must be connected to a "HA011001 E2 to Serial" converter for the use of this class.

This library uses undocumented commands to read out the serial number and the firmware version. However it is purposefully designed to restrict any possibilities to modify probe calibration settings.

### Constructor

The constructor `EExx(string)` creates a new instance of this class taking a string as the single argument. The string is interpreted as the name of the serial port. Typical examples are `COM1` or `/dev/tty.usbserial-FTY594BQ`. 

### Methods

* `MeasurementValues GetValues()`
Gets a `MeasurementValues` object which contains properties like temperature, humidity and timestamp.
 
### Properties

With the exeption of `NeverUseCache` all properties are getters only.

* `InstrumentManufacturer`
Returns the string "E+E Elektronik".

* `InstrumentType`
Returns a string of the probe designation.

* `InstrumentSerialNumber`
Returns the unique serial number of the probe as a string.

* `InstrumentFirmwareVersion`
Returns a string for the firmware version.

* `InstrumentID`
Returns a combination of the previous properties which unambiguously identifies the instrument.

* `DevicePort`
The port name as passed to the constructor.

* `NeverUseCache`
When set to `true`, the probe specific properties like `InstrumentSerialNumber` etc. are queried each time the respective property is called. As this is a very time consuming task, the default is `false` to cache the values. When it is expected to substitute probes on the fly this should be set to `true`.

## Notes

Once instantiated, it is not possible to modify the object's `DevicePort`. However swapping instruments on the same port may work. Properties like `InstrumentID` etc. will reflect the actual instrument only when `NeverUseCache` is set to `true`.

## Usage

The following code fragment demonstrate the use of this class.

```cs
using Bev.Instruments.EplusE.EExx;
using System;

namespace PhotoPlayground
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            var probe = new EExx("COM1");

            Console.WriteLine($"Instrument: {probe.InstrumentID}");
            
            for (int i = 0; i < 10; i++)
            {
                MeasurementValues values = probe.GetValues();
                Console.WriteLine($"{i,3} : {values.Temperature:F2} °C  -  {values.Humidity:F2} %");
            }
        }
    }
}
```
## MeasurementValues Class

This is a simple container class for handling the measurement values (temperature, humidity, ...) obtained by a probe query. Once created, the values are immutable. For new measurement values one has to create a new object of this class. 

### Members

The relevant values are provided by getters

* `TimeStamp`
Returns the time of the query.

* `Temperature` (or `Value1`)
Returns the air temperature in °C.

* `Humidity` (or `Value2`)
Returns the relative humidity in %.

* `Value3`
Returns the probe specific value 3. (CO2 concentration in ppm or barometric pressure in mbar)

* `Value4`
Returns the probe specific value 4. (CO2 concentration in ppm)

Invalid values are coded as `double.NaN`. There is no public constructor, objects of this class can only by created by a call to `EExx.GetValues()`.

For diagnosic matters a `ToString()`is implemented. The class is also `IComparable`, one can sort a list of objects according to their timestamps.
