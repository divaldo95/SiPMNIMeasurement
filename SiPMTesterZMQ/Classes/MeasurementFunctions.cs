using NationalInstruments.ModularInstruments.NIDmm;
using SiPMTesterInterface.Models;
using SiPMTesterZMQ.Enums;
using SiPMTesterZMQ.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SiPMTesterZMQ.Classes
{
    public static class MeasurementFunctions
    {
        public static void DMMResistanceMeasurement(double voltage, DMM dmm, SMU smu)
        {
            dmm.Init(); //Set everything, but do not start measurement
            smu.Init();
            smu.SetVoltage(voltage); //It will invoke the callback function with the measured values
        }

        public static void IVMeasurement(NIIVStartModel startModel, DMM dmm, SMU smu)
        {
            dmm.Init();
            smu.Init();
            smu.StartMultiPointMeasurement(startModel.Voltages);
            dmm.StartMultiPoint(startModel.Voltages.Count);
        }

        public static void WaitForAfterVoltageSet(VoltageAndCurrentMeasurementTypes type, bool firstIteration)
        {
            if (firstIteration)
            {
                //Wait some time after setting the voltage
                if (type == Enums.VoltageAndCurrentMeasurementTypes.DarkCurrent)
                {
                    Thread.Sleep(500);
                }
                else if (type == Enums.VoltageAndCurrentMeasurementTypes.LeakageCurrent)
                {
                    Thread.Sleep(1000);
                }
                else if (type == Enums.VoltageAndCurrentMeasurementTypes.ForwardResistance)
                {
                    Thread.Sleep(10);
                }
            }
            else
            {
                //Wait some time after setting the voltage
                if (type == Enums.VoltageAndCurrentMeasurementTypes.DarkCurrent)
                {
                    Thread.Sleep(300);
                }
                else if (type == Enums.VoltageAndCurrentMeasurementTypes.LeakageCurrent)
                {
                    Thread.Sleep(1000);
                }
                else if (type == Enums.VoltageAndCurrentMeasurementTypes.ForwardResistance)
                {
                    Thread.Sleep(10);
                }
            }
        }

        public static void VoltageAndCurrentMeasurement(NIVoltageAndCurrentStartModel startModel, SMU smu, bool firstIteration)
        {
            smu.Init();
            if (firstIteration)
            {
                smu.SetVoltage(startModel.FirstIteration.Voltage, startModel.FirstIteration.VoltageRange, startModel.FirstIteration.CurrentLimit, startModel.FirstIteration.CurrentLimitRange);
            
            }
            else
            {
                smu.SetVoltage(startModel.SecondIteration.Voltage, startModel.SecondIteration.VoltageRange, startModel.SecondIteration.CurrentLimit, startModel.SecondIteration.CurrentLimitRange);
            }
            MeasurementFunctions.WaitForAfterVoltageSet(startModel.MeasurementType, firstIteration);
        }
    }
}
