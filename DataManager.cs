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
        private Hashtable xpnse_accnt = new Hashtable();
        private Hashtable prevCostTable = new Hashtable();
        private ArrayList locations = new ArrayList();
        private DataSet itemsToProcess = new DataSet();
        private string connectStr = "";
        private string dbSelectStr = "";
        private string recordDate = "";
        private string location = "";
        private int attributeCount = 0;
        private int updateCount = 0;
        private bool goodToGo = true;
        private bool OkToUpdate = true;
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
        public bool OKToUpdate
        {
            set { OkToUpdate = value; }
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

        public void GetCurrentTierValues(string hosp)
        {
            if (trace) lm.Write("TRACE:  DataManager.GetCurrentTierValues()");
            lm.Write("PCUConsole:DataManager:GetCurrentTierValues()");
            string select = "SELECT * FROM uwm_BIAdmin.dbo.uwm_New_PatientChargeTierLevels " +
                            "WHERE CHANGE_DATE = (select MAX(CHANGE_DATE) from uwm_BIAdmin.dbo.uwm_New_PatientChargeTierLevels) " +
                            "AND HOSP = '" + hosp + "' ";
            DBReadLatestTierValues(select);
        }

        public ArrayList RunQuery(string connectStr, string sqlQuery)
        {
            if (trace) lm.Write("TRACE:  DataManager.RunQuery()");
            ArrayList alResults = new ArrayList();
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = connectStr;
            Request.CommandType = CommandType.Text;
            Request.Command = sqlQuery;
            try
            {
                alResults = ODMDataSetFactory.ExecuteDataReader(ref Request, attributeCount);                
            }
            catch (Exception ex)
            {
                lm.Write("DataManager: RunQuery:  " + ex.Message);
                errMssg.Notify += "DataManager: RunQuery:  " + ex.Message + Environment.NewLine;
            }
            return alResults;
        }

        private void DBReadLatestTierValues(string select)
        {
            if (trace) lm.Write("TRACE:  DataManager.DBReadLatestTierValues()");
            lm.Write("PCUConsole:DataManager:DBReadLatestTierValues()");
            dollarLimits.Clear();
            multiplierValu.Clear();
            connectStr = ConfigData.Get("cnctBIAdmin");
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
                if (item.ToString() == "hmc" || item.ToString() == "mpous" || item.ToString() == "uwmc" || item.ToString() == "nw")
                    continue;
                if (counter == attributeCount-1)
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
         //  loc =  ("hmc");("uwmc");("mpous");("nwh");("val");

            if (trace) lm.Write("TRACE:  DataManager.DBUpdate()");
            UpdatePatCharges upc = new UpdatePatCharges();
            foreach (string loc in locations)
            {
                lm.Write("Location: " + loc);
                if (loc == "mpous")
                {
                    #region THIS WAS FOR ITEMS STILL LISTED AS USING THE VIRTUAL LOCATION INV TOUCHSCAN ESI
                    //that location is gone and now we have to look at each mpous item to determine 
                    //the necessary updates. see the MPOUSCharges class (the PointOfUse class isn't used).

                    continue;                   
                    #endregion                   
                }
                else
                {                    
                    upc.Location = loc;
                    upc.Verbose = verbose;
                    upc.Debug = debug;
                    upc.ConnectString = GetConnectString(loc);
                    upc.ChangeItemCost = new Hashtable();
                    upc.SQLSelect = BuildHEMM_UWMSelectString(xpnse_accnt[loc].ToString());
                    GetCurrentTierValues(loc);  //tier values can change from one entity to another - from here you get dollarLimits and multiplierValu
                    upc.DollarLimits = dollarLimits;
                    upc.MultiplierValu = multiplierValu;
                    upc.PrevCostTable = prevCostTable;
                    upc.GetPatientPrice();
                    updateCount = upc.UpdateCount;
                }
            }
        }

        public void ZeroOutValues(string biAdminConnectStr)
        {//set the cost for all items in the uwm_IVPItemCost table to 0.00;
         //this will force all items to be updated, not just those that have changed
            if (trace) lm.Write("TRACE:  DataManager.ZeroOutValues()");
            foreach (string hosp in locations)
            {
                if (hosp == "mpous")
                    continue;
                ODMRequest Request = new ODMRequest();
                Request.ConnectString = biAdminConnectStr;
                Request.CommandType = CommandType.Text;
                Request.Command = "UPDATE [uwm_BIAdmin].[dbo]." + prevCostTable[hosp].ToString() + " " +
                                   "SET COST = 0.00 ";
                //"WHERE ITEM_ID = 1091";
                if (OkToUpdate)
                    ODMDataSetFactory.ExecuteNonQuery(ref Request);
            }
        }

        public DataSet GetReprocData(string connectStr,string inClause)
        {
            if (trace) lm.Write("TRACE:  DataManager.GetReprocData()");
            DataSet dsReproc = new DataSet();
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = connectStr;
            Request.CommandType = CommandType.Text;
            Request.Command = BuildReprocQuery(inClause);
            try
            {
                dsReproc = ODMDataSetFactory.ExecuteDataSetBuild(ref Request);                
            }
            catch(Exception ex)
            {
                lm.Write("DataManager: GetReprocData:  " + ex.Message);
                errMssg.Notify += "DataManager: DBReadLatGetReprocDataestTierValues:  " + ex.Message + Environment.NewLine;
            }
            return dsReproc;
        }

        public string GetOEMCost(string connectStr,string vendName, string itemNo, string ctlgNo)
        {
            if (trace) lm.Write("TRACE:  DataManager.GetOEMCost()");
            ArrayList oemCost = new ArrayList(); 
            DataSet dsReproc = new DataSet();
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = connectStr;
            Request.CommandType = CommandType.Text;
            Request.Command = BuildCostQuery(vendName,itemNo,ctlgNo);
            try
            {
                oemCost = ODMDataSetFactory.ExecuteDataReader(ref Request);
            }
            catch (Exception ex)
            {
                lm.Write("DataManager: GetReprocData:  " + ex.Message);
                errMssg.Notify += "DataManager: GetReprocData:  " + ex.Message + Environment.NewLine;
            }
            return oemCost.Count > 0 ? oemCost[0].ToString() : ""; 
        }

        public string GetSecondaryVendorCost(string connectStr, string itemNo)
        {//itemNo is the number of the reproc item without the "R"
            if (trace) lm.Write("TRACE:  DataManager.GetSecondaryVendorCost()");            
            ArrayList vendor = new ArrayList();
            DataSet dsReproc = new DataSet();
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = connectStr;
            Request.CommandType = CommandType.Text;
            Request.Command = BuildSecondaryVendCostQuery(itemNo);
            try
            {
                vendor = ODMDataSetFactory.ExecuteDataReader(ref Request);
            }
            catch (Exception ex)
            {
                lm.Write("DataManager: GetSecondaryVendorCost:  " + ex.Message);
                errMssg.Notify += "DataManager: GetSecondaryVendorCost:  " + ex.Message + Environment.NewLine;
            }
            return vendor.Count > 0 ? vendor[0].ToString() : "";
        }

        public void DBWrite()
        {//FULL UPDATE -- The Full Update track has been simplified so that methods distinctly written for the Full track aren't necessary
            if (trace) lm.Write("TRACE:  DataManager.DBWrite()");
            //PatChrgChanges pcc = new PatChrgChanges();
            
            //pcc.DollarLimits = dollarLimits;
            //pcc.MultiplierValu = multiplierValu;
            //pcc.Verbose = verbose;
            //pcc.Debug = debug;
            //foreach (string loc in locations)
            //{
            //    if (loc == "mpous")
            //        pcc.SQLSelect = BuildMPOUSSelectString();
            //    else
            //        pcc.SQLSelect = BuildHEMM_UWMSelectString(xpnse_accnt[loc].ToString());

            //    pcc.ConnectString = GetConnectString(loc);
            //    pcc.PrevCostTable = prevCostTable;
            //    pcc.Location = loc;
            //    pcc.SetNewPatientCharges();
            //}            
        }       
             
        private string GetConnectString(string loc)
        {
            if (trace) lm.Write("TRACE:  DataManager.GetConnectString()");
            if (loc == "hmc")
                connectStr = ConfigData.Get("cnctHEMM_HMC");
                //connectStr = ConfigData.Get("cnctHEMM_TEST");
            else if (loc == "uwmc")
                connectStr = ConfigData.Get("cnctUWMC");
                //connectStr = ConfigData.Get("cnctHEMM_UWMC");
            else if (loc == "mpous")
                connectStr = ConfigData.Get("cnctMPOUS");
            else if (loc == "nwh")
                connectStr = ConfigData.Get("cnctHEMM_NWH");
            else if (loc == "vmc")
                connectStr = ConfigData.Get("cnctHEMM_VMC");
            return connectStr;
        }       

        private string BuildSecondaryVendCostQuery(string itemNo)
        {
            if (trace) lm.Write("TRACE:  DataManager.BuildSecondaryVendCostQuery()");            
            string select = "SELECT DISTINCT PRICE " +
                            "FROM ITEM_VEND IV " +
                            "JOIN ITEM_VEND_PKG IVP ON IVP.ITEM_VEND_ID = IV.ITEM_VEND_ID " +
                            "WHERE VEND_ID = " +
                                "(SELECT IV.VEND_ID " +
                                "FROM ITEM " +
                                "JOIN ITEM_VEND IV ON IV.ITEM_ID = ITEM.ITEM_ID " +
                                "JOIN VEND ON VEND.VEND_ID = IV.VEND_ID " +
                                "WHERE CORP_ID = 1000 " +
                                "AND IV.SEQ_NO = (SELECT MAX(SEQ_NO) FROM ITEM_VEND WHERE ITEM_VEND.ITEM_ID = ITEM.ITEM_ID)  " +
                                "AND ITEM.ITEM_NO = '" + itemNo + "') " +
                            "AND ITEM_ID = (SELECT ITEM_ID FROM ITEM WHERE ITEM_NO = '" + itemNo + "') " +
                            "AND IVP.SEQ_NO = (SELECT MAX(SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = IV.ITEM_VEND_ID)";
            return select;
        }

        private string BuildCostQuery(string vendName,string itemNo, string ctlgNo)
        {
            if (trace) lm.Write("TRACE:  DataManager.BuildCostQuery()");
            string select = "SELECT PRICE " +
                            "FROM ITEM " +
                            "JOIN ITEM_VEND IV ON IV.ITEM_ID = ITEM.ITEM_ID " +
                            "JOIN ITEM_VEND_PKG IVP ON IVP.ITEM_VEND_ID = IV.ITEM_VEND_ID " +
                            "JOIN VEND ON VEND.VEND_ID = IV.VEND_ID " +
                            "WHERE CORP_ID = 1000 " +
                            "AND IVP.SEQ_NO = (SELECT MAX(SEQ_NO) " +
                                                "FROM ITEM_VEND_PKG " +
                                                "WHERE ITEM_VEND_ID = IV.ITEM_VEND_ID) " +
                            "AND ITEM_NO = '" + itemNo + "' " +
                            "AND ITEM.CTLG_NO = '" + ctlgNo + "' " +
                            "AND ITEM.STAT = 1 ";
            if (vendName.Length > 0)
                select += "AND VEND.NAME = '" + vendName + "' ";
            return select;
        }

        private string BuildReprocQuery(string inClause)
        {
            if (trace) lm.Write("TRACE:  DataManager.BuildReprocQuery()");            
            string select = "SELECT VEND.NAME,IV.ITEM_ID, RTRIM(ITEM_NO) ITEM_NO, RTRIM(ITEM.CTLG_NO) [ITEM CAT NO],IVP.PRICE " +
                            "FROM ITEM_VEND_PKG IVP " +
                            "JOIN ITEM_VEND IV ON IVP.ITEM_VEND_ID = IV.ITEM_VEND_ID " + 
                            "JOIN ITEM ON ITEM.ITEM_ID = IV.ITEM_ID " +
                            "JOIN VEND ON VEND.VEND_ID = IV.VEND_ID " +
                            "WHERE CORP_ID = 1000 " +
                            "AND IV.ITEM_ID IN( " + inClause + ") " +
                            "AND IVP.SEQ_NO = (SELECT MAX (SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = IV.ITEM_VEND_ID) " +
                            "AND RIGHT(RTRIM(ITEM_NO),1) = 'R' ";
            return select;
        }

        private string BuildMPOUSSelectString()
        {
            if (trace) lm.Write("TRACE:  DataManager.BuildMPOUSSelectString()");           
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

        private string BuildHEMM_UWMSelectString(string expAccnt)
        {
            if (trace) lm.Write("TRACE:  DataManager.BuildHEMM_UWMSelectString()");
            string select =
                "SELECT  distinct  SI.ITEM_ID, IVP.PRICE, ITEM_NO, SI.PAT_CHRG_PRICE, EXP_ACCT_NO " +
                "FROM ITEM_VEND_PKG IVP " +
                "JOIN ITEM_VEND IV ON IVP.ITEM_VEND_ID = IV.ITEM_VEND_ID " +
                "JOIN SLOC_ITEM SI ON IVP.ITEM_VEND_ID = SI.ITEM_VEND_ID " +
                "JOIN ITEM ON ITEM.ITEM_ID = SI.ITEM_ID " +
                "JOIN ITEM_CORP_ACCT ON ITEM.ITEM_ID = ITEM_CORP_ACCT.ITEM_ID " +
                "WHERE IVP.SEQ_NO = (SELECT MAX (SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = SI.ITEM_VEND_ID) " +
                "AND IVP.PRICE > 0 " +
                "AND IV.SEQ_NO = (SELECT MIN(SEQ_NO) FROM ITEM_VEND WHERE ITEM_VEND_ID = IVP.ITEM_VEND_ID) " +
                "AND LEN(SI.PAT_CHRG_NO) > 0 " +
                "AND SI.STAT = 1 " +
                "AND LEFT(SI.PAT_CHRG_NO,5) <> '40411' " +
                "AND ((IVP.PRICE > 49.99 AND (RIGHT(SI.PAT_CHRG_NO,5) <> '00000') " +
                "OR(IVP.PRICE < 50 AND(RIGHT(SI.PAT_CHRG_NO, 5) <> '00000')))" +
                "OR (EXP_ACCT_NO = " + expAccnt + " AND IVP.PRICE > 0))" +                
                "ORDER BY SI.ITEM_ID ";
            return select;
        }
     
    }
}
