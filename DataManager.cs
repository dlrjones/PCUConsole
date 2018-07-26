using System;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using OleDBDataManager;
using LogDefault;

namespace PCUConsole
{
    class DataManager
    {
        #region Class Variables
        private Hashtable dollarLimits = new Hashtable();
        private Hashtable multiplierValu = new Hashtable();
        private Hashtable patientPrice = new Hashtable();
        private ArrayList locations = new ArrayList();
        private DataSet itemsToProcess = new DataSet();
        private string connectStr = "";
        private string dbSelectStr = "";
        private string recordDate = "";
        private string location = "";
        private int attributeCount = 0;
        private int updateCount = 0;
        private bool goodToGo = true;
        private bool verbose = false;
        private bool debug = false;
        private bool trace = false;
        private NameValueCollection ConfigData = null;
        private LogManager lm = LogManager.GetInstance();
        private ErrorMonitor errMssg = ErrorMonitor.GetInstance();
        protected ODMDataFactory ODMDataSetFactory = null;
        #region Parameters
        public Hashtable DollarLimits
        {
            get { return dollarLimits; }
            set { dollarLimits = value; }
        }
        public Hashtable MultiplierValu
        {
            get { return multiplierValu; }
            set { multiplierValu = value; }
        }
        public ArrayList Locations
        {
            set { locations = value; }
        }        
        public int UpdateCount
        {
            get { return updateCount; }
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

        public DataManager()
        {
            if (trace) lm.Write("TRACE:  DataManager.DataManager(constructor)");
            lm.Write("PCUConsole:DataManager:(constructor)");
            ODMDataSetFactory = new ODMDataFactory();
            ConfigData = (NameValueCollection)ConfigurationSettings.GetConfig("PatientChargeUpdate");
            connectStr = ConfigData.Get("cnctBIAdmin");            
            attributeCount =  Convert.ToInt32(ConfigData.Get("attribCount"));
        }

        public void GetCurrentTierValues()
        {
            if (trace) lm.Write("TRACE:  DataManager.GetCurrentTierValues()");
            lm.Write("PCUConsole:DataManager:GetCurrentTierValues()");
            string select = "SELECT * FROM uwm_BIAdmin.dbo.uwm_PatientChargeTierLevels " +
                            "WHERE CHANGE_DATE = (select MAX(CHANGE_DATE) from uwm_BIAdmin.dbo.uwm_PatientChargeTierLevels) ";
            DBReadLatestTierValues(select);
        }

        private void DBReadLatestTierValues(string select)
        {
            if (trace) lm.Write("TRACE:  DataManager.DBReadLatestTierValues()");
            lm.Write("PCUConsole:DataManager:DBReadLatestTierValues()");
            dollarLimits.Clear();
            multiplierValu.Clear();

            ArrayList alResults = new ArrayList();
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = connectStr;
            Request.CommandType = CommandType.Text;
            Request.Command = select;
            try
            {
                alResults = ODMDataSetFactory. ExecuteDataReader(ref Request,attributeCount);
                ParseTierValuResults(alResults);
            }
            catch (Exception ex)
            {
                lm.Write("DataManager: DBReadLatestTierValues:  " + ex.Message);
                errMssg.Notify += "DataManager: DBReadLatestTierValues:  " + ex.Message + Environment.NewLine;
            }
        }
        /// <summary>
        /// reloads the hashtables used by PCUpdate to display the values
        /// </summary>
        /// <param name="results"></param>
        private void ParseTierValuResults(ArrayList results)
        { 
            if (trace) lm.Write("TRACE:  DataManager.ParseTierValuResults()");
            int indx = 1;
            int counter = 1;
            double dlrValu = 0;
            double multValu = 0;
            dollarLimits.Clear();
            multiplierValu.Clear();
            foreach (var item in results)
            {
                if (counter == attributeCount)
                {
                    recordDate = item.ToString();
                }
                else
                {
                    if (counter % 2 > 0)  //an odd counter value implies a $value, even a multiplier
                        {
                            dlrValu = Convert.ToDouble(item);                            
                        }
                        else
                        { //the "if..." below filters out the Tiers that haven't been used. Typically 4 or 5 Tiers are uses when the patient charge markup is changed.
                            multValu = Convert.ToDouble(item);
                            if (dlrValu + multValu > 0)
                            {
                                dollarLimits.Add(indx, dlrValu);
                                multiplierValu.Add(indx++, multValu);
                            }
                        }
                }
                counter++;
            }
            if(recordDate.Length > 0)
                lm.Write("PCUConsole.DataManager: " + dollarLimits.Count + " Tiers read from PatientChargeUpdate record dated  " + recordDate);
        }

        public void DBUpdate()
        {//INCREMENTAL    
            if (trace) lm.Write("TRACE:  DataManager.DBUpdate()");
            UpdatePatCharges uc = new UpdatePatCharges();
            foreach (string loc in locations)
            {
                if (loc == "mpous")
                {//THIS WAS FOR ITEMS STILL LISTED AS USING THE VIRTUAL LOCATION INV TOUCHSCAN ESI
                 //that location is gone and now we have to look at each mpous item to determine 
                 //the necessary updates. see the MPOUSCharges class (the PointOfUse class isn't used).

                    continue;
                    //if (uc.PatientPrice.Count > 0)
                    //{
                    //    PointOfUse pou = new PointOfUse();
                    //    pou.Verbose = verbose;
                    //    pou.Debug = debug;
                    //    pou.PriceToPatient = uc.PatientPrice;
                    //    pou.ProcessMPOUSChanges();
                    //    pou.RefreshPreviousValues();
                    //}
                }
                else
                {
                    uc.ConnectString = GetConnectString(loc);
                    uc.Location = loc;
                    uc.Verbose = verbose;
                    uc.Debug = debug;
                    uc.SQLSelect = BuildHEMM_UWMSelectString();
                    uc.DollarLimits = dollarLimits;
                    uc.MultiplierValu = multiplierValu;
                    uc.GetPatientPrice();
                    updateCount = uc.UpdateCount;
                }
            }
        }

        public void DBWrite()
        {//FULL UPDATE
            if (trace) lm.Write("TRACE:  DataManager.DBWrite()");
            PatChrgChanges pcc = new PatChrgChanges();
            
            pcc.DollarLimits = dollarLimits;
            pcc.MultiplierValu = multiplierValu;
            pcc.Verbose = verbose;
            pcc.Debug = debug;
            foreach (string loc in locations)
            {
                if (loc == "mpous")
                    pcc.SQLSelect = BuildMPOUSSelectString();
                else
                    pcc.SQLSelect = BuildHEMM_UWMSelectString();

                pcc.ConnectString = GetConnectString(loc);
                pcc.Location = loc;
                pcc.SetNewPatientCharges();
            }            
        }       
             
        private string GetConnectString(string loc)
        {
            if (trace) lm.Write("TRACE:  DataManager.GetConnectString()");
            if (loc == "hmc")
                connectStr = ConfigData.Get("cnctHEMM_HMC");
            else if (loc == "uwmc")
                connectStr = ConfigData.Get("cnctHEMM_UWMC");
            else if (loc == "mpous")
                connectStr = ConfigData.Get("cnctMPOUS");
            else if (loc == "nwh")
                connectStr = ConfigData.Get("cnctHEMM_NWH");
            else if (loc == "vmc")
                connectStr = ConfigData.Get("cnctHEMM_VMC");
            return connectStr;
        }
     
        private string BuildMPOUSSelectString()
        {
            if (trace) lm.Write("TRACE:  DataManager.BuildMPOUSSelectString()");
            string select =
                "SELECT DISTINCT  Alias_Id, [Location_Procedure_Code] " +
                "from [PointOfUseSupply].[dbo].[D_INVENTORY_ITEMS] DII  " +
                "JOIN AHI_ITEM_ALIAS AIA ON AIA.ITEM_ID = DII.Item_Id " +
                "WHERE (LEN([Location_Procedure_Code]) > 0 AND  [Location_Procedure_Code] <> '0') " +
                "AND ACTIVE_FLAG = 1 " +
                "AND BILLABLE_FLAG > 0 ";
            return select;          //DII.ITEM_ID,
        }

        private string BuildHEMM_UWMSelectString()
        {
            if (trace) lm.Write("TRACE:  DataManager.BuildHEMM_UWMSelectString()");
            string select =
                "SELECT  distinct  SI.ITEM_ID, IVP.PRICE, ITEM_NO, SI.PAT_CHRG_PRICE " +
                "FROM ITEM_VEND_PKG IVP " +
                "JOIN ITEM_VEND IV ON IVP.ITEM_VEND_ID = IV.ITEM_VEND_ID " +
                "JOIN SLOC_ITEM SI ON IVP.ITEM_VEND_ID = SI.ITEM_VEND_ID " +
                "JOIN ITEM ON ITEM.ITEM_ID = SI.ITEM_ID " +
                "WHERE IVP.SEQ_NO = (SELECT MAX (SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = SI.ITEM_VEND_ID) " +
                "AND IV.SEQ_NO = (SELECT MIN(SEQ_NO) FROM ITEM_VEND WHERE ITEM_VEND_ID = IVP.ITEM_VEND_ID) " +
                "AND LEN(SI.PAT_CHRG_NO) > 0 " +
                "AND SI.STAT = 1 " +
                "AND LEFT(SI.PAT_CHRG_NO,5) <> '40411' " +
                "AND IVP.PRICE > 0 " +
                "ORDER BY SI.ITEM_ID ";
            return select;
        }
     
    }
}
