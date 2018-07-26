using System;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using OleDBDataManager;
using LogDefault;

namespace PCUConsole
{
    class PCUCost
    {
        #region Class Variables
        protected DataSet itemCost = new DataSet();
        protected DataSet dsRefresh = new DataSet();
        protected DataSet dsPatChrgPrice = new DataSet();
        protected Hashtable changeItemCost = new Hashtable(); //for incremental updates, this holds the Item_ID and the new Current Cost of those items whose cost differ from the last run.
        protected Hashtable dollarLimits = new Hashtable();
        protected Hashtable multiplierValu = new Hashtable();
        protected Hashtable itemPatChrg = new Hashtable();
        protected Hashtable patientPrice = new Hashtable();
        protected Hashtable aliasLPC = new Hashtable();
        protected LogManager lm = LogManager.GetInstance();
        protected ErrorMonitor errMssg = ErrorMonitor.GetInstance();
        protected ODMDataFactory ODMDataSetFactory = null;
        private NameValueCollection ConfigData = null;
        protected string uwmConnectStr = "";
        protected string biAdminConnectStr = "";
        protected string mpousConnectStr = "";
        protected string location = "";
        protected string sqlSelect = "";
        protected string currentTask = "";
        protected char TAB = Convert.ToChar(9);
        protected bool OkToUpdate = false;
        protected bool debug = false;
        protected bool trace = false;
        protected bool verbose = false;
        protected bool suppressLogEntry = false;
        #endregion

        public PCUCost()
        {
            if (trace) lm.Write("TRACE:  PCUCost.PCUCost(constructor)");
            ODMDataSetFactory = new ODMDataFactory();
            ConfigData = (NameValueCollection)ConfigurationSettings.GetConfig("PatientChargeUpdate");
            trace = Convert.ToBoolean(ConfigData.Get("trace"));
            debug = Convert.ToBoolean(ConfigData.Get("debug"));
            OkToUpdate = Convert.ToBoolean(ConfigData.Get("updateTables"));
            uwmConnectStr = ConfigData.Get("cnctHEMM_HMC");
            biAdminConnectStr = ConfigData.Get("cnctBIAdmin");
            mpousConnectStr = ConfigData.Get("cnctMPOUS");
        }        

        protected void CalculatePatientPrice()
        {
            CalculatePatientPrice(changeItemCost);
        }

        protected void CalculatePatientPrice(Hashtable newPCPrice)         //(Hashtable ItemCost)
        {// set the patientPrice hashtable    
            if (trace) lm.Write("TRACE:  PCUCost.CalculatePatientPrice()");
            //INCREMENTAL             
            object item;
            double cost = 0.0;
            double patPrice = 0.0;
            patientPrice.Clear();
            try
            {
                foreach (DictionaryEntry itmCst in newPCPrice)
                {
                    item = itmCst.Key;
                    cost = Convert.ToDouble(itmCst.Value);
                    for (int indx = 1; indx <= dollarLimits.Count; indx++)
                    {
// indx is a key for the dollarLimits and multiplierValu hashtables, that's why it doesn't start at 0
                        if (cost <= Convert.ToDouble(dollarLimits[indx]))
                        {
// here's where the new price gets calculated for a DBUpdate
                           // patPrice = Math.Round(cost*Convert.ToDouble(multiplierValu[indx]), 2);
                            patPrice = cost * Convert.ToDouble(multiplierValu[indx]);
                            patPrice = RoundOffPatPrice(patPrice);
                            //if (patPrice < 10.00)
                            //    patPrice = Math.Round(patPrice, 1, MidpointRounding.AwayFromZero);
                            //else
                            //    patPrice = Math.Round(patPrice);

                            break;
                        }
                    }
                    patientPrice.Add(item, patPrice);
                    if(!suppressLogEntry)
                        lm.Write("PCUCost: CalculatePatientPrice:   (id-newPChg)" + TAB + item + TAB + patPrice);
                }
            }
            catch (Exception ex)
            {
                lm.Write("PCUCost: CalculatePatientPrice:  " + ex.Message);                
                errMssg.Notify += "PCUCost: CalculatePatientPrice" + Environment.NewLine;
            }
        }
        protected double RoundOffPatPrice(double patPrice)
        {
            if (patPrice < 10.00)
                patPrice = Math.Round(patPrice, 1, MidpointRounding.AwayFromZero);
            else
                patPrice = Math.Round(patPrice);
            return patPrice;
        }

        protected void CalculatePrice()
        {//this expects a dataset - itemCost
            if (trace) lm.Write("TRACE:  PCUCost.CalculatePrice()");
            int itemID = 0;
            double dlrLimit = 0.00;
            double cost = 0.00;
            double patPrice = 0.00;
            
            if (changeItemCost.Count == 0 && itemCost.Tables[0].Rows.Count == 0)
                GetCurrentItemCost();

            try
            {
                foreach (DataRow dr in itemCost.Tables[0].Rows)
                {
                    itemID = Convert.ToInt32(dr.ItemArray[0]);
                    if (itemID == 34946)
                        cost = 0.00;
                    cost = Convert.ToDouble(dr.ItemArray[1]);
                   for (int tierIndx = 1; tierIndx <= dollarLimits.Count; tierIndx++) //cycle through the 4 or so tiers of new markup values
                    {
                        dlrLimit = Convert.ToDouble(dollarLimits[tierIndx]);
                        if (cost <= dlrLimit)
                        {//**********this is where the new price gets calculated for a DBWrite
                            patPrice = cost * Convert.ToDouble(multiplierValu[tierIndx]);                          
                            if (patPrice < 10.00)
                                patPrice = Math.Round(patPrice, 1, MidpointRounding.AwayFromZero);
                            else
                                patPrice = Math.Round(patPrice);

                            if(!patientPrice.ContainsKey(itemID))
                                patientPrice.Add(itemID, patPrice);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lm.Write("PCUCost: CalculatePrice:  " + ex.Message);
                errMssg.Notify += "PCUCost: CalculatePrice:  " + ex.Message + Environment.NewLine;
            }
        }

        protected void GetCurrentItemCost()
        {//retrieves the current item & cost info from the prod db
            if (trace) lm.Write("TRACE:  PCUCost.GetCurrentItemCost()");
            ODMRequest Request = new ODMRequest();
            Request.CommandType = CommandType.Text;
            Request.ConnectString = uwmConnectStr;
            Request.Command = sqlSelect;
            try
            {
                itemCost = ODMDataSetFactory.ExecuteDataSetBuild(ref Request);
            }
            catch (Exception ex)
            {
                lm.Write("PCUCost: GetCurrentItemCost:  " + ex.Message);                               
                errMssg.Notify += "PCUCost: GetCurrentItemCost:  " + ex.Message + Environment.NewLine;
            }
        }

        protected void RefreshPreviousValuTable()
        {//previous value table for HEMM is uwm_IVPItemCost
            if (trace) lm.Write("TRACE:  PCUCost.RefreshPreviousValuTable()");
            //INCREMENTAL
            DataSet dsRefresh = new DataSet();
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = biAdminConnectStr;
            Request.CommandType = CommandType.Text;
            Request.Command = "TRUNCATE TABLE uwm_BIAdmin.dbo.uwm_IVPItemCost ";
            if (verbose)
                Console.WriteLine("Updating Previous Value Table: " + patientPrice.Keys.Count + " Changes.");

            try             //UPDATE uwm_BIAdmin.dbo.uwm_IVPItemCost -  This needs to be kept intact during testing and is only used in prod. 
            {
                ///////////PRODUCTION            From HERE//////////////   --UPDATES THE PatientItemCharge TABLE
                if (!debug)
                {
                    ODMDataSetFactory.ExecuteNonQuery(ref Request); // first truncate the uwm_IVPItemCost table
                    dsRefresh = BuildSQLRefresh(); //This reads from HEMM db
                    if (verbose)
                        Console.WriteLine("Updating the PatientItemCharge table with " + dsRefresh.Tables[0].Rows.Count +
                                          " records. This will take a moment or two");
                    foreach (DataRow dr in dsRefresh.Tables[0].Rows)
                    {
                        Request.Command = "INSERT INTO uwm_BIAdmin.dbo.uwm_IVPItemCost VALUES(" +
                                          dr.ItemArray[0] + "," + dr.ItemArray[1] + ",'" +
                                          dr.ItemArray[2].ToString().Trim() + "')";
                        ODMDataSetFactory.ExecuteNonQuery(ref Request);
                    }
                }
                // To HERE////////////// 
            }
            catch (Exception ex)
            {
                lm.Write("PCUCost: RefreshPreviousValuTable:  " + ex.Message);
                errMssg.Notify += "PCUCost: RefreshPreviousValuTable:  " + ex.Message + Environment.NewLine;
            }
        }

        protected DataSet BuildSQLRefresh()
        {//reads from the PROD. HEMM db. The resulting dataset is used to refill the uwm_BIAdmin.dbo.uwm_IVPItemCost table.
            if (trace) lm.Write("TRACE:  PCUCost.BuildSQLRefresh()");
            int itemID = 0;
            double cost = 0.0;
            string itemNo = "";
            string sqlRefresh = "SELECT  distinct  SI.ITEM_ID, IVP.PRICE, ITEM_NO " +
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

            ODMRequest Request = new ODMRequest();
            Request.ConnectString = uwmConnectStr;
            Request.CommandType = CommandType.Text;
            Request.Command = sqlRefresh;
            if (verbose)
                Console.WriteLine("Updating Previous Value Table: " + patientPrice.Keys.Count + " Changes.");
            try
            {
                dsRefresh = ODMDataSetFactory.ExecuteDataSetBuild(ref Request);
            }
            catch (Exception ex)
            {
                lm.Write("PCUCost: BuildSQLRefresh:  " + ex.Message);
                errMssg.Notify += "PCUCost: BuildSQLRefresh:  " + ex.Message + Environment.NewLine;
            }
            return dsRefresh;
        }

        protected Hashtable ConvertToHashTable(DataSet dSet)
        {
            if (trace) lm.Write("TRACE:  PCUCost.ConvertToHashTable()");
            Hashtable htHash = new Hashtable();
            try
            {
            foreach (DataRow dr in dSet.Tables[0].Rows)
            {
                if (htHash.ContainsKey(Convert.ToInt32(dr.ItemArray[0])))
                    continue;
                htHash.Add(Convert.ToInt32(dr.ItemArray[0]), Convert.ToDouble(dr.ItemArray[1]));
            }
                 }
            catch (Exception ex)
            {
                lm.Write("PCUCost: ConvertToHashTable:  " + ex.Message);               
                errMssg.Notify += "PCUCost: ConvertToHashTable:  " + ex.Message + Environment.NewLine;
            }
            return htHash;
        }

        protected void LogCurrentPatPrice(DataSet dSet)
        {//not used
            if (trace) lm.Write("TRACE:  PCUCost.LogCurrentPatPrice()");
            try
            {
                foreach (DataRow dr in dSet.Tables[0].Rows)
                {
                   lm.Write("CurrPatPrice: (id,price)" + TAB + dr.ItemArray[0] + TAB + dr.ItemArray[3]);
                }
            }
            catch (Exception ex)
            {
                lm.Write("PCUCost: LogCurrentPatPrice:  " + ex.Message);
                errMssg.Notify += "PCUCost: LogCurrentPatPrice:  " + ex.Message + Environment.NewLine;
            }
        }

       
    }
}
