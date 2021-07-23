# Bev.Instruments.EplusE.EExx

A lightweight C# library for controlling transmitters like EE03, EE07 and EE08 via the serial bus.

## Overview

The compact humidity and temperature probes EE03, EE07 and EE08 by [E+E Elektronik](https://www.epluse.com/) are modules with a digital 2-wire bus. To use this library the probes must be connected to a "HA011001 E2 to Serial" converter. 

This library uses undocummented commands to read out serial number and firmware version. However it is purposefully designed to restrict any possibilities to modify probe calibration settings.

### Constructor

The constructor `EExx(string)` creates a new instance of this class taking a string as the single argument. The string is interpreted as the port name of the serial port. Typical examples are `COM1` or `/dev/tty.usbserial-FTY594BQ`. 

### Methods

* `MeasurementValues GetValues()`
Gets a `MeasurementValues` object which contains properties like temperature, humidity and timestamp.
 
* `UpdateValues()`
Alternative way. One can consume the relevant properties after calling this method.
 
* `ClearCache()`
Necessary for on the fly probe substitution. (not recommended)
 
### Properties

All properties are getters only.

* `InstrumentManufacturer`
Returns the string "E+E Elektronik.

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

## Notes

Once instantiated, it is not possible to modify the object's `DevicePort`. However swaping  instruments on the same port may work. Properties like `InstrumentID` etc. will reflect the actual instrument only after a call to `ClearCache()`.

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
