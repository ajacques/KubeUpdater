using System;
using k8s;

namespace KubeUpdateCheck
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile("./kubeconfig.yaml");
            IKubernetes kubernetes = new Kubernetes(config);

            var result = kubernetes.ListNamespace();
            result.ToString();
        }
    }
}
