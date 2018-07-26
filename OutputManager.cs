using System;
using System.Net.Mail;
using System.IO;
using KeyMaster;
using LogDefault;

namespace PCUConsole
{
    class OutputManager
    {
        private LogManager lm = LogManager.GetInstance();
        private ErrorMonitor errMssg = ErrorMonitor.GetInstance();
        private string emailList = "";
        private string backupPath = "";
        private string subject = "Patient Charge Update Ran Successfully";
        private int updateCount = 0;
        private int mpousCount = 0;
        #region Parameters
        public int UpdateCount
        {
            set { updateCount = value; }
        }
        public int MpousCount
        {
            set { mpousCount = value; }
        }
        public string BackupPath
        {
            set { backupPath = value; }
        }
        public string EmailList
        {
            set { emailList = value; }
        }
        #endregion

        public void SendEmail()
        {           
                SendMail();           
        }

        private void SendMail()
        {
            string[] mailList = emailList.Split(";".ToCharArray());
            try
            {
                if (errMssg.Notify.Length > 0) {
                    subject = "Patient Charge Update TERMINATED with ERRORS - Check the log for details";
                    errMssg.Notify = Environment.NewLine +
                                     Environment.NewLine +
                                     "Error Message: " +
                                     Environment.NewLine +
                                     errMssg.Notify;
                }
                else {
                    subject += " (" + updateCount + " HEMM & " + mpousCount + " MPOUS items affected.)";
                    errMssg.Notify = ""; 
                }

                foreach (string recipient in mailList)
                {
                    if (recipient.Trim().Length > 0)
                    {
                        MailMessage mail = new MailMessage();
                        SmtpClient SmtpServer = new SmtpClient("smtp.uw.edu");
                        mail.From = new MailAddress("pmmhelp@uw.edu");
                        mail.To.Add(recipient);                                               
                        mail.Subject = subject;
                        mail.Body = "The log will tell you which items were affected (if any). You'll find it here: " + Environment.NewLine +
                                    @"\\lapis\h_purchasing$\purchasing\pmm is data\reference logs\HEMMApps\PatientChargeUpdate\Logs" + Environment.NewLine +
                                    "The file name begins with \"PCULog_\" followed by the date and time that the application ran." +
                                    errMssg.Notify + 
                                    Environment.NewLine +
                                    Environment.NewLine +
                                    Environment.NewLine +
                                    Environment.NewLine +
                                    Environment.NewLine +
                                    "PMMHelp" + Environment.NewLine +
                                    "UW Medicine Harborview Medical Center" + Environment.NewLine +
                                    "Supply Chain Management Informatics" + Environment.NewLine +
                                    "206-598-0044" + Environment.NewLine +
                                    "pmmhelp@uw.edu";
                        mail.ReplyToList.Add("pmmhelp@uw.edu");

                        SmtpServer.Port = 587;
                        SmtpServer.Credentials = new System.Net.NetworkCredential("pmmhelp", GetKey());
                        SmtpServer.EnableSsl = true;
                        SmtpServer.Send(mail);                        
                    }
                }
            }
            catch (Exception ex)
            {
                lm.Write("Process/SendMail_:  " + ex.Message);
            }
        }

        protected string GetKey()
        {
            string[] key = File.ReadAllLines(backupPath + "PCUKey.txt");
            return StringCipher.Decrypt(key[0], "PCUpdate");
        }
    }
}
