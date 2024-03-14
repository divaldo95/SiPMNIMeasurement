using NetMQ;
using NetMQ.Sockets;
using SiPMTesterZMQ.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiPMTesterZMQ.Classes
{
    public class PubSocket
    {
        public PublisherSocket pubSocket;
        public bool Started = false;

        public PubSocket()
        {

        }

        public void Start()
        {
            if (pubSocket == null)
            {
                pubSocket = new PublisherSocket();
            }
            var pubServerPair = KeyReader.ReadKeyFiles("PUBPrivate.key", "PUBPublic.key");
            pubSocket.Options.SendHighWatermark = 1000;
            pubSocket.Options.CurveServer = true;
            pubSocket.Options.CurveCertificate = pubServerPair;
            pubSocket.Bind("tcp://*:5557");
            Started = true;
        }

        public void Stop()
        {
            if (pubSocket != null)
            {
                pubSocket.Close();
                pubSocket.Dispose();
                Started = false;
            }
        }

        public void PublishMessage(string message)
        {
            if (pubSocket != null && Started)
            {
                pubSocket.SendFrame(message);
            }
        }
    }
}
