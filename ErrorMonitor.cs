using System;
using System.Threading;
using LogDefault;

namespace PCUConsole
{
    class ErrorMonitor
    {

        private static ErrorMonitor errMon = null;
        protected static LogManager lm = LogManager.GetInstance();
        protected string errMssg = "";
        public string Notify
        {
            get { return errMssg; }
            set { errMssg = value; }
        }

        //this is used to communicate error messages (from catch blocks) to the OutputManager 

        private ErrorMonitor()
        {
            /// <summary>
            ///constructor for the ErrorMonitor class. This overrides the default constructor and forces the use of GetInstance
            /// </summary>
            /// <returns>void</returns>
        }

        public static ErrorMonitor GetInstance()
        {
            if (errMon == null)
            {
                CreateInstance();
            }
            return errMon;
        }

        private static void CreateInstance()
        {
            try
            {
                Mutex configMutex = new Mutex();
                configMutex.WaitOne();
                errMon = new ErrorMonitor();
                configMutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                lm.Write("ErrorMonitor: CreateInstance  " + ex.Message);
            }
        }
    }
}
