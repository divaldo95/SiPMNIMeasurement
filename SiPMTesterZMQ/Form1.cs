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
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

        private int VoltageAndCurrentFirstMeasLeft = 0;
        private int VoltageAndCurrentSecondMeasLeft = 0;

        private GlobalNIStateModel globalState = new GlobalNIStateModel();

        private bool startNewMeasurement = false;

        //Signal for response message ready
        private ManualResetEvent progressEvent = new ManualResetEvent(true);

        private void ChangeGlobalMeasurementState(MeasurementState newState)
        {
            globalState.MeasurementState = newState;
            pubSocket.PublishMessage("[STATUS]" + GetStatusResponseString());
        }

        private void SendAndSaveVoltageAndCurrentMeasurementData()
        {
            globalState.AppendVoltageAndCurrentMeasurementToList();
            string toSend = $"[MEAS]VoltageAndCurrentMeasurementDone:{JsonConvert.SerializeObject(globalState.VoltageAndCurrentMeasurement)}";
            pubSocket.PublishMessage(toSend); //send VI
            Console.WriteLine(toSend);
            globalState.MeasurementType = MeasurementType.Unknown;
            //AppendLogLine(toSend);
        }

        private void SendAndSaveIVMeasurementData()
        {
            globalState.AppendUnderTestToList();
            string toSend = $"[MEAS]IVMeasurementDone:{JsonConvert.SerializeObject(globalState.UnderMeasurement)}";
            pubSocket.PublishMessage(toSend); //send IV
            Console.WriteLine(toSend);
            globalState.MeasurementType = MeasurementType.Unknown;
            //AppendLogLine(toSend);
        }

        private void SendAndSaveDMMMeasurementData()
        {
            globalState.AppendDMMResistanceToList(); //save current
            string toSend = $"[MEAS]DMMMeasurementDone:{JsonConvert.SerializeObject(globalState.DMMResistanceMeasurement)}";
            pubSocket.PublishMessage(toSend); //send DMM
            Console.WriteLine(toSend);
            globalState.MeasurementType = MeasurementType.Unknown;
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
                globalState.MeasurementType = MeasurementType.Unknown;
                measurementStatus.Text = "IV done";
                Console.WriteLine("SMU V: " + string.Join(",", globalState.UnderMeasurement.SMUVoltage));
                Console.WriteLine("SMU I: " + string.Join(",", globalState.UnderMeasurement.SMUCurrent));
                Console.WriteLine("DMM V: " + string.Join(",", globalState.UnderMeasurement.DMMVoltage));
            }
            else if (smu.CurrentState != MeasurementState.Running && dmm.CurrentState != MeasurementState.Running)
            {
                //error happened because both are finished but data is not matching
                Trace.WriteLine("SMU and DMM are both finished, but data is not matching");

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

        private void ProcessVoltageSetDoneEvent(object sender, VoltageSetDoneEventArgs e)
        {
            //globalState.DMMResistanceMeasurement.Resistance += e.Measurement.VoltageMeasurements[0] / e.Measurement.CurrentMeasurements[0];
            Console.WriteLine("VoltageSetDoneEvent called");
            if (globalState.MeasurementType == MeasurementType.DMMResistanceMeasurement)
            {
                globalState.DMMResistanceMeasurement.Voltages.Add(e.Measurement.VoltageMeasurements[0]);
                globalState.DMMResistanceMeasurement.Currents.Add(e.Measurement.CurrentMeasurements[0]);
                if (DMMMeasLeft > 0)
                {
                    Console.WriteLine($"DMM meas left: {DMMMeasLeft}");
                    smu.MeasureSinglePoint(globalState.CurrentDMMStartModel.DMMResistance.Voltage);
                    Thread.Sleep(100);
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
                    globalState.DMMResistanceMeasurement.EndTimestamp = TimestampHelper.GetCurrentTimestamp();
                    SendAndSaveDMMMeasurementData();
                    measurementStatus.Text = "DMM resistance done";
                    ChangeGlobalMeasurementState(MeasurementState.FinishedDMM);
                    globalState.MeasurementType = MeasurementType.Unknown;
                }
            }
            else if (globalState.MeasurementType == MeasurementType.VoltageAndCurrentMeasurement)
            {

                Console.WriteLine($"Voltage and Current meas left: {VoltageAndCurrentFirstMeasLeft} | {VoltageAndCurrentSecondMeasLeft}");
                if (VoltageAndCurrentFirstMeasLeft > 0) //decrement on first start too | if it is zero, go to second and decrement
                {
                    globalState.VoltageAndCurrentMeasurement.FirstIterationVoltages.Add(e.Measurement.VoltageMeasurements[0]);
                    globalState.VoltageAndCurrentMeasurement.FirstIterationCurrents.Add(e.Measurement.CurrentMeasurements[0]);
                    //Thread.Sleep(200);
                    smu.MeasureSinglePoint(globalState.CurrentVoltageAndCurrentStartModel.FirstIteration.Voltage);

                    Console.WriteLine($"Started new first iteration's voltage and current measurement");
                    VoltageAndCurrentFirstMeasLeft--;
                }
                else if (VoltageAndCurrentFirstMeasLeft == 0) //set new voltage for second
                {
                    smu.Stop();
                    if (globalState.VoltageAndCurrentMeasurement.StartModel.MeasurementType == Enums.VoltageAndCurrentMeasurementTypes.ForwardResistance)
                    {
                        //Thread.Sleep(1000);
                    }
                    MeasurementFunctions.VoltageAndCurrentMeasurement(globalState.CurrentVoltageAndCurrentStartModel, smu, false); //start second iteration
                    smu.MeasureSinglePoint(globalState.CurrentVoltageAndCurrentStartModel.SecondIteration.Voltage);
                    VoltageAndCurrentFirstMeasLeft--;
                    //VoltageAndCurrentSecondMeasLeft--; //it is started now, decrement
                }
                else if (VoltageAndCurrentSecondMeasLeft > 0)
                {
                    globalState.VoltageAndCurrentMeasurement.SecondIterationVoltages.Add(e.Measurement.VoltageMeasurements[0]);
                    globalState.VoltageAndCurrentMeasurement.SecondIterationCurrents.Add(e.Measurement.CurrentMeasurements[0]);
                    //Thread.Sleep(200);
                    smu.MeasureSinglePoint(globalState.CurrentVoltageAndCurrentStartModel.SecondIteration.Voltage);
                    Console.WriteLine($"Started new second iteration's voltage and current measurement");
                    VoltageAndCurrentSecondMeasLeft--;
                }
                else
                {
                    Console.WriteLine($"Voltage and current measurement finished");
                    smu.SetVoltage(0.0);

                    for (int i = 0; i < globalState.VoltageAndCurrentMeasurement.FirstIterationVoltages.Count; i++)
                    {
                        globalState.VoltageAndCurrentMeasurement.FirstIterationVoltageAverage += globalState.VoltageAndCurrentMeasurement.FirstIterationVoltages[i];
                    }

                    for (int i = 0; i < globalState.VoltageAndCurrentMeasurement.FirstIterationCurrents.Count; i++)
                    {
                        globalState.VoltageAndCurrentMeasurement.FirstIterationCurrentAverage += globalState.VoltageAndCurrentMeasurement.FirstIterationCurrents[i];
                    }

                    for (int i = 0; i < globalState.VoltageAndCurrentMeasurement.SecondIterationVoltages.Count; i++)
                    {
                        globalState.VoltageAndCurrentMeasurement.SecondIterationVoltageAverage += globalState.VoltageAndCurrentMeasurement.SecondIterationVoltages[i];
                    }

                    for (int i = 0; i < globalState.VoltageAndCurrentMeasurement.SecondIterationCurrents.Count; i++)
                    {
                        globalState.VoltageAndCurrentMeasurement.SecondIterationCurrentAverage += globalState.VoltageAndCurrentMeasurement.SecondIterationCurrents[i];
                    }
                    globalState.VoltageAndCurrentMeasurement.FirstIterationVoltageAverage = globalState.VoltageAndCurrentMeasurement.FirstIterationVoltageAverage / globalState.VoltageAndCurrentMeasurement.FirstIterationVoltages.Count;
                    globalState.VoltageAndCurrentMeasurement.FirstIterationCurrentAverage = globalState.VoltageAndCurrentMeasurement.FirstIterationCurrentAverage / globalState.VoltageAndCurrentMeasurement.FirstIterationCurrents.Count;

                    globalState.VoltageAndCurrentMeasurement.SecondIterationVoltageAverage = globalState.VoltageAndCurrentMeasurement.SecondIterationVoltageAverage / globalState.VoltageAndCurrentMeasurement.SecondIterationVoltages.Count;
                    globalState.VoltageAndCurrentMeasurement.SecondIterationCurrentAverage = globalState.VoltageAndCurrentMeasurement.SecondIterationCurrentAverage / globalState.VoltageAndCurrentMeasurement.SecondIterationCurrents.Count;

                    globalState.VoltageAndCurrentMeasurement.EndTimestamp = TimestampHelper.GetCurrentTimestamp();
                    SendAndSaveVoltageAndCurrentMeasurementData();
                    measurementStatus.Text = "Voltage And Current measurement done";
                    Thread.Sleep(100);
                    smu.Stop();
                    if (globalState.VoltageAndCurrentMeasurement.StartModel.MeasurementType == Enums.VoltageAndCurrentMeasurementTypes.ForwardResistance)
                    {
                        Thread.Sleep(3000);
                    }
                    ChangeGlobalMeasurementState(MeasurementState.FinishedVoltageAndCurrent);
                    globalState.MeasurementType = MeasurementType.Unknown;
                }
            }
        }

        private void OnVoltageSetDoneEvent(object sender, VoltageSetDoneEventArgs e)
        {
            ProcessVoltageSetDoneEvent(sender, e);
            //Task pollingTask = Task.Run(() => ProcessVoltageSetDoneEvent(sender, e));
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

            smu.OnVIMeasurementDone += Smu_OnVIMeasurementDone;
            smu.OnVIMeasurementError += Smu_OnVIMeasurementError;

            globalState.MeasurementState = MeasurementState.NotRunning;
            
        }

        private void Smu_OnVIMeasurementError(object sender, VIMeasurementErrorEventArgs e)
        {
            globalState.VoltageAndCurrentMeasurement.ErrorHappened = true;
            globalState.VoltageAndCurrentMeasurement.ErrorMessage = e.Message;
            SendAndSaveVoltageAndCurrentMeasurementData();
            measurementStatus.Text = $"VI error: {e.Message}";
            Trace.WriteLine($"VI error: {e.Message}");
            ChangeGlobalMeasurementState(MeasurementState.Error);
        }

        private void Smu_OnVIMeasurementDone(object sender, VIMeasurementEventArgs e)
        {
            for (int i = 0; i < globalState.VoltageAndCurrentMeasurement.FirstIterationVoltages.Count; i++)
            {
                globalState.VoltageAndCurrentMeasurement.FirstIterationVoltageAverage += globalState.VoltageAndCurrentMeasurement.FirstIterationVoltages[i];
            }

            for (int i = 0; i < globalState.VoltageAndCurrentMeasurement.FirstIterationCurrents.Count; i++)
            {
                globalState.VoltageAndCurrentMeasurement.FirstIterationCurrentAverage += globalState.VoltageAndCurrentMeasurement.FirstIterationCurrents[i];
            }

            for (int i = 0; i < globalState.VoltageAndCurrentMeasurement.SecondIterationVoltages.Count; i++)
            {
                globalState.VoltageAndCurrentMeasurement.SecondIterationVoltageAverage += globalState.VoltageAndCurrentMeasurement.SecondIterationVoltages[i];
            }

            for (int i = 0; i < globalState.VoltageAndCurrentMeasurement.SecondIterationCurrents.Count; i++)
            {
                globalState.VoltageAndCurrentMeasurement.SecondIterationCurrentAverage += globalState.VoltageAndCurrentMeasurement.SecondIterationCurrents[i];
            }
            globalState.VoltageAndCurrentMeasurement.FirstIterationVoltageAverage = globalState.VoltageAndCurrentMeasurement.FirstIterationVoltageAverage / globalState.VoltageAndCurrentMeasurement.FirstIterationVoltages.Count;
            globalState.VoltageAndCurrentMeasurement.FirstIterationCurrentAverage = globalState.VoltageAndCurrentMeasurement.FirstIterationCurrentAverage / globalState.VoltageAndCurrentMeasurement.FirstIterationCurrents.Count;

            globalState.VoltageAndCurrentMeasurement.SecondIterationVoltageAverage = globalState.VoltageAndCurrentMeasurement.SecondIterationVoltageAverage / globalState.VoltageAndCurrentMeasurement.SecondIterationVoltages.Count;
            globalState.VoltageAndCurrentMeasurement.SecondIterationCurrentAverage = globalState.VoltageAndCurrentMeasurement.SecondIterationCurrentAverage / globalState.VoltageAndCurrentMeasurement.SecondIterationCurrents.Count;

            globalState.VoltageAndCurrentMeasurement.EndTimestamp = TimestampHelper.GetCurrentTimestamp();
            SendAndSaveVoltageAndCurrentMeasurementData();
            ChangeGlobalMeasurementState(MeasurementState.FinishedVoltageAndCurrent);
            measurementStatus.Text = "Voltage And Current measurement done";
        }

        private void OnVoltageSetTimeoutEvent(object sender, VoltageSetTimeoutEventArgs e)
        {
            Trace.WriteLine($"Voltage set timeout: {globalState.MeasurementType}");
            if (globalState.MeasurementType == MeasurementType.DMMResistanceMeasurement)
            {
                globalState.DMMResistanceMeasurement.ErrorHappened = true;
                globalState.DMMResistanceMeasurement.ErrorMessage = "Voltage set timout";
                SendAndSaveDMMMeasurementData();
                measurementStatus.Text = "DMM resistance set voltage timeout";
            }
            else if (globalState.MeasurementType == MeasurementType.VoltageAndCurrentMeasurement)
            {
                globalState.VoltageAndCurrentMeasurement.ErrorHappened = true;
                globalState.VoltageAndCurrentMeasurement.ErrorMessage = "Voltage set timout";
                SendAndSaveVoltageAndCurrentMeasurementData();
                measurementStatus.Text = "VI set voltage timeout";
            }
            ChangeGlobalMeasurementState(MeasurementState.Error);
        }

        private void OnSequenceTimeoutEvent(object sender, SequenceTimeoutEventArgs e)
        {
            globalState.UnderMeasurement.SMUVoltage = new List<double>();
            globalState.UnderMeasurement.SMUCurrent = new List<double>();
            globalState.UnderMeasurement.DMMVoltage = new List<double>();
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
            globalState.DMMResistanceMeasurement.StartTimestamp = TimestampHelper.GetCurrentTimestamp();



            //handle here measurement start and if it is successful or not
            responseModel.Identifier = startModel.Identifier;

            globalState.CurrentDMMStartModel = startModel;

            DMMMeasLeft = startModel.DMMResistance.Iterations;
            if (DMMMeasLeft > 0)
            {
                //Thread.Sleep(1000);
                responseModel.Successful = true;
                globalState.IsIVMeasurement = false;
                globalState.MeasurementType = MeasurementType.DMMResistanceMeasurement;
                MeasurementFunctions.DMMResistanceMeasurement(globalState.CurrentDMMStartModel.DMMResistance.Voltage, dmm, smu);
                smu.MeasureSinglePoint(globalState.CurrentDMMStartModel.DMMResistance.Voltage);
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

        private MeasurementStartResponseModel StartVoltageAndCurrentMeasurement(NIVoltageAndCurrentStartModel startModel)
        {
            MeasurementStartResponseModel responseModel = new MeasurementStartResponseModel();

            if (globalState.MeasurementState == MeasurementState.Running)
            {
                responseModel.Successful = false;
                responseModel.ErrorMessage = "Measurement already running";
            }

            globalState.VoltageAndCurrentMeasurement = new VoltageAndCurrentMeasurementResponseModel();
            globalState.VoltageAndCurrentMeasurement.StartModel = startModel;
            globalState.VoltageAndCurrentMeasurement.FirstIterationVoltages = new List<double>();
            globalState.VoltageAndCurrentMeasurement.FirstIterationCurrents = new List<double>();
            globalState.VoltageAndCurrentMeasurement.SecondIterationVoltages = new List<double>();
            globalState.VoltageAndCurrentMeasurement.SecondIterationCurrents = new List<double>();
            globalState.VoltageAndCurrentMeasurement.Identifier = startModel.Identifier;
            globalState.VoltageAndCurrentMeasurement.StartTimestamp = TimestampHelper.GetCurrentTimestamp();

            //handle here measurement start and if it is successful or not
            responseModel.Identifier = startModel.Identifier;

            globalState.CurrentVoltageAndCurrentStartModel = startModel;

            VoltageAndCurrentFirstMeasLeft = startModel.FirstIteration.Iterations;
            VoltageAndCurrentSecondMeasLeft = startModel.SecondIteration.Iterations;
            if (VoltageAndCurrentFirstMeasLeft > 0 && VoltageAndCurrentSecondMeasLeft > 0)
            {
                //Thread.Sleep(1000);
                responseModel.Successful = true;
                //VoltageAndCurrentFirstMeasLeft--;
                //measurementStatus.Text = "Voltage and Current first iteration measurement";
                measurementStatus.Text = "Voltage and Current first iteration measurement";
                ChangeGlobalMeasurementState(MeasurementState.Running);
                globalState.MeasurementType = MeasurementType.VoltageAndCurrentMeasurement;
                startNewMeasurement = true;
            }
            else
            {
                responseModel.Successful = false;
                responseModel.ErrorMessage = "Invalid iteration number";
                globalState.MeasurementType = MeasurementType.Unknown;
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
                VoltageAndCurrentMeasurementResponseModel viResp;
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
                else if (globalState.GetVoltageAndCurrentMeasByIdentifier(identifier, out viResp))
                {
                    response = "VIMeasurementResult:" + JsonConvert.SerializeObject(viResp);

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
            else if (sender == "StartVoltageAndCurrentMeasurement")
            {
                NIVoltageAndCurrentStartModel NIVIStart;
                if (!Parser.JObject2JSON(obj, out NIVIStart, out error))
                {
                    ErrorResponse errorResponse = new ErrorResponse();
                    errorResponse.Sender = "JObject2JSON";
                    errorResponse.Message = message;
                    AppendLogLine($"Failed to parse MeasurementStart event ({error})");
                    response = "Error:" + JsonConvert.SerializeObject(errorResponse);
                }
                else
                {
                    response = "MeasurementStart:" + JsonConvert.SerializeObject(StartVoltageAndCurrentMeasurement(NIVIStart));
                    Task.Run(() => smu.MeasureVI(globalState.VoltageAndCurrentMeasurement));
                }
            }
            else if (sender == "StopMeasurement")
            {
                response = "StopMeasurement:" + "{\"Status\": 1}";
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
                if (startNewMeasurement) //flag for VIMeasurement
                {
                    //MeasurementFunctions.VoltageAndCurrentMeasurement(globalState.CurrentVoltageAndCurrentStartModel, smu, true);
                    //smu.MeasureSinglePoint(globalState.CurrentVoltageAndCurrentStartModel.FirstIteration.Voltage);

                    startNewMeasurement = false;
                }
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
