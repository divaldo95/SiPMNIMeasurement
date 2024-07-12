using System;
namespace SiPMTesterInterface.Enums
{
	public enum MeasurementState
	{
        NotRunning,
        Running,
        Finished,
        FinishedIV,
        FinishedDMM,
        FinishedSPS,
        FinishedVoltageAndCurrent,
        Error,
        Unknown
    }
}

