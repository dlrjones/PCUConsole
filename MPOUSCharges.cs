using System;
using System.Data;
using System.Collections;
using OleDBDataManager;

namespace PCUConsole
{
    /* This is an update to accommodate those cases where the INV_TOUCHSCAN virtual location isn't used. In these cases, someone has to enter the 
     * cost change into HEMM and then calculate the patient price before manually updating the location_procedure_code in MPOUS
     * The PCUConsole app runs as it as it always has with this additional class being invoked at the end. 
     * MPOUSCharges compares every MPOUS item with every item on the HEMM side, and makes a list of those items that match on the alias_id/item_no.
     * A comparison is then made on the patient charge values and another list is created with the alias_id and the new patient charges.
     * This sublist is what gets processed by calculating the new patient charge and appending it to the Location_Procedure_Code.
     */
    class MPOUSCharges : PCUCost
    {
        private Hashtable HEMMPrice = new Hashtable();
        private Hashtable MPOUS_Item_ID = new Hashtable();
        private Hashtable ItemID_ItemMarkup = new Hashtable();
        private Hashtable alias_PatChrg = new Hashtable();
        private Hashtable itemNoPCost = new Hashtable();
        private Hashtable ItemNo_HEMMItemID = new Hashtable();
        private ArrayList aliasList = new ArrayList();
        private ItemMarkup im_itemID;
        private string alias = "";
        #region Properties
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
        public string UwmConnectStr
        {
            set { uwmConnectStr = value;}
        }
        public string MpousCnctString
        {
            set { mpousConnectStr = value; }
        }
        public int Count
        {
            get { return itemNoPCost.Count; }
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

        /// <summary>
        /// an mpous item has to be Active, Billable and have the '^' in the LPC
        /// determines if hemm item is in mpous - if it is then save off the item_no and cost
        /// calculate the PatChrgPrice for each hemm item that is in mpous
        /// compare the hemm PatChrg to the mpous PatChrg
        /// if not equal then update the mpous LocProcCode.
        /// </summary>


        public void ProcessPOU()
        {
            if (trace) lm.Write("TRACE:  MPOUSCharges.ProcessPOU()");
            dsRefresh.Tables.Clear();
            dsRefresh = BuildSQLRefresh(); //all active, billable MPOUS items  with a '^' in the LPC
            BuildLPCTable();
            BuildHEMMPriceTable();
            suppressLogEntry = true;                            //the number of MPOUS items to be updated is a subset of all of the MPOUS items. suppressLogEntry keeps 
            CheckForReprocessedItems();                         //CalculatePatientPrice from logging the HEMM item_id and newPChg for all of the MPOUS items
            CalculatePatientPrice(ItemID_ItemMarkup);  //upon completion the hashtable in PCUCost named patientPrice now holds the HEMM item_ID as key and  
                                                        //an ItemMarkup object as value
            ComparePatPrices();
            UpdateMPOUS();
        }

        private void CheckForReprocessedItems()
        {
            if (trace) lm.Write("TRACE:  MPOUSCharges.CheckForReprocessedItems()");
            //when this method exits it will have the altered values for the reprocessed ("R") items in the Hashtable ItemID_ItemMarkup
            int itemID = 0;
            im_itemID = new ItemMarkup();
            try
            {
                foreach (ItemMarkup im_itemID in HEMMPrice.Values) //HEMMPrice = itemNO/ItemMarkup object
                {
                    itemID = im_itemID.ItemID;  //HEMM itemID
                    ItemID_ItemMarkup.Add(itemID, im_itemID);  //itemID/ItemMarkup object
                }               
            }
            catch(Exception ex)
            {
                lm.Write("Program: MPOUScHARGES: " + ex.Message + Environment.NewLine);
            }
            Reprocess reproc = new Reprocess();
            reproc.NewItemCost = ItemID_ItemMarkup; //HEMM  itemID/ItemMarkup object
            reproc.UwmConnectStr = uwmConnectStr;
            reproc.CheckForReprocessedItems();
            ItemID_ItemMarkup = reproc.NewItemCost;
        }

        private void SplitLPC()
        {
            if (trace) lm.Write("TRACE:  MPOUSCharges.SplitLPC()");
            //takes the aliasLPC hastable and splits off the current pat chrg price. This value is put in the resulting ArrayList aliasList
            //since we have to compare the MPOUS items to all of the HEMM items, this keeps us from calculating pac chrg values
            //for items that aren't in MPOUS.
            alias = "";
          //  string patChrg = "";
            string[] formatCheck;

            foreach (DictionaryEntry item in aliasLPC)
            {
                try
                {
                    alias = item.Key.ToString().Trim();
                    formatCheck = item.Value.ToString().Split("^".ToCharArray());
                    if (formatCheck.Length > 1)
                    {
                        //patChrg = (Convert.ToDouble(formatCheck[1])).ToString();
                        //alias_PatChrg.Add(alias, patChrg);
                        aliasList.Add(alias);
                    }
                    else
                    {
                        errMssg.Notify += "MPOUSCharges: SplitLPC:  The LPC for item " + alias + " is in the wrong format.";
                        lm.Write(errMssg.Notify);
                       // errMssg.Notify += "MPOUSCharges:SplitLPC:  Check Log for LPC Format Error" +  Environment.NewLine;
                    }
                }
                catch (Exception ex)
                {
                    lm.Write("MPOUSCharges: SplitLPC:  Alias: " + alias + "  " + ex.Message);
                    errMssg.Notify += "MPOUSCharges: SplitLPC:  Alias: " + alias + "  " + ex.Message + Environment.NewLine;
                }
            }
            
        }

        private void ComparePatPrices()
        {
            if (trace) lm.Write("TRACE:  MPOUSCharges.ComparePatPrices()");
            ItemMarkup im;
            string lpc = "";
            string[] formatCheck;
            string alias = "";
            double lpcChrg = 0;
            double hemmPatChrg = 0;
            int itemID = 0;
            itemNoPCost.Clear();

            try{
                foreach (DictionaryEntry item in aliasLPC)
                {
                    try
                    {
                        alias = item.Key.ToString().Trim();
                        lpc = item.Value.ToString().Trim();         //ex:  40526_30_C1752^1505
                        formatCheck = lpc.Split("^".ToCharArray());
                        if (formatCheck.Length > 1)
                        {
                            lpcChrg = Convert.ToDouble(formatCheck[1]); //ex: 1505
                            lpc = formatCheck[0];    //ex: 40526_30_C1752                   
                            itemID = Convert.ToInt32(ItemNo_HEMMItemID[alias]); //this gets the HEMM itemID to match with patientPrice
                            hemmPatChrg = 0;
                            if (patientPrice.ContainsKey(itemID))                   //(object)alias))
                            {
                               hemmPatChrg = Convert.ToDouble(patientPrice[itemID]);
                               hemmPatChrg = RoundOffPatPrice(hemmPatChrg); //stopped doing this for hmc as of 7/1/19 - necessary for mpous because of the way
                                                                           //the patient charge code is modified to include the patient price and if the value
                                                                           //isn't rounded the length of the code can exceed the db's field size   (varchar(25))
                                if (hemmPatChrg != lpcChrg)
                                {
                                    //im = new ItemMarkup();
                                    //im.ItemNmbr = alias;
                                    //im.ItemID = itemID;

                                    if (!itemNoPCost.Contains((object)alias))
                                    {
                                        itemNoPCost.Add((object)alias, lpc + "^" + (object)hemmPatChrg);
                                    }
                                }
                            }
                        }//there's no need to log an "LPC Bad Format" error here because it would have been done in the SplitLPC method
                         //which is invoked in the BuildLPCTable method prior to getting to this point.
                    }
                    catch (Exception ex)
                    {
                        lm.Write("MPOUSCharges: ComparePatPrices:  " + ex.Message);
                    }
                }                            
            }
            catch(Exception ex)
            {
                lm.Write("MPOUSCharges: ComparePatPrices:  " + ex.Message);
                errMssg.Notify += "MPOUSCharges: ComparePatPrices:  " + ex.Message + Environment.NewLine;
            }
        }

        private void UpdateMPOUS()
        {
            if (trace) lm.Write("TRACE:  MPOUSCharges.UpdateMPOUS()");
            string update1 = "UPDATE D_INVENTORY_ITEMS SET Location_Procedure_Code = '";
            string update2 = "' WHERE Billable_Flag = 1  AND Item_Id = ";
            string itemID = "";
            string locProcCode = "";
            ODMRequest Request = new ODMRequest();

            Request.CommandType = CommandType.Text;
            Request.ConnectString = debug ? biAdminConnectStr : mpousConnectStr;

            if (debug)
            {
                update1 = "UPDATE [uwm_BIAdmin].[dbo].[uwm_D_INVENTORY_ITEMS] SET Location_Procedure_Code = '";  //this is for TEST                       
            }

            if (itemNoPCost.Count > 0)
            {
                try
                {               
                    foreach (DictionaryEntry item in itemNoPCost)
                    {//item.key = alias_id   item.value = Loc Proc Code
                        itemID = MPOUS_Item_ID[item.Key.ToString()].ToString(); //converts the Alias_ID to the mpous Item_ID
                        locProcCode = item.Value.ToString();

                        //the output needs to be mpous item_id and LPC
                        lm.Write("Old ID/LPC: " + TAB + itemID + TAB + aliasLPC[item.Key.ToString()]); //aliasLPC is indexed with the item_no, not the item_id
                        lm.Write("New ID/LPC: " + TAB + itemID + TAB + locProcCode);

                        if (!(itemID.Length > 0))
                            OkToUpdate = false;
                        
                        Request.Command = update1 + locProcCode + update2 + itemID;
                        if (OkToUpdate)
                        {
                            ODMDataSetFactory.ExecuteDataWriter(ref Request);                            
                        }

                    }                    
                }
                catch(Exception ex)
                {
                    lm.Write("MPOUSCharges.UpdateMPOUS:  " + ex.Message);
                    errMssg.Notify += "MPOUSCharges.UpdateMPOUS:  " + ex.Message + Environment.NewLine;
                }
            }
            else
            {
                lm.Write("MPOUSCharges.UpdateMPOUS: There were no patient charges to update on the MPOUS side.");
            }
        }

        private DataSet BuildSQLRefresh()
        {//THIS GETS ALL ACTIVE, BILLABLE ITEMS FROM D_SUPPLY_ITEM
            if (trace) lm.Write("TRACE:  MPOUSCharges.BuildSQLRefresh()");
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
                                "AND CHARINDEX('^',Location_Procedure_Code) > 0 " +                               
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

        private void BuildLPCTable()
        {
            if (trace) lm.Write("TRACE:  MPOUSCharges.BuildLPCTable()");
            aliasLPC.Clear();
            string itemNo = "";
            object lpc;
            object itemid;
            lm.Write("CnctStr:" + uwmConnectStr);
            try
            {
                foreach (DataRow dr in dsRefresh.Tables[0].Rows)
                {//dr.ItemArray[0]=itemID   dr.ItemArray[1]=alias_id  dr.ItemArray[2]= Loc Proc Code
                    try
                    {
                        itemNo = dr.ItemArray[1].ToString().Trim();
                        lpc = (object)dr.ItemArray[2];
                        itemid = (object)dr.ItemArray[0];
                        if (aliasLPC.ContainsKey((object)itemNo))
                            continue;
                        aliasLPC.Add(itemNo, lpc);
                        MPOUS_Item_ID.Add(itemNo, itemid); //used to convert the Alias_ID to the Item_ID                       
                    }
                    catch (Exception ex)
                    {
                        lm.Write("MPOUSCharges: BuildLPCTable:  " + ex.Message);
                    }
                }
                GetHemmItemID(aliasLPC);
                SplitLPC(); //this is to strip off the PatChrg part of the LPC and associate it with its alias_id
            }catch(Exception ex)
            {
                lm.Write("MPOUSCharges: BuildLPCTable:  " + ex.Message);
                errMssg.Notify += "MPOUSCharges: BuildLPCTable:  " + ex.Message + Environment.NewLine;
            }
        }

        private void GetHemmItemID(Hashtable itemNoLPC)
        {
            if (trace) lm.Write("TRACE:  MPOUSCharges.GetHemmItemID()");
            ArrayList itemIDResults = new ArrayList();
            foreach (DictionaryEntry item in aliasLPC)
            {
                alias = item.Key.ToString().Trim();
                string sql = "SELECT ITEM_ID FROM ITEM WHERE ITEM_NO = '" + alias + "'";

                ODMRequest Request = new ODMRequest();
                Request.ConnectString = uwmConnectStr; //connect str for HEMM
                Request.CommandType = CommandType.Text;
                Request.Command = sql;
                if (verbose)
                    Console.WriteLine("Updating Previous Value Table: " + patientPrice.Keys.Count + " Changes.");
                try
                {
                   
                    itemIDResults = ODMDataSetFactory.ExecuteDataReader(ref Request);
                    if(itemIDResults.Count > 0)
                    {
                        ItemNo_HEMMItemID.Add(alias, itemIDResults[0]);
                    }
                }
                catch (Exception ex)
                {
                    lm.Write("UpdatePatCharges: BuildSQLRefresh:  " + ex.Message);
                    errMssg.Notify += "PointOfUse: GetHemmItemID:  " + ex.Message + Environment.NewLine;
                }                                   
            }
        }

        protected void BuildHEMMPriceTable()
        {//this gets the ITEM_NO and PAT_CHRG_PRICE so that these values can be compared to the MPOUS side.
            //each HEMM item_no that meets the query criteria is compared to the mpous alias_id in the aliasList ArrayList
            //the ones that match are saved to HEMMPrice hashtable (item_no/ItemMarkup object). the alternative is to run this query alias.Count 
            //number of times. here we run the query once and compare the results.
            if (trace) lm.Write("TRACE:  MPOUSCharges.BuildHEMMPriceTable()");
            ItemMarkup im;            
            HEMMPrice.Clear();
            string itemNo = "";
            int itemID = 0;
            string cost = "";
            string sqlRefresh = "SELECT ITEM_NO, IVP.PRICE, ITEM.ITEM_ID " +              //, ITEM.ITEM_ID
                                "FROM ITEM " +
                                "JOIN ITEM_VEND IV ON IV.ITEM_ID = ITEM.ITEM_ID " +
                                "JOIN ITEM_VEND_PKG IVP ON IV.ITEM_VEND_ID = IVP.ITEM_VEND_ID " +
                                "JOIN SLOC_ITEM ON SLOC_ITEM.ITEM_ID = ITEM.ITEM_ID " +
                                "WHERE IVP.SEQ_NO = (SELECT MAX(SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = IV.ITEM_VEND_ID) " +
                                "AND IV.SEQ_NO = (SELECT MIN(SEQ_NO) FROM ITEM_VEND  WHERE ITEM_ID = ITEM.ITEM_ID) " +
                                "AND ITEM.STAT IN(1, 2) " +
                                "AND LOC_ID NOT IN (2365,2658,2664,2659)";
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = uwmConnectStr; //connect str for HEMM
            Request.CommandType = CommandType.Text;
            Request.Command = sqlRefresh;
            if (verbose)
                Console.WriteLine("Updating Previous Value Table: " + HEMMPrice.Keys.Count + " Changes.");
            dsRefresh = ODMDataSetFactory.ExecuteDataSetBuild(ref Request);
            foreach (DataRow dr in dsRefresh.Tables[0].Rows)
            {
                try
                {

                    itemNo = dr.ItemArray[0].ToString().Trim();
                    if (itemNo == "100875")
                        itemNo = "100875";
                    cost = dr.ItemArray[1].ToString().Trim();
                    itemID = Convert.ToInt32(dr.ItemArray[2]);
                    //     if(alias_PatChrg.ContainsKey((object)itemNo))
                    if (aliasList.Contains((object)itemNo))
                    {
                        im = new ItemMarkup();
                        im.AddItemNOCost(itemNo, cost);
                        im.AddItemNOItemID(itemNo, itemID);
                        HEMMPrice.Add(itemNo, im);
                    }


                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("Item has already been added")) //the same item can be in multiple locations but the 
                    {                                                        //price will be the same
                        lm.Write("MPOUSCharges: BuildHEMMPriceTable:  " + ex.Message);
                        errMssg.Notify += "MPOUSCharges: BuildHEMMPriceTable:  " + ex.Message + Environment.NewLine;
                    }
                }
            }//FOREACH
                      
        }

        /*Patient Charge from the MPOUS side*/
        /* SELECT DISTINCT ALIAS_ID, Location_Procedure_Code, ISSUE_UOM
        FROM[PointOfUseSupply].[dbo].[D_INVENTORY_ITEMS] DII
        JOIN AHI_ITEM_ALIAS AIA ON AIA.Item_Id = DII.Item_Id
        JOIN D_SUPPLY_SOURCE_ITEM DSSI ON DSSI.Supply_Item_Id = DII.Item_Id
        WHERE Billable_Flag = 1
        AND DII.ACTIVE_FLAG = 1
        AND LEN(Location_Procedure_Code) > 0
         */

        /*Patient Charge from the HEMM side*/
        /*

            SELECT IV.ITEM_ID, PRICE,UM_CD, ITEM_NO , ITEM.DESCR, IVP.ITEM_VEND_ID, VEND_ID,IV.SEQ_NO
            FROM ITEM
            JOIN ITEM_VEND IV ON IV.ITEM_ID = ITEM.ITEM_ID
            JOIN ITEM_VEND_PKG IVP  ON IV.ITEM_VEND_ID = IVP.ITEM_VEND_ID
            WHERE  IVP.SEQ_NO = (SELECT MAX (SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = IV.ITEM_VEND_ID)
            AND IV.SEQ_NO = (SELECT MIN(SEQ_NO) FROM ITEM_VEND  WHERE ITEM_ID = ITEM.ITEM_ID)  
            AND STAT IN (1,2)



        an example of a potential problem can be seen with item# 56135 (hemm itemID 1384556). The item has 2 UOM's (CS & EA) with CS as the default. 
        MPOUS has the UOM as CS. The pricing in the Location_Proc_Code is for the EA. The item is a Catheter with 5 to a case.
        Here's what the item looks like:  http://www.bardaccess.com/products/nursing/powerpiccsolo
         */


    }
}
