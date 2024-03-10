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
    public class RespSocket
    {
        private ResponseSocket responseSocket;

        public RespSocket()
        {
            
        }

        public string ReceiveFrameString()
        {
            if (responseSocket != null)
            {
                string outString = "";
                responseSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(5000), out outString);
                return outString;
            }
            else 
            { 
                return "";
            }
        }

        public void SendFrame(string frame)
        {
            if (responseSocket == null)
            {
                return;
            }
            responseSocket.SendFrame(frame);
        }

        public void Start()
        {
            if (responseSocket != null)
            {
                return;
            }

            responseSocket = new ResponseSocket("tcp://*:5555");

            var serverPair = KeyReader.ReadKeyFiles("RESPPrivate.key", "RESPPublic.key");
            responseSocket.Options.CurveServer = true;
            responseSocket.Options.CurveCertificate = serverPair;
        }

        public void Stop()
        {
            if (responseSocket != null)
            {
                responseSocket.Close();
                responseSocket.Dispose();
            }
        }
    }
}
