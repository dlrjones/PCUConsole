using System;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using DLRUtilityCollection;

namespace PCUConsole 
{
    //LogManager.LogManager lm = LogManager.LogManager.GetInstance();
    //lm.LogFilePath = ConfigData.Get("logFilePath");
    //        lm.LogFile = ConfigData.Get("logFile");
    //        lm.Write("Test from the TestApp");

    public class LogManager
    {
        #region class variables
        private NameValueCollection ConfigData = null;
        private string logFilePath = "";
        private string logFile = "";
        private string corp = "";
        private bool debug = false;
        private string TAB = "        ";             //Convert.ToChar(9);
        private static LogManager logMngr = null;

        //public ArrayList OutgoingData
        //{
        //    set { outgoingData = value; }
        //}
        public bool Debug
        {
            set { debug = value; }
        }
        public string LogFilePath
        {
            set { logFilePath = value; }
        }
        public string LogFile
        {
            set { DateTimeUtilities dtu = new DateTimeUtilities();
                    logFile = value + dtu.DateTimeCoded() + ".txt";}
        }
        #endregion

        /// <summary>
        ///constructor for the LogMngr class. It requires that the path to the log File
        ///be passed in as parameters.
        /// </summary>
        /// <returns>void</returns>
        private LogManager()
        {
            // this constructor is private to force the calling program to use GetInstance()
            //GetInstance();            
        }

        /// <summary>
        /// provide the properties logFilePath with the trailing "\"
        /// provide the fileName with the extension
        /// </summary>
        /// <returns></returns>
        public static LogManager GetInstance()
        {
            if (logMngr == null)
            {
                CreateInstance();
            }
            return logMngr;
        }

        private static void CreateInstance()
        {
            try
            {
                Mutex configMutex = new Mutex();
                configMutex.WaitOne();
                //GetNew();
                logMngr = new LogManager();
                configMutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                int x = 0;
            }
        }

        private static void GetNew()
        {
            logMngr = new LogManager();
        }

        private bool CheckForLogFile()
        {
            bool goodToGo = false;
            //if (logFilePath.Length == 0)
            //{
            //logFilePath = logFilePath + logFile;
            //}
            if (logFilePath.Length > 0 && logFile.Length > 0)
            {
                if (!File.Exists(logFilePath + logFile))
                {
                    File.AppendAllText(logFilePath + logFile, "Application Log" + Environment.NewLine);
                }
                goodToGo = true;
            }
            return goodToGo;
        }

        /// <summary>
        /// Writes the contents of an ArrayList to the log file.
        /// </summary>
        /// <param name="list">The ArrayList to be logged.</param>
        /// <returns>void</returns>
        public void Write(ArrayList list)
        {
            string writeText = "";
            if (CheckForLogFile())
            {
                try
                {
                    if (debug)
                        Write("LogMngr/Write:  " + "LogMngr.Write()");
                    foreach (string item in list)
                    {
                        writeText += item.Trim() + TAB;
                    }
                    Write(writeText);
                }
                catch (Exception ex)
                {
                    Write("LogMngr/Write:  " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Writes the user entered text to the log file.
        /// </summary>
        /// <param name="logText">The string value to be logged.</param>
        /// <returns>void</returns>
        public void Write(string logText)
        {
            if (CheckForLogFile())
            {
                if (logText.Length > 0)
                    File.AppendAllText(logFilePath + logFile, DateTime.Now + TAB.ToString() + logText + Environment.NewLine);
            }
        }
    }
}
