using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiPMTesterZMQ.Models
{
    public class GenericResponseModel
    {
        public string Sender { get; set; } = "";
        public JObject jsonObject { get; set; } = new JObject();

        public GenericResponseModel()
        {

        }

        public GenericResponseModel(string s, JObject obj)
        {
            Sender = s;
            jsonObject = obj;
        }
    }
}
