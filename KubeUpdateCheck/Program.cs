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
                KubernetesClientConfiguration config = KubernetesClientConfiguration.InClusterConfig();
                IKubernetes kubernetes = new Kubernetes(config);

                UpgradeChecker checker = new UpgradeChecker(kubernetes);
                checker.PerformUpdate();
            } catch (Exception e) {
                Console.WriteLine("Encountered exception: {0}", e.Message);
                SentrySdk.CaptureException(e);
                throw e;
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

            Console.WriteLine("Sentry.io enabled.");
            return SentrySdk.Init(dsn);
        }
    }
}
