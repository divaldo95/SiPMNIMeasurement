using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiPMTesterZMQ.Models
{
    public class SMUVoltageModel
    {
        public double Voltage { get; set; }
        public double CurrentLimit { get; set; }
        public double CurrentLimitRange { get; set; }
        public double VoltageRange { get; set; }
        public int Iterations { get; set; }
    }
}
