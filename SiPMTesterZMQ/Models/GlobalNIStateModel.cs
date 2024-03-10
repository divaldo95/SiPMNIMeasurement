using SiPMTesterInterface.Classes;
using SiPMTesterInterface.Enums;
using SiPMTesterInterface.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiPMTesterZMQ.Models
{
    public class GlobalNIStateModel
    {
        //Current measurement state
        public MeasurementState MeasurementState { get; set; }
        //Current IV measurement properties
        public IVMeasurementResponseModel UnderMeasurement {  get; set; }
        //List of IV measurements. Store elements until the server received all the data
        public List<IVMeasurementResponseModel> MeasurementStates { get; set; }
        //Current DMM resistance measurement properties
        public DMMResistanceMeasurementResponseModel DMMResistanceMeasurement { get; set;}
        //List of DMM resistance measurements. Store elements until the server received all the data
        public List<DMMResistanceMeasurementResponseModel> DMMResistanceMeasurements { get; set; }
        //True - IV | False - DMM
        public bool IsIVMeasurement { get; set; }
        //Currently launched measurement start model
        public NIIVStartModel CurrentIVStartModel { get; set; }
        public NIDMMStartModel CurrentDMMStartModel { get; set; }

        public bool GetDMMResMeasByIdentifier(MeasurementIdentifier id, out DMMResistanceMeasurementResponseModel resp)
        {
            int existingIndex = DMMResistanceMeasurements.FindIndex(item => item.Identifier.ID == id.ID && item.Identifier.Type == id.Type);

            if (existingIndex == -1)
            {
                resp = new DMMResistanceMeasurementResponseModel();
                return false;
            }
            else
            {
                resp = DMMResistanceMeasurements[existingIndex];
                return true;
            }
        }

        public bool GetIVMeasByIdentifier(MeasurementIdentifier id, out IVMeasurementResponseModel resp)
        {
            int existingIndex = MeasurementStates.FindIndex(item => item.Identifier.ID == id.ID && item.Identifier.Type == id.Type);

            if (existingIndex == -1)
            {
                resp = new IVMeasurementResponseModel();
                return false;
            }
            else
            {
                resp = MeasurementStates[existingIndex];
                return true;
            }
        }

        public void AppendUnderTestToList()
        {
            // Find the index of an existing item with the same identifier
            int existingIndex = MeasurementStates.FindIndex(item => item.Identifier == UnderMeasurement.Identifier);

            if (existingIndex != -1)
            {
                // Replace the existing item
                MeasurementStates[existingIndex] = UnderMeasurement;
            }
            else
            {
                // Add the new item to the list
                MeasurementStates.Add(UnderMeasurement);
            }

            // Remove the first item if the count reaches the specified value
            if (MeasurementStates.Count >= 100)
            {
                MeasurementStates.RemoveAt(0);
            }
        }

        public void AppendDMMResistanceToList()
        {
            // Find the index of an existing item with the same identifier
            int existingIndex = MeasurementStates.FindIndex(item => item.Identifier == DMMResistanceMeasurement.Identifier);

            if (existingIndex != -1)
            {
                // Replace the existing item
                DMMResistanceMeasurements[existingIndex] = DMMResistanceMeasurement;
            }
            else
            {
                // Add the new item to the list
                DMMResistanceMeasurements.Add(DMMResistanceMeasurement);
            }

            // Remove the first item if the count reaches the specified value
            if (DMMResistanceMeasurements.Count >= 100)
            {
                DMMResistanceMeasurements.RemoveAt(0);
            }
        }

        public GlobalNIStateModel()
        {
            MeasurementState = new MeasurementState();
            UnderMeasurement = new IVMeasurementResponseModel();
            MeasurementStates = new List<IVMeasurementResponseModel>();
            DMMResistanceMeasurement = new DMMResistanceMeasurementResponseModel();
            DMMResistanceMeasurements = new List<DMMResistanceMeasurementResponseModel>();
            CurrentIVStartModel = new NIIVStartModel();
            CurrentDMMStartModel = new NIDMMStartModel();
        }
    }
}
