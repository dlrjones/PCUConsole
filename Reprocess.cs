using System;
using System.Data;
using System.Collections;


namespace PCUConsole
{
    class Reprocess : PCUCost
    {
        #region ClassVariables
        private Hashtable newItemCost;  // itemID / ItemMarkup
        private DataManager dm = new DataManager();
        private DataSet reprocItems = new DataSet();
        private string uwmConnectStr = "";
      //  private string hospital = "";
        private ItemMarkup itemMU;
        private ArrayList newMultiplierList = new ArrayList();
        bool reprocVendor = false;

        //public string Hospital
        //{
        //    get { return hospital; }
        //    set { hospital = value; }
        //}
        public Hashtable NewItemCost
        {
            get { return newItemCost; }
            set { newItemCost = value; }
        }

        public string UwmConnectStr
        {
            set { uwmConnectStr = value; }
        }       

        public ItemMarkup ItemMU
        {
            get { return itemMU; }
            set { itemMU = value; }
        }
        #endregion

        public void AddTestData()
        {
            ItemMarkup im;
            bool goodToGo = false;
            for (int x = 0; x < 10; x++)
            {
                im = new ItemMarkup();                
                switch (x)
                {   // item_id/cost
                    case 0:
                        if (!newItemCost.ContainsKey(2222225))
                        {
                            im.AddItemIDCost(2222225, "98.16");
                            goodToGo = true;
                        }
                        break;
                    case 1:
                        if (!newItemCost.ContainsKey(2012082))
                        {
                            im.AddItemIDCost(2012082, "4.04");  //medline
                            goodToGo = true;
                        }
                        break;
                    case 2:
                        if (!newItemCost.ContainsKey(2222572)) { 
                            im.AddItemIDCost(2222572, "33.25");
                            goodToGo = true;
                        }                       
                        break;
                    case 3:
                        if (!newItemCost.ContainsKey(2104499)) { 
                            im.AddItemIDCost(2104499, "6.08");   //
                            goodToGo = true;
                        }
                        break;
                    case 4:
                        if (!newItemCost.ContainsKey(2114184)) { 
                            im.AddItemIDCost(2114184, "5.25");   //medline
                            goodToGo = true;
                        }
                        break;
                    case 5:
                        if (!newItemCost.ContainsKey(1985726)) { 
                            im.AddItemIDCost(1985726, "425.00");
                            goodToGo = true;
                        }
                        break;
                    case 6:
                        if (!newItemCost.ContainsKey(1952589)) { 
                            im.AddItemIDCost(1952589, "125.01");
                            goodToGo = true;
                        }
                        break;
                    case 7:
                        if (!newItemCost.ContainsKey(1952590)) { 
                            im.AddItemIDCost(1952590, "85.00");
                            goodToGo = true;
                        }
                        break;
                    case 8:
                        if (!newItemCost.ContainsKey(1576516)) { 
                            im.AddItemIDCost(1576516, "4.04");
                            goodToGo = true;
                        }
                        break;
                    case 9:
                        if (!newItemCost.ContainsKey(1576511)) { 
                            im.AddItemIDCost(1576511, "1.65");
                            goodToGo = true;
                        }
                        break;
                }
                if(goodToGo)
                    newItemCost.Add(im.ItemID, im);
                goodToGo = false;
            }

            /*
             *1985726	69672R	LF4418	425.00
1952589	46264R	390.005	125.01
1952590	60022R	390.008	85.00
1576516	54265R	PIB500	4.04
1576511	46469R	V39100	1.65
             * 
             * 
             * ITEM_ID	ITEM_NO	ITEM CTLG	IVP CTLG	PRICE
                2012082	305425R        	IN800048            	RNUUWIN8000	4.04
                2104499	336830R        	9529                	AHAUWM9529	6.08
                2114184	340261R        	PIB500              	RNUUWMPIB500H	5.25
             ITEM_ID	ITEM_NO	PRICE
            2222225	106706R        	98.16
            2222566	113980R        	32.25
            2222572	113981R        	33.25
            2222577	121584R        	105.50
            2222582	130465R        	36.00*/
        }

      

        public void CheckForReprocessedItems()
        {
            if (trace) lm.Write("TRACE:  Reprocess.CheckForReprocessedItems()");
            string vendName = "";
            string inClause = "";
            string oemNumber = "";
            string oemCost = "";
            int itemIDCount = 0;
            int index = 1;
            int colCount = 0;
            string rValuItems = "";
            ArrayList reproc = new ArrayList();
            Hashtable reprocs = new Hashtable();

     //       AddTestData();  //test data is added to provide item_no's ending with "R"
            foreach(int itemID in newItemCost.Keys)
            {
                itemIDCount++;
                inClause += itemID.ToString() + ",";
                if (itemIDCount % 1000 == 0)
                {                    
                    inClause = inClause.Substring(0, inClause.Length - 1);
                    reprocItems = dm.GetReprocData(uwmConnectStr, inClause);
                    if(reprocItems.Tables[0].Rows.Count > 0)
                    {
                        foreach(DataRow dr in reprocItems.Tables[0].Rows)
                        {
                            reproc.Add(dr[colCount++]);
                            reproc.Add(dr[colCount++]);
                            reproc.Add(dr[colCount++]);
                            reproc.Add(dr[colCount++]);
                            reproc.Add(dr[colCount]);
                            reprocs.Add(index++, reproc);
                            colCount = 0;
                            reproc = new ArrayList();
                        }
                    }
                    reprocItems.Clear();
                    
                    inClause = "";
                    itemIDCount = 0;
                }
            }
            if(inClause.Length > 0)
            {
                inClause = inClause.Substring(0, inClause.Length - 1);
                reprocItems = dm.GetReprocData(uwmConnectStr, inClause);
                if (reprocItems.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow dr in reprocItems.Tables[0].Rows)
                    {
                        reproc.Add(dr[colCount++]);
                        reproc.Add(dr[colCount++]);
                        reproc.Add(dr[colCount++]);
                        reproc.Add(dr[colCount++]);
                        reproc.Add(dr[colCount]);
                        reprocs.Add(index++, reproc);
                        colCount = 0;
                        reproc = new ArrayList();
                        
                    }
                }
            }
            //inClause = inClause.Substring(0, inClause.Length - 1);//remove the trailing comma
            //reprocItems = dm.GetReprocData(uwmConnectStr,inClause);

            try
            {
                index = 0;
                foreach (ArrayList newItems in reprocs.Values)
                {
                    //reprocItems = (DataSet)newItems.Value;
                    //if (reprocItems.Tables.Count > 0)
                    //{
                        //foreach (DataRow dr in reprocItems.Tables[0].Rows)
                        //{ // [0]VEND.NAME, [1]IV.ITEM_ID, [2]ITEM_NO, [3]ITEM.CTLG_NO, [4]IVP.PRICE
                            reprocVendor = false;
                            vendName = newItems[0].ToString().Trim();
                            if (vendName == "STRYKER SUSTAINABILITY SOLUTIONS" || vendName == "RENU MEDICAL INC")
                                reprocVendor = true;
                            try
                            {
                                if (reprocVendor)
                                {//use the cost from the OEM version of this item along with its markup.
                                    oemNumber = newItems[2].ToString();
                                    oemNumber = oemNumber.Substring(0, oemNumber.Length - 1); //remove the "R" from the end of the item #
                                                                                              //oemCost = dm.GetOEMCost(uwmConnectStr, "", oemNumber, dr.ItemArray[3].ToString());
                                    oemCost = dm.GetSecondaryVendorCost(uwmConnectStr, oemNumber);
                                    oemCost = oemCost.Length == 0 ? newItems[4].ToString() : oemCost;
                                    itemMU = new ItemMarkup();
                                    itemMU.AddVendItemCtlg(vendName.Trim(), newItems[2].ToString(), newItems[3].ToString());
                                    itemMU.AddItemIDCost(Convert.ToInt32(newItems[1]), oemCost);
                                    newMultiplierList.Add(itemMU);

                                    //REMOVE THE RECORD FROM newItemCost for this itemID AND REPLACE IT WITH NEW ItemMarkup OBJECT (itemMU)                            
                                    if (newItemCost.Contains(newItems[1]))
                                        newItemCost.Remove(newItems[1]);
                                    newItemCost.Add(newItems[1], itemMU);
                                }
                                else
                                {   //use the markup from the OEM cost and apply it to the reproc item's cost                                
                                    itemMU = new ItemMarkup();
                                    itemMU.AddVendItemCtlg(vendName, newItems[2].ToString(), newItems[3].ToString());
                                    itemMU.AddItemIDCost(Convert.ToInt32(newItems[1]), newItems[4].ToString());
                                    newMultiplierList.Add(itemMU);
                                }
                            }
                            catch (Exception ex)
                            {
                                lm.Write("Reprocess: CheckForReprocessedItems: if(reprocVendor)...  " + ex.Message);
                            }

                        //}
                    //}
                }
            }catch(Exception ex)
            {
                lm.Write("Reprocess: CheckForReprocessedItems:  " + ex.Message);
            }
            if(newMultiplierList.Count > 0)
            {
                GetOEMMultiplier(newMultiplierList);
            }
        }

        private void GetOEMMultiplier(ArrayList newMultList)
        {
            if (trace) lm.Write("TRACE:  Reprocess.GetOEMMultiplier()");            
            string oemCost = "";
            string origOEMCost = "";
            string itemNo = "";
            string vendName = "";
            string ctlgNo = "";
            ItemMarkup itmMarkUp;

            foreach(object im in newMultList)
            {
                itmMarkUp = (ItemMarkup)im;
                itemNo = itmMarkUp.ItemNmbr;
                itemNo = itemNo.Substring(0, itemNo.Trim().Length - 1);  //strips off the "R";
                origOEMCost = itmMarkUp.CrntCost;

                if (itmMarkUp.VendorName == "STRYKER SUSTAINABILITY SOLUTIONS" || itmMarkUp.VendorName == "RENU MEDICAL INC")
                    oemCost = itmMarkUp.CrntCost;
                else 
                    oemCost = dm.GetOEMCost(uwmConnectStr, itmMarkUp.VendorName, itemNo, itmMarkUp.CatalogNmbr);

                if (oemCost.Length == 0)
                    oemCost = origOEMCost;
                SetNewMarkup(itmMarkUp,oemCost);
                //at this point I have the OEM cost of the item. need to get it's multiplier and save it back 
                //to the newItemCost hastable

                ///////DBReadLatestTierValues(string dm.GetCurrentTierValues())
                ///////ParseTierValuResults(ArrayList results)
                ///public Hashtable DollarLimits      ---  dollarLimits.Add(indx, dlrValu);  indx starts at 1
                ///public Hashtable MultiplierValu    ---  multiplierValu.Add(indx++, multValu);
                //////patPrice = cost * Convert.ToDouble(multiplierValu[indx]);
                //////patPrice = RoundOffPatPrice(patPrice);
            }
        }

        private void SetNewMarkup(ItemMarkup im, string oemCost)
        {
            if (trace) lm.Write("TRACE:  Reprocess.SetNewMarkup()");
            int itemID = Convert.ToInt32(im.ItemID);
            double oemMult = 0.0;
            ItemMarkup imOrig;
            Hashtable dlrLimits = new Hashtable();
            Hashtable multValu = new Hashtable();
            dm.GetCurrentTierValues(location);
            dlrLimits = dm.DollarLimits;
            multValu = dm.MultiplierValu;            
            for (int indx = 1; indx <= dlrLimits.Count; indx++)
            {
                // indx is a key for the dollarLimits and multiplierValu hashtables, that's why it doesn't start at 0
                if ( Convert.ToDouble(oemCost) <= Convert.ToDouble(dlrLimits[indx]))
                {
                    oemMult = Convert.ToDouble(multValu[indx]);                   
                    break;
                }
            }
            imOrig = (ItemMarkup)newItemCost[itemID];
            imOrig.Multiplier = oemMult;
            newItemCost.Remove(itemID);
            newItemCost.Add(itemID, imOrig);
        }

    }
}
