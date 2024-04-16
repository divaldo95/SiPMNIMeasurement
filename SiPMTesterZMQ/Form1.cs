using NationalInstruments.ModularInstruments.NIDmm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiPMTesterInterface.Classes;
using SiPMTesterInterface.Enums;
using SiPMTesterInterface.Models;
using SiPMTesterZMQ.Classes;
using SiPMTesterZMQ.Helpers;
using SiPMTesterZMQ.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace SiPMTesterZMQ
{
    public partial class Form1 : Form
    {
        private RespSocket respSocket;
        private PubSocket pubSocket;

        private SMU smu = new SMU();
        private DMM dmm = new DMM();

        private int DMMMeasLeft = 0;

        private GlobalNIStateModel globalState = new GlobalNIStateModel();

        //Signal for response message ready
        private ManualResetEvent progressEvent = new ManualResetEvent(true);

        private void ChangeGlobalMeasurementState(MeasurementState newState)
        {
            globalState.MeasurementState = newState;
            pubSocket.PublishMessage("[STATUS]" + GetStatusResponseString());
        }

        private void SendAndSaveIVMeasurementData()
        {
            globalState.AppendUnderTestToList();
            string toSend = $"[MEAS]IVMeasurementDone:{JsonConvert.SerializeObject(globalState.UnderMeasurement)}";
            pubSocket.PublishMessage(toSend); //send IV
            Console.WriteLine(toSend);
            //AppendLogLine(toSend);
        }

        private void SendAndSaveDMMMeasurementData()
        {
            globalState.AppendDMMResistanceToList(); //save current
            string toSend = $"[MEAS]DMMMeasurementDone:{JsonConvert.SerializeObject(globalState.DMMResistanceMeasurement)}";
            pubSocket.PublishMessage(toSend); //send DMM
            Console.WriteLine(toSend);
            //AppendLogLine(toSend);
        }

        private void CheckAndSendIVData()
        {
            if (globalState.UnderMeasurement.SMUVoltage == null || globalState.UnderMeasurement.DMMVoltage == null ||
                globalState.UnderMeasurement.SMUCurrent == null)
            {
                //probably one of the arrays data not received yet
                return;
            }
            if (globalState.UnderMeasurement.SMUVoltage.Count == globalState.UnderMeasurement.DMMVoltage.Count &&
                globalState.UnderMeasurement.SMUCurrent.Count == globalState.UnderMeasurement.DMMVoltage.Count)
            {
                //got both measurements, stop the instruments
                smu.Stop();
                dmm.Stop();
                globalState.UnderMeasurement.EndTimestamp = TimestampHelper.GetCurrentTimestamp();
                SendAndSaveIVMeasurementData();
                ChangeGlobalMeasurementState(MeasurementState.FinishedIV);
                measurementStatus.Text = "IV done";
            }
            else if (smu.CurrentState != MeasurementState.Running && dmm.CurrentState != MeasurementState.Running)
            {
                //error happened because both are finished but data is not matching
            }
        }

        //read dmm measurements here
        private void Measurement_FetchMultiPointCompleted(object sender, DmmMeasurementEventArgs<double[]> e)
        {
            globalState.UnderMeasurement.DMMVoltage = new List<double>(e.Reading);
            CheckAndSendIVData();
        }

        //read smu measurements here
        private void OnSequenceDoneEvent(object sender, SequenceDoneEventArgs e)
        {
            var results = e.Results;
            globalState.UnderMeasurement.SMUVoltage = new List<double>(results.VoltageMeasurements);
            globalState.UnderMeasurement.SMUCurrent = new List<double>(results.CurrentMeasurements);
            CheckAndSendIVData();
        }

        private void OnVoltageSetDoneEvent(object sender, VoltageSetDoneEventArgs e)
        {
            //globalState.DMMResistanceMeasurement.Resistance += e.Measurement.VoltageMeasurements[0] / e.Measurement.CurrentMeasurements[0];
            Console.WriteLine("VoltageSetDoneEvent called");
            globalState.DMMResistanceMeasurement.Voltages.Add(e.Measurement.VoltageMeasurements[0]);
            globalState.DMMResistanceMeasurement.Currents.Add(e.Measurement.CurrentMeasurements[0]);
            if (DMMMeasLeft > 0)
            {
                Console.WriteLine($"DMM meas left: {DMMMeasLeft}");
                MeasurementFunctions.DMMResistanceMeasurement(globalState.CurrentDMMStartModel.DMMResistance.Voltage, dmm, smu);
                Console.WriteLine($"Started new measurement");
                DMMMeasLeft--;
            }
            else
            {
                Console.WriteLine($"DMM meas left: {DMMMeasLeft}. Measurement finished");
                smu.Stop();
                dmm.Stop();
                for (int i = 0; i < globalState.DMMResistanceMeasurement.Voltages.Count; i++)
                {
                    globalState.DMMResistanceMeasurement.Resistance += globalState.DMMResistanceMeasurement.Voltages[i] / globalState.DMMResistanceMeasurement.Currents[i];
                }
                globalState.DMMResistanceMeasurement.Resistance = globalState.DMMResistanceMeasurement.Resistance / globalState.DMMResistanceMeasurement.Voltages.Count;
                SendAndSaveDMMMeasurementData();
                measurementStatus.Text = "DMM resistance done";
                ChangeGlobalMeasurementState(MeasurementState.FinishedDMM);
            }
        }

        private void periodicUpdatesCallback(object sender, SubSocketPeriodicUpdateElapsed e)
        {
            string msg = "[STATUS]" + GetStatusResponseString(); //send current state periodically
            pubSocket.PublishMessage(msg);
        }

        public Form1()
        {
            InitializeComponent();

            respSocket = new RespSocket();
            pubSocket = new PubSocket();

            pubSocket.OnPeriodicUpdateTimeElapsed += periodicUpdatesCallback;

            dmm.Init();
            smu.Init();
            smu.UIContext = WindowsFormsSynchronizationContext.Current;

            dmm.OnMultiPointEventFinished += Measurement_FetchMultiPointCompleted;

            smu.OnSequenceDoneEvent += OnSequenceDoneEvent;
            smu.OnSequenceTimeoutEvent += OnSequenceTimeoutEvent;

            smu.OnVoltageSetDoneEvent += OnVoltageSetDoneEvent;
            smu.OnVoltageSetTimeoutEvent += OnVoltageSetTimeoutEvent;

            globalState.MeasurementState = MeasurementState.NotRunning;
            
        }

        private void OnVoltageSetTimeoutEvent(object sender, VoltageSetTimeoutEventArgs e)
        {
            globalState.DMMResistanceMeasurement.ErrorHappened = true;
            globalState.DMMResistanceMeasurement.ErrorMessage = "Voltage set timout";
            SendAndSaveDMMMeasurementData();
            measurementStatus.Text = "DMM resistance set voltage timeout";
            ChangeGlobalMeasurementState(MeasurementState.Error);
        }

        private void OnSequenceTimeoutEvent(object sender, SequenceTimeoutEventArgs e)
        {
            globalState.UnderMeasurement.ErrorHappened = true;
            globalState.UnderMeasurement.ErrorMessage = "Sequence timeout";
            SendAndSaveIVMeasurementData();
            measurementStatus.Text = "IV sequence timeout";
            ChangeGlobalMeasurementState(MeasurementState.Error);

        }

        private MeasurementStartResponseModel StartIVMeasurement(NIIVStartModel startModel)
        {
            MeasurementStartResponseModel responseModel = new MeasurementStartResponseModel();

            if (globalState.MeasurementState == MeasurementState.Running)
            {
                responseModel.Successful = false;
                responseModel.ErrorMessage = "Measurement already running";
                return responseModel;
            }

            globalState.UnderMeasurement = new IVMeasurementResponseModel();
            globalState.UnderMeasurement.Identifier = startModel.Identifier;

            //handle here measurement start and if it is successful or not
            responseModel.Identifier = startModel.Identifier;

            globalState.CurrentIVStartModel = startModel;

            if (startModel.Voltages.Count > 0)
            {
                responseModel.Successful = true; //measurement started successfully
                globalState.UnderMeasurement.StartTimestamp = TimestampHelper.GetCurrentTimestamp();
                MeasurementFunctions.IVMeasurement(startModel, dmm, smu);
                measurementStatus.Text = "IV measurement";
                ChangeGlobalMeasurementState(MeasurementState.Running);
            }
            else
            {
                responseModel.Successful = false;
                responseModel.ErrorMessage = "Invalid voltages number";
            }
            return responseModel;
        }

        private MeasurementStartResponseModel StartDMMMeasurement(NIDMMStartModel startModel)
        {
            MeasurementStartResponseModel responseModel = new MeasurementStartResponseModel();

            if (globalState.MeasurementState == MeasurementState.Running)
            {
                responseModel.Successful = false;
                responseModel.ErrorMessage = "Measurement already running";
            }

            globalState.DMMResistanceMeasurement = new DMMResistanceMeasurementResponseModel();
            globalState.DMMResistanceMeasurement.Voltages = new List<double>();
            globalState.DMMResistanceMeasurement.Currents = new List<double>();
            globalState.DMMResistanceMeasurement.Identifier = startModel.Identifier;

            //handle here measurement start and if it is successful or not
            responseModel.Identifier = startModel.Identifier;

            globalState.CurrentDMMStartModel = startModel;

            DMMMeasLeft = startModel.DMMResistance.Iterations;
            if (DMMMeasLeft > 0)
            {
                //Thread.Sleep(1000);
                responseModel.Successful = true;
                globalState.IsIVMeasurement = false;
                MeasurementFunctions.DMMResistanceMeasurement(globalState.CurrentDMMStartModel.DMMResistance.Voltage, dmm, smu);
                measurementStatus.Text = "DMM resistance measurement";
                ChangeGlobalMeasurementState(MeasurementState.Running);
            }
            else
            {
                responseModel.Successful = false;
                responseModel.ErrorMessage = "Invalid iteration number";
            }
            return responseModel;
        }

        private string GetStatusResponseString()
        {
            StatusChangeResponseModel statusRespModel = new StatusChangeResponseModel();
            statusRespModel.State = globalState.MeasurementState;
            return "StatusChange:" + JsonConvert.SerializeObject(statusRespModel);
        }

        private string ProcessReceivedMessage(string message)
        {
            string sender;
            string error;
            JObject obj;
            string response = "";

            bool parseSuccessful = Parser.ParseMeasurementStatus(message, out sender, out obj);

            if (!parseSuccessful)
            {
                AppendLogLine($"Failed to parse {message}");
                ErrorResponse errorResponse = new ErrorResponse();
                errorResponse.Sender = "Parser";
                errorResponse.Message = message;
                response = "Error:" + JsonConvert.SerializeObject(errorResponse);
            }

            else if (sender == "Ping")
            {
                response = "Pong:{\"Status:\": 1}";
            }

            else if (sender == "GetState")
            {
                //StatusChangeResponseModel statusRespModel = new StatusChangeResponseModel();
                //statusRespModel.State = globalState.MeasurementState;
                //response = "StatusChange:" + JsonConvert.SerializeObject(statusRespModel);
                response = GetStatusResponseString();
            }
            else if (sender == "GetMeasurementData")
            {
                MeasurementIdentifier identifier;
                DMMResistanceMeasurementResponseModel dmmResp;
                IVMeasurementResponseModel ivResp;
                if (!Parser.JObject2JSON(obj, out identifier, out error))
                {
                    ErrorResponse errorResponse = new ErrorResponse();
                    errorResponse.Sender = "JObject2JSON";
                    errorResponse.Message = message;
                    AppendLogLine($"Failed to parse Identifier for measurement data resend: ({error})");
                    response = "Error:" + JsonConvert.SerializeObject(errorResponse);
                }
                else if (globalState.GetDMMResMeasByIdentifier(identifier, out dmmResp))
                {
                    response = "DMMMeasurementResult:" + JsonConvert.SerializeObject(dmmResp);

                }
                else if (globalState.GetIVMeasByIdentifier(identifier, out ivResp))
                {
                    response = "IVMeasurementResult:" + JsonConvert.SerializeObject(ivResp);

                }
                else
                {
                    dmmResp = new DMMResistanceMeasurementResponseModel();
                    dmmResp.ErrorHappened = true;
                    dmmResp.Identifier = identifier;
                    dmmResp.ErrorMessage = "Measurement not found";
                    response = "IVMeasurementDone:" + JsonConvert.SerializeObject(dmmResp);
                }
            }
            else if (sender == "StartIVMeasurement")
            {
                NIIVStartModel NIIVStart;
                if (!Parser.JObject2JSON(obj, out NIIVStart, out error))
                {
                    ErrorResponse errorResponse = new ErrorResponse();
                    errorResponse.Sender = "JObject2JSON";
                    errorResponse.Message = message;
                    AppendLogLine($"Failed to parse MeasurementStart event ({error})");
                    response = "Error:" + JsonConvert.SerializeObject(errorResponse);
                }
                else
                {
                    response = "MeasurementStart:" + JsonConvert.SerializeObject(StartIVMeasurement(NIIVStart));
                }
            }
            else if (sender == "StartDMMMeasurement")
            {
                NIDMMStartModel NIDMMStart;
                if (!Parser.JObject2JSON(obj, out NIDMMStart, out error))
                {
                    ErrorResponse errorResponse = new ErrorResponse();
                    errorResponse.Sender = "JObject2JSON";
                    errorResponse.Message = message;
                    AppendLogLine($"Failed to parse MeasurementStart event ({error})");
                    response = "Error:" + JsonConvert.SerializeObject(errorResponse);
                }
                else
                {
                    response = "MeasurementStart:" + JsonConvert.SerializeObject(StartDMMMeasurement(NIDMMStart));
                }
            }
            return response;
        }

        private void setBtnStates(bool isRunning)
        {
            if (isRunning)
            {
                progressBar1.Style = ProgressBarStyle.Marquee;
                progressBar1.MarqueeAnimationSpeed = 30;
                startBtn.Enabled = false;
                stopBtn.Enabled = true;
                if (!responseSocketWorker.IsBusy)
                {
                    responseSocketWorker.RunWorkerAsync();
                }
                
            }
            else
            {
                progressBar1.MarqueeAnimationSpeed = 0;
                progressBar1.Style = ProgressBarStyle.Continuous;
                startBtn.Enabled = true;
                stopBtn.Enabled = false;
                if (responseSocketWorker.IsBusy && responseSocketWorker.WorkerSupportsCancellation)
                {
                    responseSocketWorker.CancelAsync();
                }
            }
            
        }

        private void startBtn_Click(object sender, EventArgs e)
        {
            setBtnStates(true);
        }

        private void stopBtn_Click(object sender, EventArgs e)
        {
            setBtnStates(false);
        }

        private void responseSocketWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            respSocket.Start();
            pubSocket.Start();
            int counter = 0;
            
            while (!worker.CancellationPending)
            {
                progressEvent.WaitOne();
                counter++;
                var message = respSocket.ReceiveFrameString();
                if (message == null) continue;
                progressEvent.Reset();
                worker.ReportProgress(0, message);
            }

            worker.ReportProgress(1, $"Received {counter} lines");
            e.Cancel = true;
        }

        private void responseSocketWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
             //block until the response not sent
            if (e.ProgressPercentage == 0)
            {
                //maybe received something as string
                string msg = e.UserState as string;
                AppendLogLine(msg + Environment.NewLine);
                string responseMessage = ProcessReceivedMessage(msg);
                respSocket.SendFrame(responseMessage);
            }

            else if (e.ProgressPercentage == 1)
            {
                //maybe received something as string
                string msg = e.UserState as string;
                AppendLogLine(msg + Environment.NewLine);

                AppendLogLine(msg);
            }
            progressEvent.Set();
        }

        private void AppendLogLine(string msg)
        {
            logTextBox.AppendText(msg);
            if (logTextBox.Text.Length > 100000 )
            {
                int lineIndex = 0;
                for (int i = 0; i < 10; i++)
                {
                    lineIndex = logTextBox.Text.IndexOf(Environment.NewLine, lineIndex);
                }
                logTextBox.Text.Remove(0, lineIndex);
            }
        }

        private void responseSocketWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                respSocket.Stop();
                pubSocket.Stop();
                AppendLogLine("ResponseWorker canceled!" + Environment.NewLine);
            }
            else if (e.Error != null)
            {
                AppendLogLine("Error: " + e.Error.Message);
            }
            else
            {
                respSocket.Stop();
                pubSocket.Stop();
                AppendLogLine("Done!");
            }
        }

        private void sendTestPUBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            pubSocket.PublishMessage("[MEAS]Here I am");
        }
    }
}
