using System;
using System.Collections.Generic;
using System.Text;
using k8s.Models;

namespace KubeUpdateCheck
{
    class ContainerToUpdate
    {
        public string ContainerName
        {
            get;
            set;
        }

        public ImageReference FromVersion
        {
            get;
            set;
        }
        public ImageReference ToVersion
        {
            get;
            set;
        }
    }
}
