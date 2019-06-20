using System;
using System.Data;
using System.Collections;
using OleDBDataManager;

namespace PCUConsole
{
    /* This is an update to accommodate those cases where the INV_TOUCHSCAN virtual location isn't used. In these cases, Scott has to enter the cost change into
HEMM and then calculate the patient price before manually updating the location_procedure_code in MPOUS
The PCUConsole app runs as it as it always has with this additional class being invoked at the end.
     */
    class MPOUSCharges : PointOfUse
    {
        private Hashtable HEMMPatientPrice = new Hashtable();
        private Hashtable MPOUS_Item_ID = new Hashtable();
        public void ProcessPOU()
        {
            BuildHEMMChargeCodeTable();
            dsRefresh.Tables.Clear();
            dsRefresh = BuildSQLRefresh(); //for MPOUS  - look in the PointOfUse parent class
            BuildLPCTable();
            ComparePatPrices();
            UpdateMPOUS();
        }

        private void ComparePatPrices()
        {
            string lpc = "";
            string old_lpc = "";
            string alias = "";
           // string[] hicks;
            double lpcChrg = 0;
            double hemmPatChrg = 0;
            itemNoPCost.Clear();

            try{
                foreach (DictionaryEntry item in aliasLPC)
                {
                    patientPrice.Clear();
                    alias = item.Key.ToString().Trim();
                    lpc = item.Value.ToString().Trim();         //ex:  40526_30_C1752^1505
                    old_lpc = lpc;
                  //  hicks = lpc.Split('^');

                    lpc = (lpc.Split("^".ToCharArray()))[0];    //ex: 40526_30_C1752

                    //lpc = hicks[0];                      
                    int test = 0;
                    lpcChrg = Convert.ToDouble((old_lpc.Split("^".ToCharArray()))[1]); //ex: 1505
                    //lpcChrg = Convert.ToDouble(hicks[1]); //ex: 1505
                    hemmPatChrg = 0;
                    if (HEMMPatientPrice.ContainsKey((object)alias))
                    {
                        hemmPatChrg = Convert.ToDouble(HEMMPatientPrice[alias]);
                        hemmPatChrg = RoundOffPatPrice(hemmPatChrg);
                        if (hemmPatChrg != lpcChrg)
                        {
                            if (!itemNoPCost.Contains((object)alias))
                            {
                                itemNoPCost.Add((object)alias, lpc + "^" + (object)hemmPatChrg);
                            }
                        }
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

                        Request.ConnectString = debug ? biAdminConnectStr : mpousConnectStr;
                        Request.Command = update1 + locProcCode + update2 + itemID;
                        if (OkToUpdate)
                        {
                            ODMDataSetFactory.ExecuteDataWriter(ref Request);                            
                        }
                    }
                }catch(Exception ex)
                {
                    lm.Write("MPOUSCharges: UpdateMPOUS:  " + ex.Message);
                    errMssg.Notify += "MPOUSCharges: UpdateMPOUS:  " + ex.Message + Environment.NewLine;
                }
            }
        }

        private void BuildLPCTable()
        {
            aliasLPC.Clear();
            string itemNo = "";
            try
            {
                foreach (DataRow dr in dsRefresh.Tables[0].Rows)
                {//dr.ItemArray[0]=itemID   dr.ItemArray[1]=alias_id  dr.ItemArray[2]= Loc Proc Code
                    itemNo = dr.ItemArray[1].ToString().Trim();
                    if (aliasLPC.ContainsKey((object)itemNo))
                        continue;
                    aliasLPC.Add((object)itemNo, (object)dr.ItemArray[2]);
                    MPOUS_Item_ID.Add((object)itemNo, (object)dr.ItemArray[0]); //used to convert the Alias_ID to the Item_ID
                }
            }catch(Exception ex)
            {
                lm.Write("MPOUSCharges: BuildLPCTable:  " + ex.Message);
                errMssg.Notify += "MPOUSCharges: BuildLPCTable:  " + ex.Message + Environment.NewLine;
            }
        }

        protected void BuildHEMMChargeCodeTable()
        {//this gets the ITEM_NO and PAT_CHRG_PRICE so that these values can be compared to the MPOUS side.
            HEMMPatientPrice.Clear();
            if (trace) lm.Write("TRACE:  PCUCost.BuildChargeCode()");
            string itemNo = "";
            string sqlRefresh = "SELECT distinct ITEM_NO, PAT_CHRG_PRICE " +
                                "FROM SLOC_ITEM " +
                                "JOIN ITEM ON ITEM.ITEM_ID = SLOC_ITEM.ITEM_ID " +
                                "WHERE ISNULL(PAT_CHRG_PRICE,0) > 0 " +
                                "AND SLOC_ITEM.STAT IN(1,2)";
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = uwmConnectStr; //connect str for HEMM
            Request.CommandType = CommandType.Text;
            Request.Command = sqlRefresh;
            if (verbose)
                Console.WriteLine("Updating Previous Value Table: " + HEMMPatientPrice.Keys.Count + " Changes.");
            try
            {
                dsRefresh = ODMDataSetFactory.ExecuteDataSetBuild(ref Request);
                foreach (DataRow dr in dsRefresh.Tables[0].Rows)
                {
                    itemNo = dr.ItemArray[0].ToString().Trim();
                    if (HEMMPatientPrice.ContainsKey((object)itemNo))
                        continue;
                    HEMMPatientPrice.Add(itemNo, dr.ItemArray[1].ToString().Trim());                                           
                }
            }
            catch (Exception ex)
            {
                lm.Write("MPOUSCharges: BuildHEMMChargeCodeTable:  " + ex.Message);
                errMssg.Notify += "MPOUSCharges: BuildHEMMChargeCodeTable:  " + ex.Message + Environment.NewLine;
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
