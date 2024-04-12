using Microsoft.Win32;
using NetMQ;
using NetMQ.Sockets;
using SiPMTesterZMQ.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SiPMTesterZMQ.Classes
{
    public class SubSocketPeriodicUpdateElapsed : EventArgs
    {
        public SubSocketPeriodicUpdateElapsed() 
        { 

        }
    }

    public class PubSocket
    {
        private PublisherSocket pubSocket;
        private object _lockObj = new object();
        private System.Timers.Timer statusTimer;
        public bool Started = false;
        public EventHandler<SubSocketPeriodicUpdateElapsed> OnPeriodicUpdateTimeElapsed;

        public PubSocket()
        {

        }

        public void Start()
        {
            if (pubSocket == null || pubSocket.IsDisposed)
            {
                pubSocket = new PublisherSocket();
            }
            if (statusTimer == null)
            {
                statusTimer = new System.Timers.Timer(5000);
                statusTimer.Start();
            }
            statusTimer.Elapsed += periodicStatusUpdates;
            var pubServerPair = KeyReader.ReadKeyFiles("PUBPrivate.key", "PUBPublic.key");
            pubSocket.Options.SendHighWatermark = 1000;
            pubSocket.Options.CurveServer = true;
            pubSocket.Options.CurveCertificate = pubServerPair;
            pubSocket.Bind("tcp://*:5557");
            Started = true;
        }

        private void periodicStatusUpdates(object sender, System.Timers.ElapsedEventArgs e)
        {
            string msg = "[STATUS]Alive:{\"Status:\": 1}\"";
            PublishMessage(msg);
            OnPeriodicUpdateTimeElapsed?.Invoke(this, new SubSocketPeriodicUpdateElapsed()); //if anyone needs to send periodic updates
        }

        public void Stop()
        {
            if (statusTimer != null)
            {
                statusTimer.Stop();
                statusTimer.Dispose();
                statusTimer = null;
            }
            if (pubSocket != null)
            {
                string msg = "[STATUS]Exiting:{\"Status:\": 1}\"";
                PublishMessage(msg);
                pubSocket.Close();
                pubSocket.Dispose();
                Started = false;
            }
            
        }

        public void PublishMessage(string message)
        {
            if (pubSocket != null && Started)
            {
                lock(_lockObj)
                {
                    pubSocket.SendFrame(message);
                }
            }
        }
    }
}
