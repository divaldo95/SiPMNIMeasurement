using NationalInstruments.ModularInstruments.NIDCPower;
using NationalInstruments.ModularInstruments.SystemServices.DeviceServices;
using NationalInstruments;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SiPMTesterInterface.Enums;
using NationalInstruments.Restricted;

namespace SiPMTesterZMQ.Classes
{
    public class SequenceDoneEventArgs : EventArgs
    {
        public SequenceDoneEventArgs() : base()
        {

        }
        public DCPowerFetchResult Results { get; set; }
    }

    public class SequenceTimeoutEventArgs : EventArgs
    {
        public SequenceTimeoutEventArgs() : base()
        {

        }
    }

    public class VoltageSetTimeoutEventArgs : EventArgs
    {
        public VoltageSetTimeoutEventArgs() : base()
        {

        }
    }

    public class VoltageSetDoneEventArgs : EventArgs
    {
        public VoltageSetDoneEventArgs() : base() 
        {
        }
        public VoltageSetDoneEventArgs(double v, DCPowerMeasureResult results) : base()
        {
            Voltage = v;
            Measurement = results;
        }
        public double Voltage { get; set; }
        public DCPowerMeasureResult Measurement { get; set; }
        public bool InCompliance { get; set; }
    }

    public class MeasurementStateChangedEventArgs : EventArgs
    {
        public MeasurementStateChangedEventArgs() : base() 
        {
        }

        public MeasurementStateChangedEventArgs(MeasurementState prev, MeasurementState cur) : base()
        {
            Previous = prev;
            Current = cur;
        }
        public MeasurementState Previous { get; set; }
        public MeasurementState Current { get; set; }
    }

    public class SMU
    {
        public string DeviceName { get; set; } = "SMU200";

        // Lock the dcPowerSession object
        object lockObject = new object();

        public event EventHandler<SequenceDoneEventArgs> OnSequenceDoneEvent;
        public event EventHandler<SequenceTimeoutEventArgs> OnSequenceTimeoutEvent;

        public event EventHandler<MeasurementStateChangedEventArgs> OnMeasurementStateChangedEvent;

        public event EventHandler<VoltageSetDoneEventArgs> OnVoltageSetDoneEvent;
        public event EventHandler<VoltageSetTimeoutEventArgs> OnVoltageSetTimeoutEvent;

        private MeasurementState _currentState = MeasurementState.NotRunning;

        public SynchronizationContext UIContext = null;

        public MeasurementState CurrentState
        {
            get { return _currentState; }
            set
            {
                if (value != _currentState)
                {
                    MeasurementStateChangedEventArgs args = new MeasurementStateChangedEventArgs(_currentState, value);
                    _currentState = value;
                    OnMeasurementStateChangedEvent?.Invoke(this, args);
                }
            }
        }

        // Create a CancellationTokenSource for cancellation
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public int EventCount { get; set; } = 0;

        protected virtual void RaiseVoltageSetDoneEvent(VoltageSetDoneEventArgs args)
        {
            // Capture the synchronization context of the UI thread
            //SynchronizationContext uiContext = GetUIContext();

            // Post the event invocation to the UI thread's synchronization context
            UIContext?.Post(delegate
            {
                OnVoltageSetDoneEvent?.Invoke(this, args);
            }, null);
        }

        public SMU()
        {
            Init();
        }

        private void InitCommonProperties()
        {
            /* Set the Output Function to DC Voltage.  If you change the Output
            Function to DC Current, you must use Current Level and Voltage Limit
            instead of Voltage Level and Current Limit. */
            dcPowerSession.Outputs[ChannelName].Source.Output.Function = DCPowerSourceOutputFunction.DCVoltage;

            /* Configure the source delay.  This is the amount of time the device
            waits after programming the output. The source operation is complete
            after this delay. */
            dcPowerSession.Outputs[ChannelName].Source.SourceDelay = SourceDelay;

            dcPowerSession.Outputs[ChannelName].Source.OvpLimit = 50; //Set OVP to voltage level, 50V
            dcPowerSession.Outputs[ChannelName].Measurement.ApertureTime = ApertureTime;
            dcPowerSession.Outputs[ChannelName].Measurement.ApertureTimeUnits = DCPowerMeasureApertureTimeUnits.Seconds;
            dcPowerSession.Outputs[ChannelName].Measurement.PowerLineFrequency = PowerlineFrequency;

            dcPowerSession.Outputs[ChannelName].Source.Voltage.CurrentLimitAutorange = DCPowerSourceCurrentLimitAutorange.Off;
            dcPowerSession.Outputs[ChannelName].Source.Voltage.VoltageLevelAutorange = DCPowerSourceVoltageLevelAutorange.Off;
        }

        public void Init()
        {
            if (dcPowerSession != null)
            {
                return;
            }
            bool found = false;
            using (ModularInstrumentsSystem smuDevices = new ModularInstrumentsSystem("NI-DCPower"))
            {
                foreach (DeviceInfo device in smuDevices.DeviceCollection)
                {
                    //Console.WriteLine(device.Name);
                    if (device.Name.CompareTo(DeviceName) == 0)
                    {
                        //deviceName = device.Name;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                throw new NotSupportedException(DeviceName + " not found");
            }

            dcPowerSession = new NIDCPower(DeviceName, ChannelName, true);
            dcPowerSession.DriverOperation.Warning += new EventHandler<DCPowerWarningEventArgs>(DCPowerDriverOperationWarning);

            InitCommonProperties();
            
        }

        private void InitSequence(List<double> voltageList)
        {
            if (dcPowerSession == null || CurrentState == MeasurementState.Running)
            {
                return;
            }
            Stop();
            Init();
            /* Configure the Source mode to Sequence. */
            dcPowerSession.Source.Mode = DCPowerSourceMode.Sequence;
            dcPowerSession.Outputs[ChannelName].Measurement.Sense = DCPowerMeasurementSense.Remote;
            dcPowerSession.Outputs[ChannelName].Source.Voltage.VoltageLevelRange = 200.0;

            //Set limits every time
            dcPowerSession.Outputs[ChannelName].Source.Voltage.CurrentLimitRange = CurrentLimitRange;
            dcPowerSession.Outputs[ChannelName].Source.Voltage.CurrentLimit = CurrentLimit;

            dcPowerSession.ExportSignal(DCPowerSignalSource.SourceCompleteEvent, DCPowerSourceCompleteEventOutputTerminal.PxiTriggerLine1);
            dcPowerSession.Events.SourceCompleteEvent.Pulse.Polarity = DCPowerPulsePolarity.ActiveLow;

            dcPowerSession.Triggers.SourceTrigger.DigitalEdge.Configure(DCPowerDigitalEdgeSourceTriggerInputTerminal.PxiTriggerLine0, DCPowerTriggerEdge.Falling);

            try
            {
                dcPowerSession.Outputs[ChannelName].Source.AdvancedSequencing.DeleteAdvancedSequence(AdvancedSequenceName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                //probably "The active advanced sequence does not exist"
            }
            /* Specify the Advanced Sequence Attributes which can change per step. */
            DCPowerAdvancedSequenceProperty[] advancedSequenceProperties = {DCPowerAdvancedSequenceProperty.VoltageLevel,
                                                                            DCPowerAdvancedSequenceProperty.CurrentLimit,
                                                                            DCPowerAdvancedSequenceProperty.CurrentLimitRange,
                                                                            DCPowerAdvancedSequenceProperty.OutputFunction};

            dcPowerSession.Outputs[ChannelName].Source.AdvancedSequencing.CreateAdvancedSequence(AdvancedSequenceName, advancedSequenceProperties, true);

            for (int i = 0; i < voltageList.Count; i++)
            {
                dcPowerSession.Outputs[ChannelName].Source.AdvancedSequencing.CreateAdvancedSequenceStep(true);

                /* Configure the Voltage Level. */
                dcPowerSession.Outputs[ChannelName].Source.Voltage.VoltageLevel = voltageList[i];
                //Calculate current values!!
                dcPowerSession.Outputs[ChannelName].Source.Voltage.CurrentLimit = CurrentLimit;
                dcPowerSession.Outputs[ChannelName].Source.Voltage.CurrentLimitRange = CurrentLimitRange; //set the same current limit for all
            }
            EventCount = voltageList.Count;
        }

        public void Close()
        {
            if (dcPowerSession != null) 
            {
                dcPowerSession.Utility.Reset();
                dcPowerSession.Close();
                dcPowerSession = null;
            }            
        }

        public void StartMultiPointMeasurement(List<double> voltages)
        {
            if (dcPowerSession != null && CurrentState != MeasurementState.Running)
            {
                InitSequence(voltages);
                dcPowerSession.Control.Initiate();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = new CancellationTokenSource();

                // Start a task for polling the WaitForEvent function
                CurrentState = MeasurementState.Running;
                Task pollingTask = Task.Run(() => WaitForEventPolling(dcPowerSession, cancellationTokenSource.Token));
            }
            
        }
        public void Stop()
        {
            if (dcPowerSession != null)
            {
                cancellationTokenSource.Cancel();
                dcPowerSession.Control.Abort();
                dcPowerSession.Utility.Reset();
                dcPowerSession = null;
                GC.Collect();
                Init();
                Thread.Sleep(200);
                InitCommonProperties(); //init common properties
            }
        }

        void WaitForEventPolling(NIDCPower dcPowerSession, CancellationToken cancellationToken)
        {

            PrecisionTimeSpan timeout = new PrecisionTimeSpan(100.0);
            // Continuously poll WaitForEvent function
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    dcPowerSession.Events.SequenceEngineDoneEvent.WaitForEvent(timeout);
                    SequenceDoneEventArgs args = new SequenceDoneEventArgs();
                    args.Results = dcPowerSession.Measurement.Fetch(ChannelName, timeout, EventCount);
                    //Stop(); //using this stops before dmm finishes
                    OnSequenceDoneEvent?.Invoke(this, args);
                    CurrentState = MeasurementState.Finished;
                    break;
                }
                catch (Exception)
                {
                    OnSequenceTimeoutEvent?.Invoke(this, new SequenceTimeoutEventArgs());
                    cancellationTokenSource.Cancel(); //stop the measurement
                    CurrentState = MeasurementState.Error;
                }
                
                // Lock the dcPowerSession object
                /*
                lock (dcPowerSession)
                {
                    // Poll WaitForEvent function
                    
                }
                */
            }
        }

        void WaitForEventVoltageSetAndMeasurePolling(NIDCPower dcPowerSession, double vSet, CancellationToken cancellationToken)
        {

            PrecisionTimeSpan timeout = new PrecisionTimeSpan(10.0);
            // Continuously poll WaitForEvent function
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    dcPowerSession.Events.SourceCompleteEvent.WaitForEvent(timeout);

                    VoltageSetDoneEventArgs args = new VoltageSetDoneEventArgs();
                    args.Voltage = vSet;

                    Thread.Sleep(100);

                    args.Measurement = dcPowerSession.Measurement.Measure(ChannelName);
                    args.InCompliance = dcPowerSession.Measurement.QueryInCompliance(ChannelName);

                    CurrentState = MeasurementState.Finished;
                    //OnVoltageSetDoneEvent?.Invoke(this, args);
                    RaiseVoltageSetDoneEvent(args);
                    break;
                }
                catch (Exception)
                {
                    OnVoltageSetTimeoutEvent?.Invoke(this, new VoltageSetTimeoutEventArgs());
                    cancellationTokenSource.Cancel(); //stop the measurement
                    CurrentState = MeasurementState.Error;
                }

                // Lock the dcPowerSession object
                /*
                lock (dcPowerSession)
                {
                    // Poll WaitForEvent function
                    
                }
                */
            }
            Console.WriteLine("SMU wait while loop exited");
            //Stop();
        }


        //--------------------------------------------------------
        //Public functions
        public void SetVoltage(double v)
        {
            if (dcPowerSession == null || CurrentState == MeasurementState.Running)
            {
                return;
                //dcPowerSession.Utility.Reset(); //Disable output first!
            }
            try
            {
                Console.WriteLine("SMU set voltage called");
                Stop();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = new CancellationTokenSource();

                /* Configure the Source mode to SinglePoint. */
                dcPowerSession.Source.Mode = DCPowerSourceMode.SinglePoint;
                dcPowerSession.Outputs[ChannelName].Measurement.Sense = DCPowerMeasurementSense.Local;
                dcPowerSession.Outputs[ChannelName].Source.Voltage.VoltageLevelRange = 200.0;

                //Set limits every time
                dcPowerSession.Outputs[ChannelName].Source.Voltage.CurrentLimitRange = CurrentLimitRange; //0.005; //500uA max
                dcPowerSession.Outputs[ChannelName].Source.Voltage.CurrentLimit = CurrentLimit; //0.005; //300uA
                dcPowerSession.Outputs[ChannelName].Source.Voltage.VoltageLevel = v;
                dcPowerSession.Outputs[ChannelName].Source.Voltage.CurrentLimit = CurrentLimit;
                dcPowerSession.Outputs[ChannelName].Source.Voltage.CurrentLimitRange = CurrentLimitRange;

                dcPowerSession.Control.Initiate();
                Console.WriteLine("SMU voltage set initiated");

                CurrentState = MeasurementState.Running;
                Task pollingTask = Task.Run(() => WaitForEventVoltageSetAndMeasurePolling(dcPowerSession, v, cancellationTokenSource.Token));

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        //--------------------------------------------------------
        //Private functions
        void DCPowerDriverOperationWarning(object sender, DCPowerWarningEventArgs e)
        {
            
        }

        //--------------------------------------------------------
        //Private variables
        private NIDCPower dcPowerSession = null;

        //private IVSettings ivSettings = new IVSettings();
        public int ApertureSequenceSize { get; set; } = 3;

        public string AdvancedSequenceName { get; set; } = "MySeq";
        public string ChannelName { get; set; } = "0";
        public PrecisionTimeSpan SourceDelay { get; set; } = new PrecisionTimeSpan(0.1); //in s
        public double ApertureTime { get; set; } = 0.02; //in s
        public int PowerlineFrequency { get; set; } = 50; //Hz
        public int VoltageLevelRange { get; set; } = 200; //V
        public double VoltageLevel { get; set; } = 30.0; //V
        public double[] CurrentLimits = { 0.000001, 0.00001, 0.0001, 0.001, 0.01, 0.1, 1.0, 3.0 }; //1uA, 10uA, 100uA, 1mA, 10mA, 100mA, 1A, 3A

        public double CurrentLimit { get; set; } = 0.001;
        public static double CurrentLimitRange { get; set; } = 0.001;

        public double SPSVoltageMeasurement { get; set; } = 0.0;
        public double SPSCurrentMeasurement { get; set; } = 0.0;
    }
}
