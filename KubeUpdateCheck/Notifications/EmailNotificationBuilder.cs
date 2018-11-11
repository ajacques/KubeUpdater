using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Mail;

namespace KubeUpdateCheck.Notifications
{
    class EmailNotificationBuilder
    {
        public string BuildMessage(ICollection<ContainerToUpdate> containerUpdates)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Kubernetes Update Results");
            sb.AppendLine();
            sb.AppendFormat("{0} total updates", containerUpdates.Count);
            sb.AppendLine();
            foreach (var container in containerUpdates) {
                sb.Append(container.ContainerName)
                    .AppendLine()
                    .Append("  From: ")
                    .Append(container.FromVersion)
                    .AppendLine()
                    .Append("  To: ")
                    .Append(container.ToVersion)
                    .AppendLine();
            }

            return sb.ToString();
        }
    }
}
