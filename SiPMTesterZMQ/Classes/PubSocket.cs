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
        }

        public void Stop()
        {
            if (pubSocket != null)
            {
                pubSocket.Close();
                pubSocket.Dispose();
            }
        }

        public void PublishMessage(string message)
        {
            if (pubSocket != null)
            {
                pubSocket.SendFrame(message);
            }
        }
    }
}
