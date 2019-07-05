using System;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using OleDBDataManager;
using LogDefault;

namespace PCUConsole
{
    class ItemMarkup
    {
        #region class variables
        protected int itemID = 0;
        protected string crntCost = "";
        protected double multiplier = 0;
        protected string vendorName = "";
        protected string itemNmbr = "";
        protected string catalogNmbr = "";        
        public int ItemID
        {
            get { return itemID; }
            set { itemID = value; }
        }
        public double Multiplier
        {
            get { return multiplier; }
            set { multiplier = value; }
        }
        public string CrntCost
        {
            get { return crntCost; }
        }
        public string ItemNmbr
        {
            get { return itemNmbr; }
            set { itemNmbr = value; }
        }
        public string VendorName
        {
            get { return vendorName; }
        }
        public string CatalogNmbr
        {
            get { return catalogNmbr; }
        }        
        #endregion

        public void AddItemIDCost(int itmID, string cost)
        {
            itemID = itmID;
            crntCost = cost;
        }
        public void AddItemNOCost(string itemNo, string cost)
        {
            itemNmbr = itemNo;
            crntCost = cost;
        }
        public void AddVendItemCtlg(string vendName, string itemNo, string ctlgNo)
        {
            vendorName = vendName;
            itemNmbr = itemNo;
            catalogNmbr = ctlgNo;
        }
        public void AddItemNOItemID(string itemNo, int itemId )
        {
            itemNmbr = itemNo;
            itemID = itemId;
        }
    }

    class PCUCost
    {
        #region Class Variables
        protected DataSet itemCost = new DataSet();
        protected DataSet dsRefresh = new DataSet();
        protected DataSet dsPatChrgPrice = new DataSet();
        protected Hashtable changeItemCost = new Hashtable(); //for incremental updates, this holds the Item_ID/ItemMarkup and the new Current Cost of those items whose cost differ from the last run.
        protected Hashtable dollarLimits = new Hashtable();
        protected Hashtable multiplierValu = new Hashtable();
        protected Hashtable itemPatChrg = new Hashtable();
        protected Hashtable patientPrice = new Hashtable();
        protected Hashtable aliasLPC = new Hashtable();
        protected Hashtable prevCostTable = new Hashtable();
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
     //   protected string hospital = "";        
        protected char TAB = Convert.ToChar(9);
        protected bool OkToUpdate = false;
        protected bool debug = false;
        protected bool trace = false;
        protected bool verbose = false;
        protected bool suppressLogEntry = false;
        private ItemMarkup itemMUFull;

        public Hashtable PrevCostTable {       
            get { return prevCostTable; }
            set { prevCostTable = value; }
        }
        public string Location
        {
            get { return location; }
            set { location = value; }
        }
        #endregion

        public PCUCost()
        {
            if (trace) lm.Write("TRACE:  PCUCost.PCUCost(constructor)");
            ODMDataSetFactory = new ODMDataFactory();
            ConfigData = (NameValueCollection)ConfigurationSettings.GetConfig("PatientChargeUpdate");
            trace = Convert.ToBoolean(ConfigData.Get("trace"));
            debug = Convert.ToBoolean(ConfigData.Get("debug"));
            OkToUpdate = Convert.ToBoolean(ConfigData.Get("updateTables"));
            uwmConnectStr = ConfigData.Get("cnctHEMM_TEST");
            biAdminConnectStr = ConfigData.Get("cnctBIAdmin");
            mpousConnectStr = ConfigData.Get("cnctMPOUS_TEST");
        }        

        protected void CalculatePatientPrice()
        {
            CalculatePatientPrice(changeItemCost);
        }

        protected void CalculatePatientPrice(Hashtable newPCPrice)         // itemID/ItemMarkup object or for mpous itemNO/ItemMarkup object
        {// set the patientPrice hashtable    
            if (trace) lm.Write("TRACE:  PCUCost.CalculatePatientPrice()");
            //INCREMENTAL     
            ItemMarkup itmMrkUp;
            object itemID;
            double cost = 0.0;
            double multiplier = 0.0;
            double patPrice = 0.0;
            patientPrice.Clear();

            try
            {   //at this point, newPCPrice is a hashtable with itemID/ItemMarkup object
                //for each itemID in the hashtable look at its multiplier value in the ItemMarkup
                //If it has one then set the multiplier value to it otherwise set the multiplier
                //to multiplierValu[indx]
                foreach (DictionaryEntry itmCst in newPCPrice)
                {
                    itemID = itmCst.Key;
                            //if(Convert.ToInt32(itemID) == 2253882)        //a place to check the pchrg calculation
                            //    itemID = itemID = itmCst.Key;
                    itmMrkUp = (ItemMarkup)itmCst.Value;                                        
                    cost = Convert.ToDouble(itmMrkUp.CrntCost);
                    for (int indx = 1; indx <= dollarLimits.Count; indx++)
                    {
                        multiplier = Convert.ToDouble(itmMrkUp.Multiplier);
                        if (multiplier == 0.0)
                        {
                            multiplier = Convert.ToDouble(multiplierValu[indx]);
                        }

                        // indx is a key for the dollarLimits and multiplierValu hashtables, that's why it doesn't start at 0
                        if (cost <= Convert.ToDouble(dollarLimits[indx]))
                        {
                        // here's where the new price gets calculated for a DBUpdate                          
                            patPrice = cost + (cost * multiplier);
                           // patPrice = RoundOffPatPrice(patPrice); //stopped doing this for hmc as of 7/1/19                            
                            break;
                        }
                        multiplier = 0.0;
                    }
                    patientPrice.Add(itemID, patPrice);  //HEMM itemID
                    if(!suppressLogEntry)
                        lm.Write("PCUCost: CalculatePatientPrice:   (id-newPChg)" + TAB + itemID + TAB + patPrice);
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
            if (trace) lm.Write("TRACE:  PCUCost.RoundOffPatPrice()");
            if (patPrice < 10.00)
                patPrice = Math.Round(patPrice, 1, MidpointRounding.AwayFromZero);
            else
                patPrice = Math.Round(patPrice);
            return patPrice;
        }

        protected void CalculatePrice()
        {   // FULL
            //this expects a dataset - itemCost
            if (trace) lm.Write("TRACE:  PCUCost.CalculatePrice()");
            int itemID = 0;
            double dlrLimit = 0.00;
            double cost = 0.00;
            double patPrice = 0.00;
            
            if (changeItemCost.Count == 0 && itemCost.Tables[0].Rows.Count == 0)
                GetCurrentItemCost();
            //NEW STUFF
            // change itemCost dataset into an Hashtable of IemMarkup objects -> itemID/itemMUFull
            //itemID,price,itemNO
            //END NEW STUFF
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
            string pvTable = prevCostTable[location].ToString();
            if (trace) lm.Write("TRACE:  PCUCost.RefreshPreviousValuTable()");
            //INCREMENTAL
            DataSet dsRefresh = new DataSet();
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = biAdminConnectStr;
            Request.CommandType = CommandType.Text;
            Request.Command = "TRUNCATE TABLE [uwm_BIAdmin].[dbo]." + pvTable + " ";
            if (verbose)
                Console.WriteLine("Updating Previous Value Table: " + patientPrice.Keys.Count + " Changes.");

            try             //UPDATE uwm_BIAdmin.dbo.uwm_IVPItemCost -  This needs to be kept intact during testing and is only used in prod. 
            {
                ///////////PRODUCTION            From HERE....//////////////   --UPDATES THE PatientItemCharge TABLE
                if (!debug)
                {
                    ODMDataSetFactory.ExecuteNonQuery(ref Request); // first truncate the uwm_IVPItemCost table
                    dsRefresh = BuildSQLRefresh(); //This reads from HEMM db
                    if (verbose)
                        Console.WriteLine("Updating the PatientItemCharge table with " + dsRefresh.Tables[0].Rows.Count +
                                          " records. This will take a moment or two");
                    foreach (DataRow dr in dsRefresh.Tables[0].Rows)
                    { //[0]=ITEM_ID   [1]=COST  [2]=ITEM_NO
                        Request.Command = "INSERT INTO uwm_BIAdmin.dbo." + pvTable + " VALUES(" +
                                          dr.ItemArray[0] + "," + dr.ItemArray[1] + ",'" +
                                          dr.ItemArray[2].ToString().Trim() + "')";
                        ODMDataSetFactory.ExecuteNonQuery(ref Request);
                    }
                }
                // ....To HERE////////////// 
            }
            catch (Exception ex)
            {
                lm.Write("PCUCost: RefreshPreviousValuTable: " + ex.Message);
                errMssg.Notify += "PCUCost: RefreshPreviousValuTable:  " + ex.Message + Environment.NewLine;
            }
        }

        protected DataSet BuildSQLRefresh()
        {//reads from the PROD. HEMM db. The resulting dataset is used to refill the uwm_BIAdmin.dbo.uwm_IVPItemCost table.
            if (trace) lm.Write("TRACE:  PCUCost.BuildSQLRefresh()");
            int itemID = 0;
            double cost = 0.0;
            string itemNo = "";
            string sqlRefresh = "SELECT  distinct  ITEM.ITEM_ID, IVP.PRICE, ITEM_NO " +
                                           "FROM ITEM_VEND_PKG IVP " +
                                           "JOIN ITEM_VEND IV ON IVP.ITEM_VEND_ID = IV.ITEM_VEND_ID " +                                           
                                           "JOIN ITEM ON ITEM.ITEM_ID = IV.ITEM_ID " +
                                           "JOIN SLOC_ITEM SI ON IVP.ITEM_VEND_ID = SI.ITEM_VEND_ID " +
                                           "JOIN VEND ON VEND.VEND_ID = IV.VEND_ID " +
                                           "WHERE IVP.SEQ_NO = (SELECT MAX (SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = SI.ITEM_VEND_ID) " +
                                           "AND IV.SEQ_NO = (SELECT MIN(SEQ_NO) FROM ITEM_VEND WHERE ITEM_VEND_ID = IVP.ITEM_VEND_ID) " +
                                           "AND LEN(SI.PAT_CHRG_NO) > 0 " +
                                           "AND ITEM.STAT = 1 " +
                                           "AND LEFT(SI.PAT_CHRG_NO,5) <> '40411' " +
                                           "AND IVP.PRICE > 0 " +
                                           "AND CORP_ID = 1000 " +
                                           "ORDER BY ITEM.ITEM_NO ";
           
            ///OLD SELECT
            ////"SELECT  distinct  SI.ITEM_ID, IVP.PRICE, ITEM_NO " +
            ////                               "FROM ITEM_VEND_PKG IVP " +
            ////                               "JOIN ITEM_VEND IV ON IVP.ITEM_VEND_ID = IV.ITEM_VEND_ID " +
            ////                               "JOIN SLOC_ITEM SI ON IVP.ITEM_VEND_ID = SI.ITEM_VEND_ID " +
            ////                               "JOIN ITEM ON ITEM.ITEM_ID = SI.ITEM_ID " +
            ////                               "WHERE IVP.SEQ_NO = (SELECT MAX (SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = SI.ITEM_VEND_ID) " +
            ////                               "AND IV.SEQ_NO = (SELECT MIN(SEQ_NO) FROM ITEM_VEND WHERE ITEM_VEND_ID = IVP.ITEM_VEND_ID) " +
            ////                               "AND LEN(SI.PAT_CHRG_NO) > 0 " +
            ////                               "AND SI.STAT = 1 " +
            ////                               "AND LEFT(SI.PAT_CHRG_NO,5) <> '40411' " +
            ////                               "AND IVP.PRICE > 0 " +
            ////                               "ORDER BY SI.ITEM_ID ";

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
            ArrayList dupes = new ArrayList();
            try
            {
            foreach (DataRow dr in dSet.Tables[0].Rows)
            {
                    if (htHash.ContainsKey(Convert.ToInt32(dr.ItemArray[0])))
                    {
                        dupes.Add(Convert.ToInt32(dr.ItemArray[0]));
                        continue;
                    }
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
