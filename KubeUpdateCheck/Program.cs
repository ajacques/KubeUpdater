using k8s;
using Sentry;
using System;

namespace KubeUpdateCheck
{
    class Program
    {

        static void Main(string[] args)
        {
            IDisposable sentry = ConfigureErrorReporting();

            try
            {
                KubernetesClientConfiguration config = KubernetesClientConfiguration.BuildConfigFromConfigFile("kubeconfig.yaml");
                IKubernetes kubernetes = new Kubernetes(config);

                UpgradeChecker checker = new UpgradeChecker(kubernetes);
                checker.PerformUpdate();
            } catch (Exception e) {
                SentrySdk.CaptureException(e);
            } finally {
                if (sentry != null)
                {
                    sentry.Dispose();
                }
            }
        }

        private static IDisposable ConfigureErrorReporting()
        {
            string dsn = Environment.GetEnvironmentVariable("SENTRY_DSN");

            if (dsn == null)
            {
                return null;
            }

            return SentrySdk.Init(dsn);
        }
    }
}
