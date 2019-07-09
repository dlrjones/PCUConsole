using System;
using System.Collections;
using LogDefault;
using System.Collections.Specialized;
using System.Configuration;

namespace PCUConsole
{    
    class PCUpdate
    {
        #region Class Variables
        private DataManager dm = new DataManager();
        private Hashtable dollarLimits = new Hashtable();
        private Hashtable multiplierValu = new Hashtable();        
        private Hashtable xpnse_accnt = new Hashtable();
        private Hashtable prevCostTable = new Hashtable();
        private ArrayList locations = new ArrayList();
        private static LogManager lm = LogManager.GetInstance();
   //     private string hospital = "";
        private ErrorMonitor errMssg = ErrorMonitor.GetInstance();
        private byte locationCode = 0;
        private string currentTask = ""; //"incremental" or "full";
        private NameValueCollection ConfigData = null;
        private bool verbose = false;
        private bool debug = false;
        private bool trace = false;
        private bool OkToUpdate = false;
        private int updateCount = 0;

        #region Parameters
        public Hashtable DollarLimits
        {
            get { return dollarLimits; }
        }
        public Hashtable MultiplierValu
        {
            get { return multiplierValu; }
        }
        public int UpdateCount
        {
            get { return updateCount; }
        }
        public ArrayList Locations
        {
            get { return locations; }
        }
        public byte LocationCode
        {
            set { locationCode = value; }
        }
        public string CurrentTask
        {
            set { currentTask = value; }
        }
        public Hashtable Xpnse_accnt
        {
            set { xpnse_accnt = value; }
        }
        public Hashtable PrevCostTable
        {
            set { prevCostTable = value; }
        }
        public bool Verbose
        {
            set { verbose = value; }
        }
        public bool Debug
        {
            set { debug = value; }
        }
        public bool Trace
        {
            set { trace = value; }
        }
        #endregion
        #endregion

        public void Process()
        {           
            ConfigData = (NameValueCollection)ConfigurationSettings.GetConfig("PatientChargeUpdate");
            OkToUpdate = Convert.ToBoolean(ConfigData.Get("updateTables"));
            if (trace) lm.Write("TRACE:  PCUpdate.Process()");
            try
            {
                ParseLocationCode();
                
                dm.Locations = locations;
                dm.Verbose = verbose;
                dm.Debug = debug;
                dm.Trace = trace;
                dm.Xpnse_accnt = xpnse_accnt;
                dm.PrevCostTable = prevCostTable;
           //     ReadPCValues("HMC");

                if (currentTask.Equals("full"))
                {//FULL UPDATE
                    ZeroCurrentPCValues(ConfigData.Get("cnctBIAdmin"));
                }
                //INCREMENTAL
                UpdateCurrentPCValues();
                updateCount = dm.UpdateCount;

                //if (currentTask.Equals("incremental"))
                //{//INCREMENTAL
                //    UpdateCurrentPCValues();
                //    updateCount = dm.UpdateCount;
                //}
                //else
                //{//FULL UPDATE
                //    SetNewPCValues();
                //}
            }
            catch(Exception ex)
            {
                lm.Write("PCUpdate: Process:  " + ex.Message);
                errMssg.Notify += "PCUpdate: Process:  " + ex.Message + Environment.NewLine;
            }
        }

        private void ParseLocationCode()
        {
            if (trace) lm.Write("TRACE:  PCUpdate.ParseLocationCode()");
            #region convert location code
            switch (locationCode)
            {
                case 31:    //11111
                    locations.Add("hmc");
                    locations.Add("uwmc");
                    locations.Add("mpous");
                    locations.Add("nwh");
                    locations.Add("val");
                    break;
                case 30:    ///11110
                     locations.Add("hmc");
                    locations.Add("uwmc");
                    locations.Add("mpous");
                    locations.Add("nwh");
                    break;
                case 29:    //11101
                    locations.Add("hmc");
                    locations.Add("uwmc");
                    locations.Add("mpous");
                    locations.Add("val");
                    break;
                case 28:    //11100
                    locations.Add("hmc");
                    locations.Add("uwmc");
                    locations.Add("mpous");
                    break;
                case 27:    //11011
                     locations.Add("hmc");
                    locations.Add("uwmc");
                    locations.Add("nwh");
                    locations.Add("val");
                    break;
                case 26:    //11010
                    locations.Add("hmc");
                    locations.Add("uwmc");
                    locations.Add("nwh");
                    break;
                case 25:    //11001
                     locations.Add("hmc");
                    locations.Add("uwmc");
                    locations.Add("val");
                    break;
                case 24:    //11000
                     locations.Add("hmc");
                    locations.Add("uwmc");
                    break;
                case 23:    //10111
                     locations.Add("hmc");
                    locations.Add("mpous");
                    locations.Add("nwh");
                    locations.Add("val");
                    break;
                case 22:    //10110
                    locations.Add("hmc");
                    locations.Add("mpous");
                    locations.Add("nwh");
                    break;
                case 21:    //
                    locations.Add("hmc");
                    locations.Add("mpous");
                    locations.Add("val");
                    break;
                case 20:
                     locations.Add("hmc");
                    locations.Add("mpous");
                    break;
                case 19:
                    locations.Add("hmc");
                    locations.Add("nwh");
                    locations.Add("val");
                    break;
                case 18:
                    locations.Add("hmc");
                    locations.Add("nwh");
                    break;
                case 17:
                    locations.Add("hmc");
                    locations.Add("val");
                    break;
                case 16:
                    locations.Add("hmc");
                    break;
                case 15:
                    locations.Add("uwmc");
                    locations.Add("mpous");
                    locations.Add("nwh");
                    locations.Add("val");
                    break;
                case 14:
                    locations.Add("uwmc");
                    locations.Add("mpous");
                    locations.Add("nwh");
                    break;
                case 13:
                    locations.Add("uwmc");
                    locations.Add("mpous");
                    locations.Add("val");
                    break;
                case 12:
                    locations.Add("uwmc");
                    locations.Add("mpous");
                    break;
                case 11:
                    locations.Add("uwmc");
                    locations.Add("nwh");
                    locations.Add("val");
                    break;
                case 10:
                    locations.Add("uwmc");
                    locations.Add("nwh");
                    break;
                case 9:
                    locations.Add("uwmc");                    
                    locations.Add("val");
                    break;
                case 8:
                    locations.Add("uwmc");
                    break;
                case 7:
                    locations.Add("mpous");
                    locations.Add("nwh");
                    locations.Add("val");
                    break;
                case 6:
                    locations.Add("mpous");
                    locations.Add("nwh");
                    break;
                case 5:
                    locations.Add("mpous");
                    locations.Add("val");
                    break;
                case 4:
                    locations.Add("mpous");
                    break;
                case 3:
                    locations.Add("nwh");
                    locations.Add("val");
                    break;
                case 2:
                    locations.Add("nwh");
                    break;
                case 1:
                    locations.Add("val");
                    break;
                default:
                    break;
            }
            #endregion
        }

        private void ReadPCValues(string hosp)
        {
            if (trace) lm.Write("TRACE:  PCUpdate.ReadPCValues()");
            dm.GetCurrentTierValues(hosp);
            dollarLimits = dm.DollarLimits;
            multiplierValu = dm.MultiplierValu;
        }

        private void ZeroCurrentPCValues(string cnctStr)
        {// this is done for full updates
            if (trace) lm.Write("TRACE:  PCUpdate.ZeroCurrnetPCValues");
            if (verbose) Console.WriteLine("Full Update");
            
            dm.OKToUpdate = OkToUpdate;
            dm.ZeroOutValues(cnctStr);
        }

        private void UpdateCurrentPCValues()
        {//INCREMENTAL
            if (trace) lm.Write("TRACE:  PCUpdate.UpdateCurrentPCValues");
            if(verbose) Console.WriteLine("Incremental Update");

            dm.DBUpdate();
        }

        private void SetNewPCValues()
        {//FULL UPDATE -- The Full Update track has been simplified so that methods distinctly written for the Full track aren't necessary
            if (trace) lm.Write("TRACE:  PCUpdate.SetNewPCValues()");
            //if (verbose)  Console.WriteLine("Full Update");

            //dm.DBWrite();            
        }

    }
}
