using System;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using OleDBDataManager;

namespace PCUConsole
{
    class PointOfUse:PCUCost
    {
        #region Class Variables
        public int test = 0;
        protected Hashtable itemNoPCost = new Hashtable();
        private Hashtable itemIDPCost = new Hashtable();
        protected Hashtable aliasLPC = new Hashtable();
        protected NameValueCollection ConfigData = null;
        protected ODMRequest Request;

        #region Parameters
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
        public Hashtable PriceToPatient
        {
            set {patientPrice = value;}
        }
        #endregion
        #endregion

        //the hashtables:
        //patientPrice is passed in - key = HEMM Item_ID   value = new Pat_Chg_Price
        //itemIDPCost uses the Item_No from itemNoPCost to get the MPOUS Item_ID - key = MPOUS Item_ID   value = new Pat_Chg_Price
        public PointOfUse()
        {
            if (trace) lm.Write("TRACE:  PointOfUse.PointOfUse(constructor)");
            //ODMDataSetFactory = new ODMDataFactory();
            //ConfigData = (NameValueCollection)ConfigurationSettings.GetConfig("PatientChargeUpdate");
            //mpousConnectStr = ConfigData.Get("cnctMPOUS");
            //uwmConnectStr = ConfigData.Get("cnctHEMM_HMC");
            //biAdminConnectStr = ConfigData.Get("cnctBIAdmin");
            //OkToUpdate = Convert.ToBoolean(ConfigData.Get("updateTables"));
            Request = new ODMRequest();
            Request.CommandType = CommandType.Text;
        }

        public void ProcessMPOUSChanges()
        {//Convert the HEMM Item_ID into the HEMM Item_NO (which is also the MPOUS Alias_ID)
            string select = "SELECT DISTINCT Location_Procedure_Code FROM [PointOfUseSupply].[dbo].[D_INVENTORY_ITEMS] " +
                                    "WHERE Billable_Flag = 1 AND ITEM_ID = ";
            string update1 = "UPDATE D_INVENTORY_ITEMS SET Location_Procedure_Code = '";   ////USE FOR PRODUCTION
            string update2 = "' WHERE Billable_Flag = 1  AND Item_Id = "; //USE FOR PRODUCTION  
            string locProcCode = "";
            string errorLoc = "";
            ArrayList lpCode = new ArrayList();

            if (trace) lm.Write("TRACE:  PointOfUse.ProcessMPOUSChanges()");

            FillItemID_PCost();  //key = MPOUS Item_ID   value = new Pat_Chg_Price

            if (debug)
            {
                update1 = "UPDATE [uwm_BIAdmin].[dbo].[uwm_D_INVENTORY_ITEMS] SET Location_Procedure_Code = '";  //this is for TEST 
                update2 = "' where Billable_Flag = 1  AND ItemID = ";   //this is for TEST                        
            }
            
            if (itemIDPCost.Count > 0)                
            {//now get the Location_Procedure_Code from MPOUS for the given Item_ID
                //split it on the "^" and append the new Pat_Chg_Price
                
                foreach (object itemID in itemIDPCost.Keys)
                {
                    Request.ConnectString = mpousConnectStr;////USE FOR BOTH PRODUCTION & TEST - used in a select statement
                    Request.Command = select + itemID.ToString();
                    try
                    {
                        errorLoc = "SELECT";
                       // lm.Write(Request.Command); //added 3/30 for debug
                        lpCode = ODMDataSetFactory.ExecuteDataReader(Request, 1);
                        if (lpCode.Count > 0)
                        {
                            locProcCode = lpCode[0].ToString();
                            if (locProcCode.Trim().Length > 0) //this will be blank if the item doesn't already have a loc_proc_code
                            {
                                lm.Write("Old ID/LPC: " + TAB + itemID + TAB + locProcCode);
                                locProcCode = (locProcCode.Split("^".ToCharArray()))[0] + "^" + itemIDPCost[itemID];
                                lm.Write("New ID/LPC: " + TAB + itemID + TAB + locProcCode);

                                Request.ConnectString = debug ? biAdminConnectStr : mpousConnectStr;
                                Request.Command = update1 + locProcCode + update2 + itemID.ToString();
                                errorLoc = "UPDATE";
                                if (OkToUpdate)
                                {
                                    ODMDataSetFactory.ExecuteDataWriter(ref Request);
                                   // lm.Write("UPDATE --  ID/LPC: " + TAB + itemID + TAB + locProcCode);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lm.Write("PointOfUse: ProcessMPOUSChanges:  " + errorLoc + TAB + ex.Message);
                        errMssg.Notify += "PointOfUse: ProcessMPOUSChanges:  " + ex.Message + Environment.NewLine;
                    }
                }
            }
        }

        private string GetItemNoFromID(int itemID)
        {//The first step in converting the HEMM Item_id into the MPOUS Item_ID.
            //This gets the HEMM Item_NO (which is also the MPOUS Alias_ID) from the HEMM Item_ID
            if (trace) lm.Write("TRACE:  PointOfUse.GetItemNoFromID()");
            string select = "SELECT ITEM_NO FROM ITEM WHERE ITEM_ID = ";
            ArrayList item = new ArrayList();
            string itemNO = "";
            string[] itemNoDS;
            test = 0;
            Request.ConnectString = uwmConnectStr;
            Request.Command = select + itemID.ToString().Trim();
            try
            {
                item = ODMDataSetFactory.ExecuteDataReader(Request, 1);
                itemNO = item[0].ToString().Trim();
                if (itemNO.EndsWith("DS"))
                {
                    itemNoDS = itemNO.Split("D".ToCharArray());
                    itemNO = itemNoDS[0];
                }                
            }
            catch (Exception ex)
            {
                lm.Write("PointOfUse: GetItemNoFromID:  " + itemNO + "     " + ex.Message);
                errMssg.Notify += "PointOfUse: GetItemNoFromID:  " + ex.Message + Environment.NewLine;
            }
            return itemNO;
        }       

        private void FillItemID_PCost()
        {//this gets the mpous item_id from the alias_id (aka hemm's item_no)
            if (trace) lm.Write("TRACE:  PointOfUse.FillItemID_PCost()");
            string select = "SELECT ITEM_ID FROM AHI_ITEM_ALIAS WHERE ALIAS_ID = '";
            string itemNO = "";
            string[] itemNoDS;
            ArrayList item = new ArrayList();
            foreach (object HEMMItemID in patientPrice.Keys)
            {
                itemNO = GetItemNoFromID(Convert.ToInt32(HEMMItemID));
                Request.ConnectString = mpousConnectStr;
                Request.Command = select + itemNO + "'";
                try
                {
                    item = ODMDataSetFactory.ExecuteDataReader(Request, 1);
                    if (item.Count > 0)
                    {
                        if (!(itemIDPCost.ContainsKey(item[0])))
                            itemIDPCost.Add(Convert.ToInt32(item[0]), patientPrice[HEMMItemID]);
                    }
                    else
                    {
                        lm.Write("PCUConsole.PointOfUse: FillItemID_PCost:  Item# " + itemNO + " isn't part of MPOUS");
                    }
                }
                catch (Exception ex)
                {
                    lm.Write("PointOfUse: FillItemID_PCost:  " + ex.Message);
                    errMssg.Notify += "PointOfUse: FillItemID_PCost:  " + ex.Message + Environment.NewLine;
                }
            }
        }
       
        public void RefreshPreviousValues()
        {
            if (trace) lm.Write("TRACE:  PointOfUse.RefreshPreviousValues()");
            //DataSet dsRefresh = new DataSet();
            dsRefresh.Tables.Clear();
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = biAdminConnectStr;
            Request.CommandType = CommandType.Text;
            Request.Command = "TRUNCATE TABLE uwm_BIAdmin.dbo.uwm_MPOUS_LocProcCode ";
            if (verbose)
                Console.WriteLine("Updating Previous Value Table: " + patientPrice.Keys.Count + " Changes.");

            try
            {
                if (OkToUpdate)
                    ODMDataSetFactory.ExecuteNonQuery(ref Request); //truncate the uwm_MPOUS_LocProcCode table

                dsRefresh = BuildSQLRefresh(); //This reads from PointOfUse db and returns a data set
                if (verbose)
                    Console.WriteLine("Updating the PatientItemCharge table with " + dsRefresh.Tables[0].Rows.Count + " records. This will take a moment or two");
                foreach (DataRow dr in dsRefresh.Tables[0].Rows)
                {
                    Request.Command = "INSERT INTO uwm_BIAdmin.dbo.uwm_MPOUS_LocProcCode VALUES(" +
                                                    dr.ItemArray[0] + ",'" + dr.ItemArray[1] + "','" + dr.ItemArray[2].ToString().Trim() + "')";
                    //OkToUpdate = false  TO PREVENT CHANGING THE uwm_MPOUS_LocProcCode TABLE
                    if(OkToUpdate)
                        ODMDataSetFactory.ExecuteNonQuery(ref Request);
                }
            }
            catch (Exception ex)
            {
                lm.Write("PointOfUse: RefreshPreviousValuTable:  " + ex.Message);
                errMssg.Notify += "PointOfUse: RefreshPreviousValuTable:  " + ex.Message + Environment.NewLine;
            }
        }

        protected DataSet BuildSQLRefresh()
        {
            if (trace) lm.Write("TRACE:  PointOfUse.BuildSQLRefresh()");
            int itemID = 0;
            double cost = 0.0;
            string itemNo = "";
            string sqlRefresh = "SELECT AIA.ITEM_ID, ALIAS_ID, Location_Procedure_Code " +
                                "FROM D_SUPPLY_ITEM DSI " +
                                "JOIN AHI_ITEM_ALIAS AIA ON AIA.ITEM_ID = DSI.SUPPLY_ITEM_ID " +
                                "JOIN D_INVENTORY_ITEMS DII ON DII.ITEM_ID = AIA.ITEM_ID " +
                                "JOIN D_SUPPLY_SOURCE_ITEM DSSI ON DSSI.Supply_Item_Id = DSI.Supply_Item_Id " +
                                "WHERE Billable_Flag = 1 " +
                                "AND DII.ACTIVE_FLAG = 1 " +
                                "AND LEN(Location_Procedure_Code) > 0 " +
                                "ORDER BY Alias_Id";

ODMRequest Request = new ODMRequest();
            Request.ConnectString = mpousConnectStr; //connect str for PointOfUse
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
                lm.Write("UpdatePatCharges: BuildSQLRefresh:  " + ex.Message);
                errMssg.Notify += "PointOfUse: BuildSQLRefresh:  " + ex.Message + Environment.NewLine;
            }
            return dsRefresh;
        }

        /// <summary>
        /// this is to test the PointOfUse class - see GetItemNoFromID()
        /// </summary>
        /// <returns></returns>
        private string GetTestItemNO()
        {
            string rtnValu = "";
            switch (test++)
            {
                case 0:
                    rtnValu = "11047";
                    break;
                case 1:
                    rtnValu = "11049";
                    break;
                case 2:
                    rtnValu = "11051";
                    break;
                case 3:
                    rtnValu = "11052";
                    break;
                default:
                    rtnValu = "12772";
                    break;
            }
           return rtnValu;
        }


    }
}
