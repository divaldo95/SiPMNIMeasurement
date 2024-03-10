using NationalInstruments.ModularInstruments.NIDmm;
using SiPMTesterInterface.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiPMTesterZMQ.Classes
{
    public static class MeasurementFunctions
    {
        public static void DMMResistanceMeasurement(double voltage, DMM dmm, SMU smu)
        {
            dmm.Init(); //Set everything, but do not start measurement
            smu.SetVoltage(voltage); //It will invoke the callback function with the measured values
        }

        public static void IVMeasurement(NIIVStartModel startModel, DMM dmm, SMU smu)
        {
            dmm.Init();
            smu.Init();
            smu.StartMultiPointMeasurement(startModel.Voltages);
            dmm.StartMultiPoint(startModel.Voltages.Count);
        }
    }
}
