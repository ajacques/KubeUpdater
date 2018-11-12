using System;
using System.Net.Mail;

namespace KubeUpdateCheck.Notifications
{
    class EmailConfig
    {
        private EmailConfig()
        {
        }

        public bool ShouldSend
        {
            get;
            private set;
        }
        public MailAddress ToAddress
        {
            get;
            private set;
        }
        public MailAddress FromAddress
        {
            get;
            private set;
        }
        public string RelayHost
        {
            get;
            private set;
        }
        public int RelayPort
        {
            get;
            private set;
        }

        public static EmailConfig GetFromEnvironment()
        {
            var envVariables = Environment.GetEnvironmentVariables();
            bool shouldSend = envVariables.Contains("APP_EMAIL_HOST");
            if (!shouldSend)
            {
                return new EmailConfig()
                {
                    ShouldSend = false
                };
            }

            return new EmailConfig()
            {
                ShouldSend = envVariables.Contains("APP_EMAIL_HOST"),
                ToAddress = new MailAddress(envVariables["APP_EMAIL_TO"].ToString()),
                FromAddress = new MailAddress("kube-update@" + Environment.MachineName, "Kube Update"),
                RelayHost = envVariables["APP_EMAIL_HOST"].ToString(),
                RelayPort = int.Parse(envVariables["APP_EMAIL_PORT"].ToString())
            };
        }
    }
}
