using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace SiPMTesterInterface.Enums
{
    /*
    public static class MeasurementTypeEnumHelper
	{
        public static string GetEnumMemberValue(MeasurementType value)
        {
            switch (value)
            {
                case MeasurementType.IVMeasurement:
                    return "IVMeasurement";
                case MeasurementType.DMMResistanceMeasurement:
                    return "DMMMeasurement";
                case MeasurementType.SPSMeasurement:
                    return "SPSMeasurement";
                case MeasurementType.Unknown:
                    return "Unknown";
                default:
                    throw new ArgumentException($"Unhandled enum value: {value}");
            }
        }

        public static MeasurementType ParseEnum(string value)
        {
            switch (value)
            {
                case "IVMeasurement":
                    return MeasurementType.IVMeasurement;
                case "DMMMeasurement":
                    return MeasurementType.DMMResistanceMeasurement;
                case "SPSMeasurement":
                    return MeasurementType.SPSMeasurement;
                case "Unknown":
                    return MeasurementType.Unknown;
                default:
                    throw new ArgumentException($"Unknown enum value: {value}");
            }
        }
    }
    */

    //[JsonConverter(typeof(StringEnumConverter))]
    public enum MeasurementType
	{
        IVMeasurement = 0,
        SPSMeasurement = 1,
        DMMResistanceMeasurement = 2,
        VoltageAndCurrentMeasurement = 3,
        Unknown = 999
    }
}

