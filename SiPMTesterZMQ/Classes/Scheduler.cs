using SiPMTesterInterface.Classes;
using SiPMTesterInterface.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiPMTesterZMQ.Classes
{
    public class Scheduler
    {
        public MeasurementIdentifier Identifier { get; private set; }
        public int DMMResistanceMeasurementsLeft { get; private set; }
        public bool DoIVMeasurement { get; private set; }

        public string MarkAsFinishedGetNext()
        {
            DMMResistanceMeasurementsLeft--;
            if (DMMResistanceMeasurementsLeft > 0)
            {
                return "DMMRES";
            }
            else if (DoIVMeasurement)
            {
                return "IV";
            }
            else
            {
                return "SEND";
            }
        }



        public Scheduler(NIIVStartModel measurement) 
        {
            Identifier = measurement.Identifier;
        }
    }
}
