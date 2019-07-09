using System;
using System.Collections;
using System.Data;
using OleDBDataManager;
using LogDefault;

namespace PCUConsole
{
    class PatientCharges
    {
        #region Class Variables
        private string connectStr = "";
        private LogManager lm = LogManager.GetInstance();
        private ErrorMonitor errMssg = ErrorMonitor.GetInstance();
        private Hashtable patientPrice = new Hashtable();
        private ODMDataFactory ODMDataSetFactory = null;
        private bool debug = false;
        private bool trace = false;
        private bool verbose = false;
        protected char TAB = Convert.ToChar(9);
        private int updateCount = 0;

        #region Parameters
        public bool Verbose
        {
            set { verbose = value; }
        }

        public bool Debug
        {
            set { debug = value; }
        }
        public int UpdateCount
        {
            get { return updateCount; }
        }
        public bool Trace
        {
            set { trace = value; }
        }
        public string ConnectStr
        {
            get { return connectStr; }
            set { connectStr = value; }
        }
        public Hashtable PatientPrice
        {
            get { return patientPrice; }
            set { patientPrice = value; }
        }        
        #endregion
        #endregion

        public PatientCharges()
        {
            ODMDataSetFactory = new ODMDataFactory();  
        }

        public void UpdateCharges()
        {
            if (trace) lm.Write("TRACE:  PatientCharges.UpdateCharges()");
            //INCREMENTAL & FULL
            ODMRequest Request = new ODMRequest();
            Request.ConnectString = connectStr;
            Request.CommandType = CommandType.Text;
            string command = "update SLOC_ITEM set REC_UPDATE_DATE = GETDATE(), REC_UPDATE_USR_ID = 2827, PAT_CHRG_PRICE = "; //////// USE THIS FOR PRODUCTION
            //user_id 2827 = sv_pmm_jobs

            if (debug)
            {
                command = "update SLOC_ITEM set REC_UPDATE_DATE = GETDATE(), REC_UPDATE_USR_ID = 2827, PAT_CHRG_PRICE = "; //use this for TEST
            }
                                                                           
            if (verbose)
            {
                Console.WriteLine("Updating Patient Charges");
                Console.WriteLine("Each dot = 1000 records");
            }
            lm.Write("PCUConsole.PatientCharges: UpdateCharges: " + "patientPrice.Keys Count: " + patientPrice.Keys.Count);
            int itemCount = 1;   //used for the Verbose Output section

            foreach (int itemID in patientPrice.Keys)   //gives the number of charges to update
            {
                updateCount = patientPrice.Keys.Count;
              //  lm.Write("updateCount = " + updateCount + " itemID = " + itemID);  //for test

                #region verbose output
                if (verbose)
                {
                    if (++itemCount%1000 == 0)
                    {
                        if ((itemCount/1000)%5 == 0)
                            Console.Write(itemCount/1000);
                        else
                        {
                            Console.Write(".");
                        }
                    }
                }
                #endregion
                try
                {                  
                    Request.Command = command + FormatDollarValue(patientPrice[itemID].ToString()) + " WHERE ITEM_ID = " +
                                      itemID.ToString();
                    ODMDataSetFactory.ExecuteNonQuery(ref Request);
                    lm.Write("UPDATE VALUE:" + TAB + itemID.ToString() + TAB + FormatDollarValue(patientPrice[itemID].ToString()));
                }
                catch (Exception ex)
                {
                    lm.Write("PatientCharges: UpdateCharges:  " + ex.Message);
                    errMssg.Notify += "PatientCharges: UpdateCharges:  " + ex.Message + Environment.NewLine;
                }
            }
        }

        public string FormatDollarValue(string dlrValu)
        {
            if (trace) lm.Write("TRACE:  PatientCharges.FormatDollarValue()");
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
