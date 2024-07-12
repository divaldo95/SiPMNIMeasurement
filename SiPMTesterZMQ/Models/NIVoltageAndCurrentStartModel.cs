using SiPMTesterInterface.Classes;
using SiPMTesterInterface.Models;
using SiPMTesterZMQ.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiPMTesterZMQ.Models
{
    public class NIVoltageAndCurrentStartModel
    {
        public MeasurementIdentifier Identifier { get; set; } = new MeasurementIdentifier();
        public VoltageAndCurrentMeasurementTypes MeasurementType { get; set; } = VoltageAndCurrentMeasurementTypes.Unknown;
        public SMUVoltageModel FirstIteration { get; set; } = new SMUVoltageModel();
        public SMUVoltageModel SecondIteration { get; set; } = new SMUVoltageModel();
    }
}
