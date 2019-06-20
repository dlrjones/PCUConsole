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
        private ItemMarkup itemMU;
        private ArrayList newMultiplierList = new ArrayList();
        bool reprocVendor = false;

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
            for (int x = 0; x < 5; x++)
            {
                im = new ItemMarkup();
                switch (x)
                {   // item_id/cost
                    case 0:
                        im.AddItemIDCost(2222225, "98.16");
                        break;
                    case 1:
                        im.AddItemIDCost(2012082, "4.04");  //medline
                        break;
                    case 2:
                        im.AddItemIDCost(2222572, "33.25");
                        break;
                    case 3:
                        im.AddItemIDCost(2104499, "6.08");   //
                        break;
                    case 4:
                        im.AddItemIDCost(2114184, "5.25");   //medline
                        break;
                }
                newItemCost.Add(im.ItemID, im);
            }

            /*
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
            string vendName = "";
            string inClause = "";
            string oemNumber = "";
            string oemCost = "";


            AddTestData();  //test data is added to provide item_no's ending with "R"
            foreach(int itemID in newItemCost.Keys)
            {
                inClause += itemID.ToString() + ",";
            }
            inClause = inClause.Substring(0, inClause.Length - 1);//remove the trailing comma
            reprocItems = dm.GetReprocData(uwmConnectStr,inClause);

            try
            {
                if (reprocItems.Tables.Count > 0)
                {
                    foreach (DataRow dr in reprocItems.Tables[0].Rows)
                    { // [0]VEND.NAME, [1]IV.ITEM_ID, [2]ITEM_NO, [3]ITEM.CTLG_NO, [4]IVP.PRICE
                        reprocVendor = false;
                        vendName = dr.ItemArray[0].ToString().Trim();
                        if (vendName == "STRYKER SUSTAINABILITY SOLUTIONS" || vendName == "RENU MEDICAL INC")
                            reprocVendor = true;
                        try
                        {
                            if (reprocVendor)
                            {//use the cost from the OEM version of this item along with its markup.
                                oemNumber = dr.ItemArray[2].ToString();
                                oemNumber = oemNumber.Substring(0, oemNumber.Length - 1); //remove the "R" from the end of the item #
                                //oemCost = dm.GetOEMCost(uwmConnectStr, "", oemNumber, dr.ItemArray[3].ToString());
                                oemCost = dm.GetSecondaryVendorCost(uwmConnectStr, oemNumber);
                                oemCost = oemCost.Length == 0 ? dr.ItemArray[4].ToString() : oemCost;
                                itemMU = new ItemMarkup();
                                itemMU.AddVendItemCtlg(vendName.Trim(), dr.ItemArray[2].ToString(), dr.ItemArray[3].ToString());
                                itemMU.AddItemIDCost(Convert.ToInt32(dr.ItemArray[1]), oemCost);
                                newMultiplierList.Add(itemMU);

                                //REMOVE THE RECORD FROM newItemCost for this itemID AND REPLACE IT WITH NEW ItemMarkup OBJECT (itemMU)                            
                                if (newItemCost.Contains(dr.ItemArray[1]))
                                    newItemCost.Remove(dr.ItemArray[1]);
                                newItemCost.Add(dr.ItemArray[1], itemMU);
                            }
                            else
                            {   //use the markup from the OEM cost and apply it to the reproc item's cost                                
                                itemMU = new ItemMarkup();
                                itemMU.AddVendItemCtlg(vendName, dr.ItemArray[2].ToString(), dr.ItemArray[3].ToString());
                                itemMU.AddItemIDCost(Convert.ToInt32(dr.ItemArray[1]), dr.ItemArray[4].ToString());
                                newMultiplierList.Add(itemMU);
                            }
                        }catch(Exception ex)
                        {
                            lm.Write("Reprocess: CheckForReprocessedItems: if(reprocVendor)...  " + ex.Message);
                        }
                        
                    }
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
            string oemCost = "";
            string itemNo = "";
            string vendName = "";
            string ctlgNo = "";
            ItemMarkup itmMarkUp;

            foreach(object im in newMultList)
            {
                itmMarkUp = (ItemMarkup)im;
                itemNo = itmMarkUp.ItemNmbr;
                itemNo = itemNo.Substring(0, itemNo.Trim().Length - 1);  //strips off the "R";

                if (itmMarkUp.VendorName == "STRYKER SUSTAINABILITY SOLUTIONS" || itmMarkUp.VendorName == "RENU MEDICAL INC")
                    oemCost = itmMarkUp.CrntCost;
                else 
                    oemCost = dm.GetOEMCost(uwmConnectStr, itmMarkUp.VendorName, itemNo, itmMarkUp.CatalogNmbr);
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
            int itemID = Convert.ToInt32(im.ItemID);
            double oemMult = 0.0;
            ItemMarkup imOrig;
            Hashtable dlrLimits = new Hashtable();
            Hashtable multValu = new Hashtable();
            dm.GetCurrentTierValues();
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
