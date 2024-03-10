using System;
using System.Diagnostics;
using NationalInstruments;
using NationalInstruments.ModularInstruments.NIDmm;
using NationalInstruments.ModularInstruments.SystemServices.DeviceServices;
using SiPMTesterInterface.Enums;
using SiPMTesterZMQ.Enums;

namespace SiPMTesterZMQ.Classes
{
    

    public class DMM
    {
        private string DeviceName = "DMM";

        public EventHandler<DmmMeasurementEventArgs<double[]>> OnMultiPointEventFinished;
        public double ApertureTime { get; set; } = 0.1;
        public double Resolution { get; set; } = 6.5;
        public double Range { get; set; } = 100;
        public int PowerlineFrequency { get; set; } = 50;
        public double InputResistance { get; set; } = InputResistanceValues.Resistance10MOhm;

        public event EventHandler<MeasurementStateChangedEventArgs> OnMeasurementStateChangedEvent;

        private MeasurementState _currentState = MeasurementState.NotRunning;

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

        public DMM()
        {
        }

        public void Init()
        {
            if (dmmSession != null)
            {
                return;
            }
            bool found = false;
            using (ModularInstrumentsSystem dmmDevices = new ModularInstrumentsSystem("NI-DMM"))
            {
                foreach (DeviceInfo device in dmmDevices.DeviceCollection)
                {
                    if (device.Name.CompareTo(DeviceName) == 0)
                    {
                        //deviceName = device.Name;
                        found = true;
                        //Trace.WriteLine("DMM found");
                    }
                }
            }

            if (!found)
            {
                //Trace.WriteLine("DMM not found");
                throw new NotSupportedException(DeviceName + " not found");
            }

            dmmSession = new NIDmm(DeviceName, true, true); //Reset device is true
                                                            //DmmMeasurementFunction measurementMode = DmmMeasurementFunction.DCVolts;
            dmmSession.ConfigureMeasurementDigits(DmmMeasurementFunction.DCVolts, Range, Resolution);
            dmmSession.InputResistance = InputResistance;
            dmmSession.Advanced.ApertureTime = ApertureTime;
            dmmSession.Advanced.ApertureTimeUnits = DmmApertureTimeUnits.Seconds;
            dmmSession.Advanced.PowerlineFrequency = PowerlineFrequency;
            dmmSession.Advanced.AdcCalibration = DmmAdcCalibration.On;
            dmmSession.Advanced.AutoZero = DmmAuto.Once;
        }

        private void InitMultiPoint()
        {
            //Setup multipoint
            dmmSession.Trigger.MultiPoint.SampleCount = 1; //explicitly one
            dmmSession.Trigger.MultiPoint.TriggerCount = triggerCount;

            dmmSession.Trigger.MeasurementCompleteDestination = DmmMeasurementCompleteDestination.Ttl0;
            dmmSession.Trigger.MeasurementCompleteDestinationSlope = DmmSlope.Negative; //Negative = Failing edge, Positive = Rising edge
            dmmSession.Trigger.Source = DmmTriggerSource.Ttl1;
            dmmSession.Trigger.Slope = DmmSlope.Negative; //Negative = Failing edge, Positive = Rising edge
                                                            //dmmSession.Trigger.Configure(DmmTriggerSource.Ttl1, false); 
                                                            //dmmSession.Measurement.ReadMultiPointAsync();
                                                            //dmmSession.Measurement.FetchMultiPoint(10);
            dmmSession.Trigger.Source = DmmTriggerSource.Immediate;
            Trace.WriteLine(DeviceName + " MultiPoint mode initialized");
            dmmSession.Measurement.FetchMultiPointCompleted += OnMultiPointEventFinished;
            dmmSession.Measurement.FetchMultiPointCompleted += Measurement_FetchMultiPointCompleted; //Handle event finish
        }

        private void Measurement_FetchMultiPointCompleted(object sender, DmmMeasurementEventArgs<double[]> e)
        {
            CurrentState = MeasurementState.Finished;
        }

        /*
        private void Measurement_FetchMultiPointCompleted(object sender, DmmMeasurementEventArgs<double[]> e)
        {
            OnMultiPointEventFinished?.Invoke(sender, e);
        }
        */

        private void InitSingleMeasurement()
        { 
            //It takes effect when Measurement.Initiate called.
            dmmSession.Trigger.MultiPoint.TriggerCount = 1;
            dmmSession.Trigger.MeasurementCompleteDestination = DmmMeasurementCompleteDestination.None;
            dmmSession.Trigger.Source = DmmTriggerSource.Immediate;
        }

        protected void Close()
        {
            /* Reset to disable the output. */
            if (dmmSession != null)
            {
                dmmSession.Close();
            }
                
        }

        public double ReadSinglePoint()
        {
            double ret = 0.0;
            if (dmmSession == null || CurrentState == MeasurementState.Running)
            {
                return -999.0;
            }
            InitSingleMeasurement();
            ret = dmmSession.Measurement.Read();
            return ret;
        }

        public void StartMultiPoint(int tCount)
        {
            if (dmmSession == null || CurrentState == MeasurementState.Running)
            {
                return;
            }
            triggerCount = tCount;
            InitMultiPoint();
            dmmSession.Measurement.Initiate(); //start acquisition
        }

        public void Stop()
        {
            if (dmmSession != null)
            {
                dmmSession.Measurement.Abort();
            }
        }

        public void WaitForDoneEvent()
        {
            result = dmmSession.Measurement.FetchMultiPoint(triggerCount);
            if (result == null) throw new ArgumentNullException("Result array of " + DeviceName + " is null");
            else if (result.Length == 0) throw new IndexOutOfRangeException("Result array length of " + DeviceName + " zero");
            return;
        }

        //--------------------------------------------------------
        //Public functions

            

        public double[] GetMeasurementData()
        {
            return result;
        }

        public ref double[] GetMeasurementDataRef()
        {
            return ref result;
        }

        //--------------------------------------------------------
        //Private functions
        PrecisionTimeSpan SourceDelay
        {
            get
            {
                return new PrecisionTimeSpan(0);
            }
        }

        //--------------------------------------------------------
        //Private variables
        private NIDmm dmmSession;
        private int triggerCount = 0;
        private double[] result;
    }
}
