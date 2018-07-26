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
        private Hashtable alias_PatChrg = new Hashtable();
        private Hashtable itemNoPCost = new Hashtable();
        private ArrayList aliasList = new ArrayList();
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
            dsRefresh.Tables.Clear();
            dsRefresh = BuildSQLRefresh(); //all active, billable MPOUS items  with a '^' in the LPC
            BuildLPCTable();
            BuildHEMMPriceTable();
            suppressLogEntry = true; //the number of MPOUS items to be updated is a subset of all of the MPOUS items. suppressLogEntry keeps 
                                     //CalculatePatientPrice from logging the HEMM item_id and newPChg for all of the MPOUS items
            CalculatePatientPrice(HEMMPrice);  //upon completion the hashtable in PCUCost named patientPrice now holds the item_No as key and  
                                               //calculated PatChrgValu as value
            ComparePatPrices();
            UpdateMPOUS();
        }

        private void SplitLPC()
        {//takes the aliasLPC hastable and splits off the current pat chrg price. This value is put in the resulting ArrayList aliasList
            //since we have to compare the MPOUS items to all of the HEMM items, this keeps us from calculating pac chrg values
            //for items that aren't in MPOUS.
            string alias = "";
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
                    lm.Write("MPOUSCharges: SplitLPC:  " + ex.Message);
                    errMssg.Notify += "MPOUSCharges: SplitLPC:  " + ex.Message + Environment.NewLine;
                }
            }
            
        }

        private void ComparePatPrices()
        {
            string lpc = "";
            string[] formatCheck;
            string alias = "";
            double lpcChrg = 0;
            double hemmPatChrg = 0;
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

                            hemmPatChrg = 0;
                            if (patientPrice.ContainsKey((object)alias))
                            {
                                hemmPatChrg = Convert.ToDouble(patientPrice[alias]);
                                if (hemmPatChrg != lpcChrg)
                                {
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
            aliasLPC.Clear();
            string itemNo = "";
            object lpc;
            object itemid;
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
                        aliasLPC.Add((object)itemNo, (object)dr.ItemArray[2]);
                        MPOUS_Item_ID.Add((object)itemNo, (object)dr.ItemArray[0]); //used to convert the Alias_ID to the Item_ID
                    }
                    catch (Exception ex)
                    {
                        lm.Write("MPOUSCharges: BuildLPCTable:  " + ex.Message);
                    }
                }
                SplitLPC(); //this is to strip off the PatChrg part of the LPC and associate it with its alias_id
            }catch(Exception ex)
            {
                lm.Write("MPOUSCharges: BuildLPCTable:  " + ex.Message);
                errMssg.Notify += "MPOUSCharges: BuildLPCTable:  " + ex.Message + Environment.NewLine;
            }
        }

        protected void BuildHEMMPriceTable()
        {//this gets the ITEM_NO and PAT_CHRG_PRICE so that these values can be compared to the MPOUS side.
            //each HEMM item_no that meets the qurey criteria is compared to the mpous alias_id in the aliasList ArrayList
            //the ones that match are saved to HEMMPrice hashtable (item_no/cost). the alternative is to run this query alias.Count 
            //number of times. here we run the query once and compare the results.
            HEMMPrice.Clear();
            if (trace) lm.Write("TRACE:  PCUCost.BuildChargeCode()");
            string itemNo = "";
            string sqlRefresh = "SELECT ITEM_NO, PRICE " +
                                "FROM ITEM " +
                                "JOIN ITEM_VEND IV ON IV.ITEM_ID = ITEM.ITEM_ID " +
                                "JOIN ITEM_VEND_PKG IVP ON IV.ITEM_VEND_ID = IVP.ITEM_VEND_ID " +
                                "WHERE IVP.SEQ_NO = (SELECT MAX(SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = IV.ITEM_VEND_ID) " +
                                "AND IV.SEQ_NO = (SELECT MIN(SEQ_NO) FROM ITEM_VEND  WHERE ITEM_ID = ITEM.ITEM_ID) " +
                                "AND STAT IN(1, 2)";
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = uwmConnectStr; //connect str for HEMM
            Request.CommandType = CommandType.Text;
            Request.Command = sqlRefresh;
            if (verbose)
                Console.WriteLine("Updating Previous Value Table: " + HEMMPrice.Keys.Count + " Changes.");
            try
            {
                dsRefresh = ODMDataSetFactory.ExecuteDataSetBuild(ref Request);
                foreach (DataRow dr in dsRefresh.Tables[0].Rows)
                {
                    itemNo = dr.ItemArray[0].ToString().Trim();
                    //     if(alias_PatChrg.ContainsKey((object)itemNo))
                    if (aliasList.Contains((object)itemNo))
                        HEMMPrice.Add(itemNo, dr.ItemArray[1].ToString().Trim());                                           
                }
            }
            catch (Exception ex)
            {
                lm.Write("MPOUSCharges: BuildHEMMPriceTable:  " + ex.Message);
                errMssg.Notify += "MPOUSCharges: BuildHEMMPriceTable:  " + ex.Message + Environment.NewLine;
            }           
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
