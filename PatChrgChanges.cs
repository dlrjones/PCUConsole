using System;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using OleDBDataManager;

namespace PCUConsole
{
    class PatChrgChanges : PCUCost
    {//PatChrgChanges is invoked when the user clicks the "Annual GetItemChanges" button on the PatientChargeUpdate UI 
        #region Class Variables
        private NameValueCollection ConfigData = null;
        private string connectStr = "";
        

        #region Parameters
        public Hashtable PrevCostTable
        {
            set { prevCostTable = value; }
        }
        public Hashtable DollarLimits
        {
            set { dollarLimits = value; }
        }
        public Hashtable MultiplierValu
        {
            set { multiplierValu = value; }
        }
        public string Location
        {
            set { location = value; }
        }
        public string ConnectString
        {
            set { connectStr = value; }
        }
        public string SQLSelect
        {
            set { sqlSelect = value; }
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

        public PatChrgChanges()
        {
            if (trace) lm.Write("TRACE:  PatChrgChanges.PatChrgChanges(constructor)");
            // /////PRODUCTION  HERE...
            ODMDataSetFactory = new ODMDataFactory();
            ConfigData = (NameValueCollection)ConfigurationSettings.GetConfig("PatientChargeUpdate");
            biAdminConnectStr = ConfigData.Get("cnctBIAdmin");
            mpousConnectStr = ConfigData.Get("cnctMPOUS_TEST");
            OkToUpdate = Convert.ToBoolean(ConfigData.Get("updateTables"));
            /// ///// ... to HERE
        }

        public void SetNewPatientCharges()
        {//FULL UPDATE
            if (trace) lm.Write("TRACE:  PatChrgChanges.SetNewPatientCharges()");

            uwmConnectStr = connectStr;
            try
            {
                if (itemCost.Tables.Count < 1)
                {
                    GetCurrentItemCost();
                    if (verbose)
                        Console.WriteLine(itemCost.Tables[0].Rows.Count + " records" + Environment.NewLine +
                                          "Calculating New Prices");
                }
                if (patientPrice.Count == 0) 
                    CalculatePrice();
                //if (location.Equals("mpous"))             ///////////////redirects to MPOUS changes 
                //    UpdateMPOUSCharges();
                else
                {
                    UpdatePatientCharge();
                }
            }
            catch (Exception ex)
            {
                lm.Write(ex.Message);
            }
        }

        private void UpdateMPOUSCharges()
        {
            //if (trace) lm.Write("TRACE:  PatChrgChanges.UpdateMPOUSCharges()");
            //PointOfUse pou = new PointOfUse();
            //pou.Verbose = verbose;
            //pou.Debug = debug;
            //pou.PriceToPatient = patientPrice; //key= HEMM item_id  value= new PatPrice
            //pou.ProcessMPOUSChanges();
            //pou.RefreshPreviousValues();
        }

        private void UpdatePatientCharge()
        {//FULL UPDATE
            if (trace) lm.Write("TRACE:  PatChrgChanges.UpdatePatientCharge()");

            PatientCharges pc = new PatientCharges();
            if (debug)
                pc.ConnectStr = biAdminConnectStr;  //use for TEST
            else
            {   
                pc.ConnectStr = ConfigData.Get("cnctHEMM_TEST");  //////// USE THIS FOR PRODUCTION cnctHCM_TEST
            }
            pc.PatientPrice = patientPrice;    
            pc.Debug = debug;
            pc.Trace = trace;
            pc.Verbose = verbose;
            //          pc.UpdateCharges(); //Used to test this application     - COMMENT OUT TO PREVENT UPDATING uwm_BIAdmin.dbo.uwm_SLOC_ITEM  - 

            if (OkToUpdate)
            {
                pc.UpdateCharges();
                RefreshPreviousValuTable();
            }
            else
            {
                foreach (int itemID in patientPrice.Keys)
                {
                    lm.Write("UPDATE VALUE:" + TAB + "(id-PC$)    " + itemID.ToString() + TAB + pc.FormatDollarValue(patientPrice[itemID].ToString()));
                }
            }
        }       
    }
}
