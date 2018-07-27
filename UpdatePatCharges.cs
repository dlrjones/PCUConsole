﻿using System;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using OleDBDataManager;

namespace PCUConsole
{
    class UpdatePatCharges : PCUCost
    {//UpdatePatCharges is invoked when the user clicks the "Incremental GetItemChanges" button 
        //on the PatientChargeUpdate UI or when a scheduled task triggers it.
        #region Class Variables
        private Hashtable currentItemCost = new Hashtable();
        private Hashtable previousItemCost = new Hashtable();
        private NameValueCollection ConfigData = null;        
        private bool trace = false;
        private int updateCount = 0;

        #region Parameters
        public int UpdateCount
        {
            get { return updateCount; }
        }
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
        public Hashtable ChangeItemCost
        {
            get { return changeItemCost; }
        }
        public Hashtable PatientPrice
        {
            get { return patientPrice; }        //PREVIOUSLY  return changeItemCost;
            set { patientPrice = value; }
        }
        public string SQLSelect
        {
            set { sqlSelect = value; }
        }
        public string ConnectString
        {
            get { return uwmConnectStr; }
            set { uwmConnectStr = value; }
        }
        public string Location
        {
            get { return location; }
            set { location = value; }
        }
        public Hashtable ItemCostToChange
        {
            get { return changeItemCost; }
        }        
        public bool Verbose
        {
            set { verbose = value; }
        }
        public bool Debug
        {
            set { debug = value; }
        }       
        #endregion
        #endregion

        public UpdatePatCharges()
        {
            ConfigData = (NameValueCollection)ConfigurationSettings.GetConfig("PatientChargeUpdate");
            biAdminConnectStr = ConfigData.Get("cnctBIAdmin");
            uwmConnectStr = ConfigData.Get("cnctHEMM_HMC");
            OkToUpdate = Convert.ToBoolean(ConfigData.Get("updateTables"));
            trace = Convert.ToBoolean(ConfigData.Get("trace"));
            ODMDataSetFactory = new ODMDataFactory();
            if (trace) lm.Write("TRACE:  UpdatePatCharges.UpdatePatCharges(constructor)");
        }
      
        public void GetPatientPrice()
        {//INCREMENTAL
            if (trace) lm.Write("TRACE:  UpdatePatCharges.GetPatientPrice()");
            GetItemChanges();
            if (ChangeItemCost.Count > 0) //this is the point where the app stops when there are no cost changes.
            {
                CalculatePatientPrice();
                /////////set  OkToUpdate with the config file variable "updateTables"
                /////////false allows you to see which items are going to be changed (check the log file) without changing them
               if(OkToUpdate)
                   UpdateTables();
            }
        }

        private void GetItemChanges()
        {
            if (trace) lm.Write("TRACE:  UpdatePatCharges.GetItemChanges()");
            try
            {
                GetPreviousItemCostList();//this comes from uwm_IVPItemCost
                GetCurrentItemCost(); //this works for HEMM but not MPOUS - the itemID's in MPOUS are different from those in HEMM
                currentItemCost = ConvertToHashTable(itemCost);
                if (verbose)
                    Console.WriteLine(currentItemCost.Count + " records");
                CompareCost();
            }
            catch (Exception ex)
            {
                lm.Write("UpdatePatCharges: GetItemChanges:  " + ex.Message);
                errMssg.Notify += "UpdatePatCharges: GetItemChanges:  " + ex.Message + Environment.NewLine;
            }
        }          

        private void GetPreviousItemCostList()
        {//gets the previously stored item_id/cost values from the uwm_BIAdmin database
            if (trace) lm.Write("TRACE:  UpdatePatCharges.GetPreviousItemCostList()");
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = ConfigData.Get("cnctBIAdmin");
            Request.CommandType = CommandType.Text;
            Request.Command = "Select ITEM_ID, COST from uwm_BIAdmin.dbo.uwm_IVPItemCost";
            try
            {
                previousItemCost = ConvertToHashTable(ODMDataSetFactory.ExecuteDataSetBuild(ref Request));
            }
            catch (Exception ex)
            {
                lm.Write("UpdatePatCharges: GetPreviousItemCostList:  " + ex.Message);
                errMssg.Notify += "UpdatePatCharges: GetPreviousItemCostList:  " + ex.Message + Environment.NewLine;
            }
        }

        private void CompareCost()
        {//INCREMENTAL
            if (trace) lm.Write("TRACE:  UpdatePatCharges.CompareCost()");
            int itemID = 0;
            string prevCost = "";
            string crntCost = "";
            //compare the itemID's from previous & current Item Cost hashtables
            //when they match, compare the two costs. if the costs don't match then
            //fill the hashtable with the cost values that need to be updated
            foreach (DictionaryEntry pic in previousItemCost)
            {
                try
                {
                    itemID = Convert.ToInt32(pic.Key);
                    prevCost = pic.Value.ToString();
                    if (currentItemCost.ContainsKey(itemID))
                    {
                        crntCost = currentItemCost[itemID].ToString();
                        if (prevCost != crntCost)
                        {
                            changeItemCost.Add(itemID, crntCost);  //items that had a cost change are captured here
                            lm.Write("Cost Change:   (id-old-new)" + TAB + itemID + TAB + FormatDollarValue(prevCost) + TAB + FormatDollarValue(crntCost));
                        }
                    }
                }
                catch (Exception ex)
                {
                    lm.Write("UpdatePatCharges: CompareCost:  " + ex.Message);
                    errMssg.Notify += "UpdatePatCharges: CompareCost:  " + ex.Message + Environment.NewLine;
                }
            }
            updateCount = changeItemCost.Count;
            lm.Write("UpdatePatCharges.CompareCost: updateCount = " + updateCount);
            if(updateCount == 0)
                lm.Write("UpdatePatCharges.CompareCost: There were no patient charges to update on the HEMM side.");
        }

        public void UpdateTables()
        {//INCREMENTAL
            if (trace) lm.Write("TRACE:  UpdatePatCharges.UpdateTables()");
            UpdatePatientCharge();
            if (OkToUpdate)
                RefreshPreviousValuTable();
        }

        private void UpdatePatientCharge()
        {//INCREMENTAL
            if (trace) lm.Write("TRACE:  UpdatePatCharges.UpdatePatientCharge()");
            PatientCharges pc = new PatientCharges();
            if(debug)
                pc.ConnectStr = ConfigData.Get("cnctBIAdmin");  //////// USE THIS FOR TEST 
            else
            {
                pc.ConnectStr = ConfigData.Get("cnctHEMM_HMC");   //////// USE THIS FOR PRODUCTION 
            }           
            pc.PatientPrice = patientPrice;
            pc.Debug = debug;
            pc.Trace = trace;
            pc.Verbose = verbose;
            if (OkToUpdate)
            {
                pc.UpdateCharges();
               // updateCount = pc.UpdateCount;  //commented out for test, now getting this count from line# 166
            }
        }
        
        private string FormatDollarValue(string dlrValu)
        {
            if (trace) lm.Write("TRACE:  UpdatePatCharges.FormatDollarValue()");
            string[] dollars = dlrValu.Split(".".ToCharArray());
            if (dollars.Length > 1)
            {
                if (dollars[1].Length == 1)
                {
                    dlrValu += "0";
                }
            }
            else
                dlrValu += ".00";
            return dlrValu;
        }
       
    }
}