﻿using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Collections;
using LogDefault;
using DTUtilities;



namespace PCUConsole
{
    class Program
    {
        /*
         * TABLES USED IN UWM_BIADMIN
         * uwm_IVPItemCost -- This holds the last current item cost values (since the previous update to pat_chrg_values)
         * uwm_New_PatientChargeTierLevels -- This holds the current Tier values which tells you which multiplier to use to get the pat_chrg values from a given item cost
         * uwm_MPOUS_LocProcCode -- This holds the last current Location Procedure Code from MPOUS (since the previous update to pat_chrg_values)    
         * uwm_SLOC_ITEM -- Used to test this application; it's a stand-in for the real HEMM.SLOC_ITEM table         
         * [uwm_D_INVENTORY_ITEMS] -- MPOUS.  Used to test this application; it's a stand-in for the real D_INVENTORY_ITEMS table. 
        
         * TABLES USED IN PointOfUseSuply (MPOUS)
         * D_INVENTORY_ITEMS
         * AHI_ITEM_ALIAS
         
         * TABLES USED IN HEMM
         * ITEM
         * ITEM_VEND_PKG
         * SLOC_ITEM
         * */
        #region Class Variables
        private static string logFilePath = "";
        private static LogManager lm = LogManager.GetInstance();
        private static ErrorMonitor errMssg = ErrorMonitor.GetInstance();
        private static char TAB = Convert.ToChar(9);
        private static NameValueCollection ConfigData = null;
        private static Hashtable dollarLimits = new Hashtable();
        private static Hashtable multiplierValu = new Hashtable();
        private static Hashtable xpnse_accnt = new Hashtable();
        private static Hashtable prevCostTable = new Hashtable();
        private static DateTimeUtilities dtu = new DateTimeUtilities();
        private static string locationCode = "";
        private static ArrayList locations;
        private static bool verbose = false;
        private static bool debug = true;
        private static bool trace = false;
        private static string currentTask = ""; //"incremental" or "full"        
        private static string dbugText = "";
        private static int updateCount = 0;
        private static int mpousCount = 0;
        #endregion
        static void Main(string[] args)
        {
//    Determine whether to read from the test db or the prod db

                    /*  To run without updating any data set the "updateTables" config value to false. To run without updating
                        AND test the RefreshPreviousValuTable() method, set the "updateTables" config value to true and then
                        comment out these lines:
                        Program - #114 & #54
                        UpdatePatCharges - #204
                        PatChrgChanges - #123
                    */

            try
            {
              //checked the App.config  <updateTables> value

                //USE THESE NEXT 2 LINES IF ALL YOU NEED TO DO IS RECREATE THE [uwm_MPOUS_LocProcCode] TABLE
                ////////////////PointOfUse pou = new PointOfUse();
                ////////////////pou.RefreshPreviousValues();
                //Set App.config <updateTables> = true. As a precaution, comment the rest of this Main()
                //down to the closing brace of the try block. - location is marked by this //^^<>^^

                ConfigData = (NameValueCollection)ConfigurationSettings.GetConfig("PatientChargeUpdate");
                debug = Convert.ToBoolean(ConfigData.Get("debug"));
                trace = Convert.ToBoolean(ConfigData.Get("trace"));
                verbose = Convert.ToBoolean(ConfigData.Get("verbose"));
                xpnse_accnt.Add("hmc",ConfigData.Get("h-xpnse_accnt"));
                xpnse_accnt.Add("uwmc", ConfigData.Get("u-xpnse_accnt"));
                xpnse_accnt.Add("nw", ConfigData.Get("n-xpnse_accnt"));
                prevCostTable.Add("hmc", ConfigData.Get("h-prev_cost_table"));
                prevCostTable.Add("uwmc", ConfigData.Get("u-prev_cost_table"));
                prevCostTable.Add("nwh", ConfigData.Get("n-prev_cost_table"));
                lm.LogFile = ConfigData.Get("logFile") + dtu.DateTimeCoded() + ".txt";
                lm.LogFilePath = ConfigData.Get("logFilePath");
                lm.Debug = debug;

                if (trace) lm.Write("TRACE:  Program.Main()");
                ////////this was used to test the ErrorMonitor class - it records the error messages in the catch block
                ////////comment out every line from the IF stmnt to the call to SendMail below
                ////errMssg.Notify += "Program: Main: " + "ErrorMonitor Test" + Environment.NewLine;
                ////SendEmail();

                if (args.Length == 0 || debug)
                {
                    currentTask = ConfigData.Get("task");    //full or incremental
                    locationCode = ConfigData.Get("location_code");  //....see comment in the app.config file
                }
                else
                {
                    locationCode = args[0];  //16=HMC; 4=MPOUS; 20=HMC&MPOUS; 28=HMC&MPOUS&UW  etc. See PCUpdate.ParseLocationCode()
                    currentTask = args[1];  //full or incremental
                    debug = args.Length > 2 ? Convert.ToBoolean(args[2]) : false; //true = debug mode
                }
                dbugText = debug ? "DEBUG" : "";
                lm.Write("Update Tables: " + ConfigData.Get("updateTables"));
                lm.Write("Trace: " + trace);
                lm.Write(("Debug: " + debug));
                lm.Write("PCUConsole.Program: Start " + locations + "   Type: " + currentTask);
                if (verbose)
                    Console.WriteLine(Environment.NewLine + "Running... " + dbugText);

                //THIS IS HERE FOR TESTING (and bypassing ProcessFiles())
                ////////DataManager dm = new DataManager();
                ////////dm.GetCurrentTierValues();
                ////////MPOUSProcessFiles(dm); 
                // END TEST

                ProcessFiles();
                if (locations.Contains("mpous"))
                {
                    lm.Write("PCUConsole.Program: MPOUS UPDATES FOLLOW:");                    
                    MPOUSProcessFiles();
                }
                lm.Write("PCUConsole.Program: PCUConsole End " + dbugText);
                if (verbose)
                    Console.WriteLine("Complete");
                //^^<>^^   To recreate the [uwm_MPOUS_LocProcCode] table comment out the try block to this point
                SendEmail();
            }
            catch (Exception ex)
            {
                lm.Write("Program: Main: " + ex.Message + Environment.NewLine);
                errMssg.Notify += "Program: Main: " + ex.Message + Environment.NewLine;
            }
        }
      
        private static void ProcessFiles()
        {
            if (trace) lm.Write("TRACE:  Program.ProcessFiles()");
            PCUpdate pcu = new PCUpdate();
            pcu.LocationCode = Convert.ToByte(locationCode);
            pcu.CurrentTask = currentTask;
            pcu.Verbose = verbose;
            pcu.Debug = debug;
            pcu.Trace = trace;
            pcu.Xpnse_accnt = xpnse_accnt;
            pcu.PrevCostTable = prevCostTable;
            pcu.Process();
            updateCount = pcu.UpdateCount;
            dollarLimits = pcu.DollarLimits;
            multiplierValu = pcu.MultiplierValu;
            locations = pcu.Locations;
        }

        private static void MPOUSProcessFiles()       //add this for testing only MPOUS section -- (DataManager dm)
        {
            if (trace) lm.Write("TRACE:  Program.MPOUSProcessFiles()");
            DataManager dm = new DataManager();
            MPOUSCharges mc = new MPOUSCharges();
            mc.UwmConnectStr = ConfigData.Get("cnctHEMM_HMC");
            mc.MpousCnctString = ConfigData.Get("cnctMPOUS");
            mc.Debug = debug;
            mc.Trace = trace;
            dm.GetCurrentTierValues("mpous");
            mc.DollarLimits = dm.DollarLimits;  //dollarLimits;
            mc.MultiplierValu = dm.MultiplierValu; //multiplierValu
            mc.ProcessPOU();
            mpousCount = mc.Count;
        }

        private static void SendEmail()
        {
            if (trace) lm.Write("TRACE:  Program.SendEmail()");
            OutputManager om = new OutputManager();
            om.EmailList = ConfigData.Get("recipients");
            om.BackupPath = ConfigData.Get("backup_path");
            om.UpdateCount = updateCount;
            om.MpousCount = mpousCount;
            om.SendEmail();
        }
    }
}
